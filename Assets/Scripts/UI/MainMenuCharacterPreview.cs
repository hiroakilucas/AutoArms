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
    private GameObject energyHudGo;
    private readonly List<Image> _energyIcons = new List<Image>();
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
        if (energyHudGo != null) Destroy(energyHudGo);
        if (profile == null || profile.characterPrefab == null) return;

        currentCharacter = Instantiate(profile.characterPrefab, spawnPoint.position, Quaternion.identity);
        currentCharacter.transform.localScale = profile.scale * PreviewScaleFactor;
        currentCharacter.transform.position = new Vector3(CharacterCenterX, CharacterGroundY, 0);
        Camera.main.orthographicSize = OrthographicSize;

        var animController = PrepareCharacterForPreview(currentCharacter);
        BuildClickReaction(currentCharacter, animController);
        BuildLevelXpHud(profile);
        BuildEnergyHud();
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

        // CharacterPanel (gaveta no rodapé, modo bottomAnchored — ver MainMenuController.Start)
        // só lê SelectedProfileHolder.currentProfile uma vez por RefreshAll — não escuta o
        // holder sozinho, precisa ser cutucado manualmente.
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
        var oldEnergyHud = energyHudGo;
        currentCharacter = null;
        levelXpHudGo = null;
        energyHudGo = null;

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
        if (oldEnergyHud != null) Destroy(oldEnergyHud);

        currentCharacter = newCharacter;
        if (newCharacter != null)
        {
            BuildClickReaction(newCharacter, newAnimController);
            BuildLevelXpHud(profile);
            BuildEnergyHud();
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

        ComputeHudFractions(out float centerXFraction, out float yFraction);

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
        // Caixa: 240x56 (original) → 280x76 (2026-07-07) → 320x130 (2026-07-20, 1ª rodada mobile)
        // → 320x90 (2026-07-20, 2ª rodada — pedido do usuário: reduzir a altura da BARRA; "Level
        // X" e "atual/necessário" viraram uma linha só lado a lado em vez de sobrepostos,
        // liberando altura). Y calculado a partir de CharacterGroundY (yFraction acima), não mais
        // um valor fixo — segue o personagem quando ele se move na tela.
        rrt.anchorMin = rrt.anchorMax = new Vector2(centerXFraction, yFraction);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(320f, 90f);

        // Fundo sólido (2026-07-07, era translúcido) — panelBackgroundAlt, mesma cor de fundo
        // do CharacterPanel, opaco.
        var bg = rootGo.AddComponent<Image>();
        bg.sprite = UIShapeUtil.RoundedRect(panelBg, 16f);
        bg.type = Image.Type.Sliced;

        // Linha única "Level X" + "atual/necessário" lado a lado (2026-07-20, pedido do usuário —
        // era "Level X" sozinho numa linha, com o "atual/necessário" sobreposto dentro da própria
        // barra abaixo). Level à esquerda, fração de XP à direita, mesma linha.
        var lvlGo = new GameObject("LevelText");
        lvlGo.transform.SetParent(rootGo.transform, false);
        var lrt = lvlGo.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.06f, 0.58f); lrt.anchorMax = new Vector2(0.5f, 0.96f);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var lvlTxt = lvlGo.AddComponent<TextMeshProUGUI>();
        lvlTxt.text = $"Level {p.level}";
        lvlTxt.fontSize = 28; lvlTxt.fontStyle = FontStyles.Bold;
        lvlTxt.color = textColor;
        lvlTxt.alignment = TextAlignmentOptions.MidlineLeft;

        int req = XpSystem.XpRequired(p.level);
        float pct = req > 0 ? Mathf.Clamp01((float)p.xpCurrent / req) : 0f;

        var xpLabelGo = new GameObject("XpText");
        xpLabelGo.transform.SetParent(rootGo.transform, false);
        var xlrt = xpLabelGo.AddComponent<RectTransform>();
        xlrt.anchorMin = new Vector2(0.5f, 0.58f); xlrt.anchorMax = new Vector2(0.94f, 0.96f);
        xlrt.offsetMin = xlrt.offsetMax = Vector2.zero;
        var xpTxt = xpLabelGo.AddComponent<TextMeshProUGUI>();
        xpTxt.text = $"{p.xpCurrent}/{req}";
        xpTxt.fontSize = 28; xpTxt.fontStyle = FontStyles.Bold;
        xpTxt.color = textColor;
        xpTxt.alignment = TextAlignmentOptions.MidlineRight;

        // Barra fina (2026-07-20, era 0.08-0.58 = ~44px de 76px — bem mais grossa, porque antes
        // precisava caber o texto "atual/necessário" dentro dela; agora que o texto subiu pra
        // linha de cima, a barra só precisa ser uma indicação visual fina) ocupando a largura
        // toda logo abaixo da linha de texto.
        var barBgGo = new GameObject("BarBg");
        barBgGo.transform.SetParent(rootGo.transform, false);
        var bbrt = barBgGo.AddComponent<RectTransform>();
        bbrt.anchorMin = new Vector2(0.06f, 0.14f); bbrt.anchorMax = new Vector2(0.94f, 0.34f);
        bbrt.offsetMin = bbrt.offsetMax = Vector2.zero;
        var barBgImg = barBgGo.AddComponent<Image>();
        barBgImg.sprite = UIShapeUtil.RoundedRect(new Color(0f, 0f, 0f, 0.55f), 6f);
        barBgImg.type = Image.Type.Sliced;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(barBgGo.transform, false);
        var frt = fillGo.AddComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(Mathf.Max(pct, 0.001f), 1f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = UIShapeUtil.RoundedRect(goldColor, 5f);
        fillImg.type = Image.Type.Sliced;
    }

    // Fração de tela (X centralizado no personagem, Y calibrado por CharacterGroundY) usada
    // tanto por BuildLevelXpHud quanto por BuildEnergyHud — extraído (2026-07-19) pra não
    // duplicar a fórmula entre os dois HUDs empilhados acima da cabeça do personagem.
    private void ComputeHudFractions(out float centerXFraction, out float yFraction)
    {
        const float halfWidth = 8.888889f; // OrthographicSize(5) * aspecto 16:9
        centerXFraction = (CharacterCenterX + halfWidth) / (halfWidth * 2f);
        // Acompanha CharacterGroundY: mesmo deslocamento em unidades de mundo vira o mesmo
        // deslocamento em fração de tela (reposicionamento rígido do personagem, cabeça
        // inclusa) — ver comentário de CalibratedGroundY/CalibratedYFraction acima.
        yFraction = CalibratedYFraction + (CharacterGroundY - CalibratedGroundY) / (2f * OrthographicSize);
    }

    // Fileira de energia (2026-07-19, pedido do usuário) — 10 ícones. Esvazia da DIREITA pra
    // ESQUERDA ao consumir e reenche na mesma ordem (os ícones preenchidos ficam sempre "colados"
    // à esquerda) — ícone na posição visual i (0=esquerda...9=direita) aparece cheio quando
    // i < EnergyCurrent, vazio/acinzentado caso contrário. Lê PlayerEconomyState (cache síncrono
    // populado por EnergyService/LoginController.LoadEconomyRoutine) — não faz nenhuma chamada ao
    // Firestore por conta própria.
    // 2026-07-20 (bug real corrigido): ANTES ficava no topo da pilha vertical acima do
    // personagem, seguindo `ComputeHudFractions` (mesmo ponto do LevelXpHud) — mudar o aspect
    // ratio da tela desalinhava o chip porque parte do cálculo (`.../1080f` em BuildEnergyHud)
    // assumia Canvas sempre com 1080 de altura, o que só é verdade em 16:9 exato (ver comentário
    // de BuildEnergyHud). Passou a usar uma âncora FIXA no topo-centro da tela (mesmo princípio
    // do BtnJogar/BuildCurrencyHud) — não segue mais o personagem, mas fica estável em qualquer
    // resolução/aspect ratio.
    // 2026-07-20 (2ª rodada, reorganização do HUD superior): alinhado na MESMA altura de
    // moeda/diamante em vez de ficar centralizado sozinho mais acima — ver
    // EnergyHudTopAlignmentOffset. O texto "Próxima energia em H:MM:SS" deixou de ficar
    // permanente na tela — primeiro virou um tooltip inline por toque, depois (mesmo dia, 2ª
    // rodada) um popup modal centralizado (ver OnEnergyRowTapped/
    // MainMenuController.ShowEnergyStatusPopup) porque o tooltip inline ficava cortado perto do
    // topo da tela.
    private const int EnergySlotCount = 10;
    // 88f (2026-07-20, era 44f) — redesenho mobile, pedido do usuário: dobrar de novo.
    private const float EnergyIconSize = 88f;
    // Não é mais fixo (2026-07-20, 4ª rodada) — o espaçamento agora é CALCULADO em
    // MeasureTimerTextWidth/BuildEnergyHud, pra fileira de ícones ficar do mesmo tamanho do
    // texto "Próxima energia em H:MM:SS" (pedido do usuário, de quando esse texto ainda era
    // exibido ao lado da fileira — ver acima). Clamp evita sobreposição a ponto de virar uma
    // mancha ilegível se o texto for mais estreito que ~40% da largura natural da fileira.
    private const float EnergyIconSpacingMin = -60f;
    // Só usado por MeasureTimerTextWidth agora (sizing da fileira) — o texto em si não é mais
    // renderizado nesse tamanho (o popup usa a fonte padrão de BuildPopup). Mantido em 32
    // (valor já em uso antes desta rodada) pra não alterar o espaçamento dos ícones já calibrado.
    private const float EnergyTimerFontSize = 32f;
    // Altura-alvo do CENTRO da fileira de energia, em distância abaixo do topo da tela — alinhada
    // com moeda/diamante (2026-07-20, pedido do usuário: "mesma linha horizontal", em vez de
    // energia ficar centralizada mais acima e moeda/diamante à direita separadamente). Calculado
    // a partir dos valores exatos de `MainMenuController.BuildCurrencyHud` (Row anchoredPosition
    // -6.099976, sizeDelta.y 146.346, ícone de moeda em y=0 / diamante em y=4.827 dentro do Row):
    // moeda fica ~79.27 abaixo do topo da tela, diamante ~74.45 — usa a média (~76.86). Os dois
    // HUDs são classes/Canvas SEPARADOS (sem acoplamento automático) — se os valores de
    // BuildCurrencyHud mudarem no futuro, reconferir esta constante também.
    private const float EnergyHudTopAlignmentOffset = 76.86f;
    private static readonly Color EnergyEmptyTint = new Color(0.3f, 0.3f, 0.3f, 0.55f);

    private void BuildEnergyHud()
    {
        var theme = ResolveTheme();
        if (theme == null) return;

        var energySprite = Resources.Load<Sprite>("UI/Economy/Energy");

        float rowHeight = EnergyIconSize;

        var canvasGo = new GameObject("EnergyHud");
        energyHudGo = canvasGo;
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Mede a largura real do texto do timer ANTES de decidir o espaçamento dos ícones
        // (2026-07-20, pedido do usuário: "deixar [a fileira] do mesmo tamanho que o texto
        // 'Próxima energia em 0:00:00'") — e só então calcula o espaçamento negativo necessário
        // pra fileira de EnergySlotCount ícones somar essa mesma largura.
        float timerTextWidth = MeasureTimerTextWidth(canvasGo);
        float rowWidth = Mathf.Max(timerTextWidth, EnergyIconSize);
        float spacing = (rowWidth - EnergySlotCount * EnergyIconSize) / (EnergySlotCount - 1);
        spacing = Mathf.Max(spacing, EnergyIconSpacingMin);
        rowWidth = EnergySlotCount * EnergyIconSize + (EnergySlotCount - 1) * spacing;

        var rootGo = new GameObject("Root");
        rootGo.transform.SetParent(canvasGo.transform, false);
        var rrt = rootGo.AddComponent<RectTransform>();
        // Âncora FIXA no topo-centro da tela (2026-07-20, bug real corrigido — ver comentário
        // acima do método) em vez do ponto fracionário calculado a partir da posição do
        // personagem (`ComputeHudFractions`) — esse cálculo antigo somava um offset em PIXELS
        // dividido por 1080 pra virar fração (`.../1080f`), assumindo que o Canvas sempre mede
        // exatamente 1080 de altura; com CanvasScaler em ScaleWithScreenSize + matchWidthOrHeight
        // travado na LARGURA, mudar o aspect ratio (ex: tela mais larga no Free Aspect do Editor)
        // faz a altura REAL do Canvas variar (fica MENOR que 1080 numa tela mais larga), então a
        // fração calculada não batia mais com a altura verdadeira — o chip de energia "flutuava"
        // pra cima/baixo do lugar certo. (0.5, 1) é uma âncora de ponto único num canto real da
        // tela (mesmo princípio do BtnJogar/`BuildCurrencyHud`, ambos com âncora em canto fixo),
        // então fica estável em qualquer resolução/aspect ratio — só não segue mais o personagem
        // (LevelXpHud continua seguindo, ver BuildLevelXpHud/ComputeHudFractions; não fazia parte
        // do pedido). anchoredPosition.y usa EnergyHudTopAlignmentOffset (2ª rodada) em vez de
        // reservar espaço pro texto do timer acima — ele não é mais permanente (virou tooltip).
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 1f);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(rowWidth, rowHeight);
        rrt.anchoredPosition = new Vector2(0f, -EnergyHudTopAlignmentOffset);

        BuildEnergyChipBackground(rootGo, theme);

        _energyIcons.Clear();
        for (int i = 0; i < EnergySlotCount; i++)
        {
            var iconGo = new GameObject($"Slot{i}");
            iconGo.transform.SetParent(rootGo.transform, false);
            var irt = iconGo.AddComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.sizeDelta = new Vector2(EnergyIconSize, EnergyIconSize);
            irt.anchoredPosition = new Vector2(i * (EnergyIconSize + spacing), 0f);
            var img = iconGo.AddComponent<Image>();
            img.sprite = energySprite;
            img.preserveAspect = true;
            _energyIcons.Add(img);
        }

        // Botão invisível cobrindo a fileira inteira (2026-07-20, pedido do usuário — "adicionar
        // interação de toque... reaproveitar o mesmo padrão de tooltip por clique que o Arsenal
        // já usa") — toca a fileira pra abrir o popup com o tempo exato (ver OnEnergyRowTapped).
        // Usa uma Image transparente (`Color.clear`) como `targetGraphic` só pra dar área de
        // clique — sem ela o Button não teria nenhum Graphic raycastável próprio pra capturar o
        // toque.
        var tapGo = new GameObject("TapArea");
        tapGo.transform.SetParent(rootGo.transform, false);
        var tapRt = tapGo.AddComponent<RectTransform>();
        tapRt.anchorMin = Vector2.zero; tapRt.anchorMax = Vector2.one;
        tapRt.offsetMin = tapRt.offsetMax = Vector2.zero;
        var tapImg = tapGo.AddComponent<Image>();
        tapImg.color = Color.clear;
        var tapBtn = tapGo.AddComponent<Button>();
        tapBtn.targetGraphic = tapImg;
        tapBtn.transition = Selectable.Transition.None;
        tapBtn.onClick.AddListener(OnEnergyRowTapped);

        // Watcher sempre ativo, sem parte visual (2026-07-20, 2ª rodada — ver comentário de
        // BuildEnergyResyncWatcher) — mantém vivo o re-sync automático quando o countdown chega
        // em zero, independente de o jogador ter tocado a fileira ou não.
        BuildEnergyResyncWatcher(rootGo);

        ApplyEnergyFill();
    }

    // Toque na fileira de energia (2026-07-20, pedido do usuário) — mostra o tempo exato até a
    // próxima energia. 2ª rodada (mesmo dia): o tooltip inline (ancorado ACIMA da fileira, ver
    // histórico no CHANGELOG) ficava cortado/inacessível perto do topo da tela — trocado por um
    // popup modal CENTRALIZADO na tela (overlay escurecido + painel + botão "OK"), o mesmo padrão
    // que os outros popups do projeto (REPLAYS, mensagens de energia esgotada) já usam. O popup
    // mora em `MainMenuController` (mesmo Canvas/estilo dos outros); aqui só decide SE deve abrir.
    // Cheio (10/10) não tem nada regenerando — toque não faz nada nesse caso (pedido explícito do
    // usuário), em vez de abrir um popup mostrando "Energia cheia".
    private void OnEnergyRowTapped()
    {
        if (PlayerEconomyState.EnergyCurrent >= PlayerEconomyState.EnergyMax) return;
        FindObjectOfType<MainMenuController>()?.ShowEnergyStatusPopup();
    }

    // Substitui o antigo tooltip inline (removido nesta rodada) como dono do CountdownLabel que
    // dispara o re-sync automático quando o countdown chega em zero (`EnergyCountdownAtZero`/
    // `RefreshEconomyOnMenuLoad`, bug corrigido numa rodada anterior). GameObject sempre ATIVO,
    // sem Text/Image nenhum — só existe pra manter esse `Update()` rodando em segundo plano,
    // independente do popup estar aberto ou fechado (ele só abre por toque agora, ver
    // OnEnergyRowTapped/MainMenuController.ShowEnergyStatusPopup).
    private void BuildEnergyResyncWatcher(GameObject parent)
    {
        var watcherGo = new GameObject("ResyncWatcher");
        watcherGo.transform.SetParent(parent.transform, false);
        watcherGo.AddComponent<CountdownLabel>().Init(null, null,
            isDoneCheck: PlayerEconomyState.EnergyCountdownAtZero,
            onDone: () => FindObjectOfType<MainMenuController>()?.RefreshEconomyOnMenuLoad());
    }

    // Mede a largura que o texto do timer vai ocupar usando um TMP temporário/invisível (alpha
    // 0, destruído logo em seguida) — precisa estar numa hierarquia ATIVA (filho de canvasGo,
    // já ativo) pra TMP_Text.Awake() resolver o fontAsset antes de GetPreferredValues()
    // funcionar (mesma pegadinha já documentada em CharacterPanel.ShowSkillDetail sobre popups
    // desativados). "0:00:00" é o texto de referência: dígitos são intercambiáveis em largura na
    // fonte do projeto, então qualquer combinação de MM:SS mede quase o mesmo.
    private float MeasureTimerTextWidth(GameObject canvasGo)
    {
        var probeGo = new GameObject("TimerWidthProbe");
        probeGo.transform.SetParent(canvasGo.transform, false);
        var probeTxt = probeGo.AddComponent<TextMeshProUGUI>();
        probeTxt.fontSize = EnergyTimerFontSize;
        probeTxt.fontStyle = FontStyles.Bold;
        probeTxt.color = new Color(0f, 0f, 0f, 0f);
        Vector2 size = probeTxt.GetPreferredValues("Próxima energia em 0:00:00", 0f, 0f);
        Destroy(probeGo);
        return size.x;
    }

    // Chip de fundo atrás da fileira de ícones (2026-07-20, pedido do usuário — hoje ficam soltos
    // direto sobre o fundo da cena, com contraste baixo). Mesmo tom do painel de detalhe do
    // personagem (`CharacterPanel.PanelBgColor` = `theme.panelBackgroundAlt`) pra manter
    // consistência visual entre os dois HUDs, mas semi-transparente (2026-07-20, 2ª rodada —
    // pedido do usuário: "integrar melhor com o brilho variável da cena atrás, mantendo
    // contraste suficiente"; era opaco). Filho do PRÓPRIO rootGo (que já tem o tamanho exato da
    // fileira de ícones) e criado ANTES dos ícones, então renderiza atrás deles (ordem de sibling
    // no Canvas). Stretch (0,0)-(1,1) com offsets NEGATIVOS/POSITIVOS pra "abraçar" o conteúdo
    // com um padding mínimo (2026-07-20, 3ª rodada: não reserva mais espaço extra em cima pro
    // timer — ele deixou de ser permanente, virou popup por toque, ver OnEnergyRowTapped/
    // MainMenuController.ShowEnergyStatusPopup).
    private void BuildEnergyChipBackground(GameObject rootGo, UITheme theme)
    {
        const float chipPadX = 8f;
        const float chipPadY = 8f;

        var chipGo = new GameObject("ChipBg");
        chipGo.transform.SetParent(rootGo.transform, false);
        var chipRt = chipGo.AddComponent<RectTransform>();
        chipRt.anchorMin = Vector2.zero; chipRt.anchorMax = Vector2.one;
        chipRt.offsetMin = new Vector2(-chipPadX, -chipPadY);
        chipRt.offsetMax = new Vector2(chipPadX, chipPadY);
        var chipImg = chipGo.AddComponent<Image>();
        var chipColor = theme.panelBackgroundAlt;
        chipColor.a = 0.6f;
        chipImg.sprite = UIShapeUtil.RoundedRect(chipColor, 20f);
        chipImg.type = Image.Type.Sliced;
    }

    // Só recolore os ícones já construídos (sem destruir/reconstruir a fileira) — chamado pelo
    // MainMenuController depois de consumir/reabastecer energia, e por RefreshEnergyHud (público)
    // pra qualquer outro chamador externo.
    private void ApplyEnergyFill()
    {
        int current = Mathf.Clamp(PlayerEconomyState.EnergyCurrent, 0, EnergySlotCount);
        for (int i = 0; i < _energyIcons.Count; i++)
            _energyIcons[i].color = i < current ? Color.white : EnergyEmptyTint;
    }

    // Público (2026-07-19) — MainMenuController chama depois de EnergyService.ConsumeOneAsync/
    // RefillOneAsync pra refletir o novo valor sem esperar uma troca de personagem/recarregar a
    // cena. No-op silencioso se a fileira ainda não foi construída (personagem ainda carregando).
    public void RefreshEnergyHud()
    {
        if (_energyIcons.Count == 0) return;
        ApplyEnergyFill();
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
