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

    // Lido por CombatSceneLoader depois de Simulate() pra pintar de vermelho os ícones
    // sabotados no WeaponHUD da vítima (ver Spy abaixo) — nomes, não referências, porque o
    // WeaponHUD lê do PlayerLoadout visual (profile.weaponLoadout.weapons original), que nunca
    // vê os clones sabotados criados só dentro do PlayerState do simulador.
    public List<string> Player1SabotagedWeapons { get; private set; } = new List<string>();
    public List<string> Player2SabotagedWeapons { get; private set; } = new List<string>();

    public List<CombatEvent> Simulate(PlayerProfile p1Profile, PlayerProfile p2Profile, int seed = -1)
    {
        Debug.Log("[CombatSimulator] Iniciando simulação...");

        _rng    = seed >= 0 ? new System.Random(seed) : new System.Random();
        _events = new List<CombatEvent>();

        _p1 = BuildState(p1Profile, index: 0);
        _p2 = BuildState(p2Profile, index: 1);

        // Diagnóstico temporário: confirma que os campos de WeaponData chegaram certos do
        // asset (pedido pelo usuário após suspeitar que damage não estava sendo lido depois
        // da migração de WeaponType único para WeaponData.types). Remover quando confirmado.
        LogWeaponLoadout(_p1);
        LogWeaponLoadout(_p2);

        // Saboteur ANTES de Spy (ordem pedida pelo usuário) — destrói 1 arma aleatória do
        // loadout do oponente e dá -100 initiative nele, antes de qualquer turno. Validar
        // Saboteur primeiro garante que Spy calcula "metade do loadout" já em cima do que
        // sobrou depois da destruição, em vez de sabotar (gastar a redução de -20% dano numa
        // arma específica) e o Saboteur depois simplesmente destruir essa mesma arma, jogando
        // fora o trabalho do Spy. Independente entre os dois lados (cada skill se aplica nos
        // dois sentidos, mesmo que só um lado tenha cada uma).
        ApplySaboteur(saboteur: _p1, victim: _p2);
        ApplySaboteur(saboteur: _p2, victim: _p1);

        // Spy: sabota a metade (arredondado pra baixo) das armas do loadout do OPONENTE — já
        // depois do Saboteur acima, então opera sobre o loadout já reduzido. Antes de qualquer
        // coisa acontecer na luta — inclusive antes do sorteio de arma inicial
        // (EquipStartingWeaponIfNeeded) abaixo, que já deve poder sortear uma arma sabotada.
        Player2SabotagedWeapons = ApplySpySabotage(spy: _p1, victim: _p2);
        Player1SabotagedWeapons = ApplySpySabotage(spy: _p2, victim: _p1);

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

    // Spy (exclusiva do LaBrute/eternaltwin, não existe no Muxxu original): metade das armas
    // do loadout da VÍTIMA (arredondado pra baixo), escolhidas aleatoriamente, têm o dano
    // reduzido em 20% — permanente pro resto da luta, não é um efeito por hit. Clona a
    // WeaponData em vez de mutar o asset original direto (victim.weaponLoadout guarda a MESMA
    // referência do ScriptableObject em disco, ver BuildState — mutar weapon.damage ali
    // corromperia o asset pra qualquer outra luta/personagem que use essa mesma arma).
    // Substitui a entrada no PlayerState.weaponLoadout pelo clone, então qualquer pickup/swap/
    // roubo que sortear essa arma depois já usa o dano reduzido automaticamente, sem precisar
    // de nenhum flag "sabotada" extra em PlayerState.
    private List<string> ApplySpySabotage(PlayerState spy, PlayerState victim)
    {
        var sabotaged = new List<string>();
        if (!spy.HasSkill("Spy")) return sabotaged;

        int count = Mathf.FloorToInt(victim.weaponLoadout.Count / 2f);
        if (count <= 0) return sabotaged;

        var pool = new List<int>();
        for (int i = 0; i < victim.weaponLoadout.Count; i++) pool.Add(i);

        for (int n = 0; n < count; n++)
        {
            int pick = _rng.Next(pool.Count);
            int idx  = pool[pick];
            pool.RemoveAt(pick);

            var original = victim.weaponLoadout[idx];
            var sabotagedWeapon = Object.Instantiate(original);
            sabotagedWeapon.damage = Mathf.RoundToInt(original.damage * 0.80f);
            victim.weaponLoadout[idx] = sabotagedWeapon;
            sabotaged.Add(sabotagedWeapon.weaponName);
        }

        Debug.Log($"[Spy] Armas sabotadas: {string.Join(", ", sabotaged)} (-20% dano).");
        return sabotaged;
    }

    // Saboteur (LaBrute): destrói permanentemente 1 arma aleatória do loadout do OPONENTE e dá
    // -100 initiative nele, antes de qualquer turno — vantagem de agir primeiro (quem age
    // primeiro compara initiative, ver Speed System). Sem efeito se a vítima não tiver arma
    // nenhuma no loadout (RemoveAt não tem o que remover). Diferente de Spy, aqui não há clone
    // nenhum — a arma simplesmente sai do loadout (RemoveAt), igual a um WeaponDrop/Disarm
    // qualquer, então não precisa de nenhum cuidado especial com o asset original.
    private void ApplySaboteur(PlayerState saboteur, PlayerState victim)
    {
        if (!saboteur.HasSkill("Saboteur")) return;
        if (victim.weaponLoadout.Count == 0) return;

        int idx = _rng.Next(victim.weaponLoadout.Count);
        var removed = victim.weaponLoadout[idx];
        victim.weaponLoadout.RemoveAt(idx);
        victim.initiative -= 100;

        Debug.Log($"[Saboteur] Arma destruída: {removed.weaponName} | initiative do oponente -100.");
        Emit(new CombatEvent { type = CombatEventType.Saboteur, playerIndex = saboteur.index, targetIndex = victim.index, weaponName = removed.weaponName });
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

    // Diagnóstico temporário (ver chamada em Simulate) — confirma nome, damage, types,
    // hitSpeed, critChanceBonus e comboBonus de cada WeaponData do loadout, lidos direto do
    // asset, antes de qualquer sorteio/equip da luta.
    private void LogWeaponLoadout(PlayerState s)
    {
        foreach (var w in s.weaponLoadout)
        {
            if (w == null) continue;
            string types = w.types != null ? string.Join(",", w.types) : "(none)";
            Debug.Log($"[WeaponLoadout] P{s.index + 1} {s.name}: arma={w.weaponName} damage={w.damage} " +
                      $"types=[{types}] hitSpeed={w.hitSpeed} critChanceBonus={w.critChanceBonus} comboBonus={w.comboBonus}");
        }
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
        // Bodybuilder (redefinida pelo usuário — era strPct += 0.5f/"STR × 1.5"): agora só dá
        // +10% evasion e +40% hit speed enquanto empunha arma Heavy, checado vivo em
        // DodgeChance() (evasion) e em CombatPlayer (hit speed, puramente visual — ver nota lá).
        // Sem estado fixo aqui: a arma equipada pode trocar durante a luta.

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
        // Shield: +45% block rate (blockBonus, mesmo campo de Counter Attack — soma em
        // BlockChance) e +25% armor (penalidade de mobilidade do escudo equipado). hasShield
        // habilita o desarme próprio do escudo (ver ShieldDisarmChance/SimulateHit) — se cair,
        // os dois bônus são revertidos (ver case ShieldDisarm). Visual (sprite no braço oposto)
        // é equipado fora daqui, em CombatSceneLoader.Initialize — PlayerState não tem GameObject.
        if (s.HasSkill("Shield"))              { s.blockBonus += 0.45f; s.armor += 0.25f; s.hasShield = true; }
        if (s.HasSkill("Untouchable"))         { s.evasion += 0.30f; }
        if (s.HasSkill("Relentless"))          { s.accuracy += 0.30f; }
        if (s.HasSkill("Fists of Fury"))       { s.comboChanceBonus += 0.20f; }
        // Lead Skeleton (redefinida — antes só dava -15% dano de Heavy): +15% armor, -15%
        // evasion, mantendo o -15% dano de arma blunt (Heavy) já existente (ver SimulateHit/
        // SimulateRetaliation). Floor de evasion em 0 garantido pelo clamp incondicional abaixo.
        if (s.HasSkill("Lead Skeleton"))       { s.leadSkeleton = true; s.armor += 0.15f; s.evasion -= 0.15f; }
        if (s.HasSkill("Ballet Shoes"))        { s.evasion += 0.10f; s.firstHitAvoided = true; }
        if (s.HasSkill("First Strike"))        { s.initiative += 200; }
        if (s.HasSkill("Counter Attack"))      { s.blockBonus += 0.10f; s.reversalAfterBlock += 0.90f; }
        if (s.HasSkill("Sixth Sense"))         { s.counter += 0.10f; }
        if (s.HasSkill("Hostility"))            { s.reversal += 0.30f; }
        if (s.HasSkill("Monk"))                { s.counter += 0.40f; s.initiative -= 200; s.hitSpeed = 0f; }
        if (s.HasSkill("Martial Arts"))         { s.martialArts = true; }
        if (s.HasSkill("Shock"))                { s.disarmChanceBonus += 0.50f; }
        if (s.HasSkill("Weapon Master"))        { s.weaponsMaster = true; }
        // Sticky Hands: -50% chance de ser desarmado (DisarmChance) e -50% chance de arremesso,
        // incluindo o próprio (ThrowChance) — campo numérico em vez de bool, lido direto como
        // multiplicador (1 - stickyHands) nas duas fórmulas, ver CLAUDE.md.
        if (s.HasSkill("Sticky Hands"))         { s.stickyHands += 0.50f; }

        // Aplicado por último, depois de Untouchable/Ballet Shoes/Lead Skeleton já terem somado
        // ou subtraído evasion — garante que Deity zere o total mesmo que outra skill já tenha
        // alterado evasion antes. Incondicional (não só quando evasionPct != 0) pra também
        // garantir o floor em 0 quando só Lead Skeleton (-15% flat) deixa o total negativo.
        s.evasion = Mathf.Max(0f, s.evasion * (1f + evasionPct));
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

        // Chaining: turno consumido por estar estunado — pula a ação inteira (sem Thief/
        // pickup/swap/throw/melee), só decrementa o contador e emite StunSkip pra
        // CombatPlayer encerrar o loop de Hurt + remover a label (ver PlayerCombat.HideStunLabel).
        // Checado antes até do isFirstTurn abaixo — um turno pulado por stun não é a "primeira
        // ação real" do personagem pra fins de Troca de Arma.
        if (attacker.stunnedActions > 0)
        {
            attacker.stunnedActions--;
            Emit(new CombatEvent { type = CombatEventType.StunSkip, playerIndex = attacker.index });
            Emit(new CombatEvent { type = CombatEventType.TurnEnd, playerIndex = attacker.index });
            return;
        }

        // Primeiro turno de verdade deste atacante na luta (independente de round/ações extra
        // por Speed) — usado só pra bloquear Troca de Arma no item 2b abaixo, ver lá o motivo.
        bool isFirstTurn = !attacker.hasTakenFirstTurn;
        attacker.hasTakenFirstTurn = true;

        // 1. Thief: rouba a arma do oponente se eu estiver desarmado e ele armado — 44% por
        // turno, no máximo 2 vezes por luta (thiefUsesRemaining). Checado ANTES do pickup normal
        // abaixo — os dois exigem estar desarmado, então só um pode acontecer no mesmo turno; se
        // Thief não triggar (sem usos restantes, oponente desarmado, ou o roll falhar), cai pro
        // pickup comum normalmente.
        bool stoleWeapon = false;
        if (attacker.currentWeaponData == null && defender.currentWeaponData != null
            && attacker.HasSkill("Thief") && attacker.thiefUsesRemaining > 0 && Roll(0.44f))
        {
            var stolen = defender.currentWeaponData;
            defender.weaponLoadout.Remove(stolen);
            defender.currentWeaponData = null;
            attacker.weaponLoadout.Add(stolen);
            attacker.currentWeaponData = stolen;
            attacker.thiefUsesRemaining--;
            Emit(new CombatEvent { type = CombatEventType.Thief, playerIndex = attacker.index, targetIndex = defender.index, weaponName = stolen.weaponName });
            stoleWeapon = true;
        }

        // 2. Pick up weapon if unarmed (40% chance) — só se Thief não tiver acontecido acima.
        if (!stoleWeapon && attacker.currentWeaponData == null && attacker.weaponLoadout.Count > 0 && Roll(0.40f))
        {
            var w = attacker.weaponLoadout[_rng.Next(attacker.weaponLoadout.Count)];
            attacker.currentWeaponData = w;
            Emit(new CombatEvent { type = CombatEventType.PickupWeapon, playerIndex = attacker.index, weaponName = w.weaponName });
        }
        // 2b. Weapon swap if armed (mesma 40% chance do pickup acima) — mecânica geral, vale pra
        // todo mundo (não é skill, pedido pelo usuário como ação independente de Hideaway). Joga
        // a arma atual no chão e ela some do loadout/HUB pra sempre (igual a WeaponDrop — não dá
        // pra sacar de novo) e puxa uma nova arma aleatória do loadout (evita repetir a mesma, se
        // houver outra opção). !isFirstTurn: quem já nasce armado (EquipStartingWeaponIfNeeded,
        // 40% antes da luta começar) não pode trocar logo no 1º turno — só teve a arma há um
        // instante, ainda nem atacou com ela (pedido pelo usuário).
        else if (!isFirstTurn && !stoleWeapon && attacker.currentWeaponData != null && attacker.weaponLoadout.Count > 1 && Roll(0.40f))
        {
            var oldWeapon = attacker.currentWeaponData;
            int idx = _rng.Next(attacker.weaponLoadout.Count);
            var newWeapon = attacker.weaponLoadout[idx];
            if (newWeapon == oldWeapon)
                newWeapon = attacker.weaponLoadout[(idx + 1) % attacker.weaponLoadout.Count];

            Emit(new CombatEvent { type = CombatEventType.WeaponSwap, playerIndex = attacker.index, weaponName = oldWeapon.weaponName });
            attacker.weaponLoadout.Remove(oldWeapon);
            attacker.currentWeaponData = newWeapon;
            Emit(new CombatEvent { type = CombatEventType.PickupWeapon, playerIndex = attacker.index, weaponName = newWeapon.weaponName });
        }

        // 3. Check throw before melee — Hideaway dá 50% fixo (ver ThrowChance), Sticky Hands
        // multiplica essa chance (qualquer origem) por (1 - stickyHands), e Monk (hitSpeed = 0)
        // guarda em vez de atacar — arremessar é um ataque como outro qualquer, então também
        // não acontece pra ele (senão ele jogava a arma e corria o resto do turno normalmente,
        // contradizendo o "guarda em vez de atacar" — bug real reportado pelo usuário).
        if (attacker.hitSpeed > 0f && attacker.currentWeaponData != null && Roll(ThrowChance(attacker)))
        {
            SimulateThrow(attacker, defender);
            Emit(new CombatEvent { type = CombatEventType.TurnEnd, playerIndex = attacker.index });
            return;
        }

        // 4. Melee
        // Monk (hitSpeed = 0): guarda em vez de atacar — não corre até o adversário.
        if (attacker.hitSpeed > 0f)
            Emit(new CombatEvent { type = CombatEventType.RunToDefender, playerIndex = attacker.index, targetIndex = defender.index });
        bool interrupted = SimulateHitWithDetermination(attacker, defender, isCombo: false, out bool _);
        SimulateComboLoop(attacker, defender, interrupted);

        Emit(new CombatEvent { type = CombatEventType.TurnEnd, playerIndex = attacker.index });
    }

    // Combo loop (mirrors AttackRoutine's while loop). Each consecutive extra hit decays the
    // chance by ×0.5 (1st extra hit normal, 2nd ×0.5, 3rd ×0.25, ...) — mirrors My Brute, where
    // combo probability drops sharply after the first follow-up. Counter zera o resto do combo
    // (o hit nunca aconteceu de fato), assim como Iron Head (atacante perde a arma, sem
    // condições de continuar a sequência) — Dodge, Block e Reversal não interrompem nada, o
    // combo continua normal depois deles. Reversal pode disparar de novo em cada hit extra do
    // combo, independente do(s) anterior(es).
    private void SimulateComboLoop(PlayerState attacker, PlayerState defender, bool interrupted)
    {
        int comboCount = 0;
        while (!interrupted && attacker.isAlive && defender.isAlive)
        {
            float comboChance = ComboChance(attacker, comboCount);
            string weaponLabel = attacker.currentWeaponData != null ? attacker.currentWeaponData.weaponName : "Unarmed";
            Debug.Log($"[ComboChance] {attacker.name} (P{attacker.index + 1}, arma={weaponLabel}) hit extra #{comboCount + 1} chance={comboChance:P1}");
            if (!Roll(comboChance)) break;
            interrupted = SimulateHitWithDetermination(attacker, defender, isCombo: true, out bool _);
            comboCount++;
        }
    }

    // --- Hit resolution (mirrors HitRoutine) ---

    // Determination: se o golpe não causa dano ao oponente (esquivou, bloqueou, ou o defensor
    // deu Counter), 60% de chance do atacante tentar OUTRO golpe imediatamente — sempre
    // isCombo: false (é uma tentativa nova, não uma continuação do combo, então fica elegível
    // pro desarme de "primeiro hit" de novo). Recursivo: cada nova tentativa que também falhar
    // rola os mesmos 60% de novo, até acertar de verdade (damageDealt = true) ou a chance falhar.
    // Chamado tanto pro primeiro golpe do turno quanto por cada hit do loop de combo em
    // SimulateTurn — os dois usam este wrapper em vez de SimulateHit direto, então Determination
    // se aplica igual nos dois casos.
    private bool SimulateHitWithDetermination(PlayerState attacker, PlayerState defender, bool isCombo, out bool damageDealt)
    {
        bool interrupted = SimulateHit(attacker, defender, isCombo, out damageDealt);

        while (!damageDealt && attacker.HasSkill("Determination") && attacker.isAlive && defender.isAlive && Roll(0.60f))
            interrupted = SimulateHit(attacker, defender, isCombo: false, out damageDealt);

        return interrupted;
    }

    // Retorna true se o combo do atacante deve ser interrompido (Counter do defensor, ou
    // Iron Head derrubando a arma do atacante) — false em qualquer outro desfecho (incluindo
    // Dodge/Block/Reversal, que não interrompem). damageDealt (out): true só quando o dano
    // normal de fato foi aplicado ao defensor — usado por Determination (ver
    // SimulateHitWithDetermination acima) pra saber quando NÃO tentar de novo. No guard do Monk
    // abaixo, setamos damageDealt = true mesmo sem dano real — esse guard não representa uma
    // tentativa de golpe que falhou, é a ausência completa de ataque (Monk guarda), então não
    // deveria nunca disparar um retry de Determination (evitaria ficar girando indefinidamente
    // num personagem com hitSpeed = 0).
    private bool SimulateHit(PlayerState attacker, PlayerState defender, bool isCombo, out bool damageDealt)
    {
        damageDealt = false;

        // Monk: guards instead of attacking — checado ANTES do Ballet Shoes abaixo. Um hit que
        // nunca aconteceu (Monk não ataca) não deveria gastar o "esquiva o 1º golpe" do
        // defensor nem emitir um evento Dodge — sem essa ordem, CombatPlayer reposicionava e
        // fazia Monk correr+golpear visualmente (RepositionIfNeeded do evento Dodge) num turno
        // em que ele deveria ficar parado, e o jump-back de TurnEnd disparava depois só por
        // causa desse deslocamento indevido (bug real reportado pelo usuário: "saltos quando
        // não deveria se mexer").
        if (attacker.hitSpeed <= 0f) { damageDealt = true; return false; }

        // Ballet Shoes: first hit of the fight auto-dodged
        if (!isCombo && defender.firstHitAvoided)
        {
            defender.firstHitAvoided = false;
            Emit(new CombatEvent { type = CombatEventType.Dodge, playerIndex = attacker.index, targetIndex = defender.index });
            return false;
        }

        // Counter: atacante já chegou perto e iria atacar, mas o defensor bate primeiro —
        // cancela completamente o hit do atacante (e o resto do combo dele, já que esse hit
        // nunca aconteceu de fato). Checado ANTES de Block/Dodge agora — Counter representa o
        // defensor tomando a iniciativa de volta antes mesmo de precisar se defender; só se
        // Counter não disparar é que o defensor tenta bloquear (Monk/Sixth Sense davam counter
        // mas raramente chegavam a disparar de fato, já que Block/Dodge sempre tinham a primeira
        // chance e, ao suceder, retornavam antes do Counter ser checado — bug real reportado
        // pelo usuário: Monk não reagia nem depois de bloquear nem ao tomar hit).
        if (Roll(CounterChance(defender)))
        {
            SimulateRetaliation(defender, attacker, CombatEventType.Counter);
            return true;
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
            // Defender may drop weapon/shield on impact — escudo tem prioridade: enquanto o
            // defensor tiver Shield equipado, só o escudo pode cair neste hit (protege a arma
            // por baixo dele, igual ao My Brute); só depois que o escudo já caiu (aqui ou em
            // hit anterior) é que a arma propriamente passa a correr risco de cair ao bloquear.
            // Shield usa o mesmo ShieldDisarmChance fixo (sem disarmChanceBonus/disarmBonus) e
            // popup "DROP!" (não "DISARM!" — foi o próprio impacto do bloqueio, não um desarme
            // ativo do atacante, mesma distinção de WeaponDrop vs Disarm).
            if (defender.hasShield)
            {
                if (Roll(ShieldDisarmChance))
                {
                    defender.hasShield   = false;
                    defender.blockBonus -= 0.45f;
                    defender.armor      -= 0.25f;
                    Emit(new CombatEvent { type = CombatEventType.ShieldDrop, playerIndex = defender.index });
                }
            }
            else if (defender.currentWeaponData != null && Roll(0.10f))
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

        // Normal hit — fórmula multiplicativa do My Brute
        damageDealt = true;
        bool  isCrit = Roll(CritChance(attacker));
        float dmg    = CalcDamage(attacker, isCrit);

        // Resistant: cap no dano bruto, antes de Lead Skeleton/armadura (ver ApplyResistantCap).
        dmg = ApplyResistantCap(defender, dmg);

        // Lead Skeleton: -15% dano de arma blunt (Heavy)
        if (defender.leadSkeleton && WeaponData.IsBlunt(attacker.currentWeaponData))
            dmg *= 0.85f;

        // Armor reduction
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - defender.armor)));

        defender.hp = ApplyDamage(defender, finalDamage);
        // newHp/maxHp também vão no evento Hit (não só no HealthChanged seguinte) — sem isso,
        // CombatPlayer não tem como saber que Survival salvou o defensor em 1 HP em vez do
        // valor negativo que o dano bruto (finalDamage) produziria, e aplicava o dano cheio
        // direto na HealthSystem ao vivo, zerando a barra visualmente até o próximo evento
        // corrigir (ver Survival no CLAUDE.md).
        Emit(new CombatEvent { type = CombatEventType.Hit, playerIndex = attacker.index, targetIndex = defender.index, damage = finalDamage, isCrit = isCrit, isCombo = isCombo, newHp = defender.hp, maxHp = defender.maxHp });
        Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = defender.index, newHp = defender.hp, maxHp = defender.maxHp });

        // Chaining: 3 golpes consecutivos sem tomar dano (ApplyDamage zera o streak assim que
        // o atacante toma qualquer dano, de qualquer origem — Hit, Counter, Reversal ou Throw)
        // estuna o defensor por 1 ação dele. Só conta hit melee de verdade (este ponto) — não
        // Counter/Reversal/Throw, fora do escopo descrito pelo usuário ("atacar"/"combar").
        if (attacker.HasSkill("Chaining"))
        {
            attacker.chainHitStreak++;
            if (attacker.chainHitStreak >= 3)
            {
                attacker.chainHitStreak = 0;
                defender.stunnedActions++;
                Emit(new CombatEvent { type = CombatEventType.Stunned, playerIndex = attacker.index, targetIndex = defender.index });

                // Estuna também derruba a arma do estunado, se ele tiver uma — pedido pelo
                // usuário. Reusa o mesmo CombatEventType.Disarm do desarme normal (sem chance
                // nenhuma envolvida, é garantido) — CombatPlayer já sabe tocar a queda em
                // pêndulo via PlayerCombat.DropWeapon, sem precisar de nenhum case novo.
                if (defender.currentWeaponData != null)
                {
                    string stunnedWn = defender.currentWeaponData.weaponName;
                    defender.weaponLoadout.Remove(defender.currentWeaponData);
                    defender.currentWeaponData = null;
                    Emit(new CombatEvent { type = CombatEventType.Disarm, playerIndex = attacker.index, targetIndex = defender.index, weaponName = stunnedWn });
                }
            }
        }

        if (!defender.isAlive) return false;

        // Iron Head: logo após sofrer o dano (qualquer hit, incluindo combo — não só o
        // primeiro, diferente do Disarm abaixo), +40% chance do DEFENSOR derrubar a arma do
        // ATACANTE (inverso do Disarm, que é o atacante desarmando o defensor). Igual ao
        // Counter, interrompe o resto do combo deste turno — sem arma na mão, o atacante não
        // continua a sequência (antes só zerava a arma e o combo seguia normalmente, desarmado).
        bool ironHeadTriggered = false;
        if (defender.HasSkill("Iron Head") && attacker.currentWeaponData != null && Roll(0.40f))
        {
            ironHeadTriggered = true;
            string ihWn = attacker.currentWeaponData.weaponName;
            attacker.weaponLoadout.Remove(attacker.currentWeaponData);
            attacker.currentWeaponData = null;
            Emit(new CombatEvent { type = CombatEventType.WeaponDrop, playerIndex = attacker.index, weaponName = ihWn });
        }

        // Reversal: depois de já ter tomado o hit, defensor contra-ataca imediatamente — não
        // cancela o combo do atacante (continua normalmente; cada hit extra do combo checa
        // Reversal de novo, igual a este).
        if (Roll(ReversalChance(defender)))
            SimulateRetaliation(defender, attacker, CombatEventType.Reversal);

        // Desarme (first hit only, not combo). attacker.isAlive: Reversal acima pode ter matado
        // o atacante na retaliação — sem essa checagem, um atacante já morto ainda desarmava o
        // defensor que acabou de contra-atacar. Escudo tem prioridade — enquanto o defensor
        // tiver Shield equipado, só ele pode ser desarmado neste hit (protege a arma por baixo
        // dele); só depois que o escudo já caiu (aqui ou em hit/bloqueio anterior) a arma passa
        // a correr risco de desarme de verdade. ShieldDisarmChance é fixa, sem somar
        // DisarmChance(attacker) (disarmChanceBonus de Shock, disarmBonus da arma do atacante,
        // ou a futura Impact não afetam essa chance, ver CLAUDE.md).
        if (attacker.isAlive && !isCombo)
        {
            if (defender.hasShield)
            {
                if (Roll(ShieldDisarmChance))
                {
                    defender.hasShield   = false;
                    defender.blockBonus -= 0.45f;
                    defender.armor      -= 0.25f;
                    Emit(new CombatEvent { type = CombatEventType.ShieldDisarm, playerIndex = attacker.index, targetIndex = defender.index });
                }
            }
            else if (defender.currentWeaponData != null && Roll(DisarmChance(attacker, defender)))
            {
                string wn = defender.currentWeaponData.weaponName;
                defender.weaponLoadout.Remove(defender.currentWeaponData);
                defender.currentWeaponData = null;
                Emit(new CombatEvent { type = CombatEventType.Disarm, playerIndex = attacker.index, targetIndex = defender.index, weaponName = wn });
            }
        }

        return ironHeadTriggered;
    }

    // Usado por Counter e Reversal: o contra-ataque do "retaliator" passa por esquiva/bloqueio/
    // crítico normalmente contra "target", mas NÃO verifica Counter/Reversal de novo (evita
    // recursão entre as duas mecânicas — uma retaliação é sempre só uma retaliação).
    private void SimulateRetaliation(PlayerState retaliator, PlayerState target, CombatEventType eventType)
    {
        // Sem Counter aqui (ver doc do método acima — retaliação nunca recursa em Counter/
        // Reversal). Block é verificado antes de Dodge — o alvo da retaliação prioriza se
        // defender com a arma/escudo antes de tentar esquivar, mesma ordem do SimulateHit
        // principal (Counter → Block → Dodge) depois do Counter já ter sido descartado.
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

        // Resistant: cap no dano bruto, antes de Lead Skeleton/armadura (ver ApplyResistantCap).
        dmg = ApplyResistantCap(target, dmg);

        if (target.leadSkeleton && WeaponData.IsBlunt(retaliator.currentWeaponData))
            dmg *= 0.85f;

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - target.armor)));

        target.hp = ApplyDamage(target, finalDamage);
        Emit(new CombatEvent { type = eventType, playerIndex = retaliator.index, targetIndex = target.index, damage = finalDamage, isCrit = isCrit, newHp = target.hp, maxHp = target.maxHp });
        Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = target.index, newHp = target.hp, maxHp = target.maxHp });

        // Iron Head: mesma checagem de SimulateHit — target acabou de sofrer o dano da
        // retaliação, então pode derrubar a arma de quem retaliou.
        if (target.HasSkill("Iron Head") && retaliator.currentWeaponData != null && Roll(0.40f))
        {
            string ihWn = retaliator.currentWeaponData.weaponName;
            retaliator.weaponLoadout.Remove(retaliator.currentWeaponData);
            retaliator.currentWeaponData = null;
            Emit(new CombatEvent { type = CombatEventType.WeaponDrop, playerIndex = retaliator.index, weaponName = ihWn });
        }
    }

    // --- Throw resolution ---

    private void SimulateThrow(PlayerState attacker, PlayerState defender)
    {
        var  weaponData = attacker.currentWeaponData;
        // HasType (não tipo único) — uma arma pode ter Thrown combinado com outra tag (ex: uma
        // adaga Sharp+Thrown), e ainda assim deve seguir o caminho "volta pro loadout" abaixo.
        bool isThrown   = WeaponData.HasType(weaponData, WeaponType.Thrown);
        string wn       = weaponData?.weaponName ?? "";

        // Hideaway: a arma some da mão igual a qualquer arma Thrown (Unequip — não
        // UnequipPermanent), mesmo sem ter a tag — fica desarmado até o próprio TurnStart
        // seguinte, mas a arma continua no loadout, podendo ser pega de novo num pickup futuro
        // (40% normal). Sem a skill e sem a tag Thrown, a arma sai do loadout pra sempre
        // (UnequipPermanent).
        bool staysInLoadout = isThrown || attacker.HasSkill("Hideaway");
        if (!staysInLoadout)
        {
            attacker.weaponLoadout.Remove(weaponData);
        }
        attacker.currentWeaponData = null;

        Emit(new CombatEvent { type = CombatEventType.ThrowWeapon, playerIndex = attacker.index, targetIndex = defender.index, weaponName = wn });

        // Hideaway: +25% block contra arremessos recebidos — reduz direto a chance de acerto do
        // throw (80% → 55%, miss sobe de 20% pra 45%), em vez de um 3º resultado separado de
        // Block. Mais simples que a versão anterior (Roll(ThrowBlockChance) + evento Block
        // próprio) e bate com os valores oficiais do LaBrute.
        float hitChance = 0.80f - (defender.HasSkill("Hideaway") ? 0.25f : 0f);
        if (Roll(hitChance))
        {
            int dmg = CalcThrowDamage(attacker, weaponData);
            // Resistant: cap no dano bruto, antes da armadura (ver ApplyResistantCap).
            dmg = Mathf.RoundToInt(ApplyResistantCap(defender, dmg));
            if (defender.armor > 0f)
                dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - defender.armor)));

            defender.hp = ApplyDamage(defender, dmg);
            Emit(new CombatEvent { type = CombatEventType.Hit, playerIndex = attacker.index, targetIndex = defender.index, damage = dmg, isThrow = true, newHp = defender.hp, maxHp = defender.maxHp });
            Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = defender.index, newHp = defender.hp, maxHp = defender.maxHp });
        }
        else
        {
            Emit(new CombatEvent { type = CombatEventType.Miss, playerIndex = attacker.index, targetIndex = defender.index });
        }

        // Sem re-equip imediato aqui — fica desarmado até o próprio TurnStart do próximo turno
        // dele, que já tem o check normal de 40% pra pegar arma (mesmo padrão de qualquer outro
        // turno desarmado). Era 40% de chance de reequipar na hora, mas isso fazia o personagem
        // ocasionalmente "equipar" uma arma já no fim do turno (depois do hit/miss do arremesso),
        // sem nenhuma ação visível além da troca de ícone — bug real reportado pelo usuário: o
        // pickup deveria sempre acontecer no início do turno, nunca no meio/fim.
    }

    // --- Chance calculations (mirrors PlayerCombat methods) ---

    // Soma os valores-base de cada tag presente na arma (WeaponData.types é uma lista — uma
    // arma pode ter até 3 tags simultâneas) — ver tabela em CLAUDE.md. Tags sem entrada
    // explícita (Blunt/Long, exceto onde passadas) contribuem 0; não existe mais um "default"
    // genérico, cada bônus vem de uma tag específica.
    private static float TagSum(WeaponData data, float sharp = 0f, float fast = 0f, float heavy = 0f, float thrown = 0f)
    {
        if (data == null) return 0f;
        float total = 0f;
        if (data.HasType(WeaponType.Sharp))  total += sharp;
        if (data.HasType(WeaponType.Fast))   total += fast;
        if (data.HasType(WeaponType.Heavy))  total += heavy;
        if (data.HasType(WeaponType.Thrown)) total += thrown;
        return total;
    }

    private float DodgeChance(PlayerState attacker, PlayerState defender)
    {
        // Deity (-100% evasion / "sem resistência a ser atingido"): zerar só o campo
        // defender.evasion não bastava, porque base por tipo de arma + bônus de AGI + bônus
        // da própria arma do defensor ainda davam alguma chance de esquiva. noEvasion ignora
        // tudo isso — chance de esquiva fica em 0% de verdade, não só o termo de skill.
        if (defender.noEvasion) return 0f;

        float baseChance = defender.currentWeaponData == null
            ? 0.10f
            : TagSum(defender.currentWeaponData, sharp: 0.10f, fast: 0.05f, heavy: 0.05f);
        float agiBonus      = Mathf.Max(0, defender.agility - 3) * 0.02f;
        float weaponEvasion = defender.currentWeaponData != null
            ? defender.currentWeaponData.evasionBonus : UnarmedStats.EvasionBonus;
        // accuracy do atacante (Relentless +0.30) é o oposto de evasion — reduz a chance de
        // esquiva do defensor em vez de aumentar a do próprio atacante.
        // Survival: +20% evasion, só enquanto hp == 1 (sai do estado se recuperar HP, e nunca
        // ativa em outro valor de HP, mesmo baixo) — checado vivo a cada chamada, sem flag fixa.
        float survivalBonus = (defender.hp == 1 && defender.HasSkill("Survival")) ? 0.20f : 0f;
        // Bodybuilder: +10% evasion ("dexterity"), só enquanto empunha arma Heavy — checado
        // vivo contra a arma atual, igual ao Survival acima (sem flag fixa de ApplySkillStats).
        float bodybuilderBonus = (WeaponData.HasType(defender.currentWeaponData, WeaponType.Heavy) && defender.HasSkill("Bodybuilder")) ? 0.10f : 0f;
        float total = baseChance + agiBonus + defender.evasion + weaponEvasion - attacker.accuracy + survivalBonus + bodybuilderBonus;
        return Mathf.Clamp(total, 0f, 0.60f);
    }

    private float BlockChance(PlayerState attacker, PlayerState defender)
    {
        float weaponBonus = defender.currentWeaponData == null
            ? 0f
            : TagSum(defender.currentWeaponData, sharp: 0.15f, heavy: 0.15f);
        float weaponBlockBonus = defender.currentWeaponData != null
            ? defender.currentWeaponData.blockBonus : UnarmedStats.BlockBonus;
        // Survival: +20% block, mesma condição de hp == 1 do DodgeChance acima.
        float survivalBonus = (defender.hp == 1 && defender.HasSkill("Survival")) ? 0.20f : 0f;
        return weaponBonus + weaponBlockBonus + defender.blockBonus + survivalBonus;
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
        float baseChance = attacker.currentWeaponData == null
            ? 0.05f
            : TagSum(attacker.currentWeaponData, sharp: 0.05f, fast: 0.03f, heavy: 0.03f);
        float weaponBonus = attacker.currentWeaponData != null
            ? attacker.currentWeaponData.critChanceBonus : UnarmedStats.CritChanceBonus;
        return baseChance + weaponBonus + attacker.criticalChance;
    }

    // comboCount = quantos hits extra de combo já aconteceram neste turno (0 = checagem do 1º hit extra).
    // Decaimento ×0.5 por hit consecutivo: 1º normal, 2º ×0.5, 3º ×0.25... aplicado depois do clamp,
    // para o teto de 35% valer como o pico (1º hit extra) e não ser "recuperado" pelo decaimento.
    private float ComboChance(PlayerState attacker, int comboCount = 0)
    {
        float baseChance = attacker.currentWeaponData == null
            ? 0.05f
            : TagSum(attacker.currentWeaponData, sharp: 0.12f, fast: 0.03f, heavy: 0.04f);
        float agiBonus    = Mathf.Max(0, attacker.agility - 3) * 0.008f;
        float weaponCombo = attacker.currentWeaponData != null
            ? attacker.currentWeaponData.comboBonus : UnarmedStats.ComboBonus;
        float total = Mathf.Clamp(baseChance + agiBonus + attacker.comboChanceBonus + weaponCombo, 0f, 0.60f);
        return total * Mathf.Pow(0.5f, comboCount);
    }

    // Sticky Hands (defender): multiplica a chance final por (1 - stickyHands) — 50% reduz a
    // chance de o defensor ser desarmado pela metade, em cima de qualquer outro bônus do
    // atacante (Shock/disarmBonus da arma).
    private float DisarmChance(PlayerState attacker, PlayerState defender)
    {
        float baseChance = attacker.currentWeaponData == null
            ? 0f
            : TagSum(attacker.currentWeaponData, sharp: 0.10f, fast: 0.10f, heavy: 0.05f);
        float weaponBonus = attacker.currentWeaponData != null
            ? attacker.currentWeaponData.disarmBonus : UnarmedStats.DisarmBonus;
        float total = baseChance + weaponBonus + attacker.disarmChanceBonus;
        return total * (1f - defender.stickyHands);
    }

    // Shield: chance fixa de cair, bem menor que arma normal — diferente de DisarmChance(),
    // não soma disarmChanceBonus (Shock) nem disarmBonus de arma do atacante, e a futura Impact
    // (+15% disarm) também não deve aumentar essa chance (ver CLAUDE.md, roadmap de Shield).
    private const float ShieldDisarmChance = 0.10f;

    // Hideaway: 50% fixo, substitui a soma por tag (não soma a ela) — valores oficiais do
    // LaBrute. Sticky Hands multiplica o resultado por (1 - stickyHands), seja a chance base ou
    // o fixo de Hideaway — dificulta jogar a própria arma fora até por acidente.
    private float ThrowChance(PlayerState attacker)
    {
        if (attacker.currentWeaponData == null) return 0f;
        float chance = attacker.HasSkill("Hideaway")
            ? 0.50f
            : TagSum(attacker.currentWeaponData, sharp: 0.15f, heavy: 0.10f, thrown: 1.00f);
        return chance * (1f - attacker.stickyHands);
    }

    // --- Damage calculations (mirrors WeaponBaseDamage / CalcDamage / ThrowDamage) ---

    // weaponData.damage tem prioridade absoluta — cada WeaponData tem seu próprio campo
    // configurável, então não há mais ranges hardcoded por tag (Random.Range(7,13)/(10,18)/
    // (30,50) eram valores padrão do protótipo, de antes de cada arma ter o próprio Damage).
    private static int RollWeaponDamage(WeaponData data) => data.damage > 0 ? data.damage : 3;

    // Dano base da arma (sem STR/crítico/armadura).
    private int WeaponBaseDamage(PlayerState attacker)
    {
        if (attacker.currentWeaponData == null)
            return attacker.martialArts ? UnarmedStats.Damage * 2 : UnarmedStats.Damage;
        return RollWeaponDamage(attacker.currentWeaponData);
    }

    private float CritDamageMultiplier(PlayerState attacker)
    {
        float baseMult = attacker.currentWeaponData != null
            ? attacker.currentWeaponData.critDamageMultiplier : UnarmedStats.CritDamageMultiplier;
        return baseMult + attacker.critDamageBonus;
    }

    // Fórmula do My Brute original: STR soma direto no dano base da arma (flat, não percentual)
    // — (weaponBaseDamage + str) × critMultiplier × sharpMult. Lead Skeleton e armadura são
    // aplicados depois, em SimulateHit. Era weaponBaseDamage × (1 + str/10) (percentual,
    // divergia do original) — redefinida pelo usuário.
    private float CalcDamage(PlayerState attacker, bool isCrit)
    {
        int   weaponBaseDamage = WeaponBaseDamage(attacker);
        float critMult         = isCrit ? CritDamageMultiplier(attacker) : 1f;
        // Weapon Master: +50% dano com arma "sharp" (tag Sharp) — checado vivo contra a
        // arma atual (pode trocar de arma durante a luta), não um flag fixo de ApplySkillStats.
        bool  isSharp          = WeaponData.IsSharp(attacker.currentWeaponData);
        float sharpMult        = (attacker.weaponsMaster && isSharp) ? 1.5f : 1f;
        float result            = (weaponBaseDamage + attacker.str) * critMult * sharpMult;

        // Diagnóstico temporário: confirma os componentes exatos de cada hit. Remover quando confirmado.
        string weaponLabel = attacker.currentWeaponData != null ? attacker.currentWeaponData.weaponName : "Unarmed";
        Debug.Log($"[CalcDamage] {attacker.name} arma={weaponLabel} weaponBaseDamage={weaponBaseDamage} " +
                  $"str={attacker.str} critMult={critMult:F2} isCrit={isCrit} resultado={result:F1}");

        return result;
    }

    // Resistant: nenhum hit isolado pode ultrapassar 25% do HP MÁXIMO de quem tem a skill — cap
    // aplicado no dano BRUTO (antes de Lead Skeleton/armadura, não depois): essas mitigações do
    // defensor reduzem por cima do valor já capado, então as duas empilham (ex.: 100 HP máximo,
    // Resistant capa em 25, +50% armor reduz esses 25 pra 12.5 — arredondado só no finalDamage,
    // não nos 25 isolados). Usa maxHp (não hp atual) — um personagem já em HP baixo ainda pode
    // morrer de um hit, o cap só limita o quanto UM hit isolado pode arrancar da barra cheia.
    private static float ApplyResistantCap(PlayerState target, float damage)
    {
        if (!target.HasSkill("Resistant")) return damage;
        return Mathf.Min(damage, target.maxHp * 0.25f);
    }

    // Survival: se o dano aplicaria HP <= 0 e a skill ainda não foi usada nesta luta, o
    // personagem sobrevive com 1 HP em vez de morrer (uma vez por luta, consome survivalUsed).
    // Usado nos 3 pontos onde dano reduz hp (SimulateHit, SimulateRetaliation, SimulateThrow).
    private int ApplyDamage(PlayerState target, int rawDamage)
    {
        // Chaining: qualquer dano de qualquer origem (Hit, Counter, Reversal, Throw) zera o
        // streak de quem toma o dano — centralizado aqui porque os 4 pontos de dano do arquivo
        // passam por este método, ver SimulateHit pro lado que incrementa/estuna.
        if (target.HasSkill("Chaining"))
            target.chainHitStreak = 0;

        int newHp = target.hp - rawDamage;
        if (newHp <= 0 && target.HasSkill("Survival") && !target.survivalUsed)
        {
            target.survivalUsed = true;
            newHp = 1;
        }
        return Mathf.Max(0, newHp);
    }

    private int CalcThrowDamage(PlayerState attacker, WeaponData data)
    {
        int weaponDamage = data == null ? 2 : RollWeaponDamage(data);
        int result       = weaponDamage + attacker.str;

        // Diagnóstico temporário: confirma que o throw agora soma STR (era só weaponBaseDamage,
        // bug reportado pelo usuário — esperava o mesmo componente aditivo de STR do dano normal).
        Debug.Log($"[CalcThrowDamage] arma={data?.weaponName ?? "?"} weaponDamage={weaponDamage} str={attacker.str} resultado={result}");
        return result;
    }

    // --- Utilities ---

    private bool Roll(float chance) => (float)_rng.NextDouble() < chance;
    private void Emit(CombatEvent evt) => _events.Add(evt);
}
