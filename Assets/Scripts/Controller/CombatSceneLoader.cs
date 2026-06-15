using System;
using System.Collections;
using UnityEngine;

public class CombatSceneLoader : MonoBehaviour
{
    [Header("References")]
    public AttackSequencer attackSequencer;
    [SerializeField] private GameObject player2Object;
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;

    [Header("Player 2")]
    [Tooltip("Vida maxima do Player2 (Medieval Warrior Girl). Ajustar conforme o perfil do personagem.")]
    [SerializeField] private int player2MaxHealth = 50;

    void Start()
    {
        StartCoroutine(Initialize());
    }

    private IEnumerator Initialize()
    {
        var profile = selectedProfileHolder.currentProfile;
        if (profile == null)
        {
            Debug.LogError("[CombatSceneLoader] No PlayerProfile selected.");
            yield break;
        }

        var player2Combat = player2Object.GetComponent<PlayerCombat>();
        var player2Anim   = player2Object.GetComponent<AnimationController>();
        if (player2Combat == null || player2Anim == null)
        {
            Debug.LogError("[CombatSceneLoader] Player2 is missing PlayerCombat or AnimationController.");
            yield break;
        }

        GameObject player1Obj = Instantiate(profile.characterPrefab);
        player1Obj.name = "Player1";
        player1Obj.transform.position   = profile.startPos;
        player1Obj.transform.localScale = Vector3.one * 0.3f;

        var player1Combat = player1Obj.GetComponent<PlayerCombat>();
        var player1Anim   = player1Obj.GetComponent<AnimationController>();
        if (player1Combat == null || player1Anim == null)
        {
            Debug.LogError("[CombatSceneLoader] Player1 prefab is missing PlayerCombat or AnimationController.");
            yield break;
        }

        var loadout = player1Obj.GetComponent<PlayerLoadout>();
        if (loadout != null) loadout.loadout = profile.weaponLoadout;

        var handler = player1Obj.GetComponent<WeaponHandler>();
        if (handler != null) handler.loadout = loadout;

        player1Combat.settings       = profile.attackSettings;
        player1Combat.isPlayer1      = true;
        player1Combat.str            = profile.str;
        player1Combat.agility        = profile.agility;
        player1Combat.speed          = profile.speed;
        player1Combat.armor          = profile.armor;
        player1Combat.evasion        = profile.evasion;
        player1Combat.accuracy       = profile.accuracy;
        player1Combat.initiative     = profile.initiative;
        player1Combat.reversal       = profile.reversal;
        player1Combat.counter        = profile.counter;
        player1Combat.criticalChance = profile.criticalChance;
        player1Combat.hitSpeed       = profile.hitSpeed;
        player1Combat.defender       = player2Combat;
        player1Combat.defenderAnimationController = player2Anim;

        player2Combat.defender  = player1Combat;
        player2Combat.defenderAnimationController = player1Anim;

        int p1MaxHealth = ApplySkillStats(player1Combat, profile.maxHealth);
        var health1 = player1Obj.AddComponent<HealthSystem>();
        health1.Initialize(p1MaxHealth);

        var health2 = player2Object.GetComponent<HealthSystem>() ?? player2Object.AddComponent<HealthSystem>();
        health2.Initialize(player2MaxHealth);

        var combatHUD = gameObject.AddComponent<CombatHUD>();
        combatHUD.Initialize(health1, health2);

        if (loadout != null && handler != null)
        {
            var p1WeaponHUD = gameObject.AddComponent<WeaponHUD>();
            p1WeaponHUD.Initialize(loadout, handler, true, combatHUD.CanvasTransform);
        }

        var p2Loadout = player2Object.GetComponent<PlayerLoadout>();
        var p2Handler = player2Object.GetComponent<WeaponHandler>();
        if (p2Loadout != null && p2Handler != null)
        {
            var p2WeaponHUD = gameObject.AddComponent<WeaponHUD>();
            p2WeaponHUD.Initialize(p2Loadout, p2Handler, false, combatHUD.CanvasTransform);
        }

        // Aguarda um frame para PlayerCombat.Start() rodar e definir spawnPosition
        yield return null;

        Vector3 p1Land = player1Obj.transform.position;
        Vector3 p2Land = player2Object.transform.position;

        player1Obj.transform.position    = new Vector3(p1Land.x, p1Land.y + 12f, p1Land.z);
        player2Object.transform.position = new Vector3(p2Land.x, p2Land.y + 12f, p2Land.z);

        bool p1Done = false, p2Done = false;
        StartCoroutine(EntryFall(player1Obj,    p1Land, () => p1Done = true));
        StartCoroutine(EntryFall(player2Object, p2Land, () => p2Done = true));
        yield return new WaitUntil(() => p1Done && p2Done);

        attackSequencer.player1        = player1Combat;
        attackSequencer.player1Profile = profile;
    }

    private static int ApplySkillStats(PlayerCombat combat, int baseMaxHealth)
    {
        int hp = baseMaxHealth;

        if (combat.HasSkill("Vitality"))
        {
            hp += 50;
            combat.LogSkillCheck("Vitality", true, $"maxHealth {baseMaxHealth} → {hp}");
        }

        if (combat.HasSkill("Herculean Strength"))
        {
            int ps = combat.str; int pa = combat.agility;
            combat.str     += 15;
            combat.agility -= 4;
            combat.LogSkillCheck("Herculean Strength", true, $"str {ps}→{combat.str}, agi {pa}→{combat.agility}");
        }

        if (combat.HasSkill("Feline Agility"))
        {
            int prev = combat.agility;
            combat.agility = Mathf.RoundToInt(combat.agility * 1.5f);
            combat.LogSkillCheck("Feline Agility", true, $"agility {prev} → {combat.agility}");
        }

        if (combat.HasSkill("Lightning Bolt"))
        {
            combat.runSpeedMultiplier *= 1.5f;
            combat.LogSkillCheck("Lightning Bolt", true, $"runSpeedMultiplier × 1.5 = {combat.runSpeedMultiplier:F2}");
        }

        if (combat.HasSkill("Immortal"))
        {
            int prev = hp;
            hp += 100;
            combat.runSpeedMultiplier *= 0.5f;
            combat.LogSkillCheck("Immortal", true, $"maxHealth {prev}→{hp}, speed ×0.5");
        }

        if (combat.HasSkill("Armour"))
        {
            combat.armor += 0.30f;
            combat.LogSkillCheck("Armour", true, $"armor → {combat.armor:P0}");
        }

        if (combat.HasSkill("Extra Thick Skin"))
        {
            combat.armor += 0.50f;
            combat.LogSkillCheck("Extra Thick Skin", true, $"armor → {combat.armor:P0}");
        }

        if (combat.HasSkill("Untouchable"))
        {
            combat.evasion += 0.25f;
            combat.LogSkillCheck("Untouchable", true, $"evasion → {combat.evasion:P0}");
        }

        if (combat.HasSkill("Bodybuilder"))
        {
            int prev = combat.str;
            combat.str = Mathf.RoundToInt(combat.str * 1.5f);
            combat.LogSkillCheck("Bodybuilder", true, $"str {prev} → {combat.str}");
        }

        if (combat.HasSkill("Relentless"))
        {
            combat.comboChanceBonus += 0.15f;
            combat.LogSkillCheck("Relentless", true, $"comboChanceBonus → {combat.comboChanceBonus:P0}");
        }

        if (combat.HasSkill("Lead Skeleton"))
        {
            combat.leadSkeleton = true;
            combat.LogSkillCheck("Lead Skeleton", true, "heavy damage reduced by 15%");
        }

        if (combat.HasSkill("Ballet Shoes"))
        {
            combat.evasion        += 0.10f;
            combat.firstHitAvoided = true;
            combat.LogSkillCheck("Ballet Shoes", true, $"evasion +10% → {combat.evasion:P0}, first hit auto-avoided");
        }

        if (combat.HasSkill("First Strike"))
        {
            combat.initiative += 200;
            combat.LogSkillCheck("First Strike", true, $"initiative → {combat.initiative}");
        }

        if (combat.HasSkill("Counter Attack"))
        {
            combat.counter += 0.40f;
            combat.LogSkillCheck("Counter Attack", true, $"counter → {combat.counter:P0}");
        }

        if (combat.HasSkill("Monk"))
        {
            combat.counter    += 0.40f;
            combat.initiative -= 200;
            combat.hitSpeed    = 0f;
            combat.LogSkillCheck("Monk", true, $"counter +40%, initiative −200, hitSpeed = 0");
        }

        return hp;
    }

    private IEnumerator EntryFall(GameObject obj, Vector3 landPos, Action onLand)
    {
        float velocityY = 0f;
        const float gravity = 28f;

        while (obj.transform.position.y > landPos.y)
        {
            velocityY -= gravity * Time.deltaTime;
            float newY = Mathf.Max(landPos.y, obj.transform.position.y + velocityY * Time.deltaTime);
            obj.transform.position = new Vector3(obj.transform.position.x, newY, obj.transform.position.z);
            yield return null;
        }

        obj.transform.position = landPos;

        // Squash de impacto
        Vector3 baseScale = obj.transform.localScale;
        Vector3 squashed  = new Vector3(baseScale.x * 1.4f, baseScale.y * 0.55f, baseScale.z);
        obj.transform.localScale = squashed;

        float elapsed = 0f;
        while (elapsed < 0.12f)
        {
            obj.transform.localScale = Vector3.Lerp(squashed, baseScale, elapsed / 0.12f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        obj.transform.localScale = baseScale;

        onLand();
    }
}
