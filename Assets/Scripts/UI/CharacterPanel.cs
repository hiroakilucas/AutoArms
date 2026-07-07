using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HUD do personagem no lado direito do 01_MainMenu, com dois estados:
//   Compact  — nome, Win Rate, HP em texto, STR/AGI/SPD em pips (clique expande)
//   Expanded — mesmo bloco de informação do Compact (sem duplicar o desenho, só reconstruído
//              num container do mesmo tamanho) + seções de Habilidades e Armas equipadas
//              (ícone + nome cada), roláveis. Clique fora dos elementos interativos recolhe.
// Root é uma janela de altura FIXA (não anima tamanho) ancorada entre RootAnchorBottom (acima
// do BtnJogar, com margem) e RootAnchorTop — só o conteúdo interno (Compact vs Expanded)
// alterna via CanvasGroup (fade), nunca o footprint do Root em si.
public class CharacterPanel : MonoBehaviour
{
    private SelectedProfileHolder _holder;
    private UITheme _theme;

    private RectTransform _rootRt;
    private GameObject _compactGo, _expandedGo;
    private CanvasGroup _compactCg, _expandedCg;
    private bool _isExpanded;

    // Bloco de nome/winrate/HP/pips — construído duas vezes (Compact e topo do Expanded) com
    // exatamente o mesmo layout relativo, por isso os dois conjuntos de referências.
    private class InfoBlockRefs
    {
        public TMP_Text name, winRate, hp;
        public AttributePipBar str, agi, spd;
    }
    private InfoBlockRefs _compactInfo, _expandedInfo;

    // Skills/Armas — listas dinâmicas dentro do Expanded, reconstruídas a cada RefreshAll.
    private Transform _skillsList, _armasList;
    private TMP_Text _skillsEmpty, _armasEmpty;

    private Color PanelBgColor => _theme.panelBackgroundAlt;
    private Color TextColor    => _theme.textOnDark;

    const float PanelWidth    = 450f;
    const float CompactHeight = 230f;  // altura do bloco de info (Compact inteiro / topo do Expanded)
    const float FadeDuration  = 0.18f; // transição compact<->expanded

    // Janela vertical do Root, fração de um canvas 1920x1080 (ScaleWithScreenSize): topo em
    // 0.99 (margem de ~11px do topo da tela) e base em 0.28 (302px). O BtnJogar (canto
    // inferior direito, âncora (1,0), topo em 280px/0.259 — ver CLAUDE.md) fica ~22px abaixo
    // da base do Root — recalculado (2026-07-07) considerando as fontes maiores do bloco de
    // info (26pt nome, 20pt HP etc.) + as novas seções de Skills/Armas: o Root sobra ~537px
    // de área rolável abaixo do bloco de info (766px de altura total - 230px do bloco), então
    // a margem de segurança acima do Jogar continua de sobra mesmo com o conteúdo novo.
    const float RootAnchorTop    = 0.99f;
    const float RootAnchorBottom = 0.28f;

    // ── Public API ──────────────────────────────────────────────────────────

    public void Setup(SelectedProfileHolder holder, UITheme theme)
    {
        _holder = holder;
        _theme = theme;
        BuildUI();
        RefreshAll();
    }

    // Chamado pelo clique em qualquer ponto do estado Compact.
    public void Expand()
    {
        if (_isExpanded) return;
        _isExpanded = true;
        RefreshAll();
        StopAllCoroutines();
        StartCoroutine(CrossFade(true));
    }

    // Chamado pelo clique em qualquer ponto do estado Expanded fora dos elementos interativos
    // (nenhum botão de aba aqui — só o próprio background captura o clique; ver bubbling
    // abaixo em BuildExpanded).
    public void Collapse()
    {
        if (!_isExpanded) return;
        _isExpanded = false;
        StopAllCoroutines();
        StartCoroutine(CrossFade(false));
    }

    private IEnumerator CrossFade(bool toExpanded)
    {
        GameObject showGo = toExpanded ? _expandedGo : _compactGo;
        GameObject hideGo = toExpanded ? _compactGo  : _expandedGo;
        CanvasGroup showCg = toExpanded ? _expandedCg : _compactCg;
        CanvasGroup hideCg = toExpanded ? _compactCg  : _expandedCg;

        showGo.SetActive(true);
        showCg.interactable = false;
        showCg.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            float t = elapsed / FadeDuration;
            showCg.alpha = t;
            hideCg.alpha = 1f - t;
            elapsed += Time.deltaTime;
            yield return null;
        }
        showCg.alpha = 1f; hideCg.alpha = 0f;
        showCg.interactable = true;  showCg.blocksRaycasts = true;
        hideCg.interactable = false; hideCg.blocksRaycasts = false;
        hideGo.SetActive(false);
    }

    // ── UI Construction ─────────────────────────────────────────────────────

    private void BuildUI()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var rootGo = new GameObject("Root");
        rootGo.transform.SetParent(canvasGo.transform, false);
        _rootRt = rootGo.AddComponent<RectTransform>();
        _rootRt.anchorMin = new Vector2(1f, RootAnchorBottom);
        _rootRt.anchorMax = new Vector2(1f, RootAnchorTop);
        _rootRt.offsetMin = new Vector2(-PanelWidth, 0f);
        _rootRt.offsetMax = new Vector2(0f, 0f);

        BuildCompact(rootGo);
        BuildExpanded(rootGo);

        // Estado inicial: compacto visível, expandido desligado (sem animação — só acontece
        // na primeira montagem da cena).
        _compactGo.SetActive(true);
        _compactCg.alpha = 1f; _compactCg.interactable = true; _compactCg.blocksRaycasts = true;
        _expandedGo.SetActive(false);
        _expandedCg.alpha = 0f; _expandedCg.interactable = false; _expandedCg.blocksRaycasts = false;
    }

    private void BuildCompact(GameObject root)
    {
        _compactGo = new GameObject("Compact");
        _compactGo.transform.SetParent(root.transform, false);
        var rt = _compactGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -CompactHeight); rt.offsetMax = Vector2.zero;
        _compactCg = _compactGo.AddComponent<CanvasGroup>();

        var bg = _compactGo.AddComponent<Image>();
        bg.sprite = UIShapeUtil.RoundedRect(PanelBgColor, 24f);
        bg.type = Image.Type.Sliced;

        var btn = _compactGo.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(Expand);

        _compactInfo = BuildInfoBlock(_compactGo);
    }

    // Estado expandido: bloco de info (mesmo layout do Compact) no topo + Skills/Armas
    // roláveis abaixo. Clique em qualquer parte sem Button próprio borbulha até o Button do
    // fundo (Collapse) — arrastar pra rolar a lista não conta como clique (Unity distingue
    // drag de click), então rolar não recolhe por engano.
    private void BuildExpanded(GameObject root)
    {
        _expandedGo = new GameObject("Expanded");
        _expandedGo.transform.SetParent(root.transform, false);
        var rt = _expandedGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _expandedCg = _expandedGo.AddComponent<CanvasGroup>();

        var bg = _expandedGo.AddComponent<Image>();
        bg.sprite = UIShapeUtil.RoundedRect(PanelBgColor, 24f);
        bg.type = Image.Type.Sliced;

        var collapseBtn = _expandedGo.AddComponent<Button>();
        collapseBtn.targetGraphic = bg;
        collapseBtn.onClick.AddListener(Collapse);

        var infoGo = new GameObject("InfoBlock");
        infoGo.transform.SetParent(_expandedGo.transform, false);
        var irt = infoGo.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0f, 1f); irt.anchorMax = new Vector2(1f, 1f);
        irt.pivot     = new Vector2(0.5f, 1f);
        irt.offsetMin = new Vector2(0f, -CompactHeight); irt.offsetMax = Vector2.zero;
        _expandedInfo = BuildInfoBlock(infoGo);

        var lineGo = new GameObject("Divider");
        lineGo.transform.SetParent(_expandedGo.transform, false);
        var lrt = lineGo.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f);
        lrt.pivot     = new Vector2(0.5f, 1f);
        lrt.offsetMin = new Vector2(20f, -(CompactHeight + 3f));
        lrt.offsetMax = new Vector2(-20f, -CompactHeight);
        lineGo.AddComponent<Image>().color = _theme.currencyGold;

        var scrollAreaGo = new GameObject("ScrollArea");
        scrollAreaGo.transform.SetParent(_expandedGo.transform, false);
        var srt = scrollAreaGo.AddComponent<RectTransform>();
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero; srt.offsetMax = new Vector2(0f, -(CompactHeight + 6f));
        BuildSkillsAndWeapons(scrollAreaGo);
    }

    // Nome + Win Rate + HP + STR/AGI/SPD — mesmo bloco visual usado no Compact e no topo do
    // Expanded (`container` já vem dimensionado com CompactHeight de altura pelo chamador).
    private InfoBlockRefs BuildInfoBlock(GameObject container)
    {
        var refs = new InfoBlockRefs();

        var headerGo = new GameObject("Header");
        headerGo.transform.SetParent(container.transform, false);
        var hrt = headerGo.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0.05f, 0.83f); hrt.anchorMax = new Vector2(0.95f, 0.99f);
        hrt.offsetMin = hrt.offsetMax = Vector2.zero;

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(headerGo.transform, false);
        var nrt = nameGo.AddComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(0.60f, 1f);
        nrt.offsetMin = nrt.offsetMax = Vector2.zero;
        refs.name = nameGo.AddComponent<TextMeshProUGUI>();
        refs.name.fontSize = 26;
        refs.name.color = TextColor;
        refs.name.fontStyle = FontStyles.Bold;
        refs.name.alignment = TextAlignmentOptions.MidlineLeft;

        var badgeGo = new GameObject("WinRateBadge");
        badgeGo.transform.SetParent(headerGo.transform, false);
        var brt = badgeGo.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.66f, 0.06f); brt.anchorMax = new Vector2(1f, 0.94f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        var badgeImg = badgeGo.AddComponent<Image>();
        badgeImg.sprite = UIShapeUtil.RoundedRect(_theme.success, 20f);
        badgeImg.type = Image.Type.Sliced;
        refs.winRate = AddLabel(badgeGo, "—%", 16, _theme.textOnLight);
        refs.winRate.fontStyle = FontStyles.Bold;

        var hpGo = new GameObject("Hp");
        hpGo.transform.SetParent(container.transform, false);
        var hprt = hpGo.AddComponent<RectTransform>();
        hprt.anchorMin = new Vector2(0.05f, 0.63f); hprt.anchorMax = new Vector2(0.95f, 0.80f);
        hprt.offsetMin = hprt.offsetMax = Vector2.zero;
        refs.hp = hpGo.AddComponent<TextMeshProUGUI>();
        refs.hp.fontSize = 20; refs.hp.fontStyle = FontStyles.Bold;
        refs.hp.color = TextColor;
        refs.hp.alignment = TextAlignmentOptions.MidlineLeft;

        refs.str = BuildPipRow(container, 0.44f, 0.60f, "STR");
        refs.agi = BuildPipRow(container, 0.24f, 0.40f, "AGI");
        refs.spd = BuildPipRow(container, 0.04f, 0.20f, "SPD");

        return refs;
    }

    private AttributePipBar BuildPipRow(GameObject container, float yMin, float yMax, string label)
    {
        var rowGo = new GameObject(label + "Row");
        rowGo.transform.SetParent(container.transform, false);
        var rt = rowGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, yMin); rt.anchorMax = new Vector2(0.95f, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return AttributePipBar.Build(rowGo, _theme, label);
    }

    // ── Skills / Armas (Expanded) ───────────────────────────────────────────

    private void BuildSkillsAndWeapons(GameObject parent)
    {
        MakeScroll(parent.transform, out Transform content);

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 12, 16);
        vlg.spacing = 10f;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MakeSectionTitle(content, "HABILIDADES");
        _skillsList = MakeListContainer(content);
        _skillsEmpty = MakeMsg(content, "Nenhuma skill equipada ainda");

        MakeSep(content);

        MakeSectionTitle(content, "ARMAS");
        _armasList = MakeListContainer(content);
        _armasEmpty = MakeMsg(content, "Sem armas equipadas");
    }

    // Sub-container com seu próprio VerticalLayoutGroup — o grupo do `content` pai trata ele
    // como um bloco só, cuja altura vem do ContentSizeFitter interno (soma das linhas).
    private static Transform MakeListContainer(Transform parent)
    {
        var go = new GameObject("List");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go.transform;
    }

    // Linha genérica ícone + nome, usada tanto pra Skills quanto pra Armas.
    private void BuildIconNameRow(Transform parent, Sprite icon, string label)
    {
        var row = new GameObject(string.IsNullOrEmpty(label) ? "Row" : label);
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 56f; le.flexibleWidth = 1f;
        row.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);

        if (icon != null)
        {
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.transform, false);
            var irt = iconGo.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.1f); irt.anchorMax = new Vector2(0f, 0.9f);
            irt.offsetMin = new Vector2(8f, 0f); irt.offsetMax = new Vector2(52f, 0f);
            var img = iconGo.AddComponent<Image>();
            img.sprite = icon; img.preserveAspect = true;
        }

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(row.transform, false);
        var nrt = nameGo.AddComponent<RectTransform>();
        nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
        nrt.offsetMin = new Vector2(62f, 0f); nrt.offsetMax = new Vector2(-10f, 0f);
        var txt = nameGo.AddComponent<TextMeshProUGUI>();
        txt.text = label; txt.fontSize = 18; txt.fontStyle = FontStyles.Bold;
        txt.color = TextColor;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
    }

    // ── Data Refresh ─────────────────────────────────────────────────────────

    private void RefreshAll()
    {
        var p = _holder?.currentProfile;
        if (p == null) { _compactInfo.name.text = "—"; _expandedInfo.name.text = "—"; return; }

        // Placeholder simbólico (2026-07-07): profile.winRate nunca é escrito em lugar nenhum
        // hoje — não existe contador de vitórias/batalhas totais no projeto ainda. Usuário
        // confirmou que isso vai ser preenchido futuramente por um sistema de banco de
        // dados/histórico de partidas; até lá mostra "—%" pra qualquer profile novo (default 0).
        string winRateText = p.winRate > 0f ? $"{p.winRate:F0}%" : "—%";

        var (effHp, effStr, effAgi, effSpd, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _) = p.GetEffectiveStats();

        foreach (var refs in new[] { _compactInfo, _expandedInfo })
        {
            refs.name.text = p.profileName;
            refs.winRate.text = winRateText;
            refs.hp.text = $"{effHp} HP";
            refs.str.SetValue(effStr);
            refs.agi.SetValue(effAgi);
            refs.spd.SetValue(effSpd);
        }

        RefreshSkills(p);
        RefreshArmas(p);
    }

    private void RefreshSkills(PlayerProfile p)
    {
        foreach (Transform c in _skillsList) Destroy(c.gameObject);
        bool has = p.skills != null && p.skills.Count > 0;
        _skillsEmpty.gameObject.SetActive(!has);
        if (!has) return;

        foreach (var skill in p.skills)
        {
            if (skill == null) continue;

            // T2/T3 nunca têm icon próprio (ver SkillTierGenerator) — sobe a cadeia
            // previousTier até achar um, mesmo padrão de WeaponHandler.EquipSpecific.
            var iconSource = skill;
            while (iconSource != null && iconSource.icon == null) iconSource = iconSource.previousTier;

            string label = skill.tier > 1 ? $"{skill.skillName} (T{skill.tier})" : (skill.skillName ?? "");
            BuildIconNameRow(_skillsList, iconSource != null ? iconSource.icon : null, label);
        }
    }

    private void RefreshArmas(PlayerProfile p)
    {
        foreach (Transform c in _armasList) Destroy(c.gameObject);
        var loadout = p.weapons;
        int count = 0;
        if (loadout != null)
        {
            foreach (var w in loadout)
            {
                if (w == null) continue;
                count++;
                Sprite spr = w.icon != null ? w.icon : w.inHandSprite;
                BuildIconNameRow(_armasList, spr, w.weaponName);
            }
        }
        _armasEmpty.gameObject.SetActive(count == 0);
    }

    // ── Layout Helpers ───────────────────────────────────────────────────────

    private static TMP_Text AddLabel(GameObject parent, string text, float size, Color color)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = size; txt.color = color;
        txt.alignment = TextAlignmentOptions.Center;
        return txt;
    }

    private void MakeSectionTitle(Transform parent, string text)
    {
        var go = new GameObject("SectionTitle");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 30f; le.flexibleWidth = 1f;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = 20; txt.fontStyle = FontStyles.Bold;
        txt.color = _theme.currencyGold;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private void MakeSep(Transform parent)
    {
        var go = new GameObject("Sep");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 2f; le.flexibleWidth = 1f;
        var gold = _theme.currencyGold;
        go.AddComponent<Image>().color = new Color(gold.r, gold.g, gold.b, 0.4f);
    }

    private static TMP_Text MakeMsg(Transform parent, string text)
    {
        var go = new GameObject("EmptyMsg");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 30f; le.flexibleWidth = 1f;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = 16;
        txt.color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        return txt;
    }

    // ScrollRect + Viewport + Content
    private static void MakeScroll(Transform parent, out Transform content)
    {
        var scrollGo = new GameObject("Scroll");
        scrollGo.transform.SetParent(parent, false);
        Stretch(scrollGo.AddComponent<RectTransform>());
        scrollGo.AddComponent<Image>().color = Color.clear;
        var sr = scrollGo.AddComponent<ScrollRect>();
        sr.horizontal = false;

        var vpGo = new GameObject("Viewport");
        vpGo.transform.SetParent(scrollGo.transform, false);
        Stretch(vpGo.AddComponent<RectTransform>());
        vpGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        var mask = vpGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        sr.viewport = vpGo.GetComponent<RectTransform>();

        var cGo = new GameObject("Content");
        cGo.transform.SetParent(vpGo.transform, false);
        var crt = cGo.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f); crt.offsetMin = crt.offsetMax = Vector2.zero;
        sr.content = crt;
        content = cGo.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
