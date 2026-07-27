using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tela "Loja" (06_Loja, Fase 1 — placeholder, ver MONETIZACAO.md) — barra lateral vertical de
// abas à esquerda (Diamantes/Desbloqueios/Passes/Progressão/Personagens, estilo Brawl Stars,
// 2026-07-20 — era uma barra horizontal no topo com grid vertical, layout revisto a pedido do
// usuário) + grid de cards rolando HORIZONTALMENTE à direita. Mesmo padrão de construção 100% via
// código de 03_Arsenal (ArsenalController): Canvas/ScrollView/GridLayoutGroup montados em Start(),
// sem hierarquia de UI pré-colocada na cena (só Main Camera + este GameObject). Preços/itens de
// Diamantes/Desbloqueios/Passes/Progressão espelham MONETIZACAO.md — hardcoded aqui de propósito
// (comprar ainda não passa por nenhum gateway de pagamento real, Fase 8): o resultado da compra
// (diamante/desbloqueio/passe/slot) persiste de verdade desde 2026-07-21 (WalletService/
// ShopStateService), mas o PAGAMENTO em si nunca foi validado.
//
// Personagens (aba, 2026-07-23): ÚNICA exceção nesta tela — compra de verdade, validada
// server-side pela Cloud Function purchaseCase (ver CaseService/HandleCasePurchaseAsync), preço/
// limite lidos ao vivo de casePackages/ (ver ARQUITETURA.md "Modelo de roster multi-personagem").
// Recibo de pagamento cash ainda é mock (sem gateway real), mas o sorteio/concessão/débito de
// diamante já são autoritativos no servidor, nunca no cliente.
public class ShopController : MonoBehaviour
{
    [SerializeField] private UITheme theme;

    // Sistema de compra de personagens/case opening (2026-07-23) — precisa resolver ícone/nome/
    // raridade de um characterTypeId pra popular a roleta e o card de reveal (CaseOpeningPopup).
    // Wireado no Inspector da cena 06_Loja, mesmo padrão de theme.
    [SerializeField] private CharacterDatabase characterDatabase;

    private enum Tab { Diamantes, Desbloqueios, Passes, Progressao, Personagens }

    private class ShopItem
    {
        public string Title;
        public string Subtitle;
        public string PriceLabel;
        public Sprite Icon;
        public Color? AccentColor;
        public int PurchaseLimit;
        public int Purchased;

        // Diamantes (Tab.Diamantes) — bônus de 1ª compra (+25%, arredondado pra cima), só nesse
        // pacote específico. DiamondNormalAmount > 0 identifica um item como pacote de diamante
        // (os outros 4 tabs deixam em 0). TODO SEGURANÇA (pendência, Fase 1): FirstPurchaseBonusUsed
        // só existe em memória, reseta a cada sessão/reinstalação — antes do lançamento real isso
        // precisa ser autoritativo no servidor (Cloud Function/Firestore, mesma regra de "nunca
        // confiar no cliente" de ARQUITETURA.md "Moeda premium"), senão dá pra reaproveitar o
        // bônus reinstalando o app.
        public int DiamondNormalAmount;
        public int DiamondBonusAmount;
        public bool FirstPurchaseBonusUsed;

        // Sistema de compra de personagens/case opening (2026-07-23) — não vazio identifica um
        // item como pacote de case (mesmo espírito de DiamondNormalAmount > 0 pros pacotes de
        // diamante); OnBuyClicked desvia pra HandleCasePurchaseAsync antes do incremento síncrono
        // genérico, já que comprar um case chama a Cloud Function purchaseCase (assíncrono).
        public string CasePackageId;

        // "Próximo Personagem" (Coins, 2026-07-26) — substitui o antigo "Case Geral" (diamante).
        // Mesmo espírito de CasePackageId: identifica o item pra OnBuyClicked desviar pra
        // HandleNextCharacterPurchaseAsync (chama purchaseNextCharacter, assíncrono) em vez do
        // incremento síncrono genérico. Não usa CasePackageId porque não existe um doc
        // casePackages/{id} fixo pra ele — o preço escala com um contador POR JOGADOR, não por
        // pacote (ver purchaseNextCharacter.ts).
        public bool IsNextCharacterPurchase;
    }

    // Região compartilhada por Sidebar e ScrollView (2026-07-20, layout em sidebar) — mesmo topo/
    // base que a barra de abas horizontal usava antes, só a divisão horizontal entre as duas
    // colunas (sidebar à esquerda, grid à direita) é nova.
    private const float RegionTop = 0.90f;
    private const float RegionBottom = 0.04f;
    private const float SidebarLeft = 0.03f;
    private const float SidebarRight = 0.22f;
    private const float ContentLeft = 0.25f;
    private const float ContentRight = 0.97f;

    private const int DiamantesRows = 2;
    private const float CellSpacing = 20f;

    // Aba Desbloqueios — regra de exclusão do Bundle (pedido do usuário, 2026-07-20): comprar
    // Skip ou 1.5x avulso primeiro remove o Bundle da lista (não faz sentido continuar oferecendo
    // o combo depois que um dos dois já foi liberado individualmente). Ver OnBuyClicked.
    private const string SkipTitle = "Skip de batalha";
    private const string BoostTitle = "Velocidade 1.5x";
    private const string BundleTitle = "Bundle (Skip + 1.5x)";

    // Aba Passes — títulos usados como chave (mesmo padrão de SkipTitle/BoostTitle/BundleTitle
    // acima) pra identificar qual card foi comprado e decidir o status a mostrar em cada um. Ver
    // ApplyPassPurchase/PassStatusOverride/ShowPassInfoPopup.
    private const string PasseBasicoTitle = "Passe Básico";
    private const string PasseProTitle = "Passe Pro";

    // Aba Progressão — trava de pré-requisito sequencial (pedido do usuário, 2026-07-21): Slot 2
    // exige Slot 1 já comprado, Slot 3 exige Slot 2. Ver ProgressionLockState/OnBuyClicked.
    private const string Slot1Title = "Slot de Skill 1";
    private const string Slot2Title = "Slot de Skill 2";
    private const string Slot3Title = "Slot de Skill 3";

    // Aba Personagens — "Próximo Personagem" (Coins, 2026-07-26 — compra REAL, substitui o antigo
    // "Case Geral" de diamante). Preço escala com um contador PERSISTIDO por jogador
    // (PlayerEconomyState.NextCharacterPurchaseCount, espelhando users/{uid}.
    // nextCharacterPurchaseCount gravado pela Cloud Function purchaseNextCharacter) — não mais um
    // contador de sessão em memória. Ver CharacterSlotCost/HandleNextCharacterPurchaseAsync.
    private const string CharacterSlotTitle = "Próximo Personagem";

    // Aba Personagens — sistema de compra de personagens/case opening (2026-07-23). IDs batem
    // 1:1 com os documentos casePackages/{packageId} no Firestore (ver
    // functions/src/scripts/seedCasePackages.ts). Preço/limite abaixo são o FALLBACK mostrado
    // antes do primeiro carregamento real (ou se o documento não existir ainda) — mesmos valores
    // de MONETIZACAO.md seções 5/7, devem ficar em sincronia manual com o seed.
    private const string PackageIdRare = "case_rare";
    private const string PackageIdLegendary = "case_legendary";
    private const string PackageIdImmortal = "case_immortal";

    // Diamantes (2 linhas): dimensiona pra caber ~4 colunas visíveis na largura do ScrollView.
    private const float CardWidthMulti = 320f;
    private const float CardHeightMulti = 430f;

    // Demais abas (1 linha): cards bem maiores, preenchendo quase toda a altura disponível —
    // pedido do usuário ("hoje sobra muito fundo bege vazio abaixo dos cards").
    private const float CardWidthSingle = 620f;
    private const float CardHeightSingle = 840f;

    private Tab _activeTab = Tab.Diamantes;
    private Transform _contentRoot;
    private GridLayoutGroup _grid;
    private readonly Dictionary<Tab, Image> _tabBackgrounds = new Dictionary<Tab, Image>();
    private readonly Dictionary<Tab, List<ShopItem>> _items = new Dictionary<Tab, List<ShopItem>>();

    // Contador de diamante no header + efeito de "diamantes voando" (2026-07-21, pedido do
    // usuário) — ver BuildDiamondCounter/SpawnDiamondBurst/FlyingDiamondIcon.cs.
    private Transform _canvasTransform;
    private Sprite _diamondIconSprite;
    private TMP_Text _diamondValueTxt;
    private RectTransform _diamondIconRt;
    private int _displayedDiamonds;

    void Start()
    {
        if (theme == null) return;

        _diamondIconSprite = Resources.Load<Sprite>("UI/Economy/Diamond");
        BuildItemData();

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // **Bug real corrigido (2026-07-21)**: capturar `canvasGo.transform` ANTES de
        // `AddComponent<Canvas>()` guardava uma referência pro `Transform` PLANO original do
        // GameObject — `Canvas` exige `RectTransform` (`[RequireComponent]`), e o Unity troca o
        // `Transform` original por um `RectTransform` novo nesse momento (destrói o componente
        // antigo). A referência já capturada em `_canvasTransform` ficava "morta" (fake-null) —
        // `FlyingDiamondIcon.Rent` chamava `SetParent(_canvasTransform, false)` com essa
        // referência inválida, e o ícone acabava desparentado (fora de qualquer Canvas), nunca
        // renderizando, SEM lançar exceção nenhuma (motivo do usuário não ver erro nenhum no
        // Console). Corrigido capturando `_canvasTransform` só AGORA, depois que `Canvas` (e o
        // `RectTransform` que ele exige) já existem de verdade.
        _canvasTransform = canvasGo.transform;

        EnsureEventSystem();

        BuildBackground(canvasGo.transform);
        BuildHeader(canvasGo.transform);
        BuildDiamondCounter(canvasGo.transform);
#if UNITY_EDITOR
        BuildDevCoinButton(canvasGo.transform);
#endif
        BuildSidebar(canvasGo.transform);
        BuildScrollView(canvasGo.transform);

        SelectTab(Tab.Diamantes);

        // Persistência real (2026-07-21) — a 1ª renderização acima já usa o que estiver nos canais
        // estáticos (PlayerEconomyState/PlayerUnlocksState/PlayerPassState/PlayerProgressionState,
        // zerados numa sessão nova); assim que a leitura do Firestore chega, reconstrói tudo pra
        // refletir o estado real (card já comprado aparece "Ativo"/esgotado, bônus de 1ª compra
        // some se já usado). Sem conta logada, mantém o comportamento fake local de sempre (sem
        // Firestore pra ler/gravar).
        if (AuthService.IsSignedIn) _ = LoadPersistedShopStateAsync();
    }

    private async Task LoadPersistedShopStateAsync()
    {
        string uid = AuthService.CurrentUser.UserId;
        var (coins, diamonds) = await WalletService.LoadAsync(uid);
        PlayerEconomyState.Coins = coins;
        PlayerEconomyState.Diamonds = diamonds;
        PlayerEconomyState.NextCharacterPurchaseCount = await WalletService.LoadNextCharacterPurchaseCountAsync(uid);
        await ShopStateService.LoadAsync(uid);
        await LoadCasePackageStateAsync(uid);

        BuildItemData();
        RebuildGrid();
        _displayedDiamonds = PlayerEconomyState.Diamonds;
        if (_diamondValueTxt != null) _diamondValueTxt.text = _displayedDiamonds.ToString();
    }

    // Sistema de compra de personagens/case opening (2026-07-23) — carrega os 4 pacotes
    // (casePackages/{packageId}, catálogo estático) + quantas vezes esta conta já comprou cada um
    // (users/{uid}/casePurchases/{packageId}), pra BuildItemData mostrar preço/limite/"restantes"
    // reais em vez do fallback hardcoded. Falha silenciosa por pacote (CasePackageService já loga
    // erro) — card cai pro fallback se o documento não existir ainda (ex: seed não rodado).
    private static readonly string[] AllPackageIds = { PackageIdRare, PackageIdLegendary, PackageIdImmortal };

    private async Task LoadCasePackageStateAsync(string uid)
    {
        foreach (var packageId in AllPackageIds)
        {
            var info = await CasePackageService.LoadPackageAsync(packageId);
            if (info != null) CasePackageState.Packages[packageId] = info;

            int purchasedCount = await CasePackageService.LoadPurchasedCountAsync(uid, packageId);
            CasePackageState.PurchasedCounts[packageId] = purchasedCount;
        }
    }

    // Cena nova sem EventSystem pré-colocado — mesmo padrão de ArsenalController/CombatHUD.
    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    // Dados espelhando MONETIZACAO.md (seções 1-5) — inalterado nesta tarefa (só layout mudou).
    // Diamantes usa o ícone de arte final (Assets/Resources/UI/Economy/Diamond.png, mesmo
    // Resources.Load usado por MainMenuController.BuildCurrencyEntry) — as outras abas ainda não
    // têm arte final, então ShopCardUI desenha um retângulo cinza no lugar do ícone.
    private void BuildItemData()
    {
        // _diamondIconSprite já carregado em Start() (reaproveitado também pelo header/efeito de
        // "diamantes voando" — ver BuildDiamondCounter/SpawnDiamondBurst).
        var coinIcon = Resources.Load<Sprite>("UI/Economy/Coin");
        // Bônus de 1ª compra (+25%, arredondado pra cima) — ver NewDiamondItem. Preço em R$
        // inalterado; só a quantidade entregue na 1ª compra de CADA pacote muda.
        _items[Tab.Diamantes] = new List<ShopItem>
        {
            NewDiamondItem(30, "R$ 4,90", _diamondIconSprite),
            NewDiamondItem(80, "R$ 9,90", _diamondIconSprite),
            NewDiamondItem(170, "R$ 19,90", _diamondIconSprite),
            NewDiamondItem(360, "R$ 39,90", _diamondIconSprite),
            NewDiamondItem(950, "R$ 89,90", _diamondIconSprite),
            NewDiamondItem(2000, "R$ 179,90", _diamondIconSprite),
            NewDiamondItem(4100, "R$ 339,90", _diamondIconSprite),
            NewDiamondItem(6100, "R$ 449,90", _diamondIconSprite),
        };
        // Persistência real (2026-07-21) — pacote já usado nesta conta (ShopStateService.LoadAsync
        // populou PlayerEconomyState.UsedFirstPurchaseBonusAmounts antes de BuildItemData rodar de
        // novo) nasce sem o selo "1ª compra: N diamantes", mesmo efeito de OnBuyClicked depois de
        // usar o bônus pela 1ª vez.
        foreach (var diamondItem in _items[Tab.Diamantes])
        {
            if (!PlayerEconomyState.UsedFirstPurchaseBonusAmounts.Contains(diamondItem.DiamondNormalAmount)) continue;
            diamondItem.FirstPurchaseBonusUsed = true;
            diamondItem.Subtitle = null;
        }

        // Bundle primeiro (pedido do usuário) — some da lista assim que Skip ou 1.5x avulso é
        // comprado primeiro, ver OnBuyClicked/SkipTitle/BoostTitle/BundleTitle. limit: 1 nos 3
        // (pedido do usuário) — cada um é uma compra permanente única, não repetível.
        var bundleItem = NewItem(BundleTitle, "Skip de batalha + Velocidade 1.5x juntos, com desconto.\nPermanente, todos os personagens.", "R$ 14,90", limit: 1);
        var skipItem = NewItem(SkipTitle, "Pula a animação da luta em qualquer combate.\nPermanente, todos os personagens.", "R$ 9,90", limit: 1);
        var boostItem = NewItem(BoostTitle, "Acelera a animação da luta em qualquer combate.\nPermanente, todos os personagens.", "R$ 9,90", limit: 1);

        // Persistência real (2026-07-21) — reconstrói o estado de Desbloqueios a partir do que já
        // foi comprado nesta conta, reaplicando as MESMAS regras de exclusão que OnBuyClicked já
        // usa em resposta a um clique: Bundle só aparece se NENHUM dos dois foi liberado ainda; se
        // exatamente um foi liberado avulso, Bundle não faz mais sentido (não readiciona); se os
        // dois já estão liberados (via Bundle ou via compra avulsa dos dois — indistinguível a
        // partir só dos 2 flags, e sem diferença visual entre os dois casos), mostra o Bundle
        // também esgotado, pra manter a ordem visual de sempre.
        bool skipOwned = PlayerUnlocksState.SkipUnlocked;
        bool boostOwned = PlayerUnlocksState.Speed15xUnlocked;
        skipItem.Purchased = skipOwned ? 1 : 0;
        boostItem.Purchased = boostOwned ? 1 : 0;

        _items[Tab.Desbloqueios] = new List<ShopItem>();
        if (!skipOwned && !boostOwned)
        {
            _items[Tab.Desbloqueios].Add(bundleItem);
        }
        else if (skipOwned && boostOwned)
        {
            bundleItem.Purchased = 1;
            _items[Tab.Desbloqueios].Add(bundleItem);
        }
        _items[Tab.Desbloqueios].Add(skipItem);
        _items[Tab.Desbloqueios].Add(boostItem);

        // Sem limite (2026-07-21, revisado — pedido do usuário: "ainda permitir comprar de novo
        // para acumular dias"). Estado de verdade (tier ativo + dias) mora em PlayerPassState, não
        // em `item.Purchased`/`PurchaseLimit` — ver ApplyPassPurchase/PassStatusOverride.
        _items[Tab.Passes] = new List<ShopItem>
        {
            NewItem(PasseBasicoTitle, "30 dias.\n+1 XP por vitória (todos os personagens).\n8 diamantes/dia · 8 moedas/dia.\nTotal: 240 diamantes.", "R$ 19,90"),
            NewItem(PasseProTitle, "30 dias.\n+2 XP por vitória (todos os personagens).\n18 diamantes/dia · 18 moedas/dia.\nTotal: 540 diamantes.", "R$ 39,90"),
        };

        // limit: 1 em todos (pedido do usuário) — cada slot é uma compra permanente única, não
        // repetível (diferente de Desbloqueios/Passes, que continuam sem limite nesta fase).
        // Trava sequencial (2026-07-21) — ver ProgressionLockState: Slot 2 exige Slot 1, Slot 3
        // exige Slot 2.
        _items[Tab.Progressao] = new List<ShopItem>
        {
            NewItem(Slot1Title, "Level up passa a oferecer 3 opções de skill (normal: 2).\nPermanente, por conta.", "R$ 49,90", limit: 1),
            NewItem(Slot2Title, "Level up passa a oferecer 4 opções de skill.\nPermanente, por conta.", "R$ 99,90", limit: 1),
            NewItem(Slot3Title, "Level up passa a oferecer 5 opções de skill.\nPermanente, por conta.", "R$ 199,90", limit: 1),
        };
        // Persistência real (2026-07-21) — reflete slots já comprados nesta conta; a trava
        // sequencial (ProgressionLockState) já lê `Purchased` do item, então nada mais precisa
        // mudar pra ela continuar funcionando com o dado vindo do Firestore.
        _items[Tab.Progressao][0].Purchased = PlayerProgressionState.Slot1Unlocked ? 1 : 0;
        _items[Tab.Progressao][1].Purchased = PlayerProgressionState.Slot2Unlocked ? 1 : 0;
        _items[Tab.Progressao][2].Purchased = PlayerProgressionState.Slot3Unlocked ? 1 : 0;

        // Cor de acento por raridade (MONETIZACAO.md seção 8) — reaproveita os tokens que já
        // existem em UITheme (rarityRare/rarityLegendary/rarityImmortal), mesmos usados por
        // CharacterCardUI pro fundo do portrait — sem precisar inventar cor nova.
        // Sistema de compra de personagens/case opening (2026-07-23) — preço/limite lidos de
        // CasePackageState (populado por LoadCasePackageStateAsync a partir de casePackages/),
        // com fallback pros mesmos valores de MONETIZACAO.md enquanto isso não carrega ainda (ou
        // se o seed nunca rodou). `Purchased` vem do contador real por jogador
        // (casePurchases/{packageId}), não mais um contador de sessão em memória.
        var rareItem = NewItem("Personagem Raro", "Sorteio entre 19 personagens raros, sem repetição.",
            CasePriceLabel(PackageIdRare, cashFallback: 99.00), limit: CasePurchaseLimit(PackageIdRare, 10), accent: theme.rarityRare);
        rareItem.CasePackageId = PackageIdRare;
        rareItem.Purchased = CasePackageState.PurchasedCounts.TryGetValue(PackageIdRare, out var rareCount) ? rareCount : 0;

        var legendaryItem = NewItem("Personagem Legendary", "Sorteio entre 11 personagens legendary, sem repetição.",
            CasePriceLabel(PackageIdLegendary, cashFallback: 199.00), limit: CasePurchaseLimit(PackageIdLegendary, 3), accent: theme.rarityLegendary);
        legendaryItem.CasePackageId = PackageIdLegendary;
        legendaryItem.Purchased = CasePackageState.PurchasedCounts.TryGetValue(PackageIdLegendary, out var legendaryCount) ? legendaryCount : 0;

        var immortalItem = NewItem("Personagem Imortal", "Sorteio entre 3 personagens imortais, sem repetição.",
            CasePriceLabel(PackageIdImmortal, cashFallback: 249.00), limit: CasePurchaseLimit(PackageIdImmortal, 1), accent: theme.rarityImmortal);
        immortalItem.CasePackageId = PackageIdImmortal;
        immortalItem.Purchased = CasePackageState.PurchasedCounts.TryGetValue(PackageIdImmortal, out var immortalCount) ? immortalCount : 0;

        // "Próximo Personagem" (Coins, 2026-07-26) — substitui o antigo "Case Geral" (diamante,
        // `case_moeda_geral`, removido). Mesmo sorteio ponderado de raridade que o Case Geral já
        // usava (todas as 5 raridades, odds da distribuição real — ver purchaseNextCharacter.ts),
        // só que pago em Coins com preço escalando por CONTADOR DO JOGADOR (não por pacote — ver
        // CharacterSlotCost) em vez de preço fixo. Preço mostrado a partir da contagem REAL
        // (PlayerEconomyState.NextCharacterPurchaseCount, carregada em LoadPersistedShopStateAsync),
        // não mais um contador de sessão.
        var nextCharacterItem = NewItem(CharacterSlotTitle,
            "Sorteio entre TODOS os personagens do jogo, por raridade (odds da distribuição real).\nCusto aumenta a cada compra.",
            $"{CharacterSlotCost(PlayerEconomyState.NextCharacterPurchaseCount + 1)} moedas", coinIcon);
        nextCharacterItem.IsNextCharacterPurchase = true;

        _items[Tab.Personagens] = new List<ShopItem>
        {
            nextCharacterItem,
            rareItem,
            legendaryItem,
            immortalItem,
        };
    }

    private static string CasePriceLabel(string packageId, double cashFallback)
    {
        double cash = CasePackageState.Packages.TryGetValue(packageId, out var info) ? info.CashPrice : cashFallback;
        return $"R$ {cash:F2}".Replace(".", ",");
    }

    private static int CasePurchaseLimit(string packageId, int limitFallback)
    {
        return CasePackageState.Packages.TryGetValue(packageId, out var info) ? info.PurchaseLimitPerPlayer : limitFallback;
    }

    private static ShopItem NewItem(string title, string subtitle, string price, Sprite icon = null, int limit = 0, Color? accent = null)
        => new ShopItem { Title = title, Subtitle = subtitle, PriceLabel = price, Icon = icon, PurchaseLimit = limit, AccentColor = accent };

    // Pacote de diamante com bônus de 1ª compra (+25%, arredondado pra cima — Mathf.CeilToInt
    // bate exatamente com a tabela do pedido do usuário: 30→38, 80→100, 170→213, 360→450,
    // 950→1188, 2000→2500, 4100→5125, 6100→7625). Subtitle mostra o selo "1ª compra: N diamantes"
    // até o pacote ser comprado uma vez (ver OnBuyClicked, que zera o Subtitle depois da 1ª
    // compra); preço em R$ nunca muda, só a quantidade entregue na 1ª compra.
    private static ShopItem NewDiamondItem(int normalAmount, string price, Sprite icon)
    {
        int bonusAmount = Mathf.CeilToInt(normalAmount * 1.25f);
        return new ShopItem
        {
            Title = $"{normalAmount} diamantes",
            Subtitle = $"1ª compra: {bonusAmount} diamantes (+25%)",
            PriceLabel = price,
            Icon = icon,
            DiamondNormalAmount = normalAmount,
            DiamondBonusAmount = bonusAmount,
        };
    }

    // Custo em Coins da N-ésima compra de "Próximo Personagem" (unlockNumber é 1-based: 1ª
    // compra, 2ª compra...). Tabela FINAL (2026-07-26, correção de escopo — substitui a tabela
    // antiga de MONETIZACAO.md §6, 100/200/400/600/800/1000/+400, que valia pro card decorativo
    // anterior). Só pra EXIBIÇÃO — a cobrança real é sempre a de purchaseNextCharacter.ts
    // (PRICE_TABLE lá), mantida em sincronia manual com esta.
    private static readonly int[] CharacterSlotPriceTable = { 25, 50, 100, 200, 400, 800, 1200, 1400, 1600, 1800, 2000, 2200 };
    private const int CharacterSlotPriceStepAfterTable = 200;

    private static int CharacterSlotCost(int unlockNumber) => unlockNumber <= CharacterSlotPriceTable.Length
        ? CharacterSlotPriceTable[unlockNumber - 1]
        : CharacterSlotPriceTable[CharacterSlotPriceTable.Length - 1] + CharacterSlotPriceStepAfterTable * (unlockNumber - CharacterSlotPriceTable.Length);

    // Regra de ativação (pedido do usuário, 2026-07-21 — revisada: Básico e Pro são
    // INDEPENDENTES, não existe mais "tier único"/upgrade entre os dois). Comprar soma +30 dias
    // no contador PRÓPRIO daquele passe, sem tocar no outro — os dois podem estar ativos ao mesmo
    // tempo, cada um com seus próprios 30 dias contando à parte.
    private static void ApplyPassPurchase(bool isPro)
    {
        if (isPro) PlayerPassState.ProDaysRemaining += 30;
        else PlayerPassState.BasicoDaysRemaining += 30;
    }

    // Texto de status do card (aba Passes) — "Ativo — N dias restantes" usando o contador PRÓPRIO
    // daquele passe (Básico e Pro são independentes agora — ver ApplyPassPurchase). `null` quando
    // esse passe específico não está ativo — ShopCardUI cai de volta pro "Comprado Nx (sessão)"
    // genérico.
    private static string PassStatusOverride(string itemTitle)
    {
        int days = itemTitle == PasseProTitle ? PlayerPassState.ProDaysRemaining
            : itemTitle == PasseBasicoTitle ? PlayerPassState.BasicoDaysRemaining
            : 0;
        return days > 0 ? $"Ativo — {days} dias restantes" : null;
    }

    // Trava de progressão sequencial (aba Progressão, pedido do usuário, 2026-07-21) — Slot 2
    // só destrava depois do Slot 1 comprado; Slot 3 só depois do Slot 2. Usa a MESMA variável de
    // progressão já existente (`ShopItem.Purchased` de cada slot, incrementado em OnBuyClicked),
    // sem precisar de estado novo. `null`/`false` (Slot 1, ou qualquer item fora desta aba) nunca
    // fica bloqueado por pré-requisito.
    private (bool locked, string reason) ProgressionLockState(ShopItem item)
    {
        string prereqTitle = item.Title switch
        {
            Slot2Title => Slot1Title,
            Slot3Title => Slot2Title,
            _ => null,
        };
        if (prereqTitle == null) return (false, null);

        bool prereqOwned = _items[Tab.Progressao].Find(i => i.Title == prereqTitle)?.Purchased > 0;
        return prereqOwned ? (false, null) : (true, $"Requer {prereqTitle}");
    }

    // Mensagem de "recompensa diária ainda não implementada" (pedido do usuário, escopo explícito
    // desta tarefa: NÃO implementar o crédito diário de verdade ainda, só avisar) — mesmo popup
    // modal simples usado em outros avisos do projeto (overlay escurecido + painel + texto + OK),
    // construído sob demanda e descartado ao fechar.
    private void ShowPassInfoPopup()
    {
        // Atualizado (2026-07-21) — o bônus de XP por vitória JÁ está funcionando de verdade
        // (AttackSequencer.OnCombatEnd lê PlayerPassState.WinXpBonus a cada vitória); só a coleta
        // diária de diamante/moeda continua pendente.
        ShowInfoPopup("O bônus de XP por vitória do seu passe já está funcionando (some ao XP " +
            "normal de cada vitória, todos os personagens).\n\nA coleta diária de diamantes e " +
            "moedas do passe ainda será liberada em uma atualização futura.");
    }

    // Popup genérico de mensagem+OK (extraído de ShowPassInfoPopup em 2026-07-23 pra ser
    // reaproveitado pelos erros de compra de case - "pool esgotada"/limite atingido/saldo
    // insuficiente, ver HandleCasePurchaseAsync) - mesmo overlay+painel+botão de sempre, só o
    // texto muda por chamador.
    private void ShowInfoPopup(string message)
    {
        var canvasGo = new GameObject("InfoPopupCanvas (temp)");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
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
        overlayImg.color = new Color(0f, 0f, 0f, 0.6f);
        var overlayBtn = overlayGo.AddComponent<Button>();
        overlayBtn.targetGraphic = overlayImg;
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(() => Destroy(canvasGo));

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(640f, 360f);
        panelRt.anchoredPosition = Vector2.zero;
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.sprite = UIShapeUtil.RoundedRect(theme.panelBackgroundAlt, 24f);
        panelImg.type = Image.Type.Sliced;

        var msgGo = new GameObject("Message");
        msgGo.transform.SetParent(panelGo.transform, false);
        var msgRt = msgGo.AddComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0.08f, 0.28f); msgRt.anchorMax = new Vector2(0.92f, 0.92f);
        msgRt.offsetMin = msgRt.offsetMax = Vector2.zero;
        var msgTxt = msgGo.AddComponent<TextMeshProUGUI>();
        msgTxt.text = message;
        msgTxt.fontSize = 26;
        msgTxt.color = theme.textOnDark;
        msgTxt.alignment = TextAlignmentOptions.Center;
        msgTxt.enableWordWrapping = true;

        var okGo = new GameObject("BtnOk");
        okGo.transform.SetParent(panelGo.transform, false);
        var okRt = okGo.AddComponent<RectTransform>();
        okRt.anchorMin = okRt.anchorMax = new Vector2(0.5f, 0.14f);
        okRt.sizeDelta = new Vector2(220f, 64f);
        okRt.anchoredPosition = Vector2.zero;
        var okImg = okGo.AddComponent<Image>();
        okImg.sprite = UIShapeUtil.RoundedRect(theme.primaryAction, 14f);
        okImg.type = Image.Type.Sliced;
        var okBtn = okGo.AddComponent<Button>();
        okBtn.targetGraphic = okImg;
        okBtn.onClick.AddListener(() => Destroy(canvasGo));

        var okLabelGo = new GameObject("Label");
        okLabelGo.transform.SetParent(okGo.transform, false);
        var okLabelRt = okLabelGo.AddComponent<RectTransform>();
        okLabelRt.anchorMin = Vector2.zero; okLabelRt.anchorMax = Vector2.one;
        okLabelRt.offsetMin = okLabelRt.offsetMax = Vector2.zero;
        var okLabelTxt = okLabelGo.AddComponent<TextMeshProUGUI>();
        okLabelTxt.text = "OK";
        okLabelTxt.fontSize = 22;
        okLabelTxt.fontStyle = FontStyles.Bold;
        okLabelTxt.color = theme.textOnDark;
        okLabelTxt.alignment = TextAlignmentOptions.Center;
    }

    private void BuildBackground(Transform parent)
    {
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(parent, false);
        bgGo.transform.SetAsFirstSibling();
        var rt = bgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = bgGo.AddComponent<Image>();
        img.sprite = UIShapeUtil.VerticalGradient(theme.backgroundTop, theme.backgroundBottom);
        img.raycastTarget = false;
    }

    private void BuildHeader(Transform parent)
    {
        var backGo = new GameObject("BtnVoltar");
        backGo.transform.SetParent(parent, false);
        var backRt = backGo.AddComponent<RectTransform>();
        backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
        backRt.pivot = new Vector2(0f, 1f);
        backRt.sizeDelta = new Vector2(160f, 44f);
        backRt.anchoredPosition = new Vector2(30f, -30f);
        BuildButton(backGo, "Voltar", theme.secondaryButton, OnBackClicked);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(parent, false);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(900f, 60f);
        titleRt.anchoredPosition = new Vector2(0f, -26f);
        var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "LOJA";
        titleTxt.fontSize = 36;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = theme.textOnLight;
        titleTxt.alignment = TextAlignmentOptions.Center;
    }

    // Contador de diamante do header (2026-07-21, pedido do usuário) — canto superior direito,
    // mesmo canto/estilo do HUD de moeda do MainMenuController, mas só diamante (a Loja não vende
    // nada com moeda ainda além do card "Próximo Personagem", que já mostra o próprio preço em
    // moeda no card — não precisa de um segundo contador de moeda aqui). Reflete
    // `PlayerEconomyState.Diamonds` (mesmo campo estático usado no resto do app pro HUD de
    // moeda/diamante) — `_diamondIconRt` é o destino real do efeito de "diamantes voando"
    // (SpawnDiamondBurst); `_diamondValueTxt`/`_displayedDiamonds` são atualizados aos poucos
    // conforme cada ícone chega, não de uma vez (ver FlyingDiamondIcon.Burst).
    private void BuildDiamondCounter(Transform parent)
    {
        _displayedDiamonds = PlayerEconomyState.Diamonds;

        var rowGo = new GameObject("DiamondCounter");
        rowGo.transform.SetParent(parent, false);
        var rowRt = rowGo.AddComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(1f, 1f);
        rowRt.sizeDelta = new Vector2(220f, 64f);
        rowRt.anchoredPosition = new Vector2(-30f, -30f);
        var rowBg = rowGo.AddComponent<Image>();
        rowBg.sprite = UIShapeUtil.RoundedRect(new Color(0f, 0f, 0f, 0.35f), 14f);
        rowBg.type = Image.Type.Sliced;

        // Bug real corrigido (2026-07-21): `childControlWidth=false` fazia o layout IGNORAR
        // `LayoutElement.preferredWidth`/`flexibleWidth` dos filhos abaixo (esses campos só
        // afetam a largura real quando `childControlWidth=true` — com false, a largura de cada
        // filho fica presa no valor "cru" do próprio RectTransform, nunca definido explicitamente
        // aqui, então ícone e número renderizavam com largura ~0/invisíveis) — provável causa do
        // usuário reportar "não apareceu o efeito ainda" (o destino do voo existia, mas o próprio
        // ícone do contador nunca aparecia de verdade pra servir de referência visual).
        var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 14, 6, 6);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(rowGo.transform, false);
        _diamondIconRt = iconGo.AddComponent<RectTransform>();
        var iconLe = iconGo.AddComponent<LayoutElement>();
        iconLe.preferredWidth = 52f; iconLe.preferredHeight = 52f;
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = _diamondIconSprite;
        iconImg.preserveAspect = true;

        var valueGo = new GameObject("Value");
        valueGo.transform.SetParent(rowGo.transform, false);
        valueGo.AddComponent<RectTransform>();
        var valueLe = valueGo.AddComponent<LayoutElement>();
        valueLe.preferredWidth = 120f; valueLe.flexibleWidth = 1f;
        _diamondValueTxt = valueGo.AddComponent<TextMeshProUGUI>();
        _diamondValueTxt.text = _displayedDiamonds.ToString();
        _diamondValueTxt.fontSize = 30f;
        _diamondValueTxt.fontStyle = FontStyles.Bold;
        _diamondValueTxt.color = theme.textOnDark;
        _diamondValueTxt.alignment = TextAlignmentOptions.MidlineLeft;
    }

#if UNITY_EDITOR
    // Botão de DEV, só existe em builds de Editor (`#if UNITY_EDITOR`, nunca compilado num build
    // de verdade) — pedido do usuário (2026-07-27) pra facilitar testar "Próximo Personagem" sem
    // precisar jogar/vencer combates só pra acumular Coins. Usa o MESMO WalletService.
    // AddCoinsAsync já usado em qualquer crédito de moeda do jogo (client-writable, sem Cloud
    // Function — Coins nunca teve a mesma regra de segurança de Diamantes, ver WalletService.cs);
    // nenhuma lógica nova, só um atalho de UI pra uma chamada que já existe.
    private const int DevCoinGrantAmount = 5000;

    private void BuildDevCoinButton(Transform parent)
    {
        var btnGo = new GameObject("DevCoinButton (Editor only)");
        btnGo.transform.SetParent(parent, false);
        var btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin = btnRt.anchorMax = new Vector2(1f, 1f);
        btnRt.pivot = new Vector2(1f, 1f);
        btnRt.sizeDelta = new Vector2(220f, 48f);
        btnRt.anchoredPosition = new Vector2(-30f, -100f); // logo abaixo do DiamondCounter (-30,-30, altura 64)
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = UIShapeUtil.RoundedRect(new Color(0.25f, 0.55f, 0.25f, 0.85f), 12f);
        btnImg.type = Image.Type.Sliced;
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);
        var labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;
        var labelTxt = labelGo.AddComponent<TextMeshProUGUI>();
        labelTxt.text = $"DEV: +{DevCoinGrantAmount} coins";
        labelTxt.fontSize = 20f;
        labelTxt.fontStyle = FontStyles.Bold;
        labelTxt.color = Color.white;
        labelTxt.alignment = TextAlignmentOptions.Center;

        btn.onClick.AddListener(() => _ = GrantDevCoinsAsync(labelTxt));
    }

    private async Task GrantDevCoinsAsync(TMP_Text label)
    {
        if (AuthService.IsSignedIn)
        {
            string uid = AuthService.CurrentUser.UserId;
            bool ok = await WalletService.AddCoinsAsync(uid, DevCoinGrantAmount);
            if (!ok)
            {
                Debug.LogError("[Shop][DEV] Falha ao creditar moedas de teste — ver log de WalletService acima.");
                return;
            }
        }
        else
        {
            PlayerEconomyState.Coins += DevCoinGrantAmount;
        }

        Debug.Log($"[Shop][DEV] +{DevCoinGrantAmount} coins (saldo agora: {PlayerEconomyState.Coins}).");
        if (label != null) label.text = $"Saldo: {PlayerEconomyState.Coins} coins";
    }
#endif

    // Dispara o efeito de "diamantes voando" (FlyingDiamondIcon.Burst) do card comprado até o
    // ícone do contador — 5 a 8 ícones (pedido do usuário), cada um carregando uma fração de
    // `credited`; o ÚLTIMO a chegar carrega o resto da divisão, então a soma bate exatamente com
    // `credited` não importa em que ordem os ícones cheguem (a duração/atraso de cada um já varia
    // de propósito, ver FlyingDiamondIcon). `PlayerEconomyState.Diamonds` (o saldo DE VERDADE) já
    // foi incrementado antes desta chamada (ver OnBuyClicked) — esta função só anima o NÚMERO
    // exibido subindo aos poucos até alcançar esse valor real, puramente visual.
    private void SpawnDiamondBurst(ShopCardUI card, int credited)
    {
        // Guard com log (2026-07-21, diagnóstico — usuário reportou "não apareceu o efeito
        // ainda") — antes falhava em silêncio; se algo aqui ainda vier nulo (ex: Diamond.png não
        // resolvendo via Resources.Load por algum motivo de ambiente), o Console agora diz
        // exatamente qual referência está faltando em vez de simplesmente não animar nada.
        if (_diamondIconSprite == null || _diamondValueTxt == null || _diamondIconRt == null || card == null)
        {
            Debug.LogWarning($"[Shop] SpawnDiamondBurst abortado — referência nula (icon={_diamondIconSprite == null}, " +
                $"valueTxt={_diamondValueTxt == null}, iconRt={_diamondIconRt == null}, card={card == null}).");
            return;
        }

        // Sai do BOTÃO "COMPRAR" especificamente (pedido do usuário), não do centro do card
        // inteiro — ver ShopCardUI.BuyButtonWorldPosition.
        Vector3 fromPos = card.BuyButtonWorldPosition;
        Vector3 toPos = _diamondIconRt.position;

        int iconCount = UnityEngine.Random.Range(5, 9); // 5..8 — UnityEngine.Random explícito (ShopController já usa `using System;` pro Enum.GetValues, "Random" sozinho seria ambíguo com System.Random)
        int baseStep = credited / iconCount;
        int remainder = credited - baseStep * iconCount;
        int arrivedCount = 0;

        FlyingDiamondIcon.Burst(_canvasTransform, _diamondIconSprite, fromPos, toPos, iconCount, () =>
        {
            arrivedCount++;
            int step = baseStep + (arrivedCount == iconCount ? remainder : 0);
            _displayedDiamonds += step;
            if (_diamondValueTxt != null) _diamondValueTxt.text = _displayedDiamonds.ToString();
        });
    }

    private void BuildButton(GameObject go, string label, Color color, UnityEngine.Events.UnityAction onClick)
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
        txt.fontStyle = FontStyles.Bold;
        txt.color = theme.textOnDark;
        txt.alignment = TextAlignmentOptions.Center;
    }

    private void OnBackClicked() => StartCoroutine(LoadMainMenuAsync());

    private IEnumerator LoadMainMenuAsync()
    {
        var op = SceneManager.LoadSceneAsync("01_MainMenu");
        while (op != null && !op.isDone) yield return null;
    }

    // Barra lateral vertical (2026-07-20, substitui a barra horizontal de abas — pedido do
    // usuário, "mesmo padrão do menu de loja do Brawl Stars"): 5 botões empilhados via
    // VerticalLayoutGroup, ocupando a coluna esquerda da tela; ScrollView do grid ocupa o
    // restante à direita (ver BuildScrollView). Enum.GetValues devolve na ordem de declaração do
    // enum Tab acima (Diamantes/Desbloqueios/Passes/Progressão/Personagens), que já é a ordem
    // exigida (agora de cima pra baixo em vez de esquerda pra direita).
    private void BuildSidebar(Transform parent)
    {
        var sidebarGo = new GameObject("Sidebar");
        sidebarGo.transform.SetParent(parent, false);
        var sidebarRt = sidebarGo.AddComponent<RectTransform>();
        sidebarRt.anchorMin = new Vector2(SidebarLeft, RegionBottom);
        sidebarRt.anchorMax = new Vector2(SidebarRight, RegionTop);
        sidebarRt.offsetMin = sidebarRt.offsetMax = Vector2.zero;

        var layout = sidebarGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        foreach (Tab tab in Enum.GetValues(typeof(Tab)))
        {
            var btnGo = new GameObject($"Tab_{tab}");
            btnGo.transform.SetParent(sidebarGo.transform, false);
            var img = btnGo.AddComponent<Image>();
            img.sprite = UIShapeUtil.RoundedRect(theme.secondaryButton, 12f);
            img.type = Image.Type.Sliced;
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            var capturedTab = tab;
            btn.onClick.AddListener(() => SelectTab(capturedTab));

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.06f, 0f); lrt.anchorMax = new Vector2(0.94f, 1f);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var txt = labelGo.AddComponent<TextMeshProUGUI>();
            txt.text = TabLabel(tab);
            txt.fontStyle = FontStyles.Bold;
            txt.color = theme.textOnDark;
            txt.alignment = TextAlignmentOptions.Center;
            txt.enableWordWrapping = true;
            // Auto-size — "PROGRESSÃO"/"DESBLOQUEIOS" são bem mais longos que "PASSES"; botões
            // empilhados na coluna são estreitos o bastante pra precisar disso mesmo com wrap.
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 14f;
            txt.fontSizeMax = 30f;

            _tabBackgrounds[tab] = img;
        }
    }

    private static string TabLabel(Tab tab) => tab switch
    {
        Tab.Diamantes => "DIAMANTES",
        Tab.Desbloqueios => "DESBLOQUEIOS",
        Tab.Passes => "PASSES",
        Tab.Progressao => "PROGRESSÃO",
        Tab.Personagens => "PERSONAGENS",
        _ => tab.ToString(),
    };

    private void SelectTab(Tab tab)
    {
        _activeTab = tab;
        foreach (var kv in _tabBackgrounds)
            kv.Value.sprite = UIShapeUtil.RoundedRect(kv.Key == tab ? theme.primaryAction : theme.secondaryButton, 12f);

        RebuildGrid();
    }

    // ScrollView à direita da sidebar, rolando HORIZONTALMENTE (2026-07-20 — era vertical/wrap;
    // pedido do usuário: cards em fileira(s), scroll pra direita). Mesmo truque de
    // ArsenalController.BuildScrollView pro Viewport (RectMask2D + Image quase invisível, permite
    // arrastar em qualquer ponto vazio, não só em cima de um card). Content ancorado
    // esquerda-esticado-verticalmente (anchorMin=(0,0)/anchorMax=(0,1)) — a LARGURA cresce via
    // ContentSizeFitter horizontal (era vertical, quando o grid enrolava linha a linha); a altura
    // já é a altura cheia do Viewport, de graça, por causa do stretch vertical.
    private void BuildScrollView(Transform parent)
    {
        var scrollGo = new GameObject("ScrollView");
        scrollGo.transform.SetParent(parent, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(ContentLeft, RegionBottom);
        scrollRt.anchorMax = new Vector2(ContentRight, RegionTop);
        scrollRt.offsetMin = scrollRt.offsetMax = Vector2.zero;
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;

        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRt = viewportGo.AddComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = viewportRt.offsetMax = Vector2.zero;
        viewportGo.AddComponent<RectMask2D>();
        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = new Color(1f, 1f, 1f, 0.001f);

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 0f);
        contentRt.anchorMax = new Vector2(0f, 1f);
        contentRt.pivot = new Vector2(0f, 0.5f);
        contentRt.sizeDelta = new Vector2(0f, 0f);

        _grid = contentGo.AddComponent<GridLayoutGroup>();
        _grid.spacing = new Vector2(CellSpacing, CellSpacing);
        _grid.padding = new RectOffset(20, 20, 20, 20);
        _grid.childAlignment = TextAnchor.UpperLeft;
        // constraint/constraintCount/cellSize variam por aba (Diamantes = 2 linhas, resto = 1) —
        // setados em RebuildGrid antes de instanciar os cards daquela aba.

        var contentFitter = contentGo.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;

        _contentRoot = contentGo.transform;
    }

    private void RebuildGrid()
    {
        // Desparenta os cards antigos ANTES de destruí-los (bug real corrigido, 2026-07-20 —
        // reportado pelo usuário: "itens esgotados vão indo pra direita, no final da lista").
        // `Destroy()` só remove o GameObject de fato no fim do frame — sem o `SetParent(null)`
        // antes, os cards antigos ainda contavam como filhos de `_contentRoot` no instante em que
        // os cards novos eram adicionados logo abaixo, então `GridLayoutGroup`/`ContentSizeFitter`
        // calculavam a largura do Content com o DOBRO de cards por um instante (antigos + novos
        // coexistindo) — cada clique de compra (cada `RebuildGrid`) inflava um pouco mais a
        // largura calculada, empurrando os cards existentes progressivamente pra direita.
        // `SetParent(null, false)` tem efeito imediato na hierarquia (diferente de `Destroy`),
        // então o Content já fica com 0 filhos de verdade antes do loop de criação começar.
        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
        {
            var oldCard = _contentRoot.GetChild(i).gameObject;
            oldCard.transform.SetParent(null, false);
            Destroy(oldCard);
        }

        // Regra de fileiras por aba (pedido do usuário): Diamantes = 2 linhas (8 pacotes, ~4
        // colunas visíveis + scroll); todas as outras = 1 linha só. GridLayoutGroup.Constraint.
        // FixedRowCount preenche coluna a coluna (de cima pra baixo, depois pra direita) — exatamente
        // o comportamento de "fileira(s) fixas, cresce pra direita" que o scroll horizontal pede.
        bool multiRow = _activeTab == Tab.Diamantes;
        int rows = multiRow ? DiamantesRows : 1;
        float cardW = multiRow ? CardWidthMulti : CardWidthSingle;
        float cardH = multiRow ? CardHeightMulti : CardHeightSingle;

        _grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        _grid.constraintCount = rows;
        _grid.cellSize = new Vector2(cardW, cardH);

        bool isPassesTab = _activeTab == Tab.Passes;
        bool isProgressaoTab = _activeTab == Tab.Progressao;

        foreach (var item in _items[_activeTab])
        {
            var cardGo = new GameObject("Card", typeof(RectTransform));
            cardGo.transform.SetParent(_contentRoot, false);
            var card = cardGo.AddComponent<ShopCardUI>();
            var capturedItem = item;
            // Passes (2026-07-21): "Ativo — N dias restantes" no lugar do "Comprado Nx (sessão)"
            // genérico quando aplicável (ver PassStatusOverride), + badge "i" explicando que a
            // recompensa diária ainda não foi implementada (ver ShowPassInfoPopup). `null`/sem
            // onInfo pras outras 4 abas — comportamento de sempre, inalterado.
            string statusOverride = isPassesTab ? PassStatusOverride(item.Title) : null;
            System.Action onInfo = isPassesTab ? ShowPassInfoPopup : (System.Action)null;
            // Progressão (2026-07-21): Slot 2/3 bloqueados até o slot anterior ser comprado — ver
            // ProgressionLockState. `false`/`null` pras outras 4 abas, nunca bloqueadas por
            // pré-requisito.
            (bool locked, string lockedReason) = isProgressaoTab ? ProgressionLockState(item) : (false, null);
            card.Build(theme, item.Title, item.Subtitle, item.PriceLabel, item.Icon, item.AccentColor,
                item.Purchased, item.PurchaseLimit, cardH, soldOutLabel: null, statusOverride: statusOverride,
                onInfo: onInfo, locked: locked, lockedReason: lockedReason, onBuy: () => OnBuyClicked(capturedItem, card));
        }
    }

    // FASE 1 (placeholder, pedido explícito do usuário): nenhuma gravação real — só log +
    // contador local em memória (ShopItem.Purchased), perdido ao trocar de aba/sair da cena.
    // Inalterado nesta tarefa (só layout mudou) — único Debug.Log deste controller, de propósito
    // (ver Logging Policy em CLAUDE.md).
    private void OnBuyClicked(ShopItem item, ShopCardUI card)
    {
        if (item.PurchaseLimit > 0 && item.Purchased >= item.PurchaseLimit) return;

        // Sistema de compra de personagens/case opening (2026-07-23) — assíncrono (chama a Cloud
        // Function purchaseCase) e só incrementa item.Purchased/abre a roleta depois de confirmar
        // sucesso; sai antes do incremento síncrono genérico abaixo, usado pelos outros 4 tipos
        // de item (que nunca podem falhar depois do clique, diferente deste).
        if (!string.IsNullOrEmpty(item.CasePackageId))
        {
            _ = HandleCasePurchaseAsync(item, card);
            return;
        }

        // "Próximo Personagem" (Coins, 2026-07-26) — mesmo desvio de CasePackageId acima: chama a
        // Cloud Function purchaseNextCharacter (assíncrono), nunca sorteia/debita nada localmente.
        if (item.IsNextCharacterPurchase)
        {
            _ = HandleNextCharacterPurchaseAsync(item, card);
            return;
        }

        item.Purchased++;

        // Pacote de diamante — bônus de 1ª compra (ver NewDiamondItem/ShopItem.
        // FirstPurchaseBonusUsed). DiamondNormalAmount > 0 identifica um item como pacote de
        // diamante (único jeito confiável, já que Title/PriceLabel de outras abas podem colidir
        // em teoria). Log customizado mostra a quantidade REALMENTE creditada (com ou sem bônus),
        // não só o texto do card.
        if (item.DiamondNormalAmount > 0)
        {
            bool grantBonus = !item.FirstPurchaseBonusUsed;
            int credited = grantBonus ? item.DiamondBonusAmount : item.DiamondNormalAmount;
            Debug.Log($"[Shop] Compra (fake, Fase 1) — {item.Title} por {item.PriceLabel} → creditado: {credited} diamantes" +
                (grantBonus ? " (bônus 1ª compra +25%)" : "") + $". Total nesta sessão: {item.Purchased}.");

            // Saldo persistido de verdade (2026-07-21) — grava no MESMO documento users/{uid} que
            // MainMenuController/WalletService já usam pro HUD de moeda/diamante, via
            // WalletService.AddDiamondsAsync (fire-and-forget; espelha PlayerEconomyState.Diamonds
            // assim que o Firestore confirmar). Sem conta logada, mantém o comportamento fake
            // local de sempre (só em memória, perdido ao fechar) — Fase 1, sem bloquear teste sem
            // login. `SpawnDiamondBurst` já lê só `_displayedDiamonds` (contador local da própria
            // animação), não `PlayerEconomyState.Diamonds` diretamente — funciona igual não
            // importa se o valor real chega de forma síncrona (offline) ou assíncrona (Firestore).
            // TODO SEGURANÇA (ARQUITETURA.md "Moeda premium"): WalletService.AddDiamondsAsync
            // ainda é client-writable (sem Cloud Function) — compra com dinheiro real de verdade
            // exigiria validação server-side do recibo ANTES de creditar qualquer diamante; fica
            // pra quando a Fase 4/Fase 8 ganhar o gateway de pagamento e as functions de verdade.
            if (AuthService.IsSignedIn)
            {
                string uid = AuthService.CurrentUser.UserId;
                _ = WalletService.AddDiamondsAsync(uid, credited);
                if (grantBonus) _ = ShopStateService.MarkPackageFirstPurchaseUsedAsync(uid, item.DiamondNormalAmount);
            }
            else
            {
                PlayerEconomyState.Diamonds += credited;
            }
            SpawnDiamondBurst(card, credited);

            if (grantBonus)
            {
                // Selo "1ª compra: N diamantes" some do card depois de usado — precisa reconstruir
                // (RefreshPurchaseState sozinho só atualiza status/botão, não o Subtitle).
                item.FirstPurchaseBonusUsed = true;
                item.Subtitle = null;
                RebuildGrid();
                return;
            }

            card.RefreshPurchaseState(item.Purchased, item.PurchaseLimit);
            return;
        }

        Debug.Log($"[Shop] Compra (fake, Fase 1) — {item.Title} por {item.PriceLabel}. Total nesta sessão: {item.Purchased}.");

        // Desbloqueio de verdade (pedido do usuário) — grava em PlayerUnlocksState assim que
        // Skip/1.5x/Bundle é comprado; CombatHUD lê esses campos ao montar os botões de Skip/1.5x
        // da tela de combate. Roda incondicionalmente aqui (antes da regra de o Bundle sumir/
        // desabilitar os outros dois logo abaixo), porque precisa acontecer nas 3 compras
        // possíveis, não só na que aciona a remoção/desabilitação visual do card irmão.
        // Persistência real (2026-07-21) — ShopStateService grava no Firestore E já espelha em
        // PlayerUnlocksState por dentro (mesmo padrão de WalletService); sem conta, mantém o
        // fake local de sempre.
        bool signedIn = AuthService.IsSignedIn;
        string authUid = signedIn ? AuthService.CurrentUser.UserId : null;
        if (item.Title == SkipTitle)
        {
            if (signedIn) _ = ShopStateService.SetSkipUnlockedAsync(authUid);
            else PlayerUnlocksState.SkipUnlocked = true;
        }
        else if (item.Title == BoostTitle)
        {
            if (signedIn) _ = ShopStateService.SetSpeed15xUnlockedAsync(authUid);
            else PlayerUnlocksState.Speed15xUnlocked = true;
        }
        else if (item.Title == BundleTitle)
        {
            if (signedIn) _ = ShopStateService.SetBundleUnlockedAsync(authUid);
            else { PlayerUnlocksState.SkipUnlocked = true; PlayerUnlocksState.Speed15xUnlocked = true; }
        }

        // Bundle some da lista assim que Skip ou 1.5x avulso é comprado primeiro (pedido do
        // usuário) — reconstrói o grid inteiro em vez de só remover o card do Bundle "no meio"
        // (mais simples, e o card do próprio item comprado já sai reconstruído com o Purchased
        // atualizado, então não precisa do RefreshPurchaseState avulso abaixo nesse caminho).
        if (_activeTab == Tab.Desbloqueios && (item.Title == SkipTitle || item.Title == BoostTitle))
        {
            var list = _items[Tab.Desbloqueios];
            int bundleIndex = list.FindIndex(i => i.Title == BundleTitle);
            if (bundleIndex >= 0)
            {
                list.RemoveAt(bundleIndex);
                RebuildGrid();
                return;
            }
        }

        // Direção oposta (pedido do usuário): comprar o Bundle desabilita Skip e 1.5x avulsos —
        // já estão cobertos pelo combo, não fazem mais sentido como compra separada. Diferente da
        // regra acima (o Bundle SOME da lista), aqui os dois cards continuam visíveis, só marcados
        // como esgotados (`Purchased = PurchaseLimit`, mesmo estado "ESGOTADO"/desabilitado que
        // qualquer item com limite já usa) — pedido foi "desabilitar", não "sumir".
        if (item.Title == BundleTitle)
        {
            var list = _items[Tab.Desbloqueios];
            var skip = list.Find(i => i.Title == SkipTitle);
            var boost = list.Find(i => i.Title == BoostTitle);
            if (skip != null) skip.Purchased = Mathf.Max(skip.Purchased, skip.PurchaseLimit);
            if (boost != null) boost.Purchased = Mathf.Max(boost.Purchased, boost.PurchaseLimit);
            RebuildGrid();
            return;
        }

        // Passe mensal comprado (pedido do usuário, 2026-07-21 — revisado: Básico e Pro são
        // independentes, cada compra soma +30 dias no contador PRÓPRIO daquele passe, sem afetar
        // o outro) — ver ApplyPassPurchase/PlayerPassState. Sem `limit` nenhum (ver BuildItemData)
        // — comprar de novo é o fluxo normal, não uma exceção. Persistência real (2026-07-21):
        // ShopStateService.AddPassDaysAsync já soma em PlayerPassState por dentro (mesmo padrão
        // dos outros), então ApplyPassPurchase só roda no fallback sem conta.
        if (item.Title == PasseBasicoTitle || item.Title == PasseProTitle)
        {
            bool isPro = item.Title == PasseProTitle;
            if (AuthService.IsSignedIn) _ = ShopStateService.AddPassDaysAsync(AuthService.CurrentUser.UserId, isPro, 30);
            else ApplyPassPurchase(isPro);
            RebuildGrid();
            return;
        }

        // Progressão — comprar um slot pode destravar o PRÓXIMO (Slot 1 destrava Slot 2, Slot 2
        // destrava Slot 3, ver ProgressionLockState) — reconstrói o grid inteiro pra refletir isso
        // na hora, sem precisar reabrir a aba (pedido explícito do usuário).
        // Grava em PlayerProgressionState (2026-07-21, mesmo padrão de PlayerUnlocksState acima) —
        // sobrevive à troca de cena; CombatResultPanel.ShowLevelUpChoice lê
        // PlayerProgressionState.LevelUpBoxCount() pra decidir quantas caixas de bônus oferecer
        // (2 base + 1 por slot comprado, até 5).
        if (item.Title == Slot1Title || item.Title == Slot2Title || item.Title == Slot3Title)
        {
            int slotIndex = item.Title == Slot1Title ? 1 : item.Title == Slot2Title ? 2 : 3;
            // Persistência real (2026-07-21): ShopStateService.UnlockProgressionSlotAsync já
            // grava PlayerProgressionState.Slot{N}Unlocked por dentro; sem conta, mantém o fake
            // local de sempre.
            if (AuthService.IsSignedIn) _ = ShopStateService.UnlockProgressionSlotAsync(AuthService.CurrentUser.UserId, slotIndex);
            else
            {
                if (slotIndex == 1) PlayerProgressionState.Slot1Unlocked = true;
                else if (slotIndex == 2) PlayerProgressionState.Slot2Unlocked = true;
                else PlayerProgressionState.Slot3Unlocked = true;
            }

            RebuildGrid();
            return;
        }

        card.RefreshPurchaseState(item.Purchased, item.PurchaseLimit);
    }

    // Sistema de compra de personagens/case opening (2026-07-23) — chama a Cloud Function
    // purchaseCase (CaseService), nunca sorteia/decrementa nada localmente. Em sucesso, abre
    // CaseOpeningPopup com o resultado já decidido pelo servidor; em erro, mostra a mensagem certa
    // (ShowInfoPopup) sem abrir a roleta — "pool esgotada"/limite atingido/saldo insuficiente.
    private async Task HandleCasePurchaseAsync(ShopItem item, ShopCardUI card)
    {
        if (!AuthService.IsSignedIn)
        {
            ShowInfoPopup("É necessário estar logado para comprar personagens.");
            return;
        }

        // Cash (Raro/Legendary/Imortal — os 3 únicos CasePackageId que sobraram depois da remoção
        // do Case Geral/diamante em 2026-07-26) exige um "recibo" — mock por enquanto (sem
        // gateway de pagamento real, ver MONETIZACAO.md/ARQUITETURA.md "Moeda premium"). A Cloud
        // Function já está pronta pra validar de verdade (TODO explícito em purchaseCase.ts); só
        // o que entra aqui muda quando o gateway (Google Play Billing/Apple StoreKit) existir.
        string mockReceipt = $"mock-receipt-{Guid.NewGuid()}";

        var result = await CaseService.PurchaseCaseAsync(item.CasePackageId, mockReceipt);
        if (!result.Success)
        {
            string message = (result.ErrorMessage ?? "").Contains("pool esgotada")
                ? "Você já possui todos os personagens desta raridade — pool esgotada."
                : result.ErrorCode == "resource-exhausted"
                    ? "Limite de compras atingido para este pacote."
                    : result.ErrorCode == "failed-precondition"
                        ? "Saldo de diamantes insuficiente."
                        : "Não foi possível completar a compra. Tente novamente.";
            ShowInfoPopup(message);
            return;
        }

        // Diagnóstico (2026-07-25, investigando "Continuar não abre o detalhe do personagem") —
        // grantedCharacterId só existe na resposta de purchaseCase desde uma rodada anterior
        // desta mesma tarefa; se a Cloud Function implantada AINDA for a versão de antes dessa
        // mudança (functions/src/purchaseCase.ts editado localmente, mas nunca reimplantado via
        // `firebase deploy --only functions`), a resposta não teria essa chave —
        // `dict["grantedCharacterId"] as string` (CaseService.cs) não lança exceção nesse caso
        // (IDictionary não-genérico devolve null pra chave ausente, não lança), então a compra
        // continua "bem-sucedida" (reveal funciona normalmente, usa wonCharacterTypeId) mas
        // PendingCharacterSelection nunca é setado — CharacterSelectController nunca vê nenhuma
        // seleção pendente e cai na grade normal, SEM nenhum erro em lugar nenhum. Este log
        // confirma/descarta essa hipótese de forma inequívoca.
        if (string.IsNullOrEmpty(result.GrantedCharacterId))
        {
            Debug.LogError("[ShopController] purchaseCase respondeu sem 'grantedCharacterId' - a Cloud Function implantada provavelmente ainda é uma versão antiga (rodar `firebase deploy --only functions` depois de functions/src/purchaseCase.ts ter sido atualizado). 'Continuar' não vai conseguir abrir o detalhe do personagem concedido sem esse campo.");
        }

        item.Purchased++;
        CasePackageState.PurchasedCounts[item.CasePackageId] = item.Purchased;
        card.RefreshPurchaseState(item.Purchased, item.PurchaseLimit);

        // Cash não debita diamante nenhum (pagamento já validado via recibo) — os 3 cards
        // restantes (Raro/Legendary/Imortal) são todos cash, então não há mais nenhum débito de
        // diamante a espelhar aqui (Case Geral, o único pago em diamante, foi removido).
        // weightedRarityFill: false — cada card é travado numa raridade ÚNICA (isRarityLocked),
        // giro só daquela raridade é o comportamento correto (ver comentário em
        // CaseOpeningPopup.Show).
        CaseOpeningPopup.Show(theme, characterDatabase, result.ReelPoolCharacterTypeIds, result.WonCharacterTypeId,
            result.GrantedCharacterId, onClosed: null, weightedRarityFill: false);
    }

    // "Próximo Personagem" (Coins, 2026-07-26) — chama a Cloud Function purchaseNextCharacter
    // (NextCharacterService), nunca sorteia/debita nada localmente. Mesmo formato de
    // HandleCasePurchaseAsync acima, adaptado pro preço escalar por CONTADOR DO JOGADOR em vez de
    // por pacote fixo.
    private async Task HandleNextCharacterPurchaseAsync(ShopItem item, ShopCardUI card)
    {
        if (!AuthService.IsSignedIn)
        {
            ShowInfoPopup("É necessário estar logado para comprar personagens.");
            return;
        }

        var result = await NextCharacterService.PurchaseNextCharacterAsync();
        if (!result.Success)
        {
            string message = (result.ErrorMessage ?? "").Contains("pool esgotada")
                ? "Você já possui todos os personagens do jogo — pool esgotada."
                : result.ErrorCode == "failed-precondition"
                    ? "Saldo de moedas insuficiente."
                    : "Não foi possível completar a compra. Tente novamente.";
            ShowInfoPopup(message);
            return;
        }

        // Moeda já foi debitada no servidor (dentro da transaction de purchaseNextCharacter) — só
        // espelha localmente pro HUD de moeda não esperar um reload de PlayerEconomyState.
        PlayerEconomyState.Coins = result.NewCoinsBalance;
        PlayerEconomyState.NextCharacterPurchaseCount++;

        item.PriceLabel = $"{result.NextPurchaseCost} moedas";
        RebuildGrid();

        // weightedRarityFill: true (2026-07-27) — Próximo Personagem sorteia entre as 5
        // raridades, então o giro deve misturar raridades também (diferente dos 3 cards cash
        // acima, cada um travado numa raridade única — ver comentário em CaseOpeningPopup.Show).
        CaseOpeningPopup.Show(theme, characterDatabase, result.ReelPoolCharacterTypeIds, result.WonCharacterTypeId,
            result.GrantedCharacterId, onClosed: null, weightedRarityFill: true);
    }
}
