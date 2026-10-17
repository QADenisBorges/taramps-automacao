---
title: 'Guardião: teto de demonstração amarrado ao corte e regra 6 por saída'
type: 'feature'
created: '2026-10-17'
status: 'done'
baseline_commit: 'd3ab3750e8bf82aa5c9cc1d38aba9bc1f7dee82b'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/estado/auditorias/2026-10-17_guardiao.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-escrita-ble-guardiao.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A auditoria independente de 17/10 (`estado/auditorias/2026-10-17_guardiao.md`) achou dois furos que permitem dano hoje, quando a MUSICA HIGH sobe o teto das JBL:
1. O teto do limiter das JBL é um número solto. Ele pode subir sem exigir o corte de 150 Hz a 24 dB/oit, e com a rota A+B (+6 dB antes do limiter) o cone passa muito do Xmax.
2. A regra 6 é global: desligá-la depois do teste na saída 1 libera também o sub, cujo teto nunca foi medido.

**Approach:** No `estado/guardiao.json`, cada saída de JBL ganha um **teto de demonstração opcional**, separado do teto base. O teto efetivo da saída é o de demonstração só enquanto o estado da saída cumprir as condições amarradas (passa-alta ≥ 150 Hz, inclinação ≥ 24 dB/oit, rota ≠ A+B). Fora delas, vale o teto base, e um limiter acima do base vira violação da trava 3. A regra 6 passa a ser por saída.

## Boundaries & Constraints

**Always:**
- Novo formato por saída (opcional): `"teto_demo": {"teto_limiter_db": <-9.8|-8.0|…>, "exige_passa_alta_hz": 150, "exige_inclinacao_db_oit": 24, "proibe_rota_ab": true}`. Sem `teto_demo` (ou com `null`), o comportamento é idêntico ao de hoje. Ele vale só para saídas de trava `passa-alta`; numa saída de subsônico é `ConfigInvalida`.
- **Teto efetivo** de uma saída num estado (simulado ou lido) = `teto_demo.teto_limiter_db` se, **nesse estado**, valem todas: índice do filtro HPF ≠ OFF; frequência do HPF ≥ `exige_passa_alta_hz`; inclinação do filtro (6·N de BTn ou LRn) ≥ `exige_inclinacao_db_oit`; e, se `proibe_rota_ab`, rota ≠ `a+b`. Senão, `teto_limiter_db` base.
- A trava 3 usa o teto efetivo em todo estado intermediário e final. Por isso, baixar o corte para menos de 150 Hz, ou pôr a rota em A+B, com o limiter acima do teto base é **recusado**, a menos que o mesmo lote baixe o limiter antes (a busca de ordem segura já existente cuida da ordem).
- Recusa clara: "limiter da saída 1 em −9,8 dB só vale com passa-alta ≥ 150 Hz/24 dB/oit e rota sem A+B (teto de demonstração); fora disso o teto é −11,0 dB. Seguro: 071:0=-11.0".
- **Regra 6 por saída:** cada saída aceita `"regra6": true|false`. O `regra6` global continua aceito como padrão para as saídas sem o campo (compatível com o arquivo atual). A checagem `nivel:N` só existe para saídas com a regra 6 ligada.
- O `estado/guardiao.json` real é atualizado **pela implementação só no formato**: mantém todos os valores atuais, coloca `"regra6": true` em cada saída e `"teto_demo": null` nas saídas 1 e 2, e acrescenta um `_comentario` explicando os campos. Quem preenche o `teto_demo` e desliga a regra 6 é o Denis, no roteiro.
- O painel (`/api/estado`) mostra a regra 6 por saída nos Bastidores (chip "Regra 6: 1 e 2 desligadas"), sem quebrar.

**Never:**
- Relaxar qualquer limite existente, ou permitir o teto de demonstração em saída de subsônico.
- A ferramenta escrever no `guardiao.json` depois desta migração única de formato.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Teto demo com corte certo | `teto_demo` −9,8; saída 1 com LR24 150 Hz, rota A | `071:0=-9.8` aprovado | N/A |
| Teto demo com corte baixo | `teto_demo` −9,8; saída 1 com BT24 120 Hz | `071:0=-9.8` recusado (trava 3) com a explicação | Seguro: −11,0 |
| Teto demo com A+B | LR24 150 Hz, rota A+B | `071:0=-9.8` recusado | Seguro: `010:0=a` ou −11,0 |
| Baixar o corte com o limiter alto | limiter −9,8 aplicado; pedido `040:0=120` | recusado, porque deixa o limiter acima do teto efetivo | o mesmo lote com `071:0=-11.0` passa (ordem segura) |
| Sem `teto_demo` | arquivo atual | comportamento idêntico ao de hoje | N/A |
| `teto_demo` no sub | saída 3 com `teto_demo` | `ConfigInvalida` ao carregar | mensagem clara |
| Regra 6 por saída | saídas 1 e 2 `false`, sub `true` | `081:0=-9.0` aprovado; `081:2=-9.0` recusado (regra 6) | N/A |

</frozen-after-approval>

## Code Map

- `ferramentas/guardiao.py`:
  - `Saida` e `carregar_config` (l. ~104–208): campos e validação;
  - `checar` (l. ~374–436): trava 3 em `limiter:N` e regra 6 em `nivel:N`;
  - `toca` (l. ~439): o limiter passa a ser tocado também por 040/042/010 da saída quando há `teto_demo`;
  - sugestões "Seguro:" (l. ~872).
- `estado/guardiao.json` -- migração única de formato, sem mudar nenhum valor.
- `ferramentas/painel.py` -- seção do guardião em `montar_estado`: expor a regra 6 por saída e o teto efetivo.
- `ferramentas/painel/index.html` -- chip da regra 6 nos Bastidores.
- `ferramentas/tests/test_guardiao.py` -- padrão `variante(CORRIGIDO)` e `SEM_REGRA6`. Acrescentar uma classe para os casos da matriz.

## Tasks & Acceptance

**Execution:**
- [x] `ferramentas/guardiao.py` -- `teto_demo`, teto efetivo, regra 6 por saída, recusas e "Seguro:".
- [x] `estado/guardiao.json` -- migração de formato.
- [x] `ferramentas/painel.py` e `ferramentas/painel/index.html` -- regra 6 por saída e teto efetivo nos Bastidores.
- [x] `ferramentas/tests/test_guardiao.py` (+ painel) -- todos os casos da matriz; os testes atuais continuam passando.

**Acceptance Criteria:**
- Given o arquivo migrado sem `teto_demo` e com a regra 6 `true` em todas as saídas, when a suíte roda, then todos os testes atuais passam sem mudar as expectativas.
- Given `teto_demo` −9,8 nas saídas 1 e 2 e a regra 6 `false` só nelas, when a KARINA aplica `071:0=-9.8 071:1=-9.8` com o corte LR24 em 150 Hz e as rotas A/B, then é aprovado; e `081:2` acima da linha de base continua recusado.

## Implementation Notes

- `guardiao.py`: `teto_demo` com limites no carregamento (base < teto ≤ −8,0 dB; passa-alta ≥ 150 Hz e ≥ f_min; inclinação ≥ 24; `proibe_rota_ab` obrigatório). `teto_efetivo`/`demo_valendo` valem em todo estado, e `toca` amarra 040/042/010 ao limiter. Regra 6 por saída, com as saídas 3 e 4 obrigatoriamente iguais.
- `estado/guardiao.json`: migração de formato (regra 6 por saída, `teto_demo: null` nas JBL, global removido); nenhum valor mudou.
- Painel: chip da regra 6 por saída e linha "Teto do limiter agora".
- Testes: matriz completa, limites do config, ramo do princípio 6 com amarra (confirmado por mutação) e consistência do M de cada etapa com o roteiro.
- O roteiro da /regulagem (etapa 3, M = 19) e o `riscos_aceitos.json` foram atualizados na mesma rodada, fora do Code Map, pelas contramedidas da auditoria de 17/10.

## Review Triage Log

| # | Achado | Veredito | Evidência / rota |
|---|---|---|---|
| 1 | O config aceita teto demo de 0 dB, demo ≤ base, amarra abaixo de 150 Hz ou 24 dB/oit e `proibe_rota_ab=false` (blind, edge, claim) | high | O loader só checa a faixa −48..0. Uma edição desfaz a amarração exigida pela auditoria. Patch: limites no loader (máx −8,0 dB). |
| 2 | Saídas 3 e 4 podem divergir na regra 6 (blind) | medium | A ponte espelha a escrita; um lado passa e o outro não. Patch. |
| 3 | `checar` reimplementa `teto_efetivo` (blind) | low | O painel e o guardião podem divergir. Patch. |
| 4 | Motivo enganoso quando o limiter está abaixo do base (blind) | low | Correção direta. Patch. |
| 5 | `regra6` global morto, uma armadilha (blind, edge, deletion) | medium | Mudar o global não faz nada. Patch: remover do arquivo. |
| 6 | Painel diz "agora" sem leitura (blind, edge) | low | Correção direta. Patch. |
| 7 | Teste concorrente instável no Windows (gap, other) | medium | PermissionError na leitura. Patch: retry. |
| 8 | Princípio 6 com amarra sem teste do ramo de `toca` (gap) | high | Confirmado por mutação: o `cfg` em `problemas` pode sumir sem falha. Patch (teste). |
| 9 | `PASSOS_POR_ETAPA[3]` sem amarra com o roteiro (blind, gap) | medium | Retomada pularia os passos 15–19. Patch (teste de consistência). |
| 10 | Registro de ajuste com `regra6` por saída sem teste (blind, gap) | low | Trilha de auditoria. Patch (uma linha). |
| 11 | Reparo parcial (só 042) recusado com o limiter acima do base (blind, edge) | false | É intencional e seguro: o lote precisa baixar o limiter junto, e a ordem segura resolve. Coberto pelo teste novo (8). |
| 12 | Dois formatos de número nas mensagens da trava 3 (blind) | low | Cosmético, e os testes fixam os textos. Rejeitado. |
| 13 | Falta campo de prova (`_origem`) para `teto_demo` e `regra6` (blind) | low | O roteiro registra as medidas e o diário. Rejeitado. |
| 14 | `FORMATO` não subiu com M 14→19 (blind) | low | Nenhuma sessão está na etapa 3. Rejeitado. |
| 15 | Chip e `notaTetos` sem teste de JS (gap) | maybe-false | Sem harness de JS no repositório. Defer (entrada já existente). |
| 16 | `_caminho_seguro` com a frase do teto demo sem teste (gap) | low | Ramo raro; o texto principal está fixado. Rejeitado. |

## Verification

**Commands:**
- `uv run python -m unittest discover -s ferramentas/tests` -- expected: tudo passa.
