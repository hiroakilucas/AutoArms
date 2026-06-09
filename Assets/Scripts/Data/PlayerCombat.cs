using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Componentes de Apoio")]
    public WeaponHandler weaponHandler;
    public MovementController movement;
    public AnimationController animationController;

    [Header("Configurações de Ataque")]
    public AttackSettings settings;

    [Header("Identificação")]
    [Tooltip("Marcar verdadeiro para Player1, falso para Player2")]
    public bool isPlayer1;

    [Header("Defensor")]
    public PlayerCombat defender;
    public AnimationController defenderAnimationController;

    private Animator animator;
    private List<SpriteRenderer> allRenderers;

    // para restaurar após o turno
    private string defaultCharLayer;
    private int defaultCharOrder;

    // posição inicial e alvo dinâmicos
    private Vector2 initialPosition;
    private Vector2 rawTargetPos, attackTargetPos;



    private void Awake()
    {
        animator = GetComponent<Animator>();
        weaponHandler = GetComponent<WeaponHandler>();

        allRenderers = new List<SpriteRenderer>(GetComponentsInChildren<SpriteRenderer>());
        if (allRenderers.Count > 0)
        {
            defaultCharLayer = allRenderers[0].sortingLayerName;
            defaultCharOrder = allRenderers[0].sortingOrder;
        }
    }

    private void Start()
    {
        EquipAndSpawn();

        // spawn aleatório conforme jogador
        if (isPlayer1)
            initialPosition = new Vector2(
                Random.Range(-7.25f, -4.79f),
                Random.Range(-3.90f, -0.81f)
            );
        else
            initialPosition = new Vector2(
                Random.Range(7.25f, 4.79f),
                Random.Range(-3.90f, -0.81f)
            );

        transform.position = initialPosition;

        animationController.SetIdle(true);
    }

    public IEnumerator AttackRoutine()
    {
        // 1) define posição "crua" do defensor
        rawTargetPos = defender != null
            ? (Vector2)defender.transform.position
            : initialPosition;

        // 2) Ajusta sorting layers do personagem e da arma
        // Atacante
        SetCharacterLayerAndOrder(allRenderers, "Characters");
        if (weaponHandler.CurrentWeapon != null)
        {
            var srAtt = weaponHandler.CurrentWeapon.GetComponent<SpriteRenderer>();
            srAtt.sortingLayerName = "Weapons";
        }
        // Defensor
        if (defender != null)
        {
            SetCharacterLayerAndOrder(defender.allRenderers, "Characters2");
            if (defender.weaponHandler.CurrentWeapon != null)
            {
                var srDef = defender.weaponHandler.CurrentWeapon.GetComponent<SpriteRenderer>();
                srDef.sortingLayerName = "Weapons2";
            }
        }
        yield return null; // aplica sorting

        //weaponHandler.CurrentWeapon
        // 3) calcula tamanho por tipo de arma
        float reach = weaponHandler.currentType switch
        {
            WeaponType.Dagger => 1.5f,
            WeaponType.Heavy => 2.8f,
            _ => 2.0f  // Sword
        };
        // direção até o defensor e posição de ataque ajustada
        Vector2 dir = (rawTargetPos - (Vector2)transform.position).normalized;
        attackTargetPos = rawTargetPos - dir * reach;

        // 4) Idle e Run até o ponto de ataque
        yield return animationController.PlayIdle(settings.idleDuration);
        yield return animationController.PlayRun(attackTargetPos, settings.runSpeed, movement);

        // 5) Slashing e sincronização do Hurt na metade
        string trigger = weaponHandler.currentType switch
        {
            WeaponType.Heavy => "SlashingHeavy",
            WeaponType.Dagger => "SlashingDagger",
            _ => "Slashing"
        };
        animator.SetTrigger(trigger);
        // metade da duração do slashing
        yield return new WaitForSeconds(settings.slashingDuration * 0.5f);
        //Hurt do defensor
        if (defenderAnimationController != null)
            yield return defenderAnimationController.PlayHurt(settings.hurtDuration);
        // restante do slashing
        yield return new WaitForSeconds(settings.slashingDuration * 0.5f);

        // 6) Delay antes do salto
        yield return new WaitForSeconds(settings.slashingToJumpDelay);

        // 7) JumpStart e salto de volta
        yield return animationController.PlayJumpStart(settings.jumpStartDuration);
        // spawn aleatório conforme jogador
        if (isPlayer1)
            initialPosition = new Vector2(
                Random.Range(-7.25f, -4.79f),
                Random.Range(-3.90f, -0.81f)
            );
        else
            initialPosition = new Vector2(
                Random.Range(7.25f, 4.79f),
                Random.Range(-3.90f, -0.81f)
            );
        yield return movement.JumpTo(
            attackTargetPos,    // ponto de partida do salto
            initialPosition,    // destino aleatório
            settings.runSpeed,
            settings.jumpHeight
        );

        // 8) Restaura renderização (alterna a ordem da camada de renderização)
        SetCharacterLayerAndOrder(allRenderers, defaultCharLayer);
        if (defender != null)
            SetCharacterLayerAndOrder(defender.allRenderers, defaultCharLayer);

        // 9) Idle final e reposiciona para próxima rodada
        animationController.SetIdle(true);
        //EquipAndSpawn();
    }
    private void EquipAndSpawn()
    {
        weaponHandler.EquipNext();
        SetWeaponLayerAndOrder(weaponHandler,
            isPlayer1 ? "Weapons" : "Weapons2"
        );
    }

    private void SetCharacterLayerAndOrder(List<SpriteRenderer> rends, string layerName)
    {
        foreach (var sr in rends)
        {
            sr.sortingLayerName = layerName;
        }
    }

    private void SetWeaponLayerAndOrder(WeaponHandler handler, string layerName)
    {
        var w = handler.CurrentWeapon;
        if (w == null) return;
        var sr = w.GetComponent<SpriteRenderer>();
        sr.sortingLayerName = layerName;
    }
}
