using System.Collections.Generic;
using UnityEngine;

// Pure C# pre-calculation engine — mirrors PlayerCombat/AttackSequencer logic
// without any MonoBehaviour or coroutine overhead. Returns a flat event list
// that CombatPlayer replays as animation.
public class CombatSimulator
{
    System.Random     _rng;
    PlayerState       _p1, _p2;
    List<CombatEvent> _events;

    public List<CombatEvent> Simulate(PlayerProfile p1Profile, PlayerProfile p2Profile, int seed = -1)
    {
        _rng    = seed >= 0 ? new System.Random(seed) : new System.Random();
        _events = new List<CombatEvent>();

        _p1 = BuildState(p1Profile, index: 0);
        _p2 = BuildState(p2Profile, index: 1);

        ApplySkillStats(_p1);
        ApplySkillStats(_p2);

        const int maxRounds = 300;
        int round = 0;

        while (_p1.isAlive && _p2.isAlive && round < maxRounds)
        {
            SimulateRound(round);
            round++;
        }

        int winnerIndex = _p1.isAlive ? 0 : 1;
        Emit(new CombatEvent { type = CombatEventType.CombatEnd, playerIndex = winnerIndex });
        return _events;
    }

    // --- State building ---

    private PlayerState BuildState(PlayerProfile profile, int index)
    {
        var s = new PlayerState();
        s.index            = index;
        s.name             = profile.profileName;
        s.hp               = profile.maxHealth;
        s.maxHp            = profile.maxHealth;
        s.str              = profile.str;
        s.agility          = profile.agility;
        s.speed            = profile.speed;
        s.armor            = profile.armor;
        s.evasion          = profile.evasion;
        s.accuracy         = profile.accuracy;
        s.initiative       = profile.initiative;
        s.counter          = profile.counter;
        s.criticalChance   = profile.criticalChance;
        s.hitSpeed         = profile.hitSpeed;
        s.comboChanceBonus = 0f;
        s.runSpeedMultiplier = 1f;

        if (profile.weaponLoadout?.weapons != null)
            foreach (var w in profile.weaponLoadout.weapons)
                if (w != null) s.weaponLoadout.Add(w);

        if (profile.skills != null)
            foreach (var sk in profile.skills)
                if (sk?.skillName != null) s.skills.Add(sk.skillName);

        return s;
    }

    private void ApplySkillStats(PlayerState s)
    {
        if (s.HasSkill("Vitality"))            { s.maxHp += 50; s.hp += 50; }
        if (s.HasSkill("Herculean Strength"))  { s.str += 15; s.agility -= 4; }
        if (s.HasSkill("Feline Agility"))      { s.agility = Mathf.RoundToInt(s.agility * 1.5f); }
        if (s.HasSkill("Lightning Bolt"))      { s.runSpeedMultiplier *= 1.5f; }
        if (s.HasSkill("Immortal"))            { s.maxHp += 100; s.hp += 100; s.runSpeedMultiplier *= 0.5f; }
        if (s.HasSkill("Armour"))              { s.armor += 0.30f; }
        if (s.HasSkill("Extra Thick Skin"))    { s.armor += 0.50f; }
        if (s.HasSkill("Untouchable"))         { s.evasion += 0.25f; }
        if (s.HasSkill("Bodybuilder"))         { s.str = Mathf.RoundToInt(s.str * 1.5f); }
        if (s.HasSkill("Relentless"))          { s.comboChanceBonus += 0.15f; }
        if (s.HasSkill("Lead Skeleton"))       { s.leadSkeleton = true; }
        if (s.HasSkill("Ballet Shoes"))        { s.evasion += 0.10f; s.firstHitAvoided = true; }
        if (s.HasSkill("First Strike"))        { s.initiative += 200; }
        if (s.HasSkill("Counter Attack"))      { s.counter += 0.40f; }
        if (s.HasSkill("Monk"))                { s.counter += 0.40f; s.initiative -= 200; s.hitSpeed = 0f; }
    }

    // --- Round / turn dispatch ---

    private void SimulateRound(int round)
    {
        _p1.speedDebt += _p1.speed;
        _p2.speedDebt += _p2.speed;

        int p1Act = 0, p2Act = 0;
        while (_p2.speed > 0 && _p1.speedDebt >= _p2.speed) { p1Act++; _p1.speedDebt -= _p2.speed; }
        while (_p1.speed > 0 && _p2.speedDebt >= _p1.speed) { p2Act++; _p2.speedDebt -= _p1.speed; }
        p1Act = Mathf.Max(1, p1Act);
        p2Act = Mathf.Max(1, p2Act);

        if (p1Act > 1) Emit(new CombatEvent { type = CombatEventType.SpeedBonus, playerIndex = 0, extraActions = p1Act - 1 });
        if (p2Act > 1) Emit(new CombatEvent { type = CombatEventType.SpeedBonus, playerIndex = 1, extraActions = p2Act - 1 });

        bool p1First    = _p1.initiative >= _p2.initiative;
        int  maxActions = Mathf.Max(p1Act, p2Act);

        for (int i = 0; i < maxActions; i++)
        {
            if (p1First)
            {
                if (i < p1Act && _p1.isAlive && _p2.isAlive) SimulateTurn(_p1, _p2);
                if (i < p2Act && _p1.isAlive && _p2.isAlive) SimulateTurn(_p2, _p1);
            }
            else
            {
                if (i < p2Act && _p1.isAlive && _p2.isAlive) SimulateTurn(_p2, _p1);
                if (i < p1Act && _p1.isAlive && _p2.isAlive) SimulateTurn(_p1, _p2);
            }
        }
    }

    private void SimulateTurn(PlayerState attacker, PlayerState defender)
    {
        Emit(new CombatEvent { type = CombatEventType.TurnStart, playerIndex = attacker.index });

        // 1. Pick up weapon if unarmed (40% chance)
        if (attacker.currentWeaponData == null && attacker.weaponLoadout.Count > 0 && Roll(0.40f))
        {
            var w = attacker.weaponLoadout[_rng.Next(attacker.weaponLoadout.Count)];
            attacker.currentWeaponData = w;
            Emit(new CombatEvent { type = CombatEventType.PickupWeapon, playerIndex = attacker.index, weaponName = w.weaponName });
        }

        // 2. Check throw before melee
        if (attacker.currentWeaponData != null && Roll(ThrowChance(attacker)))
        {
            SimulateThrow(attacker, defender);
            Emit(new CombatEvent { type = CombatEventType.TurnEnd, playerIndex = attacker.index });
            return;
        }

        // 3. Melee
        Emit(new CombatEvent { type = CombatEventType.RunToDefender, playerIndex = attacker.index, targetIndex = defender.index });
        SimulateHit(attacker, defender, isCombo: false);

        // Combo loop (mirrors AttackRoutine's while loop)
        while (defender.isAlive && Roll(ComboChance(attacker)))
            SimulateHit(attacker, defender, isCombo: true);

        Emit(new CombatEvent { type = CombatEventType.TurnEnd, playerIndex = attacker.index });
    }

    // --- Hit resolution (mirrors HitRoutine) ---

    private void SimulateHit(PlayerState attacker, PlayerState defender, bool isCombo)
    {
        // Ballet Shoes: first hit of the fight auto-dodged
        if (!isCombo && defender.firstHitAvoided)
        {
            defender.firstHitAvoided = false;
            Emit(new CombatEvent { type = CombatEventType.Dodge, playerIndex = attacker.index, targetIndex = defender.index });
            return;
        }

        // Monk: guards instead of attacking
        if (attacker.hitSpeed <= 0f) return;

        // Dodge check
        if (Roll(DodgeChance(attacker, defender)))
        {
            Emit(new CombatEvent { type = CombatEventType.Dodge, playerIndex = attacker.index, targetIndex = defender.index });
            return;
        }

        // Block check
        if (Roll(BlockChance(attacker, defender)))
        {
            Emit(new CombatEvent { type = CombatEventType.Block, playerIndex = attacker.index, targetIndex = defender.index });

            // Attacker may drop weapon on impact (15%)
            if (attacker.currentWeaponData != null && Roll(0.15f))
            {
                string wn = attacker.currentWeaponData.weaponName;
                attacker.weaponLoadout.Remove(attacker.currentWeaponData);
                attacker.currentWeaponData = null;
                Emit(new CombatEvent { type = CombatEventType.WeaponDrop, playerIndex = attacker.index, weaponName = wn });
            }
            // Defender may drop weapon/shield on impact (10%)
            if (defender.currentWeaponData != null && Roll(0.10f))
            {
                string wn = defender.currentWeaponData.weaponName;
                defender.weaponLoadout.Remove(defender.currentWeaponData);
                defender.currentWeaponData = null;
                Emit(new CombatEvent { type = CombatEventType.WeaponDrop, playerIndex = defender.index, weaponName = wn });
            }
            return;
        }

        // Normal hit
        bool isCrit      = Roll(CritChance(attacker));
        int  baseDamage  = CalcBaseDamage(attacker);
        int  finalDamage = isCrit ? baseDamage * 2 : baseDamage;

        // Lead Skeleton: -15% heavy damage
        if (defender.leadSkeleton && attacker.currentWeaponData?.type == WeaponType.Heavy)
            finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * 0.85f));

        // Armor reduction
        if (defender.armor > 0f)
            finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * (1f - defender.armor)));

        Emit(new CombatEvent { type = CombatEventType.Hit, playerIndex = attacker.index, targetIndex = defender.index, damage = finalDamage, isCrit = isCrit, isCombo = isCombo });

        defender.hp = Mathf.Max(0, defender.hp - finalDamage);
        Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = defender.index, newHp = defender.hp, maxHp = defender.maxHp });

        if (!defender.isAlive) return;

        // Disarm (first hit only, not combo)
        if (!isCombo && defender.currentWeaponData != null && Roll(DisarmChance(attacker)))
        {
            string wn = defender.currentWeaponData.weaponName;
            defender.weaponLoadout.Remove(defender.currentWeaponData);
            defender.currentWeaponData = null;
            Emit(new CombatEvent { type = CombatEventType.Disarm, playerIndex = attacker.index, targetIndex = defender.index, weaponName = wn });
        }
    }

    // --- Throw resolution ---

    private void SimulateThrow(PlayerState attacker, PlayerState defender)
    {
        var  weaponData = attacker.currentWeaponData;
        bool isThrown   = weaponData?.type == WeaponType.Thrown;
        string wn       = weaponData?.weaponName ?? "";

        if (!isThrown)
        {
            attacker.weaponLoadout.Remove(weaponData);
        }
        attacker.currentWeaponData = null;

        Emit(new CombatEvent { type = CombatEventType.ThrowWeapon, playerIndex = attacker.index, targetIndex = defender.index, weaponName = wn });

        if (Roll(0.80f))
        {
            int dmg = CalcThrowDamage(weaponData);
            if (defender.armor > 0f)
                dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - defender.armor)));

            Emit(new CombatEvent { type = CombatEventType.Hit, playerIndex = attacker.index, targetIndex = defender.index, damage = dmg });
            defender.hp = Mathf.Max(0, defender.hp - dmg);
            Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = defender.index, newHp = defender.hp, maxHp = defender.maxHp });
        }
        else
        {
            Emit(new CombatEvent { type = CombatEventType.Miss, playerIndex = attacker.index, targetIndex = defender.index });
        }

        // 40% re-equip after throw
        if (attacker.weaponLoadout.Count > 0 && Roll(0.40f))
        {
            var w = attacker.weaponLoadout[_rng.Next(attacker.weaponLoadout.Count)];
            attacker.currentWeaponData = w;
            Emit(new CombatEvent { type = CombatEventType.WeaponEquipped, playerIndex = attacker.index, weaponName = w.weaponName });
        }
    }

    // --- Chance calculations (mirrors PlayerCombat methods) ---

    private float DodgeChance(PlayerState attacker, PlayerState defender)
    {
        float baseChance = defender.currentWeaponData == null ? 0.10f :
            defender.currentWeaponData.type switch
            {
                WeaponType.Fast   => 0.20f,
                WeaponType.Dagger => 0.15f,
                WeaponType.Sword  => 0.10f,
                WeaponType.Heavy  => 0.05f,
                _                 => 0.10f
            };
        float agiBonus = Mathf.Max(0, defender.agility - 3) * 0.02f;
        return Mathf.Min(0.60f, baseChance + agiBonus + defender.evasion);
    }

    private float BlockChance(PlayerState attacker, PlayerState defender)
    {
        if (defender.currentWeaponData == null) return 0f;
        float weaponBonus = defender.currentWeaponData.type switch
        {
            WeaponType.Block  => 0.50f,
            WeaponType.Dagger => 0.15f,
            WeaponType.Sword  => 0.15f,
            WeaponType.Heavy  => 0.15f,
            WeaponType.Slow   => 0.05f,
            _                 => 0f
        };
        return weaponBonus + defender.counter;
    }

    private float CritChance(PlayerState attacker)
    {
        float weaponBonus = attacker.currentWeaponData == null ? 0.05f :
            attacker.currentWeaponData.type switch
            {
                WeaponType.Dagger => 0.08f,
                WeaponType.Sword  => 0.05f,
                WeaponType.Heavy  => 0.03f,
                _                 => 0.05f
            };
        return weaponBonus + attacker.criticalChance;
    }

    private float ComboChance(PlayerState attacker)
    {
        float baseChance = attacker.currentWeaponData == null ? 0.10f :
            attacker.currentWeaponData.type switch
            {
                WeaponType.Fast   => 0.40f,
                WeaponType.Dagger => 0.35f,
                WeaponType.Sword  => 0.25f,
                WeaponType.Heavy  => 0.10f,
                _                 => 0.25f
            };
        float agiBonus = Mathf.Max(0, attacker.agility - 3) * 0.015f;
        return baseChance + agiBonus + attacker.comboChanceBonus;
    }

    private float DisarmChance(PlayerState attacker)
    {
        if (attacker.currentWeaponData == null) return 0f;
        return attacker.currentWeaponData.type switch
        {
            WeaponType.Dagger => 0.20f,
            WeaponType.Fast   => 0.15f,
            WeaponType.Sword  => 0.10f,
            WeaponType.Heavy  => 0.05f,
            _                 => 0f
        };
    }

    private float ThrowChance(PlayerState attacker)
    {
        if (attacker.currentWeaponData == null) return 0f;
        return attacker.currentWeaponData.type switch
        {
            WeaponType.Thrown  => 1.00f,
            WeaponType.Dagger  => 0.60f,
            WeaponType.Fast    => 0.60f,
            WeaponType.Sword   => 0.60f,
            WeaponType.Heavy   => 0.60f,
            _                  => 0f
        };
    }

    // --- Damage calculations (mirrors CalcDamage / ThrowDamage) ---

    private int CalcBaseDamage(PlayerState attacker)
    {
        if (attacker.currentWeaponData == null)
            return 1 + StrBonus(attacker);
        return attacker.currentWeaponData.type switch
        {
            WeaponType.Heavy  => _rng.Next(30, 50) + HeavyStrBonus(attacker),
            WeaponType.Sword  => _rng.Next(10, 18),
            WeaponType.Dagger => _rng.Next(7, 13),
            _                 => attacker.currentWeaponData.damage > 0 ? attacker.currentWeaponData.damage : 3
        };
    }

    private int CalcThrowDamage(WeaponData data)
    {
        if (data == null) return 2;
        return data.type switch
        {
            WeaponType.Heavy  => _rng.Next(30, 50),
            WeaponType.Sword  => _rng.Next(10, 18),
            WeaponType.Dagger => _rng.Next(7, 13),
            _                 => data.damage > 0 ? data.damage : 3
        };
    }

    private int StrBonus(PlayerState s)      => Mathf.Max(0, (s.str - 10) / 2);
    private int HeavyStrBonus(PlayerState s) => Mathf.Max(0, s.str - 10);

    // --- Utilities ---

    private bool Roll(float chance) => (float)_rng.NextDouble() < chance;
    private void Emit(CombatEvent evt) => _events.Add(evt);
}
