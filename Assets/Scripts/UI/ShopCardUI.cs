using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Card reutilizável da grade de 06_Loja (ShopController) — mesmo espírito de ArsenalSlotUI
// (RoundedRect via código, sem depender de prefab/sprite pronto), mas com conteúdo de texto
// (título/detalhe/preço/botão comprar) em vez de ícone+borda de tier.
//
// Ícone: só a aba Diamantes passa um sprite real (Diamond.png, já tem arte final — pedido
// explícito do usuário pra não usar caixa cinza nesse caso). As outras abas ainda não têm arte
// final: `icon == null` desenha um retângulo cinza (ou colorido por raridade, aba Personagens) no
// lugar — placeholder da Fase 1, ver MONETIZACAO.md seção 9.
//
// Geometria proporcional (2026-07-20): `Build` recebe a altura real do card (`cardHeight`, vinda
// do `cellSize` que `ShopController` já define por aba) e deriva `scale = cardHeight /
// ReferenceCardHeight` (420, a altura em que estas constantes `Base*` foram originalmente
// calibradas) — ícone, paddings e fontes crescem juntos na mesma proporção que a altura do card,
// sem precisar recalibrar a lógica de layout pra cada tamanho novo. A altura do subtítulo é a
// exceção: calculada a partir do espaço realmente sobrante entre título e preço (ver Build),
// não uma constante escalada, pra nunca sobrepor o cluster de baixo.
public class ShopCardUI : MonoBehaviour
{
    // Altura de referência sobre a qual estas constantes foram calibradas — `scale` (derivado de
    // cardHeight/ReferenceCardHeight) escala tudo proporcionalmente pra qualquer CardHeight* de
    // ShopController.
    private const float ReferenceCardHeight = 420f;

    private const float BasePadding = 16f;
    private const float BaseIconSize = 96f;
    private const float BaseBuyButtonHeight = 56f;
    private const float BaseTitleHeight = 52f;
    private const float BaseTitleFont = 24f;
    private const float BaseTitleGap = 8f;
    private const float BaseSubtitleFont = 16f;
    private const float BaseSubtitleGapExtra = 4f;
    private const float BasePriceHeight = 40f;
    private const float BasePriceFont = 26f;
    private const float BasePriceGap = 34f;
    private const float BaseStatusHeight = 28f;
    private const float BaseStatusFont = 15f;
    private const float BaseStatusGap = 6f;
    private const float BaseBuyLabelFont = 20f;

    private static readonly Color PlaceholderGray = new Color(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Color SoldOutTint = new Color(0.5f, 0.5f, 0.5f, 1f);

    private Image _buyBg;
    private Button _buyBtn;
    private TMP_Text _buyLabelTxt;
    private TMP_Text _statusTxt;
    private Color _buyColor;
    private string _soldOutLabel;
    // Texto fixo de status (2026-07-21, pedido do usuário — Passes: "Ativo — N dias restantes"),
    // sobrepõe o "Comprado Nx (sessão)" genérico quando o item não tem limite (`limit == 0`) — ao
    // contrário de `soldOutLabel`, não desabilita o botão nem esconde "COMPRAR" (Passes continuam
    // compráveis de novo pra acumular dias, ver ShopController.ApplyPassPurchase). `null` (default,
    // todos os outros cards) mantém o texto genérico de sempre.
    private string _statusOverride;
    // Trava de pré-requisito (2026-07-21, pedido do usuário — Progressão: Slot 2/3 bloqueados até
    // o slot anterior ser comprado) — desabilita o botão (cinza, mesmo SoldOutTint de ESGOTADO)
    // SEM trocar o texto "COMPRAR" (pedido explícito, diferente de soldOutLabel) e mostra
    // `_lockedReason` ("Requer Slot de Skill N") no lugar do status genérico. Tem prioridade
    // sobre limit/soldOutLabel/statusOverride — ver RefreshPurchaseState.
    private bool _locked;
    private string _lockedReason;

    // Rótulo do botão de ação (2026-07-27, pedido do usuário — cards de resgate gratuito da aba
    // Diamantes: "RESGATAR" em vez de "COMPRAR", nos dois estados — disponível e bloqueado/
    // contagem regressiva). `null` (default) mantém "COMPRAR" pra todo card existente, sem mudar
    // nenhum comportamento anterior a esta mudança.
    private string _buyLabel;

    // Posição de MUNDO do botão "COMPRAR" (2026-07-21, pedido do usuário — efeito de "diamante
    // voando" deve sair do BOTÃO especificamente, não do centro do card inteiro). Fallback pro
    // centro do card só se o botão nunca foi construído (não deveria acontecer em uso normal).
    public Vector3 BuyButtonWorldPosition => _buyBtn != null ? _buyBtn.transform.position : transform.position;

    // Exposto (2026-07-27) pra permitir anexar um `CountdownLabel` por cima (cards de resgate
    // gratuito — contagem regressiva até o próximo período, ver ShopController) sem duplicar o
    // componente de ticking já usado pelo timer de energia (MainMenuCharacterPreview/
    // MainMenuController). `RefreshPurchaseState`/`_lockedReason` continuam controlando o texto
    // ESTÁTICO (usado por todo card que não precisa de contagem regressiva); o `CountdownLabel`
    // anexado por fora simplesmente sobrescreve esse texto a cada tick, sem conflito — os dois
    // nunca escrevem no mesmo frame porque `RefreshPurchaseState` só roda uma vez por rebuild.
    public TMP_Text StatusText => _statusTxt;

    // `soldOutLabel` (2026-07-20, pedido do usuário — Passes mensais): texto do botão quando o
    // item está esgotado, no lugar do "ESGOTADO" genérico — usado pelos Passes pra mostrar
    // "N dias" (dias restantes do passe ativo, ver ShopController.RebuildGrid/OnBuyClicked).
    // `null` (default, todos os outros cards) mantém o "ESGOTADO" de sempre.
    // `statusOverride` (2026-07-21, pedido do usuário — Passes) e `onInfo` (mostra um botão "i" no
    // canto superior direito do card quando não-nulo — Passes usam pra explicar que a recompensa
    // diária ainda não foi implementada, ver ShopController.ShowPassInfoPopup) — ambos `null` por
    // default, sem efeito em nenhum outro card da Loja.
    public void Build(UITheme theme, string title, string subtitle, string priceLabel, Sprite icon,
        Color? accentColor, int purchased, int limit, float cardHeight, string soldOutLabel = null,
        string statusOverride = null, System.Action onInfo = null, bool locked = false,
        string lockedReason = null, System.Action onBuy = null, string buyLabel = null)
    {
        _soldOutLabel = soldOutLabel;
        _statusOverride = statusOverride;
        _locked = locked;
        _lockedReason = lockedReason;
        _buyLabel = buyLabel ?? "COMPRAR";
        var rt = (RectTransform)transform;
        _buyColor = theme.primaryAction;

        float scale = cardHeight / ReferenceCardHeight;
        float padding = BasePadding * scale;
        float iconSize = BaseIconSize * scale;
        float buyHeight = BaseBuyButtonHeight * scale;
        float titleHeight = BaseTitleHeight * scale;
        float titleGap = BaseTitleGap * scale;
        float subtitleGapExtra = BaseSubtitleGapExtra * scale;
        float priceHeight = BasePriceHeight * scale;
        float priceGap = BasePriceGap * scale;
        float statusHeight = BaseStatusHeight * scale;
        float statusGap = BaseStatusGap * scale;

        // Altura do subtítulo é o espaço REAL sobrante entre o título e o cluster de
        // preço/status/comprar (com uma folga simétrica de subtitleGapExtra dos dois lados) — não
        // uma constante proporcional fixa. Uma altura fixa podia sobrepor o cluster de baixo sem
        // aparecer visualmente só porque o texto era curto o bastante pra nunca preencher a caixa
        // inteira (bug real encontrado ao conferir a geometria manualmente antes desta tarefa
        // aumentar os cards — nunca chegou a ser visível na Fase 1 porque só as abas de 1 fileira
        // têm subtítulo, e essas sempre tiveram texto curto o bastante pra não estourar).
        float titleBottomY = cardHeight - (padding + iconSize + titleGap + titleHeight);
        float priceTopY = padding + buyHeight + priceGap + priceHeight;
        float subtitleHeight = Mathf.Max(0f, titleBottomY - priceTopY - subtitleGapExtra * 2f);

        var bgGo = new GameObject("Bg", typeof(RectTransform));
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.SetParent(rt, false);
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = UIShapeUtil.RoundedRect(theme.panelBackgroundAlt, 18f);
        bgImg.type = Image.Type.Sliced;

        // Ícone real (Diamond.png) ou placeholder cinza — placeholder usa accentColor quando
        // presente (aba Personagens, cor de raridade — MONETIZACAO.md seção 8), senão cinza neutro.
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.SetParent(rt, false);
        iconRt.anchorMin = new Vector2(0.5f, 1f);
        iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot = new Vector2(0.5f, 1f);
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);
        iconRt.anchoredPosition = new Vector2(0f, -padding);
        var iconImg = iconGo.AddComponent<Image>();
        if (icon != null)
        {
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
        }
        else
        {
            iconImg.sprite = UIShapeUtil.RoundedRect(accentColor ?? PlaceholderGray, 10f);
            iconImg.type = Image.Type.Sliced;
        }

        var titleGo = new GameObject("Title", typeof(RectTransform));
        var titleRt = (RectTransform)titleGo.transform;
        titleRt.SetParent(rt, false);
        titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(-padding * 2f, titleHeight);
        titleRt.anchoredPosition = new Vector2(0f, -(padding + iconSize + titleGap));
        var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        titleTxt.text = title;
        titleTxt.fontSize = BaseTitleFont * scale;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = theme.textOnDark;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.enableWordWrapping = true;

        if (!string.IsNullOrEmpty(subtitle))
        {
            var subGo = new GameObject("Subtitle", typeof(RectTransform));
            var subRt = (RectTransform)subGo.transform;
            subRt.SetParent(rt, false);
            subRt.anchorMin = new Vector2(0f, 1f); subRt.anchorMax = new Vector2(1f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.sizeDelta = new Vector2(-padding * 2f, subtitleHeight);
            subRt.anchoredPosition = new Vector2(0f, -(padding + iconSize + titleGap + titleHeight + subtitleGapExtra));
            var subTxt = subGo.AddComponent<TextMeshProUGUI>();
            subTxt.text = subtitle;
            subTxt.fontSize = BaseSubtitleFont * scale;
            subTxt.color = theme.textOnDark;
            subTxt.alignment = TextAlignmentOptions.Top;
            subTxt.enableWordWrapping = true;
        }

        var priceGo = new GameObject("Price", typeof(RectTransform));
        var priceRt = (RectTransform)priceGo.transform;
        priceRt.SetParent(rt, false);
        priceRt.anchorMin = new Vector2(0f, 0f); priceRt.anchorMax = new Vector2(1f, 0f);
        priceRt.pivot = new Vector2(0.5f, 0f);
        priceRt.sizeDelta = new Vector2(-padding * 2f, priceHeight);
        priceRt.anchoredPosition = new Vector2(0f, padding + buyHeight + priceGap);
        var priceTxt = priceGo.AddComponent<TextMeshProUGUI>();
        priceTxt.text = priceLabel;
        priceTxt.fontSize = BasePriceFont * scale;
        priceTxt.fontStyle = FontStyles.Bold;
        priceTxt.color = theme.currencyGold;
        priceTxt.alignment = TextAlignmentOptions.Center;

        // Status (contador de compra da sessão / "X/Y restantes") — acima do botão comprar,
        // abaixo do preço. Atualizado por RefreshPurchaseState a cada clique (ver ShopController).
        var statusGo = new GameObject("Status", typeof(RectTransform));
        var statusRt = (RectTransform)statusGo.transform;
        statusRt.SetParent(rt, false);
        statusRt.anchorMin = new Vector2(0f, 0f); statusRt.anchorMax = new Vector2(1f, 0f);
        statusRt.pivot = new Vector2(0.5f, 0f);
        statusRt.sizeDelta = new Vector2(-padding * 2f, statusHeight);
        statusRt.anchoredPosition = new Vector2(0f, padding + buyHeight + statusGap);
        _statusTxt = statusGo.AddComponent<TextMeshProUGUI>();
        _statusTxt.fontSize = BaseStatusFont * scale;
        _statusTxt.color = theme.textOnDark;
        _statusTxt.alignment = TextAlignmentOptions.Center;

        var buyGo = new GameObject("BtnBuy", typeof(RectTransform));
        var buyRt = (RectTransform)buyGo.transform;
        buyRt.SetParent(rt, false);
        buyRt.anchorMin = new Vector2(0f, 0f); buyRt.anchorMax = new Vector2(1f, 0f);
        buyRt.pivot = new Vector2(0.5f, 0f);
        buyRt.sizeDelta = new Vector2(-padding * 2f, buyHeight);
        buyRt.anchoredPosition = new Vector2(0f, padding);
        _buyBg = buyGo.AddComponent<Image>();
        _buyBg.sprite = UIShapeUtil.RoundedRect(_buyColor, 12f);
        _buyBg.type = Image.Type.Sliced;
        _buyBtn = buyGo.AddComponent<Button>();
        _buyBtn.targetGraphic = _buyBg;
        _buyBtn.onClick.AddListener(() => onBuy?.Invoke());

        var buyLabelGo = new GameObject("Label", typeof(RectTransform));
        var buyLabelRt = (RectTransform)buyLabelGo.transform;
        buyLabelRt.SetParent(buyRt, false);
        buyLabelRt.anchorMin = Vector2.zero; buyLabelRt.anchorMax = Vector2.one;
        buyLabelRt.offsetMin = buyLabelRt.offsetMax = Vector2.zero;
        _buyLabelTxt = buyLabelGo.AddComponent<TextMeshProUGUI>();
        _buyLabelTxt.fontSize = BaseBuyLabelFont * scale;
        _buyLabelTxt.fontStyle = FontStyles.Bold;
        _buyLabelTxt.color = theme.textOnDark;
        _buyLabelTxt.alignment = TextAlignmentOptions.Center;

        // Badge "i" (2026-07-21, pedido do usuário — Passes) no canto superior direito, sem mexer
        // no resto do layout já calibrado — só aparece quando `onInfo` é passado (só Passes por
        // enquanto). Ícone "i" em ASCII puro (não Unicode "ℹ") — mesmo motivo de UIShapeUtil.Star/
        // PlayTriangle existirem: glifos Unicode fora do atlas da fonte TMP do projeto viram
        // "tofu" quebrado.
        if (onInfo != null)
        {
            float infoSize = 32f * scale;
            var infoGo = new GameObject("InfoBadge", typeof(RectTransform));
            var infoRt = (RectTransform)infoGo.transform;
            infoRt.SetParent(rt, false);
            infoRt.anchorMin = infoRt.anchorMax = new Vector2(1f, 1f);
            infoRt.pivot = new Vector2(1f, 1f);
            infoRt.sizeDelta = new Vector2(infoSize, infoSize);
            infoRt.anchoredPosition = new Vector2(-padding * 0.5f, -padding * 0.5f);
            var infoBg = infoGo.AddComponent<Image>();
            infoBg.sprite = UIShapeUtil.RoundedRect(new Color(0f, 0f, 0f, 0.5f), infoSize * 0.5f);
            infoBg.type = Image.Type.Sliced;
            var infoBtn = infoGo.AddComponent<Button>();
            infoBtn.targetGraphic = infoBg;
            infoBtn.onClick.AddListener(() => onInfo());

            var infoLabelGo = new GameObject("Label", typeof(RectTransform));
            var infoLabelRt = (RectTransform)infoLabelGo.transform;
            infoLabelRt.SetParent(infoRt, false);
            infoLabelRt.anchorMin = Vector2.zero; infoLabelRt.anchorMax = Vector2.one;
            infoLabelRt.offsetMin = infoLabelRt.offsetMax = Vector2.zero;
            var infoTxt = infoLabelGo.AddComponent<TextMeshProUGUI>();
            infoTxt.text = "i";
            infoTxt.fontStyle = FontStyles.Bold | FontStyles.Italic;
            infoTxt.fontSize = infoSize * 0.6f;
            infoTxt.color = theme.textOnDark;
            infoTxt.alignment = TextAlignmentOptions.Center;
        }

        RefreshPurchaseState(purchased, limit);
    }

    // Chamado pelo ShopController logo após cada clique (compra fake, Fase 1) — sem limite, só
    // mostra um contador de sessão; com limite (aba Personagens), mostra "restantes" e desabilita
    // o botão ao esgotar (tint cinza sobre o RoundedRect colorido, mesmo truque de
    // ArsenalSlotUI.LockedIconTint). Inalterado nesta tarefa (só layout mudou).
    public void RefreshPurchaseState(int purchased, int limit)
    {
        // Trava de pré-requisito (Progressão) tem prioridade sobre tudo abaixo — o item pode nem
        // estar esgotado (limit/purchased normais), só bloqueado por causa de outro card ainda não
        // comprado. Botão fica cinza/não-clicável mas com o texto "COMPRAR" de sempre (pedido
        // explícito do usuário — diferente do "ESGOTADO" do soldOutLabel).
        if (_locked)
        {
            _statusTxt.text = _lockedReason ?? "Bloqueado";
            _buyBtn.interactable = false;
            _buyBg.color = SoldOutTint;
            _buyLabelTxt.text = _buyLabel;
            return;
        }

        if (limit > 0)
        {
            int remaining = Mathf.Max(0, limit - purchased);
            bool soldOut = remaining <= 0;
            // Com soldOutLabel customizado (Passes — "N dias"), o botão já comunica o estado; o
            // "X/Y restantes" genérico ficaria redundante/confuso ao lado dele, então some.
            _statusTxt.text = (soldOut && _soldOutLabel != null) ? "" : $"{remaining}/{limit} restantes";
            _buyBtn.interactable = !soldOut;
            _buyBg.color = soldOut ? SoldOutTint : Color.white;
            _buyLabelTxt.text = soldOut ? (_soldOutLabel ?? "ESGOTADO") : _buyLabel;
        }
        else
        {
            // _statusOverride (Passes — "Ativo — N dias restantes") tem prioridade sobre o
            // "Comprado Nx (sessão)" genérico; botão continua com o label de sempre nos dois
            // casos — Passes precisam continuar clicáveis pra acumular dias (ver
            // ApplyPassPurchase).
            _statusTxt.text = _statusOverride ?? (purchased > 0 ? $"Comprado {purchased}x (sessão)" : "");
            _buyLabelTxt.text = _buyLabel;
        }
    }
}
