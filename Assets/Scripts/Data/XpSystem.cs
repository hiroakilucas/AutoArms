public static class XpSystem
{
    public struct LevelUpResult
    {
        public bool didLevelUp;
        public int  newLevel;
    }

    // Correct formula matching the table: 1→2=6, 2→3=12, 3→4=20, 4→5=30, 5→6=42
    public static int XpRequired(int level) => (level + 1) * (level + 2);

    public static LevelUpResult AddXP(PlayerProfile profile, int xpGained)
    {
        profile.xpCurrent += xpGained;
        int required = XpRequired(profile.level);

        if (profile.xpCurrent >= required)
        {
            profile.xpCurrent -= required;
            profile.level++;
            ApplyLevelBonus(profile);
            profile.xpRequired = XpRequired(profile.level);
            MarkDirty(profile);
            return new LevelUpResult { didLevelUp = true, newLevel = profile.level };
        }

        MarkDirty(profile);
        return new LevelUpResult { didLevelUp = false, newLevel = profile.level };
    }

    // +2 maxHealth every level. STR/AGI/SPD only grow via the level-up choice screen
    // (CombatResultPanel.ApplyBonus), never automatically.
    private static void ApplyLevelBonus(PlayerProfile profile)
    {
        profile.maxHealth += 2;
    }

    private static void MarkDirty(PlayerProfile profile)
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(profile);
#endif
    }
}
