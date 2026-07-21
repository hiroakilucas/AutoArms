using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Card inteiro clicável (2026-07-16, substitui o botão "Escolher" de SelectOpponentController) —
// escurece no toque (OnPointerDown) pra dar feedback de "sendo pressionado", e a ação de escolha
// dispara em OnPointerClick (NÃO em OnPointerUp) — OnPointerUp é enviado ao mesmo alvo do
// PointerDown mesmo depois de um arraste (ex: rolando o ScrollView que contém os cards), então
// usá-lo escolheria o oponente sem querer ao só rolar a lista. OnPointerClick já é cancelado
// automaticamente pelo EventSystem quando ele detecta que houve um arraste entre o down e o up.
public class PressableCard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [HideInInspector] public Image targetImage;
    [HideInInspector] public Color normalColor;
    [HideInInspector] public Color pressedColor;
    [HideInInspector] public System.Action onChosen;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetImage != null) targetImage.color = pressedColor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (targetImage != null) targetImage.color = normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onChosen?.Invoke();
    }
}
