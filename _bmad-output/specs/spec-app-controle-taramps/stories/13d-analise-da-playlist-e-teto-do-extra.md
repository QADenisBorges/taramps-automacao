---
title: 'História 13d: análise da playlist e teto do Extra'
type: 'feature'
created: '2026-11-01'
baseline_revision: '223970c9d54dae6c5480224ea023b4a633dff6c8'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-rendimento/limites-e-niveis.md'
  - '{project-root}/_bmad-output/specs/spec-rendimento/telas-rendimento.md'
warnings:
  - oversized
deferred:
  - summary: >-
      Os trechos fortes são escolhidos pelo maior |y| de qualquer saída, não pela menor folga de cada saída (teto − pico).
    evidence: |-
      Segue a spec da 13d; para o teste a seco, o trecho útil é o que chega mais perto do teto de alguma saída. A 13e decide.
    location: >-
      app-controle/src/BgsControle.Nucleo/Rendimento/AnalisePlaylist.cs (MedidorFaixa.Trechos)
    severity: low
  - summary: >-
      A recusa do Otimizar numa memória de outro Setup (MsgOutroSetup) não tem teste.
    evidence: |-
      A fixture precisa de um segundo Setup com memória; o caminho só se alcança pela porta ou telas futuras.
    location: >-
      app-controle/src/BgsControle.Nucleo/Rendimento/AnalisePlaylist.cs (OtimizarAsync)
    severity: low
---

<intent-contract>

## Intent

**Problem:** Os níveis da 13c contam o pior caso de um tom contínuo. Por isso o master fica longe do que a música de verdade pede. O Extra (spec-rendimento CAP-5) precisa dos picos reais da playlist da memória, saída por saída. O app ainda não lê o áudio das faixas.

**Approach:**
- Uma análise no núcleo (`Rendimento/AnalisePlaylist.cs`) passa o PCM de cada faixa, no tempo, pela cadeia da memória e do preset, e acha, por saída:
  - o pico;
  - os 2 trechos fortes;
  - a potência média.
- Dela sai, por preset, o teto do Extra (o master candidato) e "quem está pesando".
- A decodificação fica atrás de `IDecodificadorAudio`: no Android, com `MediaExtractor` + `MediaCodec`; nos testes, faixas sintéticas.
- Uma música nova é analisada em segundo plano e não toca até terminar.

## Boundaries & Constraints

**Always:**
- **Quando:** a análise começa pelo "Otimizar configuração na playlist" da memória (`Otimizar`). A memória fica `Otimizada` (campo opcional `otimizada` na memória do formato 4, sem subir o formato).
  - Numa memória otimizada, toda faixa nova é analisada sozinha, uma por vez e em segundo plano.
  - Ela também é refeita quando muda:
    - a regulagem guardada da memória;
    - as bandas guardadas do preset;
    - a ficha, a calibração ou o `NivelBluetooth` do Setup.
- **Precisa de:** Setup em uso, todas as saídas calibradas e o `NivelBluetooth` medido, as mesmas condições dos níveis da 13c. Senão, o motivo da 13c.
- **A cadeia no tempo**, em `double` e na taxa da faixa, sobre as amostras em ±1,0 (0 dBFS):
  - o nível do Bluetooth;
  - o EQ de entrada, como sino RBJ ("peaking", com o ganho, a frequência e o Q);
  - as 15 bandas do preset, como sino RBJ com Q 2,1, a mesma suposição da 13c;
  - a rota por canal: a = L, b = R, a+b = L+R;
  - o ganho;
  - o EQ de saída, como sino RBJ;
  - os cortes: BT de ordem n por biquads Butterworth, e LR de ordem n como dois BT de ordem n/2 em cascata, com o primeiro estágio de primeira ordem quando a ordem é ímpar;
  - sem o master.
  
  A cadeia é a memória guardada (`Regulagem`) com o preset (`Valores`). As operações são lineares, então a ordem não muda o pico [ASSUMPTION: o processador não tem compressão antes do limitador].
- **Por faixa e por saída:**
  - o pico de amostra, em dB na escala do limitador (0 dB = 2,0 V);
  - a média RMS de toda a faixa, em dB, que é a base do térmico da 13f;
  - os 2 trechos de 10 s que não se sobrepõem e têm os maiores picos de qualquer saída (início e fim em segundos), para o teste a seco da 13e.
- **O teto do Extra, por preset:** master = min sobre faixas e saídas de (teto − 1 − pico), com teto = min(X, C) da saída e o corte da memória. É arredondado para baixo a 0,1, dentro de −80..0, com o aviso "falta ganho" acima de 0. Faixas indisponíveis ou que falharam ficam de fora, com o motivo.
- **Quem está pesando:** peso(faixa) = master sem ela − master com todas (≥ 0, em dB, a 0,1). A faixa que manda tem o maior peso; as outras dão 0.
  - O texto é "−{peso} dB no teto" quando > 0.
  - Uma faixa "em análise" mostra "Em análise".
  - Uma faixa que falhou mostra "Não deu para analisar: {motivo}".
- **Música nova:**
  - Adicionar uma faixa a uma memória otimizada a põe "em análise". O Player não toca uma faixa em análise: pula na sequência, e tocá-la direto dá "Esta faixa ainda está em análise.".
  - Dá para adicionar outras enquanto isso; a fila é uma por vez.
  - Quando a análise termina, o teto é refeito. Se a faixa pesa mais, o teto candidato desce.
- **Cache:** os resultados ficam em `analises.json` (`IArquivosApp`), por faixa (uri + duração) e por impressão da cadeia (memória, preset, fichas, ganhos e Bluetooth). Uma impressão diferente invalida.
- **A análise não muda o som.** O teto do Extra é só calculado e mostrado. Aplicá-lo exige o teste a seco (13e) e o térmico (13f).
  - Na `NivelViewModel`, a linha do Extra mostra "Analisando a playlist." durante a análise; "Falta o teste a seco." com a análise pronta; e "Falta analisar a playlist." sem ela.
- **`IDecodificadorAudio`:** `LerAsync(uri, Action<float[] blocoIntercalado, int canais, int taxa>, CancellationToken)`.
  - No Android: `MediaExtractor` + `MediaCodec` para PCM; mono vira L = R.
  - Uma falha (arquivo sumido, formato) vira o motivo da faixa.
  - Nos testes, o dublê gera senos e rajadas com picos conhecidos.
- **Cancelar:** apagar a faixa, desotimizar a memória ou fechar o app cancela a análise em curso. Uma análise pela metade não grava.
- **A porta:** o `GET /limites` traz `extra`:
  - `estado`: `falta`, `analisando`, `pronta` ou `indisponivel`;
  - `master` (o candidato do preset em uso);
  - `pesando` [{`faixa`, `db`}];
  - `trechos` por faixa.
- **ViewModels:**
  - `OtimizarViewModel`: o comando "Otimizar configuração na playlist", o progresso sem números (o anel), o resultado ("Teto do Extra: {master} dB. Falta o teste a seco.") e o cancelar.
  - O `PlayerViewModel` ganha, em cada faixa, o texto do peso, "Em análise" ou o motivo da falha.
  - Nenhum texto contém "IA".

**Never:**
- Aplicar o master do Extra.
- O teste a seco, o fiscal do clip e "revalidar" (13e).
- O térmico (13f).
- XAML (13g).
- Bloquear a tela ou o player durante a análise.
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Três faixas sintéticas | Senos de 1 kHz com picos −3, −6 e −10 dBFS; Casa calibrado; memória plana com o ganho +15 | Teto do Extra = master da faixa de −3 (teto − 1 − pico ≈ −4,9 dB), ±0,2 dB; pesando: a de −3 com 3,0 dB (a diferença para a de −6), as outras 0 | — |
| Trechos fortes | Uma faixa de 60 s com rajadas em 12 s e 40 s | Trechos que contêm 12 s e 40 s, de 10 s, sem se sobrepor | Faixa < 10 s: um trecho com a faixa toda |
| Música nova | Memória otimizada; acrescentar uma faixa de −1 dBFS | "Em análise"; o Player pula ela; ao fim, o teto desce e o peso aponta ela | — |
| Tocar em análise | `TocarFaixaAsync` na faixa em análise | "Esta faixa ainda está em análise." | — |
| Arquivo sumido | O decodificador falha | A faixa fica de fora, com o motivo; o teto vem das outras | — |
| Preset diferente | Outro preset com +6 dB em 1 kHz | O teto daquele preset 6 dB abaixo, ±0,2 | — |
| Sem calibração | Uma saída sem ganho medido | "Calibre a saída …"; nada analisado | — |
| Cache | Reabrir o app sem mudança | Nenhuma decodificação nova | Ficha mudada: analisa de novo |
| Rota a+b | Canal com a+b; L e R iguais a −6 dBFS | Pico da saída = −6 + 6 dB (antes do resto) | — |
| Porta | `GET /limites` | `extra` com estado, master, pesando e trechos | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Rendimento/PiorCaso.cs` e `Niveis.cs` (13c):
  - a cadeia em frequência (os sinos, a rota, os cortes) e `Conta`;
  - as condições (`NivelIndisponivel`), as margens e o teto do Máximo/Extra = min(X, C).
  
  A análise usa a mesma leitura dos valores e os mesmos limites.
- `src/BgsControle.Nucleo/Rendimento/LimiteFisico.cs` (`semTermico`) e `ModeloExcursao.cs` (os filtros em módulo, para conferir os biquads nos testes).
- `src/BgsControle.Nucleo/Setups/Setup.cs` (`MemoriaCelular`: `Regulagem`, `IdPreset`, `Nivel`, `Masters`) e `GerenteSetups.cs` (o formato 4, `ParaJson`/`DeJson`, `MemoriaEmUso` e `IdPresetEmUso`). `Presets/Preset.cs` (`Valores`).
- `src/BgsControle.Nucleo/Player/Faixa.cs` (`Faixa(Id, Uri, Nome, Pasta, Duracao)`), `GerentePlaylists.cs` (`Playlist(idMemoria)` 97, `AdicionarAsync` 111, `RemoverAsync` 126) e `Player.cs` (`Indisponivel` 143, `TocarFaixaAsync` 214, `ProximaAsync` 205, a sequência).
- `src/BgsControle.Telas/Abstracoes.cs:34` (`IArquivosApp`) e `RepositorioTextoArquivos.cs` (os nomes dos arquivos).
- `src/BgsControle.Telas/NivelViewModel.cs` (a linha do Extra) e `PlayerViewModel.cs`.
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs`: `/limites` (o `avisoNivel` da 13c) e a ajuda.
- `src/BgsControle.App/Platforms/Android/` (o modelo é o `ReprodutorAndroid.cs`) e `MauiProgram.cs`.
- Consulta: `_bmad-output/regulagem/playlist/analisar.py` (o pico e as janelas).

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Nucleo/Rendimento/Biquad.cs` (novo): o sino RBJ e os passa-alta e passa-baixa BT/LR em cascata, por amostra.
- `src/BgsControle.Nucleo/Rendimento/AnalisePlaylist.cs` (novo):
  - a cadeia no tempo, por saída;
  - o pico, a RMS e os trechos;
  - o teto do Extra por preset e o peso;
  - a fila em segundo plano, o cancelar e o cache (`analises.json`);
  - a impressão da cadeia e o `Otimizar`.
- `src/BgsControle.Nucleo/Player/IDecodificadorAudio.cs` (novo) e `Player.cs`: a faixa em análise não toca.
- `Setup.cs`/`GerenteSetups.cs`: a `MemoriaCelular.Otimizada` (opcional no formato 4).
- `PortaComandos.cs`: o `extra` do `/limites` e a ajuda.
- `src/BgsControle.Telas/OtimizarViewModel.cs` (novo), `PlayerViewModel.cs` (o peso e o estado por faixa) e `NivelViewModel.cs` (os textos do Extra).
- `src/BgsControle.App/Platforms/Android/DecodificadorAndroid.cs` (novo) e `MauiProgram.cs`.
- Testes:
  - `tests/BgsControle.Nucleo.Testes/BiquadTestes.cs`: o módulo dos biquads confere com `ModeloExcursao`/`PiorCaso` dentro de 0,1 dB;
  - `AnalisePlaylistTestes.cs`: uma linha da matriz por teste, com o dublê do decodificador;
  - `tests/BgsControle.Telas.Testes/OtimizarViewModelTestes.cs`.

**Acceptance Criteria:**
- Given o Casa calibrado e uma memória com três faixas sintéticas de picos conhecidos, when o usuário toca "Otimizar", then o teto do Extra fica na faixa mais pesada menos 1 dB (teto − 1 − pico, ±0,2 dB), e o "quem está pesando" aponta essa faixa com o valor certo.
- Given uma faixa em análise, when o Player chega a ela, then ela não toca.
- Given os testes que já existem, when a 13d entra, then todos passam, e o build Android sai sem erro nem aviso.

## Spec Change Log

- **01/11, depois da revisão:**
  - **Matriz "Três faixas sintéticas":** a fixture dá o teto do Extra ≈ −4,7 dB (o Re medido de 3,6 leva o X da JBL a 22,9 V), e não ≈ −4,9. A conta "teto − 1 − pico" confere dentro de 0,2 dB, que é o que a linha pede.
  - **Esclarecimentos da revisão** (fora do intent-contract):
    - só a faixa nova fica "em análise", e a reanálise deixa o player tocar;
    - no empate, o grupo empatado divide o peso;
    - a média é o RMS de verdade;
    - a memória sem preset usa as bandas planas.
  - **KEEP:** a cadeia no tempo (os biquads e a ordem linear), os trechos de 10 s e o cache por impressão.

## Review Triage Log

### 2026-11-01 — Review pass
- verdicts: 43 findings — high 0, medium 10, low 32, false 1, maybe-false 0
- findings:
  - `[low]` `[reject]` Intent: a análise não tem tela nem o botão "Otimizar" na tela — a tela é da 13g; o `OtimizarViewModel` está pronto.
  - `[low]` `[reject]` Intent: o decodificador do Android não tem teste — a decodificação real é da 12d (Verification).
  - `[low]` `[patch]` Intent: os testes só usam senos, então não provam o pico no tempo — teste (f) com dois tons somados.
  - `[low]` `[reject]` Intent: a matriz diz ≈ −4,9 dB e a fixture dá −4,7 (o Re medido de 3,6 dá X 22,9 V) — a conta (teto − 1 − pico) confere dentro de 0,2 dB; registrado no Spec Change Log.
  - `[medium]` `[patch]` Intent: a reanálise bloqueia o player — só a faixa nova fica em análise.
  - `[low]` `[reject]` Intent: o Cancelar desotimiza a memória — a spec põe desotimizar entre os cancelamentos; o resultado guardado fica no cache.
  - `[low]` `[reject]` Intent: a faixa indisponível do Player só sai da conta quando a decodificação falha — é o mesmo efeito (arquivo sumido falha na leitura).
  - `[medium]` `[patch]` Intent: o fechamento só cancela num dos caminhos — o mesmo grupo do descarte da janela.
  - `[medium]` `[patch]` Blind: mudar a cadeia trava a playlist inteira — o mesmo do Intent.
  - `[medium]` `[patch]` Blind: o `Destroying` da janela descarta o serviço para sempre — só cancela o trabalho em curso.
  - `[low]` `[patch]` Blind: um valor da cadeia que falta vira falha de cada faixa — vai para o motivo da memória ou do preset.
  - `[medium]` `[patch]` Blind: no empate, ninguém pesa — o grupo empatado leva o peso de tirar o grupo.
  - `[low]` `[reject]` Blind: só o pico de amostra (sem true peak) — é o que a spec define; a margem de 1 dB e o teste a seco (13e) cobrem o resto. O PCM em float, que evita o corte em 0 dBFS, entrou no grupo do decodificador.
  - `[medium]` `[patch]` Blind: o "rms" é 3 dB acima do RMS — grava o RMS de verdade.
  - `[low]` `[defer]` Blind: os trechos fortes usam o |y| bruto, não a folga de cada saída — segue a spec ("maiores picos de qualquer saída"); a 13e (teste a seco) decide se escolhe pela folga.
  - `[low]` `[patch]` Blind: o `OtimizarViewModel` não mostra por que não há resultado — expõe o motivo.
  - `[low]` `[patch]` Blind: a porta identifica a faixa só pelo nome — acrescenta o `id`.
  - `[low]` `[patch]` Blind: a impressão usa o Setup inteiro, e o cache cresce — só os valores da conta, e poda.
  - `[low]` `[reject]` Blind: o custo cresce com os presets e há laços quadráticos — o desempenho no aparelho é conferido na 12d; não muda o resultado.
  - `[medium]` `[patch]` Blind: o decodificador vaza o codec, pode travar a fila e repete sem fim num cancelamento de fora — libera, limita a espera e confere o token.
  - `[low]` `[patch]` Blind: faltam testes (falta ganho, mono, cache, reuso, player na reanálise, a falha na linha) — testes (a) a (e).
  - `[low]` `[patch]` Blind: código morto e duplicado; a rota desconhecida vira a+b; exceções engolidas sem log — limpos.
  - `[medium]` `[patch]` Edge: a reanálise bloqueia o player — o mesmo do Intent.
  - `[low]` `[patch]` Edge: um preset com a cadeia falha derruba a faixa em todos — pula só o preset.
  - `[low]` `[patch]` Edge: NaN ou infinito no PCM — recusa com `MsgFormato`.
  - `[low]` `[patch]` Edge: codificação de 24 ou 32 bits lida como 16 — recusa.
  - `[medium]` `[patch]` Edge: o codec que nunca dá fim de fluxo trava a fila — espera limitada.
  - `[low]` `[patch]` Edge: o codec vaza se Configure ou Start lança — libera.
  - `[low]` `[patch]` Edge: o `Carregada` nulo numa corrida do construtor — atribuído antes de assinar.
  - `[medium]` `[patch]` Edge: a memória sem preset fica indisponível para sempre — bandas planas com a chave "".
  - `[medium]` `[patch]` Edge: no empate, peso 0 — o mesmo do Blind.
  - `[low]` `[patch]` Edge: uma análise guardada sem algum canal — descartada.
  - `[low]` `[patch]` Edge: a rota desconhecida — o mesmo do Blind.
  - `[low]` `[patch]` Edge: uma faixa muda dá um falso "falta ganho" — fica fora da conta.
  - `[low]` `[patch]` Edge: o `/limites` inteiro dá 500 se o teto falha — o `extra` fica indisponível.
  - `[low]` `[patch]` Edge: um erro de ponto flutuante no `Encaixar` — arredonda e limita.
  - `[false]` `[reject]` Edge (alegação): "bloquear o player" — duplicado do primeiro achado do Edge, já com patch; esta linha é a mesma alegação.
  - `[low]` `[patch]` Edge (alegação): a faixa que manda com peso 0 no empate — o mesmo grupo.
  - `[low]` `[patch]` Verification gap: o teto acima de 0 e o piso de −80 sem teste — teste (a).
  - `[low]` `[patch]` Verification gap: a regulagem guardada mudada não refaz a análise em teste — teste (b).
  - `[low]` `[patch]` Verification gap: os estados `analisando` e `indisponivel` do `/limites` — teste (c).
  - `[low]` `[patch]` Verification gap: "Nenhuma faixa disponível" com faixas em análise — teste (d).
  - `[low]` `[patch]` Verification gap: o trecho à esquerda — teste (e).
  - `[low]` `[defer]` Verification gap: a recusa da memória de outro Setup sem teste — a fixture precisa de um segundo Setup com memória.

## Design Notes

- **Por que o pico no tempo e não em frequência:** a música não é um tom. O pico real numa saída depende de como as frequências da faixa se somam depois dos cortes e dos EQ. A 13c fica com o pior caso de um tom, e o Extra usa o que a playlist faz de verdade.
- **Por que o peso como "master sem ela − master com todas":** é o que a faixa custa ao teto. Só a faixa que manda tem custo, e o número diz quanto o Extra subiria sem ela.
- **Por que a análise não aplica nada:** o teto do Extra só sobe depois do teste a seco (13e), e o Extra só aparece com o modelo térmico (13f).

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- A decodificação real de um MP3/AAC no A52 é conferida na 12d.

## Auto Run Result

Status: done

**O que entrou.** A análise da playlist e o teto do Extra (spec-rendimento CAP-5), sem aplicar nada:
- **A análise no tempo** (`Rendimento/AnalisePlaylist.cs` e `Biquad.cs`) passa o PCM de cada faixa pela cadeia da memória e de cada preset: o Bluetooth, o EQ de entrada, as 15 bandas, a rota, o ganho, o EQ de saída e os cortes BT/LR em biquads.
- **Por saída**, ela mede o pico de amostra, o RMS de verdade e os 2 trechos fortes de 10 s.
- **O teto do Extra por preset** é min(X, C) − 1 − pico, com "quem está pesando" (o empate divide o peso).
- **A fila:** uma por vez, em segundo plano, com o cache `analises.json` por impressão numérica da cadeia, o cancelar e o `Otimizar` (memória `otimizada`).
- **A faixa nova** fica "em análise" e o Player não a toca. A reanálise deixa a playlist tocar.
- **`IDecodificadorAudio`:** no Android, `MediaExtractor` + `MediaCodec` em float, com espera limitada e liberação do codec.
- **A porta:** `extra` no `/limites`, com o estado, o master, `pesando` e `trechos` (com o `id`).
- **ViewModels:** o `OtimizarViewModel` com o motivo, o peso por faixa no `PlayerViewModel` e os textos do Extra no `NivelViewModel`.

**Arquivos** (em `app-controle/`):
- Núcleo:
  - `Rendimento/AnalisePlaylist.cs` e `Biquad.cs` (novos);
  - `Player/IDecodificadorAudio.cs` (novo) e `Player.cs`;
  - `Setup.cs` e `GerenteSetups.cs` (`Otimizada`);
  - `PiorCaso.cs` (`Sinos` interno) e `PortaComandos.cs`.
- Telas: `OtimizarViewModel.cs` (novo), `PlayerViewModel.cs`, `NivelViewModel.cs` e `RepositorioTextoArquivos.cs`.
- App: `Platforms/Android/DecodificadorAndroid.cs` (novo), `MauiProgram.cs` e `App.xaml.cs` (`CancelarEmCurso`).
- Testes:
  - `BiquadTestes`, `AnalisePlaylistTestes` e `OtimizarViewModelTestes` (novos);
  - o dublê `DecodificadorSintetico`.

**Revisão.** 43 achados (10 medium, 32 low, 1 false).
- **Patches:**
  - o player livre na reanálise;
  - a janela que fecha só cancela, sem descartar;
  - o empate;
  - o RMS;
  - a falta da cadeia por preset;
  - a rota desconhecida;
  - a memória sem preset;
  - NaN e infinito;
  - a faixa muda;
  - o cache estragado;
  - o `Carregada`;
  - o cancelamento de fora;
  - a impressão numérica com poda;
  - o `Encaixar`;
  - o decodificador (float, codificações, codec e espera);
  - a porta (`id` e o 500);
  - o motivo no ViewModel;
  - o código morto e o log;
  - os testes (a) a (f).
- **Diferidos (2):** os trechos pela folga, que a 13e decide, e o teste do outro Setup.
- **Rejeitados,** com o motivo no Review Triage Log.

**Revisão de acompanhamento: recomendada.** Foram 6 entradas medium com patch. O risco que ninguém conferiu é a decodificação real do MP3/AAC no A52 e o tempo de análise com vários presets. Só a 12d mostra isso.

**Verificação:**
- `dotnet test` Núcleo: 1273 de 1273.
- `dotnet test` Telas: 494 de 494.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- A matriz tem pelo menos um teste por linha.

**Decisões provisórias:**
- todo preset do celular é analisado;
- com uma faixa só, o peso conta contra 0 dB;
- as falhas de decodificação não são gravadas e são tentadas de novo no próximo Otimizar;
- o pico é o de amostra (o true peak fica para a 13e e a 12d).
