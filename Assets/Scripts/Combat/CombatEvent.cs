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
    Saboteur,        // playerIndex = Sabotage holder, targetIndex = victim who permanently loses a random non-equipped weapon, weaponName = destroyed weapon. 50% per landed hit, always mid-fight now — the old pre-fight Saboteur trigger was replaced by SaboteurBreak below.
    SaboteurBreak,   // playerIndex = Saboteur skill holder, targetIndex = victim, weaponName = weapon that just broke. The victim's FIRST weapon draw of the fight (via Thief steal or normal pickup) always breaks — guaranteed, no Roll — right after the pickup animation, with a Hurt reaction + weapon falling from the hand (different from Saboteur/Sabotage above, which fall from the WeaponHUD icon since that weapon was never actually equipped).
    FlashFlood,      // playerIndex = attacker, targetIndex = defender. Super: jumps to attacker's own WeaponHUD, throws ffWeapons (always up to 3, fewer if the defender dies mid-burst) one at a time — each ALWAYS hits (never rolls Dodge/Block, ignores pets too). ffDamages/ffHpAfter are parallel to ffWeapons (Survival/Resistant/armor already resolved per-hit). The 3 weapons are removed from the attacker's loadout permanently (consumed). Consumes the whole turn — no Thief/pickup/swap/throw/melee follows.
    HasteAttack,     // playerIndex = attacker, targetIndex = defender. Super: speed-based dash that runs through the defender — damage = speed * 1.5 (no weapon/STR), +5% crit chance. Rolls Dodge/Block normally (isDodged/isBlocked, no damage in either case); damage/isCrit/newHp/maxHp only set when it lands. Consumes the whole turn — no Thief/pickup/swap/throw/melee follows.
    PiledriverAttack, // playerIndex = attacker, targetIndex = defender. Super: grabs the defender, jumps with them, and slams down on top of them — damage = defender.str * 2.5 (no weapon/STR of the attacker). NEVER dodged/blocked (ignored entirely, no isDodged/isBlocked fields). damage/isCrit/newHp/maxHp always set. Consumes the whole turn — no Thief/pickup/swap/throw/melee follows.
    NetThrow,        // playerIndex = attacker, targetIndex = defender. Super: throws a net that ALWAYS lands (no Dodge/Block roll at all) — no damage, just sets defender.netEnsnared = true. Consumes the whole turn.
    NetEnsnaredSkip, // playerIndex = ensnared player whose action is being skipped this turn (still netEnsnared — distinct from StunSkip, which represents a temporary Chaining stun that always ends after exactly 1 skipped action; Net keeps skipping every turn until NetFreed).
    NetFreed,        // playerIndex = player who just broke free of the net (received a hit while netEnsnared — see CombatSimulator.ApplyDamage/SimulateThrow/SimulateRetaliation/Simulate*Attack call sites).
    FierceBruteActivated, // playerIndex = attacker who just activated the buff (fierceBruteActive = true) — doesn't end the turn by itself, falls through to Thief/pickup/throw/melee normally; the buff doubles damage (and +10% crit) on the attacker's next melee hit (see Hit.isFierceBrute below), consumed either way (hit lands or not). NÃO necessariamente no mesmo turno: se Net/Bomb também ativarem nesse turno (mesmo loop embaralhado) e consumirem o turno antes do attacker chegar a agir de verdade, o buff persiste pendente pro turno seguinte dele (ver CombatSimulator.TryActivateFierceBrute).
    BombThrow,       // playerIndex = attacker. Super: explosão em área que atinge TODOS os alvos do lado inimigo (bombTargets — hoje só o defensor, ver CombatSimulator.GetEnemyTargets) com o mesmo dano sorteado (damage, 15-25). NUNCA esquivado/bloqueado, sem crítico, sem STR do atacante, dano NÃO reduzido por armadura. Consome o turno inteiro (como Net/Piledriver/Haste/Flash Flood). netFreedTargets lista quem teve a rede (Net) quebrada pela explosão.
    TragicPotionUse, // playerIndex = quem bebeu. Super: auto-cura quando hp < 60% do maxHp — healAmount (entre 25% e 50% do maxHp), newHp/maxHp pra sincronizar a barra de vida. Não ataca ninguém, não interage com dodge/block/counter/reversal. Não consome o turno (mesmo padrão de Fierce Brute/Bomb — cai direto pro fluxo normal do turno).
    FastMetabolismRegen, // playerIndex = quem regenerou. Passiva: cura 1% do HP máximo TODO turno (healAmount, newHp) — sem condição de HP, sem chance, nunca consome o turno.
    FastMetabolismPulse, // playerIndex = quem curou. Pulso ativado a primeira vez que o HP cruza 50% do máximo: no PRÓXIMO turno do personagem (se não tiver sofrido dano nesse intervalo), cura 5% do HP máximo (healAmount, newHp) 10 vezes EM SEQUÊNCIA, todas no mesmo turno (burst) — não mais uma por turno. pulseCount = nº desta cura do burst (1 a 10), sempre 1..10 completo na mesma ativação; interrompido (sem evento próprio, cancela o burst inteiro) se sofrer dano antes do burst começar.
    VampirismAttack, // playerIndex = atacante (vampiro), targetIndex = defensor mordido. Super: mordida garantida (NUNCA esquivada/bloqueada) que causa 25% do HP que falta pro atacante (mínimo 1) como dano ao defensor (damage, newDefenderHp) e cura o atacante na mesma quantidade (healAmount, newAttackerHp) — 1x por luta. Consome o turno inteiro, mesmo padrão de Net/Piledriver/Bomb (a mordida É a própria ação do turno).
    ChefPizzaThrow,  // playerIndex = dono da skill Chef, targetIndex = defensor que come a pizza. Passivo de combate: lançado uma única vez, na 1ª ação do dono da skill (chefPizzaThrown) — SEMPRE acerta (sem Roll de Dodge/Block). Marca defensor.poisoned = true; não consome o turno, não causa dano por si só (o veneno tica a cada PoisonDamage, ver abaixo).
    PoisonDamage,    // playerIndex = quem sofre o veneno (não necessariamente quem tem Chef — é o lado envenenado). Emitido no FIM de TODO turno de quem estiver poisoned (mesmo turnos pulados por Net/Stun), até curar (Tragic Potion) ou a luta acabar. damage = 1% do próprio HP máximo (arredondado pra cima, mínimo 1, calculado uma vez no ChefPizzaThrow), newHp = HP após o tick.
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
    public bool  isDodged;   // HasteAttack only — defender dodged the dash, no damage.
    public bool  isBlocked;  // HasteAttack only — defender blocked the dash, no damage.
    public bool  isFierceBrute; // Hit only — true when this hit consumed an active Fierce Brute buff (damage already doubled in CombatSimulator) — tells CombatPlayer to show the flash/×2 popup/destroy the attacker's aura.
    public int   newHp;
    public int   maxHp;
    public int   extraActions;
    public int   healAmount; // TragicPotionUse/FastMetabolismRegen/FastMetabolismPulse/VampirismAttack — quantidade curada
    public int   pulseCount; // FastMetabolismPulse only — nº desta cura do pulso (1 a 10)
    public int   newDefenderHp; // VampirismAttack only — HP do defensor mordido após o dano
    public int   newAttackerHp; // VampirismAttack only — HP do atacante (vampiro) após a cura
    public string weaponName;

    // Flash Flood only — parallel lists, one entry per weapon thrown (see CombatEventType.FlashFlood).
    public List<string> ffWeapons;
    public List<int>    ffDamages;
    public List<int>    ffHpAfter;

    // Bomb only — bombTargets/bombTargetDamages/bombTargetHp são paralelas, uma entrada por
    // alvo atingido pela explosão (damage acima já carrega o valor sorteado, igual pra todos —
    // bombTargetDamages existe separado pra já deixar espaço pra dano variar por alvo no
    // futuro, ex: pets com resistência própria). netFreedTargets é a sublista de playerIndex
    // que tiveram CombatSimulator.PlayerState.netEnsnared quebrado por esta explosão.
    public List<int> bombTargets;
    public List<int> bombTargetDamages;
    public List<int> bombTargetHp;
    public List<int> netFreedTargets;
}
