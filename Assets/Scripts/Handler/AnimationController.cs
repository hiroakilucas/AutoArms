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
    public void SetSpeed(float speed) => anim.speed = speed;
}
