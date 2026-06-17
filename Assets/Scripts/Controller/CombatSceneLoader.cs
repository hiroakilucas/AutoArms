using System;
using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("Perfil do Player2 para o CombatSimulator. Se nulo, usa o sistema de AttackSequencer original.")]
    [SerializeField] private PlayerProfile player2Profile;

    [Header("Simulator")]
    [Tooltip("Quando true e player2Profile estiver atribuído, usa CombatSimulator em vez de AttackSequencer.")]
    [SerializeField] private bool useSimulator = true;

    private const string Player2ProfileFallbackPath =
        "Assets/ScriptableObjects/PlayerProfiles/Medieval Warrior Girl.asset";

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
        // Deity: +50% de tamanho. Aplicado aqui (antes do EntryFall) pra já valer na queda.
        float p1ScaleMult = profile.HasSkill("Deity") ? 1.5f : 1f;
        player1Obj.transform.localScale = Vector3.one * 0.3f * p1ScaleMult;

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
        player1Combat.skills         = profile.skills != null ? new List<SkillData>(profile.skills) : new List<SkillData>();
        player1Combat.defender       = player2Combat;
        player1Combat.defenderAnimationController = player2Anim;

        player2Combat.defender  = player1Combat;
        player2Combat.defenderAnimationController = player1Anim;

        int p1MaxHealth = ApplySkillStats(player1Combat, profile.maxHealth);
        var health1 = player1Obj.AddComponent<HealthSystem>();
        health1.Initialize(p1MaxHealth);

        // useSimulator path computes all of Player2's HP off player2Profile.maxHealth (see
        // CombatSimulator.BuildState) — health2 must start from the same number, or every
        // HealthChanged event's delta is computed against the wrong baseline and damage
        // appears to not apply correctly.
        if (useSimulator && player2Profile == null)
            player2Profile = LoadPlayer2ProfileFallback();
        int p2MaxHealth = (useSimulator && player2Profile != null) ? player2Profile.maxHealth : player2MaxHealth;

        // Deity: +50% de tamanho, mesma lógica do Player1 acima — Player2 já vem pré-colocado
        // na cena com sua própria escala configurada no editor, então aqui é multiplicador
        // sobre o valor atual, não um valor fixo.
        if (useSimulator && player2Profile != null && player2Profile.HasSkill("Deity"))
            player2Object.transform.localScale *= 1.5f;

        var health2 = player2Object.GetComponent<HealthSystem>() ?? player2Object.AddComponent<HealthSystem>();
        health2.Initialize(p2MaxHealth);

        var combatHUD = gameObject.AddComponent<CombatHUD>();
        combatHUD.Initialize(health1, health2);

        if (loadout != null && handler != null)
        {
            var p1WeaponHUD = gameObject.AddComponent<WeaponHUD>();
            p1WeaponHUD.Initialize(loadout, handler, true, combatHUD.CanvasTransform);
        }

        var p2Loadout = player2Object.GetComponent<PlayerLoadout>();
        var p2Handler = player2Object.GetComponent<WeaponHandler>();

        // Mirrors player1's loadout assignment above. Sem isso, Player2 fica com o valor
        // hardcoded no PlayerLoadout da cena em vez do weaponLoadout do seu próprio
        // PlayerProfile — não tinha efeito enquanto todos os profiles compartilhavam o
        // mesmo asset, mas passa a divergir agora que cada profile tem seu próprio loadout.
        if (p2Loadout != null && player2Profile != null)
            p2Loadout.loadout = player2Profile.weaponLoadout;

        if (p2Loadout != null && p2Handler != null)
        {
            var p2WeaponHUD = gameObject.AddComponent<WeaponHUD>();
            p2WeaponHUD.Initialize(p2Loadout, p2Handler, false, combatHUD.CanvasTransform);
        }

        List<CombatEvent> events = null;

        if (useSimulator && player2Profile != null)
        {
            // Pré-calcula a luta inteira ANTES do EntryFall (não depois, como antes) — só
            // assim dá pra saber qual arma a Deity começa empunhando (Player1StartingWeapon/
            // Player2StartingWeapon) a tempo de equipá-la visualmente antes da queda, em vez
            // de depois. Não depende de nada que só existe após o EntryFall (transform/
            // spawnPosition) — só lê os PlayerProfile.
            attackSequencer.player1Profile = profile;

            var simulator = new CombatSimulator();
            events = simulator.Simulate(profile, player2Profile);

            Debug.Log(CombatLogFormatter.Format(profile.profileName, player2Profile.profileName, events));

            // Deity: já cai em cena com uma arma na mão, sem disparar nenhuma animação de
            // pickup (EquipSpecific é silencioso) — ver comentário em
            // CombatSimulator.Player1StartingWeapon/Player2StartingWeapon.
            if (handler != null && simulator.Player1StartingWeapon != null)
                handler.EquipSpecific(simulator.Player1StartingWeapon);
            if (p2Handler != null && simulator.Player2StartingWeapon != null)
                p2Handler.EquipSpecific(simulator.Player2StartingWeapon);
        }
        else
        {
            // Original coroutine-based system.
            attackSequencer.player1        = player1Combat;
            attackSequencer.player1Profile = profile;
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

        if (events != null)
        {
            // AttackSequencer stays idle (player1 never assigned → WaitUntil never resolves);
            // player1Profile já foi setado acima.
            var combatPlayer = gameObject.AddComponent<CombatPlayer>();
            combatPlayer.p1Combat  = player1Combat;
            combatPlayer.p2Combat  = player2Combat;
            combatPlayer.sequencer = attackSequencer;
            combatPlayer.PlayCombat(events);

            combatHUD.AddSpeedControls(combatPlayer);
        }
    }

    // Editor-only convenience: if player2Profile wasn't wired in the Inspector, fetch
    // the Medieval Warrior Girl profile directly by path so the simulator (and its
    // pre-combat log) still runs during testing. In a build this has no effect — assign
    // player2Profile in the Inspector for the shipped scene.
    private static PlayerProfile LoadPlayer2ProfileFallback()
    {
#if UNITY_EDITOR
        var fallback = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerProfile>(Player2ProfileFallbackPath);
        if (fallback == null)
            Debug.LogError($"[CombatSceneLoader] player2Profile não atribuído e fallback não encontrado em {Player2ProfileFallbackPath}");
        return fallback;
#else
        Debug.LogError("[CombatSceneLoader] player2Profile não atribuído no Inspector.");
        return null;
#endif
    }

    private static int ApplySkillStats(PlayerCombat combat, int baseMaxHealth)
    {
        int hp = baseMaxHealth;

        // Percentuais de HP/STR/AGI/SPD são somados num percentual líquido por status e
        // aplicados uma única vez no final (ver nota em "Skills que modificam stats" no
        // CLAUDE.md) — evita que múltiplas skills no mesmo status arredondem em cascata
        // (ex: ×1.5 depois ×0.75 arredondando a cada passo dava resultado diferente de somar
        // os percentuais primeiro: 9 str com Herculean+Immortal dava 10 em vez dos 11
        // esperados de 9×(1+0.5-0.25)).
        // evasionPct é multiplicativo sobre o evasion já somado por outras skills (Untouchable,
        // Ballet Shoes) — aplicado depois delas, no fim da função.
        float hpPct = 0f, strPct = 0f, agiPct = 0f, spdPct = 0f, evasionPct = 0f;

        if (combat.HasSkill("Vitality"))
        {
            // +18 flat já aplicado permanentemente em profile.maxHealth na escolha
            // (CombatResultPanel.ApplyBonus) — aqui só acumula o +50%.
            hpPct += 0.5f;
            combat.LogSkillCheck("Vitality", true, "hp% += 50%");
        }

        if (combat.HasSkill("Herculean Strength"))
        {
            // +3 flat já foi aplicado permanentemente em profile.str no momento da escolha
            // (CombatResultPanel.ApplyBonus) — aqui só acumula o +50%.
            strPct += 0.5f;
            combat.LogSkillCheck("Herculean Strength", true, "str% += 50%");
        }

        if (combat.HasSkill("Feline Agility"))
        {
            agiPct += 0.5f;
            combat.LogSkillCheck("Feline Agility", true, "agility% += 50%");
        }

        if (combat.HasSkill("Lightning Bolt"))
        {
            // +3 flat já aplicado permanentemente em profile.speed na escolha
            // (CombatResultPanel.ApplyBonus) — aqui só acumula o +50%. Antes afetava
            // runSpeedMultiplier (velocidade de animação de correr); agora afeta o atributo
            // speed real (ações extra no Speed System), mesmo padrão de Herculean/Feline.
            spdPct += 0.5f;
            combat.LogSkillCheck("Lightning Bolt", true, "speed% += 50%");
        }

        if (combat.HasSkill("Reconnaissance"))
        {
            // +5 flat já aplicado permanentemente em profile.speed na escolha
            // (CombatResultPanel.ApplyBonus) — aqui acumula o +150% de SPD, -200 iniciativa
            // (não entra no percentual líquido — flat puro, igual First Strike/Monk) e +50%
            // de dano crítico (critDamageBonus, somado ao critDamageMultiplier da arma).
            spdPct += 1.5f;
            combat.initiative -= 200;
            combat.critDamageBonus += 0.5f;
            combat.LogSkillCheck("Reconnaissance", true,
                $"speed% += 150%, initiative → {combat.initiative}, critDamageBonus → {combat.critDamageBonus:P0}");
        }

        if (combat.HasSkill("Immortal"))
        {
            hpPct  += 2.5f;
            strPct -= 0.25f;
            agiPct -= 0.25f;
            spdPct -= 0.25f;
            combat.LogSkillCheck("Immortal", true, "hp% += 250%, str/agi/speed% -= 25%");
        }

        if (combat.HasSkill("Bodybuilder"))
        {
            strPct += 0.5f;
            combat.LogSkillCheck("Bodybuilder", true, "str% += 50%");
        }

        if (combat.HasSkill("Deity"))
        {
            // +100% HP, +100% STR, -100% AGI, -90% SPD (não -100%: ver CombatSimulator, mesmo
            // motivo — speed fixo em 0 travava o player sem chance de ação própria/pegar
            // arma), -100% evasion ("Dexterity" — sem resistência a ser atingido), -200
            // initiative, +40% reversal (cancela o hit do atacante antes de conectar).
            hpPct      += 1.0f;
            strPct     += 1.0f;
            agiPct     -= 1.0f;
            spdPct     -= 0.90f;
            evasionPct -= 1.0f;
            combat.reversal   += 0.40f;
            combat.initiative -= 200;
            combat.LogSkillCheck("Deity", true,
                $"hp/str% += 100%, agi% -= 100%, spd% -= 90%, evasion% -= 100%, reversal → {combat.reversal:P0}, initiative → {combat.initiative}");
        }

        if (hpPct != 0f || strPct != 0f || agiPct != 0f || spdPct != 0f)
        {
            int prevHp = hp, prevStr = combat.str, prevAgi = combat.agility, prevSpd = combat.speed;
            hp             = Mathf.RoundToInt(hp * (1f + hpPct));
            combat.str     = Mathf.RoundToInt(combat.str * (1f + strPct));
            combat.agility = Mathf.RoundToInt(combat.agility * (1f + agiPct));
            combat.speed   = Mathf.RoundToInt(combat.speed * (1f + spdPct));
            combat.LogSkillCheck("StatPercent", true,
                $"hp {prevHp}→{hp}, str {prevStr}→{combat.str}, agi {prevAgi}→{combat.agility}, speed {prevSpd}→{combat.speed}");
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

        // Aplicado por último, depois de Untouchable/Ballet Shoes já terem somado evasion —
        // garante que Deity zere o total mesmo que outra skill tenha adicionado evasion antes.
        if (evasionPct != 0f)
        {
            float prevEvasion = combat.evasion;
            combat.evasion = Mathf.Max(0f, combat.evasion * (1f + evasionPct));
            combat.LogSkillCheck("EvasionPercent", true, $"evasion {prevEvasion:P0} → {combat.evasion:P0}");
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
