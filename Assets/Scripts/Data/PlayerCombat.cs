using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Componentes de Apoio")]
    public WeaponHandler weaponHandler;
    public MovementController movement;
    public AnimationController animationController;

    [Header("Configurações de Ataque (tempos e velocidades)")]
    public AttackSettings settings;

    [Header("Posições de Turno")]
    public Vector2 startPos = new Vector2(-6.3f, -2.39f);
    public Vector2 targetPos = new Vector2(3.74f, -2.39f);

    [Header("Defensor")]
    public Animator defenderAnimator;

    private void Start()
    {
        // Posiciona no ponto inicial
        transform.position = startPos;
        // Já equipa a primeira arma (índice 0)
        weaponHandler?.EquipNext();
        // Mostra Idle até o turno iniciar
        animationController?.SetIdle(true);
    }

    public IEnumerator AttackRoutine()
    {
        // 1) Idle
        if (animationController != null)
            yield return animationController.PlayIdle(settings.idleDuration);

        // 2) Corrida até targetPos
        if (animationController != null && movement != null)
            yield return animationController.PlayRun(targetPos, settings.runSpeed, movement);

        // 3) Slashing
        if (animationController != null)
            yield return animationController.PlaySlash(settings.slashingDuration);

        // 4) Defensor sofre Hurt
        if (defenderAnimator != null)
        {
            defenderAnimator.SetBool("Hurt", true);
            yield return new WaitForSeconds(settings.hurtDuration);
            defenderAnimator.SetBool("Hurt", false);
        }

        // 5) Delay antes do salto
        yield return new WaitForSeconds(settings.slashingToJumpDelay);

        // 6) JumpStart
        if (animationController != null)
            yield return animationController.PlayJumpStart(settings.jumpStartDuration);

        // 7) Salto em arco de volta ao startPos
        if (movement != null)
            yield return movement.JumpTo(targetPos, startPos, settings.runSpeed, settings.jumpHeight);

        // 8) Prepara próxima arma (próximo índice) antes do próximo turno
        weaponHandler?.EquipNext();

        // 9) Volta ao Idle aguardando próximo turno
        animationController?.SetIdle(true);
    }
}
