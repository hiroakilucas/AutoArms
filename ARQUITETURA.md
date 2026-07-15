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

**Não implementado ainda** (correto deixar pra quando a Fase 4 for de fato construída) — só a
regra em si é fixa e não deve ser esquecida/relaxada quando esse dia chegar.

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
        allow write: if request.auth != null && request.auth.uid == uid
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
      allow write: if request.auth != null && request.auth.uid == request.resource.data.ownerUid
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
