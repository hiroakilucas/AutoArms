using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    // Nome exato da cena de combate (confira em Build Settings)
    [SerializeField] private string combatSceneName = "CombatScene";

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
