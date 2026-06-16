using System.Collections.Generic;

// Mutable snapshot of one combatant's state inside CombatSimulator.
// Pure C# — no MonoBehaviour. Used only during pre-calculation.
public class PlayerState
{
    public int    index;
    public string name;

    // Stats (copied from PlayerProfile, then modified by ApplySkillStats)
    public int   hp;
    public int   maxHp;
    public int   str;
    public int   agility;
    public int   speed;
    public int   initiative;
    public float armor;
    public float evasion;
    public float accuracy;
    public float counter;
    public float criticalChance;
    public float comboChanceBonus;
    public float hitSpeed;
    public float runSpeedMultiplier;

    // Skill state flags
    public bool leadSkeleton;
    public bool firstHitAvoided;

    // Weapons
    public List<WeaponData> weaponLoadout = new List<WeaponData>();
    public WeaponData       currentWeaponData;

    // Skills (by name, for HasSkill checks)
    public List<string> skills = new List<string>();

    // Speed debt accumulation across rounds
    public int speedDebt;

    public bool isAlive => hp > 0;

    public bool HasSkill(string skillName)
    {
        if (skills == null) return false;
        foreach (var s in skills)
            if (s == skillName) return true;
        return false;
    }
}
