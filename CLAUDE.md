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
  ├── StrikeRoutine()          → AttackPosition() → PlayRun → HitRoutine()
  ├── ComboStrikeRoutine()     → delay → reposition if needed → HitRoutine()
  ├── HitRoutine()             → slash trigger → dodge check → knockback+hurt+damage
  ├── DodgeLeap()              → PlayJumpStart + JumpTo (fired on defender when dodge triggers)
  ├── AnimationController      (Idle → Run → Slash → JumpStart → Hurt)
  ├── MovementController       (MoveTo linear, JumpTo parabolic arc)
  └── WeaponHandler            (equips next weapon from PlayerLoadout each round)
```

- `AttackSequencer` — Runs the indefinite turn loop; waits for both `PlayerCombat` references before starting.
- `PlayerCombat` — Owns `AttackRoutine`. Manages sorting layer swaps so the attacker renders above the defender during a strike.
- `WeaponHandler` — Instantiates a weapon prefab onto `handBone`; `WeaponType` determines attack reach.
- `PlayerLoadout` — Tracks `currentIndex` and advances round-robin through `WeaponLoadout.weapons[]`.
- `DamagePopup` — World-space TextMeshPro floating text spawned at the defender's position. Three variants: normal (yellow), crit (red "CRIT!\n{damage}"), dodge (blue "ESQUIVA!").

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

### Key States and Transitions
- **Running → Slashing/SlashingDagger/SlashingHeavy**: condition `Running=false + trigger`. These are the original transitions.
- **Any State → Slashing/SlashingDagger/SlashingHeavy** *(added for combo)*: same conditions, `CanTransitionToSelf=1`. Allows re-entering the Slashing state from itself during combo chains.
- **Slashing/SlashingDagger/SlashingHeavy → Jump Start**: only via `JumpStart=true`. No auto-exit-time — the Slashing states stay indefinitely until JumpStart fires.
- **Jump Start → Idle**: when `JumpStart=false AND Idle=true`.

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

### Knockback
Every hit (including combo) pushes the defender by `settings.knockbackDistance` in the direction away from the attacker, over `settings.hurtDuration`. Fired via `StartCoroutine` on the defender so it runs in parallel with `PlayHurt`.

## Third-Party Plugins

- **Spriter2UnityDX** (`Assets/Spriter2UnityDX/`) — Converts Spriter `.scml` files to Unity prefabs/animators. Character prefabs use its `EntityRenderer` and `TextureController` runtime components.
- **TextMesh Pro** — Used throughout UI; assets in `Assets/TextMesh Pro/`.

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
- [ ] Parry: chance de bloquear dano com arma ou escudo
- [ ] Jogar arma: arremessar a arma no adversário
- [ ] Desarmar: fazer o adversário soltar a arma
- [ ] Sistema de XP e level (vitória +3 XP, derrota +1 XP)
- [ ] Curva de XP: level × 20 XP necessário
- [ ] Ao subir de nível: escolher atributo, skill ou arma
- [ ] Tela de fim de combate com resultado e XP ganho

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

### Progresso
- Total: 48 tarefas | Concluídas: 6
- Última atualização: 2026-06-11 (esquiva, DodgeLeap, reposicionamento combo, knockback em todos os hits)
