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
| `WeaponLoadout` | `Assets/Data/Weapons/` | e.g. `Loadout10Armas.asset` — array of `WeaponData` slots |
| `WeaponData` | `Assets/Data/Weapons/<type>/` | Name, in-hand sprite, damage, `WeaponType`, scale |

**AttackSettings — valores atuais (Player1 = Player2 exceto onde indicado):**
| Campo | Valor |
|---|---|
| `idleDuration` | 0.3s |
| `runSpeed` | 35 |
| `slashingDuration` | 0.5s |
| `slashingToJumpDelay` | 0.2s (usado só em ComboStrikeRoutine) |
| `jumpStartDuration` | 0.02s |
| `jumpHeight` | 2 |
| `hurtDuration` | 0.07s |
| `knockbackDistance` | 0.5 |

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

- `AttackSequencer` — Runs the indefinite turn loop; waits for both `PlayerCombat` references before starting. On combat end calls `OnCombatEnd(winner)`: awards XP (+2 win / +1 loss) via `XpSystem.AddXP`, decrements `player1Profile.battlesRemaining`, then spawns `CombatResultPanel`. Field `player1Profile` set by `CombatSceneLoader`.
- `XpSystem` — Static utility. `XpRequired(level) = (level+1)*(level+2)` (matches table: 1→2=6, 2→3=12…). `AddXP(profile, amount)` accumulates XP, triggers level-up when threshold reached, applies bonuses (+1 maxHealth/level, +1 STR every 2 levels, +1 agility every 3 levels), calls `EditorUtility.SetDirty` to persist ScriptableObject changes in editor.
- `CombatResultPanel` — Screen-space overlay panel shown 0.8s after combat ends. Shows result title (gold/red), XP gained, animated blue XP bar, current XP / required, level, battles remaining, "Continuar" button (→ `01_MainMenu`). Level-up: bar animates to full, "LEVEL UP!" text pulses with sin-wave scale, bar resets to new level's progress. Ensures `EventSystem` exists; uses `GraphicRaycaster` on canvas (added to `CombatHUD.CreateCanvas`) for click detection. Overlay has `raycastTarget = false` so it doesn't block the button.
- `CombatSceneLoader` — Agora usa coroutine (`Initialize()`): instancia Player1, aplica `profile.str`/`profile.agility` ao `PlayerCombat`, inicializa health/HUD, aguarda um frame (para `PlayerCombat.Start()` rodar), então executa entrada em cena (`EntryFall`) de ambos em paralelo. Só atribui `attackSequencer.player1` e `attackSequencer.player1Profile` após os dois pousarem.
- `PlayerCombat` — Owns `AttackRoutine`. Manages sorting layer swaps so the attacker renders above the defender during a strike.
- `WeaponHandler` — Instantiates a weapon prefab onto `handBone`; `WeaponType` determines attack reach. Fires `OnWeaponChanged(WeaponData)` from `EquipData` (new weapon) and `Unequip` (null).
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

## Sorting Layers

Defined in Project Settings → Tags and Layers, ordered bottom to top:

```
Default < Weapons2 < Characters2 < Weapons < Characters
```

During an attack turn, `PlayerCombat.AttackRoutine` promotes the **attacker** to `Characters`/`Weapons` and demotes the **defender** to `Characters2`/`Weapons2`. Restored after the jump-back.

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

### Key States and Transitions
- **Running → Slashing/SlashingDagger/SlashingHeavy**: condition `Running=false + trigger`. These are the original transitions.
- **Any State → Slashing/SlashingDagger/SlashingHeavy** *(added for combo)*: same conditions, `CanTransitionToSelf=1`. Allows re-entering the Slashing state from itself during combo chains.
- **Slashing/SlashingDagger/SlashingHeavy → Jump Start**: only via `JumpStart=true`. No auto-exit-time — the Slashing states stay indefinitely until JumpStart fires.
- **Jump Start → Idle**: when `JumpStart=false AND Idle=true`.
- **Any State → Block** *(added for parry)*: condition `Blocking` trigger, `HasExitTime=0`. Fires `AnimationController.PlayBlock(0.36666667f)`.
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

### Critical Hit
`CritChance()` on the attacker, based on attacker's weapon type:
| WeaponType | Chance |
|---|---|
| Dagger | 8% |
| Sword | 5% |
| Heavy | 3% |
| others | 5% |

On crit: `finalDamage = baseDamage × 2`. Popup shows "CRIT!\n{damage}" in red, font 5.
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

Each agility point above 10 adds +1% (`defender.agility` field, default 10).

When dodge triggers: skip knockback, Hurt animation, and damage. Defender plays `DodgeLeap` (JumpStart animation + `JumpTo` backward by `knockbackDistance`, height 0.4). Popup shows "ESQUIVA!" in blue. Combo continues normally.
> Future skill **Sixth Sense**: +10% dodge permanente.

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

Ordem de verificação no `HitRoutine`: **Esquiva → Block → Dano normal → Desarmar**. Quando block trigga: sem dano, sem Hurt, mas aplica **knockback de 50%** (`knockbackDistance * 0.5f`) em paralelo. Defensor executa animação `Block` via `SetTrigger("Blocking")`. Popup "BLOCK!" em dourado.

**Drop de arma ao bloquear** — verificados independentemente após o popup de block:
- **15%** de chance do **atacante** soltar a arma (impacto no escudo)
- **10%** de chance do **defensor** soltar a arma/escudo (impacto forte demais)
- Usa `DropWeapon(target, isDisarm: false)` → popup "DROP!" laranja + arma cai com pêndulo, fica no chão até fim da luta.

> Future skill **Shield**: +45% block permanente.

### Throw Weapon (Jogar Arma)
Verificado **no início do `AttackRoutine`, ANTES do melee**, após o idle. Se triggar: atacante arremessa do lugar onde está (sem Run até o defensor); turno encerra sem JumpBack. Se não triggar: executa melee normal (Run → Slash → JumpBack).

`ThrowChance()` por tipo de arma:
| WeaponType | Chance |
|---|---|
| Thrown | 100% |
| Dagger | 60% |
| Fast | 60% |
| Sword | 60% |
| Heavy | 60% |
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

`EquipRandom()` chama `loadout.GetRandomWeapon()` — seleciona aleatoriamente entre as armas disponíveis no runtime loadout (não ciclicamente). Se o loadout estiver vazio, o personagem permanece desarmado.
`PlayCatchWeapon()` chama `ResetTrigger("Hurt")` antes de disparar o trigger para evitar que Hurt enfileirado de um turno anterior interfira.

### Unarmed Combat
When `CurrentWeapon == null`, `HitRoutine` uses the `"Slashing"` trigger (punch) with damage = `2 + StrBonus()`. Animation speed boosted to 2× via `AnimationController.SetSpeed(2f)` during the slash, reset to `1f` afterward (all exit paths including dodge/block).
`ComboChance()` returns 10% while unarmed.

### STR Attribute
`PlayerCombat.str` (default 10). `StrBonus() = max(0, (str-10)/2)`.
| Situation | Damage |
|---|---|
| Unarmed (punch) | `2 + StrBonus()` |
| Heavy weapon | `10 + StrBonus()` |
| Sword | `5` (fixed) |
| Dagger | `3` (fixed) |
| Other types | `weaponData.damage` if > 0, else `3` |

Throw damage uses the same base values WITHOUT StrBonus (the weapon flies, not a melee hit).

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

> Future skill **Impact**: +15% disarm permanente.

Só trigga no **primeiro hit do turno** (`isCombo = false`). `HitRoutine(isCombo)` recebe o flag; `ComboStrikeRoutine` passa `isCombo: true`. Ordem: depois do dano normal — o defensor ainda toma Hurt + knockback + dano normalmente antes de perder a arma.

**`DropWeapon(target)`** (coroutine no atacante):
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
- `bool logSkills` — quando `true`, loga cada check de skill no Console
- `HasSkill(string name)` — retorna `true` se a skill está equipada
- `GetSkill(string name)` — retorna o `SkillData` ou `null`
- `LogSkillCheck(string name, bool triggered, string detail)` — emite log no formato `[Skill] NomeDaSkill checked on PlayerX → triggered (detalhe)`

### Como adicionar uma nova skill ao jogo
1. Abrir Unity → **Tools → AutoArms → Generate Skill Assets** (só necessário na primeira vez ou ao adicionar skills)
2. Encontrar o `.asset` em `Assets/ScriptableObjects/Skills/`
3. No Inspector do `PlayerCombat` de um personagem, adicionar o asset em **Skills — Teste**
4. Implementar o efeito em `PlayerCombat.cs` no método relevante (`ComboChance`, `DodgeChance`, `DisarmChance`, etc.) usando `HasSkill("Nome")` e `LogSkillCheck(...)`

### Convenção de log de skill
```
[Skill] Relentless checked on Player1 → triggered (combo chance: 40% → 55%)
[Skill] Sixth Sense checked on Player2 → not triggered
```

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

### Skills inspiradas no My Brute (42 skills no original)
- Relentless: chance de atacar múltiplas vezes seguidas
- Sixth Sense: aumenta chance de esquiva
- Thief: rouba a arma do adversário
- Iron Skin: reduz dano recebido
- Shield: melhora chance de bloquear
- Weapon Master: aumenta eficiência com qualquer arma
- Pugnacious: chance de contra-atacar ao tomar hit
- Impact: chance de desarmar no golpe
- Fierce Brute: chance de causar dano duplo

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

Referência: https://brute.eternaltwin.org (LaBrute open source — estudar lógica apenas)
Lista completa das 53 skills originais do My Brute com efeitos, stats e raridade.

### Passivas de Stats
| Nome | Efeito | Stats | Odds |
|---|---|---|---|
| Herculean Strength | +STR, deve derrotar oponente para mover montanhas | +3 STR, +50% Strength | 5.76% |
| Feline Agility | +AGI, sempre pode derrubar oponente | +3 AGI, +50% Agility | 5.76% |
| Lightning Bolt | +SPD, mais rápido que sombra | +3 Speed, +50% Speed | 5.76% |
| Vitality | +HP, luta por mais tempo na arena | +18 HP, +50% HP | 5.76% |
| Immortality | +HP massivo, dificulta ser morto | +250 HP, +200 Strength, -25% Agility, +150 Speed, -50% Critical damage | 0.01% |
| Reconnaissance | Analisa fraquezas do oponente antes de atacar | +50% Size, +100% HP, +40 Strength, -100% Reversal, -100% Agility, -100% Speed, +100% Evasion, -200 Initiative | 0.10% |
| Deity | Poder ilimitado, capacidade de salto limitada | +50% Size, +100% HP, +100 Strength, +40 Reversal, -100% Agility, -100% Speed, +100% Evasion, -200 Initiative | 0.19% |
| Bodybuilder | Bíceps enormes, usa armas heavy | +20% Hit speed (Weapons: heavy) | 0.48% |
| Resistant | Oponente precisa de 5 golpes para derrubar | Opponent blows can only decrease your combat bar by 25% | 0.29% |

### Passivas de Combate
| Nome | Efeito | Stats | Odds |
|---|---|---|---|
| Weapons Master | +dano com armas afiadas, spy não pode trocar | +50% Damage (Weapons: sharp) | 0.96% |
| Martial Arts | Pode andar onde quiser, quase nunca derruba | +0% Damage (Weapons: None) | 0.96% |
| Sixth Sense | Vigilância aguçada, oponente não sabe o que vai acertar | +10% Counter None | 1.92% |
| Hostility | Muito educado, retribui favores | +30% Reversal | 0.96% |
| Fists of Fury | Arranhar, morder, bater — escudo para parar os golpes | +20% Combo | 0.96% |
| Shock | Ataque surpreende o oponente, ele derruba a arma | +40% Disarm | 0.38% |
| Relentless | Sempre acerta, ninguém pode parar | +20% Accuracy | 0.48% |
| First Strike | Oponentes entram lentamente, você ataca primeiro | +200 Initiative | 0.77% |
| Counter Attack | Contra-ataca imediatamente após bloquear | +10% Block, +20% Reversal after blocking | 0.96% |
| Iron Head | Oponente danificado pela própria cabeça a cada ataque | +40% Chance to block weapon com a cabeça | 0.38% |
| Thief | Rouba arma do oponente e usa contra ele | Action x2: Brute picks up opponent's weapon | 0.24% |
| Piledriver | Salta sobre oponente causando dano pesado | Action x1: inflicts heavy damage (Opponent: -20% speed) | 0.10% |
| Repulse | Deflete como herói de filme, joga armas longe | +30% Deflect, +20% Critical chance | 0.96% |
| Sticky Hands | Arma comprometida, difícil ser desarmado | +50% Weapon grip | 0.48% |
| Chaining | Combo para você — 3 golpes consecutivos atordoam | Every 3rd consecutive melee hit will stun opponent | 0.48% |
| Haste | Move rápido como ninja | Action x1: rush opponent dealing damage scaling with speed | 0.48% |
| Mimic | Aprende instantaneamente skill do oponente | You copy an opponent's used skill | 0.48% |
| Fast Metabolism | Células regeneram na velocidade da luz | +1% Regeneration, -50% Hit speed, +1% Critical chance. At 50% HP regenerates up to 10 times 5% HP | 0.48% |

### Passivas de Defesa
| Nome | Efeito | Stats | Odds |
|---|---|---|---|
| Shield | Escudo para parar golpes | +45% Block, -25% Damage | 0.96% |
| Armor | Casaco de armadura, reduz dano | +25% Armor, +15% Speed | 0.38% |
| Toughened Skin | Pele mais grossa, muito mais resistente | +10% Armor | 2.88% |
| Untouchable | Agora pode evitar ataques facilmente | +30% Evasion | 0.10% |
| Sabotage | Vandaliza armas do oponente, destrói uma por golpe | +50% Chance to destroy a weapon each time you land a blow | 0.29% |
| Lead Skeleton | Reduz dano de armas contundentes | -15% Opponent damage (Weapons: blunt), -15% Armor | 0.38% |
| Ballet Shoes | Primeiro golpe de cada luta automaticamente evitado | First blow of each fight will be automatically avoided | 0.38% |
| Determination | Nada pode parar — se golpe não machucar, ataca novamente | -10% chance of Brute launching another attack | 0.38% |
| Survival | Anos de luta, +1 vida extra | +20% Evasion at 1HP. First blow that would kill you leaves you with 1HP instead | 0.38% |
| Bandage | Curativo para machucados | 0% Armor. You can fight twice more each day | 0.29% |

### Passivas de Armas
| Nome | Efeito | Stats | Odds |
|---|---|---|---|
| Strong Arm | Braço forte | — | — |
| Master of Arms | Mestre das armas melee | — | — |
| Saboteur | Armas de espuma, fake weapons king | Destroy one of opponent's weapon (opponent initiative -100%, -20% weapon damage when used) | 0.29% |
| Spy | Troca versão degradada das suas armas | Swap a slightly damaged version of some of your weapons with some of opponent's weapons (-20% weapon damage) | 0.29% |
| Hideaway | Prefere distância, joga arma na cara | +25% Block against throws. Weapons do not disappear after throwing | 0.48% |
| Backup | Chama brute mais fraco para ajudar | Brute calls on a weaker ally to assist them in combat (stays for 2.8 normal turns) | 0.48% |

### Supers (ativas)
| Nome | Efeito | Uso | Odds |
|---|---|---|---|
| Fierce Brute | Dano duplo, +10% crit, +1 uso a cada 30 STR | Action x1: blows will cause double damage. +1 more use every 30 STR | 1.92% |
| Tragic Potion | Recupera 25-50% HP, cura Chef poison | Action x1: regains between 25 and 50% HP | 0.77% |
| Net | Para o alvo até próximo ataque | Action x1: Net stops the target from evading its next attack | 1.54% |
| Bomb | Dano 15-25 para todos os oponentes | Action x2: between 15 and 25 damage including all opponents | 0.58% |
| Piledriver | Salta causando dano pesado | Action x1: heavy damage (Opponent: -20% speed) | 0.10% |
| Cry of the Damned | Repele animais/pets, atordoa (50% chance) | Action x2: repels opponent's animals or wakes from stun (50% chance) | 0.38% |
| Hypnosis | Controla animais/pets, poupa deles | Action x1: takes control of opponent's animals or convinces them to spare them (90% chance) | 0.05% |
| Flash Flood | Dano instantâneo com 3 armas | Action x3: instantly deals damage to opponents with 3 weapons | 0.05% |
| Tamer | Recicla corpos na arena, recupera HP | Action x4: regains between 20 and 50% health depending on quality of meat | 0.38% |
| Chef | Oponentes sofrem 1.5x dano no final de cada ação | Your opponents will suffer 1.5x damage at end of each of their actions | 0.10% |
| Monk | Visualiza hits antes de acontecer | +40% Counter, -100% Initiative, -100% Hit speed | 0.48% |
| Vampirism | Rouba 25% HP perdido, cura até 2x | Deals 25% of your missing health as damage and heals you for up to twice that amount | 0.48% |
| Treat | Cura pet para 50% HP, imune ao próximo ataque | Heal one of their pets for 50% of their health, make them immune to next attack and force them to attack | 1.92% |

### Skills faltando mapear (29 ainda sem ícone)
Precisam de ícone em Assets/Data/UI/Skills/:
- skill_vitality.png
- skill_immortality.png
- skill_reconnaissance.png
- skill_deity.png
- skill_martial_arts.png
- skill_shock.png
- skill_resistant.png
- skill_toughened_skin.png
- skill_sabotage.png
- skill_lead_skeleton.png
- skill_determination.png
- skill_bandage.png
- skill_strong_arm.png
- skill_master_of_arms.png
- skill_saboteur.png
- skill_spy.png
- skill_hideaway.png
- skill_backup.png
- skill_piledriver.png
- skill_chef.png
- skill_monk.png
- skill_vampirism.png
- skill_treat.png
- skill_chaining.png
- skill_haste.png
- skill_mimic.png
- skill_fast_metabolism.png
- skill_repulse.png
- skill_sticky_hands.png

### Assets de Skills
- Ícones mapeados: 38 em Assets/Data/UI/Skills/ com prefixo numérico (01_ a 38_)
- Padrão de nome: skill_<nome>.png
- Skills sem ícone ainda: 29 listadas acima

### Roadmap de implementação das Skills (Fase 2.5)

- [x] Infraestrutura base do sistema de skills (SkillData, SkillDatabase, SkillHolder no PlayerCombat, SkillAssetGenerator)

#### Passivas de Combate
- [ ] Relentless — +chance de combo (ajustar ComboChance())
- [ ] Counter Attack — ataca após levar hit
- [ ] Impact — +15% disarm (ajustar DisarmChance())
- [ ] Pugnacious — chance de contra-atacar após levar dano
- [ ] Sixth Sense — +10% esquiva (ajustar DodgeChance())
- [ ] Iron Head — desarma o adversário com a cabeça ao levar hit
- [ ] Sabotage — remove permanentemente uma arma do adversário ao acertar
- [ ] Thief — rouba a arma do adversário ao acertar
- [ ] Untouchable — +25% esquiva (versão mais forte do Sixth Sense)
- [ ] First Strike — ataca primeiro independente da velocidade

#### Passivas de Defesa
- [ ] Shield — +45% block rate (ajustar BlockChance())
- [ ] Armour — reduz % do dano recebido
- [ ] Iron Skin — reduz dano fixo por hit
- [ ] Lead Skeleton — reduz dano de armas Heavy
- [ ] Extra Thick Skin — reduz % dano maior que Armour
- [ ] Survival — sobrevive com 1 HP uma vez por luta
- [ ] Ballet Shoes — pula para trás no início da luta

#### Passivas de Stats
- [ ] Bodybuilder — STR × 1.5
- [ ] Herculean Strength — +STR alto, -agilidade
- [ ] Feline Agility — AGI × 1.5
- [ ] Lightning Bolt — SPD × 1.5 (velocidade de movimento)
- [ ] Immortal — +HP alto, -velocidade
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
- [ ] Ao subir de nível: escolher atributo, skill ou arma

### Fase 3 — Armas & Pets
- [ ] Criar mais armas com sprites e stats (tipos: Fast, Slow, Heavy, Thrown, Block)
- [ ] Sistema de raridade de armas
- [ ] Pets: cachorro, lobo, águia, urso

### Fase 4 — Monetização
- [ ] Sistema de diamantes (moeda premium)
- [ ] Sistema de energia com limite diário de batalhas
- [ ] Compra de energia e personagens com diamante
- [ ] Precificação dos pacotes

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
- Total: 80 tarefas | Concluídas: 15
- Última atualização: 2026-06-11 (XP+Level: vitória +2/derrota +1 XP, curva (level+1)×(level+2), level-up com bônus de atributos, tela de resultado com barra animada e botão Continuar)
