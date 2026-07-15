using System.Collections;
using System.Collections.Generic;
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

    // Override explícito (2026-07-08) — quando não-nulo, RefreshAll mostra ESTE profile em vez
    // de `_holder.currentProfile`. 01_MainMenu nunca chama SetProfile (mostra sempre o
    // equipado, via _holder); 02_SelectCharacter chama a cada clique num card do grid, senão o
    // painel sempre mostraria as stats do personagem atualmente equipado, não o card clicado.
    private PlayerProfile _overrideProfile;

    private GameObject _canvasGo;
    private RectTransform _rootRt;
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
        public TMP_Text name, winRate, hp;
        public AttributePipBar str, agi, spd;
        public TMP_Text level, xpValue; // só preenchidos quando _showLevelXp
        public Image xpFill;
    }
    private InfoBlockRefs _compactInfo, _expandedInfo;

    // Skills/Armas — listas dinâmicas dentro do Expanded, reconstruídas a cada RefreshAll.
    private Transform _skillsList, _armasList;
    private TMP_Text _skillsEmpty, _armasEmpty;

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
    public void Setup(SelectedProfileHolder holder, UITheme theme, bool showLevelXp = false,
        bool startHidden = false, float? anchorBottomOverride = null, float? anchorTopOverride = null)
    {
        _holder = holder;
        _theme = theme;
        _showLevelXp = showLevelXp;
        _anchorBottom = anchorBottomOverride ?? RootAnchorBottom;
        _anchorTop = anchorTopOverride ?? RootAnchorTop;
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
        GameObject showGo = toExpanded ? _expandedGo : _compactGo;
        GameObject hideGo = toExpanded ? _compactGo  : _expandedGo;
        CanvasGroup showCg = toExpanded ? _expandedCg : _compactCg;
        CanvasGroup hideCg = toExpanded ? _compactCg  : _expandedCg;

        showGo.SetActive(true);
        showCg.interactable = false;
        showCg.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            float t = elapsed / FadeDuration;
            showCg.alpha = t;
            hideCg.alpha = 1f - t;
            elapsed += Time.deltaTime;
            yield return null;
        }
        showCg.alpha = 1f; hideCg.alpha = 0f;
        showCg.interactable = true;  showCg.blocksRaycasts = true;
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

        var rootGo = new GameObject("Root");
        rootGo.transform.SetParent(_canvasGo.transform, false);
        _rootRt = rootGo.AddComponent<RectTransform>();
        _rootRt.anchorMax = new Vector2(1f, _anchorTop);
        _rootRt.anchorMin = new Vector2(1f, _anchorBottom);
        _rootRt.offsetMin = new Vector2(-(PanelWidth + EdgeMargin), 0f);
        _rootRt.offsetMax = new Vector2(-EdgeMargin, 0f);

        BuildCompact(rootGo);
        BuildExpanded(rootGo);

        // Estado inicial: compacto visível, expandido desligado (sem animação — só acontece
        // na primeira montagem da cena).
        _compactGo.SetActive(true);
        _compactCg.alpha = 1f; _compactCg.interactable = true; _compactCg.blocksRaycasts = true;
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
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -(CompactHeight + LevelXpExtra)); rt.offsetMax = Vector2.zero;
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
        iirt.anchorMin = new Vector2(0f, 1f); iirt.anchorMax = new Vector2(1f, 1f);
        iirt.pivot     = new Vector2(0.5f, 1f);
        iirt.offsetMin = new Vector2(0f, -CompactHeight); iirt.offsetMax = Vector2.zero;
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
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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
        irt.anchorMin = new Vector2(0f, 1f); irt.anchorMax = new Vector2(1f, 1f);
        irt.pivot     = new Vector2(0.5f, 1f);
        irt.offsetMin = new Vector2(0f, -CompactHeight); irt.offsetMax = Vector2.zero;
        _expandedInfo = BuildInfoBlock(infoGo);

        if (_showLevelXp) BuildLevelXpBox(_expandedGo, CompactHeight, _expandedInfo);

        // Divider/ScrollArea deslocados pra baixo pelo espaço extra do LevelXp (0 quando
        // showLevelXp=false — comportamento idêntico ao de sempre no 01_MainMenu).
        float belowInfo = CompactHeight + LevelXpExtra;

        var lineGo = new GameObject("Divider");
        lineGo.transform.SetParent(_expandedGo.transform, false);
        var lrt = lineGo.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f);
        lrt.pivot     = new Vector2(0.5f, 1f);
        lrt.offsetMin = new Vector2(20f, -(belowInfo + 3f));
        lrt.offsetMax = new Vector2(-20f, -belowInfo);
        lineGo.AddComponent<Image>().color = _theme.currencyGold;

        var scrollAreaGo = new GameObject("ScrollArea");
        scrollAreaGo.transform.SetParent(_expandedGo.transform, false);
        var srt = scrollAreaGo.AddComponent<RectTransform>();
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero; srt.offsetMax = new Vector2(0f, -(belowInfo + 6f));
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

        var hpGo = new GameObject("Hp");
        hpGo.transform.SetParent(container.transform, false);
        var hprt = hpGo.AddComponent<RectTransform>();
        hprt.anchorMin = new Vector2(0.05f, 0.63f); hprt.anchorMax = new Vector2(0.95f, 0.80f);
        hprt.offsetMin = hprt.offsetMax = Vector2.zero;
        refs.hp = hpGo.AddComponent<TextMeshProUGUI>();
        refs.hp.fontSize = 20; refs.hp.fontStyle = FontStyles.Bold;
        refs.hp.color = TextColor;
        refs.hp.alignment = TextAlignmentOptions.MidlineLeft;

        refs.str = BuildPipRow(container, 0.44f, 0.60f, "STR");
        refs.agi = BuildPipRow(container, 0.24f, 0.40f, "AGI");
        refs.spd = BuildPipRow(container, 0.04f, 0.20f, "SPD");

        return refs;
    }

    private AttributePipBar BuildPipRow(GameObject container, float yMin, float yMax, string label)
    {
        var rowGo = new GameObject(label + "Row");
        rowGo.transform.SetParent(container.transform, false);
        var rt = rowGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, yMin); rt.anchorMax = new Vector2(0.95f, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return AttributePipBar.Build(rowGo, _theme, label);
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

        BuildDetailsToggle(content);
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
        le.preferredHeight = 44f; le.flexibleWidth = 1f;
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = UIShapeUtil.RoundedRect(_theme.secondaryButton, 10f);
        btnImg.type = Image.Type.Sliced;
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(ToggleDetails);
        _detailsButtonLabel = AddLabel(btnGo, "VER DETALHES", 18, TextColor);
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

    private TMP_Text BuildPassiveRow(Transform parent, string label)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 26f; le.flexibleWidth = 1f;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.fontSize = 17;
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
    private static Transform MakeIconGrid(Transform parent)
    {
        var go = new GameObject("Grid");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        var glg = go.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(70f, 70f);
        glg.spacing = new Vector2(8f, 8f);
        glg.childAlignment = TextAnchor.UpperLeft;
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 5;
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
    private void BuildPopupIcon(Sprite icon, int tier)
    {
        var cellGo = BuildTierIconCell(_popupContentRoot, icon, tier, null);
        var rt = cellGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(96f, 96f);
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

    private const float SkillPopupWidth = 420f;
    private const float SkillPopupMinHeight = 300f;
    // Frações reais de _popupContentRoot dentro do painel (ver anchors em BuildPopup:
    // 0.06-0.94 horizontal, 0.04-0.90 vertical) — usadas pra converter altura de CONTEÚDO
    // (onde o texto é medido/posicionado) em altura de PAINEL (`_popupPanelRt.sizeDelta`).
    private const float SkillPopupContentWidthFraction  = 0.88f;
    private const float SkillPopupContentHeightFraction = 0.86f;
    private const float SkillPopupHeaderHeight        = 144f; // espaço reservado pro ícone+nome no topo
    private const float SkillPopupDescEffectGap       = 14f;  // espaço entre a descrição e o bloco "Efeito"
    private const float SkillPopupEffectLabelHeight   = 22f;
    private const float SkillPopupEffectLabelValueGap = 4f;
    private const float SkillPopupBottomPadding       = 20f;

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
        nrt.anchoredPosition = new Vector2(0f, -100f);
        nrt.sizeDelta = new Vector2(0f, 36f);
        var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
        nameTxt.text = skill.skillName; nameTxt.fontSize = 28; nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = _theme.currencyGold;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.richText = true;

        // Descrição — cria o texto e mede a altura real que ele vai ocupar (com quebra de
        // linha) na largura disponível, antes de decidir o tamanho do painel.
        string desc = string.IsNullOrEmpty(skill.description) ? "Sem descrição disponível." : skill.description;
        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(_popupContentRoot, false);
        bodyGo.AddComponent<RectTransform>();
        var bodyTxt = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyTxt.enableWordWrapping = true;
        bodyTxt.fontSize = 20;
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
            effectValueTxt.fontSize = 18;
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
            labelTxt.text = "Efeito"; labelTxt.fontSize = 18; labelTxt.fontStyle = FontStyles.Bold;
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

    // Altura fixa reservada pro bloco ícone+nome no topo do popup de arma (mesmo valor usado
    // no cálculo de altura dinâmica abaixo e no offset da área de stats).
    private const float WeaponPopupHeaderHeight = 148f;
    private const float WeaponPopupStatRowHeight = 24f;
    private const float WeaponPopupStatRowSpacing = 3f;
    private const float WeaponPopupMinHeight = 380f;
    private const float WeaponPopupWidth = 500f;

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
        nrt.anchoredPosition = new Vector2(0f, -104f);
        nrt.sizeDelta = new Vector2(0f, 36f);
        var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
        nameTxt.text = StripTierSuffix(w.weaponName); nameTxt.fontSize = 28; nameTxt.fontStyle = FontStyles.Bold;
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
        lblTxt.text = label; lblTxt.fontSize = 20; lblTxt.fontStyle = FontStyles.Bold;
        lblTxt.color = _theme.currencyGold;
        lblTxt.alignment = TextAlignmentOptions.MidlineLeft;

        var valGo = new GameObject("Val");
        valGo.transform.SetParent(row.transform, false);
        var vrt = valGo.AddComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.42f, 0f); vrt.anchorMax = new Vector2(1f, 1f);
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        var valTxt = valGo.AddComponent<TextMeshProUGUI>();
        valTxt.text = richValue; valTxt.fontSize = 20;
        valTxt.color = TextColor;
        valTxt.alignment = TextAlignmentOptions.MidlineLeft;
        valTxt.richText = true;
    }

    // ── Data Refresh ─────────────────────────────────────────────────────────

    private void RefreshAll()
    {
        var p = _overrideProfile != null ? _overrideProfile : _holder?.currentProfile;
        if (p == null) { _compactInfo.name.text = "—"; _expandedInfo.name.text = "—"; return; }

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
            refs.name.text = p.profileName;
            refs.winRate.text = winRateText;
            refs.hp.text = $"{effHp} HP";
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
        le.preferredHeight = 30f; le.flexibleWidth = 1f;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = 20; txt.fontStyle = FontStyles.Bold;
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
