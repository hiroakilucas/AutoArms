using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Importação incremental dos personagens femininos novos (Amazon_Warrior_3, Archer_2, etc.) —
// o usuário importa só a pasta "Vector Parts" (ou "PNG/Vector Parts") de cada pacote CraftPix um
// de cada vez (controller+prefab auto-gerados pelo Spriter2UnityDX, sem nenhum parâmetro/
// transição/script de combate — confirmado lendo Amazon_Warrior.controller: m_AnimatorParameters
// vazio, m_AnyStateTransitions: [], todo m_Transitions: [] em cada estado) + 1 frame solto da
// animação Idle (nome livre, ex. "0_Amazon_Warrior_Idle_000.png" — não precisa ser exatamente
// esse nome). Rodar este menu a cada personagem novo importado — idempotente (pula pastas já
// processadas, identificadas por já ter um PlayerProfile com o nome esperado).
//
// Reproduz exatamente a state machine de "Assets/Personagens/Assassin Guy/Prefab/Assassin Guy.controller"
// (lida campo a campo — não é um resumo aproximado) e o wiring de componentes/bones do prefab
// dela — mesmo padrão usado por Medieval Warrior. Ver CLAUDE.md "## Animator Controller
// Architecture" para o resumo documentado (este gerador é mais completo: inclui também a
// transição Idle→Hurt e os 2 transições redundantes Running→Slashing/SlashingDagger que o
// resumo do CLAUDE.md não lista, mas que existem de fato no controller da Assassin Guy).
public static class FemaleCharacterImportGenerator
{
    private const string PersonagensRoot = "Assets/Personagens";
    private const string ProfilesFolder = "Assets/ScriptableObjects/PlayerProfiles";
    private const string CharacterDatabasePath = "Assets/ScriptableObjects/Databases/CharacterDatabase.asset";
    private const string AttackSettingsPath = "Assets/Data/Player1Settings.asset";

    // Clipes reaproveitados diretamente (sem duplicar) — mesmo padrão já usado por Medieval
    // Warrior, que aponta pro "Catch Weapon.anim" da Assassin Guy.
    private const string BlockClipGuid = "777bf93aaaaf202498e2a1e0b012e12a";           // Medieval Warrior/Prefab/Block.anim
    private const string CatchWeaponClipGuid = "478bd109963d38d46b36266bc1679f0f";     // Assassin Guy/Prefab/Catch Weapon.anim
    private const string SlashingDaggerClipGuid = "d02b8425b9c4a8b4fb99cc10e83a7cbf";   // Assassin Guy/Prefab/Slashing Dagger.anim

    private static readonly string[] SkipFolders = { "00-SplashArt", "Assassin Guy", "Medieval Warrior", "Medieval Warrior Girl" };
    private const string AssassinGuyControllerPath = "Assets/Personagens/Assassin Guy/Prefab/Assassin Guy.controller";

    // Doadores conhecidos de golpe de cima pra baixo, testados em ordem. IMPORTANTE: cada pacote
    // CraftPix tem sua PRÓPRIA numeração interna de bone (bone_000, bone_001...), atribuída pelo
    // Spriter por projeto — não é um índice compartilhado entre personagens diferentes, só entre
    // os que vieram do mesmo "template" (Assassin Guy/Medieval Warrior/Amazon Warrior coincidem
    // porque são o mesmo tipo de guerreiro corpo-a-corpo). Confirmado com a Archer (arqueira):
    // o bone_006 da Assassin Guy/Medieval Warrior é a raiz do braço-espada (bone_006/bone_000,
    // bone_006/bone_002/bone_003/Sword...) — na Archer, bone_006 é o osso pai da PERNA ESQUERDA,
    // sem nenhum desses filhos. Aplicar esse clipe nela girava a perna em vez do braço (bug real
    // reportado pelo usuário: "perde uma das pernas e o braço"). Por isso nunca aplicar um doador
    // sem antes medir compatibilidade real dos caminhos de curva contra a hierarquia do alvo
    // (ClipCompatibilityFraction) — abaixo do piso, fica sem Motion e sinaliza, em vez de aplicar
    // algo quebrado em silêncio.
    private const float SlashDonorMinCompatibility = 0.7f;
    private static readonly string[] SlashDonorControllerPaths =
    {
        AssassinGuyControllerPath, // resolve pro clipe embutido no Medieval Warrior na prática
        "Assets/Personagens/Amazon_Warrior_3/Prefab/Amazon_Warrior.controller",
    };

    private static Motion FindSlashMotion(string controllerPath)
    {
        var donorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (donorController == null) return null;
        var slash = donorController.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Slashing");
        return slash != null ? slash.motion : null;
    }

    // Fração dos caminhos de curva do clipe (bones/objetos animados) que resolvem de fato dentro
    // da hierarquia de `root` — base tanto do relatório de compatibilidade (Block/Catch Weapon/
    // Slashing Dagger, já existentes) quanto da escolha de doador de Slashing (novo).
    private static float ClipCompatibilityFraction(GameObject root, AnimationClip clip, out int resolved, out int total)
    {
        var paths = AnimationUtility.GetCurveBindings(clip).Select(b => b.path)
            .Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip).Select(b => b.path))
            .Distinct().ToList();
        total = paths.Count;
        resolved = total == 0 ? 0 : paths.Count(p => root.transform.Find(p) != null);
        return total == 0 ? 0f : (float)resolved / total;
    }

    // Testa cada doador conhecido contra a hierarquia do personagem alvo e fica com o de maior
    // compatibilidade — só devolve non-null se passar do piso; senão quem chama decide como
    // sinalizar (hoje: deixa o estado "Slashing" sem Motion e loga o motivo, em vez de aplicar
    // uma animação que arranca um membro do lugar).
    private static Motion PickBestSlashDonor(GameObject targetRoot, out string report)
    {
        Motion best = null;
        float bestFrac = -1f;
        string bestLabel = null;
        var lines = new List<string>();

        foreach (var path in SlashDonorControllerPaths)
        {
            var motion = FindSlashMotion(path) as AnimationClip;
            if (motion == null) continue;
            float frac = ClipCompatibilityFraction(targetRoot, motion, out int resolved, out int total);
            lines.Add($"{Path.GetFileNameWithoutExtension(path)}: {resolved}/{total} ({frac * 100f:F0}%)");
            if (frac > bestFrac) { bestFrac = frac; best = motion; bestLabel = Path.GetFileNameWithoutExtension(path); }
        }

        report = string.Join(" | ", lines);
        if (bestFrac < SlashDonorMinCompatibility)
        {
            report += $" — nenhum doador passou de {SlashDonorMinCompatibility * 100f:F0}%, 'Slashing' ficou sem Motion (precisa de animação própria)";
            return null;
        }
        report += $" — usando '{bestLabel}'";
        return best;
    }

    private struct Cond
    {
        public string Param;
        public AnimatorConditionMode Mode;
        public Cond(string param, AnimatorConditionMode mode) { Param = param; Mode = mode; }
    }

    private struct TransitionSpec
    {
        public string From;   // null = AnyState
        public string To;
        public float Duration;
        public float ExitTime;
        public bool HasExitTime;
        public bool CanTransitionToSelf;
        public Cond[] Conditions;
    }

    // Extraído campo a campo de Assassin Guy.controller (fonte da verdade — não um resumo).
    private static readonly TransitionSpec[] Transitions =
    {
        new TransitionSpec { From = null, To = "Dying", Duration = .1f, ExitTime = 0f, HasExitTime = false, CanTransitionToSelf = false,
            Conditions = new[] { new Cond("Dying", AnimatorConditionMode.If) } },
        new TransitionSpec { From = null, To = "Slashing", Duration = .1f, ExitTime = 0f, HasExitTime = false, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("Running", AnimatorConditionMode.IfNot), new Cond("Slashing", AnimatorConditionMode.If) } },
        new TransitionSpec { From = null, To = "Slashing Dagger", Duration = .1f, ExitTime = 0f, HasExitTime = false, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("Running", AnimatorConditionMode.IfNot), new Cond("SlashingDagger", AnimatorConditionMode.If) } },
        new TransitionSpec { From = null, To = "Throwing", Duration = .1f, ExitTime = 0f, HasExitTime = false, CanTransitionToSelf = false,
            Conditions = new[] { new Cond("Throwing", AnimatorConditionMode.If) } },
        new TransitionSpec { From = null, To = "Block", Duration = .1f, ExitTime = 0f, HasExitTime = false, CanTransitionToSelf = false,
            Conditions = new[] { new Cond("Blocking", AnimatorConditionMode.If) } },

        new TransitionSpec { From = "Idle", To = "Running", Duration = .25f, ExitTime = .5833334f, HasExitTime = false, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("Idle", AnimatorConditionMode.IfNot), new Cond("Running", AnimatorConditionMode.If) } },
        new TransitionSpec { From = "Idle", To = "Hurt", Duration = .25f, ExitTime = .4f, HasExitTime = false, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("Idle", AnimatorConditionMode.If), new Cond("Hurt", AnimatorConditionMode.If) } },
        new TransitionSpec { From = "Idle", To = "Catch Weapon", Duration = .25f, ExitTime = .5833334f, HasExitTime = false, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("Idle", AnimatorConditionMode.IfNot), new Cond("CatchWeapon", AnimatorConditionMode.If) } },

        new TransitionSpec { From = "Running", To = "Slashing", Duration = .25f, ExitTime = .375f, HasExitTime = false, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("Running", AnimatorConditionMode.IfNot), new Cond("Slashing", AnimatorConditionMode.If) } },
        new TransitionSpec { From = "Running", To = "Slashing Dagger", Duration = .25f, ExitTime = .375f, HasExitTime = false, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("Running", AnimatorConditionMode.IfNot), new Cond("SlashingDagger", AnimatorConditionMode.If) } },

        new TransitionSpec { From = "Hurt", To = "Idle", Duration = .25f, ExitTime = .375f, HasExitTime = true, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("Idle", AnimatorConditionMode.If) } },

        new TransitionSpec { From = "Slashing", To = "Jump Start", Duration = .25f, ExitTime = .375f, HasExitTime = false, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("JumpStart", AnimatorConditionMode.If) } },
        new TransitionSpec { From = "Slashing Dagger", To = "Jump Start", Duration = .25f, ExitTime = .375f, HasExitTime = false, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("JumpStart", AnimatorConditionMode.If) } },

        new TransitionSpec { From = "Jump Start", To = "Idle", Duration = .25f, ExitTime = 0f, HasExitTime = false, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("JumpStart", AnimatorConditionMode.IfNot), new Cond("Idle", AnimatorConditionMode.If) } },

        new TransitionSpec { From = "Throwing", To = "Idle", Duration = .25f, ExitTime = .75f, HasExitTime = true, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("Idle", AnimatorConditionMode.If) } },
        new TransitionSpec { From = "Block", To = "Idle", Duration = .25f, ExitTime = .75f, HasExitTime = true, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("Idle", AnimatorConditionMode.If) } },
        new TransitionSpec { From = "Catch Weapon", To = "Idle", Duration = .25f, ExitTime = .6666667f, HasExitTime = false, CanTransitionToSelf = true,
            Conditions = new[] { new Cond("Idle", AnimatorConditionMode.If) } },
    };

    // Nem todo pacote CraftPix nomeia o estado de ataque "Slashing". Dois casos diferentes:
    // - Rename: o apelido JÁ É o golpe genérico de cima pra baixo, só com outro nome (ex.:
    //   Medieval Hooded Girl chama de "Attacking") — renomear em paz, o clipe já serve.
    // - Clone: o apelido tem um propósito PRÓPRIO que não deve ser perdido (ex.: Archer é
    //   arqueira, "Shooting" fica intacto pra um futuro sistema de ataque à distância, e o
    //   movimento de puxar/soltar flecha não serve como slash de qualquer forma — ver
    //   PickBestSlashDonor/DuplicateClip) — cria um estado "Slashing" novo, sem tocar no original.
    // Adicionar aqui conforme aparecerem outros pacotes com nomenclatura diferente.
    private enum AliasMode { Rename, Clone }

    private struct AliasSpec
    {
        public string Name;
        public AliasMode Mode;
        public AliasSpec(string name, AliasMode mode) { Name = name; Mode = mode; }
    }

    private static readonly Dictionary<string, AliasSpec[]> StateAliases = new Dictionary<string, AliasSpec[]>
    {
        { "Slashing", new[]
            {
                new AliasSpec("Attacking", AliasMode.Rename),
                new AliasSpec("Shooting", AliasMode.Clone),
            }
        },
    };

    // Velocidades por-estado customizadas na Assassin Guy (o export cru vem tudo em 1).
    private static readonly Dictionary<string, float> StateSpeeds = new Dictionary<string, float>
    {
        { "Slashing", 0.5f },
        { "Hurt", 0.4f },
        { "Throwing", 0.5f },
        { "Jump Start", 0.3f },
    };

    // Duplica um clipe existente (Instantiate + CreateAsset) pra um novo .anim standalone —
    // idempotente, se já existir em destPath só reaproveita. Usado quando o pedido é uma CÓPIA
    // independente (não uma referência compartilhada) — o usuário pode querer editar a versão de
    // Slashing sem afetar a de Throwing original.
    private static AnimationClip DuplicateClip(AnimationClip source, string destPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(destPath);
        if (existing != null) return existing;
        var copy = Object.Instantiate(source);
        copy.name = Path.GetFileNameWithoutExtension(destPath);
        AssetDatabase.CreateAsset(copy, destPath);
        return copy;
    }

    // Troca o `path` de todas as curvas (posição/rotação/escala e sprite) que apontam exatamente
    // pra `oldPath`, preservando os keyframes — usado quando dois personagens têm a mesma
    // convenção de nomes de bone só que numerados/organizados diferente (ex.: bone_004/bone_005
    // trocados entre Assassin Guy e Amazon Warrior nas pernas).
    private static void RemapCurvePath(AnimationClip clip, string oldPath, string newPath)
    {
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path != oldPath) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            AnimationUtility.SetEditorCurve(clip, binding, null);
            var newBinding = binding;
            newBinding.path = newPath;
            AnimationUtility.SetEditorCurve(clip, newBinding, curve);
        }
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.path != oldPath) continue;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            var newBinding = binding;
            newBinding.path = newPath;
            AnimationUtility.SetObjectReferenceCurve(clip, newBinding, keys);
        }
    }

    // Soma `delta` em todo keyframe de posição do path indicado (as 3 curvas escalares x/y/z que
    // o AnimationUtility expõe pra m_LocalPosition). No-op se delta for zero.
    private static void ApplyPositionDelta(AnimationClip clip, string path, Vector3 delta)
    {
        if (delta == Vector3.zero) return;
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.path != path) continue;
            if (b.propertyName != "m_LocalPosition.x" && b.propertyName != "m_LocalPosition.y" && b.propertyName != "m_LocalPosition.z") continue;
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null) continue;
            float d = b.propertyName.EndsWith(".x") ? delta.x : b.propertyName.EndsWith(".y") ? delta.y : delta.z;
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++) keys[i].value += d;
            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, b, curve);
        }
    }

    // Retargeting por deslocamento constante: pra cada path de posição animado no clipe, soma a
    // diferença entre a pose de repouso (bind pose, lida direto do .prefab) do personagem ALVO e
    // da FONTE — reancora a curva inteira no corpo do personagem alvo, preservando a forma/
    // amplitude relativa do movimento original (ex.: "voltar quase ao repouso no fim do golpe"
    // continua voltando quase ao repouso, só que ao repouso de quem está usando o clipe agora).
    // Não corrige rotação nem diferenças de ESCALA entre os corpos — cobre o caso confirmado
    // (Head "descolando" na Amazon Warrior = ela reproduzindo a pose de repouso da Assassin Guy
    // num keyframe de "quase-repouso", que fica longe da pose de repouso DELA). Paths que não
    // existem 1:1 nos dois rigs (ex. pernas com bone_004/bone_005 trocados) são pulados aqui —
    // tratar à parte com ApplyPositionDelta manual + RemapCurvePath, como já feito nas pernas.
    private static void RetargetPositionCurves(AnimationClip clip, GameObject sourceRoot, GameObject targetRoot)
    {
        var paths = AnimationUtility.GetCurveBindings(clip)
            .Where(b => b.propertyName == "m_LocalPosition.x" || b.propertyName == "m_LocalPosition.y" || b.propertyName == "m_LocalPosition.z")
            .Select(b => b.path).Distinct().ToList();

        foreach (var path in paths)
        {
            var sourceT = sourceRoot.transform.Find(path);
            var targetT = targetRoot.transform.Find(path);
            if (sourceT == null || targetT == null) continue;
            ApplyPositionDelta(clip, path, targetT.localPosition - sourceT.localPosition);
        }
    }

    private static string FindImmediateParentName(GameObject root, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName && t.parent != null) return t.parent.name;
        return null;
    }

    // Pedido do usuário (2026-07-10): o "Slashing Dagger" compartilhado (clipe da Assassin Guy,
    // guid SlashingDaggerClipGuid) mostrava "Missing!" em Left Leg/Right Leg no Animation Window
    // da Amazon Warrior (bone_004/bone_005 trocados entre os dois rigs) e a cabeça "descolava"
    // durante o swing — causa raiz diferente: o keyframe "errado" era literalmente a pose de
    // repouso da PRÓPRIA Assassin Guy (ela voltando quase ao neutro no fim do golpe), que não tem
    // nada a ver com a pose de repouso de quem está usando o clipe. Generalizado (2026-07-10) pra
    // qualquer personagem: acha automaticamente quem é o pai de "Left Leg"/"Right Leg" no rig alvo
    // (em vez de assumir bone_004/bone_005 fixo) e aplica o mesmo retargeting genérico no resto.
    // Cria/recria uma cópia dedicada em <pasta do personagem>/Prefab/Slashing Dagger.anim — nunca
    // mexe no clipe original da Assassin Guy.
    private static string RetargetSlashingDaggerFor(string prefabPath, string controllerPath, AnimatorState state,
        AnimationClip sourceClip, GameObject sourceRoot, string sourceLeftLegParent, string sourceRightLegParent)
    {
        string prefabDestFolder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
        string destPath = $"{prefabDestFolder}/Slashing Dagger.anim";

        // Sempre recria do zero a partir do clipe compartilhado — evita reaplicar retargeting em
        // cima de uma cópia já parcialmente corrigida numa rodada anterior.
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(destPath) != null)
            AssetDatabase.DeleteAsset(destPath);
        var copy = DuplicateClip(sourceClip, destPath);

        var targetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        // Pernas: só remapeia path se o pai de fato for diferente nos dois rigs — resolvido antes
        // do retargeting genérico, que precisa do MESMO path nos dois lados pra funcionar.
        string targetLeftLegParent = FindImmediateParentName(targetRoot, "Left Leg");
        string targetRightLegParent = FindImmediateParentName(targetRoot, "Right Leg");

        RetargetLeg(copy, sourceRoot, targetRoot, sourceLeftLegParent, targetLeftLegParent, "Left Leg");
        RetargetLeg(copy, sourceRoot, targetRoot, sourceRightLegParent, targetRightLegParent, "Right Leg");

        // Todo o resto (Head, Face 01, Body, braços, mãos, Sword, SlashFX...) — path idêntico nos
        // dois rigs, retargeting direto por delta de pose de repouso.
        RetargetPositionCurves(copy, sourceRoot, targetRoot);
        EditorUtility.SetDirty(copy);

        state.motion = copy;
        EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath));

        float frac = ClipCompatibilityFraction(targetRoot, copy, out int resolved, out int total);
        string flag = frac < SlashDonorMinCompatibility ? " ⚠ ainda sobra path não resolvido, revisar manualmente" : "";
        return $"{resolved}/{total} bones resolvidos ({frac * 100f:F0}%){flag}";
    }

    private static void RetargetLeg(AnimationClip copy, GameObject sourceRoot, GameObject targetRoot, string sourceParent, string targetParent, string legName)
    {
        if (sourceParent == null || targetParent == null) return;
        string sPath = $"{sourceParent}/{legName}";
        string tPath = $"{targetParent}/{legName}";
        var sT = sourceRoot.transform.Find(sPath);
        var tT = targetRoot.transform.Find(tPath);
        if (sT == null || tT == null) return;
        ApplyPositionDelta(copy, sPath, tT.localPosition - sT.localPosition);
        if (sPath != tPath) RemapCurvePath(copy, sPath, tPath);
    }

    [MenuItem("Tools/AutoArms/Retarget Slashing Dagger For All Characters")]
    public static void RetargetSlashingDaggerForAll()
    {
        const string assassinGuyPrefabPath = "Assets/Personagens/Assassin Guy/Prefab/Assassin Guy.prefab";
        var sourcePath = AssetDatabase.GUIDToAssetPath(SlashingDaggerClipGuid);
        var sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath);
        var sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assassinGuyPrefabPath);
        if (sourceClip == null || sourceRoot == null) { Debug.LogError("[Retarget] Clipe/prefab de origem (Assassin Guy) não encontrado."); return; }

        string sourceLeftLegParent = FindImmediateParentName(sourceRoot, "Left Leg");
        string sourceRightLegParent = FindImmediateParentName(sourceRoot, "Right Leg");

        int processed = 0;
        foreach (var folderPath in Directory.GetDirectories(PersonagensRoot))
        {
            string folderName = Path.GetFileName(folderPath);
            if (SkipFolders.Contains(folderName)) continue; // 00-SplashArt, Assassin Guy, Medieval Warrior, Medieval Warrior Girl

            string prefabDestFolder = Norm($"{folderPath}/Prefab");
            if (!Directory.Exists(prefabDestFolder)) continue;
            string prefabPath = Directory.GetFiles(prefabDestFolder, "*.prefab").Select(Norm).FirstOrDefault();
            string controllerPath = Directory.GetFiles(prefabDestFolder, "*.controller").Select(Norm).FirstOrDefault();
            if (prefabPath == null || controllerPath == null) continue; // pasta ainda não processada pelo pipeline principal

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) continue;
            var state = controller.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Slashing Dagger");
            if (state == null) continue; // idem

            try
            {
                string report = RetargetSlashingDaggerFor(prefabPath, controllerPath, state, sourceClip, sourceRoot, sourceLeftLegParent, sourceRightLegParent);
                Debug.Log($"[Retarget] {ToDisplayName(folderName)}: {report}");
                processed++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Retarget] {folderName}: falhou — {e}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Retarget] Concluído — {processed} personagem(ns) processado(s).");
    }

    // Correção pontual: Archer_2 já tinha sido processada pela versão antiga do gerador, que
    // RENOMEAVA o estado alias direto (perdendo o "Shooting" original) em vez de clonar, e depois
    // usava um clipe de outro personagem incompatível com o rig dela. Pedido do usuário: usar uma
    // CÓPIA do "Throwing" dela mesma (mesmo movimento, mesmo rig — sem risco de quebrar bone).
    // Idempotente — dá pra rodar de novo sem duplicar nada. Rodar uma vez; depois pode remover
    // este menu item.
    [MenuItem("Tools/AutoArms/Repair - Split Archer Shooting-Slashing")]
    public static void RepairArcherShootingSplit()
    {
        const string controllerPath = "Assets/Personagens/Archer_2/Prefab/Archer.controller";
        const string slashClipDestPath = "Assets/Personagens/Archer_2/Prefab/Slashing.anim";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) { Debug.LogError($"[Repair] {controllerPath} não encontrado."); return; }

        var sm = controller.layers[0].stateMachine;
        var states = sm.states.Select(s => s.state).ToList();

        // Pedido do usuário: Archer não tem golpe de cima pra baixo, mas o "Throwing" dela já tem
        // o mesmo movimento — em vez de doador externo (rig incompatível, ver histórico acima),
        // duplica o Throwing DELA MESMA (garantidamente compatível, mesmo rig) como Slashing.
        var throwingState = states.FirstOrDefault(s => s.name == "Throwing");
        if (throwingState == null || !(throwingState.motion is AnimationClip throwClip))
        {
            Debug.LogError("[Repair] Estado 'Throwing' ou o Motion dele não encontrado — não dá pra duplicar.");
            return;
        }
        var donorMotion = DuplicateClip(throwClip, slashClipDestPath);
        Debug.Log($"[Repair] Cópia de 'Throwing' criada em {slashClipDestPath} — usada como Motion de 'Slashing'.");

        var shootingState = states.FirstOrDefault(s => s.name == "Shooting");
        var slashingState = states.FirstOrDefault(s => s.name == "Slashing");

        if (shootingState != null && slashingState != null)
        {
            // já separado numa rodada anterior — corrige o Motion, que tinha ficado gravado errado
            // (o golpe de Medieval Warrior arrancando a perna esquerda dela).
            slashingState.motion = donorMotion;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[Repair] 'Shooting'/'Slashing' já estavam separados — Motion de 'Slashing' corrigido pra cópia do Throwing.");
            return;
        }

        var oldState = slashingState; // ainda não separado: "Slashing" atual É o "Shooting" renomeado
        if (oldState == null) { Debug.LogWarning("[Repair] Nada pra corrigir."); return; }

        var newState = sm.AddState("Slashing (novo)");
        newState.motion = donorMotion;
        newState.speed = oldState.speed; // 0.5 (StateSpeeds) — combate herda a velocidade customizada
        newState.transitions = oldState.transitions; // move a fiação de saída (Slashing → Jump Start)
        oldState.transitions = new AnimatorStateTransition[0];

        foreach (var t in sm.anyStateTransitions)
            if (t.destinationState == oldState) t.destinationState = newState;
        foreach (var cs in sm.states)
            foreach (var t in cs.state.transitions)
                if (t.destinationState == oldState) t.destinationState = newState;

        if (sm.defaultState == oldState) sm.defaultState = newState;

        oldState.name = "Shooting";
        oldState.speed = 1f; // volta pro padrão cru do export
        newState.name = "Slashing";

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[Repair] Archer.controller: 'Shooting' restaurado intacto, 'Slashing' novo assumiu a fiação de combate.");
    }

    [MenuItem("Tools/AutoArms/Import Female Character")]
    public static void ImportNext()
    {
        var attackSettings = AssetDatabase.LoadAssetAtPath<AttackSettings>(AttackSettingsPath);
        if (attackSettings == null)
        {
            Debug.LogError($"[FemaleCharacterImportGenerator] AttackSettings não encontrado em {AttackSettingsPath} — abortando.");
            return;
        }

        int processed = 0;
        foreach (var folderPath in Directory.GetDirectories(PersonagensRoot))
        {
            string folderName = Path.GetFileName(folderPath);
            if (SkipFolders.Contains(folderName)) continue;

            string displayName = ToDisplayName(folderName);
            string profileAssetPath = $"{ProfilesFolder}/{displayName}.asset";
            if (AssetDatabase.LoadAssetAtPath<PlayerProfile>(profileAssetPath) != null) continue; // já processado

            string vectorPartsFolder = FindVectorPartsFolder(folderPath);
            if (vectorPartsFolder == null)
            {
                Debug.LogWarning($"[FemaleCharacterImportGenerator] {folderName}: pasta 'Vector Parts' não encontrada — pulando (ainda não importado?).");
                continue;
            }

            try
            {
                ProcessCharacter(folderName, displayName, vectorPartsFolder, profileAssetPath, attackSettings);
                processed++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FemaleCharacterImportGenerator] {folderName}: falhou — {e}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FemaleCharacterImportGenerator] Concluído — {processed} personagem(ns) processado(s) nesta rodada.");
    }

    private static string FindVectorPartsFolder(string folderPath)
    {
        string flat = Norm($"{folderPath}/Vector Parts");
        if (Directory.Exists(flat)) return flat;
        string nested = Norm($"{folderPath}/PNG/Vector Parts");
        if (Directory.Exists(nested)) return nested;
        // Alguns pacotes já chegam organizados como os personagens finalizados (Graphics/ +
        // Prefab/, ex. "Medieval Hooded Girl") — sem uma pasta "Vector Parts" separada, prefab e
        // controller já nascem direto em Prefab/. Se já existe um .prefab ali, usa a própria
        // Prefab/ como fonte (o passo de mover em ProcessCharacter detecta que não precisa mover).
        string prefabFolder = Norm($"{folderPath}/Prefab");
        if (Directory.Exists(prefabFolder) && Directory.GetFiles(prefabFolder, "*.prefab").Any())
            return prefabFolder;
        return null;
    }

    private static string Norm(string path) => path.Replace('\\', '/');

    private static string ToDisplayName(string folderName) =>
        System.Text.RegularExpressions.Regex.Replace(folderName.Replace('_', ' ').Replace('-', ' '), @"\s+", " ").Trim();

    private static void ProcessCharacter(string folderName, string displayName, string vectorPartsFolder, string profileAssetPath, AttackSettings attackSettings)
    {
        string folderPath = Norm($"{PersonagensRoot}/{folderName}");
        string prefabDestFolder = $"{folderPath}/Prefab";
        if (!AssetDatabase.IsValidFolder(prefabDestFolder))
            AssetDatabase.CreateFolder(folderPath, "Prefab");

        // 1) Localizar prefab + controller. Uma rodada anterior pode ter falhado DEPOIS de já
        // mover os dois pra Prefab/ (ex.: erro no Animator Controller) — nesse caso eles não
        // existem mais em vectorPartsFolder, então checar o destino primeiro antes de procurar
        // na origem (senão re-rodar depois de um erro parcial sempre falha com "nenhum .prefab
        // encontrado").
        string baseName;
        string prefabPath;
        string controllerPath;
        var alreadyMoved = Directory.Exists(prefabDestFolder) ? Directory.GetFiles(prefabDestFolder, "*.prefab").Select(Norm).FirstOrDefault() : null;
        if (alreadyMoved != null)
        {
            baseName = Path.GetFileNameWithoutExtension(alreadyMoved);
            prefabPath = alreadyMoved;
            controllerPath = $"{prefabDestFolder}/{baseName}.controller";
        }
        else
        {
            string sourcePrefab = Directory.GetFiles(vectorPartsFolder, "*.prefab").Select(Norm).FirstOrDefault();
            if (sourcePrefab == null) throw new System.Exception("nenhum .prefab encontrado em " + vectorPartsFolder);
            baseName = Path.GetFileNameWithoutExtension(sourcePrefab);
            string sourceController = Norm($"{vectorPartsFolder}/{baseName}.controller");
            if (!File.Exists(sourceController)) throw new System.Exception($"controller '{baseName}.controller' não encontrado ao lado do prefab");

            prefabPath = $"{prefabDestFolder}/{baseName}.prefab";
            controllerPath = $"{prefabDestFolder}/{baseName}.controller";
            string err1 = AssetDatabase.MoveAsset(sourcePrefab, prefabPath);
            if (!string.IsNullOrEmpty(err1)) throw new System.Exception("MoveAsset prefab: " + err1);
            string err2 = AssetDatabase.MoveAsset(sourceController, controllerPath);
            if (!string.IsNullOrEmpty(err2)) throw new System.Exception("MoveAsset controller: " + err2);
        }

        // 2) Frame de Idle solto (nome livre) → Prefab/Idle_000.png, fonte do previewIcon.
        Sprite previewIcon = MoveIdleFrame(folderPath, vectorPartsFolder, prefabDestFolder);

        // 3) Animator Controller: parâmetros + 3 estados novos + transições completas. Precisa do
        // prefab (leitura, sem editar ainda) já aqui pra medir compatibilidade real de doadores
        // de Slashing quando o personagem não tem golpe nativo (ver PickBestSlashDonor).
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var prefabForCheck = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        BuildAnimatorController(controller, prefabForCheck);

        // 4) Prefab: componentes de combate + wiring de bones + checagem de compatibilidade
        // dos 3 clipes reaproveitados (Block/Catch Weapon/Slashing Dagger).
        string report = WirePrefab(prefabPath, attackSettings);

        // 5) PlayerProfile.
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var stats = CharacterCreation.GenerateLevel1Stats();
        var profile = ScriptableObject.CreateInstance<PlayerProfile>();
        profile.profileName = displayName;
        profile.characterPrefab = prefabAsset;
        profile.previewIcon = previewIcon;
        profile.attackSettings = attackSettings;
        profile.scale = Vector3.one;
        profile.startPos = new Vector2(-6.3f, -2.407897f);
        profile.maxHealth = stats.maxHealth;
        profile.str = stats.str;
        profile.agility = stats.agility;
        profile.speed = stats.speed;
        profile.level = 1;
        profile.xpRequired = XpSystem.XpRequired(1);
        profile.battlesRemaining = 6;
        profile.isUnlockedForSelection = false;
        profile.isPlayable = false;
        AssetDatabase.CreateAsset(profile, profileAssetPath);

        // 6) CharacterDatabase.unlockedCharacters.
        var db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharacterDatabasePath);
        if (db != null && !db.unlockedCharacters.Contains(profile))
        {
            db.unlockedCharacters.Add(profile);
            EditorUtility.SetDirty(db);
        }

        Debug.Log($"[FemaleCharacterImportGenerator] {displayName}: OK. {report}");
    }

    private static Sprite MoveIdleFrame(string folderPath, string vectorPartsFolder, string prefabDestFolder)
    {
        // Quando o pacote já chega organizado (Graphics/ + Prefab/, ver FindVectorPartsFolder),
        // vectorPartsFolder resolve pra própria Prefab/ — os PNGs soltos moram na pasta irmã
        // "Graphics", não junto do prefab/controller. Procura nas duas.
        var searchFolders = new List<string> { vectorPartsFolder };
        string graphics = Norm($"{folderPath}/Graphics");
        if (Directory.Exists(graphics) && !searchFolders.Contains(graphics)) searchFolders.Add(graphics);

        string src = searchFolders
            .SelectMany(dir => Directory.GetFiles(dir, "*.png"))
            .Select(Norm)
            .Where(p => Path.GetFileName(p).ToLowerInvariant().Contains("idle"))
            .OrderBy(p => p)
            .FirstOrDefault();
        if (src == null)
        {
            Debug.LogWarning($"[FemaleCharacterImportGenerator] Nenhum frame de Idle encontrado em {string.Join(" ou ", searchFolders)} — previewIcon ficará vazio, preencher depois.");
            return null;
        }

        string destPath = $"{prefabDestFolder}/Idle_000.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(destPath) == null)
        {
            string err = AssetDatabase.MoveAsset(src, destPath);
            if (!string.IsNullOrEmpty(err))
            {
                Debug.LogWarning($"[FemaleCharacterImportGenerator] Falha movendo frame de Idle: {err}");
                return null;
            }
        }

        var importer = AssetImporter.GetAtPath(destPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
    }

    private static void BuildAnimatorController(AnimatorController controller, GameObject prefabRoot)
    {
        var sm = controller.layers[0].stateMachine;

        foreach (var paramName in new[] { "Idle", "Running", "JumpStart" })
            EnsureParameter(controller, paramName, AnimatorControllerParameterType.Bool);
        foreach (var paramName in new[] { "Hurt", "Slashing", "SlashingDagger", "Blocking", "Throwing", "Dying", "CatchWeapon" })
            EnsureParameter(controller, paramName, AnimatorControllerParameterType.Trigger);

        string[] requiredStock = { "Idle", "Running", "Hurt", "Slashing", "Throwing", "Dying", "Jump Start" };
        var stateByName = new Dictionary<string, AnimatorState>();
        foreach (var s in sm.states) stateByName[s.state.name] = s.state;

        var missing = new List<string>();
        foreach (var canonical in requiredStock)
        {
            if (stateByName.ContainsKey(canonical)) continue;

            AliasSpec aliasUsed = default;
            bool foundAlias = false;
            if (StateAliases.TryGetValue(canonical, out var aliases))
            {
                foreach (var a in aliases)
                {
                    if (!stateByName.ContainsKey(a.Name)) continue;
                    aliasUsed = a;
                    foundAlias = true;
                    break;
                }
            }
            if (!foundAlias) { missing.Add(canonical); continue; }

            var aliasState = stateByName[aliasUsed.Name];
            if (aliasUsed.Mode == AliasMode.Rename)
            {
                // O apelido já É o golpe genérico (ex. "Attacking"), só com outro nome — renomear
                // em paz, sem criar estado novo nem mexer no Motion.
                aliasState.name = canonical;
                stateByName[canonical] = aliasState;
                Debug.Log($"[FemaleCharacterImportGenerator] '{aliasUsed.Name}' renomeado pra '{canonical}' (apelido conhecido, já é o golpe genérico).");
            }
            else
            {
                // Clone: não toca no estado original (ex. "Shooting" fica intacto, sem nenhuma
                // transição, pra um futuro sistema de ataque à distância).
                var clone = sm.AddState(canonical);
                if (canonical == "Slashing")
                {
                    var donor = PickBestSlashDonor(prefabRoot, out string donorReport);
                    clone.motion = donor; // pode ficar null — sinalizado no log abaixo, sem aplicar animação quebrada
                    Debug.Log($"[FemaleCharacterImportGenerator] '{aliasUsed.Name}' intacto — '{canonical}' criado. Doadores: {donorReport}");
                }
                else
                {
                    clone.motion = aliasState.motion;
                    Debug.Log($"[FemaleCharacterImportGenerator] '{aliasUsed.Name}' não tocado — criado estado novo '{canonical}' com o mesmo Motion (apelido conhecido).");
                }
                stateByName[canonical] = clone;
            }
        }
        if (missing.Count > 0)
            throw new System.Exception("estados básicos ausentes no export cru (sem apelido conhecido): " + string.Join(", ", missing));

        // 3 estados novos, motion apontando direto pros clipes compartilhados (sem duplicar).
        stateByName["Block"] = AddCustomState(sm, "Block", BlockClipGuid);
        stateByName["Catch Weapon"] = AddCustomState(sm, "Catch Weapon", CatchWeaponClipGuid);
        stateByName["Slashing Dagger"] = AddCustomState(sm, "Slashing Dagger", SlashingDaggerClipGuid);

        foreach (var kv in StateSpeeds)
            if (stateByName.TryGetValue(kv.Key, out var st)) st.speed = kv.Value;

        foreach (var spec in Transitions)
        {
            AnimatorStateTransition t = spec.From == null
                ? sm.AddAnyStateTransition(stateByName[spec.To])
                : stateByName[spec.From].AddTransition(stateByName[spec.To]);

            t.duration = spec.Duration;
            t.exitTime = spec.ExitTime;
            t.hasExitTime = spec.HasExitTime;
            t.hasFixedDuration = true;
            t.canTransitionToSelf = spec.CanTransitionToSelf;
            t.interruptionSource = TransitionInterruptionSource.None;
            foreach (var c in spec.Conditions) t.AddCondition(c.Mode, 0f, c.Param);
        }

        sm.defaultState = stateByName["Idle"];
        EditorUtility.SetDirty(controller);
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        if (controller.parameters.Any(p => p.name == name)) return;
        controller.AddParameter(name, type);
    }

    private static AnimatorState AddCustomState(AnimatorStateMachine sm, string name, string clipGuid)
    {
        var clipPath = AssetDatabase.GUIDToAssetPath(clipGuid);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) throw new System.Exception($"clipe compartilhado não encontrado (guid {clipGuid}, path '{clipPath}')");
        var state = sm.AddState(name);
        state.motion = clip;
        return state;
    }

    // Verifica, pra cada um dos 3 clipes reaproveitados, que fração dos caminhos de curva (bones)
    // resolve de fato dentro da hierarquia do personagem atual — sinaliza incompatibilidade em vez
    // de aplicar uma animação quebrada em silêncio (bone com nome diferente = essa parte do corpo
    // simplesmente não se move durante o clipe, sem erro nenhum no Console).
    private static string CheckClipCompatibility(GameObject root, string clipGuid, string clipLabel)
    {
        var clipPath = AssetDatabase.GUIDToAssetPath(clipGuid);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) return $"{clipLabel}: clipe não encontrado";

        float frac = ClipCompatibilityFraction(root, clip, out int resolved, out int total);
        if (total == 0) return $"{clipLabel}: sem curvas (nada a checar)";

        string flag = frac < SlashDonorMinCompatibility ? " ⚠ INCOMPATÍVEL, revisar manualmente" : "";
        return $"{clipLabel}: {resolved}/{total} bones resolvidos ({frac * 100f:F0}%){flag}";
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static string WirePrefab(string prefabPath, AttackSettings attackSettings)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            string compat = string.Join(" | ",
                CheckClipCompatibility(root, BlockClipGuid, "Block"),
                CheckClipCompatibility(root, CatchWeaponClipGuid, "Catch Weapon"),
                CheckClipCompatibility(root, SlashingDaggerClipGuid, "Slashing Dagger"));

            var leftArm = FindChild(root.transform, "Left Arm");
            var rightArm = FindChild(root.transform, "Right Arm");

            var playerCombat = root.GetComponent<PlayerCombat>() ?? root.AddComponent<PlayerCombat>();
            var weaponHandler = root.GetComponent<WeaponHandler>() ?? root.AddComponent<WeaponHandler>();
            var movement = root.GetComponent<MovementController>() ?? root.AddComponent<MovementController>();
            var animController = root.GetComponent<AnimationController>() ?? root.AddComponent<AnimationController>();
            var loadout = root.GetComponent<PlayerLoadout>() ?? root.AddComponent<PlayerLoadout>();

            if (leftArm != null)
            {
                weaponHandler.handBone = leftArm;
                weaponHandler.swordBasePrefab = leftArm.gameObject;
            }
            else
            {
                Debug.LogWarning($"[FemaleCharacterImportGenerator] {prefabPath}: bone 'Left Arm' não encontrado — handBone/swordBasePrefab ficaram vazios, wireie manualmente.");
            }
            if (rightArm != null) weaponHandler.offHandBone = rightArm;
            else Debug.LogWarning($"[FemaleCharacterImportGenerator] {prefabPath}: bone 'Right Arm' não encontrado — offHandBone ficou vazio.");

            weaponHandler.positionOffset = new Vector3(1, -1, 0);
            weaponHandler.rotationOffset = Vector3.zero;
            weaponHandler.zOffset = 0;
            weaponHandler.sortingLayer = "Weapons";
            weaponHandler.sortingOrder = 0;
            weaponHandler.shieldPositionOffset = new Vector3(1, -1, 0);
            weaponHandler.shieldRotationOffset = Vector3.zero;
            weaponHandler.shieldZOffset = 0;
            weaponHandler.loadout = loadout;

            playerCombat.weaponHandler = weaponHandler;
            playerCombat.movement = movement;
            playerCombat.animationController = animController;
            playerCombat.settings = attackSettings;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return compat;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
