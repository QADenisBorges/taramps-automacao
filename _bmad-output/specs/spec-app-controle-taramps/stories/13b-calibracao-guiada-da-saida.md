---
title: 'História 13b: calibração guiada da saída'
type: 'feature'
created: '2026-10-31'
baseline_revision: '9bb47ce0c1bacafb0f03be5e8455a5b8fae418f5'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-rendimento/limites-e-niveis.md'
  - '{project-root}/_bmad-output/specs/spec-rendimento/telas-rendimento.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/failure-modes.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** Sem o ganho do módulo medido, a 13a não converte volts em dB. Hoje o ganho só entra digitado na ficha. O app não guia a medida nem sabe o nível do Bluetooth. A porta também mexe em qualquer saída, mesmo sem saber a tensão (spec-rendimento CAP-3).

**Approach:**
- Uma operação de calibração no núcleo (`Rendimento/Calibracao.cs`) leva o usuário por uma saída do Setup em uso:
  - mede o ganho do módulo com o gerador e o multímetro;
  - mede o nível do Bluetooth com um tom de 0 dBFS tocado pelo app;
  - guarda os dois como "medido";
  - volta ao som de antes.
- Um `CalibracaoViewModel` dá os passos de `telas-rendimento.md`, sem XAML.
- A porta recusa o ganho, o limitador, o EQ e o MASTER de saída sem calibração. A tela deixa mexer, com aviso.

## Boundaries & Constraints

**Always:**
- **Qual saída:** uma `SaidaApp` do Setup em uso (um canal, ou o par com link no bridge) que chega a um módulo do grupo 2 (`Ligacoes.Caminhos`).
  - Uma saída sem módulo (só caixa ativa, ou nada) não se calibra. O motivo é "Esta saída não passa por um módulo.".
  - Medir o canal grava o `Ganho` do módulo, que é por módulo, e vale para os outros canais dele. Medir a ponte grava o `GanhoPonte`.
- **Para começar**, a calibração precisa de:
  - conexão, leitura pronta e firmware que não seja só leitura;
  - Setup em uso, sem dúvida e sem outra operação na cópia;
  - a chave do Gerador ligada;
  - o módulo ligado (`252` = 1). Senão, "Ligue o módulo (remote out) antes de calibrar.".
  
  Cada recusa vem com o seu motivo.
- **Passos** (`EtapaCalibracao`, nesta ordem):
  1. **`Desligar`:** "Desligue os alto-falantes do módulo." O usuário confirma.
  2. **`Multimetro`:** "Multímetro em V~ 200 na saída {CHn | ponte CHa+CHb} do {módulo}." Ao seguir, a operação começa:
     - a cópia antes, com o tipo `calibrar-saida` e `deixaGerador: true`;
     - as escritas com `daCopia`: `081` = 0 nos canais da saída, `080` = 0, o EQ de saída dos canais plano (ganho 0) e o limitador em Lref = min(−11,0; teto da 13a, se o canal for cercado);
     - os outros canais mutados (`100`).
  3. **`Tom`:** o gerador toca o tom em f a 0 dB e mostra a contagem de 30 s (regra 1). "Tocar de novo" vale enquanto não houver resposta.
     - f é a frequência de 50 a 400 Hz, numa grade de 10 Hz, com a menor atenuação do passa-alta e do passa-baixa dos canais (módulo LR/BT, como na 13a).
     - Se a atenuação passar de 3 dB, a calibração não começa: "O corte desta saída não deixa passar um tom entre 50 e 400 Hz.".
  4. **`DigitarTom`:** o usuário digita os volts, de 0,1 a 200. O gerador para.
     - ganho = V / (2,0 × 10^(Lref/20)). O ganho deve ficar entre 1 e 100; senão, "Confira a leitura: o ganho daria ×{g}." e o passo se repete.
     - O ganho é gravado na ficha do módulo (`Ganho` ou `GanhoPonte`) com a origem `Medido` e a fonte "Calibração guiada em {data}".
  5. **`Bluetooth`** (só se o Setup não tem o nível medido, ou se o usuário pede "Medir o Bluetooth de novo"):
     - pede o celular a 100 % e o som saindo pelo Bluetooth (`IAudioDoCelular.SaindoPeloBluetooth`; senão, "O som do celular não está saindo pelo Bluetooth.");
     - o limitador dos canais vai a min(0; teto);
     - o Player pausa;
     - o `ITomCelular` toca um seno de f a 0 dBFS por até 30 s.
  6. **`DigitarBluetooth`:** o usuário digita os volts. O tom para.
     - nível = 20·log10(V / (2,0 × ganho)), arredondado a 0,1 dB.
     - Se o nível ficar a menos de 0,5 dB do limitador, "O limitador segurou o tom; confira o volume." e o passo se repete.
     - O nível é gravado no Setup (`NivelBluetooth`, dB, com a origem `Medido` e a fonte).
  7. **`Fim`:** volta ao som de antes (`CopiaAntes.DesfazerAsync`, com o limitador no teto, como na 13a) e mostra "Ganho ×{g} e Bluetooth {n} dB gravados." Depois, "Religue os alto-falantes.".
- **Cancelar** (em qualquer passo depois do 2) para o gerador e o tom e volta ao som de antes. O que já foi gravado na ficha fica. Cair a conexão segue a regra 1 (gerador) e a regra 3: ao voltar, fica "aplicado pela metade". O Terminar diz "O app não sabe refazer esta operação.".
- **`NivelBluetooth` no Setup:** campo opcional do formato 3 (`"nivelBluetooth":{"valor":-13.0,"origem":"medido","fonte":"…"}`). Aceita valor negativo, e o formato não sobe. Sai no `GET /estado` como `setup.nivelBluetooth`.
- **`ITomCelular`** (novo, no núcleo): `TocarAsync(hz, duracao)` e `PararAsync()`. No Android é um `AudioTrack` com o seno gerado, em PCM 16 bits e 48 kHz. Nos testes, um dublê registra os pedidos.
- **Saída sem calibração:** uma saída com alto-falante passivo cujo módulo não tem o ganho medido (`LimiteSaida.Calibrada == false` com alto-falante).
  - **Pela porta** (origem IA), recusa com 409 "Sem calibração: {saída}. O app não sabe a tensão.":
    - `081`, `070`–`073` e `090`–`092` dos canais dela;
    - `080`, `030`–`032` e `020`, se alguma saída do Setup em uso está sem calibração.
    
    Isso vale para escritas avulsas e para lotes (`/memoria/aplicar`, `/criacao/ajuste`). Sem Setup em uso, esses códigos pela porta dão "Sem Setup em uso: o app não sabe a tensão das saídas.". A volta à cópia e o desfazer nunca são recusados por esta regra.
  - **Pela tela,** passa, e a saída mostra o aviso "Sem calibração: o app não sabe a tensão." (`SaidaTela`/`AjusteSaidaViewModel.AvisoCalibracao`).
- Nenhum texto do ViewModel contém "IA". As escritas seguem a cerca da 13a.

**Never:**
- XAML (13g).
- Níveis, master por memória e "revalidar" (13c).
- Travar o volume do celular (13e).
- Medir com o alto-falante ligado.
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Calibrar a JBL L | Casa sem calibração, chave do gerador ligada, módulo ligado | 081 = 0, 080 = 0, 071 = −11, CH2–CH4 mutados, tom ~400 Hz; 11,0 V → Ganho ×19,5 Medido | — |
| Ponte do Sub | Saída Sub (CH3+CH4) | Tom no passa-banda do Sub; 22,8 V com −11 → GanhoPonte ×40,5 | — |
| Bluetooth | Depois do ganho, 8,73 V | NivelBluetooth −13,0 dB Medido; o Player pausou; volta ao som de antes | Fora do Bluetooth: recusa com o motivo |
| Já medido | O Setup já tem o NivelBluetooth | Pula os passos 5–6 | "Medir o Bluetooth de novo" volta a eles |
| Leitura absurda | 0,3 V | "Confira a leitura: o ganho daria ×0,5." e repete | — |
| Chave desligada | Chave do Gerador desligada | Não começa; motivo da chave | — |
| Módulo desligado | 252 = 0 | "Ligue o módulo (remote out) antes de calibrar." | — |
| Corte estreito | Passa-alta e passa-baixa sem 50–400 Hz livre | Não começa; motivo do corte | — |
| Cancelar | No passo do tom | Gerador parado; releitura igual à cópia (com o limitador no teto, se for o caso) | — |
| Queda | BLE cai no tom | Regra 1; ao voltar, pela metade; Terminar recusa | — |
| Porta sem calibração | `/ajuste` 081 na JBL L sem ganho medido | 409 "Sem calibração: JBL L. …"; nada enviado | Tela: passa, com o aviso |
| Porta global | `/ajuste` 080 com o Sub sem calibração | 409 | — |
| Porta sem Setup | `/ajuste` 081 sem Setup em uso | 409 "Sem Setup em uso: …" | — |
| Volta pela porta | Cancelar a criação pela porta com saída sem calibração | A volta acontece | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- 13a:
  - `src/BgsControle.Nucleo/Rendimento/LimiteFisico.cs`: `LimiteCaminho.Ganho` (só Medido, 259–260), `LimiteSaida.Calibrada` (110, 228), `ParaDb`/`ReferenciaLimitador` (150, 277), `MinimoLimitador`/`MaximoLimitador` e `PassaAltaDe` (246);
  - `ModeloExcursao.cs`: `PassaAlta.Modulo`/`DoAparelho` (19–54). O passa-baixa equivalente é novo;
  - `Cerca.cs`.
- `src/BgsControle.Nucleo/Setups/Equipamentos.cs`: `Ligacoes.Caminhos` (internal, 875), `Seguir` (820–860) e `DadosTecnicos.Ganho`/`GanhoPonte` (148, 209).
- `src/BgsControle.Nucleo/Gerador/GeradorSinal.cs`:
  - `TocarTomAsync(hz, db)` (242), `PararAsync` (476) e o prazo (396–398, 527–556);
  - `MotivoParaNaoTocar` (451–471);
  - os códigos 200–213 são globais.
- `src/BgsControle.Nucleo/Escrita/CopiaAntes.cs`:
  - `Registrar(tipo, aoReconectar, refazer, deixaGerador)` (188), `IniciarAsync` (202), `AnotarEtapaAsync` (254) e `ConcluirAsync` (269);
  - `DesfazerAsync` (404), com o teto da 13a em `EscreverCopiaAsync`/`ComTetos`;
  - `MotivoBloqueioGerador` (136–145).
  
  O modelo é a criação (`CriacaoMemoria.cs:136, 244-247, 898`) e o preset (`GerentePresets.cs:455-541`).
- `src/BgsControle.Nucleo/Escrita/Escritor.cs`:
  - os hooks `Cerca`/`Bloqueio`/`CercaNaVolta`;
  - `Pedir` (com `Origem.Atual`, 274–304);
  - a escrita `internal EscreverAsync(..., daCopia)`.
  
  A regra "sem calibração" pela origem IA entra aqui, num hook novo, e no lote do `GerenteSetups.AplicarAlvoAsync` (2055–2090, onde a cerca já confere).
- `src/BgsControle.Nucleo/Uso/Origem.cs` e `Seguranca/Chaves.cs:26` (`OrigemComando`), `Chave.Gerador` (98) e `Chave.RemoteOut` (102).
- `src/BgsControle.Nucleo/Aparelho/ComandosAparelho.cs:137` (`252`) e `EstadoAparelho.Valor("252")`.
- `src/BgsControle.Nucleo/Setups/GerenteSetups.cs`:
  - `EditarEquipamentoAsync` (1366);
  - o modelo do campo opcional é o `ambiente` (`Setup.cs:15-19`, `ParaJson` 2343, `DeJson` 2448, `DefinirAmbienteAsync` 1290–1311);
  - `LimitesEmUso` (539) e `SaidasEmUso` (530).
- `src/BgsControle.Nucleo/Player/IReprodutor.cs` e `Player.cs` (sem Parar público; `TocarPausarAsync`). `IAudioDoCelular.SaindoPeloBluetooth` (`Telas/Abstracoes.cs:134-152`).
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs`: `RecusaDaPorta` (662–670), `Ajuste` (709), `AplicarMemoria` (757), `AjusteCriacao` (901) e `/estado` (1104).
- Telas:
  - `SaidasTela.cs` (14–45), `AjusteSaidaViewModel.cs` (99, 194) e `GeradorViewModel.cs` (`MotivoTrava` 220–240, a contagem);
  - Cuidado: a VM nova usa o `MotivoBloqueioGerador`, não o `MotivoBloqueio`.
- App: `src/BgsControle.App/Platforms/Android/` ganha o `TomCelularAndroid` (AudioTrack). Registro no `MauiProgram.cs`.
- Testes:
  - `ControleParametroViewModelTestes.Bancada` e `CasaDoDenis.EquipamentosSemCalibracao` (Digitado);
  - `PortaTestes` (`Pronto`, `Pedir`, `NovaPorta(setups:)`);
  - `GeradorViewModelTestes` (o `Dispose` confere que cada `*202*1*` tem o `*202*0*`).

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Nucleo/Rendimento/Calibracao.cs` (novo): as etapas, as travas para começar, a escolha de f, as escritas `daCopia`, o tom, as contas, a gravação, o cancelar, a queda e o registro do tipo na cópia.
- `src/BgsControle.Nucleo/Rendimento/ModeloExcursao.cs`: o `PassaBaixa.Modulo`/`DoAparelho` (041/043).
- `src/BgsControle.Nucleo/Player/ITomCelular.cs` (novo) e `Player.cs`: pausar o player durante o tom.
- `Setup.cs` e `GerenteSetups.cs`: o `NivelBluetooth` (registro, JSON, `DefinirNivelBluetoothAsync`), o gravar do ganho medido e o `/estado`.
- `Rendimento/SemCalibracao.cs` (novo), `Escritor.cs` e `GerenteSetups.cs`: a regra da porta, no avulso e no lote, com a volta isenta. `PortaComandos.cs`: o 409 e a ajuda.
- `src/BgsControle.Telas/CalibracaoViewModel.cs` (novo): os passos e os textos, a escolha da saída, a contagem, os campos de volts com a vírgula, cancelar, "Tocar de novo" e "Medir o Bluetooth de novo". `SaidasTela`/`AjusteSaidaViewModel`: o `AvisoCalibracao`.
- `src/BgsControle.App/Platforms/Android/TomCelularAndroid.cs` (novo) e `MauiProgram.cs`: o registro do tom e da VM.
- Testes:
  - `tests/BgsControle.Nucleo.Testes/CalibracaoTestes.cs` e `SemCalibracaoTestes.cs`: uma linha da matriz por teste;
  - `tests/BgsControle.Telas.Testes/CalibracaoViewModelTestes.cs`: o caminho inteiro da JBL L e do Sub pela VM, o aviso da tela e nenhum "IA".

**Acceptance Criteria:**
- Given o Casa sem calibração, when o usuário calibra a JBL L (11,0 V) e o Bluetooth (8,73 V) e depois o Sub (22,8 V), then o módulo tem Ganho ×19,5 e GanhoPonte ×40,5 Medido, o Setup tem o NivelBluetooth −12,7 dB Medido (o tom em 360 Hz; veja o Spec Change Log), a releitura é igual à de antes da calibração, e o `/limites` passa a dar −6,8 dB na JBL com o corte em 200 Hz.
- Given os testes que já existem, when a 13b entra, then todos passam, e o build Android sai sem erro nem aviso.

## Spec Change Log

- **01/11, depois da revisão:**
  - **O achado:** o nível do Bluetooth não clampa, então a medida incluía o corte, o EQ de entrada e as 15 bandas em f.
  - **A emenda** (fora do intent-contract):
    - a preparação também deixa planos o EQ de entrada e as 15 bandas, e põe o 070 em manual;
    - o nível gravado desconta a atenuação do corte em f;
    - f é o menor tom a 0,1 dB da melhor atenuação.
  - **Efeito no aceite:** no Casa com o corte da FILME, os 8,73 V gravam −12,7 dB (tom em 360 Hz; o passa-alta corta 0,26 dB). A linha da matriz (−13,0 dB) vale literalmente sem corte, e o teste `Bluetooth_sem_corte_com_8_73_V_grava_menos_13_0_dB_medido` a confere.
  - **KEEP:** as contas ×19,5 e ×40,5 do ganho e a volta com o teto da 13a.

## Review Triage Log

### 2026-11-01 — Review pass
- verdicts: 32 findings — high 0, medium 8, low 24, false 0, maybe-false 0
- findings:
  - `[low]` `[reject]` Intent: o tom sai por um `ITomCelular` à parte, não pelo Player — "o tom de 0 dBFS é gerado pelo app"; o Player só pausa, e o tom chega ao Bluetooth do mesmo jeito.
  - `[low]` `[reject]` Intent: a medida física (o multímetro) só é testada na conta — a conferência contra o VC830L é da 12d.
  - `[low]` `[reject]` Intent: a regra "sem calibração" pega o EQ de entrada, as 15 bandas e o sem Setup — leitura escolhida na spec (os globais mexem na tensão de toda saída).
  - `[low]` `[reject]` Intent: o aviso não leva ao "Calibrar ›" — o atalho e a tela são da 13g.
  - `[low]` `[reject]` Intent: a calibração não tem tela — a tela é da 13g, como diz a intenção.
  - `[low]` `[reject]` Intent: o aceite confere o `LimitesEmUso`, não a rota `/limites` — a rota só escreve o `LimitesEmUso` (13a, testada).
  - `[low]` `[patch]` Verification gap: o Lref com o teto abaixo de −11 nunca roda — teste (a).
  - `[low]` `[patch]` Verification gap: a escrita que falha ao preparar não volta ao som de antes em nenhum teste — teste (b).
  - `[low]` `[patch]` Verification gap: o tom do celular que falha — teste (c).
  - `[medium]` `[patch]` Blind: o nível do Bluetooth inclui o corte, o EQ de entrada e as 15 bandas em f — a preparação deixa o EQ de entrada e as 15 bandas planos, e o nível desconta o corte.
  - `[medium]` `[patch]` Blind: o passo do ganho não confere a margem sobre o limitador — o mesmo conserto (EQ de entrada e bandas planos).
  - `[low]` `[patch]` Blind: o 220 e o 010 escapam da regra — entram nos códigos globais.
  - `[low]` `[reject]` Blind: uma saída em dois módulos nunca fica calibrada — configuração rara, e o conserto pede outro fluxo.
  - `[medium]` `[patch]` Blind: o Cancelar no passo 2 deixa a marca e diz que a conexão caiu — conclui a cópia e fecha como cancelada.
  - `[medium]` `[patch]` Blind: o tom do celular continua depois de uma queda ou corrida; o AudioTrack vaza — para na troca de geração e trava na troca da faixa.
  - `[low]` `[patch]` Blind: o tom sem foco de áudio, e o Player que fica pausado — foco exclusivo passageiro, e o Player volta se tocava.
  - `[low]` `[patch]` Blind: o tom vai a 400 Hz, a borda do multímetro — escolhe o menor f a 0,1 dB do melhor.
  - `[low]` `[reject]` Blind: sem piso de precisão da leitura — as leituras de 9 a 23 V do exemplo ficam longe da resolução; o aviso pediria outro passo.
  - `[low]` `[reject]` Blind: gravar com o Setup ocupado perde a leitura — uma operação de Setup durante a calibração é rara (a cópia trava as outras).
  - `[medium]` `[patch]` Blind: o 070 (modo do limitador) não é posto — vai a manual com `daCopia` antes do 071.
  - `[low]` `[reject]` Blind: o lote e a escrita avulsa usam listas de saídas diferentes — a mesma decisão da 13a.
  - `[low]` `[reject]` Blind: o aceite diz "releitura igual" e a volta põe o teto; a chave de ajustes de áudio não está na spec — no aceite o limitador está abaixo do teto; a chave de áudio é a trava de toda escrita.
  - `[low]` `[patch]` Blind: faltam testes (o tom que falha, a preparação que falha, a recalibração) — testes (a) a (c).
  - `[low]` `[patch]` Blind: o campo `Mute` sem uso, os códigos fixos e o `_vez` sem descarte — limpos.
  - `[medium]` `[patch]` Edge: o Cancelar durante o `SeguirAsync` deixa a marca — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o Setup muda entre o Abrir e o Seguir — confere de novo o Setup, o módulo e a ponte.
  - `[medium]` `[patch]` Edge: o tom do celular continua depois de fechar — o mesmo do Blind.
  - `[medium]` `[patch]` Edge: o 070 em auto — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o 220 escapa da regra — o mesmo do Blind.
  - `[low]` `[patch]` Edge: o AudioTrack vaza se o Play lança — o mesmo grupo do tom.
  - `[low]` `[reject]` Edge (alegação): o 409 de uma saída que o `/limites` mostra calibrada — a spec define "sem calibração" pelo ganho dos alto-falantes passivos; um alto-falante sem limite calculável continua pedindo o ganho.
  - `[low]` `[reject]` Edge (alegação): uma leitura fora de 0,1–200 V deixa o gerador tocando — de propósito, para digitar de novo; o prazo de 30 s vale.

## Design Notes

- **Por que mutar as outras saídas:** o gerador é global. Os alto-falantes do módulo estão soltos, mas uma caixa ativa noutro canal tocaria um tom a 0 dB.
- **Por que o limitador fixa a tensão:** com o ganho 0 e o master 0, o tom a 0 dB passa do limiar em 11 dB. A saída do processador fica em 2,0 × 10^(Lref/20), que é a medida de 17/10 (−11 dB → 0,564 V → 11,0 V). O EQ de saída plano e a escolha de f evitam que um corte deixe o sinal abaixo do limiar. O EQ de entrada e as 15 bandas ficam como estão [ASSUMPTION: um corte deles maior que ~8 dB em f daria uma medida baixa; a 12d confere].
- **Por que o Bluetooth com o limitador alto:** o Bluetooth entra ~13 dB abaixo do 0 dB interno. O limitador em min(0; teto) não pode segurá-lo, e se segurar, a conta avisa.
- **Por que a volta é isenta:** a 13a já decidiu que a volta à cópia sempre acontece. Recusar a volta da porta deixaria a marca sem saída.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- No aparelho, a calibração contra o VC830L é da 12d.

## Auto Run Result

Status: done

**O que entrou.** A calibração guiada da saída e a regra "sem calibração" (spec-rendimento CAP-3):
- **O núcleo** (`Rendimento/Calibracao.cs`) conduz os passos:
  - desligar os alto-falantes;
  - o multímetro;
  - o tom do gerador em f;
  - digitar os volts, e o ganho do módulo grava como medido;
  - o tom de 0 dBFS do celular pelo Bluetooth;
  - digitar os volts, e o NivelBluetooth do Setup grava como medido;
  - a volta ao som de antes.
- **A preparação,** com a cópia antes e escritas `daCopia`:
  - o ganho e o master em 0;
  - o EQ de saída, o de entrada e as 15 bandas planos;
  - o 070 em manual e o 071 em Lref;
  - as outras saídas mutadas.
- **Cancelar, cair a conexão e Terminar** seguem as regras 1 a 3.
- **A porta** recusa com 409 o ganho, o limitador e o EQ da saída sem calibração. Recusa também os códigos globais (080, 030–032, 020, 220 e 010) quando alguma saída está sem calibração, e tudo isso sem Setup em uso. A volta fica isenta.
- **A tela** deixa mexer, com o aviso "Sem calibração: o app não sabe a tensão.".
- **O `ITomCelular`** toca o tom no Android por um `AudioTrack` com foco de áudio exclusivo.

**Arquivos** (em `app-controle/`):
- Núcleo:
  - `Rendimento/Calibracao.cs` e `SemCalibracao.cs` (novos);
  - `Player/ITomCelular.cs` (novo) e `Player.cs` (`PausarAsync`);
  - `ModeloExcursao.cs` (`PassaBaixa`);
  - `Setup.cs` e `GerenteSetups.cs` (o `NivelBluetooth`, `GravarGanhoMedidoAsync` e o lote da porta);
  - `Escritor.cs` (o gancho `SemCalibracao`), `CriacaoMemoria.cs` e `PortaComandos.cs`.
- Telas: `CalibracaoViewModel.cs` (novo), `SaidasTela.cs` e `AjusteSaidaViewModel.cs`.
- App: `Platforms/Android/TomCelularAndroid.cs` (novo) e `MauiProgram.cs`.
- Testes: `CalibracaoTestes`, `SemCalibracaoTestes` e `CalibracaoViewModelTestes` (novos).

**Revisão.** 32 achados (8 medium, 24 low).
- **Patches:**
  - o nível do Bluetooth sem o viés do corte e do EQ;
  - o 070 em manual;
  - o 220 e o 010 na regra;
  - o Cancelar no meio do passo 2;
  - o tom do celular que parava errado, e o vazamento do `AudioTrack`;
  - o foco de áudio, e o Player que volta a tocar;
  - f no meio da faixa;
  - a reconferência do Setup e do módulo;
  - a limpeza;
  - os testes do Lref com o teto, da preparação que falha e do tom que falha.
- **Nada diferido.**
- **Rejeitados,** com o motivo no Review Triage Log.

**Revisão de acompanhamento: recomendada.** Foram 4 entradas medium com patch. O risco que ninguém conferiu é o tom real do Android:
- o mono nos dois lados do Bluetooth;
- o volume a 0 dBFS;
- o foco de áudio;
- o modo manual do 070 segurando o tom no aparelho.

Só a 12d mostra isso.

**Verificação:**
- `dotnet test` Núcleo: 1192 de 1192.
- `dotnet test` Telas: 483 de 483.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- A matriz tem pelo menos um teste por linha.

**Decisões provisórias:**
- os canais da própria saída são desmutados;
- o ganho grava com 2 casas;
- a calibração também pede a chave "Ajustes de áudio";
- numa saída com dois módulos, só o primeiro é calibrado;
- uma leitura fora de 0,1–200 V deixa o tom tocando, para digitar de novo.
