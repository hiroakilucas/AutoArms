using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Monta o prefab de VFX da skill Piledriver a partir dos frames Explosion_1..10 (CraftPix) e
// do Animator Controller já existentes em Assets/Data/UI/SkillEffect/Piledriver/ — só faltava
// o GameObject combinando SpriteRenderer + Animator + AnimationAutoDestroy.
public static class PiledriverEffectGenerator
{
    private const string FolderPath      = "Assets/Data/UI/SkillEffect/Piledriver";
    private const string ControllerPath  = FolderPath + "/Explosion_1.controller";
    private const string FirstSpritePath = FolderPath + "/Explosion_1.png";
    private const string PrefabPath      = FolderPath + "/PiledriverExplosion.prefab";

    [MenuItem("Tools/AutoArms/Generate Piledriver Effect Prefab")]
    public static void Generate()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[PiledriverEffectGenerator] Animator Controller não encontrado em {ControllerPath}");
            return;
        }

        // Garante Loop Time = false em todo clipe do controller — sem isso o efeito reiniciaria
        // em loop e AnimationAutoDestroy nunca teria um clip.length de uma única passada pra
        // calcular o delay de destruição.
        foreach (var clip in controller.animationClips)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime)
            {
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
        }

        var firstSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FirstSpritePath);
        if (firstSprite == null)
        {
            Debug.LogError($"[PiledriverEffectGenerator] Sprite não encontrado em {FirstSpritePath}");
            return;
        }

        var go = new GameObject("PiledriverExplosion");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite           = firstSprite;
        sr.sortingLayerName = "Characters";
        sr.sortingOrder     = 20;

        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        go.AddComponent<AnimationAutoDestroy>();

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.Refresh();
        Debug.Log($"[PiledriverEffectGenerator] Prefab gerado em {PrefabPath}");
    }
}
