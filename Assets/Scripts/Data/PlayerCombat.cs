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
    public int speed = 10;
    public float armor = 0f;
    public float evasion = 0f;
    public float accuracy = 0f;
    public int initiative = 0;
    public float reversal = 0f;
    public float counter = 0f;
    public float criticalChance = 0f;
    public float hitSpeed = 1f;
    public float runSpeedMultiplier = 1f;
    public float comboChanceBonus = 0f;

    [HideInInspector] public bool leadSkeleton   = false;
    [HideInInspector] public bool firstHitAvoided = false;

    [Header("Skills — Teste")]
    public List<SkillData> skills = new List<SkillData>();

    [Header("Debug")]
    public PlayerProfile debugProfile;
    public bool logSkills = true;

    [ContextMenu("Reset to Level 1")]
    private void ResetToLevel1()
    {
        if (debugProfile == null)
        {
            Debug.LogWarning($"[Debug] debugProfile não atribuído em {name}");
            return;
        }
        debugProfile.level            = 1;
        debugProfile.xpCurrent        = 0;
        debugProfile.battlesRemaining = 6;
        debugProfile.xpRequired       = XpSystem.XpRequired(1);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(debugProfile);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
        Debug.Log($"[Debug] {debugProfile.profileName} resetado para Level 1");
    }

    public bool IsDead => GetComponent<HealthSystem>()?.IsDead ?? false;

    private float RuntimeRunSpeed => settings != null ? settings.runSpeed * runSpeedMultiplier : 35f;

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

        // Throw verifica ANTES do melee. Se triggar: arremessa do lugar, sem Run+JumpBack.
        if (defender != null && !defender.IsDead && weaponHandler.CurrentWeapon != null
            && Random.value < ThrowChance())
        {
            yield return ThrowRoutine();
            animationController.SetIdle(true); // espelha ReturnToSpawn: sai do estado Throwing antes do próximo turno
        }
        else
        {
            yield return StrikeRoutine();

            while (defender != null && !defender.IsDead && Random.value < ComboChance())
                yield return ComboStrikeRoutine();

            yield return ReturnToSpawn();
        }

        RestoreDefaultLayers();
    }

    private float ThrowChance()
    {
        if (weaponHandler.CurrentWeapon == null) return 0f;
        return weaponHandler.currentType switch
        {
            WeaponType.Thrown  => 1.00f,
            WeaponType.Dagger  => 0.15f,
            WeaponType.Fast    => 0.15f,
            WeaponType.Sword   => 0.15f,
            WeaponType.Heavy   => 0.10f,
            _                  => 0f
        };
    }

    private float ComboChance()
    {
        float base_ = weaponHandler.CurrentWeapon == null ? 0.10f : weaponHandler.currentType switch
        {
            WeaponType.Fast   => 0.40f,
            WeaponType.Dagger => 0.35f,
            WeaponType.Sword  => 0.25f,
            WeaponType.Heavy  => 0.10f,
            _                 => 0.25f
        };
        float agiBonus    = Mathf.Max(0, agility - 3) * 0.015f;
        float weaponCombo = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.comboBonus : UnarmedStats.ComboBonus;
        float total = base_ + agiBonus + comboChanceBonus + weaponCombo;
        Debug.Log($"[Combo] Base: {base_*100:F0}% + AGI bonus: {agiBonus*100:F1}% + weapon: {weaponCombo*100:F0}% = Total: {total*100:F1}% (AGI: {agility})");
        return total;
    }

    // criticalChance: base do profile + bônus de skills (ex: Fierce Brute +0.10f) + bônus da arma.
    private float CritChance()
    {
        float baseChance = weaponHandler.currentType switch
        {
            WeaponType.Dagger => 0.08f,
            WeaponType.Sword  => 0.05f,
            WeaponType.Heavy  => 0.03f,
            _                 => 0.05f
        };
        float weaponBonus = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.critChanceBonus : UnarmedStats.CritChanceBonus;
        return baseChance + weaponBonus + criticalChance;
    }

    // counter: base por arma do defensor + bônus de skills (Shield +0.45f, Counter Attack +0.10f, Monk +0.40f) + bônus da arma.
    private float BlockChance()
    {
        if (defender == null) return 0f;
        float weaponBonus = defender.weaponHandler.CurrentWeapon == null ? 0f :
            defender.weaponHandler.currentType switch
            {
                WeaponType.Block  => 0.50f,
                WeaponType.Slow   => 0.05f,
                WeaponType.Dagger => 0.15f,
                WeaponType.Sword  => 0.15f,
                WeaponType.Heavy  => 0.15f,
                _                 => 0f
            };
        float weaponBlockBonus = defender.weaponHandler.CurrentWeaponData != null
            ? defender.weaponHandler.CurrentWeaponData.blockBonus : UnarmedStats.BlockBonus;
        return weaponBonus + defender.counter + weaponBlockBonus;
    }

    // Impact (skill futura): adiciona +0.15f a este valor permanentemente.
    private float DisarmChance()
    {
        float baseChance = weaponHandler.CurrentWeapon == null ? 0f : weaponHandler.currentType switch
        {
            WeaponType.Dagger => 0.20f,
            WeaponType.Fast   => 0.15f,
            WeaponType.Sword  => 0.10f,
            WeaponType.Heavy  => 0.05f,
            _                 => 0f
        };
        float weaponDisarmBonus = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.disarmBonus : UnarmedStats.DisarmBonus;
        return baseChance + weaponDisarmBonus;
    }

    // evasion: base por tipo de arma do defensor + bônus de agilidade + evasion do defensor + bônus da arma.
    // Skills: Sixth Sense +0.10f, Untouchable +0.30f, Ballet Shoes +0.10f.
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
        float agiBonus       = Mathf.Max(0, defender.agility - 3) * 0.02f;
        float weaponEvasion  = defender.weaponHandler.CurrentWeaponData != null
            ? defender.weaponHandler.CurrentWeaponData.evasionBonus : UnarmedStats.EvasionBonus;
        float total = Mathf.Min(0.60f, baseChance + agiBonus + defender.evasion + weaponEvasion);
        Debug.Log($"[Dodge] Base: {baseChance*100:F0}% + AGI bonus: {agiBonus*100:F0}% + weapon: {weaponEvasion*100:F0}% = Total: {total*100:F0}% (AGI: {defender.agility})");
        return total;
    }

    // Dano base da arma (sem STR/crítico/armadura) — Heavy/Sword/Dagger têm variação aleatória estilo My Brute.
    private int WeaponBaseDamage()
    {
        if (weaponHandler.CurrentWeapon == null)
            return UnarmedStats.Damage;
        return weaponHandler.currentType switch
        {
            WeaponType.Heavy  => Random.Range(30, 50),
            WeaponType.Sword  => Random.Range(10, 18),
            WeaponType.Dagger => Random.Range(7, 13),
            _                 => weaponHandler.CurrentWeaponData?.damage > 0
                                 ? weaponHandler.CurrentWeaponData.damage : 3
        };
    }

    private float CritDamageMultiplier() => weaponHandler.CurrentWeaponData != null
        ? weaponHandler.CurrentWeaponData.critDamageMultiplier : UnarmedStats.CritDamageMultiplier;

    // Fórmula multiplicativa do My Brute: weaponBaseDamage × (1 + str/10) × (critMultiplier se crítico).
    // Lead Skeleton e armadura são aplicados depois, em HitRoutine.
    private float CalcDamage(bool isCrit)
    {
        int   weaponBaseDamage = WeaponBaseDamage();
        float critMult         = isCrit ? CritDamageMultiplier() : 1f;
        return weaponBaseDamage * (1f + str / 10f) * critMult;
    }

    // Dano do arremesso usa os mesmos ranges sem bônus de STR.
    private static int ThrowDamage(WeaponData data)
    {
        if (data == null) return 2;
        return data.type switch
        {
            WeaponType.Heavy  => Random.Range(30, 50),
            WeaponType.Sword  => Random.Range(10, 18),
            WeaponType.Dagger => Random.Range(7, 13),
            _                 => data.damage > 0 ? data.damage : 3
        };
    }

    // Posição de ataque: imediatamente fora do alcance da arma, na direção do defensor.
    // weaponData.reach soma-se à distância base do tipo de arma.
    private Vector2 AttackPosition()
    {
        if (defender == null) return spawnPosition;
        Vector2 defPos = (Vector2)defender.transform.position;
        float baseReach = weaponHandler.CurrentWeapon == null ? 0.8f :
            weaponHandler.currentType switch
            {
                WeaponType.Dagger => 1.5f,
                WeaponType.Heavy  => 2.8f,
                _                 => 2.0f
            };
        float reach = baseReach + (weaponHandler.CurrentWeaponData?.reach ?? 0);
        Vector2 dir = (defPos - (Vector2)transform.position).normalized;
        return defPos - dir * reach;
    }

    private IEnumerator StrikeRoutine()
    {
        yield return animationController.PlayRun(AttackPosition(), RuntimeRunSpeed, movement);
        yield return HitRoutine();
    }

    // Any State → Slashing (CanTransitionToSelf=1) no controller permite re-entrar no Slashing após o run.
    private IEnumerator ComboStrikeRoutine()
    {
        yield return new WaitForSeconds(settings.slashingToJumpDelay);
        Vector2 attackPos = AttackPosition();
        if (Vector2.Distance(transform.position, attackPos) > 0.3f)
            yield return animationController.PlayRun(attackPos, RuntimeRunSpeed, movement);
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

        // Ballet Shoes: primeiro golpe da luta automaticamente esquivado
        if (defender != null && defender.firstHitAvoided)
        {
            defender.firstHitAvoided = false;
            defender.LogSkillCheck("Ballet Shoes", true, "first hit of fight automatically avoided");
            Vector2 balletDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.DodgeLeap(balletDir, settings.knockbackDistance));
            Vector3 balletPos = defender.transform.position + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.SpawnDodge(balletPos);
            yield return new WaitForSeconds(settings.slashingDuration * 0.5f);
            yield break;
        }

        // Monk: guarda em vez de atacar (hitSpeed = 0)
        if (hitSpeed <= 0f)
        {
            LogSkillCheck("Monk", true, "guarding instead of attacking (hitSpeed = 0)");
            yield return new WaitForSeconds(settings.slashingDuration);
            yield break;
        }

        float weaponHitSpeed = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.hitSpeed : UnarmedStats.HitSpeed;
        float slashSpeed = hitSpeed * weaponHitSpeed;
        if (slashSpeed != 1f) animationController.SetSpeed(slashSpeed);

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
            if (slashSpeed != 1f) animationController.SetSpeed(1f);
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

            if (slashSpeed != 1f) animationController.SetSpeed(1f);
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

        bool  isCrit = Random.value < CritChance();
        float dmg    = CalcDamage(isCrit);

        // Lead Skeleton: -15% dano de armas Heavy
        if (defender != null && defender.leadSkeleton && weaponHandler.currentType == WeaponType.Heavy)
        {
            dmg *= 0.85f;
            defender.LogSkillCheck("Lead Skeleton", true, "heavy damage ×0.85");
        }

        // Fórmula multiplicativa do My Brute: finalDamage = Max(1, Round(dmg × (1 - armor)))
        float defenderArmor = defender != null ? defender.armor : 0f;
        int   finalDamage   = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - defenderArmor)));
        if (defenderArmor > 0f)
            Debug.Log($"[Armor] {defender.name} armor {defenderArmor:P0}: damage {Mathf.RoundToInt(dmg)} → {finalDamage}");

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

        if (slashSpeed != 1f) animationController.SetSpeed(1f);
        yield return new WaitForSeconds(settings.slashingDuration * 0.5f);
    }

    // Arremessa a arma em linha reta até o defensor.
    // Thrown: nunca removida do loadout. Outras: removidas permanentemente após o arremesso.
    // Após o resultado (hit/miss): 40% pega a próxima arma imediatamente, 60% fica desarmado.
    private IEnumerator ThrowRoutine()
    {
        var weaponData = weaponHandler.CurrentWeaponData;
        bool isThrown = weaponData?.type == WeaponType.Thrown;

        // Capture world position and scale BEFORE destroying the weapon object on Unequip.
        // lossyScale reflects real visual size (character has scale ~0.3 applied).
        GameObject inHandWeapon = weaponHandler.CurrentWeapon;
        // Prefer handBone.position for the exact hand Y; fall back to weapon object position.
        Vector3 launchPos = weaponHandler.handBone != null
            ? weaponHandler.handBone.position
            : (inHandWeapon != null
                ? inHandWeapon.transform.position
                : transform.position + Vector3.up * 0.5f);
        Vector3 projectileScale = inHandWeapon != null
            ? inHandWeapon.transform.lossyScale
            : (weaponHandler.handBone != null ? weaponHandler.handBone.lossyScale : Vector3.one) * (weaponData?.scale ?? 1f);

        if (isThrown)
            weaponHandler.Unequip();
        else
            weaponHandler.UnequipPermanent();

        // Fly straight from handBone to defender's actual position.
        // For WeaponType.Thrown a slight upward arc is added inside FlyWeapon.
        Vector3 targetPos = defender != null
            ? defender.transform.position
            : transform.position + (isPlayer1 ? Vector3.right : Vector3.left) * 5f;

        Debug.Log($"[Throw] From: {launchPos} | To: {targetPos} | Direction: {(targetPos - launchPos).normalized}");

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

        // Thrown rotates and gets a slight arc; all others fly in a straight horizontal line.
        bool  rotate = weaponData?.type == WeaponType.Thrown;
        float arc    = isThrown ? 0.5f : 0f;
        yield return FlyWeapon(flyingWeapon.transform, launchPos, targetPos, 0.45f, rotate, arc);
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

        // 40% de chance de pegar uma arma aleatória imediatamente após o arremesso.
        if (Random.value < 0.40f)
            weaponHandler.EquipRandom();
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

    private IEnumerator FlyWeapon(Transform obj, Vector3 from, Vector3 to, float duration, bool rotate, float arc = 0f)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t   = elapsed / duration;
            Vector3 p = Vector3.Lerp(from, to, t);
            if (arc > 0f)
                p.y += arc * Mathf.Sin(t * Mathf.PI);
            obj.position = p;
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
        yield return animationController.PlayJumpStart(settings.jumpStartDuration);
        spawnPosition = RandomSpawnPosition();
        yield return movement.JumpTo(spawnPosition, RuntimeRunSpeed, settings.jumpHeight);
        animationController.SetIdle(true);
    }

    private Vector2 RandomSpawnPosition()
    {
        float x = isPlayer1 ? Random.Range(-7.25f, -4.79f) : Random.Range(4.79f, 7.25f);
        float y = Random.Range(-3.90f, -0.81f);
        return new Vector2(x, y);
    }

    // --- Skill queries ---

    public bool HasSkill(string skillName)
    {
        if (skills == null) return false;
        foreach (var s in skills)
            if (s != null && s.skillName == skillName) return true;
        return false;
    }

    public SkillData GetSkill(string skillName)
    {
        if (skills == null) return null;
        foreach (var s in skills)
            if (s != null && s.skillName == skillName) return s;
        return null;
    }

    // Call inside each chance method when a skill modifies the roll.
    // Example: LogSkillCheck("Relentless", triggered, "combo chance: 40% → 55%")
    public void LogSkillCheck(string skillName, bool triggered, string detail = "")
    {
        if (!logSkills) return;
        string status = triggered ? "triggered" : "not triggered";
        string owner  = isPlayer1 ? "Player1" : "Player2";
        Debug.Log(string.IsNullOrEmpty(detail)
            ? $"[Skill] {skillName} checked on {owner} → {status}"
            : $"[Skill] {skillName} checked on {owner} → {status} ({detail})");
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
