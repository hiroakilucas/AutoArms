using UnityEngine;
using UnityEditor;

public static class CharacterCreationEditor
{
    [MenuItem("Tools/AutoArms/Reset All Profiles to Level 1")]
    public static void ResetAllProfilesToLevel1()
    {
        string[] guids = AssetDatabase.FindAssets("t:PlayerProfile",
            new[] { "Assets/ScriptableObjects/PlayerProfiles" });

        foreach (string guid in guids)
        {
            string path    = AssetDatabase.GUIDToAssetPath(guid);
            var    profile = AssetDatabase.LoadAssetAtPath<PlayerProfile>(path);
            if (profile == null) continue;

            profile.level             = 1;
            profile.xpCurrent         = 0;
            profile.battlesRemaining  = 6;
            profile.xpRequired        = XpSystem.XpRequired(1);
            EditorUtility.SetDirty(profile);
            Debug.Log($"[Debug] Profile {profile.profileName} resetado para Level 1");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Debug] Todos os profiles resetados para Level 1.");
    }

    [MenuItem("Tools/AutoArms/Randomize Level 1 Stats")]
    public static void RandomizeAllProfiles()
    {
        string[] guids = AssetDatabase.FindAssets("t:PlayerProfile",
            new[] { "Assets/ScriptableObjects/PlayerProfiles" });

        int count = 0;
        foreach (string guid in guids)
        {
            string path    = AssetDatabase.GUIDToAssetPath(guid);
            var    profile = AssetDatabase.LoadAssetAtPath<PlayerProfile>(path);
            if (profile == null || profile.level != 1) continue;

            CharacterStats s = CharacterCreation.GenerateLevel1Stats();
            profile.maxHealth = s.maxHealth;
            profile.str       = s.str;
            profile.agility   = s.agility;
            profile.speed     = s.speed;
            EditorUtility.SetDirty(profile);
            count++;
            Debug.Log($"[CharacterCreation] {profile.profileName}: " +
                      $"HP={s.maxHealth} STR={s.str} AGI={s.agility} SPD={s.speed}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CharacterCreation] Randomized {count} level-1 profile(s).");
    }
}
