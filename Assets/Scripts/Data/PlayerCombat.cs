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
    public int str = 10;

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
        // 60% case: still unarmed from a previous throw — equip next weapon normally.
        if (weaponHandler.CurrentWeapon == null)
            weaponHandler.EquipNext();

        SetAttackerLayers();
        yield return null;

        yield return animationController.PlayIdle(settings.idleDuration);
        yield return StrikeRoutine();

        while (defender != null && !defender.IsDead && Random.value < ComboChance())
            yield return ComboStrikeRoutine();

        yield return ReturnToSpawn();

        if (defender != null && !defender.IsDead && weaponHandler.CurrentWeapon != null
            && Random.value < ThrowChance())
            yield return ThrowRoutine();

        RestoreDefaultLayers();
    }

    private float ThrowChance()
    {
        if (weaponHandler.CurrentWeapon == null) return 0f;
        return weaponHandler.currentType switch
        {
            WeaponType.Thrown  => 1.00f,
            WeaponType.Dagger  => 0.25f,
            WeaponType.Fast    => 0.15f,
            WeaponType.Sword   => 0.10f,
            WeaponType.Heavy   => 0.05f,
            _                  => 0f
        };
    }

    private float ComboChance()
    {
        if (weaponHandler.CurrentWeapon == null) return 0.10f;
        return weaponHandler.currentType switch
        {
            WeaponType.Fast   => 0.40f,
            WeaponType.Dagger => 0.35f,
            WeaponType.Sword  => 0.25f,
            WeaponType.Heavy  => 0.10f,
            _                 => 0.25f
        };
    }

    // Fierce Brute (skill futura): adiciona +0.10f a este valor permanentemente.
    private float CritChance() => weaponHandler.currentType switch
    {
        WeaponType.Dagger => 0.08f,
        WeaponType.Sword  => 0.05f,
        WeaponType.Heavy  => 0.03f,
        _                 => 0.05f
    };

    // Shield (skill futura): adiciona +0.45f a este valor permanentemente.
    private float BlockChance()
    {
        if (defender == null || defender.weaponHandler.CurrentWeapon == null) return 0f;
        return defender.weaponHandler.currentType switch
        {
            WeaponType.Block  => 0.50f,
            WeaponType.Slow   => 0.05f,
            WeaponType.Dagger => 0.15f,
            WeaponType.Sword  => 0.15f,
            WeaponType.Heavy  => 0.15f,
            _                 => 0f
        };
    }

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

    // Iron Fist (skill futura): aumenta dano desarmado.
    // Heavy: dano base + bônus de STR. Outras armas: dano fixo. Desarmado: 1 + bônus de STR.
    private int StrBonus() => Mathf.Max(0, (str - 10) / 2);

    private int CalcDamage()
    {
        if (weaponHandler.CurrentWeapon == null)
            return 1 + StrBonus();
        if (weaponHandler.currentType == WeaponType.Heavy)
            return (weaponHandler.CurrentWeaponData?.damage ?? 0) + StrBonus();
        return weaponHandler.CurrentWeaponData?.damage ?? 0;
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

    private IEnumerator StrikeRoutine()
    {
        yield return animationController.PlayRun(AttackPosition(), settings.runSpeed, movement);
        yield return HitRoutine();
    }

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
    // Quando desarmado: usa "Slashing" (soco) com dano calculado por STR.
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

        if (defender != null && Random.value < BlockChance())
        {
            Vector2 blockDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.Knockback(blockDir, settings.knockbackDistance * 0.5f, settings.hurtDuration));
            defender.StartCoroutine(defenderAnimationController.PlayBlock(0.36666667f));

            Vector3 blockPos = defender.transform.position
                + Vector3.up   * 1.5f
                + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.SpawnBlock(blockPos);
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
        int  baseDamage  = CalcDamage();
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

    // Arremessa a arma em linha reta até o defensor.
    // Thrown: nunca removida do loadout. Outras: removidas permanentemente após o arremesso.
    // Após o resultado (hit/miss): 40% pega a próxima arma imediatamente, 60% fica desarmado.
    private IEnumerator ThrowRoutine()
    {
        var weaponData = weaponHandler.CurrentWeaponData;
        bool isThrown = weaponData?.type == WeaponType.Thrown;

        Vector3 launchPos = weaponHandler.handBone != null
            ? weaponHandler.handBone.position
            : transform.position + Vector3.up * 0.5f;

        if (isThrown)
            weaponHandler.Unequip();
        else
            weaponHandler.UnequipPermanent();

        var flyingWeapon = new GameObject("FlyingWeapon");
        flyingWeapon.transform.position = launchPos;
        // Dagger mantém escala original (1,1,1); outras armas usam o scale do WeaponData.
        if (weaponData?.type != WeaponType.Dagger)
            flyingWeapon.transform.localScale = Vector3.one * (weaponData?.scale ?? 1f);
        var sr = flyingWeapon.AddComponent<SpriteRenderer>();
        sr.sprite = weaponData?.inHandSprite;
        sr.sortingLayerName = "Weapons";
        sr.sortingOrder = 10;

        animator.SetTrigger("Throwing");

        Vector3 targetPos = defender != null
            ? defender.transform.position + Vector3.up * 0.3f
            : transform.position + (isPlayer1 ? Vector3.right : Vector3.left) * 5f;

        // Sword e Heavy voam sem rotação; todos os outros tipos rotacionam.
        bool rotate = weaponData?.type != WeaponType.Sword && weaponData?.type != WeaponType.Heavy;
        yield return FlyWeapon(flyingWeapon.transform, launchPos, targetPos, 0.45f, rotate);
        Destroy(flyingWeapon);

        if (defender != null && Random.value < 0.80f)
        {
            Vector2 pushDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.Knockback(pushDir, settings.knockbackDistance, settings.hurtDuration));
            yield return defenderAnimationController.PlayHurt(settings.hurtDuration);
            int damage = weaponData?.damage ?? 0;
            defender.GetComponent<HealthSystem>()?.TakeDamage(damage);
            Vector3 popupPos = defender.transform.position + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.Spawn(popupPos, damage, false);
        }
        else
        {
            Vector3 missPos = (defender != null ? defender.transform.position : transform.position)
                + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.SpawnMiss(missPos);
            yield return new WaitForSeconds(settings.hurtDuration);
        }

        // 40% chance de pegar a próxima arma do loadout imediatamente.
        if (Random.value < 0.40f)
            weaponHandler.EquipNext();
    }

    private IEnumerator FlyWeapon(Transform obj, Vector3 from, Vector3 to, float duration, bool rotate)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            obj.position = Vector3.Lerp(from, to, elapsed / duration);
            if (rotate)
                obj.Rotate(0, 0, 540f * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        obj.position = to;
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
