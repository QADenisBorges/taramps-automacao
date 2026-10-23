---
title: 'História 8e: equipamentos, ligações e saídas do Setup'
type: 'feature'
created: '2026-10-23'
status: 'done'
baseline_revision: '3f3bdf74e34fa6daa12978f86f091ad04983e7de'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-23-equipamentos.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/SPEC.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/conhecimento-herdado.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/8c-setup-e-memoria-no-celular.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
warnings:
  - oversized
deferred:
  - summary: >-
      A folha do Salvar e a lista "O que não bate" rotulam por canal ("Atraso da saída 3", "Atraso da saída 4"), e um ajuste no Sub (par com link) aparece em duas linhas.
    evidence: |-
      MudancasDeCalibracao e DiferencasDeCalibracao usam Camadas.Rotulo por canal; o EXPERIENCE.md fala em "Atraso do Sub". A história 10 monta essas telas e deve rotular pelo nome da saída do Setup, juntando o par.
    location: >-
      app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs (MudancasDeCalibracao, DiferencasDeCalibracao)
    severity: medium
  - summary: >-
      Na dúvida, em "Seguir sem Setup" e ao criar um Setup novo, o link que vale é o do Setup anterior.
    evidence: |-
      O link é estado do app desde a 8c; agora ele também une as saídas na tela. Numa instalação desconhecida, o CH3 pode continuar espelhado no CH4. Qual link vale sem Setup em uso é decisão do Denis (história 10).
    location: >-
      app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs
    severity: medium
---

<intent-contract>

## Intent

**Problem:** O Setup da 8c tem só a calibração. As saídas do app estão fixas (JBL L, JBL R, Sub) e o equilíbrio só existe nas saídas 1 e 2. O Denis decidiu em 23/10 que o Setup tem equipamentos ligados aos canais pelo modelo de ligação da proposta de equipamentos, e que as saídas do app vêm do Setup (CAP-3, CAP-16).

**Approach:** No núcleo `Setups`: o equipamento e as ligações, as saídas do app derivadas do Setup (canal ou par com link) com nome e cor, o equilíbrio por canal, a sugestão de função acústica, o herdado Casa 2.1 com os equipamentos e o Link 3–4 ligado, e a migração do `setups.json`. Em `BgsControle.Telas`: o ViewModel da lista e da folha de equipamento. Tudo testado contra o `ProcessadorSimulado`.

## Boundaries & Constraints

**Always:**
- **Modelo de ligação** (seção 2 da proposta de equipamentos):
  - Grupos: **1** amplificam e tocam (caixa ativa, sub ativo, monitor ativo, line array ativo, soundbar/receiver); **2** amplificam para passivos (módulo/amplificador, com N canais e bridges); **3** processam ou distribuem e repassam (processador, crossover ativo, equalizador, distribuidor de linha, transmissor sem fio); **4** só recebem (interface de áudio, gravador, mesa); **alto-falante passivo** (tweeter, driver, médio, médio-grave, woofer, coaxial, subwoofer, shaker).
  - As categorias são texto livre, com uma lista de sugestões por grupo.
  - O equipamento tem nome, modelo (vazio = "genérico") e função acústica: sub, grave, médio-grave, médio, agudo, super-agudo, full-range ou outra.
  - Os dados técnicos são todos opcionais: diâmetro em polegadas, impedância em Ω, potência RMS em W, sensibilidade em dB, faixa em Hz, ganho do amplificador em ×, número de canais e, na caixa ativa, as vias de dentro (descrição e diâmetro).
  - **Fonte de sinal** de uma entrada de linha: um canal do processador (1 a 4), ou a saída de um equipamento do grupo 3 (que repassa o que recebe). Cada canal do processador pode alimentar quantas entradas quiser (cabo Y). A cadeia do grupo 3 não pode formar ciclo.
  - **Grupos 1, 3 e 4:** uma ou mais entradas, cada uma com a sua fonte (ou sem ligação).
  - **Grupo 2:** cada canal do módulo tem uma fonte (ou nenhuma). Um bridge junta dois canais vizinhos do módulo (1+2, 3+4…), e um canal está em no máximo um bridge.
  - **Alto-falante passivo:** fica ligado num canal ou num bridge de um módulo, ou em nada. Mais de um alto-falante pode ficar no mesmo canal.
  - Uma ligação para um equipamento que não existe ou para o grupo errado é recusada, sem mudar nada.
- **Saídas do app** (o que as telas mostram e ajustam):
  - As saídas vêm do Setup em uso, na ordem dos canais. Cada par vizinho (1–2, 3–4) com o link ligado vira uma saída só, cujo mestre é o canal ímpar; senão, cada canal é uma saída.
  - **Nome da saída:** os nomes, sem repetir e na ordem de criação, dos equipamentos que tocam e que aquela saída alimenta: grupo 1 e alto-falantes passivos, seguindo o grupo 3 e os módulos. Se a saída não alimenta nenhum que toca, vale o nome dos outros que ela alimenta; se não alimenta nada, "CH1", ou "CH3 + CH4" num par.
  - **Cor:** o canal mestre (1 a 4).
  - **Aviso:** quando os dois canais de um par alimentam o mesmo bridge e o link do par está desligado: "CH3 e CH4 alimentam o mesmo bridge: ligue o Link 3–4 para ficarem iguais."
  - **Sem Setup em uso,** as saídas são CH1 a CH4, também com os pares do link.
- **Equilíbrio por canal**, no lugar do L/R:
  - um ganho e um limiter por canal (1 a 4), de −12,0 a +12,0 dB, passo 0,1, somados à memória como na 8c, nas mesmas regras de faixa e de OFF;
  - mexer no equilíbrio de uma saída põe o mesmo valor nos dois canais quando ela é um par;
  - a conferência ao conectar, o aplicar, o Salvar e a folha do Salvar seguem iguais, só que com os 4 canais.
- **Sugestão de função acústica**, quando o usuário não escolhe:
  - pela categoria (tweeter → agudo, driver → médio ou agudo, médio → médio, médio-grave → médio-grave, woofer → grave, subwoofer e sub ativo → sub, coaxial e caixa ativa full-range → full-range);
  - senão, pelo corte lido do canal que alimenta o equipamento: passa-baixa até 200 Hz → sub; passa-alta a partir de 2 kHz → agudo; senão, full-range;
  - a escolha do usuário sempre vale mais.
- **Herdado Casa 2.1:**
  - o Link 3–4 fica ligado e o equilíbrio da JBL R (CH2) é −3 no ganho e no limiter;
  - os equipamentos de `conhecimento-herdado.md`: "Módulo genérico" (grupo 2, 4 canais, CH1 a CH4, bridge 3+4, ×19,6), JBL L (CH1) e JBL R (CH2) com os dados da JBL 4TRMS80, e "Subwoofer genérico" no bridge 3+4;
  - as saídas dele são JBL L, JBL R e Sub (o par CH3 + CH4).
- **Persistência:**
  - O `setups.json` vai ao formato 2: equilíbrio por canal e a lista de equipamentos de cada Setup.
  - Leitura do formato 1: `ganho1`/`limiter1`/`ganho2`/`limiter2` viram os canais 1 e 2, e os outros ficam 0. O Casa 2.1 herdado sem equipamentos recebe os do herdado e passa ao Link 3–4 ligado. Um Setup próprio fica sem equipamentos.
  - Um equipamento ilegível é descartado; os outros ficam.
- Criar, editar e tirar equipamento não manda nada pelo BLE e não tem chave. Tirar um módulo deixa os alto-falantes dele sem ligação, e tirar um equipamento do grupo 3 deixa sem fonte quem ele alimentava, nos dois casos com aviso.

**Never:**
- Não fazer telas XAML (histórias 10 e 10b) nem o esquema (10b). Não usar limites físicos (potência e tensão contra o equilíbrio ou o teto): isso é do Rendimento.
- Não mexer no protocolo, nas chaves, nas memórias do celular nem nos presets além do equilíbrio por canal.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Casa 2.1 herdado | Primeiro uso | Saídas: JBL L (CH1), JBL R (CH2), Sub (CH3+CH4, cor do CH3) | — |
| Migração | `setups.json` da 8d (formato 1) | Casa 2.1 com os equipamentos, Link 3–4 ligado, equilíbrio CH2 −3; Setup próprio sem equipamentos | Arquivo estragado: volta ao herdado |
| Setup de 4 canais | 2 tweeters no CH1, Snake L no CH2, Snake R no CH3, JBL L e R traseiras no CH4, sem link | 4 saídas: "Tweeter 1 + Tweeter 2", "Snake L", "Snake R", "JBL L traseira + JBL R traseira" | — |
| Cabo Y | CH1 alimenta uma caixa ativa e um sub ativo | Saída CH1 com os dois nomes | — |
| Repasse | CH2 → crossover ativo → 2 módulos → alto-falantes | Saída CH2 com os nomes dos alto-falantes | Ciclo no grupo 3: recusa |
| Sem equipamento | Setup criado do aparelho | Saídas CH1, CH2, CH3, CH4 (ou "CH3 + CH4" com link) | — |
| Bridge sem link | CH3 e CH4 no mesmo bridge, Link 3–4 desligado | Aviso do bridge na saída | — |
| Equilíbrio de par | Sub (CH3+CH4), ganho −2 | Equilíbrio −2 no CH3 e no CH4; o ganho final dos dois desce 2 dB | Fora de −12 a +12: recusa |
| Bridge inválido | Bridge 2+3 ou canal em dois bridges | Recusa com o motivo | Nada muda |
| Sugestão | Categoria "Outra", canal com passa-baixa 120 Hz | Função sugerida: sub | Usuário escolhe grave: vale grave |
| Tirar módulo | Módulo com 3 alto-falantes | Os 3 ficam sem ligação; aviso | — |
| Folha (ViewModel) | Adicionar alto-falante passivo sem módulo no Setup | Campo "ligado em" sem opções e a frase "Adicione um módulo para ligar o alto-falante." | — |

</intent-contract>

## Code Map

- `app-controle/src/BgsControle.Nucleo/Setups/Setup.cs` -- o record `Setup` (id, nome, quando, `Calibracao`, `Equilibrio`, `Link12`, `Link34`, herdado, nota): ganha a lista de equipamentos; os records novos (equipamento, fonte, ligação, saída do app, dados técnicos) ficam aqui ou em `Equipamentos.cs`.
- `app-controle/src/BgsControle.Nucleo/Setups/Camadas.cs:22-49,106-155` -- `Equilibrio` com quatro valores das saídas 1 e 2 e `TemEquilibrio` que aceita só os índices 0 e 1: passa a ser por canal (0 a 3), com `Somar`, `Subtrair`, `Compor` e `Separar` valendo para os 4.
- `app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs` -- `AjustarEquilibrioAsync(saida, limiter, valor)` (~1215), `EquilibrioEfetivo`/`_pendente` (~428-445), `MudancasDeCalibracao` (~1619-1645, as linhas de equilíbrio), `CriarSetupAsync` (~803), `ParaJson`/`DeJson` (~1985-2090, o equilíbrio e os links), `Herdados()`. Ganha as operações de equipamento (adicionar, editar, tirar), a derivação das saídas e a sugestão de função, sem quebrar "uma operação por vez".
- `app-controle/src/BgsControle.Nucleo/Herdado/ConhecimentoHerdado.cs:47,60-65` -- `EquilibrioCasa` (L/R) e `SetupCasa` com `Link34: false`: passa ao equilíbrio por canal e ao Link 3–4 ligado, mais os equipamentos herdados.
- `app-controle/src/BgsControle.Nucleo/Escrita/LinkCanais.cs` -- `Ligado(ParCanais)`, `Mudou`, `DefinirEstadoAsync` (8c): as saídas do app dependem do link do Setup em uso.
- `app-controle/src/BgsControle.Nucleo/Escrita/EstadoAparelho.cs` -- o espelho (corte lido para a sugestão de função: `040`/`041`/`042`/`043`).
- `app-controle/src/BgsControle.Telas/SetupsViewModel.cs` não existe ainda (só na tentativa da 10); a folha nova segue o modelo de `PresetsViewModel.cs`/`MemoriasViewModel.cs` (folha com nome, erro, confirmar e cancelar).
- `app-controle/tests/BgsControle.Nucleo.Testes/SetupsTestes.cs` e `tests/BgsControle.Telas.Testes/` -- os testes de equilíbrio (saídas 1 e 2) e da composição da FILME mudam para o equilíbrio por canal; a ida e volta da FILME herdada continua igual à leitura de 18/10.

## Tasks & Acceptance

**Execution:**
- `app-controle/src/BgsControle.Nucleo/Setups/Equipamentos.cs` (novo) -- os records e a lógica pura: grupos, categorias sugeridas, função acústica, fontes, validação das ligações (bridge vizinho, um bridge por canal, sem ciclo, sem referência quebrada), a cadeia de alimentação, a derivação das saídas (nome, cor, aviso do bridge) e a sugestão de função.
- `app-controle/src/BgsControle.Nucleo/Setups/Setup.cs`, `Camadas.cs`, `GerenteSetups.cs` -- o Setup com equipamentos; o equilíbrio por canal; as operações de equipamento (guardadas no `setups.json`, avisam no `Mudou`); `Saidas(idSetup)` e `SaidasEmUso`; `AjustarEquilibrioAsync` por saída (par = os dois canais); o formato 2 e a leitura do 1.
- `app-controle/src/BgsControle.Nucleo/Herdado/ConhecimentoHerdado.cs` -- os equipamentos herdados, o Link 3–4 ligado e o equilíbrio por canal.
- `app-controle/src/BgsControle.Telas/EquipamentosViewModel.cs` (novo) -- a lista de equipamentos de um Setup (nome, detalhe "Alto-falante passivo · canal 2 do Módulo genérico · CH2", "genérico") e a folha (grupo, categoria com sugestões e "Outra…", nome, modelo, função com a sugerida, dados técnicos, ligação com as opções válidas, ✓, Cancelar, Tirar com confirmação e os avisos).
- `app-controle/tests/...` -- a matriz inteira contra o simulador, mais os testes da 8c e da 8d ajustados ao equilíbrio por canal.

**Acceptance Criteria:**
- Given o Casa 2.1 conferido, when o Sub (par CH3+CH4) muda o ganho por uma escrita no canal mestre, then o simulador mostra o CH3 e o CH4 com o mesmo valor novo.
- Given a FILME herdada aplicada no Casa 2.1, when o app relê, then o espelho continua igual à leitura de 18/10 nas três camadas (o equilíbrio por canal não muda nada).
- Given o app reaberto, when os arquivos são lidos de novo, then os equipamentos, as ligações, o equilíbrio por canal e os links voltam iguais.

## Spec Change Log

## Review Triage Log

### 2026-10-23 — Review pass
- verdicts: 37 findings — high 0, medium 8, low 25, false 4, maybe-false 0
- findings:
  - `[medium]` `[defer]` (intenção) Fora da lista de equipamentos, a folha do Salvar e a lista da dúvida ainda rotulam por canal ("saída 3", "saída 4"), e um ajuste no Sub aparece em duas linhas — a spec manteve o Salvar "igual, com os 4 canais"; os rótulos por nome da saída vão para a história 10, que monta essas telas
  - `[false]` `[reject]` (intenção) O equilíbrio ficou por canal, não por saída — decisão escrita na spec; o par com link recebe o mesmo valor nos dois canais, e a migração agora iguala
  - `[low]` `[patch]` (intenção) Nome herdado "Sub" contra "Subwoofer genérico" no conhecimento-herdado.md — a tabela do conhecimento-herdado.md foi corrigida para "Sub" (subwoofer, modelo genérico)
  - `[false]` `[reject]` (intenção) O link do Setup passa a valer ao conectar — sem isso o Sub aparecia como uma saída e não escrevia no CH4; é o que "escreve nos dois" pede
  - `[low]` `[reject]` (intenção) Limiares da sugestão de função — batem com o exemplo do EXPERIENCE.md (passa-baixa em 150 Hz → grave ou sub); o Denis pode trocar
  - `[low]` `[reject]` (intenção) Nome sugerido por número e confirmação sem artigo — cosmético
  - `[low]` `[patch]` (intenção) A linha de sucesso da CAP-16 só testada na função pura — teste contra o simulador pelo GerenteSetups.Saidas
  - `[low]` `[patch]` (lacuna) Ida e volta dos equipamentos comparava o JSON consigo mesmo — asserções campo a campo de função, modelo, nota e dados técnicos
  - `[low]` `[patch]` (lacuna) O catch que descarta um equipamento ilegível nunca rodava — teste com "canal": "1"
  - `[low]` `[patch]` (lacuna) Corte com o filtro em OFF sem teste — teste junto da correção da sugestão
  - `[medium]` `[patch]` (cego) A sugestão de função lia o espelho ao vivo, e não o Setup mostrado — Corte e FuncaoSugerida leem a calibração guardada do Setup; testes
  - `[medium]` `[patch]` (cego) Editar um módulo que deixaria alto-falantes sem ligação era recusado sem dizer qual — a recusa nomeia o alto-falante; testes
  - `[low]` `[patch]` (cego) Sanear não tirava os bridges inválidos e o módulo inteiro caía — só o bridge inválido sai; teste com o JSON
  - `[low]` `[patch]` (cego) Id vazio com a mensagem de id repetido — mensagem própria
  - `[low]` `[patch]` (cego) Migração deixava o par com link com equilíbrios diferentes — a migração iguala ao mestre
  - `[medium]` `[defer]` (cego) Rótulos do Salvar por canal — o mesmo adiamento da primeira linha
  - `[low]` `[reject]` (cego) Notas do Casa 2.1 não acompanham as edições dos equipamentos — a nota é o conhecimento herdado, de 18/10, e não descreve o estado atual
  - `[medium]` `[defer]` (cego) Na dúvida ou sem Setup, o link do Setup anterior continua valendo — comportamento do link desde a 8c; qual link vale sem Setup em uso é decisão do Denis (vai para a história 10)
  - `[low]` `[patch]` (cego) Aviso do bridge só nos pares 1–2 e 3–4 — avisos para bridge com canais de pares diferentes e com um canal só; testes
  - `[low]` `[patch]` (cego) Função sugerida e mostrada para equipamento que não toca — só o grupo 1 e os alto-falantes passivos têm função
  - `[false]` `[reject]` (cego) Spec e código divergem nos nomes e ids herdados — a correção seria editar a spec desta história; o documento herdado foi alinhado
  - `[low]` `[patch]` (cego) Sem validação de texto e faixa nos dados técnicos; canal nulo derrubava o equipamento — limite de 120 caracteres, faixa mínima abaixo da máxima, canal nulo = sem ligação; testes
  - `[low]` `[reject]` (cego) Trocar o grupo na folha perde as ligações — raro; a folha pode ser cancelada
  - `[low]` `[reject]` (cego) A escrita da IA espelhada no CH4 não aparece na resposta — comportamento do link desde a história 6
  - `[low]` `[patch]` (borda) A folha confirmava no Setup que estivesse mostrado na hora do ✓ — guarda o id ao abrir e recusa se mudou; teste
  - `[low]` `[reject]` (borda) Setup apagado com a folha aberta — a tela do Setup é a única porta e não fica aberta com o Setup apagado
  - `[low]` `[patch]` (borda) Texto da confirmação ficava depois de um Tirar que falhou — os textos são limpos
  - `[low]` `[patch]` (borda) Número de canais do módulo sem limite — 1 a 16; teste
  - `[medium]` `[patch]` (borda) Sugestão pelo corte de outro Setup — o mesmo patch da sugestão
  - `[low]` `[patch]` (borda) Bridge com canais de pares diferentes sem aviso — o mesmo patch dos avisos
  - `[medium]` `[defer]` (borda) Rótulos do Salvar por canal — o mesmo adiamento
  - `[low]` `[patch]` (borda) Casa 2.1 migrado com o Link 3–4 e equilíbrios diferentes — a migração iguala; as memórias aplicam cada canal sem o espelho do link
  - `[low]` `[patch]` (borda) Sanear derrubava o módulo por um bridge inválido — o mesmo patch
  - `[low]` `[patch]` (borda) Sanear quebrava com ids repetidos — fica o primeiro de cada id
  - `[low]` `[patch]` (borda) Nota e descrição de via sem limite — o mesmo patch de validação
  - `[false]` `[reject]` (borda) Nome herdado do sub — alinhado no conhecimento-herdado.md
  - `[medium]` `[defer]` (borda) A folha do Salvar mostra linhas por canal — o mesmo adiamento

## Design Notes

- **Por que o nome vem dos que tocam:** a saída é o canal, mas o Denis pensa no que ouve (JBL R, Sub). O módulo é caminho e só dá nome quando nada que toca está ligado.
- **Por que o equilíbrio fica por canal:** um par com link é uma saída só, com um equilíbrio só; guardar por canal, com o mesmo valor nos dois, mantém o `Somar`/`Subtrair` da 8c simples e resolve a pendência "link com equilíbrio".
- **Exemplo do Casa 2.1 (formato 2, abreviado):** `equipamentos: [{id:"modulo", grupo:"amplificador", nome:"Módulo genérico", canais:[{n:1,fonte:"CH1"},…,{n:4,fonte:"CH4"}], bridges:[[3,4]], ganho:19.6}, {id:"jbl-l", grupo:"alto-falante", categoria:"médio-grave", nome:"JBL L", modelo:"JBL 4TRMS80", impedancia:3.6, ligado:{equipamento:"modulo", canal:1}}, …, {nome:"Subwoofer genérico", ligado:{equipamento:"modulo", bridge:[3,4]}}]`.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

## Auto Run Result

Status: done

**O que entrou.**
- **Equipamentos do Setup:** pelo modelo de ligação aprovado em 23/10:
  - quatro grupos mais os alto-falantes passivos;
  - categorias abertas, função acústica e dados técnicos opcionais, e "genérico" quando não se sabe;
  - cada canal do processador para uma ou mais entradas, com o repasse pelo grupo 3, os canais e o bridge do módulo e os alto-falantes nas saídas;
  - a validação recusa bridge fora de par, ciclo e ligação quebrada e nomeia o alto-falante que impede uma edição.
- **Saídas do app:** vêm do Setup: um canal, ou um par com link que escreve nos dois. Cada uma tem nome (pelos equipamentos que tocam), cor do canal mestre e avisos de bridge.
- **Equilíbrio:** passa a ser por canal no lugar do L/R; um par recebe o mesmo valor nos dois canais.
- **Sugestão de função:** pela categoria ou pelo corte guardado no Setup.
- **Casa 2.1 herdado:** Módulo genérico com bridge 3+4, JBL L, JBL R e Sub, e o Link 3–4 ligado. O link do Setup passa a valer quando a conferência confere.
- **`setups.json`:** formato 2, que ainda lê o formato 1 (a migração iguala o equilíbrio dos pares com link).
- **Telas:** o `EquipamentosViewModel`, com a lista e a folha.

**Arquivos:**
- `Setups/Equipamentos.cs` (novo);
- `Setup.cs`, `Camadas.cs` e `GerenteSetups.cs`;
- `ConhecimentoHerdado.cs`;
- `BgsControle.Telas/EquipamentosViewModel.cs` (novo) e `MauiProgram.cs`;
- testes: `EquipamentosTestes`, `SetupsEquipamentosTestes` e `EquipamentosViewModelTestes` (novos), mais os ajustes em `SetupsTestes`, `PortaTestes` e `SalvarViewModelTestes`;
- documentos: `conhecimento-herdado.md` e `deferred-work.md`.

**Revisão:** 37 achados.
- **Corrigidos:** 2 entradas medium (a sugestão pelo Setup mostrado; a recusa que nomeia o alto-falante) e as low de validação, bridge, migração, folha e testes.
- **Adiados (2, medium):** os rótulos do Salvar por saída e o link sem Setup em uso, os dois para a história 10.
- **Rejeitados:** os demais, com o motivo no triage log.

**Revisão de acompanhamento recomendada:** sim (2 entradas medium corrigidas). O risco que ficou sem segunda leitura é a leitura do `setups.json` formato 2 com dados estragados (os caminhos de saneamento novos) e o link aplicado ao conectar.

**Verificação:**
- `dotnet test` do Nucleo.Testes: 983 passam.
- `dotnet test` do Telas.Testes: 341 passam.
- `dotnet build` do App para `net10.0-android`: 0 erros e 0 avisos.
- Nada foi testado no celular nem no processador.
