---
name: BGS Controle
description: App Android de controle do PRO 2.4BT + AUDIO (POC 1). Preto de estúdio, uma luz por saída, branco para o que está escolhido e uma só capa de fader. Família visual do BGS Sound Lab; o resto é próprio.
status: final
updated: 2026-10-21
sources:
  - ../ux-taramps-automacao-2026-10-16/DESIGN.md
colors:
  # Fundos e superfícies (núcleo da família BGS Sound Lab)
  palco: '#000000'            # fundo do app
  sup-1: '#1C1C1E'            # cartões, canais, blocos da grade, folha de confirmação
  sup-2: '#2C2C2E'            # trilho do fader, contornos, botões − e +, ícone da função
  sup-3: '#232325'            # divisórias e linhas finas
  lista-aberta: '#151517'     # fundo de lista recolhida aberta e do Avançado aberto
  registro-fundo: '#0E0E10'   # fundo da lista "O que a IA fez" e do Histórico
  # Texto
  texto: '#F5F5F7'            # texto principal e fundo do que está escolhido
  apoio: '#A1A1A6'            # texto de apoio
  apagado: '#85858A'          # rótulos, escalas, ponto "desconectado"
  sobre-escolhido: '#000000'  # texto sobre o branco do escolhido
  sobre-escolhido-apoio: '#55555A'  # rótulo pequeno sobre o branco (ex.: MEMÓRIA 1 na aba ativa)
  nota: '#8E8E93'             # pílula neutra (memória com a IA)
  # Uma luz por saída
  saida-l: '#7EAEFF'          # JBL L, azul
  saida-r: '#C495FF'          # JBL R, lilás
  saida-sub: '#FFCD94'        # Sub, âmbar
  saida-sub-fundo: '#241D13'  # fundo do campo do Sub enquanto se escreve
  # IA
  ia: '#C495FF'               # IA (mesmo valor de saida-r, por decisão)
  ia-fundo: '#1B1526'         # fundo da faixa da IA e da banda que a IA está mexendo
  ia-texto: '#D6C6F5'         # texto da faixa da IA
  ia-halo: '#C495FF40'        # anel do pulso da IA (CSS #RRGGBBAA; no MAUI #40C495FF)
  # Estado
  ok: '#30D158'               # conectado
  parar: '#FF453A'            # PARAR IA, erro de senha, aviso de risco
  erro-texto: '#FF8A82'       # texto de erro e risco das chaves desligadas
  risco-fundo: '#2A1414'      # fundo do aviso de risco
  risco-borda: '#FF453A73'    # contorno do aviso de risco (CSS #RRGGBBAA; no MAUI #73FF453A)
  veu: '#000000A8'            # véu atrás da folha de confirmação (CSS #RRGGBBAA; no MAUI #A8000000)
  # Capa do fader e campo
  capa-a: '#3C3C41'           # capa, topo do degradê
  capa-b: '#26262A'           # capa, base do degradê
  capa-borda: '#4C4C52'       # contorno da capa e do disco do giratório
  campo: '#0B0B0C'            # fundo do campo numérico
  campo-borda: '#3A3A3D'      # contorno do campo, dos − e + e do botão secundário
  # Teclado do Android (só nos mocks; o real é o do sistema)
  tec-fundo: '#1F1F22'
  tec-tecla: '#36363B'
  tec-funcao: '#2A2A2E'
typography:
  # Família: Roboto variável embutida no app (eixo de peso completo, com 200 e 800 de verdade).
  # Os mocks usam a pilha do BGS Sound Lab ("Segoe UI Variable Display" … Roboto), que no celular cai em Roboto.
  # Números sempre tabulares. Tamanhos em px do quadro de 390 px = unidades independentes do MAUI,
  # multiplicadas pela escala de fonte do Android. Nenhum texto abaixo de 11 px.
  marca:
    fontFamily: Roboto
    fontSize: 22px
    fontWeight: '200'
    letterSpacing: -0.01em
  marca-forte:
    fontFamily: Roboto
    fontSize: 22px
    fontWeight: '600'
    letterSpacing: 0.02em
  memoria-hero:
    fontFamily: Roboto
    fontSize: 60px
    fontWeight: '200'
    lineHeight: '1'
    letterSpacing: -0.03em
  estado-hero:
    fontFamily: Roboto
    fontSize: 44px
    fontWeight: '200'
    lineHeight: '1.2'
    letterSpacing: -0.03em
  memoria-lista:
    fontFamily: Roboto
    fontSize: 36px
    fontWeight: '200'
    lineHeight: '1.2'
    letterSpacing: -0.03em
  leitura-ganho:
    fontFamily: Roboto
    fontSize: 32px
    fontWeight: '200'
    lineHeight: '1.1'
    letterSpacing: -0.02em
  valor-giratorio:
    fontFamily: Roboto
    fontSize: 44px
    fontWeight: '800'
    lineHeight: '1.05'
    letterSpacing: -0.02em
  valor-giratorio-campo:
    fontFamily: Roboto
    fontSize: 34px
    fontWeight: '800'
  valor-canal:
    fontFamily: Roboto
    fontSize: 24px
    fontWeight: '800'
    letterSpacing: -0.01em
  valor-campo:
    fontFamily: Roboto
    fontSize: 22px
    fontWeight: '800'
    letterSpacing: -0.01em
  valor-campo-linha:
    fontFamily: Roboto
    fontSize: 17px
    fontWeight: '700'
  titulo-saida:
    fontFamily: Roboto
    fontSize: 30px
    fontWeight: '800'
    lineHeight: '1'
    letterSpacing: -0.01em
  titulo-tela:
    fontFamily: Roboto
    fontSize: 28px
    fontWeight: '800'
    lineHeight: '1.2'
    letterSpacing: -0.01em
  titulo-folha:
    fontFamily: Roboto
    fontSize: 22px
    fontWeight: '800'
    lineHeight: '1.2'
    letterSpacing: -0.01em
  titulo-faixa-ia:            # título da faixa da IA ("IA ajustando o EQ da FILME")
    fontFamily: Roboto
    fontSize: 17px
    fontWeight: '800'
  nome-saida:
    fontFamily: Roboto
    fontSize: 16px
    fontWeight: '600'
  ajustar-saida:              # "Ajustar ›" do cartão de saída, na cor da saída
    fontFamily: Roboto
    fontSize: 13px
    fontWeight: '600'
  nome-canal:
    fontFamily: Roboto
    fontSize: 15px
    fontWeight: '800'
    letterSpacing: 0.04em
  titulo-parametro:
    fontFamily: Roboto
    fontSize: 15px
    fontWeight: '700'
  linha-titulo:
    fontFamily: Roboto
    fontSize: 15px
    fontWeight: '600'
  botao-principal:
    fontFamily: Roboto
    fontSize: 16px
    fontWeight: '800'
  botao-secundario:
    fontFamily: Roboto
    fontSize: 15px
    fontWeight: '700'
  botao-pequeno:
    fontFamily: Roboto
    fontSize: 13px
    fontWeight: '800'
  botao-parar:
    fontFamily: Roboto
    fontSize: 20px
    fontWeight: '800'
    letterSpacing: 0.04em
  passo-grande:
    fontFamily: Roboto
    fontSize: 36px
    fontWeight: '500'
  passo:
    fontFamily: Roboto
    fontSize: 26px
    fontWeight: '500'
  nav:
    fontFamily: Roboto
    fontSize: 14px
    fontWeight: '600'
  texto-risco:                # texto do aviso de risco
    fontFamily: Roboto
    fontSize: 15px
    fontWeight: '700'
  texto-erro:                 # texto do aviso de erro
    fontFamily: Roboto
    fontSize: 14px
    fontWeight: '600'
  corpo:
    fontFamily: Roboto
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.5'
  apoio:
    fontFamily: Roboto
    fontSize: 13px
    fontWeight: '400'
    lineHeight: '1.45'
  detalhe:
    fontFamily: Roboto
    fontSize: 12px
    fontWeight: '400'
    lineHeight: '1.5'
  risco-chave:                # risco escrito na linha de chave
    fontFamily: Roboto
    fontSize: 11px
    fontWeight: '400'
  rotulo:
    fontFamily: Roboto
    fontSize: 11px
    fontWeight: '600'
    letterSpacing: 0.12em
  escala:
    fontFamily: Roboto
    fontSize: 11px
    fontWeight: '600'
  escala-eq:
    fontFamily: Roboto
    fontSize: 11px
    fontWeight: '600'
    letterSpacing: -0.02em
rounded:
  xs: 6px        # selo "ilustrativo", amostras
  capa: 9px      # capa do fader
  sm: 10px       # botões da grade de tipos de filtro, teclas
  pilula-sel: 11px  # pílula do escolhido dentro da lista recolhida
  md: 12px       # campo, botões de memória, Lig./Desl., OFF
  lg: 14px       # − e + dos parâmetros, lista recolhida, botões da fase, engrenagem
  xl: 16px       # ‹ voltar, abas de saída
  card: 18px     # cartão de saída, controle de parâmetro, bloco de função, grupo, painel, Salvar, folha (botões)
  card-lg: 20px  # cartão da memória em uso, cartão de memória, cartão de conexão
  painel: 22px   # canal da mesa, MASTER, faixa da IA, − e + do ganho
  folha: 26px    # cantos de cima da folha de confirmação
  full: 9999px   # barra inferior, pílula de status, chips
spacing:
  '1': 4px
  '2': 6px
  '3': 8px
  '4': 10px
  '5': 12px
  '6': 14px
  '7': 16px
  '8': 18px
  '9': 20px
  margem: 16px          # margem lateral de toda tela
  gap-tela: 12px        # entre blocos de uma tela
  gap-cartoes: 10px     # entre cartões de saída e controles
  gap-grade: 8px        # grade Áudio, abas, canais, botões lado a lado
  alvo-min: 44px        # menor alvo de toque
  alvo-passo: 48px      # − e + dos parâmetros, ‹ voltar
  alvo-lista: 52px      # lista recolhida, linha de configuração, botões da fase
  alvo-principal: 56px  # Salvar, Conectar, confirmar
  alvo-parar: 60px      # PARAR IA
  alvo-passo-grande: 72px  # − e + do ganho
  barra-inferior: 72px
  cabecalho-tela: 48px
  topo-app: 44px
components:
  # Navegação e topo
  barra-inferior:
    height: '{spacing.barra-inferior}'
    background: '{colors.palco}'
    divider: '{colors.sup-3}'
    item-ativo-background: '{colors.texto}'
    item-ativo-foreground: '{colors.sobre-escolhido}'
    item-foreground: '{colors.apoio}'
    radius: '{rounded.full}'
    typography: '{typography.nav}'
  topo-do-app:
    height: '{spacing.topo-app}'
    marca: '{typography.marca}'
    marca-forte: '{typography.marca-forte}'
  pilula-status:
    background: '{colors.sup-1}'
    border: '{colors.sup-2}'
    foreground: '{colors.apoio}'
    ponto-conectado: '{colors.ok}'
    ponto-desconectado: '{colors.apagado}'
    ponto-esperando: '{colors.apoio}'
    radius: '{rounded.full}'
    typography: '{typography.detalhe}'
  engrenagem:
    size: '{spacing.alvo-min}'
    background: '{colors.sup-1}'
    radius: '{rounded.lg}'
  cabecalho-tela-cheia:
    height: '{spacing.cabecalho-tela}'
    voltar-size: '{spacing.alvo-passo}'
    voltar-background: '{colors.sup-1}'
    voltar-radius: '{rounded.xl}'
    titulo: '{typography.titulo-tela}'
    titulo-saida: '{typography.titulo-saida}'
    subtitulo: '{typography.detalhe}'
  # Início
  cartao-memoria-em-uso:
    background: '{colors.sup-1}'
    radius: '{rounded.card-lg}'
    nome: '{typography.memoria-hero}'
    arco: 'conic {colors.saida-l} → {colors.saida-r} → {colors.saida-sub}, anel fino 180 px no canto superior direito, opacidade 0,85'
  botoes-de-memoria:
    height: '{spacing.alvo-min}'
    radius: '{rounded.md}'
    border: '{colors.sup-2}'
    ativo-background: '{colors.texto}'
    ativo-foreground: '{colors.sobre-escolhido}'
  cartao-saida:
    background: '{colors.sup-1}'
    radius: '{rounded.card}'
    barra-lateral: '4px na cor da saída'
    ganho: '{typography.leitura-ganho}'
    nome: '{typography.nome-saida}'
    ajustar: '{typography.ajustar-saida} na cor da saída'
  # Mesa
  abas-de-memoria:
    height: 60px
    radius: '{rounded.card}'
    background: '{colors.sup-1}'
    ativa-background: '{colors.texto}'
    ativa-foreground: '{colors.sobre-escolhido}'
  canal-da-mesa:
    background: '{colors.sup-1}'
    radius: '{rounded.painel}'
    borda-superior: '4px na cor da saída'
    nome: '{typography.nome-canal}'
    valor: '{typography.valor-canal}'
    fader-largura: 56px
    trilho: '8px, {colors.sup-2}'
    ajustar-height: '{spacing.alvo-min}'
  master:
    height: 76px
    background: '{colors.sup-1}'
    radius: '{rounded.painel}'
    cor: '{colors.texto}'
  faixa-de-monitoracao:
    height: '{spacing.alvo-min}'
    background: '{colors.sup-1}'
    radius: '{rounded.lg}'
    voltimetro: '{typography.valor-campo-linha}'
    clip-apagado: '{colors.sup-2}'
    clip-aceso: '{colors.parar}'
  chips-da-mesa:
    height: 38px
    border: '{colors.sup-2}'
    radius: '{rounded.full}'
  # Controles de valor
  capa-do-fader:
    background: 'degradê {colors.capa-a} → {colors.capa-b}'
    border: '{colors.capa-borda}'
    radius: '{rounded.capa}'
    vertical: 56 × 30 px
    horizontal: 30 × 44 px
    risco: '2px na cor da saída'
  botao-giratorio:
    size: 226px
    arco: '16px na cor da saída sobre {colors.sup-2}, 270° de curso'
    disco-background: 'radial {colors.capa-a} → {colors.sup-1}'
    disco-border: '{colors.capa-borda}'
    valor: '{typography.valor-giratorio}'
    valor-campo: '{typography.valor-giratorio-campo}'
    ponto: '10px {colors.texto} com brilho na cor da saída'
  botoes-mais-menos:
    passo-size: '{spacing.alvo-passo}'
    passo-radius: '{rounded.lg}'
    passo-typography: '{typography.passo}'
    passo-grande-size: '{spacing.alvo-passo-grande}'
    passo-grande-radius: '{rounded.painel}'
    passo-grande-typography: '{typography.passo-grande}'
    background: '{colors.sup-2}'
    border: '{colors.campo-borda}'
  campo-numerico:
    height: '{spacing.alvo-min}'
    min-width: 112px
    background: '{colors.campo}'
    border: '{colors.campo-borda}'
    sublinhado: '2px na cor da saída'
    radius: '{rounded.md}'
    valor: '{typography.valor-campo}'
    ativo-border: '2px na cor da saída'
    ativo-background-sub: '{colors.saida-sub-fundo}'
  teclado:
    nota: 'só nos mocks; o real é o teclado do sistema'
    background: '{colors.tec-fundo}'
    tecla: '{colors.tec-tecla}'
    tecla-funcao: '{colors.tec-funcao}'
    radius: '{rounded.sm}'
  controle-de-parametro:
    background: '{colors.sup-1}'
    radius: '{rounded.card}'
    titulo: '{typography.titulo-parametro}'
    faixa: '{typography.rotulo}'
    trilho: '8px, {colors.sup-2}'
    escala: '{typography.escala}'
  lista-recolhida:
    min-height: '{spacing.alvo-lista}'
    radius: '{rounded.lg}'
    border: '{colors.sup-2}'
    aberta-background: '{colors.lista-aberta}'
    aberta-border: '{colors.campo-borda}'
    escolhido-background: '{colors.texto}'
    escolhido-radius: '{rounded.pilula-sel}'
    opcao-radius: '{rounded.sm}'
  avancado:
    background: '{colors.lista-aberta}'
    border: '{colors.campo-borda}'
    radius: '{rounded.card}'
  par-de-botoes:
    height: '{spacing.alvo-min}'
    height-fase: '{spacing.alvo-lista}'
    radius: '{rounded.md}'
    border: '{colors.sup-2}'
    escolhido-background: '{colors.texto}'
    escolhido-foreground: '{colors.sobre-escolhido}'
    typography: '{typography.botao-pequeno}'
  botao-off:
    height: '{spacing.alvo-min}'
    radius: '{rounded.md}'
    ligado-background: '{colors.texto}'
    typography: '{typography.botao-pequeno}'
  # Botões e folhas
  botao-principal:
    height: '{spacing.alvo-principal}'
    background: '{colors.texto}'
    foreground: '{colors.sobre-escolhido}'
    radius: '{rounded.card}'
    typography: '{typography.botao-principal}'
  botao-secundario:
    height: '{spacing.alvo-lista}'
    border: '{colors.campo-borda}'
    foreground: '{colors.texto}'
    radius: '{rounded.card}'
    typography: '{typography.botao-secundario}'
    pequeno-height: '{spacing.alvo-min}'
    pequeno-background: '{colors.sup-2}'
    pequeno-typography: '{typography.botao-pequeno}'
  folha-de-confirmacao:
    background: '{colors.sup-1}'
    radius-topo: '{rounded.folha}'
    veu: '{colors.veu}'
    titulo: '{typography.titulo-folha}'
    texto: '{typography.corpo}'
  # IA
  faixa-da-ia:
    background: '{colors.ia-fundo}'
    border: '2px {colors.ia}'
    radius: '{rounded.painel}'
    titulo: '{typography.titulo-faixa-ia}'
    texto: '{colors.ia-texto}'
  botao-parar-ia:
    height: '{spacing.alvo-parar}'
    background: '{colors.parar}'
    foreground: '#FFFFFF'
    radius: '{rounded.card}'
    typography: '{typography.botao-parar}'
  painel-ao-vivo:
    background: '{colors.sup-1}'
    radius: '{rounded.card}'
    selo-ao-vivo: '{colors.ia}'
    item-atual-background: '{colors.ia-fundo}'
    item-atual: '{colors.ia}'
    travado-opacidade: 0.42
  registro:
    background: '{colors.registro-fundo}'
    border: '{colors.sup-3}'
    radius: '{rounded.card}'
    item-atual-hora: '{colors.ia}'
  # Áudio e configurações
  abas-de-saida:
    height: 56px
    radius: '{rounded.xl}'
    background: '{colors.sup-1}'
    borda-superior: '4px na cor da saída'
    ativa-background: '{colors.texto}'
  bloco-de-funcao:
    background: '{colors.sup-1}'
    radius: '{rounded.card}'
    icone: '40 × 40 px, {colors.sup-2}, {rounded.md}'
    titulo: '{typography.titulo-parametro}'
  linha-de-configuracao:
    min-height: '{spacing.alvo-lista}'
    grupo-background: '{colors.sup-1}'
    grupo-radius: '{rounded.card}'
    divisoria: '{colors.sup-3}'
  linha-de-chave:
    min-height: '{spacing.alvo-lista}'
    risco: '{typography.risco-chave}'
    risco-chave-desligada: '{colors.erro-texto}'
    risco-outras: '{colors.apoio}'
  # Memórias e conexão
  cartao-de-memoria:
    background: '{colors.sup-1}'
    radius: '{rounded.card-lg}'
    nome: '{typography.memoria-lista}'
    selo-em-uso: '{colors.texto} com texto {colors.sobre-escolhido}, {rounded.full}'
  selo-herdado:
    border: '{colors.campo-borda}'
    foreground: '{colors.apoio}'
    radius: '{rounded.full}'
    typography: '{typography.rotulo}'
  cartao-de-conexao:
    background: '{colors.sup-1}'
    radius: '{rounded.card-lg}'
    estado: '{typography.estado-hero}'
    saida-vazia-barra: '4px na cor da saída a 40 %'
  caixas-da-senha:
    size: '64px de altura, 4 caixas'
    background: '{colors.campo}'
    radius: '{rounded.lg}'
    atual-border: '2px {colors.texto}'
  # Player
  linha-da-playlist:
    min-height: '{spacing.alvo-lista}'
    background: '{colors.sup-1}'
    divisoria: '{colors.sup-3}'
    titulo: '{typography.linha-titulo}'
    detalhe: '{typography.detalhe}'
    tocando-marca: '4px {colors.texto} na borda esquerda'
    sumiu-foreground: '{colors.apagado}'
  controles-do-player:
    tocar-size: '{spacing.alvo-parar}'
    tocar-background: '{colors.texto}'
    tocar-foreground: '{colors.sobre-escolhido}'
    proxima-size: '{spacing.alvo-passo}'
    proxima-background: '{colors.sup-2}'
    radius: '{rounded.full}'
  # Avisos
  aviso-de-risco:
    background: '{colors.risco-fundo}'
    border: '{colors.risco-borda}'
    radius: '{rounded.lg}'
    icone: '{colors.parar}'
    texto: '{typography.texto-risco}'
  aviso-de-erro:
    ponto: '8px {colors.parar}'
    foreground: '{colors.erro-texto}'
    typography: '{typography.texto-erro}'
  lembrete:
    ponto: '8px na cor da saída (ou {colors.apoio})'
    typography: '{typography.detalhe}'
  faixa-desligue-o-modulo:
    background: '{colors.parar}'
    foreground: '#FFFFFF'
    typography: '{typography.botao-parar}'
---

## Brand & Style

O BGS Controle é a tela do Denis para o processador PRO 2.4BT + AUDIO: um app de celular que substitui o app da Taramps e abre uma porta para a IA. Ele é andaime do BGS Sound Lab e herda dele **só a família visual** ([`../ux-taramps-automacao-2026-10-16/DESIGN.md`](../ux-taramps-automacao-2026-10-16/DESIGN.md)): o preto de palco ({colors.palco}), as superfícies grafite ({colors.sup-1}), os cinzas de apoio, as três luzes (fria, lilás, quente) e o vermelho de recusa. A usabilidade e a acessibilidade da Vitrine e dos Bastidores **não** são herdadas; o app é um produto novo.

A postura junta duas direções que o Denis escolheu: a calma do **Estúdio Noturno** para ler de longe (pouca coisa na tela, nomes grandes e finos) e a pegada do **Console Tátil** para regular (faders com capa, botão giratório, − e + grandes, identidade marcante). O resultado é a **síntese v2** ([`mockups/direction-sintese-v2.html`](mockups/direction-sintese-v2.html)) e as **telas-chave** ([`mockups/key-screens.html`](mockups/key-screens.html)).

Seis regras unem tudo:

1. **Fino é para ler, negrito é para mexer.** Nomes e leituras em peso 200; campos, botões e valores mexíveis em 700–800.
2. **Branco é escolha feita:** memória em uso, aba, tipo de filtro, fase, OFF do limitador, Lig./Desl.
3. **A cor da saída é o que está sendo mexido:** nível, risco da capa, sublinhado do campo, arco do giratório.
4. **Lilás é também a cor da IA.**
5. **Uma só capa de fader** (grafite neutro) no vertical, no horizontal e no giratório.
6. **Campo é uma caixa escura com sublinhado na cor da saída:** "aqui dá para escrever".

### De onde veio cada peça

| Recorte | Origem | O que ficou |
|---|---|---|
| [`imports/escolha-1-mesa-faders.png`](imports/escolha-1-mesa-faders.png) | Console Tátil | Abas de memória, canal da mesa, capa do fader, MASTER e chips da Mesa. |
| [`imports/escolha-2-inicio-noturno.png`](imports/escolha-2-inicio-noturno.png) | Híbrido Noturno | Topo do app, pílula de status, cartão da memória em uso, botões de memória e cartão de saída. |
| [`imports/escolha-3-faixa-ia.png`](imports/escolha-3-faixa-ia.png) | Console Tátil | Faixa da IA e botão PARAR IA. |
| [`imports/escolha-4-canais-travados.png`](imports/escolha-4-canais-travados.png) | Console Tátil | Painel ao vivo (controles esmaecidos e travados). |
| [`imports/escolha-5-ia-historico.png`](imports/escolha-5-ia-historico.png) | Estúdio Noturno | Registro ("O que a IA fez"). |
| [`imports/escolha-6-botao-giratorio.png`](imports/escolha-6-botao-giratorio.png) | Console Tátil | Botão giratório, botões − e + do ganho e cabeçalho de tela cheia do Ajuste da saída. |
| [`imports/escolha-7-tipo-do-filtro.png`](imports/escolha-7-tipo-do-filtro.png) | Síntese v1 | Lista recolhida (a grade de tipos de filtro, **mas recolhida**). |
| [`imports/referencia-taramps-audio.png`](imports/referencia-taramps-audio.png) | App da Taramps (aba Áudio) | Bloco de função (grade 2 × 5) e engrenagem. O "Modo Demo" **não** vem. |

Direções descartadas (só contexto, em `.working/`): Bancada Técnica e Papel Calmo (não escolhidas), os três híbridos e a síntese v1 (superadas pela v2).

### Diferenças em relação aos mocks

Este documento e o EXPERIENCE.md ganham de qualquer mock em caso de conflito. Conflitos já conhecidos:

- A barra inferior tem quatro pílulas (Início · Mesa · Áudio · Player), não duas nem três.
- {colors.apagado} mudou (ver Colors).
- Nenhum texto fica abaixo de 11 px; os mocks têm escalas de 10 px e frequências do EQ de 9 px.
- A Mesa ganhou a faixa de monitoração.
- No Ajuste da saída, atraso, fase e EQ de saída descem para Avançado.
- A chave da IA se chama "IA pela rede"; o mock de Chaves diz "Porta de comandos da IA".

Telas sem mock, montadas só com as peças de Components:

- Player
- Contador do gerador (30 s)
- Faixa "DESLIGUE O MÓDULO AGORA"
- Folha "aplicado pela metade"
- Aviso de modo só leitura
- Configuração e pareamento da IA pela rede
- Histórico
- Presets
- Voltímetro e clip
- Telas de função além do Crossover

## Colors

Tema único, escuro. [ASSUMPTION] O app não muda com o tema claro do Android; todas as direções avaliadas eram escuras e nenhuma pediu versão clara.

- **Palco ({colors.palco})** é o fundo de toda tela. Nada de degradê no fundo.
- **Superfícies ({colors.sup-1}, {colors.sup-2}, {colors.sup-3})** fazem a hierarquia por tom: cartão sobre o palco, trilho e botões sobre o cartão, divisória fina entre linhas. {colors.lista-aberta} marca o que está aberto (lista recolhida aberta, Avançado aberto). {colors.registro-fundo} é o fundo do registro.
- **Texto ({colors.texto})** é também a cor do escolhido: a pílula branca com texto preto. **Apoio ({colors.apoio})** para frases de apoio. **Apagado ({colors.apagado})** para rótulos em caixa alta e escalas.
- **Luz por saída:** **JBL L azul ({colors.saida-l})**, **JBL R lilás ({colors.saida-r})**, **Sub âmbar ({colors.saida-sub})**. São versões das luzes fria, lilás e quente do BGS Sound Lab. Aparecem só onde aquela saída está sendo lida ou mexida: borda do canal, barra lateral do cartão, nível do fader, risco da capa, sublinhado do campo, arco do giratório, título da saída e números do resumo na grade. Nunca pintam o fundo de um bloco inteiro.
- **IA lilás ({colors.ia})** com fundo {colors.ia-fundo} e texto {colors.ia-texto}. É o mesmo lilás da JBL R por escolha do Denis (recorte 3). Na tela da IA ativa, a JBL R continua identificada pelo nome escrito.
- **Conectado ({colors.ok})** só no ponto da pílula de status.
- **Vermelho ({colors.parar})** é o único alarme: PARAR IA (decisão do Denis) e também erro de senha e aviso de risco ([ASSUMPTION] registrada no memlog). Texto de erro e de risco em {colors.erro-texto}; fundo do aviso de risco em {colors.risco-fundo}.
- **Capa ({colors.capa-a} → {colors.capa-b}, contorno {colors.capa-borda})** é neutra para valer em qualquer saída; a cor entra só pelo risco no meio.
- **Campo ({colors.campo}, contorno {colors.campo-borda})**: mais escuro que o cartão, para ser lido como "buraco onde se escreve". Ao escrever no Sub, o fundo vira {colors.saida-sub-fundo}. [NOTE FOR UX] Os fundos de campo ativo da JBL L e da JBL R não foram desenhados; até lá, [ASSUMPTION] usam {colors.campo} com o contorno de 2 px na cor da saída.
- **Transparências** ({colors.ia-halo}, {colors.risco-borda}, {colors.veu}) estão no frontmatter em `#RRGGBBAA` (ordem do CSS). No .NET MAUI a ordem é `#AARRGGBB`; os valores equivalentes estão no comentário de cada token.

**Piso de contraste: 4,5:1** para todo texto de leitura, inclusive rótulos e escalas. Por isso {colors.apagado} subiu de `#6E6E73` (3,4:1 sobre o cartão) para o valor atual. Texto em {colors.apagado} não vai sobre {colors.sup-2} (3,8:1). A única exceção ao piso é o PARAR IA (ver a tabela).

Contraste medido (WCAG):

| Par | Razão | Uso |
|---|---|---|
| {colors.texto} sobre {colors.palco} / {colors.sup-1} | 19,3 / 15,6 | texto principal |
| {colors.apoio} sobre {colors.sup-1} | 6,6 | apoio |
| {colors.apagado} sobre {colors.sup-1} | 4,6 | rótulos e escalas |
| {colors.apagado} sobre {colors.palco} / {colors.lista-aberta} / {colors.registro-fundo} | 5,7 / 5,0 / 5,3 | idem |
| {colors.saida-l} / {colors.saida-r} / {colors.saida-sub} sobre {colors.sup-1} | 7,6 / 7,4 / 11,7 | nomes, números e "Ajustar" |
| {colors.sobre-escolhido} sobre {colors.texto} | 19,3 | escolhido |
| branco sobre {colors.parar} | 3,4 | PARAR IA (20 px, peso 800: texto grande, passa 3:1) |
| {colors.ia-texto} sobre {colors.ia-fundo} | 11,2 | faixa da IA |
| {colors.erro-texto} sobre {colors.sup-1} | 7,5 | erro |

## Typography

Uma família só: **Roboto variável, embutida no app**, para ter os pesos 200 e 800 de verdade (o Roboto do sistema não tem esses dois). A pilha dos mocks é a do BGS Sound Lab ("Segoe UI Variable Display") e cai em Roboto no Android. Números sempre **tabulares**, para os valores não pularem ao mudar. Os tamanhos acompanham a **escala de fonte do Android**.

A tabela lista os papéis de destaque. Os demais tokens aparecem em Components, junto da peça que os usa.

| Papel | Token | Onde |
|---|---|---|
| Nome da memória em uso | {typography.memoria-hero} | Cartão da memória em uso (Início) |
| Estado grande | {typography.estado-hero} | "Desconectado" no Início |
| Nome na lista | {typography.memoria-lista} | Cartão de memória (tela Memórias) |
| Leitura de ganho | {typography.leitura-ganho} | Cartão de saída (Início) |
| Valor do giratório | {typography.valor-giratorio} | Centro do botão giratório |
| Valor do canal | {typography.valor-canal} | Campo do canal da mesa |
| Valor do campo | {typography.valor-campo} | Campo numérico dos parâmetros e do MASTER |
| Título da saída | {typography.titulo-saida} | Cabeçalho do Ajuste da saída, na cor da saída |
| Título de tela | {typography.titulo-tela} | Cabeçalho das telas cheias |
| Título da folha | {typography.titulo-folha} | Folha de confirmação |
| Parâmetro | {typography.titulo-parametro} | "Passa-alta", "Limitador"… |
| Rótulo | {typography.rotulo} | Caixa alta: MEMÓRIA EM USO, SAÍDAS, TIPO DO FILTRO |
| Escala | {typography.escala} | Marcas sob o fader (10, 100, 1k, 22k) |
| Escala do EQ | {typography.escala-eq} | Frequências das 15 bandas no painel ao vivo |

Fora da tabela, quatro tokens de uso amplo:

- {typography.valor-giratorio-campo}: o valor do giratório enquanto se escreve (campo dentro do disco).
- {typography.botao-pequeno}: par de botões, botão OFF, Ajustar do canal da mesa e a variante pequena do botão secundário.
- {typography.botao-secundario}: botão secundário (Cancelar, Deixar desligada).
- {typography.apoio}: frases de apoio.

Regras:

- Pesos seguem a regra 1 de Brand & Style. Exceção: os nomes de canal da Mesa são nomes, mas usam {typography.nome-canal} (peso 800).
- Caixa alta só nos rótulos, com espaçamento entre letras de 0,12em.
- Decimal com vírgula e sinal de menos verdadeiro (−), sempre com unidade ao lado em {typography.detalhe} peso 600 na cor {colors.apoio}.
- **Nenhum texto abaixo de 11 px**, contando escalas e as frequências do EQ ({typography.escala}, {typography.escala-eq}). Nas 15 frequências do painel do EQ, se faltar largura, a linha mostra as frequências alternadas e a banda tocada mostra a própria frequência.

## Layout & Spacing

Celular em retrato, quadro de referência 390 × 844. Uma coluna, margem lateral {spacing.margem}. Os px dos mocks valem como unidades independentes do MAUI.

Regras gerais:

- **Ritmo:** {spacing.gap-tela} entre blocos da tela; {spacing.gap-cartoes} entre cartões de saída e controles; {spacing.gap-grade} entre colunas da grade, abas e canais.
- **Estrutura de tela com barra:** topo do app ({spacing.topo-app}: marca, pílula de status, engrenagem) · conteúdo · barra inferior ({spacing.barra-inferior}).
- **Estrutura de tela cheia:** cabeçalho fixo ({spacing.cabecalho-tela}, com ‹) e, quando há o que salvar, o botão **Salvar na memória N** fixo embaixo; o meio rola. Sem barra inferior.
- **Escala de fonte do Android:** o texto cresce com ela. Blocos crescem na altura e a tela rola; nada é cortado nem sobreposto. Os alvos de toque não diminuem.
- **Alvos de toque:** vão de {spacing.alvo-min} a {spacing.alvo-passo-grande}. Nenhum alvo abaixo de 44 px. [ASSUMPTION] Os botões da grade de tipos de filtro, que no mock têm 42 × 38 px (16 opções em 2 linhas de 8), vão a 44 px de altura na implementação.

Telas:

- **Grade Áudio:** 2 colunas × 5 linhas ocupando a altura entre o topo e a barra; com a fonte do Android maior, a grade rola.
- **Mesa:** 3 canais em colunas iguais que crescem na altura. Embaixo ficam o MASTER (76 px) e os chips; a faixa de monitoração (44 px) fica entre os canais e o MASTER.
- **Player:** lista da playlist rolando entre o cabeçalho (abas de memória) e os controles do player, fixos embaixo, acima da barra.

## Elevation & Depth

A profundidade vem do **tom**, não da sombra: palco → {colors.sup-1} → {colors.sup-2}. Cartões não têm sombra.

A sombra existe só onde há metáfora física:

- **Capa do fader:** `0 6px 12px` preto a 55 %.
- **Disco do giratório:** `0 10px 24px` preto a 60 %.
- **Brilhos:** o ponto de conectado (halo de 8 px em {colors.ok}), o ponto na ponta do giratório (halo na cor da saída), o pulso da IA (anel de 6 px em {colors.ia-halo}).
- **Folha de confirmação:** sobe sobre o {colors.veu}; é a única camada acima da tela.

## Shapes

Cantos generosos e aumentando com o tamanho do bloco: {rounded.md} nos campos e botões pequenos, {rounded.lg} nos − e + e nas listas, {rounded.card} nos cartões e no Salvar, {rounded.painel} nos canais da mesa e na faixa da IA, {rounded.folha} só nos cantos de cima da folha. {rounded.full} para a barra inferior, a pílula de status e os chips. O botão giratório e os pontos de estado são círculos.

## Components

Cada peça traz entre parênteses a chave do bloco correspondente no frontmatter; a ordem é a mesma.

### Navegação e topo

- **Barra inferior** (`barra-inferior`) — Quatro pílulas: Início · Mesa · Áudio · Player, cada uma com ícone de traço de 16 px e texto em {typography.nav}. A ativa em {colors.texto} com texto preto; as outras em {colors.apoio}. Divisória {colors.sup-3} em cima.
- **Topo do app** (`topo-do-app`) — Faixa de {spacing.topo-app} com a marca "**BGS** Controle" ({typography.marca-forte} no BGS, {typography.marca} no resto), a **pílula de status** e a **engrenagem**.
- **Pílula de status** (`pilula-status`) — {colors.sup-1}, contorno {colors.sup-2}, {rounded.full}. Ponto de 7 px: {colors.ok} com halo (Conectado · R003), {colors.apoio} sem halo (Conectando · senha), {colors.apagado} (Desconectado).
- **Engrenagem** (`engrenagem`) — Botão quadrado de {spacing.alvo-min}, fundo {colors.sup-1}, {rounded.lg}, no topo do app.
- **Cabeçalho de tela cheia** (`cabecalho-tela-cheia`) — ‹ em quadrado de {spacing.alvo-passo} ({colors.sup-1}, {rounded.xl}); título em {typography.titulo-tela} com subtítulo em {typography.detalhe} {colors.apoio}. No Ajuste da saída, o título usa {typography.titulo-saida} na cor da saída e o subtítulo diz "Saídas 3+4 em ponte · rota A+B · memória 1".

### Início

- **Cartão da memória em uso** (`cartao-memoria-em-uso`) — {colors.sup-1}, {rounded.card-lg}; rótulo MEMÓRIA EM USO, nome em {typography.memoria-hero}, linha "Memória 1 · Master 0,0 dB", o arco de luz nas três cores no canto e os **botões de memória** embaixo.
- **Botões de memória** (`botoes-de-memoria`) — Três botões segmentados de {spacing.alvo-min} (o do meio 1,45× mais largo para caber "2 MUSICA ORIG"); a memória em uso em branco.
- **Cartão de saída** (`cartao-saida`) — {colors.sup-1}, {rounded.card}, barra lateral de 4 px na cor. Nome em {typography.nome-saida} com "Saída 1 · rota B" em {colors.apoio}; ganho em {typography.leitura-ganho}; resumo em duas linhas (PA, PB, limitador, atraso, fase); mini curva do crossover de 76 × 40 px na cor; "Ajustar ›" em {typography.ajustar-saida} na cor.

### Mesa

- **Abas de memória** (`abas-de-memoria`) — Três abas de 60 px, rótulo MEMÓRIA N pequeno e nome em negrito; a ativa em branco.
- **Canal da mesa** (`canal-da-mesa`) — Coluna {colors.sup-1}, {rounded.painel}, borda superior de 4 px na cor. Nome em {typography.nome-canal} na cor, "Saída 1 · B" em {colors.apagado}, **campo numérico** com o ganho, fader vertical (trilho de 8 px, nível na cor, marca do 0 dB em {colors.apagado}, capa), resumo PA/PB/Lim e botão **Ajustar** ({spacing.alvo-min}, {colors.sup-2}, texto em {typography.botao-pequeno} na cor).
- **MASTER** (`master`) — Faixa de 76 px: "MASTER · todas as saídas", fader horizontal com escala −80 · −40 · 0, campo de 100 px. A cor do MASTER é {colors.texto}. O 0,0 dB fica no fim direito da escala (é o máximo).
- **Faixa de monitoração** (`faixa-de-monitoracao`) — Faixa de {spacing.alvo-min}, {colors.sup-1}, {rounded.lg}. À esquerda, "Voltímetro" com o valor em {typography.valor-campo-linha} e "V"; à direita, um ponto de clip por saída e um da entrada, cada um com o nome escrito embaixo (L, R, Sub, Entrada). Ponto apagado em {colors.sup-2}; com clip, aceso em {colors.parar}. O mesmo ponto aceso aparece no topo da coluna do canal da mesa.
- **Chips da Mesa** (`chips-da-mesa`) — Pílulas de 38 px com contorno {colors.sup-2}: "● Remote out ligado", "EQ gráfico plano".

### Controles de valor

- **Capa do fader** (`capa-do-fader`) — Uma peça só: degradê {colors.capa-a} → {colors.capa-b}, contorno {colors.capa-borda}, risco de 2 px na cor da saída. 56 × 30 no vertical, 30 × 44 no horizontal.
- **Botão giratório** (`botao-giratorio`) — 226 px. Arco de 16 px na cor da saída sobre {colors.sup-2}, curso de 270° começando embaixo à esquerda; [ASSUMPTION] o arco mostra a posição real do valor dentro da faixa. Disco interno com degradê radial e contorno {colors.capa-borda}, rótulo GANHO, valor em {typography.valor-giratorio} com [ASSUMPTION] sublinhado tracejado na cor (sinal de "toque para escrever") e "dB". Ao escrever, o valor vira campo em {typography.valor-giratorio-campo}. Ponto branco de 10 px na ponta do arco.
- **Botões − e +** (`botoes-mais-menos`) — Passo comum: {spacing.alvo-passo}, {colors.sup-2}, contorno {colors.campo-borda}, {typography.passo}. Passo do ganho: {spacing.alvo-passo-grande}, contorno de 2 px, {typography.passo-grande}, com as frases "Gire o botão ou toque em − e +" e "Toque no número para escrever" entre os dois botões.
- **Campo numérico** (`campo-numerico`) — {spacing.alvo-min} de altura, fundo {colors.campo}, contorno {colors.campo-borda}, **sublinhado de 2 px na cor da saída**, valor alinhado à direita em {typography.valor-campo} e unidade pequena. Ativo (escrevendo): contorno inteiro de 2 px na cor e cursor na cor.
- **Teclado** (`teclado`) — O teclado do sistema Android (numérico para valores, alfanumérico para senha e nomes). Os mocks só o ilustram.
- **Controle de parâmetro** (`controle-de-parametro`) — Bloco {colors.sup-1}, {rounded.card}: em cima, o nome ({typography.titulo-parametro}) com a faixa e o passo em {colors.apagado} ("10 Hz a 22 kHz · − e + de 1/12 de oitava") e o campo à direita; embaixo, − · fader horizontal · + e a escala. Frequências e Q usam escala logarítmica.
- **Lista recolhida** (`lista-recolhida`) — Linha de {spacing.alvo-lista} com rótulo (TIPO DO FILTRO), descrição do escolhido ("Butterworth · 24 dB por oitava"), a **pílula** branca com o escolhido (BT24) e a seta. Aberta: fundo {colors.lista-aberta}, seta para cima, legenda "LR Linkwitz-Riley · BT Butterworth" e a grade de opções em botões {rounded.sm}, com o escolhido em branco.
- **Avançado** (`avancado`) — Mesma linha da lista recolhida, sem pílula ("AVANÇADO" e o que há dentro). Aberto: fundo {colors.lista-aberta}, contorno {colors.campo-borda}.
- **Par de botões** (`par-de-botoes`) — Dois botões lado a lado (Desl. · Lig.; 0° · 180°), o escolhido em branco. Em linha: 112 px de largura total e {spacing.alvo-min} de altura. Na Fase do Ajuste: largura inteira e {spacing.alvo-lista}.
- **Botão OFF** (`botao-off`) — Botão de {spacing.alvo-min} ao lado do campo do limitador; branco quando o limitador está desligado.

### Botões e folhas

- **Botão principal** (`botao-principal`) — {spacing.alvo-principal}, {colors.texto} com texto preto, {rounded.card}, {typography.botao-principal}: Salvar na memória N, Conectar, confirmação da folha.
- **Botão secundário** (`botao-secundario`) — {spacing.alvo-lista}, só contorno {colors.campo-borda}, texto {colors.texto} em {typography.botao-secundario}: Cancelar, Deixar desligada. Variante pequena de 44 px ({colors.sup-2}, {typography.botao-pequeno}) para Trocar e Resetar; travada, fica sem fundo e com texto em {colors.apagado}.
- **Folha de confirmação** (`folha-de-confirmacao`) — Sobe de baixo sobre o {colors.veu}: pega de 40 × 4 px, título em {typography.titulo-folha}, texto em {typography.corpo} com o que vai ser perdido em negrito branco, campo opcional (nome depois de salvar), botão principal e botão secundário.

### IA

- **Faixa da IA** (`faixa-da-ia`) — Fundo {colors.ia-fundo}, contorno de 2 px {colors.ia}, {rounded.painel}. Pulso lilás de 14 px, título em {typography.titulo-faixa-ia} ("IA ajustando o EQ da FILME"), explicação em {colors.ia-texto} e o **botão PARAR IA**.
- **Botão PARAR IA** (`botao-parar-ia`) — Dentro da faixa da IA: {spacing.alvo-parar} de altura, fundo {colors.parar}, texto branco em {typography.botao-parar}, {rounded.card}.
- **Painel ao vivo** (`painel-ao-vivo`) — {colors.sup-1}: cabeçalho com o nome do que a IA está mexendo e o selo "● AO VIVO" em lilás. Controles em miniatura esmaecidos (opacidade 0,42); o item que a IA mexe agora volta à opacidade 1, com fundo {colors.ia-fundo} e capa em lilás. Linha "Agora: banda 2,5 kHz · 0,0 → +1,0 dB" com o valor em lilás.
- **Registro** (`registro`) — Fundo {colors.registro-fundo}, contorno {colors.sup-3}: linhas com hora (42 px) e frase; a linha atual em {colors.texto}, com a hora em lilás.

### Áudio e configurações

- **Abas de saída** (`abas-de-saida`) — Nas telas de função: três abas de 56 px com borda superior de 4 px na cor da saída, nome na cor e resumo pequeno ("PA 200 · PB OFF"); a escolhida em branco, mantendo a borda colorida.
- **Bloco de função** (`bloco-de-funcao`) — Bloco da grade Áudio: ícone de traço branco em quadrado de 40 px {colors.sup-2}, nome em {typography.titulo-parametro}, resumo do valor atual em {typography.detalhe} com os números na cor de cada saída, e › em {colors.apagado}. Travado: ícone a 50 %, nome em {colors.apoio} e um cadeado de traço com "Chave desligada".
- **Linha de configuração** (`linha-de-configuracao`) — Linhas de {spacing.alvo-lista} dentro de grupos {colors.sup-1}, com título, descrição em {colors.apoio} e, à direita, ›, um par de botões, um campo ou um botão secundário pequeno. Grupos com rótulo em cima (APARELHO, ACESSO E IA).
- **Linha de chave** (`linha-de-chave`) — Linha de configuração com o risco escrito em {typography.risco-chave}: em {colors.erro-texto} quando a chave vem desligada por padrão, em {colors.apoio} nas outras.

### Memórias e conexão

- **Cartão de memória** (`cartao-de-memoria`) — {colors.sup-1}, {rounded.card-lg}: MEMÓRIA N, selo EM USO branco quando é a que toca, nome em {typography.memoria-lista}, botão Renomear contornado e Carregar · Salvar aqui em dois botões de 48 px.
- **Selo herdado** (`selo-herdado`) — Pílula contornada "HERDADO" em {typography.rotulo}, nos cartões de memória e nos presets que vieram do conhecimento dos protótipos.
- **Cartão de conexão** (`cartao-de-conexao`) — {colors.sup-1}: rótulo PROCESSADOR, "Desconectado" em {typography.estado-hero}, frase de ajuda e o botão principal Conectar. As saídas embaixo ficam vazias: barra lateral a 40 %, nome em {colors.apoio} e "—" no lugar do valor.
- **Caixas da senha** (`caixas-da-senha`) — Quatro caixas de 64 px, fundo {colors.campo}, sublinhado em {colors.apagado}; a caixa atual com contorno branco de 2 px; os caracteres viram • na hora.

### Player

- **Linha da playlist** (`linha-da-playlist`) — Linha de {spacing.alvo-lista}: nome da faixa em {typography.linha-titulo}, duração e pasta em {typography.detalhe}. A faixa que toca tem a marca branca de 4 px na borda e o nome em peso 700. Arquivo que sumiu: nome em {colors.apagado} e "Arquivo não encontrado". No modo Editar, cada linha ganha a alça de arrastar (≡) à direita e o botão remover (−) de 44 px à esquerda.
- **Controles do player** (`controles-do-player`) — Fixos embaixo: nome da faixa atual, botão tocar/pausar redondo de {spacing.alvo-parar} em branco com ícone preto e o botão próxima de {spacing.alvo-passo} em {colors.sup-2}. Acima, a linha da memória: "Playlist da FILME".

### Avisos

- **Aviso de risco** (`aviso-de-risco`) — Dentro da folha: fundo {colors.risco-fundo}, contorno {colors.risco-borda}, ícone de alerta em {colors.parar} e o texto do risco em branco, em {typography.texto-risco}.
- **Aviso de erro** (`aviso-de-erro`) — Ponto vermelho de 8 px e texto {colors.erro-texto} em {typography.texto-erro}.
- **Lembrete** (`lembrete`) — Ponto de 8 px com brilho na cor da saída e frase em {typography.detalhe}: "Mexer muda o som **na hora**…".
- **Faixa "DESLIGUE O MÓDULO AGORA"** (`faixa-desligue-o-modulo`) — Fundo {colors.parar}, texto branco em {typography.botao-parar}. Sem mock.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Uma luz por saída, só onde ela é lida ou mexida | Pintar fundo de bloco com a cor da saída |
| Branco só para o que está escolhido | Usar branco como destaque decorativo |
| Peso 200 para ler, 700–800 para mexer | Valor mexível em peso fino, ou nome de leitura em negrito |
| Uma só capa de fader, neutra, com o risco na cor | Capa colorida ou capas diferentes por tela |
| Vermelho só para parar, erro e risco | Vermelho de marca (os ícones vermelhos da Taramps não vêm) |
| Campo escuro com sublinhado na cor | Campo que parece texto solto, sem sinal de que se escreve |
| Profundidade por tom; sombra só na capa e no disco | Sombra em cartão |
| Nome da saída sempre escrito ao lado da cor | Cor como única forma de saber qual saída é |
| Números tabulares com vírgula e unidade | Ponto decimal, hífen no lugar do sinal de menos, número sem unidade |
| Lilás da IA com o nome da IA escrito na faixa | Lilás da IA sem texto quando a JBL R está na mesma tela |
