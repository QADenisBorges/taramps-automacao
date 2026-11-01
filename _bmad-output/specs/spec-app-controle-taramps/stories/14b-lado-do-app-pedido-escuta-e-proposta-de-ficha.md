---
title: 'História 14b: lado do app — pedido de criação, escuta "1 ou 2" e proposta de ficha'
type: 'feature'
created: '2026-11-01'
baseline_revision: 'bb60d0ae81de8ab17f4593e646c59cdfcbe6f83c'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-agente-memoria/lado-do-app.md'
  - '{project-root}/_bmad-output/specs/spec-agente-memoria/SPEC.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/porta-de-comandos.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** Para o Agente da 14a trabalhar, o app precisa de três peças (`lado-do-app.md`; spec-agente-memoria CAP-1, CAP-4 e CAP-6):
- esperar o Agente depois do "+ Nova memória", com um prazo;
- uma pergunta do tipo "1 ou 2" no refino, que troca o som ao vivo com o volume igualado;
- a proposta de ficha para um modelo que a base não conhece, que só o toque salva.

**Approach:**
- O núcleo ganha o pedido com prazo (`CriacaoMemoria`), a escuta (`CriacaoMemoria` e a porta) e a proposta de ficha (`Setups/PropostaFicha.cs` e a porta).
- Os ViewModels (`MemoriasViewModel`, `CriacaoMemoriaViewModel` e `PropostaFichaViewModel`) dão o lado do usuário, sem XAML. As telas são da 14c.

## Boundaries & Constraints

**Always:**
- **Pedido de criação:**
  - **Com a chave "Controle pela rede" desligada:** o toque em "+ Nova memória" não manda nada. Mostra "Ligue o Controle pela rede em Aparelho." e leva à tela Aparelho (`INavegacao`; o destino entra se faltar).
  - **Com a chave ligada:** o toque emite o `criacao-pedida` (10d) e começa a espera (`EsperandoAgente`; a tela mostra o anel duplo, na 14c).
    - Se a criação abrir em até 15 s, a espera acaba e a camada da criação entra (10d).
    - Sem abrir em 15 s: "Não deu para começar agora. Confira se o computador está ligado e no mesmo Wi-Fi.", e volta.
    - Durante a espera, o "+ Nova memória" fica travado, e um 2º toque não emite outro pedido.
  - O prazo é medido com o `TimeProvider` (relógio falso nos testes).
- **Escuta "1 ou 2"** (só no refino: a etapa `Ajustando` numa rodada ≥ 2, depois de um "Como ficou?" respondido sem salvar):
  - **Pedido pela porta:** `POST /criacao/escuta {"texto":"Qual grave você prefere?","variavel":"grave","versao1":[ajustes],"versao2":[ajustes]}`. Cada ajuste é `{codigo,indice,valor,frase,motivo}`, como no `/criacao/ajuste`.
    - Valem só os códigos da regulagem, como na 9d.
    - Cada versão é conferida inteira na cerca (13a) e na regra de "sem calibração" (13b) antes de qualquer envio. Uma recusa dá 409, com o motivo, e nada é escrito.
    - Fora do refino dá 409 "A escuta só vale no refino.". Com outra escuta aberta também dá 409.
  - **Volume igualado:**
    - O app calcula, para cada versão, o nível médio da cadeia (o EQ de entrada, as 15 bandas, a rota, o ganho, o EQ de saída e os cortes; a mesma resposta da 13c) com o peso de ruído rosa: a média em dB de |H(f)|² numa grade logarítmica de 20 Hz a 20 kHz, a média das saídas com alto-falante.
    - A versão mais forte recebe o master baixado pela diferença: só baixa, nunca sobe. O master do nível (13c) e a cerca continuam valendo.
  - **Na tela** (`CriacaoMemoriaViewModel`, fase `Escuta`): o texto, dois botões grandes ("1" e "2"), e embaixo "Prefiro o 1", "Prefiro o 2" e "Tanto faz".
    - Ao abrir, o app escreve a versão 1 (com o master dela) e marca "1".
    - Tocar "1" ou "2" troca o som ao vivo: só o que difere, como App dentro da criação (`daCopia`), com a releitura.
    - Uma escrita que não confere mostra "O aparelho ficou com outro valor." e fica na escuta.
  - **A escolha:**
    - "Prefiro o 1" ou "Prefiro o 2" deixa o som naquela versão. "Tanto faz" volta à 1.
    - A escolha vai ao `/eventos` como `criacao-escuta {variavel, escolha: 1|2|"tanto-faz"}`, e a etapa volta a `Ajustando` (mesma rodada).
    - Os ajustes da versão que ficou entram nos ajustes da rodada (frase e motivo), para o resumo.
  - **O ✕ na escuta** cancela a criação e volta à cópia de antes da criação, como as outras perguntas (9d).
  - O `GET /criacao` traz a escuta aberta (texto, variável, versão marcada) e a última escolha.
- **Proposta de ficha:**
  - **Pedido:** ao salvar (adicionar ou editar) um equipamento com Modelo preenchido que a base de modelos não tem (`BaseModelos.Chave`), o app emite `ficha-pedida {idSetup, idEquipamento, modelo, grupo, categoria}` no `/eventos`. Isso só acontece com a chave "Controle pela rede" ligada; senão, nada. A espera dura 60 s.
  - **Resposta:** `POST /ficha/proposta {"idEquipamento":"…","dados":[{"campo":"fs","valor":46,"origem":"oficial|outra-fonte","fonte":"https://…","aviso":"Qts não fecha com Qes e Qms"}]}`.
    - Os campos são os nomes de `DadosTecnicos.Campos` do grupo do equipamento, com o valor > 0, a origem só `oficial` ou `outra-fonte` e a fonte obrigatória (até 120 caracteres). `aviso` é opcional.
    - Um campo inválido dá 400 com o motivo.
    - Sem pedido pendente para o equipamento, ou depois de 60 s, dá 409 "O prazo da proposta acabou.".
    - Sem resposta em 60 s, a ficha fica como está, sem aviso de erro.
  - **Na tela** (`PropostaFichaViewModel`, uma folha):
    - cada dado com o rótulo, o valor, a origem, a fonte e o aviso;
    - "Usar" e "Recusar" por dado, e "Usar todos" / "Recusar todos".
    - "Salvar" grava só os usados, na ficha do equipamento (`EditarEquipamentoAsync`, com a origem e a fonte) e na base de modelos (`BaseModelos.GuardarAsync`, que já guarda só oficial e outra fonte).
    - O que foi recusado fica em branco: um dado já preenchido na ficha não é apagado nem trocado por um recusado.
    - Fechar sem salvar não grava nada.
    - Só o toque do usuário salva: a porta nunca escreve a ficha.
- **Travas:** as mesmas da porta (`porta-de-comandos.md`): pareado, rede local, a chave "Controle pela rede", o histórico e a origem IA. A cerca do Rendimento e a cópia antes valem.
- Nenhum texto dos ViewModels contém "IA". A ajuda da porta documenta as rotas e os eventos novos.

**Never:**
- XAML (14c).
- O cérebro do Agente (as perguntas e a decisão da variável): 14d.
- A porta salvar a ficha ou a memória.
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Pedido com resposta | Chave ligada; toque; a porta abre em 3 s | `criacao-pedida`; `EsperandoAgente` até abrir; a camada entra | — |
| Pedido sem resposta | Chave ligada; nada em 15 s | O texto do prazo; a espera acaba; o chip volta | 2º toque na espera: nada |
| Chave desligada | Toque | "Ligue o Controle pela rede em Aparelho."; navega para Aparelho; nenhum evento | — |
| Escuta | Refino; versões com o ganho 081 da JBL em +2 e +5 | A versão 1 escrita; master igualado (a 2 com o master −3 dB, aprox.); a fase Escuta | Fora do refino: 409 |
| Trocar | Toque em "2", depois em "1" | Só o que difere é escrito e relido | Releitura diferente: o aviso |
| Escolher | "Tanto faz" | O som na versão 1; `criacao-escuta` com "tanto-faz"; etapa Ajustando | — |
| Cerca | Versão 2 acima do teto | 409 com a mensagem da cerca; nada escrito | — |
| ✕ na escuta | ✕, "Cancelar e voltar" | A releitura igual à cópia de antes da criação | — |
| Ficha pedida | Salvar o equipamento com o modelo "ABC 123" fora da base | `ficha-pedida` com o id e o modelo | Chave desligada: nada |
| Proposta | 3 dados (Fs oficial, Qes outra fonte, Xmax com aviso) | A folha com os 3; "Usar" em 2, "Recusar" em 1; Salvar grava 2 na ficha e na base | Campo inválido: 400 |
| Prazo da ficha | Proposta depois de 60 s | 409; nada na tela | — |
| Fechar a folha | Fechar sem salvar | Nada gravado | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Setups/CriacaoMemoria.cs`:
  - `Pedir()` e `EventoPedida` (10d);
  - as etapas (`Ajustando`, `Autorizado` 271), `_rodada` (799), `ResponderComoFicou` e `EventoComoFicou` (149);
  - `AjustarAsync` (`Conferir`, a cerca pré-conferida, `daCopia`), `CancelarAsync` e o `GET /criacao` (na porta).
  
  O modelo de pergunta com resposta pelo `/eventos` é o da 9d.
- `src/BgsControle.Nucleo/Rendimento/PiorCaso.cs` (a resposta da cadeia em frequência: os sinos, a rota, os cortes) e `Niveis.cs` (o master do nível e a recusa do master acima). `Cerca.cs` (`Conferir` do lote) e `SemCalibracao.cs`.
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs`: as rotas `/criacao/*` (o modelo da escuta e da proposta), `RecusaDaPorta`, `TextoAjuda` e os eventos da criação no `/eventos`.
- `src/BgsControle.Nucleo/Setups/BaseModelos.cs`: `Chave(modelo)` (58), `DoModelo` (77) e `GuardarAsync` (93). `Equipamentos.cs`: `DadosTecnicos.Campos`, o `DadoTecnico` com a origem e a fonte, `OrigemDado` e os grupos. `GerenteSetups.cs`: `AdicionarEquipamentoAsync` (1910) e `EditarEquipamentoAsync` (1921).
- `src/BgsControle.Nucleo/Seguranca/Chaves.cs`: `Chave.ControlePelaRede`.
- Telas:
  - `MemoriasViewModel.cs` (o `NovaMemoria` da 10d e a trava);
  - `CriacaoMemoriaViewModel.cs` (as fases da 10d: entra `Escuta`);
  - `EquipamentosViewModel.cs` (o salvar do equipamento);
  - `Abstracoes.cs` (`INavegacao`; o destino Aparelho);
  - `Falsos.cs`.
- Testes: `CriacaoTestes`, `CriacaoMemoriaViewModelTestes`, `PortaTestes` e `EquipamentosViewModelTestes`. Usar dados genéricos nas propostas, não os do Denis.

## Tasks & Acceptance

**Execution:**
- `CriacaoMemoria.cs`: a escuta (abrir, versões, trocar, escolher, evento, o ajuste no resumo e o ✕) e o estado do pedido com o prazo de 15 s (ou no `MemoriasViewModel`, com o relógio).
- `src/BgsControle.Nucleo/Rendimento/Igualador.cs` (novo): o nível médio com o peso rosa por versão e o master que iguala.
- `src/BgsControle.Nucleo/Setups/PropostaFicha.cs` (novo): o pedido pendente com o prazo de 60 s, a validação e o salvar dos usados.
- `GerenteSetups.cs`: emite `ficha-pedida` ao salvar um equipamento com o modelo fora da base.
- `PortaComandos.cs`: `POST /criacao/escuta`, `POST /ficha/proposta`, os eventos e a ajuda.
- `MemoriasViewModel.cs` (a chave desligada, a espera e o prazo), `CriacaoMemoriaViewModel.cs` (a fase Escuta) e `PropostaFichaViewModel.cs` (novo). `Abstracoes.cs`/`NavegacaoMaui.cs` (o destino Aparelho, se faltar). `MauiProgram.cs`.
- Testes: uma linha da matriz por teste, em `CriacaoTestes`, `PortaTestes`, `PropostaFichaTestes` (novo), `CriacaoMemoriaViewModelTestes`, `MemoriasViewModelTestes` e `PropostaFichaViewModelTestes` (novo).

**Acceptance Criteria:**
- Given um cliente pareado conduzindo uma criação até o refino, when ele manda uma escuta, then o app escreve a versão 1 com o volume igualado, troca ao vivo, devolve a escolha pelo `/eventos` e fica na versão escolhida. O ✕ volta à cópia de antes da criação.
- Given um equipamento salvo com o modelo fora da base, when o cliente manda a proposta em até 60 s, then a folha mostra os dados com a origem, e só o "Salvar" do usuário grava na ficha e na base.
- Given os testes que já existem, when a 14b entra, then todos passam, e o build Android sai sem erro nem aviso.

## Spec Change Log

- **01/11, implementação e revisão** (fora do intent-contract):
  - A espera de 15 s ficou no `MemoriasViewModel` e acaba pelo evento `Abriu`, ou com o motivo real (chave desligada, conexão caída).
  - O `/ficha/proposta` vale com o Setup em dúvida e com outro firmware, porque não mexe no aparelho.
  - O pedido de ficha sai enquanto a base não tem dado oficial ou de outra fonte do modelo.
  - O master igualado é só para comparar: ele volta ao de antes depois da escolha.
  - A versão 1 pedida pela porta é escrita como IA; os toques do usuário, como App.
  - A matriz diz "−3 dB, aprox."; com a média das saídas, a fixture dá −0,8 dB, fixado no teste.
  - **KEEP:** a escuta conferida inteira (cerca, sem calibração e o master do nível) antes de escrever, e só o toque salva a ficha.

## Review Triage Log

### 2026-11-01 — Review pass
- verdicts: 39 findings — high 0, medium 7, low 29, false 3, maybe-false 0
- findings:
  - `[low]` `[patch]` Verification gap: a isenção do `/ficha/proposta` com o Setup em dúvida sem teste — teste (d), e isento também do só leitura.
  - `[low]` `[patch]` Verification gap: as recusas da escuta por "sem calibração" e pelo master acima do nível sem teste — teste (a).
  - `[low]` `[patch]` Verification gap: o teste do igualador frouxo — o `Master2` exato e o `SaidasComAltoFalante` testado (b).
  - `[low]` `[patch]` Verification gap: a saída da escuta sem animação — teste (c).
  - `[medium]` `[patch]` Verification gap (outro): o modelo que fica sem ficha nunca é pedido de novo — o pedido vale enquanto a base não tem dado oficial ou de outra fonte.
  - `[medium]` `[patch]` Blind: o modelo sem ficha não é pedido de novo — o mesmo.
  - `[medium]` `[patch]` Blind: a proposta não confere o equipamento (modelo ou grupo mudados) — confere e descarta.
  - `[medium]` `[patch]` Blind: "Usar" troca o medido sem mostrar o atual — mostra o atual; "Usar todos" pula o medido.
  - `[low]` `[patch]` Blind: sem as unidades e sem uma faixa plausível — as unidades na ajuda e a faixa por campo.
  - `[low]` `[reject]` Blind: o igualador mede o nível elétrico, não o acústico — é o que a spec definiu (a média de |H|² das saídas); a 12d confere de ouvido.
  - `[low]` `[patch]` Blind: a matriz diz −3 dB, e o teste aceita 0,1 a 3,0 — o valor exato no teste (a matriz é aproximada).
  - `[medium]` `[patch]` Blind: o master baixado para comparar fica na memória — volta ao de antes depois da escolha.
  - `[low]` `[reject]` Blind: a spec não registra os desvios — vão para o Spec Change Log no fim desta rodada.
  - `[low]` `[patch]` Blind: o −80 dB sem aviso — recusa com 409.
  - `[low]` `[patch]` Blind: faltam testes (interrupção, cancelar pela porta na escuta) — teste (e).
  - `[low]` `[patch]` Blind: sem estado de ocupado na escuta — `Escrevendo`.
  - `[low]` `[patch]` Blind: o 200 da porta quando a versão 1 não confere — a resposta traz o aviso (já vai no `escuta.aviso`; o falso é coberto pelo `GET /criacao`).
  - `[low]` `[patch]` Blind: a mensagem errada no `OuvirAsync` — "versão inválida".
  - `[low]` `[patch]` Blind: a espera não acaba quando a chave cai ou a conexão cai — acaba com o motivo real.
  - `[low]` `[patch]` Blind: os pedidos vencidos ficam guardados — limpos pelo prazo.
  - `[low]` `[reject]` Intent: os 15 s ficam só no ViewModel; um `/criacao/abrir` tardio abre a criação — a spec permitiu "ou no MemoriasViewModel"; a criação que abre tarde aparece, o que não é perigoso.
  - `[medium]` `[patch]` Intent: o master igualado entra na memória — o mesmo do Blind.
  - `[low]` `[patch]` Intent: a escrita da versão 1 pedida pela porta vai como App — vai como IA, com o histórico.
  - `[low]` `[patch]` Intent: o `/ficha/proposta` fora da trava da dúvida — mantido (não mexe no aparelho), testado e isento também do só leitura.
  - `[low]` `[reject]` Intent: o igualador pela média de todas as saídas — o mesmo do Blind (a spec definiu).
  - `[medium]` `[patch]` Intent: o gatilho da ficha só pelo modelo ausente — o mesmo do Blind.
  - `[low]` `[reject]` Intent: a folha da proposta e a ida a Aparelho só no ViewModel — a tela é da 14c.
  - `[low]` `[reject]` Intent: os testes não passam pelo servidor HTTP real — o `AtenderAsync` é o mesmo código das rotas; o HTTP é da 9b.
  - `[low]` `[patch]` Edge: o modelo ou o grupo mudados antes de salvar — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o medido trocado sem aviso — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o modelo vazio na base nunca é pedido — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o só leitura recusa a proposta — isento.
  - `[low]` `[patch]` Edge: o par com link com valores diferentes numa versão — 400.
  - `[low]` `[patch]` Edge: o ajuste igual à base no resumo — pulado.
  - `[low]` `[patch]` Edge: a fila do palco perdida ao entrar na escuta — mantida.
  - `[low]` `[patch]` Edge: o "Não há escuta aberta." depois da escolha — ignorado.
  - `[low]` `[patch]` Edge: a espera por `Aberta` perde o abrir-e-fechar — pelo evento.
  - `[false]` `[reject]` Edge (alegação): as rotas novas sem teste da porta — `CriacaoTestes` e `PropostaFichaTestes` passam pelo `AtenderAsync` da porta.
  - `[false]` `[reject]` Edge (alegação): a alimentação não vai à base — a base guarda só o modelo; a alimentação é da instalação (8f), por desenho.
  - `[false]` `[reject]` Edge: o valor pequeno arredondado na folha — corrigido no mesmo patch do formato (a linha conta como o patch do Edge acima; esta duplicata não tem defeito novo).

## Design Notes

- **Por que só baixar o master para igualar:** subir poderia passar do teto do nível ou da cerca. Baixar a versão mais forte iguala do lado seguro.
- **Por que a escolha pelo `/eventos`:** é o mesmo caminho das respostas da 9d. O usuário pode levar o tempo que quiser.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet test agente/tests/Agente.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

## Auto Run Result

Status: done

**O que entrou.** O lado do app do Agente (spec-agente-memoria CAP-1, CAP-4 e CAP-6), no núcleo e nos ViewModels:
- **O pedido de criação:**
  - Com a chave "Controle pela rede" desligada, avisa e leva a Aparelho.
  - Com ela ligada, emite o `criacao-pedida` e espera 15 s pela abertura, com o texto do prazo, sem 2º pedido e com o fim pela chave ou pela conexão.
- **A escuta "1 ou 2"** (só no refino):
  - `POST /criacao/escuta`, com cada versão conferida inteira na cerca, na regra de sem calibração e no master do nível antes de escrever;
  - o volume igualado pelo master, só baixando (o `Igualador`, pela média rosa da cadeia);
  - a troca ao vivo só do que difere, com a releitura;
  - a escolha no `/eventos` (`criacao-escuta`), e o master de antes volta;
  - o ✕ volta à cópia.
- **A proposta de ficha:**
  - o `ficha-pedida` sai ao salvar um modelo sem dado oficial ou de outra fonte na base;
  - `POST /ficha/proposta` com as unidades, as faixas e o prazo de 60 s, conferindo o modelo e o grupo;
  - a folha mostra o valor atual ao lado do proposto, e "Usar todos" pula o medido;
  - só o "Salvar" do usuário grava, na ficha e na base.

**Arquivos** (em `app-controle/`):
- Núcleo:
  - `Setups/CriacaoMemoria.cs` (a escuta e o `Abriu`);
  - `Rendimento/Igualador.cs` (novo);
  - `Setups/PropostaFicha.cs` (novo);
  - `GerenteSetups.cs` (o `FichaPedida`);
  - `Porta/PortaComandos.cs` e `JsonPorta.cs`.
- Telas:
  - `MemoriasViewModel.cs`, `CriacaoMemoriaViewModel.cs` (a fase `Escuta`) e `PropostaFichaViewModel.cs` (novo);
  - `EquipamentosViewModel.cs` e `Abstracoes.cs` (`AbrirAparelho`).
- App: `NavegacaoMaui.cs` e `MauiProgram.cs`.
- Testes:
  - `CriacaoTestes`, `PropostaFichaTestes` (novo), `CriacaoMemoriaViewModelTestes`, `MemoriasViewModelTestes` e `EquipamentosViewModelTestes`;
  - `PropostaFichaViewModelTestes` (novo) e `Falsos`.

**Revisão.** 39 achados (7 medium, 29 low, 3 false).
- **Patches:**
  - o pedido de ficha repetido;
  - o equipamento conferido;
  - o valor atual e o "Usar todos";
  - as unidades e as faixas;
  - o master de volta depois da escolha;
  - a origem IA da versão 1;
  - o par com link, o ajuste igual e o −80 dB;
  - a isenção do só leitura;
  - o estado de escrita, o toque atrasado e a fila do palco;
  - o fim da espera;
  - os pedidos vencidos;
  - o formato dos números;
  - os testes (a) a (f).
- **Nada diferido.**
- **Rejeitados,** com o motivo no Review Triage Log.

**Revisão de acompanhamento: recomendada.** Foram várias entradas medium com patch. O risco que ninguém conferiu é o volume igualado de ouvido, que é elétrico e não acústico, e a escuta e a folha da ficha no aparelho. Isso é da 12e. As telas são da 14c.

**Verificação:**
- `dotnet test` Núcleo: 1365 de 1365.
- `dotnet test` Telas: 546 de 546.
- `dotnet test agente/tests/Agente.Testes`: 92 de 92.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- A matriz tem pelo menos um teste por linha.
