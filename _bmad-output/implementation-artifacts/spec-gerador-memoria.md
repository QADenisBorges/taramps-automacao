---
title: 'Gerador de tom e salvar memória pela IA, atrás do guardião'
type: 'feature'
created: '2026-10-17'
status: 'in-progress'
baseline_commit: 'fc693bd2ff43615df5778693bbf632c8ca47202b'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/protocolo/MAPA.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-guardiao-teto-amarrado.md'
  - '{project-root}/estado/auditorias/2026-10-17_guardiao.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A regulagem de hoje (17/10, já às 18h50) está lenta porque o Denis faz à mão o que a IA poderia fazer: tocar os tons de teste pelo celular e salvar as memórias pelo app. Ele pediu que a IA faça isso agora, com o próprio guardião protegendo.

**Approach:** Liberar dois comandos hoje proibidos (regra 5), cada um atrás de travas próprias:
1. **Gerador de tom fixo** do processador (200 ganho, 201 frequência, 202 liga/desliga), em `ferramentas/tom.py`, com duração máxima e desligamento garantido.
2. **Salvar memória** (240 + nome 231/232/233), em `ferramentas/memoria.py`.

Carregar memória (241), sweep (210–213), reset, senha e remote out continuam proibidos.

## Boundaries & Constraints

**Always:**
- **O gerador só funciona com o caminho provado.** Em `estado/guardiao.json`, `"gerador": {"liberado": false, "caminho_provado": null, ...}`, e só o Denis edita. `liberado` vira `true` depois do **teste de caminho**, um modo do `tom.py` que roda com o módulo desligado do processador (RCA fora). Nele o Denis mede, no RCA de saída do processador:
  1. o tom ligado;
  2. o tom com a saída 1 mutada (deve cair a ~0);
  3. o tom com o passa-alta da saída 1 em 5 kHz (deve cair muito);
  4. o tom com o limiter da saída 1 em −30 dB (deve cair muito).
  
  As mudanças desses passos são só para baixo/proteção e são desfeitas no fim (estado original restaurado e relido). O `tom.py --caminho` grava o resultado em `estado/auditorias/<data>_caminho-gerador.json`. Sem `liberado: true` e sem esse arquivo, `tom.py` recusa tocar.
- **Antes de cada tom:** leitura nova completa, e o guardião **sem nenhuma violação** em travas 1–4 e regra 7 no estado lido. Com violação → recusa e diz qual.
- **Limites do tom:** só frequência fixa (10 Hz a 20 kHz), ganho de −60 a 0 dB, duração padrão 20 s e **máximo 30 s**. O desligamento (`*202*0*`) sai sempre no fim, no Ctrl+C, em exceção e na saída do processo (try/finally + atexit), com releitura confirmando `202 = 0`. Se a conexão cair com o tom ligado: tenta reconectar e desligar até 3 vezes; se não conseguir, imprime em destaque "DESLIGUE O MÓDULO AGORA: o gerador pode ter ficado ligado" e sai com código ≠ 0.
- **Salvar memória:** `memoria.py salvar <1|2|3> "NOME"`. O nome tem até 12 caracteres A–Z, 0–9, `_` e espaço (MAPA). Antes de salvar: leitura nova + guardião sem violação (não se salva estado perigoso). Sobrescrever um slot que `estado/memorias.json` não marca como vazio ou `por: ia` exige `--sobrescrever`. Depois de salvar, registra em `memorias.json` (via `sessao.py memoria`) com `por: ia` e o hash do retrato salvo.
- Todo envio desses códigos passa por uma permissão de escrita **separada** da trava de leitura e do catálogo de ajustes: um `verificar_comando_especial` que só aceita os textos exatos (`*200*v*`, `*201*v*`, `*202*0|1*`, `*240*n*`, `*23x*NOME*`) e só quando chamado com a aprovação do guardião correspondente.
- Log bruto, senha mascarada e registro em `estado/ajustes/` como nos ajustes (tipo `tom` ou `memoria`).
- **Formato real do nome não provado:** depois de nomear, `memoria.py` relê o nome (`231#` etc.). Se divergir, avisa e segue (o salvar em si vale).

**Never:**
- Sweep, carregar memória, reset, senha, remote out.
- Tom sem caminho provado, com violação, ou acima de 30 s.
- A IA editar `estado/guardiao.json` (a migração única de formato, que acrescenta `"gerador"` com `liberado: false`, é feita por esta implementação).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Tom liberado | caminho provado, guardião limpo, `tom.py 400 --db 0 --s 20` | liga, espera, desliga, relê `202=0` | N/A |
| Sem caminho provado | `liberado: false` | recusa antes de conectar | código ≠ 0 |
| Violação no estado | ex.: subsônico em 28 Hz | recusa e diz a trava | N/A |
| Duração > 30 s | `--s 60` | recusa | N/A |
| Ctrl+C no meio | tom tocando | desliga e relê | N/A |
| Queda com o tom ligado | conexão cai | 3 tentativas de desligar; falhou → aviso "DESLIGUE O MÓDULO" | código ≠ 0 |
| Teste de caminho | `tom.py --caminho` | 4 telas de medida, estado restaurado e relido, arquivo gravado | sai e restaura em qualquer falha |
| Salvar memória | `memoria.py salvar 2 "MUSICA ORIG"` | `*240*1*` + nome, registro em memorias.json | nome inválido → recusa |
| Sobrescrever do Denis | slot com `por: denis` | recusa sem `--sobrescrever` | N/A |

</frozen-after-approval>

## Code Map

- `ferramentas/guardiao.py` -- `PROIBIDOS` (l. ~68), `checar`, `carregar_config`. O gerador e a memória saem de PROIBIDOS só pelo caminho novo; `ajustar_processador.py` continua recusando 200–202, 240 e 23x.
- `ferramentas/protocolo_taramps.py` -- `verificar_envio` (leitura), `verificar_escrita`/`CODIGOS_ESCRITA` (catálogo). Acrescentar `verificar_comando_especial`. Formatos em `protocolo/apk/.../ble/Protocol.java` l. 232–301: `*200*<dB 1 casa>*`, `*201*<Hz>*`, `*202*<0|1>*`, `*240*<0..2>*`, `*231*NOME*`/`*232*`/`*233*`.
- `ferramentas/cliente_ble.py` -- `escrever(aprovacao, indice)`. Acrescentar o envio especial, que exige a aprovação nova, e o desligamento garantido.
- `ferramentas/ler_processador.py` -- `procurar_e_conectar`, `obter_senha`, retrato.
- `ferramentas/ajustar_processador.py` -- padrão de CLI, registro e saída curta para copiar.
- `ferramentas/sessao.py` -- `gravar_memorias`/`memoria` para o registro.
- `estado/guardiao.json` -- migração: `"gerador": {"liberado": false, "caminho_provado": null, "_comentario": "..."}`.

## Tasks & Acceptance

**Execution:**
- [ ] `ferramentas/protocolo_taramps.py` -- `verificar_comando_especial`.
- [ ] `ferramentas/guardiao.py` -- `avaliar_tom` e `avaliar_salvar_memoria` (aprovações próprias, reaproveitando `checar`), mais a config do gerador.
- [ ] `ferramentas/cliente_ble.py` -- envio especial e desligamento garantido.
- [ ] `ferramentas/tom.py` -- tocar e `--caminho`.
- [ ] `ferramentas/memoria.py` -- `salvar`.
- [ ] `estado/guardiao.json` -- migração do bloco `gerador`.
- [ ] `ferramentas/tests/test_tom.py` e `test_memoria.py` -- todos os casos da matriz, com transporte falso que guarda o estado do gerador.

**Acceptance Criteria:**
- Given `liberado: false`, when qualquer `tom.py` roda (exceto `--caminho`), then nada é enviado.
- Given um tom tocando, when ocorre qualquer saída do processo, then `*202*0*` foi enviado e a releitura confirma 0, ou o aviso de desligar o módulo foi impresso.
- Given o `ajustar_processador.py`, when se pede `202:…` ou `240:…`, then continua recusado pela regra 5.

## Verification

**Commands:**
- `uv run python -m unittest discover -s ferramentas/tests` -- expected: tudo passa.
