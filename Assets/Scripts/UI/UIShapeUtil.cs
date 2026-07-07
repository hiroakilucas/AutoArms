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
        if (_cache.TryGetValue(key, out var cached)) return cached;

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
}
