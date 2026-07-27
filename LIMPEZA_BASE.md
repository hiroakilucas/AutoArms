# AutoArms — Como limpar a base Firebase (Auth + Firestore) e o cache local

Passo a passo pra zerar completamente a base de contas/personagens (Firebase Auth + Firestore) e
todo resíduo local que sobrevive no dispositivo — usado quando se quer recomeçar os testes do zero,
sem nenhuma conta/personagem antigo contaminando o resultado. Feito manualmente pelo usuário pela
primeira vez em 2026-07-15, antes de começar a implementação dos bots (ver ROADMAP_FUTURO.md/
tasks da sessão).

**Isso é destrutivo e não reversível sem backup.** Confirme que não há nada que precise ser mantido
antes de seguir os passos abaixo.

## 1. Apagar todos os usuários (Firebase Auth)

Pelo [Firebase Console](https://console.firebase.google.com/) → projeto `autoarms-c248f` →
**Authentication** → aba **Users**:
1. Marque o checkbox do cabeçalho da tabela (seleciona todos os usuários listados).
2. Botão **Delete account(s)**.

## 2. Apagar todos os dados do Firestore

**Firestore Database** → aba **Data**:
1. Cada coleção de topo (`users`, `opponents_index`) tem um menu **⋮** ao lado do nome →
   **Delete collection**. Isso apaga a coleção inteira, inclusive as subcoleções de cada documento
   (`users/{uid}/characters` — inclui personagens comprados via case opening, com `characterTypeId`
   preenchido, não só o personagem "original" — `users/{uid}/matchHistory`,
   `users/{uid}/casePurchases` — contador de limite de compra por pacote, 2026-07-23).

**Não apague `casePackages`** (2026-07-23, sistema de compra de personagens/case opening) — ao
contrário de `users`/`opponents_index`, essa coleção não é dado de conta/teste, é o CATÁLOGO
estático dos pacotes da Loja (preço/raridade/limite/odds), populado uma única vez por
`functions/src/scripts/seedCasePackages.ts` (Admin SDK, fora do app). Apagá-la por engano exige
rodar o seed de novo pra Loja voltar a mostrar preços; resetar conta/personagens não precisa
disso.

## 3. Limpar o que fica no dispositivo local

Sem isso, mesmo com o Firebase limpo, o jogo local ainda mostra dado velho — o resíduo não vem só
da nuvem:

- **`save.json`** (save local em JSON, `LocalSaveService`): apagar
  `%USERPROFILE%\AppData\LocalLow\DefaultCompany\AutoArms\save.json` (caminho exato depende do
  `companyName`/`productName` do build — confirmar em `ProjectSettings/ProjectSettings.asset` se
  mudar).
- **Cache offline nativo do Firestore** (LevelDB, `FirestoreService.PersistenceEnabled = true` —
  ver ARQUITETURA.md sobre o risco de rodar 2 processos ao mesmo tempo usando esse cache): apagar a
  pasta `%LOCALAPPDATA%\firestore\__FIRAPP_DEFAULT\autoarms-c248f\`.
- **Os `PlayerProfile.asset` do projeto**: se algum ficou com level/stats/favoritos alterados por
  sessões de teste no Editor (ScriptableObject assets persistem mudanças de Play Mode no editor,
  ver ARQUITETURA.md/"Firestore PersistenceEnabled"), rodar
  **Tools → AutoArms → Reset All Profiles to Level 1** antes de testar de novo.

## 4. Reiniciar o Play Mode/a Unity (recomendado, ver bug real abaixo)

**Contexto (2026-07-25)**: `ProjectSettings/EditorSettings.asset` tem o Domain Reload do Play Mode
DESLIGADO (`m_EnterPlayModeOptionsEnabled: 1`/`m_EnterPlayModeOptions: 1` — mitigação pro Editor
travar em "Waiting for Unity's code to finish executing" ao fechar, causado por threads nativas do
Firebase não sobrevivendo a um Domain Reload). Efeito colateral: parar/reiniciar o Play Mode
**não zera mais nenhum estado estático (C#) do processo** — antes disso era de graça a cada sessão
de Play, então os passos 1-3 acima sempre bastavam sozinhos.

Dois caches estáticos que dependiam desse reset de graça já foram corrigidos (2026-07-25):
`RosterService.SessionCache` (agora escopado por uid — nunca mais devolve personagem de uma conta
diferente) e os 4 caches de sprite de `UIShapeUtil.cs` (agora checam se o objeto cacheado ainda é
válido). Na prática, os passos 1-3 sozinhos já bastam de novo pra uma conta 100% limpa, MESMO sem
reiniciar a Unity — inclusive apagando a conta antiga direto pelo Firebase Console (sem clicar
"Sair da Conta" no app), que é o fluxo normal deste guia.

Ainda assim, **reiniciar o Play Mode (parar e apertar Play de novo) ou fechar e reabrir a Unity é
uma margem de segurança barata** contra qualquer outro estático que ainda não tenha sido mapeado —
sem custo real (poucos segundos), recomendado antes de cada rodada de teste "do zero" enquanto o
Domain Reload continuar desligado. Só fechar e reabrir a Unity de fato garante 100% (Domain Reload
não roda nem parando/reiniciando o Play Mode agora), mas parar/reiniciar já cobre a maioria dos
casos práticos (destrói e reconstrói toda a hierarquia de GameObjects/MonoBehaviours da cena, só
não toca em estáticos).

## Verificação

Depois dos passos acima: criar uma conta nova, confirmar que ela vem 100% vazia (nenhum
personagem, level 1 se algum profile for tocado) e que `opponents_index`/`05_SelectOpponent` não
mostra nenhum adversário real (cai no pool local de fallback, já que não há mais nenhuma conta com
personagem salvo).

## Automatizar (não feito ainda)

Dá pra escrever um script Node.js com o SDK `firebase-admin` que apaga Auth + Firestore de uma vez
só, em vez do passo a passo manual acima — precisa de uma chave de conta de serviço gerada em
Project Settings → Service Accounts → Generate new private key. Considerar se o processo de limpeza
virar frequente (ex: antes de cada rodada de teste de matchmaking).
