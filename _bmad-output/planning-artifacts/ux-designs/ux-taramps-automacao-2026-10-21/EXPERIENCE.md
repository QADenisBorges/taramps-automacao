---
name: BGS Controle
status: final
updated: 2026-10-21
sources:
  - ../../../specs/spec-app-controle-taramps/SPEC.md
  - ../../../specs/spec-app-controle-taramps/chaves-de-acesso.md
  - ../../../specs/spec-app-controle-taramps/porta-de-comandos.md
  - ../../../specs/spec-app-controle-taramps/failure-modes.md
  - ../../../specs/spec-app-controle-taramps/conhecimento-herdado.md
  - ../../../../protocolo/MAPA.md
  - DESIGN.md
---

# BGS Controle — Experiência

Os mocks [`mockups/direction-sintese-v2.html`](mockups/direction-sintese-v2.html) e [`mockups/key-screens.html`](mockups/key-screens.html) ilustram este documento. **Em qualquer conflito, este documento e o DESIGN.md ganham dos mocks.** Conflitos já conhecidos:

- A barra tem quatro pílulas, **Início · Mesa · Áudio · Player**. A v2 mostra só Início e Mesa, e as telas-chave mostram três.
- Nas frequências, − e + andam **1/12 de oitava**, não 1 Hz como na v2.
- No Ajuste da saída, **atraso, fase e EQ de saída ficam em Avançado**. A v2 mostra esses controles abertos.
- A Mesa ganhou a **faixa de monitoração** (voltímetro e clip), que os mocks não têm.
- A chave da IA se chama **"IA pela rede"** em todo lugar. O mock de Chaves diz "Porta de comandos da IA".
- Na IA ativa, o comportamento do toque é **configurável**. O padrão é o do mock: controles travados até PARAR IA.
- Cores e tamanhos mínimos mudaram no DESIGN.md: {colors.apagado} subiu e nenhum texto fica abaixo de 11 px.

Em aberto (resolvido nas histórias de tela 10 e 11): layout de edição do EQ 15 bandas; link 1–2/3–4 nas abas e no Ajuste; aviso da IA pausada em outra tela; fundo do campo ativo por saída.

Origem e histórico das suposições: `.memlog.md`.

## Foundation

- **Plataforma:** Android, app em **.NET MAUI** (projeto `BgsControle.App`, com os ViewModels em `BgsControle.Telas`). Sem iOS.
- **Forma:** celular em **retrato**, uma tela só. [ASSUMPTION] A rotação fica travada em retrato.
- **Quem usa:** uma pessoa, o **Denis**, no próprio celular, perto do som. A IA (Claude Code no notebook) é uma segunda origem de comandos, não um usuário da tela.
- **Aparelho:** PRO 2.4BT + AUDIO, firmware R003, uma conexão BLE por vez. A música do Player sai do celular pelo Bluetooth de áudio do processador.
- **Sistema de interface:** nenhum de terceiros. Navegação, teclado, seletor de arquivos e botão Voltar seguem o Android; o visual vem todo do [`DESIGN.md`](DESIGN.md), que é a referência de identidade.
- **Tema:** só escuro. Fonte Roboto variável embutida; os tamanhos acompanham a escala de fonte do Android.

## Information Architecture

Barra inferior com quatro destinos: **Início · Mesa · Áudio · Player**. A **engrenagem** no topo abre **Configurações**. Tudo o que não está na barra abre em **tela cheia com ‹** e sem a barra. Folhas de confirmação sobem por cima da tela atual, uma de cada vez.

| Superfície | Chega por | Para quê | Mock |
|---|---|---|---|
| **Início** | Abrir o app; barra | Ler de longe a memória em uso e as três saídas; trocar de memória; Conectar quando desconectado | v2 quadro 1; telas-chave 6a |
| **Mesa** | Barra | Ganho de cada saída e MASTER com faders; trocar de memória pelas abas; voltímetro e clip por canal na faixa de monitoração | v2 quadro 2 (sem a faixa) |
| **Áudio** | Barra | Grade 2 × 5 por função, na ordem do app da Taramps | telas-chave 1 |
| **Player** | Barra | Playlist da memória: tocar músicas do celular pelo Bluetooth até o processador | só este documento |
| **Ajuste da saída** (JBL L, JBL R, Sub) | "Ajustar ›" no cartão de saída (Início) ou "Ajustar" no canal (Mesa) | Os ajustes de uma saída numa tela | v2 quadros 3, 3b, 4 (com a divisão antiga) |
| **Telas de função** (Roteamento, EQ 15 bandas, EQ de entrada, Crossover, Alinhamento, Fase, Limitador, Nível das saídas, EQ de saída) | Bloco da grade Áudio | Uma função para as três saídas, com abas de saída [ASSUMPTION] | telas-chave 2a, 2b (Crossover); as outras, só este documento |
| **Gerador** | Bloco Gerador da grade Áudio | Tom fixo e sweep, com prazo de 30 s | só este documento |
| **Configurações** | Engrenagem | Ponto de entrada familiar para quem vem do app da Taramps | telas-chave 3 (sem a linha do voltímetro) |
| **Memórias** | Configurações › Memórias | Carregar, salvar e renomear as 3 memórias do DSP; ver o que é herdado | telas-chave 4a, 4b |
| **Presets no celular** | Configurações › Presets | Guardar, aplicar, renomear e apagar presets próprios; presets herdados | só este documento |
| **Aparelho** (grupo dentro de Configurações) | Configurações | Remote out, Bluetooth de áudio, nome do produto, senha, Voltímetro e clip; resets em Avançado | telas-chave 3 |
| **Voltímetro e clip** | Configurações › Aparelho › Voltímetro e clip; tocar na faixa de monitoração da Mesa | Tensão e clip das saídas e da entrada ao vivo, maiores | só este documento |
| **Chaves de acesso** | Configurações › Chaves de acesso | Ligar e desligar as 11 chaves, com aviso do risco | telas-chave 5a, 5b |
| **IA pela rede** | Configurações › IA pela rede | Chave da IA, código de pareamento de 6 dígitos, cliente pareado, revogar, e o comportamento do toque durante a IA | só este documento |
| **IA ativa** | Abre sozinha quando a IA começa uma tarefa | Ver ao vivo o que a IA mexe; PARAR IA | v2 quadro 5 |
| **Histórico** | Configurações › Histórico | Cada comando, da tela e da IA, com origem, hora e valor | só este documento (o recorte "O que a IA fez" é o modelo) |
| **Conectar e senha** | Início desconectado → Conectar | Conectar, digitar a senha, ver a recusa | telas-chave 6a, 6b |

Regras de navegação:

- O botão Voltar do Android faz o mesmo que ‹. [ASSUMPTION]
- A IA ativa não tem barra inferior nem ‹ [ASSUMPTION]; ela fecha ao parar e volta para a tela de antes.

### O que fica à vista e o que vai para Avançado

Regra do Denis: em toda tela de parâmetros, as funções principais e as mais mexidas ficam à vista; o resto fica em **Avançado**, recolhido.

| Tela | À vista | Em Avançado |
|---|---|---|
| Ajuste da saída | Ganho (giratório), passa-alta (frequência + tipo), passa-baixa (frequência + tipo), limitador (limiar + OFF) | Atraso, fase, EQ de saída (frequência, ganho, Q), ataque e liberação do limitador; [ASSUMPTION] Auto do limitador e mute |
| Crossover | Por saída: passa-alta e passa-baixa (frequência + tipo) | Link 1–2 e Link 3–4 |
| Limitador | Limiar com OFF | Ataque e liberação; [ASSUMPTION] Auto |
| Alinhamento | Atraso de cada saída (é a função da tela) | — |
| Fase | 0° / 180° de cada saída (é a função da tela) | — |
| EQ de saída | Frequência, ganho e Q (é a função da tela) | — |
| Nível das saídas | Ganho de cada saída | [ASSUMPTION] Mute |
| Roteamento | Rota de cada saída (A, B, A+B) | — |
| EQ 15 bandas | As 15 bandas | [ASSUMPTION] Preset de EQ (Flat, Loudness…, Custom) |
| EQ de entrada | Frequência, ganho, Q (um só, sem saídas) | — |
| Gerador | [ASSUMPTION] Tom fixo: frequência, ganho, Tocar | [ASSUMPTION] Sweep: inicial, final, ganho, velocidade |
| Configurações | Memórias, Presets, Aparelho, Chaves, IA pela rede, Histórico | Reset de áudio, Reset de fábrica |

O padrão das telas de função é o mesmo do Ajuste: o que dá nome à tela fica à vista; o que é ajuste fino daquele parâmetro vai para Avançado.

[NOTE FOR UX] O layout de edição da tela EQ 15 bandas ainda não foi desenhado; o painel ao vivo da IA só mostra a leitura.

### Player

A pílula **Player** abre a playlist da **memória em uso**. **Cada memória (SPL, MUSICA ORIG, FILME) tem a sua playlist** (decidido: uma playlist por memória, não uma playlist única), montada com arquivos de áudio do celular. O app toca e o som vai pelo Bluetooth de áudio até o processador.

- **Cabeçalho:** as abas de memória. A da memória em uso vem escolhida. Tocar em outra aba mostra e edita a playlist daquela memória, mas **não carrega a memória**. [ASSUMPTION]
- **Lista:** as faixas da playlist, na ordem. A que toca fica marcada.
- **Adicionar músicas:** abre o seletor de arquivos do Android, com vários arquivos de áudio de uma vez. As escolhidas entram no fim da lista. [ASSUMPTION] O app guarda o caminho do arquivo; ele não copia a música.
- **Editar:** mostra − para remover e ≡ para arrastar e ordenar. **Pronto** sai do modo.
- **Controles fixos embaixo:** tocar/pausar e próxima.
- **Trocar de memória com música tocando:** [ASSUMPTION] a música continua; a playlist da nova memória entra quando a faixa atual acabar.
- **Segundo plano:** [ASSUMPTION] a música continua com o app em segundo plano, com tocar/pausar e próxima na notificação do Android.

## Voice and Tone

Esta seção trata dos textos da tela (microcopy); a voz da marca está em DESIGN.md.Brand & Style.

Curto, direto, um passo por vez, dizendo o que conferir. Em português do Brasil, com vírgula decimal, sinal de menos verdadeiro (−) e a unidade sempre ao lado.

Os textos abaixo ilustram o padrão; o texto final de cada estado está em State Patterns.

| Do | Don't |
|---|---|
| "Gire o botão ou toque em − e +" | "Use os controles para ajustar o parâmetro desejado" |
| "Toque no número para escrever" | Campo sem dica |
| "Mexer muda o som **na hora**. Para guardar, toque em **Salvar na memória 1**; é um passo à parte." | "Alterações não salvas!" |
| "Escrevendo o ganho. Toque em ✓ para aplicar. Até lá, o som continua em +2,6 dB." | Aplicar sem dizer quando |
| "Salvar sobre a MUSICA ORIG?" + "**A versão atual some.**" | "Tem certeza?" |
| "Ligar a chave do Gerador?" + o risco escrito da spec | "Atenção: operação perigosa" |
| "Senha errada. Digite de novo (tentativa 2 de 3)" | "Erro de autenticação" |
| "Desconectado. Ligue o som e fique perto dele." | "Falha na conexão BLE" |
| "Controles travados enquanto a IA trabalha" | Controle travado sem dizer por quê |
| "IA ajustando o EQ da FILME" | "Processando…" |
| "A memória 2 foi salva." / "A memória 2 não foi salva. Toque em Salvar de novo." | Silêncio depois de uma queda |
| "Playlist da FILME vazia. Toque em Adicionar músicas." | "Nenhum item" |
| "O Bluetooth de áudio do processador está desligado." + Ligar | "Erro de reprodução" |
| Nomes por extenso: Passa-alta, Passa-baixa, Limitador, Atraso | HPF, LPF, Limiter, Delay na tela (PA e PB só nos resumos, com a legenda "PA passa-alta · PB passa-baixa") |
| Botão diz o que faz: "Salvar sobre a MUSICA ORIG", "Deixar desligada" | "OK", "Sim", "Não" |
| Valor fora da faixa: "Fora da faixa: −45,0 a +15,0 dB" [ASSUMPTION na redação] | Recusar sem dizer a faixa |

## Component Patterns

Esta seção trata do comportamento; o visual de cada peça está em DESIGN.md.Components, com o mesmo nome.

### Navegação

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Barra inferior** | Início, Mesa, Áudio, Player | Troca de destino sem perder o estado de cada um. Some nas telas cheias e na IA ativa. |
| **Engrenagem** | Topo do app | [ASSUMPTION] Abre Configurações em tela cheia, com ‹. |
| **Cabeçalho de tela cheia** | Telas cheias | ‹ volta. O subtítulo diz a rota e a memória em uso ("Saídas 3+4 em ponte · rota A+B · memória 1"). [ASSUMPTION] Fica fixo no topo; o meio da tela rola. |
| **Cartão de saída** | Início | Tocar no cartão ou em "Ajustar ›" abre o Ajuste daquela saída. |
| **Bloco de função** | Áudio | Abre a tela da função. Travado (chave desligada): [ASSUMPTION] tocar abre Chaves de acesso. |
| **Abas de saída** | Telas de função | [ASSUMPTION] Escolhem qual saída se mexe; o resumo de cada aba se atualiza ao vivo. Não recolhem. |
| **Avançado** | Telas de parâmetros, Configurações | Vem recolhido; tocar abre e fecha. [ASSUMPTION] Lembra se estava aberto enquanto o app estiver aberto. |
| **Linha de configuração** | Configurações | › abre tela cheia; par de botões aplica na linha; campo do nome vale no ✓; botões travados mostram por quê. |
| **Linha de chave** | Chaves de acesso | Ver Par de botões. A IA nunca muda uma chave. |

### Controles de valor

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Canal da mesa** | Mesa | Arrastar a capa muda o ganho na hora; tocar no número vira campo; "Ajustar" abre o Ajuste da saída. O ponto de clip no topo acende quando aquela saída clipa. |
| **MASTER** | Mesa | Mesmo comportamento do canal, de −80,0 a 0,0 dB, passo 0,1. |
| **Chips da Mesa** | Mesa | [ASSUMPTION] "Remote out" é só leitura (muda em Configurações); "EQ gráfico" abre a tela EQ 15 bandas. |
| **Capa do fader** | Faders | Arrastar a capa é o mesmo que arrastar o fader; tocar no trilho não pula o valor. [ASSUMPTION] |
| **Faixa de monitoração** | Mesa | Voltímetro e um ponto de clip por saída e outro para a entrada, ao vivo. [ASSUMPTION] O clip fica aceso 3 s depois do último aviso do aparelho; o Sub acende se o canal 3 ou o 4 clipar. Tocar na faixa abre Voltímetro e clip. |
| **Botão giratório** | Ajuste da saída (ganho) | Arrastar em arco gira o valor; [ASSUMPTION] o arco mostra a posição real dentro de −45,0 a +15,0 dB. Tocar no número central vira campo (com teclado); [ASSUMPTION] o número tem sublinhado tracejado e a dica "Toque no número para escrever". − e + grandes dão o passo de 0,1 dB. |
| **Controle de parâmetro** (fader + campo + − e +) | Ajuste e telas de função | **Tudo já aberto e mexível**, sem tocar para entrar em edição. Três formas no mesmo controle: arrastar o fader, escrever no campo, tocar em − e +. A linha de cima diz a faixa e o passo. |
| **Botões − e +** | Giratório, controles de parâmetro | Um toque dá um passo. **Segurar repete o passo.** [ASSUMPTION] A repetição começa depois de 0,4 s e acelera depois de 2 s; o envio ao aparelho segue o Ritmo de envio (Interaction Primitives). |
| **Campo numérico** | Todo valor | Tocar abre o teclado numérico do Android com ✓. O valor só vale no ✓; até lá o som não muda. Fora da faixa: recusado, com a faixa escrita ao lado, e o valor anterior volta. [ASSUMPTION] Fechar o teclado sem ✓ descarta. |
| **Teclado** | Campos | O do Android: numérico com ✓ para valores, alfanumérico para senha e nomes. |
| **Lista recolhida** | Tipo do filtro, rota, preset de EQ, velocidade do sweep, toque durante a IA | Mostra só o escolhido. Tocar na linha abre a grade de opções; tocar numa opção aplica e recolhe; a seta para cima recolhe sem mudar. [ASSUMPTION] Uma lista aberta por vez. Números e níveis nunca recolhem; [ASSUMPTION] fase e abas de memória também não. |
| **Par de botões** | Lig./Desl., Fase 0°/180°, links | [ASSUMPTION] Tocar no lado não escolhido aplica na hora. Ligar uma chave com risco abre a folha antes; [ASSUMPTION] desligar não confirma. [ASSUMPTION] Remote out e Bluetooth de áudio ligam na própria linha. |
| **Botão OFF** | Limitador | Liga e desliga o limitador (no fio, limiar 0,5 = OFF). [ASSUMPTION] Com OFF, o campo mostra "OFF" e o fader fica esmaecido; tocar de novo volta ao último limiar. |

### Memória

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Cartão da memória em uso** | Início | Só leitura. Na IA ativa diz "a IA está mexendo nela". |
| **Botões de memória** e **Abas de memória** | Início, Mesa, Player | Na Mesa e no Início, tocar numa memória que não está em uso **carrega** essa memória, **sem confirmação**: passa pela chave "Carregar memória", o app guarda a cópia antes (failure-modes 3) e relê tudo depois. No Player, as abas só escolhem qual playlist aparece (ver Information Architecture › Player). |
| **Botão principal "Salvar na memória N"** | Fixo no pé do Ajuste e das telas de função [ASSUMPTION] | Salva o estado atual na memória em uso. Como sobrescreve, abre a folha de sobrescrever. [ASSUMPTION] Some enquanto o teclado está aberto. |
| **Cartão de memória** | Memórias | Carregar troca todo o som (cópia antes, sem confirmação). **Salvar aqui abre a folha de sobrescrever**, com o nome editável [ASSUMPTION]. Renomear abre o campo do nome. Selo HERDADO quando há conhecimento herdado sobre aquela memória (ver Selo herdado). |
| **Selo herdado** | Memórias, Presets | Marca o que veio dos protótipos (CAP-6). Na memória, tocar no selo mostra a nota herdada (por exemplo, MUSICA ORIG: "versão antiga, precisa ser refeita"). [ASSUMPTION] Nos presets, os herdados são os estados completos que existem nas leituras dos protótipos (como a FILME de 18/10) e se aplicam como qualquer preset; DIALOGO e NOITE não entram, porque são `.trps` cifrados. |

### Folhas e avisos

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Folha de confirmação** | Sobrescrever memória, ligar chave com risco, apagar preset [ASSUMPTION], pedido de risco da IA, aplicado pela metade, reset, troca de senha | [ASSUMPTION] Sobe de baixo; o botão branco faz e o contornado desiste. [ASSUMPTION] Tocar no véu ou em Voltar desiste. Nada é enviado ao aparelho antes do botão branco. |
| **Aviso de risco** | Dentro da folha | Repete o risco escrito em `chaves-de-acesso.md`, sem mudar o texto. |
| **Aviso de erro** | Senha, valor fora da faixa | Texto curto que diz o que fazer. |
| **Lembrete** | Ajuste, telas de função, Memórias, Chaves | Texto fixo explicativo; não é tocável. |
| **Botão secundário** | Folhas, linhas de Configurações | Desiste ou faz uma ação secundária; quando travado, diz por quê ("Desligado pela chave"). |

### IA

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Faixa da IA** | IA ativa | Diz em uma linha o que a IA faz agora. **PARAR IA** revoga o acesso da IA na hora (sem confirmação) e volta para a tela de antes. |
| **Painel ao vivo** | IA ativa | Mostra os valores que a IA mexe, atualizados em até 1 s, com o item atual em lilás e a linha "Agora: banda 2,5 kHz · 0,0 → +1,0 dB". O toque segue a configuração da IA (ver "Uso junto com a IA"). |
| **Registro** | IA ativa ("O que a IA fez"), Histórico | Linhas com hora e frase curta; a mais nova em cima. No Histórico, cada linha diz também a origem (tela ou IA) e o valor. |

### Player

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Linha da playlist** | Player | Tocar numa faixa toca a partir dela. No modo **Editar**: − remove a faixa da playlist (o arquivo continua no celular, sem confirmação [ASSUMPTION]); arrastar pela alça ≡ muda a ordem. Sem deslizar para remover. |
| **Controles do player** | Player | Tocar/pausar e próxima. A faixa atual aparece em cima dos botões. [ASSUMPTION] Ao fim da última faixa, para. |

### Conexão

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Pílula de status** | Topo do app | Mostra Conectando · senha / Conectado · R003 / Desconectado / Só leitura · firmware Rxxx. [ASSUMPTION] Tocar nela quando desconectado faz o mesmo que Conectar. |
| **Cartão de conexão** | Início desconectado | Conectar inicia a conexão e a senha. |
| **Caixas da senha** | Conectar | 4 caracteres A–Z ou 0–9, maiúsculas automáticas, viram • na hora, sem "mostrar". Enviar a senha manda; Cancelar desconecta. |

## State Patterns

| Estado | Onde | Tratamento |
|---|---|---|
| **Primeira abertura** | Início, Memórias, Presets | Conhecimento herdado já carregado (CAP-6), visível como itens com selo HERDADO em Memórias e Presets. Sem senha guardada, Conectar leva às caixas da senha. |
| **Conectando** | Pílula de status, Início | Ponto cinza, "Conectando". Com senha guardada, o app envia a senha sozinho. Depois da senha vem a leitura completa (84 parâmetros, ~13 s). [ASSUMPTION] O Início mostra "Lendo o aparelho" com a contagem, e os valores aparecem só quando lidos. |
| **Conectado** | Todas | Ponto verde, "Conectado · R003". Ao conectar ou reconectar, o app **relê tudo antes de qualquer ação**, inclusive depois de usar o app da Taramps. |
| **Desconectado** | Início (telas-chave 6a) | Cartão "Desconectado" grande com a hora em que caiu e o botão Conectar. Saídas vazias com "—": nunca mostrar valor que não veio do aparelho. Nas outras telas, [ASSUMPTION] os controles ficam travados com "Sem conexão" e um Conectar. O Player continua tocando (é Bluetooth de áudio, não o BLE). |
| **App da Taramps conectado** | Conectar | Só cabe uma conexão BLE. [ASSUMPTION] Se as 3 tentativas falharem: "Não conectou. Se o app da Taramps estiver aberto, feche ele e tente de novo." |
| **Senha errada** | Conectar (telas-chave 6b) | Caixas limpas, "Senha errada. Digite de novo (tentativa N de 3)". Nenhuma escrita é enviada. Na terceira recusa, desconecta e volta ao Desconectado. |
| **Só leitura** (firmware ≠ R003) | Todas | [ASSUMPTION] Pílula "Só leitura · firmware Rxxx" e um aviso fixo no Início: "Firmware diferente do R003. O app só lê; nada é enviado." Todos os controles ficam travados; a porta da IA responde só a leituras. |
| **Chave desligada** | Bloco de função (inclui o bloco Gerador), linhas de Configurações | Travado, com "Chave desligada" ou "Desligado pela chave". A operação é recusada antes de qualquer envio pelo BLE. |
| **Escrevendo um valor** | Qualquer campo (v2 quadro 4) | Teclado aberto, campo ativo com contorno na cor, explicação de que o som só muda no ✓. |
| **Eco diferente do pedido** | Qualquer controle | [ASSUMPTION] O controle mostra o valor relido do aparelho (não o pedido) e um aviso curto "O aparelho ficou em X". |
| **Avançado aberto** | Crossover (telas-chave 2b), Ajuste, Configurações | Fundo mais claro; os controles dentro seguem as regras normais. |
| **Passa-alta ou passa-baixa em OFF** | Crossover (telas-chave 2a), Ajuste | [ASSUMPTION] A frequência some da tela e volta junto com o tipo escolhido. |
| **Link ligado** | Crossover › Avançado e telas de saída | Mexer no canal mestre muda o par (1–2: JBL L manda na JBL R; 3–4: Sub). [NOTE FOR UX] Não está desenhado como a aba e o Ajuste da JBL R aparecem com o link 1–2 ligado, nem como o Sub mostra os canais 3 e 4 quando o link 3–4 está desligado. |
| **Carregando memória** | Início, Mesa, Memórias | Sem confirmação. O app guarda a cópia, carrega e relê; [ASSUMPTION] os valores ficam esmaecidos com "Carregando a memória 3" até a releitura. |
| **Salvando memória** | Folha de sobrescrever | [ASSUMPTION] O botão vira "Salvando…" travado até a releitura; ao fim, "A memória 2 foi salva." |
| **Queda no meio do salvar** | Ao reconectar | O app relê e avisa se salvou ou não: "A memória 2 foi salva." ou "A memória 2 não foi salva. O som continua no aparelho; toque em Salvar de novo." |
| **Aplicado pela metade** | Ao reconectar | Folha "Aplicado pela metade": o preset ou a memória caiu no meio. Botões **Terminar** (branco) e **Voltar à cópia** (contornado). [ASSUMPTION na redação] |
| **Preset aplicado** | Presets | Cópia antes, aplica, relê. [ASSUMPTION] "Preset NOITE aplicado. Para guardar numa memória, use Salvar." |
| **Presets vazio** | Presets no celular | [ASSUMPTION] Só os herdados e "Guarde o som atual como preset." |
| **Apagar preset** | Presets | Folha de confirmação "Apagar o preset X?" [ASSUMPTION] |
| **Gerador tocando** | Gerador | [ASSUMPTION] Contagem regressiva de 30 s visível e botão grande Parar. Ao fim do prazo, ao fechar o app, ao perder a conexão ou num erro, o app manda desligar o gerador e relê para confirmar. |
| **Gerador sem resposta** | Qualquer tela | Se o app não conseguir desligar o gerador e 3 reconexões falharem: faixa em destaque em {colors.parar} "DESLIGUE O MÓDULO AGORA", por cima de tudo, até a conexão voltar. |
| **Gerador ligado ao reconectar** | Ao reconectar | Desliga o gerador antes de qualquer outra ação. [ASSUMPTION] Avisa "O gerador estava ligado e foi desligado". |
| **Voltímetro e clip** | Mesa, Voltímetro e clip | Valor ao vivo; o clip acende no canal certo. Sem leitura do voltímetro: "—" (o aparelho pode responder ERRO). |
| **Reset de áudio / de fábrica** | Configurações › Avançado | Travado enquanto a chave estiver desligada. Com a chave ligada: folha com o risco ("O estado resultante é imprevisível." / "Apaga a senha e as 3 memórias."); depois, o app relê e mostra o resultado. Depois do reset de fábrica, [ASSUMPTION] o app esquece a senha guardada e pede a nova. |
| **Troca de senha** | Configurações › Senha | Travado enquanto a chave estiver desligada. Com a chave: [ASSUMPTION] campos da senha atual e da nova (4 caracteres, mascarados, a nova digitada duas vezes), folha "Uma senha perdida tranca o aparelho.", e o app guarda a nova depois que o aparelho aceita. |
| **Nome do produto** | Configurações | Campo de até 12 caracteres, vale no ✓; relê depois. |
| **Porta da IA desligada** | IA pela rede | Diz que a porta está fechada e mostra a chave "IA pela rede" com Lig./Desl. |
| **Pareando** | IA pela rede | [ASSUMPTION] Código de 6 dígitos grande em {typography.estado-hero}, que some depois do pareamento; mostra o cliente pareado e "Revogar". |
| **IA ativa, travado** (padrão) | IA ativa (v2 quadro 5) | Faixa da IA, painel ao vivo, registro e "Controles travados enquanto a IA trabalha". [ASSUMPTION] Tocar num controle travado mostra "Controles travados. Toque em PARAR IA para mexer." |
| **IA ativa, pausa ao toque** (opção) | IA ativa | Tocar num controle pausa a tarefa; a faixa passa a "IA pausada · retoma após 10 s sem toques"; os controles destravam. |
| **IA pede operação de risco** | Qualquer tela | Folha de confirmação: "A IA pede para carregar a memória 3 FILME" com o risco da chave. Só roda depois do botão branco. |
| **Histórico vazio** | Histórico | [ASSUMPTION] "Nenhum comando ainda." |
| **Playlist vazia** | Player | "Playlist da FILME vazia." e o botão Adicionar músicas. |
| **Arquivo sumiu** | Player | A faixa fica na lista em apagado com "Arquivo não encontrado", é pulada ao tocar e pode ser removida no modo Editar. |
| **Bluetooth de áudio desligado** | Player | Aviso no topo: "O Bluetooth de áudio do processador está desligado." com o botão **Ligar** (comando 251, chave ligada por padrão, sem confirmação). Tocar fica travado até ligar. |
| **Celular não está mandando o som ao processador** | Player | [ASSUMPTION] "O som está saindo pelo celular. Conecte o Bluetooth do celular ao processador." com atalho para as configurações de Bluetooth do Android. |
| **Sem permissão para ler músicas** | Player | [ASSUMPTION] "O app precisa de acesso às músicas do celular." com o botão Permitir (pedido do Android). |

## Interaction Primitives

- **Mexer muda o som na hora.** Arrastar, tocar em − e + e escolher numa lista vão para o aparelho na hora. **Salvar na memória é uma ação à parte.**
- **Valor escrito vale no ✓.** Até o ✓, nada é enviado. Fora da faixa é recusado, com a faixa certa escrita ao lado.
- **Segurar − ou + repete o passo.**
- **Passos de − e +** (MAPA §4): ganho e MASTER 0,1 dB; limitador 0,1 dB; EQ (ganho) 0,1 dB; Q 0,1; atraso 0,5 cm; ataque 0,1 ms; liberação 1 ms. **Frequências: passo proporcional de 1/12 de oitava** (multiplica ou divide por 2^(1/12)), arredondado para Hz inteiro e [ASSUMPTION] com no mínimo 1 Hz de mudança; o valor exato se escreve no campo.
- **Faixas** (MAPA §4): MASTER −80,0 a 0,0 dB; ganho da saída −45,0 a +15,0 dB; frequências 10 a 22 000 Hz; limitador −48,0 a 0,0 dB (ou OFF); atraso 0 a 275 cm; EQ ±12,0 dB; Q do EQ de saída 0,1 a 10,0; Q do EQ de entrada 0,4 a 10,0; ataque 0,1 a 100 ms; liberação 1 a 1600 ms; gerador −60 a 0 dB.
- **Escalas:** frequência e Q em escala logarítmica ([ASSUMPTION] no Q); dB e cm lineares; o 0 dB marcado no trilho.
- **Ritmo de envio** (MAPA §2): uma resposta pendente por vez; cerca de 30 ms entre escritas; durante o arrasto ou a repetição de − e +, no máximo um envio a cada 300 ms por parâmetro. [ASSUMPTION] Ao soltar, o último valor sempre é enviado.
- **Eco e releitura:** a tela mostra o valor que o aparelho confirmou.
- **Abrir uma tela nunca escreve nada no aparelho.** [ASSUMPTION] Herdado do cuidado com o app da Taramps, que reescreve ataque e liberação ao abrir o Limiter em Auto.
- **Cópia antes de mexer em vários parâmetros:** antes de carregar memória ou aplicar preset, o app guarda o estado.
- **Toque, arrasto e escrita.** Sem deslizar para carregar ou apagar (o app da Taramps carrega uma memória ao deslizar; aqui não). O único arrasto de lista é a alça ≡ da playlist no modo Editar.
- **Proibidos:** tocar num bloco para entrar em edição; esconder um número ou nível atrás de um toque; confirmação para mexer em ajustes de áudio ou para carregar memória.

## Accessibility Floor

Esta seção trata do comportamento; o contraste visual está em DESIGN.md.Colors. **Não herdar** a acessibilidade da Vitrine e dos Bastidores do BGS Sound Lab.

- **Alvos de toque ≥ 44 px** em tudo ({spacing.alvo-min}); − e + dos parâmetros com {spacing.alvo-passo}; − e + do ganho com {spacing.alvo-passo-grande}; PARAR IA e tocar/pausar com {spacing.alvo-parar}. Os botões da grade de tipos de filtro sobem para 44 px. [ASSUMPTION]
- **Contraste mínimo de 4,5:1** para todo texto de leitura, inclusive rótulos e escalas ({colors.apagado} já passa). Só texto grande em negrito pode ficar entre 3:1 e 4,5:1 (PARAR IA).
- **Texto mínimo de 11 px**, contando escalas e rótulos do EQ; valores mexíveis ≥ 22 px.
- **Escala de fonte do Android:** o app acompanha. Telas rolam em vez de cortar; alvos não diminuem; números grandes podem quebrar a unidade para baixo, nunca o número.
- **Cor nunca sozinha:** cada saída sempre com o nome escrito; o clip tem o nome do canal embaixo do ponto; o escolhido também muda o peso; o travado diz por quê.
- **TalkBack:** [ASSUMPTION] cada controle com nome, valor e unidade ("Ganho do Sub, mais 2,6 decibéis"); − e + com "Diminuir" e "Aumentar"; o giratório e os faders expõem ajuste por passo; o clip anuncia "Clip no Sub".
- **Movimento:** [ASSUMPTION] o pulso da IA respeita "remover animações" do Android e fica parado.
- **Confirmações** dizem o que some, e o botão de fazer é diferente do de desistir em cor e em texto.

## Key Flows

### Fluxo 1 — Refazer a MUSICA ORIG e salvar na memória 2 só pelo celular (Denis, sala, celular na mão)

Teste 1 do sinal de sucesso.

1. Denis abre o app. A senha está guardada; a pílula passa de "Conectando" a "Conectado · R003" e o app lê o aparelho.
2. No **Início**, a memória em uso é a 1. Ele toca em **2 MUSICA ORIG**. Sem pergunta, o app guarda a cópia, carrega a memória 2 e relê tudo; o cartão mostra **MUSICA ORIG**. Em **Memórias**, o selo HERDADO dessa memória lembra que ela é a versão antiga.
3. Ele vai à **Mesa** e acerta os ganhos: arrasta a capa do Sub, toca no número da JBL L, escreve o valor e toca em ✓. O som muda a cada passo; a faixa de monitoração mostra se algum canal clipa.
4. Em **Áudio** › **Limitador**, ele escolhe a aba de cada saída e segura + até o limiar que quer. Em **Crossover**, confere o tipo do filtro na lista recolhida.
5. Ele ouve e volta ao passo 3 quantas vezes quiser. Nada foi salvo ainda.
6. Ele toca em **Salvar na memória 2**. Sobe a folha **"Salvar sobre a MUSICA ORIG?"**, com "A versão atual some." e o campo do nome.
7. Ele mantém o nome e toca em **Salvar sobre a MUSICA ORIG**.
8. **Clímax:** o app salva, relê e confirma (estado **Salvando memória**); em **Memórias**, a memória 2 aparece com o nome e os valores novos. Ele não abriu o app da Taramps em nenhum momento.

Falha: a conexão cai no meio do salvar → ao reconectar, vale o estado **Queda no meio do salvar**.

### Fluxo 2 — A IA ajusta o EQ da FILME e o Denis vê ao vivo (Denis, no sofá, notebook ao lado)

Teste 2 do sinal de sucesso.

1. Antes, uma vez só: em **Configurações › IA pela rede**, Denis toca em **Lig.**; a folha mostra "Deixa a IA comandar o som pela rede." e ele toca em **Ligar a chave**. O app mostra o código de 6 dígitos; ele passa o código ao Claude Code, que pareia. Na mesma tela, o toque durante a IA fica no padrão: **travar até PARAR IA**.
2. No notebook, ele pede à IA para ajustar o EQ da FILME.
3. A IA pede para carregar a memória 3. Como é operação de risco para a IA, sobe a folha do estado **IA pede operação de risco**; ele confirma.
4. O app abre a **IA ativa**: faixa "IA ajustando o EQ da FILME", painel ao vivo com as 15 bandas e os ganhos e limitadores, registro "O que a IA fez" e o aviso de controles travados (estado **IA ativa, travado**).
5. Cada banda que a IA mexe acende em lilás e se move em até 1 s; a linha "Agora" mostra o valor de antes e o de depois; o registro ganha uma linha.
6. Ele toca sem querer num fader: aparece o aviso do estado **IA ativa, travado**. Nada muda.
7. A IA termina e pede para salvar na memória 3. Como sobrescreve, sobe a folha de confirmação; ele confirma.
8. **Clímax:** ele decide que chega e toca em **PARAR IA**. O acesso da IA é revogado na hora, a tela volta para onde estava e o **Histórico** guarda cada comando, com origem IA, hora e valor.

Variante: com a opção **pausar ao toque** escolhida em IA pela rede, no passo 6 o toque pausa a IA, os controles destravam, ele mexe, e a IA só retoma depois de 10 s sem toques.

Falha: o Wi-Fi cai → a IA some sem deixar nada pendente (cada comando é avulso); [ASSUMPTION] a IA ativa fecha e o app volta ao normal com o aviso "A IA perdeu a conexão".

### Fluxo 3 — Noite de filme (Denis, 21h, luz apagada)

1. Denis liga o som e abre o app. Pílula: **Conectando**, depois **Conectado · R003**. Se a conexão falhar, o Início fica no estado **Desconectado**.
2. O Início mostra a memória em uso. Ele toca em **3 FILME**; o app carrega e relê sem perguntar; o cartão mostra **FILME** em letras grandes.
3. Antes do filme, ele abre o **Player**: a playlist da FILME tem as trilhas que ele usa para conferir o som. Ele toca a primeira; o som sai pelo processador.
4. Começa o filme na TV. Ele pausa o Player e, na cena de explosão, toca em **Mesa**.
5. Ele segura **+** do **SUB**. O som do sub sobe na hora, passo a passo.
6. O ponto de clip do Sub acende na faixa de monitoração; ele toca uma vez em − e o ponto apaga.
7. **Clímax:** o grave chega no ponto que ele queria, sem sair do filme e sem nenhuma confirmação.
8. Ele não salva. Na próxima vez que carregar a FILME, o sub volta ao valor guardado. Se gostar, toca em Ajustar › Salvar na memória 3.

### Fluxo 4 — Montar a playlist da MUSICA ORIG (Denis, tarde de domingo)

1. Com a MUSICA ORIG em uso, Denis toca em **Player**. A tela está no estado **Playlist vazia**.
2. Ele toca em **Adicionar músicas**; o seletor do Android abre; ele marca cinco arquivos e confirma. As faixas entram na lista.
3. Ele toca em **Editar**, arrasta a terceira faixa para o topo pela alça ≡ e remove uma com −. Toca em **Pronto**.
4. Ele toca em tocar/pausar. Aparece o aviso do estado **Bluetooth de áudio desligado**. Ele toca em **Ligar**; o aviso some.
5. **Clímax:** a primeira música sai pelas JBL e pelo sub, com a regulagem da MUSICA ORIG.

Falha: um arquivo foi apagado do celular → vale o estado **Arquivo sumiu**.

### Fluxo 5 — Conferir a leitura contra o app da Taramps (Denis, bancada, os dois apps no celular)

Teste 3 do sinal de sucesso.

1. No app da Taramps, Denis carrega a memória 1 e anota os valores tela a tela. Ele **não abre a tela Limiter com o Auto ligado**, porque ela reescreve o ataque e a liberação.
2. Ele fecha o app da Taramps (só cabe uma conexão BLE).
3. Ele abre o BGS Controle, que conecta, relê tudo (84 parâmetros) e mostra a memória 1 em uso.
4. Ele confere pela grade **Áudio**: cada bloco mostra o resumo, e cada tela de função mostra os valores por saída, nas abas. Abre o Avançado onde precisa (ataque e liberação, links).
5. Repete os passos 1 a 4 para as memórias 2 e 3.
6. **Clímax:** cada valor do BGS Controle bate com o app da Taramps nas três memórias.

Falha: o BGS Controle não conecta → vale o estado **App da Taramps conectado**.

### Fluxo 6 — Tom de teste com prazo de 30 s (Denis, bancada, RCA dos módulos desconectado)

1. Em **Áudio**, o bloco **Gerador** está no estado **Chave desligada**. Ele toca no bloco e vai para **Chaves de acesso**.
2. Toca em **Lig.** no Gerador. A folha mostra "Um tom forte pode queimar o tweeter ou o sub." Ele toca em **Ligar a chave**.
3. Volta ao **Gerador**, escolhe a frequência e o ganho do tom fixo e toca em **Tocar**.
4. A contagem de 30 s aparece com o botão grande **Parar**. Ele mede com o multímetro.
5. **Clímax:** no zero, o app desliga o gerador sozinho, relê e mostra o gerador desligado.

Falha: a conexão cai com o tom ligado → o app tenta desligar; se 3 reconexões falharem, vale o estado **Gerador sem resposta**. Ao reconectar, vale **Gerador ligado ao reconectar**.

## Uso junto com a IA

Segue `porta-de-comandos.md` e `chaves-de-acesso.md`, atualizados em 21/10 com as decisões deste documento (padrão "travar até PARAR IA" em `porta-de-comandos.md`, Player como CAP-15 no SPEC, chave "IA pela rede" em `chaves-de-acesso.md`).

- **Porta desligada por padrão.** Abre só com a chave **"IA pela rede"** ligada pelo Denis, na tela.
- **Pareamento único** por código de 6 dígitos mostrado no app. Só a rede local. A senha do processador nunca passa pela porta.
- **A IA passa pelas mesmas chaves do Denis** e nunca liga ou desliga uma chave.
- **Risco pede confirmação na tela:** reset de fábrica, reset de áudio, troca de senha, gerador, carregar memória, salvar por cima de uma memória, remote out.
- **Tudo visível:** quando a IA começa, o app abre a **IA ativa**; cada mudança aparece em até 1 s no painel ao vivo e vai para o Histórico.
- **PARAR IA revoga o acesso na hora.**
- **O toque durante a IA é uma configuração**, em Configurações › IA pela rede (lista recolhida "Quando eu toco durante a IA"):
  - **Travar até PARAR IA** (padrão): os controles ficam travados enquanto a IA trabalha; o único jeito de mexer é PARAR IA.
  - **Pausar a IA**: o toque pausa a tarefa, os controles destravam, o app avisa a IA da pausa e manda cada mudança feita na tela. A IA só retoma depois de 10 s sem toques. Um ajuste que o Denis acabou de mexer só pode ser mudado pela IA depois de 10 s sem toques nele.
- [NOTE FOR UX] No modo "pausar", a spec pede que toda mudança da IA apareça como aviso. Falta decidir o aviso quando a tarefa está pausada e o Denis está em outra tela.

## Quem vem do app da Taramps

O Denis conhece o app da Taramps ([`imports/referencia-taramps-audio.png`](imports/referencia-taramps-audio.png)). O BGS Controle oferece pontos de entrada familiares sem copiar o visual.

| No app da Taramps | No BGS Controle |
|---|---|
| Home | Início |
| Áudio (grade 2 × 5) | **Áudio**, a mesma grade, na mesma ordem: Roteamento, EQ 15 bandas, EQ de entrada, Crossover, Alinhamento, Fase, Limitador, Nível das saídas, EQ de saída, Gerador |
| Player | **Player**, com uma playlist por memória |
| Engrenagem (Salvar/Carregar memórias e presets, resets, nome, senha) | **Engrenagem → Configurações**: Memórias, Presets no celular, Aparelho, Chaves, IA pela rede, Histórico; resets em Avançado |
| Salvar (barra) | Configurações › Memórias e "Salvar na memória N" no pé dos ajustes |
| Volt. (barra) | Faixa de monitoração na Mesa e Configurações › Aparelho › Voltímetro e clip |
| Deslizar uma memória carrega ou exporta | Botões escritos (Carregar, Salvar aqui); nada acontece por deslizar |
| Abrir o Limiter em Auto reescreve ataque e liberação | Abrir uma tela nunca escreve |
| Presets `.trps` | Não são lidos. Para trazer um preset antigo, carregue-o pelo app da Taramps e salve a leitura como preset próprio. **Exportar**, na linha de Presets, grava o preset próprio como arquivo JSON (não `.trps`). |

## Inspiration & Anti-patterns

- **Tirado do Estúdio Noturno:** pouca coisa na tela para ler de longe; nome da memória enorme e fino.
- **Tirado do Console Tátil:** faders com capa, botão giratório, − e + grandes; regulagem tátil.
- **Recusado — Bancada Técnica e Papel Calmo:** direções não escolhidas.
