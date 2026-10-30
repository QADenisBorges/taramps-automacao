---
name: BGS Controle
status: final
updated: 2026-10-29
sources:
  - ../../../specs/spec-app-controle-taramps/SPEC.md
  - ../../../specs/spec-app-controle-taramps/chaves-de-acesso.md
  - ../../../specs/spec-app-controle-taramps/porta-de-comandos.md
  - ../../../specs/spec-app-controle-taramps/failure-modes.md
  - ../../../specs/spec-app-controle-taramps/conhecimento-herdado.md
  - ../../../../protocolo/MAPA.md
  - ../../sprint-change-proposal-2026-10-23.md
  - ../../sprint-change-proposal-2026-10-29.md
  - DESIGN.md
---

# BGS Controle — Experiência

Os mocks [`mockups/direction-sintese-v2.html`](mockups/direction-sintese-v2.html), [`mockups/key-screens.html`](mockups/key-screens.html) e [`mockups/key-screens-3b.html`](mockups/key-screens-3b.html) ilustram este documento. **Em qualquer conflito, este documento e o DESIGN.md ganham dos mocks.** Conflitos de comportamento já conhecidos (os visuais estão em DESIGN.md › Brand & Style):

- A barra tem quatro pílulas, **Início · Mesa · Áudio · Player**. A v2 mostra só Início e Mesa, e as telas-chave mostram três.
- Nas frequências, − e + andam **1/12 de oitava**, não 1 Hz como na v2.
- No Ajuste da saída, **atraso, fase e EQ de saída ficam em Avançado**. A v2 mostra esses controles abertos.
- A Mesa ganhou a **faixa de monitoração** (voltímetro e clip), que os mocks não têm.
- A chave da IA se chama **"IA pela rede"** em todo lugar. O mock de Chaves diz "Porta de comandos da IA".
- Na IA ativa, o comportamento do toque é **configurável**. O padrão é o do mock: controles travados até PARAR IA.
- **Memórias (história 3b, 23/10):** a v2 e as telas-chave mostram as 3 memórias do aparelho em botões e abas, "Salvar na memória N" e "Salvar aqui". Vale o modelo novo: Setup no cabeçalho, 3 botões das memórias do celular usadas por último + "…", chip de preset, um só **Salvar** e Player sem abas. O mock da 3b desenha esse modelo no Início (quadro 1), na tela Setup em dúvida (quadro 2) e na folha do Salvar (quadro 3).

Em aberto (resolvido nas histórias de tela 10 e 11): layout de edição do EQ 15 bandas; link 1–2/3–4 nas abas e no Ajuste; aviso da IA pausada em outra tela; fundo do campo ativo por saída.

Origem e histórico das suposições: `.memlog.md`.

**Mudança de 29/10/2026** (`sprint-change-proposal-2026-10-29.md`): o Setup é só o físico (equipamentos e fichas com origem); toda a regulagem, com o valor final, é da memória; o link sai sozinho das ligações; o equilíbrio do Setup sai; o app começa sem memórias e sem presets do celular; a conferência ao conectar compara com a última memória aplicada; a IA espera **Continuar** a cada 3 ajustes seguidos sem um toque do Denis. Os mocks ainda mostram o modelo antigo nesses pontos.

**Mudança de 29/10/2026 (memórias do processador):** "As memórias do processador não devem aparecer no app" (decisão do Denis). Saem a seção **Do aparelho** da tela Memórias e da lista de memórias ("…"), o carregar (`241`), a chave "Carregar memória do aparelho" (ficam 10 chaves), o estado "Memória N do aparelho" no cartão da memória em uso e na Mesa, e o estado **Carregando memória do aparelho**. Fica o **"Som do aparelho"**: ele só diz que o som atual não bate com nenhuma memória do celular. Para usar uma memória do aparelho, o caminho é o app da Taramps; depois, este app relê. Os mocks que mostram as memórias do aparelho estão velhos nesse ponto.

## Foundation

- **Plataforma:** Android, app em **.NET MAUI** (projeto `BgsControle.App`, com os ViewModels em `BgsControle.Telas`). Sem iOS.
- **Forma:** celular em **retrato**, uma tela só. [ASSUMPTION] A rotação fica travada em retrato.
- **Quem usa:** uma pessoa, o **Denis**, no próprio celular, perto do som. A IA (Claude Code no notebook) é uma segunda origem de comandos, não um usuário da tela.
- **Aparelho:** PRO 2.4BT + AUDIO, firmware R003, uma conexão BLE por vez. A música do Player sai do celular pelo Bluetooth de áudio do processador.
- **Sistema de interface:** nenhum de terceiros. Navegação, teclado, seletor de arquivos e botão Voltar seguem o Android; o visual vem todo do [`DESIGN.md`](DESIGN.md), que é a referência de identidade.
- **Tema:** só escuro. Fonte Roboto variável embutida; os tamanhos acompanham a escala de fonte do Android.
- **As 3 memórias do processador:** não aparecem no app (decisão do Denis, 29/10/2026). O app não lê os nomes nem carrega; salvar, renomear e carregar ficam no app da Taramps. O processador não diz qual memória dele está tocando.

### Três camadas do som

Cada parâmetro tem uma camada dona (CAP-4, CAP-5, CAP-16; decisões do Denis de 22 e 23/10). O Salvar usa esta tabela (ver Salvar por camadas).

| Camada | O que guarda |
|---|---|
| **Setup** | Uma instalação (o 2.1 de casa, o carro…), só o físico: nome e **equipamentos com as fichas** (ver Equipamentos e saídas). O link 1–2 e 3–4 sai sozinho das ligações. |
| **Memória no celular** | **Toda a regulagem, com o valor final:** atraso, fase, corte (passa-alta e passa-baixa, frequência e tipo), rota, EQ paramétrico de entrada, ganho de cada saída, limitador (limiar, ataque, liberação, Auto), EQ de saída e MASTER. Guarda também a playlist e o preset lembrado. Pertence a um Setup; a quantidade é livre (no dia a dia, até 4 por Setup). O app começa sem nenhuma. |
| **Preset** | Só o EQ de 15 bandas, por cima da memória. |
| Fora das camadas [ASSUMPTION] | Mute, remote out, Bluetooth de áudio, gerador, nome do produto e senha. Valem na hora; o Salvar não os guarda. |

**Memória ativa** de um Setup é a última memória do celular aplicada nele. É com ela que a conferência compara o que leu, e é ela que o app reaplica.

## Information Architecture

Barra inferior com quatro destinos: **Início · Mesa · Áudio · Player**. A **engrenagem** no topo abre **Configurações**. Tudo o que não está na barra abre em **tela cheia com ‹** e sem a barra. Folhas sobem por cima da tela atual, uma de cada vez.

| Superfície | Chega por | Para quê | Mock |
|---|---|---|---|
| **Início** | Abrir o app; barra | Ler de longe o Setup, a memória em uso, o preset dela e as três saídas; trocar de memória (3 botões + "…") e de preset (chip); tocar no nome do Setup para trocar de Setup; Conectar quando desconectado. Sem selo HERDADO | mock da 3b, quadro 1; v2 quadro 1 e telas-chave 6a (modelo antigo de memória) |
| **Mesa** | Barra | Ganho de cada saída e MASTER com faders; trocar de memória pelos mesmos 3 botões + "…" do Início, no topo [ASSUMPTION]; voltímetro e clip por canal na faixa de monitoração | v2 quadro 2 (sem a faixa; com as abas antigas) |
| **Áudio** | Barra | Grade 2 × 5 por função, na ordem do app da Taramps | telas-chave 1 |
| **Player** | Barra | Playlist da memória do celular em uso: tocar músicas do celular pelo Bluetooth até o processador | só este documento |
| **Setup em dúvida** | Sozinha ao conectar, quando corte, atraso, fase ou rota lidos não batem com a memória ativa do último Setup usado, ou quando ele ainda não tem memória | Escolher ou criar o Setup antes de tocar, com o som mutado (ver Conectar e Setup) | mock da 3b, quadro 2 |
| **Trocar de Setup** | Tocar no nome do Setup no cabeçalho do Início ("Setup Casa 2.1 ›") | A mesma tela, para escolher outro Setup ou criar um (ver Conectar e Setup) | mock da 3b, quadro 2 (variante sem dúvida) |
| **Lista de memórias** | Botão "…" ao lado dos 3 botões de memória (Início, Mesa) | As memórias do celular do Setup em uso | só este documento |
| **Ajuste da saída** (uma por saída do Setup; no Casa 2.1: JBL L, JBL R, Sub) | "Ajustar ›" no cartão de saída (Início) ou "Ajustar" no canal (Mesa) | Os ajustes de uma saída numa tela (ver Ajuste da saída) | v2 quadros 3, 3b, 4 (com a divisão antiga); mock da 3b, quadro 3 (Salvar e folha do Salvar) |
| **Telas de função** (Roteamento, EQ 15 bandas, EQ de entrada, Crossover, Alinhamento, Fase, Limitador, Nível das saídas, EQ de saída) | Bloco da grade Áudio | Uma função para as três saídas, com abas de saída [ASSUMPTION] | telas-chave 2a, 2b (Crossover); as outras, só este documento |
| **Gerador** | Bloco Gerador da grade Áudio | Tom fixo e sweep, com prazo de 30 s | só este documento |
| **Configurações** | Engrenagem | Ponto de entrada familiar para quem vem do app da Taramps | telas-chave 3 (sem a linha do voltímetro) |
| **Setups** | Configurações › Setups [ASSUMPTION] | Ver os Setups, renomear e apagar [ASSUMPTION]; selo HERDADO no Casa 2.1. Criar e trocar de Setup só pela tela Trocar de Setup ou Setup em dúvida. Tocar num Setup abre a tela **Setup** | só este documento |
| **Setup** (um Setup) | Configurações › Setups › tocar num Setup; tocar no esquema no Início [ASSUMPTION] | O **esquema do Setup** em cima (2D com sensação de 3D) e a lista de **equipamentos** embaixo: adicionar, editar, tirar (ver Equipamentos e saídas) | só este documento (proposta de 23/10, equipamentos) |
| **Folha de equipamento** | "Adicionar equipamento" ou tocar num equipamento (lista ou esquema) | Grupo, categoria, nome, modelo, função acústica, dados técnicos e onde está ligado | só este documento |
| **Memórias** | Configurações › Memórias | Memórias do celular do Setup em uso: aplicar, criar do som atual, renomear, apagar. As memórias do processador não aparecem | telas-chave 4a, 4b (modelo antigo) |
| **Presets no celular** | Configurações › Presets; chip de preset no Início (folha com a lista) | Presets de 15 bandas: aplicar por cima da memória, renomear, apagar, exportar. Não há preset herdado. Preset novo nasce na folha do Salvar | só este documento |
| **Aparelho** (grupo dentro de Configurações) | Configurações | Remote out, Bluetooth de áudio, nome do produto, senha, Voltímetro e clip; resets em Avançado | telas-chave 3 |
| **Voltímetro e clip** | Configurações › Aparelho › Voltímetro e clip; tocar na faixa de monitoração da Mesa | Tensão e clip das saídas e da entrada ao vivo, maiores | só este documento |
| **Chaves de acesso** | Configurações › Chaves de acesso | Ligar e desligar as 10 chaves, com aviso do risco | telas-chave 5a, 5b |
| **IA pela rede** | Configurações › IA pela rede | Chave da IA, código de pareamento de 6 dígitos, cliente pareado, revogar, e o comportamento do toque durante a IA | só este documento |
| **IA ativa** | Abre sozinha quando a IA começa uma tarefa | Ver ao vivo o que a IA mexe; PARAR IA | v2 quadro 5 |
| **Histórico** | Configurações › Histórico | Cada comando, da tela e da IA, com origem, hora e valor | só este documento (o recorte "O que a IA fez" é o modelo) |
| **Conectar e senha** | Início desconectado → Conectar | Conectar, digitar a senha, ver a recusa; depois, a conferência do Setup | telas-chave 6a, 6b |

Regras de navegação:

- O botão Voltar do Android faz o mesmo que ‹. [ASSUMPTION]
- A IA ativa não tem barra inferior nem ‹ [ASSUMPTION]; ela fecha ao parar e volta para a tela de antes.
- A tela **Setup em dúvida** não tem barra inferior, ‹ nem engrenagem: só se sai dela por um dos três botões. [ASSUMPTION] O Voltar do Android não a fecha.

### O que fica à vista e o que vai para Avançado

Regra do Denis: em toda tela de parâmetros, as funções principais e as mais mexidas ficam à vista; o resto fica em **Avançado**, recolhido.

| Tela | À vista | Em Avançado |
|---|---|---|
| Ajuste da saída | Ganho (giratório), passa-alta (frequência + tipo), passa-baixa (frequência + tipo), limitador (limiar + OFF) | Atraso e fase; EQ de saída (frequência, ganho, Q), ataque e liberação do limitador; [ASSUMPTION] Auto do limitador e mute |
| Crossover | Por saída: passa-alta e passa-baixa (frequência + tipo) | — (o link é automático; uma linha só de leitura diz "CH3 e CH4 ligados: alimentam o mesmo bridge") |
| Limitador | Limiar com OFF | Ataque e liberação; [ASSUMPTION] Auto |
| Alinhamento | Atraso de cada saída (é a função da tela) | — |
| Fase | 0° / 180° de cada saída (é a função da tela) | — |
| EQ de saída | Frequência, ganho e Q (é a função da tela) | — |
| Nível das saídas | Ganho de cada saída | [ASSUMPTION] Mute |
| Roteamento | Rota de cada saída (A, B, A+B) | — |
| EQ 15 bandas | As 15 bandas | [ASSUMPTION] Preset de EQ do aparelho (Flat, Loudness…, Custom), que não é o preset do app |
| EQ de entrada | Frequência, ganho, Q (um só, sem saídas) | — |
| Gerador | [ASSUMPTION] Tom fixo: frequência, ganho, Tocar | [ASSUMPTION] Sweep: inicial, final, ganho, velocidade |
| Configurações | Setups [ASSUMPTION], Memórias, Presets, Aparelho, Chaves, IA pela rede, Histórico | Reset de áudio, Reset de fábrica |

O padrão das telas de função é o mesmo do Ajuste: o que dá nome à tela fica à vista; o que é ajuste fino daquele parâmetro vai para Avançado.

[NOTE FOR UX] O layout de edição da tela EQ 15 bandas ainda não foi desenhado; o painel ao vivo da IA só mostra a leitura.

### Ajuste da saída

Tela cheia com ‹, aberta por "Ajustar ›" (Início) ou "Ajustar" (Mesa). À vista e em Avançado, como na tabela acima. O botão **Salvar** fica fixo no pé ([mock da 3b, quadro 3](mockups/key-screens-3b.html)).

**Sem equilíbrio do Setup** (decisão do Denis, 29/10/2026): cada memória guarda o valor final de cada saída. A MUSICA pode ter a JBL L igual à R, e a FILME a R mais baixa, cada uma com os seus valores.

### Conectar e Setup

Ao conectar, depois da senha e da leitura completa, o app **confere o Setup** (CAP-16, failure-modes 5). Ele compara o que leu com a **memória ativa do último Setup usado**. Na primeira conexão, o Setup é o herdado **Casa 2.1**, ainda sem memórias.

| O que o app lê | O que acontece |
|---|---|
| Tudo bate com a memória ativa | Entra direto, sem pergunta (estado **Setup confere**). |
| Só ganho, limitador, EQ ou MASTER diferem | Entra direto, **sem mutar**, com o aviso **"Som diferente da FILME"** e o botão **Reaplicar** (estado **Som diferente da memória ativa**). |
| Corte, atraso, fase ou rota diferem, ou o Setup não tem memória | **Muta** e abre a tela **Setup em dúvida**. |
| Os 4 canais ainda mutados por uma dúvida que não terminou | Volta à tela **Setup em dúvida** (estado **Dúvida interrompida**). |

Corte, atraso, fase e rota diferentes podem forçar o tweeter ou indicar outra instalação; ganho e EQ diferentes são, quase sempre, um ajuste que não foi salvo (decisão do Denis, 29/10/2026).

**O mudo da dúvida** é o mute do aparelho nos **4 canais** (MAPA, comando `100`). Ele fica no aparelho até o Denis decidir, mesmo se o app fechar ou a conexão cair. Sem o celular, o mudo sai pelo app da Taramps. Durante a dúvida, **a IA fica bloqueada**.

Tela **Setup em dúvida** (decisão do Denis sobre a recomendação da IA; [mock da 3b, quadro 2](mockups/key-screens-3b.html)):

- **Sem barra, sem ‹ e sem engrenagem:** a escolha é obrigatória. Em cima, só a marca e a pílula de status; depois, o título "Setup em dúvida" e a frase "O aparelho não está como o Casa 2.1 deixou da última vez."
- **Som mutado**, dito numa faixa: "**Som mutado** até você escolher." Nada toca nem é aplicado antes da escolha.
- **No topo, a lista do que não bate** (rótulo O QUE NÃO BATE, "lido agora no aparelho"), uma linha por diferença de corte, atraso, fase ou rota contra a memória ativa: "Atraso do Sub: 80 cm na FILME, 0 cm no aparelho". Sem memória para comparar, diz "O Casa 2.1 ainda não tem memória." Só leitura.
- **No pé, os três botões**, cada um com uma frase curta embaixo dizendo o que faz:
  - **Escolher o Setup** (botão principal): abre a lista de Setups. Ao escolher, o app aplica a memória ativa daquele Setup (com o preset que ela lembra), relê e desmuta. Num Setup sem memórias, desmuta sem aplicar nada.
  - **Criar Setup novo:** o app pede o nome do Setup e o da memória; o som que está no aparelho vira a primeira memória dele. [ASSUMPTION] Depois do ✓, o app desmuta, e o Setup novo fica em uso, com os equipamentos a cadastrar.
  - **Seguir sem Setup** (contornado): desmuta sem aplicar nada e deixa o aparelho como está. As memórias do celular ficam travadas até escolher um Setup.

**Trocar de Setup com o app conectado:** tocar no nome do Setup no cabeçalho do Início volta à escolha e à conferência, como numa nova conexão: o som muta e abre a mesma tela. Não há troca solta de Setup. [ASSUMPTION] Aqui a tela tem cabeçalho de tela cheia, com ‹ e o título "Trocar de Setup"; ‹ ou Voltar desiste, desmuta e não aplica nada. [ASSUMPTION] Sem diferença para listar, a lista some e o topo diz "O aparelho está no Casa 2.1."

**Processador que perdeu o estado ao desligar** (a história 12 mede se isso acontece): o que foi lido não bate com a memória ativa, então vale o Setup em dúvida. Ao escolher o Setup, o app reaplica a memória ativa. A reaplicação vem sempre **depois** da conferência.

**O que o cartão da memória em uso mostra.** O nome aparece só quando o app sabe o que está tocando (decisão do Denis, 23/10):

| Situação | Cartão |
|---|---|
| Memória do celular aplicada ou criada nesta conexão, ou confirmada pela conferência ao conectar | O nome dela, com o chip de preset; o botão dela em branco |
| O app não sabe: Seguir sem Setup, ou ganho, limitador, EQ ou MASTER diferentes da memória ativa (por exemplo, depois de carregar uma memória pelo app da Taramps) | **"Som do aparelho"**, sem nome nem chip; nenhum botão em branco. [ASSUMPTION] Embaixo: "O app não sabe qual memória está tocando." Com ganho ou EQ diferente, também o aviso "Som diferente da FILME" e **Reaplicar** |
| Setup sem memórias | "Nenhuma memória" e **Criar memória do som atual** |

### Salvar por camadas

**Um só botão Salvar**, fixo no pé do Ajuste e das telas de função. Mexer muda o som na hora; Salvar é um passo à parte e guarda no celular, cada mudança na camada dona dela (ver Três camadas do som). A folha do Salvar compara o estado atual com o que está guardado no Setup em uso, na memória em uso e no preset que ela lembra, e lista só o que mudou.

| Caso | Para onde vai | O que a folha pergunta |
|---|---|---|
| Memória do celular em uso | Regulagem → memória em uso; 15 bandas → preset lembrado | Com as 15 bandas mexidas: **Atualizar o preset Rock** ("Vale para toda memória que usa ele.") ou **Criar preset novo**, com o nome. [ASSUMPTION] Nenhuma vem escolhida; o botão branco só destrava depois da escolha |
| "Som do aparelho" em uso | Regulagem → **memória nova do celular**, com o nome. [ASSUMPTION] 15 bandas → **preset novo**, com o nome, lembrado pela memória nova | — Com isso, a próxima conexão confere sem dúvida |
| Setup sem memórias | [ASSUMPTION] Regulagem → memória nova do celular, com o nome | — |
| Seguir sem Setup | Nada | [ASSUMPTION] Salvar travado: "Escolha um Setup para salvar." |
| Nada mudou | Nada | [ASSUMPTION] Salvar travado: "Nada para salvar." Com "Som do aparelho" em uso, o Salvar nunca trava por isso |

Regras:

- [ASSUMPTION] O antes e o depois mostram o valor da tela, que é o final no aparelho e o que a memória guarda.
- O Salvar não manda nada pelo BLE.
- [ASSUMPTION] Ao fim, aviso curto do que foi para onde: "Salvo: regulagem na MUSICA ORIG, 15 bandas no Rock."
- [ASSUMPTION] Aplicar outra memória, ou Reaplicar, descarta as mudanças não salvas sem perguntar, porque aplicar não pede confirmação.
- **Criar memória do som atual** (Memórias, cartão "Nenhuma memória") guarda a regulagem que está tocando numa memória nova, sem a folha. Com "Som do aparelho" em uso, abre a folha do Salvar (terceira linha da tabela).

### Player

A pílula **Player** abre a playlist da **memória do celular em uso**. **Cada memória do celular tem a sua playlist** (CAP-15), montada com arquivos de áudio do celular. O app toca e o som vai pelo Bluetooth de áudio até o processador.

- **Uma playlist só na tela:** a da memória em uso, com a linha "Playlist da MUSICA ORIG" em cima. **Sem abas de memória.** Para ver ou editar outra playlist, troque de memória (Início ou Mesa).
- **Sem playlist:** sem memória do celular em uso ("Som do aparelho", Seguir sem Setup). Ver o estado **Sem playlist**.
- **Lista:** as faixas da playlist, na ordem. A que toca fica marcada.
- **Adicionar músicas:** abre o seletor de arquivos do Android, com vários arquivos de áudio de uma vez. As escolhidas entram no fim da lista. [ASSUMPTION] O app guarda o caminho do arquivo; ele não copia a música.
- **Editar:** mostra − para remover e ≡ para arrastar e ordenar. **Pronto** sai do modo.
- **Controles fixos embaixo:** tocar/pausar e próxima.
- **Trocar de memória com música tocando:** [ASSUMPTION] a música continua; a playlist da nova memória entra quando a faixa atual acabar.
- **Segundo plano:** [ASSUMPTION] a música continua com o app em segundo plano, com tocar/pausar e próxima na notificação do Android.

### Equipamentos e saídas

Proposta aprovada pelo Denis em 23/10/2026 (`sprint-change-proposal-2026-10-23-equipamentos.md`).

**O modelo de ligação.**
- O processador tem **CH1 a CH4**, saídas de linha.
- Cada canal vai para **uma ou mais entradas de linha** (cabo direto, Y ou divisor), e todas recebem o mesmo sinal com a mesma configuração.
- Os equipamentos com entrada de linha são de quatro grupos:
  1. **amplificam e tocam:** caixa ativa, sub ativo, monitor ativo, line array ativo, soundbar ou receiver;
  2. **amplificam para alto-falantes passivos:** módulo ou amplificador, com os canais dele e o **bridge** como opção;
  3. **processam ou distribuem e repassam:** outro processador, crossover ativo, equalizador, distribuidor de linha, transmissor sem fio;
  4. **só recebem:** interface de áudio, gravador, mesa.
- Os **alto-falantes passivos** ficam numa saída ou num bridge de um equipamento do grupo 2, e pode haver mais de um por saída.
- As categorias são uma lista aberta: tweeter, driver, médio, médio-grave, woofer, coaxial, subwoofer, shaker… e o usuário pode criar outras.
- O que não se sabe (modelo, dados técnicos) fica **genérico**.

**As saídas do app.**
- O que se ajusta é o **canal do processador**.
- A saída na tela é um canal, ou um par vizinho com o **link** ligado, que vira uma saída só e escreve nos dois. O link é automático: liga quando os dois canais alimentam o mesmo bridge e desliga quando não (decisão do Denis, 29/10/2026).
- O nome vem dos equipamentos que o canal alimenta (no Casa 2.1: JBL L, JBL R, Sub). Sem equipamento, aparece CH1 a CH4.
- A cor é a do canal (a do canal mestre, num par).
- As abas das telas de função, os cartões do Início e os canais da Mesa saem dessa lista, na ordem dos canais.

**Folha de equipamento.**
- Sobe de baixo, com um campo por linha:
  - **grupo** (os quatro, mais "alto-falante passivo");
  - **categoria** (lista aberta, com "Outra…" para escrever);
  - **nome** (sugerido pela categoria e pela posição, por exemplo "JBL R");
  - **modelo** ("genérico" quando vazio);
  - **função acústica** (sugerida pela categoria e pelo corte do canal, por exemplo um passa-baixa em 150 Hz sugere grave ou sub; o Denis pode trocar);
  - **ficha** (todos os dados opcionais; o que não se sabe fica em branco): diâmetro, impedância, potência RMS, sensibilidade, faixa de frequência; no alto-falante, Re medida, Fs, Qes, Qms, Vas, Bl, Sd, Xmax e Le; na caixa, tipo, volume, Fb e área do duto; no módulo, potência e tensão de ceifamento por canal e em ponte e o ganho medido; na fonte, tensão e corrente. Cada dado mostra um selo de **origem**: oficial (com o endereço do manual ou da ficha), outra fonte, medido, estimado ou digitado; só oficial e medido aparecem como verificados (✓), e um dado sem origem confirmada nunca aparece como verificado;
  - **modelo que se completa:** ao digitar o modelo (com o fabricante), a **base de modelos** do celular sugere os já guardados do mesmo grupo, e escolher um preenche a ficha com as origens dele;
  - **onde está ligado**:
    - grupos 1, 3 e 4: os canais do processador ou o equipamento do grupo 3 que alimenta a entrada;
    - grupo 2: os canais dele e quais estão em bridge;
    - alto-falante passivo: a saída ou o bridge do módulo.
- ✓ guarda; nada vai pelo BLE.
- **Tirar** pede confirmação ("Tirar a JBL R do Casa 2.1?"); um módulo com alto-falantes ligados avisa que eles ficam sem ligação.

**Esquema do Setup** (CAP-17).
- Fica no alto da tela Setup: **2D com sensação de 3D** (perspectiva isométrica, profundidade e sombra).
- À esquerda fica o processador com os 4 canais na cor de cada um. Os cabos saem em direção aos equipamentos, abrindo em Y quando um canal vai para mais de uma entrada. Depois vêm os equipamentos dos grupos 1 a 4, e por último os alto-falantes nas saídas dos módulos. O bridge aparece como uma chave juntando os dois canais do módulo.
- Cada categoria tem a sua forma, gerada pelo código e proporcional aos dados técnicos:
  - tweeter: domo pequeno;
  - driver: corneta;
  - médio-grave e woofer: cone, pelo diâmetro;
  - coaxial: cone com domo no centro;
  - subwoofer: caixa grande com cone;
  - caixa ativa: caixa com as vias de dentro;
  - módulo: bloco com os canais.

  Uma categoria nova usa a forma da função acústica dela.
- **Interação:**
  - arrastar gira o ângulo de visão entre posições isométricas;
  - os dedos aproximam;
  - um toque num equipamento abre a folha dele;
  - um toque num canal destaca tudo o que ele alimenta.
- O esquema muda **na hora**, sem botão de atualizar, sempre que um equipamento entra, sai ou muda.
- O 3D de verdade fica para uma versão futura.

## Voice and Tone

Esta seção trata dos textos da tela (microcopy); a voz da marca está em DESIGN.md › Brand & Style.

Curto, direto, um passo por vez, dizendo o que conferir. Em português do Brasil, com vírgula decimal, sinal de menos verdadeiro (−) e a unidade sempre ao lado.

Os textos abaixo ilustram o padrão; o texto final de cada estado está em State Patterns.

| Do | Don't |
|---|---|
| "Gire o botão ou toque em − e +" | "Use os controles para ajustar o parâmetro desejado" |
| "Toque no número para escrever" | Campo sem dica |
| "Mexer muda o som **na hora**. Para guardar, toque em **Salvar**; é um passo à parte." | "Alterações não salvas!" |
| "Escrevendo o ganho. Toque em ✓ para aplicar. Até lá, o som continua em +2,6 dB." | Aplicar sem dizer quando |
| Folha do Salvar diz o que vai para onde: "Regulagem → memória MUSICA ORIG" · "15 bandas → preset Rock" | "Salvar alterações?" |
| "Atualizar o preset Rock?" + "**Vale para toda memória que usa ele.**" [ASSUMPTION na redação] | Atualizar sem dizer quem mais muda |
| "Atraso do Sub: 80 cm no Casa 2.1, 0 cm no aparelho" | "Estado inconsistente" |
| "Som diferente da FILME" + **Reaplicar** | "Estado alterado" |
| "Som do aparelho" quando o som não bate com nenhuma memória do celular | "Memória 2", ou um nome que o app não sabe |
| "Ligar a chave do Gerador?" + o risco escrito da spec | "Atenção: operação perigosa" |
| "Senha errada. Digite de novo (tentativa 2 de 3)" | "Erro de autenticação" |
| "Desconectado. Ligue o som e fique perto dele." | "Falha na conexão BLE" |
| "Controles travados enquanto a IA trabalha" | Controle travado sem dizer por quê |
| "IA ajustando o EQ da FILME" | "Processando…" |
| "Aplicado pela metade" + **Terminar** e **Voltar à cópia** | Silêncio depois de uma queda |
| "Playlist da FILME vazia. Toque em Adicionar músicas." | "Nenhum item" |
| "O Bluetooth de áudio do processador está desligado." + Ligar | "Erro de reprodução" |
| Nomes por extenso: Passa-alta, Passa-baixa, Limitador, Atraso | HPF, LPF, Limiter, Delay na tela (PA e PB só nos resumos, com a legenda "PA passa-alta · PB passa-baixa") |
| Botão diz o que faz: "Escolher o Setup", "Criar preset novo", "Deixar desligada" | "OK", "Sim", "Não" |
| Valor fora da faixa: "Fora da faixa: −45,0 a +15,0 dB" [ASSUMPTION na redação] | Recusar sem dizer a faixa |

## Component Patterns

Esta seção trata do comportamento; o visual de cada peça está em DESIGN.md › Components, com o mesmo nome.

### Navegação

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Barra inferior** | Início, Mesa, Áudio, Player | Troca de destino sem perder o estado de cada um. Some nas telas cheias e na IA ativa. |
| **Engrenagem** | Topo do app | [ASSUMPTION] Abre Configurações em tela cheia, com ‹. |
| **Cabeçalho de tela cheia** | Telas cheias | ‹ volta. O subtítulo diz a rota e a memória em uso ("Saídas 3+4 em ponte · rota A+B · FILME"). [ASSUMPTION] Fica fixo no topo; o meio da tela rola. |
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
| **MASTER** | Mesa | Mesmo comportamento do canal, de −80,0 a 0,0 dB, passo 0,1. Vai para a memória. |
| **Chips da Mesa** | Mesa | [ASSUMPTION] "Remote out" é só leitura (muda em Configurações); "EQ gráfico" abre a tela EQ 15 bandas. |
| **Capa do fader** | Faders | Arrastar a capa é o mesmo que arrastar o fader; tocar no trilho não pula o valor. [ASSUMPTION] |
| **Faixa de monitoração** | Mesa | Voltímetro e um ponto de clip por saída e outro para a entrada, ao vivo. [ASSUMPTION] O clip fica aceso 3 s depois do último aviso do aparelho; o Sub acende se o canal 3 ou o 4 clipar. Tocar na faixa abre Voltímetro e clip. |
| **Botão giratório** | Ajuste da saída (ganho) | Arrastar em arco gira o valor; [ASSUMPTION] o arco mostra a posição real dentro de −45,0 a +15,0 dB. Tocar no número central vira campo (com teclado); [ASSUMPTION] o número tem sublinhado tracejado e a dica "Toque no número para escrever". − e + grandes dão o passo de 0,1 dB. |
| **Controle de parâmetro** (fader + campo + − e +) | Ajuste e telas de função | **Vem recolhido**: mostra só o nome, a faixa e o passo, e o valor no campo ([recolhido](imports/escolha-9-parametro-recolhido.png)). Tocar no valor ou no indicador de expandir abre o controle completo ([aberto](imports/escolha-8-parametros-abertos.png)): três formas no mesmo controle (arrastar o fader, escrever no campo, tocar em − e +) e, nos filtros, o tipo do filtro. Com ele aberto, tocar no valor abre o teclado. [ASSUMPTION] Um controle aberto por vez; abrir outro recolhe o anterior. O ganho no botão giratório do Ajuste fica sempre aberto. Decisão do Denis em 21/10 (ajuste fino da regra "nada de tocar para editar"). |
| **Botões − e +** | Giratório, controles de parâmetro | Um toque dá um passo. **Segurar repete o passo.** [ASSUMPTION] A repetição começa depois de 0,4 s e acelera depois de 2 s; o envio ao aparelho segue o Ritmo de envio (Interaction Primitives). |
| **Campo numérico** | Todo valor | Tocar abre o teclado numérico do Android com ✓. O valor só vale no ✓; até lá o som não muda. Fora da faixa: recusado, com a faixa escrita ao lado, e o valor anterior volta. [ASSUMPTION] Fechar o teclado sem ✓ descarta. |
| **Teclado** | Campos | O do Android: numérico com ✓ para valores, alfanumérico para senha e nomes. |
| **Lista recolhida** | Tipo do filtro, rota, preset de EQ do aparelho, velocidade do sweep, toque durante a IA | Mostra só o escolhido. Tocar na linha abre a grade de opções; tocar numa opção aplica e recolhe; a seta para cima recolhe sem mudar. [ASSUMPTION] Uma lista aberta por vez. Números e níveis nunca recolhem; [ASSUMPTION] fase e botões de memória também não. |
| **Par de botões** | Lig./Desl., Fase 0°/180°, links | [ASSUMPTION] Tocar no lado não escolhido aplica na hora. Ligar uma chave com risco abre a folha antes; [ASSUMPTION] desligar não confirma. [ASSUMPTION] Remote out e Bluetooth de áudio ligam na própria linha. |
| **Botão OFF** | Limitador | Liga e desliga o limitador (no fio, limiar 0,5 = OFF). [ASSUMPTION] Com OFF, o campo mostra "OFF" e o fader fica esmaecido; tocar de novo volta ao último limiar. |

### Memória

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Seletor de Setup** | Segunda linha do cabeçalho do Início, ao lado da pílula de status ("Setup Casa 2.1 ›") | Mostra o Setup em uso. Tocar abre **Trocar de Setup** (ver Conectar e Setup). Não há troca solta. [ASSUMPTION] Com Seguir sem Setup, diz "Sem Setup ›" e tocar abre a mesma tela. |
| **Cartão da memória em uso** | Início | Nome da memória e, embaixo dele, o **chip de preset**; depois, os botões de memória. O nome aparece só quando o app sabe o que toca (ver Conectar e Setup › O que o cartão da memória em uso mostra). O MASTER fica na Mesa. Sem selo HERDADO. Só leitura, menos o chip, os botões e o aviso. Na IA ativa diz "a IA está mexendo nela". [ASSUMPTION] Nome longo: a letra diminui até o tamanho da lista de memórias e depois quebra em duas linhas; nunca corta. |
| **Chip de preset** | Cartão da memória em uso ("Preset: Rock ▾") | Tocar abre a **folha de presets** [ASSUMPTION], com uma linha de escolha por preset, e troca ali mesmo: aplica só as 15 bandas por cima da memória, sem confirmação (cópia antes, releitura). Cada memória do celular **lembra o último preset usado com ela**; ao aplicar a memória, o preset dela volta junto. Sem memória do celular em uso, não há chip. |
| **Botões de memória** | Início; topo da Mesa | **3 botões** das memórias do celular usadas por último no Setup em uso (contando a que está em uso) e o botão **"…"**. Quem fica à vista é o app que decide. Os 3 **não mudam de lugar**: a memória que entra ocupa o lugar da usada há mais tempo; as outras duas ficam onde estão. Um toque troca para uma das 3; dois toques ("…" e escolher) para as outras. Tocar numa memória que não está em uso **aplica** essa memória, **sem confirmação** (ver Aplicando memória). [ASSUMPTION] Com menos de 3 memórias no Setup, aparecem só as que existem, mais o "…". [ASSUMPTION] Ao apagar uma memória que está num dos 3, a vaga fica com a próxima usada por último, no mesmo lugar; sem outra, fica vazia. |
| **Lista de memórias** ("…") | Início, Mesa | [ASSUMPTION] Sobe de baixo, como uma folha, e fecha ao escolher. As memórias do celular do Setup em uso, em linhas de escolha. Tocar numa delas **aplica** (como os botões). As memórias do processador não aparecem (decisão do Denis, 29/10/2026). |
| **Linha de escolha** | Lista de memórias, folha de presets, lista de Setups | [ASSUMPTION] Uma linha por item: nome e detalhe (preset lembrado), com o selo HERDADO à direita quando é herdado. A que está em uso fica em branco. Tocar escolhe e fecha a folha. Travada (Seguir sem Setup), diz por quê. |
| **Botão principal "Salvar"** | Fixo no pé do Ajuste e das telas de função [ASSUMPTION] | Um só Salvar para tudo. Abre a folha do Salvar. Trava quando nada mudou ou sem Setup (ver Salvar por camadas). [ASSUMPTION] Some enquanto o teclado está aberto. |
| **Folha do Salvar** | Sobe do botão Salvar ([mock da 3b, quadro 3](mockups/key-screens-3b.html)) | "Salvar o que mudou?" + "Cada mudança vai para o lugar dela." Uma linha por camada ("Regulagem → memória MUSICA ORIG", "15 bandas → preset Rock") e, embaixo de cada uma, o que mudou, com o antes e o depois ("Atraso do Sub: 80 → 85 cm"). Os casos e as perguntas estão em **Salvar por camadas**. Nada é guardado antes do botão branco. |
| **Cartão de memória** | Memórias | Memórias do celular do Setup em uso, em quantidade livre: nome, selo EM USO na que toca e os botões **Aplicar**, **Renomear** e **Apagar**. Aplicar faz o mesmo que os botões de memória. Renomear abre o campo do nome (vale no ✓). Apagar: ver o estado **Apagar memória**. **Criar memória do som atual**: ver Salvar por camadas; [ASSUMPTION] a playlist começa vazia, o preset lembrado é o preset em uso e a memória nova passa a ser a memória em uso. As memórias do processador não têm cartão (decisão do Denis, 29/10/2026). Criar, renomear e apagar no celular não mandam nada pelo BLE. |
| **Selo herdado** | Setups | Marca o que veio dos protótipos (CAP-6): o Setup **Casa 2.1**, com os equipamentos e as fichas. Não há memória nem preset herdados (decisão do Denis, 29/10/2026). **Não aparece no Início**, para o Início ficar leve (decisão do Denis, 23/10). [ASSUMPTION] Tocar no selo abre uma folha só de leitura com a nota de `conhecimento-herdado.md`. DIALOGO e NOITE não entram, porque são `.trps` cifrados. |

### Folhas e avisos

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Folha de confirmação** | Salvar (camadas), apagar memória, ligar chave com risco, apagar preset [ASSUMPTION], apagar Setup [ASSUMPTION], pedido de risco da IA, aplicado pela metade, reset, troca de senha | [ASSUMPTION] Sobe de baixo; o botão branco faz e o contornado desiste. [ASSUMPTION] Tocar no véu ou em Voltar desiste. Nada é enviado ao aparelho antes do botão branco. |
| **Aviso de risco** | Dentro da folha | Repete o risco escrito em `chaves-de-acesso.md`, sem mudar o texto. |
| **Aviso de erro** | Senha, valor fora da faixa | Texto curto que diz o que fazer. |
| **Aviso de som diferente** | Cartão da memória em uso | "Som diferente da FILME" e **Reaplicar**, que aplica a memória ativa como os botões de memória (sem confirmação, cópia antes, releitura). Não muta. [ASSUMPTION] Some ao Reaplicar, ao aplicar outra memória ou ao Salvar. |
| **Lembrete** | Ajuste, telas de função, Memórias, Chaves | Texto fixo explicativo; não é tocável. |
| **Botão secundário** | Folhas, linhas de Configurações | Desiste ou faz uma ação secundária; quando travado, diz por quê ("Desligado pela chave"). |

### IA

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Faixa da IA** | IA ativa | Diz em uma linha o que a IA faz agora. **PARAR IA** revoga o acesso da IA na hora (sem confirmação) e volta para a tela de antes. |
| **Painel ao vivo** | IA ativa | Mostra os valores que a IA mexe, atualizados em até 1 s, com o item atual em lilás e a linha "Agora: banda 2,5 kHz · 0,0 → +1,0 dB". O toque segue a configuração da IA (ver Uso junto com a IA). |
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
| **Faixa do som mutado** | Setup em dúvida | Diz que os 4 canais estão mutados até a escolha. Só leitura; fica enquanto o mudo da dúvida estiver no aparelho. |
| **Lista do que não bate** | Setup em dúvida | Uma linha por diferença de corte, atraso, fase ou rota, com o valor da memória ativa e o do aparelho. Só leitura. [ASSUMPTION] Com muitas diferenças, a lista rola e os três botões ficam fixos no pé. |

## State Patterns

| Estado | Onde | Tratamento |
|---|---|---|
| **Primeira abertura** | Início, Setups, Memórias, Presets | Conhecimento herdado já carregado (CAP-6): Setup **Casa 2.1**, com os equipamentos e as fichas; nenhuma memória e nenhum preset do celular. O selo HERDADO aparece só em Setups. Na primeira conexão, o Casa 2.1 ainda não tem memória: vale o **Setup em dúvida**, e Escolher o Setup desmuta sem aplicar nada. Sem senha guardada, Conectar leva às caixas da senha. |
| **Conectando** | Pílula de status, Início | Ponto cinza, "Conectando". Com senha guardada, o app envia a senha sozinho. Depois da senha vem a leitura completa (84 parâmetros, ~13 s) e a conferência do Setup. [ASSUMPTION] O Início mostra "Lendo o aparelho" com a contagem, e os valores aparecem só quando lidos. |
| **Conectado** | Todas | Ponto verde, "Conectado · R003". Ao conectar ou reconectar, o app **relê tudo antes de qualquer ação**, inclusive depois de usar o app da Taramps. |
| **Setup confere** | Ao conectar | Tudo bate com a memória ativa: entra direto, sem pergunta. O cabeçalho do Início mostra o Setup; o cartão mostra a memória ativa. |
| **Som diferente da memória ativa** | Início, ao conectar | Só ganho, limitador, EQ ou MASTER diferem: entra direto, **sem mutar**. O cartão mostra "Som do aparelho" e o aviso "Som diferente da FILME" com **Reaplicar**. |
| **Setup em dúvida** | Tela cheia, ao conectar ([mock da 3b, quadro 2](mockups/key-screens-3b.html)) | Corte, atraso, fase ou rota não batem, ou o Setup não tem memória: os 4 canais mutados e a tela Setup em dúvida. Ver Conectar e Setup. |
| **Dúvida interrompida** | Ao conectar | O app fechou ou a conexão caiu com a tela Setup em dúvida aberta. O mudo continua no aparelho. Ao reconectar, o app lê o mudo e volta à tela Setup em dúvida, que confere de novo. [ASSUMPTION] O app guarda no celular que deixou o aparelho mutado; se o mudo foi tirado pelo app da Taramps, a conferência segue normal. |
| **Trocando de Setup** | Tocar no nome do Setup no Início | A mesma tela, com o título "Trocar de Setup" e ‹. Ver Conectar e Setup. |
| **Reaplicando a memória ativa** | Ao conectar, depois da conferência | Se o processador perdeu o estado ao desligar (a história 12 mede), o app reaplica a memória ativa do Setup só **depois** de o Setup ser escolhido: cópia antes, aplica, relê, desmuta. Nada toca antes. [ASSUMPTION] Os valores ficam esmaecidos com "Aplicando a MUSICA ORIG" até a releitura. |
| **Seguir sem Setup** | Todas | Som desmutado e aparelho como está; o cartão mostra "Som do aparelho". Memórias do celular travadas até escolher um Setup: [ASSUMPTION] os 3 botões e a lista ficam esmaecidos com "Escolha um Setup para usar as memórias do celular." [ASSUMPTION] O seletor diz "Sem Setup ›". Sem Setup em uso, os links 1–2 e 3–4 ficam **desligados** (decisão do Denis, 24/10/2026): cada canal é uma saída, e nada é espelhado sem querer numa instalação desconhecida. |
| **Som do aparelho** | Início, Mesa | O app não sabe qual memória toca (ver Conectar e Setup › O que o cartão da memória em uso mostra). Sem chip, nenhum botão em branco, Player sem playlist. Salvar cria uma memória nova do celular (ver Salvar por camadas). |
| **Desconectado** | Início (telas-chave 6a) | Cartão "Desconectado" grande com a hora em que caiu e o botão Conectar. Saídas vazias com "—": nunca mostrar valor que não veio do aparelho. Nas outras telas, [ASSUMPTION] os controles ficam travados com "Sem conexão" e um Conectar. O Player continua tocando (é Bluetooth de áudio, não o BLE). |
| **App da Taramps conectado** | Conectar | Só cabe uma conexão BLE. [ASSUMPTION] Se as 3 tentativas falharem: "Não conectou. Se o app da Taramps estiver aberto, feche ele e tente de novo." |
| **Senha errada** | Conectar (telas-chave 6b) | Caixas limpas, "Senha errada. Digite de novo (tentativa N de 3)". Nenhuma escrita é enviada. Na terceira recusa, desconecta e volta ao Desconectado. |
| **Só leitura** (firmware ≠ R003) | Todas | [ASSUMPTION] Pílula "Só leitura · firmware Rxxx" e um aviso fixo no Início: "Firmware diferente do R003. O app só lê; nada é enviado." Todos os controles ficam travados; a porta da IA responde só a leituras. |
| **Chave desligada** | Bloco de função (inclui o bloco Gerador), linhas de Configurações | Travado, com "Chave desligada" ou "Desligado pela chave". A operação é recusada antes de qualquer envio pelo BLE. |
| **Escrevendo um valor** | Qualquer campo (v2 quadro 4) | Teclado aberto, campo ativo com contorno na cor, explicação de que o som só muda no ✓. |
| **Eco diferente do pedido** | Qualquer controle | [ASSUMPTION] O controle mostra o valor relido do aparelho (não o pedido) e um aviso curto "O aparelho ficou em X". |
| **Avançado aberto** | Crossover (telas-chave 2b), Ajuste, Configurações | Fundo mais claro; os controles dentro seguem as regras normais. |
| **Passa-alta ou passa-baixa em OFF** | Crossover (telas-chave 2a), Ajuste | [ASSUMPTION] A frequência some da tela e volta junto com o tipo escolhido. |
| **Link ligado** | Telas de saída | O par vira **uma saída só** nas abas, nos cartões e na Mesa, com o nome dos equipamentos e a cor do canal mestre; mexer nela escreve nos dois canais. O link é automático: liga quando os dois canais alimentam o mesmo bridge (no Casa 2.1, o CH3 e o CH4) e desliga quando não; não há controle de link. Propostas de 23/10 (equipamentos) e 29/10. |
| **Aplicando memória** (celular) | Início, Mesa, lista "…", Memórias, Reaplicar | Sem confirmação. Passa pela chave "Aplicar memória do celular", guarda a cópia (failure-modes 3), escreve a regulagem da memória e o preset que ela lembra, e relê. [ASSUMPTION] Os valores ficam esmaecidos com "Aplicando a FILME" até a releitura. |
| **Salvando** | Folha do Salvar | Guarda no celular; nada vai pelo BLE, então não há queda no meio do salvar. |
| **Aplicado pela metade** | Ao reconectar | Folha "Aplicado pela metade": aplicar memória do celular ou aplicar preset caiu no meio. Botões **Terminar** (branco) e **Voltar à cópia** (contornado). [ASSUMPTION na redação] Uma marca de carregar memória do aparelho guardada de antes de 29/10/2026 também abre esta folha, mas só **Voltar à cópia** funciona; Terminar diz "O app não sabe refazer esta operação." |
| **Preset aplicado** | Chip de preset, Presets | Cópia antes, aplica só as 15 bandas, relê. A memória em uso passa a lembrar esse preset. [ASSUMPTION] "Preset Rock aplicado na MUSICA ORIG." |
| **Presets vazio** | Presets no celular | [ASSUMPTION] "Mexa nas 15 bandas e toque em Salvar para criar um preset." É o estado da primeira abertura. |
| **Apagar preset** | Presets | Folha de confirmação "Apagar o preset X?" [ASSUMPTION]. [ASSUMPTION] Um preset lembrado por alguma memória não se apaga: Apagar fica travado com "Lembrado por 2 memórias." |
| **Setup sem equipamentos** | Início, Mesa, Áudio, Setup | As saídas aparecem como **CH1 a CH4** (os pares com link, como "CH3 + CH4"), na cor de cada canal. Na tela Setup, o esquema mostra só o processador e a frase "Adicione os equipamentos ligados em cada canal." com **Adicionar equipamento**. |
| **Setup sem memórias** | Início, Memórias | [ASSUMPTION] Logo depois de Criar Setup novo: o cartão diz "Nenhuma memória" e oferece **Criar memória do som atual**; sem chip de preset; a lista "…" não tem nenhuma memória. |
| **Apagar memória** | Memórias | [ASSUMPTION] Folha "Apagar a MUSICA ORIG?" + "**A memória e a playlist dela somem.**" Na memória em uso, Apagar fica travado com "Esta memória está em uso." [ASSUMPTION na redação] |
| **Apagar Setup** | Setups | [ASSUMPTION] Folha "Apagar o Setup Carro?" + "**O Setup, as memórias e as playlists dele somem.**" O Setup em uso e o Casa 2.1 herdado não se apagam. |
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
| **IA pede operação de risco** | Qualquer tela | Folha de confirmação: "A IA pede para aplicar a memória FILME" com o risco da chave ("Troca todo o som de uma vez."). Só roda depois do botão branco. |
| **IA bloqueada pela dúvida** | Setup em dúvida | A porta recusa os comandos da IA até o Denis escolher. [ASSUMPTION] A recusa diz à IA "Setup em dúvida; escolha na tela." |
| **Histórico vazio** | Histórico | [ASSUMPTION] "Nenhum comando ainda." |
| **Playlist vazia** | Player | "Playlist da FILME vazia." e o botão Adicionar músicas. |
| **Sem playlist** | Player | Sem memória do celular em uso. [ASSUMPTION] "Sem memória do celular em uso, não há playlist. Aplique uma memória do celular." Tocar fica travado. |
| **Arquivo sumiu** | Player | A faixa fica na lista em apagado com "Arquivo não encontrado", é pulada ao tocar e pode ser removida no modo Editar. |
| **Bluetooth de áudio desligado** | Player | Aviso no topo: "O Bluetooth de áudio do processador está desligado." com o botão **Ligar** (comando 251, chave ligada por padrão, sem confirmação). Tocar fica travado até ligar. |
| **Celular não está mandando o som ao processador** | Player | [ASSUMPTION] "O som está saindo pelo celular. Conecte o Bluetooth do celular ao processador." com atalho para as configurações de Bluetooth do Android. |
| **Sem permissão para ler músicas** | Player | [ASSUMPTION] "O app precisa de acesso às músicas do celular." com o botão Permitir (pedido do Android). |

## Interaction Primitives

- **Mexer muda o som na hora.** Arrastar, tocar em − e + e escolher numa lista vão para o aparelho na hora. **Salvar é uma ação à parte**, com um só botão (ver Salvar por camadas).
- **Valor escrito vale no ✓.** Até o ✓, nada é enviado. Fora da faixa é recusado, com a faixa certa escrita ao lado.
- **Segurar − ou + repete o passo.**
- **Passos de − e +** (MAPA §4): ganho e MASTER 0,1 dB; limitador 0,1 dB; EQ (ganho) 0,1 dB; Q 0,1; atraso 0,5 cm; ataque 0,1 ms; liberação 1 ms. **Frequências: passo proporcional de 1/12 de oitava** (multiplica ou divide por 2^(1/12)), arredondado para Hz inteiro e [ASSUMPTION] com no mínimo 1 Hz de mudança; o valor exato se escreve no campo.
- **Faixas** (MAPA §4): MASTER −80,0 a 0,0 dB; ganho da saída −45,0 a +15,0 dB; frequências 10 a 22 000 Hz; limitador −48,0 a 0,0 dB (ou OFF); atraso 0 a 275 cm; EQ ±12,0 dB; Q do EQ de saída 0,1 a 10,0; Q do EQ de entrada 0,4 a 10,0; ataque 0,1 a 100 ms; liberação 1 a 1600 ms; gerador −60 a 0 dB.
- **Escalas:** frequência e Q em escala logarítmica ([ASSUMPTION] no Q); dB e cm lineares; o 0 dB marcado no trilho.
- **Nomes:** [ASSUMPTION] memória, Setup e preset têm até 16 caracteres; o campo não aceita mais.
- **Ritmo de envio** (MAPA §2): uma resposta pendente por vez; cerca de 30 ms entre escritas; durante o arrasto ou a repetição de − e +, no máximo um envio a cada 300 ms por parâmetro. [ASSUMPTION] Ao soltar, o último valor sempre é enviado.
- **Eco e releitura:** a tela mostra o valor que o aparelho confirmou.
- **Abrir uma tela nunca escreve nada no aparelho.** [ASSUMPTION] Herdado do cuidado com o app da Taramps, que reescreve ataque e liberação ao abrir o Limiter em Auto.
- **Cópia antes de mexer em vários parâmetros:** antes de aplicar uma memória do celular ou aplicar um preset, o app guarda o estado (failure-modes 3).
- **Aplicar:** memória do celular se **aplica** (a regulagem dela, mais o preset que ela lembra), com cópia antes e releitura. O app não carrega memória do aparelho (`241`); isso fica no app da Taramps.
- **Nada toca antes de conferir o Setup.** Na dúvida, os 4 canais ficam mutados no aparelho até a escolha.
- **Toque, arrasto e escrita.** Sem deslizar para aplicar ou apagar (o app da Taramps carrega uma memória ao deslizar; aqui não). O único arrasto de lista é a alça ≡ da playlist no modo Editar.
- **Proibidos:** tocar num bloco para entrar em edição; esconder um número ou nível atrás de um toque; confirmação para mexer em ajustes de áudio, para aplicar memória ou para trocar de preset.

## Accessibility Floor

Esta seção trata do comportamento; o contraste visual está em DESIGN.md › Colors. **Não herdar** a acessibilidade da Vitrine e dos Bastidores do BGS Sound Lab.

- **Alvos de toque ≥ 44 px** em tudo ({spacing.alvo-min}); − e + dos parâmetros com {spacing.alvo-passo}; − e + do ganho com {spacing.alvo-passo-grande}; PARAR IA e tocar/pausar com {spacing.alvo-parar}. Os botões da grade de tipos de filtro sobem para 44 px. [ASSUMPTION]
- **Contraste mínimo de 4,5:1** para todo texto de leitura, inclusive rótulos e escalas ({colors.apagado} já passa). Só texto grande em negrito pode ficar entre 3:1 e 4,5:1 (PARAR IA).
- **Texto mínimo de 11 px**, contando escalas e rótulos do EQ; valores mexíveis ≥ 22 px.
- **Escala de fonte do Android:** o app acompanha. Telas rolam em vez de cortar; alvos não diminuem; números grandes podem quebrar a unidade para baixo, nunca o número.
- **Cor nunca sozinha:** cada saída sempre com o nome escrito; o clip tem o nome do canal embaixo do ponto; o escolhido também muda o peso; o travado diz por quê.
- **TalkBack:** [ASSUMPTION] cada controle com nome, valor e unidade ("Ganho do Sub, mais 2,6 decibéis"); − e + com "Diminuir" e "Aumentar"; o giratório e os faders expõem ajuste por passo; o clip anuncia "Clip no Sub".
- **Movimento:** [ASSUMPTION] o pulso da IA respeita "remover animações" do Android e fica parado.
- **Confirmações** dizem o que some, e o botão de fazer é diferente do de desistir em cor e em texto.

## Key Flows

### Fluxo 1 — Refazer a MUSICA ORIG como memória do celular, só pelo celular (Denis, sala, celular na mão)

Teste 1 do sinal de sucesso.

1. Denis abre o app. A senha está guardada; a pílula passa de "Conectando" a "Conectado · R003" e o app lê o aparelho. É a primeira vez, e o Casa 2.1 ainda não tem memória: sobe a tela **Setup em dúvida** com os 4 canais mutados; ele toca em **Escolher o Setup** › **Casa 2.1**, e o app desmuta sem aplicar nada. O cabeçalho mostra **Setup Casa 2.1 ›**.
2. No **Início**, o cartão diz **Nenhuma memória**. A MUSICA ORIG ainda não existe no celular; a antiga é a memória 2 do aparelho, feita no app da Taramps; ela não aparece no app e não é referência.
3. Ele vai à **Mesa** e acerta os ganhos: arrasta a capa do Sub, toca no número da JBL L, escreve o valor e toca em ✓. O som muda a cada passo; a faixa de monitoração mostra se algum canal clipa.
4. Em **Áudio** › **Limitador**, ele escolhe a aba de cada saída e segura + até o limiar que quer.
5. Ele ouve e volta ao passo 3 quantas vezes quiser. Nada foi salvo ainda.
6. Em **Configurações › Memórias**, ele toca em **Criar memória do som atual**, escreve "MUSICA ORIG" e toca em ✓. A memória nova guarda a regulagem que está tocando, no Setup Casa 2.1, vira a memória em uso e entra num dos 3 botões do Início, no lugar da usada há mais tempo. [ASSUMPTION] O aviso customizado da CAP-14 aparece aqui: "Memória MUSICA ORIG criada no Casa 2.1."
7. Ele desliga o processador, liga de novo e toca em **Conectar**.
8. **Clímax:** o app relê e confere o Setup. Se o aparelho guardou o som, tudo bate com a MUSICA ORIG, e o app entra direto nela. Se perdeu, o corte e o atraso não batem: sobe a tela **Setup em dúvida** com os 4 canais mutados; ele toca em **Escolher o Setup** › **Casa 2.1**, e o app reaplica a MUSICA ORIG, relê e desmuta. Nos dois casos, o som fica igual ao da memória. Ele não abriu o app da Taramps em nenhum momento.

Falha: a conexão cai no meio da reaplicação → ao reconectar, vale o estado **Aplicado pela metade**.

[NOTE FOR UX] O exemplo da CAP-14 no SPEC ("ao sobrescrever uma memória") ficou velho: sobrescrever saiu na 3b. Vale atualizar o SPEC com o aviso do passo 6.

### Fluxo 2 — A IA ajusta o EQ da FILME e o Denis vê ao vivo (Denis, no sofá, notebook ao lado)

Teste 2 do sinal de sucesso.

1. Antes, uma vez só: em **Configurações › IA pela rede**, Denis toca em **Lig.**; a folha mostra "Deixa a IA comandar o som pela rede." e ele toca em **Ligar a chave**. O app mostra o código de 6 dígitos; ele passa o código ao Claude Code, que pareia. Na mesma tela, o toque durante a IA fica no padrão: **travar até PARAR IA**.
2. No notebook, ele pede à IA para ajustar o EQ da FILME, uma memória que ele criou no celular.
3. A IA pede para aplicar a memória **FILME** do celular. Como é operação de risco para a IA, sobe a folha do estado **IA pede operação de risco**; ele confirma. A FILME volta com o preset que ela lembra.
4. O app abre a **IA ativa**: faixa "IA ajustando o EQ da FILME", painel ao vivo com as 15 bandas e os ganhos e limitadores, registro "O que a IA fez" e o aviso de controles travados (estado **IA ativa, travado**).
5. Cada banda que a IA mexe acende em lilás e se move em até 1 s; a linha "Agora" mostra o valor de antes e o de depois; o registro ganha uma linha. A cada 3 ajustes seguidos, a IA espera ele tocar em **Continuar** (chaves-de-acesso.md).
6. Ele toca sem querer num fader: aparece o aviso do estado **IA ativa, travado**. Nada muda.
7. A IA termina e diz no notebook: "Pronto, não salvei." A IA não salva (a porta não tem esse comando).
8. **Clímax:** ele toca em **PARAR IA**. O acesso da IA é revogado na hora, a tela volta para onde estava e o **Histórico** guarda cada comando, com origem IA, hora e valor.
9. Ele ouve. Se gostar, vai em **Áudio › EQ 15 bandas** e toca em **Salvar**. A folha mostra "15 bandas → preset Ação" e pergunta se atualiza o Ação ou cria um preset novo. Ele escolhe e confirma.

Variante: com a opção **pausar ao toque** escolhida em IA pela rede, no passo 6 o toque pausa a IA, os controles destravam, ele mexe, e a IA só retoma depois de 10 s sem toques.

Falha: o Wi-Fi cai → a IA some sem deixar nada pendente (cada comando é avulso); [ASSUMPTION] a IA ativa fecha e o app volta ao normal com o aviso "A IA perdeu a conexão".

### Fluxo 3 — Noite de filme (Denis, 21h, luz apagada)

1. Denis liga o som e abre o app. Pílula: **Conectando**, depois **Conectado · R003**.
2. O Início mostra a memória em uso. Ele toca no botão **FILME**; o app aplica a memória e o preset Ação que ela lembra, e relê sem perguntar; o cartão mostra **FILME** em letras grandes, com o chip **Preset: Ação ▾**.
3. Antes do filme, ele abre o **Player**: a playlist da FILME tem as trilhas que ele usa para conferir o som. Ele toca a primeira; o som sai pelo processador.
4. Começa o filme na TV. Ele pausa o Player e, na cena de explosão, toca em **Mesa**.
5. Ele segura **+** do **SUB**. O som do sub sobe na hora, passo a passo.
6. O ponto de clip do Sub acende na faixa de monitoração; ele toca uma vez em − e o ponto apaga.
7. **Clímax:** o grave chega no ponto que ele queria, sem sair do filme e sem nenhuma confirmação.
8. Ele não salva. Na próxima conexão, só o ganho difere da FILME: o app não muta, e o cartão mostra "Som do aparelho" com o aviso **Som diferente da FILME** e **Reaplicar**. Se ele tivesse gostado, teria tocado em Ajustar › **Salvar**, e a folha diria "Regulagem → memória FILME".

Variante: para uma comédia, ele toca no chip **Preset: Ação ▾** e escolhe **Diálogo** na folha de presets. Só as 15 bandas mudam, sem confirmação, e a FILME passa a lembrar o Diálogo.

Falha: a conexão não sai → o Início fica no estado **Desconectado**.

### Fluxo 4 — Montar a playlist da MUSICA ORIG (Denis, tarde de domingo)

1. No **Início**, Denis toca no botão **MUSICA ORIG**. Sem pergunta, o app aplica a memória e o preset que ela lembra, e relê.
2. Ele toca em **Player**. A tela mostra só a playlist da memória em uso, "Playlist da MUSICA ORIG", no estado **Playlist vazia**. Não há abas de memória.
3. Ele toca em **Adicionar músicas**; o seletor do Android abre; ele marca cinco arquivos e confirma. As faixas entram na lista.
4. Ele toca em **Editar**, arrasta a terceira faixa para o topo pela alça ≡ e remove uma com −. Toca em **Pronto**.
5. Ele toca em tocar/pausar. Aparece o aviso do estado **Bluetooth de áudio desligado**. Ele toca em **Ligar**; o aviso some.
6. **Clímax:** a primeira música sai pelas JBL e pelo sub, com a regulagem da MUSICA ORIG.

Para montar a playlist de outra memória, ele troca de memória no Início ou na Mesa; o Player passa a mostrar a playlist dela.

Falha: um arquivo foi apagado do celular → vale o estado **Arquivo sumiu**.

### Fluxo 5 — Conferir a leitura contra o app da Taramps (Denis, bancada, os dois apps no celular)

Teste 3 do sinal de sucesso.

A paridade é só sobre o estado atual do aparelho: este app não carrega memória (decisão do Denis, 29/10/2026).

1. No app da Taramps, Denis carrega a memória que quer conferir (por exemplo, a FILME, memória 3) e anota os valores tela a tela. Ele **não abre a tela Limiter com o Auto ligado**, porque ela reescreve o ataque e a liberação.
2. Ele fecha o app da Taramps (só cabe uma conexão BLE).
3. Ele abre o BGS Controle, que conecta, relê tudo (84 parâmetros) e confere com a memória ativa do Casa 2.1. Se o corte, o atraso, a fase ou a rota não batem (ou não há memória), sobe a tela **Setup em dúvida**, com os 4 canais mutados; ele toca em **Seguir sem Setup**, e o app desmuta sem aplicar nada. Se só o ganho ou o EQ diferem, o app entra direto, com o aviso **Som diferente**, e ele não toca em Reaplicar. Nos dois casos, o cartão mostra **Som do aparelho**: o app não diz qual memória do aparelho está tocando.
4. Ele confere pela grade **Áudio**: cada bloco mostra o resumo, e cada tela de função mostra os valores por saída, nas abas. Abre o Avançado onde precisa (ataque e liberação, links).
5. Para conferir outra memória, ele fecha o BGS Controle, carrega a memória no app da Taramps, anota, fecha o app da Taramps e conecta de novo o BGS Controle, que relê tudo. Confere como no passo 4.
6. **Clímax:** cada valor do BGS Controle bate com o que o app da Taramps mostra do som atual. As memórias do aparelho não mudam: este app não carrega nem salva memória do aparelho.

Variante, guardar um som do aparelho no celular: depois de carregar a memória pelo app da Taramps e conectar este app, com um Setup em uso (não em Seguir sem Setup) e o som do aparelho mantido, ele toca em **Salvar** num Ajuste. A regulagem vai para uma memória nova do celular (ver Salvar por camadas).

Falha: o BGS Controle não conecta → vale o estado **App da Taramps conectado**.

### Fluxo 6 — Tom de teste com prazo de 30 s (Denis, bancada, RCA dos módulos desconectado)

1. Em **Áudio**, o bloco **Gerador** está no estado **Chave desligada**. Ele toca no bloco e vai para **Chaves de acesso**.
2. Toca em **Lig.** no Gerador. A folha mostra "Um tom forte pode queimar o tweeter ou o sub." Ele toca em **Ligar a chave**.
3. Volta ao **Gerador**, escolhe a frequência e o ganho do tom fixo e toca em **Tocar**.
4. A contagem de 30 s aparece com o botão grande **Parar**. Ele mede com o multímetro.
5. **Clímax:** no zero, o app desliga o gerador sozinho, relê e mostra o gerador desligado.

Falha: a conexão cai com o tom ligado → o app tenta desligar; se 3 reconexões falharem, vale o estado **Gerador sem resposta**. Ao reconectar, vale **Gerador ligado ao reconectar**.

### Fluxo 7 — Levar o processador para o carro (Denis, garagem, processador já instalado no carro)

Aprovado pelo Denis na história 3b (CAP-16). Teste 4 do sinal de sucesso.

1. Denis abre o app no carro. O processador ainda tem o som de casa: o que foi lido bate com a memória ativa do **Casa 2.1**, e o app entra direto.
2. Ele toca no nome do Setup, **Setup Casa 2.1 ›**, no cabeçalho do Início. O som muta e abre **Trocar de Setup**.
3. Ele toca em **Criar Setup novo**, escreve "Carro" e "ESTRADA" e toca em ✓. O som que está no aparelho vira a ESTRADA, a primeira memória do Carro. Depois ele cadastra os equipamentos do carro na tela Setup.
4. Ele ajusta o carro (atraso, fase, corte) e toca em **Salvar**. A folha diz **"Regulagem → memória ESTRADA"**; ele confirma.
5. No Início, o cartão diz "Nenhuma memória". Ele toca em **Criar memória do som atual**, escreve "ESTRADA" e toca em ✓. A ESTRADA vira a memória em uso do Carro, com o chip de preset. Ele toca no chip e escolhe **Rock**; a ESTRADA passa a lembrar o Rock.
6. Dias depois, o processador volta para casa. Ao conectar, o que foi lido bate com a ESTRADA, a memória ativa do Carro, e o app entra direto no **Carro**.
7. **Clímax:** ele toca em **Setup Carro ›** e escolhe **Casa 2.1**. O app aplica a memória ativa de casa, relê e desmuta. O som de casa volta inteiro, e nada tocou antes da escolha.

Variante (o caminho da dúvida do teste 4): no carro, alguém mexe no atraso pelo app da Taramps. Na próxima conexão, o atraso lido não bate com a ESTRADA: o app muta os 4 canais e abre **Setup em dúvida** com "Atraso do Sub: …". Nada toca nem é aplicado até ele tocar em **Escolher o Setup**, **Criar Setup novo** ou **Seguir sem Setup**.

Falha: a conexão cai no meio da aplicação → ao reconectar, vale o estado **Aplicado pela metade**.

## Uso junto com a IA

Segue `porta-de-comandos.md`, `chaves-de-acesso.md` e a regra 5 de `failure-modes.md`, já atualizados com as decisões deste documento (21 e 23/10).

- **Porta desligada por padrão.** Abre só com a chave **"IA pela rede"** ligada pelo Denis, na tela.
- **Pareamento único** por código de 6 dígitos mostrado no app. Só a rede local. A senha do processador nunca passa pela porta.
- **A IA passa pelas mesmas chaves do Denis** e nunca liga ou desliga uma chave.
- **Continuar a cada 3 ajustes:** depois de 3 ajustes seguidos da IA sem um toque do Denis, o próximo espera **Continuar** na tela. Isso impede a IA de trocar o som inteiro ajuste por ajuste (decisão do Denis, 29/10/2026).
- **Risco pede confirmação na tela:** reset de fábrica, reset de áudio, troca de senha, gerador, aplicar memória do celular, remote out. Salvar, renomear e carregar memórias do aparelho não existem neste app, nem para a IA.
- **A IA não salva** (decisão do Denis, 23/10). A porta não tem comando de salvar. Ao terminar, a IA diz "Pronto, não salvei."; guardar é o Salvar do Denis.
- **A dúvida bloqueia a IA:** com a tela Setup em dúvida aberta, a porta recusa os comandos da IA até o Denis escolher.
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
| Player | **Player**, com a playlist da memória do celular em uso |
| Engrenagem (Salvar/Carregar memórias e presets, resets, nome, senha) | **Engrenagem → Configurações**: Setups [ASSUMPTION], Memórias, Presets no celular, Aparelho, Chaves, IA pela rede, Histórico; resets em Avançado |
| Memórias 1–3 do aparelho: salvar, renomear e carregar | **Continuam só no app da Taramps.** Elas não aparecem no BGS Controle (decisão do Denis, 29/10/2026). Ao conectar depois, o BGS Controle relê o aparelho. |
| Salvar (barra) | Botão **Salvar** no pé dos ajustes: guarda no celular, cada mudança na camada dona dela (ver Salvar por camadas) |
| MASTER na memória | MASTER na memória do celular, como no app da Taramps |
| Volt. (barra) | Faixa de monitoração na Mesa e Configurações › Aparelho › Voltímetro e clip |
| Deslizar uma memória carrega ou exporta | Botões escritos (Aplicar, Renomear, Apagar); nada acontece por deslizar |
| Abrir o Limiter em Auto reescreve ataque e liberação | Abrir uma tela nunca escreve |
| Presets `.trps` | Não são lidos. O preset do BGS Controle é só o EQ de 15 bandas. **Exportar**, na linha de Presets, grava o preset próprio como arquivo JSON (não `.trps`). |
| Um som feito no app da Taramps e guardado numa memória do aparelho | Vira memória do celular (decisão do Denis, 23/10): carregar a memória no app da Taramps, fechar o app da Taramps, conectar o BGS Controle e tocar em **Salvar** (ver Salvar por camadas, terceira linha). Ao conectar depois de usar o app da Taramps, o app confere com a memória ativa: se corte, atraso, fase ou rota mudaram, abre a tela Setup em dúvida; se só ganho, limitador ou EQ mudaram, entra com o aviso "Som diferente". As memórias do aparelho são intocáveis e não aparecem neste app (decisão do Denis, 29/10/2026). [NOTE FOR UX] Sem o carregar, um som do app da Taramps com corte, atraso, fase ou rota diferentes da memória ativa cai na dúvida, e Escolher o Setup aplica a memória ativa por cima dele. Num Setup sem memórias, Escolher desmuta sem aplicar, e o som fica. Falta o Denis decidir como esse som entra num Setup que já tem memória. |

## Inspiration & Anti-patterns

- **Tirado do Estúdio Noturno:** pouca coisa na tela para ler de longe; nome da memória enorme e fino.
- **Tirado do Console Tátil:** faders com capa, botão giratório, − e + grandes; regulagem tátil.
- **Recusado — Bancada Técnica e Papel Calmo:** direções não escolhidas.
