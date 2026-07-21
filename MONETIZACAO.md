# MONETIZACAO.md

Documento de referência para o sistema de monetização do AutoArms (Fase 4).

## Status
- [x] UI da loja (placeholder) — implementada 2026-07-20 (`06_Loja`/`ShopController.cs`/
  `ShopCardUI.cs`, 5 abas, grid de cards, navegação sem reload, compra fake local em memória).
  Falta: aprovação de layout, arte final dos ícones/cards, ligação com WalletService real.
- [ ] Compra real de diamante (Google Play Billing / Apple StoreKit) — bloqueado até validação server-side (Fase 8)
- [ ] Gasto de diamante (skip, 1.5x, slots, personagens) — pode ser implementado com o WalletService atual (placeholder, mas debita valor já existente, não vende)

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
4. Ligação com WalletService real e, para diamante, gateway de pagamento
   (Fase 8)

## Itens em aberto
- Definir se skip/1.5x/passe é debitado em diamante ou pago direto em cash
  — hoje modelado como cash direto (IAP), não gasta diamante.
- Confirmar arte de ícones de raridade (caixa/baú por cor, seção 8).
