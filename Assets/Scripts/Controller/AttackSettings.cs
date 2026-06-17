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
    [Tooltip("Duração do salto de esquiva (DodgeLeap) — separado de hurtDuration para o salto ficar visível")]
    public float dodgeDuration = 0.25f;
    [Tooltip("Quando, dentro do Slashing, o defensor leva o golpe (em segundos)")]
    public float hurtTriggerDelay = 0.25f;
    [Tooltip("Distância que o defensor recua ao tomar um hit")]
    public float knockbackDistance = 0.5f;
    [Tooltip("Pausa entre cada ação de um combo (hit/esquiva/bloqueio/erro) — separado de interTurnDelay, que só se aplica entre turnos")]
    public float comboDelay = 0.15f;
    public Vector2 startPos;
    public Vector2 targetPos;
}
