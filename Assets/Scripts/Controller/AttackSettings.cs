using UnityEngine;

[CreateAssetMenu(menuName = "Game/Attack Settings")]
public class AttackSettings : ScriptableObject
{
    public float idleDuration = 0.3f;
    public float runSpeed = 35f;
    public float slashingDuration = 0.5f;
    public float slashingToJumpDelay = 0.2f;
    public float jumpStartDuration = 0.1f;
    public float jumpHeight = 1f;
    public float hurtDuration = 0.1f;
    [Tooltip("Quando, dentro do Slashing, o defensor leva o golpe (em segundos)")]
    public float hurtTriggerDelay = 0.25f;
    [Range(0f, 1f), Tooltip("Probabilidade de executar um golpe extra (combo) após o ataque principal")]
    public float comboChance = 0.3f;
    [Tooltip("Distância que o defensor recua ao tomar um hit")]
    public float knockbackDistance = 0.5f;
    public Vector2 startPos;
    public Vector2 targetPos;
}
