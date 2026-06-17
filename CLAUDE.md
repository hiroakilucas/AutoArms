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
    - **`SkillDef.iconFileName`** (opcional, em `SkillAssetGenerator.cs`): nome do PNG em `Assets/Data/UI/Skills/`, se diferente de `fileName` (que também define o nome do `.asset` gerado — não pode ser trocado sem deixar um asset órfão). `Immortal` usa `fileName = "skill_immortal"` (asset existente) mas `iconFileName = "skill_immortality"`, porque o ícone re-adicionado pelo usuário segue o nome da skill na lista mestre original (`immortality`), não o nome abreviado do asset já implementado (`immortal`) — sem esse campo o gerador procurava `skill_immortal.png` (nunca existiu) e o ícone ficava sempre `null`, mesmo com `skill_immortality.png` presente na pasta e o gerador rodado.
- `CombatSceneLoader` — Agora usa coroutine (`Initialize()`): instancia Player1, aplica `profile.str`/`profile.agility` ao `PlayerCombat`, inicializa health/HUD, aguarda um frame (para `PlayerCombat.Start()` rodar), então executa entrada em cena (`EntryFall`) de ambos em paralelo. Só atribui `attackSequencer.player1` e `attackSequencer.player1Profile` após os dois pousarem. Atribui `player1`'s `PlayerLoadout.loadout = profile.weaponLoadout` e, simetricamente, `player2`'s `PlayerLoadout.loadout = player2Profile.weaponLoadout` (esse segundo não existia antes — não tinha efeito enquanto os profiles compartilhavam o mesmo asset, mas passou a ser necessário depois de cada personagem ganhar seu próprio `WeaponLoadout`; sem isso o `WeaponHUD` do Player2 mostraria o valor hardcoded na cena em vez do loadout real do perfil dela).
- `PlayerCombat` — Owns `AttackRoutine`. Manages sorting layer swaps so the attacker renders above the defender during a strike.
- `WeaponHandler` — Instantiates a weapon prefab onto `handBone`; `WeaponType` determines attack reach. Fires `OnWeaponChanged(WeaponData)` from `EquipData` (new weapon) and `Unequip` (null). `EquipSpecific(WeaponData)` equipa uma arma exata (usado por `CombatPlayer` para casar com o que o simulador sorteou); `EquipRandom()`/`EquipNext()` sorteiam/ciclam pelo loadout. Campo `sortingLayer` default `"Weapons"` (era `"Weapon"`, singular — não existe esse Sorting Layer no projeto; ver Sorting Layers abaixo — armas equipadas renderizavam num layer inexistente e ficavam atrás do corpo, parecendo invisíveis).
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

The stats row comes from `PlayerProfile.GetEffectiveStats()` — a preview-only calculation (no live `PlayerCombat`/`PlayerState` needed, since those components are destroyed for this display) that mirrors the subset of `CombatSimulator.ApplySkillStats`/`CombatSceneLoader.ApplySkillStats` affecting HP/str/agility/speed (Vitality, Herculean Strength, Feline Agility, Bodybuilder, Immortal). Shows just the four flat numbers normally, or `base→effective` in green when a skill changes any of them. **Keep this method in sync** if a stat-affecting skill's formula changes in either of those two places — it's a third, independent copy of the same logic for display purposes. `CharacterPanel.RefreshAll` (Stats tab, abaixo) also calls `GetEffectiveStats()` agora — antes mostrava `p.maxHealth`/`str`/`agility`/`speed` crus, então escolher uma skill que afeta stats (ex: Immortal) nunca refletia ali.

### CharacterPanel (3-tab slide-in)

`Assets/Scripts/UI/CharacterPanel.cs` — opened by clicking the "Personagem" button in the MainMenu (wired to `MainMenuController.OnCharacterButton()`).

- Created lazily on first click; `Setup(holder)` builds all UI then sets GO inactive
- Uses a separate ScreenSpaceOverlay Canvas (sortingOrder=20) parented to the CharacterPanel GO
- Panel RT: `anchorMin=(1, 0.22)`, `anchorMax=(1, 0.92)`, `pivot=(1, 0.5)`, `offsetMin=(-320, 0)`, `offsetMax=(0, 0)` → 320px fixed-width strip on the right edge, bottom at 237px (above the 203px button tops)
- Slide animation: `anchoredPosition.x = 340` (off-screen right) → `0` (visible). EaseOut quad (0.3s open, 0.2s close)
- Three tabs: **Stats** (HP/STR/AGI/SPD grid + XP bar + battle stats), **Skills** (3-column icon grid), **Armas** (weapon list with icon + name/type/damage)
  - Stats agora são 9 linhas verticais (`BuildStatRow`, label dourado à esquerda + valor à direita, empilhadas pelo `VerticalLayoutGroup`) em vez do grid horizontal antigo de colunas: HP, STR, AGI, SPD, INIT, CRIT CHANCE, CRIT DMG, EVASION, REVERSAL. Usa `SetStatValue`/`SetStatValuePercent` (mesmo padrão "base→efetivo" do `MainMenuCharacterPreview` acima): mostra só o número quando igual ao base, ou `base→<color verde>efetivo</color>` (fonte menor, 13 em vez de 16) quando uma skill o altera. `CRIT DMG` não tem campo base no profile (puramente derivado de skill, ex: Reconnaissance) — chamado com base `0f` fixo. `RefreshAll()` roda a cada `Open()`, então reabrir o painel depois de escolher uma skill no level-up já mostra os valores atualizados. Content da aba Stats agora tem `ContentSizeFitter` (faltava, igual Skills/Armas já tinham) — necessário pro scroll funcionar com a lista mais alta de 9 linhas.
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

### Fórmula de Dano (multiplicativa, estilo My Brute)
`PlayerCombat.CalcDamage(isCrit)` / `CombatSimulator.CalcDamage(attacker, isCrit)`:

```
finalDamage = Max(1, RoundToInt(weaponBaseDamage × (1 + str/10) × critMultiplier × (1 - defenderArmor)))
```

- `weaponBaseDamage` — `WeaponBaseDamage()`: Unarmed 5, Dagger `Random.Range(7,13)`, Sword `Random.Range(10,18)`, Heavy `Random.Range(30,50)`, outros tipos usam `weaponData.damage` (ou 3 se ≤0).
- `critMultiplier` — `1f` se não for crítico; senão `weaponData.critDamageMultiplier` (ou `UnarmedStats.CritDamageMultiplier = 1.5f` se desarmado).
- Lead Skeleton (`×0.85`) é aplicado **depois** do crit e **antes** da armadura, só para armas Heavy.
- Verificação: STR 4, soco base 5 → `5 × (1 + 4/10) = 7`.

### Propriedades das Armas (`WeaponData`, inspirado no My Brute)
Campos em `Assets/Scripts/Controller/WeaponData.cs`. Quando desarmado, usa-se a classe estática `UnarmedStats` (mesmo arquivo) em vez de uma instância de `WeaponData`.

| Campo | Efeito |
|---|---|
| `hitSpeed` | Multiplicador de velocidade da animação de slash: `slashSpeed = PlayerCombat.hitSpeed × weaponData.hitSpeed` |
| `drawChance` | % chance de pegar esta arma ao pick up (campo reservado — sem mecânica de peso ainda) |
| `reach` | Soma-se à distância base por tipo em `AttackPosition()` |
| `critChanceBonus` | Soma-se em `CritChance()` |
| `critDamageMultiplier` | Multiplicador de dano em crítico (substitui o antigo `×2` fixo) |
| `evasionBonus` | Soma-se em `DodgeChance()` (bônus do **defensor**) |
| `dexterityBonus` | Reservado (sem mecânica ainda, como `accuracy` em `PlayerProfile`) |
| `reversalBonus` | Soma-se em `ReversalChance()` (bônus do **defensor**) — ver seção **Counter e Reversal** |
| `blockBonus` | Soma-se em `BlockChance()` (bônus do **defensor**) |
| `accuracyBonus` | Reservado |
| `disarmBonus` | Soma-se em `DisarmChance()` (bônus do **atacante**) |
| `comboBonus` | Soma-se em `ComboChance()` (bônus do **atacante**) |
| `deflectBonus` | Reservado |

**Valores por tipo:**
| Propriedade | Unarmed | Knife/Dagger | Broadsword/Sword | Bumps/Heavy |
|---|---|---|---|---|
| `hitSpeed` | 1.0 | 2.0 | 1.0 | 0.6 |
| damage (base) | 5 | 10 (7–13) | 14 (10–18) | 40 (30–50) |
| `drawChance` | — | 0.33 | 0.33 | 0.33 |
| `reach` | 0 | 0 | +1 | +1 |
| `critChanceBonus` | +0.05 | +0.25 | +0.30 | +0.20 |
| `critDamageMultiplier` | 1.5 | 1.25 | 1.30 | 1.20 |
| `evasionBonus` | +0.10 | +0.10 | 0 | -0.30 |
| `dexterityBonus` | +0.20 | +0.50 | 0 | -0.65 |
| `reversalBonus` | 0 | — | +0.10 | -0.30 |
| `blockBonus` | -0.25 | — | +0.15 | -0.30 |
| `accuracyBonus` | 0 | — | 0 | +0.30 |
| `disarmBonus` | +0.05 | — | +0.15 | +0.10 |
| `comboBonus` | 0 | +0.30 | 0 | -0.60 |

Os 5 `WeaponData.asset` existentes (`Satyr1`=Dagger, `Golem3`=Heavy, `Succubus`/`VeryHeavyArmoredFrontierDefender`/`Zombie`=Sword) já têm esses valores aplicados. `comboBonus` segue a referência oficial do My Brute (Dagger +30%, Sword 0%, Heavy -40% a -60%) — não é esse o campo que inflava o combo; ver seção **Combo** abaixo (decaimento + clamp) para a correção real.

### Critical Hit
`CritChance()` no atacante = base por tipo de arma + `weaponData.critChanceBonus` (ou `UnarmedStats.CritChanceBonus`) + `criticalChance` (profile/skills):
| WeaponType | Chance base |
|---|---|
| Dagger | 8% |
| Sword | 5% |
| Heavy | 3% |
| others | 5% |

On crit: `critMultiplier = weaponData.critDamageMultiplier` (ver tabela de propriedades acima) entra na fórmula multiplicativa de dano. Popup mostra "CRIT!\n{damage}" em vermelho, fonte 5.
> Future skill **Fierce Brute**: +10% crit permanente.

### Dodge
`DodgeChance()` on the attacker, reading the **defender's** weapon type:
| WeaponType (defender) | Chance |
|---|---|
| Fast | 20% |
| Dagger | 15% |
| Sword | 10% |
| Heavy | 5% |
| others | 10% |

Each agility point above 3 adds +2% dodge, plus the defender's `weaponData.evasionBonus` (or `UnarmedStats.EvasionBonus = +10%` if unarmed) — teto máximo de esquiva total: 60%. Same AGI threshold adds +0.8% combo in `ComboChance()` (era +1.5%).

When dodge triggers: skip knockback, Hurt animation, and damage. Defender plays `DodgeLeap` (JumpStart animation + `JumpTo` backward by `knockbackDistance`, height 0.4) over `settings.dodgeDuration` (separate field from `hurtDuration` — was tied to it before, making the leap snap almost instantly). Popup shows "ESQUIVA!" in blue. Combo continues normally.
> Future skill **Sixth Sense**: +10% dodge permanente.

### Combo
`ComboChance(comboCount)` no atacante — `comboCount` = quantos hits extra de combo já aconteceram neste turno (0 no 1º hit extra):
| WeaponType | Chance base |
|---|---|
| Fast | 18% |
| Dagger | 15% |
| Sword | 12% |
| Heavy | 4% |
| outros / desarmado | 5% |

Soma-se `weaponData.comboBonus` (ver tabela de Propriedades das Armas — Dagger +30%, Sword 0%, Heavy -60%) + `0.8%` por ponto de AGI acima de 3 + `comboChanceBonus` (skills, ex: Relentless +15%). Esse total é limitado por `Mathf.Clamp(total, 0f, 0.35f)` — teto de 35% — e **só depois** multiplicado pelo decaimento `Mathf.Pow(0.5f, comboCount)`: 1º hit extra usa o valor pleno (até 35%), 2º hit extra usa metade (até 17.5%), 3º um quarto (até 8.75%), e assim por diante. Mirror oficial do My Brute, onde a chance de combo cai a cada hit consecutivo do mesmo turno.

Valores antigos (base Fast 40%/Dagger 35%/Sword 25%/Heavy 10%/desarmado 10%, AGI +1.5%/ponto, sem clamp, sem decaimento) deixavam personagens com Dagger e AGI alta combando quase sempre e por muitos hits seguidos (ex: Assassin Guy com Satyr1 chegava a ~71-86% por golpe, repetido indefinidamente). `CombatSimulator.SimulateTurn` loga `[ComboChance] {nome} (P{1|2}, arma=...) hit extra #{n} chance={valor}` a cada checagem do loop de combo, antes do `Roll()` — usar isso para confirmar visualmente o decaimento e validar se algum combo de stats/skills ainda está inflando o valor base (pré-decaimento).

### Block
`BlockChance()` no atacante, lendo o tipo de arma do **defensor**:
| WeaponType (defender) | Chance |
|---|---|
| Block | 50% |
| Dagger | 15% |
| Sword | 15% |
| Heavy | 15% |
| Slow | 5% |
| Sem arma (`CurrentWeapon == null`) | 0% |
| outros | 0% |

Soma-se ainda o `weaponData.blockBonus` do defensor (ou `UnarmedStats.BlockBonus = -25%` se desarmado). **`defender.counter` não entra mais aqui** — ver seção **Counter e Reversal** abaixo (rewired pra uma mecânica própria, em vez de ser só um bônus de block).

Ordem de verificação em `CombatSimulator.SimulateHit` (caminho ativo): **Esquiva → Block (+ Reversal) → Counter → Dano normal → Reversal → Desarmar**. Quando block trigga: sem dano, sem Hurt, mas aplica **knockback de 50%** (`knockbackDistance * 0.5f`) em paralelo. Defensor executa animação `Block` via `SetTrigger("Blocking")`. Popup "BLOCK!" em dourado.

**Drop de arma ao bloquear** — verificados independentemente após o popup de block:
- **15%** de chance do **atacante** soltar a arma (impacto no escudo)
- **10%** de chance do **defensor** soltar a arma/escudo (impacto forte demais)
- Usa `DropWeapon(target, isDisarm: false)` → popup "DROP!" laranja + arma cai com pêndulo, fica no chão até fim da luta.

### Counter e Reversal

Duas mecânicas distintas, ambas usando o defensor tomando a iniciativa de volta do atacante — implementadas só no caminho ativo (`CombatSimulator.cs`); **`PlayerCombat.cs`/`AttackSequencer.cs` (legado, código morto enquanto `useSimulator=true`) não têm nenhuma das duas, nem o ajuste de speed 0 abaixo** — seu `BlockChance()` ainda soma `defender.counter` (comportamento antigo), não existe `SimulateRetaliation` equivalente, e `CombatLoop` ainda força mínimo 1 ação mesmo com `speed = 0`. Só importa se `useSimulator` for desligado algum dia.

**Counter** — `CounterChance(defender) = defender.counter`. Checado em `SimulateHit` **depois do Block falhar, antes do dano normal**: o atacante já correu e iria acertar, mas o defensor bate primeiro — cancela completamente o hit do atacante (e o resto do combo dele, já que esse hit nunca aconteceu de fato). `defender.counter` é o mesmo campo que antes só alimentava o `BlockChance()` do atacante (via skill **Counter Attack**, `counter += 0.40`, e **Monk**, idem) — agora vira o que o nome já sugeria: as duas skills passam a dar +40% de chance de cancelar o hit do oponente em vez de +40% de chance de bloquear.

**Reversal** — `ReversalChance(defender) = defender.reversal + weaponData.reversalBonus` (ou `UnarmedStats.ReversalBonus = 0` se desarmado; os 5 `WeaponData.asset` já tinham `reversalBonus` preenchido — Sword +0.10, Heavy -0.30 — mas nenhum código lia o campo até agora). Ao contrário do Counter, Reversal **só age depois de algo já ter acontecido** — checado em **dois pontos** de `SimulateHit`:
1. **Depois de bloquear** (dentro do bloco de `Block`, depois dos checks de drop de arma): defensor já bloqueou, sem tomar dano, e ainda assim contra-ataca de bandeja.
2. **Depois do dano normal já aplicado** (hit aconteceu, HP já foi reduzido): defensor contra-ataca em seguida.

Diferente do Counter, Reversal **não cancela o resto do combo do atacante** — o combo continua normalmente depois, e cada hit extra do combo checa Reversal de novo, independente do(s) anterior(es) (pode triggar em mais de um hit do mesmo combo). O que já aconteceu (bloqueio ou dano) não é desfeito de qualquer forma. Reversal nunca age ANTES de um resultado (esse é o papel do Counter) — só depois de Block ou de Hit.

Ambas chamam `SimulateRetaliation(retaliator, target, eventType)` — o contra-ataque passa por esquiva/bloqueio/crítico normalmente contra o lado oposto (pode ser esquivado ou bloqueado pelo atacante original), mas **não verifica Counter/Reversal de novo** (evita recursão infinita entre as duas mecânicas — uma retaliação é sempre só uma retaliação, não pode ser contra-contra-atacada).

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

`ThrowChance()` por tipo de arma:
| WeaponType | Chance |
|---|---|
| Thrown | 100% |
| Dagger | 15% |
| Fast | 15% |
| Sword | 15% |
| Heavy | 10% |
| outros / sem arma | 0% |

Flow:
1. **Thrown** type: `Unequip()` only (stays in loadout, comes back next cycle). **All others**: `UnequipPermanent()` = `Unequip()` + `loadout.RemoveCurrentWeapon()` (removed from runtime loadout for this combat).
2. Create `FlyingWeapon` GameObject with the weapon's `inHandSprite`. `localScale = Vector3.one * weaponData.scale` for **all types** (same scale as the in-hand sprite).
3. `SetTrigger("Throwing")` fires animator concurrently.
4. `FlyWeapon()` moves sprite in a **straight line** over 0.45s. **Only `WeaponType.Thrown`** rotates (540°/s). All other types fly with fixed rotation.
5. On landing: 80% hit (weapon damage + Hurt + knockback), 20% miss — defender plays `DodgeLeap` (same animation as dodge) + gray "MISS!" popup.
6. After hit/miss: **40%** de chance de equipar arma aleatória imediatamente (`EquipRandom()`); 60% fica desarmado até o próximo turno.
7. `animationController.SetIdle(true)` — obrigatório ao final do caminho de throw para sair do estado `Throwing` antes do próximo turno. Sem isso, o animator fica preso em `Throwing` (a transição `Throwing → Idle` exige `Idle=true`), causando hurt e slash nas animações erradas quando o oponente ataca nesse intervalo.

`PlayerLoadout.runtimeWeapons` is a `List<WeaponData>` initialized lazily on first `GetNextWeapon()` call (after `CombatSceneLoader` has assigned the loadout). `RemoveCurrentWeapon(expected)` removes the entry at `currentIndex` only if it matches `expected` (guards against index drift), then decrements `currentIndex` so the next `EquipNext()` gets the correct successor.
`WeaponHandler.UnequipPermanent()` captures `CurrentWeaponData` before calling `Unequip()` (which clears it), then passes the reference to `RemoveCurrentWeapon(expected)` for validation.
`DamagePopup.SpawnMiss(worldPos)` spawns a gray "MISS!" popup.

### Pegar Arma (Início do Turno)
Ambos os personagens começam o combate **desarmados**. Ao iniciar cada turno, se `CurrentWeapon == null`:
- **40%** de chance de executar `EquipRandom()` (pega uma arma aleatória do loadout) + animação `CatchWeapon` (0.6s) → ataca com a arma.
- **60%** não pega → ataca desarmado (soco).

`EquipRandom()` chama `loadout.GetRandomWeapon()` — seleciona aleatoriamente entre as armas disponíveis no runtime loadout (não ciclicamente). Se o loadout estiver vazio, `GetRandomWeapon()` retorna null e `EquipRandom()`/`EquipNext()` chamam `Unequip()` (limpando `currentType`/`CurrentWeaponData` e disparando `OnWeaponChanged(null)`) em vez de destruir a arma visual sem atualizar esse estado — bug antigo deixava o ícone da `WeaponHUD` destacado em amarelo enquanto o personagem batia desarmado.
`PlayCatchWeapon()` chama `ResetTrigger("Hurt")` antes de disparar o trigger para evitar que Hurt enfileirado de um turno anterior interfira.

`CombatPlayer.ExecuteEvent`'s case `TurnStart` espera `0.1s * t` (era `yield return null`, só 1 frame) antes de processar o resto do turno (incluindo `PickupWeapon`/`CatchWeapon`). A transição "Idle → Catch Weapon" no Animator Controller só existe a partir do estado Idle especificamente (não AnyState — ver Animator Controller Architecture abaixo). Em ações extras por velocidade, o `TurnEnd` do turno anterior chama `SetIdle(true)` e o próximo `TurnStart` do mesmo personagem rodava só 1 frame depois — sem tempo do Animator concluir de fato a transição pro estado Idle antes do trigger `CatchWeapon` ser setado, deixando o trigger pendente até o Animator entrar em Idle (que podia acontecer só depois do run/ataque já ter começado, parecendo a animação de pegar arma tocando no fim do turno). Hipótese de causa, não confirmada visualmente — se persistir, verificar a duração de blend da transição `* → Idle` no `.controller` do personagem.

### Unarmed Combat
When `CurrentWeapon == null`, `HitRoutine` uses the `"Slashing"` trigger (punch) with `weaponBaseDamage = UnarmedStats.Damage = 5`, fed into the multiplicative damage formula (see above). Slash animation speed = `hitSpeed × UnarmedStats.HitSpeed (1.0)`, reset to `1f` afterward (all exit paths including dodge/block).
`ComboChance()` returns 5% base while unarmed, plus AGI bonus and `UnarmedStats.ComboBonus` (0), com o mesmo clamp (35%) e decaimento (×0.5 por hit extra consecutivo). Ver seção **Combo** acima.

### STR Attribute
`PlayerCombat.str` (default 10) feeds directly into the multiplicative damage formula: `(1 + str/10)`. There is no longer a "baseline 10 = no bonus" offset — STR scales damage linearly from 0 (no bonus) upward (STR 10 → ×2.0, STR 4 → ×1.4). See **Fórmula de Dano** above for the full formula (`weaponBaseDamage × (1 + str/10) × critMultiplier × (1 - defenderArmor)`).

| `weaponBaseDamage` | Value |
|---|---|
| Unarmed (punch) | `UnarmedStats.Damage = 5` |
| Heavy weapon | `Random.Range(30, 50)` |
| Sword | `Random.Range(10, 18)` (~10–17) |
| Dagger | `Random.Range(7, 13)` (~7–12) |
| Other types | `weaponData.damage` if > 0, else `3` |

Throw damage uses the same ranges WITHOUT the STR multiplier (the weapon flies, not a melee hit).

> Future skill **Iron Fist**: increases unarmed damage.

### Desarmar
`DisarmChance()` baseado no tipo de arma do **atacante**:
| WeaponType (atacante) | Chance |
|---|---|
| Dagger | 20% |
| Fast | 15% |
| Sword | 10% |
| Heavy | 5% |
| outros / desarmado | 0% |

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
`TurnStart, RunToDefender, ThrowWeapon, PickupWeapon, WeaponEquipped, Hit, Counter, Reversal, Dodge, Block, Miss, Disarm, WeaponDrop, HealthChanged, SpeedBonus, TurnEnd, CombatEnd`

### Key fields in CombatEvent
| Field | Used by |
|---|---|
| `playerIndex` | always set — index of the acting/affected player (0=P1, 1=P2) |
| `targetIndex` | defender or disarmed player |
| `damage`, `isCrit`, `isCombo` | Hit event |
| `newHp`, `maxHp` | HealthChanged event |
| `weaponName` | ThrowWeapon, PickupWeapon, WeaponEquipped, Disarm, WeaponDrop |
| `extraActions` | SpeedBonus (for RAPIDO! count) |

**ThrowWeapon visual:** `CombatPlayer.ExecuteEvent` spawns a `FlyingWeapon` GameObject (SpriteRenderer using `attacker.weaponHandler.CurrentWeaponData.inHandSprite`, captured before unequip destroys the in-hand object) and flies it from the hand bone to the defender's position via `PlayerCombat.FlyWeapon` (now `public`, shared with the legacy `ThrowRoutine`). Originally this case only called `Unequip()` + a `WaitForSeconds(0.45s)` with no projectile at all — the weapon just vanished with nothing visibly thrown. It also mirrors `CombatSimulator.SimulateThrow`'s loadout handling: non-`Thrown`-type weapons call `UnequipPermanent()` (so the icon disappears from `WeaponHUD` too, matching the simulator removing it from `weaponLoadout`), while `Thrown`-type weapons just `Unequip()` since they can be re-equipped later (the simulator's 40% re-equip-after-throw roll keeps them in the pool).

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
- `CombatSimulator.SimulateTurn` loga `[ComboChance] {nome} (P1|P2, arma=...) hit extra #{n} chance={valor}` a cada checagem do loop de combo (antes do `Roll()`) — instrumentação temporária para validar a fórmula de `ComboChance()` (base + AGI + comboBonus da arma + skills, teto 35%, decaimento ×0.5 por hit extra consecutivo). Remover quando o balanceamento estiver confirmado.
- `CombatSimulator.EmitSpeedBonus` loga `[SpeedBonus] round={n} {nome} (P1|P2) ação extra, index={i}` sempre que o popup "RAPIDO!" é emitido — instrumentação temporária para confirmar que `index` nunca é 0 (ou seja, nunca dispara na 1ª ação do round, só na 2ª em diante). Remover quando confirmado.
- `CombatSimulator.CalcDamage` loga `[CalcDamage] {nome} arma=... weaponBaseDamage=... str=... strMult=... critMult=... isCrit=... resultado=...` em **todo** hit normal/combo — instrumentação temporária pra investigar relato de dano muito acima do teto teórico da fórmula (ex: Dagger com STR 4-5 deveria ter teto ~18 normal/~22.5 crítico). `CombatSimulator.CalcThrowDamage` loga `[CalcThrowDamage] arma=... resultado=... (sem STR)` em todo arremesso, pra confirmar que o throw não aplica o multiplicador de STR. Remover os dois quando a causa for confirmada.

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
| `str` | int | 10 | Fórmula multiplicativa de dano: `weaponBaseDamage × (1 + str/10) × critMultiplier × (1 − defenderArmor)` (ver Combat Systems → Fórmula de Dano) |
| `agility` | int | 10 | `DodgeChance()`: +2%/ponto acima de 3, teto 60%; `ComboChance()`: +0.8%/ponto acima de 3, teto total 35% |
| `speed` | int | 10 | `AttackSequencer.CombatLoop`: acumula debt a cada round; debt >= speed do oponente = ação extra (ver Speed System) |
| `armor` | float | 0 | Fator `(1 − armor)` na fórmula multiplicativa de dano |
| `evasion` | float | 0 | `DodgeChance()`: adicionado à chance base |
| `accuracy` | float | 0 | future: reduz chance de esquiva do oponente |
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

**Iniciativa, crit chance, crit damage, evasion e reversal** agora também aparecem na tela de Stats (`CharacterPanel`) — `PlayerProfile.GetEffectiveStats()` retorna uma 9-tupla (`hp, str, agility, speed, initiative, criticalChance, critDamageBonus, evasion, reversal`). `MainMenuCharacterPreview` só usa os primeiros 5 (descarta os 4 últimos com `_, _, _, _`). Iniciativa é sempre flat puro (nunca entra no percentual líquido): `First Strike +200`, `Monk -200`, `Reconnaissance -200`, `Deity -200`. `criticalChance` é o campo bruto do profile (nenhuma skill o modifica ainda — `Fierce Brute` no roadmap ainda não implementada). `critDamageBonus` não existe como campo no profile, é 100% derivado de skill (`Reconnaissance +0.5`). `evasion`/`reversal` são campos reais do profile (`Untouchable +0.25`/`Ballet Shoes +0.10` somam em evasion; `Deity` soma `+0.40` em reversal e `-100%` multiplicativo em evasion, aplicado depois das somas — ver linha da Deity abaixo).

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
| Armour | `armor += 0.30` |
| Extra Thick Skin | `armor += 0.50` |
| Untouchable | `evasion += 0.25` |
| Bodybuilder | `strPct += 0.5` |
| Relentless | `comboChanceBonus += 0.15` |
| Lead Skeleton | `leadSkeleton = true` |
| Ballet Shoes | `evasion += 0.10`, `firstHitAvoided = true` |
| First Strike | `initiative += 200` |
| Counter Attack | `counter += 0.40` (ver **Counter e Reversal** acima — agora é chance de cancelar o hit do oponente, não bônus de block) |
| Monk | `counter += 0.40`, `initiative -= 200`, `hitSpeed = 0` |

> `accuracy` ainda não tem mecânica implementada — campo reservado para futuras skills. `reversal` agora tem mecânica real, ver **Counter e Reversal** acima.
> `hitSpeed = 0` (Monk): `HitRoutine` sai cedo — personagem guarda em vez de atacar.

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
- [x] Relentless — +15% combo chance (comboChanceBonus += 0.15)
- [x] Counter Attack — +40% counter rate (counter += 0.40) — cancela o hit do oponente antes de conectar, não mais bônus de block (ver Counter e Reversal)
- [ ] Impact — +15% disarm (ajustar DisarmChance())
- [ ] Pugnacious — chance de contra-atacar após levar dano (mecânica já existe via `reversal`/Reversal — ver Counter e Reversal acima; só falta criar o SkillData "Pugnacious" que soma nesse campo, mesmo padrão da Deity)
- [ ] Sixth Sense — +10% esquiva (ajustar DodgeChance())
- [ ] Iron Head — desarma o adversário com a cabeça ao levar hit
- [ ] Sabotage — remove permanentemente uma arma do adversário ao acertar
- [ ] Thief — rouba a arma do adversário ao acertar
- [x] Untouchable — +25% evasion
- [x] First Strike — +200 initiative (ataca primeiro)

#### Passivas de Defesa
- [ ] Shield — +45% block rate (ajustar BlockChance())
- [x] Armour — armor += 0.30 (30% redução de dano)
- [ ] Iron Skin — reduz dano fixo por hit
- [x] Lead Skeleton — -15% dano de armas Heavy (leadSkeleton = true)
- [x] Extra Thick Skin — armor += 0.50 (50% redução de dano)
- [ ] Survival — sobrevive com 1 HP uma vez por luta
- [x] Ballet Shoes — evasion +10%, primeiro golpe automaticamente esquivado

#### Passivas de Stats
- [x] Vitality — +18 HP permanente, +50% HP
- [x] Bodybuilder — str × 1.5
- [x] Herculean Strength — +3 STR permanente, +50% STR
- [x] Feline Agility — +3 AGI permanente, +50% AGI
- [x] Lightning Bolt — +3 SPD permanente, +50% SPD (atributo speed real, ações extra no Speed System — era runSpeedMultiplier × 1.5, velocidade de animação de correr)
- [x] Immortal — maxHealth × 3.5 (+250%), str/agility/speed × 0.75 (-25% cada)
- [x] Reconnaissance — +5 SPD permanente, +150% SPD, -200 initiative, +50% dano crítico (mecânica definida pelo usuário, não fazia parte do roadmap original)
- [x] Deity — +100% HP/STR, -100% AGI/evasion, -90% SPD, -200 initiative, +40% reversal (mecânica definida pelo usuário; introduziu a mecânica real de Counter/Reversal, ver seção própria em Combat Systems)
- [ ] Determination — +STR conforme perde HP

#### Passivas de Armas
- [ ] Weapon Master — +dano com qualquer arma
- [ ] Strong Arm — +dano com armas Heavy
- [ ] Master of Arms — +dano com armas Melee
- [ ] Hostility — equipa a arma mais forte primeiro
- [ ] Weapon Tampering — reduz dano das armas inimigas
- [ ] Fists of Fury — combo de socos desarmado melhorado

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
- Total: 87 tarefas | Concluídas: 35
- Última atualização: 2026-06-17 (Botão de velocidade agora alterna 1x↔1.5x (era 2x) e a cena de combate ganhou um EventSystem em runtime, sem o qual nenhum botão do CombatHUD recebia clique; fix de animação de combo retriggerando o Slashing mid-clip; hurtDuration 0.07→0.15s e novo campo dodgeDuration=0.25s separado dele; novo campo comboDelay=0.15s entre cada ação de um combo (hit/esquiva/bloqueio/erro), evitando que combos de 6 ações parecessem só 2-3 por falta de espaçamento; fix de health2 sendo inicializado com player2MaxHealth (50) dessincronizado de player2Profile.maxHealth (70), causando dano aplicado incorretamente; fix de WeaponHandler.EquipRandom/EquipNext destruindo a arma visual sem chamar Unequip quando o loadout esgota, deixando o ícone da WeaponHUD preso em amarelo; ThrowWeapon no CombatPlayer agora cria um FlyingWeapon visível voando até o defensor via PlayerCombat.FlyWeapon (tornado public) e usa UnequipPermanent para armas não-Thrown, removendo o ícone da WeaponHUD; dano/popup/hurt do evento Hit agora aplicam tudo no mesmo instante do impacto, em vez do dano só refletir na barra de vida quando o HealthChanged separado era processado depois; novo campo CombatEvent.isThrow sincroniza o hit de arma arremessada com a chegada do projétil (sem swing/espera de melee redundante); Dodge e Block agora também disparam o swing do atacante sincronizado com a reação do defensor, que antes não tinha nenhuma animação de ataque associada; fix de player1Combat.skills nunca sendo preenchido a partir de profile.skills — fazia a vida visual do Player1 ignorar bônus de skill (ex: Immortal) que o CombatSimulator aplicava corretamente, causando a barra zerar e travar bem antes do Player1 "morrer" de fato no simulador; skill Immortal rebalanceada de +100 HP/×0.5 runSpeed para ×3.5 HP (+250%) e ×0.75 (-25%) em str/agility/speed, aplicada em CombatSimulator e CombatSceneLoader; novo PlayerProfile.GetEffectiveStats() mostra esses stats com bônus de skill já calculados no preview do Menu Principal)
