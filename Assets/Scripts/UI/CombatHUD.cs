using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CombatHUD : MonoBehaviour
{
    private RectTransform p1FillRect;
    private RectTransform p2FillRect;
    private TMP_Text p1Label;
    private TMP_Text p2Label;
    private GameObject _canvasObject;

    public Transform CanvasTransform => _canvasObject != null ? _canvasObject.transform : null;

    // Creates 2x and Skip buttons wired to the given CombatPlayer.
    public void AddSpeedControls(CombatPlayer player)
    {
        if (_canvasObject == null || player == null) return;

        var row = new GameObject("SpeedControls");
        row.transform.SetParent(_canvasObject.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.38f, 0.01f);
        rowRt.anchorMax = new Vector2(0.62f, 0.07f);
        rowRt.offsetMin = Vector2.zero;
        rowRt.offsetMax = Vector2.zero;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;

        MakeSpeedToggleButton(row, player);
        MakeSpeedButton(row, "Skip", () => player.RequestSkip());
    }

    private static readonly Color SpeedNormalBg   = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    private static readonly Color SpeedActiveBg   = new Color(1f, 0.84f, 0f, 1f);

    // Toggle button: "2x" on dark gray when at 1x, "1x" on gold when accelerated to 2x.
    private static void MakeSpeedToggleButton(GameObject parent, CombatPlayer player)
    {
        var go = new GameObject("SpeedToggleBtn");
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = SpeedNormalBg;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var lblGo = new GameObject("Label");
        lblGo.transform.SetParent(go.transform, false);
        var lblRt = lblGo.AddComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = Vector2.zero;
        lblRt.offsetMax = Vector2.zero;
        var txt = lblGo.AddComponent<TextMeshProUGUI>();
        txt.text      = "2x";
        txt.fontSize  = 20;
        txt.color     = Color.white;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;

        btn.onClick.AddListener(() =>
        {
            bool is2x = player.ToggleSpeed();
            img.color = is2x ? SpeedActiveBg : SpeedNormalBg;
            txt.color = is2x ? Color.black : Color.white;
            txt.text  = is2x ? "1x" : "2x";
        });
    }

    private static void MakeSpeedButton(GameObject parent, string label, System.Action onClick)
    {
        var go = new GameObject(label + "Btn");
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.25f, 0.25f, 0.25f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick());

        var lblGo = new GameObject("Label");
        lblGo.transform.SetParent(go.transform, false);
        var lblRt = lblGo.AddComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = Vector2.zero;
        lblRt.offsetMax = Vector2.zero;
        var txt = lblGo.AddComponent<TextMeshProUGUI>();
        txt.text      = label;
        txt.fontSize  = 20;
        txt.color     = Color.white;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
    }

    public void Initialize(HealthSystem health1, HealthSystem health2)
    {
        EnsureEventSystem();
        _canvasObject = CreateCanvas();
        (p1FillRect, p1Label) = CreateBar(_canvasObject, isLeft: true);
        (p2FillRect, p2Label) = CreateBar(_canvasObject, isLeft: false);

        health1.OnHealthChanged += (cur, max) => { SetFill(p1FillRect, cur, max, isLeft: true);  SetLabel(p1Label, cur, max); };
        health2.OnHealthChanged += (cur, max) => { SetFill(p2FillRect, cur, max, isLeft: false); SetLabel(p2Label, cur, max); };
    }

    private static void SetLabel(TMP_Text lbl, int cur, int max)
    {
        if (lbl != null) lbl.text = $"{cur}/{max}";
    }

    // P1: anchorMax.x = health% → barra encolhe da direita para a esquerda
    // P2: anchorMin.x = 1 - health% → barra encolhe da esquerda para a direita
    private static void SetFill(RectTransform rt, int cur, int max, bool isLeft)
    {
        float pct = max > 0 ? (float)cur / max : 0f;
        if (isLeft)
            rt.anchorMax = new Vector2(pct, rt.anchorMax.y);
        else
            rt.anchorMin = new Vector2(1f - pct, rt.anchorMin.y);
    }

    // Without an EventSystem in the scene, UI buttons never receive clicks — the
    // combat scene has none of its own (only 01_MainMenu/02_SelectCharacter do).
    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static GameObject CreateCanvas()
    {
        var go = new GameObject("CombatHUD");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        go.AddComponent<GraphicRaycaster>();

        return go;
    }

    private static (RectTransform fill, TMP_Text label) CreateBar(GameObject canvas, bool isLeft)
    {
        // Outer container — dark border
        var container = new GameObject(isLeft ? "P1Bar" : "P2Bar");
        container.transform.SetParent(canvas.transform, false);
        var crt = container.AddComponent<RectTransform>();
        crt.anchorMin = isLeft ? new Vector2(0.02f, 0.93f) : new Vector2(0.55f, 0.93f);
        crt.anchorMax = isLeft ? new Vector2(0.45f, 0.99f) : new Vector2(0.98f, 0.99f);
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;
        container.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.95f);

        // Inner container — 3px inset, defines the bar area
        var inner = new GameObject("Inner");
        inner.transform.SetParent(container.transform, false);
        var irt = inner.AddComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(3, 3);
        irt.offsetMax = new Vector2(-3, -3);

        // Red background — always full width, reveals as green shrinks
        AddImage(inner, "RedBg", new Color(0.72f, 0.08f, 0.08f));

        // Green fill — width controlled via anchorMax.x (P1) or anchorMin.x (P2)
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(inner.transform, false);
        fillGo.AddComponent<Image>().color = new Color(0.15f, 0.78f, 0.15f);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        // HP label — rendered on top of fill/red, centered over the whole bar
        var lblGo = new GameObject("HPLabel");
        lblGo.transform.SetParent(container.transform, false);
        var lblRt = lblGo.AddComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = Vector2.zero;
        lblRt.offsetMax = Vector2.zero;
        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
        lbl.fontSize = 18;
        lbl.color = Color.white;
        lbl.fontStyle = FontStyles.Bold;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.outlineWidth = 0.35f;
        lbl.outlineColor = Color.black;

        return (fillRt, lbl);
    }

    private static void AddImage(GameObject parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<Image>().color = color;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
