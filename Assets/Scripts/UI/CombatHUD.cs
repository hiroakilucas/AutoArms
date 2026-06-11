using UnityEngine;
using UnityEngine.UI;

public class CombatHUD : MonoBehaviour
{
    private Image p1Fill;
    private Image p2Fill;

    public void Initialize(HealthSystem health1, HealthSystem health2)
    {
        var canvas = CreateCanvas();
        p1Fill = CreateBar(canvas, isLeft: true);
        p2Fill = CreateBar(canvas, isLeft: false);

        health1.OnHealthChanged += (cur, max) => p1Fill.fillAmount = (float)cur / max;
        health2.OnHealthChanged += (cur, max) => p2Fill.fillAmount = (float)cur / max;

        p1Fill.fillAmount = 1f;
        p2Fill.fillAmount = 1f;
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

    // isLeft=true  → P1: fill drena da direita para esquerda (fillOrigin=Left)
    // isLeft=false → P2: fill drena da esquerda para direita (fillOrigin=Right)
    private static Image CreateBar(GameObject canvas, bool isLeft)
    {
        var container = new GameObject(isLeft ? "P1Bar" : "P2Bar");
        container.transform.SetParent(canvas.transform, false);

        var rt = container.AddComponent<RectTransform>();
        rt.anchorMin = isLeft ? new Vector2(0.02f, 0.93f) : new Vector2(0.55f, 0.93f);
        rt.anchorMax = isLeft ? new Vector2(0.45f, 0.99f) : new Vector2(0.98f, 0.99f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Dark border
        container.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.95f);

        // Red background — representa o dano acumulado
        AddImage(container, "RedBackground", new Color(0.72f, 0.08f, 0.08f), inset: 3);

        // Green fill — representa HP restante; encolhe revelando o vermelho
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(container.transform, false);
        var fill = fillGo.AddComponent<Image>();
        fill.color = new Color(0.15f, 0.78f, 0.15f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = isLeft
            ? (int)Image.OriginHorizontal.Left
            : (int)Image.OriginHorizontal.Right;
        SetInset(fill.GetComponent<RectTransform>(), 3);

        return fill;
    }

    private static void AddImage(GameObject parent, string name, Color color, float inset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<Image>().color = color;
        SetInset(go.GetComponent<RectTransform>(), inset);
    }

    private static void SetInset(RectTransform rt, float px)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(px, px);
        rt.offsetMax = new Vector2(-px, -px);
    }
}
