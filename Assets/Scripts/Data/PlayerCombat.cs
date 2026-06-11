using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Support Components")]
    public WeaponHandler weaponHandler;
    public MovementController movement;
    public AnimationController animationController;

    [Header("Attack Settings")]
    public AttackSettings settings;

    [Tooltip("True for Player1, false for Player2")]
    public bool isPlayer1;

    [Header("Defender")]
    public PlayerCombat defender;
    public AnimationController defenderAnimationController;

    public bool IsDead => GetComponent<HealthSystem>()?.IsDead ?? false;

    private Animator animator;
    private List<SpriteRenderer> bodyRenderers;
    private string defaultSortingLayer;
    private Vector2 spawnPosition;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        weaponHandler = GetComponent<WeaponHandler>();
        bodyRenderers = new List<SpriteRenderer>(GetComponentsInChildren<SpriteRenderer>());
        if (bodyRenderers.Count > 0)
            defaultSortingLayer = bodyRenderers[0].sortingLayerName;
    }

    private void Start()
    {
        EquipAndSpawn();
        spawnPosition = RandomSpawnPosition();
        transform.position = spawnPosition;
        animationController.SetIdle(true);
    }

    public IEnumerator AttackRoutine()
    {
        Vector2 targetPos = defender != null ? (Vector2)defender.transform.position : spawnPosition;

        SetAttackerLayers();
        yield return null;

        float reach = weaponHandler.currentType switch
        {
            WeaponType.Dagger => 1.5f,
            WeaponType.Heavy  => 2.8f,
            _                 => 2.0f
        };
        Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
        Vector2 attackPos = targetPos - dir * reach;

        yield return animationController.PlayIdle(settings.idleDuration);
        yield return animationController.PlayRun(attackPos, settings.runSpeed, movement);

        string slashTrigger = weaponHandler.currentType switch
        {
            WeaponType.Heavy  => "SlashingHeavy",
            WeaponType.Dagger => "SlashingDagger",
            _                 => "Slashing"
        };
        animator.SetTrigger(slashTrigger);
        yield return new WaitForSeconds(settings.slashingDuration * 0.5f);

        if (defenderAnimationController != null)
            yield return defenderAnimationController.PlayHurt(settings.hurtDuration);

        defender?.GetComponent<HealthSystem>()?.TakeDamage(weaponHandler.CurrentWeaponData?.damage ?? 0);

        yield return new WaitForSeconds(settings.slashingDuration * 0.5f);
        yield return new WaitForSeconds(settings.slashingToJumpDelay);

        yield return animationController.PlayJumpStart(settings.jumpStartDuration);
        spawnPosition = RandomSpawnPosition();
        yield return movement.JumpTo(spawnPosition, settings.runSpeed, settings.jumpHeight);

        RestoreDefaultLayers();
        animationController.SetIdle(true);
    }

    private void EquipAndSpawn()
    {
        weaponHandler.EquipNext();
        SetWeaponLayer(weaponHandler, isPlayer1 ? "Weapons" : "Weapons2");
    }

    private Vector2 RandomSpawnPosition()
    {
        float x = isPlayer1 ? Random.Range(-7.25f, -4.79f) : Random.Range(4.79f, 7.25f);
        float y = Random.Range(-3.90f, -0.81f);
        return new Vector2(x, y);
    }

    private void SetAttackerLayers()
    {
        SetBodyLayer(bodyRenderers, "Characters");
        SetWeaponLayer(weaponHandler, "Weapons");

        if (defender != null)
        {
            SetBodyLayer(defender.bodyRenderers, "Characters2");
            SetWeaponLayer(defender.weaponHandler, "Weapons2");
        }
    }

    private void RestoreDefaultLayers()
    {
        SetBodyLayer(bodyRenderers, defaultSortingLayer);
        if (defender != null)
            SetBodyLayer(defender.bodyRenderers, defaultSortingLayer);
    }

    private static void SetBodyLayer(List<SpriteRenderer> renderers, string layerName)
    {
        foreach (var sr in renderers)
            sr.sortingLayerName = layerName;
    }

    private static void SetWeaponLayer(WeaponHandler handler, string layerName)
    {
        var weapon = handler.CurrentWeapon;
        if (weapon == null) return;
        weapon.GetComponent<SpriteRenderer>().sortingLayerName = layerName;
    }
}
