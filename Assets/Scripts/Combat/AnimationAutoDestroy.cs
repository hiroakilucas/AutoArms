using UnityEngine;

// Destroys this GameObject once its Animator clip finishes playing. Used by one-shot VFX
// prefabs (ex: Piledriver explosion) that have no other cleanup logic — the clip's Animator
// Controller must have Loop Time = false, otherwise the length read here would be the full
// looped duration instead of a single playthrough.
public class AnimationAutoDestroy : MonoBehaviour
{
    private void Start()
    {
        var anim = GetComponent<Animator>();
        var clips = anim != null ? anim.runtimeAnimatorController?.animationClips : null;
        float length = clips != null && clips.Length > 0 ? clips[0].length : 1f;
        Destroy(gameObject, length);
    }
}
