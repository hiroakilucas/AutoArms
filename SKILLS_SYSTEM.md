# AutoArms — Skills: Sistema e Referência

## Skill System

### Arquitetura

| Arquivo | Tipo | Propósito |
|---|---|---|
| `Assets/Scripts/Data/SkillData.cs` | ScriptableObject | Dados de uma skill (nome, ícone, categoria, ativação) |
| `Assets/Scripts/Data/SkillDatabase.cs` | ScriptableObject | Lista mestre de todas as 38 skills |
| `Assets/ScriptableObjects/Skills/` | Assets | Um `SkillData.asset` por skill + `SkillDatabase.asset` |
| `Assets/Data/UI/Skills/` | Sprites | `skill_<nome>.png` — ícone de cada skill |
| `Assets/Editor/SkillAssetGenerator.cs` | Editor tool | Gera todos os assets via **Tools → AutoArms → Generate Skill Assets** |

### Enums
- `SkillCategory`: `CombatPassive`, `DefensePassive`, `StatBoost`, `WeaponPassive`, `Super`
- `SkillActivationType`: `Passive`, `Active`

### SkillHolder no PlayerCombat
`PlayerCombat` expõe:
- `List<SkillData> skills` — skills equipadas; visível no Inspector para testes
- `HasSkill(string name)` — retorna `true` se a skill está equipada
- `GetSkill(string name)` — retorna o `SkillData` ou `null`
- `LogSkillCheck(string name, bool triggered, string detail)` — **no-op** (logging removido do projeto, ver Logging Policy abaixo); mantido só para não quebrar os call sites existentes

### Como adicionar uma nova skill ao jogo
1. Abrir Unity → **Tools → AutoArms → Generate Skill Assets** (só necessário na primeira vez ou ao adicionar skills)
2. Encontrar o `.asset` em `Assets/ScriptableObjects/Skills/`
3. No Inspector do `PlayerCombat` de um personagem, adicionar o asset em **Skills — Teste**
4. Implementar o efeito em `PlayerCombat.cs` no método relevante (`ComboChance`, `DodgeChance`, `DisarmChance`, etc.) usando `HasSkill("Nome")`


### Skills que modificam stats (aplicadas em `CombatSceneLoader.ApplySkillStats`)

Todas calculam o bônus em runtime a partir do `profile.str`/`agility`/`speed`/`maxHealth` salvo, **exceto Vitality, Herculean Strength, Feline Agility, Lightning Bolt e Reconnaissance** — essas têm um componente permanente (`+18`/`+3`/`+3`/`+3`/`+5` somado direto em `profile.maxHealth`/`str`/`agility`/`speed`/`speed` no momento da escolha, em `CombatResultPanel.ApplyBonus`, igual a um pick de Atributo) além do componente runtime (`+50%`/`+150%`, igual às outras). Se replicar esse padrão (flat permanente + percentual runtime) para outra skill no futuro, replicar a checagem por `skillName` em `ApplyBonus` também.

**Iniciativa, crit chance, crit damage, evasion, reversal, counter, combo, armor, accuracy, block e reversal after block** agora também aparecem na tela de Stats (`CharacterPanel`) — `PlayerProfile.GetEffectiveStats()` retorna uma 15-tupla (`hp, str, agility, speed, initiative, criticalChance, critDamageBonus, evasion, reversal, counter, comboChanceBonus, armor, accuracy, blockBonus, reversalAfterBlock`). `MainMenuCharacterPreview` só usa os primeiros 5 (descarta os 10 últimos). Iniciativa é sempre flat puro (nunca entra no percentual líquido): `First Strike +200`, `Monk -200`, `Reconnaissance -200`, `Deity -200`. `criticalChance` é o campo bruto do profile (nenhuma skill o modifica ainda — `Fierce Brute` no roadmap ainda não implementada). `critDamageBonus`/`comboChanceBonus` não existem como campos no profile, são 100% derivados de skill (`Reconnaissance +0.5` crit dmg; `Fists of Fury +0.20` combo). `evasion`/`reversal`/`counter`/`armor`/`accuracy`/`blockBonus`/`reversalAfterBlock` são campos reais do profile (`Untouchable +0.30`/`Ballet Shoes +0.10` somam em evasion; `Deity +0.40`/`Hostility +0.30` somam em reversal, e Deity ainda soma `-100%` multiplicativo em evasion, aplicado depois das somas — ver linha da Deity abaixo; `Monk +0.40`/`Sixth Sense +0.10` somam em counter; `Armour +0.25`/`Extra Thick Skin +0.50`/`Toughened Skin +0.10` somam em armor; `Relentless +0.30` soma em accuracy; `Counter Attack +0.10` soma em blockBonus e `+0.90` em reversalAfterBlock).

**Quem age primeiro**: `CombatSimulator.SimulateRound` e `AttackSequencer.StartWhenReady` comparam iniciativa primeiro; **em empate** (default 0 pra todo personagem sem skill que a altere), quem tem mais `speed` age primeiro — antes não existia esse tie-break por speed, P1 sempre ganhava o empate de iniciativa independente de speed.

**Dano crítico com bônus de skill**: novo campo `critDamageBonus` (`PlayerState`/`PlayerCombat`, default 0) somado ao `critDamageMultiplier` da arma (ou `UnarmedStats.CritDamageMultiplier` se desarmado) em `CritDamageMultiplier()`/`CombatSimulator.CritDamageMultiplier()` — `+50%` de dano crítico (Reconnaissance) soma `+0.5` ali, igual ao padrão de `criticalChance` (bônus de chance) já existente.

**Stacking de percentuais**: quando mais de uma skill afeta o mesmo status (HP/STR/AGI/SPD), os percentuais são **somados num percentual líquido e aplicados uma única vez no final** (`hpPct`/`strPct`/`agiPct`/`spdPct` acumulados, depois `RoundToInt(valor × (1 + pct))`) — não multiplicação sequencial com arredondamento a cada skill. Isso evita resultados não-intuitivos por arredondamento em cascata: STR 9 com Herculean Strength (+50%) + Immortal (-25%) dá `9 × (1 + 0.5 - 0.25) = 9 × 1.25 = 11.25 → 11`. A versão antiga (×1.5 depois ×0.75, cada um arredondando o resultado da anterior) dava `9→14→10` (`RoundToInt` usa round-half-to-even: 13.5→14, depois 10.5→10) — surpreendia o jogador, que esperava somar os dois percentuais direto. Implementado em `PlayerProfile.GetEffectiveStats()`, `CombatSceneLoader.ApplySkillStats()` e `CombatSimulator.ApplySkillStats(PlayerState)` — as três cópias mantidas em sincronia.

| Skill | Modificações |
|---|---|
| Vitality | `profile.maxHealth += 18` **permanente**, aplicado uma única vez na escolha (`CombatResultPanel.ApplyBonus`, igual a um pick de Atributo) + `hpPct += 0.5` em runtime sobre esse valor já somado (mesmo padrão de Herculean/Feline/Lightning, só que em HP). |
| Herculean Strength | `profile.str += 3` **permanente**, aplicado uma única vez na escolha (`CombatResultPanel.ApplyBonus`, igual a um pick de Atributo) + `strPct += 0.5` (entra no percentual líquido de STR, sem penalidade de agilidade). Ex: STR 5 → escolhe a skill → `profile.str` vira 8 (permanente) → efetivo `8 × 1.5 = 12`. Se depois pegar +2 STR de atributo: `profile.str` vira 10 → efetivo `10 × 1.5 = 15`. |
| Feline Agility | `profile.agility += 3` **permanente**, aplicado uma única vez na escolha (`CombatResultPanel.ApplyBonus`, igual a um pick de Atributo) + `agiPct += 0.5` em runtime sobre esse valor já somado (mesmo padrão da Herculean Strength, só que em AGI). |
| Lightning Bolt | `profile.speed += 3` **permanente**, aplicado uma única vez na escolha (`CombatResultPanel.ApplyBonus`, igual a um pick de Atributo) + `spdPct += 0.5` em runtime sobre esse valor já somado (mesmo padrão de Herculean/Feline, só que em SPD — agora afeta o atributo `speed` real, ações extra no Speed System; antes afetava só `runSpeedMultiplier`, a velocidade da animação de correr, sem relação com ações extra). |
| Reconnaissance | `profile.speed += 5` **permanente** na escolha + `spdPct += 1.5` (+150%) em runtime sobre esse valor já somado + `initiative -= 200` (flat puro) + `critDamageBonus += 0.5` (+50% dano crítico, somado ao `critDamageMultiplier` da arma). |
| Immortal | `hpPct += 2.5` (+250%), `strPct -= 0.25`, `agiPct -= 0.25`, `spdPct -= 0.25` |
| Deity | `hpPct += 1.0` (+100%), `strPct += 1.0` (+100%), `agiPct -= 1.0` (-100%), `spdPct -= 0.90` (**-90%, não -100%** — speed fixo em 0 travava o player sem chance de ação própria nem de pegar arma; com -90% ainda existe chance de arredondar > 0 dependendo do speed base), `evasionPct -= 1.0` + `noEvasion = true` (-100% de verdade, "Dexterity" da descrição original — não significa nunca ser desarmado, significa sem resistência a ser atingido; mapeado pro campo `evasion` existente, não um campo "dexterity" novo — ver nota de `noEvasion` na seção **Counter e Reversal**), `reversal += 0.40`, `initiative -= 200`. Sem componente permanente (só percentuais + flat de iniciativa/reversal). Ver seção **Counter e Reversal** acima pro que `reversal` realmente faz agora. |
| Armour | `armor += 0.25` (flat, fora do percentual líquido — `armor` não é um dos quatro status que stackeiam) + `spdPct -= 0.15` (-15% SPD, entra no percentual líquido normalmente) |
| Extra Thick Skin | `armor += 0.50` |
| Toughened Skin | `armor += 0.10` |
| Untouchable | `evasion += 0.30` |
| Bodybuilder | `heavyDexterityBonus += 0.10`, `heavyHitSpeedBonus += 0.40` — **não afeta mais STR** (era `strPct += 0.5`/"STR × 1.5", redefinida pelo usuário). Informativo apenas no preview: o bônus real só vale enquanto empunha arma Heavy, checado vivo (`HasType(currentWeaponData, Heavy)`) em `DodgeChance`/`HitRoutine`/`CombatPlayer`, não fixado aqui. |
| Relentless | `accuracy += 0.30` (ver **Dodge** — reduz a esquiva do defensor; era `comboChanceBonus += 0.15`, redefinida pelo usuário) |
| Fists of Fury | `comboChanceBonus += 0.20` |
| Lead Skeleton | `leadSkeleton = true` |
| Ballet Shoes | `evasion += 0.10`, `firstHitAvoided = true` |
| First Strike | `initiative += 200` |
| Counter Attack | `blockBonus += 0.10`, `reversalAfterBlock += 0.90` (ver **Counter e Reversal** acima — era `counter += 0.40`/cancela hit antes de conectar, redefinida pelo usuário; Monk/Sixth Sense herdaram o papel de dar counter) |
| Sixth Sense | `counter += 0.10` — mesmo campo de Monk |
| Hostility | `reversal += 0.30` |
| Monk | `counter += 0.40`, `initiative -= 200` (era também `hitSpeed = 0` — removido, ver seção própria **Monk** em Combat Systems) |
| Martial Arts | `martialArts = true` — dobra `UnarmedStats.Damage` em `WeaponBaseDamage()`/`WeaponBaseDamage(attacker)` |
| Shock | `disarmChanceBonus += 0.50` (soma em `DisarmChance()`) |
| Weapon Master | `weaponsMaster = true` — habilita `sharpMult = 1.5` em `CalcDamage` quando a arma tem a tag Sharp (ver **Fórmula de Dano**) |
| Shield | `blockBonus += 0.45` (+45% block rate, mesmo campo de Counter Attack), `armor += 0.25` (-25% dano recebido, penalidade de mobilidade do escudo), `hasShield = true`. Visual permanente equipado em `CombatSceneLoader.Initialize` via `WeaponHandler.EquipShield(shieldWeaponData)` no `offHandBone` (braço oposto ao `handBone`, ex.: `handBone = Left Arm` → `offHandBone = Right Arm`) — fora do `WeaponLoadout`, nunca entra no ciclo de troca de armas. Ver **Desarmar do Escudo** abaixo. |
| Determination | Não altera nenhum stat em `ApplySkillStats` — checada vivo via `HasSkill("Determination")` em `SimulateHitWithDetermination` (ver **Determination** abaixo), mesmo padrão de Iron Head (sem flag cacheada). |
| Hideaway | Não altera nenhum stat em `ApplySkillStats` — checada vivo via `HasSkill("Hideaway")` em `ThrowChance`/`SimulateThrow` (ver **Hideaway** abaixo), mesmo padrão de Iron Head/Determination. |
| Sticky Hands | `stickyHands += 0.50` (campo cacheado, mesmo padrão antigo de `disarmChanceBonus`/`blockBonus` — diferente de Hideaway/Determination acima) — multiplica `DisarmChance()` e `ThrowChance()` por `(1 - stickyHands)` (ver **Sticky Hands** abaixo). |

> `accuracy` agora tem mecânica real (Relentless +0.30, ver **Dodge** em Combat Systems). `reversal` agora tem mecânica real, ver **Counter e Reversal** acima.
> Monk **não guarda mais** (era `hitSpeed = 0`, com um guard extenso espalhado por vários pontos do simulador e do caminho legado — tudo removido, ver seção própria **Monk** em Combat Systems).


### Roadmap de implementação das Skills (Fase 2.5)

- [x] Infraestrutura base do sistema de skills (SkillData, SkillDatabase, SkillHolder no PlayerCombat, SkillAssetGenerator)

#### Passivas de Combate
- [x] Relentless — +30% accuracy, reduz a esquiva do adversário (accuracy += 0.30, oposto de evasion — era +15% combo chance/comboChanceBonus += 0.15, redefinida pelo usuário; Fists of Fury herdou o papel de dar combo chance)
- [x] Fists of Fury — +20% combo chance (comboChanceBonus += 0.20 — não é mais "combo de socos desarmado melhorado", movida de Passivas de Armas pra aqui, mecânica definida pelo usuário)
- [x] Counter Attack — +10% block, +90% reversal exclusivo de depois de bloquear (blockBonus += 0.10, reversalAfterBlock += 0.90 — era +40% counter rate/cancela hit antes de conectar, redefinida pelo usuário; ver Counter e Reversal)
- [x] Sixth Sense — +10% counter rate (counter += 0.10, mesma mecânica do Monk — não é mais esquiva, mecânica definida pelo usuário)
- [x] Hostility — +30% reversal (reversal += 0.30, mecânica definida pelo usuário — não é mais "equipa a arma mais forte primeiro", movida de Passivas de Armas pra aqui por ser uma mecânica de combate, não de arma)
- [x] Monk — +40% counter rate, -200 iniciativa, ataca normalmente (counter += 0.40, initiative -= 200; era "nunca ataca/guarda" via hitSpeed = 0, redefinido pelo usuário — ver seção própria **Monk** em Combat Systems), aura laranja persistente durante a luta
- [x] Shock — +50% chance de desarmar o adversário a cada ataque (disarmChanceBonus += 0.50, soma em `DisarmChance()`)
- [x] Iron Head — +40% chance de derrubar a arma do atacante ao sofrer um hit (qualquer hit, incluindo combo) e interrompe o resto do combo do atacante (mesmo `interrupted = true` do Counter — ver **Counter e Reversal**)
- [x] Sabotage — +50% de chance a cada golpe acertado de destruir uma arma aleatória do HUD do adversário (qualquer uma, inclusive a que ele tem em mão); ver seção própria **Sabotage** em Combat Systems
- [x] Saboteur — skill do LaBrute (distinta de Sabotage acima): a 1ª arma que o oponente conseguir empunhar na luta (pickup ou roubo via Thief) quebra com 100% de certeza, fazendo-o soltar a arma e tomar Hurt (era "destrói 1 arma aleatória antes da luta + -100 iniciativa", redefinida pelo usuário); ver seção própria **Saboteur** em Combat Systems
- [x] Thief — 44% por turno de roubar a arma do oponente (se eu estiver desarmado e ele armado), até 2x por luta (`thiefUsesRemaining`); redefinida pelo usuário — era "ao acertar" no roadmap original, agora é ação de início de turno, igual ao pickup comum; ver seção própria **Thief** em Combat Systems pro visual (pula nas costas do adversário, balançam juntos 4x, desce com a arma)
- [x] Untouchable — +30% evasion (era 25%, rebalanceada pelo usuário)
- [x] First Strike — +200 initiative (ataca primeiro)
- [x] Chaining — 3 golpes melee consecutivos sem tomar dano estunam o adversário por 1 ação (não fazia parte do roadmap original, mecânica definida do zero pelo usuário); ver seção própria **Chaining** em Combat Systems
- [x] Chef — lança uma pizza envenenada na 1ª ação (1x por luta, sempre acerta), oponente sofre 1% do próprio HP máximo no fim de TODO turno dele até curar (Tragic Potion) ou a luta acabar; ver seção própria **Chef** em Combat Systems

#### Passivas de Defesa
- [x] Shield — +45% block rate (`blockBonus += 0.45`), +25% armor (`armor += 0.25`), visual permanente no braço oposto (`WeaponHandler.EquipShield`, fora do `WeaponLoadout`), desarme próprio com chance fixa de 10% que não soma `disarmChanceBonus`/`disarmBonus` (ver **Desarmar do Escudo** em Combat Systems)
- [x] Armour — +25% armor, -15% velocidade (era armor += 0.30 sem penalidade nenhuma; rebalanceada pelo usuário pra ter um trade-off)
- [x] Lead Skeleton — -15% dano de armas Heavy (leadSkeleton = true)
- [x] Extra Thick Skin — armor += 0.50 (50% redução de dano)
- [x] Toughened Skin — +10% armor (armor += 0.10, mecânica definida pelo usuário; skill não tinha SkillDef no gerador antes, adicionada do zero junto da implementação)
- [x] Survival — sobrevive com 1 HP uma vez por luta (`survivalUsed`, ver seção própria **Survival** em Combat Systems), +20% evasion/+20% block enquanto em 1 HP
- [x] Ballet Shoes — evasion +10%, primeiro golpe automaticamente esquivado
- [x] Resistant — nenhum hit isolado reduz mais que 25% do HP máximo (cap no dano bruto, antes de Lead Skeleton/armadura — não tinha essa entrada no roadmap, adicionada agora; ver seção própria **Resistant** em Combat Systems)
- [x] Sticky Hands — stickyHands = 0.50, reduz 50% chance de ser desarmado e 50% chance de throw acidental (próprio); ver seção própria **Sticky Hands** em Combat Systems
- [x] Fast Metabolism — regenera 1% do HP máximo todo turno + burst de 10x 5% (todas no mesmo turno, não mais uma por turno) abaixo de 50% HP enquanto não levar dano antes do burst rodar; -50% hit speed, -5% crítico; ver seção própria **Fast Metabolism** em Combat Systems
- [x] Repulse — 30% de chance de deflectir (refletir) o throw do oponente de volta para ele mesmo; o deflect tem +5% de crítico adicional

#### Passivas de Stats
- [x] Vitality — +18 HP permanente, +50% HP
- [x] Bodybuilder — str × 1.5
- [x] Herculean Strength — +3 STR permanente, +50% STR
- [x] Feline Agility — +3 AGI permanente, +50% AGI
- [x] Lightning Bolt — +3 SPD permanente, +50% SPD (atributo speed real, ações extra no Speed System — era runSpeedMultiplier × 1.5, velocidade de animação de correr)
- [x] Immortal — maxHealth × 3.5 (+250%), str/agility/speed × 0.75 (-25% cada)
- [x] Reconnaissance — +5 SPD permanente, +150% SPD, -200 initiative, +50% dano crítico (mecânica definida pelo usuário, não fazia parte do roadmap original)
- [x] Deity — +100% HP/STR, -100% AGI/evasion, -90% SPD, -200 initiative, +40% reversal (mecânica definida pelo usuário; introduziu a mecânica real de Counter/Reversal, ver seção própria em Combat Systems)
- [x] Determination — se o golpe não causa dano (esquiva/bloqueio/Counter do defensor), 60% de chance de tentar outro golpe imediatamente, recursivo até acertar ou falhar o roll (redefinida pelo usuário — era "+STR conforme perde HP" no roadmap original; ver seção própria **Determination** em Combat Systems)

#### Passivas de Armas
- [x] Weapon Master — +50% dano com arma afiada (tag Sharp) (`weaponsMaster = true`, ver `sharpMult` em **Fórmula de Dano**; era "+dano com qualquer arma" no roadmap original, redefinida pra só Sharp)
- [x] Martial Arts — +100% dano desarmado (`martialArts = true`, dobra `UnarmedStats.Damage` em `WeaponBaseDamage()`; era "combo de socos desarmado melhorado" no roadmap original, redefinida pelo usuário)
- [x] Hideaway — 50% chance de arremesso fixa (mesmo gate de throw-ou-melee de todo mundo, sem branch forçado), +25% bloqueio contra arremessos recebidos (reduz hit de 80% pra 55%), arma some da mão mas continua no loadout (não desaparece); ver seção própria **Hideaway** em Combat Systems
- [x] Spy — metade das armas do oponente (aleatórias) recebem -20% dano permanente antes do combate; os ícones das armas sabotadas ficam vermelhos no `WeaponHUD`. Skill exclusiva do LaBrute/eternaltwin, não existe no Muxxu original; ver seção própria **Spy** em Combat Systems
- [ ] Garimpeiro — pega uma arma aleatória do chão (das `fallenWeapons`) adicionando-a ao loadout; funciona como pickup normal (início de turno, 40% de chance, animação CatchWeapon), mas a fonte é o chão em vez do loadout original

#### Supers (ativas — usadas X vezes por luta)
- [x] Fierce Brute — 33% por turno (não consome a ação): dobra o dano e +10% crítico no 1º hit melee do mesmo turno, usos escalam com STR (1 + 1 a cada 30); ver seção própria **Fierce Brute** em Combat Systems
- [x] Tragic Potion — quando HP < 60% do máximo, 50% por turno de curar entre 25% e 50% do HP máximo (1x por luta) e curar o veneno do Chef (preparação, skill ainda não implementada); auto-uso, não ataca ninguém, não interage com dodge/block/counter/reversal, não consome o turno; ver seção própria **Tragic Potion** em Combat Systems
- [x] Flash Flood — 17% de chance por ação; com >= 3 armas no inventário (excluindo a equipada), arremessa 3 aleatórias contra o oponente em sequência rápida, sempre acertando (ignora dodge/block/pet) (1x por luta); ver seção própria **Flash Flood** em Combat Systems
- [x] Haste — 23% de chance por turno; dash que atravessa o oponente, dano baseado em Speed (sem arma/STR), +5% crítico, pode ser esquivado/bloqueado normalmente (1x por luta); ver seção própria **Haste** em Combat Systems
- [x] Piledriver — 17% de chance por turno; agarra o defensor, pula com ele e cai por cima, dano baseado na STR do DEFENSOR (não do atacante), NUNCA esquivado/bloqueado (1x por luta); ver seção própria **Piledriver** em Combat Systems
- [x] Net — 50% por turno, sempre acerta, sem dano: oponente perde o turno e não pode usar dodge/block/counter-attack/outras Supers até sofrer um hit (1x por luta); ver seção própria **Net** em Combat Systems
- [x] Bomb — 17% por turno: explosão entre 15-25 de dano em TODOS os alvos inimigos (hoje só o defensor, ver GetEnemyTargets), ignora dodge/block/crítico/STR/armor, quebra Net em quem estiver enredado (2x por luta), **consome o turno** (era `false`/nunca consumia, redefinido pelo usuário); ver seção própria **Bomb** em Combat Systems
- [x] Vampirism — quando HP < 50%, 33% por turno: mordida garantida (nunca esquivada/bloqueada), causa 25% do HP que falta pro atacante como dano ao defensor e cura o atacante na mesma quantidade (mínimo 1 nos dois), **consome o turno** (1x por luta); ver seção própria **Vampirism** em Combat Systems
- [x] Mimic — 1x por combate: copia e usa a última skill ativa do adversário (a última Super que ele ativou na luta); 25% por turno quando disponível; filtragem inteligente (Treat sem pet, Thief sem arma, etc.)
- [ ] Magneto — levita TODAS as armas do chão (`fallenWeapons`) para as costas do personagem, aponta cada uma em direção ao inimigo, depois solta tudo de uma vez; cada arma causa seu próprio dano (mesmo valor de arremesso: weaponDamage + STR), sempre acertam (ignora dodge/block); NUNCA consome armas do loadout — só as do chão; 1x por luta

#### Relacionadas a Pets
- [x] Hypnosis — 38% por turno: hipnotiza um pet inimigo vivo (90% de chance) — o pet troca permanentemente para o seu time (1x por luta); ver PETS.md
- [x] Cry of the Damned — 44% por turno: grito sobrenatural expulsa cada pet inimigo vivo com 50% de chance — o pet abandona a partida para sempre (2x por luta); ver PETS.md
- [x] Tamer — ver PETS.md
- [x] Treat — ver PETS.md

