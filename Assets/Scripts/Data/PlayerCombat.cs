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

    public Animator animator;

    [Header("Defensor")]
    [Tooltip("AnimationController do personagem que sofrerá o dano")]
    public AnimationController defenderAnimationController;
    private void Awake()
    {
        weaponHandler = GetComponent<WeaponHandler>();
        animator = GetComponent<Animator>();
    }
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

        // 3) Slashing — escolhe trigger conforme arma
        if (animationController != null)
        {
            switch (weaponHandler.currentType)
            {
                case WeaponType.Sword:
                    animator.SetTrigger("Slashing");
                    break;
                case WeaponType.Heavy:
                    animator.SetTrigger("SlashingHeavy");
                    break;
                case WeaponType.Dagger:
                    animator.SetTrigger("SlashingDagger");
                    break;
            }

            // agenda Hurt no defensor
            if (defenderAnimationController != null)
                StartCoroutine(DelayedHurt());

            // aguarda duração genérica (ou use settings.slashingDuration)
            yield return new WaitForSeconds(settings.slashingDuration);

            // opcional: limpar trigger (não obrigatório com Trigger)
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
        //weaponHandler?.EquipNext();

        // 8) Idle aguardando próximo turno
        animationController?.SetIdle(true);
    }

    private IEnumerator DelayedHurt()
    {
        yield return new WaitForSeconds(settings.hurtTriggerDelay);
        yield return defenderAnimationController.PlayHurt(settings.hurtDuration);
    }
}
