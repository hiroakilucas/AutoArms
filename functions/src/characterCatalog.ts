import catalogJson from "./characterCatalog.json";

/**
 * Espelho, do lado da function, de `CharacterDatabase.unlockedCharacters` (Unity) — a function
 * roda em Node e não tem acesso a ScriptableObjects, então precisa da própria cópia de
 * `{ characterTypeId: rarity }` pra montar o pool elegível de cada case sem depender de uma
 * leitura extra no Firestore a cada compra.
 *
 * `characterTypeId` = nome do asset PlayerProfile no Unity (mesma chave que
 * `PlayerProfileConverter.FromOpponentIndexMap` já usa pra achar o "molde" de um personagem).
 * `rarity` = (int)CharacterRarity — 0 Normal, 1 Uncommon, 2 Rare, 3 Legendary, 4 Immortal (mesma
 * ordem do enum em PlayerProfile.cs).
 *
 * Gerado por `Tools > AutoArms > Export Character Catalog for Cloud Function` no Editor Unity —
 * rodar de novo (e reimplantar a function) sempre que o roster de personagens ou alguma raridade
 * mudar. **Este arquivo começa vazio** (`{}`) até a primeira exportação — enquanto vazio,
 * `purchaseCase` sempre responde "pool esgotada" pra qualquer pacote, o que é o comportamento
 * seguro (nunca concede um characterTypeId inventado).
 */
export type CharacterCatalog = Record<string, number>;

export const characterCatalog: CharacterCatalog = catalogJson as CharacterCatalog;

export function charactersOfRarity(rarity: number): string[] {
  return Object.entries(characterCatalog)
    .filter(([, r]) => r === rarity)
    .map(([characterTypeId]) => characterTypeId);
}
