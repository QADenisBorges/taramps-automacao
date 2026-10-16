# Roteiro de captura do protocolo BLE

Passo a passo do Denis para a história 1 (mapa do protocolo). A IA só analisa o que este roteiro produz: capturas prontas e anotadas.

**Regras que valem em todas as partes**

- O processador fica **desligado do amplificador** durante todas as capturas: tire os cabos RCA das saídas do PRO antes de começar e confira de novo a cada sessão.
- **Nunca** toque em "Reset Audio Settings", "Reset to Factory Defaults" nem em "Change Password". Não salve presets na memória interna do processador (DSP).
- O notebook não conecta ao processador. Só o celular conversa com ele; o notebook só recebe arquivos pelo cabo USB.
- Os arquivos brutos (bugreport, snoop log, APK e presets exportados) ficam em `protocolo/capturas/bruto/`, `protocolo/apk/` e `protocolo/presets/`. Essas pastas estão no `.gitignore` e nunca vão para o git.

**Ordem de execução (decisão do Denis, 2026-10-16)**

1. **Fase Demo, sem o processador:** fazer 1.1 a 1.3 (ferramentas, celular, APK) e depois a Parte D (Modo Demo). Nessa fase a IA analisa o APK e o que o Demo produz.
2. **Fase com o processador, amplificador desconectado:** liberada pelo Denis em 2026-10-16 **na versão curta** (Parte C, abaixo): C0, 4 passos de prova e a medição do limiter (4.2). Como o APK já deu a codificação, a captura só confirma que os bytes no ar são os do código. As Partes 3 e 4.1 (C1…C15 e preset B) ficam como reserva, caso a prova falhe.

No Modo Demo o app não conversa com o processador. O snoop log não grava nenhum byte do protocolo. Por isso, as capturas C0…C15 só existem na fase com o processador.

## Parte 1 — Preparação

### 1.1 Ferramentas no notebook

1. Baixe o **SDK Platform-Tools for Windows** na página oficial do Android (`developer.android.com/tools/releases/platform-tools`). *Feito em 2026-10-16: adb 1.0.41, versão 37.0.1, em `C:\platform-tools`, com assinatura da Google LLC válida.*
2. Extraia o zip em `C:\platform-tools`.
3. Em cada terminal novo, antes dos comandos deste roteiro, rode `$env:Path += ";C:\platform-tools"`. Isso vale só para aquele terminal e não altera o PATH do sistema. Confira com `adb version`.
4. No relógio do Windows, ligue os segundos: Configurações → Hora e idioma → Data e hora → "Mostrar segundos no relógio da bandeja". É por esse relógio que você vai anotar os horários. O celular e o notebook devem estar com a hora automática ligada.

### 1.2 Celular

1. Ative as opções de desenvolvedor: Configurações → Sobre o telefone → (Informações do software) → toque 7 vezes em "Número da versão" (no Samsung, "Número de compilação"). No Samsung com One UI 6, desligue também o **Bloqueador automático** (Segurança e privacidade), que bloqueia comandos pelo cabo USB.
2. Em Opções do desenvolvedor, ligue **Depuração USB**.
3. Em Opções do desenvolvedor, abra **Registro de rastreamento HCI Bluetooth** (ou "Ativar registro de espionagem HCI Bluetooth") e escolha **Ativado**. Não use "Ativado com filtro": ele corta o conteúdo dos pacotes.
4. Desligue e religue o Bluetooth para o registro começar. Se o celular pedir, reinicie.
5. Ligue o celular no notebook pelo USB, rode `adb devices` e aceite a autorização na tela do celular. O comando deve listar o aparelho como `device`.
6. Desconecte do celular outros aparelhos BLE (relógio, pulseira) durante as capturas. Isso diminui o ruído no log.

### 1.3 Extração do APK

Na pasta do projeto (`C:\Users\denis\Desktop\Automação Taramps`):

```powershell
adb shell pm list packages taramps
```

Se nada aparecer, rode `adb shell pm list packages` e procure o pacote do app "PRO 2.4BT + AUDIO". Depois, com o nome do pacote:

```powershell
adb shell pm path <pacote>
adb shell dumpsys package <pacote> | findstr versionName
mkdir protocolo\apk
adb pull <caminho-de-cada-linha-sem-o-prefixo-package:> protocolo\apk\
```

O `pm path` pode listar mais de um arquivo (`base.apk` e `split_config...apk`). Puxe todos. Anote o nome do pacote e a versão para o `APK.md`.

### 1.4 Como cada sessão de captura funciona

Uma **sessão** é um log limpo, uma ou mais capturas e um bugreport no fim.

1. Feche o app da Taramps (Configurações → Apps → Forçar parada).
2. Desligue e religue o Bluetooth do celular. O log anterior vira `.last` e começa um log novo.
3. Faça as capturas da sessão (Partes 2 a 4), anotando cada passo na tabela de anotação.
4. Feche o app e tire o bugreport, com o nome da sessão. Antes do primeiro bugreport, crie a pasta:

   ```powershell
   mkdir protocolo\capturas\bruto
   adb bugreport protocolo\capturas\bruto\C0-bugreport.zip
   ```

   O comando leva de 1 a 3 minutos. O zip do bugreport traz **dados pessoais** do celular (contas, apps, registros do sistema): ele fica só no notebook, nunca vai para o git nem é enviado a ninguém, e é apagado ao fim da história.
5. Confira a captura antes de seguir. Defina a senha BT uma vez por terminal, para o relatório sair com a senha mascarada. Não use `--senha`, que fica gravado no histórico do PowerShell:

   ```powershell
   $env:TARAMPS_SENHA = "<sua senha>"
   python -I ferramentas\btsnoop_att.py protocolo\capturas\bruto\C0-bugreport.zip --anotacao protocolo\capturas\C0-anotacao.csv
   ```

   O relatório termina com **CAPTURA VALIDA** ou **CAPTURA INVALIDA** e a lista dos passos para refazer. Refaça só os passos listados, numa sessão nova.

   A máscara pode esconder bytes reais de parâmetro que coincidam com a senha. O relatório lista esses quadros em "Quadros com máscara de senha" (no `--csv`, a coluna `mascarado`). Um quadro mascarado só é citado no mapa depois de conferido sem máscara, localmente, num terminal sem `TARAMPS_SENHA`.

Se o zip não tiver o log ("nenhum btsnoop dentro do zip"), reinicie o celular com o registro em **Ativado** e repita a sessão. Se ainda faltar, avise a IA com o modelo do celular e a versão do Android.

Se os horários do relatório estiverem deslocados em horas inteiras em relação à sua anotação, rode de novo com `--fuso 0`. A diferença em segundos entre os dois relógios sai da linha `conexao`: o horário exato do toque em "Connect", anotado em `obs`, menos o horário da primeira escrita do relatório é o valor do `--ajuste-s` (por exemplo, toque às 14:08:58 e primeira escrita às 14:09:01 → `--ajuste-s -3`).

### 1.5 Tabela de anotação

Uma tabela por sessão, em `protocolo/capturas/<sessão>-anotacao.csv`, no formato do modelo `protocolo/capturas/MODELO-anotacao.csv`. Pode editar no Excel: salve como **"CSV UTF-8 (delimitado por vírgulas)"** (no Excel em português o separador sai `;`). No Excel, valores que começam com `+` ou `−` viram fórmula: digite-os com um apóstrofo na frente (`'-0,5 dB`) ou formate as colunas `antigo` e `novo` como Texto antes de digitar. Não use `;` dentro de nenhum valor.

**Toda sessão começa com uma linha `conexao`**, que vai de quando você abre o app até 20 segundos depois de a tela inicial aparecer. Em `obs`, anote o horário exato do toque em "Connect": é ele que mede a diferença entre os relógios (ver 1.4). Se o app reconectar no meio da sessão (por exemplo na C1, ao desligar e religar o processador), anote outra linha `conexao` cobrindo a reconexão. Sem essa linha, as escritas da conexão viram "escrita sem anotação" e a captura sai inválida.

| Coluna | O que vai |
|---|---|
| `passo` | Identificador do passo, com a captura: `C5.1`, `C5.2`… |
| `tipo` | `passo` (um toque), `posicionamento` (trazer o valor perto do alvo, sem prova) ou `conexao` |
| `inicio` | Horário do toque, `HH:MM:SS`, lido no relógio do notebook logo depois do toque |
| `fim` | Só para `posicionamento` e `conexao`: horário em que terminou |
| `parametro` | Nome da lista abaixo |
| `canal` | `S1`…`S4` para saída, `A`/`B` para entrada; vazio se for geral |
| `antigo` / `novo` | O valor exatamente como o app mostra, com unidade: `-0,5 dB`, `80 Hz`, `LR24`, `1,23 V`, `OFF` |
| `obs` | Qualquer coisa estranha (o app travou, o valor pulou dois passos, etc.) |

Nomes de `parametro`: `ligado`, `master`, `mute`, `rota`, `ganho`, `hpf.freq`, `hpf.filtro`, `lpf.freq`, `lpf.filtro`, `delay`, `fase`, `peq.b<N>.freq`, `peq.b<N>.ganho`, `peq.b<N>.q`, `limiter.threshold`, `limiter.ataque`, `limiter.release`, `limiter.auto`, `geq.b<N>`, `geq.preset`, `peq_entrada.b<N>.freq`, `peq_entrada.b<N>.ganho`, `peq_entrada.b<N>.q`, `link`, `conexao`. `filtro` junta o tipo e a inclinação como o app mostra (`OFF`, `BT6`…`BT48`, `LR12`…`LR48`).

**Disciplina de cada passo**

- Um passo é **um toque** no botão + ou −, ou um toque num seletor. Não arraste.
- Logo depois do toque, anote o horário e o valor novo. Espere **pelo menos 20 segundos** antes do próximo toque.
- Quando o valor precisa andar muito (por exemplo do 0 dB ao −45 dB), faça um `posicionamento`: anote o início, leve o valor até perto do alvo do jeito mais rápido (digitando, se o campo aceitar, ou com vários toques), anote o fim e o valor final, espere 20 segundos e só então faça o passo de prova.
- Os passos de prova de cada parâmetro são: o **mínimo**, o **máximo** e **um valor intermediário**, cada um alcançado com um único toque (por exemplo −44,5 → −45,0 dB com o −).

### 1.6 Backup do ajuste em uso (preset A), antes da C0

As capturas mexem em todos os parâmetros. Antes de qualquer captura, salve e exporte o ajuste de hoje. A tela de presets fica no ícone de engrenagem (manual, p. 14).

1. **Preset A — o ajuste em uso hoje.** Sem alterar nada, toque em "+ New/Save", escolha **Smartphone** e salve como `A-em-uso`.
2. Exporte: deslize o nome do preset e toque em **Export**. Salve em Downloads (ou "Salvar em arquivos").
3. Copie para o notebook:

   ```powershell
   adb shell ls /sdcard/Download
   mkdir protocolo\presets
   adb pull /sdcard/Download/<arquivo-A> protocolo\presets\
   ```

## Parte D — Modo Demo (antes do processador)

Objetivo: aprender o máximo sem conectar ao processador. Abra o app e escolha "Demo Mode". Anote tudo em `protocolo/capturas/demo-telas.md` (vai para o git). Esta parte não precisa de snoop log nem de tabela de anotação.

### D1. Levantamento das telas

Para cada tela do menu Audio (roteamento, EQ gráfico, PEQ de entrada, crossover, delay, fase, limiter, PEQ de saída, níveis), anote:

- os controles que existem e as opções de cada seletor (ex.: tipos de filtro, presets de EQ);
- o mínimo e o máximo que o app aceita, e o **tamanho do passo** do + e do − (ex.: ganho de 0,5 em 0,5 dB; frequência de 1 em 1 Hz ou por degraus);
- quantas bandas tem o PEQ de saída e o de entrada, e se o PEQ de entrada separa A e B;
- se existe link de canais;
- na tela de presets, se aparece a memória "Factory default" e quais opções o Demo oferece (Save, Export, Load).

### D2. Pista do limiter no app

Na saída 1, com o limiter ligado e o threshold num valor qualquer, anote o threshold que a tela mostra em volts. Depois mude o ganho da saída 1 em alguns passos e veja se o número em volts muda sozinho. Repita com o master. Se mudar, o app converte o threshold usando o ganho, e isso é evidência para a pergunta "antes ou depois do ganho".

### D3. Presets no Demo

Se o Demo deixar salvar e exportar presets:

1. Sem mexer em nada, salve no Smartphone como `D0-demo-inicial` e exporte.
2. Monte a tabela do preset B (Parte 4.1, passo 3) no Demo, salve como `DB-demo-valores`, exporte e anote os valores reais em `protocolo/capturas/preset-DB-valores.md`.
3. Copie os dois arquivos para `protocolo\presets\` com `adb pull`, como em 1.6.

Se o arquivo exportado no Demo tiver o mesmo formato do exportado com o processador, dá para decifrar a estrutura dos parâmetros antes da fase com o processador.

## Parte C — Versão curta da fase com o processador

**Antes de começar:** os cabos RCA das 4 saídas do PRO estão **fora do amplificador** (pode deixar o multímetro na saída 1 para a 4.2). Ninguém toca em "Resetar", "Padrões de Fábrica", "Alterar senha" nem "DSP" na tela de presets.

### C.1 Preparação (Denis)

1. Opções do desenvolvedor → **Registro de rastreamento HCI do Bluetooth** → **Ativado** (não o "com filtro").
2. Desligue e religue o Bluetooth do celular.
3. Ligue o processador. O amplificador continua desconectado.
4. No notebook, num terminal novo: `$env:Path += ";C:\platform-tools"` e `$env:TARAMPS_SENHA = "<sua senha>"`. A senha não vai para nenhum arquivo.

### C.2 Sessão 1: captura C0 (Denis)

Igual à Parte 2:
1. anote o horário;
2. abra o app, toque em **Conectar** e digite a senha;
3. anote o horário exato do toque em "Conectar";
4. espere 30 s na tela inicial **sem tocar em nada**;
5. feche o app.

A IA tira o bugreport (`adb bugreport protocolo\capturasruto\C0-bugreport.zip`) e confere com o `btsnoop_att.py`.

### C.3 Sessão 2: backup e passos de prova

1. **Denis:** desligue e religue o Bluetooth, abra o app, conecte e digite a senha. Espere a tela inicial.
2. **Backup (Denis ou IA):** em Salvar → Novo/Salvar → **Smartphone**, salve como `AEMUSO`. Isso grava só no celular, não no processador. O arquivo cai em `Android/data/com.taramps.pro24bt_audio/files/presets/`, de onde a IA copia via `adb`.
3. **Passos de prova.** A IA pode executá-los via `adb`, com a anotação gerada automaticamente com o horário de cada toque, ou o Denis faz à mão e anota (§1.5). Cada passo é um toque e o seguinte só vem depois de 20 s. Todo passo é desfeito logo depois.

   | Passo | Tela | Toque | Escrita esperada (MAPA.md §4) | Desfazer |
   |---|---|---|---|---|
   | P1 | Nível das Saídas, Canal 1 | − uma vez | `*081*0*<ganho − 0,1>*` | + uma vez |
   | P2 | Limiter, Canal 1 | − uma vez (se estiver OFF, vai a 0,0) | `*071*0*0.0*` (ou o valor − 0,1) | + uma vez (volta para `*071*0*0.5*` = OFF) |
   | P3 | Crossover, Canal 1, HPF Filtro | Próximo uma vez | `*042*0*<índice + 1>*` | Anterior uma vez |
   | P4 | EQ 15 Bandas, banda 25 Hz | + uma vez | `020*0*<ganho + 0,1>` | − uma vez |

4. Feche o app. A IA tira o bugreport (`C1-bugreport.zip`), confere a captura e compara cada escrita com a esperada.
5. Se algum valor não voltou ao original, carregue o `AEMUSO` (deslizar para a direita → Carregar).

### C.4 Medição do limiter (Denis)

Siga a **4.2**: gerador do app, multímetro AC na saída RCA 1, Série 1 com o limiter em OFF e Série 2 com o threshold fixo, anotando a tensão e o LED. Ao terminar, desligue o gerador e carregue o `AEMUSO`.

### C.5 Fim

- Desligue o "Registro de rastreamento HCI" se não for mais capturar.
- Os bugreports ficam só em `protocolo\capturasruto\` (têm dados pessoais) e são apagados ao fim da história.

## Parte 2 — Captura C0: conectar sem mexer em nada

Objetivo: ver se a senha trafega em claro e se o app baixa o estado completo ao conectar.

1. Sessão nova (1.4, passos 1 e 2). Processador ligado, amplificador desconectado.
2. Anote o horário (`inicio` da linha `conexao`) e abra o app.
3. Toque em "Connect", escolha o processador e digite a senha.
4. Quando a tela inicial aparecer, **não toque em nada** por 30 segundos.
5. Anote o horário (`fim` da linha `conexao`), feche o app e tire o bugreport como `C0-bugreport.zip`.

A anotação da C0 tem uma linha só:

```
passo;tipo;inicio;fim;parametro;canal;antigo;novo;obs
C0;conexao;14:00:05;14:01:10;conexao;;;;Connect tocado às 14:00:31, senha digitada às 14:00:40
```

Entregue a C0 à IA assim que ela ficar pronta: a data de entrega é o início do orçamento de 2 dias. Se a C0 mostrar proteção (bytes que mudam a cada conexão sem relação com nada, cifra), a história para aqui e você não precisa fazer o resto.

## Parte 3 — Capturas C1…Cn: um parâmetro por vez

Uma captura por parâmetro. Pode fazer várias capturas na mesma sessão, uma depois da outra, com uma tabela de anotação só (os passos levam o nome da captura: `C5.1`, `C7.3`…). A tabela começa com a linha `conexao` (1.5). Comece cada captura com o app já conectado e parado há pelo menos 20 segundos.

As varreduras completas são feitas na saída 1. Para descobrir como o canal é endereçado, o ganho e o HPF ganham também um passo na saída 2 e um na saída 4.

| Captura | Parâmetro | Passos de prova |
|---|---|---|
| C1 | `ligado` | OFF → ON e ON → OFF → ON (botão da tela inicial) |
| C2 | `master` (−80 a 0 dB) | mínimo, máximo, 1 intermediário |
| C3 | `mute` | silenciar e reativar S1; silenciar e reativar S4 |
| C4 | `rota` S1 | A → B → A+B → A |
| C5 | `ganho` S1 (−45 a +15 dB) | mínimo, máximo, 1 intermediário; mais 1 passo em S2 e 1 em S4 |
| C6 | `hpf.freq` e `hpf.filtro` S1 | frequência: 10 Hz, 22 kHz e 1 intermediária. Filtro: OFF → BT6 → BT48 → LR12 → LR48 → de volta ao original. Mais 1 passo de frequência em S2 e 1 em S4 |
| C7 | `lpf.freq` e `lpf.filtro` S1 | igual à C6, só na S1 |
| C8 | `delay` S1 (0 a 272 cm) | mínimo, máximo, 1 intermediário |
| C9 | `fase` S1 | 0 → 180 → 0 |
| C10 | PEQ da saída S1 | banda 1: `freq` (10 Hz, 22 kHz, 1 intermediária), `ganho` (−12, +12, 1 intermediário), `q` (0,4, 10, 1 intermediário). Depois 1 passo de ganho na banda 2 e 1 na última banda. Anote em `obs` quantas bandas a tela tem |
| C11 | Limiter S1 | `limiter.threshold`: mínimo, máximo, 1 intermediário e OFF (anote o valor como o app mostra, em V ou OFF). `limiter.ataque`: 0,1 e 100 ms. `limiter.release`: 1 e 1600 ms. `limiter.auto`: liga e desliga |
| C12 | EQ gráfico da entrada | banda 1: −12,0, +12,0 e 1 intermediário. 1 passo na banda 15. Anote a frequência da banda em `obs` |
| C13 | `geq.preset` | escolher 1 preset de EQ da lista e voltar ao anterior (anote os nomes) |
| C14 | PEQ da entrada | igual à C10. Anote em `obs` se a tela separa entrada A e B |
| C15 | `link` | só se a tela tiver link de canais: ligar S1+S2 e desligar |

Ao terminar todas as capturas, carregue o preset A salvo na preparação (1.6): deslize `A-em-uso` → Load e confira na tela que voltou ao ajuste de antes.

## Parte 4 — Presets exportados e medição do limiter

### 4.1 Exportar a memória de fábrica (F) e o preset B

O preset A já foi salvo e exportado na preparação (1.6).

1. **Preset F — memória "Factory default".** O manual (p. 20) diz que o processador tem a memória "Factory default" além das 3 personalizáveis. Na tela de presets, procure essa memória e carregue-a (deslizar → Load). Depois salve no **Smartphone** como `F-fabrica`, exporte e copie para `protocolo\presets\`, como em 1.6. Se a tela não mostrar a memória "Factory default", pule este passo e anote isso em `preset-B-valores.md`. **Nunca** use "Reset Audio Settings" nem "Reset to Factory Defaults" para chegar a ela.
2. Carregue o preset A de volta e confira na tela que voltou ao ajuste de antes.
3. **Preset B — valores conhecidos.** Partindo do A, mude os parâmetros abaixo para estes valores, que são fáceis de achar nos bytes. Salve no **Smartphone** como `B-TESTE-NAO-USAR`.

   | Parâmetro | Canal | Valor |
   |---|---|---|
   | `master` | — | −23,5 dB |
   | `ganho` | S1 / S2 / S3 / S4 | −7,5 / +3,0 / −12,0 / −1,5 dB |
   | `hpf.freq` / `hpf.filtro` | S1 | 123 Hz / LR24 |
   | `lpf.freq` / `lpf.filtro` | S1 | 4567 Hz (ou o mais perto) / BT18 |
   | `delay` | S1 | 37 cm |
   | `fase` | S2 | 180 |
   | `rota` | S2 | A+B |
   | `peq.b1` | S1 | 1000 Hz, −3,5 dB, Q 2,5 |
   | `limiter.threshold` | S1 | um valor intermediário (anote como o app mostra) |
   | `geq.b1` / `geq.b15` | entrada | +1,3 / −2,7 dB |

   Se algum valor não existir no app, use o mais perto e anote o valor real. Guarde a tabela final em `protocolo/capturas/preset-B-valores.md`.
4. Exporte: deslize o nome do preset e toque em **Export**. Salve em Downloads (ou "Salvar em arquivos").
5. Copie para o notebook:

   ```powershell
   adb shell ls /sdcard/Download
   adb pull /sdcard/Download/<arquivo-B> protocolo\presets\
   ```

6. **C0b (recomendada):** com o preset B ainda carregado, repita a Parte 2 numa sessão nova e salve como `C0b-bugreport.zip`. Comparar a leitura ao conectar com os valores conhecidos acelera o mapa.
7. Carregue o preset A de volta (deslizar → Load) e confira na tela que voltou ao ajuste de antes. Depois que a história terminar, apague o preset B do celular, porque ele não é seguro para tocar com o amplificador ligado.

### 4.2 Medição do limiter

Objetivo: saber se o limiter atua antes ou depois do ganho da saída, e se o threshold mostrado em volts é a tensão real na saída.

**Montagem**

- Amplificador desconectado. Multímetro em **V AC**, ponta vermelha no pino central do RCA da saída 1 do PRO e ponta preta na carcaça do mesmo RCA. Anote o modelo do multímetro e se ele é True RMS.
- Gerador do app (Audio Generator → Fixed Generator): **200 Hz**, ganho do gerador em **−6 dB**. A frequência fica acima do passa-alta da S1 e dentro da faixa da maioria dos multímetros. Anote o valor exato que o app aceitar.
- Master fixo em **0 dB** durante toda a medição. Não mexa em filtros nem EQ da S1.
- Depois de cada mudança, espere 3 segundos e anote a tensão e se o **LED Limiter/Clip da S1** no processador e o indicador "Limiter / Out Clip" da tela acenderam (aceso/apagado).

**Série 1 — limiter da S1 em OFF (referência).** Varie o `ganho` da S1 de −20 dB a +15 dB, de 3 em 3 dB (−20, −17, … , +13, +15), e anote a tensão em cada ponto.

**Série 2 — limiter ligado, threshold fixo.** Ligue o limiter da S1 com `Auto` ligado e ponha o threshold num valor que o app mostre perto de **0,50 V**. Repita a mesma varredura de ganho. A cada ponto, anote a tensão medida **e o threshold que a tela do limiter mostra naquele momento**: se o número em volts mudar sozinho quando o ganho muda, isso também é uma pista.

**Série 3 (opcional) — outro threshold.** Threshold perto de **1,00 V**, mesma varredura e mesmas anotações.

Anote numa tabela em `protocolo/capturas/limiter-medicao.md`:

| Ganho S1 (dB) | Série 1, limiter OFF (V medido) | Série 1 (LED) | Série 2 (V medido) | Série 2 (threshold na tela) | Série 2 (LED) | Série 3 (V medido) | Série 3 (threshold na tela) | Série 3 (LED) |
|---|---|---|---|---|---|---|---|---|
| −20 | | | | | | | | |
| … | | | | | | | | |
| +15 | | | | | | | | |

No fim, desligue o gerador e carregue o preset A.

**Como a IA lê o resultado.** A Série 1 sobe 3 dB por passo até ceifar perto de 2,1 V, se 0 dB no gerador corresponder a 0 dBFS (com o gerador em −6 dB, isso aconteceria por volta de +6 dB de ganho). Os pontos acima disso só mostram o ceifamento. Se o limiter atua **depois** do ganho, a Série 2 sobe junto com a Série 1 até perto do threshold e fica parada ali, com o LED aceso, mesmo com o ganho subindo. Se atua **antes** do ganho, a Série 2 continua subindo no ritmo da Série 1 com o LED aceso, com o teto acompanhando o ganho. Se a Série 2 sai igual à Série 1 com o LED **sempre apagado**, o limiter nunca atuou: o resultado é inconclusivo, não "antes do ganho". Nesse caso, repita com o gerador mais alto ou o threshold mais baixo. A tensão do patamar, comparada com o threshold mostrado, diz se os volts do app são a tensão real na saída.

## O que entregar à IA

- `protocolo/capturas/bruto/*-bugreport.zip` (fica só no notebook; tem dados pessoais e é apagado ao fim da história).
- `protocolo/capturas/*-anotacao.csv`, `preset-B-valores.md` e `limiter-medicao.md` (vão para o git).
- `protocolo/apk/*.apk` e `protocolo/presets/*` (ficam só no notebook).
- O nome do pacote, a versão do app e o modelo do celular e do multímetro.
