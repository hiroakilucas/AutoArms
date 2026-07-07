using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuCharacterPreview : MonoBehaviour
{
    public Transform spawnPoint;
    private GameObject currentCharacter;
    [SerializeField] private SelectedProfileHolder selectedProfileHolder;

    // Reduz o personagem central levemente — o root dos prefabs de personagem fica nos pés
    // (mesmo pressuposto de CombatSceneLoader/RandomSpawnPosition), então escalar em torno da
    // própria transform mantém os pés no lugar sem precisar ajustar a posição.
    private const float PreviewScaleFactor = 0.82f;

    // Centralizado (2026-07-07) no espaço disponível à ESQUERDA do CharacterPanel (não no
    // centro absoluto da tela) — senão o personagem parece descentralizado com o painel
    // visível. Derivado da câmera ortográfica da cena (size=5, aspecto 16:9, X=0 — ver
    // "Main Camera" em 01_MainMenu.unity): meia-largura visível = 5*(1920/1080) = 8.889
    // unidades. CharacterPanel.PanelWidth=380px ocupa 380/1920=19.79% da largura da tela à
    // direita; o centro da faixa restante (à esquerda do painel), convertido de volta pra
    // espaço de mundo, fica em X ≈ -1.76. Recalcular se PanelWidth ou o orthographicSize
    // abaixo mudarem.
    private const float CharacterCenterX = -1.76f;
    private const float CharacterGroundY = -2f;

    void Start()
    {
        var profile = selectedProfileHolder.currentProfile;

        if (profile == null) return;

        currentCharacter = Instantiate(profile.characterPrefab, spawnPoint.position, Quaternion.identity);
        currentCharacter.transform.localScale = profile.scale * PreviewScaleFactor;
        currentCharacter.transform.position = new Vector3(CharacterCenterX, CharacterGroundY, 0);
        Camera.main.orthographicSize = 5;

        DestroyImmediate(currentCharacter.GetComponent<PlayerCombat>());
        DestroyImmediate(currentCharacter.GetComponent<WeaponHandler>());
        DestroyImmediate(currentCharacter.GetComponent<MovementController>());
        DestroyImmediate(currentCharacter.GetComponent<AnimationController>());

        var anim = currentCharacter.GetComponent<Animator>();
        if (anim != null) anim.SetBool("Idle", true);

        BuildLevelXpHud(profile);
    }

    // Barra de XP fina + "Level X" acima dela, estilo My Brute — única barra de XP do menu
    // agora (a que existia no painel lateral foi removida, ver CharacterPanel). Fica
    // centralizada na mesma coordenada X do personagem (ver CharacterCenterX acima),
    // convertida pra fração de tela com a mesma fórmula de câmera ortográfica.
    //
    // Bug (2026-07-07, 2 rodadas): 1ª tentativa deixava um retângulo branco vazio (Canvas não
    // parentado + exceção no meio do método). Corrigido isso, mas a 2ª tentativa (guard
    // "if (theme == null) return;") revelou o problema de verdade: o campo
    // [SerializeField] UITheme theme deste componente resolve nulo em runtime, mesmo com o
    // asset wireado corretamente em 01_MainMenu.unity (conferido linha a linha) — o elemento
    // inteiro sumia (early return silencioso, sem exception nenhuma pro Console acusar).
    // Como o MainMenuController.theme (mesmo asset, mesmo mecanismo de serialização) funciona
    // de forma comprovada — o CharacterPanel que ele alimenta renderiza sem problema — o campo
    // próprio deste componente foi removido e o tema passou a ser buscado via
    // FindObjectOfType<MainMenuController>().Theme (ResolveTheme abaixo), eliminando de vez
    // essa 2ª fonte de wiring que se mostrou frágil duas vezes seguidas.
    private void BuildLevelXpHud(PlayerProfile p)
    {
        var theme = ResolveTheme();
        if (theme == null) return;

        Color panelBg = theme.panelBackgroundAlt;
        Color textColor = theme.textOnDark;
        Color goldColor = theme.currencyGold;

        const float halfWidth = 8.888889f; // orthographicSize(5) * aspecto 16:9
        float centerXFraction = (CharacterCenterX + halfWidth) / (halfWidth * 2f);

        var canvasGo = new GameObject("LevelXpHud");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var rootGo = new GameObject("Root");
        rootGo.transform.SetParent(canvasGo.transform, false);
        var rrt = rootGo.AddComponent<RectTransform>();
        // Caixa aumentada (2026-07-07): 240x56 → 280x76, pra caber o texto de XP dentro da
        // barra com folga. Ancorado em y=0.72 (era 0.71) — compensa o crescimento pra baixo
        // (a caixa cresce a partir do centro, então +20px de altura = +10px pra cada lado;
        // sem esse ajuste a borda inferior chegaria 10px mais perto da cabeça do personagem
        // do que estava calibrado na sessão anterior).
        rrt.anchorMin = rrt.anchorMax = new Vector2(centerXFraction, 0.72f);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(280f, 76f);

        // Fundo sólido (2026-07-07, era translúcido) — panelBackgroundAlt, mesma cor de fundo
        // do CharacterPanel, opaco.
        var bg = rootGo.AddComponent<Image>();
        bg.sprite = UIShapeUtil.RoundedRect(panelBg, 16f);
        bg.type = Image.Type.Sliced;

        var lvlGo = new GameObject("LevelText");
        lvlGo.transform.SetParent(rootGo.transform, false);
        var lrt = lvlGo.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.05f, 0.66f); lrt.anchorMax = new Vector2(0.95f, 0.96f);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var lvlTxt = lvlGo.AddComponent<TextMeshProUGUI>();
        lvlTxt.text = $"Level {p.level}";
        lvlTxt.fontSize = 18; lvlTxt.fontStyle = FontStyles.Bold;
        lvlTxt.color = textColor;
        lvlTxt.alignment = TextAlignmentOptions.Center;

        // Barra engordada (2026-07-07): ocupava só 0.14-0.42 (~16px de 56px) — agora
        // 0.08-0.58 (~38px de 76px), espaço suficiente pra caber o texto "atual/necessário"
        // centralizado dentro dela, além de mais legível por si só.
        var barBgGo = new GameObject("BarBg");
        barBgGo.transform.SetParent(rootGo.transform, false);
        var bbrt = barBgGo.AddComponent<RectTransform>();
        bbrt.anchorMin = new Vector2(0.06f, 0.08f); bbrt.anchorMax = new Vector2(0.94f, 0.58f);
        bbrt.offsetMin = bbrt.offsetMax = Vector2.zero;
        var barBgImg = barBgGo.AddComponent<Image>();
        barBgImg.sprite = UIShapeUtil.RoundedRect(new Color(0f, 0f, 0f, 0.55f), 8f);
        barBgImg.type = Image.Type.Sliced;

        int req = XpSystem.XpRequired(p.level);
        float pct = req > 0 ? Mathf.Clamp01((float)p.xpCurrent / req) : 0f;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(barBgGo.transform, false);
        var frt = fillGo.AddComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(Mathf.Max(pct, 0.001f), 1f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = UIShapeUtil.RoundedRect(goldColor, 7f);
        fillImg.type = Image.Type.Sliced;

        // Texto de XP dentro da própria barra (2026-07-07), "atual/necessário" — sobreposto ao
        // fundo+preenchimento (criado depois do Fill, então desenha por cima).
        var xpTxtGo = new GameObject("XpText");
        xpTxtGo.transform.SetParent(barBgGo.transform, false);
        var xrt = xpTxtGo.AddComponent<RectTransform>();
        xrt.anchorMin = Vector2.zero; xrt.anchorMax = Vector2.one;
        xrt.offsetMin = xrt.offsetMax = Vector2.zero;
        var xpTxt = xpTxtGo.AddComponent<TextMeshProUGUI>();
        xpTxt.text = $"{p.xpCurrent}/{req}";
        xpTxt.fontSize = 14; xpTxt.fontStyle = FontStyles.Bold;
        xpTxt.color = textColor;
        xpTxt.alignment = TextAlignmentOptions.Center;
    }

    // MainMenuController.theme já é comprovadamente confiável (CharacterPanel depende dele e
    // renderiza normalmente) — usa a mesma fonte em vez de duplicar o campo serializado aqui.
    private static UITheme ResolveTheme()
    {
        var controller = FindObjectOfType<MainMenuController>();
        return controller != null ? controller.Theme : null;
    }
}
