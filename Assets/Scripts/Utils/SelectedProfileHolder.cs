using UnityEngine;

[CreateAssetMenu(fileName = "SelectedProfileHolder", menuName = "Game/Selected Profile Holder", order = 101)]
public class SelectedProfileHolder : ScriptableObject
{
    public PlayerProfile currentProfile;

    public void SetProfile(PlayerProfile profile)
    {
        currentProfile = profile;
    }

    public PlayerProfile GetProfile()
    {
        return currentProfile;
    }
}
