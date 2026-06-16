using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterPanel : MonoBehaviour
{
    private SelectedProfileHolder _holder;
    private RectTransform _panelRt;
    private GameObject   _overlayGo;
    private bool         _isOpen;

    // Stats tab
    private TMP_Text      _charName, _levelText, _xpLabel;
    private RectTransform _xpFill;
    private TMP_Text      _hpVal, _strVal, _agiVal, _spdVal;
    private TMP_Text      _battlesText, _winRateText;

    // Skills tab
    private Transform _skillsGrid;
    private TMP_Text  _skillsEmpty;

    // Armas tab
    private Transform _armasList;
    private TMP_Text  _armasEmpty;

    // Tab roots & buttons
    private GameObject _statsRoot, _skillsRoot, _armasRoot;
    private Button     _statsBtn,  _skillsBtn,  _armasBtn;

    static readonly Color Gold    = new Color(0.78f, 0.63f, 0.27f, 1f);
    static readonly Color PanelBg = new Color(0.15f, 0.10f, 0.07f, 0.98f);
    static readonly Color SectBg  = new Color(0.22f, 0.15f, 0.10f, 0.90f);
    static readonly Color TabOn   = new Color(0.38f, 0.26f, 0.14f, 1f);
    static readonly Color TabOff  = new Color(0.18f, 0.12f, 0.08f, 1f);

    const float SlideTime    = 0.3f;
    const float OffScreenX   = 780f;  // reference pixels to push panel off-screen right

    // ── Public API ──────────────────────────────────────────────────────────

    public void Setup(SelectedProfileHolder holder)
    {
        _holder = holder;
        BuildUI();
        gameObject.SetActive(false);
    }

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        RefreshAll();
        _panelRt.anchoredPosition = new Vector2(OffScreenX, 0f);
        _overlayGo.SetActive(true);
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(Slide(true));
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        StopAllCoroutines();
        StartCoroutine(Slide(false));
    }

    private IEnumerator Slide(bool open)
    {
        Vector2 from = open ? new Vector2(OffScreenX, 0f) : Vector2.zero;
        Vector2 to   = open ? Vector2.zero : new Vector2(OffScreenX, 0f);
        float elapsed = 0f;
        while (elapsed < SlideTime)
        {
            _panelRt.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / SlideTime));
            elapsed += Time.deltaTime;
            yield return null;
        }
        _panelRt.anchoredPosition = to;
        if (!open) { _overlayGo.SetActive(false); gameObject.SetActive(false); }
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

        // Overlay
        _overlayGo = new GameObject("Overlay");
        _overlayGo.transform.SetParent(canvasGo.transform, false);
        var ovImg = _overlayGo.AddComponent<Image>();
        ovImg.color = new Color(0f, 0f, 0f, 0.55f);
        ovImg.raycastTarget = false;
        RT(_overlayGo).Set(Vector2.zero, Vector2.one);

        // Panel
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        panelGo.AddComponent<Image>().color = PanelBg;
        _panelRt = panelGo.AddComponent<RectTransform>();
        _panelRt.anchorMin = new Vector2(0.60f, 0f);
        _panelRt.anchorMax = new Vector2(1.00f, 1f);
        _panelRt.offsetMin = _panelRt.offsetMax = Vector2.zero;

        // Left gold border strip
        var brd = new GameObject("Border");
        brd.transform.SetParent(panelGo.transform, false);
        brd.AddComponent<Image>().color = Gold;
        var brdRt = brd.AddComponent<RectTransform>();
        brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = new Vector2(0f, 1f);
        brdRt.offsetMin = Vector2.zero; brdRt.offsetMax = new Vector2(4f, 0f);

        BuildHeader(panelGo);
        BuildTabBar(panelGo);
        BuildContent(panelGo);
        ShowTab(0);
    }

    private void BuildHeader(GameObject panel)
    {
        var hdr = MakeSection("Header", panel, 0.92f, 1.00f);
        hdr.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.08f, 0.05f, 1f);

        // Bottom gold line
        var line = new GameObject("Line"); line.transform.SetParent(hdr.transform, false);
        line.AddComponent<Image>().color = Gold;
        var lrt = line.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = new Vector2(1f, 0f);
        lrt.offsetMin = Vector2.zero;  lrt.offsetMax = new Vector2(0f, 2f);

        // Name
        var nameGo = new GameObject("Name"); nameGo.transform.SetParent(hdr.transform, false);
        _charName = nameGo.AddComponent<TextMeshProUGUI>();
        _charName.fontSize = 20; _charName.color = Gold;
        _charName.fontStyle = FontStyles.Bold;
        _charName.alignment = TextAlignmentOptions.MidlineLeft;
        var nrt = nameGo.AddComponent<RectTransform>();
        nrt.anchorMin = Vector2.zero; nrt.anchorMax = new Vector2(0.82f, 1f);
        nrt.offsetMin = new Vector2(14f, 0f); nrt.offsetMax = Vector2.zero;

        // Close button
        var closeGo = new GameObject("Close"); closeGo.transform.SetParent(hdr.transform, false);
        var closeBtn = closeGo.AddComponent<Button>();
        closeGo.AddComponent<Image>().color = new Color(0.55f, 0.10f, 0.10f, 0.85f);
        var crt = closeGo.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.85f, 0.15f); crt.anchorMax = new Vector2(0.97f, 0.85f);
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        closeBtn.onClick.AddListener(Close);
        AddCenteredText(closeGo, "✕", 16, Color.white);
    }

    private void BuildTabBar(GameObject panel)
    {
        var bar = MakeSection("TabBar", panel, 0.85f, 0.92f);
        bar.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.08f, 0.05f, 1f);

        string[] labels = { "Stats", "Skills", "Armas" };
        Button[] btns   = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            float x0 = i / 3f, x1 = (i + 1) / 3f;
            var tabGo = new GameObject($"Tab{labels[i]}"); tabGo.transform.SetParent(bar.transform, false);
            tabGo.AddComponent<Image>().color = TabOff;
            var btn = tabGo.AddComponent<Button>();
            int idx = i;
            btn.onClick.AddListener(() => ShowTab(idx));
            btns[i] = btn;
            var trt = tabGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(x0, 0f); trt.anchorMax = new Vector2(x1, 1f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            AddCenteredText(tabGo, labels[i], 13, Color.white);
        }
        _statsBtn = btns[0]; _skillsBtn = btns[1]; _armasBtn = btns[2];
    }

    private void BuildContent(GameObject panel)
    {
        var content = MakeSection("Content", panel, 0f, 0.85f);
        _statsRoot  = BuildStatsTab(content.gameObject);
        _skillsRoot = BuildSkillsTab(content.gameObject);
        _armasRoot  = BuildArmasTab(content.gameObject);
    }

    // ── Stats Tab ───────────────────────────────────────────────────────────

    private GameObject BuildStatsTab(GameObject parent)
    {
        var root = MakeFullChild("StatsTab", parent.transform);

        // Use a scroll for safety
        var scroll = MakeScrollContent(root.transform, out Transform content);

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 10, 10);
        vlg.spacing = 6f;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        // Level row
        var levelGo = MakeTextRow(content, "levelRow", out _levelText);
        _levelText.fontSize = 13; _levelText.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        // XP bar
        var xpBg = MakeFixedRow(content, "XpBar", 20f);
        xpBg.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        var xpFillGo = new GameObject("Fill"); xpFillGo.transform.SetParent(xpBg.transform, false);
        xpFillGo.AddComponent<Image>().color = new Color(0.20f, 0.55f, 0.90f, 0.90f);
        _xpFill = xpFillGo.AddComponent<RectTransform>();
        _xpFill.anchorMin = Vector2.zero; _xpFill.anchorMax = new Vector2(0f, 1f);
        _xpFill.offsetMin = _xpFill.offsetMax = Vector2.zero;

        // XP label
        var xpLblGo = MakeTextRow(content, "xpRow", out _xpLabel);
        _xpLabel.fontSize = 10; _xpLabel.color = new Color(0.6f, 0.6f, 0.6f, 1f);

        // Gold separator
        MakeSeparator(content);

        // 2×2 stat grid
        var gridGo = MakeFixedRow(content, "StatGrid", 160f);
        var glg = gridGo.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(320f, 70f);
        glg.spacing = new Vector2(8f, 8f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;
        glg.childAlignment = TextAnchor.UpperLeft;
        glg.padding = new RectOffset(0, 0, 0, 0);

        _hpVal  = BuildStatCell(gridGo.gameObject, "❤  HP");
        _strVal = BuildStatCell(gridGo.gameObject, "⚔  STR");
        _agiVal = BuildStatCell(gridGo.gameObject, "✦  AGI");
        _spdVal = BuildStatCell(gridGo.gameObject, "⚡  SPD");

        // Gold separator
        MakeSeparator(content);

        // Battles + Win rate
        var battGo = MakeTextRow(content, "battlesRow", out _battlesText);
        _battlesText.fontSize = 12; _battlesText.color = Color.white;
        var winGo = MakeTextRow(content, "winRow", out _winRateText);
        _winRateText.fontSize = 12; _winRateText.color = Color.white;

        return root;
    }

    private TMP_Text BuildStatCell(GameObject parent, string header)
    {
        var cell = new GameObject(header + "Cell"); cell.transform.SetParent(parent.transform, false);
        cell.AddComponent<Image>().color = SectBg;

        var hdrGo = new GameObject("Hdr"); hdrGo.transform.SetParent(cell.transform, false);
        var hdrTxt = hdrGo.AddComponent<TextMeshProUGUI>();
        hdrTxt.text = header; hdrTxt.fontSize = 10;
        hdrTxt.color = new Color(Gold.r, Gold.g, Gold.b, 0.85f);
        hdrTxt.alignment = TextAlignmentOptions.TopLeft;
        var hrt = hdrGo.AddComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = new Vector2(1f, 0.45f);
        hrt.offsetMin = new Vector2(6f, 2f); hrt.offsetMax = Vector2.zero;

        var valGo = new GameObject("Val"); valGo.transform.SetParent(cell.transform, false);
        var valTxt = valGo.AddComponent<TextMeshProUGUI>();
        valTxt.fontSize = 20; valTxt.fontStyle = FontStyles.Bold; valTxt.color = Color.white;
        valTxt.alignment = TextAlignmentOptions.MidlineLeft;
        var vrt = valGo.AddComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0f, 0.45f); vrt.anchorMax = Vector2.one;
        vrt.offsetMin = new Vector2(6f, 0f); vrt.offsetMax = Vector2.zero;
        return valTxt;
    }

    // ── Skills Tab ──────────────────────────────────────────────────────────

    private GameObject BuildSkillsTab(GameObject parent)
    {
        var root = MakeFullChild("SkillsTab", parent.transform);
        MakeScrollContent(root.transform, out Transform content);

        var glg = content.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(120f, 130f);
        glg.spacing = new Vector2(8f, 8f);
        glg.padding = new RectOffset(8, 8, 8, 8);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;
        glg.childAlignment = TextAnchor.UpperLeft;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _skillsGrid = content;

        _skillsEmpty = MakeCenteredMessage(root.transform, "Nenhuma skill ainda\n— suba de nível!");
        return root;
    }

    // ── Armas Tab ───────────────────────────────────────────────────────────

    private GameObject BuildArmasTab(GameObject parent)
    {
        var root = MakeFullChild("ArmasTab", parent.transform);
        MakeScrollContent(root.transform, out Transform content);

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 4f;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _armasList = content;

        _armasEmpty = MakeCenteredMessage(root.transform, "Sem armas equipadas");
        return root;
    }

    // ── Tab switching ────────────────────────────────────────────────────────

    private void ShowTab(int index)
    {
        _statsRoot.SetActive(index == 0);
        _skillsRoot.SetActive(index == 1);
        _armasRoot.SetActive(index == 2);
        SetTabColor(_statsBtn,  index == 0);
        SetTabColor(_skillsBtn, index == 1);
        SetTabColor(_armasBtn,  index == 2);
    }

    private static void SetTabColor(Button btn, bool on)
    {
        if (btn) btn.GetComponent<Image>().color = on ? TabOn : TabOff;
    }

    // ── Data Refresh ─────────────────────────────────────────────────────────

    private void RefreshAll()
    {
        var p = _holder?.currentProfile;
        if (p == null) { _charName.text = "—"; return; }

        _charName.text  = p.profileName;
        _levelText.text = $"Level {p.level}";

        int req = XpSystem.XpRequired(p.level);
        float pct = req > 0 ? Mathf.Clamp01((float)p.xpCurrent / req) : 0f;
        _xpFill.anchorMax = new Vector2(pct, 1f);
        _xpLabel.text = $"XP: {p.xpCurrent} / {req}";

        _hpVal.text  = $"{p.maxHealth}";
        _strVal.text = $"{p.str}";
        _agiVal.text = $"{p.agility}";
        _spdVal.text = $"{p.speed}";

        _battlesText.text = $"⚡  Batalhas hoje:  {p.battlesRemaining} / 6";
        _winRateText.text  = $"🏆  Win Rate:  {p.winRate:F1}%";

        RefreshSkills(p);
        RefreshArmas(p);
    }

    private void RefreshSkills(PlayerProfile p)
    {
        foreach (Transform c in _skillsGrid) Destroy(c.gameObject);
        bool has = p.skills != null && p.skills.Count > 0;
        _skillsEmpty.gameObject.SetActive(!has);
        if (!has) return;

        foreach (var skill in p.skills)
        {
            if (skill == null) continue;
            var cell = new GameObject(skill.skillName ?? "Skill");
            cell.transform.SetParent(_skillsGrid, false);
            cell.AddComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 0.22f);

            if (skill.icon != null)
            {
                var iconGo = new GameObject("Icon"); iconGo.transform.SetParent(cell.transform, false);
                var img = iconGo.AddComponent<Image>();
                img.sprite = skill.icon; img.preserveAspect = true;
                var irt = iconGo.AddComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.10f, 0.28f); irt.anchorMax = new Vector2(0.90f, 0.94f);
                irt.offsetMin = irt.offsetMax = Vector2.zero;
            }

            var nameGo = new GameObject("Name"); nameGo.transform.SetParent(cell.transform, false);
            var nTxt = nameGo.AddComponent<TextMeshProUGUI>();
            nTxt.text = skill.skillName ?? ""; nTxt.fontSize = 8; nTxt.color = Color.white;
            nTxt.alignment = TextAlignmentOptions.Center;
            var nrt = nameGo.AddComponent<RectTransform>();
            nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(1f, 0.26f);
            nrt.offsetMin = nrt.offsetMax = Vector2.zero;
        }
    }

    private void RefreshArmas(PlayerProfile p)
    {
        foreach (Transform c in _armasList) Destroy(c.gameObject);
        var loadout = p.weaponLoadout;
        int count = 0;
        if (loadout?.weapons != null)
        {
            foreach (var w in loadout.weapons)
            {
                if (w == null) continue;
                count++;
                BuildWeaponRow(w);
            }
        }
        _armasEmpty.gameObject.SetActive(count == 0);
    }

    private void BuildWeaponRow(WeaponData w)
    {
        var row = new GameObject(w.weaponName ?? "Weapon");
        row.transform.SetParent(_armasList, false);
        row.AddComponent<Image>().color = SectBg;
        var rrt = row.AddComponent<RectTransform>();
        rrt.sizeDelta = new Vector2(0f, 60f);

        Sprite spr = (w.icon != null) ? w.icon : w.inHandSprite;
        if (spr != null)
        {
            var iconGo = new GameObject("Icon"); iconGo.transform.SetParent(row.transform, false);
            var img = iconGo.AddComponent<Image>();
            img.sprite = spr; img.preserveAspect = true;
            var irt = iconGo.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.05f); irt.anchorMax = new Vector2(0f, 0.95f);
            irt.offsetMin = new Vector2(8f, 0f); irt.offsetMax = new Vector2(56f, 0f);
        }

        var infoGo = new GameObject("Info"); infoGo.transform.SetParent(row.transform, false);
        var info = infoGo.AddComponent<TextMeshProUGUI>();
        info.text = $"<b>{w.weaponName}</b>\n<size=10><color=#C8A044>{w.type}</color>  DMG {w.damage}</size>";
        info.fontSize = 13; info.color = Color.white;
        info.alignment = TextAlignmentOptions.MidlineLeft;
        var irt2 = infoGo.AddComponent<RectTransform>();
        irt2.anchorMin = Vector2.zero; irt2.anchorMax = Vector2.one;
        irt2.offsetMin = new Vector2(64f, 4f); irt2.offsetMax = new Vector2(-8f, -4f);
    }

    // ── Layout Helpers ───────────────────────────────────────────────────────

    // Full-anchor child with no Image (transparent)
    private static GameObject MakeFullChild(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        RT(go).Set(Vector2.zero, Vector2.one);
        return go;
    }

    // Horizontal band anchored by y fractions within the PANEL
    private static RectTransform MakeSection(string name, GameObject panel, float yMin, float yMax)
    {
        var go = new GameObject(name); go.transform.SetParent(panel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, yMin); rt.anchorMax = new Vector2(1f, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    // ScrollRect with Viewport + Content; returns content transform
    private static RectTransform MakeScrollContent(Transform parent, out Transform content)
    {
        var scrollGo = new GameObject("Scroll"); scrollGo.transform.SetParent(parent, false);
        RT(scrollGo).Set(Vector2.zero, Vector2.one);
        var sr = scrollGo.AddComponent<ScrollRect>();
        sr.horizontal = false;
        scrollGo.AddComponent<Image>().color = Color.clear;

        var vpGo = new GameObject("Viewport"); vpGo.transform.SetParent(scrollGo.transform, false);
        RT(vpGo).Set(Vector2.zero, Vector2.one);
        vpGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        var mask = vpGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        sr.viewport = vpGo.GetComponent<RectTransform>();

        var cGo = new GameObject("Content"); cGo.transform.SetParent(vpGo.transform, false);
        var crt = cGo.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f); crt.offsetMin = crt.offsetMax = Vector2.zero;
        sr.content = crt;
        content = cGo.transform;
        return crt;
    }

    // Row with a text component, height-autofit via layout element
    private static GameObject MakeTextRow(Transform parent, string name, out TMP_Text txt)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 22f; le.flexibleWidth = 1f;
        txt = go.AddComponent<TextMeshProUGUI>();
        txt.color = Color.white; txt.fontSize = 12;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        return go;
    }

    // Fixed-height row (Image background) for bars / grids
    private static RectTransform MakeFixedRow(Transform parent, string name, float height)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height; le.flexibleWidth = 1f;
        go.AddComponent<Image>().color = Color.clear;
        return go.GetComponent<RectTransform>();
    }

    private static void MakeSeparator(Transform parent)
    {
        var go = new GameObject("Sep"); go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 2f; le.flexibleWidth = 1f;
        go.AddComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 0.4f);
    }

    private static TMP_Text MakeCenteredMessage(Transform parent, string text)
    {
        var go = new GameObject("EmptyMsg"); go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.35f); rt.anchorMax = new Vector2(0.95f, 0.65f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = 13;
        txt.color = new Color(0.55f, 0.55f, 0.55f, 0.85f);
        txt.alignment = TextAlignmentOptions.Center;
        return txt;
    }

    private static void AddCenteredText(GameObject parent, string text, float size, Color color)
    {
        var go = new GameObject("Lbl"); go.transform.SetParent(parent.transform, false);
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = size; txt.color = color;
        txt.alignment = TextAlignmentOptions.Center;
        RT(go).Set(Vector2.zero, Vector2.one);
    }

    // ── RectTransform mini-helper ─────────────────────────────────────────────

    private static RectTransformHelper RT(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        return new RectTransformHelper(rt);
    }

    private struct RectTransformHelper
    {
        readonly RectTransform _rt;
        public RectTransformHelper(RectTransform rt) { _rt = rt; }
        public void Set(Vector2 min, Vector2 max)
        {
            _rt.anchorMin = min; _rt.anchorMax = max;
            _rt.offsetMin = _rt.offsetMax = Vector2.zero;
        }
    }
}
