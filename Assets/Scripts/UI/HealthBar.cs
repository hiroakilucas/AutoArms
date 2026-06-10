using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    private Image fill;
    private Transform target;
    private Vector3 offset;

    public static HealthBar Create(Transform target, Vector3 offset)
    {
        var go = new GameObject("HealthBar");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Characters";
        canvas.sortingOrder = 100;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(150f, 14f);
        go.transform.localScale = Vector3.one * 0.01f;

        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0f, 0f);
        StretchRect(bg.GetComponent<RectTransform>());

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(go.transform, false);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.75f, 0.2f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        StretchRect(fillGo.GetComponent<RectTransform>());

        var bar = go.AddComponent<HealthBar>();
        bar.fill = fillImg;
        bar.target = target;
        bar.offset = offset;

        go.transform.position = target.position + offset;
        return bar;
    }

    private static void StretchRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (target != null)
            transform.position = target.position + offset;
    }

    public void UpdateBar(int current, int max)
    {
        fill.fillAmount = (float)current / max;
    }
}
