---
title: 'História 14a: ferramenta da porta, serviço que acorda e modelo falso'
type: 'feature'
created: '2026-11-01'
baseline_revision: '569a749d80bd70f874e61f6c151b8e8b3924af10'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-agente-memoria/SPEC.md'
  - '{project-root}/_bmad-output/specs/spec-agente-memoria/cerebro-do-agente.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/porta-de-comandos.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** O Agente da memória roda no notebook e só fala com o app pela porta (spec-agente-memoria CAP-1). Ainda faltam três peças:
- uma ferramenta de formato fixo para ele usar a porta;
- um serviço que escute o "+ Nova memória" e acorde o Agente;
- um jeito de testar tudo sem o modelo de verdade.

**Approach:** Uma pasta `agente/` na raiz, com uma solução .NET 10 própria:
- **`Porta.Cli`:** a ferramenta da porta, com JSON na entrada e na saída.
- **`Servico`:** escuta o `/eventos` e acorda um Agente por vez.
- **`ModeloFalso`:** respostas gravadas que fazem o papel do modelo, pela mesma ferramenta.

Os testes de ponta a ponta sobem a `PortaComandos` do núcleo do app sobre o `ProcessadorSimulado`.

## Boundaries & Constraints

**Always:**
- **Ferramenta da porta (`porta`)**, um executável de console. A entrada é um JSON (o argumento `--json '<...>'` ou o stdin) e a saída é sempre um JSON numa linha no stdout, com código de saída 0 no sucesso e 1 na recusa ou erro.
  - **Formato da entrada:** `{"acao":"ler|pedir|parear","metodo":"GET|POST|DELETE","alvo":"/estado","corpo":{...}}`.
  - **Formato da saída:** `{"ok":true,"status":200,"corpo":{...}}` ou `{"ok":false,"status":409,"motivo":"..."}`.
  - **`parear`:** `{"acao":"parear","endereco":"192.168.0.5:8787","codigo":"123456"}` chama a rota de pareamento da porta e guarda o endereço e a chave em `agente/.local/porta.json`. O `.local/` fica fora do git.
  - **Todo pedido** usa o endereço e a chave guardados. Sem pareamento: `{"ok":false,"motivo":"Sem pareamento: rode porta parear."}`.
  - **Atalhos de leitura:** `{"acao":"ler","alvo":"/estado"}` é o mesmo que um GET.
  - O tempo máximo por pedido é de 70 s, para caber a folha de risco de 60 s.
  - A ferramenta não guarda nada além do pareamento e nunca mostra a chave na saída.
- **Serviço (`servico`):** um processo que roda no notebook.
  - **Ciclo:** lê `/eventos?desde=N` a cada 1 s e guarda o último N em `agente/.local/servico.json`. Ao subir, começa do evento atual, sem reprocessar o passado.
  - **Gatilhos** (lidos de `agente/servico.config.json`):
    - `criacao-pedida` → `criar-memoria`;
    - `ficha-pedida` (que a 14b acrescenta) → `ficha-tecnica`.
  - **Um Agente por vez.** Um gatilho com um Agente rodando é ignorado, com uma linha no log ("Já há um Agente rodando; pedido ignorado.").
  - **Lançar:** o `ILancadorAgente` real roda o comando do config, que é o Claude Code em modo não interativo. O exemplo do config é `claude -p "<prompt da tarefa>" --output-format json` com a ferramenta `porta` liberada. O diretório de trabalho é `agente/`, e o prompt da tarefa vem de `agente/tarefas/<tarefa>.md`. O conteúdo das skills é da 14d; aqui entra um esqueleto que manda usar a `porta`.
  - **Prazo do Agente:** 30 min [ASSUMPTION]. Depois disso o processo é encerrado, e o serviço manda `POST /criacao/cancelar` se a criação ainda estiver aberta.
  - **Fim do Agente:** o serviço manda `DELETE /tarefa`, se houver tarefa, e registra no log o código de saída e a duração.
  - **Queda da porta:** o serviço tenta de novo a cada 5 s, com uma linha no log a cada mudança de estado (conectado ou sem resposta).
  - **Log:** `agente/.local/servico.log`, com data e hora em cada linha, sem a chave.
- **Modelo falso (`ModeloFalso`):** um `ILancadorAgente` que, em vez do Claude Code, roda um roteiro gravado (`agente/testes/roteiros/*.json`) chamando a mesma ferramenta `porta`. Os passos são `pedir` (um pedido à porta) e `esperar` (um evento no `/eventos`, por exemplo `criacao-resposta`, com um prazo), e o roteiro pode ramificar pela resposta recebida.
  - Os roteiros usam só dados genéricos, nada do sistema do Denis.
  - O roteiro de exemplo, `criacao-basica.json`, abre a criação, faz 2 perguntas (a 2ª como última), espera as respostas, manda 2 ajustes com frase e motivo, manda o resumo e espera o "Como ficou?".
- **Nenhum dado do sistema do Denis** (Setup, fichas, memórias, endereço, chave) fica embutido ou versionado.
- **O `agente/README.md`** explica como parear, como rodar o serviço e como trocar o lançador.

**Never:**
- As skills do Agente (14d), o aprendizado (14e) e os ouvintes simulados (14f).
- O lado do app da 14b: o prazo de 15 s, a escuta "1 ou 2" e a ficha.
- Chamar o modelo de verdade nos testes.
- Mudar a porta do app, exceto se faltar algo para o pareamento a partir de `localhost` nos testes (aí, só a regra mínima, com teste).
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Parear | `porta parear` com o código da tela, contra a porta do teste | `{"ok":true}`; `porta.json` com o endereço e a chave | Código errado: `ok:false` com o motivo da porta |
| Ler | `{"acao":"ler","alvo":"/estado"}` | `{"ok":true,"status":200,"corpo":{...}}` | Sem pareamento: o motivo |
| Recusa | POST `/ajuste` fora da cerca | `{"ok":false,"status":409,"motivo":"Limite: …"}`, código 1 | — |
| Acordar | O app emite `criacao-pedida` (o `Pedir()` do núcleo); o serviço roda com o modelo falso | O roteiro abre a criação em até 15 s (`GET /criacao` mostra Perguntas) | — |
| Um por vez | 2º `criacao-pedida` com o falso rodando | Nenhum 2º lançamento; a linha no log | — |
| Ponta a ponta | O roteiro básico, e as respostas pelo `CriacaoMemoria` (o usuário) | 2 perguntas respondidas; 2 ajustes relidos; o "Como ficou?" na tela | — |
| Prazo | O falso trava (um roteiro que não termina), com o prazo do teste curto | Processo encerrado; `/criacao/cancelar`; o log | — |
| Porta cai | A porta para no meio | O serviço tenta de novo e volta a ler quando ela volta | — |
| Sem segredo | Varredura dos arquivos versionados de `agente/` | Nenhuma chave, endereço de rede ou dado do Casa | — |

</intent-contract>

## Code Map

- `app-controle/src/BgsControle.Nucleo/Porta/PortaComandos.cs`:
  - as rotas (o `GET /eventos?desde=N`, o `/criacao/*`, o `DELETE /tarefa` e o pareamento) e o `TextoAjuda` (1704: o `criacao-pedida`);
  - `PortaPadrao = 8787`;
  - a regra de rede local (`RedeLocal.cs`), que precisa aceitar `127.0.0.1` no teste ou já aceita.
- `app-controle/src/BgsControle.Nucleo/Porta/ServidorHttp.cs`: o servidor HTTP mínimo (`TcpListener`).
- `app-controle/src/BgsControle.Nucleo/Setups/CriacaoMemoria.cs`: `Pedir()`, o `EventoPedida`, `Responder` e `ResponderComoFicou`. É o lado do usuário no teste.
- `app-controle/tests/BgsControle.Nucleo.Testes/PortaTestes.cs`: a montagem da porta sobre o simulador (`Pronto()`, `Pedir`, o pareamento) e o teste HTTP de ponta a ponta da 9b. O modelo da bancada é `CriacaoTestes.cs`.
- `app-controle/tests/BgsControle.Simulador/` (`ProcessadorSimulado`, `CasaDoDenis` só como dado de teste).
- `app-controle/Directory.Build.props`: fixa o JDK e o Android SDK. A solução de `agente/` não herda isso (fica fora de `app-controle/`).

## Tasks & Acceptance

**Execution:**
- `agente/BgsAgente.slnx`, `agente/Directory.Build.props` (.NET 10, nullable e avisos como erro, como o app) e `agente/.gitignore` (`.local/`).
- `agente/src/Agente.Porta/` (biblioteca: o cliente HTTP, o formato JSON, o pareamento e o armazenamento em `.local`) e `agente/src/Porta.Cli/` (o executável `porta`).
- `agente/src/Agente.Servico/` (biblioteca: o ciclo, os gatilhos, um por vez, o prazo, o fim, a queda e o log; `ILancadorAgente` com o `LancadorClaude`) e `agente/src/Servico/` (o executável).
- `agente/src/Agente.ModeloFalso/`: o lançador dos roteiros e o formato do roteiro.
- `agente/servico.config.json` (o exemplo, sem endereço), `agente/tarefas/criar-memoria.md` e `ficha-tecnica.md` (esqueletos), `agente/testes/roteiros/criacao-basica.json` e `agente/README.md`.
- `agente/tests/Agente.Testes/` (referencia o `BgsControle.Nucleo` e o `BgsControle.Simulador` do app): uma linha da matriz por teste, de ponta a ponta com a `PortaComandos` numa porta livre de `localhost`.

**Acceptance Criteria:**
- Given a porta do núcleo sobre o simulador e o serviço com o modelo falso, when o app emite `criacao-pedida`, then a criação abre em até 15 s, e um 2º pedido não lança outro Agente.
- Given os testes do app, when a 14a entra, then todos continuam passando, e os testes de `agente/` passam.

## Spec Change Log

- **01/11, depois da revisão:**
  - **Isolamento do Agente** (fora do intent-contract): a pasta local saiu do repositório (`%LOCALAPPDATA%\BgsAgente`, com a chave do pareamento), e o Agente de verdade roda numa pasta de trabalho fora do repositório (`<local>	rabalho`), com as tarefas copiadas antes de cada lançamento. Assim ele não carrega o CLAUDE.md nem a memória do projeto (o "começa zerado") e não alcança a chave. O que ainda carrega (as configurações do usuário) está no README, para a 12e conferir.
  - **No fim do Agente**, a criação aberta em perguntas, ajustando ou resumo é cancelada; em "Como ficou?" e no Salvar ela fica.
  - **KEEP:** a ferramenta `porta` como o ponto de troca do modelo falso e do Claude Code.

## Review Triage Log

### 2026-11-01 — Review pass
- verdicts: 50 findings — high 0, medium 10, low 37, false 2, maybe-false 1
- findings:
  - `[low]` `[reject]` Intent: o Claude Code real nunca roda nos testes — de propósito (o modelo falso no build-auto); a 12e roda o de verdade.
  - `[maybe-false]` `[patch]` Intent: o Agente lançado dentro do repositório pode carregar o CLAUDE.md e a memória do projeto (o "começa zerado") — roda numa pasta de trabalho fora do repositório; o que ainda carrega fica para a 12e conferir.
  - `[low]` `[patch]` Intent: o README diz que o serviço roda como processo nos testes — corrigido (roda no processo do teste).
  - `[low]` `[reject]` Intent: os 15 s com o Claude Code de verdade — é da 12e; aqui o falso, como a CAP-1 pede.
  - `[low]` `[reject]` Intent: a porta que cai com o Agente rodando não tem teste — o falso espera até o próprio prazo, e o serviço cancela a criação no fim do Agente (o patch abaixo).
  - `[false]` `[reject]` Intent: a bancada usa o `CenarioFilme` (dados do Denis) — fica fora de `agente/`, como dado de teste do app; a spec aceita.
  - `[low]` `[patch]` Blind: a ferramenta sem pegador geral (endereço inválido, chave não texto, `--local` inválido) — pegador geral e o endereço conferido ao ler.
  - `[medium]` `[patch]` Blind: a criação fica aberta sem Agente quando ele sai sozinho, o lançador falha ou o serviço para — cancela nas etapas de perguntas, ajustando e resumo.
  - `[medium]` `[patch]` Blind: um `tipo` desconhecido lança o Claude pago — só `claude` e `modelo-falso`; o resto sai com 2.
  - `[medium]` `[patch]` Blind: a config sem validação, e o JSON estragado derruba — `Validar()` e a saída 2.
  - `[medium]` `[patch]` Blind: a chave da porta ao alcance do Agente — a pasta local fora do repositório.
  - `[medium]` `[patch]` Blind: o esqueleto manda ler `/eventos?desde=0` — lê o `ultimo` primeiro e espera 1 s entre leituras.
  - `[low]` `[patch]` Blind: o falso volta ao cursor 0 se a primeira leitura falha — tenta de novo e falha.
  - `[low]` `[patch]` Blind: o falso não vê o `criacao-salva` como fim — vê.
  - `[low]` `[patch]` Blind: o `DELETE /tarefa` apaga qualquer tarefa — só a que nasceu com o Agente.
  - `[low]` `[reject]` Blind: o `servico.json` gravado e nunca lido — a spec manda começar do evento atual; o arquivo fica para diagnóstico.
  - `[low]` `[patch]` Blind: faltam testes (stdin, PATH, reinício do app, queda com o Agente, prazo dos netos, ficha sem roteiro) — testes (a) a (e).
  - `[low]` `[reject]` Blind: a spec diverge (o `RedeLocal`, o `status` do parear, o IP de exemplo, a nota do `Directory.Build.props`) — o conserto seria editar a spec; o IP é um exemplo do formato, fora de `agente/`.
  - `[low]` `[patch]` Blind: os logs sem limite e o stderr descartado — os logs giram em 5 MB e o stderr vai na recusa (o ritmo de processos do falso fica, é só de teste).
  - `[low]` `[patch]` Verification gap: o reinício do app com o cursor alto — teste (a).
  - `[low]` `[patch]` Verification gap: o PATH, a pasta e o `AGENTE_LOCAL` do lançador real — teste (b).
  - `[low]` `[patch]` Verification gap: a entrada por stdin e posicional — teste (c).
  - `[low]` `[patch]` Verification gap: o prazo de 70 s só pela constante — teste (d).
  - `[low]` `[patch]` Verification gap: a escolha do lançador pelo `tipo` — a fábrica testada.
  - `[low]` `[patch]` Verification gap: a data e a hora em cada linha do log — teste (e).
  - `[low]` `[patch]` Edge: o evento lido duas vezes entre o `ultimo` e a leitura — guarda o maior n.
  - `[low]` `[patch]` Edge: o contador que reinicia passa o cursor velho — teste do reinício.
  - `[low]` `[patch]` Edge: o corpo sem `ultimo` zera o cursor — conta como leitura recusada.
  - `[medium]` `[patch]` Edge: o `CancelAfter` fora do try trava o serviço — dentro do try, e o prazo validado.
  - `[low]` `[patch]` Edge: os intervalos ≤ 0 e as listas nulas — `Validar()`.
  - `[medium]` `[patch]` Edge: o `tipo` com erro de digitação lança o Claude — o mesmo do Blind.
  - `[medium]` `[patch]` Edge: a criação aberta quando o Agente sai — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o prazo cancela o Salvar do usuário — não cancela no `salvar`.
  - `[medium]` `[patch]` Edge: a escrita do log com `UnauthorizedAccessException` trava o serviço — pega também.
  - `[low]` `[patch]` Edge: o `agente.log` que lança no handler mata o processo — pega também.
  - `[low]` `[patch]` Edge: o `ia` que não é objeto — leitura tolerante.
  - `[low]` `[reject]` Edge: um prompt maior que a linha de comando do Windows — as tarefas são curtas; a 14d decide se o prompt vai por arquivo.
  - `[low]` `[patch]` Edge: a chave que não é texto no pareamento — recusa.
  - `[low]` `[patch]` Edge: o endereço estragado no `porta.json` — conferido ao ler.
  - `[low]` `[patch]` Edge: a resposta HTTP vazada no prazo — descartada.
  - `[low]` `[patch]` Edge: `?` ou `#` no endereço mandam a chave para outra porta — recusados.
  - `[low]` `[patch]` Edge: o código numérico perde o zero da frente — recusa o número.
  - `[low]` `[patch]` Edge: `se`, `tipos` ou `metodo` nulos no roteiro — recusados no `Validar()`.
  - `[low]` `[patch]` Edge: o `prazoS` enorme — recusado.
  - `[low]` `[patch]` Edge: o ciclo de `ir` sem pausa — limite de passos.
  - `[low]` `[patch]` Edge: a primeira leitura de eventos que falha no falso — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o `tipo` do evento que não é texto — leitura tolerante.
  - `[low]` `[patch]` Edge: a `porta` que não existe no `FerramentaProcesso` — recusa com o motivo.
  - `[false]` `[reject]` Edge: a porta livre tomada por outro processo na bancada — os testes passaram em três rodadas; é a técnica de porta livre de sempre.
  - `[low]` `[patch]` Edge: a leitura do stdout sem prazo no apoio dos testes — o prazo de 90 s também nela.

## Design Notes

- **Por que a ferramenta é o ponto de troca:** o Claude Code e o modelo falso usam a mesma `porta`, então os testes exercitam o caminho que o modelo de verdade vai usar.
- **Por que `agente/` fora de `app-controle/`:** o Agente não é parte do app (ele roda no notebook), e a solução do app fixa o SDK Android.

## Verification

**Commands:**
- `dotnet test agente/tests/Agente.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos (só se o app mudou).

## Auto Run Result

Status: done

**O que entrou.** A pasta `agente/` na raiz, com a solução .NET 10 própria (spec-agente-memoria CAP-1):
- **`porta`:** a ferramenta da porta em JSON, com o pareamento.
  - Sempre uma linha JSON, com o código 0 ou 1.
  - O endereço é conferido, e a chave fica numa pasta local fora do repositório e nunca sai.
  - O prazo é de 70 s.
- **`servico`:**
  - lê o `/eventos` a cada 1 s, com o cursor robusto (o maior n, sem zerar, e o reinício do app pela hora);
  - acorda um Agente por vez (`criacao-pedida` → `criar-memoria`; `ficha-pedida` → `ficha-tecnica`);
  - o prazo é de 30 min;
  - no fim, cancela a criação aberta nas etapas do Agente e apaga só a tarefa que nasceu com ele;
  - tenta de novo quando a porta cai;
  - o log tem data e hora em cada linha e gira em 5 MB;
  - a config é validada, e só `claude` e `modelo-falso` são aceitos.
- **O Agente de verdade** (Claude Code em modo não interativo) roda numa pasta de trabalho isolada, fora do repositório.
- **O modelo falso:** roteiros JSON gravados (`pedir`, `esperar`, ramos e fim) pela mesma ferramenta `porta`, com validação e limite de passos.
- **Os esqueletos das tarefas** e o README.

**Arquivos:**
- `agente/BgsAgente.slnx`, `Directory.Build.props` e `.gitignore`;
- `src/Agente.Porta` e `src/Porta.Cli`;
- `src/Agente.Servico` e `src/Servico` (com a `FabricaLancador`);
- `src/Agente.ModeloFalso`;
- `servico.config.json`, `tarefas/*.md`, `testes/roteiros/*.json` e `README.md`;
- `tests/Agente.Testes`, de ponta a ponta com a `PortaComandos` do núcleo sobre o simulador.

O app não mudou.

**Revisão.** 50 achados (10 medium, 37 low, 2 false, 1 maybe-false).
- **Patches:**
  - o isolamento do Agente e da chave;
  - a criação aberta no fim do Agente;
  - a tarefa alheia;
  - o cursor;
  - as exceções e os logs;
  - a validação da config e do `tipo`;
  - o pegador geral da ferramenta e o endereço;
  - os esqueletos;
  - o modelo falso;
  - os testes do reinício, do lançador, do stdin, do prazo e do log.
- **Nada diferido.**
- **Rejeitados,** com o motivo no Review Triage Log.

**Revisão de acompanhamento: recomendada.** Foram várias entradas medium com patch. O risco que ninguém conferiu é o Claude Code de verdade: a permissão `Bash(porta:*)`, o `claude.exe` no Windows e o que as configurações do usuário ainda carregam na pasta isolada. Isso é da 12e.

**Verificação:**
- `dotnet test agente/tests/Agente.Testes`: 92 de 92.
- `dotnet test` Núcleo: 1338 de 1338.
- `dotnet test` Telas: 523 de 523.
- A matriz tem pelo menos um teste por linha.
- Nenhum IP nem dado do Casa em `agente/`.

**Lacuna conhecida:** não há teste de ponta a ponta de que a criação em "Como ficou?" ou no Salvar sobrevive ao fim do Agente. A regra está numa lista fixa de etapas (`ServicoAgente.EtapasDoAgente`).
