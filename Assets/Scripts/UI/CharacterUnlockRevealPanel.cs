using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Reveal sequencial dos unlocks de skill/arma/pet concedidos ao ganhar um personagem via case
// opening (2026-07-25, ver CharacterUnlockEngine/CharacterSelectController.ResolveCaseUnlocksAsync)
// - card simples (icone com borda por tier + categoria + nome + tier + descricao) com um botao
// "Continuar" avancando pro proximo, um unlock por vez. Proposta deliberadamente simples (pedido
// do usuario: "nao precisa ser tao elaborado quanto a roleta de personagem").
//
// Refresh (2026-07-25, mesmo dia) — botao "Refresh" ao lado de "Continuar", custo FIXO em
// diamante (UnlockRerollService.CostDiamonds), ate UnlockRerollService.MaxRerollsPerUnlock vezes
// por unlock — resorteio em si e a validacao de saldo/limite sao 100% server-side
// (rerollUnlock), este painel so exibe o resultado que a function devolveu e reflete o saldo/
// contador localmente (mesmo padrao de espelhamento de PlayerEconomyState usado no resto do
// jogo). Some quando os refreshes acabam; fica desabilitado (mas visivel) se o saldo local
// conhecido for insuficiente, sem travar o fluxo (usuario ainda pode aceitar com "Continuar").
//
// Canvas PROPRIO (ScreenSpaceOverlay, sortingOrder acima do de CharacterPanel) em vez de parentar
// no Canvas principal da cena - mesma regra de CharacterPanel (ver comentario em
// CharacterPanel.BuildUI): o GameObject deste componente NUNCA pode ser parented sob outro
// Canvas, senao o Canvas filho vira "aninhado" e ignora seu proprio renderMode/CanvasScaler. Como
// o reveal precisa cobrir o CharacterPanel (que ja esta visivel/expandido nesse ponto do fluxo),
// sortingOrder tem que ficar acima do 20 que CharacterPanel usa pro seu proprio Canvas.
public class CharacterUnlockRevealPanel : MonoBehaviour
{
    private const int SortingOrder = 50;
    private const float CardWidth = 520f;
    private const float CardHeight = 520f;
    private const float IconSize = 140f;
    private const float ButtonWidth = 235f;
    private const float ButtonHeight = 56f;
    private const float ButtonGap = 10f;

    private UITheme theme;
    private GameObject root;
    private TextMeshProUGUI progressTxt;
    private TextMeshProUGUI categoryTxt;
    private TextMeshProUGUI nameTxt;
    private TextMeshProUGUI tierTxt;
    private TextMeshProUGUI descTxt;
    private TextMeshProUGUI infoTxt;
    private Image iconBorder;
    private Image icon;

    private GameObject refreshBtnGo;
    private Button refreshBtn;
    private Image refreshBtnImg;
    private TextMeshProUGUI refreshBtnLabel;
    private GameObject continueBtnGo;
    private Button continueBtn;

    private Action onContinue;
    private Action onRefresh;
    private int currentRemainingRerolls;

    public void Build(UITheme theme)
    {
        this.theme = theme;

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root");
        root.transform.SetParent(canvasGo.transform, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        var backdrop = root.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.75f);
        // Sem onClick proprio - so existe pra bloquear o raycast dos botoes/grid atras enquanto
        // o reveal esta em tela (mesmo truque do painel do CharacterPanel).
        root.AddComponent<Button>();

        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(root.transform, false);
        var cardRt = cardGo.AddComponent<RectTransform>();
        cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(CardWidth, CardHeight);
        cardRt.anchoredPosition = Vector2.zero;
        var cardImg = cardGo.AddComponent<Image>();
        cardImg.sprite = UIShapeUtil.RoundedRect(theme.panelBackgroundAlt, 20f);
        cardImg.type = Image.Type.Sliced;

        progressTxt = MakeLabel(cardGo.transform, new Vector2(0f, CardHeight / 2f - 34f), CardWidth - 40f, 30f, 18, theme.secondaryButtonAlt);
        progressTxt.alignment = TextAlignmentOptions.Center;

        var iconBorderGo = new GameObject("IconBorder");
        iconBorderGo.transform.SetParent(cardGo.transform, false);
        var ibRt = iconBorderGo.AddComponent<RectTransform>();
        ibRt.anchorMin = ibRt.anchorMax = new Vector2(0.5f, 0.5f);
        ibRt.sizeDelta = new Vector2(IconSize + 14f, IconSize + 14f);
        ibRt.anchoredPosition = new Vector2(0f, 130f);
        iconBorder = iconBorderGo.AddComponent<Image>();
        iconBorder.sprite = UIShapeUtil.RoundedRect(theme.TierColor(1), 12f);
        iconBorder.type = Image.Type.Sliced;

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(iconBorderGo.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(IconSize, IconSize);
        iconRt.anchoredPosition = Vector2.zero;
        icon = iconGo.AddComponent<Image>();
        icon.preserveAspect = true;

        categoryTxt = MakeLabel(cardGo.transform, new Vector2(0f, 30f), CardWidth - 40f, 28f, 20, theme.currencyGold);
        categoryTxt.alignment = TextAlignmentOptions.Center;
        categoryTxt.fontStyle = FontStyles.Bold;

        nameTxt = MakeLabel(cardGo.transform, new Vector2(0f, -6f), CardWidth - 40f, 36f, 28, theme.textOnDark);
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.fontStyle = FontStyles.Bold;

        tierTxt = MakeLabel(cardGo.transform, new Vector2(0f, -40f), CardWidth - 40f, 26f, 18, theme.TierColor(1));
        tierTxt.alignment = TextAlignmentOptions.Center;
        tierTxt.fontStyle = FontStyles.Bold;

        descTxt = MakeLabel(cardGo.transform, new Vector2(0f, -92f), CardWidth - 70f, 90f, 18, theme.textOnDark);
        descTxt.alignment = TextAlignmentOptions.Center;
        descTxt.enableWordWrapping = true;

        // Linha de status do refresh (contador restante + saldo de diamante conhecido) — atualizada
        // a cada Show()/refresh bem-sucedido, também reaproveitada pra mostrar erro transitório se
        // o servidor recusar o refresh (ex: saldo mudou entre a checagem local e a chamada).
        infoTxt = MakeLabel(cardGo.transform, new Vector2(0f, -155f), CardWidth - 40f, 24f, 16, theme.secondaryButtonAlt);
        infoTxt.alignment = TextAlignmentOptions.Center;

        float leftX = -(ButtonWidth / 2f + ButtonGap / 2f);
        float rightX = ButtonWidth / 2f + ButtonGap / 2f;

        (refreshBtnGo, refreshBtn, refreshBtnImg, refreshBtnLabel) = BuildButton(
            cardGo.transform, "BtnRefresh", leftX, theme.secondaryButton, "", () => onRefresh?.Invoke());

        (continueBtnGo, continueBtn, _, _) = BuildButton(
            cardGo.transform, "BtnContinuar", rightX, theme.primaryAction, "Continuar", () => onContinue?.Invoke());

        root.SetActive(false);
    }

    private (GameObject go, Button btn, Image img, TextMeshProUGUI label) BuildButton(
        Transform parent, string name, float centerX, Color color, string label, Action onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        rt.anchoredPosition = new Vector2(centerX, 24f);
        var img = go.AddComponent<Image>();
        img.sprite = UIShapeUtil.RoundedRect(color, 12f);
        img.type = Image.Type.Sliced;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var lRt = labelGo.AddComponent<RectTransform>();
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        lRt.offsetMin = lRt.offsetMax = Vector2.zero;
        var lTxt = labelGo.AddComponent<TextMeshProUGUI>();
        lTxt.text = label;
        lTxt.fontSize = 22;
        lTxt.enableAutoSizing = true;
        lTxt.fontSizeMin = 12;
        lTxt.fontSizeMax = 22;
        lTxt.fontStyle = FontStyles.Bold;
        lTxt.color = theme.textOnDark;
        lTxt.alignment = TextAlignmentOptions.Center;

        return (go, btn, img, lTxt);
    }

    private TextMeshProUGUI MakeLabel(Transform parent, Vector2 anchoredPos, float width, float height, int fontSize, Color color)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = anchoredPos;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.fontSize = fontSize;
        txt.color = color;
        return txt;
    }

    public void Show(int index, int total, LevelUpOption option, int remainingRerolls, Action onContinueClicked, Action onRefreshClicked)
    {
        onContinue = onContinueClicked;
        onRefresh = onRefreshClicked;
        currentRemainingRerolls = remainingRerolls;

        progressTxt.text = $"Desbloqueio {index}/{total}";
        categoryTxt.text = option.kind switch
        {
            LevelUpOption.Kind.Skill  => "HABILIDADE",
            LevelUpOption.Kind.Weapon => "ARMA",
            LevelUpOption.Kind.Pet    => "PET",
            _                         => "",
        };
        nameTxt.text = option.kind switch
        {
            LevelUpOption.Kind.Skill  => option.skill != null ? option.skill.skillName : "?",
            LevelUpOption.Kind.Weapon => option.weapon != null ? WeaponNameUtil.StripWeaponTierSuffix(option.weapon.weaponName) : "?",
            LevelUpOption.Kind.Pet    => option.petData != null ? PetState.DisplayName(option.petData.petType) : "?",
            _                         => "?",
        };
        int tier = option.kind switch
        {
            LevelUpOption.Kind.Skill  => option.skill != null ? option.skill.tier : 1,
            LevelUpOption.Kind.Weapon => option.weapon != null ? option.weapon.tier : 1,
            LevelUpOption.Kind.Pet    => option.petData != null ? option.petData.tier : 1,
            _                         => 1,
        };
        Color tierColor = TierColor(tier);
        tierTxt.text = $"Tier {tier}";
        tierTxt.color = tierColor;
        iconBorder.sprite = UIShapeUtil.RoundedRect(tierColor, 12f);
        descTxt.text = option.Desc();

        Sprite spr = option.kind switch
        {
            LevelUpOption.Kind.Skill  => option.skill != null ? option.skill.icon : null,
            LevelUpOption.Kind.Weapon => option.weapon != null ? option.weapon.icon : null,
            LevelUpOption.Kind.Pet    => option.petData != null ? option.petData.icon : null,
            _                         => null,
        };
        icon.sprite = spr;
        icon.color = spr != null ? Color.white : new Color(1f, 1f, 1f, 0f);

        RefreshRerollButtonState();
        SetBusy(false);
        root.SetActive(true);
    }

    // Reavalia a aparência/interatividade do botão de refresh a partir do estado atual
    // (currentRemainingRerolls + PlayerEconomyState.Diamonds conhecido localmente) — chamado ao
    // exibir um unlock novo e depois de cada refresh (bem-sucedido ou não). Nunca é a fonte da
    // verdade (o servidor sempre revalida de novo em rerollUnlock), só evita uma chamada
    // desperdiçada quando já dá pra saber de antemão que vai falhar.
    private void RefreshRerollButtonState()
    {
        if (currentRemainingRerolls <= 0)
        {
            // Acabaram os refreshes deste unlock — some por completo (pedido do usuário), só
            // "Continuar" resta.
            refreshBtnGo.SetActive(false);
            infoTxt.text = "Sem mais refreshes pra este item";
            infoTxt.color = theme.secondaryButtonAlt;
            return;
        }

        refreshBtnGo.SetActive(true);
        bool hasEnoughDiamonds = PlayerEconomyState.Diamonds >= UnlockRerollService.CostDiamonds;
        refreshBtn.interactable = hasEnoughDiamonds;
        refreshBtnImg.color = hasEnoughDiamonds ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        // Contagem de refreshes restantes direto no próprio botão (pedido do usuário) — não só na
        // linha de status abaixo, pra ficar óbvio ANTES de clicar que só sobram N usos.
        refreshBtnLabel.text = hasEnoughDiamonds
            ? $"Refresh ({UnlockRerollService.CostDiamonds} diamantes) — {currentRemainingRerolls} restante{(currentRemainingRerolls == 1 ? "" : "s")}"
            : $"Saldo insuficiente ({currentRemainingRerolls} restante{(currentRemainingRerolls == 1 ? "" : "s")})";

        infoTxt.text = $"{currentRemainingRerolls} refresh{(currentRemainingRerolls == 1 ? "" : "es")} restante{(currentRemainingRerolls == 1 ? "" : "s")} · {PlayerEconomyState.Diamonds} diamantes";
        infoTxt.color = theme.secondaryButtonAlt;
    }

    // Chamado pelo controller enquanto a chamada de rerollUnlock está em andamento — bloqueia os
    // dois botões (evita clique duplo/corrida entre "Continuar" e "Refresh" no meio de uma
    // chamada de rede).
    public void SetBusy(bool busy)
    {
        continueBtn.interactable = !busy;
        if (busy)
        {
            refreshBtn.interactable = false;
        }
        else
        {
            RefreshRerollButtonState();
        }
    }

    // Mostra um erro transitório do refresh (ex: servidor recusou por saldo/limite mudado entre a
    // checagem local e a chamada) na mesma linha de status, sem fechar o card nem consumir o
    // refresh — o jogador pode tentar de novo ou só clicar "Continuar" com o resultado atual.
    public void ShowRefreshError(string message)
    {
        infoTxt.text = string.IsNullOrEmpty(message) ? "Refresh falhou — tente de novo." : message;
        infoTxt.color = theme.danger;
        SetBusy(false);
    }

    // Delegado a UITheme.TierColor (2026-07-27) — mesma cor por tier de qualquer outra tela.
    private Color TierColor(int tier) => theme.TierColor(tier);

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }
}
