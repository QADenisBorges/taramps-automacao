---
name: BGS Sound
status: draft
updated: 2026-10-30
description: App Android que controla o Taramps PRO 2.4BT + AUDIO do home theater 2.1 do Denis. Só escuro, ciano como cor da marca, feito para a sala escura.
colors:
  # Superfícies (tema único: escuro)
  bg: '#000000'
  surface-1: '#111114'
  surface-2: '#1C1C20'
  surface-sheet: '#141418'
  border-sheet: '#2A2A30'
  divider: '#1C1C20'
  dial-face: '#07090B'
  # Texto
  text-primary: '#F2F2F4'
  text-on-dark: '#FFFFFF'
  text-chip: '#CFCFD4'
  text-secondary: '#9A9AA2'
  text-status: '#8A8A90'
  text-nav-inactive: '#77777D'
  text-dial-neighbor: '#7C7C84'
  text-overlay-label: '#9FB6C0'
  text-overlay-note: '#6F7F86'
  # Marca e estado
  brand: '#3FD0FF'
  on-brand: '#00131A'
  accent-pink: '#FF2BD6'
  glitch-cyan: '#00F0FF'
  warning: '#FFB020'
  stop: '#D92D20'
  connected: '#34D399'
  idle-dot: '#8A8A90'
  disconnected-arc: '#5A5A60'
  # Superfícies tingidas de ciano
  brand-tint-from: '#0B1D26'
  brand-tint-to: '#05090C'
  brand-tint-border: '#16394A'
  device-selected-from: '#0C2430'
  device-card: '#111418'
  device-card-border: '#1E2A30'
  device-icon-bg: '#0B1D26'
  icon-tile: '#06141A'
  overlay-glow: '#0B2230'
typography:
  # Nenhuma fonte de marca foi escolhida: os mocks usam a fonte do sistema (Roboto no Android). Tamanhos tirados dos mocks (quadro de 284 px de largura).
  wordmark-bgs:
    fontFamily: system-ui
    fontWeight: '900'
    lineHeight: '1'
    letterSpacing: -0.02em
  wordmark-sound:
    fontFamily: system-ui
    fontSize: 0.37em
    fontWeight: '500'
    letterSpacing: 0.62em
  appbar-bgs:
    fontFamily: system-ui
    fontSize: 15px
    fontWeight: '900'
    letterSpacing: -0.02em
  appbar-sound:
    fontFamily: system-ui
    fontSize: 9px
    fontWeight: '500'
    letterSpacing: 0.32em
  dial-name:
    fontFamily: system-ui
    fontSize: 26px
    fontWeight: '900'
    letterSpacing: 1.5px
  dial-name-long:
    fontFamily: system-ui
    fontSize: 19px
    fontWeight: '900'
    letterSpacing: 1.5px
  dial-status:
    fontFamily: system-ui
    fontSize: 11px
    fontWeight: '400'
  dial-label-selected:
    fontFamily: system-ui
    fontSize: 12.5px
    fontWeight: '800'
    letterSpacing: 1.5px
  dial-label-neighbor:
    fontFamily: system-ui
    fontSize: 9.5px
    fontWeight: '600'
    letterSpacing: 1.5px
  overlay-label:
    fontFamily: system-ui
    fontSize: 12px
    fontWeight: '400'
    letterSpacing: 0.14em
  overlay-name:
    fontFamily: system-ui
    fontSize: 26px
    fontWeight: '900'
    letterSpacing: 0.04em
  connect-name:
    fontFamily: system-ui
    fontSize: 20px
    fontWeight: '800'
    letterSpacing: 0.02em
  title:
    fontFamily: system-ui
    fontSize: 18px
    fontWeight: '700'
  sheet-title:
    fontFamily: system-ui
    fontSize: 14px
    fontWeight: '800'
  body:
    fontFamily: system-ui
    fontSize: 12px
    fontWeight: '400'
    lineHeight: '1.45'
  list-small:
    fontFamily: system-ui
    fontSize: 11.5px
    fontWeight: '400'
  button:
    fontFamily: system-ui
    fontSize: 12.5px
    fontWeight: '700'
  player-title:
    fontFamily: system-ui
    fontSize: 12.5px
    fontWeight: '600'
  player-meta:
    fontFamily: system-ui
    fontSize: 10.5px
    fontWeight: '400'
  chip:
    fontFamily: system-ui
    fontSize: 11px
    fontWeight: '400'
  hint:
    fontFamily: system-ui
    fontSize: 11px
    fontWeight: '400'
  nav:
    fontFamily: system-ui
    fontSize: 10px
    fontWeight: '400'
rounded:
  xs: 2px
  sm: 8px
  md: 12px
  ia-button: 11px
  lg: 14px
  xl: 16px
  sheet: 22px
  icon-tile: 23%
  circle: 50%
  full: 9999px
spacing:
  gutter: 16px
  stack-home: 10px
  stack-sheet: 9px
  stack-connect: 18px
  header: 8px 16px 6px
  chip: 6px 10px
  setup-chip: 5px 10px
  player: 10px 12px
  ia-bar: 10px 12px
  device-card: 12px
  sheet: 14px 16px 18px
  sheet-button: 11px
  nav: 9px 4px 14px
  connect-body: 24px 18px 20px
motion:
  opening-total: 3.5s
  opening-flicker-ring: 0s–0.5s
  opening-flicker-wave: 0.5s–1.1s
  opening-aura-close: 1.1s–1.7s
  opening-glow-off: 1.1s–1.8s
  opening-name: 1.8s–2.6s
  opening-name-sound-delay: 0.25s
  opening-name-ease: cubic-bezier(.2,.7,.2,1)
  opening-pink-to-cyan: 1.9s–3.1s ease-in-out
  opening-aura-retract: 2.4s–3.5s ease
  spinner-cyan: 1.1s linear, horário
  spinner-pink: 1.5s linear, anti-horário
  search-ring-cycle: 3.2s cubic-bezier(.25,.6,.35,1)
  search-ring-stagger: 0.8s
  search-blip-pulse: 2.4s ease-out
  center-button-pulse: 1.4s ease-in-out
  roda-auto-return: 5s
  reduced-motion-opening: 0.5s fade
  load-finish-spinner-close: 0.3s ease (arco ciano fecha o anel inteiro com brilho; arco rosa some em 0.25s)
  load-finish-screen-out: 0.3s ease (só opacidade; o cabeçalho fica parado)
  load-finish-screen-in: 0.38s cubic-bezier(.2,.7,.2,1) (só opacidade; o movimento fica na Roda e nos blocos)
  load-finish-stagger: 0.06s por bloco (dica, faixa da IA, player, chips), subindo 10px
  load-finish-roda-in: 0.5s cubic-bezier(.2,.7,.2,1) (de escala .82)
  load-finish-applied-flash: 0.6s ease (brilho ciano rápido na Roda ao fechar a aplicação)
  reduced-motion-load-finish: 0.2s fade, sem escala nem sequência
components:
  app-bar:
    background: '{colors.bg}'
    padding: '{spacing.header}'
    symbol-size: 22px
    wordmark-bgs: '{typography.appbar-bgs}'
    wordmark-sound: '{typography.appbar-sound}'
  setup-chip:
    background: '{colors.surface-2}'
    foreground: '{colors.text-chip}'
    radius: '{rounded.full}'
    padding: '{spacing.setup-chip}'
    dot-connected: '{colors.connected}'
    foreground-doubt: '{colors.warning}'
    foreground-disconnected: '{colors.text-secondary}'
  roda:
    size: 268px
    face: '{colors.dial-face}'
    face-radius: 118px
    face-stroke: 'rgba(255,255,255,.12) 2px'
    inner-ring: 'r 88px, rgba(255,255,255,.05) 1px'
    ticks: 'a cada 10°, r 124→128 (rgba(255,255,255,.13)); a cada 30°, r 124→132 (rgba(255,255,255,.27)); traço 1.5px'
    label-path-radius: 101px
    label-path-span: 230°
    selection-arc: '±22° no topo, r 118px, traço 4px, ponta redonda, brilho gaussiano 3px'
    arc-playing: '{colors.brand}'
    arc-chosen: '{colors.text-on-dark}'
    arc-doubt: '{colors.warning}'
    arc-disconnected: '{colors.disconnected-arc}'
    center-symbol: 56px
    label-selected: '{typography.dial-label-selected}'
    label-neighbor: '{typography.dial-label-neighbor}'
    name: '{typography.dial-name}'
    status: '{typography.dial-status}'
    locked: 'opacity .35, saturate .3, cadeado'
  roda-center-button:
    radius: 74px
    fill: 'rgba(63,208,255,.06)'
    stroke: '{colors.brand}'
    stroke-width: 2px
    pulse: '{motion.center-button-pulse}'
  spinner-dual-ring:
    viewbox: 100
    track: 'r 42, rgba(255,255,255,.08), traço 6'
    arc-cyan: 'r 42, {colors.brand}, traço 6, ponta redonda, 80/184 (~30%)'
    arc-pink: 'r 34, {colors.accent-pink}, traço 6, ponta redonda, 50/164 (~23%), mistura screen'
    speed-cyan: '{motion.spinner-cyan}'
    speed-pink: '{motion.spinner-pink}'
    size-connect: 120px
    size-applying: 130px
    size-button: 18px
  search-rings:
    size: 240px
    ring: '2px {colors.brand}'
    count: 4
    cycle: '{motion.search-ring-cycle}'
    stagger: '{motion.search-ring-stagger}'
    center-symbol: 44px
    blip: '12px, {colors.brand}, brilho 10px'
    blip-pulse: '{motion.search-blip-pulse}'
  device-card:
    background: '{colors.device-card}'
    border: '1px {colors.device-card-border}'
    radius: '{rounded.lg}'
    padding: '{spacing.device-card}'
    selected-border: '{colors.brand}'
    selected-background: 'linear-gradient(160deg, {colors.device-selected-from}, {colors.brand-tint-to})'
    not-processor-opacity: 0.45
  player-bar:
    background: '{colors.surface-2}'
    radius: '{rounded.lg}'
    padding: '{spacing.player}'
    cover: '36px, {rounded.sm}'
    play-button: '32px, círculo branco, ícone preto'
    title: '{typography.player-title}'
    meta: '{typography.player-meta}'
  chip:
    background: '{colors.surface-2}'
    foreground: '{colors.text-chip}'
    radius: '{rounded.full}'
    padding: '{spacing.chip}'
    type: '{typography.chip}'
    dot: '6px, {colors.idle-dot}'
  bottom-nav:
    background: '{colors.bg}'
    border-top: '1px {colors.divider}'
    padding: '{spacing.nav}'
    inactive: '{colors.text-nav-inactive}'
    active: '{colors.brand}'
    active-indicator: '20x3px, {colors.brand}, raio 2px, acima do rótulo'
    type: '{typography.nav}'
  sheet:
    background: '{colors.surface-sheet}'
    border-top: '1px {colors.border-sheet}'
    radius: '{rounded.sheet} {rounded.sheet} 0 0'
    padding: '{spacing.sheet}'
    gap: '{spacing.stack-sheet}'
    grab: '36x4px, #3A3A40, {rounded.xs}'
    shadow: '0 -20px 40px #000'
  applying-overlay:
    background: 'radial-gradient(60% 40% at 50% 45%, {colors.overlay-glow} a 90% de opacidade no 0%, {colors.bg} a 95% de opacidade no 70%)'
    backdrop-blur: 6px
    spinner: '{components.spinner-dual-ring.size-applying}'
    label: '{typography.overlay-label}'
    name: '{typography.overlay-name}'
    note: '{colors.text-overlay-note}'
  ia-bar:
    background: 'linear-gradient(180deg, {colors.brand-tint-from}, {colors.brand-tint-to})'
    border: '1px {colors.brand-tint-border}'
    radius: '{rounded.xl}'
    padding: '{spacing.ia-bar}'
    status: '{colors.brand}'
    status-dot: '8px, {colors.brand}, brilho 8px'
    button-radius: '{rounded.ia-button}'
  button-primary:
    background: '{colors.brand}'
    foreground: '{colors.on-brand}'
    radius: '{rounded.md}'
    padding: '{spacing.sheet-button}'
    type: '{typography.button}'
  button-secondary:
    background: 'rgba(255,255,255,.07)'
    foreground: '{colors.text-on-dark}'
    radius: '{rounded.md}'
    padding: '{spacing.sheet-button}'
    type: '{typography.button}'
  button-stop:
    background: '{colors.stop}'
    foreground: '{colors.text-on-dark}'
    radius: '{rounded.ia-button}'
    type: '{typography.button}'
  button-outline:
    background: transparent
    border: '1px #2A2A2E'
    foreground: '{colors.text-secondary}'
    radius: '{rounded.full}'
    padding: 9px 22px
  app-icon:
    background: '{colors.icon-tile}'
    radius: '{rounded.icon-tile}'
    radius-round: '{rounded.circle}'
    symbol-inset: 14%
    symbol-size: 72%
---

# BGS Sound — Design Spine

> **Referências visuais** (escolhidas pelo Denis em 30/10/2026):
> - [.working/logo-final.html](.working/logo-final.html) — logo, ícone e barra do topo.
> - [.working/abertura-escolhida.html](.working/abertura-escolhida.html) — animação de abertura (Neon + Glitch B, rosa vira ciano).
> - [.working/spinner-opcoes-1.html](.working/spinner-opcoes-1.html) — só a **opção 8, Anel duplo**.
> - [.working/conexao-radar.html](.working/conexao-radar.html) — só as **telas 2 e 3** (procurar e listar; conectando).
> - [.working/tela-inicial-roda-estados.html](.working/tela-inicial-roda-estados.html) — a Roda nos estados A a F.
> - [.working/tela-inicial-opcoes-1.html](.working/tela-inicial-opcoes-1.html) — só a **opção 2, Roda** (as outras são histórico rejeitado).
>
> Os spines vencem em caso de conflito com qualquer mock.

Decisões marcadas *(delegada)* foram recomendadas pelo facilitador e aceitas porque o Denis delegou: "o resto siga escolhendo recomendado".

## Brand & Style

O BGS Sound é um controle de equipamento de som, usado no sofá de uma sala escura, com o projetor ligado. A sensação pedida pelo Denis para a marca é **atual, imersiva e tecnológica**. O app é preto, quase sem cor, e o ciano da logo aparece só onde algo está escolhido, tocando ou ativo. O resto fica em cinzas.

A marca tem um símbolo, a **Onda**: uma onda que perde força dentro de um círculo. O rosa existe só em dois momentos: na abertura, como glitch que vira ciano, e no spinner Anel duplo. Ele lembra a abertura e não aparece como cor de interface.

**Tema: só escuro, sem modo claro** *(delegada)*. O app é usado na sala escura.

### Logo

Referência: [.working/logo-final.html](.working/logo-final.html).

- **Símbolo Onda** (viewBox 100×100):
  - círculo `cx 50, cy 50, r 42`, traço 3, sem preenchimento, em branco a 35% de opacidade (no fundo claro, preto a 35%);
  - onda `M14 50 Q20 20 27 50 Q34 78 41 50 Q48 30 55 50 Q62 66 69 50 Q75 41 81 50 Q84 54 87 50`, traço 6, `{colors.brand}`, pontas e junções redondas.
- **Nome Empilhado (formato 1):** **BGS** em `{typography.wordmark-bgs}` em cima e **SOUND** em `{typography.wordmark-sound}` embaixo, com `margin-top .4em`. Quando o nome fica centralizado, SOUND compensa o espaçamento com `margin-right −.62em`.
- **Logo completa:** símbolo a 1,5em do tamanho do nome, à esquerda, com espaço de .42em. Na tela de abertura, o símbolo fica em cima (96 a 120 px) e o nome centralizado embaixo (34 a 38 px), com 18 a 20 px de espaço.
- **Forma de uma linha (barra do topo):** símbolo de 22 px, BGS em `{typography.appbar-bgs}` e SOUND em `{typography.appbar-sound}`, na mesma linha de base, com espaço de .4em.
- **Ícone do app:** tile `{colors.icon-tile}`, cantos `{rounded.icon-tile}` (versão redonda `{rounded.circle}`), símbolo a 72% do tile com recuo de 14%. Tamanhos vistos no mock: 128 (loja), 56 (tela inicial), 44 (grade de apps) e 24 (notificação).
- Histórico rejeitado: o símbolo Cone (laranja) e as outras 11 logos. Ver EXPERIENCE.md › Inspiration & Anti-patterns.

## Colors

O preto é o canvas, o ciano marca o que está escolhido e as outras cores aparecem só como sinal de estado.

- **Preto (`{colors.bg}` #000000)** é o fundo do app. Combina com a sala escura e com o projetor.
- **Superfícies (`{colors.surface-1}` #111114, `{colors.surface-2}` #1C1C20, `{colors.surface-sheet}` #141418)** são usadas, nessa ordem, em blocos, em chips e no player, e em folhas. O mostrador da Roda usa `{colors.dial-face}` #07090B.
- **Ciano da marca (`{colors.brand}` #3FD0FF)** marca o selecionado, o que está tocando, a aba ativa, o arco da Roda, os anéis da busca, o botão principal e o estado ativo da IA. O texto sobre ciano usa `{colors.on-brand}` #00131A (10,5:1).
- **Rosa (`{colors.accent-pink}` #FF2BD6)** aparece **só** no glitch da abertura e no arco interno do spinner Anel duplo. Nunca entra em texto, botão, estado ou fundo.
- **Ciano do glitch (`{colors.glitch-cyan}` #00F0FF)** existe **só** na camada de glitch da abertura e vira `{colors.brand}` antes do fim.
- **Aviso (`{colors.warning}` #FFB020)** é a cor do Setup em dúvida: o chip "Setup em dúvida", o arco e o "MUDO" da Roda, o título da folha e os marcadores "≠" do que não bate.
- **Parar (`{colors.stop}` #D92D20)** pinta só o fundo do botão PARAR IA. Branco sobre ele dá 4,8:1 (o #FF4D4D dos mocks dava 3,27:1 e foi trocado).
- **Conectado (`{colors.connected}` #34D399)** é o ponto no chip do Setup quando há conexão.
- **Cinzas de texto:** `{colors.text-primary}` #F2F2F4 (texto principal), `{colors.text-chip}` #CFCFD4, `{colors.text-secondary}` #9A9AA2 (dicas e legendas), `{colors.text-nav-inactive}` #77777D (abas inativas) e `{colors.text-dial-neighbor}` #7C7C84 (memórias vizinhas na borda da Roda).
- **Tinta de ciano:** o degradê `{colors.brand-tint-from}` → `{colors.brand-tint-to}` com borda `{colors.brand-tint-border}` é o fundo da faixa da IA. O cartão do aparelho escolhido vai de `{colors.device-selected-from}` a `{colors.brand-tint-to}`.

**Contraste medido no preto:** texto principal 18,8:1 · secundário 7,5:1 · aba inativa 4,7:1 · vizinho da Roda 4,8:1 · ciano 11,7:1 · aviso 11,5:1 · rosa 6,6:1. **Exceção:** o branco sobre `{colors.stop}` dá **3,27:1**, abaixo de 4,5:1 para texto de 12,5 px. Ver Questões em aberto no EXPERIENCE.md.

Evitar: modo claro, rosa fora da abertura e do spinner, ciano como decoração e outras cores de estado além das listadas.

## Typography

**Nenhuma fonte de marca foi escolhida.** Os mocks usam a fonte do sistema (`system-ui`, que é a Roboto no Android). [ASSUMPTION] O app usa a fonte do sistema até o Denis escolher outra.

O nome da logo é a única tipografia "de marca": BGS em peso 900 com espaçamento negativo e SOUND em peso 500, pequeno e bem espaçado.

| Papel | Token | Onde |
|---|---|---|
| Nome da memória no centro da Roda | `{typography.dial-name}` (26 px/900); `{typography.dial-name-long}` (19 px) quando o nome tem mais de 8 caracteres | Roda |
| Memória no topo da borda | `{typography.dial-label-selected}` | Roda |
| Vizinhas na borda | `{typography.dial-label-neighbor}` | Roda |
| Linha de estado da Roda | `{typography.dial-status}` | "tocando · aplicada agora", "toque para aplicar" |
| Aplicando | `{typography.overlay-label}` (CAIXA ALTA) + `{typography.overlay-name}` | Aplicação em tela toda |
| Nome do aparelho ao conectar | `{typography.connect-name}` | Conectando |
| Título de tela | `{typography.title}` | "Procurando aparelhos" |
| Título de folha | `{typography.sheet-title}` | "Som mudo até você escolher" |
| Texto | `{typography.body}` | Folhas, avisos |
| Botão | `{typography.button}` | Todos os botões |
| Player | `{typography.player-title}` / `{typography.player-meta}` | Mini player |
| Chip / dica / aba | `{typography.chip}` / `{typography.hint}` / `{typography.nav}` | — |

Números sempre com vírgula decimal e unidade (−1,5 dB, 2,1 ms) *(delegada)*.

[ASSUMPTION] Os tamanhos vêm de mocks com quadro de 284 px de largura e valem aqui como sp. Os menores (9,5 a 11 px) ainda precisam ser conferidos no aparelho, na sala escura.

## Layout & Spacing

- Só celular, em retrato. Margem lateral `{spacing.gutter}` (16 px). O conteúdo fica numa coluna só.
- **Aba Som, de cima para baixo:** barra do topo, a Roda centralizada (268 px), uma linha de dica, o mini player, uma linha de chips (IA e "+ Nova memória"), e as abas fixas embaixo. O espaço entre blocos é `{spacing.stack-home}`. O chip do preset ("Preset: Ação ›") entra embaixo da Roda *(delegada)*. [ASSUMPTION] A posição exata dele não foi desenhada.
- **Busca e conexão:** título, anéis (240 px), dica e lista de aparelhos, com espaço `{spacing.stack-connect}` e enchimento `{spacing.connect-body}`. Ao conectar, o anel duplo fica 70 px abaixo do topo do corpo, o nome logo abaixo e o Cancelar no pé.
- **Folhas** sobem do pé, na largura toda, por cima do conteúdo.
- **Aplicando** cobre a tela toda, inclusive a barra do topo e as abas.

## Elevation & Depth

A profundidade vem da tonalidade e do brilho, não de sombras.

- As camadas são preto, depois `surface-1`/`surface-2`, depois a folha `{colors.surface-sheet}`. A folha é a única com sombra (`0 -20px 40px #000`).
- O **brilho ciano** é usado para "aceso": o arco da Roda (desfoque gaussiano de 3 px), os pontos dos aparelhos na busca, o ponto de estado da IA e o brilho dos anéis.
- **Aplicando** é uma camada escura por cima de tudo: um degradê radial azul-escuro, um desfoque de 6 px do fundo e o anel duplo no centro.
- **Travado/apagado:** opacidade .35 e saturação .3. A Roda travada mostra um cadeado no centro.

## Shapes

- O círculo é a forma da marca: o símbolo, a Roda, o botão do centro, o spinner, os anéis da busca e o botão de play.
- Chips, abas e o Cancelar são pílulas (`{rounded.full}`).
- Os cartões e blocos têm cantos médios: botões de folha `{rounded.md}`, player e cartão de aparelho `{rounded.lg}`, faixa da IA `{rounded.xl}` e folha `{rounded.sheet}` só nos cantos de cima.
- A capa do player e o ícone do aparelho usam `{rounded.sm}`. O ícone do app usa `{rounded.icon-tile}`.

## Components

- **Barra do topo (`app-bar`)** — logo de uma linha à esquerda e, à direita, o chip do Setup e a engrenagem (a engrenagem aparece em `logo-final.html`). O chip do Setup é pílula `{colors.surface-2}` com o nome e "›". Ele tem três aparências: conectado (ponto `{colors.connected}`), "Setup em dúvida" (texto `{colors.warning}`) e "Sem conexão" (texto `{colors.text-secondary}`).
- **Roda (`roda`)** ([estados A a F](.working/tela-inicial-roda-estados.html)) — mostrador de 268 px:
  - face `{colors.dial-face}` com raio 118, um anel interno fraco e marcas na borda a cada 10° (maiores a cada 30°);
  - os nomes das memórias correm pela borda, num arco de 230° em cima. A escolhida fica no topo, sob o arco de seleção (±22°, traço 4 com brilho), e as vizinhas aparecem a ±76°;
  - no centro, o símbolo (56 px), o nome da memória e a linha de estado.
  - Cor do arco e do topo: `{colors.brand}` quando está tocando, branco quando foi escolhida e ainda não aplicada, `{colors.warning}` na dúvida e `{colors.disconnected-arc}` sem conexão.
  - Travada: apagada e com cadeado.
- **Botão do centro (`roda-center-button`)** — aparece só quando a memória escolhida não é a que está tocando. É um círculo de raio 74 com preenchimento ciano a 6% e borda ciano de 2 px, pulsando (opacidade da borda .45↔1 em `{motion.center-button-pulse}`).
- **Spinner Anel duplo (`spinner-dual-ring`)** ([opção 8](.working/spinner-opcoes-1.html)) — dois arcos de traço 6 com pontas redondas:
  - o ciano (r 42, ~30% do círculo) gira no sentido horário em 1,1 s;
  - o rosa (r 34, ~23%) gira no anti-horário em 1,5 s, com mistura *screen*;
  - fica sobre uma trilha branca a 8%. Tamanhos: 120 px ao conectar, 130 px aplicando e 18 px dentro de botões.
  - O spinner não mostra números. É o indicador de **todo** carregamento.
- **Anéis da busca (`search-rings`)** ([telas 2 e 3](.working/conexao-radar.html)) — 4 anéis de 2 px em `{colors.brand}` saem da logo (44 px, no centro de uma área de 240 px). Cada um cresce de 18% a 100% e some, num ciclo de 3,2 s, com 0,8 s entre eles. O brilho aparece no começo do ciclo.
  - Cada aparelho encontrado vira um ponto ciano de 12 px com um pulso a cada 2,4 s e o nome ao lado.
  - Um aparelho que não é processador aparece apagado (40%), sem brilho.
  - **Só ciano.** A variação com anéis rosa foi descartada.
- **Cartão de aparelho (`device-card`)** — ícone em quadro de 30 px, nome em negrito e uma linha de contexto ("Taramps · Casa 2.1 da última vez"), com "Conectar ›" em ciano à direita.
  - Processador: borda ciano e fundo em degradê.
  - Outro Bluetooth: opacidade .45, com "Não é um processador".
- **Mini player (`player-bar`)** — bloco `{colors.surface-2}` com capa de 36 px, título (uma linha, cortado com reticências), linha "Playlist da FILME · 2 de 8" e botão de play redondo, branco, de 32 px.
- **Chips (`chip`)** — pílulas `{colors.surface-2}` com texto `{colors.text-chip}`. O chip da IA leva um ponto de estado de 6 px. Usados para a IA ("IA pareada · parada"), "+ Nova memória" e o preset ("Preset: Ação ›").
- **Abas (`bottom-nav`)** — 4 rótulos de texto (Som, IA, Ajustes, Setup) num fundo preto com fio em cima. A aba ativa fica em ciano, com um traço de 20×3 px acima do rótulo. [ASSUMPTION] Os mocks não têm ícones nas abas, e isso não foi decidido.
- **Folhas (`sheet`)** — sobem do pé, com fundo `{colors.surface-sheet}`, cantos de cima `{rounded.sheet}`, pegador de 36×4 px e espaço `{spacing.stack-sheet}`. Os botões ficam empilhados na largura toda: o primeiro em `button-primary` e os outros em `button-secondary`. A legenda final usa `{colors.text-secondary}`.
  - Na **folha da dúvida**, o título "🔇 Som mudo até você escolher" fica em `{colors.warning}` e a lista de diferenças usa marcadores "≠" em aviso.
- **Aplicando em tela toda (`applying-overlay`)** — camada escura sobre o app, com o anel duplo de 130 px, "APLICANDO" em `{typography.overlay-label}`, o nome da memória em `{typography.overlay-name}` e, no pé, a nota "Mantenha o celular perto do processador" em `{colors.text-overlay-note}`.
- **Faixa da IA (`ia-bar`)** — bloco em degradê de tinta ciano com borda ciano escura. Tem três partes:
  - uma linha de estado em ciano com ponto brilhante ("IA ajustando o EQ da FILME");
  - a última mudança ("JBL L · 63 Hz −1,5 dB") com "agora" em cinza;
  - dois botões lado a lado: Pausar (`rgba(255,255,255,.08)`) e **PARAR IA** (`button-stop`).
- **Botões** — `button-primary` (ciano, texto escuro, ex.: "Escolher um Setup", "Procurar aparelhos"), `button-secondary` (branco a 7%), `button-stop` (PARAR IA) e `button-outline` (pílula com fio, ex.: "Cancelar"). Com carregamento, o botão mostra o spinner de 18 px antes do texto ("Aplicando FILME", "Salvando").
- **Componentes sem mock** — telas e peças ainda sem visual: o botão Salvar fixo no pé de Ajustes, a linha de saída, o controle de parâmetro, a ficha de equipamento, o interruptor de chave de acesso, o selo de origem da ficha, o esquema isométrico, os medidores (voltímetro e clip), o gerador com contagem, o alerta "DESLIGUE O MÓDULO AGORA" em tela toda, a faixa de só leitura e os avisos (CAP-14). [ASSUMPTION] Eles usam os tokens acima; o visual de cada um está nas Questões em aberto do EXPERIENCE.md.

### Movimento

Referência: [.working/abertura-escolhida.html](.working/abertura-escolhida.html)

- **Abertura (3,5 s, só a frio)**, a partir de `abertura-escolhida.html`:
  - **0 a 0,5 s:** o círculo pisca e firma. As camadas ciano `{colors.glitch-cyan}` e rosa `{colors.accent-pink}` ficam a ±9 px, borradas a 1,2 px, com círculo de traço 4 e onda de traço 7, em mistura *screen*.
  - **0,5 a 1,1 s:** a onda pisca e acende, com brilho ciano (`drop-shadow` de 6 e 16 px).
  - **1,1 a 1,7 s:** as camadas se aproximam até ±3,5 px (aura a 90%) e o brilho apaga até 1,8 s.
  - **1,8 a 2,6 s:** o nome sai do desfoque. Começa com `blur 10px`, escala 1,15 e opacidade 0, com a curva `{motion.opening-name-ease}`. SOUND entra 0,25 s depois de BGS.
  - **1,9 a 3,1 s:** o rosa e o ciano do glitch passam a `{colors.brand}` (ease-in-out).
  - **2,4 a 3,5 s:** a aura se recolhe para dentro do símbolo (deslocamento e desfoque a zero) e some.
  - **3,5 s:** a logo fica parada, nítida, igual à do app. O fade de 4,6 a 5 s do mock é só da demonstração.
  - A abertura não tem som e um toque pula *(delegada)*.
- **Spinner:** `{motion.spinner-cyan}` e `{motion.spinner-pink}`, girando sem parar.
- **Busca:** `{motion.search-ring-cycle}`, com `{motion.search-ring-stagger}` entre os anéis. O pulso dos pontos é `{motion.search-blip-pulse}`.
- **Botão do centro da Roda:** pulso de `{motion.center-button-pulse}`.
- **Movimento reduzido** (configuração do Android) *(delegada)*:
  - a abertura vira um fade de 0,5 s da logo;
  - os anéis da busca e o anel duplo giram mais devagar, sem pulsos;
  - nada pisca. Isso inclui o neon da abertura e a borda pulsante do botão do centro.
  - [ASSUMPTION] A velocidade "mais devagar" não tem valor.

## Do's and Don'ts

| Fazer | Não fazer |
|---|---|
| Fundo preto em todas as telas; só tema escuro | Modo claro ou fundos claros dentro do app |
| Ciano só para escolhido, tocando, ativo e ação principal | Ciano como enfeite ou em várias coisas que disputam a atenção |
| Rosa só no glitch da abertura e no spinner Anel duplo | Rosa em botão, texto, estado, anel da busca ou fundo |
| Spinner Anel duplo em todo carregamento, sem números | Contador de leituras (1 a X), barras de progresso com número ou porcentagem |
| Anéis ciano saindo da logo ao procurar | Radar de feixe girando com rastro; anéis ciano e rosa alternados |
| Aviso (#FFB020) só para Setup em dúvida | Aviso para decorar; vermelho de erro como fundo de tela (só o PARAR IA é vermelho) |
| Apagar e travar (opacidade .35 e cadeado) o que não pode ser usado | Esconder a Roda travada ou mostrar um valor que o app não sabe |
| Abertura terminando exatamente na logo parada, nítida | Aura ou borrão sobrando depois da abertura |
| Logo de uma linha na barra do topo e Empilhada na abertura | Esticar, recolorir ou separar a onda do círculo |
