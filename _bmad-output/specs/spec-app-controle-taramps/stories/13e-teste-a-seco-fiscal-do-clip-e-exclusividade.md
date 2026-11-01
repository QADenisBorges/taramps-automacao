---
title: 'História 13e: teste a seco, fiscal do clip e exclusividade'
type: 'feature'
created: '2026-11-01'
baseline_revision: 'e492abc07d6842ccbe1200b684652b01d0a1aa29'
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

**Problem:** A 13d calcula o teto do Extra pelos picos da playlist, mas nada o confirma no aparelho, e nada vigia o uso. O Extra (spec-rendimento CAP-6 e CAP-8) precisa de três coisas:
- um teste a seco com o módulo desligado, antes de o teto subir;
- um fiscal do clip no uso;
- a garantia de que só o player do app, no volume da conta, alimenta o processador.

**Approach:**
- `Rendimento/TesteSeco.cs` desliga o módulo, põe o master no candidato, toca os trechos fortes da 13d e escuta o 150. A cada clip, baixa o master um passo. No fim, guarda o teto validado e só então religa o módulo.
- `Rendimento/FiscalExtra.cs` vigia o uso do Extra:
  - o clip baixa o master e marca a faixa;
  - outro player muta e pausa;
  - o volume do celular fica travado.
- O nível Extra entra no núcleo com o teto validado. A tela dele é da 13f e da 13g.

## Boundaries & Constraints

**Always:**
- **Teste a seco** (por memória otimizada, preset em uso, análise "pronta"):
  1. **`Aviso`:** "O módulo vai ser desligado." O usuário confirma.
  2. **`Preparar`:**
     - o app pausa o player;
     - desliga o módulo (`252` = 0, `ComandosAparelho.DefinirRemoteOutAsync(false)`), conferido pela releitura; se não desligar, nada mais acontece;
     - guarda a cópia antes (tipo `teste-seco`);
     - escreve o master no candidato da 13d (`TetoExtra.Master`).
  3. **`Tocando`:** o player toca, em ordem, os trechos da análise (2 por faixa, ~10 s cada), do início ao fim de cada trecho.
     - A tela mostra a faixa e o trecho ("Faixa {n} de {total}: {nome}, trecho {1|2}").
     - Durante o trecho, o app escuta o `150`. Um bit ligado numa saída é um clip:
       - o master desce um passo (1 dB [ASSUMPTION: o passo]);
       - a faixa e a saída são marcadas (`ClipNoTeste`);
       - o mesmo trecho toca de novo.
     - Se o master chegar a −80, o teste falha com "O clip não sumiu nem com o master no mínimo.".
  4. **`Resultado`:**
     - grava o teto validado (o master que passou por todos os trechos) na memória, para o preset e a impressão da cadeia (`ExtraValidado`);
     - escreve esse master;
     - conclui a cópia;
     - religa o módulo (`252` = 1).
     
     A tela mostra "Teto validado: {master} dB." ou "Clip na {faixa}, saída {saída}: o teto desceu para {master} dB.".
- **Estado seguro:** o módulo só volta a ligar com o master no validado.
  - O Cancelar, uma falha ou a queda deixam o módulo desligado.
  - Na queda, a cópia vira "pela metade", e o Terminar diz "O app não sabe refazer esta operação.". O Voltar à cópia devolve o master, mas o módulo continua desligado até o usuário ligar.
  - Fechar o app não religa nada.
- **O nível Extra no núcleo:**
  - `NivelRendimento.Extra` só pode ser escolhido com o `ExtraValidado` do preset em uso e a impressão atual.
  - O master do Extra é o validado. A cerca vai a min(X, C), como no Máximo.
  - O que a 13c refaz na hora não vale para o Extra: o master dele só muda pela revalidação abaixo e pelo fiscal.
  - A escolha do Extra na tela fica apagada até a 13f, com "Falta o modelo térmico." (CAP-7).
- **Revalidação no Extra** (`limites-e-niveis.md`):
  - **Análise refeita com o teto candidato menor:** o master desce na hora para o candidato.
  - **Candidato maior:** fica o validado até um novo teste a seco.
  - **Ficha ou ganho medido mudou:** a memória fica `Revalidar = true`. O master desce, se precisar, e não sobe. O selo é "revalidar", com o motivo "A ficha ou a calibração mudou.".
  - Um teste a seco que passa tira o `Revalidar`.
- **Fiscal no uso do Extra** (só com a memória em uso no Extra e o player do app tocando):
  - **Clip não previsto** (bit do `150` numa saída):
    - o master desce um passo (1 dB) na hora;
    - a faixa é marcada com a saída e o momento (segundos), e a marca fica na memória;
    - o 2º clip não previsto na mesma faixa põe a memória em `Revalidar`, com o motivo "Clip duas vezes na {faixa}.".
  - **Outro player** (`IOutroPlayer.Comecou`):
    - o app muta os 4 canais (`100`) e pausa o player;
    - a mensagem é "Outro app começou a tocar. O som foi mutado.";
    - o mudo sai quando o usuário volta a tocar no player do app.
  - **Volume do celular** (`IVolumeCelular`): ao começar a tocar no Extra, o app põe o volume de mídia no máximo (o valor da conta, 100 %). Se ele mudar, o app o devolve e avisa "No Extra o volume fica no máximo; use o master.".
  - **Fechar o app no Extra** não tira o mudo que estiver no aparelho. Isso vale para o mudo do outro player (CAP-8) e para o descanso da 13f.
- **Interfaces (no núcleo):** `IOutroPlayer` (`Comecou`) e `IVolumeCelular` (`Volume`, `Maximo`, `DefinirAsync`, `Mudou`). No Android, `AudioManager`: as configurações de reprodução e o `ContentObserver` do volume. Nos testes, dublês.
- **`IReprodutor`** ganha `TocarTrechoAsync(uri, inicio, duracao)`. No Android, `SeekTo` e uma parada no fim do trecho. Nos testes, o dublê registra os trechos.
- **O simulador** ganha `Sinal(saida, dB)`: o nível de pico que chega à saída antes do limitador. Ele emite o `#150#bits#` quando o nível + o master passa do limiar do 071 da saída [ASSUMPTION: o 150 marca o limitador agindo; a 12d confere]. O dublê do player chama o `Sinal` com o pico do trecho que toca (da análise da 13d).
- **A porta:** o `extra` do `/limites` ganha `validado` (o master), `revalidar` (bool e motivo) e `marcas` [{faixa, id, saida, segundos}]. Rodar o teste a seco pela porta não faz parte.
- **ViewModels:**
  - `TesteSecoViewModel`: os passos, os textos, o Confirmar e o Cancelar, o progresso por trecho e o resultado;
  - a folha do outro player e o aviso do volume (no `PlayerViewModel`, ou num `FiscalViewModel`);
  - o selo "revalidar" com o motivo, na memória.
  
  Nenhum texto contém "IA".

**Never:**
- Ligar o módulo antes de o master estar no validado.
- O modelo térmico e o descanso (13f).
- XAML (13g).
- Rodar o teste a seco com o módulo ligado.
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Teste sem clip | Análise pronta, candidato −4,7, sem clip | `252` = 0 antes do 1º trecho; todos os trechos tocados; validado −4,7; master −4,7 escrito antes de `252` = 1 | — |
| Clip num trecho | O simulador acusa clip na JBL L no 2º trecho | O master desce para −5,7; o trecho repete; validado −5,7; a faixa marcada | — |
| Queda no meio | BLE cai no 3º trecho | O módulo fica desligado; pela metade; nada validado | Terminar recusa |
| Cancelar | Cancelar no `Tocando` | O player para; o master volta à cópia; o módulo fica desligado | — |
| Não desligou | `252` não confirma 0 | Nada toca; "Não consegui desligar o módulo." | — |
| Escolher o Extra | Validado no preset em uso | Nível Extra; master = validado; cerca min(X, C) | Sem validado: "Falta o teste a seco." |
| Candidato desce | Análise refeita, o candidato 2 dB abaixo | O master desce 2 dB na hora | Candidato sobe: fica o validado |
| Ficha mudou | Xmax da JBL mudado | `Revalidar` com o motivo; o master não sobe | — |
| Clip no uso | Extra tocando; clip na saída Sub | O master −1 dB; a faixa marcada (Sub, segundos) | 2º na mesma faixa: `Revalidar` |
| Outro player | `IOutroPlayer.Comecou` no Extra | Mudo nos 4 canais; o player pausa; a mensagem | Fora do Extra: nada |
| Volume | O volume do celular cai para 60 % no Extra | O app devolve ao máximo e avisa | — |
| Porta | `GET /limites` | `extra.validado`, `revalidar` e `marcas` | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Rendimento/AnalisePlaylist.cs` (13d): `TetoExtra` (`Master`, `Estado`, `Motivo`), `TetoEmUso`/`TetoDa`, os trechos por faixa, a impressão da cadeia e `Mudou`.
- `src/BgsControle.Nucleo/Rendimento/Niveis.cs` (13c): `NivelRendimento` (o Extra entra), `GerenteNiveis` (fica de fora do Extra) e as mensagens. `GerenteSetups.DefinirNivelAsync`/`Conta`/`ContaEmUso`/`RecusaDoMaster` e o hook do master no `Escritor`.
- `src/BgsControle.Nucleo/Rendimento/Cerca.cs`/`LimiteFisico.cs` (`semTermico` para o Máximo e o Extra).
- `src/BgsControle.Nucleo/Aparelho/ComandosAparelho.cs:137` (`DefinirRemoteOutAsync`) e `Chave.RemoteOut`.
- `src/BgsControle.Nucleo/Aparelho/MonitorAparelho.cs:64` (`ClipSaida`), o `150` no catálogo (128) e `Parametro.cs:286-298` (bits por saída).
- `src/BgsControle.Nucleo/Escrita/CopiaAntes.cs`: `Registrar`, `IniciarAsync`, `ConcluirAsync` e `DesfazerAsync`. O modelo é a `Calibracao` (13b).
- `src/BgsControle.Nucleo/Player/IReprodutor.cs`, `Player.cs` (`PausarAsync`, `EmAnalise`) e `src/BgsControle.App/Platforms/Android/ReprodutorAndroid.cs`.
- `src/BgsControle.Nucleo/Setups/Setup.cs`/`GerenteSetups.cs`: a `MemoriaCelular` ganha `ExtraValidado`, `Revalidar` e `Marcas` (opcionais no formato 4).
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs`: o `extra` do `/limites`.
- `tests/BgsControle.Simulador/ProcessadorSimulado.cs`: `EmitirClip` (458–470) e o estado do `150`. A `Sinal` nova entra aqui.
- Telas: `CalibracaoViewModel` (o modelo de passos), `PlayerViewModel` e `NivelViewModel`.

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Nucleo/Rendimento/TesteSeco.cs` (novo): os passos, o módulo, a cópia, o master, os trechos, a escuta do 150, o passo de 1 dB, o resultado e o estado seguro.
- `src/BgsControle.Nucleo/Rendimento/FiscalExtra.cs` (novo): o clip no uso, as marcas, o `Revalidar`, o outro player e o volume.
- `Niveis.cs`, `GerenteSetups.cs`, `Setup.cs`: o nível Extra, `ExtraValidado`/`Revalidar`/`Marcas` e a revalidação no Extra.
- `src/BgsControle.Nucleo/Player/IOutroPlayer.cs`, `IVolumeCelular.cs` (novos), `IReprodutor.cs` (`TocarTrechoAsync`) e `Player.cs`.
- `src/BgsControle.App/Platforms/Android/` (`OutroPlayerAndroid`, `VolumeCelularAndroid` e o trecho no `ReprodutorAndroid`) e `MauiProgram.cs`.
- `tests/BgsControle.Simulador/ProcessadorSimulado.cs`: `Sinal(saida, dB)` e o 150 por nível.
- `PortaComandos.cs`: `validado`, `revalidar` e `marcas`.
- `src/BgsControle.Telas/TesteSecoViewModel.cs` (novo), `PlayerViewModel.cs` e `NivelViewModel.cs`.
- Testes:
  - `tests/BgsControle.Nucleo.Testes/TesteSecoTestes.cs` e `FiscalExtraTestes.cs`: uma linha da matriz por teste;
  - `tests/BgsControle.Telas.Testes/TesteSecoViewModelTestes.cs`.

**Acceptance Criteria:**
- Given uma memória otimizada com a análise pronta, when o usuário roda o teste a seco, then o simulador recebe `252` = 0 antes do primeiro trecho e `252` = 1 só depois do master validado. Um clip simulado num trecho derruba o teto daquela combinação.
- Given o Extra em uso, when o simulador acusa um clip não previsto, then o master desce um passo e a faixa fica marcada. O segundo na mesma faixa põe a memória em "revalidar".
- Given os testes que já existem, when a 13e entra, then todos passam, e o build Android sai sem erro nem aviso.

## Spec Change Log

- **01/11, implementação e revisão:**
  - Um teste da 13c (`Niveis.De("extra")` dava null) passou a esperar o Extra, que agora existe.
  - **Esclarecimentos da revisão** (fora do intent-contract):
    - o teste a seco põe o volume do celular no máximo e confere cada trecho tocado de verdade;
    - o fiscal fica parado durante o teste;
    - o Extra desatualizado usa min(validado, o pior caso do Máximo);
    - as marcas saem num teste que passa;
    - o mudo do fiscal é gravado em `fiscal-extra.json`.
  - **KEEP:** a ordem do fim (master confirmado → validado gravado → cópia concluída → módulo religado, nunca depois de um Cancelar ou do fechar) e o passo de 1 dB com o mesmo trecho repetido.

## Review Triage Log

### 2026-11-01 — Review pass
- verdicts: 50 findings — high 2, medium 12, low 34, false 1, maybe-false 1
- findings:
  - `[low]` `[reject]` Intent: o fiscal não se alcança no app (o Extra fica apagado até a 13f) — é o que a spec decidiu (a CAP-7 pede o modelo térmico).
  - `[low]` `[reject]` Intent: o clip do simulador vem de um nível calculado pelo dublê, não da cadeia do simulador — a intenção diz "por nível de sinal", marcado como suposição.
  - `[medium]` `[patch]` Intent: o estado seguro só vale no teste a seco; o fiscal só registra no log as escritas que falham — muta na falha do master e avisa.
  - `[medium]` `[patch]` Intent: o outro player age com o player parado — exige o player tocando.
  - `[medium]` `[patch]` Intent: as marcas nunca são limpas — limpas no teste que passa.
  - `[medium]` `[patch]` Intent: "o master desce se precisar" não acontece sem a análise pronta — no Extra desatualizado, o master é min(validado, o pior caso do Máximo).
  - `[low]` `[reject]` Intent: os trechos de ~10 s, as Android e o selo por memória não têm teste ou tela — a bancada usa trechos curtos; o Android é da 12d; o selo e o fluxo Otimizar→teste são da 13g.
  - `[low]` `[reject]` Intent: o módulo religa mesmo se estava desligado antes; o `revalidar` em dois campos — a spec manda religar no fim; os dois campos dizem o mesmo que "bool e motivo".
  - `[medium]` `[patch]` Blind: o teste a seco não põe o volume do celular no máximo — põe e confere.
  - `[medium]` `[patch]` Blind: o trecho conta pelo relógio; uma perda de foco pausa e passa — o trecho pausado sozinho falha, e a posição é conferida.
  - `[low]` `[patch]` Blind: o `OutroPlayerAndroid` conta players (falso positivo) — só os ativos de mídia ou jogo.
  - `[medium]` `[patch]` Blind: a Próxima pula o `AntesDeTocar` — passa a chamar.
  - `[low]` `[patch]` Blind: o mudo do outro player fica só na memória — gravado em arquivo.
  - `[medium]` `[patch]` Blind: as marcas nunca saem — o mesmo do Intent.
  - `[medium]` `[patch]` Blind: o validado é gravado antes de o master ser confirmado, e o `ok` está morto — confirma antes e devolve a falha do religar.
  - `[medium]` `[patch]` Blind: o validado velho continua valendo — o mesmo do Intent.
  - `[medium]` `[patch]` Blind: o outro player com o player parado — o mesmo do Intent.
  - `[low]` `[patch]` Blind: o 150 que demora conta de novo ou cai no trecho seguinte — espera o 150 apagar depois do passo.
  - `[low]` `[patch]` Blind: o teste para o player em vez de pausar — pausa.
  - `[low]` `[patch]` Blind: com várias saídas no clip, só a primeira fica — todas.
  - `[low]` `[patch]` Blind: o teste não confere o limitador desligado — recusa com o 071 OFF.
  - `[low]` `[patch]` Blind: o volume pelo `ContentObserver` e o `ShowUi` — o broadcast do volume, sem a tela, com o aviso do limite.
  - `[low]` `[patch]` Blind: o −80 nunca é tentado — tenta antes de falhar.
  - `[low]` `[patch]` Blind: faltam testes (foco, Próxima, fechar, arquivo, marcas, volume) — testes (a) a (h).
  - `[low]` `[reject]` Blind: o `MotivoTrava` do ViewModel é caro — o desempenho na tela é conferido na 12d; não muda o comportamento.
  - `[medium]` `[patch]` Edge: a perda de foco no meio do trecho — o mesmo do Blind.
  - `[high]` `[patch]` Edge: um Cancelar ou o fechar durante o `ValidarAsync` religa o módulo — confere o token e o `_fim` antes de religar.
  - `[medium]` `[patch]` Edge: o validado gravado antes da falha do master — o mesmo do Blind.
  - `[low]` `[patch]` Edge: a mensagem do religar esconde a causa — a causa real.
  - `[low]` `[patch]` Edge: os players ociosos de outro app — o mesmo do Blind.
  - `[medium]` `[patch]` Edge: o fiscal muta no meio do teste a seco — o fiscal fica parado durante o teste.
  - `[low]` `[patch]` Edge: o toque no play entre soltar a vez e pausar — pausa antes de soltar.
  - `[low]` `[patch]` Edge: o mudo desconhecido volta a 0 — não restaura o desconhecido.
  - `[medium]` `[patch]` Edge: a Próxima depois do mudo — o mesmo do Blind.
  - `[low]` `[patch]` Edge: a memória vai ao Extra já tocando, e o volume não sobe — sobe.
  - `[low]` `[patch]` Edge: o volume limitado pelo sistema não avisa — avisa.
  - `[medium]` `[patch]` Edge: as marcas velhas depois de um teste que passa — o mesmo do Blind.
  - `[low]` `[patch]` Edge (alegação): parar em vez de pausar — o mesmo do Blind.
  - `[high]` `[patch]` Edge (alegação): o módulo volta depois do Cancelar ou do fechar — o mesmo da linha high acima.
  - `[medium]` `[patch]` Edge (alegação): o outro player com o player parado — o mesmo do Intent.
  - `[maybe-false]` `[patch]` Edge (alegação): a ficha mudada não recalcula o master do Extra — coberto pelo min(validado, pior caso) do Intent.
  - `[low]` `[patch]` Verification gap: sair do Extra sem teste — teste (a).
  - `[low]` `[patch]` Verification gap: aplicar a memória ou o preset no Extra sem teste — teste (b).
  - `[low]` `[patch]` Verification gap: o bloqueio do player só como delegate — teste (c).
  - `[low]` `[patch]` Verification gap: a Próxima sem o `AntesDeTocar` — o conserto do Blind e o teste (g).
  - `[low]` `[patch]` Verification gap: o `PlayerViewModel` do fiscal sem teste — teste (d).
  - `[low]` `[patch]` Verification gap: fechar o app no meio do teste — teste (e).
  - `[low]` `[patch]` Verification gap (outro): a Próxima — o mesmo grupo.
  - `[false]` `[reject]` Verification gap (outro): os adaptadores do Android sem teste — a própria camada não abriu achado (12d).
  - `[low]` `[patch]` Intent (superfície): o `revalidar` do teste a seco desce o master só com a análise pronta — o mesmo grupo do min(validado, pior caso).

## Design Notes

- **Por que o módulo desligado:** no teste a seco o processador e o celular rodam a cadeia inteira, mas nada chega aos alto-falantes. O 150 diz se alguma saída passou do limitador com o master candidato.
- **Por que repetir o trecho depois do clip:** confirma que o novo master passa naquele mesmo trecho antes de seguir.
- **Os trechos da 13d:** foram escolhidos pelo maior pico entre as saídas (`deferred` da 13d). Se a 12d mostrar que um trecho com folga menor passa sem ser tocado, a escolha muda para a folga.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- No aparelho, o teste a seco real, o 150 e o outro player são da 12d.

## Auto Run Result

Status: done

**O que entrou.** O teste a seco, o fiscal do clip e a exclusividade do Extra (spec-rendimento CAP-6 e CAP-8):
- **O teste a seco** (`Rendimento/TesteSeco.cs`):
  - desliga o módulo (`252` = 0, conferido) e põe o master no candidato da 13d;
  - põe o volume do celular no máximo;
  - toca os trechos fortes e escuta o 150. A cada clip, baixa 1 dB e repete o trecho; um trecho que não tocou inteiro também se repete;
  - no fim, confirma o master, grava o validado por preset e impressão, conclui a cópia e só então religa o módulo.
  
  Um Cancelar, uma falha, a queda ou o fechar deixam o módulo desligado. O teste recusa o limitador desligado.
- **O nível Extra no núcleo:** usa o master validado (ou min(validado, o pior caso do Máximo) quando está desatualizado) e a cerca em min(X, C). Sair do Extra com o limitador acima é recusado.
- **A revalidação:** o candidato que desce baixa o master na hora. Uma ficha ou calibração mudada põe "revalidar".
- **O fiscal** (`Rendimento/FiscalExtra.cs`), com o Extra em uso e o player tocando, fora do teste:
  - o clip baixa 1 dB e marca a faixa (todas as saídas); o 2º clip na mesma faixa põe "revalidar"; uma falha do master muta;
  - outro player muta e pausa, e o mudo fica gravado até o próximo play;
  - o volume do celular fica travado no máximo.
- **Interfaces:** `IOutroPlayer`, `IVolumeCelular` e o `TocarTrechoAsync` do `IReprodutor`, com as versões do Android e os dublês.
- **O simulador** ganha o `Sinal` e emite o 150 quando o limitador age (suposição CLIP-LIMITADOR).
- **A porta:** `validado`, `validadoVigente`, `revalidar`, `motivoRevalidar` e `marcas` no `extra`.
- **ViewModels:** `TesteSecoViewModel`, a folha do outro player e a marca de clip no `PlayerViewModel`, e o selo "revalidar" no `NivelViewModel`.

**Arquivos** (em `app-controle/`):
- Núcleo:
  - `Rendimento/TesteSeco.cs` e `FiscalExtra.cs` (novos);
  - `Niveis.cs`, `Setup.cs` e `GerenteSetups.cs`;
  - `AnalisePlaylist.cs` (`ImpressaoAtual`);
  - `MonitorAparelho.cs` (evento `Clip`);
  - `Player/IOutroPlayer.cs` e `IVolumeCelular.cs` (novos), `IReprodutor.cs` e `Player.cs`;
  - `PortaComandos.cs`.
- Telas: `TesteSecoViewModel.cs` (novo), `PlayerViewModel.cs`, `NivelViewModel.cs` e `RepositorioTextoArquivos.cs`.
- App:
  - `Platforms/Android/OutroPlayerAndroid.cs` e `VolumeCelularAndroid.cs` (novos);
  - `ReprodutorAndroid.cs`, `MauiProgram.cs` e `App.xaml.cs`.
- Simulador: `ProcessadorSimulado.cs` (`Sinal`), `Suposicoes.cs` e `SUPOSICOES.md`.
- Testes: `TesteSecoTestes`, `FiscalExtraTestes`, `TesteSecoViewModelTestes` e `BancadaExtra` (novos).

**Revisão.** 50 achados (2 high, 12 medium, 34 low, 1 false, 1 maybe-false).
- **Patches:**
  - o módulo nunca religa depois de um Cancelar ou do fechar (high);
  - a ordem do fim e o resultado do religar;
  - o volume no teste;
  - o trecho conferido;
  - o fiscal parado no teste e só com o player tocando;
  - a falha do master muta;
  - a corrida do mudo;
  - a Próxima;
  - o mudo gravado;
  - o Extra desatualizado;
  - as marcas;
  - o limitador desligado;
  - o 150 que demora e as várias saídas;
  - o −80;
  - o outro player e o volume no Android;
  - os testes (a) a (h).
- **Nada diferido.**
- **Rejeitados,** com o motivo no Review Triage Log.

**Revisão de acompanhamento: recomendada.** Foram 2 entradas high com patch. O risco que ninguém conferiu é o aparelho real:
- o 150 como sinal do limitador;
- a detecção do outro player no Android;
- o volume travado com o limite de volume seguro do sistema;
- o teste a seco com o módulo desligado de verdade.

Tudo isso é da 12d.

**Verificação:**
- `dotnet test` Núcleo: 1301 de 1301.
- `dotnet test` Telas: 499 de 499.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- A matriz tem pelo menos um teste por linha.

**Decisões provisórias:**
- o passo de 1 dB;
- o 150 marca o limitador agindo;
- o fiscal junta os clips a menos de 3 s num só;
- o selo "revalidar" fica na folha do nível (só a memória em uso) até a 13g;
- o Extra só se escolhe na tela a partir da 13f.
