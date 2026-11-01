---
title: 'História 13c: níveis Baixo a Máximo'
type: 'feature'
created: '2026-11-01'
baseline_revision: '5151d3d307f07fc4ae1d01565b69b1f5b8973983'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-rendimento/limites-e-niveis.md'
  - '{project-root}/_bmad-output/specs/spec-rendimento/telas-rendimento.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** A 13a dá o limite de cada saída e a 13b dá o ganho e o nível do Bluetooth medidos. Mas o master ainda é posto à mão, e a memória não tem nível de rendimento. O usuário não consegue pedir "Alto" e ter o máximo de som sem passar do teto (spec-rendimento CAP-4).

**Approach:**
- Um cálculo no núcleo (`Rendimento/PiorCaso.cs`) simula em frequência a cadeia da memória e do preset para um tom de 0 dBFS e acha o maior master que deixa cada saída no teto do nível, menos a margem.
- A memória do celular ganha o nível e um master por preset (`setups.json` formato 4, com migração que não perde nada).
- Um `GerenteNiveis` refaz a conta na hora quando a cadeia, as 15 bandas, a ficha ou a calibração mudam, e escreve o master.
- O Máximo passa pela folha de risco.
- A cerca da 13a passa a seguir o nível.

## Boundaries & Constraints

**Always:**
- **Os níveis** (`NivelRendimento`) são Baixo, Médio, Alto e Máximo, por memória do celular. Null = sem nível, e o master é do usuário, como hoje. O Extra existe só como linha apagada com o motivo ("Falta analisar a playlist."): é da 13d e da 13f.
  - Baixo: teto = min(T, X, C) − 6 dB.
  - Médio: teto = min(T, X, C) − 3 dB.
  - Alto: teto = min(T, X, C) − 1 dB.
  - Máximo: teto = min(X, C) − 1 dB.
  
  Os limites T, X e C são os da 13a, com o X calculado pelo corte da própria memória.
- **O pior caso** é o maior nível na saída do processador, em dB da escala do limitador (0 dB = 2,0 V), de um tom de 0 dBFS em qualquer frequência. A grade é logarítmica, de 10 Hz a 20 kHz, com pelo menos 2000 pontos. O nível é a soma:
  - o nível do Bluetooth do Setup (13b);
  - o EQ de entrada (030–032): sino com a = 10^(g/40), (s² + s·a/Q + 1)/(s² + s/(a·Q) + 1);
  - as 15 bandas do preset (020): um sino por banda em `Catalogo.BandasHz`, com Q = 2,1 [ASSUMPTION: o Q do gráfico; a 12d mede];
  - a rota (010): "a" ou "b" somam 0 dB, e "a+b" soma +6 dB [ASSUMPTION: soma coerente, o pior caso];
  - o ganho (081) do canal;
  - o EQ de saída (090–092), também em sino;
  - os cortes (040–043), em módulo LR/BT, como na 13a/13b;
  - o master (080).
  
  O limitador não entra: ele é a segurança, e o master é posto antes dele.
- **O master do nível:** master = min sobre as saídas calibradas e com limite de (tetoDb − margem − picoSemMaster), arredondado para baixo no passo 0,1, dentro de −80..0.
  - Se a conta passar de 0, o master fica em 0, e o nível avisa "O nível não chega ao teto: falta ganho na memória." com a saída.
  - O Máximo usa min(X, C) − 1.
- **Quando o nível é oferecido:** só com o Setup em uso, todas as saídas com alto-falante passivo calibradas (13b) e o `NivelBluetooth` medido. Senão, cada linha fica apagada com o motivo:
  - "Calibre a saída {nome}.";
  - "Meça o nível do Bluetooth.".
- **Escolher o nível** (tela): grava o nível na memória e põe o master da combinação memória + preset em uso. O Máximo pede antes a folha "Passa da potência da ficha. Seguro com música; um tom contínuo ou música muito comprimida por muito tempo podem aquecer a bobina.", com Confirmar e Cancelar. "Sem nível" volta a null, sem mexer no master.
- **Um master por preset:** a memória guarda `Masters` (id do preset, ou "" para sem preset → master).
  - Aplicar a memória e trocar o preset escrevem o master recalculado da combinação nova e guardam o valor.
  - O master guardado é só registro: vale sempre a conta na hora.
- **Refeito na hora** (`GerenteNiveis`), com a memória em uso com nível e sem marca da cópia. Isso acontece quando:
  - muda um endereço da cadeia no espelho (030–032, 020, 010, 081, 090–092 e 040–043);
  - muda a ficha ou o ganho medido do Setup;
  - muda o `NivelBluetooth`;
  - muda o preset.
  
  O app escreve o novo master, com a origem App, se ele difere do atual em 0,1 dB ou mais. Baixar e subir valem do mesmo jeito.
- **O master à mão** com um nível em uso: abaixo da conta passa, porque baixar é seguro. Acima da conta, é recusado na tela e na porta: "Nível {nome}: o master vai até {v} dB.". O conserto é recusar antes de enviar, como a cerca.
- **A cerca da 13a com o nível:** com a memória em uso no Máximo, o teto do limitador é min(X, C). Sem nível, ou do Baixo ao Alto, é min(T, X, C). A volta à cópia usa o teto do nível da memória em uso.
- **O formato 4 do `setups.json`:** a memória ganha `nivel` (`baixo`, `medio`, `alto`, `maximo` ou ausente) e `masters` ({idPreset → dB}).
  - A migração do 3 para o 4 mantém tudo: Setups, memórias, botões, recentes, playlists e marca. Nada some.
  - Os formatos 1 e 2 continuam com a migração antiga.
- **A porta:** o `GET /estado` traz, em cada memória, `nivel` e `master` do preset em uso. O `GET /limites` traz `nivel` e `master` da memória em uso. Escolher o nível pela porta não existe nesta história.
- **ViewModel `NivelViewModel`:** a folha "Nível da memória", com uma linha por nível, assim:
  - o texto da linha é "Alto · até 16 V nas JBL L e JBL R · qualquer fonte", com o teto em volts da saída que manda;
  - o Máximo vem com o aviso de calor;
  - o Extra vem apagado;
  - o motivo aparece quando a linha está apagada;
  - escolher pede a folha no Máximo;
  - "Sem nível" volta a null.
  
  Nenhum texto contém "IA".

**Never:**
- XAML (13g).
- O Extra de verdade: análise, teto por picos, térmico e "revalidar" (13d a 13f).
- Mexer no limitador da memória para pôr o nível.
- Perder dados na migração.
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| JBL no Baixo | Memória plana com o ganho +9, JBL LR24 em 200 Hz, BT −13 dB, ganho ×19,5 | Pior caso da JBL = 17,9 V − 6 dB (±0,2 dB); o master posto | — |
| Médio e Alto | O mesmo | 17,9 V − 3 dB e − 1 dB | — |
| Máximo | O mesmo, confirmar a folha | min(22,2; 23) − 1 dB; cerca em min(X, C) | Cancelar: nível igual |
| Reforço no EQ | +3 dB no EQ de saída da JBL, no pico | O master baixa 3 dB na hora | — |
| Trocar o preset | Preset com +4 dB em 250 Hz | O master vai para a conta da nova combinação; `masters` guarda os dois | — |
| Sem calibração | Uma saída sem ganho medido | As linhas apagadas com "Calibre a saída …" | — |
| Sem Bluetooth | Sem o `NivelBluetooth` | As linhas apagadas com "Meça o nível do Bluetooth." | — |
| Master acima | Alto em uso; master pedido 0,5 dB acima da conta | Recusado, tela e porta; nada enviado | Abaixo: passa |
| Falta ganho | Memória plana com o ganho 0 | O master em 0 e o aviso "falta ganho" | — |
| Ficha mudou | O Xmax da JBL baixa | O master é refeito na hora | — |
| Migração | `setups.json` formato 3 com memórias e playlists | Formato 4, nada some, `nivel` ausente | — |
| Porta lê | `GET /estado` e `/limites` | `nivel` e `master` | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Rendimento/LimiteFisico.cs`: `LimiteSaida`/`LimiteCanal` (T, X, C, `TetoDb`, `TetoCerca`, `Calibrada`), o cálculo por estado (um leitor de `Endereco`) e `ParaDb`. `ModeloExcursao.cs`: `PassaAlta`/`PassaBaixa` (módulo).
- `src/BgsControle.Nucleo/Rendimento/Cerca.cs`: o teto da cerca (`TetoCerca`) e o `NaVolta`. O teto pelo nível entra aqui.
- `src/BgsControle.Nucleo/Rendimento/Calibracao.cs` e `SemCalibracao.cs`: a regra das saídas sem calibração. `Setup.NivelBluetooth` (13b).
- `src/BgsControle.Nucleo/Catalogo/Catalogo.cs`:
  - `BandasHz` (51–52);
  - os códigos 080, 081, 010, 040–043, 090–092 (frequência, ganho, Q), 030–032 (ganho, frequência, Q), 020 (Banda) e 220.
- `src/BgsControle.Nucleo/Setups/Setup.cs:92` (`MemoriaCelular`: `Regulagem`, `IdPreset`) e `Presets/Preset.cs:16` (`Valores` = as 15 bandas).
- `src/BgsControle.Nucleo/Setups/GerenteSetups.cs`:
  - `Formato = 3` (70), `CarregarAsync` (2322–2370; a migração descarta memórias quando o formato difere: mudar para 3→4 sem perda), `DeJson` (2498–2530: `migrando`) e `ParaJson`;
  - `MemoriaEmUso` (384), `IdPresetEmUso` (404) e `PresetEmUso`;
  - aplicar (`AplicarAlvoAsync`) e salvar.
- `src/BgsControle.Nucleo/Presets/GerentePresets.cs`: aplicar o preset (455–541) e `Mudou` (141).
- `src/BgsControle.Nucleo/Escrita/EstadoAparelho.cs:43` (`Mudou(Endereco)`) e `Escritor.cs` (os hooks `Cerca`/`SemCalibracao`; o master à mão entra num hook igual).
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs`: `/estado` (memórias) e `/limites`.
- Telas: o modelo é o `FolhaRiscoViewModel`/`TelaBase`. `CalibracaoViewModel` (13b) é o modelo de VM do Rendimento.
- Testes:
  - a fixture `CasaDoDenis` (calibrada; `NivelBluetooth` a acrescentar −13);
  - `CenarioFilme`;
  - as bancadas de `CercaTestes`, `CalibracaoTestes` e `ControleParametroViewModelTestes`.

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Nucleo/Rendimento/PiorCaso.cs` (novo): a resposta da cadeia por saída (os sinos, os cortes, a rota, os ganhos), o pico, o master do nível e o motivo de cada nível indisponível.
- `src/BgsControle.Nucleo/Rendimento/Niveis.cs` (novo): `NivelRendimento`, as margens, o teto por nível e `GerenteNiveis` (a conta na hora, o master no aplicar e na troca de preset, a recusa do master acima e o nível na cerca).
- `Setup.cs` e `GerenteSetups.cs`: `MemoriaCelular.Nivel` e `Masters`, o formato 4 com a migração sem perda e `DefinirNivelAsync`.
- `Cerca.cs` e `Escritor.cs`: o teto pelo nível e o hook do master.
- `PortaComandos.cs`: `nivel` e `master` no `/estado` e no `/limites`, e a ajuda.
- `src/BgsControle.Telas/NivelViewModel.cs` (novo) e `MauiProgram.cs`.
- Testes:
  - `tests/BgsControle.Nucleo.Testes/PiorCasoTestes.cs` e `NiveisTestes.cs`: uma linha da matriz por teste, com os números da JBL dentro de 0,2 dB;
  - a migração do 3 para o 4 em `SetupsTestes`;
  - `tests/BgsControle.Telas.Testes/NivelViewModelTestes.cs`.

**Acceptance Criteria:**
- Given o Casa da fixture calibrado com o Bluetooth −13 e uma memória plana com o ganho +9 e a JBL em LR24 200 Hz, when o usuário escolhe Baixo, Médio, Alto e Máximo, then o pior caso da JBL fica em 17,9 V −6/−3/−1 dB e em min(22,2; 23) −1 dB, dentro de 0,2 dB, e o master no aparelho é o da conta.
- Given um `setups.json` formato 3 real (o da fixture), when o app abre, then ele vira formato 4 sem perder nenhuma memória, playlist ou botão.
- Given os testes que já existem, when a 13c entra, then todos passam, e o build Android sai sem erro nem aviso.

## Spec Change Log

## Review Triage Log

### 2026-11-01 — Review pass
- verdicts: 44 findings — high 0, medium 8, low 35, false 1, maybe-false 0
- findings:
  - `[low]` `[reject]` Intent: os números dentro de 0,2 dB passam pelo próprio modelo — os valores ancorados à mão (pico −4,0; masters −8,8/−5,8/−3,8) e o master que chega ao simulador são independentes; a tensão real é da 12d.
  - `[low]` `[reject]` Intent: a cadeia vem do espelho, não da memória guardada — o espelho é a combinação que toca; o aplicar e a troca de preset usam a memória e o preset por cima do espelho.
  - `[low]` `[reject]` Intent: o master só é guardado para as combinações que tocaram — a spec decidiu "só registro"; vale a conta na hora.
  - `[false]` `[reject]` Intent: o limitador não refaz a conta — o limitador não entra no pior caso (o master é posto antes dele).
  - `[low]` `[reject]` Intent: só a memória em uso é refeita, não "todas do Setup" — as outras refazem a conta ao serem aplicadas.
  - `[low]` `[reject]` Intent: a folha do Máximo é local, não a folha de risco da porta — mesmo texto e mesmos botões; a folha da porta é para pedidos da porta.
  - `[low]` `[reject]` Intent: o nível só na memória em uso, não no Salvar — a folha do Salvar é da 13g; a memória salva vira a em uso e escolhe o nível ali.
  - `[low]` `[patch]` Intent: a migração é testada com um arquivo escrito pelo `ParaJson` novo — teste (g) com um JSON do formato 3 escrito à mão.
  - `[low]` `[patch]` Verification gap: o master que sobe no recálculo não tem teste — teste (a).
  - `[low]` `[patch]` Verification gap: a ordem do master no lote do preset fica escondida pelo `GerenteNiveis` — teste (b).
  - `[low]` `[patch]` Verification gap: a cerca no Máximo ao aplicar e na volta não tem teste — teste (c).
  - `[low]` `[patch]` Verification gap: `avisoNivel` não é conferido — teste (d).
  - `[low]` `[patch]` Verification gap: a folha não se refaz com mudanças de fora — teste (e).
  - `[low]` `[patch]` Verification gap (outro): sair do Máximo deixa o limitador acima — consertado com a recusa (grupo do limitador).
  - `[low]` `[patch]` Verification gap (outro): o caminho do master guardado sem conta e a exceção no `ConferirMaster` — grupo do nível sem conta.
  - `[medium]` `[patch]` Blind: o app sobe de volta o master que o usuário baixou — o offset do usuário fica até aplicar, trocar o nível ou o preset.
  - `[low]` `[reject]` Blind: um reforço na cadeia sai antes de o master baixar — a cerca do limitador (13a) segura o limite físico nesse instante; só a margem do nível passa por um momento, e o conserto pede pré-compensar cada escrita.
  - `[medium]` `[patch]` Blind: trocar do Máximo para outro nível deixa o limitador acima da cerca — a troca é recusada até baixar o limitador.
  - `[medium]` `[patch]` Blind: com o nível e sem conta, nada protege — só master igual ou abaixo do atual; o aplicar usa o menor.
  - `[low]` `[patch]` Blind: o master que sobe no lote do aplicar não vai por último — vai por último.
  - `[low]` `[patch]` Blind: "Sem nível" deixa os `Masters` velhos — limpa.
  - `[low]` `[patch]` Blind: o `/estado` mostra o master da combinação errada — usa o preset lembrado pela memória.
  - `[low]` `[patch]` Blind: o `/limites` não diz por que o nível está indisponível — `avisoNivel` com o motivo.
  - `[low]` `[patch]` Blind: o piso de −80 dB esconde o teto não atingido — o aviso com a saída.
  - `[low]` `[patch]` Blind: a migração regrava o arquivo sem cópia — `setups.formato3.json` antes de gravar o 4.
  - `[low]` `[reject]` Blind: a folha refaz a conta quatro vezes na tela — ~100 ms por mudança; otimizar não muda o comportamento.
  - `[low]` `[patch]` Blind: "10,0 V" entre 9,95 e 10 — o formato é decidido depois de arredondar.
  - `[low]` `[patch]` Blind: o master muda e a gravação falha — grava antes e escreve depois.
  - `[low]` `[patch]` Blind: faltam testes (o `NaVolta` no Máximo, a ordem, o `avisoNivel`, a marca) — testes (a) a (g).
  - `[medium]` `[patch]` Edge: o master baixado à mão volta a subir — o mesmo do Blind.
  - `[medium]` `[patch]` Edge: uma exceção no `DefinirNivelAsync` derruba o app — try/catch com `MsgFalhou`.
  - `[medium]` `[patch]` Edge: sair do Máximo com o limitador acima — o mesmo do Blind.
  - `[low]` `[patch]` Edge: a memória em uso muda com a folha do Máximo aberta — confere o id e fecha.
  - `[low]` `[patch]` Edge: o master que sobe junto com o ganho que baixa no lote — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o piso de −80 dB — o mesmo do Blind.
  - `[low]` `[patch]` Edge: no Máximo, um alto-falante só com o térmico sai da conta — usa o T.
  - `[medium]` `[patch]` Edge: o nível sem conta deixa passar qualquer master — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o `/estado` com a combinação errada — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o `/limites` sem o motivo — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o 220 não refaz a conta — entra na cadeia.
  - `[low]` `[patch]` Edge: uma gravação de preset ao mesmo tempo sobrescrita pelo instantâneo velho — usa a memória de dentro do `GravarAsync`.
  - `[low]` `[patch]` Edge: o master muda e a gravação falha — o mesmo do Blind.
  - `[low]` `[patch]` Edge: "10,0 V" — o mesmo do Blind.
  - `[medium]` `[patch]` Edge (alegação): baixar à mão não fica — o mesmo do Blind.

## Design Notes

- **Por que o master e não o limitador:** a margem fica no master, e o limitador continua sendo o teto físico (a cerca). Assim a memória pode usar o limitador mais baixo, como compressor (Assumptions da spec do Rendimento).
- **Por que a conta na hora e o master guardado só como registro:** a ficha, a calibração ou o preset podem mudar depois de a memória ser salva. Um master velho passaria do teto.
- **Por que o formato sobe:** a intenção pede o formato novo com migração. A migração de hoje descarta as memórias quando o formato difere, e isso foi feito para o 2→3. O 3→4 é só acrescentar campos e não pode descartar nada.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

## Auto Run Result

Status: done

**O que entrou.** Os níveis Baixo, Médio, Alto e Máximo por memória do celular (spec-rendimento CAP-4):
- **O pior caso** (`Rendimento/PiorCaso.cs`) simula em frequência um tom de 0 dBFS pela cadeia: o Bluetooth medido, o EQ de entrada, as 15 bandas (Q 2,1), a rota (a+b = +6), o ganho, o EQ de saída e os cortes. Dele sai o maior master que deixa cada saída no teto do nível, menos a margem, arredondado a 0,1 e entre −80 e 0, com os avisos "falta ganho" e "o master não desce o bastante".
- **O `GerenteNiveis`** (`Rendimento/Niveis.cs`) refaz a conta na hora (cadeia, 220, ficha, calibração, Bluetooth e preset) e escreve o master com a origem App. Ele respeita o corte que o usuário fez à mão e espera a marca da cópia sair.
- **O master à mão:** acima da conta é recusado, na tela e na porta. Com o nível escolhido e sem conta, o master só desce.
- **O Máximo** pede a folha de calor, e a cerca vai a min(X, C). Sair do Máximo com o limitador acima do teto novo é recusado.
- **A memória** ganha o `nivel` e os `masters` por preset (`setups.json` formato 4). A migração do 3 não perde nada e guarda uma cópia em `setups.formato3.json`.
- **A porta:** `nivel` e `master` no `/estado`; `nivel`, `master` e `avisoNivel` no `/limites`.
- **O `NivelViewModel`:** a folha com as linhas e os volts, o Extra apagado e a confirmação do Máximo presa à memória.

**Arquivos** (em `app-controle/`):
- Núcleo:
  - `Rendimento/PiorCaso.cs` e `Niveis.cs` (novos);
  - `LimiteFisico.cs` e `Cerca.cs` (`semTermico`, o nível);
  - `Setup.cs` e `GerenteSetups.cs` (formato 4, `DefinirNivelAsync`, `Conta`, o corte do usuário, o lote e a cópia da migração);
  - `Escritor.cs` (o hook do master), `GerentePresets.cs` (o master no lote) e `PortaComandos.cs`.
- Telas: `NivelViewModel.cs` (novo) e `RepositorioTextoArquivos.cs`.
- App: `MauiProgram.cs`.
- Testes:
  - `PiorCasoTestes`, `NiveisTestes` e `NivelViewModelTestes` (novos);
  - `PortaTestes`, `SetupsTestes`, `CalibracaoTestes` e `SetupsEquipamentosTestes`;
  - a fixture `CasaDoDenis.SetupCasaComBluetooth`.

**Revisão.** 44 achados (8 medium, 35 low, 1 false).
- **Patches:**
  - o corte do usuário no master;
  - sair do Máximo;
  - o nível sem conta;
  - a exceção no `DefinirNivelAsync`, a ordem gravar→escrever, os `Masters` limpos e a memória lida de dentro da gravação;
  - a ordem do master que sobe;
  - o aviso do −80;
  - o T no Máximo;
  - o 220;
  - os volts;
  - a porta (`/estado` e `avisoNivel`);
  - a cópia da migração;
  - a folha presa à memória;
  - os testes (a) a (g).
- **Nada diferido.**
- **Rejeitados,** com o motivo no Review Triage Log. O principal: o reforço que sai antes de o master baixar, que a cerca do limitador segura no limite físico.

**Revisão de acompanhamento: recomendada.** Foram 4 entradas medium com patch. O risco que ninguém conferiu é a cadeia no aparelho real:
- o Q das 15 bandas;
- a rota a+b;
- a ordem do EQ e do limitador;
- a tensão de verdade contra o VC830L, que é da 12d.

**Verificação:**
- `dotnet test` Núcleo: 1235 de 1235.
- `dotnet test` Telas: 491 de 491.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- A matriz tem pelo menos um teste por linha.

**Decisões provisórias:**
- o nível só se escolhe na memória em uso;
- uma rota desconhecida conta como +6 dB;
- o master do nível também vai para o 080 guardado na memória;
- o X da JBL no modelo dá ~22,4 V em LR24 200 Hz (dentro de 0,2 dB do exemplo).
