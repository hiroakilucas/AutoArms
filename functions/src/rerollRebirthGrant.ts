import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import { performSlotReroll, RerollSlotResult } from "./rerollShared";

// Refresh individual de um item concedido pelo Renascimento (rebirthCharacter.ts, 2026-07-26) —
// mesmo custo/limite/mecanismo de sorteio de rerollUnlock.ts (via performSlotReroll
// compartilhado, ver rerollShared.ts), só que gravando num campo de contador PRÓPRIO
// (`rebirthUnlockRerollCounts`) em vez de `unlockRerollCounts` (namespace do case-opening) —
// cada Renascimento reinicia esse campo do zero (ver rebirthCharacter.ts passo 6), então os
// índices 1..N de um Renascimento nunca colidem com uma sequência de case-opening antiga nem com
// um Renascimento anterior do mesmo personagem.
const FUNCTIONS_REGION = "southamerica-east1";

interface RerollRebirthGrantRequestData {
  characterId?: string;
  slotIndex?: number;
}

export const rerollRebirthGrant = onCall<RerollRebirthGrantRequestData>(
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
    const slotIndex = request.data?.slotIndex;
    if (typeof slotIndex !== "number" || !Number.isInteger(slotIndex) || slotIndex < 1) {
      throw new HttpsError("invalid-argument", "slotIndex inválido.");
    }

    const db = admin.firestore();
    const userRef = db.collection("users").doc(uid);
    const charRef = userRef.collection("characters").doc(characterId);

    return db.runTransaction(async (tx) => {
      const [userSnap, charSnap] = await Promise.all([tx.get(userRef), tx.get(charRef)]);
      return performSlotReroll(tx, userRef, charRef, userSnap, charSnap, "rebirthUnlockRerollCounts", slotIndex);
    });
  }
);
