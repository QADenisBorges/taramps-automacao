---
title: 'História 14e: aprendizado em arquivos'
type: 'feature'
created: '2026-11-01'
baseline_revision: '2c9d0cb7e7517639ae3ed0ec1ec9df220ec22700'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-agente-memoria/cerebro-do-agente.md'
  - '{project-root}/_bmad-output/specs/spec-agente-memoria/SPEC.md'
  - '{project-root}/agente/README.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** O Agente não aprende. Cada criação começa do zero, mesmo quando o usuário já escolheu o mesmo grave em duas memórias de música (spec-agente-memoria CAP-7; `cerebro-do-agente.md`, "Aprendizado").

**Approach** (decisões do Denis, 01/11/2026: grava o serviço; a situação, quem diz é o Agente):
- O serviço, em código, grava um registro por criação e aplica a regra do perfil. O perfil só muda com a mesma escolha repetida em memórias diferentes.
- O Agente diz a situação com as palavras do usuário e as variáveis da criação por uma ferramenta local nova, `aprendizado`. Pela mesma ferramenta, ele lê o perfil da situação como ponto de partida.
- Tudo fica na pasta local, fora do repositório, e começa vazio.

## Boundaries & Constraints

**Always:**
- **A ferramenta `aprendizado`** (`src/Aprendizado.Cli`, executável `aprendizado`, como a `porta`): JSON na entrada e um JSON numa linha na saída; código 0 no ok e 1 na recusa. Grava só na pasta local (`--local`, `AGENTE_LOCAL` ou `%LOCALAPPDATA%\BgsAgente`). As ações:
  - `{"acao":"situacao","nome":"música"}` guarda a situação da criação de agora. O nome é texto livre, de 1 a 40 caracteres. A chave é o nome em minúsculas, sem acento, sem espaço nas pontas e com os espaços internos colapsados.
  - `{"acao":"variaveis","valores":{"grave":4.0,"cobertura":"varios"}}` junta as variáveis da criação de agora (número ou texto; uma chamada nova troca o valor da mesma variável).
  - `{"acao":"outro","texto":"som de show","variavel":"grave"}` liga um "Outro…" a uma variável conhecida.
  - `{"acao":"perfil","situacao":"música"}` devolve `{"situacao":"musica","variaveis":{…},"outros":[…]}`. Sem perfil, devolve as listas vazias.
  - Tudo da criação de agora vai para `aprendizado/agora.json`. Um JSON inválido, uma ação desconhecida, um nome vazio ou um valor que não é número nem texto dão uma recusa com o motivo.
- **O registro** (o serviço, em `ServicoAgente`): no fim de cada criação (`criacao-salva`, `criacao-cancelada` ou `criacao-interrompida`, vistos no `/eventos`), o serviço grava `aprendizado/registros/<hora UTC>-<n>.json` com:
  - o fim (salva, cancelada ou interrompida) e, se salva, o id e o nome da memória (do evento);
  - o diálogo resumido: as perguntas, com as respostas e o `doOutro` (do `GET /criacao` lido antes do fim, guardado a cada leitura);
  - o proposto × o que ficou: os ajustes de cada rodada (código, índice, antes, valor e relido), mais o `somAntes`;
  - as escolhas do "1 ou 2" (`criacao-escuta`) e as respostas do "Como ficou?" (`criacao-como-ficou`: o retorno positivo é `salvar:true`; o negativo é o texto do refino);
  - o retorno sem palavras: o número de rodadas de refino e se cancelou;
  - o contexto: a hora do fim, e o `nivel` e o `master` da memória em uso no `/estado`;
  - a situação, as variáveis e os outros de `agora.json`.

  Depois, `agora.json` é apagado. Uma criação sem o `GET /criacao` lido grava o registro com o que tiver, sem falhar.
- **O ajuste à mão depois:** quando uma criação nova abre, se a memória em uso tem um registro salvo, o serviço compara o `somAntes` do `GET /criacao` com o que aquele registro salvou. Cada diferença maior que 0,05 entra no registro antigo como `ajustesDepois`, com o código, o índice, o valor salvo e o valor de agora.
- **O perfil** (`aprendizado/perfis/<chave>.json`): depois de gravar um registro salvo, o serviço refaz o perfil da situação dele.
  - Só contam os registros salvos.
  - Para cada variável, o perfil a guarda quando dois ou mais registros dessa situação, de memórias diferentes (ids diferentes), têm a mesma escolha: números a até 1 dB um do outro (o valor é a média deles) ou o mesmo texto.
  - Com mais de um grupo, vale o grupo com mais memórias, e no empate o mais recente.
  - Uma memória que repete a mesma escolha conta uma vez só.
  - O perfil de uma situação nunca muda com registros de outra.
  - Os `outros` ligados entram como opção só quando têm `variavel`.
- **A leitura pelo Agente:** as skills objetivo e receita ganham `Bash(aprendizado:*)` no `allowed-tools`.
  - A objetivo, depois da resposta que diz a situação, chama `situacao` e `perfil` e usa o perfil como ponto de partida das variáveis. A memória manda, e o perfil é só o padrão, com a dúvida dele.
  - A receita chama `variaveis` antes do resumo.
  - A tarefa `criar-memoria` e a base citam a ferramenta. As `ferramentas` do `"*"` no `servico.config.json` passam a `Bash(porta:*) Bash(aprendizado:*)`.
  - A regra "só a porta" continua para o app; a `aprendizado` só toca a pasta local.
  - Os `InstrucoesTestes` aceitam essa ferramenta nas duas skills, e só nelas.
- **O modelo falso** ganha o passo `aprender` no roteiro: `{"aprender": {"acao":"situacao","nome":"música"}}` roda a ferramenta `aprendizado` com o JSON e ramifica pelo resultado, como o `pedir`.
- **Começa vazio:** sem a pasta `aprendizado/`, tudo funciona, e o `perfil` volta vazio. Nada do aprendizado vai para o repositório. Os testes usam a pasta local temporária.
- O README explica a ferramenta, os registros e a regra do perfil.

**Never:**
- O Agente gravar registros ou perfis por outro caminho, ou a regra do perfil no modelo.
- Os ouvintes simulados e o avaliador (14f); um erro real virando caso de teste é da 14f.
- Mudar o app (`app-controle/`).
- Chamar o modelo de verdade.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Duas de música | 2 criações salvas (memórias A e B), situação "música", grave 4,0 e 4,6 | O perfil "musica" tem grave 4,3; o "filme" não muda | — |
| Uma só | 1 criação salva, "música", grave 4,0 | Nenhum perfil muda; o registro existe | — |
| Mesma memória | 2 criações salvas na mesma memória A, grave 4,0 | O perfil não muda (uma memória só) | — |
| Escolhas diferentes | Memória A grave 4,0 e memória B grave 6,0 | O perfil não muda (a mais de 1 dB) | — |
| Cancelada | Criação cancelada com a situação "música" | Registro com o fim "cancelada"; o perfil não conta | — |
| Começa vazio | Sem a pasta `aprendizado/` | O `perfil` devolve as listas vazias, com ok | — |
| Ferramenta inválida | `{"acao":"x"}`, o nome vazio, o JSON quebrado | Recusa com o motivo, código 1 | — |
| Ajuste à mão | A memória salva tem o 081[2] = 4,0; ao abrir a próxima, o `somAntes` tem 5,5 | O registro antigo ganha `ajustesDepois` com o 081[2], de 4,0 para 5,5 | — |
| Ponta a ponta | Serviço com o modelo falso: 2 criações salvas pelo lado do usuário em memórias diferentes, com o roteiro que declara "música" e grave 4 | 2 registros e o perfil "musica" com grave 4 | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `agente/`.

- `src/Agente.Servico/ServicoAgente.cs`: o laço dos eventos (≈196), onde os fins da criação e a abertura são vistos; `RodarAgenteAsync` (≈275); `FerramentaPorta.PedirAsync` para o `GET /criacao` e o `/estado`; o `_pastaLocal`. O aprendizado fica numa classe nova, `src/Agente.Servico/Aprendizado.cs` (o registro, o perfil e o `agora.json`), usada pelo serviço e pela ferramenta.
- `src/Porta.Cli/Program.cs` e `src/Agente.Porta/FerramentaPorta.cs` são o modelo da ferramenta nova (`src/Aprendizado.Cli`, no `BgsAgente.slnx`, publicada ao lado da `porta`, com o nome `aprendizado`). `PastaLocal` (`PareamentoLocal.cs`) resolve a pasta local.
- `src/Agente.ModeloFalso/Roteiro.cs` (o `Passo`, com a validação "um, e só um") e `ExecutorRoteiro.cs` (o `pedir` e o `Ramificar`): entra o `aprender`. `FerramentaProcesso.cs` roda a `porta`; a `aprendizado` roda do mesmo jeito.
- `skills/objetivo/SKILL.md`, `skills/receita/SKILL.md`, `tarefas/criar-memoria.md`, `instrucoes/base.md` (até 40 linhas não vazias), `servico.config.json` e `README.md`.
- Testes, em `tests/Agente.Testes/`:
  - `InstrucoesTestes.cs` (`As_ferramentas_de_cada_skill_…`, que muda);
  - `ServicoTestes.cs` e `Bancada.cs` (a criação de ponta a ponta com o lado do usuário: `bancada.Criacao.Responder…` e o salvar, como na 14a);
  - `RoteiroTestes.cs`;
  - `testes/roteiros/` (os roteiros novos, com dados genéricos).
- No app (só leitura): `PortaComandos.TextoAjuda` (o `GET /criacao` com o `somAntes` e os ajustes, o `criacao-salva` com a `memoria` e o `/estado` com as memórias, o `nivel` e o `master`).

## Tasks & Acceptance

**Execution:**
- `src/Agente.Servico/Aprendizado.cs` (novo): o `agora.json`, o registro, os `ajustesDepois` e o perfil, com a regra.
- `src/Aprendizado.Cli` (novo projeto) e o `BgsAgente.slnx`.
- `src/Agente.Servico/ServicoAgente.cs`: guardar o último `GET /criacao` durante a criação, gravar no fim e conferir os ajustes à mão na abertura.
- O modelo falso: o passo `aprender`. Os roteiros novos (`testes/roteiros/aprende-musica.json`, ou o `criacao-basica` com os passos `aprender`).
- As skills objetivo e receita, a tarefa, a base, o `servico.config.json` e o README.
- Testes: `AprendizadoTestes.cs` (novo), com as linhas da matriz na regra e na ferramenta; `ServicoTestes` (o ponta a ponta e o ajuste à mão); `RoteiroTestes` (o `aprender`); `InstrucoesTestes` (as ferramentas).

**Acceptance Criteria:**
- Given duas criações de música salvas em memórias diferentes com o mesmo grave, when a segunda termina, then o perfil "musica" muda e o de "filme" não; depois de uma só, nada muda (CAP-7).
- Given os testes que já existem (Agente e Núcleo), when a 14e entra, then todos passam.

## Spec Change Log

- **01/11, antes da implementação:** a primeira rodada parou por lacuna de intenção (quem grava e o que é a situação). O Denis escolheu o recomendado nos dois pontos e nos menores: o ajuste à mão é visto na criação seguinte; o contexto vem da hora, do nível e do master; "mesma escolha" é a até 1 dB.

## Review Triage Log

### 2026-11-01 — Review pass
- verdicts: 40 findings — high 0, medium 6, low 34, false 0, maybe-false 0
- findings:
  - `[low]` `[reject]` Intenção: o "aprende" (a criação seguinte partir do perfil) só está no texto das skills — é comportamento do modelo; a prova é a avaliação real (14f).
  - `[low]` `[reject]` Intenção: a escolha que conta é a que o Agente declara, não as respostas nem o "1 ou 2" — é a decisão do Denis (o Agente diz as variáveis; o código aplica a regra).
  - `[low]` `[reject]` Intenção: as janelas de 1 dB se sobrepõem e uma memória nova pode mover o perfil — é a regra da spec (o grupo com mais memórias, no empate o mais recente).
  - `[low]` `[reject]` Intenção: os ajustes à mão ficam registrados mas não entram no perfil — a spec os pede no registro; usá-los na regra seria uma decisão nova.
  - `[low]` `[reject]` Intenção: a spec fala do `somAntes`, e o código compara com o `/estado` — o `somAntes` da porta é só o nome; o README diz o comportamento real; corrigir o texto da spec não é patch.
  - `[low]` `[patch]` Intenção: a memória pelo nome quando falta o id — os registros sem id não contam, e o ajuste à mão sem id dá 0.
  - `[medium]` `[patch]` Blind: a mesma memória conta duas vezes (uma pelo id, outra pelo nome) — os registros sem id não contam (com teste).
  - `[medium]` `[patch]` Blind: uma memória salva de novo com outra escolha ainda vota pela antiga — só o registro mais recente de cada memória vota (com teste).
  - `[medium]` `[patch]` Blind: um "Outro…" ligado entra no perfil com uma criação só — só com duas memórias diferentes (com teste).
  - `[low]` `[reject]` Blind: a tolerância de 1 dB vale para qualquer variável numérica — é a regra da spec ("mesma escolha" a até 1 dB), escolhida pelo Denis.
  - `[low]` `[patch]` Blind: o /criacao lido antes do /estado pode tomar um ajuste do Agente por ajuste à mão — o /estado é lido primeiro.
  - `[low]` `[reject]` Blind: a última rodada pode ficar de fora num salvar rápido — o salvar leva segundos depois do resumo (o "Como ficou?" e a tela do Salvar) e o serviço lê a cada 1 s; o conserto pede ler de novo no fim.
  - `[low]` `[reject]` Blind: o contexto lido tarde depois de uma queda, e a memória pelo nome — raro; o nome como chave saiu da regra.
  - `[low]` `[patch]` Blind: uma exceção fora do filtro reprocessa o lote e duplica o registro — os dois blocos pegam tudo, menos o cancelamento, e vão ao log.
  - `[low]` `[reject]` Blind: duas chamadas da ferramenta ao mesmo tempo perdem uma escrita do `agora.json` — uma trava de arquivo é complexidade nova para um caso raro.
  - `[low]` `[patch]` Blind: o apóstrofo do usuário quebra o `--json '…'` no shell — as skills e a tarefa mandam o texto do usuário pelo stdin, com heredoc entre aspas.
  - `[low]` `[patch]` Blind: as skills não dizem o que fazer na recusa da ferramenta — dizem: o motivo vem junto; corrija e mande de novo, sem repetir a mesma chamada.
  - `[low]` `[reject]` Blind: o `outro` aceita qualquer variável — a ligação é juízo do modelo; o perfil agora exige duas memórias.
  - `[low]` `[reject]` Blind: o padrão do código nas ferramentas é só a porta — a config versionada é a fonte, e o padrão "só a porta" é testado de propósito (14d).
  - `[low]` `[reject]` Blind: os desvios da spec não estão no Spec Change Log — editar a spec não é patch; vão no Auto Run Result.
  - `[low]` `[reject]` Blind: o apoio dos testes repete o fim do serviço — os testes de ponta a ponta passam pelo serviço de verdade.
  - `[low]` `[reject]` Blind: o teste contra o Agente gravar registros é fraco — o Agente só tem a porta e a `aprendizado` no allowed-tools.
  - `[low]` `[patch]` Blind: caminhos do serviço sem teste — o cancelamento sem dono e o `agora.json` que sobrou ganharam teste (pelo Verification Gap).
  - `[low]` `[reject]` Blind: o `--ajuda` não sai em JSON — é para quem lê no terminal, não para o modelo.
  - `[low]` `[reject]` Blind: a ferramenta depende da biblioteca do serviço — separar é refatoração sem defeito.
  - `[low]` `[patch]` Verification Gap: o registro do cancelamento sem dono não é conferido — o teste confere o registro "cancelada", "porta", com `cancelou`.
  - `[low]` `[patch]` Verification Gap: o `agora.json` que sobrou não é testado — teste novo: o registro sai com "musica" e sem o `agudo`.
  - `[low]` `[patch]` Edge: uma chave repetida no JSON lança em vez de recusar — recusa com o motivo (com teste).
  - `[low]` `[patch]` Edge: "Grave" e "grave" na mesma chamada se fundem — recusa (com teste).
  - `[low]` `[reject]` Edge: a corrida no `agora.json` — o mesmo do Blind.
  - `[low]` `[reject]` Edge: o disco cheio no meio do registro deixa um arquivo pela metade — raro; o conserto pede limpeza e um ramo novo.
  - `[low]` `[patch]` Edge: o filtro das exceções aborta o ciclo — o mesmo patch do Blind.
  - `[low]` `[patch]` Edge: o `ToDictionary` quebra com um `ajustesDepois` repetido — pelo indexador (com teste).
  - `[medium]` `[patch]` Edge: a mesma memória com e sem id conta duas vezes — o mesmo patch do Blind.
  - `[low]` `[patch]` Edge: o ajuste à mão pelo nome pode cair numa memória de outro Setup — sem id em uso, nada (com teste).
  - `[low]` `[reject]` Edge: a última rodada entre duas leituras — o mesmo do Blind.
  - `[low]` `[reject]` Edge: o app que reinicia no meio perde o `criacao-interrompida` — sem o evento não há registro; a próxima criação recomeça o acompanhamento.
  - `[low]` `[reject]` Edge: a hora do computador misturada com a do celular nos nomes — só sem a hora no evento, que a porta sempre manda.
  - `[medium]` `[patch]` Edge (alegação): o "Outro…" muda o perfil com uma criação só — o mesmo patch do Blind.
  - `[medium]` `[patch]` Edge (alegação): uma memória conta como duas — o mesmo patch do Blind.

## Design Notes

- **Por que o serviço grava:** a regra precisa sair sempre igual, e o modelo falso precisa exercitá-la. O modelo só nomeia a situação e diz as variáveis, que são linguagem e juízo.
- **Por que a chave sem acento:** "Música", "musica" e "música " são a mesma situação. As palavras são do usuário, sem lista fixa.

## Verification

**Commands:**
- `dotnet test agente/tests/Agente.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam (o app não muda).

## Auto Run Result

Status: done

**O que entrou.** O aprendizado em arquivos (spec-agente-memoria CAP-7), com as decisões do Denis de 01/11 (o serviço grava; a situação, quem diz é o Agente):
- **A ferramenta `aprendizado`** (`src/Aprendizado.Cli`): o Agente diz a situação com as palavras do usuário, as variáveis e os "Outro…" ligados, e lê o perfil. Só toca a pasta local.
- **O registro de cada criação**, gravado pelo serviço no fim (salva, cancelada ou interrompida). Traz:
  - o diálogo;
  - o proposto × o que ficou;
  - o "1 ou 2" e o "Como ficou?";
  - o retorno sem palavras;
  - o contexto: a hora, o nível e o master.
- **Os ajustes à mão**, conferidos quando a criação seguinte abre: entram como `ajustesDepois` no registro da memória em uso.
- **O perfil por situação**, que só muda com a mesma escolha (a até 1 dB, ou o mesmo texto) em duas ou mais memórias diferentes:
  - só o registro mais recente de cada memória vota;
  - os registros sem o id da memória não contam;
  - os "Outro…" também pedem duas memórias.
- **As skills objetivo e receita** usam a ferramenta, com o texto do usuário pelo stdin.
- **O passo `aprender`** no modelo falso.
- Tudo começa vazio e fica fora do repositório.

**Desvios da spec** (a porta não traz o que o texto supunha):
- o `somAntes` do `GET /criacao` é só o nome, então o valor de agora vem do `/estado` (os `noFio`);
- o `criacao-salva` não traz o id, que vem do `/estado`, pelo nome.

**Arquivos** (em `agente/`):
- Novos: `src/Agente.Servico/Aprendizado.cs`, `src/Aprendizado.Cli/*`, `testes/roteiros/aprende-musica.json` e `tests/Agente.Testes/AprendizadoTestes.cs`.
- `src/Agente.Servico/ServicoAgente.cs`: o acompanhamento, o registro e o perfil.
- `src/Agente.ModeloFalso/*`: o passo `aprender`.
- As skills objetivo e receita, `tarefas/criar-memoria.md`, `instrucoes/base.md`, `servico.config.json`, `README.md` e `BgsAgente.slnx`.
- Testes: `ServicoTestes`, `RoteiroTestes`, `InstrucoesTestes` e `LancadorTestes`.

**Revisão.** 40 achados: 6 medium e 34 low.
- **Patches:**
  - a memória contada duas vezes;
  - o voto só do registro mais recente;
  - os "Outro…" com duas memórias;
  - a ordem /estado, depois /criacao;
  - o filtro das exceções;
  - a chave repetida e "Grave"/"grave";
  - o `ToDictionary`;
  - o ajuste à mão sem id;
  - o heredoc e a recusa nas skills;
  - os testes do cancelamento sem dono e do `agora.json` que sobrou.
- **Nada diferido.**
- **Rejeitados:** com o motivo no Review Triage Log.

**Revisão de acompanhamento: recomendada.** Três entradas medium da regra do perfil foram corrigidas. O risco que nenhum teste confere é a regra com o uso de verdade: as janelas de 1 dB que se sobrepõem e o grupo que vence no empate. Isso aparece com os ouvintes simulados (14f).

**Verificação:**
- `dotnet test agente/tests/Agente.Testes`: 171 de 171, duas vezes seguidas, depois dos patches. Antes deles, uma rodada (a que também compilou) teve 1 falha que não se repetiu em 5 rodadas.
- `dotnet test` Núcleo: 1365 de 1365. Rodou antes dos patches; o app não muda.
- A matriz tem pelo menos um teste por linha.

**Riscos:**
- o teste instável do Agente, não identificado;
- a última rodada num salvar muito rápido (rejeitado como raro);
- o comportamento do modelo real com a ferramenta (14f e 12e).
