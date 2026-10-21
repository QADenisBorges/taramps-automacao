# Suposições do processador simulado

O `ProcessadorSimulado` imita o PRO 2.4BT + AUDIO pelo que foi **medido** (MAPA §2 e §9,
histórias 1 e 2). O que **não foi medido** está marcado no código com `// SUPOSIÇÃO:` e listado
aqui. A história 12 confere cada item no aparelho real e marca o resultado.

## O que foi medido (não é suposição)

- Senha certa → `#000#PRO2.4MIR003#`; errada → `#000#ERRO#`; a conexão cai ~29 s depois de um
  `ERRO` (20/10).
- Leituras `#CCC#[c#]v#\r\n`, banda `020#b#v`, nomes `23N#NOME` sem `#` nas pontas, extras da
  história 2, voltímetro `#005#` respondido por um órfão `ERRO 0` (16/10 e 20/10).
- Escrita `*081*0*-12.0*` → eco `#081#0#-12.0#` ~85 ms depois, sem `OK` (16/10).
- Os valores da semente: `estado/leituras/2026-10-18_2145.json` (Marco 1) e os extras de 20/10.

## A conferir na história 12

| # | Suposição | Onde no código | Resultado no aparelho |
|---|---|---|---|
| 1 | O ack de `*240*n*` (salvar) e `*241*n*` (carregar) é o eco `#240#n#` / `#241#n#`, ~85 ms depois. | `Processar`, memórias | |
| 2 | Uma memória guarda só os 84 valores do Marco 1 (não o gerador, os nomes, o 251 e o 252). | `Processar`, memórias | |
| 3 | O conteúdo inicial das 3 memórias é igual ao estado semeado. | construtor | |
| 4 | O ack de `*23N*nome*` é `23N#nome`; o 251 e o 252 respondem com o eco no formato de leitura (`#252#v#`). | `Processar`, escrita | |
| 5 | O ack de `*001*antiga*nova*` é `#001#OK#` (ou `#001#ERRO#` com a antiga errada). | `Processar`, troca de senha | |
| 6 | O ack de `*004*` é `#004#`; o reset de fábrica volta o Marco 1 e as memórias ao estado de fábrica, apaga os nomes, volta a senha à de fábrica (`1234`) e a conexão continua. | `Processar`, `*004*`; `SenhaDeFabrica` | |
| 7 | O ack de `*242*` é `#242#`; o reset de áudio volta o Marco 1 ao estado de fábrica e mantém memórias, nomes, gerador, 251 e 252. | `Processar`, `*242*` | |
| 8 | O estado de fábrica do Marco 1 (ganhos 0 dB, roteamento a/b/a/b, filtros OFF, limiter OFF `+0.5`, attack 50 ms, release 200 ms, EQs planos em 1 kHz e Q 1,0, preset Flat). | `Fabrica` | |
| 9 | Um valor escrito volta com a formatação das leituras: dB e Q com sinal (`+3.0`, `+0.7`), cm e attack sem sinal, o resto inteiro; os especiais (`0.5`, `0.01`, `0.05`) como foram. | `Formatar` | |
| 10 | O aparelho só aceita o texto exato do codificador do app (`*081*0*-3.0*`, não `-3`); fora disso, não aplica nem responde. | `ValidarEscrita` | |
| 11 | Uma escrita inválida (canal 9, valor fora da faixa, código só leitura ou desconhecido) não é aplicada e não tem resposta. | `ValidarEscrita`, `Recusar` | |
| 12 | Antes da senha aceita, o aparelho ignora leituras e escritas, sem resposta. | `Processar` | |
| 13 | A senha certa dentro da janela cancela a queda de 29 s; um segundo `ERRO` não reinicia o prazo. | `Processar`, senha | |
| 14 | O órfão `ERRO 0` do voltímetro chega com `\r\n`. | `Processar`, leitura | |
| 15 | O clip espontâneo (`#150#bits#`) tem o formato da leitura; o período e o gatilho não foram medidos. | `EmitirClip` | |
