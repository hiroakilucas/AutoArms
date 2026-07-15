using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuCharacterPreview : MonoBehaviour
{
    public Transform spawnPoint;
    private GameObject currentCharacter;
    private GameObject levelXpHudGo;
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;

    // Troca rápida de personagem (2026-07-14, pedido do usuário) — setas laterais + arrastar o
    // personagem central. Lista/ordem compartilhada com o grid de 02_SelectCharacter
    // (CharacterDatabase.GetPlayableCharactersOrdered, favoritado primeiro depois alfabético),
    // pra não ter uma ordem diferente em cada tela.
    [SerializeField] private CharacterDatabase characterDatabase;
    private List<PlayerProfile> _orderedProfiles;
    private int _currentIndex;

    // Reduz o personagem central levemente — o root dos prefabs de personagem fica nos pés
    // (mesmo pressuposto de CombatSceneLoader/RandomSpawnPosition), então escalar em torno da
    // própria transform mantém os pés no lugar sem precisar ajustar a posição.
    private const float PreviewScaleFactor = 0.82f;

    // Centralizado no centro ABSOLUTO da tela (2026-07-07, revertida a decisão anterior de
    // deslocar pra esquerda do CharacterPanel) — câmera ortográfica da cena tem X=0 (ver
    // "Main Camera" em 01_MainMenu.unity), então X=0 aqui cai exatamente no centro horizontal
    // da tela, por baixo/à esquerda do CharacterPanel quando ele estiver expandido.
    private const float CharacterCenterX = 0f;
    private const float CharacterGroundY = -1f;
    private const float OrthographicSize = 5f;

    // Calibração visual de referência da barra de XP/Level (ver BuildLevelXpHud): com o
    // personagem em CalibratedGroundY, a barra fica bem posicionada acima da cabeça dele em
    // CalibratedYFraction da tela. Se CharacterGroundY mudar de novo no futuro, a barra
    // acompanha proporcionalmente (mesmo deslocamento em unidades de mundo = mesmo
    // deslocamento em fração de tela, já que é um reposicionamento rígido do personagem
    // inteiro) sem precisar recalibrar esse par de valores manualmente.
    private const float CalibratedGroundY = -2f;
    private const float CalibratedYFraction = 0.72f;

    void Start()
    {
        var profile = selectedProfileHolder.currentProfile;

        if (profile == null) return;

        _orderedProfiles = characterDatabase != null
            ? characterDatabase.GetPlayableCharactersOrdered()
            : new List<PlayerProfile>();
        _currentIndex = Mathf.Max(0, _orderedProfiles.IndexOf(profile));

        SpawnCharacter(profile);
        BuildSwapArrows();
    }

    // Extraído de Start() (2026-07-14) — instanciação simples/instantânea, usada só pelo 1º
    // personagem exibido (sem transição, ver SlideToCharacter abaixo pra troca rápida animada).
    private void SpawnCharacter(PlayerProfile profile)
    {
        if (currentCharacter != null) Destroy(currentCharacter);
        if (levelXpHudGo != null) Destroy(levelXpHudGo);
        if (profile == null || profile.characterPrefab == null) return;

        currentCharacter = Instantiate(profile.characterPrefab, spawnPoint.position, Quaternion.identity);
        currentCharacter.transform.localScale = profile.scale * PreviewScaleFactor;
        currentCharacter.transform.position = new Vector3(CharacterCenterX, CharacterGroundY, 0);
        Camera.main.orthographicSize = OrthographicSize;

        var animController = PrepareCharacterForPreview(currentCharacter);
        BuildClickReaction(currentCharacter, animController);
        BuildLevelXpHud(profile);
    }

    // Remove os componentes de combate ativos (o personagem aqui é só um preview em Idle, nunca
    // luta) e devolve o AnimationController — compartilhado entre SpawnCharacter (1ª exibição) e
    // SlideToCharacter (troca rápida animada) pra não duplicar essa sequência duas vezes.
    private AnimationController PrepareCharacterForPreview(GameObject character)
    {
        DestroyImmediate(character.GetComponent<PlayerCombat>());
        DestroyImmediate(character.GetComponent<WeaponHandler>());
        DestroyImmediate(character.GetComponent<MovementController>());

        // AnimationController continua vivo (2026-07-08) — precisa dele pra reagir a clique com
        // Hurt/Slashing, ver CharacterPreviewReaction/BuildClickReaction.
        var animController = character.GetComponent<AnimationController>();
        if (animController != null) animController.SetIdle(true);
        return animController;
    }

    // Chamado pelas setas (BuildArrowButton) e pelo arraste (CharacterSwipeInput) — mesma ação
    // nos dois casos. direction: +1 = próximo, -1 = anterior (índice cíclico dentro de
    // _orderedProfiles). Atualiza o personagem equipado de verdade
    // (SelectedProfileHolder.currentProfile), não só o preview — troca rápida aqui é
    // equivalente a escolher o personagem em 02_SelectCharacter, sem precisar navegar até lá.
    // Ignorado enquanto uma transição de slide já está em andamento (_isSliding) — evita duas
    // trocas se sobrepondo (2 personagens saindo/entrando ao mesmo tempo).
    private void SwitchCharacter(int direction)
    {
        if (_isSliding || _orderedProfiles == null || _orderedProfiles.Count == 0) return;

        _currentIndex = ((_currentIndex + direction) % _orderedProfiles.Count + _orderedProfiles.Count) % _orderedProfiles.Count;
        var profile = _orderedProfiles[_currentIndex];

        selectedProfileHolder.currentProfile = profile;
        StartCoroutine(SlideToCharacter(profile, direction));

        // CharacterPanel (lado direito) só lê SelectedProfileHolder.currentProfile uma vez por
        // RefreshAll — não escuta o holder sozinho, precisa ser cutucado manualmente.
        var panel = FindObjectOfType<CharacterPanel>();
        if (panel != null) panel.Refresh();

        // Sincroniza com a nuvem em segundo plano (CloudSyncService, 2026-07-15, Fatia 4) —
        // fire-and-forget de propósito, pra não travar a animação de troca rápida esperando
        // rede; se o dado da nuvem for mais novo que o local (personagem trocado pela primeira
        // vez nesta sessão), o panel é atualizado de novo quando a sincronização terminar.
        StartCoroutine(SyncSwitchedCharacterRoutine(profile, panel));
    }

    private IEnumerator SyncSwitchedCharacterRoutine(PlayerProfile profile, CharacterPanel panel)
    {
        var task = CloudSyncService.SyncCharacterAsync(profile);
        yield return new WaitUntil(() => task.IsCompleted);
        if (panel != null) panel.Refresh();
    }

    // Transição de "carrossel" pedida pelo usuário (2026-07-14): arrastar/clicar pra um lado
    // desliza o personagem ATUAL pra fora nesse mesmo sentido até sumir, enquanto o PRÓXIMO
    // entra do lado oposto até centralizar — em vez da troca instantânea de antes.
    // `slideSign` é o sentido de saída do personagem atual: dragged/seta pra a DIREITA (aqui,
    // direction=-1, "anterior") desliza o atual pra a direita (slideSign=+1) e traz o novo da
    // ESQUERDA; direction=+1 ("próximo") é o espelho disso. `SlideDistance` (10, maior que a
    // meia-largura visível ~8.89 em world units) garante que o personagem saia de fato da tela
    // antes de ser destruído, não só "quase".
    private const float SlideDistance = 10f;
    private const float SlideDuration = 0.28f;
    private bool _isSliding;

    private IEnumerator SlideToCharacter(PlayerProfile profile, int direction)
    {
        _isSliding = true;
        float slideSign = -direction;

        var oldCharacter = currentCharacter;
        var oldHud = levelXpHudGo;
        currentCharacter = null;
        levelXpHudGo = null;

        GameObject newCharacter = null;
        AnimationController newAnimController = null;
        if (profile != null && profile.characterPrefab != null)
        {
            newCharacter = Instantiate(profile.characterPrefab, spawnPoint.position, Quaternion.identity);
            newCharacter.transform.localScale = profile.scale * PreviewScaleFactor;
            float startX = CharacterCenterX - slideSign * SlideDistance;
            newCharacter.transform.position = new Vector3(startX, CharacterGroundY, 0f);
            Camera.main.orthographicSize = OrthographicSize;
            newAnimController = PrepareCharacterForPreview(newCharacter);
        }

        Vector3 oldStart = oldCharacter != null ? oldCharacter.transform.position : Vector3.zero;
        Vector3 oldEnd = oldStart + new Vector3(slideSign * SlideDistance, 0f, 0f);
        Vector3 newStart = newCharacter != null ? newCharacter.transform.position : Vector3.zero;
        Vector3 newEnd = new Vector3(CharacterCenterX, CharacterGroundY, 0f);

        float elapsed = 0f;
        while (elapsed < SlideDuration)
        {
            float t = elapsed / SlideDuration;
            if (oldCharacter != null) oldCharacter.transform.position = Vector3.Lerp(oldStart, oldEnd, t);
            if (newCharacter != null) newCharacter.transform.position = Vector3.Lerp(newStart, newEnd, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (oldCharacter != null) oldCharacter.transform.position = oldEnd;
        if (newCharacter != null) newCharacter.transform.position = newEnd;

        if (oldCharacter != null) Destroy(oldCharacter);
        if (oldHud != null) Destroy(oldHud);

        currentCharacter = newCharacter;
        if (newCharacter != null)
        {
            BuildClickReaction(newCharacter, newAnimController);
            BuildLevelXpHud(profile);
        }

        _isSliding = false;
    }

    // Setas laterais (2026-07-14, pedido do usuário) — canvas próprio, sortingOrder abaixo do
    // LevelXpHud (4) pra não competir visualmente. Construídas uma única vez em Start(); só o
    // personagem/HUD de XP por trás delas é reconstruído a cada troca.
    private const float ArrowOffsetX = 340f; // pixels do centro da tela — chute inicial, calibrar visualmente
    private const float ArrowSize = 64f;
    private const float ArrowYFraction = 0.5f; // centralizado verticalmente — chute inicial, calibrar visualmente

    private void BuildSwapArrows()
    {
        var theme = ResolveTheme();
        if (theme == null) return;

        var canvasGo = new GameObject("SwapArrowsHud");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        BuildArrowButton(canvasGo.transform, theme, isLeft: true);
        BuildArrowButton(canvasGo.transform, theme, isLeft: false);
    }

    // Glyph "<"/">" em ASCII puro, não os caracteres Unicode "◀"/"▶" — a fonte TMP do projeto já
    // mostrou não ter cobertura de glyphs fora do ASCII básico (mesmo bug do "★"/"☆" de
    // CharacterCardUI, virava um quadrado "tofu"). `PulsingScale` dá a respirada sutil pedida
    // (aumenta/diminui via seno, sem precisar de Animator só pra isso).
    private void BuildArrowButton(Transform parent, UITheme theme, bool isLeft)
    {
        var go = new GameObject(isLeft ? "ArrowLeft" : "ArrowRight");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, ArrowYFraction);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(ArrowSize, ArrowSize);
        rt.anchoredPosition = new Vector2(isLeft ? -ArrowOffsetX : ArrowOffsetX, 0f);

        var img = go.AddComponent<Image>();
        img.sprite = UIShapeUtil.RoundedRect(theme.panelBackgroundAlt, ArrowSize / 2f);
        img.type = Image.Type.Sliced;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        int direction = isLeft ? -1 : 1;
        btn.onClick.AddListener(() => SwitchCharacter(direction));

        var glyphGo = new GameObject("Glyph");
        glyphGo.transform.SetParent(go.transform, false);
        var glyphRt = glyphGo.AddComponent<RectTransform>();
        glyphRt.anchorMin = Vector2.zero;
        glyphRt.anchorMax = Vector2.one;
        glyphRt.offsetMin = glyphRt.offsetMax = Vector2.zero;
        var glyphTxt = glyphGo.AddComponent<TextMeshProUGUI>();
        glyphTxt.raycastTarget = false;
        glyphTxt.text = isLeft ? "<" : ">";
        glyphTxt.fontSize = 32;
        glyphTxt.fontStyle = FontStyles.Bold;
        glyphTxt.color = theme.textOnDark;
        glyphTxt.alignment = TextAlignmentOptions.Center;

        go.AddComponent<PulsingScale>();
    }

    // Clique no personagem central reage com Hurt/Slashing (2026-07-08, pedido do usuário) —
    // mesma reação do portrait de `02_SelectCharacter`, via o componente compartilhado
    // `CharacterPreviewReaction`. Diferente do portrait (renderizado numa RenderTexture, clique
    // via `Button` de UI), aqui o personagem é world-space de verdade — clique detectado por
    // `Collider2D`/`OnMouseDown` (mensagem nativa da Unity, não depende de EventSystem nenhum).
    // `BoxCollider2D` dimensionado a partir dos bounds REAIS dos `Renderer`s do personagem (soma
    // de todas as partes do sprite, Spriter2UnityDX) em vez de um tamanho fixo chutado — cada
    // personagem tem proporções diferentes. Mesmo collider também alimenta `CharacterSwipeInput`
    // (2026-07-14) — arrastar o personagem pra esquerda/direita troca de personagem, mesma ação
    // das setas (`SwitchCharacter`). Unity manda `OnMouseDown`/`OnMouseUp` pros dois componentes
    // no mesmo GameObject sem conflito.
    private void BuildClickReaction(GameObject character, AnimationController animController)
    {
        if (animController == null) return;

        var renderers = character.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        foreach (var r in renderers) combined.Encapsulate(r.bounds);

        var collider = character.AddComponent<BoxCollider2D>();
        collider.offset = character.transform.InverseTransformPoint(combined.center);
        Vector3 scale = character.transform.lossyScale;
        collider.size = new Vector2(combined.size.x / scale.x, combined.size.y / scale.y);

        character.AddComponent<CharacterPreviewReaction>().Init(animController);
        character.AddComponent<CharacterSwipeInput>().Init(SwitchCharacter);
    }

    // Barra de XP fina + "Level X" acima dela, estilo My Brute — única barra de XP do menu
    // agora (a que existia no painel lateral foi removida, ver CharacterPanel). Fica
    // centralizada na mesma coordenada X do personagem (ver CharacterCenterX acima),
    // convertida pra fração de tela com a mesma fórmula de câmera ortográfica.
    //
    // Bug (2026-07-07, 2 rodadas): 1ª tentativa deixava um retângulo branco vazio (Canvas não
    // parentado + exceção no meio do método). Corrigido isso, mas a 2ª tentativa (guard
    // "if (theme == null) return;") revelou o problema de verdade: o campo
    // [SerializeField] UITheme theme deste componente resolve nulo em runtime, mesmo com o
    // asset wireado corretamente em 01_MainMenu.unity (conferido linha a linha) — o elemento
    // inteiro sumia (early return silencioso, sem exception nenhuma pro Console acusar).
    // Como o MainMenuController.theme (mesmo asset, mesmo mecanismo de serialização) funciona
    // de forma comprovada — o CharacterPanel que ele alimenta renderiza sem problema — o campo
    // próprio deste componente foi removido e o tema passou a ser buscado via
    // FindObjectOfType<MainMenuController>().Theme (ResolveTheme abaixo), eliminando de vez
    // essa 2ª fonte de wiring que se mostrou frágil duas vezes seguidas.
    private void BuildLevelXpHud(PlayerProfile p)
    {
        var theme = ResolveTheme();
        if (theme == null) return;

        Color panelBg = theme.panelBackgroundAlt;
        Color textColor = theme.textOnDark;
        Color goldColor = theme.currencyGold;

        const float halfWidth = 8.888889f; // OrthographicSize(5) * aspecto 16:9
        float centerXFraction = (CharacterCenterX + halfWidth) / (halfWidth * 2f);
        // Acompanha CharacterGroundY: mesmo deslocamento em unidades de mundo vira o mesmo
        // deslocamento em fração de tela (reposicionamento rígido do personagem, cabeça
        // inclusa) — ver comentário de CalibratedGroundY/CalibratedYFraction acima.
        float yFraction = CalibratedYFraction + (CharacterGroundY - CalibratedGroundY) / (2f * OrthographicSize);

        var canvasGo = new GameObject("LevelXpHud");
        levelXpHudGo = canvasGo; // guardado pra SpawnCharacter destruir e reconstruir a cada troca rápida
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var rootGo = new GameObject("Root");
        rootGo.transform.SetParent(canvasGo.transform, false);
        var rrt = rootGo.AddComponent<RectTransform>();
        // Caixa aumentada (2026-07-07): 240x56 → 280x76, pra caber o texto de XP dentro da
        // barra com folga. Y calculado a partir de CharacterGroundY (yFraction acima), não
        // mais um valor fixo — segue o personagem quando ele se move na tela.
        rrt.anchorMin = rrt.anchorMax = new Vector2(centerXFraction, yFraction);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(280f, 76f);

        // Fundo sólido (2026-07-07, era translúcido) — panelBackgroundAlt, mesma cor de fundo
        // do CharacterPanel, opaco.
        var bg = rootGo.AddComponent<Image>();
        bg.sprite = UIShapeUtil.RoundedRect(panelBg, 16f);
        bg.type = Image.Type.Sliced;

        var lvlGo = new GameObject("LevelText");
        lvlGo.transform.SetParent(rootGo.transform, false);
        var lrt = lvlGo.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.05f, 0.66f); lrt.anchorMax = new Vector2(0.95f, 0.96f);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var lvlTxt = lvlGo.AddComponent<TextMeshProUGUI>();
        lvlTxt.text = $"Level {p.level}";
        lvlTxt.fontSize = 18; lvlTxt.fontStyle = FontStyles.Bold;
        lvlTxt.color = textColor;
        lvlTxt.alignment = TextAlignmentOptions.Center;

        // Barra engordada (2026-07-07): ocupava só 0.14-0.42 (~16px de 56px) — agora
        // 0.08-0.58 (~38px de 76px), espaço suficiente pra caber o texto "atual/necessário"
        // centralizado dentro dela, além de mais legível por si só.
        var barBgGo = new GameObject("BarBg");
        barBgGo.transform.SetParent(rootGo.transform, false);
        var bbrt = barBgGo.AddComponent<RectTransform>();
        bbrt.anchorMin = new Vector2(0.06f, 0.08f); bbrt.anchorMax = new Vector2(0.94f, 0.58f);
        bbrt.offsetMin = bbrt.offsetMax = Vector2.zero;
        var barBgImg = barBgGo.AddComponent<Image>();
        barBgImg.sprite = UIShapeUtil.RoundedRect(new Color(0f, 0f, 0f, 0.55f), 8f);
        barBgImg.type = Image.Type.Sliced;

        int req = XpSystem.XpRequired(p.level);
        float pct = req > 0 ? Mathf.Clamp01((float)p.xpCurrent / req) : 0f;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(barBgGo.transform, false);
        var frt = fillGo.AddComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(Mathf.Max(pct, 0.001f), 1f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = UIShapeUtil.RoundedRect(goldColor, 7f);
        fillImg.type = Image.Type.Sliced;

        // Texto de XP dentro da própria barra (2026-07-07), "atual/necessário" — sobreposto ao
        // fundo+preenchimento (criado depois do Fill, então desenha por cima).
        var xpTxtGo = new GameObject("XpText");
        xpTxtGo.transform.SetParent(barBgGo.transform, false);
        var xrt = xpTxtGo.AddComponent<RectTransform>();
        xrt.anchorMin = Vector2.zero; xrt.anchorMax = Vector2.one;
        xrt.offsetMin = xrt.offsetMax = Vector2.zero;
        var xpTxt = xpTxtGo.AddComponent<TextMeshProUGUI>();
        xpTxt.text = $"{p.xpCurrent}/{req}";
        xpTxt.fontSize = 14; xpTxt.fontStyle = FontStyles.Bold;
        xpTxt.color = textColor;
        xpTxt.alignment = TextAlignmentOptions.Center;
    }

    // MainMenuController.theme já é comprovadamente confiável (CharacterPanel depende dele e
    // renderiza normalmente) — usa a mesma fonte em vez de duplicar o campo serializado aqui.
    private static UITheme ResolveTheme()
    {
        var controller = FindObjectOfType<MainMenuController>();
        return controller != null ? controller.Theme : null;
    }
}

// Pulsa a escala do próprio RectTransform via seno (mesmo espírito de PulsingAlpha em
// AttributePipBar.cs, só que em escala em vez de alpha) — dá a "respirada" sutil pedida pelo
// usuário pras setas de troca rápida de personagem, sem precisar de Animator só pra isso.
public class PulsingScale : MonoBehaviour
{
    private const float Speed = 2.5f;
    private const float MinScale = 0.92f;
    private const float MaxScale = 1.08f;

    private RectTransform _rt;

    private void Awake() => _rt = GetComponent<RectTransform>();

    private void Update()
    {
        float t = (Mathf.Sin(Time.time * Speed) + 1f) * 0.5f;
        float s = Mathf.Lerp(MinScale, MaxScale, t);
        _rt.localScale = new Vector3(s, s, 1f);
    }
}

// Detecta arrastar horizontal no personagem central do 01_MainMenu — mesmo Collider2D/mensagens
// nativas OnMouse* de CharacterPreviewReaction (não depende de EventSystem), num componente
// separado pra não misturar a lógica de reação de combate com a de troca de personagem.
// Só decide a DIREÇÃO ao soltar (sem arrastar o personagem visualmente durante o gesto).
public class CharacterSwipeInput : MonoBehaviour
{
    private const float SwipeThresholdPixels = 80f;

    private System.Action<int> onSwipe; // +1 = próximo, -1 = anterior
    private float startX;
    private bool dragging;

    public void Init(System.Action<int> callback) => onSwipe = callback;

    private void OnMouseDown()
    {
        startX = Input.mousePosition.x;
        dragging = true;
    }

    private void OnMouseUp()
    {
        if (!dragging) return;
        dragging = false;
        float delta = Input.mousePosition.x - startX;
        if (delta <= -SwipeThresholdPixels) onSwipe?.Invoke(1);
        else if (delta >= SwipeThresholdPixels) onSwipe?.Invoke(-1);
    }
}
