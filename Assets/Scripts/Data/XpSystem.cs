public static class XpSystem
{
    public struct LevelUpResult
    {
        public bool didLevelUp;
        public int  newLevel;
    }

    // Tabela pedida pelo usuário: 1→2=5, 2→3=6, 3→4=7, 4→5=8, 5→6=9, 6→7=10 (+1 por nível),
    // depois 7→8=12, 8→9=14, 9→10=16, 10→11=18, 11→12=20, 12→13=22... (+2 por nível a partir
    // do nível 7, sem teto — continua +2 indefinidamente). Substituiu (level+1)*(level+2).
    public static int XpRequired(int level) => level <= 6 ? level + 4 : 2 * level - 2;

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

    // So marca dirty no Editor - NAO chama LocalSaveService.Save aqui (removido 2026-07-15, bug
    // real reportado pelo usuario). AddXP roda ANTES do jogador escolher o bonus de level-up
    // (CombatResultPanel.ApplyBonus, que so acontece depois de AttackSequencer.OnCombatEnd
    // mostrar o painel) - se este metodo salvasse sozinho toda vez que e chamado, capturaria um
    // profile com XP/level ja atualizados mas str/agility/speed ainda SEM o bonus da escolha
    // pendente (save prematuro/incompleto). Quem decide QUANDO salvar de verdade agora e o
    // chamador (AttackSequencer.OnCombatEnd): salva na hora se nao houve level-up (nada mais vai
    // mudar), ou deixa pra CombatResultPanel.ApplyBonus salvar depois da escolha, se houve.
    private static void MarkDirty(PlayerProfile profile)
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(profile);
#endif
    }
}
