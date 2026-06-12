using UnityEngine;
using UnityEngine.UI;

public class CombatHUD : MonoBehaviour
{
    private RectTransform p1FillRect;
    private RectTransform p2FillRect;
    private GameObject _canvasObject;

    public Transform CanvasTransform => _canvasObject != null ? _canvasObject.transform : null;

    public void Initialize(HealthSystem health1, HealthSystem health2)
    {
        _canvasObject = CreateCanvas();
        var canvas = _canvasObject;
        p1FillRect = CreateBar(canvas, isLeft: true);
        p2FillRect = CreateBar(canvas, isLeft: false);

        health1.OnHealthChanged += (cur, max) => SetFill(p1FillRect, cur, max, isLeft: true);
        health2.OnHealthChanged += (cur, max) => SetFill(p2FillRect, cur, max, isLeft: false);
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

    private static GameObject CreateCanvas()
    {
        var go = new GameObject("CombatHUD");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        return go;
    }

    private static RectTransform CreateBar(GameObject canvas, bool isLeft)
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

        return fillRt;
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
