# AutoArms — Roadmap Fases Futuras (4-13)

### Fase 4 — Monetização
- [ ] Sistema de diamantes (moeda premium)
- [ ] Sistema de energia com limite diário de batalhas
- [ ] Compra de energia e personagens com diamante
- [ ] Precificação dos pacotes
- [ ] **Reset de Level Up**: ao subir de nível, o jogador vê as 2 opções de escolha normalmente (`ShowLevelUpChoice`). Um botão "Resetar opções" permite rerolar as opções por um custo em diamantes. Cada reset dobra o custo do próximo: 1º reset = X diamantes, 2º = 2X, 3º = 4X, e assim por diante. O custo base X ainda precisa ser definido com base no balanceamento da economia. O reset regenera novas opções aleatórias seguindo as mesmas regras de peso (60% atributo, 30% skill, 10% arma). O contador de resets zera ao fechar o painel de level up.
- [ ] **Reset de Build**: o jogador pode pagar diamantes para resetar todos os atributos e skills ganhos por level up, voltando aos stats base do nível atual e redistribuindo os pontos manualmente. Custo fixo alto ou progressivo por nível. Permite experimentar builds diferentes sem criar um novo personagem.
- [ ] **Personagens/skins desbloqueáveis por passe ou compra**: além de comprar personagens direto com diamante (já listado acima), passes/pacotes podem liberar novos personagens ou skins de personagem. Diferente de uma skin puramente visual, cada um desses pode conceder **status iniciais a mais** (HP/STR/AGI/SPD acima do roll aleatório padrão de level 1) ou **skills iniciais a mais** (1+ skill já equipada desde o level 1, sem precisar tirar no level-up) — vantagem real de progressão, não só cosmética.
  - **Skill/arma específica com nível (1, 2 ou 3)**: o bônus de skill ou arma inicial não é só "tem ou não tem" — vem num **nível** (1/2/3), onde nível maior = versão mais forte da mesma skill/arma (precisa definir a escala numérica de cada nível, ex: nível 1 = valor atual da skill, nível 2 = +50%, nível 3 = dobro — só um exemplo, não decidido). Tanto a **chance de nascer com skill/arma específica** quanto o **nível sorteado** dependem da **raridade do personagem**, que por sua vez é ligada à dificuldade/forma de obtenção (comprado direto com diamante = raridade mais baixa, chance baixa de bônus e tende a nível 1; só obtido via pacote/passe/evento especial ou conquista difícil = raridade mais alta, chance maior de vir com skill/arma específica e mais chance de nível 2-3). Precisa definir: tabela de raridades (quantos tiers, nomes), probabilidades por raridade, e a escala de poder de cada nível.
- [ ] **Passe Diário A — Diamantes + libera 2x** (30 dias): recompensa diária de diamantes por logar/jogar; durante a vigência do passe, libera o botão de velocidade 2x no `CombatHUD` (hoje sempre disponível pra todo mundo — passa a ser um benefício exclusivo de quem tem o passe ativo, ou currently-free pode virar gratuito só até essa monetização entrar).
- [ ] **Passe Diário B — Diamantes + libera Skip** (30 dias): mesmo formato do Passe A, mas libera o botão **Skip** (`CombatHUD`) em vez do 2x.
- [ ] **Pacote Premium — Diamantes (quantia maior) + libera 2x e Skip juntos** (30 dias): tier acima dos dois passes diários — diamante por dia em quantidade maior que A/B somados, e libera os dois botões (2x e Skip) ao mesmo tempo durante a vigência. Pensar se substitui A+B ou é um upgrade comprável por cima.
- [ ] **Passe Mensal de Arma (Battle Pass)**: progressão por XP de passe, ganho ao completar batalhas (separado do XP de personagem/level já existente). Ao subir de nível no passe, libera **3-4 skins de arma** ao longo do mês — cada skin dá atributos extras na arma equipada (dano/crítico/etc. acima do `WeaponData` base) — sendo a **última (nível mais alto do passe) a mais rara/forte** das recompensas. Precisa definir: curva de XP do passe (independente da curva de XP de personagem), se o passe expira ao fim dos 30 dias levando recompensas não coletadas, e se dá pra comprar níveis do passe direto com diamante (skip de progresso, padrão comum em battle pass).
- [ ] **Pacotes de diamantes (loja) e promoções**: tela de loja com vários pacotes de diamante em quantidades/preços crescentes (ex: pequeno/médio/grande/mega), com bônus de diamante extra proporcionalmente maior nos pacotes mais caros (incentiva compra do pacote maior). Promoções temporárias: desconto por tempo limitado, diamante em dobro na primeira compra, pacote sazonal ligado a evento/torneio. Depende do Sistema de diamantes (ainda não implementado, item acima) e da integração de pagamento real (Google Play Billing / Apple StoreKit / Steam, ver checklist de Segurança/Validação server-side em Fase 8). Precisa definir: quantidades e preços de cada pacote, e se as promoções são manuais (painel admin) ou agendadas por código.
- [ ] **Slot extra de atributo/habilidade/arma pago (caro)**: compra cara (R$100-150) de 1 slot adicional de atributo, habilidade ou arma, liberado quando o personagem sobe de level — oferecer também como benefício de um pacote/passe mensal, como alternativa à compra avulsa única. Precisa definir: preço exato, se é só 1 slot por personagem (limite) ou repetível, e se entra como upgrade do passe mensal já planejado acima ou como pacote separado.
- [ ] **Moeda geral (soft currency) integrada ao HUD**: moeda ganha por jogar normalmente (batalhas, missões diárias etc.), separada do diamante (premium) — usada pra compras mais baratas/cotidianas. Precisa definir onde entra na economia (o que compra) e a fonte de emissão. Nota: "Battle Pass" já está coberto pelo item **Passe Mensal de Arma (Battle Pass)** acima — não duplicado aqui.

### Fase 5 — Endgame & Social
- [ ] Mapa PVE
- [ ] Torneio com premiação
  - [ ] **Definir o que o torneio dá de recompensa além de diamante/cosmético** — ideia em aberto: um **acessório** exclusivo de torneio, que pode conceder uma **skill** (mesmo sistema de skill já existente, ganha de outra forma) ou uma **asa** (novo slot de equipamento cosmético+funcional, ainda não existe no projeto — precisaria de novo bone/anchor no rig e novo campo de bônus, similar ao escudo da skill Shield). Não decidido: se é só pro 1º lugar ou por faixa de colocação, se é permanente ou só durante uma "temporada" de torneio.
  - [ ] **Sistema de elo/liga (tipo League of Legends)**: classificação de habilidade separada do level/XP de personagem, usada pra dividir os jogadores em faixas e formar torneios mais equilibrados/acirrados (cada elo roda seu próprio torneio em paralelo, com sua própria classificação de sexta e sua própria chave de 32).
    - **Promoção/rebaixamento** (definido pelo usuário): quem entra nos **32 da chave** sobe de elo na semana seguinte; quem **não** entra nos 32 desce de elo. Resultado dentro da fase de classificação de sexta é o único critério — partidas casuais fora do torneio não afetam o elo.
    - **Distribuição dos tiers** — ainda não decidida em números, mas a diretriz já está definida: formato **pirâmide**, com **mais jogadores nos elos baixos e menos nos elos altos** (quanto mais alto o elo, mais exclusivo/difícil de manter — cada vitória no corte de 32 sobe só uma fração pequena da base total). **Quantidade de tiers fica pra depois** (decisão adiada pelo usuário) — só a diretriz de pirâmide está fixada por enquanto.
    - **Elo é por personagem, não por conta** (definido pelo usuário): se o jogador tem mais de 1 personagem, só o personagem que efetivamente jogou aquele torneio ganha pontos/sobe ou desce de elo — os outros personagens da mesma conta não são afetados. Implica guardar elo como campo do `PlayerProfile` (igual a `level`/`xpCurrent`), não num lugar único por jogador.
    - [ ] **Marcador de elo por personagem na tela de seleção de personagem**: badge/ícone do elo atual visível em cada `CharacterCard` da grade do `02_SelectCharacter` (e possivelmente também no `CharacterPanel`/HUD da MainMenu, mesmo padrão de onde já aparecem level/stats) — permite ver de cara em qual elo cada personagem da conta está antes de escolher qual levar pro torneio da semana.
  - [ ] **Ciclo semanal do torneio** (corre em paralelo, um por elo):
    - **Segunda a quinta** — inscrição aberta para o torneio da semana.
    - **Sexta** — fase de classificação por pontuação (definido pelo usuário): **15 lutas aleatórias** por inscrito (valor inicial — "aumentaremos conforme a demanda", ou seja, escala pra cima se a base de jogadores crescer), contra adversários sorteados dentro do mesmo elo, **+1 ponto por luta vencida** (derrota não pontua, valor fixo simples por enquanto, sem ponderação por dificuldade do oponente); os **32 melhores em pontuação** entram na chave, e a fase de **mata-mata (eliminação simples) começa no mesmo dia** com a rodada de 32 (32 → 16 vencedores).
    - **Sábado** — mata-mata avança pelas **oitavas de final** (16 → 8 vencedores, terminando o dia com os 8 quartofinalistas definidos).
    - **Domingo** — **quartas de final → semifinal → final**, terminando com o campeão da semana.
- [ ] **Histórico de batalhas** (3 abas, definido pelo usuário — cobre batalhas normais e de torneio juntas):
  - **Aba Ataque** — só as lutas onde o próprio jogador iniciou o combate contra outro personagem. **Limite de 10** — guarda só as 10 mais recentes (rolling, a mais antiga cai quando uma 11ª entra).
  - **Aba Defesa** — só as lutas onde o personagem foi escolhido por **outro** jogador como alvo (mesmo conceito do My Brute original: outros jogadores podem desafiar/atacar seu personagem de forma assíncrona enquanto você está offline — depende de um sistema de matchmaking assíncrono "fora do torneio" que ainda não existe no projeto; hoje só Player2/Medieval Warrior Girl pré-colocada serve de oponente). **Limite de 20** — mesmo esquema rolling do Ataque, mais alto porque é passivo (o jogador não controla quando é escolhido como alvo).
  - **Aba Torneio** — junta as lutas da fase de pontuação de sexta e as lutas de chave (mata-mata) da mesma semana, com uma tag/indicador em cada entrada mostrando de qual fase ela é. **Sem limite fixo de quantidade** — em vez disso, **reseta inteira toda quinta-feira**, antes do torneio da semana seguinte abrir inscrição na sexta (alinhado com o ciclo semanal já definido acima: segunda-quinta inscrição, sexta-domingo competição) — só fica visível o histórico do torneio da semana corrente.
  - Cada entrada reusa o log de batalha já existente (mesma base do `CombatLogFormatter`/`CombatEvent`, e do log persistido planejado pro replay de torneio acima). **Logs ficam armazenados na base de dados** (depende da persistência online da Fase 6, ainda não implementada) — os limites acima (10/20/reset semanal) descrevem o que fica visível/retido por aba, não um cap teórico de quanto a base pode guardar no total.
  - [ ] **Batalhas offline + log + replay**: cada partida do torneio é resolvida de forma assíncrona com o `CombatSimulator` já existente (mesma arquitetura determinística do combate normal — os dois jogadores não precisam estar online ao mesmo tempo). Cada luta gera um log completo de eventos (mesma base do `CombatLogFormatter`/`CombatEvent` já existentes) e fica persistido (depende do banco de dados online da Fase 6, ainda não implementado); quando o **sistema de replay** for implementado (também não existe ainda — feature futura separada), qualquer jogador poderá assistir a qualquer luta do torneio reproduzindo esse log. Reforça o item de Fase 8 "calcular resultado do combate no servidor (anti-cheat)" — torneio com premiação real é o caso de uso mais sensível a isso.
- [ ] Torneios 2v2 e 3v3
- [ ] Sistema de discípulos (recrutar amigos = bônus XP)
- [ ] Guildas
- [ ] **Convite de amigo dá premiação**: jogador que convida um amigo pra instalar/criar conta ganha algum prêmio (diamante, item, etc. — ainda não decidido qual). Diferente do "Sistema de discípulos" acima (bônus de XP por recrutar) — esse é sobre o próprio ato do convite dar recompensa; precisa decidir se os dois sistemas coexistem ou se um substitui o outro.
- [ ] **Presença em redes sociais (Discord, Instagram, etc.)**: criar e divulgar canais oficiais do jogo (servidor de Discord pra comunidade, conta de Instagram). Ainda não decidido o conteúdo de cada canal nem o cronograma de lançamento.

### Fase 6 — Infraestrutura
- [ ] Criar cena 03_SelectWeapons (já referenciada no código)
- [x] Definir banco de dados para salvar personagens (Firebase ou PlayFab) — **Firebase** (Auth +
  Firestore), decidido e implementado em 2026-07-14/15 (plano de contas/save na nuvem, Fatia -1 a
  6). Ver ARQUITETURA.md.
- [x] Integrar persistência de dados online — `LocalSaveService` (save local em JSON, corrige
  progresso que nunca persistia em build) + `FirestoreService`/`CloudSyncService` (sync local↔
  nuvem, "último gravado ganha" por `updatedAtTicks`) + `opponents_index`/`OpponentSearchService`
  (superfície pública pra matchmaking, ver Fase 10 abaixo). Testado ponta a ponta pelo usuário com
  múltiplas contas reais (2026-07-15).
- [ ] **Decidir arquitetura de servidor — por região vs. por temporada/tempo**: ainda não decidido, pensar com calma antes de implementar. Servidor **por região** (ex: Brasil, EUA, Europa) reduz latência e é o padrão pra jogos competitivos/PVP em tempo real — mas esse jogo é turn-based assíncrono (`CombatSimulator` pré-calcula o combate inteiro), então a sensibilidade a latência é bem menor que num jogo de ação ao vivo, o que reduz a urgência de sharding por região. Servidor **por tempo/temporada** (ex: reset periódico de ranking/torneio, ligado ao Battle Pass mensal já planejado acima) é mais sobre ciclo de conteúdo/economia do que sobre infraestrutura física, e os dois não são mutuamente exclusivos (pode ter região E temporada ao mesmo tempo). Definir antes de decidir: se vai ter PVP em tempo real de verdade (justificaria região) ou só matchmaking assíncrono (não justificaria tanto).
- [ ] **Login/criação de conta por múltiplos métodos**: além de conta própria por email, permitir login/criação de conta via Google, Apple e Facebook (OAuth). Depende da escolha de backend de autenticação (Firebase Auth ou PlayFab Auth, já listado em Segurança/Fase 8).
  - [x] Email/senha — `AuthService.SignInAsync`/`SignUpAsync`, `00_Login.unity`/`LoginController` (2026-07-14).
  - [x] Google — `AuthService.SignInWithGoogleAsync` (2026-07-15) — só funciona em build Android/iOS de verdade (plugin lança exceção em Editor/Windows Standalone, mensagem amigável tratada).
  - [ ] Apple — depende de Mac com Xcode (Fatia 7 do plano de contas, ainda bloqueada).
  - [ ] Facebook — não iniciado.

### Fase 7 — Plataformas & Distribuição
- [ ] Instalar módulos Android e iOS no Unity Hub (Android SDK, NDK, OpenJDK)
- [ ] Configurar Player Settings para Android (bundle ID, ícone, splash screen)
- [ ] Configurar Player Settings para iOS (bundle ID, signing, capabilities)
- [ ] Adaptar UI para telas mobile (safe area, resolução, touch input)
- [ ] Testar build Android e resolver erros
- [ ] Testar build iOS e resolver erros (requer Mac com Xcode)
- [ ] Publicar na Google Play Store
- [ ] Publicar na Apple App Store
- [ ] Configurar build para Steam (Windows standalone)
- [ ] Criar página na Steam (Steam Direct — taxa única de $100)
- [ ] Publicar na Steam

#### Empresa/CNPJ (pré-requisito pra monetização real)
**Não é assessoria jurídica/contábil — confirmar cada passo com um contador antes de agir.** Resumo do que normalmente é necessário no Brasil pra vender um jogo com compras (diamantes/pacotes) nas lojas:
- [ ] Verificar se a Google Play Console e a Apple Developer Program (App Store Connect) exigem conta de desenvolvedor **pessoa jurídica** pra repasse de pagamento no Brasil, ou se aceitam pessoa física no começo (cada loja tem regra própria, e muda com o tempo — confirmar direto na documentação oficial de cada uma antes de decidir).
- [ ] Decidir o regime: **MEI** (Microempreendedor Individual — mais simples/barato, mas tem teto de faturamento anual e pode não cobrir CNAE de desenvolvimento de jogos dependendo do enquadramento) vs. **ME/Simples Nacional** (mais flexível, exige contador). Conversar com um contador sobre qual CNAE (atividade) se aplica a "desenvolvimento e publicação de jogos digitais".
- [ ] Abrir o CNPJ (processo geral: registro na Junta Comercial do estado + CNPJ na Receita Federal + inscrição municipal, atualmente em grande parte feito pelo Portal Redesim/gov.br — varia por município).
- [ ] Abrir conta bancária PJ pra receber repasses das lojas (Google/Apple/Steam pagam pra conta de pessoa jurídica quando o cadastro é PJ).
- [ ] Entender a tributação sobre a receita de compras in-app (varia por regime — MEI tem DAS fixo mensal, Simples Nacional tem alíquota por faixa de faturamento) e se há retenção na fonte pelas lojas (Apple/Google costumam reportar mas não retêm imposto brasileiro automaticamente).
- [ ] Verificar se precisa de contrato social (caso vá ter sócio) ou se abre como EI/MEI sozinho.

### Fase 8 — Segurança
- [ ] **Nunca armazenar dados críticos (XP, level, diamantes) só localmente — sempre validar no servidor**: parcialmente coberto — `users/{uid}/characters` já grava na nuvem (não é mais "só local") e as regras do Firestore já validam FAIXA de valor amarrada ao `level` (ver ARQUITETURA.md), mas isso não é validação server-side de verdade (um cliente ainda pode escrever qualquer valor dentro da faixa generosa sem passar por nenhuma Cloud Function) — item permanece aberto até essa validação real existir.
- [ ] Validação server-side de compras (Google Play Billing / Apple StoreKit / Steam)
- [ ] Ofuscar código C# com ferramentas como Obfuscator-ILLVM ou Beebyte
- [ ] Não expor API keys no código — usar variáveis de ambiente ou Unity Cloud
- [ ] Calcular resultado do combate no servidor (anti-cheat)
- [ ] Rate limiting nas chamadas de API para evitar abuso
- [x] Autenticação segura do jogador (Firebase Auth ou PlayFab Auth) — Firebase Auth (email/senha +
  Google; Apple pendente de Mac, ver Fase 6), 2026-07-14/15.
- [ ] SSL/HTTPS em todas as chamadas de rede
- [ ] Validar integridade do save local com hash

### Fase 9 — Áudio
#### Música
- [ ] Música de fundo na tela principal (loop)
- [ ] Música de fundo na tela de seleção de personagem
- [ ] Música de combate (loop durante a luta)
- [ ] Música de vitória (tela de resultado)
- [ ] Música de derrota (tela de resultado)

#### Efeitos Sonoros — Combate
- [ ] Som de golpe normal (hit)
- [ ] Som de golpe crítico (crit — mais impactante)
- [ ] Som de esquiva (dodge/miss)
- [ ] Som de block (escudo bloqueando)
- [ ] Som de combo (hit adicional)
- [ ] Som de desarmar (disarm)
- [ ] Som de arma voando (throw)
- [ ] Som de acerto da arma arremessada
- [ ] Som de soco desarmado (punch)
- [ ] Som de knockback
- [ ] Som de morte/derrota
- [ ] Som de barra de vida baixa (alerta)

#### Efeitos Sonoros — UI
- [ ] Som de botão (click)
- [ ] Som de level up
- [ ] Som de XP ganhando
- [ ] Som de seleção de personagem
- [ ] Som de entrada em cena (queda do céu)
- [ ] Som de impacto no pouso (squash)

#### Implementação Técnica
- [ ] Integrar AudioManager singleton na cena
- [ ] Separar trilha de música (Music) e efeitos (SFX) com volumes independentes
- [ ] Adicionar controles de volume nas Settings
- [ ] Suporte a AudioMixer do Unity para balanceamento
- [ ] Formato recomendado: .ogg para música, .wav para SFX

#### Fontes de Áudio Gratuitas (uso comercial permitido)
- freesound.org — efeitos sonoros variados (verificar licença por arquivo)
- opengameart.org — músicas e SFX para jogos (CC0 e CC-BY)
- pixabay.com/music — músicas livres para uso comercial
- kenney.nl/assets — pacotes de SFX prontos para jogos (CC0, sem atribuição)
- zapsplat.com — SFX profissionais (plano gratuito disponível)

#### Estilo de Áudio sugerido para o AutoArms
- Música: medieval/fantasia com clima de arena — épico mas não pesado
- SFX de combate: impactos sólidos, metálicos para armas, cartoon para eventos especiais (crítico, level up)
- Inspiração: My Brute usava sons cartunizados e exagerados — funcionava bem com o visual 2D

### Fase 10 — Fluxo de Partida & Matchmaking
- [x] Tela de seleção de oponente ao clicar em Play (grid com 6 personagens inimigos) —
  `05_SelectOpponent`/`SelectOpponentController` (2026-07-07). Desde 2026-07-15 (Fatia 6 do plano
  de contas), tenta buscar adversários REAIS primeiro via `OpponentSearchService`/`opponents_index`
  no Firestore — só cai no pool local fixo (`CharacterDatabase.opponentCharacters`) como fallback
  (offline, sem sessão, ou ninguém sincronizado ainda).
- [x] Histórico de confronto entre jogador e oponente selecionado (nº de batalhas e vitórias de
  cada lado) — diferente da aba "Histórico de batalhas" da Fase 5 (log geral de lutas): aqui é um
  recorte cabeça-a-cabeça mostrado antes de escolher o oponente. Cada card mostra
  `"{batalhas} batalhas · {vitórias} vitórias"` (`SelectOpponentController`, `PlayerPrefs` local +
  espelho em `users/{uid}/matchHistory` no Firestore desde a Fatia 6).
- [ ] Tela de Replay (últimas partidas normais, ataques recebidos, último torneio) — cobre o sistema de replay já mencionado na Fase 5 (histórico de torneio) e estende pra partidas normais/defesa também

### Fase 11 — Social & Comunidade
- [ ] Sistema de amigos (adicionar/remover)
- [ ] Chat entre amigos
- [ ] Envio de replay via chat — depende da Tela de Replay (Fase 10)
- [ ] Duelo amistoso 1x1 sem gasto de energia

### Fase 12 — Configurações
- [ ] Tela de configurações: volume, notificações, idioma
- [ ] Qualidade gráfica (avaliar viabilidade)
- [ ] Links para sites oficiais do jogo
- [ ] Mapa de skills (visualização em árvore)
- [ ] Mapa de armas (visualização em árvore)

### Fase 13 — Modo Caminho Infinito
- [ ] Modo PVE infinito: batalhas começam no level 1 e a dificuldade escala a cada vitória — versão detalhada do item "Mapa PVE" já listado na Fase 5

