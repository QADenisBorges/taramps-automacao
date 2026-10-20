---
title: 'História 2: catálogo de parâmetros e leitura completa do aparelho'
type: 'feature'
created: '2026-10-20'
status: 'in-review'
route: 'dispatch'
baseline_commit: '853673aecb815b2a8f2c0f52111ca47398df627f'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/SPEC.md'
  - '{project-root}/protocolo/MAPA.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** O app conecta e autentica, mas não sabe ler nada. Sem um catálogo que codifique e decodifique cada parâmetro, e sem uma leitura completa, não existem as telas (história 3) nem a paridade com o app da Taramps (história 4) (CAP-2).

**Approach:** Criar no núcleo um catálogo tipado com os parâmetros do MAPA §4, mais o gerador, o remote out, o Audio Classic, a monitoração e os nomes das memórias. Fazer uma leitura completa, uma consulta por vez, que monta um retrato do aparelho com o valor lido, o texto cru, as falhas e os órfãos. A meta é 84 de 84 no Marco 1.

**Decisões do Denis (20/10/2026):**
- A leitura completa roda sozinha logo depois de a senha ser aceita. A tela mostra o placar ("84 de 84") e uma lista simples de texto (código, saída, valor), com o botão "Ler de novo".
- Os extras que o app da Taramps nunca pede (voltímetro 005, clip 111 e 150, nomes das memórias 231 a 233) são perguntados uma vez em cada leitura, e o texto cru fica registrado, seja o que vier (inclusive "sem resposta"). Gerador (200 a 213), remote out (252) e Audio Classic (251) também entram nos extras.
- O plano fica inteiro, com ~2.400 tokens, acima da meta de 1.600: catálogo, leitura e trava andam juntos.

## Boundaries & Constraints

**Always:**
- Formatos pelo MAPA §2 e §9: global `#CCC#`, canal `#CCC#c#` (c = 0 a 3), banda `020#b` (b = 0 a 14). Respostas: `#CCC#c#valor#`, `#CCC#valor#`, `020#b#v`, com sinal e uma casa decimal.
- Decodificação por tipo: dB, Q, cm e attack com 1 casa; Hz, release e índices inteiros. Roteamento `a`/`b`/`a+b` (aceita `ab`). Mute e limiter auto: ≠0 = ligado. Fase só 0 ou 180. Threshold `0.5` = OFF. Attack `0.01`/`0.05` = auto. Filtros pelo índice do MAPA (0 OFF, 1–7 LR, 8–15 BT). Preset de EQ 0–11 e 255 Custom. Um valor fora da faixa é guardado com a marca "fora da faixa", nunca corrigido.
- Codificação de escrita (`*CCC*c*v*`, `020*b*v` sem `*` nas pontas, `%.1f` com ponto) existe e é testada, mas **nenhuma escrita sai pelo BLE**: a trava continua fechada para escritas.
- A trava passa a ter **lista de códigos**: só sai uma leitura de código que está no catálogo, no escopo certo. `#CCC#` fora do catálogo (por exemplo `#004#`) é recusado antes do BLE.
- Uma consulta pendente por vez, ~30 ms entre envios, 5 s de prazo por consulta e uma nova tentativa só para a consulta que falhou. A resposta casa pelo código e pelo canal ou banda; o eco do envio e o que não casa vão como órfãos para o retrato.
- Se a conexão cair no meio, a leitura para e o retrato fica "incompleto", com o que já foi lido.
- O retrato separa o Marco 1 (84 consultas) dos extras. O placar mostrado é "84 de 84" para o Marco 1 e um placar à parte para os extras.

**Never:**
- Escrever ajustes, carregar ou salvar memórias, ligar o gerador ou o remote out.
- Telas de leitura por canal (história 3) e comparação com o app da Taramps (história 4).
- Ler ou decifrar `.trps`.

## I/O & Edge-Case Matrix

| Cenário | Entrada / estado | Esperado | Erro |
|---|---|---|---|
| Caminho feliz | Aparelho responde tudo | Marco 1: 84 de 84, em ~15 s | — |
| Banda | `020#3#-1.0` | Banda 100 Hz = −1,0 dB | — |
| Roteamento `ab` | `#010#2#ab#` | Saída 3 = A+B | — |
| Threshold OFF | `#071#1#0.5#` | Saída 2: limiter OFF | — |
| Fora da faixa | `#060#0#90#` | Valor 90 marcado "fora da faixa" | Conta como lido |
| Sem resposta | Uma consulta muda | Uma nova tentativa; se falhar, entra em falhas | A leitura segue |
| Queda | Cai na consulta 40 | Para; retrato "incompleto" com 39 lidos | "A conexão caiu" |
| Órfão | `#150#3#` no meio | Vai para órfãos; não estraga a consulta pendente | — |
| Trava | Pedido `#004#` | Recusado sem envio | Registrado no log |
| Escrita | Pedido `*081*0*-3.0*` | Recusado sem envio | — |

</frozen-after-approval>

## Code Map

- `protocolo/MAPA.md` §2, §4, §9 -- formatos, faixas, índices e respostas reais.
- `ferramentas/protocolo_taramps.py` (`CATALOGO` l.77-120, `_extrair` l.514-548, `converter` l.618-658) e `ferramentas/cliente_ble.py` (ritmo l.33-40, `consultar` l.315-319) -- só consulta: 22 parâmetros = 5 globais + 16×4 por canal + 15 bandas = 84; sem nova tentativa por consulta no protótipo.
- `estado/leituras/2026-10-18_2145.json` -- respostas cruas reais (`#081#0#+10.0#`, `020#0#+0.0`, `#010#0#b#`, `#100#0#0#`), útil como dado de teste.
- `app-controle/src/BgsControle.Nucleo/Protocolo/Quadros.cs` -- reusar `Analisar`, `MontadorQuadros`, `TerminaEmSeparador`.
- `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs` -- `EnviarAsync` (l.254) é a única porta; a trava (l.64-67, l.303-320) aceita qualquer `#CCC#` hoje; `AoReceber` (l.528) manda tudo que não é a senha para o log como órfão.
- Não tocar em `ferramentas/`, `estado/` nem `protocolo/`.

## Tasks & Acceptance

**Execution:**
- [x] `app-controle/src/BgsControle.Nucleo/Catalogo/` -- tipos `Parametro` (código, nome, escopo, tipo, faixa, casas, opções, especiais), `Catalogo` com o Marco 1 e os extras, codificar e decodificar -- uma fonte única para a trava, a leitura e as escritas futuras.
- [x] `app-controle/src/BgsControle.Nucleo/Leitura/` -- `Retrato` (itens lidos com valor e cru, falhas com motivo, órfãos, placares, início, fim, completo ou incompleto) e `LeitorCompleto` (fila de consultas pela porta da sessão, casamento, prazo, nova tentativa, parada na queda).
- [x] `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs` -- trava pela lista do catálogo; entregar as respostas fora da senha ao leitor (evento), mantendo o log.
- [x] `app-controle/src/BgsControle.App/` -- leitura logo depois da senha aceita; placar, lista de texto e o botão "Ler de novo"; o último retrato salvo em JSON nos arquivos privados do app.
- [x] `app-controle/tests/BgsControle.Nucleo.Testes/` -- cada parâmetro: decodificar uma resposta real e codificar uma escrita; cada linha da matriz; a trava recusa código fora do catálogo.

**Acceptance Criteria:**
- Given o transporte falso que responde as 84 consultas com as respostas cruas de `2026-10-18_2145.json`, when a leitura termina, then o retrato bate valor a valor com aquele arquivo.
- Given o aparelho real com a FILME carregada, when o Denis lê pelo app, then o Marco 1 dá 84 de 84 e o tempo fica perto de 15 s.
- Given qualquer leitura, when o transporte falso é inspecionado, then ele só recebeu a senha e consultas de leitura do catálogo.

## Design Notes

O catálogo é a fonte única: a trava pergunta a ele se um quadro é uma leitura conhecida, o leitor monta a fila a partir dele, e as histórias de escrita vão usar o mesmo codificador. O retrato guarda sempre o texto cru ao lado do valor decodificado, para que a história 4 explique cada diferença com o app da Taramps.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros, 0 avisos.

**Manual checks (if no CLI):**
- No celular, com o app da Taramps fechado: conectar, ler, ver "84 de 84" e conferir alguns valores da FILME (atrasos L 0 / R 100 / sub 80 cm; fase do sub 180°).

## Implementation Notes

- 20/10: o Denis confirmou no chat que a banda 3 é 100 Hz (MAPA §4 e leitura real); a linha da matriz foi corrigida de 160 para 100 Hz por decisão dele.

- Implementado em 20/10: `Nucleo/Catalogo/` (`Parametro`, `Catalogo`, `Consulta`, `Valor`), `Nucleo/Leitura/` (`Retrato`, `LeitorCompleto`, `TemposLeitura`), trava e evento `Recebeu` na `Sessao`, tela com placar, lista e "Ler de novo", retrato em `files/ultimo-retrato.json`. `dotnet test`: 275 passam (93 da história 1 + 182 novos). Build Android Debug: 0 erros, 0 avisos. **Falta o teste no aparelho real** (84 de 84 com a FILME, ~15 s).
- Os tipos do catálogo ficam no namespace raiz `BgsControle.Nucleo`, como a `Sessao`: um namespace `...Catalogo` esconderia a classe `Catalogo` de todo código do núcleo.
- **Divergência na matriz:** a linha "Banda `020#3#-1.0` → Banda 160 Hz" não bate com o MAPA §4 nem com `2026-10-18_2145.json`, em que a banda 3 é 100 Hz e a 160 Hz é a banda 4. O código segue o MAPA (banda 3 = 100 Hz); o teste confere −1,0 dB na banda 3 e o rótulo "banda 100 Hz". A confirmar pelo Denis.
- Extras: "perguntados uma vez em cada leitura" foi lido como **sem nova tentativa** (só o Marco 1 tem a segunda tentativa). Um extra mudo custa 5 s; se os 15 ficarem mudos, a leitura toda leva ~75 s a mais depois do Marco 1 (o placar do Marco 1 já aparece antes). O extra casa só pelo código e guarda o conteúdo inteiro (`#232#0#MUSICA#` → `0#MUSICA`), para registrar o cru seja o que vier.
- Faixas que o MAPA não dá, assumidas: frequências do gerador e do sweep 10–22000 Hz; ganho do sweep −60 a 0 dB, como o do tom fixo; nome de memória de 1 a 12 caracteres ASCII visíveis, sem `#` nem `*` (como o nome do produto). Escritas só são codificadas; a trava recusa todas.
- Casamento: Marco 1 exige o código e o número exato de segmentos do escopo, com o canal ou a banda iguais; o eco do envio (`#081#0#`, `#080#`, `020#0`) não tem valor e vira órfão. A sessão entrega também uma resposta que chegou sem `\r\n` quando já está inteira (`020#3#-1.0`, `#080#+0.0#`).
- Valor ilegível (texto que não é número num tipo numérico) conta como falha da tentativa, com o cru guardado, e ganha a nova tentativa como uma consulta sem resposta.
- Revisão (20/10): 8 consultas do Marco 1 seguidas sem resposta (já com a nova tentativa) param a leitura como incompleta, "O aparelho parou de responder." (como o protótipo). Os extras não contam, porque o aparelho pode nunca responder a eles. O retrato guarda o firmware do início (a queda apaga o da sessão); um extra com várias partes espera o `\r\n`; `#231##` registra um nome vazio; booleano diferente de 0 e 1 fica "fora da faixa"; rótulos das bandas em "1,6 kHz".

## Spec Change Log

## Review Triage Log

| # | Achado | Veredito | Evidência | Rota |
|---|---|---|---|---|
| 1 | Resposta de extra com vários segmentos, partida entre notificações, entregue cedo (`RespostaInteira`) | medium | `#232#0#MUSICA HIGH#` + `
` passa de 20 bytes e pode chegar em duas notificações; um global aceita 1 segmento terminado em separador | patch |
| 2 | Extras juntam os segmentos (`0#120`, `0#MUSICA`) | low | O cru fica guardado na falha ou no item; o formato real ainda é desconhecido e a história 4 explica as diferenças | rejeitado |
| 3 | Exceção inesperada na leitura não gera retrato; aviso escondido | low | `EnviarAsync` já captura as falhas do transporte e o assinante do `Avancou` só agenda na thread da tela; não há caminho mostrado que lance | rejeitado |
| 4 | Aparelho mudo prende a leitura ~15 min | medium | Sem limite de silêncios seguidos (o protótipo parava em 8); 84 × 2 × 5 s | patch |
| 5 | Reconexão rápida pula a leitura automática | low | Reconectar leva segundos (scan + GATT); a fila da thread da tela esvazia em milissegundos | rejeitado |
| 6 | Salvar sobrescreve o último retrato bom; escrita não atômica | low | A decisão é guardar o último retrato; arquivo só de consulta | rejeitado |
| 7 | Linha congelada da matriz mudada sem confirmação | false | O Denis confirmou 100 Hz no chat em 20/10 antes da edição (registrado nas notas) | rejeitado |
| 8 | Faltam testes de cancelamento, Desconectar no meio, não enviado | low | Desconectar segue o mesmo caminho da queda (`Mudou` → `caiu`); o cancelamento não é alcançável pela tela | rejeitado |
| 9 | Camada do app (leitura automática, salvar) sem teste automático | medium | Não há projeto de teste para o MAUI; a conferência é no aparelho | defer |
| 10 | Teste de duas leituras depende de tempo | low | Passa hoje; risco só de fragilidade | rejeitado |
| 11 | Teste do tamanho do nome confuso | low | Cosmético | rejeitado |
| 12 | Booleano ≠0/1 sem marca de fora da faixa | low | `2` vira "mutado" sem marca; correção direta | patch |
| 13 | Rótulo "1,6k Hz" | low | Correção direta para "1,6 kHz" | patch |
| 14 | README: `run-as` só no build Debug | low | Uma frase | patch |
| 15 | `FimMarco1` só conferido como não nulo | medium | Tirar a atribuição no laço ainda passaria; é o número do critério de ~15 s | patch |
| 16 | Última consulta expira junto com a queda → `Completo=true` | low | Janela de instantes no fim de 5 s; improvável | rejeitado |
| 17 | Resposta casada depois do prazo some sem virar órfão | low | Janela mínima depois de um prazo vencido | rejeitado |
| 18 | Retrato incompleto perde o firmware depois da queda | low | `LimparConexao` zera o `Firmware` antes do `Parar`; correção direta | patch |
| 19 | Nome vazio `#231##` vira órfão + sem resposta | low | `Analisar` dá 0 segmentos; nome vazio é plausível | patch |
| 20 | Regex do nome aceita `
` final e nome em branco | low | `$` do .NET casa antes do `
`; correção direta | patch |
| 21 | `Onde` com índice inválido | false | Só é chamado por `Consulta` montada pelo próprio catálogo | rejeitado |
