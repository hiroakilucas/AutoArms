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

    public static Color HexToColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var color);
        return color;
    }
}
