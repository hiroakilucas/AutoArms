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

Fundação criada (2026-07-07) — ainda **não aplicada** nas telas existentes, exceto o botão
"Jogar" de `01_MainMenu` (`primaryAction`). Aplicação em massa acontece quando as tarefas
"Melhorar interface da página inicial" e "Melhorar interface da tela de escolha de
personagens" (Fase 1 do roadmap, ver CLAUDE.md) forem executadas — nesse momento, preferir
migrar cores hardcoded existentes (`CharacterPanel`, `CombatResultPanel`,
`MainMenuCharacterPreview`, `WeaponHUD` etc.) pra `UITheme` em vez de introduzir novos hex
soltos.
