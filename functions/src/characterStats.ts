import * as crypto from "crypto";

// Réplica exata de CharacterCreation.GenerateLevel1Stats() (Assets/Scripts/Utils/
// CharacterCreation.cs, ver CLAUDE.md "Geração de stats no level 1") — extraído de
// purchaseCase.ts (2026-07-26) pra ser reaproveitado também por rebirthCharacter.ts, sem
// duplicar a mesma função em dois arquivos. Distribui 9 pontos aleatórios entre HP (+5/ponto),
// STR/AGI/SPD (+1/ponto cada), 25% de chance cada por ponto, sem teto por atributo — mesmo
// algoritmo, mesmas proporções do lado Unity, só trocando UnityEngine.Random por
// crypto.randomInt (nunca Math.random() pra nada que decide recompensa do jogador).
export interface Level1Stats {
  maxHealth: number;
  str: number;
  agility: number;
  speed: number;
}

const BASE_LEVEL1_STATS: Level1Stats = { maxHealth: 55, str: 2, agility: 2, speed: 2 };
const LEVEL1_STAT_POOL_POINTS = 9;

export function generateLevel1Stats(): Level1Stats {
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

// Réplica de XpSystem.XpRequired(level) (ver CLAUDE.md "XpSystem") — só o caso level=1 é usado
// hoje (rebirthCharacter.ts sempre reseta pro level 1), mas a fórmula inteira é mirrorada pra não
// deixar um "mágico 5" solto sem explicação/fonte de verdade.
export function xpRequiredForLevel(level: number): number {
  return level <= 6 ? level + 4 : 2 * level - 2;
}
