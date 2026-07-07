using UnityEngine;

[CreateAssetMenu(fileName = "SelectedOpponentHolder", menuName = "Game/Selected Opponent Holder", order = 102)]
public class SelectedOpponentHolder : ScriptableObject
{
    public PlayerProfile currentOpponentProfile;

    public void SetOpponent(PlayerProfile profile)
    {
        currentOpponentProfile = profile;
    }

    public PlayerProfile GetOpponent()
    {
        return currentOpponentProfile;
    }
}
