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

        health1.OnHealthChanged += (cur, max) => UpdateBar(p1Fill, cur, max);
        health2.OnHealthChanged += (cur, max) => UpdateBar(p2Fill, cur, max);

        UpdateBar(p1Fill, health1.CurrentHealth, health1.MaxHealth);
        UpdateBar(p2Fill, health2.CurrentHealth, health2.MaxHealth);
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

    private static Image CreateBar(GameObject canvas, bool isLeft)
    {
        var container = new GameObject(isLeft ? "P1Bar" : "P2Bar");
        container.transform.SetParent(canvas.transform, false);
        var rt = container.AddComponent<RectTransform>();

        // Each bar occupies 43% of the screen width near the top
        rt.anchorMin = isLeft ? new Vector2(0.02f, 0.93f) : new Vector2(0.55f, 0.93f);
        rt.anchorMax = isLeft ? new Vector2(0.45f, 0.99f) : new Vector2(0.98f, 0.99f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Dark background
        var bg = new GameObject("Background");
        bg.transform.SetParent(container.transform, false);
        bg.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        StretchRect(bg.GetComponent<RectTransform>());

        // Colored fill with slight inset so the background shows as a border
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(container.transform, false);
        var fill = fillGo.AddComponent<Image>();
        fill.color = new Color(0.1f, 0.8f, 0.1f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = isLeft ? (int)Image.OriginHorizontal.Left : (int)Image.OriginHorizontal.Right;
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(3, 3);
        fillRt.offsetMax = new Vector2(-3, -3);

        return fill;
    }

    private static void StretchRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void UpdateBar(Image fill, int current, int max)
    {
        fill.fillAmount = (float)current / max;
        fill.color = HealthColor((float)current / max);
    }

    // Green (full) → Yellow (half) → Red (empty)
    private static Color HealthColor(float ratio)
    {
        if (ratio > 0.5f)
            return Color.Lerp(new Color(0.9f, 0.7f, 0.1f), new Color(0.1f, 0.8f, 0.1f), (ratio - 0.5f) * 2f);
        return Color.Lerp(new Color(0.85f, 0.1f, 0.1f), new Color(0.9f, 0.7f, 0.1f), ratio * 2f);
    }
}
