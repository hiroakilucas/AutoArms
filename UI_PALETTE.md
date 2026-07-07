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
