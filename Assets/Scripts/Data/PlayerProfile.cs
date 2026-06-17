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

    private bool HasSkill(string skillName)
    {
        if (skills == null) return false;
        foreach (var s in skills)
            if (s != null && s.skillName == skillName) return true;
        return false;
    }

    // Preview-only: mirrors the HP/str/agility/speed bonuses from
    // CombatSimulator.ApplySkillStats / CombatSceneLoader.ApplySkillStats, for display purposes
    // (e.g. MainMenuCharacterPreview) without needing a live PlayerCombat/PlayerState instance.
    // Keep in sync with those two if a skill affecting these four stats changes.
    public (int hp, int str, int agility, int speed) GetEffectiveStats()
    {
        int hp = maxHealth, s = str, a = agility, sp = speed;

        if (HasSkill("Vitality")) hp += 50;
        if (HasSkill("Herculean Strength")) { s += 15; a -= 4; }
        if (HasSkill("Feline Agility")) a = Mathf.RoundToInt(a * 1.5f);
        if (HasSkill("Bodybuilder")) s = Mathf.RoundToInt(s * 1.5f);
        if (HasSkill("Immortal"))
        {
            hp = Mathf.RoundToInt(hp * 3.5f);
            s  = Mathf.RoundToInt(s * 0.75f);
            a  = Mathf.RoundToInt(a * 0.75f);
            sp = Mathf.RoundToInt(sp * 0.75f);
        }

        return (hp, s, a, sp);
    }
}
