using System;
using System.Collections;
using UnityEngine;

// Animação de giro da roleta horizontal de "abertura de case" (2026-07-23) — variante horizontal
// de LevelUpReelSpinner.cs (que cicla o sprite de UM ícone parado, efeito "slot" vertical); aqui a
// faixa inteira (Content, já populada com N ícones antes de Play() ser chamado) translada em X até
// parar com o ícone vencedor exatamente sob o marcador central. Sem DOTween (não está no projeto,
// confirmado) — easing manual via coroutine, mesmo espírito do resto do projeto.
public class CaseReelSpinner : MonoBehaviour
{
    // "Rápido no início, desacelera nos últimos ~30%" (pedido original, estilo CS:GO) —
    // easeOutQuint tem velocidade alta no começo e cai suavemente até 0 no final, sem precisar de
    // uma curva composta/piecewise pra separar as duas fases.
    private static float EaseOutQuint(float t) => 1f - Mathf.Pow(1f - t, 5f);

    public void Play(RectTransform content, float startX, float finalX, float duration, Action onComplete)
    {
        StartCoroutine(SpinRoutine(content, startX, finalX, duration, onComplete));
    }

    private IEnumerator SpinRoutine(RectTransform content, float startX, float finalX, float duration, Action onComplete)
    {
        var pos = content.anchoredPosition;
        pos.x = startX;
        content.anchoredPosition = pos;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            pos.x = Mathf.Lerp(startX, finalX, EaseOutQuint(t));
            content.anchoredPosition = pos;
            yield return null;
        }

        pos.x = finalX;
        content.anchoredPosition = pos;
        onComplete?.Invoke();
    }
}
