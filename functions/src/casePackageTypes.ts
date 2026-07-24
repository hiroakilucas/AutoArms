/**
 * Shape do documento `casePackages/{packageId}` (ver ARQUITETURA.md "Modelo de roster
 * multi-personagem"/CasePackageDefinition.cs no lado Unity — os dois devem ficar em sincronia
 * manualmente, não há geração automática deste lado ainda).
 */
export interface CasePackageDoc {
  packageId: string;
  isRarityLocked: boolean;
  /** (int)CharacterRarity — só significativo quando isRarityLocked=true. */
  rarityTier: number;
  /** Preço em reais (cash/IAP). 0 = pago com moeda (currencyCost). */
  cashPrice: number;
  /** Custo em diamantes. 0 = pago em cash (cashPrice). */
  currencyCost: number;
  /** Limite de compras POR JOGADOR (não estoque global — ver MONETIZACAO.md seção 5). <=0 = sem limite. */
  purchaseLimitPerPlayer: number;
  /**
   * Pesos por raridade (Normal/Uncommon/Rare/Legendary/Immortal, 5 posições), só usado quando
   * isRarityLocked=false. Não precisam somar 1 — são normalizados no sorteio.
   */
  tierWeights: number[] | null;
}

/** Pesos padrão do "case geral" (moeda) — MONETIZACAO.md seção 7, mesma distribuição de raridade
 * já usada em outras partes do jogo. Usado só se o documento não trouxer `tierWeights` próprio. */
export const DEFAULT_TIER_WEIGHTS = [0.68, 0.20, 0.08, 0.035, 0.005];

export const RARITY_COUNT = 5;
