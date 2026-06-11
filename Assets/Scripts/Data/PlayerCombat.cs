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
        SetAttackerLayers();
        yield return null;

        yield return animationController.PlayIdle(settings.idleDuration);
        yield return StrikeRoutine();

        // Combo: hits encadeados sem movimento. O atacante permanece próximo ao
        // defensor e executa apenas slash → dano → Hurt enquanto a chance triggrar.
        while (defender != null && !defender.IsDead && Random.value < ComboChance())
            yield return ComboStrikeRoutine();

        yield return ReturnToSpawn();
        RestoreDefaultLayers();
    }

    private float ComboChance() => weaponHandler.currentType switch
    {
        WeaponType.Fast   => 0.40f,
        WeaponType.Dagger => 0.35f,
        WeaponType.Sword  => 0.25f,
        WeaponType.Heavy  => 0.10f,
        _                 => 0.25f
    };

    private float CritChance() => weaponHandler.currentType switch
    {
        WeaponType.Fast   => 0.25f,
        WeaponType.Dagger => 0.20f,
        WeaponType.Sword  => 0.15f,
        WeaponType.Heavy  => 0.10f,
        _                 => 0.15f
    };

    // Ataque principal: corre até o defensor e executa um hit completo.
    private IEnumerator StrikeRoutine()
    {
        Vector2 targetPos = defender != null ? (Vector2)defender.transform.position : spawnPosition;
        float reach = weaponHandler.currentType switch
        {
            WeaponType.Dagger => 1.5f,
            WeaponType.Heavy  => 2.8f,
            _                 => 2.0f
        };
        Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
        Vector2 attackPos = targetPos - dir * reach;

        yield return animationController.PlayRun(attackPos, settings.runSpeed, movement);
        yield return HitRoutine();
    }

    // Hit de combo: Any State → Slashing (CanTransitionToSelf=1) no controller
    // permite re-triggar do próprio estado Slashing sem precisar aguardar saída.
    private IEnumerator ComboStrikeRoutine()
    {
        yield return new WaitForSeconds(settings.slashingToJumpDelay);
        yield return HitRoutine(applyKnockback: false);
    }

    // Slash → (knockback opcional + Hurt em paralelo) → dano.
    // Knockback ativo no ataque principal; desativado nos hits de combo.
    private IEnumerator HitRoutine(bool applyKnockback = true)
    {
        string slashTrigger = weaponHandler.currentType switch
        {
            WeaponType.Heavy  => "SlashingHeavy",
            WeaponType.Dagger => "SlashingDagger",
            _                 => "Slashing"
        };
        animator.SetTrigger(slashTrigger);
        yield return new WaitForSeconds(settings.slashingDuration * 0.5f);

        if (applyKnockback && defender != null)
        {
            Vector2 pushDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.Knockback(pushDir, settings.knockbackDistance, settings.hurtDuration));
        }

        if (defenderAnimationController != null)
            yield return defenderAnimationController.PlayHurt(settings.hurtDuration);

        bool isCrit      = Random.value < CritChance();
        int  baseDamage  = weaponHandler.CurrentWeaponData?.damage ?? 0;
        int  finalDamage = isCrit ? baseDamage * 2 : baseDamage;

        defender?.GetComponent<HealthSystem>()?.TakeDamage(finalDamage);

        if (defender != null)
        {
            Vector3 popupPos = defender.transform.position
                + Vector3.up   * 1.5f
                + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.Spawn(popupPos, finalDamage, isCrit);
        }

        yield return new WaitForSeconds(settings.slashingDuration * 0.5f);
    }

    // Desliza o personagem na direção pushDirection ao tomar um hit.
    // Iniciado pelo atacante via StartCoroutine para rodar em paralelo com PlayHurt.
    public IEnumerator Knockback(Vector2 pushDirection, float distance, float duration)
    {
        Vector2 from = transform.position;
        Vector2 to   = from + pushDirection * distance;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector2.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = to;
    }

    // Pausa pós-golpe → animação de salto → move para novo spawn → idle.
    private IEnumerator ReturnToSpawn()
    {
        yield return new WaitForSeconds(settings.slashingToJumpDelay);
        yield return animationController.PlayJumpStart(settings.jumpStartDuration);
        spawnPosition = RandomSpawnPosition();
        yield return movement.JumpTo(spawnPosition, settings.runSpeed, settings.jumpHeight);
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
