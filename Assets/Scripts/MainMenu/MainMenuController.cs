using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string selectCharacter = "02_SelectCharacter";
    [SerializeField] private string selectWeapons = "03_SelectWeapons";
    [SerializeField] private string combatSceneName = "04_CombatScenePVP";

    public void OnPlayButton()
    {
        SceneManager.LoadScene(combatSceneName);
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
