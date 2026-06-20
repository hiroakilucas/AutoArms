public enum CombatEventType
{
    TurnStart,       // playerIndex = acting player
    RunToDefender,   // playerIndex = attacker, targetIndex = defender
    ThrowWeapon,     // playerIndex = attacker, targetIndex = defender, weaponName
    PickupWeapon,    // playerIndex = picker, weaponName
    WeaponSwap,      // playerIndex = player who drops their current weapon (stays in loadout, doesn't disappear) right before picking up a new one — weaponName = the dropped weapon
    Thief,           // playerIndex = thief (unarmed), targetIndex = victim (armed) who loses the weapon to the thief, weaponName
    Hit,             // playerIndex = attacker, targetIndex = defender, damage, isCrit, isCombo, isThrow
    Counter,         // playerIndex = counterer (deals damage), targetIndex = original attacker (countered), damage, isCrit. Cancela o hit do atacante e o resto do combo.
    Reversal,        // playerIndex = quem reverte (deals damage), targetIndex = original attacker (alvo), damage, isCrit. Acontece depois do atacante já ter acertado; cancela o resto do combo dele.
    Dodge,           // playerIndex = attacker, targetIndex = dodger
    Block,           // playerIndex = attacker, targetIndex = blocker (50% knockback, no damage)
    Miss,            // playerIndex = attacker, targetIndex = missed (defender DodgeLeaps)
    Disarm,          // playerIndex = attacker, targetIndex = disarmed, weaponName
    WeaponDrop,      // playerIndex = player who dropped, weaponName
    ShieldDisarm,    // playerIndex = attacker, targetIndex = defender who loses the Shield on a landed hit (independent of weapon Disarm)
    ShieldDrop,      // playerIndex = player who drops the Shield while successfully blocking (mirrors WeaponDrop)
    HealthChanged,   // playerIndex = affected player, newHp, maxHp
    SpeedBonus,      // playerIndex = faster player, extraActions (for RAPIDO! popup)
    Stunned,         // playerIndex = Chaining holder (landed the 3rd consecutive hit), targetIndex = player who becomes stunned for 1 action
    StunSkip,        // playerIndex = stunned player whose action is being skipped (consumes 1 stunnedActions)
    Saboteur,        // playerIndex = Saboteur holder, targetIndex = victim who permanently loses a random weapon and -100 initiative, weaponName = destroyed weapon. Emitted once, pre-fight (first event(s) in the list).
    TurnEnd,         // playerIndex = acting player (attacker returns to spawn)
    CombatEnd,       // playerIndex = winner
}

public class CombatEvent
{
    public CombatEventType type;
    public int   playerIndex;
    public int   targetIndex;
    public int   damage;
    public bool  isCrit;
    public bool  isCombo;
    public bool  isThrow;
    public int   newHp;
    public int   maxHp;
    public int   extraActions;
    public string weaponName;
}
