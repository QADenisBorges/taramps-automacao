---
title: 'Vitrine BGS Sound Lab: abertura audiovisual, tela-título e Bastidores'
type: 'feature'
created: '2026-10-17'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-16/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-16/EXPERIENCE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-painel.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** O painel atual tem cara de protótipo técnico. Na inauguração de hoje (17/10), os convidados olham a TV e não ficam sabendo que foi o Denis quem fez, que existe uma IA (a KARINA) e que aquilo tem marca.

**Approach:** Reconstruir a página do painel seguindo o `DESIGN.md` e o `EXPERIENCE.md` aprovados (direção Keynote). O fluxo é:
1. "Toque para começar";
2. abertura de ~10 s em tela cheia, com efeitos sonoros sintetizados em 4 fases;
3. tela-título (a Vitrine) em tela cheia, com o anel de luz reagindo ao microfone;
4. Enter, que leva aos Bastidores em janela normal.

Nos Bastidores ficam todas as funções que já existem, com visual novo, mais a Jornada (feito × planejado) e o seletor de Modo. Os spines são o contrato; os mocks em `mockups/` são referência visual, e os spines vencem quando houver conflito.

## Boundaries & Constraints

**Always:**
- **Som só na abertura.** Tudo é sintetizado com Web Audio, sem arquivos nem música, com volume limitado (~−12 dBFS). Ao fim da abertura, ou ao pulá-la, o `AudioContext` de saída é fechado. Nenhum som sai depois disso, nem nos Bastidores, nem ao voltar à Vitrine.
- **Microfone só escuta**, nunca toca nada, e só via `getUserMedia` em localhost, como define o EXPERIENCE ("Reação ao som"). Microfone negado ou ausente → o anel respira calmo, sem fingir reação.
- **Funções preservadas:**
  - login único;
  - "Ler agora";
  - polling de 3 s;
  - memórias;
  - KARINA (antes "Agente") com a linha do tempo e o "escrevendo…";
  - as 10 telas em miniatura que expandem, com curvas SVG;
  - violações do guardião;
  - faixas de leitura antiga ou incompleta.
  
  Todos os testes atuais de `ferramentas/tests/` continuam passando.
- **Offline:** sem CDN e sem fontes externas.
- **Verdade:** a Jornada mostra só fatos reais (lista do EXPERIENCE, "Projeto em evolução"). O planejado aparece como planejado, e o selo "em evolução" está sempre visível.
- **Modo ativo:** `sessao.py modo "<Filme|Música SPL|Música EQ>"` e `POST /api/modo` gravam `estado/memorias.json` → `modo_ativo`. O `/api/estado` expõe esse campo, e sem registro a Vitrine omite o Modo. Atualizar a docstring do `painel.py` e o roteiro da `/regulagem`, para que ela registre o modo ao salvar ou carregar uma memória.
- `prefers-reduced-motion` e "pular abertura" seguem o EXPERIENCE.

**Never:**
- Escrever no processador pelo painel.
- Tocar áudio fora da abertura.
- Pedir permissão de microfone na frente dos convidados (só nos Bastidores, como define o EXPERIENCE).
- Inventar números ou marcos.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Abertura completa | clique em "Toque para começar" | tela cheia, 4 fases de som e luz, tela-título | tela cheia recusada → roda na janela |
| Pular | tecla depois de 1 s | vai à tela-título, o som para na hora e o contexto fecha | N/A |
| Depois da abertura | qualquer uso | nenhum som; o contexto de áudio de saída fica `closed` | N/A |
| Enter na tela-título | Vitrine em tela cheia | sai da tela cheia e mostra os Bastidores | N/A |
| Voltar à Vitrine | botão ou tecla B | Vitrine em tela cheia, sem repetir a abertura nem o som | N/A |
| Microfone | permissão concedida | o anel segue o grave da sala | negado ou ausente → respiração calma; nota só nos Bastidores |
| Modo | `modo_ativo` gravado | "Som ajustado pela KARINA · Modo: Filme" | sem registro → sem "Modo" |
| Seletor de Modo | escolha nos Bastidores | `POST /api/modo` grava, e a Vitrine atualiza no próximo polling | falha → aviso curto nos Bastidores |
| Sem senha | arquivo ausente | login único antes de tudo (comportamento atual) | igual hoje |

</frozen-after-approval>

## Code Map

- `ferramentas/painel/index.html` -- página atual (login, introdução de 4 s, grade, cartões, modal, curvas, polling). Será reescrita. Reaproveitar a lógica de dados e de curvas, não o visual. Pode virar `index.html` + `app.css` + `app.js` + `abertura.js` servidos de `ferramentas/painel/`.
- `ferramentas/painel.py` -- `/`, `/api/estado`, `POST /api/senha`, `POST /api/ler`, `montar_estado` (seções protegidas). Acrescentar a rota estática para os arquivos de `ferramentas/painel/` (sem sair da pasta), `POST /api/modo` e `modo_ativo` no estado.
- `ferramentas/sessao.py` -- `memoria`/`preset`, `carregar_memorias`/`gravar_memorias`. Acrescentar `modo`.
- `.claude/skills/regulagem/references/roteiro.md` -- nos passos de salvar e carregar memória, registrar o modo.
- `ferramentas/tests/test_painel.py`, `test_sessao.py` -- padrões de teste do servidor e do CLI.
- `mockups/vitrine-keynote.html` e `mockups/bastidores.html` (pasta do UX) -- referência visual. O anel, a tipografia e a paleta saem do DESIGN.md.

## Tasks & Acceptance

**Execution:**
- [ ] `ferramentas/painel/` -- nova página: toque para começar, abertura (animação + `abertura.js` com o som em 4 fases e o contexto fechado ao fim), Vitrine com a KARINA e o Modo, Bastidores com as regiões do EXPERIENCE, seletor de Modo, Jornada, as 10 telas e o modal.
- [ ] `ferramentas/painel.py` -- arquivos estáticos, `POST /api/modo`, `modo_ativo` e docstring.
- [ ] `ferramentas/sessao.py` -- `modo`.
- [ ] `.claude/skills/regulagem/references/roteiro.md` -- registrar o modo.
- [ ] `ferramentas/tests/` -- testes de `/api/modo`, `modo_ativo`, rota estática (sem sair da pasta) e `sessao.py modo`. Os testes da página continuam valendo (sem CDN, os 10 títulos).

**Acceptance Criteria:**
- Given o painel aberto, when o Denis clica em "Toque para começar", then a abertura roda em tela cheia com as 4 fases e termina na Vitrine.
- Given a abertura terminada ou pulada, when se inspeciona a página, then o contexto de áudio de saída está `closed` e nenhum elemento ou nó de áudio toca.
- Given a Vitrine, when se aperta Enter, then os Bastidores aparecem em janela normal, com todas as funções antigas operando ("Ler agora", expansão das telas, linha do tempo).
- Given a página inteira, when se procura texto técnico na Vitrine (Hz, dB, BLE, firmware), then não aparece nada.

## Verification

**Commands:**
- `uv run python -m unittest discover -s ferramentas/tests` -- expected: tudo passa.

**Manual checks (if no CLI):**
- No navegador a 1920×1080 e 1366×768: abertura, pular, Vitrine, Enter → Bastidores, voltar à Vitrine sem som, modal do Crossover, "Ler agora" e console sem erros. Conferir o estado `closed` do `AudioContext` depois da abertura.
