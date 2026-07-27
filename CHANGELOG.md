# AutoArms — Changelog

- 2026-07-27: **Aba Diamantes reordenada — resgate já feito vai pro final do grid** (pedido do
  usuário) — `ShopController.RebuildGrid` particiona (partição ESTÁVEL, não `List.Sort`) os itens
  da aba em "disponível ou não é resgate" primeiro, "resgate já feito" depois, mantendo a ordem
  relativa dentro de cada grupo (Diário/Semanal/Mensal entre si, os 8 pacotes pagos entre si).
  Recalculado a cada `RebuildGrid()` — um resgate que reabilita sozinho (countdown chegou a zero)
  volta pro início na próxima reconstrução.
- 2026-07-27: **Contagem regressiva dos 3 botões de resgate de diamante passou a mostrar dias**
  (pedido do usuário) — `PlayerEconomyState.FormatCountdownUntil`: >= 1 dia de sobra mostra
  "N dias"/"1 dia" (arredondado pra cima); abaixo de 1 dia volta pro `H:MM:SS` de sempre. Evita o
  contador Semanal/Mensal mostrar algo tipo "144:00:00" na maior parte do tempo.
- 2026-07-27: **2ª rodada de bugs reais corrigidos nos resgates de diamante, reportados pelo
  usuário depois da correção anterior**:
  - `Unable to convert null value to Firebase.Firestore.Timestamp` — `FirestoreService.
    ReadServerNowAsync` usava `snap.TryGetValue<Timestamp>(...)`, mas essa versão do SDK do
    Firebase Unity PODE lançar essa exceção em vez de devolver `false` quando o valor do probe
    ainda está resolvendo no servidor (mesma corrida rara já documentada no histórico de
    `EnergyService`, só que o `TryGetValue` não é tão "Try" assim nesse caso específico) — a
    exceção saía direto do loop de retry (3 tentativas com 250ms de intervalo, pensado
    exatamente pra cobrir essa janela), nunca chegando na 2ª/3ª tentativa. Corrigido com um
    try/catch por tentativa, tratando a exceção como "ainda não resolveu" em vez de abortar.
  - `Missing or insufficient permissions` (persistente mesmo após apontar o probe pro documento
    certo) — a nova regra do Firestore pra `users/{uid}/rewardsState/{stateId}` (ver entrada
    anterior) só existe no arquivo `firestore.rules` do repositório; **precisa ser publicada** no
    projeto Firebase de verdade (`firebase deploy --only firestore:rules`) pra valer — sem isso, o
    Firestore nega por padrão qualquer acesso a um path sem regra explícita já em produção
    (subcoleções não herdam a regra do documento pai automaticamente). Nenhuma mudança de código
    corrige isto — é uma ação de deploy que só o usuário/dono do projeto deve confirmar.

- 2026-07-27: **Bug real corrigido — `[DailyRewardsService] Falha ao sincronizar estado dos
  resgates: Missing or insufficient permissions`** (reportado pelo usuário logo após a
  implementação abaixo). Causa: `RefreshStatusAsync` mirava o probe de "hora do servidor"
  (`FirestoreService.ReadServerNowAsync`) direto no documento `users/{uid}/rewardsState/diamonds`
  — mas esse documento tem `allow write: if false` DE PROPÓSITO (protege os campos de período
  contra um cliente malicioso), então a própria tentativa de gravar o campo descartável do probe
  já era rejeitada, antes de chegar na leitura de período de verdade. Corrigido mirando o probe no
  documento PRINCIPAL da conta (`users/{uid}`, já client-writable por outros fluxos como
  coins/diamonds) — a leitura dos campos de período continua vindo do doc protegido, só o probe
  muda de alvo. Bônus da mesma correção: `FirestoreService.ReadServerNowAsync` trocou
  `UpdateAsync` por `SetAsync(..., MergeAll)` — `Update` falha com "not-found" se o documento
  alvo ainda não existir (conta nova sem nenhuma escrita prévia em `users/{uid}`); `Set` com merge
  cria o documento se faltar, sem mudar nada pro caso comum onde ele já existe — mais seguro pros
  dois chamadores (`EnergyService` também usa este método).

- 2026-07-27: **Resgates gratuitos de diamante — Diário/Semanal/Mensal (aba Diamantes da Loja)** —
  pedido do usuário. 5 diamantes/dia (libera à meia-noite), 30/semana (toda segunda-feira), 100/mês
  (todo dia 1º) — fuso ÚNICO/GLOBAL America/Sao_Paulo (Horário de Brasília) pra todo mundo,
  independente de onde o jogador está.
  - **100% server-authoritative** (mesma regra inegociável de "moeda premium nunca
    client-writable", ARQUITETURA.md): 3 Cloud Functions novas (`claimDailyDiamonds`/
    `claimWeeklyDiamonds`/`claimMonthlyDiamonds`, `functions/src/dailyDiamondRewards.ts`, núcleo
    compartilhado `claimReward` parametrizado por tipo — mesmo espírito de `rerollShared.ts`).
    Período calculado via `Intl.DateTimeFormat` com o timeZone IANA `America/Sao_Paulo` (não um
    offset hardcoded — Brasil não observa horário de verão desde 2019, mas isso continua correto
    de graça se essa política mudar de novo). Chave de período: dia = `YYYY-MM-DD`, mês =
    `YYYY-MM`, semana = data da segunda-feira que iniciou aquela semana — muda exatamente no
    instante de liberação de cada tipo.
  - **Estado de período em documento SEPARADO** (`users/{uid}/rewardsState/diamonds`, regra nova
    em `firestore.rules` — `allow read` do dono, `allow write: if false`, mesmo padrão já usado
    por `casePurchases/{packageId}`): o doc `users/{uid}` principal aceita `allow read, write`
    irrestrito do dono hoje (TODO de segurança pré-existente pra coins/diamonds/
    nextCharacterPurchaseCount, ver `WalletService.cs`) — se os campos de período morassem lá, um
    cliente malicioso poderia escrevê-los direto pra uma data antiga e resgatar de novo no mesmo
    período, já que a function só valida contra o que estiver GRAVADO no documento.
  - Cliente: `DailyRewardsService.cs` (novo) — `ClaimAsync(type)` chama a Cloud Function
    certa e aplica o saldo já persistido; `RefreshStatusAsync(uid)` sincroniza
    `PlayerEconomyState.*DiamondsAvailable`/`*NextResetUtc` a partir do Firestore, reaproveitando
    o MESMO truque de "hora do servidor sem Cloud Function" que `EnergyService` já usava
    (escrever um campo descartável com `FieldValue.ServerTimestamp` e ler de volta forçando
    `Source.Server`) — extraído pra `FirestoreService.ReadServerNowAsync` (generalizado por
    documento/campo) pra não duplicar a lógica de retry entre os dois serviços. Período/próximo
    reset calculados client-side com um offset FIXO de UTC-3 (documentado no código o porquê:
    Brasil sem DST hoje, e TimeZoneInfo teria IDs diferentes entre plataformas pro mesmo fuso
    IANA) — só pra decidir o que MOSTRAR; a decisão de crédito de verdade sempre revalida no
    servidor.
  - UI (`ShopController`/`ShopCardUI`): 3 novos cards no topo da aba Diamantes ("Resgate Diário/
    Semanal/Mensal"), botão "RESGATAR" (`ShopCardUI` ganhou um `buyLabel` customizável, era sempre
    "COMPRAR") — disponível: ativo; já resgatado: desabilitado + contagem regressiva viva
    (`CountdownLabel`, mesmo componente do timer de energia do Main Menu, reaproveitado em vez de
    escrever um polling próprio — recalcula a partir do timestamp de servidor, nunca do relógio
    do device) que se auto-corrige (re-sync + rebuild) assim que a contagem chega em zero.
  - **Bolinha vermelha reaproveitando o indicador já existente** ("Chibers Aleatório", ver entrada
    anterior) em vez de duplicar a lógica de exibição: `DailyRewardsService.AnyClaimAvailable`
    (OR dos 3 tipos) aparece na aba DIAMANTES, em cada botão individual disponível, e é agregada
    (OR) com `NextCharacterService.CanAffordNextPurchase()` no botão "LOJA" do Main Menu — o botão
    Loja agora significa "tem algo pra ver/pegar na Loja" de forma geral, não só um card
    específico. Reativo por reavaliação (recalculado a cada `RebuildGrid()`/`RefreshEconomyHuds()`
    dentro de cada controller), não um evento de "sumir ao resgatar".
  - Ver MONETIZACAO.md seção 15.
- 2026-07-27: **Indicador de "compra disponível" (bolinha vermelha) + cards de personagem
  renomeados pra "Chibers"** — pedido do usuário.
  - Renomeados: "Próximo Personagem" → "Chibers Aleatório", "Personagem Raro" → "Chibers Raro",
    "Personagem Legendary" → "Chibers Lendário", "Personagem Imortal" → "Chibers Imortal"
    (`ShopController.BuildItemData`). Novas descrições nos 4 cards (raro/lendário/imortal:
    "Sorteio garantido entre chibers X, sem repetição"; aleatório: odds reais das 5 raridades por
    extenso). Texto de quantidade restante ("X/Y restantes") inalterado — vem de `ShopCardUI`,
    independente do título/subtítulo.
  - Bolinha vermelha aparece simultaneamente no botão "LOJA" do Main Menu, na aba PERSONAGENS da
    Loja e no próprio card "Chibers Aleatório", sempre que o saldo de Coins já cobre o preço da
    próxima compra — reativa (recalculada a cada mudança de saldo dentro de cada controller, não
    um evento de "sumir ao comprar"). Tabela de preço/contador extraídos de `ShopController` pra
    `NextCharacterService` (`PriceTable`/`NextPurchaseCost`/`CanAffordNextPurchase`), reaproveitados
    tanto pela Loja quanto pelo Main Menu sem duplicar a tabela; `MainMenuController.
    RefreshEconomyOnMenuLoad` passou a carregar `NextCharacterPurchaseCount` também (antes só a
    Loja carregava). Ver MONETIZACAO.md seção 14.
- 2026-07-27: **Contador de moeda adicionado ao header da Loja (`06_Loja`)**, à esquerda do
  contador de diamante já existente — pedido do usuário pra dar pra conferir o saldo de moeda sem
  sair da tela (a Loja só mostrava diamante até agora; o card "Próximo Personagem" já mostra o
  PREÇO em moeda, mas não o SALDO). `ShopController.BuildCoinCounter` — mesmo estilo/tamanho do
  `BuildDiamondCounter` (chip com ícone+valor, `UI/Economy/Coin`), sem o efeito de "voando"
  (`FlyingDiamondIcon`) que o diamante tem, já que nada na Loja credita moeda com essa animação
  hoje. Atualizado nos 3 pontos onde `PlayerEconomyState.Coins` muda dentro do controller: load
  persistido do Firestore, botão DEV de teste (`#if UNITY_EDITOR`) e compra de "Próximo
  Personagem".
- 2026-07-27: **Cor de borda de tier de Skill/Arma/Pet unificada e realinhada à raridade de
  personagem (cinza/verde/azul p/ T1/T2/T3, era bronze/prata/ouro)** — a mesma lógica de cor por
  tier estava duplicada em 3 switches independentes (`CharacterPanel.TierColor`,
  `ArsenalSlotUI.TierColor`, `CharacterUnlockRevealPanel.TierColor`); centralizada num único
  método novo, `UITheme.TierColor(int tier)` (T1→`rarityNormal`, T2→`rarityUncommon`,
  T3→`rarityRare`, mesmos tokens já usados na raridade de `PlayerProfile`), com os 3 consumidores
  agora só delegando pra ele. T4/T5 (Legendary/Imortal, laranja/vermelho) reservados pra quando
  essas evoluções existirem de verdade — bastará adicionar `case 4`/`case 5` só nesse método.
  `tierBronze`/`tierSilver`/`tierGold` (`UITheme`) não foram removidos — continuam em uso pra cor
  da tag `WeaponType.Heavy` no popup de detalhe de arma, um uso independente da borda de tier.
  Aplicado em todos os locais que mostram essa borda: Main Menu/Chibers/`02_SelectCharacter`,
  botão Arsenal, reveal de case-opening/Renascimento e — novo, essa tela nunca teve indicação de
  tier nenhuma antes — a tela de escolha de skill/arma/pet no level-up
  (`CombatResultPanel.MakeLevelUpCard`, ganhou uma borda colorida atrás do ícone, ausente pra
  cartas de Atributo).
- 2026-07-27: **Feature antiga "Resetar Personagem" removida por completo** — o "Renascimento"
  (2026-07-26) tornou-a obsoleta, cobrindo o mesmo papel em todos os aspectos (reset pro Level 1 +
  crédito de moeda), com a vantagem de também conceder skills/armas/pets pela raridade. Removidos:
  o botão de UI, `CharacterPanel.OnResetCharacterClicked`/`ShowResetConfirmPopup`/`ExecuteReset`, e
  o ScriptableObject/asset `CharacterResetSettings` (`Assets/ScriptableObjects/
  CharacterResetSettings.cs` + `Assets/Resources/CharacterResetSettings.asset`). O botão
  "Renascimento" não reaproveitava nenhum código exclusivo da feature antiga (fluxos
  independentes desde o início — a antiga era 100% client-side, a nova é server-authoritative via
  Cloud Function), então nada precisou ser extraído antes de deletar.

- 2026-07-27: **Bug visual corrigido — giro da roleta (`CaseOpeningPopup`) saía com raridade
  homogênea** (ex: giro inteiro só Normal ou só Imortal), reportado pelo usuário depois de
  confirmar que o sorteio do PRÊMIO em si estava correto (68/20/8/3.5/0.5%, ver entrada anterior).
  Causa: `BuildReelSlotIds` preenchia os slots de giro sorteando uniformemente de
  `reelPoolCharacterTypeIds` — que nunca foi um pool multi-raridade, é o resultado de
  `rollWeightedPool` no servidor (a lista de personagens elegíveis DENTRO do tier já sorteado pro
  prêmio final), sempre homogêneo por raridade de propósito. Isso é o comportamento CERTO pros 3
  cards cash (Raro/Legendary/Imortal, `isRarityLocked`) — errado só pra "Próximo Personagem"
  (sorteio ponderado entre as 5 raridades). Corrigido com um novo parâmetro
  `CaseOpeningPopup.Show(..., weightedRarityFill: bool)`: `false` (cash, comportamento antigo
  preservado) vs. `true` (Próximo Personagem — cada slot sorteado independentemente com os MESMOS
  pesos `[68%, 20%, 8%, 3.5%, 0.5%]`, mirror client-side de `DEFAULT_TIER_WEIGHTS`, puramente
  decorativo — o prêmio final continua 100% decidido no servidor).

- 2026-07-26: **"Case Geral" (diamante) removido; "Próximo Personagem" virou compra real (Coins)**
  — aba Personagens da Loja. Investigação prévia corrigiu duas premissas erradas: (1) o fluxo
  IAP (Raro/Legendary/Imortal) não tem sorteio de raridade nenhum, cada card é travado numa
  raridade fixa — quem já fazia o sorteio ponderado entre as 5 raridades era o próprio Case
  Geral; (2) "Próximo Personagem" já existia como card na UI, mas era 100% decorativo (nenhuma
  moeda debitada, nenhum personagem concedido, contador só em memória, resetado a cada troca de
  cena).
  - Nova Cloud Function `purchaseNextCharacter` (`functions/src/purchaseNextCharacter.ts`):
    sorteia com as MESMAS odds que o Case Geral já usava — `[68%, 20%, 8%, 3.5%, 0.5%]`
    (Normal/Uncommon/Rare/Legendary/Immortal, `DEFAULT_TIER_WEIGHTS`) — e concede o personagem
    exatamente como `purchaseCase`. Preço escala por um contador PERSISTIDO por jogador
    (`users/{uid}.nextCharacterPurchaseCount`), não mais um preço fixo por pacote: **25 / 50 /
    100 / 200 / 400 / 800 / 1200 / 1400 / 1600 / 1800 / 2000 / 2200, depois +200 a cada compra**
    (substitui a tabela antiga do card decorativo, 100/200/400/600/800/1000/+400).
  - Refatoração de deduplicação: `secureRandomIndex`/`weightedRandomTier`/`rollWeightedPool`/
    `rarityOfCharacter` extraídos pra `functions/src/caseRoll.ts`; a concessão do documento de
    personagem (`DEFAULT_STARTING_WEAPONS`/shape do doc) extraída pra
    `functions/src/grantCharacter.ts` — `purchaseCase.ts`/`grantStarterCharacter.ts` refatorados
    pra importar as duas em vez de manter cópias próprias (nenhuma mudança de comportamento).
  - Client: `ShopController.cs` remove o card "Case Geral" por completo (`PackageIdMoedaGeral` e
    o pacote `casePackages/case_moeda_geral`, removido também do seed script); "Próximo
    Personagem" ganha `NextCharacterService.cs` (novo, mesmo padrão de `CaseService.cs`) e reusa
    o mesmo `CaseOpeningPopup` de reveal. Preço exibido agora vem do contador REAL
    (`PlayerEconomyState.NextCharacterPurchaseCount`, carregado via novo
    `WalletService.LoadNextCharacterPurchaseCountAsync`), não mais um contador de sessão.
  - `MONETIZACAO.md` seções 6/13 atualizadas com a tabela final e a remoção do Case Geral.

- 2026-07-26: **Renascimento também reabastece a energia de batalha** (pedido do usuário) — a
  Cloud Function `rebirthCharacter` agora grava `energyCurrent`/`lastEnergyTimestamp` (mesmos
  campos de `EnergyService.cs`) reabastecendo pro teto (`EnergySettings.maxEnergy`, 10) e
  reancorando o timestamp em "agora" (servidor), mesmo espírito da reancoragem que a regeneração
  natural já faz ao bater o teto. Primeira vez que este campo é escrito por uma Cloud Function
  (antes só o SDK client-side gravava energia direto).

- 2026-07-26: **Reveal do "Renascimento" movido pra 02_SelectCharacter** (pedido do usuário, "vai
  para a tela onde tem a splashart do personagem") — `CharacterPanel.ExecuteRebirthAsync` não
  revela mais os itens inline (onde o botão foi clicado); em vez disso aplica o resultado, salva,
  e navega pra `02_SelectCharacter` via o MESMO handoff `PendingCharacterSelection` que o case
  opening já usa (abre o detalhe do personagem automaticamente, com a splash art em tela cheia) +
  um canal novo `PendingRebirthReveal` (itens concedidos). `CharacterSelectController.
  OnCharacterSelected` consome os dois e roda a revelação em sequência reaproveitando a MESMA
  `CharacterUnlockRevealPanel`/instância já usada pelo reveal de case-opening (nenhum painel novo
  instanciado).

- 2026-07-26: **Correção de escopo — economia do "Renascimento" invertida** (mesmo dia da
  implementação original, ver entrada abaixo). Renascimento passou a ser **100% GRATUITO** — não
  custa mais Coins nem Diamantes; toda a validação/débito de saldo insuficiente foi removida da
  Cloud Function `rebirthCharacter` (o erro "Saldo de moedas insuficiente" não existe mais). Em
  vez de custar, agora **CREDITA** `nível_antes × CharacterRebirthSettings.coinRewardPerLevel`
  (10 por padrão, mesmo valor numérico de antes — só o campo mudou de nome, de `coinCostPerLevel`
  pra `coinRewardPerLevel`, e de DÉBITO pra CRÉDITO via `FieldValue.increment` positivo). Nada
  mais mudou: gate de level >= 10, concessão de N itens pela raridade, sorteio de stats base e
  reroll individual (15 diamantes fixo) continuam idênticos à versão original.

- 2026-07-26: **Nova feature "Renascimento" (Reset Nível 10+)** — distinta e coexistente com o
  "Resetar Personagem" antigo (`CharacterPanel.ExecuteReset`, que continua grátis, sem gate de
  level, credita moeda e limpa o loadout). O Renascimento é o oposto: só libera em level >= 10,
  DEBITA moedas (`nível × CharacterRebirthSettings.coinCostPerLevel`, 10 por padrão) via nova
  Cloud Function `rebirthCharacter` e CONCEDE N skills/armas/pets aleatórios pela raridade do
  personagem (Normal 1 / Uncommon 2 / Rare 3 / Legendary 4 / Immortal 5 — mesma tabela de
  `CharacterUnlockEngine.UnlockCountForRarity`), com status base também re-sorteados — tudo
  decidido 100% server-side (nunca no cliente). Cada item concedido pode ser rerolado
  individualmente depois (nova Cloud Function `rerollRebirthGrant`, custo fixo de 15 diamantes,
  máx. 2 usos por item — reaproveita a mesma lógica de `rerollUnlock` via um núcleo compartilhado
  novo, `functions/src/rerollShared.ts`, em vez de duplicar). UI: novo botão "RENASCIMENTO" em
  `CharacterPanel.cs`, logo abaixo de "RESETAR PERSONAGEM"; reaproveita o mesmo
  `CharacterUnlockRevealPanel` já usado pelo reveal de case-opening. Aproveitado pra corrigir
  também o TODO de segurança do "Novo Sorteio" (reroll das caixas de level-up de combate,
  `CombatResultPanel.cs`): custo deixou de dobrar (era 50→100→200 diamantes, gasto direto do
  cliente) e passou a ser FIXO (15 diamantes, mesmo valor de `rerollUnlock`), validado/debitado
  sempre no servidor (nova Cloud Function `rerollLevelUpBoxes`) — o sorteio das caixas em si
  continua client-side, por decisão deliberada de escopo (ver comentário no arquivo da function).

- 2026-07-25: **Bug real — o personagem certo não aparecia selecionado no menu principal depois
  de parar e reiniciar o Play Mode (ou abrir o Play direto em `01_MainMenu`, pulando `00_Login`)**
  (reportado pelo usuário). Causa, diferente dos bugs de cache acima: `SelectedProfileHolder` é um
  ScriptableObject ASSET, e a Unity reverte QUALQUER mutação feita nele durante o Play Mode assim
  que ele para — isso SEMPRE existiu, não tem relação com o Domain Reload (`SetProfile()` só muda
  o objeto em memória, nunca grava de volta no `.asset` em disco). `currentProfile`/`characterId`
  voltavam pro default serializado do asset (`Medieval Warrior`/vazio) toda sessão nova, mesmo pra
  conta com personagem de verdade escolhido — `MainMenuController.
  ReconstructSelectedProfileIfMissingAsync` (guard antigo) só cobria `currentProfile == null`, que
  na prática quase nunca era verdade (o default do asset não é nulo, só é o personagem ERRADO), e
  por isso quase nunca disparava. Corrigido com `PlayerProfileConverter.EnsureValidSelection`
  (novo método compartilhado) — valida se `currentProfile` corresponde a um personagem que a conta
  REALMENTE possui (por `characterId` contra o roster do Firestore) e, se não, reconstrói o 1º
  personagem do roster como fallback; chamado tanto em `LoginController.OnAuthSuccessRoutine`
  (fluxo normal) quanto em `MainMenuController.InitializeAsync` (cobre abrir o Play Mode direto
  nesta cena, sem passar pelo login). `ReconstructSelectedProfileIfMissingAsync` removido
  (substituído pela versão unificada). Não cobre o doc "legado" sem `characterTypeId`
  (pré-2026-07-23) — mesma exceção documentada desde a migração original.

- 2026-07-25: **Bug real — botões/fundos de TODAS as telas apareciam brancos depois de parar e
  reiniciar o Play Mode sem fechar o Editor** (reportado pelo usuário, efeito colateral direto de
  ter desligado o Domain Reload no Play Mode nesta mesma sessão, ver entrada de
  `ProjectSettings/EditorSettings.asset` abaixo). `UIShapeUtil.cs` (gera em runtime todo sprite
  procedural de retângulo arredondado/gradiente/estrela/triângulo usado por praticamente qualquer
  botão/painel construído via código no jogo) cacheia esses `Sprite`s em `Dictionary`s estáticos
  sem checar validade — com Domain Reload ligado, esse cache era limpo de graça a cada sessão de
  Play; desligado, o cache sobrevive entre sessões, mas os `Sprite`/`Texture2D` da sessão anterior
  já foram destruídos pela própria Unity ao sair do Play Mode (objetos criados em runtime não
  sobrevivem à troca de volta pro Edit Mode) — `TryGetValue` continuava achando a entrada (a chave
  nunca expira) e devolvia um sprite morto, renderizando branco. Corrigido checando `!= null` no
  valor cacheado nos 4 caches do arquivo antes de reaproveitar (Unity detecta objeto destruído
  mesmo com referência C# não-nula) — se inválido, regenera do zero como se fosse cache miss.

- 2026-07-25: **Bug real — `RosterService.SessionCache` podia vazar personagem de uma conta de
  teste antiga pra uma conta nova, na MESMA sessão do Editor** (mesma causa-raiz do bug acima:
  Domain Reload desligado). Ficou mais grave ainda pelo fluxo de teste do usuário
  (`LIMPEZA_BASE.md`) — apagar a conta direto pelo Firebase Console (em vez de "Sair da Conta" no
  próprio app) nunca passa por `MainMenuController.OnLogoutClicked`, então nem a limpeza de estado
  que já existia pra logout normal rodava. Corrigido escopando a chave do cache por
  `(uid, characterId)` em vez de só `characterId` — um characterId da conta antiga nunca mais é
  devolvido pra uma leitura de uid diferente, não importa se a conta antiga foi apagada pelo app
  ou direto no Console. `PlayerProfileConverter._pristineSnapshots`/`_ownerScope` revisados e
  confirmados já seguros (só rastreiam PlayerProfile pré-autorado, nunca instância runtime de
  roster — instância nova a cada conta, sem chave reaproveitável entre contas).

- 2026-07-25: **Bug real — `ArgumentOutOfRangeException` em `SimulatePetHit` travava a luta inteira
  quando Hypnosis (ou Mimic copiando Hypnosis) roubava um pet inimigo** (reportado pelo usuário).
  `BuildInitiativeRoster()`/`RunInitiativeLoop` monta a fila de iniciativa uma única vez no início
  da luta, com dono fixo por pet — Hypnosis troca o dono de verdade em pleno combate
  (`defender.pets.RemoveAt`/`attacker.pets.Add`) sem reconstruir a fila; a entrada antiga
  continuava agendando o turno do pet pro dono ORIGINAL, e `petOwner.pets.IndexOf(pet)` devolvia
  -1 quando disparava (pet não está mais nessa lista), estourando o índice em
  `petOwner.pets[petIndex]`. Corrigido com um guard em `SimulatePetTurn` (`CombatSimulator.cs`) —
  pet roubado simplesmente para de agir pro resto da luta (some da ordem de iniciativa) em vez de
  travar a simulação; não resolve a ordem de iniciativa do pet sob o NOVO dono (limitação
  conhecida, exigiria reconstruir a fila em runtime — fora do escopo deste fix).

- 2026-07-25: **Bug real — `SelectedProfileHolder`/`SelectedOpponentHolder` perdiam o personagem
  selecionado em runtime (voltavam pro default serializado no `.asset`) só de visitar `06_Loja` e
  voltar.** Diagnosticado com log de `instanceID` (confirmou: mesmo objeto holder, `currentProfile`
  e `characterId` revertidos juntos pro valor gravado em disco, sem nenhum `SetProfile` chamado no
  meio) — `SceneManager.LoadScene` em modo Single roda `Resources.UnloadUnusedAssets`
  implicitamente a cada troca de cena; `06_Loja`/`ShopController` é a única cena do fluxo sem
  nenhuma referência serializada a esses dois assets (vivem em `Assets/Resources/`), tornando-os
  elegíveis pra descarregar nesse intervalo. Corrigido com `hideFlags |=
  HideFlags.DontUnloadUnusedAsset` no `OnEnable()` dos dois — protege incondicionalmente,
  independente de qual cena referencia o asset no momento.

### Progresso
- Total: 144 tarefas | Concluídas: 54 (2026-07-25: +1, "Onboarding — escolha do 1º personagem"
  fechada por completo)

- 2026-07-25: **Onboarding — escolha do 1º personagem implementada.** Nova cena
  `ChooseFirstCharacter` (`ChooseFirstCharacterController.cs`, mesmo padrão 100%-via-código de
  `LoginController`/`ArsenalController`), carregada por `LoginController.OnAuthSuccessRoutine`
  sempre que uma conta loga sem NENHUM personagem no roster (`RosterService`) — cobre sign-up,
  sign-in, Google e auto-login por igual. Grid 1×4 horizontal (Medieval Warrior, Medieval Warrior
  Girl, Citizen 1, Citizen Women 2 — os 4 já existiam como `PlayerProfile`/prefab completos, não
  foi criado nenhum ScriptableObject novo), tap pra destacar (`PressableCard.cs`, mesmo padrão de
  `05_SelectOpponent`) + botão "Confirmar" separado. Confirmar chama a nova Cloud Function
  `grantStarterCharacter` (`functions/src/grantStarterCharacter.ts`, região `southamerica-east1`,
  deliberadamente separada de `purchaseCase` pra não acoplar um fluxo grátis num fluxo com
  pagamento/diamante) — servidor valida que a conta ainda não possui nenhum personagem, grava
  `users/{uid}/characters/{id}` com stats fixos por template e sorteia a 1ª skill (server-side,
  não manipulável): pondera pelos odds reais do catálogo (`unlockCatalog.json`) pra achar 2
  candidatos tier-1 distintos, depois decide entre os dois com uma moeda justa
  (`crypto.randomInt`). Handoff pro cliente reaproveita 100% do canal já existente do case opening
  (`PendingCharacterSelection` → `02_SelectCharacter` → `ResolvePendingCharacterSelectionAsync`) —
  nenhum código novo do lado de lá. Documento gravado com `caseUnlocksResolved: true` de propósito
  (senão `CharacterSelectController.ResolveCaseUnlocksAsync` concederia um 2º item não pedido, o
  "bônus de boas-vindas" de 1 unlock que todo personagem Normal ganha do case opening).
  `Medieval Warrior.asset` revertido pra `isPlayable: false` (era `true` desde 2026-07-14 como
  único personagem "de teste" jogável do roster inteiro — não é mais caso especial; jogabilidade
  agora vem só de instância possuída no roster, igual a qualquer outro personagem). Botão "Pular
  (offline)" removido de `00_Login` (pedido do usuário — sem uma conta com personagem concedido
  não há mais nada jogável, o botão não levaria a lugar nenhum). **Migração manual pendente**:
  contas de teste anteriores a esta mudança têm `characters/Medieval Warrior` com
  `characterTypeId` vazio (padrão "personagem original" pré-2026-07-23) — precisam de backfill
  manual desse campo (ou simplesmente recriar a conta) pra continuarem jogáveis, já que o merge de
  roster só reconhece documentos com `characterTypeId` preenchido.

- 2026-07-25: **Bug real — `grantStarterCharacter` respondia `unauthenticated` mesmo com o
  jogador logado normalmente.** Causa era na camada de IAM do Cloud Run, não no código: o 1º
  deploy dessa function (recém-criada) não aplicou o binding `roles/run.invoker` pra `allUsers`
  (`firebase functions:log` mostrou "The request was not authorized to invoke this service" —
  rejeitado antes de chegar no `request.auth` da function), que o SDK do Unity traduz como
  `FunctionsErrorCode.Unauthenticated`, mascarando a causa real como se fosse sessão expirada.
  Corrigido reimplantando só essa function (`firebase deploy --only functions:grantStarterCharacter`),
  reaplicando o binding.

- 2026-07-25: **Bug real — conta nova ficava com `SelectedProfileHolder` apontando pro personagem
  errado (default do asset, "Medieval Warrior") depois de escolher o 1º personagem no onboarding,
  se o jogador fechasse o painel de detalhe em `02_SelectCharacter` em vez de clicar
  "Selecionar".** Causava "Missing or insufficient permissions" ao ler energia (personagem que a
  conta não possui de verdade no Firestore) e mostrava o personagem errado no menu até o jogador
  trocar manualmente pela seta lateral. Raiz: "Fechar"/"Voltar" nunca gravam
  `SelectedProfileHolder` de propósito (só "Selecionar" grava — comportamento correto pro fluxo de
  case opening, onde só *ver* um personagem novo sem virar o ativo é válido), mas uma conta nova
  não tem nenhuma seleção anterior válida pra preservar. Novo flag
  `PendingCharacterSelection.AutoConfirmSelection`, setado só por `ChooseFirstCharacterController`
  (nunca por `CaseOpeningPopup`), faz `CharacterSelectController.ResolvePendingCharacterSelectionAsync`
  confirmar automaticamente o personagem concedido assim que o encontra, independente de qual
  botão o jogador clicar depois.

- 2026-07-25: **Energia não atualizava ao trocar de personagem pela seta/arraste no menu
  principal (bug real, reportado pelo usuário — ex: trocar pro "Medieval Warrior", com 7 de
  energia, pra outro personagem nunca jogado ainda mostrava "7" também)** — energia é POR
  PERSONAGEM (`users/{uid}/characters/{characterId}`, ver `EnergyService`), mas
  `PlayerEconomyState.EnergyCurrent` é um cache único/global, recarregado só no login ou ao abrir
  o menu — `MainMenuCharacterPreview.SwitchCharacter` nunca buscava a energia do personagem NOVO,
  só reconstruía a fileira de ícones com o valor global (do personagem anterior) ainda em cache.
  Corrigido chamando `MainMenuController.RefreshEconomyOnMenuLoad()` (já lê
  `selectedProfileHolder.currentProfile.OpponentId()`, atualizado pelo `SetProfile` do próprio
  `SwitchCharacter`) a cada troca — mesmo re-fetch que já roda ao carregar o menu.

- 2026-07-25: **2 bugs reais corrigidos, achados testando a resiliência do level-up (entrada
  seguinte)**:
  1. **NullReferenceException em `CharacterPanel.RefreshAll()`/`MainMenuController.Start()`** —
     `SelectedProfileHolder.currentProfile` pode ser uma instância RUNTIME (personagem de case
     opening, `isRuntimeInstance=true`) sem referência estável entre sessões (`characterId`, campo
     do holder, já documentava essa limitação, mas a reconstrução nunca tinha sido implementada) —
     se a conta ativa era um desses personagens quando o app fechou, `currentProfile` chegava nulo
     na sessão seguinte e travava `characterPanel.Setup()` no carregamento do menu. Duas correções:
     - `MainMenuController.Start()` virou `InitializeAsync()` (chamado via `_ = InitializeAsync()`
       em `Start()`) — novo `ReconstructSelectedProfileIfMissingAsync()` roda ANTES de
       `characterPanel.Setup()`: tenta achar o personagem ORIGINAL/molde por nome em
       `characterDatabase.unlockedCharacters` primeiro; se não achar (é um personagem de ROSTER),
       busca `RosterService.GetOwnedCharacterDocAsync` + `PlayerProfileConverter.FromCharacterDTO`
       (mesmo padrão já usado por `CharacterSelectController`/`MainMenuCharacterPreview`).
     - **`CharacterPanel.RefreshAll()` tinha um guard de `p == null` que também travava** —
       assumia `_compactInfo.name`/`_expandedInfo.name` sempre existirem, mas no modo
       `_bottomAnchored` (gaveta mobile, único uso: `MainMenuController`) esses campos ficam
       `null` de propósito (ver "Gaveta mobile" em CLAUDE.md). Corrigido com null-check antes de
       escrever `.text`, eliminando a exceção mesmo se a reconstrução acima falhar por qualquer
       outro motivo (ex: sem conta/rede).
  2. **Painel de detalhe do personagem aparecia aberto no centro da tela ao retomar a escolha de
     level-up, em vez do botão "i" colapsado no canto superior direito** —
     `CombatResultPanel.ResumePendingLevelUpChoiceIfAny` usava `FindScreenCanvas()` (primeiro
     Canvas `ScreenSpaceOverlay` encontrado por `FindObjectsOfType`, sem ordem garantida) como pai
     da tela de escolha inteira; seguro em `04_CombatScenePVP` (só o Canvas do `CombatHUD`
     existe), mas em `01_MainMenu` (onde a retomada roda) já existem vários Canvas concorrentes
     (`CharacterPanel` bottomAnchored, `CurrencyHud`, HUDs de Level/XP/Energia) — reusar um deles
     tornava a ordem de desenho dependente de sibling index dentro de um Canvas alheio. Corrigido
     criando um Canvas raiz DEDICADO (`sortingOrder=1000`, abaixo do popup de detalhe da própria
     tela de escolha, que já usa 1500) só para a retomada, destruído junto quando o jogador
     finalmente escolhe uma caixa.
  3. **Regressão do próprio fix do item 2 acima, achada no teste seguinte do usuário — as 5
     caixas de escolha "estouraram" a tela** — o Canvas dedicado criado no item 2 ganhou `Canvas`+
     `GraphicRaycaster`, mas faltou o `CanvasScaler` (`ScaleWithScreenSize`, referenceResolution
     1920×1080) que TODO outro Canvas do projeto tem (`LoginController`/`MainMenuController`/
     `CharacterPanel`/etc.) — sem ele, o Canvas cai no modo default "Constant Pixel Size": cards
     de 270-405px (`CardScale=1.5`) renderizavam em pixels BRUTOS de tela, sem escalar pra caber,
     em vez de unidades de referência 1920×1080 escaladas pro tamanho real da tela/janela. O
     Canvas que `FindScreenCanvas()` encontrava ANTES do fix do item 2 sempre tinha essa config
     "de graça" por ser um Canvas já existente da cena — o Canvas dedicado novo não herda nada
     automaticamente. Corrigido adicionando o `CanvasScaler` que faltava.

- 2026-07-25: **Resiliência do level-up de combate a fechamento abrupto do app implementada**
  (investigação prévia confirmou cenário (b): XP/level/+2 HP automático/`battlesRemaining` e a
  escolha de skill/arma/pet/status ficavam represados em memória até o jogador escolher uma
  caixa — fechar o app no meio da tela "Escolha 1 bônus" perdia a luta inteira, sem nenhuma
  lógica de retomada existente). Desenho aprovado antes de implementar (ver histórico da
  conversa) — dois pontos de save:
  - **`AttackSequencer.OnCombatEnd`**: o `if (!result.didLevelUp)` que guardava o save some — salva
    SEMPRE agora, incondicional, logo após `XpSystem.AddXP` (antes até de `CombatResultPanel` ser
    instanciado). `battlesRemaining`/`xpCurrent`/`level`/o +2 HP automático nunca dependeram da
    escolha pendente, só ficavam represados por cautela — `XpSystem.cs` também teve o comentário
    (agora desatualizado) corrigido. Novo campo `PlayerProfile.hasPendingLevelUpChoice` é setado
    (`= result.didLevelUp`) na mesma gravação.
  - **`CombatResultPanel.ShowLevelUpChoice`/`DrawAndBuildCards`**: assim que as N caixas são
    sorteadas (1ª vez OU a cada "Novo Sorteio"), persiste o rascunho em
    `PlayerProfile.pendingLevelUpBoxes` (kind/name/tier de cada caixa — mesmo shape de
    `CharacterUnlockEngine.ToServerShape`, mas cobrindo também `Kind.Attribute`, que os unlocks do
    case opening nunca produzem) + `pendingLevelUpRerollsUsed`, ANTES de montar qualquer card na
    tela. `ApplyBonus` limpa os três campos (`hasPendingLevelUpChoice`/`pendingLevelUpBoxes`/
    `pendingLevelUpRerollsUsed`) na mesma chamada de save que já aplicava o bônus escolhido —
    único ponto em todo o sistema que os zera.
  - **Retomada**: novo `CombatResultPanel.ResumePendingLevelUpChoiceIfAny(profile, theme)`,
    chamado por `MainMenuController.Start()` a cada carregamento do menu — se
    `hasPendingLevelUpChoice` estiver true, resolve `pendingLevelUpBoxes` de volta pro
    `LevelUpOption` real (`ResolvePendingBox`, usando `SkillDatabase`/`WeaponDatabase`/
    `PetDatabase` via `Resources.Load`, já que fora da cena de combate não há Inspector wireando
    isso) e reabre a MESMA tela de escolha (`ShowLevelUpChoice` ganhou os parâmetros opcionais
    `resumeOptions`/`resumeRerollsUsed`) — mesmas caixas, mesmo progresso de custo do "Novo
    Sorteio" (não reinicia pra 50 diamantes). Bloqueio do menu (Jogar/Chibers/etc.) é automático:
    o overlay opaco que a tela de escolha já constrói (`raycastTarget=true`, cobre a tela
    inteira) já intercepta qualquer clique atrás dele, sem precisar desabilitar cada botão à
    parte. Se os dados salvos não resolverem por completo (ex: catálogo mudou entre fechar e
    reabrir), cai pro sorteio fresco de novo em vez de arriscar mostrar caixas incompletas —
    mesma rede de segurança já usada no case opening.
  - **`ShowAllOptionsForTesting` desligada de novo** (`CombatResultPanel.cs`, `true` → `false`,
    pedido do usuário) — precisava estar `false` pra testar o item 3 do plano (progressão de
    custo do "Novo Sorteio" sobrevivendo a fechar/reabrir o app), que só existe na tela real em
    pirâmide, não na grade de teste com todas as opções. Religar se precisar testar skill nova
    sem ícone/tier de novo (ver histórico da constante).

- 2026-07-25: **2ª rodada da retomada de unlocks — a "limitação conhecida" registrada na entrada
  anterior aconteceu de verdade no teste do usuário e foi corrigida**: sorteou "Book", fechou o
  app antes de aceitar, reabriu e veio um resultado DIFERENTE ("Vampirismo") em vez de continuar
  mostrando "Book"; e o contador de refresh reiniciava mostrando "2 disponíveis" mesmo já tendo
  usado 1 antes de fechar, causando um "resource-exhausted" inesperado (com o botão ainda
  habilitado) ao tentar de novo. Causa: só `caseUnlocksAcceptedCount` (unlocks JÁ ACEITOS) era
  persistido — o RASCUNHO do unlock em andamento (ainda não aceito) só existia na variável local
  `option`, perdida ao fechar o processo; o contador de refresh (`remainingRerolls`) também só
  vivia no client, resetado pra `MaxRerollsPerUnlock` a cada novo sorteio, sem nunca checar o que
  o servidor (`unlockRerollCounts`) já tinha de verdade. Fix: novos campos
  `PlayerProfile.pendingUnlockIndex/Kind/Name/Tier/RerollsUsed` (+ round-trip completo em
  `CharacterDTO`/`CharacterDTOMap`/`PlayerProfileConverter.ToDTO`**e**`ApplyDTO`) persistem o
  rascunho atual (mesmo shape kind/name/tier que o servidor usa, ver novo
  `CharacterUnlockEngine.ToServerShape`) IMEDIATAMENTE após cada sorteio/refresh, antes mesmo de
  mostrar o painel — se `pendingUnlockIndex` já bate com o unlock atual ao entrar no loop
  (`ResolveCaseUnlocksAsync`), resolve esse mesmo rascunho de volta (`ResolveServerResult`) em vez
  de sortear um novo, e restaura `remainingRerolls` a partir do `pendingUnlockRerollsUsed`
  salvo — nunca mais reinicia a contagem à toa. Rascunho é limpo (`ClearUnlockDraft`) só quando o
  unlock é de fato aceito.

- 2026-07-25: **Bug real corrigido — fechar o app no meio da sequência de unlocks perdia os
  unlocks restantes pra sempre** (pergunta do usuário: "caso ele feche o jogo na 1ª sorte de
  skill, perde toda as skills futuras que tinha pra receber?" — resposta era sim, antes deste
  fix). Causa: `ResolveCaseUnlocksAsync` só era chamado uma vez, no caminho específico de
  `PendingCharacterSelection` logo após a compra — `PendingCharacterId` já tinha sido consumido
  nesse momento, então reabrir o personagem depois (mesmo clicando normalmente na grade) nunca
  tentava de novo. Fix: `OnCharacterSelected` agora dispara/retoma os unlocks sozinho toda vez que
  o detalhe de um personagem do roster (`isRuntimeInstance`) com `!caseUnlocksResolved` é aberto —
  cobre tanto o caminho pós-compra quanto reabrir depois. Retomar do 1º unlock sempre incorreria
  em duplicar os já aceitos (não dá pra usar `skills.Count+weapons.Count+pets.Count` como proxy —
  um unlock que evolui uma família já possuída não aumenta esse total), então novo campo
  `PlayerProfile.caseUnlocksAcceptedCount` (+ `CharacterDTO`/`CharacterDTOMap`/
  `PlayerProfileConverter.ToDTO`**e**`ApplyDTO`, os dois sentidos desta vez — ver bug do
  `characterTypeId` mais acima no dia) é persistido a cada "Continuar" aceito e usado como ponto
  de retomada do loop (`for (i = acceptedCount + 1; i <= total; i++)`). **Limitação conhecida,
  não corrigida agora**: se o app fechar depois de usar 1+ refresh num unlock mas antes de aceitar
  esse mesmo unlock, o contador de refresh no SERVIDOR (`unlockRerollCounts`) continua contando
  o(s) uso(s) anterior(es), mas o CLIENTE reinicia a UI mostrando 2 refreshes disponíveis de novo
  ao retomar — cosmético, não é brecha de segurança (o servidor sempre recusa corretamente se o
  limite real já tiver sido atingido).

- 2026-07-25: **Melhoria de UI no refresh dos unlocks** (pedido do usuário) — contagem de
  refreshes restantes agora aparece direto no próprio botão ("Refresh (15 diamantes) — N
  restante(s)"), não só na linha de status abaixo. Também corrigido: label usava o emoji 💎, que
  não existe na fonte TMP do projeto e renderizava como quadrado vazio (mesmo bug já visto antes
  com ★) — trocado por "diamantes" por extenso, igual ao resto do jogo (`CombatResultPanel`
  "Novo Sorteio (X diamantes)").

- 2026-07-25: **Bug real corrigido — `rerollUnlock` falhava com `UNAUTHENTICATED` na 1ª tentativa
  de uso** (reportado pelo usuário testando o refresh pela primeira vez). Causa: não era erro de
  código — o Cloud Run subjacente à function recém-criada rejeitava a chamada antes mesmo dela
  chegar no nosso `request.auth` ("The request was not authorized to invoke this service"), porque
  a permissão de invocação pública (que o `firebase deploy` normalmente configura sozinho pra toda
  function `onCall`) não foi aplicada na criação inicial — `purchaseCase` (function já existente,
  só atualizada) não sofria disso. Resolvido reimplantando só `rerollUnlock`
  (`firebase deploy --only functions:rerollUnlock`) — o redeploy reaplicou a permissão
  corretamente.

- 2026-07-25: **Refresh dos unlocks progressivos do case opening implementado** (pedido do
  usuário) — cada unlock revelado (skill/arma/pet) pode ser resorteado até 2 vezes antes de
  aceitar, custo FIXO de 15 diamantes por uso (não escala, deliberadamente separado do "Novo
  Sorteio" do level-up de combate, que dobra a cada uso — dois sistemas econômicos distintos, sem
  lógica de custo compartilhada). Servidor-autoritativo de ponta a ponta, seguindo a regra
  inegociável de `ARQUITETURA.md` "Moeda premium": nova Cloud Function `rerollUnlock`
  (`functions/src/rerollUnlock.ts`) valida saldo de diamante e o limite de 2 refreshes (contador
  `unlockRerollCounts` no próprio documento do personagem, nunca só client-side — não burlável) e
  resorteia usando as MESMAS regras já implementadas (família ponderada por odds + tier por
  posse, ver `functions/src/unlockEngine.ts`, porta 1:1 de `CharacterUnlockEngine.DrawUnlock`),
  tudo numa única transaction atômica com o débito de diamante. Como a function roda em Node sem
  acesso aos ScriptableObjects do Unity, novo `functions/src/unlockCatalog.json` (espelho de
  odds/tiers reais de skill/arma/pet, gerado por `Tools > AutoArms > Export Unlock Catalog for
  Cloud Function`, `Assets/Editor/UnlockCatalogExporter.cs`) — mesmo papel de
  `characterCatalog.json` pra `purchaseCase`, também começa vazio até a 1ª exportação.
  `CharacterSelectController.ResolveCaseUnlocksAsync` reestruturado: cada unlock agora é um
  RASCUNHO (sorteado mas só aplicado a `match.skills/weapons/pets` quando "Continuar" é clicado)
  — refresh troca o rascunho sem nunca ter tocado o personagem de verdade, o que também garante
  que um rascunho descartado nunca conta como "possuído" pro próximo unlock nem pro próprio
  `rerollUnlock` (que decide o tier lendo o documento do personagem no Firestore). Novo
  `UnlockRerollService.cs` (client da function, mesmo padrão de `CaseService`/`purchaseCase`) e
  `CharacterUnlockEngine.ResolveServerResult` (resolve nome+tier devolvidos pelo servidor pro
  `SkillData`/`WeaponData`/`PetData` real via os databases locais). `CharacterUnlockRevealPanel`
  ganhou botão "Refresh (15💎)" ao lado de "Continuar", linha de status (refreshes
  restantes + saldo de diamante conhecido), estado ocupado durante a chamada e mensagem de erro
  transitória se o servidor recusar (saldo insuficiente/limite atingido) sem travar o fluxo —
  botão some por completo quando os 2 refreshes acabam. **Pré-requisito antes de testar**: rodar
  `Tools > AutoArms > Export Unlock Catalog for Cloud Function` no Editor (catálogo começa vazio)
  e `firebase deploy --only functions` — sem isso `rerollUnlock` sempre falha com "internal"
  (nenhum candidato no pool), comportamento seguro por padrão.

- 2026-07-25: **Bug real corrigido — personagem comprado via case opening sempre vinha com os
  stats BASE mínimos (55 HP/2 STR/2 AGI/2 SPD), sem a distribuição aleatória de 9 pontos que
  "Tools > AutoArms > Reset All Profiles to Level 1" e todo personagem pré-autorado do projeto
  sempre tiveram** (reportado pelo usuário). Não era um bug — a 1ª versão de `purchaseCase.ts`
  (2026-07-23) já documentava isso no próprio código como decisão deliberada: "sem RNG de stats no
  servidor nesta primeira versão". Fix: nova `generateLevel1Stats()` em `purchaseCase.ts`,
  réplica exata de `CharacterCreation.GenerateLevel1Stats()` (`Assets/Scripts/Utils/
  CharacterCreation.cs`) — distribui 9 pontos aleatórios entre HP (+5/ponto), STR/AGI/SPD
  (+1/ponto cada), 25% de chance cada por ponto, sem teto por atributo — usando
  `crypto.randomInt` (mesmo padrão de segurança já usado no resto da function, nunca
  `Math.random()` pra nada que decide recompensa do jogador). Precisa de `firebase deploy --only
  functions` pra valer em produção (editar o `.ts` local nunca implanta sozinho — mesma lição já
  aprendida antes nesta mesma function).

- 2026-07-25: **Causa raiz REAL do bug "personagem comprado volta a aparecer bloqueado"
  encontrada (4ª rodada) — bug próprio, introduzido na implementação dos unlocks desta mesma
  sessão, não era cache/consistência do Firestore**. Instrumentação temporária
  (`[RosterDebug]`, `Debug.Log` em `RosterService.ListOwnedCharacterDocsAsync`/`CacheDto` e
  `CharacterSelectController.LoadRosterAndRefreshGridAsync`) confirmou no Console do usuário: o
  documento `users/{uid}/characters/{characterId}` do personagem comprado tinha `characterTypeId`
  correto ("Valkyrie 1") na 1ª leitura (logo após a compra), e **vazio** na 2ª leitura (mesma
  sessão, via botão CHIBERS) — o campo estava sendo apagado de verdade no Firestore, não só lido
  de um cache desatualizado. Causa: `PlayerProfile.characterTypeId` (novo campo desta sessão, ver
  entrada "Unlocks progressivos..." acima) foi preenchido em `PlayerProfileConverter.
  FromCharacterDTO` (leitura), mas **esquecido em `ToDTO`** (escrita) — todo `LocalSaveService.
  Save(profile)` (chamado a cada unlock concedido em `ResolveCaseUnlocksAsync`, exatamente a
  sequência que roda logo após a compra) reconstruía o `CharacterDTO` sem esse campo e
  regravava o documento inteiro com `characterTypeId` vazio, apagando a própria marca que
  `LoadRosterAndRefreshGridAsync` usa pra reconhecer o personagem como concedido via case opening.
  Fix: `characterTypeId = profile.characterTypeId` adicionado em `PlayerProfileConverter.ToDTO`.
  O `Source.Server`/cache de sessão das rodadas anteriores (`RosterService`) continuam válidos
  como defesa adicional contra inconsistência real de leitura, mas não eram a causa deste bug
  específico — mantidos, instrumentação de diagnóstico removida. **Nota**: personagens já
  concedidos ANTES deste fix (ex: o "Valkyrie 1" usado no diagnóstico) já têm o documento
  corrompido no Firestore — precisam de correção manual do campo `characterTypeId` no Console ou
  de uma nova compra pra validar o fix; não é retroativo sozinho.

- 2026-07-25: **3ª rodada do bug "personagem comprado volta a aparecer bloqueado" — o
  `Source.Server` sozinho (rodada anterior) não foi suficiente** (reportado pelo usuário com
  repro passo a passo: compra "Anubis" (Raro), reveal→Continuar mostra ele desbloqueado
  corretamente na grade, mas "Voltar" pro menu → "CHIBERS" de novo mostra Anubis travado — mesma
  sessão, sem fechar o app). Investigação confirmou que os dois caminhos (entrar vindo do case
  opening e entrar vindo do botão CHIBERS) são o MESMO código
  (`CharacterSelectController.Start()` → `LoadRosterAndRefreshGridAsync` →
  `RosterService.ListOwnedCharacterDocsAsync`, já que `02_SelectCharacter` é recarregada do zero
  via `SceneManager.LoadScene` nos dois casos, nunca reaproveitando a instância anterior) — não
  havia dois caminhos divergentes, era a MESMA query de listagem não sendo confiável de forma
  consistente mesmo com `Source.Server` (sem garantia formal de que uma query AGREGADA reflita um
  doc criado poucos segundos antes por outro processo — a Cloud Function via Admin SDK — tão
  rápido/consistentemente quanto um `get()` direto por ID). Fix definitivo: novo cache de SESSÃO
  em `RosterService` (`characterId → CharacterDTO`, estático, nunca limpo entre cenas — só reseta
  ao fechar o app, mesmo padrão de `PendingCharacterSelection`/`SelectedProfileHolder`) — todo
  personagem que este device já confirmou possuir nesta sessão (via listagem OU via
  `GetOwnedCharacterDocAsync`) fica cacheado e é sempre MESCLADO no resultado de
  `ListOwnedCharacterDocsAsync` dali em diante — uma vez visto corretamente, nunca mais
  "desaparece" de uma leitura futura nesta sessão, independente do que a query de listagem
  devolva. Cache é sempre atualizado/sobrescrito a cada leitura bem-sucedida (sem stats
  desatualizados presos além do necessário).

- 2026-07-25: **2ª rodada do bug "personagem comprado aparece desabilitado" — o fix anterior
  (deduplicar molde vs. instância do roster) não resolveu, porque o roster nem chegava a listar o
  personagem** (reportado pelo usuário: comprou, fechou o overlay, voltou pro menu, clicou em
  "Chibers" de novo — personagem continuava travado). Causa raiz de verdade:
  `RosterService.ListOwnedCharacterDocsAsync` (a query que alimenta a grade inteira) chamava
  `GetSnapshotAsync()` sem `Source.Server` — o personagem é criado pela Cloud Function
  `purchaseCase` via Admin SDK, que nunca passa pelos listeners/sync normais do SDK client-side;
  sem forçar o servidor, essa LISTAGEM podia continuar devolvendo o cache local persistente
  (`FirestoreService.PersistenceEnabled = true`) indefinidamente, não só no instante seguinte à
  compra — mesma causa raiz já identificada e corrigida antes só pro caso pontual de
  `GetOwnedCharacterDocAsync` (fallback do fluxo de compra), mas nunca aplicada à query de
  LISTAGEM geral, que é a que realmente popula a grade em qualquer reabertura normal da tela. Fix:
  `Source.Server` explícito também em `ListOwnedCharacterDocsAsync`. O fix da rodada anterior
  (`PlayerProfile.characterTypeId` + dedup do molde travado) continua válido e necessário — os
  dois bugs eram reais e distintos, um mascarando o outro no teste.

- 2026-07-25: **Bug real corrigido — personagem comprado via case opening aparecia "desabilitado"
  na grade de `02_SelectCharacter`** (reportado pelo usuário depois de fechar o overlay de
  detalhe e reabrir a tela pelo botão "Chibers"). Não era o personagem comprado em si — era um
  card DUPLICADO e visualmente idêntico: quando o `characterTypeId` sorteado pelo case opening
  bate com um dos 72 moldes pré-autorados do projeto (ex: comprou e caiu em "Anubis"), o molde
  em si (`characterDatabase.unlockedCharacters`, sempre `isPlayable=false` a menos que seja o
  personagem "original" desta conta) continuava aparecendo na grade travado/cinza, ao lado da
  instância jogável de verdade concedida no roster — mesmo portrait, mesmo nome, fácil de olhar
  pro card errado (o travado) e concluir que a compra veio desabilitada. Fix: novo campo
  `PlayerProfile.characterTypeId` (`[NonSerialized]`, preenchido por
  `PlayerProfileConverter.FromCharacterDTO`) identifica de qual molde cada instância do roster
  veio; `CharacterSelectController.PopulateCharacterGridRoutine` agora pula qualquer molde cujo
  `.name` já esteja representado por uma instância no roster da conta — só a instância jogável
  aparece, sem o card travado redundante ao lado.

- 2026-07-25: **2ª rodada do bug da grade quebrada de `02_SelectCharacter` — o fix anterior
  (`LayoutRebuilder.ForceRebuildLayoutImmediate` no fim da construção + em `HideOverlayAfterDelay`)
  não resolveu de fato** (reportado pelo usuário: continuava "uma caixinha pequena" ao clicar
  "Fechar" em vez de "Selecionar"). Causa raiz de verdade: forçar o rebuild não adianta se
  `gridPanel` ainda está DESATIVADO no momento em que ele roda — `Start()` desativava `gridPanel`
  (`SetActive(false)`) até a seleção pendente resolver, e a 1ª leva de cards de
  `PopulateCharacterGridRoutine` é construída de forma SÍNCRONA assim que `StartCoroutine` é
  chamado (comportamento padrão da Unity — o corpo da coroutine roda até o 1º `yield` antes de
  `StartCoroutine` retornar), o que acontece ANTES de `gridPanel.SetActive(true)` nesse fluxo —
  `LayoutRebuilder.ForceRebuildLayoutImmediate` também não faz nada numa hierarquia inativa.
  **Fix definitivo (substitui o da rodada anterior por completo)**: `gridPanel` nunca mais é
  desativado — fica sempre ativo, construindo normalmente o tempo todo, IGUAL ao fluxo de clique
  manual num card (que nunca teve esse bug). O "esconder a grade até resolver a seleção pendente"
  virou um painel opaco temporário próprio (`ShowPendingSelectionCover`/
  `HidePendingSelectionCover`, mesma cor de `selectionOverlayGo`), posicionado como sibling logo
  acima de `gridPanel` (cobre só a grade, sem tapar o botão "Voltar" fixo) — puramente visual,
  nunca toca a hierarquia/estado ativo da grade. Os dois `LayoutRebuilder.ForceRebuildLayoutImmediate`
  da rodada anterior foram removidos (sem função nenhuma agora que a causa raiz não existe mais).

- 2026-07-25: **Bug real corrigido — grade de `02_SelectCharacter` aparecia quebrada (1 card
  cortado, em vez da grade completa de 3 colunas) depois de fechar o overlay de detalhe aberto
  via case opening** (reportado pelo usuário testando o fluxo completo comprar→reveal→Continuar→
  overlay de detalhe→Fechar; some voltando ao normal só saindo e reentrando na cena). Comparado
  com o fluxo normal (clicar num card já dentro da grade, sem o bug) — a diferença é que só o
  caminho de `PendingCharacterSelection` (personagem recém-ganho via case opening) desativa
  `gridPanel` em `Start()` até resolver a seleção pendente. `PopulateCharacterGridRoutine`
  (coroutine que constrói os cards em lotes de 12 por frame) tem sua 1ª leva executada de forma
  SÍNCRONA assim que `StartCoroutine` é chamado — isso acontece ANTES de `gridPanel.
  SetActive(true)` (só reativado depois, em `ResolvePendingCharacterSelectionAsync`) — então
  `GridLayoutGroup`/`ContentSizeFitter` (em `gridContent`) tentam calcular layout de uma
  hierarquia ainda INATIVA, o que a Unity simplesmente não processa (Canvas não renderiza pra
  disparar o rebuild pendente), deixando o tamanho do Content e a posição dos cards presos num
  estado parcial que não se autocorrige de forma confiável mesmo depois de `gridPanel` ser
  reativado (limitação conhecida do Layout System da Unity com `ContentSizeFitter`/`LayoutGroup`
  reativados). Fix: `LayoutRebuilder.ForceRebuildLayoutImmediate(gridContent)` no fim de
  `PopulateCharacterGridRoutine` (causa raiz — ponto único que sempre roda com `gridPanel` já
  ativo e cobre 100% dos cards, independente do timing durante a construção) + a mesma chamada
  defensiva em `HideOverlayAfterDelay` (momento exato em que o jogador volta a ver a grade). Sem
  efeito no fluxo normal (clique manual no card) — a grade já estava correta o tempo todo nesse
  caminho.

- 2026-07-25: **Bug real de design corrigido — unlocks progressivos do case opening concediam
  T2/T3 de item que o personagem nunca tinha possuído em T1** (reportado pelo usuário: um Raro
  saiu com "Vitalidade T1, Faca T2, Rato T3" — Faca T2/Rato T3 sem nunca ter tido Faca T1/Rato
  T1/T2). **Substitui por completo** a lógica implementada na rodada anterior (tier elegível por
  POSIÇÃO do unlock na sequência — 1º só T1, 2º T1/T2, 3º+ T1/T2/T3 — independente de qual item
  saía), que estava desconectada de posse real e por isso conseguia entregar um tier alto de um
  item nunca visto antes. Regra corrigida em `CharacterUnlockEngine.DrawUnlock` (agora recebe o
  `PlayerProfile`, não mais um índice de unlock): cada unlock sorteia uma FAMÍLIA (skill/arma/pet,
  ponderado pelos mesmos odds de sempre) e o tier concedido é sempre `1 + maior tier que o
  personagem já possui desta família específica` — nunca um tier arbitrário. Família já no tier
  máximo (T3) não desperdiça o unlock: sorteia outra em vez de conceder. Como o profile já reflete
  tudo aplicado nos unlocks anteriores da MESMA sequência (`LevelUpEngine.ApplyOption` roda antes
  do próximo sorteio), T2/T3 só aparecem quando a mesma família calha de repetir dentro da mesma
  sequência de abertura — raro por natureza, como esperado. Reaproveita
  `SkillDatabase.FindByFamilyNameAndTier`/`WeaponDatabase.FindByFamilyNameAndTier`/
  `PetDatabase.FindByTypeAndTier` (mesma resolução já usada pela camada de save) em vez de andar
  cadeia de tier manualmente. `CharacterSelectController.ResolveCaseUnlocksAsync` simplificado
  junto — não precisa mais rastrear conjuntos de exclusão por família (a checagem de posse já
  cobre isso sozinha).

- 2026-07-25: **Bug real corrigido — compra de case falhava com `NotFound`/"Pacote não
  encontrado"** (reportado pelo usuário testando `case_rare` pela 1ª vez desde a implementação
  dos unlocks acima). Causa: não era bug de código — `purchaseCase` lê `casePackages/{packageId}`
  no Firestore (`functions/src/purchaseCase.ts`) e o script que popula essa coleção
  (`functions/src/scripts/seedCasePackages.ts`, `npm run seed`) nunca tinha sido executado contra
  o Firestore de produção desde que o sistema de case opening foi criado (2026-07-23) — os 4
  documentos (`case_rare`/`case_legendary`/`case_immortal`/`case_moeda_geral`) simplesmente não
  existiam ainda. Rodado agora (`GOOGLE_APPLICATION_CREDENTIALS` apontando pra uma service account
  key local do projeto `autoarms-c248f`, já que o script usa Admin SDK/ADC, autenticação diferente
  do login do `firebase` CLI) — "Seed concluído — 4 pacotes gravados em casePackages/." confirmado.

- 2026-07-25: **Unlocks progressivos de skill/arma/pet ao ganhar personagem via case opening
  implementado** (pedido do usuário — cada raridade concede N sorteios sequenciais: Normal 1,
  Uncommon 2, Rare 3, Legendary 4, Immortal 5). Investigação prévia (obrigatória antes de
  implementar, pedido explícito do usuário) confirmou que as tabelas de odds POR ITEM já existiam
  e já estavam conectadas (`SkillData.odds`/`WeaponData.dropOdds`/`PetData.odds`, aplicadas por
  `OddsApplier.cs`, mesmas usadas em `LevelUpEngine.DrawWeightedOption` no level-up de combate) —
  reaproveitadas sem alterar nenhum valor. Não existia, e foi confirmado com o usuário antes de
  codar: pesos de TIER por unlock (1º unlock só T1; 2º T1 ou T2; 3º em diante T1/T2/T3) — decisão
  final do usuário foi não inventar uma tabela de pesos nova, e sim incluir os tiers elegíveis no
  MESMO pool ponderado por odds já existente; como o odds de uma família é idêntico em todos os
  seus tiers (`OddsApplier`), isso já produz uma divisão uniforme entre tiers elegíveis sem
  nenhuma tabela extra. Novo `CharacterUnlockEngine.cs` (`Assets/Scripts/Utils/`) faz esse sorteio
  (exclui família já concedida na mesma sequência) e devolve um `LevelUpOption` reaproveitado
  direto por `LevelUpEngine.ApplyOption` (mesma mutação/bônus flat de Vitality-Herculean
  Strength-etc/HP malus de pet já testada no level-up de combate — nenhuma lógica de aplicação
  duplicada). Novo campo `PlayerProfile.caseUnlocksResolved`/`CharacterDTO.caseUnlocksResolved`
  (Firestore, mesmo padrão de `isFavorite`/`rarity`) marca a sequência como concluída, pra nunca
  reconceder ao reabrir o detalhe do mesmo personagem. Integração no fluxo (decisão do usuário,
  pergunta explícita): dentro do MESMO overlay de detalhe que `OnCharacterSelected` já abre pro
  personagem recém-concedido (não uma tela própria antes dele) — `Selecionar`/`Fechar` ficam
  escondidos e um card de reveal simples (`CharacterUnlockRevealPanel.cs`, novo, Canvas próprio
  sortingOrder 50 pra desenhar por cima do Canvas do `CharacterPanel`) mostra categoria+item+tier
  um por vez, com "Continuar" avançando pro próximo; cada unlock é persistido (`LocalSaveService.
  Save`) e refletido ao vivo no `CharacterPanel` (`Refresh()`) assim que aplicado, antes mesmo do
  próximo ser sorteado. Ver MONETIZACAO.md seção 13.

- 2026-07-25: **Causa raiz real encontrada e corrigida — "Continuar" ainda caía na grade mesmo
  depois do fix anterior**: a Cloud Function `purchaseCase` implantada em produção ainda era uma
  versão ANTIGA, de antes do campo `grantedCharacterId` ter sido adicionado à resposta (rodada
  anterior desta mesma tarefa) — editar `functions/src/purchaseCase.ts` localmente nunca implanta
  sozinho, precisa de `firebase deploy --only functions` explícito, que nunca tinha rodado depois
  dessa mudança. Sintoma sem nenhum erro em lugar nenhum: `dict["grantedCharacterId"] as string`
  em `CaseService.cs` usa `IDictionary` não-genérico, que devolve `null` (não lança exceção) pra
  chave ausente — a compra continuava "bem-sucedida" (reveal funcionava normal, usa
  `wonCharacterTypeId`), mas `PendingCharacterSelection` nunca era setado, então
  `CharacterSelectController` nunca via seleção pendente nenhuma pra resolver. Diagnosticado com
  um novo log em `ShopController.HandleCasePurchaseAsync` (avisa explicitamente se
  `GrantedCharacterId` vier vazio) e confirmado rodando `firebase deploy --only functions`
  (projeto `autoarms-c248f`) — deploy concluído com sucesso, função `purchaseCase
  (southamerica-east1)` atualizada.

- 2026-07-25: **Mais dois bugs reais corrigidos no case opening** (reportados pelo usuário depois
  do fix anterior — persistiam no teste).
  **1) "Continuar" ainda caía na grade completa em vez de abrir o detalhe do personagem
  recém-ganho**: adicionado um fallback direto — `RosterService.GetOwnedCharacterDocAsync`
  (novo) busca o documento específico do personagem concedido direto do SERVIDOR
  (`Source.Server`, ignora cache local) sempre que a listagem geral do roster
  (`ListOwnedCharacterDocsAsync`) não o retorna por qualquer motivo (timing, cache do SDK ainda
  sem conhecimento de um doc gravado por outro processo — a Cloud Function via Admin SDK).
  `CharacterSelectController.ResolvePendingCharacterSelectionAsync` (antes síncrono, agora
  `async Task`) tenta esse fallback antes de desistir e cair na grade normal.
  **2) `WinGlow` aparecia flutuando sobre o painel de reveal do personagem sorteado**: o destaque
  de raridade ao redor do ícone vencedor NA FAIXA da roleta foi projetado numa época em que o card
  de reveal era pequeno e a faixa continuava visível atrás dele — desde que o reveal virou
  full-screen (2026-07-24), o painel de reveal cobre a faixa inteira, e o `WinGlow` (criado DEPOIS
  do painel na hierarquia, portanto desenhado por cima de tudo) passou a flutuar sobre o painel em
  vez de destacar algo visível. Removido por completo — a borda de raridade do próprio ícone de
  reveal (`_revealIconBorder`, já existente) já cumpre esse papel no layout atual.

- 2026-07-25: **Dois bugs reais corrigidos na tela de case opening, pós-migração do roster**
  (reportados pelo usuário testando o fluxo completo).
  **1) Marcador central nunca alinhava exatamente com o vencedor**: causa raiz não era um
  desalinhamento de índice/posição do marcador (ambos já batiam certo na leitura do código) — era
  o Canvas/`CanvasScaler` do próprio `CaseOpeningPopup` terem sido criados NO MESMO FRAME em que
  `viewportRt.rect.width` era lido; a `RectTransform` de um Canvas `ScreenSpaceOverlay` recém-criado
  não reflete o tamanho real da tela até o sistema de Canvas rodar seu próprio update interno —
  ler antes disso podia devolver um valor obsoleto/placeholder, fazendo `finalX` parar a faixa
  numa posição calculada pra uma largura ERRADA. Fix: `Canvas.ForceUpdateCanvases()` logo após
  montar Canvas/`CanvasScaler`, forçando o rebuild antes de qualquer leitura de `rect.width`.
  **2) Botão "Continuar" caía na grade completa em vez de abrir direto no detalhe do personagem
  recém-ganho**: não existe (nem existiu) um `CharacterDetailController` separado — a tela de
  detalhe (splash art + painel de stats + Fechar/Selecionar) sempre foi construída inline em
  `CharacterSelectController` (`BuildSelectionOverlay`/`OnCharacterSelected`), a mesma
  `02_SelectCharacter`. `CharacterSelectController.Start()` agora esconde `gridPanel`
  imediatamente quando chega com uma `PendingCharacterSelection` pendente (setada por
  `CaseOpeningPopup` antes do `SceneManager.LoadScene`), só reexibindo depois que
  `ResolvePendingCharacterSelection` resolve (ou falha em resolver) a seleção — o usuário nunca
  vê nem um flash da grade completa antes do overlay de detalhe cobrir a tela. Adicionado
  `Debug.LogError` em `PlayerProfileConverter.FromCharacterDTO` (molde não encontrado no
  `CharacterDatabase`) e em `CharacterSelectController.ResolvePendingCharacterSelection`
  (personagem concedido não encontrado no roster carregado) — falhas reais que antes ficavam
  silenciosas, dificultando diagnosticar se o problema persistir.

- 2026-07-24: **Bug real corrigido — vencedor da roleta de case opening sempre caía no
  penúltimo slot visível, sobrando espaço vazio à direita da faixa** (reportado pelo usuário com
  screenshot). Causa: `CaseOpeningPopup.cs` tinha `ReelSlotCount`/`WinningSlotIndex` como
  CONSTANTES fixas (32/27) — só 4 slots sobravam depois do vencedor, quantidade que não
  considerava a largura real do viewport nem o tamanho do ícone, então não escalava pra telas/
  resoluções diferentes (e piorou nesta mesma rodada, que aumentou os ícones). Fix: `Init` agora
  mede `viewportRt.rect.width` em runtime e CALCULA `winningSlotIndex` (distância de giro fixa,
  `SpinTravelSlots=20`, mantém a mesma sensação de duração de antes) e `totalSlotCount` (padding
  depois do vencedor = quantos slots cabem visíveis na largura real + `SafetyMarginSlots`) — nunca
  mais expõe o fim do array, em qualquer resolução/aspect ratio. `BuildReel` foi separado em
  `BuildViewport`/`BuildContent` porque o Content só pode ser dimensionado depois de medir o
  viewport.
  Mesma rodada, ajustes visuais pedidos pelo usuário: ícones da faixa bem maiores (`SlotSize`
  160→380, `SlotSpacing` 18→32, borda 6→14 — só ~4-5 personagens visíveis por vez, era ~7-8) e o
  viewport passou a esticar 100% da largura da tela sem nenhuma margem (era 24px de cada lado).

- 2026-07-24: **Migração de `CharacterSelectController`/`SelectedProfileHolder`/
  `MainMenuCharacterPreview` pro roster real concluída** — revoga o adiamento documentado em
  `ARQUITETURA.md` no dia anterior. `PlayerProfileConverter.FromCharacterDTO` (extraído de
  `FromCharacterMap`, que virou wrapper fino) passou a marcar personagens do roster como
  `isUnlockedForSelection = true; isPlayable = true;` (era `false/false`) — o grid de
  `02_SelectCharacter` e a troca rápida do menu principal (`MainMenuCharacterPreview`) agora
  mesclam os assets pré-autorados de sempre com o roster buscado de `RosterService.
  ListOwnedCharacterDocsAsync` (assíncrono, mesmo padrão "mostra default, atualiza quando os
  dados reais chegam" de `ShopController`). `SelectedProfileHolder` ganhou o campo `characterId`
  (sincronizado por `SetProfile()`). Novo `PendingCharacterSelection` (canal estático cross-scene,
  mesmo padrão de `ReplayPlaybackState`) é o handoff do case opening pra seleção: o botão
  "Continuar" do `CaseOpeningPopup` agora navega direto pra `02_SelectCharacter` com o personagem
  recém-concedido já em destaque (mesmo efeito de clicar o card manualmente) — exigiu um campo
  novo (`grantedCharacterId`) na resposta de `purchaseCase`, já que a function só retornava o
  MOLDE (`wonCharacterTypeId`), não o ID da instância concedida.
  **Risco real achado e corrigido junto** (pesquisa antes de implementar): `PlayerProfileConverter.
  _pristineSnapshots`/`_ownerScope` são dicionários chaveados por referência de objeto, sem teto,
  pensados só pros ~72 assets pré-autorados — alimentá-los com o fluxo de instâncias runtime que
  esta migração passou a gerar viraria vazamento de memória sem limite durante a sessão. Fix: novo
  `PlayerProfile.isRuntimeInstance` guarda os dois métodos pra pular instâncias runtime por
  completo (nunca precisam da proteção cross-conta que esses dicionários existem pra dar). Ver
  `ARQUITETURA.md` "Modelo de roster multi-personagem" pro desenho completo.

- 2026-07-24: **Polish visual do case opening** (pedido do usuário depois de testar o fluxo pela
  primeira vez) — `CaseOpeningPopup`/`CaseReelSpinner`: borda colorida por raridade em todo ícone
  (faixa da roleta e card de reveal, via `UITheme.RarityColor`), faixa esticada de ponta a ponta
  da tela (era uma faixa central de 1040px — `finalX`/`startX` agora calculados a partir da
  largura REAL medida em runtime, já que o viewport estica por âncora em vez de largura fixa),
  fundo 100% opaco (era ~90%), marcador central trocado de vermelho opaco pra amarelo (`currencyGold`)
  a 45% de opacidade, painel de reveal expandido pra quase tela cheia com o ícone do personagem
  bem maior (560px, era 200px).

- 2026-07-23: **Bug real corrigido — compra de personagem (Case Geral/Raro/Legendary/Imortal)
  sempre falhava com popup genérico, sem log nenhum no Console e sem nenhuma invocação chegando
  no Cloud Logging de `purchaseCase`** (reportado pelo usuário testando "Personagem Legendary",
  R$199,00). Causa: `CaseService.cs` chamava `FirebaseFunctions.DefaultInstance.
  GetHttpsCallable("purchaseCase")` — `DefaultInstance` aponta pra `us-central1` por padrão, mas
  `purchaseCase` foi implantada em `southamerica-east1` (`functions/src/purchaseCase.ts`,
  `onCall({ region: "southamerica-east1" }, ...)`); a chamada ia pra uma região onde a function
  nunca existiu, falhando antes de sair do cliente (por isso nenhuma invocação real aparecia no
  Cloud Logging — só logs de deploy/inicialização do container). Fix: nova constante
  `CaseService.FunctionsRegion = "southamerica-east1"` (precisa ficar em sincronia manual com a
  `region` de `purchaseCase.ts`) + `FirebaseFunctions.GetInstance(FirebaseApp.DefaultInstance,
  FunctionsRegion)` em vez de `DefaultInstance`.
  **Segundo bug real, achado no mesmo diagnóstico**: o `catch (FunctionsException e)` nunca
  chamava `Debug.LogError` — qualquer erro desse tipo (incluindo o de região acima) ficava
  completamente silencioso no Console, atrapalhando o diagnóstico ("nenhum log de erro aparece").
  Corrigido logando `e.ErrorCode`/`e.Message` antes de montar o `CasePurchaseResult` de erro —
  não muda a lógica de decisão de mensagem do `ShopController` (que já lia `ErrorCode`/
  `ErrorMessage` do resultado, não do log), só deixa de engolir a exceção silenciosamente.

- 2026-07-23: **Sistema de compra de personagens/case opening implementado do zero** — infra de
  Cloud Functions criada pela primeira vez no projeto (`functions/`, Node/TS, `firebase.json`,
  `firestore.rules` versionado pela primeira vez no repo — até então as regras só existiam coladas
  manualmente no Console). Cloud Function `purchaseCase` (`functions/src/purchaseCase.ts`) valida
  pagamento (cash mock por recibo não-vazio — `// TODO` explícito pra validação real via App
  Store/Google Play antes de produção — ou saldo de diamante via Admin SDK), limite de compras por
  jogador (`users/{uid}/casePurchases/{packageId}`, não estoque global — decisão explícita do
  usuário, diferente do texto original da task), monta o pool elegível excluindo personagens já
  possuídos e sorteia o vencedor com `crypto.randomInt` dentro de uma única transaction (concede o
  personagem, incrementa o contador de compras, debita diamante se aplicável).
  Descoberta que evitou uma migração de schema: `users/{uid}/characters/{characterId}` já era uma
  subcoleção desde a Fatia 3 (2026-07-15) — bastou um campo novo (`characterTypeId`, referência ao
  molde `PlayerProfile`) pro `CharacterDTO`/`CharacterDTOMap`, mais `PlayerProfileConverter.
  FromCharacterMap` (mesma técnica de `FromOpponentIndexMap`) pra reconstruir um `PlayerProfile`
  runtime a partir de um documento do roster. Ver ARQUITETURA.md "Modelo de roster
  multi-personagem" pra lista completa de telas (`02_SelectCharacter`, `SelectedProfileHolder`,
  `MainMenuCharacterPreview`) que ainda precisam migrar antes de um personagem concedido virar
  jogável — essa migração ficou fora do escopo desta tarefa, deliberadamente.
  Client (`CaseOpeningPopup`/`CaseReelSpinner`, `Assets/Scripts/UI/`): roleta horizontal estilo
  CS:GO — faixa de 32 ícones translada em X com easing `easeOutQuint` (~4,5s, sem DOTween, que não
  está no projeto) até parar com o vencedor sob um marcador fixo (slot 27/32); reveal com glow por
  cor de raridade (`UITheme.RarityColor`, novo helper) e punch de escala via coroutine. Aba
  Personagens da Loja (`ShopController`) ganhou um 4º card "Case Geral" (diamante, pool de todas
  as raridades pelas odds da seção 7 de MONETIZACAO.md) — distinto do card "Próximo Personagem"
  (moeda/soft currency, seção 6), que continua intocado. Reverte a nota "NÃO FAZER AINDA" que
  MONETIZACAO.md registrava desde 2026-07-21 pra persistência de personagens comprados — decisão
  revogada pelo usuário nesta data.
  **Pré-requisito ainda pendente do lado do usuário**: pacote `Firebase.Functions` (Unity SDK)
  precisa ser importado antes de `CaseService.cs` compilar — só `Firebase.Firestore`/
  `Firebase.Auth`/`FirebaseApp` estavam presentes até agora. Plano Blaze do Firebase Console
  também precisa estar habilitado antes de `firebase deploy --only functions,firestore:rules`
  funcionar de verdade (Cloud Functions não rodam no plano Spark).

- 2026-07-22: **Bug real corrigido — pet voltava ao spawn e corria de novo a CADA hit de combo**
  (reportado pelo usuário testando o sistema de iniciativa ATB novo: "o macaco está voltando
  para o ponto inicial e correndo novamente até o oponente 3 vezes"). Causa: `CombatPlayer`
  chamava o ciclo completo de `PetCombatController` (corre → ataca → volta ao spawn em pêndulo)
  uma vez POR HIT (`CombatEventType.PetAttack`), inclusive pra cada hit extra de combo dentro do
  MESMO turno — diferente do personagem, que só reposiciona SE precisar entre hits de combo
  (`RepositionIfNeeded`) e só retorna ao spawn 1x, no `TurnEnd`. Fix: `PlayAttackSequence`
  dividido em `PlayAttackHit` (corre condicionalmente — sempre no 1º hit do turno, só se
  necessário nos hits de combo seguintes — ataca, sem retorno) + `PlayReturnToSpawn` (só o
  pêndulo de volta, chamado 1x no `PetTurnEnd`, não mais a cada hit). Novo campo
  `CombatEvent.PetAttack.isCombo` (`comboCount > 0` em `SimulatePetHit`) alimenta a decisão —
  puramente visual, não toca dano/combo/alvo. Ver PETS.md.
  Instrumentação temporária `[INIT-DEBUG]` (adicionada no mesmo dia pra investigar uma sequência
  de 2 turnos seguidos do mesmo pet, suspeita de bug de agendamento) removida depois que a causa
  real acabou sendo esta — mesmo padrão de remoção de log temporário já usado antes no projeto
  (ver Logging Policy no CLAUDE.md). `[Iniciativa]`/"Resumo de turnos" (ver entrada abaixo)
  continuam no log — não eram debug temporário, são melhoria permanente de legibilidade.

- 2026-07-21: **Log de combate ganhou cabeçalho de speeds/limiar + resumo de turnos por
  combatente** — pedido do usuário depois de notar, testando o novo sistema de iniciativa ATB,
  que um pet estava agindo bem mais que o próprio dono sem isso ser óbvio no log. Novo
  `CombatEventType.CombatStart` (emitido 1x no início de `Simulate()`, só pra debug — nenhum
  consumidor de gameplay lê) carrega speed/initiative efetivos de P1/P2 e speed de cada pet;
  `CombatLogFormatter` imprime `[Iniciativa] limiar=... | ...` no topo e um bloco "Resumo de
  turnos por combatente" antes do vencedor. Campos novos também persistidos em
  `ReplayEventDTO`/`ReplayEventDTOMap`/`CombatEventReplayConverter` pra sobreviver ao replay
  salvo (não só à luta ao vivo).

- 2026-07-21: **Sistema de turnos reescrito do zero como iniciativa ATB** (contador por
  combatente, soma `speed` a cada tick, age ao cruzar `CombatSettings.initiativeThreshold`,
  default 100) — substitui as DUAS implementações anteriores de speed (o modelo original de
  débito relativo P1×P2 + baseline fixa de pet, e a tentativa unificada revertida logo abaixo
  neste changelog). Player1, Player2 e todos os pets vivos entram na mesma fila, sem distinção de
  categoria; empates exatos no mesmo tick são resolvidos por sorteio, exceto o desempate
  Player1×Player2 na 1ª leva de cruzamentos da luta inteira, que usa `initiative` (pedido do
  usuário). Novo `CombatSettings` ScriptableObject (`Assets/Resources/CombatSettings.asset`).
  Validado numericamente (script standalone, fora do Unity) contra 2 exemplos fornecidos pelo
  usuário antes da implementação — resultado bate exatamente, inclusive um caso de empate triplo
  (pet+pet+personagem) que a descrição inicial do usuário não cobria explicitamente, confirmado
  com ele como comportamento correto antes de codar. Ver CLAUDE.md → Sistema de Iniciativa (ATB)
  e PETS.md → Speed System.

- 2026-07-21: **Revertida por completo a unificação de speed entre personagem e pet** (as 2 tarefas
  anteriores neste changelog: "Combatentes empatados em speed agora intercalam..." e "Speed System
  unificado — personagens e pets numa única corrida...") — pedido explícito do usuário depois de
  testar em jogo ("ta tudo errado e piorou"). `CombatSimulator.cs` volta ao modelo de 2
  personagens com `_speedTieFavorsP1` (bool, alterna empate P1/P2) + `SimulatePetActions` com
  baseline fixa de 10 isolada por pet, sem intercalamento — removidos `SpeedEntry`,
  `BuildSpeedRoster`, `ResolveRoundOrder`, `ExecuteCluster`, `EmitDeadPetSkipTurns`,
  `PlayerState.speedTieWait`, `PetState.speedTieWait`. **Não revertido**: a fórmula de dano do pet
  (`str * 0.45`, ver entrada "Dano do pet agora deriva de STR" abaixo) — mudança independente, sem
  relação com speed. Sessão de design de speed de pet será refeita do zero. Ver CLAUDE.md/PETS.md
  → Speed System pro estado restaurado e a nota de "tentativa revertida".

- 2026-07-21: **Combatentes empatados em speed agora intercalam ações dentro do mesmo round**
  (pedido do usuário) — `CombatSimulator.SimulateRound`/novo `ExecuteCluster` agrupam a fila em
  clusters (mesma `initiative` E mesma `speed` exatas) e revezam 1 ação de cada membro por vez, em
  vez de um fazer TODAS as ações antes do próximo começar. Reportado testando Macaco vs Macaco
  (ambos 25 de speed): o Macaco do jogador matava o Macaco adversário no meio do próprio bloco
  (mirando o pet inimigo via `RollPetTarget`) sem o outro nunca chegar a agir — agora ambos
  revezam, e o que morre no meio da troca simplesmente para de ser chamado sem travar o resto do
  cluster. Cluster de 1 membro (sem empate, inclusive qualquer luta sem pet) se reduz sozinho ao
  comportamento de bloco de sempre — sem regressão. Ver CLAUDE.md → Speed System.

- 2026-07-21: **Modo de teste do level-up religado (`CombatResultPanel.ShowAllOptionsForTesting =
  true`), pedido do usuário pra testar skills** — próximo level-up mostra uma grade rolável com
  TODAS as skills elegíveis (não sorteio ponderado), agora também sem o gate de ícone
  (`BuildAvailableSkills(..., requireIcon: false)` — skill sem ícone re-adicionado ainda aparece,
  com card placeholder + nome). Lembrar de desligar os dois de volta (`false`/`true`) quando
  terminar de testar, pra voltar ao fluxo real de sorteio.

- 2026-07-21: **Speed System unificado — personagens e pets numa única corrida de speed
  compartilhada** (pedido do usuário, substitui de vez a baseline fixa de 10 isolada dos pets e o
  `_speedTieFavorsP1` fixo P1/P2 da correção anterior). `CombatSimulator.SimulateRound` reescrito:
  todo combatente vivo (2 personagens + todo pet vivo dos 2 lados) acumula o próprio `speedDebt` e
  gasta contra a MENOR speed entre todos os OUTROS combatentes vivos do campo (comparação direta,
  não média nem vizinho mais próximo) — generaliza o algoritmo de 2 partidas de sempre pra N
  participantes. Personagem mantém o mínimo garantido de 1 ação/round; pet continua SEM esse
  mínimo (só acumula puro, podendo ficar vários rounds sem agir). Novo `PlayerState.speedTieWait`/
  `PetState.speedTieWait` (contador "rounds desde a última vitória em empate") generaliza o
  tie-break pra qualquer grupo empatado, não só P1 vs P2 — 2 empatados alternam A,B,A,B..., 3+
  viram um round-robin natural. Validado (script isolado, não só cálculo manual) contra: luta sem
  pet (idêntico ao algoritmo antigo), Javali(2)/Rato(9)/Personagem(10)/Macaco(30) e
  Personagem(20)/Pet(2) — números batendo com o esperado nos 3 casos, incluindo o efeito colateral
  intencional (pet lento em campo acelera as ações de AMBOS os personagens, não só do pet). Ver
  CLAUDE.md → Speed System e PETS.md pro detalhe completo.

- 2026-07-21: **Popup de detalhe do pet reescrito — formato "card de referência" My Brute**
  (`CharacterPanel.ShowPetDetail`) — antes mostrava só o tier atual num parágrafo corrido, sem
  Odds/HP malus/Initiative; agora mesmo layout do popup de arma (ícone+nome+linhas de stat, sem
  scroll), com STR/AGI/SPD/HP em tripla `[T1/T2/T3]` (tier atual destacado, reaproveitando
  `ResolveTierFamily`/`FormatTierTriplet`/`AddTieredBonusRow` já existentes pra arma) e Odds/HP
  malus/Initiative como valor único (fixos por tipo, não escalam por tier). Bônus especiais
  nomeados (Combo/Evasão/Precisão/Desarme/Combo do oponente/Block do oponente) só aparecem se o
  pet realmente usar aquele campo. Sem linha "Dano" (campo removido, ver acima). Sem prefab novo.

- 2026-07-21: **Bug real corrigido — empate de speed/initiative entre P1 e P2 sempre favorecia
  P1 em todo round da luta** (reportado como "pets com Speed igual/próxima não alternam ataques,
  um lado bate várias vezes seguidas antes do outro agir uma vez") — `CombatSimulator.
  SimulateRound` usava `_p1.speed >= _p2.speed` só nos casos DE VERDADE empatados (não em
  qualquer desempate); como esse empate não muda durante o combate, o mesmo lado sempre agia
  primeiro round após round, e como pets seguem a mesma ordem `firstAttacker`/`secondAttacker`
  do dono, o efeito cascateava pra eles também. Novo `_speedTieFavorsP1` (bool, determinístico —
  sem RNG, preserva replay) alterna a cada round genuinamente empatado; desempates por
  initiative/speed diferentes continuam idênticos a antes. Ver CLAUDE.md → Speed System.

- 2026-07-21: **Dano do pet agora deriva de STR, campo `damage` removido de `PetData`/`PetState`**
  (pedido do usuário) — `CombatSimulator.SimulatePetHit` calcula `Round(str * 0.45)` em vez de
  ler um valor fixo por tier; multiplicador `0.45` calibrado contra teste real em jogo (Rato
  3/4/5, Macaco 11/13/15, Javali 21/23/25 — resolve de quebra o Javali "fraco" no late game, já
  que `ApplyLevelScaling` escala STR dele automaticamente). `PetTierGenerator.cs` e os 9 assets
  em `Assets/ScriptableObjects/Pets/` também tiveram o campo `damage` removido. Ver PETS.md pro
  histórico completo (substitui a decisão em aberto registrada em 2026-07-17).

- 2026-07-21: **Docs sincronizados com o roadmap** — `ROADMAP_FUTURO.md` Fase 4: marcados `[x]`
  Sistema de diamantes, Sistema de energia com limite diário, Precificação dos pacotes e Reset de
  Level Up (com nota de divergência de cada um — desenho final ficou diferente do texto original);
  "Reset de Build" continua em aberto, com nota distinguindo do novo "Resetar Personagem" (mecanismo
  paralelo). `MONETIZACAO.md` ganhou 3 seções novas (10/11/12: energia por diamante, Novo Sorteio,
  Resetar Personagem) e Status atualizado. `CLAUDE.md`: descrição de `06_Loja`/`ShowLevelUpChoice`/
  `EnergySettings`/`CharacterPanel` atualizada pra refletir a persistência real e as mudanças de
  layout do level-up; contador de Progresso recalculado (144/52).

- 2026-07-21: **Continuar jogando com energia zerada ganhou preço progressivo por dia, por
  personagem (pedido do usuário)**: substitui o custo fixo único (`EnergySettings.
  diamondCostToRefill=20`) por 3 faixas em `EnergySettings` (`refillCostTier1=10`/
  `refillCostTier2=20`/`refillCostTier3Plus=40`, travado a partir da 3ª) — novos campos
  `energyRefillPaymentsToday`/`energyRefillLastPaymentTimestamp` em
  `users/{uid}/characters/{characterId}` (mesmo documento de energyCurrent, contador POR
  PERSONAGEM) resetam sozinhos quando a data (hora do SERVIDOR, mesmo `ReadServerNowAsync` já
  usado pela regeneração natural — nunca o relógio do device) muda, sem precisar de job/Cloud
  Function rodando à meia-noite. `EnergyService.GetRefillCostAsync`/`PayToRefillAsync` (novos,
  substituem `RefillOneAsync`); popup de confirmação em `MainMenuController` já mostra o valor
  certo e sugere a Loja quando o saldo é insuficiente.

- 2026-07-21: **Level-up — correção de layout (pedido do usuário, 3ª rodada)**: ícone das caixas
  de escolha reposicionado pra posY 45 (era 85), nome logo abaixo (posY -52, antes -30 acima
  colava no meio do ícone maior). Botão "Novo Sorteio" corrigido — a tentativa anterior (canto
  inferior esquerdo, posX 840) caiu no meio da tela; trocado pra canto INFERIOR DIREITO da tela,
  fora da janela de escolha (que fica centralizada).

- 2026-07-21: **Level-up — correção de layout (pedido do usuário, 2ª rodada)**: ícone das caixas
  de escolha corrigido pra 150×150 (tentativa anterior, +130=210, ficou grande demais). Botão
  "Novo Sorteio" movido pra FORA do painel da pirâmide — reparentado em `root` (tela inteira) em
  vez de `bg`/ChoicePanel, ancorado no canto inferior esquerdo da tela (posX≈840, abaixo/fora do
  quadrante das caixas). Posição exata não testada visualmente — avisar se não bater com "abaixo
  da informação do personagem".

- 2026-07-21: **Level-up — ajustes finos de layout (pedido do usuário)**: contador de diamante
  escalado 2x; botão "Novo Sorteio" virou um quadrado 200×200 (fonte 35), movido pro centro-
  inferior do painel (âncora trocada de canto-direito pra centro, já que um posX positivo só faz
  sentido geometricamente a partir do centro); descrição removida das caixas de escolha (só ícone
  + nome agora); ícone das caixas +130 de largura/altura (80→210) — combinação não testada
  visualmente, o ícone maior pode encostar no nome logo abaixo dependendo da caixa.

- 2026-07-21: **Level-up — contador de diamante visível + "Novo Sorteio" com preço progressivo e
  limite de 3 (pedido do usuário)**: painel de escolha ganhou um contador de diamante (canto
  superior direito, mesmo estilo do contador da Loja) pra o jogador ver o saldo antes de decidir
  se vale a pena resortear. Preço do "Novo Sorteio" deixou de ser fixo (30) e virou progressivo —
  1º uso 50 diamantes, 2º 100, 3º 200 — e o botão trava permanentemente ("Limite de sorteios
  atingido") depois do 3º uso, mesmo com saldo suficiente pra continuar. Gasto agora passa por
  `WalletService.SpendDiamondsAsync` (persistido de verdade) quando há conta logada, mesmo padrão
  de `MainMenuController.SpendAndContinueRoutine` — antes só decrementava o contador local em
  memória.

- 2026-07-21: **Bug real corrigido — desbloqueios/passe/progressão persistidos não tinham efeito
  fora da Loja**: reportado pelo usuário depois de reiniciar o app — diamante funcionou, mas
  Skip/1.5x ficaram desabilitados em combate mesmo "esgotados" (comprados) na Loja, o bônus de XP
  do passe não aplicou numa vitória mesmo mostrando "Ativo", e o level-up não ofereceu as caixas
  extras de Progressão. Causa: `ShopStateService.LoadAsync` só rodava dentro de
  `ShopController.Start()` — se o jogador fosse direto pra batalha sem visitar a Loja NESTA
  sessão, `PlayerUnlocksState`/`PlayerPassState`/`PlayerProgressionState` ficavam no default
  (tudo desligado) mesmo com o dado certo no Firestore; a própria Loja sempre parecia certa porque
  recarregava o estado ao abrir, mascarando o problema. Corrigido carregando esse estado nos
  mesmos 2 pontos onde moeda/diamante/energia já carregam (`LoginController.LoadEconomyRoutine`,
  `MainMenuController.RefreshEconomyOnMenuLoad`) — agora vale pra qualquer combate da sessão, não
  só depois de abrir a Loja.

- 2026-07-21: **Loja — persistência real de compras (Firestore), substitui o estado fake local**:
  diamante/desbloqueios (Skip/1.5x/Bundle)/Passes (Básico/Pro)/Progressão (Slots 1-3)/bônus de 1ª
  compra por pacote agora gravam em `users/{uid}` (novo `ShopStateService.cs` + `WalletService.
  AddDiamondsAsync`) em vez de resetar a cada sessão; `ShopController` carrega esse estado ao
  abrir a Loja e os cards já nascem refletindo o que a conta já possui. Reaproveita os campos
  `coins`/`diamonds` já existentes (não os nomes `diamondBalance`/`coinBalance`) e o modelo Básico/
  Pro independente já vigente (não um `tier` único) — ver comentário no topo de
  `ShopStateService.cs`. TODO SEGURANÇA registrado no código: gravação ainda client-side, sem
  Cloud Function (ARQUITETURA.md "Moeda premium").

- 2026-07-21: **Resetar Personagem — gera moeda (mecanismo paralelo ao "Reset de Build" do
  roadmap, que ainda não existe em código)**: novo botão no painel de detalhamento do personagem
  (`CharacterPanel`, fim da lista de Habilidades/Armas/Pets/Passivas), com popup de confirmação
  explícita antes de executar. Reseta o personagem pro Level 1 (mesma lógica de campos de
  `Tools > AutoArms > Reset All Profiles to Level 1`) e credita `nível anterior × coinsPerLevel`
  moedas (novo `CharacterResetSettings.asset`, `Assets/Resources/`, multiplicador ajustável sem
  código — 10 por padrão). Não implementa a liberação de personagem por moeda (fora do escopo).

- 2026-07-21: **Bug real corrigido + botão quadrado pro painel de detalhamento (pedido do
  usuário)**: "o primeiro painel sem expandir está cobrindo uma skill" — o bloco Compact do
  `CharacterPanel` (nome/HP/pips, sempre visível por padrão) tapava uma das caixas de escolha.
  `CharacterPanel.Setup` ganhou um parâmetro opcional `hideCompact` (default `false`, sem efeito
  em nenhum outro caller — 01_MainMenu/02_SelectCharacter/03_Arsenal continuam idênticos) que pula
  o estado Compact por completo, inclusive ao recolher (`CrossFade` ajustado pra não reativar o
  Compact quando `hideCompact=true` — sem isso, fechar o painel expandido reexibiria o bloco que
  cobria a skill). `CombatResultPanel` agora usa esse modo + um botão quadrado próprio (ícone "i",
  canto superior direito, fora do `Root` do CharacterPanel — sempre clicável independente do
  estado) que chama `Expand()`/`Collapse()` direto, abrindo o painel já expandido, sem passar pelo
  Compact.

- 2026-07-21: **Level-up — layout em pirâmide, cards 1.5x, botão de reset no canto inferior
  direito, painel de detalhamento maior (pedido do usuário)**: caixas passam de fileira única
  horizontal pra pirâmide (fileira de cima = metade das caixas arredondada pra baixo, fileira de
  baixo = o resto — 2 em cima/3 em baixo pra 5 caixas, como pedido; generaliza pras contagens
  2/3/4 também). Cards escalados 1.5x (`MakeLevelUpCard` ganhou parâmetro `scale`, aplicado direto
  na `RectTransform` do card — conteúdo interno escala junto de graça). Botão "Novo Sorteio" movido
  do canto superior pro INFERIOR direito (evita disputar espaço com o painel de detalhamento, que
  mora no canto superior direito). Painel de detalhamento do personagem escalado 1.5x — como
  `CharacterPanel` não tem parâmetro de escala próprio (componente compartilhado com
  01_MainMenu/02_SelectCharacter/03_Arsenal), a `RectTransform` "Root" é escalada de fora
  (`CombatResultPanel`, sem tocar em `CharacterPanel.cs`), com pivot movido pro canto superior
  direito antes de escalar pra crescer pra dentro da tela em vez de vazar pela borda. Geometria
  (posições/tamanhos de painel) é um chute inicial não testado visualmente — calibrar depois de
  ver em jogo, especialmente com 4-5 caixas (mais provável de sobrepor o painel de detalhamento).

- 2026-07-21: **Level-up — roleta de cassino, sem repetição na mesma sequência, botão "Novo
  Sorteio" (pedido do usuário)**: reportado "veio dois Feline Agility na mesma escolha" — as
  caixas 2+ agora excluem skill/arma/pet já sorteado nas caixas ANTERIORES do mesmo level-up
  (`DrawAndBuildCards`, filtra os pools elegíveis antes de cada sorteio; reverte a permissão de
  repetição da entrada anterior do changelog). Novo `LevelUpReelSpinner.cs` gira o ícone de cada
  caixa por um pool de sprites de todo o jogo antes de travar no resultado real já sorteado —
  caixas travam em sequência da esquerda pra direita (duração cumulativa maior por caixa);
  "Escolher" e o popup de detalhe do ícone ficam desabilitados até travar. Novo botão "Novo
  Sorteio (30 diamantes)" no canto do painel — gasta `PlayerEconomyState.Diamonds` (client-side,
  mesmo placeholder de Fase 1 de sempre) e refaz o sorteio + a roleta das N caixas do zero; preço
  fixo escolhido sem confirmação do usuário, ajustar a constante `RerollCost` se não for o valor
  desejado.

- 2026-07-21: **Level-up — volta direto ao menu ao escolher + fundo preto sólido (pedido do
  usuário)**: escolher um bônus na tela de level-up carrega `01_MainMenu` na hora (mesma coroutine
  do botão "Continuar"), sem exigir um clique extra. Overlay atrás das caixas de escolha trocado de
  55% translúcido pra preto sólido — como o painel de detalhamento usa um canvas raiz próprio
  (`sortingOrder=1500`, sempre acima de qualquer canvas do jogo), o mesmo overlay único cobre os
  dois (caixas de escolha e painel de detalhamento) sem precisar duplicar o backdrop.

- 2026-07-21: **Bug real corrigido (2ª rodada)** — painel de detalhamento do level-up e o popup de
  detalhe continuavam atrás das caixas de escolha mesmo depois de forçar `sortingOrder=1500` no
  Canvas do `CharacterPanel`. Causa real: o painel estava parentado dentro de `root` (já filho do
  Canvas da tela de escolha) — isso vira um CANVAS ANINHADO, e `sortingOrder` de um canvas aninhado
  só reordena entre irmãos do mesmo canvas pai, nunca vence canvases-RAIZ concorrentes
  (HealthBar/HealthBarPet = 100). Corrigido instanciando o painel SEM PAI nenhum (GameObject raiz
  de cena própria, mesmo padrão de `ArsenalController.EnsureDetailPanel`/`SelectOpponentController`
  — os únicos outros lugares que já usavam `CharacterPanel` com sucesso) — canvas raiz de verdade,
  `sortingOrder=1500` agora vence qualquer canvas do jogo. Passou a precisar de destruição manual
  (não é mais filho de `root`, `Object.Destroy(root)` não alcança mais ele).

- 2026-07-21: **Level-up — popup de detalhe ao clicar no ícone + painel de detalhamento do
  personagem (pedido do usuário)**: `CombatResultPanel.MakeLevelUpCard` agora abre o mesmo popup
  do `01_MainMenu` (`CharacterPanel.ShowSkillDetail`/`ShowWeaponDetail`/`ShowPetDetail`) ao clicar
  no ícone de uma caixa com skill/arma/pet. `ShowLevelUpChoice` também instancia um `CharacterPanel`
  visível (mesmo componente/posição do menu — compacto por padrão, nome/HP/STR/AGI/SPD; clique
  expande pra ver Habilidades/Armas/Pets equipados) mostrando o profile ANTES do bônus escolhido,
  pra comparar com o que já possui. Novo campo `AttackSequencer.theme` (wireado em
  `04_CombatScenePVP.unity`) leva o `UITheme` até o `CombatResultPanel`, que não tinha acesso a
  nenhum antes.

- 2026-07-21: **Level-up — Caixa 1 sempre status base, Caixas 2+ sorteio ponderado por odds reais
  (pedido do usuário)**: nova `PlayerProgressionState` (canal estático, mesmo padrão de
  `PlayerUnlocksState`/`PlayerPassState`) grava os Slots de Skill comprados na Loja e decide o
  número de caixas do level-up (2 base + 1 por slot, até 5). `LevelUpEngine.DrawBaseAttributeOption`
  (Caixa 1, só HP/STR/AGI/SPD) e `DrawWeightedOption` (Caixas 2+, pesa cada skill/arma/pet
  elegível pelo próprio `odds`/`dropOdds` aplicado na tarefa anterior; fatia residual cai pro
  mesmo pool de status base — nenhuma caixa fica vazia) somam-se ao `DrawOption` antigo (mantido
  intacto, ainda usado por `BotProfileGenerator`). `CombatResultPanel.ShowAllOptionsForTesting`
  desligado (era o modo de teste "mostra tudo numa grade") pra essa diferenciação aparecer de
  verdade em jogo; painel de escolha agora tem largura dinâmica pra caber de 2 a 5 cartões.

- 2026-07-21: **Loja — trava de progressão sequencial na aba Progressão**: Slot de Skill 2 e Slot
  de Skill 3 aparecem bloqueados ("Requer Slot de Skill N") até o slot anterior ser comprado —
  `ShopController.ProgressionLockState` checa `ShopItem.Purchased` do prerequisito (mesma variável
  local de sempre, sem estado novo) e `ShopCardUI.RefreshPurchaseState` desabilita o botão Comprar
  (cinza, sem clique) enquanto bloqueado; destrava imediatamente após a compra do slot anterior
  (`RebuildGrid`, sem reload de cena).

- 2026-07-21: **Odds de sorteio (My Brute) aplicados em Skills/Armas/Pets**: novo campo
  `SkillData.odds` (`WeaponData.dropOdds`/`PetData.odds` já existiam) preenchido pra 51/53 skills,
  26/26 armas e 3/3 pets via tabela original My Brute/eternaltwin (soma combinada 99.35%, não
  normalizada — valor confere com a fonte original). Ferramenta `Tools > AutoArms > Apply My Brute
  Odds` (`Assets/Editor/OddsApplier.cs`) aplica os valores nos `.asset`; "Bandage"/"Backup" ficaram
  de fora (sem `SkillData` correspondente no projeto ainda). Campos só preenchidos — nenhum sorteio
  de combate foi alterado (CombatResultPanel.ShowLevelUpChoice continua nos pesos 60/30/10 de
  sempre).

- 2026-07-21: **Bug real corrigido** — `MainMenuController.characterDatabase` nunca tinha sido
  wireado em `01_MainMenu.unity` (campo adicionado ao script depois do último save da cena, nunca
  arrastado no Inspector) — clicar num replay logava "CharacterDatabase não wireado" em vez de
  reproduzir. Corrigido adicionando a referência direto na cena (mesmo GUID já usado por
  `CharacterPreviewManager` pro mesmo asset).

- 2026-07-21: **Skip/1.5x sempre habilitados durante replay** (pedido do usuário) — os dois
  botões da tela de combate passavam pelo mesmo gate de `PlayerUnlocksState` mesmo ao reproduzir
  um replay já resolvido, travando quem não tinha comprado o desbloqueio na Loja mesmo só pra
  assistir a própria luta de novo. `CombatHUD.AddSpeedControls` agora lê `player.sequencer.
  isReplayPlayback` (já setado por `CombatSceneLoader` antes de montar o HUD) e força os dois
  botões habilitados nesse modo, independente do estado de compra.

- 2026-07-21: **Loja — Passes revisados pra INDEPENDENTES + bônus de XP real por vitória**
  (pedido do usuário, substitui o modelo de "tier único"/upgrade da entrada anterior do
  changelog): `PlayerPassState` trocou `ActiveTier`/`DaysRemaining` por `BasicoDaysRemaining`/
  `ProDaysRemaining` — Básico e Pro agora contam 30 dias cada um por conta própria, podendo os
  dois estarem ativos ao mesmo tempo (comprar um não afeta o contador do outro). Novo
  `PlayerPassState.WinXpBonus()` (0/1/2/3) somado ao XP base de vitória em
  `AttackSequencer.OnCombatEnd` (2 + bônus) fecha exatamente nos totais pedidos: sem passe = 2 XP,
  só Básico = 3 XP, só Pro = 4 XP, os dois = 5 XP — só na vitória, nunca na derrota, e só enquanto
  o(s) passe(s) estiver(em) dentro do período de 30 dias. Popup "i" do card atualizado pra refletir
  que o bônus de XP já funciona de verdade; a coleta diária de diamante/moeda continua pendente
  (fora do escopo pedido).

- 2026-07-21: **Loja — lógica de compra da aba Passes (ativação/upgrade/acúmulo de dias)**: novo
  `PlayerPassState.cs` (canal estático cross-scene, mesmo padrão de `PlayerEconomyState`/
  `PlayerUnlocksState`) guardando o tier ativo (nenhum/Básico/Pro) e os dias restantes.
  `ShopController.ApplyPassPurchase` implementa as 5 regras pedidas: sem passe ativo → ativa com
  30 dias; Pro comprado com Básico ativo → upgrade pra Pro + acumula (dias do Básico + 30); Básico
  comprado com Pro ativo → mantém Pro, só soma 30 dias; mesmo tier comprado de novo → só soma 30
  dias. Os dois cards agora ficam SEM LIMITE (removido `limit: 1` de antes) — continuam
  compráveis pra acumular, mostrando "Ativo — N dias restantes" no lugar do "Comprado Nx (sessão)"
  genérico quando aplicável (`ShopCardUI` ganhou um parâmetro `statusOverride` pra isso, sem afetar
  nenhum outro card). Novo botão "i" no canto superior direito dos cards de passe (`ShopCardUI`,
  parâmetro `onInfo`) abre um popup avisando que a recompensa diária (diamante/moeda/XP) ainda não
  foi implementada — só a ativação/contagem de dias funciona nesta Fase 1. Lógica/efeitos de
  Diamantes, Skip, 1.5x, Bundle e o layout de sidebar+scroll horizontal inalterados.

- 2026-07-21: **Desbloqueios de verdade: Skip/1.5x do combate agora exigem compra na Loja**
  (pedido do usuário, teste da aba Desbloqueios) — novo `PlayerUnlocksState.cs` (canal estático
  cross-scene, mesmo padrão de `PlayerEconomyState`) com `SkipUnlocked`/`Speed15xUnlocked`;
  `ShopController.OnBuyClicked` grava `true` neles ao comprar Skip/1.5x/Bundle na aba
  Desbloqueios. `CombatHUD.MakeSpeedToggleButton`/`MakeSpeedButton` (botões de velocidade/Skip da
  tela de combate, antes sempre disponíveis pra todo mundo) agora leem esse estado — sem a compra,
  o botão continua visível mas fica opaco (40% de alpha) e `interactable = false`; depois de
  comprado funciona normalmente. **TODO de segurança marcado no código** — só em memória nesta
  Fase 1 (sem Firestore), reseta a cada sessão/reinstalação.

- 2026-07-21: **Bug real corrigido (causa raiz de verdade)** — efeito de diamantes voando
  continuava não aparecendo mesmo sem nenhum erro no Console. Causa: `ShopController.Start()`
  capturava `_canvasTransform = canvasGo.transform` ANTES de `canvasGo.AddComponent<Canvas>()` —
  `Canvas` exige `RectTransform` (`[RequireComponent]`), então o Unity troca o `Transform` plano
  original por um `RectTransform` novo nesse momento (destruindo o componente antigo). A
  referência já capturada em `_canvasTransform` ficava "morta" (fake-null, sem lançar exceção) —
  `FlyingDiamondIcon.Rent` reparentava os ícones nessa referência inválida, então eles ficavam
  fora de qualquer Canvas e nunca renderizavam (tudo o mais na Loja usa `canvasGo.transform`
  fresco a cada chamada, por isso só este efeito específico quebrava). Corrigido movendo a
  captura de `_canvasTransform` pra depois de `AddComponent<Canvas>()`.

- 2026-07-21: **Bug real corrigido** — usuário reportou "não apareceu o efeito ainda" (diamantes
  voando). Causa: o contador de diamante do header (`ShopController.BuildDiamondCounter`) usava
  `HorizontalLayoutGroup.childControlWidth = false`, que faz o layout IGNORAR `LayoutElement.
  preferredWidth`/`flexibleWidth` dos filhos — ícone e número do contador renderizavam com
  largura ~0 (efetivamente invisíveis), então o "destino" visual do voo nunca aparecia de
  referência. Corrigido pra `childControlWidth = true`. Aproveitado pra deixar o efeito em si mais
  perceptível: ícone 48→64px, "pop" de entrada (escala 0→overshoot→1 em vez de aparecer estático
  no tamanho final) e timings reajustados pra caber com folga na janela de 0.4-0.8s mesmo com o
  pop somado; guard de `SpawnDiamondBurst` agora loga um aviso se alguma referência necessária
  vier nula, em vez de falhar em silêncio.

- 2026-07-21: **Loja — efeito "diamantes voando" + saldo local de verdade (aba Diamantes)**: novo
  contador de diamante no header da Loja (`ShopController.BuildDiamondCounter`, canto superior
  direito), refletindo `PlayerEconomyState.Diamonds` (mesmo campo estático usado no resto do app).
  Comprar qualquer pacote agora credita o saldo DE VERDADE em memória (`PlayerEconomyState.
  Diamonds += credited`, considerando o bônus de 1ª compra quando aplicável) e dispara 5-8 ícones
  de diamante (`FlyingDiamondIcon.cs`, componente novo) voando do botão "COMPRAR" clicado
  (`ShopCardUI.BuyButtonWorldPosition`, refinado 2026-07-21 — saía do centro do card inteiro antes)
  até o contador, em trajetória de arco (Bézier quadrática, altura proporcional à distância), com atraso/duração levemente
  variados por ícone (rajada) — o número do contador sobe aos poucos conforme cada ícone chega
  (não tudo de uma vez), fechando exatamente no valor certo quando o último chega. Duração total
  do efeito entre ~0.4s-0.8s. `FlyingDiamondIcon` usa pool estático com `SetActive`/`RemoveAll(p
  => p == null)`, mesmo padrão de `DamagePopup`/`CombatPlayer._ghostPool`. **TODO de segurança
  marcado no código** — saldo só em memória nesta Fase 1 (sem gravação no Firestore/WalletService
  ainda), então é perdido ao voltar pro menu principal (que resincroniza com o Firestore de
  verdade); compra com dinheiro real vai precisar de validação server-side do recibo antes de
  creditar, quando a Fase 4 ganhar o gateway de pagamento (ver ARQUITETURA.md "Moeda premium").

- 2026-07-21: **Bug real corrigido** — painel do personagem (gaveta mobile, `01_MainMenu`) expandido
  ultrapassava o teto da tela em telas com proporção mais larga que 16:9, cortando as skills/armas
  do topo (`BottomDrawerExpandedTopOverflow` era um valor fixo de 348.4799px em pixels de
  referência 1920×1080; como o Canvas trava a escala pela LARGURA, telas mais "esticadas" têm
  menos altura disponível em unidades locais do que 1080). `CharacterPanel.BuildExpanded` agora
  clampa esse overflow pelo espaço real sobrando até o teto do `SafeArea` (lido direto do
  RectTransform, com uma margem de 20px — `BottomDrawerTopSafeMargin`) — em telas grandes (16:9 ou
  mais estreitas) o comportamento não muda nada; só telas pequenas (proporção mais larga) passam a
  ter o Expanded "acompanhando o teto" em vez de ultrapassá-lo.

- 2026-07-20: **Loja — bônus de 1ª compra por pacote de diamante (aba Diamantes)**: cada um dos 8
  cards ganhou um selo "1ª compra: N diamantes (+25%)" (`ShopController.NewDiamondItem`, +25%
  arredondado pra cima — `Mathf.CeilToInt`, bate com a tabela do pedido: 30→38, 80→100, 170→213,
  360→450, 950→1188, 2000→2500, 4100→5125, 6100→7625), visível só até aquele pacote específico ser
  comprado uma vez (`ShopItem.FirstPurchaseBonusUsed`, por pacote — comprar o de 80 não afeta o de
  170); depois disso o selo some e o card volta a mostrar só a quantidade normal. A compra fake
  (log no Console) credita a quantidade certa (com ou sem bônus). Preço em R$ de cada pacote
  inalterado. **TODO de segurança marcado no código** (`ShopItem`) — o flag só existe em memória
  nesta Fase 1, reseta a cada sessão/reinstalação; precisa virar autoritativo no servidor antes do
  lançamento real (mesma regra de "nunca confiar no cliente" de ARQUITETURA.md "Moeda premium").

- 2026-07-20: **Loja — Passes mensais (aba Passes) desabilitam e mostram dias restantes após a
  compra**: `limit: 1` nos dois passes (pedido do usuário) + `ShopItem.PassDaysRemaining` (fixado
  em 30 na compra, `ShopController.OnBuyClicked`) — o botão passa de "COMPRAR" pra "30 dias" em
  vez do "ESGOTADO" genérico (`ShopCardUI.Build`/`RefreshPurchaseState` ganharam um parâmetro
  `soldOutLabel` opcional pra isso). Fase 1 (mock): só fixa o valor inicial, não decrementa
  sozinho — não existe relógio/tick nem Firestore ainda pra um countdown real.

- 2026-07-20: **Loja — comprar o Bundle desabilita Skip/1.5x avulsos**: direção oposta da regra já
  existente (Skip/1.5x avulso faz o Bundle sumir) — agora comprar o Bundle primeiro marca Skip de
  batalha e Velocidade 1.5x como esgotados (`Purchased = PurchaseLimit`, mesmo estado "ESGOTADO"/
  desabilitado de qualquer item com limite), sem removê-los da lista (`ShopController.
  OnBuyClicked`, novo bloco `item.Title == BundleTitle`).

- 2026-07-20: **Loja — Skip/1.5x/Bundle (aba Desbloqueios) limitados a 1 compra cada**: os 3 cards
  ganharam `limit: 1` (pedido do usuário) — mesmo comportamento de esgotar (desabilita, mostra
  "ESGOTADO") já usado em Progressão/Personagens. Regra de o Bundle sumir ao comprar Skip ou 1.5x
  primeiro (ver linha anterior no changelog) continua funcionando igual, agora com os 3 também
  esgotando individualmente depois da 1ª compra.

- 2026-07-20: **Bug real corrigido** — cards da Loja "iam indo pra direita" a cada compra
  (`ShopController.RebuildGrid`): `Destroy()` só remove o GameObject de fato no fim do frame, mas
  os cards novos eram adicionados como filhos do `Content` imediatamente — por um instante o
  `GridLayoutGroup`/`ContentSizeFitter` viam o DOBRO de cards (antigos pendentes + novos) e
  calculavam a largura em cima disso, empurrando os cards um pouco mais pra direita a cada clique
  de compra. Corrigido desparentando (`SetParent(null, false)`, efeito imediato) os cards antigos
  antes de destruí-los, em vez de só chamar `Destroy()` direto.

- 2026-07-20: **Loja — Slots de Skill (aba Progressão) limitados a 1 compra cada**: os 3 cards
  (`Slot de Skill 1/2/3`) ganharam `limit: 1` (pedido do usuário — eram sem limite, dava pra
  comprar o mesmo slot várias vezes) — mesmo comportamento de esgotar já usado na aba Personagens
  (card desabilita e mostra "ESGOTADO" depois da 1ª compra). Textos/preços inalterados.

- 2026-07-20: **Loja — card de liberação de personagem por MOEDA (aba Personagens)**: novo
  primeiro card da aba (`ShopController.BuildItemData`, título "Próximo Personagem"), cobrindo a
  seção 6 do MONETIZACAO.md que ainda não tinha UI — único card da Loja com preço em moeda (ícone
  Coin, não Diamond) e preço DINÂMICO: 100/200/400/600/800/1000 da 1ª à 6ª liberação, +400 a cada
  liberação a partir da 7ª (`CharacterSlotCost`). Clicar em "Comprar" incrementa o contador local
  (`ShopItem.Purchased`, mesmo mecanismo em memória da Fase 1) e reconstrói o card já mostrando o
  preço da PRÓXIMA liberação — dá pra clicar várias vezes seguidas e ver a progressão
  100→200→400→600→800→1000→1400→1800... Cards de Raro/Legendary/Imortal inalterados.

- 2026-07-20: **Loja — regra do Bundle na aba Desbloqueios**: Bundle (Skip + 1.5x) passou a ser o
  primeiro card da lista (antes vinha por último); comprar Skip de batalha OU Velocidade 1.5x
  avulso agora remove o Bundle da lista (`ShopController.OnBuyClicked`, checa `SkipTitle`/
  `BoostTitle`/`BundleTitle`) — não faz mais sentido oferecer o combo depois que um dos dois já
  foi liberado individualmente. Estado só em memória (Fase 1), reseta ao sair da cena.

- 2026-07-20: **Loja — layout revisto (sidebar + scroll horizontal)**: as 5 abas
  (`ShopController.BuildSidebar`) saíram da barra horizontal no topo e viraram uma barra lateral
  vertical à esquerda (botões empilhados via `VerticalLayoutGroup`, estilo Brawl Stars, pedido do
  usuário); os cards de cada aba (`ShopController.BuildScrollView`/`RebuildGrid`) passaram de grid
  vertical (wrap) pra rolagem HORIZONTAL — `GridLayoutGroup.Constraint.FixedRowCount`, 2 linhas na
  aba Diamantes (8 pacotes), 1 linha nas demais. Cards bem maiores (altura quase preenchendo a
  região disponível, corrigindo o fundo bege vazio sobrando abaixo deles) — `ShopCardUI.Build`
  agora recebe a altura real do card e escala ícone/paddings/fontes proporcionalmente
  (`scale = cardHeight/420`); a altura do subtítulo deixou de ser uma constante proporcional fixa
  e passou a ser calculada a partir do espaço realmente sobrante entre título e preço (bug real
  encontrado ao conferir a geometria — a caixa de subtítulo antiga podia se sobrepor ao cluster de
  preço/status/comprar, só não aparecia porque o texto nunca era longo o bastante pra preencher a
  caixa inteira). Lógica de compra fake, textos e preços de cada card **inalterados**.

- 2026-07-20: **Loja (Fase 1 — placeholder, ver MONETIZACAO.md)**: nova cena `06_Loja`
  (`ShopController.cs`) com 5 abas horizontais (Diamantes/Desbloqueios/Passes/Progressão/
  Personagens), grid de cards (`ShopCardUI.cs`, mesmo padrão de grid/RoundedRect de
  `ArsenalController`/`ArsenalSlotUI`) e navegação entre abas sem reload de cena. Preços/itens
  espelham `MONETIZACAO.md` (documento novo, criado nesta sessão — referência oficial da Fase 4).
  Aba Diamantes usa o ícone final (`Diamond.png`); as outras 4 abas usam retângulo cinza
  placeholder (sem arte ainda). Clicar em "Comprar" não grava no Firestore nem debita
  `WalletService` de verdade — só loga no Console e incrementa um contador local em memória (aba
  Personagens desabilita o card ao esgotar o limite de compras). Botão "LOJA" novo em
  `01_MainMenu` (`MainMenuController.BuildLojaMenuButton`), mesma coluna de atalhos abaixo de
  REPLAY, ícone placeholder (arte dos 4 botões da coluna a gerar depois, junto).

- 2026-07-20: **Bug real corrigido** — energia era descontada no clique do botão Jogar
  (`MainMenuController.OnPlayButton`), antes até da luta acontecer; desistir em
  `05_SelectOpponent` ou fechar o jogo no meio do combate já cobrava a energia à toa. Consumo
  movido pra `AttackSequencer.OnCombatEnd` (só roda quando a luta termina em vitória ou derrota
  de verdade); `OnPlayButton` só checa/gate a energia agora, e `SpendAndContinueRoutine` só
  reabastece (sem consumir na hora) — ver seção **Sistema de Energia/Moeda** em CLAUDE.md.

- 2026-07-20: Botão REPLAY do menu principal (`MainMenuController.BuildReplaysMenuButton`):
  - Texto "REPLAYS"→**"REPLAY"** (singular, pedido do usuário).
  - **Bug real corrigido** — o texto renderizava mais fino e com contorno menos visível que
    CHIBERS/ARSENAL mesmo com `fontStyle`/cor/`outlineWidth` idênticos no código. Causa real:
    `AddComponent<TextMeshProUGUI>()` cria o texto com a fonte PADRÃO do TMP (LiberationSans SDF),
    enquanto os labels de CHIBERS/ARSENAL (pré-colocados na cena) usam um `TMP_FontAsset`
    customizado ("LuckiestGuy-Regular SDF", mais grosso) — `outlineWidth` é normalizado por
    fonte, então o mesmo valor numérico produz um contorno bem mais fino em fontes diferentes.
  - **2ª rodada — regressão real corrigida**: a 1ª tentativa do fix acima (só `labelTxt.font = ...`,
    aplicado DEPOIS de já ter setado `.text` e o resto) quebrou de vez — texto virou "um monte de
    rabisco" (glifos errados/embaralhados), reportado pelo usuário. Causa: `fontSharedMaterial`
    continuou apontando pro material/atlas da fonte ANTIGA enquanto os glifos passaram a ser
    buscados na fonte NOVA — atlas e UVs incompatíveis. Fix de verdade: copia `font` **e**
    `fontSharedMaterial` do label de ARSENAL já existente na cena (`arsenalGo.
    GetComponentInChildren<TMP_Text>()`), **antes** de setar `.text`/qualquer outra propriedade
    (fonte/material precisam estar corretos antes do texto ser gerado, não depois), seguido de um
    `ForceMeshUpdate()` explícito.

- 2026-07-20: Toque fora do painel fecha o popup (`MainMenuController.BuildPopup`, pedido do
  usuário testando o popup de energia) — novo `Button` no `Overlay` (o fundo escurecido atrás do
  painel), mesmo efeito de Cancelar (só fecha, nunca invoca `onConfirm`). Vale pra TODOS os popups
  que passam por `BuildPopup` (energia, refill de diamante, mensagens genéricas), não só o de
  energia — cliques DENTRO do painel continuam não fazendo nada, já que o painel (sem Button
  próprio) bloqueia o raycast antes de chegar no Overlay por trás dele.

- 2026-07-20: Ajustes no popup "Tempo até a próxima energia:" (`MainMenuController.BuildPopup`,
  `PlayerEconomyState.FormatEnergyCountdown`), pedido do usuário:
  - **Prefixo removido**: `FormatEnergyCountdown` retornava "Próxima energia em H:MM:SS" —
    redundante com o título do popup, que já diz a mesma coisa. Agora retorna só o valor
    (ex: "0:28:43"). Único chamador restante é `ShowEnergyStatusPopup`.
  - **Fonte do valor bem maior**: 20→**64pt** (é o elemento central do popup agora). Painel do
    popup ficou mais alto só nesse modo (300→420px) e a faixa reservada pro valor cresceu de 10%
    pra 32% da altura do painel, com a faixa da mensagem/título reposicionada acima pra não
    colidir — nenhuma mudança no popup padrão (sem countdown, ex: "Crie uma conta pra jogar").

- 2026-07-20: **Bug real corrigido** — o tooltip de energia (mostra "Próxima energia em MM:SS" ao
  tocar a fileira, ver rodada anterior) abria ancorado ACIMA da própria fileira e, por estar perto
  do topo da tela, ficava cortado/inacessível (reportado pelo usuário). Fix: trocado por um popup
  modal CENTRALIZADO na tela — novo `MainMenuController.ShowEnergyStatusPopup` (público), que
  reaproveita o MESMO `BuildPopup` já usado pelos outros popups do projeto (overlay escurecido +
  painel centralizado + botão "OK" que fecha, mesmo padrão do popup de energia esgotada/REPLAYS)
  em vez de um tooltip próprio ancorado relativo à fileira. `MainMenuCharacterPreview.
  OnEnergyRowTapped` agora só decide SE deve abrir (guard de energia cheia inalterado) e chama o
  popup via `FindObjectOfType`. O `CanvasGroup`/tooltip inline antigo (`BuildEnergyTooltip`) foi
  removido; o `CountdownLabel` que dispara o re-sync automático em zero (bug de rodada anterior)
  ganhou um novo lar sempre-ativo e sem parte visual (`BuildEnergyResyncWatcher`), independente do
  popup estar aberto ou fechado.

- 2026-07-20: Reorganização do HUD superior (`MainMenuCharacterPreview`), pedido do usuário:
  - **Texto "Próxima energia em H:MM:SS" deixou de ser permanente** — virou um tooltip pequeno
    (novo `BuildEnergyTooltip`, substitui o antigo `BuildEnergyTimer`) que só aparece ao TOCAR a
    fileira de ícones de energia (novo botão invisível "TapArea" cobrindo a fileira inteira,
    mesmo espírito mobile-first "toque pra ver detalhe" que o Arsenal já usa pra skill/arma —
    `OnEnergyRowTapped` alterna um `CanvasGroup` em vez de abrir um popup cheio, já que é só uma
    linha de texto). Com energia no MÁXIMO (10/10), tocar não faz nada (nada regenerando, pedido
    explícito do usuário) — sem esse guard, mostraria "Energia cheia" à toa.
  - O `CountdownLabel` do tooltip continua num GameObject sempre ATIVO (só o `CanvasGroup` alterna
    visibilidade) — importante pro re-sync automático quando o countdown chega em zero
    (`isDoneCheck`/`onDone`, bug corrigido numa rodada anterior) continuar funcionando em segundo
    plano mesmo com o tooltip escondido.
  - **Fileira de energia alinhada na MESMA altura de moeda/diamante** (novo
    `EnergyHudTopAlignmentOffset`, calculado a partir dos valores exatos de
    `MainMenuController.BuildCurrencyHud`) — antes ficava centralizada sozinha mais acima, agora
    todos os três ficam na mesma linha horizontal no topo da tela.
  - Chip de fundo da fileira (`BuildEnergyChipBackground`) simplificado — não reserva mais espaço
    extra em cima pro texto do timer (que não é mais permanente), só um padding mínimo ao redor
    dos ícones.

- 2026-07-20: **Bug real corrigido** — coluna de atalhos CHIBERS/ARSENAL/REPLAYS não ficava
  "colada no chão" de forma estável (reportado pelo usuário — "no chão não está funcionando
  ainda", depois do ajuste anterior de alinhamento com o Jogar). Causa raiz: `Btn_SelectCharacter`/
  `Btn_Arsenal` (pré-colocados em `01_MainMenu.unity`) usavam âncora CENTRAL
  (`anchorMin=anchorMax=(0.5,0.5)`) com offset fixo em pixels, diferente da âncora de PONTO ÚNICO
  num canto real que o `BtnJogar` usa (`(1,0)`). O offset em X a partir do centro já era estável
  (CanvasScaler trava a escala pela LARGURA, então a largura do Canvas em unidades locais é
  sempre 1920 — por isso "colado na borda esquerda" já funcionava), mas o offset em Y a partir do
  CENTRO não: a ALTURA do Canvas varia com o aspect ratio (só a largura é travada), então "tantos
  pixels acima/abaixo do centro" aponta pra uma distância diferente do chão conforme a tela fica
  mais larga/estreita — o ajuste anterior (`AlignLeftColumnWithJogar`) só corrigia um snapshot
  pontual no `Start`, não o problema estrutural de fundo. Fix: novo
  `ReanchorLeftColumnToBottomLeft` (chamado ANTES de `BuildReplaysMenuButton`, já que Replays
  copia a âncora de Arsenal) reancora os dois pro canto inferior-ESQUERDO
  (`anchorMin=anchorMax=pivot=(0,0)`, espelho do BtnJogar) preservando a posição visual atual —
  lida via `GetWorldCorners` antes de trocar a âncora, convertida de volta pro espaço local do pai
  via `RectTransform.rect` (que já resolve o pivot do próprio pai automaticamente, sem precisar
  assumir nenhuma altura fixa de Canvas). `AlignLeftColumnWithJogar` continua rodando depois, pra
  fechar o alinhamento fino com a base do Jogar.

- 2026-07-20: **Bug real corrigido** — chip de energia (ícones + timer "Próxima energia") se
  desalinhava ao mudar o aspect ratio da tela (Free Aspect/mais largo no Editor), enquanto
  BtnJogar ficava fixo corretamente (reportado pelo usuário). Investigação confirmou: moeda e
  diamante (`MainMenuController.BuildCurrencyHud`) JÁ usavam o padrão correto — âncora de PONTO
  ÚNICO num canto real da tela (`anchorMin=anchorMax=(1,1)`, igual ao canto (1,0) do BtnJogar),
  sizeDelta e anchoredPosition fixos — não precisaram de nenhuma mudança. O problema real estava
  só em `MainMenuCharacterPreview.BuildEnergyHud`: a posição Y do chip vinha de
  `ComputeHudFractions` (o mesmo ponto usado pelo LevelXpHud pra seguir o personagem), que soma um
  offset em PIXELS dividido por 1080 (`.../1080f`) pra virar fração — presumindo que o Canvas
  sempre mede exatamente 1080 de altura. Com `CanvasScaler` em `ScaleWithScreenSize` +
  `matchWidthOrHeight` travado na LARGURA, a altura REAL do Canvas varia com o aspect ratio (fica
  menor que 1080 numa tela mais larga), então essa fração calculada parava de bater com a altura
  verdadeira e o chip "flutuava" pra fora do lugar. Fix: o chip de energia passou a usar uma
  âncora FIXA de ponto único no topo-centro da tela (`anchorMin=anchorMax=(0.5, 1)`, mesmo
  princípio do BtnJogar/moeda-diamante) em vez do ponto fracionário calculado a partir da posição
  do personagem — não segue mais o personagem (o LevelXpHud continua seguindo normalmente, não
  fazia parte do pedido), mas fica estável em qualquer resolução/aspect ratio. Novas constantes
  `EnergyTimerHeight`/`EnergyTimerGap`/`EnergyHudTopMargin` substituem os números soltos
  (4f/56f) que antes apareciam duplicados em `BuildEnergyTimer`/`BuildEnergyChipBackground`.

- 2026-07-20: Dois ajustes no HUD principal (`MainMenuController`):
  - **Fundo atrás do valor de moeda/diamante** (`BuildCurrencyEntry`): novo "ValueBg" — retângulo
    arredondado preto semi-opaco (alpha 0.55) atrás do número de cada label, melhora a
    legibilidade sobre o fundo variável da cena (mesmo espírito do chip da energia).
  - **Coluna CHIBERS/ARSENAL/REPLAYS alinhada com o Jogar** (`AlignLeftColumnWithJogar`, novo,
    chamado no `Start` logo depois de `BuildReplaysMenuButton`) — a coluna terminava mais acima
    que o `BtnJogar`/painel de detalhe do personagem. Comparação feita em espaço de MUNDO
    (`RectTransform.GetWorldCorners`), não derivada dos valores brutos de `anchoredPosition` no
    arquivo de cena: `BtnJogar` é ancorado no canto inferior-direito do seu pai enquanto
    Chibers/Arsenal/Replays são center-anchored, então comparar em espaço local exigiria saber a
    altura REAL renderizada do Canvas (varia com a resolução/aspect ratio de tela real, não
    necessariamente 1920×1080 mesmo com CanvasScaler assim configurado) — espaço de mundo já dá a
    distância certa, convertida de volta pra unidades locais via `lossyScale.y` (os 4 botões
    compartilham o mesmo Canvas/pai "Panel", escala idêntica).

- 2026-07-20: Gaveta mobile do `CharacterPanel` — Expanded agora cresce ACIMA do topo do Root
  (novo `BottomDrawerExpandedTopOverflow = 348.4799f`, `offsetMax.y` do `_expandedGo`, valor lido
  pelo usuário direto no Editor: campo "Top" da Inspector = -348.4799) — satisfaz o ponto 2 do
  pedido anterior ("no estado expandido, o painel deve poder crescer até sobrepor a fileira de
  energia"). Só o Expanded cresce; Root e Compact continuam do tamanho de sempre, e a base dos
  dois estados continua a mesma (`BottomDrawerFloorGap`).

- 2026-07-20: **Bug real corrigido** — base do painel de detalhe do personagem (gaveta mobile,
  `CharacterPanel._bottomAnchored`) descia ~28px ao expandir, em vez de ficar fixa alinhada com a
  base do botão Jogar (reportado pelo usuário). Causa: `Root` sempre foi uma janela de altura FIXA
  (`BottomDrawerMaxHeight`, ancorada na base, nunca anima — comentário de topo do arquivo já
  documentava isso), mas os DOIS estados internos que alternam por cima dele (`_compactGo`/
  `_expandedGo`, via CanvasGroup) usavam bases diferentes: `_compactGo` sempre flutuou 28px acima
  da base do Root (offsetMin.y=28), enquanto `_expandedGo` ficava colado exatamente na base do
  Root (offsetMin.y=0, stretch total) — ao trocar de um pro outro, o fundo arredondado visível
  "pulava" 28px pra baixo. Fix: nova constante `BottomDrawerFloorGap` (28px, extraída do valor que
  já existia hardcoded em `BuildCompact`) usada nos DOIS lugares — `_expandedGo` (só quando
  `_bottomAnchored`) agora usa `offsetMin.y = BottomDrawerFloorGap` (era 0) mantendo
  `offsetMax.y = 0` (topo continua subindo até o topo do Root). Como todo o conteúdo interno do
  Expanded (InfoBlock/Divider/ScrollArea) já era posicionado em frações RELATIVAS ao próprio
  `_expandedGo` (não ao Root diretamente), nada mais precisou mudar — o layout inteiro só
  "encolheu" 28px por baixo, mantendo a mesma base do Compact. `02_SelectCharacter` (painel-lateral,
  `_bottomAnchored=false`) não foi afetado — mantém `offsetMin=offsetMax=Vector2.zero` de sempre.

- 2026-07-20: **Bug real corrigido** — countdown de energia travava em "0:00:00" e a energia
  nunca incrementava sozinha (reportado pelo usuário). Causa: `PlayerEconomyState.EnergyCurrent`/
  `LastEnergyTimestampUtc` só avançam via `EnergyService.GetOrRegenAsync`, chamado só em pontos
  específicos (login, abertura do menu, clique em Jogar) — nada disparava um novo re-sync
  enquanto o jogador só ficava olhando o timer no menu, e `FormatEnergyCountdown` sempre clampa
  `remaining` em zero, então o texto congelava ali pra sempre mesmo com o tempo real já tendo
  passado do próximo tick. Fix:
  - `PlayerEconomyState.EnergyCountdownAtZero()` (novo) — verdadeiro quando o relógio local já
    passou do instante da próxima energia mas o cache ainda não foi atualizado.
  - `CountdownLabel.Init` ganhou `isDoneCheck`/`onDone` opcionais — dispara `onDone` só UMA vez
    por ciclo (borda de subida, com debounce via `_donePending`) quando `isDoneCheck()` fica
    verdadeiro, evitando martelar o Firestore a cada tick (1s) enquanto ficasse travado.
  - `MainMenuController.RefreshEconomyOnMenuLoad` virou público (era só chamado no `Start`) —
    `MainMenuCharacterPreview.BuildEnergyTimer` agora passa `onDone` chamando esse mesmo método
    via `FindObjectOfType`, forçando um re-sync completo (Firestore) assim que o countdown local
    zera, exatamente como se o menu tivesse acabado de abrir. `RefreshEconomyHuds` (já chamado
    dentro dele) atualiza a fileira de ícones automaticamente; o próprio texto do timer se
    recalcula sozinho no tick seguinte (`FormatEnergyCountdown` lê o estado atualizado).

- 2026-07-20: Ajuste fino no HUD de moeda/diamante (`MainMenuController.BuildCurrencyHud`) —
  posições exatas lidas pelo usuário direto no RectTransform em Play mode: `Row` sizeDelta
  (670, 146.346), anchoredPosition (-6.099976, -6.099976); ícone de moeda (0, 0), valor da moeda
  (142, 4.827); ícone de diamante (322, 4.827), valor do diamante (475, 4.827). A fórmula antiga
  (`x + iconSize + gap`) não reproduzia esses valores — não são uniformes entre os dois blocos —
  então `BuildCurrencyEntry` passou a receber `iconPos`/`valuePos` já prontos em vez de derivá-los
  de `x`/`gap`. Escala (156px/54pt) mantida.

- 2026-07-20: Ajustes finos no HUD superior (3ª rodada) — energia, fora da gaveta do
  `CharacterPanel`:
  - **Fileira de energia do mesmo tamanho do texto do timer** (pedido do usuário): `MainMenuCharacterPreview.EnergyIconSpacing` deixou de ser uma constante fixa — `BuildEnergyHud` agora mede a largura real do texto "Próxima energia em H:MM:SS" (`MeasureTimerTextWidth`, novo — TMP temporário invisível, medido via `GetPreferredValues`, destruído em seguida) e calcula o espaçamento negativo necessário pra fileira de 10 ícones somar essa mesma largura. Clamp em -60px por espaço (`EnergyIconSpacingMin`) evita que os ícones se sobreponham a ponto de virar uma mancha ilegível se o texto for muito estreito.
  - **Máscara do timer**: `PlayerEconomyState.FormatEnergyCountdown` — horas sem zero à esquerda,
    "00:00:00"→**"0:00:00"** (era `{totalHours:D2}`, agora `{totalHours}`).
  - Nenhuma mudança afeta a gaveta do `CharacterPanel`.

- 2026-07-20: Ajustes finos no HUD superior (2ª rodada), fora da gaveta do `CharacterPanel`:
  - **Fundo da energia mais justo** (`BuildEnergyChipBackground`): padding lateral/vertical
    reduzido de 24/14px pra **8px** nos três lados — o chip agora "abraça" a fileira de
    ícones+timer em vez de sobrar espaço morto nas laterais.
  - **Fundo da energia semi-transparente**: alpha de `theme.panelBackgroundAlt` reduzido pra
    **0.6** (era opaco, alpha 1) — se integra melhor com o brilho variável da cena atrás,
    mantendo contraste pro texto/ícones.
  - **Ícones de energia ainda mais agrupados**: `EnergyIconSpacing` 0→**-10px** — como já não
    havia espaço entre as bordas dos ícones (0px), "agrupar mais" só é possível com espaçamento
    negativo (leve sobreposição das margens transparentes dos sprites).
  - **Moeda/diamante revertidos pra lado a lado**: a rodada anterior tinha empilhado
    verticalmente (moeda em cima, diamante embaixo); usuário pediu de volta a disposição
    horizontal original (moeda esquerda, diamante direita) — escala 1.5x (156px/54pt) mantida.
  - Nenhuma mudança afeta a gaveta do `CharacterPanel`.

- 2026-07-20: Ajustes no HUD superior (moeda/diamante e energia), fora da gaveta do
  `CharacterPanel`:
  - **Moeda/diamante** (`MainMenuController.BuildCurrencyHud`): ícone/fonte escalados em x1.5
    (104px/36pt → 156px/54pt); os dois blocos, que ficavam lado a lado, agora ficam **empilhados
    verticalmente** (moeda em cima, diamante embaixo) — mesma âncora de canto superior direito
    (dentro do `SafeArea`), só a orientação do empilhamento muda. `BuildCurrencyEntry` trocou o
    parâmetro de offset horizontal (`x`) por vertical (`y`).
  - **Chip de fundo atrás da energia** (`MainMenuCharacterPreview.BuildEnergyChipBackground`,
    novo): fundo arredondado sólido (`theme.panelBackgroundAlt`, mesmo tom do painel de detalhe
    do personagem) atrás da fileira de ícones de energia + texto "Próxima energia em MM:SS" —
    antes ficavam soltos direto sobre o fundo da cena, com contraste baixo. Filho do `rootGo` da
    fileira, criado antes dos ícones/timer (fica atrás por ordem de sibling), com stretch+offsets
    calculados pra cobrir tanto a fileira quanto o timer acima dela.
  - Fonte do timer "Próxima energia em MM:SS": 32→**40pt** (altura da caixa 48→56px junto).
  - Espaçamento dos ícones de energia mantido agrupado (já reduzido numa rodada anterior).

- 2026-07-20: 5ª rodada de ajustes finos na gaveta mobile do `CharacterPanel`:
  - **Grid de Habilidades/Armas/Pets** (`MakeIconGrid`): célula 110→**220px** (dobrada de novo),
    espaçamento 12→**6px** (ícones mais agrupados). Em 220px, 5 colunas não cabem mais na largura
    do painel (`BottomDrawerWidth`), então `constraintCount` cai de 5→**3** só quando
    `_bottomAnchored` — `02_SelectCharacter`/`03_Arsenal` continuam em 70px/5 colunas.
  - **Bug real corrigido — fileiras de STR/AGI/SPD com tamanhos diferentes**: só a fileira de AGI
    tinha `xMin` empurrado pra 0.16 (pra abrir espaço pro quadrado do HP ao lado), enquanto
    STR/SPD ficavam no `xMin` padrão (0.05) — larguras diferentes entre as 3 fileiras faziam os
    ícones/pips internos (frações da largura da própria fileira) renderizarem em tamanhos
    visivelmente diferentes. Fix: as 3 fileiras agora usam o mesmo `xMin` (0.16), ficando
    geometricamente idênticas; o HP continua na mesma faixa vertical do AGI, na margem que sobra
    à esquerda.
  - **Popup de detalhe de skill/arma/pet aumentado de novo**: largura (skill/pet 620→**780px**,
    arma 650→**780px**), altura mínima (skill/pet 420→**520px**, arma 480→**560px**), ícone
    (`BuildPopupIcon`, antes fixo em 96px pros dois modos) agora **170px** só quando
    `_bottomAnchored` — proporcional ao resto do conteúdo maior. Área reservada pro bloco
    ícone+nome (`SkillPopupHeaderHeight`/`WeaponPopupHeaderHeight`) virou propriedade computada a
    partir do tamanho do ícone/fonte do nome, em vez de constante fixa. Fonte do título/nome:
    34→**40pt**. Fonte da descrição (skill/pet): 28→**35pt**.
  - Nenhuma mudança afeta `02_SelectCharacter`/`05_SelectOpponent`/`03_Arsenal`.

- 2026-07-20: 4ª rodada de ajustes finos na gaveta mobile do `CharacterPanel`:
  - **HP voltou a não usar pips** (pedido do usuário) — de volta ao estilo ícone+número
    sobreposto (`BuildIconWithValue`, mesmo do modo painel-lateral) em vez do ícone+badge+pips
    das rodadas anteriores. `InfoBlockRefs.hpPips` (campo temporário das rodadas anteriores)
    removido — `refs.hp` agora serve os dois modos.
  - **HP reposicionado**: quadrado pequeno à ESQUERDA do ícone de AGI, na mesma faixa vertical da
    fileira de AGI — a fileira de AGI teve o início em X empurrado (0.05→0.16) pra abrir espaço
    sem sobrepor. STR/AGI/SPD voltaram a ser só 3 fileiras (não mais 4 com HP), dividindo o
    container inteiro.
  - **Badge de STR/AGI/SPD não-quadrado**: `AttributePipBar.Build` trocou o parâmetro único
    `badgeSize` por `badgeWidth`/`badgeHeight` separados (default 36×36, preserva todo chamador
    existente) — gaveta mobile usa os valores exatos pedidos (width 61.425, height 64.675); a
    borda de prestígio e o raio do RoundedRect acompanham proporcionalmente (`Mathf.Min` dos
    dois lados).
  - Fonte das linhas de PASSIVAS (Evasion/Counter/Reverse/etc, dentro de "VER DETALHES"):
    17→**35pt** (só `_bottomAnchored`).
  - Nenhuma mudança afeta `02_SelectCharacter`/`05_SelectOpponent`/`03_Arsenal`.

- 2026-07-20: 3ª rodada de ajustes finos na gaveta mobile do `CharacterPanel`:
  - `iconScale` de STR/AGI/SPD ("os atributos") voltou pro 2.5 original (HP continua em 3.75, o
    meio-termo definido na rodada anterior — só STR/AGI/SPD foram pedidos de volta ao tamanho
    de sempre).
  - Fonte do número dentro do badge circular de STR/AGI/SPD: 16→**35pt** — `AttributePipBar.Build`
    ganhou parâmetros `badgeSize`/`badgeFontSize` (novos, default preserva 36px/16pt pra todo
    outro chamador — `05_SelectOpponent`/painel-lateral); o círculo do badge e a borda/texto de
    prestígio crescem proporcionalmente ao `badgeFontSize` em vez de ficarem hardcoded em 36/44px,
    pra o número sempre caber dentro do círculo.
  - `MakeSectionTitle` ("HABILIDADES"/"ARMAS"/"PETS"/"PASSIVAS"): fonte 20→**40pt** (só
    `_bottomAnchored`).
  - Label do botão "VER DETALHES"/"OCULTAR DETALHES": fonte 18→**40pt** (só `_bottomAnchored`).
  - Nenhuma mudança afeta `02_SelectCharacter`/`05_SelectOpponent`/`03_Arsenal`.

- 2026-07-20: Mais ajustes finos na gaveta mobile do `CharacterPanel` (`_bottomAnchored` —
  usuário ainda a chama de "MobileCharacterDrawer.cs", nome do componente já removido):
  - Ícones de HP/STR/AGI/SPD reduzidos (`iconScale` 5→**3.75**) — usuário reportou grandes
    demais na rodada anterior, mas pediu explicitamente pra NÃO voltar ao 2.5 original; 3.75 é o
    meio-termo.
  - **Bug real corrigido**: o painel Expandido (Habilidades/Armas/Pets) ficava mais ESTREITO que
    o painel Compacto fechado, porque só o "Compact" tinha sido alargado (via offsets manuais)
    na rodada anterior, enquanto o `Root` (que o Expanded sempre preenche 100%) continuou no
    `PanelWidth` de 450px antigo. Fix: `Root` passou a usar uma constante própria
    (`BottomDrawerWidth=865.7f`, a mesma largura real que o Compact já tinha) em vez de
    `PanelWidth`; o "Compact" foi simplificado pra só preencher 100% do Root em X (offsets 0/0)
    em vez de extrapolar as bordas manualmente — os dois ficam sempre com a MESMA largura agora,
    por construção, sem risco de divergir de novo.
  - Ícones de Habilidades/Armas/Pets na lista expandida aumentados (célula 70→110px, espaçamento
    8→12px) — só quando `_bottomAnchored` (`MakeIconGrid`, que deixou de ser `static` pra ler
    `_bottomAnchored`).
  - Popup de detalhe de skill/arma/pet (o que abre ao clicar num ícone) aumentado no geral:
    largura (skill 420→620px, arma 500→650px), altura mínima (skill 300→420px, arma 380→480px),
    fonte da descrição (skill/pet 20→28pt), fonte do bloco "Efeito" (18→24pt valor, label
    18→22pt), fonte do nome (28→34pt), fonte das linhas de stat da arma (20→26pt, com a altura de
    linha ajustada de 24→32px junto pra não cortar). Todos os consts viraram propriedades que
    variam por `_bottomAnchored` — `02_SelectCharacter`/`03_Arsenal` continuam exatamente nos
    valores originais.

- 2026-07-20: Ajustes finos no painel de detalhe do personagem (gaveta mobile do
  `CharacterPanel`, modo `bottomAnchored` — usuário ainda se referia a ele como
  "MobileCharacterDrawer.cs", nome do componente removido na rodada anterior; a funcionalidade
  agora mora inteiramente em `CharacterPanel.cs`):
  - RectTransform do "Compact" (faixa visível do estado fechado) ajustado pros valores exatos
    lidos pelo usuário no Inspector em Play Mode: Left -206.857 / Pos Y 28 / Right -208.846 /
    Height 230 — a faixa fica mais larga que o `Root` (450px) por design, sem problema (nada
    corta, não há Mask no Root).
  - Removidos do estado fechado: texto do nome do personagem e o badge/pill verde de Win Rate —
    `BuildInfoBlock` agora pula esse header por completo quando `_bottomAnchored`.
  - HP passou a usar o MESMO estilo ícone+badge+pips de STR/AGI/SPD (`AttributePipBar.Build`,
    novo case `"HP"` em `AttributePipBar.IconForLabel` reaproveitando `HpIcon`) — antes usava um
    coração+número sobreposto (`BuildIconWithValue`), estilo diferente dos outros 3. HP entra
    como uma 4ª fileira à esquerda/acima da coluna STR/AGI/SPD. `iconScale` dobrado (2.5→5) nas 4
    fileiras, e elas passaram a dividir o container INTEIRO (0-1, sem faixa reservada pro header
    removido) em vez de ~56% da altura — preenche bem mais o espaço do painel.
  - Nada disso afeta `02_SelectCharacter` (painel-lateral, `_bottomAnchored=false`) — todo ajuste
    ficou dentro de `if (_bottomAnchored)`, o caminho antigo (header + HP-coração + 3 pips em
    56% da altura) continua idêntico.

- 2026-07-20: Redesenho mobile do HUD principal, 2ª rodada (usuário reportou que a 1ª ficou ruim
  visualmente). Ver seção **Redesenho Mobile do HUD Principal** no CLAUDE.md pro detalhe completo.
  Resumo:
  - **Gaveta do personagem refeita do zero** — `MobileCharacterDrawer.cs` (componente novo da 1ª
    rodada, reimplementava nome/HP/STR/AGI/SPD do zero e não mostrava skills/armas/pets)
    **removido por completo**. `CharacterPanel` ganhou um modo `Setup(..., bottomAnchored: true)`
    que reaproveita 100% da lógica já existente e testada (`BuildInfoBlock` com `AttributePipBar`
    de verdade — ícone+badge+pips, nunca barras esticadas — `BuildSkillsAndWeapons` com Skills/
    Armas/Pets em grade + o botão "VER DETALHES"/PASSIVAS já existente cobrindo o pedido de
    "nível 2 de expansão" de graça), só espelhando a geometria de ancoragem: painel de largura
    fixa (450px, igual de sempre) **centralizado horizontalmente** (não mais tela inteira) e
    ancorado no RODAPÉ (era o topo, num painel lateral) — cabe no vão entre a coluna Chibers/
    Arsenal/Replays e o botão Jogar. Compact/InfoBlock ficam colados na base (cresce pra cima);
    Skills/Armas/Pets/Passivas ficam acima dele. `MainMenuController.Start()` não precisa mais de
    `HideRootPermanently()` nem de um segundo componente — só um `Setup` com o parâmetro novo.
  - Level/XP: barra reduzida de novo (era 320×130 grosso demais) pra 320×90 fina; "Level X" e
    "10/16" viraram uma linha só lado a lado (Level esquerda, fração direita) em vez de "Level X"
    sozinho + texto sobreposto na barra.
  - Energia: espaçamento entre ícones reduzido de novo (2px → 0px, colados) — usuário pediu mais
    agrupamento ainda.

- 2026-07-20: Redesenho mobile do HUD principal de `01_MainMenu` (pedido do usuário — elementos
  pequenos demais pra leitura confortável em celular). Ver seção própria **Redesenho Mobile do
  HUD Principal** no CLAUDE.md pro detalhe completo. Resumo:
  - Checagem prévia (pedida pelo usuário antes de travar pixels): `CanvasScaler` de TODAS as
    Canvas do projeto usa `referenceResolution=1920×1080`/`matchWidthOrHeight=0`, e
    `ProjectSettings` está em Auto Rotation — nenhuma Canvas trata Safe Area. Decisão do usuário:
    manter esse sistema por enquanto (não migrar pra retrato), só ajustar os 4 elementos deste
    HUD dentro dele; dívida técnica registrada em ROADMAP_FUTURO.md (Fase 7) pra decisão futura.
  - `SafeArea.cs` novo (`Assets/Scripts/UI/`) — primeiro tratamento de safe area do projeto,
    aplicado nos elementos ancorados em canto/borda deste HUD (moeda/diamante, gaveta do
    personagem).
  - Moeda/diamante: saiu do `CharacterPanel` (canto superior esquerdo do painel) e virou HUD
    próprio no canto superior DIREITO da tela (`MainMenuController.BuildCurrencyHud`), ícone
    52→104px, fonte 18→36pt.
  - Energia: ícones 44→88px, espaçamento 6→2px (agrupar mais, sem alargar a fileira de 10), timer
    16→32pt.
  - Level/XP: caixa 280×76→320×130, fonte Level 18→32pt, fonte XP 14→24pt.
  - Painel de detalhes do personagem → gaveta expansível no rodapé (`MobileCharacterDrawer.cs`,
    novo componente): fechada mostra só nome+HP (não cobre o personagem), toque expande revelando
    STR/AGI/SPD numa área acima (`iconScale` bem maior, 2.5→4.2), mesma técnica de crossfade por
    `CanvasGroup` que o `CharacterPanel` já usava. `CharacterPanel` continua vivo em
    `01_MainMenu` só pela infra de popup (replays/detalhe de skill-arma), agora escondido via
    `HideRootPermanently()` — mesmo truque do `ArsenalController` em `03_Arsenal`.

- 2026-07-20: Bug real corrigido — o timer de "próxima energia" no menu ficava sempre em branco.
  `PlayerEconomyState` (coins/diamonds/energyCurrent/LastEnergyTimestampUtc) só era populado por
  `LoginController.LoadEconomyRoutine` (roda só em `00_Login`) ou ao clicar Jogar
  (`OnPlayButton`) — abrir/testar `01_MainMenu` direto (sem passar pela cena de login, fluxo comum
  no Editor) deixava tudo no valor default de fábrica, então `FormatEnergyCountdown` sempre
  retornava `null` e o texto nunca era escrito. Fix: `MainMenuController.Start()` agora dispara
  `RefreshEconomyOnMenuLoad()` (busca wallet+energia de uma vez, `Task.WhenAll`) toda vez que o
  menu carrega, não só nesses dois pontos. Reposicionado também a pedido do usuário: o timer
  agora fica ACIMA da fileira de ícones de energia (era abaixo).

- 2026-07-20: Timer de "próxima energia" (pedido do usuário) — texto ao vivo (`HH:MM:SS`) abaixo
  da fileira de 10 ícones no menu, e o mesmo texto dentro do popup que abre ao clicar Jogar com
  energia zerada (tanto o de confirmar gasto de diamante quanto o de saldo insuficiente). Novo
  componente genérico `CountdownLabel` (chama um `Func<string>` a cada 1s) reaproveitado nos dois
  lugares via `PlayerEconomyState.FormatEnergyCountdown`. `PlayerEconomyState` ganhou
  `LastEnergyTimestampUtc`/`RegenIntervalHours` (espelhados por `EnergyService.GetOrRegenAsync`) —
  o countdown entre uma sincronização e outra usa o relógio local só pra decoração; o valor real
  gasto/creditado continua sempre revalidado contra o servidor, então isso não reabre a brecha de
  trapaça do relógio do device que o resto do sistema já fecha.

- 2026-07-20: Bug real corrigido (regra do Firestore, não código) — `[FirestoreService] Falha ao
  salvar replay... Missing or insufficient permissions` a cada luta. A regra de
  `replays/{replayId}` usava um único `allow write` (create+update+**delete**) exigindo
  `request.resource.data.result in [...]`/`events is list`/etc. — mas numa operação de DELETE,
  `request.resource` é `null` (não existe "dado novo" pra validar), então TODO delete era
  rejeitado. `FirestoreService.TrimOldReplaysAsync` (apaga replays além do teto de 10) sempre
  falhava nesse delete — o replay NOVO salvava normal (create passa na validação), só a limpeza
  dos antigos nunca funcionava, e o erro (dentro do mesmo try/catch do save) aparentava "falha ao
  salvar" por inteiro. Fix: regra separada em `allow create, update` (validação de dados) +
  `allow delete` (só checagem de dono) — aplicado também em `opponents_index` por precaução (nada
  apaga esse doc hoje, mesma armadilha existiria se algo passar a apagar no futuro). Regra
  completa atualizada em ARQUITETURA.md — precisa ser colada de novo no Firebase Console.

- 2026-07-20: Bug real corrigido — energia sempre voltava pro mesmo número (ex: sempre 9) depois
  de jogar, não importa quantas vezes (reportado pelo usuário). Causa: `FirestoreService.
  SaveCharacterAsync` gravava o documento do personagem com `SetAsync(map)` **sem merge** — uma
  sobrescrita TOTAL do documento com só os campos do `CharacterDTO` (level/str/weapons/skills/
  etc.), que nunca incluíram `energyCurrent`/`lastEnergyTimestamp` (de propósito, ver
  `EnergyService.cs`). Esse save dispara a cada luta (XP ganho) — então toda vez que uma batalha
  terminava, os campos de energia gravados minutos antes eram apagados do documento, e a próxima
  leitura (`EnergyService.GetOrRegenAsync`) achava "documento sem os campos ainda" e reinicializava
  pra cheio, consumindo 1 de novo — sempre no mesmo número. Diagnosticado com logs temporários
  `[EnergyDebug]` (adicionados e depois removidos nesta sessão) confirmando `hasCurrent=False
  hasTimestamp=False` em toda chamada, mesmo o documento existindo. Fix: `SaveCharacterAsync` agora
  usa `SetAsync(map, SetOptions.MergeAll)` — os campos do DTO continuam sendo sobrescritos
  normalmente (todos presentes no payload a cada save), só os campos de FORA do DTO (energia)
  deixam de ser apagados.
  **Achados secundários no mesmo log** (pré-existentes, fora do escopo desta correção): (1)
  `[FirestoreService] Falha ao salvar replay... Missing or insufficient permissions` — regra de
  segurança do Firestore rejeitando o save de replay, precisa investigar separadamente; (2)
  `[OpponentSearchService] Falha ao buscar adversários: The query requires an index` — falta um
  índice composto no Firestore pra query de `opponents_index`; o próprio log já traz um link do
  Console pra criar o índice com 1 clique.

- 2026-07-20: Ícones de moeda/diamante (`CharacterPanel.BuildWalletBar`) e de energia
  (`MainMenuCharacterPreview.BuildEnergyHud`) dobrados de tamanho (x2/y2), pedido do usuário —
  26→52px e 22→44px respectivamente. `WalletBarHeight` (34→60px) cresceu junto pra caber o ícone
  maior sem vazar da faixa; a fileira de energia não precisou de nenhum ajuste extra (largura/
  altura/posição acima do Level-XP já são derivadas de `EnergyIconSize`).

- 2026-07-19: Sistema de energia/moeda (HUD de moeda geral, diamante e energia, pedido do
  usuário) — ver seção própria **Sistema de Energia/Moeda** no CLAUDE.md pro detalhe completo.
  Resumo:
  - `EnergySettings.asset` (`Assets/Resources/`) — balanceamento (10 energia, +1/2h,
    `diamondCostToRefill` placeholder 20) editável no Inspector sem código novo.
  - Moeda/diamante POR CONTA (`users/{uid}.coins/diamonds`, `WalletService.cs`, incremento
    atômico via `FieldValue.Increment`); energia POR PERSONAGEM
    (`users/{uid}/characters/{characterId}.energyCurrent/lastEnergyTimestamp`,
    `EnergyService.cs`). Regeneração nunca lê o relógio do device — usa um probe com
    `FieldValue.ServerTimestamp` + leitura forçada em `Source.Server` pra obter a hora real do
    Firestore sem precisar de Cloud Function só pra isso; timestamp reancora em "agora" ao bater
    o teto (evita reencher instantaneamente depois de ficar muito tempo parado no máximo) e
    avança pelo tempo exato consumido enquanto abaixo do teto (preserva progresso parcial).
  - `MainMenuController.OnPlayButton()` agora exige `AuthService.IsSignedIn` (decisão do usuário:
    conta vira obrigatória pra jogar) e, com energia em 0, abre popup de confirmação pra gastar
    diamante e reabastecer 1 energia — sem diamante suficiente, mensagem clara sem batalha.
  - **TODO de segurança**: gasto de diamante (`WalletService.SpendDiamondsAsync`) é placeholder
    client-writable — o projeto ainda não tem Cloud Functions implantadas. Decisão explícita do
    usuário registrada em ARQUITETURA.md ("Moeda premium"), pra trocar por uma Function de
    verdade na Fase 4/Monetização sem mudar a assinatura pro chamador.
  - HUD: `CharacterPanel.BuildWalletBar` (ícone+número, canto superior esquerdo do Root, sempre
    visível independente de Compact/Expanded) e
    `MainMenuCharacterPreview.BuildEnergyHud` (10 ícones acima do Level/XP, esvazia/reenche da
    direita pra esquerda). Ícones novos em `Assets/Resources/UI/Economy/{Coin,Diamond,Energy}.png`
    (fornecidos pelo usuário), mesmo padrão `Resources.Load<Sprite>` já usado por
    `AttributePipBar`.
  - Limpeza tentada e **revertida**: apagar os 8 `SpriteRenderer` órfãos de `01_MainMenu.unity`
    (`imgDiamond`/`imgBlackDiamond`/`imgPlusDiamond`/`imgEnergy`/`imgBlackEnergy`/`imgPlusEnergy`/
    `imgQuest`/`imgPass`, GUIDs de sprite quebrados) por edição direta do `.unity` corrompeu a cena
    (Unity acusou "Broken text PPtr"/"Transform child can't be loaded" ao reabrir) — um objeto
    real ("Panel", com Animator + filhos como "Text (TMP)") estava intercalado no meio do
    intervalo de linhas apagado, sem checagem individual antes da exclusão em massa. Cena
    restaurada de um backup feito antes da edição; usuário decidiu deixar os 8 órfãos como estão
    (inofensivos) em vez de arriscar nova edição de texto — ver CLAUDE.md.
  - Bugs de compilação corrigidos: `EnergyService.GetOrRegenAsync` usava `out var` dentro de um
    `&&` curto-circuitado (CS0165, unassigned local `storedCurrent`/`storedTimestamp` — o
    compilador não correlaciona a bool resultante com o out-var em statements separados); e o
    campo `MainMenuController.selectWeapons` (morto desde o cancelamento de `03_SelectWeapons`
    em 2026-07-16, CS0414) foi removido, já que esta sessão mexeu em `MainMenuController` por
    outro motivo mesmo (CLAUDE.md já pedia essa limpeza "se for mexer por outro motivo").

- 2026-07-19: 3 ajustes no popup de REPLAYS (`CharacterPanel`), pedidos do usuário depois de
  testar:
  1. Ícone de play do cartão trocado de ">" (ASCII) pra um triângulo real rasterizado em runtime
     (`UIShapeUtil.PlayTriangle`, mesmo espírito de `Star` já existente — não um glifo Unicode
     "▶", que não é seguro nesta fonte TMP, mesmo risco já documentado com "★"/"☆"/"—" neste
     projeto). Cor do botão trocada de `primaryAction` (vermelho) pra `secondaryButtonAlt`
     (cinza-azulado) — não competir com o X de fechar do mesmo popup nem com o botão JOGAR do
     menu, ambos vermelhos.
  2. Palavra "Vitória"/"Derrota" na 2ª linha do cartão agora colorida (verde/vermelho, via
     `<color>` rich text + `ColorUtility.ToHtmlStringRGB`), não só a faixa lateral — acessibilidade
     pra daltonismo, pedido explícito do usuário.
  3. Subtítulo novo abaixo do título "REPLAYS" mostrando o nome do personagem dono daquele
     histórico (mesma resolução de profile — `_overrideProfile` ou `_holder.currentProfile` — que
     `LoadAndShowReplaysAsync` já usa pra montar a query), já que o popup é reaberto pra qualquer
     personagem selecionado no momento. Área da lista abaixo ajustada (56→76px reservados) pra
     abrir espaço pro subtítulo.

- 2026-07-18: Cartões do popup de REPLAYS aumentados de novo (pedido do usuário — "deixe
  visualizar 3 e meio de card no primeiro momento"). `ReplayCardHeight` 84→**164px**, calculado a
  partir da altura real da área rolável (`ReplayPopupHeight×0.86 − 56 ≈ 597.6px` com o popup
  820×760 da rodada anterior) resolvendo `3.5×H + 3×spacing(8) = 597.6` — 3 cartões inteiros +
  metade do 4º ficam visíveis sem rolar, o resto (até 10) acessível rolando. `ReplayAvatarSize`
  52→96px, `ReplayPlayButtonSize` 58→72px; nome 22→30pt, linha de resultado 17→22pt, letra de
  fallback 28→40pt, seta do play 28→34pt. Só constantes — mesma lógica/estrutura de antes.

- 2026-07-18: Popup de REPLAYS aumentado (pedido do usuário — "pegar mais da tela do main menu
  pode preencher bem") — painel de 480×520 pra 820×760 (canvas 1920×1080), título 26→32pt.
  Cartões (`ReplayCardHeight`/`ReplayAvatarSize`/`ReplayPlayButtonSize`) de 64/38/44 pra 84/52/58,
  nome 18→22pt, linha de resultado/data/duração 14→17pt, letra de fallback e seta do play 22→28pt,
  rodapé 14→16pt — espaçamento de 8px entre cartões mantido intacto (`VerticalLayoutGroup.
  spacing`, requisito explícito do pedido anterior). Só constantes/tamanhos — nenhuma mudança de
  lógica.

- 2026-07-18: Popup de REPLAYS redesenhado — cartões estilo "battle log" (Clash Royale) no lugar
  de 1 linha de texto cru (`CharacterPanel.BuildReplayRow`): borda esquerda colorida por resultado
  (verde/vermelho, 3px, altura inteira), avatar circular do OPONENTE (`ResolveOpponentIcon` — casa
  `p2Snapshot.profileName` no `CharacterDatabase` e reaproveita o mesmo `PlayerProfile.previewIcon`
  já usado em 02_SelectCharacter, recortado em círculo via `Mask` + `UIShapeUtil.RoundedRect(raio =
  metade do lado)`, mesmo truque do badge de `AttributePipBar`; ajuste "cover" manual — calcula o
  tamanho a partir da proporção real do sprite pra sempre cobrir os 38×38 inteiros, já que
  `Image.preserveAspect` sozinho só faz "contain"), nome do oponente + linha de resultado/data/
  duração, e um botão de play circular SEPARADO do cartão (sibling, não filho — só ele dispara
  `PlayReplay`, cartão em si não é mais clicável). Fundo do cartão levemente mais claro que o
  painel do popup (`Color.Lerp` com `Color.white`, mesmo truque de tint em runtime de
  `CharacterCardButtonStyle`), cantos 8px, 8px de espaço entre cartões (já cobertos pelo
  `VerticalLayoutGroup.spacing` existente). Rodapé novo "X de N replays salvos" (N =
  `FirestoreService.MaxReplaysPerCharacter`).
  **Duração em rounds reais, não estimada** (pedido explícito do usuário) — `CombatSimulator`
  ganhou `RoundCount` (nº real de rounds simulados, lido no fim de `Simulate()`), propagado por
  `CombatSceneLoader` → `AttackSequencer.lastCombatRoundCount` → `ReplayRecorder.Save` →
  `ReplayDTO.roundCount` (persistido, mesmo padrão do `seed`). Replays salvos ANTES desta mudança
  não têm o campo (lido como 0) — o cartão cai pra `eventCount` nesse caso, rotulado como "eventos"
  (não "rounds"), pra nunca fabricar um número como se fosse round de verdade.
  **Fallback sem ícone** (personagem removido/renomeado, ou sem sprite resolvido): letra "V"/"D"
  no círculo, não um glyph de coroa/caveira — Unicode fora do ASCII básico já teve um bug real
  documentado neste mesmo popup (`FormatTierTriplet`, travessão "—" virando glyph quebrado);
  separador da 2ª linha também é "|" (ASCII), não "·"/"—", pelo mesmo motivo.
  Não mexe em `ListReplaysAsync`/no fluxo de `PlayReplay` — só troca o item renderizado dentro da
  lista já existente.

- 2026-07-18: Botão "REPLAYS" movido de dentro do `CharacterPanel` (Expanded, abaixo de "VER
  DETALHES") pra virar um botão próprio de `01_MainMenu` (pedido do usuário) — mesma coluna de
  atalhos de "Chibers" (`Btn_SelectCharacter`)/"Arsenal" (`Btn_Arsenal`), logo abaixo deste último.
  `MainMenuController.BuildReplaysMenuButton` posiciona o novo `Btn_Replays` a partir do
  `Btn_Arsenal` já existente na cena (`GameObject.Find`, mesmo padrão de
  `CombatSceneLoader.RandomizeArenaBackground`) — mesmo tamanho/âncora, deslocado pra baixo pelo
  mesmo espaçamento vertical já usado entre Chibers e Arsenal (lido dos dois `RectTransform` em
  runtime, com fallback fixo de 211.87px caso `Btn_SelectCharacter` não seja achado). Visual
  replica `CharacterCardButtonStyle` manualmente (ícone placeholder + faixa de label + sombra via
  `UIButtonShadowStyle`) em vez de reaproveitar aquele componente — seus campos `theme`/
  `iconOverride` são `[SerializeField] private`, só wireáveis pelo Inspector em GameObjects já
  existentes na cena, não dava pra configurar num GameObject novo criado por código.
  `CharacterPanel` ganhou `public void ShowReplays()` (chama o mesmo fluxo de popup que antes só o
  botão interno disparava) e perdeu o botão "REPLAYS"/`BuildReplaysButton` de dentro da seção
  Habilidades/Armas/Pets — o popup de lista em si (`ShowReplayListLoading`/
  `LoadAndShowReplaysAsync`/`PlayReplay`) não mudou, só quem o aciona.

- 2026-07-18: Bug real corrigido — `FirestoreService.EnsurePersistence()` derrubava TODA
  gravação/leitura da sessão (personagem, matchHistory, opponents_index, replay) com
  `InvalidOperationException: The settings cannot be modified after calling non-static methods...`
  sempre que `OpponentSearchService.FetchOpponentsAsync` (usa `FirebaseFirestore.DefaultInstance`
  direto, sem passar por `FirestoreService`) rodava antes de qualquer save da sessão — ex: 1ª luta
  depois de pular o login, ou Play Mode iniciado direto numa cena que não passa por
  `00_Login`/`LoginController.SyncCharacterRoutine`. `EnsurePersistence()` agora marca
  `_persistenceConfigured = true` ANTES de tentar (não depois) e envolve
  `Db.Settings.PersistenceEnabled = true` num try/catch silencioso — falhar só significa que o
  cache offline nativo não liga nesta sessão, sem derrubar a operação real de leitura/escrita.
  Novo `FirestoreService.TryEnsurePersistence()` (wrapper público) chamado por
  `OpponentSearchService` antes do próprio acesso direto ao Firestore, na ordem certa.

- 2026-07-18: Sistema de replay (reprodução) — botão "REPLAYS" no `CharacterPanel` (01_MainMenu,
  logo abaixo de "VER DETALHES"), completando a gravação implementada mais cedo no mesmo dia (ver
  entrada abaixo). Clicar abre um popup (reaproveita a MESMA infra de `_popupOverlayGo`/
  `_popupContentRoot`/`_popupPanelRt` já usada por `ShowSkillDetail`/`ShowWeaponDetail`, só com uma
  lista rolável em vez de ícone+texto) que busca `FirestoreService.ListReplaysAsync` e mostra os
  últimos replays daquele personagem (resultado + adversário + data); clicar num item reconstrói os
  dois `PlayerProfile` (runtime, via novo `ReplaySnapshotConverter.ToRuntimeProfile` — mesmo
  espírito de `PlayerProfileConverter.FromOpponentIndexMap`, casando por `profileName` no
  `CharacterDatabase` e aplicando os stats/loadout CONGELADOS do snapshot em vez do progresso atual
  do personagem) e carrega `04_CombatScenePVP`.
  Novo canal cross-scene `ReplayPlaybackState` (`Assets/Scripts/Data/`) — deliberadamente um campo
  estático puro, não um `ScriptableObject` em Resources (padrão de sempre do projeto): os dois
  profiles reconstruídos usam os holders normais por baixo (`SelectedProfileHolder`/
  `SelectedOpponentHolder` continuam intocados, então o personagem real do jogador nunca é
  sobrescrito), só faltava um jeito de dizer "não rode `CombatSimulator`, use estes eventos já
  prontos". `CombatSceneLoader.Initialize()` checa `ReplayPlaybackState.IsActive` ANTES do branch de
  `useSimulator` — se ativo, pula a simulação inteira, usa os eventos gravados direto e consome
  (`Clear()`) o estado, pra uma luta normal seguinte não herdar nada por engano.
  `AttackSequencer` ganhou `isReplayPlayback` (setado por `CombatSceneLoader` nesse modo) — checado
  logo no início de `OnCombatEnd`, pulando XP/`LocalSaveService.Save`/histórico de batalhas/gravação
  de outro replay por completo (o `player1Profile` nesse momento é o profile RUNTIME reconstruído
  do snapshot, não o personagem de verdade — tratá-lo como se fosse salvaria stats congelados por
  cima do progresso real) e mostrando `ReplayEndPanel` (novo, `Assets/Scripts/UI/` — versão mínima
  de `CombatResultPanel`, só "VITÓRIA/DERROTA de {nome} contra {nome}" + botão Voltar, sem
  XP/level-up) em vez do painel normal.
  `MainMenuController` ganhou o campo `characterDatabase` (`[SerializeField]`, precisa ser wireado
  manualmente no Inspector com `Assets/ScriptableObjects/Databases/CharacterDatabase.asset` —
  usado só por `ReplaySnapshotConverter` pra resolver o adversário de um replay; sem ele o botão
  REPLAYS continua listando normalmente, só falha ao tentar reproduzir um item, com log de erro em
  vez de travar).

- 2026-07-18: Sistema de replay (gravação) — log de eventos completo de cada luta salvo em
  `users/{uid}/characters/{characterId}/replays/{replayId}` (Opção B da análise, aprovada pelo
  usuário: log completo em vez de seed+snapshot só, porque WeaponData/SkillData são rebalanceados
  com frequência real no projeto — um replay reconstruído por seed divergiria do resultado
  original assim que qualquer skill/arma usada naquela luta mudasse de valor). Novo `ReplayEventDTO`
  (`Assets/Scripts/Combat/`) — formato salvo DESACOPLADO de `CombatEvent` (a classe de runtime),
  com conversão via `CombatEventReplayConverter`; `type` gravado como string (nome do enum), não o
  índice numérico, pra sobreviver a uma futura inserção no meio de `CombatEventType`.
  `ReplayEventDTOMap.ToMap` só grava por evento os campos que fogem do valor-padrão (um `TurnStart`
  vira só `{type, playerIndex}`) — evita o bloat de serializar sempre os ~30 campos de
  `CombatEvent`/DTO como o `JsonUtility` do projeto faria. `ReplayDTO`/`ReplayPlayerSnapshotDTO`
  (`Assets/Scripts/Data/`) guardam `createdAtTicks`, `opponentCharacterId`/`opponentName`,
  `result` ("win"/"loss"), `seed` (capturado de verdade — `CombatSceneLoader` agora gera o seed
  explicitamente antes de `CombatSimulator.Simulate()` em vez de deixar cair no default aleatório;
  não é estritamente necessário pro replay, mas serve de auditoria/debug), `eventCount` e o
  snapshot de stats/armas/skills/pets de cada jogador NO MOMENTO da luta (reaproveita
  `PlayerProfileConverter.ToDTO`, já que `PlayerProfile`/`WeaponData`/`SkillData` são todos assets
  vivos que mudam depois — replay não pode depender do estado atual deles). `ReplayRecorder`
  (`Assets/Scripts/Backend/`) monta o DTO e chama `FirestoreService.SaveReplayAsync`, disparado em
  `AttackSequencer.OnCombatEnd` (mesmo ponto/padrão fire-and-forget de `SaveMatchHistoryAsync`).
  Rotação client-side (sem Cloud Function — projeto ainda não tem nenhuma implantada, ver
  ARQUITETURA.md): a própria escrita, depois de gravar o replay novo, consulta os últimos por
  `createdAtTicks` desc e apaga o que sobrar além de 10 por personagem (`FirestoreService.
  TrimOldReplaysAsync`, `MaxReplaysPerCharacter=10`). Regras do Firestore novas (subcoleção
  `replays` dentro de `characters/{characterId}`) e uma nota nova em ARQUITETURA.md documentando
  que replay fabricado client-side (nenhuma validação server-side do CONTEÚDO ainda) é dívida
  técnica conhecida/aceita por enquanto (dano cosmético, não money), a revisitar na Fase 8 junto do
  anti-cheat de moeda. **Fora de escopo desta rodada**: a UI de "assistir" (botão, lista de
  replays, tela que realimenta `CombatPlayer` a partir do `List<CombatEvent>` reconstruído por
  `CombatEventReplayConverter.FromDTOList`) — só a gravação/persistência foi implementada; a
  leitura/reprodução fica pra uma próxima tarefa.

- 2026-07-18: Ajustes de layout no card de `05_SelectOpponent` (`SelectOpponentController.cs`):
  "Level X" movido do RightPanel pra cima do retrato do personagem (`LevelAbovePortrait`, nova
  faixa dourada centralizada, mesma largura do Portrait); Portrait escalado 1.5x (`PortraitScale`,
  cresce a partir do próprio pivot superior-esquerdo); ícones de STR/AGI/SPD reduzidos de 3x pra
  2x e badge/pips puxados pra mais perto deles (`AttributePipBar.Build` ganhou os parâmetros
  opcionais `iconScale`/`badgeAnchorX`/`pipsAnchorMinX`, default inalterado — `CharacterPanel`
  continua igual); ícone de HP reduzido de 2.7x pra 1.8x (`AttributePipBar.BuildIconWithValue`
  ganhou `iconScale` opcional, mesmo motivo).

- 2026-07-18: Posicionamento fino no card de `05_SelectOpponent`, calibrado visualmente pelo
  usuário no Editor — Portrait movido pra `anchoredPosition (-38, 22)` (substitui o cálculo
  baseado em `CardPadding`/`LevelAbovePortraitHeight`); ícone de HP movido pra
  `anchoredPosition (-68, -114)` e escalado de 1.8x pra 3x (`iconScale` do
  `AttributePipBar.BuildIconWithValue`).

- 2026-07-18: Mais um passe de calibração visual em `05_SelectOpponent`: ícone de HP reescalado
  pra 2x (era 3x) e reposicionado pra `anchoredPosition (-100, -106)`; ícones de STR/AGI/SPD
  reescalados pra 2.5x (era 2x); linha de ícones de skill/arma/pet (`ItemIcons`) reposicionada pra
  `anchoredPosition (-42, -204)`, fixa em vez de seguir a pilha vertical de "y".

- 2026-07-18: **Correção** — a mudança de escala do HP acima estava aplicada só no ícone
  (`AttributePipBar.BuildIconWithValue`'s `iconScale`), deixando a caixa/número no tamanho
  original; trocado pra `hpRt.localScale = (2.5, 2.5, 1)` no próprio container `Hp` (mesma técnica
  do `Portrait`), escalando ícone+número juntos, como pedido.

- 2026-07-18: HP reposicionado de novo em `05_SelectOpponent` — `anchoredPosition (-113, -106)`.

- 2026-07-18: Mesmo ajuste de ícones de STR/AGI/SPD do `05_SelectOpponent` levado pro
  `CharacterPanel` compartilhado (`BuildPipRow`, afeta `01_MainMenu` Compact/Expanded **e** o
  painel deslizante de `02_SelectCharacter`, já que os dois reusam o mesmo componente) — iconScale
  2.5x (era 3x) + badge/pips puxados pra mais perto do ícone (`badgeAnchorX: 0.19`,
  `pipsAnchorMinX: 0.34`). HP não precisou de nenhum ajuste próprio — seu alinhamento em X com o
  ícone de STR (`BuildInfoBlock`) só replica a caixa do ícone, que não mudou de posição/tamanho.

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