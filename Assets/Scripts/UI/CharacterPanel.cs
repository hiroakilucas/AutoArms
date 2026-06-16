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
    private TMP_Text _charName, _levelText;
    private TMP_Text _hpVal, _strVal, _agiVal, _spdVal;
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

    const float PanelWidth   = 320f;    // fixed pixel width
    const float OffScreenX   = 340f;    // > PanelWidth to fully hide panel right
    const float SlideInTime  = 0.30f;
    const float SlideOutTime = 0.20f;

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
        float duration = open ? SlideInTime : SlideOutTime;
        Vector2 from = open ? new Vector2(OffScreenX, 0f) : Vector2.zero;
        Vector2 to   = open ? Vector2.zero : new Vector2(OffScreenX, 0f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t    = elapsed / duration;
            float ease = 1f - (1f - t) * (1f - t);  // EaseOut quad
            _panelRt.anchoredPosition = Vector2.Lerp(from, to, ease);
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

        // Overlay — raycastTarget=false so buttons below panel stay clickable
        _overlayGo = new GameObject("Overlay");
        _overlayGo.transform.SetParent(canvasGo.transform, false);
        var ovRt = _overlayGo.AddComponent<RectTransform>();
        Stretch(ovRt);
        var ovImg = _overlayGo.AddComponent<Image>();
        ovImg.color = new Color(0f, 0f, 0f, 0.45f);
        ovImg.raycastTarget = false;

        // Panel — 320px fixed width, anchored to right edge, y 10%–90%
        // pivot (1,0.5) so anchoredPosition.x=0 → right edge flush, x=340 → off-screen right
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        _panelRt = panelGo.AddComponent<RectTransform>();
        _panelRt.anchorMin = new Vector2(1f, 0.22f);  // 0.22*1080=237px > button top 203px
        _panelRt.anchorMax = new Vector2(1f, 0.92f);
        _panelRt.pivot     = new Vector2(1f, 0.5f);
        _panelRt.offsetMin = new Vector2(-PanelWidth, 0f);
        _panelRt.offsetMax = new Vector2(0f, 0f);
        panelGo.AddComponent<Image>().color = PanelBg;

        // Left gold border strip (4px)
        var brd = new GameObject("Border");
        brd.transform.SetParent(panelGo.transform, false);
        var brdRt = brd.AddComponent<RectTransform>();
        brdRt.anchorMin = Vector2.zero;
        brdRt.anchorMax = new Vector2(0f, 1f);
        brdRt.offsetMin = Vector2.zero;
        brdRt.offsetMax = new Vector2(4f, 0f);
        brd.AddComponent<Image>().color = Gold;

        // Close button — 70×70px at top-right corner of panel
        BuildCloseButton(panelGo);

        BuildHeader(panelGo);
        BuildTabBar(panelGo);
        BuildContent(panelGo);
        ShowTab(0);
    }

    private void BuildCloseButton(GameObject panel)
    {
        var go = new GameObject("CloseBtn");
        go.transform.SetParent(panel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(-68f, -68f);   // 60×60px with 8px margin
        rt.offsetMax = new Vector2(-8f,  -8f);
        // Image before Button so Button.targetGraphic is set automatically
        go.AddComponent<Image>().color = new Color(0.60f, 0.10f, 0.10f, 0.92f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(Close);
        AddLabel(go, "X", 28, Color.white);
    }

    private void BuildHeader(GameObject panel)
    {
        var hdr = MakeStrip("Header", panel, 0.90f, 1.00f);
        hdr.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.08f, 0.05f, 1f);

        // Bottom gold divider
        var line = new GameObject("Line");
        line.transform.SetParent(hdr.transform, false);
        var lrt = line.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = new Vector2(1f, 0f);
        lrt.offsetMin = Vector2.zero;  lrt.offsetMax = new Vector2(0f, 2f);
        line.AddComponent<Image>().color = Gold;

        // Character name (leaves right 20% free for close button overlap)
        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(hdr.transform, false);
        var nrt = nameGo.AddComponent<RectTransform>();
        nrt.anchorMin = Vector2.zero; nrt.anchorMax = new Vector2(0.80f, 1f);
        nrt.offsetMin = new Vector2(16f, 0f); nrt.offsetMax = Vector2.zero;
        _charName = nameGo.AddComponent<TextMeshProUGUI>();
        _charName.fontSize = 28;
        _charName.color = Gold;
        _charName.fontStyle = FontStyles.Bold;
        _charName.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private void BuildTabBar(GameObject panel)
    {
        var bar = MakeStrip("TabBar", panel, 0.82f, 0.90f);
        bar.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.06f, 0.04f, 1f);

        string[] labels = { "Stats", "Skills", "Armas" };
        Button[] btns   = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            float x0 = i / 3f, x1 = (i + 1) / 3f;
            var tabGo = new GameObject($"Tab{labels[i]}");
            tabGo.transform.SetParent(bar.transform, false);
            var trt = tabGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(x0, 0f); trt.anchorMax = new Vector2(x1, 1f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            tabGo.AddComponent<Image>().color = TabOff;
            var btn = tabGo.AddComponent<Button>();
            int idx = i;
            btn.onClick.AddListener(() => ShowTab(idx));
            btns[i] = btn;
            AddLabel(tabGo, labels[i], 18, Color.white);
        }
        _statsBtn = btns[0]; _skillsBtn = btns[1]; _armasBtn = btns[2];
    }

    private void BuildContent(GameObject panel)
    {
        var content = MakeStrip("Content", panel, 0f, 0.82f);
        _statsRoot  = BuildStatsTab(content.gameObject);
        _skillsRoot = BuildSkillsTab(content.gameObject);
        _armasRoot  = BuildArmasTab(content.gameObject);
    }

    // ── Stats Tab ───────────────────────────────────────────────────────────

    private GameObject BuildStatsTab(GameObject parent)
    {
        var root = MakeChild("StatsTab", parent.transform);
        MakeScroll(root.transform, out Transform content);

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 12, 12);
        vlg.spacing = 8f;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        // Level + XP text (no bar)
        MakeRow(content, "levelRow", out _levelText);
        _levelText.fontSize = 18; _levelText.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        MakeSep(content);

        // Horizontal stats row — 4 columns: HP | STR | AGI | SPD
        var statsRow = MakeTall(content, "StatsRow", 70f);
        var hlg = statsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        _hpVal  = BuildStatColumn(statsRow.gameObject, "HP");
        _strVal = BuildStatColumn(statsRow.gameObject, "STR");
        _agiVal = BuildStatColumn(statsRow.gameObject, "AGI");
        _spdVal = BuildStatColumn(statsRow.gameObject, "SPD");

        MakeSep(content);

        MakeRow(content, "battlesRow", out _battlesText);
        _battlesText.fontSize = 16; _battlesText.color = Color.white;
        MakeRow(content, "winRow", out _winRateText);
        _winRateText.fontSize = 16; _winRateText.color = Color.white;

        return root;
    }

    private TMP_Text BuildStatColumn(GameObject parent, string label)
    {
        var colGo = new GameObject(label + "Col");
        colGo.transform.SetParent(parent.transform, false);
        colGo.AddComponent<RectTransform>();  // RT before UIBehaviour
        colGo.AddComponent<Image>().color = SectBg;

        // Label: top 40% (golden, size 12)
        var lblGo = new GameObject("Lbl");
        lblGo.transform.SetParent(colGo.transform, false);
        var lrt = lblGo.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0.55f); lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
        lbl.text = label; lbl.fontSize = 12; lbl.color = Gold;
        lbl.alignment = TextAlignmentOptions.Center;

        // Value: bottom 55% (white, bold, size 20)
        var valGo = new GameObject("Val");
        valGo.transform.SetParent(colGo.transform, false);
        var vrt = valGo.AddComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero; vrt.anchorMax = new Vector2(1f, 0.55f);
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        var val = valGo.AddComponent<TextMeshProUGUI>();
        val.fontSize = 20; val.fontStyle = FontStyles.Bold; val.color = Color.white;
        val.alignment = TextAlignmentOptions.Center;
        return val;
    }

    // ── Skills Tab ──────────────────────────────────────────────────────────

    private GameObject BuildSkillsTab(GameObject parent)
    {
        var root = MakeChild("SkillsTab", parent.transform);
        MakeScroll(root.transform, out Transform content);

        var glg = content.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(130f, 145f);
        glg.spacing = new Vector2(8f, 8f);
        glg.padding = new RectOffset(10, 10, 10, 10);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;
        glg.childAlignment = TextAnchor.UpperLeft;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _skillsGrid = content;

        _skillsEmpty = MakeMsg(root.transform, "Nenhuma skill ainda\n— suba de nível!");
        return root;
    }

    // ── Armas Tab ───────────────────────────────────────────────────────────

    private GameObject BuildArmasTab(GameObject parent)
    {
        var root = MakeChild("ArmasTab", parent.transform);
        MakeScroll(root.transform, out Transform content);

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 5f;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _armasList = content;

        _armasEmpty = MakeMsg(root.transform, "Sem armas equipadas");
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

        int req = XpSystem.XpRequired(p.level);
        _levelText.text = $"Level {p.level}  •  XP: {p.xpCurrent} / {req}";

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
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(cell.transform, false);
                var irt = iconGo.AddComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.08f, 0.28f); irt.anchorMax = new Vector2(0.92f, 0.92f);
                irt.offsetMin = irt.offsetMax = Vector2.zero;
                var img = iconGo.AddComponent<Image>();
                img.sprite = skill.icon; img.preserveAspect = true;
            }

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(cell.transform, false);
            var nrt = nameGo.AddComponent<RectTransform>();
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = new Vector2(1f, 0.26f);
            nrt.offsetMin = nrt.offsetMax = Vector2.zero;
            var nTxt = nameGo.AddComponent<TextMeshProUGUI>();
            nTxt.text = skill.skillName ?? ""; nTxt.fontSize = 16; nTxt.color = Color.white;
            nTxt.alignment = TextAlignmentOptions.Center;
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
        var rrt = row.AddComponent<RectTransform>();
        rrt.sizeDelta = new Vector2(0f, 68f);
        row.AddComponent<Image>().color = SectBg;

        Sprite spr = (w.icon != null) ? w.icon : w.inHandSprite;
        if (spr != null)
        {
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.transform, false);
            var irt = iconGo.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.06f); irt.anchorMax = new Vector2(0f, 0.94f);
            irt.offsetMin = new Vector2(10f, 0f);   irt.offsetMax = new Vector2(60f, 0f);
            var img = iconGo.AddComponent<Image>();
            img.sprite = spr; img.preserveAspect = true;
        }

        var infoGo = new GameObject("Info");
        infoGo.transform.SetParent(row.transform, false);
        var irt2 = infoGo.AddComponent<RectTransform>();
        irt2.anchorMin = Vector2.zero; irt2.anchorMax = Vector2.one;
        irt2.offsetMin = new Vector2(68f, 4f); irt2.offsetMax = new Vector2(-8f, -4f);
        var info = infoGo.AddComponent<TextMeshProUGUI>();
        info.text = $"<b>{w.weaponName}</b>\n<size=13><color=#C8A044>{w.type}</color>  DMG {w.damage}</size>";
        info.fontSize = 16; info.color = Color.white;
        info.alignment = TextAlignmentOptions.MidlineLeft;
    }

    // ── Layout Helpers ───────────────────────────────────────────────────────

    // Full-stretch child (transparent)
    private static GameObject MakeChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Stretch(go.AddComponent<RectTransform>());
        return go;
    }

    // Horizontal strip anchored by y-fraction in its parent
    private static RectTransform MakeStrip(string name, GameObject parent, float yMin, float yMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, yMin); rt.anchorMax = new Vector2(1f, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
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

    // Text row managed by VerticalLayoutGroup
    private static void MakeRow(Transform parent, string name, out TMP_Text txt)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();  // RT before any UIBehaviour
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 28f; le.flexibleWidth = 1f;
        txt = go.AddComponent<TextMeshProUGUI>();
        txt.color = Color.white; txt.fontSize = 14;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
    }

    // Fixed-height block managed by VerticalLayoutGroup
    private static RectTransform MakeTall(Transform parent, string name, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();  // RT before any UIBehaviour
        go.AddComponent<Image>().color = Color.clear;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height; le.flexibleWidth = 1f;
        return rt;
    }

    private static void MakeSep(Transform parent)
    {
        var go = new GameObject("Sep");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();  // RT before any UIBehaviour
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 2f; le.flexibleWidth = 1f;
        go.AddComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 0.4f);
    }

    private static TMP_Text MakeMsg(Transform parent, string text)
    {
        var go = new GameObject("EmptyMsg");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.35f); rt.anchorMax = new Vector2(0.95f, 0.65f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = 16;
        txt.color = new Color(0.55f, 0.55f, 0.55f, 0.85f);
        txt.alignment = TextAlignmentOptions.Center;
        return txt;
    }

    // Centered label that fills its parent
    private static void AddLabel(GameObject parent, string text, float size, Color color)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent.transform, false);
        Stretch(go.AddComponent<RectTransform>());
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = size; txt.color = color;
        txt.alignment = TextAlignmentOptions.Center;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
