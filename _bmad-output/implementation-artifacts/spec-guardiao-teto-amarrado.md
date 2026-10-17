---
title: 'Guardião: teto de demonstração amarrado ao corte e regra 6 por saída'
type: 'feature'
created: '2026-10-17'
status: 'draft'
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
- [ ] `ferramentas/guardiao.py` -- `teto_demo`, teto efetivo, regra 6 por saída, recusas e "Seguro:".
- [ ] `estado/guardiao.json` -- migração de formato.
- [ ] `ferramentas/painel.py` e `ferramentas/painel/index.html` -- regra 6 por saída e teto efetivo nos Bastidores.
- [ ] `ferramentas/tests/test_guardiao.py` (+ painel) -- todos os casos da matriz; os testes atuais continuam passando.

**Acceptance Criteria:**
- Given o arquivo migrado sem `teto_demo` e com a regra 6 `true` em todas as saídas, when a suíte roda, then todos os testes atuais passam sem mudar as expectativas.
- Given `teto_demo` −9,8 nas saídas 1 e 2 e a regra 6 `false` só nelas, when a KARINA aplica `071:0=-9.8 071:1=-9.8` com o corte LR24 em 150 Hz e as rotas A/B, then é aprovado; e `081:2` acima da linha de base continua recusado.

## Verification

**Commands:**
- `uv run python -m unittest discover -s ferramentas/tests` -- expected: tudo passa.
