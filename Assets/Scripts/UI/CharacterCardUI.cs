using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Card do grid de 02_SelectCharacter no estilo "grid de heróis" do Brawl Stars: portrait grande
// com fundo colorido, nome sobreposto na base do portrait, Level+XP compacto abaixo. Construído
// 100% via código (mesmo padrão de CharacterPanel/MainMenuCharacterPreview/SelectOpponentController)
// — não depende de nenhum prefab (o antigo Assets/Prefabs/CharacterCards/CharacterCard estava
// perdido do projeto, ver CHANGELOG 2026-07-08).
public class CharacterCardUI : MonoBehaviour
{
    // Expostos pra CharacterSelectController dimensionar o GridLayoutGroup sem duplicar os
    // números aqui e lá. Tamanho calibrado (2026-07-10) pra caber exatamente 3 colunas × 3 linhas
    // dentro do Scroll View de 1834×964 (spacing 17 em CharacterSelectController.EnsureGridLayout):
    // 3×600 + 2×17 = 1834; 3×310 + 2×17 = 964.
    public const float CardWidth = 600f;
    public const float CardHeight = TopMargin + PortraitBoxHeight + Gap + LevelXpHeight + BottomMargin;

    // Portrait não é mais quadrado (era 170×170, igual a CardWidth por coincidência) — a altura
    // do card é limitada pelo orçamento das 3 linhas (ScrollHeight), então a caixa de portrait
    // vira um retângulo mais largo que alto, com uma margem de 30px de cada lado dentro do card.
    private const float PortraitBoxWidth = 540f;
    private const float PortraitBoxHeight = 200f;
    private const float NameStripHeight = 40f;
    private const float LevelXpHeight = 64f;
    private const float TopMargin = 20f;
    private const float Gap = 12f;
    private const float BottomMargin = 14f;

    private PlayerProfile profile;
    private CharacterSelectController controller;

    // Paleta simples "por enquanto" (pedido do usuário) — cor sólida atrás do portrait, tokens
    // do UITheme, escolhida deterministicamente pelo NOME do personagem (não pelo índice no
    // grid, pra não mudar de cor se a ordem do CharacterDatabase mudar no futuro).
    private static Color[] Palette(UITheme theme) => new[]
    {
        theme.primaryActionAlt, theme.secondaryButtonAlt, theme.success, theme.currencyGem, theme.tierBronze
    };

    public void Setup(PlayerProfile profile, CharacterSelectController controller, UITheme theme, bool isEnabled)
    {
        this.profile = profile;
        this.controller = controller;
        Build(theme, isEnabled);
    }

    public void OnClick()
    {
        controller?.OnCharacterSelected(profile);
    }

    private void Build(UITheme theme, bool isEnabled)
    {
        var rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(CardWidth, CardHeight);

        // Fundo do card quase invisível — só existe pra dar um Graphic com raycastTarget na
        // hierarquia do Button (GraphicRaycaster precisa disso pra registrar clique).
        var hit = gameObject.AddComponent<Image>();
        hit.sprite = UIShapeUtil.RoundedRect(theme.panelBackground, 14f);
        hit.color = new Color(theme.panelBackground.r, theme.panelBackground.g, theme.panelBackground.b, 0.25f);
        hit.type = Image.Type.Sliced;

        // Bloqueado: sem Button nenhum — "sem reação ao toque" de forma literal, não só
        // desabilitada visualmente.
        if (isEnabled)
        {
            var btn = gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.onClick.AddListener(OnClick);
        }

        float y = -TopMargin;
        y = BuildPortraitBox(theme, isEnabled, y);
        y -= Gap;
        BuildLevelXpBar(theme, isEnabled, y);
    }

    private float BuildPortraitBox(UITheme theme, bool isEnabled, float y)
    {
        var palette = Palette(theme);
        int index = Mathf.Abs(profile.profileName.GetHashCode()) % palette.Length;
        Color bgColor = isEnabled ? palette[index] : Desaturate(palette[index]);

        var boxGo = new GameObject("PortraitBox");
        boxGo.transform.SetParent(transform, false);
        var boxRt = boxGo.AddComponent<RectTransform>();
        boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 1f);
        boxRt.sizeDelta = new Vector2(PortraitBoxWidth, PortraitBoxHeight);
        boxRt.anchoredPosition = new Vector2(0f, y);
        var boxImg = boxGo.AddComponent<Image>();
        boxImg.sprite = UIShapeUtil.RoundedRect(bgColor, 20f);
        boxImg.type = Image.Type.Sliced;

        if (profile.previewIcon != null)
        {
            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(boxGo.transform, false);
            var portraitRt = portraitGo.AddComponent<RectTransform>();
            portraitRt.anchorMin = new Vector2(0.08f, 0.10f);
            portraitRt.anchorMax = new Vector2(0.92f, 0.98f);
            // Left=3, Right=-3, Top=20, Bottom=-20 (valores do Inspector, calibrados pelo usuário)
            // convertidos pra offsetMin/Max: offsetMin=(Left, Bottom), offsetMax=(-Right, -Top).
            portraitRt.offsetMin = new Vector2(3f, -20f);
            portraitRt.offsetMax = new Vector2(3f, -20f);
            portraitRt.localScale = new Vector3(2.3f, 2.3f, 1f);
            var portraitImg = portraitGo.AddComponent<Image>();
            portraitImg.sprite = profile.previewIcon;
            portraitImg.preserveAspect = true;
            // Silhueta quase preta via tint (sem shader custom) — bloqueado fica bem escuro/preto
            // no sombreamento, estilo personagem não desbloqueado do Brawl Stars (era um cinza
            // médio 0.42, pedido do usuário pra ficar bem mais escuro).
            if (!isEnabled) portraitImg.color = new Color(0.04f, 0.04f, 0.04f, 1f);
        }

        // Faixa escura na base do portrait, com o nome sobreposto por cima (mesmo recorte visual
        // do grid de heróis do Brawl Stars).
        var stripGo = new GameObject("NameStrip");
        stripGo.transform.SetParent(boxGo.transform, false);
        var stripRt = stripGo.AddComponent<RectTransform>();
        stripRt.anchorMin = new Vector2(0f, 0f);
        stripRt.anchorMax = new Vector2(1f, 0f);
        stripRt.pivot = new Vector2(0.5f, 0f);
        stripRt.sizeDelta = new Vector2(0f, NameStripHeight);
        stripRt.anchoredPosition = Vector2.zero;
        var stripImg = stripGo.AddComponent<Image>();
        stripImg.color = new Color(0f, 0f, 0f, isEnabled ? 0.55f : 0.7f);

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(stripGo.transform, false);
        var nameRt = nameGo.AddComponent<RectTransform>();
        nameRt.anchorMin = Vector2.zero;
        nameRt.anchorMax = Vector2.one;
        nameRt.offsetMin = nameRt.offsetMax = Vector2.zero;
        var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
        nameTxt.text = profile.profileName;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = FadeIfLocked(theme.textOnDark, isEnabled);
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.enableAutoSizing = true;
        nameTxt.fontSizeMin = 14;
        nameTxt.fontSizeMax = 24;

        return y - PortraitBoxHeight;
    }

    // Mesmo estilo visual de MainMenuCharacterPreview.BuildLevelXpHud (fundo sólido
    // panelBackgroundAlt, "Level X" acima, barra grossa com "atual/necessário" centralizado
    // dentro dela) — adaptado ao empilhamento vertical do card.
    private void BuildLevelXpBar(UITheme theme, bool isEnabled, float y)
    {
        var boxGo = new GameObject("LevelXp");
        boxGo.transform.SetParent(transform, false);
        var boxRt = boxGo.AddComponent<RectTransform>();
        boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f, 1f);
        boxRt.sizeDelta = new Vector2(CardWidth - 12f, LevelXpHeight);
        boxRt.anchoredPosition = new Vector2(0f, y);
        var boxImg = boxGo.AddComponent<Image>();
        boxImg.sprite = UIShapeUtil.RoundedRect(
            isEnabled ? theme.panelBackgroundAlt : Desaturate(theme.panelBackgroundAlt), 10f);
        boxImg.type = Image.Type.Sliced;

        Color textColor = FadeIfLocked(theme.textOnDark, isEnabled);

        var lvlGo = new GameObject("LevelText");
        lvlGo.transform.SetParent(boxGo.transform, false);
        var lvlRt = lvlGo.AddComponent<RectTransform>();
        lvlRt.anchorMin = new Vector2(0.05f, 0.58f);
        lvlRt.anchorMax = new Vector2(0.95f, 0.98f);
        lvlRt.offsetMin = lvlRt.offsetMax = Vector2.zero;
        var lvlTxt = lvlGo.AddComponent<TextMeshProUGUI>();
        lvlTxt.text = $"Level {profile.level}";
        lvlTxt.fontSize = 18;
        lvlTxt.fontStyle = FontStyles.Bold;
        lvlTxt.color = textColor;
        lvlTxt.alignment = TextAlignmentOptions.Center;

        var barBgGo = new GameObject("BarBg");
        barBgGo.transform.SetParent(boxGo.transform, false);
        var barBgRt = barBgGo.AddComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0.06f, 0.06f);
        barBgRt.anchorMax = new Vector2(0.94f, 0.52f);
        barBgRt.offsetMin = barBgRt.offsetMax = Vector2.zero;
        var barBgImg = barBgGo.AddComponent<Image>();
        barBgImg.sprite = UIShapeUtil.RoundedRect(new Color(0f, 0f, 0f, 0.55f), 6f);
        barBgImg.type = Image.Type.Sliced;

        int req = XpSystem.XpRequired(profile.level);
        float pct = req > 0 ? Mathf.Clamp01((float)profile.xpCurrent / req) : 0f;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(barBgGo.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(Mathf.Max(pct, 0.001f), 1f);
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = UIShapeUtil.RoundedRect(
            isEnabled ? theme.currencyGold : Desaturate(theme.currencyGold), 5f);
        fillImg.type = Image.Type.Sliced;

        var xpTxtGo = new GameObject("XpText");
        xpTxtGo.transform.SetParent(barBgGo.transform, false);
        var xpRt = xpTxtGo.AddComponent<RectTransform>();
        xpRt.anchorMin = Vector2.zero;
        xpRt.anchorMax = Vector2.one;
        xpRt.offsetMin = xpRt.offsetMax = Vector2.zero;
        var xpTxt = xpTxtGo.AddComponent<TextMeshProUGUI>();
        xpTxt.text = $"{profile.xpCurrent}/{req}";
        xpTxt.fontSize = 14;
        xpTxt.fontStyle = FontStyles.Bold;
        xpTxt.color = textColor;
        xpTxt.alignment = TextAlignmentOptions.Center;
    }

    private static Color FadeIfLocked(Color c, bool isEnabled) =>
        isEnabled ? c : new Color(c.r, c.g, c.b, c.a * 0.6f);

    // Aproximação de dessaturação via tint (sem shader/material custom): mistura a cor com seu
    // equivalente em escala de cinza (luminância) e escurece um pouco.
    private static Color Desaturate(Color c)
    {
        float gray = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        Color mixed = Color.Lerp(c, new Color(gray, gray, gray, c.a), 0.85f);
        return new Color(mixed.r * 0.75f, mixed.g * 0.75f, mixed.b * 0.75f, c.a);
    }
}
