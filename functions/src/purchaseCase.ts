import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as crypto from "crypto";
import { charactersOfRarity, characterCatalog } from "./characterCatalog";
import { CasePackageDoc, DEFAULT_TIER_WEIGHTS, RARITY_COUNT } from "./casePackageTypes";

// Ticks .NET (100ns desde 0001-01-01) equivalentes ao epoch Unix (1970-01-01) — mesma unidade de
// CharacterDTO.updatedAtTicks (DateTime.UtcNow.Ticks no lado C#), pra este documento ficar
// comparável com qualquer outro gravado pelo cliente (LocalSaveService/FirestoreService).
const DOTNET_EPOCH_TICKS = 621355968000000000;

// Loadout inicial de personagem novo (mesmo conjunto de "Reset All Profiles to Level 1" no Editor
// Unity, CharacterCreationEditor.cs — Satyr1/Golem3/Succubus/Zombie, todos tier 1) — sem isso um
// personagem recém-concedido nasceria com o loadout vazio (sempre desarmado em combate).
const DEFAULT_STARTING_WEAPONS = [
  { name: "Satyr1", tier: 1 },
  { name: "Golem3", tier: 1 },
  { name: "Succubus", tier: 1 },
  { name: "Zombie", tier: 1 },
];

// Réplica exata de CharacterCreation.GenerateLevel1Stats() (Assets/Scripts/Utils/
// CharacterCreation.cs, ver CLAUDE.md "Geração de stats no level 1") — bug real corrigido
// (2026-07-25, reportado pelo usuário: personagem comprado vinha sempre com os valores BASE
// mínimos, sem a distribuição aleatória de pontos que "Tools > AutoArms > Reset All Profiles to
// Level 1" e qualquer personagem pré-autorado do projeto sempre tiveram). A versão anterior
// gravava só BASE_LEVEL1_STATS direto, deliberadamente sem RNG "nesta primeira versão" — nunca
// foi corrigido depois. Distribui 9 pontos aleatórios entre HP (+5/ponto), STR/AGI/SPD (+1/ponto
// cada), 25% de chance cada por ponto, sem teto por atributo — mesmo algoritmo, mesmas
// proporções, só trocando UnityEngine.Random por crypto.randomInt (mesmo padrão de segurança já
// usado no resto desta function - nunca Math.random() pra nada que decide recompensa do
// jogador).
const BASE_LEVEL1_STATS = { maxHealth: 55, str: 2, agility: 2, speed: 2 };
const LEVEL1_STAT_POOL_POINTS = 9;

function generateLevel1Stats(): { maxHealth: number; str: number; agility: number; speed: number } {
  const stats = { ...BASE_LEVEL1_STATS };
  for (let i = 0; i < LEVEL1_STAT_POOL_POINTS; i++) {
    switch (crypto.randomInt(0, 4)) {
      case 0: stats.maxHealth += 5; break;
      case 1: stats.str += 1; break;
      case 2: stats.agility += 1; break;
      case 3: stats.speed += 1; break;
    }
  }
  return stats;
}

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

function secureRandomIndex(length: number): number {
  return crypto.randomInt(0, length);
}

function secureRandomUnit(): number {
  // crypto.randomInt é inclusive/exclusive em inteiros — mapeia pra um float em [0, 1).
  return crypto.randomInt(0, 1_000_000_000) / 1_000_000_000;
}

function weightedRandomTier(weights: number[]): number {
  const total = weights.reduce((sum, w) => sum + Math.max(0, w), 0);
  if (total <= 0) return 0;
  let roll = secureRandomUnit() * total;
  for (let tier = 0; tier < weights.length; tier++) {
    const w = Math.max(0, weights[tier]);
    if (roll < w) return tier;
    roll -= w;
  }
  return weights.length - 1;
}

// Sorteia um tier pelas tierWeights e devolve o pool elegível daquele tier (excluindo possuídos);
// se o pool vier vazio, resorteia o tier algumas vezes antes de cair pro fallback "qualquer tier
// não vazio" (da mais comum pra mais rara) — ver spec original (ETAPA 3, item 6).
function rollWeightedPool(weights: number[], owned: Set<string>): string[] {
  const maxAttempts = 20;
  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    const tier = weightedRandomTier(weights);
    const pool = charactersOfRarity(tier).filter((id) => !owned.has(id));
    if (pool.length > 0) return pool;
  }
  for (let tier = 0; tier < RARITY_COUNT; tier++) {
    const pool = charactersOfRarity(tier).filter((id) => !owned.has(id));
    if (pool.length > 0) return pool;
  }
  return [];
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
      const nowTicks = Date.now() * 10000 + DOTNET_EPOCH_TICKS;
      tx.set(newCharRef, {
        characterId: newCharRef.id,
        characterTypeId: wonCharacterTypeId,
        profileName: wonCharacterTypeId,
        level: 1,
        winRate: 0,
        xpCurrent: 0,
        xpRequired: 6,
        battlesRemaining: 6,
        isFavorite: false,
        rarity: pkg.isRarityLocked ? pkg.rarityTier : rarityOfCharacter(wonCharacterTypeId),
        maxHealth: level1Stats.maxHealth,
        str: level1Stats.str,
        agility: level1Stats.agility,
        speed: level1Stats.speed,
        armor: 0,
        evasion: 0,
        accuracy: 0,
        initiative: 0,
        reversal: 0,
        counter: 0,
        blockBonus: 0,
        reversalAfterBlock: 0,
        criticalChance: 0,
        hitSpeed: 1,
        weapons: DEFAULT_STARTING_WEAPONS,
        skills: [],
        pets: [],
        updatedAtTicks: nowTicks,
      });

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

function rarityOfCharacter(characterTypeId: string): number {
  // Usado só pra preencher o campo `rarity` (cosmético) do documento concedido quando o pacote é
  // de moeda (pool mistura raridades) — o pacote de raridade travada já sabe o tier de antemão
  // (pkg.rarityTier), então só este branch precisa consultar o catálogo.
  return characterCatalog[characterTypeId] ?? 0;
}
