using UnityEngine;

// Definição de um pacote de case da Loja (2026-07-23, sistema de compra de personagens/case
// opening) - espelha o documento casePackages/{packageId} no Firestore (fonte de verdade em
// runtime, ver ARQUITETURA.md "Modelo de roster multi-personagem"). Este asset é só REFERÊNCIA
// pro time de design/UI (preview do preço/regra no Editor) - o preço/limite REAL que vale em
// combate/compra vem sempre do documento do Firestore (populado por
// functions/src/scripts/seedCasePackages.ts), nunca deste asset diretamente. Manter os dois em
// sincronia manualmente ao balancear preços.
[CreateAssetMenu(fileName = "NewCasePackage", menuName = "Game/Case Package Definition", order = 105)]
public class CasePackageDefinition : ScriptableObject
{
    [Tooltip("Deve bater com o ID do documento em casePackages/{packageId} no Firestore.")]
    public string packageId;

    [Tooltip("true = pool restrito a uma raridade (rarityTier), com limite de compras por jogador. false = pool de todas as raridades, sorteadas por tierWeights (case de moeda).")]
    public bool isRarityLocked = true;

    [Tooltip("Só significativo quando isRarityLocked=true. Ignorado (pool mistura raridades via tierWeights) quando isRarityLocked=false.")]
    public CharacterRarity rarityTier = CharacterRarity.Rare;

    [Tooltip("Preço em reais (cash/IAP). 0 = pago com moeda (currencyCost abaixo).")]
    public double cashPrice = 0.0;

    [Tooltip("Custo em diamantes. 0 = pago em cash (cashPrice acima).")]
    public int currencyCost = 0;

    [Tooltip("Limite de compras POR JOGADOR (não é um estoque global compartilhado entre jogadores - ver MONETIZACAO.md seção 5). <=0 = sem limite.")]
    public int purchaseLimitPerPlayer = 0;

    [Tooltip("Pesos por raridade (Normal/Uncommon/Rare/Legendary/Immortal, nesta ordem) - só usado quando isRarityLocked=false. Não precisam somar 1, são normalizados no sorteio. MONETIZACAO.md seção 7: 0.68/0.20/0.08/0.035/0.005.")]
    public float[] tierWeights = new float[5];
}
