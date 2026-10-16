# Mapa do protocolo BLE — PRO 2.4BT + AUDIO

**Versão do mapa:** 0 (esqueleto; nenhuma codificação provada ainda)
**Status:** aguardando a captura C0 e o APK (ver `ROTEIRO-CAPTURA.md`).
**Início do orçamento:** pendente. São cerca de 2 dias de análise, contados a partir da entrega da captura C0. A data de entrega entra aqui.
**Alvo:** firmware P012, app 1.0 (23) (manual, p. 15). FCC ID 2BQGR-PRO24BTAUDIO.

**Fontes**

| Fonte | Arquivo | Status |
|---|---|---|
| Análise estática do APK | `APK.md` | pendente |
| Capturas anotadas (HCI snoop log) | `capturas/<sessão>-anotacao.csv` + bruto local | pendente |
| Presets exportados (A em uso, F memória de fábrica, B com valores conhecidos) | `capturas/preset-B-valores.md` + arquivos locais | pendente |
| Medição do limiter | `capturas/limiter-medicao.md` | pendente |

**Regras deste mapa**

- Toda codificação cita a captura que a prova: arquivo, quadro (número do `btsnoop_att.py`, igual ao do Wireshark), valor antigo → novo e o hex, com a senha mascarada (`--senha` ou `TARAMPS_SENHA`).
- Codificação sem captura citada não conta como mapeada.
- Escala não linear exige 3 ou mais valores do mesmo parâmetro, incluindo mínimo e máximo; o resultado é uma fórmula ajustada ou uma tabela.
- Os nomes dos parâmetros são os do `ROTEIRO-CAPTURA.md`.
- Um quadro que o `btsnoop_att.py` lista em "Quadros com máscara de senha" (ou com `mascarado = 1` no CSV) só é citado depois de conferido sem máscara, localmente: a máscara pode esconder bytes reais de parâmetro.

## 1. Serviço e características

| Handle | UUID | Propriedades | Papel | Prova |
|---|---|---|---|---|
| pendente | | | | C0 (descoberta GATT) + `APK.md` §4 |

## 2. Autenticação

Pendente. Como a senha de 4 dígitos vai ao processador (em claro, número, hash, desafio-resposta), o que ele responde no acerto e no erro, e se há algum segredo além da senha. Prova: C0. O hex citado aqui sempre sai com a senha mascarada.

## 3. Leitura completa ao conectar

Pendente. Hipótese do `protocol-discovery.md`: o app baixa o estado completo ao conectar. Registrar: o comando que pede o estado, a ordem e o tamanho das respostas, e a posição de cada parâmetro no bloco. Prova: C0 e C0b (preset B com valores conhecidos).

## 4. Formato de escrita

Pendente. Estrutura do quadro de escrita (cabeçalho, comando, canal ou banda, valor, verificação), tipo de escrita ATT (Write Request ou Write Command), se o processador confirma por notificação e se existe polling periódico (o `btsnoop_att.py` lista as escritas repetidas).

## 5. Arquivo de preset

Pendente. Formato do arquivo exportado e relação com o bloco da leitura completa. Prova: arquivos A, F e B comparados com `capturas/preset-B-valores.md`; o F serve de referência neutra.

## 6. Parâmetros do Marco 1

Uma tabela por parâmetro. "Característica" é o handle (e o UUID da seção 1); "Comando" é o código ou cabeçalho do quadro; "Offset" é a posição do valor no quadro; "Tipo" é o tipo binário (ex.: int16 LE); "Escala" é a conversão do valor do app para o valor no protocolo.

Endereçamento de canal (S1…S4) e de banda: pendente (provas: C5 e C6 com passos em S2 e S4; C10 com bandas 1, 2 e a última).

### Gerais

#### `ligado` — ON/OFF do processador

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | OFF / ON → pendente | pendente (C1) |

#### `master` — Master

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | −80 a 0 dB → pendente | pendente (C2) |

#### `link` — Link de canais (se existir)

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | a confirmar na tela → pendente | pendente (C15) |

### Por saída (S1…S4)

#### `ganho` — Ganho da saída

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | −45 a +15 dB → pendente | pendente (C5) |

#### `mute` — Mute

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | ligado / desligado → pendente | pendente (C3) |

#### `rota` — Roteamento

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | A / B / A+B → pendente | pendente (C4) |

#### `hpf.freq` — Passa-alta, frequência

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | 10 Hz a 22 kHz → pendente | pendente (C6) |

#### `hpf.filtro` — Passa-alta, tipo e inclinação

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | OFF, BT6…BT48, LR12…LR48 → pendente | pendente (C6) |

#### `lpf.freq` — Passa-baixa, frequência

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | 10 Hz a 22 kHz → pendente | pendente (C7) |

#### `lpf.filtro` — Passa-baixa, tipo e inclinação

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | OFF, BT6…BT48, LR12…LR48 → pendente | pendente (C7) |

#### `delay` — Alinhamento (delay)

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | 0 a 272 cm → pendente | pendente (C8) |

#### `fase` — Fase

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | 0 / 180 → pendente | pendente (C9) |

#### `peq.b<N>.freq` — PEQ da saída, frequência

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | 10 Hz a 22 kHz → pendente | pendente (C10) |

#### `peq.b<N>.ganho` — PEQ da saída, ganho

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | −12 a +12 dB → pendente | pendente (C10) |

#### `peq.b<N>.q` — PEQ da saída, Q

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | 0,4 a 10 → pendente | pendente (C10) |

#### `limiter.threshold` — Limiter, threshold

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | −48 a 0 dB ou OFF (o app mostra em V) → pendente | pendente (C11) |

#### `limiter.ataque` — Limiter, ataque

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | 0,1 a 100 ms → pendente | pendente (C11) |

#### `limiter.release` — Limiter, release

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | 1 a 1600 ms → pendente | pendente (C11) |

#### `limiter.auto` — Limiter, Auto

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | ligado / desligado → pendente | pendente (C11) |

### Entrada

#### `geq.b<N>` — EQ gráfico, banda N (15 bandas)

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | −12 a +12 dB, passo 0,1 → pendente | pendente (C12) |

#### `geq.preset` — Preset de EQ

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | lista do app → pendente | pendente (C13) |

#### `peq_entrada.b<N>.freq` — PEQ da entrada, frequência

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | 10 Hz a 22 kHz → pendente | pendente (C14) |

#### `peq_entrada.b<N>.ganho` — PEQ da entrada, ganho

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | −12 a +12 dB → pendente | pendente (C14) |

#### `peq_entrada.b<N>.q` — PEQ da entrada, Q

| Característica | Comando | Offset | Tipo | Escala | Faixa no app → no protocolo | Captura que prova |
|---|---|---|---|---|---|---|
| pendente | pendente | pendente | pendente | pendente | 0,4 a 10 → pendente | pendente (C14) |

## 7. Limiter vs. ganho da saída

Pergunta aberta do SPEC: o limiter atua antes ou depois do ganho da saída, e qual é a unidade do threshold no protocolo?

| Item | Resposta | Evidência |
|---|---|---|
| Unidade do threshold no protocolo (dB, V, código) | pendente | captura C11 + `APK.md` §6 |
| Conversão dB ↔ V que o app mostra | pendente | `APK.md` §6 + medição |
| O limiter atua antes ou depois do ganho da saída | pendente | `capturas/limiter-medicao.md`, séries 1 e 2 |
| A tensão do patamar bate com o threshold mostrado em V | pendente | `capturas/limiter-medicao.md` |

Como ler a medição, olhando só os pontos abaixo do ceifamento da Série 1 (≈2,1 V, se 0 dB no gerador corresponder a 0 dBFS). Cada ponto anota se o LED Limiter/Clip da S1 e o indicador da tela acenderam:
- Se a Série 2 para num patamar que não sobe com o ganho, com o LED aceso, o limiter atua depois do ganho.
- Se a Série 2 acompanha a Série 1 com o LED aceso, o limiter atua antes do ganho.
- Se a Série 2 é igual à Série 1 com o LED sempre apagado, o resultado é inconclusivo (o limiter nunca atuou): repetir com o gerador mais alto ou o threshold mais baixo. Isso não conta como "antes do ganho".
- Se o threshold em V mostrado na tela muda quando o ganho muda, o app está convertendo um threshold interno pelo ganho. Isso entra como evidência.

## 8. Lacunas

- Tudo acima está pendente até a C0 e o APK.
- Número de bandas do PEQ de saída e de entrada: sai do APK.
- O link de canais só entra se existir na tela do app.

## 9. Critério de parada

Preencher só se disparar. Disparos: protocolo protegido (`APK.md` §9 ou payload da captura que muda sem relação com o valor), ou 2 dias de análise desde a entrega da C0 sem bytes legíveis.

| Campo | Valor |
|---|---|
| Disparo | — |
| Data | — |
| Evidência | — |
| O que foi tentado | — |
| Recomendação | Procurar a Taramps e pedir documentação do protocolo ou acesso para integração |
