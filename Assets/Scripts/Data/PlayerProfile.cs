using UnityEngine;
using System.Collections.Generic;

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

    [Header("Par�metros de Combate")]
    [Tooltip("Configura��es de ataque e anima��o (velocidade, idle, delay etc.)")]
    public AttackSettings attackSettings;

    [Tooltip("Armas atribu�das para esse personagem")]
    public WeaponLoadout weaponLoadout;

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
    public float criticalChance = 0f;
    public float hitSpeed = 1f;

    [Header("Skills")]
    public List<SkillData> skills = new List<SkillData>();

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

    public bool HasSkill(string skillName)
    {
        if (skills == null) return false;
        foreach (var s in skills)
            if (s != null && s.skillName == skillName) return true;
        return false;
    }

    // Preview-only: mirrors the HP/str/agility/speed/initiative/critChance/critDamageBonus/
    // evasion/reversal bonuses from CombatSimulator.ApplySkillStats /
    // CombatSceneLoader.ApplySkillStats, for display purposes (e.g. MainMenuCharacterPreview)
    // without needing a live PlayerCombat/PlayerState instance. Keep in sync with those two if
    // a skill affecting these stats changes.
    public (int hp, int str, int agility, int speed, int initiative, float criticalChance, float critDamageBonus, float evasion, float reversal) GetEffectiveStats()
    {
        int hp = maxHealth, s = str, a = agility, sp = speed, init = initiative;
        float critChance = criticalChance, critDmgBonus = 0f, eva = evasion, rev = reversal;

        // Percentuais somados num percentual líquido por status, aplicados uma única vez —
        // evita arredondamento em cascata quando múltiplas skills afetam o mesmo status
        // (ex: Herculean Strength + Immortal no mesmo STR). Ver nota em "Skills que modificam
        // stats" no CLAUDE.md. Iniciativa é flat puro, fora do percentual líquido. evasionPct
        // é multiplicativo sobre eva, aplicado depois de todas as somas flat (Deity zera mesmo
        // que outra skill já tenha somado evasion).
        float hpPct = 0f, sPct = 0f, aPct = 0f, spPct = 0f, evaPct = 0f;

        // +18 flat já está em profile.maxHealth (aplicado na escolha, CombatResultPanel.ApplyBonus).
        if (HasSkill("Vitality")) hpPct += 0.5f;
        // +3 flat já está em profile.str (aplicado na escolha, CombatResultPanel.ApplyBonus);
        // aqui só o +50%, sem penalidade de agilidade.
        if (HasSkill("Herculean Strength")) sPct += 0.5f;
        if (HasSkill("Feline Agility")) aPct += 0.5f;
        if (HasSkill("Lightning Bolt")) spPct += 0.5f;
        // +5 flat já está em profile.speed (aplicado na escolha); -200 iniciativa e +50% dano
        // crítico são flat puro.
        if (HasSkill("Reconnaissance")) { spPct += 1.5f; init -= 200; critDmgBonus += 0.5f; }
        if (HasSkill("Bodybuilder")) sPct += 0.5f;
        if (HasSkill("First Strike")) init += 200;
        if (HasSkill("Monk")) init -= 200;
        if (HasSkill("Immortal"))
        {
            hpPct += 2.5f;
            sPct  -= 0.25f;
            aPct  -= 0.25f;
            spPct -= 0.25f;
        }
        if (HasSkill("Deity"))
        {
            // -90% SPD, não -100%: ver CombatSimulator/CombatSceneLoader, mesmo motivo.
            hpPct  += 1.0f;
            sPct   += 1.0f;
            aPct   -= 1.0f;
            spPct  -= 0.90f;
            evaPct -= 1.0f;
            rev    += 0.40f;
            init   -= 200;
        }
        if (HasSkill("Untouchable")) eva += 0.25f;
        if (HasSkill("Ballet Shoes")) eva += 0.10f;

        if (hpPct != 0f || sPct != 0f || aPct != 0f || spPct != 0f)
        {
            hp = Mathf.RoundToInt(hp * (1f + hpPct));
            s  = Mathf.RoundToInt(s * (1f + sPct));
            a  = Mathf.RoundToInt(a * (1f + aPct));
            sp = Mathf.RoundToInt(sp * (1f + spPct));
        }
        if (evaPct != 0f) eva = Mathf.Max(0f, eva * (1f + evaPct));

        return (hp, s, a, sp, init, critChance, critDmgBonus, eva, rev);
    }
}
