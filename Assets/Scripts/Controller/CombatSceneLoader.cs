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

    [Header("Shield")]
    [Tooltip("WeaponData usado como visual permanente da skill Shield (Assets/Data/UI/Weapons/Shield/Shield1.asset).")]
    [SerializeField] private WeaponData shieldWeaponData;

    [Header("Piledriver")]
    [Tooltip("Prefab do efeito de explosão da skill Piledriver (Assets/Data/UI/SkillEffect/Piledriver/PiledriverExplosion.prefab, gerado por Tools > AutoArms > Generate Piledriver Effect Prefab).")]
    [SerializeField] private GameObject piledriverEffectPrefab;

    [Header("Net")]
    [Tooltip("Sprite da rede em voo (Assets/Data/UI/SkillEffect/Net/net1.png).")]
    [SerializeField] private Sprite netFlyingSprite;
    [Tooltip("Sprite da rede caída/oscilando sobre o enredado (Assets/Data/UI/SkillEffect/Net/net2.png).")]
    [SerializeField] private Sprite netLandedSprite;

    [Header("Bomb")]
    [Tooltip("Prefab da bomba (Assets/Data/UI/SkillEffect/Bomb/Bomb.prefab) — GameObject com SpriteRenderer (sprite \"bomb\") + Animator (controller Explosion_1, já criado pelo usuário). Usado tanto na fase de voo (sprite estático) quanto na explosão (2ª instância, Animator religado).")]
    [SerializeField] private GameObject bombPrefab;

    [Header("Tragic Potion")]
    [Tooltip("Sprite do frasco da poção (Assets/Data/UI/SkillEffect/TragicPotion/potion.png).")]
    [SerializeField] private Sprite tragicPotionSprite;
    [Tooltip("Sprite do efeito de cura/partículas (Assets/Data/UI/SkillEffect/TragicPotion/healing.png).")]
    [SerializeField] private Sprite tragicPotionHealSprite;

    [Header("Fast Metabolism")]
    [Tooltip("Animator Controller da folha animada (Assets/Data/UI/SkillEffect/FastMetabolism/Leaf Shield_Frame_01.controller) — usado tanto na regeneração passiva (1 instância pequena) quanto no pulso de 50% (6 instâncias orbitando). Não precisa de prefab — CombatPlayer monta o GameObject (SpriteRenderer+Animator) em runtime.")]
    [SerializeField] private RuntimeAnimatorController fastMetabolismController;

    [Header("Vampirism")]
    [Tooltip("Animator Controller do efeito de sangue (Assets/Data/UI/SkillEffect/Vampirism/1.controller, flipbook de 6 frames já em loop) — usado durante o bounce da mordida, do corpo do defensor até a boca do atacante. Não precisa de prefab — PlayerCombat.VampirismRoutine monta o GameObject (SpriteRenderer+Animator) em runtime, mesmo padrão do Fast Metabolism.")]
    [SerializeField] private RuntimeAnimatorController vampirismEffectController;

    [Header("Chef")]
    [Tooltip("Prefab da pizza (Assets/Data/UI/SkillEffect/Chef/ChefPizzaPrefab.prefab, gerado por Tools > AutoArms > Generate Chef Effect Prefab) — GameObject com SpriteRenderer (sprite \"chef\") + Animator (controller da explosão verde). Usado tanto na fase de voo (sprite estático) quanto na explosão (2ª instância, Animator religado), mesmo padrão do Bomb Prefab.")]
    [SerializeField] private GameObject chefPizzaPrefab;

    [Header("Pets")]
    // NÃO usar o prefab em "Vector Parts/<Tipo>.prefab" (rig multi-bone do Spriter) — ele não
    // tem SpriteRenderer na própria raiz, e as animações dos pets são flipbooks simples que
    // trocam o m_Sprite de um SpriteRenderer na raiz (ver Assets/Editor/PetPrefabGenerator.cs).
    // Usar os prefabs gerados por Tools > AutoArms > Generate Pet Gameplay Prefabs.
    [Tooltip("Prefab de gameplay do Javali (Assets/Data/UI/Pets/Boar/BoarPet.prefab, gerado por Tools > AutoArms > Generate Pet Gameplay Prefabs).")]
    [SerializeField] private GameObject boarPetPrefab;
    [Tooltip("Prefab de gameplay do Macaco (Assets/Data/UI/Pets/Monkey/MonkeyPet.prefab, gerado por Tools > AutoArms > Generate Pet Gameplay Prefabs).")]
    [SerializeField] private GameObject monkeyPetPrefab;
    [Tooltip("Prefab de gameplay do Rato (Assets/Data/UI/Pets/Mouse/MousePet.prefab, gerado por Tools > AutoArms > Generate Pet Gameplay Prefabs).")]
    [SerializeField] private GameObject mousePetPrefab;

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

        // Shield: item visual permanente no braço oposto (offHandBone), fora do WeaponLoadout —
        // equipado uma única vez aqui, igual ao ajuste de escala da Deity acima, não pelo ciclo
        // normal de troca de armas (handler.EquipNext/EquipRandom nunca tocam isso).
        if (handler != null && profile.HasSkill("Shield"))
            handler.EquipShield(shieldWeaponData);

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

        WeaponHUD p1WeaponHUD = null, p2WeaponHUD = null;

        if (loadout != null && handler != null)
        {
            p1WeaponHUD = gameObject.AddComponent<WeaponHUD>();
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
            p2WeaponHUD = gameObject.AddComponent<WeaponHUD>();
            p2WeaponHUD.Initialize(p2Loadout, p2Handler, false, combatHUD.CanvasTransform);
        }

        // Espelha o equip de Shield do Player1 acima.
        if (p2Handler != null && player2Profile != null && player2Profile.HasSkill("Shield"))
            p2Handler.EquipShield(shieldWeaponData);

        List<CombatEvent> events = null;

        if (useSimulator && player2Profile != null)
        {
            // Pré-calcula a luta inteira ANTES do EntryFall — não depende de nada que só existe
            // depois dele (transform/spawnPosition), só lê os PlayerProfile, e permite pintar os
            // ícones sabotados pela Spy (abaixo) já antes da queda em cena. Personagens não
            // nascem mais com arma equipada (era 40% de chance — EquipStartingWeaponIfNeeded,
            // removido a pedido do usuário; todo mundo agora começa sempre desarmado).
            attackSequencer.player1Profile = profile;

            var simulator = new CombatSimulator();
            events = simulator.Simulate(profile, player2Profile);

            Debug.Log(CombatLogFormatter.Format(profile.profileName, player2Profile.profileName, events, profile.pets, player2Profile.pets));

            // Spy: ícones das armas sabotadas (ver CombatSimulator.ApplySpySabotage) ficam
            // vermelhos no WeaponHUD de quem foi sabotado, mesmo antes de equipá-las.
            p1WeaponHUD?.SetSabotagedWeapons(simulator.Player1SabotagedWeapons);
            p2WeaponHUD?.SetSabotagedWeapons(simulator.Player2SabotagedWeapons);
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

        // Pets (Fase 3) — instanciados e soltos do céu junto dos personagens principais, só
        // quando o simulador está ativo (events != null), já que o caminho legado não tem
        // nenhuma lógica de pet. p1Pets/p2Pets ficam preenchidos antes do WaitUntil abaixo
        // resolver, prontos pra passar pro CombatPlayer.
        var p1Pets = new List<PetCombatController>();
        var p2Pets = new List<PetCombatController>();
        var petsDone = new List<bool>();

        if (events != null)
        {
            // 1 frame de respiro antes de instanciar pets — deixa o frame de
            // StartCoroutine(EntryFall) dos 2 personagens principais (acima) sozinho, sem
            // competir com Instantiate+HealthBarPet.Create de cada pet no mesmo frame
            // (investigação de stutter ao entrar na cena: SpawnPets virou IEnumerator, ver
            // abaixo, e CLAUDE.md/Pets — causa real era esse bloco síncrono concentrado
            // exatamente no frame em que a queda do céu dos personagens principais começa a
            // ser visível, agravado pela quantidade de pets no profile).
            yield return null;
            StartCoroutine(SpawnPets(profile.pets, profile.level, p1Land, isPlayer1: true, p1Pets, petsDone));
            StartCoroutine(SpawnPets(player2Profile.pets, player2Profile.level, p2Land, isPlayer1: false, p2Pets, petsDone));
        }

        yield return new WaitUntil(() => p1Done && p2Done && petsDone.TrueForAll(d => d));

        if (events != null)
        {
            // AttackSequencer stays idle (player1 never assigned → WaitUntil never resolves);
            // player1Profile já foi setado acima.
            var combatPlayer = gameObject.AddComponent<CombatPlayer>();
            combatPlayer.p1Combat    = player1Combat;
            combatPlayer.p2Combat    = player2Combat;
            combatPlayer.p1Pets      = p1Pets;
            combatPlayer.p2Pets      = p2Pets;
            combatPlayer.sequencer   = attackSequencer;
            combatPlayer.p1WeaponHUD = p1WeaponHUD;
            combatPlayer.p2WeaponHUD = p2WeaponHUD;
            combatPlayer.piledriverEffectPrefab = piledriverEffectPrefab;
            combatPlayer.netFlyingSprite = netFlyingSprite;
            combatPlayer.netLandedSprite = netLandedSprite;
            combatPlayer.bombPrefab = bombPrefab;
            combatPlayer.tragicPotionSprite = tragicPotionSprite;
            combatPlayer.tragicPotionHealSprite = tragicPotionHealSprite;
            combatPlayer.fastMetabolismController = fastMetabolismController;
            combatPlayer.vampirismEffectController = vampirismEffectController;
            combatPlayer.chefPizzaPrefab = chefPizzaPrefab;
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

        // Bodybuilder (redefinida — era strPct += 0.5f/"STR × 1.5"): +10% evasion e +40% hit
        // speed enquanto empunha Heavy, checado vivo (DodgeChance/CombatPlayer), sem estado
        // fixo aqui já que a arma equipada pode trocar durante a luta.

        if (combat.HasSkill("Armour"))
        {
            // +25% armor (flat, fora do percentual líquido) e -15% SPD (entra normalmente).
            combat.armor += 0.25f;
            spdPct       -= 0.15f;
            combat.LogSkillCheck("Armour", true, $"armor → {combat.armor:P0}, speed% -= 15%");
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

        if (combat.HasSkill("Extra Thick Skin"))
        {
            combat.armor += 0.50f;
            combat.LogSkillCheck("Extra Thick Skin", true, $"armor → {combat.armor:P0}");
        }

        if (combat.HasSkill("Toughened Skin"))
        {
            combat.armor += 0.10f;
            combat.LogSkillCheck("Toughened Skin", true, $"armor → {combat.armor:P0}");
        }

        if (combat.HasSkill("Shield"))
        {
            // +45% block rate (blockBonus, mesmo campo de Counter Attack — soma em BlockChance)
            // e +25% armor (penalidade de mobilidade do escudo equipado). hasShield habilita o
            // desarme próprio do escudo (ver CombatSimulator.ShieldDisarmChance) — se cair, os
            // dois bônus são revertidos. Visual (sprite no braço oposto) equipado fora daqui,
            // em CombatSceneLoader.Initialize.
            combat.blockBonus += 0.45f;
            combat.armor      += 0.25f;
            combat.hasShield   = true;
            combat.LogSkillCheck("Shield", true, $"blockBonus → {combat.blockBonus:P0}, armor → {combat.armor:P0}");
        }

        if (combat.HasSkill("Untouchable"))
        {
            combat.evasion += 0.30f;
            combat.LogSkillCheck("Untouchable", true, $"evasion → {combat.evasion:P0}");
        }

        if (combat.HasSkill("Relentless"))
        {
            combat.accuracy += 0.30f;
            combat.LogSkillCheck("Relentless", true, $"accuracy → {combat.accuracy:P0}");
        }

        if (combat.HasSkill("Fists of Fury"))
        {
            combat.comboChanceBonus += 0.20f;
            combat.LogSkillCheck("Fists of Fury", true, $"comboChanceBonus → {combat.comboChanceBonus:P0}");
        }

        if (combat.HasSkill("Lead Skeleton"))
        {
            // Redefinida — antes só dava -15% dano de Heavy. Agora também +15% armor, -15%
            // evasion (floor em 0 garantido pelo clamp incondicional de evasionPct abaixo).
            combat.leadSkeleton = true;
            combat.armor   += 0.15f;
            combat.evasion -= 0.15f;
            combat.LogSkillCheck("Lead Skeleton", true, $"armor → {combat.armor:P0}, evasion → {combat.evasion:P0}, blunt damage ×0.85");
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
            combat.blockBonus += 0.10f;
            combat.reversalAfterBlock += 0.90f;
            combat.LogSkillCheck("Counter Attack", true,
                $"blockBonus → {combat.blockBonus:P0}, reversalAfterBlock → {combat.reversalAfterBlock:P0}");
        }

        if (combat.HasSkill("Sixth Sense"))
        {
            combat.counter += 0.10f;
            combat.LogSkillCheck("Sixth Sense", true, $"counter → {combat.counter:P0}");
        }

        if (combat.HasSkill("Hostility"))
        {
            combat.reversal += 0.30f;
            combat.LogSkillCheck("Hostility", true, $"reversal → {combat.reversal:P0}");
        }

        if (combat.HasSkill("Monk"))
        {
            // Não guarda mais (era hitSpeed = 0f, removido — Monk ataca normalmente, igual a
            // qualquer personagem) — só o bônus de counter e o malus de iniciativa permanecem.
            combat.counter    += 0.40f;
            combat.initiative -= 200;
            combat.LogSkillCheck("Monk", true, $"counter +40%, initiative −200");
        }

        if (combat.HasSkill("Martial Arts"))
        {
            combat.martialArts = true;
            combat.LogSkillCheck("Martial Arts", true, "unarmed damage ×2");
        }

        if (combat.HasSkill("Shock"))
        {
            combat.disarmChanceBonus += 0.50f;
            combat.LogSkillCheck("Shock", true, $"disarmChanceBonus → {combat.disarmChanceBonus:P0}");
        }

        if (combat.HasSkill("Weapon Master"))
        {
            combat.weaponsMaster = true;
            combat.LogSkillCheck("Weapon Master", true, "+50% damage with sharp-tagged weapons");
        }

        if (combat.HasSkill("Sticky Hands"))
        {
            combat.stickyHands += 0.50f;
            combat.LogSkillCheck("Sticky Hands", true, $"stickyHands → {combat.stickyHands:P0}");
        }

        // Aplicado por último, depois de Untouchable/Ballet Shoes/Lead Skeleton já terem somado
        // ou subtraído evasion — garante que Deity zere o total mesmo que outra skill já tenha
        // alterado evasion antes. Incondicional (não só quando evasionPct != 0) pra também
        // garantir o floor em 0 quando só Lead Skeleton (-15% flat) deixa o total negativo.
        {
            float prevEvasion = combat.evasion;
            combat.evasion = Mathf.Max(0f, combat.evasion * (1f + evasionPct));
            if (combat.evasion != prevEvasion)
                combat.LogSkillCheck("EvasionPercent", true, $"evasion {prevEvasion:P0} → {combat.evasion:P0}");
        }

        return hp;
    }

    private GameObject PetPrefabFor(PetType type)
    {
        switch (type)
        {
            case PetType.Boar:   return boarPetPrefab;
            case PetType.Monkey: return monkeyPetPrefab;
            case PetType.Mouse:  return mousePetPrefab;
            default: return null;
        }
    }

    // Instancia um pet por entrada em petTypes, acima da câmera (mesmo padrão de spawnY + 12f
    // do EntryFall dos personagens principais), com X aleatório dentro da arena — P1 do lado
    // esquerdo (-5 a -1), P2 do lado direito (1 a 5) — e dispara o próprio EntryFall (mesma
    // coroutine, com squash de impacto). doneFlags acumula 1 bool por pet, lido pelo
    // WaitUntil em Initialize() junto dos p1Done/p2Done dos personagens principais.
    // IEnumerator (não mais void síncrono) — yield a cada pet instanciado, pra espalhar o custo
    // de Instantiate+HealthBarPet.Create (Canvas+Image+RectTransform por pet) por frame em vez
    // de empacar todos os pets de um lado no mesmo frame (escala mal com profiles que acumulam
    // vários pets — ver investigação de stutter ao entrar na cena, CLAUDE.md/Pets). Chamada via
    // StartCoroutine em Initialize() (fire-and-forget, mesmo padrão de EntryFall) — o WaitUntil
    // final já tolera conclusão assíncrona/fora de ordem via petsDone.
    private IEnumerator SpawnPets(List<PetType> petTypes, int ownerLevel, Vector3 ownerLand, bool isPlayer1, List<PetCombatController> outList, List<bool> doneFlags)
    {
        if (petTypes == null) yield break;

        foreach (var petType in petTypes)
        {
            var prefab = PetPrefabFor(petType);
            if (prefab == null)
            {
                Debug.LogError($"[CombatSceneLoader] Prefab não wireado para pet {petType} — pulando instanciação.");
                continue;
            }

            float x = isPlayer1 ? UnityEngine.Random.Range(-5f, -1f) : UnityEngine.Random.Range(1f, 5f);
            Vector3 landPos = new Vector3(x, ownerLand.y, ownerLand.z);

            var petObj = Instantiate(prefab);
            petObj.name = PetState.DisplayName(petType);

            float scale = PetState.Scale(petType);
            petObj.transform.localScale = new Vector3(scale, scale, scale);
            petObj.transform.position   = new Vector3(landPos.x, landPos.y + 12f, landPos.z);

            var petCombat = petObj.AddComponent<PetCombatController>();
            petCombat.petType = petType;
            petCombat.isPlayer1 = isPlayer1;
            outList.Add(petCombat);

            // Mesmo escalonamento por nível do dono aplicado em CombatSimulator.BuildState
            // (PetState.ApplyLevelScaling) — só pra essa preview (maxHp inicial da barra de
            // vida) não ficar desincronizada do maxHp real usado na simulação.
            var preview = PetState.Create(petType);
            preview?.ApplyLevelScaling(ownerLevel);
            petCombat.healthBar = HealthBarPet.Create(petObj.transform);
            if (preview != null)
            {
                petCombat.maxHp = preview.maxHp;
                petCombat.healthBar.UpdateBar(preview.hp, preview.maxHp);
            }

            int idx = doneFlags.Count;
            doneFlags.Add(false);
            StartCoroutine(EntryFall(petObj, landPos, () =>
            {
                petCombat.spawnPosition = landPos;
                doneFlags[idx] = true;
            }));

            yield return null;
        }
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
