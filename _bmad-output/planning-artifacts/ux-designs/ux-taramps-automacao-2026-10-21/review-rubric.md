# Revisão do par de documentos — taramps-automacao (BGS Controle)

Revisor: rubric walker (`validate.md`, passes 1 e 2), com o item extra 9 (cobertura da 3b) pedido pelo Denis.
Lidos: `DESIGN.md`, `EXPERIENCE.md`, `.memlog.md`, `mockups/key-screens-3b.html`, a seção 4.6 de `sprint-change-proposal-2026-10-23.md`, as fontes do EXPERIENCE.md (`SPEC.md`, `chaves-de-acesso.md`, `porta-de-comandos.md`, `failure-modes.md`, `conhecimento-herdado.md`, `protocolo/MAPA.md`) e `stories.yaml` (3b, 8c, 8d, 10, 11).
Severidade = impacto em quem constrói o núcleo (8c, 8d) e as telas (10, 11). Os `[NOTE FOR UX]` "Em aberto" anteriores à 3b (EQ 15 bandas, link, aviso da IA pausada, fundo do campo ativo) não foram contados como erro.

## Veredito geral

A 3b entrou inteira: as 33 entradas do `.memlog.md` desde "Início da revisão da história 3b" e os 13 itens da seção 4.6 estão nos dois documentos, sem contradizer nenhuma decisão anterior que continua valendo, e os tokens, os componentes novos e o mock da 3b batem entre si. O ponto fraco é o modelo da **conferência do Setup**, que é o coração da 8c: não está dito contra o quê o app confere (a "última aplicação"), como o som é mutado e o que acontece se a conexão cai mutada, nem o que o Início mostra quando o app não sabe qual som está no aparelho; e a camada da **rota** contradiz o `conhecimento-herdado.md`. Vale fechar esses cinco pontos altos antes da 8c; o resto pode ser resolvido junto com as histórias 10 e 11.

## 1. Cobertura dos fluxos — adequado

Conferido: cada CAP e cada teste do sinal de sucesso do `SPEC.md` contra os 7 Key Flows (protagonista, passos numerados, clímax, falha). CAP-4 → Fluxo 1; CAP-12/13 → Fluxo 2; CAP-15 → Fluxo 4; CAP-2/teste 3 → Fluxo 5; CAP-8/11 → Fluxo 6; CAP-16 → Fluxo 7. Todos têm protagonista, passos e clímax.

### Achados
- **high** O Fluxo 2, passo 7, faz a IA "pedir para salvar na memória do celular FILME" e abrir a folha do Salvar na tela. Nem a `porta-de-comandos.md` nem a CAP-12 nem a história 8d têm um comando "salvar" (a 8d acrescenta só "aplicar memória"), e a `chaves-de-acesso.md` diz que criar, renomear e apagar no celular não têm chave. Quem constrói a porta não sabe se esse comando existe, nem como a folha abre enquanto os controles estão travados pela IA (EXPERIENCE.md › Key Flows › Fluxo 2, passo 7). *Fix:* decidir com o Denis: ou a IA não salva (o passo 7 vira "a IA termina; o Denis toca em PARAR IA e depois em Salvar"), ou a porta ganha "pedir para salvar", que só abre a folha na tela, e isso entra na `porta-de-comandos.md` e na 8d.
- **medium** O teste 4 do sinal de sucesso ("com o processador no estado de outro Setup, o app não aplica nem toca antes de confirmar") não tem fluxo marcado como "Teste 4". O Fluxo 7 é a troca pedida pelo usuário (sem dúvida), e o caminho de dúvida aparece só de passagem no Fluxo 5, passo 3, cujo clímax é o teste 3 (EXPERIENCE.md › Key Flows). *Fix:* marcar um fluxo como teste 4 com o clímax "nada tocou antes da escolha" no caminho Setup em dúvida, ou acrescentar uma linha "Teste 4" ao Fluxo 7 com a variante em que o aparelho volta no estado do Carro e o último Setup usado é o Casa 2.1.
- **medium** Fluxo 7, passos 4 e 5: o Setup Carro acabou de nascer "ainda sem memórias". O fluxo não diz onde o Denis cria a ESTRADA, como o preset Rock é aplicado sem memória em uso (o chip de preset fica no cartão da memória, que nesse estado diz "Nenhuma memória"), nem para onde vai uma mudança de caráter se ele tocar em Salvar antes de criar a memória (EXPERIENCE.md › Fluxo 7; State Patterns › Setup sem memórias). *Fix:* escrever os toques do passo 5 (Início › Criar memória do som atual, chip de preset disponível ou não) e dizer na Folha do Salvar o que acontece com o caráter num Setup sem memórias (por exemplo, vira memória nova, como na memória do aparelho).
- **medium** A CAP-14 pede "pelo menos um aviso customizado funcionando no teste 1". O exemplo antigo (sobrescrever memória) saiu, e o Fluxo 1 não nomeia mais nenhum aviso que apareça sempre (Setup em dúvida só aparece se o aparelho perder o estado) (EXPERIENCE.md › Fluxo 1). *Fix:* nomear no Fluxo 1 o aviso que cumpre a CAP-14 (por exemplo, o aviso "Salvo: …" ou o lembrete "Mexer muda o som na hora") e avisar o SPEC de que o exemplo da CAP-14 ficou velho.
- **low** O chip de preset (componente novo da 3b) e a decisão 83 (som do aparelho virando memória do celular) não aparecem em nenhum Key Flow; a decisão 83 só está na tabela "Quem vem do app da Taramps" (EXPERIENCE.md › Key Flows). *Fix:* uma linha no Fluxo 3 trocando o preset pelo chip; a decisão 83 pode virar variante do Fluxo 5.
- **low** O Fluxo 3 não tem a linha "Falha:"; a falha está dentro do passo 1 (EXPERIENCE.md › Fluxo 3). *Fix:* mover para uma linha "Falha:".

## 2. Completude dos tokens — adequado

Conferido por script: as 95 referências `{…}` do DESIGN.md e as 7 do EXPERIENCE.md resolvem todas; os 42 tokens de cor têm hex válido (as transparências em `#RRGGBBAA`, com o equivalente MAUI no comentário); tema único escuro, então não há par claro/escuro. Os componentes novos (`seletor-de-setup`, `chip-de-preset`, `botoes-de-memoria`, `folha-do-salvar`, `selo-herdado`, `faixa-som-mutado`, `lista-o-que-nao-bate`) estão no frontmatter e no corpo com a mesma chave. Tipos e tamanhos do mock da 3b batem com os tokens (seletor 12/15·800, chip 13/13·800, camada 15·700, opção 15·800, campo 17·700, dica 12).

### Achados
- **low** `botoes-de-memoria` não tem token de tipografia: o texto do botão (13 px, peso 400; ativo 600) e as reticências do "…" (20 px, peso 800) só existem no mock (DESIGN.md › frontmatter `components.botoes-de-memoria`; Components › Botões de memória). *Fix:* acrescentar `typography` e `ativo-typography` ao bloco (ou um token `botao-memoria`) e o tamanho do "…".
- **low** Pares novos fora da tabela de contraste: {colors.sobre-escolhido-apoio} sobre {colors.texto} (opção escolhida da folha do Salvar, 6,8:1), {colors.apoio} sobre {colors.palco} (chip de preset e bloco das camadas, 8,2:1) e as cores das saídas sobre {colors.palco} (valores no bloco das camadas, 9,4 / 9,1 / 14,4). Todos passam (DESIGN.md › Colors › tabela). *Fix:* acrescentar as três linhas.
- **low** O comentário de `sobre-escolhido-apoio` ainda cita "MEMÓRIA 1 na aba ativa", aba que saiu na 3b; `colors.nota` ("memória com a IA") não é usado em lugar nenhum (DESIGN.md › frontmatter, linhas 21 e 22). *Fix:* trocar o exemplo para "explicação da opção escolhida na folha do Salvar"; usar ou tirar `nota`.

## 3. Cobertura dos componentes — adequado

Conferido: cada nome de componente nos dois documentos contra DESIGN.md › Components (visual) e EXPERIENCE.md › Component Patterns (comportamento). Seletor de Setup, chip de preset, botões de memória, cartão da memória em uso, cartão de memória, folha do Salvar e selo herdado têm as duas metades, com regras de verdade.

### Achados
- **medium** As listas de escolha da 3b não têm peça de linha: a lista do "…" (celular + DO APARELHO), a lista de Setups aberta por "Escolher o Setup", a lista de presets aberta pelo chip e a tela Setups. O DESIGN.md diz que "Lista de memórias ('…') e Setups" são montadas "só com as peças de Components", mas nenhuma peça é uma linha de escolha (o cartão de memória é da tela Memórias, com Aplicar/Renomear/Apagar). Também não está dito se o chip abre uma folha ou a tela Presets (EXPERIENCE.md diz "troca ali mesmo"; a IA põe Presets como tela cheia) (DESIGN.md › Brand & Style › Telas sem mock; EXPERIENCE.md › Component Patterns › Chip de preset, Lista de memórias; IA › Escolha do Setup, Setups). *Fix:* uma peça "linha de escolha" (altura {spacing.alvo-lista}, nome, detalhe, escolhida em branco ou com marca, selo HERDADO à direita) usada pelas três listas, e dizer que o chip abre uma folha com essa lista.
- **low** `faixa-som-mutado` e `lista-o-que-nao-bate` não têm linha em EXPERIENCE.md › Component Patterns; o comportamento ("só leitura", "rola com os botões fixos") está espalhado em Conectar e Setup e no DESIGN.md (EXPERIENCE.md › Component Patterns). *Fix:* duas linhas na tabela Conexão.

## 4. Cobertura dos estados — fraco

Conferido: cada superfície da IA contra os estados que ela pede (conectando, conferência, dúvida, sem Setup, vazio, aplicando, metade, sem conexão, só leitura, IA ativa). A maior parte está coberta, inclusive os estados novos da 3b (Setup confere, Setup em dúvida, Trocando de Setup, Reaplicando, Seguir sem Setup, Aplicando memória, Carregando memória do aparelho, Salvando, Preset aplicado, Setup sem memórias, Apagar memória/Setup). As lacunas estão todas na conferência do Setup, que a 8c implementa.

### Achados
- **high** A referência da conferência não está definida. O documento diz "o estado lido bate com a última aplicação do último Setup", mas não diz o que é guardado como "última aplicação", quando ela é atualizada nem quais parâmetros entram na comparação. As passagens pedem regras diferentes: no Fluxo 3, passo 8, o Denis sobe o sub e não salva — se a referência for a última memória aplicada, a próxima conexão abre o Setup em dúvida com o som mutado; no Fluxo 1, passos 6 a 8, a MUSICA ORIG foi criada do som atual, sem "aplicação", e o clímax espera "o estado bate"; a decisão 83 diz que manter ou atualizar a calibração faz "a próxima conexão conferir sem dúvida", o que sugere comparar só a calibração. A primeira conexão também não tem referência: o Casa 2.1 herdado nunca foi aplicado pelo app (EXPERIENCE.md › Conectar e Setup; State Patterns › Setup confere, Primeira abertura; Fluxo 1 passo 8; Fluxo 3 passo 8; Quem vem do app da Taramps, última linha). *Fix:* uma regra curta, decidida com o Denis: a referência é o último estado que o app deixou no aparelho (atualizada a cada escrita confirmada) ou só a calibração do Setup (+ caráter da memória ativa); que parâmetros entram; e, na primeira conexão, abrir sempre a escolha do Setup (o Casa 2.1 herdado como opção).
- **high** Mutar não está definido, nem o que acontece se a tela de dúvida for interrompida. Não se diz como o app muta (MASTER, remote out `252`, outro), se o mudo é uma escrita no aparelho, o que acontece se a conexão cair ou o app fechar com o som mutado (o processador pode ficar mudo sem o celular, e "Seguir sem Setup" promete "deixar o aparelho como está"), nem se a porta da IA fica fechada enquanto o Setup está em dúvida ("nada toca nem é aplicado antes da escolha") (EXPERIENCE.md › Conectar e Setup; State Patterns › Setup em dúvida, Trocando de Setup; Interaction Primitives › "Nada toca antes de conferir o Setup"; Uso junto com a IA). *Fix:* nomear o mecanismo do mudo (decisão de arquitetura para a 8c) e acrescentar o estado "Dúvida interrompida": ao reconectar, a conferência recomeça e o som segue mudo; ao fechar o app, o app desmuta antes ou avisa. Dizer que a IA recebe "Setup em dúvida" e só pode ler.
- **high** O app não sabe qual memória do aparelho está tocando quando não foi ele que carregou. O `MAPA.md` só tem `241` (carregar) e os nomes; não há leitura de "memória atual". Mesmo assim, o Fluxo 5, passo 3, diz que depois de Seguir sem Setup "o cartão mostra Memória 1 do aparelho (app da Taramps)". O conteúdo do Início nesse caso (nome no cartão, chip de preset, botão escolhido, Player, subtítulo do Ajuste) não está definido (EXPERIENCE.md › Fluxo 5 passo 3; State Patterns › Seguir sem Setup; Component Patterns › Cartão da memória em uso). *Fix:* um estado "Som do aparelho, sem memória" (por exemplo, cartão "Som do aparelho" em {typography.memoria-hero}, sem chip, nenhum botão escolhido, Player em "Sem playlist") e corrigir o Fluxo 5, passo 3.
- **medium** O Salvar não tem estado "nada mudou" nem sinal de mudança não salva, e a base do "antes → depois" da folha não está dita (diferença contra as camadas guardadas: Setup, memória ativa, preset lembrado). Aplicar outra memória descarta as mudanças sem aviso, o que o Fluxo 3, passo 8, aceita, mas nada na tela mostra que havia mudança pendente (EXPERIENCE.md › Component Patterns › Botão principal "Salvar", Folha do Salvar). *Fix:* Salvar travado (ou folha "Nada para salvar") sem diferença; dizer que a folha compara o estado atual com as três camadas guardadas.
- **medium** Faltam regras de apagar que a 8c e a 8d precisam: apagar um preset que outras memórias lembram (o que elas passam a lembrar?), apagar o Cinema herdado (pode?), e apagar uma memória que ocupa um dos 3 botões (a vaga fica vazia ou entra a próxima usada?) (EXPERIENCE.md › State Patterns › Apagar preset, Apagar memória; Component Patterns › Botões de memória). *Fix:* três linhas: o preset apagado vira "Sem preset" nas memórias que o lembravam, ou o apagar fica travado com "Em uso por N memórias"; o herdado se apaga ou não; a vaga é ocupada pela próxima mais usada.
- **medium** Nome longo e limites de nome: memórias, Setups e presets agora têm nome livre, e "MUSICA ORIG" em {typography.memoria-hero} (60 px) passa da largura útil do cartão (cerca de 324 px no quadro de 390). Não há regra de quebra ou redução, nem limite de caracteres (o nome do produto tem 12) (DESIGN.md › Components › Cartão da memória em uso; EXPERIENCE.md › Cartão de memória). *Fix:* limite de caracteres por nome e a regra do hero (reduz até {typography.memoria-lista} ou quebra em duas linhas; nunca corta).

## 5. Cobertura das referências visuais — forte

Conferido: `mockups/` (direction-sintese-v2.html, key-screens.html, key-screens-3b.html) e `imports/` (escolha-1 a 9, referencia-taramps-audio.png). Todos estão ligados no lugar certo, com o que ilustram (o mock da 3b por quadro: Início, Setup em dúvida, folha do Salvar). "Os documentos ganham dos mocks" está dito em cada documento, com a lista de conflitos conhecidos, inclusive os da 3b (abas e "Salvar na memória N" saíram; primeiro botão mais largo). Sem órfãos.

### Achados
- **low** O comentário no topo de `mockups/key-screens-3b.html` diz que "o spine ainda é o de antes da 3b", o que deixou de ser verdade (mockups/key-screens-3b.html, linhas 13 e 14). *Fix:* atualizar a frase.

## 6. Excesso e sobre-especificação — adequado

### Achados
- **medium** A regra "memória do aparelho em uso → Salvar cria memória do celular e pergunta da calibração" está escrita em cinco lugares do EXPERIENCE.md (Component Patterns › Folha do Salvar e Cartão de memória; State Patterns › Carregando memória do aparelho e Salvando; Interaction Primitives; Quem vem do app da Taramps) e mais no DESIGN.md, com redações um pouco diferentes. A célula da Folha do Salvar tem cerca de 250 palavras com oito suposições. Uma mudança futura vai atualizar um lugar e esquecer outro (EXPERIENCE.md › Component Patterns › Folha do Salvar). *Fix:* uma subseção "Salvar por camadas" (na IA, como Conectar e Setup), com uma tabela caso → camadas → perguntas da folha; as outras passagens apontam para ela.
- **low** O Setup em dúvida aparece em quatro passagens (EXPERIENCE.md › Conectar e Setup, State Patterns × 2; DESIGN.md › Layout & Spacing e Components), e as listas de conflitos com os mocks são quase iguais nos dois documentos. *Fix:* deixar o texto completo em Conectar e Setup e só o ponteiro nos estados.

## 7. Disciplina de herança — fraco

Conferido: `sources` resolvem (os seis arquivos do EXPERIENCE.md e o DESIGN.md da família, `../ux-taramps-automacao-2026-10-16/DESIGN.md`). Os nomes das CAPs e dos testes batem com o SPEC. Os nomes dos componentes são iguais nos dois documentos. As referências de token do EXPERIENCE.md resolvem no DESIGN.md.

### Achados
- **high** A camada da rota contradiz uma fonte. O `conhecimento-herdado.md` (fonte do EXPERIENCE.md) põe "rotas 1 = B e 2 = A" na calibração do Setup Casa 2.1; o SPEC (CAP-16) e o EXPERIENCE.md (Foundation; Folha do Salvar) dizem que a calibração é "atraso, fase, corte, equilíbrio L/R" e que "o resto" vai para o caráter da memória. A 8c divide a FILME herdada e a folha do Salvar separa as camadas: com as regras de hoje, a rota cai em lugares diferentes. Link 1–2/3–4, MASTER e EQ de entrada também não têm camada dita (EXPERIENCE.md › Foundation, Component Patterns › Folha do Salvar; `conhecimento-herdado.md`, linha 39). *Fix:* uma tabela parâmetro → camada para todo o MAPA §4 (rota, link, MASTER, EQ de entrada, mute, remote out, Bluetooth de áudio fora das camadas), decidida com o Denis, e alinhar o SPEC ou o `conhecimento-herdado.md`.
- **medium** Valor mostrado e valor guardado com o equilíbrio: o giratório e o limiar mostram o valor final (memória + equilíbrio), mas a folha do Salvar não diz se o "antes → depois" do caráter mostra o valor final ou o da memória (na JBL R, +8,0 na tela é +11,0 na memória). Também não diz o que acontece quando a soma sai da faixa (ganho −45,0 a +15,0; limitador −48,0 a 0,0 ou OFF) (EXPERIENCE.md › Ajuste da saída › Equilíbrio L/R do Setup; Component Patterns › Folha do Salvar). *Fix:* a folha mostra o valor final da tela; a soma é limitada à faixa do MAPA, e o Ajuste avisa "No limite da faixa com o equilíbrio do Setup".
- **low** As fontes não citam a proposta de 23/10 (`sprint-change-proposal-2026-10-23.md`), de onde vem a lista revisada na 3b (EXPERIENCE.md, frontmatter `sources`). *Fix:* acrescentar.
- **low** "Memória ativa" (do Setup, a que é reaplicada) e "memória em uso" aparecem como sinônimos sem definição (EXPERIENCE.md › Conectar e Setup; State Patterns › Reaplicando a memória ativa). *Fix:* usar só "memória em uso" ou definir "memória ativa do Setup = a última em uso nele".

## 8. Encaixe na forma — forte

DESIGN.md na ordem canônica (Brand & Style → Colors → Typography → Layout & Spacing → Elevation & Depth → Shapes → Components → Do's and Don'ts). EXPERIENCE.md com todos os padrões (Foundation, IA, Voice and Tone, Component Patterns, State Patterns, Interaction Primitives, Accessibility Floor, Key Flows) e Inspiration presente (há direções recusadas). Responsive omitido com razão (uma superfície, retrato travado). As seções inventadas se pagam: "Uso junto com a IA" segura as travas da porta, e "Quem vem do app da Taramps" foi pedida na 4.6.

### Achados
- **low** "Ajuste da saída", "Conectar e Setup" e "Player" são regras de comportamento dentro da Information Architecture; quem procura comportamento olha em Component Patterns ou State Patterns (EXPERIENCE.md › Information Architecture). *Fix:* deixar como está ou subir para seções próprias ("Superfícies com regra própria").

## 9. Cobertura da 3b — adequado

Conferido: cada entrada do `.memlog.md` da linha 50 ("Início da revisão da história 3b") até o fim (linha 83) e cada item da seção 4.6 da proposta de 23/10, contra os dois documentos e o mock da 3b; depois, cada decisão anterior que continua valendo contra o texto novo.

**Resultado:** todas as decisões e todos os itens entraram. Nenhuma decisão da 3b contradiz outra decisão que continua valendo: as que foram trocadas estão marcadas como trocadas (58 → 60; a parte do Início da suposição 71 → 80; "Salvar aqui" e a folha de sobrescrever da suposição 45 → um só Salvar). As contradições encontradas são com as **fontes**, não com o memlog: rota (seção 7), comando "salvar" da IA (seção 1) e "Memória 1 do aparelho" no Fluxo 5 (seção 4).

### Decisões do `.memlog.md` (linhas 51 a 83)

| Linha | Decisão | Onde está | Situação |
|---|---|---|---|
| 51 | Até 4 memórias por Setup no dia a dia; quantidade livre | EXP Foundation (três camadas) | entrou |
| 52 | Um toque para as mais usadas, dois para as outras | EXP Component Patterns › Botões de memória | entrou |
| 53 | O app decide quem fica à vista: as usadas por último no Setup | EXP Botões de memória | entrou |
| 54 | 3 botões (contando a em uso) + botão da lista | EXP Botões de memória; DESIGN `botoes-de-memoria`; mock quadro 1 | entrou |
| 55 | Os 3 não mudam de lugar; entra no lugar da usada há mais tempo | EXP Botões de memória; Fluxo 1 passo 6 | entrou (falta a vaga ao apagar, ver 4) |
| 56 | Memórias do aparelho nunca nos 3 botões | EXP Botões de memória, Lista de memórias | entrou |
| 57 | Com memória do aparelho: cartão diz "Memória N do aparelho", nenhum botão escolhido | EXP Cartão da memória em uso; DESIGN Cartão da memória em uso, Botões de memória | entrou, com divergência pequena (achado abaixo) |
| 58 | Setup só ao conectar | trocada pela 60 | coerente |
| 59 | Bate → entra direto; não bate → pergunta | EXP Conectar e Setup; State Setup confere | entrou (falta definir "bate", ver 4) |
| 60 | Tocar no nome do Setup volta à escolha e à conferência | EXP Seletor de Setup, Trocando de Setup; Fluxo 7 | entrou no EXP; o DESIGN não tem a variante (achado abaixo) |
| 61 | Preset em uso no Início; tocar troca ali (chip) | EXP Chip de preset; DESIGN `chip-de-preset`; mock quadro 1 | entrou (forma da lista em aberto, ver 3) |
| 62 | Memória lembra o último preset e o traz junto | EXP Chip de preset, Botões de memória, Aplicando memória | entrou |
| 63 | Arrumo do Início (Setup no cabeçalho, chip no cartão, 3 botões + "…", saídas) | DESIGN Layout › Início, Topo do app; EXP IA › Início; mock quadro 1 | entrou |
| 64 | Salvar continua um passo à parte | EXP Interaction Primitives; Voice and Tone | entrou |
| 65 | Um só Salvar; cada mudança na camada dona; a folha diz o que vai para onde | EXP Folha do Salvar; DESIGN `folha-do-salvar`; mock quadro 3 | entrou |
| 66 | 15 bandas: atualizar o preset (com aviso) ou criar novo | EXP Folha do Salvar; Voice and Tone | entrou |
| 67 | Herdado não se sobrescreve; só criar novo | EXP Folha do Salvar; DESIGN opção travada; mock quadro 3 | entrou |
| 68 | Memória nova começa do som atual | EXP Cartão de memória; Fluxo 1 passo 6 | entrou |
| 69 | Memória nova: playlist vazia, preset em uso; apagar com folha; em uso não se apaga | EXP Cartão de memória; State Apagar memória | entrou |
| 70 | Player só com a playlist da memória em uso; sem abas | EXP Player; DESIGN Layout › Player, Controles do player | entrou |
| 71 | Selo HERDADO no Casa 2.1, FILME e Cinema; tocar mostra a nota | EXP Selo herdado; DESIGN `selo-herdado` | entrou ("mostra a nota" sem forma, achado abaixo) |
| 72 | Tela Setup em dúvida (mutado, lista, três botões) | EXP Conectar e Setup; DESIGN Layout, Faixa do som mutado, Lista do que não bate; mock quadro 2 | entrou |
| 73 | Fluxo "Levar o processador para o carro" | EXP Fluxo 7 | entrou (passos 4–5 incompletos, ver 1) |
| 74 | Fluxos 1 e 4 refeitos | EXP Fluxos 1 e 4 | entrou |
| 75 | Recomendação da IA primeiro nas perguntas | regra de processo da conversa | não se aplica aos documentos |
| 76 | Mesa usa os botões do Início; "Salvar" fixo; aplicar × carregar | EXP IA › Mesa, Botões de memória, Salvar, Interaction Primitives; DESIGN "De onde veio" | entrou (lugar na Mesa em aberto, achado abaixo) |
| 77 | Desenhos: Início, Setup em dúvida, folha do Salvar | mock key-screens-3b.html, quadros 1 a 3 | entrou |
| 78 | Spec atualizada (CAP-4, 5, 16; regra 5; 8c e 8d) | conferido nos arquivos | coerente com os documentos |
| 79 | Revisão de cobertura | esta revisão | — |
| 80 | Selo não aparece no Início | EXP Selo herdado, Cartão da memória em uso, IA › Início, Primeira abertura; DESIGN Selo herdado | entrou |
| 81 | Escolhas do mock (cabeçalho em duas linhas, sai "Memória N · Master", "…" de 44 px, dúvida sem ‹/engrenagem/barra) | DESIGN Topo do app, Botões de memória, Cartão da memória em uso, Layout › Setup em dúvida | entrou |
| 82 | Equilíbrio L/R no Avançado da JBL L/R; Salvar leva ao Setup | EXP Ajuste da saída; tabela À vista/Avançado; DESIGN Avançado | entrou (valor final × guardado, ver 7) |
| 83 | Som do aparelho vira memória do celular; a folha pergunta da calibração | EXP Folha do Salvar, Carregando memória do aparelho, Quem vem; DESIGN Folha do Salvar | entrou (aviso de alcance, achado abaixo) |

### Itens da seção 4.6

| Item | Onde está | Situação |
|---|---|---|
| IA: Setups novo | EXP IA › Escolha do Setup, Setups | entrou |
| IA: Memórias = celular + "Do aparelho" | EXP IA › Memórias, Lista de memórias | entrou |
| IA: Presets = 15 bandas | EXP IA › Presets no celular; Foundation | entrou |
| Conectar com escolha e conferência do Setup | EXP Conectar e Setup; State Patterns | entrou |
| Componente Memória em quantidade livre, salvando no celular | EXP Cartão de memória, Botões de memória, Salvar | entrou |
| Player preso à memória do celular | EXP Player; Fluxo 4 | entrou |
| Selo herdado (Setup, FILME, Cinema) | EXP Selo herdado; DESIGN `selo-herdado` | entrou |
| Fluxos 1 e 4 refeitos | EXP Key Flows | entrou |
| Fluxo novo do carro | EXP Fluxo 7 | entrou |
| Estados: aplicando, aplicado pela metade, Setup em dúvida | EXP State Patterns | entrou |
| "Quem vem do app da Taramps" | EXP seção própria | entrou |
| DESIGN: botões e abas de memória, cartão de memória, cartão da memória em uso | DESIGN `botoes-de-memoria`, `cartao-de-memoria`, `cartao-memoria-em-uso` | entrou (abas tiradas) |
| DESIGN: seletor de Setup e chip de preset | DESIGN `seletor-de-setup`, `chip-de-preset` | entrou |
| Primeira pergunta (os 3 botões deixam de caber) | resolvida pelas decisões 51 a 56 | entrou |

### Achados
- **medium** A variante "Trocando de Setup" (decisão 60) só existe no EXPERIENCE.md: lá a tela ganha ‹ e, sem diferença, diz "O aparelho está no Casa 2.1.". O DESIGN.md descreve a tela só "sem barra, ‹ e engrenagem" e com o título "Setup em dúvida", que fica errado quando não há dúvida (DESIGN.md › Layout & Spacing › Setup em dúvida; Components › Topo do app; EXPERIENCE.md › State Patterns › Trocando de Setup). *Fix:* no DESIGN.md, a variante com o cabeçalho de tela cheia (‹ + título "Trocar de Setup"), sem a lista quando não há diferença.
- **medium** A Mesa perdeu as abas de memória (decisão 76), mas o layout da Mesa não diz onde ficam os botões de memória (em cima dos canais? com qual rótulo?) nem o que a Mesa mostra com uma memória do aparelho em uso, já que ela não tem o cartão da memória (DESIGN.md › Layout & Spacing › Mesa; EXPERIENCE.md › IA › Mesa). *Fix:* uma linha no layout da Mesa: botões de memória no topo, com o rótulo MEMÓRIA EM USO e o nome lido quando nenhum botão está escolhido.
- **medium** "Atualizar o Casa 2.1 com a do aparelho" (decisão 83) muda a calibração de todas as memórias do Setup, mas a folha não avisa o alcance, ao contrário do preset ("Vale para toda memória que usa ele", decisão 66) (EXPERIENCE.md › Component Patterns › Folha do Salvar). *Fix:* a explicação da opção diz "Vale para todas as memórias do Casa 2.1."
- **low** Memória do aparelho no cartão: o DESIGN.md põe o nome lido em {typography.memoria-hero} e, embaixo, a linha "Memória 2 do aparelho (app da Taramps)"; o EXPERIENCE.md e o Fluxo 5 falam só da linha (DESIGN.md › Components › Cartão da memória em uso; EXPERIENCE.md › Cartão da memória em uso). *Fix:* escrever a mesma anatomia nos dois.
- **low** "Tocar no selo mostra a nota herdada" (suposição 71) não tem forma (folha? linha que abre?) (EXPERIENCE.md › Selo herdado). *Fix:* "abre uma folha com a nota de `conhecimento-herdado.md`, só leitura".

## Notas mecânicas

- Referências de token: 95 no DESIGN.md e 7 no EXPERIENCE.md, todas resolvem. Chaves de componente do corpo (`(…)`) iguais às do frontmatter, nos dois sentidos.
- Tokens sem uso no corpo: `colors.nota`, `rounded.xs`, `spacing.1` e `spacing.3` a `spacing.9` (a escala numérica existe, mas o texto usa os nomes `gap-*`, `alvo-*`). Não é erro; vale saber.
- Frontmatter completo nos dois (`name`, `status: final`, `updated: 2026-10-23`, `sources`). Sem Mermaid.
- `sources` do EXPERIENCE.md resolvem a partir da pasta do UX (`../../../specs/…`, `../../../../protocolo/MAPA.md`).
- Comentário do mock da 3b e do token `sobre-escolhido-apoio` desatualizados (ver 2 e 5).
- Contagem: 0 critical, 5 high, 12 medium, 13 low.
