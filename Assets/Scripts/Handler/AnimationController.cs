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

    public IEnumerator PlayHurt(float duration)
    {
        anim.SetTrigger("Hurt");
        yield return new WaitForSeconds(duration);
    }

    public void SetIdle(bool state) => anim.SetBool("Idle", state);
}
