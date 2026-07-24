using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    // Roster real (2026-07-24, sistema de compra de personagens/case opening — ver
    // ARQUITETURA.md "Modelo de roster multi-personagem"). Populado por
    // LoadRosterAndRefreshGridAsync, mesclado com os assets pré-autorados em
    // PopulateCharacterGridRoutine. Vazio até o fetch assíncrono completar (ou pra sempre, sem
    // conta logada) — o grid nasce só com os assets legados, igual sempre foi, e é reconstruído
    // quando o roster chega.
    private readonly List<PlayerProfile> _rosterProfiles = new List<PlayerProfile>();
    private GameObject btnSelecionarGo;
    private GameObject btnFecharGo;
    private GameObject selectionOverlayGo;
    private Image frameImage;

    // Unlocks progressivos de skill/arma/pet do case opening (2026-07-25) - ver
    // CharacterUnlockEngine/ResolveCaseUnlocksAsync. Instanciado uma única vez em Start() e
    // reaproveitado a cada personagem recém-concedido que passar por
    // ResolvePendingCharacterSelectionAsync.
    private CharacterUnlockRevealPanel unlockRevealPanel;

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
        BuildUnlockRevealPanel();

        // Bug real corrigido (2026-07-25 — reportado pelo usuário: "Continuar" do case opening
        // caía na grade completa em vez de ir direto pro detalhe do personagem recém-ganho).
        // PendingCharacterSelection.PendingCharacterId já está disponível de forma SÍNCRONA aqui
        // (setado por CaseOpeningPopup antes do SceneManager.LoadScene) — se presente, cobre a
        // grade ATÉ resolver a seleção (fetch assíncrono do roster, ver
        // LoadRosterAndRefreshGridAsync/ResolvePendingCharacterSelection abaixo), pra o usuário
        // nunca ver nem um flash da grade inteira antes do overlay de detalhe cobrir a tela —
        // "SEM passar pela grade completa antes", pedido explícito do usuário.
        //
        // Bug real corrigido (2ª rodada, 2026-07-25) — a 1ª versão fazia isso desativando
        // `gridPanel` (`SetActive(false)`) em vez de só cobrir visualmente: `PopulateCharacterGrid`
        // (chamada logo abaixo, e de novo em `LoadRosterAndRefreshGridAsync` quando o roster
        // chega) constrói os cards com `gridPanel` ainda INATIVO nesse caminho — `GridLayoutGroup`/
        // `ContentSizeFitter` nunca recalculam layout de uma hierarquia inativa (a Unity só
        // processa esses rebuilds quando o Canvas de fato renderiza), deixando a grade presa num
        // estado quebrado (card cortado/mal posicionado) que NÃO se autocorrige de forma
        // confiável mesmo depois de `gridPanel` ser reativado — reportado pelo usuário como "uma
        // caixinha pequena" ao fechar o overlay de detalhe. Fix definitivo: `gridPanel` nunca mais
        // é desativado — fica sempre ativo (constrói normalmente, IGUAL ao fluxo de clique manual
        // num card, que nunca teve esse bug) e o "esconder até resolver" agora é um painel opaco
        // temporário próprio (`ShowPendingSelectionCover`/`HidePendingSelectionCover`), puramente
        // visual, sem nunca desativar a hierarquia da grade.
        bool hasPendingSelection = !string.IsNullOrEmpty(PendingCharacterSelection.PendingCharacterId);
        if (hasPendingSelection) ShowPendingSelectionCover();

        PopulateCharacterGrid();

        // Roster real (2026-07-24) — grid acima já nasce só com os assets legados (0 latência,
        // comportamento de sempre); se logado, busca o roster do Firestore e reconstrói o grid
        // mesclado quando chegar (mesmo padrão "mostra default, atualiza quando os dados reais
        // chegam" de ShopController.LoadPersistedShopStateAsync). Sem conta, mantém o
        // comportamento de sempre (só os assets).
        if (AuthService.IsSignedIn)
        {
            _ = LoadRosterAndRefreshGridAsync();
        }
        else if (hasPendingSelection)
        {
            // Defensivo (2026-07-25) — não deveria acontecer na prática (comprar um case exige
            // estar logado), mas sem isso uma seleção pendente sem sessão ativa deixaria a grade
            // coberta pra sempre (LoadRosterAndRefreshGridAsync, o único lugar que remove a
            // cobertura, nunca rodaria) e o PendingCharacterId nunca seria limpo.
            PendingCharacterSelection.PendingCharacterId = null;
            HidePendingSelectionCover();
        }
    }

    // Painel opaco temporário (mesma cor do overlay de detalhe, `theme.panelBackgroundAlt`) que
    // cobre a tela enquanto uma seleção pendente do case opening é resolvida — ver comentário em
    // Start(). Deliberadamente SEPARADO de `selectionOverlayGo` (que só é populado com o
    // personagem certo depois que `match` é resolvido) e NUNCA desativa `gridPanel` — a grade
    // constrói/permanece corretamente laid out por baixo o tempo todo, igual ao fluxo normal de
    // clique manual num card.
    private GameObject pendingSelectionCoverGo;

    private void ShowPendingSelectionCover()
    {
        // Sibling logo ACIMA de gridPanel (não o último da lista) — cobre só a grade, sem tapar
        // o botão "Voltar" fixo nem qualquer outro elemento construído depois em Start() (mesmo
        // escopo visual que `gridPanel.SetActive(false)` tinha antes, só sem desativar a
        // hierarquia).
        if (pendingSelectionCoverGo != null)
        {
            pendingSelectionCoverGo.SetActive(true);
            pendingSelectionCoverGo.transform.SetSiblingIndex(gridPanel.transform.GetSiblingIndex() + 1);
            return;
        }

        var canvasTransform = gridPanel.transform.parent;
        pendingSelectionCoverGo = new GameObject("PendingSelectionCover");
        pendingSelectionCoverGo.transform.SetParent(canvasTransform, false);
        pendingSelectionCoverGo.transform.SetSiblingIndex(gridPanel.transform.GetSiblingIndex() + 1);
        var rt = pendingSelectionCoverGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = pendingSelectionCoverGo.AddComponent<Image>();
        img.color = theme.panelBackgroundAlt;
    }

    private void HidePendingSelectionCover()
    {
        if (pendingSelectionCoverGo != null) pendingSelectionCoverGo.SetActive(false);
    }

    // Busca users/{uid}/characters via RosterService, reconstrói um PlayerProfile runtime por
    // documento com characterTypeId preenchido (o doc "legado" sem esse campo já está
    // representado pelo asset pré-autorado correspondente — nunca duplicar), reconstrói o grid
    // mesclado e, por fim, resolve PendingCharacterSelection (personagem recém-concedido pelo
    // case opening, se houver — ver CaseOpeningPopup).
    private async Task LoadRosterAndRefreshGridAsync()
    {
        string uid = AuthService.CurrentUser.UserId;
        var docs = await RosterService.ListOwnedCharacterDocsAsync(uid);

        _rosterProfiles.Clear();
        foreach (var dto in docs)
        {
            if (dto == null || string.IsNullOrEmpty(dto.characterTypeId)) continue;
            var runtime = PlayerProfileConverter.FromCharacterDTO(dto, characterDatabase);
            if (runtime == null) continue;
            LocalSaveService.ApplyIfSaved(runtime);
            _rosterProfiles.Add(runtime);
        }

        PopulateCharacterGrid();
        await ResolvePendingCharacterSelectionAsync(uid);
    }

    // Personagem recém-concedido pelo case opening (2026-07-24) — CaseOpeningPopup grava o
    // characterId aqui antes de navegar pra esta cena; abrir o detalhe automaticamente (mesmo
    // efeito visual de clicar o card manualmente — overlay full-screen, background art, painel
    // expandido) satisfaz "abrir já com esse personagem em destaque". Consumido uma única vez —
    // limpo logo em seguida, nunca reaplicado numa visita futura à cena.
    private async Task ResolvePendingCharacterSelectionAsync(string uid)
    {
        string pendingId = PendingCharacterSelection.PendingCharacterId;
        if (string.IsNullOrEmpty(pendingId)) return;
        PendingCharacterSelection.PendingCharacterId = null;

        PlayerProfile match = null;
        foreach (var profile in _rosterProfiles)
        {
            if (profile != null && profile.characterId == pendingId) { match = profile; break; }
        }

        // Fallback (2026-07-25, bug real reportado pelo usuário — "Continuar" não levava pro
        // detalhe do personagem) — se a listagem geral (LoadRosterAndRefreshGridAsync acima) não
        // trouxe o documento por qualquer motivo (cache local do SDK ainda sem conhecimento do
        // doc gravado por outro processo — a Cloud Function, via Admin SDK — timing, etc.), busca
        // ESSE documento específico direto do SERVIDOR (RosterService.GetOwnedCharacterDocAsync,
        // Source.Server explícito, ignora cache) antes de desistir.
        if (match == null)
        {
            var dto = await RosterService.GetOwnedCharacterDocAsync(uid, pendingId);
            if (dto != null)
            {
                match = PlayerProfileConverter.FromCharacterDTO(dto, characterDatabase);
                if (match != null)
                {
                    LocalSaveService.ApplyIfSaved(match);
                    _rosterProfiles.Add(match);
                }
            }
        }

        // Remove o painel de cobertura temporário (Start() o mostrou só pra evitar o flash
        // enquanto isto rodava, ver comentário lá) — `gridPanel` nunca foi desativado, então não
        // precisa ser reativado aqui. Se achou o personagem, a grade fica coberta pelo overlay
        // full-screen de qualquer forma (OnCharacterSelected abaixo); se não achou (ver log
        // abaixo), a grade normal (já corretamente construída, já que nunca ficou inativa)
        // aparece como fallback em vez de deixar o usuário preso numa tela vazia.
        HidePendingSelectionCover();

        if (match != null)
        {
            // OnCharacterSelected já dispara/retoma ResolveCaseUnlocksAsync sozinho quando
            // necessário (ver comentário lá) — cobre tanto este caminho (recém-concedido) quanto
            // reabrir um personagem cuja sequência ficou incompleta numa sessão anterior.
            OnCharacterSelected(match);
        }
        else
        {
            // Falha real (2026-07-25) — não deveria acontecer no fluxo normal (o personagem
            // acabou de ser concedido por purchaseCase, a transaction já commitou antes do
            // Cloud Function responder, e o fallback acima já tentou um get() direto no
            // servidor); logado pra ajudar a diagnosticar se persistir (ex: characterDatabase
            // deste componente não bate com o catálogo exportado pra
            // functions/src/characterCatalog.json — ver PlayerProfileConverter.FromCharacterDTO,
            // que já loga o motivo exato da falha de match de molde separadamente).
            Debug.LogError($"[CharacterSelectController] PendingCharacterSelection '{pendingId}' não encontrado (nem na listagem geral, nem no fallback direto ao servidor) — caindo na grade normal.");
        }
    }

    // Cria (escondido) o painel de reveal dos unlocks de skill/arma/pet do case opening — ver
    // CharacterUnlockRevealPanel. Mesma regra de instanciação de CharacterPanel (GameObject SEM
    // pai, raiz da cena — o componente cria seu próprio Canvas filho).
    private void BuildUnlockRevealPanel()
    {
        var go = new GameObject("CharacterUnlockRevealPanel");
        unlockRevealPanel = go.AddComponent<CharacterUnlockRevealPanel>();
        unlockRevealPanel.Build(theme);
    }

    // Unlocks progressivos de skill/arma/pet concedidos ao ganhar este personagem via case
    // opening (2026-07-25, ver CharacterUnlockEngine) — N sorteios sequenciais
    // (CharacterUnlockEngine.UnlockCountForRarity, por raridade), revelados um de cada vez dentro
    // do próprio overlay de detalhe já aberto por OnCharacterSelected (pedido explícito do
    // usuário — não uma tela própria antes de chegar aqui), bloqueando Selecionar/Fechar até
    // resolver todos.
    //
    // Refresh (2026-07-25, mesmo dia): cada unlock começa como um RASCUNHO (sorteado, mas ainda
    // NÃO aplicado a `match.skills/weapons/pets`) — só vira de verdade quando o jogador clica
    // "Continuar". Enquanto isso, "Refresh" pode substituir o rascunho (até
    // UnlockRerollService.MaxRerollsPerUnlock vezes), sempre via Cloud Function
    // (UnlockRerollService/rerollUnlock — diamante e limite validados/decididos 100%
    // server-side, nunca só no client, mesma regra inegociável de ARQUITETURA.md "Moeda
    // premium"). Aplicar só no aceite (não no sorteio) é o que garante que um rascunho descartado
    // nunca contamine `CharacterUnlockEngine.OwnedTier` do PRÓXIMO unlock (ou do próprio
    // rerollUnlock no servidor, que lê o characterId no Firestore pra decidir o tier por posse).
    //
    // Retomada (2026-07-25, bug real corrigido — reportado pelo usuário: fechar o app durante o
    // 1º unlock perdia todos os seguintes): `match.caseUnlocksAcceptedCount` é persistido a cada
    // "Continuar" aceito, então o loop sempre recomeça do PRÓXIMO unlock ainda não aceito, nunca
    // do 1º — nem perde os restantes (chamada não roda de novo sozinha, mas `OnCharacterSelected`
    // agora tenta de novo toda vez que o detalhe deste personagem é reaberto, ver lá) nem
    // duplica os já aceitos (não dá pra usar o tamanho de skills/weapons/pets como proxy — um
    // unlock que evolui uma família já possuída não aumenta esse total).
    //
    // Retomada do RASCUNHO (2026-07-25, 2ª rodada — reportado pelo usuário: sorteou "Book",
    // fechou o app antes de aceitar, reabriu e veio "Vampirismo" — um sorteio DIFERENTE em vez de
    // continuar mostrando o mesmo; e o contador de refresh reiniciava pra "2 disponíveis" mesmo
    // já tendo usado algum antes, causando "resource-exhausted" inesperado ao tentar de novo).
    // `match.pendingUnlock*` persiste o rascunho (kind/name/tier + refreshes já usados) a cada
    // sorteio/refresh, ANTES de mostrar o painel — se `pendingUnlockIndex` já bater com o `i`
    // atual ao entrar no loop, resolve esse mesmo rascunho de volta em vez de sortear um novo.
    private async Task ResolveCaseUnlocksAsync(PlayerProfile match)
    {
        int total = CharacterUnlockEngine.UnlockCountForRarity(match.rarity);

        // Esconde os botões de ação (já visíveis desde OnCharacterSelected) até resolver a
        // sequência inteira — mesmo espírito de "Continuar" bloqueado até escolher no level-up de
        // combate (CombatResultPanel.ShowLevelUpChoice).
        btnSelecionarGo.SetActive(false);
        btnFecharGo.SetActive(false);

        for (int i = match.caseUnlocksAcceptedCount + 1; i <= total; i++)
        {
            LevelUpOption option;
            int remainingRerolls;

            var resumed = match.pendingUnlockIndex == i
                ? CharacterUnlockEngine.ResolveServerResult(match.pendingUnlockKind, match.pendingUnlockName, match.pendingUnlockTier)
                : null;

            if (resumed.HasValue)
            {
                // Rascunho de uma sessão anterior — mesmo resultado, mesmo contador de refresh
                // real (não reinicia pra "2 disponíveis" à toa).
                option = resumed.Value;
                remainingRerolls = Mathf.Max(0, UnlockRerollService.MaxRerollsPerUnlock - match.pendingUnlockRerollsUsed);
            }
            else
            {
                // Sem rascunho pendente pra este índice (1ª vez chegando aqui, ou o rascunho
                // salvo ficou inconsistente — ex: catálogo mudou) — sorteia do zero e já persiste
                // imediatamente, antes de mostrar o painel, pra sobreviver a um fechamento logo
                // em seguida.
                option = CharacterUnlockEngine.DrawUnlock(match);
                remainingRerolls = UnlockRerollService.MaxRerollsPerUnlock;
                PersistUnlockDraft(match, i, option, 0);
            }

            while (true)
            {
                var tcs = new TaskCompletionSource<bool>(); // true = Continuar, false = Refresh
                unlockRevealPanel.Show(i, total, option, remainingRerolls,
                    onContinueClicked: () => tcs.TrySetResult(true),
                    onRefreshClicked: () => tcs.TrySetResult(false));
                bool accepted = await tcs.Task;
                if (accepted) break;

                // Refresh clicado — chamada server-authoritative; painel fica bloqueado
                // (SetBusy) até a resposta chegar, pra evitar clique duplo/corrida.
                unlockRevealPanel.SetBusy(true);
                var result = await UnlockRerollService.RerollUnlockAsync(match.characterId, i);
                if (!result.Success)
                {
                    // Mesmo rascunho, mesmo contador — servidor recusou (saldo/limite mudou
                    // entre a checagem local e a chamada, ou falha de rede); jogador pode tentar
                    // de novo ou só aceitar o resultado atual com "Continuar".
                    unlockRevealPanel.ShowRefreshError(result.ErrorMessage);
                    continue;
                }

                var newOption = CharacterUnlockEngine.ResolveServerResult(result.Kind, result.Name, result.Tier);
                if (newOption.HasValue) option = newOption.Value;
                remainingRerolls = result.RemainingRerolls;
                int rerollsUsed = UnlockRerollService.MaxRerollsPerUnlock - remainingRerolls;
                PersistUnlockDraft(match, i, option, rerollsUsed);
                // Diamante já debitado no servidor dentro da mesma transaction do sorteio — só
                // espelha localmente (mesmo padrão de WalletService.SpendDiamondsAsync, nunca uma
                // segunda escrita client-side).
                PlayerEconomyState.Diamonds = Mathf.Max(0, PlayerEconomyState.Diamonds - UnlockRerollService.CostDiamonds);
            }

            // Aceito — só agora aplica de verdade (mesma mutação/bônus do level-up de combate,
            // LevelUpEngine.ApplyOption), limpa o rascunho e persiste, antes de sortear o próximo
            // unlock.
            LevelUpEngine.ApplyOption(option, match);
            match.caseUnlocksAcceptedCount = i; // ponto de retomada — ver comentário acima
            ClearUnlockDraft(match);
            LocalSaveService.Save(match);
            characterPanel.Refresh(); // reflete o item novo na grade de Habilidades/Armas/Pets ao vivo
        }

        unlockRevealPanel.Hide();
        match.caseUnlocksResolved = true;
        LocalSaveService.Save(match);

        btnSelecionarGo.SetActive(true);
        btnFecharGo.SetActive(true);
    }

    // Persiste o rascunho ATUAL do unlock em andamento (índice + kind/name/tier + quantos
    // refreshes já foram usados) — chamado logo após sortear/rerolar, ANTES de mostrar o painel
    // e aguardar a resposta do jogador, pra sobreviver a um fechamento do app nesse meio-tempo
    // (ver ResolveCaseUnlocksAsync).
    private static void PersistUnlockDraft(PlayerProfile match, int unlockIndex, LevelUpOption option, int rerollsUsed)
    {
        var shape = CharacterUnlockEngine.ToServerShape(option);
        match.pendingUnlockIndex = unlockIndex;
        match.pendingUnlockKind = shape.kind;
        match.pendingUnlockName = shape.name;
        match.pendingUnlockTier = shape.tier;
        match.pendingUnlockRerollsUsed = rerollsUsed;
        LocalSaveService.Save(match);
    }

    // Limpa o rascunho pendente ao aceitar um unlock (não faz sentido mais depois de aplicado).
    private static void ClearUnlockDraft(PlayerProfile match)
    {
        match.pendingUnlockIndex = 0;
        match.pendingUnlockKind = "";
        match.pendingUnlockName = "";
        match.pendingUnlockTier = 0;
        match.pendingUnlockRerollsUsed = 0;
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
    //
    // Construção em lotes (2026-07-14, correção de perf — reportado pelo usuário como "trava" ao
    // entrar na cena): com 72 personagens em `unlockedCharacters` hoje, criar os 72
    // `CharacterCardUI` (cada um com ~8 GameObjects + texto TMP com auto-sizing) num único frame
    // de `Start()` gera um pico perceptível. `PopulateCharacterGrid()` agora só (re)inicia uma
    // coroutine que constrói `CardsPerFrame` cards por frame — o resultado final (mesma ordem,
    // mesmo conteúdo) é idêntico, só distribuído ao longo de alguns frames em vez de travar um
    // só. Chamado de novo em `OnFavoriteClicked` (repopula a grade inteira) — cancela qualquer
    // construção em andamento antes de recomeçar, pra não duplicar cards se o usuário favoritar
    // outro personagem antes da grade terminar de aparecer.
    private const int CardsPerFrame = 12;
    private Coroutine populateRoutine;

    public void PopulateCharacterGrid()
    {
        if (populateRoutine != null) StopCoroutine(populateRoutine);
        populateRoutine = StartCoroutine(PopulateCharacterGridRoutine());
    }

    private IEnumerator PopulateCharacterGridRoutine()
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
        // Bug real corrigido (2026-07-25, reportado pelo usuário: "os chibers que eu comprei
        // vieram desabilitados") — comprar um personagem via case opening cujo `characterTypeId`
        // bate com um dos 72 moldes pré-autorados (ex: "Anubis") fazia o molde travado
        // (`characterDatabase.unlockedCharacters`, sempre isPlayable=false a menos que seja o
        // "original" desta conta) aparecer JUNTO da instância jogável de verdade do roster —
        // visualmente idênticos (mesmo portrait/nome), fácil de olhar pro card travado e achar que
        // é o personagem recém-comprado. Molde cujo `.name` já está em `_rosterProfiles`
        // (`characterTypeId`, ver PlayerProfile.characterTypeId) é IGNORADO aqui — a instância do
        // roster já representa esse tipo de personagem, o card travado do molde vira redundante.
        var ownedTypeIds = new HashSet<string>();
        foreach (var rp in _rosterProfiles)
            if (rp != null && !string.IsNullOrEmpty(rp.characterTypeId))
                ownedTypeIds.Add(rp.characterTypeId);

        var enabled = new List<PlayerProfile>();
        var locked = new List<PlayerProfile>();
        foreach (var p in characterDatabase.unlockedCharacters)
        {
            if (p == null || !p.isUnlockedForSelection) continue;
            if (ownedTypeIds.Contains(p.name)) continue;
            (p.isPlayable ? enabled : locked).Add(p);
        }
        // Roster real (2026-07-24) — personagens concedidos via case opening entram junto dos
        // assets legados isPlayable=true (todos já nascem isPlayable=true por construção, ver
        // PlayerProfileConverter.FromCharacterDTO), ordenados pela MESMA regra de sempre
        // (favorito → raridade → nome). O personagem "original" da conta (doc legado, sem
        // characterTypeId) não entra em _rosterProfiles — já está representado pelo asset acima.
        enabled.AddRange(_rosterProfiles);
        enabled.Sort(CharacterDatabase.ComparePlayerProfiles);
        locked.Sort(CharacterDatabase.ComparePlayerProfiles);

        var ordered = new List<PlayerProfile>(enabled);
        ordered.AddRange(locked);

        int builtThisFrame = 0;
        foreach (var profile in ordered)
        {
            var cardGo = new GameObject($"Card_{profile.profileName}");
            cardGo.transform.SetParent(gridContent, false);
            cardGo.AddComponent<RectTransform>();
            var card = cardGo.AddComponent<CharacterCardUI>();
            card.Setup(profile, this, theme, profile.isPlayable);

            builtThisFrame++;
            if (builtThisFrame >= CardsPerFrame)
            {
                builtThisFrame = 0;
                yield return null;
            }
        }

        populateRoutine = null;
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
        // Perf (2026-07-14): sem personagem nenhum instanciado ainda em Start(), essa câmera não
        // tem nada útil pra filmar — deixá-la ativa só soma um render extra por frame à toa até o
        // 1º clique num card. OnCharacterSelected já reativa (`previewCameraGo.SetActive(true)`)
        // e HideOverlayAfterDelay já desativa de novo ao fechar o painel; esse é só o estado
        // inicial coerente com esses dois pontos.
        previewCameraGo.SetActive(false);

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

        // Bug real corrigido (2026-07-25) — dispara/RETOMA os unlocks de skill/arma/pet do case
        // opening pra qualquer personagem do roster (isRuntimeInstance — nunca true nos ~72
        // assets pré-autorados, então isto nunca roda neles) que ainda não terminou a sequência
        // (!caseUnlocksResolved). Antes, isso só era chamado uma vez, no caminho específico do
        // PendingCharacterSelection logo após a compra — se o app fechasse no meio da sequência
        // (ex: no 1º unlock, antes de aceitar), os unlocks restantes eram perdidos pra sempre: o
        // PendingCharacterId já tinha sido consumido, então reabrir o personagem depois (mesmo
        // clicando normalmente no grid) nunca tentava de novo. Chamar isto aqui, de forma
        // incondicional a cada abertura de detalhe, cobre os dois casos com o mesmo código —
        // ResolveCaseUnlocksAsync retoma do ponto certo via `caseUnlocksAcceptedCount`, nunca
        // reconcede um unlock já aceito.
        if (profile.isRuntimeInstance && !profile.caseUnlocksResolved)
        {
            _ = ResolveCaseUnlocksAsync(profile);
        }
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
        // SetProfile() (não atribuição direta) — mantém SelectedProfileHolder.characterId em
        // sincronia (2026-07-24, ver SelectedProfileHolder.cs).
        selectedProfileHolder.SetProfile(selectedProfile);
        StartCoroutine(SyncAndLoadMainMenuRoutine());
    }

    // Sincroniza com a nuvem (CloudSyncService, 2026-07-15, Fatia 4) antes de ir pro menu — o
    // personagem escolhido aqui no grid pode não ser o mesmo que estava ativo no login (ver
    // LoginController.SyncCharacterRoutine, que só cobria esse caso antes desta fatia), então
    // sem isso o MainMenu podia mostrar dados desatualizados/default até a próxima sincronização
    // acontecer por acaso em outro ponto.
    private IEnumerator SyncAndLoadMainMenuRoutine()
    {
        var syncTask = CloudSyncService.SyncCharacterAsync(selectedProfile);
        yield return new WaitUntil(() => syncTask.IsCompleted);

        var op = SceneManager.LoadSceneAsync("01_MainMenu");
        while (op != null && !op.isDone) yield return null;
    }
}
