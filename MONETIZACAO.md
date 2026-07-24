# MONETIZACAO.md

Documento de referência para o sistema de monetização do AutoArms (Fase 4).

## Status
- [x] UI da loja (placeholder) — implementada 2026-07-20 (`06_Loja`/`ShopController.cs`/
  `ShopCardUI.cs`, 5 abas, grid de cards, navegação sem reload). Falta: aprovação de layout, arte
  final dos ícones/cards.
- [x] Persistência real do RESULTADO da compra (2026-07-21) — diamante creditado de verdade
  (`WalletService.AddDiamondsAsync`), desbloqueios/passe/progressão gravados em `users/{uid}`
  (`ShopStateService.cs`) em vez de resetar a cada sessão. Não é o mesmo que "gateway de pagamento
  real" abaixo — o clique em "Comprar" ainda não processa nenhum pagamento de verdade, só grava o
  resultado como se tivesse pago.
- [ ] Compra real de diamante (Google Play Billing / Apple StoreKit) — bloqueado até validação server-side (Fase 8)
- Desbloqueios/Passes/Progressão (seções 2-4) continuam modelados como **cash direto** (IAP), não
  gastam diamante — ver "Itens em aberto" abaixo (decisão já tomada, não é mais uma pergunta em
  aberto).

## 1. Loja de diamantes (cash)
| Diamantes | Preço (R$) | R$/diamante |
|---|---|---|
| 30 | 4,90 | 0,163 |
| 80 | 9,90 | 0,124 |
| 170 | 19,90 | 0,117 |
| 360 | 39,90 | 0,111 |
| 950 | 89,90 | 0,095 |
| 2000 | 179,90 | 0,090 |
| 4100 | 339,90 | 0,083 |
| 6100 | 449,90 | 0,074 |

## 2. Desbloqueios permanentes (cash, uma vez para sempre, todos os personagens)
| Item | Preço |
|---|---|
| Skip de batalha | R$ 9,90 |
| Velocidade 1.5x | R$ 9,90 |
| Bundle (skip + 1.5x) | R$ 14,90 |

## 3. Passes mensais (cash, 30 dias)
| Passe | Preço | Benefícios diários | Total 30 dias | R$/diamante |
|---|---|---|---|---|
| Básico | R$ 19,90 | +1 XP por vitória (todos os personagens), 8 diamantes, 8 moedas | 240 diamantes | 0,083 |
| Pro | R$ 39,90 | +2 XP por vitória (todos os personagens), 18 diamantes, 18 moedas | 540 diamantes | 0,074 |

## 4. Progressão — slots extras de skill no level up (cash, permanente, por conta)
Normalmente 2 opções de skill ao subir de nível. Cada compra adiciona +1 opção.
| Compra | Preço | Opções de skill no level up |
|---|---|---|
| Slot 1 | R$ 49,90 | 3 |
| Slot 2 | R$ 99,90 | 4 |
| Slot 3 | R$ 199,90 | 5 |

## 5. Personagem aleatório por raridade (cash, limite de compras, sem repetição)
| Raridade | Preço | Limite de compras | Pool disponível |
|---|---|---|---|
| Raro | R$ 99,00 | 10 | 19 personagens |
| Legendary | R$ 199,00 | 3 | 11 personagens |
| Imortal | R$ 249,00 | 1 | 3 personagens |

Regra de não-repetição: ao liberar um personagem, ele sai do pool de sorteio
daquela raridade — considerando toda a coleção do jogador (não só compras
desse gacha), incluindo personagens obtidos por level up normal, evento ou
passe.

## 6. Moeda (soft currency) — liberação de slot de personagem
| Liberação | Custo (moeda) |
|---|---|
| 1ª | 100 |
| 2ª | 200 |
| 3ª | 400 |
| 4ª | 600 |
| 5ª | 800 |
| 6ª | 1000 |
| 7ª em diante | +400 a cada liberação |

## 7. Distribuição de raridade de personagens
| Raridade | Qtd. personagens | % de chance total |
|---|---|---|
| Normal | 18 | 68% |
| Uncommon | 14 | 20% |
| Raro | 19 | 8% |
| Legendary | 11 | 3,5% |
| Imortal | 3 | 0,5% |

## 8. Paleta de cor — raridade de personagem
| Raridade | Cor |
|---|---|
| Normal | Cinza |
| Uncommon | Verde |
| Raro | Azul |
| Legendary | Laranja |
| Imortal | Vermelho |

Nota para o futuro (não implementar agora): alinhar cor de tier de arma/pet
à mesma progressão — T1 cinza, T2 verde, T3 azul — deixando espaço para
T4/T5 (laranja/vermelho). Substitui bronze/prata/ouro do Arsenal quando
essa migração for priorizada.

## 9. Estrutura da tela de loja (UI)
5 abas: Diamantes · Desbloqueios · Passes · Progressão · Personagens

Ordem de implementação:
1. UI com placeholder (caixas cinza + texto), navegação entre abas, fluxo de
   compra local (sem gravar no Firestore)
2. Aprovação de layout
3. Substituição de placeholder por arte final (Leonardo AI)
4. ~~Ligação com WalletService real~~ — feito 2026-07-21 (`ShopStateService.cs`,
   `WalletService.AddDiamondsAsync`); falta só o gateway de pagamento real (Fase 8) — sem ele, a
   escrita no Firestore acontece sem nenhum pagamento de verdade ter ocorrido.

## 10. Continuar jogando com energia zerada (diamante, por personagem)

Preço PROGRESSIVO por dia, POR PERSONAGEM (implementado 2026-07-21, `EnergySettings.
refillCostTier1/2/3Plus`, `EnergyService.GetRefillCostAsync`/`PayToRefillAsync`): 1ª vez no dia
(hora do servidor) = 10 diamantes, 2ª = 20, 3ª em diante = 40 (travado). Contador reseta sozinho à
meia-noite (hora do servidor, nunca o device) — não é uma aba da Loja, é o popup que aparece ao
clicar "Jogar" com energia em 0 (`MainMenuController.ShowRefillConfirmPopup`).

## 11. Novo Sorteio no Level-Up (diamante)

Implementado 2026-07-21 (`CombatResultPanel.ShowLevelUpChoice`) — desenho final divergiu do
"Reset de Level Up" do `ROADMAP_FUTURO.md` Fase 4 (ver nota lá): custo fixo em 3 degraus (1º
sorteio novo = 50 diamantes, 2º = 100, 3º = 200), travado depois do 3º uso por level-up, em vez de
dobrar indefinidamente. Refaz as N caixas do level-up (não só 2) com a mesma roleta de revelação.

## 12. Resetar Personagem (gera moeda — não é o "Reset de Build" do roadmap)

Implementado 2026-07-21 (`CharacterPanel`, botão no painel de detalhamento) — mecanismo PARALELO
ao "Reset de Build" ainda não implementado (`ROADMAP_FUTURO.md` Fase 4): reseta o personagem pro
Level 1 (não mantém o nível atual) e GERA `nível anterior × CharacterResetSettings.coinsPerLevel`
(10) moedas, em vez de custar diamante. Popup de confirmação explícita antes de executar (ação
destrutiva).

## 13. Case opening (compra de personagens, cash + moeda) — implementado 2026-07-23

Reverte a nota "NÃO FAZER AINDA" que existia aqui desde 2026-07-21 — o usuário pediu a
implementação real da persistência de personagens comprados, com uma tela de "abertura de case"
estilo CS:GO (roleta horizontal desacelerando até parar no personagem sorteado, sorteado no
servidor). Ver `ARQUITETURA.md` ("Modelo de roster multi-personagem") pro desenho completo.

- As 3 raridades cash da seção 5 (Raro/Legendary/Imortal) agora compram de verdade — sorteio
  server-side (Cloud Function `purchaseCase`), sem repetição (exclui personagens já possuídos),
  concede um documento novo em `users/{uid}/characters`. Limite de compras (10/3/1) continua **por
  jogador** (`users/{uid}/casePurchases/{packageId}`), não um estoque global.
- **Novo 4º pacote "Case Geral"** (moeda/diamante, `case_moeda_geral`): pool = todos os
  personagens de todas as raridades, sorteados pelas `tierWeights` da seção 7 (Normal 68% /
  Uncommon 20% / Raro 8% / Legendary 3,5% / Imortal 0,5%). Sem limite de compras. Preço em
  diamantes ajustável em `functions/src/scripts/seedCasePackages.ts` (`currencyCost`).
  **Distinto** do card "Próximo Personagem" da seção 6 (liberação de slot por moeda escalando
  100/200/400...) — esse mecanismo continua intocado, sem relação com odds de raridade.
- Validação de recibo IAP (cash): **mock** por enquanto — a function aceita qualquer
  `paymentReceipt` não vazio. `// TODO` explícito no código (`purchaseCase.ts`) marcando onde a
  validação real (App Store Server API / Google Play Developer API) deve entrar antes de
  produção.
- Personagem concedido fica persistido de verdade (Firestore), mas ainda **não é jogável** — as
  telas que listam/selecionam personagem (`02_SelectCharacter`, troca rápida do menu) continuam
  só lendo os assets pré-autorados do projeto, não o roster do Firestore. Ver a lista de telas
  pendentes em `ARQUITETURA.md`.
- **Unlocks progressivos de skill/arma/pet (2026-07-25, corrigido no mesmo dia)** — ao abrir o
  detalhe do personagem recém-concedido (dentro do mesmo overlay de `02_SelectCharacter`, antes
  de liberar Selecionar/Fechar), a raridade dele concede N sorteios sequenciais de skill/arma/pet:
  Normal 1, Uncommon 2, Rare 3, Legendary 4, Immortal 5 (`CharacterUnlockEngine.
  UnlockCountForRarity`). Cada sorteio escolhe uma FAMÍLIA usando os odds REAIS já existentes por
  item (`SkillData.odds`/`WeaponData.dropOdds`/`PetData.odds`, mesmos do level-up de combate) —
  sem escolha do jogador, tudo automático. **Tier concedido é sempre por POSSE, nunca por posição
  do unlock** (correção de um bug real da 1ª versão, que dava T2/T3 de item nunca possuído em T1):
  `1 + maior tier que o personagem já possui daquela família` (considerando os unlocks já
  aplicados na mesma sequência); família já no tier máximo não desperdiça o unlock, sorteia outra
  — ver `CharacterUnlockEngine.cs`. `PlayerProfile.caseUnlocksResolved` marca a sequência como
  concluída (nunca reconcede). Ver `CharacterUnlockRevealPanel.cs` pro card de reveal.
- **Refresh dos unlocks (2026-07-25, mesmo dia)** — cada unlock revelado pode ser resorteado até
  2 vezes antes de aceitar ("Continuar"), custo FIXO de 15 diamantes por uso (não escala/dobra —
  diferente do "Novo Sorteio" do level-up de combate, seção 11, que dobra a cada uso; são dois
  sistemas econômicos deliberadamente separados). Diamante e limite de 2 refreshes são validados/
  decididos 100% pela Cloud Function `rerollUnlock` (nunca pelo cliente — mesma regra
  inegociável de `ARQUITETURA.md` "Moeda premium"), que resorteia com as MESMAS regras do sorteio
  original (odds real por família + tier por posse). O resultado de um unlock só é aplicado ao
  personagem quando aceito com "Continuar" — enquanto isso é só um rascunho, substituível pelo
  refresh sem tocar no personagem de verdade. Botão de refresh some quando os 2 usos acabam, ou
  fica desabilitado (com aviso de saldo insuficiente) sem travar o fluxo. Ver
  `UnlockRerollService.cs`/`functions/src/rerollUnlock.ts`.

## Itens em aberto
- ~~Definir se skip/1.5x/passe é debitado em diamante ou pago direto em cash~~ — resolvido: cash
  direto (IAP), não gasta diamante. O que ERA fake (o estado da compra em si, não o valor gasto)
  passou a persistir de verdade em 2026-07-21 — ver seção Status.
- Confirmar arte de ícones de raridade (caixa/baú por cor, seção 8).
- ~~Personagens (seção 5/aba Personagens da Loja) continuam sem persistência real...~~ — resolvido
  2026-07-23, ver seção 13 acima. Falta só a migração de `02_SelectCharacter` pra tornar o
  personagem concedido jogável (fora do escopo desta rodada, ver `ARQUITETURA.md`).
