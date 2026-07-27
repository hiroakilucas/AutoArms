import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";

// Resgates gratuitos de diamante — Diário/Semanal/Mensal (2026-07-27, pedido do usuário, aba
// Diamantes da Loja). Mesma regra inegociável de "moeda premium nunca client-writable"
// (ARQUITETURA.md "Moeda premium") — 100% decidido e creditado aqui (Admin SDK), o cliente só
// envia a INTENÇÃO de resgatar (sem payload nenhum, cada tipo é uma function própria).
//
// Fuso horário ÚNICO/GLOBAL: America/Sao_Paulo (Horário de Brasília), independente de onde o
// jogador está fisicamente — pedido explícito do usuário. Brasil não observa horário de verão
// desde 2019 (decreto federal, sem previsão de retorno), então o offset seria fixo (UTC-3) na
// prática — mas usamos `Intl.DateTimeFormat` com o timeZone IANA em vez de hardcodar "-3":
// Node/Cloud Functions roda com ICU completo por padrão (sem custo de dependência nova), e isso
// continua correto de graça se essa política mudar de novo no futuro.
const TIME_ZONE = "America/Sao_Paulo";
const FUNCTIONS_REGION = "southamerica-east1";

interface SaoPauloParts {
  year: number;
  month: number; // 1-12
  day: number;
  weekday: number; // 0=domingo .. 6=sábado (mesma convenção de Date.getUTCDay())
}

const WEEKDAY_INDEX: Record<string, number> = { Sun: 0, Mon: 1, Tue: 2, Wed: 3, Thu: 4, Fri: 5, Sat: 6 };

function saoPauloParts(date: Date): SaoPauloParts {
  const fmt = new Intl.DateTimeFormat("en-US", {
    timeZone: TIME_ZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    weekday: "short",
  });
  const map: Record<string, string> = {};
  for (const part of fmt.formatToParts(date)) map[part.type] = part.value;
  return {
    year: Number(map.year),
    month: Number(map.month),
    day: Number(map.day),
    weekday: WEEKDAY_INDEX[map.weekday],
  };
}

function pad2(n: number): string {
  return n < 10 ? `0${n}` : String(n);
}

// Chave do dia atual em São Paulo — muda exatamente à meia-noite (00:00) de Brasília.
function dailyPeriodKey(parts: SaoPauloParts): string {
  return `${parts.year}-${pad2(parts.month)}-${pad2(parts.day)}`;
}

// Chave do mês atual em São Paulo — muda todo dia 1º às 00:00 de Brasília.
function monthlyPeriodKey(parts: SaoPauloParts): string {
  return `${parts.year}-${pad2(parts.month)}`;
}

// Chave da semana atual = data (YYYY-MM-DD) da SEGUNDA-FEIRA que iniciou essa semana em São
// Paulo — muda toda segunda-feira às 00:00 de Brasília. `weekday` é 0=domingo..6=sábado;
// `(weekday + 6) % 7` converte pra "dias desde a última segunda" (segunda=0, domingo=6). Usa
// meio-dia UTC (não meia-noite) como âncora pra aritmética de dias inteiros nunca escorregar por
// causa de arredondamento — só a CALENDÁRIO (Y/M/D) importa aqui, a hora do dia é descartada.
function weeklyPeriodKey(parts: SaoPauloParts): string {
  const daysSinceMonday = (parts.weekday + 6) % 7;
  const noonUtcMs = Date.UTC(parts.year, parts.month - 1, parts.day, 12);
  const mondayMs = noonUtcMs - daysSinceMonday * 86400000;
  const monday = new Date(mondayMs);
  return `${monday.getUTCFullYear()}-${pad2(monday.getUTCMonth() + 1)}-${pad2(monday.getUTCDate())}`;
}

interface RewardConfig {
  amount: number;
  fieldName: "dailyLastPeriod" | "weeklyLastPeriod" | "monthlyLastPeriod";
  periodKeyFn: (parts: SaoPauloParts) => string;
  label: string; // só pra mensagem de erro
}

const DAILY_CONFIG: RewardConfig = { amount: 5, fieldName: "dailyLastPeriod", periodKeyFn: dailyPeriodKey, label: "diário" };
const WEEKLY_CONFIG: RewardConfig = { amount: 30, fieldName: "weeklyLastPeriod", periodKeyFn: weeklyPeriodKey, label: "semanal" };
const MONTHLY_CONFIG: RewardConfig = { amount: 100, fieldName: "monthlyLastPeriod", periodKeyFn: monthlyPeriodKey, label: "mensal" };

interface ClaimResponseData {
  diamondsGranted: number;
  newDiamondsBalance: number;
}

// Núcleo compartilhado pelas 3 functions abaixo (mesmo princípio de rerollShared.ts — um único
// caminho de validação/crédito, cada function só passa a própria config). O estado de período
// mora num documento SEPARADO do users/{uid} principal —
// users/{uid}/rewardsState/diamonds, protegido por regra própria (`allow write: if false`, ver
// firestore.rules) — diferente de coins/diamonds no doc principal (ainda client-writable, TODO
// de segurança pré-existente, ver WalletService.cs comentário de topo). Sem essa separação, um
// cliente malicioso poderia escrever direto no próprio campo de período (users/{uid} aceita
// qualquer write do dono hoje) pra resetá-lo pra uma data antiga e resgatar de novo no mesmo
// dia/semana/mês — a function sozinha não bastaria pra impedir isso se o dado que ela CONSULTA
// pudesse ser forjado pelo cliente.
async function claimReward(uid: string, config: RewardConfig): Promise<ClaimResponseData> {
  const db = admin.firestore();
  const userRef = db.collection("users").doc(uid);
  const stateRef = userRef.collection("rewardsState").doc("diamonds");
  const periodKey = config.periodKeyFn(saoPauloParts(new Date()));

  return db.runTransaction(async (tx) => {
    // Todas as leituras primeiro (exigência do Firestore).
    const [userSnap, stateSnap] = await Promise.all([tx.get(userRef), tx.get(stateRef)]);

    const lastPeriod = stateSnap.exists ? stateSnap.data()?.[config.fieldName] : null;
    if (lastPeriod === periodKey) {
      throw new HttpsError("failed-precondition", `Resgate ${config.label} já feito neste período.`);
    }

    const currentDiamonds = userSnap.exists ? Number(userSnap.data()?.diamonds ?? 0) : 0;
    const newDiamonds = currentDiamonds + config.amount;

    tx.set(userRef, { diamonds: admin.firestore.FieldValue.increment(config.amount) }, { merge: true });
    tx.set(stateRef, { [config.fieldName]: periodKey }, { merge: true });

    return { diamondsGranted: config.amount, newDiamondsBalance: newDiamonds };
  });
}

function requireUid(uid: string | undefined, label: string): string {
  if (!uid) throw new HttpsError("unauthenticated", `É necessário estar autenticado para resgatar o diamante ${label}.`);
  return uid;
}

export const claimDailyDiamonds = onCall({ region: FUNCTIONS_REGION }, async (request): Promise<ClaimResponseData> =>
  claimReward(requireUid(request.auth?.uid, DAILY_CONFIG.label), DAILY_CONFIG)
);

export const claimWeeklyDiamonds = onCall({ region: FUNCTIONS_REGION }, async (request): Promise<ClaimResponseData> =>
  claimReward(requireUid(request.auth?.uid, WEEKLY_CONFIG.label), WEEKLY_CONFIG)
);

export const claimMonthlyDiamonds = onCall({ region: FUNCTIONS_REGION }, async (request): Promise<ClaimResponseData> =>
  claimReward(requireUid(request.auth?.uid, MONTHLY_CONFIG.label), MONTHLY_CONFIG)
);
