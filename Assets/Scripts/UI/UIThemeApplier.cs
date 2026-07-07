using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Aplica uma cor do UITheme ao Image ou TextMeshProUGUI do próprio GameObject — evita cor
// hardcoded em cada prefab/elemento de UI. Ainda não usado em nenhuma tela existente (fundação
// pra Fase 1: "Melhorar interface da página inicial"/"...tela de escolha de personagens").
public class UIThemeApplier : MonoBehaviour
{
    public enum ColorRole
    {
        BackgroundTop,
        BackgroundBottom,
        PanelBackground,
        PanelBackgroundAlt,
        PrimaryAction,
        PrimaryActionAlt,
        SecondaryButton,
        SecondaryButtonAlt,
        CurrencyGold,
        CurrencyGem,
        Energy,
        HpFull,
        HpCritical,
        Danger,
        Success,
        TextOnLight,
        TextOnDark,
    }

    [SerializeField] private UITheme theme;
    [SerializeField] private ColorRole colorRole;

    void Start()
    {
        if (theme == null) return;

        Color color = Resolve(colorRole);

        var image = GetComponent<Image>();
        if (image != null) image.color = color;

        var text = GetComponent<TextMeshProUGUI>();
        if (text != null) text.color = color;
    }

    private Color Resolve(ColorRole role)
    {
        switch (role)
        {
            case ColorRole.BackgroundTop: return theme.backgroundTop;
            case ColorRole.BackgroundBottom: return theme.backgroundBottom;
            case ColorRole.PanelBackground: return theme.panelBackground;
            case ColorRole.PanelBackgroundAlt: return theme.panelBackgroundAlt;
            case ColorRole.PrimaryAction: return theme.primaryAction;
            case ColorRole.PrimaryActionAlt: return theme.primaryActionAlt;
            case ColorRole.SecondaryButton: return theme.secondaryButton;
            case ColorRole.SecondaryButtonAlt: return theme.secondaryButtonAlt;
            case ColorRole.CurrencyGold: return theme.currencyGold;
            case ColorRole.CurrencyGem: return theme.currencyGem;
            case ColorRole.Energy: return theme.energy;
            case ColorRole.HpFull: return theme.hpFull;
            case ColorRole.HpCritical: return theme.hpCritical;
            case ColorRole.Danger: return theme.danger;
            case ColorRole.Success: return theme.success;
            case ColorRole.TextOnLight: return theme.textOnLight;
            case ColorRole.TextOnDark: return theme.textOnDark;
            default: return Color.white;
        }
    }
}
