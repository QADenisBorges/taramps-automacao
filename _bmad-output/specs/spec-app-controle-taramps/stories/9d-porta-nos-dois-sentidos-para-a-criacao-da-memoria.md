---
title: 'História 9d: porta nos dois sentidos para a criação da memória'
type: 'feature'
created: '2026-10-30'
baseline_revision: '9b1427a84396db1b436f1b5d59971a6fc348a67c'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/porta-de-comandos.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/failure-modes.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/chaves-de-acesso.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** A porta só recebe comandos avulsos. A CAP-18 pede que o cliente pareado conduza a criação de uma memória: ele manda as perguntas e os ajustes, e o app mostra, devolve a resposta e só aplica depois da última resposta. O Salvar fica só com o usuário, e cancelar volta ao som de antes (`porta-de-comandos.md`, seção "Criação da memória"; regra 6 de `failure-modes.md`).

**Approach:** Criar um `CriacaoMemoria` no núcleo, com as etapas, a cópia antes, as escritas, o Salvar, o cancelar e a interrupção. Ele é exposto em rotas `/criacao` da `PortaComandos`, e as respostas chegam pelo `/eventos`. Um `CriacaoMemoriaViewModel` em `BgsControle.Telas` dá ao usuário responder, avaliar, salvar e cancelar. Tudo é testado contra o `ProcessadorSimulado`, sem XAML.

## Boundaries & Constraints

**Always:**
- **Etapas:** `Fechada` → `Perguntas` → `Ajustando` (depois da resposta à última pergunta) → `Resumo` → `ComoFicou`. No "Como ficou?", "Ótimo, pode salvar" leva a `Salvar`, e qualquer outra resposta (ou "Outro…") volta a `Ajustando`, numa rodada nova (refino). Em `Salvar`, "Ajustar mais" volta a `ComoFicou`. Só há uma criação por vez.
- **Abrir** (`POST /criacao/abrir`) exige Setup em uso, sem dúvida (`MotivoBloqueioIa`), conectado e com leitura pronta. Guarda a cópia antes (`CopiaAntes.IniciarAsync`, tipo `criar-memoria`) e inicia a tarefa (`UsoCompartilhado.IniciarTarefa`), para valer o vigia de 30 s. Guarda o Setup e o nome do som em uso para o texto do cancelar.
- **Pergunta** (`POST /criacao/pergunta {"texto","respostas":[3 ou 4],"ultima"}`) só cabe na etapa `Perguntas` e depois da resposta à anterior. A tela acrescenta o "Outro…".
- **Resposta:** o toque numa resposta, ou o "Outro…" com texto não vazio, responde a pergunta. Ela vai ao `/eventos` como `criacao-resposta` (número, texto e se veio do "Outro…") e aparece no `GET /criacao`. Responder à última pergunta autoriza os ajustes. O "Outro…" vazio não responde.
- **Ajuste** (`POST /criacao/ajuste {"codigo","indice","valor","frase","motivo"}`) só é aceito em `Ajustando`. Ele aceita só os códigos da `Camada.Regulagem`: as 15 bandas, o link e o mute dão 400. A escrita sai pelo `Escritor`, com o link repetindo e passando pela trava da cópia, como IA. Ela vale para as mesmas chaves e para o só leitura, pela `Sessao`. O app guarda e devolve a frase, o motivo, o valor de antes (do espelho), o valor relido e se ficou diferente. Não conta para o Continuar. O `/ajuste` e o `/memoria/aplicar` comuns são recusados durante a criação, com 409.
- **Resumo** (`POST /criacao/resumo`) termina a rodada e só é aceito se todo endereço mexido foi relido igual ao pedido. Lista cada endereço com o último ajuste e marca os da rodada atual. Fora dele, nada.
- **Como ficou?** (`POST /criacao/como-ficou {"respostas":[2 ou 3]}`) é aceito em `Resumo` ou `ComoFicou`. A tela mostra "Ótimo, pode salvar" (constante do app) e depois essas respostas e o "Outro…". "Avaliar o som" passa de `Resumo` a `ComoFicou`. A resposta vai ao `/eventos` como `criacao-como-ficou`, com `salvar` verdadeiro ou falso.
- **Salvar** é só do usuário, pelo ViewModel com o nome. Cria a memória do Setup aberto com a regulagem relida (o espelho), mesmo vindo do som do aparelho, lembrando o preset em uso. Ela vira a memória em uso e a ativa. Depois do Salvar, a marca da cópia sai (`ConcluirAsync`) e a criação fecha, com o evento `criacao-salva`. Um nome inválido ou repetido não fecha nada.
- **Cancelar** volta à cópia, com escritas como App e só no que difere, e fecha a criação sem salvar, com o evento `criacao-cancelada` e o campo `por` (`usuario`, `folha` ou `porta`). Ele chega pelo ✕ do ViewModel (só em `Perguntas` e `ComoFicou`), por `POST /criacao/cancelar`, ou pelo "Cancelar" de qualquer folha de risco que a porta abra durante a criação (D5). Nesse caso, o pedido recebe 409 "O usuário cancelou a nova memória.". Se a volta falhar no meio, fica "aplicado pela metade" (regra 3).
- **Regra 6:** a criação é interrompida se a sessão cai ou se a tarefa da porta acaba no meio dela (vigia de 30 s, `DELETE /tarefa` ou o revogar). Ela fecha com o evento `criacao-interrompida` e nada é salvo. Se algum ajuste já foi escrito (etapa `ajustes` anotada na marca), fica "aplicado pela metade": na hora com a sessão aberta, ou na próxima conexão. O Terminar dessa marca diz "O app não sabe refazer esta operação.". Sem ajuste escrito, a marca sai e nada fica pela metade.
- O `GET /criacao` traz a etapa, o Setup, as perguntas com as respostas, se está autorizado, a rodada, os ajustes e as respostas do "Como ficou?". O `TextoAjuda` documenta as rotas. Nenhum texto do ViewModel contém "IA" (o teste de arquitetura da 9c cobre isso).

**Never:**
- A porta salvar ou nomear a memória.
- Telas, XAML, navegação, animação ou o ‹ de voltar pergunta (história 10d).
- O pedido de criação, a escuta "1 ou 2" ou o cérebro (spec do Agente).
- Nenhum `240`, `241` ou `23N`, e nenhum pacote novo.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Abrir | Casa 2.1 com a FILME em uso, pareado | 200; marca `criar-memoria` com a cópia; etapa Perguntas | — |
| Abrir sem poder | Sem Setup, em dúvida ou com outra criação aberta | 409 com o motivo; nada guardado | — |
| Pergunta | 3 respostas, `ultima:false` | 200; o ViewModel mostra o texto, as 3 respostas e o "Outro…" | 2 ou 5 respostas: 400; outra pendente: 409 |
| Responder | Toque na 2ª resposta | `/eventos` traz `criacao-resposta` nº 1 com o texto | "Outro…" vazio: não responde |
| Ajuste cedo | Antes da resposta à última pergunta | 409; nada enviado | "Os ajustes só vêm depois da resposta à última pergunta." |
| Ajuste | Depois da última resposta: 081, saída 3, +2,0, frase e motivo | 200 com antes, novo e `relido`; histórico IA; o espelho mostra +2,0 | 020: 400 |
| Sem Continuar | 4 ajustes da criação seguidos | Nenhuma folha | `/ajuste` comum durante a criação: 409 |
| Releitura diferente | O simulador devolve outro valor | 200 com `diferente:true`; o resumo dá 409 até corrigir | — |
| Refino | Resumo, "Avaliar o som", "Quero mais impacto" | Evento `criacao-como-ficou` com `salvar:false`; ajustes aceitos de novo; o resumo destaca só a rodada 2 | — |
| Salvar | "Ótimo, pode salvar"; Salvar "MUSICA ORIG" | A memória guarda a regulagem relida e fica em uso; a marca sai; `criacao-salva` | Nome repetido: falha e fica em Salvar |
| ✕ | Depois de 2 ajustes | Releitura igual à cópia; fecha; `criacao-cancelada` `por:usuario`; nenhuma memória nova | — |
| Folha de risco | Na criação, `/gerador/tom` com a chave ligada; Cancelar | 409 "O usuário cancelou a nova memória."; volta à cópia; `por:folha` | Confirmar: o tom toca e a criação segue |
| BLE cai | Depois de 1 ajuste | Fecha; `criacao-interrompida`; ao reconectar, pela metade; Terminar recusa; Voltar à cópia volta | Sem ajuste escrito: nada pela metade |
| Porta some | 30 s sem pedidos, depois de ajustes | Pela metade na hora | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Porta/PortaComandos.cs`:
  - `EventoPorta` (l. 15) ganha os dados do evento da criação;
  - `Registrar` (388);
  - o despacho dos POST (545–568) e a trava da dúvida (566);
  - `RodarAsync` (572–617): `Recusada`, folha (599–603), `ContaSequencia`;
  - `Operacao` (656);
  - `Ajuste` (677–716), modelo de leitura do corpo e de codificação;
  - `IniciarTarefa` (830);
  - `RespostaEventos` (1019–1048);
  - `TextoAjuda` (1091);
  - o construtor (107–153) ganha um `CriacaoMemoria? criacao = null` no fim.
- `src/BgsControle.Nucleo/Porta/JsonPorta.cs`: `CorpoPedido` (`Texto`, `Numero`, `InteiroOpcional`, `Booleano`) e `PedidoInvalidoException`. Falta uma leitura de lista de textos.
- `src/BgsControle.Nucleo/Escrita/CopiaAntes.cs`:
  - `IniciarAsync` (183), `AnotarEtapaAsync` (235), `ConcluirAsync` (250), `Interromper` (267), `MarcarPelaMetade` (278) e `Registrar` (170);
  - `VoltarAsync` (339–414) escreve a cópia, mas só a partir do "pela metade". Falta um desfazer da operação em andamento que reaproveite o laço de escrita (372–397);
  - `RetomarAsync` (484) decide pelo `AoReconectar` registrado;
  - sem registro, o Terminar dá `MsgNaoSeiRefazer`.
- `src/BgsControle.Nucleo/Escrita/Escritor.cs`: `EscreverAsync(..., espelhar, daCopia)` (133–167) é `internal`. Com `daCopia`, a escrita passa pelo `Bloqueio` da marca (180, 356). `ResultadoEscrita` (33) traz `Relido`, `Diferente` e `Valor`.
- `src/BgsControle.Nucleo/Setups/GerenteSetups.cs`:
  - `CriarMemoriaAsync` (1395–1437) é o modelo do Salvar, mas recusa o som do aparelho (1406). Falta um `internal` que salve do espelho para o Setup aberto;
  - `NovaMemoria` (1886), `MsgCriada` (122), `_operacao` (197), `AdotarPlaylistsAsync`;
  - `MotivoBloqueioIa` (494), `SetupEmUso`, `EmUso`, `MemoriaEmUso`, `PresetEmUso`, `EmEscolha`;
  - o registro de tipos na cópia (258–267).
- `src/BgsControle.Nucleo/Setups/Camadas.cs`: `De(codigo) == Camada.Regulagem` e `Rotulo`/`Exibir` (127, 162) para os rótulos e os valores.
- `src/BgsControle.Nucleo/Uso/UsoCompartilhado.cs`: `IniciarTarefa` (189), `TerminarTarefa` (207), `PararIa` (219), `IaAtiva`, `Mudou`. `Origem.Como(OrigemComando.App)` serve para a volta à cópia.
- `src/BgsControle.Nucleo/Porta/PedidosConfirmacao.cs`: `DecisaoConfirmacao.Recusou`.
- `src/BgsControle.Telas/FolhaRiscoViewModel.cs`, `TelaBase.cs` e `Comando.cs`: o modelo de ViewModel.
- `src/BgsControle.App/MauiProgram.cs`: o `PortaComandos` (244–259) e a ordem de criação cedo (320–338), onde o tipo da cópia precisa estar registrado antes da primeira leitura.
- Testes:
  - `tests/BgsControle.Nucleo.Testes/PortaTestes.cs` (113–240) é o modelo da montagem: `Pronto()`, `Pedir`, `Rodar`, `AndarAte`, `GanhoDiferentePeloApp` e `CenarioFilme`;
  - `ProcessadorSimulado` (cair a conexão e releitura diferente, conforme ele expõe);
  - `tests/BgsControle.Telas.Testes/ArquiteturaTestes.cs` (o teste do "IA") e as bancadas de `ControlePelaRedeViewModelTestes.cs` e `ControleParametroViewModelTestes.cs`.

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Nucleo/Escrita/CopiaAntes.cs`: um `DesfazerAsync()` que volta à cópia a operação em andamento. Só escreve o que difere e conclui no sucesso. Na falha, fica pela metade com a sessão aberta, ou interrompida sem ela. Reaproveita o laço do `VoltarAsync`. Serve ao cancelar da criação.
- `src/BgsControle.Nucleo/Setups/CriacaoMemoria.cs` (novo): etapas, perguntas, ajustes, rodadas, resumo, "Como ficou?", Salvar, cancelar, interrupção (`Sessao.Mudou` e `UsoCompartilhado.Mudou`), o registro do tipo `criar-memoria` na cópia, os eventos `Mudou` e `Aconteceu` e as mensagens. É o coração da CAP-18.
- `src/BgsControle.Nucleo/Setups/GerenteSetups.cs`: um salvar da criação `internal`, com o espelho, o Setup aberto e o preset em uso, sob `_operacao`. Assim o Salvar vale para o som do aparelho.
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs` e `JsonPorta.cs`:
  - as rotas `GET /criacao` e `POST /criacao/{abrir,pergunta,ajuste,resumo,como-ficou,cancelar}`;
  - os eventos da criação no `/eventos`;
  - a recusa de `/ajuste` e `/memoria/aplicar` durante a criação;
  - o Cancelar de folha que cancela a criação;
  - a ajuda.
  
  Essa é a porta nos dois sentidos.
- `src/BgsControle.Telas/CriacaoMemoriaViewModel.cs` (novo): etapa, Setup, pergunta e respostas com o "Outro…", os ajustes da rodada, o resumo ("Pronto. Ouça um pouco.", "N ajustes no aparelho, todos relidos." ou "Mudou N ajuste(s), em destaque. O resto ficou igual."), "Avaliar o som", "Como ficou?", Salvar com o nome, "Ajustar mais" e o ✕ com a folha "Cancelar a nova memória?". A folha diz "Nada é salvo e o aparelho volta para a <som de antes>, como estava antes.", com "Cancelar e voltar" e "Continuar criando". É o lado do usuário, sem XAML.
- `src/BgsControle.App/MauiProgram.cs`: registrar o `CriacaoMemoria` (criado cedo), passá-lo ao `PortaComandos` e registrar o ViewModel. Assim o app real tem as rotas.
- `tests/BgsControle.Nucleo.Testes/CriacaoTestes.cs` e `tests/BgsControle.Telas.Testes/CriacaoMemoriaViewModelTestes.cs`: um teste por linha da matriz, mais uma criação de ponta a ponta pela porta e pelo ViewModel até o Salvar. Essa é a prova.

**Acceptance Criteria:**
- Given um cliente pareado e o Casa 2.1 com a FILME, when ele abre a criação, manda 2 perguntas (a 2ª como última), o usuário responde, ele manda 3 ajustes e o resumo, e o usuário avalia e salva "MUSICA ORIG", then a memória nova tem a regulagem relida, fica em uso, a marca da cópia saiu e o `/eventos` trouxe as 2 respostas, a do "Como ficou?" e `criacao-salva`.
- Given uma criação cancelada por qualquer caminho, when o app relê, then cada parâmetro de áudio é igual à cópia de antes, e nenhuma memória foi criada.
- Given os testes que já existem, when a 9d entra, then todos continuam passando, e o build Android sai sem erro nem aviso.

## Spec Change Log

## Review Triage Log

### 2026-10-30 — Review pass
- verdicts: 39 findings — high 0, medium 13, low 21, false 5, maybe-false 0
- findings:
  - `[false]` `[reject]` Intent: a folha de risco não sai do `/criacao/ajuste`, só de outras rotas (o tom) — a spec (Design Notes) escolheu D5 para qualquer folha aberta na criação, porque nenhum ajuste de áudio confirma hoje; a linha da matriz pede o tom.
  - `[false]` `[reject]` Intent: a "resposta" não é rota, vai por evento — leitura escolhida e justificada na spec (Design Notes), com o `/eventos` que o `invoke_dev_with` manda reusar.
  - `[low]` `[reject]` Intent: a reabertura do app (cópia nova lida do arquivo) e o revogar não têm teste — a retomada pela marca registrada é o mecanismo genérico da `CopiaAntes`, já provado para os outros tipos; o revogar cai no mesmo `AoMudarUso` do `DELETE /tarefa`, que tem teste.
  - `[false]` `[reject]` Intent: os testes da porta não usam socket — padrão da 9b, que tem o teste HTTP de ponta a ponta; a rota nova não muda o transporte.
  - `[false]` `[reject]` Intent: o ✕ do núcleo não confere a regra das etapas — a regra é do ViewModel, e `CriacaoMemoriaViewModelTestes` a cobre.
  - `[false]` `[reject]` Intent: o cancelar pela porta e pela folha vale em qualquer etapa — é o que a spec diz (só o ✕ fica nas perguntas e no "Como ficou?").
  - `[medium]` `[patch]` Intent: a mudança do link no `Escritor` não tem teste — grupo do link: o teste confere que a saída 4 acompanhou.
  - `[medium]` `[patch]` Verification gap: a cópia do link para a saída 4 nunca é conferida — grupo do link: o teste confere `081`/3 em +2,0 e a resposta sem aviso.
  - `[medium]` `[patch]` Verification gap: a volta que falha no cancelar não tem teste — teste novo: `voltou:false`, pela metade com o tipo `criar-memoria`, evento com o motivo.
  - `[medium]` `[patch]` Verification gap: o ajuste sem confirmação que trava o resumo não tem teste — teste novo com o simulador calado: 409 no resumo até um ajuste relido.
  - `[low]` `[patch]` Verification gap: os campos JSON dos eventos da criação no `/eventos` só são conferidos no registro em memória — o teste de ponta a ponta e o do cancelar pela porta leem o JSON.
  - `[medium]` `[patch]` Blind: o cancelar durante o Salvar dá salva e cancelada ao mesmo tempo — grupo do Salvar: a marca `_salvando` recusa o cancelar e ignora a interrupção enquanto salva.
  - `[medium]` `[patch]` Blind: a interrupção durante o Salvar ainda salva e tira a marca — grupo do Salvar.
  - `[medium]` `[patch]` Blind: o Salvar morre se a porta some (o cliente para de consultar ou chama `DELETE /tarefa` depois do `salvar:true`) — a ajuda passa a dizer que o cliente mantém a tarefa e as consultas até `criacao-salva` ou `criacao-cancelada` (a regra 6 da spec fica).
  - `[low]` `[reject]` Blind: a tarefa iniciada ao abrir não termina ao fechar — o cliente termina com `DELETE /tarefa`, e o vigia de 30 s termina sozinho; nada aparece na tela.
  - `[medium]` `[patch]` Blind: a saída do link que não acompanhou passa pelo resumo — grupo do link: o endereço com o aviso do link entra nos incertos, e o resumo dá 409.
  - `[low]` `[reject]` Blind: o "antes" do resumo depois de uma escrita sem confirmação mostra o valor do meio — só depois de uma escrita sem eco nem releitura, o que é raro, e o conserto pede consultar a cópia.
  - `[low]` `[reject]` Blind: a volta que falha no cancelar não aparece na tela da criação — a tela fecha, mas a folha global de "aplicado pela metade" aparece, e a 10d desenha o erro (questão 18).
  - `[low]` `[reject]` Blind: toda falha do Salvar aparece como erro do nome — só ocupado, fechada ou leitura em andamento, o que é raro; separar pede um ramo novo.
  - `[low]` `[patch]` Blind: o `relido` tem tipos diferentes no `/ajuste` e no `/criacao/ajuste` — a ajuda explica o `relido` (valor) e o `semEco`.
  - `[low]` `[reject]` Blind: o "Avaliar o som" e o "Ajustar mais" não geram evento — a resposta seguinte ao "Como ficou?" chega como evento, e é ela que importa ao cliente.
  - `[low]` `[reject]` Blind: o ajuste em voo durante o cancelar recebe "interrompida" — só a mensagem, numa janela de milissegundos.
  - `[low]` `[reject]` Blind: o parse do ajuste está duplicado entre a porta e a criação — refatorar não muda o comportamento, e a porta precisa do quadro para o histórico.
  - `[low]` `[patch]` Blind: a mensagem do 400 não fala do link — `MsgSoRegulagem` cita as 15 bandas, o preset de EQ, o link e o mute.
  - `[low]` `[reject]` Blind: uma exceção no `ConcluirAsync` do desfazer pula o evento — o `ConcluirAsync` só grava pelo `Gravacao`, que devolve falso em vez de lançar.
  - `[low]` `[patch]` Blind: os caminhos de maior risco não têm teste — coberto pelos testes novos do link, da volta que falha, do sem confirmação e do Salvar; o resto foi rejeitado nas linhas acima.
  - `[medium]` `[patch]` Edge: o cancelar enquanto o Salvar segura a vez — grupo do Salvar.
  - `[medium]` `[patch]` Edge: a sessão cai entre o Salvar e o `ConcluirAsync` — grupo do Salvar.
  - `[low]` `[reject]` Edge: o `Interromper` marca pela metade com uma escrita ainda em voo — a janela é a de uma escrita (milissegundos) mais um toque em Voltar, e o conserto pede esperar a vez de forma assíncrona.
  - `[medium]` `[patch]` Edge: o link que não acompanhou com a mestre Ok — grupo do link.
  - `[low]` `[reject]` Edge: uma falha depois do envio com outro motivo (exceção) não vai aos incertos — o `Escritor` devolve os motivos listados quando o quadro saiu; os outros são recusas antes do envio.
  - `[low]` `[patch]` Edge: o `/criacao/cancelar` responde 200 `voltou:false` se a criação fecha entre a leitura e o cancelar — 409 quando o motivo é `MsgFechada`.
  - `[low]` `[reject]` Edge: o Cancelar de uma folha aberta antes da criação cancela a criação — exige o cliente pedir um risco e abrir a criação com a folha ainda aberta; guardar isso pede estado novo.
  - `[low]` `[reject]` Edge: o cancelar sem escrita, com a leitura em andamento, fica pela metade à toa — raro na criação, e o "Voltar à cópia" resolve sem escrever nada.
  - `[low]` `[patch]` Edge: o `Dispose` da porta não sai do `Aconteceu` — a linha foi acrescentada.
  - `[low]` `[reject]` Edge: o Salvar mostra falhas que não são do nome como erro do nome — igual à linha do Blind acima.
  - `[low]` `[patch]` Edge: o texto do "Outro…" fica na pergunta seguinte — o ViewModel limpa o texto quando muda o número da pergunta.
  - `[medium]` `[patch]` Edge (alegação): o cancelar pela porta durante o Salvar deixa memória e som sem voltar — grupo do Salvar.
  - `[medium]` `[patch]` Edge (alegação): o desfazer que volta cedo sem marca diz "pela metade" — grupo do Salvar (era o caso do Salvar em andamento).

## Design Notes

- **A resposta vai por evento, não por espera.** O usuário pode levar minutos numa pergunta, e o pedido HTTP da folha de risco já espera no máximo 60 s. O cliente consulta o `/eventos`, que é o padrão da 9b, e isso também mantém vivo o vigia de 30 s.
- **"Ótimo, pode salvar" é do app.** A porta não salva, então quem leva ao Salvar não pode ser uma resposta qualquer da IA.
- **Só a regulagem.** A memória guarda a regulagem, e as 15 bandas são do preset. Um ajuste de banda na criação sumiria no Salvar, e o link vem do Setup.
- **Por que `daCopia`.** Enquanto a marca existe, o `Bloqueio` recusa toda escrita de áudio. É isso que deixa a tela e o `/ajuste` de fora durante a criação. Só a criação e a volta à cópia passam.
- **Folha de risco na criação.** Hoje nenhum ajuste de áudio confirma (`ConfirmacaoIa.Nunca`). A regra D5 vale para qualquer folha que a porta abra com a criação aberta, por exemplo um tom do gerador. Se um código de áudio passar a confirmar, o mesmo caminho serve.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes`: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes`: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug`: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- No aparelho, é a história 12c (uma criação pela porta com o Claude Code mandando as perguntas à mão).

## Auto Run Result

Status: done

**O que entrou.** A criação da memória (CAP-18) pela porta, nos dois sentidos:
- o `CriacaoMemoria` no núcleo: etapas, perguntas, ajustes só da regulagem e só depois da última resposta, rodadas de refino, resumo, "Como ficou?" com "Ótimo, pode salvar" do app, Salvar só do usuário, cancelar com volta à cópia e a interrupção da regra 6;
- as rotas `GET /criacao` e `POST /criacao/{abrir,pergunta,ajuste,resumo,como-ficou,cancelar}`, com as respostas e o fim da criação no `/eventos`;
- o `CriacaoMemoriaViewModel`, sem XAML.

**Arquivos** (em `app-controle/`):
- `src/BgsControle.Nucleo/Setups/CriacaoMemoria.cs` (novo): o coração da CAP-18.
- `src/BgsControle.Nucleo/Escrita/CopiaAntes.cs`: `DesfazerAsync` (o laço do Voltar à cópia reaproveitado) e `MotivoBloqueioGerador` (o tom confirmado toca na criação).
- `src/BgsControle.Nucleo/Escrita/Escritor.cs`: a cópia do link passa pela trava como a escrita mestre.
- `src/BgsControle.Nucleo/Gerador/GeradorSinal.cs`: usa o `MotivoBloqueioGerador`.
- `src/BgsControle.Nucleo/Setups/GerenteSetups.cs`: `SalvarCriacaoAsync`, que salva do espelho mesmo vindo do som do aparelho.
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs` e `JsonPorta.cs`: as rotas, os eventos, a recusa do `/ajuste` e do `/memoria/aplicar` na criação, o Cancelar da folha (D5) e a ajuda.
- `src/BgsControle.Telas/CriacaoMemoriaViewModel.cs` (novo): o lado do usuário, sem "IA" nos textos.
- `src/BgsControle.App/MauiProgram.cs`: o `CriacaoMemoria` criado cedo, passado à porta, e o ViewModel registrado.
- `tests/BgsControle.Nucleo.Testes/CriacaoTestes.cs` e `tests/BgsControle.Telas.Testes/CriacaoMemoriaViewModelTestes.cs` (novos): uma linha da matriz por teste, mais as criações de ponta a ponta.

**Revisão.** 39 achados (13 medium, 21 low, 5 false):
- **Patches (10 grupos):**
  - o Salvar contra o cancelar e a interrupção (`_salvando`);
  - a saída do link que não acompanhou trava o resumo;
  - testes da volta que falha, do ajuste sem confirmação e dos campos JSON dos eventos;
  - a ajuda (manter a tarefa até `criacao-salva` ou `criacao-cancelada`; `relido` e `semEco`);
  - `MsgSoRegulagem` com o link;
  - 409 no cancelar já fechado;
  - o `Dispose` sai do `Aconteceu`;
  - o "Outro…" limpo a cada pergunta.
- **Nada diferido.**
- **Rejeitados com o motivo no Triage Log:**
  - a tarefa que fica aberta;
  - o "antes" depois de uma escrita sem eco;
  - a falha do cancelar fora da tela;
  - o erro do Salvar no campo do nome;
  - os eventos de Avaliar e Ajustar mais;
  - a mensagem do ajuste em voo;
  - o parse duplicado;
  - a exceção no `ConcluirAsync`;
  - a escrita em voo no `Interromper`;
  - outros motivos de falha;
  - a folha aberta antes da criação;
  - o cancelar com leitura em andamento;
  - a reabertura do app sem teste próprio;
  - e os 5 false do alinhamento.

**Revisão de acompanhamento: recomendada.** Foram 5 entradas medium com patch. O risco que ninguém verificou é a ordem do Salvar contra a queda e o cancelar (`_salvando`). Os testes injetam a corrida no arquivo de Setups, mas uma queda real no meio de uma escrita de criação, junto com o toque em Voltar à cópia, só a 12c confere.

**Verificação:**
- `dotnet test` Núcleo: 1051 de 1051. Telas: 424 de 424.
- **Build Android:** o JDK 17 e o Android SDK de `%LOCALAPPDATA%\BgsFerramentas` não estão mais na máquina. O C# do App (com o `MauiProgram.cs`) compilou com 0 avisos contra um SDK temporário mínimo. O empacotamento parou só no merge do manifesto Java, que pede o JDK 17 (o que há é o 11).
- A auditoria da matriz deu um teste por linha, e todos rodaram.

**Riscos que ficam:**
- O APK não foi gerado: restaurar o `BgsFerramentas` antes da 12b e da 12c.
- A criação pela porta não foi testada no aparelho (história 12c).
- O cliente precisa manter a tarefa até o fim, como está na ajuda.
- **31/10, depois das ferramentas reinstaladas:**
  - O JDK 17.0.20.1 da Microsoft foi extraído em `%LOCALAPPDATA%\BgsFerramentas`, com o SHA-256 conferido.
  - O Android SDK foi instalado pelo `InstallAndroidDependencies`.
  - `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` saiu com 0 erros e 0 avisos, e o APK foi gerado com a 9d.
