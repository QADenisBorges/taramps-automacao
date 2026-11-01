---
title: 'História 14d: skills e instruções em camadas'
type: 'feature'
created: '2026-11-01'
baseline_revision: '098530fe66cbd0162b5834fd8f5207b38fa32926'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-agente-memoria/cerebro-do-agente.md'
  - '{project-root}/_bmad-output/forge/montagem-do-setup/estrategia-mesa.md'
  - '{project-root}/_bmad-output/specs/spec-agente-memoria/SPEC.md'
  - '{project-root}/agente/README.md'
warnings:
  - oversized
deferred:
  - summary: >-
      O "só a porta" é garantido pelo texto e pelo --allowedTools, não pelas permissões: o Agente carrega o ~/.claude/settings.json do usuário.
    evidence: |-
      O --allowedTools só acrescenta permissões. Um modo de permissão amplo nas configurações do usuário deixaria o Agente usar outro comando.
      Fecha com o lançador fixando as fontes de configuração e o modo de permissão (flags do Claude Code a conferir na versão instalada), com teste dos argumentos e a conferência na 12e.
    location: >-
      agente/servico.config.json
    severity: medium
  - summary: >-
      Não se sabe se, no modo não interativo, as skills de .claude/skills carregam e se a ferramenta Skill precisa estar no --allowedTools.
    evidence: |-
      Sem rodar o Claude Code de verdade, nenhum teste confere o carregamento. Se a Skill for negada, o Agente trabalha só com a base e as tarefas.
      Fecha com uma rodada real na 12e (ou na avaliação da 14f): o log do agente mostra as skills usadas.
    location: >-
      agente/servico.config.json
    severity: medium (unverified)
  - summary: >-
      Um segundo ficha-pedida com um Agente rodando é ignorado, e o pedido de 60 s do app vence sem proposta.
    evidence: |-
      É a regra da 14a (um Agente por vez, o pedido com outro rodando vai ao log). Dois equipamentos novos salvos em seguida perdem a segunda ficha.
      Fecha com uma fila curta de pedidos de ficha no serviço, dentro do prazo do app.
    location: >-
      agente/src/Agente.Servico/ServicoAgente.cs
    severity: medium
---

<intent-contract>

## Intent

**Problem:** O Agente da 14a só tem o esqueleto das tarefas: não tem regras invioláveis, skills, nem conhecimento com fonte. A tarefa da ficha nem sabe qual equipamento pediu a ficha, porque o serviço não passa o evento que o acordou. Sem isso, o Claude Code não cria uma memória como técnico (spec-agente-memoria CAP-2 a CAP-6).

**Approach:** Versionar em `agente/` as instruções em camadas de `cerebro-do-agente.md`:
- a base curta;
- as skills objetivo, escuta, diagnostico, receita, ficha-tecnica e medicao-assistida;
- o conhecimento com fonte;
- as tarefas, reescritas para usar as skills.

O lançador leva tudo para a pasta de trabalho, junto com o evento que acordou o Agente. Testes conferem a estrutura e o encanamento com o modelo falso, sem modelo real.

## Boundaries & Constraints

**Always:**
- **Camadas e onde ficam** (em `agente/`, versionadas):
  1. `instrucoes/base.md`, a base curta, com até 40 linhas não vazias. É copiada como `CLAUDE.md` da pasta de trabalho, que o Claude Code carrega sozinho. Traz as regras invioláveis de `cerebro-do-agente.md` ("Instruções em camadas", item 1):
     - a cerca do Rendimento vale sempre (o `GET /limites`; a recusa da porta é regra);
     - nenhum valor sem motivo;
     - a ordem da física;
     - o Agente não salva: salvar é do usuário;
     - nenhum texto para a tela com a palavra "IA";
     - os dados do caso só pela porta, nunca supostos;
     - todo padrão é dúvida, sem direção fixa nem uso fixo;
     - o valor pedido, o calculado e o recomendado, cada um com o motivo, e a estimativa marcada como estimativa.
  2. `skills/<nome>/SKILL.md`, uma por função (a tabela "Skills" de `cerebro-do-agente.md`, sem a cerca-fisica, que é código do app). São copiadas para `.claude/skills/<nome>/SKILL.md` na pasta de trabalho.
     - O frontmatter tem `name` (igual à pasta), `description` (quando usar, até 1024 caracteres) e `allowed-tools`.
     - O corpo tem `## Entrega` (o que a skill devolve, como na tabela) e `## Fontes` (os arquivos de `conhecimento/` que ela usa).
     - **objetivo:** a ficha de variáveis (fenômenos: objetivo, cobertura, posição de quem ouve, ambiente, tom, voz, dinâmica, cansaço; cada uma só se o Setup permitir), o padrão pela física e pelo Setup, a dúvida, a próxima pergunta por dúvida × impacto e a parada em 1 dB. A primeira pergunta é sempre o objetivo, com respostas montadas para aquele Setup e ambiente. Uma pergunta por vez (`POST /criacao/pergunta`, com 3 ou 4 respostas, e o "Outro…" do app).
     - **escuta:** só no refino; uma variável por vez; `POST /criacao/escuta`, com as versões 1 e 2 que só diferem nela; cada escolha (`criacao-escuta`) corta a dúvida pela metade.
     - **diagnostico:** a queixa vira hipóteses e depois a pergunta que separa uma da outra. O que o processador não faz (separação, espaço, alcance) é posição da caixa, sem ajuste.
     - **receita:** as variáveis viram ajustes na ordem da física (cerca, rota, ganho, corte, atraso e fase; depois tom; por último dinâmica). Cada ajuste vai com frase, valor de antes, valor novo e motivo (`POST /criacao/ajuste`), dentro da cerca. A rota (entradas separadas ou somadas) é decisão com motivo, nunca padrão.
     - **ficha-tecnica:** busca a ficha do modelo do evento, confere se ela fecha (Qts pelo Qes e Qms; a sensibilidade pelos parâmetros) e manda `POST /ficha/proposta`, com a origem (`oficial` ou `outra-fonte`), a fonte (o endereço) e o aviso do que não fechou. Nunca inventa um dado: o que não achou fica fora.
     - **medicao-assistida:** lê pela porta o que a calibração guiada do app mediu (`GET /limites`: calibrada, teto, tetoV, manda e as contas) e explica a diferença entre o esperado e o medido. Não mede nem calibra: a calibração é do usuário no celular.
  3. `conhecimento/*.md`, as regras de áudio gerais. Cada regra é um item da seção `## Regras`, com uma linha `Fonte:` que não pode ficar vazia. A fonte é uma referência real e conferível (livro, norma, artigo ou um documento do projeto, como `estrategia-mesa.md`). Se não houver uma fonte assim, a regra fica fora.
  4. `tarefas/criar-memoria.md` e `tarefas/ficha-tecnica.md`, reescritas para dizer qual skill usar em cada passo. O texto "Esqueleto da história 14a" sai.
- **Só a porta:** toda skill fala com o app só pela ferramenta `porta`.
  - O `allowed-tools` é `Bash(porta:*)`. Só a ficha-tecnica acrescenta `WebSearch` e `WebFetch`, para ler a ficha na web (a busca precisa do endereço da fonte).
  - Nenhum arquivo das camadas manda usar `curl`, `wget`, `Invoke-WebRequest`, `adb`, um endereço `http://` ou a porta `8787`.
  - Todo alvo citado (`"alvo":"/…"` ou um caminho como `POST /criacao/ajuste`) existe no `PortaComandos.TextoAjuda`.
- **O evento que acordou o Agente:**
  - `ILancadorAgente.RodarAsync` ganha o evento (`JsonObject?`), e o `ServicoAgente` passa o evento do gatilho.
  - O `LancadorClaude` acrescenta ao prompt uma seção "O evento que acordou você" com o JSON do evento.
  - O modelo falso recebe `--evento <json>`. Num `pedir`, um valor de texto inteiro `"{evento.<campo>}"` vira o valor do campo do evento, com o tipo dele.
- **Ferramentas por tarefa:**
  - `ConfigLancador.Ferramentas` mapeia tarefa → lista, com a chave `"*"` como padrão (`["Bash(porta:*)"]`).
  - O marcador `{ferramentas}` dos argumentos vira a lista da tarefa, separada por espaço.
  - O `servico.config.json` passa a usar `"--allowedTools", "{ferramentas}"` e dá à ficha-tecnica `Bash(porta:*) WebSearch WebFetch`.
  - Uma lista vazia ou com item vazio para o serviço (código 2), como as outras configurações inválidas.
- **A cópia:** a cada lançamento, o `LancadorClaude` recria na pasta de trabalho as `tarefas/`, o `CLAUDE.md` (de `instrucoes/base.md`), as `.claude/skills/` e o `conhecimento/`. O que sobrou de antes sai.
- **O roteiro novo:** `testes/roteiros/ficha-basica.json` manda uma proposta genérica com `"{evento.idEquipamento}"` e entra em `roteiros` com a `ficha-tecnica`.
- Nada do sistema do Denis: os `SemSegredoTestes` já varrem todo arquivo versionado de `agente/`.
- O README explica as camadas, o evento e as ferramentas por tarefa.

**Never:**
- Chamar o modelo de verdade, em teste ou em script.
- Os registros e o perfil do aprendizado (14e), os ouvintes simulados e o avaliador (14f).
- A cerca como skill (é código do app), e mudar o app (`app-controle/`).
- Um gabarito de valores, memórias do aparelho ou regulagens antigas nas camadas.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Skills carregam | As 6 pastas em `skills/` | Cada uma com `name` igual à pasta, `description`, `allowed-tools`, `## Entrega` e `## Fontes` | Falta um campo: o teste aponta a skill |
| Fontes | Cada `## Fontes` | Cita arquivos de `conhecimento/` que existem; cada regra de lá tem `Fonte:` | Regra sem fonte: o teste aponta o arquivo e a regra |
| Só a porta | O texto das camadas | Nenhum `curl`, `wget`, `adb`, `http://` ou `8787`; os alvos existem na ajuda | Alvo que não existe: o teste aponta |
| Ferramentas | `allowed-tools` | `Bash(porta:*)`; a ficha-tecnica com `WebSearch` e `WebFetch` | Outra ferramenta: falha |
| Cópia | `InicioPara("criar-memoria")` | A pasta de trabalho com o `CLAUDE.md`, as 6 skills, o conhecimento e as tarefas; os restos de antes saem | — |
| Prompt com evento | Lançar com `{"tipo":"ficha-pedida","modelo":"ABC 123",…}` | O prompt traz a tarefa e o JSON do evento | Sem evento: só a tarefa |
| Ferramentas da tarefa | `{ferramentas}` na ficha-tecnica | "Bash(porta:*) WebSearch WebFetch"; nas outras, "Bash(porta:*)" | Lista vazia: config inválida (2) |
| Ficha ponta a ponta | Serviço com o modelo falso; salvar um equipamento com o modelo fora da base | `ficha-pedida`, a tarefa ficha-tecnica, o roteiro manda a proposta com o id do evento, e a `PropostaFicha.Atual` tem os dados com a origem | — |
| Base curta | `instrucoes/base.md` | Até 40 linhas não vazias, com as regras invioláveis | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `agente/`, salvo onde diz outro lugar.

- `src/Agente.Servico/Lancadores.cs`:
  - `ILancadorAgente.RodarAsync` (l.8–15) recebe o evento;
  - `LancadorProcesso.Montar` (29) recebe a tarefa;
  - `LancadorClaude` (125–200): `Prompt` (136), `Montar` (149, onde entram os marcadores `{prompt}` e `{tarefa}`) e `CopiarTarefas` (169), que vira a cópia das camadas.
- `src/Agente.Servico/ConfigServico.cs`: `ConfigLancador` (≈105) com `Argumentos` e `Validar` (59), onde entra `Ferramentas`.
- `src/Agente.Servico/ServicoAgente.cs`:
  - `Acordar(tipo, tarefa, …)` (≈257) e `RodarAgenteAsync` (≈271) passam o evento;
  - o laço dos eventos (196–202) tem o `e.Evento` (o `JsonNode` do evento).
- `src/Agente.ModeloFalso/LancadorModeloFalso.cs` (o `--evento`), `Program.cs` (o argumento) e `ExecutorRoteiro.cs` (`PedirAsync` ≈106, a troca do `{evento.campo}` no corpo).
- `servico.config.json`, `README.md`, `tarefas/*.md` e `testes/roteiros/criacao-basica.json` (o modelo de roteiro).
- O app (só leitura):
  - `app-controle/src/BgsControle.Nucleo/Porta/PortaComandos.cs:1841`, `TextoAjuda` (`public static readonly`), com as rotas e os corpos: `/criacao/pergunta`, `/criacao/ajuste`, `/criacao/escuta`, `/ficha/proposta`, `/limites` e `/catalogo`;
  - `Setups/PropostaFicha.cs` (`Atual`, os campos por grupo em `DadosTecnicos.Campos`, as faixas).
- Testes, em `tests/Agente.Testes/`:
  - `LancadorTestes.cs` (o modelo dos testes do lançador e da config);
  - `ServicoTestes.cs` e `Bancada.cs` (o serviço com o modelo falso contra a porta do núcleo sobre o simulador; como a 14a faz a criação ponta a ponta);
  - `Apoio.cs:146` (`LancadorQueConta`, que muda de assinatura);
  - `SemSegredoTestes.cs` (já varre tudo).

## Tasks & Acceptance

**Execution:**
- `instrucoes/base.md`, `skills/{objetivo,escuta,diagnostico,receita,ficha-tecnica,medicao-assistida}/SKILL.md` e `conhecimento/*.md` (novos), como em Always.
- `tarefas/criar-memoria.md` e `tarefas/ficha-tecnica.md`: as skills em cada passo. A da ficha usa o evento do prompt (modelo, grupo, categoria e `idEquipamento`).
- `src/Agente.Servico/Lancadores.cs`, `ConfigServico.cs` e `ServicoAgente.cs`: o evento, as ferramentas por tarefa e a cópia das camadas.
- `src/Agente.ModeloFalso/*`: o `--evento` e a troca no corpo. `testes/roteiros/ficha-basica.json` (novo).
- `servico.config.json` e `README.md`.
- `tests/Agente.Testes/InstrucoesTestes.cs` (novo): a estrutura (as linhas Skills carregam, Fontes, Só a porta, Ferramentas e Base curta da matriz).
- `LancadorTestes.cs`: Cópia, Prompt com evento e Ferramentas da tarefa. `RoteiroTestes.cs`: a troca do `{evento.campo}`. `ServicoTestes.cs`: Ficha ponta a ponta. `Apoio.cs`: a nova assinatura.

**Acceptance Criteria:**
- Given o serviço com o modelo falso e a porta do núcleo, when o usuário salva um equipamento com o modelo fora da base, then a tarefa ficha-tecnica roda com o evento e a proposta chega à folha do app com a origem de cada dado (CAP-6, com o modelo falso).
- Given os testes que já existem (Agente, Núcleo e Telas), when a 14d entra, then todos passam.

## Spec Change Log

## Review Triage Log

### 2026-11-01 — Review pass
- verdicts: 38 findings — high 0, medium 6, low 28, false 2, maybe-false 2
- findings:
  - `[low]` `[reject]` Verification Gap: o ramo do `--evento` inválido no executável do modelo falso não tem teste — só o uso à mão chega nele (o serviço sempre manda um objeto); o teste pede subir o executável.
  - `[low]` `[reject]` Intenção: nenhum teste confere que o corpo de cada skill diz as regras da sua CAP — o sentido do texto é provado pela avaliação real (14f); a estrutura está testada.
  - `[low]` `[patch]` Intenção: o frontmatter é lido por um parser próprio, não pelo YAML — o teste agora recusa valores que o YAML não aceitaria sem aspas (`: `, ` #`, começo especial).
  - `[maybe-false]` `[defer]` Intenção: não se sabe se as skills carregam numa rodada real — no `deferred` (12e).
  - `[low]` `[reject]` Intenção: as fontes não são conferidas como verdadeiras — conferidas à mão nesta revisão (Small, Thiele, Toole, Moore, ISO 226, ITU-R BS.1116 e BS.775, ANSI S3.5, NIOSH 98-126, Olive et al. 2013 e as outras existem e dizem o que a regra diz); um teste não confere isso.
  - `[low]` `[reject]` Intenção: os documentos do projeto citados como fonte não vão à pasta de trabalho — cada regra traz o seu texto inteiro; a fonte é para quem confere, no repositório.
  - `[false]` `[reject]` Intenção: a ficha-tecnica com a web contraria o "só a porta" — a spec decidiu (Design Notes): "só a porta" é o caminho até o app; a web só lê.
  - `[medium]` `[defer]` Intenção: a restrição de ferramentas na hora de rodar depende das configurações do usuário — o mesmo do Blind abaixo, no `deferred`.
  - `[low]` `[reject]` Intenção: o modelo falso não percorre as skills da criação — as skills são texto para o modelo; o encanamento da criação é o roteiro da 14a, e o da ficha é o novo.
  - `[low]` `[reject]` Intenção: a cópia das camadas e o evento são testados em dois lançadores separados — cada metade tem o seu teste; o modelo falso não lê as camadas.
  - `[false]` `[reject]` Intenção: o encanamento do evento e das ferramentas passa da intenção curta — está no contrato da história (Approach e Always).
  - `[medium]` `[defer]` Blind: o "só a porta" é imposto pelo texto, não pelas permissões (o usuário pode ter um modo amplo) — no `deferred`; as flags do Claude Code precisam ser conferidas na versão instalada.
  - `[medium]` `[patch]` Blind: nada diz que o texto do evento e das páginas da web é dado, não instrução — uma linha na base.
  - `[medium]` `[patch]` Blind: a regra de "sem calibração" está incompleta (uma saída sem calibração também trava o master, o EQ de entrada, as 15 bandas, o preset e a rota) — corrigida em `ganho-e-limites.md`, na receita e na medicao-assistida.
  - `[low]` `[patch]` Blind: a receita não diz o que o `/criacao/ajuste` recusa (15 bandas, preset, link, mute) nem o par com link — uma frase no passo do tom.
  - `[medium]` `[patch]` Blind: o fim não espera `criacao-interrompida`, e nada diz que 30 s sem pedidos encerram a tarefa — a base e a tarefa dizem o teto de 10 s entre as consultas e o fim com `criacao-interrompida`.
  - `[low]` `[patch]` Blind: o "Tanto faz" da escuta contradiz o app (ele volta à versão 1) — a skill diz que fecha na versão 1.
  - `[low]` `[patch]` Blind: o diagnóstico manda o "Como ficou?" sem o resumo, que a porta exige — o passo fecha a rodada com o resumo antes.
  - `[low]` `[patch]` Blind: o objetivo não trata o ‹ do usuário (uma resposta com número menor) — o passo 6 diz refazer a ficha a partir dela.
  - `[low]` `[patch]` Blind: a ficha não conta o prazo pela hora do evento, nem diz o limite do aviso e os outros grupos — o passo 7 conta da `hora`, o aviso tem até 120 caracteres e os amplificadores conferem V = √(P × Z).
  - `[low]` `[patch]` Blind: "muda o valor por mil" com o exemplo de 10 000 — "por 10, 100, 1000 ou mais".
  - `[low]` `[patch]` Blind: o limite de 8 perguntas sem fonte — cita o sinal de sucesso da CAP-2.
  - `[low]` `[patch]` Blind: "o que sobrou de antes sai" promete mais que a cópia (só as camadas são recriadas) — o README e o comentário dizem só as camadas.
  - `[low]` `[patch]` Blind: o teste não lê o YAML — o mesmo patch do achado de intenção.
  - `[low]` `[patch]` Blind: um item de ferramenta com espaço quebra o `--allowedTools`, e uma chave com erro de digitação cai no padrão — o `Validar` recusa os dois (com teste).
  - `[low]` `[reject]` Blind: um campo do evento com `null` é dado como ausente — nenhum evento da porta manda campo nulo que um roteiro use; o conserto é um ramo novo.
  - `[low]` `[patch]` Edge: o texto do prompt passava pela troca dos outros marcadores (um `{ferramentas}` digitado no modelo virava a lista) — o prompt entra por último (com teste).
  - `[low]` `[patch]` Edge: um item de ferramenta com espaço — o mesmo do Blind.
  - `[low]` `[patch]` Edge: uma chave das ferramentas fora dos gatilhos — o mesmo do Blind.
  - `[low]` `[reject]` Edge: o `FerramentasDe` sem o `"*"` numa config montada em código — o `Validar` exige o `"*"` em toda config lida; só um teste montaria sem ele.
  - `[low]` `[reject]` Edge: a cópia leva só `.md` — as camadas só têm `.md`; outro tipo de arquivo é de uma história futura.
  - `[low]` `[patch]` Edge: os restos fora das camadas ficam na pasta de trabalho — o mesmo do Blind (a promessa foi estreitada; o Agente não tem ferramenta para escrever ali).
  - `[low]` `[reject]` Edge: o campo nulo do evento — o mesmo do Blind.
  - `[medium]` `[defer]` Edge: um segundo `ficha-pedida` com um Agente rodando se perde — é a regra de um Agente por vez da 14a; no `deferred`.
  - `[maybe-false]` `[defer]` Edge: a ferramenta Skill talvez precise estar no `--allowedTools` no modo não interativo — junto com o carregamento das skills, no `deferred`.
  - `[low]` `[reject]` Edge: o exemplo do `--evento` no README não roda no PowerShell 5.1 — o exemplo é de terminal comum; o serviço não passa pelo shell.
  - `[low]` `[patch]` Edge: o frontmatter que o YAML recusaria passa no teste — o mesmo patch.
  - `[low]` `[reject]` Edge: o teste dos alvos não vê PUT nem PATCH — a porta só tem GET, POST e DELETE.

## Design Notes

- **Por que a ficha-tecnica tem a web:** "só a porta" é o caminho até o app e o aparelho. A ficha precisa da fonte com o endereço (CAP-6). Sem ler a web, o modelo teria de inventar o endereço, o que a base proíbe. A web só lê; nada nela escreve no app.
- **Por que o evento vai no prompt:** a tarefa da ficha precisa do modelo e do `idEquipamento`. Como o serviço acorda o Agente depois do evento, o `/eventos` "desde o último" já não o traz.
- **Por que a base vira `CLAUDE.md`:** o Claude Code carrega o `CLAUDE.md` da pasta onde roda, e a pasta de trabalho fica fora do repositório (14a). As skills em `.claude/skills/` carregam pela descrição.

## Verification

**Commands:**
- `dotnet test agente/tests/Agente.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam (o app não muda).

## Auto Run Result

Status: done

**O que entrou.** As instruções em camadas do Agente, versionadas em `agente/` (spec-agente-memoria CAP-2 a CAP-6), e o encanamento que faltava:
- **A base curta** (`instrucoes/base.md`, que vira o `CLAUDE.md` da pasta de trabalho): as regras invioláveis, o teto de 30 s sem pedido e o evento e a web como dados.
- **As seis skills** (`skills/<nome>/SKILL.md`, copiadas para `.claude/skills/`): objetivo, escuta, diagnostico, receita, ficha-tecnica e medicao-assistida, cada uma com a Entrega e as Fontes.
- **O conhecimento com fonte** (`conhecimento/*.md`): percepção, alto-falantes, ganho e limites, sala e posição, dinâmica e tom. Cada regra tem a sua `Fonte:`.
- **As tarefas** dizem a skill de cada passo.
- **O evento que acordou o Agente** vai ao lançador: no prompt do Claude e em `--evento` no modelo falso, que troca `"{evento.<campo>}"` no corpo.
- **As ferramentas por tarefa** (`{ferramentas}`): só a porta, e a ficha-tecnica com a web.
- **O roteiro `ficha-basica.json`:** com o modelo falso, a proposta chega à folha do app com a origem de cada dado.

**Arquivos** (em `agente/`):
- Novos: `instrucoes/base.md`, `skills/*/SKILL.md` (6), `conhecimento/*.md` (5), `testes/roteiros/ficha-basica.json`, `tests/Agente.Testes/InstrucoesTestes.cs`.
- `tarefas/criar-memoria.md` e `tarefas/ficha-tecnica.md`: as skills em cada passo.
- `src/Agente.Servico/Lancadores.cs` (o evento, as ferramentas e a cópia das camadas), `ConfigServico.cs` (as ferramentas, validadas) e `ServicoAgente.cs` (o evento do gatilho).
- `src/Agente.ModeloFalso/*`: o `--evento` e a troca no corpo.
- `servico.config.json` e `README.md`.
- Testes: `LancadorTestes`, `RoteiroTestes`, `ServicoTestes` (a ficha de ponta a ponta), `Apoio` e `Bancada`.

**Revisão.** 38 achados: 6 medium, 28 low, 2 false e 2 maybe-false.
- **Patches:**
  - o teto de 30 s e o `criacao-interrompida`;
  - o evento e a web como dados;
  - a regra de "sem calibração" inteira;
  - o que o `/criacao/ajuste` recusa;
  - o "Tanto faz";
  - o resumo antes do "Como ficou?" no diagnóstico;
  - o ‹ no objetivo;
  - o prazo, o aviso e os outros grupos na ficha;
  - a unidade errada e o limite de 8 perguntas com fonte;
  - a promessa da cópia;
  - o frontmatter que o YAML aceita;
  - as ferramentas validadas;
  - o prompt que entra por último.
- **Diferidos** (no `deferred`): as permissões do usuário, o carregamento das skills no modo não interativo e o segundo pedido de ficha com um Agente rodando.
- **Rejeitados:** com o motivo no Review Triage Log.

**Revisão de acompanhamento: recomendada.** Três entradas medium foram corrigidas. O risco que nenhum teste confere é o comportamento do Claude Code de verdade com estas camadas: se as skills carregam, se ele respeita o teto de 30 s com raciocínios longos e se só usa a porta. Isso só aparece numa rodada real (14f ou 12e).

**Verificação:**
- `dotnet test agente/tests/Agente.Testes`: 117 de 117, depois dos patches.
- `dotnet test` Núcleo: 1365 de 1365. Rodou antes dos patches, que não tocam o app.
- A matriz tem pelo menos um teste por linha.
- As fontes do conhecimento foram conferidas à mão: as referências existem.

**Riscos:**
- os três diferidos;
- a janela de 60 s da ficha com uma busca na web de verdade;
- o diagnóstico no refino usa o "Como ficou?" como pergunta, o único caminho que a porta tem.
