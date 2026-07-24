import * as crypto from "crypto";
import { unlockCatalog, UnlockFamilyEntry } from "./unlockCatalog";

/**
 * Réplica server-side de `CharacterUnlockEngine.DrawUnlock` (Unity,
 * `Assets/Scripts/Utils/CharacterUnlockEngine.cs`) — usada só por `rerollUnlock.ts` (o sorteio
 * ORIGINAL dos unlocks, sem custo, continua 100% client-side, mesmo nível de confiança de
 * qualquer outra recompensa client-authoritative do level-up de combate). Precisa existir aqui
 * porque o refresh custa diamante de verdade — regra inegociável de ARQUITETURA.md ("Moeda
 * premium"): nada que gasta diamante pode ser decidido só pelo cliente.
 *
 * MESMA regra de tier do cliente: sorteia uma FAMÍLIA (ponderada pelo odds real, idêntico em
 * todos os tiers dela) e concede sempre `1 + maior tier que o personagem já possui daquela
 * família` — nunca um tier arbitrário. Família já no tier máximo (ou sem o próximo tier gerado,
 * ex: Garimpeiro/Magneto/Tamer) não desperdiça a tentativa, sorteia de novo.
 */

export type UnlockKind = "skill" | "weapon" | "pet";

export interface UnlockResult {
  kind: UnlockKind;
  name: string;
  tier: number;
}

// Mesmo shape gravado em CharacterDTO.weapons/skills/pets (ver CharacterDTOMap.cs) — é isso que
// vem de volta ao ler users/{uid}/characters/{characterId} no Firestore.
export interface CharacterRosterDoc {
  skills?: { name: string; tier: number }[];
  weapons?: { name: string; tier: number }[];
  pets?: { type: string; tier: number }[];
}

interface PoolCandidate {
  kind: UnlockKind;
  entry: UnlockFamilyEntry;
}

function buildFamilyPool(): PoolCandidate[] {
  const pool: PoolCandidate[] = [];
  for (const s of unlockCatalog.skills ?? []) pool.push({ kind: "skill", entry: s });
  for (const w of unlockCatalog.weapons ?? []) pool.push({ kind: "weapon", entry: w });
  for (const p of unlockCatalog.pets ?? []) pool.push({ kind: "pet", entry: p });
  return pool;
}

function weightedPick(pool: PoolCandidate[]): PoolCandidate {
  const total = pool.reduce((sum, c) => sum + Math.max(0, c.entry.odds), 0);
  if (total <= 0) return pool[crypto.randomInt(0, pool.length)];

  let roll = (crypto.randomInt(0, 1_000_000_000) / 1_000_000_000) * total;
  for (const c of pool) {
    const w = Math.max(0, c.entry.odds);
    if (roll < w) return c;
    roll -= w;
  }
  return pool[pool.length - 1]; // segurança contra erro de ponto flutuante
}

function ownedTier(kind: UnlockKind, name: string, character: CharacterRosterDoc): number {
  let max = 0;
  if (kind === "skill") {
    for (const s of character.skills ?? []) if (s.name === name && s.tier > max) max = s.tier;
  } else if (kind === "weapon") {
    for (const w of character.weapons ?? []) if (w.name === name && w.tier > max) max = w.tier;
  } else {
    for (const p of character.pets ?? []) if (p.type === name && p.tier > max) max = p.tier;
  }
  return max;
}

const MAX_ATTEMPTS = 500;

export function drawUnlock(character: CharacterRosterDoc): UnlockResult | null {
  const pool = buildFamilyPool();
  if (pool.length === 0) return null;

  for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
    const picked = weightedPick(pool);
    const owned = ownedTier(picked.kind, picked.entry.name, character);
    if (owned >= 3) continue; // já no teto - sorteia de novo

    const grantTier = owned + 1;
    if (!picked.entry.tiers.includes(grantTier)) continue; // tier não gerado (ex: Garimpeiro/Magneto/Tamer)

    return { kind: picked.kind, name: picked.entry.name, tier: grantTier };
  }
  return null;
}
