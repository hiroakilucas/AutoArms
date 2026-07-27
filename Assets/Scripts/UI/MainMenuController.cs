using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;
    [SerializeField] private UITheme theme;
    [Tooltip("Necessário pro botão REPLAYS do CharacterPanel reconstruir o adversário de um replay salvo (ReplaySnapshotConverter) — arraste Assets/ScriptableObjects/Databases/CharacterDatabase.asset aqui.")]
    [SerializeField] private CharacterDatabase characterDatabase;

    // Balanceamento do gate de energia (2026-07-19) — carregado de Assets/Resources/
    // EnergySettings.asset via Resources.Load (mesmo padrão de SelectedProfileHolder/
    // SelectedOpponentHolder morarem em Resources/), evitando precisar wirear mais um campo
    // [SerializeField] direto na cena só pra isto.
    private EnergySettings _energySettings;

    // HUD de moeda/diamante (2026-07-20, redesenho mobile — pedido do usuário: mover do canto
    // superior esquerdo do CharacterPanel pro canto superior direito da TELA, padrão de HUD
    // mobile, ícone+fonte dobrados). Independente do CharacterPanel agora — ver BuildCurrencyHud.
    private TMP_Text _hudCoinText, _hudDiamondText;

    // Exposto pra MainMenuCharacterPreview conseguir buscar o UITheme via FindObjectOfType,
    // sem precisar de um campo [SerializeField] próprio — ver comentário em
    // MainMenuCharacterPreview.ResolveTheme() pra motivo (campo próprio já quebrou 2x, sempre
    // resolvendo nulo em runtime apesar do asset estar wireado corretamente no arquivo de cena).
    public UITheme Theme => theme;

    void Start()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _energySettings = Resources.Load<EnergySettings>("EnergySettings");

        // Bug real corrigido (2026-07-25, reportado pelo usuário — "quando eu paro a aplicação e
        // starto novamente no Unity, o primeiro personagem não aparece selecionado na main menu",
        // além do NullReferenceException original em CharacterPanel.RefreshAll ao reabrir o app)
        // — `SelectedProfileHolder` é um ScriptableObject ASSET; a Unity reverte QUALQUER mutação
        // feita nele durante o Play Mode assim que ele para (comportamento SEMPRE existiu, não é
        // ligado ao Domain Reload) — `currentProfile`/`characterId` voltam pro default serializado
        // do asset (hoje `Medieval Warrior`/vazio) toda vez que uma sessão nova começa, mesmo pra
        // uma conta que já tem personagem de verdade escolhido. A checagem antiga (só
        // `currentProfile == null`) quase nunca era verdadeira (o default do asset não é nulo, é
        // só o personagem ERRADO) — por isso raramente disparava. `PlayerProfileConverter.
        // EnsureValidSelection` (compartilhado com `LoginController.OnAuthSuccessRoutine`, que
        // cobre o fluxo normal via `00_Login`) cobre também o caso de abrir o Play Mode direto
        // nesta cena (comum ao iterar em UI no Editor, pulando o login) — busca o roster de novo
        // se logado e corrige `currentProfile` antes de qualquer coisa depender dele abaixo.
        if (selectedProfileHolder != null && AuthService.IsSignedIn)
        {
            var owned = await RosterService.ListOwnedCharacterDocsAsync(AuthService.CurrentUser.UserId);
            PlayerProfileConverter.EnsureValidSelection(selectedProfileHolder, owned, characterDatabase);
        }

        // Restaura o save local (2026-07-14, ver LocalSaveService.cs) ANTES de qualquer leitura
        // dos campos do profile abaixo — sem isso, num build real, o CharacterPanel mostraria
        // sempre os valores do asset original (level/XP zerados de novo a cada abertura do jogo).
        if (selectedProfileHolder != null)
            LocalSaveService.ApplyIfSaved(selectedProfileHolder.currentProfile);

        // CharacterPanel (2026-07-20, redesenho mobile — 2ª rodada): em vez de um componente
        // separado reimplementando nome/HP/STR-AGI-SPD/skills/armas/pets do zero (tentativa
        // anterior, MobileCharacterDrawer — removida, ficou visualmente ruim e não tinha
        // skills/armas/pets), o CharacterPanel de sempre ganhou um modo `bottomAnchored` que
        // reaproveita 100% da lógica já existente (Compact/Expanded/Skills/Armas/Pets/Passivas),
        // só trocando a geometria de ancoragem: painel de largura fixa (PanelWidth) centralizado
        // horizontalmente, ancorado no rodapé, crescendo pra cima ao expandir — em vez do painel
        // vertical do lado direito usado por 02_SelectCharacter. Ver CharacterPanel.cs.
        var go = new GameObject("CharacterPanel");
        var characterPanel = go.AddComponent<CharacterPanel>();
        characterPanel.Setup(selectedProfileHolder, theme, characterDatabase: characterDatabase, bottomAnchored: true);

        BuildLogoutButton();
        ReanchorLeftColumnToBottomLeft();
        BuildReplaysMenuButton(characterPanel);
        BuildLojaMenuButton();
        AlignLeftColumnWithJogar();
        BuildCurrencyHud();

        RefreshEconomyOnMenuLoad();

        // Resiliência do level-up de combate (2026-07-25, bug real corrigido) — se o app fechou
        // com a tela "Escolha 1 bônus" aberta antes do jogador decidir, reabre a MESMA tela agora
        // (mesmo profile, mesmas caixas já sorteadas) em vez de deixar a escolha perder-se pra
        // sempre. Chamado por último, depois de CharacterPanel já existir (FindScreenCanvas
        // precisa de algum Canvas ScreenSpaceOverlay já presente na cena) — o overlay opaco da
        // tela de escolha bloqueia sozinho qualquer clique em Jogar/Chibers/etc. atrás dele, sem
        // precisar desabilitar cada botão manualmente. Ver CombatResultPanel.
        // ResumePendingLevelUpChoiceIfAny/AttackSequencer.OnCombatEnd.
        if (selectedProfileHolder != null)
            CombatResultPanel.ResumePendingLevelUpChoiceIfAny(selectedProfileHolder.currentProfile, theme);
    }

    // Moeda/diamante — canto superior direito da TELA (2026-07-20, redesenho mobile).
    // Envolvido em SafeArea (novo, ver Assets/Scripts/UI/SafeArea.cs) — canto de tela é
    // exatamente onde notch/câmera-furo mais atrapalham.
    // 2026-07-20 (2ª rodada): ícone+fonte escalados em x1.5 (104px/36pt → 156px/54pt).
    // 2026-07-20 (3ª rodada): usuário pediu de volta lado a lado (moeda esquerda, diamante
    // direita) — a 2ª rodada tinha empilhado verticalmente, revertido aqui; a escala 1.5x foi
    // mantida.
    // 2026-07-20 (4ª rodada): posições exatas lidas pelo usuário direto no Editor (ajuste fino
    // manual do Row/Coin/Diamante em Play) — `BuildCurrencyEntry` deixou de derivar a posição do
    // texto a partir de `x + iconSize + gap` (a fórmula não reproduzia os valores manuais, que
    // não são uniformes entre os 2 blocos) e passou a receber a posição de ícone/valor já prontas.
    private void BuildCurrencyHud()
    {
        if (theme == null) return;

        var canvasGo = new GameObject("CurrencyHud");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var safeAreaGo = new GameObject("SafeArea");
        safeAreaGo.transform.SetParent(canvasGo.transform, false);
        var safeRt = safeAreaGo.AddComponent<RectTransform>();
        safeRt.anchorMin = Vector2.zero; safeRt.anchorMax = Vector2.one;
        safeRt.offsetMin = safeRt.offsetMax = Vector2.zero;
        safeAreaGo.AddComponent<SafeArea>();

        const float iconSize = 156f;  // 2026-07-20: 104→156 (x1.5, pedido do usuário)
        const float fontSize = 54f;   // 36→54 (x1.5)
        const float valueWidth = 195f; // 130×1.5

        var rowGo = new GameObject("Row");
        rowGo.transform.SetParent(safeAreaGo.transform, false);
        var rowRt = rowGo.AddComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(1f, 1f);
        rowRt.sizeDelta = new Vector2(670f, 146.346f);
        rowRt.anchoredPosition = new Vector2(-6.099976f, -6.099976f);

        _hudCoinText = BuildCurrencyEntry(rowGo, "UI/Economy/Coin",
            iconPos: new Vector2(0f, 0f), valuePos: new Vector2(142f, 4.827f),
            iconSize: iconSize, valueWidth: valueWidth, fontSize: fontSize,
            valueBgPos: new Vector2(77f, 4.827003f), valueBgWidth: 245f);
        _hudDiamondText = BuildCurrencyEntry(rowGo, "UI/Economy/Diamond",
            iconPos: new Vector2(322f, 4.827f), valuePos: new Vector2(475f, 4.827f),
            iconSize: iconSize, valueWidth: valueWidth, fontSize: fontSize,
            valueBgPos: new Vector2(406f, 4.827f), valueBgWidth: 245f);
    }

    private TMP_Text BuildCurrencyEntry(GameObject parent, string spritePath, Vector2 iconPos, Vector2 valuePos, float iconSize, float valueWidth, float fontSize, Vector2 valueBgPos, float valueBgWidth)
    {
        // Fundo preto semi-opaco atrás do número (2026-07-20, pedido do usuário — melhora a
        // legibilidade do valor sobre o fundo variável da cena, mesmo espírito do chip da
        // energia). Criado ANTES do ícone (sibling anterior = desenha atrás de tudo, inclusive
        // do próprio ícone da moeda/diamante — pedido do usuário, 2026-07-20).
        const float valueBgHeight = 78f; // ~1.4× fontSize — hug do texto, não a altura cheia do ícone
        var valueBgGo = new GameObject("ValueBg");
        valueBgGo.transform.SetParent(parent.transform, false);
        var valueBgRt = valueBgGo.AddComponent<RectTransform>();
        valueBgRt.anchorMin = valueBgRt.anchorMax = new Vector2(0f, 0.5f);
        valueBgRt.pivot = new Vector2(0f, 0.5f);
        valueBgRt.sizeDelta = new Vector2(valueBgWidth, valueBgHeight);
        valueBgRt.anchoredPosition = valueBgPos;
        var valueBgImg = valueBgGo.AddComponent<Image>();
        valueBgImg.sprite = UIShapeUtil.RoundedRect(new Color(0f, 0f, 0f, 0.55f), 12f);
        valueBgImg.type = Image.Type.Sliced;

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(parent.transform, false);
        var irt = iconGo.AddComponent<RectTransform>();
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.sizeDelta = new Vector2(iconSize, iconSize);
        irt.anchoredPosition = iconPos;
        var img = iconGo.AddComponent<Image>();
        img.sprite = Resources.Load<Sprite>(spritePath);
        img.preserveAspect = true;

        var txtGo = new GameObject("Value");
        txtGo.transform.SetParent(parent.transform, false);
        var trt = txtGo.AddComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
        trt.pivot = new Vector2(0f, 0.5f);
        trt.sizeDelta = new Vector2(valueWidth, iconSize);
        trt.anchoredPosition = valuePos;
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyles.Bold;
        txt.color = theme.textOnDark;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.outlineWidth = 0.2f;
        txt.outlineColor = theme.panelBackground;
        return txt;
    }

    // Busca moeda/diamante/energia toda vez que o menu carrega (2026-07-20, bug real corrigido —
    // o timer "próxima energia"/os ícones ficavam sempre em branco quando o menu era aberto sem
    // passar pelo login: LoginController.LoadEconomyRoutine só roda em 00_Login, e antes disso
    // PlayerEconomyState fica no valor default de fábrica até o jogador clicar Jogar pela 1ª vez
    // — cenário comum ao testar 01_MainMenu direto no Editor, sem passar pela cena de login).
    // `async void` disparado uma vez em Start, mesmo padrão de OnPlayButton — falha vira só um
    // log dentro dos próprios serviços, nunca trava o menu.
    // Público (2026-07-20, bug real corrigido — pedido do usuário: "o timer ficou travado em
    // 0:00:00 e a energia não incrementou") — além do Start, agora também é chamado por
    // `MainMenuCharacterPreview.BuildEnergyTimer` (via `CountdownLabel.onDone`, ver
    // `PlayerEconomyState.EnergyCountdownAtZero`) toda vez que o countdown local chega em zero,
    // forçando o mesmo re-sync com o Firestore que antes só rodava no carregamento do menu.
    public async void RefreshEconomyOnMenuLoad()
    {
        if (!AuthService.IsSignedIn || selectedProfileHolder?.currentProfile == null || _energySettings == null)
            return;

        string uid = AuthService.CurrentUser.UserId;
        string characterId = selectedProfileHolder.currentProfile.OpponentId();

        Task<(int coins, int diamonds)> walletTask = WalletService.LoadAsync(uid);
        Task<(int current, int max)> energyTask = EnergyService.GetOrRegenAsync(uid, characterId, _energySettings);
        // Bug real corrigido (2026-07-21) — mesmo motivo do hook em LoginController.
        // LoadEconomyRoutine: sem isto, quem abre 01_MainMenu direto no Editor (sem passar por
        // 00_Login) nunca carrega desbloqueios/passe/progressão nenhuma vez na sessão.
        Task shopStateTask = ShopStateService.LoadAsync(uid);
        await Task.WhenAll(walletTask, energyTask, shopStateTask);

        var (coins, diamonds) = walletTask.Result;
        var (energyCurrent, energyMax) = energyTask.Result;
        PlayerEconomyState.Set(coins, diamonds, energyCurrent, energyMax);

        RefreshEconomyHuds();
    }

    // Botão "REPLAYS" (2026-07-18, pedido do usuário — movido de dentro do CharacterPanel pra cá)
    // — mesma coluna de atalhos de "Chibers" (Btn_SelectCharacter) e "Arsenal" (Btn_Arsenal), logo
    // abaixo deste último. Posicionado por código a partir do próprio Btn_Arsenal já colocado na
    // cena (GameObject.Find, mesmo padrão de CombatSceneLoader.RandomizeArenaBackground) em vez de
    // coordenadas fixas — sobrevive a um reposicionamento futuro da coluna inteira sem precisar
    // editar este script. Visual replica CharacterCardButtonStyle manualmente (ícone placeholder +
    // faixa de label + sombra) em vez de reaproveitar o componente: CharacterCardButtonStyle lê
    // `theme`/`iconOverride` de campos `[SerializeField] private`, só wireáveis pelo Inspector em
    // GameObjects já existentes na cena — não dava pra setar isso num GameObject criado agora via
    // código sem expor uma API nova nele.
    private void BuildReplaysMenuButton(CharacterPanel characterPanel)
    {
        if (theme == null || characterPanel == null) return;

        var arsenalGo = GameObject.Find("Btn_Arsenal");
        if (arsenalGo == null) return;
        var arsenalRt = arsenalGo.GetComponent<RectTransform>();
        if (arsenalRt == null) return;

        // Mesmo espaçamento vertical já usado entre "Chibers" e "Arsenal" nessa coluna (ambos
        // filhos do mesmo pai) — a diferença de anchoredPosition.y entre os dois já é o "gap"
        // certo a repetir. Fallback (211.87px, ver CLAUDE.md) só se Btn_SelectCharacter não for
        // achado por algum motivo.
        float gapY = 211.87f;
        var chibersGo = GameObject.Find("Btn_SelectCharacter");
        if (chibersGo != null)
        {
            var chibersRt = chibersGo.GetComponent<RectTransform>();
            if (chibersRt != null) gapY = chibersRt.anchoredPosition.y - arsenalRt.anchoredPosition.y;
        }

        var btnGo = new GameObject("Btn_Replays");
        btnGo.transform.SetParent(arsenalRt.parent, false);
        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = arsenalRt.anchorMin;
        rt.anchorMax = arsenalRt.anchorMax;
        rt.pivot = arsenalRt.pivot;
        rt.sizeDelta = arsenalRt.sizeDelta;
        rt.anchoredPosition = arsenalRt.anchoredPosition - new Vector2(0f, gapY);

        var bg = btnGo.AddComponent<Image>();
        bg.sprite = UIShapeUtil.RoundedRect(theme.secondaryButton, 16f);
        bg.type = Image.Type.Sliced;
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(characterPanel.ShowReplays);

        // Ícone placeholder — sem arte dedicada pra "Replays" ainda, mesmo tom que
        // CharacterCardButtonStyle usa quando nenhum iconOverride está configurado.
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(btnGo.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.08f, 0.30f);
        iconRt.anchorMax = new Vector2(0.92f, 0.92f);
        iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = UIShapeUtil.RoundedRect(Color.Lerp(theme.secondaryButton, Color.white, 0.35f), 10f);
        iconImg.type = Image.Type.Sliced;

        var stripGo = new GameObject("LabelStrip");
        stripGo.transform.SetParent(btnGo.transform, false);
        var stripRt = stripGo.AddComponent<RectTransform>();
        stripRt.anchorMin = new Vector2(0f, 0f);
        stripRt.anchorMax = new Vector2(1f, 0.30f);
        stripRt.offsetMin = Vector2.zero; stripRt.offsetMax = Vector2.zero;
        var stripImg = stripGo.AddComponent<Image>();
        stripImg.sprite = UIShapeUtil.RoundedRect(theme.panelBackgroundAlt, 16f);
        stripImg.type = Image.Type.Sliced;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(stripGo.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var labelTxt = labelGo.AddComponent<TextMeshProUGUI>();
        // **Bug real corrigido (2026-07-20)**: mesmo com fontStyle/cor/outline idênticos aos de
        // CHIBERS/ARSENAL, o texto do REPLAY renderizava mais fino e com o contorno menos visível
        // — causa real: `AddComponent<TextMeshProUGUI>()` cria o texto com a fonte PADRÃO do TMP
        // (LiberationSans SDF), enquanto os labels de CHIBERS/ARSENAL (pré-colocados na cena, ver
        // CharacterCardButtonStyle) usam um TMP_FontAsset customizado ("LuckiestGuy-Regular SDF").
        // 1ª tentativa de fix (só `labelTxt.font = arsenalLabel.font`, DEPOIS de já ter setado
        // `.text` e o resto) quebrou de vez — texto virou "rabisco" (glyphs errados/embaralhados).
        // Causa real do rabisco: `fontSharedMaterial` continuou apontando pro material/atlas da
        // fonte ANTIGA (LiberationSans) enquanto os glifos passaram a ser buscados na fonte NOVA —
        // atlas e UVs incompatíveis. Fix de verdade: copia `font` E `fontSharedMaterial` do label
        // de ARSENAL já existente na cena (mesmo GameObject achado acima, `arsenalGo`), ANTES de
        // setar `.text`/qualquer outra propriedade (a fonte/material precisam estar corretos ANTES
        // do texto ser gerado, não depois) — garante a MESMA fonte E o MESMO material/atlas, não
        // só os mesmos parâmetros de estilo.
        var arsenalLabel = arsenalGo.GetComponentInChildren<TMP_Text>(true);
        if (arsenalLabel != null)
        {
            labelTxt.font = arsenalLabel.font;
            labelTxt.fontSharedMaterial = arsenalLabel.fontSharedMaterial;
        }
        labelTxt.text = "REPLAY"; // 2026-07-20, pedido do usuário — era "REPLAYS" (plural)
        labelTxt.fontStyle = FontStyles.Bold;
        labelTxt.color = theme.textOnDark;
        labelTxt.outlineWidth = 0.2f;
        labelTxt.outlineColor = theme.panelBackground;
        labelTxt.enableAutoSizing = true;
        labelTxt.fontSizeMin = 10f;
        labelTxt.fontSizeMax = 40f;
        labelTxt.alignment = TextAlignmentOptions.Center;
        labelTxt.ForceMeshUpdate();

        btnGo.AddComponent<UIButtonShadowStyle>();
    }

    // Botão "LOJA" (Fase 4/Monetização, placeholder — ver MONETIZACAO.md) — mesma coluna de
    // atalhos de Chibers/Arsenal/Replay, logo abaixo deste último. Mesmo padrão de
    // BuildReplaysMenuButton acima (posição derivada de um botão já existente na coluna via
    // GameObject.Find, ícone placeholder sem arte dedicada ainda — Lucas vai gerar as artes dos
    // 4 botões juntos mais pra frente, pedido explícito do usuário nesta tarefa) — só troca o
    // botão-âncora (Btn_Replays em vez de Btn_Arsenal) e o destino do clique (06_Loja).
    private void BuildLojaMenuButton()
    {
        if (theme == null) return;

        var replaysGo = GameObject.Find("Btn_Replays");
        if (replaysGo == null) return;
        var replaysRt = replaysGo.GetComponent<RectTransform>();
        if (replaysRt == null) return;

        float gapY = 211.87f;
        var chibersGo = GameObject.Find("Btn_SelectCharacter");
        var arsenalGo = GameObject.Find("Btn_Arsenal");
        if (chibersGo != null && arsenalGo != null)
        {
            var chibersRt = chibersGo.GetComponent<RectTransform>();
            var arsenalRt = arsenalGo.GetComponent<RectTransform>();
            if (chibersRt != null && arsenalRt != null) gapY = chibersRt.anchoredPosition.y - arsenalRt.anchoredPosition.y;
        }

        var btnGo = new GameObject("Btn_Loja");
        btnGo.transform.SetParent(replaysRt.parent, false);
        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = replaysRt.anchorMin;
        rt.anchorMax = replaysRt.anchorMax;
        rt.pivot = replaysRt.pivot;
        rt.sizeDelta = replaysRt.sizeDelta;
        rt.anchoredPosition = replaysRt.anchoredPosition - new Vector2(0f, gapY);

        var bg = btnGo.AddComponent<Image>();
        bg.sprite = UIShapeUtil.RoundedRect(theme.secondaryButton, 16f);
        bg.type = Image.Type.Sliced;
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(OnLojaButton);

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(btnGo.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.08f, 0.30f);
        iconRt.anchorMax = new Vector2(0.92f, 0.92f);
        iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = UIShapeUtil.RoundedRect(Color.Lerp(theme.secondaryButton, Color.white, 0.35f), 10f);
        iconImg.type = Image.Type.Sliced;

        var stripGo = new GameObject("LabelStrip");
        stripGo.transform.SetParent(btnGo.transform, false);
        var stripRt = stripGo.AddComponent<RectTransform>();
        stripRt.anchorMin = new Vector2(0f, 0f);
        stripRt.anchorMax = new Vector2(1f, 0.30f);
        stripRt.offsetMin = Vector2.zero; stripRt.offsetMax = Vector2.zero;
        var stripImg = stripGo.AddComponent<Image>();
        stripImg.sprite = UIShapeUtil.RoundedRect(theme.panelBackgroundAlt, 16f);
        stripImg.type = Image.Type.Sliced;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(stripGo.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var labelTxt = labelGo.AddComponent<TextMeshProUGUI>();
        // Mesma correção de fonte/material já aplicada ao label do Replay (ver comentário em
        // BuildReplaysMenuButton) — copia font+fontSharedMaterial do label de ARSENAL antes de
        // setar .text, senão o glyph vem da fonte TMP padrão (fina, sem o outline customizado).
        var arsenalLabel = arsenalGo != null ? arsenalGo.GetComponentInChildren<TMP_Text>(true) : null;
        if (arsenalLabel != null)
        {
            labelTxt.font = arsenalLabel.font;
            labelTxt.fontSharedMaterial = arsenalLabel.fontSharedMaterial;
        }
        labelTxt.text = "LOJA";
        labelTxt.fontStyle = FontStyles.Bold;
        labelTxt.color = theme.textOnDark;
        labelTxt.outlineWidth = 0.2f;
        labelTxt.outlineColor = theme.panelBackground;
        labelTxt.enableAutoSizing = true;
        labelTxt.fontSizeMin = 10f;
        labelTxt.fontSizeMax = 40f;
        labelTxt.alignment = TextAlignmentOptions.Center;
        labelTxt.ForceMeshUpdate();

        btnGo.AddComponent<UIButtonShadowStyle>();
    }

    // Fase 1 (placeholder) da Loja — cena 06_Loja, ver ShopController.cs/MONETIZACAO.md.
    public void OnLojaButton()
    {
        SceneManager.LoadScene("06_Loja");
    }

    // **Bug real corrigido (2026-07-20)**: "Btn_SelectCharacter"/"Btn_Arsenal" (Chibers/Arsenal,
    // pré-colocados em 01_MainMenu.unity) usavam âncora CENTRAL (anchorMin=anchorMax=(0.5,0.5))
    // com um offset fixo em pixels — diferente do BtnJogar, que usa uma âncora de PONTO ÚNICO num
    // canto real da tela (anchorMin=anchorMax=(1,0)). Offsets em X a partir do centro já ficavam
    // estáveis (CanvasScaler trava a escala pela LARGURA, então a largura do Canvas em unidades
    // locais é sempre 1920 — "colado na borda esquerda" já funcionava, como o usuário confirmou),
    // mas offsets em Y a partir do centro NÃO: a ALTURA do Canvas varia com o aspect ratio (só a
    // largura é travada), então "tantos pixels acima/abaixo do CENTRO" aponta pra uma distância
    // diferente do chão conforme a tela fica mais larga/estreita — exatamente o "no chão não
    // funciona" reportado. Fix: reancora os dois pro canto inferior-ESQUERDO
    // (anchorMin=anchorMax=pivot=(0,0), espelho do BtnJogar) preservando a posição visual atual
    // (lida via GetWorldCorners antes de trocar a âncora, convertida de volta pro espaço local do
    // pai via `RectTransform.rect` — que já resolve o pivot do próprio pai automaticamente).
    // Chamado ANTES de BuildReplaysMenuButton: Replays copia anchorMin/anchorMax/pivot de
    // Btn_Arsenal, então herda a âncora nova automaticamente.
    private void ReanchorLeftColumnToBottomLeft()
    {
        var chibersGo = GameObject.Find("Btn_SelectCharacter");
        var arsenalGo = GameObject.Find("Btn_Arsenal");
        if (chibersGo != null) ReanchorToBottomLeft(chibersGo.GetComponent<RectTransform>());
        if (arsenalGo != null) ReanchorToBottomLeft(arsenalGo.GetComponent<RectTransform>());
    }

    private void ReanchorToBottomLeft(RectTransform rt)
    {
        if (rt == null) return;
        var parentRt = rt.parent as RectTransform;
        if (parentRt == null) return;

        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector3 bottomLeftWorld = corners[0]; // [0] = bottom-left em espaço de mundo

        // Posição atual do canto inferior-esquerdo no espaço LOCAL do pai (relativo ao PIVOT do
        // pai, não ao canto dele) — e o canto inferior-esquerdo do próprio pai nesse mesmo
        // espaço (`rect.xMin/yMin` já descontam o pivot do pai automaticamente, qualquer que
        // seja). A diferença entre os dois é o `anchoredPosition` certo pra âncora (0,0).
        Vector3 localInParent = parentRt.InverseTransformPoint(bottomLeftWorld);
        Vector2 parentBottomLeftLocal = new Vector2(parentRt.rect.xMin, parentRt.rect.yMin);
        Vector2 newAnchoredPosition = new Vector2(localInParent.x, localInParent.y) - parentBottomLeftLocal;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = newAnchoredPosition;
    }

    // Alinha a base da coluna de atalhos (CHIBERS/ARSENAL/REPLAY/LOJA) com a base do BtnJogar
    // (2026-07-20, pedido do usuário — hoje a coluna termina mais acima, desalinhada com o Jogar
    // e com o painel de detalhe do personagem). Chamado DEPOIS de BuildReplaysMenuButton/
    // BuildLojaMenuButton (precisa que os dois já existam pra deslocar os 4 juntos).
    // Usa espaço de MUNDO (`GetWorldCorners`) em vez de tentar derivar a diferença a partir dos
    // valores brutos de anchoredPosition — mais simples de raciocinar e continua funcionando
    // mesmo depois de ReanchorLeftColumnToBottomLeft (a comparação em espaço de mundo não muda
    // com o esquema de âncora usado). Como os 4 botões são filhos do MESMO Canvas ("Panel"), a
    // conversão de volta pra unidades locais (`lossyScale.y`) é exata. Btn_Loja (mais recente,
    // mais abaixo na coluna) é quem define a base agora — era Btn_Replays antes dele existir.
    private void AlignLeftColumnWithJogar()
    {
        var jogarGo = GameObject.Find("BtnJogar");
        var chibersGo = GameObject.Find("Btn_SelectCharacter");
        var arsenalGo = GameObject.Find("Btn_Arsenal");
        var replaysGo = GameObject.Find("Btn_Replays");
        var lojaGo = GameObject.Find("Btn_Loja");
        if (jogarGo == null || chibersGo == null || arsenalGo == null || replaysGo == null || lojaGo == null) return;

        var jogarRt = jogarGo.GetComponent<RectTransform>();
        var chibersRt = chibersGo.GetComponent<RectTransform>();
        var arsenalRt = arsenalGo.GetComponent<RectTransform>();
        var replaysRt = replaysGo.GetComponent<RectTransform>();
        var lojaRt = lojaGo.GetComponent<RectTransform>();
        if (jogarRt == null || chibersRt == null || arsenalRt == null || replaysRt == null || lojaRt == null) return;

        var corners = new Vector3[4];
        jogarRt.GetWorldCorners(corners);
        float jogarBottomWorldY = corners[0].y; // corners[0] = bottom-left, em espaço de mundo

        lojaRt.GetWorldCorners(corners);
        float lojaBottomWorldY = corners[0].y;

        float deltaWorldY = jogarBottomWorldY - lojaBottomWorldY;
        float localDelta = deltaWorldY / arsenalRt.lossyScale.y;
        var shift = new Vector2(0f, localDelta);

        chibersRt.anchoredPosition += shift;
        arsenalRt.anchoredPosition += shift;
        replaysRt.anchoredPosition += shift;
        lojaRt.anchoredPosition += shift;
    }

    // Botão "Sair da Conta" TEMPORÁRIO (2026-07-15, pedido do usuário) — só pra testar o fluxo
    // de logout enquanto não existe uma tela de Configurações de verdade (ver OnOptionsButton
    // abaixo, ainda um stub); mover pra lá quando ela for construída. Construído via código
    // (mesmo padrão de CharacterPanel logo acima) em vez de editado na cena 01_MainMenu.unity —
    // canto superior esquerdo, mesma convenção de posição do botão "Voltar" já usada em
    // 02_SelectCharacter/03_Arsenal, só que aqui não existe nenhum "Voltar" pra colidir.
    private void BuildLogoutButton()
    {
        if (theme == null) return;

        var canvasGo = new GameObject("LogoutButtonCanvas (temp)");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var btnGo = new GameObject("BtnLogout (temp)");
        btnGo.transform.SetParent(canvasGo.transform, false);
        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(200f, 50f);
        rt.anchoredPosition = new Vector2(30f, -30f);

        var img = btnGo.AddComponent<Image>();
        img.sprite = UIShapeUtil.RoundedRect(theme.danger, 10f);
        img.type = Image.Type.Sliced;
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OnLogoutClicked);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);
        var lrt = labelGo.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var txt = labelGo.AddComponent<TextMeshProUGUI>();
        txt.text = "Sair da Conta";
        txt.fontSize = 18;
        txt.fontStyle = FontStyles.Bold;
        txt.color = theme.textOnDark;
        txt.alignment = TextAlignmentOptions.Center;
    }

    private void OnLogoutClicked()
    {
        // Reseta todo PlayerProfile já tocado nesta sessão do processo pro estado "de fábrica"
        // (2026-07-15, correção de isolamento entre contas) — sem isso, a próxima conta a logar
        // no MESMO processo (sem fechar o jogo) herdaria em memória o progresso que a conta que
        // acabou de sair deixou nesses ScriptableObjects, mesmo com save.json já isolado por uid
        // (ver LocalSaveService/PlayerProfileConverter). Precisa rodar ANTES do SignOut, pra
        // garantir que nenhuma tela consiga ler o estado contaminado no meio da transição.
        PlayerProfileConverter.RestoreAllPristine();
        AuthService.SignOut();
        StartCoroutine(LoadLoginSceneAsync());
    }

    private System.Collections.IEnumerator LoadLoginSceneAsync()
    {
        var op = SceneManager.LoadSceneAsync("00_Login");
        while (op != null && !op.isDone) yield return null;
    }

    public void OnCharacterButton()
    {
        SceneManager.LoadScene("02_SelectCharacter");
    }

    // Botão "Arsenal" (2026-07-14) — abre 03_Arsenal (grade de armas/skills, ver ArsenalController),
    // não confundir com "03_SelectWeapons" (cena cancelada 2026-07-16, ver CLAUDE.md — campo
    // `selectWeapons` que apontava pra ela, nunca usado em nenhum método, removido nesta sessão).
    public void OnArsenalButton()
    {
        SceneManager.LoadScene("03_Arsenal");
    }

    // Gate de energia (2026-07-19, pedido do usuário): 10 batalhas diárias, +1 a cada 2h (ver
    // EnergySettings/EnergyService). Ponto exato onde a checagem entra no fluxo de sempre —
    // continua carregando 05_SelectOpponent normalmente quando há energia disponível; só passa a
    // bloquear/perguntar quando chega a 0. `async void` é o padrão aceito pra handler de clique de
    // UI em Unity (não há Task pra aguardar de fora) — qualquer exceção dentro é só logada via
    // Debug.LogError nos serviços chamados, nunca propaga sem tratamento.
    //
    // Conta obrigatória (decisão do usuário ao definir este fluxo): sem AuthService.IsSignedIn não
    // existe wallet/energia nenhuma pra ler na nuvem, então o botão pede login em vez de deixar
    // "Pular (offline)" jogar sem gate nenhum.
    public async void OnPlayButton()
    {
        if (selectedProfileHolder.currentProfile == null) return;

        if (!AuthService.IsSignedIn)
        {
            ShowMessagePopup("Crie uma conta ou entre pra jogar.");
            return;
        }

        if (_energySettings == null)
        {
            // Sem o asset (Resources/EnergySettings.asset não encontrado) não dá pra aplicar o
            // gate com segurança — deixa jogar sem checagem em vez de travar o jogo inteiro por
            // causa de uma referência ausente.
            SceneManager.LoadScene("05_SelectOpponent");
            return;
        }

        string uid = AuthService.CurrentUser.UserId;
        string characterId = selectedProfileHolder.currentProfile.OpponentId();

        var (current, _) = await EnergyService.GetOrRegenAsync(uid, characterId, _energySettings);
        RefreshEconomyHuds();

        // Só CHECA energia aqui — o consumo de fato só acontece em AttackSequencer.OnCombatEnd,
        // depois que a luta termina em vitória ou derrota (bug real reportado pelo usuário: energia
        // estava sendo gasta neste clique, antes até da luta começar, então desistir em
        // 05_SelectOpponent ou fechar o jogo no meio do combate já cobrava a energia à toa).
        if (current > 0)
        {
            SceneManager.LoadScene("05_SelectOpponent");
            return;
        }

        ShowRefillConfirmPopup(uid, characterId);
    }

    // Cutuca CharacterPanel (moeda/diamante) e MainMenuCharacterPreview (fileira de energia) pra
    // relerem PlayerEconomyState — os dois já expõem Refresh()/RefreshEnergyHud() por outro
    // motivo (troca de personagem/sync de nuvem), reaproveitados aqui em vez de duplicar lógica.
    private void RefreshEconomyHuds()
    {
        if (_hudCoinText != null) _hudCoinText.text = PlayerEconomyState.Coins.ToString();
        if (_hudDiamondText != null) _hudDiamondText.text = PlayerEconomyState.Diamonds.ToString();

        var preview = FindObjectOfType<MainMenuCharacterPreview>();
        if (preview != null) preview.RefreshEnergyHud();
    }

    // Popup de confirmação pra gastar diamante e reabastecer 1 energia (chegou a 0). Construído
    // sob demanda (não faz parte de BuildUI nenhum) — mesmo espírito visual de BuildLogoutButton/
    // CharacterPanel (RoundedRect + TMP, sem prefab), mas autocontido/descartável: cria e destrói
    // o próprio Canvas a cada abertura, já que é um popup raro (só quando a energia zera).
    // Preço progressivo por dia, POR PERSONAGEM (2026-07-21, pedido do usuário — substitui o
    // custo fixo único de antes): 1ª vez hoje = refillCostTier1, 2ª = refillCostTier2, 3ª em
    // diante = refillCostTier3Plus (travado). `async void` (mesmo padrão de OnPlayButton) porque
    // precisa consultar EnergyService.GetRefillCostAsync (Firestore, hora do servidor) ANTES de
    // saber que valor mostrar no popup — o call site em OnPlayButton continua chamando isto sem
    // `await`, de propósito (mesmo fire-and-forget de sempre pra UI).
    private async void ShowRefillConfirmPopup(string uid, string characterId)
    {
        if (theme == null || _energySettings == null) return;

        int cost = await EnergyService.GetRefillCostAsync(uid, characterId, _energySettings);
        if (PlayerEconomyState.Diamonds < cost)
        {
            ShowMessagePopup($"Energia esgotada. Você precisa de {cost} diamantes pra continuar jogando agora (tem {PlayerEconomyState.Diamonds}). Visite a Loja pra comprar mais diamantes.",
                showEnergyCountdown: true);
            return;
        }

        BuildPopup(
            $"Energia esgotada.\nGastar {cost} diamantes pra continuar jogando com este personagem hoje?",
            confirmLabel: "Gastar diamantes",
            onConfirm: () => { _ = SpendAndContinueRoutine(uid, characterId); },
            cancelLabel: "Cancelar",
            showEnergyCountdown: true);
    }

    private async Task SpendAndContinueRoutine(string uid, string characterId)
    {
        // Recalcula o custo de novo por dentro (mesma lógica de GetRefillCostAsync acima) em vez
        // de reaproveitar o valor já mostrado no popup — evita cobrar um preço desatualizado se o
        // dia virou ou outro pagamento aconteceu entre abrir o popup e confirmar (ver comentário
        // em EnergyService.PayToRefillAsync).
        var (spent, cost) = await EnergyService.PayToRefillAsync(uid, characterId, _energySettings);
        if (!spent)
        {
            ShowMessagePopup("Não foi possível gastar diamantes agora. Tente de novo.");
            return;
        }

        RefreshEconomyHuds();
        SceneManager.LoadScene("05_SelectOpponent");
    }

    // Popup só-de-mensagem (1 botão "OK") — mesmo BuildPopup abaixo, sem callback de confirmação.
    private void ShowMessagePopup(string message, bool showEnergyCountdown = false)
    {
        if (theme == null) return;
        BuildPopup(message, confirmLabel: "OK", onConfirm: null, cancelLabel: null, showEnergyCountdown: showEnergyCountdown);
    }

    // Popup do toque na fileira de energia (2026-07-20, pedido do usuário — o tooltip inline
    // ancorado acima da fileira ficava cortado/inacessível perto do topo da tela). Reaproveita o
    // MESMO popup modal usado pelos outros popups do projeto (overlay escurecido + painel
    // centralizado + botão "OK" que fecha, ver BuildPopup) em vez de um tooltip próprio ancorado
    // relativo à fileira. Público — chamado por MainMenuCharacterPreview.OnEnergyRowTapped via
    // FindObjectOfType, já que o toque acontece lá (onde a fileira de ícones vive), mas o popup
    // mora aqui (mesmo Canvas/estilo dos outros popups do MainMenuController). O chamador só
    // invoca isto quando a energia está ABAIXO do máximo (nada regenerando = nada a mostrar).
    public void ShowEnergyStatusPopup()
    {
        ShowMessagePopup("Tempo até a próxima energia:", showEnergyCountdown: true);
    }

    // Base compartilhada dos dois popups acima — painel central com mensagem + até 2 botões.
    // cancelLabel nulo = só 1 botão (mensagem informativa); onConfirm nulo = botão de confirmar
    // só fecha o popup (equivalente a um "OK"). showEnergyCountdown (2026-07-20, pedido do
    // usuário) — mostra o tempo até a próxima energia entre a mensagem e os botões; usado pelos
    // popups disparados a partir de energia zerada (ShowRefillConfirmPopup) e por
    // ShowEnergyStatusPopup (toque na fileira de energia do menu). Painel mais alto e fonte bem
    // maior nesse modo (2026-07-20, 2ª rodada — pedido do usuário: "agora é o elemento central do
    // popup", sem o prefixo "Próxima energia em" redundante com o título que já vem em `message`
    // — ver PlayerEconomyState.FormatEnergyCountdown).
    private void BuildPopup(string message, string confirmLabel, System.Action onConfirm, string cancelLabel,
        bool showEnergyCountdown = false)
    {
        var canvasGo = new GameObject("EnergyPopupCanvas (temp)");
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
        // Toque fora do painel fecha o popup (2026-07-20, pedido do usuário — testado no popup de
        // energia, mas vale pra todos os popups que passam por aqui). O painel é um sibling
        // desenhado POR CIMA do overlay, então cliques dentro dele já são capturados pelo próprio
        // Image do painel antes de chegarem aqui — este botão só dispara quando o toque cai FORA
        // dele. Mesmo efeito de Cancelar (só fecha, nunca invoca onConfirm).
        var overlayBtn = overlayGo.AddComponent<Button>();
        overlayBtn.targetGraphic = overlayImg;
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(() => Destroy(canvasGo));

        // Painel mais alto quando mostra o countdown (2026-07-20, 2ª rodada — pedido do usuário:
        // o valor de tempo virou o elemento central/em destaque do popup, precisa de mais espaço
        // vertical que os 300px de antes davam pra um fontSize bem maior).
        float panelHeight = showEnergyCountdown ? 420f : 300f;
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(560f, panelHeight);
        panelRt.anchoredPosition = Vector2.zero;
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.sprite = UIShapeUtil.RoundedRect(theme.panelBackgroundAlt, 24f);
        panelImg.type = Image.Type.Sliced;

        var msgGo = new GameObject("Message");
        msgGo.transform.SetParent(panelGo.transform, false);
        var msgRt = msgGo.AddComponent<RectTransform>();
        // Faixa própria (0.68-0.92) quando mostra o countdown — o valor grande abaixo (ver
        // EnergyTimer logo adiante) precisa da faixa do meio (0.30-0.62) só pra ele; sem
        // countdown, a mensagem continua ocupando a faixa de sempre (0.4-0.9).
        if (showEnergyCountdown)
        {
            msgRt.anchorMin = new Vector2(0.08f, 0.68f); msgRt.anchorMax = new Vector2(0.92f, 0.92f);
        }
        else
        {
            msgRt.anchorMin = new Vector2(0.08f, 0.4f); msgRt.anchorMax = new Vector2(0.92f, 0.9f);
        }
        msgRt.offsetMin = msgRt.offsetMax = Vector2.zero;
        var msgTxt = msgGo.AddComponent<TextMeshProUGUI>();
        msgTxt.text = message;
        msgTxt.fontSize = 24;
        msgTxt.color = theme.textOnDark;
        msgTxt.alignment = TextAlignmentOptions.Center;
        msgTxt.enableWordWrapping = true;

        if (showEnergyCountdown)
        {
            var timerGo = new GameObject("EnergyTimer");
            timerGo.transform.SetParent(panelGo.transform, false);
            var timerRt = timerGo.AddComponent<RectTransform>();
            // Faixa generosa (32% da altura do painel) entre a mensagem e o botão — o valor
            // (ex: "0:28:43", sem prefixo, ver PlayerEconomyState.FormatEnergyCountdown) é o
            // elemento central do popup agora, precisa de espaço pra um fontSize bem maior.
            timerRt.anchorMin = new Vector2(0.08f, 0.30f); timerRt.anchorMax = new Vector2(0.92f, 0.62f);
            timerRt.offsetMin = timerRt.offsetMax = Vector2.zero;
            var timerTxt = timerGo.AddComponent<TextMeshProUGUI>();
            timerTxt.fontSize = 64; // 20→64 (2026-07-20, 2ª rodada) — elemento central do popup agora
            timerTxt.fontStyle = FontStyles.Bold;
            timerTxt.color = Color.black; // 2026-07-20, pedido do usuário (era theme.currencyGold)
            timerTxt.alignment = TextAlignmentOptions.Center;
            // Borda clara — o painel do popup é escuro (panelBackgroundAlt), texto preto sem
            // contorno ficaria ilegível em cima dele.
            timerTxt.outlineWidth = 0.2f;
            timerTxt.outlineColor = Color.white;
            timerGo.AddComponent<CountdownLabel>().Init(timerTxt, PlayerEconomyState.FormatEnergyCountdown);
        }

        bool hasCancel = !string.IsNullOrEmpty(cancelLabel);

        var confirmGo = new GameObject("BtnConfirm");
        confirmGo.transform.SetParent(panelGo.transform, false);
        var confirmRt = confirmGo.AddComponent<RectTransform>();
        confirmRt.anchorMin = confirmRt.anchorMax = new Vector2(hasCancel ? 0.73f : 0.5f, 0.16f);
        confirmRt.sizeDelta = new Vector2(220f, 64f);
        confirmRt.anchoredPosition = Vector2.zero;
        var confirmImg = confirmGo.AddComponent<Image>();
        confirmImg.sprite = UIShapeUtil.RoundedRect(theme.primaryAction, 14f);
        confirmImg.type = Image.Type.Sliced;
        var confirmBtn = confirmGo.AddComponent<Button>();
        confirmBtn.targetGraphic = confirmImg;
        confirmBtn.onClick.AddListener(() => { Destroy(canvasGo); onConfirm?.Invoke(); });

        var confirmLabelGo = new GameObject("Label");
        confirmLabelGo.transform.SetParent(confirmGo.transform, false);
        var clrt = confirmLabelGo.AddComponent<RectTransform>();
        clrt.anchorMin = Vector2.zero; clrt.anchorMax = Vector2.one;
        clrt.offsetMin = clrt.offsetMax = Vector2.zero;
        var confirmLabelTxt = confirmLabelGo.AddComponent<TextMeshProUGUI>();
        confirmLabelTxt.text = confirmLabel;
        confirmLabelTxt.fontSize = 20;
        confirmLabelTxt.fontStyle = FontStyles.Bold;
        confirmLabelTxt.color = theme.textOnDark;
        confirmLabelTxt.alignment = TextAlignmentOptions.Center;

        if (hasCancel)
        {
            var cancelGo = new GameObject("BtnCancel");
            cancelGo.transform.SetParent(panelGo.transform, false);
            var cancelRt = cancelGo.AddComponent<RectTransform>();
            cancelRt.anchorMin = cancelRt.anchorMax = new Vector2(0.27f, 0.16f);
            cancelRt.sizeDelta = new Vector2(220f, 64f);
            cancelRt.anchoredPosition = Vector2.zero;
            var cancelImg = cancelGo.AddComponent<Image>();
            cancelImg.sprite = UIShapeUtil.RoundedRect(theme.secondaryButtonAlt, 14f);
            cancelImg.type = Image.Type.Sliced;
            var cancelBtn = cancelGo.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(() => Destroy(canvasGo));

            var cancelLabelGo = new GameObject("Label");
            cancelLabelGo.transform.SetParent(cancelGo.transform, false);
            var callt = cancelLabelGo.AddComponent<RectTransform>();
            callt.anchorMin = Vector2.zero; callt.anchorMax = Vector2.one;
            callt.offsetMin = callt.offsetMax = Vector2.zero;
            var cancelLabelTxt = cancelLabelGo.AddComponent<TextMeshProUGUI>();
            cancelLabelTxt.text = cancelLabel;
            cancelLabelTxt.fontSize = 20;
            cancelLabelTxt.fontStyle = FontStyles.Bold;
            cancelLabelTxt.color = theme.textOnDark;
            cancelLabelTxt.alignment = TextAlignmentOptions.Center;
        }
    }

    public void OnSelectCharacterButton()
    {
        SceneManager.LoadScene("02_SelectCharacter");
    }
    public void OnOptionsButton()
    {
        // Aqui voc� pode abrir um painel de configura��es
    }

    public void OnQuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
