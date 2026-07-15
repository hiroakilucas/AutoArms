using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Game/Skill Database", order = 102)]
public class SkillDatabase : ScriptableObject
{
    public List<SkillData> skills = new List<SkillData>();

    // Acha a skill exata (T1/T2/T3) a partir do nome e do tier desejado (2026-07-14) — usado pela
    // camada de save (LocalSaveService/PlayerProfileConverter). Diferente de WeaponData, o
    // `SkillData.skillName` é IDÊNTICO em todos os tiers da mesma skill (SkillTierGenerator copia
    // verbatim T1→T2/T3, sem sufixo — ver SkillTierGenerator.CopyLiteral) e `skills` já lista
    // TODAS as entradas (todos os tiers de todas as skills, não só T1) — por isso é busca direta
    // por nome+tier, sem precisar andar cadeia nenhuma (mesmo padrão de
    // SkillTierGenerator.FindSkillAsset, que é editor-only/AssetDatabase; este aqui funciona em
    // runtime).
    public SkillData FindByFamilyNameAndTier(string skillName, int tier)
    {
        if (string.IsNullOrEmpty(skillName)) return null;
        foreach (var s in skills)
            if (s != null && s.skillName == skillName && s.tier == tier) return s;
        return null;
    }
}
