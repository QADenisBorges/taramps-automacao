# Suposições do processador simulado

O `ProcessadorSimulado` imita o PRO 2.4BT + AUDIO pelo que foi **medido** (MAPA §2 e §9,
histórias 1 e 2). O que **não foi medido** está listado aqui, uma linha por suposição. Cada linha
tem um id; o mesmo id está em `Suposicoes.cs`, com o mesmo texto, e aparece como
`Suposição <ID>` no código onde a suposição vale (no simulador ou, quando é o app que depende
dela, no núcleo). O `SuposicoesTestes` confere que as listas batem nos dois sentidos: todo id
marcado no código está aqui, e todo id daqui está marcado no código. A história 12 confere cada
item no aparelho real e marca o resultado.

## O que foi medido (não é suposição)

- Senha certa → `#000#PRO2.4MIR003#`; errada → `#000#ERRO#`; a conexão cai ~29 s depois de um
  `ERRO` (20/10).
- Leituras `#CCC#[c#]v#\r\n`, banda `020#b#v`, nomes `23N#NOME` sem `#` nas pontas, extras da
  história 2, voltímetro `#005#` respondido por um órfão `ERRO 0` (16/10 e 20/10).
- Notificações de até 20 bytes: a resposta da senha (`#000#PRO2.4MIR003#\r\n`, 20 bytes) chega
  numa notificação só, e nenhuma passa de 20 (`estado/log-ble/2026-10-20_1322-memoria.log`). No
  simulador: `MaximoPorNotificacao`.
- Escrita `*081*0*-12.0*` → eco `#081#0#-12.0#` ~85 ms depois, sem `OK` (16/10).
- Os valores da semente: `estado/leituras/2026-10-18_2145.json` (Marco 1) e os extras de 20/10.
- Salvar memória (20/10, `estado/log-ble/2026-10-20_1322-memoria.log`, linhas 174–181, e
  `estado/ajustes/2026-10-20_1322.json`): `*240*0*` → `#240#0#\r\n` **~1,17 s** depois
  (13:22:44.254 → 13:22:45.427). O nome `*231*SPL*`, enviado depois do 240, foi aceito **sem
  eco**; a releitura respondeu `231#SPL\r\n` (chegou duas vezes). No simulador:
  `AtrasoAckSalvarReal`, e as escritas de nome não respondem.

## A conferir na história 12

| Id | Suposição | Onde no código | Resultado no aparelho |
|---|---|---|---|
| ACK-241 | O ack de `*241*n*` (carregar) é `#241#n#`, no mesmo prazo do 240 (~1,17 s). O do 240 foi medido (acima). | `Processar`, memórias; `AtrasoAckMemoria`; `GerenteMemorias.PrazoAckMemoria` | |
| CONTEUDO-MEMORIA | Uma memória guarda só os 84 valores do Marco 1 (não o gerador, os nomes, o 251 e o 252). | `Processar`, memórias | |
| MEMORIAS-INICIAIS | O conteúdo inicial das 3 memórias é igual ao estado semeado. | construtor | |
| ECO-251-252 | O 251 e o 252 respondem com o eco no formato de leitura (`#252#v#`). (O nome `*23N*nome*` não ecoa: medido, acima.) | `Processar`, escrita | |
| ACK-001 | O ack de `*001*antiga*nova*` é `#001#OK#` (ou `#001#ERRO#` com a antiga errada). | `Processar`, troca de senha | |
| RESET-004 | O ack de `*004*` é `#004#`; o reset de fábrica volta o Marco 1 e as memórias ao estado de fábrica, apaga os nomes, volta a senha à de fábrica (`1234`) e a conexão continua. | `Processar`, `*004*`; `SenhaDeFabrica` | |
| RESET-242 | O ack de `*242*` é `#242#`; o reset de áudio volta o Marco 1 ao estado de fábrica e mantém memórias, nomes, gerador, 251 e 252. | `Processar`, `*242*` | |
| FABRICA | O estado de fábrica do Marco 1 (ganhos 0 dB, roteamento a/b/a/b, filtros OFF, limiter OFF `+0.5`, attack 50 ms, release 200 ms, EQs planos em 1 kHz e Q 1,0, preset Flat). | `Fabrica` | |
| FORMATO-ECO | Um valor escrito volta com a formatação das leituras: dB e Q com sinal (`+3.0`, `+0.7`), cm e attack sem sinal, o resto inteiro; os especiais (`0.5`, `0.01`, `0.05`) como foram. | `Formatar` | |
| MODO-ESTRITO | O aparelho só aceita o texto exato do codificador do app (`*081*0*-3.0*`, não `-3`); fora disso, não aplica nem responde. | `ValidarEscrita` | |
| ESCRITA-INVALIDA | Uma escrita inválida (canal 9, valor fora da faixa, código só leitura ou desconhecido) não é aplicada e não tem resposta. | `ValidarEscrita`, `Recusar` | |
| MUDO-SEM-SENHA | Antes da senha aceita, o aparelho ignora leituras e escritas, sem resposta. | `Processar` | |
| QUEDA-APOS-ERRO | A senha certa dentro da janela cancela a queda de 29 s; um segundo `ERRO` não reinicia o prazo. | `Processar`, senha | |
| VOLTIMETRO-CRLF | O órfão `ERRO 0` do voltímetro chega com `\r\n`. | `Processar`, leitura | |
| CLIP-ESPONTANEO | O clip espontâneo (`#150#bits#`) tem o formato da leitura; o período e o gatilho não foram medidos. | `EmitirClip` | |
| MEMORIA-N-NOME | O nome da memória vai como `*23N*NOME*`, e a memória 1 é o `n` 0 do 240/241 e o nome 231 (a 2 é `n` 1 e 232; a 3 é `n` 2 e 233). | `QuadroMemoria`; `Catalogo.ComandoDeMemoria` | |
| REGRA-NOMES | A regra de nomes da Taramps vale para as memórias: de 1 a 12 caracteres, A–Z, 0–9, espaço e `_`, em maiúsculas. | `Catalogo.NormalizarNomeMemoria` | |
| CARREGAR-SEM-NOMES | Carregar (`*241*n*`) troca os 84 valores do Marco 1 e não mexe nos nomes 23N; o nome é do aparelho, não da memória. | `Processar`, memórias | |
| ACK-240-SALVA | A resposta `#240#n#` quer dizer que a memória foi gravada (o app diz "Memória N salva"; sem ela, "enviada"). Não há como ler o conteúdo de uma memória sem carregá-la: numa queda no meio, o app só diz "pode não ter sido salva". | `GerenteMemorias` | |
| LINK-PARAMETROS | O link copia e repete os parâmetros de canal do Marco 1 menos o roteamento, como o app da Taramps (`BVM:16189`); o comportamento na tela é conferido na história 10. | `LinkCanais.Parametros` | |
| SENHA-ERRADA-DESAUTENTICA | Uma senha errada com a sessão já autenticada tira a autenticação: leituras e escritas voltam a ser ignoradas até a senha certa. | `Processar`, senha | |
| FILA-UNICA | As respostas com atraso saem de uma fila única, na ordem em que foram geradas: cada uma espera o seu atraso e a anterior (um eco de 85 ms seguido de uma leitura de 25 ms chega antes dela). | `Enviar` | |
| QUEDA-DESCARTA-FILA | Desconectar ou cair joga fora as respostas que ainda não saíram; elas não chegam numa conexão nova. | `EmitirAgora`, `Soltar` | |
| ATRASO-RESPOSTAS | A senha responde ~23 ms e cada leitura ~25 ms depois do pedido, sempre com o mesmo atraso. No log de 20/10, 13:22 (`2026-10-20_1322-memoria.log`), a senha levou 23 ms e as leituras de 18 a 135 ms (as primeiras perto de 25 ms). | `AtrasoSenha`, `AtrasoLeitura` | |

## Falhas injetáveis

- `CairNaEscrita = N`: a conexão cai ao receber a N-ésima escrita, sem aplicá-la.
- `CairDepoisDaEscrita = N`: a N-ésima escrita é aplicada, e a conexão cai logo depois, sem
  resposta (o aparelho fez, mas o app não soube). Usada nas quedas no meio do salvar e do
  carregar (história 6).
- `DefinirMemoria(n, código, índice, valor)`: muda uma memória para preparar um teste.
- `TamanhoMaximoFragmento = N`: quebra cada mensagem em notificações de tamanho sorteado, de 1 a
  N bytes (N até 20), em vez dos pedaços de 20.
- `AtrasoSenha` e `AtrasoLeitura` em zero: a resposta sai de dentro da própria escrita, para os
  testes que não andam o relógio falso enquanto conectam e leem.
