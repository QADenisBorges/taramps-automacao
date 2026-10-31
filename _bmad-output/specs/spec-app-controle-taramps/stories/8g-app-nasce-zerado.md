---
title: 'História 8g: app nasce zerado'
type: 'feature'
created: '2026-10-31'
baseline_revision: 'be02b0b79e17fe9d386072d696af8407c9ced9b2'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/failure-modes.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** O app traz embutido o conhecimento herdado (`ConhecimentoHerdado`):
- o Setup Casa 2.1 com fichas;
- 3 modelos na base;
- as notas SPL, MUSICA ORIG e FILME das memórias do aparelho;
- as seções de `conhecimento-herdado.md`, com as preferências do Denis.

O Casa existe sempre, mesmo sem `setups.json`. A CAP-6 (decisão do Denis de 30/10/2026) pede um app que nasce zerado, só com o protocolo. A CAP-16 dá ao Setup o ambiente (sala, carro, ao ar livre ou outro), que é um dado físico.

**Approach:**
- Tirar `Herdado/` do núcleo e do app.
- Levar os dados reais para uma fixture de teste no `BgsControle.Simulador`.
- Fazer o gerente nascer e voltar ao vazio.
- Pôr a primeira conexão sem Setup na escolha, com o mudo.
- Acrescentar o ambiente ao Setup como campo opcional do formato 3.

## Boundaries & Constraints

**Always:**
- **Primeira abertura:** sem nenhum arquivo, há 0 Setups, 0 equipamentos, 0 modelos na base, 0 memórias e 0 presets.
  - Nenhum texto, selo ou tela vem do herdado: nem notas das memórias do aparelho, nem seções, nem o selo "HERDADO".
  - Ficam as chaves de acesso de `chaves-de-acesso.md` e os padrões do app, como "Animação ao criar o som". Isso é protocolo e segurança, não conhecimento herdado.
- **Arquivo ilegível ou sem nenhum Setup:** o `setups.json` estragado volta ao vazio, sem erro, e registra no log. O `modelos.json` estragado volta à base vazia.
- **Sem migração que apague dados:**
  - Um `setups.json` que já tem o Casa 2.1 (o id herdado) carrega como um Setup comum: pode ser apagado e renomeado, sem nota e sem selo.
  - Os formatos 1 e 2 continuam sendo lidos como hoje, mas sem pôr equipamentos ou fichas herdadas.
  - O `playlists.json` formato 1 nomeia as pendentes como "Memória {n}".
- **Primeira conexão sem nenhum Setup** (regra 5 de `failure-modes.md`; CAP-16 "sem memória para comparar"):
  - O app muta os 4 canais e entra na dúvida (`EmEscolha`).
  - A IA fica bloqueada (`MotivoBloqueioIa` = `MsgDuvida`), e a escolha abre.
  - A frase é "Nenhum Setup cadastrado. Crie um Setup do aparelho ou siga sem Setup.".
  - "Escolher o Setup" some da escolha quando não há nenhum Setup.
  - "Criar Setup novo" e "Seguir sem Setup" desmutam, como hoje. Uma nova conexão sem Setup volta à escolha.
- **Ambiente do Setup:** o enum `AmbienteSetup { Sala, Carro, ArLivre, Outro }` usa os rótulos "Sala", "Carro", "Ao ar livre" e "Outro".
  - É opcional: null quer dizer "não informado", e não há padrão.
  - No `setups.json`, entra como campo `ambiente` (`sala`, `carro`, `ar-livre`, `outro`) do formato 3. Ele só é gravado com valor, e um texto que não vale fica null. O formato não sobe.
  - Na tela, é escolhido em pílulas na folha "Criar Setup novo" e editado na tela Setup.
  - Sai no `GET /estado` como `setup.ambiente` (o nome do JSON, ou null).
- **Os dados reais vão para a fixture `CasaDoDenis`**, no `BgsControle.Simulador`: o Casa 2.1, as fichas, os 3 modelos e a leitura de 18/10.
  - O `CenarioFilme` e as bancadas semeiam a partir dela, com `setups.json`, `presets.json` e também `modelos.json`.
  - Os testes que conferiam o herdado passam a conferir o vazio, ou passam a usar a fixture.

**Never:**
- Código que apague ou reescreva sozinho um arquivo do usuário só por ele conter o Casa.
- Subir o `Formato` dos Setups.
- Mudar o `presets.json` ou o `Preset.Herdado`, que continua sempre falso.
- A temperatura ambiente do Rendimento.
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Primeira abertura | Nenhum arquivo | 0 Setups, 0 modelos, 0 memórias, 0 presets; Início "Sem Setup ›" | — |
| Primeira conexão | 0 Setups, conectar | Muta os 4 canais; dúvida; a escolha abre com a frase de nenhum Setup e sem "Escolher o Setup"; a porta dá 409 com `MsgDuvida` | — |
| Criar Setup | Na escolha: "Sala", nomes "Casa" e "SOM 1" | O Setup tem o ambiente Sala, o som do aparelho vira a memória SOM 1, desmuta | Sem ambiente: cria com null |
| Seguir sem Setup | Na escolha | Desmuta; as memórias ficam travadas; a próxima conexão volta à escolha | — |
| Arquivo com o Casa | `setups.json` formato 3 com o id herdado | Carrega como Setup comum; apagar funciona | — |
| Estragado | `setups.json` ou `modelos.json` ilegível | Vazio, sem erro | Log |
| Ambiente no JSON | `"ambiente":"carro"` / `"ambiente":"lua"` / ausente | Carro / null / null | — |
| Editar o ambiente | Tela Setup, tocar "Ao ar livre" | Grava e relê ArLivre | Falha ao gravar: mensagem; o valor não muda |
| Porta | Setup com ambiente Carro em uso | `GET /estado` traz `"ambiente":"carro"` | Sem Setup: `setup` como hoje |
| Fixture | `Bancada(filme:true)` | O Casa da fixture, a FILME e os 3 modelos semeados | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Herdado/ConhecimentoHerdado.cs`:
  - o Casa (33–35, 56, 196–197, 232), as fichas e os equipamentos (85–177), os `Modelos` (188–193) e as `Notas` (199–216);
  - `Presets` (229, vazio), `Secoes` (238–303) e `LeituraFilme` (223–226, 309–333);
  - o recurso embutido em `BgsControle.Nucleo.csproj:13-21`, que só os testes usam. Este arquivo sai.
- `src/BgsControle.Nucleo/Setups/GerenteSetups.cs`:
  - `_dados = Herdados()` (200), `Herdados()` (2099–2104), `CarregarAsync` (2170–2187), `DeJson` (2329–2391: null sem Setups em 2360, `Herdado`/nota em 2347/2354, a migração com `EquipamentosCasa` em 2350–2351 e 2398–2409);
  - `NomeGuardado` (2497–2498), `ReferenciaEm` (2090–2093), `OrdemDeAdocao` (1884), `ApagarSetupAsync` e `MsgSetupHerdado` (1252–1253, 100);
  - `ParaJson` (2235–2288; o ambiente perto de "nome", em 2250–2253);
  - `ConferirAsync` (651–760; o ramo `referencia is null` em 689–699 não muta nem abre a dúvida, e precisa disso), `MutarAsync(null, …)` (já aceita);
  - `CriarSetupAsync` (1032–1092) e `RenomearSetupAsync` (1217–1240), modelo para `DefinirAmbienteAsync`;
  - `MotivoBloqueioIa` (494–507) e `SeguirSemSetupAsync` (1098–1127);
  - `GerentePresets.cs:180, 680, 685` soma o `ConhecimentoHerdado.Presets`.
- `src/BgsControle.Nucleo/Setups/Setup.cs:16-28`: `Herdado` e `Nota` saem, e entra o `Ambiente`. O modelo do enum e do JSON é `FuncaoAcustica` (`Equipamentos.cs:29-39`), com `Ligacoes.Funcoes`/`RotuloFuncao` (451–467) e `NomeFuncao`/`FuncaoDe` (`GerenteSetups.cs:2513-2526`, 2541, 2656).
- `src/BgsControle.Nucleo/Setups/BaseModelos.cs:32, 131-145`: a base nasce de `ConhecimentoHerdado.Modelos`.
- `src/BgsControle.Nucleo/Playlists/GerentePlaylists.cs:424`: o formato 1 usa `NotaMemoria(n).NomeNoPrototipo`.
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs:1098-1136`: o objeto `setup` do `/estado`. A ajuda do `/estado` fica em 1360.
- Telas:
  - `HerdadoViewModel.cs`: sai, junto com o registro em `MauiProgram.cs:192`;
  - o selo "HERDADO": `SetupViewModel.cs:20,33`, `SetupsViewModel.cs:16,20,41`, `EquipamentosViewModel.cs:443`, `SetupEscolhaPage.xaml:114-118`, `SetupPage.xaml:50-52`;
  - `SetupViewModel`: `FraseSemReferencia` (71, 170–172), `AbrirCriar`/`ConfirmarCriarAsync` (113, 340–361) e as constantes (60–69);
  - `SetupEscolhaPage.xaml:85-87` ("Escolher o Setup") e 139–165 (a folha de criar);
  - `EquipamentosViewModel.cs:22, 761` e `SetupPage.xaml:19` têm as pílulas `EscolhaViewModel`, que servem de modelo para o ambiente;
  - `SetupsViewModel.Lembrete` (75).
- Comentários que citam o herdado: `PresetsPage.xaml:4`, `SetupsPage.xaml:3`, `SetupPage.xaml:4`, `MemoriasPage.xaml:4`, `PresetsFolhaView.xaml:4`, `BaseModelos.cs:20-21` e `MauiProgram.cs:102`.
- Testes:
  - `tests/BgsControle.Simulador/CenarioFilme.cs:36` (`Herdados()`) e `BgsControle.Simulador.csproj:16-18`, onde o recurso da leitura já existe;
  - a `Bancada` em `ControleParametroViewModelTestes.cs:45-87`;
  - os que dependem do herdado: `SetupsTestes` (95 `SemArquivos`, 212, 256, 1090, 1305, 1448, 1480, 1530, 1610…), `PrimeiraAberturaTelasTestes` (28, 42), `PresetsViewModelTestes:65`, `SetupsEquipamentosTestes`, `EquipamentosTestes`, `EquipamentosViewModelTestes` (339, 468, 504, 616, 652), `EsquemaTestes`, `TelasParte1Testes`, `TelasSaidasDoSetupTestes`, `MemoriasViewModelTestes:49`, `PresetsTestes:199,913`;
  - `ArquiteturaTestes.cs:50`.

## Tasks & Acceptance

**Execution:**
- `tests/BgsControle.Simulador/CasaDoDenis.cs` (novo): os dados reais (o Casa, as fichas, os modelos e a leitura de 18/10), copiados de `ConhecimentoHerdado`. `CenarioFilme` e as bancadas usam esta fixture e semeiam também o `modelos.json`.
- `src/BgsControle.Nucleo/Herdado/` e o `EmbeddedResource` do `Nucleo.csproj`: saem.
- `Setup.cs`, `GerenteSetups.cs`, `BaseModelos.cs`, `GerentePresets.cs` e `GerentePlaylists.cs`:
  - o vazio no começo e no arquivo estragado;
  - zero Setups aceito no JSON;
  - sem `Herdado`, sem nota e sem `MsgSetupHerdado`;
  - o ambiente (enum, rótulos, JSON e `DefinirAmbienteAsync`; `CriarSetupAsync` ganha `ambiente`);
  - a conferência sem Setup muta e entra na dúvida;
  - os formatos antigos sem equipamentos herdados;
  - as pendentes "Memória {n}".
- `PortaComandos.cs` e `porta-de-comandos.md`, se citarem o `/estado`: `setup.ambiente` e a ajuda.
- Telas e App:
  - sai o `HerdadoViewModel` e o selo;
  - entra a frase de nenhum Setup;
  - "Escolher o Setup" fica escondido sem Setups;
  - entram as pílulas do ambiente na folha de criar e na tela Setup;
  - entra o texto de vazio em Configurações › Setups: "Nenhum Setup. Conecte o aparelho e toque em "Sem Setup ›" no Início para criar.";
  - saem os comentários do herdado.
- Testes:
  - um teste por linha da matriz;
  - `ArquiteturaTestes`: nenhum texto de tela ou constante pública das Telas cita "Casa 2.1", "JBL", "Protech" ou "SD800";
  - os testes do herdado migram para o vazio ou para a fixture.

**Acceptance Criteria:**
- Given uma instalação nova, when o usuário conecta, cria o Setup "Casa" com o ambiente Sala e a primeira memória, then o Início mostra essa memória em uso, e nada do Casa 2.1, das JBL ou das notas aparece em nenhuma tela.
- Given o núcleo compilado, when se procura `ConhecimentoHerdado`, then ele não existe em `src/`.
- Given os testes que já existem, when a 8g entra, then todos passam, com os dados reais vindo só da fixture, e o build Android sai sem erro nem aviso.

## Spec Change Log

## Review Triage Log

### 2026-10-31 — Review pass
- verdicts: 30 findings — high 0, medium 4, low 23, false 3, maybe-false 0
- findings:
  - `[low]` `[reject]` Intent: muta em toda conexão com 0 Setups, também depois de "Seguir sem Setup" — a CAP-16 manda cair na escolha sem Setup cadastrado, e a spec justifica o mudo (Design Notes); sem Setup não há memória para comparar.
  - `[low]` `[reject]` Intent: o mundo padrão dos testes semeia a fixture — a intenção permite as fichas reais nos testes, e o vazio tem testes próprios (`SemArquivos`, `AppZeradoTelasTestes`, `zerado:true`).
  - `[low]` `[reject]` Intent: sobram constantes privadas do Setup antigo e exemplos em comentários — as constantes servem ao "sem migração que apague dados" (o patch do nome com ponto as tira); comentários não aparecem no app.
  - `[false]` `[reject]` Intent: o ambiente opcional e o `setup.ambiente` no `/estado` vão além da intenção — a spec decidiu o opcional (sem caso fixo), e expor um dado físico na porta não muda nada da intenção.
  - `[low]` `[reject]` Intent: os formatos 1 e 2 mudam de sentido (sem equipamentos herdados; playlists renomeadas) — nada é apagado, e a 12b reinstala o app limpo.
  - `[low]` `[reject]` Intent: a primeira abertura no aparelho não é conferida — fica na 12b, como diz o contexto.
  - `[low]` `[patch]` Verification gap: a troca com 0 Setups ("Sem Setup ›" depois de seguir sem Setup) não tem teste — grupo T: teste novo, do Trocar até o Criar.
  - `[medium]` `[patch]` Verification gap: a dúvida interrompida com 0 Setups não tem teste — grupo T: teste novo da reconexão sem mutar de novo.
  - `[medium]` `[patch]` Verification gap: o mudo recusado com 0 Setups não tem teste (a tela diria que mutou) — grupo T: teste novo com a chave desligada.
  - `[low]` `[reject]` Verification gap (outro): um formato antigo com a lista vazia passa a valer e descarta as pendentes — os formatos 1 e 2 sempre tinham o Casa; não aparece no uso, e o conserto pede outro ramo.
  - `[low]` `[reject]` Verification gap (outro): a spec manda registrar no log o arquivo sem nenhum Setup, e o código não registra — um arquivo válido sem Setups é o estado normal depois de apagar o último; o conserto seria editar a spec.
  - `[low]` `[reject]` Blind: as playlists antigas "Memória {n}" quase nunca são adotadas — só o `playlists.json` formato 1, de antes da 8c; a 12b reinstala limpo, e os nomes antigos eram do herdado.
  - `[low]` `[patch]` Blind: a fixture copia o `LeitorCompleto.Casar` e já divergiu (sem o `p.Extra`) — grupo P: `InternalsVisibleTo` para o Simulador e o método real.
  - `[medium]` `[patch]` Blind: faltam os testes da dúvida interrompida e do mudo recusado com 0 Setups — grupo T.
  - `[low]` `[patch]` Blind: a troca com 0 Setups não tem teste — grupo T.
  - `[low]` `[patch]` Blind: o `MsgAmbienteDefinido` nunca aparece, e o toque duplo não espera — grupo A: retorna se já está agindo e mostra a mensagem.
  - `[low]` `[patch]` Blind: as pílulas do ambiente não têm descrição para o TalkBack (o modelo duplicado fica) — grupo S: a descrição com o rótulo e o estado.
  - `[low]` `[patch]` Blind: o rótulo do ambiente está duplicado e com estilos diferentes — grupo R: uma constante e o mesmo jeito.
  - `[low]` `[patch]` Blind: o teste de arquitetura quebra com qualquer recurso futuro e não varre as mensagens do núcleo — grupo Q: confere só o recurso da leitura e varre as constantes públicas do núcleo.
  - `[low]` `[reject]` Blind: o EXPERIENCE.md de 21/10 ainda fala do Casa herdado — é o UX antigo, substituído pelo de 30/10, e não é lido pelo app.
  - `[medium]` `[patch]` Blind: o sinal de sucesso pede o Setup "Casa 2.1", e a regra do nome recusa o ponto — grupo N: o nome do Setup aceita ".", e a exceção do Setup antigo sai.
  - `[low]` `[reject]` Blind: o caminho de `GerentePlaylists.cs` no Code Map está errado — o conserto é editar a spec.
  - `[low]` `[patch]` Blind: sobra "herdado" em nomes de teste e variáveis, o exemplo fixo no comentário da `MemoriasPage` e o comentário errado do ramo sem Setup — grupo C: renomeados e corrigidos.
  - `[low]` `[patch]` Blind: o braço `_ =>` grava um ambiente indefinido como "outro" — grupo E: um braço explícito, exceção no indefinido e recusa no criar e no definir.
  - `[low]` `[reject]` Blind: a porta não define o ambiente — a intenção não pede, e a porta do Agente é da 14a–14f.
  - `[low]` `[patch]` Edge: o segundo toque na pílula durante a gravação — grupo A.
  - `[low]` `[patch]` Edge: um `AmbienteSetup` indefinido vira "outro" — grupo E.
  - `[false]` `[reject]` Edge: um celular sem `modelos.json` perde os 3 modelos — é a CAP-6 (sem base de modelos), não um defeito.
  - `[false]` `[reject]` Edge: uma instalação sem `setups.json` perde o Casa — é a CAP-6, e a 12b reinstala limpo.
  - `[low]` `[patch]` Edge: o resumo do `DeJson` dos presets ainda cita o herdado — grupo C: o comentário foi atualizado.

## Design Notes

- **Por que muta sem Setup:** a CAP-16 e a regra 5 mandam mutar quando não há memória para comparar. Sem Setup, também não há, e o processador pode estar com qualquer corte, inclusive um tweeter sem passa-alta.
- **Por que o ambiente não tem padrão:** o app é para qualquer instalação. Um padrão "Sala" seria um caso fixo, e o Agente precisa saber quando o dado não foi informado.
- **Por que o formato não sobe:** subir o formato dispara a migração que descarta memórias e playlists (`GerenteSetups.cs:2206-2224`). Um campo opcional não precisa disso.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.
- `grep -rn "ConhecimentoHerdado" app-controle/src` -- expected: nada.

**Manual checks (if no CLI):**
- No aparelho, a 12b reinstala o app limpo e confere a primeira abertura.

## Auto Run Result

Status: done

**O que entrou.** O app nasce zerado (CAP-6):
- o `ConhecimentoHerdado` e o `HerdadoViewModel` saíram, junto com o selo HERDADO e a leitura embutida no núcleo;
- o gerente dos Setups e a base de modelos começam vazios, e um arquivo estragado também volta ao vazio, com uma linha no log;
- a primeira conexão sem Setup muta os 4 canais, entra na dúvida, trava a porta e mostra "Nenhum Setup cadastrado…", sem o "Escolher o Setup";
- o Setup ganhou o ambiente opcional (Sala, Carro, Ao ar livre, Outro): no `setups.json` formato 3, nas pílulas da folha de criar e da tela Setup, e no `GET /estado`;
- o nome do Setup aceita ".", então dá para criar o "Casa 2.1" do zero;
- os dados reais foram para a fixture `CasaDoDenis`, no Simulador.

**Arquivos** (em `app-controle/`):
- Núcleo:
  - `Herdado/ConhecimentoHerdado.cs` (apagado) e o `Nucleo.csproj` (sem o recurso; com `InternalsVisibleTo` para o Simulador);
  - `Setups/Setup.cs`: `AmbienteSetup` e `Ambientes`; saíram o `Herdado` e a `Nota`;
  - `Setups/GerenteSetups.cs`: o vazio, a conferência sem Setup, o ambiente e o nome com ponto;
  - `Setups/BaseModelos.cs`, `Presets/GerentePresets.cs` e `Player/GerentePlaylists.cs`;
  - `Porta/PortaComandos.cs`: o `setup.ambiente`.
- Telas:
  - `HerdadoViewModel.cs` (apagado);
  - `SetupViewModel.cs`, `EquipamentosViewModel.cs`, `SetupsViewModel.cs` e o `EscolhaViewModel` (descrição).
- App:
  - `SetupEscolhaPage.xaml`, `SetupPage.xaml`, `SetupsPage.xaml`, `MauiProgram.cs` e os comentários das páginas.
- Testes:
  - `BgsControle.Simulador/CasaDoDenis.cs` (novo) e `CenarioFilme.cs`;
  - a `Bancada`, com o `zerado:true` e a semente do `modelos.json`;
  - `AppZeradoTelasTestes` e os testes do vazio, do ambiente e da migração;
  - `ArquiteturaTestes`: nada do herdado nas telas nem nas constantes do núcleo.

**Revisão.** 30 achados: 4 medium, 23 low e 3 false.
- **Patches (9 grupos):**
  - **N (medium):** o nome do Setup com ponto.
  - **T (medium):** os testes da dúvida interrompida, do mudo recusado e da troca com 0 Setups.
  - **P:** a fixture usa o `Casar` real.
  - **A:** a pílula espera a gravação e mostra a mensagem.
  - **E:** o ambiente indefinido é recusado.
  - **S:** o TalkBack das pílulas.
  - **R:** um rótulo só.
  - **Q:** o teste de arquitetura.
  - **C:** nomes e comentários.
- **Nada diferido.**
- **Rejeitados, com o motivo no Review Triage Log:**
  - o mudo a cada conexão sem Setup;
  - a fixture como padrão dos testes;
  - as constantes e os comentários;
  - o ambiente na porta;
  - os formatos antigos;
  - o aparelho (12b);
  - a lista vazia de formato antigo;
  - o log do arquivo sem Setups;
  - as playlists "Memória {n}";
  - o UX de 21/10;
  - o caminho do Code Map;
  - a porta que não define o ambiente;
  - e os 3 false (CAP-6).

**Revisão de acompanhamento: recomendada.** Foram 2 entradas medium com patch (N e T). O risco que ninguém conferiu é um `setups.json` real do celular, com o Casa antigo e as playlists, carregado pela versão nova. Ele só foi exercido com arquivos montados nos testes.

**Verificação:**
- `dotnet test` Núcleo: 1072 de 1072.
- `dotnet test` Telas: 476 de 476.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- `grep ConhecimentoHerdado app-controle/src` não acha nada.
- A matriz tem pelo menos um teste por linha.
- Nada foi visto num celular: a 12b reinstala o app limpo.

**Decisões a conferir com o Denis:**
- tocar de novo na pílula escolhida deixa o ambiente sem informar;
- com Setups cadastrados e nenhum usado por último, a conferência segue usando o primeiro, como antes;
- "Seguir sem Setup" muta de novo a cada conexão enquanto não houver Setup.
