---
title: 'Painel do protótipo: introdução, login único e tudo numa tela'
type: 'feature'
created: '2026-10-16'
status: 'done'
baseline_commit: '903e5432af70294d030bc193a322993978bc0feb'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/forge/prototipo-presets-ia/forged-idea.md'
  - '{project-root}/protocolo/MAPA.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Hoje só dá para ver o que a IA fez no processador abrindo o app no celular ou lendo JSON. No sábado o Denis quer mostrar, além do som, a automação que criou: abrir um painel com o nome dele, ver numa tela só o estado do processador, as memórias, o agente atuando e cada tela do app em miniatura.

**Approach:** Um painel local no navegador, servido por um script Python só com stdlib (`ferramentas/painel.py`), que lê a pasta `estado/` e desenha tudo a partir da última leitura BLE. Login único na primeira abertura grava a senha BT no arquivo fora do projeto. Atualiza sozinho a cada poucos segundos. É protótipo: um arquivo HTML, sem framework, sem build.

## Boundaries & Constraints

**Always:**
- **Abertura:** `uv run ferramentas/painel.py` sobe em `http://localhost:8765` e abre o navegador. Um atalho `painel.cmd` na raiz faz o mesmo com dois cliques.
- **Login único:** se `~/.taramps/senha` não existe, a primeira tela é o login (nome fixo "Denis", campo de senha de 4 caracteres, sem eco). O POST grava o arquivo com o mesmo formato que as ferramentas leem e nunca devolve a senha. Com o arquivo presente, o login não aparece mais.
- **Introdução, toda abertura:** tela cheia escura com "Denis Borges", "Home Theater 2.1", "regulado por IA com guardião", o nome do processador e o firmware lido. Entra no painel com um clique ou depois de 4 s.
- **Tela única** (desktop, grade; funciona a 1366 px):
  - cabeçalho: última leitura (hora, "há N min"), firmware, violações do guardião agora (contagem e lista curta), regra 6 ligada/desligada;
  - **Memórias:** as 3 do DSP e os presets do celular, com nome, origem (IA ou Denis) e quando foram salvos, lidos de `estado/memorias.json`;
  - **Agente:** o passo atual da regulagem (`estado/sessoes/progresso.json`), a linha do tempo dos ajustes (`estado/ajustes/*.json`: hora, lote, APROVADO/RECUSADO, aplicados, motivo da recusa), e "lendo…/escrevendo…" enquanto um ajuste estiver `em_andamento`;
  - **Telas do app em miniatura:** um cartão por tela (Home, Roteamento, Alinhamento, Fase, EQ Param Entrada, Crossover, Limiter, Nível das Saídas, EQ Param Saída, EQ 15 Bandas), desenhado a partir da última leitura no estilo do app (fundo preto, vermelho e laranja). Clique expande para a tela inteira com todos os valores; Crossover e EQs mostram a curva em SVG; limiter mostra dB e volts.
- **Dados ao vivo:** o navegador busca `/api/estado` a cada 3 s; o servidor monta a resposta lendo os arquivos na hora. Nenhuma conexão BLE pelo painel: o botão **"Ler agora"** roda `ler_processador.py` em subprocesso (só leitura) e fica desabilitado enquanto roda.
- `estado/memorias.json` é mantido pela skill `/regulagem` por `sessao.py memoria N "NOME" --por ia|denis` (3 slots do DSP) e `sessao.py preset "NOME" --por ia|denis` (celular). O painel só lê.
- Só stdlib no servidor; HTML, CSS e JS inline, sem CDN, para funcionar sem internet no sábado.
- A senha nunca aparece em log, resposta HTTP, HTML ou console.

**Never:**
- Escrever no processador pelo painel, ou expor `ajustar_processador.py` por HTTP.
- Fotos ou prints do celular (sem adb). As telas são desenhadas pelo painel.
- Autenticação de rede, HTTPS ou acesso de outra máquina: só `localhost`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Primeira abertura | sem `~/.taramps/senha` | tela de login → grava o arquivo → introdução → painel | senha com tamanho ≠ 4 volta com mensagem, sem gravar |
| Abertura normal | arquivo presente, leituras em `estado/` | introdução → painel com a última leitura | N/A |
| Sem leitura ainda | `estado/leituras/` vazio | painel com cartões "sem leitura" e o botão "Ler agora" em destaque | N/A |
| Ajuste em curso | um `estado/ajustes/*.json` com `em_andamento` | Agente mostra "escrevendo 081:0=-12.0…" | some quando o arquivo fecha |
| Ler agora | clique com o processador ligado | botão desabilitado, "lendo… 13 s", depois a leitura nova aparece | código ≠ 0 → mensagem curta com o motivo (celular conectado? senha?) |
| Leitura incompleta ou antiga | retrato `incompleto` ou com mais de 1 h | faixa amarela "leitura incompleta/antiga" no cabeçalho | N/A |
| Arquivo corrompido | JSON inválido em `estado/` | o painel ignora esse arquivo e segue | nunca derruba o servidor |

</frozen-after-approval>

## Code Map

- `ferramentas/ler_processador.py` -- `arquivo_senha()` (caminho do arquivo), `obter_senha`; reaproveitar o caminho e o formato (4 chars, sem BOM nem quebra). O "Ler agora" chama o CLI em subprocesso com `uv run`.
- `ferramentas/guardiao.py` -- `carregar_config`, `estado_do_retrato`, `checar` para listar as violações do último retrato no cabeçalho.
- `ferramentas/protocolo_taramps.py` -- `CATALOGO` (tela de cada parâmetro, rótulos de filtro e preset), `BANDAS_HZ`, `formatar_valor`; `guardiao.ganho_filtro`/`ganho_pico` para as curvas.
- `ferramentas/sessao.py` -- acrescentar `memoria` e `preset` gravando `estado/memorias.json`; mesmo padrão de gravação atômica.
- `estado/leituras/*.json` -- retrato (`leituras[]` com código, canal, valor, rótulo, cru). `estado/ajustes/*.json` -- registro do ajuste (`lote`, `decisao`, `aplicados`, `em_andamento`, `status`). `estado/sessoes/progresso.json` -- etapa/passo.
- `.claude/skills/regulagem/references/roteiro.md` -- nos passos "salvar pelo app" (etapas 5 e 6), acrescentar o comando `sessao.py memoria|preset`.

## Tasks & Acceptance

**Execution:**
- [x] `ferramentas/painel.py` -- servidor stdlib: `/` (HTML), `/api/estado`, `POST /api/senha`, `POST /api/ler`; monta o estado a partir de `estado/`; abre o navegador.
- [x] `ferramentas/painel/index.html` -- introdução, login, grade única, cartões por tela com expansão, curvas SVG, polling de 3 s. Estilo do app Taramps (preto, vermelho, laranja), nome do Denis em destaque.
- [x] `ferramentas/sessao.py` -- subcomandos `memoria` e `preset` → `estado/memorias.json`.
- [x] `.claude/skills/regulagem/references/roteiro.md` -- registrar memória/preset nos passos de salvar.
- [x] `painel.cmd` -- atalho na raiz.
- [x] `ferramentas/tests/test_painel.py` -- montagem do estado com os retratos reais, ajuste em andamento, arquivo corrompido ignorado, senha gravada no formato certo e nunca devolvida, leitura antiga marcada.

**Acceptance Criteria:**
- Given `painel.cmd`, when o Denis dá dois cliques, then o navegador abre na introdução com o nome dele e, após o clique, a tela única com a última leitura.
- Given a última leitura de 16/10, when o painel abre, then os 10 cartões mostram os valores que o app mostra (crossover 150/120/28 Hz, limiter 0,56 V e 1,00 V, saída 2 mutada), e o cabeçalho lista as violações do guardião.
- Given um ajuste rodando pela IA, when o painel atualiza, then o cartão Agente mostra o lote em curso e, ao terminar, o resultado.
- Given qualquer requisição, when se procura a senha nas respostas e no HTML, then ela não aparece.

## Implementation Notes

- Novos: `ferramentas/painel.py` (servidor stdlib em 127.0.0.1:8765, `/api/estado`, `POST /api/senha`, `POST /api/ler`), `ferramentas/painel/index.html` (login, introdução, tela única, 10 cartões com expansão e curvas SVG, polling de 3 s que só redesenha quando muda), `painel.cmd`, `ferramentas/tests/test_painel.py`.
- `sessao.py`: `memoria`/`preset` (com `--limpar`/`--remover`) → `estado/memorias.json`; `os.replace` com retry para o leitor do Windows.
- `ajustar_processador.py`: registro gravado "em andamento" dentro do try/finally e gravação atômica, para o painel mostrar o agente atuando sem ficar preso.
- Curvas calculadas no servidor com as mesmas fórmulas do guardião. O painel mostra sempre o retrato mais recente por nome, mesmo que seja o de uma memória carregada só para conferência.
- Não testado com o processador ligado; "Ler agora" foi exercitado até o código 3 (não encontrado).

## Spec Change Log

- 2026-10-16 · O implementador alterou `ferramentas/ajustar_processador.py` (fora do Code Map) para gravar o registro com `status: "em andamento"` antes de conectar e antes de cada escrita. Sem isso, o critério "o cartão Agente mostra o lote em curso" não tem produtor. Aceito como parte do Code Map: `ajustar_processador.py` -- grava o registro em andamento; gravação atômica. KEEP: o arquivo final continua idêntico; o `finally` regrava o status definitivo.

## Review Triage Log

| # | Achado | Veredito | Evidência / rota |
|---|---|---|---|
| 1 | Registro "em andamento" gravado antes do try/finally fica preso para sempre após Ctrl+C no scan; painel mostra "lote em curso" eternamente (blind, edge, gap) | high | Novo nesta mudança: antes não existia arquivo até o status final. Patch: mover para dentro do try + validade de 10 min no painel. |
| 2 | `subprocess.run` sem timeout: leitor travado deixa "lendo…" para sempre (blind, edge) | medium | Patch: timeout 120 s; stderr no detalhe. |
| 3 | `/api/ler` sem senha disponível → filho bloqueia no getpass; durante ajuste aberto → duas sessões BLE (edge) | medium | Patch: 409 nos dois casos. |
| 4 | `senha_guardada` ignora `TARAMPS_SENHA` (blind, edge) | low | Correção direta. Patch. |
| 5 | `_item` 071 não numérico, `alertas` não lista, datas com fuso → 500 no `/api/estado` e página inteira cai (blind, edge, gap) | medium | Contradiz "arquivo corrompido é ignorado". Patch: guardas por seção. |
| 6 | Redesenho total a cada 3 s perde rolagem, seleção e o modal (blind) | medium | `innerHTML` em tudo. Patch: redesenhar só se mudou. |
| 7 | `status` do ajuste sem escape; login não volta se a senha sumir (edge) | low | Correções diretas. Patch. |
| 8 | `_gravar_json` do ajustar não é atômico: painel pode ler JSON truncado (edge) | low | Patch: tmp + replace, com retry no Windows. |
| 9 | Windows `SO_REUSEADDR` deixa duas instâncias na mesma porta; `localhost` pode ir para ::1 (edge) | medium | Verificado pelo revisor. Patch. |
| 10 | Sem como apagar memória/preset registrados (blind) | medium | `memorias.json` diverge do aparelho sem remédio. Patch. |
| 11 | Testes: "em andamento" intermediário nunca observado; fixture do painel com forma diferente do produtor; reserva sem teste; 071 OFF sem teste; teste tautológico; CliTest não faz GET (gap, blind) | medium | Confirmados por mutação. Patch. |
| 12 | Docstring do painel e duplicata de `carregar_memorias` (blind) | low | Correções diretas. Patch. |
| 13 | Handler sem timeout e corpo inválido mal diagnosticado; chmod 600 (blind, edge) | low | Correções diretas. Patch. |
| 14 | Acessibilidade por teclado nos cartões (blind) | low | O Denis usa mouse; adiciona marcação. Rejeitado. |
| 15 | `Host` não validado no GET (DNS rebinding) (blind) | low | Só localhost, uso pessoal. Rejeitado. |
| 16 | Dois POST de senha simultâneos (edge) | low | Um usuário. Rejeitado. |
| 17 | Sufixo -10 ordena antes de -2 (edge) | low | Nunca 10 leituras no mesmo minuto. Rejeitado. |
| 18 | Nome de memória começando com "-" (edge) | low | Nomes do roteiro não começam assim. Rejeitado. |
| 19 | Nits: comentário "13 min" e CSS duplicado (blind) | low | Rejeitado. |

## Design Notes

Os cartões são desenhados por função JS por tela, a partir de `leituras[]` agrupadas por `tela`. O expandido do Crossover usa a mesma fórmula do guardião (Butterworth/LR por índice) para a curva, em escala log de 10 Hz a 22 kHz, com a cor do canal (vermelho 1, verde 2, azul 3, laranja 4 como no app).

## Verification

**Commands:**
- `uv run python -m unittest discover -s ferramentas/tests` -- expected: tudo passa.
- `uv run ferramentas/painel.py --sem-navegador` -- expected: sobe em 8765 e `/api/estado` responde com a última leitura.

**Manual checks (if no CLI):**
- Abrir `painel.cmd`, ver a introdução, entrar, expandir Crossover e Limiter, clicar "Ler agora" com o processador ligado.
