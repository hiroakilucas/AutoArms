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
   (`users/{uid}/characters`, `users/{uid}/matchHistory`).

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

## Verificação

Depois dos 3 passos: criar uma conta nova, confirmar que ela vem 100% vazia (nenhum personagem,
level 1 se algum profile for tocado) e que `opponents_index`/`05_SelectOpponent` não mostra nenhum
adversário real (cai no pool local de fallback, já que não há mais nenhuma conta com personagem
salvo).

## Automatizar (não feito ainda)

Dá pra escrever um script Node.js com o SDK `firebase-admin` que apaga Auth + Firestore de uma vez
só, em vez do passo a passo manual acima — precisa de uma chave de conta de serviço gerada em
Project Settings → Service Accounts → Generate new private key. Considerar se o processo de limpeza
virar frequente (ex: antes de cada rodada de teste de matchmaking).
