# Relatório de Espaço em Disco — Personagens vs. Resto do Projeto

**Data:** 2026-07-10
**Escopo:** Auditoria somente — nenhum arquivo do projeto foi alterado.
**Objetivo:** decidir quantos personagens lançar no jogo sem pesar o build mobile.

## TL;DR

**Personagens não são o problema.** Cada personagem custa ~2.3–2.9 MB. O que realmente pesa o
build são os **50 backgrounds de arena em `Assets/Resources/BattleGround`** (497 MB em disco,
todos embutidos no instalador independente de serem usados ou não — ver seção 4). Isso é ~50-70x
o peso de todos os 3 personagens somados. Se a meta é reduzir o tamanho de instalação, o alvo
certo é essa pasta, não o roster de personagens.

---

## 1. Tamanho por personagem

Personagens ficam em `Assets/Personagens/<Nome>/`, com `splashArt` (quando existe) numa pasta
irmã `Assets/Personagens/SplashArt/`. Cada um tem: 1 prefab (Spriter2UnityDX), 1 Animator
Controller, sprites (`Graphics/` + `Prefab/*.png`), animation clips (`.anim`) e opcionalmente uma
splash art. **Sem áudio ainda** — nenhum arquivo de som existe no projeto hoje (fase de áudio é
futura, ver ROADMAP_FUTURO.md).

### Tamanho "limpo" (só o que de fato é referenciado/vai pro build)

| Personagem | Total | Prefab | Sprites (PNG) | Animation Clips | Animator Controller | Splash Art |
|---|---|---|---|---|---|---|
| Assassin Guy | **2.87 MB** | 1.89 MB | 0.15 MB | 0.81 MB (4 clips) | 0.02 MB | — (ainda não tem) |
| Medieval Warrior | **2.85 MB*** | 1.72 MB** | 0.16 MB | 0.07 MB** (1 clip solto) | 0.03 MB | 0.40 MB |
| Medieval Warrior Girl | **1.64 MB** | 0.69 MB | 0.15 MB | 0.77 MB (4 clips) | 0.03 MB | — (ainda não tem) |
| **Total (3 personagens)** | **7.36 MB** | | | | | |
| **Média por personagem** | **2.45 MB** | | | | | |

\* Inclui os 0.40 MB de `SplashArt/MedievalWarrior.jpg` (única splash art existente hoje — os
outros 2 personagens ainda usam o placeholder dourado, ver CLAUDE.md).

\** Ressalva de metodologia: o prefab do Medieval Warrior foi exportado pelo Spriter2UnityDX com
os `AnimationClip` **embutidos dentro do próprio `.prefab`** (36 ocorrências de `AnimationClip` no
YAML), em vez de arquivos `.anim` separados como nos outros dois personagens — por isso o prefab
dele é proporcionalmente maior e o bucket "Animation Clips" menor. O **total** por personagem
continua comparável; só a divisão entre as colunas "Prefab"/"Animation Clips" não é 1:1 entre
personagens.

### Limpeza feita (2026-07-10) — ~1.27 MB removidos, com uma ressalva importante

A auditoria original apontou 4 arquivos como órfãos (`Prefab/Medieval Warrior - Copy.prefab` +
`.controller`, e um `Medieval Warrior.prefab` + `.controller` soltos na raiz da pasta, fora de
`Prefab/`) além do `Animations.scml`. Antes de apagar, refiz a checagem de GUID **também dentro**
dos arquivos (não só se o prefab em si era referenciado por `PlayerProfile.characterPrefab`) — e
descobri que a checagem original estava incompleta:

- **`Prefab/Medieval Warrior - Copy.prefab` NÃO é lixo** — ele embute um `AnimationClip`
  (`fileID: -1604484028028560711`) que é referenciado como `m_Motion` de um estado real tanto no
  `Medieval Warrior.controller` **em uso** quanto no `Medieval Warrior Girl.controller` **em uso**.
  Ou seja, esse "Copy" secretamente serve de contêiner de um clipe de animação compartilhado por
  dois personagens — apagá-lo quebraria uma animação (silenciosamente, como "Motion ausente") em
  AMBOS. **Mantido**, junto do `Medieval Warrior - Copy.controller` (referenciado só por esse
  prefab, mas inofensivo de manter — evita deixar uma referência quebrada dentro dele).
- **`Medieval Warrior/Medieval Warrior.prefab` + `Medieval Warrior/Medieval Warrior.controller`**
  (os dois soltos na raiz, fora de `Prefab/`) — confirmados como referenciando **só um ao outro**
  (o prefab aponta pro controller via `m_Controller`, o controller embute clipes só usados pelos
  próprios estados dele), sem nenhuma referência externa de nenhum outro arquivo do projeto. Essa
  dupla era de fato um par isolado e sem uso — **removidos**.
- **`Animations.scml`** — confirmado sem nenhuma referência em lugar nenhum (arquivo fonte do
  Spriter, só usado em tempo de import no Editor) — **removido**.

**Resultado:** `Assets/Personagens/Medieval Warrior/` caiu de 4.5 MB pra **3.2 MB** (~1.27 MB
liberados). Menos que a estimativa inicial de ~2.5 MB porque o "Copy" prefab/controller, que eu
tinha marcado como lixo na auditoria, na verdade é uma dependência real e ficou.

---

## 2. Onde está o peso de verdade (não é nos personagens)

| Pasta | Tamanho em disco | O que é | É por personagem? |
|---|---|---|---|
| `Assets/Resources/BattleGround` | **497 MB** | 52 backgrounds de arena, 3840×2160 cada | Não — compartilhado |
| `Assets/Data/UI/Pets` | 20 MB | Sprites dos 3 pets (Mouse/Monkey/Boar) | Não — sistema à parte |
| `Assets/Data/UI/Skills` | 13 MB | Ícones das 53 skills | Não — compartilhado |
| `Assets/Data/UI/Weapons` | 7.7 MB | Sprites de armas legadas (Whip etc.) | Não — compartilhado |
| `Assets/Data/UI/SkillEffect` | 5.4 MB | Efeitos visuais de skills | Não — compartilhado |
| `Assets/Data/Weapons` | 0.4 MB | 78 `WeaponData.asset` (as 26 famílias T1-T3) | Não — compartilhado |
| `Assets/Personagens` (3 chars) | 9.6 MB | Ver seção 1 | **Sim** |

`Assets/Resources/BattleGround` sozinho é **~53× maior** que os 3 personagens somados. E por estar
dentro de uma pasta `Resources/`, **as 52 imagens vão inteiras pro instalador sempre**, mesmo o
código só carregando 1 por vez em runtime via `Resources.Load` (ver CLAUDE.md, seção "Background
Aleatório") — a seleção em runtime não evita empacotar as outras 51 no build.

---

## 3. Projeção: quanto cada personagem novo adiciona

Com a média limpa de **2.45 MB/personagem** (prefab + sprites + anims + controller + splash art),
seguindo o mesmo padrão de assets já usado (idle + slash/dagger/heavy + catch weapon + block +
ícone + splash art):

| Personagens adicionados | Peso extra estimado |
|---|---|
| +5 | ~12 MB |
| +10 | ~25 MB |
| +20 | ~49 MB |
| +50 | ~123 MB |

Mesmo lançando **20 personagens novos de uma vez** (~49 MB), isso ainda fica abaixo do peso de
UMA ÚNICA pasta de backgrounds (497 MB em disco / ~55-120 MB estimados já comprimidos pro mobile,
ver seção 4). Personagens não são o fator limitante pra decidir quantos lançar.

---

## 4. Tamanho do build — estimativa (não foi possível medir de verdade)

**Não há Build Report nem pasta de build no projeto** (nenhuma pasta `Builds/` encontrada, e não
tenho acesso ao Unity Editor/CLI neste ambiente pra rodar um build real e ler o
`Window → Analysis → Build Report`). Os números abaixo são **estimativa por metodologia**, não
medição — recomendo gerar o Build Report de verdade no Editor antes de decidir algo com base
nisso.

**Metodologia:** o tamanho em disco dos `.png`/`.jpg` fonte (PNG/JPEG comprimem muito bem imagens
fotográficas/pintadas) **não** é uma boa proxy pro tamanho real no build — o Unity recomprime
tudo pro formato de textura da plataforma (ASTC no iOS, ETC2/ASTC no Android), que tem uma taxa de
bits fixa por pixel, geralmente pior que PNG/JPEG pra esse tipo de imagem. O que importa é
resolução final × formato de compressão:

- **Backgrounds**: `.meta` mostra `maxTextureSize: 2048` sem overrides por plataforma
  (`overridden: 0` em iOS/Android) — a imagem nativa 3840×2160 é reamostrada pra **2048×1152** no
  import. A ~0.44–1.0 byte/pixel (faixa ASTC 6×6 até ETC2 RGBA8, dependendo da plataforma/qualidade
  "Normal" já configurada), cada background comprimido fica entre **~1.0 MB e ~2.2 MB** — bem
  longe dos 15 MB do PNG fonte. Com 52 arquivos: **~55 MB a ~120 MB** no build, ainda o maior
  bloco isolado do jogo, só que bem menos catastrófico que os 497 MB brutos.
- **Personagens**: sprites nativos pequenos (900×900 até 480×480, bem abaixo do limite de
  compressão) — o tamanho em disco (2.45 MB médio) é uma proxy razoável do custo real no build,
  sem o mesmo efeito de "resolução cortada" dos backgrounds.
- **UI/Skills/Weapons/Pets** (~46 MB em disco): ícones, provavelmente resolução baixa/média —
  estimativa grosseira de ~35–50 MB no build.
- **Overhead do engine** (runtime Unity/IL2CPP, shaders padrão, etc.): tipicamente ~20–40 MB pra
  um jogo 2D simples, independente dos assets.

**Estimativa total do build mobile: ~140 MB a ~220 MB**, com os backgrounds de arena respondendo
por 40–60% disso sozinhos — os 3 personagens atuais somados (7.36 MB) são <5% do total estimado.

---

## 5. Recomendação

1. **Não trave o lançamento de personagens no orçamento de espaço.** A ~2.3–2.9 MB cada, dá pra
   lançar praticamente qualquer quantidade de personagens prontos (10, 20+) sem impacto relevante
   no tamanho de instalação — o teto de bom senso pra apps mobile (manter a instalação inicial
   look ~150 MB pra evitar avisos de rede/download em alguns mercados, ou bem abaixo de 200 MB
   pra iOS antes de pedir confirmação de rede celular) já está sob risco por causa dos
   **backgrounds**, não dos personagens.
2. **O alvo prioritário de otimização é `Assets/Resources/BattleGround`.** Mover essas 50+ imagens
   pra **Addressables com download remoto/sob demanda** (ou pelo menos pra fora de `Resources/`,
   que sempre empacota tudo) cortaria a maior fatia do instalador de uma vez — muito mais impacto
   que qualquer decisão sobre roster de personagens. Alternativa mais simples (sem mexer em
   Addressables): reduzir a quantidade de backgrounds distintos ou baixar ainda mais o
   `maxTextureSize` de import (hoje 2048 — já ajuda bastante vs. os 3840×2160 originais, mas dá pra
   ir mais agressivo se a arte aguentar, ex.: 1536).
3. **Addressables para personagens: não vale o esforço agora.** Dado o custo baixíssimo por
   personagem (~2.5 MB), a complexidade de implementar download sob demanda especificamente pros
   characters não se paga — o ganho seria marginal comparado ao esforço de engenharia. Vale
   revisitar essa ideia só se o roster crescer pra dezenas/centenas de personagens no futuro, ou
   se cada um passar a incluir voice-over/áudio pesado (fase de áudio ainda não implementada).
4. ~~Limpeza de graça~~ — **feita em 2026-07-10** (ver seção 1): removidos o `Medieval Warrior.prefab`
   + `.controller` duplicados soltos na raiz da pasta e o `Animations.scml` não referenciado
   (~1.27 MB liberados). O `Prefab/Medieval Warrior - Copy.prefab` **não** foi removido — acabou
   sendo uma dependência real (contém um `AnimationClip` usado por dois personagens), ver detalhe
   na seção 1.
5. **Validar com número real:** gerar um build real (Android ou iOS) e abrir
   `Window → Analysis → Build Report` no Editor pra confirmar/ajustar as estimativas da seção 4 —
   as escolhas de compressão de textura por plataforma (Android/iOS) podem estar diferentes do que
   os `.meta` sugerem se houver overrides que eu não tenha visto, e o Build Report dá o número
   exato por asset.
