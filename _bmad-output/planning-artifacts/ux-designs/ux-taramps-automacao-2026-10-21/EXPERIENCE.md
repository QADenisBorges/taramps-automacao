---
name: BGS Controle
status: final
updated: 2026-10-23
sources:
  - ../../../specs/spec-app-controle-taramps/SPEC.md
  - ../../../specs/spec-app-controle-taramps/chaves-de-acesso.md
  - ../../../specs/spec-app-controle-taramps/porta-de-comandos.md
  - ../../../specs/spec-app-controle-taramps/failure-modes.md
  - ../../../specs/spec-app-controle-taramps/conhecimento-herdado.md
  - ../../../../protocolo/MAPA.md
  - ../../sprint-change-proposal-2026-10-23.md
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

## Foundation

- **Plataforma:** Android, app em **.NET MAUI** (projeto `BgsControle.App`, com os ViewModels em `BgsControle.Telas`). Sem iOS.
- **Forma:** celular em **retrato**, uma tela só. [ASSUMPTION] A rotação fica travada em retrato.
- **Quem usa:** uma pessoa, o **Denis**, no próprio celular, perto do som. A IA (Claude Code no notebook) é uma segunda origem de comandos, não um usuário da tela.
- **Aparelho:** PRO 2.4BT + AUDIO, firmware R003, uma conexão BLE por vez. A música do Player sai do celular pelo Bluetooth de áudio do processador.
- **Sistema de interface:** nenhum de terceiros. Navegação, teclado, seletor de arquivos e botão Voltar seguem o Android; o visual vem todo do [`DESIGN.md`](DESIGN.md), que é a referência de identidade.
- **Tema:** só escuro. Fonte Roboto variável embutida; os tamanhos acompanham a escala de fonte do Android.
- **As 3 memórias do processador:** o app só lê os nomes e carrega. Salvar e renomear ficam no app da Taramps. O processador não diz qual memória dele está tocando.

### Três camadas do som

Cada parâmetro tem uma camada dona (CAP-4, CAP-5, CAP-16; decisões do Denis de 22 e 23/10). O Salvar usa esta tabela (ver Salvar por camadas).

| Camada | O que guarda |
|---|---|
| **Setup** | Uma instalação (o 2.1 de casa, o carro…): nome e **calibração** da instalação e da sala. Calibração = atraso, fase, corte (passa-alta e passa-baixa, frequência e tipo), equilíbrio L/R (ganho e limitador), rota, link 1–2 e 3–4 e EQ paramétrico de entrada. |
| **Memória no celular** | O **caráter** do som: ganho de cada saída, limitador (limiar, ataque, liberação, Auto), EQ de saída e MASTER, como no app da Taramps. Guarda também a playlist e o preset lembrado. Pertence a um Setup; a quantidade é livre (no dia a dia, até 4 por Setup). |
| **Preset** | Só o EQ de 15 bandas, por cima da memória. |
| Fora das camadas [ASSUMPTION] | Mute, remote out, Bluetooth de áudio, gerador, nome do produto e senha. Valem na hora; o Salvar não os guarda. |

**Memória ativa** de um Setup é a última memória do celular em uso nele. É com ela que a conferência compara o caráter, e é ela que o app reaplica.

## Information Architecture

Barra inferior com quatro destinos: **Início · Mesa · Áudio · Player**. A **engrenagem** no topo abre **Configurações**. Tudo o que não está na barra abre em **tela cheia com ‹** e sem a barra. Folhas sobem por cima da tela atual, uma de cada vez.

| Superfície | Chega por | Para quê | Mock |
|---|---|---|---|
| **Início** | Abrir o app; barra | Ler de longe o Setup, a memória em uso, o preset dela e as três saídas; trocar de memória (3 botões + "…") e de preset (chip); tocar no nome do Setup para trocar de Setup; Conectar quando desconectado. Sem selo HERDADO | mock da 3b, quadro 1; v2 quadro 1 e telas-chave 6a (modelo antigo de memória) |
| **Mesa** | Barra | Ganho de cada saída e MASTER com faders; trocar de memória pelos mesmos 3 botões + "…" do Início, no topo [ASSUMPTION]; voltímetro e clip por canal na faixa de monitoração | v2 quadro 2 (sem a faixa; com as abas antigas) |
| **Áudio** | Barra | Grade 2 × 5 por função, na ordem do app da Taramps | telas-chave 1 |
| **Player** | Barra | Playlist da memória do celular em uso: tocar músicas do celular pelo Bluetooth até o processador | só este documento |
| **Setup em dúvida** | Sozinha ao conectar, quando a calibração lida não bate com a do último Setup usado | Escolher ou criar o Setup antes de tocar, com o som mutado (ver Conectar e Setup) | mock da 3b, quadro 2 |
| **Trocar de Setup** | Tocar no nome do Setup no cabeçalho do Início ("Setup Casa 2.1 ›") | A mesma tela, para escolher outro Setup ou criar um (ver Conectar e Setup) | mock da 3b, quadro 2 (variante sem dúvida) |
| **Lista de memórias** | Botão "…" ao lado dos 3 botões de memória (Início, Mesa) | As memórias do celular do Setup em uso e, na seção **"Do aparelho"**, as 3 do processador | só este documento |
| **Ajuste da saída** (JBL L, JBL R, Sub) | "Ajustar ›" no cartão de saída (Início) ou "Ajustar" no canal (Mesa) | Os ajustes de uma saída numa tela; na JBL L e na JBL R, também o equilíbrio L/R do Setup, em Avançado (ver Ajuste da saída) | v2 quadros 3, 3b, 4 (com a divisão antiga); mock da 3b, quadro 3 (Salvar e folha do Salvar) |
| **Telas de função** (Roteamento, EQ 15 bandas, EQ de entrada, Crossover, Alinhamento, Fase, Limitador, Nível das saídas, EQ de saída) | Bloco da grade Áudio | Uma função para as três saídas, com abas de saída [ASSUMPTION] | telas-chave 2a, 2b (Crossover); as outras, só este documento |
| **Gerador** | Bloco Gerador da grade Áudio | Tom fixo e sweep, com prazo de 30 s | só este documento |
| **Configurações** | Engrenagem | Ponto de entrada familiar para quem vem do app da Taramps | telas-chave 3 (sem a linha do voltímetro) |
| **Setups** | Configurações › Setups [ASSUMPTION] | Ver os Setups, renomear e apagar [ASSUMPTION]; selo HERDADO no Casa 2.1. Criar e trocar de Setup só pela tela Trocar de Setup ou Setup em dúvida | só este documento |
| **Memórias** | Configurações › Memórias | Memórias do celular do Setup em uso: aplicar, criar do som atual, renomear, apagar. Seção **"Do aparelho"**: as 3 memórias do processador, pelos nomes lidos, só para carregar. Ver o que é herdado | telas-chave 4a, 4b (modelo antigo) |
| **Presets no celular** | Configurações › Presets; chip de preset no Início (folha com a lista) | Presets de 15 bandas: aplicar por cima da memória, renomear, apagar, exportar; herdado: Cinema. Preset novo nasce na folha do Salvar | só este documento |
| **Aparelho** (grupo dentro de Configurações) | Configurações | Remote out, Bluetooth de áudio, nome do produto, senha, Voltímetro e clip; resets em Avançado | telas-chave 3 |
| **Voltímetro e clip** | Configurações › Aparelho › Voltímetro e clip; tocar na faixa de monitoração da Mesa | Tensão e clip das saídas e da entrada ao vivo, maiores | só este documento |
| **Chaves de acesso** | Configurações › Chaves de acesso | Ligar e desligar as 11 chaves, com aviso do risco | telas-chave 5a, 5b |
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
| Ajuste da saída | Ganho (giratório), passa-alta (frequência + tipo), passa-baixa (frequência + tipo), limitador (limiar + OFF) | Atraso, fase e, só na JBL L e na JBL R, **equilíbrio L/R do Setup** (ganho e limitador); EQ de saída (frequência, ganho, Q), ataque e liberação do limitador; [ASSUMPTION] Auto do limitador e mute |
| Crossover | Por saída: passa-alta e passa-baixa (frequência + tipo) | Link 1–2 e Link 3–4 |
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

**Equilíbrio L/R do Setup** (decisão do Denis, 23/10): mexe-se no Ajuste da **JBL L** ou da **JBL R**, em **Avançado**, junto do atraso e da fase. São dois valores por lado, **ganho** e **limitador**, somados ao valor da memória (no Casa 2.1, a R fica −3 dB nos dois). O Sub não tem equilíbrio.

- Mexer muda o som na hora, como qualquer ajuste. **Salvar** leva o equilíbrio para a camada **Calibração → Setup**.
- [ASSUMPTION] Os dois usam o controle de parâmetro, em dB com passo de 0,1 dB, e o título diz de onde vêm: "Equilíbrio do Casa 2.1 · ganho".
- [ASSUMPTION] O giratório e o limiar à vista mostram o valor final no aparelho (memória + equilíbrio). Mexer neles muda a memória; mexer no equilíbrio muda o Setup.
- [ASSUMPTION] A soma fica dentro da faixa do MAPA (ganho −45,0 a +15,0 dB; limitador −48,0 a 0,0 dB). No limite, o Ajuste avisa "No limite da faixa com o equilíbrio do Casa 2.1."

### Conectar e Setup

Ao conectar, depois da senha e da leitura completa, o app **confere o Setup** (CAP-16, failure-modes 5). Ele compara **só a calibração** lida com a **calibração salva do último Setup usado**. Essa referência muda só quando o Setup é salvo ou criado. Na primeira conexão, a referência é o Setup herdado **Casa 2.1**.

| O que o app lê | O que acontece |
|---|---|
| Calibração bate; caráter bate com a memória ativa | Entra direto, sem pergunta (estado **Setup confere**). |
| Calibração bate; caráter difere da memória ativa | Entra direto, **sem mutar**, com o aviso **"Som diferente da FILME"** e o botão **Reaplicar** (estado **Som diferente da memória ativa**). |
| Calibração não bate | **Muta** e abre a tela **Setup em dúvida**. |
| Os 4 canais ainda mutados por uma dúvida que não terminou | Volta à tela **Setup em dúvida** (estado **Dúvida interrompida**). |

[NOTE FOR UX] No aparelho, o equilíbrio L/R vem somado ao caráter (ganho e limitador finais). Falta decidir, na 8c, como a conferência o separa.

**O mudo da dúvida** é o mute do aparelho nos **4 canais** (MAPA, comando `100`). Ele fica no aparelho até o Denis decidir, mesmo se o app fechar ou a conexão cair. Sem o celular, o mudo sai pelo app da Taramps. Durante a dúvida, **a IA fica bloqueada**.

Tela **Setup em dúvida** (decisão do Denis sobre a recomendação da IA; [mock da 3b, quadro 2](mockups/key-screens-3b.html)):

- **Sem barra, sem ‹ e sem engrenagem:** a escolha é obrigatória. Em cima, só a marca e a pílula de status; depois, o título "Setup em dúvida" e a frase "O aparelho não está como o Casa 2.1 deixou da última vez."
- **Som mutado**, dito numa faixa: "**Som mutado** até você escolher." Nada toca nem é aplicado antes da escolha.
- **No topo, a lista do que não bate** (rótulo O QUE NÃO BATE, "lido agora no aparelho"), uma linha por diferença de calibração: "Atraso do Sub: 80 cm no Casa 2.1, 0 cm no aparelho". Só leitura.
- **No pé, os três botões**, cada um com uma frase curta embaixo dizendo o que faz:
  - **Escolher o Setup** (botão principal): abre a lista de Setups. Ao escolher, o app aplica a calibração e a memória ativa daquele Setup (com o preset que ela lembra), relê e desmuta.
  - **Criar Setup novo:** o Setup nasce com a calibração que está no aparelho; o app pede o nome. [ASSUMPTION] Depois do ✓, o app desmuta e o Setup novo fica em uso, ainda sem memórias.
  - **Seguir sem Setup** (contornado): desmuta sem aplicar nada e deixa o aparelho como está. As memórias do celular ficam travadas até escolher um Setup.

**Trocar de Setup com o app conectado:** tocar no nome do Setup no cabeçalho do Início volta à escolha e à conferência, como numa nova conexão: o som muta e abre a mesma tela. Não há troca solta de Setup. [ASSUMPTION] Aqui a tela tem cabeçalho de tela cheia, com ‹ e o título "Trocar de Setup"; ‹ ou Voltar desiste, desmuta e não aplica nada. [ASSUMPTION] Sem diferença para listar, a lista some e o topo diz "O aparelho está no Casa 2.1."

**Processador que perdeu o estado ao desligar** (a história 12 mede se isso acontece): a calibração lida não bate, então vale o Setup em dúvida. Ao escolher o Setup, o app reaplica a memória ativa. A reaplicação vem sempre **depois** da conferência.

**O que o cartão da memória em uso mostra.** O nome aparece só quando o app sabe o que está tocando (decisão do Denis, 23/10):

| Situação | Cartão |
|---|---|
| Memória do celular aplicada ou criada nesta conexão, ou confirmada pela conferência ao conectar | O nome dela, com o chip de preset; o botão dela em branco |
| Memória do aparelho carregada por este app nesta conexão | O nome lido ("MUSICA ORIG") e, embaixo, "Memória 2 do aparelho (app da Taramps)"; sem chip; nenhum botão em branco |
| O app não sabe: Seguir sem Setup, caráter diferente da memória ativa, ou reconexão depois de carregar uma memória do aparelho | **"Som do aparelho"**, sem nome nem chip; nenhum botão em branco. [ASSUMPTION] Embaixo: "O app não sabe qual memória está tocando." Com caráter diferente, também o aviso "Som diferente da FILME" e **Reaplicar** |
| Setup sem memórias | "Nenhuma memória" e **Criar memória do som atual** |

### Salvar por camadas

**Um só botão Salvar**, fixo no pé do Ajuste e das telas de função. Mexer muda o som na hora; Salvar é um passo à parte e guarda no celular, cada mudança na camada dona dela (ver Três camadas do som). A folha do Salvar compara o estado atual com o que está guardado no Setup em uso, na memória em uso e no preset que ela lembra, e lista só o que mudou.

| Caso | Para onde vai | O que a folha pergunta |
|---|---|---|
| Memória do celular em uso | Calibração → Setup em uso; caráter → memória em uso; 15 bandas → preset lembrado | Com as 15 bandas mexidas: **Atualizar o preset Rock** ("Vale para toda memória que usa ele.") ou **Criar preset novo**, com o nome. [ASSUMPTION] Nenhuma vem escolhida; o botão branco só destrava depois da escolha |
| A memória em uso lembra um preset herdado (Cinema) | Igual à linha de cima | [ASSUMPTION] **Atualizar o Cinema** vem travado ("Preset herdado não é sobrescrito."), e **Criar preset novo** já vem escolhido, com o campo do nome aberto |
| Memória do aparelho ou "Som do aparelho" em uso | Caráter → **memória nova do celular**, com o nome. [ASSUMPTION] 15 bandas → **preset novo**, com o nome, lembrado pela memória nova | Uma linha por diferença entre a calibração do aparelho e a do Setup, e a pergunta: **Manter a calibração do Casa 2.1** (o app reaplica a do Setup; a memória guarda só o caráter) ou **Atualizar o Casa 2.1 com a do aparelho** ("Vale para todas as memórias do Casa 2.1."). [ASSUMPTION] Sem diferença, não pergunta. Com isso, a próxima conexão confere sem dúvida |
| Setup sem memórias | Calibração → Setup; [ASSUMPTION] caráter → memória nova do celular, com o nome | — |
| Seguir sem Setup | Nada | [ASSUMPTION] Salvar travado: "Escolha um Setup para salvar." |
| Nada mudou | Nada | [ASSUMPTION] Salvar travado: "Nada para salvar." Com memória do aparelho ou "Som do aparelho" em uso, o Salvar nunca trava por isso |

Regras:

- [ASSUMPTION] O antes e o depois mostram o valor da tela, que é o final no aparelho (memória + equilíbrio). O app guarda em cada camada a parte dela.
- Só **Manter a calibração** manda algo pelo BLE: o app guarda a memória nova e reaplica a calibração do Setup, [ASSUMPTION] com cópia antes e releitura. Uma queda no meio cai no estado **Aplicado pela metade**.
- [ASSUMPTION] Ao fim, aviso curto do que foi para onde: "Salvo: calibração no Casa 2.1, caráter na MUSICA ORIG."
- [ASSUMPTION] Aplicar ou carregar outra memória, ou Reaplicar, descarta as mudanças não salvas sem perguntar, porque aplicar não pede confirmação.
- **Criar memória do som atual** (Memórias, cartão "Nenhuma memória") guarda o caráter que está tocando numa memória nova, sem a folha. Com memória do aparelho ou "Som do aparelho" em uso, abre a folha do Salvar (terceira linha da tabela).

### Player

A pílula **Player** abre a playlist da **memória do celular em uso**. **Cada memória do celular tem a sua playlist** (CAP-15), montada com arquivos de áudio do celular. O app toca e o som vai pelo Bluetooth de áudio até o processador.

- **Uma playlist só na tela:** a da memória em uso, com a linha "Playlist da MUSICA ORIG" em cima. **Sem abas de memória.** Para ver ou editar outra playlist, troque de memória (Início ou Mesa).
- **Sem playlist:** sem memória do celular em uso (memória do aparelho, "Som do aparelho", Seguir sem Setup). Ver o estado **Sem playlist**.
- **Lista:** as faixas da playlist, na ordem. A que toca fica marcada.
- **Adicionar músicas:** abre o seletor de arquivos do Android, com vários arquivos de áudio de uma vez. As escolhidas entram no fim da lista. [ASSUMPTION] O app guarda o caminho do arquivo; ele não copia a música.
- **Editar:** mostra − para remover e ≡ para arrastar e ordenar. **Pronto** sai do modo.
- **Controles fixos embaixo:** tocar/pausar e próxima.
- **Trocar de memória com música tocando:** [ASSUMPTION] a música continua; a playlist da nova memória entra quando a faixa atual acabar.
- **Segundo plano:** [ASSUMPTION] a música continua com o app em segundo plano, com tocar/pausar e próxima na notificação do Android.

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
| Folha do Salvar diz o que vai para onde: "Calibração → Setup Casa 2.1" · "Caráter → memória MUSICA ORIG" | "Salvar alterações?" |
| "Atualizar o preset Rock?" + "**Vale para toda memória que usa ele.**" [ASSUMPTION na redação] | Atualizar sem dizer quem mais muda |
| "Atraso do Sub: 80 cm no Casa 2.1, 0 cm no aparelho" | "Estado inconsistente" |
| "Som diferente da FILME" + **Reaplicar** | "Estado alterado" |
| "Memória 2 do aparelho (app da Taramps)" só quando este app carregou; senão, "Som do aparelho" | "Memória 2" sem dizer de onde vem, ou um nome que o app não sabe |
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
| **MASTER** | Mesa | Mesmo comportamento do canal, de −80,0 a 0,0 dB, passo 0,1. É caráter: vai para a memória. |
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
| **Chip de preset** | Cartão da memória em uso ("Preset: Cinema ▾") | Tocar abre a **folha de presets** [ASSUMPTION], com uma linha de escolha por preset, e troca ali mesmo: aplica só as 15 bandas por cima da memória, sem confirmação (cópia antes, releitura). Cada memória do celular **lembra o último preset usado com ela**; ao aplicar a memória, o preset dela volta junto (a FILME volta com o Cinema). Sem memória do celular em uso, não há chip. |
| **Botões de memória** | Início; topo da Mesa | **3 botões** das memórias do celular usadas por último no Setup em uso (contando a que está em uso) e o botão **"…"**. Quem fica à vista é o app que decide. Os 3 **não mudam de lugar**: a memória que entra ocupa o lugar da usada há mais tempo; as outras duas ficam onde estão. Memórias do aparelho nunca ocupam os 3 botões. Um toque troca para uma das 3; dois toques ("…" e escolher) para as outras. Tocar numa memória que não está em uso **aplica** essa memória, **sem confirmação** (ver Aplicando memória). [ASSUMPTION] Com menos de 3 memórias no Setup, aparecem só as que existem, mais o "…". [ASSUMPTION] Ao apagar uma memória que está num dos 3, a vaga fica com a próxima usada por último, no mesmo lugar; sem outra, fica vazia. |
| **Lista de memórias** ("…") | Início, Mesa | [ASSUMPTION] Sobe de baixo, como uma folha, e fecha ao escolher. Em cima, as memórias do celular do Setup em uso, em linhas de escolha; embaixo, a seção **"Do aparelho"** com as 3 memórias do processador, pelos nomes lidos. Tocar numa do celular **aplica** (como os botões); numa do aparelho **carrega** (ver Carregando memória do aparelho). |
| **Linha de escolha** | Lista de memórias, folha de presets, lista de Setups | [ASSUMPTION] Uma linha por item: nome e detalhe (preset lembrado, "Memória 2 do aparelho"), com o selo HERDADO à direita quando é herdado. A que está em uso fica em branco. Tocar escolhe e fecha a folha. Travada (Seguir sem Setup), diz por quê. |
| **Botão principal "Salvar"** | Fixo no pé do Ajuste e das telas de função [ASSUMPTION] | Um só Salvar para tudo. Abre a folha do Salvar. Trava quando nada mudou ou sem Setup (ver Salvar por camadas). [ASSUMPTION] Some enquanto o teclado está aberto. |
| **Folha do Salvar** | Sobe do botão Salvar ([mock da 3b, quadro 3](mockups/key-screens-3b.html)) | "Salvar o que mudou?" + "Cada mudança vai para o lugar dela." Uma linha por camada ("Calibração → Setup Casa 2.1", "Caráter → memória MUSICA ORIG", "15 bandas → preset Rock") e, embaixo de cada uma, o que mudou, com o antes e o depois ("Atraso do Sub: 80 → 85 cm"). O preset herdado leva o selo HERDADO. Os casos e as perguntas estão em **Salvar por camadas**. Nada é guardado antes do botão branco. |
| **Cartão de memória** | Memórias | Memórias do celular do Setup em uso, em quantidade livre: nome, selo EM USO na que toca e os botões **Aplicar**, **Renomear** e **Apagar**. Aplicar faz o mesmo que os botões de memória. Renomear abre o campo do nome (vale no ✓). Apagar: ver o estado **Apagar memória**. **Criar memória do som atual**: ver Salvar por camadas; [ASSUMPTION] a playlist começa vazia, o preset lembrado é o preset em uso e a memória nova passa a ser a memória em uso. Na seção **"Do aparelho"**, as 3 memórias do processador só têm **Carregar**: sem selo, sem renomear e sem salvar. [ASSUMPTION] Um lembrete diz "Salvar e renomear estas memórias só no app da Taramps." Criar, renomear e apagar no celular não mandam nada pelo BLE. |
| **Selo herdado** | Setups, Memórias, Presets, folha do Salvar | Marca o que veio dos protótipos (CAP-6): a FILME de 18/10, dividida em três. O selo HERDADO fica no Setup **Casa 2.1**, na memória **FILME** e no preset **Cinema**. **Não aparece no Início**, para o Início ficar leve (decisão do Denis, 23/10). [ASSUMPTION] Tocar no selo abre uma folha só de leitura com a nota de `conhecimento-herdado.md`. As memórias do aparelho não têm selo. DIALOGO e NOITE não entram, porque são `.trps` cifrados. |

### Folhas e avisos

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Folha de confirmação** | Salvar (camadas), apagar memória, ligar chave com risco, apagar preset [ASSUMPTION], apagar Setup [ASSUMPTION], pedido de risco da IA, aplicado pela metade, reset, troca de senha | [ASSUMPTION] Sobe de baixo; o botão branco faz e o contornado desiste. [ASSUMPTION] Tocar no véu ou em Voltar desiste. Nada é enviado ao aparelho antes do botão branco. |
| **Aviso de risco** | Dentro da folha | Repete o risco escrito em `chaves-de-acesso.md`, sem mudar o texto. |
| **Aviso de erro** | Senha, valor fora da faixa | Texto curto que diz o que fazer. |
| **Aviso de som diferente** | Cartão da memória em uso | "Som diferente da FILME" e **Reaplicar**, que aplica a memória ativa como os botões de memória (sem confirmação, cópia antes, releitura). Não muta. [ASSUMPTION] Some ao Reaplicar, ao aplicar ou carregar outra memória, ou ao Salvar. |
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
| **Lista do que não bate** | Setup em dúvida | Uma linha por diferença de calibração, com o valor do Setup e o do aparelho. Só leitura. [ASSUMPTION] Com muitas diferenças, a lista rola e os três botões ficam fixos no pé. |

## State Patterns

| Estado | Onde | Tratamento |
|---|---|---|
| **Primeira abertura** | Início, Setups, Memórias, Presets | Conhecimento herdado já carregado (CAP-6): Setup **Casa 2.1**, memória **FILME** e preset **Cinema**. O selo HERDADO aparece em Setups, Memórias e Presets; o Início não mostra o selo. Na primeira conexão, a conferência compara com a calibração do Casa 2.1. Sem senha guardada, Conectar leva às caixas da senha. |
| **Conectando** | Pílula de status, Início | Ponto cinza, "Conectando". Com senha guardada, o app envia a senha sozinho. Depois da senha vem a leitura completa (84 parâmetros, ~13 s) e a conferência do Setup. [ASSUMPTION] O Início mostra "Lendo o aparelho" com a contagem, e os valores aparecem só quando lidos. |
| **Conectado** | Todas | Ponto verde, "Conectado · R003". Ao conectar ou reconectar, o app **relê tudo antes de qualquer ação**, inclusive depois de usar o app da Taramps. |
| **Setup confere** | Ao conectar | Calibração e caráter batem: entra direto, sem pergunta. O cabeçalho do Início mostra o Setup; o cartão mostra a memória ativa. |
| **Som diferente da memória ativa** | Início, ao conectar | Calibração bate, caráter difere: entra direto, **sem mutar**. O cartão mostra "Som do aparelho" e o aviso "Som diferente da FILME" com **Reaplicar**. |
| **Setup em dúvida** | Tela cheia, ao conectar ([mock da 3b, quadro 2](mockups/key-screens-3b.html)) | Calibração não bate: os 4 canais mutados e a tela Setup em dúvida. Ver Conectar e Setup. |
| **Dúvida interrompida** | Ao conectar | O app fechou ou a conexão caiu com a tela Setup em dúvida aberta. O mudo continua no aparelho. Ao reconectar, o app lê o mudo e volta à tela Setup em dúvida, que confere de novo. [ASSUMPTION] O app guarda no celular que deixou o aparelho mutado; se o mudo foi tirado pelo app da Taramps, a conferência segue normal. |
| **Trocando de Setup** | Tocar no nome do Setup no Início | A mesma tela, com o título "Trocar de Setup" e ‹. Ver Conectar e Setup. |
| **Reaplicando a memória ativa** | Ao conectar, depois da conferência | Se o processador perdeu o estado ao desligar (a história 12 mede), o app reaplica a memória ativa do Setup só **depois** de o Setup ser escolhido: cópia antes, aplica, relê, desmuta. Nada toca antes. [ASSUMPTION] Os valores ficam esmaecidos com "Aplicando a MUSICA ORIG" até a releitura. |
| **Seguir sem Setup** | Todas | Som desmutado e aparelho como está; o cartão mostra "Som do aparelho". Memórias do celular travadas até escolher um Setup: [ASSUMPTION] os 3 botões e a parte do celular na lista ficam esmaecidos com "Escolha um Setup para usar as memórias do celular."; a seção "Do aparelho" continua carregando. [ASSUMPTION] O seletor diz "Sem Setup ›". |
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
| **Link ligado** | Crossover › Avançado e telas de saída | Mexer no canal mestre muda o par (1–2: JBL L manda na JBL R; 3–4: Sub). [NOTE FOR UX] Não está desenhado como a aba e o Ajuste da JBL R aparecem com o link 1–2 ligado, nem como o Sub mostra os canais 3 e 4 quando o link 3–4 está desligado. |
| **Aplicando memória** (celular) | Início, Mesa, lista "…", Memórias, Reaplicar | Sem confirmação. Passa pela chave "Aplicar memória do celular", guarda a cópia (failure-modes 3), escreve o caráter da memória (somado ao equilíbrio do Setup) e o preset que ela lembra, e relê. [ASSUMPTION] Os valores ficam esmaecidos com "Aplicando a FILME" até a releitura. |
| **Carregando memória do aparelho** | Lista "…" › Do aparelho, Memórias › Do aparelho | Sem confirmação. Passa pela chave "Carregar memória do aparelho", guarda a cópia, carrega (`241`) e relê. [ASSUMPTION] Os valores ficam esmaecidos com "Carregando a memória 2 do aparelho" até a releitura. Depois, o cartão mostra o nome lido e "Memória 2 do aparelho (app da Taramps)" até a conexão cair. Para guardar esse som no celular: Salvar (ver Salvar por camadas). |
| **Salvando** | Folha do Salvar | Guarda no celular; nada vai pelo BLE, então não há queda no meio do salvar. A exceção (Manter a calibração) está em Salvar por camadas. |
| **Aplicado pela metade** | Ao reconectar | Folha "Aplicado pela metade": aplicar memória do celular, aplicar preset, carregar memória do aparelho ou reaplicar a calibração caiu no meio. Botões **Terminar** (branco) e **Voltar à cópia** (contornado). [ASSUMPTION na redação] |
| **Preset aplicado** | Chip de preset, Presets | Cópia antes, aplica só as 15 bandas, relê. A memória em uso passa a lembrar esse preset. [ASSUMPTION] "Preset Rock aplicado na MUSICA ORIG." |
| **Presets vazio** | Presets no celular | [ASSUMPTION] Só o herdado Cinema e "Mexa nas 15 bandas e toque em Salvar para criar um preset." |
| **Apagar preset** | Presets | Folha de confirmação "Apagar o preset X?" [ASSUMPTION]. [ASSUMPTION] Um preset lembrado por alguma memória não se apaga: Apagar fica travado com "Lembrado por 2 memórias." O Cinema herdado também não se apaga. |
| **Setup sem memórias** | Início, Memórias | [ASSUMPTION] Logo depois de Criar Setup novo: o cartão diz "Nenhuma memória" e oferece **Criar memória do som atual**; sem chip de preset; a lista "…" só tem a seção "Do aparelho". |
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
- **Cópia antes de mexer em vários parâmetros:** antes de aplicar uma memória do celular, aplicar um preset, carregar uma memória do aparelho ou reaplicar a calibração, o app guarda o estado (failure-modes 3).
- **Aplicar e carregar:** memória do celular se **aplica** (caráter somado ao equilíbrio do Setup, mais o preset que ela lembra); memória do aparelho se **carrega** (`241`). As duas com cópia antes e releitura.
- **Nada toca antes de conferir o Setup.** Na dúvida, os 4 canais ficam mutados no aparelho até a escolha.
- **Toque, arrasto e escrita.** Sem deslizar para aplicar, carregar ou apagar (o app da Taramps carrega uma memória ao deslizar; aqui não). O único arrasto de lista é a alça ≡ da playlist no modo Editar.
- **Proibidos:** tocar num bloco para entrar em edição; esconder um número ou nível atrás de um toque; confirmação para mexer em ajustes de áudio, para aplicar ou carregar memória ou para trocar de preset.

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

1. Denis abre o app. A senha está guardada; a pílula passa de "Conectando" a "Conectado · R003" e o app lê o aparelho. A calibração e o caráter batem com o Casa 2.1 e a FILME, e o app entra direto: o cabeçalho mostra **Setup Casa 2.1 ›** (estado **Setup confere**).
2. No **Início**, a memória em uso é a **FILME**, com o chip **Preset: Cinema ▾**. A MUSICA ORIG ainda não existe no celular; a antiga é a memória 2 do aparelho, feita no app da Taramps.
3. Ele vai à **Mesa** e acerta os ganhos: arrasta a capa do Sub, toca no número da JBL L, escreve o valor e toca em ✓. O som muda a cada passo; a faixa de monitoração mostra se algum canal clipa.
4. Em **Áudio** › **Limitador**, ele escolhe a aba de cada saída e segura + até o limiar que quer.
5. Ele ouve e volta ao passo 3 quantas vezes quiser. Nada foi salvo ainda; a FILME guardada continua como estava.
6. Em **Configurações › Memórias**, ele toca em **Criar memória do som atual**, escreve "MUSICA ORIG" e toca em ✓. A memória nova guarda o caráter que está tocando, no Setup Casa 2.1, vira a memória em uso e entra num dos 3 botões do Início, no lugar da usada há mais tempo. [ASSUMPTION] O aviso customizado da CAP-14 aparece aqui: "Memória MUSICA ORIG criada no Casa 2.1."
7. Ele desliga o processador, liga de novo e toca em **Conectar**.
8. **Clímax:** o app relê e confere o Setup. Se o aparelho guardou o som, a calibração e o caráter batem, e o app entra direto na MUSICA ORIG. Se perdeu, a calibração não bate: sobe a tela **Setup em dúvida** com os 4 canais mutados; ele toca em **Escolher o Setup** › **Casa 2.1**, e o app reaplica a calibração e a MUSICA ORIG, relê e desmuta. Nos dois casos, o som fica igual ao da memória. Ele não abriu o app da Taramps em nenhum momento.

Falha: a conexão cai no meio da reaplicação → ao reconectar, vale o estado **Aplicado pela metade**.

[NOTE FOR UX] O exemplo da CAP-14 no SPEC ("ao sobrescrever uma memória") ficou velho: sobrescrever saiu na 3b. Vale atualizar o SPEC com o aviso do passo 6.

### Fluxo 2 — A IA ajusta o EQ da FILME e o Denis vê ao vivo (Denis, no sofá, notebook ao lado)

Teste 2 do sinal de sucesso.

1. Antes, uma vez só: em **Configurações › IA pela rede**, Denis toca em **Lig.**; a folha mostra "Deixa a IA comandar o som pela rede." e ele toca em **Ligar a chave**. O app mostra o código de 6 dígitos; ele passa o código ao Claude Code, que pareia. Na mesma tela, o toque durante a IA fica no padrão: **travar até PARAR IA**.
2. No notebook, ele pede à IA para ajustar o EQ da FILME.
3. A IA pede para aplicar a memória **FILME** do celular. Como é operação de risco para a IA, sobe a folha do estado **IA pede operação de risco**; ele confirma. A FILME volta com o preset Cinema.
4. O app abre a **IA ativa**: faixa "IA ajustando o EQ da FILME", painel ao vivo com as 15 bandas e os ganhos e limitadores, registro "O que a IA fez" e o aviso de controles travados (estado **IA ativa, travado**).
5. Cada banda que a IA mexe acende em lilás e se move em até 1 s; a linha "Agora" mostra o valor de antes e o de depois; o registro ganha uma linha.
6. Ele toca sem querer num fader: aparece o aviso do estado **IA ativa, travado**. Nada muda.
7. A IA termina e diz no notebook: "Pronto, não salvei." A IA não salva (a porta não tem esse comando).
8. **Clímax:** ele toca em **PARAR IA**. O acesso da IA é revogado na hora, a tela volta para onde estava e o **Histórico** guarda cada comando, com origem IA, hora e valor.
9. Ele ouve. Se gostar, vai em **Áudio › EQ 15 bandas** e toca em **Salvar**. A folha mostra "15 bandas → preset Cinema", com o selo HERDADO; **Atualizar o Cinema** vem travado e **Criar preset novo** já vem escolhido. Ele escreve o nome e confirma; a FILME passa a lembrar o preset novo.

Variante: com a opção **pausar ao toque** escolhida em IA pela rede, no passo 6 o toque pausa a IA, os controles destravam, ele mexe, e a IA só retoma depois de 10 s sem toques.

Falha: o Wi-Fi cai → a IA some sem deixar nada pendente (cada comando é avulso); [ASSUMPTION] a IA ativa fecha e o app volta ao normal com o aviso "A IA perdeu a conexão".

### Fluxo 3 — Noite de filme (Denis, 21h, luz apagada)

1. Denis liga o som e abre o app. Pílula: **Conectando**, depois **Conectado · R003**.
2. O Início mostra a memória em uso. Ele toca no botão **FILME**; o app aplica a memória e o preset Cinema que ela lembra, e relê sem perguntar; o cartão mostra **FILME** em letras grandes, com o chip **Preset: Cinema ▾**.
3. Antes do filme, ele abre o **Player**: a playlist da FILME tem as trilhas que ele usa para conferir o som. Ele toca a primeira; o som sai pelo processador.
4. Começa o filme na TV. Ele pausa o Player e, na cena de explosão, toca em **Mesa**.
5. Ele segura **+** do **SUB**. O som do sub sobe na hora, passo a passo.
6. O ponto de clip do Sub acende na faixa de monitoração; ele toca uma vez em − e o ponto apaga.
7. **Clímax:** o grave chega no ponto que ele queria, sem sair do filme e sem nenhuma confirmação.
8. Ele não salva. Na próxima conexão, a calibração bate e o caráter difere da FILME: o app não muta, e o cartão mostra "Som do aparelho" com o aviso **Som diferente da FILME** e **Reaplicar**. Se ele tivesse gostado, teria tocado em Ajustar › **Salvar**, e a folha diria "Caráter → memória FILME".

Variante: para uma comédia, ele toca no chip **Preset: Cinema ▾** e escolhe **Diálogo** na folha de presets. Só as 15 bandas mudam, sem confirmação, e a FILME passa a lembrar o Diálogo.

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

1. No app da Taramps, Denis carrega as memórias 1, 2 e 3, uma por vez, e anota os valores tela a tela. Ele **não abre a tela Limiter com o Auto ligado**, porque ela reescreve o ataque e a liberação. Termina com a memória 1 carregada.
2. Ele fecha o app da Taramps (só cabe uma conexão BLE).
3. Ele abre o BGS Controle, que conecta, relê tudo (84 parâmetros) e confere a calibração com a do Casa 2.1. Se não bate, sobe a tela **Setup em dúvida**, com os 4 canais mutados; ele toca em **Seguir sem Setup**, e o app desmuta sem aplicar nada. Se bate, o app entra direto, com o aviso **Som diferente da FILME**, e ele não toca em Reaplicar. Nos dois casos, o cartão mostra **Som do aparelho**: o app não carregou a memória 1, então não diz qual é.
4. Ele confere pela grade **Áudio**: cada bloco mostra o resumo, e cada tela de função mostra os valores por saída, nas abas. Abre o Avançado onde precisa (ataque e liberação, links).
5. Para as memórias 2 e 3, ele toca em **…** e, na seção **Do aparelho**, carrega cada uma (estado **Carregando memória do aparelho**). O cartão passa a mostrar "Memória 2 do aparelho (app da Taramps)". Confere de novo como no passo 4.
6. **Clímax:** cada valor do BGS Controle bate com o app da Taramps nas três memórias do aparelho.

Variante, guardar um som do aparelho no celular: com um Setup em uso (não em Seguir sem Setup), depois de carregar a memória 2 ele toca em **Salvar** num Ajuste. O caráter vai para uma memória nova do celular, e, se a calibração difere, a folha pergunta se mantém a do Casa 2.1 ou atualiza o Casa 2.1 (ver Salvar por camadas).

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

1. Denis abre o app no carro. O processador ainda tem o som de casa: a calibração lida bate com a do **Casa 2.1**, e o app entra direto.
2. Ele toca no nome do Setup, **Setup Casa 2.1 ›**, no cabeçalho do Início. O som muta e abre **Trocar de Setup**.
3. Ele toca em **Criar Setup novo**, escreve "Carro" e toca em ✓. O Setup Carro nasce com a calibração que está no aparelho, ainda sem memórias (estado **Setup sem memórias**).
4. Ele ajusta a calibração do carro (atraso, fase, corte) e toca em **Salvar**. A folha diz **"Calibração → Setup Carro"**; ele confirma.
5. No Início, o cartão diz "Nenhuma memória". Ele toca em **Criar memória do som atual**, escreve "ESTRADA" e toca em ✓. A ESTRADA vira a memória em uso do Carro, com o chip de preset. Ele toca no chip e escolhe **Rock**; a ESTRADA passa a lembrar o Rock.
6. Dias depois, o processador volta para casa. Ao conectar, a calibração lida bate com a do Carro, o último Setup usado, e o app entra direto no **Carro**.
7. **Clímax:** ele toca em **Setup Carro ›** e escolhe **Casa 2.1**. O app aplica a calibração e a memória ativa de casa, relê e desmuta. O som de casa volta inteiro, e nada tocou antes da escolha.

Variante (o caminho da dúvida do teste 4): no carro, alguém mexe no atraso pelo app da Taramps. Na próxima conexão, a calibração lida não bate com a do Carro: o app muta os 4 canais e abre **Setup em dúvida** com "Atraso do Sub: …". Nada toca nem é aplicado até ele tocar em **Escolher o Setup**, **Criar Setup novo** ou **Seguir sem Setup**.

Falha: a conexão cai no meio da aplicação → ao reconectar, vale o estado **Aplicado pela metade**.

## Uso junto com a IA

Segue `porta-de-comandos.md`, `chaves-de-acesso.md` e a regra 5 de `failure-modes.md`, já atualizados com as decisões deste documento (21 e 23/10).

- **Porta desligada por padrão.** Abre só com a chave **"IA pela rede"** ligada pelo Denis, na tela.
- **Pareamento único** por código de 6 dígitos mostrado no app. Só a rede local. A senha do processador nunca passa pela porta.
- **A IA passa pelas mesmas chaves do Denis** e nunca liga ou desliga uma chave.
- **Risco pede confirmação na tela:** reset de fábrica, reset de áudio, troca de senha, gerador, aplicar memória do celular, carregar memória do aparelho, remote out. Salvar e renomear memórias do aparelho não existem neste app, nem para a IA.
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
| Memórias 1–3 do aparelho: carregar | "…" › **Do aparelho**, ou Configurações › Memórias › Do aparelho: só **Carregar** |
| Memórias 1–3 do aparelho: salvar e renomear | **Continuam só no app da Taramps.** O BGS Controle não salva nem renomeia memórias do aparelho. |
| Salvar (barra) | Botão **Salvar** no pé dos ajustes: guarda no celular, cada mudança na camada dona dela (ver Salvar por camadas) |
| MASTER na memória | MASTER na memória do celular (caráter), como no app da Taramps |
| Volt. (barra) | Faixa de monitoração na Mesa e Configurações › Aparelho › Voltímetro e clip |
| Deslizar uma memória carrega ou exporta | Botões escritos (Aplicar, Carregar, Renomear, Apagar); nada acontece por deslizar |
| Abrir o Limiter em Auto reescreve ataque e liberação | Abrir uma tela nunca escreve |
| Presets `.trps` | Não são lidos. O preset do BGS Controle é só o EQ de 15 bandas. **Exportar**, na linha de Presets, grava o preset próprio como arquivo JSON (não `.trps`). |
| Um som feito no app da Taramps e guardado numa memória do aparelho | Vira memória do celular (decisão do Denis, 23/10): carregar a memória ("…" › **Do aparelho**) e tocar em **Salvar** (ver Salvar por camadas, terceira linha). Ao conectar depois de usar o app da Taramps, o app confere só a calibração: se ela mudou, abre a tela Setup em dúvida; se só o caráter mudou, entra com o aviso "Som diferente". [ASSUMPTION] Na dúvida, o caminho é Escolher o Setup e depois carregar a memória do aparelho; o som que não foi guardado numa memória do aparelho não é trazido. |

## Inspiration & Anti-patterns

- **Tirado do Estúdio Noturno:** pouca coisa na tela para ler de longe; nome da memória enorme e fino.
- **Tirado do Console Tátil:** faders com capa, botão giratório, − e + grandes; regulagem tátil.
- **Recusado — Bancada Técnica e Papel Calmo:** direções não escolhidas.
