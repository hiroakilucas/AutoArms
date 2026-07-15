using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        // Restaura o save local (2026-07-14, ver LocalSaveService.cs) ANTES de qualquer leitura
        // dos campos do profile abaixo — sem isso, num build real, o CharacterPanel mostraria
        // sempre os valores do asset original (level/XP zerados de novo a cada abertura do jogo).
        if (selectedProfileHolder != null)
            LocalSaveService.ApplyIfSaved(selectedProfileHolder.currentProfile);

        // CharacterPanel é um painel único e persistente no lado direito (sem estado de
        // expandir/recolher — removido em 2026-07-07, ver CharacterPanel.cs), então é criado
        // direto aqui em vez de sob demanda no clique do botão PERSONAGEM (ver OnCharacterButton
        // abaixo).
        var go = new GameObject("CharacterPanel");
        go.AddComponent<CharacterPanel>().Setup(selectedProfileHolder, theme);

        BuildLogoutButton();
    }

    // Botão "Sair da Conta" TEMPORÁRIO (2026-07-15, pedido do usuário) — só pra testar o fluxo
    // de logout enquanto não existe uma tela de Configurações de verdade (ver OnOptionsButton
    // abaixo, ainda um stub); mover pra lá quando ela for construída. Construído via código
    // (mesmo padrão de CharacterPanel logo acima) em vez de editado na cena 01_MainMenu.unity —
    // canto superior esquerdo, mesma convenção de posição do botão "Voltar" já usada em
    // 02_SelectCharacter/03_Arsenal, só que aqui não existe nenhum "Voltar" pra colidir.
    private void BuildLogoutButton()
    {
        if (theme == null) return;

        var canvasGo = new GameObject("LogoutButtonCanvas (temp)");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var btnGo = new GameObject("BtnLogout (temp)");
        btnGo.transform.SetParent(canvasGo.transform, false);
        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(200f, 50f);
        rt.anchoredPosition = new Vector2(30f, -30f);

        var img = btnGo.AddComponent<Image>();
        img.sprite = UIShapeUtil.RoundedRect(theme.danger, 10f);
        img.type = Image.Type.Sliced;
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OnLogoutClicked);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var txt = labelGo.AddComponent<TextMeshProUGUI>();
        txt.text = "Sair da Conta";
        txt.fontSize = 18;
        txt.fontStyle = FontStyles.Bold;
        txt.color = theme.textOnDark;
        txt.alignment = TextAlignmentOptions.Center;
    }

    private void OnLogoutClicked()
    {
        // Reseta todo PlayerProfile já tocado nesta sessão do processo pro estado "de fábrica"
        // (2026-07-15, correção de isolamento entre contas) — sem isso, a próxima conta a logar
        // no MESMO processo (sem fechar o jogo) herdaria em memória o progresso que a conta que
        // acabou de sair deixou nesses ScriptableObjects, mesmo com save.json já isolado por uid
        // (ver LocalSaveService/PlayerProfileConverter). Precisa rodar ANTES do SignOut, pra
        // garantir que nenhuma tela consiga ler o estado contaminado no meio da transição.
        PlayerProfileConverter.RestoreAllPristine();
        AuthService.SignOut();
        StartCoroutine(LoadLoginSceneAsync());
    }

    private System.Collections.IEnumerator LoadLoginSceneAsync()
    {
        var op = SceneManager.LoadSceneAsync("00_Login");
        while (op != null && !op.isDone) yield return null;
    }

    public void OnCharacterButton()
    {
        SceneManager.LoadScene("02_SelectCharacter");
    }

    // Botão "Arsenal" (2026-07-14) — abre 03_Arsenal (grade de armas/skills, ver ArsenalController),
    // não confundir com "03_SelectWeapons" (campo selectWeapons acima, nunca usado em nenhum
    // método — reservado pra uma futura tela de escolha de LOADOUT pré-combate, propósito
    // diferente: montar quais armas levar pra luta, não visualizar a coleção inteira).
    public void OnArsenalButton()
    {
        SceneManager.LoadScene("03_Arsenal");
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
