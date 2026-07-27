using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

public class CombatResultPanel : MonoBehaviour
{
    public void Show(bool player1Won, int xpGained, int xpBefore, int levelBefore,
                     PlayerProfile profile, bool didLevelUp,
                     SkillDatabase skillDatabase = null, WeaponData[] allWeapons = null,
                     PetData[] petPool = null, UITheme theme = null)
    {
        StartCoroutine(ShowRoutine(player1Won, xpGained, xpBefore, levelBefore,
                                   profile, didLevelUp, skillDatabase, allWeapons, petPool, theme));
    }

    private IEnumerator ShowRoutine(bool player1Won, int xpGained, int xpBefore, int levelBefore,
                                    PlayerProfile profile, bool didLevelUp,
                                    SkillDatabase skillDatabase, WeaponData[] allWeapons, PetData[] petPool,
                                    UITheme theme)
    {
        yield return new WaitForSeconds(0.8f);

        EnsureEventSystem();

        Canvas canvas = FindScreenCanvas();
        if (canvas == null) yield break;

        MakeOverlay(canvas.transform);

        var panel = MakePanel(canvas.transform);

        // Title
        string titleText  = player1Won ? "VITÓRIA!" : "DERROTA!";
        Color  titleColor = player1Won ? new Color(1f, 0.84f, 0f) : new Color(0.9f, 0.15f, 0.15f);
        MakeLabel(panel, titleText, 52, titleColor, new Vector2(0, 155f), new Vector2(420f, 65f), bold: true);

        // XP gained
        MakeLabel(panel, $"+{xpGained} XP", 36, new Color(0.45f, 1f, 0.45f),
            new Vector2(0, 90f), new Vector2(280f, 46f));

        // XP progress bar
        int xpRequiredBefore = XpSystem.XpRequired(levelBefore);
        int xpRequiredAfter  = XpSystem.XpRequired(profile.level);
        var barFill = MakeXpBar(panel, new Vector2(0f, 45f), new Vector2(380f, 22f));

        // XP label
        string initialXpText = didLevelUp
            ? $"{xpRequiredBefore} / {xpRequiredBefore} XP"
            : $"{profile.xpCurrent} / {xpRequiredBefore} XP";
        var xpLabel = MakeLabel(panel, initialXpText, 21, Color.white,
            new Vector2(0f, 12f), new Vector2(320f, 28f));

        MakeLabel(panel, $"Level {profile.level}", 26, new Color(0.82f, 0.82f, 0.82f),
            new Vector2(0f, -26f), new Vector2(280f, 34f));

        MakeLabel(panel, $"Batalhas restantes: {profile.battlesRemaining} / 6", 20,
            new Color(0.65f, 0.65f, 0.65f),
            new Vector2(0f, -66f), new Vector2(380f, 30f));

        // Level-up text (hidden until needed)
        var levelUpLabel = MakeLabel(panel, $"LEVEL UP!  →  Level {profile.level}", 30,
            new Color(1f, 0.84f, 0f),
            new Vector2(0f, -110f), new Vector2(420f, 40f), bold: true);
        levelUpLabel.gameObject.SetActive(false);

        // Continue button — disabled until choice is made when leveling up
        bool choiceDone = false;
        // Assíncrono (2026-07-14) — mesmo ajuste feito em ArsenalController.OnBackClicked:
        // LoadSceneAsync evita bloquear a thread principal num frame só, sem custo/risco. Não
        // resolve sozinho um eventual atraso maior (ver investigação de 03_Arsenal no
        // CHANGELOG — naquele caso era overhead específico do Editor/Play Mode, não do jogo em
        // build real), mas é estritamente melhor que o LoadScene síncrono de qualquer forma.
        var continueBtn = MakeButton(panel, "Continuar", new Vector2(0f, -172f),
            () => StartCoroutine(LoadMainMenuAsync()));
        continueBtn.interactable = !didLevelUp;

        // Animate XP bar
        float startFill  = xpRequiredBefore > 0 ? (float)xpBefore / xpRequiredBefore : 0f;
        float targetFill = didLevelUp ? 1f
            : (xpRequiredBefore > 0 ? (float)profile.xpCurrent / xpRequiredBefore : 0f);

        yield return AnimateBar(barFill, startFill, targetFill, 0.75f);

        if (didLevelUp)
        {
            yield return new WaitForSeconds(0.15f);
            levelUpLabel.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                float s = 1f + Mathf.Sin(elapsed / 0.5f * Mathf.PI) * 0.28f;
                levelUpLabel.transform.localScale = Vector3.one * s;
                elapsed += Time.deltaTime;
                yield return null;
            }
            levelUpLabel.transform.localScale = Vector3.one;

            barFill.fillAmount = xpRequiredAfter > 0 ? (float)profile.xpCurrent / xpRequiredAfter : 0f;
            xpLabel.text = $"{profile.xpCurrent} / {xpRequiredAfter} XP";

            // Pedido do usuário (2026-07-21): ao escolher o bônus, voltar direto pro menu
            // principal em vez de exigir um clique extra em "Continuar" — mesma coroutine
            // assíncrona do próprio botão "Continuar" (LoadMainMenuAsync).
            ShowLevelUpChoice(canvas.transform, profile, skillDatabase, allWeapons, petPool, theme, () => {
                choiceDone = true;
                StartCoroutine(LoadMainMenuAsync());
            });

            yield return new WaitUntil(() => choiceDone);
        }
    }

    // ── Level-Up Choice Panel ────────────────────────────────────────────────
    // LevelUpOption (struct) e a matemática de sorteio/aplicação/elegibilidade (DrawOption/
    // SameOption/ApplyBonus/filtros de skill-arma) moraram aqui antes — extraídas pra
    // LevelUpEngine (Assets/Scripts/Utils/) pra serem reaproveitadas por BotProfileGenerator, sem
    // duplicar os pesos/regras de tier-chain em dois lugares. Este arquivo continua dono da UI e
    // da persistência (ApplyBonus abaixo).

    private static void ApplyBonus(LevelUpOption opt, PlayerProfile profile)
    {
        LevelUpEngine.ApplyOption(opt, profile);

        // Resiliência (2026-07-25, bug real corrigido) — limpa a escolha pendente só AGORA, que
        // o jogador de fato escolheu; ver PlayerProfile.hasPendingLevelUpChoice/
        // AttackSequencer.OnCombatEnd/ResumePendingLevelUpChoiceIfAny abaixo.
        profile.hasPendingLevelUpChoice = false;
        profile.pendingLevelUpBoxes.Clear();
        profile.pendingLevelUpRerollsUsed = 0;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(profile);
#endif
        // Save real (2026-07-14) — ver LocalSaveService.cs. Este é o único dos 6 pontos que pode
        // mudar profile.weapons/skills (Kind.Weapon/Kind.Skill acima), então também é o único
        // onde a lista de armas/skills salva localmente pode de fato divergir da anterior.
        LocalSaveService.Save(profile);
    }

    // Converte um LevelUpOption (sorteado agora, ou já resolvido de volta de um rascunho salvo)
    // pro shape serializável persistido em PlayerProfile.pendingLevelUpBoxes — mesmo espírito de
    // CharacterUnlockEngine.ToServerShape, mas cobrindo também Kind.Attribute (que os unlocks do
    // case opening nunca produzem).
    private static PendingLevelUpBoxRef ToPendingBoxRef(LevelUpOption opt)
    {
        switch (opt.kind)
        {
            case LevelUpOption.Kind.Attribute:
                return new PendingLevelUpBoxRef { kind = "attribute", attrIndex = opt.attrIndex };
            case LevelUpOption.Kind.Skill:
                return new PendingLevelUpBoxRef
                {
                    kind = "skill",
                    name = opt.skill != null ? opt.skill.skillName : "",
                    tier = opt.skill != null ? opt.skill.tier : 1,
                };
            case LevelUpOption.Kind.Weapon:
                return new PendingLevelUpBoxRef
                {
                    kind = "weapon",
                    name = opt.weapon != null ? WeaponNameUtil.StripWeaponTierSuffix(opt.weapon.weaponName) : "",
                    tier = opt.weapon != null ? opt.weapon.tier : 1,
                };
            case LevelUpOption.Kind.Pet:
                return new PendingLevelUpBoxRef
                {
                    kind = "pet",
                    name = opt.petData != null ? opt.petData.petType.ToString() : "",
                    tier = opt.petData != null ? opt.petData.tier : 1,
                };
            default:
                return new PendingLevelUpBoxRef { kind = "" };
        }
    }

    // Caminho inverso — resolve um PendingLevelUpBoxRef salvo de volta pro LevelUpOption real
    // (com referência de asset), usando os mesmos databases/pool já carregados por quem chama
    // (ShowLevelUpChoice em fluxo normal, ou ResumePendingLevelUpChoiceIfAny ao retomar).
    private static LevelUpOption? ResolvePendingBox(PendingLevelUpBoxRef box, SkillDatabase skillDb,
        WeaponData[] allWeaponsPool, PetData[] petPool)
    {
        if (box == null) return null;
        switch (box.kind)
        {
            case "attribute":
                return new LevelUpOption { kind = LevelUpOption.Kind.Attribute, attrIndex = box.attrIndex };
            case "skill":
            {
                var skill = skillDb != null ? skillDb.FindByFamilyNameAndTier(box.name, box.tier) : null;
                return skill != null ? new LevelUpOption { kind = LevelUpOption.Kind.Skill, skill = skill } : (LevelUpOption?)null;
            }
            case "weapon":
            {
                WeaponData found = null;
                if (allWeaponsPool != null)
                    foreach (var w in allWeaponsPool)
                        if (w != null && w.tier == box.tier && WeaponNameUtil.StripWeaponTierSuffix(w.weaponName) == box.name) { found = w; break; }
                return found != null ? new LevelUpOption { kind = LevelUpOption.Kind.Weapon, weapon = found } : (LevelUpOption?)null;
            }
            case "pet":
            {
                PetData found = null;
                if (petPool != null)
                    foreach (var p in petPool)
                        if (p != null && p.tier == box.tier && p.petType.ToString() == box.name) { found = p; break; }
                return found != null ? new LevelUpOption { kind = LevelUpOption.Kind.Pet, petData = found } : (LevelUpOption?)null;
            }
            default:
                return null;
        }
    }

    // Persiste o rascunho ATUAL das N caixas (chamado logo após sortear ou resortear via "Novo
    // Sorteio", ANTES de mostrar/atualizar os cards) — sobrevive a fechar o app antes do jogador
    // escolher. Não chamado ao reconstruir a partir de um rascunho JÁ salvo (ver
    // ResumePendingLevelUpChoiceIfAny) — nesse caso não há nada novo pra persistir.
    private static void PersistPendingLevelUpBoxes(PlayerProfile profile, List<LevelUpOption> options, int rerollsUsed)
    {
        profile.hasPendingLevelUpChoice = true;
        profile.pendingLevelUpBoxes = new List<PendingLevelUpBoxRef>();
        foreach (var opt in options)
            profile.pendingLevelUpBoxes.Add(ToPendingBoxRef(opt));
        profile.pendingLevelUpRerollsUsed = rerollsUsed;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(profile);
#endif
        LocalSaveService.Save(profile);
    }

    // TESTE: mostra todas as skills/armas/atributos disponíveis em vez de sortear as caixas reais.
    // Desligada de novo (2026-07-25, pedido do usuário) — precisava estar `false` pra testar a
    // resiliência do level-up a fechamento abrupto do app (item 3 do plano de teste: progressão
    // de custo do "Novo Sorteio" sobrevivendo a fechar/reabrir), que só existe na tela real em
    // pirâmide (ShowLevelUpChoice), não nesta grade de teste. Religar (`true`) se precisar
    // testar skill nova sem ícone/tier de novo (ver histórico acima).
    private const bool ShowAllOptionsForTesting = false;

    // resumeOptions/resumeRerollsUsed (2026-07-25, bug real corrigido) — preenchidos só por
    // ResumePendingLevelUpChoiceIfAny, quando o app fechou com esta mesma tela aberta antes do
    // jogador escolher: reconstrói as MESMAS N caixas (resolvidas de volta de
    // PlayerProfile.pendingLevelUpBoxes) em vez de sortear de novo, e continua a progressão real
    // de custo do "Novo Sorteio" (não reinicia pra 50 diamantes de novo). null/0 em todo o resto
    // do jogo (fluxo normal pós-combate) — comportamento inalterado nesse caminho.
    private static void ShowLevelUpChoice(Transform canvasRoot, PlayerProfile profile,
        SkillDatabase skillDb, WeaponData[] allWeaponsPool, PetData[] petPool, UITheme theme, System.Action onChosen,
        List<LevelUpOption> resumeOptions = null, int resumeRerollsUsed = 0)
    {
        // Build available option pools
        if (skillDb == null || skillDb.skills == null || skillDb.skills.Count == 0)
            Debug.LogError("[LevelUp] ERRO: SkillDatabase não encontrado ou vazio");

        // requireIcon:false (2026-07-21, pedido do usuário — "libere todas as skills pra eu
        // testar") — derruba temporariamente o gate de ícone (skill só aparecia se a raiz T1 já
        // tivesse um ícone re-adicionado em Assets/Data/UI/Skills/, ver CLAUDE.md); skill sem
        // ícone mostra card com cor placeholder + nome (MakeLevelUpCard já trata `icon == null`
        // sem erro, mesmo fallback que os bots usam com requireIcon:false). Voltar pra `true`
        // junto de ShowAllOptionsForTesting=false quando terminar de testar.
        var availableSkills  = LevelUpEngine.BuildAvailableSkills(profile, skillDb?.skills, requireIcon: false);
        var availableWeapons = LevelUpEngine.BuildAvailableWeapons(profile, allWeaponsPool);
        // Filtro de elegibilidade (2026-07-17): T1 só se não possui nenhum tier deste pet; T2/T3
        // só o `nextTier` do que já possui — mesmo padrão de BuildAvailableSkills/Weapons.
        var availablePets = LevelUpEngine.BuildAvailablePets(profile, petPool != null ? new List<PetData>(petPool) : null);

        if (ShowAllOptionsForTesting)
        {
            ShowAllOptionsChoice(canvasRoot, profile, availableSkills, availableWeapons, availablePets, theme, onChosen);
            return;
        }

        // N = PlayerProgressionState.LevelUpBoxCount() (2 base + 1 por Slot de Skill comprado na
        // Loja, até 5). Caixa 1 sempre status base (HP/STR/AGI/SPD); Caixas 2..N sorteio ponderado
        // — ver DrawAndBuildCards (local, abaixo) pra regra completa de sorteio/no-repeat.
        // Retomando (resumeOptions != null): usa a contagem REAL salva, não recalcula — evita um
        // mismatch se PlayerProgressionState mudou entre fechar e reabrir (ex: comprou um slot
        // novo nesse meio-tempo).
        int boxCount = resumeOptions != null
            ? resumeOptions.Count
            : Mathf.Clamp(PlayerProgressionState.LevelUpBoxCount(), 2, 5);

        // Root container covers the whole canvas (renders above result panel as last sibling)
        var root = new GameObject("LevelUpChoiceRoot");
        root.transform.SetParent(canvasRoot, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        // Blocking overlay so result panel buttons can't be clicked. Fundo preto sólido (pedido do
        // usuário, 2026-07-21 — era 55% translúcido) — fica atrás tanto das caixas de escolha
        // (mesmo canvas, `bg`/ChoicePanel criado depois, sibling na frente) quanto do painel de
        // detalhamento (canvas PRÓPRIO/raiz, sortingOrder=1500, sempre acima de QUALQUER canvas
        // deste, incluindo este overlay — ver comentário em detailPanelGo abaixo), então um único
        // overlay cobre os dois sem precisar duplicar o backdrop em cada canvas.
        var ov = new GameObject("Overlay");
        ov.transform.SetParent(root.transform, false);
        var ovRt = ov.AddComponent<RectTransform>();
        ovRt.anchorMin = Vector2.zero;
        ovRt.anchorMax = Vector2.one;
        ovRt.offsetMin = ovRt.offsetMax = Vector2.zero;
        var ovImg = ov.AddComponent<Image>();
        ovImg.color = new Color(0, 0, 0, 1f);
        ovImg.raycastTarget = true;

        // Painel de detalhamento do personagem (2026-07-21, pedido do usuário) — mesmo
        // CharacterPanel do 01_MainMenu (compacto por padrão: nome/HP/STR/AGI/SPD; clique expande
        // pra ver Habilidades/Armas/Pets equipados), mostrando o profile ATUAL (antes do bônus
        // escolhido) — ajuda a decidir comparando com o que já possui. `Setup(null, ...)` +
        // `SetProfile(profile)` porque já temos o PlayerProfile de verdade aqui, sem precisar de
        // um SelectedProfileHolder (RefreshAll usa `_overrideProfile ?? _holder?.currentProfile`,
        // então holder nulo é seguro). `theme` vem de AttackSequencer (wireado no Inspector de
        // 04_CombatScenePVP) — nulo é seguro, só pula a construção deste painel.
        //
        // Bug real corrigido (2026-07-21, 2ª rodada — reportado pelo usuário: painel ainda não
        // aparecia e o popup ainda ficava atrás mesmo depois de forçar sortingOrder=1500): a 1ª
        // tentativa parentava `detailPanelGo` dentro de `root` (que já é filho do Canvas de
        // `canvasRoot`) — isso faz o Canvas PRÓPRIO do CharacterPanel virar um CANVAS AGRUPADO
        // (nested), e um canvas aninhado só reordena entre IRMÃOS dentro do mesmo canvas pai;
        // `sortingOrder` alto não adianta contra canvases-RAIZ concorrentes (HealthBar/
        // HealthBarPet = 100). Todo outro lugar que usa CharacterPanel (ArsenalController.
        // EnsureDetailPanel, SelectOpponentController) instancia ele SEM PAI NENHUM — GameObject
        // raiz de cena própria, canvas raiz de verdade, sortingOrder funciona contra qualquer
        // outro canvas raiz do jogo. Corrigido replicando esse padrão: `detailPanelGo` não é mais
        // filho de `root` — como não é destruído junto (`Object.Destroy(root)` não alcança mais
        // ele), cada callback de escolha abaixo destrói os dois explicitamente.
        CharacterPanel detailPanel = null;
        GameObject detailPanelGo = null;
        if (theme != null)
        {
            detailPanelGo = new GameObject("LevelUpDetailPanel");
            detailPanel = detailPanelGo.AddComponent<CharacterPanel>();
            // hideCompact:true (2026-07-21, pedido do usuário — "o primeiro painel sem expandir
            // está cobrindo uma skill") — o bloco Compact (nome/HP/pips, sempre visível por
            // padrão) tapava uma das caixas de escolha. Com hideCompact, nada do CharacterPanel
            // aparece até o botão quadrado próprio abaixo chamar Expand() — ver comentário em
            // CharacterPanel.Setup/CrossFade.
            detailPanel.Setup(null, theme, hideCompact: true);
            detailPanel.SetProfile(profile);

            var detailCanvas = detailPanelGo.GetComponentInChildren<Canvas>();
            if (detailCanvas != null) detailCanvas.sortingOrder = 1500;

            // Painel maior (pedido do usuário, 2026-07-21) — CharacterPanel não tem parâmetro de
            // escala próprio (componente compartilhado por 01_MainMenu/02_SelectCharacter/
            // 03_Arsenal, PanelWidth/EdgeMargin são const fixos usados por várias telas) — escala
            // a RectTransform "Root" de fora, sem tocar em CharacterPanel.cs. Pivot movido pro
            // canto superior direito ANTES de escalar, com offsetMin/Max REAPLICADOS com os
            // mesmos valores que CharacterPanel.BuildUI já usa pro modo painel-lateral (só a
            // troca de pivot por si só deslocaria o retângulo, já que anchoredPosition é relativo
            // ao pivot) — assim o painel cresce PRA DENTRO da tela a partir do canto superior
            // direito (mesma posição de sempre), em vez de crescer também pra fora da tela.
            var detailRootRt = detailPanelGo.transform.Find("Canvas/Root")?.GetComponent<RectTransform>();
            if (detailRootRt != null)
            {
                const float DetailPanelScale = 1.5f;
                detailRootRt.pivot = new Vector2(1f, 1f);
                detailRootRt.offsetMin = new Vector2(-(CharacterPanel.PanelWidth + CharacterPanel.EdgeMargin), 0f);
                detailRootRt.offsetMax = new Vector2(-CharacterPanel.EdgeMargin, 0f);
                detailRootRt.localScale = Vector3.one * DetailPanelScale;
            }

            // Botão quadrado (pedido do usuário, 2026-07-21) — parentado no MESMO Canvas do
            // CharacterPanel (sibling de "Root"/popup, adicionado por último = desenha por cima
            // dos dois), não dentro de "Root" — assim continua clicável em qualquer estado
            // (fechado ou expandido), sempre no mesmo canto. Toggle simples: abre já EXPANDIDO
            // (pedido do usuário — não passa pelo Compact, que está escondido) e fecha de volta.
            if (detailCanvas != null)
            {
                bool detailExpanded = false;
                var toggleGo = new GameObject("DetailToggleButton");
                toggleGo.transform.SetParent(detailCanvas.transform, false);
                var toggleRt = toggleGo.AddComponent<RectTransform>();
                toggleRt.anchorMin = toggleRt.anchorMax = new Vector2(1f, 1f);
                toggleRt.pivot = new Vector2(1f, 1f);
                toggleRt.anchoredPosition = new Vector2(-CharacterPanel.EdgeMargin, -20f);
                toggleRt.sizeDelta = new Vector2(80f, 80f);
                var toggleImg = toggleGo.AddComponent<Image>();
                toggleImg.sprite = UIShapeUtil.RoundedRect(theme.primaryAction, 14f);
                toggleImg.type = Image.Type.Sliced;
                var toggleBtn = toggleGo.AddComponent<Button>();
                toggleBtn.targetGraphic = toggleImg;

                var toggleLabelGo = new GameObject("Label");
                toggleLabelGo.transform.SetParent(toggleGo.transform, false);
                var toggleLabelRt = toggleLabelGo.AddComponent<RectTransform>();
                toggleLabelRt.anchorMin = Vector2.zero; toggleLabelRt.anchorMax = Vector2.one;
                toggleLabelRt.offsetMin = toggleLabelRt.offsetMax = Vector2.zero;
                var toggleLabelTxt = toggleLabelGo.AddComponent<TextMeshProUGUI>();
                toggleLabelTxt.text = "i";
                toggleLabelTxt.fontSize = 34;
                toggleLabelTxt.fontStyle = FontStyles.Bold;
                toggleLabelTxt.color = theme.textOnDark;
                toggleLabelTxt.alignment = TextAlignmentOptions.Center;

                toggleBtn.onClick.AddListener(() =>
                {
                    detailExpanded = !detailExpanded;
                    if (detailExpanded) detailPanel.Expand();
                    else detailPanel.Collapse();
                });
            }
        }

        // Layout em pirâmide (pedido do usuário, 2026-07-21 — "deixar as skills em formato de
        // pirâmide, 2 em cima 3 em baixo") + cards escalados 1.5x (também pedido). Generaliza pra
        // qualquer boxCount (2 a 5): fileira de cima = metade arredondada pra baixo, fileira de
        // baixo = o resto — bate exatamente com "2 em cima 3 em baixo" pra 5 caixas (N=5 → 2/3);
        // 4 caixas → 2/2; 3 caixas → 1/2; 2 caixas → 1/1.
        const float CardScale = 1.5f;
        const float CardBaseSize = 270f; // tamanho real do card, ver MakeLevelUpCard — CardScale só escala visualmente por cima
        const float RowSpacingX = 300f * CardScale; // distância entre centros de card na MESMA fileira (era CardSlotWidth)
        const float RowGapY = 40f; // vão vertical entre a fileira de cima e a de baixo
        const float TopRowY = 200f;
        float bottomRowY = TopRowY - (CardBaseSize * CardScale + RowGapY);

        int topCount = boxCount / 2;
        int bottomCount = boxCount - topCount;
        int maxRowCount = Mathf.Max(topCount, bottomCount);

        const float PanelSidePadding = 90f;
        const float PanelHeight = 980f;
        const float TitleY = 450f;
        float panelWidth = maxRowCount * RowSpacingX + PanelSidePadding;

        var bg = new GameObject("ChoicePanel");
        bg.transform.SetParent(root.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(panelWidth, PanelHeight);
        bgRt.anchoredPosition = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.14f, 0.98f);

        MakeLabel(bg, "ESCOLHA 1 BÔNUS:", 26, new Color(1f, 0.84f, 0f),
            new Vector2(0, TitleY), new Vector2(panelWidth - 40f, 38f), bold: true);

        // Posição de cada caixa na pirâmide — índice 0..topCount-1 na fileira de cima
        // (centralizada em X, independente da fileira de baixo), o resto na fileira de baixo.
        Vector2 CardPosition(int i)
        {
            bool isTop = i < topCount;
            int rowCount = isTop ? topCount : bottomCount;
            int indexInRow = isTop ? i : i - topCount;
            float rowY = isTop ? TopRowY : bottomRowY;
            float rowFirstX = -(rowCount - 1) * RowSpacingX / 2f;
            return new Vector2(rowFirstX + indexInRow * RowSpacingX, rowY);
        }

        // Container só pras caixas (separado do título/botão de reset acima) — o botão "Novo
        // Sorteio" reconstrói só isto, sem destruir o resto do painel.
        var cardsRow = new GameObject("CardsRow");
        cardsRow.transform.SetParent(bg.transform, false);
        var cardsRowRt = cardsRow.AddComponent<RectTransform>();
        cardsRowRt.anchorMin = Vector2.zero; cardsRowRt.anchorMax = Vector2.one;
        cardsRowRt.offsetMin = cardsRowRt.offsetMax = Vector2.zero;

        // Pool de ícones da "roleta" (pedido do usuário, 2026-07-21 — "consegue fazer uma roleta
        // igual aquelas máquinas de cassino? parando da esquerda para direita") — sprites de
        // skills/armas/pets do JOGO INTEIRO (não só das opções sorteadas), pra dar variedade
        // visual ao efeito de giro; construído uma vez só, reaproveitado em todo re-sorteio.
        var spinPool = BuildSpinIconPool(skillDb, allWeaponsPool, petPool);
        const float BaseSpinDuration = 0.9f;
        const float StaggerPerBox = 0.35f; // cada caixa seguinte trava um pouco depois — efeito "parando da esquerda pra direita"

        // Contador de diamante (pedido do usuário, 2026-07-21 — "deixar o diamante na seleção de
        // skill... pra ele ver quanto tem") — canto superior direito do painel de escolha, mesmo
        // ícone/estilo do contador da Loja (ShopController.BuildDiamondCounter). Só leitura — não
        // é o mesmo contador da Loja (telas diferentes), mas lê o MESMO PlayerEconomyState.
        // Diamonds; atualizado manualmente a cada gasto de "Novo Sorteio" (ver UpdateDiamondLabel
        // abaixo), já que não há binding automático.
        var diamondIconSprite = Resources.Load<Sprite>("UI/Economy/Diamond");
        var diamondRowGo = new GameObject("DiamondCounter");
        diamondRowGo.transform.SetParent(bg.transform, false);
        var diamondRowRt = diamondRowGo.AddComponent<RectTransform>();
        diamondRowRt.anchorMin = diamondRowRt.anchorMax = new Vector2(1f, 1f);
        diamondRowRt.pivot = new Vector2(1f, 1f);
        diamondRowRt.anchoredPosition = new Vector2(-16f, -16f);
        diamondRowRt.sizeDelta = new Vector2(170f, 44f);
        // Escala 2x (pedido do usuário, 2026-07-21) — pivot já é o canto superior direito (1,1),
        // então cresce pra dentro do painel (esquerda/baixo) em vez de vazar pela borda.
        diamondRowRt.localScale = new Vector3(2f, 2f, 1f);
        var diamondRowImg = diamondRowGo.AddComponent<Image>();
        diamondRowImg.sprite = UIShapeUtil.RoundedRect(new Color(0f, 0f, 0f, 0.35f), 12f);
        diamondRowImg.type = Image.Type.Sliced;
        var diamondLayout = diamondRowGo.AddComponent<HorizontalLayoutGroup>();
        diamondLayout.padding = new RectOffset(8, 10, 4, 4);
        diamondLayout.spacing = 6f;
        diamondLayout.childAlignment = TextAnchor.MiddleLeft;
        diamondLayout.childControlWidth = true;
        diamondLayout.childControlHeight = true;
        diamondLayout.childForceExpandWidth = false;
        diamondLayout.childForceExpandHeight = true;

        var diamondIconGo = new GameObject("Icon");
        diamondIconGo.transform.SetParent(diamondRowGo.transform, false);
        diamondIconGo.AddComponent<RectTransform>();
        var diamondIconLe = diamondIconGo.AddComponent<LayoutElement>();
        diamondIconLe.preferredWidth = 32f; diamondIconLe.preferredHeight = 32f;
        var diamondIconImg = diamondIconGo.AddComponent<Image>();
        diamondIconImg.sprite = diamondIconSprite;
        diamondIconImg.preserveAspect = true;

        var diamondValueGo = new GameObject("Value");
        diamondValueGo.transform.SetParent(diamondRowGo.transform, false);
        diamondValueGo.AddComponent<RectTransform>();
        var diamondValueLe = diamondValueGo.AddComponent<LayoutElement>();
        diamondValueLe.preferredWidth = 90f; diamondValueLe.flexibleWidth = 1f;
        var diamondValueTxt = diamondValueGo.AddComponent<TextMeshProUGUI>();
        diamondValueTxt.text = PlayerEconomyState.Diamonds.ToString();
        diamondValueTxt.fontSize = 22f;
        diamondValueTxt.fontStyle = FontStyles.Bold;
        diamondValueTxt.color = Color.white;
        diamondValueTxt.alignment = TextAlignmentOptions.MidlineLeft;

        // Botão "Novo Sorteio" (pedido do usuário, 2026-07-21) — gasta diamante e refaz o sorteio
        // (incluindo a roleta de novo). Canto INFERIOR direito. Preço FIXO (2026-07-26, corrigido
        // — era progressivo 50/100/200 e gasto direto do cliente, TODO de segurança sinalizado
        // desde 2026-07-21; ver functions/src/rerollLevelUpBoxes.ts). Agora reusa o MESMO valor
        // fixo de rerollUnlock/rerollRebirthGrant (LevelUpRerollService.CostDiamonds, 15
        // diamantes), validado/debitado sempre no servidor antes de refazer o sorteio local —
        // limite de usos por level-up também passa a ser server-side
        // (LevelUpRerollService.MaxRerollsPerLevelUp).
        // Retomando (resumeRerollsUsed): continua da contagem real já usada antes de fechar o
        // app, em vez de voltar pra "3 disponíveis" de graça (ver comentário de resumeOptions
        // acima) — só é usado de fato quando não há conta sincronizada (ver handler abaixo); com
        // conta, a contagem real vem sempre do servidor (levelUpRerollCount/levelUpRerollAtLevel).
        int rerollCount = resumeRerollsUsed;
        // MakeButton sempre registra um listener que invoca `onClick()` sem checar nulo — passar
        // um no-op aqui (em vez de null) evita NullReferenceException nesse listener; o handler
        // de verdade é registrado como um 2º listener via resetBtn.onClick.AddListener abaixo.
        // Movido pra FORA do quadrante das caixas (pedido do usuário, 2026-07-21, 5ª rodada — a
        // tentativa anterior, canto inferior ESQUERDO com posX 840, caiu no meio da tela) —
        // parentado em `root` (tela inteira) em vez de `bg`/ChoicePanel, ancorado no canto
        // INFERIOR DIREITO da tela, fora da janela de escolha (que fica centralizada).
        var resetBtn = MakeButton(root, "", Vector2.zero, () => { });
        var resetBtnRt = resetBtn.GetComponent<RectTransform>();
        resetBtnRt.anchorMin = resetBtnRt.anchorMax = new Vector2(1f, 0f);
        resetBtnRt.pivot = new Vector2(1f, 0f);
        resetBtnRt.anchoredPosition = new Vector2(-32f, 40f);
        resetBtnRt.sizeDelta = new Vector2(200f, 200f);
        var resetBtnLabel = resetBtn.GetComponentInChildren<TextMeshProUGUI>();
        resetBtnLabel.fontSize = 35;

        void UpdateRerollButtonLabel()
        {
            resetBtnLabel.text = rerollCount >= LevelUpRerollService.MaxRerollsPerLevelUp
                ? "Limite de sorteios atingido"
                : $"Novo Sorteio ({LevelUpRerollService.CostDiamonds} diamantes)";
        }
        UpdateRerollButtonLabel();

        int spinsRemaining = 0;
        void OnOneCardLanded()
        {
            spinsRemaining--;
            if (spinsRemaining <= 0) resetBtn.interactable = rerollCount < LevelUpRerollService.MaxRerollsPerLevelUp;
        }

        // Consumido na 1ª chamada de DrawAndBuildCards (seja usando resumeOptions, seja sorteando
        // fresco) — toda chamada SEGUINTE (sempre vinda de "Novo Sorteio") sorteia fresco, nunca
        // mais reaproveita resumeOptions.
        bool resumeOptionsConsumed = false;

        // Sorteia e monta as N caixas — chamado na abertura da tela E a cada clique em "Novo
        // Sorteio". Caixa 1: sempre status base (HP/STR/AGI/SPD), nunca skill/arma/pet (pedido do
        // usuário). Caixas 2..N: sorteio ponderado pelos odds reais (LevelUpEngine.
        // DrawWeightedOption), SEM REPETIR skill/arma/pet já sorteado NESTA MESMA sequência
        // (pedido do usuário, 2026-07-21 — "veio dois feline agility na mesma escolha, não deve
        // se repetir na mesma sequência"; reverte a permissão de repetição da versão anterior) —
        // cada pool disponível é filtrado excluindo o que já saiu antes de cada sorteio seguinte.
        // A fatia residual do sorteio ponderado ainda cai pro mesmo pool de status base da Caixa 1
        // (nenhuma caixa fica vazia); atributos podem repetir entre caixas (não são "itens", só
        // bônus numéricos — fora do escopo do pedido).
        //
        // Resiliência (2026-07-25, bug real corrigido): logo após decidir as N caixas (sorteadas
        // OU herdadas de resumeOptions na 1ª chamada), persiste o rascunho ANTES de montar
        // qualquer card na tela — sobrevive a fechar o app nesse meio-tempo. Retomar
        // (resumeOptions != null, só na 1ª chamada) usa o mesmo resultado de antes em vez de
        // sortear de novo, então não persiste de novo (já está salvo).
        void DrawAndBuildCards()
        {
            List<LevelUpOption> options;
            if (!resumeOptionsConsumed && resumeOptions != null)
            {
                options = resumeOptions;
            }
            else
            {
                var usedSkills = new HashSet<SkillData>();
                var usedWeapons = new HashSet<WeaponData>();
                var usedPets = new HashSet<PetData>();
                options = new List<LevelUpOption> { LevelUpEngine.DrawBaseAttributeOption() };
                for (int i = 1; i < boxCount; i++)
                {
                    var skillsPool = availableSkills.FindAll(s => !usedSkills.Contains(s));
                    var weaponsPool = availableWeapons.FindAll(w => !usedWeapons.Contains(w));
                    var petsPool = availablePets.FindAll(p => !usedPets.Contains(p));
                    var opt = LevelUpEngine.DrawWeightedOption(skillsPool, weaponsPool, petsPool);
                    if (opt.kind == LevelUpOption.Kind.Skill) usedSkills.Add(opt.skill);
                    else if (opt.kind == LevelUpOption.Kind.Weapon) usedWeapons.Add(opt.weapon);
                    else if (opt.kind == LevelUpOption.Kind.Pet) usedPets.Add(opt.petData);
                    options.Add(opt);
                }
                PersistPendingLevelUpBoxes(profile, options, rerollCount);
            }
            resumeOptionsConsumed = true;

            for (int i = cardsRow.transform.childCount - 1; i >= 0; i--)
                Object.Destroy(cardsRow.transform.GetChild(i).gameObject);

            spinsRemaining = options.Count;
            resetBtn.interactable = false;

            for (int i = 0; i < options.Count; i++)
            {
                var capturedOpt = options[i];
                Vector2 cardPos = CardPosition(i);
                float spinDuration = BaseSpinDuration + i * StaggerPerBox;
                MakeLevelUpCard(cardsRow, capturedOpt, cardPos, CardScale, detailPanel,
                    spinPool, spinDuration, OnOneCardLanded,
                    () => {
                        ApplyBonus(capturedOpt, profile);
                        Object.Destroy(root);
                        // detailPanelGo não é mais filho de `root` (ver comentário acima) —
                        // precisa ser destruído à parte, senão o painel/canvas ficam vazando na
                        // cena depois da escolha.
                        if (detailPanelGo != null) Object.Destroy(detailPanelGo);
                        onChosen();
                    }, theme);
            }
        }

        resetBtn.onClick.AddListener(async () =>
        {
            if (rerollCount >= LevelUpRerollService.MaxRerollsPerLevelUp) return;

            resetBtn.interactable = false; // evita duplo clique enquanto a chamada está em andamento
            bool spent;
            if (AuthService.IsSignedIn && !string.IsNullOrEmpty(profile.characterId))
            {
                // Correção de segurança (2026-07-26) — custo FIXO, validado/debitado sempre no
                // servidor (rerollLevelUpBoxes, ver comentário acima) antes de refazer o sorteio
                // local; era WalletService.SpendDiamondsAsync (client-writable, TODO de segurança
                // sinalizado desde 2026-07-21).
                var auth = await LevelUpRerollService.SpendRerollAsync(profile.characterId);
                spent = auth.Success;
                if (spent)
                {
                    PlayerEconomyState.Diamonds = auth.RemainingDiamonds;
                    // rerollCount server-side é a fonte de verdade (levelUpRerollCount/
                    // levelUpRerollAtLevel) — deriva daqui pra manter o label/limite local em
                    // sincronia mesmo se o contador local (resumeRerollsUsed) estivesse
                    // desatualizado.
                    rerollCount = LevelUpRerollService.MaxRerollsPerLevelUp - auth.RemainingUses;
                }
            }
            else
            {
                // Sem conta sincronizada (ou personagem sem characterId real) — mesmo fallback
                // "cliente confiável" já usado em outros pontos do jogo sem conta (ver
                // PlayerEconomyState) — nada pra validar no servidor sem um characterId real.
                if (PlayerEconomyState.Diamonds < LevelUpRerollService.CostDiamonds)
                {
                    spent = false;
                }
                else
                {
                    PlayerEconomyState.Diamonds -= LevelUpRerollService.CostDiamonds;
                    rerollCount++;
                    spent = true;
                }
            }
            if (!spent)
            {
                resetBtn.interactable = rerollCount < LevelUpRerollService.MaxRerollsPerLevelUp;
                return;
            }

            diamondValueTxt.text = PlayerEconomyState.Diamonds.ToString();
            UpdateRerollButtonLabel();
            DrawAndBuildCards();
        });

        DrawAndBuildCards();
    }

    // ── Resiliência: retomar escolha de level-up pendente ───────────────────────
    // (2026-07-25, bug real corrigido — fechar o app com a tela "Escolha 1 bônus" aberta perdia
    // XP/level/battlesRemaining/bônus da luta inteira, sem chance de retomar. Ver CHANGELOG.md/
    // ARQUITETURA.md pro desenho completo.)
    //
    // Chamado por MainMenuController.Start() a cada carregamento do menu principal — se o
    // profile do jogador tem uma escolha pendente (AttackSequencer.OnCombatEnd seta
    // hasPendingLevelUpChoice=true assim que detecta level-up, ANTES de qualquer UI aparecer),
    // reabre a MESMA tela de escolha agora, fora do contexto de qualquer combate ao vivo. O
    // overlay opaco que ShowLevelUpChoice/ShowAllOptionsChoice já constroem (raycastTarget=true,
    // cobre a tela inteira) sozinho já bloqueia clique em QUALQUER botão do menu atrás dele
    // (Jogar/Chibers/Arsenal/etc.) — não precisa desabilitar cada botão individualmente.
    //
    // Não é uma instância viva de CombatResultPanel (não roda ShowRoutine, sem título de vitória/
    // derrota nem animação de barra de XP) — chama ShowLevelUpChoice diretamente (mesma classe,
    // método privado acessível), com resumeOptions/resumeRerollsUsed reconstruídos a partir de
    // PlayerProfile.pendingLevelUpBoxes/pendingLevelUpRerollsUsed.
    public static void ResumePendingLevelUpChoiceIfAny(PlayerProfile profile, UITheme theme)
    {
        if (profile == null || !profile.hasPendingLevelUpChoice) return;

        EnsureEventSystem();

        // Canvas PRÓPRIO/dedicado (2026-07-25, bug real corrigido — reportado pelo usuário: o
        // painel de detalhe do personagem aparecia aberto no centro da tela em vez do botão "i"
        // colapsado no canto superior direito) — `FindScreenCanvas()` (usado pelo fluxo normal em
        // ShowRoutine) devolve literalmente o PRIMEIRO Canvas ScreenSpaceOverlay que
        // `FindObjectsOfType` encontrar, sem ordem garantida; isso é seguro em 04_CombatScenePVP
        // (só o Canvas do CombatHUD existe), mas em 01_MainMenu (onde este método roda) já existem
        // vários Canvas ScreenSpaceOverlay concorrentes (CharacterPanel bottomAnchored,
        // CurrencyHud, Level/XP e Energia do MainMenuCharacterPreview) — reusar QUALQUER um deles
        // como pai da tela de escolha inteira (overlay+cards) fazia a ordem de desenho depender de
        // sibling index dentro de um Canvas alheio, em vez de um sortingOrder próprio e
        // determinístico. Um Canvas raiz dedicado, criado aqui, elimina essa ambiguidade — mesmo
        // padrão do `detailPanelGo` (canvas raiz próprio, sortingOrder alto) já usado logo abaixo
        // dentro de ShowLevelUpChoice.
        var resumeCanvasGo = new GameObject("LevelUpResumeCanvas");
        var canvas = resumeCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        // Bug real corrigido (2026-07-25, reportado pelo usuário — as 5 caixas de escolha
        // "estouraram" a tela) — faltava o CanvasScaler aqui. Sem ele, este Canvas cai no modo
        // default "Constant Pixel Size" (270-405px de card em pixels BRUTOS de tela, sem
        // escalar), diferente de TODO outro Canvas do projeto (sempre ScaleWithScreenSize +
        // referenceResolution 1920x1080, ver LoginController/MainMenuController/CharacterPanel/
        // etc.) — o Canvas que `FindScreenCanvas()` encontrava antes desta correção (Bug 1)
        // sempre tinha essa configuração "de graça" por ser um Canvas já existente da cena; o
        // Canvas dedicado criado aqui não herda nada automaticamente.
        var resumeScaler = resumeCanvasGo.AddComponent<CanvasScaler>();
        resumeScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        resumeScaler.referenceResolution = new Vector2(1920, 1080);
        resumeCanvasGo.AddComponent<GraphicRaycaster>();

        // Mesmos databases que AttackSequencer normalmente wireia via Inspector (04_CombatScenePVP)
        // — aqui, fora da cena de combate, resolvidos via Resources.Load (mesmo padrão de
        // PlayerProfileConverter/CharacterUnlockEngine). allWeapons/petPool precisam ser um array
        // FLAT com TODOS os tiers (T1+T2+T3), não só os T1 raiz que os databases guardam — mesmo
        // formato que AttackSequencer.allWeapons/petPool já usam (Tools > AutoArms > Assign All
        // Weapon Tiers to AttackSequencer), senão BuildAvailableWeapons/Pets nunca ofereceria T2/T3
        // pra upgrade num futuro "Novo Sorteio" depois de retomar.
        var skillDb = Resources.Load<SkillDatabase>("SkillDatabase");
        var weaponDb = Resources.Load<WeaponDatabase>("WeaponDatabase");
        var petDb = Resources.Load<PetDatabase>("PetDatabase");
        var allWeapons = FlattenAllWeaponTiers(weaponDb);
        var petPool = FlattenAllPetTiers(petDb);

        List<LevelUpOption> resumeOptions = null;
        if (profile.pendingLevelUpBoxes != null && profile.pendingLevelUpBoxes.Count > 0)
        {
            resumeOptions = new List<LevelUpOption>();
            foreach (var box in profile.pendingLevelUpBoxes)
            {
                var opt = ResolvePendingBox(box, skillDb, allWeapons, petPool);
                if (opt.HasValue) resumeOptions.Add(opt.Value);
            }
            // Dado inconsistente (ex: catálogo mudou entre fechar e reabrir) — não arrisca
            // mostrar menos caixas do que o esperado; cai pro sorteio fresco de novo dentro de
            // ShowLevelUpChoice (mesma rede de segurança usada em CharacterUnlockEngine).
            if (resumeOptions.Count != profile.pendingLevelUpBoxes.Count) resumeOptions = null;
        }

        ShowLevelUpChoice(canvas.transform, profile, skillDb, allWeapons, petPool, theme,
            // Destrói o Canvas dedicado criado acima junto da escolha (ShowLevelUpChoice só
            // destrói "root", filho dele — sem isso o GameObject do Canvas ficaria pra sempre na
            // cena, vazio, depois da escolha feita).
            onChosen: () => { if (resumeCanvasGo != null) Object.Destroy(resumeCanvasGo); },
            resumeOptions: resumeOptions, resumeRerollsUsed: profile.pendingLevelUpRerollsUsed);
    }

    private static WeaponData[] FlattenAllWeaponTiers(WeaponDatabase db)
    {
        var list = new List<WeaponData>();
        if (db?.weapons != null)
            foreach (var root in db.weapons)
            {
                var w = root;
                while (w != null) { list.Add(w); w = w.nextTier; }
            }
        return list.ToArray();
    }

    private static PetData[] FlattenAllPetTiers(PetDatabase db)
    {
        var list = new List<PetData>();
        if (db?.pets != null)
            foreach (var root in db.pets)
            {
                var p = root;
                while (p != null) { list.Add(p); p = p.nextTier; }
            }
        return list.ToArray();
    }

    // Sprites de TODAS as skills/armas/pets do jogo (não só das opções sorteadas) — usado só pra
    // variedade visual do efeito de "roleta" (LevelUpReelSpinner), nunca influencia o sorteio em
    // si (que já aconteceu antes, em DrawAndBuildCards).
    private static List<Sprite> BuildSpinIconPool(SkillDatabase skillDb, WeaponData[] allWeaponsPool, PetData[] petPool)
    {
        var pool = new List<Sprite>();
        if (skillDb?.skills != null)
            foreach (var s in skillDb.skills)
            {
                var spr = LevelUpEngine.ResolveSkillIcon(s);
                if (spr != null) pool.Add(spr);
            }
        if (allWeaponsPool != null)
            foreach (var w in allWeaponsPool)
            {
                var spr = LevelUpEngine.ResolveWeaponIcon(w);
                if (spr != null) pool.Add(spr);
            }
        if (petPool != null)
            foreach (var p in petPool)
                if (p?.icon != null) pool.Add(p.icon);
        return pool;
    }

    // TESTE: grade rolável com as opções disponíveis (4 atributos + skills com ícone já
    // re-adicionado em Assets/Data/UI/Skills/) — sem sorteio, escolhe livremente qualquer uma.
    // Armas removidas temporariamente da lista enquanto o teste foca em testar as skills uma
    // a uma (availableWeapons mantido como parâmetro, sem uso, pra reativar depois bastando
    // descomentar o foreach abaixo).
    private static void ShowAllOptionsChoice(Transform canvasRoot, PlayerProfile profile,
        List<SkillData> availableSkills, List<WeaponData> availableWeapons, List<PetData> availablePets,
        UITheme theme, System.Action onChosen)
    {
        var allOptions = new List<LevelUpOption>();
        for (int i = 0; i < 10; i++)
            allOptions.Add(new LevelUpOption { kind = LevelUpOption.Kind.Attribute, attrIndex = i });
        foreach (var s in availableSkills)
            allOptions.Add(new LevelUpOption { kind = LevelUpOption.Kind.Skill, skill = s });
        foreach (var w in availableWeapons)
            allOptions.Add(new LevelUpOption { kind = LevelUpOption.Kind.Weapon, weapon = w });
        foreach (var p in availablePets)
            allOptions.Add(new LevelUpOption { kind = LevelUpOption.Kind.Pet, petData = p });

        var root = new GameObject("LevelUpChoiceRoot");
        root.transform.SetParent(canvasRoot, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        var ov = new GameObject("Overlay");
        ov.transform.SetParent(root.transform, false);
        var ovRt = ov.AddComponent<RectTransform>();
        ovRt.anchorMin = Vector2.zero;
        ovRt.anchorMax = Vector2.one;
        ovRt.offsetMin = ovRt.offsetMax = Vector2.zero;
        var ovImg = ov.AddComponent<Image>();
        ovImg.color = new Color(0, 0, 0, 0.75f);
        ovImg.raycastTarget = true;

        var bg = new GameObject("ChoicePanelAll");
        bg.transform.SetParent(root.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(1100f, 700f);
        bgRt.anchoredPosition = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.14f, 0.98f);

        MakeLabel(bg, $"[TESTE] ESCOLHA 1 BÔNUS ({allOptions.Count} opções):", 24,
            new Color(1f, 0.84f, 0f), new Vector2(0, 320f), new Vector2(1000f, 36f), bold: true);

        var scrollGo = new GameObject("ScrollView");
        scrollGo.transform.SetParent(bg.transform, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = scrollRt.anchorMax = scrollRt.pivot = new Vector2(0.5f, 0.5f);
        scrollRt.sizeDelta = new Vector2(1060f, 580f);
        scrollRt.anchoredPosition = new Vector2(0, -20f);
        scrollGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.15f);
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
        grid.cellSize        = new Vector2(170f, 200f);
        grid.spacing         = new Vector2(10f, 10f);
        grid.childAlignment  = TextAnchor.UpperCenter;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRt;

        foreach (var opt in allOptions)
        {
            var capturedOpt = opt;
            // scale: 1f (sem escala neste modo — grade de teste usa células fixas 170x200).
            // detailPanel: null (modo de teste, sem painel de detalhamento — ver ShowLevelUpChoice
            // pro fluxo real, que constrói e passa um). spinPool: null (sem roleta neste modo —
            // mostra o resultado direto, mesmo comportamento de sempre).
            MakeLevelUpCard(content, capturedOpt, Vector2.zero, 1f, null, null, 0f, null,
                () => { ApplyBonus(capturedOpt, profile); Object.Destroy(root); onChosen(); }, theme);
        }
    }

    // spinPool/spinDuration/onSpinComplete (2026-07-21, pedido do usuário — efeito de "roleta de
    // cassino"): se `spinPool` tiver itens, o ícone gira (LevelUpReelSpinner) por `spinDuration`
    // segundos antes de travar no resultado — "Escolher" e o botão de popup do ícone ficam
    // desabilitados até travar. `spinPool` nulo/vazio (modo de teste, ShowAllOptionsChoice) pula a
    // animação e mostra o resultado direto, mesmo comportamento de antes desta mudança.
    private static void MakeLevelUpCard(GameObject parent, LevelUpOption opt, Vector2 pos, float scale,
        CharacterPanel detailPanel, List<Sprite> spinPool, float spinDuration,
        System.Action onSpinComplete, System.Action onClick, UITheme theme = null)
    {
        var card = new GameObject("Card");
        card.transform.SetParent(parent.transform, false);
        var rt = card.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(270f, 270f);
        rt.anchoredPosition = pos;
        // Escala o card inteiro (pedido do usuário, 2026-07-21: "1.5") — pivot central já
        // configurado acima, então cresce simetricamente em torno da própria posição; todo o
        // conteúdo interno (ícone/labels/botão) escala junto de graça, sem precisar tocar em
        // nenhum valor de layout individual.
        rt.localScale = Vector3.one * scale;
        card.AddComponent<Image>().color = new Color(0.09f, 0.09f, 0.22f, 1f);

        // Borda colorida por tier (T1/T2/T3, cinza/verde/azul — ver UITheme.TierColor), atrás do
        // ícone — só para Skill/Weapon/Pet (Attribute não tem tier, não é um item). Local novo
        // (2026-07-27, pedido do usuário): esta tela nunca teve indicação de tier nenhuma antes,
        // igual à mesma borda já usada em CharacterPanel/ArsenalSlotUI/CharacterUnlockRevealPanel.
        if (opt.kind != LevelUpOption.Kind.Attribute && theme != null)
        {
            int borderTier = opt.kind switch
            {
                LevelUpOption.Kind.Skill  => opt.skill != null ? opt.skill.tier : 1,
                LevelUpOption.Kind.Weapon => opt.weapon != null ? opt.weapon.tier : 1,
                LevelUpOption.Kind.Pet    => opt.petData != null ? opt.petData.tier : 1,
                _ => 1,
            };
            var borderGo = new GameObject("TierBorder");
            borderGo.transform.SetParent(card.transform, false);
            var borderRt = borderGo.AddComponent<RectTransform>();
            borderRt.anchorMin = borderRt.anchorMax = new Vector2(0.5f, 0.5f);
            borderRt.sizeDelta = new Vector2(164f, 164f);
            borderRt.anchoredPosition = new Vector2(0, 45f);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.sprite = UIShapeUtil.RoundedRect(theme.TierColor(borderTier), 12f);
            borderImg.type = Image.Type.Sliced;
            borderImg.raycastTarget = false;
        }

        // Icon
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(card.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
        // 150x150 (pedido do usuário, 2026-07-21, correção — era 80x80, tentativa anterior de
        // +130=210 ficou grande demais).
        iconRt.sizeDelta = new Vector2(150f, 150f);
        // posY 45 (pedido do usuário, 2026-07-21) — era 85.
        iconRt.anchoredPosition = new Vector2(0, 45f);
        var iconImg = iconGo.AddComponent<Image>();

        // Bug real corrigido (2026-07-17): Kind.Pet nunca entrava neste switch (caía sempre no
        // `_ => null` e mostrava só a cor placeholder abaixo) — mesmo depois de `PetData.icon`
        // ser preenchido de verdade (2026-07-16, ver SelectOpponentController). O ícone já
        // existia, só não estava sendo lido aqui.
        Sprite finalSprite = opt.kind switch {
            LevelUpOption.Kind.Skill   => LevelUpEngine.ResolveSkillIcon(opt.skill),
            LevelUpOption.Kind.Weapon  => opt.weapon?.inHandSprite,
            LevelUpOption.Kind.Pet     => opt.petData?.icon,
            _                          => null
        };
        Color finalColor;
        if (finalSprite != null) finalColor = Color.white;
        else if (opt.kind == LevelUpOption.Kind.Pet)
            // Fallback — só alcançado se opt.petData.icon vier null (asset sem ícone atribuído,
            // não deveria mais acontecer pros 3 pets reais, mas mantido por segurança).
            finalColor = new Color(0.55f, 0.35f, 0.18f);
        else
            finalColor = opt.AttrColor();

        // Popup de detalhe (2026-07-21, pedido do usuário) — clicar no ícone abre o mesmo popup
        // do 01_MainMenu (ShowSkillDetail/ShowWeaponDetail/ShowPetDetail, CharacterPanel). Sem
        // popup pra Kind.Attribute (não tem SkillData/WeaponData/PetData pra mostrar) nem quando
        // detailPanel é nulo (theme não disponível — ver ShowLevelUpChoice).
        Button iconBtn = null;
        if (detailPanel != null && opt.kind != LevelUpOption.Kind.Attribute)
        {
            iconBtn = iconGo.AddComponent<Button>();
            iconBtn.targetGraphic = iconImg;
            iconBtn.onClick.AddListener(() =>
            {
                switch (opt.kind)
                {
                    case LevelUpOption.Kind.Skill:  detailPanel.ShowSkillDetail(opt.skill); break;
                    case LevelUpOption.Kind.Weapon: detailPanel.ShowWeaponDetail(opt.weapon); break;
                    case LevelUpOption.Kind.Pet:    detailPanel.ShowPetDetail(opt.petData, opt.petData?.icon); break;
                }
            });
        }

        // Descrição removida (pedido do usuário, 2026-07-21 — "a descrição abaixo desses dois
        // remove") — card mostra só ícone + nome agora. Nome logo abaixo do ícone (pedido do
        // usuário) — ícone em posY 45, meia-altura 75 (150/2), borda inferior em -30; label
        // centralizado em -52 (borda -30, meia-altura do label 17, +5 de vão).
        MakeLabel(card, opt.Name(), 22, Color.white, new Vector2(0, -52f), new Vector2(250f, 34f), bold: true);

        var btn = MakeButton(card, "Escolher", new Vector2(0, -100f), onClick);
        btn.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 44f);

        // Roleta (2026-07-21, pedido do usuário): gira o ícone até travar no resultado real (já
        // sorteado antes desta chamada — a animação é só a revelação); "Escolher" e o popup do
        // ícone ficam desabilitados até travar, pra não deixar escolher/inspecionar um resultado
        // que ainda está "girando". `spinPool` vazio/nulo (modo de teste) pula direto pro estado
        // final, mesmo comportamento de antes desta mudança.
        void HandleLanded()
        {
            btn.interactable = true;
            if (iconBtn != null) iconBtn.interactable = true;
            onSpinComplete?.Invoke();
        }

        if (spinPool != null && spinPool.Count > 0)
        {
            btn.interactable = false;
            if (iconBtn != null) iconBtn.interactable = false;
            var spinner = iconGo.AddComponent<LevelUpReelSpinner>();
            spinner.Play(iconImg, finalSprite, finalColor, spinPool, spinDuration, HandleLanded);
        }
        else
        {
            iconImg.sprite = finalSprite;
            iconImg.color = finalColor;
            HandleLanded();
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IEnumerator AnimateBar(Image fill, float from, float to, float duration)
    {
        fill.fillAmount = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            fill.fillAmount = Mathf.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        fill.fillAmount = to;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static IEnumerator LoadMainMenuAsync()
    {
        var op = SceneManager.LoadSceneAsync("01_MainMenu");
        while (op != null && !op.isDone) yield return null;
    }

    private static Canvas FindScreenCanvas()
    {
        foreach (var c in FindObjectsOfType<Canvas>())
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        return null;
    }

    private static void MakeOverlay(Transform parent)
    {
        var go = new GameObject("Overlay");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.65f);
        img.raycastTarget = false;
    }

    private static GameObject MakePanel(Transform parent)
    {
        var go = new GameObject("ResultPanel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(500f, 450f);
        rt.anchoredPosition = Vector2.zero;
        go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.13f, 0.97f);
        return go;
    }

    private static TextMeshProUGUI MakeLabel(GameObject panel, string text, float fontSize,
        Color color, Vector2 pos, Vector2 size, bool bold = false)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(panel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        return tmp;
    }

    private static Image MakeXpBar(GameObject panel, Vector2 pos, Vector2 size)
    {
        var bg = new GameObject("XpBarBg");
        bg.transform.SetParent(panel.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = size;
        bgRt.anchoredPosition = pos;
        bg.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(bg.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        var img = fillGo.AddComponent<Image>();
        img.color      = new Color(0.25f, 0.55f, 1f);
        img.type       = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillAmount = 0f;
        return img;
    }

    private static Button MakeButton(GameObject panel, string label, Vector2 pos, System.Action onClick)
    {
        var go = new GameObject("Button");
        go.transform.SetParent(panel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(240f, 54f);
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.13f, 0.42f, 0.13f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 26f;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return btn;
    }

}
