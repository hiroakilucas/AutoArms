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
    private TMP_Text _hpVal, _strVal, _agiVal, _spdVal, _initVal, _critChanceVal, _critDmgVal, _evasionVal, _reversalVal, _counterVal, _comboVal, _armorVal, _accuracyVal, _blockVal, _reversalAfterBlockVal, _disarmVal;
    private TMP_Text _unarmedDmgVal, _weaponSharpVal, _daggerDmgVal, _swordDmgVal, _heavyDmgVal, _heavyDexVal, _heavyHitSpeedVal;
    private TMP_Text      _battlesText, _winRateText;

    // Pets — seção dinâmica no fim da aba Stats, só aparece quando o personagem tem pelo menos
    // 1 pet (ganho via level-up, categoria Pet — ver CombatResultPanel.ApplyBonus). Reconstruída
    // a cada RefreshAll (mesmo padrão de RefreshSkills/RefreshArmas), já que profile.pets varia
    // de personagem pra personagem e pode mudar entre aberturas do painel.
    private Transform _statsContent;
    private readonly List<GameObject> _petRows = new List<GameObject>();

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
        // Content rect tem altura 0 por padrão (ancorada só no topo) — sem isso o ScrollRect
        // não sabe a altura real do conteúdo e o scroll não funciona quando passa da viewport
        // (ficou mais provável de acontecer agora, com 7 linhas de stat em vez de 1 linha
        // horizontal). Mesmo padrão já usado nas abas Skills/Armas.
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Level + XP text (no bar)
        MakeRow(content, "levelRow", out _levelText);
        _levelText.fontSize = 18; _levelText.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        MakeSep(content);

        // Stats verticais, um por linha (era uma linha horizontal de 5 colunas — ficou
        // apertado demais depois de adicionar INIT e, agora, CRIT CHANCE/CRIT DMG).
        _hpVal         = BuildStatRow(content.gameObject, "HP");
        _strVal        = BuildStatRow(content.gameObject, "STR");
        _agiVal        = BuildStatRow(content.gameObject, "AGI");
        _spdVal        = BuildStatRow(content.gameObject, "SPD");
        _initVal       = BuildStatRow(content.gameObject, "INIT");
        _critChanceVal = BuildStatRow(content.gameObject, "CRIT CHANCE");
        _critDmgVal    = BuildStatRow(content.gameObject, "CRIT DMG");
        _accuracyVal   = BuildStatRow(content.gameObject, "ACCURACY");
        _evasionVal    = BuildStatRow(content.gameObject, "EVASION");
        _reversalVal   = BuildStatRow(content.gameObject, "REVERSAL");
        _counterVal    = BuildStatRow(content.gameObject, "COUNTER");
        _comboVal      = BuildStatRow(content.gameObject, "COMBO");
        _armorVal      = BuildStatRow(content.gameObject, "ARMOR");
        _blockVal      = BuildStatRow(content.gameObject, "BLOCK");
        _reversalAfterBlockVal = BuildStatRow(content.gameObject, "REVERSAL AFTER BLOCK");
        _disarmVal             = BuildStatRow(content.gameObject, "DISARM");

        MakeSep(content);

        // Dano normal (sem crítico) por arquétipo de arma — ver PlayerProfile.GetWeaponDamageRanges().
        // Desarmado e ARMA AFIADA (logo abaixo, "weapons: none" → "weapons: sharp") usam o
        // padrão base→efetivo pra destacar bônus de skill (Martial Arts dobra o desarmado;
        // Weapon Master dá +50% em armas Sharp); Pesado mostra o valor direto, já que nenhuma
        // skill o modifica ainda.
        _unarmedDmgVal   = BuildStatRow(content.gameObject, "DANO DESARMADO");
        _weaponSharpVal  = BuildStatRow(content.gameObject, "ARMA AFIADA (BÔNUS)");
        _daggerDmgVal    = BuildStatRow(content.gameObject, "DANO ADAGA");
        _swordDmgVal     = BuildStatRow(content.gameObject, "DANO ESPADA");
        _heavyDmgVal     = BuildStatRow(content.gameObject, "DANO PESADO");
        // Bodybuilder: bônus só enquanto empunha Heavy — mostrados aqui, agrupados com DANO
        // PESADO, mesmo padrão informativo de ARMA AFIADA (a UI não sabe qual arma está
        // equipada agora, então só confirma a magnitude da skill).
        _heavyDexVal      = BuildStatRow(content.gameObject, "DEXTERITY (HEAVY)");
        _heavyHitSpeedVal = BuildStatRow(content.gameObject, "HIT SPEED (HEAVY)");

        MakeSep(content);

        MakeRow(content, "battlesRow", out _battlesText);
        _battlesText.fontSize = 16; _battlesText.color = Color.white;
        MakeRow(content, "winRow", out _winRateText);
        _winRateText.fontSize = 16; _winRateText.color = Color.white;

        // Seção de Pets é construída dinamicamente em RefreshPets (ver _petRows) — precisa da
        // referência ao Content do ScrollRect pra anexar/remover linhas a cada refresh.
        _statsContent = content;

        return root;
    }

    // Linha de stat: label dourado à esquerda, valor branco à direita, empilhada verticalmente
    // pelo VerticalLayoutGroup do content (substituiu o antigo grid horizontal de colunas).
    private TMP_Text BuildStatRow(GameObject parent, string label)
    {
        var rowGo = new GameObject(label + "Row");
        rowGo.transform.SetParent(parent.transform, false);
        rowGo.AddComponent<RectTransform>();  // RT before UIBehaviour
        var le = rowGo.AddComponent<LayoutElement>();
        le.preferredHeight = 30f; le.flexibleWidth = 1f;
        rowGo.AddComponent<Image>().color = SectBg;

        var lblGo = new GameObject("Lbl");
        lblGo.transform.SetParent(rowGo.transform, false);
        var lrt = lblGo.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0.55f, 1f);
        lrt.offsetMin = new Vector2(10f, 0f); lrt.offsetMax = Vector2.zero;
        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
        lbl.text = label; lbl.fontSize = 14; lbl.color = Gold;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;

        var valGo = new GameObject("Val");
        valGo.transform.SetParent(rowGo.transform, false);
        var vrt = valGo.AddComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.55f, 0f); vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero; vrt.offsetMax = new Vector2(-10f, 0f);
        var val = valGo.AddComponent<TextMeshProUGUI>();
        val.fontSize = 16; val.fontStyle = FontStyles.Bold; val.color = Color.white;
        val.alignment = TextAlignmentOptions.MidlineRight;
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

        // Mostra os stats efetivos (com bônus de skill já aplicados — ex: Immortal soma HP
        // e subtrai STR/AGI/SPD), mesma lógica de PlayerProfile.GetEffectiveStats() usada no
        // preview do menu principal. Antes mostrava p.maxHealth/str/agility/speed crus, então
        // escolher uma skill que afeta stats nunca aparecia aqui.
        var (effHp, effStr, effAgi, effSpd, effInit, effCritChance, effCritDmg, effEvasion, effReversal, effCounter, effCombo, effArmor, effAccuracy, effBlock, effReversalAfterBlock, effDisarm, effSharpDmg, effHeavyDex, effHeavyHitSpeed) = p.GetEffectiveStats();
        SetStatValue(_hpVal,   p.maxHealth,  effHp);
        SetStatValue(_strVal,  p.str,        effStr);
        SetStatValue(_agiVal,  p.agility,    effAgi);
        SetStatValue(_spdVal,  p.speed,      effSpd);
        SetStatValue(_initVal, p.initiative, effInit);
        SetStatValuePercent(_critChanceVal, p.criticalChance, effCritChance);
        SetStatValuePercent(_critDmgVal,    0f,                effCritDmg);
        SetStatValuePercent(_accuracyVal,   p.accuracy,        effAccuracy);
        SetStatValuePercent(_evasionVal,    p.evasion,         effEvasion);
        SetStatValuePercent(_reversalVal,   p.reversal,        effReversal);
        SetStatValuePercent(_counterVal,    p.counter,         effCounter);
        SetStatValuePercent(_armorVal,      p.armor,           effArmor);
        SetStatValuePercent(_comboVal,      0f,                effCombo);
        SetStatValuePercent(_blockVal,              p.blockBonus,          effBlock);
        SetStatValuePercent(_reversalAfterBlockVal, p.reversalAfterBlock,  effReversalAfterBlock);
        SetStatValuePercent(_disarmVal,             0f,                    effDisarm);
        SetStatValuePercent(_weaponSharpVal,        0f,                    effSharpDmg);

        var dmg     = p.GetWeaponDamageRanges();
        var baseDmg = p.GetBaseSharpDamageRanges();
        int baseUnarmedDmg = Mathf.Max(1, UnarmedStats.Damage + effStr);
        SetStatValue(_unarmedDmgVal, baseUnarmedDmg, dmg.unarmedMin);
        SetStatRangeWithBase(_daggerDmgVal, baseDmg.daggerMin, baseDmg.daggerMax, dmg.daggerMin, dmg.daggerMax);
        SetStatRangeWithBase(_swordDmgVal,  baseDmg.swordMin,  baseDmg.swordMax,  dmg.swordMin,  dmg.swordMax);
        SetStatRange(_heavyDmgVal, dmg.heavyMin, dmg.heavyMax);
        SetStatValuePercent(_heavyDexVal,      0f, effHeavyDex);
        SetStatValuePercent(_heavyHitSpeedVal, 0f, effHeavyHitSpeed);

        _battlesText.text = $"⚡  Batalhas hoje:  {p.battlesRemaining} / 6";
        _winRateText.text  = $"🏆  Win Rate:  {p.winRate:F1}%";

        RefreshSkills(p);
        RefreshArmas(p);
        RefreshPets(p);
    }

    // Mostra HP/Dano/Speed/AGI/STR efetivos (já com o escalonamento por nível do dono — ver
    // PetState.ApplyLevelScaling, mesmo cálculo usado por CombatSceneLoader.SpawnPets/
    // CombatSimulator.BuildState) de cada tipo de pet que o personagem tem. Sem seção nenhuma
    // (nem separador) quando profile.pets estiver vazio — só "aparece quando ele selecionar o
    // atributo do pet" (level-up, categoria Pet), pedido pelo usuário.
    private void RefreshPets(PlayerProfile p)
    {
        foreach (var go in _petRows) Destroy(go);
        _petRows.Clear();

        if (p.pets == null || p.pets.Count == 0) return;

        var counts = new Dictionary<PetType, int>();
        var order  = new List<PetType>();
        foreach (var type in p.pets)
        {
            if (!counts.ContainsKey(type)) { counts[type] = 0; order.Add(type); }
            counts[type]++;
        }

        _petRows.Add(MakeSepGo(_statsContent));
        _petRows.Add(MakeSectionTitleGo(_statsContent, "PETS"));

        foreach (var type in order)
        {
            var pet = PetState.Create(type);
            if (pet == null) continue;
            pet.ApplyLevelScaling(p.level);

            int   count = counts[type];
            string name = PetState.DisplayName(type) + (count > 1 ? $" x{count}" : "");
            _petRows.Add(BuildPetHeaderGo(_statsContent, name));

            var (dmgMin, dmgMax) = PetState.DamageRange(type);
            string dmgText = dmgMin == dmgMax ? $"{dmgMin}" : $"{dmgMin}–{dmgMax}";

            _petRows.Add(BuildPetStatRow(_statsContent, "HP",   $"{pet.maxHp}"));
            _petRows.Add(BuildPetStatRow(_statsContent, "DANO", dmgText));
            _petRows.Add(BuildPetStatRow(_statsContent, "SPD",  $"{pet.speed}"));
            _petRows.Add(BuildPetStatRow(_statsContent, "AGI",  $"{pet.agility}"));
            _petRows.Add(BuildPetStatRow(_statsContent, "STR",  $"{pet.str:F0}"));
        }
    }

    private GameObject MakeSepGo(Transform parent)
    {
        var go = new GameObject("PetSep");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 2f; le.flexibleWidth = 1f;
        go.AddComponent<Image>().color = new Color(Gold.r, Gold.g, Gold.b, 0.4f);
        return go;
    }

    private GameObject MakeSectionTitleGo(Transform parent, string text)
    {
        var go = new GameObject("PetsTitle");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 26f; le.flexibleWidth = 1f;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = 17; txt.fontStyle = FontStyles.Bold;
        txt.color = Gold; txt.alignment = TextAlignmentOptions.MidlineLeft;
        return go;
    }

    private GameObject BuildPetHeaderGo(Transform parent, string name)
    {
        var go = new GameObject("PetHeader_" + name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 24f; le.flexibleWidth = 1f;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = name; txt.fontSize = 15; txt.fontStyle = FontStyles.Bold;
        txt.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        return go;
    }

    // Mesma estrutura visual de BuildStatRow (label dourado à esquerda, valor branco à
    // direita), mas com indentação maior e valor estático (sem highlight base→efetivo — os
    // stats de pet não são modificados por skill nenhuma do dono, só pelo escalonamento por
    // nível já refletido no valor passado).
    private GameObject BuildPetStatRow(Transform parent, string label, string value)
    {
        var rowGo = new GameObject("Pet" + label + "Row");
        rowGo.transform.SetParent(parent, false);
        rowGo.AddComponent<RectTransform>();
        var le = rowGo.AddComponent<LayoutElement>();
        le.preferredHeight = 26f; le.flexibleWidth = 1f;
        rowGo.AddComponent<Image>().color = SectBg;

        var lblGo = new GameObject("Lbl");
        lblGo.transform.SetParent(rowGo.transform, false);
        var lrt = lblGo.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0.55f, 1f);
        lrt.offsetMin = new Vector2(22f, 0f); lrt.offsetMax = Vector2.zero;
        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
        lbl.text = label; lbl.fontSize = 13; lbl.color = Gold;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;

        var valGo = new GameObject("Val");
        valGo.transform.SetParent(rowGo.transform, false);
        var vrt = valGo.AddComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.55f, 0f); vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero; vrt.offsetMax = new Vector2(-10f, 0f);
        var val = valGo.AddComponent<TextMeshProUGUI>();
        val.text = value; val.fontSize = 15; val.fontStyle = FontStyles.Bold; val.color = Color.white;
        val.alignment = TextAlignmentOptions.MidlineRight;
        return rowGo;
    }

    // Mirrors MainMenuCharacterPreview's "base→effective" highlight: mostra só o valor
    // efetivo quando igual ao base, ou "base→efetivo" em verde quando uma skill o altera.
    private static void SetStatValue(TMP_Text label, int baseValue, int effectiveValue)
    {
        if (effectiveValue == baseValue)
        {
            label.text = $"{effectiveValue}";
            label.fontSize = 16;
        }
        else
        {
            label.text = $"{baseValue}→<color=#7CD27C>{effectiveValue}</color>";
            label.fontSize = 13;
        }
    }

    // Mesmo padrão "base→efetivo", pra stats percentuais (CRIT CHANCE, CRIT DMG). critDmgBonus
    // não tem campo base no profile (é puramente derivado de skill, ex: Reconnaissance) — quem
    // chama passa 0f como base nesse caso.
    private static void SetStatValuePercent(TMP_Text label, float baseValue, float effectiveValue)
    {
        if (Mathf.Approximately(effectiveValue, baseValue))
        {
            label.text = $"{effectiveValue:P0}";
            label.fontSize = 16;
        }
        else
        {
            label.text = $"{baseValue:P0}→<color=#7CD27C>{effectiveValue:P0}</color>";
            label.fontSize = 13;
        }
    }

    // Faixa de dano min–max sem highlight (nenhuma skill modifica Pesado ainda).
    private static void SetStatRange(TMP_Text label, int min, int max)
    {
        label.text = min == max ? $"{min}" : $"{min}–{max}";
        label.fontSize = 16;
    }

    // Mesmo padrão base→efetivo de SetStatValue, mas para uma faixa min–max (Adaga/Espada,
    // afetadas por Weapon Master).
    private static void SetStatRangeWithBase(TMP_Text label, int baseMin, int baseMax, int effMin, int effMax)
    {
        if (baseMin == effMin && baseMax == effMax)
        {
            label.text = baseMin == baseMax ? $"{baseMin}" : $"{baseMin}–{baseMax}";
            label.fontSize = 16;
        }
        else
        {
            string baseStr = baseMin == baseMax ? $"{baseMin}" : $"{baseMin}–{baseMax}";
            string effStr  = effMin  == effMax  ? $"{effMin}"  : $"{effMin}–{effMax}";
            label.text = $"{baseStr}→<color=#7CD27C>{effStr}</color>";
            label.fontSize = 13;
        }
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

            // T2/T3 nunca têm icon próprio (ver SkillTierGenerator) — sobe a cadeia previousTier
            // até achar um, mesmo padrão de WeaponHandler.EquipSpecific pro sprite da arma.
            var iconSource = skill;
            while (iconSource != null && iconSource.icon == null) iconSource = iconSource.previousTier;

            if (iconSource != null && iconSource.icon != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(cell.transform, false);
                var irt = iconGo.AddComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.08f, 0.28f); irt.anchorMax = new Vector2(0.92f, 0.92f);
                irt.offsetMin = irt.offsetMax = Vector2.zero;
                var img = iconGo.AddComponent<Image>();
                img.sprite = iconSource.icon; img.preserveAspect = true;
            }

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(cell.transform, false);
            var nrt = nameGo.AddComponent<RectTransform>();
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = new Vector2(1f, 0.26f);
            nrt.offsetMin = nrt.offsetMax = Vector2.zero;
            var nTxt = nameGo.AddComponent<TextMeshProUGUI>();
            string skillLabel = skill.tier > 1 ? $"{skill.skillName} (T{skill.tier})" : (skill.skillName ?? "");
            nTxt.text = skillLabel; nTxt.fontSize = 16; nTxt.color = Color.white;
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
        info.text = $"<b>{w.weaponName}</b>\n<size=13><color=#C8A044>{string.Join(", ", w.types)}</color>  DMG {w.damage}</size>";
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
