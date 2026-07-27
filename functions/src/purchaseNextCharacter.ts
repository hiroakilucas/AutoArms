import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import { DEFAULT_TIER_WEIGHTS } from "./casePackageTypes";
import { generateLevel1Stats } from "./characterStats";
import { secureRandomIndex, rollWeightedPool, rarityOfCharacter } from "./caseRoll";
import { buildNewCharacterDocData, nowTicks } from "./grantCharacter";

// "Próximo Personagem" (Coins, 2026-07-26) — card real na aba Personagens da Loja, substituindo
// "Case Geral" (diamante, `purchaseCase.ts`/pacote `case_moeda_geral`, removido). Mesma mecânica
// de sorteio de raridade (`rollWeightedPool` + `DEFAULT_TIER_WEIGHTS`, as MESMAS odds
// [68%, 20%, 8%, 3.5%, 0.5%] que o Case Geral já usava) e o mesmo shape de concessão de
// `purchaseCase.ts` (via ./grantCharacter.ts) — só a precificação muda: em vez de um preço FIXO
// por pacote (`casePackages/{id}.currencyCost`), o preço aqui escala com quantas vezes ESTE
// jogador já comprou (contador persistido `users/{uid}.nextCharacterPurchaseCount`), então não
// faz sentido modelar como mais um doc em `casePackages` (schema de preço único por pacote) — é
// uma function própria com sua própria tabela de preço.
const FUNCTIONS_REGION = "southamerica-east1";

// Tabela de preço (MONETIZACAO.md §6 original era 100/200/400/600/800/1000/+400 — substituída
// nesta correção de escopo pela tabela final abaixo, pedida pelo usuário 2026-07-26). Fonte da
// verdade é SEMPRE este array — o lado Unity (ShopController.CharacterSlotCost) só espelha pra
// exibição.
const PRICE_TABLE = [25, 50, 100, 200, 400, 800, 1200, 1400, 1600, 1800, 2000, 2200];
const PRICE_STEP_AFTER_TABLE = 200;

function priceForPurchaseNumber(n: number): number {
  if (n <= PRICE_TABLE.length) return PRICE_TABLE[n - 1];
  return PRICE_TABLE[PRICE_TABLE.length - 1] + PRICE_STEP_AFTER_TABLE * (n - PRICE_TABLE.length);
}

interface PurchaseNextCharacterResponseData {
  wonCharacterTypeId: string;
  grantedCharacterId: string;
  coinsSpent: number;
  newCoinsBalance: number;
  nextPurchaseCost: number;
  reelPoolCharacterTypeIds: string[];
}

export const purchaseNextCharacter = onCall(
  { region: FUNCTIONS_REGION },
  async (request): Promise<PurchaseNextCharacterResponseData> => {
    const uid = request.auth?.uid;
    if (!uid) {
      throw new HttpsError("unauthenticated", "É necessário estar autenticado para comprar um personagem.");
    }

    const db = admin.firestore();
    const userRef = db.collection("users").doc(uid);
    const charactersRef = userRef.collection("characters");

    return db.runTransaction(async (tx) => {
      // Todas as leituras primeiro (exigência do Firestore).
      const [userSnap, ownedSnap] = await Promise.all([tx.get(userRef), tx.get(charactersRef)]);

      const purchaseNumber = (userSnap.exists ? Number(userSnap.data()?.nextCharacterPurchaseCount ?? 0) : 0) + 1;
      const cost = priceForPurchaseNumber(purchaseNumber);

      const coins = userSnap.exists ? Number(userSnap.data()?.coins ?? 0) : 0;
      if (coins < cost) {
        throw new HttpsError("failed-precondition", "Saldo de moedas insuficiente.");
      }

      const ownedTypeIds = new Set<string>();
      ownedSnap.forEach((doc) => {
        const typeId = doc.data()?.characterTypeId;
        if (typeId) ownedTypeIds.add(typeId);
      });

      const pool = rollWeightedPool(DEFAULT_TIER_WEIGHTS, ownedTypeIds);
      if (pool.length === 0) {
        throw new HttpsError("failed-precondition", "pool esgotada");
      }

      const wonCharacterTypeId = pool[secureRandomIndex(pool.length)];
      const level1Stats = generateLevel1Stats();
      const rarity = rarityOfCharacter(wonCharacterTypeId);

      const newCharRef = charactersRef.doc();
      tx.set(newCharRef, buildNewCharacterDocData(newCharRef.id, wonCharacterTypeId, rarity, level1Stats, nowTicks()));

      tx.set(userRef, {
        coins: admin.firestore.FieldValue.increment(-cost),
        nextCharacterPurchaseCount: purchaseNumber,
      }, { merge: true });

      return {
        wonCharacterTypeId,
        grantedCharacterId: newCharRef.id,
        coinsSpent: cost,
        newCoinsBalance: coins - cost,
        nextPurchaseCost: priceForPurchaseNumber(purchaseNumber + 1),
        reelPoolCharacterTypeIds: pool,
      };
    });
  }
);
