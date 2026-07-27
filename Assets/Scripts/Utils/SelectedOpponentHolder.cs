using UnityEngine;

[CreateAssetMenu(fileName = "SelectedOpponentHolder", menuName = "Game/Selected Opponent Holder", order = 102)]
public class SelectedOpponentHolder : ScriptableObject
{
    public PlayerProfile currentOpponentProfile;

    // Mesma proteção de SelectedProfileHolder.OnEnable (2026-07-25, ver comentário lá pro bug
    // real) — asset em Assets/Resources/ referenciado só por algumas cenas; sem isto, uma troca
    // de cena pra uma tela que não referencia este holder pode descarregá-lo via
    // Resources.UnloadUnusedAssets (implícito em SceneManager.LoadScene) e perder
    // currentOpponentProfile setado em runtime.
    private void OnEnable()
    {
        hideFlags |= HideFlags.DontUnloadUnusedAsset;
    }

    public void SetOpponent(PlayerProfile profile)
    {
        currentOpponentProfile = profile;
    }

    public PlayerProfile GetOpponent()
    {
        return currentOpponentProfile;
    }
}
