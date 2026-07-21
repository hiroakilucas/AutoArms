using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Efeito de "roleta de cassino" pro ícone de cada caixa de level-up (pedido do usuário,
// 2026-07-21) — cicla rapidamente por sprites aleatórios de um pool compartilhado (skills+armas+
// pets do jogo, ver CombatResultPanel.BuildSpinIconPool), desacelerando até travar no resultado
// FINAL já sorteado (o sorteio em si já aconteceu antes de Play() ser chamado — isso aqui é só a
// revelação visual, não sorteia nada). Cada caixa recebe uma `spinDuration` diferente (maior da
// esquerda pra direita, ver CombatResultPanel.ShowLevelUpChoice) pra travarem em sequência, como
// os rolos de uma slot machine parando um de cada vez.
public class LevelUpReelSpinner : MonoBehaviour
{
    private const float IntervalStart = 0.045f;
    private const float IntervalGrowth = 1.18f; // desacelera a cada troca — mais rápido no início, mais lento perto de travar
    private const float IntervalMax = 0.22f;

    public void Play(Image iconImg, Sprite finalSprite, Color finalColor, List<Sprite> spinPool,
        float spinDuration, System.Action onComplete)
    {
        StartCoroutine(SpinRoutine(iconImg, finalSprite, finalColor, spinPool, spinDuration, onComplete));
    }

    private IEnumerator SpinRoutine(Image iconImg, Sprite finalSprite, Color finalColor,
        List<Sprite> spinPool, float spinDuration, System.Action onComplete)
    {
        float elapsed = 0f;
        float interval = IntervalStart;
        while (elapsed < spinDuration)
        {
            if (spinPool != null && spinPool.Count > 0)
            {
                iconImg.sprite = spinPool[Random.Range(0, spinPool.Count)];
                iconImg.color = Color.white;
            }
            yield return new WaitForSeconds(interval);
            elapsed += interval;
            interval = Mathf.Min(interval * IntervalGrowth, IntervalMax);
        }
        iconImg.sprite = finalSprite;
        iconImg.color = finalColor;
        onComplete?.Invoke();
    }
}
