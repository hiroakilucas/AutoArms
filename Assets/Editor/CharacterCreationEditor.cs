using UnityEngine;
using UnityEditor;

public static class CharacterCreationEditor
{
    // Armas iniciais padrão (Satyr1, Golem3, Succubus, Zombie) — mesmas 4 que cada
    // Loadout_<Personagem>.asset já começa com. Usado para restaurar o loadout de cada
    // personagem ao estado inicial, descartando qualquer arma ganha em level-up.
    private static readonly string[] DefaultWeaponGuids = {
        "af40afb8bb8443e4d8943e5fc48bfb25", // Satyr1 (Dagger)
        "f027d5453540da84fa7b171f2f781ee0", // Golem3 (Heavy)
        "19ea5491d0fb4ee41b2857c3f55495cd", // Succubus (Sword)
        "455a2591cb26eb94ab312d7b69629227", // Zombie (Sword)
    };

    [MenuItem("Tools/AutoArms/Reset All Profiles to Level 1")]
    public static void ResetAllProfilesToLevel1()
    {
        string[] guids = AssetDatabase.FindAssets("t:PlayerProfile",
            new[] { "Assets/ScriptableObjects/PlayerProfiles" });

        var defaultWeapons = new WeaponData[DefaultWeaponGuids.Length];
        for (int i = 0; i < DefaultWeaponGuids.Length; i++)
            defaultWeapons[i] = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(DefaultWeaponGuids[i]));

        foreach (string guid in guids)
        {
            string path    = AssetDatabase.GUIDToAssetPath(guid);
            var    profile = AssetDatabase.LoadAssetAtPath<PlayerProfile>(path);
            if (profile == null) continue;

            profile.level             = 1;
            profile.xpCurrent         = 0;
            profile.battlesRemaining  = 6;
            profile.xpRequired        = XpSystem.XpRequired(1);

            // Re-sorteia status de level 1 (mesma lógica de "Randomize Level 1 Stats").
            CharacterStats s  = CharacterCreation.GenerateLevel1Stats();
            profile.maxHealth = s.maxHealth;
            profile.str       = s.str;
            profile.agility   = s.agility;
            profile.speed     = s.speed;

            profile.skills.Clear();

            // Cada profile tem seu próprio WeaponLoadout (não é mais compartilhado entre
            // personagens) — resetar aqui não afeta os outros.
            if (profile.weaponLoadout != null)
            {
                profile.weaponLoadout.weapons = (WeaponData[])defaultWeapons.Clone();
                EditorUtility.SetDirty(profile.weaponLoadout);
            }

            EditorUtility.SetDirty(profile);
            Debug.Log($"[Debug] Profile {profile.profileName} resetado para Level 1 " +
                      $"(HP={s.maxHealth} STR={s.str} AGI={s.agility} SPD={s.speed}, skills e armas resetadas)");
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
