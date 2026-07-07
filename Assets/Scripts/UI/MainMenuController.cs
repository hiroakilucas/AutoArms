using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string selectWeapons = "03_SelectWeapons";
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;
    [SerializeField] private UITheme theme;

    // Exposto pra MainMenuCharacterPreview conseguir buscar o UITheme via FindObjectOfType,
    // sem precisar de um campo [SerializeField] próprio — ver comentário em
    // MainMenuCharacterPreview.ResolveTheme() pra motivo (campo próprio já quebrou 2x, sempre
    // resolvendo nulo em runtime apesar do asset estar wireado corretamente no arquivo de cena).
    public UITheme Theme => theme;

    void Start()
    {
        // CharacterPanel é um painel único e persistente no lado direito (sem estado de
        // expandir/recolher — removido em 2026-07-07, ver CharacterPanel.cs), então é criado
        // direto aqui em vez de sob demanda no clique do botão PERSONAGEM (ver OnCharacterButton
        // abaixo).
        var go = new GameObject("CharacterPanel");
        go.AddComponent<CharacterPanel>().Setup(selectedProfileHolder, theme);
    }

    public void OnCharacterButton()
    {
        // Pendente de nova definição — antes abria um painel lateral com abas Stats/Skills/
        // Armas; esse painel expandido foi removido (2026-07-07, ver CharacterPanel.cs) e o
        // que restou (CharacterPanel) já fica sempre visível sozinho, sem depender de clique.
        // Este botão não tem ação própria até uma nova função ser decidida.
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
