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

    // Toggles between 1x and 1.5x playback. Returns the new state (true = now at 1.5x).
    public bool ToggleSpeed()
    {
        if (!_is2x)
        {
            _playbackSpeed = 1.5f;
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
                    float slashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t;

                    // Thrown-weapon hits already had their "windup" during ThrowWeapon's flight —
                    // the attacker has no weapon in hand anymore, so don't re-trigger a melee
                    // swing or wait out slashHalf again; apply the impact immediately instead.
                    if (!evt.isThrow)
                    {
                        string trigger = attacker?.weaponHandler.currentType switch
                        {
                            WeaponType.Heavy  => "SlashingHeavy",
                            WeaponType.Dagger => "SlashingDagger",
                            _                 => "Slashing"
                        };
                        attacker?.GetComponent<Animator>()?.SetTrigger(trigger);
                        yield return new WaitForSeconds(slashHalf);
                    }

                    // Impact moment: damage, hurt animation, knockback, and popup all fire
                    // together here instead of being staggered — health used to only update
                    // once the separate HealthChanged event was reached afterward, and the
                    // popup only appeared once PlayHurt finished, both visibly lagging the hit.
                    Vector2 pushDir = ComputePushDir(attacker, defender);
                    float   kbDist  = attacker?.settings?.knockbackDistance ?? 0.5f;
                    float   kbDur   = attacker?.settings?.hurtDuration ?? 0.07f;

                    var hs = evt.targetIndex == 0 ? _h1 : _h2;
                    hs?.TakeDamage(evt.damage);

                    Vector3 popupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.Spawn(popupPos, evt.damage, evt.isCrit);

                    StartCoroutine(defender.Knockback(pushDir, kbDist, kbDur * t));
                    yield return StartCoroutine(defender.animationController.PlayHurt(kbDur * t));

                    // Let the slash clip finish its second half before anything else can
                    // re-trigger it — without this, combo hits retrigger mid-clip and the
                    // animation snaps/restarts instead of playing through.
                    if (!evt.isThrow)
                        yield return new WaitForSeconds(slashHalf);
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.HealthChanged:
                ApplyHealthChanged(evt);
                yield return null;
                break;

            case CombatEventType.Dodge:
                if (defender != null)
                {
                    // Dodge always follows a melee swing attempt (never a throw) — the attacker
                    // swings, and exactly when it would have landed, the defender leaps away.
                    string trigger = attacker?.weaponHandler.currentType switch
                    {
                        WeaponType.Heavy  => "SlashingHeavy",
                        WeaponType.Dagger => "SlashingDagger",
                        _                 => "Slashing"
                    };
                    attacker?.GetComponent<Animator>()?.SetTrigger(trigger);

                    float slashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t;
                    yield return new WaitForSeconds(slashHalf);

                    Vector3 dodgePopupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnDodge(dodgePopupPos);
                    Vector2 dodgeDir = ComputePushDir(attacker, defender);
                    float   dodgeDist = attacker?.settings?.knockbackDistance ?? 0.5f;
                    yield return StartCoroutine(defender.DodgeLeap(dodgeDir, dodgeDist));

                    yield return new WaitForSeconds(slashHalf);
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.Block:
                if (defender != null)
                {
                    // Same reasoning as Dodge: sync the attacker's swing with the moment of impact.
                    string trigger = attacker?.weaponHandler.currentType switch
                    {
                        WeaponType.Heavy  => "SlashingHeavy",
                        WeaponType.Dagger => "SlashingDagger",
                        _                 => "Slashing"
                    };
                    attacker?.GetComponent<Animator>()?.SetTrigger(trigger);

                    float slashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t;
                    yield return new WaitForSeconds(slashHalf);

                    Vector3 blockPopupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnBlock(blockPopupPos);

                    float kbDist = (attacker?.settings?.knockbackDistance ?? 0.5f) * 0.5f;
                    float kbDur  = (attacker?.settings?.hurtDuration ?? 0.07f) * t;
                    Vector2 blockDir = ComputePushDir(attacker, defender);

                    // Block animation and knockback in parallel
                    StartCoroutine(defender.Knockback(blockDir, kbDist, kbDur));
                    yield return StartCoroutine(defender.animationController.PlayBlock(0.36666667f * t));

                    yield return new WaitForSeconds(slashHalf);
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
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
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
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
                if (attacker != null)
                {
                    // Capture sprite/position/scale before Unequip destroys the in-hand weapon object.
                    var weaponData   = attacker.weaponHandler.CurrentWeaponData;
                    var inHandWeapon = attacker.weaponHandler.CurrentWeapon;
                    Vector3 launchPos = attacker.weaponHandler.handBone != null
                        ? attacker.weaponHandler.handBone.position
                        : (inHandWeapon != null ? inHandWeapon.transform.position : attacker.transform.position + Vector3.up * 0.5f);
                    Vector3 projectileScale = inHandWeapon != null
                        ? inHandWeapon.transform.lossyScale
                        : (attacker.weaponHandler.handBone != null ? attacker.weaponHandler.handBone.lossyScale : Vector3.one) * (weaponData?.scale ?? 1f);

                    // Mirrors CombatSimulator.SimulateThrow: non-Thrown weapons are removed from
                    // the loadout permanently (so the icon also disappears from WeaponHUD); a
                    // Thrown weapon just unequips, since it can be picked up/re-equipped later.
                    if (weaponData != null && weaponData.type != WeaponType.Thrown)
                        attacker.weaponHandler.UnequipPermanent();
                    else
                        attacker.weaponHandler.Unequip();
                    attacker.GetComponent<Animator>()?.SetTrigger("Throwing");

                    if (weaponData?.inHandSprite != null)
                    {
                        Vector3 targetPos = defender != null
                            ? defender.transform.position
                            : attacker.transform.position + (attacker.isPlayer1 ? Vector3.right : Vector3.left) * 5f;

                        Vector3 flightDir   = (targetPos - launchPos).normalized;
                        float   flightAngle = Mathf.Atan2(flightDir.y, flightDir.x) * Mathf.Rad2Deg;

                        var flyingWeapon = new GameObject("FlyingWeapon");
                        flyingWeapon.transform.position   = launchPos;
                        flyingWeapon.transform.localScale = new Vector3(
                            Mathf.Abs(projectileScale.x), Mathf.Abs(projectileScale.y), Mathf.Abs(projectileScale.z));
                        flyingWeapon.transform.rotation = Quaternion.Euler(0, 0, flightAngle);
                        var sr = flyingWeapon.AddComponent<SpriteRenderer>();
                        sr.sprite           = weaponData.inHandSprite;
                        sr.sortingLayerName = "Weapons";
                        sr.sortingOrder     = 10;

                        bool  rotate = weaponData.type == WeaponType.Thrown;
                        float arc    = rotate ? 0.5f : 0f;
                        yield return StartCoroutine(attacker.FlyWeapon(flyingWeapon.transform, launchPos, targetPos, 0.45f * t, rotate, arc));
                        Destroy(flyingWeapon);
                    }
                    else
                    {
                        yield return new WaitForSeconds(0.45f * t);
                    }
                }
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
