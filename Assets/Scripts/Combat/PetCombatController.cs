using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Adicionado via AddComponent ao pet instanciado em CombatSceneLoader (mesmo padrão de
// PlayerCombat, mas bem mais simples — sem armas/skills, só correr/atacar/cair). Movimentação
// reusa o MovementController genérico (já existente, sem acoplamento a PlayerCombat — ver
// CLAUDE.md/MovementController.cs).
public class PetCombatController : MonoBehaviour
{
    // Pets caídos NÃO são destruídos (preparação pra skill futura Tamer, que "come" pets
    // caídos) — rastreados aqui mesmo padrão de PlayerCombat.fallenWeapons, drenado em
    // CleanupDeadPets() (chamado por AttackSequencer.OnCombatEnd, junto de CleanupFallenWeapons).
    private static readonly List<PetCombatController> deadPets = new List<PetCombatController>();

    public PetAnimationController animController;
    public MovementController movement;
    public SpriteRenderer[] bodyRenderers;
    public HealthBarPet healthBar;

    // Setado por CombatSceneLoader.SpawnPets na instanciação — usado por CombatPlayer pra
    // resolver PetState.SlashDuration(type) (duração do swing varia por tipo, ex: Boar 0.85s).
    public PetType petType;

    // HP máximo do pet — cacheado aqui (setado por CombatSceneLoader na instanciação) porque
    // CombatPlayer só recebe newHp/newTargetHp nos eventos, nunca o máximo de novo; o máximo
    // nunca muda durante a luta, então não precisa vir em todo evento.
    public int maxHp;

    // Posição de spawn do pet — capturada de fora (CombatSceneLoader) depois que o EntryFall
    // pousa, mesmo padrão de PlayerCombat.spawnPosition (Vector2, não Transform — não há
    // necessidade de um GameObject marcador só pra guardar uma posição fixa). Antes era fixa a
    // vida inteira da luta; agora CombatPlayer reatribui um novo ponto aleatório aqui antes de
    // cada retorno do pet (ver CombatPlayer.RollPetSpawnPosition), igual ao
    // PlayerCombat.TurnEnd/RandomSpawnPosition dos personagens (pedido do usuário).
    public Vector2 spawnPosition;

    // Lado do pet (Player1 ou Player2) — setado por CombatSceneLoader.SpawnPets, usado por
    // CombatPlayer.RollPetSpawnPosition pra sortear dentro da mesma faixa de X do spawn
    // original (-5 a -1 pro P1, 1 a 5 pro P2).
    public bool isPlayer1;

    // Glow dourado do Treat — criado em CombatPlayer.TreatFeed, destruído ao absorver um golpe
    // ou quando o pet morre. Null quando o pet não está com escudo ativo.
    public GameObject shieldVisual;

    private float _initialForwardSign = 1f;

    // Pequeno offset de Z (não de sortingLayer) usado pra profundidade visual contra o
    // personagem adversário — ver CombatPlayer.UpdatePetDepthSorting. Tentativa anterior
    // (sortingLayerName Characters/Characters2) não funcionava porque os personagens, fora do
    // instante de um golpe (SetAttackerLayers), ficam na layer "Default"/sortingOrder 0 (não
    // "Characters") — promover o pet pra "Characters2" (acima de Default) deixava ele SEMPRE
    // na frente, nas duas direções, já que "Characters2" também é maior que "Default". Como o
    // projeto já usa Transparency Sort Mode = Default numa câmera ortográfica (ordena por
    // distância no eixo de visão = Z aqui, ver CLAUDE.md), um pequeno deslocamento de Z dentro
    // da MESMA layer ("Default", a do personagem) resolve o empate de forma confiável sem
    // tocar no sistema de promoção de layer dos personagens. Câmera fica em Z=-10 olhando pra
    // +Z, então Z menor (mais negativo) = mais perto da câmera = renderiza na FRENTE.
    private const float DepthZOffset = 0.05f;

    // Aplicado TODO frame (não só quando muda) porque MovementController.MoveTo/JumpTo
    // reatribui `transform.position` a partir de um Vector2 a cada passo de movimento — a
    // conversão implícita Vector2→Vector3 zera Z de volta pra 0 enquanto o pet corre/pula,
    // desfazendo o offset; reaplicar todo Update garante que o offset volte no frame seguinte.
    public void SetDepthLayer(bool inFront)
    {
        var pos = transform.position;
        float targetZ = inFront ? -DepthZOffset : DepthZOffset;
        if (pos.z == targetZ) return;
        pos.z = targetZ;
        transform.position = pos;
    }

    private void Awake()
    {
        animController = GetComponent<PetAnimationController>();
        if (animController == null) animController = gameObject.AddComponent<PetAnimationController>();

        movement = GetComponent<MovementController>();
        if (movement == null) movement = gameObject.AddComponent<MovementController>();

        if (bodyRenderers == null || bodyRenderers.Length == 0)
            bodyRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        _initialForwardSign = Mathf.Sign(transform.localScale.x == 0 ? 1f : transform.localScale.x);
    }

    public IEnumerator RunToTarget(Vector3 target, float speed)
    {
        yield return movement.MoveTo(target, speed);
    }

    // Volta ao spawn em pêndulo parabólico (mesmo MovementController.JumpTo usado pelo
    // jump-back dos personagens no TurnEnd) — não é mais uma corrida linear como RunToTarget.
    public IEnumerator ReturnToSpawn(float speed, float jumpHeight)
    {
        yield return movement.JumpTo(spawnPosition, speed, jumpHeight);
    }

    // Esquiva com movimento real — pêndulo pra trás (igual PlayerCombat.DodgeLeap), em vez de só
    // tocar o trigger "Jumping" no lugar (bug real reportado pelo usuário: pet "esquivava"
    // parado, sem nenhum deslocamento). Altura maior (0.6 vs 0.4 do personagem, pedido explícito
    // "pular mais alto") e distância menor (0.35 vs 0.5 do personagem — pet é menor, recua menos).
    private const float DodgeDistance = 0.35f;
    private const float DodgeHeight   = 0.6f;
    private const float DodgeDuration = 0.2f;

    public IEnumerator DodgeLeap(Vector2 pushDirection, float t = 1f)
    {
        Vector2 to = (Vector2)transform.position + pushDirection * DodgeDistance;
        animController.PlayJump();
        yield return movement.JumpTo(to, (DodgeDistance / DodgeDuration) * t, DodgeHeight);
    }

    // Vira em direção ao alvo — mesmo mecanismo de flip já existente no projeto (sinal de
    // localScale.x; ver Medieval Warrior Girl/Thief/Vampirism no CLAUDE.md).
    public void FlipToward(Vector3 target)
    {
        float sign = Mathf.Sign(target.x - transform.position.x);
        if (sign == 0f) return;
        var scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * sign;
        transform.localScale = scale;
    }

    // Define a direção de repouso do pet (e já aplica no localScale) — chamado por
    // CombatSceneLoader depois do Instantiate, já que _initialForwardSign é capturado
    // no Awake com o valor do prefab antes de qualquer flip externo.
    public void SetInitialFacing(float forwardSign)
    {
        _initialForwardSign = forwardSign;
        var scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * _initialForwardSign;
        transform.localScale = scale;
    }

    // Restaura a direção inicial (a que o pet tinha antes deste ataque) — chamado antes de
    // correr de volta ao spawn.
    public void FlipToInitial()
    {
        var scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * _initialForwardSign;
        transform.localScale = scale;
    }

    // Sequência completa de ataque do pet: corre até o alvo, ataca, e volta ao spawn em
    // pêndulo. `onImpact` é invocado uma única vez no momento de impacto/esquiva — quem chama
    // (CombatPlayer) já sabe se foi hit ou dodge (via CombatEvent) e decide ali dentro: aplicar
    // dano + popup + Hurt no alvo (hit) ou PlayJump + SpawnDodge no alvo (dodge). Knockback do
    // alvo (0.3f, metade do normal) também é responsabilidade do chamador, que tem a
    // referência do alvo.
    public IEnumerator PlayAttackSequence(Vector3 target, bool isDodged, int damage, System.Action onImpact, float runSpeed = 6f, float comboDelay = 0.2f, float jumpHeight = 1.2f)
    {
        FlipToward(target);
        animController.PlayRun(true);
        yield return RunToTarget(target, runSpeed);

        animController.PlayRun(false);
        animController.PlaySlash();
        yield return new WaitForSeconds(comboDelay);

        onImpact?.Invoke();

        yield return new WaitForSeconds(comboDelay);

        // Retorno em pêndulo (mesmo padrão visual do jump-back dos personagens no TurnEnd) —
        // vira pra direção do spawn (de costas pro alvo que acabou de atacar), salta em arco
        // com a animação Jumping, e só ao pousar restaura a direção de descanso original
        // (FlipToInitial) — sem isso o pet ficava de costas indefinidamente depois do 1º ataque.
        FlipToward(spawnPosition);
        animController.PlayJump();
        yield return ReturnToSpawn(runSpeed, jumpHeight);

        animController.SetIdle(true);
        FlipToInitial();
    }

    public void PlayDeath()
    {
        animController.PlayDying();
        animController.DisableAfterDying();
        if (healthBar != null) healthBar.FadeOutAndDestroy(2f);
        if (!deadPets.Contains(this)) deadPets.Add(this);
    }

    // Chamado por AttackSequencer.OnCombatEnd, junto de PlayerCombat.CleanupFallenWeapons() —
    // diferente das armas caídas, os pets NÃO são destruídos aqui (preparação pra Tamer), só
    // a lista estática é limpa pra não vazar entre lutas.
    public static void CleanupDeadPets()
    {
        deadPets.Clear();
    }
}
