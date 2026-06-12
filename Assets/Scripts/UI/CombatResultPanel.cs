using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

public class CombatResultPanel : MonoBehaviour
{
    public void Show(bool player1Won, int xpGained, int xpBefore, int levelBefore,
                     PlayerProfile profile, bool didLevelUp)
    {
        StartCoroutine(ShowRoutine(player1Won, xpGained, xpBefore, levelBefore, profile, didLevelUp));
    }

    private IEnumerator ShowRoutine(bool player1Won, int xpGained, int xpBefore, int levelBefore,
                                    PlayerProfile profile, bool didLevelUp)
    {
        yield return new WaitForSeconds(0.8f);

        EnsureEventSystem();

        Canvas canvas = FindScreenCanvas();
        if (canvas == null) yield break;

        MakeOverlay(canvas.transform);

        var panel = MakePanel(canvas.transform);

        // Title
        string titleText = player1Won ? "VITÓRIA!" : "DERROTA!";
        Color  titleColor = player1Won ? new Color(1f, 0.84f, 0f) : new Color(0.9f, 0.15f, 0.15f);
        MakeLabel(panel, titleText, 52, titleColor, pos: new Vector2(0, 155f), size: new Vector2(420f, 65f), bold: true);

        // XP gained
        MakeLabel(panel, $"+{xpGained} XP", 36, new Color(0.45f, 1f, 0.45f),
            new Vector2(0, 90f), new Vector2(280f, 46f));

        // XP progress bar
        int xpRequiredBefore = XpSystem.XpRequired(levelBefore);
        int xpRequiredAfter  = XpSystem.XpRequired(profile.level);
        var barFill = MakeXpBar(panel, new Vector2(0f, 45f), new Vector2(380f, 22f));

        // XP label — shows old-level state initially, updated after level-up anim
        string initialXpText = didLevelUp
            ? $"{xpRequiredBefore} / {xpRequiredBefore} XP"
            : $"{profile.xpCurrent} / {xpRequiredBefore} XP";
        var xpLabel = MakeLabel(panel, initialXpText, 21, Color.white,
            new Vector2(0f, 12f), new Vector2(320f, 28f));

        // Current level
        MakeLabel(panel, $"Level {profile.level}", 26, new Color(0.82f, 0.82f, 0.82f),
            new Vector2(0f, -26f), new Vector2(280f, 34f));

        // Battles remaining
        MakeLabel(panel, $"Batalhas restantes: {profile.battlesRemaining} / 6", 20,
            new Color(0.65f, 0.65f, 0.65f),
            new Vector2(0f, -66f), new Vector2(380f, 30f));

        // Level-up text (hidden until needed)
        var levelUpLabel = MakeLabel(panel, $"LEVEL UP!  →  Level {profile.level}", 30,
            new Color(1f, 0.84f, 0f),
            new Vector2(0f, -110f), new Vector2(420f, 40f), bold: true);
        levelUpLabel.gameObject.SetActive(false);

        // Continue button
        MakeButton(panel, "Continuar", new Vector2(0f, -172f),
            () => SceneManager.LoadScene("01_MainMenu"));

        // ── Animate XP bar ──────────────────────────────────────────────────
        float startFill  = xpRequiredBefore > 0 ? (float)xpBefore / xpRequiredBefore : 0f;
        float targetFill = didLevelUp ? 1f
            : (xpRequiredBefore > 0 ? (float)profile.xpCurrent / xpRequiredBefore : 0f);

        yield return AnimateBar(barFill, startFill, targetFill, 0.75f);

        if (didLevelUp)
        {
            yield return new WaitForSeconds(0.15f);
            levelUpLabel.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                float s = 1f + Mathf.Sin(elapsed / 0.5f * Mathf.PI) * 0.28f;
                levelUpLabel.transform.localScale = Vector3.one * s;
                elapsed += Time.deltaTime;
                yield return null;
            }
            levelUpLabel.transform.localScale = Vector3.one;

            // Reset bar and label to new level's current XP
            barFill.fillAmount = xpRequiredAfter > 0 ? (float)profile.xpCurrent / xpRequiredAfter : 0f;
            xpLabel.text = $"{profile.xpCurrent} / {xpRequiredAfter} XP";
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IEnumerator AnimateBar(Image fill, float from, float to, float duration)
    {
        fill.fillAmount = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            fill.fillAmount = Mathf.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        fill.fillAmount = to;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static Canvas FindScreenCanvas()
    {
        foreach (var c in FindObjectsOfType<Canvas>())
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        return null;
    }

    private static void MakeOverlay(Transform parent)
    {
        var go = new GameObject("Overlay");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.65f);
        img.raycastTarget = false;
    }

    private static GameObject MakePanel(Transform parent)
    {
        var go = new GameObject("ResultPanel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(500f, 450f);
        rt.anchoredPosition = Vector2.zero;
        go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.13f, 0.97f);
        return go;
    }

    private static TextMeshProUGUI MakeLabel(GameObject panel, string text, float fontSize,
        Color color, Vector2 pos, Vector2 size, bool bold = false)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(panel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        return tmp;
    }

    private static Image MakeXpBar(GameObject panel, Vector2 pos, Vector2 size)
    {
        var bg = new GameObject("XpBarBg");
        bg.transform.SetParent(panel.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = size;
        bgRt.anchoredPosition = pos;
        bg.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bg.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        var img = fillGo.AddComponent<Image>();
        img.color      = new Color(0.25f, 0.55f, 1f);
        img.type       = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillAmount = 0f;
        return img;
    }

    private static void MakeButton(GameObject panel, string label, Vector2 pos, System.Action onClick)
    {
        var go = new GameObject("Button");
        go.transform.SetParent(panel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(240f, 54f);
        rt.anchoredPosition = pos;
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
        tmp.fontSize  = 26f;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
    }
}
