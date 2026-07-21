using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

// Painel mínimo mostrado ao fim da REPRODUÇÃO de um replay (AttackSequencer.isReplayPlayback) —
// versão enxuta de CombatResultPanel, sem XP/level-up/save (o profile aqui é um PlayerProfile
// RUNTIME reconstruído do snapshot, não o personagem real do jogador). Mesmo padrão de scaffolding
// (EnsureEventSystem/FindScreenCanvas/overlay+painel), só sem toda a lógica de progressão.
public class ReplayEndPanel : MonoBehaviour
{
    public void Show(bool p1Won, PlayerProfile p1Profile, PlayerProfile p2Profile)
    {
        StartCoroutine(ShowRoutine(p1Won, p1Profile, p2Profile));
    }

    private IEnumerator ShowRoutine(bool p1Won, PlayerProfile p1Profile, PlayerProfile p2Profile)
    {
        yield return new WaitForSeconds(0.8f);

        EnsureEventSystem();

        Canvas canvas = FindScreenCanvas();
        if (canvas == null) yield break;

        MakeOverlay(canvas.transform);
        var panel = MakePanel(canvas.transform);

        string title = "FIM DO REPLAY";
        MakeLabel(panel, title, 40, new Color(0.85f, 0.85f, 0.9f),
            new Vector2(0f, 90f), new Vector2(420f, 50f), bold: true);

        string resultText  = p1Won ? "VITÓRIA" : "DERROTA";
        Color  resultColor = p1Won ? new Color(1f, 0.84f, 0f) : new Color(0.9f, 0.15f, 0.15f);
        string p1Name = p1Profile != null ? p1Profile.profileName : "?";
        string p2Name = p2Profile != null ? p2Profile.profileName : "?";
        MakeLabel(panel, $"{resultText} de {p1Name}", 30, resultColor,
            new Vector2(0f, 30f), new Vector2(420f, 40f), bold: true);
        MakeLabel(panel, $"contra {p2Name}", 22, new Color(0.75f, 0.75f, 0.75f),
            new Vector2(0f, -14f), new Vector2(420f, 34f));

        MakeButton(panel, "Voltar", new Vector2(0f, -100f), () => StartCoroutine(LoadMainMenuAsync()));
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static IEnumerator LoadMainMenuAsync()
    {
        var op = SceneManager.LoadSceneAsync("01_MainMenu");
        while (op != null && !op.isDone) yield return null;
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
        var go = new GameObject("ReplayEndPanel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(480f, 320f);
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

    private static Button MakeButton(GameObject panel, string label, Vector2 pos, System.Action onClick)
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
        return btn;
    }
}
