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
        // coisa acontecer na luta.
        Player2SabotagedWeapons = ApplySpySabotage(spy: _p1, victim: _p2);
        Player1SabotagedWeapons = ApplySpySabotage(spy: _p2, victim: _p1);

        // Personagens não nascem mais armados (era 40% de chance — EquipStartingWeaponIfNeeded,
        // removido a pedido do usuário) — todo mundo começa a luta desarmado, sempre.

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

    // Saboteur (LaBrute, redefinida pelo usuário — antes destruía 1 arma aleatória do loadout
    // ANTES da luta e dava -100 initiative; agora não destrói nada de antemão): marca
    // `victim.saboteurPending = true` — a 1ª arma que a vítima conseguir empunhar de fato
    // durante a luta (via pickup normal OU roubo via Thief, ver checagem em SimulateTurn logo
    // depois do bloco de Pegar Arma/Thief/Swap) quebra na hora, com 100% de certeza, sem
    // nenhum Roll envolvido. Sem efeito até a vítima de fato puxar uma arma — se ela nunca
    // empunhar nenhuma na luta inteira (raro, mas possível), a skill simplesmente não tem
    // efeito visível algum.
    private void ApplySaboteur(PlayerState saboteur, PlayerState victim)
    {
        if (!saboteur.HasSkill("Saboteur")) return;
        victim.saboteurPending = true;
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

        // Pets (Fase 3) — instâncias independentes a partir de PlayerProfile.pets, sem
        // restrição de duplicatas (3 Ratos geram 3 PetState separados, cada um com seu
        // próprio HP/estado).
        if (profile.pets != null)
            foreach (var petType in profile.pets)
            {
                var pet = PetState.Create(petType);
                if (pet != null)
                {
                    pet.ApplyLevelScaling(profile.level);
                    s.pets.Add(pet);
                }
            }

        return s;
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
        // Monk: NÃO guarda mais (era hitSpeed = 0f, removido — Monk ataca normalmente, igual a
        // qualquer personagem) — só o bônus de counter e o malus de iniciativa permanecem,
        // redefinido pelo usuário.
        if (s.HasSkill("Monk"))                { s.counter += 0.40f; s.initiative -= 200; }
        if (s.HasSkill("Martial Arts"))         { s.martialArts = true; }
        if (s.HasSkill("Shock"))                { s.disarmChanceBonus += 0.50f; }
        if (s.HasSkill("Weapon Master"))        { s.weaponsMaster = true; }
        // Sticky Hands: -50% chance de ser desarmado (DisarmChance) e -50% chance de arremesso,
        // incluindo o próprio (ThrowChance) — campo numérico em vez de bool, lido direto como
        // multiplicador (1 - stickyHands) nas duas fórmulas, ver CLAUDE.md.
        if (s.HasSkill("Sticky Hands"))         { s.stickyHands += 0.50f; }
        // Fierce Brute: 1 uso base + 1 extra pra cada 30 de STR (já com strPct aplicado acima —
        // lido depois do bloco de hpPct/strPct/agiPct/spdPct, então usa o STR final do
        // personagem, não o base do profile).
        if (s.HasSkill("Fierce Brute"))         { s.fierceBruteUsesRemaining = 1 + Mathf.FloorToInt(s.str / 30f); }
        // Fast Metabolism: penalidades fixas do My Brute original ("-50% Hit speed, -5%
        // Critical chance") — a regeneração/pulso em si não depende de nenhum campo aqui, ver
        // SimulateTurn/ApplyDamage. hitSpeed não tem mais nenhum guard (`> 0f`) no simulador
        // desde que Monk parou de zerá-lo — sem efeito nenhum no caminho ativo hoje (o
        // simulador não usa hitSpeed pra escalar nenhuma animação; só o caminho legado,
        // `PlayerCombat.HitRoutine`, multiplica `slashSpeed` por ele).
        if (s.HasSkill("Fast Metabolism"))      { s.hitSpeed -= 0.50f; s.criticalChance -= 0.05f; }

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
            if (i > 0) EmitSpeedBonus(firstAttacker);
            SimulateTurn(firstAttacker, firstDefender);
        }

        // Pets (Fase 3) — ordem simplificada: pets do atacante agem logo depois das ações
        // principais dele, antes do segundo atacante (e dos pets dele) começarem. Não entram
        // no sorteio de iniciativa acima — só seguem o jogador a quem pertencem.
        if (!_p1.isAlive || !_p2.isAlive) return;
        SimulatePetActions(firstAttacker, firstDefender);

        for (int i = 0; i < secondActCount; i++)
        {
            if (!_p1.isAlive || !_p2.isAlive) return;
            if (i > 0) EmitSpeedBonus(secondAttacker);
            SimulateTurn(secondAttacker, secondDefender);
        }

        if (!_p1.isAlive || !_p2.isAlive) return;
        SimulatePetActions(secondAttacker, secondDefender);
    }

    // Speed debt PRÓPRIO de cada pet (não compara contra a speed de um "oponente" 1:1 como os
    // personagens fazem entre si — não existe par equivalente pra pets). Usa uma baseline fixa
    // de 10 (mesmo valor base de personagem comum) como divisor: Rato (10) e Macaco (20) agem
    // pelo menos 1x por round quase sempre (Macaco às vezes 2x); Javali (3) acumula devagar e só
    // libera a 1ª ação por volta do 3º-4º round — sem mínimo forçado de 1 ação por round (ao
    // contrário do personagem) pra essa demora aparecer de verdade, conforme pedido.
    private void SimulatePetActions(PlayerState owner, PlayerState enemyOwner)
    {
        if (owner.pets.Count == 0) return;

        foreach (var pet in owner.pets)
        {
            if (!owner.isAlive || !enemyOwner.isAlive) return;

            if (!pet.isAlive)
            {
                // Pet caído ainda "ocupa" um turno por round (emite só os eventos de skip) —
                // preparação pra Tamer futuramente poder interagir com ele fora de uma janela
                // de ação específica não é necessária aqui, é só pra manter o log consistente.
                SimulatePetTurn(pet, owner, enemyOwner, enemyOwner.pets);
                continue;
            }

            pet.speedDebt += pet.speed;
            int actions = 0;
            while (pet.speedDebt >= 10) { actions++; pet.speedDebt -= 10; }

            for (int i = 0; i < actions; i++)
            {
                if (!owner.isAlive || !enemyOwner.isAlive) return;
                if (!pet.isAlive) break;
                SimulatePetTurn(pet, owner, enemyOwner, enemyOwner.pets);
            }
        }
    }

    private void EmitSpeedBonus(PlayerState player)
    {
        Emit(new CombatEvent { type = CombatEventType.SpeedBonus, playerIndex = player.index, extraActions = 1 });
    }

    private void SimulateTurn(PlayerState attacker, PlayerState defender)
    {
        Emit(new CombatEvent { type = CombatEventType.TurnStart, playerIndex = attacker.index });

        // Net: turno inteiro perdido enquanto enredado — checado antes até do stun do Chaining
        // abaixo ("antes de tudo", pedido pelo usuário). Diferente de stunnedActions (contador
        // que zera por conta própria depois de 1 ação), netEnsnared não tem contador — continua
        // pulando turno após turno até o próprio atacante sofrer um hit (ver NetFreed nos pontos
        // de dano) ou, no caso de pets (netEnsnaredPermanent, ainda não implementado), nunca.
        if (attacker.netEnsnared)
        {
            Emit(new CombatEvent { type = CombatEventType.NetEnsnaredSkip, playerIndex = attacker.index });
            EmitTurnEnd(attacker);
            return;
        }

        // Chaining: turno consumido por estar estunado — pula a ação inteira (sem Thief/
        // pickup/swap/throw/melee), só decrementa o contador e emite StunSkip pra
        // CombatPlayer encerrar o loop de Hurt + remover a label (ver PlayerCombat.HideStunLabel).
        if (attacker.stunnedActions > 0)
        {
            attacker.stunnedActions--;
            Emit(new CombatEvent { type = CombatEventType.StunSkip, playerIndex = attacker.index });
            EmitTurnEnd(attacker);
            return;
        }

        // Pets como alvo válido do adversário (pedido pelo usuário): decidido 1x por turno,
        // antes de qualquer ação real do atacante — cada pet vivo do defensor tem sua própria
        // chance de ser sorteado como alvo no lugar do personagem principal (Boar 75%, Monkey/
        // Mouse 50%; se mais de um passar, sorteia 1 entre eles). Usado por melee (SimulateHit/
        // SimulateComboLoop), SimulateThrow, Haste e Piledriver, e pela Super Vampirism (todas
        // resolvidas mais abaixo) — Flash Flood e Bomb continuam ignorando isso por completo
        // (já atingem todos os alvos via GetEnemyTargets, sem necessidade de redirecionar um
        // único alvo) e Thief/pickup/swap continuam só contra o personagem (não faz sentido
        // roubar/equipar arma de um pet). Net já tem seu próprio roll de 50% pra pegar um pet
        // em vez do personagem, separado e sem relação com este (ver TryActivateNet).
        PetState targetPet = RollPetTarget(defender);

        // 0. Flash Flood (Super, 1x por luta): 17% de chance por ação, exige >= 3 armas no
        // weaponLoadout e o uso ainda disponível. weaponLoadout inclui a arma em mão enquanto
        // equipada (nunca é removida da lista só por estar empunhada — ver BuildState/
        // pickup/swap), então essa mesma contagem >= 3 cobre os dois cenários: armado (mão +
        // 2 outras) ou desarmado (3 quaisquer do loadout) — ver SimulateFlashFlood pra como a
        // arma em mão é sempre incluída quando armado. Checado ANTES de Thief/pickup/swap/
        // throw normal/melee — consome a ação inteira do turno (sem mais nada acontecendo
        // neste mesmo turno).
        if (attacker.HasSkill("Flash Flood") && attacker.flashFloodUsesRemaining > 0
            && attacker.weaponLoadout.Count >= 3 && Roll(0.17f))
        {
            defender.lastSuperUsed = "Flash Flood"; // Mimic tracking
            SimulateFlashFlood(attacker, defender);
            EmitTurnEnd(attacker);
            return;
        }

        // 0b. Haste (Super, 1x por luta): 23% de chance por turno quando disponível. Não exige
        // arma nenhuma (dano vem só de Speed, ver SimulateHaste) — diferente de Flash Flood.
        // Checado ANTES de Thief/pickup/swap/throw normal/melee — consome a ação inteira do turno.
        if (attacker.HasSkill("Haste") && attacker.hasteUsesRemaining > 0 && Roll(0.23f))
        {
            defender.lastSuperUsed = "Haste"; // Mimic tracking
            SimulateHaste(attacker, defender, targetPet);
            EmitTurnEnd(attacker);
            return;
        }

        // 0c. Piledriver (Super, 1x por luta): 17% de chance por turno quando disponível
        // (valor fixo do jogo original). NUNCA pode ser esquivado nem bloqueado — ignora
        // DodgeChance/BlockChance inteiramente (o atacante já agarrou o defensor antes de
        // pular; não há janela pra reagir depois do grab). Dano escala com a STR do
        // DEFENSOR, não do atacante — ver SimulatePiledriver.
        if (attacker.HasSkill("Piledriver") && attacker.piledriverUsesRemaining > 0 && Roll(0.17f))
        {
            defender.lastSuperUsed = "Piledriver"; // Mimic tracking
            SimulatePiledriver(attacker, defender, targetPet);
            EmitTurnEnd(attacker);
            return;
        }

        // 0c-bis. Fast Metabolism: regeneração passiva de 1% do HP máximo TODO turno do
        // personagem (sem rolar chance — não é uma ação de ataque, roda mesmo num round em que
        // ele não chegue a atacar de verdade) + pulso de cura intensa abaixo de 50% HP —
        // redefinido pelo usuário: as 10 curas de 5% não são mais distribuídas uma por turno,
        // acontecem todas DE UMA VEZ (burst) no 1º turno do personagem em que o pulso estiver
        // ativo e ele não tiver sofrido dano (ver ApplyDamage pra como o pulso é ativado/marcado
        // pra interromper). Checado ANTES do loop de Supers embaralhado (0d) — nunca consome o
        // turno (Thief/pickup/throw/melee do mesmo turno continuam normais depois), mesma
        // posição de antes. Nota: como isso fica DEPOIS de Flash Flood/Haste/Piledriver (0/0b/
        // 0c, que retornam antes se ativarem), um personagem com Fast Metabolism + uma dessas
        // Supers não regenera/burst no turno em que a Super consome a ação.
        if (attacker.HasSkill("Fast Metabolism"))
        {
            // Captura ANTES de resetar — representa "sofreu dano desde a última checagem dele",
            // setado por ApplyDamage (mesma regra do My Brute: "if they don't take damage") —
            // exceto no próprio hit que ativa o pulso (ver ApplyDamage), que não conta como
            // interrupção de si mesmo.
            bool tookDamageSinceLastTurn = attacker.fastMetabolismTookDamage;
            attacker.fastMetabolismTookDamage = false;

            if (attacker.fastMetabolismPulseActive)
            {
                if (tookDamageSinceLastTurn)
                {
                    attacker.fastMetabolismPulseActive = false; // interrompido por dano antes do burst acontecer
                }
                else
                {
                    // Burst: as 10 curas de 5% acontecem todas neste turno, em sequência —
                    // CombatPlayer pausa o personagem (parado) durante toda a sequência visual
                    // (ver case FastMetabolismPulse).
                    for (int i = 0; i < 10; i++)
                    {
                        int heal2 = Mathf.Max(1, Mathf.RoundToInt(attacker.maxHp * 0.05f));
                        attacker.hp = Mathf.Min(attacker.maxHp, attacker.hp + heal2);
                        attacker.fastMetabolismPulseCount++;
                        Emit(new CombatEvent { type = CombatEventType.FastMetabolismPulse, playerIndex = attacker.index, healAmount = heal2, newHp = attacker.hp, pulseCount = attacker.fastMetabolismPulseCount });
                    }
                    attacker.fastMetabolismPulseActive = false; // esgotado — já fez as 10 de uma vez
                }
            }

            // Regeneração de 1% — incondicional, e colocada DEPOIS do burst de propósito: quando
            // o burst acontece neste turno, o usuário pediu que a cura de 1% normal aconteça só
            // depois da animação do burst terminar, "em seguida", não antes.
            int heal1 = Mathf.Max(1, Mathf.RoundToInt(attacker.maxHp * 0.01f));
            attacker.hp = Mathf.Min(attacker.maxHp, attacker.hp + heal1);
            Emit(new CombatEvent { type = CombatEventType.FastMetabolismRegen, playerIndex = attacker.index, healAmount = heal1, newHp = attacker.hp });
        }

        // Chef (Passivo de combate, NÃO é Super — não entra no loop embaralhado abaixo): na 1ª
        // ação do dono da skill nesta luta (chefPizzaThrown), lança a pizza envenenada no
        // defensor — SEMPRE acerta (sem Roll de Dodge/Block, mesmo padrão de Net/Piledriver/
        // Bomb) e NÃO consome o turno (Thief/pickup/throw/melee continuam normais depois,
        // mesmo padrão de Fierce Brute/Tragic Potion). poisonDamagePerTurn é calculado aqui, não
        // em ApplySkillStats, porque o defensor só é conhecido em runtime; o veneno não tem
        // duração — dura até o fim da luta ou até curar (Tragic Potion, ver
        // TryActivateTragicPotion) — e tica no fim de TODO turno do defensor a partir de agora
        // (ver EmitTurnEnd).
        if (attacker.HasSkill("Chef") && !attacker.chefPizzaThrown)
        {
            attacker.chefPizzaThrown = true;
            defender.poisoned = true;
            defender.poisonDamagePerTurn = Mathf.Max(1, Mathf.CeilToInt(defender.maxHp * 0.01f));
            Emit(new CombatEvent { type = CombatEventType.ChefPizzaThrow, playerIndex = attacker.index, targetIndex = defender.index });
        }

        // 0d. Supers checados em ORDEM ALEATÓRIA (Net, Fierce Brute, Bomb, Tragic Potion, Vampirism, futuros): a lista de
        // Supers disponíveis do atacante é embaralhada a cada turno (mesmo _rng do simulador,
        // determinístico por seed) e cada um é checado em sequência — todos que passarem no
        // próprio roll de chance ativam no MESMO turno (ex: Net e Bomb podem ambos ativar — Net
        // imobiliza o oponente e Bomb explode em seguida, causando dano e quebrando a rede).
        // Era uma checagem fixa Net → Fierce Brute (nessa ordem); generalizado pra um loop sobre
        // delegates pra acomodar Bomb (e futuras Supers) sem duplicar a lógica de embaralhamento
        // a cada uma nova. Net e Bomb consomem o turno inteiro (sinalizado pelo retorno `true`
        // do delegate, ver TryActivateBomb) — Fierce Brute e Tragic Potion sempre caem direto pro
        // fluxo normal (Thief/pickup/throw/melee) do mesmo turno, então a checagem de "consumiu
        // o turno" só acontece DEPOIS do loop inteiro rodar, nunca interrompendo Supers ainda não
        // checados na mesma lista (ex: Net imobiliza e Bomb ainda explode no mesmo turno, em
        // qualquer ordem sorteada, antes do `if (turnConsumed)` abaixo encerrar o turno).
        var supers = new List<System.Func<bool>>();
        if (attacker.HasSkill("Net") && attacker.netUsesRemaining > 0)
            supers.Add(() => TryActivateNet(attacker, defender));
        if (attacker.HasSkill("Fierce Brute") && attacker.fierceBruteUsesRemaining > 0)
            supers.Add(() => {
                int br = attacker.fierceBruteUsesRemaining;
                TryActivateFierceBrute(attacker);
                if (attacker.fierceBruteUsesRemaining < br) defender.lastSuperUsed = "Fierce Brute";
                return false;
            });
        if (attacker.HasSkill("Bomb") && attacker.bombUsesRemaining > 0)
            supers.Add(() => TryActivateBomb(attacker, defender));
        if (attacker.HasSkill("Tragic Potion") && attacker.tragicPotionUsesRemaining > 0)
            supers.Add(() => {
                int tp = attacker.tragicPotionUsesRemaining;
                TryActivateTragicPotion(attacker);
                if (attacker.tragicPotionUsesRemaining < tp) defender.lastSuperUsed = "Tragic Potion";
                return false;
            });
        if (attacker.HasSkill("Vampirism") && attacker.vampirismUsesRemaining > 0)
            supers.Add(() => TryActivateVampirism(attacker, defender, targetPet));
        if (attacker.HasSkill("Cry of the Damned") && attacker.cryOfTheDamnedUsesRemaining > 0)
            supers.Add(() => { TryActivateCryOfTheDamned(attacker, defender); return false; });
        if (attacker.HasSkill("Hypnosis") && attacker.hypnosisUsesRemaining > 0)
            supers.Add(() => { TryActivateHypnosis(attacker, defender); return false; });
        if (attacker.HasSkill("Tamer") && attacker.tamerUsesRemaining > 0)
            supers.Add(() => { TryActivateTamer(attacker, defender); return false; });
        if (attacker.HasSkill("Treat") && attacker.treatUsesRemaining > 0)
            supers.Add(() => { TryActivateTreat(attacker, defender); return false; });
        if (attacker.HasSkill("Mimic") && attacker.mimicUsesRemaining > 0
            && !string.IsNullOrEmpty(defender.lastSuperUsed))
            supers.Add(() => TryActivateMimic(attacker, defender, targetPet));
        // futuros Supers entram aqui

        ShuffleList(supers);
        bool turnConsumed = false;
        foreach (var trySuper in supers)
            if (trySuper()) turnConsumed = true;

        // Re-valida: CryOfTheDamned pode ter fugado o pet que foi sorteado ANTES dos supers.
        // Sem isso o simulador emite Run/Hit targeteando um pet que já fugiu.
        if (targetPet != null && !targetPet.isAlive)
            targetPet = null;

        if (turnConsumed)
        {
            EmitTurnEnd(attacker);
            return;
        }

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
        // houver outra opção). Como ninguém mais nasce armado (EquipStartingWeaponIfNeeded
        // removido), este `else if` só pode mesmo disparar a partir do 2º turno em diante de
        // quem já pegou arma antes (o 1º turno de todo mundo cai sempre no `if` acima, unarmed).
        else if (!stoleWeapon && attacker.currentWeaponData != null && attacker.weaponLoadout.Count > 1 && Roll(0.40f))
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

        // Saboteur (do oponente): a 1ª arma que esta vítima conseguir empunhar de verdade nesta
        // luta — via roubo (item 1/Thief) OU pickup normal (item 2) — quebra na hora, com 100%
        // de certeza, sem nenhum Roll envolvido (diferente de Sabotage, que rola 50% por hit
        // acertado). Uma única checagem aqui cobre os dois jeitos de ficar armado de fato: item
        // 2b/swap nunca dispara enquanto `saboteurPending` ainda for true (exige já estar
        // armado, e a 1ª arma sempre quebra antes disso virar possível). A arma quebrada sai do
        // loadout pra sempre, igual a um Disarm garantido.
        if (attacker.saboteurPending && attacker.currentWeaponData != null)
        {
            var broken = attacker.currentWeaponData;
            attacker.weaponLoadout.Remove(broken);
            attacker.currentWeaponData = null;
            attacker.saboteurPending = false;
            Emit(new CombatEvent { type = CombatEventType.SaboteurBreak, playerIndex = defender.index, targetIndex = attacker.index, weaponName = broken.weaponName });
        }

        // 3. Check throw before melee — Hideaway dá 50% fixo (ver ThrowChance), Sticky Hands
        // multiplica essa chance (qualquer origem) por (1 - stickyHands).
        if (attacker.currentWeaponData != null && Roll(ThrowChance(attacker)))
        {
            // Fierce Brute escopado só a CalcDamage/melee (ver SimulateHit) — se o turno virou
            // arremesso em vez de melee, o buff se perde aqui (sem dobro, sem crítico bônus),
            // já que o arremesso É a ação de ataque deste turno (consome o buff sem aplicá-lo,
            // mesma lógica de "uso gasto, sem reembolso" do hit melee normal). Diferente de Net/
            // Bomb consumindo o turno ANTES desse ponto (item 0d acima) — aí o personagem nunca
            // chega a agir de verdade, então o buff persiste pro turno seguinte (ver
            // TryActivateFierceBrute) — aqui ele chega a agir (arremessar), só não com o bônus.
            attacker.fierceBruteActive = false;
            SimulateThrow(attacker, defender, targetPet);
            EmitTurnEnd(attacker);
            return;
        }

        // 4. Melee
        {
            int runTargetPetIdx = targetPet != null ? defender.pets.IndexOf(targetPet) : -1;
            Emit(new CombatEvent { type = CombatEventType.RunToDefender, playerIndex = attacker.index, targetIndex = defender.index, targetIsPet = targetPet != null, targetPetIndex = runTargetPetIdx });
        }
        bool interrupted = SimulateHitWithDetermination(attacker, defender, isCombo: false, out bool _, targetPet);
        SimulateComboLoop(attacker, defender, interrupted, targetPet);

        EmitTurnEnd(attacker);
    }

    // Funil único pra todo TurnEnd de SimulateTurn (skip por Net/Stun, qualquer Super, throw,
    // melee) — skill Chef: o veneno tica no fim de TODO turno de quem estiver `poisoned`,
    // independente do que aconteceu nesse turno (até de um turno inteiro pulado), até curar
    // (Tragic Potion) ou a luta acabar. Emitido DEPOIS do próprio TurnEnd (pedido do usuário) —
    // a explosão do veneno só toca depois do personagem já ter pulado de volta pro ponto de
    // partida (spawn), nunca enquanto ele ainda está na posição em que agiu.
    private void EmitTurnEnd(PlayerState attacker)
    {
        Emit(new CombatEvent { type = CombatEventType.TurnEnd, playerIndex = attacker.index });
        if (attacker.poisoned)
        {
            int poisonDamage = attacker.poisonDamagePerTurn;
            attacker.hp = ApplyDamage(attacker, poisonDamage);
            Emit(new CombatEvent { type = CombatEventType.PoisonDamage, playerIndex = attacker.index, damage = poisonDamage, newHp = attacker.hp });
        }
    }

    // --- Pets (Fase 3) ---

    // Turno de um pet. Alvo é decidido uma única vez (não re-sorteado a cada hit extra de
    // combo) — usa o mesmo RollPetTarget do turno de personagem (Javali=75%, Macaco/Rato=50%
    // de chance individual de interceptar em vez do personagem principal). Combo do pet usa o
    // mesmo decaimento ×0.5 do personagem (ComboChance), mas sem clamp/bônus de tag de arma
    // — é só pet.comboRate puro — e com um teto fixo de 3 hits extras.
    private void SimulatePetTurn(PetState pet, PlayerState petOwner, PlayerState enemy, List<PetState> enemyPets)
    {
        int petIndex = petOwner.pets.IndexOf(pet);
        Emit(new CombatEvent { type = CombatEventType.PetTurnStart, playerIndex = petOwner.index, petIndex = petIndex });

        if (!pet.isAlive || pet.netEnsnared)
        {
            Emit(new CombatEvent { type = CombatEventType.PetNetSkip, playerIndex = petOwner.index, petIndex = petIndex });
            Emit(new CombatEvent { type = CombatEventType.PetTurnEnd, playerIndex = petOwner.index, petIndex = petIndex });
            return;
        }

        PetState targetPetState = RollPetTarget(enemy);
        bool targetIsPet  = targetPetState != null;
        int  targetPetIdx = targetIsPet ? enemy.pets.IndexOf(targetPetState) : -1;

        bool interrupted = SimulatePetHit(petOwner, petIndex, enemy, enemyPets, targetIsPet, targetPetIdx, comboCount: 0);

        int comboCount = 0;
        while (!interrupted && pet.isAlive && comboCount < 3)
        {
            float chance = pet.comboRate * Mathf.Pow(0.5f, comboCount);
            if (!Roll(chance)) break;
            comboCount++;
            interrupted = SimulatePetHit(petOwner, petIndex, enemy, enemyPets, targetIsPet, targetPetIdx, comboCount);
        }

        Emit(new CombatEvent { type = CombatEventType.PetTurnEnd, playerIndex = petOwner.index, petIndex = petIndex });
    }

    // Uma tentativa de ataque do pet (1ª do turno, ou hit extra de combo). Retorna `interrupted`
    // = true só quando o Macaco alvo conta com sucesso (mesmo padrão do Counter de personagem:
    // cancela o resto do combo deste pet no turno). comboCount == 0 identifica o 1º hit do
    // turno — só nele o Javali pode desarmar (mesma regra "first hit only" do desarme normal).
    private bool SimulatePetHit(PlayerState petOwner, int petIndex, PlayerState enemyOwner, List<PetState> enemyPets, bool targetIsPet, int targetPetIdx, int comboCount)
    {
        var pet = petOwner.pets[petIndex];
        if (!pet.isAlive) return true;

        PetState targetPet = targetIsPet ? enemyPets[targetPetIdx] : null;
        if (targetIsPet && (targetPet == null || !targetPet.isAlive)) return true;
        if (!targetIsPet && !enemyOwner.isAlive) return true;

        var (minDmg, maxDmg) = PetState.DamageRange(pet.type);
        int damage = _rng.Next(minDmg, maxDmg + 1);

        // Esquiva: alvo personagem usa a fórmula normal de DodgeChance, mas sem o termo de
        // accuracy do atacante (pet não tem esse stat) — ver PetDodgeChanceOnCharacter. Alvo
        // pet usa só o próprio evasionBase, sem nenhum outro termo.
        bool isDodged = targetIsPet ? Roll(targetPet.evasionBase) : Roll(PetDodgeChanceOnCharacter(enemyOwner));

        // Macaco sendo atacado: Counter cancela o hit antes de conectar (interrompe o resto do
        // combo deste pet, mesmo padrão do Counter de personagem).
        if (targetIsPet && targetPet.type == PetType.Monkey && Roll(targetPet.counter))
        {
            Emit(new CombatEvent { type = CombatEventType.PetAttack, playerIndex = petOwner.index, petIndex = petIndex, targetIsPet = true, targetIndex = enemyOwner.index, targetPetIndex = targetPetIdx, isDodged = true });
            SimulatePetRetaliation(enemyOwner, targetPetIdx, petOwner, petIndex);
            return true;
        }

        if (isDodged)
        {
            Emit(new CombatEvent { type = CombatEventType.PetAttack, playerIndex = petOwner.index, petIndex = petIndex, targetIsPet = targetIsPet, targetIndex = enemyOwner.index, targetPetIndex = targetIsPet ? targetPetIdx : -1, isDodged = true });
            return false;
        }

        if (targetIsPet)
        {
            // Escudo do Treat: absorve o ataque do pet sem dano.
            if (targetPet.shielded)
            {
                targetPet.shielded = false;
                Emit(new CombatEvent { type = CombatEventType.PetAttack, playerIndex = petOwner.index, petIndex = petIndex, targetIsPet = true, targetIndex = enemyOwner.index, targetPetIndex = targetPetIdx, damage = 0, newTargetHp = targetPet.hp, newTargetMaxHp = targetPet.maxHp, petShieldAbsorb = true });
                return false;
            }
            ApplyDamageToPet(targetPet, damage);
            Emit(new CombatEvent { type = CombatEventType.PetAttack, playerIndex = petOwner.index, petIndex = petIndex, targetIsPet = true, targetIndex = enemyOwner.index, targetPetIndex = targetPetIdx, damage = damage, newTargetHp = targetPet.hp, newTargetMaxHp = targetPet.maxHp });

            if (!targetPet.isAlive)
                Emit(new CombatEvent { type = CombatEventType.PetDeath, playerIndex = enemyOwner.index, petIndex = targetPetIdx });
            else if (targetPet.type == PetType.Monkey && Roll(targetPet.reversal))
                SimulatePetRetaliation(enemyOwner, targetPetIdx, petOwner, petIndex); // não interrompe o combo, mesmo padrão do Reversal de personagem
        }
        else
        {
            enemyOwner.hp = ApplyDamage(enemyOwner, damage);
            Emit(new CombatEvent { type = CombatEventType.PetAttack, playerIndex = petOwner.index, petIndex = petIndex, targetIsPet = false, targetIndex = enemyOwner.index, damage = damage, newHp = enemyOwner.hp, maxHp = enemyOwner.maxHp });
            Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = enemyOwner.index, newHp = enemyOwner.hp, maxHp = enemyOwner.maxHp });
            CheckNetFreed(enemyOwner);

            // Javali: 15% de desarme, só no 1º hit do turno (comboCount == 0), só contra personagem.
            if (comboCount == 0 && pet.type == PetType.Boar && enemyOwner.currentWeaponData != null && Roll(0.15f))
            {
                string wn = enemyOwner.currentWeaponData.weaponName;
                enemyOwner.weaponLoadout.Remove(enemyOwner.currentWeaponData);
                enemyOwner.currentWeaponData = null;
                Emit(new CombatEvent { type = CombatEventType.PetDisarm, playerIndex = petOwner.index, petIndex = petIndex, targetIndex = enemyOwner.index, weaponName = wn });
            }
        }

        return false;
    }

    // Macaco contra-atacando (Counter, antes do hit conectar, ou Reversal, depois de já ter
    // tomado dano) — sempre o pet retaliador batendo no pet atacante original. "Simples" por
    // pedido: sem checar esquiva do atacante além do evasionBase dele, sem recursão de
    // Counter/Reversal (uma retaliação nunca é retaliada de novo).
    private void SimulatePetRetaliation(PlayerState retaliatorOwner, int retaliatorPetIndex, PlayerState targetOwner, int targetPetIndex)
    {
        var retaliator = retaliatorOwner.pets[retaliatorPetIndex];
        var targetPet  = targetOwner.pets[targetPetIndex];
        if (!retaliator.isAlive || !targetPet.isAlive) return;

        var (minDmg, maxDmg) = PetState.DamageRange(retaliator.type);
        int dmg    = _rng.Next(minDmg, maxDmg + 1);
        bool dodged = Roll(targetPet.evasionBase);

        if (!dodged) ApplyDamageToPet(targetPet, dmg);

        Emit(new CombatEvent
        {
            type = CombatEventType.PetAttack,
            playerIndex = retaliatorOwner.index, petIndex = retaliatorPetIndex,
            targetIsPet = true, targetIndex = targetOwner.index, targetPetIndex = targetPetIndex,
            damage = dodged ? 0 : dmg, isDodged = dodged,
            newTargetHp = targetPet.hp, newTargetMaxHp = targetPet.maxHp,
        });

        if (!targetPet.isAlive)
            Emit(new CombatEvent { type = CombatEventType.PetDeath, playerIndex = targetOwner.index, petIndex = targetPetIndex });
    }

    // Mirrors DodgeChance(attacker, defender), mas sem o termo `- attacker.accuracy` (pets não
    // têm esse stat) — usado quando o atacante é um pet e o alvo é o personagem principal.
    private float PetDodgeChanceOnCharacter(PlayerState defender)
    {
        if (defender.noEvasion) return 0f;

        float baseChance = defender.currentWeaponData == null
            ? 0.10f
            : TagSum(defender.currentWeaponData, sharp: 0.10f, fast: 0.05f, heavy: 0.05f);
        float agiBonus      = Mathf.Max(0, defender.agility - 3) * 0.02f;
        float weaponEvasion = defender.currentWeaponData != null
            ? defender.currentWeaponData.evasionBonus : UnarmedStats.EvasionBonus;
        float survivalBonus = (defender.hp == 1 && defender.HasSkill("Survival")) ? 0.20f : 0f;
        float bodybuilderBonus = (WeaponData.HasType(defender.currentWeaponData, WeaponType.Heavy) && defender.HasSkill("Bodybuilder")) ? 0.10f : 0f;
        float total = baseChance + agiBonus + defender.evasion + weaponEvasion + survivalBonus + bodybuilderBonus;
        return Mathf.Clamp(total, 0f, 0.60f);
    }

    private void ApplyDamageToPet(PetState pet, int rawDamage)
    {
        pet.hp = Mathf.Max(0, pet.hp - rawDamage);
        if (pet.hp <= 0) pet.isAlive = false;
    }

    // Combo loop (mirrors AttackRoutine's while loop). Each consecutive extra hit decays the
    // chance by ×0.5 (1st extra hit normal, 2nd ×0.5, 3rd ×0.25, ...) — mirrors My Brute, where
    // combo probability drops sharply after the first follow-up. Counter zera o resto do combo
    // (o hit nunca aconteceu de fato), assim como Iron Head (atacante perde a arma, sem
    // condições de continuar a sequência) — Dodge, Block e Reversal não interrompem nada, o
    // combo continua normal depois deles. Reversal pode disparar de novo em cada hit extra do
    // combo, independente do(s) anterior(es).
    private void SimulateComboLoop(PlayerState attacker, PlayerState defender, bool interrupted, PetState targetPet = null)
    {
        int comboCount = 0;
        // Alvo decidido 1x no início do turno (targetPet) — se for um pet, o combo inteiro
        // continua nele; a condição de continuação usa a vida do pet em vez da do personagem.
        while (!interrupted && attacker.isAlive && (targetPet != null ? targetPet.isAlive : defender.isAlive))
        {
            float comboChance = ComboChance(attacker, comboCount);
            if (!Roll(comboChance)) break;
            interrupted = SimulateHitWithDetermination(attacker, defender, isCombo: true, out bool _, targetPet);
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
    private bool SimulateHitWithDetermination(PlayerState attacker, PlayerState defender, bool isCombo, out bool damageDealt, PetState targetPet = null)
    {
        bool interrupted = SimulateHit(attacker, defender, isCombo, out damageDealt, targetPet);

        bool targetAlive = targetPet != null ? targetPet.isAlive : defender.isAlive;
        while (!damageDealt && attacker.HasSkill("Determination") && attacker.isAlive && targetAlive && Roll(0.60f))
        {
            interrupted = SimulateHit(attacker, defender, isCombo: false, out damageDealt, targetPet);
            targetAlive = targetPet != null ? targetPet.isAlive : defender.isAlive;
        }

        return interrupted;
    }

    // Retorna true se o combo do atacante deve ser interrompido (Counter do defensor, ou
    // Iron Head derrubando a arma do atacante) — false em qualquer outro desfecho (incluindo
    // Dodge/Block/Reversal, que não interrompem). damageDealt (out): true só quando o dano
    // normal de fato foi aplicado ao defensor — usado por Determination (ver
    // SimulateHitWithDetermination acima) pra saber quando NÃO tentar de novo.
    private bool SimulateHit(PlayerState attacker, PlayerState defender, bool isCombo, out bool damageDealt, PetState targetPet = null)
    {
        damageDealt = false;

        // Pets como alvo válido (ver RollPetTarget/SimulateTurn): quando o alvo deste turno é
        // um pet em vez do personagem, o golpe é resolvido de forma simplificada — só esquiva
        // (pet.evasionBase, mesmo termo já usado quando um pet ataca um personagem em
        // SimulatePetHit/PetDodgeChanceOnCharacter), sem Counter/Block/Reversal/Disarm/Chaining/
        // Sabotage/Iron Head/Resistant/armadura (nenhum desses existe pra um pet). Fierce Brute
        // ainda dobra o dano normalmente (é um buff do atacante, não depende do tipo de alvo) —
        // mas a interceptação do Rato (ver branch de personagem abaixo) não se aplica aqui, já
        // que o alvo já É um pet.
        if (targetPet != null)
        {
            bool fierceBruteThisPetHit = !isCombo && attacker.fierceBruteActive;
            int petIdx = defender.pets.IndexOf(targetPet);

            if (Roll(targetPet.evasionBase))
            {
                if (fierceBruteThisPetHit) attacker.fierceBruteActive = false;
                Emit(new CombatEvent { type = CombatEventType.Dodge, playerIndex = attacker.index, targetIndex = defender.index, targetIsPet = true, targetPetIndex = petIdx });
                return false;
            }

            damageDealt = true;
            bool  isCritPet = Roll(CritChance(attacker));
            float dmgPet    = CalcDamage(attacker, isCritPet);
            if (fierceBruteThisPetHit)
            {
                dmgPet *= 2f;
                attacker.fierceBruteActive = false;
            }
            int finalDamagePet = Mathf.Max(1, Mathf.RoundToInt(dmgPet));
            // Escudo do Treat: absorve o golpe sem causar dano ao pet.
            if (targetPet.shielded)
            {
                targetPet.shielded = false;
                damageDealt = false;
                Emit(new CombatEvent { type = CombatEventType.Hit, playerIndex = attacker.index, targetIndex = defender.index, damage = 0, isCombo = isCombo, isFierceBrute = fierceBruteThisPetHit, targetIsPet = true, targetPetIndex = petIdx, newTargetHp = targetPet.hp, newTargetMaxHp = targetPet.maxHp, petShieldAbsorb = true });
                return false;
            }
            ApplyDamageToPet(targetPet, finalDamagePet);

            Emit(new CombatEvent { type = CombatEventType.Hit, playerIndex = attacker.index, targetIndex = defender.index, damage = finalDamagePet, isCrit = isCritPet, isCombo = isCombo, isFierceBrute = fierceBruteThisPetHit, targetIsPet = true, targetPetIndex = petIdx, newTargetHp = targetPet.hp, newTargetMaxHp = targetPet.maxHp });

            if (!targetPet.isAlive)
                Emit(new CombatEvent { type = CombatEventType.PetDeath, playerIndex = defender.index, petIndex = petIdx });

            return false; // pets não têm Counter — nunca interrompe o combo do atacante
        }

        // Fierce Brute: só a 1ª tentativa de hit do turno (isCombo == false) é elegível pro
        // dobro de dano — combo extra nunca dobra, e cada retry de Determination (também
        // isCombo == false) só vê o buff true na 1ª chamada de fato, já que é consumido (true
        // ou false) antes do retorno desta função, qualquer que seja o desfecho (ver branches
        // abaixo e a doc original: "se o hit falhar por qualquer motivo, o buff é consumido
        // mesmo assim").
        bool fierceBruteThisHit = !isCombo && attacker.fierceBruteActive;

        // Ballet Shoes: first hit of the fight auto-dodged
        if (!isCombo && defender.firstHitAvoided)
        {
            defender.firstHitAvoided = false;
            if (fierceBruteThisHit) attacker.fierceBruteActive = false;
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
        // Net: defensor enredado não pode counterar — "não pode usar counter-attack" — cai
        // direto pro Block (também gated abaixo).
        if (!defender.netEnsnared && Roll(CounterChance(defender)))
        {
            if (fierceBruteThisHit) attacker.fierceBruteActive = false;
            SimulateRetaliation(defender, attacker, CombatEventType.Counter);
            return true;
        }

        // Block check — gated por !defender.netEnsnared ("não pode usar block/parry" enquanto
        // enredado; a rede impede evadir o próximo ataque, ver descrição original da skill Net).
        if (!defender.netEnsnared && Roll(BlockChance(attacker, defender)))
        {
            if (fierceBruteThisHit) attacker.fierceBruteActive = false;
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

        // Dodge check — gated por !defender.netEnsnared ("não pode usar dodge" enquanto enredado).
        if (!defender.netEnsnared && Roll(DodgeChance(attacker, defender)))
        {
            if (fierceBruteThisHit) attacker.fierceBruteActive = false;
            Emit(new CombatEvent { type = CombatEventType.Dodge, playerIndex = attacker.index, targetIndex = defender.index });
            return false;
        }

        // Normal hit — fórmula multiplicativa do My Brute
        damageDealt = true;
        bool  isCrit = Roll(CritChance(attacker)); // já soma +10% de Fierce Brute se ativo — lido ANTES de zerar o flag abaixo.
        float dmg    = CalcDamage(attacker, isCrit);

        // Fierce Brute: dobra o dano BRUTO deste hit, antes do Resistant cap (pra não furar o
        // teto de 25% do HP máximo que essa skill garante) — e consome o buff agora que o hit
        // de fato conectou.
        if (fierceBruteThisHit)
        {
            dmg *= 2f;
            attacker.fierceBruteActive = false;

            // Rato (Mouse) como escudo vivo: 50% de chance dele interceptar o hit já dobrado no
            // lugar do personagem, se o defensor tiver um Rato vivo. Dano vai direto pro Rato
            // (sem Resistant/Lead Skeleton/armadura do personagem — esses nunca chegam a entrar
            // em jogo, já que o hit nem alcança o defensor de verdade).
            var mouse = defender.pets.Find(p => p.isAlive && p.type == PetType.Mouse);
            if (mouse != null && Roll(0.50f))
            {
                int mouseIdx    = defender.pets.IndexOf(mouse);
                int petDamage   = Mathf.Max(1, Mathf.RoundToInt(dmg));
                ApplyDamageToPet(mouse, petDamage);
                Emit(new CombatEvent
                {
                    type = CombatEventType.PetAttack, playerIndex = defender.index, petIndex = mouseIdx,
                    targetIsPet = true, targetIndex = defender.index, targetPetIndex = mouseIdx,
                    damage = petDamage, newTargetHp = mouse.hp, newTargetMaxHp = mouse.maxHp,
                    shieldIntercept = true,
                });
                if (!mouse.isAlive)
                    Emit(new CombatEvent { type = CombatEventType.PetDeath, playerIndex = defender.index, petIndex = mouseIdx });
                return false;
            }
        }

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
        Emit(new CombatEvent { type = CombatEventType.Hit, playerIndex = attacker.index, targetIndex = defender.index, damage = finalDamage, isCrit = isCrit, isCombo = isCombo, isFierceBrute = fierceBruteThisHit, newHp = defender.hp, maxHp = defender.maxHp });
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

        // Sabotage: 50% de chance, a cada golpe que acerta (qualquer hit, incluindo combo —
        // não só o primeiro), de destruir permanentemente uma arma aleatória do HUD do
        // defensor — só as com ícone CINZA (na "bolsa", não equipadas). A arma com ícone
        // DOURADO (a que está na mão dele agora) nunca é elegível — pedido pelo usuário, a
        // skill não deve influenciar o que o oponente já está empunhando, só o resto do HUD.
        // Reusa o mesmo CombatEventType.Saboteur (e a mesma queda visual em pêndulo do ícone
        // até o chão) — único emissor restante desse tipo de evento, já que a skill Saboteur
        // (mecânica distinta, ver ApplySaboteur/SaboteurBreak acima) passou a quebrar a 1ª arma
        // puxada em vez de destruir uma do HUD antes da luta.
        if (attacker.HasSkill("Sabotage"))
        {
            var sabotagePool = new List<WeaponData>();
            foreach (var w in defender.weaponLoadout)
                if (w != defender.currentWeaponData) sabotagePool.Add(w);

            if (sabotagePool.Count > 0 && Roll(0.50f))
            {
                var destroyed = sabotagePool[_rng.Next(sabotagePool.Count)];
                defender.weaponLoadout.Remove(destroyed);
                Emit(new CombatEvent { type = CombatEventType.Saboteur, playerIndex = attacker.index, targetIndex = defender.index, weaponName = destroyed.weaponName });
            }
        }

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
        // Reversal de novo, igual a este). Gated por !defender.netEnsnared — "não pode usar
        // counter-attack" enquanto enredado (Reversal é a 2ª metade dessa mecânica, ver Counter
        // já gated acima). CheckNetFreed (no fim do método) só solta o defensor DEPOIS deste
        // check — ele ainda está enredado no exato momento do próprio hit que vai libertá-lo,
        // então não pode usar Reversal neste mesmo golpe.
        if (!defender.netEnsnared && Roll(ReversalChance(defender)))
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

        // Net: solta o defensor DEPOIS de toda a resolução deste hit (Reversal/Desarme acima já
        // rodaram com netEnsnared ainda true, corretamente bloqueados) — qualquer hit de
        // verdade que o defensor sofra encerra o status.
        CheckNetFreed(defender);

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

    private void SimulateThrow(PlayerState attacker, PlayerState defender, PetState targetPet = null)
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

        // Pets como alvo válido: arremesso contra um pet usa só o evasionBase dele (mesmo termo
        // de SimulateHit acima) em vez do hitChance/Hideaway do personagem — Hideaway é skill do
        // personagem, pets não a herdam.
        if (targetPet != null)
        {
            int petIdx = defender.pets.IndexOf(targetPet);
            if (Roll(targetPet.evasionBase))
            {
                Emit(new CombatEvent { type = CombatEventType.Miss, playerIndex = attacker.index, targetIndex = defender.index, targetIsPet = true, targetPetIndex = petIdx });
                return;
            }

            int dmg = CalcThrowDamage(attacker, weaponData);
            ApplyDamageToPet(targetPet, dmg);
            Emit(new CombatEvent { type = CombatEventType.Hit, playerIndex = attacker.index, targetIndex = defender.index, damage = dmg, isThrow = true, targetIsPet = true, targetPetIndex = petIdx, newTargetHp = targetPet.hp, newTargetMaxHp = targetPet.maxHp });
            if (!targetPet.isAlive)
                Emit(new CombatEvent { type = CombatEventType.PetDeath, playerIndex = defender.index, petIndex = petIdx });
            return;
        }

        // Repulse: 30% de chance de deflectir o throw de volta para o lançador original.
        // Não ativa se o defensor estiver enredado (Net — sem mobilidade pra rebater).
        if (!defender.netEnsnared && defender.HasSkill("Repulse") && Roll(0.30f))
        {
            SimulateRepulse(defender, attacker, weaponData);
            return;
        }

        // Hideaway: +25% block contra arremessos recebidos — reduz direto a chance de acerto do
        // throw (80% → 55%, miss sobe de 20% pra 45%), em vez de um 3º resultado separado de
        // Block. Mais simples que a versão anterior (Roll(ThrowBlockChance) + evento Block
        // próprio) e bate com os valores oficiais do LaBrute.
        // Net: defensor enredado não pode evadir o arremesso também — força acerto, ignorando
        // hitChance/Hideaway por completo (mesmo "sempre acerta" das outras checagens, ver
        // Counter/Block/Dodge gated em SimulateHit/SimulateHaste).
        float hitChance = 0.80f - (defender.HasSkill("Hideaway") ? 0.25f : 0f);
        if (defender.netEnsnared || Roll(hitChance))
        {
            int dmg = CalcThrowDamage(attacker, weaponData);
            // Resistant: cap no dano bruto, antes da armadura (ver ApplyResistantCap).
            dmg = Mathf.RoundToInt(ApplyResistantCap(defender, dmg));
            if (defender.armor > 0f)
                dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - defender.armor)));

            defender.hp = ApplyDamage(defender, dmg);
            Emit(new CombatEvent { type = CombatEventType.Hit, playerIndex = attacker.index, targetIndex = defender.index, damage = dmg, isThrow = true, newHp = defender.hp, maxHp = defender.maxHp });
            Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = defender.index, newHp = defender.hp, maxHp = defender.maxHp });
            CheckNetFreed(defender);
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

    // --- Repulse (Passiva de Defesa) ---

    private void SimulateRepulse(PlayerState deflector, PlayerState originalThrower, WeaponData weaponData)
    {
        // +5% de crítico adicional do Repulse; CritChance base usa o weapon atual do deflector
        bool  isCrit   = Roll(CritChance(deflector) + 0.05f);
        // Dano: weaponDamage + STR do lançador original (força do arremesso original)
        int   dmg      = CalcThrowDamage(originalThrower, weaponData);
        float critMult = (weaponData != null ? weaponData.critDamageMultiplier : UnarmedStats.CritDamageMultiplier)
                       + deflector.critDamageBonus;
        if (isCrit) dmg = Mathf.RoundToInt(dmg * critMult);
        // Mitigação do lançador original (é ele quem recebe o impacto de volta)
        dmg = Mathf.RoundToInt(ApplyResistantCap(originalThrower, dmg));
        if (originalThrower.armor > 0f)
            dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - originalThrower.armor)));

        originalThrower.hp = ApplyDamage(originalThrower, dmg);
        Emit(new CombatEvent
        {
            type        = CombatEventType.Repulse,
            playerIndex = deflector.index,
            targetIndex = originalThrower.index,
            weaponName  = weaponData?.weaponName ?? "",
            damage      = dmg,
            isCrit      = isCrit,
            newHp       = originalThrower.hp,
            maxHp       = originalThrower.maxHp
        });
        Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = originalThrower.index, newHp = originalThrower.hp, maxHp = originalThrower.maxHp });
        CheckNetFreed(originalThrower);
    }

    // --- Flash Flood (Super) ---

    // Se o atacante estiver armado, a arma em mão é SEMPRE uma das 3 arremessadas (ordem
    // garantida: sempre a 1ª de ffWeapons) — as outras 2 são sorteadas aleatoriamente do
    // restante do weaponLoadout (que inclui a própria arma em mão enquanto equipada, ver
    // BuildState/pickup/swap acima — nunca é removida da lista só por estar empunhada).
    // Se estiver desarmado, as 3 são sorteadas aleatoriamente do weaponLoadout inteiro, como
    // antes. Em ambos os casos arremessa cada uma em sequência — todas SEMPRE ACERTAM: nunca
    // passam por Roll(BlockChance)/Roll(DodgeChance) (e, quando pets existirem, também não
    // devem poder interceptar este ataque). Armadura e Resistant do defensor continuam
    // valendo normalmente — só esquiva/bloqueio/pet são ignorados, não mitigação de dano. As
    // 3 armas saem da mão e do loadout pra sempre (consumidas no arremesso, mesmo as com a
    // tag Thrown) — diferente de SimulateThrow, que devolve armas Thrown ao loadout.
    private void SimulateFlashFlood(PlayerState attacker, PlayerState defender)
    {
        attacker.flashFloodUsesRemaining--;

        var pool = new List<int>();
        for (int i = 0; i < attacker.weaponLoadout.Count; i++) pool.Add(i);

        var chosen = new List<WeaponData>();
        if (attacker.currentWeaponData != null)
        {
            chosen.Add(attacker.currentWeaponData);
            pool.Remove(attacker.weaponLoadout.IndexOf(attacker.currentWeaponData));
        }
        while (chosen.Count < 3 && pool.Count > 0)
        {
            int pick = _rng.Next(pool.Count);
            int idx  = pool[pick];
            pool.RemoveAt(pick);
            chosen.Add(attacker.weaponLoadout[idx]);
        }
        foreach (var w in chosen)
            attacker.weaponLoadout.Remove(w);
        attacker.currentWeaponData = null; // arma em mão (se havia) também foi arremessada

        var weaponNames = new List<string>();
        var damages     = new List<int>();
        var hpAfter     = new List<int>();

        foreach (var w in chosen)
        {
            int dmg = CalcThrowDamage(attacker, w);
            dmg = Mathf.RoundToInt(ApplyResistantCap(defender, dmg));
            if (defender.armor > 0f)
                dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - defender.armor)));

            defender.hp = ApplyDamage(defender, dmg);

            weaponNames.Add(w.weaponName);
            damages.Add(dmg);
            hpAfter.Add(defender.hp);
        }

        Emit(new CombatEvent
        {
            type        = CombatEventType.FlashFlood,
            playerIndex = attacker.index,
            targetIndex = defender.index,
            ffWeapons   = weaponNames,
            ffDamages   = damages,
            ffHpAfter   = hpAfter,
            maxHp       = defender.maxHp,
        });
        Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = defender.index, newHp = defender.hp, maxHp = defender.maxHp });
        CheckNetFreed(defender);
    }

    // --- Haste (Super) ---

    // Dash que atravessa o defensor — dano baseado em Speed (sem arma/STR, fórmula
    // speed × 1.5, ver CLAUDE.md), com +5% de chance de crítico exclusivo do Haste somado por
    // cima de CritChance() normal. Diferente de Flash Flood, PODE ser esquivado/bloqueado
    // normalmente (mesmas DodgeChance/BlockChance de qualquer hit) — só não passa por
    // Counter/Reversal/Desarme, já que não é um golpe corpo a corpo comum, é um dash que
    // atravessa o oponente. Não consome a arma em mão (currentWeaponData intocado).
    private void SimulateHaste(PlayerState attacker, PlayerState defender, PetState targetPet = null)
    {
        attacker.hasteUsesRemaining--;

        // Pets como alvo válido: dash contra um pet só rola a esquiva dele (evasionBase) — sem
        // Block (pets não têm), sem CheckNetFreed (netEnsnared de pet é permanente).
        if (targetPet != null)
        {
            int petIdx = defender.pets.IndexOf(targetPet);
            if (Roll(targetPet.evasionBase))
            {
                Emit(new CombatEvent { type = CombatEventType.HasteAttack, playerIndex = attacker.index, targetIndex = defender.index, targetIsPet = true, targetPetIndex = petIdx, isDodged = true });
                return;
            }

            bool  isCritPet = Roll(CritChance(attacker) + 0.05f);
            float dmgPet    = attacker.speed * 1.5f;
            if (isCritPet) dmgPet *= CritDamageMultiplier(attacker);
            int finalDamagePet = Mathf.Max(1, Mathf.RoundToInt(dmgPet));
            ApplyDamageToPet(targetPet, finalDamagePet);

            Emit(new CombatEvent { type = CombatEventType.HasteAttack, playerIndex = attacker.index, targetIndex = defender.index, targetIsPet = true, targetPetIndex = petIdx, damage = finalDamagePet, isCrit = isCritPet, newTargetHp = targetPet.hp, newTargetMaxHp = targetPet.maxHp });
            if (!targetPet.isAlive)
                Emit(new CombatEvent { type = CombatEventType.PetDeath, playerIndex = defender.index, petIndex = petIdx });
            return;
        }

        // Net: defensor enredado não pode esquivar/bloquear o dash também (mesmo gate de
        // SimulateHit) — a rede impede evadir o próximo ataque, qualquer que seja.
        if (!defender.netEnsnared && Roll(BlockChance(attacker, defender)))
        {
            Emit(new CombatEvent { type = CombatEventType.HasteAttack, playerIndex = attacker.index, targetIndex = defender.index, isBlocked = true });
            return;
        }
        if (!defender.netEnsnared && Roll(DodgeChance(attacker, defender)))
        {
            Emit(new CombatEvent { type = CombatEventType.HasteAttack, playerIndex = attacker.index, targetIndex = defender.index, isDodged = true });
            return;
        }

        bool  isCrit = Roll(CritChance(attacker) + 0.05f);
        float dmg    = attacker.speed * 1.5f;
        if (isCrit) dmg *= CritDamageMultiplier(attacker);
        dmg = ApplyResistantCap(defender, dmg);
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - defender.armor)));

        defender.hp = ApplyDamage(defender, finalDamage);

        Emit(new CombatEvent { type = CombatEventType.HasteAttack, playerIndex = attacker.index, targetIndex = defender.index, damage = finalDamage, isCrit = isCrit, newHp = defender.hp, maxHp = defender.maxHp });
        Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = defender.index, newHp = defender.hp, maxHp = defender.maxHp });
        CheckNetFreed(defender);
    }

    // --- Piledriver (Super) ---

    // Agarra o defensor, pula com ele e cai por cima — dano baseado na STR do DEFENSOR, não
    // do atacante (referência do My Brute: quanto mais forte/pesado o oponente, maior o
    // impacto da queda). NUNCA passa por Roll(BlockChance)/Roll(DodgeChance) — o grab já
    // aconteceu antes do pulo, não há janela pra reagir (diferente de Haste, que pode ser
    // esquivado/bloqueado).
    private void SimulatePiledriver(PlayerState attacker, PlayerState defender, PetState targetPet = null)
    {
        attacker.piledriverUsesRemaining--;

        // Pets como alvo válido: igual ao personagem, NUNCA esquivado — mas a STR usada na
        // fórmula passa a ser a do próprio PET agarrado (targetPet.str), não a do dono
        // (PetState tem o campo str, ver tabela de stats dos pets).
        if (targetPet != null)
        {
            int petIdx = defender.pets.IndexOf(targetPet);
            bool  isCritPet = Roll(CritChance(attacker));
            float dmgPet    = targetPet.str * 2.5f;
            if (isCritPet) dmgPet *= CritDamageMultiplier(attacker);
            int finalDamagePet = Mathf.Max(1, Mathf.RoundToInt(dmgPet));
            ApplyDamageToPet(targetPet, finalDamagePet);

            Emit(new CombatEvent { type = CombatEventType.PiledriverAttack, playerIndex = attacker.index, targetIndex = defender.index, targetIsPet = true, targetPetIndex = petIdx, damage = finalDamagePet, isCrit = isCritPet, newTargetHp = targetPet.hp, newTargetMaxHp = targetPet.maxHp });
            if (!targetPet.isAlive)
                Emit(new CombatEvent { type = CombatEventType.PetDeath, playerIndex = defender.index, petIndex = petIdx });
            return;
        }

        bool  isCrit = Roll(CritChance(attacker));
        float dmg    = defender.str * 2.5f;
        if (isCrit) dmg *= CritDamageMultiplier(attacker);
        dmg = ApplyResistantCap(defender, dmg);
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - defender.armor)));

        defender.hp = ApplyDamage(defender, finalDamage);

        Emit(new CombatEvent { type = CombatEventType.PiledriverAttack, playerIndex = attacker.index, targetIndex = defender.index, damage = finalDamage, isCrit = isCrit, newHp = defender.hp, maxHp = defender.maxHp });
        Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = defender.index, newHp = defender.hp, maxHp = defender.maxHp });
        CheckNetFreed(defender);
    }

    // --- Net / Fierce Brute / Bomb / Tragic Potion / Vampirism (Supers checados em ordem aleatória, ver SimulateTurn) ---

    // Net: SEMPRE acerta (sem dano, sem Roll de Block/Dodge) — retorna true porque consome o
    // turno inteiro do atacante (única das três que faz isso; ver SimulateTurn).
    private bool TryActivateNet(PlayerState attacker, PlayerState defender)
    {
        if (attacker.netUsesRemaining <= 0 || !Roll(0.50f)) return false;

        attacker.netUsesRemaining--;
        defender.lastSuperUsed = "Net";

        // Alvo: prioridade total nos pets — se houver qualquer pet vivo e não-enredado do
        // defensor, a rede SEMPRE pega um deles (aleatório entre os disponíveis). Só vai no
        // personagem quando todos os pets já estão mortos ou enredados. netEnsnared de pet é
        // PERMANENTE (PetState.netEnsnared nunca é solto, ver SimulatePetTurn) — diferente do
        // personagem, que se liberta no próximo hit que sofrer.
        var alivePets = new List<int>();
        for (int i = 0; i < defender.pets.Count; i++)
            if (defender.pets[i].isAlive && !defender.pets[i].netEnsnared) alivePets.Add(i);

        bool caughtPet    = alivePets.Count > 0;
        int  caughtPetIdx = caughtPet ? alivePets[_rng.Next(alivePets.Count)] : -1;

        if (caughtPet)
            defender.pets[caughtPetIdx].netEnsnared = true;
        else
            defender.netEnsnared = true;

        Emit(new CombatEvent { type = CombatEventType.NetThrow, playerIndex = attacker.index, targetIndex = defender.index, targetIsPet = caughtPet, targetPetIndex = caughtPetIdx });
        return true;
    }

    // Fierce Brute: só seta o buff — nunca consome o turno por conta própria. Normalmente
    // consumido ainda no MESMO turno, pela 1ª ação real dele (melee, ver SimulateHit, ou
    // arremesso, que zera o buff sem aplicar — ver branch de Throw acima). Mas se Net ou Bomb
    // também ativarem nesse turno (mesmo loop embaralhado "0d") e consumirem o turno antes do
    // personagem chegar a agir de verdade (Thief/pickup/throw/melee nunca são alcançados), o
    // buff persiste pendente pro turno seguinte (ghost trail continua visível) — comportamento
    // observado pelo usuário no My Brute eternaltwin ("ativa Fierce Brute, joga uma bomba em
    // seguida, passa o round, e fica com o ghost trail até o próximo round"). Antes disso ser
    // corrigido, Bomb nunca consumia o turno (caía direto pro fluxo normal no mesmo turno), então
    // esse cenário nem existia — ver TryActivateBomb.
    private void TryActivateFierceBrute(PlayerState attacker)
    {
        if (attacker.fierceBruteUsesRemaining <= 0 || !Roll(0.33f)) return;

        attacker.fierceBruteUsesRemaining--;
        attacker.fierceBruteActive = true;
        Emit(new CombatEvent { type = CombatEventType.FierceBruteActivated, playerIndex = attacker.index });
    }

    // Bomb (Super, 2x por luta): 17% de chance por turno. Explosão em área que atinge TODOS os
    // alvos do lado inimigo (ver GetEnemyTargets) com o MESMO dano sorteado — entre 15 e 25
    // inclusive (My Brute: "Between 15 and 25 damage inflicted on all your opponents"). NUNCA
    // esquivado/bloqueado (sem Roll de Dodge/Block — explosão em área, ninguém escapa dela),
    // SEM crítico, SEM STR do atacante, e o dano NÃO é reduzido por armadura (dano bruto direto
    // ao HP — ainda passa por ApplyDamage, então Survival/Chaining continuam funcionando
    // normalmente). **Consome o turno inteiro** quando ativa (retorna `true`) — comportamento
    // observado pelo usuário no My Brute eternaltwin: a bomba é a própria ação do turno, igual
    // a Net/Piledriver/Haste/Flash Flood (era `false`/nunca consumia antes, deixando o fluxo
    // normal — Thief/pickup/throw/melee — continuar no mesmo turno; redefinido pelo usuário).
    private bool TryActivateBomb(PlayerState attacker, PlayerState defender)
    {
        if (attacker.bombUsesRemaining <= 0 || !Roll(0.17f)) return false;

        attacker.bombUsesRemaining--;
        defender.lastSuperUsed = "Bomb";
        int rawDamage = _rng.Next(15, 26); // Next(min, max) é max-exclusivo: sorteia 15..25 inclusive

        var targets       = GetEnemyTargets(attacker);
        var targetIndexes = new List<int>();
        var targetDamages = new List<int>();
        var targetHp      = new List<int>();
        var netFreed      = new List<int>();

        foreach (var target in targets)
        {
            target.hp = ApplyDamage(target, rawDamage);
            targetIndexes.Add(target.index);
            targetDamages.Add(rawDamage);
            targetHp.Add(target.hp);

            // Quebra a rede de qualquer alvo enredado (oponente, pets/backup futuros) — sem
            // emitir um CombatEventType.NetFreed separado: a info já vai embutida no próprio
            // BombThrow (netFreedTargets), pra CombatPlayer tocar os fragmentos de rede já
            // sincronizados com o impacto da explosão, em vez de um evento solto depois.
            if (target.netEnsnared && !target.netEnsnaredPermanent)
            {
                target.netEnsnared = false;
                netFreed.Add(target.index);
            }
        }

        // Pets vivos do defensor também são atingidos pela mesma explosão, com o mesmo
        // rawDamage — sem Roll de esquiva (Bomb ignora evasão de qualquer alvo) e sem checar
        // netEnsnared (Bomb NÃO liberta pets da rede, diferente do personagem acima).
        var bombPetIndexes = new List<int>();
        var bombPetHpList  = new List<int>();
        for (int i = 0; i < defender.pets.Count; i++)
        {
            if (!defender.pets[i].isAlive) continue;
            ApplyDamageToPet(defender.pets[i], rawDamage);
            bombPetIndexes.Add(i);
            bombPetHpList.Add(defender.pets[i].hp);
        }

        Emit(new CombatEvent
        {
            // targetIndex = defender (1º alvo de GetEnemyTargets) — usado por CombatPlayer só
            // pra saber em direção a quem a bomba voa visualmente (ver case BombThrow); o dano
            // real por alvo já vem nas listas paralelas abaixo, que cobrem todos os alvos.
            // Sem isso, targetIndex ficava no default 0 — quando o próprio atacante já era o
            // índice 0 (P1), CombatPlayer resolvia "defender" como o próprio atacante e a bomba
            // parecia voar pra ele mesmo (bug real reportado pelo usuário).
            type              = CombatEventType.BombThrow,
            playerIndex       = attacker.index,
            targetIndex       = defender.index,
            damage            = rawDamage,
            bombTargets       = targetIndexes,
            bombTargetDamages = targetDamages,
            bombTargetHp      = targetHp,
            netFreedTargets   = netFreed,
            bombPetIndexes    = bombPetIndexes,
            bombPetHp         = bombPetHpList,
        });

        for (int i = 0; i < targetIndexes.Count; i++)
            Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = targetIndexes[i], newHp = targetHp[i], maxHp = targets[i].maxHp });

        for (int i = 0; i < bombPetIndexes.Count; i++)
            if (!defender.pets[bombPetIndexes[i]].isAlive)
                Emit(new CombatEvent { type = CombatEventType.PetDeath, playerIndex = defender.index, petIndex = bombPetIndexes[i] });

        return true;
    }

    // Tragic Potion (Super, 1x por luta): só pode rolar quando o HP atual já caiu abaixo de 60%
    // do máximo (My Brute: bebe a poção quando já tá levando a pior — não ativa estando acima
    // disso). 50% de chance por turno quando essa condição é satisfeita. Cura entre 25% e 50%
    // (sorteado) do HP MÁXIMO, nunca passando dele. Auto-uso — não ataca ninguém, não rola
    // Dodge/Block/Counter/Reversal, e nunca consome o turno (mesmo padrão de Fierce Brute/Bomb,
    // cai direto pro fluxo normal — Thief/pickup/throw/melee — do mesmo turno). Também cura o
    // veneno do Chef (skill ainda não implementada, ver PlayerState.poisoned).
    private void TryActivateTragicPotion(PlayerState attacker)
    {
        if (attacker.tragicPotionUsesRemaining <= 0) return;
        if (attacker.hp >= attacker.maxHp * 0.60f) return;
        if (!Roll(0.50f)) return;

        attacker.tragicPotionUsesRemaining--;
        float healPct = 0.25f + (float)_rng.NextDouble() * 0.25f; // 25%–50% do HP máximo
        int heal = Mathf.RoundToInt(healPct * attacker.maxHp);
        attacker.hp = Mathf.Min(attacker.maxHp, attacker.hp + heal);
        attacker.poisoned = false;

        Emit(new CombatEvent { type = CombatEventType.TragicPotionUse, playerIndex = attacker.index, healAmount = heal, newHp = attacker.hp, maxHp = attacker.maxHp });
    }

    // Vampirism (Super, 1x por luta): só pode rolar quando o atacante está com HP < 50% do
    // máximo (pedido pelo usuário — mesmo padrão de pré-condição de HP da Tragic Potion,
    // que usa 60%). Quando essa condição é satisfeita, 33% de chance por turno. Mordida
    // garantida — NUNCA pode ser esquivada ou bloqueada (sem Roll de Dodge/Block, mesmo
    // padrão de Net/Piledriver/Bomb). Causa 25% do HP que falta pro atacante (missingHp,
    // não o HP atual nem o máximo) como dano ao defensor, e cura o atacante na MESMA
    // quantidade do dano causado — mínimo de 1 nos dois (cobre o caso de HP cheio, onde
    // missingHp = 0 e a mordida ainda ativa com efeito mínimo — na prática nunca acontece
    // mais, já que a checagem de HP < 50% acima garante missingHp > 0). Consome o turno
    // inteiro quando ativa (retorna true) — mesma lógica de Bomb: a mordida É a própria
    // ação do turno, não um efeito que cai por cima e deixa Thief/pickup/throw/melee
    // continuarem.
    private bool TryActivateVampirism(PlayerState attacker, PlayerState defender, PetState targetPet = null)
    {
        if (attacker.vampirismUsesRemaining <= 0) return false;
        if (attacker.hp >= attacker.maxHp * 0.50f) return false;
        if (!Roll(0.33f)) return false;

        attacker.vampirismUsesRemaining--;
        defender.lastSuperUsed = "Vampirism";

        int missingHp = attacker.maxHp - attacker.hp;
        int damage    = Mathf.Max(1, Mathf.RoundToInt(missingHp * 0.25f));
        int heal      = damage;

        // Pets como alvo válido: a mordida (garantida, sem Dodge) atinge o pet em vez do
        // personagem — a cura do atacante continua igual (sempre soma na própria vida dele,
        // independente de quem levou o dano).
        if (targetPet != null)
        {
            int petIdx = defender.pets.IndexOf(targetPet);
            ApplyDamageToPet(targetPet, damage);
            attacker.hp = Mathf.Min(attacker.maxHp, attacker.hp + heal);

            Emit(new CombatEvent
            {
                type          = CombatEventType.VampirismAttack,
                playerIndex   = attacker.index,
                targetIndex   = defender.index,
                targetIsPet   = true,
                targetPetIndex = petIdx,
                damage        = damage,
                healAmount    = heal,
                newTargetHp   = targetPet.hp,
                newTargetMaxHp = targetPet.maxHp,
                newAttackerHp = attacker.hp,
            });
            Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = attacker.index, newHp = attacker.hp, maxHp = attacker.maxHp });
            if (!targetPet.isAlive)
                Emit(new CombatEvent { type = CombatEventType.PetDeath, playerIndex = defender.index, petIndex = petIdx });
            return true;
        }

        defender.hp = ApplyDamage(defender, damage);
        attacker.hp = Mathf.Min(attacker.maxHp, attacker.hp + heal);

        Emit(new CombatEvent
        {
            type          = CombatEventType.VampirismAttack,
            playerIndex   = attacker.index,
            targetIndex   = defender.index,
            damage        = damage,
            healAmount    = heal,
            newDefenderHp = defender.hp,
            newAttackerHp = attacker.hp,
        });
        Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = defender.index, newHp = defender.hp, maxHp = defender.maxHp });
        Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = attacker.index, newHp = attacker.hp, maxHp = attacker.maxHp });
        CheckNetFreed(defender);
        return true;
    }

    private void TryActivateCryOfTheDamned(PlayerState attacker, PlayerState defender)
    {
        if (attacker.cryOfTheDamnedUsesRemaining <= 0) return;
        if (!Roll(0.44f)) return;

        attacker.cryOfTheDamnedUsesRemaining--;
        defender.lastSuperUsed = "Cry of the Damned";
        Emit(new CombatEvent { type = CombatEventType.CryOfTheDamned, playerIndex = attacker.index });

        for (int i = 0; i < defender.pets.Count; i++)
        {
            var pet = defender.pets[i];
            if (!pet.isAlive) continue;
            if (!Roll(0.50f)) continue;
            pet.hp = 0;
            Emit(new CombatEvent { type = CombatEventType.PetFlee, playerIndex = defender.index, petIndex = i });
        }
    }

    private void TryActivateHypnosis(PlayerState attacker, PlayerState defender)
    {
        if (attacker.hypnosisUsesRemaining <= 0) return;
        if (!Roll(0.38f)) return;

        var livingPets = new List<int>();
        for (int i = 0; i < defender.pets.Count; i++)
            if (defender.pets[i].isAlive) livingPets.Add(i);
        if (livingPets.Count == 0) return;

        attacker.hypnosisUsesRemaining--;
        defender.lastSuperUsed = "Hypnosis";
        Emit(new CombatEvent { type = CombatEventType.Hypnosis, playerIndex = attacker.index });

        int idx = livingPets[_rng.Next(livingPets.Count)];
        if (!Roll(0.90f)) return;

        var pet = defender.pets[idx];
        defender.pets.RemoveAt(idx);
        attacker.pets.Add(pet);

        Emit(new CombatEvent {
            type        = CombatEventType.PetHypnotized,
            playerIndex = defender.index,
            petIndex    = idx,
            targetIndex = attacker.index
        });
    }

    private void TryActivateTamer(PlayerState attacker, PlayerState defender)
    {
        if (attacker.tamerUsesRemaining <= 0) return;

        // Coleta carcaças disponíveis: pets mortos e ainda não consumidos, de ambos os lados.
        var ownerIdxs = new List<int>();
        var petIdxs   = new List<int>();
        var pets      = new List<PetState>();

        for (int i = 0; i < attacker.pets.Count; i++)
        {
            var p = attacker.pets[i];
            if (!p.isAlive && !p.isConsumed) { ownerIdxs.Add(attacker.index); petIdxs.Add(i); pets.Add(p); }
        }
        for (int i = 0; i < defender.pets.Count; i++)
        {
            var p = defender.pets[i];
            if (!p.isAlive && !p.isConsumed) { ownerIdxs.Add(defender.index); petIdxs.Add(i); pets.Add(p); }
        }
        if (pets.Count == 0) return;
        if (!Roll(0.67f)) return;

        attacker.tamerUsesRemaining--;
        defender.lastSuperUsed = "Tamer";

        int choice  = _rng.Next(pets.Count);
        var carcass = pets[choice];

        int healMin = carcass.maxHp * 20 / 100;
        int healMax = carcass.maxHp * 50 / 100;
        int heal    = healMin + _rng.Next(System.Math.Max(1, healMax - healMin + 1));
        attacker.hp = System.Math.Min(attacker.hp + heal, attacker.maxHp);
        carcass.isConsumed = true;

        Emit(new CombatEvent {
            type        = CombatEventType.TamerEat,
            playerIndex = attacker.index,
            targetIndex = ownerIdxs[choice],
            petIndex    = petIdxs[choice],
            healAmount  = heal,
            newHp       = attacker.hp,
            maxHp       = attacker.maxHp,
        });
    }

    private void TryActivateTreat(PlayerState attacker, PlayerState defender)
    {
        if (attacker.treatUsesRemaining <= 0) return;

        // Prioridade: pet enredado (liberar Net é crítico) → menor HP relativo entre vivos.
        PetState targetPet   = null;
        int      targetPetIdx = -1;
        bool     foundEnsnared = false;
        float    lowestRatio  = 1f;

        for (int i = 0; i < attacker.pets.Count; i++)
        {
            var p = attacker.pets[i];
            if (!p.isAlive || p.isConsumed || p.shielded) continue;

            bool  ensnared = p.netEnsnared;
            float ratio    = (float)p.hp / p.maxHp;

            if      (ensnared && !foundEnsnared)                       { targetPet = p; targetPetIdx = i; foundEnsnared = true; lowestRatio = ratio; }
            else if (ensnared && ratio < lowestRatio)                  { targetPet = p; targetPetIdx = i; lowestRatio = ratio; }
            else if (!ensnared && !foundEnsnared && ratio < lowestRatio){ targetPet = p; targetPetIdx = i; lowestRatio = ratio; }
        }

        if (targetPet == null || !Roll(0.33f)) return;

        attacker.treatUsesRemaining--;
        defender.lastSuperUsed = "Treat";

        int heal = targetPet.maxHp / 2;
        targetPet.hp          = System.Math.Min(targetPet.hp + heal, targetPet.maxHp);
        targetPet.shielded    = true;
        targetPet.netEnsnared = false;

        Emit(new CombatEvent
        {
            type           = CombatEventType.TreatFeed,
            playerIndex    = attacker.index,
            petIndex       = targetPetIdx,
            healAmount     = heal,
            newTargetHp    = targetPet.hp,
            newTargetMaxHp = targetPet.maxHp,
        });

        // Ataque forçado: o pet alimentado ataca imediatamente.
        var aliveEnemyPetIdx = new List<int>();
        for (int i = 0; i < defender.pets.Count; i++)
            if (defender.pets[i].isAlive) aliveEnemyPetIdx.Add(i);
        bool forceTargetIsPet  = aliveEnemyPetIdx.Count > 0 && Roll(0.5f);
        int  forceTargetPetIdx = forceTargetIsPet ? aliveEnemyPetIdx[_rng.Next(aliveEnemyPetIdx.Count)] : -1;

        SimulatePetHit(attacker, targetPetIdx, defender, defender.pets, forceTargetIsPet, forceTargetPetIdx, comboCount: 0);
    }

    // --- Mimic (Super, 1x) ---

    // Copia a última Super do oponente (defender.lastSuperUsed) e executa diretamente,
    // ignorando HasSkill/usesRemaining/Roll da skill original — Mimic já passou pelo próprio Roll.
    // Filtragem inteligente impede copias sem sentido (Treat sem pet, Thief sem arma, etc.).
    private bool TryActivateMimic(PlayerState attacker, PlayerState defender, PetState targetPet = null)
    {
        if (attacker.mimicUsesRemaining <= 0) return false;
        string copied = defender.lastSuperUsed;
        if (string.IsNullOrEmpty(copied)) return false;
        if (!Roll(0.25f)) return false;

        // Filtragem: não copiar skills que não têm efeito no contexto atual
        switch (copied)
        {
            case "Treat":
                bool hasPet = false;
                foreach (var p in attacker.pets) if (p.isAlive) { hasPet = true; break; }
                if (!hasPet) return false;
                break;
            case "Thief":
                if (defender.currentWeaponData == null
                    && (defender.weaponLoadout == null || defender.weaponLoadout.Count == 0))
                    return false;
                break;
            case "Tamer":
                bool anyDead = false;
                foreach (var p in attacker.pets) if (!p.isAlive && !p.isConsumed) { anyDead = true; break; }
                foreach (var p in defender.pets) if (!p.isAlive && !p.isConsumed) { anyDead = true; break; }
                if (!anyDead) return false;
                break;
            case "Hypnosis":
            case "Cry of the Damned":
                bool defHasPet = false;
                foreach (var p in defender.pets) if (p.isAlive) { defHasPet = true; break; }
                if (!defHasPet) return false;
                break;
            case "Flash Flood":
                if (attacker.weaponLoadout == null || attacker.weaponLoadout.Count < 3) return false;
                break;
            case "Tragic Potion":
                if (attacker.hp >= attacker.maxHp * 0.60f) return false;
                break;
            case "Vampirism":
                if (attacker.hp >= attacker.maxHp * 0.50f) return false;
                break;
        }

        attacker.mimicUsesRemaining--;
        Emit(new CombatEvent { type = CombatEventType.Mimic, playerIndex = attacker.index, weaponName = copied });

        switch (copied)
        {
            case "Flash Flood":  SimulateFlashFlood(attacker, defender); return true;
            case "Haste":        SimulateHaste(attacker, defender, targetPet); return true;
            case "Piledriver":   SimulatePiledriver(attacker, defender, targetPet); return true;

            case "Net":
            {
                var alive = new List<int>();
                for (int i = 0; i < defender.pets.Count; i++)
                    if (defender.pets[i].isAlive && !defender.pets[i].netEnsnared) alive.Add(i);
                bool caughtPet = alive.Count > 0;
                int  caughtIdx = caughtPet ? alive[_rng.Next(alive.Count)] : -1;
                if (caughtPet) defender.pets[caughtIdx].netEnsnared = true;
                else           defender.netEnsnared = true;
                Emit(new CombatEvent { type = CombatEventType.NetThrow, playerIndex = attacker.index, targetIndex = defender.index, targetIsPet = caughtPet, targetPetIndex = caughtIdx });
                return true;
            }

            case "Fierce Brute":
                attacker.fierceBruteActive = true;
                Emit(new CombatEvent { type = CombatEventType.FierceBruteActivated, playerIndex = attacker.index });
                return false;

            case "Bomb":
            {
                int raw   = _rng.Next(15, 26);
                var tgts  = GetEnemyTargets(attacker);
                var tIdxs = new List<int>(); var tDmg = new List<int>();
                var tHp   = new List<int>(); var nFr  = new List<int>();
                foreach (var tgt in tgts)
                {
                    tgt.hp = ApplyDamage(tgt, raw);
                    tIdxs.Add(tgt.index); tDmg.Add(raw); tHp.Add(tgt.hp);
                    if (tgt.netEnsnared && !tgt.netEnsnaredPermanent) { tgt.netEnsnared = false; nFr.Add(tgt.index); }
                }
                var bpi = new List<int>(); var bph = new List<int>();
                for (int i = 0; i < defender.pets.Count; i++)
                {
                    if (!defender.pets[i].isAlive) continue;
                    ApplyDamageToPet(defender.pets[i], raw); bpi.Add(i); bph.Add(defender.pets[i].hp);
                }
                Emit(new CombatEvent { type = CombatEventType.BombThrow, playerIndex = attacker.index, targetIndex = defender.index, damage = raw, bombTargets = tIdxs, bombTargetDamages = tDmg, bombTargetHp = tHp, netFreedTargets = nFr, bombPetIndexes = bpi, bombPetHp = bph });
                for (int i = 0; i < tIdxs.Count; i++)
                    Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = tIdxs[i], newHp = tHp[i], maxHp = tgts[i].maxHp });
                for (int i = 0; i < bpi.Count; i++)
                    if (!defender.pets[bpi[i]].isAlive)
                        Emit(new CombatEvent { type = CombatEventType.PetDeath, playerIndex = defender.index, petIndex = bpi[i] });
                return true;
            }

            case "Tragic Potion":
            {
                float pct  = 0.25f + (float)_rng.NextDouble() * 0.25f;
                int   heal = Mathf.RoundToInt(pct * attacker.maxHp);
                attacker.hp = Mathf.Min(attacker.maxHp, attacker.hp + heal);
                attacker.poisoned = false;
                Emit(new CombatEvent { type = CombatEventType.TragicPotionUse, playerIndex = attacker.index, healAmount = heal, newHp = attacker.hp, maxHp = attacker.maxHp });
                return false;
            }

            case "Vampirism":
            {
                int miss = attacker.maxHp - attacker.hp;
                int dmg  = Mathf.Max(1, Mathf.RoundToInt(miss * 0.25f));
                if (targetPet != null)
                {
                    int pi = defender.pets.IndexOf(targetPet);
                    ApplyDamageToPet(targetPet, dmg);
                    attacker.hp = Mathf.Min(attacker.maxHp, attacker.hp + dmg);
                    Emit(new CombatEvent { type = CombatEventType.VampirismAttack, playerIndex = attacker.index, targetIndex = defender.index, targetIsPet = true, targetPetIndex = pi, damage = dmg, healAmount = dmg, newTargetHp = targetPet.hp, newTargetMaxHp = targetPet.maxHp, newAttackerHp = attacker.hp });
                    Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = attacker.index, newHp = attacker.hp, maxHp = attacker.maxHp });
                    if (!targetPet.isAlive) Emit(new CombatEvent { type = CombatEventType.PetDeath, playerIndex = defender.index, petIndex = pi });
                }
                else
                {
                    defender.hp = ApplyDamage(defender, dmg);
                    attacker.hp = Mathf.Min(attacker.maxHp, attacker.hp + dmg);
                    Emit(new CombatEvent { type = CombatEventType.VampirismAttack, playerIndex = attacker.index, targetIndex = defender.index, damage = dmg, healAmount = dmg, newDefenderHp = defender.hp, newAttackerHp = attacker.hp });
                    Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = defender.index, newHp = defender.hp, maxHp = defender.maxHp });
                    Emit(new CombatEvent { type = CombatEventType.HealthChanged, playerIndex = attacker.index, newHp = attacker.hp, maxHp = attacker.maxHp });
                    CheckNetFreed(defender);
                }
                return true;
            }

            case "Cry of the Damned":
            {
                Emit(new CombatEvent { type = CombatEventType.CryOfTheDamned, playerIndex = attacker.index });
                for (int i = 0; i < defender.pets.Count; i++)
                {
                    var pet = defender.pets[i];
                    if (!pet.isAlive || !Roll(0.50f)) continue;
                    pet.hp = 0;
                    Emit(new CombatEvent { type = CombatEventType.PetFlee, playerIndex = defender.index, petIndex = i });
                }
                return false;
            }

            case "Hypnosis":
            {
                var living = new List<int>();
                for (int i = 0; i < defender.pets.Count; i++)
                    if (defender.pets[i].isAlive) living.Add(i);
                if (living.Count == 0) return false;
                Emit(new CombatEvent { type = CombatEventType.Hypnosis, playerIndex = attacker.index });
                int idx = living[_rng.Next(living.Count)];
                if (!Roll(0.90f)) return false;
                var hpet = defender.pets[idx];
                defender.pets.RemoveAt(idx);
                attacker.pets.Add(hpet);
                Emit(new CombatEvent { type = CombatEventType.PetHypnotized, playerIndex = defender.index, petIndex = idx, targetIndex = attacker.index });
                return false;
            }

            case "Tamer":
            {
                var oIdxs = new List<int>(); var pIdxs = new List<int>(); var pets = new List<PetState>();
                for (int i = 0; i < attacker.pets.Count; i++) { var p = attacker.pets[i]; if (!p.isAlive && !p.isConsumed) { oIdxs.Add(attacker.index); pIdxs.Add(i); pets.Add(p); } }
                for (int i = 0; i < defender.pets.Count; i++) { var p = defender.pets[i]; if (!p.isAlive && !p.isConsumed) { oIdxs.Add(defender.index); pIdxs.Add(i); pets.Add(p); } }
                if (pets.Count == 0) return false;
                int ch      = _rng.Next(pets.Count);
                var carcass = pets[ch];
                int hMin = carcass.maxHp * 20 / 100;
                int hMax = carcass.maxHp * 50 / 100;
                int heal = hMin + _rng.Next(System.Math.Max(1, hMax - hMin + 1));
                attacker.hp = System.Math.Min(attacker.hp + heal, attacker.maxHp);
                carcass.isConsumed = true;
                Emit(new CombatEvent { type = CombatEventType.TamerEat, playerIndex = attacker.index, targetIndex = oIdxs[ch], petIndex = pIdxs[ch], healAmount = heal, newHp = attacker.hp, maxHp = attacker.maxHp });
                return false;
            }

            case "Treat":
            {
                PetState tPet = null; int tIdx = -1; bool found = false; float low = 1f;
                for (int i = 0; i < attacker.pets.Count; i++)
                {
                    var p = attacker.pets[i];
                    if (!p.isAlive || p.isConsumed || p.shielded) continue;
                    bool ens = p.netEnsnared; float rat = (float)p.hp / p.maxHp;
                    if      (ens && !found)             { tPet = p; tIdx = i; found = true; low = rat; }
                    else if (ens && rat < low)          { tPet = p; tIdx = i; low = rat; }
                    else if (!ens && !found && rat < low){ tPet = p; tIdx = i; low = rat; }
                }
                if (tPet == null) return false;
                int th = tPet.maxHp / 2;
                tPet.hp = System.Math.Min(tPet.hp + th, tPet.maxHp);
                tPet.shielded = true; tPet.netEnsnared = false;
                Emit(new CombatEvent { type = CombatEventType.TreatFeed, playerIndex = attacker.index, petIndex = tIdx, healAmount = th, newTargetHp = tPet.hp, newTargetMaxHp = tPet.maxHp });
                var epIdx = new List<int>();
                for (int i = 0; i < defender.pets.Count; i++) if (defender.pets[i].isAlive) epIdx.Add(i);
                bool fIsPet = epIdx.Count > 0 && Roll(0.5f);
                int  fPetIdx = fIsPet ? epIdx[_rng.Next(epIdx.Count)] : -1;
                SimulatePetHit(attacker, tIdx, defender, defender.pets, fIsPet, fPetIdx, comboCount: 0);
                return false;
            }
        }
        return false;
    }

    // Alvos PERSONAGEM do lado inimigo do atacante — hoje só o defensor (1v1). Pets vivos do
    // defensor (Fase 3) são tratados separadamente em TryActivateBomb (bombPetIndexes/
    // bombPetHp) em vez de entrarem nesta lista — PetState não é um PlayerState, então
    // misturar os dois tipos aqui exigiria um wrapper só pra isso. Preparado ainda pra uma
    // futura skill "Backup" (chama um aliado personagem): esse sim poderia entrar nesta lista.
    private List<PlayerState> GetEnemyTargets(PlayerState attacker)
    {
        var defender = attacker == _p1 ? _p2 : _p1;
        return new List<PlayerState> { defender };
    }

    // Pets como alvo válido (ver chamada em SimulateTurn) — cada pet vivo do defensor rola sua
    // própria chance independente; se 1 ou mais passarem, sorteia 1 entre eles. Sem efeito se o
    // defensor não tiver pets vivos.
    private PetState RollPetTarget(PlayerState defender)
    {
        if (defender.pets.Count == 0) return null;

        var passed = new List<PetState>();
        foreach (var pet in defender.pets)
        {
            if (!pet.isAlive || pet.netEnsnared) continue;  // enredado: invisível como alvo
            float chance = PetTargetChance(pet.type);
            if (chance > 0f && Roll(chance)) passed.Add(pet);
        }
        if (passed.Count == 0) return null;
        return passed[_rng.Next(passed.Count)];
    }

    private static float PetTargetChance(PetType type)
    {
        switch (type)
        {
            case PetType.Boar:   return 0.75f;
            case PetType.Monkey: return 0.50f;
            case PetType.Mouse:  return 0.50f;
            default: return 0f;
        }
    }

    // Embaralha a lista in-place (Fisher-Yates) usando o mesmo _rng do simulador — determinístico
    // por seed, igual a qualquer outro sorteio do arquivo (ApplySpySabotage, SimulateFlashFlood).
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
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
        // Fierce Brute: +10% crítico enquanto o buff estiver ativo (lido ANTES de SimulateHit
        // consumir/zerar fierceBruteActive depois do hit — ver lá).
        float fierceBruteBonus = attacker.fierceBruteActive ? 0.10f : 0f;
        return baseChance + weaponBonus + attacker.criticalChance + fierceBruteBonus;
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
        int finalHp = Mathf.Max(0, newHp);

        // Fast Metabolism: marca que sofreu dano — interrompe o burst de cura (10x 5%, todas no
        // mesmo turno) antes dele rodar pela 1ª vez (ver SimulateTurn, item "0c-bis"). Ativa o
        // burst (fastMetabolismPulseActive) na primeira vez que o HP cruza 50% do máximo pra
        // baixo — `fastMetabolismPulseCount == 0` garante que só ativa uma vez por luta, mesmo
        // que o HP volte a subir e caia de novo abaixo de 50% depois. O PRÓPRIO hit que ativa o
        // burst não conta como "sofreu dano" pra fins de interrupção (`crossedThreshold`) — sem
        // essa exceção, o burst seria cancelado na 1ª checagem por causa do hit que o ativou, e
        // nunca chegaria a disparar de verdade.
        if (target.HasSkill("Fast Metabolism"))
        {
            bool crossedThreshold = finalHp < target.maxHp * 0.5f && !target.fastMetabolismPulseActive && target.fastMetabolismPulseCount == 0;
            if (crossedThreshold)
                target.fastMetabolismPulseActive = true;
            else
                target.fastMetabolismTookDamage = true;
        }

        return finalHp;
    }

    // Net: qualquer hit de verdade solta o alvo enredado — chamado por cada call site de dano
    // (SimulateHit, SimulateRetaliation, SimulateThrow, SimulateFlashFlood, SimulateHaste,
    // SimulatePiledriver) DEPOIS de emitir o próprio evento de dano daquele hit, pra o NetFreed
    // sempre vir na ordem certa na lista de eventos (rede só "quebra" depois do golpe acontecer
    // visualmente, nunca antes). netEnsnaredPermanent (pets, Fase 3 — ainda não implementado)
    // nunca libera, mesmo tomando dano.
    private void CheckNetFreed(PlayerState target)
    {
        if (!target.netEnsnared || target.netEnsnaredPermanent) return;
        target.netEnsnared = false;
        Emit(new CombatEvent { type = CombatEventType.NetFreed, playerIndex = target.index });
    }

    private int CalcThrowDamage(PlayerState attacker, WeaponData data)
    {
        int weaponDamage = data == null ? 2 : RollWeaponDamage(data);
        return weaponDamage + attacker.str;
    }

    // --- Utilities ---

    private bool Roll(float chance) => (float)_rng.NextDouble() < chance;
    private void Emit(CombatEvent evt) => _events.Add(evt);
}
