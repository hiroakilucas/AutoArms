using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

// Cena 05_SelectOpponent — grid de até 6 oponentes. Tenta buscar adversários REAIS na nuvem
// primeiro (OpponentSearchService/opponents_index, priorizando level próximo via levelBucket,
// 2026-07-15); se vier incompleto, completa com bots gerados em runtime (personagens de raridade
// Normal em CharacterDatabase.unlockedCharacters + BotProfileGenerator, escalados pro level do
// jogador, 2026-07-16 — ver GenerateBotOpponents); só cai pro pool local antigo
// (CharacterDatabase.opponentCharacters, excluindo o profile que o jogador está usando) como
// fallback de ÚLTIMA instância, se nem os bots renderem nada. Toda a UI é construída em código
// (mesmo padrão de CombatResultPanel/CharacterPanel/MainMenuCharacterPreview) — não existe
// prefab de card nesta cena.
public class SelectOpponentController : MonoBehaviour
{
    [SerializeField] private CharacterDatabase characterDatabase;
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;
    [SerializeField] private SelectedOpponentHolder selectedOpponentHolder;

    // Wireados manualmente no Inspector (2026-07-15) — mesmos assets já arrastados em
    // AttackSequencer (04_CombatScenePVP): SkillDatabase.asset e os 78 WeaponData (T1/T2/T3) de
    // Assets/Data/Weapons/. Sem eles, BotProfileGenerator não consegue dar skill/arma pros bots
    // (pool vazio) nem deixá-los subir de tier.
    [SerializeField] private SkillDatabase skillDatabase;
    [SerializeField] private WeaponData[] allWeapons;
    // T1 dos 3 pets (2026-07-16, mesmo motivo/padrão acima) — sem isso, bots nunca sorteiam pet
    // nenhum no level-up simulado (LevelUpEngine.DrawOption degrada o peso de pet pra 0 se a
    // lista vier vazia).
    [SerializeField] private PetData[] petPool;

    // Wireado manualmente no Inspector (2026-07-15) — mesmo UITheme.asset usado em
    // CharacterPanel/01_MainMenu — necessário pra AttributePipBar.Build (cor do label/badge).
    [SerializeField] private UITheme theme;

    private const int MaxOpponents = 6;
    private const string CombatSceneName = "04_CombatScenePVP";
    private const string MainMenuSceneName = "01_MainMenu";

    // Grid 2 colunas × 3 linhas (2026-07-16, pedido do usuário) — card bem maior e mais legível
    // em vez do layout de 1 linha × 6 colunas estreitas que o GridLayoutGroup produzia antes sem
    // nenhuma constraint (cabiam ~6 células de 260px lado a lado na largura do ScrollView).
    // Retrato grande na coluna ESQUERDA do card; todo o resto (nome/level/HP/atributos/itens)
    // empilhado numa coluna DIREITA própria (`RightPanel`, ver BuildCard).
    //
    // Altura reduzida à metade (2026-07-16, pedido do usuário) — junto da remoção do botão
    // "Escolher" (o card inteiro virou clicável, ver PressableCard.cs) e do texto de histórico de
    // batalhas, sobrou espaço mesmo com metade da altura; skill/arma/pet viraram uma única linha
    // combinada (`MakeItemIconsRow`) em vez de 2 linhas separadas — o suficiente pra caber os 3
    // stats cada um na sua própria linha (`MakeAttributeRow`, empilhados) mais HP/Level e a linha
    // de itens, tudo dentro da metade da altura.
    private const int GridColumns = 2;
    private const float CardWidth = 820f;
    private const float CardHeight = 320f;
    private const float CardPadding = 24f;
    private const float PortraitSize = 260f;
    private const float RightPanelWidth = CardWidth - PortraitSize - CardPadding * 3f;

    // Ícones de skill/arma/pet aumentados de 36 pra 56px (2026-07-15→16: "não está legível").
    private const float IconSize = 56f;
    private const float IconRowHeight = 68f;

    static readonly Color Gold          = new Color(0.85f, 0.72f, 0.35f, 1f);
    static readonly Color PanelBg       = new Color(0.07f, 0.06f, 0.09f, 1f);
    static readonly Color CardBg        = new Color(0.15f, 0.10f, 0.07f, 0.98f);
    static readonly Color CardBgPressed = new Color(0.24f, 0.17f, 0.11f, 0.98f);

    // Popup de detalhe (2026-07-16, pedido do usuário — "mesmo nível de detalhe do MainMenu/
    // Arsenal") — reaproveita o MESMO `CharacterPanel` usado em 01_MainMenu/02_SelectCharacter e
    // instanciado "de cabeça" por 03_Arsenal (`ArsenalController.EnsureDetailPanel`): monta um
    // `CharacterPanel` real, escondendo o HUD Compact/Expanded (`HideRootPermanently`) e usando
    // só o popup dele (`ShowSkillDetail`/`ShowWeaponDetail`/`ShowPetDetail`) — nome, borda por
    // tier, descrição/efeito (skill) ou stats completos (arma), no clique do ícone, em vez do
    // tooltip de hover só-com-nome da rodada anterior (não reaproveitava nenhum padrão existente
    // no jogo). Construção preguiçosa (só na 1ª vez que um ícone é clicado), mesmo motivo de
    // perf documentado em ArsenalController.
    private CharacterPanel _detailPanel;

    private void EnsureDetailPanel()
    {
        if (_detailPanel != null) return;
        var go = new GameObject("CharacterPanel (Opponent Detail)");
        _detailPanel = go.AddComponent<CharacterPanel>();
        _detailPanel.Setup(selectedProfileHolder, theme);
        _detailPanel.HideRootPermanently();
    }

    void Start()
    {
        EnsureEventSystem();
        var canvas = CreateCanvas();
        BuildTitle(canvas);
        BuildBackButton(canvas);
        StartCoroutine(BuildGridRoutine(canvas));
    }

    private IEnumerator BuildGridRoutine(GameObject canvas)
    {
        int playerLevel = selectedProfileHolder != null && selectedProfileHolder.currentProfile != null
            ? selectedProfileHolder.currentProfile.level
            : 1;

        var onlineTask = OpponentSearchService.FetchOpponentsAsync(characterDatabase, playerLevel, MaxOpponents);
        yield return new WaitUntil(() => onlineTask.IsCompleted);

        var opponents = onlineTask.Result ?? new List<PlayerProfile>();

        // Fallback intermediário (2026-07-15): completa com bots escalados pro level do jogador
        // antes de cair pro pool antigo (opponentCharacters) — esse último vira fallback de
        // ÚLTIMA instância, só se nenhum personagem de raridade Normal existir (nunca deve
        // acontecer de verdade, ver GenerateBotOpponents).
        if (opponents.Count < MaxOpponents)
            opponents.AddRange(GenerateBotOpponents(MaxOpponents - opponents.Count, playerLevel));

        if (opponents.Count == 0)
            opponents = GetLocalOpponentPool();

        BuildGrid(canvas, opponents);
    }

    // Sorteia até `needed` personagens de raridade Normal (2026-07-16, pedido do usuário — "só os
    // personagens com raridade normal aparecerem como bots") entre `characterDatabase.
    // unlockedCharacters` e gera um PlayerProfile runtime (stats/skills/armas escalados pro
    // `level`) pra cada um via BotProfileGenerator — nunca persiste, gerado de novo a cada visita
    // a esta tela. Substitui a curadoria fixa por nome de `characterDatabase.botTemplates`
    // (`BotTemplateSetup.cs`) — essa lista existia só porque nenhum PlayerProfile tinha `rarity`
    // != Normal até agora (ver comentário em BotTemplateSetup.cs); com as 72 raridades já
    // preenchidas, filtrar pelo enum de verdade é mais correto e não depende mais de rodar
    // Tools > AutoArms > Setup Bot Templates a cada personagem novo.
    private List<PlayerProfile> GenerateBotOpponents(int needed, int level)
    {
        var result = new List<PlayerProfile>();
        if (characterDatabase == null || characterDatabase.unlockedCharacters == null) return result;

        var pool = new List<PlayerProfile>();
        foreach (var p in characterDatabase.unlockedCharacters)
            if (p != null && p.rarity == CharacterRarity.Normal) pool.Add(p);

        int count = Mathf.Min(needed, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);
            var template = pool[index];
            pool.RemoveAt(index);
            result.Add(BotProfileGenerator.Generate(template, level, skillDatabase, allWeapons, petPool));
        }
        return result;
    }

    // Pool local de sempre (pré-Fatia 6) — fallback de ÚLTIMA instância, só alcançado se nem a
    // busca online nem os bots (item acima) renderem nenhum oponente. Comportamento idêntico ao
    // de antes desta fatia.
    private List<PlayerProfile> GetLocalOpponentPool()
    {
        var result = new List<PlayerProfile>();
        if (characterDatabase == null) return result;

        var currentPlayer = selectedProfileHolder != null ? selectedProfileHolder.currentProfile : null;

        foreach (var profile in characterDatabase.opponentCharacters)
        {
            if (profile == null || profile == currentPlayer) continue;
            result.Add(profile);
            if (result.Count >= MaxOpponents) break;
        }
        return result;
    }

    private void OnOpponentChosen(PlayerProfile profile)
    {
        if (selectedOpponentHolder == null || profile == null) return;
        selectedOpponentHolder.currentOpponentProfile = profile;
        SceneManager.LoadScene(CombatSceneName);
    }

    // ── UI construction ────────────────────────────────────────────────────

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static GameObject CreateCanvas()
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
        bg.AddComponent<Image>().color = PanelBg;

        return go;
    }

    private static void BuildTitle(GameObject canvas)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta = new Vector2(0f, 80f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "ESCOLHA SEU OPONENTE";
        tmp.fontSize = 42;
        tmp.color = Gold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
    }

    private void BuildBackButton(GameObject canvas)
    {
        var go = new GameObject("BtnVoltar");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -40f);
        rt.sizeDelta = new Vector2(160f, 60f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.30f, 0.10f, 0.10f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => SceneManager.LoadScene(MainMenuSceneName));

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "Voltar";
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
    }

    private void BuildGrid(GameObject canvas, List<PlayerProfile> opponents)
    {
        var scrollGo = new GameObject("ScrollView");
        scrollGo.transform.SetParent(canvas.transform, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.5f, 0.5f);
        scrollRt.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRt.pivot     = new Vector2(0.5f, 0.5f);
        scrollRt.sizeDelta = new Vector2(1700f, 860f);
        scrollRt.anchoredPosition = new Vector2(0f, -30f);
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var viewportRt = viewport.AddComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = viewportRt.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = viewportRt;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot     = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(CardWidth, CardHeight);
        grid.spacing         = new Vector2(40f, 40f);
        grid.childAlignment  = TextAnchor.UpperCenter;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = GridColumns;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRt;

        foreach (var profile in opponents)
            BuildCard(content, profile);
    }

    private void BuildCard(GameObject parent, PlayerProfile profile)
    {
        var card = new GameObject($"Card_{profile.profileName}");
        card.transform.SetParent(parent.transform, false);
        var rt = card.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(CardWidth, CardHeight);
        var cardImg = card.AddComponent<Image>();
        cardImg.color = CardBg;

        // Card inteiro clicável (2026-07-16, substitui o botão "Escolher" removido) — escurece no
        // toque (OnPointerDown) e escolhe no clique de verdade (OnPointerClick — não
        // OnPointerUp, que dispararia mesmo depois de um arraste pra rolar o ScrollRect; ver
        // PressableCard.cs).
        var pressable = card.AddComponent<PressableCard>();
        pressable.targetImage = cardImg;
        pressable.normalColor = CardBg;
        pressable.pressedColor = CardBgPressed;
        pressable.onChosen = () => OnOpponentChosen(profile);

        // Retrato grande na coluna ESQUERDA — fixo no canto superior esquerdo do card; todo o
        // resto do conteúdo vive numa coluna direita própria (`RightPanel` abaixo), lado a lado.
        var portraitGo = new GameObject("Portrait");
        portraitGo.transform.SetParent(card.transform, false);
        var portraitRt = portraitGo.AddComponent<RectTransform>();
        portraitRt.anchorMin = portraitRt.anchorMax = portraitRt.pivot = new Vector2(0f, 1f);
        portraitRt.sizeDelta = new Vector2(PortraitSize, PortraitSize);
        portraitRt.anchoredPosition = new Vector2(CardPadding, -CardPadding);
        var portraitImg = portraitGo.AddComponent<Image>();
        if (profile.previewIcon != null)
        {
            portraitImg.sprite = profile.previewIcon;
            portraitImg.preserveAspect = true;
        }
        else
        {
            // Sem previewIcon configurado no profile — placeholder sólido em vez de deixar um
            // buraco invisível sem nenhum feedback visual.
            portraitImg.color = new Color(0.25f, 0.20f, 0.15f, 1f);
        }

        // Coluna direita — mesmo padrão de empilhamento de sempre (pivot no topo (0.5,1) dentro
        // deste container, "y" negativo descendo), só que agora dentro de um container próprio
        // mais estreito (`RightPanelWidth`) em vez do card inteiro.
        var rightPanelGo = new GameObject("RightPanel");
        rightPanelGo.transform.SetParent(card.transform, false);
        var rightPanelRt = rightPanelGo.AddComponent<RectTransform>();
        rightPanelRt.anchorMin = rightPanelRt.anchorMax = rightPanelRt.pivot = new Vector2(0f, 1f);
        rightPanelRt.sizeDelta = new Vector2(RightPanelWidth, CardHeight - CardPadding * 2f);
        rightPanelRt.anchoredPosition = new Vector2(CardPadding * 2f + PortraitSize, -CardPadding);

        var y = -2f;

        var nameTxt = MakeLabel(rightPanelGo, profile.profileName, 22, Gold, y, new Vector2(RightPanelWidth, 26f), bold: true);
        nameTxt.alignment = TextAlignmentOptions.Center;
        y -= 26f;

        // "Level X" sozinho, flush à esquerda (2026-07-16) — ancorado no canto superior esquerdo
        // do RightPanel (não centralizado como MakeLabel faria) pra começar colado na borda
        // direita do retrato.
        var eff = profile.GetEffectiveStats();

        var levelGo = new GameObject("Level");
        levelGo.transform.SetParent(rightPanelGo.transform, false);
        var levelRt = levelGo.AddComponent<RectTransform>();
        levelRt.anchorMin = levelRt.anchorMax = levelRt.pivot = new Vector2(0f, 1f);
        levelRt.sizeDelta = new Vector2(150f, 22f);
        levelRt.anchoredPosition = new Vector2(0f, y);
        var levelTxt = levelGo.AddComponent<TextMeshProUGUI>();
        levelTxt.text = $"Level {profile.level}";
        levelTxt.fontSize = 15;
        levelTxt.color = new Color(0.85f, 0.85f, 0.85f);
        levelTxt.alignment = TextAlignmentOptions.MidlineLeft;
        y -= 22f;

        // HP (coração + número branco centralizado, ver AttributePipBar.BuildIconWithValue)
        // alinhado em X com o ícone de STR logo abaixo (2026-07-16, pedido do usuário — "alinhe
        // o hp com o str verticalmente, hp em cima os outros 3 logo abaixo").
        // **Bug real corrigido (2026-07-17)**: a 1ª tentativa (2026-07-16) só igualava a borda
        // ESQUERDA dos dois ícones (mesmo `anchoredPosition.x = 14`), mas usava uma largura FIXA
        // em pixels (22px) enquanto o ícone de STR (`AttributePipBar.Build`'s `lblGo`, dentro de
        // `MakeAttributeRow`) usa uma largura PROPORCIONAL (20% de `RightPanelWidth`, menos os
        // mesmos 14px de inset) — como `preserveAspect`+`localScale` centralizam e escalam o
        // sprite em torno do CENTRO do próprio rect, bordas esquerdas iguais com larguras
        // diferentes produzem CENTROS renderizados diferentes (visível a olho, mesmo com a borda
        // "alinhada" no código). Fix: `sizeDelta.x` calculado com a MESMA fórmula de `lblGo`
        // (`0.20 × RightPanelWidth − 14`) em vez do valor fixo 22 — mesma largura exata do ícone
        // de STR, logo mesmo centro renderizado.
        var hpGo = new GameObject("Hp");
        hpGo.transform.SetParent(rightPanelGo.transform, false);
        var hpRt = hpGo.AddComponent<RectTransform>();
        hpRt.anchorMin = hpRt.anchorMax = hpRt.pivot = new Vector2(0f, 1f);
        hpRt.sizeDelta = new Vector2(RightPanelWidth * 0.20f - 14f, 22f);
        hpRt.anchoredPosition = new Vector2(14f, y);
        AttributePipBar.BuildIconWithValue(hpGo, AttributePipBar.HpIcon, 13f).text = eff.hp.ToString();
        y -= 34f;

        // 3 linhas empilhadas (2026-07-16, voltou — a versão em 3 colunas lado a lado espremia
        // demais o AttributePipBar de cada stat num container estreito, fazendo label/badge/pips
        // renderizarem em posições relativas diferentes entre STR/AGI/SPD; empilhado, as 3 usam
        // exatamente o mesmo container (RightPanelWidth) e o mesmo código, então ficam idênticas
        // em X por construção — só a distância vertical entre elas precisa ser consistente).
        y = MakeAttributeRow(rightPanelGo, "STR", eff.str, y);
        y = MakeAttributeRow(rightPanelGo, "AGI", eff.agility, y);
        y = MakeAttributeRow(rightPanelGo, "SPD", eff.speed, y);
        y -= 8f;

        MakeItemIconsRow(rightPanelGo, profile, y);
    }

    // Badge circular + 10 pips coloridos por tier — mesmo componente usado em CharacterPanel
    // (01_MainMenu), pedido do usuário pra STR/AGI/SPD ficarem visualmente consistentes entre as
    // duas telas em vez da fill-bar simples que existia antes aqui.
    private const float AttributeRowHeight = 38f;

    // Uma linha por stat, empilhadas com o MESMO espaçamento entre elas (2026-07-16) — todas as
    // 3 chamadas usam o mesmo container (RightPanelWidth) e o mesmo `AttributePipBar.Build`, então
    // label/badge/pips caem exatamente na mesma posição X em STR/AGI/SPD por construção; só o "y"
    // muda entre uma linha e a próxima, sempre pelo mesmo delta (`AttributeRowHeight + 6`).
    private float MakeAttributeRow(GameObject card, string label, int value, float y)
    {
        var rowGo = new GameObject(label + "Row");
        rowGo.transform.SetParent(card.transform, false);
        var rt = rowGo.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(RightPanelWidth, AttributeRowHeight);
        rt.anchoredPosition = new Vector2(0f, y);

        var pipBar = AttributePipBar.Build(rowGo, theme, label);
        pipBar.SetValue(value);

        return y - AttributeRowHeight - 6f;
    }

    // Skills, armas E pets equipados numa única linha combinada (2026-07-16 — antes eram 2 linhas
    // separadas, sem espaço vertical pra uma 3ª só pra pets no card mais baixo; a linha usa a
    // largura de sobra do card mais largo em vez de crescer verticalmente). Ícone do pet vem de
    // `PetData.icon` (ver abaixo). ResolveSkillIcon/ResolveWeaponIcon sobem a cadeia `previousTier`
    // até achar sprite (T2/T3
    // nunca têm ícone próprio). Cada ícone abre o popup de detalhe (nome/tier/descrição ou stats
    // completos, ver EnsureDetailPanel acima) no clique — mesmo padrão de 01_MainMenu/03_Arsenal.
    private float MakeItemIconsRow(GameObject card, PlayerProfile profile, float y)
    {
        var row = new GameObject("ItemIcons");
        row.transform.SetParent(card.transform, false);
        var rt = row.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(RightPanelWidth, IconRowHeight);
        rt.anchoredPosition = new Vector2(0f, y);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

        if (profile.skills != null)
            foreach (var skill in profile.skills)
            {
                if (skill == null) continue;
                var sprite = LevelUpEngine.ResolveSkillIcon(skill);
                if (sprite == null) continue;
                AddIcon(row, skill.skillName ?? "Skill", sprite,
                    () => { EnsureDetailPanel(); _detailPanel.ShowSkillDetail(skill); });
            }

        if (profile.weapons != null)
            foreach (var weapon in profile.weapons)
            {
                if (weapon == null) continue;
                var sprite = LevelUpEngine.ResolveWeaponIcon(weapon);
                if (sprite == null) continue;
                AddIcon(row, weapon.weaponName ?? "Weapon", sprite,
                    () => { EnsureDetailPanel(); _detailPanel.ShowWeaponDetail(weapon); });
            }

        // Ícone vem direto de PetData.icon agora (2026-07-16 — antes eram 3 campos fixos por
        // tipo, wireados manualmente no Inspector; PetData já carrega o ícone "de skill" próprio
        // desde a sub-fase de tiers, mesmo padrão de SkillData.icon/WeaponData.icon).
        if (profile.pets != null)
            foreach (var petData in profile.pets)
            {
                if (petData == null || petData.icon == null) continue;
                AddIcon(row, petData.petType.ToString(), petData.icon,
                    () => { EnsureDetailPanel(); _detailPanel.ShowPetDetail(petData, petData.icon); });
            }

        return y - IconRowHeight;
    }

    // Botão próprio no ícone (não bubbling até o PressableCard do card) — clicar num ícone abre
    // o popup de detalhe em vez de escolher o oponente; clicar em qualquer outro ponto do card
    // (sem Button próprio) ainda borbulha até o PressableCard normalmente.
    private void AddIcon(GameObject row, string goName, Sprite sprite, System.Action onClick)
    {
        var iconGo = new GameObject(goName);
        iconGo.transform.SetParent(row.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.sizeDelta = new Vector2(IconSize, IconSize);
        var img = iconGo.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;

        if (onClick == null) return;
        var btn = iconGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());
    }

    // pivot no topo (0.5,1) — "y" é a distância a partir do topo do parent, xOffset desloca do
    // centro horizontal (ex: labels das barras de stat, à esquerda da barra em si).
    private static TextMeshProUGUI MakeLabel(GameObject parent, string text, float fontSize,
        Color color, float y, Vector2 size, bool bold = false, float xOffset = 0f)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(xOffset, y);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        return tmp;
    }
}
