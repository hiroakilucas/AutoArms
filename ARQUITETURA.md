# AutoArms — Regras Arquiteturais Permanentes

Este arquivo guarda decisões de arquitetura que são **regra fixa do projeto**, não anotação de
sessão — o que está aqui vale pra qualquer implementação futura, mesmo que o sistema descrito
ainda não exista. Ler antes de desenhar qualquer sistema que envolva dinheiro real, contas de
usuário ou persistência online.

## Moeda premium (diamantes) — regra de segurança inegociável

Contexto: o sistema de diamantes (Fase 4 — Monetização, ver `ROADMAP_FUTURO.md`) ainda não foi
implementado. Esta regra foi registrada **antes** da implementação, a pedido explícito do
usuário, justamente pra nascer certa desde o desenho inicial — decidida durante o planejamento do
backend de contas/save na nuvem (2026-07-14), quando ficou claro que o modelo de dados do
Firestore para progressão de personagem precisava já prever isso.

**Regras, sem exceção:**

1. **O campo de diamantes nunca pode ter permissão de escrita direta pelo cliente no Firestore.**
   Regra de segurança: `allow write: if false;` no documento/campo de moeda — mesmo pro próprio
   dono da conta. Não existe caminho em que o SDK client-side grava um novo saldo de diamante
   diretamente.
2. **Toda alteração de diamante passa obrigatoriamente por uma Cloud Function** — ganhar por
   vitória, gastar em reroll de level-up, reset de build, comprar pacote, o que for. O cliente
   nunca envia "novo saldo": envia a **intenção** (ex.: "gastar 50 diamantes no reroll do slot X"),
   e a function calcula o resultado e escreve.
3. **Compra com dinheiro real exige validação server-side do recibo** (Google Play Billing /
   Apple StoreKit) dentro da Cloud Function, **antes** de creditar qualquer diamante. Nunca
   confiar no cliente dizer "comprei o pacote X".
4. **Consequência**: diamantes precisam de Cloud Functions **desde o dia 1** da implementação da
   monetização — não dá pra esperar a Fase 8 (anti-cheat/validação server-side geral) pra isso
   especificamente, porque aqui envolve dinheiro real. O resto do jogo (combate, level, skills)
   pode continuar no modelo "cliente confiável" por mais tempo — moeda premium não.

**Atualização 2026-07-19 — exceção temporária registrada, não um relaxamento da regra**: o campo
`diamonds` (`users/{uid}`, ver `WalletService.cs`) passou a existir de verdade, junto do sistema de
energia (HUD de moeda/diamante/energia, `EnergyService.cs`/`CharacterPanel.BuildWalletBar`/
`MainMenuCharacterPreview.BuildEnergyHud`) — mas o projeto ainda não tem NENHUMA Cloud Function
implantada. Decisão explícita do usuário ao encomendar essa feature: entregar
`WalletService.SpendDiamondsAsync` como um placeholder client-writable por enquanto (mesmo modelo
"cliente confiável" que `level`/`str`/etc já usam), isolado numa função só, marcada com TODO —
trocar por uma Cloud Function callable assim que a Fase 4/Monetização criar o projeto de Functions
de verdade, sem mudar a assinatura pro chamador. A regra abaixo (nunca client-writable) continua
sendo o alvo final; isto é uma exceção datada e sinalizada, não uma reversão silenciosa dela.

**Não implementado ainda** (correto deixar pra quando a Fase 4 for de fato construída) — só a
regra em si é fixa e não deve ser esquecida/relaxada quando esse dia chegar.

**Primeira implementação real da regra (2026-07-23) — só no caminho do case opening**: a Cloud
Function `purchaseCase` (`functions/src/purchaseCase.ts`, ver seção "Modelo de roster
multi-personagem" abaixo) é a PRIMEIRA gravação de diamante do projeto que segue a regra à risca —
lê `users/{uid}.diamonds` via Admin SDK (nunca confia em saldo enviado pelo cliente) e debita
dentro de uma transaction, só depois de validar o pagamento (recibo mock/cash ou saldo
suficiente/moeda). Isto **não migra** `WalletService.SpendDiamondsAsync` (usado por
reroll/refill/etc.) pra Cloud Function — esse continua sendo o placeholder client-writable
descrito acima, propositalmente fora do escopo desta tarefa. `casePackages/{packageId}` e
`users/{uid}/casePurchases/{packageId}` (novo, contador de limite de compra por jogador) seguem a
mesma regra de "nunca client-writable": `allow write: if false` no `firestore.rules`, únicas
escritas possíveis são via Admin SDK (a própria function, ou o script de seed
`functions/src/scripts/seedCasePackages.ts`).

**2ª implementação real da regra (2026-07-25) — refresh dos unlocks de skill/arma/pet do case
opening**: Cloud Function `rerollUnlock` (`functions/src/rerollUnlock.ts`) — custo FIXO de 15
diamantes (não escala) e limite de 2 refreshes por unlock individual, ambos validados/decididos
100% server-side (Admin SDK lê `users/{uid}.diamonds` e o contador
`users/{uid}/characters/{characterId}.unlockRerollCounts`, nunca confia em nada enviado pelo
cliente) — mesma transaction debita o diamante e incrementa o contador atomicamente com o
resorteio. O sorteio ORIGINAL de cada unlock (sem custo) continua 100% client-side
(`CharacterUnlockEngine.DrawUnlock`, mesmo nível de confiança client-authoritative de qualquer
recompensa do level-up de combate) — só o REFRESH, por gastar dinheiro premium de verdade,
precisou de Cloud Function. `functions/src/unlockCatalog.json` (gerado por `Tools > AutoArms >
Export Unlock Catalog for Cloud Function`) é o espelho server-side dos databases de skill/arma/pet
(odds + quais tiers cada família realmente tem), mesmo papel de `characterCatalog.json` pra
`purchaseCase`. A validação/sorteio/débito em si (`performSlotReroll`) foi extraída pra
`functions/src/rerollShared.ts` (2026-07-26) — reaproveitada por `rerollRebirthGrant.ts` (reroll
individual dos itens concedidos pelo Renascimento, campo de contador PRÓPRIO
`rebirthUnlockRerollCounts`, nunca reaproveita `unlockRerollCounts` pra não colidir).

**TODO de segurança do "Novo Sorteio" (level-up de combate, `CombatResultPanel.cs`) fechado em
2026-07-26** — antes gastava diamante direto do cliente (`WalletService.SpendDiamondsAsync`,
placeholder client-writable) com custo PROGRESSIVO (50→100→200). Nova Cloud Function
`rerollLevelUpBoxes.ts` valida/debita sempre no servidor, custo agora FIXO (reusa
`REROLL_COST_DIAMONDS=15` de `rerollShared.ts`, o MESMO valor de `rerollUnlock` — não um valor
novo), limite de 3 usos por level-up escopado por `levelUpRerollCount`/`levelUpRerollAtLevel`
(reinicia sozinho quando o `level` do documento muda, sem precisar de "session id"). Escopo
DELIBERADAMENTE reduzido em relação a `rerollUnlock`/`rerollRebirthGrant`: só a AUTORIZAÇÃO do
gasto é server-side — o sorteio das N caixas em si continua client-side (pirâmide + resume
`pendingLevelUpBoxes`, sistema já validado e com histórico extenso de bugs corrigidos; portar o
motor de sorteio inteiro pra fechar uma brecha de baixo valor econômico — o conteúdo de cada caixa
já é obtível de graça no level-up normal, "Novo Sorteio" é só conveniência — não valeu o risco de
regressão). Revisitar se essa brecha residual virar um problema real medido em produção.

## Regras de segurança do Firestore — `users/{uid}/characters`

Implementado na Fatia 3 do plano de contas/save na nuvem (2026-07-15). `allow write: if
request.auth.uid == uid` sozinho só verifica QUEM escreve, não O QUE está sendo escrito —
deixaria qualquer dono mandar `str: 9999`/`level: 9999` direto pelo SDK client-side (pedido
explícito do usuário, feito antes de aprovar o plano original). As regras abaixo validam formato
e faixa também, amarradas ao próprio `level` do documento (não um teto fixo solto) — margem
generosa de propósito, só pra barrar valor grosseiramente fora da curva de progressão real
(level-up dá no máximo +8 HP / +2 STR/AGI/SPD por escolha, ver `CombatResultPanel.ApplyBonus`).
Os valores exatos das constantes (`20 +`, `4 *`, `9999`, os tetos de array) são um ponto de
partida — ajustar se o balanceamento mudar, mas a regra em si (amarrada ao level, com teto de
array) deve continuar existindo.

Colar em **Firebase Console > Firestore Database > Regras** (substitui o "modo de teste" usado
até aqui):

```
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /users/{uid} {
      allow read, write: if request.auth != null && request.auth.uid == uid;

      match /characters/{characterId} {
        allow read: if request.auth != null && request.auth.uid == uid;
        // `create, update` (não `write` puro) — ver nota abaixo sobre a armadilha de `delete`.
        // Nenhum código apaga um characters/{characterId} hoje, mas separado por precaução (o
        // mesmo bug real já aconteceu em replays/{replayId}, ver logo abaixo).
        allow create, update: if request.auth != null && request.auth.uid == uid
          && request.resource.data.level is int
          && request.resource.data.level >= 1
          && request.resource.data.level <= 9999
          && request.resource.data.str is number
          && request.resource.data.str <= 20 + 4 * request.resource.data.level
          && request.resource.data.agility is number
          && request.resource.data.agility <= 20 + 4 * request.resource.data.level
          && request.resource.data.speed is number
          && request.resource.data.speed <= 20 + 4 * request.resource.data.level
          && request.resource.data.maxHealth is number
          && request.resource.data.maxHealth <= 80 + 16 * request.resource.data.level
          && request.resource.data.weapons is list
          && request.resource.data.weapons.size() <= 50
          && request.resource.data.skills is list
          && request.resource.data.skills.size() <= 100;

        // Replay (log de eventos completo, 2026-07-18 — ver CHANGELOG.md e a nota "Replay
        // fabricado client-side" mais abaixo). Cap de 400 eventos cobre o teto de segurança do
        // próprio CombatSimulator (maxRounds=300, ver CombatSimulator.cs) com folga. `result` só
        // aceita os 2 valores válidos — não impede um cliente forjar um replay falso (ver nota),
        // só barra um documento grosseiramente mal formado.
        //
        // Bug real corrigido (2026-07-20): era um único `allow write` cobrindo create+update+
        // DELETE com essa mesma validação de `request.resource.data.*` — mas numa operação de
        // delete, `request.resource` é `null` (não existe "dado novo" a validar), então QUALQUER
        // delete era rejeitado com "Missing or insufficient permissions". `FirestoreService.
        // TrimOldReplaysAsync` apaga replays além do teto de `MaxReplaysPerCharacter` (10) — todo
        // delete falhava silenciosamente (dentro do mesmo try/catch do save), aparentando "falha
        // ao salvar replay" no log quando na real o replay NOVO salvava certinho (create passa
        // pela validação normal); só a limpeza dos antigos nunca funcionava, acumulando document
        // os sem rotação. Separado em `create, update` (com a validação de dados) + `delete` (só
        // checagem de dono, sem exigir `request.resource.data` nenhum).
        match /replays/{replayId} {
          allow read: if request.auth != null && request.auth.uid == uid;
          allow create, update: if request.auth != null && request.auth.uid == uid
            && request.resource.data.result in ["win", "loss"]
            && request.resource.data.events is list
            && request.resource.data.events.size() <= 400;
          allow delete: if request.auth != null && request.auth.uid == uid;
        }
      }

      match /matchHistory/{opponentId} {
        allow read, write: if request.auth != null && request.auth.uid == uid;
      }
    }

    // opponents_index (Fatia 5, implementado em 2026-07-15; lido pela busca de adversário na
    // Fatia 6): qualquer usuário autenticado pode ler (é a superfície pública), só o dono pode
    // escrever o próprio doc. A regra abaixo não depende do nome do path variable ({characterId}
    // aqui é só um placeholder de segmento — não precisa bater com nenhum campo), já que a
    // checagem real é sempre sobre o campo `ownerUid` dentro do documento. Mesma validação de
    // faixa amarrada ao `level` que `characters/{characterId}` já tem acima (Fatia 6, 2026-07-15)
    // — necessário porque, desde a correção da Fatia 6, este documento também carrega os stats
    // BASE (não só os `eff*` de exibição), então fica sujeito ao mesmo risco de valor
    // grosseiramente forjado pelo cliente.
    match /opponents_index/{characterId} {
      allow read: if request.auth != null;
      // `create, update` (não `write` puro, mesmo motivo do fix em replays/{replayId} acima) —
      // nenhum código apaga um opponents_index hoje, separado só por precaução.
      allow create, update: if request.auth != null && request.auth.uid == request.resource.data.ownerUid
        && request.resource.data.level is int
        && request.resource.data.level >= 1
        && request.resource.data.level <= 9999
        && request.resource.data.str is number
        && request.resource.data.str <= 20 + 4 * request.resource.data.level
        && request.resource.data.agility is number
        && request.resource.data.agility <= 20 + 4 * request.resource.data.level
        && request.resource.data.speed is number
        && request.resource.data.speed <= 20 + 4 * request.resource.data.level
        && request.resource.data.maxHealth is number
        && request.resource.data.maxHealth <= 80 + 16 * request.resource.data.level
        && request.resource.data.weapons is list
        && request.resource.data.weapons.size() <= 50
        && request.resource.data.skills is list
        && request.resource.data.skills.size() <= 100;
    }
  }
}
```

**ID do documento em `opponents_index` é `{ownerUid}_{characterId}`, NÃO só `characterId`**
(`FirestoreService.OpponentIndexDoc`, Fatia 5) — `characterId` hoje é só o nome do personagem
(`PlayerProfile.OpponentId()`), que não é único entre contas diferentes; duas contas jogando de
"Medieval Warrior" colidiriam no mesmo documento se o ID fosse só `characterId`. `ownerUid` e
`characterId` continuam gravados como campos dentro do documento também, pra consulta/exibição —
a regra acima não depende de saber a composição do ID, só lê os campos.

Isso não substitui validação server-side de verdade (Cloud Function, Fase 8, correto deixar pra
depois) — só fecha o caso mais grosseiro de edição direta de documento sem precisar de Cloud
Function agora.

## Modelo de roster multi-personagem (2026-07-23)

Contexto: até 2026-07-21 (`MONETIZACAO.md`, seção "Itens em aberto") a persistência de múltiplos
personagens por conta estava marcada como **"NÃO FAZER AINDA"**, pedido explícito do usuário —
decisão revertida em 2026-07-23 para viabilizar o sistema de compra de personagens/case opening
(Loja, aba Personagens). Esta seção documenta o modelo adotado.

**Descoberta que evitou uma migração de schema**: `users/{uid}/characters/{characterId}` **já
era** uma subcoleção (não um documento único) desde a Fatia 3 (2026-07-15) — o "1 conta = 1
personagem" de até então era só uma limitação do CLIENTE (nada além do personagem selecionado era
lido/escrito), não do schema do Firestore. Ganhar um personagem novo é simplesmente criar mais um
documento nessa mesma subcoleção.

**Dois IDs distintos, propósitos diferentes:**
- `characterId` — ID **único desta instância** de personagem (documento). Pro personagem
  "original" de cada conta (o único que existia antes desta data), continua sendo
  `PlayerProfile.OpponentId()` (nome do asset, ou vazio → nome). Pra personagem concedido via
  `purchaseCase` (Cloud Function), é um ID auto-gerado pelo Firestore (`charactersRef.doc()`,
  nunca reaproveita o nome do template) — permite, em tese, que contas diferentes possuam
  instâncias do mesmo personagem sem colidir, e (futuramente) que a mesma conta possua mais de
  uma instância do mesmo `characterTypeId` sem duas gravarem no mesmo documento.
- `characterTypeId` (campo novo em `CharacterDTO`/`CharacterDTOMap`, 2026-07-23) — referência ao
  "molde" `PlayerProfile` de origem, igual ao **nome do asset Unity** (mesma convenção que
  `PlayerProfileConverter.FromOpponentIndexMap` já usava pra achar o molde visual de um
  oponente). Vazio nos documentos gravados antes desta data (retrocompatível, sem migração
  necessária). `PlayerProfileConverter.FromCharacterDTO(dto, templateCatalog)` reconstrói um
  `PlayerProfile` runtime a partir de um documento do roster, casando por `characterTypeId` —
  mesma técnica de `FromOpponentIndexMap`, mas indexando pelo campo novo em vez de `characterId`.
  `FromCharacterMap(map, templateCatalog)` continua existindo como wrapper fino sobre
  `FromCharacterDTO` pra quem só tem o `Dictionary` cru de um `DocumentSnapshot`.

**Quem concede um personagem**: só Cloud Functions (Admin SDK) criam um documento novo em
`users/{uid}/characters/{id}` — o cliente nunca cria um documento de personagem do zero, só
atualiza um que já possui (mesmo fluxo de sempre, `FirestoreService.SaveCharacterAsync`).
`RosterService.ListOwnedCharacterDocsAsync` (cliente) só LÊ o roster inteiro. Três functions
concedem personagem hoje, todas reaproveitando o mesmo shape de documento
(`functions/src/grantCharacter.ts`, extraído 2026-07-27 pra não duplicar entre elas):
`purchaseCase.ts` (case opening, cash/diamante), `purchaseNextCharacter.ts` (2026-07-26, "Próximo
Personagem" na Loja, preço em Coins escalando por contador do jogador em vez de preço fixo por
pacote) e `grantStarterCharacter.ts` (onboarding, 1x por conta). `rebirthCharacter.ts` (2026-07-26,
Renascimento) é diferente — não cria personagem novo, RESETA um já existente pro Level 1 com
loadout/stats re-sorteados, mesmo princípio de nunca confiar no cliente.

**Migração de `CharacterSelectController`/`SelectedProfileHolder`/`MainMenuCharacterPreview`
concluída em 2026-07-24** (estava deliberadamente pendente desde 2026-07-23 — ver histórico no
`CHANGELOG.md`). Um personagem concedido via `purchaseCase` agora é genuinamente selecionável e
jogável, não só persistido:

- `PlayerProfileConverter.FromCharacterDTO` passou a marcar `isUnlockedForSelection = true;
  isPlayable = true;` (era `false/false`, "ainda não é jogável") — esses dois campos são o
  mesmo "gate" que `CharacterSelectController` (grid) e `MainMenuCharacterPreview` (troca rápida)
  já usavam pra decidir quem aparece/é clicável; só precisou inverter o default.
- `CharacterSelectController.PopulateCharacterGridRoutine` mescla os assets pré-autorados
  (`CharacterDatabase.unlockedCharacters`, comportamento de sempre — inclui o personagem
  "original" da conta) com `_rosterProfiles` (buscado assincronamente por
  `LoadRosterAndRefreshGridAsync`, via `RosterService.ListOwnedCharacterDocsAsync` +
  `FromCharacterDTO`, filtrando só documentos com `characterTypeId` preenchido — o doc legado sem
  esse campo já está representado pelo asset, nunca duplicado). Grid nasce só com os assets (0
  latência) e reconstrói quando o roster chega — mesmo padrão de
  `ShopController.LoadPersistedShopStateAsync`.
- `MainMenuCharacterPreview` (troca rápida por seta/swipe) segue o mesmo padrão —
  `_orderedProfiles` nasce de `CharacterDatabase.GetPlayableCharactersOrdered()` e é mesclado com
  o roster assim que `LoadRosterAndMergeOrderedProfilesAsync` completa, sem respawnar o
  personagem já exibido.
- `SelectedProfileHolder` ganhou o campo `characterId` (sincronizado por `SetProfile()`, que
  substituiu toda atribuição direta a `.currentProfile` nos dois arquivos acima) — ID de
  instância do roster do personagem selecionado, independente da identidade do objeto
  `PlayerProfile` em si (asset ou runtime).
- Novo `PendingCharacterSelection` (`Assets/Scripts/Data/`, canal estático cross-scene, mesmo
  padrão de `ReplayPlaybackState`) — handoff do case opening pra seleção:
  `CaseOpeningPopup`("Continuar") grava o `grantedCharacterId` (novo campo retornado por
  `purchaseCase`, distinto de `wonCharacterTypeId`/o molde) e navega direto pra
  `02_SelectCharacter`; `CharacterSelectController.ResolvePendingCharacterSelection` lê+limpa o
  campo assim que o roster carrega e abre o detalhe automaticamente (`OnCharacterSelected`, mesmo
  efeito de clicar o card manualmente). Deliberadamente separado de
  `SelectedProfileHolder.currentProfile` — este representa o personagem CONFIRMADO pra combate
  (só muda em `OnClickSelect`), um conceito diferente de "destacar ao abrir a tela".

**Risco real identificado e corrigido junto desta migração**: `PlayerProfileConverter.
_pristineSnapshots`/`_ownerScope` são `Dictionary<PlayerProfile, ...>` chaveados por referência de
objeto, pensados pra um conjunto pequeno e estável (~72 assets) — nunca removem entradas.
Alimentá-los com o fluxo de instâncias runtime que esta migração passou a gerar (uma por
fetch/troca de roster) viraria vazamento de memória sem teto, e o mecanismo de isolamento entre
contas (a razão desses dicionários existirem) ficaria inerte pra essas instâncias (nunca são
reaproveitadas entre contas, então não precisam dessa proteção). Fix: novo
`PlayerProfile.isRuntimeInstance` (`[NonSerialized]`, setado por `FromCharacterDTO`/
`FromOpponentIndexMap`) guarda `CapturePristineIfNeeded`/`MarkOwnerScope` pra pular essas
instâncias por completo.

**Ainda fora de escopo (confirmado, não implementado)**: restaurar "o último personagem
selecionado" entre reinícios do app — `LoginController` continua carregando
`SelectedProfileHolder.currentProfile` a partir do valor "wireado no Inspector" (comportamento
documentado, preservado de propósito); esta migração funciona só dentro de uma sessão em
execução.

**3ª implementação real da regra "quem concede um personagem" (2026-07-24) — onboarding/escolha
do 1º personagem**: Cloud Function `grantStarterCharacter` (`functions/src/
grantStarterCharacter.ts`), deliberadamente SEPARADA de `purchaseCase` (pedido explícito do
usuário — um fluxo grátis de onboarding não deve se acoplar à lógica de preço/recibo/contador de
compras que `purchaseCase` já tem, risco de bug futuro num fluxo que já funciona). Concede 1 dos 4
personagens "normais" de onboarding (Medieval Warrior/Medieval Warrior Girl/Citizen 1/Citizen
Women 2) — Admin SDK, dentro de uma transaction que primeiro verifica se a conta já possui
QUALQUER personagem (não só deste template); se sim, `failed-precondition`. Sem regra de `delete`
em `characters/{id}`, esse gate é definitivo (não existe caminho client-side de "concede, apaga,
concede de novo"). Stats vêm de uma constante fixa por template (hardcoded na function, extraída
dos `.asset` reais — só 4 personagens, não justifica um catálogo/pipeline de export dedicado como
`unlockCatalog.json`). A 1ª skill é sorteada 100% server-side (nunca no cliente): pondera pelos
odds reais de `unlockCatalog.json` (tier 1) pra achar 2 candidatos distintos, decide entre os dois
com uma moeda justa (`crypto.randomInt`) — "sortear entre 2 opções, sem escolha manual", pedido
explícito do usuário. Documento gravado com `caseUnlocksResolved: true` (diferente de um
personagem de case opening real) — sem isso, `CharacterSelectController.ResolveCaseUnlocksAsync`
trataria esta instância como "case opening ainda não revelado" e concederia mais 1 item (o bônus
de boas-vindas que `CharacterUnlockEngine.UnlockCountForRarity(Normal) == 1` dá a qualquer
personagem Normal), duplicando a concessão de item pro onboarding.

Consequência da mudança: `Medieval Warrior.asset` (o único personagem com `isPlayable=true` desde
2026-07-14, ver CHANGELOG.md — usado como "personagem de teste" fixo, sem escolha real) voltou pra
`isPlayable=false`, igual aos outros 71 — jogabilidade agora vem exclusivamente de instância
possuída no roster (concedida por `purchaseCase` OU `grantStarterCharacter`), nunca mais do asset
base compartilhado. **Contas de teste anteriores a esta mudança** têm `characters/Medieval
Warrior` com `characterTypeId` vazio (padrão "personagem original" pré-2026-07-23, ver acima) —
precisam de backfill manual desse campo (script Admin SDK avulso, mesmo estilo de
`functions/src/scripts/seedCasePackages.ts`) ou simplesmente recriar a conta, senão ficam sem
nenhum personagem jogável depois desta mudança.

## Stat base vs. stat efetivo — nunca confundir os dois em UI voltada a PvP

Contexto: bug real investigado em 2026-07-15 (não era o bug reportado, mas motivou este registro
pra não virar um de verdade mais tarde) — `PlayerProfile.speed`/`str`/`agility`/`maxHealth` são
os valores **base** (o que o level-up soma direto), enquanto `PlayerProfile.GetEffectiveStats()`
retorna esses mesmos stats **com os bônus PERCENTUAIS de skills passivas já somados** (ex:
Lightning Bolt em cima de `speed`), calculado ao vivo, nunca persistido.

**Regra:**

- `users/{uid}/characters/{characterId}` (Fatia 3, implementado) guarda o valor **base**
  (`PlayerProfileConverter.ToDTO` lê `profile.speed` etc. direto) — é o correto pra esse
  documento, porque é reaplicado sobre `GetEffectiveStats()` de novo a cada carregamento; salvar
  o valor efetivo duplicaria o bônus percentual da skill a cada sincronização.
- `opponents_index/{ownerUid}_{characterId}` (Fatia 5, implementado em 2026-07-15) é uma
  superfície **diferente**, feita pra ser lida/exibida por OUTROS jogadores antes de uma luta —
  guarda os DOIS conjuntos de campos, com propósitos diferentes: os campos prefixados `eff*`
  (`effHp`, `effStr`, `effAgility`, `effSpeed`) são pra EXIBIÇÃO rápida (card de oponente) sem
  precisar recalcular nada no cliente; os campos base (`str`/`agility`/`speed`/`maxHealth`/
  `weapons`/`skills`, mesmo formato de `characters/{id}`) são o que
  `PlayerProfileConverter.FromOpponentIndexMap` (Fatia 6) usa pra reconstruir um `PlayerProfile`
  de verdade e permitir lutar contra esse adversário — **nunca usar os campos `eff*` pra
  reconstruir um personagem lutável**: isso re-somaria o bônus percentual da skill em cima de um
  valor que já o contém (mesmo erro de duplicação, na direção oposta).
- Qualquer tela que mostre stat de personagem pra decisão de PvP (card de oponente, comparação
  pré-luta) deve usar os campos `eff*`/`GetEffectiveStats()`, nunca o base direto — mesmo padrão
  que `SelectOpponentController`/`CharacterPanel` já seguem hoje.
- Ao implementar a escrita de `opponents_index` (Fatia 5): calcular os `eff*` no MESMO momento em
  que `characters/{characterId}` é salvo (depois do level-up choice resolvido, mesmo ponto único
  de save já corrigido em `AttackSequencer`/`CombatResultPanel`) — não em um momento separado,
  senão os dois documentos podem ficar dessincronizados um do outro.

## Isolamento entre contas — save local e estado em memória devem ser escopados por uid

Contexto: bug real reportado pelo usuário (2026-07-15) — criar uma conta nova no mesmo
executável/device onde outra conta já tinha sido testada fazia a conta nova herdar os
personagens/progresso da conta antiga. Duas causas distintas, ambas por não terem vínculo de conta
nenhum: `LocalSaveService`/`save.json` (persistido em disco) e o estado em memória dos
`PlayerProfile` (`ScriptableObject`s, vivem durante todo o processo, não só durante uma cena).

**Regra, pra qualquer sistema futuro de save/progressão por conta:**

1. **Nenhum save local (arquivo em disco, `PlayerPrefs`, cache em memória) pode usar uma chave que
   não inclua o uid da conta** (ou um valor fixo tipo `"offline"` pra sem-sessão) — só `characterId`/
   `opponentId` sozinho não é suficiente, porque o mesmo device/executável pode logar contas
   diferentes ao longo do tempo, e uma delas pode ter progredido no mesmo personagem "molde" que a
   outra. Ver `LocalSaveService.CurrentScope()`/`CacheKey`.
2. **Qualquer objeto em memória que recebe dado de progressão específico de uma conta (ex:
   `PlayerProfile` via `ApplyDTO`) precisa ser resetável ao trocar de conta sem fechar o app** — um
   `ScriptableObject` (ou qualquer singleton/estático) não é implicitamente "por sessão de login",
   é por PROCESSO inteiro. Todo fluxo de logout/troca de conta deve devolver esses objetos ao
   estado "de fábrica" (não vinculado a nenhuma conta) antes da próxima conta poder logar — ver
   `PlayerProfileConverter.CapturePristineIfNeeded`/`RestoreAllPristine`,
   `MainMenuController.OnLogoutClicked`.
3. Dado gravado ANTES de uma correção deste tipo (sem o vínculo de conta) deve ser tratado como
   **órfão/não confiável ao ler**, nunca aplicado a nenhuma conta específica — não tentar
   "adivinhar" a quem pertencia.
4. **(2ª rodada, 2026-07-15) O reset de logout (regra 2) sozinho não é suficiente como única
   linha de defesa.** O fix original dependia inteiramente de `RestoreAllPristine` nunca falhar em
   restaurar TODO objeto tocado antes da próxima gravação — um modelo "opt-out" (assume que é
   seguro persistir o que estiver em memória). Bug persistiu mesmo assim (conta nova recebeu
   personagem em level intermediário). **Qualquer código que possa GRAVAR (local ou nuvem) o
   estado em memória de um objeto compartilhado como se fosse dado de uma conta deve validar
   afirmativamente a posse antes de gravar** ("opt-in" — só grava se puder provar que o dado
   pertence a esta conta: veio dela agora, já era dela, está genuinamente intocado, ou é dado
   pré-login legitimamente reivindicável), não confiar que um reset anterior necessariamente
   funcionou. Ver `PlayerProfileConverter.MarkOwnerScope`/`GetOwnerScope`,
   `CloudSyncService.SyncCharacterAsync`.

## Firestore `PersistenceEnabled` — nunca rodar 2 processos do jogo ao mesmo tempo no mesmo PC

Contexto: bug real diagnosticado via crash dump (2026-07-15) — build Windows (`Development Build`)
fechava sozinho ao criar conta, mas funcionava normalmente no Editor (Play Mode). Causa raiz (achada
no `.dmp` de crash, string em texto puro): `FIRESTORE INTERNAL ASSERTION FAILED... Failed to open DB:
LevelDB error: IO error: .../LOCK: O arquivo já está sendo usado por outro processo`.

`FirestoreService.EnsurePersistence()` liga `Db.Settings.PersistenceEnabled = true` — o cache
offline nativo do Firestore (LevelDB) usa um arquivo de lock exclusivo em
`%LOCALAPPDATA%\firestore\__FIRAPP_DEFAULT\{project-id}\...`, **compartilhado por processo/máquina
para o mesmo projeto Firebase**, não por instância do jogo. Se dois processos (Unity Editor em Play
Mode + build standalone, ou duas execuções do build) tentam abrir esse cache ao mesmo tempo, o
segundo falha ao adquirir o lock — e o SDK C++ do Firestore trata essa falha como uma **internal
assertion**, chamando `abort()` direto (crash irrecuperável, sem exceção .NET capturável por nenhum
`try/catch` do nosso código).

**Decisão do usuário (2026-07-15)**: manter `PersistenceEnabled = true` — não vale a pena abrir mão
do cache nativo por esse risco. Consequência prática: **nunca rodar o Unity Editor em Play Mode e um
build ao mesmo tempo no mesmo PC** (nem duas instâncias do build) enquanto o projeto usar o mesmo
Firebase project ID — isso vale tanto pra testes quanto, em tese, pra qualquer jogador que abra o
executável duas vezes. Não é um bug do nosso código — é uma limitação conhecida da SDK C++ do
Firestore; revisitar (`PersistenceEnabled = false`, já que `LocalSaveService` cobre o offline real)
se esse tipo de crash voltar a acontecer sem a causa óbvia de "2 instâncias rodando".

## Replay fabricado client-side — dívida técnica conhecida e aceita por enquanto

Contexto: análise do sistema de replay (2026-07-18, ver CHANGELOG.md) — `AttackSequencer.OnCombatEnd`
grava o log de eventos completo da luta (`ReplayRecorder`/`ReplayDTO`) em
`users/{uid}/characters/{characterId}/replays/{replayId}` depois que o **cliente** já rodou
`CombatSimulator` e decidiu quem venceu. As regras do Firestore (ver acima) só validam a FORMA do
documento (`result` é "win"/"loss", `events` é lista, cap de tamanho) — não existe nenhuma
validação server-side do CONTEÚDO. Nada impede um cliente modificado gravar um replay inteiramente
inventado (ex: "venci" contra um adversário que nunca existiu, ou um log de eventos que não bate
com nenhuma luta real).

**Decisão consciente**: aceitar esse risco por enquanto, sem bloquear a feature nele. Diferente da
regra de diamantes (moeda premium, dinheiro real — ver "Moeda premium" no topo deste arquivo), um
replay forjado não move nenhum recurso de ninguém — o dano é só cosmético (um jogador mentir pra si
mesmo sobre o próprio histórico). Não vale a pena exigir Cloud Function **desde o dia 1** pra isso,
ao contrário de diamantes.

**Revisitar na Fase 8** (anti-cheat/validação server-side geral, ver ROADMAP_FUTURO.md), junto do
resto do trabalho de validação de resultado de luta — nesse ponto o projeto já deve ter Cloud
Functions de verdade implantadas por outro motivo (ex: a própria regra de diamantes acima), e dá
pra reaproveitar essa infra em vez de justificar uma implantação nova só pra replay.

## Nome de exibição público — nickname da conta + nome do personagem (requisito futuro)

Contexto: registrado por pedido explícito do usuário (2026-07-15) — o sistema de nickname/apelido
de usuário **ainda não existe** no projeto. Esta regra é só requisito de design pra quando ele for
implementado; não implementar nada agora.

**Regra:**

1. O nome exibido de um personagem em qualquer lugar **público** (`opponents_index`, ranking
   futuro, histórico de batalha, etc.) deve ser a concatenação
   **`{nickname da conta} - {nome do personagem}`** — ex.: `"Hiroaki - Medieval Warrior"`.
2. `profileName` (já existe hoje em `characters/{id}` e `opponents_index/{id}`) continua guardando
   **só** o nome do personagem (ex.: `"Medieval Warrior"`) — isso não muda quando o nickname for
   implementado.
3. O nickname mora em `users/{uid}` (documento da CONTA), **nunca** dentro de
   `characters/{characterId}` — é um dado por conta, não por personagem; o mesmo nickname vale
   pra todos os personagens daquele jogador.
4. A concatenação `"{nickname} - {nome do personagem}"` é feita **só na hora de exibir** (UI —
   card de oponente, ranking, etc.), lendo os dois campos separadamente e montando a string em
   runtime — **nunca gravada como string fixa** em nenhum documento do Firestore. Isso garante
   que, se o jogador trocar o nickname depois, todo personagem já salvo reflete o novo nickname
   automaticamente, sem precisar reescrever nenhum documento existente.

**Consequência prática pra quando `opponents_index` for lido de verdade (Fatia 6)**: montar o
nome de exibição do card de oponente vai exigir buscar o nickname em `users/{ownerUid}` (um
documento a mais por oponente listado, já que `opponents_index` não guarda o nickname em si) —
considerar isso no desenho da query/paginação da busca de adversário quando ela for implementada.
