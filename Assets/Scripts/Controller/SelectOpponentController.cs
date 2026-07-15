using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

// Cena 05_SelectOpponent — grid de até 6 oponentes. Desde a Fatia 6 (2026-07-15), tenta buscar
// adversários REAIS na nuvem primeiro (OpponentSearchService/opponents_index); se vier vazio
// (offline, sem sessão, ou ainda não há ninguém sincronizado) cai pro pool local de sempre
// (CharacterDatabase.opponentCharacters, excluindo o profile que o jogador está usando) — esse
// caminho de fallback não mudou em nada. Toda a UI é construída em código (mesmo padrão de
// CombatResultPanel/CharacterPanel/MainMenuCharacterPreview) — não existe prefab de card nesta
// cena.
public class SelectOpponentController : MonoBehaviour
{
    [SerializeField] private CharacterDatabase characterDatabase;
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;
    [SerializeField] private SelectedOpponentHolder selectedOpponentHolder;

    private const int MaxOpponents = 6;
    private const string CombatSceneName = "04_CombatScenePVP";
    private const string MainMenuSceneName = "01_MainMenu";

    // Referência só pra normalizar as barras de STR/AGI/SPD visualmente entre cards — não é
    // limite de gameplay nenhum, só o valor que preenche a barra 100%.
    private const float StatBarReference = 20f;

    static readonly Color Gold     = new Color(0.85f, 0.72f, 0.35f, 1f);
    static readonly Color PanelBg  = new Color(0.07f, 0.06f, 0.09f, 1f);
    static readonly Color CardBg   = new Color(0.15f, 0.10f, 0.07f, 0.98f);
    static readonly Color BarBg    = new Color(0.08f, 0.08f, 0.08f, 0.9f);
    static readonly Color BarFill  = new Color(0.70f, 0.20f, 0.20f, 0.95f);

    void Start()
    {
        EnsureEventSystem();
        var canvas = CreateCanvas();
        BuildTitle(canvas);
        BuildBackButton(canvas);
        StartCoroutine(BuildGridRoutine(canvas));
    }

    private IEnumerator BuildGridRoutine(GameObject canvas)
    {
        var onlineTask = OpponentSearchService.FetchOpponentsAsync(characterDatabase, MaxOpponents);
        yield return new WaitUntil(() => onlineTask.IsCompleted);

        var opponents = onlineTask.Result;
        if (opponents == null || opponents.Count == 0)
            opponents = GetLocalOpponentPool();

        BuildGrid(canvas, opponents);
    }

    // Pool local de sempre (pré-Fatia 6) — agora só o FALLBACK quando a busca online não retorna
    // ninguém (offline, sem sessão, ou ninguém mais sincronizado ainda). Comportamento idêntico
    // ao de antes desta fatia.
    private List<PlayerProfile> GetLocalOpponentPool()
    {
        var result = new List<PlayerProfile>();
        if (characterDatabase == null) return result;

        var currentPlayer = selectedProfileHolder != null ? selectedProfileHolder.currentProfile : null;

        foreach (var profile in characterDatabase.opponentCharacters)
        {
            if (profile == null || profile == currentPlayer) continue;
            result.Add(profile);
            if (result.Count >= MaxOpponents) break;
        }
        return result;
    }

    private void OnOpponentChosen(PlayerProfile profile)
    {
        if (selectedOpponentHolder == null || profile == null) return;
        selectedOpponentHolder.currentOpponentProfile = profile;
        SceneManager.LoadScene(CombatSceneName);
    }

    // ── UI construction ────────────────────────────────────────────────────

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static GameObject CreateCanvas()
    {
        var go = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        go.AddComponent<GraphicRaycaster>();

        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = PanelBg;

        return go;
    }

    private static void BuildTitle(GameObject canvas)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta = new Vector2(0f, 80f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "ESCOLHA SEU OPONENTE";
        tmp.fontSize = 42;
        tmp.color = Gold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
    }

    private void BuildBackButton(GameObject canvas)
    {
        var go = new GameObject("BtnVoltar");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -40f);
        rt.sizeDelta = new Vector2(160f, 60f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.30f, 0.10f, 0.10f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => SceneManager.LoadScene(MainMenuSceneName));

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "Voltar";
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
    }

    private void BuildGrid(GameObject canvas, List<PlayerProfile> opponents)
    {
        var scrollGo = new GameObject("ScrollView");
        scrollGo.transform.SetParent(canvas.transform, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.5f, 0.5f);
        scrollRt.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRt.pivot     = new Vector2(0.5f, 0.5f);
        scrollRt.sizeDelta = new Vector2(1700f, 820f);
        scrollRt.anchoredPosition = new Vector2(0f, -30f);
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var viewportRt = viewport.AddComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = viewportRt.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = viewportRt;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot     = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize       = new Vector2(260f, 540f);
        grid.spacing        = new Vector2(24f, 24f);
        grid.childAlignment = TextAnchor.UpperCenter;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRt;

        foreach (var profile in opponents)
            BuildCard(content, profile);
    }

    private void BuildCard(GameObject parent, PlayerProfile profile)
    {
        var card = new GameObject($"Card_{profile.profileName}");
        card.transform.SetParent(parent.transform, false);
        var rt = card.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260f, 540f);
        card.AddComponent<Image>().color = CardBg;

        // Todo o conteúdo do card usa pivot no TOPO (0.5,1) — "y" é sempre a distância (negativa)
        // a partir do topo do card, empilhando de cima pra baixo. Antes usava pivot central
        // (0.5,0.5), o que empurrava tudo pra metade inferior do card (deixando um vão vazio
        // enorme no topo) — bug real reportado pelo usuário junto da imagem do personagem
        // faltando (o retrato precisava de um ponto de referência confiável no topo pra fazer
        // sentido "acima do nome").
        var y = -14f;

        const float PortraitSize = 120f;
        var portraitGo = new GameObject("Portrait");
        portraitGo.transform.SetParent(card.transform, false);
        var portraitRt = portraitGo.AddComponent<RectTransform>();
        portraitRt.anchorMin = portraitRt.anchorMax = portraitRt.pivot = new Vector2(0.5f, 1f);
        portraitRt.sizeDelta = new Vector2(PortraitSize, PortraitSize);
        portraitRt.anchoredPosition = new Vector2(0f, y);
        var portraitImg = portraitGo.AddComponent<Image>();
        if (profile.previewIcon != null)
        {
            portraitImg.sprite = profile.previewIcon;
            portraitImg.preserveAspect = true;
        }
        else
        {
            // Sem previewIcon configurado no profile — placeholder sólido em vez de deixar um
            // buraco invisível sem nenhum feedback visual.
            portraitImg.color = new Color(0.25f, 0.20f, 0.15f, 1f);
        }
        y -= PortraitSize + 10f;

        var nameTxt = MakeLabel(card, profile.profileName, 22, Gold, y, new Vector2(240f, 30f), bold: true);
        nameTxt.alignment = TextAlignmentOptions.Center;
        y -= 32f;

        MakeLabel(card, $"Level {profile.level}", 16, Color.white, y, new Vector2(240f, 24f));
        y -= 26f;

        var eff = profile.GetEffectiveStats();
        MakeLabel(card, $"HP {eff.hp}", 16, new Color(0.8f, 0.8f, 0.8f), y, new Vector2(240f, 24f));
        y -= 30f;

        y = MakeStatBar(card, "STR", eff.str, y);
        y = MakeStatBar(card, "AGI", eff.agility, y);
        y = MakeStatBar(card, "SPD", eff.speed, y);
        y -= 10f;

        y = MakeWeaponRow(card, profile, y);
        y -= 10f;

        int battles = PlayerPrefs.GetInt("battles_" + profile.OpponentId(), 0);
        int wins    = PlayerPrefs.GetInt("wins_" + profile.OpponentId(), 0);
        string historyText = battles > 0
            ? $"{battles} batalhas · {wins} vitórias"
            : "Nenhuma batalha ainda";
        MakeLabel(card, historyText, 14, new Color(0.7f, 0.7f, 0.7f), y, new Vector2(240f, 40f));
        y -= 40f + 14f;

        MakeButton(card, "Escolher", y, () => OnOpponentChosen(profile));
    }

    private float MakeStatBar(GameObject card, string label, float value, float y)
    {
        MakeLabel(card, label, 13, new Color(0.75f, 0.75f, 0.75f), y, new Vector2(50f, 20f), xOffset: -95f);

        var bg = new GameObject($"{label}Bar");
        bg.transform.SetParent(card.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 1f);
        bgRt.sizeDelta = new Vector2(140f, 16f);
        bgRt.anchoredPosition = new Vector2(30f, y - 2f);
        bg.AddComponent<Image>().color = BarBg;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bg.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(Mathf.Clamp01(value / StatBarReference), 1f);
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        fillGo.AddComponent<Image>().color = BarFill;

        return y - 24f;
    }

    private float MakeWeaponRow(GameObject card, PlayerProfile profile, float y)
    {
        var row = new GameObject("WeaponIcons");
        row.transform.SetParent(card.transform, false);
        var rt = row.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(230f, 44f);
        rt.anchoredPosition = new Vector2(0f, y);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

        if (profile.weapons != null)
        {
            foreach (var weapon in profile.weapons)
            {
                if (weapon == null) continue;
                var sprite = weapon.icon != null ? weapon.icon : weapon.inHandSprite;
                if (sprite == null) continue;

                var iconGo = new GameObject(weapon.weaponName ?? "Weapon");
                iconGo.transform.SetParent(row.transform, false);
                var iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.sizeDelta = new Vector2(36f, 36f);
                var img = iconGo.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
            }
        }

        return y - 44f;
    }

    // pivot no topo (0.5,1) — "y" é a distância a partir do topo do parent, xOffset desloca do
    // centro horizontal (ex: labels das barras de stat, à esquerda da barra em si).
    private static TextMeshProUGUI MakeLabel(GameObject parent, string text, float fontSize,
        Color color, float y, Vector2 size, bool bold = false, float xOffset = 0f)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(xOffset, y);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        return tmp;
    }

    private static Button MakeButton(GameObject parent, string label, float y, System.Action onClick)
    {
        var go = new GameObject("Button");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(200f, 48f);
        rt.anchoredPosition = new Vector2(0f, y);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.13f, 0.42f, 0.13f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 22f;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return btn;
    }
}
