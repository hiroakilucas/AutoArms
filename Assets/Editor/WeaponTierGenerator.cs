using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public static class WeaponTierGenerator
{
    private const string WeaponsFolder = "Assets/Data/Weapons";

    [MenuItem("Tools/AutoArms/Generate Weapon Tiers (T2 & T3)")]
    public static void GenerateWeaponTiers()
    {
        var t1Assets = LoadT1Assets();
        int created = 0;

        foreach (var t1 in t1Assets)
        {
            // Deriva o nome base removendo sufixo " T1" se já foi aplicado antes
            string baseName = t1.weaponName.EndsWith(" T1")
                ? t1.weaponName[..^3]
                : t1.weaponName;

            // Renomeia o asset file de "Axe.asset" para "Axe T1.asset"
            string t1Path = AssetDatabase.GetAssetPath(t1);
            if (!Path.GetFileNameWithoutExtension(t1Path).EndsWith(" T1"))
                AssetDatabase.RenameAsset(t1Path, baseName + " T1");

            t1.weaponName   = baseName + " T1";
            t1.tier         = 1;
            t1.previousTier = null;
            EditorUtility.SetDirty(t1);

            var t2 = GetOrCreateAsset(baseName + " T2", out bool t2New);
            CopyStats(baseName, t2, 2, t1);
            EditorUtility.SetDirty(t2);
            if (t2New) created++;

            var t3 = GetOrCreateAsset(baseName + " T3", out bool t3New);
            CopyStats(baseName, t3, 3, t2);
            EditorUtility.SetDirty(t3);
            if (t3New) created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WeaponTierGenerator] {created} assets criados. {t1Assets.Count} armas T1 processadas.");
    }

    [MenuItem("Tools/AutoArms/Assign All Weapon Tiers to AttackSequencer")]
    public static void AssignWeaponsToAttackSequencer()
    {
        var seq = Object.FindObjectOfType<AttackSequencer>();
        if (seq == null)
        {
            Debug.LogError("[WeaponTierGenerator] AttackSequencer não encontrado na cena. Abra 04_CombatScenePVP primeiro.");
            return;
        }

        var all = new List<WeaponData>();
        var guids = AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponsFolder });
        foreach (var guid in guids)
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
            if (w != null) all.Add(w);
        }

        // Ordena: T1 primeiro, depois T2, depois T3; dentro do mesmo tier, por nome
        all.Sort((a, b) => {
            int tierCmp = a.tier.CompareTo(b.tier);
            return tierCmp != 0 ? tierCmp : string.Compare(a.weaponName, b.weaponName);
        });

        Undo.RecordObject(seq, "Assign All Weapon Tiers");
        seq.allWeapons = all.ToArray();
        EditorUtility.SetDirty(seq);
        AssetDatabase.SaveAssets();
        Debug.Log($"[WeaponTierGenerator] {all.Count} armas atribuídas ao AttackSequencer.");
    }

    private static List<WeaponData> LoadT1Assets()
    {
        var result = new List<WeaponData>();
        var guids = AssetDatabase.FindAssets("t:WeaponData", new[] { WeaponsFolder });
        foreach (var guid in guids)
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
            if (w != null && w.tier <= 1)
                result.Add(w);
        }
        return result;
    }

    private static WeaponData GetOrCreateAsset(string assetName, out bool wasCreated)
    {
        string path = $"{WeaponsFolder}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
        if (existing != null) { wasCreated = false; return existing; }

        var w = ScriptableObject.CreateInstance<WeaponData>();
        AssetDatabase.CreateAsset(w, path);
        wasCreated = true;
        return w;
    }

    private static void CopyStats(string baseName, WeaponData dest, int tier, WeaponData prevTier)
    {
        // T1 é sempre a base de scaling: para T2 prevTier IS o T1; para T3 subimos a cadeia
        var t1 = tier == 2 ? prevTier : (prevTier.previousTier ?? prevTier);

        dest.weaponName       = $"{baseName} T{tier}";
        dest.tier             = tier;
        dest.previousTier     = prevTier;
        dest.icon             = null;
        dest.inHandSprite     = null;

        // Damage: T2 = T1 × 1.35, T3 = T1 × 1.75
        float scale = tier == 2 ? 1.35f : 1.75f;
        dest.damage = Mathf.Max(1, Mathf.RoundToInt(t1.damage * scale));

        dest.attackAnimation = t1.attackAnimation;
        dest.speedModifier   = t1.speedModifier;
        dest.types           = new List<WeaponType>(t1.types ?? new List<WeaponType>());
        dest.scale         = t1.scale;
        dest.dropOdds      = t1.dropOdds;
        dest.hitSpeed      = t1.hitSpeed;
        dest.reach         = t1.reach;
        dest.drawChance    = Mathf.Min(0.65f, t1.drawChance + (tier - 1) * 0.05f);

        // Bonuses positivos sobem +0.03 por tier step; negativos permanecem iguais
        float b = (tier - 1) * 0.03f;
        dest.critChanceBonus      = t1.critChanceBonus      > 0 ? t1.critChanceBonus + b                  : t1.critChanceBonus;
        dest.critDamageMultiplier = t1.critDamageMultiplier > 1f ? t1.critDamageMultiplier + (tier-1)*0.10f : t1.critDamageMultiplier;
        dest.evasionBonus         = t1.evasionBonus         > 0 ? t1.evasionBonus + b                     : t1.evasionBonus;
        dest.dexterityBonus       = t1.dexterityBonus       > 0 ? t1.dexterityBonus + b                   : t1.dexterityBonus;
        dest.reversalBonus        = t1.reversalBonus        > 0 ? t1.reversalBonus + b                    : t1.reversalBonus;
        dest.blockBonus           = t1.blockBonus           > 0 ? t1.blockBonus + b                       : t1.blockBonus;
        dest.accuracyBonus        = t1.accuracyBonus        > 0 ? t1.accuracyBonus + b                    : t1.accuracyBonus;
        dest.disarmBonus          = t1.disarmBonus          > 0 ? t1.disarmBonus + b                      : t1.disarmBonus;
        dest.deflectBonus         = t1.deflectBonus         > 0 ? t1.deflectBonus + b                     : t1.deflectBonus;
        // comboBonus sobe mais (+0.05) pois tem impacto maior no gameplay
        dest.comboBonus           = t1.comboBonus           > 0 ? t1.comboBonus + (tier-1)*0.05f          : t1.comboBonus;
    }
}
