using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Barra de HP fininha e discreta acima da cabeça do pet — Canvas world-space que segue o pet
// (offset fixo). Redimensiona o preenchimento via RectTransform.anchorMax.x (mesma técnica
// comprovada de CombatHUD.SetFill, usada pela barra real dos personagens) em vez de
// Image.Type.Filled/fillAmount — a 1ª versão usava Filled, copiada do HealthBar.cs, mas esse
// arquivo nunca é chamado por nada no projeto (código morto, nunca validado em jogo de
// verdade); a barra real dos personagens (CombatHUD) sempre usou anchorMax/anchorMin, nunca
// Filled. Bug real reportado pelo usuário (golpe de personagem não diminuía a barra do pet,
// apesar de UpdateBar ser chamado com os valores certos — confirmado via log) era essa
// discrepância entre a técnica usada aqui e a única que de fato é exercitada/visível no jogo.
public class HealthBarPet : MonoBehaviour
{
    private RectTransform fillRt;
    private CanvasGroup canvasGroup;
    private Transform target;
    private Vector3 offset = new Vector3(0f, 0.8f, 0f);

    public static HealthBarPet Create(Transform target)
    {
        var go = new GameObject("HealthBarPet");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Characters";
        canvas.sortingOrder = 100;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80f, 8f);
        go.transform.localScale = Vector3.one * 0.01f;

        var cg = go.AddComponent<CanvasGroup>();

        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.5f);
        StretchRect(bg.GetComponent<RectTransform>());

        // Fill encolhe da direita pra esquerda conforme o HP cai — mesmo esquema de
        // CombatHUD.SetFill (isLeft=true: anchorMax.x = pct).
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(go.transform, false);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.85f, 0.2f, 1f);
        var frt = fillGo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero;
        frt.offsetMax = Vector2.zero;

        var bar = go.AddComponent<HealthBarPet>();
        bar.fillRt = frt;
        bar.canvasGroup = cg;
        bar.target = target;

        go.transform.position = target.position + bar.offset;
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
        float pct = max > 0 ? (float)current / max : 0f;
        fillRt.anchorMax = new Vector2(pct, fillRt.anchorMax.y);
        Debug.Log($"[PetBarDebug] {name}: current={current}, max={max}, pct={pct}, anchorMax={fillRt.anchorMax}, fillRtNull={fillRt == null}, activeInHierarchy={gameObject.activeInHierarchy}");
    }

    public void FadeOutAndDestroy(float duration = 2f)
    {
        StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }
        Destroy(gameObject);
    }
}
