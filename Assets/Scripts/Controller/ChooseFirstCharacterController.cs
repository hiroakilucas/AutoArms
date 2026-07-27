using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Cena `ChooseFirstCharacter` (2026-07-24, onboarding) — carregada por
// LoginController.OnAuthSuccessRoutine assim que uma conta loga sem NENHUM personagem no roster
// (nova ou reinstalada). Grid 1×4 horizontal com os 4 templates "normais" do jogo (Medieval
// Warrior, Medieval Warrior Girl, Citizen 1, Citizen Women 2) — tap pra destacar (mesmo padrão de
// toque de PressableCard.cs, já usado em 05_SelectOpponent/Arsenal), botão "Confirmar" separado
// chama a Cloud Function `grantStarterCharacter` (functions/src/grantStarterCharacter.ts, ver
// ARQUITETURA.md "Modelo de roster multi-personagem") — o servidor decide os stats/a 1ª skill
// (sorteada entre 2 opções, nunca escolha manual) e grava o documento; o cliente só aplica o
// resultado, mesmo princípio de CaseService/purchaseCase.
//
// Cena 100% construída via código (mesmo padrão de LoginController/ArsenalController/
// SelectOpponentController) — só precisa de Main Camera + 1 GameObject com este componente.
public class ChooseFirstCharacterController : MonoBehaviour
{
    [SerializeField] private UITheme theme;
    [SerializeField] private CharacterDatabase characterDatabase;

    private const string SelectCharacterSceneName = "02_SelectCharacter";

    // Ordem de exibição no grid — precisa bater exatamente com os nomes dos PlayerProfile.asset
    // (Assets/ScriptableObjects/PlayerProfiles/) e com STARTER_TEMPLATE_STATS em
    // functions/src/grantStarterCharacter.ts.
    private static readonly string[] TemplateNames =
    {
        "Medieval Warrior",
        "Medieval Warrior Girl",
        "Citizen 1",
        "Citizen Women 2",
    };

    private const float CardWidth = 380f;
    private const float CardHeight = 520f;
    private const float PortraitSize = 300f;
    private const float CardSpacing = 40f;
    private const float BorderThickness = 6f;

    private string _selectedTemplateName;
    private readonly Dictionary<string, Image> _cardBorders = new Dictionary<string, Image>();
    private readonly Dictionary<string, Image> _cardBackgrounds = new Dictionary<string, Image>();

    private Button _confirmButton;
    private Image _confirmButtonImage;
    private TextMeshProUGUI _confirmButtonLabel;
    private TextMeshProUGUI _statusLabel;

    private static readonly Color CardBg = new Color(0.15f, 0.10f, 0.07f, 0.98f);
    private static readonly Color CardBgPressed = new Color(0.24f, 0.17f, 0.11f, 0.98f);

    void Start()
    {
        EnsureEventSystem();
        var canvas = CreateCanvas();
        BuildTitle(canvas);
        BuildGrid(canvas);
        BuildConfirmButton(canvas);
        BuildStatusLabel(canvas);
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private GameObject CreateCanvas()
    {
        var go = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        go.AddComponent<GraphicRaycaster>();

        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = UIShapeUtil.VerticalGradient(theme.backgroundTop, theme.backgroundBottom);
        bgImg.raycastTarget = false;

        return go;
    }

    private void BuildTitle(GameObject canvas)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -60f);
        rt.sizeDelta = new Vector2(0f, 80f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "ESCOLHA SEU PRIMEIRO PERSONAGEM";
        tmp.fontSize = 42;
        tmp.color = theme.textOnLight;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private void BuildGrid(GameObject canvas)
    {
        var content = new GameObject("Grid");
        content.transform.SetParent(canvas.transform, false);
        var contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition = new Vector2(0f, 20f);
        contentRt.sizeDelta = new Vector2(TemplateNames.Length * CardWidth + (TemplateNames.Length - 1) * CardSpacing, CardHeight);

        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CardWidth, CardHeight);
        grid.spacing = new Vector2(CardSpacing, 0f);
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 1;

        foreach (var templateName in TemplateNames)
        {
            var profile = FindTemplate(templateName);
            BuildCard(content, templateName, profile);
        }
    }

    private PlayerProfile FindTemplate(string templateName)
    {
        if (characterDatabase == null || characterDatabase.unlockedCharacters == null) return null;
        foreach (var p in characterDatabase.unlockedCharacters)
            if (p != null && p.name == templateName) return p;
        return null;
    }

    private void BuildCard(GameObject parent, string templateName, PlayerProfile profile)
    {
        // Slot = a célula do GridLayoutGroup em si (tamanho fixo CardWidth×CardHeight). A borda de
        // seleção (2026-07-24) precisa desenhar ATRÁS do card de verdade, não na frente — um
        // Image.Sliced de UIShapeUtil.RoundedRect é PREENCHIDO (não um anel oco), então colocá-lo
        // como filho do próprio card (na frente do fundo dele, mas atrás de portrait/nome) cobriria
        // o card inteiro com uma cor sólida em vez de aparecer só como uma moldura fina. Fix: a
        // borda é filha do SLOT, adicionada ANTES do card (desenha embaixo); o card (mesmo tamanho
        // do slot, sem a folga de BorderThickness) é adicionado DEPOIS, cobrindo o centro da borda
        // por cima — só a margem que sobra pra fora do card (BorderThickness) fica visível, como
        // um anel fino ao redor.
        var slot = new GameObject($"Slot_{templateName}");
        slot.transform.SetParent(parent.transform, false);
        slot.AddComponent<RectTransform>().sizeDelta = new Vector2(CardWidth, CardHeight);

        var borderGo = new GameObject("Border");
        borderGo.transform.SetParent(slot.transform, false);
        var borderRt = borderGo.AddComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(-BorderThickness, -BorderThickness);
        borderRt.offsetMax = new Vector2(BorderThickness, BorderThickness);
        var borderImg = borderGo.AddComponent<Image>();
        borderImg.sprite = UIShapeUtil.RoundedRect(theme.primaryAction, 20f);
        borderImg.type = Image.Type.Sliced;
        borderImg.enabled = false;
        _cardBorders[templateName] = borderImg;

        var card = new GameObject($"Card_{templateName}");
        card.transform.SetParent(slot.transform, false);
        var rt = card.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = UIShapeUtil.RoundedRect(CardBg, 16f);
        cardImg.type = Image.Type.Sliced;
        _cardBackgrounds[templateName] = cardImg;

        // Tap pra destacar (não confirma na hora) — mesmo componente de toque usado em
        // 05_SelectOpponent (escurece no toque, ação real em OnPointerClick pra não disparar num
        // arraste), só que aqui `onChosen` marca seleção em vez de agir direto; "Confirmar" é um
        // botão separado (padrão "tap pra destacar, botão confirma" já usado nos painéis de
        // detalhe do jogo).
        var pressable = card.AddComponent<PressableCard>();
        pressable.targetImage = cardImg;
        pressable.normalColor = CardBg;
        pressable.pressedColor = CardBgPressed;
        pressable.onChosen = () => SelectTemplate(templateName);

        var portraitGo = new GameObject("Portrait");
        portraitGo.transform.SetParent(card.transform, false);
        var portraitRt = portraitGo.AddComponent<RectTransform>();
        portraitRt.anchorMin = portraitRt.anchorMax = new Vector2(0.5f, 1f);
        portraitRt.pivot = new Vector2(0.5f, 1f);
        portraitRt.sizeDelta = new Vector2(PortraitSize, PortraitSize);
        portraitRt.anchoredPosition = new Vector2(0f, -30f);
        var portraitImg = portraitGo.AddComponent<Image>();
        portraitImg.raycastTarget = false;
        if (profile != null && profile.previewIcon != null)
        {
            portraitImg.sprite = profile.previewIcon;
            portraitImg.preserveAspect = true;
        }
        else
        {
            portraitImg.color = new Color(0.25f, 0.20f, 0.15f, 1f);
        }

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(card.transform, false);
        var nameRt = nameGo.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0f);
        nameRt.anchorMax = new Vector2(1f, 0f);
        nameRt.pivot = new Vector2(0.5f, 0f);
        nameRt.sizeDelta = new Vector2(0f, 60f);
        nameRt.anchoredPosition = new Vector2(0f, 24f);
        var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
        nameTxt.text = profile != null ? profile.profileName : templateName;
        nameTxt.fontSize = 24;
        nameTxt.color = theme.textOnDark;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.raycastTarget = false;
    }

    private void SelectTemplate(string templateName)
    {
        _selectedTemplateName = templateName;
        foreach (var kv in _cardBorders)
            kv.Value.enabled = kv.Key == templateName;

        _confirmButton.interactable = true;
        SetStatus(string.Empty);
    }

    private void BuildConfirmButton(GameObject canvas)
    {
        var go = new GameObject("BtnConfirmar");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 120f);
        rt.sizeDelta = new Vector2(480f, 76f);

        _confirmButtonImage = go.AddComponent<Image>();
        _confirmButtonImage.sprite = UIShapeUtil.RoundedRect(theme.primaryAction, 14f);
        _confirmButtonImage.type = Image.Type.Sliced;

        _confirmButton = go.AddComponent<Button>();
        _confirmButton.targetGraphic = _confirmButtonImage;
        _confirmButton.interactable = false;
        _confirmButton.onClick.AddListener(OnConfirmClicked);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        _confirmButtonLabel = labelGo.AddComponent<TextMeshProUGUI>();
        _confirmButtonLabel.text = "Confirmar";
        _confirmButtonLabel.fontSize = 26;
        _confirmButtonLabel.fontStyle = FontStyles.Bold;
        _confirmButtonLabel.color = theme.textOnDark;
        _confirmButtonLabel.alignment = TextAlignmentOptions.Center;
    }

    private void BuildStatusLabel(GameObject canvas)
    {
        var go = new GameObject("Status");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        rt.sizeDelta = new Vector2(800f, 50f);
        _statusLabel = go.AddComponent<TextMeshProUGUI>();
        _statusLabel.fontSize = 20;
        _statusLabel.color = theme.danger;
        _statusLabel.alignment = TextAlignmentOptions.Center;
        _statusLabel.enableWordWrapping = true;
    }

    private void SetStatus(string text)
    {
        if (_statusLabel != null) _statusLabel.text = text;
    }

    private void OnConfirmClicked()
    {
        if (string.IsNullOrEmpty(_selectedTemplateName)) return;
        StartCoroutine(GrantStarterCharacterRoutine(_selectedTemplateName));
    }

    private IEnumerator GrantStarterCharacterRoutine(string templateName)
    {
        _confirmButton.interactable = false;
        SetStatus("Concedendo personagem...");

        var task = StarterCharacterService.GrantStarterCharacterAsync(templateName);
        yield return new WaitUntil(() => task.IsCompleted);

        var result = task.Result;
        if (!result.Success)
        {
            SetStatus(MapErrorMessage(result));
            _confirmButton.interactable = true;
            yield break;
        }

        // Mesmo canal estático que CaseOpeningPopup já usa pro handoff case opening →
        // 02_SelectCharacter — a tela já sabe destacar/abrir o personagem recém-concedido sozinha
        // (CharacterSelectController.ResolvePendingCharacterSelectionAsync), sem nenhum código
        // novo do lado de lá.
        PendingCharacterSelection.PendingCharacterId = result.GrantedCharacterId;
        // Conta nova, sem nenhuma seleção anterior válida — confirma o personagem concedido
        // automaticamente em SelectedProfileHolder mesmo que o jogador feche o painel de detalhe
        // em vez de clicar "Selecionar" em 02_SelectCharacter (ver PendingCharacterSelection.cs).
        PendingCharacterSelection.AutoConfirmSelection = true;
        SceneManager.LoadScene(SelectCharacterSceneName);
    }

    private static string MapErrorMessage(StarterCharacterService.StarterGrantResult result)
    {
        if (result.ErrorCode == "failed-precondition")
            return "Esta conta já possui um personagem.";
        if (result.ErrorCode == "unauthenticated")
            return "Sessão expirada — faça login novamente.";
        return "Não foi possível conceder o personagem. Tente novamente.";
    }
}
