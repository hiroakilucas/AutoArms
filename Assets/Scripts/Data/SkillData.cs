using UnityEngine;

public enum SkillCategory
{
    CombatPassive,
    DefensePassive,
    StatBoost,
    WeaponPassive,
    Super
}

public enum SkillActivationType
{
    Passive,
    Active
}

[CreateAssetMenu(fileName = "NewSkill", menuName = "Game/Skill Data", order = 101)]
public class SkillData : ScriptableObject
{
    [Header("Identificação")]
    public string skillName;
    [TextArea(2, 5)]
    public string description;
    [Tooltip("Texto do efeito mecânico, formato \"Label +[v1/v2/v3]%\" — os 3 valores entre colchetes são os 3 tiers; o popup de detalhe destaca o valor do tier equipado. Vazio para skills ainda não implementadas (Garimpeiro/Magneto).")]
    [TextArea(2, 5)]
    public string effectText;

    [Header("Visual")]
    public Sprite icon;

    [Header("Classificação")]
    public SkillCategory category;
    public SkillActivationType activationType;

    [Tooltip("Número de usos por luta. Apenas relevante para Supers (Active).")]
    public int usesPerFight = 1;

    [Header("Tier")]
    public int tier = 1;
    public SkillData previousTier;

    [Header("Valores de Efeito")]
    [Tooltip("Valores numéricos usados pela lógica da skill em CombatSimulator/CombatSceneLoader/CombatResultPanel/PlayerProfile — o significado de cada campo varia por skill, ver mapeamento em SKILLS_SYSTEM.md.")]
    public float bonusValue1;
    public float bonusValue2;
    public float bonusValue3;
    public float bonusValue4;
    public float bonusValue5;
    public float bonusValue6;
    public float bonusValue7;
}
