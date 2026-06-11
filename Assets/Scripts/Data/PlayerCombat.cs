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

    [Header("Attributes")]
    public int agility = 10;

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

    // Fierce Brute (skill futura): adiciona +0.10f a este valor permanentemente.
    private float CritChance() => weaponHandler.currentType switch
    {
        WeaponType.Dagger => 0.08f,
        WeaponType.Sword  => 0.05f,
        WeaponType.Heavy  => 0.03f,
        _                 => 0.05f
    };

    // Sixth Sense (skill futura): adiciona +0.10f a este valor permanentemente.
    private float DodgeChance()
    {
        if (defender == null) return 0f;
        float baseChance = defender.weaponHandler.currentType switch
        {
            WeaponType.Fast   => 0.20f,
            WeaponType.Dagger => 0.15f,
            WeaponType.Sword  => 0.10f,
            WeaponType.Heavy  => 0.05f,
            _                 => 0.10f
        };
        float agilityBonus = Mathf.Max(0, defender.agility - 10) * 0.01f;
        return baseChance + agilityBonus;
    }

    // Posição de ataque: imediatamente fora do alcance da arma, na direção do defensor.
    private Vector2 AttackPosition()
    {
        if (defender == null) return spawnPosition;
        Vector2 defPos = (Vector2)defender.transform.position;
        float reach = weaponHandler.currentType switch
        {
            WeaponType.Dagger => 1.5f,
            WeaponType.Heavy  => 2.8f,
            _                 => 2.0f
        };
        Vector2 dir = (defPos - (Vector2)transform.position).normalized;
        return defPos - dir * reach;
    }

    // Ataque principal: corre até o defensor e executa um hit completo.
    private IEnumerator StrikeRoutine()
    {
        yield return animationController.PlayRun(AttackPosition(), settings.runSpeed, movement);
        yield return HitRoutine();
    }

    // Hit de combo: reposiciona se o defensor se moveu (knockback/esquiva), depois executa o hit.
    // Any State → Slashing (CanTransitionToSelf=1) no controller permite re-entrar no Slashing após o run.
    private IEnumerator ComboStrikeRoutine()
    {
        yield return new WaitForSeconds(settings.slashingToJumpDelay);
        Vector2 attackPos = AttackPosition();
        if (Vector2.Distance(transform.position, attackPos) > 0.3f)
            yield return animationController.PlayRun(attackPos, settings.runSpeed, movement);
        yield return HitRoutine();
    }

    // Slash → (knockback + Hurt em paralelo) → dano.
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

        if (defender != null && Random.value < DodgeChance())
        {
            Vector2 dodgeDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.DodgeLeap(dodgeDir, settings.knockbackDistance));

            Vector3 dodgePos = defender.transform.position
                + Vector3.up   * 1.5f
                + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.SpawnDodge(dodgePos);
            yield return new WaitForSeconds(settings.slashingDuration * 0.5f);
            yield break;
        }

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

    // Salta para trás ao esquivar: JumpStart animation + arco parabólico na direção oposta ao ataque.
    public IEnumerator DodgeLeap(Vector2 pushDirection, float distance)
    {
        Vector2 to = (Vector2)transform.position + pushDirection * distance;
        float duration = settings.hurtDuration;
        StartCoroutine(animationController.PlayJumpStart(duration));
        yield return movement.JumpTo(to, distance / duration, 0.4f);
        animationController.SetIdle(true);
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
