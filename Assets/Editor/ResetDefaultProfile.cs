using UnityEngine;
using UnityEditor;

public static class ResetDefaultProfile
{
    private const string ProfilePath  = "Assets/ScriptableObjects/PlayerProfiles/Medieval Warrior.asset";
    private const string HolderPath   = "Assets/Resources/SelectedProfileHolder.asset";

    [MenuItem("Tools/AutoArms/Reset Default Profile")]
    public static void Execute()
    {
        var profile = AssetDatabase.LoadAssetAtPath<PlayerProfile>(ProfilePath);
        if (profile == null)
        {
            Debug.LogError($"[ResetDefaultProfile] PlayerProfile not found at: {ProfilePath}");
            return;
        }

        var holder = AssetDatabase.LoadAssetAtPath<SelectedProfileHolder>(HolderPath);
        if (holder == null)
        {
            Debug.LogError($"[ResetDefaultProfile] SelectedProfileHolder not found at: {HolderPath}");
            return;
        }

        holder.currentProfile = profile;
        EditorUtility.SetDirty(holder);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ResetDefaultProfile] SelectedProfileHolder.currentProfile → {profile.profileName}");
    }
}
