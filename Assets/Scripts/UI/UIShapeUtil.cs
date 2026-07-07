using UnityEngine;

// Sprites de retângulo arredondado gerados em runtime (mesmo espírito das texturas
// procedurais já usadas no projeto, ex: CombatPlayer.PlayWeaponTipEffect) — dá cantos
// arredondados a painéis/badges/ícones de UI construídos 100% via código, sem depender de
// nenhum sprite externo. SpriteMeshType.FullRect + borda 9-slice (mesmo raio nos 4 lados)
// mantém o raio do canto constante em qualquer tamanho de Image, desde que Image.type
// seja Sliced.
public static class UIShapeUtil
{
    private const int TextureSize = 64;

    public static Sprite RoundedRect(Color color, float radius = 16f)
    {
        radius = Mathf.Clamp(radius, 0f, TextureSize / 2f);
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

        return Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
