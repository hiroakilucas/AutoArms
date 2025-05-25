using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Componentes de Apoio")]
    public WeaponHandler weaponHandler;
    public MovementController movement;
    public AnimationController animationController;

    [Header("Configurações de Ataque")]
    public AttackSettings settings;

    [Header("Posições de Turno")]
    public Vector2 startPos = new Vector2(-6.3f, -2.39f);
    public Vector2 targetPos = new Vector2(3.74f, -2.39f);

    [Header("Defensor")]
    public PlayerCombat defender;
    public AnimationController defenderAnimationController;

    private Animator animator;
    private List<SpriteRenderer> allRenderers;

    // para restaurar após o turno
    private string defaultCharLayer;
    private int defaultCharOrder;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        weaponHandler = GetComponent<WeaponHandler>();

        // captura todos os SpriteRenderers deste personagem
        allRenderers = new List<SpriteRenderer>(GetComponentsInChildren<SpriteRenderer>());

        // guarda camada e order originais
        if (allRenderers.Count > 0)
        {
            defaultCharLayer = allRenderers[0].sortingLayerName;
            defaultCharOrder = allRenderers[0].sortingOrder;
        }
    }

    private void Start()
    {
        // 1) Posição inicial e arma já equipada
        transform.position = startPos;
        weaponHandler.EquipNext();

        // 2) Garante que a arma recém-instanciada já fique em Weapons/0
        SetWeaponLayerAndOrder(weaponHandler, "Weapons", 0);

        // 3) Idle inicial
        animationController.SetIdle(true);
    }

    public IEnumerator AttackRoutine()
    {
        // 1) Prepara renderização do turno
        SetCharacterLayerAndOrder(allRenderers, "Characters", 4);
        SetWeaponLayerAndOrder(weaponHandler, "Weapons", 3);

        if (defender != null)
        {
            SetCharacterLayerAndOrder(defender.allRenderers, "Characters2", 2);
            SetWeaponLayerAndOrder(defender.weaponHandler, "Weapons2", 1);
        }

        // **pequena pausa para o Unity aplicar os novos sorting layers**
        yield return null;

        // 2) Idle
        yield return animationController.PlayIdle(settings.idleDuration);

        // 3) Run
        yield return animationController.PlayRun(targetPos, settings.runSpeed, movement);

        // 4) Slashing
        switch (weaponHandler.currentType)
        {
            case WeaponType.Sword: animator.SetTrigger("Slashing"); break;
            case WeaponType.Heavy: animator.SetTrigger("SlashingHeavy"); break;
            case WeaponType.Dagger: animator.SetTrigger("SlashingDagger"); break;
        }
        yield return new WaitForSeconds(settings.slashingDuration);

        // 5) Hurt imediato no defensor
        if (defenderAnimationController != null)
            yield return defenderAnimationController.PlayHurt(settings.hurtDuration);

        // 6) Delay antes do salto
        yield return new WaitForSeconds(settings.slashingToJumpDelay);

        // 7) JumpStart e salto de volta
        yield return animationController.PlayJumpStart(settings.jumpStartDuration);
        yield return movement.JumpTo(targetPos, startPos, settings.runSpeed, settings.jumpHeight);

        // 8) Restaura estado de renderização
        SetCharacterLayerAndOrder(allRenderers, defaultCharLayer, defaultCharOrder);
        if (defender != null)
            SetCharacterLayerAndOrder(defender.allRenderers, defaultCharLayer, defaultCharOrder);

        // 9) Idle final
        animationController.SetIdle(true);

    }

    private void SetCharacterLayerAndOrder(List<SpriteRenderer> rends, string layerName, int order)
    {
        foreach (var sr in rends)
        {
            sr.sortingLayerName = layerName;
            sr.sortingOrder = order;
        }
    }

    private void SetWeaponLayerAndOrder(WeaponHandler handler, string layerName, int order)
    {
        var w = handler.CurrentWeapon;
        if (w == null) return;

        var sr = w.GetComponent<SpriteRenderer>();
        sr.sortingLayerName = layerName;
        sr.sortingOrder = order;
    }
}
