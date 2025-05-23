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
    [Tooltip("AnimationController do personagem que sofrerá o dano")]
    public AnimationController defenderAnimationController;

    private void Start()
    {
        transform.position = startPos;
        weaponHandler?.EquipNext();
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

        // 3) Slashing + agendamento do Hurt
        if (animationController != null)
        {
            // dispara o Hurt no defensor após o atraso
            if (defenderAnimationController != null)
                StartCoroutine(DelayedHurt());

            // dispara o Slashing e aguarda o término
            yield return animationController.PlaySlash(settings.slashingDuration);
        }

        // 4) Delay antes do salto
        yield return new WaitForSeconds(settings.slashingToJumpDelay);

        // 5) JumpStart
        if (animationController != null)
            yield return animationController.PlayJumpStart(settings.jumpStartDuration);

        // 6) Salto em arco de volta
        if (movement != null)
            yield return movement.JumpTo(targetPos, startPos, settings.runSpeed, settings.jumpHeight);

        // 7) Prepara próxima arma
        weaponHandler?.EquipNext();

        // 8) Idle aguardando próximo turno
        animationController?.SetIdle(true);
    }

    private IEnumerator DelayedHurt()
    {
        yield return new WaitForSeconds(settings.hurtTriggerDelay);
        yield return defenderAnimationController.PlayHurt(settings.hurtDuration);
    }
}
