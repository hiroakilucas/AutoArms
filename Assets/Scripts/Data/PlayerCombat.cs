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

    private static readonly List<GameObject> fallenWeapons = new List<GameObject>();

    public static void CleanupFallenWeapons()
    {
        foreach (var w in fallenWeapons)
            if (w != null) Destroy(w);
        fallenWeapons.Clear();
    }

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
        spawnPosition = RandomSpawnPosition();
        transform.position = spawnPosition;
        animationController.SetIdle(true);
    }

    public IEnumerator AttackRoutine()
    {
        // Se desarmado ao início do turno: 40% de chance de pegar a próxima arma.
        // Sucesso → ataca com a arma normalmente.
        // Falha   → soca desarmado e passa a vez (sem re-equip ao final).
        if (weaponHandler.CurrentWeapon == null)
        {
            if (Random.value < 0.40f)
            {
                weaponHandler.EquipRandom();
                if (weaponHandler.CurrentWeapon != null)
                    yield return animationController.PlayCatchWeapon(0.6f);
            }
        }

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
            WeaponType.Dagger  => 0.60f,
            WeaponType.Fast    => 0.60f,
            WeaponType.Sword   => 0.60f,
            WeaponType.Heavy   => 0.60f,
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

    // Impact (skill futura): adiciona +0.15f a este valor permanentemente.
    private float DisarmChance()
    {
        if (weaponHandler.CurrentWeapon == null) return 0f;
        return weaponHandler.currentType switch
        {
            WeaponType.Dagger => 0.20f,
            WeaponType.Fast   => 0.15f,
            WeaponType.Sword  => 0.10f,
            WeaponType.Heavy  => 0.05f,
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
    private int StrBonus() => Mathf.Max(0, (str - 10) / 2);

    // Dano base por tipo de arma (ataque normal em HitRoutine).
    // Heavy recebe bônus de STR; outros tipos têm dano fixo por tipo.
    private int CalcDamage()
    {
        if (weaponHandler.CurrentWeapon == null)
            return 2 + StrBonus();
        return weaponHandler.currentType switch
        {
            WeaponType.Heavy  => 10 + StrBonus(),
            WeaponType.Sword  => 5,
            WeaponType.Dagger => 3,
            _                 => weaponHandler.CurrentWeaponData?.damage > 0
                                 ? weaponHandler.CurrentWeaponData.damage : 3
        };
    }

    // Dano do arremesso usa o mesmo valor base do tipo (sem bônus de STR).
    private static int ThrowDamage(WeaponData data)
    {
        if (data == null) return 2;
        return data.type switch
        {
            WeaponType.Heavy  => 10,
            WeaponType.Sword  => 5,
            WeaponType.Dagger => 3,
            _                 => data.damage > 0 ? data.damage : 3
        };
    }

    // Posição de ataque: imediatamente fora do alcance da arma, na direção do defensor.
    private Vector2 AttackPosition()
    {
        if (defender == null) return spawnPosition;
        Vector2 defPos = (Vector2)defender.transform.position;
        float reach = weaponHandler.CurrentWeapon == null ? 0.8f :
            weaponHandler.currentType switch
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
        yield return HitRoutine(isCombo: true);
    }

    // Slash → (knockback + Hurt em paralelo) → dano → (se não combo) checar Desarmar.
    // Quando desarmado: usa "Slashing" (soco) com dano calculado por STR.
    private IEnumerator HitRoutine(bool applyKnockback = true, bool isCombo = false)
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

            if (weaponHandler.CurrentWeapon != null && Random.value < 0.15f)
                StartCoroutine(DropWeapon(this, isDisarm: false));
            if (defender.weaponHandler.CurrentWeapon != null && Random.value < 0.10f)
                StartCoroutine(DropWeapon(defender, isDisarm: false));

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

        if (!isCombo && defender != null && !defender.IsDead
            && defender.weaponHandler.CurrentWeapon != null
            && Random.value < DisarmChance())
        {
            StartCoroutine(DropWeapon(defender));
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

        // Captura posição e escala mundiais ANTES de destruir o objeto de arma.
        // lossyScale reflete o tamanho visual real (personagem com scale 0.3 aplicado).
        GameObject inHandWeapon = weaponHandler.CurrentWeapon;
        Vector3 launchPos = inHandWeapon != null
            ? inHandWeapon.transform.position
            : weaponHandler.handBone != null
                ? weaponHandler.handBone.position
                : transform.position + Vector3.up * 0.5f;
        Vector3 projectileScale = inHandWeapon != null
            ? inHandWeapon.transform.lossyScale
            : (weaponHandler.handBone != null ? weaponHandler.handBone.lossyScale : Vector3.one) * (weaponData?.scale ?? 1f);

        if (isThrown)
            weaponHandler.Unequip();
        else
            weaponHandler.UnequipPermanent();

        // targetPos calculado antes do flying weapon para usar no ângulo de rotação.
        Vector3 targetPos = defender != null
            ? defender.transform.position + Vector3.up * 0.3f
            : transform.position + (isPlayer1 ? Vector3.right : Vector3.left) * 5f;

        // Orienta o sprite na direção do voo (evita ponta para baixo/diagonal da rotação in-hand).
        Vector3 flightDir = (targetPos - launchPos).normalized;
        float flightAngle = Mathf.Atan2(flightDir.y, flightDir.x) * Mathf.Rad2Deg;

        // Projétil pertence exclusivamente a este atacante. O weaponHandler do defensor nunca é tocado.
        // Usar Mathf.Abs no scale: personagens espelhados (Player 2) têm lossyScale.x negativo,
        // o que causaria flip do sprite + ângulo invertido. O Atan2 já cuida da direção correta.
        var flyingWeapon = new GameObject("FlyingWeapon");
        flyingWeapon.transform.position = launchPos;
        flyingWeapon.transform.localScale = new Vector3(
            Mathf.Abs(projectileScale.x),
            Mathf.Abs(projectileScale.y),
            Mathf.Abs(projectileScale.z));
        flyingWeapon.transform.rotation = Quaternion.Euler(0, 0, flightAngle);
        var sr = flyingWeapon.AddComponent<SpriteRenderer>();
        sr.sprite = weaponData?.inHandSprite;
        sr.sortingLayerName = "Weapons";
        sr.sortingOrder = 10;

        animator.SetTrigger("Throwing");

        // Somente Thrown rotaciona; todos os outros voam com rotação fixa.
        bool rotate = weaponData?.type == WeaponType.Thrown;
        yield return FlyWeapon(flyingWeapon.transform, launchPos, targetPos, 0.45f, rotate);
        Destroy(flyingWeapon);  // sempre destruído antes de resolver hit/miss

        if (defender != null && Random.value < 0.80f)
        {
            Vector2 pushDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.Knockback(pushDir, settings.knockbackDistance, settings.hurtDuration));
            yield return defenderAnimationController.PlayHurt(settings.hurtDuration);
            int damage = ThrowDamage(weaponData);
            defender.GetComponent<HealthSystem>()?.TakeDamage(damage);
            Vector3 popupPos = defender.transform.position + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.Spawn(popupPos, damage, false);
        }
        else
        {
            Vector3 missPos = (defender != null ? defender.transform.position : transform.position)
                + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.SpawnMiss(missPos);
            if (defender != null)
            {
                Vector2 dodgeDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
                defender.StartCoroutine(defender.DodgeLeap(dodgeDir, settings.knockbackDistance));
            }
            yield return new WaitForSeconds(settings.hurtDuration);
        }

    }

    private IEnumerator DropWeapon(PlayerCombat target, bool isDisarm = true)
    {
        var data = target.weaponHandler.CurrentWeaponData;
        if (data?.inHandSprite == null) yield break;

        var inHand = target.weaponHandler.CurrentWeapon;
        Vector3 startPos  = inHand != null ? inHand.transform.position : target.transform.position + Vector3.up * 0.5f;
        Vector3 worldScale = inHand != null ? inHand.transform.lossyScale : Vector3.one * data.scale;

        target.weaponHandler.UnequipPermanent();

        Vector3 popupPos = target.transform.position + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
        if (isDisarm)
            DamagePopup.SpawnDisarm(popupPos);
        else
            DamagePopup.SpawnDrop(popupPos);

        var fallen = new GameObject("FallenWeapon");
        fallen.transform.position   = startPos;
        fallen.transform.localScale = new Vector3(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z));
        var sr = fallen.AddComponent<SpriteRenderer>();
        sr.sprite           = data.inHandSprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder     = 0;

        float groundY   = target.transform.position.y - 1.5f;
        float velocityY = 0f;
        float t         = 0f;
        float theta0    = Random.Range(60f, 100f); // amplitude inicial do pêndulo
        const float omega = 10f;                   // frequência angular (rad/s)
        const float gamma = 0.8f;                  // amortecimento

        fallen.transform.rotation = Quaternion.Euler(0f, 0f, theta0);

        while (fallen.transform.position.y > groundY)
        {
            velocityY -= 9.8f * Time.deltaTime;
            fallen.transform.position += new Vector3(0f, velocityY * Time.deltaTime, 0f);
            t += Time.deltaTime;
            float angle = theta0 * Mathf.Exp(-gamma * t) * Mathf.Cos(omega * t);
            fallen.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        var p = fallen.transform.position;
        fallen.transform.position = new Vector3(p.x, groundY, p.z);
        fallenWeapons.Add(fallen);
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
