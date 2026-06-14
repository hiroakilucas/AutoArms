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
    public string description;

    [Header("Visual")]
    public Sprite icon;

    [Header("Classificação")]
    public SkillCategory category;
    public SkillActivationType activationType;

    [Tooltip("Número de usos por luta. Apenas relevante para Supers (Active).")]
    public int usesPerFight = 1;
}
