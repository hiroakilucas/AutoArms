using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// Espelha WeaponTierGenerator.cs, mas pra skills: popula bonusValue1..7 dos assets T1
// existentes e gera T2/T3 com os valores EXATOS de uma tabela de balanceamento fornecida
// pelo usuário (não mais escalados por multiplicador ×1.35/×1.75 — os números de cada tier
// são literais, um por skill, ver Values abaixo). usesPerFight também varia explicitamente
// por tier onde a tabela pede (ex: Thief 2x/3x/4x).
public static class SkillTierGenerator
{
    private const string SkillsFolder = "Assets/ScriptableObjects/Skills";

    // 7 valores (bonusValue1..7) por tier. Campos não usados por uma skill ficam 0 (default).
    // Ver CLAUDE.md / SKILLS_SYSTEM.md pro significado de cada slot, por skill.
    private struct SkillTierValues
    {
        public string name;
        public float[] t1, t2, t3;
        public int usesT1, usesT2, usesT3;

        public SkillTierValues(string name, float[] t1, float[] t2, float[] t3, int usesT1 = 1, int usesT2 = 1, int usesT3 = 1)
        {
            this.name = name; this.t1 = t1; this.t2 = t2; this.t3 = t3;
            this.usesT1 = usesT1; this.usesT2 = usesT2; this.usesT3 = usesT3;
        }
    }

    // Helper: monta um float[7] a partir de até 7 valores posicionais (bonusValue1..7); o
    // resto fica 0. Deixa a tabela abaixo legível (só os slots realmente usados aparecem).
    private static float[] V(params float[] vals)
    {
        var r = new float[7];
        for (int i = 0; i < vals.Length && i < 7; i++) r[i] = vals[i];
        return r;
    }

    private static readonly SkillTierValues[] Values = new SkillTierValues[]
    {
        // --- CombatPassive ---
        // Relentless: v1 = accuracy
        new SkillTierValues("Relentless",         V(0.30f), V(0.40f), V(0.50f)),
        // Counter Attack: v1 = blockBonus, v2 = reversalAfterBlock
        new SkillTierValues("Counter Attack",      V(0.10f, 0.90f), V(0.15f, 0.95f), V(0.20f, 0.99f)),
        // Sixth Sense: v1 = counter
        new SkillTierValues("Sixth Sense",         V(0.10f), V(0.15f), V(0.20f)),
        // Monk: v1 = counter, v2 = initiative (constante)
        new SkillTierValues("Monk",                V(0.40f, 200f), V(0.45f, 200f), V(0.50f, 200f)),
        // Iron Head: v1 = chance de derrubar arma do atacante
        new SkillTierValues("Iron Head",           V(0.40f), V(0.50f), V(0.60f)),
        // Shock: v1 = disarmChanceBonus
        new SkillTierValues("Shock",               V(0.50f), V(0.60f), V(0.70f)),
        // Sabotage: v1 = chance por golpe acertado
        new SkillTierValues("Sabotage",            V(0.50f), V(0.75f), V(0.90f)),
        // Saboteur: v1 = penalidade de iniciativa no oponente (novo — antes sem bonusValue)
        new SkillTierValues("Saboteur",            V(100f), V(150f), V(200f)),
        // Thief: v1 = chance/turno (constante); usos variam 2/3/4
        new SkillTierValues("Thief",               V(0.44f), V(0.44f), V(0.44f), usesT1: 2, usesT2: 3, usesT3: 4),
        // Untouchable: v1 = evasion
        new SkillTierValues("Untouchable",         V(0.30f), V(0.40f), V(0.50f)),
        // First Strike: v1 = initiative, v2 = SPD permanente (novo — slot livre antes)
        new SkillTierValues("First Strike",        V(200f, 0f), V(300f, 2f), V(500f, 4f)),
        // Determination: v1 = chance de retry
        new SkillTierValues("Determination",       V(0.60f), V(0.70f), V(0.80f)),
        // Chaining: v1 = nº de hits (constante), v2 = ações de stun (constante), v3 = comboChanceBonus (novo)
        new SkillTierValues("Chaining",            V(3f, 1f, 0f), V(3f, 1f, 0.10f), V(3f, 1f, 0.20f)),
        // Chef: v1 = % HP máx do oponente por turno (veneno)
        new SkillTierValues("Chef",                V(0.015f), V(0.03f), V(0.05f)),
        // Hostility: v1 = reversal
        new SkillTierValues("Hostility",           V(0.30f), V(0.35f), V(0.40f)),
        // Fists of Fury: v1 = comboChanceBonus
        new SkillTierValues("Fists of Fury",       V(0.20f), V(0.30f), V(0.40f)),

        // --- DefensePassive ---
        // Shield: v1 = blockBonus, v2 = penalidade de dano causado (constante), v3 sem uso
        // (chance de cair agora usa a fórmula real de DisarmChance do atacante, não um valor fixo)
        new SkillTierValues("Shield",              V(0.45f, 0.25f), V(0.50f, 0.25f), V(0.55f, 0.25f)),
        // Armour: v1 = armor, v2 = spdPct (constante)
        new SkillTierValues("Armour",              V(0.25f, 0.15f), V(0.30f, 0.15f), V(0.35f, 0.15f)),
        // Lead Skeleton: v1 = armor, v2 = evasion subtraída (constante), v3 = multiplicador de dano Heavy
        new SkillTierValues("Lead Skeleton",       V(0.15f, 0.15f, 0.85f), V(0.25f, 0.15f, 0.80f), V(0.35f, 0.15f, 0.75f)),
        // Toughened Skin: v1 = armor
        new SkillTierValues("Toughened Skin",      V(0.10f), V(0.15f), V(0.20f)),
        // Survival: v1 = evasion/block enquanto em 1 HP
        new SkillTierValues("Survival",            V(0.20f), V(0.30f), V(0.40f)),
        // Ballet Shoes: v1 = evasion
        new SkillTierValues("Ballet Shoes",        V(0.10f), V(0.15f), V(0.20f)),
        // Resistant: v1 = teto de dano (% do HP máximo)
        new SkillTierValues("Resistant",           V(0.25f), V(0.20f), V(0.17f)),
        // Sticky Hands: v1 = stickyHands
        new SkillTierValues("Sticky Hands",        V(0.50f), V(0.60f), V(0.70f)),
        // Fast Metabolism: v1 = penalidade hitSpeed (constante), v2 = penalidade crit (constante),
        // v3 = regeneração passiva %/turno, v4 = cura do burst (constante), v5 = threshold do burst (constante)
        new SkillTierValues("Fast Metabolism",     V(0.50f, 0.05f, 0.01f, 0.05f, 0.5f), V(0.50f, 0.05f, 0.02f, 0.05f, 0.5f), V(0.50f, 0.05f, 0.03f, 0.05f, 0.5f)),
        // Repulse: v1 = chance de deflect, v2 = crit bonus no deflect
        new SkillTierValues("Repulse",             V(0.30f, 0.05f), V(0.35f, 0.10f), V(0.40f, 0.15f)),

        // --- StatBoost ---
        // Vitality: v1 = hpPct, v2 = HP permanente
        new SkillTierValues("Vitality",            V(0.5f, 18f), V(0.6f, 30f), V(0.7f, 42f)),
        // Herculean Strength: v1 = strPct, v2 = STR permanente (total cumulativo por tier)
        new SkillTierValues("Herculean Strength",  V(0.5f, 3f), V(0.6f, 5f), V(0.7f, 7f)),
        // Feline Agility: v1 = agiPct, v2 = AGI permanente (total cumulativo)
        new SkillTierValues("Feline Agility",      V(0.5f, 3f), V(0.6f, 5f), V(0.7f, 7f)),
        // Lightning Bolt: v1 = spdPct, v2 = SPD permanente (total cumulativo)
        new SkillTierValues("Lightning Bolt",      V(0.5f, 3f), V(0.6f, 5f), V(0.7f, 7f)),
        // Reconnaissance: v1 = spdPct, v2 = SPD permanente, v3 = initiative (constante), v4 = critDamageBonus
        new SkillTierValues("Reconnaissance",      V(1.5f, 5f, 200f, 0.5f), V(2.0f, 10f, 200f, 0.6f), V(2.5f, 15f, 200f, 0.7f)),
        // Immortal: v1 = hpPct, v2 = penalidade str/agi/spd (constante)
        new SkillTierValues("Immortal",            V(2.5f, 0.25f), V(3.0f, 0.25f), V(3.5f, 0.25f)),
        // Deity: v1..v6 = hp/str/agi/spd/evasion/initiative pct, v7 = reversal (novo slot)
        new SkillTierValues("Deity",               V(1.0f, 1.0f, 1.0f, 0.90f, 1.0f, 200f, 0.40f),
                                                    V(1.25f, 1.25f, 1.0f, 0.90f, 1.0f, 200f, 0.50f),
                                                    V(1.5f, 1.5f, 1.0f, 0.90f, 1.0f, 200f, 0.60f)),

        // --- WeaponPassive ---
        // Weapon Master: v1 = sharpMult
        new SkillTierValues("Weapon Master",       V(1.5f), V(1.75f), V(2.0f)),
        // Martial Arts: v1 = multiplicador de dano desarmado
        new SkillTierValues("Martial Arts",        V(2.0f), V(2.5f), V(3.0f)),
        // Bodybuilder: v1 = dexterityBonus (Heavy), v2 = hitSpeedBonus (Heavy)
        new SkillTierValues("Bodybuilder",         V(0.10f, 0.40f), V(0.15f, 0.50f), V(0.20f, 0.60f)),
        // Hideaway: v1 = throwChance fixa (constante), v2 = bloqueio contra arremesso recebido
        new SkillTierValues("Hideaway",            V(0.50f, 0.25f), V(0.50f, 0.30f), V(0.50f, 0.35f)),
        // Spy: v1 = redução de dano por arma sabotada, v2 = fração das armas afetadas (constante)
        new SkillTierValues("Spy",                 V(0.20f, 0.5f), V(0.25f, 0.5f), V(0.30f, 0.5f)),
        // Garimpeiro/Magneto: ainda não implementadas — sem entrada aqui, sem T2/T3.

        // --- Super ---
        // Fierce Brute: v1 = chance (constante), v2 = multiplicador de dano (constante), v3 = crit bonus,
        // v4 = STR necessário por uso extra (constante); usos base variam 1/2/3
        new SkillTierValues("Fierce Brute",        V(0.33f, 2.0f, 0.10f, 30f), V(0.33f, 2.0f, 0.20f, 30f), V(0.33f, 2.0f, 0.30f, 30f), usesT1: 1, usesT2: 2, usesT3: 3),
        // Tragic Potion: v1 = chance, v2 = threshold HP, v3 = cura mín, v4 = cura máx (todos constantes); usos variam
        new SkillTierValues("Tragic Potion",       V(0.50f, 0.60f, 0.25f, 0.50f), V(0.50f, 0.60f, 0.25f, 0.50f), V(0.50f, 0.60f, 0.25f, 0.50f), usesT1: 1, usesT2: 2, usesT3: 3),
        // Flash Flood: v1 = chance, v2 = armas mínimas, v3 = nº de armas arremessadas (constantes); usos variam
        new SkillTierValues("Flash Flood",         V(0.17f, 3f, 3f), V(0.17f, 3f, 3f), V(0.17f, 3f, 3f), usesT1: 1, usesT2: 2, usesT3: 3),
        // Haste: v1 = chance (constante), v2 = mult. de dano/dash (constante), v3 = crit bonus; usos variam
        new SkillTierValues("Haste",               V(0.23f, 1.5f, 0.05f), V(0.23f, 1.5f, 0.10f), V(0.23f, 1.5f, 0.15f), usesT1: 1, usesT2: 2, usesT3: 3),
        // Piledriver: v1 = chance, v2 = (constante, não especificado na tabela); usos variam
        new SkillTierValues("Piledriver",          V(0.17f, 2.5f), V(0.17f, 2.5f), V(0.17f, 2.5f), usesT1: 1, usesT2: 2, usesT3: 3),
        // Net: v1 = chance (constante); usos variam
        new SkillTierValues("Net",                 V(0.50f), V(0.50f), V(0.50f), usesT1: 1, usesT2: 2, usesT3: 3),
        // Bomb: v1 = chance, v2 = dano mín, v3 = dano máx (constantes); usos variam
        new SkillTierValues("Bomb",                V(0.17f, 15f, 25f), V(0.17f, 15f, 25f), V(0.17f, 15f, 25f), usesT1: 2, usesT2: 3, usesT3: 4),
        // Vampirism: v1 = chance, v2 = threshold HP, v3 = fração (constantes); usos variam
        new SkillTierValues("Vampirism",           V(0.33f, 0.50f, 0.25f), V(0.33f, 0.50f, 0.25f), V(0.33f, 0.50f, 0.25f), usesT1: 1, usesT2: 2, usesT3: 3),
        // Mimic: v1 = chance (constante); usos variam. T3 usa lógica especial (copia histórico
        // indexado em vez de sempre o último Super usado) — ver CombatSimulator.TryActivateMimic.
        new SkillTierValues("Mimic",               V(0.25f), V(0.25f), V(0.25f), usesT1: 1, usesT2: 2, usesT3: 3),
        // Magneto: ainda não implementada — sem entrada aqui, sem T2/T3.

        // --- Pets ---
        // Hypnosis: v1 = chance, v2 = chance de sucesso (constantes); usos variam
        new SkillTierValues("Hypnosis",            V(0.38f, 0.90f), V(0.38f, 0.90f), V(0.38f, 0.90f), usesT1: 1, usesT2: 2, usesT3: 3),
        // Cry of the Damned: v1 = chance, v2 = chance de expulsão (constantes); usos variam
        // (tabela do usuário pede T1=1x — nota: default anterior era 2x, seguindo a tabela aqui)
        new SkillTierValues("Cry of the Damned",   V(0.44f, 0.50f), V(0.44f, 0.50f), V(0.44f, 0.50f), usesT1: 1, usesT2: 2, usesT3: 3),
        // Treat: mecânica não usa bonusValue ainda (ver PETS.md) — só os usos variam
        new SkillTierValues("Treat",               V(), V(), V(), usesT1: 4, usesT2: 5, usesT3: 6),
        // Tamer: fora do escopo desta tabela (não fornecida pelo usuário) — sem entrada, sem T2/T3.
    };

    [MenuItem("Tools/AutoArms/Populate Skill T1 Bonus Values")]
    public static void PopulateT1BonusValues()
    {
        int updated = 0, missing = 0;
        foreach (var v in Values)
        {
            var skill = FindSkillAsset(v.name, tier: 1);
            if (skill == null)
            {
                Debug.LogWarning($"[SkillTierGenerator] Asset T1 não encontrado para '{v.name}' — pulado.");
                missing++;
                continue;
            }

            skill.tier         = 1;
            skill.previousTier = null;
            skill.usesPerFight = v.usesT1;
            AssignValues(skill, v.t1);
            EditorUtility.SetDirty(skill);
            updated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SkillTierGenerator] {updated} assets T1 atualizados, {missing} não encontrados.");
    }

    [MenuItem("Tools/AutoArms/Generate Skill Tiers (T2 & T3)")]
    public static void GenerateSkillTiers()
    {
        int created = 0, skipped = 0;
        foreach (var v in Values)
        {
            var t1 = FindSkillAsset(v.name, tier: 1);
            if (t1 == null)
            {
                Debug.LogWarning($"[SkillTierGenerator] Asset T1 não encontrado para '{v.name}' — pulado (rode Populate Skill T1 Bonus Values primeiro).");
                skipped++;
                continue;
            }

            var t2 = GetOrCreateAsset(v.name, 2, out bool t2New);
            CopyLiteral(t1, t2, tier: 2, prevTier: t1, values: v.t2, uses: v.usesT2);
            EditorUtility.SetDirty(t2);
            if (t2New) created++;

            var t3 = GetOrCreateAsset(v.name, 3, out bool t3New);
            CopyLiteral(t1, t3, tier: 3, prevTier: t2, values: v.t3, uses: v.usesT3);
            EditorUtility.SetDirty(t3);
            if (t3New) created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SkillTierGenerator] {created} assets T2/T3 criados, {skipped} skills sem T1 puladas.");
    }

    // Escaneia Assets/ScriptableObjects/Skills/ inteira (T1+T2+T3) e sobrescreve
    // SkillDatabase.asset.skills — necessário porque SkillAssetGenerator.GenerateAll()
    // só conhece os T1 do próprio Defs[] e nunca inclui os T2/T3 gerados aqui.
    [MenuItem("Tools/AutoArms/Rebuild Skill Database From Folder")]
    public static void RebuildSkillDatabaseFromFolder()
    {
        string dbPath = $"{SkillsFolder}/SkillDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<SkillDatabase>(dbPath);
        if (db == null)
        {
            Debug.LogError($"[SkillTierGenerator] SkillDatabase.asset não encontrado em '{dbPath}' — rode Tools → AutoArms → Generate Skill Assets primeiro.");
            return;
        }

        var all = new List<SkillData>();
        var guids = AssetDatabase.FindAssets("t:SkillData", new[] { SkillsFolder });
        foreach (var guid in guids)
        {
            var s = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
            if (s != null) all.Add(s);
        }
        all.Sort((a, b) =>
        {
            int byName = string.Compare(a.skillName, b.skillName, System.StringComparison.Ordinal);
            return byName != 0 ? byName : a.tier.CompareTo(b.tier);
        });

        db.skills = all;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SkillTierGenerator] SkillDatabase.asset reconstruído: {all.Count} assets (T1+T2+T3).");
    }

    // Deleta os 3 assets órfãos da skill removida (Extra Thick Skin — T1 + os T2/T3 já criados
    // por uma execução anterior do gerador antigo) via AssetDatabase — evita deixar .meta/GUID
    // solto de uma deleção manual pelo Explorer.
    [MenuItem("Tools/AutoArms/Delete Extra Thick Skin Asset")]
    public static void DeleteExtraThickSkinAsset()
    {
        int deleted = 0;
        foreach (var suffix in new[] { "", "_t2", "_t3" })
        {
            string path = $"{SkillsFolder}/skill_extra_thick_skin{suffix}.asset";
            if (AssetDatabase.LoadAssetAtPath<SkillData>(path) == null) continue;
            if (AssetDatabase.DeleteAsset(path)) deleted++;
            else Debug.LogError($"[SkillTierGenerator] Falha ao deletar '{path}'.");
        }
        Debug.Log(deleted > 0
            ? $"[SkillTierGenerator] {deleted} asset(s) de Extra Thick Skin removido(s)."
            : "[SkillTierGenerator] Nenhum asset de Extra Thick Skin encontrado — nada a fazer.");
    }

    // Nome do asset em disco segue o padrão skill_<nome> já usado por SkillAssetGenerator — busca
    // por skillName dentro dos assets existentes em vez de recalcular o nome de arquivo, pra não
    // depender de nenhuma convenção de nome (alguns assets têm fileName != skillName, ver
    // SkillAssetGenerator.SkillDef.iconFileName).
    private static SkillData FindSkillAsset(string skillName, int tier)
    {
        var guids = AssetDatabase.FindAssets("t:SkillData", new[] { SkillsFolder });
        foreach (var guid in guids)
        {
            var s = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
            if (s != null && s.skillName == skillName && s.tier == tier) return s;
        }
        return null;
    }

    private static SkillData GetOrCreateAsset(string baseSkillName, int tier, out bool wasCreated)
    {
        string fileSafeName = baseSkillName.Replace(" ", "_").Replace("'", "").ToLowerInvariant();
        string path = $"{SkillsFolder}/skill_{fileSafeName}_t{tier}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (existing != null) { wasCreated = false; return existing; }

        var s = ScriptableObject.CreateInstance<SkillData>();
        AssetDatabase.CreateAsset(s, path);
        wasCreated = true;
        return s;
    }

    private static void AssignValues(SkillData dest, float[] values)
    {
        dest.bonusValue1 = values[0];
        dest.bonusValue2 = values[1];
        dest.bonusValue3 = values[2];
        dest.bonusValue4 = values[3];
        dest.bonusValue5 = values[4];
        dest.bonusValue6 = values[5];
        dest.bonusValue7 = values[6];
    }

    private static void CopyLiteral(SkillData t1, SkillData dest, int tier, SkillData prevTier, float[] values, int uses)
    {
        dest.skillName      = t1.skillName;
        // Mesmo texto do T1, sem sufixo de tier (o popup de detalhe já indica o tier só pela
        // borda colorida do ícone — ver CharacterPanel.ShowSkillDetail). Antes concatenava
        // " (T{tier})" na description, violando essa regra pros assets T2/T3.
        dest.description      = t1.description;
        dest.effectText       = t1.effectText;
        dest.category        = t1.category;
        dest.activationType  = t1.activationType;
        dest.tier            = tier;
        dest.previousTier     = prevTier;
        // Ícone não copiado — igual às armas, T2/T3 herdam o visual do tier anterior (aqui não
        // há sprite pra herdar, então fica null; ShowLevelUpChoice/ShowAllOptionsChoice já
        // filtram `icon != null`, então T2/T3 não aparecem no level-up sem tocar nessa lógica).
        dest.icon = null;

        dest.usesPerFight = uses;
        AssignValues(dest, values);
    }
}
