# Ícones e Imagens Pendentes (Levantamento p/ Leonardo AI)

Levantamento gerado em 2026-07-09. **Documento só de inventário** — nada foi alterado em código/cena. Cobre `01_MainMenu`, `02_SelectCharacter`, `04_CombatScenePVP`, `05_SelectOpponent` e os popups/HUDs construídos via código (a maior parte da UI do projeto é montada em runtime, não em prefabs prontos — ver `CLAUDE.md`).

**Achado geral**: quase nenhuma dessas telas usa sprite "placeholder genérico do Unity". O padrão real do projeto é **retângulo de cor sólida via `UIShapeUtil.RoundedRect`** (textura procedural 64×64, só preenchimento de cor + cantos arredondados) ou **`Image.color` liso sem sprite nenhum**, tanto lendo `UITheme` quanto com cor literal hardcoded no script. Nos dois casos, é exatamente o tipo de slot que uma arte gerada substituiria.

---

## 1. Ícones de moeda/recursos (moeda geral, diamante, energia)

**HUD ainda não existe no código** — item do roadmap segue com `[ ]` ("HUD superior: moeda geral, diamante e energia do personagem"). Nenhum script em `Scripts/UI`/`Scripts/Controller` referencia moeda/diamante/energia. Não há nada pra gerar ainda nessa categoria — quando o HUD for implementado, revisitar este item.

Nota à parte: `01_MainMenu.unity` tem 8 `SpriteRenderer` world-space soltos na raiz da cena (`imgDiamond`, `imgBlackDiamond`, `imgPlusDiamond`, `imgEnergy`, `imgBlackEnergy`, `imgPlusEnergy`, `imgQuest`, `imgPass`, + um fundo `floating castle`) — sobras de um asset pack, **já têm arte real**, não referenciadas por nenhum script, fora do `Canvas` ativo. Não precisam de geração; só ficam ali sem uso (fora do escopo deste levantamento mexer nelas).

---

## 2. Ícones de botão de navegação

Nenhum desses botões tem glyph de ícone — todos são retângulo colorido (procedural ou cor literal) + label de texto. Nenhum "Play" triangular, "X" gráfico, seta de "Voltar" ou check de "Selecionar" existe hoje; o "X" do popup, por exemplo, é literalmente o caractere de texto "X".

| Botão | Onde (GameObject/método) | Estado atual | Dimensão | Referência visual |
|---|---|---|---|---|
| `BtnJogar` ("JOGAR") | `01_MainMenu.unity` | Sprite genérico do asset pack (guid `6619242053cf0d8419e084104430bbee`, botão arredondado sem desenho específico) tintado via `UIThemeApplier` → `UITheme.primaryAction` | 480×220 | `primaryAction = #E63946` (⚠ CLAUDE.md descreve como verde `#48D15C` — conferir cor real no Editor antes de gerar, pode estar desatualizado) |
| `Btn_SelectCharacter` ("GUERREIROS") | `01_MainMenu.unity` | Mesmo sprite genérico do asset pack, sem tint de tema (branco) | 495×170 | — |
| `BtnShop` | — | **Não existe mais na cena** (removido; CLAUDE.md ainda o cita) | — | — |
| Popup "X" (fechar) | `CharacterPanel.cs` (popup de skill/arma) | `Image` sólida sem sprite, cor `danger` a 92% alpha, glyph = texto "X" | 44×44 | `danger = #D64545` |
| "VER DETALHES" / "OCULTAR DETALHES" | `CharacterPanel.cs` | `RoundedRect(secondaryButton)` + texto | altura 44, largura ~450 | `secondaryButton = #8B6F47` |
| "Voltar" (grid de personagens) | `CharacterSelectController.cs` | `RoundedRect(secondaryButton)` + texto | 160×44 | `secondaryButton = #8B6F47` |
| "Fechar" (painel de detalhe) | `CharacterSelectController.cs` | `RoundedRect(secondaryButton)` + texto | ~237×56 | `secondaryButton = #8B6F47` |
| "Selecionar" (painel de detalhe) | `CharacterSelectController.cs` | `RoundedRect(primaryAction)` + texto | ~237×56 | `primaryAction = #E63946` |
| "Voltar" (`05_SelectOpponent`) | `SelectOpponentController.cs` | Cor literal `(0.30, 0.10, 0.10)` sem sprite, **não usa UITheme** | 160×60 | ~`#4C1A1A` |
| "Escolher" (card de oponente) | `SelectOpponentController.cs` | Cor literal `(0.13, 0.42, 0.13)`, **não usa UITheme** | 200×48 | ~`#22421A` |
| "Continuar" (fim de combate) | `CombatResultPanel.cs` | Cor literal `(0.13, 0.42, 0.13)`, **não usa UITheme** | 240×54 | ~`#22421A` |
| "Escolher" (card de level-up) | `CombatResultPanel.cs` | Mesma cor literal do "Continuar" | 180×44 | ~`#22421A` |
| Toggle velocidade ("1x"/"1.5x") | `CombatHUD.cs` | Cor literal cinza-escuro/dourado conforme estado, **não usa UITheme** | preenche célula da linha inferior | `(0.1,0.1,0.1,0.85)` / `(1,0.84,0,1)` |
| "Skip" | `CombatHUD.cs` | Cor literal cinza-escuro | mesma linha | `(0.1,0.1,0.1,0.85)` |

---

## 3. Ícones de skill (53 no total)

Confirmado direto em `Assets/ScriptableObjects/Skills/` (53 assets T1) x `Assets/Data/UI/Skills/` (PNGs presentes):

- **51 de 53 já têm ícone atribuído** (256×256, fundo transparente, mesmo estilo visual entre si — usar como referência de arte).
- **Faltam 2**: **Garimpeiro** e **Magneto** — coincide com o que o `CLAUDE.md` já registra como skills "ainda não implementadas" mecanicamente (sem `effectText`).
- Nota de nomenclatura (não é um problema, só FYI): o asset `skill_armour.asset` usa o arquivo `skill_armor.png`, e `skill_immortal.asset` usa `skill_immortality.png` — mapeamento manual via campo `iconFileName` no gerador, ambos já resolvidos corretamente.
- Achado à parte: existe um arquivo órfão `skill_backup.png` na pasta sem nenhum asset `skill_backup` correspondente hoje — sobra de uma versão anterior da lista de skills, não usado.
- **Slot de UI**: grid HABILIDADES do `CharacterPanel` = célula 70×70 (borda de tier ao redor, ver seção 6); popup de detalhe = ícone 96×96. Gerar em quadrado (1:1), mesmo estilo dos 51 já existentes.
- Se um ícone não resolver em runtime (`icon == null` em toda a cadeia de tier), a célula mostra só a borda de tier + fundo liso, sem nenhum glyph de fallback — reforça a importância de fechar os 2 que faltam.

---

## 4. Ícones de arma (por tipo: Sharp, Blunt, Heavy, Fast, Long, Thrown)

**Não existem hoje como ícone algum.** A linha "Types" do popup de detalhe de arma (`CharacterPanel.cs`, `TypeColor`/`BuildTypesLine`) mostra só **texto colorido** por tag, sem nenhum glyph:

| Tipo | Cor usada hoje (texto) |
|---|---|
| Sharp | `danger` `#D64545` |
| Blunt | `secondaryButton` `#8B6F47` |
| Heavy | `tierBronze` `#CD7F32` |
| Long | `secondaryButtonAlt` `#6B7B8C` |
| Fast | `currencyGold` `#FFC93C` |
| Thrown | `currencyGem` `#4ECDC4` |
| Ranged | `success` `#52B788` |

Não há RectTransform de ícone ainda pra essa linha (é só uma `TextMeshProUGUI` com rich text `<color>`) — se for gerar ícones por tipo, sugiro ~32×32 a 40×40 (inline antes do texto), usando a paleta acima como referência de cor por tag pra manter consistência com o resto da UI.

Nota separada (fora do pedido, mas relevante): as armas individuais (`WeaponData.icon`/`inHandSprite`) já têm arte real pros 26+5 conjuntos T1 (500×500, ver `Assets/Data/UI/Weapons/`) — **não incluídas neste levantamento**. T2/T3 de cada arma não têm sprite próprio (herdam visualmente o do T1 via `previousTier`) — não é um placeholder no sentido pedido aqui, mas é uma lacuna se algum dia quiser diferenciar visualmente os tiers de uma mesma arma.

---

## 5. Ícones de atributo (Força, Agilidade, Velocidade, Vida)

Hoje é **100% número + texto**, sem nenhum ícone/glyph, via `AttributePipBar.cs`:

- **Badge** (STR/AGI/SPD): círculo `RoundedRect(branco)` 36×36 tintado por tier (paleta HSL de 40 cores, procedural — não é `UITheme`), mostra o **valor numérico** centralizado — ocupa o círculo inteiro, sem espaço sobrando pra um ícone.
- **Pips**: 10 blocos `RoundedRect` de 4px de raio cada, só cor, sem ícone.
- **Label** ("STR"/"AGI"/"SPD"): texto puro, cor `currencyGold`.
- **HP**: não tem badge nem barra no `CharacterPanel` — é só texto `"{hp} HP"`, sem ícone.
- Se quiser adicionar ícone (espada p/ STR, bota p/ AGI, etc.), o encaixe natural é à esquerda do badge, dentro da coluna de label (hoje só texto) — sugiro ~28×28 a 32×32 pra não competir com o badge de 36×36.

---

## 6. Bordas/molduras de tier (cinza/verde/azul — era bronze/prata/ouro até 2026-07-27)

**Confirmado: são só cor aplicada via código, não sprites de moldura.** `CharacterPanel.cs`, `BuildTierIconCell`: a borda é o próprio `RoundedRect(TierColor(tier), 10f)` preenchendo a célula inteira por baixo, com um quadrado de fundo (`panelBackgroundAlt`) por cima, e o ícone (skill/arma) inset ainda mais por cima disso — três camadas de retângulo arredondado, nenhuma delas sprite. `TierColor(tier)` delega pra `UITheme.TierColor` (fonte única desde 2026-07-27, ver UI_PALETTE.md) em vez de um switch bronze/prata/ouro próprio.

- `rarityNormal = #9E9E9E` (T1, cinza)
- `rarityUncommon = #43A047` (T2, verde)
- `rarityRare = #1E88E5` (T3, azul)
- Tamanho: célula 70×70 nas grades HABILIDADES/ARMAS; 96×96 no ícone do popup.

Se for gerar molduras reais (em vez de continuar só com cor), pensar em 3 variantes (bronze/prata/ouro) desenhadas como frame vazado (para o ícone aparecer por dentro), no mesmo tamanho quadrado acima.

---

## 7. Outros ícones genéricos de UI

| Item | Estado atual | Onde | Dimensão |
|---|---|---|---|
| **Cadeado** (personagem bloqueado no grid) | **Não existe.** Personagem bloqueado só perde o `Button` (sem clique) e tem a cor dessaturada — nenhum glyph de cadeado é desenhado | `CharacterCardUI.cs` (dentro do `PortraitBox`) | Se adicionar, 170×170 é a caixa de portrait — cadeado ficaria pequeno sobreposto, ex. 48×48 centralizado |
| **Portrait de personagem sem `previewIcon`** | Cor sólida da paleta (5 cores fixas, escolhidas por hash do nome), sem nenhum silhueta/placeholder genérico | `CharacterCardUI.cs` | 170×170 |
| **Portrait de oponente sem `previewIcon`** | Cor literal marrom sólida `(0.25, 0.20, 0.15)`, comentário no código já assume "placeholder" | `SelectOpponentController.cs` | 120×120 |
| **Moldura de tela cheia do `02_SelectCharacter`** | `RoundedRect(currencyGold)` cobrindo o canvas inteiro (1920×1080) — comentário no próprio código já diz que é placeholder aguardando arte do Leonardo AI | `CharacterSelectController.cs` | 1920×1080 |
| **Troféu** (Win Rate) | **Não existe.** Badge "Win Rate" é só pílula de cor (`success`) + texto "—%" | `CharacterPanel.cs` | badge atual ~pill, altura 20 |
| **Seta lateral de troca rápida de personagem** | **Não implementada** (item do roadmap ainda `[ ]`) | — | — |
| **10 ícones de atributo no level-up** (bônus de +HP/+STR/+AGI/+SPD e 6 combinações) | Cor sólida lisa por opção (10 cores literais fixas, sem `UITheme`), sem ícone | `CombatResultPanel.cs` (cards de level-up) | 80×80 |
| **3 ícones de pet** (Rato/Macaco/Javali) no level-up | **Mesma cor marrom sólida pros 3** — comentário no código confirma "sem sprite de preview próprio ainda" | `CombatResultPanel.cs` | 80×80 |
| **Ícone de arma/skill sem sprite resolvido no level-up** | Cai pra quadrado branco liso (efeito colateral, não intencional) | `CombatResultPanel.cs` | 80×80 |
| **Fundo de cada ícone na `WeaponHUD` (combate)** | Preto 35% alpha (fica dourado quando ativa, vermelho se sabotada) — nunca sprite | `WeaponHUD.cs` | 100×100, rotacionado 45° |
| **Fundo de cada ícone na `SkillsHUD` (combate)** | Preto 45% alpha, sem sprite; se a skill não tiver ícone resolvido, fica só o quadrado preto vazio | `SkillsHUD.cs` | 80×80 |

---

## Resumo — alvos concretos pra gerar primeiro

1. **Moldura de tela cheia** de `02_SelectCharacter` (1920×1080) — já sinalizada no próprio código como placeholder nº1.
2. **2 ícones de skill faltantes**: Garimpeiro, Magneto (256×256, mesmo estilo dos 51 existentes).
3. **10 ícones de atributo** do level-up (80×80).
4. **3 ícones de pet** (Rato/Macaco/Javali) do level-up (80×80).
5. **Cadeado** para personagem bloqueado (novo, ~48×48).
6. **Portrait fallback genérico** de personagem/oponente (170×170 / 120×120).
7. Opcional: **7 ícones de tipo de arma** (Sharp/Blunt/Heavy/Long/Fast/Thrown/Ranged), hoje só texto colorido.
8. Opcional: **glyphs de botão** (Jogar/Voltar/Fechar/Selecionar/X) — hoje todos são retângulo de cor + texto, zero ícone gráfico.
9. Opcional: **troféu** pro badge de Win Rate (recurso ainda não usado de verdade — `winRate` nunca é escrito em lugar nenhum do código ainda).
10. Opcional: **3 molduras de tier reais** (bronze/prata/ouro) pra substituir a borda de cor lisa atual.

Categoria de moeda/diamante/energia fica de fora por enquanto — a HUD correspondente ainda não foi implementada.
