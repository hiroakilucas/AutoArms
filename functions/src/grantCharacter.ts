import { Level1Stats } from "./characterStats";

// Concessão de personagem novo (documento em users/{uid}/characters/{characterId}) — extraído de
// purchaseCase.ts (2026-07-26) pra ser reaproveitado por purchaseNextCharacter.ts sem duplicar o
// shape do documento.

// Ticks .NET (100ns desde 0001-01-01) equivalentes ao epoch Unix (1970-01-01) — mesma unidade de
// CharacterDTO.updatedAtTicks (DateTime.UtcNow.Ticks no lado C#), pra este documento ficar
// comparável com qualquer outro gravado pelo cliente (LocalSaveService/FirestoreService).
export const DOTNET_EPOCH_TICKS = 621355968000000000;

export function nowTicks(): number {
  return Date.now() * 10000 + DOTNET_EPOCH_TICKS;
}

// Loadout inicial de personagem novo (mesmo conjunto de "Reset All Profiles to Level 1" no Editor
// Unity, CharacterCreationEditor.cs — Satyr1/Golem3/Succubus/Zombie, todos tier 1) — sem isso um
// personagem recém-concedido nasceria com o loadout vazio (sempre desarmado em combate).
export const DEFAULT_STARTING_WEAPONS = [
  { name: "Satyr1", tier: 1 },
  { name: "Golem3", tier: 1 },
  { name: "Succubus", tier: 1 },
  { name: "Zombie", tier: 1 },
];

// Mesmo shape gravado por purchaseCase.ts — level 1, loadout inicial, stats sorteados,
// skills/pets vazios. `characterId` é o próprio ID do documento (newCharRef.id), passado pelo
// chamador porque só ele tem a referência do documento recém-criado.
export function buildNewCharacterDocData(
  characterId: string,
  characterTypeId: string,
  rarity: number,
  stats: Level1Stats,
  nowTicksValue: number
): Record<string, unknown> {
  return {
    characterId,
    characterTypeId,
    profileName: characterTypeId,
    level: 1,
    winRate: 0,
    xpCurrent: 0,
    xpRequired: 6,
    battlesRemaining: 6,
    isFavorite: false,
    rarity,
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
    skills: [],
    pets: [],
    updatedAtTicks: nowTicksValue,
  };
}
