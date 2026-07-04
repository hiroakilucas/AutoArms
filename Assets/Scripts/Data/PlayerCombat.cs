using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Spriter2UnityDX;
using TMPro;

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Support Components")]
    public WeaponHandler weaponHandler;
    public MovementController movement;
    public AnimationController animationController;

    [Header("Attack Settings")]
    public AttackSettings settings;

    [Tooltip("True for Player1, false for Player2")]
    public bool isPlayer1;

    [Header("Defender")]
    public PlayerCombat defender;
    public AnimationController defenderAnimationController;

    [Header("Attributes")]
    public int agility = 10;
    public int str = 10;
    public int speed = 10;
    public float armor = 0f;
    public float evasion = 0f;
    public float accuracy = 0f;
    public int initiative = 0;
    public float reversal = 0f;
    public float counter = 0f;
    public float blockBonus = 0f;
    public float reversalAfterBlock = 0f;
    public float criticalChance = 0f;
    public float critDamageBonus = 0f;
    public float hitSpeed = 1f;
    public float runSpeedMultiplier = 1f;
    public float comboChanceBonus = 0f;
    public float disarmChanceBonus = 0f;
    public float stickyHands = 0f;

    [HideInInspector] public bool leadSkeleton   = false;
    [HideInInspector] public bool firstHitAvoided = false;
    [HideInInspector] public bool martialArts     = false;
    [HideInInspector] public bool weaponsMaster   = false;
    [HideInInspector] public bool hasShield       = false;

    [Header("Skills — Teste")]
    public List<SkillData> skills = new List<SkillData>();

    [Header("Debug")]
    public PlayerProfile debugProfile;

    [ContextMenu("Reset to Level 1")]
    private void ResetToLevel1()
    {
        if (debugProfile == null) return;
        debugProfile.level            = 1;
        debugProfile.xpCurrent        = 0;
        debugProfile.battlesRemaining = 6;
        debugProfile.xpRequired       = XpSystem.XpRequired(1);
        debugProfile.skills.Clear();
        debugProfile.pets.Clear();
        if (debugProfile.weaponLoadout != null)
            debugProfile.weaponLoadout.weapons = new WeaponData[0];
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(debugProfile);
        if (debugProfile.weaponLoadout != null)
            UnityEditor.EditorUtility.SetDirty(debugProfile.weaponLoadout);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

    public bool IsDead => GetComponent<HealthSystem>()?.IsDead ?? false;

    private float RuntimeRunSpeed => settings != null ? settings.runSpeed * runSpeedMultiplier : 35f;

    private Animator animator;
    private List<SpriteRenderer> bodyRenderers;
    private string defaultSortingLayer;
    public Vector2 spawnPosition;
    private SpriteRenderer faceRenderer;
    private Sprite[]       faceSprites;
    private GameObject stunLabel;
    private Coroutine   stunDazedRoutine;
    private GameObject netVisual;
    private Coroutine   netFaceRoutine;
    private Coroutine   netOscillateRoutine;
    private GameObject fierceBruteAura;
    private Coroutine   fierceBruteAuraPulseRoutine;
    private GameObject monkAura;
    private Coroutine   monkAuraPulseRoutine;
    private Coroutine   monkAuraFlashRoutine;
    private GameObject poisonAura;
    private Coroutine   poisonAuraPulseRoutine;
    private static Sprite _glowSprite;

    // Chaining: chamado quando este personagem é estunado (CombatEventType.Stunned) — label
    // fixo acima da cabeça (texto por enquanto; o usuário vai trocar por sprite depois) + pose
    // de "atordoado" (Face 03, olhos fechados, mesmo sprite do piscar de Thief) até o próprio
    // StunSkip consumir a ação estunada (ver HideStunLabel). Era um loop de SetTrigger("Hurt")
    // repetido — visualmente não combinava com "estunado" (parecia só levando hit repetidamente);
    // redefinido pelo usuário pra essa pose estática.
    //
    // O tronco tombado pra frente (bone "Body" rotacionado manualmente) foi tentado e revertido
    // — o pivot desse bone no rig do Spriter2UnityDX não fica no centro do sprite, então
    // rotacionar -20° girava o tronco pra fora da silhueta do personagem, deixando-o "sem
    // corpo" visualmente (bug real reportado pelo usuário). Sem solução segura sem testar
    // visualmente no Editor — só Face 03 por enquanto.
    public void ShowStunLabel()
    {
        if (stunLabel == null)
        {
            stunLabel = new GameObject("StunLabel");
            stunLabel.transform.SetParent(transform);
            stunLabel.transform.localPosition = Vector3.up * 2.2f;
            // Cancela o sinal de localScale.x do pai (mecanismo de flip de direção, ver
            // StealWeapon) pra o texto nunca renderizar espelhado/de trás pra frente.
            stunLabel.transform.localScale = new Vector3(Mathf.Sign(transform.localScale.x), 1f, 1f);

            var label = stunLabel.AddComponent<TextMeshPro>();
            label.alignment      = TextAlignmentOptions.Center;
            label.sortingLayerID = SortingLayer.NameToID("Characters");
            label.sortingOrder   = 50;
            label.fontStyle      = FontStyles.Bold;
            label.text           = "ATORDOADO!";
            label.fontSize       = 3.5f;
            label.color          = new Color(1f, 0.95f, 0.2f);
        }
        if (stunDazedRoutine == null)
            stunDazedRoutine = StartCoroutine(StunDazedLoop());
    }

    // Encerra a pose de atordoado e remove a label — chamado pelo StunSkip, no início do turno
    // que consome a ação estunada (ver CombatSimulator.SimulateTurn).
    public void HideStunLabel()
    {
        if (stunDazedRoutine != null)
        {
            StopCoroutine(stunDazedRoutine);
            stunDazedRoutine = null;
        }
        if (stunLabel != null)
        {
            Destroy(stunLabel);
            stunLabel = null;
        }
        if (faceRenderer != null && faceSprites != null && faceSprites.Length > 0)
            faceRenderer.sprite = faceSprites[0]; // Face 01, volta ao normal
        animationController.SetIdle(true);
    }

    // Mantém a pose presa todo frame (igual ao antigo loop de Hurt) porque o Animator (Idle em
    // loop) continua trocando o sprite da face normalmente em paralelo — sem reforçar a cada
    // frame, a troca manual seria sobrescrita no frame seguinte.
    private IEnumerator StunDazedLoop()
    {
        bool hasFace = faceRenderer != null && faceSprites != null && faceSprites.Length > 2;
        while (true)
        {
            if (hasFace) faceRenderer.sprite = faceSprites[2]; // Face 03, olhos fechados
            yield return null;
        }
    }

    // Skill Net: chamado pelo CombatPlayer (case NetThrow) quando este personagem acaba de ser
    // enredado — pose de agachado (Face 02, mesmo padrão reforçado todo frame do StunDazedLoop
    // acima) + sprite da rede caída (net2), parented a este transform (acompanha qualquer
    // knockback) oscilando lateralmente em loop. Continua "ligado" através de qualquer número
    // de NetEnsnaredSkip seguidos — só termina quando ReleaseNet() é chamado (CombatPlayer, case
    // NetFreed, ao sofrer um hit de verdade).
    public void ShowNetEnsnared(Sprite netLandedSprite, float scale = 1f)
    {
        if (netFaceRoutine == null)
            netFaceRoutine = StartCoroutine(NetFaceLoop());

        if (netVisual == null)
        {
            netVisual = new GameObject("NetVisual");
            netVisual.transform.SetParent(transform);
            netVisual.transform.localPosition = Vector3.zero;
            // Cancela o sinal de localScale.x do pai (flip de direção, mesmo padrão do
            // StunLabel acima) pra a rede nunca renderizar espelhada quando o personagem
            // está virado pra esquerda.
            netVisual.transform.localScale = new Vector3(Mathf.Sign(transform.localScale.x) * scale, scale, 1f);
            var sr = netVisual.AddComponent<SpriteRenderer>();
            sr.sprite           = netLandedSprite;
            sr.sortingLayerName = "Characters";
            sr.sortingOrder     = 30;
            netOscillateRoutine = StartCoroutine(NetOscillateLoop());
        }
    }

    // Solta este personagem da rede — para os loops de face/oscilação e restaura Face 01, mas
    // devolve o próprio GameObject da rede pro chamador (CombatPlayer) animar a sequência de
    // libertação (scale-up + fragmentos) antes de destruí-lo, em vez de já destruir aqui.
    public GameObject ReleaseNet()
    {
        if (netFaceRoutine != null) { StopCoroutine(netFaceRoutine); netFaceRoutine = null; }
        if (netOscillateRoutine != null) { StopCoroutine(netOscillateRoutine); netOscillateRoutine = null; }
        if (faceRenderer != null && faceSprites != null && faceSprites.Length > 0)
            faceRenderer.sprite = faceSprites[0]; // Face 01, volta ao normal
        animationController.SetIdle(true);

        var go = netVisual;
        netVisual = null;
        return go;
    }

    private IEnumerator NetFaceLoop()
    {
        bool hasFace = faceRenderer != null && faceSprites != null && faceSprites.Length > 1;
        while (true)
        {
            if (hasFace) faceRenderer.sprite = faceSprites[1]; // Face 02, agachado
            yield return null;
        }
    }

    // Oscilação lateral suave (amplitude ~0.05, frequência ~2Hz) — posição ABSOLUTA a cada
    // frame (não um "+=" acumulado): a descrição original pedia um incremento por frame, mas
    // somar Mathf.Sin(Time.time) every frame não estabiliza num vaivém, só deriva sem limite.
    // Recalcular o X relativo a partir de Time.time direto produz o vaivém pretendido de verdade.
    private IEnumerator NetOscillateLoop()
    {
        while (netVisual != null)
        {
            var lp = netVisual.transform.localPosition;
            lp.x = Mathf.Sin(Time.time * 2f) * 0.05f;
            netVisual.transform.localPosition = lp;
            yield return null;
        }
    }

    // Skill Fierce Brute: aura roxa persistente enquanto o buff (CombatSimulator.
    // PlayerState.fierceBruteActive) estiver ativo — chamada pelo CombatPlayer (case
    // FierceBruteActivated) e destruída de novo no Hit que consome o buff (com sucesso ou não,
    // ver CombatPlayer). Sprite gerado por procedimento (círculo com fade radial) em vez de um
    // asset novo — não existe nenhum sprite de glow pronto no projeto ainda.
    public void ShowFierceBruteAura()
    {
        if (fierceBruteAura != null) return;

        fierceBruteAura = new GameObject("FierceBruteAura");
        fierceBruteAura.transform.SetParent(transform);
        fierceBruteAura.transform.localPosition = Vector3.zero;
        fierceBruteAura.transform.localScale    = Vector3.one * 2.5f; // calibrável

        var sr = fierceBruteAura.AddComponent<SpriteRenderer>();
        sr.sprite           = GetGlowSprite();
        sr.color            = new Color(0.5f, 0f, 0.8f, 0.25f);
        sr.sortingLayerName = "Characters";
        sr.sortingOrder     = -1; // atrás de qualquer parte do corpo (todas usam ordem >= 0)

        fierceBruteAuraPulseRoutine = StartCoroutine(FierceBruteAuraPulseLoop());
    }

    // Pulso de alpha entre 0.15 e 0.35 via seno — mesmo padrão pedido pelo usuário pra outros
    // efeitos pulsantes (frequência ~3Hz, arbitrária/calibrável).
    private IEnumerator FierceBruteAuraPulseLoop()
    {
        var sr = fierceBruteAura.GetComponent<SpriteRenderer>();
        while (fierceBruteAura != null)
        {
            float wave  = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
            var   c     = sr.color;
            c.a         = Mathf.Lerp(0.15f, 0.35f, wave);
            sr.color    = c;
            yield return null;
        }
    }

    // Destrói a aura com um fade rápido (chamado tanto no Hit que acerta com o buff — fade
    // rápido pedido pelo usuário, 0.2s — quanto em qualquer desfecho que consome o buff sem
    // acertar: dodge/block/counter/reversal/arremesso).
    public void HideFierceBruteAura(float fadeDuration = 0.2f)
    {
        if (fierceBruteAura == null) return;
        if (fierceBruteAuraPulseRoutine != null) { StopCoroutine(fierceBruteAuraPulseRoutine); fierceBruteAuraPulseRoutine = null; }
        StartCoroutine(FadeOutAndDestroy(fierceBruteAura, fadeDuration));
        fierceBruteAura = null;
    }

    private IEnumerator FadeOutAndDestroy(GameObject go, float duration)
    {
        var sr = go.GetComponent<SpriteRenderer>();
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
        if (go != null) Destroy(go);
    }

    // Skill Monk: aura laranja persistente durante a luta INTEIRA — diferente da aura da Fierce
    // Brute (some no próximo hit), essa nunca é destruída/escondida; chamada uma única vez por
    // CombatPlayer.PlayCombat (idempotente, no-op se já existir), não depende de nenhum evento
    // específico. Mesmo sprite procedural (GetGlowSprite) e mesmo padrão de pulso de alpha via
    // seno da Fierce Brute, só com cor/escala/faixa de alpha diferentes (referência visual
    // pedida pelo usuário: personagem com energia ao redor, do print do jogo original).
    public void ShowMonkAura()
    {
        if (monkAura != null) return;

        monkAura = new GameObject("MonkAura");
        monkAura.transform.SetParent(transform);
        monkAura.transform.localPosition = Vector3.zero;
        monkAura.transform.localScale    = Vector3.one * 1.8f; // calibrável

        var sr = monkAura.AddComponent<SpriteRenderer>();
        sr.sprite           = GetGlowSprite();
        sr.color            = new Color(1f, 0.5f, 0f, 0.30f);
        sr.sortingLayerName = "Characters";
        sr.sortingOrder     = -1; // atrás de qualquer parte do corpo (todas usam ordem >= 0)

        monkAuraPulseRoutine = StartCoroutine(MonkAuraPulseLoop());
    }

    // Pulso de alpha entre 0.20 e 0.40 via seno — mesma frequência (~3Hz) e mesmo padrão da
    // Fierce Brute, só com a faixa de alpha própria do Monk.
    private IEnumerator MonkAuraPulseLoop()
    {
        var sr = monkAura.GetComponent<SpriteRenderer>();
        while (monkAura != null)
        {
            float wave = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
            var   c    = sr.color;
            c.a        = Mathf.Lerp(0.20f, 0.40f, wave);
            sr.color   = c;
            yield return null;
        }
    }

    // Contra-ataque do Monk (CombatEventType.Counter com playerIndex = Monk, ver CombatPlayer):
    // pisca a aura — spike de alpha até 0.8 em 0.05s, volta ao normal em 0.1s — sem destruir a
    // aura nem o pulso contínuo, só pausa o pulso enquanto o flash roda e retoma depois.
    public void FlashMonkAura()
    {
        if (monkAura == null) return;
        if (monkAuraFlashRoutine != null) StopCoroutine(monkAuraFlashRoutine);
        monkAuraFlashRoutine = StartCoroutine(MonkAuraFlashRoutine());
    }

    private IEnumerator MonkAuraFlashRoutine()
    {
        if (monkAuraPulseRoutine != null) { StopCoroutine(monkAuraPulseRoutine); monkAuraPulseRoutine = null; }

        var sr = monkAura.GetComponent<SpriteRenderer>();
        float startAlpha = sr.color.a;

        const float upDuration = 0.05f;
        float elapsed = 0f;
        while (elapsed < upDuration && monkAura != null)
        {
            elapsed += Time.deltaTime;
            var c = sr.color;
            c.a = Mathf.Lerp(startAlpha, 0.8f, elapsed / upDuration);
            sr.color = c;
            yield return null;
        }

        const float downDuration = 0.1f;
        elapsed = 0f;
        while (elapsed < downDuration && monkAura != null)
        {
            elapsed += Time.deltaTime;
            var c = sr.color;
            c.a = Mathf.Lerp(0.8f, 0.30f, elapsed / downDuration);
            sr.color = c;
            yield return null;
        }

        if (monkAura != null)
            monkAuraPulseRoutine = StartCoroutine(MonkAuraPulseLoop());
    }

    // Para o pulso contínuo da aura do Monk sem destruir o GameObject — a aura em si é
    // permanente "durante a luta" por design (ver ShowMonkAura acima), mas nada se beneficia
    // de continuar pulsando (Update todo frame) depois que a luta de fato terminou e o
    // resultado já foi decidido (tela de level-up pode ficar aberta por tempo indefinido).
    // Chamado só por CombatPlayer.TriggerCombatEnd — no-op se a aura nunca existiu ou já não
    // está pulsando.
    public void StopMonkAuraPulse()
    {
        if (monkAuraPulseRoutine != null) { StopCoroutine(monkAuraPulseRoutine); monkAuraPulseRoutine = null; }
        if (monkAuraFlashRoutine != null) { StopCoroutine(monkAuraFlashRoutine); monkAuraFlashRoutine = null; }
    }

    // Skill Chef: aura verde persistente no defensor enquanto `poisoned` (CombatSimulator.
    // PlayerState.poisoned) estiver true — chamada pelo CombatPlayer (case ChefPizzaThrow, ao
    // chegar a pizza) e destruída de novo quando o veneno é curado (case TragicPotionUse) ou a
    // luta acaba. Mesmo sprite procedural (GetGlowSprite) e mesmo padrão de pulso de alpha via
    // seno da Fierce Brute/Monk, só com cor/faixa de alpha próprias do veneno.
    public void ShowPoisonAura()
    {
        if (poisonAura != null) return;

        poisonAura = new GameObject("PoisonAura");
        poisonAura.transform.SetParent(transform);
        poisonAura.transform.localPosition = Vector3.zero;
        poisonAura.transform.localScale    = Vector3.one * 1.8f; // calibrável

        var sr = poisonAura.AddComponent<SpriteRenderer>();
        sr.sprite           = GetGlowSprite();
        sr.color            = new Color(0f, 0.8f, 0.2f, 0.25f);
        sr.sortingLayerName = "Characters";
        sr.sortingOrder     = -1; // atrás de qualquer parte do corpo (todas usam ordem >= 0)

        poisonAuraPulseRoutine = StartCoroutine(PoisonAuraPulseLoop());
    }

    // Pulso de alpha entre 0.15 e 0.35 via seno — mesmo padrão/frequência (~3Hz) da Fierce
    // Brute/Monk, só com a faixa de alpha própria do veneno.
    private IEnumerator PoisonAuraPulseLoop()
    {
        var sr = poisonAura.GetComponent<SpriteRenderer>();
        while (poisonAura != null)
        {
            float wave = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
            var   c    = sr.color;
            c.a        = Mathf.Lerp(0.15f, 0.35f, wave);
            sr.color   = c;
            yield return null;
        }
    }

    // Destrói a aura com fade (chamado ao curar o veneno — Tragic Potion — ou ao final da
    // luta). No-op se a aura não existir (mesmo padrão de HideFierceBruteAura), então pode ser
    // chamado incondicionalmente em qualquer TragicPotionUse sem checar antes se o personagem
    // de fato estava envenenado.
    public void HidePoisonAura(float fadeDuration = 0.3f)
    {
        if (poisonAura == null) return;
        if (poisonAuraPulseRoutine != null) { StopCoroutine(poisonAuraPulseRoutine); poisonAuraPulseRoutine = null; }
        StartCoroutine(FadeOutAndDestroy(poisonAura, fadeDuration));
        poisonAura = null;
    }

    // Círculo branco com fade radial (alpha caindo do centro pra borda, "blur" pobre) gerado uma
    // única vez e cacheado — evita depender de um asset de glow que não existe no projeto ainda.
    public static Sprite GetGlowSprite()
    {
        if (_glowSprite != null) return _glowSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Vector2 center  = new Vector2(size / 2f, size / 2f);
        float   maxDist = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist  = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - dist / maxDist);
                alpha *= alpha; // queda mais suave perto da borda
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        _glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _glowSprite;
    }

    private static readonly List<GameObject> fallenWeapons = new List<GameObject>();

    public static void CleanupFallenWeapons()
    {
        foreach (var w in fallenWeapons)
            if (w != null) Destroy(w);
        fallenWeapons.Clear();
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        weaponHandler = GetComponent<WeaponHandler>();
        bodyRenderers = new List<SpriteRenderer>(GetComponentsInChildren<SpriteRenderer>());
        if (bodyRenderers.Count > 0)
            defaultSortingLayer = bodyRenderers[0].sortingLayerName;

        // "Face 01" é o nome do GameObject gerado pelo Spriter2UnityDX nos 3 personagens, com um
        // TextureController (Sprites[0..2] = Face 01/02/03) e um SpriteRenderer próprio. Usado
        // pelo "piscar" da skill Thief (StealWeapon abaixo). Pega o SpriteRenderer e o array de
        // sprites direto, em vez de setar TextureController.DisplayedSprite — esse componente só
        // aplica a troca em Update() quando o Animator não está em transição (IsTransitioning()),
        // o que pode atrasar/perder a troca durante uma sequência rápida; setar o SpriteRenderer
        // direto garante a troca no mesmo frame, sem depender desse gate.
        foreach (var tc in GetComponentsInChildren<TextureController>(true))
        {
            if (tc.gameObject.name != "Face 01") continue;
            faceRenderer = tc.GetComponent<SpriteRenderer>();
            faceSprites  = tc.Sprites;
            break;
        }
    }

    private void Start()
    {
        spawnPosition = RandomSpawnPosition();
        transform.position = spawnPosition;
        animationController.SetIdle(true);
    }

    public IEnumerator AttackRoutine()
    {
        // Se desarmado ao início do turno: 40% de chance de pegar a próxima arma.
        // Sucesso → ataca com a arma normalmente.
        // Falha   → soca desarmado e passa a vez (sem re-equip ao final).
        if (weaponHandler.CurrentWeapon == null)
        {
            if (Random.value < 0.40f)
            {
                weaponHandler.EquipRandom();
                if (weaponHandler.CurrentWeapon != null)
                    yield return animationController.PlayCatchWeapon(0.6f);
            }
        }

        SetAttackerLayers();
        yield return null;

        yield return animationController.PlayIdle(settings.idleDuration);

        // Throw verifica ANTES do melee. Se triggar: arremessa do lugar, sem Run+JumpBack.
        if (defender != null && !defender.IsDead && weaponHandler.CurrentWeapon != null
            && Random.value < ThrowChance())
        {
            yield return ThrowRoutine();
            animationController.SetIdle(true); // espelha ReturnToSpawn: sai do estado Throwing antes do próximo turno
        }
        else
        {
            yield return StrikeRoutine();

            int comboCount = 0;
            while (defender != null && !defender.IsDead && Random.value < ComboChance(comboCount))
            {
                yield return ComboStrikeRoutine();
                comboCount++;
            }

            yield return ReturnToSpawn();
        }

        RestoreDefaultLayers();
    }

    // Soma os valores-base de cada tag presente na arma (WeaponData.types é uma lista) — mesma
    // tabela/regra de CombatSimulator.TagSum, ver CLAUDE.md.
    private static float TagSum(WeaponData data, float sharp = 0f, float fast = 0f, float heavy = 0f, float thrown = 0f)
    {
        if (data == null) return 0f;
        float total = 0f;
        if (data.HasType(WeaponType.Sharp))  total += sharp;
        if (data.HasType(WeaponType.Fast))   total += fast;
        if (data.HasType(WeaponType.Heavy))  total += heavy;
        if (data.HasType(WeaponType.Thrown)) total += thrown;
        return total;
    }

    private float ThrowChance()
    {
        if (weaponHandler.CurrentWeapon == null) return 0f;
        return TagSum(weaponHandler.CurrentWeaponData, sharp: 0.15f, heavy: 0.10f, thrown: 1.00f);
    }

    // comboCount = quantos hits extra de combo já aconteceram neste turno (0 = checagem do 1º hit extra).
    // Decaimento ×0.5 por hit consecutivo, aplicado depois do clamp — ver CombatSimulator.ComboChance.
    private float ComboChance(int comboCount = 0)
    {
        float base_ = weaponHandler.CurrentWeapon == null
            ? 0.05f
            : TagSum(weaponHandler.CurrentWeaponData, sharp: 0.12f, fast: 0.03f, heavy: 0.04f);
        float agiBonus    = Mathf.Max(0, agility - 3) * 0.008f;
        float weaponCombo = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.comboBonus : UnarmedStats.ComboBonus;
        float total = Mathf.Clamp(base_ + agiBonus + comboChanceBonus + weaponCombo, 0f, 0.35f);
        return total * Mathf.Pow(0.5f, comboCount);
    }

    // criticalChance: base do profile + bônus de skills (ex: Fierce Brute +0.10f) + bônus da arma.
    private float CritChance()
    {
        float baseChance = TagSum(weaponHandler.CurrentWeaponData, sharp: 0.05f, fast: 0.03f, heavy: 0.03f);
        float weaponBonus = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.critChanceBonus : UnarmedStats.CritChanceBonus;
        return baseChance + weaponBonus + criticalChance;
    }

    // Legado/código morto enquanto useSimulator=true (ver CombatSimulator.BlockChance, que lê
    // defender.blockBonus — Shield/Counter Attack somam ali, não em defender.counter, que aqui
    // virou a mecânica própria de Counter/Reversal). Não atualizado pra refletir blockBonus.
    private float BlockChance()
    {
        if (defender == null) return 0f;
        float weaponBonus = defender.weaponHandler.CurrentWeapon == null
            ? 0f
            : TagSum(defender.weaponHandler.CurrentWeaponData, sharp: 0.15f, heavy: 0.15f);
        float weaponBlockBonus = defender.weaponHandler.CurrentWeaponData != null
            ? defender.weaponHandler.CurrentWeaponData.blockBonus : UnarmedStats.BlockBonus;
        return weaponBonus + defender.counter + weaponBlockBonus;
    }

    // Impact (skill futura): adiciona +0.15f a este valor permanentemente. Shield desarma por
    // uma chance própria e independente (ver CombatSimulator.ShieldDisarmChance), não por aqui.
    private float DisarmChance()
    {
        float baseChance = weaponHandler.CurrentWeapon == null
            ? 0f
            : TagSum(weaponHandler.CurrentWeaponData, sharp: 0.10f, fast: 0.10f, heavy: 0.05f);
        float weaponDisarmBonus = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.disarmBonus : UnarmedStats.DisarmBonus;
        return baseChance + weaponDisarmBonus + disarmChanceBonus;
    }

    // evasion: base por tipo de arma do defensor + bônus de agilidade + evasion do defensor + bônus da arma.
    // Skills: Sixth Sense +0.10f, Untouchable +0.30f, Ballet Shoes +0.10f.
    private float DodgeChance()
    {
        if (defender == null) return 0f;
        float baseChance = TagSum(defender.weaponHandler.CurrentWeaponData, sharp: 0.10f, fast: 0.05f, heavy: 0.05f);
        float agiBonus       = Mathf.Max(0, defender.agility - 3) * 0.02f;
        float weaponEvasion  = defender.weaponHandler.CurrentWeaponData != null
            ? defender.weaponHandler.CurrentWeaponData.evasionBonus : UnarmedStats.EvasionBonus;
        // Bodybuilder: +10% evasion ("dexterity"), só enquanto empunha arma Heavy.
        float bodybuilderBonus = (WeaponData.HasType(defender.weaponHandler.CurrentWeaponData, WeaponType.Heavy) && defender.HasSkill("Bodybuilder")) ? 0.10f : 0f;
        return Mathf.Min(0.60f, baseChance + agiBonus + defender.evasion + weaponEvasion + bodybuilderBonus);
    }

    // weaponData.damage tem prioridade absoluta — cada WeaponData tem seu próprio campo
    // configurável, então não há mais ranges hardcoded por tag (Random.Range(7,13)/(10,18)/
    // (30,50) eram valores padrão do protótipo, de antes de cada arma ter o próprio Damage).
    private static int RollWeaponDamage(WeaponData data) => data.damage > 0 ? data.damage : 3;

    // Dano base da arma (sem STR/crítico/armadura).
    private int WeaponBaseDamage()
    {
        if (weaponHandler.CurrentWeapon == null)
            return martialArts ? UnarmedStats.Damage * 2 : UnarmedStats.Damage;
        return RollWeaponDamage(weaponHandler.CurrentWeaponData);
    }

    private float CritDamageMultiplier()
    {
        float baseMult = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.critDamageMultiplier : UnarmedStats.CritDamageMultiplier;
        return baseMult + critDamageBonus;
    }

    // Fórmula do My Brute original: STR soma direto no dano base da arma (flat, não percentual)
    // — (weaponBaseDamage + str) × critMultiplier × sharpMult. Lead Skeleton e armadura são
    // aplicados depois, em HitRoutine. Era weaponBaseDamage × (1 + str/10) (percentual,
    // divergia do original) — redefinida pelo usuário.
    private float CalcDamage(bool isCrit)
    {
        int   weaponBaseDamage = WeaponBaseDamage();
        float critMult         = isCrit ? CritDamageMultiplier() : 1f;
        bool  isSharp          = WeaponData.IsSharp(weaponHandler.CurrentWeaponData);
        float sharpMult        = (weaponsMaster && isSharp) ? 1.5f : 1f;
        return (weaponBaseDamage + str) * critMult * sharpMult;
    }

    // Dano do arremesso soma STR igual ao golpe normal (weaponBaseDamage + str) — era só
    // weaponBaseDamage, redefinida pelo usuário.
    private static int ThrowDamage(WeaponData data, int str)
    {
        int weaponDamage = data == null ? 2 : RollWeaponDamage(data);
        return weaponDamage + str;
    }

    // Posição de ataque: imediatamente fora do alcance da arma, na direção do defensor.
    // weaponData.reach soma-se à distância base do tipo de arma. Heavy e Sharp/default não são
    // somados (não faz sentido físico somar dois alcances de categoria inteiros) — Heavy tem
    // prioridade, depois Fast desconta (recria exatamente o 1.5 da antiga Dagger = Sharp+Fast).
    private Vector2 AttackPosition()
    {
        if (defender == null) return spawnPosition;
        Vector2 defPos = (Vector2)defender.transform.position;
        float baseReach;
        if (weaponHandler.CurrentWeapon == null)
        {
            baseReach = 0.8f;
        }
        else
        {
            baseReach = WeaponData.HasType(weaponHandler.CurrentWeaponData, WeaponType.Heavy) ? 2.8f : 2.0f;
            if (WeaponData.HasType(weaponHandler.CurrentWeaponData, WeaponType.Fast)) baseReach -= 0.5f;
        }
        float reach = baseReach + (weaponHandler.CurrentWeaponData?.reach ?? 0);
        Vector2 dir = (defPos - (Vector2)transform.position).normalized;
        return defPos - dir * reach;
    }

    private IEnumerator StrikeRoutine()
    {
        yield return animationController.PlayRun(AttackPosition(), RuntimeRunSpeed, movement);
        yield return HitRoutine();
    }

    // Any State → Slashing (CanTransitionToSelf=1) no controller permite re-entrar no Slashing após o run.
    private IEnumerator ComboStrikeRoutine()
    {
        yield return new WaitForSeconds(settings.slashingToJumpDelay);
        Vector2 attackPos = AttackPosition();
        if (Vector2.Distance(transform.position, attackPos) > 0.3f)
            yield return animationController.PlayRun(attackPos, RuntimeRunSpeed, movement);
        yield return HitRoutine(isCombo: true);
    }

    // Slash → (knockback + Hurt em paralelo) → dano → (se não combo) checar Desarmar.
    // Quando desarmado: usa "Slashing" (soco) com dano calculado por STR.
    private IEnumerator HitRoutine(bool applyKnockback = true, bool isCombo = false)
    {
        string slashTrigger =
            WeaponData.HasType(weaponHandler.CurrentWeaponData, WeaponType.Fast) ? "SlashingDagger" :
            "Slashing";

        // Ballet Shoes: primeiro golpe da luta automaticamente esquivado
        if (defender != null && defender.firstHitAvoided)
        {
            defender.firstHitAvoided = false;
            defender.LogSkillCheck("Ballet Shoes", true, "first hit of fight automatically avoided");
            Vector2 balletDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.DodgeLeap(balletDir, settings.knockbackDistance));
            Vector3 balletPos = defender.transform.position + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.SpawnDodge(balletPos);
            yield return new WaitForSeconds(settings.slashingDuration * 0.5f);
            yield break;
        }

        float weaponHitSpeed = weaponHandler.CurrentWeaponData != null
            ? weaponHandler.CurrentWeaponData.hitSpeed : UnarmedStats.HitSpeed;
        // Bodybuilder: +40% hit speed, só enquanto empunha arma Heavy.
        float bodybuilderSpeedMult = (WeaponData.HasType(weaponHandler.CurrentWeaponData, WeaponType.Heavy) && HasSkill("Bodybuilder")) ? 1.4f : 1f;
        float slashSpeed = hitSpeed * weaponHitSpeed * bodybuilderSpeedMult;
        if (slashSpeed != 1f) animationController.SetSpeed(slashSpeed);

        animator.SetTrigger(slashTrigger);
        yield return new WaitForSeconds(settings.slashingDuration * 0.5f);

        if (defender != null && Random.value < DodgeChance())
        {
            Vector2 dodgeDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.DodgeLeap(dodgeDir, settings.knockbackDistance));

            Vector3 dodgePos = defender.transform.position
                + Vector3.up   * 1.5f
                + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.SpawnDodge(dodgePos);
            if (slashSpeed != 1f) animationController.SetSpeed(1f);
            yield return new WaitForSeconds(settings.slashingDuration * 0.5f);
            yield break;
        }

        if (defender != null && Random.value < BlockChance())
        {
            Vector2 blockDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.Knockback(blockDir, settings.knockbackDistance * 0.5f, settings.hurtDuration));
            defender.StartCoroutine(defenderAnimationController.PlayBlock(0.36666667f));

            Vector3 blockPos = defender.transform.position
                + Vector3.up   * 1.5f
                + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.SpawnBlock(blockPos);

            if (weaponHandler.CurrentWeapon != null && Random.value < 0.15f)
                StartCoroutine(DropWeapon(this, isDisarm: false));
            if (defender.weaponHandler.CurrentWeapon != null && Random.value < 0.10f)
                StartCoroutine(DropWeapon(defender, isDisarm: false));

            if (slashSpeed != 1f) animationController.SetSpeed(1f);
            yield return new WaitForSeconds(settings.slashingDuration * 0.5f);
            yield break;
        }

        if (applyKnockback && defender != null)
        {
            Vector2 pushDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.Knockback(pushDir, settings.knockbackDistance, settings.hurtDuration));
        }

        if (defenderAnimationController != null)
            yield return defenderAnimationController.PlayHurt(settings.hurtDuration);

        bool  isCrit = Random.value < CritChance();
        float dmg    = CalcDamage(isCrit);

        // Lead Skeleton: -15% dano de arma blunt (Heavy)
        if (defender != null && defender.leadSkeleton && WeaponData.IsBlunt(weaponHandler.CurrentWeaponData))
        {
            dmg *= 0.85f;
            defender.LogSkillCheck("Lead Skeleton", true, "blunt damage ×0.85");
        }

        // Fórmula multiplicativa do My Brute: finalDamage = Max(1, Round(dmg × (1 - armor)))
        float defenderArmor = defender != null ? defender.armor : 0f;
        int   finalDamage   = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - defenderArmor)));

        defender?.GetComponent<HealthSystem>()?.TakeDamage(finalDamage);

        if (defender != null)
        {
            Vector3 popupPos = defender.transform.position
                + Vector3.up   * 1.5f
                + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.Spawn(popupPos, finalDamage, isCrit);
        }

        // Iron Head: logo após sofrer o dano (qualquer hit, incluindo combo), +40% chance do
        // defensor derrubar a arma do atacante (inverso do Disarm abaixo).
        if (defender != null && defender.HasSkill("Iron Head") && weaponHandler.CurrentWeapon != null
            && Random.value < 0.40f)
        {
            StartCoroutine(DropWeapon(this, isDisarm: false));
        }

        if (!isCombo && defender != null && !defender.IsDead
            && defender.weaponHandler.CurrentWeapon != null
            && Random.value < DisarmChance())
        {
            StartCoroutine(DropWeapon(defender));
        }

        if (slashSpeed != 1f) animationController.SetSpeed(1f);
        yield return new WaitForSeconds(settings.slashingDuration * 0.5f);
    }

    // Arremessa a arma em linha reta até o defensor.
    // Thrown: nunca removida do loadout. Outras: removidas permanentemente após o arremesso.
    // Após o resultado (hit/miss): 40% pega a próxima arma imediatamente, 60% fica desarmado.
    private IEnumerator ThrowRoutine()
    {
        var weaponData = weaponHandler.CurrentWeaponData;
        bool isThrown = WeaponData.HasType(weaponData, WeaponType.Thrown);

        // Capture world position and scale BEFORE destroying the weapon object on Unequip.
        // lossyScale reflects real visual size (character has scale ~0.3 applied).
        GameObject inHandWeapon = weaponHandler.CurrentWeapon;
        // Prefer handBone.position for the exact hand Y; fall back to weapon object position.
        Vector3 launchPos = weaponHandler.handBone != null
            ? weaponHandler.handBone.position
            : (inHandWeapon != null
                ? inHandWeapon.transform.position
                : transform.position + Vector3.up * 0.5f);
        Vector3 projectileScale = inHandWeapon != null
            ? inHandWeapon.transform.lossyScale
            : (weaponHandler.handBone != null ? weaponHandler.handBone.lossyScale : Vector3.one) * (weaponData?.scale ?? 1f);

        if (isThrown)
            weaponHandler.Unequip();
        else
            weaponHandler.UnequipPermanent();

        // Fly straight from handBone to defender's actual position.
        // For WeaponType.Thrown a slight upward arc is added inside FlyWeapon.
        Vector3 targetPos = defender != null
            ? defender.transform.position
            : transform.position + (isPlayer1 ? Vector3.right : Vector3.left) * 5f;

        // Orienta o sprite na direção do voo (evita ponta para baixo/diagonal da rotação in-hand).
        Vector3 flightDir = (targetPos - launchPos).normalized;
        float flightAngle = Mathf.Atan2(flightDir.y, flightDir.x) * Mathf.Rad2Deg;

        // Projétil pertence exclusivamente a este atacante. O weaponHandler do defensor nunca é tocado.
        // Usar Mathf.Abs no scale: personagens espelhados (Player 2) têm lossyScale.x negativo,
        // o que causaria flip do sprite + ângulo invertido. O Atan2 já cuida da direção correta.
        var flyingWeapon = new GameObject("FlyingWeapon");
        flyingWeapon.transform.position = launchPos;
        flyingWeapon.transform.localScale = new Vector3(
            Mathf.Abs(projectileScale.x),
            Mathf.Abs(projectileScale.y),
            Mathf.Abs(projectileScale.z));
        flyingWeapon.transform.rotation = Quaternion.Euler(0, 0, flightAngle);
        var sr = flyingWeapon.AddComponent<SpriteRenderer>();
        sr.sprite = weaponData?.inHandSprite;
        sr.sortingLayerName = "Weapons";
        sr.sortingOrder = 10;

        animator.SetTrigger("Throwing");

        // Thrown rotates and gets a slight arc; all others fly in a straight horizontal line.
        bool  rotate = isThrown;
        float arc    = isThrown ? 0.5f : 0f;
        yield return FlyWeapon(flyingWeapon.transform, launchPos, targetPos, 0.45f, rotate, arc);
        Destroy(flyingWeapon);  // sempre destruído antes de resolver hit/miss

        if (defender != null && Random.value < 0.80f)
        {
            Vector2 pushDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
            defender.StartCoroutine(defender.Knockback(pushDir, settings.knockbackDistance, settings.hurtDuration));
            yield return defenderAnimationController.PlayHurt(settings.hurtDuration);
            int damage = ThrowDamage(weaponData, str);
            defender.GetComponent<HealthSystem>()?.TakeDamage(damage);
            Vector3 popupPos = defender.transform.position + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.Spawn(popupPos, damage, false);
        }
        else
        {
            Vector3 missPos = (defender != null ? defender.transform.position : transform.position)
                + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
            DamagePopup.SpawnMiss(missPos);
            if (defender != null)
            {
                Vector2 dodgeDir = ((Vector2)defender.transform.position - (Vector2)transform.position).normalized;
                defender.StartCoroutine(defender.DodgeLeap(dodgeDir, settings.knockbackDistance));
            }
            yield return new WaitForSeconds(settings.hurtDuration);
        }

        // 40% de chance de pegar uma arma aleatória imediatamente após o arremesso.
        if (Random.value < 0.40f)
            weaponHandler.EquipRandom();
    }

    // Public/static so CombatPlayer (CombatSimulator replay) can reuse the same pendulum-fall
    // visual for Disarm/WeaponDrop events — mirrors how FlyWeapon was made public for ThrowWeapon.
    // Doesn't read any instance state, only target's, so it doesn't need a PlayerCombat instance to run on.
    public static IEnumerator DropWeapon(PlayerCombat target, bool isDisarm = true, bool isSabotage = false)
    {
        var data = target.weaponHandler.CurrentWeaponData;
        if (data?.inHandSprite == null) yield break;

        var inHand = target.weaponHandler.CurrentWeapon;
        Vector3 startPos  = inHand != null ? inHand.transform.position : target.transform.position + Vector3.up * 0.5f;
        Vector3 worldScale = inHand != null ? inHand.transform.lossyScale : Vector3.one * data.scale;

        target.weaponHandler.UnequipPermanent();

        Vector3 popupPos = target.transform.position + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
        // isSabotage tem prioridade — Saboteur (1ª arma quebrada na hora, ver CombatPlayer's
        // case SaboteurBreak) reusa a queda em pêndulo normal de DropWeapon, mas com o popup
        // "SABOTAGE!" em vez de "DISARM!"/"DROP!", consistente com o popup já usado pela skill
        // Sabotage (mesmo nome parecido, mecânica diferente — ver CLAUDE.md).
        if (isSabotage)
            DamagePopup.SpawnSabotage(popupPos);
        else if (isDisarm)
            DamagePopup.SpawnDisarm(popupPos);
        else
            DamagePopup.SpawnDrop(popupPos);

        var fallen = new GameObject("FallenWeapon");
        fallen.transform.position   = startPos;
        fallen.transform.localScale = new Vector3(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z));
        var sr = fallen.AddComponent<SpriteRenderer>();
        sr.sprite           = data.inHandSprite;
        sr.sortingLayerName = "Default";
        // sortingOrder 1 (não 0) — "Colosseum arena" também está na layer Default, ordem 0, no
        // mesmo Z (0) de qualquer personagem (Player1 e Player2). Empate de profundidade real
        // entre os dois deixa a ordem de desenho indefinida — podia renderizar a arma atrás do
        // fundo (invisível durante a queda inteira), reproduzido de forma consistente na posição
        // do Player2 (bug real reportado pelo usuário, "drop do player2 não anima"). Mesma
        // correção já aplicada em DropWeaponFromHud (Saboteur) por esse mesmo motivo.
        sr.sortingOrder     = 1;

        float groundY   = target.transform.position.y - 1.5f;
        float velocityY = 0f;
        float t         = 0f;
        float theta0    = Random.Range(60f, 100f); // amplitude inicial do pêndulo
        const float omega = 10f;                   // frequência angular (rad/s)
        const float gamma = 0.8f;                  // amortecimento

        fallen.transform.rotation = Quaternion.Euler(0f, 0f, theta0);

        while (fallen.transform.position.y > groundY)
        {
            velocityY -= 9.8f * Time.deltaTime;
            fallen.transform.position += new Vector3(0f, velocityY * Time.deltaTime, 0f);
            t += Time.deltaTime;
            float angle = theta0 * Mathf.Exp(-gamma * t) * Mathf.Cos(omega * t);
            fallen.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        var p = fallen.transform.position;
        fallen.transform.position = new Vector3(p.x, groundY, p.z);
        fallenWeapons.Add(fallen);
    }

    // Saboteur: mesma queda em pêndulo amortecido de DropWeapon, mas a arma nunca esteve
    // equipada (destruída direto do loadout, antes do 1º turno) — não tem CurrentWeapon/mão
    // pra sair de, então começa de `startWorldPos` (posição do ícone na WeaponHUD, já
    // convertida de tela pra mundo por CombatPlayer) em vez do handBone. Quem chama já
    // removeu a arma do loadout/HUD (WeaponHUD.RemoveWeapon) — aqui só cuida da queda visual.
    public static IEnumerator DropWeaponFromHud(PlayerCombat victim, WeaponData data, Vector3 startWorldPos)
    {
        if (data?.inHandSprite == null) yield break;

        // O X nunca muda durante a queda (só Y, por gravidade) — se o ícone na WeaponHUD
        // converter pra um X fora da arena jogável (ex: ícones nos cantos da tela, perto da
        // borda), a arma cai reto fora da área visível e parece "desaparecer do mapa" (bug
        // real reportado pelo usuário). Clampa só o X nos mesmos limites de ClampToArena
        // (±7.25) — não usa ClampToArena inteiro porque ele também clampa Y pro intervalo de
        // posição de PERSONAGEM (-3.90 a -0.81), o que destruiria a altura inicial da queda
        // (bem mais alta, perto do topo da tela, de propósito).
        startWorldPos.x = Mathf.Clamp(startWorldPos.x, -7.25f, 7.25f);

        var fallen = new GameObject("FallenWeapon");
        fallen.transform.position   = startWorldPos;
        // data.scale por si só é grande demais — é o multiplicador relativo ao bone da mão,
        // que por sua vez já está dentro do personagem (escala raiz ~0.3, ver
        // CombatSceneLoader.Initialize). DropWeapon/DropShield não têm esse problema porque
        // usam inHand.transform.lossyScale (escala já resolvida pela hierarquia); aqui não
        // existe nenhum objeto na mão pra ler de — multiplicar manualmente pela escala raiz
        // do personagem reproduz o mesmo resultado.
        fallen.transform.localScale = victim.transform.lossyScale * data.scale;
        var sr = fallen.AddComponent<SpriteRenderer>();
        sr.sprite           = data.inHandSprite;
        sr.sortingLayerName = "Default";
        // sortingOrder 1 (não 0) — o fundo da arena também está na layer Default, ordem 0;
        // como os dois ficam no mesmo Z (sprites 2D), empatam no critério de profundidade da
        // câmera e a ordem de desenho fica indefinida (podia renderizar atrás do fundo,
        // invisível durante toda a queda). +1 garante a arma sempre na frente do fundo, sem
        // mudar a relação com Characters/Weapons (ainda abaixo dos dois, igual a um
        // DropWeapon normal já pousado).
        sr.sortingOrder     = 1;

        float groundY   = victim.transform.position.y - 1.5f;
        float velocityY = 0f;
        float t         = 0f;
        float theta0    = Random.Range(60f, 100f);
        const float omega = 10f;
        const float gamma = 0.8f;

        fallen.transform.rotation = Quaternion.Euler(0f, 0f, theta0);

        while (fallen.transform.position.y > groundY)
        {
            velocityY -= 9.8f * Time.deltaTime;
            fallen.transform.position += new Vector3(0f, velocityY * Time.deltaTime, 0f);
            t += Time.deltaTime;
            float angle = theta0 * Mathf.Exp(-gamma * t) * Mathf.Cos(omega * t);
            fallen.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        var p2 = fallen.transform.position;
        fallen.transform.position = new Vector3(p2.x, groundY, p2.z);
        fallenWeapons.Add(fallen);
    }

    // Mesma queda em pêndulo amortecido de DropWeapon, mas pro escudo da skill Shield —
    // CurrentShieldData/CurrentShield (não current/CurrentWeaponData) e RemoveShield() em vez
    // de UnequipPermanent(), já que o escudo nunca esteve no WeaponLoadout. isDisarm escolhe o
    // popup: true (ao tomar hit, ShieldDisarm) → "DISARM!" laranja; false (ao bloquear com
    // sucesso, ShieldDrop) → "DROP!" laranja, mesma distinção de DropWeapon.
    public static IEnumerator DropShield(PlayerCombat target, bool isDisarm)
    {
        var data = target.weaponHandler.CurrentShieldData;
        if (data?.inHandSprite == null) yield break;

        var inHand = target.weaponHandler.CurrentShield;
        Vector3 startPos   = inHand != null ? inHand.transform.position : target.transform.position + Vector3.up * 0.5f;
        Vector3 worldScale = inHand != null ? inHand.transform.lossyScale : Vector3.one * data.scale;

        target.weaponHandler.RemoveShield();

        Vector3 popupPos = target.transform.position + Vector3.up * 1.5f + Vector3.right * Random.Range(-0.3f, 0.3f);
        if (isDisarm)
            DamagePopup.SpawnDisarm(popupPos);
        else
            DamagePopup.SpawnDrop(popupPos);

        var fallen = new GameObject("FallenShield");
        fallen.transform.position   = startPos;
        fallen.transform.localScale = new Vector3(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z));
        var sr = fallen.AddComponent<SpriteRenderer>();
        sr.sprite           = data.inHandSprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder     = 1; // mesmo motivo de DropWeapon — evita empate de profundidade com "Colosseum arena" (Default/0, Z=0)

        float groundY   = target.transform.position.y - 1.5f;
        float velocityY = 0f;
        float t         = 0f;
        float theta0    = Random.Range(60f, 100f);
        const float omega = 10f;
        const float gamma = 0.8f;

        fallen.transform.rotation = Quaternion.Euler(0f, 0f, theta0);

        while (fallen.transform.position.y > groundY)
        {
            velocityY -= 9.8f * Time.deltaTime;
            fallen.transform.position += new Vector3(0f, velocityY * Time.deltaTime, 0f);
            t += Time.deltaTime;
            float angle = theta0 * Mathf.Exp(-gamma * t) * Mathf.Cos(omega * t);
            fallen.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        var p2 = fallen.transform.position;
        fallen.transform.position = new Vector3(p2.x, groundY, p2.z);
        fallenWeapons.Add(fallen);
    }

    // Visual da skill Thief: o ladrão pula nas costas do adversário (igual montar um cavalo) e
    // os dois balançam juntos 4 vezes, antes do ladrão descer já com a arma roubada na mão —
    // referência visual do My Brute original pedida pelo usuário. Não existe sprite dedicado de
    // "montar" no projeto (Spriter2UnityDX só gera Idle/Running/Slashing/etc.), então é
    // aproximado via movimento de transform puro (mesmo espírito do pêndulo de DropWeapon/
    // DropShield acima — sem novo estado de Animator).
    public static IEnumerator StealWeapon(PlayerCombat thief, PlayerCombat victim)
    {
        var data = victim.weaponHandler.CurrentWeaponData;
        if (data == null) yield break;

        // jumpHeight era uma constante fixa de 0.8 — aumentado pra parecer mais um salto de
        // verdade (pedido pelo usuário), igual ao arco usado em ReturnToSpawn/DodgeLeap.
        float jumpHeight = thief.settings != null ? thief.settings.jumpHeight : 2f;
        // Mesma velocidade de ReturnToSpawn/TurnEnd (RuntimeRunSpeed) — pedido pelo usuário pra
        // o "pêndulo" de ida e volta não ficar mais lento que o salto normal de fim de turno.
        float jumpSpeed = thief.RuntimeRunSpeed;

        Vector3 thiefStart  = thief.transform.position;
        Vector3 thiefScale  = thief.transform.localScale;
        Vector3 mountOffset = new Vector3(thief.isPlayer1 ? 0.3f : -0.3f, 0.6f, 0f);
        Vector3 mountPos    = victim.transform.position + mountOffset;

        // Sobe nas costas do adversário — promove o ladrão na sorting layer (igual a um
        // atacante normal) pra renderizar por cima de quem ele está montando.
        SetBodyLayer(thief.bodyRenderers, "Characters");
        SetBodyLayer(victim.bodyRenderers, "Characters2");
        yield return thief.movement.JumpTo(mountPos, jumpSpeed, jumpHeight);

        // Vira pra mesma direção do adversário ao chegar nas costas dele — mesmo mecanismo de
        // flip já usado no projeto (sinal de localScale.x; ver Medieval Warrior Girl na cena,
        // -0.3 pra virar pra esquerda). Restaurado mais abaixo, antes do salto de volta.
        thief.transform.localScale = new Vector3(
            Mathf.Sign(victim.transform.localScale.x) * Mathf.Abs(thiefScale.x), thiefScale.y, thiefScale.z);

        // Balançam juntos 4 vezes, pra frente e pra trás (não mais vertical) e um pouco mais
        // devagar que a primeira versão — a vítima "pisca" a cada ciclo trocando Face 01 por
        // Face 03 (Spriter2UnityDX), tudo pedido pelo usuário. Seta o SpriteRenderer.sprite
        // direto (não TextureController.DisplayedSprite) — esse componente só aplica a troca em
        // Update() quando o Animator não está em transição, o que podia atrasar ou perder a
        // troca; setar o renderer direto garante o "pisca" no mesmo frame.
        Vector3 victimBase = victim.transform.position;
        bool hasFace = victim.faceRenderer != null && victim.faceSprites != null && victim.faceSprites.Length > 2;
        const float bounceDistance = 0.18f;
        const float bounceDuration = 0.18f;
        for (int i = 0; i < 4; i++)
        {
            if (hasFace) victim.faceRenderer.sprite = victim.faceSprites[2]; // Face 03
            float t = 0f;
            while (t < bounceDuration)
            {
                float k = Mathf.Sin((t / bounceDuration) * Mathf.PI) * bounceDistance;
                thief.transform.position  = mountPos   + Vector3.right * k;
                victim.transform.position = victimBase + Vector3.right * k * 0.5f;
                t += Time.deltaTime;
                yield return null;
            }
            if (hasFace) victim.faceRenderer.sprite = victim.faceSprites[0]; // Face 01
        }
        thief.transform.position  = mountPos;
        victim.transform.position = victimBase;

        // Rouba a arma: sai do loadout do adversário e entra no do ladrão — não destrói, igual
        // ao DropWeapon, mas em vez de cair no chão ela troca de dono direto (WeaponHUD dos dois
        // lados atualiza via OnWeaponsChanged, mesmo evento de RemoveCurrentWeapon/AddWeapon).
        victim.weaponHandler.Unequip();
        victim.weaponHandler.loadout?.RemoveCurrentWeapon(data);
        thief.weaponHandler.EquipSpecific(data);
        thief.weaponHandler.loadout?.AddWeapon(data);

        DamagePopup.SpawnDisarm(victim.transform.position + Vector3.up * 1.5f);

        SetBodyLayer(thief.bodyRenderers, thief.defaultSortingLayer);
        SetBodyLayer(victim.bodyRenderers, victim.defaultSortingLayer);

        thief.transform.localScale = thiefScale; // volta a virar pra direção original antes de saltar de volta

        yield return thief.movement.JumpTo(thiefStart, jumpSpeed, jumpHeight);
    }

    // Visual da skill Vampirism: MESMA estrutura de StealWeapon acima (salto nas costas,
    // bounce 4x, vira pra mesma direção do defensor, "pisca" via Face 01/03, salto de volta) —
    // só muda o que acontece durante o bounce (efeito "vampirism" saindo do corpo do defensor
    // em direção à boca do atacante, em vez de só roubar a arma) e a direção do offset de
    // montagem (espelhada — ver abaixo). Dano/cura já foram resolvidos pelo CombatSimulator;
    // esta coroutine só cobre a aproximação/mordida visual. `onBiteComplete` é invocado bem
    // entre o fim do 4º bounce e o salto de volta — CombatPlayer usa esse gancho pra aplicar
    // HP/popups (ApplyHealthDelta centraliza isso, ver CombatPlayer.cs) sem que este método
    // precise conhecer HealthSystem/DamagePopup diretamente.
    public static IEnumerator VampirismRoutine(PlayerCombat attacker, PlayerCombat defender,
                                                RuntimeAnimatorController effectController,
                                                float t, System.Action onBiteComplete)
    {
        float jumpHeight = attacker.settings != null ? attacker.settings.jumpHeight : 2f;
        float jumpSpeed  = attacker.RuntimeRunSpeed;

        Vector3 attackerStart = attacker.transform.position;

        // Offset lateral OPOSTO ao do Thief (StealWeapon usa +0.3 pra P1/-0.3 pra P2) — o
        // atacante sobe nas costas do defensor pelo lado contrário, senão a mordida acontece
        // de frente em vez de por trás (verificado empiricamente com P1 à esquerda/P2 à direita).
        Vector3 mountOffset = new Vector3(attacker.isPlayer1 ? -0.3f : 0.3f, 0.6f, 0f);
        Vector3 mountPos    = defender.transform.position + mountOffset;

        SetBodyLayer(attacker.bodyRenderers, "Characters");
        SetBodyLayer(defender.bodyRenderers, "Characters2");
        yield return attacker.movement.JumpTo(mountPos, jumpSpeed, jumpHeight);

        // Diferente do Thief, o atacante NÃO vira pra direção do defensor aqui — virar deixava
        // ele de costas/encarando errado durante a mordida (bug reportado pelo usuário). Mantém
        // a própria direção original o tempo todo.

        // Efeito "vampirism" (Assets/Data/UI/SkillEffect/Vampirism/, flipbook de 6 frames já em
        // loop no próprio .anim — partículas pequenas 1→2→3, fluxo maior 4→5→6) nasce no CORPO
        // do defensor (não na mão — é de onde o sangue sai) e é movido manualmente em direção à
        // boca do atacante durante o bounce.
        GameObject effect = null;
        if (effectController != null)
        {
            effect = new GameObject("VampirismEffect");
            effect.transform.position = defender.transform.position;
            var effectRenderer = effect.AddComponent<SpriteRenderer>();
            effectRenderer.sortingLayerName = "Characters";
            effectRenderer.sortingOrder     = 25;
            var effectAnimator = effect.AddComponent<Animator>();
            effectAnimator.runtimeAnimatorController = effectController;
        }

        Vector3 defenderBase = defender.transform.position;
        Vector3 mouthPos     = attacker.transform.position + Vector3.up * 0.8f;
        bool hasFace = defender.faceRenderer != null && defender.faceSprites != null && defender.faceSprites.Length > 2;
        const float bounceDistance = 0.18f;
        const float bounceDuration = 0.18f;
        for (int i = 0; i < 4; i++)
        {
            if (hasFace) defender.faceRenderer.sprite = defender.faceSprites[2]; // Face 03
            float elapsed = 0f;
            while (elapsed < bounceDuration * t)
            {
                float p = elapsed / (bounceDuration * t);
                float k = Mathf.Sin(p * Mathf.PI) * bounceDistance;
                attacker.transform.position = mountPos     + Vector3.right * k;
                defender.transform.position = defenderBase + Vector3.right * k * 0.5f;

                // Corpo do defensor -> boca do atacante, durante os 2 primeiros ciclos (i<2) —
                // depois disso o sangue já foi "absorvido"; o efeito só continua tocando o
                // flipbook em loop perto da boca até o 4º bounce terminar.
                if (effect != null)
                {
                    float moveP = Mathf.Clamp01((i + p) / 2f);
                    effect.transform.position = Vector3.Lerp(defenderBase, mouthPos, moveP);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
            if (hasFace) defender.faceRenderer.sprite = defender.faceSprites[0]; // Face 01
        }
        attacker.transform.position = mountPos;
        defender.transform.position = defenderBase;

        if (effect != null) Destroy(effect);

        // Aplica dano/cura/popups (CombatPlayer.ApplyHealthDelta) ANTES do salto de volta — a
        // mordida já terminou, só falta restaurar a pose e voltar pro spawn.
        onBiteComplete?.Invoke();

        SetBodyLayer(attacker.bodyRenderers, attacker.defaultSortingLayer);
        SetBodyLayer(defender.bodyRenderers, defender.defaultSortingLayer);

        yield return attacker.movement.JumpTo(attackerStart, jumpSpeed, jumpHeight);
    }

    // Public so CombatPlayer (CombatSimulator replay) can reuse the same projectile arc
    // for ThrowWeapon events instead of just unequipping with no flight visual.
    public IEnumerator FlyWeapon(Transform obj, Vector3 from, Vector3 to, float duration, bool rotate, float arc = 0f)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t   = elapsed / duration;
            Vector3 p = Vector3.Lerp(from, to, t);
            if (arc > 0f)
                p.y += arc * Mathf.Sin(t * Mathf.PI);
            obj.position = p;
            if (rotate)
                obj.Rotate(0, 0, 540f * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        obj.position = to;
    }

    // Salta para trás ao esquivar: JumpStart animation + arco parabólico na direção oposta ao ataque.
    public IEnumerator DodgeLeap(Vector2 pushDirection, float distance)
    {
        Vector2 to = ClampToArena((Vector2)transform.position + pushDirection * distance);
        float duration = settings.dodgeDuration;
        StartCoroutine(animationController.PlayJumpStart(duration));
        // minDuration=duration: no limite da arena, ClampToArena pode deixar `to` quase igual à
        // posição atual — sem o piso de duração, JumpTo (distance/speed) colapsa pra ~0s e o
        // pulinho nunca chega a tocar, só o "teleporte" instantâneo pro mesmo lugar. Com o piso,
        // o arco (Sin) ainda joga por `duration` inteiro, só sem deslocamento horizontal — o
        // personagem pula no próprio lugar em vez de ficar parado.
        yield return movement.JumpTo(to, distance / duration, 0.4f, minDuration: duration);
        animationController.SetIdle(true);
    }

    // Desliza o personagem na direção pushDirection ao tomar um hit.
    // Iniciado pelo atacante via StartCoroutine para rodar em paralelo com PlayHurt.
    public IEnumerator Knockback(Vector2 pushDirection, float distance, float duration)
    {
        Vector2 from = transform.position;
        Vector2 to   = ClampToArena(from + pushDirection * distance);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector2.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = to;
    }

    // Limite da janela jogável (mesmos valores de RandomSpawnPosition abaixo — área visível
    // da câmera). Combos longos com vários hits/esquivas seguidas empurravam o personagem
    // cada vez mais pra fora desse intervalo, eventualmente saindo da tela.
    private static Vector2 ClampToArena(Vector2 pos)
    {
        pos.x = Mathf.Clamp(pos.x, -7.25f, 7.25f);
        pos.y = Mathf.Clamp(pos.y, -3.90f, -0.81f);
        return pos;
    }

    private IEnumerator ReturnToSpawn()
    {
        // Mesma checagem de CombatPlayer.ExecuteEvent (caso TurnEnd): se o personagem nunca
        // saiu da própria zona de spawn neste turno, não tem por que saltar pra um ponto
        // aleatório novo.
        if (InSpawnZone(transform.position))
        {
            animationController.SetIdle(true);
            yield break;
        }
        yield return animationController.PlayJumpStart(settings.jumpStartDuration);
        spawnPosition = RandomSpawnPosition();
        yield return movement.JumpTo(spawnPosition, RuntimeRunSpeed, settings.jumpHeight);
        animationController.SetIdle(true);
    }

    private Vector2 RandomSpawnPosition()
    {
        float x = isPlayer1 ? Random.Range(-7.25f, -4.79f) : Random.Range(4.79f, 7.25f);
        float y = Random.Range(-3.90f, -0.81f);
        return new Vector2(x, y);
    }

    private bool InSpawnZone(Vector2 pos)
    {
        float xMin = isPlayer1 ? -7.25f : 4.79f;
        float xMax = isPlayer1 ? -4.79f : 7.25f;
        return pos.x >= xMin && pos.x <= xMax && pos.y >= -3.90f && pos.y <= -0.81f;
    }

    // --- Skill queries ---

    public bool HasSkill(string skillName)
    {
        if (skills == null) return false;
        foreach (var s in skills)
            if (s != null && s.skillName == skillName) return true;
        return false;
    }

    public SkillData GetSkill(string skillName)
    {
        if (skills == null) return null;
        foreach (var s in skills)
            if (s != null && s.skillName == skillName) return s;
        return null;
    }

    // No-op kept for call-site compatibility (logging removed project-wide).
    public void LogSkillCheck(string skillName, bool triggered, string detail = "") { }

    // Bug 2 (sorting) — ordem final pedida pelo usuário (fundo → frente):
    //   Background < Arma do atacante < Corpo do atacante < Arma do defensor < Corpo do defensor
    // Cada personagem: própria arma sempre atrás do próprio corpo. Cruzado: arma do atacante
    // nunca sobrepõe nada do defensor (fica atrás até do corpo dele); arma do defensor fica
    // na frente do atacante inteiro (corpo+arma), mas ainda atrás do próprio corpo.
    // Mapeada nas 4 sorting layers do projeto por ordem de prioridade fixa (Default < Weapons2
    // < Characters2 < Weapons < Characters, ver CLAUDE.md — Weapons2 é a mais atrás das 4,
    // Characters a mais à frente):
    //   Arma do atacante → Weapons2 (mais atrás)     Corpo do atacante → Characters2
    //   Arma do defensor → Weapons                    Corpo do defensor → Characters (mais à frente)
    // Note que "Characters"/"Characters2" agora identificam DEFENSOR/ATACANTE (não mais
    // atacante/defensor como antes) — só importa qual layer cada um ocupa em cada turno, os
    // outros sistemas que reusam essas 2 layers (ghost trail da Fierce Brute, pets) não dependem
    // de qual papel (atacante/defensor) está em qual layer, só de onde as sprites de personagem/
    // arma realmente estão a cada momento. Chamado por CombatPlayer.ExecuteEvent nos casos
    // TurnStart/TurnEnd (path ativo do simulador) — antes só existia em AttackRoutine/legado,
    // nunca executado de verdade enquanto CombatSceneLoader.useSimulator=true (default).
    public void SetAttackerLayers()
    {
        SetBodyLayer(bodyRenderers, "Characters2");
        SetWeaponLayer(weaponHandler, "Weapons2");

        if (defender != null)
        {
            SetBodyLayer(defender.bodyRenderers, "Characters");
            SetWeaponLayer(defender.weaponHandler, "Weapons");
        }
    }

    public void RestoreDefaultLayers()
    {
        SetBodyLayer(bodyRenderers, defaultSortingLayer);
        if (defender != null)
            SetBodyLayer(defender.bodyRenderers, defaultSortingLayer);
    }

    private static void SetBodyLayer(List<SpriteRenderer> renderers, string layerName)
    {
        foreach (var sr in renderers)
            sr.sortingLayerName = layerName;
    }

    private static void SetWeaponLayer(WeaponHandler handler, string layerName)
    {
        var weapon = handler.CurrentWeapon;
        if (weapon == null) return;
        weapon.GetComponent<SpriteRenderer>().sortingLayerName = layerName;
    }
}
