import { onCall, HttpsError } from "firebase-functions/v2/https";
import * as admin from "firebase-admin";
import * as crypto from "crypto";
import { unlockCatalog } from "./unlockCatalog";
import { DEFAULT_STARTING_WEAPONS, DOTNET_EPOCH_TICKS } from "./grantCharacter";

// Concessão do 1º personagem da conta (onboarding, logo após criação de conta) — 2026-07-24.
// Deliberadamente uma function separada de purchaseCase.ts (mesmo padrão de segurança: Admin SDK,
// nunca confia em nada vindo do client), não uma variação dela — aqui não existe pagamento/
// diamante/pacote nenhum, só "conceder o personagem escolhido, uma única vez por conta", e
// misturar isso na lógica de case opening (preço, recibo, contador de compras) só aumentaria o
// acoplamento de um fluxo que já funciona. Mesma region de purchaseCase.ts/rerollUnlock.ts —
// precisa bater com o que o client (StarterCharacterService.cs) espera.
const FUNCTIONS_REGION = "southamerica-east1";

// Só estes 4 podem ser concedidos por aqui (telas de onboarding, ver ChooseFirstCharacterController)
// — stats extraídos direto dos PlayerProfile.asset reais (únicos campos que variam entre os 4; o
// resto do documento é 0/1 fixo, igual a purchaseCase.ts). Atualizar aqui se os .asset mudarem.
const STARTER_TEMPLATE_STATS: Record<string, { maxHealth: number; str: number; agility: number; speed: number }> = {
  "Medieval Warrior": { maxHealth: 70, str: 6, agility: 3, speed: 3 },
  "Medieval Warrior Girl": { maxHealth: 75, str: 5, agility: 3, speed: 3 },
  "Citizen 1": { maxHealth: 65, str: 5, agility: 4, speed: 4 },
  "Citizen Women 2": { maxHealth: 65, str: 5, agility: 4, speed: 4 },
};

interface GrantStarterCharacterRequestData {
  characterTypeId?: string;
}

interface GrantStarterCharacterResponseData {
  characterTypeId: string;
  grantedCharacterId: string;
  grantedSkillName: string;
}

interface SkillCandidate {
  name: string;
  odds: number;
}

function weightedPickSkill(pool: SkillCandidate[]): SkillCandidate {
  const total = pool.reduce((sum, c) => sum + Math.max(0, c.odds), 0);
  if (total <= 0) return pool[crypto.randomInt(0, pool.length)];

  let roll = (crypto.randomInt(0, 1_000_000_000) / 1_000_000_000) * total;
  for (const c of pool) {
    const w = Math.max(0, c.odds);
    if (roll < w) return c;
    roll -= w;
  }
  return pool[pool.length - 1]; // segurança contra erro de ponto flutuante
}

// Sorteia a 1ª skill "entre 2 opções, sem escolha manual" (pedido do usuário): pondera pelos odds
// reais do catálogo (mesmo peso que o resto do jogo usa) pra achar 2 candidatos DISTINTOS de tier
// 1, depois decide entre os dois com uma moeda justa — as odds decidem QUEM entra na disputa, a
// moeda decide o resultado final. Roda inteiramente aqui (server-side) pra não ser manipulável
// pelo cliente, igual à regra de qualquer sorteio que concede recompensa de verdade.
function rollStarterSkill(): string {
  const tier1Pool: SkillCandidate[] = (unlockCatalog.skills ?? [])
    .filter((s) => s.tiers.includes(1) && s.odds > 0)
    .map((s) => ({ name: s.name, odds: s.odds }));

  if (tier1Pool.length === 0) {
    throw new HttpsError("internal", "Catálogo de skills tier 1 vazio (ver Tools > AutoArms > Export Unlock Catalog for Cloud Function).");
  }

  const first = weightedPickSkill(tier1Pool);
  const remainingPool = tier1Pool.filter((c) => c.name !== first.name);
  const second = remainingPool.length > 0 ? weightedPickSkill(remainingPool) : first;

  const chosen = crypto.randomInt(0, 2) === 0 ? first.name : second.name;
  console.log(`[grantStarterCharacter] candidatos=[${first.name}, ${second.name}] escolhido=${chosen}`);
  return chosen;
}

export const grantStarterCharacter = onCall<GrantStarterCharacterRequestData>(
  { region: FUNCTIONS_REGION },
  async (request): Promise<GrantStarterCharacterResponseData> => {
    const uid = request.auth?.uid;
    if (!uid) {
      throw new HttpsError("unauthenticated", "É necessário estar autenticado para escolher o primeiro personagem.");
    }

    const characterTypeId = request.data?.characterTypeId;
    if (!characterTypeId || typeof characterTypeId !== "string" || !(characterTypeId in STARTER_TEMPLATE_STATS)) {
      throw new HttpsError("invalid-argument", "characterTypeId inválido.");
    }

    const db = admin.firestore();
    const charactersRef = db.collection("users").doc(uid).collection("characters");

    return db.runTransaction(async (tx) => {
      // Leitura primeiro (exigência do Firestore) — conta já possui QUALQUER personagem (não só
      // deste template)? Não há regra de `delete` em characters/{id} hoje, então este gate é
      // definitivo: uma vez concedido, não existe caminho client-side pra apagar e chamar de novo.
      const ownedSnap = await tx.get(charactersRef);
      if (!ownedSnap.empty) {
        throw new HttpsError("failed-precondition", "Esta conta já possui um personagem.");
      }

      const stats = STARTER_TEMPLATE_STATS[characterTypeId];
      const grantedSkillName = rollStarterSkill();

      const newCharRef = charactersRef.doc();
      const nowTicks = Date.now() * 10000 + DOTNET_EPOCH_TICKS;
      tx.set(newCharRef, {
        characterId: newCharRef.id,
        characterTypeId,
        profileName: characterTypeId,
        level: 1,
        winRate: 0,
        xpCurrent: 0,
        xpRequired: 6,
        battlesRemaining: 6,
        isFavorite: false,
        rarity: 0, // os 4 templates de onboarding são todos CharacterRarity.Normal
        maxHealth: stats.maxHealth,
        str: stats.str,
        agility: stats.agility,
        speed: stats.speed,
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
        skills: [{ name: grantedSkillName, tier: 1 }],
        pets: [],
        // true (não o default false) — sem isso, CharacterSelectController.ResolveCaseUnlocksAsync
        // trata este runtime instance como um personagem de case opening ainda não revelado e
        // concede MAIS um item (CharacterUnlockEngine.UnlockCountForRarity(Normal) == 1, sorteado
        // client-side) na primeira vez que o jogador abre o detalhe dele — um bônus de boas-vindas
        // do case opening, não algo pedido pro onboarding (que já tem sua própria skill, sorteada
        // aqui em cima). Marcar como já resolvido evita essa concessão duplicada/não intencional.
        caseUnlocksResolved: true,
        caseUnlocksAcceptedCount: 0,
        updatedAtTicks: nowTicks,
      });

      return {
        characterTypeId,
        grantedCharacterId: newCharRef.id,
        grantedSkillName,
      };
    });
  }
);
