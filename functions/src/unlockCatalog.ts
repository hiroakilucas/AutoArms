import catalogJson from "./unlockCatalog.json";

/**
 * Espelho, do lado da function, dos databases de skill/arma/pet do Unity (`SkillDatabase`/
 * `WeaponDatabase`/`PetDatabase`) — a function roda em Node e não tem acesso a ScriptableObjects,
 * então precisa da própria cópia de "qual família existe, com que odds, e quais tiers ela tem de
 * verdade" pra sortear o refresh de um unlock (`rerollUnlock.ts`) sem depender de nenhuma leitura
 * extra no Firestore.
 *
 * `name` = nome de família (skill: `SkillData.skillName`; arma: nome sem sufixo " T1/T2/T3"; pet:
 * `PetType.ToString()` — "Mouse"/"Monkey"/"Boar") — mesma chave usada em
 * `CharacterDTO.weapons/skills` (`name`) e `CharacterDTO.pets` (`type`).
 * `odds` = % de sorteio da família (idêntico em todos os tiers dela — ver OddsApplier.cs).
 * `tiers` = quais tiers REALMENTE têm asset gerado (a maioria é [1,2,3], mas algumas skills como
 * Garimpeiro/Magneto/Tamer só têm T1 — ver SkillTierGenerator.cs) — nunca assumir [1,2,3] fixo.
 *
 * Gerado por `Tools > AutoArms > Export Unlock Catalog for Cloud Function` no Editor Unity —
 * rodar de novo (e reimplantar a function) sempre que odds ou tiers mudarem. **Este arquivo
 * começa vazio** até a primeira exportação — enquanto vazio, `rerollUnlock` sempre falha com
 * "internal" (nenhum candidato no pool), o que é o comportamento seguro (nunca inventa um
 * resultado).
 */
export interface UnlockFamilyEntry {
  name: string;
  odds: number;
  tiers: number[];
}

export interface UnlockCatalog {
  skills: UnlockFamilyEntry[];
  weapons: UnlockFamilyEntry[];
  pets: UnlockFamilyEntry[];
}

export const unlockCatalog: UnlockCatalog = catalogJson as UnlockCatalog;
