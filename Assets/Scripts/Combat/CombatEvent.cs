using System.Collections.Generic;

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
    Saboteur,        // playerIndex = Saboteur/Sabotage holder, targetIndex = victim who permanently loses a random weapon, weaponName = destroyed weapon. Pre-fight (Saboteur skill, also gives -100 initiative) OR mid-fight (Sabotage skill, 50% per landed hit — see isMidFight).
    FlashFlood,      // playerIndex = attacker, targetIndex = defender. Super: jumps to attacker's own WeaponHUD, throws ffWeapons (always up to 3, fewer if the defender dies mid-burst) one at a time — each ALWAYS hits (never rolls Dodge/Block, ignores pets too). ffDamages/ffHpAfter are parallel to ffWeapons (Survival/Resistant/armor already resolved per-hit). The 3 weapons are removed from the attacker's loadout permanently (consumed). Consumes the whole turn — no Thief/pickup/swap/throw/melee follows.
    HasteAttack,     // playerIndex = attacker, targetIndex = defender. Super: speed-based dash that runs through the defender — damage = speed * 1.5 (no weapon/STR), +5% crit chance. Rolls Dodge/Block normally (isDodged/isBlocked, no damage in either case); damage/isCrit/newHp/maxHp only set when it lands. Consumes the whole turn — no Thief/pickup/swap/throw/melee follows.
    PiledriverAttack, // playerIndex = attacker, targetIndex = defender. Super: grabs the defender, jumps with them, and slams down on top of them — damage = defender.str * 2.5 (no weapon/STR of the attacker). NEVER dodged/blocked (ignored entirely, no isDodged/isBlocked fields). damage/isCrit/newHp/maxHp always set. Consumes the whole turn — no Thief/pickup/swap/throw/melee follows.
    NetThrow,        // playerIndex = attacker, targetIndex = defender. Super: throws a net that ALWAYS lands (no Dodge/Block roll at all) — no damage, just sets defender.netEnsnared = true. Consumes the whole turn.
    NetEnsnaredSkip, // playerIndex = ensnared player whose action is being skipped this turn (still netEnsnared — distinct from StunSkip, which represents a temporary Chaining stun that always ends after exactly 1 skipped action; Net keeps skipping every turn until NetFreed).
    NetFreed,        // playerIndex = player who just broke free of the net (received a hit while netEnsnared — see CombatSimulator.ApplyDamage/SimulateThrow/SimulateRetaliation/Simulate*Attack call sites).
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
    public bool  isMidFight; // Saboteur event only — true when emitted by Sabotage (per-hit) instead of Saboteur (pre-fight); skips the post-popup pause so the weapon falls without blocking the fight.
    public bool  isDodged;   // HasteAttack only — defender dodged the dash, no damage.
    public bool  isBlocked;  // HasteAttack only — defender blocked the dash, no damage.
    public int   newHp;
    public int   maxHp;
    public int   extraActions;
    public string weaponName;

    // Flash Flood only — parallel lists, one entry per weapon thrown (see CombatEventType.FlashFlood).
    public List<string> ffWeapons;
    public List<int>    ffDamages;
    public List<int>    ffHpAfter;
}
