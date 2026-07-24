using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// HUD do personagem, com dois estados:
//   Compact  — nome, Win Rate, HP em texto, STR/AGI/SPD em pips (clique expande)
//   Expanded — mesmo bloco de informação do Compact (sem duplicar o desenho, só reconstruído
//              num container do mesmo tamanho) + seções de Habilidades e Armas equipadas
//              (grade de ícones com borda colorida por tier, clique abre popup de detalhe) +
//              botão "Detalhes" que revela a seção PASSIVAS (label:valor simples, sem
//              ícone/barra) — tudo rolável. Clique fora dos elementos interativos recolhe.
// Root é uma janela de altura FIXA (não anima tamanho) ancorada entre RootAnchorBottom (acima
// do BtnJogar, com margem) e RootAnchorTop por padrão — só o conteúdo interno (Compact vs
// Expanded) alterna via CanvasGroup (fade), nunca o footprint do Root em si. Essa janela pode ser
// sobrescrita por instância via `Setup(..., anchorBottomOverride, anchorTopOverride)` — usado por
// 02_SelectCharacter pra encolher o Root e casar com a moldura ao redor dele, sem afetar a janela
// calibrada do 01_MainMenu (que nunca passa esses parâmetros).
// Originalmente só do 01_MainMenu (sempre visível, sem Level/XP — mostrado acima da cabeça do
// personagem por MainMenuCharacterPreview) — reaproveitado por 02_SelectCharacter desde
// 2026-07-08 via `Setup(holder, theme, showLevelXp: true, startHidden: true)` +
// `ShowSlideIn()`/`HideSlideOut()` + `SetProfile(profile)` a cada clique num card (sem isso o
// painel sempre mostraria `_holder.currentProfile`, o personagem EQUIPADO, não o clicado no
// grid) — sem duplicar nenhuma lógica de Compact/Expanded/Skills/Armas. Largura sempre a mesma
// (`PanelWidth` fixo, ancorado à direita) nas duas telas — não existe mais um modo "preenche o
// espaço restante" (removido, ver histórico no CHANGELOG).
public class CharacterPanel : MonoBehaviour
{
    private SelectedProfileHolder _holder;
    private UITheme _theme;

    // Wireado via Setup (2026-07-18, botão REPLAYS) — só usado pra resolver o characterPrefab/
    // attackSettings/etc do adversário de um replay (ReplaySnapshotConverter.ToRuntimeProfile);
    // null = botão REPLAYS ainda funciona (lista), mas clicar num replay loga erro em vez de
    // travar, já que não há como reconstruir os personagens sem o catálogo.
    private CharacterDatabase _characterDatabase;

    // Lista de replays aberta no popup (2026-07-18) — token evita que dois cliques rápidos no
    // botão REPLAYS façam a resposta do primeiro Firestore.ListReplaysAsync popular a lista do
    // segundo clique (ver LoadAndShowReplaysAsync).
    private Transform _replayListContent;
    private int _replayLoadToken;

    // Override explícito (2026-07-08) — quando não-nulo, RefreshAll mostra ESTE profile em vez
    // de `_holder.currentProfile`. 01_MainMenu nunca chama SetProfile (mostra sempre o
    // equipado, via _holder); 02_SelectCharacter chama a cada clique num card do grid, senão o
    // painel sempre mostraria as stats do personagem atualmente equipado, não o card clicado.
    private PlayerProfile _overrideProfile;

    private GameObject _canvasGo;
    private RectTransform _rootRt;
    // SafeArea do modo gaveta-inferior (2026-07-21) — guardado pra BuildExpanded conseguir ler
    // `rect.height` na hora de clampar o overflow do Expanded pro teto real da tela (ver
    // BottomDrawerExpandedTopOverflow/BuildExpanded). Só existe quando `_bottomAnchored`.
    private RectTransform _safeAreaRt;
    private GameObject _compactGo, _expandedGo;
    private CanvasGroup _compactCg, _expandedCg;
    private bool _isExpanded;

    // Janela vertical efetiva do Root (2026-07-08) — default = RootAnchorBottom/Top (comportamento
    // de sempre do 01_MainMenu), mas 02_SelectCharacter pode sobrescrever via Setup pra encolher o
    // Root e casar exatamente com a altura da própria moldura dourada ao redor dele (pedido do
    // usuário: painel vazava por cima da moldura; ao usar a MESMA janela vertical da moldura, o
    // Root passa a caber perfeitamente dentro dela, sem precisar centralizar nada à parte).
    private float _anchorBottom = RootAnchorBottom;
    private float _anchorTop = RootAnchorTop;

    // Gaveta mobile ancorada embaixo (2026-07-20, redesenho do HUD principal — ver CLAUDE.md
    // "Redesenho Mobile do HUD Principal") — Root vira uma janela de largura FIXA (PanelWidth,
    // igual de sempre) centralizada horizontalmente e ancorada no rodapé, em vez do painel
    // vertical do lado direito. Reaproveita 100% da lógica de Compact/Expanded/Skills/Armas/
    // Pets/Passivas já existente (só a geometria de ancoragem muda) — ver os `if (_bottomAnchored)`
    // em BuildUI/BuildCompact/BuildExpanded. Cresce PRA CIMA (Compact fica na base do Root,
    // Expanded's InfoBlock também na base, com o ScrollArea de Skills/Armas/Pets ACIMA dele —
    // o oposto do modo painel-lateral, onde tudo fica ancorado no TOPO do Root).
    private bool _bottomAnchored;
    // Ver comentário em Setup — quando true, o estado Compact nunca aparece (nem inicialmente nem
    // ao Collapse()), CrossFade pula direto pra "nada visível" em vez de mostrar Compact.
    private bool _hideCompact;
    private const float BottomDrawerMaxHeight = 820f; // CompactHeight + folga generosa pro ScrollArea
    // Largura real da gaveta (2026-07-20, "ajustes finos" pedidos pelo usuário) — igual à largura
    // final que o "Compact" já tinha (450 de PanelWidth + 206.857 à esquerda + 208.846 à direita,
    // ver BuildCompact) — Root passou a usar ESSA largura diretamente (em vez de PanelWidth) pra
    // Expanded (que sempre preenche 100% do Root) parar de ficar mais ESTREITO que o Compact.
    private const float BottomDrawerWidth = 865.7f;
    // Distância entre a base do Root (= base do botão Jogar, ver BuildUI) e a base VISÍVEL do
    // painel (o fundo arredondado do Compact/Expanded) — 2026-07-20, bug real corrigido: o
    // Expanded usava offsetMin.y=0 (colado na base do Root), enquanto o Compact sempre usou 28px
    // (ver BuildCompact) — ao expandir, o fundo visível "descia" 28px porque os dois estados não
    // compartilhavam a mesma base. Root em si NUNCA muda de tamanho (fixo em BottomDrawerMaxHeight,
    // ancorado na base) — só o CONTEÚDO visível (Compact vs Expanded) preenchia frações diferentes
    // dele; agora os dois começam exatamente na mesma linha, só o TOPO do Expanded sobe (até o
    // topo do Root, que já é alto o bastante pra sobrepor a fileira de energia se precisar).
    private const float BottomDrawerFloorGap = 28f;
    // Quanto o TOPO do Expanded ultrapassa o topo do Root (2026-07-20, pedido do usuário — ponto
    // 2 do requisito: "no estado expandido, o painel deve poder crescer até sobrepor a fileira de
    // energia no topo da tela"). Valor lido pelo usuário direto no Editor (campo "Top" da
    // Inspector com o Expanded stretched em Y = -348.4799, ou seja offsetMax.y = +348.4799) — só
    // o Expanded cresce além do Root; o Compact continua do tamanho de sempre, e a base dos dois
    // permanece a mesma (BottomDrawerFloorGap).
    private const float BottomDrawerExpandedTopOverflow = 348.4799f;
    // Margem de respiro entre o topo do Expanded e o teto real da tela, depois de clampado (ver
    // BuildExpanded) — mesma ordem de grandeza de outras margens de borda do projeto (EdgeMargin
    // acima, ~11px do RootAnchorTop de sempre).
    private const float BottomDrawerTopSafeMargin = 20f;

    // Level+XP dentro do painel (2026-07-08, opt-in via Setup) — usado quando o painel é
    // reaproveitado fora do 01_MainMenu (ali o XP já fica acima da cabeça do personagem via
    // MainMenuCharacterPreview.BuildLevelXpHud, não duplicado aqui).
    private bool _showLevelXp;

    // Slide-in/out (2026-07-08) — usado quando o painel é instanciado já escondido fora da tela
    // (startHidden em Setup) e precisa aparecer/sumir sob demanda (ex: 02_SelectCharacter, ao
    // clicar num card / clicar Voltar). Não usado pelo 01_MainMenu (painel sempre visível).
    private Vector2 _restAnchoredPos;
    private Coroutine _slideRoutine;

    // Bloco de nome/winrate/HP/pips — construído duas vezes (Compact e topo do Expanded) com
    // exatamente o mesmo layout relativo, por isso os dois conjuntos de referências.
    private class InfoBlockRefs
    {
        // hp: ícone+número sobreposto (BuildIconWithValue) — usado nos DOIS modos (2026-07-20,
        // 4ª rodada: HP na gaveta mobile voltou a este estilo, sem pips; ver BuildInfoBlock).
        public TMP_Text name, winRate, hp;
        public AttributePipBar str, agi, spd;
        public TMP_Text level, xpValue; // só preenchidos quando _showLevelXp
        public Image xpFill;
    }
    private InfoBlockRefs _compactInfo, _expandedInfo;

    // Skills/Armas — listas dinâmicas dentro do Expanded, reconstruídas a cada RefreshAll.
    private Transform _skillsList, _armasList, _petsList;
    private TMP_Text _skillsEmpty, _armasEmpty, _petsEmpty;

    // Botão "Detalhes" + seção PASSIVAS (2026-07-07) — linhas fixas (o conjunto de passivas
    // não varia por personagem, só o valor), então são construídas uma vez e só o texto é
    // atualizado em RefreshAll, ao contrário de Skills/Armas (que variam em quantidade e por
    // isso são destruídas/reconstruídas a cada refresh).
    private GameObject _passivesSection;
    private TMP_Text _detailsButtonLabel;
    private bool _passivesVisible;
    private readonly Dictionary<string, TMP_Text> _passiveRows = new Dictionary<string, TMP_Text>();

    // Popup de descrição (2026-07-07) — construído uma vez (sibling mais recente do Canvas, por
    // isso desenha por cima de tudo o resto, inclusive o Expanded), escondido por padrão.
    // ShowSkillDetail/ShowWeaponDetail preenchem título+corpo e mostram; CloseDetailPopup some.
    private GameObject _popupOverlayGo;
    private RectTransform _popupPanelRt;
    private Transform _popupContentRoot;

    private Color PanelBgColor => _theme.panelBackgroundAlt;
    private Color TextColor    => _theme.textOnDark;

    // Públicas (2026-07-08) — 02_SelectCharacter precisa alinhar seus próprios botões/áreas de
    // layout exatamente com o Root deste painel (mesma largura fixa/margem/janela vertical do
    // 01_MainMenu, ver CharacterSelectController), em vez de duplicar esses valores como magic
    // numbers soltos em outro arquivo.
    public const float PanelWidth    = 450f;
    const float CompactHeight = 230f;  // altura do bloco de info (Compact inteiro / topo do Expanded)
    const float FadeDuration  = 0.18f; // transição compact<->expanded

    // Bloco opcional de Level+XP (2026-07-08) — mesmo estilo compacto do card
    // (CharacterCardUI.BuildLevelXpBar), inserido logo abaixo do bloco de info de sempre quando
    // showLevelXp=true (ver BuildLevelXpBox). Extra some por completo (0) quando false — zero
    // impacto no layout calibrado do 01_MainMenu.
    const float LevelXpBoxHeight = 54f;
    const float LevelXpGap = 10f;
    private float LevelXpExtra => _showLevelXp ? LevelXpGap + LevelXpBoxHeight : 0f;

    // Janela vertical do Root, fração de um canvas 1920x1080 (ScaleWithScreenSize): topo em
    // 0.99 (margem de ~11px do topo da tela) e base em 0.28 (302px). O BtnJogar (canto
    // inferior direito, âncora (1,0), topo em 253px/0.234 — puxado pra baixo em 2026-07-07,
    // era 280px/0.259, ver CLAUDE.md) fica ~49px abaixo da base do Root — recalculado
    // (2026-07-07) considerando as fontes maiores do bloco de info (26pt nome, 20pt HP etc.) +
    // as novas seções de Skills/Armas: o Root sobra ~537px de área rolável abaixo do bloco de
    // info (766px de altura total - 230px do bloco), então a margem de segurança acima do
    // Jogar continua de sobra mesmo com o conteúdo novo.
    public const float RootAnchorTop    = 0.99f;
    public const float RootAnchorBottom = 0.28f;

    // Margem direita (2026-07-07, alinhada com o BtnJogar) — igual ao inset horizontal do
    // `BtnJogar` na cena (`m_AnchoredPosition.x = -25`, âncora/pivot em x=1 — ver
    // `01_MainMenu.unity`), então a borda direita do painel fica exatamente alinhada com a
    // borda direita do botão "Jogar" (era 11px, valor arbitrário que só copiava a margem do
    // topo do Root, sem relação nenhuma com o botão).
    public const float EdgeMargin = 25f;

    // ── Public API ──────────────────────────────────────────────────────────

    // `anchorBottomOverride`/`anchorTopOverride` (2026-07-08, opcionais): substituem
    // RootAnchorBottom/RootAnchorTop só pra ESTA instância — usado por 02_SelectCharacter pra
    // encolher o Root e casar com a altura da moldura dourada ao redor dele. `null` (default,
    // usado pelo 01_MainMenu) preserva a janela vertical calibrada de sempre.
    // `hideCompact` (2026-07-21, opcional, default false — sem efeito em nenhum caller existente):
    // pula o estado Compact por completo (nunca aparece, nem no início nem ao clicar Collapse) —
    // usado por CombatResultPanel (tela de level-up), que constrói seu PRÓPRIO botão quadrado de
    // fora pra abrir/fechar (chamando Expand()/Collapse() diretamente) em vez do bloco Compact
    // padrão, que estava cobrindo uma caixa de escolha. Ver CrossFade abaixo.
    public void Setup(SelectedProfileHolder holder, UITheme theme, bool showLevelXp = false,
        bool startHidden = false, float? anchorBottomOverride = null, float? anchorTopOverride = null,
        CharacterDatabase characterDatabase = null, bool bottomAnchored = false, bool hideCompact = false)
    {
        _holder = holder;
        _theme = theme;
        _showLevelXp = showLevelXp;
        _anchorBottom = anchorBottomOverride ?? RootAnchorBottom;
        _anchorTop = anchorTopOverride ?? RootAnchorTop;
        _characterDatabase = characterDatabase;
        _bottomAnchored = bottomAnchored;
        _hideCompact = hideCompact;
        BuildUI();
        RefreshAll();

        _restAnchoredPos = _rootRt.anchoredPosition;
        if (startHidden)
        {
            _rootRt.anchoredPosition = _restAnchoredPos + new Vector2(HideOffsetX, 0f);
            // Cinto e suspensório (2026-07-09): além de posicionar fora da tela, desativa o
            // Canvas inteiro — garante invisibilidade total independente de qualquer nuance de
            // RectTransform/CanvasScaler, em vez de depender só da posição.
            _canvasGo.SetActive(false);
        }
    }

    // Distância suficiente pra garantir que o Root (largura fixa `PanelWidth`) fique inteiramente
    // fora da tela à direita.
    private float HideOffsetX => PanelWidth + 60f;

    // ── Slide in/out (2026-07-08) ────────────────────────────────────────────
    // Desliza o Root inteiro a partir de fora da tela (direita) até a posição ancorada de
    // sempre, ou de volta pra fora — usado por telas que instanciam o painel já escondido
    // (startHidden em Setup) e querem revelá-lo sob demanda (ex: 02_SelectCharacter ao clicar
    // num card), em vez de sempre visível como no 01_MainMenu.
    // Troca qual PlayerProfile este painel exibe e atualiza tudo na hora (nome/HP/pips/skills/
    // armas/passivas) — usado por telas com grid (02_SelectCharacter) onde o painel precisa
    // mostrar o personagem CLICADO, não o `_holder.currentProfile` (o equipado agora).
    public void SetProfile(PlayerProfile profile)
    {
        _overrideProfile = profile;
        RefreshAll();
    }

    // Reconstrói nome/HP/pips/skills/armas/passivas a partir de `_holder.currentProfile` de novo
    // (sem tocar em `_overrideProfile`) — usado por 01_MainMenu (troca rápida de personagem via
    // setas/arraste, ver MainMenuCharacterPreview) depois de mudar
    // `SelectedProfileHolder.currentProfile` em runtime, já que este painel só lê o holder uma
    // vez por RefreshAll e não escuta nenhum evento de mudança sozinho.
    public void Refresh() => RefreshAll();

    // Move o Root pra fora da tela SEM desativar `_canvasGo` (diferente de HideSlideOut, que
    // também desliga o Canvas ao final da animação) — usado por telas que só querem o POPUP de
    // detalhe deste painel (ex: ArsenalController/03_Arsenal), sem mostrar o HUD Compact/
    // Expanded. O popup (BuildPopup, sibling mais recente de `_canvasGo`) continua funcionando
    // normalmente, já que o Canvas em si permanece ativo — só o Root fica fora da área visível.
    public void HideRootPermanently()
    {
        _rootRt.anchoredPosition = _restAnchoredPos + new Vector2(HideOffsetX, 0f);
    }

    public void ShowSlideIn(float duration = 0.3f)
    {
        _canvasGo.SetActive(true);
        if (_slideRoutine != null) StopCoroutine(_slideRoutine);
        _rootRt.anchoredPosition = _restAnchoredPos + new Vector2(HideOffsetX, 0f);
        _slideRoutine = StartCoroutine(SlideTo(_restAnchoredPos, duration, hideCanvasAtEnd: false));
    }

    public void HideSlideOut(float duration = 0.3f)
    {
        if (_slideRoutine != null) StopCoroutine(_slideRoutine);
        Vector2 hiddenPos = _restAnchoredPos + new Vector2(HideOffsetX, 0f);
        _slideRoutine = StartCoroutine(SlideTo(hiddenPos, duration, hideCanvasAtEnd: true));
    }

    private IEnumerator SlideTo(Vector2 target, float duration, bool hideCanvasAtEnd)
    {
        Vector2 start = _rootRt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            _rootRt.anchoredPosition = Vector2.Lerp(start, target, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        _rootRt.anchoredPosition = target;
        if (hideCanvasAtEnd) _canvasGo.SetActive(false);
    }

    // Chamado pelo clique em qualquer ponto do estado Compact.
    public void Expand()
    {
        if (_isExpanded) return;
        _isExpanded = true;
        RefreshAll();
        StopAllCoroutines();
        StartCoroutine(CrossFade(true));
    }

    // Chamado pelo clique em qualquer ponto do estado Expanded fora dos elementos interativos
    // (nenhum botão de aba aqui — só o próprio background captura o clique; ver bubbling
    // abaixo em BuildExpanded).
    public void Collapse()
    {
        if (!_isExpanded) return;
        _isExpanded = false;
        StopAllCoroutines();
        StartCoroutine(CrossFade(false));
    }

    private IEnumerator CrossFade(bool toExpanded)
    {
        // `_hideCompact` (2026-07-21): ao recolher (toExpanded=false), NÃO mostra o Compact —
        // showGo/showCg ficam nulos, então o trecho abaixo só desliga o Expanded (fade de saída),
        // sem nunca ativar/mostrar o bloco Compact. Sem efeito quando `_hideCompact=false` (todo
        // outro caller) — showGo/showCg continuam sendo `_compactGo`/`_compactCg` normalmente.
        GameObject showGo = toExpanded ? _expandedGo : (_hideCompact ? null : _compactGo);
        GameObject hideGo = toExpanded ? _compactGo  : _expandedGo;
        CanvasGroup showCg = toExpanded ? _expandedCg : (_hideCompact ? null : _compactCg);
        CanvasGroup hideCg = toExpanded ? _compactCg  : _expandedCg;

        if (showGo != null)
        {
            showGo.SetActive(true);
            showCg.interactable = false;
            showCg.blocksRaycasts = false;
        }

        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            float t = elapsed / FadeDuration;
            if (showCg != null) showCg.alpha = t;
            hideCg.alpha = 1f - t;
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (showCg != null) { showCg.alpha = 1f; showCg.interactable = true; showCg.blocksRaycasts = true; }
        hideCg.alpha = 0f;
        hideCg.interactable = false; hideCg.blocksRaycasts = false;
        hideGo.SetActive(false);
    }

    // ── UI Construction ─────────────────────────────────────────────────────

    // IMPORTANTE: o GameObject deste componente (`this.transform`, o pai de `_canvasGo` logo
    // abaixo) NUNCA pode ser parented sob outro Canvas (bug real, 2026-07-08) — `_canvasGo` só
    // funciona como um Canvas de verdade (ScreenSpaceOverlay, CanvasScaler 1920×1080 aplicado)
    // se for RAIZ. Um Canvas aninhado sob outro Canvas ignora seu próprio `renderMode` e
    // `CanvasScaler` (limitação documentada do Unity — herda tudo isso do ancestral), fazendo o
    // `Root` usar o RectTransform padrão (100×100) em vez de tela cheia: painel minúsculo e fora
    // do lugar. `CharacterSelectController`/`MainMenuController` sempre criam o GameObject do
    // CharacterPanel SEM chamar `SetParent` (fica raiz da cena) por causa disso.
    private void BuildUI()
    {
        _canvasGo = new GameObject("Canvas");
        _canvasGo.transform.SetParent(transform, false);
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _canvasGo.AddComponent<GraphicRaycaster>();

        // SafeArea (2026-07-20, ver Assets/Scripts/UI/SafeArea.cs) — só no modo gaveta-inferior:
        // o rodapé é exatamente onde a barra de gestos/home indicator de um celular real
        // atrapalharia. O painel lateral (modo de sempre) não precisa disso por enquanto.
        Transform rootParent = _canvasGo.transform;
        if (_bottomAnchored)
        {
            var safeAreaGo = new GameObject("SafeArea");
            safeAreaGo.transform.SetParent(_canvasGo.transform, false);
            var safeRt = safeAreaGo.AddComponent<RectTransform>();
            safeRt.anchorMin = Vector2.zero; safeRt.anchorMax = Vector2.one;
            safeRt.offsetMin = safeRt.offsetMax = Vector2.zero;
            safeAreaGo.AddComponent<SafeArea>();
            rootParent = safeAreaGo.transform;
            _safeAreaRt = safeRt;
        }

        var rootGo = new GameObject("Root");
        rootGo.transform.SetParent(rootParent, false);
        _rootRt = rootGo.AddComponent<RectTransform>();
        if (_bottomAnchored)
        {
            // Largura fixa BottomDrawerWidth, centralizada horizontalmente, ancorada no rodapé —
            // pedido do usuário: não ocupar a tela inteira (encaixa no vão entre a coluna
            // Chibers/Arsenal/Replays e o botão Jogar, que ficam nas bordas esquerda/direita).
            // Usa BottomDrawerWidth (não PanelWidth) pra bater exatamente com a largura real do
            // "Compact" (2026-07-20) — Expanded preenche 100% do Root, então precisa da MESMA
            // largura, senão fica mais estreito que o Compact ao expandir (bug reportado pelo
            // usuário).
            _rootRt.anchorMin = new Vector2(0.5f, 0f);
            _rootRt.anchorMax = new Vector2(0.5f, 0f);
            _rootRt.pivot = new Vector2(0.5f, 0f);
            _rootRt.sizeDelta = new Vector2(BottomDrawerWidth, BottomDrawerMaxHeight);
            _rootRt.anchoredPosition = Vector2.zero;
        }
        else
        {
            _rootRt.anchorMax = new Vector2(1f, _anchorTop);
            _rootRt.anchorMin = new Vector2(1f, _anchorBottom);
            _rootRt.offsetMin = new Vector2(-(PanelWidth + EdgeMargin), 0f);
            _rootRt.offsetMax = new Vector2(-EdgeMargin, 0f);
        }

        BuildCompact(rootGo);
        BuildExpanded(rootGo);

        // Estado inicial: compacto visível, expandido desligado (sem animação — só acontece
        // na primeira montagem da cena). `_hideCompact` (2026-07-21): nem o compacto aparece —
        // fica tudo escondido até o caller externo chamar Expand() (ver comentário em Setup).
        _compactGo.SetActive(!_hideCompact);
        _compactCg.alpha = _hideCompact ? 0f : 1f;
        _compactCg.interactable = !_hideCompact;
        _compactCg.blocksRaycasts = !_hideCompact;
        _expandedGo.SetActive(false);
        _expandedCg.alpha = 0f; _expandedCg.interactable = false; _expandedCg.blocksRaycasts = false;

        // Criado por último — sibling mais recente do canvasGo, desenha por cima do Root
        // inteiro (Compact/Expanded), independente de qual dos dois estiver ativo.
        BuildPopup(_canvasGo);
    }

    private void BuildCompact(GameObject root)
    {
        _compactGo = new GameObject("Compact");
        _compactGo.transform.SetParent(root.transform, false);
        var rt = _compactGo.AddComponent<RectTransform>();
        if (_bottomAnchored)
        {
            // Colado na BASE do Root (não no topo) — é daqui que a gaveta "cresce pra cima".
            // X preenche 100% do Root (2026-07-20, corrigido — Root já usa BottomDrawerWidth,
            // a mesma largura real que esta faixa tinha antes por offsets manuais; simplificado
            // pra só "encostar nas bordas" em vez de extrapolar o Root, senão Expanded — que
            // sempre preenche 100% do Root — ficava mais ESTREITO que o Compact ao expandir,
            // bug reportado pelo usuário). Y mantém o offset exato pedido (28px do chão, 230px
            // de altura) — só a largura mudou. BottomDrawerFloorGap (não mais o literal 28)
            // compartilhado com BuildExpanded — os dois precisam da MESMA base (ver comentário
            // da constante).
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0f, BottomDrawerFloorGap);
            rt.offsetMax = new Vector2(0f, BottomDrawerFloorGap + CompactHeight);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -(CompactHeight + LevelXpExtra)); rt.offsetMax = Vector2.zero;
        }
        _compactCg = _compactGo.AddComponent<CanvasGroup>();

        var bg = _compactGo.AddComponent<Image>();
        bg.sprite = UIShapeUtil.RoundedRect(PanelBgColor, 24f);
        bg.type = Image.Type.Sliced;

        var btn = _compactGo.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(Expand);

        // Sub-container do tamanho FIXO de sempre (CompactHeight) — BuildInfoBlock usa frações
        // relativas ao próprio container, então precisa continuar recebendo exatamente essa
        // altura pra renderizar idêntico ao 01_MainMenu; quem cresce é só o _compactGo por fora,
        // pra sobrar espaço pro LevelXp abaixo (ver BuildLevelXpBox).
        var infoInnerGo = new GameObject("InfoInner");
        infoInnerGo.transform.SetParent(_compactGo.transform, false);
        var iirt = infoInnerGo.AddComponent<RectTransform>();
        if (_bottomAnchored)
        {
            iirt.anchorMin = new Vector2(0f, 0f); iirt.anchorMax = new Vector2(1f, 0f);
            iirt.pivot     = new Vector2(0.5f, 0f);
            iirt.offsetMin = Vector2.zero; iirt.offsetMax = new Vector2(0f, CompactHeight);
        }
        else
        {
            iirt.anchorMin = new Vector2(0f, 1f); iirt.anchorMax = new Vector2(1f, 1f);
            iirt.pivot     = new Vector2(0.5f, 1f);
            iirt.offsetMin = new Vector2(0f, -CompactHeight); iirt.offsetMax = Vector2.zero;
        }
        _compactInfo = BuildInfoBlock(infoInnerGo);

        if (_showLevelXp) BuildLevelXpBox(_compactGo, CompactHeight, _compactInfo);
    }

    // Estado expandido: bloco de info (mesmo layout do Compact) no topo + Skills/Armas
    // roláveis abaixo. Clique em qualquer parte sem Button próprio borbulha até o Button do
    // fundo (Collapse) — arrastar pra rolar a lista não conta como clique (Unity distingue
    // drag de click), então rolar não recolhe por engano.
    private void BuildExpanded(GameObject root)
    {
        _expandedGo = new GameObject("Expanded");
        _expandedGo.transform.SetParent(root.transform, false);
        var rt = _expandedGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        if (_bottomAnchored)
        {
            // **Bug real corrigido (2026-07-20)**: antes usava offsetMin.y=0 (colado na base do
            // Root), enquanto o Compact sempre usou BottomDrawerFloorGap (28px) — a base VISÍVEL
            // do painel "descia" 28px ao expandir porque os dois estados não compartilhavam a
            // mesma linha de base. Root em si nunca muda de tamanho (ver BuildUI/BottomDrawerMaxHeight,
            // ancorado fixo na base) — a base do Expanded fica igual à do Compact, e o TOPO agora
            // ultrapassa o topo do Root em BottomDrawerExpandedTopOverflow (pedido do usuário —
            // ver comentário da constante), sobrepondo a fileira de energia quando expandido.
            //
            // **Bug real corrigido (2026-07-21)**: BottomDrawerExpandedTopOverflow é um valor
            // FIXO em pixels de referência (1920×1080) — como o Canvas usa ScaleWithScreenSize
            // travado pela LARGURA (matchWidthOrHeight=0, ver CLAUDE.md "Fase 7"), uma tela com
            // proporção mais "esticada"/larga que 16:9 (comum em celular moderno em paisagem,
            // ex: 20:9) tem MENOS altura disponível em unidades locais do Canvas do que 1080 —
            // o overflow fixo então empurrava o topo do Expanded pra além do teto real da tela,
            // cortando as skills/armas do topo da lista (reportado pelo usuário: "hoje ela
            // ultrapassa, não dando pra visualizar algumas skills no topo"). Clampa o overflow
            // pelo espaço REAL sobrando entre o topo do Root e o teto do SafeArea (que já reflete
            // a altura de tela disponível, lida direto do RectTransform — computada na hora, sem
            // precisar esperar um frame, já que SafeArea.Apply() já rodou no próprio Awake ao
            // adicionar o componente em BuildUI) — em telas pequenas o Expanded "acompanha o
            // teto" em vez de ultrapassá-lo; em telas grandes (16:9 ou mais estreitas que isso)
            // continua exatamente com os 348.4799px de sempre, sem mudar nada.
            float availableHeight = _safeAreaRt != null ? _safeAreaRt.rect.height : BottomDrawerMaxHeight;
            float maxOverflow = Mathf.Max(0f, availableHeight - BottomDrawerTopSafeMargin - BottomDrawerMaxHeight);
            float overflow = Mathf.Min(BottomDrawerExpandedTopOverflow, maxOverflow);
            rt.offsetMin = new Vector2(0f, BottomDrawerFloorGap);
            rt.offsetMax = new Vector2(0f, overflow);
        }
        else
        {
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        _expandedCg = _expandedGo.AddComponent<CanvasGroup>();

        var bg = _expandedGo.AddComponent<Image>();
        bg.sprite = UIShapeUtil.RoundedRect(PanelBgColor, 24f);
        bg.type = Image.Type.Sliced;

        var collapseBtn = _expandedGo.AddComponent<Button>();
        collapseBtn.targetGraphic = bg;
        collapseBtn.onClick.AddListener(Collapse);

        var infoGo = new GameObject("InfoBlock");
        infoGo.transform.SetParent(_expandedGo.transform, false);
        var irt = infoGo.AddComponent<RectTransform>();
        if (_bottomAnchored)
        {
            // Na base do Expanded (= base do Root) — mesmo lugar de sempre onde o Compact fica,
            // já que os dois representam o "mesmo" bloco de info, só um por cima do outro.
            irt.anchorMin = new Vector2(0f, 0f); irt.anchorMax = new Vector2(1f, 0f);
            irt.pivot     = new Vector2(0.5f, 0f);
            irt.offsetMin = Vector2.zero; irt.offsetMax = new Vector2(0f, CompactHeight);
        }
        else
        {
            irt.anchorMin = new Vector2(0f, 1f); irt.anchorMax = new Vector2(1f, 1f);
            irt.pivot     = new Vector2(0.5f, 1f);
            irt.offsetMin = new Vector2(0f, -CompactHeight); irt.offsetMax = Vector2.zero;
        }
        _expandedInfo = BuildInfoBlock(infoGo);

        if (_showLevelXp) BuildLevelXpBox(_expandedGo, CompactHeight, _expandedInfo);

        // Divider/ScrollArea deslocados pelo espaço extra do LevelXp (0 quando showLevelXp=false
        // — comportamento idêntico ao de sempre no 01_MainMenu/02_SelectCharacter).
        float belowInfo = CompactHeight + LevelXpExtra;

        var lineGo = new GameObject("Divider");
        lineGo.transform.SetParent(_expandedGo.transform, false);
        var lrt = lineGo.AddComponent<RectTransform>();
        if (_bottomAnchored)
        {
            // Logo ACIMA do InfoBlock (que fica na base) — no modo painel-lateral o Divider fica
            // abaixo do InfoBlock (que fica no topo); aqui é o espelho vertical disso.
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot     = new Vector2(0.5f, 0f);
            lrt.offsetMin = new Vector2(20f, belowInfo); lrt.offsetMax = new Vector2(-20f, belowInfo + 3f);
        }
        else
        {
            lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot     = new Vector2(0.5f, 1f);
            lrt.offsetMin = new Vector2(20f, -(belowInfo + 3f));
            lrt.offsetMax = new Vector2(-20f, -belowInfo);
        }
        lineGo.AddComponent<Image>().color = _theme.currencyGold;

        var scrollAreaGo = new GameObject("ScrollArea");
        scrollAreaGo.transform.SetParent(_expandedGo.transform, false);
        var srt = scrollAreaGo.AddComponent<RectTransform>();
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
        if (_bottomAnchored)
        {
            // Preenche de ABAIXO do topo do Expanded até logo acima do InfoBlock (que fica na
            // base) — Skills/Armas/Pets ficam ACIMA do nome/HP/STR/AGI/SPD, crescendo pra cima.
            srt.offsetMin = new Vector2(0f, belowInfo + 6f); srt.offsetMax = Vector2.zero;
        }
        else
        {
            srt.offsetMin = Vector2.zero; srt.offsetMax = new Vector2(0f, -(belowInfo + 6f));
        }
        BuildSkillsAndWeapons(scrollAreaGo);
    }

    // Mesmo estilo visual de CharacterCardUI.BuildLevelXpBar / MainMenuCharacterPreview.
    // BuildLevelXpHud (fundo sólido, "Level X" acima, barra grossa com "atual/necessário"
    // centralizada dentro dela) — só chamado quando _showLevelXp=true, logo abaixo do bloco de
    // info de sempre (nome/winrate/HP/pips), sem alterar esse bloco.
    private void BuildLevelXpBox(GameObject parent, float topOffset, InfoBlockRefs refs)
    {
        var boxGo = new GameObject("LevelXp");
        boxGo.transform.SetParent(parent.transform, false);
        var boxRt = boxGo.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0f, 1f); boxRt.anchorMax = new Vector2(1f, 1f);
        boxRt.pivot = new Vector2(0.5f, 1f);
        boxRt.offsetMin = new Vector2(20f, -(topOffset + LevelXpGap + LevelXpBoxHeight));
        boxRt.offsetMax = new Vector2(-20f, -(topOffset + LevelXpGap));
        var boxImg = boxGo.AddComponent<Image>();
        boxImg.sprite = UIShapeUtil.RoundedRect(new Color(0f, 0f, 0f, 0.35f), 10f);
        boxImg.type = Image.Type.Sliced;

        var lvlGo = new GameObject("LevelText");
        lvlGo.transform.SetParent(boxGo.transform, false);
        var lvlRt = lvlGo.AddComponent<RectTransform>();
        lvlRt.anchorMin = new Vector2(0.05f, 0.58f); lvlRt.anchorMax = new Vector2(0.95f, 0.98f);
        lvlRt.offsetMin = lvlRt.offsetMax = Vector2.zero;
        refs.level = lvlGo.AddComponent<TextMeshProUGUI>();
        refs.level.fontSize = 14; refs.level.fontStyle = FontStyles.Bold;
        refs.level.color = TextColor;
        refs.level.alignment = TextAlignmentOptions.Center;

        var barBgGo = new GameObject("BarBg");
        barBgGo.transform.SetParent(boxGo.transform, false);
        var barBgRt = barBgGo.AddComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0.06f, 0.06f); barBgRt.anchorMax = new Vector2(0.94f, 0.52f);
        barBgRt.offsetMin = barBgRt.offsetMax = Vector2.zero;
        var barBgImg = barBgGo.AddComponent<Image>();
        barBgImg.sprite = UIShapeUtil.RoundedRect(new Color(0f, 0f, 0f, 0.55f), 6f);
        barBgImg.type = Image.Type.Sliced;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(barBgGo.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        refs.xpFill = fillGo.AddComponent<Image>();
        refs.xpFill.sprite = UIShapeUtil.RoundedRect(_theme.currencyGold, 5f);
        refs.xpFill.type = Image.Type.Sliced;

        var xpTxtGo = new GameObject("XpText");
        xpTxtGo.transform.SetParent(barBgGo.transform, false);
        var xpRt = xpTxtGo.AddComponent<RectTransform>();
        xpRt.anchorMin = Vector2.zero; xpRt.anchorMax = Vector2.one;
        xpRt.offsetMin = xpRt.offsetMax = Vector2.zero;
        refs.xpValue = xpTxtGo.AddComponent<TextMeshProUGUI>();
        refs.xpValue.fontSize = 12; refs.xpValue.fontStyle = FontStyles.Bold;
        refs.xpValue.color = TextColor;
        refs.xpValue.alignment = TextAlignmentOptions.Center;
    }

    // Nome + Win Rate + HP + STR/AGI/SPD — mesmo bloco visual usado no Compact e no topo do
    // Expanded (`container` já vem dimensionado com CompactHeight de altura pelo chamador).
    private InfoBlockRefs BuildInfoBlock(GameObject container)
    {
        var refs = new InfoBlockRefs();

        if (_bottomAnchored)
        {
            // Gaveta mobile (2026-07-20, "ajustes finos" pedidos pelo usuário) — 4ª rodada: HP
            // voltou a NÃO usar pips (usuário pediu de volta o estilo ícone+número sobreposto,
            // BuildIconWithValue — mesmo componente do modo painel-lateral) — só 3 fileiras de
            // pips agora (STR/AGI/SPD, "os atributos" de verdade), dividindo o container INTEIRO
            // (0-1, sem faixa reservada pro header removido). HP é um quadrado pequeno
            // posicionado à ESQUERDA do ícone de AGI (pedido explícito), na MESMA faixa vertical
            // da fileira de AGI.
            // 2026-07-20 (2ª rodada): usuário reportou as 3 fileiras com alturas/larguras
            // aparentemente diferentes. Causa real: só a fileira de AGI tinha xMin empurrado pra
            // 0.16 (pra abrir espaço pro quadrado do HP), enquanto STR/SPD ficavam no xMin padrão
            // (0.05) — larguras diferentes, então os ícones/pips internos (que são frações da
            // largura da própria fileira) renderizavam em tamanhos diferentes entre si. Fix:
            // TODAS as 3 fileiras agora usam o mesmo xMin (attrXMin), ficando geometricamente
            // idênticas; o HP continua só na faixa vertical do AGI, ocupando a margem que sobra
            // à esquerda (0.01-0.14) sem sobrepor nenhuma das 3.
            const float attrIconScale = 2.5f;
            const float attrBadgeFontSize = 35f;
            // "diminua os badge em width 61.425 height 64.675" (pedido do usuário) — valores
            // exatos, não mais derivados de attrBadgeFontSize (que geraria um badge quadrado).
            const float attrBadgeWidth = 61.425f;
            const float attrBadgeHeight = 64.675f;
            const float attrXMin = 0.16f;

            refs.str = BuildPipRow(container, 0.68f, 0.96f, "STR", iconScale: attrIconScale,
                badgeFontSize: attrBadgeFontSize, badgeWidthOverride: attrBadgeWidth, badgeHeightOverride: attrBadgeHeight,
                xMin: attrXMin);
            refs.agi = BuildPipRow(container, 0.36f, 0.64f, "AGI", iconScale: attrIconScale,
                badgeFontSize: attrBadgeFontSize, badgeWidthOverride: attrBadgeWidth, badgeHeightOverride: attrBadgeHeight,
                xMin: attrXMin);
            refs.spd = BuildPipRow(container, 0.04f, 0.32f, "SPD", iconScale: attrIconScale,
                badgeFontSize: attrBadgeFontSize, badgeWidthOverride: attrBadgeWidth, badgeHeightOverride: attrBadgeHeight,
                xMin: attrXMin);

            // Nomes prefixados com "mobile" (2026-07-20) — C# não permite reusar `hpGo`/`hprt`
            // aqui: mesmo dentro deste `if`, um bloco ANINHADO não pode declarar um nome já usado
            // em QUALQUER lugar do bloco que o envolve (o método inteiro), mesmo que a outra
            // declaração (do modo painel-lateral, mais abaixo) venha depois textualmente — regra
            // de escopo do C#, não bug (CS0136).
            var mobileHpGo = new GameObject("Hp");
            mobileHpGo.transform.SetParent(container.transform, false);
            var mobileHpRt = mobileHpGo.AddComponent<RectTransform>();
            mobileHpRt.anchorMin = new Vector2(0.01f, 0.36f); mobileHpRt.anchorMax = new Vector2(0.14f, 0.64f);
            mobileHpRt.offsetMin = mobileHpRt.offsetMax = Vector2.zero;
            refs.hp = AttributePipBar.BuildIconWithValue(mobileHpGo, AttributePipBar.HpIcon, 32f, iconScale: 3.2f);

            return refs;
        }

        var headerGo = new GameObject("Header");
        headerGo.transform.SetParent(container.transform, false);
        var hrt = headerGo.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0.05f, 0.83f); hrt.anchorMax = new Vector2(0.95f, 0.99f);
        hrt.offsetMin = hrt.offsetMax = Vector2.zero;

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(headerGo.transform, false);
        var nrt = nameGo.AddComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(0.60f, 1f);
        nrt.offsetMin = nrt.offsetMax = Vector2.zero;
        refs.name = nameGo.AddComponent<TextMeshProUGUI>();
        refs.name.fontSize = 26;
        refs.name.color = TextColor;
        refs.name.fontStyle = FontStyles.Bold;
        refs.name.alignment = TextAlignmentOptions.MidlineLeft;

        var badgeGo = new GameObject("WinRateBadge");
        badgeGo.transform.SetParent(headerGo.transform, false);
        var brt = badgeGo.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.66f, 0.06f); brt.anchorMax = new Vector2(1f, 0.94f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        var badgeImg = badgeGo.AddComponent<Image>();
        badgeImg.sprite = UIShapeUtil.RoundedRect(_theme.success, 20f);
        badgeImg.type = Image.Type.Sliced;
        refs.winRate = AddLabel(badgeGo, "—%", 16, _theme.textOnLight);
        refs.winRate.fontStyle = FontStyles.Bold;

        // Coração + número branco centralizado (2026-07-16, pedido do usuário — substitui o
        // texto "N HP") — alinhado em X com o ícone de STR/AGI/SPD logo abaixo. Já ficava ACIMA
        // de STR/AGI/SPD por causa da faixa Y (0.63-0.80, contra 0.44-0.60 do STR).
        // **Bug real corrigido (2026-07-17)**: a 1ª tentativa (2026-07-16) só igualava a borda
        // ESQUERDA dos dois ícones (mesmo offset de 14px), mas usava uma largura FIXA em pixels
        // (34px) enquanto o ícone de STR/AGI/SPD (`AttributePipBar.Build`'s `lblGo`) usa uma
        // largura PROPORCIONAL (20% da linha, que mede 0.90 da largura do container) — como
        // `preserveAspect`+`localScale` centralizam e escalam o sprite em torno do CENTRO do
        // próprio rect, bordas esquerdas iguais com larguras diferentes produzem CENTROS
        // diferentes (o coração ficava ~16px à esquerda do centro real do ícone de STR, visível
        // a olho mesmo com a borda "alinhada" no código). Fix: em vez de replicar só o offset de
        // 14px, replicar a geometria PROPORCIONAL inteira de `lblGo` em espaço do `container`
        // (rowGo do STR é um stretch 0.05-0.95 com offset zero, então suas frações internas
        // mapeiam direto pra frações de `container`: anchorMin.x 0 vira 0.05, anchorMax.x 0.20
        // vira 0.05 + 0.20×0.90 = 0.23) — mesma proporção, mesmo offset de 14px, logo mesmo
        // centro renderizado do ícone, não importa a largura real do container em pixels.
        var hpGo = new GameObject("Hp");
        hpGo.transform.SetParent(container.transform, false);
        var hprt = hpGo.AddComponent<RectTransform>();
        hprt.anchorMin = new Vector2(0.05f, 0.63f); hprt.anchorMax = new Vector2(0.23f, 0.80f);
        hprt.offsetMin = new Vector2(14f, 0f);
        hprt.offsetMax = Vector2.zero;
        refs.hp = AttributePipBar.BuildIconWithValue(hpGo, AttributePipBar.HpIcon, 15f);

        refs.str = BuildPipRow(container, 0.44f, 0.60f, "STR");
        refs.agi = BuildPipRow(container, 0.24f, 0.40f, "AGI");
        refs.spd = BuildPipRow(container, 0.04f, 0.20f, "SPD");

        return refs;
    }

    private AttributePipBar BuildPipRow(GameObject container, float yMin, float yMax, string label,
        float iconScale = 2.5f, float badgeFontSize = 16f, float xMin = 0.05f,
        float? badgeWidthOverride = null, float? badgeHeightOverride = null)
    {
        var rowGo = new GameObject(label + "Row");
        rowGo.transform.SetParent(container.transform, false);
        var rt = rowGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(0.95f, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        // iconScale 2.5x (era 3x padrão) + badge/pips puxados pra mais perto do ícone (2026-07-18,
        // pedido do usuário — mesmo ajuste já feito em `05_SelectOpponent`/`MakeAttributeRow`,
        // aplicado aqui pra ficar consistente em toda tela que usa este `CharacterPanel`
        // compartilhado: `01_MainMenu` Compact/Expanded e o painel deslizante de
        // `02_SelectCharacter`). `iconScale`/`badgeFontSize`/`xMin` parametrizados (2026-07-20) —
        // a gaveta mobile passa valores próprios por linha, sem afetar
        // 02_SelectCharacter/05_SelectOpponent. `badgeWidth`/`badgeHeight` crescem com
        // `badgeFontSize` por padrão (proporção fixa), mas `badgeWidthOverride`/
        // `badgeHeightOverride` (2026-07-20, pedido explícito do usuário: "width 61.425 height
        // 64.675") tomam precedência quando informados, pra valores não-proporcionais exatos.
        float badgeWidth = badgeWidthOverride ?? 36f * (badgeFontSize / 16f);
        float badgeHeight = badgeHeightOverride ?? 36f * (badgeFontSize / 16f);
        return AttributePipBar.Build(rowGo, _theme, label,
            iconScale: iconScale, badgeAnchorX: 0.19f, pipsAnchorMinX: 0.34f,
            badgeWidth: badgeWidth, badgeHeight: badgeHeight, badgeFontSize: badgeFontSize);
    }

    // ── Skills / Armas (Expanded) ───────────────────────────────────────────

    private void BuildSkillsAndWeapons(GameObject parent)
    {
        MakeScroll(parent.transform, out Transform content);

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 12, 16);
        vlg.spacing = 10f;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MakeSectionTitle(content, "HABILIDADES");
        _skillsList = MakeIconGrid(content);
        _skillsEmpty = MakeMsg(content, "Nenhuma skill equipada ainda");

        MakeSectionTitle(content, "ARMAS");
        _armasList = MakeIconGrid(content);
        _armasEmpty = MakeMsg(content, "Sem armas equipadas");

        MakeSectionTitle(content, "PETS");
        _petsList = MakeIconGrid(content);
        _petsEmpty = MakeMsg(content, "Nenhum pet ainda");

        BuildDetailsToggle(content);
        BuildResetCharacterButton(content);
    }

    // Botão "REPLAYS" (2026-07-18, movido pro menu principal — MainMenuController.
    // BuildReplaysMenuButton, na mesma coluna de atalhos de "Chibers"/"Arsenal" — pedido do
    // usuário) chama isto de fora do painel. Abre o MESMO popup de detalhe de skill/arma
    // (_popupOverlayGo/_popupContentRoot/_popupPanelRt), só que com uma lista rolável dos últimos
    // replays salvos em vez de ícone+nome+descrição — funciona independente do painel estar
    // Compact ou Expanded (o popup é sibling de _canvasGo, não filho do Expanded).
    public void ShowReplays() => OnReplaysButtonClicked();

    // Aumentado (2026-07-18, pedido do usuário — "pegar mais da tela do main menu") de 480×520
    // pra ocupar bem mais da tela (canvas 1920×1080): dá espaço de sobra pros cartões maiores
    // abaixo (avatar/botão de play/fontes também aumentados), em vez de ficar apertado.
    private const float ReplayPopupWidth = 820f;
    private const float ReplayPopupHeight = 760f;

    private void OnReplaysButtonClicked()
    {
        int token = ++_replayLoadToken;
        ShowReplayListLoading();
        _ = LoadAndShowReplaysAsync(token);
    }

    // Monta o esqueleto do popup (título + área rolável com "Carregando...") — reaproveita
    // _popupOverlayGo/_popupContentRoot/_popupPanelRt (mesmo popup de ShowSkillDetail/
    // ShowWeaponDetail), só com um layout diferente (lista, não ícone+texto).
    private void ShowReplayListLoading()
    {
        ClearPopupContent();
        _popupOverlayGo.SetActive(true);
        _popupPanelRt.anchoredPosition = Vector2.zero;
        _popupPanelRt.sizeDelta = new Vector2(ReplayPopupWidth, ReplayPopupHeight);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(_popupContentRoot, false);
        var trt = titleGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = Vector2.zero;
        trt.sizeDelta = new Vector2(0f, 40f);
        var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "REPLAYS";
        titleTxt.fontSize = 32; titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = _theme.currencyGold;
        titleTxt.alignment = TextAlignmentOptions.Center;

        // Subtítulo (2026-07-19, pedido do usuário) — nome do personagem DONO deste histórico,
        // já que este mesmo popup é reaberto pra qualquer personagem selecionado no momento
        // (_overrideProfile em 02_SelectCharacter, ou _holder.currentProfile em 01_MainMenu —
        // mesma resolução de profile que LoadAndShowReplaysAsync usa pra montar a query).
        var ownerProfile = _overrideProfile != null ? _overrideProfile : _holder?.currentProfile;
        var subtitleGo = new GameObject("Subtitle");
        subtitleGo.transform.SetParent(_popupContentRoot, false);
        var subRt = subtitleGo.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 1f); subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -42f);
        subRt.sizeDelta = new Vector2(0f, 26f);
        var subTxt = subtitleGo.AddComponent<TextMeshProUGUI>();
        subTxt.text = ownerProfile != null ? ownerProfile.profileName : "";
        subTxt.fontSize = 18;
        subTxt.color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
        subTxt.alignment = TextAlignmentOptions.Center;

        var areaGo = new GameObject("ReplayListArea");
        areaGo.transform.SetParent(_popupContentRoot, false);
        var art = areaGo.AddComponent<RectTransform>();
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
        art.offsetMin = Vector2.zero;
        art.offsetMax = new Vector2(0f, -76f); // espaço reservado pro título + subtítulo acima

        MakeScroll(areaGo.transform, out Transform listContent);
        var vlg = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var csf = listContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _replayListContent = listContent;
        MakeMsg(_replayListContent, "Carregando...");
    }

    private async Task LoadAndShowReplaysAsync(int token)
    {
        var profile = _overrideProfile != null ? _overrideProfile : _holder?.currentProfile;
        if (profile == null) { RenderReplayMessage(token, "Nenhum personagem selecionado."); return; }
        if (!AuthService.IsSignedIn) { RenderReplayMessage(token, "Faça login pra ver seus replays."); return; }

        var replays = await FirestoreService.ListReplaysAsync(AuthService.CurrentUser.UserId, profile.OpponentId());

        // O popup pode ter sido fechado/reaberto (outro clique em REPLAYS, ou um ícone de skill/
        // arma) enquanto o await rodava — token muda a cada abertura e ClearPopupContent destrói
        // o _replayListContent antigo (comparação `== null` do Unity detecta objeto destruído).
        if (token != _replayLoadToken || _replayListContent == null) return;

        if (replays == null || replays.Count == 0)
        {
            RenderReplayMessage(token, "Nenhum replay salvo ainda.");
            return;
        }

        ClearReplayList();
        foreach (var (_, dto) in replays)
            BuildReplayRow(dto);
        BuildReplayFooter(replays.Count);
    }

    private void RenderReplayMessage(int token, string msg)
    {
        if (token != _replayLoadToken || _replayListContent == null) return;
        ClearReplayList();
        MakeMsg(_replayListContent, msg);
    }

    private void ClearReplayList()
    {
        for (int i = _replayListContent.childCount - 1; i >= 0; i--)
            Destroy(_replayListContent.GetChild(i).gameObject);
    }

    // Aumentados de novo (2026-07-18, pedido do usuário — "deixe visualizar 3 e meio de card no
    // primeiro momento"). Área visível da lista = ReplayPopupHeight×0.86 (frações do content root
    // em BuildPopup) − 56 (título) ≈ 597.6px com ReplayPopupHeight=760 — resolvendo
    // 3.5×H + 3×spacing(8) = 597.6 dá H≈164, que é o valor usado abaixo (3 cartões inteiros +
    // metade do 4º visíveis sem rolar). Eram 84/38/44 na rodada anterior.
    private const float ReplayCardHeight = 164f;
    private const float ReplayAvatarSize = 96f;
    private const float ReplayPlayButtonSize = 72f;

    // Cartão estilo "battle log" (2026-07-18, pedido do usuário — antes era 1 linha de texto cru)
    // — borda esquerda colorida por resultado, avatar circular do oponente, 2 linhas de texto e um
    // botão de play circular SEPARADO do cartão (sibling na Row, não filho dele, pra não competir
    // pelo mesmo fundo arredondado). Tudo posicionado manualmente (sem VerticalLayoutGroup
    // aninhado dentro do cartão) — mesma cautela de ShowSkillDetail/ShowWeaponDetail neste
    // arquivo, que já teve um bug real de espaçamento gigante com `childControlHeight=false` numa
    // VerticalLayoutGroup aninhada.
    private void BuildReplayRow(ReplayDTO dto)
    {
        bool won = dto.result == "win";
        Color accentColor = won ? _theme.success : _theme.danger;
        string dateLabel = new System.DateTime(dto.createdAtTicks).ToLocalTime().ToString("dd/MM HH:mm");
        // Duração em rounds REAIS (CombatSimulator.RoundCount, ver ReplayDTO.roundCount) — nunca
        // estimado. Replays salvos antes desta mudança não têm o campo (vem 0) — cai pra
        // eventCount, rotulado como "eventos" (não "rounds"), pra nunca fingir ser um round de
        // verdade.
        string durationLabel = dto.roundCount > 0 ? $"{dto.roundCount} rounds"
            : (dto.eventCount > 0 ? $"{dto.eventCount} eventos" : null);
        // Colorida (verde/vermelho, 2026-07-19, pedido do usuário — acessibilidade pra
        // daltonismo, não depender só da cor da faixa lateral como indicador de resultado). Só a
        // palavra em si — resto da linha (data/duração) continua no cinza neutro de sempre.
        string resultLabel = $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>{(won ? "Vitória" : "Derrota")}</color>";
        // Separador "|" (ASCII), não "·"/"—" — este mesmo arquivo já teve um bug real
        // (FormatTierTriplet) de caractere fora do ASCII básico virando glyph quebrado na fonte
        // TMP deste popup.
        string line2 = durationLabel == null
            ? $"{resultLabel} | {dateLabel}"
            : $"{resultLabel} | {dateLabel} | {durationLabel}";

        var rowGo = new GameObject("ReplayRow");
        rowGo.transform.SetParent(_replayListContent, false);
        rowGo.AddComponent<RectTransform>();
        var rowLe = rowGo.AddComponent<LayoutElement>();
        rowLe.preferredHeight = ReplayCardHeight; rowLe.flexibleWidth = 1f;

        // Cartão — ocupa a largura toda menos o espaço reservado pro botão de play à direita.
        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(rowGo.transform, false);
        var cardRt = cardGo.AddComponent<RectTransform>();
        cardRt.anchorMin = Vector2.zero; cardRt.anchorMax = Vector2.one;
        cardRt.offsetMin = Vector2.zero;
        cardRt.offsetMax = new Vector2(-(ReplayPlayButtonSize + 12f), 0f);
        var cardBg = cardGo.AddComponent<Image>();
        // Levemente mais claro que o fundo do próprio popup (panelBackgroundAlt) — mesmo truque
        // de tint em runtime já usado em CharacterCardButtonStyle (Color.Lerp com Color.white),
        // em vez de inventar um tom fixo novo no UITheme.
        cardBg.sprite = UIShapeUtil.RoundedRect(Color.Lerp(_theme.panelBackgroundAlt, Color.white, 0.12f), 8f);
        cardBg.type = Image.Type.Sliced;

        // Borda esquerda colorida — 3px, altura inteira do cartão.
        var borderGo = new GameObject("LeftBorder");
        borderGo.transform.SetParent(cardGo.transform, false);
        var borderRt = borderGo.AddComponent<RectTransform>();
        borderRt.anchorMin = new Vector2(0f, 0f); borderRt.anchorMax = new Vector2(0f, 1f);
        borderRt.pivot = new Vector2(0f, 0.5f);
        borderRt.sizeDelta = new Vector2(3f, 0f);
        borderRt.anchoredPosition = Vector2.zero;
        borderGo.AddComponent<Image>().color = accentColor;

        // Avatar circular — mesmo truque de círculo via UIShapeUtil.RoundedRect(raio = metade do
        // lado) já usado no badge de AttributePipBar; Mask recorta o ícone do oponente (se
        // resolvido) pro formato circular.
        var avatarGo = new GameObject("Avatar");
        avatarGo.transform.SetParent(cardGo.transform, false);
        var avatarRt = avatarGo.AddComponent<RectTransform>();
        avatarRt.anchorMin = avatarRt.anchorMax = new Vector2(0f, 0.5f);
        avatarRt.pivot = new Vector2(0f, 0.5f);
        avatarRt.sizeDelta = new Vector2(ReplayAvatarSize, ReplayAvatarSize);
        avatarRt.anchoredPosition = new Vector2(22f, 0f);

        Sprite opponentIcon = ResolveOpponentIcon(dto);
        var avatarBg = avatarGo.AddComponent<Image>();
        avatarBg.sprite = UIShapeUtil.RoundedRect(opponentIcon != null ? _theme.panelBackground : accentColor, ReplayAvatarSize / 2f);
        avatarBg.type = Image.Type.Sliced;
        var mask = avatarGo.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        if (opponentIcon != null)
        {
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(avatarGo.transform, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            // "Cover" (preenche o círculo inteiro, cortando o excesso via Mask do pai acima) —
            // Image.preserveAspect sozinho só faz "contain" (cabe dentro, podendo sobrar vão
            // vazio); calculado manualmente pra sempre cobrir os 38x38 inteiros, sem depender da
            // proporção real de cada previewIcon (varia por personagem).
            float spriteAspect = opponentIcon.rect.width / opponentIcon.rect.height;
            iconRt.sizeDelta = spriteAspect >= 1f
                ? new Vector2(ReplayAvatarSize * spriteAspect, ReplayAvatarSize)
                : new Vector2(ReplayAvatarSize, ReplayAvatarSize / spriteAspect);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = opponentIcon;
            iconImg.type = Image.Type.Simple;
        }
        else
        {
            // Fallback temporário (personagem removido/renomeado desde o replay, ou
            // CharacterDatabase não wireado) — letra V/D em vez de ícone de coroa/caveira: glyphs
            // Unicode de coroa/caveira não são seguros nesta fonte TMP (mesmo problema já
            // documentado com "—" em CharacterPanel.FormatTierTriplet — caractere fora do ASCII
            // básico virava "tofu" quebrado no popup).
            var letterGo = new GameObject("FallbackLetter");
            letterGo.transform.SetParent(avatarGo.transform, false);
            var letterRt = letterGo.AddComponent<RectTransform>();
            letterRt.anchorMin = Vector2.zero; letterRt.anchorMax = Vector2.one;
            letterRt.offsetMin = letterRt.offsetMax = Vector2.zero;
            var letterTxt = letterGo.AddComponent<TextMeshProUGUI>();
            letterTxt.text = won ? "V" : "D";
            letterTxt.fontSize = 40;
            letterTxt.fontStyle = FontStyles.Bold;
            letterTxt.color = _theme.textOnDark;
            letterTxt.alignment = TextAlignmentOptions.Center;
        }

        // Textos — 2 linhas empilhadas manualmente a partir do fim do avatar.
        float textLeft = 22f + ReplayAvatarSize + 20f;

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(cardGo.transform, false);
        var nameRt = nameGo.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0.5f); nameRt.anchorMax = new Vector2(1f, 0.5f);
        nameRt.pivot = new Vector2(0f, 0f);
        nameRt.offsetMin = new Vector2(textLeft, 14f); nameRt.offsetMax = new Vector2(-16f, 14f);
        nameRt.sizeDelta = new Vector2(0f, 40f);
        var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
        nameTxt.text = $"vs {dto.opponentName}";
        nameTxt.fontSize = 30;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = _theme.textOnDark;
        nameTxt.alignment = TextAlignmentOptions.BottomLeft;

        var line2Go = new GameObject("Line2");
        line2Go.transform.SetParent(cardGo.transform, false);
        var line2Rt = line2Go.AddComponent<RectTransform>();
        line2Rt.anchorMin = new Vector2(0f, 0.5f); line2Rt.anchorMax = new Vector2(1f, 0.5f);
        line2Rt.pivot = new Vector2(0f, 1f);
        line2Rt.offsetMin = new Vector2(textLeft, -14f); line2Rt.offsetMax = new Vector2(-16f, -14f);
        line2Rt.sizeDelta = new Vector2(0f, 30f);
        var line2Txt = line2Go.AddComponent<TextMeshProUGUI>();
        line2Txt.richText = true; // pra <color> de resultLabel acima renderizar de verdade
        line2Txt.text = line2;
        line2Txt.fontSize = 22;
        line2Txt.color = new Color(0.78f, 0.78f, 0.78f, 0.9f);
        line2Txt.alignment = TextAlignmentOptions.TopLeft;

        // Botão de play circular — sibling do Card dentro da Row (não filho dele), pinado na
        // borda direita: visualmente separado, mesmo pedido do usuário. Cor `secondaryButtonAlt`
        // (cinza-azulado) de propósito (2026-07-19, pedido do usuário) — não `primaryAction`/
        // `danger` (vermelhos), que já são os CTAs do X de fechar deste mesmo popup e do botão
        // JOGAR do menu; um 2º botão vermelho aqui competiria visualmente com esses.
        var playGo = new GameObject("PlayButton");
        playGo.transform.SetParent(rowGo.transform, false);
        var playRt = playGo.AddComponent<RectTransform>();
        playRt.anchorMin = playRt.anchorMax = new Vector2(1f, 0.5f);
        playRt.pivot = new Vector2(1f, 0.5f);
        playRt.sizeDelta = new Vector2(ReplayPlayButtonSize, ReplayPlayButtonSize);
        playRt.anchoredPosition = Vector2.zero;
        var playBg = playGo.AddComponent<Image>();
        playBg.sprite = UIShapeUtil.RoundedRect(_theme.secondaryButtonAlt, ReplayPlayButtonSize / 2f);
        playBg.type = Image.Type.Sliced;
        var playBtn = playGo.AddComponent<Button>();
        playBtn.targetGraphic = playBg;
        playBtn.onClick.AddListener(() => PlayReplay(dto));

        // Ícone de play triangular REAL (▶), rasterizado via UIShapeUtil.PlayTriangle — não um
        // glifo Unicode "▶" na fonte TMP (não seguro nesta fonte, mesmo risco já documentado com
        // "★"/"☆"/"—" neste projeto), nem o ">" ASCII usado antes (fácil de confundir com o "X"
        // de fechar do mesmo popup — pedido do usuário pra diferenciar melhor).
        var arrowGo = new GameObject("PlayIcon");
        arrowGo.transform.SetParent(playGo.transform, false);
        var arrowRt = arrowGo.AddComponent<RectTransform>();
        arrowRt.anchorMin = arrowRt.anchorMax = new Vector2(0.5f, 0.5f);
        float iconSize = ReplayPlayButtonSize * 0.55f;
        arrowRt.sizeDelta = new Vector2(iconSize, iconSize);
        var arrowImg = arrowGo.AddComponent<Image>();
        arrowImg.sprite = UIShapeUtil.PlayTriangle(_theme.textOnDark);
        arrowImg.type = Image.Type.Simple;
    }

    // Ícone do OPONENTE daquela luta — mesmo previewIcon já usado em 02_SelectCharacter/
    // MainMenuCharacterPreview para o mesmo personagem, casado por profileName no
    // CharacterDatabase (mesmo critério de ReplaySnapshotConverter.ToRuntimeProfile). null se o
    // personagem foi removido/renomeado desde o replay, ou se CharacterDatabase não foi wireado —
    // BuildReplayRow cai pro fallback de letra V/D nesse caso.
    private Sprite ResolveOpponentIcon(ReplayDTO dto)
    {
        if (_characterDatabase?.unlockedCharacters == null || dto?.p2Snapshot == null) return null;
        string name = dto.p2Snapshot.profileName;
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var t in _characterDatabase.unlockedCharacters)
            if (t != null && t.profileName == name) return t.previewIcon;
        return null;
    }

    // Rodapé "X de N replays salvos" — N vem de FirestoreService.MaxReplaysPerCharacter (mesmo
    // teto que a rotação client-side já mantém), não um número fixo solto aqui.
    private void BuildReplayFooter(int count)
    {
        var go = new GameObject("Footer");
        go.transform.SetParent(_replayListContent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 32f; le.flexibleWidth = 1f;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = $"{count} de {FirestoreService.MaxReplaysPerCharacter} replays salvos";
        txt.fontSize = 16;
        txt.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        txt.alignment = TextAlignmentOptions.Center;
    }

    // Reconstrói os dois PlayerProfile (ReplaySnapshotConverter) a partir do snapshot congelado,
    // seta o canal cross-scene dedicado (ReplayPlaybackState — NÃO SelectedProfileHolder/
    // SelectedOpponentHolder, que ficam intocados) e carrega 04_CombatScenePVP, que detecta
    // ReplayPlaybackState.IsActive e reproduz os eventos em vez de rodar CombatSimulator.
    private void PlayReplay(ReplayDTO dto)
    {
        if (_characterDatabase == null)
        {
            Debug.LogError("[CharacterPanel] CharacterDatabase não wireado — não é possível reproduzir replays (ver MainMenuController.characterDatabase).");
            return;
        }

        var p1 = ReplaySnapshotConverter.ToRuntimeProfile(dto.p1Snapshot, _characterDatabase);
        var p2 = ReplaySnapshotConverter.ToRuntimeProfile(dto.p2Snapshot, _characterDatabase);
        if (p1 == null || p2 == null)
        {
            Debug.LogError("[CharacterPanel] Não foi possível reconstruir os personagens deste replay (removido/renomeado desde que foi gravado?).");
            return;
        }

        var events = CombatEventReplayConverter.FromDTOList(dto.events);
        ReplayPlaybackState.Set(p1, p2, events);
        UnityEngine.SceneManagement.SceneManager.LoadScene("04_CombatScenePVP");
    }

    // Botão "Detalhes" — revela/esconde a seção PASSIVAS (label + valor, sem ícone/barra)
    // abaixo dela. Fica escondida por padrão; só constrói as linhas uma vez (RefreshPassives
    // só atualiza o texto depois).
    private void BuildDetailsToggle(Transform content)
    {
        var btnGo = new GameObject("DetailsButton");
        btnGo.transform.SetParent(content, false);
        btnGo.AddComponent<RectTransform>();
        var le = btnGo.AddComponent<LayoutElement>();
        // Fonte 40pt na gaveta mobile (2026-07-20, pedido do usuário — era 18pt) — altura do
        // botão cresce junto (44→64px) pra não cortar o texto maior.
        le.preferredHeight = _bottomAnchored ? 64f : 44f; le.flexibleWidth = 1f;
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = UIShapeUtil.RoundedRect(_theme.secondaryButton, 10f);
        btnImg.type = Image.Type.Sliced;
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(ToggleDetails);
        _detailsButtonLabel = AddLabel(btnGo, "VER DETALHES", _bottomAnchored ? 40 : 18, TextColor);
        _detailsButtonLabel.fontStyle = FontStyles.Bold;

        _passivesSection = new GameObject("PassivesSection");
        _passivesSection.transform.SetParent(content, false);
        _passivesSection.AddComponent<RectTransform>();
        var sectionLe = _passivesSection.AddComponent<LayoutElement>();
        sectionLe.flexibleWidth = 1f;
        var sectionVlg = _passivesSection.AddComponent<VerticalLayoutGroup>();
        sectionVlg.spacing = 10f;
        sectionVlg.childControlWidth = true; sectionVlg.childControlHeight = false;
        sectionVlg.childForceExpandWidth = true; sectionVlg.childForceExpandHeight = false;
        var sectionCsf = _passivesSection.AddComponent<ContentSizeFitter>();
        sectionCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MakeSectionTitle(_passivesSection.transform, "PASSIVAS");
        var passivesList = MakeListContainer(_passivesSection.transform);

        // Todas as passivas já existentes em PlayerProfile/GetEffectiveStats() que ainda não
        // apareciam em nenhum outro lugar da UI — valores efetivos (com skill já aplicada),
        // mesmo padrão de HP/STR/AGI/SPD no bloco de info. "Hit Speed" não tem cálculo efetivo
        // (nenhuma skill o modifica hoje), mostra o valor base direto de profile.hitSpeed.
        string[] passiveLabels =
        {
            "Evasion", "Counter", "Reverse", "Accuracy", "Armor", "Block",
            "Reversal After Block", "Critical Chance", "Critical Damage",
            "Combo Chance", "Disarm Chance", "Initiative", "Hit Speed",
        };
        foreach (var label in passiveLabels)
            _passiveRows[label] = BuildPassiveRow(passivesList, label);

        _passivesSection.SetActive(false);
    }

    // Profile atualmente exibido — mesma expressão usada em RefreshAll (_overrideProfile tem
    // prioridade sobre _holder.currentProfile, ver comentário em SetProfile) — extraída aqui pra
    // ser reaproveitada por fora de RefreshAll (ver OnResetCharacterClicked abaixo).
    private PlayerProfile CurrentProfile() => _overrideProfile != null ? _overrideProfile : _holder?.currentProfile;

    // "Resetar Personagem" (2026-07-21, pedido do usuário) — botão destrutivo no final do
    // Expanded (depois de Habilidades/Armas/Pets/Passivas), mesmo estilo/posição de
    // BuildDetailsToggle, cor `danger` pra sinalizar ação irreversível. Mecanismo PARALELO ao
    // "Reset de Build" do roadmap (ROADMAP_FUTURO.md Fase 4 — ainda não implementado, custaria
    // diamante e manteria o nível atual): este reseta pro Level 1 (mesma lógica de
    // Tools > AutoArms > Reset All Profiles to Level 1) e GERA moeda em vez de custar diamante.
    private void BuildResetCharacterButton(Transform content)
    {
        var btnGo = new GameObject("ResetCharacterButton");
        btnGo.transform.SetParent(content, false);
        btnGo.AddComponent<RectTransform>();
        var le = btnGo.AddComponent<LayoutElement>();
        le.preferredHeight = _bottomAnchored ? 64f : 44f; le.flexibleWidth = 1f;
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = UIShapeUtil.RoundedRect(_theme.danger, 10f);
        btnImg.type = Image.Type.Sliced;
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(OnResetCharacterClicked);
        var label = AddLabel(btnGo, "RESETAR PERSONAGEM", _bottomAnchored ? 40 : 18, TextColor);
        label.fontStyle = FontStyles.Bold;
    }

    private void OnResetCharacterClicked()
    {
        var profile = CurrentProfile();
        if (profile == null) return;

        // CharacterResetSettings (2026-07-21) — mesmo padrão de EnergySettings: ScriptableObject
        // em Assets/Resources/, carregado por Resources.Load sem precisar wirear no Inspector
        // (CharacterPanel é instanciado em 3 telas diferentes — 01_MainMenu/02_SelectCharacter/
        // 03_Arsenal — wirear um campo novo em todas exigiria editar as 3 cenas).
        var settings = Resources.Load<CharacterResetSettings>("CharacterResetSettings");
        int coinsPerLevel = settings != null ? settings.coinsPerLevel : 10;
        int coinsReward = profile.level * coinsPerLevel;

        ShowResetConfirmPopup(profile, coinsPerLevel, coinsReward);
    }

    // Confirmação explícita (pedido do usuário — "ação destrutiva, precisa de confirmação
    // explícita, não pode ser 1 clique só") — mesmo idioma visual de MainMenuController.
    // BuildPopup/ShopController.ShowPassInfoPopup (overlay+painel+texto+botões), construído sob
    // demanda e descartado ao fechar; canvas próprio com sortingOrder alto o bastante pra ficar
    // acima do popup de detalhe de skill/arma deste mesmo CharacterPanel (_canvasGo, sortingOrder
    // 20) e de qualquer outra coisa da tela onde o painel estiver sendo usado.
    private void ShowResetConfirmPopup(PlayerProfile profile, int coinsPerLevel, int coinsReward)
    {
        var canvasGo = new GameObject("ResetConfirmPopupCanvas (temp)");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var overlayGo = new GameObject("Overlay");
        overlayGo.transform.SetParent(canvasGo.transform, false);
        var overlayRt = overlayGo.AddComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero; overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = overlayRt.offsetMax = Vector2.zero;
        var overlayImg = overlayGo.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.7f);
        var overlayBtn = overlayGo.AddComponent<Button>();
        overlayBtn.targetGraphic = overlayImg;
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(() => Destroy(canvasGo)); // clicar fora cancela

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(680f, 420f);
        panelRt.anchoredPosition = Vector2.zero;
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.sprite = UIShapeUtil.RoundedRect(_theme.panelBackgroundAlt, 24f);
        panelImg.type = Image.Type.Sliced;
        var panelBtn = panelGo.AddComponent<Button>(); // sem onClick — só bloqueia o bubbling pro overlay
        panelBtn.targetGraphic = panelImg;

        var msgGo = new GameObject("Message");
        msgGo.transform.SetParent(panelGo.transform, false);
        var msgRt = msgGo.AddComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0.08f, 0.30f); msgRt.anchorMax = new Vector2(0.92f, 0.92f);
        msgRt.offsetMin = msgRt.offsetMax = Vector2.zero;
        var msgTxt = msgGo.AddComponent<TextMeshProUGUI>();
        msgTxt.text = $"Resetar {profile.profileName}?\n\n" +
            $"Você vai PERDER todo o progresso de nível, status, skills, armas e pets deste " +
            $"personagem, voltando ao Level 1.\n\n" +
            $"Em troca, recebe {coinsReward} moedas (Level {profile.level} × {coinsPerLevel} moedas/nível).\n\n" +
            $"Essa ação não pode ser desfeita.";
        msgTxt.fontSize = 24;
        msgTxt.color = _theme.textOnDark;
        msgTxt.alignment = TextAlignmentOptions.Center;
        msgTxt.enableWordWrapping = true;

        var confirmGo = new GameObject("BtnConfirm");
        confirmGo.transform.SetParent(panelGo.transform, false);
        var confirmRt = confirmGo.AddComponent<RectTransform>();
        confirmRt.anchorMin = confirmRt.anchorMax = new Vector2(0.73f, 0.14f);
        confirmRt.sizeDelta = new Vector2(280f, 64f);
        confirmRt.anchoredPosition = Vector2.zero;
        var confirmImg = confirmGo.AddComponent<Image>();
        confirmImg.sprite = UIShapeUtil.RoundedRect(_theme.danger, 14f);
        confirmImg.type = Image.Type.Sliced;
        var confirmBtn = confirmGo.AddComponent<Button>();
        confirmBtn.targetGraphic = confirmImg;
        confirmBtn.onClick.AddListener(() => { Destroy(canvasGo); ExecuteReset(profile, coinsReward); });
        AddLabel(confirmGo, "RESETAR", 20, _theme.textOnDark).fontStyle = FontStyles.Bold;

        var cancelGo = new GameObject("BtnCancel");
        cancelGo.transform.SetParent(panelGo.transform, false);
        var cancelRt = cancelGo.AddComponent<RectTransform>();
        cancelRt.anchorMin = cancelRt.anchorMax = new Vector2(0.27f, 0.14f);
        cancelRt.sizeDelta = new Vector2(280f, 64f);
        cancelRt.anchoredPosition = Vector2.zero;
        var cancelImg = cancelGo.AddComponent<Image>();
        cancelImg.sprite = UIShapeUtil.RoundedRect(_theme.secondaryButtonAlt, 14f);
        cancelImg.type = Image.Type.Sliced;
        var cancelBtn = cancelGo.AddComponent<Button>();
        cancelBtn.targetGraphic = cancelImg;
        cancelBtn.onClick.AddListener(() => Destroy(canvasGo));
        AddLabel(cancelGo, "CANCELAR", 20, _theme.textOnDark).fontStyle = FontStyles.Bold;
    }

    // Reseta o profile pro Level 1 — MESMA lógica de campos de
    // Tools > AutoArms > Reset All Profiles to Level 1 (Assets/Editor/CharacterCreationEditor.cs,
    // ferramenta de Editor já existente): level/xpCurrent/battlesRemaining/xpRequired +
    // HP/STR/AGI/SPD re-sorteados via CharacterCreation.GenerateLevel1Stats() + skills/pets/armas
    // zerados. Persistido de verdade via LocalSaveService.Save (local + Firestore, mesmo caminho
    // de sempre — ver ApplyBonus em CombatResultPanel pro mesmo padrão de EditorUtility.SetDirty
    // + LocalSaveService.Save). Moeda creditada em cima do saldo real (WalletService, mesmo
    // documento users/{uid} da Loja) quando há conta logada; sem conta, cai no fake local de
    // sempre (PlayerEconomyState.Coins), mesmo padrão de ShopController.OnBuyClicked.
    private void ExecuteReset(PlayerProfile profile, int coinsReward)
    {
        profile.level = 1;
        profile.xpCurrent = 0;
        profile.battlesRemaining = 6;
        profile.xpRequired = XpSystem.XpRequired(1);

        CharacterStats resetStats = CharacterCreation.GenerateLevel1Stats();
        profile.maxHealth = resetStats.maxHealth;
        profile.str = resetStats.str;
        profile.agility = resetStats.agility;
        profile.speed = resetStats.speed;

        profile.skills.Clear();
        profile.pets.Clear();
        profile.weapons.Clear();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(profile);
#endif
        LocalSaveService.Save(profile);

        if (AuthService.IsSignedIn) _ = WalletService.AddCoinsAsync(AuthService.CurrentUser.UserId, coinsReward);
        else PlayerEconomyState.Coins += coinsReward;

        RefreshAll();
    }

    private TMP_Text BuildPassiveRow(Transform parent, string label)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        // Fonte 35pt na gaveta mobile (2026-07-20, pedido do usuário — "aumente a fonte dos
        // detalhes evasion counter etc", era 17pt) — altura da linha cresce junto (26→54px).
        le.preferredHeight = _bottomAnchored ? 54f : 26f; le.flexibleWidth = 1f;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.fontSize = _bottomAnchored ? 35 : 17;
        txt.color = TextColor;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        return txt;
    }

    private void ToggleDetails()
    {
        _passivesVisible = !_passivesVisible;
        _passivesSection.SetActive(_passivesVisible);
        _detailsButtonLabel.text = _passivesVisible ? "OCULTAR DETALHES" : "VER DETALHES";
    }

    // Sub-container com seu próprio VerticalLayoutGroup — o grupo do `content` pai trata ele
    // como um bloco só, cuja altura vem do ContentSizeFitter interno (soma das linhas).
    private static Transform MakeListContainer(Transform parent)
    {
        var go = new GameObject("List");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go.transform;
    }

    // Grade de células quadradas (ícone + borda de tier) — substitui a lista vertical de
    // ícone+nome (2026-07-07: nome removido, só o ícone com a cor de borda importa agora).
    private Transform MakeIconGrid(Transform parent)
    {
        // Célula maior na gaveta mobile (2026-07-20, pedido do usuário — ícones pequenos demais
        // na lista de Habilidades/Armas): 70→110px, só quando _bottomAnchored (painel bem mais
        // largo agora, ver BottomDrawerWidth) — 02_SelectCharacter/03_Arsenal continuam em 70px.
        // 2026-07-20 (2ª rodada): dobrado de novo (110→220) e espaçamento reduzido para agrupar
        // os ícones. Em 220px, 5 colunas não cabem na largura do painel (BottomDrawerWidth), então
        // constraintCount cai para 3 só no modo mobile — 02_SelectCharacter mantém 5 colunas de 70px.
        float cellSize = _bottomAnchored ? 220f : 70f;
        float spacing = _bottomAnchored ? 6f : 8f;

        var go = new GameObject("Grid");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        var glg = go.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(cellSize, cellSize);
        glg.spacing = new Vector2(spacing, spacing);
        glg.childAlignment = TextAnchor.UpperLeft;
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = _bottomAnchored ? 3 : 5;
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go.transform;
    }

    // Cor da borda por tier — T1 bronze, T2 prata, T3 ouro (ver UITheme). Qualquer tier fora de
    // 1-3 (não existe hoje) cai no tom mais alto (ouro).
    private Color TierColor(int tier)
    {
        switch (tier)
        {
            case 1: return _theme.tierBronze;
            case 2: return _theme.tierSilver;
            default: return _theme.tierGold;
        }
    }

    // Célula quadrada só com ícone + borda colorida por tier — usada tanto pra Skills quanto
    // pra Armas, e reaproveitada (2026-07-07) pelo ícone no topo do popup de detalhe (mesmo
    // componente/lógica, sem recriar um sistema de borda separado). Clicar abre o popup de
    // descrição/status (onClick) — no popup, chamada com onClick=null (Button sem listener,
    // igual ao painel do popup que também bloqueia bubbling sem ação própria).
    private GameObject BuildTierIconCell(Transform parent, Sprite icon, int tier, UnityEngine.Events.UnityAction onClick)
    {
        var cellGo = new GameObject("IconCell");
        cellGo.transform.SetParent(parent, false);
        cellGo.AddComponent<RectTransform>();

        var borderImg = cellGo.AddComponent<Image>();
        borderImg.sprite = UIShapeUtil.RoundedRect(TierColor(tier), 10f);
        borderImg.type = Image.Type.Sliced;

        var btn = cellGo.AddComponent<Button>();
        btn.targetGraphic = borderImg;
        if (onClick != null) btn.onClick.AddListener(onClick);

        var bgGo = new GameObject("Bg");
        bgGo.transform.SetParent(cellGo.transform, false);
        var bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.08f, 0.08f); bgRt.anchorMax = new Vector2(0.92f, 0.92f);
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = UIShapeUtil.RoundedRect(PanelBgColor, 8f);
        bgImg.type = Image.Type.Sliced;

        if (icon == null) return cellGo;
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(cellGo.transform, false);
        var irt = iconGo.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.18f, 0.18f); irt.anchorMax = new Vector2(0.82f, 0.82f);
        irt.offsetMin = irt.offsetMax = Vector2.zero;
        var img = iconGo.AddComponent<Image>();
        img.sprite = icon; img.preserveAspect = true;
        return cellGo;
    }

    // Ícone com borda de tier ancorado no topo-centro do popup (96×96) — só posiciona o
    // GameObject que BuildTierIconCell já constrói, sem duplicar nada da lógica de borda/tier.
    // 2026-07-20, pedido do usuário: ícone do popup de detalhe proporcional ao resto do
    // conteúdo na gaveta mobile (antes ficava pequeno perto do título/descrição maiores) —
    // 96→170px só quando _bottomAnchored; 02_SelectCharacter/03_Arsenal mantêm 96px.
    private float PopupIconSize => _bottomAnchored ? 170f : 96f;

    private void BuildPopupIcon(Sprite icon, int tier)
    {
        var cellGo = BuildTierIconCell(_popupContentRoot, icon, tier, null);
        var rt = cellGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(PopupIconSize, PopupIconSize);
    }

    // ── Popup de descrição (Skills/Armas) ───────────────────────────────────

    // Overlay escuro (clique fora fecha) + painel central + X. O painel tem seu próprio Button
    // sem listener — só pra interceptar o clique e não deixar borbulhar até o Button do Overlay,
    // mesmo truque já usado no Expanded pra diferenciar "clique dentro" de "clique fora". O
    // conteúdo (`_popupContentRoot`) é reconstruído do zero a cada Show — skill e arma têm
    // layouts diferentes (nome+descrição vs ícone+nome+stats), então não faz sentido um único
    // conjunto fixo de campos pra ambos, como tinha antes.
    private void BuildPopup(GameObject canvasParent)
    {
        _popupOverlayGo = new GameObject("PopupOverlay");
        _popupOverlayGo.transform.SetParent(canvasParent.transform, false);
        var ort = _popupOverlayGo.AddComponent<RectTransform>();
        Stretch(ort);
        var oImg = _popupOverlayGo.AddComponent<Image>();
        oImg.color = new Color(0f, 0f, 0f, 0.6f);
        var oBtn = _popupOverlayGo.AddComponent<Button>();
        oBtn.targetGraphic = oImg;
        oBtn.onClick.AddListener(CloseDetailPopup);

        var panelGo = new GameObject("PopupPanel");
        panelGo.transform.SetParent(_popupOverlayGo.transform, false);
        _popupPanelRt = panelGo.AddComponent<RectTransform>();
        _popupPanelRt.anchorMin = _popupPanelRt.anchorMax = new Vector2(0.5f, 0.5f);
        _popupPanelRt.sizeDelta = new Vector2(420f, 300f);
        var pImg = panelGo.AddComponent<Image>();
        pImg.sprite = UIShapeUtil.RoundedRect(_theme.panelBackgroundAlt, 24f);
        pImg.type = Image.Type.Sliced;
        var pBtn = panelGo.AddComponent<Button>(); // sem onClick — só bloqueia o bubbling
        pBtn.targetGraphic = pImg;

        // Botão de fechar (X), mesmo estilo do antigo close button do painel lateral.
        var closeGo = new GameObject("CloseBtn");
        closeGo.transform.SetParent(panelGo.transform, false);
        var crt = closeGo.AddComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.offsetMin = new Vector2(-52f, -52f);
        crt.offsetMax = new Vector2(-8f, -8f);
        var danger = _theme.danger;
        closeGo.AddComponent<Image>().color = new Color(danger.r, danger.g, danger.b, 0.92f);
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseDetailPopup);
        AddLabel(closeGo, "X", 22, TextColor).fontStyle = FontStyles.Bold;

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(panelGo.transform, false);
        var content = contentGo.AddComponent<RectTransform>();
        content.anchorMin = new Vector2(0.06f, 0.04f); content.anchorMax = new Vector2(0.94f, 0.90f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        _popupContentRoot = content;

        _popupOverlayGo.SetActive(false);
    }

    private void ClearPopupContent()
    {
        for (int i = _popupContentRoot.childCount - 1; i >= 0; i--)
            Destroy(_popupContentRoot.GetChild(i).gameObject);
    }

    private void CloseDetailPopup()
    {
        _popupOverlayGo.SetActive(false);
    }

    // 2026-07-20, pedido do usuário: popup de detalhe (skill/arma/pet) maior no geral, gaveta
    // mobile — deixaram de ser `const` pra poder variar por `_bottomAnchored` sem afetar
    // 02_SelectCharacter/03_Arsenal (painel-lateral continua nos valores originais).
    // 2ª rodada (2026-07-20): painel aumentado de novo (620→780) pra caber o ícone/fontes
    // maiores sem apertar o texto.
    private float SkillPopupWidth => _bottomAnchored ? 780f : 420f;
    private float SkillPopupMinHeight => _bottomAnchored ? 520f : 300f;
    // Frações reais de _popupContentRoot dentro do painel (ver anchors em BuildPopup:
    // 0.06-0.94 horizontal, 0.04-0.90 vertical) — usadas pra converter altura de CONTEÚDO
    // (onde o texto é medido/posicionado) em altura de PAINEL (`_popupPanelRt.sizeDelta`).
    private const float SkillPopupContentWidthFraction  = 0.88f;
    private const float SkillPopupContentHeightFraction = 0.86f;
    // Espaço reservado pro ícone+nome no topo. Não-const: na gaveta mobile o ícone (PopupIconSize)
    // e o nome (fonte 40, ver ShowSkillDetail/ShowPetDetail) são maiores, então a área reservada
    // cresce junto — 96(ícone)+4+36(nome)+8 = 144 no desktop; 170+14+54+12 = 250 no mobile.
    private float SkillPopupHeaderHeight => _bottomAnchored ? 250f : 144f;
    private const float SkillPopupDescEffectGap       = 14f;  // espaço entre a descrição e o bloco "Efeito"
    private float SkillPopupEffectLabelHeight => _bottomAnchored ? 28f : 22f; // acompanha a fonte maior do label "Efeito" na gaveta mobile
    private const float SkillPopupEffectLabelValueGap = 4f;
    private const float SkillPopupBottomPadding       = 20f;
    // Offset/altura do bloco de nome — cresce junto com PopupIconSize e a fonte maior (40) na
    // gaveta mobile (ver ShowSkillDetail/ShowPetDetail).
    private float SkillPopupNameOffsetY => _bottomAnchored ? -(PopupIconSize + 14f) : -100f;
    private float SkillPopupNameHeight => _bottomAnchored ? 54f : 36f;

    // Layout: ícone (com borda de tier, reaproveitando BuildTierIconCell — 2026-07-07,
    // substitui o antigo sufixo de texto "(T{tier})" no título) + nome + descrição corrida
    // (SkillData.description, texto temático) + linha "Efeito" (SkillData.effectText, formato
    // de colchetes [T1/T2/T3] com o tier equipado destacado, mesma cor de destaque do popup de
    // arma) — omitida pra skills sem effectText (Garimpeiro/Magneto, ainda não implementadas).
    // Altura do painel calculada a partir da altura REAL do texto (`GetPreferredValues`, mesma
    // largura que o texto vai ocupar de fato) em vez de um valor fixo — skills com descrição
    // longa (ex: Shield, Lead Skeleton, Deity) vazavam pra fora da caixa com a altura fixa
    // anterior. Posicionamento 100% manual (sem VerticalLayoutGroup/LayoutElement): o popup de
    // arma já teve um bug real de espaçamento gigante entre linhas causado por
    // `childControlHeight=false` numa VerticalLayoutGroup (ver ShowWeaponDetail) — evitado aqui
    // não usando layout automático nenhum pro texto da skill.
    // Público (2026-07-14) — ArsenalController (03_Arsenal) reaproveita o MESMO popup pra
    // mostrar os atributos de qualquer arma/skill do jogo (não só as equipadas do personagem),
    // inclusive quando bloqueada/não possuída — evita duplicar toda essa lógica de popup numa
    // 2ª tela. Ver CharacterPanel.HideRootPermanently, usado pra esconder o HUD Compact/Expanded
    // deste painel quando instanciado só pelo popup.
    public void ShowSkillDetail(SkillData skill)
    {
        ClearPopupContent();
        // Ativa o popup ANTES de criar/medir os textos novos: `_popupOverlayGo` começa/fica
        // desativado entre uma abertura e outra (ver CloseDetailPopup), e um GameObject
        // desativado na hierarquia nunca roda Awake() dos componentes recém-adicionados —
        // TMP_Text.Awake() é o que resolve fontAsset/material internos. Chamar
        // GetPreferredValues() nesse estado (bug real, 2026-07-07) estourava
        // NullReferenceException dentro de TMPro.MaterialReference..ctor, porque o fontAsset
        // do TMP_Text recém-criado ainda não tinha sido inicializado.
        _popupOverlayGo.SetActive(true);

        bool hasEffect = !string.IsNullOrEmpty(skill.effectText);
        float contentWidthPx = SkillPopupWidth * SkillPopupContentWidthFraction;

        var iconSource = skill;
        while (iconSource != null && iconSource.icon == null) iconSource = iconSource.previousTier;
        BuildPopupIcon(iconSource != null ? iconSource.icon : null, skill.tier);

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(_popupContentRoot, false);
        var nrt = nameGo.AddComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(0.5f, 1f);
        nrt.anchoredPosition = new Vector2(0f, SkillPopupNameOffsetY);
        nrt.sizeDelta = new Vector2(0f, SkillPopupNameHeight);
        var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
        nameTxt.text = skill.skillName; nameTxt.fontSize = _bottomAnchored ? 40 : 28; nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = _theme.currencyGold;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.richText = true;

        // Descrição — cria o texto e mede a altura real que ele vai ocupar (com quebra de
        // linha) na largura disponível, antes de decidir o tamanho do painel. Fonte maior na
        // gaveta mobile (2026-07-20, pedido do usuário — "aumentar a fonte do texto de
        // descrição, hoje pequena demais pra ler confortavelmente no mobile"; 2ª rodada:
        // 28→35).
        string desc = string.IsNullOrEmpty(skill.description) ? "Sem descrição disponível." : skill.description;
        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(_popupContentRoot, false);
        bodyGo.AddComponent<RectTransform>();
        var bodyTxt = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyTxt.enableWordWrapping = true;
        bodyTxt.fontSize = _bottomAnchored ? 35 : 20;
        bodyTxt.color = TextColor;
        bodyTxt.alignment = TextAlignmentOptions.TopLeft;
        bodyTxt.text = desc;
        float descHeight = bodyTxt.GetPreferredValues(contentWidthPx, 0f).y;

        // Efeito — mesma ideia: mede a altura real do valor formatado (pode ter mais de uma
        // linha em skills com texto de efeito longo, ex: Shield).
        TextMeshProUGUI effectValueTxt = null;
        float effectValueHeight = 0f;
        if (hasEffect)
        {
            var valueGo = new GameObject("EffectValue");
            valueGo.transform.SetParent(_popupContentRoot, false);
            valueGo.AddComponent<RectTransform>();
            effectValueTxt = valueGo.AddComponent<TextMeshProUGUI>();
            effectValueTxt.richText = true;
            effectValueTxt.enableWordWrapping = true;
            effectValueTxt.fontSize = _bottomAnchored ? 24 : 18;
            effectValueTxt.color = TextColor;
            effectValueTxt.alignment = TextAlignmentOptions.TopLeft;
            effectValueTxt.text = HighlightEffectTiers(skill.effectText, skill.tier);
            effectValueHeight = effectValueTxt.GetPreferredValues(contentWidthPx, 0f).y;
        }

        float contentHeight = SkillPopupHeaderHeight + descHeight
            + (hasEffect ? SkillPopupDescEffectGap + SkillPopupEffectLabelHeight + SkillPopupEffectLabelValueGap + effectValueHeight : 0f)
            + SkillPopupBottomPadding;
        float panelHeight = Mathf.Max(contentHeight / SkillPopupContentHeightFraction, SkillPopupMinHeight);
        _popupPanelRt.sizeDelta = new Vector2(SkillPopupWidth, panelHeight);
        _popupPanelRt.anchoredPosition = Vector2.zero; // centralizado — só o popup de arma sobe (ver ShowWeaponDetail)

        // Só agora (com a altura final do texto conhecida) posiciona a descrição e o efeito,
        // um embaixo do outro a partir do topo do conteúdo.
        var brt = bodyGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0f, -SkillPopupHeaderHeight);
        brt.sizeDelta = new Vector2(0f, descHeight);

        if (hasEffect)
        {
            float labelY = SkillPopupHeaderHeight + descHeight + SkillPopupDescEffectGap;
            var labelGo = new GameObject("EffectLabel");
            labelGo.transform.SetParent(_popupContentRoot, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, -labelY);
            lrt.sizeDelta = new Vector2(0f, SkillPopupEffectLabelHeight);
            var labelTxt = labelGo.AddComponent<TextMeshProUGUI>();
            labelTxt.text = "Efeito"; labelTxt.fontSize = _bottomAnchored ? 22 : 18; labelTxt.fontStyle = FontStyles.Bold;
            labelTxt.color = _theme.currencyGold;
            labelTxt.alignment = TextAlignmentOptions.TopLeft;

            float valueY = labelY + SkillPopupEffectLabelHeight + SkillPopupEffectLabelValueGap;
            var vrt = effectValueTxt.GetComponent<RectTransform>();
            vrt.anchorMin = new Vector2(0f, 1f); vrt.anchorMax = new Vector2(1f, 1f);
            vrt.pivot = new Vector2(0.5f, 1f);
            vrt.anchoredPosition = new Vector2(0f, -valueY);
            vrt.sizeDelta = new Vector2(0f, effectValueHeight);
        }
    }

    // Popup mínimo pra pets (2026-07-16, pedido do usuário — "quero os 3 tipos [arma/skill/pet]
    // com o mesmo nível de detalhe" na tela de oponentes) — reaproveita a MESMA infraestrutura de
    // popup (overlay, painel, `BuildPopupIcon`, medição de altura dinâmica via
    // `GetPreferredValues`, mesmas constantes `SkillPopup*`), só o corpo do texto muda pra stats
    // formatados em vez de description/effectText, já que não existe `description`/`effectText`
    // em `PetData` (pet não tem texto temático, só números). `icon` vem de quem chama
    // (`SelectOpponentController` tem os sprites de pet, `CharacterPanel` não).
    // Reescrito (2026-07-21, pedido do usuário — formato "card de referência" My Brute). Mesmo
    // layout/constantes do popup de arma (ícone+nome+linhas de stat, sem scroll, altura dinâmica
    // calculada pela contagem real de linhas) em vez do parágrafo corrido de antes — reaproveita
    // ResolveTierFamily/FormatTierTriplet/AddPopupStatRow/AddTieredBonusRow (este último já era
    // 100% genérico, sem nenhuma referência a WeaponData no corpo, então serve pra pet sem
    // duplicar nada). Odds/HP malus/Initiative são FIXOS por TIPO de pet — não escalam por tier
    // (ver PETS.md), por isso aparecem como valor único, não tripla `[T1/T2/T3]` (diferente de
    // STR/AGI/SPD/HP, que escalam de verdade). Sem linha "Dano" — campo `damage` removido de
    // `PetData` (dano deriva de STR direto, ver PETS.md). Bônus especiais nomeados
    // (comboRate/evasionBase/accuracyBonus/disarmRate/comboDebuff/blockDebuff) só aparecem se
    // != 0 pra esse pet (`AddTieredBonusRow`) — mesma regra "só o que esse pet realmente usa"
    // (Javali mostra os 6, Macaco 2, Rato 1), sem precisar checar `petType` explicitamente.
    public void ShowPetDetail(PetData data, Sprite icon)
    {
        ClearPopupContent();
        _popupOverlayGo.SetActive(true);

        if (data == null)
        {
            BuildPopupIcon(icon, 3);
            var missingGo = new GameObject("Body");
            missingGo.transform.SetParent(_popupContentRoot, false);
            missingGo.AddComponent<RectTransform>();
            var missingTxt = missingGo.AddComponent<TextMeshProUGUI>();
            missingTxt.text = "Sem dados disponíveis.";
            missingTxt.fontSize = _bottomAnchored ? 35 : 20;
            missingTxt.color = TextColor;
            missingTxt.alignment = TextAlignmentOptions.Center;
            _popupPanelRt.sizeDelta = new Vector2(WeaponPopupWidth, WeaponPopupMinHeight);
            _popupPanelRt.anchoredPosition = Vector2.zero;
            return;
        }

        var (t1, t2, t3) = ResolveTierFamily(data);

        int rowCount = 7 + CountActivePetBonusRows(data); // Odds/HP malus/Initiative/STR/AGI/SPD/HP + bônus condicionais
        float statsHeight = rowCount * WeaponPopupStatRowHeight + Mathf.Max(0, rowCount - 1) * WeaponPopupStatRowSpacing;
        float contentHeight = WeaponPopupHeaderHeight + statsHeight + 56f;
        float panelHeight = Mathf.Max(contentHeight / 0.86f, WeaponPopupMinHeight);
        _popupPanelRt.sizeDelta = new Vector2(WeaponPopupWidth, panelHeight);
        _popupPanelRt.anchoredPosition = Vector2.zero;

        BuildPopupIcon(icon, data.tier);

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(_popupContentRoot, false);
        var nrt = nameGo.AddComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(0.5f, 1f);
        nrt.anchoredPosition = new Vector2(0f, SkillPopupNameOffsetY);
        nrt.sizeDelta = new Vector2(0f, SkillPopupNameHeight);
        var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
        nameTxt.text = PetState.DisplayName(data.petType);
        nameTxt.fontSize = _bottomAnchored ? 40 : 28; nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = _theme.currencyGold;
        nameTxt.alignment = TextAlignmentOptions.Center;

        var statsAreaGo = new GameObject("StatsArea");
        statsAreaGo.transform.SetParent(_popupContentRoot, false);
        var saRt = statsAreaGo.AddComponent<RectTransform>();
        saRt.anchorMin = new Vector2(0f, 0f); saRt.anchorMax = new Vector2(1f, 1f);
        saRt.offsetMin = Vector2.zero; saRt.offsetMax = new Vector2(0f, -WeaponPopupHeaderHeight);
        var vlg = statsAreaGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = WeaponPopupStatRowSpacing;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        Transform statsContent = statsAreaGo.transform;

        Color orange = _theme.primaryActionAlt;

        // Odds já vem armazenado em pontos percentuais no asset (1.92 = "1.92%", não 0.0192 —
        // confirmado nos 9 .asset em disco), diferente de HP malus (fração 0-1, :P0 correto).
        AddPopupStatRow(statsContent, "Odds", $"{data.odds:0.##}%");
        AddPopupStatRow(statsContent, "HP malus", $"{data.hpMalusPercent:P0}");
        AddPopupStatRow(statsContent, "Initiative", $"{data.initiative:0}");
        AddPopupStatRow(statsContent, "Strength",
            FormatTierTriplet(t1?.str, t2?.str, t3?.str, data.tier, v => $"{v:0}", orange));
        AddPopupStatRow(statsContent, "Agility",
            FormatTierTriplet(NullableInt(t1?.agility), NullableInt(t2?.agility), NullableInt(t3?.agility), data.tier, v => $"{v:0}", orange));
        AddPopupStatRow(statsContent, "Speed",
            FormatTierTriplet(NullableInt(t1?.speed), NullableInt(t2?.speed), NullableInt(t3?.speed), data.tier, v => $"{v:0}", orange));
        AddPopupStatRow(statsContent, "HP",
            FormatTierTriplet(NullableInt(t1?.hp), NullableInt(t2?.hp), NullableInt(t3?.hp), data.tier, v => $"{v:0}", orange));

        AddTieredBonusRow(statsContent, "Combo",              data.comboRate,    t1?.comboRate,    t2?.comboRate,    t3?.comboRate,    data.tier);
        AddTieredBonusRow(statsContent, "Evasão",             data.evasionBase,  t1?.evasionBase,  t2?.evasionBase,  t3?.evasionBase,  data.tier);
        AddTieredBonusRow(statsContent, "Precisão",           data.accuracyBonus,t1?.accuracyBonus,t2?.accuracyBonus,t3?.accuracyBonus,data.tier);
        AddTieredBonusRow(statsContent, "Desarme",            data.disarmRate,   t1?.disarmRate,   t2?.disarmRate,   t3?.disarmRate,   data.tier);
        AddTieredBonusRow(statsContent, "Combo do oponente",  data.comboDebuff,  t1?.comboDebuff,  t2?.comboDebuff,  t3?.comboDebuff,  data.tier);
        AddTieredBonusRow(statsContent, "Block do oponente",  data.blockDebuff,  t1?.blockDebuff,  t2?.blockDebuff,  t3?.blockDebuff,  data.tier);
    }

    // Quantos dos 6 bônus nomeados esse pet vai realmente mostrar (!= 0) — mesmo espírito de
    // CountActiveBonusRows (armas), usado só pra calcular a altura dinâmica do popup.
    private static int CountActivePetBonusRows(PetData d)
    {
        int count = 0;
        if (d.comboRate != 0f) count++;
        if (d.evasionBase != 0f) count++;
        if (d.accuracyBonus != 0f) count++;
        if (d.disarmRate != 0f) count++;
        if (d.comboDebuff != 0f) count++;
        if (d.blockDebuff != 0f) count++;
        return count;
    }

    // Sobe previousTier até achar o T1, desce por nextTier até o T3 — mesmo padrão de
    // ResolveTierFamily(WeaponData) acima, PetData tem os mesmos dois campos.
    private static (PetData t1, PetData t2, PetData t3) ResolveTierFamily(PetData p)
    {
        PetData t1 = p;
        while (t1.previousTier != null) t1 = t1.previousTier;
        PetData t2 = t1.nextTier;
        PetData t3 = t2 != null ? t2.nextTier : null;
        return (t1, t2, t3);
    }

    // Colore cada valor dentro de "[v1/v2/v3]" no effectText — o segmento do tier atualmente
    // equipado usa a mesma cor de destaque do popup de arma (primaryActionAlt), os outros dois
    // ficam num tom neutro (secondaryButtonAlt, igual a FormatTierTriplet). effectText é o
    // MESMO texto pros 3 tiers de uma skill (SkillTierGenerator copia verbatim T1→T2/T3) — só a
    // formatação muda por tier equipado, não o texto em si.
    private static readonly System.Text.RegularExpressions.Regex TierTripletRegex =
        new System.Text.RegularExpressions.Regex(@"\[([^/\[\]]+)/([^/\[\]]+)/([^/\[\]]+)\]");

    private string HighlightEffectTiers(string text, int currentTier)
    {
        if (string.IsNullOrEmpty(text)) return text;
        Color active = _theme.primaryActionAlt;
        Color muted = _theme.secondaryButtonAlt;
        return TierTripletRegex.Replace(text, m =>
        {
            var sb = new System.Text.StringBuilder("[");
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) sb.Append('/');
                Color c = (i + 1 == currentTier) ? active : muted;
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{m.Groups[i + 1].Value}</color>");
            }
            sb.Append(']');
            return sb.ToString();
        });
    }

    // Altura reservada pro bloco ícone+nome no topo do popup de arma (mesmo valor usado no
    // cálculo de altura dinâmica abaixo e no offset da área de stats). Não-const (2026-07-20,
    // 2ª rodada) — cresce junto com PopupIconSize/fonte do nome na gaveta mobile, mesma ideia
    // de SkillPopupHeaderHeight.
    private float WeaponPopupHeaderHeight => _bottomAnchored ? 250f : 148f;
    // Não-const (2026-07-20, mesmo motivo do Skill/PetPopup acima) — linha de stat mais alta na
    // gaveta mobile pra caber fonte maior sem cortar (ver AddPopupStatRow).
    private float WeaponPopupStatRowHeight => _bottomAnchored ? 32f : 24f;
    private const float WeaponPopupStatRowSpacing = 3f;
    private float WeaponPopupMinHeight => _bottomAnchored ? 560f : 380f;
    private float WeaponPopupWidth => _bottomAnchored ? 780f : 500f;

    // Layout estilo My Brute: ícone (com borda de tier, ver BuildPopupIcon) + nome (sem sufixo
    // de tier — a indicação vem só da borda do ícone) + status, todos visíveis de uma vez, sem
    // scroll (removido 2026-07-07 a pedido do usuário — o popup agora cresce o quanto for
    // preciso pra caber tudo, em vez de rolar). Damage/Draw Chance/qualquer bônus condicional
    // que varie por tier mostram os 3 tiers entre colchetes com o atual destacado — precisa
    // navegar a cadeia completa T1↔T2↔T3 (ResolveTierFamily, usa o campo WeaponData.nextTier já
    // que previousTier sozinho só anda pra trás). Os 10 bônus condicionais (Crit Chance,
    // Evasion, Dexterity, Reversal, Block, Accuracy, Disarm, Combo, Deflect, Counter) só
    // aparecem se != 0 pra essa arma (`AddTieredBonusRow`); `critDamageMultiplier` nunca aparece
    // (pedido do usuário — não é um "bônus" no mesmo sentido dos outros).
    // Público (2026-07-14) — ver comentário de ShowSkillDetail acima.
    public void ShowWeaponDetail(WeaponData w)
    {
        ClearPopupContent();

        var (t1, t2, t3) = ResolveTierFamily(w);

        // Altura do popup calculada a partir da quantidade REAL de linhas que essa arma vai
        // mostrar (5 fixas + quantos bônus condicionais estiverem != 0) — armas com poucos
        // bônus ficam com popup compacto; sem scroll, o popup só cresce o quanto precisar.
        // Padding extra de 56px (era 16px — "aumente o vertical do popup", pedido do usuário)
        // dá mais respiro embaixo da última linha.
        int rowCount = 5 + CountActiveBonusRows(w);
        float statsHeight = rowCount * WeaponPopupStatRowHeight + Mathf.Max(0, rowCount - 1) * WeaponPopupStatRowSpacing;
        float contentHeight = WeaponPopupHeaderHeight + statsHeight + 56f;
        float panelHeight = Mathf.Max(contentHeight / 0.86f, WeaponPopupMinHeight);
        _popupPanelRt.sizeDelta = new Vector2(WeaponPopupWidth, panelHeight);
        // Sobe o popup ~40px na tela (pedido do usuário) — só a arma; o popup de skill continua
        // centralizado (ver Vector2.zero explícito em ShowSkillDetail).
        _popupPanelRt.anchoredPosition = new Vector2(0f, 40f);

        Sprite spr = w.icon != null ? w.icon : w.inHandSprite;
        BuildPopupIcon(spr, w.tier);

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(_popupContentRoot, false);
        var nrt = nameGo.AddComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(0.5f, 1f);
        nrt.anchoredPosition = new Vector2(0f, _bottomAnchored ? SkillPopupNameOffsetY : -104f);
        nrt.sizeDelta = new Vector2(0f, SkillPopupNameHeight);
        var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
        nameTxt.text = StripTierSuffix(w.weaponName); nameTxt.fontSize = _bottomAnchored ? 40 : 28; nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = _theme.currencyGold;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.richText = true;

        // Área de stats — abaixo do bloco ícone/nome, sem scroll (o popup inteiro já foi
        // dimensionado acima pra caber essa quantidade exata de linhas).
        var statsAreaGo = new GameObject("StatsArea");
        statsAreaGo.transform.SetParent(_popupContentRoot, false);
        var saRt = statsAreaGo.AddComponent<RectTransform>();
        saRt.anchorMin = new Vector2(0f, 0f); saRt.anchorMax = new Vector2(1f, 1f);
        saRt.offsetMin = Vector2.zero; saRt.offsetMax = new Vector2(0f, -WeaponPopupHeaderHeight);
        var vlg = statsAreaGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = WeaponPopupStatRowSpacing;
        // childControlHeight=true (2026-07-07) — ERA a causa real do espaçamento excessivo entre
        // linhas, não o valor de WeaponPopupStatRowHeight em si: com controlHeight desligado, o
        // VerticalLayoutGroup POSICIONA cada linha usando o LayoutElement.preferredHeight (24px),
        // mas nunca redimensiona o RectTransform de cada linha pra esse valor — cada "row"/"go"
        // ficava com a altura padrão de um RectTransform novo (100px), sobrepondo visualmente a
        // linha seguinte com uma caixa bem maior que o texto, o que criava a aparência de um
        // vão gigante entre um atributo e o próximo. Com controlHeight ligado, cada linha
        // finalmente É redimensionada pra 24px de verdade.
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        Transform statsContent = statsAreaGo.transform;

        Color orange = _theme.primaryActionAlt;

        AddPopupStatRow(statsContent, "Types", BuildTypesLine(w.types));
        AddPopupStatRow(statsContent, "Odds", $"{w.dropOdds:P0}");
        AddPopupStatRow(statsContent, "Hit Speed", $"{w.hitSpeed:P0}");
        AddPopupStatRow(statsContent, "Damage",
            FormatTierTriplet(NullableInt(t1?.damage), NullableInt(t2?.damage), NullableInt(t3?.damage),
                w.tier, v => $"{v:0}", orange));
        AddPopupStatRow(statsContent, "Draw Chance",
            FormatTierTriplet(t1?.drawChance, t2?.drawChance, t3?.drawChance,
                w.tier, v => $"{v * 100f:0}", orange) + "%");

        AddTieredBonusRow(statsContent, "Crit Chance", w.critChanceBonus, t1?.critChanceBonus, t2?.critChanceBonus, t3?.critChanceBonus, w.tier);
        AddTieredBonusRow(statsContent, "Evasion", w.evasionBonus, t1?.evasionBonus, t2?.evasionBonus, t3?.evasionBonus, w.tier);
        AddTieredBonusRow(statsContent, "Dexterity", w.dexterityBonus, t1?.dexterityBonus, t2?.dexterityBonus, t3?.dexterityBonus, w.tier);
        AddTieredBonusRow(statsContent, "Reversal", w.reversalBonus, t1?.reversalBonus, t2?.reversalBonus, t3?.reversalBonus, w.tier);
        AddTieredBonusRow(statsContent, "Block", w.blockBonus, t1?.blockBonus, t2?.blockBonus, t3?.blockBonus, w.tier);
        AddTieredBonusRow(statsContent, "Accuracy", w.accuracyBonus, t1?.accuracyBonus, t2?.accuracyBonus, t3?.accuracyBonus, w.tier);
        AddTieredBonusRow(statsContent, "Disarm", w.disarmBonus, t1?.disarmBonus, t2?.disarmBonus, t3?.disarmBonus, w.tier);
        AddTieredBonusRow(statsContent, "Combo", w.comboBonus, t1?.comboBonus, t2?.comboBonus, t3?.comboBonus, w.tier);
        AddTieredBonusRow(statsContent, "Deflect", w.deflectBonus, t1?.deflectBonus, t2?.deflectBonus, t3?.deflectBonus, w.tier);
        AddTieredBonusRow(statsContent, "Counter", w.counterBonus, t1?.counterBonus, t2?.counterBonus, t3?.counterBonus, w.tier);

        _popupOverlayGo.SetActive(true);
    }

    // A indicação de tier no popup vem só da borda colorida do ícone agora, então o sufixo
    // " T1"/" T2"/" T3" embutido no `weaponName` do asset nunca deve aparecer no título (pedido
    // explícito do usuário, 2026-07-07). Lógica extraída (2026-07-14) pra WeaponNameUtil —
    // reaproveitada também pela camada de save (LocalSaveService/PlayerProfileConverter).
    private static string StripTierSuffix(string name) => WeaponNameUtil.StripWeaponTierSuffix(name);

    // Quantos dos 10 bônus condicionais essa arma vai realmente mostrar (!= 0) — usado só pra
    // calcular a altura dinâmica do popup antes de construir as linhas de verdade.
    private static int CountActiveBonusRows(WeaponData w)
    {
        int count = 0;
        if (w.critChanceBonus != 0f) count++;
        if (w.evasionBonus != 0f) count++;
        if (w.dexterityBonus != 0f) count++;
        if (w.reversalBonus != 0f) count++;
        if (w.blockBonus != 0f) count++;
        if (w.accuracyBonus != 0f) count++;
        if (w.disarmBonus != 0f) count++;
        if (w.comboBonus != 0f) count++;
        if (w.deflectBonus != 0f) count++;
        if (w.counterBonus != 0f) count++;
        return count;
    }

    // Linha de bônus condicional — só aparece se o valor ATUAL (`current`, sempre lido direto
    // da arma equipada) for != 0; nunca usada pra `critDamageMultiplier` (pedido do usuário:
    // nunca exibir esse campo). Quando os 3 tiers têm valores diferentes entre si (`v1`/`v2`/`v3`
    // — resolvidos via ResolveTierFamily, podem ser null se a arma não tiver cadeia completa),
    // mostra o mesmo formato de colchetes `[T1/T2/T3]` já usado em Damage/Draw Chance, com o
    // tier atual destacado; se os 3 valores forem iguais (ou desconhecidos), cai pro formato
    // simples "+X%"/"-X%". Verde se positivo, vermelho se negativo (penalidade). Reaproveita
    // `AddPopupStatRow` (mesmas colunas label/valor de Types/Odds/Damage/etc, 2026-07-07 — antes
    // era uma única string "Label +valor" sem coluna própria, ficando desalinhado do resto).
    private void AddTieredBonusRow(Transform parent, string label, float current,
        float? v1, float? v2, float? v3, int currentTier)
    {
        if (current == 0f) return;

        bool allKnown = v1.HasValue && v2.HasValue && v3.HasValue;
        bool varies = allKnown && !(Mathf.Approximately(v1.Value, v2.Value) && Mathf.Approximately(v2.Value, v3.Value));
        Color activeColor = current > 0f ? _theme.success : _theme.danger;
        string sign = current > 0f ? "+" : "-";

        string valuePart = varies
            ? sign + FormatTierTriplet(v1, v2, v3, currentTier, v => $"{Mathf.Abs(v) * 100f:0}", activeColor) + "%"
            : $"<color=#{ColorUtility.ToHtmlStringRGB(activeColor)}>{sign}{Mathf.Abs(current) * 100f:0}%</color>";

        AddPopupStatRow(parent, label, valuePart);
    }

    // Sobe previousTier até achar o T1, depois desce por nextTier até T3 — funciona a partir
    // de QUALQUER tier equipado (T1, T2 ou T3), não só do T1 (previousTier sozinho só anda pra
    // trás; nextTier — novo campo — é o que permite completar a cadeia pra frente).
    private static (WeaponData t1, WeaponData t2, WeaponData t3) ResolveTierFamily(WeaponData w)
    {
        WeaponData t1 = w;
        while (t1.previousTier != null) t1 = t1.previousTier;
        WeaponData t2 = t1.nextTier;
        WeaponData t3 = t2 != null ? t2.nextTier : null;
        return (t1, t2, t3);
    }

    // int? -> float? — WeaponData.damage é int, mas FormatTierTriplet trabalha em float (pra
    // servir também drawChance/critChanceBonus, que já são float).
    private static float? NullableInt(int? value) => value.HasValue ? (float?)value.Value : null;

    // Monta "[valor T1/valor T2/valor T3]" — o segmento do tier atualmente equipado usa
    // `activeColor`, os outros dois usam um tom neutro/cinza; "-" (hífen simples, não travessão)
    // pra tier que não existe (arma sem cadeia completa, ex: os 5 WeaponData legados sem
    // nextTier) — bug real (2026-07-07): o travessão "—" usado antes aqui (e em BuildTypesLine)
    // aparecia como caractere quebrado/placeholder no popup (glyph ausente no atlas da fonte
    // TMP usada), reportado pelo usuário como "Damage aparece com caracteres quebrados" ao
    // testar com o loadout inicial (as 4 armas legadas de reset não têm `nextTier`, então T2/T3
    // sempre caem nesse fallback). Hífen ASCII simples está garantido em qualquer fonte.
    private string FormatTierTriplet(float? v1, float? v2, float? v3, int currentTier,
        System.Func<float, string> formatter, Color activeColor)
    {
        string Seg(float? v, int tierNum)
        {
            string text = v.HasValue ? formatter(v.Value) : "-";
            Color c = tierNum == currentTier ? activeColor : _theme.secondaryButtonAlt;
            return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>";
        }
        return $"[{Seg(v1, 1)}/{Seg(v2, 2)}/{Seg(v3, 3)}]";
    }

    // Cor própria por WeaponType, reaproveitando tokens já existentes do UITheme (nenhum campo
    // novo só pra isso) — Sharp/Blunt/Heavy/Long/Fast/Thrown/Ranged, cada um com uma associação
    // visual razoável (Sharp=vermelho/corte, Heavy=bronze/peso, etc.).
    private Color TypeColor(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Sharp:  return _theme.danger;
            case WeaponType.Blunt:  return _theme.secondaryButton;
            case WeaponType.Heavy:  return _theme.tierBronze;
            case WeaponType.Long:   return _theme.secondaryButtonAlt;
            case WeaponType.Fast:   return _theme.currencyGold;
            case WeaponType.Thrown: return _theme.currencyGem;
            case WeaponType.Ranged: return _theme.success;
            default:                return TextColor;
        }
    }

    private string BuildTypesLine(List<WeaponType> types)
    {
        if (types == null || types.Count == 0) return "-";
        var parts = new List<string>();
        foreach (var t in types)
            parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(TypeColor(t))}>{t}</color>");
        return string.Join(", ", parts);
    }

    // Linha label+valor do popup de arma — igual no espírito a BuildPassiveRow, mas com o
    // valor podendo trazer tags <color> embutidas (rich text, TMP já suporta nativamente —
    // `richText` setado explícito, 2026-07-07, em vez de depender só do default da classe).
    // Fontes aumentadas (16→20) e altura de linha reduzida (26→24, com o VerticalLayoutGroup
    // do chamador usando spacing bem menor) — popup estava ilegível e com linhas espalhadas
    // demais, reportado pelo usuário.
    private void AddPopupStatRow(Transform parent, string label, string richValue)
    {
        var row = new GameObject(label);
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = WeaponPopupStatRowHeight; le.flexibleWidth = 1f;

        var lblGo = new GameObject("Lbl");
        lblGo.transform.SetParent(row.transform, false);
        var lrt = lblGo.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0.42f, 1f);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var lblTxt = lblGo.AddComponent<TextMeshProUGUI>();
        lblTxt.text = label; lblTxt.fontSize = _bottomAnchored ? 26 : 20; lblTxt.fontStyle = FontStyles.Bold;
        lblTxt.color = _theme.currencyGold;
        lblTxt.alignment = TextAlignmentOptions.MidlineLeft;

        var valGo = new GameObject("Val");
        valGo.transform.SetParent(row.transform, false);
        var vrt = valGo.AddComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.42f, 0f); vrt.anchorMax = new Vector2(1f, 1f);
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        var valTxt = valGo.AddComponent<TextMeshProUGUI>();
        valTxt.text = richValue; valTxt.fontSize = _bottomAnchored ? 26 : 20;
        valTxt.color = TextColor;
        valTxt.alignment = TextAlignmentOptions.MidlineLeft;
        valTxt.richText = true;
    }

    // ── Data Refresh ─────────────────────────────────────────────────────────

    private void RefreshAll()
    {
        var p = _overrideProfile != null ? _overrideProfile : _holder?.currentProfile;
        if (p == null)
        {
            // Bug real corrigido (2026-07-25, NullReferenceException reportada pelo usuário) —
            // no modo `_bottomAnchored` (gaveta mobile, único uso: MainMenuController) `refs.name`
            // fica intencionalmente null (ver BuildInfoBlock, "sem nome/winrate" nesse modo) - o
            // guard antigo assumia `_compactInfo.name`/`_expandedInfo.name` sempre existirem,
            // travando aqui sempre que currentProfile chegasse nulo (ver SelectedProfileHolder -
            // instância runtime de case opening sem referência estável entre sessões).
            if (_compactInfo.name != null) _compactInfo.name.text = "—";
            if (_expandedInfo.name != null) _expandedInfo.name.text = "—";
            return;
        }

        // Placeholder simbólico (2026-07-07): profile.winRate nunca é escrito em lugar nenhum
        // hoje — não existe contador de vitórias/batalhas totais no projeto ainda. Usuário
        // confirmou que isso vai ser preenchido futuramente por um sistema de banco de
        // dados/histórico de partidas; até lá mostra "—%" pra qualquer profile novo (default 0).
        string winRateText = p.winRate > 0f ? $"{p.winRate:F0}%" : "—%";

        var (effHp, effStr, effAgi, effSpd, effInit, effCritChance, effCritDmg, effEvasion,
             effReversal, effCounter, effCombo, effArmor, effAccuracy, effBlock,
             effReversalAfterBlock, effDisarm, _, _, _) = p.GetEffectiveStats();

        int xpReq = XpSystem.XpRequired(p.level);
        float xpPct = xpReq > 0 ? Mathf.Clamp01((float)p.xpCurrent / xpReq) : 0f;

        foreach (var refs in new[] { _compactInfo, _expandedInfo })
        {
            // Gaveta mobile (2026-07-20): sem nome/winrate (refs.name/winRate ficam null nesse
            // modo, ver BuildInfoBlock) — HP usa o mesmo refs.hp (texto sobreposto) nos dois
            // modos agora (4ª rodada: HP voltou a não usar pips).
            if (!_bottomAnchored)
            {
                refs.name.text = p.profileName;
                refs.winRate.text = winRateText;
            }
            refs.hp.text = $"{effHp}";
            refs.str.SetValue(effStr);
            refs.agi.SetValue(effAgi);
            refs.spd.SetValue(effSpd);

            if (_showLevelXp && refs.level != null)
            {
                refs.level.text = $"Level {p.level}";
                refs.xpValue.text = $"{p.xpCurrent}/{xpReq}";
                refs.xpFill.rectTransform.anchorMax = new Vector2(Mathf.Max(xpPct, 0.001f), 1f);
            }
        }

        RefreshSkills(p);
        RefreshArmas(p);
        RefreshPets(p);

        SetPassive("Evasion", $"{effEvasion:P0}");
        SetPassive("Counter", $"{effCounter:P0}");
        SetPassive("Reverse", $"{effReversal:P0}");
        SetPassive("Accuracy", $"{effAccuracy:P0}");
        SetPassive("Armor", $"{effArmor:P0}");
        SetPassive("Block", $"{effBlock:P0}");
        SetPassive("Reversal After Block", $"{effReversalAfterBlock:P0}");
        SetPassive("Critical Chance", $"{effCritChance:P0}");
        SetPassive("Critical Damage", $"{effCritDmg:P0}");
        SetPassive("Combo Chance", $"{effCombo:P0}");
        SetPassive("Disarm Chance", $"{effDisarm:P0}");
        SetPassive("Initiative", $"{effInit}");
        SetPassive("Hit Speed", $"{p.hitSpeed:F2}");
    }

    private void SetPassive(string label, string value)
    {
        if (_passiveRows.TryGetValue(label, out var txt)) txt.text = $"{label}: {value}";
    }

    private void RefreshSkills(PlayerProfile p)
    {
        foreach (Transform c in _skillsList) Destroy(c.gameObject);
        bool has = p.skills != null && p.skills.Count > 0;
        _skillsEmpty.gameObject.SetActive(!has);
        if (!has) return;

        foreach (var skill in p.skills)
        {
            if (skill == null) continue;

            // T2/T3 nunca têm icon próprio (ver SkillTierGenerator) — sobe a cadeia
            // previousTier até achar um, mesmo padrão de WeaponHandler.EquipSpecific. A borda
            // usa o tier REAL da skill equipada, não o do iconSource (podem divergir quando o
            // ícone precisou subir a cadeia).
            var iconSource = skill;
            while (iconSource != null && iconSource.icon == null) iconSource = iconSource.previousTier;

            var s = skill; // captura por valor pro closure do onClick
            BuildTierIconCell(_skillsList, iconSource != null ? iconSource.icon : null, skill.tier,
                () => ShowSkillDetail(s));
        }
    }

    private void RefreshArmas(PlayerProfile p)
    {
        foreach (Transform c in _armasList) Destroy(c.gameObject);
        var loadout = p.weapons;
        int count = 0;
        if (loadout != null)
        {
            foreach (var w in loadout)
            {
                if (w == null) continue;
                count++;
                Sprite spr = w.icon != null ? w.icon : w.inHandSprite;
                var weapon = w; // captura por valor pro closure do onClick
                BuildTierIconCell(_armasList, spr, w.tier, () => ShowWeaponDetail(weapon));
            }
        }
        _armasEmpty.gameObject.SetActive(count == 0);
    }

    private void RefreshPets(PlayerProfile p)
    {
        foreach (Transform c in _petsList) Destroy(c.gameObject);
        var pets = p.pets;
        int count = 0;
        if (pets != null)
        {
            foreach (var pet in pets)
            {
                if (pet == null) continue;
                count++;
                var data = pet; // captura por valor pro closure do onClick
                BuildTierIconCell(_petsList, data.icon, data.tier, () => ShowPetDetail(data, data.icon));
            }
        }
        _petsEmpty.gameObject.SetActive(count == 0);
    }

    // ── Layout Helpers ───────────────────────────────────────────────────────

    private static TMP_Text AddLabel(GameObject parent, string text, float size, Color color)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = size; txt.color = color;
        txt.alignment = TextAlignmentOptions.Center;
        return txt;
    }

    private void MakeSectionTitle(Transform parent, string text)
    {
        var go = new GameObject("SectionTitle");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        // Fonte 40pt na gaveta mobile (2026-07-20, pedido do usuário — era 20pt) — altura da
        // linha (LayoutElement) cresce junto (30→56px) pra não cortar o texto maior.
        le.preferredHeight = _bottomAnchored ? 56f : 30f; le.flexibleWidth = 1f;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = _bottomAnchored ? 40 : 20; txt.fontStyle = FontStyles.Bold;
        txt.color = _theme.currencyGold;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static TMP_Text MakeMsg(Transform parent, string text)
    {
        var go = new GameObject("EmptyMsg");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 30f; le.flexibleWidth = 1f;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = 16;
        txt.color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        return txt;
    }

    // ScrollRect + Viewport + Content
    private static void MakeScroll(Transform parent, out Transform content)
    {
        var scrollGo = new GameObject("Scroll");
        scrollGo.transform.SetParent(parent, false);
        Stretch(scrollGo.AddComponent<RectTransform>());
        scrollGo.AddComponent<Image>().color = Color.clear;
        var sr = scrollGo.AddComponent<ScrollRect>();
        sr.horizontal = false;

        var vpGo = new GameObject("Viewport");
        vpGo.transform.SetParent(scrollGo.transform, false);
        Stretch(vpGo.AddComponent<RectTransform>());
        vpGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        var mask = vpGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        sr.viewport = vpGo.GetComponent<RectTransform>();

        var cGo = new GameObject("Content");
        cGo.transform.SetParent(vpGo.transform, false);
        var crt = cGo.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f); crt.offsetMin = crt.offsetMax = Vector2.zero;
        sr.content = crt;
        content = cGo.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
