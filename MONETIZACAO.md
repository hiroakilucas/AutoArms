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

## 6. Moeda (soft currency) — "Chibers Aleatório" (compra real, 2026-07-26; renomeado de "Próximo
Personagem" em 2026-07-27, junto com os 3 cards cash da seção 5: "Personagem Raro/Legendary/
Imortal" → "Chibers Raro/Lendário/Imortal")
Substitui "Case Geral" (diamante, seção 13 antiga) — mesmo sorteio ponderado de raridade (seção
7 abaixo), mesma regra de não-repetição da seção 5, mas pago em Coins com preço escalando por um
contador PERSISTIDO por jogador (`users/{uid}.nextCharacterPurchaseCount`, Cloud Function
`purchaseNextCharacter`) em vez de preço fixo por pacote. Tabela FINAL (substitui a antiga
100/200/400/600/800/1000/+400) — espelhada no cliente em `NextCharacterService.PriceTable`
(extraída de `ShopController` em 2026-07-27 pra ser reaproveitada pelo indicador da seção 14
abaixo):
| Compra | Custo (moeda) | | Compra | Custo (moeda) |
|---|---|---|---|---|
| 1ª | 25 | | 7ª | 1200 |
| 2ª | 50 | | 8ª | 1400 |
| 3ª | 100 | | 9ª | 1600 |
| 4ª | 200 | | 10ª | 1800 |
| 5ª | 400 | | 11ª | 2000 |
| 6ª | 800 | | 12ª | 2200 |

13ª em diante: +200 a cada compra (2400, 2600, 2800...).

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

**Cor de tier de skill/arma/pet alinhada a esta mesma progressão desde 2026-07-27** —
`UITheme.TierColor(int tier)` (T1 cinza/`rarityNormal`, T2 verde/`rarityUncommon`, T3 azul/
`rarityRare`) substituiu o antigo bronze/prata/ouro em todos os locais que mostram essa borda:
Main Menu/Chibers/02_SelectCharacter (`CharacterPanel`), botão Arsenal (`ArsenalSlotUI`), tela de
escolha de skill no level-up (`CombatResultPanel.MakeLevelUpCard`, borda nova — não existia
nenhuma antes) e o reveal de case-opening/Renascimento (`CharacterUnlockRevealPanel`). T4/T5
(Legendary/Imortal, laranja/vermelho) continuam reservados pra quando essas evoluções existirem
de verdade — `tierBronze`/`tierSilver`/`tierGold` (`UITheme`) não foram removidos, só pararam de
ser lidos pra essa borda (ainda usados pela cor de tag de `WeaponType.Heavy` no popup de detalhe
de arma, ver `CharacterPanel.TypeColor`).

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

## 12. Resetar Personagem — REMOVIDO (2026-07-27), substituído por Renascimento

Implementado 2026-07-21, removido por completo 2026-07-27 — o "Renascimento" (Reset Nível 10+,
ver `CharacterPanel`/CLAUDE.md) tornou esta feature obsoleta: cobre o mesmo papel (reset pro
Level 1 + crédito de moeda) em todos os aspectos, com a vantagem de também conceder skills/armas/
pets pela raridade do personagem. `CharacterResetSettings` (ScriptableObject/asset) foi removido
junto. Ainda não confundir com "Reset de Build" (`ROADMAP_FUTURO.md` Fase 4, continua em aberto —
mantém o nível atual e custa diamante, diferente do Renascimento).

## 13. Case opening (compra de personagens, cash + moeda) — implementado 2026-07-23

Reverte a nota "NÃO FAZER AINDA" que existia aqui desde 2026-07-21 — o usuário pediu a
implementação real da persistência de personagens comprados, com uma tela de "abertura de case"
estilo CS:GO (roleta horizontal desacelerando até parar no personagem sorteado, sorteado no
servidor). Ver `ARQUITETURA.md` ("Modelo de roster multi-personagem") pro desenho completo.

- As 3 raridades cash da seção 5 (Raro/Legendary/Imortal) agora compram de verdade — sorteio
  server-side (Cloud Function `purchaseCase`), sem repetição (exclui personagens já possuídos),
  concede um documento novo em `users/{uid}/characters`. Limite de compras (10/3/1) continua **por
  jogador** (`users/{uid}/casePurchases/{packageId}`), não um estoque global.
- **"Case Geral" (4º pacote, moeda/diamante, `case_moeda_geral`) foi REMOVIDO em 2026-07-26** —
  substituído por "Chibers Aleatório" (seção 6, nome atual — era "Próximo Personagem"), que
  passou a fazer o MESMO sorteio ponderado
  entre as 5 raridades (`rollWeightedPool`/`DEFAULT_TIER_WEIGHTS`, extraído pra
  `functions/src/caseRoll.ts` e reaproveitado por `purchaseCase.ts`/`purchaseNextCharacter.ts`
  sem duplicar), só que pago em Coins com preço por contador-do-jogador em vez de diamante a
  preço fixo por pacote. O doc `casePackages/case_moeda_geral` ficou órfão no Firestore
  (inofensivo); removido de `functions/src/scripts/seedCasePackages.ts`.
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

## 14. Indicador de "compra disponível" (bolinha vermelha) — implementado 2026-07-27

Puramente visual/client-side (sem Cloud Function nova) — aparece simultaneamente em 3 lugares
sempre que `PlayerEconomyState.Coins >= NextCharacterService.NextPurchaseCost(NextCharacterPurchaseCount + 1)`
(mesmo contador/tabela da seção 6, `NextCharacterService.CanAffordNextPurchase()`, extraído de
`ShopController` pra ser lido também pelo Main Menu): botão "LOJA" do Main Menu
(`MainMenuController.BuildAvailableBadge`), aba PERSONAGENS da Loja e o próprio card "Chibers
Aleatório" (`ShopController.BuildAvailableBadge`/`_personagensTabBadge`, ambos reavaliados a cada
`RebuildGrid()`). Reativo por reavaliação (recalculado toda vez que `PlayerEconomyState.Coins`
muda dentro de cada controller), não por um evento único de "sumir ao comprar" — some sozinho se
o saldo cair abaixo do preço, não só quando a compra acontece. Lê sempre o mesmo canal estático
já populado por leituras reais do Firestore (`WalletService`/`PlayerEconomyState`), nunca um
saldo recalculado à parte — `MainMenuController.RefreshEconomyOnMenuLoad` passou a carregar
também `NextCharacterPurchaseCount` (antes só a Loja carregava isso).

**Atualização (2026-07-27, mesmo dia — resgates de diamante, seção 15 abaixo)**: o botão "LOJA"
passou a agregar (OR) este indicador com `DailyRewardsService.AnyClaimAvailable` — significa
"tem algo pra ver/pegar na Loja" de forma geral, não só o card específico desta seção. A aba
PERSONAGENS e o card "Chibers Aleatório" continuam isolados (só este indicador); a aba DIAMANTES
ganhou o PRÓPRIO indicador equivalente, isolado ao dela (ver seção 15) — o padrão/mecanismo é o
mesmo, reaproveitado em vez de duplicado, mas cada aba mostra só a própria condição.

## 15. Resgates gratuitos de diamante — Diário/Semanal/Mensal (aba Diamantes) — implementado 2026-07-27

Pedido do usuário — 3 cards no topo da aba Diamantes, antes dos 8 pacotes pagos.

| Tipo | Recompensa | Libera |
|---|---|---|
| Diário | 5 diamantes | Toda meia-noite (00:00) |
| Semanal | 30 diamantes | Toda segunda-feira (00:00) |
| Mensal | 100 diamantes | Todo dia 1º do mês (00:00) |

**Fuso horário ÚNICO/GLOBAL: America/Sao_Paulo (Horário de Brasília)**, independente de onde o
jogador está fisicamente. Calculado no servidor via `Intl.DateTimeFormat` com o timeZone IANA
(não um offset hardcoded) — Brasil não observa horário de verão desde 2019, mas isso continua
correto de graça se essa política mudar de novo.

**100% server-authoritative** (mesma regra inegociável de "moeda premium", ver ARQUITETURA.md) —
3 Cloud Functions (`claimDailyDiamonds`/`claimWeeklyDiamonds`/`claimMonthlyDiamonds`,
`functions/src/dailyDiamondRewards.ts`, núcleo compartilhado `claimReward` parametrizado por
tipo, mesmo espírito de `rerollShared.ts`) validam server-side, contra o timestamp do PRÓPRIO
servidor de Cloud Functions (não precisa do truque de round-trip via Firestore que o client usa —
a function já roda no servidor), se o jogador já resgatou dentro do período atual antes de
creditar. Nunca credita diamante direto pelo cliente.

**Estado de período em documento separado** — `users/{uid}/rewardsState/diamonds`
(`dailyLastPeriod`/`weeklyLastPeriod`/`monthlyLastPeriod`), com regra própria no
`firestore.rules` (`allow read` do dono, `allow write: if false`, mesmo padrão de
`casePurchases/{packageId}`). Deliberadamente SEPARADO do `users/{uid}` principal — aquele doc
aceita `allow read, write` irrestrito do dono hoje (TODO de segurança pré-existente pra
coins/diamonds/nextCharacterPurchaseCount, ver `WalletService.cs`); se os campos de período
morassem lá, um cliente malicioso poderia escrevê-los direto pra uma data antiga e resgatar de
novo no mesmo dia/semana/mês, já que a Cloud Function só valida contra o que estiver GRAVADO no
documento (protegê-la sozinha não bastaria se o dado que ela consulta pudesse ser forjado).

**Cliente** (`DailyRewardsService.cs`): `ClaimAsync(type)` chama a Cloud Function certa e aplica
o saldo já persistido (nunca `WalletService.AddDiamondsAsync`, que é o caminho client-writable de
outros fluxos). `RefreshStatusAsync(uid)` sincroniza `PlayerEconomyState.*DiamondsAvailable`/
`*NextResetUtc` a partir do Firestore — reaproveita o MESMO truque de "hora do servidor sem Cloud
Function" que `EnergyService` já usava (campo descartável com `FieldValue.ServerTimestamp` +
leitura forçando `Source.Server`), extraído pra `FirestoreService.ReadServerNowAsync`
(generalizado por documento/campo, 2026-07-27) pra não duplicar a lógica de retry entre os dois.
Período/próximo reset calculados client-side com offset FIXO de UTC-3 (documentado no código:
Brasil sem DST hoje; `TimeZoneInfo` teria IDs diferentes entre plataformas pro mesmo fuso IANA,
problema conhecido do Unity/mobile) — só pra decidir O QUE MOSTRAR (botão habilitado/contagem
regressiva); a decisão de crédito de verdade sempre revalida contra o relógio real do servidor de
Cloud Functions, então uma eventual divergência entre os dois cálculos nunca duplicaria uma
recompensa, só mostraria o botão certo/errado por alguns instantes até a próxima sincronização.

**UI**: botão "RESGATAR" (`ShopCardUI` ganhou um `buyLabel` customizável — era sempre "COMPRAR"
hardcoded); disponível = ativo; já resgatado = desabilitado + contagem regressiva VIVA
(`CountdownLabel`, mesmo componente do timer de energia do Main Menu — `StatusText` exposto em
`ShopCardUI` só pra permitir anexar o componente por fora) que recalcula a partir do timestamp de
servidor (nunca do relógio do device) e se auto-corrige (re-sync + `RebuildGrid`) assim que chega
em zero, mesmo princípio de `PlayerEconomyState.EnergyCountdownAtZero`.

**Indicador de bolinha vermelha**: reaproveita o padrão da seção 14 em vez de duplicar —
`DailyRewardsService.AnyClaimAvailable` (OR dos 3 tipos) aparece na aba DIAMANTES e em cada botão
individual disponível; agregado (OR) com `NextCharacterService.CanAffordNextPurchase()` no botão
"LOJA" do Main Menu.

## Itens em aberto
- ~~Definir se skip/1.5x/passe é debitado em diamante ou pago direto em cash~~ — resolvido: cash
  direto (IAP), não gasta diamante. O que ERA fake (o estado da compra em si, não o valor gasto)
  passou a persistir de verdade em 2026-07-21 — ver seção Status.
- Confirmar arte de ícones de raridade (caixa/baú por cor, seção 8).
- ~~Personagens (seção 5/aba Personagens da Loja) continuam sem persistência real...~~ — resolvido
  2026-07-23, ver seção 13 acima. Falta só a migração de `02_SelectCharacter` pra tornar o
  personagem concedido jogável (fora do escopo desta rodada, ver `ARQUITETURA.md`).
