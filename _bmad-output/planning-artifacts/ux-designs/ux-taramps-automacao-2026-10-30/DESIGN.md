---
name: BGS Sound
status: final
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
  text-overlay-note: '#6F7F86'
  # Marca e estado
  brand: '#3FD0FF'
  on-brand: '#00131A'
  accent-pink: '#FF2BD6'
  glitch-cyan: '#00F0FF'
  warning: '#FFB020'
  connected: '#34D399'
  on-connected: '#00140C'
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
  # Criação do som (30/10)
  stage-floor: '#03070A'
  channel-done: '#16394A'
typography:
  # Nenhuma fonte de marca foi escolhida: os mocks usam a fonte do sistema (Roboto no Android). Tamanhos tirados dos mocks (quadro de 284 px de largura; a criação do som, de 324 px).
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
  dial-preset:
    fontFamily: system-ui
    fontSize: 11.5px
    fontWeight: '600'
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
  aplicando-label:
    fontFamily: system-ui
    fontSize: 17px
    fontWeight: '800'
    letterSpacing: 0.18em
    textTransform: uppercase
  aplicando-name:
    fontFamily: system-ui
    fontSize: 20px
    fontWeight: '800'
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
  flow-title:
    fontFamily: system-ui
    fontSize: 13px
    fontWeight: '700'
  flow-subtitle:
    fontFamily: system-ui
    fontSize: 10.5px
    fontWeight: '400'
  question:
    fontFamily: system-ui
    fontSize: 22px
    fontWeight: '800'
    lineHeight: '1.25'
  answer:
    fontFamily: system-ui
    fontSize: 14px
    fontWeight: '600'
  creation-phrase:
    fontFamily: system-ui
    fontSize: 24px
    fontWeight: '800'
    lineHeight: '1.25'
    letterSpacing: 0.01em
  creation-value:
    fontFamily: system-ui
    fontSize: 15px
    fontWeight: '600'
    fontVariantNumeric: tabular-nums
  summary-item:
    fontFamily: system-ui
    fontSize: 13px
    fontWeight: '700'
  summary-reason:
    fontFamily: system-ui
    fontSize: 11px
    fontWeight: '400'
    lineHeight: '1.35'
  schematic-label:
    fontFamily: system-ui
    fontSize: 9px
    fontWeight: '400'
  schematic-processor:
    fontFamily: system-ui
    fontSize: 8.5px
    fontWeight: '400'
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
  button-flow:
    fontFamily: system-ui
    fontSize: 14px
    fontWeight: '800'
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
  stack-question: 10px
  header: 8px 16px 6px
  flow-top: 2px 8px 0
  chip: 6px 10px
  setup-chip: 5px 10px
  player: 10px 12px
  device-card: 12px
  sheet: 14px 16px 18px
  sheet-button: 11px
  answer: 0 18px
  summary-row: 10px 14px
  stage-gutter: 18px
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
  load-finish-stagger: 0.06s por bloco (dica, player, chips), subindo 10px
  load-finish-roda-in: 0.5s cubic-bezier(.2,.7,.2,1) (de escala .82)
  load-finish-applied-flash: 0.6s ease (brilho ciano rápido na Roda ao fechar a aplicação)
  reduced-motion-load-finish: 0.2s fade, sem escala nem sequência
  # Criação do som (30/10)
  question-in: 0.32s cubic-bezier(.2,.8,.2,1), de 28px à direita (ao voltar, de 28px à esquerda)
  answer-mark: 0.18s (a resposta acende antes de avançar)
  creation-start: 0.3s (palco parado antes da 1ª frase)
  phrase-in: 0.8s cubic-bezier(.3,.55,.35,1), da borda esquerda ao centro, desfoque 12px→0 preso à posição
  phrase-hold: 1.3s
  phrase-out: 0.65s cubic-bezier(.6,0,.7,.45), do centro à borda direita, desfoque 0→12px preso à posição
  phrase-gap: 0.12s
  value-in: 0.9s, mesma curva e mesmo foco da frase, começa junto
  value-bar: 0.6s cubic-bezier(.45,0,.2,1) (da posição antiga à nova)
  value-strike: 0.3s ease-out (risco no valor de antes)
  value-arrow: 0.24s, de 8px à esquerda
  value-new: 0.4s, 0.1s depois da seta, desfoque 6px→0
  value-out: 0.62s, 0.06s depois da frase, mesma curva da saída
  horizon-follow: 0.5s cubic-bezier(.45,0,.2,1)
  passage: 1.6s cubic-bezier(.45,0,.2,1) (anel do chão até o lugar do anel do Aplicando)
  passage-scrim: 0.8s ease (camada preta a 90%)
  passage-spin: uma volta, curva seno (1−cos πt)/2; abre de 0 a 25%, fecha no plano de 70 a 100%
  aplicando-schematic-in: 0.5s ease, a 1.0s da passagem
  aplicando-text-in: 0.35s ease-out, a 1.2s da passagem; APLICANDO desce 6px, o nome sobe 6px, 0.08s depois
  aplicando-channel: 0.7s por canal (gravado e relido)
  aplicando-final-hold: 1s (tudo gravado, antes do resumo)
  aplicando-out: 0.3s ease (só opacidade)
  summary-item-in: 0.35s cubic-bezier(.2,.8,.2,1), subindo 10px, 0.07s entre itens
  toast: 2.6s
  reduced-motion-creation: 0.3s fade (frases e Aplicando), sem passagem
components:
  app-bar:
    background: '{colors.bg}'
    padding: '{spacing.header}'
    symbol-size: 22px
    wordmark-bgs: '{typography.appbar-bgs}'
    wordmark-sound: '{typography.appbar-sound}'
    gear-hit-area: 44x44px
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
    preset: '{typography.dial-preset}, {colors.brand}'
    preset-pill: 'altura 22px, raio 11px, ciano a 10% com borda ciano a 33%'
    preset-hit-area: 160x48px
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
    size-applying: 30% da largura da tela
    size-floor: 56% da largura da tela, traço 2.5, opacidade .35
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
  bottom-nav:
    background: '{colors.bg}'
    border-top: '1px {colors.divider}'
    padding: '{spacing.nav}'
    tabs: 'Som, Ajustes, Setup'
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
    scrim: 'preto a 60%'
  applying-overlay:
    background: 'radial-gradient(60% 40% at 50% 45%, {colors.overlay-glow} a 90% de opacidade no 0%, {colors.bg} a 95% de opacidade no 70%)'
    background-creation: 'preto a 90% sobre o palco da criação'
    backdrop-blur: 6px
    block: 'embaixo; APLICANDO, anel e nome, nessa ordem, com 14px entre eles; termina a 90% da altura da tela'
    label: '{typography.aplicando-label}, {colors.text-primary}, brilho 0 0 12px rgba(63,208,255,.45)'
    spinner: '{components.spinner-dual-ring.size-applying}'
    name: '{typography.aplicando-name}, {colors.text-chip}'
    schematic: '{components.setup-schematic}, de 12% da altura até 10px acima do bloco'
    note: '{colors.text-overlay-note}'
  setup-schematic:
    size: 250x225px (viewBox 230x215)
    processor: '60x186, {colors.surface-1}, borda {colors.border-sheet}, raio 8; rótulo PRO 2.4BT embaixo em {typography.schematic-processor}, {colors.text-secondary}'
    port: '14x12, {colors.surface-2}, borda {colors.border-sheet}, raio 3; CH1 a CH4 de cima para baixo, 46 em 46'
    wire: 'curva da porta ao equipamento, {colors.border-sheet}, traço 1.4'
    device: '84x20, {colors.surface-1}, borda {colors.border-sheet}, raio 6; nome em {typography.schematic-label}, {colors.text-chip}'
    lit: 'porta, fio e borda do equipamento em {colors.brand}, brilho drop-shadow de 3 a 5px'
    done: 'porta, fio e borda em {colors.channel-done}, sem brilho'
    transition: 0.3s
  creation-stage:
    background: 'radial-gradient(ellipse 80% 40% at 50% 34%, {colors.overlay-glow} 0%, transparent 70%), {colors.bg}'
    text-top: 27% da altura
    gutter: '{spacing.stage-gutter}'
    horizon: '1px, linear-gradient(90deg, transparent, rgba(63,208,255,.35), transparent), brilho 0 0 14px 2px rgba(63,208,255,.18); 12px abaixo do texto'
    floor: 'linear-gradient(180deg, {colors.stage-floor}, {colors.bg} 60%), do horizonte ao pé'
    reflection: 'reflexo do texto 22px abaixo, linear-gradient(transparent 50%, rgba(255,255,255,.14))'
    floor-ring: '{components.spinner-dual-ring.size-floor}, topo a 58% da altura, girando sem parar'
    phrase: '{typography.creation-phrase}, {colors.text-primary}; palavra-chave em {colors.brand}'
    value: '{components.value-line}'
  value-line:
    type: '{typography.creation-value}'
    gap: 7px
    bar: '54x5px, {colors.surface-2}, raio 3; preenchimento linear-gradient(90deg, {colors.channel-done}, {colors.brand}), brilho 0 0 8px rgba(63,208,255,.45)'
    old: '{colors.text-secondary}, peso 500, risco de 1px'
    arrow: '{colors.text-nav-inactive}'
    new: '{colors.brand}, peso 800'
  neon-sphere:
    ring: 'r 44 (viewBox 100), traço 2.2, uma linha, sem preenchimento'
    cyan: '{colors.brand}, drop-shadow 0 0 2px rgba(63,208,255,.9) e 0 0 6px rgba(63,208,255,.5)'
    pink: '{colors.accent-pink}, drop-shadow 0 0 2px rgba(255,43,214,.9) e 0 0 6px rgba(255,43,214,.5)'
    cyan-spin: 'em pé, eixo vertical, 0→360°'
    pink-spin: 'deitado a 64°, eixo horizontal, 0→−360° (sentido contrário); encolhe até 77% ao fechar no plano'
    perspective: 900px
    opacity: '.35 → 1 (até 12%) → 1 (até 88%) → 0'
  flow-top-bar:
    min-height: 52px
    padding: '{spacing.flow-top}'
    buttons: '48x48px, ‹ à esquerda e ✕ à direita, 22px, {colors.text-chip}; escondidos quando não valem'
    title: '{typography.flow-title}, {colors.text-chip}, centralizado'
    subtitle: '{typography.flow-subtitle}, {colors.text-secondary}'
  question-answer:
    min-height: 56px
    background: '{colors.surface-1}'
    foreground: '{colors.text-primary}'
    radius: '{rounded.lg}'
    padding: '{spacing.answer}'
    type: '{typography.answer}'
    chevron: '›, 18px, {colors.text-nav-inactive}'
    selected: 'ciano a 12%, contorno interno 1.5px {colors.brand}, texto {colors.brand}'
    other: 'transparente, contorno interno 1px {colors.border-sheet}, texto {colors.text-secondary}'
    other-field: 'altura 56px, {rounded.lg}, borda 1.5px {colors.brand}, fundo {colors.bg}; botão OK ao lado em {colors.brand}, texto {colors.on-brand}, peso 800'
    gap: '{spacing.stack-question}'
  summary-list:
    background: '{colors.surface-1}'
    radius: '{rounded.lg}'
    row-padding: '{spacing.summary-row}'
    divider: '1px {colors.surface-2}'
    check: '20px, círculo {colors.connected}, ✓ em {colors.on-connected}, 12px/900'
    name: '{typography.summary-item}'
    value: '{typography.summary-item}, {colors.brand}'
    reason: '{typography.summary-reason}, {colors.text-secondary}'
    changed: 'fundo ciano a 6%'
  setting-switch:
    size: 48x28px
    radius: 14px
    off: 'trilho {colors.surface-2}, botão {colors.text-secondary}'
    on: 'trilho {colors.brand}, botão {colors.on-brand}'
    row: '{colors.surface-1}, {rounded.lg}, altura mínima 64px'
  toast:
    background: '{colors.surface-2}'
    radius: '{rounded.md}'
    padding: 12px 14px
    position: 16px das laterais, 76px acima do pé
    shadow: '0 10px 30px #000'
    duration: '{motion.toast}'
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
  button-flow-primary:
    min-height: 52px
    background: '{colors.brand}'
    foreground: '{colors.on-brand}'
    radius: '{rounded.lg}'
    type: '{typography.button-flow}'
  button-flow-secondary:
    min-height: 52px
    background: '{colors.surface-2}'
    foreground: '{colors.text-chip}'
    radius: '{rounded.lg}'
    type: '{typography.button-flow}, peso 700'
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
> - [mockups/prototipo.html](mockups/prototipo.html) — protótipo navegável (abertura → busca → conexão → Roda), anterior a 30/10: os traços da IA e o Aplicando antigo não valem.
> - [mockups/criacao-memoria-ia.html](mockups/criacao-memoria-ia.html) — **criar a memória com a IA** (rodada de 30/10): uma pergunta por tela, o palco com as frases, a passagem dos anéis de neon, o Aplicando novo com o esquema de ligação, o resumo, o "Como ficou?", o Salvar e as Configurações do app. É a referência mais atual para o Aplicando.
> - [mockups/logo-final.html](mockups/logo-final.html) — logo, ícone e barra do topo.
> - [mockups/abertura-escolhida.html](mockups/abertura-escolhida.html) — abertura (Neon + Glitch B, rosa vira ciano).
> - [mockups/conexao-radar.html](mockups/conexao-radar.html) — só as **telas 2 e 3** (procurar e listar; conectando).
> - [mockups/tela-inicial-roda-estados.html](mockups/tela-inicial-roda-estados.html) — a Roda nos estados A a F, anterior a 30/10: os traços da IA e o Aplicando antigo não valem.
> - [.working/spinner-opcoes-1.html](.working/spinner-opcoes-1.html) — histórico: só vale a **opção 8, Anel duplo**.
> - [.working/tela-inicial-opcoes-1.html](.working/tela-inicial-opcoes-1.html) — histórico: só vale a **opção 2, Roda**.
> - [reconcile-referencia-esfera-neon.md](reconcile-referencia-esfera-neon.md) — o que ficou da imagem de referência da esfera de neon.
>
> **Em conflito com qualquer mock, as spines vencem.**

Decisões marcadas *(delegada)* foram recomendadas pelo facilitador e aceitas porque o Denis delegou: "o resto siga escolhendo recomendado".

## Brand & Style

O BGS Sound é o controle de um equipamento de som. É usado no sofá, na sala escura, com o projetor ligado. A sensação pedida pelo Denis é **atual, imersiva e tecnológica**. O app é preto e quase sem cor. O ciano da logo aparece só no que está escolhido, tocando ou ativo. O resto é cinza.

O símbolo da marca é a **Onda**: uma onda que perde força dentro de um círculo. O rosa aparece só em três lugares: no glitch da abertura (que vira ciano), no spinner Anel duplo e no anel rosa da passagem até o Aplicando. O rosa lembra a abertura e não é cor de interface.

**Tema: só escuro, sem modo claro** *(delegada)*. O app é usado na sala escura.

**Criar o som é um momento de cinema** *(Denis, 30/10)*. Quando a memória é montada, a tela vira um palco escuro: cada ajuste passa como uma frase, com o valor embaixo, e o anel duplo gira no chão. O objetivo é uma experiência imersiva na criação do som.

**Nenhum traço da IA na interface** *(Denis, 30/10)*. A IA trabalha por baixo. Nenhuma tela mostra a palavra "IA", estado da IA, PARAR IA ou Pausar.

### Logo

Referência: [mockups/logo-final.html](mockups/logo-final.html).

- **Símbolo Onda** (viewBox 100×100):
  - círculo `cx 50, cy 50, r 42`, traço 3, sem preenchimento, branco a 35% de opacidade (preto a 35% no fundo claro);
  - onda `M14 50 Q20 20 27 50 Q34 78 41 50 Q48 30 55 50 Q62 66 69 50 Q75 41 81 50 Q84 54 87 50`, traço 6, `{colors.brand}`, pontas e junções redondas.
- **Nome Empilhado (formato 1):** **BGS** em `{typography.wordmark-bgs}` em cima e **SOUND** em `{typography.wordmark-sound}` embaixo, com `margin-top .4em`. Com o nome centralizado, SOUND compensa o espaçamento com `margin-right −.62em`.
- **Logo completa:** símbolo a 1,5em do tamanho do nome, à esquerda, com espaço de .42em. Na abertura, o símbolo fica em cima (96 a 120 px) e o nome centralizado embaixo (34 a 38 px), com 18 a 20 px entre os dois.
- **Logo de uma linha (barra do topo):** símbolo de 22 px, BGS em `{typography.appbar-bgs}` e SOUND em `{typography.appbar-sound}`, na mesma linha de base, com espaço de .4em.
- **Ícone do app:** tile `{colors.icon-tile}`, cantos `{rounded.icon-tile}` (versão redonda: `{rounded.circle}`), símbolo a 72% do tile com recuo de 14%. Tamanhos no mock: 128 (loja), 56 (tela inicial), 44 (grade de apps) e 24 (notificação).
- Histórico rejeitado: o símbolo Cone (laranja) e as outras 11 logos. Ver EXPERIENCE.md › Inspiration & Anti-patterns.

## Colors

O preto é a base. O ciano marca o que está escolhido. As outras cores são só sinal de estado.

- **Preto (`{colors.bg}` #000000):** fundo do app. Combina com a sala escura e o projetor.
- **Superfícies:** `{colors.surface-1}` #111114 nos blocos e nas respostas, `{colors.surface-2}` #1C1C20 nos chips e no player, `{colors.surface-sheet}` #141418 nas folhas. O mostrador da Roda usa `{colors.dial-face}` #07090B.
- **Ciano da marca (`{colors.brand}` #3FD0FF):** o selecionado, o que está tocando, a aba ativa, o arco da Roda, o preset no centro, os anéis da busca, o botão principal, a palavra-chave da frase e o valor novo na criação, e o canal que está sendo gravado no esquema. Texto sobre ciano: `{colors.on-brand}` #00131A (10,5:1).
- **Rosa (`{colors.accent-pink}` #FF2BD6):** **só** no glitch da abertura, no arco interno do Anel duplo e no anel rosa da passagem até o Aplicando. Nunca em texto, botão, estado ou fundo.
- **Ciano do glitch (`{colors.glitch-cyan}` #00F0FF):** **só** na camada de glitch da abertura. Vira `{colors.brand}` antes do fim.
- **Aviso (`{colors.warning}` #FFB020):** Setup em dúvida. Pinta o chip "Setup em dúvida", o arco e o "MUDO" da Roda, o título da folha e os marcadores "≠".
- **Conectado (`{colors.connected}` #34D399):** o ponto do chip do Setup com conexão e o círculo do ✓ no resumo da criação (✓ em `{colors.on-connected}` #00140C).
- **Luz fraca (`{colors.channel-done}` #16394A):** o canal já gravado e relido no esquema do Aplicando. É o mesmo hex de `{colors.brand-tint-border}`. Também é o começo do degradê da barrinha do valor.
- **Chão do palco (`{colors.stage-floor}` #03070A):** o começo do degradê do chão, abaixo do horizonte.
- **Cinzas de texto:** `{colors.text-primary}` #F2F2F4 (principal), `{colors.text-chip}` #CFCFD4 (chips, nome no Aplicando), `{colors.text-secondary}` #9A9AA2 (dicas, legendas, valor de antes), `{colors.text-nav-inactive}` #77777D (abas inativas, seta do valor) e `{colors.text-dial-neighbor}` #7C7C84 (memórias vizinhas na borda da Roda).
- **Tinta de ciano:** o cartão do aparelho escolhido vai de `{colors.device-selected-from}` a `{colors.brand-tint-to}`.

**Contraste no preto:** texto principal 18,8:1 · secundário 7,5:1 · aba inativa 4,7:1 · vizinha na Roda 4,8:1 · ciano 11,7:1 · aviso 11,5:1 · rosa 6,6:1.

Evitar: modo claro; rosa fora da abertura, do spinner e da passagem; ciano como enfeite; cores de estado além das listadas.

## Typography

**Nenhuma fonte de marca foi escolhida.** Os mocks usam a fonte do sistema (`system-ui`, a Roboto no Android). [ASSUMPTION] O app usa a fonte do sistema até o Denis escolher outra.

A única tipografia "de marca" é o nome da logo: BGS em peso 900 com espaçamento negativo; SOUND em peso 500, pequeno e bem espaçado.

| Papel | Token | Onde |
|---|---|---|
| Nome da memória no centro | `{typography.dial-name}` (26 px/900); `{typography.dial-name-long}` (19 px) com mais de 8 caracteres | Roda |
| Preset no centro | `{typography.dial-preset}` (11,5 px/600), em ciano | Roda: "Ação ›" |
| Memória no topo da borda | `{typography.dial-label-selected}` | Roda |
| Vizinhas na borda | `{typography.dial-label-neighbor}` | Roda |
| Linha de estado | `{typography.dial-status}` | Roda: "tocando · aplicada agora", "toque para aplicar" |
| Aplicando | `{typography.aplicando-label}` (17 px/800, CAIXA ALTA, .18em, com brilho ciano leve) + `{typography.aplicando-name}` (20 px/800) | Aplicando |
| Nome do aparelho | `{typography.connect-name}` | Conectando |
| Título de tela | `{typography.title}` | "Procurando aparelhos" |
| Título do fluxo | `{typography.flow-title}` + `{typography.flow-subtitle}` | "Nova memória / Casa 2.1" |
| Pergunta | `{typography.question}` (22 px/800) | "Como você prefere o grave?", "Como ficou?", "Pronto. Ouça um pouco." |
| Resposta | `{typography.answer}` (14 px/600) | Botões de resposta |
| Frase da criação | `{typography.creation-phrase}` (24 px/800) | "Definindo o corte das JBL" |
| Valor da criação | `{typography.creation-value}` (15 px/600, números tabulares) | "desligado → 80 Hz" |
| Item do resumo | `{typography.summary-item}` + `{typography.summary-reason}` | "Corte do Sub · LPF 80 Hz" |
| Esquema de ligação | `{typography.schematic-label}` / `{typography.schematic-processor}` | CH1 a CH4, equipamentos, PRO 2.4BT |
| Título de folha | `{typography.sheet-title}` | "Som mudo até você escolher" |
| Texto | `{typography.body}` | Folhas, avisos |
| Botão | `{typography.button}`; no fluxo da criação, `{typography.button-flow}` (14 px/800) | Todos os botões |
| Player | `{typography.player-title}` / `{typography.player-meta}` | Mini player |
| Chip / dica / aba | `{typography.chip}` / `{typography.hint}` / `{typography.nav}` | Chips, dicas, abas |

Números sempre com vírgula decimal e unidade (−1,5 dB, 2,1 ms) *(delegada)*.

[ASSUMPTION] Os tamanhos vêm de mocks com quadro de 284 px de largura (a criação, de 324 px) e valem como sp. Os menores (8,5 a 11 px) ainda precisam ser conferidos no aparelho, na sala escura.

## Layout & Spacing

- Só celular, em retrato. Margem lateral `{spacing.gutter}` (16 px). Conteúdo numa coluna só. As alturas em porcentagem contam a partir do topo da tela.
- **Aba Som, de cima para baixo:** barra do topo, a Roda centralizada (268 px), uma linha de dica, o mini player, o chip "+ Nova memória" e as abas fixas. Espaço entre blocos: `{spacing.stack-home}`. O preset em uso fica **dentro da Roda, abaixo do nome da memória** *(Denis)*.
- **Busca e conexão:** título, anéis (240 px), dica e lista de aparelhos, com espaço `{spacing.stack-connect}` e enchimento `{spacing.connect-body}`. Ao conectar, o anel duplo fica 70 px abaixo do topo do corpo, o nome logo abaixo e o Cancelar no pé.
- **Folhas** sobem do pé, na largura toda, por cima do conteúdo, com uma camada preta a 60% atrás.
- **Telas da criação** (perguntas, resumo, "Como ficou?", Salvar) *(Denis, 30/10)*: sem barra do topo da marca e sem abas. Em cima, a barra do fluxo (52 px) com ‹ e título; o ✕ aparece só nas perguntas e no "Como ficou?" (ver Barra do fluxo, em Components). A pergunta fica 22 px abaixo da barra, com 14 px até as respostas e `{spacing.stack-question}` entre elas. Os botões de ação ficam no pé.
- **Palco da criação:** tela toda. O texto fica a cerca de **27% da altura**. O horizonte fica 12 px abaixo do texto e acompanha a altura dele (uma ou duas linhas). O anel grande e apagado fica no chão, com o topo a 58% da altura.
- **Aplicando** cobre a tela toda, inclusive a barra do topo e as abas. O bloco (APLICANDO, anel, nome) fica **embaixo** e termina a 90% da altura. O esquema de ligação ocupa o alto, de 12% da altura até 10 px acima do bloco *(Denis, 30/10)*.

## Elevation & Depth

A profundidade vem do tom e do brilho, não de sombras.

- Camadas: preto, depois `surface-1`/`surface-2`, depois a folha `{colors.surface-sheet}`. Só a folha e o aviso rápido têm sombra.
- O **brilho ciano** marca o que está aceso: o arco da Roda (desfoque gaussiano de 3 px), os pontos dos aparelhos na busca, os anéis, o horizonte do palco, a barrinha do valor e o canal que está sendo gravado no esquema.
- **Palco da criação:** brilho ciano escuro no alto (`{colors.overlay-glow}`), um horizonte fino com brilho e um chão mais escuro, com o reflexo do texto. A profundidade é de cena, sem sombra.
- **Passagem:** a única cena em 3D do app. Os dois anéis de neon giram com perspectiva de 900 px.
- **Aplicando** é uma camada escura por cima de tudo. Na criação, é preto a 90% sobre o palco. Nos outros casos, é o degradê radial azul-escuro, com o fundo desfocado em 6 px.
- **Travado/apagado:** opacidade 0,35 e saturação 0,3. A Roda travada mostra um cadeado no centro.

## Shapes

- O círculo é a forma da marca: o símbolo, a Roda, o botão do centro, o spinner, os anéis da busca, os anéis de neon e o botão de play.
- Chips, abas, o Cancelar e o preset no centro da Roda são pílulas (`{rounded.full}`).
- Cartões e blocos têm cantos médios: botões de folha `{rounded.md}`; player, cartão de aparelho, respostas, lista do resumo e botões da criação `{rounded.lg}`; folha `{rounded.sheet}`, só nos cantos de cima.
- No esquema de ligação, o processador tem raio 8, os equipamentos raio 6 e as portas raio 3.
- A capa do player e o ícone do aparelho usam `{rounded.sm}`. O ícone do app usa `{rounded.icon-tile}`.

## Components

- **Barra do topo (`app-bar`)** — logo de uma linha à esquerda; à direita, o chip do Setup e a engrenagem (área de 44 × 44 px). O chip do Setup é uma pílula `{colors.surface-2}` com o nome e "›". Três aparências: conectado (ponto `{colors.connected}`), "Setup em dúvida" (texto `{colors.warning}`) e "Sem conexão" (texto `{colors.text-secondary}`).
- **Roda (`roda`)** ([estados A a F](mockups/tela-inicial-roda-estados.html); preset no centro no [protótipo](mockups/prototipo.html)) — mostrador de 268 px:
  - face `{colors.dial-face}` com raio 118, um anel interno fraco e marcas na borda a cada 10° (maiores a cada 30°);
  - os nomes das memórias correm pela borda, num arco de 230° em cima. A escolhida fica no topo, sob o arco de seleção (±22°, traço 4, com brilho). As vizinhas ficam a ±76°;
  - no centro, de cima para baixo: o símbolo (56 px), o nome da memória, o preset e a linha de estado;
  - **preset:** só o nome, sem a palavra "Preset" ("Ação ›"; sem preset, "Sem preset ›"), em `{typography.dial-preset}` ciano, sobre uma pílula de 22 px de altura em ciano a 10%. A área de toque tem 160 × 48 px (o piso de 48 dp), maior que a pílula. Com outra memória escolhida e ainda não aplicada, mostra o último preset dela, sem "›";
  - cor do arco e do topo: `{colors.brand}` tocando, branco escolhida e ainda não aplicada, `{colors.warning}` na dúvida, `{colors.disconnected-arc}` sem conexão;
  - travada: apagada, com cadeado.
- **Botão do centro (`roda-center-button`)** — aparece só quando a escolhida não é a que está tocando. Círculo de raio 74, preenchimento ciano a 6% e borda ciano de 2 px, pulsando (opacidade da borda 0,45↔1 em `{motion.center-button-pulse}`).
- **Spinner Anel duplo (`spinner-dual-ring`)** ([opção 8](.working/spinner-opcoes-1.html)) — dois arcos de traço 6, pontas redondas, sobre uma trilha branca a 8%:
  - ciano (r 42, ~30% do círculo), sentido horário, 1,1 s por volta;
  - rosa (r 34, ~23%), anti-horário, 1,5 s por volta, com mistura *screen*;
  - tamanhos: 120 px ao conectar, **30% da largura da tela no Aplicando** (era 130 px), 18 px dentro de botões;
  - **no chão do palco:** 56% da largura, traço 2,5, opacidade 0,35, girando durante toda a criação;
  - sem números. É o indicador de **todo** carregamento.
- **Anéis da busca (`search-rings`)** ([telas 2 e 3](mockups/conexao-radar.html)) — 4 anéis de 2 px em `{colors.brand}` saem da logo (44 px, no centro de uma área de 240 px). Cada um cresce de 18% a 100% e some, num ciclo de 3,2 s, com 0,8 s entre eles. O brilho aparece no começo do ciclo.
  - Cada aparelho encontrado vira um ponto ciano de 12 px, com pulso a cada 2,4 s e o nome ao lado.
  - O ponto de um aparelho que não é processador fica apagado (opacidade 0,4), sem brilho. O cartão dele, na lista, usa 0,45 (ver Cartão de aparelho).
  - **Só ciano.**
- **Cartão de aparelho (`device-card`)** — ícone em quadro de 30 px, nome em negrito, linha de contexto ("Taramps · Casa 2.1 da última vez") e "Conectar ›" em ciano à direita.
  - Processador: borda ciano e fundo em degradê.
  - Outro Bluetooth: opacidade 0,45 (`{components.device-card.not-processor-opacity}`), com "Não é um processador".
- **Mini player (`player-bar`)** — bloco `{colors.surface-2}` com capa de 36 px, título (uma linha, com reticências), linha "Playlist da FILME · 2 de 8" e botão de play redondo, branco, de 32 px.
- **Chips (`chip`)** — pílulas `{colors.surface-2}` com texto `{colors.text-chip}`. Na aba Som fica só "+ Nova memória" (altura mínima de 44 px no mock). O chip da IA saiu *(Denis, 30/10)*.
- **Abas (`bottom-nav`)** — **3 rótulos de texto: Som, Ajustes, Setup** *(Denis, 30/10)*, em fundo preto, com fio em cima. A ativa fica em ciano, com um traço de 20×3 px acima do rótulo. A aba IA saiu. [ASSUMPTION] Os mocks não têm ícones nas abas, e isso não foi decidido.
- **Folhas (`sheet`)** — sobem do pé, com fundo `{colors.surface-sheet}`, cantos de cima `{rounded.sheet}`, pegador de 36×4 px e espaço `{spacing.stack-sheet}`. Botões empilhados na largura toda: o primeiro em `button-primary` (ou `button-flow-primary` no fluxo da criação), os outros em `button-secondary`. A legenda final usa `{colors.text-secondary}`.
  - **Folha da dúvida:** título "🔇 Som mudo até você escolher" em `{colors.warning}`; a lista de diferenças usa marcadores "≠" em aviso.
  - **Cancelar a criação:** "Cancelar a nova memória?", texto e dois botões ("Cancelar e voltar", principal; "Continuar criando").
  - **Configurações do app:** título "Configurações do app", uma linha com a chave "Animação ao criar o som" (`setting-switch`) e "Fechar".
  - **Confirmação de risco na criação:** folha do próprio app, com Confirmar e Cancelar. [ASSUMPTION] O texto e o visual não foram desenhados.
- **Barra do fluxo (`flow-top-bar`)** — 52 px; ‹ à esquerda e ✕ à direita (alvos de 48 × 48 px); no meio, título e subtítulo. O ‹ só aparece da 2ª pergunta em diante. O ✕ aparece só nas perguntas e no "Como ficou?"; some no palco, no Aplicando, no resumo e no Salvar.
- **Resposta (`question-answer`)** — botão de 56 px de altura, `{colors.surface-1}`, `{rounded.lg}`, texto à esquerda e "›" à direita. Ao tocar, acende (ciano a 12%, contorno ciano). A última opção é sempre **"Outro…"**, com contorno e texto cinza; ela vira um campo ("Escreva do seu jeito") com o botão **OK** ao lado. Uma resposta já escrita em "Outro…" aparece no lugar dele.
- **Palco da criação (`creation-stage`)** — tela toda escura com brilho ciano no alto. O texto fica a cerca de 27% da altura. Logo abaixo, o horizonte fino com brilho, que se ajusta para ficar 12 px abaixo do texto (0,5 s). Abaixo do horizonte, o chão escuro, com o reflexo do texto. No chão, o anel duplo grande e apagado girando o tempo todo. Em cima, a barra do fluxo com o nome da memória e o Setup ("MUSICA ORIG / Casa 2.1"), **sem ‹ e sem ✕**. Quando o nome é pedido: ver EXPERIENCE.md › Questões em aberto, nº 12.
  - **Frase:** `{typography.creation-phrase}`, uma ação com verbo no gerúndio, com a palavra-chave em ciano ("Definindo o corte das **JBL**").
  - **Linha do valor (`value-line`):** logo abaixo da frase, numa linha só: barrinha do medidor (54 × 5 px), valor de antes riscado em cinza, seta e valor novo em ciano. **Só os valores**: sem "em todas" e sem os prefixos HPF, LPF e Sub ("desligado → 80 Hz") *(Denis; prefixos tirados pelo facilitador e aprovados no protótipo)*.
- **Esfera de neon (`neon-sphere`)** ([reconcile](reconcile-referencia-esfera-neon.md)) — dois anéis de neon de **1 linha** (traço 2,2, com brilho): um ciano (`{colors.brand}`) e um rosa (`{colors.accent-pink}`). Existem só durante a passagem até o Aplicando.
- **Aplicando (`applying-overlay`)** ([criação](mockups/criacao-memoria-ia.html)) — camada escura sobre o app *(Denis, 30/10; muda o padrão anterior, que tinha o anel no centro e o texto embaixo)*:
  - **embaixo**, um bloco que termina a 90% da altura, de cima para baixo: **"APLICANDO"** (ou "REAPLICANDO", ao reaplicar) em `{typography.aplicando-label}`, `{colors.text-primary}` com brilho ciano leve; o anel do Aplicando, a 30% da largura; o nome da memória em `{typography.aplicando-name}`, em `{colors.text-chip}`. 14 px entre as três partes;
  - **no alto**, o esquema de ligação do Setup em miniatura (`setup-schematic`);
  - nome do preset e nota "Mantenha o celular perto do processador": ver EXPERIENCE.md › Questões em aberto, nº 16.
- **Esquema de ligação (`setup-schematic`)** — miniatura de 250 × 225 px:
  - à esquerda, o PRO 2.4BT (60 × 186) com as portas CH1 a CH4; o rótulo "PRO 2.4BT" embaixo;
  - à direita, os equipamentos do Setup (84 × 20), um embaixo do outro, com o nome;
  - um fio curvo liga cada porta ao equipamento que ela alimenta. Um equipamento com dois canais (o Sub em CH3 e CH4) recebe dois fios;
  - **aceso:** porta, fio e borda do equipamento em ciano, com brilho. **Já gravado:** luz fraca em `{colors.channel-done}`. **Ainda não:** cinza `{colors.border-sheet}`. Troca em 0,3 s;
  - desenhado com os dados que o Setup já tem (equipamentos e canais). Serve para qualquer Setup: Casa 2.1, carro ou 5.1 *(Denis, 30/10)*.
- **Lista do resumo (`summary-list`)** — bloco `{colors.surface-1}` com uma linha por ajuste: ✓ verde, nome, valor em ciano à direita e o motivo embaixo, em cinza. No refino, o que mudou fica com fundo ciano a 6%.
- **Chave de configuração (`setting-switch`)** — 48 × 28 px. Desligada: trilho `{colors.surface-2}`. Ligada: trilho ciano. Fica numa linha `{colors.surface-1}` com título e explicação.
- **Aviso rápido (`toast`)** — bloco `{colors.surface-2}` 76 px acima do pé, por 2,6 s. Ex.: "Memória MUSICA ORIG criada no Casa 2.1."
- **Botões** — `button-primary` (ciano, texto escuro: "Escolher um Setup", "Procurar aparelhos"), `button-secondary` (branco a 7%) e `button-outline` (pílula com fio: "Cancelar"). No fluxo da criação, `button-flow-primary` (52 px, ciano: "Avaliar o som", "Salvar") e `button-flow-secondary` (52 px, `{colors.surface-2}`: "Ajustar mais"). Ao carregar, o botão mostra o spinner de 18 px antes do texto ("Aplicando FILME", "Salvando").
- **Componentes sem mock** — o Salvar fixo no pé de Ajustes, a linha de saída, o controle de parâmetro, a ficha de equipamento, o interruptor de chave de acesso, o selo de origem, o esquema isométrico, os medidores (voltímetro e clip), o gerador com contagem, o alerta "DESLIGUE O MÓDULO AGORA" em tela toda, a faixa de só leitura e os avisos (CAP-14). [ASSUMPTION] Eles usam os tokens acima. O visual de cada um está nas Questões em aberto do EXPERIENCE.md.

### Movimento

Referências: [mockups/abertura-escolhida.html](mockups/abertura-escolhida.html) (abertura), [mockups/prototipo.html](mockups/prototipo.html) (fim do carregamento) e [mockups/criacao-memoria-ia.html](mockups/criacao-memoria-ia.html) (criação do som).

- **Abertura (`{motion.opening-total}`, só a frio):**
  - `{motion.opening-flicker-ring}`: o círculo pisca e firma. As camadas ciano `{colors.glitch-cyan}` e rosa `{colors.accent-pink}` ficam a ±9 px, borradas a 1,2 px, com círculo de traço 4 e onda de traço 7, em mistura *screen*.
  - `{motion.opening-flicker-wave}`: a onda pisca e acende, com brilho ciano (`drop-shadow` de 6 e 16 px).
  - `{motion.opening-aura-close}`: as camadas se aproximam até ±3,5 px (aura a 90%). O brilho apaga em `{motion.opening-glow-off}`.
  - `{motion.opening-name}`: o nome sai do desfoque. Começa com `blur 10px`, escala 1,15 e opacidade 0, na curva `{motion.opening-name-ease}`. SOUND entra `{motion.opening-name-sound-delay}` depois de BGS.
  - `{motion.opening-pink-to-cyan}`: o rosa e o ciano do glitch passam a `{colors.brand}`.
  - `{motion.opening-aura-retract}`: a aura se recolhe para dentro do símbolo (deslocamento e desfoque a zero) e some.
  - No fim, a logo fica parada e nítida, igual à do app. O fade de 4,6 a 5 s do mock é só da demonstração.
  - Sem som. Um toque pula *(delegada)*.
- **Fim de todo carregamento** *(Denis)*:
  - o anel duplo fecha num anel ciano inteiro, com brilho, e o arco rosa some (`{motion.load-finish-spinner-close}`);
  - as telas trocam **só pela opacidade**: a de carregamento sai (`{motion.load-finish-screen-out}`) e a pronta entra (`{motion.load-finish-screen-in}`). Sem escala, para o cabeçalho não duplicar;
  - o movimento fica na Roda, que cresce (`{motion.load-finish-roda-in}`), e nos blocos (dica, player, chips), que sobem em sequência (`{motion.load-finish-stagger}`);
  - ao terminar de aplicar, o Aplicando sai do mesmo jeito e a Roda dá um brilho ciano rápido (`{motion.load-finish-applied-flash}`).
- **Perguntas da criação:** cada pergunta entra em `{motion.question-in}` (de 28 px à direita; ao voltar com ‹, de 28 px à esquerda). A resposta tocada acende (`{motion.answer-mark}`) e a próxima pergunta entra.
- **Palco da criação** *(Denis, 30/10)*:
  - o palco aparece e fica parado (`{motion.creation-start}`); o anel do chão já gira;
  - **Foco (entrada):** a frase parte da borda esquerda, já visível e desfocada, e anda até o centro (`{motion.phrase-in}`). O desfoque cai junto com o caminho e chega a zero quando ela para. Sem fade;
  - a linha do valor vem logo atrás, com o mesmo Foco, mostrando o valor de antes (`{motion.value-in}`);
  - ao parar: a barrinha anda da posição antiga à nova (`{motion.value-bar}`), o valor de antes é riscado (`{motion.value-strike}`), a seta entra (`{motion.value-arrow}`) e o valor novo acende, saindo do desfoque (`{motion.value-new}`);
  - a frase fica parada (`{motion.phrase-hold}`);
  - **Foco (saída):** a frase parte nítida do centro e segue até a borda direita, desfocando (`{motion.phrase-out}`). A linha do valor sai logo depois, do mesmo jeito (`{motion.value-out}`);
  - a próxima frase entra `{motion.phrase-gap}` depois. Sentido fixo: sempre da esquerda para a direita;
  - o horizonte acompanha a altura do texto (`{motion.horizon-follow}`).
- **Passagem até o Aplicando (Pião e cambalhota)** *(Denis, 30/10)* — `{motion.passage}`; a curva vale para a escala e a posição:
  - a camada preta cobre o palco (`{motion.passage-scrim}`);
  - o anel grande e apagado do chão se abre nos dois anéis de neon e sobe até o lugar do anel do Aplicando, encolhendo de 56% para 30% da largura;
  - o **ciano** fica em pé e gira no eixo vertical; o **rosa** fica deitado (64°) e gira no eixo horizontal, **no sentido contrário**. Uma volta só, com curva suave, pico de cerca de 350° por segundo (`{motion.passage-spin}`);
  - no fim, os dois se fecham no plano, e o anel do Aplicando acende no lugar deles (o traço vai de 2,5 a 6);
  - ainda durante a passagem, o esquema aparece (`{motion.aplicando-schematic-in}`); depois "APLICANDO" desce até o lugar e o nome sobe (`{motion.aplicando-text-in}`).
- **Aplicando da criação:** os canais acendem **um por um** (`{motion.aplicando-channel}`), enquanto cada um é gravado e relido; o que já foi gravado fica com a luz fraca. Com tudo gravado, a tela fica parada (`{motion.aplicando-final-hold}`) e sai só pela opacidade (`{motion.aplicando-out}`). Total: cerca de 5,7 s (1,6 s de passagem + 2,8 s de canais, 4 × 0,7 s + 1 s parado + 0,3 s de saída).
- **Resumo:** cada item sobe e aparece, um depois do outro (`{motion.summary-item-in}`).
- **A animação manda no tempo** *(Denis, 30/10)*: a escrita real roda junto, mas nenhuma frase sai antes do tempo dela, mesmo que o aparelho responda rápido.
- **Animação desligada** (chave "Animação ao criar o som", nas Configurações do app): sem frases e sem passagem. Entra só o Aplicando (camada em 0,3 s, bloco e esquema com os canais), pelo tempo real da operação, e depois o resumo *(facilitador; aprovado no protótipo 3)*. [ASSUMPTION] O mock ainda segura cada canal por no mínimo 0,7 s nesse modo; ver EXPERIENCE.md › Questões em aberto, nº 15.
- **Spinner:** `{motion.spinner-cyan}` e `{motion.spinner-pink}`, sem parar.
- **Busca:** `{motion.search-ring-cycle}`, com `{motion.search-ring-stagger}` entre os anéis. Pulso dos pontos: `{motion.search-blip-pulse}`.
- **Botão do centro da Roda:** pulso de `{motion.center-button-pulse}`.
- **Movimento reduzido** (configuração do Android):
  - a abertura vira um fade da logo (`{motion.reduced-motion-opening}`) *(delegada)*;
  - o fim do carregamento vira um fade, sem escala nem sequência (`{motion.reduced-motion-load-finish}`) *(Denis)*;
  - na criação (`{motion.reduced-motion-creation}`), as frases só trocam por fade, já com o valor novo; não há passagem, e o Aplicando entra num fade; os itens do resumo aparecem sem movimento *(facilitador; aprovado no protótipo 3)*;
  - os anéis da busca e o anel duplo giram mais devagar, sem pulsos *(delegada)*. No mock, o anel duplo vai a 2,2 s e 3 s por volta;
  - nada pisca, nem o neon da abertura nem a borda do botão do centro *(delegada)*;
  - [ASSUMPTION] "Mais devagar" não tem valor definido para os anéis da busca.

## Do's and Don'ts

| Fazer | Não fazer |
|---|---|
| Fundo preto em todas as telas; só tema escuro | Modo claro ou fundos claros dentro do app |
| Ciano só para escolhido, tocando, ativo e ação principal | Ciano como enfeite ou em várias coisas que disputam a atenção |
| Rosa só no glitch da abertura, no Anel duplo e no anel rosa da passagem | Rosa em botão, texto, estado, anel da busca ou fundo |
| Anel duplo em todo carregamento, sem números | Contador de leituras (1 a X), barra de progresso com número ou porcentagem |
| Anéis ciano saindo da logo ao procurar | Radar de feixe girando com rastro; anéis ciano e rosa alternados |
| Preset no centro da Roda, só com o nome ("Ação ›") | Chip de preset embaixo da Roda; a palavra "Preset" no rótulo |
| Fim do carregamento: telas trocando só pela opacidade; movimento na Roda e nos blocos | Escala na tela inteira (o cabeçalho duplica) |
| Aviso (#FFB020) só para Setup em dúvida | Aviso como enfeite; vermelho em qualquer lugar |
| Nenhum traço da IA: sem a palavra "IA", sem estado da IA, sem PARAR IA, sem Pausar | Chip, faixa ou aba da IA; "a IA está ajustando" |
| Na criação, uma frase por vez com verbo no gerúndio e, na linha do valor, só os valores | Várias frases juntas; "em todas", "HPF", "LPF" ou "Sub" repetindo a frase na linha do valor |
| Frase com o Foco: entra pela esquerda e sai pela direita, sempre no mesmo sentido | Alternar o sentido; fade, subida ou outras transições da frase |
| Aplicando com APLICANDO em cima do anel, o nome embaixo e o esquema no alto | Anel no centro com o texto embaixo (padrão antigo) |
| Esquema de ligação feito só com os dados do Setup | Planta da sala ou do carro com "você" (exige dado que o Setup não tem) |
| Anéis de neon de 1 linha, um de cada cor, uma volta suave | Tubo de 2 linhas, 3 anéis de cada cor, neon forte, setas de giro, duas voltas rápidas |
| Apagar e travar (opacidade 0,35 e cadeado) o que não pode ser usado | Esconder a Roda travada ou mostrar um valor que o app não sabe |
| Abertura terminando exatamente na logo parada e nítida | Aura ou borrão sobrando depois da abertura |
| Logo de uma linha na barra do topo; Empilhada na abertura | Esticar, recolorir ou separar a onda do círculo |
