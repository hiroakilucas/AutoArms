using System.Collections;
using UnityEngine;

// Spawns blood splatter animations at hit positions.
// Add this component to a GameObject in 04_CombatScenePVP and wire the three
// AnimatorControllers (hit1, hit2, hitcrit) and the BloodEffect prefab in the Inspector.
// "Effects" sorting layer must exist in Project Settings → Tags and Layers.
public class BloodEffectPlayer : MonoBehaviour
{
    private static BloodEffectPlayer _instance;

    [SerializeField] private GameObject bloodEffectPrefab;
    [SerializeField] private RuntimeAnimatorController hit1Controller;
    [SerializeField] private RuntimeAnimatorController hit2Controller;
    [SerializeField] private RuntimeAnimatorController hitCritController;

    private const float EffectLifetime = 0.2f;

    private void Awake() => _instance = this;

    public static void PlayHit(Vector3 worldPosition)
    {
        if (_instance == null) return;
        var ctrl = Random.value < 0.5f ? _instance.hit1Controller : _instance.hit2Controller;
        _instance.StartCoroutine(_instance.Spawn(worldPosition, ctrl));
    }

    public static void PlayCrit(Vector3 worldPosition)
    {
        if (_instance == null) return;
        _instance.StartCoroutine(_instance.Spawn(worldPosition, _instance.hitCritController));
    }

    private IEnumerator Spawn(Vector3 position, RuntimeAnimatorController controller)
    {
        if (controller == null) yield break;

        GameObject go;
        if (bloodEffectPrefab != null)
        {
            go = Instantiate(bloodEffectPrefab, position, Quaternion.identity);
        }
        else
        {
            go = new GameObject("BloodEffect");
            go.transform.position = position;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<Animator>();
        }

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = "Effects";
            sr.sortingOrder     = 10;
        }

        var anim = go.GetComponent<Animator>();
        if (anim != null)
            anim.runtimeAnimatorController = controller;

        yield return new WaitForSeconds(EffectLifetime);
        if (go != null) Destroy(go);
    }
}
