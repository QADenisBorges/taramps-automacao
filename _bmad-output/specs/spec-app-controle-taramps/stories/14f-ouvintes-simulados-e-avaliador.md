---
title: 'História 14f: ouvintes simulados e avaliador'
type: 'feature'
created: '2026-11-01'
baseline_revision: '12832c0bfa9d98910d0b2e4edba1a0b77a3387db'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-agente-memoria/cerebro-do-agente.md'
  - '{project-root}/_bmad-output/specs/spec-agente-memoria/SPEC.md'
  - '{project-root}/agente/README.md'
  - '{project-root}/protocolo/MAPA.md'
warnings:
  - oversized
deferred:
  - summary: >-
      Os alvos fora de dB (os cortes em 80 Hz dos perfis de carro e de sala) contam só por igualdade, e o 80 Hz não está nos fatos do perfil: um Agente real razoável que escolha 90 Hz reprova.
    evidence: |-
      Os perfis e o Agente falso bom foram escritos juntos: o bom põe 80 Hz porque o código dele diz, e os fatos repetem as palavras das opções dele.
      O avaliador erra para o lado de reprovar (não aprova um Agente ruim), mas a avaliação real da 12e pode reprovar o Agente bom por um corte.
      Fecha com a decisão do Denis: uma tolerância para os alvos em Hz (por exemplo, um terço de oitava), tirar os cortes do gosto escondido, ou dizer o corte nos fatos.
    location: >-
      agente/avaliacao/perfis/
    severity: medium
---

<intent-contract>

## Intent

**Problem:** Nada prova que o Agente cria memórias como técnico, e não de forma genérica. Também nada barra uma mudança nas skills que piore o Agente (spec-agente-memoria CAP-8; `cerebro-do-agente.md`, "Ouvintes simulados e avaliação").

**Approach:** Um avaliador em .NET (`agente/avaliacao/`) com:
- 8 perfis de ouvinte com gosto escondido;
- o ouvinte simulado em código, que faz o lado do usuário de uma criação no núcleo do app sobre o `ProcessadorSimulado`;
- as métricas e o relatório;
- o comando da avaliação real, que mostra o custo antes e só roda com a confirmação;
- a conferência que barra camadas mudadas sem uma avaliação real que passou.

Nos testes, Agentes falsos em código: o bom passa; o genérico e o que supõe as entradas separadas falham.

## Boundaries & Constraints

**Always:**
- **Os perfis** (`agente/avaliacao/perfis/*.json`, 8 arquivos, só dados genéricos). Cada um traz:
  - um Setup (os equipamentos, as saídas e o ambiente: `sala`, `carro`, `ar-livre` ou `outro`), com as saídas calibradas para a cerca valer;
  - os fatos que o ouvinte sabe dizer em palavras (por exemplo, `{"objetivo":"o máximo que o kit aguenta","onde":"espalhados"}`);
  - o gosto escondido: os alvos `{codigo, indice, valor}` e a rota esperada por saída (`a`, `b` ou `a+b`);
  - as frases de refino (uma queixa e um gosto).

  Os 8 cobrem um carro, uma caixa bob ao ar livre, uma sala 2.1, um sistema de 2 vias ativo e o caso da SPL: nível máximo, posição indiferente e as entradas somadas (`a+b`) em todas as saídas com alto-falante. Ao menos dois perfis têm o mesmo Setup e gostos diferentes.
- **O ouvinte simulado** (código, sem modelo) faz o lado do usuário da `CriacaoMemoria` do núcleo, como os testes da 14a fazem:
  - **Pergunta:** responde com a opção que mais tem palavras em comum com os fatos (sem acento e sem maiúsculas). Sem nenhuma em comum, usa o "Outro…" com o fato mais perto do texto da pergunta.
  - **"1 ou 2":** escolhe a versão de menor distância aos alvos. Em distâncias iguais, "Tanto faz".
  - **"Como ficou?":** salva quando todo alvo está a até 1,5 dB. Senão, responde a opção mais perto da frase de refino, ou o "Outro…" com ela. Depois de 3 refinos, salva assim mesmo.
  - **O nome** é `AVAL <id do perfil>`.
- **As métricas**, por perfil:
  - **Perguntas:** quantas o Agente fez (as do `GET /criacao`, contando também as refeitas pelo ‹).
  - **Distância:** a maior diferença entre o valor final relido e o alvo, entre os alvos com unidade em dB. Um alvo em Hz, cm ou graus conta como acerto só se for igual.
  - **Violações:**
    - cada recusa da cerca (409 da cerca no `/criacao/ajuste`, no `/criacao/escuta` ou no `/ajuste`);
    - cada ajuste de tom (030 a 032, 090 a 092) seguido, na mesma rodada, de um ajuste de rota (010), ganho (080, 081), corte (040 a 043), atraso (050) ou fase (060);
    - cada valor final fora da cerca.
  - **Rota:** a rota final de cada saída com alto-falante igual à esperada.
  - **Passa** com distância ≤ 1,5 dB, perguntas ≤ 8, nenhuma violação e a rota certa.
  - **O conjunto passa** se todos os perfis passam.
- **O relatório** (`relatorio.json` e `relatorio.txt`, na pasta de saída) traz, por perfil: as perguntas (o texto e a resposta), a distância, as violações, a rota, o passou e o motivo. No topo: o passou geral, a data, o tipo (`falso` ou `real`) e o hash das camadas (o SHA-256 de `instrucoes/`, `skills/`, `conhecimento/` e `tarefas/`, pelos caminhos e pelo conteúdo, em ordem).
- **O comando** (`dotnet run --project agente/avaliacao/Agente.Avaliacao -- …`):
  - `real` mostra o custo estimado antes (US$ 1,50 por perfil, US$ 12 os 8) e só roda com `--confirmo-custo`. Sem ele, sai com o código 3 sem lançar nada.
  - Com a confirmação, para cada perfil:
    - sobe o núcleo e a porta sobre o simulador, numa pasta local temporária;
    - pareia;
    - lança o `LancadorClaude` com a tarefa `criar-memoria` e o evento;
    - deixa o ouvinte simulado responder.

    No fim, grava o relatório em `agente/avaliacao/relatorios/<data>/` (versionado).
  - `conferir` sai com o código 0 quando o último relatório real que passou tem o hash das camadas de agora. Senão, sai com 1 e o motivo ("As camadas mudaram desde a última avaliação real que passou." ou "Nenhuma avaliação real passou ainda."). É a trava: o README diz para rodar o `conferir` antes de levar uma mudança das camadas ao `main`. Instalar o gancho do git é decisão do Denis.
- **Os Agentes falsos** (em `tests/Agente.Testes`, como `ILancadorAgente` em código, falando só pela porta):
  - **O bom:** lê o `/estado` e o `/limites` e faz perguntas que dependem do Setup e do ambiente. Escolhe a rota pelo objetivo. Aplica na ordem da física, dentro da cerca, e refina pela escuta "1 ou 2" (cortando a dúvida pela metade) até 1,5 dB.
  - **O genérico:** faz as mesmas perguntas e a mesma receita para todos, sem escuta.
  - **O das entradas separadas:** é o bom com a rota sempre separada.
- **O avaliador** roda o lançador que recebe. Os testes usam os falsos; o comando `real` usa o `LancadorClaude`.

**Never:**
- Rodar o modelo de verdade (o comando `real` não roda nesta história, nem em teste).
- Um dado do sistema do Denis nos perfis (os `SemSegredoTestes` varrem `agente/`).
- Mudar o app (`app-controle/`).
- Instalar um gancho do git.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Bom passa | O Agente falso bom nos 8 perfis | Cada perfil passa (≤ 1,5 dB, ≤ 8 perguntas, sem violação, a rota certa); o conjunto passa | — |
| Genérico falha | O Agente falso genérico nos 8 | O conjunto falha; perfis falham pela distância ou pela rota | — |
| Entradas separadas | O falso das entradas separadas | Falha no perfil da SPL pela rota | — |
| Sequências diferentes | O bom, em dois perfis do mesmo Setup com gostos diferentes | As sequências de perguntas diferem | — |
| Primeiras perguntas | O bom num carro e numa sala | As respostas da primeira pergunta diferem | — |
| Violação da cerca | Um falso que pede o limitador acima do teto | A recusa conta como violação, e o perfil falha | — |
| Violação da ordem | Um falso que manda um EQ antes do ganho na mesma rodada | A violação de ordem conta, e o perfil falha | — |
| Ouvinte e o 1 ou 2 | Gosto 081[2] = +4; versões +2 e +5 | Escolhe a 2 | Distâncias iguais: "Tanto faz" |
| Custo antes | `real` sem `--confirmo-custo` | Mostra "US$ 12" e sai com 3; nenhum lançamento | — |
| Conferir | Relatório real que passou com o hash de agora / camadas mudadas / nenhum | 0 / 1 com o motivo / 1 com o motivo | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `agente/`, salvo onde diz outro lugar.

- `tests/Agente.Testes/Bancada.cs`: o núcleo e a porta sobre o `ProcessadorSimulado` numa porta livre, o pareamento, a `Criacao` (o lado do usuário). O avaliador precisa do mesmo fora dos testes: extraia o que for preciso para `avaliacao/Agente.Avaliacao` (ele pode referenciar o núcleo do app e o `BgsControle.Simulador`, como o `Agente.Testes`).
- `app-controle/src/BgsControle.Nucleo/Setups/CriacaoMemoria.cs`: o lado do usuário (`Responder`, `ResponderDeNovo`, `ResponderComoFicou`, `OuvirAsync`, `EscolherAsync` e o salvar), as etapas e a `Escuta`. `GerenteSetups` monta os Setups dos perfis (equipamentos, ambiente, calibração). `Rendimento/Cerca.cs` (`Conferir`) e o `/limites` dão a cerca.
- `app-controle/src/BgsControle.Nucleo/Porta/PortaComandos.cs`: as recusas da cerca (a mensagem "Limite: …") e o `GET /criacao` (as perguntas, os ajustes com a rodada).
- `protocolo/MAPA.md`: os códigos e as unidades (dB: 080, 081, 030, 091, 071; Hz: 040, 041, 031, 090; cm: 050; graus: 060; rota: 010).
- `src/Agente.Servico/Lancadores.cs` (`ILancadorAgente`, `LancadorClaude`) e `src/Agente.Porta/FerramentaPorta.cs` (os falsos em código falam pela porta).
- `tests/Agente.Testes/SemSegredoTestes.cs`, que já varre todo `agente/`.
- `BgsAgente.slnx`, onde entra o projeto novo.

## Tasks & Acceptance

**Execution:**
- `avaliacao/Agente.Avaliacao/` (novo projeto, no `BgsAgente.slnx`): os perfis (o modelo e a carga), o ouvinte simulado, as métricas, o relatório, o hash das camadas, os comandos `real` e `conferir`, e a montagem do núcleo sobre o simulador.
- `avaliacao/perfis/*.json` (8, novos).
- `avaliacao/relatorios/.gitkeep`.
- `README.md`: a seção da avaliação (os perfis, as métricas, o custo e o `conferir`).
- `tests/Agente.Testes/`: os três Agentes falsos e `AvaliacaoTestes.cs` (novo), com as linhas da matriz; a referência ao projeto novo no `.csproj`.

**Acceptance Criteria:**
- Given o avaliador com o Agente falso bom, when ele roda os 8 perfis, then o conjunto passa. Com o genérico, ele falha, e o das entradas separadas falha no perfil da SPL (CAP-8, com os falsos).
- Given os testes que já existem (Agente e Núcleo), when a 14f entra, then todos passam.

## Spec Change Log

- **01/11, no plano** (o Denis pediu "siga com recomendado"):
  - O ouvinte responde pelas palavras em comum com os fatos.
  - Os alvos fora de dB contam por igualdade.
  - A trava é o comando `conferir`, sem gancho do git.
  - O custo é US$ 1,50 por perfil (SPEC, Assumptions).

## Review Triage Log

### 2026-11-01 — Review pass
- verdicts: 42 findings — high 0, medium 8, low 34, false 0, maybe-false 0
- findings:
  - `[low]` `[reject]` Intenção: os testes provam o avaliador com Agentes falsos, não o Agente real — a avaliação real não roda nesta história (12e).
  - `[medium]` `[defer]` Intenção: os perfis e o Agente falso bom foram escritos juntos, e os cortes em Hz contam por igualdade sem estar nos fatos — no `deferred`, para a decisão do Denis.
  - `[low]` `[reject]` Intenção: o comando real não confere as sequências diferentes nem a reprovação do genérico — são critérios do conjunto de Agentes falsos, testados aqui; na avaliação real só há um Agente.
  - `[low]` `[reject]` Intenção: as escutas e os refinos não contam nas 8 perguntas, e o CAP-4 não tem métrica — o teto é de perguntas (CAP-2); a métrica da escuta não está no escopo da 14f.
  - `[low]` `[reject]` Intenção: a trava é um comando, não um bloqueio — escolha recomendada aceita pelo Denis (sem gancho do git).
  - `[low]` `[patch]` Intenção: o ramo do `real` confirmado nunca roda — teste de ponta a ponta com o Agente falso bom injetado.
  - `[medium]` `[patch]` Verification Gap: as reprovações por distância, por alvo fora de dB e por perguntas nunca reprovam sozinhas — testes no `Metricas.Calcular` (2 dB, 81 Hz contra 80, 9 perguntas).
  - `[medium]` `[patch]` Verification Gap: o limitador final fora da cerca e a recusa da escuta nunca viram violação num teste — testes do `ForaDaCerca` e do `RecusaDaCerca` nas duas formas.
  - `[medium]` `[patch]` Verification Gap: o `real` confirmado e o `conferir` nunca rodam pelo comando — o teste de ponta a ponta (relatório real, `conferir` 0, camada mudada 1).
  - `[medium]` `[patch]` Blind: o perfil que não sobe derruba a avaliação inteira e perde o relatório — a montagem dentro do `try`; o perfil falha com o motivo e a rodada segue.
  - `[low]` `[patch]` Blind: o `TrimEnd(')')` corta "(canal N))" — tira um só.
  - `[low]` `[patch]` Blind: um tempo esgotado interno vira "passou do prazo", e o código de saída do Agente some — o filtro pelo prazo e o código no motivo.
  - `[low]` `[reject]` Blind: o hash não cobre os perfis, as métricas e o modelo — a trava é das camadas do Agente (CAP-8); os perfis mudam pelo git, com revisão.
  - `[low]` `[patch]` Blind: uma rodada real mais nova que falhou não trava o `conferir` — decide pelo relatório real mais recente com o hash de agora.
  - `[low]` `[patch]` Blind: o hash lê qualquer arquivo do disco — só os `.md`, como a cópia das camadas.
  - `[low]` `[reject]` Blind: o relatório não traz o modelo, a duração e o custo real — o código de saída entrou; o resto é conforto para uma rodada que ainda não existe.
  - `[low]` `[reject]` Blind: a memória salva não é conferida — o perfil sem a memória salva já falha com o motivo.
  - `[low]` `[patch]` Blind: o "1 ou 2" soma dB, Hz e texto — compara primeiro os alvos fora de dB que erram e depois a maior distância em dB.
  - `[low]` `[reject]` Blind: a mesma pergunta repetida palavra por palavra não conta de novo, e o ‹ não é exercitado — é a regra das perguntas distintas; o ‹ é do usuário, não do ouvinte.
  - `[low]` `[reject]` Blind: o ouvinte junta as palavras de todos os fatos — é a regra escolhida (as palavras em comum), repetível; refinar é calibração para a 12e.
  - `[low]` `[patch]` Blind: um item nulo no perfil dá NullReferenceException — recusa com "arquivo: campo"; a faixa do valor fica fora (rejeitada).
  - `[low]` `[patch]` Blind: os alvos da SPL sem origem — a descrição diz que são o teto da cerca do app para aquele Setup.
  - `[low]` `[patch]` Blind: a porta livre pode colidir com 8 núcleos em paralelo — até 3 tentativas com porta nova.
  - `[low]` `[patch]` Blind: caminhos sem teste — os testes do Verification Gap cobrem a cerca, a recusa e a reprovação por alvo.
  - `[low]` `[reject]` Blind: o `Esperar` duplicado e a `Bancada` sem dispose na falha — só nos testes; sem defeito visível.
  - `[low]` `[reject]` Blind: o `real` sempre roda os 8 — a avaliação é do conjunto; um perfil só é conforto.
  - `[medium]` `[patch]` Edge: o `ProntoAsync` que falha derruba a rodada — o mesmo patch do Blind.
  - `[medium]` `[patch]` Edge: o ouvinte que falha relança no `finally` — vira o motivo "O ouvinte falhou (…).".
  - `[medium]` `[patch]` Edge: na rodada real, a espera depois do Agente (15 s) é menor que a do "Como ficou?" (2 min) — a espera cobre as duas.
  - `[low]` `[patch]` Edge: o código de saída diferente de 0 não reprova — vira o motivo.
  - `[low]` `[patch]` Edge: o tempo esgotado interno — o mesmo patch do Blind.
  - `[low]` `[patch]` Edge: a porta livre — o mesmo patch do Blind.
  - `[low]` `[patch]` Edge: o item nulo no perfil — o mesmo patch do Blind.
  - `[low]` `[reject]` Edge: o alvo fora da faixa do parâmetro — os 8 perfis estão dentro; um perfil novo passa por revisão.
  - `[low]` `[patch]` Edge: a config que falta dá o rastro da pilha — mensagem e código 2.
  - `[low]` `[patch]` Edge: o relatório sem permissão derruba o `conferir` — é pulado.
  - `[low]` `[patch]` Edge: os arquivos que não são das camadas mudam o hash — o mesmo patch do Blind.
  - `[low]` `[reject]` Edge: o relido nulo cai no estado de partida no "1 ou 2" — raro; a escrita sem eco já aparece no aviso da escuta.
  - `[low]` `[reject]` Edge: um ajuste sem texto nem número — a porta não aceita um ajuste assim.
  - `[low]` `[reject]` Edge: os ajustes depois do último retrato do ouvinte (40 ms) — raro; só no cancelamento pelo avaliador.
  - `[low]` `[reject]` Edge: a pasta temporária que não apaga em 5 tentativas — o Windows limpa o %TEMP%; a chave fica numa pasta temporária de teste.
  - `[low]` `[reject]` Edge (alegação): a trava não é imposta — o mesmo da intenção (escolha do Denis).

## Design Notes

- **Por que os Agentes falsos em código, e não roteiros:** o bom precisa ler o caso e convergir pela escuta, e um roteiro fixo não faz isso. Eles falam só pela porta, como o modelo.
- **Por que o ouvinte responde pelas palavras em comum:** as perguntas são do Agente, em texto livre. O ouvinte não pode usar modelo (custo e repetibilidade). As palavras dos fatos dão um gosto repetível.

## Verification

**Commands:**
- `dotnet test agente/tests/Agente.Testes` -- expected: todos passam.
- `dotnet run --project agente/avaliacao/Agente.Avaliacao -- real` -- expected: mostra o custo e sai com 3, sem lançar nada.
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam (o app não muda).

## Auto Run Result

Status: done

**O que entrou.** O avaliador do Agente (spec-agente-memoria CAP-8), em `agente/avaliacao/`:
- **8 perfis com gosto escondido:**
  - um carro com dois gostos;
  - uma caixa bob ao ar livre;
  - uma sala 2.1 com dois gostos;
  - um 2 vias ativo;
  - um quintal de conversa;
  - a SPL, com o limitador no teto e as entradas somadas.
- **O ouvinte simulado**, em código:
  - responde pelas palavras em comum com os fatos e pelo "Outro…";
  - escolhe no "1 ou 2" a versão mais perto do gosto;
  - salva no "Como ficou?" quando todo alvo está a até 1,5 dB, ou depois de 3 refinos.
- **As métricas:** as perguntas, a distância, as violações (as recusas da cerca, a ordem da física e o valor final fora da cerca) e a rota.
- **O relatório** em JSON e texto, com o hash das camadas.
- **O comando `real`**, que mostra o custo (US$ 12) e só roda com `--confirmo-custo`.
- **O comando `conferir`**, a trava das camadas pelo último relatório real.
- **Nos testes:** o Agente falso bom passa os 8. O genérico falha. O das entradas separadas falha na SPL (e nos outros perfis de entradas somadas).

**Arquivos** (em `agente/`):
- Novos:
  - `avaliacao/Agente.Avaliacao/*` (o avaliador, o ouvinte, as métricas, o relatório, os comandos e o `NucleoSimulado`);
  - `avaliacao/perfis/*.json` (8) e `avaliacao/relatorios/.gitkeep`;
  - `tests/Agente.Testes/AgentesFalsos.cs` e `AvaliacaoTestes.cs`.
- `tests/Agente.Testes/Bancada.cs` passou a usar o `NucleoSimulado`. Mudaram também o `.csproj` dos testes, o `BgsAgente.slnx` e o `README.md`.

**Revisão.** 42 achados: 8 medium e 34 low.
- **Patches:**
  - o avaliador robusto: o perfil que não sobe, o ouvinte que falha, o código de saída e o prazo;
  - a espera depois do Agente;
  - o `TrimEnd`;
  - o "1 ou 2" por unidade;
  - o hash só dos `.md` e o `conferir` pelo relatório real mais recente;
  - a config e o perfil inválidos com mensagem;
  - as tentativas de porta;
  - a origem dos alvos da SPL;
  - os testes das reprovações, da cerca, das recusas e do `real` confirmado de ponta a ponta.
- **Diferido:** a tolerância dos alvos em Hz, que é decisão do Denis.
- **Rejeitados:** com o motivo no Review Triage Log.

**Revisão de acompanhamento: recomendada.** Cinco entradas medium foram corrigidas. O risco que nenhum teste confere é o caminho do Claude no `real --confirmo-custo`, que nunca rodou. Ele usa o `LancadorClaude` e a `porta` publicada na pasta local de verdade, com o pareamento numa pasta temporária. Junto vai a calibração dos perfis contra um Agente real: os cortes em Hz e as palavras dos fatos. Os dois são da 12e.

**Verificação:**
- `dotnet test agente/tests/Agente.Testes`: 201 de 201, duas vezes seguidas, depois dos patches.
- `dotnet run --project agente/avaliacao/Agente.Avaliacao -- real`: mostra o custo e sai com 3, sem lançar nada.
- O app (`app-controle/`) não mudou. O Núcleo deu 1365 de 1365 na 14e, e esta história não o toca.
- A matriz tem pelo menos um teste por linha.

**Riscos:**
- o diferido;
- o `real` confirmado nunca rodou com o Claude;
- a `porta` precisa estar publicada em `%LOCALAPPDATA%\BgsAgente\bin` antes da avaliação real.
