import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import { performSlotReroll, RerollSlotResult } from "./rerollShared";

// Refresh dos unlocks progressivos de skill/arma/pet do case opening (2026-07-25) — custo FIXO
// em diamante (não escala, diferente do "Novo Sorteio" do level-up de combate, que dobra a cada
// uso — dois sistemas econômicos deliberadamente separados, sem lógica de custo compartilhada).
// Mesmo princípio de segurança de `purchaseCase.ts`: diamante e limite de refresh são sempre
// validados/decididos aqui (Admin SDK), nunca confiando no cliente — ver ARQUITETURA.md "Moeda
// premium (diamantes) — regra de segurança inegociável".
//
// A validação/sorteio/débito em si foi extraída pra ./rerollShared.ts (2026-07-26,
// `performSlotReroll`) pra ser reaproveitada por rerollRebirthGrant.ts sem duplicar a lógica —
// este arquivo só resolve o request/transaction e aponta pro campo de contador certo
// (`unlockRerollCounts`, namespace exclusivo do case-opening).

// Mesma region de purchaseCase.ts — precisa bater com o que o client (UnlockRerollService.cs)
// espera; ver nota lá sobre FirebaseFunctions.DefaultInstance apontar pra us-central1 por padrão.
const FUNCTIONS_REGION = "southamerica-east1";

interface RerollUnlockRequestData {
  characterId?: string;
  unlockIndex?: number;
}

export const rerollUnlock = onCall<RerollUnlockRequestData>(
  { region: FUNCTIONS_REGION },
  async (request): Promise<RerollSlotResult> => {
    const uid = request.auth?.uid;
    if (!uid) {
      throw new HttpsError("unauthenticated", "É necessário estar autenticado para usar refresh.");
    }

    const characterId = request.data?.characterId;
    if (!characterId || typeof characterId !== "string") {
      throw new HttpsError("invalid-argument", "characterId ausente.");
    }
    const unlockIndex = request.data?.unlockIndex;
    if (typeof unlockIndex !== "number" || !Number.isInteger(unlockIndex) || unlockIndex < 1) {
      throw new HttpsError("invalid-argument", "unlockIndex inválido.");
    }

    const db = admin.firestore();
    const userRef = db.collection("users").doc(uid);
    const charRef = userRef.collection("characters").doc(characterId);

    return db.runTransaction(async (tx) => {
      // Todas as leituras primeiro (exigência do Firestore).
      const [userSnap, charSnap] = await Promise.all([tx.get(userRef), tx.get(charRef)]);
      return performSlotReroll(tx, userRef, charRef, userSnap, charSnap, "unlockRerollCounts", unlockIndex);
    });
  }
);
