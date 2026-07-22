# AutoArms — Pets Reference

### Pets (Fase 3)

Pets entram na luta desde o início, atacam separadamente com seus próprios atributos, e ficam
no chão (não destruídos) quando nocauteados — preparação pra skill futura **Tamer**, que
permitirá "comer" pets caídos. Referência: My Brute (Muxxu/eternaltwin), pets de combate.

**3 tipos** (`PetType` enum em `PlayerProfile.cs`). **Tiers T1/T2/T3 (2026-07-16)** — mesmo
espírito de `SkillData`/`WeaponData`: `PetData : ScriptableObject` (`Assets/Scripts/Data/
PetData.cs`, campos NOMEADOS em vez de `bonusValue1-7` — pet precisa de mais valores distintos
do que os 7 slots genéricos comportam) com `tier`/`previousTier`/`nextTier`/`icon`, um asset por
tier (`pet_mouse_t1/t2/t3.asset` em `Assets/ScriptableObjects/Pets/`, gerados por
`Tools > AutoArms > Generate Pet Tiers (T1, T2 & T3)`, ver `Assets/Editor/PetTierGenerator.cs`).
`PetState.Create(PetData)` (era `Create(PetType)` com switch hardcoded) lê os stats do asset.
`PetDatabase` novo (`Assets/Resources/PetDatabase.asset`, populado automaticamente pelo próprio
gerador — mesmo espírito de `WeaponDatabase`/`SkillDatabase`) resolve o `PetData` real a partir
de tipo+tier no save/load (`PlayerProfileConverter`/`CharacterDTOMap`, novo `PetTierRef`).
Tabela extraída manualmente da referência visual do My Brute e adaptada pro nosso jogo (aprovada
pelo usuário 2026-07-16 — **substitui por completo** os valores antigos, incluindo a remoção do
Counter/Reversal do Macaco, que não faz parte da tabela nova):

| Pet | Odds* | HP malus** | Initiative** | STR | AGI | SPD | HP | Especial |
|---|---|---|---|---|---|---|---|---|
| Rato (Mouse) T1/T2/T3 | 1.92% | 10% | 0 | 7/9/11 | 6/8/10 | 5/7/9 | 21/23/25 | Combo 20%/30%/40% |
| Macaco (Monkey) T1/T2/T3 | 0.10% | 25% | 1 | 24/29/34 | 17/21/25 | 25/29/33 | 34/38/42 | Evasão 20%/25%/30% · Combo 70%/75%/80% |
| Javali (Boar) T1/T2/T3 | 0.10% | 40% | 4 | 46/51/56 | 3/5/7 | 2/4/6 | 140/150/160 | Evasão 10%/15%/20% · Precisão 20%/30%/40% · Desarme 5%/10%/15% · Combo do oponente -20% (fixo) · Block do oponente -25% (fixo) |

**Dano = `Round(STR × 0.45)`** (não é mais uma coluna separada da tabela — ver "Dano do pet
deriva de STR" logo abaixo). Com os valores de STR acima: Rato 3/4/5, Macaco 11/13/15, Javali
21/23/25.

\* **Odds** — chance deste pet aparecer como opção de level-up; campo existe em `PetData` mas
**ainda não está conectado** no sorteio (`LevelUpEngine.DrawOption`) — aguardando o usuário
passar os odds de skill/arma junto, pra conectar tudo de uma vez.
\** **HP malus/Initiative são FIXOS por TIPO de pet, não escalam por tier** (confirmado pelo
usuário) — só STR/AGI/SPD/HP e os bônus especiais escalam (Dano escala junto, por ser derivado
direto de STR — ver "Dano do pet deriva de STR" abaixo). HP malus agora é **% do HP
máximo BASE do dono** (era valor fixo -12/-36/-48) e só é cobrado na 1ª aquisição (T1) — evoluir
pra T2/T3 (mesmo tipo escolhido de novo no level-up) não cobra HP de novo.

**Mecânica nova ainda NÃO implementada** (dado já existe em `PetData`, wiring de combate é uma
sub-fase separada, ver CLAUDE.md/Fase 3): Initiative (ordem entre os próprios pets do mesmo
dono), Accuracy do Javali (reduz esquiva do personagem contra os ataques dele), e os debuffs
fixos de Combo/Block do Javali no oponente (aura passiva durante a luta inteira). Evasão/Combo/
Desarme do próprio pet e Dano JÁ funcionam tier-a-tier desde esta sub-fase (BuildState/
SimulatePetHit já leem os valores do `PetData`).

**Dano do pet deriva de STR — campo `damage` removido (2026-07-21, decisão final do usuário,
substitui a "decisão em aberto" de 2026-07-17 registrada abaixo pra histórico)**: a investigação
de 2026-07-17 (ver texto original preservado no parágrafo seguinte) tinha confirmado que dano
fixo + STR separada não era bug, era design — mas o usuário decidiu nesta rodada que quer STR
como ÚNICA fonte de dano do pet, sem uma coluna "Dano" independente pra manter balanceada à mão.
`PetData.damage`/`PetState.damage` foram removidos por completo (classe, gerador
`PetTierGenerator.cs` e os 9 `.asset` em `Assets/ScriptableObjects/Pets/`); `CombatSimulator.
SimulatePetHit` agora calcula `int damage = Mathf.RoundToInt(pet.str * 0.45f)`. O multiplicador
`0.45` foi calibrado pelo usuário contra teste real em jogo (não é um valor teórico) — bate com a
faixa observada em combate pros 3 pets: Rato (STR 7/9/11) → 3/4/5 (teste real: 4-6), Macaco (STR
24/29/34) → 11/13/15 (teste real: 10-14), Javali (STR 46/51/56) → 21/23/25, que fica alto por
conta própria sem precisar inflar nada à mão — resolve de quebra o pedido separado do usuário de
"Javali apelão por ser lento e sem agi" (compensação natural: STR alta dele já vira dano alto
puro). **Não mexer nesse multiplicador sem re-validar contra teste real em jogo** — é o único
valor deste sistema calibrado por observação direta, não por fórmula. Consequência collateral
desejada: `PetState.ApplyLevelScaling` (Javali `+3 STR/tier` a cada 5 níveis do dono) agora
também aumenta o dano do Javali automaticamente no late game, sem precisar de um termo de
escalonamento próprio pra `damage` (que nunca existiu). Piledriver (`targetPet.str *
piledriverDmgMult`, já lia `str` direto) não foi afetado por esta mudança.

**Histórico da investigação original (2026-07-17, preservado — não é mais a decisão vigente)**:
na época, dano do pet (`CombatSimulator.SimulatePetHit`, `int damage = pet.damage`) era só o
valor fixo da coluna "Dano" da tabela — STR do pet era independente, nunca somada ao próprio
ataque (diferente do personagem principal, onde STR soma direto no dano da arma). Investigado a
pedido do usuário (suspeita de bug — "Macaco causa só 3 de dano, ignora STR") e confirmado que
NÃO era bug: tier-scaling do campo `damage` funcionava corretamente, a tabela aprovada tratava
STR e Dano como colunas separadas por design, e o sintoma reportado era de um save antigo com pet
preso em T1 pelo bug de duplicação (já corrigido separadamente). Na época, alertou-se que somar
`damage + str` direto infla o Javali de 5→51 (T1) e 15→71 (T3) — acima de qualquer arma T3 de
personagem no jogo. Essa era exatamente a razão de não ter sido implementado ali; a decisão atual
(usar só `str * 0.45`, sem somar a um `damage` fixo) evita esse problema porque não soma dois
termos — substitui um pelo outro, com um multiplicador calibrado pra manter a faixa de dano
parecida com a que já existia.

`PlayerProfile.pets` (`List<PetData>`, era `List<PetType>`) — sem restrição de duplicatas ainda
(evoluir em vez de somar cópia é a próxima sub-fase). `PlayerState.pets`/`PetState` (pure C#,
mesmo padrão de `PlayerState`) guardam o estado de cada pet durante a simulação — `CombatSimulator.BuildState`
constrói a lista a partir de `profile.pets`.

**Speed System — sistema de iniciativa ATB (2026-07-21, substitui por completo os dois modelos
anteriores)**: Player1, Player2 e cada pet vivo de cada lado entram na MESMA fila de iniciativa
(`CombatSimulator.RunInitiativeLoop`) — não existe mais uma regra de pets separada da de
personagem. Cada combatente tem um contador (`PlayerState.speedDebt`/`PetState.speedDebt`,
reaproveitados) que soma a própria `speed` a cada tick de simulação; ao atingir/ultrapassar
`CombatSettings.initiativeThreshold` (`Assets/Resources/CombatSettings.asset`, default 100), o
combatente age e o limiar é subtraído do contador (overflow mantido — quem tem `speed` bem acima
do limiar pode agir mais de 1x no mesmo tick). Quando mais de um combatente cruza no mesmo tick
(pets e personagens juntos, sem distinção), a ordem é decrescente pelo valor do contador; empates
exatos são resolvidos por sorteio — inclusive entre um pet e um personagem, se cruzarem com o
mesmo valor. `initiative` (stat só de `PlayerState`, pets nunca tiveram) só entra como desempate
na PRIMEIRA leva de cruzamentos da luta inteira, e só quando o grupo empatado é exatamente
Player1×Player2. Ver CLAUDE.md → Sistema de Iniciativa (ATB) pro detalhe completo do algoritmo.

**Tentativa revertida (2026-07-21, anterior a esta implementação)** — uma fila unificada
diferente desta (`SpeedEntry`/`BuildSpeedRoster`/`ResolveRoundOrder`/`ExecuteCluster`, comparando
cada combatente contra a MENOR speed do campo) foi testada e revertida a pedido do usuário
("ta tudo errado e piorou"). O sistema acima é uma reimplementação do zero, com algoritmo
diferente (contador vs. limiar fixo, não comparação relativa), validada numericamente contra 2
exemplos fornecidos pelo usuário antes de codar — não reaproveita nada da tentativa revertida.

**Turno do pet** (`CombatSimulator.SimulatePetTurn`/`SimulatePetHit`) — alvo decidido 1x por
turno (não re-sorteado a cada hit de combo): usa `RollPetTarget(enemy)` (Javali=75%,
Macaco=50%, Rato=50% de interceptar; pula pets `netEnsnared`) — se retornar `null`, ataca o
personagem principal. Antes era `Roll(0.40f)` com pet aleatório, sem respeitar as chances
por tipo nem excluir enredados. Dano = `Round(STR × 0.45)` (ver "Dano do pet deriva de STR"
acima) — valor único por hit, não é mais um range aleatório. Esquiva contra personagem usa a mesma fórmula de `DodgeChance` mas sem o termo
`accuracy` do atacante (pet não tem esse stat, ver `PetDodgeChanceOnCharacter`); esquiva contra
pet usa só o `evasionBase` do alvo. Combo do pet: `pet.comboRate × 0.5^comboCount`, decaimento
igual ao personagem, mas com teto fixo de **3 hits extras** (sem decair até ficar irrelevante).
**Macaco sendo atacado** por outro pet conta Counter (cancela o hit antes de conectar,
interrompe o combo do atacante — mesmo padrão do Counter de personagem) e Reversal (contra-ataca
depois de já ter tomado dano, não interrompe) via `SimulatePetRetaliation` — "simples": só rola
a esquiva do alvo, sem recursão de Counter/Reversal. **Javali** desarma o personagem (15%, só no
1º hit do turno, nunca contra outro pet) via `CombatEventType.PetDisarm`.

**Interações com skills existentes**:
- **Net** — alvo decidido ANTES de imobilizar: se o defensor tem pets vivos e não-enredados,
  a rede **SEMPRE pega um deles** (100% de prioridade, aleatório entre os elegíveis) — nunca
  vai no personagem enquanto houver qualquer pet vivo e não-enredado. `PetState.netEnsnared`
  é setado no pet capturado; `SimulatePetTurn` checa isso no topo (skip completo do turno,
  mesmo padrão do personagem). Pet enredado **não pode ser alvo de nenhum ataque** —
  `RollPetTarget` (usado tanto pelo turno de personagem quanto pelo turno de pet inimigo)
  exclui `pet.netEnsnared`; a rede em si também já filtra `!netEnsnared` ao montar `alivePets`
  (evita reenredar o mesmo pet). **Visual**: a rede voa até a posição do pet (não do
  personagem) e chama `PetAnimationController.ShowNetEnsnared` ao pousar — mesmo sprite
  `net2`, sorting layer `Characters`/order 100 (acima de todos os sprites do corpo do pet),
  com `NetOscillateLoop` igual ao do personagem. `ReleaseNet()` para o loop e devolve o
  `GameObject` pro chamador.
- **Treat** — liberta o pet enredado: `TryActivateTreat` prioriza pet com `netEnsnared=true`
  (menor HP relativo entre os enredados, depois entre os não-enredados), seta
  `targetPet.netEnsnared = false`, e o visual (`TreatFeed`) chama
  `PlayNetBreakEffect(fedPet, t)` — mesmo efeito de scale-up + 5 fragmentos radiais que
  acontece quando o personagem é atacado enquanto enredado.
- **Bomb** — pets vivos do defensor são atingidos pela mesma explosão (mesmo dano bruto, sem
  esquiva/crítico/STR/armadura, igual ao personagem) — `CombatEvent.bombPetIndexes`/
  `bombPetHp` (paralelas), tratadas separado de `GetEnemyTargets`/`bombTargets` porque
  `PetState` não é um `PlayerState` (misturar os dois tipos exigiria um wrapper só pra isso).
  Bomb **não** liberta pets da rede (diferente do personagem, que tem a rede quebrada pela
  explosão).
- **Fierce Brute — Rato como escudo vivo** — quando o hit dobrado de Fierce Brute vai acertar o
  personagem e o defensor tem um Rato vivo, 50% de chance do Rato interceptar o golpe no lugar
  dele (`CombatSimulator.SimulateHit`, dentro do bloco `fierceBruteThisHit`) — dano vai direto
  pro Rato, sem Resistant/Lead Skeleton/armadura do personagem (nunca chegam a entrar em jogo).
  Emite `CombatEventType.PetAttack` com `shieldIntercept = true` (reaproveita o evento de ataque
  de pet pra representar "pet sendo atingido", não "pet atacando").
- **Chef/poison, Flash Flood, Haste, Piledriver, Vampirism, Tragic Potion** — todos continuam
  escopados só ao personagem principal, sem nenhuma mudança de código (nunca leem/escrevem
  `PlayerState.pets`/`PetState.poisoned`).

**Novos `CombatEventType`** (`CombatEvent.cs`): `PetTurnStart`, `PetAttack` (`petIndex`,
`targetIsPet`, `targetPetIndex`, `newTargetHp`/`newTargetMaxHp` — campos próprios, não reusam
`newHp`/`maxHp`, que no resto do arquivo representam sempre um jogador; `shieldIntercept` pro
caso do Rato acima), `PetNetSkip`, `PetDeath`, `PetDisarm`, `PetTurnEnd`. `playerIndex` sempre =
índice do DONO do pet; quando o alvo de um `PetAttack` é outro pet, `targetIndex` = dono do pet
alvo (não o pet em si) e `targetPetIndex` = índice na lista dele.

**Instanciação em cena** (`CombatSceneLoader.SpawnPets`, chamado em `Initialize()` só quando o
simulador está ativo) — 3 campos novos `boarPetPrefab`/`monkeyPetPrefab`/`mousePetPrefab`
(prefabs em `Assets/Data/UI/Pets/<Tipo>/Vector Parts/<Tipo>.prefab`, wirear no Inspector da cena
`04_CombatScenePVP`). Cada pet de `profile.pets`/`player2Profile.pets`: escala inicial
(`PetState.Scale` — Rato 0.30, Macaco 0.35, Javali 0.60, aumentada a pedido do usuário —
era 0.15/0.20/0.25), posição acima da câmera (mesma
convenção `spawnY + 12f` dos personagens principais) com X aleatório dentro da arena (P1:
-5 a -1, P2: 1 a 5), `AddComponent<PetCombatController>` (que por sua vez já adiciona
`PetAnimationController`/`MovementController` no próprio `Awake`), `HealthBarPet.Create`, e o
mesmo `EntryFall` coroutine dos personagens (squash de impacto incluso). `CombatPlayer.p1Pets`/
`p2Pets` (`List<PetCombatController>`) só são atribuídos depois que TODOS os pets e os 2
personagens principais terminam de pousar (`WaitUntil` combinado).

**Componentes novos**:
- `PetAnimationController.cs` — resolve o `Animator` do prefab (Spriter2UnityDX), expõe
  `SetIdle`/`PlayRun`/`PlaySlash`/`PlayHurt`/`PlayJump` (esquiva — trigger `"Jumping"`, **não**
  `"Jump_Loop"`)/`PlayDying`. Os 3 Animator Controllers dos pets vieram **sem nenhum parâmetro
  nem transição** (`m_AnimatorParameters: []`, todo estado com `m_Transitions: []` —
  diferente dos personagens principais, que já tinham isso ajustado manualmente) — novo
  `Assets/Editor/PetAnimatorSetup.cs` (**Tools → AutoArms → Setup Pet Animators**) recria do
  zero as 6 states (`Idle`/`Running`/`Slashing`/`Hurt`/`Jumping`/`Dying`) apontando direto pros
  `.anim` avulsos em `Assets/Data/UI/Pets/<Tipo>/Animations/`, com parâmetros (`Idle`/`Running`
  bool, `Slashing`/`Hurt`/`Jumping`/`Dying` trigger) e transições equivalentes (versão
  simplificada) ao padrão dos personagens — `AnyState → Slashing/Hurt/Jumping/Dying`,
  `Idle ↔ Running`, retorno automático pro Idle depois de Slashing/Hurt/Jumping
  (`ExitTime = 0.9`), e **Dying sem transição de saída** (trava no último frame, `Loop Time =
  false`, preparação pra Tamer). O Boar também tinha estados decoy de uma versão anterior do
  rig (`Sleep`/`Walking`/`Base`/`"Jump Loop"` — esse último apontando pra um clipe embutido
  diferente do `Jumping.anim` avulso) — todos removidos pelo gerador junto da reconstrução.
  **Pendente do usuário**: rodar **Tools → AutoArms → Setup Pet Animators** no Editor antes de
  testar combate com pets — sem isso, os pets ficam presos no estado default do Animator,
  sem nenhuma animação reagindo aos eventos de combate.
  - **Bug encontrado depois do 1º teste em combate, reportado pelo usuário**: o Rato se movia
    de posição normalmente (corria, atacava, voltava ao spawn) mas a POSE visual nunca mudava
    — sempre a mesma sprite estática, sem nenhum erro no Console. Causa raiz: os clipes em
    `Animations/*.anim` são **flipbooks simples** — cada um troca o `m_Sprite` de UM
    `SpriteRenderer` no `path=""` (a própria GameObject que tem o `Animator`), ciclando os
    frames de `PNG Sequences/<Estado>/*.png` (confirmado lendo `Idle.anim`: 18 keyframes de
    `m_Sprite`, batendo 1:1 com `Idle_000..017.png`). O prefab `Vector Parts/<Tipo>.prefab`
    (rig multi-bone exportado do Spriter, usado até então como o GameObject de gameplay) tem o
    `Animator` na raiz mas **sem nenhum `SpriteRenderer` nela** — só nos ossos filhos (Head/
    Body/Tail/etc, cada um com seu próprio `SpriteRenderer`) — a curva de `m_Sprite` em
    `path=""` não tinha componente nenhum pra escrever, falhando em silêncio (sem warning),
    enquanto o Animator continuava transicionando de estado normalmente por baixo (daí a
    posição mudar mas a pose nunca mudar). Confirmado o mesmo problema estrutural em Boar e
    Monkey (mesmo padrão de asset pack). Fix: novo `Assets/Editor/PetPrefabGenerator.cs`
    (**Tools → AutoArms → Generate Pet Gameplay Prefabs**) gera o prefab de gameplay CORRETO
    por tipo — 1 GameObject raiz só com `SpriteRenderer` (sprite inicial = `Idle_000`, sorting
    layer `Characters`) + `Animator` (controller já montado por `PetAnimatorSetup`), sem
    nenhum osso — salvo em `Assets/Data/UI/Pets/<Tipo>/<Tipo>Pet.prefab` (`BoarPet`/
    `MonkeyPet`/`MousePet`). **Pendente do usuário**: rodar esse comando e trocar os 3 campos
    `boarPetPrefab`/`monkeyPetPrefab`/`mousePetPrefab` no Inspector do `CombatSceneLoader`
    (cena `04_CombatScenePVP`) pra apontar pros novos prefabs em vez dos antigos
    `Vector Parts/<Tipo>.prefab`.
- `PetCombatController.cs` — `RunToTarget`/`ReturnToSpawn` (reusa `MovementController.MoveTo`
  genérico, já existente, sem acoplamento a `PlayerCombat`), `FlipToward`/`FlipToInitial`
  (mesmo mecanismo de flip por `localScale.x` já usado no projeto), `PlayDeath` (toca
  `Dying`, faz fade da `HealthBarPet`, adiciona à lista estática `deadPets` — **nunca destrói o
  GameObject**). `CleanupDeadPets()` (static) só limpa essa lista de rastreamento entre lutas —
  chamado por `AttackSequencer.OnCombatEnd`, junto de `PlayerCombat.CleanupFallenWeapons()`.
  **`PlayAttackSequence` dividido em dois (2026-07-22)** — bug real reportado pelo usuário: o
  pet voltava ao spawn e corria de novo a CADA hit de um combo (3-4 hits = 3-4 ciclos completos
  de corrida+retorno), diferente do personagem, que só reposiciona SE precisar entre hits de
  combo (`CombatPlayer.RepositionIfNeeded`) e só retorna ao spawn 1x, no fim do turno. Agora:
  `PlayAttackHit(target, reposition, isDodged, damage, onImpact, ...)` — 1x por evento
  `PetAttack` (1 por hit, inclusive combo); `reposition` (decidido por `CombatPlayer`: sempre
  `true` no 1º hit do turno, `!evt.isCombo`; nos hits de combo seguintes, só `true` se a
  distância atual até o alvo for > 0.3 unidades, mesmo threshold do personagem) decide se corre
  até o alvo antes de atacar ou ataca do lugar onde já está — vira pro alvo, corre condicional,
  ataca, `onImpact` callback, pausa; **sem retorno ao spawn nenhum aqui**. `PlayReturnToSpawn
  (runSpeed, jumpHeight)` — extraído da cauda da função antiga (vira pro spawn → pula em arco →
  idle → restaura direção), chamado 1x só, no `case PetTurnEnd` de `CombatPlayer` (não mais a
  cada `PetAttack`) — só quando o pet realmente se moveu neste turno (`Vector2.Distance(pet.
  transform.position, pet.spawnPosition) > 0.1f`; turnos pulados por morte/rede nunca tiram o
  pet do lugar, então o retorno é pulado automaticamente sem precisar rastrear um flag à parte).
  `CombatEvent.PetAttack` ganhou `isCombo` (true a partir do 1º hit EXTRA de combo,
  `comboCount > 0` em `CombatSimulator.SimulatePetHit`) só pra alimentar essa decisão — não
  afeta nenhum cálculo de combate.
- `HealthBarPet.cs` (`Assets/Scripts/UI/`) — mesmo padrão world-space Canvas de `HealthBar.cs`
  (personagens principais), mas mais fino (80×8px), sem texto, verde pouco-visível
  (`Color(0.2, 0.8, 0.2, 0.7)`) sobre fundo preto semi-transparente, e `FadeOutAndDestroy(2s)`
  em vez de simplesmente desaparecer ao morrer.

**Level-up** (`CombatResultPanel.cs`) — `LevelUpOption.Kind.Pet` (novo, junto de
Attribute/Skill/Weapon) com peso `wPet = 0.10f` somado ao denominador junto dos outros 3 (sem
degradar a 0 quando vazio — o pool de pets é sempre `{Mouse, Monkey, Boar}`, nunca vazio, ao
contrário de Skill/Weapon). `ApplyBonus` adiciona o tipo a `profile.pets` e subtrai o custo de
HP (tabela acima) de `profile.maxHealth`, clampado em `Mathf.Max(1, ...)`. Também adicionado à
grade de teste (`ShowAllOptionsChoice`, `ShowAllOptionsForTesting = true` atualmente ativo).

**Investigação de queda de FPS reportada pelo usuário** (depois dos pets entrarem em combate)
— nenhuma das hipóteses sobre os PETS em si se confirmou (`SpawnGhostTrail` já é escopado só ao
`transform` do personagem, nunca varre pets; `HealthBarPet.UpdateBar` já é 100% event-driven,
sem nenhum `Update()` tocando `fillAmount`; `HealthBarPet.Create` já só é chamado 1x por pet;
`BoarPet`/`MonkeyPet`/`MousePet.prefab` não têm Collider2D/Rigidbody2D — `MovementController`
sempre usa `transform.position` puro). A causa real era em mecânicas de PERSONAGEM
pré-existentes, sem nenhum stop garantido ao fim da luta — agravado pelos pets só por
coincidência de timing (a luta ficou mais comprida com pets em jogo, aumentando a chance de
terminar com algum desses loops ainda ativo):
- **Aura do Monk** (`PlayerCombat.MonkAuraPulseLoop`) é permanente "durante a luta" por design
  (nunca tinha um `Hide`) — `while (monkAura != null) yield return null;` continuava pulsando
  TODO FRAME mesmo depois do `CombatEnd`, durante o tempo indefinido em que a tela de
  resultado/level-up fica aberta (a cena `04_CombatScenePVP` continua carregada por baixo
  dela). Mesmo risco pra quem terminasse a luta ainda net-ensnared (`NetFaceLoop`/
  `NetOscillateLoop`) ou com a aura de Fierce Brute/Poison ainda ativa.
- Fix: `CombatPlayer.StopLingeringLoops()` (chamado no topo de `TriggerCombatEnd`, antes de
  `sequencer.OnCombatEnd`) — `HideStunLabel`/`HideFierceBruteAura(0f)`/`HidePoisonAura(0f)`/
  novo `PlayerCombat.StopMonkAuraPulse()`/`ReleaseNet()` (destruindo o `netVisual` retornado
  direto, sem animar fragmentos — irrelevante já com a luta decidida) pros dois personagens, e
  `FadeFastMetabolismLeaves(0)`/`FadeFastMetabolismLeaves(1)`. Todos já eram idempotentes/no-op
  se o efeito nunca esteve ativo.
- **Pets mortos com Animator ainda ativo** — esse sim era um custo real introduzido pelos pets:
  `PetCombatController.PlayDeath` tocava `Dying` mas nunca desligava o `Animator` depois —
  como o pet morto NUNCA é destruído (preparação pra Tamer), o Animator continuava avaliando
  esse estado parado (já travado no último frame, sem nenhuma mudança visual a mais) pelo resto
  da luta inteira. Novo `PetAnimationController.DisableAfterDying()` — lê a duração real do
  clipe `Dying` (`animationClips`, mesma técnica de `AnimationAutoDestroy`) e desliga
  `_animator.enabled` depois dela, chamado por `PlayDeath` logo depois do trigger.
- Limpeza/instrumentação menor: removido um `Debug.Log` de diagnóstico esquecido em
  `SpawnGhostTrail` (de quando a Fierce Brute foi implementada, nunca removido — chamava
  `GetComponentsInChildren` + log a cada ~0.06s durante 1.5s do trail, somando bem mais chamadas
  do que aparenta). Adicionado um novo `Debug.Log($"[PetHUD] Criando barra para {petType}")` em
  `CombatSceneLoader.SpawnPets` (diagnóstico, a pedido do usuário, pra confirmar visualmente no
  Console que cada pet recebe exatamente 1 Canvas — já era o caso antes desta investigação,
  `SpawnPets` só itera 1x por dono). Novo `CombatPlayer.Update()` com toggle de
  `UnityEngine.Profiling.Profiler.enabled` na tecla **P** — diagnóstico pra próximas
  investigações, sem custo relevante (só reage a uma tecla específica).

**Continuação da investigação de FPS (2026-06-24) — causa real era as centenas de `Debug.Log`
de instrumentação temporária, removidas (ver Logging Policy abaixo); depois disso o usuário
reportou que a sensação de FPS baixo persistia DENTRO da cena de combate (não só na entrada).
Profiler do usuário: Rendering (verde) dominante, baseline ~30fps, picos a 15fps com Scripts
(amarelo) contribuindo nos spikes — confirmado com os 2 profiles de teste (`Medieval Warrior`/
`Medieval Warrior Girl`) **sem nenhum pet** (`profile.pets` vazio nos dois) — ou seja, o custo de
Rendering de base **não tem nada a ver com pets**, acontece em qualquer luta normal de 2
personagens.**

- **Causa do baseline (Rendering)**: cada rig de personagem (`Assets/Personagens/<Nome>/
  Graphics/`) usa **10+ PNGs separados por bone** (`Body.png`, `Left Arm.png`, `Head.png`,
  `Sword.png`, `Face 01/02/03.png`, etc.) — cada um sua própria `Texture2D`. O batching de
  sprites do Unity só agrupa `SpriteRenderer`s que compartilham a MESMA textura/material; sem
  nenhum Sprite Atlas, cada bone visível de cada personagem é um draw call separado (~20+ só
  pros 2 rigs principais, antes de armas/HUD/pets) — bate exatamente com "Rendering dominante,
  mesmo sem pets". Fix: novo `Assets/Editor/CombatSpriteAtlasGenerator.cs`
  (**Tools → AutoArms → Generate Combat Sprite Atlas**) cria `Assets/Data/SpriteAtlas/
  CombatAtlas.spriteatlas` (Type Master, sem rotation/tight packing — sprites com transparência
  e flipbooks animados não combinam com nenhum dos dois) empacotando as 3 pastas `Graphics/` dos
  personagens + as 3 pastas `PNG Sequences/` dos pets (mesmo problema de textura-por-frame
  quando pets entrarem em jogo) + `Assets/Data/UI/Weapons/`. **Nenhuma mudança de código
  necessária além do gerador** — o Unity usa o atlas automaticamente pra qualquer Sprite já
  referenciado nos assets/prefabs existentes assim que ele for empacotado. **Pendente do
  usuário**: rodar o comando acima, selecionar o atlas gerado e clicar **Pack Preview** no
  Inspector (ou simplesmente entrar em Play/Build, que empacota automaticamente).
- **Causa dos spikes (Scripts)**: confirmado real — `CombatPlayer.SpawnGhostTrail` (efeito
  "Matrix" da Fierce Brute) cria um `GameObject`+`SpriteRenderer` novo por `Instantiate` pra
  CADA SpriteRenderer do rig (~10+) a cada 0.06s, por até 1.5s — até ~250 instanciações/luta só
  nesse efeito, cada uma sendo destruída ~0.5s depois (`FadeOutAndDestroyGhost`). Fix: pool de
  objetos (`CombatPlayer._ghostPool`/`RentGhost`) — cresce sob demanda, nunca destrói, recicla
  via `SetActive(true/false)`; `FadeOutAndDestroyGhost` agora desativa em vez de `Destroy`.
- **Transparency Sort Mode (sugestão do usuário, não aplicada)**: investigado e descartado —
  `ProjectSettings/GraphicsSettings.asset` já está em `Default` (`m_TransparencySortMode: 0`),
  que pra uma câmera ortográfica já ordena por distância ao longo do eixo de visão (equivalente
  a Z aqui). O CUSTO de ordenar N renderers transparentes é o mesmo `O(n log n)` independente do
  eixo escolhido — trocar pra "Custom Axis (0,1,0)" não reduz nenhum custo de CPU/GPU mensurável,
  e arriscaria reordenar sprites dentro da MESMA Sorting Layer por posição Y em vez de respeitar
  só `sortingOrder`, que é como o projeto controla profundidade hoje (ver Sorting Layers acima) —
  não aplicado.
- **Canvas world-space por pet / Animator idle de pet (sugestões do usuário)**: não aplicável ao
  teste reportado — os 2 profiles envolvidos não tinham nenhum pet no momento do teste. Mantidos
  como possíveis otimizações futuras SE uma luta com vários pets simultâneos vier a reproduzir o
  mesmo sintoma, mas não implementados agora (sem evidência de que sejam o problema real).

**Bug corrigido no próprio `CombatSpriteAtlasGenerator.cs` (personagens ficaram foscos depois da
1ª geração do atlas, reportado pelo usuário)**: a 1ª versão nunca chamava
`atlas.SetPlatformSettings(...)` — sem isso, o atlas usa o `maxTextureSize` default da
plataforma (bem menor que a soma dos 3 rigs + armas + (na 1ª versão) todas as PNG Sequences dos
3 pets), forçando o packer a fazer downscale de tudo pra caber numa única página — causa real do
desfoque, não o `FilterMode` (que já estava em `Bilinear`, igual ao `filterMode: 1` dos `.meta`
originais de cada PNG — confirmado lendo `Body.png.meta`; **não** trocado pra `Point` como o
usuário sugeriu, isso deixaria os personagens pixelados, um estilo visual diferente do resto do
jogo). Fix: `SetPlatformSettings` explícito (`maxTextureSize = 4096`, `format = RGBA32`, sem
compressão) + o gerador agora deleta e recria o atlas do zero a cada execução (`AssetDatabase.
DeleteAsset` no início de `Generate()`), garantindo que nenhuma configuração de uma geração
anterior sobreviva. **Pets removidos do `PackFolders`** (saem do atlas, ficam pra um atlas
separado só quando forem testados de verdade em combate) — as PNG Sequences somam muitos frames
por estado × 3 pets, inflando a área total sem nenhum benefício mensurável no teste atual (os 2
profiles envolvidos não tinham pets). **Pendente do usuário**: rodar **Tools → AutoArms →
Generate Combat Sprite Atlas** de novo (recria do zero automaticamente, sem precisar apagar o
asset manualmente) e confirmar visualmente que os personagens voltaram a ficar nítidos.

**Hipótese do `EntityRenderer` (Spriter2UnityDX) como causa de custo por frame — investigada e
descartada por leitura direta do código-fonte do plugin**: `Assets/Spriter2UnityDX/Runtime/
EntityRenderer.cs` não tem nenhum método `Update()` — só `Awake`/`OnEnable`/`OnDisable`, todos
disparados só na criação/ativação do GameObject, nunca por frame. O único componente do plugin
com `Update()` é `SortingOrderUpdater.cs` (reordena `sortingOrder` com base na posição Z do
bone) — mas esse só é adicionado quando `EntityRenderer.ApplySpriterZOrder = true`, e
confirmado via grep que **nenhum** prefab de personagem tem isso ativado
(`applySpriterZOrder: 0` nos 3 `.prefab` do Medieval Warrior, e zero ocorrências de
`SortingOrderUpdater` em `Assets/Personagens` no total) — esse componente nunca chega a existir
em cena. Spriter2UnityDX não introduz nenhum custo por frame neste projeto; não é a causa do
Rendering dominante no Profiler.

**Pendente de confirmação do usuário**: número de **Batches** no Stats overlay antes/depois do
atlas (corrigido) — se não cair de forma perceptível mesmo com o atlas empacotando
corretamente, a causa mais provável não é falta de atlas, e sim **quebra de continuidade do
batching por intercalação de Sorting Layer**: o Unity só funde em 1 draw call desenhos
CONSECUTIVOS que compartilham material+textura — se uma arma (`Weapons`/`Weapons2`) ou um
ghost trail (`Characters2`) é desenhado ENTRE dois bones do mesmo personagem (`Characters`) na
ordem de profundidade, a sequência se quebra mesmo com todos os bones já num atlas único. Isso
seria uma limitação estrutural do sistema de Sorting Layers do próprio projeto (ver Sorting
Layers acima), não um problema do atlas ou do Spriter2UnityDX — precisa do número real de
Batches pra confirmar antes de qualquer mudança nesse sentido.

**Confirmado pelo usuário**: Batches não mudou entre Menu (14) e Combate (14) — atlas funcionando
corretamente (Saved by batching subiu de 10 pra 18-20 durante o combate). O CPU ms (33-36ms,
~27-30fps) era na maior parte **VSync** — `ProjectSettings/QualitySettings.asset` tinha
`m_CurrentQuality: 5` (Ultra) com `vSyncCount: 1` ("Every V Blank"); com VSync desligado nessa
Quality, FPS subiu pra ~36 (27.8ms). Não é mais investigação, é configuração de projeto
confirmada pelo usuário diretamente no Editor.

**Freezes pontuais remanescentes mesmo com VSync desligado (GC.Alloc)**: `Assets/Scripts/UI/
DamagePopup.cs` — cada popup (`Spawn`/`SpawnDodge`/`SpawnHeal`/etc., usado em TODO hit/dodge/
block/miss/combo/cura) fazia `new GameObject` + `AddComponent<DamagePopup>` +
`AddComponent<TextMeshPro>`, destruído 1s depois — `TextMeshPro` aloca buffers de mesh/material
internamente em Add/Destroy, caro o bastante pra gerar um spike perceptível quando vários
popups nascem em sequência rápida (combo longo, Flash Flood com 3 arremessos, burst de 10 curas
do Fast Metabolism). Reescrito pra pool estático (`DamagePopup._pool`/`Rent`, mesmo padrão de
`CombatPlayer._ghostPool`/`RentGhost`) — `AddComponent<TextMeshPro>` só roda 1x por objeto
pooled, reciclado via `SetActive(true/false)` em vez de `Destroy`; os ~14 métodos `Init*`
antigos (1 por tipo de popup) foram consolidados num único `Begin(text, fontSize, color)` de
instância, chamado pelos `Spawn*` estáticos depois de `Rent()` — mesmo texto/fonte/cor de cada
variante preservados exatamente.

**Bug corrigido no próprio pool do `DamagePopup` (`MissingReferenceException` reportado pelo
usuário)**: `_pool` é `static`, então sobrevive a um `SceneManager.LoadScene` (Combate →
MainMenu → Combate de novo, dentro da mesma sessão de Play) — diferente dos GameObjects que ele
referencia, destruídos junto da cena anterior. A próxima luta tentava reusar uma entrada morta
do pool e `p.gameObject` lançava `MissingReferenceException`. Fix: `Rent()` agora começa com
`_pool.RemoveAll(p => p == null)` — usa o operator overload do `UnityEngine.Object` (`==`
retorna `true` pra uma referência já destruída, "fake null" do Unity), autolimpando o pool a
cada uso, sem precisar de nenhum hook de ciclo de vida externo (`ClearPool()`/
`CombatSceneLoader` nunca precisam saber que o pool existe). `CombatPlayer._ghostPool`
(ghost trail da Fierce Brute) **não** tem esse problema — é um campo de instância de
`CombatPlayer`, que por sua vez é criado via `AddComponent` do zero em todo
`CombatSceneLoader.Initialize()` (sem `DontDestroyOnLoad`/singleton), então é destruído junto da
cena toda vez, nunca carrega referência de uma luta anterior.

**Causa real do stutter recorrente, confirmado pelo palpite do usuário ("começou junto dos
pets")**: `CombatPlayer.ExecuteEvent`'s `case PetAttack` montava `System.Action onImpact = () =>
{...}` capturando 5 variáveis locais (`pet`, `evt`, `targetPetCombat`, `targetCharacter`,
`targetPos`) — toda closure que captura variáveis precisa de um objeto extra no heap pra guardar
essas variáveis, MAIS o delegate em si, **alocados de novo em TODO ataque de pet da luta
inteira** (não 1x por luta como os outros `Instantiate`/`new GameObject` já investigados — um
pet ataca repetidas vezes ao longo dos rounds, então essa alocação se repete continuamente
durante todo o combate, batendo com "o stutter continua acontecendo" em vez de um hitch único).
Fix: `CombatPlayer._onPetImpact` — campo `readonly System.Action` inicializado 1x com o method
group de `HandlePetImpact` (instância nunca recriada, sem closure) — e o estado que antes era
capturado (`pet`/`evt`/alvo/posição) agora mora em campos de instância (`_petImpactPet`/
`_petImpactEvt`/`_petImpactTargetPet`/`_petImpactTargetCharacter`/`_petImpactTargetPos`),
setados no `case PetAttack` imediatamente antes de `StartCoroutine(PlayAttackHit(...))` e
lidos por `HandlePetImpact()` (novo método privado, mesma lógica de antes, sem captura). Seguro
porque os eventos do `CombatPlayer` são processados estritamente em sequência (1 única coroutine
de replay, nunca 2 `PetAttack` concorrentes no mesmo `CombatPlayer`) — não há risco de um
`PetAttack` sobrescrever os campos de outro ainda em voo.

**Causa real do "engasgo" (diferente do fix acima — esse era válido, mas não era O stutter
reportado)**: o usuário esclareceu que o FPS no Stats overlay continua normal durante o
travamento, e que ele acontece especificamente **no momento de entrar em `04_CombatScenePVP`**
(queda do céu dos personagens) — não espalhado pelo combate. FPS médio normal + um "engasgo"
visual pontual = jitter de frame time concentrado, não custo sustentado de GC — outra categoria
de problema, sem relação com o fix do `PetAttack` acima (esse continua válido, só não era a
causa DESTE sintoma específico).

`CombatSceneLoader.SpawnPets` era um método **síncrono** (`void`, não coroutine) chamado 2x em
sequência direto de dentro de `Initialize()`, no MESMO frame em que `StartCoroutine(EntryFall(...))`
dos 2 personagens principais acabava de ser disparado — ou seja, todo o custo de `Instantiate`
+ `AddComponent<PetCombatController>` + `HealthBarPet.Create` (que monta um Canvas+Image+
RectTransform inteiro) de **cada pet de cada lado** ficava concentrado exatamente no frame em
que a queda do céu (a única coisa em movimento visível bem no instante de entrar na cena) está
prestes a começar a ser desenhada. Antes dos pets existirem, esse frame só continha o
`StartCoroutine` dos 2 `EntryFall` (barato); pets sozinhos não mudaram a ARQUITETURA do bug
(`SpawnPets` sempre foi síncrono), só inflaram o que já cabia nesse frame até passar do limiar
de ser visualmente perceptível — explica por que o usuário associa o início do problema aos
pets sem que o código deles tenha, sozinho, criado o padrão.

Fix: `SpawnPets` virou `IEnumerator`, chamado via `StartCoroutine` (fire-and-forget, mesmo
padrão de `EntryFall`) em vez de uma chamada direta — `yield return null` depois de cada pet
instanciado, espalhando o custo por frame em vez de empacar todos os pets de um lado de uma vez
só. Mais 1 `yield return null` inserido logo ANTES dos 2 `StartCoroutine(SpawnPets(...))`,
depois do `StartCoroutine(EntryFall(...))` dos personagens principais — garante que a queda dos
2 personagens principais comece sozinha no próprio frame, sem nenhum `Instantiate`/`AddComponent`
de pet competindo por ciclos de CPU naquele exato instante. `petsDone`/`WaitUntil` (mecanismo já
existente que tolera conclusão assíncrona/fora de ordem) não precisou de nenhuma mudança.
Removido também o `Debug.Log($"[PetHUD] Criando barra para {petType}")` temporário (instrumentação
da investigação 1e, já confirmada há tempo — 1 Canvas por pet, sem duplicação).

**Log pré-combate** (`CombatLogFormatter.cs`) — `Format()` ganhou 2 parâmetros opcionais
(`p1Pets`/`p2Pets`, `List<PetType>`) pra resolver o nome de exibição (Rato/Macaco/Javali) por
índice — sem eles (chamadas antigas) cai no fallback genérico `"Pet"`. `CombatSceneLoader`
passa `profile.pets`/`player2Profile.pets` na única chamada existente.

**Escalonamento por nível do dono** (`PetState.ApplyLevelScaling`) — a cada 5 níveis do
PERSONAGEM DONO (level 5, 10, 15...), os stats do pet sobem permanentemente pro resto daquela
luta: `levelTiers = ownerLevel / 5` (divisão inteira). Rato: `+5 HP`/`+1 Speed` por tier.
Macaco: `+10 HP`/`+2 AGI`/`+1 Speed` por tier. Javali: `+20 HP`/`+1 AGI`/`+1 Speed`/`+3 STR` por
tier. Chamado 1x na construção do `PetState` (`CombatSimulator.BuildState`, logo depois de
`PetState.Create`) — entra no `maxHp`/`speed`/`agility`/`str` definitivos da luta, nunca
recalculado durante o combate em si. **Não afeta** o custo de HP do dono (`PetState.HpCost` —
Rato -12/Macaco -36/Javali -48): esse é fixo, perdido de uma vez só na escolha do pet no
level-up, sem relação com o escalonamento. Espelhado em `CombatSceneLoader.SpawnPets` (a
`PetState.Create` "preview" usada só pra inicializar o valor de `PetCombatController.maxHp`/
a `HealthBarPet`, ver acima) — sem isso a barra de vida mostraria um máximo desatualizado em
relação ao HP real usado pelo simulador. Sem exibição na UI ainda — intenção documentada (não
implementada) de uma futura `PlayerProfile.GetEffectivePetStats()`, mesmo padrão de
`GetEffectiveStats()` (ver **Stats System**), pra quando o `CharacterPanel` ganhar uma
aba/seção própria de Pets.

