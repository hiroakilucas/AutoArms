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
| `PlayerProfile` | `Assets/ScriptableObjects/PlayerProfiles/` | Assassin Guy, Medieval Warrior, Medieval Warrior Girl |
| `CharacterDatabase` | `Assets/ScriptableObjects/Databases/` | Only **Assassin Guy** and **Medieval Warrior** are unlocked (selectable); Medieval Warrior Girl is hardcoded as Player2 |
| `SelectedProfileHolder` | `Assets/ScriptableObjects/` | Cross-scene singleton — read by `CombatSceneLoader` and `MainMenuCharacterPreview` |
| `AttackSettings` | `Assets/ScriptableObjects/` | Combat timing: idle duration, run speed, slashing/jump/hurt durations |
| `WeaponLoadout` | `Assets/ScriptableObjects/` | Array of 10 `WeaponData` slots cycled per round |
| `WeaponData` | `Assets/ScriptableObjects/` | Name, in-hand sprite, damage, speed modifier, `WeaponType` (Sword/Heavy/Dagger), scale |

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
  ├── AnimationController  (Idle → Run → Slash → JumpStart → Hurt)
  ├── MovementController   (MoveTo linear, JumpTo parabolic arc)
  └── WeaponHandler        (equips next weapon from PlayerLoadout each round)
```

- `AttackSequencer` — Runs the indefinite turn loop; waits for both `PlayerCombat` references before starting.
- `PlayerCombat` — Owns `AttackRoutine`. Manages sorting layer swaps so the attacker renders above the defender during a strike.
- `WeaponHandler` — Instantiates a weapon prefab onto `handBone`; `WeaponType` determines attack reach.
- `PlayerLoadout` — Tracks `currentIndex` and advances round-robin through `WeaponLoadout.weapons[]`.

### 04_CombatScenePVP Hierarchy

| GameObject | Role |
|---|---|
| `Main Camera` | Scene camera |
| `Medieval Warrior Girl` | Player2 — pre-placed, has all combat components configured |
| `TurnManager` | **Dead object** — has a missing (deleted) script, can be removed from the scene |
| `Colosseum arena` | Background/visual |
| `CombatInitializer` | Hosts `CombatSceneLoader` — spawns Player1 and wires both combatants at runtime |
| `AttackSequencer` | Hosts `AttackSequencer` script — Player2 (Medieval Warrior Girl) pre-assigned, `interTurnDelay = 0.2`; Player1 starts as `None` and is filled at runtime by `CombatSceneLoader` |

`CombatSceneLoader.Start()` wiring sequence:
1. Reads `SelectedProfileHolder.currentProfile`
2. Instantiates Player1 prefab, assigns `AttackSettings` and `WeaponLoadout` from the profile
3. Finds the pre-placed Player2 (`Medieval Warrior Girl`)
4. Sets mutual `defender` / `defenderAnimationController` references on both `PlayerCombat` instances
5. Assigns `attackSequencer.player1` — this is what unblocks `AttackSequencer` and starts the combat loop

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

## Third-Party Plugins

- **Spriter2UnityDX** (`Assets/Spriter2UnityDX/`) — Converts Spriter `.scml` files to Unity prefabs/animators. Character prefabs use its `EntityRenderer` and `TextureController` runtime components.
- **TextMesh Pro** — Used throughout UI; assets in `Assets/TextMesh Pro/`.

## Roadmap

### Fase 1 — Interface & Personagens (Prioridade Alta)
- [ ] Melhorar interface da página inicial
- [ ] Melhorar interface da tela de escolha de personagens
- [ ] Criação do primeiro personagem masculino e feminino
- [ ] Adicionar novos prefabs de personagens guardados
- [ ] Atributos aleatórios ao criar personagem level 1 (vida, força, agilidade, velocidade)
- [ ] Habilidades inspiradas no My Brute
- [ ] Criar habilidades originais adicionais

### Fase 2 — Combate Robusto (Core do Jogo)
- [ ] Mecânicas My Brute: jogar arma, derrubar arma adversária, combo, troca de arma, crítico, esquiva, parry
- [ ] Porcentagens fixas por arma para eventos de combate
- [ ] Animação de knockback ao tomar hit
- [x] Barra de vida com dano baseado em status + dano da arma
- [ ] Sistema de XP e level (vitória +2 XP, derrota +1 XP)
- [ ] Curva de XP por nível crescente (sugestão: level × 20 XP necessário)
- [ ] Ao subir de nível: escolher atributo, skill ou arma (igual My Brute)

### Fase 3 — Armas & Itens (Conteúdo)
- [ ] Criar mais armas com sprites e stats diferentes
- [ ] Sistema de raridade de armas
- [ ] Pets: cachorro, lobo, águia, urso
- [ ] Magias (avaliar viabilidade)

### Fase 4 — Monetização (Receita)
- [ ] Sistema de diamantes (moeda premium)
- [ ] Compra de energia para lutar mais vezes
- [ ] Compra de personagens com diamante
- [ ] Precificação dos pacotes

### Fase 5 — Endgame & Social (Futuro)
- [ ] Mapa PVE (avaliar viabilidade)
- [ ] Torneio com premiação
- [ ] Torneios 2v2 e 3v3
- [ ] Guildas (avaliar viabilidade)

### Fase 6 — Infraestrutura & Dados (Técnico)
- [ ] Definir banco de dados para salvar personagens (Firebase ou PlayFab)
- [ ] Integrar persistência de dados do personagem online
- [ ] Criar cena 03_SelectWeapons (já referenciada no código)

### Progresso
- Total de tarefas: 29
- Concluídas: 1
- Ao concluir uma tarefa, marcar com [x] e atualizar o contador.
