using System.Collections;
using UnityEngine;

// Componente compartilhado (2026-07-08) entre `MainMenuCharacterPreview` (personagem central do
// 01_MainMenu, world-space, clique via Collider2D/OnMouseDown) e `CharacterSelectController`
// (portrait de 02_SelectCharacter, renderizado numa RenderTexture, clique via Button de UI) —
// mesma reação nos dois lugares: sorteia Hurt ou Slashing (mesmos triggers do Animator usados em
// combate) e volta pro Idle sozinho ao final. `reactionPlaying` evita disparar uma 2ª reação por
// cima de uma já em andamento.
public class CharacterPreviewReaction : MonoBehaviour
{
    private AnimationController animController;
    private bool reactionPlaying;

    public void Init(AnimationController controller) => animController = controller;

    // Só usado pelo preview do 01_MainMenu (world-space + Collider2D) — Unity envia essa
    // mensagem automaticamente pro GameObject dono do collider clicado, sem precisar de
    // EventSystem/GraphicRaycaster (caminho totalmente separado do clique em UI).
    private void OnMouseDown() => TriggerReaction();

    // Chamado direto pelo `Button.onClick` do portrait de 02_SelectCharacter (clique em UI, não
    // physics), e indiretamente por `OnMouseDown` acima no 01_MainMenu.
    public void TriggerReaction()
    {
        if (animController == null || reactionPlaying) return;
        StartCoroutine(PlayReaction());
    }

    private IEnumerator PlayReaction()
    {
        reactionPlaying = true;
        if (Random.value < 0.5f)
            yield return animController.PlayHurt(0.3f);
        else
            yield return animController.PlaySlash(0.4f);
        reactionPlaying = false;
    }
}
