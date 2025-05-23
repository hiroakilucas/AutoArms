using UnityEngine;

[CreateAssetMenu(menuName = "Game/Attack Settings")]
public class AttackSettings : ScriptableObject
{
    public float idleDuration = 0.3f;
    public float runSpeed = 25f;
    public float slashingDuration = 0.5f;
    public float slashingToJumpDelay = 0.2f;
    public float jumpStartDuration = 0.1f;
    public float jumpHeight = 2f;
    public float hurtDuration = 0.1f;
    [Tooltip("Quando, dentro do Slashing, o defensor leva o golpe (em segundos)")]
    public float hurtTriggerDelay = 0.25f;
    public Vector2 startPos;
    public Vector2 targetPos;
}
