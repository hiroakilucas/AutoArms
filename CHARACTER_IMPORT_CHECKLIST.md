# Checklist — Importação de Personagens Novos

Ler antes de importar/processar qualquer personagem novo (pacote CraftPix). Ferramentas em
`Assets/Editor/FemaleCharacterImportGenerator.cs` (menu `Tools > AutoArms`).

## Fluxo básico

1. Importar só a pasta `Vector Parts` (ou `PNG/Vector Parts`, ou já o padrão `Graphics/`+`Prefab/`
   de pacotes que vêm pré-organizados) do personagem em `Assets/Personagens/<Nome>/`.
2. Adicionar 1 frame solto com "idle" no nome (fonte do `previewIcon`).
3. `Tools > AutoArms > Import Female Character` — idempotente, processa qualquer pasta pronta
   ainda sem `PlayerProfile`. Cria prefab jogável (5 componentes de combate + wiring de bones),
   Animator Controller completo (parâmetros + Block/Catch Weapon/Slashing Dagger + todas as
   transições, replicadas campo a campo do controller da Assassin Guy) e o `PlayerProfile`
   (`isUnlockedForSelection = false`, `isPlayable = false` por padrão — ligar manualmente no
   Inspector quando for liberar).
4. **Não precisa mais rodar separado (2026-07-11)** — o passo 3 (`Import Female Character`) já
   retargeta o "Slashing Dagger" automaticamente pra cada personagem assim que o Animator
   Controller é montado (`RetargetSlashingDaggerNow`, chamado de dentro de `ProcessCharacter`).
   `Tools > AutoArms > Retarget Slashing Dagger For All Characters` continua existindo só pra
   **reprocessar personagens importados ANTES deste ajuste** (idempotente, não quebra quem já
   está retargetado) — inclui `Magician_Girl_1/2`, `Medusa_2`, `Winter_Witch_1/2` (importados no
   dia anterior a este ajuste, ficaram com o clipe cru/sem retarget até rodar esse menu uma vez).
   Confere no Console: `X/Y bones resolvidos (Z%)` por personagem — abaixo de 70% = revisar
   manualmente (provável estrutura de rig diferente demais pra reaproveitar).

## Problemas reais já encontrados (não são hipotéticos — aconteceram)

### 1. Numeração interna de bone não é portável entre pacotes
`bone_000`, `bone_001`... são atribuídos pelo Spriter por projeto, não é um índice compartilhado.
Confirmado: o `bone_006` da Assassin Guy/Medieval Warrior é a raiz do braço-espada; na Amazon
Warrior o mesmo número é outra coisa; na Archer, `bone_006` é o osso pai da **perna esquerda**.
Aplicar uma animação de outro personagem sem checar isso arranca o membro errado do lugar
(sintoma real: "perde uma perna e o braço" ao reaproveitar Slashing da Assassin Guy na Archer).

**Mitigação já automatizada**: `PickBestSlashDonor` (Slashing por apelido) mede compatibilidade
real de caminho antes de aplicar; `Retarget Slashing Dagger For All Characters` auto-detecta o
pai de "Left Leg"/"Right Leg" no rig alvo e remapeia se for diferente do da Assassin Guy.

### 2. Path bater não garante que o valor absoluto faça sentido no corpo alvo
Mesmo quando o caminho de curva (`bone_006/bone_007/Head`) resolve certinho pros dois lados, o
clipe guarda valores ABSOLUTOS de posição por keyframe — se foram calibrados pro corpo da
Assassin Guy, aplicar sem ajuste desloca a cabeça/parte pra um lugar sem sentido no corpo de outra
proporção (sintoma real: "a cabeça fica descolada" mesmo com o Animation Window sem nenhum
"Missing!"). **Corrigido via `RetargetPositionCurves`**: soma, em cada keyframe, a diferença entre
a pose de repouso (bind pose, lida direto do `.prefab`) do personagem alvo e da fonte — reancora a
curva inteira sem precisar de ajuste visual manual. Só cobre POSIÇÃO, não rotação nem escala —
se sobrar estranheza depois do retarget automático, pode ser um desses dois.

### 3. "Missing!" amarelo no Animation Window = path não resolve mesmo
Sinal direto e confiável de que aquele bone não existe (ou está em outro lugar) na hierarquia do
personagem selecionado — sempre que aparecer, é a mesma classe de problema do item 1, não do
item 2. `ClipCompatibilityFraction`/`CheckClipCompatibility` já automatizam essa checagem via
`Transform.Find`, sem precisar abrir o Editor.

### 4. Alguns personagens não têm NENHUMA animação equivalente a "Slashing"
Ex.: Archer (arqueira pura) — só tem "Shooting" (puxar/soltar flecha), que não serve como golpe de
cima pra baixo. Resolvido caso a caso (ver `RepairArcherShootingSplit`): duplicar outra animação
NATIVA do próprio personagem que tenha o movimento certo (no caso da Archer, o "Throwing" dela
mesma — compatibilidade de rig garantida por ser o próprio corpo) em vez de forçar um doador
externo. Não existe solução automática genérica pra esse caso — precisa de decisão visual.

### 5. Apelido de estado de ataque: Rename vs Clone importam
Tabela `StateAliases` em `FemaleCharacterImportGenerator.cs`. Dois modos:
- **Rename** (ex. "Attacking" → "Slashing"): o apelido já É o golpe genérico, só nome diferente —
  renomear em paz.
- **Clone** (ex. "Shooting" → "Slashing"): o apelido tem propósito PRÓPRIO que não pode ser
  perdido (uso futuro, ou o movimento não serve mesmo) — cria um estado novo, mantém o original
  intacto sem nenhuma transição.
Adicionar aqui quando aparecer um pacote com nomenclatura ainda não vista.

### 6. Colisão de GUID ao importar variantes numeradas do mesmo personagem base
Ex.: `Magician_Girl_1/2/3`, `Winter_Witch_1/2/3`, `Medusa_2/3` — pacotes CraftPix da MESMA base,
só variando roupa/cor. Se a pasta "Vector Parts" de uma variante for copiada por fora do Unity
(Explorer) a partir de outra já importada — em vez de cada `.unitypackage` sendo importado
separadamente pelo Editor —, os `.meta` (que carregam o GUID) vêm duplicados e o Unity passa a
tratar variantes diferentes como o MESMO asset por baixo dos panos; edições numa vazam pra outra.
**Sintoma real**: 6 personagens (Archer_2, Magician_Girl_1/2, Medusa_2, Winter_Witch_1/2) tiveram
que ser apagados por completo depois de ficarem corrompidos assim. Prevenção: sempre importar cada
`.unitypackage` direto pelo Unity (`Assets > Import Package`), nunca copiar pastas já importadas
por fora pra criar a próxima variante.

**Confirmado de novo (2026-07-11, Magician_Girl_2)**: o GUID duplicado não é um acidente de quem
importa — os 3 `.unitypackage` (`Magician_Girl_1/2/3`) vêm com os **mesmos GUIDs internos
craftados pela CraftPix** (mesmo template clonado, só a arte muda), confirmado extraindo o
`.unitypackage` (é só um `tar.gz` — cada pasta é um GUID, contém `asset`/`asset.meta`/`pathname`) e
comparando: o `Body.png` do pacote da variante 2 tem o **mesmo** guid que o `Magician_Girl_3` já
processado no projeto. Ou seja, mesmo importando cada `.unitypackage` direto pelo Unity (não por
fora), a colisão acontece se duas variantes forem importadas na mesma sessão/projeto — o problema
não é só "não copiar por fora", é o pacote em si.
**Fix aplicado**: extrair o `.unitypackage`, copiar só os arquivos de `Assets/Vector Parts/*` que
o projeto de fato usa (as PNGs de corpo + `Animations.scml` — **sem** `Sword.png`/`.prefab`/
`.controller` do pacote, que são descartados e regerados do zero pelo
`Tools > AutoArms > Import Female Character`, mesmo padrão já usado pelas outras pastas
`Vector Parts/` do projeto), gerar um **GUID novo aleatório por arquivo** (regravando só a linha
`guid:` do `.meta`, sem tocar no resto) antes de colocar em
`Assets/Personagens/<Nome>/Vector Parts/`. Como o `.prefab`/`.controller` do pacote (os únicos
arquivos que referenciavam as PNGs por GUID) são descartados, não sobra nenhuma referência cruzada
pra reescrever — só regravar o GUID próprio de cada `.meta` já resolve, sem precisar editar YAML
de prefab/controller. `Assets/Spriter2UnityDX/*` e `Assets/Scenes` do pacote são ignorados (já
existem no projeto, mesmos GUIDs, não precisam ser reimportados). **A pasta `Vector Parts` em si
também tem GUID próprio** (o `.meta` da pasta) — regenerar junto, senão duas variantes da mesma
coleção (ex. `Samurai_2`/`_3` abaixo) colidem no GUID da PASTA mesmo com os arquivos de dentro já
corrigidos.

**Confirmado de novo (2026-07-13, `Samurai_2`/`Samurai_3`/`Vampire_Hunter_1`)**: mesmo diagnóstico
de sempre, mas com um detalhe novo — **nem toda variante numerada colide**. Comparando os GUIDs
extraídos de cada `.unitypackage` contra tudo que já existe no projeto (não só contra a variante
irmã, contra `Assets/` inteiro):
- `Samurai_1`/`Samurai_2`/`Samurai_3`: os 3 pacotes compartilham os mesmos 13 GUIDs internos entre
  si (mesmo template CraftPix). Nenhum dos 3 tinha sido importado ainda, então não colidiam com o
  projeto — mas colidiriam **entre si** assim que 2 ou mais fossem importados. `Samurai_1` foi
  deixado para import normal (só ele "ganha" os GUIDs originais); `Samurai_2`/`Samurai_3` tiveram
  GUID novo gerado por arquivo antes de entrar no projeto.
- `Fallen_Angels_3`: comparado contra o projeto inteiro (incluindo `Fallen_Angels_1`, já
  processado) — **zero colisão**. Nem toda variante numerada da mesma coleção reusa GUID; não dá
  pra assumir automaticamente, tem que comparar caso a caso antes de importar.
- `Vampire_Hunter_1`: colisão confirmada e grave — os 17 GUIDs do pacote são **idênticos** aos de
  `Vampire_Hunter_3`, já totalmente processado no projeto (com `PlayerProfile` próprio apontando
  pro prefab dele). Importar como veio do pacote faria o Unity tratar os arquivos como o mesmo
  asset de `Vampire_Hunter_3`, sobrescrevendo/corrompendo o personagem já pronto — mesmo fix
  aplicado (GUID novo por arquivo + pasta).

Fluxo usado pra achar as colisões antes de tocar em qualquer arquivo: extrair o `.unitypackage`
(é `tar.gz`), montar um mapa `guid → pathname` a partir dos arquivos `pathname` de cada subpasta, e
comparar cada guid contra `grep -r "guid: <guid>" Assets/` do projeto inteiro (não só a pasta do
personagem correspondente) — só assim `Fallen_Angels_3` pôde ser confirmado como seguro em vez de
assumido como mais um caso de colisão só por ser variante numerada.

### 7. `Sword.png` do pacote CraftPix não deve ser importado (decisão do usuário)
O corpo do personagem (`Vector Parts`/`Graphics`) não deve carregar nenhum sprite de espada
próprio — só as armas do sistema `WeaponData`/`WeaponHandler` (equipadas em runtime, ver
CLAUDE.md → **Combat Systems**) devem aparecer na mão. Todo pacote CraftPix já vem com um
`Sword.png` solto (bone "Sword" da rig, sprite estático "embainhada"/parada) que não tem relação
nenhuma com o sistema de armas do jogo — se deixado, fica um sprite de espada permanente e sem uso
por baixo de qualquer arma equipada de verdade.

**Confirmado seguro remover antes do import** (`Assets/Spriter2UnityDX/Editor/PrefabBuilder.cs`,
`GetSpriteAtPath`, linha ~184): quando o arquivo não existe, o método só faz
`Debug.LogErrorFormat("Error: No Sprite was found at {0}", path)` e retorna `null` — **não** seta a
flag `success` (isso só acontece no branch de textura com tipo/pivot errado, arquivo **presente**
mas mal configurado). Ou seja, a ausência de `Sword.png` não aborta `PrefabBuilder.Build` nem
impede o `.prefab`/`.controller` de serem gerados — o bone "Sword" só fica sem sprite (invisível),
mesmo padrão já em produção em `Vampire_Hunter_3` (também sem `Sword.png`, personagem já jogável).

**Como aplicar**: apagar `Sword.png` + `Sword.png.meta` de dentro de `Vector Parts/` **antes** de
rodar `Tools > AutoArms > Import Female Character` (ou antes do 1º import pelo Unity, se o pacote
ainda nem gerou prefab). Se o `.prefab`/`.controller` **já foram gerados** com `Sword.png` presente
(ex.: pacote importado normalmente antes dessa decisão, caso de `Samurai_1`), apagar também esse
`.prefab`/`.controller` + o `.meta` do próprio `Animations.scml` — sem o `.meta`, o Unity trata o
`.scml` como asset novo no próximo refresh e o Spriter2UnityDX regenera os dois do zero, já sem o
bone morto (evita ficar com uma referência de sprite quebrada presa num prefab já publicado).

**Aplicado em 2026-07-13**: `Citizen_1`, `Desert_Nomad_3`, `Persian_and_Arab_Warriors_3`, `Pirate`,
`Priest_3`, `Spiritual_Monk_1`, `Technomage_2`, `Thief` (todos ainda crus, só arquivo+`.meta`
removidos) e `Samurai_1` (prefab/controller já gerados — apagados junto do `.meta` do
`Animations.scml` pra forçar reimport limpo). Não mexido em personagens que já têm `PlayerProfile`
publicado (fora de escopo — exigiria editar `.prefab` já em uso, não só um import pendente).

### 8. `CharacterDatabase.unlockedCharacters` pode acumular referência órfã
Se um `PlayerProfile` for apagado por fora sem tirar da lista, o slot vira `null` no
`List<PlayerProfile>` e quebra `CharacterSelectController.PopulateCharacterGrid()` com
`NullReferenceException` ao abrir `02_SelectCharacter`. O código já ignora entradas `null` (não
trava mais a cena), mas a referência órfã continua na lista até alguém limpar manualmente — dá
uma conferida no `.asset` se apagar um personagem já registrado na database.

## Duas flags do PlayerProfile — não confundir

- `isUnlockedForSelection` — aparece no grid de `02_SelectCharacter` (ou não).
- `isPlayable` — fica clicável/escolhível pra batalhar (ou aparece só travado/cinza).

Um personagem pode estar visível e travado ao mesmo tempo (`isUnlockedForSelection=true`,
`isPlayable=false`) — útil pra mostrar "em breve" sem deixar escolher ainda.

## Menu items disponíveis (`Assets/Editor/FemaleCharacterImportGenerator.cs`)

- `Tools > AutoArms > Import Female Character` — pipeline principal, idempotente.
- `Tools > AutoArms > Retarget Slashing Dagger For All Characters` — corrige perna trocada +
  reancora posição em todo mundo já processado (exceto os 3 originais).
- `Tools > AutoArms > Repair - Split Archer Shooting-Slashing` — específico da Archer (Shooting
  intacto + Slashing = cópia do Throwing dela). Só roda de novo se precisar reprocessar ela.
