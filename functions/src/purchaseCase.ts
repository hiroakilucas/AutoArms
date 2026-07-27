import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import { charactersOfRarity } from "./characterCatalog";
import { CasePackageDoc, DEFAULT_TIER_WEIGHTS, RARITY_COUNT } from "./casePackageTypes";
import { generateLevel1Stats } from "./characterStats";
import { secureRandomIndex, rollWeightedPool, rarityOfCharacter } from "./caseRoll";
import { buildNewCharacterDocData, nowTicks } from "./grantCharacter";

// generateLevel1Stats() vive em ./characterStats.ts, o sorteio ponderado (secureRandomIndex/
// rollWeightedPool/rarityOfCharacter) em ./caseRoll.ts, e o shape do documento concedido em
// ./grantCharacter.ts (todos extraídos em 2026-07-26) — reaproveitados também por
// rebirthCharacter.ts/purchaseNextCharacter.ts, sem duplicar nenhum dos três aqui.

interface PurchaseCaseRequestData {
  packageId?: string;
  paymentReceipt?: string;
}

interface PurchaseCaseResponseData {
  wonCharacterTypeId: string;
  // ID do documento recém-criado em users/{uid}/characters (2026-07-24, sistema de compra de
  // personagens/case opening → seleção) — distinto de wonCharacterTypeId (o MOLDE). O client
  // precisa deste ID pra destacar a instância certa em 02_SelectCharacter depois da compra
  // (CaseOpeningPopup → PendingCharacterSelection → CharacterSelectController).
  grantedCharacterId: string;
  remainingPurchasesForPlayer: number; // -1 = sem limite
  reelPoolCharacterTypeIds: string[];
}

export const purchaseCase = onCall<PurchaseCaseRequestData>(
  { region: "southamerica-east1" },
  async (request): Promise<PurchaseCaseResponseData> => {
    const uid = request.auth?.uid;
    if (!uid) {
      throw new HttpsError("unauthenticated", "É necessário estar autenticado para comprar um case.");
    }

    const packageId = request.data?.packageId;
    if (!packageId || typeof packageId !== "string") {
      throw new HttpsError("invalid-argument", "packageId ausente.");
    }
    const paymentReceipt = request.data?.paymentReceipt;

    const db = admin.firestore();
    const packageRef = db.collection("casePackages").doc(packageId);
    const purchaseRef = db.collection("users").doc(uid).collection("casePurchases").doc(packageId);
    const userRef = db.collection("users").doc(uid);
    const charactersRef = db.collection("users").doc(uid).collection("characters");

    return db.runTransaction(async (tx) => {
      // Todas as leituras da transaction primeiro (exigência do Firestore) — inclusive a query
      // de personagens já possuídos.
      const [packageSnap, purchaseSnap, userSnap, ownedSnap] = await Promise.all([
        tx.get(packageRef),
        tx.get(purchaseRef),
        tx.get(userRef),
        tx.get(charactersRef),
      ]);

      if (!packageSnap.exists) {
        throw new HttpsError("not-found", "Pacote não encontrado.");
      }
      const pkg = packageSnap.data() as CasePackageDoc;

      // 1) Validação de pagamento.
      if (pkg.cashPrice > 0) {
        if (!paymentReceipt || typeof paymentReceipt !== "string" || paymentReceipt.trim().length === 0) {
          throw new HttpsError("invalid-argument", "Recibo de pagamento ausente.");
        }
        // TODO: validar recibo real via App Store Server API / Google Play Developer API antes de
        // produção. Mock (fase atual, sem gateway de pagamento real — ver MONETIZACAO.md): aceita
        // qualquer paymentReceipt não vazio como válido.
      } else if (pkg.currencyCost > 0) {
        const diamonds = userSnap.exists ? Number(userSnap.data()?.diamonds ?? 0) : 0;
        if (diamonds < pkg.currencyCost) {
          throw new HttpsError("failed-precondition", "Saldo de diamantes insuficiente.");
        }
      } else {
        throw new HttpsError("failed-precondition", "Pacote mal configurado (sem cashPrice nem currencyCost).");
      }

      // 2) Limite de compras por jogador (MONETIZACAO.md seção 5 — 10/3/1 pros pacotes cash;
      // <=0 = sem limite, usado pelo pacote de moeda).
      const purchasedCount = purchaseSnap.exists ? Number(purchaseSnap.data()?.purchasedCount ?? 0) : 0;
      const limit = pkg.purchaseLimitPerPlayer ?? 0;
      if (limit > 0 && purchasedCount >= limit) {
        throw new HttpsError("resource-exhausted", "Limite de compras atingido para este pacote.");
      }

      // 3) Personagens já possuídos (characterTypeId, não characterId — vários characterId podem
      // apontar pro mesmo template em tese, embora a regra de não-repetição abaixo evite isso).
      const ownedTypeIds = new Set<string>();
      ownedSnap.forEach((doc) => {
        const typeId = doc.data()?.characterTypeId;
        if (typeId) ownedTypeIds.add(typeId);
      });

      // 4) Monta pool elegível.
      let pool: string[];
      if (pkg.isRarityLocked) {
        pool = charactersOfRarity(pkg.rarityTier).filter((id) => !ownedTypeIds.has(id));
      } else {
        const weights = pkg.tierWeights && pkg.tierWeights.length === RARITY_COUNT ? pkg.tierWeights : DEFAULT_TIER_WEIGHTS;
        pool = rollWeightedPool(weights, ownedTypeIds);
      }
      if (pool.length === 0) {
        throw new HttpsError("failed-precondition", "pool esgotada");
      }

      // 5) Sorteia o vencedor (crypto, nunca Math.random()).
      const wonCharacterTypeId = pool[secureRandomIndex(pool.length)];
      const level1Stats = generateLevel1Stats();

      // 6) Grava a concessão — um documento novo em users/{uid}/characters, nunca reaproveitando
      // um characterId existente (mesmo personagem-molde pode ser concedido mais de uma vez a
      // contas diferentes, mas nunca duas vezes à MESMA conta — já garantido pelo filtro acima).
      const newCharRef = charactersRef.doc();
      const rarity = pkg.isRarityLocked ? pkg.rarityTier : rarityOfCharacter(wonCharacterTypeId);
      tx.set(newCharRef, buildNewCharacterDocData(newCharRef.id, wonCharacterTypeId, rarity, level1Stats, nowTicks()));

      // 7) Incrementa o contador de compras por jogador.
      tx.set(purchaseRef, { purchasedCount: purchasedCount + 1 }, { merge: true });

      // 8) Debita diamante, se pago em moeda (cash não debita nada — o "pagamento" já foi
      // validado no passo 1 via recibo).
      if (pkg.cashPrice <= 0 && pkg.currencyCost > 0) {
        tx.set(userRef, { diamonds: admin.firestore.FieldValue.increment(-pkg.currencyCost) }, { merge: true });
      }

      const remainingPurchasesForPlayer = limit > 0 ? Math.max(0, limit - (purchasedCount + 1)) : -1;

      return {
        wonCharacterTypeId,
        grantedCharacterId: newCharRef.id,
        remainingPurchasesForPlayer,
        reelPoolCharacterTypeIds: pool,
      };
    });
  }
);
