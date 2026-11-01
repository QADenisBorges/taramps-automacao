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

- No aparelho, com o app no A52 (24/10, história 12a, `estado/log-ble/2026-10-24_1712-app-12a.log`):
  o eco de uma escrita chega em 60–240 ms, mas o da fase (`060`) leva ~1,1 s. O simulador usa os
  ~85 ms de 16/10 para todos; o `PrazoEco` do escritor é de 2 s.

## A conferir na história 12 (12a e 12b)

| Id | Suposição | Onde no código | Resultado no aparelho |
|---|---|---|---|
| ACK-241 | Fora de uso: o app não envia mais o 241 (decisão do Denis, 29/10/2026). O ack de `*241*n*` (carregar) é `#241#n#`, no mesmo prazo do 240 (~1,17 s). O do 240 foi medido (acima). | `Processar`, memórias; `AtrasoAckMemoria` | Confirmada (24/10, A52): `#241#2#` em 1,41 s e `#241#0#` em 1,40 s (`estado/log-ble/2026-10-24_1712-app-12a.log`). |
| CONTEUDO-MEMORIA | Uma memória guarda os 84 valores do Marco 1 e o ganho (200) e a frequência (201) do tom fixo (medido); não guarda o 202, o 210–213, o 251 e o 252 (não medido: eram iguais nas memórias carregadas). | `Processar`, memórias | Caiu (24/10): carregar a memória 1 (SPL) trocou o 200 (−45 → −25) e o 201 (1000 → 51); os nomes, o 202, o 251 e o 252 ficaram. O 202 e o 210–213 eram iguais nas duas memórias. Simulador corrigido. |
| MEMORIAS-INICIAIS | O conteúdo inicial das 3 memórias é igual ao estado semeado. | construtor | Não se aplica ao aparelho: é a semente do simulador. No aparelho, as 3 memórias são diferentes (SPL, MUSICA ORIG, FILME). |
| ECO-251-252 | O 251 e o 252 respondem com o eco no formato de leitura (`#252#v#`). (O nome `*23N*nome*` não ecoa: medido, acima.) | `Processar`, escrita |  |
| ACK-001 | O ack de `*001*antiga*nova*` é `#001#OK#` (ou `#001#ERRO#` com a antiga errada). | `Processar`, troca de senha | Não medido. Decisão do Denis de 24/10: reset e troca de senha só na recusa. |
| RESET-004 | O ack de `*004*` é `#004#`; o reset de fábrica volta o Marco 1 e as memórias ao estado de fábrica, apaga os nomes, volta a senha à de fábrica (`1234`) e a conexão continua. | `Processar`, `*004*`; `SenhaDeFabrica` | Não medido. Decisão do Denis de 24/10: reset e troca de senha só na recusa. |
| RESET-242 | O ack de `*242*` é `#242#`; o reset de áudio volta o Marco 1 ao estado de fábrica e mantém memórias, nomes, gerador, 251 e 252. | `Processar`, `*242*` | Não medido. Decisão do Denis de 24/10: reset e troca de senha só na recusa. |
| FABRICA | O estado de fábrica do Marco 1 (ganhos 0 dB, roteamento a/b/a/b, filtros OFF, limiter OFF `+0.5`, attack 50 ms, release 200 ms, EQs planos em 1 kHz e Q 1,0, preset Flat). | `Fabrica` | Não medido: depende do reset. Decisão do Denis de 24/10: reset e troca de senha só na recusa. |
| FORMATO-ECO | Um valor escrito volta com a formatação das leituras: dB e Q com sinal (`+3.0`, `+0.7`), cm e attack sem sinal, o resto inteiro; os especiais (`0.5`, `0.01`, `0.05`) como foram. | `Formatar` | Confirmada (24/10): 31 escritas da FILME; `*081*0*10.0*` → `#081#0#+10.0#`, `*092*2*0.7*` → `#092#2#+0.7#`, cm, attack e Hz sem sinal. Eco em 60–240 ms; o da fase (060) em ~1,1 s. |
| MODO-ESTRITO | O aparelho só aceita o texto exato do codificador do app (`*081*0*-3.0*`, não `-3`); fora disso, não aplica nem responde. | `ValidarEscrita` | Não medido: o app só manda o texto do codificador, e medir exigiria escrita fora do guardião (spec da 12a). |
| ESCRITA-INVALIDA | Uma escrita inválida (canal 9, valor fora da faixa, código só leitura ou desconhecido) não é aplicada e não tem resposta. | `ValidarEscrita`, `Recusar` | Não medido, pelo mesmo motivo do MODO-ESTRITO. |
| MUDO-SEM-SENHA | Antes da senha aceita, uma escrita é ignorada, sem resposta. (Uma leitura recebe `ERRO 0`: medido.) | `Processar` | Caiu em parte (24/10, notebook): antes da senha, a leitura `#080#` recebeu `ERRO 0` com CR LF em 29 ms. A escrita antes da senha não foi medida. Simulador corrigido (`estado/log-ble/2026-10-24_1725-sonda-12a.log`). |
| QUEDA-APOS-ERRO | A senha certa dentro da janela cancela a queda de 29 s; um segundo `ERRO` não reinicia o prazo. | `Processar`, senha | Confirmada (24/10, notebook): ERRO e logo a senha certa, e a conexão seguiu por mais de 45 s; dois ERRO a 10 s um do outro, e a queda veio 28,3 s depois do primeiro (`2026-10-24_1730-sonda-12a-b.log`). |
| VOLTIMETRO-CRLF | O órfão `ERRO 0` do voltímetro chega com `\r\n`. | `Processar`, leitura | Confirmada (24/10, notebook): `#005#` → `ERRO 0` com CR LF. |
| CLIP-ESPONTANEO | O clip espontâneo (`#150#bits#`) tem o formato da leitura; o período e o gatilho não foram medidos. | `EmitirClip` |  |
| MEMORIA-N-NOME | O nome da memória é lido em `#23N#` (resposta `23N#NOME`), e a memória 1 é o `n` 0 do 241 e o nome 231 (a 2 é `n` 1 e 232; a 3 é `n` 2 e 233). O app não escreve nem lê o nome (história 8c e decisão do Denis de 29/10/2026). | `QuadroMemoria`; `NomesDasMemorias` | Confirmada (24/10, A52): `n` 0 carregou a SPL (nome 231) e `n` 2 a FILME (nome 233). |
| REGRA-NOMES | A regra de nomes da Taramps (de 1 a 12 caracteres, A–Z, 0–9, espaço e `_`, em maiúsculas) vale para o nome do produto (`*002*NOME*`), como valia para as memórias no app da Taramps. | `Catalogo.NormalizarNomeProduto` |  |
| CARREGAR-SEM-NOMES | Fora de uso: o app não envia mais o 241 nem lê os nomes 23N (decisão do Denis, 29/10/2026). Carregar (`*241*n*`) troca os 84 valores do Marco 1 e não mexe nos nomes 23N; o nome é do aparelho, não da memória. | `Processar`, memórias | Confirmada (24/10, A52): os nomes 231–233 não mudaram ao carregar as memórias 1 e 3. |
| LINK-PARAMETROS | O link copia e repete os parâmetros de canal do Marco 1 menos o roteamento, como o app da Taramps (`BVM:16189`); o comportamento na tela é conferido na história 10. | `LinkCanais.Parametros` |  |
| SENHA-ERRADA-DESAUTENTICA | Com a sessão já autenticada, uma senha errada recebe a mesma resposta da certa e a sessão continua (medido; era a suposição contrária). | `Processar`, senha | Caiu (24/10, notebook): já autenticado, a senha errada recebeu `#000#PRO2.4MIR003#`, a leitura seguinte respondeu e não houve queda em 35 s. Simulador corrigido. |
| FILA-UNICA | As respostas com atraso saem de uma fila única, na ordem em que foram geradas: cada uma espera o seu atraso e a anterior (um eco de 85 ms seguido de uma leitura de 25 ms chega antes dela). | `Enviar` | Não medível (24/10): a 1122 só aceita escrita com resposta, e cada consulta é respondida antes da próxima, então as respostas nunca se sobrepõem. Elas sempre chegaram na ordem das perguntas. |
| QUEDA-DESCARTA-FILA | Desconectar ou cair joga fora as respostas que ainda não saíram; elas não chegam numa conexão nova. | `EmitirAgora`, `Soltar` | Não medível do notebook (24/10): como cada consulta é respondida antes da próxima, não sobra fila na queda. Na conexão nova não chegou nada além da resposta da senha. |
| ATRASO-RESPOSTAS | No simulador, a senha responde ~23 ms e cada leitura ~25 ms depois do pedido, sempre com o mesmo atraso. No aparelho o atraso varia (medido em 24/10, na coluna ao lado); os prazos do app são de 2 s ou mais. | `AtrasoSenha`, `AtrasoLeitura` | Em parte (24/10): pelo notebook, a senha em 23–175 ms e as leituras em 20–223 ms (mediana ~80; seguidas, ~25 ms). No A52, a senha em 115 ms e as leituras em 73–189 ms (mediana 94). O atraso não é fixo. O simulador fica com os valores fixos, porque os prazos do app são de 2 s ou mais. |
| GERADOR-MODO | O tom fixo é o 213 em 0 e o sweep é o 213 de 1 a 3; o 202 liga e desliga os dois. | `GeradorSinal.TocarTomAsync` |  |
| ECO-GERADOR | Os códigos 200–202 e 210–213 ecoam no formato de leitura (`#202#1#`), como o 081. | `Processar`, escrita; `GeradorSinal.EnviarParametroAsync` |  |
| ACK-002 | O ack de `*002*NOME*` (nome do produto) é `#002#NOME#`, com o nome aplicado; o app espera até 5 s, como nos outros comandos de aparelho inteiro. | `Processar`, nome do produto; `ComandosAparelho.NomearProdutoAsync` |  |
| NOME-PRODUTO | O 002 não muda o começo `PRO2.4MI` da resposta 000. Se mudar, a próxima conexão entra em modo só leitura, que é o lado seguro. | `Processar`, nome do produto; `ComandosAparelho.NomearProdutoAsync` |  |
| VOLTIMETRO-ESPONTANEO | O aparelho manda o voltímetro sozinho, como `#005#v#` (V × 10); a consulta `#005#` responde `ERRO 0` (medido). O período não foi medido. | `EmitirVoltimetro`; `MonitorAparelho` |  |
| CLIP-ENTRADA | O clip da entrada (`#111#v#`, ≠0 = clip) chega sozinho, no formato da leitura; o período e o gatilho não foram medidos. | `EmitirClipEntrada`; `MonitorAparelho` |  |
| PRESET-APLICA-TUDO | Escrever todos os ajustes de áudio diferentes em sequência e reler tudo confirma o preset no PRO 2.4BT; o aparelho não tem comando de preset. | `GerentePresets.AplicarInternoAsync` |  |
| CLIP-LIMITADOR | O 150 marca o limitador agindo: o aparelho manda `#150#bits#` com o bit da saída quando o pico na saída mais o master passa do limiar do limitador (071) dela. | `Sinal`; `TesteSeco` | A conferir na 12d (o teste a seco real). |

## Falhas injetáveis

- `CairNaEscrita = N`: a conexão cai ao receber a N-ésima escrita, sem aplicá-la.
- `CairDepoisDaEscrita = N`: a N-ésima escrita é aplicada, e a conexão cai logo depois, sem
  resposta (o aparelho fez, mas o app não soube). Usada nas quedas no meio do salvar e do
  carregar (história 6).
- `DefinirMemoria(n, código, índice, valor)`: muda uma memória para preparar um teste.
- `EmitirClip(bits)`, `EmitirClipEntrada(clip)` e `EmitirVoltimetro(decivolts)`: o aparelho
  manda o clip das saídas, o clip da entrada e o voltímetro sozinho, quando o teste quiser.
- `Sinal(canal, dB)` (história 13e): o pico que chega à saída antes do limitador, sem o master;
  com o nível mais o master acima do limiar do 071 da saída, o aparelho manda o `#150#bits#`.
- `TamanhoMaximoFragmento = N`: quebra cada mensagem em notificações de tamanho sorteado, de 1 a
  N bytes (N até 20), em vez dos pedaços de 20.
- `AtrasoSenha` e `AtrasoLeitura` em zero: a resposta sai de dentro da própria escrita, para os
  testes que não andam o relógio falso enquanto conectam e leem.
