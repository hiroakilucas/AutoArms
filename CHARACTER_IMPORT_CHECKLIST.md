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
4. `Tools > AutoArms > Retarget Slashing Dagger For All Characters` — roda depois do passo 3 pra
   TODOS os personagens de uma vez (pula Assassin Guy/Medieval Warrior/Medieval Warrior Girl).
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

### 7. `CharacterDatabase.unlockedCharacters` pode acumular referência órfã
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
