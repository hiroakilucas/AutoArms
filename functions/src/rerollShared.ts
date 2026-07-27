import { HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import { drawUnlock, CharacterRosterDoc, UnlockResult } from "./unlockEngine";

// Núcleo compartilhado do "reroll de um slot já concedido, a custo FIXO em diamante" — extraído
// de rerollUnlock.ts (2026-07-26) pra ser reaproveitado por rerollRebirthGrant.ts sem duplicar a
// lógica de validação/transaction/sorteio. Custo (2026-07-25, deliberadamente FIXO — não escala,
// diferente do "Novo Sorteio" do level-up de combate) e limite por slot são os MESMOS pros dois
// chamadores; o que muda entre eles é só QUAL campo do documento guarda o contador de uso (ver
// `countersField` abaixo) — cada fluxo (unlock de case-opening vs. item concedido por
// Renascimento) precisa do seu PRÓPRIO namespace de contador, senão um Renascimento herdaria (ou
// contaminaria) a contagem de refresh de uma sequência de case-opening antiga do mesmo
// personagem, ou de um Renascimento anterior.
export const REROLL_COST_DIAMONDS = 15;
export const MAX_REROLLS_PER_SLOT = 2;

export interface RerollSlotResult {
  kind: string;
  name: string;
  tier: number;
  remainingRerolls: number;
}

// `countersField` = nome do campo no documento do personagem que guarda o mapa
// `{ [slotIndex]: usedCount }` (ex: "unlockRerollCounts" pro case-opening, "rebirthUnlockRerollCounts"
// pro Renascimento). Assume que TODAS as leituras da transaction já foram feitas pelo chamador
// (exigência do Firestore) — esta função só faz a validação de negócio + os writes.
export function performSlotReroll(
  tx: FirebaseFirestore.Transaction,
  userRef: FirebaseFirestore.DocumentReference,
  charRef: FirebaseFirestore.DocumentReference,
  userSnap: FirebaseFirestore.DocumentSnapshot,
  charSnap: FirebaseFirestore.DocumentSnapshot,
  countersField: string,
  slotIndex: number
): RerollSlotResult {
  if (!charSnap.exists) {
    throw new HttpsError("not-found", "Personagem não encontrado.");
  }
  const character = charSnap.data() as CharacterRosterDoc;

  // 1) Limite de refreshes POR SLOT (contador server-side, nunca confiar num contador só no
  // client, senão dá pra burlar reabrindo a tela/reenviando a chamada).
  const rerollCounts = ((charSnap.data()?.[countersField] ?? {}) as Record<string, number>);
  const key = String(slotIndex);
  const usedSoFar = rerollCounts[key] ?? 0;
  if (usedSoFar >= MAX_REROLLS_PER_SLOT) {
    throw new HttpsError("resource-exhausted", "Limite de refreshes atingido para este item.");
  }

  // 2) Saldo de diamante (nunca confia no cliente).
  const diamonds = userSnap.exists ? Number(userSnap.data()?.diamonds ?? 0) : 0;
  if (diamonds < REROLL_COST_DIAMONDS) {
    throw new HttpsError("failed-precondition", "Saldo de diamantes insuficiente.");
  }

  // 3) Resorteia com as MESMAS regras do sorteio original (odds real por família + tier por
  // posse, ver unlockEngine.ts) — lê `character` (o documento JÁ COMMITADO), então "o que já
  // possui" está sempre correto.
  const result: UnlockResult | null = drawUnlock(character);
  if (!result) {
    throw new HttpsError("internal", "Falha ao sortear novo resultado (catálogo de unlocks vazio ou sem candidato elegível).");
  }

  // 4) Debita diamante + incrementa o contador de refresh — atômico com o sorteio, na mesma
  // transaction.
  tx.set(userRef, { diamonds: admin.firestore.FieldValue.increment(-REROLL_COST_DIAMONDS) }, { merge: true });
  const newCount = usedSoFar + 1;
  tx.set(charRef, { [countersField]: { ...rerollCounts, [key]: newCount } }, { merge: true });

  return {
    kind: result.kind,
    name: result.name,
    tier: result.tier,
    remainingRerolls: MAX_REROLLS_PER_SLOT - newCount,
  };
}
