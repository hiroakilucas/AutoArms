using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class WeaponRenamer
{
    private const string WeaponsFolder = "Assets/Data/Weapons";

    private static readonly (string oldName, string newName)[] Renames =
    {
        ("Mammoth Bone", "Bone"),
        ("Mug",          "Bottle"),
        ("Pio Pio",      "Boomerang"),
        ("Halberd",      "Reaper"),
        ("Noodle Bowl",  "Bow"),
        ("Leek",         "Branch"),
        ("Trombone",     "Anchor"),
        ("Keyboard",     "Book"),
    };

    [MenuItem("Tools/AutoArms/Rename 8 Weapons (Bone/Bottle/Boomerang/Reaper/Bow/Branch/Anchor/Book)")]
    public static void RenameWeapons()
    {
        int renamed = 0;

        foreach (var (oldName, newName) in Renames)
        {
            foreach (var tier in new[] { "T1", "T2", "T3" })
            {
                string oldAssetName = $"{oldName} {tier}";
                string path = $"{WeaponsFolder}/{oldAssetName}.asset";
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(path);

                if (weapon == null)
                {
                    Debug.LogWarning($"[WeaponRenamer] Não encontrado: {path}");
                    continue;
                }

                string newAssetName = $"{newName} {tier}";
                string error = AssetDatabase.RenameAsset(path, newAssetName);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[WeaponRenamer] Falha ao renomear {path}: {error}");
                    continue;
                }

                weapon.weaponName = newAssetName;
                EditorUtility.SetDirty(weapon);
                renamed++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WeaponRenamer] {renamed}/24 assets renomeados.");

        VerifyAttackSequencerReferences();
    }

    private static void VerifyAttackSequencerReferences()
    {
        var seq = Object.FindObjectOfType<AttackSequencer>();
        if (seq == null)
        {
            Debug.LogWarning("[WeaponRenamer] AttackSequencer não encontrado na cena aberta — abra 04_CombatScenePVP e rode novamente para verificar allWeapons.");
            return;
        }

        if (seq.allWeapons == null)
        {
            Debug.LogWarning("[WeaponRenamer] AttackSequencer.allWeapons está vazio.");
            return;
        }

        int nullCount = 0;
        for (int i = 0; i < seq.allWeapons.Length; i++)
        {
            if (seq.allWeapons[i] == null)
            {
                nullCount++;
                Debug.LogError($"[WeaponRenamer] AttackSequencer.allWeapons[{i}] está com referência quebrada (null).");
            }
        }

        if (nullCount == 0)
            Debug.Log($"[WeaponRenamer] AttackSequencer.allWeapons OK — {seq.allWeapons.Length} referências, nenhuma quebrada.");
        else
            Debug.LogError($"[WeaponRenamer] {nullCount} referência(s) quebrada(s) em AttackSequencer.allWeapons — reassocie manualmente no Inspector.");
    }
}
