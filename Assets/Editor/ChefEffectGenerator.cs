using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Monta o prefab de VFX da skill Chef a partir do clipe/controller já criados pelo usuário em
// Assets/Data/UI/SkillEffect/Chef/ (chef.anim — flipbook dos 10 frames Explosion_1..10, verdes —
// e Explosion_1.controller, com o estado "chef" usando esse clipe como motion) — mesmo padrão do
// PiledriverEffectGenerator, só faltava o GameObject combinando SpriteRenderer + Animator.
public static class ChefEffectGenerator
{
    private const string FolderPath      = "Assets/Data/UI/SkillEffect/Chef";
    private const string ControllerPath  = FolderPath + "/Explosion_1.controller";
    private const string FlyingSpritePath = FolderPath + "/chef.png";
    private const string PrefabPath      = FolderPath + "/ChefPizzaPrefab.prefab";

    [MenuItem("Tools/AutoArms/Generate Chef Effect Prefab")]
    public static void Generate()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[ChefEffectGenerator] Animator Controller não encontrado em {ControllerPath}");
            return;
        }

        // Garante Loop Time = false em todo clipe do controller — o clipe "chef" estava com
        // m_LoopTime: 1 (a explosão reiniciaria em loop em vez de tocar uma única vez, e
        // AnimationAutoDestroy nunca teria um clip.length de uma única passada pra calcular o
        // delay de destruição), mesmo bug já visto no Piledriver/Bomb.
        foreach (var clip in controller.animationClips)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime)
            {
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
        }

        var flyingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FlyingSpritePath);
        if (flyingSprite == null)
        {
            Debug.LogError($"[ChefEffectGenerator] Sprite não encontrado em {FlyingSpritePath}");
            return;
        }

        var go = new GameObject("ChefPizzaPrefab");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite           = flyingSprite;
        sr.sortingLayerName = "Characters";
        sr.sortingOrder     = 20;

        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.Refresh();
        Debug.Log($"[ChefEffectGenerator] Prefab gerado em {PrefabPath}");
    }
}
