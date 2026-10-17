---
name: BGS Sound Lab
description: Identidade visual do painel do home theater do Denis Borges, com a IA KARINA · Engenheira de Som. Duas superfícies da mesma família, a Vitrine (TV, tela cheia, para convidados) e os Bastidores (notebook, janela, só o Denis), no registro de lançamento de produto (Keynote).
status: final
updated: 2026-10-17
colors:
  # Núcleo Keynote, extraído de .working/vitrine-keynote.html (mockups/vitrine-keynote.html)
  preto: '#000000'
  branco: '#FFFFFF'
  cinza-1: '#A1A1A6'
  cinza-2: '#6E6E73'
  luz-fria: '#8FB4FF'
  luz-lilas: '#C59BFF'
  luz-quente: '#FFD6A8'
  # Neutros de interface, extraídos dos controles do mesmo mock
  texto-claro: '#F5F5F7'
  texto-apoio: '#D2D2D7'
  cinza-3: '#86868B'
  superficie-1: '#1C1C1E'
  contorno: '#2C2C2E'
  contorno-suave: '#232325'
  # Bastidores: acréscimos [ASSUMPTION], mesma família
  superficie-0: '#0E0E10'
  canal-1: '#FF6961'
  canal-2: '#30D158'
  canal-3: '#409CFF'
  canal-4: '#FF9F0A'
  estado-ok: '#30D158'
  estado-recusa: '#FF453A'
  estado-atencao: '#FFD6A8'
typography:
  # Família única: "Segoe UI Variable Display", "Segoe UI Variable", "Segoe UI", system-ui, sans-serif
  # Tamanhos da Vitrine medidos no quadro de 1920 px (no código, em cqw do quadro 16:9).
  vitrine-wordmark:
    fontFamily: Segoe UI Variable Display
    fontSize: 127px
    fontWeight: '200'
    lineHeight: '1'
    letterSpacing: -0.02em
  vitrine-wordmark-forte:
    fontFamily: Segoe UI Variable Display
    fontSize: 127px
    fontWeight: '500'
    lineHeight: '1'
    letterSpacing: 0.01em
  vitrine-assinatura:
    fontFamily: Segoe UI Variable Display
    fontSize: 25px
    fontWeight: '400'
    lineHeight: '1.2'
    letterSpacing: 0.34em
  vitrine-linha-ia:
    fontFamily: Segoe UI Variable Display
    fontSize: 27px
    fontWeight: '300'
    lineHeight: '1.3'
    letterSpacing: 0.01em
  karina-nome:
    # Nome da IA: mesma família, sempre secundário ao wordmark BGS; herda o tamanho da frase
    fontFamily: Segoe UI Variable Display
    fontWeight: '500'
    letterSpacing: 0.08em
  karina-titulo:
    fontFamily: Segoe UI Variable Display
    fontSize: 26px
    fontWeight: '400'
    lineHeight: '1.2'
    letterSpacing: 0.12em
  selo:
    fontFamily: Segoe UI Variable Display
    fontSize: 16.5px
    fontWeight: '400'
    lineHeight: '1'
    letterSpacing: 0.14em
  # Bastidores [ASSUMPTION]: rampa derivada do mock, lida a 60–80 cm do notebook
  bastidores-marca:
    fontFamily: Segoe UI Variable Display
    fontSize: 26px
    fontWeight: '200'
    lineHeight: '1'
    letterSpacing: -0.01em
  regiao-titulo:
    fontFamily: Segoe UI Variable Display
    fontSize: 26px
    fontWeight: '300'
    lineHeight: '1.2'
    letterSpacing: -0.01em
  valor-destaque:
    fontFamily: Segoe UI Variable Display
    fontSize: 44px
    fontWeight: '200'
    lineHeight: '1.05'
    letterSpacing: -0.02em
  corpo:
    fontFamily: Segoe UI Variable Display
    fontSize: 17px
    fontWeight: '400'
    lineHeight: '1.5'
  corpo-sm:
    fontFamily: Segoe UI Variable Display
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.45'
  rotulo:
    fontFamily: Segoe UI Variable Display
    fontSize: 12.5px
    fontWeight: '500'
    lineHeight: '1.2'
    letterSpacing: 0.14em
  codigo:
    fontFamily: Cascadia Mono
    fontSize: 13.5px
    fontWeight: '400'
    lineHeight: '1.4'
rounded:
  sm: 6px
  md: 10px
  lg: 16px
  full: 9999px
spacing:
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 24px
  '6': 32px
  '7': 48px
  vitrine-margem: 2.4cqw
  bastidores-margem: 40px
  bastidores-gutter: 24px
  regiao-padding: 24px
motion:
  # Extensão deste projeto (não faz parte das chaves do spec); EXPERIENCE.md referencia por nome.
  abertura-duracao: 10000ms
  abertura-reduzida: 2500ms
  pular-abertura-apos: 1000ms
  saida-suave: 150ms
  transicao: 300ms
  expandir: 420ms
  easing-entrada: cubic-bezier(0.16, 1, 0.3, 1)
  easing-respiro: ease-in-out
  selo-piscar: 2600ms
  anel-ocioso-periodo: 6000ms
  anel-ataque: '0.55 por quadro a 60 fps'
  anel-soltura: '0.10 por quadro a 60 fps'
components:
  anel-de-luz:
    raio-base: 232px
    centro: '960 × 402 (quadro 1920 × 1080)'
    gradiente: '{colors.luz-fria} → {colors.luz-lilas} → {colors.luz-quente} (cônico, girando)'
    nucleo: '{colors.branco}'
    expansao-grave: '+7,5% do raio no grave máximo'
  vitrine-wordmark:
    texto: 'BGS (peso 500) + Sound Lab (peso 200)'
    tipografia: '{typography.vitrine-wordmark}'
    cor: '{colors.branco}'
  vitrine-assinatura:
    texto: 'by Denis Borges'
    tipografia: '{typography.vitrine-assinatura}'
    cor: '{colors.cinza-1}'
    nome: '{colors.branco}'
  vitrine-linha-ia:
    texto: 'Som ajustado pela KARINA (+ · Modo: <modo>, quando conhecido)'
    tipografia: '{typography.vitrine-linha-ia}'
    cor: '{colors.cinza-1}'
    karina: '{colors.branco}'
    modo: '{colors.branco}'
    separador: '{colors.cinza-2}'
  karina-nome:
    texto: 'KARINA · Engenheira de Som'
    nome: '{typography.karina-nome}'
    cor-nome: '{colors.branco}'
    complemento: '{colors.cinza-1}'
    regra: 'nunca maior nem mais pesado que o BGS do wordmark; sem lockup próprio'
  seletor-modo:
    background: 'transparent'
    borda: '{colors.contorno}'
    ativo-background: '{colors.texto-claro}'
    ativo-foreground: '{colors.preto}'
    foreground: '{colors.texto-apoio}'
    radius: '{rounded.full}'
  selo-em-evolucao:
    tipografia: '{typography.selo}'
    cor: '{colors.cinza-1}'
    borda: 'rgba(255,255,255,.18)'
    ponto: '{colors.luz-quente}'
    radius: '{rounded.full}'
  botao-bastidores:
    tamanho: 44px
    borda: 'rgba(255,255,255,.10)'
    opacidade-repouso: '0.32'
    opacidade-foco: '0.9'
    radius: '{rounded.full}'
  botao-primario:
    background: '{colors.texto-claro}'
    foreground: '{colors.preto}'
    radius: '{rounded.full}'
  botao-secundario:
    background: '{colors.superficie-1}'
    foreground: '{colors.texto-claro}'
    borda: '{colors.contorno}'
    radius: '{rounded.full}'
  regiao:
    background: '{colors.superficie-0}'
    borda: '{colors.contorno-suave}'
    radius: '{rounded.lg}'
    padding: '{spacing.regiao-padding}'
  chip-status:
    background: 'transparent'
    borda: '{colors.contorno}'
    foreground: '{colors.texto-apoio}'
    radius: '{rounded.full}'
  faixa-aviso:
    background: 'rgba(255,214,168,.08)'
    borda: 'rgba(255,214,168,.35)'
    foreground: '{colors.luz-quente}'
    radius: '{rounded.md}'
  selo-decisao-aprovado:
    ponto: '{colors.estado-ok}'
    foreground: '{colors.texto-claro}'
    radius: '{rounded.full}'
  selo-decisao-recusado:
    ponto: '{colors.estado-recusa}'
    foreground: '{colors.texto-claro}'
    radius: '{rounded.full}'
  miniatura-tela:
    background: '{colors.preto}'
    borda: '{colors.contorno-suave}'
    borda-foco: 'rgba(255,255,255,.45)'
    radius: '{rounded.md}'
  tela-expandida:
    background: '{colors.preto}'
    veu: 'rgba(0,0,0,.72)'
    borda: '{colors.contorno}'
    radius: '{rounded.lg}'
  curva-svg:
    grade: '{colors.contorno-suave}'
    linha-zero: '{colors.contorno}'
    canais: '{colors.canal-1} · {colors.canal-2} · {colors.canal-3} · {colors.canal-4}'
    espessura: 2px
---

## Brand & Style

**BGS Sound Lab**, com a assinatura **"by Denis Borges"**. BGS vem de Borges. É a marca pessoal do home theater que o Denis montou e regula com uma inteligência artificial, a **KARINA · Engenheira de Som**, que leva o nome da esposa dele em homenagem. A marca existe para que um convidado, olhando a TV da sala, entenda três coisas sem explicação: isto parece coisa de marca grande, existe uma IA cuidando do som (a KARINA), e quem fez foi o Denis.

**Hierarquia dos nomes:** BGS Sound Lab é a marca e vem sempre primeiro; "by Denis Borges" é a assinatura; KARINA é a personagem que trabalha dentro da marca, sempre secundária. A marca continua "BGS Sound Lab by Denis Borges", e a KARINA nunca vira um segundo logo.

O registro é **Keynote**: um lançamento de produto da Apple ou da Bang & Olufsen. Preto absoluto, quase nada na tela, e **a luz é a protagonista**. O anel de luz é o símbolo da marca: ele respira com o grave da música que toca na sala, e é isso que faz alguém perguntar "que som é esse?!". A tipografia é enorme e fina, com "BGS" em peso forte e "Sound Lab" fino, para soar como nome de empresa. A assinatura fica espaçada logo abaixo e deixa claro de quem é.

Duas superfícies, uma família:

- **Vitrine** (TV ou projetor, tela cheia). **Nada técnico**: nem número, nem sigla, nem nome de parâmetro. Só a marca, a assinatura, o anel e uma linha humana ("Som ajustado pela KARINA · Modo: Filme"), mais o selo discreto "em evolução".
- **Bastidores** (notebook, janela normal, só o Denis). Mesmo preto, mesma luz e mesma tipografia, porém **mais densa**: regiões em superfícies quase pretas, números tabulares e códigos em fonte mono quando o valor exato importa. É o mesmo produto visto por dentro, não outro aplicativo.

**A abertura sonora faz parte da marca.** São cerca de 10 segundos de efeitos sintetizados, sem música, que acompanham o nascimento da luz: um grave misterioso, camadas que sobem, um impacto grandioso na revelação do nome e uma assinatura sonora curta e elegante no fim. A referência é a imersão das aberturas de console e de jogos AAA, sem perder a sofisticação. Depois disso, silêncio. A marca só fala uma vez; o resto é luz seguindo a música da sala. O comportamento (fases, tempos, volume) está em [EXPERIENCE.md · Abertura audiovisual](EXPERIENCE.md#abertura-audiovisual).

Referência visual da Vitrine: [mockups/vitrine-keynote.html](mockups/vitrine-keynote.html) ([png](mockups/vitrine-keynote.png)). Referência dos Bastidores: [mockups/bastidores.html](mockups/bastidores.html) ([png](mockups/bastidores.png)). **Os spines (este arquivo e o EXPERIENCE.md) vencem qualquer conflito com mocks, rascunhos ou imports.**

Rejeitado, para não voltar: as três direções técnicas (Rack Âmbar com VUs, Bancada de Medição com osciloscópio, Vidro Negro com vermelho Taramps). Eram técnicas demais. O vermelho e o laranja do app Taramps não são cores da marca.

## Colors

O núcleo tem sete cores e todas vêm do mock Keynote.

- **Preto (`{colors.preto}`)** é o palco. É o fundo de tudo, nas duas superfícies, e não tem gradiente de fundo nem textura. O único brilho na tela vem do anel.
- **Branco (`{colors.branco}`)** é a tipografia principal e o núcleo da luz do anel. Na Vitrine, só a marca, o nome "Denis Borges", o nome "KARINA" e o modo ficam em branco.
- **Cinza 1 (`{colors.cinza-1}`)** é o texto de apoio: a assinatura "by", a linha da IA e o selo. Contraste de 8,2:1 sobre o preto.
- **Cinza 2 (`{colors.cinza-2}`)** fica para separadores e anotações grandes. Ele tem 4,1:1 sobre o preto e por isso **nunca** vai em texto menor que 18,66 px em peso normal.
- **Luz fria (`{colors.luz-fria}`), luz lilás (`{colors.luz-lilas}`) e luz quente (`{colors.luz-quente}`)** existem só como luz: no halo cônico do anel, que gira devagar, e no ponto do selo, que é quente. Não são cores de texto, nem de botão, nem de estado na Vitrine.

Neutros de interface (também do mock): **texto claro (`{colors.texto-claro}`)** para títulos dos Bastidores e para o preenchimento do botão primário, **texto de apoio (`{colors.texto-apoio}`)** para o corpo, **cinza 3 (`{colors.cinza-3}`, 5,8:1)** para rótulos pequenos, e **superfície 1 (`{colors.superficie-1}`)**, **contorno (`{colors.contorno}`)** e **contorno suave (`{colors.contorno-suave}`)** para botões e bordas.

Acréscimos dos Bastidores **[ASSUMPTION]**, todos da mesma família escura:

- **Superfície 0 (`{colors.superficie-0}`)** é o fundo das regiões, um degrau acima do preto.
- **Canais 1 a 4** (`{colors.canal-1}` vermelho, `{colors.canal-2}` verde, `{colors.canal-3}` azul, `{colors.canal-4}` laranja). Mantêm o mapeamento de matiz do app Taramps, que o Denis já conhece (vermelho 1, verde 2, azul 3, laranja 4), em tons mais luminosos para o fundo preto. Usados **só** em curvas SVG e nas marcas de canal das miniaturas. Todos passam de 7:1 sobre o preto.
- **Estado OK (`{colors.estado-ok}`)**, **recusa (`{colors.estado-recusa}`)** e **atenção (`{colors.estado-atencao}`, a própria luz quente)** aparecem só como ponto ou fio, nunca como fundo cheio. A palavra sempre acompanha a cor: APROVADO, RECUSADO, antiga.

Contraste mínimo para combinações que carregam informação: texto ≥ 4,5:1 sobre `{colors.preto}` e `{colors.superficie-0}`. Os pares do texto claro, do texto de apoio, do cinza 1 e do cinza 3 passam; o cinza 2 não passa em texto pequeno.

## Typography

Uma família só: **Segoe UI Variable Display**, com recuo para Segoe UI Variable, Segoe UI e system-ui. Ela já vem no Windows do notebook, então não depende de internet nem de arquivo de fonte. A personalidade vem do contraste de peso: **200 contra 500**.

- **Wordmark** (`{typography.vitrine-wordmark}` e `{typography.vitrine-wordmark-forte}`): "**BGS**" em 500 com espaçamento levemente aberto, "Sound Lab" em 200 com espaçamento fechado. Com 127 px no quadro de 1920, é lido do sofá.
- **Assinatura** (`{typography.vitrine-assinatura}`): "by Denis Borges" bem espaçado (0,34 em). "Denis Borges" em 500 e branco; "by" em cinza 1.
- **Linha da IA** (`{typography.vitrine-linha-ia}`): peso 300, frase humana ("Som ajustado pela KARINA · Modo: Filme"), com "KARINA" e o modo em 500 e branco.
- **Nome KARINA**: `{typography.karina-nome}` dentro de uma frase, herdando o tamanho dela, e `{typography.karina-titulo}` como título da região nos Bastidores. Sempre em maiúsculas, peso 500 (400 no título) e espaçamento aberto. Na Vitrine, herda o tamanho da linha da IA e fica bem menor que o wordmark. O complemento "Engenheira de Som" vai em cinza 1, peso normal.
- **Selo** (`{typography.selo}`): minúsculas espaçadas, "em evolução".
- **Bastidores** **[ASSUMPTION]**: `{typography.bastidores-marca}` no cabeçalho, `{typography.regiao-titulo}` para o título de cada região, `{typography.valor-destaque}` para os números que importam (por exemplo 150 Hz, 0,56 V), `{typography.corpo}` e `{typography.corpo-sm}` para o texto, `{typography.rotulo}` para rótulos em minúsculas espaçadas, e `{typography.codigo}` (Cascadia Mono) **só** para códigos do protocolo (`081:0=-12.0`) e nomes de arquivo.

Regras: números sempre com `font-variant-numeric: tabular-nums`. Sem caixa alta corrida, nem nos rótulos, porque o registro Keynote fala baixo. A exceção é o nome KARINA, que é um nome próprio e sempre vai em maiúsculas. Decimais em vírgula, como no Brasil (0,56 V), exceto dentro de códigos do protocolo, que ficam como o aparelho escreve.

## Layout & Spacing

**Vitrine.** É um quadro 16:9 que ocupa a tela inteira. Os tamanhos são proporcionais à largura do quadro (unidades `cqw`), de modo que a composição é a mesma na TV e no projetor Wanbo T6 Max. Composição fixa e centrada: o anel acima do centro óptico (centro em y = 402 de 1080), o wordmark logo abaixo, a assinatura colada nele, a linha da IA no rodapé, o selo no canto superior direito e o botão dos Bastidores no canto inferior direito, todos a `{spacing.vitrine-margem}` das bordas. Nada mais entra nessa tela.

**Bastidores.** Janela normal maximizada no notebook, sem cobrir a barra de tarefas. A referência é 1920 × 1080, e a tela precisa funcionar a 1366 px (piso herdado do painel atual). Margem externa de `{spacing.bastidores-margem}` e gutter de `{spacing.bastidores-gutter}`. A grade:

1. **Cabeçalho** de altura única: a marca pequena com "Bastidores" à esquerda, os chips de estado no meio e, à direita, "Ler agora" e "voltar à Vitrine".
2. **Três colunas** de altura igual: *Agora* (o aparelho, o guardião e as memórias), *KARINA* (a região do agente: o passo da regulagem e a linha do tempo), com *Memórias* e o seletor de modo embaixo e *Jornada* (o que está feito e o que está planejado).
3. **Faixa inferior** com as **10 telas do app** em miniatura, numa linha só, todas do mesmo tamanho.

A 1366 px, as três colunas passam a duas (Jornada desce para baixo da KARINA) e as miniaturas quebram em 5 + 5. **[ASSUMPTION]**

Escala de espaçamento em múltiplos de 4 (`{spacing.1}` a `{spacing.7}`). Dentro das regiões, `{spacing.regiao-padding}`.

## Elevation & Depth

**Não há sombra.** A profundidade vem da luz e de degraus tonais: preto, depois `{colors.superficie-0}` nas regiões, depois `{colors.superficie-1}` nos botões secundários. As bordas são fios de 1 px em `{colors.contorno-suave}`.

A única coisa que "brilha" é luz de verdade: o anel (com halo desfocado e composição aditiva), o ponto do selo, o ponto "ao vivo" da KARINA quando ela está escrevendo e o leve halo branco do botão primário quando não há leitura. A tela expandida de uma miniatura aparece sobre um véu preto translúcido (`rgba(0,0,0,.72)`), sem sombra projetada.

## Shapes

Formas suaves, com o círculo como forma-mãe (o anel). Pílulas (`{rounded.full}`) para botões, chips e selos. `{rounded.md}` para as miniaturas e faixas, `{rounded.lg}` para as regiões e a tela expandida, e `{rounded.sm}` para os detalhes internos (barras do EQ, marcas de canal). Nada de canto vivo e nada de chanfro "industrial", porque isso pertence às direções técnicas rejeitadas.

## Components

- **Anel de luz** (`components.anel-de-luz`). Um traço branco nítido com dois halos desfocados por baixo, num gradiente cônico frio → lilás → quente que gira devagar. Há também um anel interno muito fino a 88% do raio. Raio base de 232 px no quadro de 1920. O grave expande o anel até +7,5% e dá brilho ao halo; médios e agudos deformam o contorno de leve; uma batida forte solta uma onda fina que se expande e some. Em repouso (microfone indisponível) fica só o respiro lento descrito no EXPERIENCE.md. Também é o ícone de "voltar à Vitrine", num círculo fino de 20 px.
- **Wordmark, assinatura e linha da IA**. Estes três estão especificados nos tokens acima, sempre em branco e cinza 1 sobre preto, sem cor de luz no texto.
- **Nome KARINA** (`components.karina-nome`). "KARINA" em branco com espaçamento aberto, seguido de " · Engenheira de Som" em cinza 1. Aparece em três lugares: na linha da IA da Vitrine; na fase 4 da abertura, onde a linha que surge com a assinatura é essa mesma; e no título da região do agente nos Bastidores ("KARINA", com o subtítulo "Engenheira de Som · /regulagem" no rótulo à direita). Nunca é maior nem mais pesado que o "BGS" do wordmark, e não tem ícone nem lockup próprio.
- **Selo "em evolução"**. Pílula com fio branco a 18%, o texto em cinza 1 e um ponto de luz quente que pisca a cada `{motion.selo-piscar}`.
- **Botão Bastidores** (Vitrine). Círculo de 44 px com fio quase invisível e o ícone de três faders, com opacidade 0,32 em repouso e 0,9 no foco. É discreto de propósito: o convidado não deve notá-lo.
- **Toque para começar** **[ASSUMPTION]**. Preto total, um ponto de luz branca parado no centro do anel e a frase "Toque para começar" em `{typography.vitrine-linha-ia}` e cinza 1. Nada mais.
- **Login único** **[ASSUMPTION]**. Mesma linguagem dos Bastidores: no centro, a região `components.regiao` com "Denis" fixo, um campo de 4 caracteres sem eco (pontos grandes, espaçados) e o botão primário "Entrar". A mensagem de erro vai em luz quente.
- **Botão primário** ("Ler agora"). Pílula `{colors.texto-claro}` com texto preto. Quando não há leitura, ganha um halo branco suave e estático (sem pulsar). Desabilitado, fica `{colors.superficie-1}` com o texto "lendo… 13 s" em cinza 1.
- **Botão secundário** ("voltar à Vitrine" e "fechar"). Pílula `{colors.superficie-1}` com fio `{colors.contorno}`.
- **Região** (`components.regiao`). Superfície 0, fio suave e canto lg. O título em `{typography.regiao-titulo}` e um rótulo pequeno à direita (por exemplo "16/10 · 17:47").
- **Chip de estado**. Pílula vazada com um ponto opcional: ponto de luz quente para "antiga" ou "incompleta", ponto verde para "regra 6 ligada", nenhum ponto para o neutro (firmware).
- **Faixa de aviso**. Fio e texto em luz quente sobre um véu quente a 8%, usada para "leitura antiga" e "incompleta". Usa a mesma família para "sem conexão com o painel local" **[ASSUMPTION]**.
- **Item da linha do tempo**. A hora em mono à esquerda, o selo de decisão (ponto verde APROVADO ou ponto vermelho RECUSADO) e uma frase humana em `{typography.corpo}`, com o código exato embaixo em `{typography.codigo}` e cinza 3. Simulação leva o rótulo "simulação" em cinza 3.
- **Passo da regulagem** (região KARINA). O rótulo "regulagem", "Etapa 0 · passo 2 de 3" em `{typography.valor-destaque}` (um degrau menor, 34 px, para caber) e a nota de retomada em cinza 1. Embaixo, uma régua de 9 segmentos finos (etapas 0 a 8) em `{colors.superficie-1}`: a etapa atual tem um fio branco a 35% e se preenche de branco na proporção dos passos feitos.
- **Ponto ao vivo** (KARINA escrevendo, ou leitura em curso). Um ponto de luz fria que respira, seguido de "escrevendo 081:0=-12.0…" no item do topo da linha do tempo.
- **Seletor de modo** (`components.seletor-modo`, na região Memórias). Uma fileira de pílulas vazadas: Filme, Música SPL, Música EQ e nenhum. A opção ativa vai cheia em texto claro, com texto preto. Ao lado, em cinza 3, a origem e a hora ("pela KARINA · 12:40" ou "pelo Denis · 12:40").
- **Slot de memória**. Círculo de 32 px com o número do slot (1 a 3), fio quando vazio e branco cheio quando ocupado. Ao lado vão o nome, a origem ("IA" ou "Denis", em chip) e a data.
- **Marco da jornada**. Uma linha vertical fina ligando os marcos. Feito: um ponto cheio branco com a data. Planejado: um anel vazio em cinza 2 com "planejado" ou a data prevista. Os dois grupos ficam separados por um rótulo claro, "feito" e "planejado", nunca misturados.
- **Miniatura de tela** (`components.miniatura-tela`). Cartão preto com o título da tela do app e o conteúdo desenhado a partir da leitura: valores por canal com a marca colorida do canal, e mini-curva SVG em Crossover, EQ de entrada, EQ de saída e EQ 15 bandas. No foco ou no hover, o fio sobe para branco a 45%.
- **Tela expandida** (`components.tela-expandida`). Abre sobre o véu, ocupando cerca de 80% da janela, com o título, todos os valores da tela em tabela por canal e a curva SVG grande (escala log de 10 Hz a 22 kHz, cores dos canais, linha de 0 dB). O limiter mostra dB e volts. Botão "fechar" secundário. Veja o segundo quadro em [mockups/bastidores.html](mockups/bastidores.html).
- **Curva SVG** (`components.curva-svg`). Grade em contorno suave, uma linha por canal em 2 px, sem preenchimento e sem brilho. É dado, não decoração.
- **Nota "microfone desligado"** (só nos Bastidores). Um chip de estado sem ponto, em cinza 3. Nunca aparece na Vitrine.

## Do's and Don'ts

| Faça | Não faça |
|---|---|
| Deixe a luz ser a única coisa colorida da Vitrine | Usar o vermelho ou o laranja do app Taramps como cor da marca |
| Mantenha a Vitrine com marca, assinatura, anel, linha da IA e selo, e nada mais | Mostrar número, sigla, código, Hz, dB ou V na Vitrine |
| Use peso 200 contra 500 para a hierarquia | Usar negrito pesado (700+) ou caixa alta corrida |
| KARINA sempre secundária ao BGS Sound Lab, dentro de frase ou como título de região | Dar à KARINA um logo próprio ou pô-la maior que o wordmark |
| Leve o anel a reagir só a som real (microfone) ou ao som da própria abertura | Animar o anel como se ouvisse quando não está ouvindo |
| Bastidores densos, porém pretos, finos e calmos | Fazer os Bastidores parecerem outro app (cartões cinza-claros, sombras, cores de dashboard) |
| Cor de estado sempre com a palavra junto | Usar só a cor para dizer aprovado, recusado ou antiga |
| Cores de canal só em curvas e marcas de canal | Usar as cores de canal em botões, títulos ou fundos |
| Separe "feito" de "planejado" com rótulo e forma diferentes | Desenhar o planejado como se estivesse pronto |
| Cinza 2 só em separadores e anotações grandes | Cinza 2 em texto pequeno (4,1:1) |
