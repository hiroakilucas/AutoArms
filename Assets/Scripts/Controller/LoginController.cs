using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tela 00_Login (Fatia 1, 2026-07-14; Google Sign-In na Fatia 2, 2026-07-15) - primeira cena do
// jogo (indice 0 no Build Settings), antes de 01_MainMenu. Construida 100% via codigo (mesmo
// padrao de ArsenalController/SelectOpponentController) - a cena em si so precisa de Main Camera
// + um GameObject com este componente.
//
// Fluxo: inicializa o Firebase -> se ja existe sessao em cache (SDK do Firebase Auth persiste
// isso sozinho), auto-login silencioso -> senao mostra o formulario (email/senha + Google). Não
// existe mais caminho offline/sem conta (botão "Pular" removido, 2026-07-24 — sem uma conta não
// há personagem nenhum jogável, ver onboarding abaixo) — logar (ou criar conta) é obrigatório
// pra entrar no jogo. Login bem-sucedido roda a sincronizacao com a nuvem (ver comentario mais
// abaixo) antes de seguir pro menu.
//
// Google Sign-In so funciona em builds Android/iOS de verdade (o plugin GoogleSignIn lança
// excecao em qualquer outra plataforma) - testado no Editor/Windows, o botao mostra uma
// mensagem clara em vez de travar, mas o fluxo completo (AuthService.SignInWithGoogleAsync) so
// fica validavel quando houver um build Android (Fase 7 do roadmap).
//
// Onboarding (2026-07-24): OnAuthSuccessRoutine checa se a conta possui QUALQUER personagem no
// roster (RosterService) antes de tudo o mais — se não possuir nenhum (conta nova, ou
// reinstalada sem personagem ainda), carrega a cena `ChooseFirstCharacter` em vez de continuar o
// fluxo normal (a escolha lá concede o personagem via Cloud Function `grantStarterCharacter` e
// segue pro jogo sozinha). Cobre sign-up, sign-in, Google e auto-login por igual, já que todos
// passam por este mesmo método.
//
// Sincronizacao com a nuvem (Fatia 3, 2026-07-15): todo login bem-sucedido de uma conta que JÁ
// possui personagem roda SyncCharacterRoutine antes de ir pro 01_MainMenu — compara o
// SelectedProfileHolder.currentProfile de hoje com o que existe em
// users/{uid}/characters/{characterId} no Firestore, aplica o mais recente (por
// updatedAtTicks) e garante que os dois lados fiquem consistentes.
public class LoginController : MonoBehaviour
{
    [SerializeField] private UITheme theme;
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;
    // Necessário pra EnsureValidSelectedCharacterRoutine reconstruir um personagem do roster via
    // PlayerProfileConverter.FromCharacterDTO (mesmo padrão de CharacterSelectController/
    // MainMenuController) — ver comentário completo nesse método.
    [SerializeField] private CharacterDatabase characterDatabase;

    private GameObject _loadingGo;
    private GameObject _formGo;
    private TMP_InputField _emailField;
    private TMP_InputField _passwordField;
    private TextMeshProUGUI _statusLabel;
    private Button _enterBtn;
    private Button _signUpBtn;
    private Button _googleBtn;

    void Start()
    {
        if (theme == null) { LoadMainMenu(); return; }

        EnsureEventSystem();

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        BuildBackground(canvasGo.transform);
        BuildTitle(canvasGo.transform);
        BuildLoading(canvasGo.transform);
        BuildForm(canvasGo.transform);

        ShowLoading(true);
        ShowForm(false);

        StartCoroutine(InitializeRoutine());
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private IEnumerator InitializeRoutine()
    {
        var initTask = FirebaseBootstrapper.InitializeAsync();
        yield return new WaitUntil(() => initTask.IsCompleted);

        if (!FirebaseBootstrapper.IsReady)
        {
            SetStatus($"Nao foi possivel conectar ({FirebaseBootstrapper.Error}). Use \"Pular\" pra jogar offline.");
            ShowLoading(false);
            ShowForm(true);
            yield break;
        }

        // Sessao em cache (SDK do Firebase Auth persiste sozinho, sem codigo nenhum nosso) ->
        // auto-login silencioso, sem passar pelo formulario.
        if (AuthService.IsSignedIn)
        {
            StartCoroutine(OnAuthSuccessRoutine());
            yield break;
        }

        ShowLoading(false);
        ShowForm(true);
    }

    // ── Sincronização pós-login ─────────────────────────────────────────────

    private IEnumerator OnAuthSuccessRoutine()
    {
        SetStatus("Sincronizando...");
        ShowLoading(true);
        ShowForm(false);

        // Onboarding (2026-07-24) — conta sem NENHUM personagem no roster (nova, ou reinstalada
        // sem personagem ainda) precisa escolher o 1º antes de entrar no jogo. Cobre sign-up,
        // sign-in, Google e auto-login por igual (todos passam por este método) — uma conta que já
        // possui personagem nunca vê esta tela, comportamento 100% preservado pra ela.
        var rosterTask = RosterService.ListOwnedCharacterDocsAsync(AuthService.CurrentUser.UserId);
        yield return new WaitUntil(() => rosterTask.IsCompleted);
        if (rosterTask.Result == null || rosterTask.Result.Count == 0)
        {
            yield return StartCoroutine(LoadChooseFirstCharacterAsync());
            yield break;
        }

        // Ver PlayerProfileConverter.EnsureValidSelection pro motivo completo (bug real
        // corrigido 2026-07-25) — garante que SelectedProfileHolder aponte pra um personagem que
        // esta conta REALMENTE possui antes de sincronizar/entrar no menu.
        PlayerProfileConverter.EnsureValidSelection(selectedProfileHolder, rosterTask.Result, characterDatabase);

        yield return StartCoroutine(SyncCharacterRoutine());
        yield return StartCoroutine(LoadEconomyRoutine());
        yield return StartCoroutine(LoadMainMenuAsync());
    }

    // Carrega moeda/diamante (carteira por conta) + energia (por personagem, com regeneração já
    // calculada — ver EnergyService) pra PlayerEconomyState ANTES de entrar em 01_MainMenu, pra
    // o HUD (CharacterPanel/MainMenuCharacterPreview) já nascer com os números certos, sem um
    // "pulo" de zero pro valor real logo após a cena carregar. Só roda no caminho de login de
    // verdade (não em "Pular (offline)") — sem conta não existe wallet/energia na nuvem pra ler.
    private IEnumerator LoadEconomyRoutine()
    {
        if (selectedProfileHolder == null || selectedProfileHolder.currentProfile == null) yield break;

        string uid = AuthService.CurrentUser.UserId;
        string characterId = selectedProfileHolder.currentProfile.OpponentId();
        var energySettings = Resources.Load<EnergySettings>("EnergySettings");

        var walletTask = WalletService.LoadAsync(uid);
        yield return new WaitUntil(() => walletTask.IsCompleted);
        var (coins, diamonds) = walletTask.Result;

        // Bug real corrigido (2026-07-21, reportado pelo usuário: desbloqueios/passe/progressão
        // comprados antes "não funcionavam" depois de reiniciar o app, mesmo a Loja mostrando o
        // estado certo) — ShopStateService.LoadAsync só rodava dentro de ShopController.Start(),
        // então PlayerUnlocksState/PlayerPassState/PlayerProgressionState ficavam no default
        // (tudo desligado) em qualquer combate/level-up que acontecesse sem o jogador ter reaberto
        // a Loja NESTA sessão — a Loja em si sempre mostrava certo porque ela mesma recarregava o
        // estado ao abrir, mascarando o problema. Carregado aqui (mesmo ponto de wallet/energia,
        // sempre roda antes de 01_MainMenu) pra já valer pra qualquer combate da sessão.
        var shopStateTask = ShopStateService.LoadAsync(uid);
        yield return new WaitUntil(() => shopStateTask.IsCompleted);

        int energyCurrent = energySettings != null ? energySettings.maxEnergy : 10;
        int energyMax = energySettings != null ? energySettings.maxEnergy : 10;
        if (energySettings != null)
        {
            var energyTask = EnergyService.GetOrRegenAsync(uid, characterId, energySettings);
            yield return new WaitUntil(() => energyTask.IsCompleted);
            (energyCurrent, energyMax) = energyTask.Result;
        }

        PlayerEconomyState.Set(coins, diamonds, energyCurrent, energyMax);
    }

    // Compara o personagem local (SelectedProfileHolder.currentProfile) com o que existe na
    // nuvem pra essa conta e aplica o mais recente dos dois — lógica compartilhada em
    // CloudSyncService (2026-07-15, Fatia 4 — extraída daqui pra também ser chamada sempre que o
    // jogador troca de personagem via grid/setas rápidas, não só no login). Sem personagem
    // selecionado (não deveria acontecer hoje, já que SelectedProfileHolder sempre vem com um
    // valor wireado no Inspector), não faz nada.
    private IEnumerator SyncCharacterRoutine()
    {
        if (selectedProfileHolder == null || selectedProfileHolder.currentProfile == null) yield break;

        var task = CloudSyncService.SyncCharacterAsync(selectedProfileHolder.currentProfile);
        yield return new WaitUntil(() => task.IsCompleted);
    }

    // ── Ações dos botões ────────────────────────────────────────────────────

    private void OnEnterClicked() => StartCoroutine(SignInRoutine());
    private void OnSignUpClicked() => StartCoroutine(SignUpRoutine());
    private void OnGoogleClicked() => StartCoroutine(SignInWithGoogleRoutine());

    private IEnumerator SignInWithGoogleRoutine()
    {
#if !UNITY_ANDROID && !UNITY_IOS
        SetStatus("Login com Google só funciona em builds Android/iOS por enquanto — use email/senha aqui no Editor.");
        yield break;
#else
        SetInteractable(false);
        SetStatus("Entrando com Google...");
        var task = AuthService.SignInWithGoogleAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        var (success, error) = task.Result;
        if (success) { StartCoroutine(OnAuthSuccessRoutine()); yield break; }
        SetStatus(error);
        SetInteractable(true);
#endif
    }

    private IEnumerator SignInRoutine()
    {
        if (!ValidateFields()) yield break;

        SetInteractable(false);
        SetStatus("Entrando...");
        var task = AuthService.SignInAsync(_emailField.text.Trim(), _passwordField.text);
        yield return new WaitUntil(() => task.IsCompleted);

        var (success, error) = task.Result;
        if (success) { StartCoroutine(OnAuthSuccessRoutine()); yield break; }
        SetStatus(error);
        SetInteractable(true);
    }

    private IEnumerator SignUpRoutine()
    {
        if (!ValidateFields()) yield break;

        SetInteractable(false);
        SetStatus("Criando conta...");
        var task = AuthService.SignUpAsync(_emailField.text.Trim(), _passwordField.text);
        yield return new WaitUntil(() => task.IsCompleted);

        var (success, error) = task.Result;
        if (success) { StartCoroutine(OnAuthSuccessRoutine()); yield break; }
        SetStatus(error);
        SetInteractable(true);
    }

    private bool ValidateFields()
    {
        if (string.IsNullOrEmpty(_emailField.text) || string.IsNullOrEmpty(_passwordField.text))
        {
            SetStatus("Preencha email e senha.");
            return false;
        }
        return true;
    }

    private void SetInteractable(bool value)
    {
        _enterBtn.interactable = value;
        _signUpBtn.interactable = value;
        _googleBtn.interactable = value;
        _emailField.interactable = value;
        _passwordField.interactable = value;
    }

    private void SetStatus(string text)
    {
        if (_statusLabel != null) _statusLabel.text = text;
    }

    private void LoadMainMenu() => StartCoroutine(LoadMainMenuAsync());

    private IEnumerator LoadMainMenuAsync()
    {
        var op = SceneManager.LoadSceneAsync("01_MainMenu");
        while (op != null && !op.isDone) yield return null;
    }

    private IEnumerator LoadChooseFirstCharacterAsync()
    {
        var op = SceneManager.LoadSceneAsync("ChooseFirstCharacter");
        while (op != null && !op.isDone) yield return null;
    }

    // ── Construção de UI ────────────────────────────────────────────────────

    private void BuildBackground(Transform parent)
    {
        var go = new GameObject("Background");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = UIShapeUtil.VerticalGradient(theme.backgroundTop, theme.backgroundBottom);
        img.raycastTarget = false;
    }

    private void BuildTitle(Transform parent)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(900f, 90f);
        rt.anchoredPosition = new Vector2(0f, -80f);
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = "AUTOARMS";
        txt.fontSize = 56;
        txt.fontStyle = FontStyles.Bold;
        txt.color = theme.textOnLight;
        txt.alignment = TextAlignmentOptions.Center;
    }

    private void BuildLoading(Transform parent)
    {
        _loadingGo = new GameObject("Loading");
        _loadingGo.transform.SetParent(parent, false);
        var rt = _loadingGo.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(500f, 60f);
        rt.anchoredPosition = Vector2.zero;
        var txt = _loadingGo.AddComponent<TextMeshProUGUI>();
        txt.text = "Conectando...";
        txt.fontSize = 26;
        txt.color = theme.textOnLight;
        txt.alignment = TextAlignmentOptions.Center;
    }

    private const float FieldWidth = 480f;
    private const float FieldHeight = 56f;

    private void BuildForm(Transform parent)
    {
        _formGo = new GameObject("Form");
        _formGo.transform.SetParent(parent, false);
        var rt = _formGo.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(FieldWidth, 520f);
        rt.anchoredPosition = Vector2.zero;

        _emailField = BuildInputField(_formGo.transform, "Email", false, new Vector2(0f, 160f));
        _passwordField = BuildInputField(_formGo.transform, "Senha", true, new Vector2(0f, 90f));

        _enterBtn = BuildButton(_formGo.transform, "Entrar", theme.primaryAction,
            new Vector2(0f, 10f), new Vector2(FieldWidth, FieldHeight), OnEnterClicked);
        _signUpBtn = BuildButton(_formGo.transform, "Criar Conta", theme.secondaryButton,
            new Vector2(0f, -60f), new Vector2(FieldWidth, FieldHeight), OnSignUpClicked);
        // Google (Fatia 2, 2026-07-15) — só funciona em build Android/iOS de verdade, ver
        // SignInWithGoogleRoutine.
        _googleBtn = BuildButton(_formGo.transform, "Entrar com Google", theme.secondaryButtonAlt,
            new Vector2(0f, -130f), new Vector2(FieldWidth, FieldHeight), OnGoogleClicked);

        var statusGo = new GameObject("Status");
        statusGo.transform.SetParent(_formGo.transform, false);
        var statusRt = statusGo.AddComponent<RectTransform>();
        statusRt.anchorMin = statusRt.anchorMax = new Vector2(0.5f, 0.5f);
        statusRt.sizeDelta = new Vector2(FieldWidth, 80f);
        statusRt.anchoredPosition = new Vector2(0f, -220f);
        _statusLabel = statusGo.AddComponent<TextMeshProUGUI>();
        _statusLabel.fontSize = 18;
        _statusLabel.color = theme.danger;
        _statusLabel.alignment = TextAlignmentOptions.Center;
        _statusLabel.enableWordWrapping = true;
    }

    private void ShowLoading(bool show) => _loadingGo.SetActive(show);
    private void ShowForm(bool show) => _formGo.SetActive(show);

    private Button BuildButton(GameObject go, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var img = go.AddComponent<Image>();
        img.sprite = UIShapeUtil.RoundedRect(color, 12f);
        img.type = Image.Type.Sliced;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var txt = labelGo.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 20;
        txt.fontStyle = FontStyles.Bold;
        txt.color = theme.textOnDark;
        txt.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    private Button BuildButton(Transform parent, string label, Color color, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Btn" + label);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return BuildButton(go, label, color, onClick);
    }

    // TMP_InputField construído via código - mesma hierarquia interna que o Unity gera pelo menu
    // GameObject > UI > Input Field (TextMeshPro): "Text Area" (com RectMask2D) contendo "Text" +
    // "Placeholder".
    private TMP_InputField BuildInputField(Transform parent, string placeholder, bool isPassword, Vector2 anchoredPos)
    {
        var go = new GameObject(placeholder + "Field");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(FieldWidth, FieldHeight);
        rt.anchoredPosition = anchoredPos;

        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = UIShapeUtil.RoundedRect(theme.panelBackgroundAlt, 10f);
        bgImg.type = Image.Type.Sliced;

        var inputField = go.AddComponent<TMP_InputField>();

        var textAreaGo = new GameObject("Text Area");
        textAreaGo.transform.SetParent(go.transform, false);
        var textAreaRt = textAreaGo.AddComponent<RectTransform>();
        textAreaRt.anchorMin = Vector2.zero;
        textAreaRt.anchorMax = Vector2.one;
        textAreaRt.offsetMin = new Vector2(16f, 6f);
        textAreaRt.offsetMax = new Vector2(-16f, -6f);
        textAreaGo.AddComponent<RectMask2D>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(textAreaGo.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.offsetMin = textRt.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 20;
        text.color = theme.textOnDark;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;

        var placeholderGo = new GameObject("Placeholder");
        placeholderGo.transform.SetParent(textAreaGo.transform, false);
        var placeholderRt = placeholderGo.AddComponent<RectTransform>();
        placeholderRt.anchorMin = Vector2.zero; placeholderRt.anchorMax = Vector2.one;
        placeholderRt.offsetMin = placeholderRt.offsetMax = Vector2.zero;
        var placeholderTxt = placeholderGo.AddComponent<TextMeshProUGUI>();
        placeholderTxt.text = placeholder;
        placeholderTxt.fontSize = 20;
        placeholderTxt.fontStyle = FontStyles.Italic;
        var faded = theme.textOnDark; faded.a = 0.5f;
        placeholderTxt.color = faded;
        placeholderTxt.alignment = TextAlignmentOptions.MidlineLeft;

        inputField.textViewport = textAreaRt;
        inputField.textComponent = text;
        inputField.placeholder = placeholderTxt;
        inputField.targetGraphic = bgImg;
        inputField.text = "";

        if (isPassword)
        {
            inputField.contentType = TMP_InputField.ContentType.Password;
            inputField.inputType = TMP_InputField.InputType.Password;
            inputField.ForceLabelUpdate();
        }

        return inputField;
    }
}
