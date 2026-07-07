using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatSceneLoader : MonoBehaviour
{
    [Header("References")]
    public AttackSequencer attackSequencer;
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;
    [Tooltip("Oponente escolhido em 05_SelectOpponent. Player2 é instanciado dinamicamente a partir do characterPrefab desse profile, mesmo fluxo do Player1.")]
    [SerializeField] private SelectedOpponentHolder selectedOpponentHolder;

    // Resolvido no início de Initialize() (Inspector/holder ou fallback de editor) — sempre
    // não-nulo depois do guard inicial, usado tanto pra instanciar Player2 quanto pro simulador.
    private PlayerProfile player2Profile;

    [Header("Simulator")]
    [Tooltip("Quando true, usa CombatSimulator em vez do AttackSequencer legado.")]
    [SerializeField] private bool useSimulator = true;

    [Header("Shield")]
    [Tooltip("WeaponData usado como visual permanente da skill Shield T1 (Assets/Data/UI/Weapons/Shield/Shield1.asset).")]
    [SerializeField] private WeaponData shieldWeaponData;
    [Tooltip("Visual da skill Shield T2 (Assets/Data/UI/Weapons/Shield/Shield2.asset). Se vazio, usa o visual do T1.")]
    [SerializeField] private WeaponData shieldWeaponDataT2;
    [Tooltip("Visual da skill Shield T3 (Assets/Data/UI/Weapons/Shield/Shield3.asset). Se vazio, usa o visual do T2/T1.")]
    [SerializeField] private WeaponData shieldWeaponDataT3;

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

    [Header("Treat")]
    [Tooltip("Sprite da coxa de frango (Assets/Data/UI/SkillEffect/Treat/treat.png) — arremessada em arco até o pet alimentado.")]
    [SerializeField] private Sprite treatSprite;

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

    // Nome fixo do GameObject de fundo já pré-colocado na cena (ver hierarquia em CLAUDE.md) —
    // achado por nome em vez de exigir um campo [SerializeField] wireado no Inspector, já que
    // ele já existe sozinho na cena com esse nome específico.
    private const string ArenaBackgroundObjectName = "Colosseum arena";

    // Nomes dos 50 arquivos em Assets/Resources/BattleGround (movida de Assets/BattleGround pra
    // poder usar Resources.Load aqui), sem extensão. Lista fixa em vez de Resources.LoadAll —
    // a pasta inteira soma ~500MB (imagens 3840x2160), e LoadAll carregaria todas as 50 pra
    // memória de uma vez só pra usar 1; Resources.Load(nome) carrega só a sorteada (~10MB).
    // Adicionar um arquivo novo na pasta exige adicionar o nome aqui também (não é automático).
    private static readonly string[] ArenaBackgroundNames =
    {
        "5", "6", "Castle arena", "Colosseum arena", "Desert ruins", "Dragon dungeon 2",
        "Dragon dungeon 3", "Dragon dungeon 4", "Forest", "Horizontal Battle Backgrounds 2",
        "Horizontal Battle Backgrounds 3", "Horizontal Battle Backgrounds 4", "PRIMAVERA",
        "Prison arena", "Ruins", "Sandy beach", "Underground ruins", "WINTER",
        "Winter arena", "battle arena", "battle arena2", "castle", "castle bridge",
        "castle corridor", "castle hall", "crystal cave", "dead forest", "desert", "empty cave",
        "enchanted stones", "floating castle", "floating islands", "forest bridge", "forest hut",
        "heavenly garden", "heavenly meadow", "hold ship", "magic forest", "magic portal",
        "mushroom forest", "night forest", "prison", "pyramid", "rocky shores", "ship deck",
        "sky bridge", "spider cave", "swamp", "terrace", "throne room", "tomb",
    };

    private void RandomizeArenaBackground()
    {
        var bgObject = GameObject.Find(ArenaBackgroundObjectName);
        var renderer = bgObject != null ? bgObject.GetComponent<SpriteRenderer>() : null;
        if (renderer == null) return;

        string chosen = ArenaBackgroundNames[UnityEngine.Random.Range(0, ArenaBackgroundNames.Length)];
        var sprite = Resources.Load<Sprite>("BattleGround/" + chosen);
        if (sprite != null) renderer.sprite = sprite;
    }

    private IEnumerator Initialize()
    {
        RandomizeArenaBackground();

        var profile = selectedProfileHolder.currentProfile;
        if (profile == null)
        {
            Debug.LogError("[CombatSceneLoader] No PlayerProfile selected.");
            yield break;
        }

        player2Profile = selectedOpponentHolder != null ? selectedOpponentHolder.currentOpponentProfile : null;
        if (player2Profile == null)
            player2Profile = LoadPlayer2ProfileFallback();
        if (player2Profile == null)
        {
            Debug.LogError("[CombatSceneLoader] No opponent PlayerProfile selected.");
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

        // Player2 (oponente) instanciado do mesmo jeito que Player1 — mirrorado no X (mesma
        // posição/escala que o antigo objeto pré-colocado usava: startPos.x negativo vira
        // positivo, scale.x negativo flipa o sprite pra olhar pra esquerda, na direção do
        // Player1). Deity aplicado do mesmo jeito, direto na escala inicial (era um multiplicador
        // separado em cima da escala pré-configurada na cena — não existe mais "escala pré-
        // configurada", então precisa entrar aqui igual ao Player1).
        GameObject player2Object = Instantiate(player2Profile.characterPrefab);
        player2Object.name = "Player2";
        player2Object.transform.position = new Vector3(-player2Profile.startPos.x, player2Profile.startPos.y, 0f);
        float p2ScaleMult = player2Profile.HasSkill("Deity") ? 1.5f : 1f;
        player2Object.transform.localScale = new Vector3(-0.3f * p2ScaleMult, 0.3f * p2ScaleMult, 0.3f * p2ScaleMult);

        var player2Combat = player2Object.GetComponent<PlayerCombat>();
        var player2Anim   = player2Object.GetComponent<AnimationController>();
        if (player2Combat == null || player2Anim == null)
        {
            Debug.LogError("[CombatSceneLoader] Opponent prefab is missing PlayerCombat or AnimationController.");
            yield break;
        }

        var loadout = player1Obj.GetComponent<PlayerLoadout>();
        if (loadout != null) loadout.loadout = profile.weapons;

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

        // Espelha o bloco acima — antes só existia hardcoded na cena (Player2 pré-colocado com
        // stats manualmente sincronizados com o profile). Agora que Player2 é instanciado a
        // partir de qualquer PlayerProfile do pool de oponentes, precisa da mesma cópia de stats
        // que o Player1 já recebe, senão qualquer oponente diferente da Girl combateria com os
        // valores default do componente (str/agility/speed = 10/10/10 etc.) em vez do profile.
        player2Combat.settings       = player2Profile.attackSettings;
        player2Combat.isPlayer1      = false;
        player2Combat.str            = player2Profile.str;
        player2Combat.agility        = player2Profile.agility;
        player2Combat.speed          = player2Profile.speed;
        player2Combat.armor          = player2Profile.armor;
        player2Combat.evasion        = player2Profile.evasion;
        player2Combat.accuracy       = player2Profile.accuracy;
        player2Combat.initiative     = player2Profile.initiative;
        player2Combat.reversal       = player2Profile.reversal;
        player2Combat.counter        = player2Profile.counter;
        player2Combat.criticalChance = player2Profile.criticalChance;
        player2Combat.hitSpeed       = player2Profile.hitSpeed;
        player2Combat.skills         = player2Profile.skills != null ? new List<SkillData>(player2Profile.skills) : new List<SkillData>();
        player2Combat.defender  = player1Combat;
        player2Combat.defenderAnimationController = player1Anim;

        int p1MaxHealth = ApplySkillStats(player1Combat, profile.maxHealth);
        var health1 = player1Obj.AddComponent<HealthSystem>();
        health1.Initialize(p1MaxHealth);

        // Shield: item visual permanente no braço oposto (offHandBone), fora do WeaponLoadout —
        // equipado uma única vez aqui, igual ao ajuste de escala da Deity acima, não pelo ciclo
        // normal de troca de armas (handler.EquipNext/EquipRandom nunca tocam isso). Visual
        // varia por tier da skill equipada (T1/T2/T3), ver ResolveShieldVisual.
        if (handler != null && profile.HasSkill("Shield"))
            handler.EquipShield(ResolveShieldVisual(profile.GetSkill("Shield")));

        // Mesma ApplySkillStats do Player1 acima — antes Player2 usava player2Profile.maxHealth
        // direto (sem bônus de skill), já que ela nunca tinha skills configuradas na cena
        // hardcoded. Com qualquer oponente do pool agora podendo ter skills que afetam HP
        // (Vitality, Immortal, Deity...), precisa do mesmo tratamento pra health2 não dessincronizar
        // do HP interno calculado pelo CombatSimulator (mesmo bug já documentado pro Player1).
        int p2MaxHealth = ApplySkillStats(player2Combat, player2Profile.maxHealth);

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
        // hardcoded no PlayerLoadout da cena em vez da lista de armas do seu próprio
        // PlayerProfile — não tinha efeito enquanto todos os profiles compartilhavam o
        // mesmo asset satélite, mas passa a divergir agora que cada profile tem sua própria lista.
        if (p2Loadout != null)
            p2Loadout.loadout = player2Profile.weapons;

        if (p2Loadout != null && p2Handler != null)
        {
            p2WeaponHUD = gameObject.AddComponent<WeaponHUD>();
            p2WeaponHUD.Initialize(p2Loadout, p2Handler, false, combatHUD.CanvasTransform);
        }

        // Espelha o equip de Shield do Player1 acima.
        if (p2Handler != null && player2Profile.HasSkill("Shield"))
            p2Handler.EquipShield(ResolveShieldVisual(player2Profile.GetSkill("Shield")));

        // Skills HUD: exibe skills ativas (Supers) com contador de usos abaixo do WeaponHUD.
        var p1SkillsHUD = gameObject.AddComponent<SkillsHUD>();
        p1SkillsHUD.Initialize(profile.skills, true, combatHUD.CanvasTransform);
        var p2SkillsHUD = gameObject.AddComponent<SkillsHUD>();
        p2SkillsHUD.Initialize(player2Profile.skills, false, combatHUD.CanvasTransform);

        List<CombatEvent> events = null;

        if (useSimulator)
        {
            // Pré-calcula a luta inteira ANTES do EntryFall — não depende de nada que só existe
            // depois dele (transform/spawnPosition), só lê os PlayerProfile, e permite pintar os
            // ícones sabotados pela Spy (abaixo) já antes da queda em cena. Personagens não
            // nascem mais com arma equipada (era 40% de chance — EquipStartingWeaponIfNeeded,
            // removido a pedido do usuário; todo mundo agora começa sempre desarmado).
            attackSequencer.player1Profile = profile;
            attackSequencer.player2Profile = player2Profile;

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
            attackSequencer.player2Profile = player2Profile;
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
            combatPlayer.treatSprite = treatSprite;
            combatPlayer.p1SkillsHUD = p1SkillsHUD;
            combatPlayer.p2SkillsHUD = p2SkillsHUD;
            combatPlayer.PlayCombat(events);

            combatHUD.AddSpeedControls(combatPlayer);
        }
    }

    // Escudo visual muda de sprite por tier da skill Shield equipada (T1/T2/T3) — cai pro tier
    // anterior se o campo do tier atual não estiver wireado no Inspector (T2 sem asset
    // assinado, por exemplo), e por fim pro T1 (sempre esperado presente).
    private WeaponData ResolveShieldVisual(SkillData shieldSkill)
    {
        int tier = shieldSkill != null ? shieldSkill.tier : 1;
        if (tier >= 3 && shieldWeaponDataT3 != null) return shieldWeaponDataT3;
        if (tier >= 2 && shieldWeaponDataT2 != null) return shieldWeaponDataT2;
        return shieldWeaponData;
    }

    // Editor-only convenience: se selectedOpponentHolder.currentOpponentProfile estiver vazio
    // (ex: abrindo 04_CombatScenePVP direto no Editor pra testar, sem passar por
    // 05_SelectOpponent), busca a Medieval Warrior Girl por path pra ainda rodar o simulador.
    // Em build isso nunca deveria disparar — o fluxo normal sempre passa por 05_SelectOpponent,
    // que grava selectedOpponentHolder antes de carregar esta cena.
    private static PlayerProfile LoadPlayer2ProfileFallback()
    {
#if UNITY_EDITOR
        var fallback = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerProfile>(Player2ProfileFallbackPath);
        if (fallback == null)
            Debug.LogError($"[CombatSceneLoader] Nenhum oponente selecionado e fallback não encontrado em {Player2ProfileFallbackPath}");
        return fallback;
#else
        Debug.LogError("[CombatSceneLoader] Nenhum oponente selecionado (selectedOpponentHolder vazio).");
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

        // Valores de efeito lidos do SkillData equipado (bonusValue1..6) — mesmo mapeamento de
        // CombatSimulator.ApplySkillStats, ver SKILLS_SYSTEM.md. Fallback (`?? literal`) só entra
        // em jogo se o asset ainda não tiver sido populado.
        var vitalitySkill = combat.GetSkill("Vitality");
        if (vitalitySkill != null)
        {
            // +18 flat já aplicado permanentemente em profile.maxHealth na escolha
            // (CombatResultPanel.ApplyBonus, lê bonusValue2 da mesma skill).
            hpPct += vitalitySkill.bonusValue1;
            combat.LogSkillCheck("Vitality", true, $"hp% += {vitalitySkill.bonusValue1:P0}");
        }

        var herculeanSkill = combat.GetSkill("Herculean Strength");
        if (herculeanSkill != null)
        {
            strPct += herculeanSkill.bonusValue1;
            combat.LogSkillCheck("Herculean Strength", true, $"str% += {herculeanSkill.bonusValue1:P0}");
        }

        var felineSkill = combat.GetSkill("Feline Agility");
        if (felineSkill != null)
        {
            agiPct += felineSkill.bonusValue1;
            combat.LogSkillCheck("Feline Agility", true, $"agility% += {felineSkill.bonusValue1:P0}");
        }

        var lightningSkill = combat.GetSkill("Lightning Bolt");
        if (lightningSkill != null)
        {
            // Antes afetava runSpeedMultiplier; agora afeta o atributo speed real (ações extra
            // no Speed System), mesmo padrão de Herculean/Feline.
            spdPct += lightningSkill.bonusValue1;
            combat.LogSkillCheck("Lightning Bolt", true, $"speed% += {lightningSkill.bonusValue1:P0}");
        }

        var reconnaissanceSkill = combat.GetSkill("Reconnaissance");
        if (reconnaissanceSkill != null)
        {
            // initiative/critDamageBonus são flat puro (não entram no percentual líquido).
            spdPct += reconnaissanceSkill.bonusValue1;
            combat.initiative -= Mathf.RoundToInt(reconnaissanceSkill.bonusValue3);
            combat.critDamageBonus += reconnaissanceSkill.bonusValue4;
            combat.LogSkillCheck("Reconnaissance", true,
                $"speed% += {reconnaissanceSkill.bonusValue1:P0}, initiative → {combat.initiative}, critDamageBonus → {combat.critDamageBonus:P0}");
        }

        var immortalSkill = combat.GetSkill("Immortal");
        if (immortalSkill != null)
        {
            hpPct  += immortalSkill.bonusValue1;
            strPct -= immortalSkill.bonusValue2;
            agiPct -= immortalSkill.bonusValue2;
            spdPct -= immortalSkill.bonusValue2;
            combat.LogSkillCheck("Immortal", true, $"hp% += {immortalSkill.bonusValue1:P0}, str/agi/speed% -= {immortalSkill.bonusValue2:P0}");
        }

        // Bodybuilder (redefinida — era strPct += 0.5f/"STR × 1.5"): +evasion e +hit speed
        // enquanto empunha Heavy, checado vivo (DodgeChance/CombatPlayer), sem estado fixo aqui
        // já que a arma equipada pode trocar durante a luta.

        var armourSkill = combat.GetSkill("Armour");
        if (armourSkill != null)
        {
            // armor (flat, fora do percentual líquido) e spd (entra normalmente, subtraído).
            combat.armor += armourSkill.bonusValue1;
            spdPct       -= armourSkill.bonusValue2;
            combat.LogSkillCheck("Armour", true, $"armor → {combat.armor:P0}, speed% -= {armourSkill.bonusValue2:P0}");
        }

        var deitySkill = combat.GetSkill("Deity");
        if (deitySkill != null)
        {
            // hpPct/strPct positivos, agiPct/spdPct/evasionPct negativos (magnitude em
            // bonusValueN, subtraída). reversal e o +50% de tamanho (abaixo, em Initialize)
            // ficam como exceção hardcoded — não coube nos 6 bonusValue já usados aqui, ver
            // SKILLS_SYSTEM.md.
            hpPct      += deitySkill.bonusValue1;
            strPct     += deitySkill.bonusValue2;
            agiPct     -= deitySkill.bonusValue3;
            spdPct     -= deitySkill.bonusValue4;
            evasionPct -= deitySkill.bonusValue5;
            combat.reversal   += 0.40f; // exceção hardcoded (7º valor de Deity)
            combat.initiative -= Mathf.RoundToInt(deitySkill.bonusValue6);
            combat.LogSkillCheck("Deity", true,
                $"hp/str% += {deitySkill.bonusValue1:P0}, agi% -= {deitySkill.bonusValue3:P0}, spd% -= {deitySkill.bonusValue4:P0}, evasion% -= {deitySkill.bonusValue5:P0}, reversal → {combat.reversal:P0}, initiative → {combat.initiative}");
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

        var toughenedSkinSkill = combat.GetSkill("Toughened Skin");
        if (toughenedSkinSkill != null)
        {
            combat.armor += toughenedSkinSkill.bonusValue1;
            combat.LogSkillCheck("Toughened Skin", true, $"armor → {combat.armor:P0}");
        }

        var shieldSkill = combat.GetSkill("Shield");
        if (shieldSkill != null)
        {
            // blockBonus (mesmo campo de Counter Attack — soma em BlockChance). A penalidade de
            // dano causado (bonusValue2, era "armor +=" antes) só existe no simulador
            // (CombatSimulator.CalcDamage) — não é um stat exibido no CharacterPanel, então não
            // entra aqui. hasShield habilita o desarme próprio do escudo (agora usando
            // DisarmChance real, ver CombatSimulator) — se cair, os bônus são revertidos.
            // Visual (sprite no braço oposto) equipado fora daqui, em Initialize.
            combat.blockBonus += shieldSkill.bonusValue1;
            combat.hasShield   = true;
            combat.LogSkillCheck("Shield", true, $"blockBonus → {combat.blockBonus:P0}");
        }

        var untouchableSkill = combat.GetSkill("Untouchable");
        if (untouchableSkill != null)
        {
            combat.evasion += untouchableSkill.bonusValue1;
            combat.LogSkillCheck("Untouchable", true, $"evasion → {combat.evasion:P0}");
        }

        var relentlessSkill = combat.GetSkill("Relentless");
        if (relentlessSkill != null)
        {
            combat.accuracy += relentlessSkill.bonusValue1;
            combat.LogSkillCheck("Relentless", true, $"accuracy → {combat.accuracy:P0}");
        }

        var fistsOfFurySkill = combat.GetSkill("Fists of Fury");
        if (fistsOfFurySkill != null)
        {
            combat.comboChanceBonus += fistsOfFurySkill.bonusValue1;
            combat.LogSkillCheck("Fists of Fury", true, $"comboChanceBonus → {combat.comboChanceBonus:P0}");
        }

        // Chaining T2/T3: comboChanceBonus adicional (bonusValue3, novo — T1 fica 0). O resto
        // da mecânica (threshold/stun) só existe no simulador, não entra no preview de stats.
        var chainingSkillStats = combat.GetSkill("Chaining");
        if (chainingSkillStats != null && chainingSkillStats.bonusValue3 != 0f)
        {
            combat.comboChanceBonus += chainingSkillStats.bonusValue3;
            combat.LogSkillCheck("Chaining", true, $"comboChanceBonus → {combat.comboChanceBonus:P0}");
        }

        var leadSkeletonSkill = combat.GetSkill("Lead Skeleton");
        if (leadSkeletonSkill != null)
        {
            // armor/evasion (bonusValue1/2, evasion subtraída; floor em 0 garantido pelo clamp
            // incondicional de evasionPct abaixo) mantendo o dano de arma blunt multiplicado por
            // bonusValue3 (0.85 = -15%) já existente.
            combat.leadSkeleton = true;
            combat.armor   += leadSkeletonSkill.bonusValue1;
            combat.evasion -= leadSkeletonSkill.bonusValue2;
            combat.LogSkillCheck("Lead Skeleton", true, $"armor → {combat.armor:P0}, evasion → {combat.evasion:P0}, blunt damage ×{leadSkeletonSkill.bonusValue3:0.00}");
        }

        var balletShoesSkill = combat.GetSkill("Ballet Shoes");
        if (balletShoesSkill != null)
        {
            combat.evasion        += balletShoesSkill.bonusValue1;
            combat.firstHitAvoided = true;
            combat.LogSkillCheck("Ballet Shoes", true, $"evasion +{balletShoesSkill.bonusValue1:P0} → {combat.evasion:P0}, first hit auto-avoided");
        }

        var firstStrikeSkill = combat.GetSkill("First Strike");
        if (firstStrikeSkill != null)
        {
            combat.initiative += Mathf.RoundToInt(firstStrikeSkill.bonusValue1);
            combat.LogSkillCheck("First Strike", true, $"initiative → {combat.initiative}");
        }

        var counterAttackSkill = combat.GetSkill("Counter Attack");
        if (counterAttackSkill != null)
        {
            combat.blockBonus += counterAttackSkill.bonusValue1;
            combat.reversalAfterBlock += counterAttackSkill.bonusValue2;
            combat.LogSkillCheck("Counter Attack", true,
                $"blockBonus → {combat.blockBonus:P0}, reversalAfterBlock → {combat.reversalAfterBlock:P0}");
        }

        var sixthSenseSkill = combat.GetSkill("Sixth Sense");
        if (sixthSenseSkill != null)
        {
            combat.counter += sixthSenseSkill.bonusValue1;
            combat.LogSkillCheck("Sixth Sense", true, $"counter → {combat.counter:P0}");
        }

        var hostilitySkill = combat.GetSkill("Hostility");
        if (hostilitySkill != null)
        {
            combat.reversal += hostilitySkill.bonusValue1;
            combat.LogSkillCheck("Hostility", true, $"reversal → {combat.reversal:P0}");
        }

        var monkSkill = combat.GetSkill("Monk");
        if (monkSkill != null)
        {
            // Não guarda mais (era hitSpeed = 0f, removido — Monk ataca normalmente, igual a
            // qualquer personagem) — só o bônus de counter e o malus de iniciativa permanecem.
            combat.counter    += monkSkill.bonusValue1;
            combat.initiative -= Mathf.RoundToInt(monkSkill.bonusValue2);
            combat.LogSkillCheck("Monk", true, $"counter +{monkSkill.bonusValue1:P0}, initiative −{monkSkill.bonusValue2}");
        }

        var martialArtsSkill = combat.GetSkill("Martial Arts");
        if (martialArtsSkill != null)
        {
            combat.martialArts = true;
            combat.LogSkillCheck("Martial Arts", true, $"unarmed damage ×{martialArtsSkill.bonusValue1:0.00}");
        }

        var shockSkill = combat.GetSkill("Shock");
        if (shockSkill != null)
        {
            combat.disarmChanceBonus += shockSkill.bonusValue1;
            combat.LogSkillCheck("Shock", true, $"disarmChanceBonus → {combat.disarmChanceBonus:P0}");
        }

        var weaponMasterSkill = combat.GetSkill("Weapon Master");
        if (weaponMasterSkill != null)
        {
            combat.weaponsMaster = true;
            combat.LogSkillCheck("Weapon Master", true, $"×{weaponMasterSkill.bonusValue1:0.00} damage with sharp-tagged weapons");
        }

        var stickyHandsSkill = combat.GetSkill("Sticky Hands");
        if (stickyHandsSkill != null)
        {
            combat.stickyHands += stickyHandsSkill.bonusValue1;
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

        // Rastreia posições de spawn já escolhidas pra evitar sobreposição entre pets do mesmo lado.
        var chosenPositions = new List<UnityEngine.Vector2>();
        const float SpawnMinSep = 1.5f;
        const int   SpawnTries  = 12;

        foreach (var petType in petTypes)
        {
            var prefab = PetPrefabFor(petType);
            if (prefab == null)
            {
                Debug.LogError($"[CombatSceneLoader] Prefab não wireado para pet {petType} — pulando instanciação.");
                continue;
            }

            // Sorteia posição espalhada (X + Y) para o pet não nascer agrupado com os outros.
            // Mesma faixa de Y usada pelo RollPetSpawnPosition do CombatPlayer em combate.
            float spawnX = 0f, spawnY = 0f;
            for (int attempt = 0; attempt < SpawnTries; attempt++)
            {
                spawnX = isPlayer1 ? UnityEngine.Random.Range(-5f, -1f) : UnityEngine.Random.Range(1f, 5f);
                spawnY = UnityEngine.Random.Range(-3.90f, -0.81f);
                bool tooClose = false;
                foreach (var prev in chosenPositions)
                    if (UnityEngine.Vector2.Distance(new UnityEngine.Vector2(spawnX, spawnY), prev) < SpawnMinSep)
                    { tooClose = true; break; }
                if (!tooClose) break;
            }
            chosenPositions.Add(new UnityEngine.Vector2(spawnX, spawnY));
            Vector3 landPos = new Vector3(spawnX, spawnY, ownerLand.z);

            var petObj = Instantiate(prefab);
            petObj.name = PetState.DisplayName(petType);

            float scale = PetState.Scale(petType);
            petObj.transform.localScale = new Vector3(scale, scale, scale);
            petObj.transform.position   = new Vector3(landPos.x, landPos.y + 12f, landPos.z);

            var petCombat = petObj.AddComponent<PetCombatController>();
            petCombat.petType = petType;
            petCombat.isPlayer1 = isPlayer1;
            // P1 fica no lado esquerdo (X negativo) → pet vira pra direita (+1).
            // P2 fica no lado direito (X positivo) → pet vira pra esquerda (-1).
            petCombat.SetInitialFacing(isPlayer1 ? 1f : -1f);
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
