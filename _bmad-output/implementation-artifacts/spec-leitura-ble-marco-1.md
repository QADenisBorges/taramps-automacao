---
title: 'Leitura completa do PRO 2.4BT por BLE a partir do notebook (Marco 1)'
type: 'feature'
created: '2026-10-16'
status: 'done'
baseline_commit: '86f619c5489a158c4506b76d83113cc8ef6e173c'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/protocolo/MAPA.md'
  - '{project-root}/_bmad-output/forge/prototipo-presets-ia/forged-idea.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Nada no projeto conversa com o processador. O protótipo de sábado (regulagem a partir das 12h, em 17/10) precisa que o notebook leia o estado real do PRO 2.4BT + AUDIO, e o mapa BLE v1 só foi lido no APK, nunca provado no aparelho.

**Approach:** Criar um cliente BLE em Python (bleak, Windows) que conecta, autentica com a senha BT e lê, um a um, todos os parâmetros do Marco 1 do `MAPA.md` §4. O resultado é um retrato em JSON, e cada quadro enviado e recebido fica registrado num log bruto, para corrigir o mapa rápido se o aparelho responder diferente do APK.

## Boundaries & Constraints

**Always:**
- Só leitura. O cliente recusa no código qualquer envio que não seja consulta `#…#`, `020#b` ou a autenticação `*000*SENHA*`.
- A senha vem da variável `TARAMPS_SENHA` ou é pedida sem eco (`getpass`). Ela nunca é gravada em arquivo, log ou retrato: aparece como `*000*****`.
- Uma consulta pendente por vez, com ~30 ms entre envios e timeout de 5 s por consulta. Consulta sem resposta vira `null` com o motivo, e a leitura continua.
- O log bruto guarda o hex e o ASCII de cada quadro, com horário e direção.
- O processador fica desligado do módulo amplificador.
- As mensagens para o Denis são curtas, um passo por vez.
- **Conferência com o app (decidido): amostra guiada em 2 memórias.**
  - ~34 valores por memória (decidido pelo Denis no checkpoint de revisão), cobrindo cada tipo de parâmetro pelo menos uma vez.
  - HPF/LPF (frequência e filtro), limiter (threshold e auto) e ganho da saída entram nos 4 canais.
  - Um valor por tela.
  - O CLI gera essa lista a partir do retrato (`--conferir`).

**Never:**
- Escrever qualquer parâmetro, salvar ou carregar memória, ou ligar o gerador.
- Fazer pareamento ou bond, ou mexer em configurações do Windows.
- Servidor MCP, guardião e escrita: ficam para a próxima spec.
- Ler a senha do app ou de arquivo de preset.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Leitura feliz | Processador ligado, celular desconectado, senha certa | Retrato JSON com 84 leituras (master, 16 parâmetros × 4 canais, EQ de entrada, 15 bandas, preset de EQ) e firmware da resposta `000` | N/A |
| Senha errada | Resposta `000` com `ERRO` | Para antes de qualquer leitura, com "Senha recusada" | Sai com código ≠ 0 e desconecta |
| Não acha o aparelho | Nenhum anúncio com UUID `0x2055` em 10 s | "Processador não encontrado. O celular ainda está conectado a ele?" | Sai com código ≠ 0 |
| Resposta em formato inesperado | Separador `*`, sem canal ou fragmentada | O parser aceita `#` ou `*`, junta fragmentos e casa pelo código e pelo canal | Quadro não casado vai para o log como "órfão" |
| Consulta sem resposta | Timeout de 5 s | Valor `null` com `motivo: timeout` | Continua as demais. O resumo final lista as falhas. |
| Queda de conexão | Desconecta no meio | Grava o retrato parcial, marcado como `incompleto` | Sai com código ≠ 0 |

</frozen-after-approval>

## Code Map

- `protocolo/MAPA.md` -- fonte dos UUIDs (§1), do formato (§2), da autenticação (§3), dos códigos, faixas e unidades (§4) e das lacunas (§8). Atualizar §8 e as etiquetas [Captura] com o que a leitura real provar.
- `ferramentas/btsnoop_att.py` -- referência de estilo (stdlib, docstring de uso no topo, mensagens em português) e da máscara da senha (`AUTH_PREFIXES`, l. 439). Não alterar.
- `ferramentas/tests/test_btsnoop_att.py` -- padrão de teste: `unittest`, dados sintéticos e `sys.path` apontando para `ferramentas/`.
- `.gitignore` -- já exclui capturas e presets. Acrescentar `estado/log-ble/`.

## Tasks & Acceptance

**Execution:**
- [x] `pyproject.toml` -- criar um projeto uv mínimo (Python ≥3.12, dependência `bleak`) -- para rodar com `uv run` sem instalar nada global.
- [x] `ferramentas/protocolo_taramps.py` -- catálogo dos parâmetros do Marco 1 (código, com ou sem canal, tipo, unidade, faixa); montagem das consultas; parser de respostas (`#`/`*`, fragmentos, casamento por código e canal); trava que só deixa passar consultas e autenticação. Tudo em stdlib, testável sem aparelho.
- [x] `ferramentas/cliente_ble.py` -- scan por `0x2055` ou serviço `…e58f`, conexão (3 tentativas), notificação/indicação na `…1123`, escrita na `…1122` com o tipo permitido pela característica, autenticação, consulta com espera de resposta e log bruto.
- [x] `ferramentas/ler_processador.py` -- CLI. `uv run ferramentas/ler_processador.py` lê tudo e grava `estado/leituras/AAAA-MM-DD_HHMM.json` e o log em `estado/log-ble/`. `--bruto "#081#0#"` manda uma única consulta e mostra a resposta crua, para depurar. Imprime um resumo curto (lidos, falhas, firmware). `--conferir ARQUIVO` mostra a amostra de conferência, um valor por vez, esperando Enter (o Denis digita `x` quando diverge), e grava o resultado ao lado do retrato.
- [x] `ferramentas/tests/test_protocolo_taramps.py` -- casos da matriz: parser com `#` e `*`, fragmentos, órfão, valor fora da faixa sinalizado, recusa de envio de escrita, máscara da senha no log, amostra de conferência cobrindo todos os tipos e os 4 canais nos críticos.
- [x] `.gitignore` -- acrescentar `estado/log-ble/`.

**Acceptance Criteria:**
- Given o processador ligado e o celular desconectado, when o Denis roda o comando de leitura, then em menos de 1 minuto sai o retrato com o firmware e todas as leituras, ou a lista exata das que falharam.
- Given qualquer execução, when se procura a senha em `estado/` e na saída do terminal, then ela não aparece.
- Given o retrato pronto, when ele é conferido com o app pelo método decidido, then os valores batem, ou cada divergência vira correção no parser ou no `MAPA.md` com o quadro do log citado.
- Given o código do cliente, when se tenta enviar `*081*0*0.0*`, then o envio é recusado antes de chegar ao BLE.

## Spec Change Log

## Review Triage Log

| # | Achado | Veredito | Evidência / rota |
|---|---|---|---|
| 1 | Tentativa de conexão falha → `caiu=True` permanente (blind, edge) | high | `disconnected_callback` dispara no `disconnect()` do `except`; `_cair` não distingue tentativa. Patch. |
| 2 | Callback tardio escreve em log fechado (edge) | low | Mesmo root do 1; corrigido junto. Patch. |
| 3 | Trava por lista de bloqueio deixa passar códigos não mapeados e canal fora da faixa (blind) | medium | `_CONSULTA_RE` aceita qualquer `\d{3}` e canal de 1–2 dígitos. Patch: lista de permissão. |
| 4 | Máscara da senha falha com eco fragmentado, segundo `*001*` ou firmware = senha (blind, edge, gap) | medium | `_mascara` só olha o prefixo do fragmento; `firmware_de` não é checado. Patch. |
| 5 | Auth vazia ou "ER" truncado vira sucesso (blind, edge) | low | `casar` com `final=True` aceita qualquer carga ≠ ERRO. Correção direta. Patch. |
| 6 | `escrever` sem timeout pode travar (edge) | medium | `enviar` aguarda `write_gatt_char` sem `wait_for`. Patch. |
| 7 | `_carimbo` ignora `-bruto.log` (blind, edge) | low | Correção direta. Patch. |
| 8 | `_conferir` conta qualquer resposta ≠ x como igual (blind) | medium | `else: confere=True`. Patch. |
| 9 | Docstring de códigos de saída e textos fixos (blind, edge) | low | Correção direta de texto. Patch. |
| 10 | Faltam testes: `*001*`, eco no `resposta_000`, tipo de escrita, saída 5, colisão de carimbo (gap) | medium | Confirmado pelo revisor (mutação passa nos 92). Patch. |
| 11 | Quadro sem separador final se funde com quadro espontâneo seguinte (020 + 150/111) (blind, edge) | maybe-false | Depende do formato real; a primeira leitura real decide. Defer. |
| 12 | Fragmento atrasado >0,3 s aceito truncado no ocioso (edge) | maybe-false | Depende do intervalo real entre notificações; a conferência pega. Defer. |
| 13 | Resposta sem canal igual ao eco é descartada (edge) | maybe-false | Aparece como timeout, não como valor errado; a leitura real decide. Defer. |
| 14 | Ctrl+C perde leituras / mensagem enganosa no prompt (blind, edge) | low | Raro no uso; correção adiciona ramos. Rejeitado. |
| 15 | `_conferir` sobrescreve conferência, não avisa incompleto, quebra com JSON estranho (blind, edge) | low | Cada memória tem seu retrato; arquivo estranho é uso fora do fluxo. Rejeitado. |
| 16 | `write_text` com OSError perde o retrato (edge) | low | Raro; adiciona ramo. Rejeitado. |
| 17 | 020 duplicado na amostra se todos os dB falharem (edge) | low | Só com o ganho falhando nos 4 canais. Rejeitado. |
| 18 | Timeouts espalhados passam de 1 min (edge, claim) | low | Só com aparelho instável; o resumo lista as falhas. Rejeitado. |
| 19 | Amostra põe cada crítico em 1 canal em rodízio, e o bloco congelado diz "entram nos 4 canais" (gap) | medium | Desvio do texto congelado. O Denis escolheu os 4 canais (~34 itens); amostra e teste corrigidos. Patch. |

## Design Notes

O retrato deve servir de entrada para o guardião e de backup na próxima spec. Por isso cada leitura guarda código, canal, valor convertido (número, índice ou texto), unidade e resposta crua:

```json
{"codigo": "071", "canal": 0, "nome": "limiter_threshold", "valor": -12.5, "unidade": "dB", "cru": "#071#0#-12.5#"}
```

## Verification

**Commands:**
- `uv run python -m unittest discover -s ferramentas/tests` -- expected: todos os testes passam, os antigos e os novos.
- `uv run ferramentas/ler_processador.py` -- expected: com o aparelho ligado, retrato gravado e resumo sem falhas.

**Manual checks (if no CLI):**
- Conferência guiada com o app, um valor por tela.
