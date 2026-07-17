# AutoArms — Changelog

### Progresso
- Total: 143 tarefas | Concluídas: 43 (recontado em 2026-07-15 — ver nota em CLAUDE.md)

- 2026-07-17: **Investigado, sem bug de código encontrado** — reportado que o Macaco causava
  sempre 3 de dano em combate "independente do tier/STR configurado". Verificação:
  `CombatSimulator.SimulatePetHit` (`int damage = pet.damage`) lê o campo `damage` direto do
  `PetData` equipado, e os assets em disco confirmam o tier-scaling correto e independente
  (`pet_monkey_t1.asset`: `damage: 3`/`str: 24`; `pet_monkey_t3.asset`: `damage: 9`/`str: 34`) —
  `PetState.Create`/`CombatSimulator.BuildState` constroem cada `PetState` direto de
  `profile.pets`, então um Macaco T3 de verdade já deveria causar 9, não 3. O usuário confirmou
  que o teste original foi feito num save ANTIGO, de antes do fix do bug de duplicação de pet no
  level-up (ver entrada "Bug corrigido — selecionar o mesmo tipo de pet 2x..." acima, mesma
  sessão) — o profile salvo provavelmente tinha o Macaco preso em T1 por causa daquele bug já
  corrigido, não um problema novo no cálculo de dano. **Sem alteração de código** — reteste
  pendente pelo usuário com um profile fresco (Macaco genuinamente evoluído a T2/T3 via level-up
  pós-fix).
  **Nota de design em aberto (NÃO implementada, aguardando decisão do usuário)**: diferente do
  personagem principal (`(weaponBaseDamage + str) × ...`, STR soma direto no dano da arma), o
  dano do pet hoje é só o valor fixo da coluna "Dano" da tabela aprovada (`PETS.md`) — STR do
  pet é uma coluna INDEPENDENTE, usada só pela fórmula do Piledriver (agarra o pet e usa
  `pet.str` como dano daquele golpe específico) e pelo escalonamento por nível do dono (Javali
  `+3 STR/tier de nível`), nunca somada ao próprio ataque do pet. Somar STR ao dano do pet
  (proposta levantada nesta investigação) infla os valores MUITO acima da tabela atual (ex:
  Javali T1 iria de 5 pra 51 de dano, T3 de 15 pra 71 — mais que qualquer arma T3 de personagem
  no jogo hoje) — mudança de balanceamento significativa, não uma correção de bug. Usuário pediu
  pra deixar anotado pra decidir depois, sem implementar agora.
- 2026-07-17: **Bug corrigido** — pets não animavam NENHUM estado (nem Idle "de verdade" —
  ficavam travados sempre na mesma pose, deslizando estáticos até o oponente e "atacando"
  estáticos), reportado pelo usuário no Macaco, mas o mesmo bug afeta os 3 pets (Rato/Macaco/
  Javali) igualmente — não é específico de nenhum deles, ver **Causa raiz compartilhada** abaixo.
  Investigação anterior (comparação estática de Animator Controllers, prefabs, clipes, GUIDs,
  código de combate) não achava NENHUMA divergência entre os 3 pets porque o bug não é de dado
  nenhum — é de TIMING de inicialização em runtime, invisível a qualquer inspeção de arquivo.
  Causa real: `PetAnimationController.Awake()` enumerava `Animator.parameters` pra cachear quais
  dos 6 parâmetros esperados existiam (`_hasIdle`/`_hasRunning`/etc.), e todo método de animação
  (`SetIdle`/`PlayRun`/`PlaySlash`/`PlayHurt`/`PlayJump`/`PlayDying`) só chamava o Animator se a
  flag correspondente tivesse dado `true` — pensado só pra evitar warnings caso `Tools/AutoArms/
  Setup Pet Animators` não tivesse rodado ainda. Esse `Awake()` roda SINCRONAMENTE dentro de
  `gameObject.AddComponent<PetAnimationController>()` (chamado por `PetCombatController.Awake()`,
  também síncrono via `petObj.AddComponent<PetCombatController>()` em
  `CombatSceneLoader.SpawnPets`) — tudo no MESMO frame do `Instantiate(prefab)` que criou o pet.
  `Animator.parameters`, lido tão cedo (antes do Animator ter feito seu próprio bind/init
  interno, que a Unity só garante a partir do primeiro `Update`/habilitação), pode devolver uma
  lista VAZIA mesmo com o Controller corretamente configurado — as 6 flags ficavam `false`, e
  toda chamada de animação virava no-op silencioso pelo resto da luta inteira, enquanto
  movimento (`MovementController`, componente totalmente separado) e dano
  (`CombatSimulator`/`HealthSystem`) continuavam funcionando normalmente — exatamente o sintoma
  reportado. Fix: removida a checagem prévia por completo — `PetAnimationController` agora só
  guarda `_animator != null` e chama `SetBool`/`SetTrigger` direto pelo nome, mesmo padrão
  simples do `AnimationController.cs` dos personagens principais (que nunca teve esse gate e
  nunca demonstrou o bug).
  **Causa raiz compartilhada (pedido explícito do usuário — documentar pra referência futura)**:
  este bug não tem relação com o sistema de tiers de pets (`PetData`/`PetTierGenerator`/
  `PetDatabase`, implementado na sessão anterior) nem com nenhum dado específico de cada pet —
  é puramente sobre a ORDEM/TIMING em que `AddComponent` é encadeado em `CombatSceneLoader.
  SpawnPets` (`Instantiate` → `AddComponent<PetCombatController>` → `AddComponent<
  PetAnimationController>`, tudo síncrono no mesmo frame) versus quando o `Animator` da Unity
  de fato fica pronto pra responder `.parameters` corretamente. Qualquer componente FUTURO que
  seja anexado via `AddComponent` encadeado a um objeto recém-`Instantiate`d e que precise ler
  `Animator.parameters`/`.parameterCount` (não `SetBool`/`SetTrigger` por nome, que são seguros
  a qualquer momento) está sujeito ao mesmo bug — evitar essa leitura no primeiro frame, ou
  adiar pra depois de um `yield return null`/`Start()` se for realmente necessária.
- 2026-07-17: **Bug corrigido** — ícone de HP ainda não alinhava de verdade com o ícone de STR/
  AGI/SPD nas 3 telas (Main Menu/Seleção de Personagem via `CharacterPanel`, Seleção de Oponente
  via `SelectOpponentController`), apesar de uma tentativa anterior (2026-07-16) já ter igualado
  a borda ESQUERDA dos dois ícones. Causa raiz: essa tentativa só igualava o offset esquerdo, mas
  o retângulo do ícone de HP usava uma largura **fixa** em pixels (34px em `CharacterPanel`, 22px
  em `SelectOpponentController`) enquanto o retângulo do ícone de STR (`AttributePipBar.Build`)
  usa uma largura **proporcional** (20% da linha) — como `Image.preserveAspect` + `localScale`
  centralizam e escalam o sprite em torno do CENTRO do próprio retângulo (não da borda), bordas
  esquerdas iguais com larguras diferentes produzem CENTROS renderizados diferentes (~16px de
  diferença em `CharacterPanel`), visível a olho mesmo com o "alinhamento" anterior no código.
  Fix: recalculada a largura do retângulo do ícone de HP com a MESMA fórmula proporcional do
  ícone de STR em cada tela (`CharacterPanel.BuildInfoBlock`: `anchorMax.x` de 0.05 fixo pra
  0.05+0.20×0.90=0.23, espelhando a fração de `lblGo` dentro do espaço do `container`;
  `SelectOpponentController`: `sizeDelta.x = RightPanelWidth×0.20 − 14`, mesma fórmula de
  `MakeAttributeRow`/`AttributePipBar.Build`) — mesma largura exata do ícone de STR em ambas as
  telas, logo mesmo centro renderizado, não importa a largura real do container em pixels.
- 2026-07-17: **Feature** — nova aba "PETS" no `CharacterPanel` (painel expandido de
  01_MainMenu/02_SelectCharacter), abaixo de HABILIDADES/ARMAS, mesmo padrão visual e de
  interação (grade de células com borda colorida por tier, click abre popup de detalhe — não
  hover). `RefreshPets(p)` mirrora `RefreshArmas` 1:1: itera `PlayerProfile.pets` (agora
  `List<PetData>`, um pet por instância possuída — sem "singular equipado", mesma lógica de
  Skills/Armas que também listam tudo que o personagem tem, não um "ativo" só), célula por pet
  com `data.icon`/`data.tier`, clique chama `ShowPetDetail(data, data.icon)` — método já existia
  desde a implementação do tier system de pets (2026-07-16), só não tinha nenhum ponto de entrada
  na UI do menu principal ainda (só era usado em `SelectOpponentController`/05_SelectOpponent).
  Mensagem "Nenhum pet ainda" quando a lista está vazia, mesmo padrão de "Sem armas equipadas".
- 2026-07-17: **Bug corrigido** — ícones dos pets não apareciam na grade de level-up
  (`CombatResultPanel.MakeLevelUpCard`, tela de teste "escolha 1 bônus"), mostrando só uma cor
  sólida marrom placeholder. Os ícones em si já tinham sido importados e vinculados a
  `PetData.icon` numa sessão anterior (2026-07-16, `Rato_Icon.png`/`Macaco_Icon.png`/
  `Javali_Icon.png` em `Assets/Data/UI/Pets/<Nome>/`, conferido ainda intacto) — a causa raiz era
  só que `MakeLevelUpCard` nunca lia esse campo: o switch que resolve o sprite do card
  (`iconSprite = opt.kind switch {...}`) tratava `Skill`/`Weapon` mas caía em `_ => null` pra
  `Kind.Pet`, então o branch de ícone de verdade nunca era alcançado pra pet, só o fallback de
  cor sólida. Fix de 1 linha: `LevelUpOption.Kind.Pet => opt.petData?.icon` no switch.
- 2026-07-17: **Bug corrigido** — escolher o mesmo tipo de pet 2x no level-up duplicava (2 Macacos
  separados) em vez de evoluir o tier (T1→T2→T3). Causa raiz: essa era literalmente a sub-fase B
  já documentada como pendente desde 2026-07-16 (`LevelUpEngine.ApplyOption`'s Pet case fazia só
  `profile.pets.Add(...)` sem checar nada, e o pool de opções (`ShowAllOptionsChoice`/
  `DrawOption`) sempre oferecia os 3 T1 sem filtro nenhum). Fix: novo `LevelUpEngine.
  BuildAvailablePets(profile, petPoolT1)` — se o jogador não tem nenhum tier daquele TIPO de pet,
  oferece o T1; se já tem, oferece só `owned.nextTier` (null quando já é T3 = não oferece mais
  nada desse pet); `ApplyOption` agora remove o tier anterior antes de adicionar o novo (upgrade
  in-place, mesmo padrão de skill/arma). `BotProfileGenerator` tinha o mesmo bug (o pool de pets
  disponíveis era montado 1x fora do loop de level-up dos bots, nunca atualizado) — corrigido
  junto, recalculado a cada nível igual skill/arma.
- 2026-07-17: **Bug corrigido** — HP com 3 dígitos (>= 100) quebrava em 2 linhas nos badges de
  `CharacterPanel` (01_MainMenu/02_SelectCharacter) e `SelectOpponentController`
  (05_SelectOpponent). Causa raiz: `AttributePipBar.BuildIconWithValue` (o coração+número
  introduzido em 2026-07-16) nunca setava `enableWordWrapping`, então herdava o `true` padrão do
  TMP — "100" não cabia numa linha na caixa pequena do badge (34px/22px) no fontSize configurado
  e quebrava em "10"/"0". Fix: `enableWordWrapping = false` (nunca quebra linha) +
  `enableAutoSizing` com piso em 60% do fontSize pedido (3 dígitos encolhem pra caber em vez de
  vazar da caixa; 1-2 dígitos continuam no tamanho cheio de sempre). Mesmo fix aplicado por
  segurança na label de HP da barra de vida em combate (`CombatHUD.CreateBar`) — essa caixa é bem
  mais larga e dificilmente quebrava na prática, mas também não desabilitava wrapping em lugar
  nenhum.
- 2026-07-16: Ícones novos do usuário — HP/STR/Speed (`Assets/Resources/UI/Attributes/{HP,Str,
  Speed}.png`) substituídos por sobrescrita direta do arquivo (mesmo GUID, sem precisar rewireear
  nada); AGI segue com o ícone antigo (usuário não forneceu substituto ainda). 3 ícones "de
  skill" novos pros pets (Rato/Macaco/Javali, `Assets/Data/UI/Pets/<Nome>/<Nome>_Icon.png`) —
  aproveitado pra corrigir uma pendência arquitetural: `PetData.icon` (existia desde a sub-fase A,
  mas sempre null) agora é preenchido de verdade pelos 9 assets (3 pets × 3 tiers, mesmo ícone
  nos 3 tiers do mesmo bicho) e pelo próprio `PetTierGenerator` (pra sobreviver a uma
  regeneração futura). `SelectOpponentController` trocou os 3 campos fixos por tipo
  (`mousePetIcon`/`monkeyPetIcon`/`boarPetIcon`, wireados manualmente no Inspector) por
  `petData.icon` direto — mesmo padrão que `SkillData.icon`/`WeaponData.icon` já usavam,
  eliminando a necessidade de rewireear ícone de pet em cada tela nova que precisar dele.
- 2026-07-16: Sistema de tiers T1/T2/T3 pra pets (sub-fase A de 6, plano revisado pelo usuário
  antes de codar) — `PetData : ScriptableObject` novo (`Assets/Scripts/Data/PetData.cs`, campos
  nomeados em vez de `bonusValue1-7` de `SkillData` — pet precisa de mais valores distintos do
  que os 7 slots comportam) + `PetTierGenerator.cs` (`Tools > AutoArms > Generate Pet Tiers`) com
  a tabela real extraída manualmente da referência do My Brute e adaptada pro jogo (aprovada
  pelo usuário). `PlayerProfile.pets` virou `List<PetData>` (era `List<PetType>`);
  `PetState.Create(PetData)` (era `Create(PetType)` com switch hardcoded); `LevelUpOption.petType`
  virou `petData`; `LevelUpEngine.PetPool` (array fixo de enum) removido, substituído por um pool
  de assets passado por parâmetro (`AttackSequencer.petPool`/`SelectOpponentController.petPool`,
  novos campos, mesmo padrão de `skillDatabase`/`allWeapons` — precisam ser wireados no Inspector
  com os 3 T1 depois de rodar o gerador). Counter/Reversal do Macaco (mecânica antiga, fora da
  tabela nova aprovada) removidos por completo, junto do método `SimulatePetRetaliation` (ficou
  sem chamador). Save/load (local JSON + Firestore) também precisou de ajuste, achado ao seguir o
  padrão já existente de `WeaponDatabase`/`SkillDatabase`: `PetDatabase.cs` novo (`Assets/
  Resources/PetDatabase.asset`, populado automaticamente pelo próprio `PetTierGenerator`) +
  `PetTierRef` novo em `CharacterDTO.cs` (mesma ideia de `WeaponTierRef`/`SkillTierRef` — tipo+tier
  em vez de só `PetType.ToString()`) — sem isso, um pet salvo carregaria de volta sempre como T1
  (tier perdido no round-trip). 2 bugs corrigidos de brinde: Disarm do Javali lia `Roll(0.15f)` fixo em vez de
  `pet.disarmRate` (agora tier-escalável); popup de detalhe do pet (`CharacterPanel.ShowPetDetail`)
  usava tier 3 hardcoded pra borda do ícone (não existia tier real ainda) — agora usa o tier de
  verdade. Dano de pet virou valor único por tier (era range aleatório, seguindo a mesma
  simplificação já aplicada a `WeaponData` antes). **Ainda faltam** (sub-fases D/E/F, mecânica de
  combate nova, ver plano revisado em CLAUDE.md/Fase 3): evoluir em vez de duplicar pet no
  level-up, Initiative, Accuracy do Javali, debuffs fixos de Combo/Block do Javali no oponente.
- 2026-07-16: Manutenção de roadmap pedida pelo usuário — "Sistema de raridade de armas" marcado
  `[x]` (coberto pelos Tiers T1/T2/T3 de arma, confirmado pelo usuário: "raridade de arma ja foi
  feito, t1 t2 t3"); "Mapa de skills"/"Mapa de armas (árvore)" marcados `[x]` (cobertos pelo grid
  de `03_Arsenal`, confirmado pelo usuário: "mapa de skill e arma é o botao arsenal"); "Criar cena
  03_SelectWeapons" cancelado e removido do roadmap (`03_Arsenal` já cobre a necessidade — campo
  `MainMenuController.selectWeapons` fica como resíduo morto, não removido do código). 2 itens
  novos adicionados em Fase 3: **Pets T2/T3** (não pode repetir o mesmo `PetType` no profile — ao
  escolher de novo, evolui o pet já possuído em vez de duplicar; ainda sem stats por tier nem
  campo de tier em `PlayerProfile.pets` definidos) e **achar ícones de pet de verdade** (hoje usa
  o frame `Idle_000` como provisório). Progresso recontado do zero: 146 tarefas, 47 concluídas.
- 2026-07-16: Ajuste fino dos ícones de atributo (pedido do usuário, valores exatos) — ícones de
  STR/AGI/SPD (`AttributePipBar.Build`) escalados 3x (`RectTransform.localScale`, em cima do
  sprite já centralizado por `preserveAspect`, sem mexer em sizeDelta/anchors) e o coração de HP
  (`AttributePipBar.BuildIconWithValue`) escalado 2.7x. HP realinhado em X com o ícone de STR
  (mesmo inset de 14px que o ícone usa dentro da linha de STR) e movido pra cima da pilha de
  STR/AGI/SPD (antes ficava ao lado de "Level X") — em `CharacterPanel.cs` (01_MainMenu/
  02_SelectCharacter) e `SelectOpponentController.cs` (05_SelectOpponent).
- 2026-07-16: 4 ícones novos do usuário (`Str.png`/`Agi.png`/`Speed.png`/`HP.png`, movidos de
  `C:\Users\user\Desktop\Prototipo\Icones\` pra `Assets/Resources/UI/Attributes/`, carregados via
  `Resources.Load` — `AttributePipBar` não é `MonoBehaviour`/não tem GameObject de cena pra
  wireear um Sprite no Inspector) substituem o texto "STR"/"AGI"/"SPD" (`AttributePipBar.Build`,
  `IconForLabel`) e "N HP" (novo `AttributePipBar.BuildIconWithValue` — ícone de coração com o
  número em branco centralizado por cima, texto "HP" removido) nos 3 lugares que mostravam esses
  textos: `CharacterPanel` (reaproveitado por `01_MainMenu` e `02_SelectCharacter`, mesmo
  componente nos dois) e `SelectOpponentController` (`05_SelectOpponent`, "Level X" e o HP
  viraram elementos irmãos flush-à-esquerda em vez de um texto combinado centralizado).
- 2026-07-16: Ajuste de layout no card de `05_SelectOpponent`, pedido do usuário ("disposição
  confusa"): (1) linha "Level X · HP Y" mudou de centralizada (flutuava sozinha na largura toda
  do `RightPanel`) pra alinhada à esquerda — agora começa colada na borda direita do retrato,
  lendo como parte do personagem em vez de solta no topo; (2) STR/AGI/SPD voltaram a ser 3 linhas
  empilhadas (`MakeAttributeRow`, chamado 3x) com o mesmo espaçamento vertical entre elas, no
  lugar da versão em 3 colunas lado a lado (`MakeAttributeRowsCompact`, removida) da rodada
  anterior — essa versão espremia cada `AttributePipBar` num container estreito demais, fazendo
  label/badge/pips renderizarem em posições relativas ligeiramente diferentes entre as 3 stats;
  empilhadas, as 3 chamadas usam o mesmo container/código, então ficam idênticas em X por
  construção. Nenhuma outra mudança (cores/fontes/ícones/popup de detalhe intocados).
- 2026-07-16: Tooltip de hover (rodada anterior, só nome) trocado por popup de detalhe de verdade
  nos ícones de skill/arma/pet de `05_SelectOpponent` — usuário apontou que era um retrocesso
  comparado ao resto do jogo. Investigação confirmou o padrão já aprovado: `CharacterPanel.
  ShowSkillDetail`/`ShowWeaponDetail` (`public`, popup no CLIQUE — nome, borda por tier,
  descrição/efeito ou stats completos), já reaproveitado por `03_Arsenal` via
  `ArsenalController.EnsureDetailPanel()` (instancia um `CharacterPanel` "de cabeça", só o popup,
  sem o HUD). `SelectOpponentController` ganhou o mesmo `EnsureDetailPanel()`; cada ícone virou um
  `Button` que abre o popup (não borbulha pro `PressableCard` do card). Pets não tinham
  equivalente (sem `PetData`/asset próprio) — `CharacterPanel` ganhou `ShowPetDetail(PetType,
  Sprite)`, novo método público que reaproveita a MESMA infraestrutura de popup (overlay/painel/
  ícone com borda/medição de altura dinâmica), só trocando description/effectText por stats
  formatados de `PetState.Create`/`DamageRange`. `IconTooltip.cs` (tooltip de hover) removido —
  sem uso depois da troca.
- 2026-07-16: Ajustes pedidos pelo usuário no card de `05_SelectOpponent`: (1) bots agora vêm de
  `unlockedCharacters` filtrado por `rarity == Normal` (`SelectOpponentController.
  GenerateBotOpponents`), substituindo a curadoria fixa por nome (`botTemplates`/
  `BotTemplateSetup.cs`, obsoleta, deixada no projeto sem consumidor); (2) altura do card reduzida
  à metade (820×320, era 820×640) — coube removendo o botão "Escolher" e o histórico de batalhas,
  virando STR/AGI/SPD numa linha horizontal de 3 colunas (`MakeAttributeRowsCompact`, era 3 linhas
  empilhadas) e skill+arma+pet numa única linha combinada (`MakeItemIconsRow`, era 2 linhas
  separadas); (3) botão "Escolher" removido — o card inteiro ficou clicável (`PressableCard.cs`,
  novo: escurece no toque, escolhe no `OnPointerClick` — não `OnPointerUp`, que dispararia mesmo
  depois de um arraste de scroll); (4) pets agora aparecem no card — não era falta de ícone como o
  usuário suspeitava, é que nunca existiu nenhuma linha pra eles; usa o frame `Idle_000` de cada
  pet (`Assets/Data/UI/Pets/<Nome>/`), wireado via 3 campos novos (`mousePetIcon`/`monkeyPetIcon`/
  `boarPetIcon`) direto no `.unity` (guids); (5) texto "X batalhas · Y vitórias" removido.
- 2026-07-16: `05_SelectOpponent` — sessão anterior fechou sem salvar (Unity não autosalva) e
  perdeu o wiring manual de `theme`/`skillDatabase`/`allWeapons` no `SelectOpponentController`
  (causava `NullReferenceException` em `AttributePipBar.Build`) e todas as raridades de
  `PlayerProfile` já setadas — re-wireado direto no `.unity`/`.asset` (guids lidos dos `.meta`,
  sem precisar abrir o Editor pela VPN). As 72 raridades (`CharacterRarity`) foram preenchidas
  pra valor final (lista dada pelo usuário); `isUnlockedForSelection`/`isPlayable` de todos os 72
  `PlayerProfile` setados pra `true` temporariamente pra visualização, depois revertido — só
  `Medieval Warrior` ficou `isPlayable: true`. Card de `05_SelectOpponent` redesenhado: grid
  2 colunas × 3 linhas (`GridLayoutGroup.Constraint.FixedColumnCount`, antes sem constraint,
  virava 1 linha × 6 colunas estreitas); card maior (820×640, era 260×620) com retrato grande
  (300px, era 120px) fixo na coluna esquerda e todo o resto (nome/level/HP/atributos/skills/
  armas/histórico/botão) numa coluna direita própria (`RightPanel`); ícones de skill/arma
  aumentados de 36 pra 56px; tooltip de hover novo (`IconTooltip.cs`, painel único compartilhado
  por card, mostra o nome ao passar o mouse no ícone de skill/arma).
- 2026-07-15: 12 bots de matchmaking (`CharacterDatabase.botTemplates` + `BotProfileGenerator`,
  stats/skill/arma escaláveis do level 1 ao pedido, mesmos pesos de level-up do jogador real via
  `LevelUpEngine`, extraído de `CombatResultPanel`) como fallback intermediário em
  `05_SelectOpponent`, entre a busca online (agora priorizada por `levelBucket`,
  `OpponentSearchService`) e o pool antigo `opponentCharacters`. Falta rodar `Tools > AutoArms >
  Setup Bot Templates` e wireear `SkillDatabase`/`allWeapons`/`UITheme` no `SelectOpponentController`
  da cena (passos manuais, ver plano da sessão).
- 2026-07-15: Ajustes pedidos pelo usuário depois do 1º teste dos bots — (1) `BotTemplateSetup`
  agora usa 9 personagens "normais"/humanos + 3 "incomuns"/fantásticos (não existe ainda nenhum
  `PlayerProfile.rarity` != Normal no projeto, então a curadoria é por nome, não pelo enum); (2)
  card de `05_SelectOpponent` ganhou uma linha de ícones de SKILL (antes inexistente — nenhuma
  skill aparecia pra nenhum oponente, bot ou real) via `LevelUpEngine.ResolveSkillIcon` (sobe
  `previousTier` até achar ícone, T2/T3 nunca têm um próprio); ícone de arma também passou a subir
  a mesma cadeia (`ResolveWeaponIcon`) em vez de só olhar o tier exato; (3) STR/AGI/SPD do card
  trocaram a fill-bar simples pelo `AttributePipBar` (badge + 10 pips coloridos por tier), mesmo
  componente do `CharacterPanel`/`01_MainMenu` — precisa de `UITheme` wireado no
  `SelectOpponentController` (novo campo, obrigatório: sem ele o grid inteiro quebra).
- 2026-07-15: Novo `LIMPEZA_BASE.md` — passo a passo pra zerar a base Firebase (Auth + Firestore) e
  o cache local (save.json, LevelDB do Firestore, `PlayerProfile.asset` contaminado por teste) —
  usuário zerou a base pela primeira vez seguindo este processo, antes de iniciar a próxima tarefa
  (12 personagens bot com stats escaláveis por level + matchmaking por level em
  `05_SelectOpponent`, planejado mas ainda não implementado).
- 2026-07-15: Revisão de código pedida pelo usuário pra atualizar itens de roadmap já
  implementados — `ROADMAP_FUTURO.md`: Fase 6 ("banco de dados"/"persistência online" → Firebase,
  concluído; "login múltiplos métodos" desmembrado em email/senha e Google concluídos, Apple/
  Facebook pendentes), Fase 8 ("autenticação segura do jogador" → concluído, com nota de que
  validação server-side de dado crítico continua pendente), Fase 10 ("tela de seleção de
  oponente"/"histórico de confronto" → já existiam desde 2026-07-07, nunca tinham sido marcados).
  `CLAUDE.md` (Fase 0-3) conferido também — as 4 pendências (tutorial, atributos aleatórios na
  criação, HUD de moeda/diamante/energia, raridade de armas) seguem genuinamente não
  implementadas, sem mudança. Contador de Progresso recontado do zero a pedido do usuário — método:
  toda linha `- [ ]`/`- [x]` em CLAUDE.md (Fase 0-3) + ROADMAP_FUTURO.md (Fase 4-13), sem contar
  SKILLS_SYSTEM.md/PETS.md/etc (listas de implementação por skill/pet, não roadmap de projeto).
  Resultado: CLAUDE.md 36/40 concluídas, ROADMAP_FUTURO.md 7/103 concluídas — total 43/143
  (número antigo, 127/68, estava dessincronizado — provável resíduo da refatoração dos docs em 8
  arquivos, ver `project_docs_structure` em memória).
- 2026-07-15: Fatia 6 (busca de adversário online) confirmada funcionando pelo usuário em teste
  real com múltiplas contas (`teste5@teste.com` incluída) — encerra o plano de contas/save na
  nuvem/matchmaching básico (Fatia -1 a 6); só falta a Fatia 7 (Sign in with Apple + build iOS),
  bloqueada por acesso a Mac.
- 2026-07-15: Corrigido bug latente em `OpponentSearchService.FetchOpponentsAsync` (Fatia 6) —
  buscava exatamente `count` (6) documentos crus do Firestore e só depois excluía o próprio
  jogador/resultados sem molde resolvido, então a lista final podia vir menor que 6 mesmo havendo
  mais oponentes válidos no `opponents_index` nunca chegados a buscar (não visível ainda com o
  pool pequeno de contas de teste, mas reproduziria assim que passasse de ~6 jogadores reais).
  Corrigido buscando `count + 5` documentos crus antes de filtrar, mantendo o corte final em
  `count` depois da filtragem.
- 2026-07-15: Diagnosticado (via Visualizador de Eventos + crash dump) o crash "fecha sozinho" do
  Development Build ao criar conta — não era bug de código: `FirestoreService.PersistenceEnabled`
  usa um cache local (LevelDB) com lock exclusivo por processo/máquina para o mesmo projeto
  Firebase; rodar o Editor em Play Mode e o build ao mesmo tempo (ou 2 builds) faz o segundo
  processo falhar ao abrir o lock, e o SDK C++ do Firestore trata isso como falha interna
  irrecuperável (`abort()`, sem exceção .NET capturável). Decisão do usuário: manter
  `PersistenceEnabled = true`, só evitar rodar 2 processos ao mesmo tempo — ver regra registrada em
  ARQUITETURA.md.
- 2026-07-15: 2ª rodada da correção de isolamento entre contas — o fix anterior (accountScope +
  CapturePristineIfNeeded/RestoreAllPristine) não foi suficiente: usuário reportou conta nova
  recebendo um personagem em Level 2 (nem o estado de fábrica, nem o último nível jogado). Causa:
  `CloudSyncService.SyncCharacterAsync` gravava (`LocalSaveService.Save`) DE FORMA INCONDICIONAL ao
  final — um modelo "opt-out" que assumia ser sempre seguro persistir o que quer que estivesse em
  memória, dependendo inteiramente do reset de logout nunca falhar. Bastava essa função rodar uma
  vez (todo login chama) com qualquer resíduo em memória pra criar um personagem "fantasma" na
  conta nova. Corrigido invertendo pra um modelo "opt-in": novo
  `PlayerProfileConverter.MarkOwnerScope`/`GetOwnerScope` rastreia explicitamente qual conta é
  "dona" do estado em memória de cada `PlayerProfile` tocado na sessão — `SyncCharacterAsync` só
  grava se puder afirmar que o estado pertence à conta atual (veio da nuvem dela agora, já era
  dela desde antes, está genuinamente intocado, ou é progresso offline pré-login reivindicável pela
  1ª conta real). `RestoreAllPristine` (logout) também limpa essa marca de posse.
- 2026-07-15: Corrigido bug real de isolamento entre contas, reportado pelo usuário (conta nova
  herdando personagens/progresso de uma conta antiga testada no mesmo executável). Duas causas
  distintas: (1) `LocalSaveService`/`save.json` não vinculava o save a nenhuma conta — chave do
  cache agora é `{accountScope}:{characterId}` (`accountScope` = uid do Firebase Auth, novo campo
  em `CharacterDTO`, ou `"offline"` sem sessão); entradas gravadas antes da correção (sem
  `accountScope`) são tratadas como órfãs e nunca aplicadas a nenhuma conta. (2) Estado em memória
  dos `PlayerProfile` (ScriptableObject, vive durante todo o processo) ficava "contaminado" entre
  contas dentro da mesma sessão do jogo — sem relação com o `save.json` — porque nada resetava o
  profile ao trocar de conta sem fechar o jogo (fluxo só existe desde o botão de logout, adicionado
  numa sessão anterior). `PlayerProfileConverter` ganhou `CapturePristineIfNeeded`/
  `RestoreAllPristine` (snapshot de fábrica de cada profile na 1ª vez que é tocado no processo);
  `MainMenuController.OnLogoutClicked` chama `RestoreAllPristine()` antes do `SignOut`, devolvendo
  todo profile já tocado ao estado de fábrica antes da próxima conta poder logar.
- 2026-07-15: Fatia 6 do plano de contas/save na nuvem — busca de adversário online, conectada em
  `05_SelectOpponent`. Correção de desenho feita antes de implementar: `opponents_index` (Fatia 5)
  só gravava os campos `eff*` (pensados pra exibição) — pra realmente LUTAR contra um adversário
  achado, o `CombatSimulator` precisa dos stats BASE + lista de skills/armas (mecânicas de combate
  que um número efetivo único não cobre), senão o bônus de skill seria contado 2x ao reconstruir o
  oponente. `FirestoreService.SaveOpponentIndexAsync` agora reaproveita `CharacterDTOMap.ToMap`
  (mesmos campos base de `characters/{id}`) e só acrescenta `ownerUid`/`levelBucket`/`eff*`/
  `randomSeed` por cima — regras de segurança do Firestore atualizadas com a mesma validação de
  faixa amarrada ao `level` que já existia pra `characters/{id}`. Novo
  `Data/PlayerProfileConverter.FromOpponentIndexMap` reconstrói um `PlayerProfile` runtime a
  partir de um documento de `opponents_index`, achando o "molde" visual certo (prefab/ícone) em
  `CharacterDatabase.unlockedCharacters` pelo nome. Novo `Backend/OpponentSearchService.cs` —
  busca ~6 adversários via o truque de `randomSeed` (Firestore não tem "N aleatórios" nativo).
  `SelectOpponentController.Start` virou assíncrono: tenta a busca online primeiro, cai pro pool
  local de sempre (`CharacterDatabase.opponentCharacters`) se vier vazio (offline/sem
  sessão/ninguém sincronizado ainda) — comportamento local 100% preservado nesse fallback.
  `AttackSequencer.OnCombatEnd` também grava o histórico de batalhas em
  `users/{uid}/matchHistory/{opponentId}` (espelho do PlayerPrefs, fire-and-forget) — a leitura do
  card continua só PlayerPrefs por enquanto (decisão de escopo, não implementado leitura
  Firestore-primeiro nesta fatia).

- 2026-07-15: Registrado em `ARQUITETURA.md` — requisito de design futuro (sistema de nickname
  ainda não existe): nome de exibição público de personagem = `"{nickname da conta} -
  {nome do personagem}"`, montado só na hora de EXIBIR (nunca gravado como string fixa no
  Firestore) — nickname mora em `users/{uid}` (por conta), `profileName` continua só o nome do
  personagem. Nenhum código alterado nesta entrada, só documentação.

- 2026-07-15: Fatia 5 do plano de contas/save na nuvem — escrita em `opponents_index` (coleção
  flat no topo do banco, superfície pública pra busca de adversário na Fatia 6, ainda não
  implementada). `FirestoreService.SaveOpponentIndexAsync` calcula os campos `eff*`
  (`effHp`/`effStr`/`effAgility`/`effSpeed`) via `PlayerProfile.GetEffectiveStats()` — regra
  "Stat base vs. stat efetivo" registrada em `ARQUITETURA.md` — chamado sempre junto de
  `SaveCharacterAsync`, dentro de `LocalSaveService.Save`, no mesmo instante/mesmo `profile`, pra
  não dessincronizar os dois documentos. Bug de desenho corrigido antes de implementar: o ID do
  documento é `{ownerUid}_{characterId}`, não só `characterId` — esse último hoje é só o nome do
  personagem (`PlayerProfile.OpponentId()`), que não é único entre contas diferentes (duas
  contas jogando de "Medieval Warrior" colidiriam no mesmo documento). Regras de segurança do
  Firestore pra `opponents_index` (já documentadas desde a Fatia 3) confirmadas compatíveis com o
  ID composto — não dependem do nome do path variable, só dos campos `ownerUid`/`characterId`
  dentro do documento.

- 2026-07-15: Fatia 4 do plano de contas/save na nuvem — escopo mínimo por decisão do usuário
  (só a esteira de sincronização, sem fluxo de criar personagem novo do zero). Nova
  `Backend/CloudSyncService.cs` — extrai `LoginController.SyncCharacterRoutine` pra um serviço
  compartilhado (`SyncCharacterAsync`), reaproveitado agora também em
  `CharacterSelectController.OnClickSelect` (grid de `02_SelectCharacter`, bloqueia a navegação
  até sincronizar) e `MainMenuCharacterPreview.SwitchCharacter` (setas rápidas/arraste do
  MainMenu, fire-and-forget pra não travar a animação de troca — `CharacterPanel.Refresh()`
  chamado de novo quando a sincronização termina). Antes desta fatia, só o personagem ativo no
  momento do login era sincronizado com a nuvem — trocar de personagem depois do login (grid ou
  setas) não puxava o save daquele personagem específico, arriscando sobrescrever a nuvem com
  dado local desatualizado/default na próxima gravação. Sem efeito no fluxo offline/sem conta
  (`CloudSyncService` não faz nada quando `AuthService.IsSignedIn` é falso). Continua exigindo
  que o jogador tenha mais de 1 `PlayerProfile` com `isPlayable = true` pra ser testável de
  verdade — hoje só "Medieval Warrior" está nesse estado.

- 2026-07-15: Registrado em `ARQUITETURA.md` — regra permanente "stat base vs. stat efetivo":
  `opponents_index` (Fatia 5, ainda não implementada) precisa usar `GetEffectiveStats()` (campos
  `eff*`) pra exibir personagem em contexto de PvP, nunca o valor base salvo em
  `characters/{characterId}` — motivado pela investigação do bug de save prematuro (linha acima),
  pra não confundir os dois quando a busca de adversário for construída.

- 2026-07-15: Bug real corrigido — usuário reportou que o save na nuvem (Fatia 3) ficava "um
  passo atrás" do personagem depois de subir de nível (ex: SPD mostrado 13 no MainMenu, salvo
  como 8, e antes disso como 6). Duas causas distintas encontradas:
  1. **Saves prematuros/duplicados** (causa real do "um passo atrás"): `AttackSequencer.OnCombatEnd`
     salvava logo após decrementar `battlesRemaining`, e `XpSystem.AddXP` salvava de novo (via
     `MarkDirty`) logo após atualizar XP/level — ambos ANTES do jogador escolher o bônus de
     level-up (`CombatResultPanel.ApplyBonus`, que só roda depois que o painel de escolha
     aparece), capturando `str`/`agility`/`speed` sem o bônus da escolha ainda aplicado.
     Corrigido: `XpSystem.MarkDirty` não chama mais `LocalSaveService.Save` (só
     `EditorUtility.SetDirty`, editor-only); `AttackSequencer.OnCombatEnd` só salva se **não**
     houve level-up (nada mais vai mudar); quando houve, o único save acontece em
     `CombatResultPanel.ApplyBonus`, depois da escolha — exatamente 1 save por combate agora, não
     mais 2-3 saves intermediários incompletos.
  2. **Não era bug** (esclarecido, não alterado): o SPD "13" no `CharacterPanel` do MainMenu é
     `PlayerProfile.GetEffectiveStats().speed` (base + bônus PERCENTUAL de skills como Lightning
     Bolt, calculado ao vivo) — `CharacterDTO`/Firestore salva `profile.speed` (o valor BASE, sem
     o percentual), que é o correto: salvar o valor efetivo duplicaria o bônus da skill na
     próxima vez que `GetEffectiveStats()` rodasse sobre o valor já salvo.

- 2026-07-15: Botão "Sair da Conta" TEMPORÁRIO em `01_MainMenu` (pedido do usuário, só pra testar
  o fluxo de logout enquanto não existe tela de Configurações — mover pra lá quando ela for
  construída, ver `MainMenuController.OnOptionsButton`, ainda um stub). Construído via código em
  `MainMenuController.BuildLogoutButton` (mesmo padrão do `CharacterPanel` já criado em `Start()`
  — Canvas próprio, sem editar `01_MainMenu.unity`), canto superior esquerdo. `AuthService.SignOut()`
  (já existia desde a Fatia 1) + `SceneManager.LoadSceneAsync("00_Login")`. Nenhuma mudança em
  `LoginController` foi necessária — a checagem `AuthService.IsSignedIn` no `InitializeRoutine`
  já cai naturalmente pro formulário quando não há sessão, cobrindo o requisito de não
  auto-logar depois do logout.

- 2026-07-15: Fatia 3 do plano de contas/save na nuvem/busca de adversário — Firestore save/load
  (1 conta/1 personagem). Refatoração: `PlayerProfileConverter.ToDTO/ApplyDTO` extraído de
  `LocalSaveService` (Fatia 0) pra `Data/PlayerProfileConverter.cs`, compartilhado agora pelo
  save local (JSON) e pelo save na nuvem, evitando duas cópias divergentes da mesma lógica. Novo
  `Data/CharacterDTOMap.cs` converte `CharacterDTO <-> Dictionary<string,object>` pro Firestore —
  decisão deliberada de não usar os atributos `[FirestoreData]`/`[FirestoreProperty]` do SDK
  (exigem propriedades, não os campos públicos que `CharacterDTO` já usa pra funcionar com
  `JsonUtility`); `Dictionary`/`SetAsync`/`ToDictionary()` é a API mais estável/documentada.
  Novo `Backend/FirestoreService.cs` (save/load em `users/{uid}/characters/{characterId}`,
  `PersistenceEnabled` habilitado). `LoginController` ganhou `SyncCharacterRoutine` — todo login
  bem-sucedido (auto-login, email/senha, Google) compara o personagem local com a nuvem e aplica
  o mais recente por `updatedAtTicks` (reconciliação "último gravado ganha", relógio do cliente —
  simplificação assumida no plano). `LocalSaveService.Save` agora também empurra pro Firestore em
  segundo plano (fire-and-forget) quando há sessão ativa — acopla `LocalSaveService` ao Firebase
  pela primeira vez, decisão consciente registrada no próprio arquivo. Regras de segurança do
  Firestore pra `users/{uid}/characters` (validação de faixa amarrada ao `level`, não só
  ownership — pedido explícito do usuário) documentadas e prontas pra colar no Console, ver
  `ARQUITETURA.md`.

- 2026-07-15: Fatia 2 do plano de contas/save na nuvem/busca de adversário — Google Sign-In
  (caminho Android nativo, escopo definido pelo usuário; desktop/Editor fica pra depois).
  `AuthService.SignInWithGoogleAsync` (usa `Google.GoogleSignIn` do plugin
  `google-signin-plugin-1.0.4`, API lida direto do `.cs` importado pra evitar suposição errada de
  versão) + botão "Entrar com Google" em `00_Login`. Bug real corrigido antes disso: o pacote do
  plugin trazia `Assets/Parse/Plugins/{Unity.Compat,Unity.Tasks}.dll` — DLLs de compatibilidade
  de uma versão antiga do Unity (.NET 3.5, sem `System.Threading.Tasks` nativo), sobrando de uma
  dependência transitiva de "Parse" nunca usada no projeto — conflitavam com os mesmos tipos já
  existentes no `mscorlib` do .NET Standard 2.1 atual, gerando 39 erros de compilação em pacotes
  de terceiros (`com.unity.searcher`, `com.unity.visualscripting`, etc.). Removida a pasta
  `Assets/Parse/` inteira (nada no projeto referenciava `Parse`). **Só testável em build Android
  de verdade** (`GoogleSignIn.DefaultInstance` lança exceção em qualquer outra plataforma,
  inclusive Editor/Windows — `LoginController` mostra uma mensagem clara nesse caso em vez de
  travar) — módulo Android ainda não instalado no Unity Hub (Fase 7 do roadmap), então o fluxo
  fica como código pronto mas não verificado end-to-end até lá.

- 2026-07-14: Fatia 1 do plano de contas/save na nuvem/busca de adversário — Firebase Auth
  (email/senha). Nova cena `00_Login.unity` (índice 0 no Build Settings, antes de `01_MainMenu`),
  construída via código (mesmo padrão de `ArsenalController`/`SelectOpponentController`):
  formulário de email/senha ("Entrar"/"Criar Conta") + botão "Pular (offline)" sempre visível.
  Novos `Backend/FirebaseBootstrapper.cs` (`CheckAndFixDependenciesAsync`, idempotente) e
  `Backend/AuthService.cs` (wrapper fino sobre `Firebase.Auth`, com tradução dos erros mais
  comuns pra português). Sessão em cache do Firebase Auth → auto-login silencioso, sem passar
  pelo formulário. **Nada depois do login ainda lê dados de conta** (isso começa na Fatia 3) —
  hoje o login é só autenticação; `01_MainMenu` continua funcionando exatamente como antes,
  inclusive pulando a tela de login inteira. Google Sign-In (Fatia 2) e Sign in with Apple
  (Fatia 7, depende de Mac) entram como métodos novos em `AuthService`/`LoginController` depois,
  sem alterar o que já existe.

- 2026-07-14: Bug real corrigido — progressão do jogador (level, XP, skills/armas ganhas,
  favoritos, batalhas restantes) só era "salva" via `EditorUtility.SetDirty`, que é editor-only e
  não faz nada num build real — ou seja, nenhum progresso persistia entre sessões fora do Editor.
  Corrigido com `LocalSaveService` novo (`Assets/Scripts/Backend/LocalSaveService.cs`), que grava
  um snapshot (`CharacterDTO`) em `Application.persistentDataPath/save.json` nos mesmos 5 pontos
  que já chamavam `EditorUtility.SetDirty` de verdade (`XpSystem.MarkDirty`,
  `AttackSequencer.OnCombatEnd`, `CharacterCardUI.OnFavoriteClicked`,
  `CombatResultPanel.ApplyBonus`) — o 6º ponto (`PlayerCombat.ResetToLevel1`, um
  `[ContextMenu]` de debug só acessível no Editor) foi deixado de fora de propósito, já que é uma
  ferramenta de reset pra teste, não parte do fluxo real de progressão. Restauração conectada em
  `MainMenuController.Start`, `CombatSceneLoader.Initialize` (ponto mais crítico — garante que o
  combate usa os stats/armas/skills salvos, não os do asset original) e `CharacterCardUI.Setup`
  (cobre qualquer personagem exibido na grade, não só o atual). `PlayerProfile.characterId`
  (novo campo, retrocompatível) e `OpponentId()` atualizado pra usá-lo quando preenchido.
  `WeaponDatabase.asset`/`SkillDatabase.asset` movidos pra `Assets/Resources/` (mesmo padrão já
  usado por `SelectedProfileHolder`/`BattleGround`) — `LocalSaveService` precisa resolvê-los via
  `Resources.Load` pra reconstruir a lista de armas/skills salva (nomes+tier) de volta em
  referências reais de asset, usando os novos `WeaponDatabase.FindByFamilyNameAndTier`/
  `SkillDatabase.FindByFamilyNameAndTier`. **Dívida técnica conhecida, registrada por pedido
  explícito do usuário**: `save.json` é texto plano, editável por qualquer editor de
  texto/save-editor — decisão consciente de não ofuscar/criptografar por enquanto (ver comentário
  no topo de `LocalSaveService.cs` e `ARQUITETURA.md`); revisitar quando houver ranking
  competitivo de verdade. Primeira etapa ("Fatia 0") do plano de contas/save na nuvem/busca de
  adversário — Firebase entra nas próximas fatias.

- 2026-07-14: Bug de performance — usuário reportou o botão "Continuar" de `CombatResultPanel`
  (tela de fim de combate) demorando pra voltar ao `01_MainMenu`. Mesmo padrão já corrigido em
  `ArsenalController.OnBackClicked`: o botão chamava `SceneManager.LoadScene` (síncrono)
  diretamente no `onClick`. Trocado por `SceneManager.LoadSceneAsync` (via coroutine
  `LoadMainMenuAsync`) — `04_CombatScenePVP` carrega ainda mais assets que `03_Arsenal` (os 2
  personagens completos, todas as armas em `AttackSequencer.allWeapons`, o `SkillDatabase`
  inteiro), então é candidato ainda mais forte ao mesmo overhead de Play Mode do Editor
  investigado antes — o fix é o mesmo (sem custo, estritamente melhor), independente da causa
  ser Editor ou real.

- 2026-07-14: Bug de performance — conclusão final da investigação do "Voltar" lento em
  `03_Arsenal` (entradas abaixo). Instrumentação temporária (`Stopwatch`, removida depois de
  usada) mediu ~700-800ms entre o clique e `MainMenuController.Start()` no Editor (Play Mode),
  contra ~160ms do "Voltar" equivalente em `02_SelectCharacter` — confirmando que o atraso era
  real e específico do Arsenal, não imaginação. Uma tentativa de evitar o descarregamento
  automático de assets (carregar `01_MainMenu` em modo aditivo + descarregar `03_Arsenal`
  manualmente via `UnloadSceneAsync`) **não reduziu o tempo** — o próprio carregamento aditivo (sem
  nenhum descarregamento ainda) já mostrava os mesmos ~700ms, descartando aquela hipótese.
  Usuário testou o mesmo fluxo num **build real** (fora do Editor) e confirmou que é rápido lá —
  ou seja, era overhead específico do Play Mode do Editor (serialização/GC mais pesados,
  escalando com o quanto `03_Arsenal` carrega: é a única cena que referencia
  `WeaponDatabase`/`SkillDatabase`, o catálogo inteiro de armas/skills do jogo), sem impacto real
  no jogo jogável. A tentativa de carga aditiva foi revertida (complexidade sem benefício
  comprovado); `ArsenalController.OnBackClicked` ficou só com `SceneManager.LoadSceneAsync`
  (assíncrono simples, ver entrada abaixo) — estritamente melhor que o `LoadScene` síncrono
  original, sem custo. Toda a instrumentação de diagnóstico (`PerfDebugClock` e os `Debug.Log`
  associados em `MainMenuController`/`MainMenuCharacterPreview`/`CharacterSelectController`) foi
  removida.

- 2026-07-14: Bug de performance — usuário reportou o "Voltar" de `03_Arsenal` ainda lento depois
  do fix do `CharacterPanel` preguiçoso (entrada abaixo), diferente do "Voltar" de
  `02_SelectCharacter` (mais rápido, por comparação subjetiva nesse momento). Hipótese
  investigada nesta rodada (revista pela entrada acima após medição): `03_Arsenal` é a única cena
  que referencia `WeaponDatabase`/`SkillDatabase` — o catálogo inteiro de armas/skills do jogo —
  e `SceneManager.LoadScene` (síncrono) descarregaria esse volume todo num frame só.
  `ArsenalController.OnBackClicked` trocado de `SceneManager.LoadScene` (síncrono) pra
  `SceneManager.LoadSceneAsync` (via coroutine) — mudança mantida (sem custo, estritamente
  melhor), mas não foi essa a causa raiz real (ver entrada acima).

- 2026-07-14: Bug de performance — usuário reportou o botão "Voltar" de `03_Arsenal` lento
  também. Causa: `ArsenalController.BuildDetailPanel` construía eager em `Start()` um
  `CharacterPanel` completo (HUD Compact+Expanded, grades de Habilidades/Armas, seção Passivas,
  popup — ~150 GameObjects) só pra reaproveitar `ShowWeaponDetail`/`ShowSkillDetail`; o HUD nunca
  aparece (`HideRootPermanently`), mas o custo de montar e depois destruir tudo isso era pago
  mesmo sem o jogador clicar em nenhum slot. Trocado por construção preguiçosa
  (`EnsureDetailPanel`, chamado só na 1ª vez que um slot é clicado) — entrar/sair do Arsenal sem
  abrir nenhum detalhe não paga mais esse custo.

- 2026-07-14: Bug de performance — usuário reportou travamento ao clicar em "Chibers"/"Arsenal"
  no menu principal. Causa em `02_SelectCharacter`: `CharacterSelectController.PopulateCharacterGrid`
  criava os 72 `CharacterCardUI` (cada um com ~8 GameObjects + TMP com auto-sizing) num único
  frame de `Start()` — pico perceptível de CPU. Corrigido construindo os cards em lotes de 12 por
  frame via coroutine (`PopulateCharacterGridRoutine`), mesmo resultado final, sem pico único.
  Também a câmera de preview (`BuildPortraitPreview`) ficava ativa renderizando desde o `Start()`
  mesmo sem nenhum personagem instanciado ainda pra filmar — agora começa desativada, só liga ao
  selecionar um card (`OnCharacterSelected` já fazia isso; só faltava o estado inicial coerente).
  `03_Arsenal` foi auditada também (grid de ~77 armas/skills, sem auto-sizing) e está bem mais
  leve — não precisou de mudança.

- 2026-07-14: Roadmap (Fase 1) — 2 itens marcados como concluídos após verificação no código
  (nenhum dos dois exigiu implementação nova, só confirmação de que já estavam cobertos):
  "Arte chibi + retrato realista do personagem" (`PlayerProfile.splashArt`, já preenchido nos 72
  `PlayerProfile` — nenhum com `{fileID: 0}` — exibido em tela cheia atrás do preview chibi em
  `02_SelectCharacter`) e "Exibir status base... armadura" (já aparece na seção PASSIVAS do
  `CharacterPanel` Expanded, `SetPassive("Armor", ...)` — a nota antiga dizendo que faltava esse
  campo estava desatualizada).

- 2026-07-14: Grid de `02_SelectCharacter` trocado de scroll horizontal (3 linhas fixas,
  `FixedRowCount`) pra scroll VERTICAL (3 cards por linha, `FixedColumnCount`) — mesmo padrão do
  Arsenal, pedido do usuário. `CharacterCardUI.CardWidth`/`CardHeight` reduzidos de 600×310 pra
  500×259 (~5/6, junto com os demais elementos internos do card — portrait, faixa de nome,
  barra de XP, estrela de favorito, fontes) pra 3 caberem na largura nova do Scroll View
  (1544.65px). `CharacterSelectController.EnsureGridLayout`: `Content` passou do esquema "largura
  auto-fit, altura = Viewport" pro esquema "largura = Viewport, altura auto-fit"
  (`ContentSizeFitter.verticalFit=PreferredSize`); `childAlignment=UpperCenter` (3×500+2×17=1534
  não preenche exatamente os 1544.65px, então centraliza em vez de deixar vão à direita); os dois
  scrollbars desligados (só arraste, igual ao Arsenal). Scroll View reposicionado: pos=(175.03,
  -21), size=(1544.65, 964). O `Viewport` já tinha `Image`+`Mask` próprios desde a montagem
  original da cena — diferente do bug corrigido antes no Arsenal, aqui arrastar em qualquer
  ponto da tela (não só em cima de um card) já funcionava sem precisar de nenhum fix adicional.

- 2026-07-14: Sombra de bloqueado das skills em `03_Arsenal` ajustada de 0.90 pra 0.96 de
  opacidade (pedido do usuário — valor final desta rodada de ajuste).

- 2026-07-14: Sombra de bloqueado das skills em `03_Arsenal` (`ArsenalSlotUI.LightLockedOverlayColor`)
  ajustada de 0.72 pra 0.90 de opacidade (pedido do usuário — valor final desta rodada).

- 2026-07-14: Dois ajustes em `03_Arsenal`: (1) sombra de bloqueado das skills (`dimIcon=false`)
  escurecida de 0.5 pra 0.72 de opacidade — 0.5 tinha ficado "muito claro" (pedido do usuário).
  (2) Corrigido scroll que só funcionava arrastando em cima de um ícone — o `Viewport` do
  `ScrollRect` só tinha `RectMask2D` (sem nenhum `Image`), então o `GraphicRaycaster` não achava
  nada pra "bater" nas áreas vazias entre/abaixo dos ícones, e o `ScrollRect` nunca recebia o
  evento de arrastar ali. Adicionado um `Image` quase invisível (alpha 0.001, só cosmético — o
  raycast não liga pra alpha) cobrindo o Viewport inteiro, mesmo padrão do ScrollView default da
  própria Unity — arrastar em qualquer ponto da tela agora rola.

- 2026-07-14: `ArsenalSlotUI.Build` ganhou o parâmetro `dimIcon` (default `true`) — bloqueado
  antes ficava "totalmente preto" nas skills (ícone tingido quase-preto + overlay 55% em cima,
  os dois se somando). Armas mantêm esse visual (`dimIcon=true`, "pode manter totalmente escuro
  como está" — pedido do usuário); skills usam `dimIcon=false`: ícone na cor original + só uma
  sombra de 50% por cima, bem mais claro. `ArsenalController.BuildScrollView` passa `dimIcon:
  true` pra ARMAS e `dimIcon: false` pra SKILLS.

- 2026-07-14: `Btn_Arsenal` reposicionado em `01_MainMenu.unity` — de pos=(-617.32, 8.18) (ao
  lado do "Chibers") pra pos=(-837.91, -203.69) (mesmo X do "Chibers", abaixo dele), mesmo
  tamanho (200.59×166.73). `ArsenalController.BuildSkillSlots` passou a pular Garimpeiro/Magneto
  (`skillName`) — skills ainda não implementadas (`effectText` vazio), não devem aparecer no
  grid do Arsenal até terem mecânica de verdade.

- 2026-07-14: Corrigido clique não funcionando nos slots de `03_Arsenal` — bug real:
  `ArsenalSlotUI` tinha o `Button` no `Border`, mas `IconBg`/`Icon` (desenhados DEPOIS, por cima)
  tinham `raycastTarget=true` por padrão e absorviam o clique antes dele chegar no `Border`.
  Corrigido com `raycastTarget=false` nos dois. Além disso, o popup de detalhe próprio criado
  antes foi **substituído** por uma instância real de `CharacterPanel` (mesmo componente do HUD
  lateral de `01_MainMenu`) — `ShowWeaponDetail`/`ShowSkillDetail` tornados públicos e
  reaproveitados diretamente, garantindo popup IDÊNTICO ao do menu principal (pedido do
  usuário), em vez de uma versão simplificada própria. Novo `CharacterPanel.HideRootPermanently()`
  move o HUD Compact/Expanded desse painel embutido pra fora da tela sem desativar seu Canvas
  (diferente de `HideSlideOut`), já que o popup é sibling do mesmo Canvas e precisa dele ativo
  pra continuar funcionando. Funciona também pra armas/skills bloqueadas — mostra os atributos da
  família/T1 com uma nota indicando que ainda não foi obtida.

- 2026-07-14: Ajustes na tela de Arsenal (`03_Arsenal`), a pedido do usuário: (1) grids de ARMAS e
  SKILLS centralizados (`GridLayoutGroup.childAlignment` de `UpperLeft` pra `UpperCenter` — antes
  sobrava um vão vazio à direita, já que as células não preenchem sozinhas a largura toda do
  Content) + título de cada seção também centralizado; (2) todo slot agora é clicável, mesmo
  bloqueado/não possuído (`ArsenalSlotUI.Build` ganhou um `onClick` opcional no `Border`, que
  cobre o slot inteiro) — abre um popup de detalhe (overlay + painel, auto-size via
  `VerticalLayoutGroup`+`ContentSizeFitter`) mostrando os atributos completos da arma (tipos,
  dano, velocidade, alcance, chance de puxar, bônus condicionais só se não-zero) ou skill
  (descrição + effectText), inclusive quando o personagem não possui — nesse caso mostra os
  atributos da família/T1 como referência, com uma nota indicando que ainda não foi obtida.

- 2026-07-14: Criada tela de Arsenal (Armas & Skills) com grid de 6 colunas, tier visual
  (bronze/prata/ouro) e estado bloqueado/desbloqueado por personagem. Nova cena `03_Arsenal.unity`
  (registrada em Build Settings) — cena minimalista (só Main Camera + `ArsenalController`), a UI
  inteira (Canvas/ScrollView/grids/botão Voltar) é montada via código em `Start()`, mesmo padrão
  de `CharacterSelectController`. Acessível por um novo botão "Arsenal" em `01_MainMenu`, mesma
  linha de atalhos de "Chibers" e mesmo estilo card (`CharacterCardButtonStyle`) + outline/drop
  shadow (`UIButtonShadowStyle`, mesmo componente do JOGAR/Chibers).
  Novo `WeaponDatabase` (`Assets/ScriptableObjects/Databases/WeaponDatabase.asset`) — as armas
  nunca tiveram um database equivalente ao `SkillDatabase` (que já existia); lista os 26
  `WeaponData` T1 (raiz de cada família). Novo componente reutilizável `ArsenalSlotUI`
  (ícone + borda por tier + escurecido/bloqueado se não possuído) compartilhado entre a grade de
  ARMAS e a de SKILLS, evitando duplicar a lógica entre as duas. `ArsenalController` resolve o
  maior tier possuído subindo a cadeia `previousTier` de cada arma/skill do `PlayerProfile` até
  achar a raiz da família e comparando com a célula da grade — só LÊ `PlayerProfile.weapons`/
  `skills`, nunca altera.
  **Decisão de nomeação de cena**: `03_SelectWeapons` (campo `MainMenuController.selectWeapons`,
  referenciado mas nunca chamado por nenhum método, e nunca criado) foi deixado intocado —
  reservado pra uma futura tela de escolha de LOADOUT pré-combate (propósito diferente: montar
  quais armas levar pra uma luta específica, não visualizar a coleção inteira). Arsenal ganhou o
  nome/slot próprio `03_Arsenal` em vez de reaproveitar esse slot reservado, pra não confundir os
  dois conceitos.

- 2026-07-14: Adicionado outline + drop shadow reutilizável nos botões principais do main menu
  (JOGAR, Chibers) para melhorar legibilidade contra o background. Novo componente
  `UIButtonShadowStyle` (`Assets/Scripts/UI/`, `[ExecuteAlways]`) adiciona/configura os efeitos
  nativos `UnityEngine.UI.Outline` + `Shadow` sobre o próprio `Graphic` do GameObject (mesmo
  Image de fundo do botão, sem sprite pré-renderizado) — cor/distância/`useGraphicAlpha` de cada
  efeito expostos no Inspector, com defaults sutis (contorno marrom escuro ~2px, sombra preta
  ~45% opacidade, offset (0,-3)). Aplicado em `Btn_SelectCharacter` ("Chibers") e `BtnJogar`
  ("JOGAR") em `01_MainMenu.unity`, sem alterar cor de fundo nem `onClick` de nenhum dos dois.
  Detalhe de implementação: `GetComponent<Shadow>()` também casaria com um `Outline` já presente
  (`Outline` herda de `Shadow` em `UnityEngine.UI`) — `FindPlainShadow()` filtra pelo tipo exato
  pra não confundir os dois efeitos num só.

- 2026-07-14: Background de `01_MainMenu` ("floating castle", `SpriteRenderer` world-space,
  `sortingOrder -1`, atrás de tudo) desativado (`m_IsActive: 0`, não deletado — reversível) a
  pedido do usuário.
  **Substituído no mesmo dia**: o usuário adicionou a arte nova em
  `Assets/Resources/Backgrounds/Background Main Menu.jpg` (já importada como Sprite) —
  reaproveitado o mesmo GameObject (renomeado de "floating castle" pra "Background Main Menu",
  reativado) em vez de criar um novo do zero: `SpriteRenderer.m_Sprite` trocado pro novo asset,
  `Transform` ajustado pra pos=(-0.02, 3.38, 0)/scale=(1.992416, 1.992416, 1.992416) (valores
  calibrados pelo usuário no Editor).

- 2026-07-14: `Btn_SelectCharacter` ("Chibers") reposicionado/redimensionado em `01_MainMenu.unity`
  — `RectTransform` (anchor 0.5/0.5) de pos=(0, −422.4)/size=(495, 170) pra pos=(−837.91, 8.18)/
  size=(200.59, 166.73), a pedido do usuário.

- 2026-07-14: `isPlayable` desligado (`false`) em massa nos 71 `PlayerProfile` restantes (script
  em lote, `sed` direto nos `.asset`) — só `Medieval Warrior` continua jogável/clicável no grid
  de `02_SelectCharacter`; os outros 71 continuam aparecendo (isUnlockedForSelection segue `true`),
  só travados/cinza sem Button, mesma regra de sempre. `CharacterCardUI` também parou de desenhar
  a estrela de favorito em cards travados (`isEnabled=false`) — não fazia sentido favoritar um
  personagem que ainda nem pode ser escolhido pra jogar.

- 2026-07-14: `CharacterDatabase.ComparePlayerProfiles` ganhou um critério de ordenação novo entre
  favorito e nome: raridade crescente (Normal/"comum" → Uncommon → Rare → Legendary → Immortal —
  já é a ordem dos valores do enum `CharacterRarity`, só comparar os ints). Afeta o grid de
  `02_SelectCharacter` e a troca rápida de personagem do menu principal ao mesmo tempo (os dois
  usam esse mesmo comparador, ver histórico acima) — favoritos continuam vindo primeiro, depois
  agrupados por raridade, e só dentro da mesma raridade a ordem alfabética desempata.

- 2026-07-14: Roadmap "Seta lateral no personagem central para troca rápida de personagem" (Fase
  1) implementado — `MainMenuCharacterPreview` ganhou setas `<`/`>` (canvas próprio,
  `BuildSwapArrows`/`BuildArrowButton`) flanqueando o personagem central, com pulso sutil de
  escala (`PulsingScale`, novo componente, mesmo espírito de `PulsingAlpha` em
  `AttributePipBar.cs`). Clicar numa seta ou arrastar o próprio personagem (`CharacterSwipeInput`,
  novo componente no mesmo `BoxCollider2D`/mensagens `OnMouse*` já usadas por
  `CharacterPreviewReaction`) chama `SwitchCharacter(direction)`, que avança/volta dentro de
  `CharacterDatabase.GetPlayableCharactersOrdered()` (novo, filtro `isUnlockedForSelection &&
  isPlayable` + mesmo critério de ordenação favorito-depois-alfabético já usado pelo grid de
  `02_SelectCharacter` — `ComparePlayerProfiles`, agora estático em `CharacterDatabase` e
  reaproveitado por `CharacterSelectController` em vez de duplicado). A troca atualiza
  `SelectedProfileHolder.currentProfile` de verdade (não só o preview) e chama o novo
  `CharacterPanel.Refresh()` público pra o painel lateral reflitir o personagem novo na hora.
  `SpawnCharacter` (extraído de `Start()`) usa `Destroy()` em vez de `DestroyImmediate()` pro
  GameObject do personagem — a troca pode ser disparada de dentro de um `OnMouseUp` rodando no
  próprio objeto (arraste), e destruir na hora um GameObject cuja própria mensagem nativa ainda
  está no stack é arriscado.
  **Refinamento no mesmo dia**: pedido do usuário pra dar feedback visual de verdade ao arrastar —
  a troca não é mais instantânea; `SwitchCharacter` agora roda a coroutine `SlideToCharacter`
  (nova, substitui a chamada direta a `SpawnCharacter` na troca — `SpawnCharacter` continua só
  pro 1º personagem exibido em `Start()`, sem transição): o personagem atual desliza pra fora no
  mesmo sentido do arraste/seta (~10 unidades de mundo, além da meia-largura visível ~8.89, garante
  sair da tela de vez) enquanto o próximo entra do lado oposto até centralizar (`SlideDuration`
  0.28s, `Vector3.Lerp`). `_isSliding` ignora uma 2ª troca disparada no meio da animação (evita
  dois personagens entrando/saindo ao mesmo tempo). Lógica de "tirar componentes de combate +
  Idle" extraída pra `PrepareCharacterForPreview` (compartilhada entre `SpawnCharacter` e
  `SlideToCharacter`, evita duplicar a sequência duas vezes).

- 2026-07-14: Grade de `02_SelectCharacter` ordenada alfabeticamente por nome (era a ordem crua de
  `CharacterDatabase.unlockedCharacters`) + novo campo `PlayerProfile.isFavorite` com estrela no
  canto superior direito do `PortraitBox` de cada `CharacterCardUI` — clicar na estrela
  favorita/desfavorita o personagem sem selecionar o card (Button próprio, não borbulha) e
  repopula a grade (`CharacterSelectController.PopulateCharacterGrid`/`CompareFavoriteThenName`),
  trazendo favoritados pro início da lista (antes da ordenação alfabética, dentro do mesmo grupo
  habilitado/travado). Como o `GridLayoutGroup` já usa `FixedRowCount=3`/`Axis.Vertical`, a ordem
  da lista já corresponde à leitura "cima pra baixo, esquerda pra direita" sem mudar o layout.
  **Correção no mesmo dia**: a 1ª versão desenhava a estrela como glyph Unicode TMP ("★"/"☆") —
  a fonte usada no projeto não tem esse glyph no atlas, renderizava como um quadrado "tofu"
  (reportado pelo usuário). Trocado por `UIShapeUtil.Star(Color, filled)` (novo, mesmo padrão de
  `RoundedRect`) — rasteriza o polígono de 5 pontas em runtime (ray-casting point-in-polygon),
  sem depender de cobertura de fonte nem de importar sprites externos; contorno vazado (não
  favoritado) é o mesmo polígono encolhido subtraído do preenchido. **2ª correção**: a estrela
  gerada apareceu de cabeça pra baixo — `StarPoints` usava `rot = -90°` pro 1º vértice, mas
  `Texture2D`/`Sprite` tem `y=0` na base (convenção padrão da Unity), então esse ângulo apontava
  pra BAIXO em vez de pra cima; trocado pra `+90°`.
- 2026-07-14: `PlayerProfile.rarity` (novo enum `CharacterRarity`: Normal/Uncommon/Rare/Legendary/
  Immortal, default Normal) — raridade puramente cosmética por personagem, sem efeito em combate.
  `UITheme` ganhou 5 cores novas (`rarityNormal` cinza, `rarityUncommon` verde, `rarityRare` azul,
  `rarityLegendary` laranja, `rarityImmortal` vermelho — ver UI_PALETTE.md). `CharacterCardUI`
  (grid de `02_SelectCharacter`) agora colore o fundo do `PortraitBox` pela raridade do
  `PlayerProfile` em vez do hash do nome (paleta arco-íris antiga, sem significado nenhum) —
  bloqueado continua dessaturando a mesma cor (`Desaturate`, padrão já existente).
- 2026-07-14: Roadmap "Criação do primeiro personagem masculino e feminino" (Fase 1) marcado como
  concluído no CLAUDE.md — já estava coberto desde os primeiros personagens (Assassin Guy/Medieval
  Warrior Girl), confirmado agora com os 69 `PlayerProfile` existentes cobrindo ambos os gêneros.
- 2026-07-14: Renomeado botão Guerreiros para Chibers + restyle visual estilo card (ícone +
  label, badge de notificação). `01_MainMenu.unity` — `Btn_SelectCharacter` teve o texto TMP
  trocado de "GUERREIROS" pra "CHIBERS" (nenhum script referenciava a string antiga — nome do
  GameObject/lógica de navegação, `MainMenuController.OnCharacterButton`, inalterados) e ganhou
  o componente `CharacterCardButtonStyle` (novo, `Assets/Scripts/UI/`), que reestiliza o botão em
  runtime no padrão card (estilo Brawl Stars): fundo arredondado (`UIShapeUtil.RoundedRect`,
  raio 16px, cor `UITheme.secondaryButton`), ícone placeholder preenchendo o topo do card (tint
  claro do mesmo tom, até a arte definitiva existir — `iconOverride` já expõe o campo pra
  substituição), faixa inferior mais escura (`panelBackgroundAlt`) com o label bold+outline, e um
  badge circular de notificação (`NotificationBadge`, filho desativado por padrão — `danger`,
  canto superior direito) ligado via `SetNotificationCount(int)`. Cores todas via `UITheme`, sem
  hex hardcoded (ver UI_PALETTE.md).
- 2026-07-14: `isUnlockedForSelection`/`isPlayable` ligados em massa (`true`) nos 72 `PlayerProfile`
  — pedido do usuário pra visualizar todos os personagens já importados no grid de
  `02_SelectCharacter` (antes só Assassin Guy/Medieval Warrior/Medieval Warrior Girl apareciam
  desbloqueados; os outros 56+ importados via `Import Female Character` ficavam ocultos por
  padrão). Puramente visual/seleção — não afeta o hardcode de Medieval Warrior Girl como Player2
  em `04_CombatScenePVP`.
- 2026-07-14: `splashArt` atribuído em massa aos 70 `PlayerProfile` com arte gerada em
  `Assets/Personagens/00-SplashArt/` (nome do arquivo casado com o nome do personagem/número da
  variante; 2 casos ambíguos resolvidos por comparação visual — "Dark Knight.jpg" pertence ao
  **Death Knight**, não ao Hell Knight; "Golem.jpg"/"Globin.jpg" para Golem 1/Goblin). `Medieval
  Warrior.asset` tinha uma referência de `splashArt` órfã (guid sem arquivo correspondente,
  provavelmente do path antigo `Assets/Personagens/SplashArt/`) — corrigida para apontar ao novo
  arquivo da mesma pasta.
- 2026-07-13: `Tools > AutoArms > Import Female Character` rodado com sucesso sobre todo o backlog
  acumulado — **56 personagens novos** ganharam `PlayerProfile` + prefab jogável completo nesta
  passada (todos ocultos/travados por padrão, `isUnlockedForSelection=false`/`isPlayable=false`):
  Anubis, Black Ninja, Black Reaper 1, Blacksmith Guy, Blood Demon 2, Citizen 1, Dark Oracle 3,
  Death Knight, Demons of Darkness 1, Desert Nomad 3, Devil, Devil Masked Guy, Egyptian Mummy,
  Egyptian Sentry, Elemental Spirits 1, Elementals 2, Evil Bald Guy, Fallen Angels 3, Forest
  Guardian 1, Ghost Knight 3, Ghoul, Goblin, Golem 1, Hell Knight, Lich, Magician 3, Magician Girl 1,
  Magician Girl 2, Medieval King, Medieval Thug, Medusa 2, Mimic 1, Minotaur 1, Old Guy, Persian and
  Arab Warriors 3, Pirate, Priest 3, Pumpkin Head Guy, Reaper Man 1, Romanian Settler, Samurai 1,
  Samurai 2, Samurai 3, Seer 2, Shamans 3, Skeleton, Skeleton Samurai 2, Skull Knight, Spiritual
  Monk 1, Technomage 2, Thief, Vampire, Vampire Hunter 1, White Ninja, Winter Witch 1, Winter Witch 2.
  Nenhum erro de GUID (checagem prévia deste mesmo dia já tinha confirmado zero colisão em todo
  `Assets/Personagens`) nem de retargeting reportado. Efeito colateral esperado do pipeline em
  personagens já existentes: idle frame movido pra dentro de `Prefab/` (`Vampire_2`,
  `Vampire_Hunter_3` — arquivo solto na raiz antes) e retarget do "Slashing Dagger" reaplicado nos
  12 personagens processados numa rodada anterior (`Amazon_Warrior_3`, `Citizen-Women_2`,
  `Dark_Elves_1`, `Fallen_Angels_1`, `Magician_Girl_3`, `Medieval Hooded Girl`, `Medusa_3`,
  `Necromancer_of_the_Shadow_1`, `Succubus`, `Vampire_2`, `Vampire_Hunter_3`, `Winter_Witch_3` —
  prefab reaberto e resalvo pelo `PrefabUtility.SaveAsPrefabAsset`, sem mudança funcional). Ver
  itens 6 e 7 do `CHARACTER_IMPORT_CHECKLIST.md` pro histórico de GUID/Sword.png que antecedeu esta
  rodada.

- 2026-07-13: Novo lote grande de personagens importado pelo usuário (`Anubis`, `Black_Reaper_1`,
  `Blood_Demon_2`, `Dark_Oracle_3`, `Death_Knight`, `Demons_of_Darkness_1`, `Devil`,
  `Elemental_Spirits_1`, `Elementals_2`, `Forest_Guardian_1`, `Ghost_Knight_3`, `Ghoul`, `Goblin`,
  `Golem_1`, `Hell_Knight`, `Lich`, `Magician_3`, `Mimic_1`, `Minotaur_1`, `Reaper_Man_1`, `Seer_2`,
  `Shamans_3`, `Skeleton`, `Skeleton_Samurai_2`, `Vampire`), ainda sem rodar
  `Tools > AutoArms > Import Female Character`. Checagem de GUID em todo o `Assets/Personagens`
  (1314 `.meta`, `Vampire` vs `Vampire_2` incluído) — **zero colisão** no lote inteiro, nenhum fix
  de GUID necessário desta vez. `Sword.png` removido (mesma política do item 7 do
  `CHARACTER_IMPORT_CHECKLIST.md`) de 22 dos 24 personagens novos crus (`Black_Reaper_1` até
  `Skeleton_Samurai_2` — todos sem `Prefab/` ainda, remoção limpa, sem referência quebrada);
  `Anubis` já não tinha `Sword.png`. **`Vampire` foi deixado com o `Sword.png`** — veio no formato
  "pronto" (`Graphics/`+`Prefab/` já montados pelo Spriter2UnityDX, sem `Animations.scml` na pasta)
  e o `.prefab`/`.controller` já foram gerados COM o sprite presente; sem o `.scml` de origem não
  tem como forçar um reimport limpo (truque usado no `Samurai_1` não se aplica aqui) — apagar o
  arquivo agora só deixaria uma referência quebrada pendurada no prefab já construído, pra nenhum
  ganho visual real sobre deixar como está. Fica pendente de limpeza manual no Editor (arrastar o
  campo `Sprite` do componente do bone "Sword" pra `None` dentro do prefab) se o usuário quiser.

- 2026-07-13: `Sword.png` removido de `Vector Parts/` de todo personagem ainda não processado pelo
  `Import Female Character` — decisão do usuário: o corpo do personagem não deve carregar sprite de
  espada nenhum, só as armas do sistema `WeaponData`/`WeaponHandler` (equipadas em runtime) devem
  aparecer na mão. Afeta `Samurai_1` (já tinha `Samurai.prefab`/`.controller` gerados pelo
  Spriter2UnityDX com `Sword` referenciando o arquivo agora removido — apagados junto com o `.meta`
  do `Animations.scml` pra forçar reimport limpo, sem o bone morto, na próxima vez que o Unity
  abrir o projeto) e mais 8 personagens ainda 100% crus (sem `Prefab/`, sem `PlayerProfile`):
  `Citizen_1`, `Desert_Nomad_3`, `Persian_and_Arab_Warriors_3`, `Pirate`, `Priest_3`,
  `Spiritual_Monk_1`, `Technomage_2`, `Thief` — só o arquivo+`.meta` removidos, nada mais a fazer
  neles (o `Import Female Character` ainda não tinha rodado, não sobrou prefab pra limpar). Não
  mexido em nenhum personagem já com `PlayerProfile` existente (fora de escopo, exigiria editar
  `.prefab` já publicado). Confirmado lendo `Spriter2UnityDX/Editor/PrefabBuilder.cs` (`GetSpriteAtPath`,
  linha ~184) que um `Sword.png` ausente é só um `Debug.LogError` cosmético — não seta `success =
  false`, então não aborta a geração do prefab; o bone fica sem sprite (invisível), mesmo padrão já
  em produção em `Vampire_Hunter_3`. Detalhe em `CHARACTER_IMPORT_CHECKLIST.md` (item 7, novo).

- 2026-07-13: `Samurai_2`, `Samurai_3` e `Vampire_Hunter_1` (`Assets/Personagens/<nome>/Vector Parts/`) tiveram os `.unitypackage` extraídos manualmente e GUIDs regenerados antes de entrar no projeto — mesmo problema do item 6 do `CHARACTER_IMPORT_CHECKLIST.md` (variantes numeradas da CraftPix reusam os GUIDs internos do template). Confirmado por diff de GUID: `Samurai_1/2/3` compartilham os mesmos 13 GUIDs entre si (nenhum ainda importado no projeto, mas colidiriam entre si); `Vampire_Hunter_1` tinha os 17 GUIDs **idênticos** aos de `Vampire_Hunter_3`, já totalmente processado no projeto (`PlayerProfile` próprio referenciando o prefab dele) — importar `Vampire_Hunter_1` como veio do pacote sobrescreveria `Vampire_Hunter_3` por baixo dos panos. `Samurai_1` não precisou de fix (único dos 3 que herda os GUIDs originais do template; segue pendente de import normal pelo usuário). Fix aplicado nos 3 casos: extraídos os PNGs de corpo (`Body`/`Face 01-03`/`Head`/`Left-Right Arm-Hand-Leg`/`SlashFX`) + `Animations.scml` do `.unitypackage` (descartados `Sword.png`/`.prefab`/`.controller` do pacote — sem referência cruzada sobrando pra reescrever, já que só a linha `guid:` de cada `.meta` precisou de valor novo), GUID novo por arquivo (13 arquivos + pasta `Vector Parts` em si, 14 GUIDs por personagem) via `openssl rand -hex 16`, confirmado sem colisão contra o projeto inteiro antes de colocar em `Assets/Personagens/<nome>/Vector Parts/`. Faltam pra cada um: rodar `Tools > AutoArms > Import Female Character` (regenera `.prefab`/`.controller` via Spriter2UnityDX ao abrir o Unity) + 1 frame solto com "idle" no nome. Detalhe completo em `CHARACTER_IMPORT_CHECKLIST.md` (item 6, ampliado).

- 2026-07-11: Retarget do "Slashing Dagger" passou a rodar automaticamente dentro de
  `Tools > AutoArms > Import Female Character` (`FemaleCharacterImportGenerator.ProcessCharacter`
  chama `RetargetSlashingDaggerNow` logo após montar o Animator Controller) — antes dependia de
  rodar `Retarget Slashing Dagger For All Characters` manualmente DEPOIS, passo que ficou pra trás
  em `Magician_Girl_1/2`, `Medusa_2`, `Winter_Witch_1/2` (reportado pelo usuário: cabeça "descolando"
  no Slashing Dagger — causa real era o clipe cru compartilhado da Assassin Guy, nunca retargetado
  pra essas 5). Refatorado `TryLoadSlashDaggerSource` (carrega clipe/prefab da Assassin Guy uma vez)
  compartilhado entre o novo `RetargetSlashingDaggerNow` (por personagem, chamado no import) e o
  `RetargetSlashingDaggerForAll` existente (em lote, mantido só pra reprocessar quem foi importado
  antes deste ajuste). Detalhe em `CHARACTER_IMPORT_CHECKLIST.md` (passo 4).

- 2026-07-11: `Magician_Girl_1`, `Magician_Girl_2`, `Medusa_2`, `Winter_Witch_1` e `Winter_Witch_2` (`Assets/Personagens/<nome>/Vector Parts/`) importados com GUIDs regenerados — completa a lista dos 6 personagens que tinham sido apagados por corrupção de GUID (item 6 do `CHARACTER_IMPORT_CHECKLIST.md`; `Archer_2` ainda não recuperado, arte de origem não localizada). Confirmado que os `.unitypackage` numerados vêm da CraftPix com os mesmos GUIDs internos entre variantes do mesmo personagem-base (mesmo template clonado), colidindo com a variante já processada no projeto (`Magician_Girl_3`/`Medusa_3`/`Winter_Witch_3`) mesmo importando cada pacote direto pelo Unity — inclusive entre si (`Winter_Witch_1` e `_2` também colidiam um com o outro, não só com o `_3`). Detalhe do problema/fix em `CHARACTER_IMPORT_CHECKLIST.md` (item 6, ampliado). Faltam pra cada um: 1 frame solto com "idle" no nome (fonte do `previewIcon`) e rodar `Tools > AutoArms > Import Female Character`.

- 2026-07-10: Pipeline de importação incremental de personagens novos (`Assets/Editor/FemaleCharacterImportGenerator.cs`, menu `Tools > AutoArms`) — 13 personagens processados (Amazon Warrior 3, Citizen Women 2, Dark Elves 1, Fallen Angels 1, Magician Girl 3, Medieval Hooded Girl, Medusa 3, Necromancer of the Shadow 1, Succubus, Valkyrie 1, Vampire 2, Vampire Hunter 3, Winter Witch 3), cada um ganhando prefab jogável completo (componentes de combate + Animator Controller reconstruído campo a campo a partir do `Assassin Guy.controller`) e `PlayerProfile` próprio, todos ocultos/travados por padrão. Duas flags novas em `PlayerProfile`: `isUnlockedForSelection` (aparece no grid de `02_SelectCharacter` ou não) e `isPlayable` (fica clicável/escolhível pra batalhar ou só travado/cinza) — antes só o `SelectedProfileHolder.currentProfile` era clicável, beco sem saída pra escolher qualquer personagem novo pela UI; `CharacterSelectController.PopulateCharacterGrid` reescrito pra usar as duas flags, com guarda contra referência `null` órfã na lista. Animações reaproveitadas (Block/Catch Weapon/Slashing Dagger, compartilhadas da Assassin Guy) ganharam retargeting automático por delta de pose de repouso (`RetargetPositionCurves`) — a numeração interna de bone do Spriter não é portável entre pacotes CraftPix diferentes (confirmado: mesmo `bone_XXX` é o braço num personagem e a perna em outro), então aplicar uma animação de outro corpo sem isso desloca partes pro lugar errado (achado real: cabeça "descolando", perna sumindo). Archer (arqueira, sem golpe corpo-a-corpo no pacote) resolvida à parte — `Slashing` dela é uma cópia do próprio "Throwing" (mesmo rig, sem risco). Detalhe completo dos problemas encontrados e como evitá-los da próxima vez: `CHARACTER_IMPORT_CHECKLIST.md` (novo, raiz do projeto) — inclui o achado de que copiar pastas "Vector Parts" de variantes numeradas (ex. Magician_Girl_1/2/3) por fora do Unity duplica GUID e corrompe os assets (6 personagens tiveram que ser apagados por causa disso).

- 2026-07-10: Limpeza dos órfãos apontados em ASSET_SIZE_REPORT.md — removidos `Medieval Warrior/Medieval Warrior.prefab` + `.controller` (duplicatas soltas na raiz da pasta, fora de `Prefab/`) e `Animations.scml` (fonte do Spriter, sem referência em runtime); ~1.27 MB liberados. `Prefab/Medieval Warrior - Copy.prefab` foi **mantido** — checagem de GUID antes de apagar revelou que ele embute um `AnimationClip` de fato usado (`m_Motion`) tanto pelo `Medieval Warrior.controller` quanto pelo `Medieval Warrior Girl.controller` em uso; a auditoria original tinha marcado errado esse arquivo como órfão (só checou se o prefab em si era referenciado, não os sub-assets embutidos nele).

- 2026-07-10: Levantamento de espaço em disco por personagem — `ASSET_SIZE_REPORT.md` novo (raiz do projeto): cada personagem custa ~2.3-2.9 MB (média 2.45 MB), enquanto `Assets/Resources/BattleGround` (50+ backgrounds de arena) sozinho pesa 497 MB em disco (~55-120 MB estimados já comprimidos pro mobile) — o real candidato a Addressables/download sob demanda é essa pasta, não o roster de personagens. Também achado ~2.5 MB de prefab/controller órfãos (`Medieval Warrior - Copy` + duplicata solta) e `Animations.scml` não usado em runtime, ainda não removidos (só auditoria, nenhum arquivo alterado).

- 2026-07-10: Grid de `02_SelectCharacter` bem maior (pedido do usuário) — `Scroll View` (RightPanel) redimensionado pra 1834×964, Pos X=40/Y=-21 (`CharacterSelectController.GridScrollWidth/ScrollHeight/GridLeftMargin/GridPosY`); `CharacterCardUI` recalibrado de 170×246 pra 600×310 (portrait 540×200, não mais quadrado) pra caber exatamente 3 colunas × 3 linhas visíveis de uma vez (spacing 17) — continua `FixedRowCount=3`/scroll horizontal, 4ª coluna em diante revela rolando pra direita conforme mais personagens forem cadastrados em `CharacterDatabase.unlockedCharacters`.

- 2026-07-09: `PlayerProfile` ganhou o campo `splashArt` (Sprite, opcional) — arrastar manualmente no Inspector conforme cada arte de personagem for gerada (pedido do usuário: "algo pra eu colocar na mão"). `02_SelectCharacter` (`CharacterSelectController.UpdateFrameArt`, chamado em `OnCharacterSelected`) preenche o `Frame` de tela cheia (`SelectionOverlay > Frame`) com essa splash art (`Image.Type.Simple`, `preserveAspect=false` — estica pra cobrir o frame inteiro) quando o personagem selecionado tiver uma; sem `splashArt`, mantém o retângulo dourado placeholder de sempre. `Medieval Warrior.asset` já vem com `splashArt` apontando pra `Assets/Personagens/SplashArt/MedievalWarrior.jpg` (já importada como Sprite); os outros 2 profiles (`Assasin Guy`, `Medieval Warrior Girl`) ficam com o placeholder até o usuário gerar a arte de cada um e arrastar no campo.

- 2026-07-09: Levantamento de ícones/imagens de botão pendentes pra gerar no Leonardo AI — `ICONS_NEEDED.md` novo (raiz do projeto), cobrindo `01_MainMenu`/`02_SelectCharacter`/`04_CombatScenePVP`/`05_SelectOpponent` e os popups/HUDs construídos via código. Só levantamento, nenhum código/cena alterado.

- 2026-07-08: `SelectionOverlay` de `02_SelectCharacter` reestruturado (pedido do usuário): moldura dourada da DIREITA (ao redor do `CharacterPanel`) removida por completo; moldura da ESQUERDA (antes só ao redor do portrait) agora cobre a TELA INTEIRA — placeholder até o usuário substituí-la por uma imagem gerada no Leonardo AI. `Portrait` (RawImage do preview do personagem) ganhou Rect Transform próprio e independente, copiado 1:1 dos valores que o usuário ajustou manualmente no Inspector (`PosX=220.47, Width=440.95, Top=549.91, Bottom=-108.01`) — resulta num retângulo de ~441×444px encostado no canto inferior esquerdo da tela (canvas 1920×1080). **Dimensão recomendada pra imagem do frame: 1920×1080** (a moldura é o canvas inteiro agora); a "janela" onde o personagem de verdade aparece fica em `X:[0,441] Y:[0,444]` (origem inferior-esquerda). Também corrigido "está cortando a cabeça dele": `FitPreviewCharacter()` novo centraliza o personagem no eixo Y da câmera de preview e recalcula o `orthographicSize` a partir dos bounds reais dos `Renderer`s (com folga), substituindo os antigos `PreviewOrthographicSize`/`PreviewCharacterGroundY` fixos (nunca calibrados de verdade, cortavam a cabeça de personagens mais altos).

- 2026-07-08: Mesma reação de clique (Hurt/Slashing) estendida pro personagem central do `01_MainMenu` (pedido do usuário: "faça a mesma coisa"). Extraída a lógica compartilhada (antes só dentro de `CharacterSelectController`) pro novo componente `CharacterPreviewReaction` — usado nos dois lugares. `MainMenuCharacterPreview` não destrói mais `AnimationController` do personagem instanciado (só `PlayerCombat`/`WeaponHandler`/`MovementController` continuam sendo removidos); clique detectado via `BoxCollider2D` (dimensionado a partir dos bounds reais dos `Renderer`s do personagem) + `OnMouseDown` — diferente do portrait de `02_SelectCharacter` (clique via `Button` de UI sobre a `RenderTexture`), já que aqui o personagem é world-space de verdade, visível diretamente pela Main Camera (01_MainMenu não tem o problema de overlay `ScreenSpaceOverlay` que motivou a RenderTexture na outra tela). Também não testado rodando o jogo nesta sessão — calibrar visualmente se o collider ficar grande/pequeno demais ou a reação não disparar.

- 2026-07-08: Portrait de `02_SelectCharacter` trocado de ícone estático (`previewIcon`) pra um preview "ao vivo" do personagem — Idle contínuo, reage com Hurt ou Slashing (sorteio 50/50) ao clicar nele (pedido do usuário). Causa raiz da tentativa anterior de preview em world-space (revertida em uma sessão passada por "sorting/profundidade de câmera nunca validado") diagnosticada: `SelectionOverlay` é um Canvas `ScreenSpaceOverlay`, que SEMPRE desenha na frente de qualquer câmera de mundo, então um personagem instanciado normalmente na cena fica atrás do fundo sólido do overlay por definição, não é questão de sorting. Fix: personagem instanciado longe do resto da cena (`PreviewWorldPos`) e filmado por uma câmera dedicada (fundo transparente) numa `RenderTexture`, exibida num `RawImage` dentro do Canvas normal — vira conteúdo de UI de verdade, sem depender de nenhum truque de profundidade. `AnimationController.PlaySlash(duration)` novo (dispara `Slashing` e sai via o mesmo toggle de `JumpStart` já usado pra escapar desse estado fora de combate). Ver **Portrait Preview (02_SelectCharacter)** no CLAUDE.md. Ainda não testado rodando o jogo (sem acesso ao Editor nesta sessão) — calibrar visualmente `PreviewOrthographicSize`/`PreviewCharacterGroundY` se o enquadramento não estiver bom.

- 2026-07-08: `02_SelectCharacter` agora chama `CharacterPanel.Expand()` em `OnCharacterSelected` — o painel abre direto no estado Expandido (Habilidades/Armas visíveis) ao clicar num personagem, em vez do Compact de sempre exigindo um clique extra dentro do painel. Chamado ANTES de `ShowSlideIn()` de propósito: `Expand()` usa `StopAllCoroutines()` internamente, que cortaria a animação de slide-in pela metade se rodasse depois dela. `Expand()` é idempotente, então reabrir com outro personagem depois não reinicia a animação de crossfade à toa.

- 2026-07-08: `CharacterPanel` ganhou `anchorBottomOverride`/`anchorTopOverride` opcionais em `Setup` (default `null` = janela vertical calibrada de sempre do `01_MainMenu`, sem nenhum efeito lá) — `02_SelectCharacter` passa as mesmas frações do portrait (`PortraitAreaBottomFraction/TopFraction`), encolhendo o Root pra caber exatamente dentro da moldura dourada ao redor dele (antes vazava por cima, já que a janela padrão — `RootAnchorBottom/Top` — é mais alta que a moldura). Como as duas passam a usar a mesma janela, o painel fica automaticamente do tamanho certo e centralizado dentro da moldura, sem precisar de nenhum ajuste de posição à parte. Também puxada a área do portrait bem mais perto do `CharacterPanel` (`PortraitAreaLeft/Right` 120–980 → 420–1300, mesmo tamanho de caixa só deslocada) — vão até o painel caiu de ~465px pra ~145px.

- 2026-07-08: Moldura dourada direita de `02_SelectCharacter` (a que fica atrás do `CharacterPanel`) realinhada verticalmente com a moldura do portrait à esquerda — ambas agora usam as mesmas frações `PortraitAreaBottomFraction/TopFraction` (antes a da direita usava `CharacterPanel.RootAnchorBottom/Top`, uma janela vertical diferente e mais alta). Só a moldura (o retângulo dourado desenhado atrás do painel) mudou — `CharacterPanel.Root` continua na mesma posição/altura de sempre.

- 2026-07-08: Causa raiz REAL (e corrigida) do `CharacterPanel` aparecendo pequeno/fora do lugar em `02_SelectCharacter` — não era timing de Canvas (1ª tentativa de fix, `Canvas.ForceUpdateCanvases()`, confirmada pelo usuário como sem efeito nenhum mesmo reiniciando o Play mode; revertida). **Causa real**: `CharacterPanel` cria seu PRÓPRIO Canvas filho (`ScreenSpaceOverlay`, `CanvasScaler` 1920×1080) dentro de si mesmo, e depende desse Canvas ser RAIZ (sem nenhum Canvas ancestral) pra `renderMode`/`CanvasScaler` funcionarem — limitação documentada do Unity: um Canvas aninhado sob outro Canvas ignora seu próprio `renderMode` e não aplica seu próprio `CanvasScaler` (herda tudo do ancestral), fazendo o `Root` cair no RectTransform padrão de 100×100 em vez de tela cheia. `CharacterSelectController.BuildDetailUI()` criava o GameObject do `CharacterPanel` e imediatamente o `SetParent`ava sob o Canvas principal da cena — exatamente essa armadilha. `MainMenuController.Start()` nunca teve esse bug porque lá o GameObject é criado **sem chamar `SetParent` nenhuma vez** (fica raiz da cena, Canvas interno genuinamente raiz). **Fix**: removida a chamada `panelGo.transform.SetParent(canvasTransform, false)` — o GameObject do `CharacterPanel` agora fica raiz da cena em `02_SelectCharacter` também, igual ao `01_MainMenu`. Documentado esse requisito direto no `CharacterPanel.BuildUI()` pra não se repetir. `Canvas.ForceUpdateCanvases()` da tentativa anterior foi revertido por não ter relação com o bug de verdade (`HideOffsetX` continua usando a constante `PanelWidth` em vez de `_rootRt.rect.width` — simplificação inofensiva, mantida).

- 2026-07-08: Ajustes finos de layout/estilo em `02_SelectCharacter` (feedback visual do usuário depois da 2ª correção do overlay). **Portrait afastado do painel**: área do personagem puxada bem pra esquerda (`PortraitAreaLeft/Right`, 120–980px, fixos e independentes das constantes do grid já que ele fica escondido atrás do overlay) — antes ficava a só 40px do `CharacterPanel` (dava a impressão de sobreposição); agora sobra ~465px de vão até o painel na lateral direita. **Botões "Fechar"/"Selecionar" mais baixos**: antes esticavam a janela vertical inteira até `CharacterPanel.RootAnchorBottom` (~270px de altura, "muito gordo verticalmente" no relato do usuário) — agora altura fixa de 56px, ancorados perto da base da tela (`ActionButtonHeight`/`ActionButtonBottomMargin`). Botão "Voltar" fixo também reduzido (64px → 44px de altura). **Moldura dourada** (`BuildFrameBorder`, `UITheme.currencyGold`) adicionada tanto ao redor do `CharacterPanel` quanto do portrait — um retângulo arredondado ligeiramente maior desenhado atrás de cada um, só a borda "vazando" pra fora, dando aos dois um contorno visível em vez do fundo marrom liso de antes. Não mexe em `SelectedProfileHolder`/lógica de seleção.

- 2026-07-08: 2ª correção do overlay de `02_SelectCharacter` — cobertura total do fundo, painel de stats restaurado ao lado do portrait, e novo botão "Fechar". **Causa real da tira bege perto do botão "Selecionar"**: o `SelectionOverlay` só cobria de x=0 até o início do `CharacterPanel` (não a tela inteira), e além disso era construído DEPOIS de `CharacterPanel`/dos botões — mesmo se cobrisse a área toda, teria desenhado por cima deles, escondendo-os (GameObjects novos são sempre anexados como último filho do Canvas compartilhado = desenham por cima dos anteriores). Corrigido nos dois pontos: `SelectionOverlay` agora cobre a **tela inteira** (x=0 até a borda direita, altura inteira) e é construído **antes** de `CharacterPanel`/"Fechar"/"Selecionar"/"Voltar" em `Start()`, garantindo que fique sempre atrás deles. O painel de stats (`CharacterPanel`, nome/HP/STR-AGI-SPD/Level-XP) não foi alterado — usa seu próprio Canvas `ScreenSpaceOverlay` (sempre à frente de tudo, independente de ordem no Canvas comum) e continua ancorado na faixa lateral direita, ao lado do portrait grande dentro da mesma área marrom. **Novo botão "Fechar"**: ao lado de "Selecionar" (dividem a faixa horizontal ao meio, mesmo espaço de antes), fecha a visualização expandida chamando `CharacterPanel.HideSlideOut()` (desliza o painel de volta pra fora da tela) e esconde overlay/portrait/botões só depois do slide terminar (mesma duração, 0.3s) — revela o grid de novo sem sair da cena, diferente do "Voltar" fixo do canto superior esquerdo (esse continua indo direto pro `01_MainMenu`). Não mexe em `SelectedProfileHolder`/lógica de seleção.

- 2026-07-08: Corrigido o fundo do overlay do painel expandido de `02_SelectCharacter` + portrait do personagem selecionado. O `CharacterPanel` (faixa lateral de 450px) já tinha fundo sólido, mas nada cobria o resto da coluna do grid — o painel ficava "flutuando" na lateral direita com os cards ainda visíveis atrás dele. Novo `SelectionOverlay` (Image sólida, `UITheme.panelBackgroundAlt`, sem sprite/cantos arredondados) cobre toda a área selecionável (de x=0 até o início do `CharacterPanel`), escondendo o grid por completo enquanto um personagem está selecionado — escondido por padrão, ativado junto do painel em `OnCharacterSelected`. Substituído o preview em world-space (prefab instanciado, dependia de sorting/profundidade de câmera nunca validado) por um `Image` simples dentro do próprio overlay usando `PlayerProfile.previewIcon` (mesmo sprite já usado nos cards do grid) — mais previsível e garantidamente visível. Botão "Voltar" fixo reconstruído por último na hierarquia pra continuar clicável por cima do overlay; "Selecionar" mantido na mesma posição de antes, junto do `CharacterPanel`.

- 2026-07-08: Painel de detalhe de `02_SelectCharacter` reconstruído do zero e novo botão "Voltar" fixo. **Causa raiz do painel quebrado (transparente/colapsado/nome quebrando letra por letra)**: `CharacterPanel` ganhou no dia anterior um modo `fillFromXFraction` (esticar a largura a partir de uma fração de tela, em vez do card fixo de 450px do `01_MainMenu`) nunca validado de verdade no Editor — removido por completo; o painel volta a usar a MESMA largura fixa (`PanelWidth=450` + `EdgeMargin=25`, ancorado à direita) já comprovada no menu principal, agora exposta como consts públicas (`CharacterPanel.PanelWidth/EdgeMargin/RootAnchorBottom`) pra `CharacterSelectController` alinhar seus próprios botões sem duplicar números mágicos. Também corrigido um bug real de dados: `CharacterPanel.RefreshAll` sempre lia `SelectedProfileHolder.currentProfile` (o personagem EQUIPADO), nunca o personagem clicado no grid — novo método `CharacterPanel.SetProfile(profile)` chamado em `OnCharacterSelected` faz o painel mostrar de fato o card clicado. **Botão "Voltar" fixo (novo)**: canto superior esquerdo, `UITheme.secondaryButton`, visível desde a entrada na cena independente de seleção — substitui o antigo botão "Voltar" (que só escondia o painel sem sair da cena; hoje esse era o único jeito de sair de `02_SelectCharacter` sem escolher personagem). **Layout novo**: grid encolhido pra ~3 colunas visíveis (`GridScrollWidth` 1100→560px) e um preview grande do personagem (prefab instanciado igual a `MainMenuCharacterPreview`, sem os componentes de combate) apareceu na área central, entre o grid e o painel; painel de detalhe + botão "Selecionar" continuam juntos na lateral direita.

- 2026-07-09: Ajustes finais de `02_SelectCharacter`. **Fundo**: o tint próprio do `RightPanel`/`gridPanel` (herdado da cena, cor diferente do resto) foi zerado (alpha 0) — agora só o gradiente `UITheme.backgroundTop/backgroundBottom` aparece, uniforme em toda a tela, sem coluna "colada" por cima. **Scroll vertical removido**: `ScrollRect.vertical=false` já impedia rolar, mas a barra vertical em si (herdada da cena) ainda ficava visível sem função — desconectada (`verticalScrollbar = null`) e desativada; só a horizontal continua funcionando. **Painel de detalhe escondido de verdade**: além de posicionado fora da tela, `CharacterPanel` agora desativa o próprio `Canvas` inteiro (`GameObject.SetActive(false)`) enquanto oculto — `ShowSlideIn()`/`HideSlideOut()` reativam/desativam o Canvas no início/fim da animação, então não há mais chance de aparecer sozinho ao carregar a cena por alguma nuance de posicionamento. **Layout final**: grid ancorado de vez na lateral esquerda (antes ficava centralizado na tela cheia); `CharacterPanel` ganhou o parâmetro `fillFromXFraction` — em vez do card fixo de 450px do `01_MainMenu`, estica e preenche **todo** o espaço à direita do grid (fundo sólido `panelBackgroundAlt`, já opaco, cobrindo a área inteira). Botão "Selecionar" ficou junto do painel na lateral direita; "Voltar" passou a ficar na lateral esquerda, centralizado logo abaixo do grid — antes os dois ficavam empilhados juntos à direita.

- 2026-07-08: Limpeza definitiva de `02_SelectCharacter` + painel de detalhe reaproveitado do `01_MainMenu`. Removidos por completo os resíduos do bug de layout antigo: `LeftPanel` (coluna cinza/creme à esquerda, com um Medieval Warrior duplicado instanciado via `SpawnPoint` — puro resto de um preview nunca usado de verdade — e os textos fantasma "Lv. 2"/"0%"/"3/6"/barra branca dos labels de Level/Winrate/Xp/Jogos, nunca estilizados) e o antigo `DetailPanel` (abas Habilidades/Armas mortas, botões com `onClick` quebrado) foram **desativados por completo** (`m_IsActive: 0`); `CharacterSelectController` reescrito do zero, sem nenhuma referência a esses campos. `RightPanel` (grid) agora ocupa a tela inteira — antes sobrava uma faixa vazia reservada à esquerda pro `LeftPanel`. `CanvasScaler` da cena corrigido de `Constant Pixel Size`/800×600 (destoava do resto do projeto — provável causa raiz de parte da bagunça) pra `Scale With Screen Size`/1920×1080. Grid agora com `GridLayoutGroup.Constraint.FixedRowCount=3` (exatamente 3 personagens visíveis verticalmente) e `ScrollRect` horizontal em vez de vertical — revela novas colunas à direita conforme o `CharacterDatabase` cresce; cards ordenados com o habilitado primeiro. O painel de detalhe deixou de ser um conjunto de elementos ad-hoc da cena e passou a ser uma instância real do **mesmo `CharacterPanel` usado no `01_MainMenu`** — a classe ganhou os parâmetros opcionais `showLevelXp`/`startHidden` (default `false`, sem efeito nenhum no uso existente do menu) e os métodos `ShowSlideIn()`/`HideSlideOut()` (anima `Root.anchoredPosition` de/pra fora da tela); em `02_SelectCharacter` é instanciado já escondido e desliza a partir da direita ao clicar num card habilitado, com Level+barra de XP embutidos (campo que o painel nunca teve, já que no menu principal o XP fica acima da cabeça do personagem). Botões "Voltar"/"Selecionar" reconstruídos via código (mesmas cores `secondaryButton`/`primaryAction`), ancorados no espaço livre abaixo do painel, só aparecem junto dele.

- 2026-07-08: Removida por completo a `PrefabInstance` órfã (`fileID 748239791`, nome original "GridCharacters") do prefab perdido `0b51182c70804a94e897b3daa31618c8` que ainda sentava dentro do `ScrollRect` de `02_SelectCharacter` — Unity acusava "Missing Prefab Asset" ao abrir a cena. Numa passada anterior essa instância tinha sido deixada de propósito (só desconectada do C#, sem editar sua estrutura interna, por precaução), mas o próprio Editor sinalizou o problema; removida agora junto dos dois objetos "stripped" associados (`748239792`/`811281549`+`811281555`) e da referência órfã na lista de filhos do `ScrollRect`.

- 2026-07-08: Grid de `02_SelectCharacter` redesenhado no estilo "grid de heróis" do Brawl Stars: `CharacterCardUI` reescrito de novo (portrait grande com fundo colorido por personagem — cor tirada de tokens do `UITheme`, escolhida deterministicamente pelo nome —, nome sobreposto numa faixa escura na base do portrait, Level+barra de XP compacta abaixo; HP e STR/AGI/SPD do redesign anterior removidos, fora do escopo do visual de referência). `GridLayoutGroup` passou a ler `CharacterCardUI.CardWidth/CardHeight` direto, sem número duplicado, então o grid cresce/quebra linha sozinho conforme `CharacterDatabase.unlockedCharacters` ganha personagens. Card fica "cheio"/clicável só se o `PlayerProfile` for o mesmo do `SelectedProfileHolder.currentProfile` atual (não mais hardcoded pra Medieval Warrior); qualquer outro aparece com portrait/nome/fundo dessaturados (tint via código, sem shader) e **sem `Button` nenhum** — sem reação a clique de propósito. `Medieval Warrior Girl` adicionada a `CharacterDatabase.unlockedCharacters` (só pra aparecer no grid, nesse estado bloqueado) — continua sem tocar no hardcode dela como Player2 de `04_CombatScenePVP`. Limpeza final da `DetailPanel`/`LeftPanel`: removidos de vez os labels mortos `TxtVida`/`TxtForca`/`TxtAgi`/`TxtVelocidade` e o botão "Voltar" duplicado/quebrado (label "Sair", `onClick` apontava pra uma classe `CharacterSelectUI` que não existe mais, `m_Target` nulo); `ScrollHabilidades`/`ScrollArmas` (abas mortas de uma versão antiga do painel, cada uma com seu próprio botão de `onClick` quebrado) desativadas em vez de deletadas por completo (hierarquia de Viewport/Scrollbar/Content aninhada não totalmente mapeada — seguro terminar de deletar pelo Editor). No `01_MainMenu`, botão "Herois" renomeado pra "GUERREIROS" e recentralizado (`anchoredPosition.x` de `-160` pra `0`) pra ficar sozinho, centralizado, abaixo do personagem central — antes dividia a fileira com o Shop (já removido)

- 2026-07-08: Restilização completa de `02_SelectCharacter` com o sistema visual do `UITheme` (mesmo já validado em `01_MainMenu`): fundo com gradiente `backgroundTop`/`backgroundBottom` (primeiro uso real desses dois campos no projeto, via novo `UIShapeUtil.VerticalGradient`), `RightPanel`/`DetailPanel` retintados de cinza translúcido padrão Unity pra `panelBackground`/`panelBackgroundAlt`, e botões "Voltar"/"Selecionar" com `secondaryButton`/`primaryAction` + fonte Luckiest Guy (autosize pra evitar o corte "Se..." que o texto "Selecionar" sofria com a fonte/tamanho antigos). `CharacterCardUI` foi reescrito do zero pra montar todo o card via código (nome, Level+barra de XP no estilo de `MainMenuCharacterPreview.BuildLevelXpHud`, HP em texto, STR/AGI/SPD via `AttributePipBar` — mesmo componente do `CharacterPanel`) porque o prefab que ele dependia (`Assets/Prefabs/CharacterCards/CharacterCard`) estava **perdido do projeto** — a referência na cena apontava pra um guid sem asset correspondente nenhum, então o grid de personagens não tinha como funcionar antes desta mudança. Descobertos e corrigidos de tabela dois bugs estruturais pré-existentes na cena que impediam qualquer card de aparecer: o `gridContent` da `CharacterSelectController` apontava pra essa mesma instância de prefab quebrada (não pro `Content` real do `ScrollRect`) e o `Viewport` do scroll tinha `m_AnchorMax` zerado (`{0,0}` em vez de `{1,1}`), resultando numa área de máscara de tamanho zero. `GridLayoutGroup`/`ContentSizeFitter` do `Content` passaram a ser adicionados em código (`CharacterSelectController.EnsureGridLayout`), já que não existiam na cena. Removido também o botão "Shop" de `01_MainMenu` (`BtnShop`, onClick apontava pra `OnPlayButton` — placeholder sem função própria)

- 2026-07-07: `BtnJogar` de `01_MainMenu` puxado pra mais perto do canto inferior direito (`m_AnchoredPosition` de `{x: -60, y: 60}` pra `{x: -25, y: 33}`, mesmo `sizeDelta`/âncora) e `CharacterPanel.EdgeMargin` alinhado ao novo inset horizontal do botão (`11f` → `25f`) — antes a margem direita do painel de status era um valor arbitrário sem relação com o botão; agora a borda direita do painel fica exatamente alinhada com a borda direita do "Jogar"

- 2026-07-07: Personagem central de `01_MainMenu` recentralizado no meio absoluto da tela (`MainMenuCharacterPreview.CharacterCenterX = 0f`, era `-1.76f` — decisão anterior de deslocar pra esquerda do `CharacterPanel` revertida a pedido do usuário) e reposicionado mais alto (`CharacterGroundY = -1f`, era `-2f`). A barra de Level/XP acima da cabeça agora acompanha `CharacterGroundY` dinamicamente em vez de usar uma fração de tela fixa (`yFraction` derivado de um par de calibração `CalibratedGroundY`/`CalibratedYFraction`) — qualquer ajuste futuro na altura do personagem move a barra junto, proporcionalmente, sem precisar recalibrar manualmente

- 2026-07-07: Corrigida `NullReferenceException` (dentro de `TMPro.MaterialReference..ctor`) ao clicar em qualquer ícone de skill, causada pela correção anterior de altura dinâmica do popup: `ShowSkillDetail` chamava `TMP_Text.GetPreferredValues` nos textos recém-criados **antes** de `_popupOverlayGo.SetActive(true)` (só executado no fim do método) — um `GameObject` desativado na hierarquia nunca roda `Awake()` dos componentes recém-adicionados, então o `fontAsset`/material interno do TMP_Text ainda não tinha sido inicializado quando a medição forçava o parse do texto. Corrigido movendo `_popupOverlayGo.SetActive(true)` pro início do método, antes de criar/medir qualquer texto novo

- 2026-07-07: Corrigidos dois bugs reportados pelo usuário no popup de detalhe de skill logo após o teste da entrega anterior. (1) Descrições longas (Shield, Lead Skeleton, Deity etc.) vazavam pra fora da caixa do popup — a altura do painel era fixa; trocada por medição real do texto (`TMP_Text.GetPreferredValues`, mesma largura que o texto ocupa de fato) tanto pra descrição quanto pro efeito, então o painel sempre cresce o suficiente pra caber os dois sem cortar. (2) Espaçamento gigante entre o label "Efeito" e o valor — mesmo bug real já visto antes no popup de arma (`VerticalLayoutGroup` com `childControlHeight=false` prendendo cada linha na altura padrão de 100px em vez da `preferredHeight` configurada); corrigido removendo layout automático do popup de skill por completo, com posicionamento manual (`anchoredPosition`/`sizeDelta` calculados em código)

- 2026-07-07: Aplicadas as descrições finais das 53 skills (`skills_descricoes_v2.md`) aos ScriptableObjects: `SkillData.description` passou a guardar o texto temático/engraçado de cada skill (era o texto mecânico antes) e um novo campo `SkillData.effectText` guarda o "Efeito:" no formato de colchetes `[T1/T2/T3]` — nenhum dos dois menciona "tier"/"T1"/"T2"/"T3" em texto. Popup de detalhe de skill (`CharacterPanel.ShowSkillDetail`, mesmo componente visual do popup de arma) ganhou uma linha "Efeito" abaixo da descrição, destacando o valor do tier equipado (`HighlightEffectTiers`, mesma cor de destaque já usada no popup de arma) — omitida pra Garimpeiro/Magneto (ainda não implementadas, `effectText` vazio de propósito). `SkillTierGenerator.CopyLiteral` corrigido pra copiar `description`/`effectText` verbatim de T1 pra T2/T3 (antes concatenava `" (T{tier})"` na description, bug real). Requer rodar `Tools → AutoArms → Generate Skill Assets` + `Populate Skill T1 Bonus Values` + `Generate Skill Tiers (T2 & T3)` + `Rebuild Skill Database From Folder` no Editor pra aplicar aos `.asset` existentes

- 2026-07-07: Substituída a paleta cíclica de 4 cores fixas da barra de pips de STR/AGI/SPD por um sistema de 40 tiers gerados via fórmula HSL (`AttributePipBar.GetColorForTier`/`TierPalette`, 4 "eras" de 10 matizes cada, cobrindo os valores 1-400 com uma cor distinta a cada 10 pontos, calculada uma única vez e cacheada numa tabela). Acima de 400 (tier ≥ 40), a paleta recicla (`tier % 40`) e um indicador de prestígio aparece junto do badge: borda dourada pulsante sempre que houver ao menos uma volta completa, mais o texto "×N" a partir da 2ª volta. A janela deslizante de 10 blocos continua igual, só a fonte da cor de cada tier mudou

- 2026-07-07: Corrigida a ordem dos blocos na janela deslizante da barra de pips de STR/AGI/SPD (`AttributePipBar.ComputeSlidingWindowColors`) — estava invertida: agora, quando a janela está cheia (V≥10), o bloco mais à ESQUERDA representa o ponto mais ALTO/recente do valor (V) e o mais à DIREITA o ponto mais baixo da janela (V-9), então o tier mais alto (ex: vermelho) preenche a partir da esquerda, empurrando o tier anterior (amarelo) pra direita conforme o valor sobe. Caso V<10 não mudou (ordem ascendente da esquerda pra direita, blocos vazios à direita, como já estava certo)

- 2026-07-07: Corrigida a lógica de cor das barras de pips de STR/AGI/SPD — era "reset por tier" (a barra zerava visualmente a cada múltiplo de 10), virou **janela deslizante** de exatamente 10 blocos representando os últimos 10 pontos do valor atual (início = max(1, V-9), fim = V), cada bloco colorido pelo tier do PONTO que ele representa, não pelo tier do valor inteiro — um valor numa transição de tier (ex: 12, 24) agora mostra a barra com dois tons ao mesmo tempo, misturando as cores dos dois tiers proporcionalmente. Implementado como função pura (`AttributePipBar.ComputeSlidingWindowColors`, recebe o valor e devolve as 10 cores), usada igualmente por STR/AGI/SPD

- 2026-07-07: 2ª investigação da diferença de tonalidade relatada entre os pips de STR/AGI/SPD (persistia mesmo após a centralização anterior da lógica de cor por tier). Auditoria completa não encontrou nenhuma divergência possível no código — `AttributePipBar.TierColorFor` já era um único método estático, lido da mesma referência de `UITheme` pelos três atributos, e a tintagem (`Image.color` multiplicando uma textura branca) é matematicamente exata. Como reforço definitivo, `UIShapeUtil.RoundedRect` passou a cachear sprites por (cor, raio) — STR/AGI/SPD agora reutilizam literalmente o mesmo objeto `Sprite`, não só valores "iguais", eliminando qualquer dúvida teórica remanescente. Suspeita mais provável pro que foi visto: diferença na quantidade de pips preenchidos entre atributos (densidade de área colorida), não um bug de cor — recomendado validar com os três atributos no mesmo valor

- 2026-07-07: Três retoques no popup de detalhe de arma do `CharacterPanel` (`01_MainMenu`), depois de confirmado o fix do espaçamento. Spacing entre linhas aumentado de volta pra 3px (o fix anterior permitiu isso sem reintroduzir o vão gigante de antes). Linhas de bônus condicionais (Evasion, Dexterity, etc.) passaram a usar a mesma coluna label/valor de Types/Odds/Damage/etc — antes eram uma única string solta e ficavam desalinhadas do resto da lista. `Reach` removido da lista de atributos sempre visíveis — não deve mais aparecer no popup

- 2026-07-07: Achada e corrigida a causa real do espaçamento excessivo entre linhas do popup de detalhe de arma (`CharacterPanel`, `01_MainMenu`), que persistia mesmo depois de reduzir os valores de altura/spacing em rodadas anteriores: a `VerticalLayoutGroup` da lista de stats estava com `childControlHeight` desligado, então o Unity posicionava cada linha usando a altura configurada (24px) mas nunca redimensionava a linha de fato pra esse valor — cada uma ficava presa na altura padrão de 100px, criando um vão visual enorme até a próxima. Corrigido ligando `childControlHeight`. Aproveitado o ajuste pra também dar mais respiro vertical ao popup (padding extra) e subir o popup ~40px na tela, a pedido do usuário

- 2026-07-07: Ajustes finos no popup de detalhe de arma do `CharacterPanel` (`01_MainMenu`), depois do feedback sobre a versão anterior. Removido o `ScrollRect` interno (o usuário achou o espaçamento resultante grande demais e preferiu sem rolagem) — o popup agora só cresce em largura/altura o quanto for preciso pra caber todos os atributos exibidos, sem cortar nada. Espaçamento entre linhas reduzido bem mais (mais compacto, fácil de ler de uma vez). Bônus que variam por tier na mesma família de arma (ex: Evasion, Dexterity, Combo, Crit Chance) agora mostram o mesmo formato de colchetes `[T1/T2/T3]` já usado em Damage/Draw Chance, com o tier atual destacado — a decisão de mostrar colchetes ou um valor simples é feita dinamicamente comparando os 3 tiers de cada arma, não fixada por campo. Corrigido em definitivo o texto "T1"/"T2"/"T3" no título: a causa raiz era o próprio `weaponName` do asset já vir com o sufixo embutido (ex: "Knife T1"), não algo concatenado pelo código — agora é removido do texto exibido (`StripTierSuffix`), mantendo a indicação de tier só na borda colorida do ícone

- 2026-07-07: Popup de detalhe de arma do `CharacterPanel` (`01_MainMenu`) agora exibe todos os campos configuráveis de `WeaponData`: Types/Odds/Hit Speed/Damage/Draw Chance/Reach sempre aparecem (mesmo com valor 0), enquanto Crit Chance/Evasion/Dexterity/Reversal/Block/Accuracy/Disarm/Combo/Deflect/Counter só aparecem quando != 0 pra aquela arma, cada um como "+X% Nome" (verde) ou "-X% Nome" (vermelho, penalidade); `critDamageMultiplier` nunca é exibido. Corrigido também o overflow que fazia linhas (Draw Chance, Reach) vazarem pra fora da caixa do popup e sobrepor os botões Shop/Personagem embaixo: a lista de stats agora fica numa área rolável (reaproveitando o mesmo `ScrollRect` já usado na lista de Skills/Armas) e a altura do popup é calculada dinamicamente pela quantidade real de linhas de cada arma, com um teto que preserva a rolagem em vez de deixar o painel crescer demais

- 2026-07-07: Passe de legibilidade no popup de detalhe de skill/arma do `CharacterPanel` (`01_MainMenu`). Fontes de título/labels/valores aumentadas e o espaçamento vertical entre linhas de stat reduzido (popup mais compacto e legível). Removida a linha divisória dourada abaixo do nome. Removido de vez o texto "T1"/"T2"/"T3" do popup (inclusive o sufixo no título, ex: "Knife T1" → "Knife") — substituído por uma borda colorida (bronze/prata/ouro) ao redor do ícone no topo, reaproveitando o mesmo componente das células de HABILIDADES/ARMAS (`BuildTierIconCell`) em vez da estrela de raridade solta que existia antes; skills também ganharam esse ícone com borda (não tinham nenhum antes). Corrigido também um bug real de formatação: o traço usado como placeholder pra tiers ausentes (armas legadas sem `nextTier`) usava o caractere travessão "—", que aparecia como glyph quebrado/ausente na fonte do popup — trocado por um hífen simples "-" em todos os campos `[T1/T2/T3]` (Damage, Draw Chance, Crit Bonus) e na linha de Types

- 2026-07-07: Três ajustes no `CharacterPanel` de `01_MainMenu`. (1) Removida a faixa horizontal dourada (placeholder de layout sem função) que sobrava logo abaixo das grades de ícones de HABILIDADES e de ARMAS. (2) Centralizada a lógica de cor por tier dos pips de STR/AGI/SPD num único método estático (`AttributePipBar.TierColorFor`) — já era compartilhada entre os três atributos, mas a extração deixa essa garantia explícita e impossível de quebrar por engano depois. (3) Popup de detalhe de arma redesenhado no estilo My Brute: ícone centralizado + estrela de raridade (cor por tier) + nome, Types coloridos por `WeaponType`, Odds/Hit Speed/Reach simples, Damage/Draw Chance/Crit Bonus no formato `[T1/T2/T3]` com o tier atual destacado (laranja pros dois primeiros, verde pro crítico) e os outros em cinza, Block (redução, cinza) só quando a arma tiver. Para isso, adicionado `WeaponData.nextTier` (contrário de `previousTier`, só exibição) — populado nos 26 conjuntos de armas existentes e mantido automaticamente pelo `WeaponTierGenerator` daqui pra frente

- 2026-07-07: Três refinamentos de UI em `01_MainMenu`. (1) Indicador de Level/XP acima do personagem central: fundo trocado de translúcido pra sólido (`panelBackgroundAlt`), texto `"atual/necessário"` centralizado dentro da própria barra, caixa e barra aumentadas (280×76px, barra ~38px de altura) sem invadir o personagem. (2) Seções HABILIDADES/ARMAS do `CharacterPanel`: nome ao lado do ícone removido, ícones agora numa grade (5 colunas) com borda colorida por tier (T1 bronze, T2 prata, T3 ouro — novos tokens `tierBronze`/`tierSilver`/`tierGold` no `UITheme`), aplicada em skills e armas (ambas já tinham campo `tier`). (3) Clicar num ícone de skill/arma abre um popup (fundo sólido, cantos arredondados, nome em destaque + descrição/status, fecha no X ou clicando fora) — skills mostram `SkillData.description` (campo que já existia e já vinha preenchido, não precisou criar nada); armas mostram dano/velocidade de ataque/tipo direto de `WeaponData`

- 2026-07-07: Painel lateral (`CharacterPanel`) de `01_MainMenu` ganhou margem direita de 11px (mesmo valor da margem que já existia no topo — antes ficava encostado na borda da tela, tanto no estado Compact quanto no Expanded, já que ambos compartilham o mesmo `Root`). Estado Expanded ganhou um botão "VER DETALHES" logo abaixo da seção ARMAS, que revela/esconde uma nova seção PASSIVAS — linhas simples `"Label: valor"` (sem ícone/barra) com todas as passivas do personagem que ainda não apareciam em nenhum lugar da UI do menu (Evasion, Counter, Reverse, Accuracy, Armor, Block, Reversal After Block, Critical Chance, Critical Damage, Combo Chance, Disarm Chance, Initiative, Hit Speed) — todos valores já existentes em `PlayerProfile`/`GetEffectiveStats()`, nenhum campo novo precisou ser criado

- 2026-07-07: Restaurada a expansão do `CharacterPanel` de `01_MainMenu` (tinha sido removida numa sessão anterior, decisão revertida pelo usuário) — clicar no painel compacto (nome, Win Rate, HP, STR/AGI/SPD em pips) expande no mesmo lugar, sem perder nada do conteúdo/fontes/posições já ajustados (reconstruído via `BuildInfoBlock` compartilhado entre os dois estados, pra não duplicar o desenho). Estado expandido ganhou duas seções novas — Habilidades e Armas equipadas, cada item como ícone+nome, em lista vertical rolável (sem abas — mais legível dado o tamanho de fonte já grande). Clicar fora dos itens recolhe de volta ao compacto. Root recalculado (0.28-0.99 de fração de tela) considerando as fontes maiores + as seções novas, mantendo margem de segurança acima do `BtnJogar`

- 2026-07-07: Aproximado o indicador de Level+XP acima do personagem central (estava com folga grande demais depois do ajuste anterior) — de `y=0.80` pra `y=0.71` de fração de tela, mais próximo da cabeça sem cobrir ou tocar o sprite

- 2026-07-07: Corrigida a causa raiz do indicador de Level+XP acima do personagem central sumir por completo (regressão da correção anterior, que só tinha adicionado um `if (theme == null) return` — mascarou o bug do retângulo branco transformando-o num desaparecimento silencioso, sem exception nenhuma). O campo `[SerializeField] UITheme theme` de `MainMenuCharacterPreview` resolvia nulo em runtime mesmo com o asset corretamente wireado em `01_MainMenu.unity` — removido de vez e substituído por `MainMenuController.Theme` (nova propriedade pública), buscado via `FindObjectOfType` — fonte comprovadamente confiável, já que o `CharacterPanel` depende dela e sempre renderizou certo. Indicador volta a aparecer sempre visível, com "Level X" + barra fina dourada proporcional ao XP, ~0.80 de fração de tela acima da cabeça do personagem

- 2026-07-07: Simplificação do `CharacterPanel` de `01_MainMenu` — removido de vez o painel expandido (abas Stats/Skills/Armas) e o toggle de clicar pra expandir/recolher; sobrou só o painel único e persistente (nome, Win Rate, HP em texto, STR/AGI/SPD em pips), sempre visível, sem interação nenhuma. Junto saiu o texto "XP: X/X" e a barra amarela de XP que ainda restavam ali por engano (deveriam ter sido removidos numa sessão anterior). Skills e armas equipadas (que ficavam nas abas removidas) ficaram sem tela própria — pendência registrada no CLAUDE.md. Painel aumentado de novo (320→450px de largura, agora ~230px de altura fixa) e fontes bem maiores em todo o conteúdo (nome, HP, labels e valores de STR/AGI/SPD) pra melhorar a leitura. Corrigido também o indicador de Level+XP acima do personagem central, que renderizava como um retângulo branco vazio (leitura de cores do UITheme espalhada pelo meio do método — se `theme` viesse nulo, um `Image` sem sprite ficava "pela metade" configurado; reescrito lendo tudo no início) e ficava baixo demais, cobrindo a cabeça (reposicionado de 0.64 pra 0.80 de fração de tela, bem mais alto)

- 2026-07-07: Refinamento do HUD/painel unificado de `01_MainMenu`. Personagem central recentralizado (`CharacterCenterX = -1.76`, considerando o espaço ocupado pelo `CharacterPanel` à direita, não o centro absoluto da tela) e ganhou uma barra de XP fina + "Level X" acima da cabeça (estilo My Brute, única barra de XP do menu agora). No `CharacterPanel`: estado Compact perdeu a barra de XP (realocada) e o badge de level virou Win Rate (placeholder simbólico — `profile.winRate` nunca é escrito em lugar nenhum do projeto ainda; time vai plugar num banco de dados/histórico de partidas depois); ganhou HP em texto puro (sem barra) e STR/AGI/SPD num novo componente reutilizável (`AttributePipBar`: badge circular com o valor + fileira de 10 pips, cor mudando a cada múltiplo de 10 pontos entre 4 tons do UITheme) — mesmo componente substituiu a barra contínua de STR/AGI/SPD na aba Stats do estado Expanded. Painéis (Compact e Expanded) aumentados — largura 320→380px (~19%), altura do Root ~15%, altura do Compact ~46% (precisou mais que o resto por causa das 3 linhas de pips novas) — mantendo a margem de segurança acima do `BtnJogar`

- 2026-07-07: Unificado o HUD do personagem central e o painel lateral "Personagem" de `01_MainMenu` num único componente (`CharacterPanel`), sempre visível no lado direito da tela com dois estados (Compact: nome+level+XP; Expanded: cabeçalho+abas Stats/Skills/Armas — mesmo conteúdo que o painel lateral já tinha), alternando por clique com crossfade de 0.18s. `MainMenuCharacterPreview` não constrói mais HUD nenhum. `MainMenuController.OnCharacterButton()` ficou sem ação (painel não depende mais do clique pra aparecer) e o `CharacterPanel` passou a ser instanciado eagerly em `MainMenuController.Start()`. Root do painel tem altura fixa ancorada com margem de segurança acima do `BtnJogar` (nunca sobrepõe o botão Jogar, mesmo expandido); clicar em qualquer parte do conteúdo expandido fora das abas recolhe de volta (bubbling de clique até o Button de fundo — arrastar pra rolar a lista de stats não conta como clique, não recolhe por engano)

- 2026-07-07: Redesenho do HUD do personagem central (`MainMenuCharacterPreview`) e do painel lateral "Personagem" (`CharacterPanel`) em `01_MainMenu`, todas as cores migradas pra `UITheme` (novo `UIShapeUtil.RoundedRect` gera sprites de canto arredondado em runtime, sem asset externo). Personagem central escalado a 0.82x (pés mantidos no lugar, root do prefab já fica nos pés) pra abrir espaço vertical. HUD ganhou painel arredondado + sombra com nome, badge de level dourado, barra de XP, e duas linhas de até 4 ícones (skills equipadas / armas equipadas, tons diferentes pra diferenciar). Painel lateral ganhou cabeçalho com retrato + nome + badge de level, e os 4 atributos principais (HP/STR/AGI/SPD) + Armadura viraram barras coloridas (HP usa hpFull/hpCritical, atributos usam 3 tons neutros do tema); resto das abas (Skills/Armas/stats derivados) sem mudança de conteúdo, só de cor. Fecha o item "Melhorar interface da página inicial" da Fase 1 (demais sub-itens — seta lateral, HUD superior de moeda/energia — seguem pendentes)

- 2026-07-07: Limpeza nos .md — corrigido fileID quebrado do `UIThemeApplier` em `01_MainMenu.unity` (Broken text PPtr, `Local file identifier (400763831) doesn't exist`; regenerado com um fileID novo). Marcadas como concluídas as skills Garimpeiro e Magneto (SKILLS_SYSTEM.md — tinham `SkillDef` registrado mas o roadmap ainda mostrava pendente); adicionada a skill Backup como novo item pendente (chama aliado, mecânica ainda não definida). Removida a ressalva "sprites pendentes" da linha de armas em CLAUDE.md (Fase 3). Referências a "ROADMAP_FUTURO.md — fases 4-9" corrigidas pra "4-13" (lista de arquivos + tabela de regras de documentação). "Sistema de raridade de armas" (Fase 3) permanece pendente — `WeaponData.dropOdds` ainda sem nenhum uso no código

- 2026-07-07: Criado UI_PALETTE.md documentando a paleta do `UITheme` (campo a campo, hex e uso), referenciado no CLAUDE.md (lista de arquivos + tabela de regras de documentação + tabela de ScriptableObject Assets). Botão "Jogar" de `01_MainMenu` migrado pra usar `UITheme.primaryAction` via `UIThemeApplier` em vez da cor hardcoded

- 2026-07-07: Criado UITheme ScriptableObject com paleta de cores central do jogo (fundo, botões, ícones, texto)

- 2026-07-07: Novo fluxo `01_MainMenu → 05_SelectOpponent (nova cena) → 04_CombatScenePVP` — Play reposicionado pro canto inferior direito (estilo Brawl Stars: verde, maior, "JOGAR"). `05_SelectOpponent` mostra até 6 oponentes de `CharacterDatabase.opponentCharacters` (novo campo, hoje 6x Medieval Warrior Girl como placeholder — só existem 3 `PlayerProfile` no projeto ainda) em cards construídos 100% via código (nome/level/HP/barras STR-AGI-SPD/ícones de arma/histórico de batalhas), gravando a escolha em `SelectedOpponentHolder` (novo ScriptableObject, mesmo padrão do `SelectedProfileHolder`). `CombatSceneLoader` não depende mais de um Player2 pré-colocado na cena — instancia o oponente dinamicamente a partir do profile escolhido, mesmo fluxo do Player1 (exigiu adicionar os componentes de combate, antes só existentes como override na cena, direto no prefab `Medieval Warrior Girl.prefab`). Histórico de batalhas/vitórias por oponente salvo em `PlayerPrefs` (`AttackSequencer.OnCombatEnd`), enquanto não há backend (Fase 6)

- 2026-07-07: Roadmap atualizado: adicionadas Fases 10-13 (Fluxo de Partida & Matchmaking, Social & Comunidade, Configurações, Modo Caminho Infinito — renumeradas a partir de 10 no ROADMAP_FUTURO.md pra não colidir com as Fases 7/8/9 já existentes) e expandidas Fases 1 (CLAUDE.md) e 4 (ROADMAP_FUTURO.md)

- 2026-07-06: Implementados os visuais T2/T3 da skill Shield (`Shield2.asset`/`Shield3.asset`, novas imagens do usuário em `Assets/Data/UI/Weapons/Shield/`) — `CombatSceneLoader.ResolveShieldVisual` escolhe o sprite certo pelo tier da skill equipada, com fallback pro tier anterior. Corrigido também bug real reportado pelo usuário: o escudo renderizava atrás do próprio corpo (`WeaponHandler.EquipShield` reusava a sorting layer da arma normal, `"Weapons"`, que fica atrás de `Characters` na ordem fixa do projeto, e nunca participa do swap dinâmico `SetAttackerLayers`/`RestoreDefaultLayers`) — nova sorting layer `Accessories` (sempre a mais à frente, depois de `Characters2`) dedicada ao escudo

- 2026-07-06: Corrigido bug real que impedia o Mimic de funcionar desde sempre (reportado pelo usuário testando Bomb vs Mimic, persistia mesmo com a chance forçada em 100%) — os 12 pontos de ativação de Super gravavam `lastSuperUsed`/`superActivationHistory` no `defender` (a vítima) em vez do `attacker` (quem usou a skill), então o Mimic nunca encontrava nada pra copiar. Corrigidos todos (Flash Flood, Haste, Piledriver, Fierce Brute, Tragic Potion, Net, Bomb, Vampirism, Cry of the Damned, Hypnosis, Tamer, Treat). Corrigido também `SkillAssetGenerator.GenerateAll()` — não incluía os T2/T3 já gerados no `SkillDatabase.asset`, apagando silenciosamente os tiers se rodado depois de `Rebuild Skill Database From Folder`; agora inclui qualquer `SkillData` com `tier > 1` encontrado na pasta

- 2026-07-06: Corrigido o deflector do Repulse travando em animação de Jump Start após o 1º deflect (achado testando Repulse vs Shuriken) — a transição Jump Start → Idle exige `JumpStart==false` E `Idle==true` juntos (confirmado no `.controller`), e o case `Repulse` setava `Idle=false` antes do swing sem nunca reverter, diferente de Counter/Reversal (que nunca mexe nesse bool)

- 2026-07-06: Consolidado `PlayerProfile.weaponLoadout` (referência a um asset `WeaponLoadout` satélite) em `PlayerProfile.weapons` (`List<WeaponData>` direta, mesmo padrão de `skills`/`pets`) — motivado por facilitar adversários reais/tabelas de dados futuras. Os 3 assets de loadout antigos estavam vazios, sem dado pra migrar. Tipo `WeaponLoadout` e os 3 assets satélite removidos; atualizados todos os pontos de leitura/escrita (`PlayerLoadout`, `CombatSceneLoader`, `CombatSimulator`, `CombatResultPanel`, `CharacterPanel`, `CharacterCreationEditor`)

- 2026-07-06: Ícone das skills ativas não aparecia no `SkillsHUD` (achado testando Bomb) — duas causas: T2/T3 sempre tem `icon` null por design e `AddIcon` não subia a cadeia `previousTier` pra herdar do T1 (mesmo padrão já usado em `CombatResultPanel`/`CharacterPanel`/`MainMenuCharacterPreview`); e o `HorizontalLayoutGroup` do container não tinha `childControlWidth/Height = false` (`WeaponHUD` já tinha), então o default `true` colapsava o ícone de 80x80 pra ~0, só o texto de usos (que não clipa) continuava visível

- 2026-07-06: Corrigido level-up não oferecendo upgrade de tier (T2/T3) de skills já equipadas (ex: Herculean Strength T1 → T2) — `ShowLevelUpChoice` filtrava por "já possui esse nome" e `icon != null` (T2/T3 sempre null), igual ao sistema de armas agora: T1 aparece se não possui nenhum tier, T2/T3 aparecem se o tier anterior já está equipado. Ícone resolve a cadeia `previousTier`. Corrigido também double-counting do bônus permanente (HP/STR/AGI/SPD) ao trocar de tier — `bonusValue2` é o total acumulado do tier, não aditivo

- 2026-07-06: Implementados os valores exatos de T1/T2/T3 de 50 skills (tabela de balanceamento do usuário) — `SkillTierGenerator.cs` reescrito: trocada a escala genérica ×1.35/×1.75 por números literais por skill/tier (`Populate Skill T1 Bonus Values`/`Generate Skill Tiers` continuam idempotentes). Removida a skill Extra Thick Skin (`Defs[]`, 3 assets T1/T2/T3, referências em `CombatSceneLoader`/`PlayerProfile`). `SkillData` ganhou `bonusValue7` (só usado por Deity, cujo `reversal` agora varia por tier: 0.40/0.50/0.60). Corrigidos 2 bugs do Shield: penalidade agora é no PRÓPRIO dano causado (`shieldDamagePenalty`, não mais `armor +=`) e o desarme do escudo passou a usar a fórmula real de `DisarmChance(attacker, defender)` em vez de uma chance fixa de 10%. Saboteur ganhou penalidade de iniciativa por tier (100/150/200, mecânica reintroduzida). Mimic T3 agora copia o histórico cronológico de Supers do oponente (`PlayerState.superActivationHistory`, novo) indexado pela própria contagem de uso, em vez de sempre a Super mais recente. First Strike ganhou SPD permanente em T2/T3 (`CombatResultPanel.ApplyBonus`). Novo `Tools → AutoArms → Rebuild Skill Database From Folder` (faltava — `SkillDatabase.asset` só refletia os T1 do `Defs[]`, nunca os T2/T3 gerados). Fora do escopo: Tamer (não incluído na tabela), Garimpeiro/Magneto (não implementadas)

- 2026-07-06: Refatorado sistema de skills para suportar T1/T2/T3, valores movidos para ScriptableObjects, criados assets T2/T3 para todas as skills. `SkillData` ganhou `tier`/`previousTier`/`bonusValue1..6`; migrados os literais hardcoded de `CombatSimulator.ApplySkillStats` + ~35 checagens vivas espalhadas pelo arquivo (Iron Head, Chaining, Sabotage, Determination, Survival, Bodybuilder, Hideaway, Spy, Repulse, Mimic, Fierce Brute, Bomb, Tragic Potion, Vampirism, Haste, Piledriver, Net, Hypnosis, Cry of the Damned, CalcDamage/WeaponBaseDamage, ShieldDisarmChance), de `CombatSceneLoader.ApplySkillStats` (2ª cópia) e de `PlayerProfile.GetEffectiveStats`/`GetWeaponDamageRanges` (3ª cópia, preview) pra ler do asset em vez de literal — as 3 cópias continuam com o mesmo mapeamento skill→bonusValueN, ver SKILLS_SYSTEM.md. `usesPerFight` do asset agora é lido de verdade (antes só descritivo — os contadores de uso eram hardcoded em `PlayerState`). Bug real corrigido no caminho: revert do bônus do Shield ao cair (`ShieldDisarm`/`ShieldDrop`) usava `-0.45f`/`-0.25f` fixos em vez do valor real do asset equipado — quebraria com Shield T2/T3. Novo `SkillTierGenerator.cs` (`Tools → AutoArms → Populate Skill T1 Bonus Values` / `Generate Skill Tiers (T2 & T3)`), mesmo padrão do `WeaponTierGenerator`. Deity mantém `reversal`/tamanho hardcoded (não cabiam nos 6 bonusValue, acordado com o usuário). Removidos 7 assets órfãos (`Hammer`, `Impact`, `Iron Skin`, `Master of Arms`, `Pugnacious`, `Strong Arm`, `Weapon Tampering`) que não estavam em `SkillAssetGenerator.Defs[]` nem no `SkillDatabase`. Escopo não coberto: `Tamer`/`Treat` (mecânica não auditada, ver PETS.md) e o caminho legado `PlayerCombat.AttackRoutine` (dead code enquanto `useSimulator=true`) continuam com valores hardcoded.

- 2026-07-06: Rastro/faísca do Slashing ("SlashFX") desligado só enquanto o Bow está equipado — novo `PlayerCombat.SetSlashFxEnabled(bool)`, chamado nos 6 pontos de swing junto de `IsRangedWeapon`, sem precisar mexer no clipe de animação (compartilhado por todas as armas)

- 2026-07-06: Revertida a tentativa de animação code-driven do Bow (corpo parado + mira dinâmica girando a arma) a pedido do usuário — corpo volta a fazer Slashing normal como qualquer arma melee (mesmo com o Bow parado no lugar); flecha simplificada de volta pro voo direto, sem giro de mira. Animação dedicada de "erguer o arco" fica pra depois

- 2026-07-06: Bow ainda disparava o trigger "Slashing" (giro de espada) mesmo depois de parar de correr até o alvo — visualmente errado pra um arco parado. `IsRangedWeapon` agora também pula `SwingTrigger`/`SetTrigger`/`SetSpeed` nos 6 pontos de swing (Hit/Dodge/Block/Counter-Reversal, pet e principal), mantendo só o tempo de espera — corpo fica em Idle, só a arma (mira dinâmica) e a flecha se movem

- 2026-07-06: 2 ajustes no Bow depois do 1º teste — atacante não corre mais até o defensor pra atacar (`IsRangedWeapon` pula `RunToDefender`/`RepositionIfNeeded` em 7 pontos, mantendo o resto do swing), com mira dinâmica nova (`AimWeaponAt`, gira a arma em espaço mundo pra apontar de verdade pro alvo, já que a distância/ângulo agora variam); flecha reduzida de tamanho (`ArrowProjectileScale = 0.3f`, independente do `scale` da arma)

- 2026-07-06: Implementada animação de arco e flecha pro Bow — trocada a tag `Thrown` (placeholder errado, fazia o arco inteiro "voar" até o defensor) por `Ranged` de verdade, caindo no fluxo de melee normal. Novo `WeaponData.projectileSprite` (flecha) viaja da ponta da arma até o defensor sem a arma sair da mão, reusando o sistema de 2 frames do Whip (`attackSprite`/`attackRotationOffset`) pra pose de "erguer o arco". Ligado nos 6 pontos que tocam `SetWeaponSwingPose` (Hit/Dodge/Block/Counter/Reversal, pet e principal)

- 2026-07-05: Confirmado pelo usuário — `ThrowFlightDuration = 0.25f` resolveu de vez o "throw a mais" da Shuriken/arremessos repetidos

- 2026-07-05: Corrigida a causa REAL do "throw a mais" (as 2 tentativas anteriores não resolviam porque o bug não era no C#) — o clipe de animação "Throwing" tem 0.4s de duração com loop ativado, e o código segurava a animação por 0.45s (0.05s a mais), fazendo ela reiniciar um 2º loop sozinha antes de conseguir sair pro Idle — o personagem "arremessava de novo" visualmente sem soltar nenhuma arma. Nova constante `ThrowFlightDuration = 0.35f` (era 0.45f) fica dentro do clipe, sem precisar de um 2º loop

- 2026-07-05: Ícone da WeaponHUD fica dourado o tempo todo durante ciclos de arremesso repetido (Shuriken etc.) — antes piscava cinza no intervalo entre um arremesso e o próximo, porque `Unequip()` zera `CurrentWeaponData` (usado pro destaque) a cada ciclo. Novo `WeaponHandler.PinnedHudWeapon`, setado antes de cada `Unequip` e limpo no `TurnEnd`

- 2026-07-05: Corrigido "throw a mais" visual na Shuriken (achado com log de debug, dados já estavam 100% corretos) — reequipar a arma ANTES da pausa entre ciclos deixava o personagem parado segurando a shuriken nova por meio segundo, lido como um 1º arremesso (postura) seguido do arremesso de fato. Reequipamento movido pro final da pausa, junto do trigger de `Throwing`

- 2026-07-05: 3 ajustes na Shuriken pedidos pelo usuário — pausa entre ciclos de arremesso aumentada de ×1 pra ×3 comboDelay (ainda "muito corrido"); mira do arremesso generalizada pra acertar o centro do corpo em vez do pé do defensor (era só pro bumerangue, `BoomerangHitHeight` virou `ThrownHitHeight`); novo `WeaponData.straightThrow` força o arremesso em linha reta sem arco/pêndulo (Shuriken T1/T2/T3 ligado, resto das armas Thrown sem mudança)

- 2026-07-05: Adicionada pausa extra entre ciclos de arremesso repetido (hitSpeed alto, ex: Shuriken 10.0) — reportado como "muito corrido", sem respiro entre a reação do defensor e o próximo lançamento. `CombatPlayer` espera mais um `comboDelay` só nesses ciclos repetidos, sem afetar o ritmo do melee normal nem do 1º arremesso do turno

- 2026-07-05: Corrigido arremesso "invisível" a partir do 2º ciclo do mesmo turno (armas Thrown com hitSpeed alto, ex: Shuriken 10.0) — o simulador reatribuía `currentWeaponData` internamente antes de cada arremesso, mas nada reequipava a arma visualmente depois do 1º `Unequip`, então o `FlyingWeapon` nunca era criado. `CombatPlayer` agora reequipa (`EquipSpecific` + `SetAttackerLayers`) se a arma não estiver equipada no início do case `ThrowWeapon`, fazendo "surgir outra shuriken" na mão antes de cada arremesso seguinte

- 2026-07-05: Background da arena aleatório a cada combate — `Assets/BattleGround` movida pra `Assets/Resources/BattleGround` (51 imagens), `CombatSceneLoader.RandomizeArenaBackground()` sorteia uma e troca o `SpriteRenderer` do `Colosseum arena` no início de `Initialize()`. Usa `Resources.Load` por nome (lista fixa no código) em vez de `LoadAll`, pra não carregar os ~500MB da pasta inteira de uma vez só

- 2026-07-05: Atualizada a curva de XP (`XpSystem.XpRequired`) pra tabela nova pedida pelo usuário — 1→2 até 6→7 sobe +1 por nível (5,6,7,8,9,10), 7→8 em diante sobe +2 por nível sem teto (12,14,16,18,20,22...). Substituiu a fórmula antiga `(level+1)*(level+2)`

- 2026-07-05: Corrigido o fix anterior do Reversal sem Hurt — usar `PlayJumpStart` pra tirar o defensor de Slashing tocava a animação de pulo de verdade, e a duração curta não dava tempo da transição real terminar antes do Hurt disparar, fazendo o hurt aparecer "no meio do pulo" em vez de imediatamente. Novo `AnimationController.ForceIdleState()` corta direto pro estado Idle via `Animator.Play(...)`, sem transição/blend nenhuma — elimina a animação de pulo visível por completo
- 2026-07-05: Corrigido o bug real do Reversal sem Hurt (achado por vídeo, não pelos logs de posição) — a transição pro estado Hurt no Animator exige `Idle == true` e pertence só ao estado Idle (não Any State), e o defensor de um Reversal-após-Hit ainda está preso em Slashing (do próprio golpe que deu), então o trigger Hurt nunca disparava — sem reação visual nenhuma, apesar do dano/popup aparecerem certos. `CombatPlayer` agora tira o defensor de Slashing (toggle de JumpStart, sem deslocar) antes do Hurt, só no Reversal
- 2026-07-05: Revertida a tentativa de pular o jump-back do `TurnEnd` em ações extras emendadas — não corrigiu o bug do Reversal e quebrou o retorno ao spawn esperado a cada "RAPIDO!" (parecia um combo contínuo em vez de ações distintas). `TurnEnd` volta a pular pro spawn sempre

- 2026-07-05: Corrigido `ComboChance()` — clamp de 60% era aplicado ANTES do decaimento por hit extra, achatando armas com comboBonus extremo (ex: Branch ~400% de chance total, esperado 400%→200%→100%→50%→25%, na prática virava 60%→30%→15%...). Clamp movido pro resultado FINAL (pós-decaimento), em `[0,1]` — armas normais não mudam de comportamento, só as deliberadamente extremas passam a garantir combo nos primeiros hits de verdade
- 2026-07-05: Corrigido bug de Reversal não reposicionando — "quem retalia nunca saiu do lugar" era verdade só pro Counter (age antes de qualquer dano); Reversal age DEPOIS de um Hit/Block já resolvidos (que aplicam knockback no retaliador), então sem reposicionar o retaliador podia golpear fora de alcance — reportado pelo usuário como "dano do reversal só parece acontecer depois do atacante original voltar pro spawn". `CombatPlayer` agora chama `RepositionIfNeeded` quando `evt.type == Reversal` (não `Counter`); restaurado também nas retaliações que viram Block/Dodge (`isRetaliation`), que o fix anterior tinha pulado por engano
- 2026-07-05: Corrigidos 2 bugs visuais de retaliação (Counter/Reversal), reportados testando a arma Branch (reversalBonus 100%): quando a retaliação era bloqueada/esquivada pelo atacante original (`SimulateRetaliation` emitindo `Block`/`Dodge` normal em vez de um `Reversal` limpo), o retaliador "corria" até o alvo do zero antes de bloquear/esquivar de volta e ficava preso fora do Idle depois (hurt do alvo parecia atrasado, aparecendo só depois do TurnEnd já ter mandado ele pro spawn). Novo `CombatEvent.isRetaliation` marca esses eventos; `CombatPlayer` pula o `RepositionIfNeeded` (mantendo o swing) e aplica o mesmo toggle de `PlayJumpStart` que os cases dedicados `Counter`/`Reversal` já tinham
- 2026-07-05: Conectado `WeaponData.deflectBonus` (morto antes) — mesma ação da skill Repulse (deflecte um arremesso de volta pro lançador original), só que como bônus por arma equipada pelo defensor. Soma com o 30% de Repulse se o defensor tiver os dois. Racquet/Frying Pan/Book/Fan/Sai já tinham valores calibrados (0.25-0.56), agora com efeito real
- 2026-07-05: Conectados `WeaponData.accuracyBonus` e `dexterityBonus` (auditados antes — ambos existiam desde sempre mas nunca eram lidos). `accuracyBonus` da arma do ATACANTE agora subtrai de `BlockChance` (quanto maior, menor o block do defensor); `dexterityBonus` da arma do ATACANTE agora subtrai de `DodgeChance` (positivo dificulta esquiva do defensor, negativo facilita). `attacker.accuracy` (Relentless/personagem) mantido intacto em `DodgeChance` — dois sistemas separados, arma neutraliza Block e skill neutraliza Dodge. Testado mentalmente com Branch T1 (accuracyBonus 2.0, dexterityBonus -1.0): Block fica efetivamente 0% (chance negativa nunca dispara no `Roll()`), Dodge vai pro teto de 60%
- 2026-07-05: `ThrowChance()` agora soma `Long` (0.12) — pedido do usuário: toda arma não-Thrown deve ter chance pequena de arremesso ocasional, incluindo Whip (antes tinha ThrowChance=0, nunca arremessava). `TagSum` ganhou o parâmetro `longTag`, só usado por `ThrowChance`; ThrowChance legado (`PlayerCombat.cs`, dead code) não foi atualizado
- 2026-07-04: Verificado e implementado sistema de Draw Chance por turno no CombatSimulator — `WeaponData.drawChance` nunca era lido em nenhuma rolagem (campo morto); conectado como peso do sorteio de arma no Pickup/WeaponSwap (`PickWeaponByDrawChance`), não como re-rolagem "luta desarmado" a cada turno (decisão do usuário, evita conflito com hitSpeed/WeaponSwap/bumerangue já implementados). Corrigidos drawChance de T2/T3 de Shuriken/Bottle/Racquet/Broadsword/Fan pros valores originais do My Brute
- 2026-07-04: Corrigido bug de sorting layer no bumerangue — mesmo problema do `PickupWeapon`: `EquipSpecific` (chamado por `BoomerangReturnFlight` ao reequipar a arma na mão do atacante após o voo de volta) sempre cria o sprite na layer padrão do `WeaponHandler` ("Weapons", papel do defensor) em vez de "Weapons2" (papel do atacante). Corrigido chamando `thrower.SetAttackerLayers()` de novo logo após o `EquipSpecific`
- 2026-07-04: 3ª iteração dos Sorting Layers — ordem `Default < Weapons < Characters < Characters2 < Weapons2` deixava a arma do atacante (frontmost) na frente do PRÓPRIO corpo dele, errado visualmente ao sacar (bug reportado pelo usuário). Trocada posição de `Characters2`↔`Weapons2`: ordem final `Default < Weapons < Characters < Weapons2 < Characters2` — arma do atacante continua na frente do defensor inteiro, mas agora atrás do próprio corpo (regra "própria arma atrás do próprio corpo" preservada pros dois lados)
- 2026-07-04: Corrigido bug de sorting layer no turno em que a arma é sacada — `SetAttackerLayers()` (TurnStart) não achava GameObject de arma pra reatribuir enquanto o personagem ainda estava desarmado; a arma, criada depois no `PickupWeapon`, ficava presa na layer padrão errada ("Weapons") só nesse 1º turno, corrigindo sozinha a partir do 2º. `CombatPlayer` agora chama `attacker.SetAttackerLayers()` de novo logo após o equip no case `PickupWeapon`
- 2026-07-04: Revertida a 1ª correção dos Sorting Layers (movia só `Weapons2` pro fim) — causava regressão real reportada pelo usuário: corpo do atacante desarmado ficava atrás da arma parada do defensor (ex: Player1 armado/Player2 desarmado, arma do Player1 "apagava" o Player2 se aproximando). Nova ordem definitiva: `Default < Weapons < Characters < Characters2 < Weapons2` — arma de quem ataca sempre na frente de tudo, corpo do atacante sempre na frente da arma parada do oponente
- 2026-07-04: Corrigido bug visual — a arma do atacante não aparecia por cima do defensor durante o ataque (ficava atrás até do corpo dele, por design anterior). Reordenados os Sorting Layers em `ProjectSettings/TagManager.asset` (ver correção seguinte, esta 1ª tentativa foi revertida)
- 2026-07-04: Corrigido bug visual do Counter — o atacante parava na distância do PRÓPRIO alcance (`RunToDefender`), mas quem bate primeiro num Counter é o defensor; se o alcance dele fosse menor, o golpe conectava fora de alcance e o knockback seguinte parecia um "teleporte" pra trás. `CalcAttackPosition` ganhou um 3º parâmetro `reachOwner`; `RunToDefender` agora dá um peek no próximo evento e usa o alcance do defensor quando ele for um Counter
- 2026-07-04: `CombatPlayer.CalcAttackPosition` — removido o termo de `scale` da fórmula de alcance (era `2.0 + reach − (scale−1)×4`, virou só `2.0 + reach`), pedido do usuário: misturar scale confundia a calibração e deixava o atacante grudado demais no defensor em algumas armas, fazendo o knockback do Counter/Reversal parecer um "teleporte" pra trás. Afeta Anchor/Axe/Baton/Bone (scale != 1, distância efetiva mudou); Book não muda (já batia no piso de 0.3 nos dois formatos)
- 2026-07-04: `WeaponData.counterBonus` conectado em `CounterChance(defender) = defender.counter + weaponData.counterBonus` — campo tinha sido adicionado sem efeito nenhum, usuário configurou valor no Whip e reportou "não funcionou"
- 2026-07-04: Novo campo `WeaponData.counterBonus` (float, default 0) — contra-ataque por arma. Adicionado às 78 armas de `Assets/Data/Weapons/`, todas em 0 por enquanto
- 2026-07-04: Timing do sprite de ataque (attackSprite) ajustado — trocava pro 2º frame junto do SetTrigger (início do swing); agora só troca depois do primeiro WaitForSeconds(slashHalf), bem perto do impacto, dando a impressão de "chicotada" (Whip fechado na largada, abre só no finalzinho) em vez de esticado o swing inteiro. Afeta os 6 pontos de swing (Hit, Dodge, Block, Counter/Reversal, pet e personagem)
- 2026-07-04: Whip: `attackRotationOffset.z` ajustado de -25 pra -10
- 2026-07-04: Corrigido bug de hitSpeed<100% — `weaponHitSpeedDebt` era zerado (`0f`) ao trocar/pegar arma (Thief/PickupWeapon/WeaponSwap), então o 1º turno com uma arma lenta (ex: Whip 0.8) sempre caía em `HitSpeedSkip` (personagem "aparece com lentidão e não ataca" ao sacar a arma). Agora seta `1f`, garantindo que o turno de saque já ataque de verdade
- 2026-07-04: Whip aumentado pra `attackScaleMultiplier: (3, 1, 1)` (era 2). Novo `WeaponData.attackRotationOffset` (soma no rotationOffset só durante o attackSprite, Whip usa -25°→-10° pra apontar pro pé do defensor) e `showAttackTipEffect`/`attackTipOffset` (faísca procedural na ponta da arma, sem asset externo — `CombatPlayer.PlayWeaponTipEffect`, burst radial de 6 sprites gerados em runtime)
- 2026-07-04: `WeaponData.attackScaleMultiplier` (Vector3, default 1,1,1) — escala aplicada só durante o attackSprite, sem afetar o idle. Whip usa (1.5, 1, 1) pra ficar mais largo só no estalo (frame de ataque estava pequeno demais)
- 2026-07-04: Animação de 2 frames pra armas — `WeaponData.attackSprite` (opcional) + `WeaponHandler.SetAttackPose`, chamado por `CombatPlayer.SetWeaponSwingPose` em todo swing melee (Hit/Dodge/Block/Counter/Reversal, personagem e pet). Whip T1/T2/T3 configurada com os PNGs já existentes em `Assets/Data/UI/Weapons/Whip/` (idle + ataque); `.meta` dos 6 PNGs criados manualmente (ainda não importados pelo Unity)
- 2026-07-04: Book T1/T2/T3 ajustada — `scale: 0.8` fazia o alcance calculado (`CalcAttackPosition`) ficar em 2.9 (personagem atacando de longe, já que a fórmula é calibrada com scale=1.5 como referência e o termo de scale soma distância abaixo disso); `reach: -2.5` compensa e traz pro piso de 0.3 (mínimo)
- 2026-07-04: Bumerangue implementado — `WeaponData.isBoomerang` (T1/T2/T3), novo evento `BoomerangReturn`: em vez de ficar desarmado após o arremesso, a arma volta pra mão do atacante (arco por baixo, oposto ao arco por cima da ida) e permanece equipada até WeaponSwap/Disarm/WeaponDrop
- 2026-07-04: Corrigido bug no bumerangue — evento `BoomerangReturn` não setava `targetIndex`, então `CombatPlayer` (que resolve `defender = GetCombat(evt.targetIndex)`) sempre caía no default (0); sempre que o Player1 arremessava, o voo de volta "nascia" na posição dele mesmo (from ≈ to), tornando a animação de retorno invisível
- 2026-07-04: Corrigido bug no bumerangue — a arma sumia por um instante bem no momento do Hit (destruída ao chegar no case ThrowWeapon, recriada só no case BoomerangReturn, com o Hit inteiro rodando no meio). `CombatPlayer` agora mantém o mesmo GameObject vivo (parado no ponto de impacto, `_boomerangFlyingObject`/`_boomerangLandPosition`) do arremesso até a volta reaproveitá-lo, com limpeza defensiva nos caminhos que nunca emitem `BoomerangReturn` (Repulse, alvo pet)
- 2026-07-04: Corrigida travada na volta do bumerangue — `PlayCatchWeapon` (mexe no bool Idle) disparava logo após o ThrowWeapon, competindo com a transição Throwing→Idle (ExitTime) ainda em andamento no Animator. Removido; a arma agora só reequipa (EquipSpecific) ao chegar, sem pose de captura — mais sutil e sem o travamento
- 2026-07-04: Bumerangue agora fica sempre em movimento — o voo de volta era só disparado no case BoomerangReturn (depois do Hit inteiro rodar), deixando a arma parada/imóvel no ponto de impacto por um instante perceptível. `CombatPlayer.ThrowWeapon` agora dispara o voo de volta em paralelo (fire-and-forget, `BoomerangReturnFlight`) assim que a ida chega ao alvo, sobrepondo com o Hit/Dodge/Block seguinte; `BoomerangReturn` só espera essa coroutine terminar
- 2026-07-04: Ajustado alvo do arremesso do bumerangue — mirava direto no pivô (pés) do defensor; sobe `BoomerangHitHeight = 0.9` pra acertar mais perto do centro do corpo (só o bumerangue, outras Thrown inalteradas)
- 2026-07-04: Bumerangue passou a rolar Block/Dodge REAIS do defensor (BlockChance/DodgeChance, mesmas fórmulas do melee) em vez da chance fixa de acerto (80%/55%) usada pelas outras armas Thrown — pedido do usuário. `CombatPlayer` ganhou suporte a `Dodge`/`Block` com `isThrow=true` (pula reposicionamento/swing do atacante, que nunca correu nem sacou arma pro arremesso)
- 2026-07-04: `WeaponData.hitSpeed` agora tem efeito real no jogo (antes só afetava velocidade de animação no caminho legado morto) — `CombatSimulator.ResolveHitSpeedUnits` decide quantas sequências de ataque (melee) ou ciclos de arremesso (Thrown/bumerangue) acontecem no mesmo turno: piso + chance da fração pra hitSpeed ≥100% (ex: 375% = 3 garantidos + 75% de chance de um 4º), débito acumulado entre turnos (`PlayerState.weaponHitSpeedDebt`) pra hitSpeed <100% (arma pode pular turnos inteiros, ex: Anchor 0.48). Novo evento `HitSpeedSkip` + popup "LENTO!" quando a arma não age no turno. Afeta todas as 26+ armas do jogo, não só o bumerangue
- 2026-07-04: Corrigidos 4 bugs visuais: weapon drop delay/pêndulo, sorting order armas, morte por reversal com teleporte, dodge no limite da arena sem animação
- 2026-07-04: Renomeadas 8 armas: Mammoth Bone→Bone, Mug→Bottle, Pio Pio→Boomerang, Halberd→Reaper, Noodle Bowl→Bow, Leek→Branch, Trombone→Anchor, Keyboard→Book
- 2026-06-30: CalcAttackPosition unificado — fórmula única `reach = 2.0 + data.reach − (scale−1)×4.0` para todas as armas; removidas distinções por Heavy/Fast/Long; K aumentado de 3.2 para 4.0; a scale=1.5 o valor de `data.reach` é o alcance final direto; `WeaponData.reach` alterado de `int` para `float` (calibração mais fina, ex: 1.4); Axe T1 ajustada para reach=1.2 (compensa remoção do bônus Heavy)
- 2026-06-30: SwingTrigger com herança de attackAnimation por cadeia previousTier — T2/T3 com `attackAnimation=Auto` herdam o valor explícito do T1; prioridade: attackAnimation explícito > tag Fast > fallback Slashing
- 2026-06-30: WeaponHandler.EquipSpecific corrigido — T2/T3 sem `inHandSprite` sobem a cadeia `previousTier` para sprite fallback; `CurrentWeaponData` sempre refletindo a arma real (era `Unequip()` → `null` → animação errada)
- 2026-06-30: Sistema de tiers de armas T1/T2/T3 — WeaponData com campos `tier` (int), `previousTier` (WeaponData ref), `attackAnimation` (enum Auto/Slashing/SlashingDagger); WeaponTierGenerator editor tool (Tools → AutoArms → Generate Weapon Tiers / Assign All Tiers to AttackSequencer); T2 damage ×1.35, T3 damage ×1.75
- 2026-06-30: CombatResultPanel — ShowLevelUpChoice com filtragem por tier (T1 sem upgrade no loadout; T2/T3 se previousTier presente); ApplyBonus remove previousTier do loadout ao evoluir; HasUpgradeInLoadout percorre cadeia T3→T2→T1
- 2026-06-29: Adicionadas 4 armas: Pio Pio, Noodle Bowl, Frying Pan, Racquet (ScriptableObjects em Assets/Data/Weapons/)
- 2026-06-29: Implementados ScriptableObjects de 22 armas com stats completos T1/T2/T3 (Knife, Sai, Mug, Fan, Keyboard, Leek, Broadsword, Scimitar, Sword, Axe, Halberd, Baton, Lance, Trident, Whip, Bumps, Flail, Morning Star, Mammoth Bone, Hammer, Trombone, Shuriken); dropOdds e Ranged adicionados ao WeaponData.cs
- 2026-06-29: Mimic implementada — copia a última Super do oponente (25%/turno, 1x/luta); filtragem inteligente (Treat sem pet, Thief sem arma, Tamer sem carcaça, Hypnosis/Cry sem pets vivos inimigos, Flash Flood sem 3 armas, Tragic Potion/Vampirism por HP); lastSuperUsed rastreado em todos os TryActivate*; SkillsHUD criado (band Y 0.700–0.810, 80px por ícone, contador de usos some quando esgota); Tamer usesPerFight=4 corrigido; CombatPlayer.Mimic popup magenta + UseSkill em todos os Super cases
- 2026-06-29: Repulse implementada — 30% de chance de deflectir throw de volta ao lançador (sempre acerta, +5% crit, dano com STR do lançador); bloqueado por netEnsnared; CombatEventType.Repulse, SimulateRepulse, case Repulse em CombatPlayer (Slashing + FlyWeapon + SpawnRepulse cyan), CombatLogFormatter, SKILLS_PASSIVE.md
- 2026-06-29: Roadmap de skills enxugado — removidas 8 skills não implementadas (Impact, Pugnacious, Iron Skin, Strong Arm, Master of Arms, Weapon Tampering, Hammer, Treat); Hypnosis/Cry of the Damned/Tamer mantidas como [x] em Relacionadas a Pets; adicionadas 4 novas: Repulse (30% deflect de throw, +5% crit no deflect), Garimpeiro (pega arma do chão), Mimic (copia última Super do oponente, 1x), Magneto (levita armas do chão e solta tudo de uma vez, 1x); SkillAssetGenerator atualizado
- 2026-06-29: Dying — animação de morte ao fim do combate; PlayDying() em AnimationController, TriggerCombatEndRoutine em CombatPlayer (0.5s ciclo + SetSpeed(0) congela no último frame + 0.4s pausa antes da tela de resultado); trigger Dying + transição AnyState→Dying (CanTransitionToSelf=0) nos 3 controllers; Assassin Guy ganhou novo AnimatorState Dying
- 2026-06-29: SlashingHeavy removido — armas Heavy usam trigger Slashing padrão; parâmetro, estado e todas as transições removidos dos 3 controllers (Assassin Guy, Medieval Warrior, Medieval Warrior Girl)
- 2026-06-29: Piledriver — nova animação EarthFissure substituiu Explosion_1; offset Y -0.049 em relação ao pivot do defensor, escala 0.5×0.5
- 2026-06-29: BloodEffectPlayer — efeitos de sangue em hits normais (hit1/hit2 aleatório), críticos (hitcrit) e hits em pets; integrado no CombatPlayer (case Hit); singleton MonoBehaviour + 3 RuntimeAnimatorController SerializeField
- 2026-06-29: Net — pet enredado exclui rede visual do pet (sorting layer Characters/100, voa até posição do pet); Treat libera pet com PlayNetBreakEffect (scale-up + fragmentos radiais)
- 2026-06-29: Net — RollPetTarget exclui pets netEnsnared como alvo; prioridade 100% nos pets (caughtPet = alivePets.Count > 0)
- 2026-06-28: Tamer — volta ao spawn (InSpawnZone + JumpTo) depois de comer carcaça, antes do próximo ataque
- 2026-06-28: Pets — SimulatePetTurn usa RollPetTarget (Javali 75%/Macaco 50%/Rato 50%) em vez de Roll(40%) com pet aleatório
- 2026-06-28: Pets — PetAnimationController: ShowNetEnsnared/ReleaseNet/NetOscillateLoop (rede visual oscilante no pet)
- 2026-06-28: Pets — PetAnimatorSetup: canTransitionToSelf=true no Hurt, duration=0f em todas transições, Slashing→Idle exige Idle=true (fix stutter e desconexão visual do golpe)
- 2026-06-25: 5 tasks novas roadmap — chibi, slot extra pago, redes sociais, login email/Google/Apple/Facebook
- 2026-06-24: Pets — 4 ajustes: targeting como alvo válido, escala visual, fix animação (sample rate 12→30fps), dano Monkey (9-12)
- 2026-06-24: Pets implementados (Mouse, Monkey, Boar) — PetState, PetCombatController, HealthBarPet, CombatSceneLoader.SpawnPets
- 2026-06-23: Chef — timing: PoisonDamage emitido após TurnEnd; CombatPlayer espera duração real do clipe
- 2026-06-23: Chef — visual: explosão só no tick do veneno (PoisonDamage); pizza some sem explodir
- 2026-06-23: Chef — bug compilação ChefEffectGenerator (EditorCurveBinding via campos públicos do struct)
- 2026-06-23: Chef implementada — veneno 1%/turno via EmitTurnEnd, PoisonDamage, ChefPizzaPrefab, ChefEffectGenerator
- 2026-06-23: Vampirism — 2 ajustes: sem flip de direção ao montar; pré-condição HP < 50%
- 2026-06-23: Vampirism implementada — Super 33%/turno, cura por missingHp×25%, VampirismRoutine salto nas costas
- 2026-06-23: Bug — arma/escudo caindo invisível no Player2 (sortingOrder 0→1 em DropWeapon/DropShield)
- 2026-06-23: Fast Metabolism — redefinida: burst 10 curas no mesmo turno; fix fastMetabolismTookDamage que impedia disparo
- 2026-06-23: Monk — corrigida: hitSpeed=0 removido completamente; aura laranja persistente, flash a cada counter
- 2026-06-23: Saboteur — redefinida: 1ª arma empunhada quebra 100%; EquipStartingWeaponIfNeeded removido
- 2026-06-23: Bomb agora consome turno; Fierce Brute + Bomb podem coexistir no mesmo turno
- 2026-06-23: Tragic Potion — posição ajustada (Y=0.38, offset +0.38 na direção que o personagem está virado)
- 2026-06-23: Tragic Potion — 3 ajustes: inclinação pelo lado certo, boca mais baixa, pausa 0.25s antes da cura
- 2026-06-23: Tragic Potion — 3 ajustes visuais: frasco 0.5x, nasce em offHandBone, gesto de beber 0.35s
- 2026-06-23: Tragic Potion implementada — cura 25-50% HP, pré-cond HP<60%, sem consumir turno, visual 5 fases
- 2026-06-23: Fast Metabolism implementada — regen 1%/turno + burst 5%x10 quando HP cruza 50% pela 1ª vez
- 2026-06-23: Histórico de batalhas — limites: 10 Ataque, 20 Defesa, Torneio reseta toda quinta-feira
- 2026-06-23: Task nova Fase 5: Histórico de batalhas com 3 abas (Ataque / Defesa / Torneio)
- 2026-06-23: Torneio — pontuação/elo: 15 lutas/inscrito, elo por personagem, marcador em SelectCharacter
- 2026-06-23: Torneio — 3 tasks: sistema de elo/liga, ciclo semanal dia a dia, batalhas offline+log+replay
- 2026-06-23: Roadmap — 9 tasks: raridade personagens, pacotes diamantes, acessório torneio, servidor/região, checklist CNPJ
- 2026-06-22: Bomb — 3 bugs: targetIndex, log Supers, turno não encerrava se Bomb matasse defensor
- 2026-06-22: Bomb implementada — 17%/turno, 2 usos, explosão todos alvos, ShuffleList Supers a cada turno
- 2026-06-22: Fierce Brute implementada — 33%/turno, x2 dano sem consumir turno, ghost trail roxo persistente
- 2026-06-22: Net implementada — 50%/turno, netEnsnared pula turno e bloqueia reações, CheckNetFreed, visual net1/net2
- 2026-06-22: Piledriver implementada — 17%/turno, dano=defStr×2.5, PiledriverEffectGenerator, AnimationAutoDestroy
- 2026-06-22: Haste implementada — 23%/turno, dano=Speed×1.5, pode esquivar/bloquear, dash atravessa oponente
- 2026-06-22: Sabotage implementada — 50%/hit destrói arma cinza; Flash Flood: salto vertical, velocidade, sem early break
- 2026-06-21: Flash Flood implementada — Super 17%/turno, arremessa 3 armas consecutivas sempre acertando
- 2026-06-20: Roadmap — 6 tasks monetização/onboarding: tutorial, passe diário A/B, pacote premium, battle pass
- 2026-06-20: Chaining — 3 ajustes: pose Face 03, popup "ESTUNADO!", desarme garantido ao estunar
- 2026-06-20: Saboteur — 4 bugs: ordem ApplySaboteur antes Spy, sortingOrder, ForceUpdateCanvases, escala da arma
- 2026-06-20: Hideaway corrigida (ThrowChance normal + Unequip ao arremessar); Sticky Hands implementada (-50% desarme/-50% throw)
- 2026-06-20: Saboteur bug — arma destruída caía invisível na WeaponHUD; DropWeaponFromHud criado
- 2026-06-20: Saboteur implementada — destrói 1 arma aleatória do oponente antes da luta; -100 initiative
- 2026-06-20: Spy implementada — -20% dano permanente em metade do loadout inimigo antes da luta
- 2026-06-19: Chaining implementada — 3 hits sem levar dano estunam por 1 ação; StunDazedLoop, popup, desarme garantido
- 2026-06-19: Hideaway/WeaponSwap — 2 ajustes: combo pós-throw rearremessa; troca de arma bloqueada no 1º turno
- 2026-06-19: Hideaway — corrigida para arremesso forçado 100% sempre que armado (SimulateHideawayThrowCombo)
- 2026-06-19: Hideaway/WeaponSwap — 2 bugs: arma fica na mão ao arremessar; troca remove do loadout permanentemente
- 2026-06-19: Roadmap — Resistant adicionada como task em Passivas de Defesa (estava implementada mas sem task)
- 2026-06-19: Hideaway implementada (+25% block throw, 50% throw, arma permanece); WeaponSwap mecânica geral (40%/turno)
- 2026-06-19: Bug — CalcThrowDamage agora soma STR do atacante (era só weaponBaseDamage)
- 2026-06-19: Thief — 2 ajustes: pisca via SpriteRenderer.sprite direto; altura do salto = jumpHeight
- 2026-06-19: Thief visual refinado — flip ao chegar, bounce horizontal, pisca face a cada ciclo, velocidade RunSpeed
- 2026-06-19: Thief implementada — roubo 44%/turno desarmado, 2 usos, visual procedural salto nas costas
- 2026-06-19: Bug — re-equip automático após throw removido; CombatEventType.WeaponEquipped removido
- 2026-06-19: Determination implementada — retry 60%/turno se o golpe não causou dano (miss/dodge/block/counter)
- 2026-06-19: 2 bugs — escudo/arma mutuamente exclusivos ao cair; Monk jumpback corrigido (guard por hitSpeed>0)
- 2026-06-19: Bug visual — Medieval Warrior corrigido: Running=true no default do controller causava corrida no spawn
- 2026-06-19: Counter reordenado para 1º em SimulateHit (antes de Block); ShieldDrop ao bloquear adicionado
- 2026-06-19: Shield implementada — +45% block, +25% armor, hasShield, WeaponHandler.offHandBone, DropShield visual
- 2026-06-19: WeaponType→List<WeaponType> (até 3 tags); fórmula dano aditiva STR; fix Monk hitSpeed; roadmap corrigido
- 2026-06-18: Skills rebalanceadas — Armour, Untouchable, Relentless→accuracy, Fists of Fury, Counter Attack; ComboChance cap 60%