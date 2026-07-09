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

    // Centralizado no centro ABSOLUTO da tela (2026-07-07, revertida a decisão anterior de
    // deslocar pra esquerda do CharacterPanel) — câmera ortográfica da cena tem X=0 (ver
    // "Main Camera" em 01_MainMenu.unity), então X=0 aqui cai exatamente no centro horizontal
    // da tela, por baixo/à esquerda do CharacterPanel quando ele estiver expandido.
    private const float CharacterCenterX = 0f;
    private const float CharacterGroundY = -1f;
    private const float OrthographicSize = 5f;

    // Calibração visual de referência da barra de XP/Level (ver BuildLevelXpHud): com o
    // personagem em CalibratedGroundY, a barra fica bem posicionada acima da cabeça dele em
    // CalibratedYFraction da tela. Se CharacterGroundY mudar de novo no futuro, a barra
    // acompanha proporcionalmente (mesmo deslocamento em unidades de mundo = mesmo
    // deslocamento em fração de tela, já que é um reposicionamento rígido do personagem
    // inteiro) sem precisar recalibrar esse par de valores manualmente.
    private const float CalibratedGroundY = -2f;
    private const float CalibratedYFraction = 0.72f;

    void Start()
    {
        var profile = selectedProfileHolder.currentProfile;

        if (profile == null) return;

        currentCharacter = Instantiate(profile.characterPrefab, spawnPoint.position, Quaternion.identity);
        currentCharacter.transform.localScale = profile.scale * PreviewScaleFactor;
        currentCharacter.transform.position = new Vector3(CharacterCenterX, CharacterGroundY, 0);
        Camera.main.orthographicSize = OrthographicSize;

        DestroyImmediate(currentCharacter.GetComponent<PlayerCombat>());
        DestroyImmediate(currentCharacter.GetComponent<WeaponHandler>());
        DestroyImmediate(currentCharacter.GetComponent<MovementController>());

        // AnimationController continua vivo (2026-07-08, diferente de antes) — precisa dele pra
        // reagir a clique com Hurt/Slashing, ver `CharacterPreviewReaction`/BuildClickReaction.
        var animController = currentCharacter.GetComponent<AnimationController>();
        if (animController != null) animController.SetIdle(true);

        BuildClickReaction(currentCharacter, animController);
        BuildLevelXpHud(profile);
    }

    // Clique no personagem central reage com Hurt/Slashing (2026-07-08, pedido do usuário) —
    // mesma reação do portrait de `02_SelectCharacter`, via o componente compartilhado
    // `CharacterPreviewReaction`. Diferente do portrait (renderizado numa RenderTexture, clique
    // via `Button` de UI), aqui o personagem é world-space de verdade — clique detectado por
    // `Collider2D`/`OnMouseDown` (mensagem nativa da Unity, não depende de EventSystem nenhum).
    // `BoxCollider2D` dimensionado a partir dos bounds REAIS dos `Renderer`s do personagem (soma
    // de todas as partes do sprite, Spriter2UnityDX) em vez de um tamanho fixo chutado — cada
    // personagem tem proporções diferentes.
    private void BuildClickReaction(GameObject character, AnimationController animController)
    {
        if (animController == null) return;

        var renderers = character.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        foreach (var r in renderers) combined.Encapsulate(r.bounds);

        var collider = character.AddComponent<BoxCollider2D>();
        collider.offset = character.transform.InverseTransformPoint(combined.center);
        Vector3 scale = character.transform.lossyScale;
        collider.size = new Vector2(combined.size.x / scale.x, combined.size.y / scale.y);

        character.AddComponent<CharacterPreviewReaction>().Init(animController);
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

        const float halfWidth = 8.888889f; // OrthographicSize(5) * aspecto 16:9
        float centerXFraction = (CharacterCenterX + halfWidth) / (halfWidth * 2f);
        // Acompanha CharacterGroundY: mesmo deslocamento em unidades de mundo vira o mesmo
        // deslocamento em fração de tela (reposicionamento rígido do personagem, cabeça
        // inclusa) — ver comentário de CalibratedGroundY/CalibratedYFraction acima.
        float yFraction = CalibratedYFraction + (CharacterGroundY - CalibratedGroundY) / (2f * OrthographicSize);

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
        // barra com folga. Y calculado a partir de CharacterGroundY (yFraction acima), não
        // mais um valor fixo — segue o personagem quando ele se move na tela.
        rrt.anchorMin = rrt.anchorMax = new Vector2(centerXFraction, yFraction);
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
