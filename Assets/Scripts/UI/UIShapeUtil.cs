using UnityEngine;
using System.Collections.Generic;

// Sprites de retângulo arredondado gerados em runtime (mesmo espírito das texturas
// procedurais já usadas no projeto, ex: CombatPlayer.PlayWeaponTipEffect) — dá cantos
// arredondados a painéis/badges/ícones de UI construídos 100% via código, sem depender de
// nenhum sprite externo. SpriteMeshType.FullRect + borda 9-slice (mesmo raio nos 4 lados)
// mantém o raio do canto constante em qualquer tamanho de Image, desde que Image.type
// seja Sliced.
public static class UIShapeUtil
{
    private const int TextureSize = 64;

    // Cache por (cor, raio) — 2026-07-07: qualquer chamada repetida com os MESMOS parâmetros
    // (ex: os 3+ badges/30 pips de AttributePipBar, todos gerados via `RoundedRect(Color.white,
    // ...)`, ou os ícones com borda por tier) agora reutiliza literalmente o mesmo `Sprite`/
    // `Texture2D`, em vez de gerar uma textura nova a cada chamada. Investigação de um relato
    // de "SPD com cor mais saturada que AGI no mesmo tier": a matemática de tint (`Image.color`
    // multiplicando uma textura branca) já era exata e compartilhada (`AttributePipBar.
    // GetColorForTier`/`TierPalette`), sem nenhuma divergência possível no código — o cache
    // elimina de vez qualquer dúvida teórica remanescente (garante o MESMO objeto Sprite, não
    // só valores "iguais"), além de evitar gerar dezenas de texturas repetidas à toa.
    private static readonly Dictionary<(Color, float), Sprite> _cache = new Dictionary<(Color, float), Sprite>();

    public static Sprite RoundedRect(Color color, float radius = 16f)
    {
        radius = Mathf.Clamp(radius, 0f, TextureSize / 2f);
        var key = (color, radius);
        // `cached != null` (2026-07-25, bug real corrigido — reportado pelo usuário: botões/fundos
        // brancos em todas as telas, só depois de reiniciar o Play Mode sem fechar o Editor) —
        // Domain Reload desligado (ver ProjectSettings/EditorSettings.asset) faz este cache
        // estático SOBREVIVER entre sessões de Play, mas os Sprite/Texture2D gerados em runtime na
        // sessão ANTERIOR são destruídos pela própria Unity ao sair do Play Mode — sem essa
        // checagem, `TryGetValue` continuava achando a entrada (a chave em si nunca expira) e
        // devolvia um Sprite "morto" (`!= null` do Unity detecta objeto destruído mesmo com
        // referência C# não-nula), renderizando como branco. Mesmo fix nos outros 3 caches abaixo.
        if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        float half = TextureSize / 2f;
        float inner = half - radius;
        var pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - half) - inner);
                float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - half) - inner);
                bool inside = (dx * dx + dy * dy) <= radius * radius;
                pixels[y * TextureSize + x] = inside ? color : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        var sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        _cache[key] = sprite;
        return sprite;
    }

    // Textura 1px de largura com gradiente vertical top→bottom, esticada pra qualquer tamanho de
    // Image (Image.type = Simple) — usa UITheme.backgroundTop/backgroundBottom como pano de
    // fundo de tela cheia (primeiro uso real desses dois campos no projeto).
    private static readonly Dictionary<(Color, Color), Sprite> _gradientCache = new Dictionary<(Color, Color), Sprite>();

    public static Sprite VerticalGradient(Color top, Color bottom, int resolution = 64)
    {
        var key = (top, bottom);
        if (_gradientCache.TryGetValue(key, out var cached) && cached != null) return cached;

        var texture = new Texture2D(1, resolution, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color[resolution];
        for (int y = 0; y < resolution; y++)
            pixels[y] = Color.Lerp(bottom, top, y / (float)(resolution - 1));
        texture.SetPixels(pixels);
        texture.Apply();

        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, resolution), new Vector2(0.5f, 0.5f), 100f);
        _gradientCache[key] = sprite;
        return sprite;
    }

    // Estrela de 5 pontas rasterizada em runtime (mesmo espírito de RoundedRect acima) — usada
    // pelo favorito de CharacterCardUI. Existe porque o glyph Unicode "★"/"☆" não está incluso
    // no atlas da fonte TMP do projeto (renderizava como um quadrado "tofu" em vez da estrela,
    // bug reportado pelo usuário) — em vez de depender de cobertura de fonte ou de importar
    // sprites externos, desenha o polígono direto (ray-casting point-in-polygon, 10 vértices
    // alternando raio externo/interno). `filled=false` desenha só o contorno (mesmo polígono
    // encolhido subtraído do polígono cheio, formando um anel).
    private static readonly Dictionary<(Color, bool), Sprite> _starCache = new Dictionary<(Color, bool), Sprite>();

    public static Sprite Star(Color color, bool filled)
    {
        var key = (color, filled);
        if (_starCache.TryGetValue(key, out var cached) && cached != null) return cached;

        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        float half = TextureSize / 2f;
        float outerR = half - 2f;
        float innerR = outerR * 0.45f;
        const float OutlineThickness = 0.22f; // fração do raio externo, só usada quando !filled
        var outerPts = StarPoints(half, half, outerR, innerR);
        var innerPts = filled ? null : StarPoints(half, half, outerR * (1f - OutlineThickness), innerR * (1f - OutlineThickness));

        var pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                bool insideOuter = PointInPolygon(p, outerPts);
                bool inside = filled ? insideOuter : insideOuter && !PointInPolygon(p, innerPts);
                pixels[y * TextureSize + x] = inside ? color : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        var sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
        _starCache[key] = sprite;
        return sprite;
    }

    // Triângulo de "play" apontando pra direita, rasterizado em runtime (mesmo espírito de Star
    // acima) — usado pelo botão de assistir replay do CharacterPanel (2026-07-19). Existe pelo
    // mesmo motivo de Star: o glifo Unicode "▶" não está incluso no atlas da fonte TMP do
    // projeto (viraria "tofu" quebrado, mesma classe de bug já documentada com "★"/"☆" e "—").
    private static readonly Dictionary<Color, Sprite> _playTriangleCache = new Dictionary<Color, Sprite>();

    public static Sprite PlayTriangle(Color color)
    {
        if (_playTriangleCache.TryGetValue(color, out var cached) && cached != null) return cached;

        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        // Vértices centralizados no canvas 64×64 — ápice à direita, base vertical à esquerda.
        var pts = new[]
        {
            new Vector2(20f, 18f),
            new Vector2(20f, 46f),
            new Vector2(48f, 32f),
        };

        var pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                pixels[y * TextureSize + x] = PointInPolygon(p, pts) ? color : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        var sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
        _playTriangleCache[color] = sprite;
        return sprite;
    }

    private static Vector2[] StarPoints(float cx, float cy, float outerR, float innerR, int spikes = 5)
    {
        var pts = new Vector2[spikes * 2];
        float angleStep = Mathf.PI / spikes;
        // +90° (não -90°): Texture2D/Sprite tem y=0 na base (convenção padrão da Unity), então
        // sin(angle)>0 sobe na imagem — -90° apontava pra BAIXO, deixando a estrela de cabeça
        // pra baixo (bug reportado pelo usuário).
        float rot = Mathf.PI / 2f; // 1º vértice apontando pra cima
        for (int i = 0; i < spikes * 2; i++)
        {
            float r = (i % 2 == 0) ? outerR : innerR;
            float angle = rot + i * angleStep;
            pts[i] = new Vector2(cx + Mathf.Cos(angle) * r, cy + Mathf.Sin(angle) * r);
        }
        return pts;
    }

    // Ray-casting padrão (par/ímpar de cruzamentos com as arestas do polígono).
    private static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        int j = poly.Length - 1;
        for (int i = 0; i < poly.Length; i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }
}
