---
title: 'História 13f: Extra térmico'
type: 'feature'
created: '2026-11-01'
baseline_revision: 'c29a24f6c4bcd27b450a12f75f4bde7b6800a5b5'
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
      Depois da queda da conexão, a troca de faixa na memória retida não tem teste de que a potência acompanha a faixa nova.
    evidence: |-
      Não verificado: precisa de uma segunda faixa tocando depois do Derrubar(); a potência conservadora (sem análise → pior caso) reduz o risco.
    location: >-
      app-controle/src/BgsControle.Nucleo/Rendimento/TermicoExtra.cs (Analises / PotenciaAgora)
    severity: medium (unverified)
---

<intent-contract>

## Intent

**Problem:** O Extra (13d e 13e) passa do térmico da ficha, e nada mede quanto tempo a bobina aguenta. Sem um modelo de calor, o Extra não pode aparecer (spec-rendimento CAP-7: "o app nunca inventa um tempo seguro").

**Approach:**
- A ficha do alto-falante ganha cinco dados térmicos, cada um com origem, e o Setup ganha a temperatura ambiente.
- Um modelo de duas constantes (`Rendimento/ModeloTermico.cs`), alimentado pela potência média da análise no master em uso, estima a bobina de cada saída.
- No Extra, a bobina que chega à temperatura máxima corta. Vem então o descanso, com o Bluetooth travado (`251` desligado e mudo `100`), até a bobina voltar à temperatura ambiente.
- O Extra passa a aparecer na folha do nível.

## Boundaries & Constraints

**Always:**
- **Dados térmicos da ficha do alto-falante** (`DadoTecnico`, com origem, e valor > 0):
  - `TemperaturaMaximaBobina` (°C);
  - `RthBobinaIma` (K/W);
  - `RthImaAr` (K/W);
  - `TauBobina` (s);
  - `TauIma` (s).
  
  Eles entram em `DadosTecnicos.Campos` (JSON: `tMaxBobina`, `rthBobinaIma`, `rthImaAr`, `tauBobina`, `tauIma`), na ficha do grupo do alto-falante e na base de modelos (só o oficial e outra fonte, como hoje).
- **Temperatura ambiente do Setup:** `TemperaturaAmbiente` (°C, `DadoTecnico`, opcional no formato 4).
  - Sem ela, vale 25 °C, mostrado como "25 °C (padrão)".
  - Ela sai no `/estado`.
- **Modelo** (por alto-falante passivo de cada saída):
  - **Potência na bobina:** P = V²/Z, com V = 2,0 × 10^((rms + master)/20) × ganho do módulo. O rms é o RMS da faixa naquela saída (13d), e Z é a `Impedancia` da ficha.
  - **ΔT da bobina** = ΔTv + ΔTm, em duas partes desacopladas. Cada parte responde à potência com 1ª ordem: ΔTv → P·RthBobinaIma com τ = TauBobina, e ΔTm → P·RthImaAr com τ = TauIma.
  - **O passo é exato** para a potência constante no intervalo: x ← x·e^(−dt/τ) + P·R·(1 − e^(−dt/τ)), com dt de 1 s.
  - **Temperatura da bobina** = ambiente + ΔTv + ΔTm.
- **Quando o modelo roda:** com a memória em uso otimizada e a análise pronta.
  - Com o player do app tocando, cada segundo usa o RMS da faixa que toca no master atual. Uma faixa sem análise usa o maior RMS da playlist.
  - Parado ou pausado, P = 0 (esfria).
  - O estado de cada alto-falante (ΔTv, ΔTm e a hora) é gravado em `termico.json` a cada 10 s e ao fechar. Ao abrir, o tempo fora entra como esfriamento.
- **O Extra aparece** só se todo alto-falante passivo das saídas do Setup tem os cinco dados térmicos. Sem eles, a linha fica apagada com "Falta o modelo térmico do {alto-falante}.".
  - Com algum dado térmico só digitado (nem oficial nem medido), a linha leva o selo "estimado".
  - Com o teste a seco validado (13e) e o térmico, a linha se acende e escolher o Extra passa a valer na tela.
- **Tempo de Extra** (no Extra em uso, tocando): para cada alto-falante, o tempo até a bobina chegar à temperatura máxima com a potência da faixa atual (a solução fechada da soma de exponenciais, resolvida numericamente). O menor de todos é mostrado como "Extra: {mm:ss}".
- **O corte:** quando a bobina estimada de algum alto-falante chega à temperatura máxima, sem folga:
  - o app pausa o player;
  - muta os 4 canais (`100`);
  - desliga o Bluetooth de áudio (`251` = 0, `ComandosAparelho.DefinirBluetoothAudioAsync(false)`);
  - grava o descanso (`descanso` em `termico.json`, com o alto-falante e a hora).
- **O descanso:**
  - dura até toda bobina estimada voltar a ambiente + 1 °C [ASSUMPTION: "voltar à temperatura ambiente" com 1 °C de tolerância, porque a exponencial não zera];
  - enquanto dura, o player não toca ("Descansando: {mm:ss}");
  - no fim, o app religa o `251` e tira o mudo que ele pôs;
  - se o app fechar no descanso, o mudo e o `251` desligado ficam no aparelho, e ao abrir o app retoma o descanso pelo relógio;
  - se o descanso já acabou, ao conectar o app religa o `251` e desmuta.
- **Falhas:**
  - se o mudo ou o `251` não confirmam no corte, o app tenta de novo e mostra "Não consegui travar o Bluetooth. Desligue o módulo." (o estado seguro é o módulo desligado);
  - sem conexão no corte, o descanso começa do mesmo jeito, e o travamento é escrito ao reconectar.
- **A porta:** o `extra` do `/limites` ganha:
  - `termico`: por alto-falante, a temperatura estimada, a máxima e o selo;
  - `tempoExtra` (s);
  - `descanso` (s que faltam, ou null).
- **ViewModels:**
  - `ExtraAoVivoViewModel`: o tempo de Extra, "Descansando: {mm:ss}" e o Bluetooth travado.
  - A linha do Extra no `NivelViewModel`: aparece, com o teto ("Extra · até {V} nas {saídas} · só a playlist"), o selo "estimado", o motivo quando apagada e o escolher.
  - Os campos térmicos na ficha (`EquipamentosViewModel`/`DadoViewModel`) e a temperatura ambiente na tela Setup.
  
  Nenhum texto contém "IA".

**Never:**
- Inventar um tempo seguro sem os cinco dados.
- Vigiar a média de outras fontes: o modelo usa só o player do app.
- XAML (13g).
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Tempo de Extra | Rv 2 K/W, Rm 1,5 K/W, τv 15 s, τm 600 s, Tmax 150 °C, ambiente 25 °C, P 40 W constante | Corte em 832 s (±5 %) | — |
| Descanso | O mesmo, depois do corte | Descanso de 2284 s até ambiente + 1 °C (±5 %) | — |
| Corte | O modelo chega a 150 °C no Extra | Player pausado; `100` nos 4 canais; `251` = 0 no simulador | Mudo não confirma: repete e avisa |
| Fim do descanso | O descanso acaba | `251` = 1; mudo tirado; o player toca de novo | — |
| Fechar no descanso | O app fecha e reabre 10 min depois | O descanso continua pelo relógio; o mudo segue no aparelho | Já acabou: religa e desmuta |
| Sem dados | Falta o `TauIma` do Sub | O Extra apagado: "Falta o modelo térmico do Sub." | — |
| Digitado | Um dado térmico só digitado | O Extra com o selo "estimado" | — |
| Escolher o Extra | Validado (13e) e os dados térmicos | O Extra escolhido na tela | — |
| Esfriar parado | Parar no meio, 5 min | ΔT cai pela solução exata | — |
| Ambiente | Setup com 35 °C | O corte vem mais cedo (Tmax − 35) | Sem dado: 25 °C (padrão) |
| Porta | `GET /limites` | `extra.termico`, `tempoExtra` e `descanso` | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Setups/Equipamentos.cs`:
  - `DadosTecnicos` (os campos e `Campos`, a lista nome→leitor/escritor), `DadoTecnico`/`Verificado` e `OrigemDado`;
  - os grupos da ficha (o alto-falante passivo e a caixa) e a validação (`DadosValidos`, valor > 0).
  
  `BaseModelos.cs` (`DoModelo`: só o oficial e outra fonte).
- `src/BgsControle.Nucleo/Setups/Setup.cs`/`GerenteSetups.cs`: os campos opcionais do Setup no formato 4 (o modelo é `NivelBluetooth`, `DefinirNivelBluetoothAsync`), `DefinirNivelAsync` (o Extra), `ContaEmUso` e o master do Extra (13e).
- `src/BgsControle.Nucleo/Rendimento/AnalisePlaylist.cs`: `MedidaCanal(Canal, Pico, Rms)` (16–17), `AnaliseFaixa` e `TetoEmUso`. O RMS por faixa e por saída é a entrada do modelo.
- `src/BgsControle.Nucleo/Rendimento/LimiteFisico.cs`: `Ligacoes.Caminhos` (o alto-falante, o módulo, o canal ou a ponte, e o ganho medido).
- `src/BgsControle.Nucleo/Rendimento/FiscalExtra.cs` (13e): o mudo e o retorno, o `fiscal-extra.json`, o Extra em uso, o player tocando e `AntesDeTocar`. `Niveis.cs`: `MsgFaltaTermico` (58).
- `src/BgsControle.Nucleo/Aparelho/ComandosAparelho.cs:141` (`DefinirBluetoothAudioAsync`).
- `src/BgsControle.Nucleo/Player/Player.cs` (`Bloqueio`, a faixa atual, o estado).
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs`: o `extra` do `/limites` e o `/estado` (o Setup).
- Telas: `NivelViewModel.cs` (a linha do Extra, 282), `EquipamentosViewModel.cs`/`DadoViewModel.cs` (os campos da ficha) e `PlayerViewModel.cs`.
- Testes: `BancadaExtra` (13e), `FiscalExtraTestes` e `EquipamentosViewModelTestes`.

## Tasks & Acceptance

**Execution:**
- `Equipamentos.cs`/`BaseModelos.cs`: os cinco dados térmicos. `Setup.cs`/`GerenteSetups.cs`: `TemperaturaAmbiente` e `DefinirTemperaturaAmbienteAsync`.
- `src/BgsControle.Nucleo/Rendimento/ModeloTermico.cs` (novo): o passo exato, o tempo até Tmax, o tempo de descanso e o "pode o Extra" com o motivo e o selo.
- `src/BgsControle.Nucleo/Rendimento/TermicoExtra.cs` (novo):
  - o modelo ao vivo por alto-falante;
  - o corte, o descanso e o religar;
  - `termico.json`;
  - a retomada pelo relógio e ao conectar;
  - o bloqueio do player no descanso.
- `Niveis.cs`/`NivelViewModel.cs`: o Extra aparece e se escolhe.
- `PortaComandos.cs`: `termico`, `tempoExtra` e `descanso`.
- `src/BgsControle.Telas/ExtraAoVivoViewModel.cs` (novo). Os campos térmicos e a temperatura ambiente nos ViewModels da ficha e do Setup. `MauiProgram.cs`.
- Testes:
  - `tests/BgsControle.Nucleo.Testes/ModeloTermicoTestes.cs`: os tempos contra a solução analítica, dentro de 5 %;
  - `TermicoExtraTestes.cs`: uma linha da matriz por teste, com o relógio falso e o simulador;
  - os ViewModels.

**Acceptance Criteria:**
- Given os dados térmicos de teste (Rv 2, Rm 1,5, τv 15 s, τm 600 s, Tmax 150 °C, ambiente 25 °C, P 40 W), when o modelo roda, then o corte vem em 832 s e o descanso dura 2284 s, os dois dentro de 5 % da solução analítica.
- Given o Extra em uso no simulador, when a bobina chega à máxima, then o simulador recebe o `251` desligado e o mudo `100`, e no fim do descanso os dois voltam.
- Given os testes que já existem, when a 13f entra, then todos passam, e o build Android sai sem erro nem aviso.

## Spec Change Log

- **01/11, implementação e revisão** (fora do intent-contract). Suposições e decisões para o Denis conferir:
  - a temperatura ambiente aceita de −40 a 60 °C;
  - a trava falhando é tentada de novo a cada 5 s;
  - sem análise da cadeia atual, a potência é a de um tom de 0 dBFS pelo pior caso no master em uso (o lado seguro);
  - um `termico.json` ilegível põe toda bobina na Tmax e começa um descanso de precaução;
  - a Tmax precisa passar de ambiente + 1 °C;
  - no descanso, a tela e a porta não religam o `251` nem desmutam;
  - o Extra sem o modelo completo usa min(validado, o pior caso do Máximo);
  - o descanso vale só para o Setup do corte;
  - o player volta a tocar no fim, se tocava no corte.
  
  **KEEP:** o passo exato por segundo e os tempos do exemplo (832 s e 2284 s), pelo caminho ao vivo.

## Review Triage Log

### 2026-11-01 — Review pass
- verdicts: 38 findings — high 1, medium 10, low 26, false 0, maybe-false 1
- findings:
  - `[low]` `[reject]` Intent: a potência é a da faixa que toca, não a média da playlist — a spec decidiu (Design Notes): a média da playlist subestimaria uma faixa pesada.
  - `[low]` `[reject]` Intent: o tempo de Extra é a contagem ao vivo, não um orçamento antes de escolher — a tela "Extra ao vivo" de `telas-rendimento.md` é a contagem.
  - `[low]` `[reject]` Intent: a linha apagada, não escondida — é o que a spec do Rendimento pede ("fica apagado, com o motivo").
  - `[low]` `[patch]` Intent: o "estimado" vale para outra fonte e estimado, mas só o digitado tem teste — teste (f).
  - `[low]` `[reject]` Intent: o descanso até ambiente + 1 °C — suposição da spec (a exponencial não zera).
  - `[low]` `[patch]` Intent: os 5 % só nas funções puras — teste (a) pelo caminho ao vivo.
  - `[low]` `[reject]` Intent: o impedimento do teste a seco no descanso e o modelo em qualquer nível — acréscimos que protegem; o modelo em qualquer nível guarda o calor antes do Extra.
  - `[low]` `[patch]` Verification gap: a faixa sem análise com o maior RMS sem teste — teste (b).
  - `[low]` `[patch]` Verification gap: a gravação a cada 10 s sem teste — teste (c).
  - `[low]` `[patch]` Verification gap: o corte só no Extra sem teste — teste (d).
  - `[maybe-false]` `[defer]` Verification gap: depois da queda, a troca de faixa não muda a potência em teste — precisa de uma segunda faixa depois da queda; o conserto da potência conservadora reduz o risco.
  - `[medium]` `[patch]` Verification gap (outro): a memória que já está no Extra sem o modelo — o master do Máximo e o motivo.
  - `[medium]` `[patch]` Verification gap (outro): P = 0 sem análise — a potência conservadora do pior caso.
  - `[low]` `[patch]` Verification gap (outro): a gravação do fechar sem espera — esperada.
  - `[medium]` `[patch]` Edge: a Tmax até 1 °C acima do ambiente faz o corte oscilar — recusado com o motivo.
  - `[high]` `[patch]` Edge: o descanso acaba na hora sem os estados — o estado fica durante o descanso, e o fim nunca vale no vazio.
  - `[medium]` `[patch]` Edge: o Extra sem o modelo depois de tirar um dado — o mesmo da linha do Verification gap.
  - `[medium]` `[patch]` Edge: P = 0 no Extra sem análise — o mesmo.
  - `[low]` `[patch]` Edge: o `First` lança numa troca de Setup — `FirstOrDefault`.
  - `[low]` `[patch]` Edge: o player toca antes de carregar o descanso — bloqueia até carregar.
  - `[medium]` `[patch]` Edge: o `termico.json` ilegível vira "tudo frio" — assume a Tmax e esfria.
  - `[low]` `[patch]` Edge: o `Bloqueio` que lança pula o descanso — o descanso num try próprio.
  - `[low]` `[patch]` Edge: o `Impedimento` que lança — try com `MsgFalhou`.
  - `[low]` `[patch]` Edge: o alto-falante em dois canais com um sem ganho — incompleto.
  - `[low]` `[patch]` Edge (alegação): a gravação do fechar sem espera — o mesmo do Verification gap.
  - `[low]` `[patch]` Edge (alegação): o ganho que falta aparece como "modelo térmico" — o motivo real.
  - `[medium]` `[patch]` Blind: o laço de corte e descanso com a Tmax baixa — o mesmo do Edge.
  - `[medium]` `[patch]` Blind: o calor se perde quando a ficha fica incompleta; no descanso, o fim imediato — o mesmo da linha high.
  - `[medium]` `[patch]` Blind: a potência de reserva velha ou zerada — o mesmo da potência conservadora.
  - `[medium]` `[patch]` Blind: o descanso gravado depois da pausa e o fechar sem espera — grava antes e espera.
  - `[medium]` `[patch]` Blind: a trava não é recusada no descanso — recusa o 251 = 1 e o desmudo, na tela e na porta.
  - `[low]` `[patch]` Blind: sem o estado "sem limite" — "Extra: sem limite nesta faixa" e `semLimite`.
  - `[low]` `[patch]` Blind: "o player toca de novo" — retoma se tocava no corte.
  - `[low]` `[patch]` Blind: o Setup só com caixas ativas — o motivo que o usuário consegue resolver.
  - `[low]` `[patch]` Blind: o descanso mistura Setups — só o Setup do corte.
  - `[low]` `[patch]` Blind: faltam testes (sem análise, análise que some, mais de 99 min) — testes (a) a (g) e o formato h:mm:ss.
  - `[low]` `[reject]` Blind: as suposições ficam só no código — vão para o Spec Change Log no fim desta rodada.
  - `[low]` `[patch]` Blind: a temperatura ambiente com precisões diferentes — uma casa nos dois (a rota da porta para gravar fica fora: a intenção não pede).

## Design Notes

- **Por que duas partes desacopladas:** é a forma de duas constantes de tempo da spec (bobina rápida, ímã lento), com solução fechada para conferir e um passo exato por segundo.
- **Por que P = V²/Z:** é a mesma base do limite térmico da 13a (V = √(P·Z)). O RMS da ficha é potência média na impedância nominal.
- **Por que a faixa que toca:** a média de cada música muda o calor. Usar a média da playlist inteira subestimaria uma faixa pesada tocando em repetição.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- O `251` desligando de fato o A2DP é conferido na 12d.

## Auto Run Result

Status: done

**O que entrou.** O Extra térmico (spec-rendimento CAP-7):
- **A ficha do alto-falante** ganha cinco dados térmicos com origem (Tmax da bobina, Rth bobina→ímã, Rth ímã→ar, τ da bobina e τ do ímã).
- **O Setup** ganha a temperatura ambiente, com o padrão de 25 °C.
- **O modelo de duas constantes** (`Rendimento/ModeloTermico.cs`) usa P = V²/Z pelo RMS da faixa que toca, no master em uso, com o passo exato por segundo.
- **O modelo ao vivo** (`Rendimento/TermicoExtra.cs`):
  - mostra o tempo de Extra, ou "sem limite nesta faixa";
  - corta na Tmax, sem folga: grava o descanso, pausa, muta (`100`) e desliga o `251`;
  - descansa até ambiente + 1 °C, com a trava protegida contra a tela e a porta, e depois religa e retoma;
  - guarda tudo em `termico.json` (a cada 10 s e ao fechar, com espera) e retoma pelo relógio.
- **O Extra aparece** na folha só com os cinco dados e o teste a seco, com o selo "estimado" e o motivo real quando falta algo.
- **A porta:** `termico`, `tempoExtra`, `semLimite` e `descanso` no `extra`; `temperaturaAmbiente` no `/estado`.
- **ViewModels:** o `ExtraAoVivoViewModel` (novo), a linha do Extra e os campos térmicos e de temperatura.

**Arquivos** (em `app-controle/`):
- Núcleo:
  - `Rendimento/ModeloTermico.cs` e `TermicoExtra.cs` (novos);
  - `Equipamentos.cs`, `Setup.cs` e `GerenteSetups.cs`;
  - `AnalisePlaylist.cs`, `TesteSeco.cs`, `Niveis.cs`;
  - `Escritor.cs`, `ComandosAparelho.cs`, `Player.cs` e `PortaComandos.cs`.
- Telas: `ExtraAoVivoViewModel.cs` (novo), `NivelViewModel.cs` e `EquipamentosViewModel.cs`.
- App: `MauiProgram.cs` e `App.xaml.cs`.
- Testes:
  - `ModeloTermicoTestes`, `TermicoExtraTestes` e `ExtraAoVivoViewModelTestes` (novos);
  - a fixture `CasaDoDenis.SetupCasaComTermico` (dados térmicos de teste).

**Revisão.** 38 achados (1 high, 10 medium, 26 low, 1 maybe-false).
- **Patches:**
  - o descanso que nunca acaba no vazio e mantém as bobinas (high);
  - o Extra sem o modelo;
  - a potência conservadora;
  - a Tmax baixa;
  - o arquivo ilegível;
  - a ordem do corte e a gravação ao fechar;
  - a trava protegida;
  - o retomar;
  - o descanso por Setup;
  - as guardas (carregar, `Bloqueio`, `Impedimento`, `FirstOrDefault`);
  - os motivos reais;
  - o "sem limite" e o h:mm:ss;
  - a precisão da temperatura;
  - os testes (a) a (g).
- **Diferido (1):** a troca de faixa depois da queda.
- **Rejeitados,** com o motivo no Review Triage Log.

**Revisão de acompanhamento: recomendada.** Foi 1 entrada high com patch. O risco que ninguém conferiu é o modelo com dados reais de um alto-falante e o `251` cortando o A2DP de verdade. Só a 12d mostra isso.

**Verificação:**
- `dotnet test` Núcleo: 1338 de 1338.
- `dotnet test` Telas: 505 de 505.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- A matriz tem pelo menos um teste por linha.
