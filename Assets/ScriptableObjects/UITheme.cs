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
}
