using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string selectWeapons = "03_SelectWeapons";
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;

    private CharacterPanel _charPanel;

    public void OnCharacterButton()
    {
        if (_charPanel == null)
        {
            var go = new GameObject("CharacterPanel");
            _charPanel = go.AddComponent<CharacterPanel>();
            _charPanel.Setup(selectedProfileHolder);
        }
        _charPanel.Open();
    }

    public void OnPlayButton()
    {
        if (selectedProfileHolder.currentProfile == null) return;

        SceneManager.LoadScene("05_SelectOpponent");
    }

    public void OnSelectCharacterButton()
    {
        SceneManager.LoadScene("02_SelectCharacter");
    }
    public void OnOptionsButton()
    {
        // Aqui voc� pode abrir um painel de configura��es
    }

    public void OnQuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
