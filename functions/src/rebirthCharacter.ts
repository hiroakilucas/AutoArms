import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import { drawUnlock, CharacterRosterDoc, UnlockResult } from "./unlockEngine";
import { generateLevel1Stats, xpRequiredForLevel } from "./characterStats";

// "Renascimento" (Reset Nível 10+, 2026-07-26) — feature NOVA, separada do "Resetar Personagem"
// antigo (100% client-side, CharacterPanel.ExecuteReset: grátis, sem gate de level, CREDITA
// moeda, limpa o loadout). ECONOMIA INVERTIDA (correção de escopo, 2026-07-26, mesmo dia —
// versão original desta function DEBITAVA moeda; o usuário corrigiu o requisito: Renascimento é
// 100% GRATUITO, e além disso CREDITA `nível_antes × CharacterRebirthSettings.coinRewardPerLevel`
// moedas, mesma fórmula de valor, só que como recompensa em vez de custo). Só libera em
// level >= 10 e CONCEDE N skills/armas/pets aleatórios (N pela raridade do personagem) em vez de
// limpar. Mesmo princípio de segurança de purchaseCase.ts/rerollUnlock.ts: level, crédito e
// sorteio são todos decididos aqui (Admin SDK), nunca confiando no cliente.
const REBIRTH_COIN_REWARD_PER_LEVEL = 10;
const REBIRTH_MIN_LEVEL = 10;

// Mesmo valor de EnergySettings.maxEnergy (Assets/ScriptableObjects/EnergySettings.cs) —
// Renascimento também reabastece a energia de batalha do personagem pro teto (pedido do usuário,
// 2026-07-26: "quando resetar o personagem precisa resetar a energia de batalha também"), mesmos
// campos de EnergyService.cs (energyCurrent/lastEnergyTimestamp). Mantido em sincronia manual com
// o lado Unity, mesmo padrão dos outros valores espelhados nesta function.
const REBIRTH_ENERGY_RESET_VALUE = 10;

// Mesma tabela de CharacterUnlockEngine.UnlockCountForRarity (Assets/Scripts/Utils/
// CharacterUnlockEngine.cs) — índice = (int)CharacterRarity (Normal=0/Uncommon=1/Rare=2/
// Legendary=3/Immortal=4, mesma convenção de casePackageTypes.ts/purchaseCase.ts). Mantido em
// sincronia manualmente com o lado Unity, mesmo padrão de unlockCatalog.ts.
const REBIRTH_ITEM_COUNT_BY_RARITY = [1, 2, 3, 4, 5];

const FUNCTIONS_REGION = "southamerica-east1";

interface RebirthCharacterRequestData {
  characterId?: string;
}

interface RebirthCharacterResponseData {
  level: number;
  maxHealth: number;
  str: number;
  agility: number;
  speed: number;
  grantedItems: UnlockResult[];
  coinsRewarded: number;
  newCoinsBalance: number;
}

interface RebirthCharacterDoc extends CharacterRosterDoc {
  level?: number;
  rarity?: number;
}

// Aplica um UnlockResult num roster acumulado localmente (mesma mecânica sequencial de
// CharacterUnlockEngine.DrawUnlock no client: cada sorteio já reflete os anteriores desta MESMA
// sequência, então a mesma família pode sair de novo e virar upgrade de tier em vez de
// duplicata) — nunca dois entries da mesma família/tipo no mesmo roster.
function applyUnlockResult(roster: Required<CharacterRosterDoc>, result: UnlockResult): void {
  if (result.kind === "skill") {
    const existing = roster.skills.find((s) => s.name === result.name);
    if (existing) existing.tier = result.tier;
    else roster.skills.push({ name: result.name, tier: result.tier });
  } else if (result.kind === "weapon") {
    const existing = roster.weapons.find((w) => w.name === result.name);
    if (existing) existing.tier = result.tier;
    else roster.weapons.push({ name: result.name, tier: result.tier });
  } else {
    const existing = roster.pets.find((p) => p.type === result.name);
    if (existing) existing.tier = result.tier;
    else roster.pets.push({ type: result.name, tier: result.tier });
  }
}

export const rebirthCharacter = onCall<RebirthCharacterRequestData>(
  { region: FUNCTIONS_REGION },
  async (request): Promise<RebirthCharacterResponseData> => {
    const uid = request.auth?.uid;
    if (!uid) {
      throw new HttpsError("unauthenticated", "É necessário estar autenticado para renascer um personagem.");
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
      const character = charSnap.data() as RebirthCharacterDoc;

      // 1) Elegibilidade — nível mínimo.
      const levelBefore = Number(character.level ?? 1);
      if (levelBefore < REBIRTH_MIN_LEVEL) {
        throw new HttpsError("failed-precondition", `Renascimento só é permitido a partir do level ${REBIRTH_MIN_LEVEL}.`);
      }

      // 2) Recompensa em moeda (nunca decidida no cliente) — SEM custo/validação de saldo,
      // Renascimento é gratuito (correção de escopo 2026-07-26).
      const reward = levelBefore * REBIRTH_COIN_REWARD_PER_LEVEL;
      const coinsBefore = userSnap.exists ? Number(userSnap.data()?.coins ?? 0) : 0;

      // 3) Quantidade de itens pela raridade (campo cosmético hoje, ganha efeito real aqui).
      const rarity = Number(character.rarity ?? 0);
      const itemCount = REBIRTH_ITEM_COUNT_BY_RARITY[rarity] ?? REBIRTH_ITEM_COUNT_BY_RARITY[0];

      // 4) Sorteia itemCount itens, acumulando um roster local do zero (mesmas regras reais de
      // odds/tier-por-posse de unlockEngine.ts, já usadas por rerollUnlock/case-opening).
      const roster: Required<CharacterRosterDoc> = { skills: [], weapons: [], pets: [] };
      const grantedItems: UnlockResult[] = [];
      for (let i = 0; i < itemCount; i++) {
        const result = drawUnlock(roster);
        if (!result) {
          throw new HttpsError("internal", "Falha ao sortear item de renascimento (catálogo de unlocks vazio ou sem candidato elegível).");
        }
        applyUnlockResult(roster, result);
        grantedItems.push(result);
      }

      // 5) Rerola status base do zero (mesma distribuição de personagem novo).
      const stats = generateLevel1Stats();

      // 6) Grava tudo atomicamente: level 1, stats novos, roster novo, contador de reroll
      // pós-rebirth reiniciado (campo PRÓPRIO, nunca reaproveitar unlockRerollCounts — evita
      // colidir com contagens de uma sequência de case-opening ou de um Renascimento anterior
      // deste mesmo personagem).
      tx.set(charRef, {
        level: 1,
        xpCurrent: 0,
        xpRequired: xpRequiredForLevel(1),
        battlesRemaining: 6,
        maxHealth: stats.maxHealth,
        str: stats.str,
        agility: stats.agility,
        speed: stats.speed,
        skills: roster.skills,
        weapons: roster.weapons,
        pets: roster.pets,
        rebirthUnlockRerollCounts: {},
        // Energia de batalha reabastecida pro teto — mesmo campo/formato de EnergyService.cs
        // (lastEnergyTimestamp reancorado em "agora", igual ao que a regeneração natural já faz
        // ao bater o teto — ver comentário sobre reancoragem em EnergyService.GetOrRegenAsync).
        energyCurrent: REBIRTH_ENERGY_RESET_VALUE,
        lastEnergyTimestamp: admin.firestore.FieldValue.serverTimestamp(),
      }, { merge: true });

      tx.set(userRef, { coins: admin.firestore.FieldValue.increment(reward) }, { merge: true });

      return {
        level: 1,
        maxHealth: stats.maxHealth,
        str: stats.str,
        agility: stats.agility,
        speed: stats.speed,
        grantedItems,
        coinsRewarded: reward,
        newCoinsBalance: coinsBefore + reward,
      };
    });
  }
);
