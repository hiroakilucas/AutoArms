using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Replays a pre-calculated event list from CombatSimulator as animation.
// Bridges the pure-logic simulator with the existing MonoBehaviour components.
public class CombatPlayer : MonoBehaviour
{
    [HideInInspector] public PlayerCombat    p1Combat;
    [HideInInspector] public PlayerCombat    p2Combat;
    [HideInInspector] public AttackSequencer sequencer;
    [HideInInspector] public WeaponHUD       p1WeaponHUD;
    [HideInInspector] public WeaponHUD       p2WeaponHUD;
    [HideInInspector] public SkillsHUD       p1SkillsHUD;
    [HideInInspector] public SkillsHUD       p2SkillsHUD;

    // Pets (Fase 3) — instanciados e populados por CombatSceneLoader antes de PlayCombat ser
    // chamado (mesmo padrão de p1Combat/p2Combat). Índice na lista = petIndex dos eventos.
    [HideInInspector] public List<PetCombatController> p1Pets;
    [HideInInspector] public List<PetCombatController> p2Pets;

    // CombatPlayer é adicionado via gameObject.AddComponent<CombatPlayer>() em runtime
    // (CombatSceneLoader.Initialize) — não existe como componente colocado na cena em modo
    // Editor, então não há onde arrastar esse prefab direto no Inspector dele. Wireado em
    // CombatSceneLoader.piledriverEffectPrefab (esse sim um componente real da cena) e
    // copiado pra aqui no momento da criação, mesmo padrão de p1WeaponHUD/p2WeaponHUD acima.
    [HideInInspector] public GameObject piledriverEffectPrefab;

    // Skill Net — mesmo motivo/padrão do piledriverEffectPrefab acima: wireados em
    // CombatSceneLoader (netFlyingSprite/netLandedSprite) e copiados pra aqui na criação.
    [HideInInspector] public Sprite netFlyingSprite;
    [HideInInspector] public Sprite netLandedSprite;

    // Skill Bomb — mesmo motivo/padrão de piledriverEffectPrefab/netFlyingSprite acima:
    // wireado em CombatSceneLoader.bombPrefab e copiado pra aqui na criação. Um único prefab
    // combinando SpriteRenderer (sprite "bomb", usado durante o voo) + Animator (controller da
    // explosão, BombExplosion.anim/Explosion_1.controller, já criados pelo usuário) — ver
    // case BombThrow abaixo pra como o Animator é mantido desligado durante o voo (senão a
    // explosão, único estado do controller, tocaria imediatamente ao instanciar) e religado só
    // na 2ª instância, no momento do impacto.
    [HideInInspector] public GameObject bombPrefab;

    // Skill Tragic Potion — mesmo motivo/padrão de netFlyingSprite/netLandedSprite acima:
    // wireados em CombatSceneLoader (tragicPotionSprite/tragicPotionHealSprite) e copiados pra
    // aqui na criação. potion = frasco (fase de pegar/beber); healSprite = partículas de cura
    // subindo (fase de efeito).
    [HideInInspector] public Sprite tragicPotionSprite;
    [HideInInspector] public Sprite tragicPotionHealSprite;

    // Skill Fast Metabolism — mesmo motivo/padrão dos campos acima: wireado em
    // CombatSceneLoader.fastMetabolismController e copiado pra aqui na criação. Um único
    // RuntimeAnimatorController (não precisa de prefab — CombatPlayer monta o GameObject com
    // SpriteRenderer+Animator em runtime), reusado tanto pela regeneração passiva (1 instância
    // pequena) quanto pelas folhas orbitando do pulso (6 instâncias).
    [HideInInspector] public RuntimeAnimatorController fastMetabolismController;

    // Skill Vampirism — mesmo motivo/padrão de fastMetabolismController acima: wireado em
    // CombatSceneLoader.vampirismEffectController e copiado pra aqui na criação. Um único
    // RuntimeAnimatorController (1.controller, flipbook de 6 frames já em loop) — sem
    // prefab, PlayerCombat.VampirismRoutine monta o GameObject (SpriteRenderer+Animator) em
    // runtime, mesmo padrão das folhas do Fast Metabolism.
    [HideInInspector] public RuntimeAnimatorController vampirismEffectController;

    // Skill Treat — sprite da coxa de frango (Assets/Data/UI/SkillEffect/Treat/treat.png);
    // wireado em CombatSceneLoader e copiado pra aqui na criação. Arremessado em arco até o pet.
    [HideInInspector] public Sprite treatSprite;

    // Skill Chef — mesmo motivo/padrão de bombPrefab acima: wireado em
    // CombatSceneLoader.chefPizzaPrefab e copiado pra aqui na criação. Um único prefab
    // combinando SpriteRenderer (sprite "chef", usado durante o voo) + Animator (controller da
    // explosão verde, ChefExplosion.anim/Explosion_1.controller, gerados por Tools → AutoArms →
    // Generate Chef Effect Prefab) — mesmo Animator-desligado-durante-o-voo do Bomb.
    [HideInInspector] public GameObject chefPizzaPrefab;

    // Chef: escala única, pedida pelo usuário pra ficar pequena/discreta ("simular que está
    // envenenado") — usada tanto na pizza durante o voo quanto na explosão verde do tick do
    // veneno (PoisonDamage). Nada a ver com a explosão da Bomb (2.5, "domina a tela") — são
    // skills/efeitos diferentes, não relacionados.
    private const float ChefPizzaScale = 0.3f;

    // Fast Metabolism — folhas orbitando do pulso de 50% HP, uma entrada por jogador (index 0/1),
    // cada uma um array das 6 folhas (sem GameObject pai — RotateAround já opera em posição de
    // mundo, não precisa de hierarquia). Null = sem pulso ativo agora. Ver
    // SpawnFastMetabolismLeaves/FadeFastMetabolismLeaves.
    private readonly GameObject[][] _fastMetabolismLeaves = new GameObject[2][];
    private readonly Coroutine[]  _fastMetabolismOrbitRoutine = new Coroutine[2];

    // net1/net2.png vêm em resolução cheia (sem nenhum ajuste de escala por baixo, diferente de
    // armas que escalam pelo lossyScale do handBone). Escalas independentes — net1 (voo) e net2
    // (caída sobre o enredado) calibradas separadamente a pedido do usuário.
    private const float Net1FlyingScale = 0.75f;
    private const float Net2LandedScale = 2f;

    // Bomb: escala da explosão no impacto (deve "dominar a tela", pedido pelo usuário) e
    // duração do voo em pêndulo até o defensor.
    private const float BombExplosionScale = 2.5f;
    private const float BombFlightDuration = 0.5f;

    // Repulse: arma lançada que será deflectida — capturada no case ThrowWeapon e lida no case Repulse.
    private WeaponData _lastThrownWeaponData;

    private List<CombatEvent> _events;
    private float             _playbackSpeed = 1f;
    private bool              _is2x = false;
    private bool              _skipRequested;
    private HealthSystem      _h1, _h2;

    // Bug 1 (weapon drop): índice do evento em execução em PlayEvents (pra case Hit poder
    // espiar eventos futuros na lista) e o conjunto de Disarm já disparados antecipadamente no
    // instante do impacto — evita que o case Disarm, ao ser alcançado de verdade mais tarde,
    // dispare a queda da arma de novo.
    private int _currentEventIndex;
    private readonly HashSet<CombatEvent> _consumedDisarms = new HashSet<CombatEvent>();

    // case PetAttack montava `System.Action onImpact = () => {...}` capturando 5 variáveis
    // locais a cada execução — closure (objeto extra no heap pra guardar as variáveis
    // capturadas) + delegate, alocados de novo em TODO ataque de pet da luta inteira (suspeita
    // do usuário confirmada: o stutter recorrente começou junto da implementação dos pets).
    // Fix: delegate cacheado 1x (method group de uma instância NUNCA recriada) + estado do
    // impacto guardado em campos de instância em vez de variáveis capturadas — zero alocação
    // por ataque de pet. Seguro porque eventos são processados estritamente em sequência (1
    // coroutine, nunca 2 PetAttack concorrentes no mesmo CombatPlayer).
    // Atribuído 1x em Awake() (não dá pra inicializar direto no campo — CS0236, field
    // initializer não pode referenciar membro de instância via `this` implícito).
    private System.Action       _onPetImpact;
    private CombatEvent         _petImpactEvt;
    private PetCombatController _petImpactPet;
    private PetCombatController _petImpactTargetPet;
    private PlayerCombat        _petImpactTargetCharacter;
    private Vector3             _petImpactTargetPos;

    // Distância de "alcance" pra qualquer interação pet↔personagem ou pet↔pet — sem isso, quem
    // corre até o alvo (pet correndo pra atacar, ou personagem correndo pra atacar um pet via
    // Ajuste 1) ia até a posição EXATA do alvo, sobrepondo/atravessando o sprite (bug real
    // reportado pelo usuário: "passando do hitbox"). Um valor único fixo (0.55f, 1ª tentativa)
    // ainda deixava os dois indo "até a cabeça um do outro" — pets têm tamanhos bem diferentes
    // entre si (PetState.Scale 0.30–0.60) e o reach precisa escalar com o "meio-corpo" de quem
    // está envolvido, não um número fixo igual pra todos. `CharacterHalfBody` aproxima a metade
    // do alcance desarmado entre 2 personagens (0.8f em CalcAttackPosition, ou seja ~0.4 cada
    // lado); `PetHalfBody` escala linear com `PetState.Scale` do tipo do pet.
    private const float CharacterHalfBody = 0.35f;
    private static float PetHalfBody(PetType type) => PetState.Scale(type) * 1.3f;

    // Mesma fórmula de CalcAttackPosition (reach na direção do alvo), mas sem depender de arma
    // — usado em toda corrida pet↔personagem/pet↔pet (CombatPlayer corre o personagem até o pet
    // nos casos Hit/Dodge/RunToDefender/PlayPetTargetedSuper; PetCombatController.
    // PlayAttackSequence corre o pet até o alvo dele, ver case PetAttack abaixo).
    private static Vector2 CalcPetStopPosition(Vector3 fromPos, Vector3 towardPos, float reach)
    {
        Vector2 from = fromPos, toward = towardPos;
        Vector2 dir  = toward - from;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        else dir.Normalize();
        return toward - dir * reach;
    }

    // Reach entre um personagem e um pet (qualquer direção) — meio-corpo fixo do personagem +
    // meio-corpo do pet, pelo tipo dele.
    private static float CharacterPetReach(PetType petType) => CharacterHalfBody + PetHalfBody(petType);

    // Reach entre 2 pets — soma o meio-corpo de cada um (sem o termo de personagem).
    private static float PetPetReach(PetType a, PetType b) => PetHalfBody(a) + PetHalfBody(b);

    // Calibração manual por pet, ajustada pelo usuário direto na cena de combate (corrige
    // pivot/proporção que a fórmula geométrica de CharacterPetReach acima não cobre
    // perfeitamente para cada tipo) — X espelha pelo sentido do ataque (esquerda↔direita, igual
    // a todo flip do projeto), Y é absoluto (altura não espelha).
    private static readonly Dictionary<PetType, Vector2> PetAttacksCharacterOffset = new Dictionary<PetType, Vector2>
    {
        { PetType.Monkey, new Vector2(-0.31f, 0.74f) },
        // Javali ainda não testado pelo usuário — chute inicial escalado pela proporção de
        // tamanho contra o Macaco (PetState.Scale: Boar 0.60 vs Monkey 0.35, fator ~1.714),
        // mesmo ponto de partida usado pra calibrar o Macaco. Ajustar depois de testar em jogo.
        { PetType.Boar, new Vector2(-0.31f * 1.714f, 0.74f * 1.714f) },
        // Rato ainda não testado — chute inicial escalado pela proporção de tamanho contra o
        // Macaco (Mouse 0.30 vs Monkey 0.35, fator ~0.857). Ajustar depois de testar em jogo.
        { PetType.Mouse, new Vector2(-0.31f * 0.857f, 0.74f * 0.857f) },
    };

    // Mesma calibração, mas pro sentido contrário (personagem desarmado correndo até o pet) —
    // valor diferente porque a pose/alcance desarmado não é simétrica à do pet atacando. Sem
    // espelhamento de X (ver ApplyPetOffsetRaw abaixo) — o usuário calibra olhando direto pra
    // cena com P2 sempre do mesmo lado, então "direita"/"esquerda" aqui é literal, não relativo
    // ao sentido do ataque.
    // X negativo: ApplyPetOffset espelha pelo sentido do ataque (dirSign). P2 ataca P1's pet
    // → dirSign=-1 → basePos.x += X*(-1) = +|X| (empurra direita ✓). P1 ataca P2's pet
    // → dirSign=+1 → basePos.x += X*(+1) = -|X| (empurra esquerda ✓).
    private static readonly Dictionary<PetType, Vector2> UnarmedAttacksPetOffset = new Dictionary<PetType, Vector2>
    {
        { PetType.Monkey, new Vector2(-0.5897036f, -0.8f) },
        { PetType.Boar,   new Vector2(-1.3607f,    -0.8f * 1.714f) },
        { PetType.Mouse,  new Vector2(-0.5897036f * 0.857f, -0.8f * 0.857f) },
    };

    private static readonly Dictionary<PetType, Vector2> ArmedAttacksPetOffset = new Dictionary<PetType, Vector2>
    {
        { PetType.Boar,   new Vector2(-1.3607f,    -0.8f * 1.714f) },
        { PetType.Monkey, new Vector2(-0.5897036f, -0.8f) },
        { PetType.Mouse,  new Vector2(-0.5897036f * 0.857f, -0.8f * 0.857f) },
    };

    private static Vector3 ApplyPetOffset(Vector3 basePos, Vector3 fromPos, Vector3 towardPos, Dictionary<PetType, Vector2> table, PetType type)
    {
        if (!table.TryGetValue(type, out var off)) return basePos;
        float dirSign = Mathf.Sign(towardPos.x - fromPos.x);
        if (dirSign == 0f) dirSign = 1f;
        basePos.x += off.x * dirSign;
        basePos.y += off.y;
        return basePos;
    }

    // Mesma ideia, mas SEM espelhar o X pelo sentido do ataque — usado por UnarmedAttacksPetOffset,
    // onde a 1ª tentativa espelhada inverteu a direção (P2 fica à direita do Rato/Macaco na cena
    // de teste, então dirSign saía negativo e jogava o offset pro lado errado; reportado pelo
    // usuário). X positivo aqui sempre desloca pra direita, negativo pra esquerda, literal.
    private static Vector3 ApplyPetOffsetRaw(Vector3 basePos, Dictionary<PetType, Vector2> table, PetType type)
    {
        if (!table.TryGetValue(type, out var off)) return basePos;
        basePos.x += off.x;
        basePos.y += off.y;
        return basePos;
    }

    // Pets agora sorteiam um novo ponto aleatório a cada retorno ao spawn (case PetAttack
    // abaixo), em vez de voltar sempre pro mesmo ponto fixo da queda inicial — mesma faixa de X
    // do spawn original (CombatSceneLoader.SpawnPets) e mesma faixa de Y de
    // PlayerCombat.RandomSpawnPosition, já que pets ocupam a mesma faixa vertical da arena.
    // Tenta algumas vezes até achar um ponto sem ninguém (outro pet ou os 2 personagens) muito
    // próximo, pra não aterrissar em cima de alguém — pedido explícito do usuário ("fique atento
    // as sobreposições"); se não achar em N tentativas, usa o último sorteado mesmo assim (evita
    // travar esperando um ponto perfeito numa arena cheia de pets).
    private const float PetOverlapMinDistance = 1.0f;
    private const int   PetSpawnRollAttempts  = 8;

    // ThrowWeapon nunca carrega targetIsPet/targetPetIndex (só o Hit/Miss seguinte, ver
    // CombatSimulator.SimulateThrow) — esse par é sempre emitido em sequência, sem nenhum
    // evento entre os dois (Emit(ThrowWeapon) é a linha imediatamente anterior ao
    // Emit(Hit)/Emit(Miss) pra essa mesma jogada), então olhar pro próximo item de _events é
    // seguro.
    private PetCombatController FindThrowTargetPet(CombatEvent throwEvt)
    {
        int idx = _events.IndexOf(throwEvt);
        if (idx < 0 || idx + 1 >= _events.Count) return null;
        var next = _events[idx + 1];
        if (!next.targetIsPet) return null;
        if (next.type != CombatEventType.Hit && next.type != CombatEventType.Miss) return null;
        return GetPet(next.targetIndex, next.targetPetIndex);
    }

    private Vector2 RollPetSpawnPosition(PetCombatController pet)
    {
        Vector2 candidate = pet.spawnPosition;
        for (int attempt = 0; attempt < PetSpawnRollAttempts; attempt++)
        {
            float x = pet.isPlayer1 ? Random.Range(-5f, -1f) : Random.Range(1f, 5f);
            float y = Random.Range(-3.90f, -0.81f);
            candidate = new Vector2(x, y);
            if (!PetSpawnOverlapsAnyone(candidate, pet)) break;
        }
        return candidate;
    }

    private bool PetSpawnOverlapsAnyone(Vector2 pos, PetCombatController self)
    {
        if (p1Combat != null && Vector2.Distance(pos, p1Combat.transform.position) < PetOverlapMinDistance) return true;
        if (p2Combat != null && Vector2.Distance(pos, p2Combat.transform.position) < PetOverlapMinDistance) return true;
        return PetListOverlaps(p1Pets, pos, self) || PetListOverlaps(p2Pets, pos, self);
    }

    private static bool PetListOverlaps(List<PetCombatController> pets, Vector2 pos, PetCombatController self)
    {
        if (pets == null) return false;
        for (int i = 0; i < pets.Count; i++)
        {
            var p = pets[i];
            if (p == null || p == self) continue;
            if (Vector2.Distance(pos, p.transform.position) < PetOverlapMinDistance) return true;
        }
        return false;
    }

    private void Awake()
    {
        _onPetImpact = HandlePetImpact;
    }

    // Pool de ghosts da Fierce Brute (Profiler confirmou Rendering dominante com spikes de
    // Scripts — SpawnGhostTrail cria/destrói até ~10 SpriteRenderer por tick a cada 0.06s
    // durante até 1.5s, e Instantiate/Destroy repetido em GameObjects é exatamente esse tipo de
    // custo de Scripts em spike). Cresce sob demanda (sem tamanho fixo pré-alocado) e nunca
    // encolhe — GameObjects ficam `SetActive(false)` entre usos em vez de destruídos.
    private readonly List<(GameObject go, SpriteRenderer sr)> _ghostPool = new List<(GameObject, SpriteRenderer)>();

    private (GameObject go, SpriteRenderer sr) RentGhost()
    {
        foreach (var item in _ghostPool)
        {
            if (!item.go.activeSelf)
            {
                item.go.SetActive(true);
                return item;
            }
        }

        var go = new GameObject("GhostTrail (pooled)");
        var sr = go.AddComponent<SpriteRenderer>();
        var entry = (go, sr);
        _ghostPool.Add(entry);
        return entry;
    }

    // Diagnóstico temporário (investigação de FPS drop introduzido pelos pets, pedido pelo
    // usuário) — P liga/desliga o Profiler em runtime durante a luta, sem precisar abrir o
    // painel manualmente antes de o problema acontecer. Remover depois de identificado o
    // método mais custoso (ou manter, é inócuo: só reage a uma tecla específica em builds com
    // o Profiler já compilado, que normalmente nem chega a fazer parte de builds finais).
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
            UnityEngine.Profiling.Profiler.enabled = !UnityEngine.Profiling.Profiler.enabled;

        UpdatePetDepthSorting();
    }

    // Profundidade visual pet↔personagem por posição Y (bug reportado pelo usuário: o
    // personagem "sobrescrevia" o pet ao passar por trás dele, sem nenhuma relação com quem
    // está mais próximo da câmera) — Y maior = mais "pro fundo" da arena = deve renderizar
    // ATRÁS; Y menor = mais "pra frente" = na FRENTE. Reusa as sorting layers já existentes do
    // projeto (Characters/Characters2, ver CLAUDE.md) em vez de inventar sortingOrder novo —
    // cada pet só compara contra o personagem ADVERSÁRIO (o lado que pode de fato cruzar o
    // caminho dele em combate; o próprio dono nunca ataca o próprio pet).
    private void UpdatePetDepthSorting()
    {
        UpdatePetGroupDepthSorting(p1Pets, p2Combat);
        UpdatePetGroupDepthSorting(p2Pets, p1Combat);
    }

    private static void UpdatePetGroupDepthSorting(List<PetCombatController> pets, PlayerCombat opponent)
    {
        if (pets == null || opponent == null) return;
        for (int i = 0; i < pets.Count; i++)
        {
            var pet = pets[i];
            if (pet == null) continue;
            pet.SetDepthLayer(pet.transform.position.y <= opponent.transform.position.y);
        }
    }

    // Called by CombatSceneLoader after both players have landed.
    public void PlayCombat(List<CombatEvent> events)
    {
        _events = events;
        _h1     = p1Combat.GetComponent<HealthSystem>();
        _h2     = p2Combat.GetComponent<HealthSystem>();

        // Monk: aura laranja persistente durante a luta inteira (referência visual do jogo
        // original — personagem com energia ao redor), independente de qualquer evento — só
        // depende da skill estar equipada, então liga de uma vez aqui, antes do 1º evento, em
        // vez de esperar o 1º TurnStart de cada um. Nunca destruída/escondida (ver
        // PlayerCombat.ShowMonkAura) — só pisca rápido a cada contra-ataque (case Counter abaixo).
        if (p1Combat != null && p1Combat.HasSkill("Monk")) p1Combat.ShowMonkAura();
        if (p2Combat != null && p2Combat.HasSkill("Monk")) p2Combat.ShowMonkAura();

        StartCoroutine(PlayEvents());
    }

    // Toggles between 1x and 1.5x playback. Returns the new state (true = now at 1.5x).
    public bool ToggleSpeed()
    {
        if (!_is2x)
        {
            _playbackSpeed = 1.5f;
            _is2x = true;
        }
        else
        {
            _playbackSpeed = 1f;
            _is2x = false;
        }
        return _is2x;
    }

    public void RequestSkip()
    {
        _skipRequested = true;
    }

    private IEnumerator PlayEvents()
    {
        for (int i = 0; i < _events.Count; i++)
        {
            var evt = _events[i];
            if (_skipRequested)
            {
                // Fast-forward: apply all remaining HealthChanged events, then fire CombatEnd
                foreach (var remaining in _events)
                {
                    if (remaining.type == CombatEventType.HealthChanged)
                        ApplyHealthChanged(remaining);
                }
                var endEvt = _events.FindLast(e => e.type == CombatEventType.CombatEnd);
                if (endEvt != null) TriggerCombatEnd(endEvt);
                yield break;
            }

            // Bug 1 (weapon drop): case Hit abaixo usa este índice pra espiar se um Disarm
            // deste mesmo golpe vem mais à frente na lista, e disparar a queda da arma já no
            // instante do impacto em vez de esperar o Hit inteiro (swing + hurt + comboDelay)
            // terminar primeiro.
            _currentEventIndex = i;
            yield return StartCoroutine(ExecuteEvent(evt));
        }
    }

    private IEnumerator ExecuteEvent(CombatEvent evt)
    {
        var attacker = GetCombat(evt.playerIndex);
        var defender = GetCombat(evt.targetIndex);
        float t = 1f / _playbackSpeed;

        switch (evt.type)
        {
            case CombatEventType.Saboteur:
                // Pré-fight (skill Saboteur, antes do 1º TurnStart) OU durante o combate (skill
                // Sabotage, 50% por golpe acertado) — mesmo evento/visual nos dois casos: popup
                // "SABOTAGE!" acima da vítima, e a arma destruída cai do próprio ícone na
                // WeaponHUD (abaixo da barra de vida) até o chão, igual a um Disarm normal —
                // sem isso, o ícone continuava visível mesmo com a arma já removida do loadout
                // do simulador. Nunca é a arma equipada (ícone dourado) — só ícones cinza,
                // ver CombatSimulator.SimulateHit, então não precisa desequipar a mão aqui.
                if (defender != null)
                {
                    // Força o layout group a recalcular antes de ler a posição do ícone —
                    // RectTransform.position só reflete o resultado final do HorizontalLayoutGroup
                    // depois de um passe de layout; sem isso, ler a posição logo após o frame em
                    // que a WeaponHUD foi montada podia pegar um valor desatualizado/zerado.
                    Canvas.ForceUpdateCanvases();

                    var weaponHud  = GetWeaponHUD(evt.targetIndex);
                    var weaponData = FindWeaponByName(defender.weaponHandler.loadout, evt.weaponName);
                    Vector3? iconScreenPos = weaponHud?.GetIconScreenPosition(evt.weaponName);

                    if (weaponHud != null && weaponData != null && iconScreenPos.HasValue && Camera.main != null)
                    {
                        weaponHud.RemoveWeapon(weaponData);
                        float depth = defender.transform.position.z - Camera.main.transform.position.z;
                        Vector3 startWorldPos = Camera.main.ScreenToWorldPoint(
                            new Vector3(iconScreenPos.Value.x, iconScreenPos.Value.y, depth));
                        StartCoroutine(PlayerCombat.DropWeaponFromHud(defender, weaponData, startWorldPos));
                    }
                    else
                    {
                        Debug.LogError($"[Saboteur] Não foi possível animar a queda da arma destruída ({evt.weaponName}): weaponHud={weaponHud != null}, weaponData={weaponData != null}, iconScreenPos={iconScreenPos.HasValue}, Camera.main={Camera.main != null}");
                    }
                }
                DamagePopup.SpawnSabotage((defender?.transform.position ?? Vector3.zero) + Vector3.up * 1.5f);
                // Sem pausa — a queda da arma já roda em paralelo via StartCoroutine acima; não
                // precisa segurar o resto da luta esperando ela terminar (skill Sabotage, único
                // emissor restante deste evento, é sempre mid-fight — ver CombatEvent.Saboteur).
                yield return null;
                break;

            // Saboteur (skill, distinta de Sabotage acima): a 1ª arma que a vítima conseguir
            // empunhar de verdade nesta luta quebra na hora, 100% garantido — diferente do
            // Saboteur/Sabotage acima, a arma JÁ está na mão (acabou de ser puxada pelo
            // PickupWeapon/Thief que vem imediatamente antes na lista de eventos), então a
            // queda usa o pêndulo normal de DropWeapon (a partir da mão), não o
            // DropWeaponFromHud (a partir do ícone na WeaponHUD). Hurt fire-and-forget em
            // paralelo com a queda — a vítima reage no mesmo instante em que a arma quebra.
            case CombatEventType.SaboteurBreak:
                if (defender != null)
                {
                    StartCoroutine(defender.animationController.PlayHurt((defender.settings?.hurtDuration ?? 0.07f) * t));
                    yield return StartCoroutine(PlayerCombat.DropWeapon(defender, isDisarm: false, isSabotage: true));
                }
                break;

            case CombatEventType.TurnStart:
                // Bug 2 (sorting): promove o atacante (corpo+arma) e demove o defensor pra
                // este turno — ver PlayerCombat.SetAttackerLayers. Nunca era chamado no path
                // ativo do simulador antes (só existia em AttackRoutine/legado, código morto
                // enquanto useSimulator=true), então corpo/arma de ambos ficavam sempre nas
                // sorting layers default da luta inteira — a arma (layer "Weapons", acima de
                // "Default") sempre na frente do corpo (layer "Default") de QUALQUER
                // personagem, o tempo todo.
                attacker?.SetAttackerLayers();

                // Pequeno buffer antes de qualquer PickupWeapon/CatchWeapon deste turno.
                // A transição "Idle → Catch Weapon" no Animator só existe a partir do
                // estado Idle (não AnyState) — em turnos extras por velocidade, o TurnEnd
                // anterior chama SetIdle(true) e o próximo TurnStart rodava 1 frame depois,
                // sem tempo do Animator de fato concluir a transição pro estado Idle antes
                // do trigger CatchWeapon ser setado. O trigger ficava pendente e só disparava
                // quando o Animator finalmente entrava em Idle — tarde, parecendo a animação
                // de pegar arma rodando no fim do turno em vez do início.
                yield return new WaitForSeconds(0.1f * t);
                break;

            case CombatEventType.RunToDefender:
                // Pets como alvo válido: corre até CharacterPetReach de distância do pet (CalcPetStopPosition)
                // em vez de calcular alcance de arma contra o personagem (CalcAttackPosition não
                // se aplica a pets) — sem isso o personagem corria pra dentro do sprite do pet.
                if (evt.targetIsPet)
                {
                    var runTargetPet = GetPet(evt.targetIndex, evt.targetPetIndex);
                    if (attacker != null && runTargetPet != null)
                    {
                        Vector3 runStopPos = CalcPetStopPosition(attacker.transform.position, runTargetPet.transform.position, CharacterPetReach(runTargetPet.petType));
                        runStopPos = ApplyPetOffset(runStopPos, attacker.transform.position, runTargetPet.transform.position,
                            attacker.weaponHandler.CurrentWeapon == null ? UnarmedAttacksPetOffset : ArmedAttacksPetOffset,
                            runTargetPet.petType);
                        yield return StartCoroutine(
                            attacker.animationController.PlayRun(runStopPos, (attacker.settings?.runSpeed ?? 35f) * t, attacker.movement));
                    }
                    break;
                }
                if (attacker != null && defender != null)
                {
                    Vector2 attackPos = CalcAttackPosition(attacker, defender);
                    yield return StartCoroutine(
                        attacker.animationController.PlayRun(attackPos, (attacker.settings?.runSpeed ?? 35f) * t, attacker.movement));
                }
                break;

            case CombatEventType.WeaponSwap:
                // Troca de arma estando armado — joga a atual no chão (mesmo pêndulo de
                // DropWeapon/WeaponDrop, remoção permanente do loadout — some do WeaponHUD,
                // não pode ser sacada de novo, igual a qualquer outra arma largada). Fire-and-
                // forget: o PickupWeapon que vem logo a seguir na lista de eventos já cobre a
                // animação de pegar a nova arma, não precisa esperar a queda terminar.
                if (attacker != null)
                    StartCoroutine(PlayerCombat.DropWeapon(attacker, isDisarm: false));
                yield return null;
                break;

            case CombatEventType.PickupWeapon:
                if (attacker != null)
                {
                    // Equipa exatamente a arma que o CombatSimulator sorteou para este evento
                    // (evt.weaponName) — EquipRandom() fazia um sorteio independente aqui,
                    // que podia equipar visualmente uma arma diferente da usada no cálculo
                    // de dano daquele turno.
                    var weaponToEquip = FindWeaponByName(attacker.weaponHandler.loadout, evt.weaponName);
                    if (weaponToEquip != null) attacker.weaponHandler.EquipSpecific(weaponToEquip);
                    else attacker.weaponHandler.EquipRandom();
                    yield return StartCoroutine(attacker.animationController.PlayCatchWeapon(0.6f * t));
                }
                break;

            case CombatEventType.Thief:
                // PlayerCombat.StealWeapon cobre o visual inteiro (pula nas costas do
                // adversário, balançam juntos, desce com a arma) e a troca de dono no
                // WeaponHandler/loadout de ambos — bloqueante (yield return StartCoroutine,
                // não fire-and-forget) porque o resto deste mesmo turno (Throw/Run/Melee logo
                // abaixo) depende do ladrão já estar armado quando a coroutine termina.
                if (attacker != null && defender != null)
                    yield return StartCoroutine(PlayerCombat.StealWeapon(attacker, defender));
                break;

            case CombatEventType.FlashFlood:
                GetSkillHUD(evt.playerIndex)?.UseSkill("Flash Flood");
                // Super "Flash Flood": pula até o próprio WeaponHUD (mesma conversão tela→mundo
                // do Saboteur, ScreenToWorldPoint sobre a posição do ícone da 1ª arma sorteada),
                // depois arremessa as armas escolhidas em sequência rápida — cada uma SEMPRE
                // ACERTA (o simulador já nunca rolou Dodge/Block pra nenhuma delas), aplicando
                // dano/popup/knockback no exato instante do impacto. As armas saem do próprio
                // WeaponHUD do atacante (RemoveWeapon) à medida que são arremessadas, igual ao
                // Saboteur remover o ícone da vítima — aqui é o atacante consumindo as próprias
                // armas. Sem salto de volta explícito: o TurnEnd que vem a seguir na lista já
                // detecta que o atacante saiu da zona de spawn e cuida do jump-back normalmente.
                if (attacker != null && defender != null && evt.ffWeapons != null && evt.ffWeapons.Count > 0)
                {
                    var attackerHud = GetWeaponHUD(evt.playerIndex);
                    var loadout     = attacker.weaponHandler.loadout;

                    Canvas.ForceUpdateCanvases();
                    // Altura do salto é um limite ABSOLUTO de tela (Y=1.86, topo da área
                    // visível), não mais um offset relativo somado à posição atual do atacante
                    // — somar 1.86 à posição atual variava o pico conforme a altura de onde o
                    // personagem partia; agora o destino é sempre o mesmo Y no topo da tela,
                    // qualquer que seja a posição de partida. Sem deslocamento horizontal (X
                    // igual ao do atacante — era puxado pro X do ícone na WeaponHUD antes, o que
                    // fazia o salto parecer um pêndulo balançando na diagonal em vez de subir
                    // reto, pedido pelo usuário pra tirar esse desvio).
                    const float screenTopY = 1.86f;
                    Vector3 jumpTarget = new Vector3(attacker.transform.position.x, screenTopY, attacker.transform.position.z);

                    // JumpTo calcula a duração como distância/velocidade. Duração fixa em vez de
                    // velocidade fixa — mesmo padrão de DodgeLeap (distância/duração, não
                    // duração/velocidade). Reduzida de 0.18s pra 0.12s (pedido pelo usuário pra
                    // acelerar ainda mais o salto inicial).
                    // ffJumpHeight (bump do arco parabólico do JumpTo) zerado — com o destino já
                    // sendo o próprio topo do salto (jumpTarget no limite da tela) e sem nenhum
                    // bump por cima, o ponto de chegada já É o ápice, então o arremesso a seguir
                    // acontece exatamente lá, sem o personagem ultrapassar e descer antes de
                    // arremessar.
                    float ffJumpHeight   = 0f;
                    float ffJumpDuration = 0.12f * t;
                    float ffJumpDist     = Vector2.Distance(attacker.transform.position, jumpTarget);
                    float ffJumpSpeed    = ffJumpDist > 0.01f ? ffJumpDist / ffJumpDuration : 1f;
                    yield return StartCoroutine(attacker.animationController.PlayJumpStart(0.02f * t));
                    yield return StartCoroutine(attacker.movement.JumpTo(jumpTarget, ffJumpSpeed, ffJumpHeight));
                    attacker.animationController.SetIdle(true);

                    // Delay antes do 1º arremesso, já no ápice do salto (0.20s, era 0.40s —
                    // reduzido a pedido do usuário).
                    yield return new WaitForSeconds(0.20f * t);

                    // Arremesso rápido e reto — sem giro/arco (pedido pelo usuário: a arma deve ir
                    // direto na direção do defensor, não girando como um throw normal de arma Thrown).
                    const float perThrowDuration = 0.15f;
                    for (int i = 0; i < evt.ffWeapons.Count; i++)
                    {
                        string wn         = evt.ffWeapons[i];
                        var    weaponData = FindWeaponByName(loadout, wn);

                        // Se o atacante estava armado, a arma em mão é sempre a 1ª arremessada
                        // (garantido pelo CombatSimulator) — desequipa o objeto visual da mão
                        // aqui, no exato momento em que ela é "jogada", sem remover de novo do
                        // loadout (o simulador já fez isso; o ícone da HUD some via
                        // attackerHud.RemoveWeapon mais abaixo, igual às outras 2).
                        if (i == 0 && attacker.weaponHandler.CurrentWeaponData != null)
                            attacker.weaponHandler.Unequip();

                        // Trigger "Throwing" a cada arremesso (mesmo trigger do ThrowWeapon
                        // normal) — sem isso não havia nenhuma ação/animação visível no
                        // personagem ao soltar cada arma. SetIdle(false) antes do trigger e
                        // SetIdle(true) depois do voo (não só no fim do loop) — mesmo padrão do
                        // combo de arremessos da Hideaway: sem o reset por arremesso, o 2º/3º
                        // trigger dispara enquanto o Animator ainda está preso no estado
                        // Throwing do anterior (bug documentado no CLAUDE.md, "parece que está
                        // com parkinson").
                        attacker.animationController.SetIdle(false);
                        attacker.GetComponent<Animator>()?.SetTrigger("Throwing");

                        if (weaponData?.inHandSprite != null)
                        {
                            var flying = new GameObject("FlashFloodWeapon");
                            flying.transform.position = jumpTarget;
                            Vector3 scale = attacker.transform.lossyScale * weaponData.scale;
                            flying.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

                            // Orienta o sprite na direção do voo (mesma técnica de ThrowRoutine/
                            // ThrowWeapon) — sem rotate contínuo, a arma não gira durante o voo.
                            Vector3 flightDir   = (defender.transform.position - jumpTarget).normalized;
                            float   flightAngle = Mathf.Atan2(flightDir.y, flightDir.x) * Mathf.Rad2Deg;
                            flying.transform.rotation = Quaternion.Euler(0f, 0f, flightAngle);

                            var sr = flying.AddComponent<SpriteRenderer>();
                            sr.sprite           = weaponData.inHandSprite;
                            sr.sortingLayerName = "Weapons";
                            sr.sortingOrder     = 10;

                            yield return StartCoroutine(attacker.FlyWeapon(
                                flying.transform, jumpTarget, defender.transform.position, perThrowDuration * t, rotate: false, arc: 0f));
                            Destroy(flying);
                        }
                        else
                        {
                            yield return new WaitForSeconds(perThrowDuration * t);
                        }
                        attacker.animationController.SetIdle(true);

                        ApplyHealthDelta(evt.targetIndex, evt.ffHpAfter[i]);

                        Vector3 ffPopupPos = defender.transform.position + Vector3.up * 1.5f
                            + Vector3.right * Random.Range(-0.3f, 0.3f);
                        DamagePopup.Spawn(ffPopupPos, evt.ffDamages[i], isCrit: false);
                        StartCoroutine(defender.Knockback(ComputePushDir(attacker, defender),
                            attacker.settings?.knockbackDistance ?? 0.5f, (attacker.settings?.hurtDuration ?? 0.07f) * t));
                        StartCoroutine(defender.animationController.PlayHurt((attacker.settings?.hurtDuration ?? 0.07f) * t));

                        if (weaponData != null)
                            attackerHud?.RemoveWeapon(weaponData);
                    }
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.HasteAttack:
                GetSkillHUD(evt.playerIndex)?.UseSkill("Haste");
                // Pets como alvo válido: dash simplificado (PlayPetTargetedSuper) em vez da
                // coreografia cheia de atravessar a tela — ver CombatSimulator.SimulateHaste.
                if (evt.targetIsPet)
                {
                    var hastePet = GetPet(evt.targetIndex, evt.targetPetIndex);
                    yield return StartCoroutine(PlayPetTargetedSuper(attacker, hastePet, t, () =>
                    {
                        if (hastePet == null) return;
                        Vector3 petPopupPos = hastePet.transform.position + Vector3.up * 0.8f;
                        if (evt.isDodged)
                        {
                            DamagePopup.SpawnDodge(petPopupPos);
                            Vector2 hasteDodgeDir = ((Vector2)hastePet.transform.position - (Vector2)attacker.transform.position).normalized;
                            StartCoroutine(hastePet.DodgeLeap(hasteDodgeDir, t));
                        }
                        else
                        {
                            hastePet.healthBar?.UpdateBar(evt.newTargetHp, hastePet.maxHp);
                            DamagePopup.Spawn(petPopupPos, evt.damage, evt.isCrit);
                            hastePet.animController.PlayHurt();
                            if (evt.newTargetHp <= 0) hastePet.PlayDeath();
                        }
                    }));
                    yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                    break;
                }

                // Super "Haste": dash que atravessa o defensor — corre até a posição dele (onde
                // o resultado é resolvido, exatamente na sobreposição), continua na mesma direção
                // até sair pela borda da tela, reaparece do lado oposto e corre de volta pra
                // posição original. Direção calculada a partir de quem está atacando (não fixa
                // "direita"/"esquerda" — funciona pros dois lados).
                if (attacker != null && defender != null)
                {
                    Vector3 startPos = attacker.transform.position;
                    float   dir      = Mathf.Sign(defender.transform.position.x - startPos.x);
                    if (dir == 0f) dir = 1f;

                    const float offscreenX  = 10f; // além do limite jogável (±7.25), fora da câmera
                    Vector3 exitPos  = new Vector3(dir * offscreenX, startPos.y, startPos.z);
                    Vector3 enterPos = new Vector3(-dir * offscreenX, startPos.y, startPos.z);

                    // Duração fixa por trecho (mesmo padrão de DodgeLeap/Flash Flood: duração
                    // fixa, velocidade derivada da distância) — bem mais rápido que o run normal,
                    // pedido pelo usuário ("corrida em alta velocidade").
                    float legDuration = 0.12f * t;
                    float distToDefender = Vector2.Distance(startPos, defender.transform.position);
                    float speedToDefender = distToDefender > 0.01f ? distToDefender / legDuration : 1f;

                    attacker.GetComponent<Animator>()?.SetBool("Running", true);

                    // 1ª perna: até a posição do defensor — é ali que o resultado é aplicado,
                    // no instante exato da sobreposição.
                    yield return StartCoroutine(attacker.movement.MoveTo(defender.transform.position, speedToDefender));

                    Vector3 popupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    if (evt.isBlocked)
                    {
                        DamagePopup.SpawnBlock(popupPos);
                        StartCoroutine(defender.Knockback(ComputePushDir(attacker, defender),
                            (attacker.settings?.knockbackDistance ?? 0.5f) * 0.5f, (attacker.settings?.hurtDuration ?? 0.07f) * t));
                        StartCoroutine(defender.animationController.PlayBlock(0.36666667f * t));
                    }
                    else if (evt.isDodged)
                    {
                        DamagePopup.SpawnMiss(popupPos);
                        StartCoroutine(defender.DodgeLeap(ComputePushDir(attacker, defender), attacker.settings?.knockbackDistance ?? 0.5f));
                    }
                    else
                    {
                        ApplyHealthDelta(evt.targetIndex, evt.newHp);
                        DamagePopup.Spawn(popupPos, evt.damage, evt.isCrit);
                        StartCoroutine(defender.Knockback(ComputePushDir(attacker, defender),
                            attacker.settings?.knockbackDistance ?? 0.5f, (attacker.settings?.hurtDuration ?? 0.07f) * t));
                        StartCoroutine(defender.animationController.PlayHurt((attacker.settings?.hurtDuration ?? 0.07f) * t));
                    }

                    // 2ª perna: continua na mesma direção (ainda correndo) até sair da tela.
                    float distToExit  = Vector2.Distance(defender.transform.position, exitPos);
                    float speedToExit = distToExit > 0.01f ? distToExit / legDuration : 1f;
                    yield return StartCoroutine(attacker.movement.MoveTo(exitPos, speedToExit));
                    attacker.animationController.SetIdle(true);

                    // Reaparece do lado oposto. Volta pra posição original com um lerp rápido
                    // (não uma corrida visível) — o personagem estaria de costas/virado pro
                    // lado errado pra "correr de volta" sem um sistema de flip dedicado, então
                    // um reposicionamento rápido cobre o pedido ("lerp rápido ou fade").
                    attacker.transform.position = enterPos;
                    const float returnDuration = 0.15f;
                    float elapsed = 0f;
                    while (elapsed < returnDuration * t)
                    {
                        elapsed += Time.deltaTime;
                        attacker.transform.position = Vector3.Lerp(enterPos, startPos, elapsed / (returnDuration * t));
                        yield return null;
                    }
                    attacker.transform.position = startPos;
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.PiledriverAttack:
                GetSkillHUD(evt.playerIndex)?.UseSkill("Piledriver");
                // Pets como alvo válido: sem o grab/salto cheio (não faz sentido erguer um pet
                // pequeno em arco até o topo da tela) — PlayPetTargetedSuper cobre a abordagem +
                // impacto. Nunca esquivado, mesmo padrão do personagem (ver SimulatePiledriver).
                if (evt.targetIsPet)
                {
                    var piledriverPet = GetPet(evt.targetIndex, evt.targetPetIndex);
                    yield return StartCoroutine(PlayPetTargetedSuper(attacker, piledriverPet, t, () =>
                    {
                        if (piledriverPet == null) return;
                        piledriverPet.healthBar?.UpdateBar(evt.newTargetHp, piledriverPet.maxHp);
                        DamagePopup.Spawn(piledriverPet.transform.position + Vector3.up * 0.8f, evt.damage, evt.isCrit);
                        piledriverPet.animController.PlayHurt();
                        if (evt.newTargetHp <= 0) piledriverPet.PlayDeath();
                    }));
                    yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                    break;
                }

                // Super "Piledriver": agarra o defensor, sobe com ele em arco até o topo da
                // tela, e cai de volta exatamente na posição original do defensor — onde o
                // impacto é resolvido. Nunca é esquivado/bloqueado (o simulador já garante
                // isso, sem checagem nenhuma aqui).
                if (attacker != null && defender != null)
                {
                    Vector3 originalDefenderPos = defender.transform.position;

                    // Fase 1 — Abordagem: corre até o defensor, igual a um run normal.
                    yield return StartCoroutine(RepositionIfNeeded(attacker, defender, t));

                    // Fase 2 — Grab + Salto: sem animação de grab dedicada no Animator —
                    // reusa Idle (sem trigger de swing), e o defensor "agarrado" passa a se
                    // mover junto do atacante via offset de posição em vez de reagir por conta
                    // própria. Ambos sobem em arco parabólico até o centro/topo da tela.
                    Vector3 grabPos     = attacker.transform.position;
                    Vector3 carryOffset = new Vector3(0f, 0.6f, 0f);
                    Vector3 apexPos     = new Vector3((grabPos.x + originalDefenderPos.x) * 0.5f, 1.7f, grabPos.z);

                    attacker.animationController.SetIdle(false);
                    defender.animationController.SetIdle(true);
                    attacker.GetComponent<Animator>()?.SetBool("JumpStart", true);

                    float riseDuration = 0.35f * t;
                    const float riseBump = 0.6f;
                    float elapsed = 0f;
                    while (elapsed < riseDuration)
                    {
                        elapsed += Time.deltaTime;
                        float p = Mathf.Clamp01(elapsed / riseDuration);
                        Vector3 pos = Vector3.Lerp(grabPos, apexPos, p);
                        pos.y += Mathf.Sin(Mathf.PI * p) * riseBump;
                        attacker.transform.position = pos;
                        defender.transform.position = pos + carryOffset;
                        yield return null;
                    }
                    attacker.transform.position = apexPos;
                    defender.transform.position = apexPos + carryOffset;

                    // Fase 3 — Queda + Impacto: caem em queda livre acelerada (ease-in, não
                    // linear) até a posição original do defensor — é ali, no chão, que o
                    // impacto acontece.
                    float fallDuration = 0.18f * t;
                    elapsed = 0f;
                    while (elapsed < fallDuration)
                    {
                        elapsed += Time.deltaTime;
                        float p = Mathf.Clamp01(elapsed / fallDuration);
                        float eased = p * p;
                        Vector3 pos = Vector3.Lerp(apexPos, originalDefenderPos, eased);
                        attacker.transform.position = pos;
                        defender.transform.position = pos + carryOffset;
                        yield return null;
                    }
                    attacker.transform.position = originalDefenderPos;
                    defender.transform.position = originalDefenderPos;
                    attacker.GetComponent<Animator>()?.SetBool("JumpStart", false);
                    attacker.animationController.SetIdle(true);

                    if (piledriverEffectPrefab != null)
                    {
                        var fx = Instantiate(piledriverEffectPrefab, originalDefenderPos + new Vector3(0f, -0.049f, 0f), Quaternion.identity);
                        fx.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                    }

                    ApplyHealthDelta(evt.targetIndex, evt.newHp);
                    DamagePopup.Spawn(originalDefenderPos + Vector3.up * 1.5f, evt.damage, evt.isCrit);
                    StartCoroutine(defender.animationController.PlayHurt((attacker.settings?.hurtDuration ?? 0.07f) * t));

                    // Defensor já está de volta na própria posição original (o ponto de
                    // impacto É essa posição) — sem reposicionamento extra necessário. O
                    // atacante fica fora da própria zona de spawn; o TurnEnd seguinte na
                    // lista de eventos já detecta isso e cuida do jump-back normalmente,
                    // mesmo padrão "sem salto de volta explícito" do Flash Flood.
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.NetThrow:
                GetSkillHUD(evt.playerIndex)?.UseSkill("Net");
                // Super "Net": SEMPRE acerta (sem swing/reposicionamento condicionado a
                // dodge/block, igual ao Flash Flood) — arremessa net1 (mesmo trigger Throwing
                // de qualquer arremesso) e, ao chegar, liga a Fase 2 (rede caída + pose
                // agachada) no defensor via PlayerCombat.ShowNetEnsnared — que continua rodando
                // por conta própria através de qualquer NetEnsnaredSkip seguinte, até o
                // NetFreed correspondente.
                if (attacker != null && defender != null)
                {
                    Vector3 netLaunchPos = attacker.weaponHandler.handBone != null
                        ? attacker.weaponHandler.handBone.position
                        : attacker.transform.position + Vector3.up * 0.5f;

                    attacker.animationController.SetIdle(false);
                    attacker.GetComponent<Animator>()?.SetTrigger("Throwing");

                    // Destino da rede: pet alvo (prioridade) ou personagem principal.
                    var netTargetPet = evt.targetIsPet ? GetPet(evt.targetIndex, evt.targetPetIndex) : null;
                    Vector3 netDest = netTargetPet != null
                        ? netTargetPet.transform.position + Vector3.up * 0.3f
                        : defender.transform.position;

                    if (netFlyingSprite != null)
                    {
                        var net1 = new GameObject("NetFlying");
                        net1.transform.position   = netLaunchPos;
                        net1.transform.localScale = Vector3.one * Net1FlyingScale;
                        var sr = net1.AddComponent<SpriteRenderer>();
                        sr.sprite           = netFlyingSprite;
                        sr.sortingLayerName = "Weapons";
                        sr.sortingOrder     = 10;

                        yield return StartCoroutine(attacker.FlyWeapon(
                            net1.transform, netLaunchPos, netDest, 0.4f * t, rotate: false, arc: 0.5f));
                        Destroy(net1);
                    }
                    else
                    {
                        yield return new WaitForSeconds(0.4f * t);
                    }
                    attacker.animationController.SetIdle(true);

                    // Rede sempre prioriza pets (100% quando há pet vivo, ver TryActivateNet).
                    // Personagem só fica enredado quando não há pets vivos disponíveis.
                    if (evt.targetIsPet && netTargetPet != null)
                        netTargetPet.animController.ShowNetEnsnared(netLandedSprite, Net2LandedScale);
                    else if (!evt.targetIsPet)
                        defender.ShowNetEnsnared(netLandedSprite, Net2LandedScale);
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.NetEnsnaredSkip:
                // Defensor enredado perde o turno inteiro — Face 02 e a rede oscilando (net2)
                // já estão rodando desde o NetThrow original (PlayerCombat.ShowNetEnsnared),
                // sem precisar de nada extra aqui além de aguardar a duração de um turno
                // normal antes do TurnEnd seguinte, igual ao StunSkip.
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.NetFreed:
                // Quem foi libertado vem em evt.playerIndex (igual ao StunSkip — "attacker" é
                // só o nome da variável local, não o papel real no evento).
                if (attacker != null)
                    yield return StartCoroutine(PlayNetBreakEffect(attacker, t));
                break;

            case CombatEventType.FierceBruteActivated:
                GetSkillHUD(evt.playerIndex)?.UseSkill("Fierce Brute");
                // Super "Fierce Brute": NÃO consome o turno (ver CombatSimulator.SimulateTurn,
                // item "0e") — só liga o ghost trail + aura persistente aqui. A pose de braço
                // levantado NÃO toca neste evento (1ª versão tocava aqui, na posição de spawn,
                // antes de correr até o defensor — ficava parecendo um slash sendo dado no lugar
                // errado, bug reportado pelo usuário); ela foi movida pro case Hit (ver
                // PlayFierceBrutePose), que já roda DEPOIS de RepositionIfNeeded — ou seja, na
                // posição correta, bem ao lado do defensor, imediatamente antes do swing de
                // verdade. O resto do turno (Thief/pickup/throw/melee) continua normalmente nos
                // eventos seguintes da mesma lista.
                if (attacker != null)
                {
                    // Ghost trail (efeito Matrix) — fire-and-forget, sem yield, pra não atrasar
                    // o resto da sequência. Duração bem maior que antes (pedido do usuário: o
                    // trail não durava o suficiente pra ainda estar tocando durante a corrida até
                    // o defensor, que só começa no RunToDefender — evento futuro, ainda nem
                    // emitido aqui) — 1.5s cobre com folga a corrida + a pose + o swing seguintes.
                    // Escalado por t (1x/2x), igual a todo o resto da sequência.
                    StartCoroutine(SpawnGhostTrail(attacker.transform, 1.5f * t, 0.06f * t, new Color(0.5f, 0f, 0.8f, 0.6f)));

                    // Aura persistente — fica ligada através de qualquer evento seguinte (Thief/
                    // pickup/throw/RunToDefender) até o Hit que consome o buff (ver case Hit,
                    // isFierceBrute) ou até falhar por dodge/block/counter/reversal/arremesso
                    // (ver CombatSimulator.SimulateHit/SimulateTurn).
                    attacker.ShowFierceBruteAura();
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.BombThrow:
                GetSkillHUD(evt.playerIndex)?.UseSkill("Bomb");
                // Super "Bomb": arremesso em pêndulo (arco parabólico, mesma base de FlyWeapon)
                // até o defensor, seguido de explosão em área que atinge todos os alvos do lado
                // inimigo (evt.bombTargets — hoje só o defensor, ver CombatSimulator.
                // GetEnemyTargets). NUNCA esquivado/bloqueado, sem knockback individual — o
                // simulador já resolveu o resultado, aqui só toca a animação e aplica os dados.
                if (attacker != null && defender != null && evt.bombTargets != null && evt.bombTargets.Count > 0)
                {
                    Vector3 launchPos = attacker.weaponHandler.handBone != null
                        ? attacker.weaponHandler.handBone.position
                        : attacker.transform.position + Vector3.up * 0.5f;
                    Vector3 impactPos = defender.transform.position;

                    attacker.animationController.SetIdle(false);
                    attacker.GetComponent<Animator>()?.SetTrigger("Throwing");

                    // Fase 1 — Arremesso em pêndulo: arco parabólico (bump de seno, mesma curva
                    // de FlyWeapon) + rotação contínua manual de 200°/s — diferente dos 540°/s
                    // fixos de FlyWeapon, então não dá pra reusar o helper direto; loop próprio.
                    if (bombPrefab != null)
                    {
                        var bomb = Instantiate(bombPrefab, launchPos, Quaternion.identity);
                        // Desliga o Animator antes do 1º frame rodar — o controller já criado
                        // pelo usuário tem um único estado (a própria explosão), que tocaria
                        // imediatamente ao instanciar e sobrescreveria o sprite estático "bomb"
                        // usado durante o voo. Religado só na 2ª instância, no impacto (abaixo).
                        var bombAnimator = bomb.GetComponent<Animator>();
                        if (bombAnimator != null) bombAnimator.enabled = false;

                        const float arcHeight = 0.6f;
                        float elapsed = 0f;
                        while (elapsed < BombFlightDuration * t)
                        {
                            float p = elapsed / (BombFlightDuration * t);
                            Vector3 pos = Vector3.Lerp(launchPos, impactPos, p);
                            pos.y += arcHeight * Mathf.Sin(p * Mathf.PI);
                            bomb.transform.position = pos;
                            bomb.transform.Rotate(0f, 0f, 200f * Time.deltaTime);
                            elapsed += Time.deltaTime;
                            yield return null;
                        }
                        Destroy(bomb);
                    }
                    else
                    {
                        yield return new WaitForSeconds(BombFlightDuration * t);
                    }
                    attacker.animationController.SetIdle(true);

                    // Fase 2 — Impacto + Explosão: 2ª instância do mesmo prefab, centrada no
                    // defensor, escala grande ("deve dominar a tela") e sorting layer Characters
                    // (na frente de tudo durante a explosão). Animator religado aqui — único
                    // estado do controller já é a explosão, toca direto ao ligar.
                    if (bombPrefab != null)
                    {
                        var fx = Instantiate(bombPrefab, impactPos, Quaternion.identity);
                        fx.transform.localScale = Vector3.one * BombExplosionScale;
                        var fxAnimator = fx.GetComponent<Animator>();
                        if (fxAnimator != null) fxAnimator.enabled = true;
                        var fxRenderer = fx.GetComponent<SpriteRenderer>();
                        if (fxRenderer != null)
                        {
                            fxRenderer.sortingLayerName = "Characters";
                            fxRenderer.sortingOrder     = 30;
                        }
                        if (fx.GetComponent<AnimationAutoDestroy>() == null)
                            fx.AddComponent<AnimationAutoDestroy>();
                    }

                    // Screen flash único pra explosão inteira (não um por alvo) — sinaliza o
                    // impacto em área sem empilhar vários flashes sobrepostos quando houver
                    // múltiplos alvos no futuro (pets/backup).
                    StartCoroutine(FlashScreenWhite(0.15f, 0.3f));

                    for (int i = 0; i < evt.bombTargets.Count; i++)
                    {
                        int targetIndex  = evt.bombTargets[i];
                        var targetCombat = GetCombat(targetIndex);
                        if (targetCombat == null) continue;

                        ApplyHealthDelta(targetIndex, evt.bombTargetHp[i]);

                        Vector3 popupPos = targetCombat.transform.position + Vector3.up * 1.5f
                            + Vector3.right * Random.Range(-0.3f, 0.3f);
                        DamagePopup.Spawn(popupPos, evt.bombTargetDamages[i], isCrit: false);

                        StartCoroutine(targetCombat.animationController.PlayHurt((attacker.settings?.hurtDuration ?? 0.07f) * t));

                        // Net quebrada pela explosão: mesmos fragmentos voadores de NetFreed,
                        // fire-and-forget — sincronizado com o impacto, sem esperar um evento
                        // NetFreed separado mais adiante na lista.
                        if (evt.netFreedTargets != null && evt.netFreedTargets.Contains(targetIndex))
                            StartCoroutine(PlayNetBreakEffect(targetCombat, t));
                    }

                    // Pets vivos do defensor atingidos pela mesma explosão (evt.bombPetIndexes/
                    // bombPetHp, paralelas entre si — ver CombatSimulator.TryActivateBomb).
                    if (evt.bombPetIndexes != null)
                    {
                        for (int i = 0; i < evt.bombPetIndexes.Count; i++)
                        {
                            var petCombat = GetPet(evt.targetIndex, evt.bombPetIndexes[i]);
                            if (petCombat == null) continue;

                            petCombat.healthBar?.UpdateBar(evt.bombPetHp[i], petCombat.maxHp);
                            Vector3 petPopupPos = petCombat.transform.position + Vector3.up * 0.8f;
                            DamagePopup.Spawn(petPopupPos, evt.damage, isCrit: false);
                            petCombat.animController.PlayHurt();
                            if (evt.bombPetHp[i] <= 0) petCombat.PlayDeath();
                        }
                    }

                    yield return new WaitForSeconds(0.6f * t);
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.TragicPotionUse:
                GetSkillHUD(evt.playerIndex)?.UseSkill("Tragic Potion");
                // Super "Tragic Potion": auto-cura, não ataca ninguém — não rola Dodge/Block/
                // Counter/Reversal, e o simulador já resolveu o resultado (evt.healAmount/newHp);
                // aqui só toca a sequência visual (pegar/beber a poção, partículas de cura,
                // popup verde) e sincroniza a barra de vida.
                if (attacker != null)
                {
                    yield return StartCoroutine(PlayTragicPotion(attacker, evt, t));
                    // Cura o veneno do Chef (CombatSimulator.TryActivateTragicPotion já zera
                    // poisoned = false) — no-op se este personagem nunca esteve envenenado
                    // (HidePoisonAura só age se a aura existir).
                    attacker.HidePoisonAura(0.3f);
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.FastMetabolismRegen:
                // Passiva: cura 1% do HP máximo TODO turno — fire-and-forget (não pode atrasar
                // o ritmo do combate, já que acontece em todo turno do personagem). Aplica a
                // cura/popup já, sem esperar a animaçãozinha terminar.
                if (attacker != null)
                {
                    StartCoroutine(PlayFastMetabolismRegen(attacker, t));
                    ApplyHealthDelta(evt.playerIndex, evt.newHp);
                    DamagePopup.SpawnHeal(attacker.transform.position + Vector3.up * 1.5f, evt.healAmount, fontSize: 4f);
                }
                yield return null;
                break;

            case CombatEventType.FastMetabolismPulse:
                // Burst de cura intensa abaixo de 50% HP — as 10 curas de 5% chegam todas em
                // sequência no mesmo turno (CombatSimulator), não mais uma por turno. Na 1ª cura
                // (pulseCount == 1) spawna as folhas orbitando; a cada cura, popup verde (tamanho
                // normal, maior que o da regeneração passiva) + flash verde no personagem; ao
                // chegar em 10 curas, já desfaz a aura (esgotado). Este case agora BLOQUEIA (não
                // é mais fire-and-forget) com um pequeno delay entre cada cura — pedido do
                // usuário: cada +5 precisa aparecer individualmente, e o personagem fica parado
                // (nenhum outro evento do turno, ex: RunToDefender, começa a tocar) até o burst
                // inteiro terminar. Interrupção por dano não tem evento próprio — tratada
                // reativamente em ApplyHealthDelta, que desfaz a aura no instante em que o
                // personagem leva qualquer dano.
                if (attacker != null)
                {
                    if (evt.pulseCount == 1)
                        SpawnFastMetabolismLeaves(attacker, evt.playerIndex);

                    ApplyHealthDelta(evt.playerIndex, evt.newHp);
                    DamagePopup.SpawnHeal(attacker.transform.position + Vector3.up * 1.5f, evt.healAmount);
                    StartCoroutine(FlashCharacterGreen(attacker, 0.2f));

                    if (evt.pulseCount >= 10)
                        FadeFastMetabolismLeaves(evt.playerIndex);
                }
                yield return new WaitForSeconds(0.3f * t);
                break;

            case CombatEventType.VampirismAttack:
                GetSkillHUD(evt.playerIndex)?.UseSkill("Vampirism");
                // Pets como alvo válido: sem a coreografia de "montar nas costas" (StealWeapon-
                // like), não faz sentido contra um pet — PlayPetTargetedSuper resolve a mordida
                // de forma simplificada; cura do atacante continua igual (sempre na vida dele).
                if (evt.targetIsPet)
                {
                    var vampirismPet = GetPet(evt.targetIndex, evt.targetPetIndex);
                    yield return StartCoroutine(PlayPetTargetedSuper(attacker, vampirismPet, t, () =>
                    {
                        if (vampirismPet != null)
                        {
                            vampirismPet.healthBar?.UpdateBar(evt.newTargetHp, vampirismPet.maxHp);
                            DamagePopup.Spawn(vampirismPet.transform.position + Vector3.up * 0.8f, evt.damage, isCrit: false);
                            vampirismPet.animController.PlayHurt();
                            if (evt.newTargetHp <= 0) vampirismPet.PlayDeath();
                        }
                        if (attacker != null)
                        {
                            ApplyHealthDelta(evt.playerIndex, evt.newAttackerHp);
                            DamagePopup.SpawnHeal(attacker.transform.position + Vector3.up * 1.5f, evt.healAmount);
                        }
                    }));
                    yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                    break;
                }

                // Super "Vampirism": mordida garantida (nunca esquivada/bloqueada) — o
                // simulador já resolveu dano/cura (evt.damage/evt.healAmount); PlayerCombat.
                // VampirismRoutine cobre o visual inteiro (salto nas costas, bounce, efeito
                // corpo→boca, salto de volta), mesma estrutura de StealWeapon/Thief. O dano ao
                // defensor e a cura do atacante são aplicados via ApplyHealthDelta (centraliza
                // Survival-safety/Fast Metabolism, ver helper) dentro do callback onBiteComplete,
                // entre o fim do bounce e o salto de volta.
                if (attacker != null && defender != null)
                {
                    yield return StartCoroutine(PlayerCombat.VampirismRoutine(attacker, defender, vampirismEffectController, t, () =>
                    {
                        ApplyHealthDelta(evt.targetIndex, evt.newDefenderHp);
                        DamagePopup.Spawn(defender.transform.position + Vector3.up * 1.5f, evt.damage, isCrit: false);

                        ApplyHealthDelta(evt.playerIndex, evt.newAttackerHp);
                        DamagePopup.SpawnHeal(attacker.transform.position + Vector3.up * 1.5f, evt.healAmount);
                    }));
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.ChefPizzaThrow:
                // Passivo de combate "Chef": pizza envenenada lançada uma única vez, na 1ª ação
                // do dono da skill — SEMPRE acerta (sem swing/reposicionamento condicionado a
                // dodge/block, mesmo padrão de Net/Bomb). Só a Fase 1 (voo, base idêntica ao
                // Bomb) acontece aqui — a pizza some ao chegar SEM explodir (pedido do usuário:
                // a explosão/dano não é mais no momento do lançamento, ver case PoisonDamage
                // abaixo, onde ela tica de fato no fim de cada turno futuro do envenenado). Aqui
                // só aplica PlayHurt (reação a ter sido atingido pela pizza) e liga a aura verde
                // persistente.
                if (attacker != null && defender != null)
                {
                    Vector3 launchPos = attacker.weaponHandler.handBone != null
                        ? attacker.weaponHandler.handBone.position
                        : attacker.transform.position + Vector3.up * 0.5f;
                    Vector3 impactPos = defender.transform.position;

                    attacker.animationController.SetIdle(false);
                    attacker.GetComponent<Animator>()?.SetTrigger("Throwing");

                    if (chefPizzaPrefab != null)
                    {
                        var pizza = Instantiate(chefPizzaPrefab, launchPos, Quaternion.identity);
                        pizza.transform.localScale = Vector3.one * ChefPizzaScale;
                        // Mesmo motivo do Bomb: o Animator do prefab já tocaria a explosão
                        // imediatamente ao instanciar (único estado do controller), sobrescrevendo
                        // o sprite estático "chef" usado durante o voo — fica desligado aqui (a
                        // pizza nunca explode no lançamento, só some — ver comentário acima).
                        var pizzaAnimator = pizza.GetComponent<Animator>();
                        if (pizzaAnimator != null) pizzaAnimator.enabled = false;

                        const float arcHeight     = 0.6f;
                        const float flightDuration = 0.5f;
                        float elapsed = 0f;
                        while (elapsed < flightDuration * t)
                        {
                            float p = elapsed / (flightDuration * t);
                            Vector3 pos = Vector3.Lerp(launchPos, impactPos, p);
                            pos.y += arcHeight * Mathf.Sin(p * Mathf.PI);
                            pizza.transform.position = pos;
                            pizza.transform.Rotate(0f, 0f, 300f * Time.deltaTime);
                            elapsed += Time.deltaTime;
                            yield return null;
                        }
                        Destroy(pizza);
                    }
                    else
                    {
                        yield return new WaitForSeconds(0.5f * t);
                    }

                    StartCoroutine(defender.animationController.PlayHurt((attacker.settings?.hurtDuration ?? 0.07f) * t));
                    defender.ShowPoisonAura();

                    attacker.animationController.SetIdle(true);
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.PoisonDamage:
                // Veneno da skill Chef — pedido do usuário: a explosão (chef.anim) só toca
                // DEPOIS que o envenenado já pulou de volta pro ponto de partida (TurnEnd, agora
                // emitido ANTES deste evento — ver CombatSimulator.EmitTurnEnd) — nunca enquanto
                // ele ainda está na posição em que agiu. Diferente do resto da skill (fire-and-
                // forget, mesmo padrão da regeneração passiva do Fast Metabolism), este case é
                // BLOQUEANTE — espera a explosão terminar de tocar antes de liberar a próxima
                // ação da lista de eventos (pedido explícito do usuário).
                if (attacker != null)
                {
                    float poisonFxDuration = 1f;
                    if (chefPizzaPrefab != null)
                    {
                        var fx = Instantiate(chefPizzaPrefab, attacker.transform.position, Quaternion.identity);
                        fx.transform.localScale = Vector3.one * ChefPizzaScale;
                        var fxAnimator = fx.GetComponent<Animator>();
                        if (fxAnimator != null) fxAnimator.enabled = true;
                        var fxRenderer = fx.GetComponent<SpriteRenderer>();
                        if (fxRenderer != null)
                        {
                            fxRenderer.sortingLayerName = "Characters";
                            fxRenderer.sortingOrder     = 20;
                        }
                        if (fx.GetComponent<AnimationAutoDestroy>() == null)
                            fx.AddComponent<AnimationAutoDestroy>();

                        var fxClips = fxAnimator != null ? fxAnimator.runtimeAnimatorController?.animationClips : null;
                        if (fxClips != null && fxClips.Length > 0) poisonFxDuration = fxClips[0].length;
                    }

                    StartCoroutine(FlashCharacterGreen(attacker, 0.15f));
                    ApplyHealthDelta(evt.playerIndex, evt.newHp);
                    DamagePopup.SpawnPoison(attacker.transform.position + Vector3.up * 1.5f, evt.damage);

                    yield return new WaitForSeconds(poisonFxDuration * t);
                }
                break;

            case CombatEventType.Hit:
                // Pets como alvo válido: melee/throw redirecionado pra um pet em vez do
                // personagem (ver CombatSimulator.SimulateHit/SimulateThrow) — sem
                // RepositionIfNeeded (assume PlayerCombat defender) nem Counter/Block/Reversal/
                // Disarm visuais (o simulador já não emite nenhum desses pra alvo pet); só
                // aproxima do pet se precisar (combo hit depois do pet já ter sido atingido) e
                // aplica swing + hurt/morte direto nele.
                if (evt.targetIsPet)
                {
                    var hitPet = GetPet(evt.targetIndex, evt.targetPetIndex);
                    if (hitPet != null && attacker != null)
                    {
                        float swingMultPet = SwingSpeedMultiplier(attacker);
                        float slashHalfPet = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t / swingMultPet;

                        if (!evt.isThrow)
                        {
                            float distToPet = Vector2.Distance(attacker.transform.position, hitPet.transform.position);
                            if (distToPet > 1.0f)
                            {
                                Vector3 hitPetStopPos = CalcPetStopPosition(attacker.transform.position, hitPet.transform.position, CharacterPetReach(hitPet.petType));
                                hitPetStopPos = ApplyPetOffset(hitPetStopPos, attacker.transform.position, hitPet.transform.position,
                                    attacker.weaponHandler.CurrentWeapon == null ? UnarmedAttacksPetOffset : ArmedAttacksPetOffset,
                                    hitPet.petType);
                                yield return StartCoroutine(attacker.animationController.PlayRun(hitPetStopPos, (attacker.settings?.runSpeed ?? 35f) * t, attacker.movement));
                            }

                            string triggerPet = SwingTrigger(attacker);
                            if (swingMultPet != 1f) attacker?.animationController.SetSpeed(swingMultPet);
                            attacker?.GetComponent<Animator>()?.SetTrigger(triggerPet);
                            yield return new WaitForSeconds(slashHalfPet);
                        }

                        Vector3 petPopupPos = hitPet.transform.position + Vector3.up * 0.8f;
                        if (evt.petShieldAbsorb)
                        {
                            // Escudo do Treat absorveu o golpe — destroi o visual, sem dano.
                            if (hitPet.shieldVisual != null) { Destroy(hitPet.shieldVisual); hitPet.shieldVisual = null; }
                            DamagePopup.SpawnBlock(petPopupPos);
                        }
                        else
                        {
                            hitPet.healthBar?.UpdateBar(evt.newTargetHp, hitPet.maxHp);
                            if (evt.isFierceBrute)
                            {
                                // Fierce Brute conectou num pet — mesmo dobro de dano já aplicado
                                // pelo simulador; só a aura/popup diferenciados, sem flash de tela
                                // nem ghost trail extra (mantém o caso pet mais discreto).
                                DamagePopup.SpawnFierceBrute(petPopupPos, evt.damage, evt.isCrit);
                                attacker?.HideFierceBruteAura(0.2f);
                            }
                            else
                            {
                                DamagePopup.Spawn(petPopupPos, evt.damage, evt.isCrit);
                            }
                            hitPet.animController.PlayHurt();
                            BloodEffectPlayer.PlayHit(hitPet.transform.position + Vector3.up * 0.3f);
                            if (evt.newTargetHp <= 0) hitPet.PlayDeath();
                        }

                        if (!evt.isThrow)
                        {
                            yield return new WaitForSeconds(slashHalfPet);
                            if (swingMultPet != 1f) attacker?.animationController.SetSpeed(1f);
                        }
                    }
                    yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                    break;
                }
                if (defender != null)
                {
                    float swingMult = SwingSpeedMultiplier(attacker);
                    float slashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t / swingMult;

                    // Thrown-weapon hits already had their "windup" during ThrowWeapon's flight —
                    // the attacker has no weapon in hand anymore, so don't re-trigger a melee
                    // swing or wait out slashHalf again; apply the impact immediately instead.
                    if (!evt.isThrow)
                    {
                        yield return StartCoroutine(RepositionIfNeeded(attacker, defender, t));

                        // Fierce Brute: pose de braço levantado (power-up) AQUI — já depois do
                        // reposicionamento, ou seja, na posição certa ao lado do defensor — em
                        // vez de no case FierceBruteActivated (posição de spawn, antes de correr;
                        // bug corrigido, ver comentário lá).
                        if (evt.isFierceBrute && attacker != null)
                            yield return StartCoroutine(PlayFierceBrutePose(attacker, t));

                        string trigger = SwingTrigger(attacker);
                        if (swingMult != 1f) attacker?.animationController.SetSpeed(swingMult);
                        attacker?.GetComponent<Animator>()?.SetTrigger(trigger);
                        yield return new WaitForSeconds(slashHalf);
                    }

                    // Impact moment: damage, hurt animation, knockback, and popup all fire
                    // together here instead of being staggered — health used to only update
                    // once the separate HealthChanged event was reached afterward, and the
                    // popup only appeared once PlayHurt finished, both visibly lagging the hit.
                    Vector2 pushDir = ComputePushDir(attacker, defender);
                    float   kbDist  = attacker?.settings?.knockbackDistance ?? 0.5f;
                    float   kbDur   = attacker?.settings?.hurtDuration ?? 0.07f;

                    // Bug 1 (weapon drop): se este hit vai desarmar o defensor (Disarm emitido
                    // mais à frente na lista, ver CombatSimulator.SimulateHit — sempre depois de
                    // Hit/HealthChanged, possivelmente depois de Sabotage/Iron Head/Reversal
                    // também), dispara a queda da arma JÁ NESTE INSTANTE (junto do resto do
                    // impacto) em vez de esperar o Hit inteiro (swing + hurt + comboDelay)
                    // terminar e só então o case Disarm, bem mais tarde, começar a animação de
                    // queda — a arma "demorava" pra cair depois do golpe que a derrubou. Busca
                    // limitada até o próximo TurnStart/TurnEnd/CombatEnd (fronteira de turno) —
                    // um Disarm fora desse turno nunca pertence a este hit.
                    if (defender != null)
                    {
                        CombatEvent aheadDisarm = null;
                        for (int j = _currentEventIndex + 1; j < _events.Count; j++)
                        {
                            var futureEvt = _events[j];
                            if (futureEvt.type == CombatEventType.TurnStart || futureEvt.type == CombatEventType.TurnEnd || futureEvt.type == CombatEventType.CombatEnd)
                                break;
                            if (futureEvt.type == CombatEventType.Disarm && futureEvt.targetIndex == evt.targetIndex)
                            {
                                aheadDisarm = futureEvt;
                                break;
                            }
                        }
                        if (aheadDisarm != null && !_consumedDisarms.Contains(aheadDisarm))
                        {
                            _consumedDisarms.Add(aheadDisarm);
                            StartCoroutine(PlayerCombat.DropWeapon(defender, isDisarm: true));
                        }
                    }

                    ApplyHealthDelta(evt.targetIndex, evt.newHp);

                    Vector3 popupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);

                    if (evt.isFierceBrute)
                    {
                        // Fierce Brute conectou: flash de tela, popup "×2!" diferenciado, aura
                        // do atacante destruída (fade rápido, 0.2s — o buff já cumpriu seu
                        // papel) e um ghost trail extra mais curto no instante do impacto pra
                        // reforçar o golpe, além do que já tocou na ativação.
                        DamagePopup.SpawnFierceBrute(popupPos, evt.damage, evt.isCrit);
                        StartCoroutine(FlashScreenWhite(0.15f));
                        attacker?.HideFierceBruteAura(0.2f);
                        if (attacker != null)
                            StartCoroutine(SpawnGhostTrail(attacker.transform, 0.4f * t, 0.06f * t, new Color(0.5f, 0f, 0.8f, 0.6f)));
                    }
                    else
                    {
                        DamagePopup.Spawn(popupPos, evt.damage, evt.isCrit);
                    }

                    Vector3 bloodPos = defender.transform.position + Vector3.up * 0.3f;
                    if (evt.isCrit) BloodEffectPlayer.PlayCrit(bloodPos);
                    else            BloodEffectPlayer.PlayHit(bloodPos);

                    StartCoroutine(defender.Knockback(pushDir, kbDist, kbDur * t));
                    yield return StartCoroutine(defender.animationController.PlayHurt(kbDur * t));

                    // Let the slash clip finish its second half before anything else can
                    // re-trigger it — without this, combo hits retrigger mid-clip and the
                    // animation snaps/restarts instead of playing through.
                    if (!evt.isThrow)
                    {
                        yield return new WaitForSeconds(slashHalf);
                        if (swingMult != 1f) attacker?.animationController.SetSpeed(1f);
                    }
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            // Counter (defensor bate antes do hit do atacante conectar) e Reversal (defensor
            // bate de volta depois de já ter tomado o hit) — em ambos, playerIndex é quem
            // retalia (o "attacker" deste evento) e targetIndex é o atacante original (que
            // agora toma o dano). Mesmo fluxo do Hit, só troca o popup.
            case CombatEventType.Counter:
            case CombatEventType.Reversal:
                if (defender != null)
                {
                    // Sem RepositionIfNeeded aqui: quem retalia (attacker deste evento) nunca
                    // saiu do lugar — é o atacante original (defender deste evento) que correu
                    // até ele. Counter/Reversal disparam antes de qualquer dano nesta troca
                    // (ver SimulateHit), então não há knockback prévio que o tenha deslocado.

                    float retSwingMult = SwingSpeedMultiplier(attacker);
                    float retSlashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t / retSwingMult;
                    string retTrigger = SwingTrigger(attacker);
                    if (retSwingMult != 1f) attacker?.animationController.SetSpeed(retSwingMult);
                    attacker?.GetComponent<Animator>()?.SetTrigger(retTrigger);
                    yield return new WaitForSeconds(retSlashHalf);

                    Vector2 retPushDir = ComputePushDir(attacker, defender);
                    float   retKbDist  = attacker?.settings?.knockbackDistance ?? 0.5f;
                    float   retKbDur   = attacker?.settings?.hurtDuration ?? 0.07f;

                    ApplyHealthDelta(evt.targetIndex, evt.newHp);

                    Vector3 retPopupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    if (evt.type == CombatEventType.Counter)
                    {
                        DamagePopup.SpawnCounter(retPopupPos, evt.damage, evt.isCrit);
                        // Monk: pisca a aura a cada contra-ataque (FlashMonkAura já é no-op se
                        // este personagem não tiver a skill/aura — Sixth Sense também alimenta
                        // counter e pode chegar até aqui sem nenhum efeito visual extra).
                        attacker?.FlashMonkAura();
                    }
                    else
                        DamagePopup.SpawnReversal(retPopupPos, evt.damage, evt.isCrit);

                    // Fierce Brute: aqui quem possivelmente tinha o buff é o ATACANTE ORIGINAL
                    // (targetIndex deste evento, "defender" na nomenclatura local — playerIndex/
                    // "attacker" é o retaliador) — Counter cancela o hit dele antes de conectar
                    // (consome o buff sem dobro); Reversal-após-hit já não tem nada pra destruir
                    // (o Hit anterior já resolveu o buff), e Reversal-após-bloqueio também já foi
                    // limpo pelo Block que veio antes — chamada incondicional seguindo o mesmo
                    // padrão no-op-se-não-existir do Block/Dodge acima.
                    defender?.HideFierceBruteAura();

                    StartCoroutine(defender.Knockback(retPushDir, retKbDist, retKbDur * t));
                    yield return StartCoroutine(defender.animationController.PlayHurt(retKbDur * t));

                    yield return new WaitForSeconds(retSlashHalf);
                    if (retSwingMult != 1f) attacker?.animationController.SetSpeed(1f);

                    // Os estados Slashing/SlashingDagger/SlashingHeavy só têm UMA transição de
                    // saída no Animator Controller: pro estado Jump Start (via bool JumpStart),
                    // que por sua vez só sai pro Idle quando JumpStart volta a false. Isso nunca
                    // foi um problema antes porque todo combo termina em TurnEnd, que passa por
                    // PlayJumpStart + JumpTo (volta ao spawn) — e quem retalia num Counter/
                    // Reversal nunca tem um TurnEnd próprio nesta troca. Um SetIdle(true) sozinho
                    // não adianta nada aqui: a transição de saída do Slashing nem olha pro bool
                    // Idle, só pro JumpStart. Solução: disparar o mesmo toggle de JumpStart que o
                    // TurnEnd usa, mas sem chamar movement.JumpTo() — fica parado no lugar.
                    float retJumpDur = attacker?.settings?.jumpStartDuration ?? 0.02f;
                    if (attacker != null)
                        yield return StartCoroutine(attacker.animationController.PlayJumpStart(retJumpDur * t));
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.HealthChanged:
                ApplyHealthChanged(evt);
                yield return null;
                break;

            case CombatEventType.Dodge:
                // Pets como alvo válido: esquiva do pet (PetState.evasionBase) em vez do
                // personagem — sem RepositionIfNeeded/DodgeLeap (PlayerCombat-only); usa o
                // trigger "Jumping" do próprio PetAnimationController.
                if (evt.targetIsPet)
                {
                    var dodgePet = GetPet(evt.targetIndex, evt.targetPetIndex);
                    if (dodgePet != null && attacker != null)
                    {
                        float dodgeSwingMultPet = SwingSpeedMultiplier(attacker);
                        float slashHalfPet = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t / dodgeSwingMultPet;

                        float distToPet = Vector2.Distance(attacker.transform.position, dodgePet.transform.position);
                        if (distToPet > 1.0f)
                            yield return StartCoroutine(attacker.animationController.PlayRun(CalcPetStopPosition(attacker.transform.position, dodgePet.transform.position, CharacterPetReach(dodgePet.petType)), (attacker.settings?.runSpeed ?? 35f) * t, attacker.movement));

                        string triggerPet = SwingTrigger(attacker);
                        if (dodgeSwingMultPet != 1f) attacker?.animationController.SetSpeed(dodgeSwingMultPet);
                        attacker?.GetComponent<Animator>()?.SetTrigger(triggerPet);
                        yield return new WaitForSeconds(slashHalfPet);

                        DamagePopup.SpawnDodge(dodgePet.transform.position + Vector3.up * 0.8f);
                        attacker?.HideFierceBruteAura();
                        Vector2 petDodgeDir = ((Vector2)dodgePet.transform.position - (Vector2)attacker.transform.position).normalized;
                        yield return StartCoroutine(dodgePet.DodgeLeap(petDodgeDir, t));

                        yield return new WaitForSeconds(slashHalfPet);
                        if (dodgeSwingMultPet != 1f) attacker?.animationController.SetSpeed(1f);
                    }
                    yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                    break;
                }
                if (defender != null)
                {
                    // Dodge always follows a melee swing attempt (never a throw) — the attacker
                    // swings, and exactly when it would have landed, the defender leaps away.
                    yield return StartCoroutine(RepositionIfNeeded(attacker, defender, t));

                    string trigger = SwingTrigger(attacker);
                    float dodgeSwingMult = SwingSpeedMultiplier(attacker);
                    if (dodgeSwingMult != 1f) attacker?.animationController.SetSpeed(dodgeSwingMult);
                    attacker?.GetComponent<Animator>()?.SetTrigger(trigger);

                    float slashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t / dodgeSwingMult;
                    yield return new WaitForSeconds(slashHalf);

                    Vector3 dodgePopupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnDodge(dodgePopupPos);

                    // Fierce Brute: esquivado consome o buff de qualquer forma — ver mesmo
                    // comentário no case Block.
                    attacker?.HideFierceBruteAura();
                    Vector2 dodgeDir = ComputePushDir(attacker, defender);
                    float   dodgeDist = attacker?.settings?.knockbackDistance ?? 0.5f;
                    yield return StartCoroutine(defender.DodgeLeap(dodgeDir, dodgeDist));

                    yield return new WaitForSeconds(slashHalf);
                    if (dodgeSwingMult != 1f) attacker?.animationController.SetSpeed(1f);
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.Block:
                if (defender != null)
                {
                    string trigger = SwingTrigger(attacker);
                    float blockSwingMult = SwingSpeedMultiplier(attacker);
                    float slashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t / blockSwingMult;

                    // Same reasoning as Dodge: sync the attacker's swing with the moment of impact.
                    yield return StartCoroutine(RepositionIfNeeded(attacker, defender, t));
                    if (blockSwingMult != 1f) attacker?.animationController.SetSpeed(blockSwingMult);
                    attacker?.GetComponent<Animator>()?.SetTrigger(trigger);
                    yield return new WaitForSeconds(slashHalf);

                    Vector3 blockPopupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnBlock(blockPopupPos);

                    // Fierce Brute: bloqueado/esquivado/contra-atacado consome o buff de
                    // qualquer forma (sem dobro, sem flash) — destrói a aura roxa do atacante se
                    // ela existir (no-op se não); chamada incondicional é segura, HideFierceBruteAura
                    // já checa null por conta própria.
                    attacker?.HideFierceBruteAura();

                    float kbDist = (attacker?.settings?.knockbackDistance ?? 0.5f) * 0.5f;
                    float kbDur  = (attacker?.settings?.hurtDuration ?? 0.07f) * t;
                    Vector2 blockDir = ComputePushDir(attacker, defender);

                    // Block animation and knockback in parallel
                    StartCoroutine(defender.Knockback(blockDir, kbDist, kbDur));
                    yield return StartCoroutine(defender.animationController.PlayBlock(0.36666667f * t));

                    yield return new WaitForSeconds(slashHalf);
                    if (blockSwingMult != 1f) attacker?.animationController.SetSpeed(1f);
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.Miss:
                // Pets como alvo válido: arremesso errado contra um pet — popup + Jumping (pet
                // não tem DodgeLeap, é um personagem-only; reusa o trigger de esquiva do próprio
                // PetAnimationController).
                if (evt.targetIsPet)
                {
                    var missPet = GetPet(evt.targetIndex, evt.targetPetIndex);
                    if (missPet != null)
                    {
                        DamagePopup.SpawnMiss(missPet.transform.position + Vector3.up * 0.8f);
                        Vector2 missDodgeDir = attacker != null
                            ? ((Vector2)missPet.transform.position - (Vector2)attacker.transform.position).normalized
                            : Vector2.right;
                        StartCoroutine(missPet.DodgeLeap(missDodgeDir, t));
                    }
                    yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                    break;
                }
                if (defender != null)
                {
                    Vector3 missPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnMiss(missPos);
                    Vector2 missDir = ComputePushDir(attacker, defender);
                    float missDist = attacker?.settings?.knockbackDistance ?? 0.5f;
                    yield return StartCoroutine(defender.DodgeLeap(missDir, missDist));
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.Repulse:
            {
                // attacker = deflector (evt.playerIndex), defender = lançador original que toma o impacto (evt.targetIndex)
                float repSlashMult = SwingSpeedMultiplier(attacker);
                float repSlashHalf = (attacker?.settings?.slashingDuration ?? 0.4f) * 0.5f * t / repSlashMult;
                float repJumpDur   = (attacker?.settings?.jumpStartDuration ?? 0.02f) * t;

                // 1. Slashing no deflector (rebate a arma de volta)
                if (repSlashMult != 1f) attacker?.animationController.SetSpeed(repSlashMult);
                attacker?.animationController.SetIdle(false);
                attacker?.GetComponent<Animator>()?.SetTrigger(SwingTrigger(attacker));
                yield return new WaitForSeconds(repSlashHalf);

                // 2. Arma voa de volta para o lançador original
                var repWeaponData = _lastThrownWeaponData;
                if (repWeaponData?.inHandSprite != null && attacker != null && defender != null)
                {
                    Vector3 repLaunchPos = attacker.weaponHandler?.handBone?.position
                        ?? attacker.transform.position + Vector3.up * 0.5f;
                    Vector3 repTargetPos  = defender.transform.position;
                    Vector3 repFlightDir  = (repTargetPos - repLaunchPos).normalized;

                    var repFlyGo = new GameObject("RepulseWeapon");
                    repFlyGo.transform.position = repLaunchPos;
                    repFlyGo.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(repFlightDir.y, repFlightDir.x) * Mathf.Rad2Deg);
                    Vector3 repHandLossy = attacker.weaponHandler?.handBone?.lossyScale ?? Vector3.one * 0.3f;
                    repFlyGo.transform.localScale = new Vector3(
                        Mathf.Abs(repHandLossy.x) * repWeaponData.scale,
                        Mathf.Abs(repHandLossy.y) * repWeaponData.scale,
                        Mathf.Abs(repHandLossy.z) * repWeaponData.scale);
                    var repSr = repFlyGo.AddComponent<SpriteRenderer>();
                    repSr.sprite           = repWeaponData.inHandSprite;
                    repSr.sortingLayerName = "Weapons";
                    repSr.sortingOrder     = 10;

                    bool repRotate = WeaponData.HasType(repWeaponData, WeaponType.Thrown);
                    yield return StartCoroutine(attacker.FlyWeapon(repFlyGo.transform, repLaunchPos, repTargetPos, 0.45f * t, repRotate, repRotate ? 0.5f : 0f));
                    Destroy(repFlyGo);
                }
                else
                {
                    yield return new WaitForSeconds(0.45f * t);
                }

                // 3. Impacto no lançador original: popup + dano + hurt + knockback
                if (defender != null)
                {
                    Vector3 repPopupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnRepulse(repPopupPos, evt.damage, evt.isCrit);
                    ApplyHealthDelta(evt.targetIndex, evt.newHp);
                    Vector2 repPushDir = ComputePushDir(attacker, defender);
                    float   repKbDist  = attacker?.settings?.knockbackDistance ?? 0.5f;
                    float   repKbDur   = (attacker?.settings?.hurtDuration ?? 0.07f) * t;
                    StartCoroutine(defender.Knockback(repPushDir, repKbDist, repKbDur));
                    yield return StartCoroutine(defender.animationController.PlayHurt(repKbDur));
                }

                yield return new WaitForSeconds(repSlashHalf);
                if (repSlashMult != 1f) attacker?.animationController.SetSpeed(1f);

                // Sair do estado Slashing — mesmo padrão de Counter/Reversal (PlayJumpStart sem JumpTo)
                if (attacker != null)
                    yield return StartCoroutine(attacker.animationController.PlayJumpStart(repJumpDur));

                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;
            }

            case CombatEventType.Disarm:
                // Bug 1 (weapon drop): se o case Hit já disparou esta queda antecipadamente no
                // instante do impacto (ver lookahead lá), só consome o evento aqui — sem isso a
                // arma cairia (e o popup apareceria) uma segunda vez.
                if (_consumedDisarms.Remove(evt))
                {
                    yield return null;
                    break;
                }
                // PlayerCombat.DropWeapon já cobre popup + UnequipPermanent + a queda em
                // pêndulo amortecido até o chão (ver Drop de Arma no CLAUDE.md) — antes este
                // case só tirava a arma e mostrava o popup, sem nenhum visual de queda.
                if (defender != null)
                    StartCoroutine(PlayerCombat.DropWeapon(defender, isDisarm: true));
                yield return null;
                break;

            case CombatEventType.WeaponDrop:
                if (attacker != null)
                    StartCoroutine(PlayerCombat.DropWeapon(attacker, isDisarm: false));
                yield return null;
                break;

            case CombatEventType.ShieldDisarm:
                // PlayerCombat.DropShield cobre popup + RemoveShield + a mesma queda em pêndulo
                // amortecido de DropWeapon (ver CLAUDE.md, Desarmar do Escudo) — ao tomar hit,
                // popup "DISARM!" (mesma convenção de Disarm de arma).
                if (defender != null)
                    StartCoroutine(PlayerCombat.DropShield(defender, isDisarm: true));
                yield return null;
                break;

            case CombatEventType.ShieldDrop:
                // Espelha WeaponDrop: escudo cai pelo próprio impacto de um bloqueio bem-sucedido,
                // não por um desarme ativo do atacante — popup "DROP!" em vez de "DISARM!".
                if (attacker != null)
                    StartCoroutine(PlayerCombat.DropShield(attacker, isDisarm: false));
                yield return null;
                break;

            case CombatEventType.ThrowWeapon:
                if (attacker != null)
                {
                    // Fierce Brute: escopado só a melee (CombatSimulator.SimulateHit) — se o
                    // turno virou arremesso em vez disso, o buff já foi zerado lá sem efeito;
                    // destrói a aura aqui também, senão ficaria órfã (no-op se não existir).
                    attacker.HideFierceBruteAura();

                    // Capture sprite/position/scale before Unequip destroys the in-hand weapon object.
                    var weaponData   = attacker.weaponHandler.CurrentWeaponData;
                    _lastThrownWeaponData = weaponData; // lido pelo case Repulse que segue imediatamente
                    var inHandWeapon = attacker.weaponHandler.CurrentWeapon;
                    Vector3 launchPos = attacker.weaponHandler.handBone != null
                        ? attacker.weaponHandler.handBone.position
                        : (inHandWeapon != null ? inHandWeapon.transform.position : attacker.transform.position + Vector3.up * 0.5f);
                    Vector3 projectileScale = inHandWeapon != null
                        ? inHandWeapon.transform.lossyScale
                        : (attacker.weaponHandler.handBone != null ? attacker.weaponHandler.handBone.lossyScale : Vector3.one) * (weaponData?.scale ?? 1f);

                    // Mirrors CombatSimulator.SimulateThrow: a arma some da mão (Unequip), mas
                    // Hideaway faz ela ficar no loadout igual a uma arma Thrown normal (pode ser
                    // pega de novo num pickup futuro) mesmo sem ter a tag. Sem a skill e sem a
                    // tag Thrown, sai do loadout pra sempre (UnequipPermanent — some do WeaponHUD).
                    if (weaponData != null && !WeaponData.HasType(weaponData, WeaponType.Thrown) && !attacker.HasSkill("Hideaway"))
                        attacker.weaponHandler.UnequipPermanent();
                    else
                        attacker.weaponHandler.Unequip();
                    // Idle=false antes do trigger (mesmo padrão de PlayCatchWeapon) — garante que
                    // "Any State → Throwing" dispara mesmo que o frame anterior já tivesse
                    // deixado Idle=true de um throw anterior neste mesmo turno.
                    attacker.animationController.SetIdle(false);
                    attacker.GetComponent<Animator>()?.SetTrigger("Throwing");

                    if (weaponData?.inHandSprite != null)
                    {
                        // Pets como alvo válido (Ajuste 1): o próprio evento ThrowWeapon NUNCA
                        // carrega targetIsPet/targetPetIndex (CombatSimulator.SimulateThrow só os
                        // seta no Hit/Miss seguinte, ver CombatEvent.cs) — sem essa checagem a
                        // arma sempre mirava no personagem dono (evt.targetIndex), "passando" do
                        // pet de verdade (bug reportado pelo usuário: arma "mirada no player1" em
                        // vez do pet sorteado pelo simulador). FindThrowTargetPet espia o Hit/Miss
                        // imediatamente seguinte na lista (sempre o par desta mesma jogada).
                        var throwTargetPet = FindThrowTargetPet(evt);
                        Vector3 targetPos = throwTargetPet != null
                            ? throwTargetPet.transform.position
                            : defender != null
                                ? defender.transform.position
                                : attacker.transform.position + (attacker.isPlayer1 ? Vector3.right : Vector3.left) * 5f;

                        Vector3 flightDir   = (targetPos - launchPos).normalized;
                        float   flightAngle = Mathf.Atan2(flightDir.y, flightDir.x) * Mathf.Rad2Deg;

                        var flyingWeapon = new GameObject("FlyingWeapon");
                        flyingWeapon.transform.position   = launchPos;
                        flyingWeapon.transform.localScale = new Vector3(
                            Mathf.Abs(projectileScale.x), Mathf.Abs(projectileScale.y), Mathf.Abs(projectileScale.z));
                        flyingWeapon.transform.rotation = Quaternion.Euler(0, 0, flightAngle);
                        var sr = flyingWeapon.AddComponent<SpriteRenderer>();
                        sr.sprite           = weaponData.inHandSprite;
                        sr.sortingLayerName = "Weapons";
                        sr.sortingOrder     = 10;

                        bool  rotate = WeaponData.HasType(weaponData, WeaponType.Thrown);
                        float arc    = rotate ? 0.5f : 0f;
                        yield return StartCoroutine(attacker.FlyWeapon(flyingWeapon.transform, launchPos, targetPos, 0.45f * t, rotate, arc));
                        Destroy(flyingWeapon);
                    }
                    else
                    {
                        yield return new WaitForSeconds(0.45f * t);
                    }

                    // Sai do estado Throwing assim que o arremesso termina — a transição
                    // Throwing → Idle exige Idle=true e CanTransitionToSelf=0 (não reentra em si
                    // mesma). Mantido por segurança mesmo agora que Hideaway voltou a ser um
                    // throw probabilístico só (sem combo de arremesso, no máximo 1 ThrowWeapon
                    // por turno) — era essencial enquanto existiu um combo de throws no mesmo
                    // turno (bug real reportado pelo usuário, "parece que está com parkinson").
                    attacker.animationController.SetIdle(true);
                }
                break;

            case CombatEventType.SpeedBonus:
                for (int i = 0; i < evt.extraActions; i++)
                    DamagePopup.SpawnRapido((attacker?.transform.position ?? Vector3.zero) + Vector3.up * 2f);
                yield return null;
                break;

            case CombatEventType.Stunned:
                // Chaining: 3º hit consecutivo do streak — liga a label persistente + pose de
                // atordoado no alvo (defender, quem ficou estunado), e mostra um popup
                // "ESTUNADO!" instantâneo (sobe e desaparece, igual a um Disarm/Sabotage) pra
                // marcar o momento exato em que o stun foi recebido. Fire-and-forget: o resto
                // do turno do atacante (mais combo, TurnEnd) continua normalmente em paralelo.
                defender?.ShowStunLabel();
                if (defender != null)
                    DamagePopup.SpawnStun(defender.transform.position + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f));
                yield return null;
                break;

            case CombatEventType.StunSkip:
                // Ação consumida pelo stun — sem Thief/pickup/throw/melee neste turno. Desliga
                // a label/Hurt (volta ao normal) e segue pro TurnEnd como um turno qualquer.
                attacker?.HideStunLabel();
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.TurnEnd:
                // Return attacker to spawn (jump-back) — mirrors ReturnToSpawn.
                // RandomSpawnPos sorteia um ponto NOVO a cada chamada (não a posição original
                // do personagem) — só faz sentido pular pra lá se o atacante de fato saiu da
                // própria zona de spawn neste turno (correu até o adversário). Monk corre e
                // ataca normalmente agora (não guarda mais — ver CombatSimulator.ApplySkillStats),
                // então cai no mesmo caminho de qualquer outro personagem, sem guard especial.
                //
                // Bug 3 (morte por Counter/Reversal): CombatSimulator.SimulateTurn emite este
                // TurnEnd incondicionalmente pro atacante, mesmo quando o Counter/Reversal do
                // defensor (SimulateRetaliation, disparado dentro do próprio Hit deste turno) já
                // matou o atacante antes deste ponto. Sem o guard `!attacker.IsDead`, o
                // personagem já morto ainda pulava de volta pro spawn antes do CombatEnd tocar
                // Dying — teleportando o cadáver pra longe de onde ele de fato morreu.
                if (attacker != null && !attacker.IsDead && !InSpawnZone(attacker.transform.position, attacker.isPlayer1))
                {
                    float jsDur = attacker.settings?.jumpStartDuration ?? 0.02f;
                    float jh    = attacker.settings?.jumpHeight ?? 2f;
                    float spd   = (attacker.settings?.runSpeed ?? 35f) * t;
                    Vector2 spawnPos = RandomSpawnPos(attacker.isPlayer1);
                    yield return StartCoroutine(attacker.animationController.PlayJumpStart(jsDur * t));
                    yield return StartCoroutine(attacker.movement.JumpTo(spawnPos, spd, jh));
                }
                attacker?.animationController.SetIdle(true);
                // Bug 2 (sorting): desfaz a promoção de TurnStart — mirrors PlayerCombat.
                // AttackRoutine's RestoreDefaultLayers() at the end of the legacy turn.
                attacker?.RestoreDefaultLayers();
                break;

            case CombatEventType.CombatEnd:
                yield return StartCoroutine(TriggerCombatEndRoutine(evt));
                break;

            // --- Pets (Fase 3) ---

            case CombatEventType.PetTurnStart:
                GetPet(evt.playerIndex, evt.petIndex)?.animController.SetIdle(true);
                yield return null;
                break;

            case CombatEventType.PetAttack:
            {
                var pet = GetPet(evt.playerIndex, evt.petIndex);
                if (pet == null) { yield return null; break; }

                // Mesma velocidade de corrida do personagem dono (settings.runSpeed, escalada
                // por t igual ao jump-back de TurnEnd) — era um valor fixo de 6f (default do
                // parâmetro), bem mais lento que os ~35 dos personagens, pedido pelo usuário
                // pra equalizar.
                float petRunSpeed = (GetCombat(evt.playerIndex)?.settings?.runSpeed ?? 35f) * t;

                // Duração do swing varia por tipo de pet (PetState.SlashDuration — Boar 0.85s,
                // ajustado a pedido do usuário; Monkey/Mouse mantêm o default 0.4s). comboDelay
                // de PlayAttackSequence é metade da duração total (pré-impacto + pós-impacto,
                // mesmo papel de slashHalf nos personagens).
                float petComboDelay = PetState.SlashDuration(pet.petType) * 0.5f * t;

                if (evt.shieldIntercept)
                {
                    // Rato intercepta um hit de Fierce Brute do PRÓPRIO dono — aqui não é o pet
                    // atacando, é o pet sendo atingido no lugar do personagem (ver
                    // CombatSimulator.SimulateHit) — sem correr/atacar, só reage.
                    pet.animController.PlayHurt();
                    pet.healthBar?.UpdateBar(evt.newTargetHp, pet.maxHp);
                    DamagePopup.Spawn(pet.transform.position + Vector3.up * 0.8f, evt.damage, isCrit: false);
                    if (evt.newTargetHp <= 0) pet.PlayDeath();
                    yield return new WaitForSeconds(0.2f * t);
                    break;
                }

                _petImpactPet             = pet;
                _petImpactEvt             = evt;
                _petImpactTargetPet       = null;
                _petImpactTargetCharacter = null;
                _petImpactTargetPos       = pet.transform.position;
                Vector3 petRunPos         = pet.transform.position;

                if (evt.targetIsPet)
                {
                    _petImpactTargetPet = GetPet(evt.targetIndex, evt.targetPetIndex);
                    if (_petImpactTargetPet != null)
                    {
                        _petImpactTargetPos = _petImpactTargetPet.transform.position;
                        petRunPos = CalcPetStopPosition(pet.transform.position, _petImpactTargetPet.transform.position, PetPetReach(pet.petType, _petImpactTargetPet.petType));
                    }
                }
                else
                {
                    _petImpactTargetCharacter = GetCombat(evt.targetIndex);
                    if (_petImpactTargetCharacter != null)
                    {
                        _petImpactTargetPos = _petImpactTargetCharacter.transform.position;
                        petRunPos = CalcPetStopPosition(pet.transform.position, _petImpactTargetCharacter.transform.position, CharacterPetReach(pet.petType));
                        petRunPos = ApplyPetOffset(petRunPos, pet.transform.position, _petImpactTargetCharacter.transform.position, PetAttacksCharacterOffset, pet.petType);
                    }
                }

                pet.spawnPosition = RollPetSpawnPosition(pet);
                yield return StartCoroutine(pet.PlayAttackSequence(petRunPos, evt.isDodged, evt.damage, _onPetImpact, runSpeed: petRunSpeed, comboDelay: petComboDelay));
                break;
            }

            case CombatEventType.PetNetSkip:
                // Pet enredado (permanente) ou caído — sem nada visível pra tocar, só ocupa o
                // tempo de um "turno" curto antes do PetTurnEnd seguinte.
                yield return new WaitForSeconds(0.1f * t);
                break;

            case CombatEventType.PetDeath:
            {
                var deadPet = GetPet(evt.playerIndex, evt.petIndex);
                if (deadPet != null)
                {
                    deadPet.PlayDeath();
                    if (deadPet.shieldVisual != null) { Destroy(deadPet.shieldVisual); deadPet.shieldVisual = null; }
                }
                yield return null;
                break;
            }

            case CombatEventType.PetDisarm:
            {
                var disarmedCharacter = GetCombat(evt.targetIndex);
                if (disarmedCharacter != null)
                    StartCoroutine(PlayerCombat.DropWeapon(disarmedCharacter, isDisarm: true));
                yield return null;
                break;
            }

            case CombatEventType.PetTurnEnd:
                yield return null;
                break;

            case CombatEventType.CryOfTheDamned:
            {
                GetSkillHUD(evt.playerIndex)?.UseSkill("Cry of the Damned");
                var crier = GetCombat(evt.playerIndex);
                if (crier != null)
                    yield return StartCoroutine(PlayCryOfTheDamned(crier, t));
                else
                    yield return null;
                break;
            }

            case CombatEventType.PetFlee:
            {
                var fleeingPet = GetPet(evt.playerIndex, evt.petIndex);
                if (fleeingPet != null)
                    yield return StartCoroutine(PlayPetFlee(fleeingPet, t));
                break;
            }

            case CombatEventType.Hypnosis:
            {
                GetSkillHUD(evt.playerIndex)?.UseSkill("Hypnosis");
                var hypnotizer = GetCombat(evt.playerIndex);
                if (hypnotizer != null)
                    yield return StartCoroutine(PlayHypnosisScreenEffect(hypnotizer, t));
                else
                    yield return null;
                break;
            }

            case CombatEventType.PetHypnotized:
            {
                var ownerPets    = evt.playerIndex == 0 ? p1Pets : p2Pets;
                var newOwnerPets = evt.targetIndex == 0 ? p1Pets : p2Pets;
                PetCombatController hypnotizedPet = null;
                if (evt.petIndex >= 0 && evt.petIndex < ownerPets.Count)
                {
                    hypnotizedPet = ownerPets[evt.petIndex];
                    ownerPets.RemoveAt(evt.petIndex);
                    newOwnerPets.Add(hypnotizedPet);
                }
                if (hypnotizedPet != null)
                {
                    // Atualiza ownership visual do pet: RollPetSpawnPosition usa isPlayer1 para
                    // decidir o lado do spawn; sem isso o pet continuaria aparecendo no campo do
                    // dono antigo após hipnose, atacando o alvo certo mas do lugar errado.
                    bool newIsP1 = evt.targetIndex == 0;
                    hypnotizedPet.isPlayer1 = newIsP1;
                    hypnotizedPet.SetInitialFacing(newIsP1 ? 1f : -1f);

                    var newOwner = GetCombat(evt.targetIndex);
                    yield return StartCoroutine(PlayPetHypnotized(hypnotizedPet, newOwner, t));
                }
                else
                    yield return null;
                break;
            }

            case CombatEventType.TreatFeed:
            {
                GetSkillHUD(evt.playerIndex)?.UseSkill("Treat");
                var feeder = GetCombat(evt.playerIndex);
                var fedPet = GetPet(evt.playerIndex, evt.petIndex);

                if (feeder != null && fedPet != null)
                {
                    float feedSpeed = (feeder.settings?.runSpeed ?? 35f) * t;
                    Vector3 feedPos = CalcPetStopPosition(feeder.transform.position, fedPet.transform.position, CharacterPetReach(fedPet.petType));
                    yield return StartCoroutine(feeder.animationController.PlayRun(feedPos, feedSpeed, feeder.movement));

                    // Arremessa o petisco em arco parabólico até o pet
                    float tossTime = 0.4f * t;
                    if (treatSprite != null)
                    {
                        var treatGO = new GameObject("TreatItem");
                        var sr = treatGO.AddComponent<SpriteRenderer>();
                        sr.sprite           = treatSprite;
                        sr.sortingLayerName = "Weapons";
                        treatGO.transform.localScale = Vector3.one * 0.25f;
                        Vector3 tossStart = feeder.transform.position + Vector3.up * 0.4f;
                        Vector3 tossEnd   = fedPet.transform.position + Vector3.up * 0.4f;
                        treatGO.transform.position = tossStart;

                        float elapsed = 0f;
                        while (elapsed < tossTime)
                        {
                            elapsed += Time.deltaTime;
                            float p = Mathf.Clamp01(elapsed / tossTime);
                            treatGO.transform.position = Vector3.Lerp(tossStart, tossEnd, p) + Vector3.up * (Mathf.Sin(p * Mathf.PI) * 0.7f);
                            yield return null;
                        }
                        Destroy(treatGO);
                    }
                    else
                    {
                        yield return new WaitForSeconds(tossTime);
                    }

                    // Cura HP do pet + barra de vida + popup
                    fedPet.healthBar?.UpdateBar(evt.newTargetHp, fedPet.maxHp);
                    DamagePopup.SpawnHeal(fedPet.transform.position + Vector3.up * 1f, evt.healAmount);

                    // Se o pet estava enredado, disipa a rede (scale-up + fragmentos radiais)
                    yield return StartCoroutine(PlayNetBreakEffect(fedPet, t));

                    // Escudo visual dourado ao redor do pet
                    if (fedPet.shieldVisual != null) Destroy(fedPet.shieldVisual);
                    fedPet.shieldVisual = CreatePetShieldVisual(fedPet);

                    yield return new WaitForSeconds(0.25f * t);
                }
                yield return new WaitForSeconds((feeder?.settings?.comboDelay ?? 0.15f) * t);
                break;
            }

            case CombatEventType.TamerEat:
            {
                GetSkillHUD(evt.playerIndex)?.UseSkill("Tamer");
                var tamer   = GetCombat(evt.playerIndex);
                var carcass = GetPet(evt.targetIndex, evt.petIndex);

                if (tamer != null && carcass != null)
                {
                    // Corre até a carcaça (mesma lógica de distância do caso Hit→pet)
                    float eatSpeed = (tamer.settings?.runSpeed ?? 35f) * t;
                    Vector3 eatPos = CalcPetStopPosition(tamer.transform.position, carcass.transform.position, CharacterPetReach(carcass.petType));
                    yield return StartCoroutine(tamer.animationController.PlayRun(eatPos, eatSpeed, tamer.movement));

                    // Swing rápido simulando o Tamer comendo a carcaça
                    float slashHalf = (tamer.settings?.slashingDuration ?? 0.5f) * 0.25f * t;
                    string eatTrigger = SwingTrigger(tamer);
                    tamer.GetComponent<Animator>()?.SetTrigger(eatTrigger);
                    yield return new WaitForSeconds(slashHalf);

                    // Cura e popup verde
                    ApplyHealthDelta(evt.playerIndex, evt.newHp);
                    DamagePopup.SpawnHeal(tamer.transform.position + Vector3.up * 1.5f, evt.healAmount);

                    // Carcaça consumida — destroi o GameObject
                    if (carcass.healthBar != null) carcass.healthBar.FadeOutAndDestroy(0.2f);
                    Destroy(carcass.gameObject);

                    yield return new WaitForSeconds(slashHalf);

                    // Volta ao spawn antes de continuar o turno (RunToDefender + ataque normal)
                    // — mesmo padrão do TurnEnd: só pula se estiver fora da zona de spawn.
                    if (!InSpawnZone(tamer.transform.position, tamer.isPlayer1))
                    {
                        float jsDur = tamer.settings?.jumpStartDuration ?? 0.02f;
                        float jh    = tamer.settings?.jumpHeight ?? 2f;
                        float spd   = (tamer.settings?.runSpeed ?? 35f) * t;
                        Vector2 spawnPos = RandomSpawnPos(tamer.isPlayer1);
                        yield return StartCoroutine(tamer.animationController.PlayJumpStart(jsDur * t));
                        yield return StartCoroutine(tamer.movement.JumpTo(spawnPos, spd, jh));
                    }
                    tamer.animationController.SetIdle(true);
                }
                yield return new WaitForSeconds((tamer?.settings?.comboDelay ?? 0.15f) * t);
                break;
            }

            case CombatEventType.Mimic:
            {
                // Mostra popup MIMIC! no usuário; os eventos da skill copiada seguem imediatamente
                // na lista e são tratados pelos seus próprios cases existentes.
                GetSkillHUD(evt.playerIndex)?.UseSkill("Mimic");
                if (attacker != null)
                {
                    Vector3 mimicPos = attacker.transform.position + Vector3.up * 2f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnMimic(mimicPos, evt.weaponName);
                }
                yield return new WaitForSeconds(0.4f * t);
                break;
            }

            default:
                yield return null;
                break;
        }
    }

    // --- Helpers ---

    private PlayerCombat GetCombat(int index) => index == 0 ? p1Combat : p2Combat;
    private SkillsHUD    GetSkillHUD(int index) => index == 0 ? p1SkillsHUD : p2SkillsHUD;
    private WeaponHUD GetWeaponHUD(int index) => index == 0 ? p1WeaponHUD : p2WeaponHUD;

    // Pets (Fase 3) — ownerIndex = índice do DONO (0/1, mesma convenção de evt.playerIndex/
    // evt.targetIndex), petIndex = posição na lista PlayerState.pets/p1Pets-p2Pets dele.
    private PetCombatController GetPet(int ownerIndex, int petIndex)
    {
        var list = ownerIndex == 0 ? p1Pets : p2Pets;
        if (list == null || petIndex < 0 || petIndex >= list.Count) return null;
        var pet = list[petIndex];
        // Unity's operator== retorna true para objetos destruídos (fake-null) — garante que
        // callers que usam ?. (C# null-conditional, não Unity-aware) não acessem animadores
        // de pets cujo GameObject foi destruído (ex: carcaça comida pelo Tamer).
        return pet != null ? pet : null;
    }

    // Pets como alvo válido (Ajuste 1) — versão simplificada de Haste/Piledriver/Vampirism
    // quando CombatSimulator redireciona o alvo do turno pra um pet em vez do personagem: corre
    // até o pet, resolve (callback onResolve aplica dano/cura/popup/Hurt/morte), e volta pro
    // lugar original. Sem a coreografia cheia de cada Super (sair da tela, erguer no ar, montar
    // nas costas) — não faz sentido fisicamente contra um pet pequeno, e o caso é raro o
    // bastante (precisa do roll de alvo E do roll da própria Super) pra não justificar duplicar
    // a coreografia inteira de cada uma só pra esse alvo.
    private IEnumerator PlayPetTargetedSuper(PlayerCombat attacker, PetCombatController targetPet, float t, System.Action onResolve)
    {
        if (attacker == null || targetPet == null) yield break;

        Vector3 startPos = attacker.transform.position;
        float   runSpeed = (attacker.settings?.runSpeed ?? 35f) * t;

        yield return StartCoroutine(attacker.animationController.PlayRun(CalcPetStopPosition(attacker.transform.position, targetPet.transform.position, CharacterPetReach(targetPet.petType)), runSpeed, attacker.movement));

        onResolve?.Invoke();
        yield return new WaitForSeconds(0.15f * t);

        yield return StartCoroutine(attacker.animationController.PlayRun(startPos, runSpeed, attacker.movement));
        attacker.animationController.SetIdle(true);
    }

    // Cria o glow dourado de escudo do Treat ao redor do pet. O chamador é responsável por
    // guardar a referência em pet.shieldVisual e destruí-la quando o escudo absorver um hit.
    private GameObject CreatePetShieldVisual(PetCombatController pet)
    {
        var go = new GameObject("PetShieldVisual");
        go.transform.position = pet.transform.position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite           = PlayerCombat.GetGlowSprite();
        sr.color            = new Color(1f, 0.85f, 0.1f, 0.55f); // dourado, semi-transparente
        sr.sortingLayerName = "Characters";
        sr.sortingOrder     = 5;
        float baseScale = 3.5f * PetState.Scale(pet.petType);
        go.transform.localScale = Vector3.one * baseScale;
        StartCoroutine(PulseAndFollowPet(go, pet, baseScale));
        return go;
    }

    private IEnumerator PulseAndFollowPet(GameObject go, PetCombatController pet, float baseScale)
    {
        float elapsed = 0f;
        while (go != null && pet != null)
        {
            go.transform.position   = pet.transform.position;
            go.transform.localScale = Vector3.one * (baseScale + Mathf.Sin(elapsed * 4f) * 0.15f);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // Callback de impacto do case PetAttack — lê só dos campos _petImpact* (setados ali,
    // imediatamente antes de StartCoroutine(PlayAttackSequence(...))), nunca de uma closure, pra
    // _onPetImpact poder ser cacheado 1x (ver campo acima) em vez de uma lambda nova por ataque.
    private void HandlePetImpact()
    {
        var evt             = _petImpactEvt;
        var pet             = _petImpactPet;
        var targetPetCombat = _petImpactTargetPet;
        var targetCharacter = _petImpactTargetCharacter;
        var targetPos       = _petImpactTargetPos;

        if (evt.isDodged)
        {
            if (targetPetCombat != null)
            {
                Vector2 petPushDir = ((Vector2)targetPetCombat.transform.position - (Vector2)pet.transform.position).normalized;
                StartCoroutine(targetPetCombat.DodgeLeap(petPushDir, 1f / _playbackSpeed));
            }
            else if (targetCharacter != null)
            {
                Vector2 pushDir = ((Vector2)targetCharacter.transform.position - (Vector2)pet.transform.position).normalized;
                StartCoroutine(targetCharacter.DodgeLeap(pushDir, targetCharacter.settings?.knockbackDistance ?? 0.5f));
            }
            DamagePopup.SpawnDodge(targetPos + Vector3.up * 0.8f);
        }
        else if (targetPetCombat != null)
        {
            if (evt.petShieldAbsorb)
            {
                // Escudo do Treat absorveu o ataque do pet — sem dano ao alvo.
                if (targetPetCombat.shieldVisual != null) { Destroy(targetPetCombat.shieldVisual); targetPetCombat.shieldVisual = null; }
                DamagePopup.SpawnBlock(targetPetCombat.transform.position + Vector3.up * 0.8f);
            }
            else
            {
                targetPetCombat.healthBar?.UpdateBar(evt.newTargetHp, targetPetCombat.maxHp);
                DamagePopup.Spawn(targetPetCombat.transform.position + Vector3.up * 0.8f, evt.damage, evt.isCrit);
                targetPetCombat.animController.PlayHurt();
                if (evt.newTargetHp <= 0) targetPetCombat.PlayDeath();
            }
        }
        else if (targetCharacter != null)
        {
            ApplyHealthDelta(evt.targetIndex, evt.newHp);
            DamagePopup.Spawn(targetCharacter.transform.position + Vector3.up * 1.5f, evt.damage, evt.isCrit);
            // Knockback pequeno (0.3f, metade do normal) — valor fixo pedido na spec.
            Vector2 pushDir = ((Vector2)targetCharacter.transform.position - (Vector2)pet.transform.position).normalized;
            StartCoroutine(targetCharacter.Knockback(pushDir, 0.3f, targetCharacter.settings?.hurtDuration ?? 0.07f));
            StartCoroutine(targetCharacter.animationController.PlayHurt(targetCharacter.settings?.hurtDuration ?? 0.07f));
        }
    }

    // --- Cry of the Damned ---

    private IEnumerator PlayCryOfTheDamned(PlayerCombat crier, float t)
    {
        var animator     = crier.GetComponent<Animator>();
        float savedSpeed = animator != null ? animator.speed : 1f;
        if (animator != null) animator.speed = 0f;

        float   forwardSign = Mathf.Sign(crier.transform.localScale.x);
        Vector3 mouthPos    = crier.transform.position
                            + new Vector3(0.38f * forwardSign, 0.38f, 0f);

        // Direção do vento: aponta para o pet inimigo mais próximo vivo; fallback = frente.
        var enemyPets = crier == p1Combat ? p2Pets : p1Pets;
        Vector2 windDir  = new Vector2(forwardSign, 0f);
        float   distToPet = 8f; // fallback razoável (largura da arena)
        foreach (var ep in enemyPets)
        {
            if (ep != null)
            {
                Vector2 toPet = (Vector2)ep.transform.position - (Vector2)mouthPos;
                distToPet = toPet.magnitude;
                windDir   = toPet / distToPet;
                break;
            }
        }

        var glow = PlayerCombat.GetGlowSprite();

        // 3 rajadas curtas em sequência; partículas continuam voando em background
        for (int wave = 0; wave < 3; wave++)
        {
            int count = UnityEngine.Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                StartCoroutine(SpawnWindParticle(mouthPos, windDir, glow, distToPet));
                yield return new WaitForSeconds(0.045f * t);
            }
            if (wave < 2)
                yield return new WaitForSeconds(0.13f * t);
        }

        yield return new WaitForSeconds(0.30f * t);

        if (animator != null) animator.speed = savedSpeed;
    }

    // Partícula de vento alongada na direção do alvo. Life calculado para chegar até o pet.
    private IEnumerator SpawnWindParticle(Vector3 origin, Vector2 windDir, Sprite glow, float targetDist)
    {
        var go = new GameObject("WindPuff");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite           = glow;
        sr.sortingLayerName = "Characters";
        sr.sortingOrder     = 28;

        sr.color = new Color(
            UnityEngine.Random.Range(0.75f, 1.00f),
            UnityEngine.Random.Range(0.88f, 1.00f),
            1.0f,
            UnityEngine.Random.Range(0.55f, 0.82f));
        Color startColor = sr.color;

        Vector2 perp    = new Vector2(-windDir.y, windDir.x);
        float   speed   = UnityEngine.Random.Range(4.0f, 7.0f);
        // Life calculado para que a partícula percorra targetDist ± 10% — alcança o pet.
        float   life    = (targetDist / speed) * UnityEngine.Random.Range(0.90f, 1.10f);
        float   waveFreq = UnityEngine.Random.Range(4f, 9f);
        float   waveAmp  = UnityEngine.Random.Range(0.04f, 0.14f);
        float   scaleW   = UnityEngine.Random.Range(0.18f, 0.30f);
        float   elapsed  = 0f;

        go.transform.position = origin + (Vector3)(perp * UnityEngine.Random.Range(-0.18f, 0.18f))
                                       + new Vector3(0f, UnityEngine.Random.Range(-0.06f, 0.06f), 0f);
        float angle = Mathf.Atan2(windDir.y, windDir.x) * Mathf.Rad2Deg;
        go.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
        go.transform.localScale = new Vector3(scaleW * 2.8f, scaleW, 1f);

        Vector3 pos = go.transform.position;
        while (elapsed < life && go != null)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / life;

            pos += (Vector3)(windDir * speed * Time.deltaTime);
            pos += (Vector3)(perp * Mathf.Sin(elapsed * waveFreq) * waveAmp * Time.deltaTime);
            go.transform.position = pos;

            float s = Mathf.Lerp(1f, 0.3f, p * p);
            go.transform.localScale = new Vector3(scaleW * 2.8f * s, scaleW * s, 1f);

            if (sr != null)
                sr.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - p));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // --- Pet Flee ---

    private IEnumerator PlayPetFlee(PetCombatController pet, float t)
    {
        if (pet == null) yield break;

        // Foge para o lado de fora da arena
        float   targetX    = pet.transform.position.x > 0f ? 12f : -12f;
        Vector3 startPos   = pet.transform.position;
        Vector3 startScale = pet.transform.localScale;

        pet.animController?.PlayRun(true);
        pet.FlipToward(new Vector3(targetX, 0f, 0f));

        float duration = 0.60f * t;
        float elapsed  = 0f;

        while (elapsed < duration && pet != null)
        {
            elapsed  += Time.deltaTime;
            float p   = elapsed / duration;
            float ease = p * p; // ease-in: acelera ao sair
            pet.transform.position   = Vector3.Lerp(startPos, new Vector3(targetX, startPos.y, startPos.z), ease);
            pet.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, p * 0.75f);
            yield return null;
        }

        if (pet != null)
        {
            pet.healthBar?.FadeOutAndDestroy(0.2f);
            Destroy(pet.gameObject);
        }
    }

    // --- Hypnosis ---

    // Espiral de Arquimedes em P&B — gerada 1x e cacheada. Raio interno vazio (sem alpha),
    // faixas B&W em 4 voltas, fade suave na borda.
    private static Sprite _hypnoSpiralSprite;
    private static Sprite GetHypnoSpiralSprite()
    {
        if (_hypnoSpiralSprite != null) return _hypnoSpiralSprite;
        const int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float cx = size * 0.5f, cy = size * 0.5f, maxR = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                float r  = Mathf.Sqrt(dx * dx + dy * dy);
                if (r >= maxR) { tex.SetPixel(x, y, Color.clear); continue; }
                float angle  = Mathf.Atan2(dy, dx);
                float norm   = r / maxR;
                float phase  = angle - norm * Mathf.PI * 8f;   // 4 voltas completas
                float stripe = Mathf.Sin(phase);
                // Alpha: zero no centro, 1 no meio, zero na borda
                float alpha  = Mathf.SmoothStep(0f, 1f, norm * 6f)
                             * Mathf.SmoothStep(1f, 0f, (norm - 0.72f) * 3.6f);
                alpha = Mathf.Clamp01(alpha);
                float bright = stripe * 0.5f + 0.5f;
                tex.SetPixel(x, y, new Color(bright, bright, bright, alpha));
            }
        }
        tex.Apply();
        _hypnoSpiralSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _hypnoSpiralSprite;
    }

    private IEnumerator PlayHypnosisScreenEffect(PlayerCombat hypnotizer, float t)
    {
        var canvasGO = new GameObject("HypnosisOverlay");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        // Camada de branqueamento full-screen — simula P&B lavando as cores
        var bwGO  = new GameObject("BWOverlay");
        bwGO.transform.SetParent(canvas.transform, false);
        var bwRT  = bwGO.AddComponent<RectTransform>();
        bwRT.anchorMin = Vector2.zero;
        bwRT.anchorMax = Vector2.one;
        bwRT.offsetMin = bwRT.offsetMax = Vector2.zero;
        var bwImg = bwGO.AddComponent<UnityEngine.UI.Image>();
        bwImg.color = new Color(0.88f, 0.88f, 0.88f, 0f);

        // Espiral — 2300px cobre a diagonal da tela (1920×1080 → diagonal ≈ 2202px)
        var spiralGO  = new GameObject("HypnoSpiral");
        spiralGO.transform.SetParent(canvas.transform, false);
        var spiralRT  = spiralGO.AddComponent<RectTransform>();
        spiralRT.anchorMin = spiralRT.anchorMax = new Vector2(0.5f, 0.5f);
        spiralRT.pivot     = new Vector2(0.5f, 0.5f);
        spiralRT.sizeDelta = new Vector2(2300f, 2300f);
        var spiralImg = spiralGO.AddComponent<UnityEngine.UI.Image>();
        spiralImg.sprite = GetHypnoSpiralSprite();
        spiralImg.color  = new Color(1f, 1f, 1f, 0f);

        float duration = 1.8f * t;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / duration;

            // Envelope: sobe em 30%, plateau, desce nos últimos 35%
            float alpha;
            if      (p < 0.30f) alpha = p / 0.30f;
            else if (p < 0.65f) alpha = 1f;
            else                alpha = (1f - p) / 0.35f;
            alpha = Mathf.Clamp01(alpha);

            bwImg.color    = new Color(0.88f, 0.88f, 0.88f, alpha * 0.78f);
            spiralImg.color = new Color(1f, 1f, 1f, alpha);

            // Rotação: acelera na entrada, auge no meio, desacelera na saída
            float rotSpeed = Mathf.Lerp(20f, 260f, Mathf.Sin(p * Mathf.PI));
            spiralGO.transform.Rotate(0f, 0f, rotSpeed * Time.deltaTime);

            yield return null;
        }

        Destroy(canvasGO);
    }

    private IEnumerator PlayPetHypnotized(PetCombatController pet, PlayerCombat newOwner, float t)
    {
        if (pet == null || newOwner == null) yield break;

        // Flash purple
        float   flashDuration = 0.3f * t;
        float   elapsed       = 0f;
        var     renderers     = pet.GetComponentsInChildren<SpriteRenderer>(true);
        var     origColors    = System.Array.ConvertAll(renderers, r => r.color);
        var     purple        = new Color(0.6f, 0f, 0.9f, 1f);
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / flashDuration;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].color = Color.Lerp(origColors[i], purple, Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].color = origColors[i];

        // Run to new owner's area
        float   sign      = newOwner.transform.position.x > 0f ? -1f : 1f;
        Vector3 targetPos = new Vector3(
            newOwner.spawnPosition.x + sign * 1.5f,
            newOwner.spawnPosition.y,
            pet.transform.position.z);

        pet.FlipToward(targetPos);
        pet.animController?.PlayRun(true);

        float   runDuration = 0.8f * t;
        Vector3 startPos    = pet.transform.position;
        elapsed = 0f;
        while (elapsed < runDuration && pet != null)
        {
            elapsed += Time.deltaTime;
            pet.transform.position = Vector3.Lerp(startPos, targetPos, elapsed / runDuration);
            yield return null;
        }
        if (pet != null)
        {
            pet.transform.position = targetPos;
            pet.spawnPosition      = targetPos;   // próximo PetAttack já começa no lado certo
            pet.animController?.PlayRun(false);
            pet.animController?.SetIdle(true);
            pet.FlipToInitial();
        }
    }

    private static WeaponData FindWeaponByName(PlayerLoadout loadout, string name)
    {
        if (loadout == null || string.IsNullOrEmpty(name)) return null;
        foreach (var w in loadout.Weapons)
            if (w != null && w.weaponName == name) return w;
        return null;
    }

    // Mirrors ComboStrikeRoutine's reposition step (legacy path) — só a 1ª ação do turno tinha
    // RunToDefender; combo hits após uma esquiva ou knockback do hit anterior deixavam o
    // defensor mais longe, e nada recolocava o atacante perto antes do próximo swing/dodge/block.
    // No-op quando a distância já é pequena (ex: 1ª ação do turno, attacker já parado em posição).
    private IEnumerator RepositionIfNeeded(PlayerCombat attacker, PlayerCombat defender, float t)
    {
        if (attacker == null || defender == null) yield break;
        Vector2 attackPos = CalcAttackPosition(attacker, defender);
        if (Vector2.Distance(attacker.transform.position, attackPos) > 0.3f)
            yield return StartCoroutine(
                attacker.animationController.PlayRun(attackPos, (attacker.settings?.runSpeed ?? 35f) * t, attacker.movement));
    }

    private void ApplyHealthChanged(CombatEvent evt) => ApplyHealthDelta(evt.playerIndex, evt.newHp);

    // Compartilhado por Hit/Counter/Reversal/HealthChanged/TragicPotionUse — sempre sincroniza a
    // HealthSystem pro newHp já resolvido pelo simulador (que já leva Survival em conta), em vez
    // de aplicar o dano bruto do evento direto. TakeDamage/Heal só entendem valor relativo
    // (soma/subtração), então o delta é calculado aqui antes de chamar. delta > 0 é dano
    // (newHp menor que o atual); delta < 0 é cura (newHp maior — caso novo, introduzido pela
    // Tragic Potion, nenhum call site anterior produzia esse sinal).
    private void ApplyHealthDelta(int targetIndex, int newHp)
    {
        var hs = targetIndex == 0 ? _h1 : _h2;
        if (hs == null) return;
        int delta = hs.CurrentHealth - newHp;
        if (delta > 0)
        {
            hs.TakeDamage(delta);
            // Fast Metabolism: qualquer dano interrompe o pulso de cura (ver CombatSimulator.
            // ApplyDamage/SimulateTurn) — sem evento próprio pra esse desfecho, então a aura de
            // folhas é desfeita aqui, no único ponto que já centraliza toda perda de HP do
            // jogo, em vez de duplicar a checagem em cada case de dano (Hit/Counter/Reversal/
            // Bomb/Haste/Piledriver/Throw). No-op se este jogador não tiver a aura ativa.
            FadeFastMetabolismLeaves(targetIndex);
        }
        else if (delta < 0) hs.Heal(-delta);
    }

    private IEnumerator TriggerCombatEndRoutine(CombatEvent evt)
    {
        StopLingeringLoops();

        PlayerCombat loser = evt.playerIndex == 0 ? p2Combat : p1Combat;
        if (loser != null)
        {
            loser.animationController.PlayDying();
            yield return new WaitForSeconds(0.5f); // um ciclo da animação Dying (500ms no SCML)
            loser.animationController.SetSpeed(0f); // congela no último frame
            yield return new WaitForSeconds(0.4f); // pausa antes da tela de resultado
        }
        else
        {
            yield return new WaitForSeconds(0.9f);
        }

        if (sequencer != null)
        {
            PlayerCombat winner = evt.playerIndex == 0 ? p1Combat : p2Combat;
            sequencer.OnCombatEnd(winner);
        }
    }

    private void TriggerCombatEnd(CombatEvent evt)
    {
        StopLingeringLoops();

        if (sequencer != null)
        {
            PlayerCombat winner = evt.playerIndex == 0 ? p1Combat : p2Combat;
            sequencer.OnCombatEnd(winner);
        }
    }

    // Para todos os loops/coroutines visuais de duração indefinida que podem ter sobrado
    // ativos quando a luta termina de verdade (ex: a luta acaba enquanto alguém ainda está
    // net-ensnared, ou um Monk vencedor cuja aura é "permanente durante a luta" por design —
    // ver ShowMonkAura) — sem isso continuavam rodando Update todo frame durante o tempo
    // indefinido em que a tela de resultado/level-up fica aberta, já que a cena
    // 04_CombatScenePVP continua carregada por baixo dela. Cada Hide*/Release*/Stop* chamado
    // aqui já é idempotente/no-op se aquele efeito nunca esteve ativo (mesmo padrão usado nos
    // pontos normais de término de cada efeito durante a luta).
    private void StopLingeringLoops()
    {
        foreach (var combat in new[] { p1Combat, p2Combat })
        {
            if (combat == null) continue;

            combat.HideStunLabel();
            combat.HideFierceBruteAura(0f);
            combat.HidePoisonAura(0f);
            combat.StopMonkAuraPulse();

            // ReleaseNet devolve o GameObject da rede pro chamador animar fragmentos antes de
            // destruir — irrelevante aqui (luta já acabou), só destrói direto.
            var netVisual = combat.ReleaseNet();
            if (netVisual != null) Destroy(netVisual);
        }

        FadeFastMetabolismLeaves(0);
        FadeFastMetabolismLeaves(1);
    }

    // Fórmula única para todas as armas: base 2.0 + data.reach − (scale − 1) × 4.0
    // Scale é o principal fator: arma maior → personagem para mais perto do defensor.
    // data.reach é o knob de calibração por arma (float, permite 1.4 etc.).
    // A scale=1.5 o reach final = data.reach (efeito direto e intuitivo).
    private static Vector2 CalcAttackPosition(PlayerCombat attacker, PlayerCombat defender)
    {
        float reach;
        if (attacker.weaponHandler.CurrentWeapon == null)
        {
            reach = 0.8f;
        }
        else
        {
            var data = attacker.weaponHandler.CurrentWeaponData;
            reach = 2.0f + (data?.reach ?? 0f) - ((data?.scale ?? 1f) - 1f) * 4.0f;
            reach = Mathf.Max(0.3f, reach);
        }
        Vector2 defPos = defender.transform.position;
        Vector2 attPos = attacker.transform.position;
        Vector2 dir    = (defPos - attPos).normalized;
        return defPos - dir * reach;
    }

    private static string SwingTrigger(PlayerCombat attacker)
    {
        var data = attacker?.weaponHandler.CurrentWeaponData;

        // Sobe a cadeia previousTier para herdar attackAnimation do T1 caso T2/T3 ainda
        // estejam em Auto (gerados antes de o T1 ter a animação configurada manualmente).
        var check = data;
        while (check != null)
        {
            if (check.attackAnimation != AttackAnimation.Auto)
                return check.attackAnimation == AttackAnimation.SlashingDagger ? "SlashingDagger" : "Slashing";
            check = check.previousTier;
        }

        if (WeaponData.HasType(data, WeaponType.Fast)) return "SlashingDagger";
        return "Slashing";
    }

    // Bodybuilder: +40% velocidade de swing, só enquanto empunha arma Heavy — puramente visual
    // (CombatSimulator não usa PlayerState.hitSpeed pra escalar nenhuma animação, então o bônus
    // precisa ser aplicado aqui na reprodução, não no cálculo de chances/dano).
    private static float SwingSpeedMultiplier(PlayerCombat attacker)
    {
        if (attacker == null) return 1f;
        bool heavy = WeaponData.HasType(attacker.weaponHandler.CurrentWeaponData, WeaponType.Heavy);
        return (heavy && attacker.HasSkill("Bodybuilder")) ? 1.4f : 1f;
    }

    private static Vector2 ComputePushDir(PlayerCombat attacker, PlayerCombat defender)
    {
        if (attacker == null || defender == null) return Vector2.right;
        return ((Vector2)defender.transform.position - (Vector2)attacker.transform.position).normalized;
    }

    // Skill Net (libertação) — extraído do case NetFreed pra também ser reusado pela skill Bomb
    // (explosão que quebra a rede de qualquer alvo atingido, ver case BombThrow e
    // evt.netFreedTargets): ReleaseNet() para os loops de face/oscilação do personagem e
    // devolve o próprio GameObject da rede; aqui ela faz scale-up rápido e estoura em 5
    // fragmentos antes de ser destruída. No-op se o personagem não estiver com a rede ativa.
    private IEnumerator PlayNetBreakEffect(PlayerCombat character, float t)
    {
        if (character == null) yield break;
        yield return StartCoroutine(PlayNetBreakEffect(character.ReleaseNet(), t));
    }

    // Overload para pet — mesmo efeito de dissipação (scale-up + fragmentos radiais).
    private IEnumerator PlayNetBreakEffect(PetCombatController pet, float t)
    {
        if (pet == null) yield break;
        yield return StartCoroutine(PlayNetBreakEffect(pet.animController.ReleaseNet(), t));
    }

    private IEnumerator PlayNetBreakEffect(GameObject net2, float t)
    {
        if (net2 == null) yield break;

        var net2Renderer = net2.GetComponent<SpriteRenderer>();
        Vector3 net2BaseScale = net2.transform.localScale;

        // Scale up rápido (~1.3x, ~0.1s) antes de estourar.
        const float scaleUpDuration = 0.1f;
        float scaleElapsed = 0f;
        while (scaleElapsed < scaleUpDuration * t)
        {
            scaleElapsed += Time.deltaTime;
            net2.transform.localScale = Vector3.Lerp(net2BaseScale, net2BaseScale * 1.3f, scaleElapsed / (scaleUpDuration * t));
            yield return null;
        }

        // 5 fragmentos de mini-net distribuídos radialmente (0°, 72°, 144°, 216°, 288°),
        // reaproveitando o mesmo sprite de net2 em escala ~0.25x — sem asset novo, pedido pelo
        // usuário.
        if (net2Renderer != null && net2Renderer.sprite != null)
        {
            Vector3 burstPos   = net2.transform.position;
            Vector3 fragScale  = net2BaseScale * 0.25f;
            string  sortLayer  = net2Renderer.sortingLayerName;
            int     sortOrder  = net2Renderer.sortingOrder;
            Sprite  fragSprite = net2Renderer.sprite;

            for (int i = 0; i < 5; i++)
            {
                float angle = (360f / 5f) * i * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                var frag = new GameObject("NetFragment");
                frag.transform.position   = burstPos;
                frag.transform.localScale = fragScale;
                var fr = frag.AddComponent<SpriteRenderer>();
                fr.sprite           = fragSprite;
                fr.sortingLayerName = sortLayer;
                fr.sortingOrder     = sortOrder;
                StartCoroutine(NetFragmentFade(frag, dir, t));
            }
        }

        Destroy(net2);
    }

    // Skill Net (libertação): fragmento radial de mini-net — voa na direção dir a ~3 unid/s e
    // desaparece (fade alpha 1→0) ao longo de ~0.35s, destruindo-se no fim.
    private IEnumerator NetFragmentFade(GameObject frag, Vector3 dir, float t)
    {
        var sr = frag.GetComponent<SpriteRenderer>();
        const float speed    = 3f;
        const float duration = 0.35f;
        Color baseColor = sr.color;
        float elapsed = 0f;
        while (elapsed < duration * t)
        {
            elapsed += Time.deltaTime;
            frag.transform.position += dir * speed * Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / (duration * t));
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }
        Destroy(frag);
    }

    // Skill Fierce Brute — pose de braço levantado (power-up), chamada pelo case Hit já depois
    // de RepositionIfNeeded (posição correta, ao lado do defensor) em vez de no case
    // FierceBruteActivated (posição de spawn — bug corrigido, ver comentário lá). Pausa o
    // Animator pra a rotação manual do handBone não ser sobrescrita pela própria animação de
    // Idle/Run no frame seguinte (mesmo motivo de qualquer outro override manual de bone/sprite
    // no projeto, ver StunDazedLoop/NetFaceLoop em PlayerCombat).
    private IEnumerator PlayFierceBrutePose(PlayerCombat character, float t)
    {
        var armBone = character.weaponHandler.handBone;
        if (armBone == null) yield break;

        var anim = character.GetComponent<Animator>();
        if (anim != null) anim.speed = 0f;

        Quaternion fromRot = armBone.localRotation;
        Quaternion toRot   = Quaternion.Euler(-90f, 0f, 0f);

        float liftDuration = 0.2f * t;
        float elapsed = 0f;
        while (elapsed < liftDuration)
        {
            elapsed += Time.deltaTime;
            armBone.localRotation = Quaternion.Lerp(fromRot, toRot, elapsed / liftDuration);
            yield return null;
        }
        armBone.localRotation = toRot;

        yield return new WaitForSeconds(0.3f * t);

        float returnDuration = 0.15f * t;
        elapsed = 0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            armBone.localRotation = Quaternion.Lerp(toRot, fromRot, elapsed / returnDuration);
            yield return null;
        }
        armBone.localRotation = fromRot;

        if (anim != null) anim.speed = 1f;
    }

    // Skill Fierce Brute — efeito "Matrix" de ghost trail: clona todos os SpriteRenderers filhos
    // do personagem (membros do rig, igual ao Spriter2UnityDX) a cada `interval`, por `duration`
    // segundos, cada clone desaparecendo sozinho via FadeOutAndDestroy.
    // Bug corrigido (ghost trail não aparecia): GetComponentsInChildren<SpriteRenderer>() sem
    // includeInactive perdia qualquer parte do rig que o Spriter2UnityDX desativa via SetActive
    // entre frames de animação (PrefabBuilder.cs linha ~146 — variantes de sprite por bone são
    // GameObjects separados, só o do frame atual fica ativo) — em alguns frames isso podia
    // zerar renderers o suficiente pra o trail sair vazio/incompleto. `true` inclui todos,
    // mesmo os momentaneamente desligados. sr.sprite == null também pulado (renderer existe mas
    // ainda não recebeu nenhum sprite, ex.: TextureController que ainda não rodou Start()).
    private IEnumerator SpawnGhostTrail(Transform target, float duration, float interval, Color ghostColor)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // target é sempre p1Combat.transform/p2Combat.transform (chamadas em FierceBrute/
            // Hit) — GetComponentsInChildren aqui nunca varre pets (GameObjects raiz separados,
            // fora da hierarquia do personagem), então pets não introduzem custo extra aqui.
            var renderers = target.GetComponentsInChildren<SpriteRenderer>(true);

            foreach (var sr in renderers)
            {
                if (sr.sprite == null) continue;

                var (ghost, ghostSr) = RentGhost();
                ghost.transform.position   = sr.transform.position;
                ghost.transform.rotation   = sr.transform.rotation;
                ghost.transform.localScale = sr.transform.lossyScale;
                ghostSr.sprite           = sr.sprite;
                ghostSr.color            = ghostColor;
                // Default (mais atrás que TODAS as layers de combate — Weapons2/Characters2/
                // Weapons/Characters, ver CLAUDE.md) — trail sempre atrás do personagem/arma de
                // verdade, em vez de depender de sortingOrder - 1 dentro da MESMA layer do corpo
                // (arriscava overlaps imprevisíveis entre partes do rig que compartilham ordem).
                // Era "Characters2", correto só por acaso enquanto SetAttackerLayers nunca rodava
                // no path ativo (arma do atacante ficava sempre em "Weapons", que já é maior que
                // Characters2) — ver Bug 2 no CLAUDE.md. Agora que o atacante pode estar em
                // "Weapons2" (menor que Characters2), só "Default" garante ficar atrás em
                // qualquer um dos dois casos.
                ghostSr.sortingLayerName = "Default";
                ghostSr.sortingOrder     = sr.sortingOrder;
                ghostSr.flipX            = sr.flipX;
                ghostSr.flipY            = sr.flipY;
                // 0.5s de fade (era 0.3s) — rastro mais longo/denso, pedido pelo usuário.
                StartCoroutine(FadeOutAndDestroyGhost(ghost, 0.5f));
            }
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }

    // Desativa em vez de destruir (pool de _ghostPool, ver RentGhost) — o GameObject volta a
    // ficar disponível pro próximo RentGhost assim que o fade termina.
    private IEnumerator FadeOutAndDestroyGhost(GameObject go, float duration)
    {
        if (go == null) yield break;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        float startAlpha = sr.color.a;
        float elapsed = 0f;
        while (elapsed < duration && go != null)
        {
            elapsed += Time.deltaTime;
            var c = sr.color;
            c.a = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            sr.color = c;
            yield return null;
        }
        if (go != null) go.SetActive(false);
    }

    // Flash branco de tela inteira — Canvas/Image temporários criados e destruídos na hora (sem
    // nenhuma referência wireada), alpha 0 → peakAlpha → 0 em `duration` segundos no total.
    // Originalmente só da skill Fierce Brute (peakAlpha 0.4, default abaixo, mantém o mesmo
    // visual de antes); reusado pela skill Bomb com peakAlpha 0.3 (impacto em área, mais sutil).
    private IEnumerator FlashScreenWhite(float duration, float peakAlpha = 0.4f)
    {
        var canvasGo = new GameObject("FierceBruteFlash");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvasGo.AddComponent<CanvasScaler>();

        var imgGo = new GameObject("Flash");
        imgGo.transform.SetParent(canvasGo.transform, false);
        var img = imgGo.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);
        var rect = img.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        float half    = duration * 0.5f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            img.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, peakAlpha, elapsed / half));
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            img.color = new Color(1f, 1f, 1f, Mathf.Lerp(peakAlpha, 0f, elapsed / half));
            yield return null;
        }
        Destroy(canvasGo);
    }

    // Skill Tragic Potion — sequência de fases pedida pelo usuário: (1) pega o frasco na mão
    // LIVRE (offHandBone — a espada fica no handBone normal) e leva até a boca em arco (EaseOut
    // quad, mesmo padrão de CharacterPanel.cs), Animator pausado nesse intervalo pra não competir
    // com a animação de Idle/Run; (2) na boca, inclina o frasco simulando o gesto de beber e
    // mantém ali (drinkHold) ANTES de destruir — pedido explícito do usuário pra a poção não
    // desaparecer antes da cura visualmente acontecer; só então o frasco é destruído e o Animator
    // retomado; (3) partículas de cura sobem ao redor do personagem (fire-and-forget); (4) popup
    // verde "+N"; (5) sincroniza a barra de vida — ApplyHealthDelta (mesmo helper de Hit/Counter/
    // Reversal/Bomb) produz delta < 0 aqui (cura, ver HealthSystem.Heal), caso novo que nenhum
    // call site anterior gerava.
    private IEnumerator PlayTragicPotion(PlayerCombat character, CombatEvent evt, float t)
    {
        var anim = character.GetComponent<Animator>();
        if (anim != null) anim.speed = 0f;

        if (tragicPotionSprite != null)
        {
            // offHandBone = braço sem espada (mesmo bone usado pelo escudo da skill Shield,
            // ver WeaponHandler.EquipShield) — handBone normal fica ocupado pela arma equipada.
            var offHandBone = character.weaponHandler.offHandBone;
            Vector3 startPos = offHandBone != null ? offHandBone.position : character.transform.position + Vector3.up * 0.5f;

            // Posição da boca: Y baixado de 1.0 pra 0.38 (-0.62, estava alto demais — "tomando
            // pelo pescoço" e cobrindo a face) e um novo offset horizontal de +0.38 "pra frente"
            // (na direção que o personagem está virado, mesmo sinal de `localScale.x` usado na
            // inclinação abaixo) — sem isso a poção ficava alinhada com o centro do corpo em vez
            // de na frente do rosto. Mirror automático pro Player2 (vira pra esquerda).
            float forwardSign = Mathf.Sign(character.transform.localScale.x);
            Vector3 mouthPos = character.transform.position + new Vector3(0.38f * forwardSign, 0.38f, 0f);

            var potion = new GameObject("TragicPotion");
            potion.transform.position   = startPos;
            potion.transform.localScale = Vector3.one * 0.5f; // metade do tamanho original, pedido pelo usuário
            var potionRenderer = potion.AddComponent<SpriteRenderer>();
            potionRenderer.sprite           = tragicPotionSprite;
            potionRenderer.sortingLayerName = "Weapons";

            // Fase 1 — sobe da mão livre até a boca.
            const float liftDuration = 0.4f;
            float elapsed = 0f;
            while (elapsed < liftDuration * t)
            {
                elapsed += Time.deltaTime;
                float p    = Mathf.Clamp01(elapsed / (liftDuration * t));
                float ease = 1f - (1f - p) * (1f - p); // EaseOut quad
                potion.transform.position = Vector3.Lerp(startPos, mouthPos, ease);
                yield return null;
            }
            potion.transform.position = mouthPos;

            // Fase 2 — bebe: inclina o frasco (gesto de virar o frasco na boca) e mantém
            // inclinado por `drinkHold` ANTES de destruir, pra a poção continuar visível durante
            // o "beber" em vez de só sumir instantaneamente ao tocar a boca. Direção da
            // inclinação é sempre OPOSTA à direção que o personagem está virado (mesmo gesto de
            // tombar a cabeça/garrafa pra trás ao beber) — usa o mesmo sinal de
            // `localScale.x` do flip de direção já usado no projeto (ver NetVisual/StunLabel em
            // PlayerCombat.cs): Player1 vira pra direita (`localScale.x` positivo) então o
            // frasco tomba pra ESQUERDA (+50°, pedido explícito do usuário); Player2 vira pra
            // esquerda (`localScale.x` negativo, ver Medieval Warrior Girl na cena) então tomba
            // pra direita (-50°), espelhado.
            const float tiltDuration = 0.15f;
            const float drinkHold    = 0.35f;
            Quaternion fromRot = potion.transform.rotation;
            Quaternion tiltRot = Quaternion.Euler(0f, 0f, 50f * forwardSign); // mesmo sinal do offset "pra frente" acima
            elapsed = 0f;
            while (elapsed < tiltDuration * t)
            {
                elapsed += Time.deltaTime;
                potion.transform.rotation = Quaternion.Lerp(fromRot, tiltRot, elapsed / (tiltDuration * t));
                yield return null;
            }
            yield return new WaitForSeconds(drinkHold * t);

            Destroy(potion);
        }
        if (anim != null) anim.speed = 1f;

        // Pausa extra depois de beber, antes da animação de cura (partículas/popup/barra de
        // vida) — pedido pelo usuário pra a cura não começar colada no fim do gesto de beber.
        yield return new WaitForSeconds(0.25f * t);

        if (tragicPotionHealSprite != null)
        {
            int count = Random.Range(5, 8); // 5–7 inclusive
            for (int i = 0; i < count; i++)
                StartCoroutine(SpawnHealParticle(character, t, i * 0.08f * t));
        }

        Vector3 popupPos = character.transform.position + Vector3.up * 1.5f;
        DamagePopup.SpawnHeal(popupPos, evt.healAmount);
        ApplyHealthDelta(evt.playerIndex, evt.newHp);
    }

    // Tragic Potion (fase 3, uma partícula): pop-in de escala (0.3 -> 0.6 em 0.15s), sobe 1.5
    // unidades ao longo de 0.6s com fade out nos últimos 0.3s do movimento. delay escalona o
    // início de cada partícula (uma StartCoroutine por instância, chamadas em paralelo) pra dar
    // efeito de fluxo contínuo subindo em vez de todas nascerem no mesmo frame.
    private IEnumerator SpawnHealParticle(PlayerCombat character, float t, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (character == null) yield break;

        Vector3 startPos = character.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), -0.5f, 0f);
        var particle = new GameObject("TragicPotionHeal");
        particle.transform.position   = startPos;
        particle.transform.localScale = Vector3.one * 0.3f;
        var sr = particle.AddComponent<SpriteRenderer>();
        sr.sprite           = tragicPotionHealSprite;
        sr.sortingLayerName = "Characters";
        sr.sortingOrder     = 30;
        Color baseColor = sr.color;

        const float popDuration  = 0.15f;
        const float moveDuration = 0.6f;
        const float fadeDuration = 0.3f;

        float elapsed = 0f;
        while (elapsed < popDuration * t)
        {
            elapsed += Time.deltaTime;
            particle.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 0.6f, elapsed / (popDuration * t));
            yield return null;
        }
        particle.transform.localScale = Vector3.one * 0.6f;

        elapsed = 0f;
        float fadeStart = (moveDuration - fadeDuration) * t;
        while (elapsed < moveDuration * t)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / (moveDuration * t);
            particle.transform.position = startPos + Vector3.up * (1.5f * p);

            float alpha = elapsed > fadeStart ? Mathf.Lerp(1f, 0f, (elapsed - fadeStart) / (fadeDuration * t)) : 1f;
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }
        Destroy(particle);
    }

    // Fast Metabolism — regeneração passiva (1% do HP máximo, todo turno): animação LEVE e
    // rápida, fire-and-forget (chamada sem yield em ExecuteEvent — não pode atrasar o ritmo do
    // combate, já que acontece em todo turno do personagem). Sprite pequeno sobe suavemente e
    // desaparece — só reforço visual, a cura/popup já são aplicados no case antes desta coroutine
    // terminar.
    private IEnumerator PlayFastMetabolismRegen(PlayerCombat character, float t)
    {
        if (fastMetabolismController == null || character == null) yield break;

        var go = new GameObject("FastMetabolismRegen");
        go.transform.position   = character.transform.position;
        go.transform.localScale = Vector3.one * 0.5f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Characters";
        sr.sortingOrder     = 25;
        var anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = fastMetabolismController;

        Vector3 startPos = go.transform.position;
        Vector3 endPos   = startPos + Vector3.up * 0.8f;
        const float duration  = 0.4f;
        const float fadeStart = duration - 0.2f;
        Color baseColor = sr.color;
        float elapsed = 0f;
        while (elapsed < duration * t)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / (duration * t);
            go.transform.position = Vector3.Lerp(startPos, endPos, p);
            float fadeP = elapsed > fadeStart * t ? (elapsed - fadeStart * t) / (0.2f * t) : 0f;
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - Mathf.Clamp01(fadeP));
            yield return null;
        }
        Destroy(go);
    }

    // Fast Metabolism — folhas orbitando do pulso de 50% HP: 6 instâncias em posições radiais
    // (60° entre cada uma), cada uma com o mesmo Animator/controller da regeneração passiva.
    // Guardadas em _fastMetabolismLeaves[playerIndex] (null = sem pulso ativo) e giradas
    // continuamente por OrbitFastMetabolismLeaves enquanto existirem. Idempotente — não spawna
    // de novo se já houver uma aura ativa pra esse jogador.
    private void SpawnFastMetabolismLeaves(PlayerCombat character, int playerIndex)
    {
        if (fastMetabolismController == null || character == null) return;
        if (_fastMetabolismLeaves[playerIndex] != null) return;

        const int leafCount = 6;
        const float radius  = 1f;
        var leaves = new GameObject[leafCount];
        for (int i = 0; i < leafCount; i++)
        {
            float angle    = (360f / leafCount) * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

            var leaf = new GameObject($"FastMetabolismLeaf{i}");
            leaf.transform.position = character.transform.position + offset;
            var sr = leaf.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Characters";
            sr.sortingOrder     = 25;
            var anim = leaf.AddComponent<Animator>();
            anim.runtimeAnimatorController = fastMetabolismController;
            leaves[i] = leaf;
        }

        _fastMetabolismLeaves[playerIndex] = leaves;
        _fastMetabolismOrbitRoutine[playerIndex] = StartCoroutine(OrbitFastMetabolismLeaves(character, leaves));
    }

    // Gira cada folha em torno da posição ATUAL do personagem (RotateAround lida com o
    // personagem se movendo — knockback, jump-back — ao reler transform.position todo frame,
    // em vez de orbitar um ponto fixo capturado no spawn). Roda indefinidamente até a coroutine
    // ser parada de fora (FadeFastMetabolismLeaves) ou todas as folhas serem destruídas.
    private IEnumerator OrbitFastMetabolismLeaves(PlayerCombat character, GameObject[] leaves)
    {
        while (character != null)
        {
            bool anyAlive = false;
            foreach (var leaf in leaves)
            {
                if (leaf == null) continue;
                anyAlive = true;
                leaf.transform.RotateAround(character.transform.position, Vector3.forward, 60f * Time.deltaTime);
            }
            if (!anyAlive) yield break;
            yield return null;
        }
    }

    // Encerra a aura de folhas do jogador (pulso esgotado em 10 curas OU interrompido por dano,
    // ver ApplyHealthDelta) — para a coroutine de órbita e faz fade out (reusa
    // FadeOutAndDestroyGhost, já usado pelo ghost trail da Fierce Brute) em cada folha. No-op se
    // não houver aura ativa pra esse jogador (chamado incondicionalmente em todo dano sofrido).
    private void FadeFastMetabolismLeaves(int playerIndex)
    {
        var leaves = _fastMetabolismLeaves[playerIndex];
        if (leaves == null) return;
        _fastMetabolismLeaves[playerIndex] = null;

        if (_fastMetabolismOrbitRoutine[playerIndex] != null)
        {
            StopCoroutine(_fastMetabolismOrbitRoutine[playerIndex]);
            _fastMetabolismOrbitRoutine[playerIndex] = null;
        }

        foreach (var leaf in leaves)
            if (leaf != null) StartCoroutine(FadeOutAndDestroyGhost(leaf, 0.3f));
    }

    // Fast Metabolism — flash verde suave no personagem a cada cura do pulso: cor original →
    // verde → cor original (que já é branco/sem tint na maioria dos renderers, igual à
    // descrição "verde → branco" do pedido original). GetComponentsInChildren direto (em vez de
    // PlayerCombat.bodyRenderers, privado) — mesma técnica já usada em SpawnGhostTrail.
    private IEnumerator FlashCharacterGreen(PlayerCombat character, float duration)
    {
        if (character == null) yield break;
        var renderers = character.GetComponentsInChildren<SpriteRenderer>(true);
        var original = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            original[i] = renderers[i].color;

        Color flashColor = new Color(0.3f, 1f, 0.3f);
        float half = duration * 0.5f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / half;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = Color.Lerp(original[i], flashColor, p);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / half;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = Color.Lerp(flashColor, original[i], p);
            yield return null;
        }
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = original[i];
    }

    private static Vector2 RandomSpawnPos(bool isPlayer1)
    {
        float x = isPlayer1 ? Random.Range(-7.25f, -4.79f) : Random.Range(4.79f, 7.25f);
        float y = Random.Range(-3.90f, -0.81f);
        return new Vector2(x, y);
    }

    private static bool InSpawnZone(Vector2 pos, bool isPlayer1)
    {
        float xMin = isPlayer1 ? -7.25f : 4.79f;
        float xMax = isPlayer1 ? -4.79f : 7.25f;
        return pos.x >= xMin && pos.x <= xMax && pos.y >= -3.90f && pos.y <= -0.81f;
    }
}
