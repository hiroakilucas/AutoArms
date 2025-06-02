using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string selectWeapons = "03_SelectWeapons";

    public void OnPlayButton()
    {
        if (SelectedProfileHolder.Instance?.selectedProfile == null)
        {
            Debug.LogWarning("Nenhum personagem foi selecionado.");
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
        // Aqui você pode abrir um painel de configurações
        Debug.Log("Abrir Configurações (a implementar)");
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
