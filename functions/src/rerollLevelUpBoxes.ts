import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import { REROLL_COST_DIAMONDS } from "./rerollShared";

// Correção de segurança do "Novo Sorteio" (CombatResultPanel.cs, reroll das N caixas de
// level-up) — 2026-07-26, pedido do usuário ao mesmo tempo que a feature de Renascimento.
// Antes: 100% client-side, custo DOBRANDO (50→100→200 diamantes,
// `WalletService.SpendDiamondsAsync` gravando diamante direto do cliente) — TODO de segurança já
// sinalizado no próprio CombatResultPanel.cs. Correção: custo FIXO (REUSA
// REROLL_COST_DIAMONDS=15, o MESMO valor já usado por rerollUnlock/rerollRebirthGrant — não um
// valor novo) e validação/débito de diamante sempre aqui (Admin SDK), nunca confiando no
// cliente.
//
// ESCOPO DELIBERADAMENTE REDUZIDO em relação a rerollUnlock/rerollRebirthGrant: esta function só
// AUTORIZA o gasto (valida saldo, debita, controla o limite de usos) — o SORTEIO das N caixas em
// si (LevelUpEngine.DrawWeightedOption, com toda a lógica de pirâmide/pool "sem repetir
// nesta sequência"/resume-após-fechar-o-app) continua client-side, INALTERADO. Diferente do
// Renascimento (onde os itens só existem DENTRO daquela transação paga, sem nenhuma via
// gratuita), o conteúdo de cada caixa de level-up já é obtido de graça no fluxo normal — "Novo
// Sorteio" é só CONVENIÊNCIA para rerolar mais rápido, não uma recompensa exclusiva de
// pagamento. Portar o motor de sorteio inteiro (pirâmide + persistência de resume,
// `pendingLevelUpBoxes`, já com histórico extenso de bugs corrigidos) pra cá teria alto risco de
// regressão numa feature já validada, para fechar uma brecha de baixo valor econômico. Se essa
// brecha se tornar um problema real medido em produção, revisitar e portar o sorteio também
// (mesmo padrão de unlockEngine.ts/drawUnlock).
const FUNCTIONS_REGION = "southamerica-east1";
const MAX_REROLLS_PER_LEVELUP = 3;

interface RerollLevelUpBoxesRequestData {
  characterId?: string;
}

interface RerollLevelUpBoxesResponseData {
  remainingUses: number;
  remainingDiamonds: number;
}

export const rerollLevelUpBoxes = onCall<RerollLevelUpBoxesRequestData>(
  { region: FUNCTIONS_REGION },
  async (request): Promise<RerollLevelUpBoxesResponseData> => {
    const uid = request.auth?.uid;
    if (!uid) {
      throw new HttpsError("unauthenticated", "É necessário estar autenticado para usar o Novo Sorteio.");
    }

    const characterId = request.data?.characterId;
    if (!characterId || typeof characterId !== "string") {
      throw new HttpsError("invalid-argument", "characterId ausente.");
    }

    const db = admin.firestore();
    const userRef = db.collection("users").doc(uid);
    const charRef = userRef.collection("characters").doc(characterId);

    return db.runTransaction(async (tx) => {
      // Todas as leituras primeiro (exigência do Firestore).
      const [userSnap, charSnap] = await Promise.all([tx.get(userRef), tx.get(charRef)]);

      if (!charSnap.exists) {
        throw new HttpsError("not-found", "Personagem não encontrado.");
      }
      const character = charSnap.data() as { level?: number; levelUpRerollCount?: number; levelUpRerollAtLevel?: number };
      const currentLevel = Number(character.level ?? 1);

      // Contador escopado ao level ATUAL (server-side) — um level-up novo sempre é uma sessão de
      // escolha nova, então o contador reinicia sozinho quando o level salvo não bate mais com o
      // level do documento, sem precisar de um "session id" à parte.
      const storedAtLevel = Number(character.levelUpRerollAtLevel ?? -1);
      const usedSoFar = storedAtLevel === currentLevel ? Number(character.levelUpRerollCount ?? 0) : 0;
      if (usedSoFar >= MAX_REROLLS_PER_LEVELUP) {
        throw new HttpsError("resource-exhausted", "Limite de Novo Sorteio atingido para este level-up.");
      }

      const diamonds = userSnap.exists ? Number(userSnap.data()?.diamonds ?? 0) : 0;
      if (diamonds < REROLL_COST_DIAMONDS) {
        throw new HttpsError("failed-precondition", "Saldo de diamantes insuficiente.");
      }

      tx.set(userRef, { diamonds: admin.firestore.FieldValue.increment(-REROLL_COST_DIAMONDS) }, { merge: true });
      const newCount = usedSoFar + 1;
      tx.set(charRef, { levelUpRerollCount: newCount, levelUpRerollAtLevel: currentLevel }, { merge: true });

      return {
        remainingUses: MAX_REROLLS_PER_LEVELUP - newCount,
        remainingDiamonds: diamonds - REROLL_COST_DIAMONDS,
      };
    });
  }
);
