import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import { drawUnlock, CharacterRosterDoc } from "./unlockEngine";

// Refresh dos unlocks progressivos de skill/arma/pet do case opening (2026-07-25) — custo FIXO
// em diamante (não escala, diferente do "Novo Sorteio" do level-up de combate, que dobra a cada
// uso — dois sistemas econômicos deliberadamente separados, sem lógica de custo compartilhada).
// Mesmo princípio de segurança de `purchaseCase.ts`: diamante e limite de refresh são sempre
// validados/decididos aqui (Admin SDK), nunca confiando no cliente — ver ARQUITETURA.md "Moeda
// premium (diamantes) — regra de segurança inegociável".
const REROLL_COST_DIAMONDS = 15;
const MAX_REROLLS_PER_UNLOCK = 2;

// Mesma region de purchaseCase.ts — precisa bater com o que o client (UnlockRerollService.cs)
// espera; ver nota lá sobre FirebaseFunctions.DefaultInstance apontar pra us-central1 por padrão.
const FUNCTIONS_REGION = "southamerica-east1";

interface RerollUnlockRequestData {
  characterId?: string;
  unlockIndex?: number;
}

interface RerollUnlockResponseData {
  kind: string;
  name: string;
  tier: number;
  remainingRerolls: number;
}

export const rerollUnlock = onCall<RerollUnlockRequestData>(
  { region: FUNCTIONS_REGION },
  async (request): Promise<RerollUnlockResponseData> => {
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

      if (!charSnap.exists) {
        throw new HttpsError("not-found", "Personagem não encontrado.");
      }
      const character = charSnap.data() as CharacterRosterDoc;

      // 1) Limite de refreshes POR UNLOCK (contador server-side, chave = índice do unlock dentro
      // da sequência de abertura deste personagem — nunca confiar num contador só no client, senão
      // dá pra burlar reabrindo a tela/reenviando a chamada).
      const rerollCounts = ((charSnap.data()?.unlockRerollCounts ?? {}) as Record<string, number>);
      const key = String(unlockIndex);
      const usedSoFar = rerollCounts[key] ?? 0;
      if (usedSoFar >= MAX_REROLLS_PER_UNLOCK) {
        throw new HttpsError("resource-exhausted", "Limite de refreshes atingido para este unlock.");
      }

      // 2) Saldo de diamante (nunca confia no cliente).
      const diamonds = userSnap.exists ? Number(userSnap.data()?.diamonds ?? 0) : 0;
      if (diamonds < REROLL_COST_DIAMONDS) {
        throw new HttpsError("failed-precondition", "Saldo de diamantes insuficiente.");
      }

      // 3) Resorteia com as MESMAS regras do sorteio original (odds real por família + tier por
      // posse, ver unlockEngine.ts) — nunca uma variação diferente. Lê `character` (o documento
      // JÁ COMMITADO — unlocks anteriores da mesma sequência já foram aceitos/salvos pelo client
      // antes deste), então "o que já possui" está sempre correto; o resultado ainda-não-aceito
      // deste MESMO unlock nunca chega a ser persistido antes do "Continuar", então não polui essa
      // leitura.
      const result = drawUnlock(character);
      if (!result) {
        throw new HttpsError("internal", "Falha ao sortear novo resultado (catálogo de unlocks vazio ou sem candidato elegível — ver Tools > AutoArms > Export Unlock Catalog for Cloud Function).");
      }

      // 4) Debita diamante + incrementa o contador de refresh — atômico com o sorteio, na mesma
      // transaction.
      tx.set(userRef, { diamonds: admin.firestore.FieldValue.increment(-REROLL_COST_DIAMONDS) }, { merge: true });
      const newCount = usedSoFar + 1;
      tx.set(charRef, { unlockRerollCounts: { ...rerollCounts, [key]: newCount } }, { merge: true });

      return {
        kind: result.kind,
        name: result.name,
        tier: result.tier,
        remainingRerolls: MAX_REROLLS_PER_UNLOCK - newCount,
      };
    });
  }
);
