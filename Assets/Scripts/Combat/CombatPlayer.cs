using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Replays a pre-calculated event list from CombatSimulator as animation.
// Bridges the pure-logic simulator with the existing MonoBehaviour components.
public class CombatPlayer : MonoBehaviour
{
    [HideInInspector] public PlayerCombat    p1Combat;
    [HideInInspector] public PlayerCombat    p2Combat;
    [HideInInspector] public AttackSequencer sequencer;

    private List<CombatEvent> _events;
    private float             _playbackSpeed = 1f;
    private bool              _is2x = false;
    private bool              _skipRequested;
    private HealthSystem      _h1, _h2;

    // Called by CombatSceneLoader after both players have landed.
    public void PlayCombat(List<CombatEvent> events)
    {
        _events = events;
        _h1     = p1Combat.GetComponent<HealthSystem>();
        _h2     = p2Combat.GetComponent<HealthSystem>();
        StartCoroutine(PlayEvents());
    }

    // Toggles between 1x and 2x playback. Returns the new state (true = now at 2x).
    public bool ToggleSpeed()
    {
        if (!_is2x)
        {
            _playbackSpeed = 2f;
            _is2x = true;
        }
        else
        {
            _playbackSpeed = 1f;
            _is2x = false;
        }
        return _is2x;
    }

    public void RequestSkip()
    {
        _skipRequested = true;
    }

    private IEnumerator PlayEvents()
    {
        foreach (var evt in _events)
        {
            if (_skipRequested)
            {
                // Fast-forward: apply all remaining HealthChanged events, then fire CombatEnd
                foreach (var remaining in _events)
                {
                    if (remaining.type == CombatEventType.HealthChanged)
                        ApplyHealthChanged(remaining);
                }
                var endEvt = _events.FindLast(e => e.type == CombatEventType.CombatEnd);
                if (endEvt != null) TriggerCombatEnd(endEvt);
                yield break;
            }

            yield return StartCoroutine(ExecuteEvent(evt));
        }
    }

    private IEnumerator ExecuteEvent(CombatEvent evt)
    {
        var attacker = GetCombat(evt.playerIndex);
        var defender = GetCombat(evt.targetIndex);
        float t = 1f / _playbackSpeed;

        switch (evt.type)
        {
            case CombatEventType.TurnStart:
                yield return null;
                break;

            case CombatEventType.RunToDefender:
                if (attacker != null && defender != null)
                {
                    Vector2 attackPos = CalcAttackPosition(attacker, defender);
                    yield return StartCoroutine(
                        attacker.animationController.PlayRun(attackPos, (attacker.settings?.runSpeed ?? 35f) * t, attacker.movement));
                }
                break;

            case CombatEventType.PickupWeapon:
                if (attacker != null)
                {
                    attacker.weaponHandler.EquipRandom();
                    yield return StartCoroutine(attacker.animationController.PlayCatchWeapon(0.6f * t));
                }
                break;

            case CombatEventType.WeaponEquipped:
                if (attacker != null)
                    attacker.weaponHandler.EquipRandom();
                yield return null;
                break;

            case CombatEventType.Hit:
                if (defender != null)
                {
                    // Play slash animation on attacker, hurt on defender
                    string trigger = attacker?.weaponHandler.currentType switch
                    {
                        WeaponType.Heavy  => "SlashingHeavy",
                        WeaponType.Dagger => "SlashingDagger",
                        _                 => "Slashing"
                    };
                    attacker?.GetComponent<Animator>()?.SetTrigger(trigger);

                    float slashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t;
                    yield return new WaitForSeconds(slashHalf);

                    // Knockback in parallel with hurt
                    Vector2 pushDir = ComputePushDir(attacker, defender);
                    float   kbDist  = attacker?.settings?.knockbackDistance ?? 0.5f;
                    float   kbDur   = attacker?.settings?.hurtDuration ?? 0.07f;
                    if (defender != null)
                    {
                        StartCoroutine(defender.Knockback(pushDir, kbDist, kbDur * t));
                        yield return StartCoroutine(defender.animationController.PlayHurt(kbDur * t));
                    }

                    // Damage popup
                    if (defender != null)
                    {
                        Vector3 popupPos = defender.transform.position + Vector3.up * 1.5f
                            + Vector3.right * Random.Range(-0.3f, 0.3f);
                        DamagePopup.Spawn(popupPos, evt.damage, evt.isCrit);
                    }
                }
                break;

            case CombatEventType.HealthChanged:
                ApplyHealthChanged(evt);
                yield return null;
                break;

            case CombatEventType.Dodge:
                if (defender != null)
                {
                    Vector3 dodgePopupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnDodge(dodgePopupPos);
                    Vector2 dodgeDir = ComputePushDir(attacker, defender);
                    float   dodgeDist = attacker?.settings?.knockbackDistance ?? 0.5f;
                    yield return StartCoroutine(defender.DodgeLeap(dodgeDir, dodgeDist));
                }
                break;

            case CombatEventType.Block:
                if (defender != null)
                {
                    Vector3 blockPopupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnBlock(blockPopupPos);

                    float kbDist = (attacker?.settings?.knockbackDistance ?? 0.5f) * 0.5f;
                    float kbDur  = (attacker?.settings?.hurtDuration ?? 0.07f) * t;
                    Vector2 blockDir = ComputePushDir(attacker, defender);

                    // Block animation and knockback in parallel
                    StartCoroutine(defender.Knockback(blockDir, kbDist, kbDur));
                    yield return StartCoroutine(defender.animationController.PlayBlock(0.36666667f * t));
                }
                break;

            case CombatEventType.Miss:
                if (defender != null)
                {
                    Vector3 missPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnMiss(missPos);
                    Vector2 missDir = ComputePushDir(attacker, defender);
                    float missDist = attacker?.settings?.knockbackDistance ?? 0.5f;
                    yield return StartCoroutine(defender.DodgeLeap(missDir, missDist));
                }
                break;

            case CombatEventType.Disarm:
                if (defender != null)
                {
                    Vector3 disarmPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnDisarm(disarmPos);
                    defender.weaponHandler.UnequipPermanent();
                }
                yield return null;
                break;

            case CombatEventType.WeaponDrop:
                if (attacker != null)
                {
                    Vector3 dropPos = attacker.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnDrop(dropPos);
                    attacker.weaponHandler.UnequipPermanent();
                }
                yield return null;
                break;

            case CombatEventType.ThrowWeapon:
                // Simplified: unequip and wait for flight duration
                if (attacker != null)
                {
                    attacker.weaponHandler.Unequip();
                    attacker.GetComponent<Animator>()?.SetTrigger("Throwing");
                }
                yield return new WaitForSeconds(0.45f * t);
                break;

            case CombatEventType.SpeedBonus:
                for (int i = 0; i < evt.extraActions; i++)
                    DamagePopup.SpawnRapido((attacker?.transform.position ?? Vector3.zero) + Vector3.up * 2f);
                yield return null;
                break;

            case CombatEventType.TurnEnd:
                // Return attacker to spawn (jump-back) — mirrors ReturnToSpawn
                if (attacker != null)
                {
                    float jsDur = attacker.settings?.jumpStartDuration ?? 0.02f;
                    float jh    = attacker.settings?.jumpHeight ?? 2f;
                    float spd   = (attacker.settings?.runSpeed ?? 35f) * t;
                    Vector2 spawnPos = RandomSpawnPos(attacker.isPlayer1);
                    yield return StartCoroutine(attacker.animationController.PlayJumpStart(jsDur * t));
                    yield return StartCoroutine(attacker.movement.JumpTo(spawnPos, spd, jh));
                    attacker.animationController.SetIdle(true);
                }
                break;

            case CombatEventType.CombatEnd:
                TriggerCombatEnd(evt);
                yield return null;
                break;

            default:
                yield return null;
                break;
        }
    }

    // --- Helpers ---

    private PlayerCombat GetCombat(int index) => index == 0 ? p1Combat : p2Combat;

    private void ApplyHealthChanged(CombatEvent evt)
    {
        var hs = evt.playerIndex == 0 ? _h1 : _h2;
        if (hs == null) return;
        int delta = hs.CurrentHealth - evt.newHp;
        if (delta > 0) hs.TakeDamage(delta);
    }

    private void TriggerCombatEnd(CombatEvent evt)
    {
        if (sequencer != null)
        {
            PlayerCombat winner = evt.playerIndex == 0 ? p1Combat : p2Combat;
            sequencer.OnCombatEnd(winner);
        }
    }

    private static Vector2 CalcAttackPosition(PlayerCombat attacker, PlayerCombat defender)
    {
        float reach = attacker.weaponHandler.CurrentWeapon == null ? 0.8f :
            attacker.weaponHandler.currentType switch
            {
                WeaponType.Dagger => 1.5f,
                WeaponType.Heavy  => 2.8f,
                _                 => 2.0f
            };
        Vector2 defPos = defender.transform.position;
        Vector2 attPos = attacker.transform.position;
        Vector2 dir    = (defPos - attPos).normalized;
        return defPos - dir * reach;
    }

    private static Vector2 ComputePushDir(PlayerCombat attacker, PlayerCombat defender)
    {
        if (attacker == null || defender == null) return Vector2.right;
        return ((Vector2)defender.transform.position - (Vector2)attacker.transform.position).normalized;
    }

    private static Vector2 RandomSpawnPos(bool isPlayer1)
    {
        float x = isPlayer1 ? Random.Range(-7.25f, -4.79f) : Random.Range(4.79f, 7.25f);
        float y = Random.Range(-3.90f, -0.81f);
        return new Vector2(x, y);
    }
}
