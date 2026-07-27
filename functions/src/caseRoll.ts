import * as crypto from "crypto";
import { charactersOfRarity, characterCatalog } from "./characterCatalog";
import { RARITY_COUNT } from "./casePackageTypes";

// Sorteio ponderado de raridade + pool elegível — extraído de purchaseCase.ts (2026-07-26) pra
// ser reaproveitado por purchaseNextCharacter.ts sem duplicar a lógica (mesma regra de
// segurança: crypto.randomInt, nunca Math.random(), pra qualquer coisa que decide recompensa do
// jogador).

export function secureRandomIndex(length: number): number {
  return crypto.randomInt(0, length);
}

export function secureRandomUnit(): number {
  // crypto.randomInt é inclusive/exclusive em inteiros — mapeia pra um float em [0, 1).
  return crypto.randomInt(0, 1_000_000_000) / 1_000_000_000;
}

export function weightedRandomTier(weights: number[]): number {
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
export function rollWeightedPool(weights: number[], owned: Set<string>): string[] {
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

// Usado pra preencher o campo `rarity` (cosmético) do documento concedido quando o sorteio mistura
// raridades (pool não travado numa raridade única) — quem já sabe o tier de antemão (ex: pacote
// de raridade travada) não precisa consultar o catálogo.
export function rarityOfCharacter(characterTypeId: string): number {
  return characterCatalog[characterTypeId] ?? 0;
}
