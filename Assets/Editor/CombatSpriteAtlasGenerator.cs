using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

// Investigação de FPS drop (Profiler: Rendering dominante, baseline ~30fps mesmo SEM nenhum pet
// na luta) — cada rig de personagem (Medieval Warrior/Girl, Assassin Guy) usa 10+ PNGs
// separados por bone (Body.png, Left Arm.png, Head.png, Sword.png, etc., confirmado em
// Assets/Personagens/<Nome>/Graphics/), cada um sua própria Texture2D — o batching de sprites
// do Unity só agrupa SpriteRenderers que compartilham a MESMA textura/material, então sem
// nenhum Sprite Atlas cada bone visível de cada personagem é um draw call separado (~20+ só
// pros 2 rigs principais, antes de armas/HUD/pets). Este gerador empacota só os 3 rigs de
// personagem + Assets/Data/UI/Weapons (sempre ativos em toda luta) — sem nenhuma mudança de
// código adicional necessária, o Unity usa o atlas automaticamente pra qualquer Sprite já
// referenciado nos prefabs/assets existentes assim que ele for empacotado.
//
// Pets ficam DE FORA deste atlas de propósito (eram incluídos numa 1ª versão) — as PNG
// Sequences de cada pet somam MUITOS frames (6 estados × várias dezenas de frames cada, × 3
// pets), inflando bastante a área total a empacotar; como os 2 profiles testados não tinham
// nenhum pet ativo na luta (ver investigação no CLAUDE.md), incluir esse volume todo no mesmo
// atlas só aumentava o risco de forçar um downscale (causa provável do desfoque reportado pelo
// usuário) sem nenhum benefício mensurável no teste que estava sendo feito. Gerar um atlas
// separado pra pets (mesma receita, pastas "PNG Sequences") só quando for testar performance
// com pets de fato em jogo.
public static class CombatSpriteAtlasGenerator
{
    private const string AtlasFolder = "Assets/Data/SpriteAtlas";
    private const string AtlasPath = AtlasFolder + "/CombatAtlas.spriteatlas";

    private static readonly string[] PackFolders =
    {
        "Assets/Personagens/Medieval Warrior/Graphics",
        "Assets/Personagens/Medieval Warrior Girl/Graphics",
        "Assets/Personagens/Assassin Guy/Graphics",
        "Assets/Data/UI/Weapons",
    };

    [MenuItem("Tools/AutoArms/Generate Combat Sprite Atlas")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(AtlasFolder))
            AssetDatabase.CreateFolder("Assets/Data", "SpriteAtlas");

        // Sempre recriado do zero (em vez de reusar um asset existente) — garante que nenhuma
        // configuração de uma geração anterior (ex: o maxTextureSize default que causou o
        // desfoque reportado) sobreviva entre execuções.
        if (AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath) != null)
            AssetDatabase.DeleteAsset(AtlasPath);

        var atlas = new SpriteAtlas();
        atlas.SetPackingSettings(new SpriteAtlasPackingSettings
        {
            enableRotation = false,     // rotação bugaria sprites animados (flipbooks/rig)
            enableTightPacking = false, // sprites com transparência (a maioria aqui) não combinam com tight packing
            padding = 4,
        });
        // FilterMode Bilinear (não Point) de propósito — os PNGs originais de cada bone já
        // importam com filterMode: 1 (Bilinear, confirmado nos .meta) — usar Point deixaria os
        // personagens com bordas pixeladas, diferente do visual atual do resto do jogo. O
        // desfoque reportado não vinha do filtro, vinha do maxTextureSize abaixo (nunca setado
        // antes, então caía no default da plataforma — pequeno demais pra cobrir os 3 rigs +
        // armas somados sem downscale).
        atlas.SetTextureSettings(new SpriteAtlasTextureSettings
        {
            filterMode = FilterMode.Bilinear,
            generateMipMaps = false, // jogo 2D em câmera ortográfica fixa, sem necessidade de mip chain
            readable = false,
            sRGB = true,
        });
        atlas.SetPlatformSettings(new TextureImporterPlatformSettings
        {
            name = "DefaultTexturePlatform",
            overridden = true,
            maxTextureSize = 4096, // grande o bastante pra empacotar os 3 rigs + armas sem downscale
            format = TextureImporterFormat.RGBA32, // sem compressão lossy — sem blocking nas bordas dos sprites
            compressionQuality = 100,
            crunchedCompression = false,
        });
        AssetDatabase.CreateAsset(atlas, AtlasPath);

        int added = 0;
        foreach (var folder in PackFolders)
        {
            var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
            if (folderAsset == null)
            {
                Debug.LogWarning($"[CombatSpriteAtlasGenerator] Pasta não encontrada, pulando: {folder}");
                continue;
            }
            SpriteAtlasExtensions.Add(atlas, new Object[] { folderAsset });
            added++;
        }

        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CombatSpriteAtlasGenerator] CombatAtlas.spriteatlas recriado com {added} pasta(s) " +
                  "(maxTextureSize=4096, RGBA32, sem compressão). Selecione o atlas em Assets/Data/SpriteAtlas/ " +
                  "e clique 'Pack Preview' no Inspector pra empacotar agora (ou deixa pro próprio Build/Play).");
    }
}
