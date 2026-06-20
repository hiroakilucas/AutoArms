# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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
| `WeaponLoadout` | `Assets/Data/Weapons/` | array of `WeaponData` slots. **Um asset por personagem** — `Loadout_AssasinGuy.asset`, `Loadout_MedievalWarrior.asset`, `Loadout_MedievalWarriorGirl.asset` (todos começam com as mesmas 4 armas: Satyr1, Golem3, Succubus, Zombie). Antes os 3 `PlayerProfile` apontavam para o mesmo `Loadout10Armas.asset` (ainda existe no projeto, sem uso) — qualquer arma ganha em level-up por um personagem vazava pra todos, já que `CombatResultPanel.ApplyBonus` muta `profile.weaponLoadout.weapons` diretamente. Separar os assets corrigiu isso. |
| `WeaponData` | `Assets/Data/Weapons/<type>/` | Name, in-hand sprite, damage, `WeaponType`, scale |

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
  ├── AnimationController      (Idle → Run → Slash → JumpStart → Hurt)
  ├── MovementController       (MoveTo linear, JumpTo parabolic arc)
  └── WeaponHandler            (equips next weapon from PlayerLoadout each round)
```

- `AttackSequencer` — Runs the indefinite turn loop; waits for both `PlayerCombat` references before starting. On combat end calls `OnCombatEnd(winner)`: awards XP (+2 win / +1 loss) via `XpSystem.AddXP`, decrements `player1Profile.battlesRemaining`, then spawns `CombatResultPanel`. Field `player1Profile` set by `CombatSceneLoader`. Fields `skillDatabase` (`Assets/ScriptableObjects/Skills/SkillDatabase.asset`) and `allWeapons` (array of all `WeaponData` assets eligible as level-up rewards) are wired directly on the `AttackSequencer` GameObject in `04_CombatScenePVP` — required for the level-up choice screen to offer skill/weapon options instead of only attributes.
  - `OnCombatEnd` determina vitória via `winner.isPlayer1` (não `winner == player1`) — o campo `AttackSequencer.player1` nunca é atribuído no caminho do simulador (`CombatSceneLoader` só seta `player1Profile`, de propósito, pra `StartWhenReady`/`CombatLoop` legado não rodarem em paralelo com o `CombatPlayer`). Comparar contra `player1` fazia `player1Won` ser sempre `false`, mostrando "DERROTA!" e dando XP de derrota mesmo quando P1 vencia. `isPlayer1` é setado por `CombatSceneLoader` (`player1Combat.isPlayer1 = true`; Player2 fica `false`) e funciona nos dois caminhos (simulador e legado).
- `XpSystem` — Static utility. `XpRequired(level) = (level+1)*(level+2)` (matches table: 1→2=6, 2→3=12…). `AddXP(profile, amount)` accumulates XP, triggers level-up when threshold reached, applies **+2 maxHealth only** (`ApplyLevelBonus`) — STR/AGI/SPD never increase automatically, they only grow via the level-up choice screen (see below) — calls `EditorUtility.SetDirty` to persist ScriptableObject changes in editor.
- `CombatResultPanel` — Screen-space overlay panel shown 0.8s after combat ends. Shows result title (gold/red), XP gained, animated blue XP bar, current XP / required, level, battles remaining, "Continuar" button (→ `01_MainMenu`). Level-up: bar animates to full, "LEVEL UP!" text pulses with sin-wave scale, bar resets to new level's progress, **then `ShowLevelUpChoice` always opens and blocks "Continuar" until a choice is made** (`continueBtn.interactable = !didLevelUp`). Ensures `EventSystem` exists; uses `GraphicRaycaster` on canvas (added to `CombatHUD.CreateCanvas`) for click detection. Overlay has `raycastTarget = false` so it doesn't block the button.
  - `ShowLevelUpChoice` draws 2 unique `LevelUpOption`s via `DrawOption`: weighted 60% Attribute (+8 HP / +2 STR / +2 AGI / +2 SPD, picked uniformly among the 4), 30% Skill (random `SkillData` from `skillDatabase` not already in `profile.skills`), 10% Weapon (random `WeaponData` from `allWeapons` not already in `profile.weaponLoadout`) — weights for Skill/Weapon drop to 0 if their pool is empty. Logs `[LevelUp] ERRO: SkillDatabase não encontrado ou vazio` if `skillDatabase` is null/empty. Picking a card calls `ApplyBonus` (mutates `profile` directly) then unblocks "Continuar".
    - **Modo teste (`ShowAllOptionsForTesting = true`, const em `CombatResultPanel.cs`)**: em vez de sortear 2 opções, chama `ShowAllOptionsChoice` — grade rolável (`ScrollRect` + `GridLayoutGroup`) com as opções disponíveis: 4 atributos + skills da `SkillDatabase` que (a) ainda não foram escolhidas e (b) têm `icon != null`. Reverter para o comportamento de 2 cartas: trocar essa const para `false`.
    - **Testando skills uma a uma**: todos os ícones foram removidos de `Assets/Data/UI/Skills/`; são re-adicionados um por vez conforme cada skill é testada (`skill_immortality.png` é a primeira). `ShowLevelUpChoice` filtra `availableSkills` por `s.icon != null` — só aparecem no level-up as skills cujo ícone já foi re-adicionado. **Armas estão temporariamente fora** da grade de teste (`foreach (var w in availableWeapons)` comentado em `ShowAllOptionsChoice`) — só atributos + skills disponíveis, por pedido do usuário enquanto o foco é testar skills. Reativar descomentando esse loop quando for testar armas de novo.
    - **Importante**: `SkillData.icon` é vinculado por GUID na geração (`Tools → AutoArms → Generate Skill Assets`, `Assets/Editor/SkillAssetGenerator.cs`). Se você apagar/recriar um PNG do zero (não restaurar o arquivo original), ele recebe um GUID novo e o `icon` salvo no `.asset` antigo fica apontando pra um GUID inexistente (resolve como `null` mesmo com o arquivo presente). O gerador é idempotente e re-resolve `skill.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath)` pra **todas** as 32 skills a cada execução (null se o PNG não existir) — rodar a ferramenta de novo depois de adicionar/remover qualquer ícone mantém os links corretos, sem precisar editar os `.asset` manualmente.
    - **`SkillDef.iconFileName`** (opcional, em `SkillAssetGenerator.cs`): nome do PNG em `Assets/Data/UI/Skills/`, se diferente de `fileName` (que também define o nome do `.asset` gerado — não pode ser trocado sem deixar um asset órfão). `Immortal` usa `fileName = "skill_immortal"` (asset existente) mas `iconFileName = "skill_immortality"`, porque o ícone re-adicionado pelo usuário segue o nome da skill na lista mestre original (`immortality`), não o nome abreviado do asset já implementado (`immortal`) — sem esse campo o gerador procurava `skill_immortal.png` (nunca existiu) e o ícone ficava sempre `null`, mesmo com `skill_immortality.png` presente na pasta e o gerador rodado. Mesmo padrão pra `Armour`: `fileName = "skill_armour"` (asset existente) + `iconFileName = "skill_armor"` (usuário re-adicionou o PNG com o nome sem "u" por engano da primeira vez).
- `CombatSceneLoader` — Agora usa coroutine (`Initialize()`): instancia Player1, aplica `profile.str`/`profile.agility` ao `PlayerCombat`, inicializa health/HUD, aguarda um frame (para `PlayerCombat.Start()` rodar), então executa entrada em cena (`EntryFall`) de ambos em paralelo. Só atribui `attackSequencer.player1` e `attackSequencer.player1Profile` após os dois pousarem. Atribui `player1`'s `PlayerLoadout.loadout = profile.weaponLoadout` e, simetricamente, `player2`'s `PlayerLoadout.loadout = player2Profile.weaponLoadout` (esse segundo não existia antes — não tinha efeito enquanto os profiles compartilhavam o mesmo asset, mas passou a ser necessário depois de cada personagem ganhar seu próprio `WeaponLoadout`; sem isso o `WeaponHUD` do Player2 mostraria o valor hardcoded na cena em vez do loadout real do perfil dela).
- `PlayerCombat` — Owns `AttackRoutine`. Manages sorting layer swaps so the attacker renders above the defender during a strike.
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
| `Colosseum arena` | Background/visual |
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

`Default < Weapons2 < Characters2 < Weapons < Characters` — attacker promoted to `Characters`/`Weapons`, defender demoted to `Characters2`/`Weapons2` during strike; restored after jump-back.

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
| `Slashing` | Trigger | Sword/default attack |
| `SlashingDagger` | Trigger | Dagger attack |
| `SlashingHeavy` | Trigger | Heavy weapon attack |
| `Blocking` | Trigger | Block reaction (defense pose) |
| `Throwing` | Trigger | Throw weapon animation |
| `CatchWeapon` | Trigger | Pick up weapon animation (início do turno) |

### Key States and Transitions (manually added — originals generated by Spriter2UnityDX)
- **Any State → Slashing/SlashingDagger/SlashingHeavy** *(combo)*: same conditions as Running→Slashing, `CanTransitionToSelf=1`. Allows re-entering Slashing from itself during combo chains.
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
| `SlashingHeavy` | `Slashing Heavy` |

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

`Assets/Scripts/Controller/WeaponData.cs`. O antigo enum exclusivo (`Sword/Heavy/Dagger/Fast/Slow/Thrown/Block`) foi substituído por um enum simples (`None, Sharp, Blunt, Long, Heavy, Fast, Thrown`) guardado numa **lista** (`public List<WeaponType> types`), não um `[Flags]` bitmask — o Inspector do Unity não tem como esconder os valores automáticos `None`/`Everything` que `[Flags]` gera no dropdown de máscara, então a lista é a forma de deixar o usuário adicionar manualmente cada tag (elemento 0, 1, 2...) sem lixo no dropdown. Uma arma pode ter até 3 tags simultâneas (ex: Halberd = `Long, Heavy, Sharp`, Trombone = `Heavy, Blunt`, mirrorando o My Brute original). `Sword` e `Dagger` se fundiram em `Sharp` — a diferença "adaga vs espada" agora vem de combinar `Sharp` com `Fast` (adaga) ou não (espada). `Slow` e `Block` foram removidos (`Slow` não tinha asset usando; `Block` não é um tipo de arma, era uma categoria antiga de shield). `WeaponData.HasType(flag)` (instance) / `WeaponData.HasType(data, flag)` (static, null-safe) são os helpers de leitura (`types.Contains(flag)`), usados em vez de `switch`/`==`; `OnValidate()` avisa no Console se `types.Count > 3` (não força/limpa automaticamente).

**Cada fórmula de chance abaixo soma os valores-base de todas as tags presentes na arma** (não pega o máximo nem usa mais um "default" genérico — cada bônus vem de uma tag específica; tags sem entrada na tabela contribuem 0):

| Fórmula | Sharp | Fast | Heavy | Thrown |
|---|---|---|---|---|
| `CritChance` base | 0.05 | 0.03 | 0.03 | 0 |
| `ComboChance` base | 0.12 | 0.03 | 0.04 | 0 |
| `DodgeChance` base (arma do defensor) | 0.10 | 0.05 | 0.05 | 0 |
| `DisarmChance` base | 0.10 | 0.10 | 0.05 | 0 |
| `BlockChance` base (arma do defensor) | 0.15 | 0.00 | 0.15 | 0 |
| `ThrowChance` | 0.15 | 0.00 | 0.10 | 1.00 |

`Blunt` e `Long` contribuem 0 em todas essas tabelas — `Blunt` só importa pra Lead Skeleton (-15% dano recebido, ver `WeaponData.IsBlunt`) e pra **não** receber o bônus de Weapon Master; `Long` não tem tabela própria — o bônus de Counter Rate/Reversal de cada arma "Long" é definido manualmente por asset (`reversalBonus`/skills futuras), não por uma constante global.

**Dano base não soma por tag** — `weaponData.damage` tem prioridade absoluta (`RollWeaponDamage(data) => data.damage > 0 ? data.damage : 3`), sem depender de Sharp/Heavy/Fast. Cada `WeaponData` configura seu próprio valor fixo no Inspector; não há mais range aleatório por tipo.

**Alcance não é soma pura** (somar distâncias inteiras por tag não faz sentido físico):
- **Alcance** (`AttackPosition`/`CalcAttackPosition`): `base = Heavy presente ? 2.8 : 2.0`, depois `-0.5` se Fast presente, depois soma o campo `weaponData.reach` (inalterado). Ex: Sharp só = 2.0; Sharp+Fast = 1.5; Heavy só = 2.8.
- **Trigger de animação** (`Slashing`/`SlashingDagger`/`SlashingHeavy`): prioridade `Heavy > Fast > default` — `CombatPlayer.SwingTrigger`/`PlayerCombat.HitRoutine`.
- **Bodybuilder/Lead Skeleton**: checam `HasType(data, Heavy)`/`IsBlunt(data)` em vez de `== WeaponType.Heavy`.

Campos não afetados pela migração (continuam somando direto, sem tabela por tag): `hitSpeed`, `drawChance`, `critChanceBonus`, `critDamageMultiplier`, `evasionBonus`, `dexterityBonus`, `reversalBonus`, `blockBonus`, `accuracyBonus`, `disarmBonus`, `comboBonus`, `deflectBonus` — cada um é um valor manual por asset, somado em cima do resultado das tabelas acima (mesmo padrão de sempre, ver `CombatSimulator`/`PlayerCombat`).

Os 5 `WeaponData.asset` existentes: `Satyr1` = `Sharp, Fast`, `Golem3` = `Heavy, Blunt`, `Succubus`/`VeryHeavyArmoredFrontierDefender`/`Zombie` = `Sharp` (trio continua idêntico entre si). As tabelas acima foram calibradas pra reproduzir exatamente os valores de chance/dano/alcance que essas 5 armas já tinham antes da migração. `WeaponHandler` não tem mais uma propriedade `currentType` própria (era um espelho de `data.type`, que não existe mais como valor único) — todo lugar que precisa ler o tipo da arma equipada usa `weaponHandler.CurrentWeaponData` direto com `HasType`/`IsSharp`/`IsBlunt`.

### Critical Hit
`CritChance()` no atacante = soma por tag (ver tabela acima) + `weaponData.critChanceBonus` (ou `UnarmedStats.CritChanceBonus`) + `criticalChance` (profile/skills).

On crit: `critMultiplier = weaponData.critDamageMultiplier` entra na fórmula multiplicativa de dano. Popup mostra "CRIT!\n{damage}" em vermelho, fonte 5.
> Future skill **Fierce Brute**: +10% crit permanente.

### Dodge
`DodgeChance()` on the attacker, reading the **defender's** weapon tags (soma por tag, ver tabela acima).

Each agility point above 3 adds +2% dodge, plus the defender's `weaponData.evasionBonus` (or `UnarmedStats.EvasionBonus = +10%` if unarmed) e `defender.evasion` (campo de skill, ex: Untouchable +30%, Ballet Shoes +10%). Same AGI threshold adds +0.8% combo in `ComboChance()` (era +1.5%).

**`accuracy` do atacante (Relentless +30%) é o oposto de evasion** — em vez de aumentar a chance de esquiva de quem tem a skill, reduz a chance de esquiva do **defensor** contra esse atacante. `CombatSimulator.DodgeChance(attacker, defender)`: `total = baseChance + agiBonus + defender.evasion + weaponEvasion - attacker.accuracy`, depois `Mathf.Clamp(total, 0f, 0.60f)` — mesmo teto de 60%, mas agora o clamp também cobre o piso (`accuracy` alto pode zerar a esquiva, não só reduzir). Era `Mathf.Min(0.60f, ...)` sem piso explícito (não importava antes porque nenhum termo conseguia ficar negativo sozinho).

When dodge triggers: skip knockback, Hurt animation, and damage. Defender plays `DodgeLeap` (JumpStart animation + `JumpTo` backward by `knockbackDistance`, height 0.4) over `settings.dodgeDuration` (separate field from `hurtDuration` — was tied to it before, making the leap snap almost instantly). Popup shows "ESQUIVA!" in blue. Combo continues normally.

### Combo
`ComboChance(comboCount)` no atacante — `comboCount` = quantos hits extra de combo já aconteceram neste turno (0 no 1º hit extra). Base = soma por tag (ver tabela em **Tipos de Arma** acima; desarmado = 5%).

Soma-se `weaponData.comboBonus` (campo manual por asset, referência oficial do My Brute — Satyr1 +0.30, Sword trio 0, Golem3 -0.60) + `0.8%` por ponto de AGI acima de 3 + `comboChanceBonus` (skills, ex: Fists of Fury +20% — Relentless **não** soma mais aqui, foi redefinida para +30% accuracy, ver **Dodge** abaixo). Esse total é limitado por `Mathf.Clamp(total, 0f, 0.60f)` — teto de **60%** (era 35%) — e **só depois** multiplicado pelo decaimento `Mathf.Pow(0.5f, comboCount)`: 1º hit extra usa o valor pleno (até 60%), 2º hit extra usa metade (até 30%), 3º um quarto (até 15%), e assim por diante. Mirror oficial do My Brute, onde a chance de combo cai a cada hit consecutivo do mesmo turno.

Valores antigos (base Fast 40%/Dagger 35%/Sword 25%/Heavy 10%/desarmado 10%, AGI +1.5%/ponto, sem clamp, sem decaimento) deixavam personagens com Dagger e AGI alta combando quase sempre e por muitos hits seguidos (ex: Assassin Guy com Satyr1 chegava a ~71-86% por golpe, repetido indefinidamente). `CombatSimulator.SimulateTurn` loga `[ComboChance] {nome} (P{1|2}, arma=...) hit extra #{n} chance={valor}` a cada checagem do loop de combo, antes do `Roll()` — usar isso para confirmar visualmente o decaimento e validar se algum combo de stats/skills ainda está inflando o valor base (pré-decaimento).

### Determination

Mecânica de retry independente do Combo acima — **Combo é pra golpes que ACERTAM** (chance de continuar batendo depois de causar dano); **Determination é pra golpes que FALHAM** (esquivado, bloqueado, ou cancelado por Counter do defensor): `SimulateHitWithDetermination(attacker, defender, isCombo, out damageDealt)` chama `SimulateHit` normalmente e, se `damageDealt` voltar `false` e o atacante tiver a skill, rola **60% fixo** (sem decaimento, sem depender de arma/agilidade) pra tentar **outro golpe completo** imediatamente — sempre com `isCombo: false` (é uma tentativa nova, não uma continuação do combo, então cada tentativa fica elegível pro desarme de "primeiro hit" de novo). Recursivo: cada nova tentativa que também falhar rola os mesmos 60% de novo, até `damageDealt` virar `true` (dano de verdade aplicado) ou a chance de 60% falhar.

`SimulateTurn` chama este wrapper em vez de `SimulateHit` direto, tanto pro primeiro golpe do turno quanto por cada hit do loop de Combo — então um personagem com Determination pode encadear: falha → 60% retry → falha → 60% retry → ... → acerta → (a partir daqui, ComboChance normal assume, podendo emendar mais hits se acertar de novo).

`damageDealt` (novo `out bool` em `SimulateHit`) é `true` só quando o dano normal foi de fato aplicado ao defensor (chegou até a fórmula de dano, depois de Counter/Block/Dodge todos falharem) — **exceto** no guard do Monk (`hitSpeed <= 0f`), que força `damageDealt = true` mesmo sem nenhum ataque real, porque esse guard não representa um golpe que falhou (é a ausência completa de ataque — Monk guarda) e não deveria nunca disparar um retry de Determination (evitaria ficar girando indefinidamente num personagem com `hitSpeed = 0`).

Redefinida pelo usuário — a `SkillDef` original em `SkillAssetGenerator.cs` tinha `description = "+STR conforme perde HP"` (roadmap original, nunca implementada); movida de `StatBoost` pra `CombatPassive` e a descrição atualizada pra bater com a mecânica nova. **Rodar Tools → AutoArms → Generate Skill Assets de novo** depois dessa mudança pra atualizar `category`/`description` no `.asset` já gerado (o gerador é idempotente, mas só escreve essas mudanças quando reexecutado).

### Block
`BlockChance()` no atacante, lendo as tags da arma do **defensor** (soma por tag, ver tabela em **Tipos de Arma** acima; sem arma equipada = 0).

Soma-se ainda o `weaponData.blockBonus` do defensor (ou `UnarmedStats.BlockBonus = -25%` se desarmado) e `defender.blockBonus` (campo de skill — Counter Attack `+0.10`, ver **Counter e Reversal** abaixo). **`defender.counter` não entra mais aqui** — ver seção **Counter e Reversal** abaixo (rewired pra uma mecânica própria, em vez de ser só um bônus de block).

Ordem de verificação em `CombatSimulator.SimulateHit` (caminho ativo): **Counter → Block (+ Reversal) → Esquiva → Dano normal → Reversal → Desarmar** (era Block → Esquiva → Counter — Counter movido pra primeiro, ver **Counter e Reversal** abaixo pro motivo; mesmo padrão de Block-antes-de-Esquiva aplicado em `SimulateRetaliation`, que não tem Counter). Quando block trigga: sem dano, sem Hurt, mas aplica **knockback de 50%** (`knockbackDistance * 0.5f`) em paralelo. Defensor executa animação `Block` via `SetTrigger("Blocking")`. Popup "BLOCK!" em dourado.

**Drop de arma/escudo ao bloquear** — verificados após o popup de block:
- **15%** de chance do **atacante** soltar a arma (impacto no escudo) — independente do que o defensor tem equipado.
- Do lado do **defensor**, escudo tem prioridade sobre a arma — **mutuamente exclusivos no mesmo hit**: enquanto `defender.hasShield` for true, só o escudo pode cair (**10%**, `ShieldDisarmChance`, mesma constante fixa da seção **Desarmar do Escudo** — sem `disarmChanceBonus`/`disarmBonus`); só depois que o escudo já caiu (neste hit ou em hit/bloqueio anterior) é que a arma do defensor passa a correr risco de cair (**10%**) ao bloquear. Antes os dois eram checados independentemente — podiam cair os dois no mesmo hit, o que não fazia sentido (o escudo deveria proteger a arma por baixo dele, igual ao My Brute; bug real reportado pelo usuário).
- Arma usa `DropWeapon(target, isDisarm: false)`; escudo usa `DropShield(target, isDisarm: false)` (mesma queda em pêndulo, evento `CombatEventType.ShieldDrop`) → popup "DROP!" laranja + item cai com pêndulo, fica no chão até fim da luta.

### Counter e Reversal

Duas mecânicas distintas, ambas usando o defensor tomando a iniciativa de volta do atacante — implementadas só no caminho ativo (`CombatSimulator.cs`); **`PlayerCombat.cs`/`AttackSequencer.cs` (legado, código morto enquanto `useSimulator=true`) não têm nenhuma das duas, nem o ajuste de speed 0 abaixo** — seu `BlockChance()` ainda soma `defender.counter` (comportamento antigo), não existe `SimulateRetaliation` equivalente, e `CombatLoop` ainda força mínimo 1 ação mesmo com `speed = 0`. Só importa se `useSimulator` for desligado algum dia.

**Counter** — `CounterChance(defender) = defender.counter`. Checado em `SimulateHit` **primeiro de tudo, antes até do Block** (era depois do Block falhar — Monk/Sixth Sense quase nunca chegavam a counterar de fato, porque sempre que o Block do defensor tinha sucesso primeiro a função retornava antes do Counter ser checado; bug real reportado pelo usuário: Monk não reagia nem depois de bloquear nem ao tomar hit): o atacante já correu e iria acertar, mas o defensor bate primeiro — cancela completamente o hit do atacante (e o resto do combo dele, já que esse hit nunca aconteceu de fato). Só se Counter **não** disparar é que o defensor tenta Block, depois Esquiva. `defender.counter` é alimentado por **Monk** (`counter += 0.40`) e **Sixth Sense** (`counter += 0.10`, versão mais fraca, sem nenhum outro efeito). **Counter Attack não soma mais nesse campo** — foi redefinida (ver Reversal abaixo).

**Reversal** — `ReversalChance(defender) = defender.reversal + weaponData.reversalBonus` (ou `UnarmedStats.ReversalBonus = 0` se desarmado; os 5 `WeaponData.asset` já tinham `reversalBonus` preenchido — Sword +0.10, Heavy -0.30 — mas nenhum código lia o campo até agora). Ao contrário do Counter, Reversal **só age depois de algo já ter acontecido** — checado em **dois pontos** de `SimulateHit`:
1. **Depois de bloquear** (dentro do bloco de `Block`, depois dos checks de drop de arma): defensor já bloqueou, sem tomar dano, e ainda assim contra-ataca de bandeja. Aqui a chance verificada é `ReversalChance(defender) + defender.reversalAfterBlock` — campo extra que **só soma neste ponto**, nunca no ponto 2 abaixo.
2. **Depois do dano normal já aplicado** (hit aconteceu, HP já foi reduzido): defensor contra-ataca em seguida. Usa só `ReversalChance(defender)` puro, sem `reversalAfterBlock`.

**Counter Attack** (redefinida pelo usuário — antes dava `counter += 0.40`, papel que hoje é só de Monk/Sixth Sense) agora dá `blockBonus += 0.10` (soma em `BlockChance()`, ver seção **Block** acima) e `reversalAfterBlock += 0.90` — uma chance de reversal **exclusiva do ponto 1**, que só dispara depois que o personagem já bloqueou (não depois de um hit normal). `PlayerState`/`PlayerCombat` ganharam os dois campos novos (`blockBonus`, `reversalAfterBlock`), mesmo padrão dos outros campos de skill (default 0).

Diferente do Counter, Reversal **não cancela o resto do combo do atacante** — o combo continua normalmente depois, e cada hit extra do combo checa Reversal de novo, independente do(s) anterior(es) (pode triggar em mais de um hit do mesmo combo). O que já aconteceu (bloqueio ou dano) não é desfeito de qualquer forma. Reversal nunca age ANTES de um resultado (esse é o papel do Counter) — só depois de Block ou de Hit.

Ambas chamam `SimulateRetaliation(retaliator, target, eventType)` — o contra-ataque passa por bloqueio/esquiva/crítico normalmente contra o lado oposto (pode ser bloqueado ou esquivado pelo atacante original), mas **não verifica Counter/Reversal de novo** (evita recursão infinita entre as duas mecânicas — uma retaliação é sempre só uma retaliação, não pode ser contra-contra-atacada). `SimulateRetaliation` também checa **Block primeiro, depois Esquiva** — mesma ordem do `SimulateHit` principal (ambos eram Esquiva → Block antes do usuário pedir a inversão; primeiro só na retaliação, depois generalizada pro hit normal também).

`SimulateHit` retorna `bool interrupted` (era `void`) — `true` **só quando Counter trigga** (o único caso que de fato cancela o resto do combo, já que o hit nunca aconteceu); `false` em qualquer outro desfecho, **incluindo Dodge/Block/Reversal**. `Disarm` (depois do hit normal) agora também checa `attacker.isAlive` — Reversal pode matar o atacante na própria retaliação, e sem essa checagem um atacante já morto ainda desarmava o defensor que tinha acabado de contra-atacar. `SimulateTurn`'s loop de combo é `while (!interrupted && attacker.isAlive && defender.isAlive)` — a checagem de `attacker.isAlive` é necessária porque o atacante pode morrer de um Counter/Reversal no meio do próprio turno.

Eventos novos: `CombatEventType.Counter`/`Reversal` (`playerIndex` = quem retalia e causa dano, `targetIndex` = atacante original que recebe) — visual em `CombatPlayer.cs` é parecido com o `Hit` (swing + knockback + hurt), com duas diferenças: **sem `RepositionIfNeeded`** (quem retalia nunca saiu do lugar — é o atacante original que correu até ele; Counter/Reversal disparam antes de qualquer dano nesta troca, então não há knockback prévio que tenha deslocado o retaliador) e **`PlayJumpStart` em vez de `JumpTo`** depois do swing (ver nota abaixo sobre o bug de animação). Popup: `DamagePopup.SpawnCounter`/`SpawnReversal`, texto "CONTRA-ATAQUE!"/"REVERSAL!" em roxo. `CombatLogFormatter` imprime `[CONTRA-ATAQUE]`/`[REVERSAL]` antes da linha de dano.

**Bug de animação travada depois de Counter/Reversal (e a causa real)**: nos `.controller` dos 3 personagens (verificado no da Assassin Guy, mesmo padrão nos outros), os estados `Slashing`/`Slashing Dagger`/`Slashing Heavy` têm **uma única transição de saída**: pro estado `Jump Start`, condicionada ao bool `JumpStart == true`. `Jump Start` só sai pro `Idle` quando `JumpStart` volta a `false` (e `Idle == true`). Não existe transição direta Slashing→Idle no Animator. Isso nunca foi um problema antes porque todo combo termina em `TurnEnd`, que sempre chama `PlayJumpStart` (toggle do bool) **+** `movement.JumpTo` (pulo de volta ao spawn) em sequência — mas quem retalia num Counter/Reversal nunca passa por um `TurnEnd` próprio nesta troca, então ficava **permanentemente travado no estado de Slashing** depois de atacar (um primeiro fix tentando `SetIdle(true)` não resolvia nada, porque a transição de saída do Slashing nem olha pro bool `Idle`, só pro `JumpStart`). Fix: `CombatPlayer.cs` chama `attacker.animationController.PlayJumpStart(jumpStartDuration * t)` pro retaliador depois do swing — mesmo toggle de bool que o `TurnEnd` usa, **sem** chamar `movement.JumpTo()`, então o personagem só faz o pequeno "hop" do Jump Start no lugar (sem se deslocar) e volta pro Idle corretamente.

**Evasion zerada de verdade (Deity)**: zerar só o campo `defender.evasion` (via `evasionPct`) não bloqueava esquiva de verdade — `DodgeChance()` ainda soma chance base por tipo de arma do defensor, bônus de AGI e o `evasionBonus` da própria arma do defensor, todos independentes do campo `evasion`. Novo campo `PlayerState.noEvasion` (`bool`, setado por Deity) faz `DodgeChance()` retornar `0f` direto no início, ignorando todos esses outros termos — só assim "-100% evasion" garante 0% de esquiva de fato, e não só zerar o termo de skill dentro de uma soma que ainda dava chance.

**Chance de já cair em cena armado (genérico, não é skill)**

`CombatSimulator.EquipStartingWeaponIfNeeded(s)`, chamado em `Simulate()` logo depois de `BuildState`/antes de `ApplySkillStats` — **40% de chance**, independente de skill, de qualquer um dos dois jogadores já começar a luta com uma arma aleatória do loadout equipada, em vez de desarmado (o pickup normal de 40% no início de cada turno, ver **Pegar Arma** acima, continua valendo igual pra todo mundo, incluindo Deity — sem tratamento especial). `CombatSimulator.Player1StartingWeapon`/`Player2StartingWeapon` (propriedades públicas, capturadas logo após o sorteio) deixam `CombatSceneLoader` saber qual arma foi escolhida, se alguma.

**Visual sincronizado com o EntryFall**: por causa do ponto acima, `CombatSceneLoader.Initialize()` teve que **inverter a ordem**: `CombatSimulator.Simulate()` agora roda ANTES do EntryFall (não depois, como antes) — só assim dá tempo de chamar `handler.EquipSpecific(simulator.Player1StartingWeapon)` (e o equivalente pro Player2) ANTES da queda do céu, fazendo o personagem já aparecer empunhando a arma durante a animação de entrada em vez de só equipá-la depois. `Simulate()` não depende de nada que só existe pós-EntryFall (transform/`spawnPosition`) — só lê os `PlayerProfile`, então a inversão é segura. `events` (a lista pré-calculada) é guardada numa variável e só consumida por `CombatPlayer.PlayCombat()` depois que ambos pousam, como antes.

**Deity: +50% de tamanho** — `player1Obj.transform.localScale = Vector3.one * 0.3f * (profile.HasSkill("Deity") ? 1.5f : 1f)` em `CombatSceneLoader.Initialize()`, aplicado antes do EntryFall (já vale na queda). Pro Player2 (pré-colocado na cena, escala configurada no editor): `player2Object.transform.localScale *= 1.5f` se `player2Profile.HasSkill("Deity")` — multiplicador sobre o valor atual, não um valor fixo, já que Player2 não tem um valor base hardcoded em código como o Player1.

> Future skill **Shield**: +45% block permanente.

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

### Hideaway

**Redefinida pelo usuário pra bater com os valores oficiais do LaBrute** — passou por uma versão intermediária (arremesso forçado a 100%, nunca melee, arma nunca saía da mão) que foi **revertida**; a versão atual é mais simples e mais próxima do My Brute/LaBrute original. 2 efeitos, ambos checados vivo via `HasSkill("Hideaway")` (sem flag cacheada em `ApplySkillStats`, mesmo padrão de Iron Head/Determination/Resistant):

1. **50% de chance de arremesso, fixa** (`ThrowChance(attacker)`) — **substitui** a soma por tag (não soma a ela). Mesmo com uma arma `Thrown` (que sozinha já daria 100%), Hideaway trava em 50%. Passa pelo **mesmo** `Roll(ThrowChance(attacker))` de item 3 em `SimulateTurn` que todo mundo usa — **não existe mais nenhum branch separado/forçado** pra Hideaway; se o roll falhar, vai pro melee normal (corre até o defensor e ataca), exatamente como qualquer outro personagem armado. Sem combo especial de throws — um throw de Hideaway termina o turno igual a qualquer outro (`TurnEnd` imediato depois de `SimulateThrow`).
2. **+25% bloqueio contra arremessos recebidos** — reduz direto a chance de **acerto** do arremesso (não é mais um 3º resultado "Block" separado com evento próprio): `SimulateThrow` usa `float hitChance = 0.80f - (defender.HasSkill("Hideaway") ? 0.25f : 0f)`, então o throw que normalmente é 80% hit/20% miss passa a ser **55% hit/45% miss** contra um defensor com Hideaway. Mais simples que a 1ª versão (que tinha um `ThrowBlockChance`/`CombatEventType.Block` com `isThrow=true` dedicado, removido).

**A arma não desaparece, mas sai da mão**: arremessar com Hideaway agora usa exatamente a mesma regra de uma arma `Thrown` normal — `Unequip()` (não `UnequipPermanent()`), então o personagem fica **desarmado** até pegar arma de novo (40% normal num turno futuro), mas a arma em si **continua no loadout**, podendo ser usada de novo depois. Isso é uma mudança da versão anterior, que fazia a arma nunca sair da mão (`keepsWeapon`/sempre armado) — removida.

**`ThrowChance()`/`SimulateThrow` também são afetados por Sticky Hands** (ver seção própria abaixo) — a chance de 50% de Hideaway ainda é multiplicada por `(1 - attacker.stickyHands)` se o mesmo personagem também tiver Sticky Hands (raro, mas não excludente).

### Sticky Hands

Campo numérico `stickyHands` (float, default `0`) em `PlayerState` e `PlayerCombat` — `ApplySkillStats` seta `stickyHands += 0.50f` quando `HasSkill("Sticky Hands")` (mesmo padrão antigo de `disarmChanceBonus`/`blockBonus`, diferente das skills mais novas que checam `HasSkill()` vivo sem cachear nada). 2 efeitos, ambos como multiplicador `(1 - stickyHands)` sobre o resultado final da fórmula (não um termo aditivo somado dentro dela):

1. **-50% chance de ser desarmado**: `DisarmChance(attacker, defender)` agora recebe os dois lados (antes só `attacker`) — calcula a chance normal (tags + `disarmBonus` da arma + `disarmChanceBonus` do atacante, ex: Shock) e multiplica o total por `(1 - defender.stickyHands)` antes de retornar. Reduz a chance de o **defensor** (quem tem a skill) perder a própria arma, não afeta a chance de o atacante desarmar.
2. **-50% chance de arremesso, incluindo o próprio**: `ThrowChance(attacker)` multiplica o resultado (tag sum normal, ou o 50% fixo de Hideaway) por `(1 - attacker.stickyHands)` — dificulta jogar a própria arma fora até por acidente, mesmo sem ter Hideaway.

**Só implementada no caminho ativo** (`CombatSimulator.cs`) — o campo `stickyHands` existe em `PlayerCombat.cs`/`CombatSceneLoader.ApplySkillStats` por paridade (mesmo padrão de outros campos numéricos de skill), mas as fórmulas legadas `PlayerCombat.ThrowChance()`/`DisarmChance()` (código morto enquanto `useSimulator=true`) não foram atualizadas — mesmo precedente de `BlockChance()` legado, que também não reflete `blockBonus` desde a mudança do Counter Attack. Não entra em `PlayerProfile.GetEffectiveStats()`/`CharacterPanel` (a 15-tupla de stats exibida na UI) — não é um dos status já rastreados ali, e adicionar um novo exigiria mudanças na tela de Stats fora do escopo pedido.

### Troca de Arma (Weapon Swap) — mecânica geral, não é skill

Pedido pelo usuário como ação independente de Hideaway, **vale pra todo mundo**: se o atacante já está **armado** no início do turno, há a **mesma chance de 40%** do pickup normal (ver **Pegar Arma** abaixo) de **trocar** de arma — joga a atual no chão (mesma queda em pêndulo de `DropWeapon`/`WeaponDrop`) e puxa uma nova aleatória do loadout (evita repetir a mesma arma, se houver outra opção disponível). Checado em `SimulateTurn`, **mutuamente exclusivo** com o pickup-se-desarmado (`else if`, já que um exige estar desarmado e o outro armado) e com Thief (`!stoleWeapon`, mesmo motivo).

**Não acontece no 1º turno de quem já nasce armado** — `PlayerState.hasTakenFirstTurn` (novo, default `false`, setado `true` no topo de `SimulateTurn`) alimenta `isFirstTurn`, que entra na condição do swap (`!isFirstTurn && ...`). Sem essa checagem, um personagem que começa a luta já armado (`CombatSimulator.EquipStartingWeaponIfNeeded`, 40% de chance antes da luta) podia trocar a arma que acabou de "nascer" com ainda no próprio 1º turno, sem nunca ter atacado com ela — pedido pelo usuário pra não acontecer. Não bloqueia o resto do turno (Hideaway/throw/melee continuam normais nesse mesmo 1º turno) — só o swap especificamente. Estruturalmente isso só importa pra quem já nasce armado: quem começa desarmado e pega arma no próprio pickup do item 2 nunca cai no `else if` do item 2b no mesmo turno (são ramos mutuamente exclusivos do mesmo `if`/`else if`), então o swap nunca "competia" com o pickup normal de qualquer forma.

A arma trocada **some do loadout/`WeaponHUD` permanentemente** — `attacker.weaponLoadout.Remove(oldWeapon)` no simulador, `PlayerCombat.DropWeapon` (sem nenhum parâmetro extra — a primeira versão tinha um `removeFromLoadout: false` pra manter a arma disponível, mas o usuário corrigiu: ela deve desaparecer de verdade, igual a `WeaponDrop`/`Disarm`, pra não poder ser sacada de novo) cobre o popup "DROP!" + a queda visual + a remoção via `UnequipPermanent()`.

Dois eventos novos em sequência: `CombatEventType.WeaponSwap` (`weaponName` = arma largada, visual fire-and-forget via `DropWeapon`) seguido de um `PickupWeapon` normal pra nova arma (reusa a animação `CatchWeapon` já existente, sem precisar de nenhum evento/animação dedicada pra "puxar a nova arma").

### Pegar Arma (Início do Turno)
Ambos os personagens começam o combate **desarmados**. Ao iniciar cada turno, se `CurrentWeapon == null`:
- **40%** de chance de executar `EquipRandom()` (pega uma arma aleatória do loadout) + animação `CatchWeapon` (0.6s) → ataca com a arma.
- **60%** não pega → ataca desarmado (soco).

`EquipRandom()` chama `loadout.GetRandomWeapon()` — seleciona aleatoriamente entre as armas disponíveis no runtime loadout (não ciclicamente). Se o loadout estiver vazio, `GetRandomWeapon()` retorna null e `EquipRandom()`/`EquipNext()` chamam `Unequip()` (limpando `CurrentWeaponData` e disparando `OnWeaponChanged(null)`) em vez de destruir a arma visual sem atualizar esse estado — bug antigo deixava o ícone da `WeaponHUD` destacado em amarelo enquanto o personagem batia desarmado.
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
4. Cria `FallenWeapon` GameObject com `SpriteRenderer` na layer **Default** (sorting order 0 — sempre atrás de todos os personagens).
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

### Survival
`ApplyDamage(target, rawDamage)` (`CombatSimulator.cs`) — se o dano aplicaria HP ≤ 0 e a skill ainda não foi usada nesta luta (`target.survivalUsed`), o personagem sobrevive com 1 HP em vez de morrer (uma vez por luta, consome `survivalUsed`). Enquanto `hp == 1` e a skill ainda equipada: `+20% evasion` (`DodgeChance`) e `+20% block` (`BlockChance`) — não há cura no jogo hoje, então na prática esse bônus dura até o fim da luta (ou até o personagem efetivamente morrer num próximo hit que ele não sobrevive de novo).

**Bug visual corrigido**: `CombatEvent.Hit`/`Counter`/`Reversal` carregam `newHp`/`maxHp` direto no próprio evento (não só no `HealthChanged` separado que vem depois). Sem isso, `CombatPlayer.ApplyHealthDelta` ficava aplicando o dano bruto (`finalDamage`, antes de Survival entrar em ação) direto na `HealthSystem` ao vivo — que não tem como saber que Survival ia salvar o personagem em 1 HP — e a barra visualmente ia a 0 (clampada) por um frame, mesmo com o log de combate já registrando corretamente "sobrevive com 1 HP". `ApplyHealthDelta` hoje sempre usa o `newHp` já resolvido pelo simulador (que já leva Survival em conta), nunca o dano bruto do evento.

### Thief

Ação de **início de turno** (mesmo ponto do pickup normal de arma, ver **Pegar Arma** abaixo), não depende de acertar um golpe: se `attacker.HasSkill("Thief")`, `attacker.currentWeaponData == null` (desarmado) e `defender.currentWeaponData != null` (oponente armado), **44% de chance por turno** de roubar a arma equipada do oponente — limitado a `thiefUsesRemaining` (começa em **2**, decrementado a cada roubo de sucesso; sem uso depois de zerar). Checado **antes** do pickup comum de 40% em `SimulateTurn` — os dois exigem estar desarmado, então só um pode acontecer no mesmo turno (`stoleWeapon` decide se o pickup comum ainda roda).

Ao triggar: a arma sai do `weaponLoadout` do defensor e entra no do atacante (`defender.weaponLoadout.Remove`/`attacker.weaponLoadout.Add`), `defender.currentWeaponData = null`, `attacker.currentWeaponData = stolen` — diferente de Sabotage (roadmap, ainda não implementada), que remove a arma do jogo inteiramente; aqui o ladrão fica com ela de verdade, podendo usá-la pelo resto da luta. Evento `CombatEventType.Thief` (`playerIndex` = ladrão, `targetIndex` = vítima, `weaponName`).

**Visual**: `PlayerCombat.StealWeapon(thief, victim)` — referência do My Brute original pedida pelo usuário (ladrão pula nas costas do adversário, "montando" como cavalinho, os dois balançam juntos 4 vezes, e ele desce já com a arma na mão). Não existe sprite dedicado de "montar" no projeto (Spriter2UnityDX só gera Idle/Running/Slashing/etc.) — aproximado via movimento de transform puro (mesmo espírito procedural do pêndulo de `DropWeapon`/`DropShield`, sem novo estado de Animator):
1. Salto (`MovementController.JumpTo`) até uma posição nas costas do defensor (offset lateral+vertical) — **velocidade igual ao `RuntimeRunSpeed` usado em `ReturnToSpawn`/`TurnEnd`** (era uma constante fixa de 3, bem mais lenta — redefinida pelo usuário pra o "pêndulo" de ida e volta não ficar mais lento que o salto normal de fim de turno) e **altura igual a `settings.jumpHeight`** (era uma constante fixa de 0.8, baixa demais — aumentada pra parecer mais um salto de verdade, mesmo arco usado em `ReturnToSpawn`/`DodgeLeap`). Mesma velocidade/altura usadas no salto de volta, no fim da sequência.
2. Ao chegar nas costas: **ladrão vira pra mesma direção do adversário**, usando o mesmo mecanismo de flip já existente no projeto (sinal de `localScale.x` — ver Medieval Warrior Girl na cena, `-0.3` pra virar pra esquerda; não havia nenhum sistema de flip dinâmico antes disso, os personagens só tinham a direção fixa do próprio prefab). Restaurado pra direção original antes do salto de volta.
3. 4 ciclos de bounce **horizontal** (pra frente e pra trás, `Vector3.right`, não mais vertical) sincronizado nos dois (ladrão balança mais que a vítima), um pouco mais devagar que a 1ª versão (`bounceDuration` 0.18s, era 0.1s) — pedido pelo usuário.
4. A cada um dos 4 ciclos, a **vítima "pisca"**: alterna entre o sprite Face 01 (normal) e Face 03 (olhos fechados) num GameObject filho chamado "Face 01" que cada personagem já tem (Spriter2UnityDX, com `TextureController` + `SpriteRenderer` próprios). `PlayerCombat.faceRenderer`/`faceSprites` (resolvidos em `Awake()` por nome do GameObject, mesmo padrão de `bodyRenderers`) setam o `SpriteRenderer.sprite` **direto**, não `TextureController.DisplayedSprite` — esse componente só aplica a troca dentro do próprio `Update()` e só quando o Animator não está em transição (`IsTransitioning()`), o que podia atrasar ou simplesmente nunca mostrar a troca durante uma sequência rápida (bug real reportado pelo usuário: vítima não piscava). Setar o `SpriteRenderer` direto ignora esse gate e garante a troca no mesmo frame.
5. Troca de arma (`Unequip`/`EquipSpecific` + `loadout.RemoveCurrentWeapon`/novo `PlayerLoadout.AddWeapon`, pra `WeaponHUD` dos dois lados atualizar) e popup "DISARM!" laranja.
6. Salto de volta pro ponto de partida (mesma velocidade do passo 1).

Sorting layer promovida temporariamente (`Characters`/`Characters2`, mesmo padrão de `SetAttackerLayers`) pra o ladrão renderizar por cima da vítima durante o "cavalinho". `CombatPlayer`'s case `Thief` é **bloqueante** (`yield return StartCoroutine`, não fire-and-forget) — o resto do turno (Throw/Run/Melee) depende do ladrão já estar armado quando a sequência termina.

`PlayerLoadout.AddWeapon(WeaponData)` (novo) — adiciona uma arma ao `runtimeWeapons` e dispara `OnWeaponsChanged`, mesmo padrão de `RemoveCurrentWeapon`; não existia método de adicionar antes (só remover), necessário pra arma roubada aparecer na `WeaponHUD` do ladrão.

`PlayerState.thiefUsesRemaining` (novo, default 2) — contador de usos restantes nesta luta; não tem equivalente em `PlayerCombat.cs` (caminho legado), mesmo padrão de Determination/Resistant (mecânicas novas só implementadas no caminho ativo do `CombatSimulator`).

### Resistant

`ApplyResistantCap(target, damage)` (`CombatSimulator.cs`) — se `target.HasSkill("Resistant")`, nenhum hit isolado pode ultrapassar **25% do HP MÁXIMO** de quem tem a skill (`Mathf.Min(damage, target.maxHp * 0.25f)`). Usa `maxHp`, não o HP atual — o cap só limita o quanto UM hit isolado pode arrancar da barra cheia, não impede a morte de quem já está com HP baixo (um personagem em 10% de HP ainda morre normalmente de um hit capado em 25% do máximo).

**Ordem importa**: o cap é aplicado no dano **bruto**, **antes** de Lead Skeleton e da redução de armadura — não depois. Armor/Lead Skeleton reduzem por cima do valor já capado, então as mitigações empilham em vez do cap "absorver" o que a armadura já teria reduzido. Exemplo do usuário: 100 HP máximo + Resistant capa o dano bruto em 25, e se o personagem também tiver +50% armor, esses 25 ainda são reduzidos pra 12.5 (arredondado no `finalDamage` final, não nos 25 isolados) — dano final 12 ou 13 dependendo do arredondamento. Se o cap fosse aplicado depois da armadura (ordem errada), a armadura teria reduzido o dano bruto primeiro e o cap só entraria em jogo se esse valor já reduzido ainda excedesse 25% do HP máximo — resultado bem diferente (e tipicamente mais alto) do esperado.

Aplicado nos 3 pontos onde dano é calculado antes de `ApplyDamage` (mesmo padrão de Survival acima): hit normal (`SimulateHit`), retaliação de Counter/Reversal (`SimulateRetaliation`), e arremesso (`SimulateThrow`) — nos três, a chamada vem imediatamente depois de `CalcDamage`/`CalcThrowDamage`, antes de qualquer outra mitigação. Não tem flag cacheada em `ApplySkillStats` — checado vivo via `HasSkill("Resistant")`, mesmo padrão de Iron Head.

### Chaining

`PlayerState.chainHitStreak` (int, default 0) conta golpes **melee** consecutivos que o atacante **acerta de verdade** (mesmo ponto de `SimulateHit` onde o dano normal é aplicado ao defensor — vale tanto pro primeiro golpe do turno quanto pra cada hit extra de Combo) sem que o próprio atacante tenha tomado **nenhum** dano nesse intervalo. Ao chegar em **3**, zera o streak e estuna o defensor por **1 ação dele** (`defender.stunnedActions++`, novo `CombatEventType.Stunned`).

**O que quebra o streak**: qualquer dano que o atacante (dono da skill) tome, de **qualquer origem** — hit normal, Counter, Reversal ou Throw — checado direto em `ApplyDamage(target, rawDamage)` (`target.chainHitStreak = 0` se `target.HasSkill("Chaining")`), já que os 4 pontos de dano do arquivo passam por esse método único. **O que NÃO quebra**: o atacante ser alvo de Miss/Dodge/Block — nenhum desses chama `ApplyDamage`, então o streak continua intacto. Isso cobre exatamente os 3 cenários descritos pelo usuário: (1) atacar→combar→combar no mesmo turno (3 hits, 1 turno); (2) atacar→(TurnEnd/volta ao spawn)→atacar→(TurnEnd)→atacar de novo (3 hits, 3 turnos separados — o streak não é resetado por `TurnEnd`/round, só por `ApplyDamage`); (3) atacar→combar (2 hits) → turno do oponente, que erra o arremesso ou é esquivado (sem `ApplyDamage`, streak continua em 2) → atacar de novo (3º hit) → estuna.

**Consumo do stun** (`PlayerState.stunnedActions`, int) — checado no **topo de `SimulateTurn`**, antes até do `isFirstTurn`: se `attacker.stunnedActions > 0`, decrementa, emite `CombatEventType.StunSkip` e `TurnEnd` direto, **sem** Thief/pickup/swap/throw/melee nenhum — a ação inteira é perdida. "Por uma ação adversária" é tratado como literalmente 1 ação (não 1 round/turno completo) — um personagem rápido (Speed System, várias ações por round) só perde 1 dessas ações, não o round inteiro.

**Visual** (`PlayerCombat.ShowStunLabel`/`HideStunLabel`): a pedido do usuário, por enquanto é só uma **label de texto** ("ATORDOADO!", `TextMeshPro` world-space, mesmo padrão do `DamagePopup` mas **persistente**, sem subir/desaparecer) fixa acima da cabeça do estunado, parented ao próprio `transform` (`localScale.x` compensado pelo sinal do pai, pra não renderizar espelhada se o personagem estiver virado pra esquerda) — substituir por sprite dedicado é trabalho futuro do usuário. `CombatEventType.Stunned` (emitido no momento do 3º hit) chama `ShowStunLabel()` no defensor — liga a label e inicia `StunHurtLoop`, uma coroutine que refaz `SetTrigger("Hurt")` a cada 0.3s indefinidamente (Hurt não tem nenhum bool de "hold" no Animator Controller, diferente de Throwing/Slashing — volta pra Idle sozinho via tempo de saída do clip, então sem o loop a pose não ficaria presa). `CombatEventType.StunSkip` (emitido quando a ação estunada é consumida) chama `HideStunLabel()` no próprio atacante daquele turno (que é quem estava estunado) — para a coroutine, destroi a label e chama `SetIdle(true)` pra voltar ao normal.

**Intervalo de 0.3s no loop, não `settings.hurtDuration` (~0.12s)**: re-trigger rápido demais reproduziria a mesma cintilação entre poses já corrigida no bug do Throwing ("parece que está com parkinson") — 0.3s é mais espaçado que o gap natural entre hits de um combo de verdade.

Implementado só no `CombatSimulator.cs`/`PlayerCombat.cs` (caminho ativo) — sem equivalente em `PlayerCombat`/`AttackSequencer` legado, mesmo padrão de Thief/Resistant/Hideaway/Determination.

### Spy

Skill exclusiva do LaBrute/eternaltwin — **não existe no Muxxu original**. `CombatSimulator.ApplySpySabotage(spy, victim)` — chamado duas vezes em `Simulate()` (uma vez pra cada direção, já que os dois lados podem ter a skill independente um do outro), logo depois de `BuildState`/`LogWeaponLoadout` e **antes** de `EquipStartingWeaponIfNeeded` (a arma inicial sorteada pré-luta também já deve poder vir sabotada). **Roda depois de `ApplySaboteur`** (ver seção própria abaixo) — pedido explícito do usuário: Saboteur precisa estar validado primeiro, já que ele destrói uma arma do loadout, e Spy deve calcular "metade do loadout" sobre o que sobrou, não sobre a contagem original (senão Spy podia gastar a redução de -20% numa arma que o Saboteur ia destruir de qualquer jeito no mesmo instante).

Se `spy.HasSkill("Spy")`: **metade do loadout da vítima, arredondado pra baixo** (`Mathf.FloorToInt(victim.weaponLoadout.Count / 2f)`), escolhidas **aleatoriamente** (sorteio sem reposição via `_rng`, não as primeiras N), tem o `damage` reduzido em **20%** — `Mathf.RoundToInt(original.damage * 0.80f)`. Com 0 ou 1 arma no loadout da vítima, `count` vira 0 e a skill não tem efeito algum (sem sabotagem).

**Permanente, não por hit**: a redução fica gravada na própria `WeaponData` usada pelo simulador pro resto da luta inteira — diferente de Resistant/Iron Head/etc., que são checadas vivas a cada hit, aqui o número já sai reduzido antes de qualquer turno acontecer, então `RollWeaponDamage(data)`/`CalcDamage` nem sabem que existe uma skill Spy envolvida — só leem `weaponData.damage`, já com o valor final.

**Clona em vez de mutar o asset original**: `victim.weaponLoadout` guarda a **mesma referência** do `WeaponData` ScriptableObject salvo em disco (`BuildState` copia direto de `profile.weaponLoadout.weapons`, sem clonar — ver **PlayerLoadout**/`runtimeWeapons`, que já evita esse problema do lado do loadout visual, mas o do simulador nunca precisou disso até agora). Mutar `weapon.damage` direto corromperia o asset pra **qualquer outra luta ou personagem** que use essa mesma arma, já que é o mesmo objeto em memória. `ApplySpySabotage` usa `Object.Instantiate(original)` para criar uma cópia isolada só para esta simulação, ajusta o `damage` nela, e **substitui a entrada** em `victim.weaponLoadout[idx]` pelo clone — qualquer pickup/swap/roubo (Thief) que sortear essa arma depois automaticamente usa o dano já reduzido, sem precisar de nenhuma flag "sabotada" extra em `PlayerState`/`SimulateHit`.

**Log pré-combate**: `Debug.Log($"[Spy] Armas sabotadas: {nomes} (-20% dano).")` — uma linha por direção (só quando `count > 0`), com os nomes das armas escolhidas separados por vírgula. Exceção deliberada à Logging Policy (igual a `[ComboChance]`/`[CalcDamage]`/etc.) — pedido explícito do usuário, não é instrumentação temporária.

**Visual — ícones vermelhos no `WeaponHUD`**: como o `WeaponHUD` lê do `PlayerLoadout` **visual** (`profile.weaponLoadout.weapons`, os assets originais — nunca vê os clones do simulador, que vivem só dentro do `PlayerState`), a sabotagem é comunicada por **nome**, não por referência. `CombatSimulator.Player1SabotagedWeapons`/`Player2SabotagedWeapons` (novas, `List<string>`, lidas depois de `Simulate()` retornar) guardam os nomes das armas sabotadas de cada lado — `CombatSceneLoader.Initialize()` passa essas listas pro `WeaponHUD` correspondente via novo `WeaponHUD.SetSabotagedWeapons(IEnumerable<string>)`. `WeaponHUD.UpdateHighlight()` agora soma um terceiro estado de cor: ativa (dourado, prioridade) → sabotada (vermelho, `0.75, 0.15, 0.15, 0.55`) → padrão (preto semi-transparente) — continua funcionando mesmo numa arma sabotada que nunca chega a ser equipada.

### Saboteur

Skill do LaBrute, categoria CombatPassive. `CombatSimulator.ApplySaboteur(saboteur, victim)` — chamado nas duas direções **antes** do Spy (mesmo ponto: depois de `BuildState`, antes de `EquipStartingWeaponIfNeeded`), antes de qualquer turno. Ordem invertida a pedido do usuário (era depois do Spy) — ver nota na seção **Spy** acima pro motivo (Spy precisa calcular "metade do loadout" já sobre o que sobrou depois da destruição do Saboteur).

Se `saboteur.HasSkill("Saboteur")` e `victim.weaponLoadout.Count > 0`: sorteia **1 arma aleatória** do loadout da vítima e remove **permanentemente** (`RemoveAt`, sem clonar — diferente de Spy, aqui a arma simplesmente deixa de existir pra essa luta, não há dano nenhum pra preservar/reduzir, então não tem o mesmo risco de mutar o asset original) e dá **-100 initiative** na vítima, naquele instante (soma direto em `victim.initiative`, antes de qualquer outro ajuste de `ApplySkillStats` — quem age primeiro compara initiative, ver **Speed System**, então isso dá vantagem real de agir primeiro pro Saboteur). Com loadout vazio, `count` é 0 e não há o que sortear — sem efeito algum.

**Diferente de Spy**: Saboteur **destrói** (a arma simplesmente não existe mais pra ninguém pegar, nem o próprio dono) em vez de **reduzir dano permanentemente**; e soma um malus direto de iniciativa, que Spy não tem.

**Evento + popup**: novo `CombatEventType.Saboteur` (`playerIndex` = quem tem a skill, `targetIndex` = vítima, `weaponName` = arma destruída) — emitido junto da sabotagem em `Simulate()`, antes do primeiro `TurnStart` da luta (primeiro(s) evento(s) da lista, antes de qualquer ação de combate). `CombatPlayer`'s case `Saboteur` mostra `DamagePopup.SpawnSabotage` (novo, mesmo estilo/cor laranja do `Disarm`, texto "SABOTAGE!") acima da vítima logo no início da animação, com uma pausa curta (0.6s) antes do resto da luta prosseguir.

**Visual da arma destruída — cai do próprio ícone da WeaponHUD até o chão** (bug corrigido, reportado pelo usuário): `ApplySaboteur` só removia a arma do `PlayerState.weaponLoadout` do **simulador** — a `WeaponHUD` (visual) lê de um `PlayerLoadout` totalmente separado (`profile.weaponLoadout.weapons`, já construído ANTES de `Simulate()` rodar), então o ícone continuava lá, intacto, mesmo com a arma já destruída pro simulador. Corrigido em 3 partes:
1. `WeaponHUD.GetIconScreenPosition(weaponName)` (novo) — devolve a posição em tela do ícone (Screen Space Overlay, então `transform.position` já é pixel) ou `null` se não achar.
2. `WeaponHUD.RemoveWeapon(WeaponData)` (novo) — chama `_loadout.RemoveCurrentWeapon(data)` passando a referência explícita (não depende de `currentIndex`/equipar, já que a arma nunca foi pega) — dispara `OnWeaponsChanged`, e o ícone desaparece do HUD.
3. `PlayerCombat.DropWeaponFromHud(victim, data, startWorldPos)` (novo) — mesma queda em pêndulo amortecido de `DropWeapon`, mas sem `CurrentWeapon`/mão pra sair (a arma nunca foi equipada): começa de `startWorldPos` em vez do `handBone`. `CombatPlayer`'s case `Saboteur` converte a posição em tela do ícone (passo 1) pra posição no mundo via `Camera.main.ScreenToWorldPoint` (profundidade = `defender.transform.position.z - Camera.main.transform.position.z`), remove o ícone (passo 2) e dispara a queda (passo 3) — tudo isso em paralelo com o popup "SABOTAGE!" já existente. `CombatPlayer` ganhou `p1WeaponHUD`/`p2WeaponHUD` (novos campos, wireados por `CombatSceneLoader` igual a `p1Combat`/`p2Combat`) pra poder achar a `WeaponHUD` certa pelo `targetIndex` do evento.

**2 ajustes depois do 1º teste (ícone sumia, mas a queda não aparecia — reportado pelo usuário)**:
- **Sorting order**: `DropWeaponFromHud` usava `sortingOrder = 0` na layer `Default`, **mesma layer+ordem do fundo da arena** (`Colosseum arena`, também `Default`/0) — como os dois sprites ficam no mesmo Z (jogo 2D), empatam no critério de profundidade da câmera e a ordem de desenho entre eles fica indefinida, podendo renderizar a arma atrás do fundo (invisível) durante a queda inteira. Trocado pra `sortingOrder = 1` (ainda na layer `Default`, então continua abaixo de `Characters`/`Weapons` depois de pousada, igual a um `DropWeapon` normal — só ganha prioridade sobre o fundo especificamente).
- **Layout do HUD**: `Canvas.ForceUpdateCanvases()` chamado antes de `GetIconScreenPosition` — `RectTransform.position` de um ícone dentro do `HorizontalLayoutGroup` só reflete a posição final depois de um passe de layout do Canvas; ler antes desse passe podia devolver uma posição desatualizada (ex: ainda no canto/zero). Forçar o recálculo garante a posição correta no momento exato da leitura.
- `Debug.LogError` novo no `else` do `if` que monta a queda — se `weaponHud`/`weaponData`/`iconScreenPos`/`Camera.main` vier nulo por qualquer motivo, aparece no Console qual condição falhou, em vez de silenciosamente não fazer nada.

**3º ajuste — arma caindo grande demais (reportado pelo usuário)**: `fallen.transform.localScale = Vector3.one * data.scale` usava só o campo `WeaponData.scale` sozinho — mas esse valor é relativo ao bone da mão, que por sua vez já está dentro da hierarquia do personagem (escala raiz tipicamente ~0.3, ver `CombatSceneLoader.Initialize`/`player1Obj.transform.localScale = Vector3.one * 0.3f`). `DropWeapon`/`DropShield` nunca tiveram esse problema porque leem `inHand.transform.lossyScale` (a escala já resolvida pela hierarquia inteira, incluindo a do personagem) quando existe um objeto na mão — mas `DropWeaponFromHud` nunca tem nenhum objeto na mão pra ler de (a arma nunca foi equipada). Corrigido multiplicando manualmente pela escala raiz do personagem: `fallen.transform.localScale = victim.transform.lossyScale * data.scale`.

**4º ajuste — arma caindo fora da arena, de vez em quando (reportado pelo usuário)**: o X da queda nunca muda (só o Y, por gravidade) — vem direto da conversão tela→mundo da posição do ícone na `WeaponHUD`, sem nenhum limite. Ícones perto das bordas da tela (ex: loadout grande, ícone bem no canto do `HorizontalLayoutGroup`) podiam converter pra um X fora da área jogável, fazendo a arma cair reto fora da arena — visualmente "desaparecia do mapa". Corrigido clampando só o `startWorldPos.x` em `DropWeaponFromHud` nos mesmos limites de `ClampToArena` (`±7.25`) — não reusa `ClampToArena` inteiro porque ele também clampa Y pro intervalo de posição de **personagem** (`-3.90` a `-0.81`), o que destruiria a altura inicial da queda (bem mais alta, perto do topo da tela, de propósito — é assim que fica visualmente "caindo do HUD").

**Log pré-combate**: `Debug.Log($"[Saboteur] Arma destruída: {nome} | initiative do oponente -100.")` — exceção deliberada à Logging Policy (igual a Spy), pedido explícito do usuário.

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

When both are set, after EntryFall: `attackSequencer.player1Profile = profile` is assigned (so `OnCombatEnd` can still award XP / show `CombatResultPanel` even though `attackSequencer.player1` is never set), then the simulator runs instead of the coroutine loop. The `AttackSequencer` stays idle (its `WaitUntil` never resolves) — `TriggerCombatEnd` in `CombatPlayer` calls `sequencer.OnCombatEnd(winner)` directly once `CombatEnd` is reached. `CombatHUD.AddSpeedControls(player)` creates a speed-toggle button and a **Skip** button in the bottom-center of the screen.

**Speed toggle button:** `CombatPlayer.ToggleSpeed()` flips between `_playbackSpeed = 1f`/`1.5f` (tracked by `_is2x`) and returns the new state. `CombatHUD.MakeSpeedToggleButton` reacts to that return value: label "1x" / dark gray background / white text normally → label "1.5x" / gold background / black text when accelerated (and back). No separate "set to 1x" button — clicking it again toggles back. The combat scene has no `EventSystem` of its own (only `01_MainMenu`/`02_SelectCharacter` do), so `CombatHUD.Initialize` calls `EnsureEventSystem()` to create one at runtime — without it, none of the HUD buttons receive clicks.

**Hit event animation timing (`CombatPlayer.ExecuteEvent`, `CombatEventType.Hit`):** waits `slashHalf` (half of `slashingDuration`) before applying knockback/hurt/damage popup, then waits `slashHalf` again afterward so the attacker's slash clip always finishes before anything can re-trigger it. Without that second wait, combo hits (consecutive `Hit` events with no `TurnEnd` between them) retriggered the `Slashing`/`SlashingHeavy`/`SlashingDagger` Animator trigger mid-clip, snapping/restarting the animation instead of playing it through.

At the impact moment (after the first `slashHalf` wait), `HealthSystem.TakeDamage(evt.damage)` is called directly off the `Hit` event, in the same breath as the damage popup and the knockback/hurt animations — not waiting for the separate `HealthChanged` event later in the list. The standalone `HealthChanged` case (`ApplyHealthChanged`) still runs when reached, but is now a no-op for normal playback since the delta against `CurrentHealth` is already 0; it still matters for the `Skip` fast-forward path, which never goes through `Hit` at all and applies every remaining `HealthChanged` directly.

`Dodge` and `Block` now also trigger the attacker's `Slashing`/`SlashingHeavy`/`SlashingDagger` swing and wait `slashHalf` before the defender's reaction (mirroring `Hit`) — previously these two events only animated the defender, with no attacker swing at all, so a dodge/block looked like the defender randomly leaping/blocking nothing. Both also wait the trailing `slashHalf` afterward, same as `Hit`, to protect against combo retrigger.

`CombatEvent.isThrow` (set on the `Hit` emitted by `CombatSimulator.SimulateThrow`) tells `CombatPlayer` to skip the melee swing trigger and the `slashHalf` waits for that hit — the attacker already has no weapon in hand (just unequipped it in the `ThrowWeapon` event) and already did the "windup" during the projectile's flight, so the impact (damage/popup/hurt) applies immediately when the `Hit` event starts, synced with the moment the thrown weapon visually reaches the defender. `Miss` (only ever emitted after a throw) was already immediate and needed no change.

**`RepositionIfNeeded(attacker, defender, t)`:** chamado no início dos casos `Hit` (quando `!evt.isThrow`), `Dodge` e `Block`, antes do swing trigger — mirrors `ComboStrikeRoutine.AttackPosition()`/reposicionamento do caminho legado (ver Combo Architecture acima), que nunca tinha sido portado pro `CombatPlayer`. Recalcula `CalcAttackPosition(attacker, defender)` e roda `PlayRun` se a distância atual for > 0.3 unidades; no-op na 1ª ação do turno (atacante já está no lugar certo por causa do `RunToDefender`). Sem isso, um combo hit/dodge/block depois de uma esquiva ou knockback anterior (que empurrou o defensor mais longe) acontecia com o atacante parado fora de alcance — o defensor levava um knockback/dodge sem nenhum swing visível por perto, parecendo um pulo "do nada" sem ação alguma.

`comboDelay` (0.15s):** added after every `Hit`/`Dodge`/`Block`/`Miss` event in `CombatPlayer.ExecuteEvent`, scaled by the speed-toggle's `t`. A combo turn (e.g. hit→dodge→hit→dodge→hit, all part of one attacker's combo loop in `CombatSimulator.SimulateTurn`) had zero gap between consecutive actions before this — each action's own animation timing ran back-to-back with nothing in between, so a 6-action combo blurred together and felt like only 2-3 distinguishable actions happened, even though every event individually played out and dealt/avoided damage correctly. `interTurnDelay` only applies *between* different turns/attackers, not between actions within the same attacker's combo.

`CombatSimulator.Simulate()` logs `[CombatSimulator] Iniciando simulação...` on entry and `[CombatSimulator] {n} eventos gerados` on exit — exceptions to the no-stray-logs rule (see Logging Policy), kept as permanent confirmation that the simulator actually ran.

### CombatEventType values
`TurnStart, RunToDefender, ThrowWeapon, PickupWeapon, WeaponSwap, Thief, Hit, Counter, Reversal, Dodge, Block, Miss, Disarm, WeaponDrop, ShieldDisarm, ShieldDrop, HealthChanged, SpeedBonus, Stunned, StunSkip, Saboteur, TurnEnd, CombatEnd`

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

### Arquitetura

| Arquivo | Tipo | Propósito |
|---|---|---|
| `Assets/Scripts/Data/SkillData.cs` | ScriptableObject | Dados de uma skill (nome, ícone, categoria, ativação) |
| `Assets/Scripts/Data/SkillDatabase.cs` | ScriptableObject | Lista mestre de todas as 38 skills |
| `Assets/ScriptableObjects/Skills/` | Assets | Um `SkillData.asset` por skill + `SkillDatabase.asset` |
| `Assets/Data/UI/Skills/` | Sprites | `skill_<nome>.png` — ícone de cada skill |
| `Assets/Editor/SkillAssetGenerator.cs` | Editor tool | Gera todos os assets via **Tools → AutoArms → Generate Skill Assets** |

### Enums
- `SkillCategory`: `CombatPassive`, `DefensePassive`, `StatBoost`, `WeaponPassive`, `Super`
- `SkillActivationType`: `Passive`, `Active`

### SkillHolder no PlayerCombat
`PlayerCombat` expõe:
- `List<SkillData> skills` — skills equipadas; visível no Inspector para testes
- `HasSkill(string name)` — retorna `true` se a skill está equipada
- `GetSkill(string name)` — retorna o `SkillData` ou `null`
- `LogSkillCheck(string name, bool triggered, string detail)` — **no-op** (logging removido do projeto, ver Logging Policy abaixo); mantido só para não quebrar os call sites existentes

### Como adicionar uma nova skill ao jogo
1. Abrir Unity → **Tools → AutoArms → Generate Skill Assets** (só necessário na primeira vez ou ao adicionar skills)
2. Encontrar o `.asset` em `Assets/ScriptableObjects/Skills/`
3. No Inspector do `PlayerCombat` de um personagem, adicionar o asset em **Skills — Teste**
4. Implementar o efeito em `PlayerCombat.cs` no método relevante (`ComboChance`, `DodgeChance`, `DisarmChance`, etc.) usando `HasSkill("Nome")`

## Logging Policy

O projeto não usa `Debug.Log`/`Debug.LogWarning` soltos pelo código — só `Debug.LogError` para falhas críticas de setup (ex: `CombatSceneLoader` sem `PlayerProfile`, `SkillDatabase` vazio em `CombatResultPanel`). Antes de adicionar um novo `Debug.Log`, prefira: (a) um `Debug.LogError` se for uma falha real, ou (b) nada — UI/popups já comunicam o resultado ao jogador.

Exceções (todas no caminho do `CombatSimulator`, quando `useSimulator=true`):
- `CombatSceneLoader.Initialize()` imprime **um** `Debug.Log(CombatLogFormatter.Format(...))` com o resumo completo da luta inteira, gerado depois de `CombatSimulator.Simulate()` e antes de `CombatPlayer.PlayCombat()` começar a tocar as animações — ver `CombatLogFormatter` abaixo.
- `CombatSimulator.Simulate()` loga `[CombatSimulator] Iniciando simulação...` na entrada e `[CombatSimulator] {n} eventos gerados` na saída — confirmação rápida de que o simulador rodou, sem precisar ler o log completo.
- `CombatSimulator.SimulateTurn` loga `[ComboChance] {nome} (P1|P2, arma=...) hit extra #{n} chance={valor}` a cada checagem do loop de combo (antes do `Roll()`) — instrumentação temporária para validar a fórmula de `ComboChance()` (base + AGI + comboBonus da arma + skills, teto 60%, decaimento ×0.5 por hit extra consecutivo). Remover quando o balanceamento estiver confirmado.
- `CombatSimulator.EmitSpeedBonus` loga `[SpeedBonus] round={n} {nome} (P1|P2) ação extra, index={i}` sempre que o popup "RAPIDO!" é emitido — instrumentação temporária para confirmar que `index` nunca é 0 (ou seja, nunca dispara na 1ª ação do round, só na 2ª em diante). Remover quando confirmado.
- `CombatSimulator.CalcDamage` loga `[CalcDamage] {nome} arma=... weaponBaseDamage=... str=... critMult=... isCrit=... resultado=...` em **todo** hit normal/combo — confirma os componentes exatos da fórmula `(weaponBaseDamage + str) × critMultiplier × sharpMult`. `CombatSimulator.CalcThrowDamage` loga `[CalcThrowDamage] arma=... weaponDamage=... str=... resultado=...` em todo arremesso, pra confirmar que o throw soma STR (`weaponDamage + str`). Remover os dois quando o balanceamento estiver confirmado.
- `CombatSimulator.ApplySpySabotage` loga `[Spy] Armas sabotadas: {nome1}, {nome2} (-20% dano).` quando a skill **Spy** sabota pelo menos 1 arma do oponente — **não** é instrumentação temporária, pedido explícito do usuário para sempre aparecer no pré-combate (ver seção própria **Spy** em Combat Systems).
- `CombatSimulator.ApplySaboteur` loga `[Saboteur] Arma destruída: {nome} | initiative do oponente -100.` quando a skill **Saboteur** destrói uma arma do oponente — mesma exceção deliberada, pedido explícito do usuário (ver seção própria **Saboteur** em Combat Systems).

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

**Tools → AutoArms → Reset All Profiles to Level 1** — além de `level`/`xpCurrent`/`xpRequired`/`battlesRemaining`, agora também: re-sorteia HP/STR/AGI/SPD via `CharacterCreation.GenerateLevel1Stats()` (mesma lógica do botão acima), limpa `profile.skills`, e reseta `profile.weaponLoadout.weapons` para as 4 armas iniciais (Satyr1, Golem3, Succubus, Zombie — guids hardcoded em `DefaultWeaponGuids`). Como cada profile tem seu próprio `WeaponLoadout` (ver tabela de ScriptableObject Assets acima), isso não afeta os outros personagens.

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

### Skills que modificam stats (aplicadas em `CombatSceneLoader.ApplySkillStats`)

Todas calculam o bônus em runtime a partir do `profile.str`/`agility`/`speed`/`maxHealth` salvo, **exceto Vitality, Herculean Strength, Feline Agility, Lightning Bolt e Reconnaissance** — essas têm um componente permanente (`+18`/`+3`/`+3`/`+3`/`+5` somado direto em `profile.maxHealth`/`str`/`agility`/`speed`/`speed` no momento da escolha, em `CombatResultPanel.ApplyBonus`, igual a um pick de Atributo) além do componente runtime (`+50%`/`+150%`, igual às outras). Se replicar esse padrão (flat permanente + percentual runtime) para outra skill no futuro, replicar a checagem por `skillName` em `ApplyBonus` também.

**Iniciativa, crit chance, crit damage, evasion, reversal, counter, combo, armor, accuracy, block e reversal after block** agora também aparecem na tela de Stats (`CharacterPanel`) — `PlayerProfile.GetEffectiveStats()` retorna uma 15-tupla (`hp, str, agility, speed, initiative, criticalChance, critDamageBonus, evasion, reversal, counter, comboChanceBonus, armor, accuracy, blockBonus, reversalAfterBlock`). `MainMenuCharacterPreview` só usa os primeiros 5 (descarta os 10 últimos). Iniciativa é sempre flat puro (nunca entra no percentual líquido): `First Strike +200`, `Monk -200`, `Reconnaissance -200`, `Deity -200`. `criticalChance` é o campo bruto do profile (nenhuma skill o modifica ainda — `Fierce Brute` no roadmap ainda não implementada). `critDamageBonus`/`comboChanceBonus` não existem como campos no profile, são 100% derivados de skill (`Reconnaissance +0.5` crit dmg; `Fists of Fury +0.20` combo). `evasion`/`reversal`/`counter`/`armor`/`accuracy`/`blockBonus`/`reversalAfterBlock` são campos reais do profile (`Untouchable +0.30`/`Ballet Shoes +0.10` somam em evasion; `Deity +0.40`/`Hostility +0.30` somam em reversal, e Deity ainda soma `-100%` multiplicativo em evasion, aplicado depois das somas — ver linha da Deity abaixo; `Monk +0.40`/`Sixth Sense +0.10` somam em counter; `Armour +0.25`/`Extra Thick Skin +0.50`/`Toughened Skin +0.10` somam em armor; `Relentless +0.30` soma em accuracy; `Counter Attack +0.10` soma em blockBonus e `+0.90` em reversalAfterBlock).

**Quem age primeiro**: `CombatSimulator.SimulateRound` e `AttackSequencer.StartWhenReady` comparam iniciativa primeiro; **em empate** (default 0 pra todo personagem sem skill que a altere), quem tem mais `speed` age primeiro — antes não existia esse tie-break por speed, P1 sempre ganhava o empate de iniciativa independente de speed.

**Dano crítico com bônus de skill**: novo campo `critDamageBonus` (`PlayerState`/`PlayerCombat`, default 0) somado ao `critDamageMultiplier` da arma (ou `UnarmedStats.CritDamageMultiplier` se desarmado) em `CritDamageMultiplier()`/`CombatSimulator.CritDamageMultiplier()` — `+50%` de dano crítico (Reconnaissance) soma `+0.5` ali, igual ao padrão de `criticalChance` (bônus de chance) já existente.

**Stacking de percentuais**: quando mais de uma skill afeta o mesmo status (HP/STR/AGI/SPD), os percentuais são **somados num percentual líquido e aplicados uma única vez no final** (`hpPct`/`strPct`/`agiPct`/`spdPct` acumulados, depois `RoundToInt(valor × (1 + pct))`) — não multiplicação sequencial com arredondamento a cada skill. Isso evita resultados não-intuitivos por arredondamento em cascata: STR 9 com Herculean Strength (+50%) + Immortal (-25%) dá `9 × (1 + 0.5 - 0.25) = 9 × 1.25 = 11.25 → 11`. A versão antiga (×1.5 depois ×0.75, cada um arredondando o resultado da anterior) dava `9→14→10` (`RoundToInt` usa round-half-to-even: 13.5→14, depois 10.5→10) — surpreendia o jogador, que esperava somar os dois percentuais direto. Implementado em `PlayerProfile.GetEffectiveStats()`, `CombatSceneLoader.ApplySkillStats()` e `CombatSimulator.ApplySkillStats(PlayerState)` — as três cópias mantidas em sincronia.

| Skill | Modificações |
|---|---|
| Vitality | `profile.maxHealth += 18` **permanente**, aplicado uma única vez na escolha (`CombatResultPanel.ApplyBonus`, igual a um pick de Atributo) + `hpPct += 0.5` em runtime sobre esse valor já somado (mesmo padrão de Herculean/Feline/Lightning, só que em HP). |
| Herculean Strength | `profile.str += 3` **permanente**, aplicado uma única vez na escolha (`CombatResultPanel.ApplyBonus`, igual a um pick de Atributo) + `strPct += 0.5` (entra no percentual líquido de STR, sem penalidade de agilidade). Ex: STR 5 → escolhe a skill → `profile.str` vira 8 (permanente) → efetivo `8 × 1.5 = 12`. Se depois pegar +2 STR de atributo: `profile.str` vira 10 → efetivo `10 × 1.5 = 15`. |
| Feline Agility | `profile.agility += 3` **permanente**, aplicado uma única vez na escolha (`CombatResultPanel.ApplyBonus`, igual a um pick de Atributo) + `agiPct += 0.5` em runtime sobre esse valor já somado (mesmo padrão da Herculean Strength, só que em AGI). |
| Lightning Bolt | `profile.speed += 3` **permanente**, aplicado uma única vez na escolha (`CombatResultPanel.ApplyBonus`, igual a um pick de Atributo) + `spdPct += 0.5` em runtime sobre esse valor já somado (mesmo padrão de Herculean/Feline, só que em SPD — agora afeta o atributo `speed` real, ações extra no Speed System; antes afetava só `runSpeedMultiplier`, a velocidade da animação de correr, sem relação com ações extra). |
| Reconnaissance | `profile.speed += 5` **permanente** na escolha + `spdPct += 1.5` (+150%) em runtime sobre esse valor já somado + `initiative -= 200` (flat puro) + `critDamageBonus += 0.5` (+50% dano crítico, somado ao `critDamageMultiplier` da arma). |
| Immortal | `hpPct += 2.5` (+250%), `strPct -= 0.25`, `agiPct -= 0.25`, `spdPct -= 0.25` |
| Deity | `hpPct += 1.0` (+100%), `strPct += 1.0` (+100%), `agiPct -= 1.0` (-100%), `spdPct -= 0.90` (**-90%, não -100%** — speed fixo em 0 travava o player sem chance de ação própria nem de pegar arma; com -90% ainda existe chance de arredondar > 0 dependendo do speed base), `evasionPct -= 1.0` + `noEvasion = true` (-100% de verdade, "Dexterity" da descrição original — não significa nunca ser desarmado, significa sem resistência a ser atingido; mapeado pro campo `evasion` existente, não um campo "dexterity" novo — ver nota de `noEvasion` na seção **Counter e Reversal**), `reversal += 0.40`, `initiative -= 200`. Sem componente permanente (só percentuais + flat de iniciativa/reversal). Ver seção **Counter e Reversal** acima pro que `reversal` realmente faz agora. |
| Armour | `armor += 0.25` (flat, fora do percentual líquido — `armor` não é um dos quatro status que stackeiam) + `spdPct -= 0.15` (-15% SPD, entra no percentual líquido normalmente) |
| Extra Thick Skin | `armor += 0.50` |
| Toughened Skin | `armor += 0.10` |
| Untouchable | `evasion += 0.30` |
| Bodybuilder | `heavyDexterityBonus += 0.10`, `heavyHitSpeedBonus += 0.40` — **não afeta mais STR** (era `strPct += 0.5`/"STR × 1.5", redefinida pelo usuário). Informativo apenas no preview: o bônus real só vale enquanto empunha arma Heavy, checado vivo (`HasType(currentWeaponData, Heavy)`) em `DodgeChance`/`HitRoutine`/`CombatPlayer`, não fixado aqui. |
| Relentless | `accuracy += 0.30` (ver **Dodge** — reduz a esquiva do defensor; era `comboChanceBonus += 0.15`, redefinida pelo usuário) |
| Fists of Fury | `comboChanceBonus += 0.20` |
| Lead Skeleton | `leadSkeleton = true` |
| Ballet Shoes | `evasion += 0.10`, `firstHitAvoided = true` |
| First Strike | `initiative += 200` |
| Counter Attack | `blockBonus += 0.10`, `reversalAfterBlock += 0.90` (ver **Counter e Reversal** acima — era `counter += 0.40`/cancela hit antes de conectar, redefinida pelo usuário; Monk/Sixth Sense herdaram o papel de dar counter) |
| Sixth Sense | `counter += 0.10` — mesmo campo de Monk |
| Hostility | `reversal += 0.30` |
| Monk | `counter += 0.40`, `initiative -= 200`, `hitSpeed = 0` |
| Martial Arts | `martialArts = true` — dobra `UnarmedStats.Damage` em `WeaponBaseDamage()`/`WeaponBaseDamage(attacker)` |
| Shock | `disarmChanceBonus += 0.50` (soma em `DisarmChance()`) |
| Weapon Master | `weaponsMaster = true` — habilita `sharpMult = 1.5` em `CalcDamage` quando a arma tem a tag Sharp (ver **Fórmula de Dano**) |
| Shield | `blockBonus += 0.45` (+45% block rate, mesmo campo de Counter Attack), `armor += 0.25` (-25% dano recebido, penalidade de mobilidade do escudo), `hasShield = true`. Visual permanente equipado em `CombatSceneLoader.Initialize` via `WeaponHandler.EquipShield(shieldWeaponData)` no `offHandBone` (braço oposto ao `handBone`, ex.: `handBone = Left Arm` → `offHandBone = Right Arm`) — fora do `WeaponLoadout`, nunca entra no ciclo de troca de armas. Ver **Desarmar do Escudo** abaixo. |
| Determination | Não altera nenhum stat em `ApplySkillStats` — checada vivo via `HasSkill("Determination")` em `SimulateHitWithDetermination` (ver **Determination** abaixo), mesmo padrão de Iron Head (sem flag cacheada). |
| Hideaway | Não altera nenhum stat em `ApplySkillStats` — checada vivo via `HasSkill("Hideaway")` em `ThrowChance`/`SimulateThrow` (ver **Hideaway** abaixo), mesmo padrão de Iron Head/Determination. |
| Sticky Hands | `stickyHands += 0.50` (campo cacheado, mesmo padrão antigo de `disarmChanceBonus`/`blockBonus` — diferente de Hideaway/Determination acima) — multiplica `DisarmChance()` e `ThrowChance()` por `(1 - stickyHands)` (ver **Sticky Hands** abaixo). |

> `accuracy` agora tem mecânica real (Relentless +0.30, ver **Dodge** em Combat Systems). `reversal` agora tem mecânica real, ver **Counter e Reversal** acima.
> `hitSpeed = 0` (Monk): `HitRoutine`/`SimulateHit` sai cedo — personagem guarda em vez de atacar. Esse guard precisa ser checado **antes** de qualquer outra coisa no próprio turno do atacante — três pontos tinham esse guard ausente ou fora de ordem, fazendo Monk ainda se mover/atacar visualmente em alguns turnos (bug reportado: "saltos quando não deveria se mexer"): (1) o check de Throw em `SimulateTurn`/`AttackRoutine` não olhava pra `hitSpeed`, deixando Monk arremessar arma normalmente; (2) `StrikeRoutine` corria até o adversário antes de `HitRoutine` sair cedo; (3) em `SimulateHit`/`HitRoutine`, o auto-dodge de Ballet Shoes (`defender.firstHitAvoided`) era checado **antes** do guard do Monk — Monk "atacava" (CombatPlayer reposicionava + golpe) só pra ver o oponente Ballet Shoes esquivar de um golpe que o Monk nunca deveria ter desferido, e o jump-back de `TurnEnd`/`ReturnToSpawn` disparava depois só por causa desse deslocamento indevido. Todos os três agora checam `hitSpeed > 0f`/`hitSpeed <= 0f` primeiro, nos dois caminhos (`CombatSimulator.cs` e `PlayerCombat.cs`).
>
> **Pulinhos residuais no próprio turno do Monk (4º ponto, achado depois)**: mesmo com os três fixes acima, Monk ainda recebia um jump-back ocasional no PRÓPRIO `TurnEnd`, sem nenhuma ação visível naquele turno (bug real reportado pelo usuário). Causa: `CombatPlayer.ExecuteEvent`'s case `TurnEnd` só pulava o jump-back se o atacante já estivesse `InSpawnZone` — isso cobre Monk não correr no próprio turno, mas não cobre ele ser empurrado **pra fora** da zona por knockback enquanto *defende* nos turnos do adversário (tomar hit, ser knockbackado num bloqueio, etc.). Como Monk nunca corre de volta sozinho, no turno seguinte dele `InSpawnZone` ainda dava falso, disparando um jump-back pra um ponto aleatório sem nenhuma ação correspondente no log. Fix: o guard agora é incondicional por `hitSpeed > 0f`, não só pela zona — Monk nunca jump-back no próprio `TurnEnd`, ponto final, já que ele é um guarda estacionário que nunca decide se reposicionar por conta própria.

## Third-Party Plugins

- **Spriter2UnityDX** (`Assets/Spriter2UnityDX/`) — Converts Spriter `.scml` files to Unity prefabs/animators. Character prefabs use its `EntityRenderer` and `TextureController` runtime components.
- **TextMesh Pro** — Used throughout UI; assets in `Assets/TextMesh Pro/`.

## Assets
- **CraftPix.net** — todos os assets visuais do jogo (personagens, ícones, backgrounds, GUI) foram adquiridos com licença comercial. Licença permite: uso comercial, modificação, distribuição em jogos. Proibido: revender arquivos fonte, usar para treinar IA. Referência: https://craftpix.net/file-licenses/

## Game Vision

AutoArms é inspirado no My Brute (jogo browser francês de 2008 da Motion Twin).
Referência jogável: https://brute.eternaltwin.org/

### Conceito central
- Combate automático entre dois personagens — o jogador não controla as ações, apenas monta o personagem
- Progressão por XP e level: ao subir de nível, o jogador escolhe 1 bônus (atributo, skill ou arma)
- Personagem com stats aleatórios ao criar: vida, força, agilidade, velocidade
- Limite de batalhas por dia (energia) — incentiva retorno diário

### Mecânicas de combate inspiradas no My Brute
- Combo: chance de atacar mais de uma vez seguida
- Crítico: chance de causar dano dobrado
- Esquiva: chance de desviar do ataque baseada em agilidade
- Parry: chance de bloquear o dano com arma ou escudo
- Knockback: ao tomar hit, personagem recua levemente
- Jogar arma: chance de arremessar a arma no adversário
- Derrubar arma: chance de desarmar o adversário no golpe
- Troca de arma: personagem troca de arma durante o combate

### Tipos de arma inspirados no My Brute (26 armas no original)
- Fast: maior chance de ataque extra, mais difícil de esquivar
- Slow: menor chance de ataque duplo e bloqueio
- Heavy: alto dano, penalidade de velocidade
- Thrown: pode ser arremessada no adversário
- Block: chance de bloquear dano recebido

### Pets planejados
- Cachorro — meat shield inicial, combatente fraco
- Lobo — versão mais forte do cachorro
- Águia — ataque à distância
- Urso — mais poderoso, alta vida própria

### Progressão e XP
- Vitória: +3 XP
- Derrota: +1 XP
- XP necessário por nível: level × 20 (ex: level 2→3 = 40 XP)
- Ao subir de nível: escolher 1 entre 3 opções sorteadas (atributo, skill ou arma)

### Monetização planejada
- Diamantes: moeda premium
- Energia: comprar recargas para lutar mais vezes
- Personagens: desbloquear com diamante

### Código Fonte de Referência
- LaBrute (remake open source do My Brute): https://github.com/Zenoo/labrute
- Pasta de lógica de combate: core/src/
- IMPORTANTE: Licença PolyForm Noncommercial — estudar lógica apenas, não copiar código
- Quando implementar uma skill ou mecânica, consultar o repositório para entender a lógica original

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

### Roadmap de implementação das Skills (Fase 2.5)

- [x] Infraestrutura base do sistema de skills (SkillData, SkillDatabase, SkillHolder no PlayerCombat, SkillAssetGenerator)

#### Passivas de Combate
- [x] Relentless — +30% accuracy, reduz a esquiva do adversário (accuracy += 0.30, oposto de evasion — era +15% combo chance/comboChanceBonus += 0.15, redefinida pelo usuário; Fists of Fury herdou o papel de dar combo chance)
- [x] Fists of Fury — +20% combo chance (comboChanceBonus += 0.20 — não é mais "combo de socos desarmado melhorado", movida de Passivas de Armas pra aqui, mecânica definida pelo usuário)
- [x] Counter Attack — +10% block, +90% reversal exclusivo de depois de bloquear (blockBonus += 0.10, reversalAfterBlock += 0.90 — era +40% counter rate/cancela hit antes de conectar, redefinida pelo usuário; ver Counter e Reversal)
- [ ] Impact — +15% disarm (ajustar DisarmChance())
- [ ] Pugnacious — chance de contra-atacar após levar dano (mecânica já existe via `reversal`/Reversal — ver Counter e Reversal acima; só falta criar o SkillData "Pugnacious" que soma nesse campo, mesmo padrão da Deity)
- [x] Sixth Sense — +10% counter rate (counter += 0.10, mesma mecânica do Monk — não é mais esquiva, mecânica definida pelo usuário)
- [x] Hostility — +30% reversal (reversal += 0.30, mecânica definida pelo usuário — não é mais "equipa a arma mais forte primeiro", movida de Passivas de Armas pra aqui por ser uma mecânica de combate, não de arma)
- [x] Monk — +40% counter rate, -200 iniciativa, nunca ataca (guarda) (counter += 0.40, initiative -= 200, hitSpeed = 0; ver **Pegar Arma/Combat Systems → `hitSpeed = 0` (Monk)** pro fix do guard que precisava ser checado antes de Throw/Ballet Shoes/Run pra ele nunca se mover no próprio turno)
- [x] Shock — +50% chance de desarmar o adversário a cada ataque (disarmChanceBonus += 0.50, soma em `DisarmChance()`)
- [x] Iron Head — +40% chance de derrubar a arma do atacante ao sofrer um hit (qualquer hit, incluindo combo) e interrompe o resto do combo do atacante (mesmo `interrupted = true` do Counter — ver **Counter e Reversal**)
- [ ] Sabotage — remove permanentemente uma arma do adversário ao acertar
- [x] Saboteur — skill do LaBrute (distinta de Sabotage acima): antes da luta, destrói 1 arma aleatória do loadout do oponente e dá -100 iniciativa nele; ver seção própria **Saboteur** em Combat Systems
- [x] Thief — 44% por turno de roubar a arma do oponente (se eu estiver desarmado e ele armado), até 2x por luta (`thiefUsesRemaining`); redefinida pelo usuário — era "ao acertar" no roadmap original, agora é ação de início de turno, igual ao pickup comum; ver seção própria **Thief** em Combat Systems pro visual (pula nas costas do adversário, balançam juntos 4x, desce com a arma)
- [x] Untouchable — +30% evasion (era 25%, rebalanceada pelo usuário)
- [x] First Strike — +200 initiative (ataca primeiro)
- [x] Chaining — 3 golpes melee consecutivos sem tomar dano estunam o adversário por 1 ação (não fazia parte do roadmap original, mecânica definida do zero pelo usuário); ver seção própria **Chaining** em Combat Systems

#### Passivas de Defesa
- [x] Shield — +45% block rate (`blockBonus += 0.45`), +25% armor (`armor += 0.25`), visual permanente no braço oposto (`WeaponHandler.EquipShield`, fora do `WeaponLoadout`), desarme próprio com chance fixa de 10% que não soma `disarmChanceBonus`/`disarmBonus` (ver **Desarmar do Escudo** em Combat Systems)
- [x] Armour — +25% armor, -15% velocidade (era armor += 0.30 sem penalidade nenhuma; rebalanceada pelo usuário pra ter um trade-off)
- [ ] Iron Skin — reduz dano fixo por hit
- [x] Lead Skeleton — -15% dano de armas Heavy (leadSkeleton = true)
- [x] Extra Thick Skin — armor += 0.50 (50% redução de dano)
- [x] Toughened Skin — +10% armor (armor += 0.10, mecânica definida pelo usuário; skill não tinha SkillDef no gerador antes, adicionada do zero junto da implementação)
- [x] Survival — sobrevive com 1 HP uma vez por luta (`survivalUsed`, ver seção própria **Survival** em Combat Systems), +20% evasion/+20% block enquanto em 1 HP
- [x] Ballet Shoes — evasion +10%, primeiro golpe automaticamente esquivado
- [x] Resistant — nenhum hit isolado reduz mais que 25% do HP máximo (cap no dano bruto, antes de Lead Skeleton/armadura — não tinha essa entrada no roadmap, adicionada agora; ver seção própria **Resistant** em Combat Systems)
- [x] Sticky Hands — stickyHands = 0.50, reduz 50% chance de ser desarmado e 50% chance de throw acidental (próprio); ver seção própria **Sticky Hands** em Combat Systems

#### Passivas de Stats
- [x] Vitality — +18 HP permanente, +50% HP
- [x] Bodybuilder — str × 1.5
- [x] Herculean Strength — +3 STR permanente, +50% STR
- [x] Feline Agility — +3 AGI permanente, +50% AGI
- [x] Lightning Bolt — +3 SPD permanente, +50% SPD (atributo speed real, ações extra no Speed System — era runSpeedMultiplier × 1.5, velocidade de animação de correr)
- [x] Immortal — maxHealth × 3.5 (+250%), str/agility/speed × 0.75 (-25% cada)
- [x] Reconnaissance — +5 SPD permanente, +150% SPD, -200 initiative, +50% dano crítico (mecânica definida pelo usuário, não fazia parte do roadmap original)
- [x] Deity — +100% HP/STR, -100% AGI/evasion, -90% SPD, -200 initiative, +40% reversal (mecânica definida pelo usuário; introduziu a mecânica real de Counter/Reversal, ver seção própria em Combat Systems)
- [x] Determination — se o golpe não causa dano (esquiva/bloqueio/Counter do defensor), 60% de chance de tentar outro golpe imediatamente, recursivo até acertar ou falhar o roll (redefinida pelo usuário — era "+STR conforme perde HP" no roadmap original; ver seção própria **Determination** em Combat Systems)

#### Passivas de Armas
- [x] Weapon Master — +50% dano com arma afiada (tag Sharp) (`weaponsMaster = true`, ver `sharpMult` em **Fórmula de Dano**; era "+dano com qualquer arma" no roadmap original, redefinida pra só Sharp)
- [x] Martial Arts — +100% dano desarmado (`martialArts = true`, dobra `UnarmedStats.Damage` em `WeaponBaseDamage()`; era "combo de socos desarmado melhorado" no roadmap original, redefinida pelo usuário)
- [ ] Strong Arm — +dano com armas Heavy
- [ ] Master of Arms — +dano com armas Melee
- [ ] Weapon Tampering — reduz dano das armas inimigas
- [x] Hideaway — 50% chance de arremesso fixa (mesmo gate de throw-ou-melee de todo mundo, sem branch forçado), +25% bloqueio contra arremessos recebidos (reduz hit de 80% pra 55%), arma some da mão mas continua no loadout (não desaparece); ver seção própria **Hideaway** em Combat Systems
- [x] Spy — metade das armas do oponente (aleatórias) recebem -20% dano permanente antes do combate; os ícones das armas sabotadas ficam vermelhos no `WeaponHUD`. Skill exclusiva do LaBrute/eternaltwin, não existe no Muxxu original; ver seção própria **Spy** em Combat Systems

#### Supers (ativas — usadas X vezes por luta)
- [ ] Fierce Brute — dano duplo no próximo hit (1x por luta)
- [ ] Tragic Potion — recupera HP (1x por luta)
- [ ] Hammer — golpe massivo de dano (1x por luta)
- [ ] Flash Flood — dano em área (1x por luta)
- [ ] Net — imobiliza o adversário (1x por luta)
- [ ] Hypnosis — adversário ataca a si mesmo (1x por luta)
- [ ] Bomb — explosão de dano alto (1x por luta)
- [ ] Cry of the Damned — reduz stats do adversário (1x por luta)

#### Relacionadas a Pets
- [ ] Tamer — pets mais fortes e com mais HP

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
- [ ] Adicionar novos prefabs de personagens guardados
- [ ] Atributos aleatórios ao criar personagem level 1 (vida, força, agilidade, velocidade)
- [ ] Habilidades inspiradas no My Brute
- [ ] Criar habilidades originais adicionais

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
- [x] Curva de XP não linear: (level+1)×(level+2) — nível 1→2=6, 2→3=12, 3→4=20...
- [x] Tela de fim de combate com resultado e XP ganho
- [x] CombatSimulator: pré-cálculo determinístico de todo o combate (CombatEvent, PlayerState, CombatSimulator, CombatPlayer)
- [x] Botão de velocidade toggle (2x ↔ 1x, com cor indicando estado) e Skip no CombatHUD (controlam CombatPlayer)
- [x] Ao subir de nível: escolher atributo, skill ou arma

### Fase 3 — Armas & Pets
- [ ] Criar mais armas com sprites e stats (tipos: Fast, Slow, Heavy, Thrown, Block)
- [ ] Sistema de raridade de armas
- [ ] Pets: cachorro, lobo, águia, urso

### Fase 4 — Monetização
- [ ] Sistema de diamantes (moeda premium)
- [ ] Sistema de energia com limite diário de batalhas
- [ ] Compra de energia e personagens com diamante
- [ ] Precificação dos pacotes
- [ ] **Reset de Level Up**: ao subir de nível, o jogador vê as 2 opções de escolha normalmente (`ShowLevelUpChoice`). Um botão "Resetar opções" permite rerolar as opções por um custo em diamantes. Cada reset dobra o custo do próximo: 1º reset = X diamantes, 2º = 2X, 3º = 4X, e assim por diante. O custo base X ainda precisa ser definido com base no balanceamento da economia. O reset regenera novas opções aleatórias seguindo as mesmas regras de peso (60% atributo, 30% skill, 10% arma). O contador de resets zera ao fechar o painel de level up.
- [ ] **Reset de Build**: o jogador pode pagar diamantes para resetar todos os atributos e skills ganhos por level up, voltando aos stats base do nível atual e redistribuindo os pontos manualmente. Custo fixo alto ou progressivo por nível. Permite experimentar builds diferentes sem criar um novo personagem.

### Fase 5 — Endgame & Social
- [ ] Mapa PVE
- [ ] Torneio com premiação
- [ ] Torneios 2v2 e 3v3
- [ ] Sistema de discípulos (recrutar amigos = bônus XP)
- [ ] Guildas

### Fase 6 — Infraestrutura
- [ ] Criar cena 03_SelectWeapons (já referenciada no código)
- [ ] Definir banco de dados para salvar personagens (Firebase ou PlayFab)
- [ ] Integrar persistência de dados online

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

### Fase 8 — Segurança
- [ ] Nunca armazenar dados críticos (XP, level, diamantes) só localmente — sempre validar no servidor
- [ ] Validação server-side de compras (Google Play Billing / Apple StoreKit / Steam)
- [ ] Ofuscar código C# com ferramentas como Obfuscator-ILLVM ou Beebyte
- [ ] Não expor API keys no código — usar variáveis de ambiente ou Unity Cloud
- [ ] Calcular resultado do combate no servidor (anti-cheat)
- [ ] Rate limiting nas chamadas de API para evitar abuso
- [ ] Autenticação segura do jogador (Firebase Auth ou PlayFab Auth)
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

### Progresso
- Total: 93 tarefas | Concluídas: 44
- Última atualização: 2026-06-20 (4 bugs corrigidos a pedido do usuário, todos na skill **Saboteur**: (1) ordem invertida em `CombatSimulator.Simulate()` — `ApplySaboteur` agora roda **antes** de `ApplySpySabotage` (era depois), pra Spy calcular "metade do loadout" já em cima do que sobrou depois da destruição do Saboteur, em vez de arriscar sabotar (gastar -20% dano) numa arma que o Saboteur ia destruir de qualquer jeito; (2) a queda visual da arma destruída (do ícone na `WeaponHUD` até o chão, ver atualização anterior) não aparecia — `sortingOrder` da arma caída empatava com o fundo da arena (`Colosseum arena`, mesma layer `Default`/ordem 0), renderizando atrás dele por causa do empate no critério de profundidade da câmera; subido pra `sortingOrder = 1`. Também adicionado `Canvas.ForceUpdateCanvases()` antes de ler a posição do ícone (`RectTransform.position` dentro de um `HorizontalLayoutGroup` só reflete o valor final depois de um passe de layout) e um `Debug.LogError` de diagnóstico se a queda não puder ser montada; (3) a arma caída renderizava grande demais — `Vector3.one * data.scale` ignorava a escala raiz do personagem (~0.3), que normalmente entra via `inHand.transform.lossyScale` em `DropWeapon`/`DropShield` (sem objeto na mão aqui pra ler essa escala); corrigido multiplicando manualmente por `victim.transform.lossyScale`; (4) a arma às vezes caía fora da arena — o X da queda (fixo, só Y muda por gravidade) vinha sem nenhum limite da conversão tela→mundo do ícone, podendo cair fora da área jogável se o ícone estivesse perto da borda da tela; corrigido clampando só o X em `DropWeaponFromHud` nos mesmos `±7.25` de `ClampToArena` (sem usar `ClampToArena` inteiro, que também clamparia a altura inicial da queda pro intervalo de posição de personagem, bem mais baixo))
- Última atualização anterior: 2026-06-20 (Dois ajustes de skill pedidos pelo usuário: (1) **Hideaway corrigida pra valores oficiais do LaBrute** — revertida a versão intermediária (arremesso forçado a 100%, nunca melee, arma nunca saía da mão); agora é 50% de chance de arremesso fixa via o mesmo `Roll(ThrowChance())` de todo mundo (sem branch forçado nem combo especial — removidos `SimulateHideawayThrowCombo` e o item 3 antigo de `SimulateTurn`), +25% bloqueio contra arremessos reduz a chance de acerto direto (80%→55%, removido o `ThrowBlockChance`/`CombatEventType.Block(isThrow=true)` dedicado), e a arma agora sai da mão ao arremessar (`Unequip()`, igual a uma arma Thrown normal) mas continua no loadout. (2) **Skill Sticky Hands implementada do zero** — novo campo numérico `stickyHands` (`PlayerState`/`PlayerCombat`, `+= 0.50f` em `ApplySkillStats` quando a skill está presente, mesmo padrão antigo de `disarmChanceBonus`) multiplica `DisarmChance()` (agora recebe `defender` também) e `ThrowChance()` por `(1 - stickyHands)` — -50% chance de ser desarmado e -50% chance de arremesso (incluindo o próprio, acidental). `SkillDef` nova em `SkillAssetGenerator.cs` (categoria DefensePassive) e descrição do Hideaway atualizada — precisa rodar **Tools → AutoArms → Generate Skill Assets**)
- Última atualização anterior: 2026-06-20 (Bug corrigido a pedido do usuário: a arma destruída pela skill **Saboteur** desaparecia do simulador mas o ícone continuava visível na `WeaponHUD` — agora cai do próprio ícone até o chão, igual a um Disarm normal. Novos `WeaponHUD.GetIconScreenPosition`/`RemoveWeapon` e `PlayerCombat.DropWeaponFromHud` (mesma queda em pêndulo de `DropWeapon`, mas começando da posição do ícone em tela convertida pra mundo via `Camera.main.ScreenToWorldPoint`, já que a arma nunca foi equipada). `CombatPlayer` ganhou `p1WeaponHUD`/`p2WeaponHUD`, wireados por `CombatSceneLoader`)
- Última atualização anterior: 2026-06-20 (Skill **Saboteur** implementada do zero — do LaBrute, distinta da Sabotage já listada no roadmap (que continua não implementada): antes do primeiro turno, destrói 1 arma aleatória do loadout do oponente (`RemoveAt`, sem clonar — diferente de Spy, aqui a arma simplesmente deixa de existir, sem dano nenhum pra preservar) e dá -100 initiative nele. Novo `CombatSimulator.ApplySaboteur`, chamado nas duas direções logo depois do Spy, no mesmo ponto (antes de `EquipStartingWeaponIfNeeded`). Novo `CombatEventType.Saboteur` (emitido junto da sabotagem, antes do primeiro `TurnStart` da luta) — `CombatPlayer` mostra `DamagePopup.SpawnSabotage` (novo, laranja, "SABOTAGE!", mesmo estilo do Disarm) acima da vítima logo no início da animação. Log `[Saboteur] Arma destruída: ... | initiative do oponente -100.` é exceção deliberada à Logging Policy, pedido explícito do usuário. `SkillDef` nova em `SkillAssetGenerator.cs` (categoria CombatPassive) — precisa rodar **Tools → AutoArms → Generate Skill Assets**)
- Última atualização anterior: 2026-06-20 (Skill **Spy** implementada do zero — exclusiva do LaBrute/eternaltwin, sem equivalente no Muxxu original: antes do primeiro turno, metade do loadout do oponente (arredondado pra baixo, sorteio aleatório sem reposição) recebe -20% de dano permanente pro resto da luta. Novo `CombatSimulator.ApplySpySabotage`, chamado nas duas direções logo depois de `BuildState`, antes até de `EquipStartingWeaponIfNeeded`. Clona a `WeaponData` via `Object.Instantiate` em vez de mutar o asset original (`victim.weaponLoadout` guarda a mesma referência do ScriptableObject em disco — mutar direto corromperia a arma pra qualquer outra luta/personagem que a use) e substitui a entrada no `PlayerState.weaponLoadout`, então qualquer pickup/swap/roubo dessa arma depois já usa o dano reduzido automaticamente. Novos `CombatSimulator.Player1SabotagedWeapons`/`Player2SabotagedWeapons` (nomes, não referências — o `WeaponHUD` visual nunca vê os clones do simulador) lidos por `CombatSceneLoader` depois de `Simulate()` e passados pro novo `WeaponHUD.SetSabotagedWeapons()`, que pinta os ícones sabotados vermelhos (`UpdateHighlight` ganhou um 3º estado de cor: ativo dourado > sabotado vermelho > padrão preto). Log `[Spy] Armas sabotadas: ...` é exceção deliberada (não temporária) à Logging Policy, pedido explícito do usuário. `SkillDef` nova em `SkillAssetGenerator.cs` (categoria WeaponPassive) — precisa rodar **Tools → AutoArms → Generate Skill Assets**)
- Última atualização anterior: 2026-06-19 (Skill **Chaining** implementada do zero — 3 golpes melee consecutivos sem o atacante tomar dano estunam o defensor por 1 ação (`PlayerState.chainHitStreak`/`stunnedActions`, novos eventos `CombatEventType.Stunned`/`StunSkip`). Streak incrementado no ponto onde `SimulateHit` aplica dano normal de verdade, zerado em `ApplyDamage` (cobre dano de qualquer origem — Hit/Counter/Reversal/Throw — já que os 4 pontos do arquivo passam por esse método único); Miss/Dodge/Block não resetam, só dano de fato. Stun consumido no topo de `SimulateTurn`, pulando a ação inteira (Thief/pickup/swap/throw/melee, tudo). Visual a pedido do usuário (placeholder, sprite fica pra depois): novo `PlayerCombat.ShowStunLabel`/`HideStunLabel` — label `TextMeshPro` persistente "ATORDOADO!" acima da cabeça + `StunHurtLoop` (refaz `SetTrigger("Hurt")` a cada 0.3s, já que Hurt não tem bool de "hold" no Animator, diferente de Throwing/Slashing). `SkillDef` nova em `SkillAssetGenerator.cs` (categoria CombatPassive) — precisa rodar **Tools → AutoArms → Generate Skill Assets**)
- Última atualização anterior: 2026-06-19 (Dois ajustes em Hideaway/Troca de Arma, ambos reportados pelo usuário: (1) combo depois do arremesso forçado de Hideaway ainda virava melee (chamava `SimulateComboLoop`) — contradizia a própria skill, "nunca vai pro corpo a corpo enquanto tiver arma"; novo `SimulateHideawayThrowCombo` rearremessa em vez de fazer melee a cada hit extra do combo, mesma fórmula/decaimento de `ComboChance()`. (2) Troca de Arma não deve acontecer no 1º turno de quem já nasce armado (`EquipStartingWeaponIfNeeded`, 40% antes da luta) — novo `PlayerState.hasTakenFirstTurn`/`isFirstTurn` em `SimulateTurn`, usado só pra gatear o swap (item 2b); o resto do turno continua normal)
- Última atualização anterior: 2026-06-19 (Hideaway corrigida de novo — a chance de arremesso de 50% fixa estava errada; lógica correta, redefinida pelo usuário: **arremesso forçado a 100%** sempre que estiver armado, independente do tipo da arma — nunca vai pro melee. Checado no topo de `SimulateTurn`, sem rolar `ThrowChance()` (que voltou a ser só pra quem não tem a skill). Se `ComboChance()` disparar depois do throw, o personagem continua com combo melee do lugar onde está, sem correr de volta ao spawn — novo método `SimulateComboLoop` (extraído do loop que antes só existia inline no melee normal) reusado nos dois caminhos. Ação extra por Speed já funciona automaticamente (cada uma é só outra chamada de `SimulateTurn`, refaz o forçamento do zero). Sem arma equipada, Hideaway não tem efeito nenhum — pickup/swap continuam nos mesmos 40% de sempre, sem influência da skill)
- Última atualização anterior: 2026-06-19 (Dois bugs corrigidos no que tinha acabado de ser implementado, ambos reportados pelo usuário: (1) Hideaway — "a arma não desaparece" tinha sido interpretado errado como "continua disponível no loadout pra ser sorteada de novo depois"; o correto é que a arma **nunca sai da mão de quem arremessa** — `attacker.currentWeaponData` não é mais zerado em `SimulateThrow` quando `keepsWeapon` (Hideaway), e `Unequip()`/`UnequipPermanent()` nem são chamados no visual (`CombatPlayer`'s case `ThrowWeapon`, que antes nem checava a skill e desequipava incondicionalmente — bug de mirror entre simulador e visual); só a réplica voadora (`FlyingWeapon`, já existente) representa o arremesso. (2) Troca de Arma (Weapon Swap) — o entendimento original também estava invertido: a arma trocada DEVE desaparecer do loadout/`WeaponHUD` permanentemente (não pode ser sacada de novo), igual a `WeaponDrop`/`Disarm`; revertido o parâmetro `removeFromLoadout` de `PlayerCombat.DropWeapon` (removido, nunca precisou existir) e adicionado `attacker.weaponLoadout.Remove(oldWeapon)` no simulador, que faltava)
- Última atualização anterior: 2026-06-19 (Roadmap corrigido: **Resistant** (implementada há algumas atualizações) nunca tinha sido adicionada como item do roadmap em **Passivas de Defesa** — corrigido agora, junto com a atualização abaixo)
- Última atualização anterior: 2026-06-19 (Skill **Hideaway** implementada (3 efeitos, mecânica oficial do My Brute): +25% chance de bloquear arremessos — mecânica nova, `ThrowBlockChance`, não existia nenhum Block contra Throw antes (só 80% hit/20% miss); 50% de chance de arremesso fixa, substitui a soma por tag em `ThrowChance`; arma arremessada não desaparece do loadout mesmo sem a tag Thrown. Novo evento `Block` com `isThrow = true` quando bloqueia um arremesso — `CombatPlayer` pula o swing/reposicionamento do atacante nesse caso (mesmo padrão do `Hit`). Junto, implementada a pedido do usuário uma mecânica **geral** (não-skill, vale pra todo mundo): **Troca de Arma** — se armado no início do turno, mesma chance de 40% do pickup normal de trocar a arma atual por uma nova aleatória do loadout, sem a antiga desaparecer (novo parâmetro `removeFromLoadout` em `PlayerCombat.DropWeapon`, novo evento `CombatEventType.WeaponSwap`). `SkillDef` do Hideaway adicionada do zero em `SkillAssetGenerator.cs` (categoria WeaponPassive) — precisa rodar **Tools → AutoArms → Generate Skill Assets**)
- Última atualização anterior: 2026-06-19 (Bug corrigido: dano de arremesso (`CombatSimulator.CalcThrowDamage`/`PlayerCombat.ThrowDamage`) não somava STR do atacante — usava só `weaponBaseDamage`, decisão antiga documentada como intencional ("a arma voa, não é golpe corpo a corpo") que o usuário pediu pra reverter: agora é `weaponBaseDamage + str`, mesmo componente aditivo do golpe normal, sem `critMultiplier`/`sharpMult`. Atualizado nos dois arquivos — `CalcThrowDamage` e `ThrowDamage` (legado) ganharam um parâmetro de STR)
- Última atualização: 2026-06-19 (Dois ajustes adicionais no visual da skill **Thief**, ambos reportados pelo usuário depois do refinamento anterior: (1) "pisca" da vítima não estava aparecendo — `TextureController.DisplayedSprite` só aplica a troca dentro do próprio `Update()` e só quando o Animator não está em transição, o que podia atrasar ou simplesmente nunca mostrar a troca durante a sequência rápida do roubo; trocado pra setar `SpriteRenderer.sprite` direto (`PlayerCombat.faceRenderer`/`faceSprites`, substituindo o `faceController`/`TextureController` anterior), ignorando esse gate; (2) altura do salto até as costas do adversário (antes uma constante fixa de 0.8) trocada pra `settings.jumpHeight`, pra parecer mais um salto de verdade — mesmo arco usado em `ReturnToSpawn`/`DodgeLeap`)
- Última atualização anterior: 2026-06-19 (Visual da skill **Thief** refinado a pedido do usuário, 4 ajustes em `PlayerCombat.StealWeapon`: (1) ladrão vira pra mesma direção do adversário ao chegar nas costas dele — primeiro uso do mecanismo de flip por `localScale.x` fora do setup estático de cada prefab; (2) bounce trocado de vertical pra horizontal (pra frente e pra trás) e um pouco mais devagar (`bounceDuration` 0.18s, era 0.1s); (3) vítima "pisca" a cada um dos 4 ciclos — novo `PlayerCombat.faceController` (resolvido em `Awake()` pelo nome do GameObject "Face 01") alterna `TextureController.DisplayedSprite` entre Face 01 (0) e Face 03 (2); (4) velocidade do salto de ida e volta (antes uma constante fixa de 3) trocada pra `RuntimeRunSpeed`, igual ao salto de retorno ao spawn em `TurnEnd`)
- Última atualização anterior: 2026-06-19 (Skill **Thief** implementada — ação de início de turno (mesmo ponto do pickup normal): se desarmado e o oponente armado, 44% de chance por turno de roubar a arma equipada dele, limitado a 2 usos por luta (`PlayerState.thiefUsesRemaining`). Checada antes do pickup comum em `SimulateTurn` (os dois exigem estar desarmado, só um acontece por turno). Novo evento `CombatEventType.Thief` e novo `PlayerCombat.StealWeapon(thief, victim)` — visual procedural (sem sprite dedicado) referenciando o My Brute original: ladrão pula nas costas do adversário, balançam juntos 4 vezes rapidamente, desce já com a arma. Novo `PlayerLoadout.AddWeapon` (só existia `RemoveCurrentWeapon` antes) pra arma roubada aparecer na `WeaponHUD` do ladrão. `SkillDef` em `SkillAssetGenerator.cs` atualizada (descrição + `usesPerFight = 2`, era 1) — precisa rodar **Tools → AutoArms → Generate Skill Assets** de novo)
- Última atualização anterior: 2026-06-19 (Bug corrigido a partir do log de combate: `CombatSimulator.SimulateThrow` tinha um re-equip imediato de 40% no fim do arremesso (`CombatEventType.WeaponEquipped`) — fazia o personagem "equipar" uma arma visualmente já no fim do turno, sem nenhuma ação correspondente, contradizendo o design de que pickup de arma só deveria acontecer no `TurnStart` do início do turno. Removido o re-equip e o `CombatEventType.WeaponEquipped` inteiro (sem outro uso no projeto) — depois de arremessar, o personagem fica desarmado normalmente até o check de 40% do próprio `TurnStart` seguinte, mesmo padrão de qualquer outro turno desarmado)
- Última atualização anterior: 2026-06-19 (Skill **Determination** implementada — redefinida do roadmap original ("+STR conforme perde HP", nunca implementada) pra: se o golpe do atacante não causa dano ao oponente (esquivado, bloqueado, ou cancelado por Counter do defensor), 60% de chance fixa de tentar outro golpe completo imediatamente, recursivo até acertar de verdade ou o roll de 60% falhar. Novo wrapper `CombatSimulator.SimulateHitWithDetermination` (usado por `SimulateTurn` no lugar de `SimulateHit` direto, tanto pro primeiro golpe do turno quanto pra cada hit do loop de Combo) e novo parâmetro `out bool damageDealt` em `SimulateHit` pra sinalizar quando o dano de fato foi aplicado — `true` também (sem dano real) no guard do Monk, pra não disparar retry de Determination num personagem que nunca ataca. `SkillDef` em `SkillAssetGenerator.cs` movida de `StatBoost` pra `CombatPassive` com descrição atualizada — precisa rodar **Tools → AutoArms → Generate Skill Assets** de novo pro `.asset` já gerado refletir a mudança)
- Última atualização anterior: 2026-06-19 (Dois bugs reportados pelo usuário a partir do log de combate corrigidos: (1) drop de escudo e arma do defensor (ao bloquear) e desarme de escudo e arma (ao tomar hit) eram checados de forma totalmente independente, permitindo os dois caírem no mesmo hit — agora são mutuamente exclusivos com o escudo tendo prioridade: enquanto `defender.hasShield` for true, só o escudo pode cair; a arma só passa a correr risco depois que o escudo já tiver caído (ver **Drop de arma/escudo ao bloquear** e **Desarmar do Escudo**); (2) Monk ainda recebia jump-back ocasional no próprio `TurnEnd` sem nenhuma ação visível no turno, porque o guard de `CombatPlayer`'s case `TurnEnd` só checava `InSpawnZone` — não cobria ele ter sido empurrado pra fora da zona por knockback enquanto defendia nos turnos do adversário; guard agora é incondicional por `hitSpeed > 0f` (ver nota no `hitSpeed = 0 (Monk)` em **Skills que modificam stats**))
- Última atualização anterior: 2026-06-19 (Bug visual corrigido: `Medieval Warrior.controller` tinha os parâmetros `Idle` **e** `Running` com `m_DefaultBool: 1` (true) simultaneamente — Assassin Guy e Medieval Warrior Girl já tinham os dois em `0`/false corretamente. Sem nada zerando `Running` explicitamente no spawn, Medieval Warrior ficava com a animação de corrida tocando "no lugar" desde o `EntryFall` até o primeiro `PlayRun` real zerar o bool no fim daquele run — visualmente parecia "descer correndo" antes de avançar de verdade pro adversário (bug real reportado pelo usuário). Corrigido o default no `.controller` (`0` pros dois, igual aos outros personagens) e `AnimationController.SetIdle(true)` agora também força `Running=false` como guarda extra, pra não depender de nenhum controller futuro ter os defaults certos)
- Última atualização anterior: 2026-06-19 (Counter reordenado pra **primeiro de tudo** em `CombatSimulator.SimulateHit` — antes vinha depois de Block, então Monk/Sixth Sense quase nunca chegavam a counterar de fato, já que um Block bem-sucedido do defensor retornava antes do Counter ser checado; bug real reportado pelo usuário, "Monk não ativando depois que bloqueia nem ao tomar hit". Nova ordem: Counter → Block (+Reversal) → Esquiva → Dano normal. Escudo da skill Shield agora também pode cair durante um bloqueio bem-sucedido (`CombatEventType.ShieldDrop`, mesmo `ShieldDisarmChance = 0.10f` fixo, popup "DROP!"), além do desarme ao tomar hit já existente (`ShieldDisarm`, popup "DISARM!") — ambos os casos agora usam novo `PlayerCombat.DropShield(target, isDisarm)`, que reaproveita a queda em pêndulo amortecido de `DropWeapon` em vez de só destruir o sprite sem visual de queda)
- Última atualização anterior: 2026-06-19 (Skill **Shield** implementada — `blockBonus += 0.45`, `armor += 0.25`, `hasShield = true` em `CombatSceneLoader.ApplySkillStats`/`CombatSimulator.ApplySkillStats`/`PlayerProfile.GetEffectiveStats`; visual permanente no braço oposto via novo `WeaponHandler.EquipShield/RemoveShield` — slot `currentShield`/bone `offHandBone` separados do equip normal de arma, fora do `WeaponLoadout`; novo `WeaponData` asset `Assets/Data/UI/Weapons/Shield/Shield1.asset` usando o sprite já existente `Shield1.png`; novo evento `CombatEventType.ShieldDisarm` com chance própria fixa (`ShieldDisarmChance = 0.10f`, sem somar `disarmChanceBonus`/`disarmBonus`) em `CombatSimulator.SimulateHit`, removendo o sprite e revertendo os dois bônus quando trigga; `Block.anim` do Medieval Warrior (já ajustada) copiado para Assassin Guy e Medieval Warrior Girl. `WeaponHandler.offHandBone` wireado pro "Right Arm" de cada personagem (Assassin Guy/Medieval Warrior diretamente no `.prefab`; Medieval Warrior Girl via novo stripped Transform na cena, já que seu `WeaponHandler` é um componente adicionado direto no Player2 pré-colocado, não no prefab) e `CombatSceneLoader.shieldWeaponData` wireado em `04_CombatScenePVP`. Pendente no Editor: `shieldPositionOffset`/`shieldRotationOffset`/`shieldZOffset` (todos 0 por enquanto) e `Shield1.scale` ainda precisam de ajuste visual por personagem)
- Última atualização anterior: 2026-06-19 (WeaponType migrado de enum único para `List<WeaponType>` com até 3 tags por arma — Sharp/Blunt/Long/Heavy/Fast/Thrown somados por tabela em vez de switch por tipo exclusivo, ver **Tipos de Arma**; fórmula de dano redefinida de multiplicativa para STR aditiva (`(weaponBaseDamage + str) × critMultiplier × sharpMult`, era `weaponBaseDamage × (1 + str/10)`); `RollWeaponDamage` simplificado pra usar `weaponData.damage` direto, sem mais ranges hardcoded por tipo; `PlayerProfile.GetWeaponDamageRanges/GetBaseSharpDamageRanges` (preview da aba Stats) atualizados pra mesma fórmula aditiva e pros valores reais dos assets, em vez do range antigo do protótipo; fix do guard do Monk (`hitSpeed = 0`) que não era checado antes do Throw/Ballet Shoes/Run no próprio turno, fazendo Monk ainda se mover/atacar visualmente e levar um jump-back indevido em alguns turnos; roadmap de skills corrigido — Monk, Shock, Iron Head, Survival, Weapon Master e Martial Arts já estavam implementados mas constavam como `[ ]`/ausentes; nova seção **Survival** documentando o fix de HP mostrando 0 visualmente quando a skill deveria salvar em 1 HP; entrada da Bodybuilder na tabela de skills corrigida (não afeta mais STR, redefinida pra bônus condicionados a arma Heavy)
- Última atualização anterior: 2026-06-18 (Skills rebalanceadas/redefinidas: Armour (+25% armor, -15% SPD, era só +30% armor), Untouchable (+30% evasion, era +25%), Relentless (substituída — agora +30% accuracy/reduz esquiva do oponente, era +15% combo chance), Fists of Fury (nova, +20% combo chance, herdou o papel da Relentless), Counter Attack (substituída — agora +10% block + 90% reversal exclusivo de pós-block via novo campo reversalAfterBlock, era +40% counter/cancela hit), Toughened Skin (nova, +10% armor); teto de ComboChance subiu de 35% para 60%; DodgeChance ganhou o termo accuracy (subtrai do atacante, oposto de evasion) e SimulateHit/SimulateRetaliation tiveram a ordem de checagem invertida de Esquiva→Block para Block→Esquiva; CharacterPanel.Stats cresceu de 10 para 15 linhas (ACCURACY, ARMOR, BLOCK, REVERSAL AFTER BLOCK adicionadas) e PlayerProfile.GetEffectiveStats() é agora uma 15-tupla)
