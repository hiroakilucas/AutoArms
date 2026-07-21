using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ícone de diamante "voando" do card comprado até o contador do header da Loja (ShopController) —
// pool ESTÁTICO com SetActive(true/false) em vez de Instantiate/Destroy a cada clique, mesmo
// padrão de DamagePopup.cs (combate) e CombatPlayer._ghostPool/RentGhost (rastro do Fierce Brute):
// cresce sob demanda, nunca destrói. `RemoveAll(p => p == null)` no início de cada Rent() purga
// entradas cujo GameObject já foi destruído (troca de cena — 06_Loja sai/entra de novo dentro da
// mesma sessão de Play) antes de tentar reusar alguma — mesmo motivo/mesmo padrão de DamagePopup.
public class FlyingDiamondIcon : MonoBehaviour
{
    private RectTransform _rt;
    private Image _img;
    private Coroutine _routine;

    private static readonly List<FlyingDiamondIcon> _pool = new List<FlyingDiamondIcon>();

    private static FlyingDiamondIcon Rent(Transform parent, Sprite icon, float size)
    {
        _pool.RemoveAll(p => p == null);

        FlyingDiamondIcon flying = null;
        foreach (var p in _pool)
        {
            if (!p.gameObject.activeSelf) { flying = p; break; }
        }

        if (flying == null)
        {
            var go = new GameObject("FlyingDiamondIcon (pooled)", typeof(RectTransform));
            flying = go.AddComponent<FlyingDiamondIcon>();
            flying._rt = (RectTransform)go.transform;
            flying._img = go.AddComponent<Image>();
            flying._img.preserveAspect = true;
            flying._img.raycastTarget = false;
            _pool.Add(flying);
        }

        flying.gameObject.SetActive(true);
        flying.transform.SetParent(parent, false);
        flying.transform.SetAsLastSibling(); // sempre desenha por cima do resto da UI da Loja
        flying._img.sprite = icon;
        flying._img.color = Color.white;
        flying._rt.sizeDelta = new Vector2(size, size);
        // Escala 0 (não 1) — o ícone fica invisível durante o atraso de largada escalonado
        // (WaitForSeconds em FlyRoutine) e só "aparece" quando o pop de entrada de fato começa;
        // sem isso, ele ficava visível e parado em tamanho cheio no botão durante o delay, depois
        // encolhia de repente pra 0 só pra crescer de novo — um "pisca" estranho.
        flying._rt.localScale = Vector3.zero;
        return flying;
    }

    // Reajustado (2026-07-21) pra caber com folga na janela de 0.4-0.8s pedida mesmo com o "pop"
    // de entrada novo somado — pior caso (8 ícones, i=7): 7×0.03 (delay) + 0.06 (pop) + 0.42
    // (voo) + 0.08 (fade) ≈ 0.77s.
    private const float MinIconDelay = 0.012f;
    private const float MaxIconDelay = 0.03f;
    private const float MinFlightDuration = 0.28f;
    private const float MaxFlightDuration = 0.42f;
    private const float ArrivalFadeDuration = 0.08f;
    private const float SpawnPopDuration = 0.06f;
    // 48→64 (2026-07-21, pedido do usuário — efeito relatado como "não apareceu"; aumentado pra
    // ficar mais difícil de passar despercebido, junto do "pop" de entrada abaixo).
    private const float IconSize = 64f;

    // Dispara `count` ícones (5-8, ver ShopController.SpawnDiamondBurst) de `fromWorldPos` até
    // `toWorldPos` (posição real do ícone do contador) — cada um com atraso de largada e duração
    // de voo levemente variados (rajada, não saem todos juntos) e trajetória em ARCO (Bézier
    // quadrática com ponto de controle acima da linha reta, escalado pela distância — não uma
    // linha reta nem um arco de altura fixa). `onArrive` é chamado a cada ícone que chega no
    // destino (ShopController usa isso pra incrementar o número do contador aos poucos, não tudo
    // de uma vez). Todos os parâmetros já em posição de MUNDO (`.position`, não `.anchoredPosition`
    // — mesmo espaço de coordenadas de qualquer RectTransform dentro do mesmo Canvas
    // ScreenSpaceOverlay), então não precisa de conversão entre `from`/`to`/o card clicado.
    public static void Burst(Transform parent, Sprite icon, Vector3 fromWorldPos, Vector3 toWorldPos,
        int count, System.Action onArrive)
    {
        for (int i = 0; i < count; i++)
        {
            var flying = Rent(parent, icon, IconSize);
            flying._rt.position = fromWorldPos;
            float delay = i * Random.Range(MinIconDelay, MaxIconDelay);
            float duration = Random.Range(MinFlightDuration, MaxFlightDuration);
            if (flying._routine != null) flying.StopCoroutine(flying._routine);
            flying._routine = flying.StartCoroutine(flying.FlyRoutine(fromWorldPos, toWorldPos, delay, duration, onArrive));
        }
    }

    private IEnumerator FlyRoutine(Vector3 from, Vector3 to, float delay, float duration, System.Action onArrive)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // "Pop" de entrada (2026-07-21) — cresce de 0 até um pouco além de 1 (overshoot) antes de
        // acomodar em 1, em vez de já aparecer estático no tamanho final. Deixa o início do voo
        // mais chamativo/perceptível (pedido do usuário: efeito relatado como "não apareceu").
        float popElapsed = 0f;
        while (popElapsed < SpawnPopDuration)
        {
            popElapsed += Time.deltaTime;
            float pt = Mathf.Clamp01(popElapsed / SpawnPopDuration);
            float overshoot = Mathf.Sin(pt * Mathf.PI * 0.5f) * 1.15f;
            _rt.localScale = Vector3.one * overshoot;
            yield return null;
        }
        _rt.localScale = Vector3.one;

        // Ponto de controle proporcional à DISTÂNCIA (não um offset fixo em pixels) — arco
        // consistente independente de onde o card clicado fica na tela em relação ao contador.
        float distance = Vector3.Distance(from, to);
        float arcHeight = Mathf.Clamp(distance * 0.35f, 60f, 220f);
        Vector3 mid = (from + to) * 0.5f + Vector3.up * arcHeight;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Bézier quadrática (De Casteljau com 1 ponto de controle) — curva suave em arco.
            Vector3 a = Vector3.Lerp(from, mid, t);
            Vector3 b = Vector3.Lerp(mid, to, t);
            _rt.position = Vector3.Lerp(a, b, t);
            yield return null;
        }
        _rt.position = to;
        onArrive?.Invoke();

        float fadeElapsed = 0f;
        while (fadeElapsed < ArrivalFadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            float ft = 1f - Mathf.Clamp01(fadeElapsed / ArrivalFadeDuration);
            _img.color = new Color(1f, 1f, 1f, ft);
            _rt.localScale = Vector3.one * Mathf.Max(0.05f, ft);
            yield return null;
        }

        gameObject.SetActive(false);
        _routine = null;
    }
}
