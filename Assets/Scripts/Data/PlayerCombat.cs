using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Spriter2UnityDX;
using TMPro;

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
    public float blockBonus = 0f;
    public float reversalAfterBlock = 0f;
    public float criticalChance = 0f;
    public float critDamageBonus = 0f;
    public float hitSpeed = 1f;
    public float runSpeedMultiplier = 1f;
    public float comboChanceBonus = 0f;
    public float disarmChanceBonus = 0f;

    [HideInInspector] public bool leadSkeleton   = false;
    [HideInInspector] public bool firstHitAvoided = false;
    [HideInInspector] public bool martialArts     = false;
    [HideInInspector] public bool weaponsMaster   = false;
    [HideInInspector] public bool hasShield       = false;

    [Header("Skills — Teste")]
    public List<SkillData> skills = new List<SkillData>();

    [Header("Debug")]
    public PlayerProfile debugProfile;

    [ContextMenu("Reset to Level 1")]
    private void ResetToLevel1()
    {
        if (debugProfile == null) return;
        debugProfile.level            = 1;
        debugProfile.xpCurrent        = 0;
        debugProfile.battlesRemaining = 6;
        debugProfile.xpRequired       = XpSystem.XpRequired(1);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(debugProfile);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

    public bool IsDead => GetComponent<HealthSystem>()?.IsDead ?? false;

    private float RuntimeRunSpeed => settings != null ? settings.runSpeed * runSpeedMultiplier : 35f;

    private Animator animator;
    private List<SpriteRenderer> bodyRenderers;
    private string defaultSortingLayer;
    private Vector2 spawnPosition;
    private SpriteRenderer faceRenderer;
    private Sprite[]       faceSprites;
    private GameObject stunLabel;
    private Coroutine   stunHurtRoutine;

    // Chaining: chamado quando este personagem é estunado (CombatEventType.Stunned) — label
    // fixo acima da cabeça (texto por enquanto; o usuário vai trocar por sprite depois) +
    // Hurt em loop até o próprio StunSkip consumir a ação estunada (ver HideStunLabel).
    public void ShowStunLabel()
    {
        if (stunLabel == null)
        {
            stunLabel = new GameObject("StunLabel");
            stunLabel.transform.SetParent(transform);
            stunLabel.transform.localPosition = Vector3.up * 2.2f;
            // Cancela o sinal de localScale.x do pai (mecanismo de flip de direção, ver
            // StealWeapon) pra o texto nunca renderizar espelhado/de trás pra frente.
            stunLabel.transform.localScale = new Vector3(Mathf.Sign(transform.localScale.x), 1f, 1f);

            var label = stunLabel.AddComponent<TextMeshPro>();
            label.alignment      = TextAlignmentOptions.Center;
            label.sortingLayerID = SortingLayer.NameToID("Characters");
            label.sortingOrder   = 50;
            label.fontStyle      = FontStyles.Bold;
            label.text           = "ATORDOADO!";
            label.fontSize       = 3.5f;
            label.color          = new Color(1f, 0.95f, 0.2f);
        }
        if (stunHurtRoutine == null)
            stunHurtRoutine = StartCoroutine(StunHurtLoop());
    }

    // Encerra o loop de Hurt e remove a label — chamado pelo StunSkip, no início do turno que
    // consome a ação estunada (ver CombatSimulator.SimulateTurn).
    public void HideStunLabel()
    {
        if (stunHurtRoutine != null)
        {
            StopCoroutine(stunHurtRoutine);
            stunHurtRoutine = null;
        }
        if (stunLabel != null)
        {
            Destroy(stunLabel);
            stunLabel = null;
        }
        animationController.SetIdle(true);
    }

    // Hurt não tem nenhum bool de "hold" no Animator Controller (volta pra Idle sozinho via
    // tempo de saída do próprio clip, diferente de Throwing/Slashing) — então, pra manter a
    // pose de Hurt "presa" durante toda a duração do stun, o trigger é refeito em loop em vez
    // de uma chamada única. Intervalo de 0.3s (não settings.hurtDuration, ~0.12s) — bem mais
    // espaçado que o gap natural entre hits de um combo de verdade, pra não repetir o trigger
    // rápido demais e dar a mesma cintilação entre poses já corrigida no bug do Throwing
    // ("parece que está com parkinson").
    private IEnumerator StunHurtLoop()
    {
        while (true)
        {
            animator.SetTrigger("Hurt");
            yield return new WaitForSeconds(0.3f);
        }
    }

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

        // "Face 01" é o nome do GameObject gerado pelo Spriter2UnityDX nos 3 personagens, com um
        // TextureController (Sprites[0..2] = Face 01/02/03) e um SpriteRenderer próprio. Usado
        // pelo "piscar" da skill Thief (StealWeapon abaixo). Pega o SpriteRenderer e o array de
        // sprites direto, em vez de setar TextureController.DisplayedSprite — esse componente só
        // aplica a troca em Update() quando o Animator não está em transição (IsTransitioning()),
        // o que pode atrasar/perder a troca durante uma sequência rápida; setar o SpriteRenderer
        // direto garante a troca no mesmo frame, sem depender desse gate.
        foreach (var tc in GetComponentsInChildren<TextureController>(true))
        {
            if (tc.gameObject.name != "Face 01") continue;
            faceRenderer = tc.GetComponent<SpriteRenderer>();
            faceSprites  = tc.Sprites;
            break;
        }
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
        // Monk (hitSpeed = 0) guarda em vez de atacar — arremessar também é um ataque, então
        // também não acontece pra ele (mesma checagem de CombatSimulator.SimulateTurn).
        if (hitSpeed > 0f && defender != null && !defender.IsDead && weaponHandler.CurrentWeapon != null
            && Random.value < ThrowChance())
        {
            yield return ThrowRoutine();
            animationController.SetIdle(true); // espelha ReturnToSpawn: sai do estado Throwing antes do próximo turno
        }
        else
        {
            yield return StrikeRoutine();

            int comboCount = 0;
            while (defender != null && !defender.IsDead && Random.value < ComboChance(comboCount))
            {
                yield return ComboStrikeRoutine();
                comboCount++;
            }

            yield return ReturnToSpawn();
        }

        RestoreDefaultLayers();
    }

    // Soma os valores-base de cada tag presente na arma (WeaponData.types é uma lista) — mesma
    // tabela/regra de CombatSimulator.TagSum, ver CLAUDE.md.
    private static float TagSum(WeaponData data, float sharp = 0f, float fast = 0f, float heavy = 0f, float thrown = 0f)
    {
        if (data == null) return 0f;
        float total = 0f;
        if (data.HasType(WeaponType.Sharp))  total += sharp;
        if (data.HasType(WeaponType.Fast))   total += fast;
        if (data.HasType(WeaponType.Heavy))  total += heavy;
        if (data.HasType(WeaponType.Thrown)) total += thrown;
        return total;
    }

    private float ThrowChance()
    {
        if (weaponHandler.CurrentWeapon == null) return 0f;
        return TagSum(weaponHandler.CurrentWeaponData, sharp: 0.15f, heavy: 0.10f, thrown: 1.00f);
    }

    // comboCount = quantos hits extra de combo já aconteceram neste turno (0 = checagem do 1º hit extra).
    // Decaimento ×0.5 por hit consecutivo, aplicado depois do clamp — ver CombatSimulator.ComboChance.
    private float ComboChance(int comboCount = 0)
    {
        float base_ = weaponHandler.CurrentWeapon == null
            ? 0.05f
            : TagSum(weaponHandler.CurrentWeaponData, sharp: 0.12f, fast: 0.03f, heavy: 0.04f);
        float agiBonus    = Mathf.Max(0, agility - 3) * 0.008f;
        float weaponCombo = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.comboBonus : UnarmedStats.ComboBonus;
        float total = Mathf.Clamp(base_ + agiBonus + comboChanceBonus + weaponCombo, 0f, 0.35f);
        return total * Mathf.Pow(0.5f, comboCount);
    }

    // criticalChance: base do profile + bônus de skills (ex: Fierce Brute +0.10f) + bônus da arma.
    private float CritChance()
    {
        float baseChance = TagSum(weaponHandler.CurrentWeaponData, sharp: 0.05f, fast: 0.03f, heavy: 0.03f);
        float weaponBonus = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.critChanceBonus : UnarmedStats.CritChanceBonus;
        return baseChance + weaponBonus + criticalChance;
    }

    // Legado/código morto enquanto useSimulator=true (ver CombatSimulator.BlockChance, que lê
    // defender.blockBonus — Shield/Counter Attack somam ali, não em defender.counter, que aqui
    // virou a mecânica própria de Counter/Reversal). Não atualizado pra refletir blockBonus.
    private float BlockChance()
    {
        if (defender == null) return 0f;
        float weaponBonus = defender.weaponHandler.CurrentWeapon == null
            ? 0f
            : TagSum(defender.weaponHandler.CurrentWeaponData, sharp: 0.15f, heavy: 0.15f);
        float weaponBlockBonus = defender.weaponHandler.CurrentWeaponData != null
            ? defender.weaponHandler.CurrentWeaponData.blockBonus : UnarmedStats.BlockBonus;
        return weaponBonus + defender.counter + weaponBlockBonus;
    }

    // Impact (skill futura): adiciona +0.15f a este valor permanentemente. Shield desarma por
    // uma chance própria e independente (ver CombatSimulator.ShieldDisarmChance), não por aqui.
    private float DisarmChance()
    {
        float baseChance = weaponHandler.CurrentWeapon == null
            ? 0f
            : TagSum(weaponHandler.CurrentWeaponData, sharp: 0.10f, fast: 0.10f, heavy: 0.05f);
        float weaponDisarmBonus = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.disarmBonus : UnarmedStats.DisarmBonus;
        return baseChance + weaponDisarmBonus + disarmChanceBonus;
    }

    // evasion: base por tipo de arma do defensor + bônus de agilidade + evasion do defensor + bônus da arma.
    // Skills: Sixth Sense +0.10f, Untouchable +0.30f, Ballet Shoes +0.10f.
    private float DodgeChance()
    {
        if (defender == null) return 0f;
        float baseChance = TagSum(defender.weaponHandler.CurrentWeaponData, sharp: 0.10f, fast: 0.05f, heavy: 0.05f);
        float agiBonus       = Mathf.Max(0, defender.agility - 3) * 0.02f;
        float weaponEvasion  = defender.weaponHandler.CurrentWeaponData != null
            ? defender.weaponHandler.CurrentWeaponData.evasionBonus : UnarmedStats.EvasionBonus;
        // Bodybuilder: +10% evasion ("dexterity"), só enquanto empunha arma Heavy.
        float bodybuilderBonus = (WeaponData.HasType(defender.weaponHandler.CurrentWeaponData, WeaponType.Heavy) && defender.HasSkill("Bodybuilder")) ? 0.10f : 0f;
        return Mathf.Min(0.60f, baseChance + agiBonus + defender.evasion + weaponEvasion + bodybuilderBonus);
    }

    // weaponData.damage tem prioridade absoluta — cada WeaponData tem seu próprio campo
    // configurável, então não há mais ranges hardcoded por tag (Random.Range(7,13)/(10,18)/
    // (30,50) eram valores padrão do protótipo, de antes de cada arma ter o próprio Damage).
    private static int RollWeaponDamage(WeaponData data) => data.damage > 0 ? data.damage : 3;

    // Dano base da arma (sem STR/crítico/armadura).
    private int WeaponBaseDamage()
    {
        if (weaponHandler.CurrentWeapon == null)
            return martialArts ? UnarmedStats.Damage * 2 : UnarmedStats.Damage;
        return RollWeaponDamage(weaponHandler.CurrentWeaponData);
    }

    private float CritDamageMultiplier()
    {
        float baseMult = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.critDamageMultiplier : UnarmedStats.CritDamageMultiplier;
        return baseMult + critDamageBonus;
    }

    // Fórmula do My Brute original: STR soma direto no dano base da arma (flat, não percentual)
    // — (weaponBaseDamage + str) × critMultiplier × sharpMult. Lead Skeleton e armadura são
    // aplicados depois, em HitRoutine. Era weaponBaseDamage × (1 + str/10) (percentual,
    // divergia do original) — redefinida pelo usuário.
    private float CalcDamage(bool isCrit)
    {
        int   weaponBaseDamage = WeaponBaseDamage();
        float critMult         = isCrit ? CritDamageMultiplier() : 1f;
        bool  isSharp          = WeaponData.IsSharp(weaponHandler.CurrentWeaponData);
        float sharpMult        = (weaponsMaster && isSharp) ? 1.5f : 1f;
        return (weaponBaseDamage + str) * critMult * sharpMult;
    }

    // Dano do arremesso soma STR igual ao golpe normal (weaponBaseDamage + str) — era só
    // weaponBaseDamage, redefinida pelo usuário.
    private static int ThrowDamage(WeaponData data, int str)
    {
        int weaponDamage = data == null ? 2 : RollWeaponDamage(data);
        return weaponDamage + str;
    }

    // Posição de ataque: imediatamente fora do alcance da arma, na direção do defensor.
    // weaponData.reach soma-se à distância base do tipo de arma. Heavy e Sharp/default não são
    // somados (não faz sentido físico somar dois alcances de categoria inteiros) — Heavy tem
    // prioridade, depois Fast desconta (recria exatamente o 1.5 da antiga Dagger = Sharp+Fast).
    private Vector2 AttackPosition()
    {
        if (defender == null) return spawnPosition;
        Vector2 defPos = (Vector2)defender.transform.position;
        float baseReach;
        if (weaponHandler.CurrentWeapon == null)
        {
            baseReach = 0.8f;
        }
        else
        {
            baseReach = WeaponData.HasType(weaponHandler.CurrentWeaponData, WeaponType.Heavy) ? 2.8f : 2.0f;
            if (WeaponData.HasType(weaponHandler.CurrentWeaponData, WeaponType.Fast)) baseReach -= 0.5f;
        }
        float reach = baseReach + (weaponHandler.CurrentWeaponData?.reach ?? 0);
        Vector2 dir = (defPos - (Vector2)transform.position).normalized;
        return defPos - dir * reach;
    }

    private IEnumerator StrikeRoutine()
    {
        // Monk (hitSpeed = 0) guarda em vez de atacar — não corre até o adversário, mesma
        // checagem de CombatSimulator.SimulateTurn (HitRoutine já saía cedo sem golpear, mas
        // ainda corria até o adversário antes disso).
        if (hitSpeed > 0f)
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
        // Prioridade Heavy > Fast > default — uma arma só tem uma animação de swing, ainda que
        // tenha múltiplas tags (ex: Heavy|Blunt entra em SlashingHeavy; Sharp|Fast em SlashingDagger).
        string slashTrigger =
            WeaponData.HasType(weaponHandler.CurrentWeaponData, WeaponType.Heavy) ? "SlashingHeavy" :
            WeaponData.HasType(weaponHandler.CurrentWeaponData, WeaponType.Fast)  ? "SlashingDagger" :
            "Slashing";

        // Monk: guarda em vez de atacar (hitSpeed = 0) — checado ANTES do Ballet Shoes abaixo,
        // mesma ordem de CombatSimulator.SimulateHit. Um hit que nunca aconteceu não deveria
        // gastar o "esquiva o 1º golpe" do defensor nem fazer ele saltar pra esquivar de um
        // ataque que o Monk nunca desferiu.
        if (hitSpeed <= 0f)
        {
            LogSkillCheck("Monk", true, "guarding instead of attacking (hitSpeed = 0)");
            yield return new WaitForSeconds(settings.slashingDuration);
            yield break;
        }

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

        float weaponHitSpeed = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.hitSpeed : UnarmedStats.HitSpeed;
        // Bodybuilder: +40% hit speed, só enquanto empunha arma Heavy.
        float bodybuilderSpeedMult = (WeaponData.HasType(weaponHandler.CurrentWeaponData, WeaponType.Heavy) && HasSkill("Bodybuilder")) ? 1.4f : 1f;
        float slashSpeed = hitSpeed * weaponHitSpeed * bodybuilderSpeedMult;
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

        // Lead Skeleton: -15% dano de arma blunt (Heavy)
        if (defender != null && defender.leadSkeleton && WeaponData.IsBlunt(weaponHandler.CurrentWeaponData))
        {
            dmg *= 0.85f;
            defender.LogSkillCheck("Lead Skeleton", true, "blunt damage ×0.85");
        }

        // Fórmula multiplicativa do My Brute: finalDamage = Max(1, Round(dmg × (1 - armor)))
        float defenderArmor = defender != null ? defender.armor : 0f;
        int   finalDamage   = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - defenderArmor)));

        defender?.GetComponent<HealthSystem>()?.TakeDamage(finalDamage);

        if (defender != null)
        {
            Vector3 popupPos = defender.transform.position
                + Vector3.up   * 1.5f
                + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.Spawn(popupPos, finalDamage, isCrit);
        }

        // Iron Head: logo após sofrer o dano (qualquer hit, incluindo combo), +40% chance do
        // defensor derrubar a arma do atacante (inverso do Disarm abaixo).
        if (defender != null && defender.HasSkill("Iron Head") && weaponHandler.CurrentWeapon != null
            && Random.value < 0.40f)
        {
            StartCoroutine(DropWeapon(this, isDisarm: false));
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
        bool isThrown = WeaponData.HasType(weaponData, WeaponType.Thrown);

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
        bool  rotate = isThrown;
        float arc    = isThrown ? 0.5f : 0f;
        yield return FlyWeapon(flyingWeapon.transform, launchPos, targetPos, 0.45f, rotate, arc);
        Destroy(flyingWeapon);  // sempre destruído antes de resolver hit/miss

        if (defender != null && Random.value < 0.80f)
        {
            Vector2 pushDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.Knockback(pushDir, settings.knockbackDistance, settings.hurtDuration));
            yield return defenderAnimationController.PlayHurt(settings.hurtDuration);
            int damage = ThrowDamage(weaponData, str);
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

    // Public/static so CombatPlayer (CombatSimulator replay) can reuse the same pendulum-fall
    // visual for Disarm/WeaponDrop events — mirrors how FlyWeapon was made public for ThrowWeapon.
    // Doesn't read any instance state, only target's, so it doesn't need a PlayerCombat instance to run on.
    public static IEnumerator DropWeapon(PlayerCombat target, bool isDisarm = true)
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

    // Mesma queda em pêndulo amortecido de DropWeapon, mas pro escudo da skill Shield —
    // CurrentShieldData/CurrentShield (não current/CurrentWeaponData) e RemoveShield() em vez
    // de UnequipPermanent(), já que o escudo nunca esteve no WeaponLoadout. isDisarm escolhe o
    // popup: true (ao tomar hit, ShieldDisarm) → "DISARM!" laranja; false (ao bloquear com
    // sucesso, ShieldDrop) → "DROP!" laranja, mesma distinção de DropWeapon.
    public static IEnumerator DropShield(PlayerCombat target, bool isDisarm)
    {
        var data = target.weaponHandler.CurrentShieldData;
        if (data?.inHandSprite == null) yield break;

        var inHand = target.weaponHandler.CurrentShield;
        Vector3 startPos   = inHand != null ? inHand.transform.position : target.transform.position + Vector3.up * 0.5f;
        Vector3 worldScale = inHand != null ? inHand.transform.lossyScale : Vector3.one * data.scale;

        target.weaponHandler.RemoveShield();

        Vector3 popupPos = target.transform.position + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
        if (isDisarm)
            DamagePopup.SpawnDisarm(popupPos);
        else
            DamagePopup.SpawnDrop(popupPos);

        var fallen = new GameObject("FallenShield");
        fallen.transform.position   = startPos;
        fallen.transform.localScale = new Vector3(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z));
        var sr = fallen.AddComponent<SpriteRenderer>();
        sr.sprite           = data.inHandSprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder     = 0;

        float groundY   = target.transform.position.y - 1.5f;
        float velocityY = 0f;
        float t         = 0f;
        float theta0    = Random.Range(60f, 100f);
        const float omega = 10f;
        const float gamma = 0.8f;

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

        var p2 = fallen.transform.position;
        fallen.transform.position = new Vector3(p2.x, groundY, p2.z);
        fallenWeapons.Add(fallen);
    }

    // Visual da skill Thief: o ladrão pula nas costas do adversário (igual montar um cavalo) e
    // os dois balançam juntos 4 vezes, antes do ladrão descer já com a arma roubada na mão —
    // referência visual do My Brute original pedida pelo usuário. Não existe sprite dedicado de
    // "montar" no projeto (Spriter2UnityDX só gera Idle/Running/Slashing/etc.), então é
    // aproximado via movimento de transform puro (mesmo espírito do pêndulo de DropWeapon/
    // DropShield acima — sem novo estado de Animator).
    public static IEnumerator StealWeapon(PlayerCombat thief, PlayerCombat victim)
    {
        var data = victim.weaponHandler.CurrentWeaponData;
        if (data == null) yield break;

        // jumpHeight era uma constante fixa de 0.8 — aumentado pra parecer mais um salto de
        // verdade (pedido pelo usuário), igual ao arco usado em ReturnToSpawn/DodgeLeap.
        float jumpHeight = thief.settings != null ? thief.settings.jumpHeight : 2f;
        // Mesma velocidade de ReturnToSpawn/TurnEnd (RuntimeRunSpeed) — pedido pelo usuário pra
        // o "pêndulo" de ida e volta não ficar mais lento que o salto normal de fim de turno.
        float jumpSpeed = thief.RuntimeRunSpeed;

        Vector3 thiefStart  = thief.transform.position;
        Vector3 thiefScale  = thief.transform.localScale;
        Vector3 mountOffset = new Vector3(thief.isPlayer1 ? 0.3f : -0.3f, 0.6f, 0f);
        Vector3 mountPos    = victim.transform.position + mountOffset;

        // Sobe nas costas do adversário — promove o ladrão na sorting layer (igual a um
        // atacante normal) pra renderizar por cima de quem ele está montando.
        SetBodyLayer(thief.bodyRenderers, "Characters");
        SetBodyLayer(victim.bodyRenderers, "Characters2");
        yield return thief.movement.JumpTo(mountPos, jumpSpeed, jumpHeight);

        // Vira pra mesma direção do adversário ao chegar nas costas dele — mesmo mecanismo de
        // flip já usado no projeto (sinal de localScale.x; ver Medieval Warrior Girl na cena,
        // -0.3 pra virar pra esquerda). Restaurado mais abaixo, antes do salto de volta.
        thief.transform.localScale = new Vector3(
            Mathf.Sign(victim.transform.localScale.x) * Mathf.Abs(thiefScale.x), thiefScale.y, thiefScale.z);

        // Balançam juntos 4 vezes, pra frente e pra trás (não mais vertical) e um pouco mais
        // devagar que a primeira versão — a vítima "pisca" a cada ciclo trocando Face 01 por
        // Face 03 (Spriter2UnityDX), tudo pedido pelo usuário. Seta o SpriteRenderer.sprite
        // direto (não TextureController.DisplayedSprite) — esse componente só aplica a troca em
        // Update() quando o Animator não está em transição, o que podia atrasar ou perder a
        // troca; setar o renderer direto garante o "pisca" no mesmo frame.
        Vector3 victimBase = victim.transform.position;
        bool hasFace = victim.faceRenderer != null && victim.faceSprites != null && victim.faceSprites.Length > 2;
        const float bounceDistance = 0.18f;
        const float bounceDuration = 0.18f;
        for (int i = 0; i < 4; i++)
        {
            if (hasFace) victim.faceRenderer.sprite = victim.faceSprites[2]; // Face 03
            float t = 0f;
            while (t < bounceDuration)
            {
                float k = Mathf.Sin((t / bounceDuration) * Mathf.PI) * bounceDistance;
                thief.transform.position  = mountPos   + Vector3.right * k;
                victim.transform.position = victimBase + Vector3.right * k * 0.5f;
                t += Time.deltaTime;
                yield return null;
            }
            if (hasFace) victim.faceRenderer.sprite = victim.faceSprites[0]; // Face 01
        }
        thief.transform.position  = mountPos;
        victim.transform.position = victimBase;

        // Rouba a arma: sai do loadout do adversário e entra no do ladrão — não destrói, igual
        // ao DropWeapon, mas em vez de cair no chão ela troca de dono direto (WeaponHUD dos dois
        // lados atualiza via OnWeaponsChanged, mesmo evento de RemoveCurrentWeapon/AddWeapon).
        victim.weaponHandler.Unequip();
        victim.weaponHandler.loadout?.RemoveCurrentWeapon(data);
        thief.weaponHandler.EquipSpecific(data);
        thief.weaponHandler.loadout?.AddWeapon(data);

        DamagePopup.SpawnDisarm(victim.transform.position + Vector3.up * 1.5f);

        SetBodyLayer(thief.bodyRenderers, thief.defaultSortingLayer);
        SetBodyLayer(victim.bodyRenderers, victim.defaultSortingLayer);

        thief.transform.localScale = thiefScale; // volta a virar pra direção original antes de saltar de volta

        yield return thief.movement.JumpTo(thiefStart, jumpSpeed, jumpHeight);
    }

    // Public so CombatPlayer (CombatSimulator replay) can reuse the same projectile arc
    // for ThrowWeapon events instead of just unequipping with no flight visual.
    public IEnumerator FlyWeapon(Transform obj, Vector3 from, Vector3 to, float duration, bool rotate, float arc = 0f)
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
        Vector2 to = ClampToArena((Vector2)transform.position + pushDirection * distance);
        float duration = settings.dodgeDuration;
        StartCoroutine(animationController.PlayJumpStart(duration));
        yield return movement.JumpTo(to, distance / duration, 0.4f);
        animationController.SetIdle(true);
    }

    // Desliza o personagem na direção pushDirection ao tomar um hit.
    // Iniciado pelo atacante via StartCoroutine para rodar em paralelo com PlayHurt.
    public IEnumerator Knockback(Vector2 pushDirection, float distance, float duration)
    {
        Vector2 from = transform.position;
        Vector2 to   = ClampToArena(from + pushDirection * distance);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector2.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = to;
    }

    // Limite da janela jogável (mesmos valores de RandomSpawnPosition abaixo — área visível
    // da câmera). Combos longos com vários hits/esquivas seguidas empurravam o personagem
    // cada vez mais pra fora desse intervalo, eventualmente saindo da tela.
    private static Vector2 ClampToArena(Vector2 pos)
    {
        pos.x = Mathf.Clamp(pos.x, -7.25f, 7.25f);
        pos.y = Mathf.Clamp(pos.y, -3.90f, -0.81f);
        return pos;
    }

    private IEnumerator ReturnToSpawn()
    {
        // Mesma checagem de CombatPlayer.ExecuteEvent (caso TurnEnd): se o personagem nunca
        // saiu da própria zona de spawn neste turno (Monk guardando, hitSpeed = 0, nunca corre
        // até o adversário em StrikeRoutine), não tem por que saltar pra um ponto aleatório novo.
        if (InSpawnZone(transform.position))
        {
            animationController.SetIdle(true);
            yield break;
        }
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

    private bool InSpawnZone(Vector2 pos)
    {
        float xMin = isPlayer1 ? -7.25f : 4.79f;
        float xMax = isPlayer1 ? -4.79f : 7.25f;
        return pos.x >= xMin && pos.x <= xMax && pos.y >= -3.90f && pos.y <= -0.81f;
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

    // No-op kept for call-site compatibility (logging removed project-wide).
    public void LogSkillCheck(string skillName, bool triggered, string detail = "") { }

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
