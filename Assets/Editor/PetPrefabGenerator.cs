using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// As animações dos pets (Idle/Running/Slashing/Hurt/Jumping/Dying, em
// Assets/Data/UI/Pets/<Tipo>/Animations/*.anim) são flipbooks simples — cada clipe troca o
// m_Sprite de UM SpriteRenderer no path="" (a própria GameObject que tem o Animator), ciclando
// os frames de Assets/Data/UI/Pets/<Tipo>/PNG Sequences/<Estado>/*.png (confirmado lendo
// Idle.anim: m_PPtrCurves com path vazio, classID 212/SpriteRenderer, 18 keyframes batendo
// 1:1 com Idle_000..017.png).
//
// O prefab "Vector Parts/<Tipo>.prefab" (rig multi-bone exportado do Spriter, usado até agora
// como o GameObject de gameplay em CombatSceneLoader.PetPrefabFor) tem o Animator na raiz, mas
// SEM NENHUM SpriteRenderer nela — só nos ossos filhos (Head/Body/Tail/etc, cada um com seu
// próprio SpriteRenderer). A curva m_Sprite em path="" não tem componente nenhum pra escrever
// ali, então falha em silêncio (sem warning/erro no Console) — Animator transiciona de estado
// normalmente (Idle/Running/Slashing toggling certinho), a posição em cena muda normalmente
// (MovementController, sem relação com o Animator), mas a pose visual nunca muda: exatamente o
// bug reportado ("anima a posição mas não anima a pose").
//
// Este gerador cria o prefab de gameplay CORRETO: 1 GameObject raiz só com SpriteRenderer +
// Animator (controller já montado por PetAnimatorSetup.cs), sem nenhum osso — compatível com
// as curvas dos clipes. Depois de gerar, trocar os 3 campos boarPetPrefab/monkeyPetPrefab/
// mousePetPrefab no Inspector do CombatSceneLoader (cena 04_CombatScenePVP) pra apontar pra
// esses novos prefabs em vez dos antigos "Vector Parts/<Tipo>.prefab".
public static class PetPrefabGenerator
{
    private struct PetSpec
    {
        public string Name;
        public string ControllerPath;
        public string FirstFramePath;
        public string OutputPath;
    }

    private static readonly PetSpec[] Pets =
    {
        new PetSpec {
            Name = "Boar",
            ControllerPath = "Assets/Data/UI/Pets/Boar/Vector Parts/Boar.controller",
            FirstFramePath = "Assets/Data/UI/Pets/Boar/PNG Sequences/Idle/Idle_000.png",
            OutputPath = "Assets/Data/UI/Pets/Boar/BoarPet.prefab",
        },
        new PetSpec {
            Name = "Monkey",
            ControllerPath = "Assets/Data/UI/Pets/Monkey/Vector Parts/Monkey.controller",
            FirstFramePath = "Assets/Data/UI/Pets/Monkey/PNG Sequences/Idle/Idle_000.png",
            OutputPath = "Assets/Data/UI/Pets/Monkey/MonkeyPet.prefab",
        },
        new PetSpec {
            Name = "Mouse",
            ControllerPath = "Assets/Data/UI/Pets/Mouse/Vector Parts/Mouse.controller",
            FirstFramePath = "Assets/Data/UI/Pets/Mouse/PNG Sequences/Idle/Idle_000.png",
            OutputPath = "Assets/Data/UI/Pets/Mouse/MousePet.prefab",
        },
    };

    [MenuItem("Tools/AutoArms/Generate Pet Gameplay Prefabs")]
    public static void GenerateAll()
    {
        foreach (var pet in Pets)
            Generate(pet);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PetPrefabGenerator] Prefabs de gameplay dos pets gerados (Boar/Monkey/Mouse) — wirear nos campos do CombatSceneLoader.");
    }

    private static void Generate(PetSpec pet)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(pet.ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[PetPrefabGenerator] Controller não encontrado em {pet.ControllerPath} — rode Tools/AutoArms/Setup Pet Animators primeiro.");
            return;
        }

        var firstFrame = AssetDatabase.LoadAssetAtPath<Sprite>(pet.FirstFramePath);
        if (firstFrame == null)
        {
            Debug.LogError($"[PetPrefabGenerator] Sprite inicial não encontrado em {pet.FirstFramePath}");
            return;
        }

        var go = new GameObject(pet.Name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = firstFrame;
        sr.sortingLayerName = "Characters";

        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        PrefabUtility.SaveAsPrefabAsset(go, pet.OutputPath);
        Object.DestroyImmediate(go);

        Debug.Log($"[PetPrefabGenerator] {pet.Name}: prefab gerado em {pet.OutputPath}");
    }
}
