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

## Itens em aberto
- ~~Definir se skip/1.5x/passe é debitado em diamante ou pago direto em cash~~ — resolvido: cash
  direto (IAP), não gasta diamante. O que ERA fake (o estado da compra em si, não o valor gasto)
  passou a persistir de verdade em 2026-07-21 — ver seção Status.
- Confirmar arte de ícones de raridade (caixa/baú por cor, seção 8).
- Personagens (seção 5/aba Personagens da Loja) continuam sem persistência real e sem opção de
  compra com diamante — só R$ (raridades) e moeda (card "Próximo Personagem", também sem persistir
  ainda). Fora do escopo até agora ("NÃO FAZER AINDA", pedido explícito do usuário 2026-07-21).
