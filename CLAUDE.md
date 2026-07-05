# CLAUDE.md

## Arquivos de referência (ler só quando relevante)
- SKILLS_SYSTEM.md — arquitetura de skills, tabela de stats, roadmap de implementação
- SKILLS_PASSIVE.md / SKILLS_ACTIVE.md — documentação individual de cada skill
- PETS.md — antes de alterar sistema de pets
- ROADMAP_FUTURO.md — fases 4-9, monetização, infra, áudio
- CHANGELOG.md — histórico completo de atualizações
- VISION.md — conceito do jogo, inspirações, progressão (raramente necessário)

Foco atual: Fase 3 (Pets) + polimento de combate.

## Regras de documentação — obrigatórias a cada implementação

| Conteúdo | Arquivo |
|---|---|
| Skill nova/alterada, tabela de stats, roadmap de skills | SKILLS_SYSTEM.md + SKILLS_PASSIVE.md / SKILLS_ACTIVE.md |
| Pet: stats, comportamento, PetState/PetCombatController | PETS.md |
| Tasks Fases 4–9 | ROADMAP_FUTURO.md |
| Arquitetura de combate, ScriptableObjects, fórmulas, cenas | CLAUDE.md |
| Toda tarefa concluída — 1 linha `YYYY-MM-DD: resumo` | CHANGELOG.md |

**Nunca no CLAUDE.md:** bugs resolvidos, versões revertidas, detalhe de skill/pet individual, Fases 4–9.

**Antes de encerrar:** arquivo correto atualizado? CHANGELOG.md ganhou 1 linha? counter de Progresso atualizado? conteúdo no lugar errado?

**Tamanho:** se um arquivo ultrapassar ~300 linhas, avisar e sugerir divisão.

## Project Overview

**AutoArms** is a Unity 2D turn-based combat game (auto-battler) where two characters automatically fight each other in sequence, cycling through a loadout of weapons each round.

**Unity version:** 2022.3.61f1

## Development Workflow

This is a Unity project. All development happens inside the Unity Editor. There is no CLI build step — open `AutoArms/` as a project in Unity Hub.

- **Run the game**: Play button in the Unity Editor
- **Build**: File → Build Settings → Build
- **Scenes must be registered** in File → Build Settings for `SceneManager.LoadScene` to work
- **Cache de Compilação**: Ao fazer mudanças significativas em scripts como CombatHUD ou CombatPlayer, se aparecerem bugs fantasmas (ex: OnClick vazio, listeners não registrados), deletar a pasta Library/ do projeto e reabrir no Unity Hub para forçar recompilação limpa. Cache de código antigo pode persistir e causar comportamentos inconsistentes mesmo com o código correto salvo.

## Scene Flow

```
01_MainMenu → 02_SelectCharacter → 01_MainMenu → 04_CombatScenePVP
```

- `01_MainMenu` — Play button navigates to `04_CombatScenePVP`; a character must be selected first
- `02_SelectCharacter` — Grid of characters from `CharacterDatabase`; selection persists via `SelectedProfileHolder`
- `03_SelectWeapons` — **Not yet created.** Referenced in `MainMenuController` (`selectWeapons` field) but absent from the build and the file system — needs to be built.
- `04_CombatScenePVP` — Player1 is spawned at runtime from the selected `PlayerProfile`; Player2 (Medieval Warrior Girl) is pre-placed in the scene

## ScriptableObject Assets

All game data is ScriptableObjects. Cross-scene state flows through a ScriptableObject "channel" instead of DontDestroyOnLoad.

| Asset type | Location | Notes |
|---|---|---|
| `PlayerProfile` | `Assets/ScriptableObjects/PlayerProfiles/` | Assassin Guy, Medieval Warrior, Medieval Warrior Girl. Campos de progresso: `level`, `xpCurrent`, `xpRequired` (calculado por `XpSystem.XpRequired`), `battlesRemaining` (max 6), `str`, `agility`, `maxHealth` (padrão 50) |
| `CharacterDatabase` | `Assets/ScriptableObjects/Databases/` | Only **Assassin Guy** and **Medieval Warrior** are unlocked (selectable); Medieval Warrior Girl is hardcoded as Player2 |
| `SelectedProfileHolder` | `Assets/Resources/` | Cross-scene singleton — read by `CombatSceneLoader` and `MainMenuCharacterPreview` |
| `AttackSettings` | `Assets/Data/Player1Settings.asset`, `Assets/Data/Player2Settings.asset` | Combat timing — see current values below |
| `WeaponLoadout` | `Assets/Data/UI/Weapons/` | array of `WeaponData` slots. **Um asset por personagem** — `Loadout_AssasinGuy.asset`, `Loadout_MedievalWarrior.asset`, `Loadout_MedievalWarriorGirl.asset` (todos começam com as mesmas 4 armas: Satyr1, Golem3, Succubus, Zombie). Antes os 3 `PlayerProfile` apontavam para o mesmo `Loadout10Armas.asset` (ainda existe no projeto, sem uso) — qualquer arma ganha em level-up por um personagem vazava pra todos, já que `CombatResultPanel.ApplyBonus` muta `profile.weaponLoadout.weapons` diretamente. Separar os assets corrigiu isso. |
| `WeaponData` (legados) | `Assets/Data/UI/Weapons/<type>/` | 5 assets originais: Satyr1, Golem3, Succubus, VeryHeavyArmoredFrontierDefender, Zombie. Têm sprites. |
| `WeaponData` (My Brute) | `Assets/Data/Weapons/` | 26 armas organizadas em **3 tiers** (T1/T2/T3): Knife, Sai, Mug, Fan, Keyboard, Leek, Broadsword, Scimitar, Sword, Axe, Halberd, Baton, Lance, Trident, Whip, Bumps, Flail, Morning Star, Mammoth Bone, Hammer, Trombone, Shuriken, Pio Pio, Noodle Bowl, Frying Pan, Racquet. T1 têm sprites (icon + inHandSprite) e stats base. T2/T3 são gerados por `WeaponTierGenerator` (sem sprite — herdam a do tier anterior via `EquipSpecific`) e têm dano multiplicado (T2 ×1.35, T3 ×1.75). **Precisam ser arrastados para `AttackSequencer.allWeapons` no Inspector da cena `04_CombatScenePVP` para entrar no pool de level-up.** |

**AttackSettings — valores atuais (Player1 = Player2 exceto onde indicado):**
| Campo | Valor |
|---|---|
| `idleDuration` | 0.3s (não usado no caminho ativo — só em `PlayerCombat.AttackRoutine`, código morto enquanto `useSimulator=true`) |
| `runSpeed` | 35 |
| `slashingDuration` | 0.4s (era 0.5s — reduzido para deixar o swing menos arrastado; `CombatPlayer` divide em duas metades de 0.2s antes/depois do impacto) |
| `slashingToJumpDelay` | 0.2s (não usado no caminho ativo — só em `ComboStrikeRoutine`, código morto enquanto `useSimulator=true`) |
| `jumpStartDuration` | 0.02s |
| `jumpHeight` | 2 |
| `hurtDuration` | 0.12s (era 0.15s) |
| `dodgeDuration` | 0.2s (era 0.25s) |
| `knockbackDistance` | 0.5 |
| `comboDelay` | 0.1s (era 0.15s — gap entre ações de um combo, usado por `CombatPlayer.ExecuteEvent`) |

> `interTurnDelay` (campo do `AttackSequencer`, valor 0.2s na cena) também é código morto no caminho ativo — só usado em `AttackSequencer.CombatLoop`, que não roda enquanto `CombatSceneLoader.useSimulator=true` (default). O ritmo real entre turnos hoje vem só do tempo de animação (`TurnEnd` → jump-back) mais o `comboDelay` ao final de cada ação.

## Prefabs

| Prefab | Location | Purpose |
|---|---|---|
| Character prefabs | `Assets/Personagens/<name>/` | One per character; contain all combat components |
| `CharacterCard` | `Assets/Prefabs/CharacterCards/` | UI card with Image + Grid Layout Group, used in the character selection grid |

## Architecture

### Combat Runtime (MonoBehaviours)

```
AttackSequencer
  └── loops: player1.AttackRoutine() → delay → player2.AttackRoutine()

PlayerCombat.AttackRoutine()
  ├── StrikeRoutine()          → AttackPosition() → PlayRun → HitRoutine()
  ├── ComboStrikeRoutine()     → delay → reposition if needed → HitRoutine()
  ├── HitRoutine()             → slash trigger → dodge check → knockback+hurt+damage → disarm check
  ├── DodgeLeap()              → PlayJumpStart + JumpTo (fired on defender when dodge triggers)
  ├── AnimationController      (Idle → Run → Slash → JumpStart → Hurt → Dying)
  ├── MovementController       (MoveTo linear, JumpTo parabolic arc)
  └── WeaponHandler            (equips next weapon from PlayerLoadout each round)
```

- `AttackSequencer` — Runs the indefinite turn loop; waits for both `PlayerCombat` references before starting. On combat end calls `OnCombatEnd(winner)`: awards XP (+2 win / +1 loss) via `XpSystem.AddXP`, decrements `player1Profile.battlesRemaining`, then spawns `CombatResultPanel`. Field `player1Profile` set by `CombatSceneLoader`. Fields `skillDatabase` (`Assets/ScriptableObjects/Skills/SkillDatabase.asset`) and `allWeapons` (array of all `WeaponData` assets eligible as level-up rewards) are wired directly on the `AttackSequencer` GameObject in `04_CombatScenePVP` — required for the level-up choice screen to offer skill/weapon options instead of only attributes.
  - `OnCombatEnd` determina vitória via `winner.isPlayer1` (não `winner == player1`) — o campo `AttackSequencer.player1` nunca é atribuído no caminho do simulador (`CombatSceneLoader` só seta `player1Profile`, de propósito, pra `StartWhenReady`/`CombatLoop` legado não rodarem em paralelo com o `CombatPlayer`). Comparar contra `player1` fazia `player1Won` ser sempre `false`, mostrando "DERROTA!" e dando XP de derrota mesmo quando P1 vencia. `isPlayer1` é setado por `CombatSceneLoader` (`player1Combat.isPlayer1 = true`; Player2 fica `false`) e funciona nos dois caminhos (simulador e legado).
- `XpSystem` — Static utility. `XpRequired(level) = level <= 6 ? level + 4 : 2*level - 2` (tabela pedida pelo usuário: 1→2=5, 2→3=6...6→7=10, +1 por nível; 7→8=12, 8→9=14...+2 por nível daí em diante, sem teto — era `(level+1)*(level+2)`, 1→2=6/2→3=12/3→4=20...). `AddXP(profile, amount)` accumulates XP, triggers level-up when threshold reached, applies **+2 maxHealth only** (`ApplyLevelBonus`) — STR/AGI/SPD never increase automatically, they only grow via the level-up choice screen (see below) — calls `EditorUtility.SetDirty` to persist ScriptableObject changes in editor.
- `CombatResultPanel` — Screen-space overlay panel shown 0.8s after combat ends. Shows result title (gold/red), XP gained, animated blue XP bar, current XP / required, level, battles remaining, "Continuar" button (→ `01_MainMenu`). Level-up: bar animates to full, "LEVEL UP!" text pulses with sin-wave scale, bar resets to new level's progress, **then `ShowLevelUpChoice` always opens and blocks "Continuar" until a choice is made** (`continueBtn.interactable = !didLevelUp`). Ensures `EventSystem` exists; uses `GraphicRaycaster` on canvas (added to `CombatHUD.CreateCanvas`) for click detection. Overlay has `raycastTarget = false` so it doesn't block the button.
  - `ShowLevelUpChoice` draws 2 unique `LevelUpOption`s via `DrawOption`: weighted 60% Attribute (+8 HP / +2 STR / +2 AGI / +2 SPD, picked uniformly among the 4), 30% Skill (random `SkillData` from `skillDatabase` not already in `profile.skills`), 10% Weapon (random `WeaponData` from `allWeapons` not already in `profile.weaponLoadout`) — weights for Skill/Weapon drop to 0 if their pool is empty. Logs `[LevelUp] ERRO: SkillDatabase não encontrado ou vazio` if `skillDatabase` is null/empty. Picking a card calls `ApplyBonus` (mutates `profile` directly) then unblocks "Continuar".
    - **Modo teste (`ShowAllOptionsForTesting = true`, const em `CombatResultPanel.cs`)**: em vez de sortear 2 opções, chama `ShowAllOptionsChoice` — grade rolável (`ScrollRect` + `GridLayoutGroup`) com as opções disponíveis: 4 atributos + skills da `SkillDatabase` que (a) ainda não foram escolhidas e (b) têm `icon != null`. Reverter para o comportamento de 2 cartas: trocar essa const para `false`.
    - **Testando skills uma a uma**: todos os ícones foram removidos de `Assets/Data/UI/Skills/`; são re-adicionados um por vez conforme cada skill é testada (`skill_immortality.png` é a primeira). `ShowLevelUpChoice` filtra `availableSkills` por `s.icon != null` — só aparecem no level-up as skills cujo ícone já foi re-adicionado. **Armas estão temporariamente fora** da grade de teste (`foreach (var w in availableWeapons)` comentado em `ShowAllOptionsChoice`) — só atributos + skills disponíveis, por pedido do usuário enquanto o foco é testar skills. Reativar descomentando esse loop quando for testar armas de novo.
    - **Importante**: `SkillData.icon` é vinculado por GUID na geração (`Tools → AutoArms → Generate Skill Assets`, `Assets/Editor/SkillAssetGenerator.cs`). Se você apagar/recriar um PNG do zero (não restaurar o arquivo original), ele recebe um GUID novo e o `icon` salvo no `.asset` antigo fica apontando pra um GUID inexistente (resolve como `null` mesmo com o arquivo presente). O gerador é idempotente e re-resolve `skill.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath)` pra **todas** as 32 skills a cada execução (null se o PNG não existir) — rodar a ferramenta de novo depois de adicionar/remover qualquer ícone mantém os links corretos, sem precisar editar os `.asset` manualmente.
    - **`SkillDef.iconFileName`** (opcional, em `SkillAssetGenerator.cs`): nome do PNG em `Assets/Data/UI/Skills/`, se diferente de `fileName` (que também define o nome do `.asset` gerado — não pode ser trocado sem deixar um asset órfão). `Immortal` usa `fileName = "skill_immortal"` (asset existente) mas `iconFileName = "skill_immortality"`, porque o ícone re-adicionado pelo usuário segue o nome da skill na lista mestre original (`immortality`), não o nome abreviado do asset já implementado (`immortal`) — sem esse campo o gerador procurava `skill_immortal.png` (nunca existiu) e o ícone ficava sempre `null`, mesmo com `skill_immortality.png` presente na pasta e o gerador rodado. Mesmo padrão pra `Armour`: `fileName = "skill_armour"` (asset existente) + `iconFileName = "skill_armor"` (usuário re-adicionou o PNG com o nome sem "u" por engano da primeira vez).
- `CombatSceneLoader` — Agora usa coroutine (`Initialize()`): instancia Player1, aplica `profile.str`/`profile.agility` ao `PlayerCombat`, inicializa health/HUD, aguarda um frame (para `PlayerCombat.Start()` rodar), então executa entrada em cena (`EntryFall`) de ambos em paralelo. Só atribui `attackSequencer.player1` e `attackSequencer.player1Profile` após os dois pousarem. Atribui `player1`'s `PlayerLoadout.loadout = profile.weaponLoadout` e, simetricamente, `player2`'s `PlayerLoadout.loadout = player2Profile.weaponLoadout` (esse segundo não existia antes — não tinha efeito enquanto os profiles compartilhavam o mesmo asset, mas passou a ser necessário depois de cada personagem ganhar seu próprio `WeaponLoadout`; sem isso o `WeaponHUD` do Player2 mostraria o valor hardcoded na cena em vez do loadout real do perfil dela).
- `PlayerCombat` — Owns `AttackRoutine` (legado, dead code enquanto `useSimulator=true`). `SetAttackerLayers()`/`RestoreDefaultLayers()` (públicos) fazem o swap de sorting layers descrito em **Sorting Layers** abaixo — chamados por `CombatPlayer.ExecuteEvent` nos casos `TurnStart`/`TurnEnd` (path ativo), não só por `AttackRoutine`; antes só existiam nesse path legado e nunca rodavam de verdade em combate.
- `WeaponHandler` — Instantiates a weapon prefab onto `handBone`; `WeaponType` determines attack reach. Fires `OnWeaponChanged(WeaponData)` from `EquipData` (new weapon) and `Unequip` (null). `EquipSpecific(WeaponData)` equipa uma arma exata (usado por `CombatPlayer` para casar com o que o simulador sorteou); `EquipRandom()`/`EquipNext()` sorteiam/ciclam pelo loadout. Campo `sortingLayer` default `"Weapons"` (era `"Weapon"`, singular — não existe esse Sorting Layer no projeto; ver Sorting Layers abaixo — armas equipadas renderizavam num layer inexistente e ficavam atrás do corpo, parecendo invisíveis). `EquipShield(WeaponData)`/`RemoveShield()` (slot `currentShield`/bone `offHandBone`, totalmente separado de `current`/`handBone`) equipam/removem o visual permanente da skill Shield — nunca tocam `loadout`/`OnWeaponChanged`, já que o escudo não entra no `WeaponLoadout` nem no ciclo de troca de armas. `offHandBone` deve ser wireado no Inspector pro braço oposto ao `handBone` de cada personagem (ex.: `handBone = Left Arm` → `offHandBone = Right Arm`); `shieldPositionOffset`/`shieldRotationOffset`/`shieldZOffset` (mesmo padrão de `positionOffset`/`rotationOffset`/`zOffset` das armas) precisam ser ajustados visualmente no Editor por personagem.
- `PlayerLoadout` — Tracks `currentIndex` and advances round-robin through `WeaponLoadout.weapons[]`. Fires `OnWeaponsChanged` from `RemoveCurrentWeapon`. Exposes `IReadOnlyList<WeaponData> Weapons` (lazy-initializes `runtimeWeapons` on first access).
- `DamagePopup` — World-space TextMeshPro floating text spawned at the defender's position. Variants: normal (yellow), crit (red "CRIT!\n{damage}"), dodge (blue "ESQUIVA!"), block (gold "BLOCK!"), miss (gray "MISS!"), disarm (orange "DISARM!"), drop (orange "DROP!").
- `WeaponHUD` — Screen-space UI row of weapon icons (100×100px, rotated 45°, tip up) below each player's health bar. One instance per player on `CombatInitializer`. P1 icons left→right; P2 icons right→left (mirrored). Subscribes to `PlayerLoadout.OnWeaponsChanged` (rebuild all icons) and `WeaponHandler.OnWeaponChanged` (update gold highlight). Background: semi-transparent black (alpha 0.35); active weapon: gold (alpha 0.70). Thrown weapons never lose their icon (only `UnequipPermanent` triggers `OnWeaponsChanged`). Created and wired in `CombatSceneLoader.Initialize()` via `CombatHUD.CanvasTransform`.

### 04_CombatScenePVP Hierarchy

| GameObject | Role |
|---|---|
| `Main Camera` | Scene camera |
| `Medieval Warrior Girl` | Player2 — pre-placed, has all combat components configured |
| `TurnManager` | **Dead object** — has a missing (deleted) script, can be removed from the scene |
| `Colosseum arena` | Background/visual — sprite trocado aleatoriamente a cada combate, ver **Background Aleatório** abaixo |
| `CombatInitializer` | Hosts `CombatSceneLoader` — spawns Player1 and wires both combatants at runtime |
| `AttackSequencer` | Hosts `AttackSequencer` script — Player2 (Medieval Warrior Girl) pre-assigned, `interTurnDelay = 0.2`; Player1 starts as `None` and is filled at runtime by `CombatSceneLoader` |

`CombatSceneLoader.Initialize()` wiring sequence (coroutine iniciada em `Start()`):
1. Reads `SelectedProfileHolder.currentProfile`
2. Instantiates Player1 prefab, assigns `AttackSettings` and `WeaponLoadout` from the profile
3. Finds the pre-placed Player2 (`Medieval Warrior Girl`)
4. Sets mutual `defender` / `defenderAnimationController` references on both `PlayerCombat` instances
5. `yield return null` — garante que `PlayerCombat.Start()` rodou em ambos (necessário para `spawnPosition`)
6. Move ambos para `spawnY + 12f`, executa `EntryFall` em paralelo, aguarda via callbacks `bool`
7. Assigns `attackSequencer.player1` e `attackSequencer.player1Profile` — **só após ambos pousarem**, desbloqueando o loop de combate

### Background Aleatório (2026-07-05)

`CombatSceneLoader.RandomizeArenaBackground()` roda no início de `Initialize()` (antes de qualquer outra coisa) — acha o GameObject `Colosseum arena` por nome (`GameObject.Find`, sem precisar de campo `[SerializeField]` wireado no Inspector), sorteia um nome entre os 51 arquivos de `Assets/Resources/BattleGround/` (movida de `Assets/BattleGround` — precisa estar dentro de uma pasta `Resources` pra `Resources.Load` funcionar) e troca o `SpriteRenderer.sprite`.

Lista de nomes é **fixa no código** (`ArenaBackgroundNames`), não um `Resources.LoadAll` — a pasta inteira soma ~500MB (imagens 3840x2160), e `LoadAll` carregaria as 51 pra memória de uma vez só pra usar 1; `Resources.Load(nome)` carrega só a sorteada (~10MB). Adicionar um arquivo novo na pasta exige adicionar o nome na lista também (não é automático). Todos os 51 arquivos verificados com a mesma resolução (3840×2160) e mesmo `spritePixelsToUnits` (100) — trocar entre eles não muda o tamanho aparente em cena.

### Menu Character Preview

Both `MainMenuCharacterPreview` and `CharacterSelectController` instantiate the character prefab for display, then immediately `DestroyImmediate` `PlayerCombat`, `WeaponHandler`, `MovementController`, and `AnimationController` — leaving only the `Animator` in idle state.

`MainMenuCharacterPreview.Start()` also calls `BuildSummaryHUD(profile)` — creates a standalone ScreenSpaceOverlay Canvas (sortingOrder=5) with a semi-transparent strip showing: character name + level, a stats row (HP/STR/AGI/SPD), animated XP bar, and the first 3 skill icons. Container anchors: `(0.30, 0.21)–(0.70, 0.40)` — positioned above the bottom buttons (button tops ≈ 0.188 of 1080p).

The stats row comes from `PlayerProfile.GetEffectiveStats()` — a preview-only calculation (no live `PlayerCombat`/`PlayerState` needed, since those components are destroyed for this display) that mirrors the subset of `CombatSimulator.ApplySkillStats`/`CombatSceneLoader.ApplySkillStats` affecting HP/str/agility/speed (Vitality, Herculean Strength, Feline Agility, Immortal — Bodybuilder não afeta mais STR, ver tabela **Skills que modificam stats** abaixo). Shows just the four flat numbers normally, or `base→effective` in green when a skill changes any of them. **Keep this method in sync** if a stat-affecting skill's formula changes in either of those two places — it's a third, independent copy of the same logic for display purposes. `CharacterPanel.RefreshAll` (Stats tab, abaixo) also calls `GetEffectiveStats()` agora — antes mostrava `p.maxHealth`/`str`/`agility`/`speed` crus, então escolher uma skill que afeta stats (ex: Immortal) nunca refletia ali.

### CharacterPanel (3-tab slide-in)

`Assets/Scripts/UI/CharacterPanel.cs` — opened by clicking the "Personagem" button in the MainMenu (wired to `MainMenuController.OnCharacterButton()`).

- Created lazily on first click; `Setup(holder)` builds all UI then sets GO inactive
- Uses a separate ScreenSpaceOverlay Canvas (sortingOrder=20) parented to the CharacterPanel GO
- Panel RT: `anchorMin=(1, 0.22)`, `anchorMax=(1, 0.92)`, `pivot=(1, 0.5)`, `offsetMin=(-320, 0)`, `offsetMax=(0, 0)` → 320px fixed-width strip on the right edge, bottom at 237px (above the 203px button tops)
- Slide animation: `anchoredPosition.x = 340` (off-screen right) → `0` (visible). EaseOut quad (0.3s open, 0.2s close)
- Three tabs: **Stats** (HP/STR/AGI/SPD grid + XP bar + battle stats), **Skills** (3-column icon grid), **Armas** (weapon list with icon + name/type/damage)
  - Stats agora são 15 linhas verticais (`BuildStatRow`, label dourado à esquerda + valor à direita, empilhadas pelo `VerticalLayoutGroup`) em vez do grid horizontal antigo de colunas: HP, STR, AGI, SPD, INIT, CRIT CHANCE, CRIT DMG, ACCURACY, EVASION, REVERSAL, COUNTER, COMBO, ARMOR, BLOCK, REVERSAL AFTER BLOCK (`ACCURACY` fica acima de `EVASION` na ordem de construção — são os opostos um do outro, ver **Dodge** em Combat Systems; `BLOCK`/`REVERSAL AFTER BLOCK` ficam depois de `ARMOR`, alimentadas pela Counter Attack). Usa `SetStatValue`/`SetStatValuePercent` (mesmo padrão "base→efetivo" do `MainMenuCharacterPreview` acima): mostra só o número quando igual ao base, ou `base→<color verde>efetivo</color>` (fonte menor, 13 em vez de 16) quando uma skill o altera. `CRIT DMG` e `COMBO` não têm campo base no profile (puramente derivados de skill, ex: Reconnaissance e Fists of Fury) — chamados com base `0f` fixo; `ARMOR`/`ACCURACY`/`BLOCK`/`REVERSAL AFTER BLOCK` usam `p.armor`/`p.accuracy`/`p.blockBonus`/`p.reversalAfterBlock` como base (campos reais do profile, default 0). `RefreshAll()` roda a cada `Open()`, então reabrir o painel depois de escolher uma skill no level-up já mostra os valores atualizados. Content da aba Stats agora tem `ContentSizeFitter` (faltava, igual Skills/Armas já tinham) — necessário pro scroll funcionar com a lista mais alta de 15 linhas.
- Overlay behind panel has `raycastTarget = false` so bottom buttons stay clickable

### UI Construction Rule — RectTransform First

**CRITICAL:** When building UI GameObjects in code, always `AddComponent<RectTransform>()` BEFORE adding `Image` or `TextMeshProUGUI`.

UI components (`Image`, `TextMeshProUGUI`) auto-create a `RectTransform` when added. If you try to add `RectTransform` afterward, Unity returns `null` (can't add a duplicate component), causing `NullReferenceException`.

```csharp
// CORRECT
var rt = go.AddComponent<RectTransform>();
go.AddComponent<Image>();

// WRONG — rt will be null
go.AddComponent<Image>();
var rt = go.AddComponent<RectTransform>(); // null!
```

`MakeStrip()` and similar helpers return `RectTransform` — call `.gameObject.AddComponent<Image>()` on the result, not `.AddComponent<Image>()` directly.

### 01_MainMenu Scene — Key Objects

| Object | Notes |
|---|---|
| `Canvas` | ScreenSpaceCamera (renderMode=1), camera=519420031. Has: RectTransform, Canvas, CanvasScaler (1920×1080), GraphicRaycaster |
| `EventSystem` | Standalone GO with EventSystem + StandaloneInputModule — the only EventSystem in the scene |
| `MainMenuController` | fileID 1078761889; `selectedProfileHolder` set in Inspector |
| `CharacterPreviewManager` | Has `MainMenuCharacterPreview`; `spawnPoint` and `selectedProfileHolder` set |
| `Btn_SelectCharacter` | The "Personagem" button at anchor(0.5,0.5) pos=(600,−422), size=(495,170); onClick → `OnCharacterButton` |
| `BtnJogar` | Play button at pos=(0,−422); onClick → `OnPlayButton` |
| `BtnShop` | Shop button at pos=(−600,−422) |

Button math (1920×1080 canvas, anchor center): button center y = 540−422 = **118px** from bottom; tops at **203px**. Any panel `anchorMin.y` must be > 0.188 (use ≥ 0.20) to clear the buttons.

## Sorting Layers

`Default < Weapons < Characters < Weapons2 < Characters2` (ordem fixa do projeto, fundo→frente). Durante um turno (`CombatPlayer.ExecuteEvent`, casos `TurnStart`/`TurnEnd`, via `PlayerCombat.SetAttackerLayers`/`RestoreDefaultLayers`), body/arma de atacante e defensor são mapeados nessas 4 layers pra reproduzir, fundo→frente:

`Background < Arma do defensor < Corpo do defensor < Arma do atacante < Corpo do atacante`

- Arma do defensor → `Weapons` (mais atrás das 4)
- Corpo do defensor → `Characters`
- Arma do atacante → `Weapons2`
- Corpo do atacante → `Characters2` (mais à frente das 4)

Regras (todas simultâneas com só 4 layers, não dá pra ter mais que isso):
1. Arma do atacante na frente de TUDO do defensor (corpo e arma) — pra ficar visível conectando o golpe.
2. Corpo do atacante na frente da arma PARADA do defensor — um atacante desarmado não pode ficar com o corpo atrás da arma parada do oponente.
3. Cada personagem com a própria arma atrás do próprio corpo — vale pros dois lados, inclusive o atacante (a arma dele fica na frente do defensor inteiro, mas ainda atrás do PRÓPRIO corpo).
4. Defensor: arma atrás do próprio corpo (igual de sempre).

Não importa quem é o atacante do turno — `SetAttackerLayers` sempre atribui `Weapons2`/`Characters2` a `this`, `Weapons`/`Characters` ao `defender`, então os PAPÉIS trocam de personagem a cada turno, nunca de layer (a ordem relativa das 4 layers é fixa; só a reatribuição de qual personagem ocupa qual papel é por turno — não há necessidade de mudar a ORDEM em si turno a turno, só quem é "atacante" e quem é "defensor" neste momento).

**Histórico do bug (2026-07-04, três iterações):**
1. Ordem original (`Default < Weapons2 < Characters2 < Weapons < Characters`) deixava a arma do atacante (`Weapons2`) a mais ATRASADA das 4 — nunca aparecia na frente do defensor durante o golpe (bug reportado pelo usuário).
2. 1ª tentativa moveu só `Weapons2` pro fim (`Default < Characters2 < Weapons < Characters < Weapons2`) — resolvia (1), mas jogou `Characters2` (corpo do atacante) pra trás de `Weapons` (arma do defensor): atacante desarmado ficava com o corpo atrás da arma parada do defensor (regra 2 violada). Corrigido reordenando pra `Default < Weapons < Characters < Characters2 < Weapons2`.
3. Essa 2ª ordem resolvia (1) e (2), mas violava a regra 3 pro atacante: a arma dele (`Weapons2`, frontmost) ficava na frente do PRÓPRIO corpo (`Characters2`) — visualmente errado ao sacar a arma (bug reportado pelo usuário: "a arma fica por cima do Player1"). Corrigido trocando `Characters2`↔`Weapons2` de posição, chegando na ordem atual (`Default < Weapons < Characters < Weapons2 < Characters2`) — agora as 4 regras acima valem ao mesmo tempo.

Note que `Characters`/`Characters2` identificam **defensor/atacante** (não atacante/defensor, como antes de 2026-07-04) — só a ocupação por turno importa; outros sistemas que reusam essas 2 layers (pets, ver `PetCombatController.SetDepthLayer`) não fixam suposição nenhuma sobre qual papel ocupa qual layer. Restaurado (`Default`) em `TurnEnd`.

**Bug corrigido 2026-07-04 (3ª iteração — arma só ficava correta a partir do 2º turno)**: `SetAttackerLayers()` roda no `TurnStart`, mas se o personagem estava DESARMADO nesse momento (vai sacar arma pela 1ª vez ainda neste turno, via `PickupWeapon`/`WeaponSwap`/`Thief`), `SetWeaponLayer` não tinha nenhum `GameObject` de arma pra reatribuir (`CurrentWeapon == null`, early return) — a arma só é instanciada depois, no case `PickupWeapon`, e `EquipSpecific`/`EquipData` sempre criam o sprite na layer padrão do `WeaponHandler` (campo `sortingLayer`, default `"Weapons"`, nunca `"Weapons2"`). Resultado: no turno em que a arma é sacada, ela ficava presa em `"Weapons"` (mesma layer/prioridade da arma do DEFENSOR) em vez de `"Weapons2"` — aparecia atrás do defensor só nesse 1º turno; corrigia sozinha a partir do 2º turno, quando `SetAttackerLayers()` já achava o `CurrentWeapon` existente e reatribuía certo. Corrigido chamando `attacker.SetAttackerLayers()` de novo (idempotente) logo após o equip, dentro do próprio case `PickupWeapon` de `CombatPlayer` — `PickupWeapon` só acontece pro atacante da vez, então é sempre seguro reaplicar. `Thief`/`PlayerCombat.StealWeapon` tem o mesmo `EquipSpecific` (linha 1171) e provavelmente o mesmo bug, mas ainda não foi corrigido ali (reportado só via pickup normal até agora).

Bug corrigido 2026-07-04: `SetAttackerLayers`/`RestoreDefaultLayers` só existiam em `PlayerCombat.AttackRoutine` (legado, dead code enquanto `useSimulator=true`) — nunca rodavam de verdade em combate, então armas (sempre na layer `Weapons`, acima da `Default` do corpo) renderizavam na frente do corpo de QUALQUER personagem o tempo todo. Agora chamado pelo path ativo (`CombatPlayer`).

Ghost trail da Fierce Brute (`CombatPlayer.SpawnGhostTrail`) usa a layer `Default` (mais atrás que as 4 layers de combate acima) pra garantir que o rastro sempre renderize atrás do personagem/arma reais, independente de qual das 4 o atacante estiver usando no momento.

## Characters

| Character | Selectable | Role |
|---|---|---|
| Assassin Guy | Yes | Player1 option |
| Medieval Warrior | Yes | Player1 option |
| Medieval Warrior Girl | No (not in CharacterDatabase) | Hardcoded as Player2 in 04_CombatScenePVP |

To add a new selectable character: create a `PlayerProfile` in `Assets/ScriptableObjects/PlayerProfiles/` and add it to the `CharacterDatabase` asset.

## Animator Controller Architecture

Each character prefab has a Spriter2UnityDX-generated Animator Controller with a **flat state machine** (no sub-state machines) in the Base Layer.

### Parameters
| Name | Type | Purpose |
|---|---|---|
| `Idle` | Bool | Idle animation loop |
| `Running` | Bool | Run animation loop |
| `JumpStart` | Bool | Jump takeoff animation |
| `Hurt` | Trigger | Hit reaction |
| `Slashing` | Trigger | Sword/default attack (também usado por Heavy — sem estado separado) |
| `SlashingDagger` | Trigger | Dagger/Fast attack |
| `Blocking` | Trigger | Block reaction (defense pose) |
| `Dying` | Trigger | Animação de morte — AnyState→Dying (CanTransitionToSelf=0); disparado por AnimationController.PlayDying() no TriggerCombatEndRoutine |
| `Throwing` | Trigger | Throw weapon animation |
| `CatchWeapon` | Trigger | Pick up weapon animation (início do turno) |

### Key States and Transitions (manually added — originals generated by Spriter2UnityDX)
- **Any State → Slashing/SlashingDagger** *(combo)*: same conditions as Running→Slashing, `CanTransitionToSelf=1`. Allows re-entering Slashing from itself during combo chains.
- **Any State → Dying** *(death)*: condition `Dying` trigger, `HasExitTime=0`, `CanTransitionToSelf=0`. Fired by `AnimationController.PlayDying()` at `CombatEnd`. `TriggerCombatEndRoutine` in `CombatPlayer` waits 0.5s (one cycle), calls `SetSpeed(0f)` to freeze on the last frame, then waits 0.4s before calling `sequencer.OnCombatEnd(winner)`.
- **Any State → Block** *(parry)*: condition `Blocking` trigger, `HasExitTime=0`. Fires `AnimationController.PlayBlock(0.36666667f)`.
- **Block → Idle**: `HasExitTime=1`, `ExitTime=0.75`, condition `Idle=true`. Auto-exits after playing ≥75% of the animation.
- **Any State → Throwing** *(added for throw weapon)*: condition `Throwing` trigger, `HasExitTime=0`, `CanTransitionToSelf=0`. Fired by `ThrowRoutine` concurrently with `FlyWeapon`.
- **Throwing → Idle**: `HasExitTime=1`, `ExitTime=0.75`, condition `Idle=true`.
- **Idle state → Catch Weapon** *(added for pick-up)*: conditions `Idle=false AND CatchWeapon`, `HasExitTime=0`. Transition is in the **Idle state's own transitions** (NOT AnyState). Assassin Guy and Medieval Warrior Girl were correct from the start; Medieval Warrior required a manual fix (had it in AnyState + exit with `HasExitTime=1`).
- **Catch Weapon → Idle**: condition `Idle=true`, `HasExitTime=0` (condition-only, fires as soon as Idle is set). Motion: `Catch Weapon.anim` from Assassin Guy's prefab folder (GUID `478bd109963d38d46b36266bc1679f0f`), compatible with all three character skeletons.

`Block.anim` lives in each character's `Prefab/` folder (copied from Medieval Warrior original). Duration: `0.36666667s`. Animates arm/weapon bones into a raised-guard pose.

### Important: State Names Have Spaces
State names in the `.controller` files differ from trigger/parameter names:
| Trigger param | State name in controller |
|---|---|
| `Slashing` | `Slashing` |
| `SlashingDagger` | `Slashing Dagger` |

Use `animator.SetTrigger("SlashingDagger")` (no space), but `stateInfo.IsName("Slashing Dagger")` (with space) if checking current state.

### Combo Architecture
The combo fires `SetTrigger(slashTrigger)` from within the Slashing state. This works because the `Any State → Slashing` transitions have `CanTransitionToSelf=1`, allowing the animator to re-enter Slashing from itself with a 0.1s blend (restarts the animation). The `slashingToJumpDelay` pause in `ComboStrikeRoutine` controls rhythm between hits.

`ComboStrikeRoutine` recalculates `AttackPosition()` on every hit and runs `PlayRun` to reposition if the attacker is more than 0.3 units away — this handles both knockback (defender was pushed back on previous hit) and dodge (defender jumped back). Every hit including combo applies knockback.

## Combat Systems

### Fórmula de Dano (estilo My Brute — STR aditiva, não percentual)
`PlayerCombat.CalcDamage(isCrit)` / `CombatSimulator.CalcDamage(attacker, isCrit)`:

```
finalDamage = Max(1, RoundToInt((weaponBaseDamage + str) × critMultiplier × sharpMult × (1 - defenderArmor)))
```

STR soma direto no dano base da arma como valor flat (não como multiplicador percentual) — alinhado com o My Brute original, onde cada ponto de STR contribui dano fixo adicional. Era `weaponBaseDamage × (1 + str/10)` (percentual, divergia do original) — redefinida pelo usuário.

- `weaponBaseDamage` — `WeaponBaseDamage()`/`RollWeaponDamage()`: `weaponData.damage` direto (valor fixo configurado no asset, sem range aleatório), ou `3` se ≤0; Unarmed 5. Os ranges hardcoded por tipo (`Random.Range(7,13)` Dagger, `(10,18)` Sword, `(30,50)` Heavy) eram valores padrão do protótipo, de antes de cada arma ter o próprio campo `damage` configurável — removidos.
- `critMultiplier` — `1f` se não for crítico; senão `weaponData.critDamageMultiplier` (ou `UnarmedStats.CritDamageMultiplier = 1.5f` se desarmado).
- `sharpMult` — `1.5f` se Weapon Master + arma com a tag Sharp; senão `1f`.
- Lead Skeleton (`×0.85`) é aplicado **depois** do crit e **antes** da armadura, só para armas com a tag Blunt.
- Throw (`CalcThrowDamage`/`ThrowDamage`) **soma STR** — `weaponBaseDamage + str`, sem `critMultiplier`/`sharpMult` (era só `weaponBaseDamage`, sem STR — bug reportado pelo usuário, redefinida pra usar o mesmo componente aditivo de STR do golpe normal; ver seção **Throw Weapon**).

**Verificação:**
- Succubus `damage: 14`, STR 4, sem crit/armor/Weapon Master → `(14 + 4) × 1 × 1 × 1 = 18`
- Succubus `damage: 14`, STR 4, com Weapon Master (Sharp) → `(14 + 4) × 1 × 1.5 × 1 = 27`
- Desarmado (`damage: 5`), STR 4 → `(5 + 4) × 1 × 1 × 1 = 9`

### Tipos de Arma (`WeaponType`, `List<WeaponType>` — até 3 tags por arma)

`Assets/Scripts/Controller/WeaponData.cs`. Campos novos adicionados: `dropOdds` (float, % de chance de drop no level-up — ainda sem efeito no código, reservado para sistema de raridade futuro). O antigo enum exclusivo (`Sword/Heavy/Dagger/Fast/Slow/Thrown/Block`) foi substituído por um enum simples (`None, Sharp, Blunt, Long, Heavy, Fast, Thrown, Ranged`) guardado numa **lista** (`public List<WeaponType> types`), não um `[Flags]` bitmask — o Inspector do Unity não tem como esconder os valores automáticos `None`/`Everything` que `[Flags]` gera no dropdown de máscara, então a lista é a forma de deixar o usuário adicionar manualmente cada tag (elemento 0, 1, 2...) sem lixo no dropdown. Uma arma pode ter até 3 tags simultâneas (ex: Halberd = `Long, Heavy, Sharp`, Trombone = `Heavy, Blunt`, mirrorando o My Brute original). `Sword` e `Dagger` se fundiram em `Sharp` — a diferença "adaga vs espada" agora vem de combinar `Sharp` com `Fast` (adaga) ou não (espada). `Slow` e `Block` foram removidos (`Slow` não tinha asset usando; `Block` não é um tipo de arma, era uma categoria antiga de shield). `WeaponData.HasType(flag)` (instance) / `WeaponData.HasType(data, flag)` (static, null-safe) são os helpers de leitura (`types.Contains(flag)`), usados em vez de `switch`/`==`; `OnValidate()` avisa no Console se `types.Count > 3` (não força/limpa automaticamente).

**Cada fórmula de chance abaixo soma os valores-base de todas as tags presentes na arma** (não pega o máximo nem usa mais um "default" genérico — cada bônus vem de uma tag específica; tags sem entrada na tabela contribuem 0):

| Fórmula | Sharp | Fast | Heavy | Thrown | Long |
|---|---|---|---|---|---|
| `CritChance` base | 0.05 | 0.03 | 0.03 | 0 | 0 |
| `ComboChance` base | 0.12 | 0.03 | 0.04 | 0 | 0 |
| `DodgeChance` base (arma do defensor) | 0.10 | 0.05 | 0.05 | 0 | 0 |
| `DisarmChance` base | 0.10 | 0.10 | 0.05 | 0 | 0 |
| `BlockChance` base (arma do defensor) | 0.15 | 0.00 | 0.15 | 0 | 0 |
| `ThrowChance` | 0.15 | 0.00 | 0.10 | 1.00 | 0.12 |

`Blunt` e `Ranged` contribuem 0 em todas essas tabelas — `Blunt` só importa pra Lead Skeleton (-15% dano recebido, ver `WeaponData.IsBlunt`) e pra **não** receber o bônus de Weapon Master; `Ranged` (=7) adicionado ao enum para uso futuro com Arco e Flecha — sem lógica de combate associada ainda. `Long` contribui 0 em todas exceto `ThrowChance` (0.12, ver abaixo) — o bônus de Counter Rate/Reversal de cada arma "Long" continua definido manualmente por asset (`reversalBonus`/skills futuras), não por uma constante global.

**`ThrowChance` inclui `Long` (0.12) desde 2026-07-05** — pedido do usuário: TODA arma não-Thrown deve ter uma chance pequena de arremesso ocasional (mesmo espírito do My Brute original, onde qualquer arma podia ser arremessada eventualmente), incluindo armas `Long` como o **Whip** (antes tinha `ThrowChance = 0`, nunca arremessava). `TagSum` ganhou o parâmetro `longTag` (não pode se chamar `long`, palavra reservada em C#) — só usado por `ThrowChance`, as outras 5 fórmulas da tabela continuam sem `Long`. **Nota**: o `ThrowChance()` legado (`PlayerCombat.cs:570`, dead code enquanto `useSimulator=true`) não foi atualizado — mantém `Long` em 0, mesmo padrão de todo o resto do código legado nesta sessão.

**Dano base não soma por tag** — `weaponData.damage` tem prioridade absoluta (`RollWeaponDamage(data) => data.damage > 0 ? data.damage : 3`), sem depender de Sharp/Heavy/Fast. Cada `WeaponData` configura seu próprio valor fixo no Inspector; não há mais range aleatório por tipo.

**Alcance não é soma pura** (somar distâncias inteiras por tag não faz sentido físico):
- **Alcance** (`CombatPlayer.CalcAttackPosition`, caminho ativo): `reach = 2.0 + data.reach`, clampado em 0.3. **`scale` removido do cálculo** (pedido do usuário) — misturar os dois confundia a calibração (scale < 1 chegava a *aumentar* a distância em vez de aproximar, ver bug do Book documentado antes). `data.reach` (float) é o único knob de calibração por arma a partir de agora. Desarmado: reach fixo = 0.8.
  - **`CalcAttackPosition` ganhou um 3º parâmetro opcional `reachOwner`** (default = o próprio `mover`/atacante): de quem é o alcance que decide a distância de parada. Reversal não recalcula posição própria (retaliador nunca sai do lugar, herda a distância já estabelecida). **Counter é diferente**: quem bate primeiro é o DEFENSOR, então o atacante precisa parar dentro do alcance da arma DELE, não da própria — senão o defensor golpearia alguém fora do alcance de quem de fato conecta, e o knockback subsequente parecia um "teleporte" pra trás (bug real reportado pelo usuário). Corrigido no case `RunToDefender` de `CombatPlayer`: dá um peek no próximo evento da lista (`_events[_currentEventIndex + 1]`) — se for `CombatEventType.Counter`, passa `defender` como `reachOwner`; senão, comportamento de sempre (`attacker`). Só cobre a 1ª ação do turno (onde `RunToDefender` sempre roda); um Counter em plena continuação de combo herda a posição do `RepositionIfNeeded` anterior (sempre com o alcance do atacante), caso mais raro ainda não tratado.
  - Weapons com `scale != 1` (`Anchor` 1.3, `Axe` 1.2, `Baton` 1.5, `Bone` 1.2) tinham seu `reach` calibrado assumindo o termo de scale — a distância efetiva delas mudou com a remoção (scale > 1 antes aproximava; agora não aproxima mais nada, então essas 4 armas ficaram com alcance MAIOR que antes). `Book` (scale 0.8, reach -2.5) não muda — já batia no piso de 0.3 nos dois formatos, por coincidência. Recalibrar o `reach` dessas 4 armas se a distância nova não estiver boa (`AttackPosition/CalcAttackPosition`).
- **Trigger de animação** (`Slashing`/`SlashingDagger`): checado em ordem de prioridade — (1) `attackAnimation` explícito na cadeia `previousTier` (T2/T3 com Auto herdam do T1); (2) tag `Fast` → `SlashingDagger`; (3) fallback → `Slashing`. Configurado via `WeaponData.attackAnimation` (enum `Auto/Slashing/SlashingDagger`) — `CombatPlayer.SwingTrigger`.
- **Bodybuilder/Lead Skeleton**: checam `HasType(data, Heavy)`/`IsBlunt(data)` em vez de `== WeaponType.Heavy`.

Campos não afetados pela migração (continuam somando direto, sem tabela por tag): `hitSpeed`, `critChanceBonus`, `critDamageMultiplier`, `evasionBonus`, `dexterityBonus`, `reversalBonus`, `blockBonus`, `accuracyBonus`, `disarmBonus`, `comboBonus`, `deflectBonus` — cada um é um valor manual por asset, somado em cima do resultado das tabelas acima (mesmo padrão de sempre, ver `CombatSimulator`/`PlayerCombat`). `drawChance` **não** entra nessa lista — não é somado em nenhuma fórmula de chance; é peso de sorteio no Pickup/WeaponSwap, ver **Pegar Arma** abaixo.

`counterBonus` (float, default 0) — campo novo, mesmo padrão dos acima (valor manual por asset), soma direto em `CounterChance()` (ver **Counter e Reversal** abaixo) — defensor bate ANTES do golpe do atacante conectar, cancelando o hit, mesmo mecanismo de `PlayerState.counter`/skills Monk e Sixth Sense, só que como bônus de arma em vez de skill. Todas as 78 armas em `Assets/Data/Weapons/` começam em `0`.

Os 5 `WeaponData.asset` legados: `Satyr1` = `Sharp, Fast`, `Golem3` = `Heavy, Blunt`, `Succubus`/`VeryHeavyArmoredFrontierDefender`/`Zombie` = `Sharp` (trio continua idêntico entre si). As tabelas acima foram calibradas pra reproduzir exatamente os valores de chance/dano/alcance que essas 5 armas já tinham antes da migração. Os 26 assets novos em `Assets/Data/Weapons/` usam os mesmos campos — T1 têm sprites, T2/T3 sem sprite (herdam via `previousTier`). `WeaponHandler` não tem mais uma propriedade `currentType` própria (era um espelho de `data.type`, que não existe mais como valor único) — todo lugar que precisa ler o tipo da arma equipada usa `weaponHandler.CurrentWeaponData` direto com `HasType`/`IsSharp`/`IsBlunt`.

### Sistema de Tiers de Armas (T1 → T2 → T3)

**Campos em `WeaponData`:**
- `tier` (int): 1, 2 ou 3 — indica o nível de evolução.
- `previousTier` (WeaponData): referência ao tier anterior (T2→T1, T3→T2). `null` em T1.
- `attackAnimation` (AttackAnimation enum): `Auto` / `Slashing` / `SlashingDagger`. Auto herda pela cadeia `previousTier` via `SwingTrigger`.
- `reach` (float): knob de calibração de alcance (substituiu `int` — permite valores como 1.4).

**Geração automática — `Assets/Editor/WeaponTierGenerator.cs`:**
- Menu `Tools → AutoArms → Generate Weapon Tiers (T2 & T3)`: para cada T1 em `Assets/Data/Weapons/`, cria `<Nome> T2.asset` e `<Nome> T3.asset` se não existirem. Copia todos os stats (incluindo `attackAnimation`, `scale`, `reach`, `types`). T2: damage ×1.35; T3: damage ×1.75. `icon`/`inHandSprite` ficam `null` (atribuir manualmente).
- Menu `Tools → AutoArms → Assign All Weapon Tiers to AttackSequencer`: popula `AttackSequencer.allWeapons` com todos os assets T1/T2/T3 da pasta.

**Progressão em combate — `CombatResultPanel`:**
- `ShowLevelUpChoice` filtra: T1 aparece no pool se **não** há T2 ou T3 do mesmo já no loadout; T2/T3 aparecem se `previousTier` está no loadout.
- `ApplyBonus` remove `previousTier` do loadout ao escolher a evolução.
- `HasUpgradeInLoadout` percorre a cadeia T3→T2→T1 para verificar a presença.

**Herança de sprite em `WeaponHandler.EquipSpecific`:**
T2/T3 sem `inHandSprite` sobem a cadeia `previousTier` até achar um sprite válido — o personagem mostra visualmente o sprite do T1, mas `CurrentWeaponData` reflete a arma real do tier equipado (stats corretos para cálculos de dano/alcance/animação).

### Animação de 2 Frames (Sprite de Ataque)

`WeaponData.attackSprite` (opcional, `null` por padrão) — 2º frame da arma pra armas com sprite de "aberto/atacando" diferente do "fechado/idle" (`inHandSprite`), ex: **Whip** (chicote enrolado parado vs. estalando no golpe). `WeaponHandler.SetAttackPose(bool attacking)` troca o sprite do `SpriteRenderer` já instanciado na mão: `true` usa `attackSprite` (no-op se não configurado — arma continua com sprite único de sempre); `false` volta pro `inHandSprite`, subindo a cadeia `previousTier` se o tier atual não tiver um (mesmo fallback do `EquipSpecific`).

`CombatPlayer` chama `SetWeaponSwingPose(attacker, true/false)` (helper privado) simetricamente em todo `SetTrigger(Slashing/SlashingDagger)` melee — Hit (pet e personagem), Dodge (pet e personagem), Block, Counter/Reversal — `true` só depois do primeiro `WaitForSeconds(slashHalf...)` (bem perto do impacto, não junto do `SetTrigger`) — pedido do usuário pra dar a impressão de "chicotada" (o Whip fica fechado durante a largada do swing e só abre/estala no finalzinho, em vez de esticado o swing inteiro); `false` no fim do swing (mesmo ponto onde `SwingSpeedMultiplier` é resetado pra `1f`). Não se aplica a Repulse (arma arremessada voltando, não um swing de mão) nem ao arremesso em si (`ThrowWeapon`/`BoomerangReturn` usam sprites próprios via `FlyingWeapon`, sem relação com `attackSprite`).

Whip T1/T2/T3 (`Assets/Data/Weapons/`) usam os PNGs em `Assets/Data/UI/Weapons/Whip/` (`Whip1/2/3.png` = idle, `WhipAttack1/2/3.png` = ataque) — os 6 `.meta` foram criados manualmente (GUIDs fixos) porque o Unity ainda não tinha importado esses PNGs; `alignment: 0`/pivot centro (0.5, 0.5) por padrão — calibrar `positionOffset`/`rotationOffset`/`zOffset`/`scale` visualmente no Editor como qualquer arma nova.

`WeaponData.attackScaleMultiplier` (`Vector3`, default `(1,1,1)` = sem efeito) — multiplica `scale` só enquanto `attackSprite` está visível, pra ajustar proporção (ex: largura) do frame de ataque sem afetar o frame idle. Whip T1/T2/T3 usam `(3, 1, 1)` — chicote 3x mais largo (X) só no estalo, mesma altura/profundidade. `SetAttackPose` restaura `localScale = Vector3.one * scale` (sem multiplicador) ao voltar pro idle.

`WeaponData.attackRotationOffset` (`Vector3` euler, default `(0,0,0)` = sem efeito) — somado ao `rotationOffset` (campo do `WeaponHandler`, por personagem) só enquanto `attackSprite` está visível, sem alterar o `rotationOffset` base usado pelo idle e por qualquer outra arma. Whip T1/T2/T3 usam `(0, 0, -25)` — chicote apontando um pouco pra baixo, na direção do pé do defensor, só no golpe.

`WeaponData.showAttackTipEffect` (`bool`, default `false`) + `attackTipOffset` (`Vector3`, espaço local da arma antes de escala/rotação) — faísca procedural na ponta da arma, só no instante em que a pose de ataque liga (nunca ao desligar). `WeaponHandler.GetAttackTipWorldPosition()` resolve a posição mundial via `current.transform.TransformPoint(attackTipOffset)` — já reflete `attackScaleMultiplier`/`attackRotationOffset` automaticamente, então a faísca acompanha a pose de ataque mesmo se a arma girar/esticar. `CombatPlayer.PlayWeaponTipEffect` gera um burst radial de 6 sprites (textura circular criada em runtime via `Texture2D`, sem depender de nenhum asset externo — mesmo espírito de `FlashScreenWhite`/`SpawnGhostTrail`) que encolhem e desaparecem em ~0.18s, layer "Effects" (mesma de `BloodEffectPlayer`). Whip T1/T2/T3 ligam isso com `attackTipOffset: (0.5, -0.15, 0)`.

Valores de `attackRotationOffset`/`attackTipOffset`/pivot dos sprites (ver `.meta` acima) são chutes iniciais razoáveis — calibrar visualmente no Editor olhando o resultado real do sprite (sinal do ângulo, magnitude do offset).

### Critical Hit
`CritChance()` no atacante = soma por tag (ver tabela acima) + `weaponData.critChanceBonus` (ou `UnarmedStats.CritChanceBonus`) + `criticalChance` (profile/skills).

On crit: `critMultiplier = weaponData.critDamageMultiplier` entra na fórmula multiplicativa de dano. Popup mostra "CRIT!\n{damage}" em vermelho, fonte 5.
> Future skill **Fierce Brute**: +10% crit permanente.

### Dodge
`DodgeChance()` on the attacker, reading the **defender's** weapon tags (soma por tag, ver tabela acima).

Each agility point above 3 adds +2% dodge, plus the defender's `weaponData.evasionBonus` (or `UnarmedStats.EvasionBonus = +10%` if unarmed) e `defender.evasion` (campo de skill, ex: Untouchable +30%, Ballet Shoes +10%). Same AGI threshold adds +0.8% combo in `ComboChance()` (era +1.5%).

**`accuracy` do atacante (Relentless +30%) é o oposto de evasion** — em vez de aumentar a chance de esquiva de quem tem a skill, reduz a chance de esquiva do **defensor** contra esse atacante. `CombatSimulator.DodgeChance(attacker, defender)`: `total = baseChance + agiBonus + defender.evasion + weaponEvasion - attacker.accuracy - attackerDexterityBonus`, depois `Mathf.Clamp(total, 0f, 0.60f)` — mesmo teto de 60%, mas agora o clamp também cobre o piso (`accuracy`/`dexterityBonus` alto pode zerar a esquiva, não só reduzir). Era `Mathf.Min(0.60f, ...)` sem piso explícito (não importava antes porque nenhum termo conseguia ficar negativo sozinho).

**`WeaponData.dexterityBonus` da arma do ATACANTE** (2026-07-05) — sistema **separado** de `attacker.accuracy` acima (skill Relentless, stat de personagem): `dexterityBonus` é campo por arma (ex: Branch -1.0), lido como `attacker.currentWeaponData?.dexterityBonus ?? UnarmedStats.DexterityBonus` e subtraído do total (`- attackerDexterityBonus`) — dexterity positivo dificulta o dodge do defensor (subtrai mais), dexterity negativo facilita (subtrai um negativo = soma). Antes da auditoria pedida pelo usuário, o campo existia em toda arma mas nunca era lido em lugar nenhum (mesmo status de `deflectBonus`/`counterBonus` antes de serem conectados).

When dodge triggers: skip knockback, Hurt animation, and damage. Defender plays `DodgeLeap` (JumpStart animation + `JumpTo` backward by `knockbackDistance`, height 0.4) over `settings.dodgeDuration` (separate field from `hurtDuration` — was tied to it before, making the leap snap almost instantly). Popup shows "ESQUIVA!" in blue. Combo continues normally.

### Combo
`ComboChance(comboCount)` no atacante — `comboCount` = quantos hits extra de combo já aconteceram neste turno (0 no 1º hit extra). Base = soma por tag (ver tabela em **Tipos de Arma** acima; desarmado = 5%).

Soma-se `weaponData.comboBonus` (campo manual por asset, referência oficial do My Brute — Satyr1 +0.30, Sword trio 0, Golem3 -0.60, Branch +2.0 pra testar valores extremos) + `0.8%` por ponto de AGI acima de 3 + `comboChanceBonus` (skills, ex: Fists of Fury +20% — Relentless **não** soma mais aqui, foi redefinida para +30% accuracy, ver **Dodge** abaixo). O decaimento `Mathf.Pow(0.5f, comboCount)` age direto sobre esse total **bruto, sem clamp antes** (1º hit extra usa o valor cheio, 2º usa metade, 3º um quarto, e assim por diante) — só o resultado final (já decaído) é clampado em `[0, 1]` por sanidade de probabilidade. **Era `Mathf.Clamp(total, 0f, 0.60f)` antes do decaimento** (teto de 60%, originalmente 35%) — bug real reportado pelo usuário testando Branch (comboBonus alto o bastante pra somar ~400% de chance total): o clamp achatava tudo em 60%→30%→15%→7.5%..., bem abaixo do "sempre comba nos primeiros hits, depois decai" que uma chance tão alta deveria produzir (400%→200%→100%→50%→25%). Como `Roll()` trata qualquer chance ≥ 1 como "sempre dispara", uma arma normal (total baixo, tipo 0.30-0.45) não muda de comportamento — só armas deliberadamente extremas passam a de fato garantir combos consecutivos.

Valores antigos (base Fast 40%/Dagger 35%/Sword 25%/Heavy 10%/desarmado 10%, AGI +1.5%/ponto, sem clamp, sem decaimento) deixavam personagens com Dagger e AGI alta combando quase sempre e por muitos hits seguidos (ex: Assassin Guy com Satyr1 chegava a ~71-86% por golpe, repetido indefinidamente). `CombatSimulator.SimulateTurn` loga `[ComboChance] {nome} (P{1|2}, arma=...) hit extra #{n} chance={valor}` a cada checagem do loop de combo, antes do `Roll()` — usar isso para confirmar visualmente o decaimento e validar se algum combo de stats/skills ainda está inflando o valor base (pré-decaimento).

### Hit Speed (Ataques por Turno)

`WeaponData.hitSpeed` (ex: Anchor 0.48, Sword 0.67, Knife 2.0, Bow 2.67, Boomerang 3.75, Fan 4.29, Shuriken 10.0 — valores calibrados no padrão My Brute original) antes só multiplicava a velocidade da animação de swing no caminho legado (`PlayerCombat.HitRoutine`, código morto enquanto `useSimulator=true`) — sem efeito nenhum no jogo ativo. Agora `CombatSimulator.ResolveHitSpeedUnits(attacker, weaponData)` usa esse campo pra decidir quantas **sequências de ataque independentes** (cada uma com seu próprio golpe inicial `isCombo:false` + loop de combo próprio) o atacante executa no turno — pedido do usuário: 300% hitSpeed = 3 sequências; se o combo emendar em 2 delas, o turno todo soma 6 hits.

**Regra por faixa** (rolada de novo a cada turno em que a arma é usada):
- **hitSpeed ≥ 100%**: piso garantido (`Mathf.FloorToInt`, ex: 3.75 → 3) + chance da fração pra 1 unidade extra (ex: 75% de chance de virar 4). Sem estado entre turnos — resolvido do zero toda vez.
- **hitSpeed < 100%** (armas lentas, ex: Anchor 0.48, Hammer 0.52, Whip 0.8): acumula em `PlayerState.weaponHitSpeedDebt` turno a turno (mesmo princípio do acúmulo de Speed já existente pro Speed System, ver acima) até fechar 1.0 — a arma pode literalmente pular turnos inteiros sem agir enquanto o débito não fecha. `weaponHitSpeedDebt` é setado pra `1f` (não `0f`) sempre que a arma equipada muda de fato (Thief/PickupWeapon/WeaponSwap) — garante que o 1º turno com a arma nova já ataque de verdade (débito 1f + hitSpeed no cálculo desse mesmo turno sempre fecha 1.0), em vez de cair direto num `HitSpeedSkip` só por começar zerado; era `0f`, bug real reportado pelo usuário ("quando pega a arma aparece como lentidão e não ataca" — Whip 0.8 nunca fechava 1.0 no primeiro turno).
- Desarmado usa `UnarmedStats.HitSpeed = 1.0` (cai na faixa ≥100%, sempre exatamente 1 unidade — sem mudança de comportamento).

**Quando `ResolveHitSpeedUnits` retorna 0** (arma lenta, débito ainda não fechou): emite `CombatEventType.HitSpeedSkip` em vez de Run/Throw/Hit — o atacante simplesmente não age neste turno. `CombatPlayer` mostra o popup `DamagePopup.SpawnSlow` ("LENTO!") acima do próprio atacante, sem nenhuma animação de ataque.

**Arremesso** (`SimulateTurn`, item 3, antes do melee): hitSpeed também decide quantos ciclos completos de arremesso (ida + volta) acontecem no mesmo turno — **sem combo** (nunca existiu pra arremesso). `weaponToThrow` é capturado uma única vez antes do loop porque armas Thrown sem retorno (Shuriken, etc.) zeram `currentWeaponData` a cada arremesso (ver `SimulateThrow`) — sem recapturar, o 2º ciclo não teria mais arma pra jogar; reatribuída antes de cada `SimulateThrow` no loop. Bumerangue (`isBoomerang`) já devolve a arma à mão sozinho em cada ciclo (ver **Bumerangue** abaixo), então essa reatribuição vira um no-op nesse caso — é o motivo de o bumerangue (375%) realmente arremessar-e-voltar 3-4 vezes seguidas no mesmo turno, em vez de só uma. Cada ciclo revalida `targetPet` (pode ter morrido no ciclo anterior).

**Bug real (2026-07-05, Shuriken hitSpeed 10.0): arma "invisível" a partir do 2º arremesso do turno** — a reatribuição de `attacker.currentWeaponData` acima é só um dado do SIMULADOR; nada nunca reequipava a arma VISUALMENTE (`WeaponHandler.CurrentWeaponData`) depois que o 1º arremesso já a desequipou (`Unequip`, arma sem tag Thrown-permanente continua no loadout mas o objeto na mão é destruído). No 2º `ThrowWeapon` em diante, `CombatPlayer` achava `CurrentWeaponData == null` e pulava a criação do `FlyingWeapon` inteira — nada era arremessado, nem visualmente nem com objeto nenhum na tela. Fix: `CombatPlayer` reequipa a arma (`FindWeaponByName` + `EquipSpecific`, por `evt.weaponName`) se `CurrentWeaponData` estiver nulo no início do case `ThrowWeapon` — "surge outra shuriken" na mão antes de cada arremesso subsequente, pedido explícito do usuário. Mesmo chama `attacker.SetAttackerLayers()` de novo logo depois (`EquipSpecific` sempre cria a arma na layer padrão do `WeaponHandler`, papel do DEFENSOR — mesmo bug/fix já visto em `PickupWeapon`/`BoomerangReturnFlight`). Não afeta bumerangue (já reequipa sozinho via `BoomerangReturnFlight`, então essa checagem já encontra `CurrentWeaponData` preenchido e não faz nada) nem armas com só 1 arremesso por turno (nunca atingem um 2º ciclo).

**Melee** (`SimulateTurn`, item 4): o `for` externo repete `RunToDefender` + `SimulateHitWithDetermination(isCombo:false)` + `SimulateComboLoop` por `meleeUnits` vezes, quebrando cedo se o atacante ou o defensor morrer, ou se `interrupted` vier `true` (Counter cancela o resto, mesma regra que já cancelava o combo em si).

### Block
`BlockChance()` no atacante, lendo as tags da arma do **defensor** (soma por tag, ver tabela em **Tipos de Arma** acima; sem arma equipada = 0).

Soma-se ainda o `weaponData.blockBonus` do defensor (ou `UnarmedStats.BlockBonus = -25%` se desarmado) e `defender.blockBonus` (campo de skill — Counter Attack `+0.10`, ver **Counter e Reversal** abaixo). **`defender.counter` não entra mais aqui** — ver seção **Counter e Reversal** abaixo (rewired pra uma mecânica própria, em vez de ser só um bônus de block).

**`WeaponData.accuracyBonus` da arma do ATACANTE** (2026-07-05) — subtraído no final: `BlockChance = weaponBonus + weaponBlockBonus + defender.blockBonus + survivalBonus - attackerAccuracyBonus`, onde `attackerAccuracyBonus = attacker.currentWeaponData?.accuracyBonus ?? UnarmedStats.AccuracyBonus`. Quanto maior o accuracy da arma do atacante, menor a chance de block do defensor (o golpe "penetra" o block com mais frequência) — ex: Branch `accuracyBonus: 2.0` deixa `BlockChance` bem negativa contra qualquer defensor, e como `Roll()` nunca dispara com chance negativa, o block fica efetivamente impossível. Sem clamp (mesmo padrão de sempre desta fórmula). Antes da auditoria pedida pelo usuário, o campo existia em toda arma mas nunca era lido em lugar nenhum — inclusive o parâmetro `attacker` de `BlockChance(attacker, defender)` era vestigial (nunca referenciado no corpo do método) até esta mudança.

Ordem de verificação em `CombatSimulator.SimulateHit` (caminho ativo): **Counter → Block (+ Reversal) → Esquiva → Dano normal → Reversal → Desarmar** (era Block → Esquiva → Counter — Counter movido pra primeiro, ver **Counter e Reversal** abaixo pro motivo; mesmo padrão de Block-antes-de-Esquiva aplicado em `SimulateRetaliation`, que não tem Counter). Quando block trigga: sem dano, sem Hurt, mas aplica **knockback de 50%** (`knockbackDistance * 0.5f`) em paralelo. Defensor executa animação `Block` via `SetTrigger("Blocking")`. Popup "BLOCK!" em dourado.

**Drop de arma/escudo ao bloquear** — verificados após o popup de block:
- **15%** de chance do **atacante** soltar a arma (impacto no escudo) — independente do que o defensor tem equipado.
- Do lado do **defensor**, escudo tem prioridade sobre a arma — **mutuamente exclusivos no mesmo hit**: enquanto `defender.hasShield` for true, só o escudo pode cair (**10%**, `ShieldDisarmChance`, mesma constante fixa da seção **Desarmar do Escudo** — sem `disarmChanceBonus`/`disarmBonus`); só depois que o escudo já caiu (neste hit ou em hit/bloqueio anterior) é que a arma do defensor passa a correr risco de cair (**10%**) ao bloquear. Antes os dois eram checados independentemente — podiam cair os dois no mesmo hit, o que não fazia sentido (o escudo deveria proteger a arma por baixo dele, igual ao My Brute; bug real reportado pelo usuário).
- Arma usa `DropWeapon(target, isDisarm: false)`; escudo usa `DropShield(target, isDisarm: false)` (mesma queda em pêndulo, evento `CombatEventType.ShieldDrop`) → popup "DROP!" laranja + item cai com pêndulo, fica no chão até fim da luta.

### Counter e Reversal

Duas mecânicas distintas, ambas usando o defensor tomando a iniciativa de volta do atacante — implementadas só no caminho ativo (`CombatSimulator.cs`); **`PlayerCombat.cs`/`AttackSequencer.cs` (legado, código morto enquanto `useSimulator=true`) não têm nenhuma das duas, nem o ajuste de speed 0 abaixo** — seu `BlockChance()` ainda soma `defender.counter` (comportamento antigo), não existe `SimulateRetaliation` equivalente, e `CombatLoop` ainda força mínimo 1 ação mesmo com `speed = 0`. Só importa se `useSimulator` for desligado algum dia.

**Counter** — `CounterChance(defender) = defender.counter + weaponData.counterBonus` (arma do próprio defensor — campo novo, mesmo padrão de `reversalBonus`/`blockBonus`/`disarmBonus`, valor manual por asset em vez de tabela por tag; todas as 78 armas em `Assets/Data/Weapons/` começam em `0`, exceto onde o usuário já configurou manualmente, ex: Whip). Checado em `SimulateHit` **primeiro de tudo, antes até do Block** (era depois do Block falhar — Monk/Sixth Sense quase nunca chegavam a counterar de fato, porque sempre que o Block do defensor tinha sucesso primeiro a função retornava antes do Counter ser checado; bug real reportado pelo usuário: Monk não reagia nem depois de bloquear nem ao tomar hit): o atacante já correu e iria acertar, mas o defensor bate primeiro — cancela completamente o hit do atacante (e o resto do combo dele, já que esse hit nunca aconteceu de fato). Só se Counter **não** disparar é que o defensor tenta Block, depois Esquiva. `defender.counter` é alimentado por **Monk** (`counter += 0.40`) e **Sixth Sense** (`counter += 0.10`, versão mais fraca, sem nenhum outro efeito). **Counter Attack não soma mais nesse campo** — foi redefinida (ver Reversal abaixo).

**Reversal** — `ReversalChance(defender) = defender.reversal + weaponData.reversalBonus` (ou `UnarmedStats.ReversalBonus = 0` se desarmado; os 5 `WeaponData.asset` já tinham `reversalBonus` preenchido — Sword +0.10, Heavy -0.30 — mas nenhum código lia o campo até agora). Ao contrário do Counter, Reversal **só age depois de algo já ter acontecido** — checado em **dois pontos** de `SimulateHit`:
1. **Depois de bloquear** (dentro do bloco de `Block`, depois dos checks de drop de arma): defensor já bloqueou, sem tomar dano, e ainda assim contra-ataca de bandeja. Aqui a chance verificada é `ReversalChance(defender) + defender.reversalAfterBlock` — campo extra que **só soma neste ponto**, nunca no ponto 2 abaixo.
2. **Depois do dano normal já aplicado** (hit aconteceu, HP já foi reduzido): defensor contra-ataca em seguida. Usa só `ReversalChance(defender)` puro, sem `reversalAfterBlock`.

**Counter Attack** (redefinida pelo usuário — antes dava `counter += 0.40`, papel que hoje é só de Monk/Sixth Sense) agora dá `blockBonus += 0.10` (soma em `BlockChance()`, ver seção **Block** acima) e `reversalAfterBlock += 0.90` — uma chance de reversal **exclusiva do ponto 1**, que só dispara depois que o personagem já bloqueou (não depois de um hit normal). `PlayerState`/`PlayerCombat` ganharam os dois campos novos (`blockBonus`, `reversalAfterBlock`), mesmo padrão dos outros campos de skill (default 0).

Diferente do Counter, Reversal **não cancela o resto do combo do atacante** — o combo continua normalmente depois, e cada hit extra do combo checa Reversal de novo, independente do(s) anterior(es) (pode triggar em mais de um hit do mesmo combo). O que já aconteceu (bloqueio ou dano) não é desfeito de qualquer forma. Reversal nunca age ANTES de um resultado (esse é o papel do Counter) — só depois de Block ou de Hit.

Ambas chamam `SimulateRetaliation(retaliator, target, eventType)` — o contra-ataque passa por bloqueio/esquiva/crítico normalmente contra o lado oposto (pode ser bloqueado ou esquivado pelo atacante original), mas **não verifica Counter/Reversal de novo** (evita recursão infinita entre as duas mecânicas — uma retaliação é sempre só uma retaliação, não pode ser contra-contra-atacada). `SimulateRetaliation` também checa **Block primeiro, depois Esquiva** — mesma ordem do `SimulateHit` principal (ambos eram Esquiva → Block antes do usuário pedir a inversão; primeiro só na retaliação, depois generalizada pro hit normal também).

`SimulateHit` retorna `bool interrupted` (era `void`) — `true` **só quando Counter trigga** (o único caso que de fato cancela o resto do combo, já que o hit nunca aconteceu); `false` em qualquer outro desfecho, **incluindo Dodge/Block/Reversal**. `Disarm` (depois do hit normal) agora também checa `attacker.isAlive` — Reversal pode matar o atacante na própria retaliação, e sem essa checagem um atacante já morto ainda desarmava o defensor que tinha acabado de contra-atacar. `SimulateTurn`'s loop de combo é `while (!interrupted && attacker.isAlive && defender.isAlive)` — a checagem de `attacker.isAlive` é necessária porque o atacante pode morrer de um Counter/Reversal no meio do próprio turno.

Eventos novos: `CombatEventType.Counter`/`Reversal` (`playerIndex` = quem retalia e causa dano, `targetIndex` = atacante original que recebe) — visual em `CombatPlayer.cs` é parecido com o `Hit` (swing + knockback + hurt), com duas diferenças: **sem `RepositionIfNeeded`** (quem retalia nunca saiu do lugar — é o atacante original que correu até ele; Counter/Reversal disparam antes de qualquer dano nesta troca, então não há knockback prévio que tenha deslocado o retaliador) e **`PlayJumpStart` em vez de `JumpTo`** depois do swing (ver nota abaixo sobre o bug de animação). Popup: `DamagePopup.SpawnCounter`/`SpawnReversal`, texto "CONTRA-ATAQUE!"/"REVERSAL!" em roxo. `CombatLogFormatter` imprime `[CONTRA-ATAQUE]`/`[REVERSAL]` antes da linha de dano.

**Bug de animação travada depois de Counter/Reversal (e a causa real)**: nos `.controller` dos 3 personagens (verificado no da Assassin Guy, mesmo padrão nos outros), os estados `Slashing`/`Slashing Dagger` têm **uma única transição de saída**: pro estado `Jump Start`, condicionada ao bool `JumpStart == true`. `Jump Start` só sai pro `Idle` quando `JumpStart` volta a `false` (e `Idle == true`). Não existe transição direta Slashing→Idle no Animator. Isso nunca foi um problema antes porque todo combo termina em `TurnEnd`, que sempre chama `PlayJumpStart` (toggle do bool) **+** `movement.JumpTo` (pulo de volta ao spawn) em sequência — mas quem retalia num Counter/Reversal nunca passa por um `TurnEnd` próprio nesta troca, então ficava **permanentemente travado no estado de Slashing** depois de atacar (um primeiro fix tentando `SetIdle(true)` não resolvia nada, porque a transição de saída do Slashing nem olha pro bool `Idle`, só pro `JumpStart`). Fix: `CombatPlayer.cs` chama `attacker.animationController.PlayJumpStart(jumpStartDuration * t)` pro retaliador depois do swing — mesmo toggle de bool que o `TurnEnd` usa, **sem** chamar `movement.JumpTo()`, então o personagem só faz o pequeno "hop" do Jump Start no lugar (sem se deslocar) e volta pro Idle corretamente.

**`CombatEvent.isRetaliation` (2026-07-05)** — o fix acima só cobria os cases dedicados `Counter`/`Reversal`. Mas `SimulateRetaliation` pode **também** resolver a retaliação como um `Block` ou `Dodge` comum (se o alvo original bloquear/esquivar a retaliação — checado antes do dano, ver função acima), e o `attacker` desses dois eventos passa a ser o RETALIADOR (não o dono do turno) — que **não** vai ter um `TurnEnd` próprio nesta troca pra sair do `Slashing` (única saída é o toggle de `JumpStart`, ver bug acima). `SimulateRetaliation` marca `isRetaliation = true` nesses eventos; `CombatPlayer` usa a flag só pra aplicar esse mesmo toggle de `PlayJumpStart` ao final (reposicionamento normal continua rodando igual, ver correção abaixo).

**Reversal sem reposicionar (bug real, 2026-07-05, corrigido depois do primeiro fix não resolver)**: o comentário original do case `Counter`/`Reversal` dizia "quem retalia nunca saiu do lugar" e por isso nunca chamava `RepositionIfNeeded` — verdade pro **Counter** (dispara ANTES de qualquer dano nesta troca, cancela o hit antes dele conectar, então o retaliador realmente nunca levou nada), mas **falso pro Reversal**, que só age DEPOIS de um Hit ou Block já resolvidos — e os dois aplicam knockback no retaliador (Hit: cheio; Block: 50%). Sem reposicionar, o retaliador podia golpear de longe, fora de alcance de verdade — reportado pelo usuário (testando Branch, `reversalBonus: 1` = 100%, combo alto, retaliação acontecendo o tempo todo) como "o dano do reversal só parece acontecer depois que o atacante original volta pro spawn (TurnEnd)", quando deveria ser no instante do próprio golpe de volta. Fix: `CombatPlayer` chama `RepositionIfNeeded(attacker, defender, t)` só quando `evt.type == CombatEventType.Reversal` (não `Counter`) antes do swing — e a mesma correção se aplica às retaliações que viram `Block`/`Dodge` (`isRetaliation`), que também tiveram o `RepositionIfNeeded` restaurado (a 1ª tentativa de fix tinha pulado o reposicionamento ali também, por engano, seguindo a mesma suposição errada). `RepositionIfNeeded` já é no-op se a distância já for pequena, então não introduz uma corrida desnecessária quando o retaliador já está perto.

**Tentativa revertida (2026-07-05)**: chegou a existir uma versão de `TurnEnd` que pulava o jump-back pro spawn quando o próximo `TurnStart` era do mesmo jogador (ação extra por velocidade emendando) — hipótese de que esse jump-back seria um "vai-e-volta inútil" responsável pelo bug do Reversal atrasado (ver log de debug temporário que motivou a tentativa). Não corrigiu o bug do Reversal **e** quebrou o comportamento correto: o personagem deve voltar ao spawn a cada ação, mesmo nas extras de velocidade — sem isso, ações extras emendavam sem o "RAPIDO!"/retorno correspondente, parecendo um combo contínuo em vez de ações distintas. Revertido — `TurnEnd` volta a pular pro spawn incondicionalmente (sujeito só ao guard `!attacker.IsDead`/`InSpawnZone` de sempre).

**Causa real do "Reversal sem Hurt" (bug real, 2026-07-05, achado por vídeo — os logs de posição/tempo não revelavam nada de errado, a sequência de dano/knockback sempre batia)**: no `.controller` (verificado no da Medieval Warrior Girl), a transição pro estado **Hurt** exige `Idle == true` **E** o trigger `Hurt` simultaneamente, e pertence só ao estado `Idle` (não é uma transição Any State, ao contrário de `Blocking`, que está em `m_AnyStateTransitions` e por isso já funcionava de qualquer estado). No Reversal-após-Hit (o caso mais comum), o `defender` deste evento é o ATACANTE ORIGINAL, que acabou de golpear e ainda está preso no estado `Slashing` — só sai dele via o toggle de `JumpStart`, que só roda no `TurnEnd` PRÓPRIO dele, ainda não alcançado nesta troca (o Reversal acontece antes). Preso em Slashing, o trigger `Hurt` nunca encontra uma transição válida pra consumir — o defensor nunca reage visualmente ao dano (sem flash, sem knockback aparente), mesmo com o número de dano/popup aparecendo certinho. Mesmo problema no Reversal-após-bloqueio (o Block anterior também não tira do Slashing). Fix (2ª tentativa, a 1ª usando `PlayJumpStart` não resolveu de verdade — ver abaixo): `AnimationController.ForceIdleState()` (novo método) chamado no **defensor**, só quando `evt.type == CombatEventType.Reversal` (não `Counter` — nesse caso o defensor nunca chegou a golpear, já está em Idle), imediatamente antes do `Knockback`/`PlayHurt` — `anim.Play("Idle", 0, 0f)` força o estado direto, **sem transição/blend nenhuma**, então o Hurt já dispara no frame seguinte.

**1ª tentativa (não resolveu, revertida)**: usar `PlayJumpStart` (mesmo toggle do retaliador) no defensor antes do Hurt. Diferença crucial: `PlayJumpStart` toca o estado **Jump Start de verdade** (Slashing→Jump Start→Idle, cada transição com seu próprio blend/duração real no Animator) — e só espera a duração nominal (`jumpStartDuration`, ~0.02s), tempo curto demais pra essas transições terminarem de fato. Resultado: o Hurt trigger disparava enquanto o personagem ainda estava VISUALMENTE no meio da transição de pulo — reportado pelo usuário como "o hurt está no momento que o player2 está no final do jumping voltando pro spawn, deveria ser antes de pular". `ForceIdleState()`/`Animator.Play(...)` corta direto pro Idle sem depender de transição nenhuma configurada no Controller, eliminando a animação de pulo visível por completo.

**Evasion zerada de verdade (Deity)**: zerar só o campo `defender.evasion` (via `evasionPct`) não bloqueava esquiva de verdade — `DodgeChance()` ainda soma chance base por tipo de arma do defensor, bônus de AGI e o `evasionBonus` da própria arma do defensor, todos independentes do campo `evasion`. Novo campo `PlayerState.noEvasion` (`bool`, setado por Deity) faz `DodgeChance()` retornar `0f` direto no início, ignorando todos esses outros termos — só assim "-100% evasion" garante 0% de esquiva de fato, e não só zerar o termo de skill dentro de uma soma que ainda dava chance.

### Throw Weapon (Jogar Arma)
Verificado **no início do `AttackRoutine`, ANTES do melee**, após o idle. Se triggar: atacante arremessa do lugar onde está (sem Run até o defensor); turno encerra sem JumpBack. Se não triggar: executa melee normal (Run → Slash → JumpBack).

`ThrowChance()` = soma por tag (ver tabela em **Tipos de Arma** acima; sem arma = 0).

Flow:
1. **Sem Thrown e sem Hideaway**: `UnequipPermanent()` = `Unequip()` + `loadout.RemoveCurrentWeapon()` (removida do loadout dessa luta pra sempre). **Com a tag `Thrown` (`HasType`, não `==` exato) OU com Hideaway**: `Unequip()` apenas — a arma fica desarmada na mão, mas continua no loadout, podendo ser pega de novo num pickup futuro (40% normal no início de algum turno seguinte).
2. Create `FlyingWeapon` GameObject with the weapon's `inHandSprite` — uma réplica independente. `localScale = Vector3.one * weaponData.scale` for **all types** (same scale as the in-hand sprite).
3. `SetTrigger("Throwing")` fires animator concurrently.
4. `FlyWeapon()` moves sprite in a **straight line** over 0.45s. **Only weapons with the `Thrown` tag** rotate (540°/s). All other types fly with fixed rotation.
5. On landing: chance de acerto **80%**, ou **55%** se o defensor tiver Hideaway (`-25%`, ver **Hideaway** abaixo) — hit aplica weapon damage + STR + Hurt + knockback; miss (`20%`/`45%`) faz o defensor jogar `DodgeLeap` (same animation as dodge) + gray "MISS!" popup.
6. After hit/miss: fica desarmado até o próprio `TurnStart` do próximo turno dele, que já tem o check normal de 40% pra pegar arma — mesmo padrão de qualquer outro turno desarmado (vale também pra Hideaway agora, sem nenhum tratamento especial — ver **Hideaway** abaixo). Era **40%** de chance de re-equipar imediatamente aqui (`CombatEventType.WeaponEquipped`, removido), mas isso fazia o personagem ocasionalmente "equipar" uma arma já no fim do turno (depois do hit/miss do arremesso), sem nenhuma ação visível correspondente — bug real reportado pelo usuário: pickup deveria sempre acontecer no início do turno, nunca no meio/fim.
7. `animationController.SetIdle(true)` — obrigatório ao final do caminho de throw para sair do estado `Throwing` antes do próximo turno. Sem isso, o animator fica preso em `Throwing` (a transição `Throwing → Idle` exige `Idle=true`), causando hurt e slash nas animações erradas quando o oponente ataca nesse intervalo.

`PlayerLoadout.runtimeWeapons` is a `List<WeaponData>` initialized lazily on first `GetNextWeapon()` call (after `CombatSceneLoader` has assigned the loadout). `RemoveCurrentWeapon(expected)` removes the entry at `currentIndex` only if it matches `expected` (guards against index drift), then decrements `currentIndex` so the next `EquipNext()` gets the correct successor.
`WeaponHandler.UnequipPermanent()` captures `CurrentWeaponData` before calling `Unequip()` (which clears it), then passes the reference to `RemoveCurrentWeapon(expected)` for validation.
`DamagePopup.SpawnMiss(worldPos)` spawns a gray "MISS!" popup.

**Bumerangue (`WeaponData.isBoomerang`)** — exceção ao passo 6 acima: em vez de ficar desarmado até o próximo `TurnStart`, a arma volta pra mão do próprio atacante e permanece equipada até um `WeaponSwap` (40% no início de um turno futuro) ou um `Disarm`/`WeaponDrop` tirá-la. Boomerang T1/T2/T3 (`Assets/Data/Weapons/`) têm `isBoomerang: 1` além da tag `Thrown` normal (então continuam sempre-arremesso via `ThrowChance` 100%, ver **Tipos de Arma** acima).
- **Resolução do arremesso é diferente das outras Thrown**: em vez da chance fixa de acerto (80%/55%) do passo 5 acima, o bumerangue rola `BlockChance(attacker, defender)` (+25% se o defensor tiver Hideaway) e, se não bloquear, `DodgeChance(attacker, defender)` — as mesmas fórmulas reais do melee (agilidade, tags da arma do defensor, skills), em vez de Miss genérico. Emite `CombatEventType.Block`/`Dodge` normalmente (com `isThrow = true`) só quando bloqueia/esquiva; senão aplica o Hit como qualquer arremesso. Net ainda força acerto (sem mobilidade pra reagir). Pedido explícito do usuário — as outras armas Thrown (Shuriken etc.) continuam com a chance fixa.
- `CombatPlayer` (`Dodge`/`Block`): quando `evt.isThrow = true`, pula `RepositionIfNeeded`/swing do atacante (ele nunca correu nem sacou arma pro arremesso) — só a reação do defensor (DodgeLeap/Block) toca.
- `CombatSimulator.SimulateThrow`: logo após resolver Hit/Dodge/Block (não se aplica ao caminho de Repulse nem de pet como alvo), se `isBoomerang` seta `attacker.currentWeaponData = weaponData` de volta (em vez de ficar `null`) e emite `CombatEventType.BoomerangReturn` (`targetIndex = defender.index` obrigatório — `CombatPlayer` resolve `defender` por esse campo).
- `CombatPlayer` (case `ThrowWeapon`): assim que a ida chega ao alvo, dispara o voo de volta em **paralelo** (fire-and-forget, `StartCoroutine(BoomerangReturnFlight(...))`, referência guardada em `_boomerangReturnRoutine`) reaproveitando o mesmo `GameObject` (`_boomerangFlyingObject`/`_boomerangLandPosition`) em vez de esperar o case `BoomerangReturn` (que só roda depois do Hit/Dodge/Block) — a arma nunca fica parada/imóvel esperando; fica sempre em movimento, sobrepondo o voo de volta com a reação do defensor. Peek no próximo evento da lista (`_currentEventIndex + 1`) decide se o voo de volta deve mesmo começar: pula (destrói normalmente, como qualquer Thrown) se o próximo evento for `Repulse` (o defensor deflectiu — cria seu próprio objeto de volta) ou um Hit/Miss com `targetIsPet` (bumerangue nunca volta contra pet, ver `SimulateThrow`).
- `BoomerangReturnFlight` (helper coroutine): reorienta o objeto pra direção de volta, voa via `FlyWeapon(..., rotate: true, arc: -0.5f)` — arco negativo curva por baixo, oposto ao arco positivo (por cima) da ida — depois destrói o objeto e `weaponHandler.EquipSpecific(weaponData)`, sem `PlayCatchWeapon` (o trigger competia com a transição Throwing→Idle ainda em andamento no Animator, causando uma travada visual). `PlayerCombat.FlyWeapon` mudou a guarda de `arc > 0f` para `arc != 0f` pra permitir esse arco negativo. **Sorting layer ao reequipar** (bug reportado pelo usuário: "o boomerang ficou por cima do player1" ao voltar pra mão): `EquipSpecific` sempre cria o sprite na layer padrão do `WeaponHandler` (`"Weapons"`, papel do DEFENSOR — mesmo bug do `PickupWeapon`, ver **Sorting Layers**), nunca `"Weapons2"` (papel do atacante) — corrigido chamando `thrower.SetAttackerLayers()` de novo logo após o `EquipSpecific`, dentro do próprio `BoomerangReturnFlight` (`thrower` é sempre quem está atacando/arremessando neste turno).
- `CombatPlayer` (case `BoomerangReturn`): só `yield return _boomerangReturnRoutine` — espera a coroutine já em andamento terminar, sem iniciar um voo novo (evitava tanto o "sumiço" quanto a "travada" de versões anteriores, quando o voo de volta só começava aqui, depois do Hit inteiro já ter rodado). Limpeza defensiva do objeto/coroutine nos caminhos que nunca chegam a emitir `BoomerangReturn` (Repulse, alvo pet, rede de segurança no topo do case `ThrowWeapon`).

### Troca de Arma (Weapon Swap) — mecânica geral, não é skill

Pedido pelo usuário como ação independente de Hideaway, **vale pra todo mundo**: se o atacante já está **armado** no início do turno, há a **mesma chance de 40%** do pickup normal (ver **Pegar Arma** abaixo) de **trocar** de arma — joga a atual no chão (mesma queda em pêndulo de `DropWeapon`/`WeaponDrop`) e puxa uma nova do loadout, ponderada por `WeaponData.drawChance` (ver **Pegar Arma** abaixo — excluída a arma atual do sorteio). Checado em `SimulateTurn`, **mutuamente exclusivo** com o pickup-se-desarmado (`else if`, já que um exige estar desarmado e o outro armado) e com Thief (`!stoleWeapon`, mesmo motivo).

**Guard de "1º turno" removido (2026-06-23)**: existia um `PlayerState.hasTakenFirstTurn`/`isFirstTurn` só para bloquear o swap no 1º turno de quem já nascia armado (`EquipStartingWeaponIfNeeded`, 40% antes da luta — ver nota em **Chance de já cair em cena armado**, removida). Como ninguém mais nasce armado, esse guard nunca tinha mais como disparar de verdade (o 1º turno de todo personagem agora sempre cai no `if` de pickup normal — item 2 — nunca no `else if` de swap — item 2b, que exige já estar armado) — removido por completo junto do campo. Item 2/2b continuam mutuamente exclusivos no mesmo turno por construção (`if`/`else if`), então o swap nunca "compete" com o pickup normal de qualquer forma.

A arma trocada **some do loadout/`WeaponHUD` permanentemente** — `attacker.weaponLoadout.Remove(oldWeapon)` no simulador, `PlayerCombat.DropWeapon` (sem nenhum parâmetro extra — a primeira versão tinha um `removeFromLoadout: false` pra manter a arma disponível, mas o usuário corrigiu: ela deve desaparecer de verdade, igual a `WeaponDrop`/`Disarm`, pra não poder ser sacada de novo) cobre o popup "DROP!" + a queda visual + a remoção via `UnequipPermanent()`.

Dois eventos novos em sequência: `CombatEventType.WeaponSwap` (`weaponName` = arma largada, visual fire-and-forget via `DropWeapon`) seguido de um `PickupWeapon` normal pra nova arma (reusa a animação `CatchWeapon` já existente, sem precisar de nenhum evento/animação dedicada pra "puxar a nova arma").

### Pegar Arma (Início do Turno)
Ambos os personagens começam o combate **desarmados**. Ao iniciar cada turno, se desarmado:
- **40%** de chance de puxar uma arma do loadout + animação `CatchWeapon` (0.6s) → ataca com a arma.
- **60%** não pega → ataca desarmado (soco).

**Caminho ativo (`CombatSimulator.SimulateTurn`, item 2/2b)**: a arma sorteada dentro do loadout é ponderada por `WeaponData.drawChance` — `PickWeaponByDrawChance(loadout, exclude)` monta um peso cumulativo (`Mathf.Max(drawChance, 0.01f)` por arma, piso mínimo pra loadouts com o campo zerado não travarem) e sorteia um float entre 0 e o peso total. Chance de 40% "acontece um pickup neste turno" continua fixa — `drawChance` só decide QUAL arma entre as disponíveis, não é recalculado turno a turno (uma vez armado, a arma sorteada fica equipada normalmente até `WeaponSwap`/`Disarm`/`WeaponDrop`, mesma regra de sempre — ver **Draw Chance** abaixo pro histórico da verificação/decisão).

**Caminho legado (`PlayerCombat.AttackRoutine`, dead code enquanto `useSimulator=true`)**: `EquipRandom()` chama `loadout.GetRandomWeapon()` — sorteio **uniforme** (não ponderado por `drawChance`; nunca foi atualizado pra usar o campo, já que esse caminho não roda de verdade). Se o loadout estiver vazio, `GetRandomWeapon()` retorna null e `EquipRandom()`/`EquipNext()` chamam `Unequip()` (limpando `CurrentWeaponData` e disparando `OnWeaponChanged(null)`) em vez de destruir a arma visual sem atualizar esse estado — bug antigo deixava o ícone da `WeaponHUD` destacado em amarelo enquanto o personagem batia desarmado.

### Draw Chance
`WeaponData.drawChance` (ex: Shuriken T1/T2/T3 = 33%/41%/47%, valores originais do My Brute) — verificado a pedido do usuário se estava influenciando alguma rolagem por turno: **não estava** (única ocorrência no código era a própria declaração do campo). O modelo do jogo é diferente do My Brute original desde o início (uma vez armado, a arma fica equipada até trocar/ser desarmada — não há re-rolagem "luta desarmado apesar de armado" a cada turno), então em vez de replicar o modelo original 1:1 (o que colidiria com hitSpeed/WeaponSwap/bumerangue já implementados), `drawChance` foi conectado como o **peso do sorteio de qual arma é puxada** no Pickup/WeaponSwap (`PickWeaponByDrawChance`, ver acima), preservando o resto do fluxo de turno inalterado.
`PlayCatchWeapon()` chama `ResetTrigger("Hurt")` antes de disparar o trigger para evitar que Hurt enfileirado de um turno anterior interfira.

`CombatPlayer.ExecuteEvent`'s case `TurnStart` espera `0.1s * t` (era `yield return null`, só 1 frame) antes de processar o resto do turno (incluindo `PickupWeapon`/`CatchWeapon`). A transição "Idle → Catch Weapon" no Animator Controller só existe a partir do estado Idle especificamente (não AnyState — ver Animator Controller Architecture abaixo). Em ações extras por velocidade, o `TurnEnd` do turno anterior chama `SetIdle(true)` e o próximo `TurnStart` do mesmo personagem rodava só 1 frame depois — sem tempo do Animator concluir de fato a transição pro estado Idle antes do trigger `CatchWeapon` ser setado, deixando o trigger pendente até o Animator entrar em Idle (que podia acontecer só depois do run/ataque já ter começado, parecendo a animação de pegar arma tocando no fim do turno). Hipótese de causa, não confirmada visualmente — se persistir, verificar a duração de blend da transição `* → Idle` no `.controller` do personagem.

### Unarmed Combat
When `CurrentWeapon == null`, `HitRoutine` uses the `"Slashing"` trigger (punch) with `weaponBaseDamage = UnarmedStats.Damage = 5`, fed into the multiplicative damage formula (see above). Slash animation speed = `hitSpeed × UnarmedStats.HitSpeed (1.0)`, reset to `1f` afterward (all exit paths including dodge/block).
`ComboChance()` returns 5% base while unarmed, plus AGI bonus and `UnarmedStats.ComboBonus` (0), com o mesmo clamp (60%) e decaimento (×0.5 por hit extra consecutivo). Ver seção **Combo** acima.

### STR Attribute
`PlayerCombat.str` (default 10) soma direto (flat) no `weaponBaseDamage`, não como multiplicador percentual — `(weaponBaseDamage + str)`. Era `weaponBaseDamage × (1 + str/10)` (percentual), redefinida pelo usuário pra alinhar com o My Brute original (STR contribui dano fixo adicional por ponto). Ver **Fórmula de Dano** acima para a fórmula completa (`(weaponBaseDamage + str) × critMultiplier × sharpMult × (1 - defenderArmor)`).

`weaponBaseDamage` (`RollWeaponDamage`) = `weaponData.damage` direto (valor fixo do asset, sem range aleatório nem dependência de tag), ou `3` se ≤0. Unarmed (punch): `UnarmedStats.Damage = 5`.

Throw damage **soma STR** (`weaponBaseDamage + str`, mesmo componente aditivo do golpe normal, sem `critMultiplier`/`sharpMult`) — era só `weaponBaseDamage`, sem STR; bug reportado pelo usuário, redefinida.

> Future skill **Iron Fist**: increases unarmed damage.

### Desarmar
`DisarmChance()` baseado nas tags da arma do **atacante** (soma por tag, ver tabela em **Tipos de Arma** acima; desarmado = 0).

Soma-se ainda o `weaponData.disarmBonus` do atacante (ou `UnarmedStats.DisarmBonus = +5%` se desarmado — soco também pode desarmar).

> Future skill **Impact**: +15% disarm permanente.

Só trigga no **primeiro hit do turno** (`isCombo = false`). `HitRoutine(isCombo)` recebe o flag; `ComboStrikeRoutine` passa `isCombo: true`. Ordem: depois do dano normal — o defensor ainda toma Hurt + knockback + dano normalmente antes de perder a arma.

**`DropWeapon(target, isDisarm)`** — `public static` em `PlayerCombat` (não lê estado de instância, só o de `target`) para poder ser chamado tanto pelo caminho legado quanto por `CombatPlayer.ExecuteEvent` nos casos `Disarm`/`WeaponDrop` (mesmo padrão do `FlyWeapon`, feito `public` para o `ThrowWeapon`). Antes, `CombatPlayer` só mostrava o popup e chamava `UnequipPermanent()` direto nesses dois casos — sem nenhum visual de queda; agora chama `StartCoroutine(PlayerCombat.DropWeapon(...))` (fire-and-forget, roda em paralelo, igual ao caminho legado original):
1. Captura `CurrentWeaponData` (sprite, scale) e posição do `CurrentWeapon` antes de chamar `UnequipPermanent()`.
2. Chama `target.weaponHandler.UnequipPermanent()` — arma removida permanentemente do loadout.
3. Spawna popup "DISARM!" em laranja acima do defensor.
4. Cria `FallenWeapon` GameObject com `SpriteRenderer` na layer **Default** (sorting order **1** — sempre atrás de todos os personagens/armas, mas na frente do fundo da arena).
5. Animação de **pêndulo amortecido** durante a queda: `θ(t) = θ₀ × e^(-γt) × cos(ωt)` com θ₀ aleatório 60°–100°, ω = 10 rad/s, γ = 0.8.
6. Queda com gravidade (9.8f) até `groundY = target.y - 1.5f`; snappa ao chão ao parar.
7. Objeto **não é destruído** — fica no chão pelo resto da luta.

Armas caídas são rastreadas na lista estática `PlayerCombat.fallenWeapons`. `CleanupFallenWeapons()` é chamado por `AttackSequencer.OnCombatEnd` ao declarar o vencedor, destruindo todos os objetos e limpando a lista. Nenhum personagem pode pegar a arma caída — ela é puramente visual.

### Desarmar do Escudo (Shield)

Duas formas do escudo cair — **`ShieldDisarm`** (ao sofrer um hit normal, depois do dano, só no **primeiro hit do turno**, `isCombo = false`, mesmo ponto onde o `Desarmar` de arma é checado) e **`ShieldDrop`** (mesmo num **bloqueio bem-sucedido**, sem dano — ver **Drop de arma/escudo ao bloquear** na seção **Block** acima).

Em ambos os pontos, **o escudo tem prioridade sobre a arma e são mutuamente exclusivos no mesmo hit**: enquanto `defender.hasShield` for true, só o escudo corre risco de cair — a arma do defensor só passa a poder ser desarmada/cair depois que o escudo já tiver caído (no próprio hit anterior ou num bloqueio anterior). Antes as duas chances eram roladas de forma totalmente independente (podiam cair os dois no mesmo hit) — redefinido pelo usuário pra bater com o My Brute original, onde o escudo protege a arma por baixo dele.

`ShieldDisarmChance = 0.10f` — constante fixa, **não soma** `attacker.disarmChanceBonus` (Shock) nem `weaponData.disarmBonus` do atacante, e a futura skill **Impact** (+15% disarm) também não deve aumentar essa chance — diferente de `DisarmChance()`, que soma todos esses termos pro desarme de arma normal. Mesma constante usada nos dois pontos acima.

Quando qualquer um dos dois trigga: `defender.hasShield = false`, reverte os dois bônus que a skill Shield concedia (`blockBonus -= 0.45`, `armor -= 0.25`). `ShieldDisarm` emite com `playerIndex` = atacante, `targetIndex` = defensor (mesma convenção de `Disarm`); `ShieldDrop` emite só com `playerIndex` = defensor que solta o próprio escudo (mesma convenção de `WeaponDrop`, sem `targetIndex`).

`PlayerCombat.DropShield(target, isDisarm)` — mesma queda em pêndulo amortecido de `DropWeapon` (ver **Desarmar** acima), mas lendo `CurrentShieldData`/`CurrentShield` e chamando `RemoveShield()` em vez de `UnequipPermanent()` (o escudo nunca esteve no `WeaponLoadout`). `isDisarm` escolhe o popup: `true` (`ShieldDisarm`) → "DISARM!" laranja; `false` (`ShieldDrop`) → "DROP!" laranja — mesma distinção de `DropWeapon`. `CombatPlayer.ExecuteEvent` chama `StartCoroutine(PlayerCombat.DropShield(...))` nos dois cases, em vez do antigo `RemoveShield()` direto sem queda visual.


### Pets (Fase 3)
Documentação completa em PETS.md.
Antes de alterar pets, leia PETS.md.
Pets: Rato (Mouse), Macaco (Monkey), Javali (Boar).
Componentes: PetCombatController, PetAnimationController,
HealthBarPet. Stats em PetState.cs. Prefabs:
Assets/Data/UI/Pets/<nome>/Pet.prefab.
Setup: Tools → AutoArms → Setup Pet Animators.

### Entry Drop (Entrada em Cena)
Ao carregar `04_CombatScenePVP`, ambos os personagens aparecem 12 unidades acima de sua `spawnPosition` (fora da câmera) e caem simultaneamente com gravidade (28f) antes do combate começar.

**Fluxo em `CombatSceneLoader.Initialize()`:**
1. Toda a configuração (instanciar Player1, wiring, health, HUD) ocorre normalmente.
2. `yield return null` — garante que `PlayerCombat.Start()` rodou e `spawnPosition` foi definido.
3. Captura posições de pouso (`p1Land`, `p2Land`) dos dois objetos.
4. Move ambos para `landPos.y + 12f` (céu).
5. `StartCoroutine(EntryFall)` para os dois em paralelo; aguarda via callbacks `bool`.
6. Só após ambos pousarem: `attackSequencer.player1 = player1Combat` → desbloqueia o loop de combate.

**`EntryFall(obj, landPos, onLand)`:**
- Queda com aceleração gravitacional (`gravity = 28f`), velocityY parte de 0.
- Ao atingir `landPos.y`: snappa posição e aplica squash de impacto (`scale X × 1.4, Y × 0.55`) interpolado de volta ao scale normal em 0.12s.
- Animação durante a queda: Idle (já ativo por `PlayerCombat.Start()`).

### Knockback
Every hit (including combo) pushes the defender by `settings.knockbackDistance` in the direction away from the attacker, over `settings.hurtDuration`. Fired via `StartCoroutine` on the defender so it runs in parallel with `PlayHurt`.

**Limite da janela jogável**: `PlayerCombat.ClampToArena(pos)` (privado, estático) clampa `X` em `[-7.25, 7.25]` e `Y` em `[-3.90, -0.81]` — mesmos valores de `RandomSpawnPosition` (área visível da câmera). Aplicado no destino calculado por `Knockback()` e `DodgeLeap()` antes de mover o personagem. Sem isso, combos longos com vários hits/esquivas seguidas empurravam o personagem cada vez mais na mesma direção a cada evento, eventualmente saindo da área visível da câmera (sem limitador algum antes).

### Speed System
Speed determina quantas vezes um personagem age por round via acúmulo de debt. Implementado em `AttackSequencer.CombatLoop`.

**Algoritmo por round:**
1. `p1SpeedDebt += player1.speed` | `p2SpeedDebt += player2.speed`
2. Enquanto `p1SpeedDebt >= player2.speed`: p1 age mais 1x, `p1SpeedDebt -= player2.speed`
3. Enquanto `p2SpeedDebt >= player1.speed`: p2 age mais 1x, `p2SpeedDebt -= player1.speed`
4. Mínimo garantido: 1 ação por player por round — **exceto se `speed <= 0`** (ex: Deity, -90% speed — ainda chega a 0 quando o speed base é baixo o suficiente pra arredondar pra zero): nesse caso 0 ações garantidas, o personagem nunca corre/ataca/pega arma por conta própria no round, só reage via Counter/Reversal nos turnos do oponente (`CombatSimulator.SimulateRound`, era `Mathf.Max(1, pXAct)` incondicional — forçava o personagem a atacar normalmente todo round mesmo com speed efetivo 0).

**Exemplos:**
- Speed 6 vs 2 → Round 1: P1 age 3x (6/2=3), P2 age 1x (2<6)
- Speed 4 vs 3 → Maioria dos rounds 1x cada; a cada ~4 rounds P1 age 2x

**Visual:** popup "RAPIDO!" amarelo aparece no início de cada ação extra (2ª em diante).

`CombatSimulator.SimulateRound` agora segue o mesmo modelo de **bloco** que `AttackSequencer.CombatLoop` ("primeiro jogador executa TODAS as suas ações do round, só então o segundo jogador age") em vez de intercalar ação-a-ação (1ª de cada, depois 2ª de cada...). O modelo intercalado tinha dois problemas:
1. Emitia o evento `SpeedBonus` em bloco (`extraActions = pXAct - 1`) antes de qualquer ação do round, fazendo o popup aparecer junto da 1ª ação normal.
2. Mesmo depois de corrigir (1) para emitir por ação, quando o jogador mais rápido também tinha iniciativa pra agir primeiro no round seguinte, sua última ação extra de um round ficava "colada" (sem nada no meio) à 1ª ação normal do round seguinte — visualmente parecia uma 2ª ação extra sem nenhum aviso, já que o intercalado só garante popup quando o índice da ação é > 0 *dentro do mesmo round*.

Com o modelo de bloco, o segundo jogador sempre age por último em cada round, então o primeiro jogador nunca emenda duas ações suas atravessando um round sem alguém no meio — `EmitSpeedBonus` continua sendo chamado só quando `i > 0`, mas agora isso cobre exatamente os casos certos.

Initiative ainda determina quem age PRIMEIRO no round (maior initiative = `first`). Speed determina quantas vezes cada um age.

## CombatSimulator Architecture

Pre-calculation system that computes the full fight outcome before any animation plays. Enables instant replay, 2x speed, and future web/mobile server-side validation.

### Files
| File | Type | Purpose |
|---|---|---|
| `Assets/Scripts/Combat/CombatEvent.cs` | Pure C# | Data class + `CombatEventType` enum — one event per game action |
| `Assets/Scripts/Combat/PlayerState.cs` | Pure C# | Mutable snapshot of one combatant during simulation |
| `Assets/Scripts/Combat/CombatSimulator.cs` | Pure C# | Pre-calculation engine; mirrors PlayerCombat/AttackSequencer logic |
| `Assets/Scripts/Combat/CombatPlayer.cs` | MonoBehaviour | Reads the event list and drives existing animation components |
| `Assets/Scripts/Combat/CombatLogFormatter.cs` | Pure C# | `Format(p1Name, p2Name, events)` — builds the readable pre-combat log string (see Logging Policy) |

### CombatSimulator.Simulate(p1Profile, p2Profile, seed)
Returns `List<CombatEvent>`. Optional `seed` makes the fight deterministic (replay / server-side validation).

Internally:
1. Builds `PlayerState` from both `PlayerProfile` objects
2. Calls `ApplySkillStats` on each (mirrors `CombatSceneLoader.ApplySkillStats`)
3. Runs `SimulateRound` in a loop (max 300 rounds, same speed-debt logic as `AttackSequencer`)
4. Each round calls `SimulateTurn(attacker, defender)` → `SimulateHit` / `SimulateThrow`
5. All outcomes (dodge, block, crit, disarm, throw, etc.) are resolved with `System.Random`
6. Emits one `CombatEvent` per discrete action; final event is `CombatEnd`

### CombatPlayer
Coroutine-based replay of the event list. On each event, drives existing components:
- `animationController.PlayRun/PlayCatchWeapon/PlayBlock/PlayHurt/PlayJumpStart`
- `weaponHandler.EquipSpecific/EquipRandom/Unequip/UnequipPermanent` — `PickupWeapon`/`WeaponEquipped` usam `EquipSpecific` com a `WeaponData` resolvida por `evt.weaponName` (helper `FindWeaponByName`, busca em `weaponHandler.loadout.Weapons`), não mais `EquipRandom()`. Esse sorteava de novo, podendo equipar visualmente uma arma diferente da que o `CombatSimulator` já tinha sorteado e usado no cálculo de dano daquele evento — a arma na mão não correspondia ao tipo/dano real do hit.
- `healthSystem.TakeDamage(delta)` — delta computed from event `newHp` vs current HP
- `DamagePopup.Spawn/SpawnDodge/SpawnBlock/SpawnDisarm/SpawnDrop/SpawnMiss/SpawnRapido`
- `sequencer.OnCombatEnd(winner)` — triggers XP/result panel

### Integration in CombatSceneLoader
Two Inspector fields on `CombatSceneLoader`:
- `player2Profile` (PlayerProfile) — Medieval Warrior Girl's profile, enables the simulator. Wired directly on the `CombatSceneLoader` component in `04_CombatScenePVP` (`guid: fcb3d4326a2a4f14b9f5de165814a1c6`). If left unassigned, `LoadPlayer2ProfileFallback()` loads it by path (`Assets/ScriptableObjects/PlayerProfiles/Medieval Warrior Girl.asset`) via `AssetDatabase` — **editor-only**, logs `Debug.LogError` and stays null in a build, so the shipped scene must have `player2Profile` assigned in the Inspector.
- `useSimulator` (bool, **default true**) — set to false to fall back to the original `AttackSequencer` coroutine loop.
- `player2MaxHealth` (int) — only used as a fallback when `useSimulator` is false or `player2Profile` is unassigned. When the simulator path is active, `health2` is initialized from `player2Profile.maxHealth` directly (resolved — including the editor fallback — *before* `health2.Initialize(...)` runs), since `CombatSimulator.BuildState` computes Player2's entire HP off that same number. These two values previously could drift apart (e.g. profile at 70, `player2MaxHealth` field at 50), which desynced `HealthSystem.CurrentHealth` from the simulator's internal HP and made `ApplyHealthChanged`'s delta calculation produce wrong damage from the first hit onward.

`player1Combat.skills` is now also assigned from `profile.skills` (copied into a new `List<SkillData>`) right alongside the other stat fields (`str`, `agility`, etc.) — it was the one field missing from that block. Without it, `CombatSceneLoader.ApplySkillStats(player1Combat, profile.maxHealth)` (which drives the visual `health1`) silently ignored every one of Player1's skills, since `combat.HasSkill(...)` checks the live component's own (always-empty) `skills` list — while `CombatSimulator.BuildState`/`ApplySkillStats(PlayerState)` correctly reads `profile.skills` for the simulation. A profile with a maxHealth-affecting skill (e.g. Immortal, +100) would simulate with the bonus (combat log shows the inflated max) while `health1` displayed and accumulated damage against the un-bonused number — once cumulative damage passed the smaller real max, the bar clamped to 0 and `HealthSystem.TakeDamage`'s `if (IsDead) return;` froze it there for the rest of the fight, even though the simulator (and the attacker performing the killing blow) never considered that player dead.

When both are set, after EntryFall: `attackSequencer.player1Profile = profile` is assigned (so `OnCombatEnd` can still award XP / show `CombatResultPanel` even though `attackSequencer.player1` is never set), then the simulator runs instead of the coroutine loop. The `AttackSequencer` stays idle (its `WaitUntil` never resolves) — `TriggerCombatEndRoutine` (coroutine) in `CombatPlayer` plays the loser's Dying animation, freezes the Animator, then calls `sequencer.OnCombatEnd(winner)` once `CombatEnd` is reached. `CombatHUD.AddSpeedControls(player)` creates a speed-toggle button and a **Skip** button in the bottom-center of the screen.

**Speed toggle button:** `CombatPlayer.ToggleSpeed()` flips between `_playbackSpeed = 1f`/`1.5f` (tracked by `_is2x`) and returns the new state. `CombatHUD.MakeSpeedToggleButton` reacts to that return value: label "1x" / dark gray background / white text normally → label "1.5x" / gold background / black text when accelerated (and back). No separate "set to 1x" button — clicking it again toggles back. The combat scene has no `EventSystem` of its own (only `01_MainMenu`/`02_SelectCharacter` do), so `CombatHUD.Initialize` calls `EnsureEventSystem()` to create one at runtime — without it, none of the HUD buttons receive clicks.

**Hit event animation timing (`CombatPlayer.ExecuteEvent`, `CombatEventType.Hit`):** waits `slashHalf` (half of `slashingDuration`) before applying knockback/hurt/damage popup, then waits `slashHalf` again afterward so the attacker's slash clip always finishes before anything can re-trigger it. Without that second wait, combo hits (consecutive `Hit` events with no `TurnEnd` between them) retriggered the `Slashing`/`SlashingDagger` Animator trigger mid-clip, snapping/restarting the animation instead of playing it through.

At the impact moment (after the first `slashHalf` wait), `HealthSystem.TakeDamage(evt.damage)` is called directly off the `Hit` event, in the same breath as the damage popup and the knockback/hurt animations — not waiting for the separate `HealthChanged` event later in the list. The standalone `HealthChanged` case (`ApplyHealthChanged`) still runs when reached, but is now a no-op for normal playback since the delta against `CurrentHealth` is already 0; it still matters for the `Skip` fast-forward path, which never goes through `Hit` at all and applies every remaining `HealthChanged` directly.

`Dodge` and `Block` now also trigger the attacker's `Slashing`/`SlashingDagger` swing and wait `slashHalf` before the defender's reaction (mirroring `Hit`) — previously these two events only animated the defender, with no attacker swing at all, so a dodge/block looked like the defender randomly leaping/blocking nothing. Both also wait the trailing `slashHalf` afterward, same as `Hit`, to protect against combo retrigger.

`CombatEvent.isThrow` (set on the `Hit` emitted by `CombatSimulator.SimulateThrow`) tells `CombatPlayer` to skip the melee swing trigger and the `slashHalf` waits for that hit — the attacker already has no weapon in hand (just unequipped it in the `ThrowWeapon` event) and already did the "windup" during the projectile's flight, so the impact (damage/popup/hurt) applies immediately when the `Hit` event starts, synced with the moment the thrown weapon visually reaches the defender. `Miss` (only ever emitted after a throw) was already immediate and needed no change.

**`RepositionIfNeeded(attacker, defender, t)`:** chamado no início dos casos `Hit` (quando `!evt.isThrow`), `Dodge` e `Block`, antes do swing trigger — mirrors `ComboStrikeRoutine.AttackPosition()`/reposicionamento do caminho legado (ver Combo Architecture acima), que nunca tinha sido portado pro `CombatPlayer`. Recalcula `CalcAttackPosition(attacker, defender)` e roda `PlayRun` se a distância atual for > 0.3 unidades; no-op na 1ª ação do turno (atacante já está no lugar certo por causa do `RunToDefender`). Sem isso, um combo hit/dodge/block depois de uma esquiva ou knockback anterior (que empurrou o defensor mais longe) acontecia com o atacante parado fora de alcance — o defensor levava um knockback/dodge sem nenhum swing visível por perto, parecendo um pulo "do nada" sem ação alguma.

`comboDelay` (0.15s):** added after every `Hit`/`Dodge`/`Block`/`Miss` event in `CombatPlayer.ExecuteEvent`, scaled by the speed-toggle's `t`. A combo turn (e.g. hit→dodge→hit→dodge→hit, all part of one attacker's combo loop in `CombatSimulator.SimulateTurn`) had zero gap between consecutive actions before this — each action's own animation timing ran back-to-back with nothing in between, so a 6-action combo blurred together and felt like only 2-3 distinguishable actions happened, even though every event individually played out and dealt/avoided damage correctly. `interTurnDelay` only applies *between* different turns/attackers, not between actions within the same attacker's combo.

`CombatSimulator.Simulate()` logs `[CombatSimulator] Iniciando simulação...` on entry and `[CombatSimulator] {n} eventos gerados` on exit — exceptions to the no-stray-logs rule (see Logging Policy), kept as permanent confirmation that the simulator actually ran.

### CombatEventType values
`TurnStart, RunToDefender, ThrowWeapon, PickupWeapon, WeaponSwap, Thief, Hit, Counter, Reversal, Dodge, Block, Miss, Disarm, WeaponDrop, ShieldDisarm, ShieldDrop, HealthChanged, SpeedBonus, Stunned, StunSkip, Saboteur, VampirismAttack, ChefPizzaThrow, PoisonDamage, TurnEnd, CombatEnd`

### Key fields in CombatEvent
| Field | Used by |
|---|---|
| `playerIndex` | always set — index of the acting/affected player (0=P1, 1=P2) |
| `targetIndex` | defender or disarmed player |
| `damage`, `isCrit`, `isCombo` | Hit event |
| `newHp`, `maxHp` | HealthChanged event |
| `weaponName` | ThrowWeapon, PickupWeapon, WeaponSwap, Thief, Disarm, WeaponDrop |
| `extraActions` | SpeedBonus (for RAPIDO! count) |

**ThrowWeapon visual:** `CombatPlayer.ExecuteEvent` spawns a `FlyingWeapon` GameObject (SpriteRenderer using `attacker.weaponHandler.CurrentWeaponData.inHandSprite`, captured before unequip destroys the in-hand object) and flies it from the hand bone to the defender's position via `PlayerCombat.FlyWeapon` (now `public`, shared with the legacy `ThrowRoutine`). Originally this case only called `Unequip()` + a `WaitForSeconds(0.45s)` with no projectile at all — the weapon just vanished with nothing visibly thrown. It also mirrors `CombatSimulator.SimulateThrow`'s loadout handling: non-`Thrown`-type weapons call `UnequipPermanent()` (so the icon disappears from `WeaponHUD` too, matching the simulator removing it from `weaponLoadout`), while `Thrown`-type weapons just `Unequip()` since they can be re-equipped later (next time the runtime loadout is rolled — não há mais um re-equip imediato de 40% após o arremesso, removido; ver **Throw Weapon**).

**Bug corrigido (Animator preso em `Throwing` entre arremessos do mesmo turno)**: este case fazia `SetTrigger("Throwing")` mas nunca chamava `animationController.SetIdle(true)` depois do voo da arma — só o `TurnEnd`, bem mais tarde, fazia isso (`Throwing → Idle` exige `Idle=true`, `CanTransitionToSelf=0` na transição de entrada, ver **Animator Controller Architecture**). Funcionava enquanto só existia 1 `ThrowWeapon` por turno, mas com o combo de arremessos da Hideaway (`SimulateHideawayThrowCombo`, múltiplos `ThrowWeapon` no mesmo turno, sem `TurnEnd` entre eles) o 2º `SetTrigger("Throwing")` disparava enquanto o Animator ainda estava preso no próprio estado `Throwing` do arremesso anterior — sem transição válida pra consumir o trigger, o personagem tremia entre poses (bug real reportado pelo usuário, "parece que está com parkinson"). Corrigido chamando `SetIdle(false)` antes do `SetTrigger` (mesmo padrão de `PlayCatchWeapon`) e `SetIdle(true)` depois do voo da arma, a cada `ThrowWeapon` — não só no `TurnEnd` final.


## Skill System
Documentação completa em SKILLS_SYSTEM.md / SKILLS_PASSIVE.md / SKILLS_ACTIVE.md.
Antes de implementar ou alterar qualquer skill, leia SKILLS_SYSTEM.md / SKILLS_PASSIVE.md / SKILLS_ACTIVE.md.
Arquitetura: SkillData ScriptableObject, SkillDatabase,
HasSkill(string) em PlayerCombat. Assets em
Assets/ScriptableObjects/Skills/. Gerador:
Tools → AutoArms → Generate Skill Assets.

## Logging Policy

O projeto não usa `Debug.Log`/`Debug.LogWarning` soltos pelo código — só `Debug.LogError` para falhas críticas de setup (ex: `CombatSceneLoader` sem `PlayerProfile`, `SkillDatabase` vazio em `CombatResultPanel`). Antes de adicionar um novo `Debug.Log`, prefira: (a) um `Debug.LogError` se for uma falha real, ou (b) nada — UI/popups já comunicam o resultado ao jogador.

Exceções (todas no caminho do `CombatSimulator`, quando `useSimulator=true`):
- `CombatSceneLoader.Initialize()` imprime **um** `Debug.Log(CombatLogFormatter.Format(...))` com o resumo completo da luta inteira, gerado depois de `CombatSimulator.Simulate()` e antes de `CombatPlayer.PlayCombat()` começar a tocar as animações — ver `CombatLogFormatter` abaixo.
- `CombatSimulator.Simulate()` loga `[CombatSimulator] Iniciando simulação...` na entrada e `[CombatSimulator] {n} eventos gerados` na saída — confirmação rápida de que o simulador rodou, sem precisar ler o log completo.
- `CombatSimulator.ApplySpySabotage` loga `[Spy] Armas sabotadas: {nome1}, {nome2} (-20% dano).` quando a skill **Spy** sabota pelo menos 1 arma do oponente — **não** é instrumentação temporária, pedido explícito do usuário para sempre aparecer no pré-combate (ver seção própria **Spy** em Combat Systems).

**Instrumentação temporária removida (2026-06-24, investigação de FPS drop)** — `[WeaponLoadout]` (`LogWeaponLoadout`, 4 armas × 2 jogadores = 8 linhas/luta), `[ComboChance]` (`SimulateComboLoop`, 1 linha por checagem de combo), `[SpeedBonus]` (`EmitSpeedBonus`) e `[CalcDamage]`/`[CalcThrowDamage]` (1 linha por hit/arremesso, literalmente todo hit da luta) — todas já estavam marcadas no código como "remover quando confirmado" desde a implementação original de cada mecânica, mas nunca tinham sido removidas de fato. Causavam dezenas de `Debug.Log` síncronos (cada um com captura de stack trace pro Console, intrinsicamente lento no Editor) concentrados em poucos frames — `CombatSimulator.Simulate()` roda a luta INTEIRA de uma vez, antes de qualquer animação tocar (ver arquitetura no CombatSimulator Architecture abaixo), então todas essas centenas de chamadas aconteciam basicamente no mesmo frame, exatamente quando a cena `04_CombatScenePVP` carrega — reportado pelo usuário como "fica estranho" especificamente ao entrar na cena de combate (MainMenu continuava normal, sem nenhuma chamada desse tipo). Removida também a referência a um log de `ApplySaboteur` que a doc antiga ainda citava aqui — esse log já tinha sido removido bem antes, junto da reescrita da mecânica de Saboteur pra "quebra a 1ª arma puxada" (ver seção própria **Saboteur** em Combat Systems — "Log pré-combate removido junto da mecânica antiga").

### CombatLogFormatter
`Assets/Scripts/Combat/CombatLogFormatter.cs` — `Format(p1Name, p2Name, List<CombatEvent>)` é puro C# (sem MonoBehaviour) e devolve uma string multi-linha legível: um cabeçalho com os dois nomes, uma linha `--- Turno de {nome} ---` por `TurnStart`, e uma linha por ação relevante (pickup/equip/throw/hit com dano+crit/combo+HP resultante/dodge/block/miss/disarm/drop/speed bonus), terminando em `========== VENCEDOR: {nome} ==========`. `Hit` consome o `HealthChanged` emparelhado (mesmo `targetIndex`, evento seguinte) para anexar o HP resultante na mesma linha. `RunToDefender` e `TurnEnd` não geram linha própria.

## Stats System

Todos os atributos são definidos em `PlayerProfile` (ScriptableObject) e copiados para `PlayerCombat` (runtime) por `CombatSceneLoader.Initialize()` antes do combate.

### Geração de stats no level 1

`Assets/Scripts/Utils/CharacterCreation.cs` — `CharacterCreation.GenerateLevel1Stats()`.

**Valores base:**
| Atributo | Base |
|---|---|
| maxHealth | 55 |
| str | 2 |
| agility | 2 |
| speed | 2 |

**Pool de 9 pontos** distribuídos aleatoriamente (25% cada atributo):
- HP: +5 por ponto
- STR: +1 por ponto
- AGI: +1 por ponto
- SPD: +1 por ponto

Sem limitador por atributo — toda a sorte pode cair em um único stat.

**Exemplos de resultado:**
- 9 em STR → HP 55, STR 11, AGI 2, SPD 2
- 9 em HP → HP 100, STR 2, AGI 2, SPD 2
- Distribuído → HP 70, STR 4, AGI 4, SPD 4

**Stats atuais dos personagens (aplicados via geração aleatória):**
| Personagem | HP | STR | AGI | SPD |
|---|---|---|---|---|
| Assassin Guy | 60 | 4 | 7 | 3 |
| Medieval Warrior | 65 | 7 | 3 | 3 |
| Medieval Warrior Girl | 50 | 3 | 3 | 3 |

Para re-sortear: **Tools → AutoArms → Randomize Level 1 Stats** (`Assets/Editor/CharacterCreationEditor.cs`). Só afeta profiles com `level == 1`.

**Tools → AutoArms → Reset All Profiles to Level 1** — além de `level`/`xpCurrent`/`xpRequired`/`battlesRemaining`, agora também: re-sorteia HP/STR/AGI/SPD via `CharacterCreation.GenerateLevel1Stats()` (mesma lógica do botão acima), limpa `profile.skills`, limpa `profile.pets` (bug corrigido — faltava desde a implementação dos pets; sem isso o profile voltava pro level 1 mas continuava com os pets antigos, e como `profile.maxHealth` já é sobrescrito direto pelo HP sorteado de `GenerateLevel1Stats()` — não descontado relativamente — o custo de HP de cada pet já saía implicitamente zerado mesmo sem essa linha, só os pets em si que ficavam presos), e reseta `profile.weaponLoadout.weapons` para as 4 armas iniciais (Satyr1, Golem3, Succubus, Zombie — guids hardcoded em `DefaultWeaponGuids`). Como cada profile tem seu próprio `WeaponLoadout` (ver tabela de ScriptableObject Assets acima), isso não afeta os outros personagens.

### Campos e defaults

| Campo | Tipo | Default | Onde é usado |
|---|---|---|---|
| `str` | int | 10 | Soma flat no dano base: `(weaponBaseDamage + str) × critMultiplier × sharpMult × (1 − defenderArmor)` (ver Combat Systems → Fórmula de Dano) |
| `agility` | int | 10 | `DodgeChance()`: +2%/ponto acima de 3, teto 60%; `ComboChance()`: +0.8%/ponto acima de 3, teto total 60% |
| `speed` | int | 10 | `AttackSequencer.CombatLoop`: acumula debt a cada round; debt >= speed do oponente = ação extra (ver Speed System) |
| `armor` | float | 0 | Fator `(1 − armor)` na fórmula multiplicativa de dano |
| `evasion` | float | 0 | `DodgeChance()`: adicionado à chance base |
| `accuracy` | float | 0 | `DodgeChance()` (do atacante): subtraído da chance de esquiva do defensor — oposto de `evasion` (Relentless +0.30) |
| `initiative` | int | 0 | `AttackSequencer.StartWhenReady`: quem tem mais initiative ataca primeiro |
| `reversal` | float | 0 | future: chance de reverter a iniciativa |
| `counter` | float | 0 | `BlockChance()`: adicionado à chance base do defensor |
| `criticalChance` | float | 0 | `CritChance()`: adicionado à chance base do atacante |
| `hitSpeed` | float | 1 | `HitRoutine`: velocidade da animação de slash (`slashSpeed = hitSpeed × weaponData.hitSpeed`, ver tabela de Propriedades das Armas) |
| `comboChanceBonus` | float | 0 | `ComboChance()`: adicionado à chance base |
| `runSpeedMultiplier` | float | 1 | `RuntimeRunSpeed = settings.runSpeed × runSpeedMultiplier` |

### Campos de estado (runtime, não persistidos no perfil)

| Campo | Tipo | Propósito |
|---|---|---|
| `leadSkeleton` | bool | Se `true`, reduz 15% do dano de armas Heavy recebidas |
| `firstHitAvoided` | bool | Se `true`, o primeiro golpe da luta é automaticamente esquivado (Ballet Shoes) |


*Skills que modificam stats: ver SKILLS_SYSTEM.md / SKILLS_PASSIVE.md / SKILLS_ACTIVE.md.*

## Third-Party Plugins

- **Spriter2UnityDX** (`Assets/Spriter2UnityDX/`) — Converts Spriter `.scml` files to Unity prefabs/animators. Character prefabs use its `EntityRenderer` and `TextureController` runtime components.
- **TextMesh Pro** — Used throughout UI; assets in `Assets/TextMesh Pro/`.

## Assets
- **CraftPix.net** — todos os assets visuais do jogo (personagens, ícones, backgrounds, GUI) foram adquiridos com licença comercial. Licença permite: uso comercial, modificação, distribuição em jogos. Proibido: revender arquivos fonte, usar para treinar IA. Referência: https://craftpix.net/file-licenses/

## Game Vision
Ver VISION.md — contexto de design, não necessário para implementação.

## Skills Reference

Referência jogável: https://brute.eternaltwin.org
Repositório open source LaBrute (estudar lógica apenas, não copiar — licença PolyForm Noncommercial): https://github.com/Zenoo/labrute
Lista completa das 53 skills com stats e odds: ver imagem salva em `C:\Users\user\Desktop\Prototipo\Skills_My_Brute.png`
Skills implementadas: ver Roadmap de Skills (Fase 2.5) abaixo.

### Assets de Skills
- Ícones mapeados (estado original, antes da pasta ser esvaziada): 38 em `Assets/Data/UI/Skills/` com prefixo numérico (01_ a 38_)
- Padrão de nome: `skill_<nome>.png`
- Skills sem ícone (lista original, pré-esvaziamento): vitality, immortality, reconnaissance, deity, martial_arts, shock, resistant, toughened_skin, sabotage, lead_skeleton, determination, bandage, strong_arm, master_of_arms, saboteur, spy, hideaway, backup, piledriver, chef, monk, vampirism, treat, chaining, haste, mimic, fast_metabolism, repulse, sticky_hands
- **Estado atual** (ver "Testando skills uma a uma" em Combat Systems → Pegar Arma... não, ver seção **Stats System → Skills que modificam stats** acima): a pasta foi esvaziada e está sendo repovoada uma skill por vez, sem o prefixo numérico — `vitality`, `immortality` e `reconnaissance` (listadas acima como "sem ícone" no estado original) já têm ícone de volta (`skill_vitality.png`, `skill_immortality.png`, `skill_reconnaissance.png`), assim como `herculean_strength` e `feline_agility` (que já tinham ícone no set original).


*Roadmap de implementação de Skills: ver SKILLS_SYSTEM.md / SKILLS_PASSIVE.md / SKILLS_ACTIVE.md.*

## Roadmap

### Como atualizar
Ao concluir uma tarefa, troque [ ] por [x] e atualize o contador em Progresso.

### Fase 0 — UI Básica de Personagem
- [x] HP numbers (HP_ATUAL/HP_MAX) nas barras de vida do combate via TMP_Text
- [x] Painel de personagem slide-in pela direita na MainMenu (3 abas: Stats / Skills / Armas)
- [x] HUD permanente abaixo do personagem na MainMenu (nome + Level, barra de XP, 3 ícones de skill)

### Fase 1 — Interface & Personagens
- [ ] Melhorar interface da página inicial
- [ ] Melhorar interface da tela de escolha de personagens
- [ ] Criação do primeiro personagem masculino e feminino
- [ ] **Tutorial das primeiras batalhas**: fluxo guiado pro jogador novo logo após criar o primeiro personagem — explica o loop básico (combate automático, XP, level up, escolha de bônus) durante as primeiras lutas, antes de soltar o jogador sem contexto na tela principal.
- [ ] Adicionar novos prefabs de personagens guardados
- [ ] Atributos aleatórios ao criar personagem level 1 (vida, força, agilidade, velocidade)
- [ ] Habilidades inspiradas no My Brute
- [ ] Criar habilidades originais adicionais
- [ ] **Arte chibi + retrato realista do personagem**: ao criar o personagem, ter duas versões visuais — o boneco chibi (estilo atual usado em combate/seleção) na frente, e uma versão mais realista do mesmo personagem ao fundo. Pedir ajuda a alguma IA de geração de imagem pra gerar essas duas versões e definir/separar o estilo de cada uma.

### Fase 2 — Combate Robusto
- [x] Barra de vida com dano baseado em status + dano da arma
- [x] Números de dano flutuantes com TextMesh Pro
- [x] Animação de hit ao tomar dano (defensor permanece no lugar)
- [x] Combo: atacante executa Slash adicional sem Run, sem limite de hits
- [x] Crítico: 5% base, Dagger 8%, Sword 5%, Heavy 3% — dano × 2
- [x] Esquiva: chance de desviar baseada em agilidade
- [x] Parry: chance de bloquear dano com arma ou escudo
- [x] Jogar arma: arremessar a arma no adversário
- [x] Pegar arma: começar desarmado e pegar arma aleatória (40% chance) no início do turno com animação CatchWeapon
- [x] Desarmar: fazer o adversário soltar a arma
- [x] Entrada em cena: personagens caem do céu ao iniciar combate
- [x] Drop de arma ao bloquear (atacante 15%, defensor 10%)
- [x] HUD de armas abaixo da barra de vida
- [x] Sistema de XP e level (vitória +2 XP, derrota +1 XP)
- [x] Curva de XP não linear: nível ≤6 usa level+4 (1→2=5...6→7=10, +1/nível), nível ≥7 usa 2×level-2 (7→8=12, 8→9=14..., +2/nível sem teto)
- [x] Tela de fim de combate com resultado e XP ganho
- [x] CombatSimulator: pré-cálculo determinístico de todo o combate (CombatEvent, PlayerState, CombatSimulator, CombatPlayer)
- [x] Botão de velocidade toggle (2x ↔ 1x, com cor indicando estado) e Skip no CombatHUD (controlam CombatPlayer)
- [x] Ao subir de nível: escolher atributo, skill ou arma

### Fase 3 — Armas & Pets
- [x] Criar mais armas com sprites e stats — 26 assets criados em `Assets/Data/Weapons/` com stats T1 completos (sprites pendentes)
- [ ] Sistema de raridade de armas
- [x] Pets: Rato (Mouse), Macaco (Monkey), Javali (Boar) — substituem o roster original (cachorro/lobo/águia/urso) do "Pets planejados" abaixo, que ficou desatualizado frente aos assets reais (Boar/Monkey/Mouse) já disponíveis em `Assets/Data/UI/Pets/`. Ver seção própria **Pets** em Combat Systems.


### Fases 4–9 (Monetização → Áudio)
Ver ROADMAP_FUTURO.md — não carregar nesta sessão.

### Progresso
- Total: 121 tarefas | Concluídas: 56
- Histórico completo em CHANGELOG.md
