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

    // Lido por CombatSceneLoader ANTES do EntryFall, pra equipar visualmente (sem trigger de
    // animação) a mesma arma que o EquipStartingWeaponIfNeeded já sorteou na simulação —
    // "cai em cena já com a arma", em vez de cair desarmado e só equipar depois.
    public WeaponData Player1StartingWeapon { get; private set; }
    public WeaponData Player2StartingWeapon { get; private set; }

    public List<CombatEvent> Simulate(PlayerProfile p1Profile, PlayerProfile p2Profile, int seed = -1)
    {
        Debug.Log("[CombatSimulator] Iniciando simulação...");

        _rng    = seed >= 0 ? new System.Random(seed) : new System.Random();
        _events = new List<CombatEvent>();

        _p1 = BuildState(p1Profile, index: 0);
        _p2 = BuildState(p2Profile, index: 1);

        EquipStartingWeaponIfNeeded(_p1);
        EquipStartingWeaponIfNeeded(_p2);
        Player1StartingWeapon = _p1.currentWeaponData;
        Player2StartingWeapon = _p2.currentWeaponData;

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

        Debug.Log($"[CombatSimulator] {_events.Count} eventos gerados");
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
        s.reversal         = profile.reversal;
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

    // 40% de chance de qualquer um dos dois jogadores já começar a luta com uma arma
    // aleatória do loadout equipada, em vez de sempre desarmado — independe de skill, vale
    // pros dois lados igual. Ver comentário em Player1StartingWeapon/Player2StartingWeapon.
    private void EquipStartingWeaponIfNeeded(PlayerState s)
    {
        if (s.weaponLoadout.Count > 0 && Roll(0.40f))
            s.currentWeaponData = s.weaponLoadout[_rng.Next(s.weaponLoadout.Count)];
    }

    private void ApplySkillStats(PlayerState s)
    {
        // Percentuais de HP/STR/AGI/SPD somados num percentual líquido por status e aplicados
        // uma única vez no final — evita arredondamento em cascata quando múltiplas skills
        // afetam o mesmo status (ver nota em "Skills que modificam stats" no CLAUDE.md).
        // evasionPct é multiplicativo sobre o evasion já somado por outras skills (Untouchable,
        // Ballet Shoes) — aplicado depois delas, no fim da função, pra Deity (-100%) zerar o
        // total mesmo que outra skill já tenha somado evasion antes.
        float hpPct = 0f, strPct = 0f, agiPct = 0f, spdPct = 0f, evasionPct = 0f;

        // +18 flat já aplicado permanentemente em profile.maxHealth na escolha (CombatResultPanel.ApplyBonus).
        if (s.HasSkill("Vitality"))            hpPct += 0.5f;
        // +3 flat já aplicado permanentemente em profile.str na escolha (CombatResultPanel.ApplyBonus).
        if (s.HasSkill("Herculean Strength"))  strPct += 0.5f;
        if (s.HasSkill("Feline Agility"))       agiPct += 0.5f;
        // +3 flat já aplicado permanentemente em profile.speed na escolha. Antes afetava
        // runSpeedMultiplier; agora afeta o atributo speed real (ações extra no Speed System).
        if (s.HasSkill("Lightning Bolt"))      spdPct += 0.5f;
        // +5 flat já aplicado permanentemente em profile.speed na escolha. -200 iniciativa e
        // +50% dano crítico são flat puro (não entram no percentual líquido).
        if (s.HasSkill("Reconnaissance"))
        {
            spdPct += 1.5f;
            s.initiative -= 200;
            s.critDamageBonus += 0.5f;
        }
        if (s.HasSkill("Immortal"))
        {
            hpPct  += 2.5f;
            strPct -= 0.25f;
            agiPct -= 0.25f;
            spdPct -= 0.25f;
        }
        if (s.HasSkill("Bodybuilder"))          strPct += 0.5f;

        // +25% armor (flat, fora do percentual líquido — armor não é um dos quatro status
        // que stackeiam em percentual) e -15% SPD (entra no percentual líquido normalmente).
        if (s.HasSkill("Armour"))
        {
            s.armor += 0.25f;
            spdPct  -= 0.15f;
        }

        // +100% HP, +100% STR, -100% AGI, -90% SPD (não -100%: speed fixo em 0 travava o
        // player pra nunca ter ação própria nem chance de pegar arma — com -90% ainda existe
        // chance de arredondar > 0 dependendo do speed base), -100% evasion ("Dexterity" — sem
        // resistência a ser atingido), -200 initiative, +40% reversal (cancela o hit do
        // atacante antes dele conectar — ver SimulateHit).
        if (s.HasSkill("Deity"))
        {
            hpPct      += 1.0f;
            strPct     += 1.0f;
            agiPct     -= 1.0f;
            spdPct     -= 0.90f;
            evasionPct -= 1.0f;
            s.noEvasion  = true;
            s.reversal   += 0.40f;
            s.initiative -= 200;
        }

        if (hpPct != 0f || strPct != 0f || agiPct != 0f || spdPct != 0f)
        {
            s.maxHp   = Mathf.RoundToInt(s.maxHp * (1f + hpPct));
            s.hp      = s.maxHp;
            s.str     = Mathf.RoundToInt(s.str * (1f + strPct));
            s.agility = Mathf.RoundToInt(s.agility * (1f + agiPct));
            s.speed   = Mathf.RoundToInt(s.speed * (1f + spdPct));
        }

        if (s.HasSkill("Extra Thick Skin"))    { s.armor += 0.50f; }
        if (s.HasSkill("Toughened Skin"))      { s.armor += 0.10f; }
        if (s.HasSkill("Untouchable"))         { s.evasion += 0.30f; }
        if (s.HasSkill("Relentless"))          { s.accuracy += 0.30f; }
        if (s.HasSkill("Fists of Fury"))       { s.comboChanceBonus += 0.20f; }
        if (s.HasSkill("Lead Skeleton"))       { s.leadSkeleton = true; }
        if (s.HasSkill("Ballet Shoes"))        { s.evasion += 0.10f; s.firstHitAvoided = true; }
        if (s.HasSkill("First Strike"))        { s.initiative += 200; }
        if (s.HasSkill("Counter Attack"))      { s.blockBonus += 0.10f; s.reversalAfterBlock += 0.90f; }
        if (s.HasSkill("Sixth Sense"))         { s.counter += 0.10f; }
        if (s.HasSkill("Hostility"))            { s.reversal += 0.30f; }
        if (s.HasSkill("Monk"))                { s.counter += 0.40f; s.initiative -= 200; s.hitSpeed = 0f; }

        // Aplicado por último, depois de Untouchable/Ballet Shoes já terem somado evasion —
        // garante que Deity zere o total mesmo que outra skill tenha adicionado evasion antes.
        if (evasionPct != 0f) s.evasion = Mathf.Max(0f, s.evasion * (1f + evasionPct));
    }

    // --- Round / turn dispatch ---

    private void SimulateRound(int round)
    {
        _p1.speedDebt += _p1.speed;
        _p2.speedDebt += _p2.speed;

        int p1Act = 0, p2Act = 0;
        while (_p2.speed > 0 && _p1.speedDebt >= _p2.speed) { p1Act++; _p1.speedDebt -= _p2.speed; }
        while (_p1.speed > 0 && _p2.speedDebt >= _p1.speed) { p2Act++; _p2.speedDebt -= _p1.speed; }
        // Speed 0 (ex: Deity, -100%) não age por conta própria — nunca corre/ataca, só reage
        // via Counter/Reversal nos turnos do oponente. O mínimo garantido de 1 ação só vale
        // pra quem tem speed > 0.
        p1Act = _p1.speed > 0 ? Mathf.Max(1, p1Act) : 0;
        p2Act = _p2.speed > 0 ? Mathf.Max(1, p2Act) : 0;

        // Initiative decide quem age primeiro; em empate (default 0 pra todo personagem sem
        // skill que a altere), quem tem mais speed age primeiro. Empate total continua P1.
        bool p1First = _p1.initiative != _p2.initiative
            ? _p1.initiative > _p2.initiative
            : _p1.speed >= _p2.speed;

        PlayerState firstAttacker  = p1First ? _p1 : _p2;
        PlayerState firstDefender  = p1First ? _p2 : _p1;
        int         firstActCount  = p1First ? p1Act : p2Act;

        PlayerState secondAttacker = p1First ? _p2 : _p1;
        PlayerState secondDefender = p1First ? _p1 : _p2;
        int         secondActCount = p1First ? p2Act : p1Act;

        // Mirrors AttackSequencer.CombatLoop: o primeiro jogador (por iniciativa) executa
        // TODAS as suas ações do round, só então o segundo executa as dele — em vez de
        // intercalar ação-a-ação. Intercalar fazia a última ação extra de um round (com o
        // popup correto) ficar visualmente colada à 1ª ação normal do round seguinte quando
        // o mesmo jogador agia primeiro nos dois rounds, parecendo uma 2ª ação extra sem aviso.
        for (int i = 0; i < firstActCount; i++)
        {
            if (!_p1.isAlive || !_p2.isAlive) return;
            if (i > 0) EmitSpeedBonus(round, firstAttacker, i);
            SimulateTurn(firstAttacker, firstDefender);
        }

        for (int i = 0; i < secondActCount; i++)
        {
            if (!_p1.isAlive || !_p2.isAlive) return;
            if (i > 0) EmitSpeedBonus(round, secondAttacker, i);
            SimulateTurn(secondAttacker, secondDefender);
        }
    }

    // Diagnóstico temporário: confirma round/índice de ação exatos em que o popup "RAPIDO!"
    // é disparado, para validar que nunca acontece em i=0 (1ª ação, nunca é bônus de velocidade).
    private void EmitSpeedBonus(int round, PlayerState player, int actionIndex)
    {
        Debug.Log($"[SpeedBonus] round={round} {player.name} (P{player.index + 1}) ação extra, index={actionIndex}");
        Emit(new CombatEvent { type = CombatEventType.SpeedBonus, playerIndex = player.index, extraActions = 1 });
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
        bool interrupted = SimulateHit(attacker, defender, isCombo: false);

        // Combo loop (mirrors AttackRoutine's while loop). Each consecutive extra hit
        // decays the chance by ×0.5 (1st extra hit normal, 2nd ×0.5, 3rd ×0.25, ...) —
        // mirrors My Brute, where combo probability drops sharply after the first follow-up.
        // Só Counter zera o resto do combo (o hit nunca aconteceu de fato) — Dodge, Block e
        // Reversal não interrompem nada, o combo continua normal depois deles. Reversal pode
        // disparar de novo em cada hit extra do combo, independente do(s) anterior(es).
        int comboCount = 0;
        while (!interrupted && attacker.isAlive && defender.isAlive)
        {
            float comboChance = ComboChance(attacker, comboCount);
            string weaponLabel = attacker.currentWeaponData != null ? attacker.currentWeaponData.weaponName : "Unarmed";
            Debug.Log($"[ComboChance] {attacker.name} (P{attacker.index + 1}, arma={weaponLabel}) hit extra #{comboCount + 1} chance={comboChance:P1}");
            if (!Roll(comboChance)) break;
            interrupted = SimulateHit(attacker, defender, isCombo: true);
            comboCount++;
        }

        Emit(new CombatEvent { type = CombatEventType.TurnEnd, playerIndex = attacker.index });
    }

    // --- Hit resolution (mirrors HitRoutine) ---

    // Retorna true se o combo do atacante deve ser interrompido (Counter ou Reversal do
    // defensor) — false em qualquer outro desfecho (incluindo Dodge/Block, que não interrompem).
    private bool SimulateHit(PlayerState attacker, PlayerState defender, bool isCombo)
    {
        // Ballet Shoes: first hit of the fight auto-dodged
        if (!isCombo && defender.firstHitAvoided)
        {
            defender.firstHitAvoided = false;
            Emit(new CombatEvent { type = CombatEventType.Dodge, playerIndex = attacker.index, targetIndex = defender.index });
            return false;
        }

        // Monk: guards instead of attacking
        if (attacker.hitSpeed <= 0f) return false;

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

            // Reversal: depois de bloquear, defensor pode contra-atacar imediatamente — não
            // cancela o combo do atacante (continua normalmente; cada hit extra do combo
            // checa Reversal de novo, igual ao primeiro). reversalAfterBlock (Counter Attack
            // +90%) só soma AQUI, no caminho de pós-block — não entra no check de pós-hit
            // abaixo, que usa só ReversalChance() puro.
            if (Roll(ReversalChance(defender) + defender.reversalAfterBlock))
                SimulateRetaliation(defender, attacker, CombatEventType.Reversal);
            return false;
        }

        // Dodge check
        if (Roll(DodgeChance(attacker, defender)))
        {
            Emit(new CombatEvent { type = CombatEventType.Dodge, playerIndex = attacker.index, targetIndex = defender.index });
            return false;
        }

        // Counter: atacante já chegou perto e iria atacar, mas o defensor bate primeiro —
        // cancela completamente o hit do atacante (e o resto do combo dele, já que esse hit
        // nunca aconteceu de fato). Checado antes do dano normal — defensor nunca chega a
        // tomar o hit quando triggar.
        if (Roll(CounterChance(defender)))
        {
            SimulateRetaliation(defender, attacker, CombatEventType.Counter);
            return true;
        }

        // Normal hit — fórmula multiplicativa do My Brute
        bool  isCrit = Roll(CritChance(attacker));
        float dmg    = CalcDamage(attacker, isCrit);

        // Lead Skeleton: -15% heavy damage
        if (defender.leadSkeleton && attacker.currentWeaponData?.type == WeaponType.Heavy)
            dmg *= 0.85f;

        // Armor reduction
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - defender.armor)));

        Emit(new CombatEvent { type = CombatEventType.Hit, playerIndex = attacker.index, targetIndex = defender.index, damage = finalDamage, isCrit = isCrit, isCombo = isCombo });

        defender.hp = Mathf.Max(0, defender.hp - finalDamage);
        Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = defender.index, newHp = defender.hp, maxHp = defender.maxHp });

        if (!defender.isAlive) return false;

        // Reversal: depois de já ter tomado o hit, defensor contra-ataca imediatamente — não
        // cancela o combo do atacante (continua normalmente; cada hit extra do combo checa
        // Reversal de novo, igual a este).
        if (Roll(ReversalChance(defender)))
            SimulateRetaliation(defender, attacker, CombatEventType.Reversal);

        // Disarm (first hit only, not combo). attacker.isAlive: Reversal acima pode ter
        // matado o atacante na retaliação — sem essa checagem, um atacante já morto ainda
        // desarmava o defensor que acabou de contra-atacar.
        if (attacker.isAlive && !isCombo && defender.currentWeaponData != null && Roll(DisarmChance(attacker)))
        {
            string wn = defender.currentWeaponData.weaponName;
            defender.weaponLoadout.Remove(defender.currentWeaponData);
            defender.currentWeaponData = null;
            Emit(new CombatEvent { type = CombatEventType.Disarm, playerIndex = attacker.index, targetIndex = defender.index, weaponName = wn });
        }

        return false;
    }

    // Usado por Counter e Reversal: o contra-ataque do "retaliator" passa por esquiva/bloqueio/
    // crítico normalmente contra "target", mas NÃO verifica Counter/Reversal de novo (evita
    // recursão entre as duas mecânicas — uma retaliação é sempre só uma retaliação).
    private void SimulateRetaliation(PlayerState retaliator, PlayerState target, CombatEventType eventType)
    {
        // Ordem invertida em relação ao SimulateHit principal (Esquiva → Block): aqui o Block é
        // verificado primeiro — o alvo da retaliação prioriza se defender com a arma/escudo
        // antes de tentar esquivar.
        if (Roll(BlockChance(retaliator, target)))
        {
            Emit(new CombatEvent { type = CombatEventType.Block, playerIndex = retaliator.index, targetIndex = target.index });
            return;
        }
        if (Roll(DodgeChance(retaliator, target)))
        {
            Emit(new CombatEvent { type = CombatEventType.Dodge, playerIndex = retaliator.index, targetIndex = target.index });
            return;
        }

        bool  isCrit = Roll(CritChance(retaliator));
        float dmg    = CalcDamage(retaliator, isCrit);

        if (target.leadSkeleton && retaliator.currentWeaponData?.type == WeaponType.Heavy)
            dmg *= 0.85f;

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - target.armor)));

        Emit(new CombatEvent { type = eventType, playerIndex = retaliator.index, targetIndex = target.index, damage = finalDamage, isCrit = isCrit });

        target.hp = Mathf.Max(0, target.hp - finalDamage);
        Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = target.index, newHp = target.hp, maxHp = target.maxHp });
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

            Emit(new CombatEvent { type = CombatEventType.Hit, playerIndex = attacker.index, targetIndex = defender.index, damage = dmg, isThrow = true });
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
        // Deity (-100% evasion / "sem resistência a ser atingido"): zerar só o campo
        // defender.evasion não bastava, porque base por tipo de arma + bônus de AGI + bônus
        // da própria arma do defensor ainda davam alguma chance de esquiva. noEvasion ignora
        // tudo isso — chance de esquiva fica em 0% de verdade, não só o termo de skill.
        if (defender.noEvasion) return 0f;

        float baseChance = defender.currentWeaponData == null ? 0.10f :
            defender.currentWeaponData.type switch
            {
                WeaponType.Fast   => 0.20f,
                WeaponType.Dagger => 0.15f,
                WeaponType.Sword  => 0.10f,
                WeaponType.Heavy  => 0.05f,
                _                 => 0.10f
            };
        float agiBonus      = Mathf.Max(0, defender.agility - 3) * 0.02f;
        float weaponEvasion = defender.currentWeaponData != null
            ? defender.currentWeaponData.evasionBonus : UnarmedStats.EvasionBonus;
        // accuracy do atacante (Relentless +0.30) é o oposto de evasion — reduz a chance de
        // esquiva do defensor em vez de aumentar a do próprio atacante.
        float total = baseChance + agiBonus + defender.evasion + weaponEvasion - attacker.accuracy;
        return Mathf.Clamp(total, 0f, 0.60f);
    }

    private float BlockChance(PlayerState attacker, PlayerState defender)
    {
        float weaponBonus = defender.currentWeaponData == null ? 0f :
            defender.currentWeaponData.type switch
            {
                WeaponType.Block  => 0.50f,
                WeaponType.Dagger => 0.15f,
                WeaponType.Sword  => 0.15f,
                WeaponType.Heavy  => 0.15f,
                WeaponType.Slow   => 0.05f,
                _                 => 0f
            };
        float weaponBlockBonus = defender.currentWeaponData != null
            ? defender.currentWeaponData.blockBonus : UnarmedStats.BlockBonus;
        return weaponBonus + weaponBlockBonus + defender.blockBonus;
    }

    // Counter: atacante corre, ataca, e o defensor bate antes do hit conectar — cancela o
    // hit (e o resto do combo) do atacante. defender.counter era somado em BlockChance antes
    // dessa mecânica existir de fato (usado só pela skill Counter Attack) — agora vira o que
    // o nome já sugeria.
    private float CounterChance(PlayerState defender) => defender.counter;

    // Reversal: depois de já ter tomado o hit, defensor contra-ataca imediatamente, cancelando
    // o resto do combo do atacante. weaponData.reversalBonus já existe nos 5 assets (Sword
    // +0.10, Heavy -0.30) mas nunca tinha sido lido por nenhum código até essa mecânica existir.
    private float ReversalChance(PlayerState defender)
    {
        float weaponBonus = defender.currentWeaponData != null
            ? defender.currentWeaponData.reversalBonus : UnarmedStats.ReversalBonus;
        return defender.reversal + weaponBonus;
    }

    private float CritChance(PlayerState attacker)
    {
        float baseChance = attacker.currentWeaponData == null ? 0.05f :
            attacker.currentWeaponData.type switch
            {
                WeaponType.Dagger => 0.08f,
                WeaponType.Sword  => 0.05f,
                WeaponType.Heavy  => 0.03f,
                _                 => 0.05f
            };
        float weaponBonus = attacker.currentWeaponData != null
            ? attacker.currentWeaponData.critChanceBonus : UnarmedStats.CritChanceBonus;
        return baseChance + weaponBonus + attacker.criticalChance;
    }

    // comboCount = quantos hits extra de combo já aconteceram neste turno (0 = checagem do 1º hit extra).
    // Decaimento ×0.5 por hit consecutivo: 1º normal, 2º ×0.5, 3º ×0.25... aplicado depois do clamp,
    // para o teto de 35% valer como o pico (1º hit extra) e não ser "recuperado" pelo decaimento.
    private float ComboChance(PlayerState attacker, int comboCount = 0)
    {
        float baseChance = attacker.currentWeaponData == null ? 0.05f :
            attacker.currentWeaponData.type switch
            {
                WeaponType.Fast   => 0.18f,
                WeaponType.Dagger => 0.15f,
                WeaponType.Sword  => 0.12f,
                WeaponType.Heavy  => 0.04f,
                _                 => 0.12f
            };
        float agiBonus    = Mathf.Max(0, attacker.agility - 3) * 0.008f;
        float weaponCombo = attacker.currentWeaponData != null
            ? attacker.currentWeaponData.comboBonus : UnarmedStats.ComboBonus;
        float total = Mathf.Clamp(baseChance + agiBonus + attacker.comboChanceBonus + weaponCombo, 0f, 0.60f);
        return total * Mathf.Pow(0.5f, comboCount);
    }

    private float DisarmChance(PlayerState attacker)
    {
        float baseChance = attacker.currentWeaponData == null ? 0f :
            attacker.currentWeaponData.type switch
            {
                WeaponType.Dagger => 0.20f,
                WeaponType.Fast   => 0.15f,
                WeaponType.Sword  => 0.10f,
                WeaponType.Heavy  => 0.05f,
                _                 => 0f
            };
        float weaponBonus = attacker.currentWeaponData != null
            ? attacker.currentWeaponData.disarmBonus : UnarmedStats.DisarmBonus;
        return baseChance + weaponBonus;
    }

    private float ThrowChance(PlayerState attacker)
    {
        if (attacker.currentWeaponData == null) return 0f;
        return attacker.currentWeaponData.type switch
        {
            WeaponType.Thrown  => 1.00f,
            WeaponType.Dagger  => 0.15f,
            WeaponType.Fast    => 0.15f,
            WeaponType.Sword   => 0.15f,
            WeaponType.Heavy   => 0.10f,
            _                  => 0f
        };
    }

    // --- Damage calculations (mirrors WeaponBaseDamage / CalcDamage / ThrowDamage) ---

    // Dano base da arma (sem STR/crítico/armadura) — Heavy/Sword/Dagger têm variação aleatória estilo My Brute.
    private int WeaponBaseDamage(PlayerState attacker)
    {
        if (attacker.currentWeaponData == null)
            return UnarmedStats.Damage;
        return attacker.currentWeaponData.type switch
        {
            WeaponType.Heavy  => _rng.Next(30, 50),
            WeaponType.Sword  => _rng.Next(10, 18),
            WeaponType.Dagger => _rng.Next(7, 13),
            _                 => attacker.currentWeaponData.damage > 0 ? attacker.currentWeaponData.damage : 3
        };
    }

    private float CritDamageMultiplier(PlayerState attacker)
    {
        float baseMult = attacker.currentWeaponData != null
            ? attacker.currentWeaponData.critDamageMultiplier : UnarmedStats.CritDamageMultiplier;
        return baseMult + attacker.critDamageBonus;
    }

    // Fórmula multiplicativa do My Brute: weaponBaseDamage × (1 + str/10) × (critMultiplier se crítico).
    // Lead Skeleton e armadura são aplicados depois, em SimulateHit.
    private float CalcDamage(PlayerState attacker, bool isCrit)
    {
        int   weaponBaseDamage = WeaponBaseDamage(attacker);
        float strMult          = 1f + attacker.str / 10f;
        float critMult         = isCrit ? CritDamageMultiplier(attacker) : 1f;
        float result            = weaponBaseDamage * strMult * critMult;

        // Diagnóstico temporário: confirma os componentes exatos de cada hit (relatos de
        // dano muito alto — ex: Dagger chegando a ~25 normal/~50 crítico com STR 4-5, quando
        // o teto teórico da fórmula seria ~18/~22.5). Remover quando confirmado.
        string weaponLabel = attacker.currentWeaponData != null ? attacker.currentWeaponData.weaponName : "Unarmed";
        Debug.Log($"[CalcDamage] {attacker.name} arma={weaponLabel} weaponBaseDamage={weaponBaseDamage} " +
                  $"str={attacker.str} strMult={strMult:F2} critMult={critMult:F2} isCrit={isCrit} resultado={result:F1}");

        return result;
    }

    private int CalcThrowDamage(WeaponData data)
    {
        int result;
        if (data == null)
            result = 2;
        else
            result = data.type switch
            {
                WeaponType.Heavy  => _rng.Next(30, 50),
                WeaponType.Sword  => _rng.Next(10, 18),
                WeaponType.Dagger => _rng.Next(7, 13),
                _                 => data.damage > 0 ? data.damage : 3
            };

        // Diagnóstico temporário: confirma que o throw NÃO usa o multiplicador de STR.
        Debug.Log($"[CalcThrowDamage] arma={data?.weaponName ?? "?"} resultado={result} (sem STR)");
        return result;
    }

    // --- Utilities ---

    private bool Roll(float chance) => (float)_rng.NextDouble() < chance;
    private void Emit(CombatEvent evt) => _events.Add(evt);
}
