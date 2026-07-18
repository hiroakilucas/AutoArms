using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Badge circular colorido (valor numérico do atributo) + fileira de 10 pips/blocos — SEMPRE
// exatamente 10, nunca mais nem menos. Os blocos são uma JANELA DESLIZANTE dos últimos 10
// pontos do valor (`ComputeSlidingWindowColors`): cada bloco representa um ponto específico
// (não uma posição fixa 1-10 que reseta a cada múltiplo de 10), e a cor de cada bloco vem do
// tier DESSE PONTO, então um valor bem no meio de uma transição de tier (ex: 12, 24) mostra a
// barra com dois tons diferentes ao mesmo tempo.
//
// Paleta de 40 tiers via HSL (2026-07-07, substitui os 4 tokens fixos do UITheme) — cobre os
// valores 1-400 com uma cor distinta a cada 10 pontos (`GetColorForTier`/`TierPalette`, ver
// abaixo). Acima de 400 (tier >= 40), a paleta recicla (`tier % 40`) e um indicador de
// "prestígio" (borda dourada pulsante + "×N") aparece ao lado do badge — ver `SetValue`.
// Usado nas 3 linhas de atributo (STR/AGI/SPD) do CharacterPanel único.
//
// `Build` só preenche o conteúdo de um `rowContainer` já dimensionado pelo chamador (fração
// manual de RectTransform ou LayoutElement dentro de um VerticalLayoutGroup) — não decide
// onde a linha fica posicionada, isso é responsabilidade de quem chama.
public class AttributePipBar
{
    private const int PipCount = 10;
    private const int TierSize = 10;

    // 40 tiers = 4 "eras" (S/L diferentes) × 10 matizes (hue 0,36,72...324°) cada — cobre os
    // valores 1-400 (tier 0-39) antes de reciclar.
    private const int TierPaletteSize = 40;
    private const int TiersPerEra = 10;

    private readonly Image _badgeImg;
    private readonly TMP_Text _badgeText;
    private readonly Image[] _pips;
    private readonly GameObject _prestigeBorderGo;
    private readonly TMP_Text _prestigeText;

    // Nenhuma referência a UITheme guardada na instância — a cor por tier vem inteiramente de
    // TierPalette/GetColorForTier (HSL calculado, não lido do tema); `theme` só é usado dentro de
    // `Build` (label/dígito do badge), como parâmetro local, sem precisar virar campo.
    private AttributePipBar(Image badgeImg, TMP_Text badgeText, Image[] pips,
        GameObject prestigeBorderGo, TMP_Text prestigeText)
    {
        _badgeImg = badgeImg;
        _badgeText = badgeText;
        _pips = pips;
        _prestigeBorderGo = prestigeBorderGo;
        _prestigeText = prestigeText;
    }

    // Ícones de STR/AGI/SPD (2026-07-16, pedido do usuário — substituem o texto "STR"/"AGI"/
    // "SPD" que ficava nessa mesma área) — carregados via Resources.Load (não dá pra wireary por
    // Inspector: AttributePipBar não é MonoBehaviour, e é construído 100% via código em runtime,
    // sem nenhum GameObject de cena pra arrastar um Sprite nele; mesmo padrão de
    // CombatSceneLoader.RandomizeArenaBackground pros backgrounds de arena). PNGs originais do
    // usuário em Assets/Resources/UI/Attributes/{Str,Agi,Speed}.png, movidos de
    // C:\Users\user\Desktop\Prototipo\Icones\. `static readonly` — carregado uma vez só, no
    // carregamento da classe, igual TierPalette abaixo.
    private static readonly Sprite StrIcon = Resources.Load<Sprite>("UI/Attributes/Str");
    private static readonly Sprite AgiIcon = Resources.Load<Sprite>("UI/Attributes/Agi");
    private static readonly Sprite SpdIcon = Resources.Load<Sprite>("UI/Attributes/Speed");

    private static Sprite IconForLabel(string label)
    {
        switch (label)
        {
            case "STR": return StrIcon;
            case "AGI": return AgiIcon;
            case "SPD": return SpdIcon;
            default: return null;
        }
    }

    // `iconScale`/`badgeAnchorX`/`pipsAnchorMinX` (2026-07-18, pedido do usuário — "05_SelectOpponent"
    // com ícones maiores e mais próximos do badge/pips do que o `CharacterPanel` usa) — defaults
    // preservam exatamente o comportamento original (todos os chamadores existentes, ex:
    // `CharacterPanel.BuildInfoBlock`, continuam sem passar esses argumentos).
    public static AttributePipBar Build(GameObject rowContainer, UITheme theme, string label,
        float iconScale = 3f, float badgeAnchorX = 0.24f, float pipsAnchorMinX = 0.44f)
    {
        var lblGo = new GameObject("Lbl");
        lblGo.transform.SetParent(rowContainer.transform, false);
        var lrt = lblGo.AddComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0.1f); lrt.anchorMax = new Vector2(0.20f, 0.9f);
        lrt.offsetMin = new Vector2(14f, 0f); lrt.offsetMax = Vector2.zero;
        var labelIcon = IconForLabel(label);
        if (labelIcon != null)
        {
            var lblImg = lblGo.AddComponent<Image>();
            lblImg.sprite = labelIcon;
            lblImg.preserveAspect = true;
            // Scale 3x (2026-07-16, pedido do usuário) — o ícone original é bem menor que a
            // área reservada pro antigo texto "STR"/"AGI"/"SPD"; escala em cima do RectTransform
            // (em vez de aumentar o próprio rect/sizeDelta) porque `preserveAspect` já centraliza
            // o sprite dentro do rect base — isso só faz o resultado final crescer a partir do
            // centro, sem precisar recalcular anchors/offsets.
            lrt.localScale = new Vector3(iconScale, iconScale, 1f);
        }
        else
        {
            // Fallback de texto — nenhum dos 2 chamadores hoje (CharacterPanel/
            // SelectOpponentController) passa um label fora de STR/AGI/SPD, mas mantém o
            // comportamento antigo em vez de deixar a área vazia se isso mudar no futuro.
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = label; lbl.fontSize = 18; lbl.fontStyle = FontStyles.Bold;
            lbl.color = theme.currencyGold;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
        }

        // Borda de prestígio (2026-07-07) — anel metálico/dourado pulsante atrás do badge,
        // visível só quando prestigeLevel >= 1 (valor > 400). Criada ANTES do badge (sibling
        // mais antigo = desenha atrás), levemente maior (44×44 vs 36×36 do badge) e concêntrica
        // com ele (mesmo ponto de ancoragem 0.24/0.5, pivot central, deslocada +18px em X pra
        // coincidir com o centro do badge — que usa pivot (0,0.5) e começa nesse mesmo ponto).
        var prestigeBorderGo = new GameObject("PrestigeBorder");
        prestigeBorderGo.transform.SetParent(rowContainer.transform, false);
        var pbRt = prestigeBorderGo.AddComponent<RectTransform>();
        pbRt.anchorMin = new Vector2(badgeAnchorX, 0.5f); pbRt.anchorMax = new Vector2(badgeAnchorX, 0.5f);
        pbRt.pivot = new Vector2(0.5f, 0.5f);
        pbRt.anchoredPosition = new Vector2(18f, 0f);
        pbRt.sizeDelta = new Vector2(44f, 44f);
        var prestigeBorderImg = prestigeBorderGo.AddComponent<Image>();
        prestigeBorderImg.sprite = UIShapeUtil.RoundedRect(PrestigeColor, 22f);
        prestigeBorderImg.type = Image.Type.Sliced;
        prestigeBorderGo.AddComponent<PulsingAlpha>();
        prestigeBorderGo.SetActive(false);

        // Badge circular: RoundedRect com raio = metade do lado vira um círculo (mesma técnica
        // já usada nos badges de level em pill, só que com sizeDelta quadrado em vez de retangular).
        var badgeGo = new GameObject("Badge");
        badgeGo.transform.SetParent(rowContainer.transform, false);
        var brt = badgeGo.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(badgeAnchorX, 0.5f); brt.anchorMax = new Vector2(badgeAnchorX, 0.5f);
        brt.pivot = new Vector2(0f, 0.5f);
        brt.sizeDelta = new Vector2(36f, 36f);
        var badgeImg = badgeGo.AddComponent<Image>();
        badgeImg.sprite = UIShapeUtil.RoundedRect(Color.white, 18f);
        badgeImg.type = Image.Type.Sliced;
        var badgeText = AddCenteredLabel(badgeGo, "0", 16, theme.textOnLight);

        // Texto "×N" de prestígio — canto superior direito do badge, também escondido por padrão.
        var prestigeTextGo = new GameObject("PrestigeText");
        prestigeTextGo.transform.SetParent(rowContainer.transform, false);
        var ptRt = prestigeTextGo.AddComponent<RectTransform>();
        ptRt.anchorMin = new Vector2(badgeAnchorX, 0.5f); ptRt.anchorMax = new Vector2(badgeAnchorX, 0.5f);
        ptRt.pivot = new Vector2(0f, 0f);
        ptRt.anchoredPosition = new Vector2(28f, 14f);
        ptRt.sizeDelta = new Vector2(32f, 16f);
        var prestigeText = prestigeTextGo.AddComponent<TextMeshProUGUI>();
        prestigeText.fontSize = 11; prestigeText.fontStyle = FontStyles.Bold;
        prestigeText.color = PrestigeColor;
        prestigeText.alignment = TextAlignmentOptions.MidlineLeft;
        prestigeTextGo.SetActive(false);

        var pipsGo = new GameObject("Pips");
        pipsGo.transform.SetParent(rowContainer.transform, false);
        var prt = pipsGo.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(pipsAnchorMinX, 0.18f); prt.anchorMax = new Vector2(1f, 0.82f);
        prt.offsetMin = Vector2.zero; prt.offsetMax = new Vector2(-8f, 0f);
        var hlg = pipsGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        var pips = new Image[PipCount];
        for (int i = 0; i < PipCount; i++)
        {
            var pipGo = new GameObject($"Pip{i}");
            pipGo.transform.SetParent(pipsGo.transform, false);
            pipGo.AddComponent<RectTransform>();
            var pipImg = pipGo.AddComponent<Image>();
            pipImg.sprite = UIShapeUtil.RoundedRect(Color.white, 4f);
            pipImg.type = Image.Type.Sliced;
            pips[i] = pipImg;
        }

        return new AttributePipBar(badgeImg, badgeText, pips, prestigeBorderGo, prestigeText);
    }

    // Ícone de HP (coração) — mesmo motivo de Resources.Load acima. PNG original do usuário em
    // Assets/Resources/UI/Attributes/HP.png.
    public static readonly Sprite HpIcon = Resources.Load<Sprite>("UI/Attributes/HP");

    // Ícone com um número branco centralizado por cima dele (2026-07-16, pedido do usuário —
    // "removemos [o texto] HP, deixamos um coração com uma string branca centralizada nele") —
    // usado nos 3 lugares que mostravam "N HP" como texto puro (CharacterPanel, reaproveitado
    // por 01_MainMenu/02_SelectCharacter, e SelectOpponentController). `parent` já vem
    // posicionado/dimensionado pelo chamador (mesmo padrão do resto da classe); devolve o
    // TMP_Text do número pra quem chama poder atualizar o valor depois (ex: RefreshAll), já que
    // o valor muda em runtime mas o ícone não.
    // `iconScale` (2026-07-18, pedido do usuário — coração menor em `05_SelectOpponent`, 1.8x em
    // vez do 2.7x padrão) — default preserva o comportamento original de todo chamador existente.
    public static TMP_Text BuildIconWithValue(GameObject parent, Sprite icon, float fontSize, float iconScale = 2.7f)
    {
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(parent.transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
        var img = iconGo.AddComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        // Scale 2.7x por padrão (2026-07-16, pedido do usuário — mesmo motivo do 3x em Build acima,
        // mas um pouco menor pro coração de HP não dominar visualmente sobre STR/AGI/SPD).
        iconRt.localScale = new Vector3(iconScale, iconScale, 1f);

        var valGo = new GameObject("Value");
        valGo.transform.SetParent(parent.transform, false);
        var vrt = valGo.AddComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        var txt = valGo.AddComponent<TextMeshProUGUI>();
        // Bug real (2026-07-17, reportado pelo usuário): HP >= 100 (3 dígitos) quebrava em 2
        // linhas dentro desta caixa pequena (34px em CharacterPanel, 22px em
        // SelectOpponentController) — TMP tem `enableWordWrapping = true` por padrão, e "100"
        // não cabia numa linha só no fontSize configurado. `enableWordWrapping = false` corta o
        // problema pela raiz (nunca quebra linha); `enableAutoSizing` com piso em 60% do
        // `fontSize` pedido garante que 3 dígitos ainda encolhem pra caber em vez de vazar pra
        // fora da caixa (1-2 dígitos continuam no tamanho cheio, que é o caso comum).
        txt.enableWordWrapping = false;
        txt.enableAutoSizing = true;
        txt.fontSizeMin = fontSize * 0.6f;
        txt.fontSizeMax = fontSize;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Center;
        return txt;
    }

    public void SetValue(int value)
    {
        int v = Mathf.Max(0, value);
        int tier = v <= 0 ? 0 : (v - 1) / TierSize; // tier "cru", pode passar de 39 (sem reciclar ainda)

        // Badge: cor do tier do valor ATUAL (topo da janela).
        _badgeText.text = $"{v}";
        _badgeImg.color = TierPalette[WrapTier(tier)];

        // Prestígio (2026-07-07): tier >= 40 (valor > 400) = pelo menos uma volta completa na
        // paleta de 40 cores. Borda dourada pulsante sempre que houver prestígio; texto "×N"
        // (N = prestigeLevel+1, ex: ×2 na 2ª volta, ×3 na 3ª) junto dela.
        int prestigeLevel = tier / TierPaletteSize;
        bool hasPrestige = prestigeLevel >= 1;
        _prestigeBorderGo.SetActive(hasPrestige);
        _prestigeText.gameObject.SetActive(hasPrestige);
        if (hasPrestige) _prestigeText.text = $"×{prestigeLevel + 1}";

        // Pips: janela deslizante dos últimos 10 pontos (ver ComputeSlidingWindowColors) — a
        // MESMA função pura pra STR/AGI/SPD, sem nenhum tint/alpha extra aplicado aqui.
        Color[] pipColors = ComputeSlidingWindowColors(value);
        for (int i = 0; i < _pips.Length; i++)
            _pips[i].color = pipColors[i];
    }

    // Janela deslizante de 10 blocos. Regra: início = max(1, V-9), fim = V — os 10 blocos
    // representam esse intervalo de pontos. A cor de cada bloco vem do tier do PONTO que ele
    // representa (`tier = (ponto-1) / 10`, buscado em `TierPalette`), não do tier do valor V
    // inteiro — por isso um valor na transição entre tiers (ex: V=12, V=24) mistura cores de
    // dois tiers na mesma barra. Função PURA: só depende de `value`, devolve um array de 10
    // cores (uma por bloco, na ordem) — STR/AGI/SPD chamam exatamente esta função, nenhuma
    // implementa a lógica por conta própria.
    //
    // Ordem dos blocos: quando V >= 10 (janela cheia, sempre 10 pontos existentes), o bloco MAIS
    // À ESQUERDA (índice 0) representa o ponto MAIS ALTO (V, o mais recente), decrescendo até o
    // mais à DIREITA representar o ponto mais baixo da janela (V-9) — assim, conforme o valor
    // sobe, blocos de um tier mais alto vão "empurrando" os do tier anterior da esquerda pra
    // direita, até tomar a barra inteira. Quando V < 10 (janela ainda não cheia, início forçado
    // pra 1), a ordem continua ascendente da esquerda pra direita (bloco 0 = ponto 1, ..., blocos
    // além de V ficam vazios à direita).
    //   V=6  → blocos [1,2,3,4,5,6,vazio,vazio,vazio,vazio] (6 tier 0 à esquerda + 4 vazios)
    //   V=12 → blocos [12,11,10,9,8,7,6,5,4,3] → 2 tier 1 à esquerda + 8 tier 0 à direita
    //   V=24 → blocos [24,23,22,21,20,19,18,17,16,15] → 4 tier 2 à esquerda + 6 tier 1 à direita
    public static Color[] ComputeSlidingWindowColors(int value)
    {
        var result = new Color[PipCount];
        int v = Mathf.Max(0, value);

        // Prévia (blocos ainda não alcançados, só possível quando V<10): mesmo tom do tier 0
        // (único tier possível nesse caso), em alpha baixo.
        Color notReached = new Color(TierPalette[0].r, TierPalette[0].g, TierPalette[0].b, 0.18f);

        bool fullWindow = v >= PipCount;
        for (int i = 0; i < PipCount; i++)
        {
            // Janela cheia: bloco 0 = ponto V, decrescendo (mais recente à esquerda). Janela
            // parcial (V<10): bloco 0 = ponto 1, crescendo (ordem original, inalterada).
            int point = fullWindow ? v - i : i + 1;
            if (point <= v && point >= 1)
            {
                int tier = (point - 1) / TierSize;
                result[i] = TierPalette[WrapTier(tier)];
            }
            else
            {
                result[i] = notReached;
            }
        }
        return result;
    }

    // Recicla qualquer tier "cru" (pode vir >= 40 pra valores > 400) pro intervalo válido da
    // paleta (0-39) — "tier efetivo para cor = tier % 40" do pedido do usuário.
    private static int WrapTier(int tier) => ((tier % TierPaletteSize) + TierPaletteSize) % TierPaletteSize;

    // Cor dourada/metálica da borda e do texto de prestígio — tom fixo, não faz parte da
    // paleta de 40 tiers (é um indicador à parte, não uma cor de tier).
    private static readonly Color PrestigeColor = new Color(0.83f, 0.69f, 0.22f, 1f);

    // Tabela de 40 cores calculada UMA VEZ (inicializador de campo `static readonly`, roda no
    // carregamento da classe — "calculada uma vez", não a cada frame) chamando GetColorForTier
    // pra cada tier 0-39. Todo lookup em runtime (SetValue/ComputeSlidingWindowColors) usa esta
    // tabela, nunca recalcula HSL→RGB.
    private static readonly Color[] TierPalette = BuildTierPalette();

    private static Color[] BuildTierPalette()
    {
        var palette = new Color[TierPaletteSize];
        for (int t = 0; t < TierPaletteSize; t++)
            palette[t] = GetColorForTier(t);
        return palette;
    }

    // Função pura: tier (qualquer inteiro, reciclado internamente pra 0-39) → cor HSL. 4 "eras"
    // de 10 matizes cada (hue = índice-dentro-da-era × 36°, cobrindo o círculo de cor inteiro em
    // cada era) — era 0 pastel/clara (S 0.55, L 0.75), era 1 mais viva (S 0.70, L 0.60), era 2
    // saturada e mais escura (S 0.85, L 0.42), era 3 escura/intensa (S 0.75, L 0.22). Usada só
    // pra CONSTRUIR `TierPalette` acima — chamadas em runtime devem ler a tabela, não este
    // método diretamente (custo de HSL→RGB por chamada).
    public static Color GetColorForTier(int tier)
    {
        int t = WrapTier(tier);
        int era = t / TiersPerEra;
        int hueIndex = t % TiersPerEra;
        float hue = hueIndex * 36f;
        float s, l;
        switch (era)
        {
            case 0: s = 0.55f; l = 0.75f; break;
            case 1: s = 0.70f; l = 0.60f; break;
            case 2: s = 0.85f; l = 0.42f; break;
            default: s = 0.75f; l = 0.22f; break; // era 3
        }
        return HslToRgb(hue, s, l);
    }

    // Conversão HSL→RGB padrão (H em graus 0-360, S/L em 0-1) — Unity só tem Color.HSVToRGB
    // (HSV, modelo diferente de HSL), então implementada direto em vez de adaptar os parâmetros
    // (adaptação HSL→HSV existe mas é menos direta/mais propensa a erro que a fórmula padrão).
    private static Color HslToRgb(float h, float s, float l)
    {
        h = ((h % 360f) + 360f) % 360f;
        float c = (1f - Mathf.Abs(2f * l - 1f)) * s;
        float x = c * (1f - Mathf.Abs((h / 60f) % 2f - 1f));
        float m = l - c / 2f;
        float r1, g1, b1;
        if (h < 60f)       { r1 = c; g1 = x; b1 = 0f; }
        else if (h < 120f) { r1 = x; g1 = c; b1 = 0f; }
        else if (h < 180f) { r1 = 0f; g1 = c; b1 = x; }
        else if (h < 240f) { r1 = 0f; g1 = x; b1 = c; }
        else if (h < 300f) { r1 = x; g1 = 0f; b1 = c; }
        else               { r1 = c; g1 = 0f; b1 = x; }
        return new Color(r1 + m, g1 + m, b1 + m, 1f);
    }

    private static TMP_Text AddCenteredLabel(GameObject parent, string text, float size, Color color)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text; txt.fontSize = size; txt.color = color;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        return txt;
    }
}

// Pulsa o alpha do próprio Image via seno — só existe pra dar vida à borda de prestígio.
// AttributePipBar não é MonoBehaviour (classe C# pura, sem Update próprio), por isso esse
// comportamento fica num componente pequeno e dedicado, anexado só ao GameObject da borda.
public class PulsingAlpha : MonoBehaviour
{
    private const float Speed = 2.2f;
    private const float MinAlpha = 0.45f;
    private const float MaxAlpha = 1f;

    private Image _img;
    private Color _baseColor;

    private void Awake()
    {
        _img = GetComponent<Image>();
        _baseColor = _img.color;
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.time * Speed) + 1f) * 0.5f; // 0..1
        float alpha = Mathf.Lerp(MinAlpha, MaxAlpha, t);
        _img.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, _baseColor.a * alpha);
    }
}
