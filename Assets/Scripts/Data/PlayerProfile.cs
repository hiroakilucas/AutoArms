using UnityEngine;
using System.Collections.Generic;

// Rato (Mouse, equivalente ao Dog), Macaco (Monkey, equivalente ao Wolf/Panther), Javali
// (Boar, equivalente ao Bear) — ver stats completos em PetState.cs.
public enum PetType
{
    None,
    Mouse,
    Monkey,
    Boar,
}

[CreateAssetMenu(fileName = "NewPlayerProfile", menuName = "Game/Player Profile", order = 100)]
public class PlayerProfile : ScriptableObject
{
    [Header("Identifica��o")]
    [Tooltip("Nome do personagem para exibi��o")]
    public string profileName;

    [Header("Prefab e Imagem")]
    [Tooltip("Prefab do personagem (deve conter todos os componentes necess�rios para o combate)")]
    public GameObject characterPrefab;

    [Tooltip("�cone utilizado na UI de sele��o de personagens")]
    public Sprite previewIcon;

    [Tooltip("Arte de fundo em tela cheia (splash art) mostrada no Frame ao selecionar este personagem em 02_SelectCharacter. Vazio = mant�m o placeholder dourado.")]
    public Sprite splashArt;

    [Header("Par�metros de Combate")]
    [Tooltip("Configura��es de ataque e anima��o (velocidade, idle, delay etc.)")]
    public AttackSettings attackSettings;

    [Tooltip("Armas atribu�das para esse personagem")]
    public List<WeaponData> weapons = new List<WeaponData>();

    [Header("Instancia��o")]
    [Tooltip("Escala personalizada do personagem no momento da inst�ncia")]
    public Vector3 scale = Vector3.one;

    [Tooltip("Posi��o de in�cio no combate (usada no PvP, exemplo: lado esquerdo)")]
    public Vector2 startPos = new Vector2(-6.3f, -2.407897f);

    [Header("Combate")]
    [Tooltip("Vida máxima do personagem")]
    public int maxHealth = 50;

    [Header("Atributos")]
    public int str = 10;
    public int agility = 10;
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
    public float hitSpeed = 1f;

    [Header("Skills")]
    public List<SkillData> skills = new List<SkillData>();

    // Pode ter múltiplos pets do mesmo tipo (ex: 3 Ratos) — sem restrição de duplicatas, cada
    // entrada vira uma instância independente (PetState) com seu próprio HP/estado na luta.
    // Stats efetivos de cada pet (já com o escalonamento por nível do dono — ver
    // PetState.ApplyLevelScaling/CombatSimulator.BuildState) não têm exibição na UI ainda;
    // intenção documentada pra uma futura GetEffectivePetStats(), mesmo padrão de
    // GetEffectiveStats() abaixo, quando o CharacterPanel ganhar uma aba/seção própria de Pets.
    [Header("Pets")]
    public List<PetType> pets = new List<PetType>();

    [Header("Progresso")]
    [Tooltip("N�vel atual do personagem")]
    public int level = 1;

    [Tooltip("Taxa de vit�ria (0 a 100%)")]
    [Range(0f, 100f)] public float winRate = 0f;

    [Tooltip("Experi�ncia atual")]
    public int xpCurrent = 0;

    [Tooltip("Experi�ncia necess�ria para o pr�ximo n�vel")]
    public int xpRequired = 6;

    [Tooltip("Lutas restantes (m�ximo por ciclo)")]
    public int battlesRemaining = 6;

    // Chave estável pro histórico de batalhas por oponente (PlayerPrefs "battles_{id}"/
    // "wins_{id}", ver SelectOpponentController/AttackSequencer) — usa o nome do próprio asset
    // (Object.name), já que não existe um campo de ID dedicado. Repetir o mesmo PlayerProfile
    // várias vezes no pool de oponentes (ex: 6x Medieval Warrior Girl) soma no mesmo histórico
    // de propósito — é literalmente o mesmo personagem.
    public string OpponentId() => name;

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

    // Preview-only: mirrors the HP/str/agility/speed/initiative/critChance/critDamageBonus/
    // evasion/reversal/counter/comboChanceBonus/armor bonuses from CombatSimulator.ApplySkillStats /
    // CombatSceneLoader.ApplySkillStats, for display purposes (e.g. MainMenuCharacterPreview)
    // without needing a live PlayerCombat/PlayerState instance. Keep in sync with those two if
    // a skill affecting these stats changes.
    public (int hp, int str, int agility, int speed, int initiative, float criticalChance, float critDamageBonus, float evasion, float reversal, float counter, float comboChanceBonus, float armor, float accuracy, float blockBonus, float reversalAfterBlock, float disarmChanceBonus, float sharpDamageBonus, float heavyDexterityBonus, float heavyHitSpeedBonus) GetEffectiveStats()
    {
        int hp = maxHealth, s = str, a = agility, sp = speed, init = initiative;
        float critChance = criticalChance, critDmgBonus = 0f, eva = evasion, rev = reversal, cnt = counter, combo = 0f, arm = armor, acc = accuracy;
        float blk = blockBonus, revBlk = reversalAfterBlock, disarm = 0f, sharp = 0f, heavyDex = 0f, heavyHitSpd = 0f;

        // Percentuais somados num percentual líquido por status, aplicados uma única vez —
        // evita arredondamento em cascata quando múltiplas skills afetam o mesmo status
        // (ex: Herculean Strength + Immortal no mesmo STR). Ver nota em "Skills que modificam
        // stats" no CLAUDE.md. Iniciativa é flat puro, fora do percentual líquido. evasionPct
        // é multiplicativo sobre eva, aplicado depois de todas as somas flat (Deity zera mesmo
        // que outra skill já tenha somado evasion).
        float hpPct = 0f, sPct = 0f, aPct = 0f, spPct = 0f, evaPct = 0f;

        // Valores de efeito lidos do SkillData equipado (bonusValue1..6) — mesmo mapeamento de
        // CombatSimulator.ApplySkillStats, ver SKILLS_SYSTEM.md.
        // +18 flat já está em profile.maxHealth (aplicado na escolha, CombatResultPanel.ApplyBonus).
        var vitalitySk = GetSkill("Vitality");
        if (vitalitySk != null) hpPct += vitalitySk.bonusValue1;
        // +3 flat já está em profile.str (aplicado na escolha, CombatResultPanel.ApplyBonus);
        // aqui só o percentual, sem penalidade de agilidade.
        var herculeanSk = GetSkill("Herculean Strength");
        if (herculeanSk != null) sPct += herculeanSk.bonusValue1;
        var felineSk = GetSkill("Feline Agility");
        if (felineSk != null) aPct += felineSk.bonusValue1;
        var lightningSk = GetSkill("Lightning Bolt");
        if (lightningSk != null) spPct += lightningSk.bonusValue1;
        // +5 flat já está em profile.speed (aplicado na escolha); initiative/critDmgBonus são flat puro.
        var reconnaissanceSk = GetSkill("Reconnaissance");
        if (reconnaissanceSk != null) { spPct += reconnaissanceSk.bonusValue1; init -= Mathf.RoundToInt(reconnaissanceSk.bonusValue3); critDmgBonus += reconnaissanceSk.bonusValue4; }
        var firstStrikeSk = GetSkill("First Strike");
        if (firstStrikeSk != null) init += Mathf.RoundToInt(firstStrikeSk.bonusValue1);
        var monkSk = GetSkill("Monk");
        if (monkSk != null) { init -= Mathf.RoundToInt(monkSk.bonusValue2); cnt += monkSk.bonusValue1; }
        var counterAttackSk = GetSkill("Counter Attack");
        if (counterAttackSk != null) { blk += counterAttackSk.bonusValue1; revBlk += counterAttackSk.bonusValue2; }
        var sixthSenseSk = GetSkill("Sixth Sense");
        if (sixthSenseSk != null) cnt += sixthSenseSk.bonusValue1;
        var hostilitySk = GetSkill("Hostility");
        if (hostilitySk != null) rev += hostilitySk.bonusValue1;
        var relentlessSk = GetSkill("Relentless");
        if (relentlessSk != null) acc += relentlessSk.bonusValue1;
        var fistsOfFurySk = GetSkill("Fists of Fury");
        if (fistsOfFurySk != null) combo += fistsOfFurySk.bonusValue1;
        // Chaining T2/T3: comboChanceBonus adicional (bonusValue3, novo — T1 fica 0).
        var chainingSk = GetSkill("Chaining");
        if (chainingSk != null) combo += chainingSk.bonusValue3;
        var shockSk = GetSkill("Shock");
        if (shockSk != null) disarm += shockSk.bonusValue1;
        var weaponMasterSk = GetSkill("Weapon Master");
        if (weaponMasterSk != null) sharp += weaponMasterSk.bonusValue1 - 1f; // bonusValue1 é o multiplicador (1.5); sharp é exibido como bônus (+0.5)
        // Informativo apenas: o bônus real só vale enquanto empunha arma Heavy (checado vivo
        // em DodgeChance/CombatPlayer) — aqui só confirma a magnitude da skill, igual ao padrão
        // de Disarm/Sharp acima.
        var bodybuilderSk = GetSkill("Bodybuilder");
        if (bodybuilderSk != null) { heavyDex += bodybuilderSk.bonusValue1; heavyHitSpd += bodybuilderSk.bonusValue2; }
        var armourSk = GetSkill("Armour");
        if (armourSk != null) { arm += armourSk.bonusValue1; spPct -= armourSk.bonusValue2; }
        var toughenedSkinSk = GetSkill("Toughened Skin");
        if (toughenedSkinSk != null) arm += toughenedSkinSk.bonusValue1;
        // Shield: a penalidade de dano causado (bonusValue2, era "armor +=" antes) só existe no
        // simulador (CombatSimulator.CalcDamage) — não é um dos stats desta tupla, então não
        // entra aqui.
        var shieldSk = GetSkill("Shield");
        if (shieldSk != null) blk += shieldSk.bonusValue1;
        // Lead Skeleton (redefinida — antes só dava -15% dano de Heavy, sem entrar aqui):
        // armor/evasion (bonusValue1/2). O dano de arma blunt (Heavy) continua existindo
        // (ver CombatSimulator/PlayerCombat, bonusValue3), só não aparece aqui por não ser
        // um % de stat.
        var leadSkeletonSk = GetSkill("Lead Skeleton");
        if (leadSkeletonSk != null) { arm += leadSkeletonSk.bonusValue1; eva -= leadSkeletonSk.bonusValue2; }
        var immortalSk = GetSkill("Immortal");
        if (immortalSk != null)
        {
            hpPct += immortalSk.bonusValue1;
            sPct  -= immortalSk.bonusValue2;
            aPct  -= immortalSk.bonusValue2;
            spPct -= immortalSk.bonusValue2;
        }
        var deitySk = GetSkill("Deity");
        if (deitySk != null)
        {
            // reversal e iniciativa mantêm o valor hardcoded (7º/8º valor de Deity, excedente
            // aos 6 bonusValue — ver CombatSimulator.ApplySkillStats).
            hpPct  += deitySk.bonusValue1;
            sPct   += deitySk.bonusValue2;
            aPct   -= deitySk.bonusValue3;
            spPct  -= deitySk.bonusValue4;
            evaPct -= deitySk.bonusValue5;
            rev    += 0.40f;
            init   -= Mathf.RoundToInt(deitySk.bonusValue6);
        }
        var untouchableSk = GetSkill("Untouchable");
        if (untouchableSk != null) eva += untouchableSk.bonusValue1;
        var balletShoesSk = GetSkill("Ballet Shoes");
        if (balletShoesSk != null) eva += balletShoesSk.bonusValue1;

        if (hpPct != 0f || sPct != 0f || aPct != 0f || spPct != 0f)
        {
            hp = Mathf.RoundToInt(hp * (1f + hpPct));
            s  = Mathf.RoundToInt(s * (1f + sPct));
            a  = Mathf.RoundToInt(a * (1f + aPct));
            sp = Mathf.RoundToInt(sp * (1f + spPct));
        }
        // Incondicional (não só quando evaPct != 0) pra também garantir o floor em 0 quando só
        // Lead Skeleton (-15% flat) deixa o total negativo.
        eva = Mathf.Max(0f, eva * (1f + evaPct));

        return (hp, s, a, sp, init, critChance, critDmgBonus, eva, rev, cnt, combo, arm, acc, blk, revBlk, disarm, sharp, heavyDex, heavyHitSpd);
    }

    // Valores de damage dos assets representativos de cada arquétipo, pra preview de UI sem
    // depender de carregar o WeaponData em runtime — Satyr1 (Adaga: Sharp+Fast), trio Sword
    // (Espada: Sharp), Golem3 (Pesado: Heavy+Blunt). Cada WeaponData agora tem um campo
    // `damage` fixo próprio (sem mais range aleatório por categoria) — se esses 3 assets
    // mudarem de damage, atualizar aqui também (mesmo padrão de "cópia independente pra
    // exibição" das outras faixas de preview deste arquivo).
    private const int DaggerArchetypeDamage = 10;
    private const int SwordArchetypeDamage  = 14;
    private const int HeavyArchetypeDamage  = 40;

    // Faixas de dano normal (sem crítico) por arquétipo de arma, com STR efetivo
    // (GetEffectiveStats) e bônus de skill já aplicados (Martial Arts dobra o desarmado;
    // Weapon Master dá +50% em armas Sharp) — espelha WeaponBaseDamage()/CalcDamage() de
    // CombatSimulator/PlayerCombat para fins de exibição (CharacterPanel, aba Stats), sem
    // precisar de uma instância de combate em runtime. STR soma flat (não multiplica) — ver
    // Fórmula de Dano no CLAUDE.md. Sem mais range aleatório: min==max em todo arquétipo
    // (collapsa pra um número só nos helpers SetStatRange/SetStatRangeWithBase do CharacterPanel).
    public (int unarmedMin, int unarmedMax, int daggerMin, int daggerMax, int swordMin, int swordMax, int heavyMin, int heavyMax) GetWeaponDamageRanges()
    {
        int   str = GetEffectiveStats().str;
        var martialArtsSk = GetSkill("Martial Arts");
        int unarmedBase = martialArtsSk != null ? Mathf.RoundToInt(UnarmedStats.Damage * (martialArtsSk.bonusValue1 > 0f ? martialArtsSk.bonusValue1 : 2f)) : UnarmedStats.Damage;
        int u  = Mathf.Max(1, unarmedBase + str);
        var weaponMasterSk = GetSkill("Weapon Master");
        float sharpMult = weaponMasterSk != null ? (weaponMasterSk.bonusValue1 > 0f ? weaponMasterSk.bonusValue1 : 1.5f) : 1f;
        int d  = Mathf.Max(1, Mathf.RoundToInt((DaggerArchetypeDamage + str) * sharpMult));
        int sw = Mathf.Max(1, Mathf.RoundToInt((SwordArchetypeDamage  + str) * sharpMult));
        int h  = Mathf.Max(1, HeavyArchetypeDamage + str);
        return (u, u, d, d, sw, sw, h, h);
    }

    // Faixa "base" (sem Weapon Master) de Adaga/Espada, pra UI mostrar base→efetivo (mesmo
    // padrão de Desarmado/Martial Arts). Não inclui no tuple principal pra não duplicar os
    // campos unarmed/heavy, que nenhuma skill de "sharp" afeta.
    public (int daggerMin, int daggerMax, int swordMin, int swordMax) GetBaseSharpDamageRanges()
    {
        int str = GetEffectiveStats().str;
        int d  = Mathf.Max(1, DaggerArchetypeDamage + str);
        int sw = Mathf.Max(1, SwordArchetypeDamage  + str);
        return (d, d, sw, sw);
    }
}
