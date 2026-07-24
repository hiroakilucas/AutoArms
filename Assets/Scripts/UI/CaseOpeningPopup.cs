using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Popup de "abertura de case" estilo CS:GO (2026-07-23, sistema de compra de personagens) —
// overlay sobre a cena atual da Loja (Canvas próprio, mesmo padrão de
// ShopController.ShowPassInfoPopup: GameObject temporário, Destroy() ao fechar, não
// instantiate-e-esconde). Já recebe o resultado do servidor (Cloud Function purchaseCase) — não
// sorteia nada aqui, só anima a revelação de um resultado que já está decidido.
public class CaseOpeningPopup : MonoBehaviour
{
    // Tamanho dos ícones da faixa (2026-07-24, 2ª rodada — pedido do usuário: só ~4-5
    // personagens visíveis por vez, era 160/18 (~7-8 visíveis)). ViewportHeight cresce junto
    // (ícone + folga vertical), senão o ícone maior vazaria pra fora do RectMask2D.
    private const float SlotSize = 380f;
    private const float SlotSpacing = 32f;
    private const float SlotBorderThickness = 14f; // proporcional ao novo SlotSize (era 6 pra 160)
    private const float ViewportHeight = SlotSize + 40f;
    private const float SpinDuration = 4.5f;
    private const float StartOffsetSlots = 6f; // giro começa alguns slots "antes" da faixa, não parado em 0

    // Quantos slots o giro percorre visualmente do início ao vencedor (duração percebida/
    // "empolgação" da roleta) — independente do padding de correção abaixo. Mantém o mesmo
    // espírito da distância original (WinningSlotIndex=27 fixo − StartOffsetSlots=6 ≈ 21 slots
    // de percurso).
    private const float SpinTravelSlots = 20f;

    // Bug real corrigido (2026-07-24, 2ª rodada — reportado pelo usuário com screenshot: o
    // vencedor sempre caía no penúltimo slot visível, sobrando espaço vazio à direita da faixa).
    // ReelSlotCount/WinningSlotIndex eram CONSTANTES fixas (32/27) — com a faixa cobrindo a tela
    // inteira e ícones deste tamanho, só 4 slots sobravam depois do vencedor, insuficiente pra
    // cobrir a largura real do viewport em qualquer resolução/aspect ratio. O padding DEPOIS do
    // vencedor (quantos slots sobram no array) passa a ser CALCULADO a partir da largura real
    // medida em runtime (ver Init) — sempre cobre pelo menos um viewport inteiro + esta margem de
    // segurança, nunca mais hardcoded/fixo.
    private const int SafetyMarginSlots = 3;

    // Reveal full-screen (2026-07-24, pedido do usuário — era um card pequeno 520×460 centralizado)
    private const float RevealIconSize = 560f;
    private const float RevealIconBorderThickness = 10f;
    private const float RevealIconTopOffset = 40f;

    private UITheme _theme;
    private CharacterDatabase _characterDatabase;
    private RectTransform _revealGroup;
    private Image _revealIconBorder;
    private Image _revealIcon;
    private TMP_Text _revealName;
    private TMP_Text _revealRarity;

    private string _grantedCharacterId;

    public static void Show(UITheme theme, CharacterDatabase characterDatabase,
        List<string> reelPoolCharacterTypeIds, string wonCharacterTypeId, string grantedCharacterId,
        Action onClosed)
    {
        var go = new GameObject("CaseOpeningPopup (temp)");
        var popup = go.AddComponent<CaseOpeningPopup>();
        popup.Init(theme, characterDatabase, reelPoolCharacterTypeIds, wonCharacterTypeId, grantedCharacterId, onClosed);
    }

    private void Init(UITheme theme, CharacterDatabase characterDatabase,
        List<string> reelPoolCharacterTypeIds, string wonCharacterTypeId, string grantedCharacterId,
        Action onClosed)
    {
        _theme = theme;
        _characterDatabase = characterDatabase;
        _grantedCharacterId = grantedCharacterId;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        BuildBackground(transform);
        var viewportRt = BuildViewport(transform);

        // Bug real corrigido (2026-07-25 — reportado pelo usuário: o marcador nunca alinhava
        // exatamente com o vencedor). Causa: o Canvas/CanvasScaler acabaram de ser criados NESTE
        // MESMO frame (linhas acima) — a RectTransform de um Canvas ScreenSpaceOverlay recém-
        // criado só reflete o tamanho REAL da tela depois que o sistema de Canvas roda seu
        // próprio update interno (não é síncrono só por termos setado CanvasScaler em código);
        // ler `viewportRt.rect.width` antes disso podia devolver um valor obsoleto/placeholder
        // (ex: 100×100, o tamanho padrão de uma RectTransform nova), fazendo `finalX` parar a
        // faixa numa posição calculada pra uma largura de tela ERRADA — o vencedor não ficava,
        // então, sob o marcador de verdade. `Canvas.ForceUpdateCanvases()` força esse rebuild
        // imediatamente, garantindo que a leitura abaixo reflita a largura real da tela.
        Canvas.ForceUpdateCanvases();

        // Largura medida em runtime — o viewport estica âncora-a-âncora pela tela inteira (ver
        // BuildViewport), `rect.width` agora reflete o tamanho real — nenhuma constante de
        // largura fixa envolvida.
        float viewportWidth = viewportRt.rect.width;
        float slotStride = SlotSize + SlotSpacing;

        // winningSlotIndex controla a DISTÂNCIA percorrida pelo giro (SpinTravelSlots, fixo —
        // sensação de duração/empolgação); totalSlotCount é quem corrige o bug — o padding DEPOIS
        // do vencedor (trailingPadding) é calculado a partir de quantos slots cabem visíveis na
        // largura REAL do viewport + margem de segurança, então nunca sobra espaço vazio no fim
        // da faixa, em qualquer resolução/aspect ratio/tamanho de ícone.
        int visibleSlots = Mathf.CeilToInt(viewportWidth / slotStride);
        int winningSlotIndex = Mathf.CeilToInt(StartOffsetSlots + SpinTravelSlots);
        int trailingPadding = visibleSlots + SafetyMarginSlots;
        int totalSlotCount = winningSlotIndex + trailingPadding + 1;

        var content = BuildContent(viewportRt, totalSlotCount);
        BuildMarker(viewportRt);

        var slotIds = BuildReelSlotIds(reelPoolCharacterTypeIds, wonCharacterTypeId, totalSlotCount, winningSlotIndex);
        PopulateSlots(content, slotIds);

        BuildRevealGroup(transform, onClosed);

        float startX = -StartOffsetSlots * slotStride;
        float finalX = viewportWidth / 2f - (winningSlotIndex * slotStride + SlotSize / 2f);

        var spinner = gameObject.AddComponent<CaseReelSpinner>();
        spinner.Play(content, startX, finalX, SpinDuration, () => OnSpinComplete(wonCharacterTypeId));
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private void BuildBackground(Transform parent)
    {
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(parent, false);
        var rt = bgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = bgGo.AddComponent<Image>();
        img.color = Color.black; // 2026-07-24 — era 90% opacidade, pedido do usuário: preto sólido
        // Sem Button/onClick de propósito — este popup só fecha pelo botão "Continuar" no card de
        // reveal, nunca clicando fora (evita fechar sem querer no meio do giro).
    }

    private RectTransform BuildViewport(Transform parent)
    {
        var viewportGo = new GameObject("ReelViewport");
        viewportGo.transform.SetParent(parent, false);
        var viewportRt = viewportGo.AddComponent<RectTransform>();
        // Full-width de verdade (2026-07-24, 2ª rodada — pedido do usuário: "sem sobrar área
        // preta sem cobertura"; ainda sobrava uma margem de 24px de cada lado antes) — âncoras
        // full-width, sem nenhum offset.
        viewportRt.anchorMin = new Vector2(0f, 0.55f);
        viewportRt.anchorMax = new Vector2(1f, 0.55f);
        viewportRt.pivot = new Vector2(0.5f, 0.5f);
        viewportRt.sizeDelta = new Vector2(0f, ViewportHeight);
        viewportRt.anchoredPosition = Vector2.zero;
        viewportGo.AddComponent<RectMask2D>();
        var viewportBg = viewportGo.AddComponent<Image>();
        viewportBg.sprite = UIShapeUtil.RoundedRect(_theme.panelBackground, 16f);
        viewportBg.type = Image.Type.Sliced;
        return viewportRt;
    }

    // Separado de BuildViewport (2026-07-24, 2ª rodada) — o total de slots só é conhecido depois
    // de medir a largura real do viewport (ver Init), então o Content precisa ser construído
    // numa segunda etapa, já sabendo quantos slots cabem.
    private RectTransform BuildContent(RectTransform viewport, int totalSlotCount)
    {
        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewport.transform, false);
        var contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin = contentRt.anchorMax = new Vector2(0f, 0.5f);
        contentRt.pivot = new Vector2(0f, 0.5f);
        contentRt.sizeDelta = new Vector2(totalSlotCount * (SlotSize + SlotSpacing), ViewportHeight);
        contentRt.anchoredPosition = Vector2.zero;
        return contentRt;
    }

    private void BuildMarker(RectTransform viewport)
    {
        var markerGo = new GameObject("Marker");
        markerGo.transform.SetParent(viewport.parent, false);
        var rt = markerGo.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.55f);
        rt.sizeDelta = new Vector2(6f, ViewportHeight + 30f);
        rt.anchoredPosition = Vector2.zero;
        var img = markerGo.AddComponent<Image>();
        // Amarelo sutil (2026-07-24, pedido do usuário — era primaryAction/vermelho, 100% opaco)
        // — reaproveita currencyGold (já existente em UITheme, nenhuma cor nova inventada), só
        // com a opacidade bem reduzida pra marcar a posição sem dominar a composição.
        img.color = new Color(_theme.currencyGold.r, _theme.currencyGold.g, _theme.currencyGold.b, 0.45f);
        img.raycastTarget = false;
    }

    private List<string> BuildReelSlotIds(List<string> pool, string winner, int slotCount, int winningIndex)
    {
        if (pool == null || pool.Count == 0) pool = new List<string> { winner };
        var slots = new List<string>(slotCount);
        for (int i = 0; i < slotCount; i++)
            slots.Add(pool[UnityEngine.Random.Range(0, pool.Count)]);
        slots[winningIndex] = winner;
        return slots;
    }

    private void PopulateSlots(RectTransform content, List<string> slotIds)
    {
        float slotStride = SlotSize + SlotSpacing;
        for (int i = 0; i < slotIds.Count; i++)
        {
            var icon = ResolveIcon(slotIds[i], out var rarity, out _);

            // Borda por raridade (2026-07-24, pedido do usuário) — GameObject "Border" sizeado em
            // SlotSize, tintado por UITheme.RarityColor; o ícone entra INSET dentro dele (mesmo
            // espírito do padrão bronze/prata/ouro de ArsenalSlotUI), formando um anel visível.
            var slotGo = new GameObject($"Slot_{i}");
            slotGo.transform.SetParent(content, false);
            var slotRt = slotGo.AddComponent<RectTransform>();
            slotRt.anchorMin = slotRt.anchorMax = new Vector2(0f, 0.5f);
            slotRt.pivot = new Vector2(0f, 0.5f);
            slotRt.sizeDelta = new Vector2(SlotSize, SlotSize);
            slotRt.anchoredPosition = new Vector2(i * slotStride, 0f);
            var borderImg = slotGo.AddComponent<Image>();
            borderImg.sprite = UIShapeUtil.RoundedRect(_theme.RarityColor(rarity), 12f);
            borderImg.type = Image.Type.Sliced;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(SlotBorderThickness, SlotBorderThickness);
            iconRt.offsetMax = new Vector2(-SlotBorderThickness, -SlotBorderThickness);
            var img = iconGo.AddComponent<Image>();
            if (icon != null) { img.sprite = icon; img.preserveAspect = true; }
            else { img.sprite = UIShapeUtil.RoundedRect(_theme.secondaryButtonAlt, 8f); img.type = Image.Type.Sliced; }
        }
    }

    private Sprite ResolveIcon(string characterTypeId, out CharacterRarity rarity, out string displayName)
    {
        rarity = CharacterRarity.Normal;
        displayName = characterTypeId;
        if (_characterDatabase?.unlockedCharacters == null) return null;
        foreach (var p in _characterDatabase.unlockedCharacters)
        {
            if (p != null && p.name == characterTypeId)
            {
                rarity = p.rarity;
                displayName = string.IsNullOrEmpty(p.profileName) ? p.name : p.profileName;
                return p.previewIcon;
            }
        }
        return null;
    }

    private void BuildRevealGroup(Transform parent, Action onClosed)
    {
        var groupGo = new GameObject("RevealGroup");
        groupGo.transform.SetParent(parent, false);
        _revealGroup = groupGo.AddComponent<RectTransform>();
        // Full-screen (2026-07-24, pedido do usuário — era um card pequeno 520×460 centralizado)
        // — âncoras esticadas com margem modesta, em vez de sizeDelta+anchoredPosition fixos.
        _revealGroup.anchorMin = new Vector2(0.06f, 0.05f);
        _revealGroup.anchorMax = new Vector2(0.94f, 0.95f);
        _revealGroup.offsetMin = _revealGroup.offsetMax = Vector2.zero;
        groupGo.SetActive(false); // some até o giro parar

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(groupGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero; panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.sprite = UIShapeUtil.RoundedRect(_theme.panelBackgroundAlt, 24f);
        panelImg.type = Image.Type.Sliced;

        // Ícone bem maior (2026-07-24, era 200×200) com borda de raridade (sprite atribuído só em
        // OnSpinComplete, quando a raridade do vencedor já é conhecida).
        var iconBorderGo = new GameObject("IconBorder");
        iconBorderGo.transform.SetParent(groupGo.transform, false);
        var iconBorderRt = iconBorderGo.AddComponent<RectTransform>();
        iconBorderRt.anchorMin = new Vector2(0.5f, 1f); iconBorderRt.anchorMax = new Vector2(0.5f, 1f);
        iconBorderRt.pivot = new Vector2(0.5f, 1f);
        iconBorderRt.sizeDelta = new Vector2(RevealIconSize, RevealIconSize);
        iconBorderRt.anchoredPosition = new Vector2(0f, -RevealIconTopOffset);
        _revealIconBorder = iconBorderGo.AddComponent<Image>();
        _revealIconBorder.type = Image.Type.Sliced;

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(iconBorderGo.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(RevealIconBorderThickness, RevealIconBorderThickness);
        iconRt.offsetMax = new Vector2(-RevealIconBorderThickness, -RevealIconBorderThickness);
        _revealIcon = iconGo.AddComponent<Image>();
        _revealIcon.preserveAspect = true;

        float y = -(RevealIconTopOffset + RevealIconSize);

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(groupGo.transform, false);
        var nameRt = nameGo.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.05f, 1f); nameRt.anchorMax = new Vector2(0.95f, 1f);
        nameRt.pivot = new Vector2(0.5f, 1f);
        nameRt.sizeDelta = new Vector2(0f, 60f);
        nameRt.anchoredPosition = new Vector2(0f, y);
        _revealName = nameGo.AddComponent<TextMeshProUGUI>();
        _revealName.fontSize = 42;
        _revealName.fontStyle = FontStyles.Bold;
        _revealName.color = _theme.textOnDark;
        _revealName.alignment = TextAlignmentOptions.Center;

        y -= 70f;
        var rarityGo = new GameObject("Rarity");
        rarityGo.transform.SetParent(groupGo.transform, false);
        var rarityRt = rarityGo.AddComponent<RectTransform>();
        rarityRt.anchorMin = new Vector2(0.05f, 1f); rarityRt.anchorMax = new Vector2(0.95f, 1f);
        rarityRt.pivot = new Vector2(0.5f, 1f);
        rarityRt.sizeDelta = new Vector2(0f, 40f);
        rarityRt.anchoredPosition = new Vector2(0f, y);
        _revealRarity = rarityGo.AddComponent<TextMeshProUGUI>();
        _revealRarity.fontSize = 26;
        _revealRarity.fontStyle = FontStyles.Bold;
        _revealRarity.alignment = TextAlignmentOptions.Center;

        y -= 50f;
        var noteGo = new GameObject("Note");
        noteGo.transform.SetParent(groupGo.transform, false);
        var noteRt = noteGo.AddComponent<RectTransform>();
        noteRt.anchorMin = new Vector2(0.08f, 1f); noteRt.anchorMax = new Vector2(0.92f, 1f);
        noteRt.pivot = new Vector2(0.5f, 1f);
        noteRt.sizeDelta = new Vector2(0f, 60f);
        noteRt.anchoredPosition = new Vector2(0f, y);
        var noteTxt = noteGo.AddComponent<TextMeshProUGUI>();
        noteTxt.text = "Adicionado à sua coleção.";
        noteTxt.fontSize = 20;
        noteTxt.color = _theme.textOnDark;
        noteTxt.alignment = TextAlignmentOptions.Center;
        noteTxt.enableWordWrapping = true;

        var okGo = new GameObject("BtnContinuar");
        okGo.transform.SetParent(groupGo.transform, false);
        var okRt = okGo.AddComponent<RectTransform>();
        okRt.anchorMin = okRt.anchorMax = new Vector2(0.5f, 0f);
        okRt.pivot = new Vector2(0.5f, 0f);
        okRt.sizeDelta = new Vector2(300f, 84f);
        okRt.anchoredPosition = new Vector2(0f, 40f);
        var okImg = okGo.AddComponent<Image>();
        okImg.sprite = UIShapeUtil.RoundedRect(_theme.primaryAction, 16f);
        okImg.type = Image.Type.Sliced;
        var okBtn = okGo.AddComponent<Button>();
        okBtn.targetGraphic = okImg;
        okBtn.onClick.AddListener(() =>
        {
            onClosed?.Invoke();
            // Fluxo case opening → seleção (2026-07-24): "Continuar" navega direto pra
            // 02_SelectCharacter com o personagem recém-concedido em destaque, em vez de só
            // voltar pra Loja. PendingCharacterSelection é o handoff (CharacterSelectController
            // lê e limpa assim que o roster carrega — ver ARQUITETURA.md).
            PendingCharacterSelection.PendingCharacterId = _grantedCharacterId;
            SceneManager.LoadScene("02_SelectCharacter");
        });

        var okLabelGo = new GameObject("Label");
        okLabelGo.transform.SetParent(okGo.transform, false);
        var okLabelRt = okLabelGo.AddComponent<RectTransform>();
        okLabelRt.anchorMin = Vector2.zero; okLabelRt.anchorMax = Vector2.one;
        okLabelRt.offsetMin = okLabelRt.offsetMax = Vector2.zero;
        var okLabelTxt = okLabelGo.AddComponent<TextMeshProUGUI>();
        okLabelTxt.text = "Continuar";
        okLabelTxt.fontSize = 24;
        okLabelTxt.fontStyle = FontStyles.Bold;
        okLabelTxt.color = _theme.textOnDark;
        okLabelTxt.alignment = TextAlignmentOptions.Center;
    }

    private static readonly string[] RarityLabels = { "Normal", "Incomum", "Raro", "Legendary", "Imortal" };

    private void OnSpinComplete(string wonCharacterTypeId)
    {
        var icon = ResolveIcon(wonCharacterTypeId, out var rarity, out var displayName);
        var rarityColor = _theme.RarityColor(rarity);

        // Bug real corrigido (2026-07-25, reportado pelo usuário: "WinGlow está sobre o painel
        // do personagem sorteado") — o WinGlow antigo destacava o ícone vencedor NA FAIXA DA
        // ROLETA (posição fixa em y=0.55, sob o marcador), pensado pra uma época em que o card de
        // reveal era pequeno e a faixa continuava visível atrás dele. Desde que o reveal virou
        // full-screen (2026-07-24), o painel de reveal cobre a tela quase inteira, INCLUSIVE a
        // faixa — o glow, criado DEPOIS do painel (último na hierarquia = desenha por cima de
        // tudo), acabava flutuando sobre o painel de reveal em vez de destacar algo visível.
        // Removido por completo — a borda de raridade do próprio ícone de reveal
        // (`_revealIconBorder`, logo abaixo) já cumpre o papel de "destaque por raridade" no
        // layout atual.

        _revealIconBorder.sprite = UIShapeUtil.RoundedRect(rarityColor, 24f);
        _revealIcon.sprite = icon;
        if (icon == null) _revealIcon.sprite = UIShapeUtil.RoundedRect(_theme.secondaryButtonAlt, 16f);
        _revealName.text = displayName;
        _revealRarity.text = RarityLabels[Mathf.Clamp((int)rarity, 0, RarityLabels.Length - 1)];
        _revealRarity.color = rarityColor;

        _revealGroup.gameObject.SetActive(true);
        StartCoroutine(PunchScale(_revealGroup));
    }

    private IEnumerator PunchScale(RectTransform target)
    {
        const float duration = 0.3f;
        const float peakScale = 1.15f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Sobe até peakScale na metade e volta a 1 — mesmo espírito de um "punch" de escala
            // sem depender de DOTween.Punch (não está no projeto).
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * (peakScale - 1f);
            target.localScale = Vector3.one * scale;
            yield return null;
        }
        target.localScale = Vector3.one;
    }
}
