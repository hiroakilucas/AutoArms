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
        if (selectedProfileHolder.currentProfile == null)
        {
            Debug.LogWarning("Nenhum personagem selecionado para o combate.");
            return;
        }

        SceneManager.LoadScene("04_CombatScenePVP");
    }

    public void OnSelectCharacterButton()
    {
        SceneManager.LoadScene("02_SelectCharacter");
    }
    public void OnOptionsButton()
    {
        // Aqui voc� pode abrir um painel de configura��es
        Debug.Log("Abrir Configura��es (a implementar)");
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
