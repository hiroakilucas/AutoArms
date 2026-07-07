using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Badge circular colorido (valor numérico do atributo) + fileira de 10 pips/blocos
// preenchidos representando o progresso dentro da faixa atual de 10 pontos. A cor de
// badge+pips muda a cada múltiplo de 10 (1-10/11-20/21-30/31+, ciclando de volta pro 1º tom
// se passar de 40) — usa 4 tokens do UITheme (secondaryButton/currencyGold/primaryActionAlt/
// danger). Usado nas 3 linhas de atributo (STR/AGI/SPD) do CharacterPanel único.
//
// `Build` só preenche o conteúdo de um `rowContainer` já dimensionado pelo chamador (fração
// manual de RectTransform ou LayoutElement dentro de um VerticalLayoutGroup) — não decide
// onde a linha fica posicionada, isso é responsabilidade de quem chama.
public class AttributePipBar
{
    private const int PipCount = 10;
    private const int TierSize = 10;

    private readonly UITheme _theme;
    private readonly Image _badgeImg;
    private readonly TMP_Text _badgeText;
    private readonly Image[] _pips;

    private AttributePipBar(UITheme theme, Image badgeImg, TMP_Text badgeText, Image[] pips)
    {
        _theme = theme;
        _badgeImg = badgeImg;
        _badgeText = badgeText;
        _pips = pips;
    }

    public static AttributePipBar Build(GameObject rowContainer, UITheme theme, string label)
    {
        var lblGo = new GameObject("Lbl");
        lblGo.transform.SetParent(rowContainer.transform, false);
        var lrt = lblGo.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0.20f, 1f);
        lrt.offsetMin = new Vector2(14f, 0f); lrt.offsetMax = Vector2.zero;
        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
        lbl.text = label; lbl.fontSize = 18; lbl.fontStyle = FontStyles.Bold;
        lbl.color = theme.currencyGold;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;

        // Badge circular: RoundedRect com raio = metade do lado vira um círculo (mesma técnica
        // já usada nos badges de level em pill, só que com sizeDelta quadrado em vez de retangular).
        var badgeGo = new GameObject("Badge");
        badgeGo.transform.SetParent(rowContainer.transform, false);
        var brt = badgeGo.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.24f, 0.5f); brt.anchorMax = new Vector2(0.24f, 0.5f);
        brt.pivot = new Vector2(0f, 0.5f);
        brt.sizeDelta = new Vector2(36f, 36f);
        var badgeImg = badgeGo.AddComponent<Image>();
        badgeImg.sprite = UIShapeUtil.RoundedRect(Color.white, 18f);
        badgeImg.type = Image.Type.Sliced;
        var badgeText = AddCenteredLabel(badgeGo, "0", 16, theme.textOnLight);

        var pipsGo = new GameObject("Pips");
        pipsGo.transform.SetParent(rowContainer.transform, false);
        var prt = pipsGo.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.44f, 0.18f); prt.anchorMax = new Vector2(1f, 0.82f);
        prt.offsetMin = Vector2.zero; prt.offsetMax = new Vector2(-8f, 0f);
        var hlg = pipsGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        var pips = new Image[PipCount];
        for (int i = 0; i < PipCount; i++)
        {
            var pipGo = new GameObject($"Pip{i}");
            pipGo.transform.SetParent(pipsGo.transform, false);
            pipGo.AddComponent<RectTransform>();
            var pipImg = pipGo.AddComponent<Image>();
            pipImg.sprite = UIShapeUtil.RoundedRect(Color.white, 4f);
            pipImg.type = Image.Type.Sliced;
            pips[i] = pipImg;
        }

        return new AttributePipBar(theme, badgeImg, badgeText, pips);
    }

    public void SetValue(int value)
    {
        int clamped = Mathf.Max(0, value);
        Color[] tiers = { _theme.secondaryButton, _theme.currencyGold, _theme.primaryActionAlt, _theme.danger };
        int tierIndex = clamped <= 0 ? 0 : ((clamped - 1) / TierSize) % tiers.Length;
        int filled    = clamped <= 0 ? 0 : ((clamped - 1) % TierSize) + 1;
        Color tierColor = tiers[tierIndex];

        _badgeText.text = $"{clamped}";
        _badgeImg.color = tierColor;

        Color dim = new Color(tierColor.r, tierColor.g, tierColor.b, 0.18f);
        for (int i = 0; i < _pips.Length; i++)
            _pips[i].color = i < filled ? tierColor : dim;
    }

    private static TMP_Text AddCenteredLabel(GameObject parent, string text, float size, Color color)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = size; txt.color = color;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        return txt;
    }
}
