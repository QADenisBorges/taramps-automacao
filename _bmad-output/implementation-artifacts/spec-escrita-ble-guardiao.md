---
title: 'Escrita BLE no PRO 2.4BT atrás do guardião mínimo, com backup e releitura'
type: 'feature'
created: '2026-10-16'
status: 'done'
baseline_commit: 'bf30a0efb280537732107f3c2b8d1fec545c341d'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/protocolo/MAPA.md'
  - '{project-root}/_bmad-output/forge/prototipo-presets-ia/forged-idea.md'
  - '{project-root}/_bmad-output/specs/spec-taramps-automacao/damage-thresholds.md'
  - '{project-root}/_bmad-output/specs/spec-taramps-automacao/setup-home-theater-2.1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Amanhã (17/10), a partir das 12h, a IA precisa regular o processador, mas hoje o notebook só lê. Escrever sem trava pode queimar as JBL ou o sub, porque o módulo estará ligado.

**Approach:** Criar uma ferramenta que recebe um lote de ajustes, faz o backup (leitura completa), simula o estado resultante, e só escreve o que o **guardião mínimo** aprova. Cada escrita é seguida de releitura. Uma divergência ou uma queda interrompe o lote.

## Boundaries & Constraints

**Always:**
- **Guardião mínimo (forge, travado).** Ele valida o estado resultante das 4 saídas e cada estado intermediário do lote:
  1. JBL (saídas 1 e 2): passa-alta ligado e a curva somada (crossover + EQ de entrada, 15 bandas e EQ de saída) nunca acima da referência abaixo do f_min (`damage-thresholds.md`, travas 1 e 2).
  2. Sub (saídas 3 e 4): subsônico ligado e curva somada nunca acima da referência abaixo do f_min.
  3. Limiter nunca em OFF (`0.5`), e threshold nunca acima do teto de cada saída.
  4. Saídas 3 e 4 idênticas em todos os parâmetros. Pedido em uma delas é espelhado na outra, e as duas são escritas em sequência.
  5. Só códigos do catálogo do Marco 1, com valor dentro da faixa. Reset, memórias (240, 241, 242), gerador, senha, nome e remote out são sempre recusados.
  6. **Regra 6, ligada por padrão:** master + ganho + maior reforço de EQ de cada saída nunca acima da linha de base. Ela só desliga quando o Denis registrar que passou no teste dos +2 dB.
  7. Sempre G_ef ≤ −1 dB, somando +6 dB quando a rota for A+B.
- **Saída em violação no estado lido:** o lote só é aceito se resolver a violação dessa saída (princípio 6).
- **Limites do guardião (decidido pelo Denis, opção A):**
  - JBL (saídas 1 e 2): passa-alta de no mínimo 100 Hz a 24 dB/oit (ficha do fabricante).
  - Sub (saídas 3 e 4): subsônico de no mínimo 35 Hz a 24 dB/oit enquanto a Fb for desconhecida.
  - Tetos do limiter: −11,0 dB (0,56 V) nas JBL e −6,0 dB (1,00 V) no sub.
  - O estado atual (12 dB/oit nas JBL e 28 Hz no sub) conta como violação já existente.
- Os limites ficam em `estado/guardiao.json`. Só o Denis edita esse arquivo, e a ferramenta nunca o escreve.
- Backup completo antes da primeira escrita (retrato em `estado/backups/`). Releitura de cada parâmetro escrito.
- **Fail-safe:** releitura divergente, queda ou timeout param o lote. O resultado informa o que foi aplicado, o que não foi e o estado relido.
- `--simular`: valida contra uma leitura nova sem escrever nada.
- A senha vem de `TARAMPS_SENHA` ou de `getpass` e nunca é impressa nem gravada.
- Mensagens curtas, em português. Uma recusa diz qual regra barrou e qual valor seria seguro.

**Never:**
- Escrever sem aprovação do guardião. O caminho de escrita só existe por meio dele.
- Relaxar limites pela ferramenta ou pela IA.
- Salvar ou carregar memória (o Denis salva pelo app).
- Servidor MCP e skill da persona: ficam para a próxima spec.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Ajuste seguro | `081:0=-12.0` | Backup, escrita, releitura igual e "aplicado" | N/A |
| Passa-alta da JBL abaixo do mínimo ou em OFF | `042:0=0` | Recusado pela trava 1, com o valor seguro | Nada é escrito |
| Reforço de EQ abaixo do corte | `091:0=+6`, com a frequência `090:0` = 50 Hz | Recusado pela trava 1 (curva somada) | Nada é escrito |
| Limiter OFF ou acima do teto | `071:2=0.5` ou acima do teto | Recusado pela trava 3 | Nada é escrito |
| Ponte | `081:2=-12.0` | Escreve as saídas 3 e 4 | Se a releitura de uma divergir, o lote para |
| Subir nível com a regra 6 ligada | `080=+1.0` acima da linha de base | Recusado pela regra 6 | Nada é escrito |
| Ordem insegura | Mudar frequência e filtro juntos, com um intermediário inseguro | Escolhe uma ordem em que todo intermediário é seguro | Se não existir ordem segura, recusa o lote |
| Releitura divergente ou queda | Valor relido ≠ pedido | Para e lista o que foi aplicado e o que não foi | Código de saída ≠ 0 |
| Código proibido | `241:1` | Recusado (regra 5) | Nada vai ao BLE |

</frozen-after-approval>

## Code Map

- `ferramentas/protocolo_taramps.py`:
  - `CATALOGO` (faixas e casas decimais) e `verificar_envio` (trava só-leitura, com lista de permissão). Manter a trava de leitura intacta.
  - A escrita ganha uma montagem própria: `*CCC*c*v*`, `*CCC*v*`, e `020*b*v` sem `*` nas pontas (MAPA §4). Formato: 1 casa para dB, Q, cm e attack; inteiro para Hz, release, índice e bool; texto `a`/`b`/`a+b`; fase `0`/`180`.
- `ferramentas/cliente_ble.py`:
  - `ClienteTaramps.enviar` é o único caminho até o BLE e hoje só aceita leitura. Acrescentar um método de escrita que exige a aprovação emitida pelo guardião.
  - A característica 1122 é só `write` (com resposta), segundo o MAPA §9. Respostas ou acks depois da escrita vão para o log como órfãos.
- `ferramentas/ler_processador.py` -- reaproveitar o fluxo de scan, conexão, autenticação, `ler_tudo` e gravação do retrato. Não mudar o comportamento.
- `estado/leituras/2026-10-16_1747.json` -- estado atual real. Serve de fixture de teste: ponte com attack 50,0 × 50,1 (violação já existente) e subsônico em 28 Hz.
- MAPA §9 -- com o limiter em Auto, o app reescreve attack e release ao abrir a tela. O guardião sempre parte de uma leitura nova.

## Tasks & Acceptance

**Execution:**
- [x] `ferramentas/guardiao.py` -- carrega `estado/guardiao.json` e simula a aplicação de um lote ao retrato. Também:
  - curva somada: magnitude Butterworth/LR de ordem n mais biquads de pico; nas 15 bandas, Q ≈ 2,1;
  - regras 1 a 7;
  - procura de uma ordem segura;
  - devolve aprovação (com a ordem) ou recusas com motivo e valor seguro.
  
  Só stdlib.
- [x] `estado/guardiao.json` -- limites da decisão A (ver Boundaries), com comentário de origem e data. Linha de base: master, ganhos e EQs do retrato `2026-10-16_1747`. Flag `regra6: true`.
- [x] `ferramentas/protocolo_taramps.py` -- montagem e validação de escrita, separada da trava de leitura.
- [x] `ferramentas/cliente_ble.py` -- `escrever(aprovacao)`: escreve um item, espera a janela de ack e relê.
- [x] `ferramentas/ajustar_processador.py` -- CLI.
  - Entrada: `CCC:c=v` ou `CCC=v` (repetíveis), ou `--arquivo lote.json`.
  - Opção `--simular`.
  - Fluxo: leitura completa → backup → guardião → escrita em ordem → releitura → resumo curto.
  - Grava o resultado em `estado/ajustes/`.
- [x] `ferramentas/tests/test_guardiao.py` e `ferramentas/tests/test_ajustar_processador.py` -- todos os casos da matriz, com transporte falso e o retrato real como fixture.
- [x] `.gitignore` -- nada novo (backups e ajustes são versionados como histórico).

**Acceptance Criteria:**
- Given o processador ligado e um lote seguro, when o comando roda, then o backup é gravado antes da primeira escrita e cada item volta "igual" na releitura.
- Given qualquer lote, when o guardião recusa, then nenhum byte de escrita sai pelo BLE.
- Given o código do cliente, when se tenta escrever sem aprovação do guardião, then a chamada é recusada antes do BLE.
- Given a execução, when se procura a senha em `estado/` e no terminal, then ela não aparece.

## Spec Change Log

## Review Triage Log

| # | Achado | Veredito | Evidência / rota |
|---|---|---|---|
| 1 | Eco tardio da escrita (`*…*`) confirma "igual" na releitura (blind, edge) | high | Depois de trocar `*` por `#`, o eco tem os mesmos segmentos da resposta de leitura. Patch: na releitura, recusar quadro que começa com `*`. |
| 2 | `mesmo_valor` arredonda o inteiro relido (042 11,4 → igual) (edge) | medium | Verificado: `mesmo_valor('042', 11, 11.4)` dá True. Patch. |
| 3 | Q=0 e valores absurdos estouram a conta e saem como erro do BLE (edge) | medium | `ganho_pico` divide por q; a faixa não é checada antes de simular. Patch (regra 5 com faixa). |
| 4 | Retrato com `fora_da_faixa` aceito como estado (edge) | medium | `estado_do_retrato` não olha a marca. Patch. |
| 5 | A regra 6 ignora os +6 dB do A+B (blind) | medium | `toca('nivel')` ignora 010, e o nível não soma a rota. Patch. |
| 6 | Senha em pedido `000`/`001` sai no texto da recusa e no registro (edge, claim) | medium | `Recusa.texto` usa `pedido.original`. Patch. |
| 7 | Ponte desigual depois de falha no meio do par, com aviso genérico (blind) | medium | Aviso explícito e o comando de volta. Patch. |
| 8 | "Use o backup" sem ferramenta (blind) | medium | Patch: lote de volta no registro. |
| 9 | Ctrl+C perde o item em curso e os não aplicados (blind, edge) | low | Correção direta no `finally`. Patch. |
| 10 | Registro não mostra quais limites valeram (blind) | low | Patch: SHA-256 do `guardiao.json`. |
| 11 | O selo assina só o horário do retrato (blind) | low | Patch: hash das leituras. |
| 12 | Sugestões "Seguro:" fora do formato de entrada (blind) | low | A IA cola o texto amanhã. Patch. |
| 13 | 15 bandas sem teste na curva somada (gap) | medium | Confirmado por mutação. Patch (testes). |
| 14 | Escrita global e de banda sem teste ponta a ponta (blind) | medium | Patch (testes). |
| 15 | Firmware mascarado no ajustar sem teste (gap) | medium | Confirmado por mutação. Patch (teste). |
| 16 | Códigos só de leitura (005, 111, 150) aceitos na escrita (blind) | false | Verificado: eles não estão no `CATALOGO`, e `CODIGOS_ESCRITA` vem só do catálogo. Rejeitado. |
| 17 | 070 (limiter auto) fora da trava 3 (edge) | false | O Auto só recalcula attack e release (MAPA §9), não o threshold. Rejeitado. |
| 18 | LR de ordem ímpar aproximado (blind) | low | LR18, LR30 e LR42 não são usados no setup. Rejeitado; anotar se for usar. |
| 19 | Config não valida saídas 3 e 4 iguais nem o tipo de trava (blind) | low | Só o Denis edita; o arquivo atual está certo. Rejeitado. |
| 20 | Sem leitura completa salva depois das escritas (blind) | low | Cada execução começa por leitura completa e backup. Rejeitado. |
| 21 | Registro não salvo quando falta a senha (blind) | low | Nada é escrito nesse caso. Rejeitado. |
| 22 | `reg.gravar` falhando no `finally` (edge) | low | Raro, e adiciona ramos. Rejeitado. |
| 23 | Aprovação reutilizada em outro cliente (edge) | low | O selo não sobrevive ao processo. Rejeitado. |
| 24 | Saídas 3 e 5 do ajustar sem teste (gap) | maybe-false | Terminam antes de qualquer escrita. Defer. |
| 25 | Princípio 6 aplicado por regra, e não por saída inteira (relato do implementador) | low | O bloco congelado conflita com a matriz (`081:0=-12.0` aprovado com a saída 1 em violação). A leitura por regra atende as duas, e baixar o ganho não piora nenhuma trava. Mantido. |

## Design Notes

A referência das travas 1 e 2 é um Butterworth da inclinação mínima, no f_min. A curva somada da saída é checada em pontos log-espaçados de 10 Hz até o f_min, com tolerância de 0,5 dB. Exemplo: JBL com BT24 em 100 Hz e EQ de entrada a +6 dB em 63 Hz. Se a soma passar da referência BT24@100 abaixo de 100 Hz, o lote é recusado com "reforço abaixo do corte da JBL".

## Verification

**Commands:**
- `uv run python -m unittest discover -s ferramentas/tests` -- expected: tudo passa.
- `uv run ferramentas/ajustar_processador.py --simular 081:0=-12.0` -- expected: com o aparelho ligado, "aprovado" sem nenhuma escrita.
