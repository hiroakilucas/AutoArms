using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Os 3 Animator Controllers de pets (Boar/Monkey/Mouse, gerados pelo Spriter2UnityDX a partir
// do .scml) vieram sem nenhum parâmetro nem transição (m_AnimatorParameters: [], todo estado
// com m_Transitions: []) — diferente dos personagens principais, que já tinham isso ajustado
// manualmente no Editor. O Boar também tem estados decoy deixados de uma versão anterior do
// rig (Sleep/Walking/Base/"Jump Loop", esse último apontando pra um clipe embutido diferente
// do Jumping.anim avulso) — ver CLAUDE.md/spec do Pets: "o clip de esquiva do Monkey e Boar
// chama-se 'Jumping' (não 'Jump_Loop')". Em vez de tentar reaproveitar/rewire os estados
// existentes (alguns sem garantia de qual clipe embutido realmente usam), este gerador recria
// do zero as 6 states que os pets realmente precisam, apontando direto pros .anim avulsos em
// Assets/Data/UI/Pets/<Tipo>/Animations/, com parâmetros e transições equivalentes (versão
// simplificada) ao padrão dos personagens principais (ver Animator Controller Architecture no
// CLAUDE.md).
public static class PetAnimatorSetup
{
    private struct PetSpec
    {
        public string Name;
        public string ControllerPath;
        public string AnimFolder;
    }

    private static readonly PetSpec[] Pets =
    {
        new PetSpec { Name = "Boar",   ControllerPath = "Assets/Data/UI/Pets/Boar/Vector Parts/Boar.controller",     AnimFolder = "Assets/Data/UI/Pets/Boar/Animations" },
        new PetSpec { Name = "Monkey", ControllerPath = "Assets/Data/UI/Pets/Monkey/Vector Parts/Monkey.controller", AnimFolder = "Assets/Data/UI/Pets/Monkey/Animations" },
        new PetSpec { Name = "Mouse",  ControllerPath = "Assets/Data/UI/Pets/Mouse/Vector Parts/Mouse.controller",   AnimFolder = "Assets/Data/UI/Pets/Mouse/Animations" },
    };

    [MenuItem("Tools/AutoArms/Setup Pet Animators")]
    public static void SetupAll()
    {
        foreach (var pet in Pets)
            Setup(pet);
        AssetDatabase.SaveAssets();
        Debug.Log("[PetAnimatorSetup] Animator Controllers de pets configurados (Boar/Monkey/Mouse).");
    }

    private static void Setup(PetSpec pet)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(pet.ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[PetAnimatorSetup] Controller não encontrado em {pet.ControllerPath}");
            return;
        }

        var clips = new Dictionary<string, AnimationClip>();
        foreach (var clipName in new[] { "Idle", "Running", "Slashing", "Hurt", "Jumping", "Dying" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{pet.AnimFolder}/{clipName}.anim");
            if (clip == null)
            {
                Debug.LogError($"[PetAnimatorSetup] {pet.Name}: clipe '{clipName}.anim' não encontrado em {pet.AnimFolder}");
                continue;
            }
            clips[clipName] = clip;
        }

        // Idle/Running tocam em loop; Slashing/Hurt/Jumping tocam uma vez e voltam pro Idle;
        // Dying toca uma vez e TRAVA no último frame (sem transição de saída, ver abaixo).
        SetLoop(clips, "Idle", true);
        SetLoop(clips, "Running", true);
        SetLoop(clips, "Slashing", false);
        SetLoop(clips, "Hurt", false);
        SetLoop(clips, "Jumping", false);
        SetLoop(clips, "Dying", false);

        var sm = controller.layers[0].stateMachine;

        // Remove todos os estados existentes (decoy/embutidos do Boar, ou estados sem
        // parâmetro/transição nenhum do Monkey/Mouse) — recriados do zero abaixo.
        foreach (var child in sm.states)
            sm.RemoveState(child.state);

        // RemoveState não garante que TODO sub-asset órfão (transições de "Any State", de
        // sub-state machines, ou qualquer AnimatorTransition/AnimatorState que tenha sobrado
        // fisicamente embutido no arquivo .controller de versões anteriores — incluindo a 1ª
        // tentativa deste mesmo gerador antes deste fix) seja de fato removido do arquivo —
        // o legacy graph view do Animator window (UnityEditor.Graphs, usado pra desenhar o
        // grafo de estados) enumera TODOS os sub-assets físicos do arquivo, não só os
        // referenciados por sm.states/anyStateTransitions; uma edge órfã sobrando (origem/
        // destino apontando pra um objeto já destruído) crasha o Animator window com
        // NullReferenceException em UnityEditor.Graphs.Edge.WakeUp() — reproduzido mesmo só
        // zerando anyStateTransitions, então a purga abaixo é mais agressiva: destrói TODO
        // sub-asset do arquivo exceto o AnimatorController principal e o AnimatorStateMachine
        // raiz da própria layer (esse precisa sobreviver — é o que controller.layers[0]
        // referencia; destruí-lo quebraria o controller inteiro).
        var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(pet.ControllerPath);
        foreach (var asset in allSubAssets)
        {
            if (asset == null || asset == controller || asset == sm) continue;
            Object.DestroyImmediate(asset, true);
        }

        // Depois da purga física acima, garante que os arrays do próprio sm não carreguem mais
        // nenhuma referência (agora "missing") pros objetos destruídos.
        sm.states = new ChildAnimatorState[0];
        sm.anyStateTransitions = new AnimatorStateTransition[0];
        sm.stateMachines = new ChildAnimatorStateMachine[0];
        sm.defaultState = null;

        while (controller.parameters.Length > 0)
            controller.RemoveParameter(0);

        controller.AddParameter("Idle", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Running", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Slashing", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jumping", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dying", AnimatorControllerParameterType.Trigger);

        AnimatorState MakeState(string name, Vector3 pos)
        {
            var state = sm.AddState(name, pos);
            if (clips.TryGetValue(name, out var clip)) state.motion = clip;
            return state;
        }

        var idleState     = MakeState("Idle",     new Vector3(280, 0, 0));
        var runningState  = MakeState("Running",  new Vector3(280, 60, 0));
        var slashingState = MakeState("Slashing", new Vector3(280, 120, 0));
        var hurtState     = MakeState("Hurt",     new Vector3(280, 180, 0));
        var jumpingState  = MakeState("Jumping",  new Vector3(280, 240, 0));
        var dyingState    = MakeState("Dying",    new Vector3(280, 300, 0));

        sm.defaultState = idleState;

        // Idle <-> Running, por bool — blend time 0s (sem crossfade) e Has Exit Time = false,
        // pedido pelo usuário pra eliminar qualquer engasgo residual no loop de Idle/Running.
        var idleToRun = idleState.AddTransition(runningState);
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0f;
        idleToRun.AddCondition(AnimatorConditionMode.If, 0, "Running");

        var runToIdle = runningState.AddTransition(idleState);
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0f;
        runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Running");

        // AnyState -> Slashing/Hurt/Jumping/Dying via trigger, sem exit time — dispara
        // imediatamente, CanTransitionToSelf=true só em Slashing (permite re-entrar pra
        // sustentar combo/hits extras de pet, mesmo padrão dos personagens principais).
        void AnyStateTo(AnimatorState target, string trigger, bool canSelf)
        {
            var t = sm.AddAnyStateTransition(target);
            t.hasExitTime = false;
            t.duration = 0.05f;
            t.canTransitionToSelf = canSelf;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        }
        AnyStateTo(slashingState, "Slashing", true);
        AnyStateTo(hurtState, "Hurt", false);
        AnyStateTo(jumpingState, "Jumping", false);
        AnyStateTo(dyingState, "Dying", false);

        // Slashing/Hurt/Jumping voltam pro Idle automaticamente depois de tocar quase tudo do
        // clipe (sem precisar de nenhum bool/trigger extra do lado de fora).
        void ExitToIdle(AnimatorState from)
        {
            var t = from.AddTransition(idleState);
            t.hasExitTime = true;
            t.exitTime = 0.9f;
            t.duration = 0.05f;
        }
        ExitToIdle(slashingState);
        ExitToIdle(hurtState);
        ExitToIdle(jumpingState);
        // Dying: SEM transição de saída — trava no último frame (Loop Time=false já garante
        // isso) até o fim da luta. Preparação pra Tamer (CLAUDE.md): pet morto fica no chão.

        EditorUtility.SetDirty(controller);
        Debug.Log($"[PetAnimatorSetup] {pet.Name}: 6 estados/parâmetros configurados.");
    }

    private static void SetLoop(Dictionary<string, AnimationClip> clips, string name, bool loop)
    {
        if (!clips.TryGetValue(name, out var clip)) return;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime != loop)
        {
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }
    }
}
