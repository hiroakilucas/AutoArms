# AutoArms — Paleta de Cores de UI

Documentação da paleta central de UI, implementada como ScriptableObject em
`Assets/ScriptableObjects/UITheme.cs` (asset: `Assets/ScriptableObjects/UITheme.asset`).
Objetivo: nenhuma cor de interface deve ser hardcoded num prefab/script novo — referenciar
`UITheme` (direto ou via `UIThemeApplier.cs`, `Assets/Scripts/UI/`) em vez disso.

## Como usar

- **Novo elemento de UI com cor fixa que não muda em runtime**: adicionar um `UIThemeApplier`
  no mesmo GameObject do `Image`/`TextMeshProUGUI`, apontando pro asset `UITheme.asset` e
  selecionando o `ColorRole` correspondente à tabela abaixo. Aplicado uma vez em `Start()`.
- **Script que precisa da cor em tempo de execução** (ex: lerp, animação, condicional): ler o
  campo direto de uma referência a `UITheme` (`[SerializeField] private UITheme theme;`) em vez
  de duplicar o hex.
- Primeiro uso real: botão "Jogar" em `01_MainMenu` (`primaryAction`), 2026-07-07.

## Paleta

### Fundo

| Campo | Hex | Uso |
|---|---|---|
| `backgroundTop` | `#F5E9D3` | Topo de gradientes de fundo (tela cheia, painéis claros) |
| `backgroundBottom` | `#E8D5B5` | Base de gradientes de fundo |
| `panelBackground` | `#3A2E27` | Fundo de painéis escuros (ex: `CharacterPanel`, `CombatResultPanel`) |
| `panelBackgroundAlt` | `#4A3F35` | Variante de painel escuro (seções internas, abas) |

### Botão de ação principal (Play / Iniciar Combate)

| Campo | Hex | Uso |
|---|---|---|
| `primaryAction` | `#E63946` | Cor base do botão de ação principal (ex: "Jogar") |
| `primaryActionAlt` | `#FF6B35` | Par do botão principal (estado pressionado/hover ou gradiente) |

### Botões secundários

| Campo | Hex | Uso |
|---|---|---|
| `secondaryButton` | `#8B6F47` | Botões secundários (ex: Personagem, Shop) |
| `secondaryButtonAlt` | `#6B7B8C` | Variante fria de botão secundário |

### Ícones / status

| Campo | Hex | Uso |
|---|---|---|
| `currencyGold` | `#FFC93C` | Moeda geral (soft currency) |
| `currencyGem` | `#4ECDC4` | Diamante (moeda premium) |
| `energy` | `#FFD23F` | Energia do personagem |
| `hpFull` | `#6BCB77` | Barra de vida em estado saudável |
| `hpCritical` | `#FF5C5C` | Barra de vida em estado crítico (HP baixo) |
| `danger` | `#D64545` | Alertas/erros genéricos |
| `success` | `#52B788` | Confirmações/estados positivos genéricos |

### Texto

| Campo | Hex | Uso |
|---|---|---|
| `textOnLight` | `#2B2118` | Texto sobre fundo claro (`backgroundTop`/`backgroundBottom`) |
| `textOnDark` | `#F5E9D3` | Texto sobre fundo escuro (`panelBackground`/`panelBackgroundAlt`) |

### Tiers de Skill/Arma (T1/T2/T3)

| Campo | Hex | Uso |
|---|---|---|
| `tierBronze` | `#CD7F32` | Borda do ícone de skill/arma **T1** (`CharacterPanel`, seções HABILIDADES/ARMAS) |
| `tierSilver` | `#C0C0C0` | Borda do ícone de skill/arma **T2** |
| `tierGold` | `#FFD700` | Borda do ícone de skill/arma **T3** — tom distinto de `currencyGold` (mais saturado/"medalha"), de propósito, pra não confundir com moeda |

### Raridade de Personagem (`PlayerProfile.rarity`)

| Campo | Hex | Uso |
|---|---|---|
| `rarityNormal` | `#9E9E9E` | Fundo do `PortraitBox` (02_SelectCharacter) — raridade Normal (cinza) |
| `rarityUncommon` | `#43A047` | Raridade Incomum (verde) |
| `rarityRare` | `#1E88E5` | Raridade Rara (azul) |
| `rarityLegendary` | `#FB8C00` | Raridade Lendária (laranja) |
| `rarityImmortal` | `#E53935` | Raridade Imortal (vermelho) — tier mais raro |

## Status de adoção

Fundação criada (2026-07-07). Botão "Jogar" de `01_MainMenu` (`primaryAction`) foi o primeiro
uso real. No mesmo dia, `CharacterPanel` (hoje o HUD unificado compacto/expandido do lado
direito da tela — ver CLAUDE.md, "01_MainMenu" — antes eram dois elementos separados: um HUD
sobre o personagem central e um painel lateral por clique) migrou todas as cores hardcoded que
tinha pra `UITheme` — ganhou um campo `[SerializeField] private UITheme theme;` (wireado em
`01_MainMenu.unity`, mesmo padrão de `UIThemeApplier`) e lê os campos diretamente em vez de
duplicar hex. `CombatResultPanel`/`WeaponHUD`/`02_SelectCharacter` ainda não migrados.

- `panelBackground`/`panelBackgroundAlt` — fundo dos estados Compact/Expanded do `CharacterPanel`.
- `currencyGold` — badge de level e barra de XP (nos dois estados).
- `secondaryButtonAlt`/`secondaryButton` — barras de STR/AGI/SPD/Armadura no estado Expanded
  (SPD usa uma variante mais clara de `secondaryButtonAlt`, `Color.Lerp` com branco).
- `hpFull`/`hpCritical` — cor da barra de HP no estado Expanded (crítico abaixo de 30 HP
  efetivo, valor arbitrário só pra UI).
- `textOnDark`/`textOnLight` — texto sobre os painéis escuros e sobre o badge de level dourado,
  respectivamente.
- `danger` — fundo do botão de fechar (X) do painel lateral.
- `tierBronze`/`tierSilver`/`tierGold` (2026-07-07) — borda ao redor de cada ícone nas seções HABILIDADES/ARMAS do `CharacterPanel`, conforme o tier (T1/T2/T3) da skill/arma equipada; reaproveitados também na estrela de raridade (★) do popup de detalhe de arma.
- `secondaryButton`/`tierBronze`/`secondaryButtonAlt`/`currencyGold`/`currencyGem`/`success`/`danger` (2026-07-07) — cor própria por `WeaponType` (Blunt/Heavy/Long/Fast/Thrown/Ranged/Sharp) na linha "Types" do popup de detalhe de arma; e `primaryActionAlt`/`success`/`secondaryButtonAlt` pro destaque de tier atual vs. os outros dois nos campos `[T1/T2/T3]` (Damage/Draw Chance laranja, Crit Bonus verde, inativos cinza).
- `secondaryButton`/`panelBackgroundAlt`/`textOnDark`/`panelBackground`/`danger` (2026-07-14) — `CharacterCardButtonStyle` (`Btn_SelectCharacter`/"Chibers" em `01_MainMenu`): fundo do card (`secondaryButton`, mesmo token já documentado acima pra esse botão), faixa de label (`panelBackgroundAlt`), texto+outline do label (`textOnDark`/`panelBackground`) e badge circular de notificação (`danger`). Ícone placeholder usa um tint claro (`Color.Lerp` com branco) do próprio `secondaryButton`, mesmo padrão de derivar variantes já usado pra SPD em `AttributePipBar`.
- `rarityNormal`/`rarityUncommon`/`rarityRare`/`rarityLegendary`/`rarityImmortal` (2026-07-14) — `CharacterCardUI.BuildPortraitBox` (02_SelectCharacter): fundo do `PortraitBox` de cada card, escolhido por `PlayerProfile.rarity` em vez do hash-do-nome usado antes (paleta arco-íris sem significado). Bloqueado usa a mesma cor passada por `Desaturate` (padrão já existente).
