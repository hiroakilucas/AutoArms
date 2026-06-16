public enum CombatEventType
{
    TurnStart,       // playerIndex = acting player
    RunToDefender,   // playerIndex = attacker, targetIndex = defender
    ThrowWeapon,     // playerIndex = attacker, targetIndex = defender, weaponName
    PickupWeapon,    // playerIndex = picker, weaponName
    WeaponEquipped,  // playerIndex = who equipped, weaponName (after throw: 40% re-equip)
    Hit,             // playerIndex = attacker, targetIndex = defender, damage, isCrit, isCombo
    Dodge,           // playerIndex = attacker, targetIndex = dodger
    Block,           // playerIndex = attacker, targetIndex = blocker (50% knockback, no damage)
    Miss,            // playerIndex = attacker, targetIndex = missed (defender DodgeLeaps)
    Disarm,          // playerIndex = attacker, targetIndex = disarmed, weaponName
    WeaponDrop,      // playerIndex = player who dropped, weaponName
    HealthChanged,   // playerIndex = affected player, newHp, maxHp
    SpeedBonus,      // playerIndex = faster player, extraActions (for RAPIDO! popup)
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
    public int   newHp;
    public int   maxHp;
    public int   extraActions;
    public string weaponName;
}
