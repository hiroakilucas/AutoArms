using UnityEngine;

[CreateAssetMenu(fileName = "UITheme", menuName = "Game/UI Theme", order = 103)]
public class UITheme : ScriptableObject
{
    [Header("Fundo")]
    public Color backgroundTop = HexToColor("#F5E9D3");
    public Color backgroundBottom = HexToColor("#E8D5B5");
    public Color panelBackground = HexToColor("#3A2E27");
    public Color panelBackgroundAlt = HexToColor("#4A3F35");

    [Header("Botão de ação principal (Play / Iniciar Combate)")]
    public Color primaryAction = HexToColor("#E63946");
    public Color primaryActionAlt = HexToColor("#FF6B35");

    [Header("Botões secundários")]
    public Color secondaryButton = HexToColor("#8B6F47");
    public Color secondaryButtonAlt = HexToColor("#6B7B8C");

    [Header("Ícones / status")]
    public Color currencyGold = HexToColor("#FFC93C");
    public Color currencyGem = HexToColor("#4ECDC4");
    public Color energy = HexToColor("#FFD23F");
    public Color hpFull = HexToColor("#6BCB77");
    public Color hpCritical = HexToColor("#FF5C5C");
    public Color danger = HexToColor("#D64545");
    public Color success = HexToColor("#52B788");

    [Header("Texto")]
    public Color textOnLight = HexToColor("#2B2118");
    public Color textOnDark = HexToColor("#F5E9D3");

    [Header("Tiers de Skill/Arma (T1/T2/T3)")]
    public Color tierBronze = HexToColor("#CD7F32");
    public Color tierSilver = HexToColor("#C0C0C0");
    public Color tierGold = HexToColor("#FFD700");

    [Header("Raridade de Personagem (PlayerProfile.rarity)")]
    public Color rarityNormal = HexToColor("#9E9E9E");
    public Color rarityUncommon = HexToColor("#43A047");
    public Color rarityRare = HexToColor("#1E88E5");
    public Color rarityLegendary = HexToColor("#FB8C00");
    public Color rarityImmortal = HexToColor("#E53935");

    public static Color HexToColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var color);
        return color;
    }

    // Helper (2026-07-23, sistema de compra de personagens/case opening) — centraliza o mapeamento
    // CharacterRarity → cor, em vez de cada tela repetir o próprio switch (mesmo espírito do
    // ShopController.BuildItemData, que já lê rarityRare/Legendary/Immortal diretamente).
    public Color RarityColor(CharacterRarity rarity)
    {
        switch (rarity)
        {
            case CharacterRarity.Normal: return rarityNormal;
            case CharacterRarity.Uncommon: return rarityUncommon;
            case CharacterRarity.Rare: return rarityRare;
            case CharacterRarity.Legendary: return rarityLegendary;
            case CharacterRarity.Immortal: return rarityImmortal;
            default: return rarityNormal;
        }
    }

    // Cor de borda por tier de Skill/Arma/Pet (T1/T2/T3) — fonte única, centralizada aqui
    // (2026-07-27) porque a mesma lógica estava duplicada em 3 lugares (CharacterPanel.TierColor,
    // ArsenalSlotUI.TierColor, CharacterUnlockRevealPanel.TierColor), cada um com seu próprio
    // switch. Alinhada à mesma progressão de cor da raridade de personagem (cinza/verde/azul, ver
    // MONETIZACAO.md seção 8) em vez do antigo bronze/prata/ouro (`tierBronze`/`tierSilver`/
    // `tierGold` continuam existindo só para a cor de tag de `WeaponType`, ver `CharacterPanel.
    // TypeColor` — usos independentes, não são mais lidos para borda de tier). T4/T5 (Legendary/
    // Imortal, laranja/vermelho) reservados para quando essas evoluções existirem de verdade —
    // adicionar `case 4`/`case 5` aqui quando chegar a hora, sem precisar tocar nos consumidores.
    public Color TierColor(int tier)
    {
        switch (tier)
        {
            case 1: return rarityNormal;
            case 2: return rarityUncommon;
            default: return rarityRare;
        }
    }
}
