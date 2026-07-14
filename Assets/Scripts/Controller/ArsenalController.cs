using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tela "Arsenal" (03_Arsenal, 2026-07-14) — grade de TODAS as armas e skills do jogo (não só as
// do personagem), mostrando pro personagem selecionado (SelectedProfileHolder.currentProfile,
// mesma fonte do main menu/02_SelectCharacter) qual o maior tier que ele possui de cada uma —
// borda bronze/prata/ouro (ArsenalSlotUI) se possuir, ícone escurecido/"bloqueado" se não possuir
// nenhum tier daquela arma/skill. Puramente informativo: só LÊ PlayerProfile.weapons/skills,
// nunca altera. Construído 100% via código (mesmo padrão de CharacterSelectController/
// CharacterPanel) — a cena em si só precisa de Main Camera + EventSystem + um GameObject com
// este componente (sem hierarquia de UI pré-montada, tudo é gerado aqui em Start()).
//
// Popup de detalhe (2026-07-14, pedido do usuário: "igual o painel de informação do main menu",
// mesmo clicando numa arma/skill bloqueada) — em vez de reimplementar um popup próprio,
// instancia um `CharacterPanel` de verdade (mesmo componente do HUD lateral de 01_MainMenu) só
// pra reaproveitar `ShowWeaponDetail`/`ShowSkillDetail` (tornados públicos) — garante popup
// IDÊNTICO ao do menu principal, sem duplicar a lógica de layout/tier/stats. O HUD Compact/
// Expanded desse CharacterPanel embutido nunca aparece: `HideRootPermanently()` (novo) move o
// Root pra fora da tela sem desativar o Canvas (diferente de HideSlideOut), então o popup
// (sibling do Canvas, sortingOrder 20 — sempre acima do Canvas desta tela) continua funcionando.
public class ArsenalController : MonoBehaviour
{
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;
    [SerializeField] private WeaponDatabase weaponDatabase;
    [SerializeField] private SkillDatabase skillDatabase;
    [SerializeField] private UITheme theme;

    // 6 colunas pras duas seções (pedido do usuário) — número de linhas de cada grid segue
    // direto da quantidade de armas/skills cadastradas em cada database, sem limite fixo.
    private const int Columns = 6;
    private const float CellSize = 150f;
    private const float CellSpacing = 18f;

    private CharacterPanel _detailPanel;

    void Start()
    {
        if (theme == null) return;

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        BuildBackground(canvasGo.transform);
        BuildHeader(canvasGo.transform);
        BuildScrollView(canvasGo.transform);
        BuildDetailPanel();
    }

    // Cena nova (03_Arsenal) sem EventSystem pré-colocado — mesma necessidade já documentada em
    // CombatHUD.EnsureEventSystem (a cena de combate também não tem um por padrão).
    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    // CharacterPanel monta seu PRÓPRIO Canvas (ScreenSpaceOverlay, sortingOrder 20) — precisa
    // ficar SEM pai (raiz da cena), senão vira um Canvas aninhado e ignora seu próprio
    // CanvasScaler (mesma armadilha documentada em CharacterPanel.BuildUI).
    private void BuildDetailPanel()
    {
        var go = new GameObject("CharacterPanel (Arsenal Detail)");
        _detailPanel = go.AddComponent<CharacterPanel>();
        _detailPanel.Setup(selectedProfileHolder, theme);
        _detailPanel.HideRootPermanently();
    }

    private void BuildBackground(Transform parent)
    {
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(parent, false);
        bgGo.transform.SetAsFirstSibling();
        var rt = bgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = bgGo.AddComponent<Image>();
        img.sprite = UIShapeUtil.VerticalGradient(theme.backgroundTop, theme.backgroundBottom);
        img.raycastTarget = false;
    }

    private void BuildHeader(Transform parent)
    {
        var profile = selectedProfileHolder != null ? selectedProfileHolder.currentProfile : null;

        var backGo = new GameObject("BtnVoltar");
        backGo.transform.SetParent(parent, false);
        var backRt = backGo.AddComponent<RectTransform>();
        backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
        backRt.pivot = new Vector2(0f, 1f);
        backRt.sizeDelta = new Vector2(160f, 44f);
        backRt.anchoredPosition = new Vector2(30f, -30f);
        BuildButton(backGo, "Voltar", theme.secondaryButton, OnBackClicked);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(parent, false);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(900f, 60f);
        titleRt.anchoredPosition = new Vector2(0f, -26f);
        var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        titleTxt.text = profile != null ? $"ARSENAL — {profile.profileName}" : "ARSENAL";
        titleTxt.fontSize = 36;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = theme.textOnLight;
        titleTxt.alignment = TextAlignmentOptions.Center;
    }

    private void BuildButton(GameObject go, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var img = go.AddComponent<Image>();
        img.sprite = UIShapeUtil.RoundedRect(color, 12f);
        img.type = Image.Type.Sliced;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var txt = labelGo.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 22;
        txt.fontStyle = FontStyles.Bold;
        txt.color = theme.textOnDark;
        txt.alignment = TextAlignmentOptions.Center;
    }

    private void OnBackClicked() => SceneManager.LoadScene("01_MainMenu");

    private void BuildScrollView(Transform parent)
    {
        var scrollGo = new GameObject("ScrollView");
        scrollGo.transform.SetParent(parent, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.06f, 0.04f);
        scrollRt.anchorMax = new Vector2(0.94f, 0.86f);
        scrollRt.offsetMin = scrollRt.offsetMax = Vector2.zero;
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRt = viewportGo.AddComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = viewportRt.offsetMax = Vector2.zero;
        // RectMask2D (não Image+Mask) — recorte puramente por bounds do RectTransform, sem
        // depender de canal alpha/stencil; padrão do próprio ScrollView default da Unity.
        viewportGo.AddComponent<RectMask2D>();
        // Image quase invisível (bug real, 2026-07-14: "só arrasta se clicar num ícone") — sem
        // NENHUM Graphic no Viewport, o GraphicRaycaster não acha nada pra "bater" nas áreas
        // vazias entre/abaixo dos ícones, então o ScrollRect nunca recebe o evento de arrastar
        // ali (só funcionava clicando em cima de um ícone, que tem sua própria Image). Cor quase
        // transparente (alpha 0.001, não 0 — só cosmético, raycast não liga pra alpha) cobre o
        // Viewport inteiro com um alvo de raycast, então arrastar em QUALQUER ponto da tela rola.
        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 0.001f);

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 0f);
        var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.spacing = 24f;
        contentLayout.padding = new RectOffset(0, 0, 0, 24);
        var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;

        var profile = selectedProfileHolder != null ? selectedProfileHolder.currentProfile : null;

        // dimIcon=true (armas, padrão) mantém o ícone tingido quase-preto quando bloqueado;
        // dimIcon=false (skills, 2026-07-14 — pedido do usuário, "está totalmente preto")
        // deixa o ícone na cor original, só com a sombra de 50% por cima — bem mais claro.
        BuildSection(contentGo.transform, "ARMAS", BuildWeaponSlots(profile), dimIcon: true);
        BuildSection(contentGo.transform, "SKILLS", BuildSkillSlots(profile), dimIcon: false);
    }

    private void BuildSection(Transform parent, string title, List<(Sprite icon, ArsenalSlotUI.Tier tier, System.Action onClick)> slots, bool dimIcon)
    {
        var headerGo = new GameObject($"{title}Header");
        headerGo.transform.SetParent(parent, false);
        var headerLayout = headerGo.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 44f;
        var headerTxt = headerGo.AddComponent<TextMeshProUGUI>();
        headerTxt.text = title;
        headerTxt.fontSize = 28;
        headerTxt.fontStyle = FontStyles.Bold;
        headerTxt.color = theme.textOnLight;
        headerTxt.alignment = TextAlignmentOptions.Center;

        var gridGo = new GameObject($"{title}Grid");
        gridGo.transform.SetParent(parent, false);
        var grid = gridGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CellSize, CellSize);
        grid.spacing = new Vector2(CellSpacing, CellSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        // Centralizado (2026-07-14, pedido do usuário) — antes UpperLeft, deixava um vão vazio à
        // direita já que o grid ocupa a largura toda do Content (childControlWidth=true no
        // VerticalLayoutGroup pai) mas as células não preenchem essa largura sozinhas.
        grid.childAlignment = TextAnchor.UpperCenter;

        foreach (var (icon, tier, onClick) in slots)
        {
            var slotGo = new GameObject("Slot", typeof(RectTransform));
            slotGo.transform.SetParent(gridGo.transform, false);
            slotGo.AddComponent<ArsenalSlotUI>().Build(theme, icon, tier, onClick, dimIcon);
        }
    }

    private List<(Sprite icon, ArsenalSlotUI.Tier tier, System.Action onClick)> BuildWeaponSlots(PlayerProfile profile)
    {
        var result = new List<(Sprite, ArsenalSlotUI.Tier, System.Action)>();
        if (weaponDatabase == null) return result;
        foreach (var family in weaponDatabase.weapons)
        {
            if (family == null) continue;
            var (icon, tier, data) = ResolveWeaponSlot(profile, family);
            result.Add((icon, tier, () => _detailPanel.ShowWeaponDetail(data)));
        }
        return result;
    }

    private List<(Sprite icon, ArsenalSlotUI.Tier tier, System.Action onClick)> BuildSkillSlots(PlayerProfile profile)
    {
        var result = new List<(Sprite, ArsenalSlotUI.Tier, System.Action)>();
        if (skillDatabase == null) return result;
        foreach (var family in skillDatabase.skills)
        {
            // Só a raiz (T1) de cada família representa uma célula da grade — T2/T3 do mesmo
            // family já são cobertos ao resolver o tier mais alto possuído, ver ResolveSkillSlot.
            if (family == null || family.previousTier != null) continue;
            // Garimpeiro/Magneto (2026-07-14, pedido do usuário) — ainda não implementadas
            // (SkillData.effectText vazio pras duas, ver SKILLS_SYSTEM.md/CLAUDE.md), não devem
            // aparecer no Arsenal até terem mecânica de verdade.
            if (family.skillName == "Garimpeiro" || family.skillName == "Magneto") continue;
            var (icon, tier, data) = ResolveSkillSlot(profile, family);
            result.Add((icon, tier, () => _detailPanel.ShowSkillDetail(data)));
        }
        return result;
    }

    // Resolve, pra uma FAMÍLIA de arma (T1 raiz), o maior tier que o personagem possui, o ícone
    // correspondente e o próprio WeaponData a exibir no popup (o possuído, ou a família/T1 como
    // representante quando bloqueado — sobe a cadeia previousTier de cada arma do loadout até
    // achar a raiz, e compara com a família por referência de asset, não por nome).
    private static (Sprite icon, ArsenalSlotUI.Tier tier, WeaponData data) ResolveWeaponSlot(PlayerProfile profile, WeaponData family)
    {
        WeaponData owned = null;
        if (profile?.weapons != null)
        {
            foreach (var w in profile.weapons)
            {
                if (w == null) continue;
                var root = w;
                while (root.previousTier != null) root = root.previousTier;
                if (root == family && (owned == null || w.tier > owned.tier)) owned = w;
            }
        }

        // Não possui em nenhum tier: mostra o ícone/stats da própria família (T1), escurecido —
        // ArsenalSlotUI decide o tint/overlay a partir do tier None; ainda clicável (ver
        // BuildWeaponSlots) pra consultar os atributos base mesmo sem possuir.
        if (owned == null) return (family.icon, ArsenalSlotUI.Tier.None, family);

        // T2/T3 sem sprite próprio herdam do tier anterior (mesmo fallback de
        // WeaponHandler.EquipSpecific/CharacterPanel.ResolveTierFamily).
        var icon = owned.icon;
        var walker = owned;
        while (icon == null && walker.previousTier != null) { walker = walker.previousTier; icon = walker.icon; }
        return (icon, TierFromInt(owned.tier), owned);
    }

    private static (Sprite icon, ArsenalSlotUI.Tier tier, SkillData data) ResolveSkillSlot(PlayerProfile profile, SkillData family)
    {
        SkillData owned = null;
        if (profile?.skills != null)
        {
            foreach (var s in profile.skills)
            {
                if (s == null) continue;
                var root = s;
                while (root.previousTier != null) root = root.previousTier;
                if (root == family && (owned == null || s.tier > owned.tier)) owned = s;
            }
        }

        if (owned == null) return (family.icon, ArsenalSlotUI.Tier.None, family);

        var icon = owned.icon;
        var walker = owned;
        while (icon == null && walker.previousTier != null) { walker = walker.previousTier; icon = walker.icon; }
        return (icon, TierFromInt(owned.tier), owned);
    }

    private static ArsenalSlotUI.Tier TierFromInt(int tier) => tier switch
    {
        1 => ArsenalSlotUI.Tier.T1,
        2 => ArsenalSlotUI.Tier.T2,
        3 => ArsenalSlotUI.Tier.T3,
        _ => ArsenalSlotUI.Tier.None,
    };
}
