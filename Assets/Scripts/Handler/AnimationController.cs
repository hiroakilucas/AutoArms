using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Animator))]
public class AnimationController : MonoBehaviour
{
    private Animator anim;

    private void Awake() => anim = GetComponent<Animator>();

    public IEnumerator PlayIdle(float duration)
    {
        anim.SetBool("Idle", true);
        yield return new WaitForSeconds(duration);
        anim.SetBool("Idle", false);
    }

    public IEnumerator PlayRun(Vector2 target, float speed, MovementController mover)
    {
        anim.SetBool("Running", true);
        yield return mover.MoveTo(target, speed);
        anim.SetBool("Running", false);
    }

    public IEnumerator PlayJumpStart(float duration)
    {
        anim.SetBool("JumpStart", true);
        yield return new WaitForSeconds(duration);
        anim.SetBool("JumpStart", false);
    }

    public IEnumerator PlayBlock(float duration)
    {
        anim.SetTrigger("Blocking");
        yield return new WaitForSeconds(duration);
    }

    public IEnumerator PlayCatchWeapon(float duration)
    {
        anim.ResetTrigger("Hurt");
        anim.SetBool("Idle", false);
        anim.SetTrigger("CatchWeapon");
        yield return new WaitForSeconds(duration);
    }

    public IEnumerator PlayHurt(float duration)
    {
        anim.SetTrigger("Hurt");
        yield return new WaitForSeconds(duration);
    }

    // Fora de combate (ex: preview de personagem em 02_SelectCharacter reagindo a clique) não
    // existe um TurnEnd depois pra sair do estado Slashing — a única transição de saída dele é o
    // toggle do bool JumpStart (ver "Bug de animação travada" no CLAUDE.md), então replicamos só
    // esse toggle aqui em vez de PlayJumpStart (que tocaria o hop de verdade, deslocando o
    // personagem visualmente sem nenhum PlayerCombat pra reposicionar depois).
    public IEnumerator PlaySlash(float duration)
    {
        anim.SetTrigger("Slashing");
        yield return new WaitForSeconds(duration);
        anim.SetBool("JumpStart", true);
        yield return null;
        anim.SetBool("JumpStart", false);
    }

    // Corte direto pro estado Idle, sem transição/blend nenhuma — usado quando o personagem
    // precisa "sair" do Slashing pra receber um trigger que só tem transição de entrada a partir
    // do Idle (ex: Hurt, ver CombatPlayer case Reversal) mas não pode mostrar NENHUMA animação
    // de pulo antes (diferente de PlayJumpStart, que toca o Jump Start de verdade e precisa de
    // tempo real de transição — insuficiente com durações curtas, causava o Hurt disparando "no
    // meio do pulo" em vez de imediatamente, bug real reportado pelo usuário). Animator.Play(...)
    // força o estado sem depender de nenhuma transição configurada no Controller.
    public void ForceIdleState()
    {
        anim.SetBool("Idle", true);
        anim.SetBool("Running", false);
        anim.Play("Idle", 0, 0f);
    }

    // Força Running=false junto de Idle=true — Medieval Warrior.controller tinha os dois bools
    // (Idle e Running) com m_DefaultBool: 1 (true) simultaneamente (Assassin Guy/Medieval
    // Warrior Girl já tinham os dois corretos em 0/false); sem nada zerando Running explicitamente,
    // o personagem ficava com a animação de corrida tocando "no lugar" desde o EntryFall até o
    // primeiro PlayRun de verdade zerar o bool no fim do run (bug real reportado pelo usuário:
    // "desce correndo no mesmo lugar" antes de avançar pro adversário). Corrigido o default no
    // .controller, mas mantém essa guarda aqui pra não depender de nenhum Animator Controller
    // futuro ter os defaults certos.
    public void SetIdle(bool state)
    {
        anim.SetBool("Idle", state);
        if (state) anim.SetBool("Running", false);
    }
    public void PlayDying()
    {
        anim.SetBool("Idle", false);
        anim.SetBool("Running", false);
        anim.SetTrigger("Dying");
    }

    public void SetSpeed(float speed) => anim.speed = speed;
}
