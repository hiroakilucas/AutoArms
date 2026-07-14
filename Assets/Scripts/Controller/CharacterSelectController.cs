using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

// 02_SelectCharacter (2026-07-08): grid de personagens na lateral esquerda + painel de detalhe
// reaproveitando o MESMO CharacterPanel do 01_MainMenu (Status/Habilidades/Armas, com Level+XP
// embutido — showLevelXp=true), escondido fora da tela por padrão (startHidden=true; Canvas
// inteiro desativado, não só posicionado fora) e revelado com slide-in na lateral direita ao
// clicar num card habilitado. Ao selecionar, um overlay sólido (`SelectionOverlay`,
// panelBackgroundAlt) cobre a tela INTEIRA — escondendo os cards do grid por completo — com uma
// moldura de TELA CHEIA (`Frame`/`BuildFrameBorder`) atrás de tudo — preenchida pela splash art do
// personagem (`PlayerProfile.splashArt`, ver `UpdateFrameArt`, 2026-07-09) quando existir, ou o
// retângulo dourado placeholder de sempre caso contrário —, o preview "ao vivo" do personagem
// (`Portrait`/`BuildPortraitPreview`, Rect Transform próprio dentro dessa moldura) e o
// `CharacterPanel` (stats) na lateral DIREITA, sem moldura própria (removida a pedido do
// usuário). "Fechar" e "Selecionar" ficam lado a lado, baixos e compactos, logo abaixo do
// `CharacterPanel`; "Fechar" desliza o painel de volta pra fora da tela e esconde o overlay,
// revelando o grid de novo — diferente do "Voltar" fixo do canto superior esquerdo, que sai da
// cena inteira (SceneManager.LoadScene) e não depende de nenhum personagem estar selecionado.
public class CharacterSelectController : MonoBehaviour
{
    [Header("Grid")]
    public GameObject gridPanel;
    public Transform gridContent;

    [Header("Data Sources (ScriptableObjects)")]
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;
    [SerializeField] private CharacterDatabase characterDatabase;
    [SerializeField] private UITheme theme;

    private PlayerProfile selectedProfile;
    private CharacterPanel characterPanel;
    private GameObject btnSelecionarGo;
    private GameObject btnFecharGo;
    private GameObject selectionOverlayGo;
    private Image frameImage;

    // Preview "ao vivo" do personagem selecionado (2026-07-08, substitui o ícone estático
    // `previewIcon`) — Idle contínuo, reage a clique com Hurt/Slashing. Como `SelectionOverlay`
    // é um Canvas ScreenSpaceOverlay (sempre desenha na frente de QUALQUER coisa em world space,
    // câmera nenhuma consegue "furar" isso), um personagem instanciado normalmente na cena
    // ficaria escondido atrás do fundo sólido do overlay — por isso é instanciado longe do resto
    // da cena (`PreviewWorldPos`) e filmado por uma câmera ortográfica própria (fundo
    // transparente) que grava numa `RenderTexture`, exibida num `RawImage` dentro do MESMO
    // Canvas do resto da UI (por cima do overlay, como qualquer outro elemento).
    private static readonly Vector3 PreviewWorldPos = new Vector3(300f, 0f, 0f);
    private const float PreviewScaleFactor = 1f;
    private const int PreviewTextureSize = 768;

    // Enquadramento automático (2026-07-08, substitui um offset/tamanho fixos chutados) — depois
    // de instanciado, o personagem é centralizado no eixo Y da câmera de preview e o
    // orthographicSize é recalculado a partir dos bounds REAIS dos Renderers (soma de todas as
    // partes do sprite), com folga — sem isso a cabeça de personagens mais altos (a proporção
    // varia por `PlayerProfile.scale`) ficava cortada pra fora da RenderTexture (bug reportado
    // pelo usuário). Ver `FitPreviewCharacter`.
    private const float PreviewFitMargin = 1.15f; // 15% de folga acima/abaixo do personagem
    private const float MinPreviewOrthographicSize = 1.5f;

    private RawImage portraitPreviewImage;
    private GameObject previewCameraGo;
    private Camera previewCamera;
    private RenderTexture previewRenderTexture;
    private GameObject previewCharacterGo;
    private CharacterPreviewReaction previewReaction;

    // Grid reposicionado (2026-07-14, pedido do usuário) — scroll agora é VERTICAL, igual ao
    // Arsenal (03_Arsenal), com 3 CharacterCardUI por linha (FixedColumnCount, ver
    // EnsureGridLayout) em vez das 3 linhas fixas + scroll horizontal de antes. Valores de
    // posição/tamanho calibrados pelo usuário no Inspector do Scroll View e replicados aqui
    // (mesmo esquema de anchor ponto (0, 0.5)/(0, 0.5) de sempre — Pos X/Y mapeiam direto pra
    // anchoredPosition, Width/Height pra sizeDelta). CardWidth/CardHeight (CharacterCardUI)
    // reduzidos (~5/6 do tamanho antigo) pra 3 caberem dentro da largura nova: 3×500 + 2×17 =
    // 1534, cabe em 1544.65 com ~10px de folga (childAlignment=UpperCenter absorve isso).
    private const float GridLeftMargin = 175.03f;
    private const float GridPosY = -21f;
    private const float GridScrollWidth = 1544.65f;
    private const float ScrollHeight = 964f;

    // Janela vertical compartilhada por `CharacterPanel` (Root) e pelo anchor Y do `Portrait`
    // (ver `PortraitTop/Bottom` abaixo) — já não define mais uma moldura própria em volta de
    // nenhum dos dois (a moldura da esquerda virou tela cheia, a da direita foi removida).
    private const float PortraitAreaBottomFraction = 0.10f;
    private const float PortraitAreaTopFraction = 0.92f;

    // Rect Transform do `Portrait` (2026-07-08, valores ajustados manualmente pelo usuário no
    // Inspector, copiados 1:1 aqui) — mesmos campos mostrados no Inspector quando
    // `anchorMin.x == anchorMax.x` (Pos X / Width) e `anchorMin.y != anchorMax.y` (Top / Bottom):
    // Pos X=220.47, Width=440.95, Top=549.91, Bottom=-108.01. Ancoragem em si não mudou — X
    // colapsado em 0 (borda esquerda da tela), Y esticado entre
    // `PortraitAreaBottomFraction`/`TopFraction` — só a posição/tamanho dentro dessa janela.
    private const float PortraitPosX = 220.47f;
    private const float PortraitWidth = 440.95f;
    private const float PortraitTop = 549.91f;
    private const float PortraitBottom = -108.01f;

    void Start()
    {
        ApplyBackgroundGradient();
        ResizeGridPanelFullScreen();
        // Ordem importa: cada GameObject novo é anexado como o ÚLTIMO filho do Canvas
        // compartilhado (desenha por cima dos anteriores) — o overlay precisa ser construído
        // ANTES de qualquer botão/painel que deva ficar visível/clicável por cima dele, senão
        // ele os esconde (foi exatamente o bug da tira bege sem cobertura perto do botão
        // "Selecionar" — o overlay não cobria aquela área, e mesmo se cobrisse, sendo construído
        // depois teria desenhado por cima do botão). `CharacterPanel` usa seu próprio Canvas
        // ScreenSpaceOverlay internamente — sempre desenha por cima de tudo isso, então a ordem
        // de construção dele não importa pra ele mesmo, só pros elementos do Canvas normal.
        BuildSelectionOverlay();
        BuildDetailUI();
        BuildBackButton();
        PopulateCharacterGrid();
    }

    // Fundo cheio da cena (gradiente vertical UITheme.backgroundTop/backgroundBottom) — inserido
    // como primeiro filho do Canvas, atrás de tudo.
    private void ApplyBackgroundGradient()
    {
        if (theme == null || gridPanel == null) return;
        var canvasTransform = gridPanel.transform.parent;
        if (canvasTransform == null) return;

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasTransform, false);
        bgGo.transform.SetAsFirstSibling();
        var rt = bgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = bgGo.AddComponent<Image>();
        img.sprite = UIShapeUtil.VerticalGradient(theme.backgroundTop, theme.backgroundBottom);
        img.raycastTarget = false;
    }

    // O container do grid (gridPanel) só serve de "casca" pro ScrollRect/EventSystem agora — o
    // tint próprio dele (herdado da cena, cor diferente do gradiente de fundo) foi removido pra
    // não parecer uma coluna colada por cima do resto da cena (mesmo token de fundo em toda a
    // tela). Redimensionado pra tela inteira (não precisa mais reservar espaço — o grid em si
    // fica só na lateral esquerda via EnsureGridLayout).
    private void ResizeGridPanelFullScreen()
    {
        var rt = gridPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = gridPanel.GetComponent<Image>();
        if (img != null) img.color = new Color(img.color.r, img.color.g, img.color.b, 0f);
    }

    private void BuildDetailUI()
    {
        var canvasTransform = gridPanel.transform.parent;

        // NÃO parentar sob canvasTransform (bug real, 2026-07-08) — CharacterPanel cria seu
        // PRÓPRIO Canvas filho (ScreenSpaceOverlay) dentro de si mesmo; se este GameObject
        // ("panelGo") for parented sob OUTRO Canvas (o Canvas principal da cena), o Canvas
        // interno do CharacterPanel deixa de ser "raiz" (vira um canvas aninhado) — limitação
        // documentada do Unity: um canvas aninhado sempre herda o render mode do ancestral e seu
        // próprio CanvasScaler não tem efeito nenhum. O Root acabava usando o RectTransform
        // padrão (100×100, nunca redimensionado pra tela cheia) em vez do 1920×1080 esperado —
        // exatamente o "painel pequeno e fora do lugar, sobreposto ao personagem" reportado pelo
        // usuário via screenshot (a moldura dourada, construída direto no Canvas principal sem
        // essa armadilha, sempre apareceu no lugar certo). `MainMenuController.Start()` nunca
        // teve esse bug porque lá o GameObject é criado SEM PAI NENHUM (raiz da cena, Canvas
        // interno genuinamente raiz) — mesma regra seguida aqui agora.
        var panelGo = new GameObject("CharacterPanel");
        characterPanel = panelGo.AddComponent<CharacterPanel>();
        // Janela vertical do Root encolhida (`PortraitAreaBottomFraction/TopFraction`, mesmas
        // frações usadas antes pra casar com a moldura dourada — removida, ver `BuildSelectionOverlay`,
        // mas a janela em si continua útil: menor que o `RootAnchorBottom/Top` padrão, que vazava
        // pra fora da área disponível nesta tela).
        characterPanel.Setup(selectedProfileHolder, theme, showLevelXp: true, startHidden: true,
            anchorBottomOverride: PortraitAreaBottomFraction, anchorTopOverride: PortraitAreaTopFraction);

        BuildActionButtons(canvasTransform);
    }

    // Botão "Voltar" fixo no canto superior esquerdo (2026-07-08) — sempre visível desde a
    // entrada na cena, independente de ter um personagem selecionado ou não; ao contrário do
    // antigo botão "Voltar" (removido, ver CHANGELOG), este sai de fato da cena em vez de só
    // esconder o painel de detalhe — hoje é a ÚNICA forma de deixar 02_SelectCharacter sem
    // escolher um personagem.
    private void BuildBackButton()
    {
        var canvasTransform = gridPanel.transform.parent;
        var go = new GameObject("BtnVoltarFixo");
        go.transform.SetParent(canvasTransform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(160f, 44f); // altura reduzida (era 64f — "muito gordo verticalmente")
        rt.anchoredPosition = new Vector2(30f, -30f);
        BuildButtonVisual(go, "Voltar", theme.secondaryButton, OnClickBackToMenu);
    }

    // "Fechar" e "Selecionar" ficam lado a lado, na lateral DIREITA, logo abaixo do painel de
    // detalhe — dividem ao meio o mesmo span horizontal do Root do CharacterPanel (largura fixa
    // `PanelWidth` + `EdgeMargin`, ver consts públicas de CharacterPanel). Altura FIXA e compacta
    // (2026-07-08, correção — antes esticavam a janela vertical inteira até `RootAnchorBottom`,
    // ~270px de altura, "muito gordo verticalmente"); agora uma faixa baixa perto da base da
    // tela. Só aparecem junto do painel, ao clicar num personagem habilitado. "Fechar" (metade
    // esquerda) recolhe o painel/overlay e volta pro grid; "Selecionar" (metade direita) confirma
    // a escolha.
    private const float ActionButtonHeight = 56f;
    private const float ActionButtonBottomMargin = 30f;

    private void BuildActionButtons(Transform canvasTransform)
    {
        float leftEdge = -(CharacterPanel.PanelWidth + CharacterPanel.EdgeMargin);
        float rightEdge = -CharacterPanel.EdgeMargin;
        float mid = (leftEdge + rightEdge) / 2f;
        const float gap = 10f;

        var closeGo = new GameObject("BtnFecharDetalhe");
        closeGo.transform.SetParent(canvasTransform, false);
        var crt = closeGo.AddComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 0f);
        crt.offsetMin = new Vector2(leftEdge, ActionButtonBottomMargin);
        crt.offsetMax = new Vector2(mid - gap / 2f, ActionButtonBottomMargin + ActionButtonHeight);
        BuildButtonVisual(closeGo, "Fechar", theme.secondaryButton, OnClickCloseDetail);
        btnFecharGo = closeGo;
        btnFecharGo.SetActive(false);

        var selectGo = new GameObject("BtnSelecionar");
        selectGo.transform.SetParent(canvasTransform, false);
        var srt = selectGo.AddComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(1f, 0f);
        srt.offsetMin = new Vector2(mid + gap / 2f, ActionButtonBottomMargin);
        srt.offsetMax = new Vector2(rightEdge, ActionButtonBottomMargin + ActionButtonHeight);
        BuildButtonVisual(selectGo, "Selecionar", theme.primaryAction, OnClickSelect);
        btnSelecionarGo = selectGo;
        btnSelecionarGo.SetActive(false);
    }

    private void BuildButtonVisual(GameObject go, string label, Color color, UnityEngine.Events.UnityAction onClick)
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
        txt.fontSize = 22;
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 12; txt.fontSizeMax = 22;
        txt.fontStyle = FontStyles.Bold;
        txt.color = theme.textOnDark;
        txt.alignment = TextAlignmentOptions.Center;
    }

    // Ordena habilitados (isPlayable) primeiro, desabilitados depois; dentro de cada grupo,
    // favoritados primeiro, depois ordem alfabética por nome (2026-07-14, pedido do usuário —
    // era a ordem crua do CharacterDatabase). GridLayoutGroup usa FixedRowCount=3 com
    // Axis.Vertical (ver EnsureGridLayout), então o 1º da lista cai na célula de cima da 1ª
    // coluna, preenchendo de cima pra baixo antes de abrir a coluna seguinte à direita — "cima
    // pra baixo, esquerda pra direita" já é a ordem de leitura do grid sem precisar mudar o
    // layout, só a ordem da lista em si.
    public void PopulateCharacterGrid()
    {
        foreach (Transform child in gridContent)
            Destroy(child.gameObject);

        EnsureGridLayout();

        // isUnlockedForSelection (2026-07-10) filtra quem aparece no grid; isPlayable
        // (2026-07-10) decide, dentre esses, quem fica clicável/escolhível pra batalhar —
        // os dois são independentes de `SelectedProfileHolder.currentProfile` agora (antes só o
        // currentProfile ficava clicável, um beco sem saída pra escolher qualquer outro
        // personagem pela UI). Não-jogável ainda aparece no grid, só travado/cinza sem Button.
        // p == null: referência órfã (asset deletado por fora sem tirar da lista) — ignora em vez
        // de derrubar a cena inteira com NullReferenceException.
        var enabled = new List<PlayerProfile>();
        var locked = new List<PlayerProfile>();
        foreach (var p in characterDatabase.unlockedCharacters)
        {
            if (p == null || !p.isUnlockedForSelection) continue;
            (p.isPlayable ? enabled : locked).Add(p);
        }
        enabled.Sort(CharacterDatabase.ComparePlayerProfiles);
        locked.Sort(CharacterDatabase.ComparePlayerProfiles);

        var ordered = new List<PlayerProfile>(enabled);
        ordered.AddRange(locked);

        foreach (var profile in ordered)
        {
            var cardGo = new GameObject($"Card_{profile.profileName}");
            cardGo.transform.SetParent(gridContent, false);
            cardGo.AddComponent<RectTransform>();
            var card = cardGo.AddComponent<CharacterCardUI>();
            card.Setup(profile, this, theme, profile.isPlayable);
        }
    }

    // Scroll VERTICAL (2026-07-14, pedido do usuário — "igual do Arsenal") — GridLayoutGroup com
    // FixedColumnCount=3 preenche cada linha da esquerda pra direita (3 cards) antes de abrir
    // uma nova linha abaixo; revela mais personagens conforme o CharacterDatabase cresce, sem
    // ajuste manual de layout. Era FixedRowCount=3 + scroll horizontal (3 linhas fixas, colunas
    // extras à direita) — invertido a pedido do usuário. O grid fica ancorado na lateral
    // esquerda da tela (não mais centralizado), deixando o centro livre pro preview e a direita
    // livre pro painel.
    private void EnsureGridLayout()
    {
        var grid = gridContent.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CharacterCardUI.CardWidth, CharacterCardUI.CardHeight);
        grid.spacing = new Vector2(17f, 17f);
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        // Centralizado (mesmo motivo do Arsenal) — 3×500+2×17=1534 não preenche exatamente os
        // 1544.65px do Content, então UpperCenter absorve a sobra em vez de deixar um vão à
        // direita (UpperLeft).
        grid.childAlignment = TextAnchor.UpperCenter;

        // Content estica pra combinar com a largura do Viewport (mesmo padrão do Arsenal) —
        // anchorMin=(0,1)/anchorMax=(1,1)/pivot=(0.5,1)/sizeDelta.y=0, altura cresce pra baixo
        // via ContentSizeFitter conforme o nº de linhas. Substitui o esquema antigo (scroll
        // horizontal: anchorMin/Max=(0,0)-(0,1), largura controlada pelo fitter horizontal).
        var contentRt = gridContent.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 0f);
        contentRt.anchoredPosition = Vector2.zero;

        var fitter = gridContent.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = gridContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = gridContent.GetComponentInParent<ScrollRect>();
        if (scrollRect == null) return;

        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Os dois scrollbars desligados/desconectados de vez (2026-07-14 — antes só o vertical,
        // já que horizontal era o eixo ativo) — "igual do Arsenal", que não tem scrollbar
        // visível nenhum, só arraste. `vertical=true`/`horizontal=false` já bastam pra travar os
        // eixos errados, isso aqui é só pra não sobrar uma barra inativa visível na tela.
        if (scrollRect.verticalScrollbar != null)
        {
            scrollRect.verticalScrollbar.gameObject.SetActive(false);
            scrollRect.verticalScrollbar = null;
        }
        if (scrollRect.horizontalScrollbar != null)
        {
            scrollRect.horizontalScrollbar.gameObject.SetActive(false);
            scrollRect.horizontalScrollbar = null;
        }

        var scrollRt = scrollRect.GetComponent<RectTransform>();
        scrollRt.anchorMin = scrollRt.anchorMax = new Vector2(0f, 0.5f);
        scrollRt.pivot = new Vector2(0f, 0.5f);
        scrollRt.sizeDelta = new Vector2(GridScrollWidth, ScrollHeight);
        scrollRt.anchoredPosition = new Vector2(GridLeftMargin, GridPosY);
    }

    // Container/overlay que envolve o painel de detalhe (2026-07-08, correção — 2ª rodada: a 1ª
    // versão só cobria de x=0 até o início do `CharacterPanel`, deixando uma tira bege sem
    // cobertura na faixa do próprio `CharacterPanel` que não é ocupada pelo Root dele — inclusive
    // atrás do botão "Selecionar") — agora cobre a TELA INTEIRA (x=0 até a borda direita, altura
    // inteira) com o fundo sólido `UITheme.panelBackgroundAlt`, escondendo os cards do grid por
    // completo enquanto o painel está aberto. Escondido por padrão (`SetActive(false)`), ligado
    // junto do painel em `OnCharacterSelected`. Construído ANTES de `CharacterPanel`/botões (ver
    // `Start()`) pra ficar sempre atrás deles no Canvas compartilhado.
    private void BuildSelectionOverlay()
    {
        var canvasTransform = gridPanel.transform.parent;

        selectionOverlayGo = new GameObject("SelectionOverlay");
        selectionOverlayGo.transform.SetParent(canvasTransform, false);
        var rt = selectionOverlayGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = selectionOverlayGo.AddComponent<Image>();
        img.color = theme.panelBackgroundAlt;

        // Moldura dourada da direita (ao redor do CharacterPanel) removida (2026-07-08, pedido
        // do usuário) — CharacterPanel continua exatamente onde estava, só sem a borda decorativa
        // atrás dele.

        // Moldura dourada da esquerda agora cobre a TELA INTEIRA (2026-07-08, pedido do usuário
        // — placeholder até uma imagem gerada por IA preencher esse quadro, ver CHANGELOG) — não
        // é mais recortada pelas frações do portrait; `Portrait` (preview do personagem) ganhou
        // seu próprio Rect Transform independente, posicionado dentro dela via
        // `BuildPortraitPreview`.
        frameImage = BuildFrameBorder(
            selectionOverlayGo.transform,
            anchorMinX: 0f, anchorMaxX: 1f,
            anchorMinY: 0f, anchorMaxY: 1f,
            offsetMinX: 0f, offsetMaxX: 0f,
            offsetMinY: 0f, offsetMaxY: 0f);

        BuildPortraitPreview(selectionOverlayGo.transform);

        selectionOverlayGo.SetActive(false);
    }

    // Câmera dedicada (posicionada longe do resto da cena, `PreviewWorldPos`) que filma o
    // personagem instanciado por `SpawnCharacterPreview` com fundo transparente
    // (`CameraClearFlags.SolidColor`, alpha 0) numa `RenderTexture` — exibida no `RawImage`
    // abaixo, dentro do Canvas normal da UI. `RawImage` já tem um `Button` próprio (clique reage
    // com Hurt/Slashing, ver `OnPortraitClicked`), então não precisa de raycast físico/collider
    // nenhum no personagem em si — o clique é 100% UI, igual a qualquer outro botão da tela.
    private void BuildPortraitPreview(Transform overlayParent)
    {
        previewRenderTexture = new RenderTexture(PreviewTextureSize, PreviewTextureSize, 16, RenderTextureFormat.ARGB32);

        var camGo = new GameObject("CharacterPreviewCamera");
        camGo.transform.position = new Vector3(PreviewWorldPos.x, PreviewWorldPos.y, -10f);
        previewCamera = camGo.AddComponent<Camera>();
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = MinPreviewOrthographicSize; // provisório — FitPreviewCharacter recalcula a cada SpawnCharacterPreview
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.targetTexture = previewRenderTexture;
        previewCamera.depth = -10;
        previewCameraGo = camGo;

        var portraitGo = new GameObject("Portrait");
        portraitGo.transform.SetParent(overlayParent, false);
        var prt = portraitGo.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(0f, PortraitAreaBottomFraction);
        prt.anchorMax = new Vector2(0f, PortraitAreaTopFraction);
        prt.offsetMin = new Vector2(PortraitPosX - PortraitWidth / 2f, PortraitBottom);
        prt.offsetMax = new Vector2(PortraitPosX + PortraitWidth / 2f, -PortraitTop);
        portraitPreviewImage = portraitGo.AddComponent<RawImage>();
        portraitPreviewImage.texture = previewRenderTexture;

        var btn = portraitGo.AddComponent<Button>();
        btn.targetGraphic = portraitPreviewImage;
        btn.onClick.AddListener(OnPortraitClicked);
    }

    // Mesma técnica de `MainMenuCharacterPreview`: instancia o prefab, remove os componentes de
    // combate (`PlayerCombat`/`WeaponHandler`/`MovementController`) e deixa só `Animator`/
    // `AnimationController` em Idle — `AnimationController` continua vivo aqui (diferente do
    // menu principal antes desta sessão) porque a reação de clique precisa dele pra disparar
    // Hurt/Slashing (`CharacterPreviewReaction`, componente compartilhado com o menu principal).
    private void SpawnCharacterPreview(PlayerProfile profile)
    {
        if (previewCharacterGo != null) DestroyImmediate(previewCharacterGo);
        previewReaction = null;
        if (profile == null || profile.characterPrefab == null) return;

        previewCharacterGo = Instantiate(profile.characterPrefab, PreviewWorldPos, Quaternion.identity);
        previewCharacterGo.transform.localScale = profile.scale * PreviewScaleFactor;

        DestroyImmediate(previewCharacterGo.GetComponent<PlayerCombat>());
        DestroyImmediate(previewCharacterGo.GetComponent<WeaponHandler>());
        DestroyImmediate(previewCharacterGo.GetComponent<MovementController>());

        var animController = previewCharacterGo.GetComponent<AnimationController>();
        if (animController != null) animController.SetIdle(true);

        previewReaction = previewCharacterGo.AddComponent<CharacterPreviewReaction>();
        previewReaction.Init(animController);

        FitPreviewCharacter();
    }

    // Centraliza o personagem instanciado no eixo Y da câmera de preview e recalcula o
    // `orthographicSize` a partir dos bounds REAIS dos Renderers (soma de todas as partes do
    // sprite, mesma técnica de `MainMenuCharacterPreview.BuildClickReaction`) — antes usava um
    // offset/tamanho fixos chutados, que cortava a cabeça de personagens com proporção mais alta
    // (`PlayerProfile.scale` varia por personagem). Funciona pra qualquer personagem, sem precisar
    // de calibração manual por asset.
    private void FitPreviewCharacter()
    {
        if (previewCharacterGo == null || previewCamera == null) return;

        var renderers = previewCharacterGo.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        foreach (var r in renderers) combined.Encapsulate(r.bounds);

        float verticalOffset = PreviewWorldPos.y - combined.center.y;
        previewCharacterGo.transform.position += new Vector3(0f, verticalOffset, 0f);

        float halfHeight = combined.size.y / 2f;
        previewCamera.orthographicSize = Mathf.Max(halfHeight * PreviewFitMargin, MinPreviewOrthographicSize);
    }

    // Encaminha o clique do `Button` de UI (`RawImage`, ver `BuildPortraitPreview`) pro
    // `CharacterPreviewReaction` do personagem atualmente instanciado — indireto porque o Button
    // é construído uma única vez em `Start()`, antes de qualquer personagem existir.
    private void OnPortraitClicked()
    {
        if (previewReaction != null) previewReaction.TriggerReaction();
    }

    // Moldura genérica (retângulo arredondado na cor de destaque `currencyGold` por padrão) —
    // hoje só usada pela moldura de tela cheia da esquerda; mantida genérica (aceita qualquer
    // anchor/offset) caso outra moldura seja necessária no futuro. Retorna o `Image` pra quem
    // construiu poder trocar o conteúdo depois (ver `UpdateFrameArt`, que substitui esse
    // placeholder pela splash art do personagem selecionado quando `PlayerProfile.splashArt`
    // estiver preenchido).
    private Image BuildFrameBorder(Transform parent, float anchorMinX, float anchorMaxX,
        float anchorMinY, float anchorMaxY, float offsetMinX, float offsetMaxX,
        float offsetMinY, float offsetMaxY)
    {
        var frameGo = new GameObject("Frame");
        frameGo.transform.SetParent(parent, false);
        var rt = frameGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorMinX, anchorMinY);
        rt.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
        rt.offsetMin = new Vector2(offsetMinX, offsetMinY);
        rt.offsetMax = new Vector2(offsetMaxX, offsetMaxY);
        var img = frameGo.AddComponent<Image>();
        img.sprite = UIShapeUtil.RoundedRect(theme.currencyGold, 20f);
        img.type = Image.Type.Sliced;
        return img;
    }

    // Preenche o `Frame` com a splash art do personagem selecionado (`PlayerProfile.splashArt`,
    // arrastada manualmente no Inspector conforme cada arte é gerada — ver `Assets/Personagens/
    // SplashArt/`), esticada pra cobrir o frame inteiro (`Type.Simple` + `preserveAspect=false`).
    // Sem splash art configurada: volta pro retângulo dourado placeholder de sempre.
    private void UpdateFrameArt(PlayerProfile profile)
    {
        if (frameImage == null) return;

        if (profile != null && profile.splashArt != null)
        {
            frameImage.sprite = profile.splashArt;
            frameImage.type = Image.Type.Simple;
            frameImage.preserveAspect = false;
        }
        else
        {
            frameImage.sprite = UIShapeUtil.RoundedRect(theme.currencyGold, 20f);
            frameImage.type = Image.Type.Sliced;
            frameImage.preserveAspect = false;
        }
    }

    // Só chamado por cards habilitados (CharacterCardUI só adiciona Button quando isEnabled) —
    // cards bloqueados nunca chegam aqui.
    public void OnCharacterSelected(PlayerProfile profile)
    {
        selectedProfile = profile;
        UpdateFrameArt(profile);
        characterPanel.SetProfile(profile);
        // Painel já abre expandido (Habilidades/Armas visíveis), não no estado Compact de sempre
        // — pedido do usuário, pra não precisar de um clique extra dentro do painel só pra ver o
        // conteúdo. Chamado ANTES de ShowSlideIn() de propósito: Expand() usa StopAllCoroutines()
        // internamente, o que cortaria a animação de slide-in pela metade se rodasse depois dela
        // (StopAllCoroutines para TUDO que estiver rodando neste componente, sem distinguir qual
        // coroutine é qual). Chamando antes, não há nenhuma coroutine de slide ainda em andamento
        // pra cortar. `Expand()` é idempotente (`if (_isExpanded) return;`) — reabrir um personagem
        // diferente depois de já ter expandido uma vez não reinicia a animação à toa.
        characterPanel.Expand();
        characterPanel.ShowSlideIn();
        selectionOverlayGo.SetActive(true);
        if (previewCameraGo != null) previewCameraGo.SetActive(true);
        SpawnCharacterPreview(profile);
        btnSelecionarGo.SetActive(true);
        btnFecharGo.SetActive(true);
    }

    // "Fechar" (dentro da visualização expandida, diferente do "Voltar" fixo do canto superior
    // esquerdo que sai da cena) — desliza o painel de volta pra fora da tela (mesma animação de
    // `ShowSlideIn`, invertida) e some com o overlay/portrait/botões só depois que o slide
    // terminar, revelando o grid de novo. Não mexe em `SelectedProfileHolder`/`selectedProfile`.
    public void OnClickCloseDetail()
    {
        const float duration = 0.3f;
        characterPanel.HideSlideOut(duration);
        btnSelecionarGo.SetActive(false);
        btnFecharGo.SetActive(false);
        StartCoroutine(HideOverlayAfterDelay(duration));
    }

    private IEnumerator HideOverlayAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        selectionOverlayGo.SetActive(false);
        if (previewCameraGo != null) previewCameraGo.SetActive(false);
    }

    public void OnClickBackToMenu()
    {
        SceneManager.LoadScene("01_MainMenu");
    }

    public void OnClickSelect()
    {
        if (selectedProfile == null) return;
        selectedProfileHolder.currentProfile = selectedProfile;
        SceneManager.LoadScene("01_MainMenu");
    }
}
