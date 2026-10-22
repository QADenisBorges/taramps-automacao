---
title: 'História 7a: gerador de sinal e as regras de falha 1 e 2'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
baseline_commit: '23c44326de790efbab484c1de1c6840b5f00079f'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/failure-modes.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/chaves-de-acesso.md'
  - '{project-root}/protocolo/MAPA.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** O app ainda não usa o gerador de sinal do processador: tom fixo e sweep (CAP-8). Um tom esquecido ligado pode queimar o tweeter ou o sub. Por isso o gerador só entra com as regras 1 e 2 de `failure-modes.md`: prazo de 30 s, desligamento garantido e, ao reconectar, desligar antes de qualquer outra ação.

**Approach:** No núcleo, um `GeradorSinal` pela porta única da sessão, com a chave "Gerador". Ele faz a contagem de 30 s, desliga em toda saída e tenta reconectar até 3 vezes; depois, mostra o alarme. Na leitura de cada conexão, ele desliga o gerador antes que a leitura seja dada como terminada. Em `BgsControle.Telas`, o `GeradorViewModel`, testado contra o `ProcessadorSimulado`. A história 7 foi dividida: monitoração, remote out, Bluetooth de áudio e os comandos de aparelho inteiro ficam para a 7b.

## Boundaries & Constraints

**Always:**
- **Trava.** Passam, só na forma exata do codificador do catálogo, com a sessão Conectada e fora do modo só leitura: `*200*v*`, `*201*v*`, `*210*v*`, `*211*v*`, `*212*v*`, `*213*i*` e `*202*1*`, com a chave "Gerador" ligada. **`*202*0*` passa sem a chave:** desligar é sempre o lado seguro. Isso vale mesmo se o Denis desligar a chave com o tom tocando. Fora da forma exata, a recusa é "comando do gerador fora do formato". 251, 252, 002, 004, 242 e 001 continuam "não liberados" (são da 7b).
- **Tocar.** Envia os parâmetros, cada um confirmado pelo eco ou pela releitura, e por último o `*202*1*`.
  - Tom fixo: `*213*0*`, `*201*hz*`, `*200*dB*` e `*202*1*`.
  - Sweep: `*210*`, `*211*`, `*212*`, `*213*v*` (v de 1 a 3) e `*202*1*`.
  - [SUPOSIÇÃO GERADOR-MODO] O tom fixo é o 213 em 0. O sweep é o 213 de 1 a 3. O 202 liga e desliga os dois.
  - [SUPOSIÇÃO ECO-GERADOR] Os códigos 200–202 e 210–213 ecoam no formato de leitura, como o 081.
  - Faixas do catálogo: frequências de 10 a 22000 Hz; ganhos de −60,0 a 0,0 dB.
  - Fica recusado, sem enviar nada, quando:
    - um valor está fora da faixa;
    - a sessão está sem leitura ou lendo;
    - uma operação de memória está em andamento ou ficou pela metade;
    - o gerador já está em uso.
  - Se um parâmetro falha, o `*202*1*` não sai.
- **Prazo.** São 30 s contados a partir da confirmação do `*202*1*`. Quando o prazo vence, o app envia `*202*0*` e confirma pelo eco ou pela releitura de `#202#`. O `*202*0*` não espera uma leitura completa em andamento. A partir do envio do `*202*1*`, o app trata o gerador como "talvez ligado" e só deixa de tratar quando lê 202 = 0.
- **Regra 1.** O app desliga também nestes casos:
  - no Parar;
  - num erro ao ligar;
  - quando o Denis toca em Desconectar ou Esquecer a senha (o app desliga antes de soltar);
  - ao sair do app ou fechá-lo.
  - Se a conexão cair, ou se o desligar não for confirmado, o app tenta reconectar até 3 vezes. Cada tentativa é um `Sessao.ConectarAsync` completo, e a regra 2 desliga o gerador. Se as 3 falharem, o estado vira **SemResposta**: alarme "DESLIGUE O MÓDULO AGORA", que fica até uma leitura mostrar 202 = 0.
- **Regra 2.** Ao fim de cada leitura completa, antes do aviso de fim (e, portanto, antes do espelho, da retomada da cópia e de qualquer escrita), o app confere o 202.
  - Se o 202 estiver ligado e o app não estiver tocando, o app desliga e avisa "O gerador estava ligado e foi desligado."
  - No modo só leitura, nada sai e vale o alarme.
  - O espelho passa a mostrar o 202 confirmado.
- **`GeradorViewModel`** (Telas):
  - os campos do tom e do sweep, semeados pelo espelho;
  - Tocar e Parar;
  - a contagem regressiva em segundos;
  - o motivo da trava ("Chave desligada", "Sem conexão", "Modo só leitura"…);
  - a mensagem e o alarme.
  - O Parar nunca fica travado.
- **Testes** contra o simulador: a linha do tempo do 202 prova cada desligamento e o prazo de ≤ 30 s.

**Never:**
- Voltímetro, clip, 251, 252, resets, troca de senha e nome do produto (são da 7b).
- XAML (é da história 11).
- Escrita fora de `Sessao.EnviarAsync`.
- Limite de ganho próprio do app além da faixa do catálogo (é decisão do Denis, não do app).

## I/O & Edge-Case Matrix

| Cenário | Entrada / estado | Esperado | Erro |
|---|---|---|---|
| Tom fixo | Chave ligada, 1000 Hz, −20 dB | `*213*0*`, `*201*1000*`, `*200*-20.0*`, `*202*1*`; tocando, 30 s | — |
| Prazo | 30 s depois | `*202*0*`, relido 0: "O prazo de 30 s acabou: o gerador foi desligado." | — |
| Sweep | 20 → 200 Hz, −30 dB, médio | `*210*20*`, `*211*200*`, `*212*-30.0*`, `*213*2*`, `*202*1*` | — |
| Parar | Tocando | `*202*0*` na hora; desligado | — |
| Chave desligada | Tocar | Nada sai | "Chave desligada" |
| Fora da faixa | +1,0 dB ou 5 Hz | Nada sai | A faixa |
| Sem eco no desligar | `*202*0*` silenciado | Relê `#202#` = 0; desligado | — |
| Queda tocando | Cai; reconecta | Na leitura, `*202*0*` antes de qualquer outra escrita | — |
| Queda sem volta | 3 reconexões falham | SemResposta e "DESLIGUE O MÓDULO AGORA" | Alarme até ler 202 = 0 |
| Ligado ao conectar | Simulador com 202 = 1 | Leitura, `*202*0*`, aviso "estava ligado" | — |
| Desconectar tocando | Tela desconecta | `*202*0*` antes de soltar | — |
| Ler de novo tocando | Tocando | Não desliga | — |
| Só leitura com 202 = 1 | Firmware R004 | Nada sai | Alarme |

</frozen-after-approval>

## Code Map

- `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs`:
  - `Trava`: acrescentar o gerador; tirar 200–202 e 210–213 de `ComandosNaoLiberados`.
  - `DesconectarAsync` e `EsquecerSenhaAsync`: um gancho interno "antes de desconectar".
- `app-controle/src/BgsControle.Nucleo/Catalogo/Catalogo.cs`:
  - `ComandoDeGerador(quadro)`, no padrão de `ComandoDeMemoria` e `EscritaDeAudio`;
  - `Extras` 200–213, que já existem com as faixas;
  - `Parametro.CodificarEscrita` valida a faixa.
- `app-controle/src/BgsControle.Nucleo/Leitura/LeituraAutomatica.cs`: um gancho interno, aguardado em `ExecutarAsync` antes de `Terminou`.
- `app-controle/src/BgsControle.Nucleo/Memorias/GerenteMemorias.cs` (`EnviarEEsperarAsync`, `Aguardo`): modelo do envio com espera, no ritmo de `TemposEscrita`. Vira uma classe pequena reusável, que a 7b também vai usar; o gerente e o `Escritor` não mudam.
- `app-controle/src/BgsControle.Nucleo/Escrita/CopiaAntes.cs` (`MotivoBloqueio`) e `EstadoAparelho` (`Atualizar`, `Pronto`, `Lendo`): reusar.
- `app-controle/src/BgsControle.Telas/MemoriasViewModel.cs`: modelo das travas e das mensagens (`ControleParametroViewModel.Msg*`).
- `app-controle/tests/BgsControle.Simulador/` (`LinhaDoTempoGerador`, `Silenciar`, `CairDepoisDaEscrita`, `Endereco = null`, `Firmware`; `Suposicoes.cs` e `SUPOSICOES.md`): o simulador já ecoa o gerador; falta marcar as suposições novas.
- Testes que hoje esperam "não liberado" para o gerador: `EscritaTestes` (linhas 447 e 448), `LeituraTestes` (linha 571) e `SessaoTestes` (linha 246).

## Tasks & Acceptance

**Execution:**
- [x] `app-controle/src/BgsControle.Nucleo/Catalogo/Catalogo.cs`: `ComandoDeGerador`.
- [x] `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs`: a trava e o gancho de desconectar.
- [x] `app-controle/src/BgsControle.Nucleo/Leitura/LeituraAutomatica.cs`: o gancho antes do fim.
- [x] `app-controle/src/BgsControle.Nucleo/Escrita/EnvioComResposta.cs`: o envio com espera, reusável.
- [x] `app-controle/src/BgsControle.Nucleo/Gerador/GeradorSinal.cs`: o gerador, o prazo e as regras 1 e 2.
- [x] `app-controle/src/BgsControle.Telas/GeradorViewModel.cs`: a tela, sem XAML.
- [x] `app-controle/src/BgsControle.App/MauiProgram.cs` e `App.xaml.cs`: o DI, criar o gerador cedo e desligar ao sair ou fechar.
- [x] Os testes: as linhas da matriz, os testes antigos atualizados e as suposições GERADOR-MODO e ECO-GERADOR em `SUPOSICOES.md` e `Suposicoes.cs`.

**Acceptance Criteria:**
- Given qualquer teste do gerador, when o simulador é inspecionado, then cada `*202*1*` tem um `*202*0*` aceito até 30 s depois, ou a queda e o alarme. Fora do gerador, só chegaram a senha e as leituras do catálogo.
- Given uma conexão nova com o 202 em 1, when a leitura termina, then o `*202*0*` é a primeira escrita depois das leituras.

## Implementation Notes

- **O prazo conta do envio do `*202*1*`, não da confirmação.** O Always diz "a partir da confirmação", e o critério de aceite exige o `*202*0*` até 30 s depois do `*202*1*`. Como o eco leva cerca de 85 ms, as duas coisas não valem ao mesmo tempo. Ficou o lado seguro: o tom nunca passa de 30 s, e a contagem na tela começa na confirmação, com o que falta (cerca de 29,9 s).
- Decisões que a spec não fecha:
  - Se o 202 não vier na leitura, o app manda `*202*0*` por segurança, sem o aviso "estava ligado".
  - Um parâmetro que volta com outro valor conta como falha, e o `*202*1*` não sai.
  - Se o Desconectar não consegue confirmar o desligar, a reconexão da regra 1 começa mesmo assim.
  - Depois do alarme, o app não abre outra reconexão sozinho; ela volta quando o Denis conecta.
- **Desligar ao sair do app.** O app desliga no `Stopped` e no `Destroying` da janela. No Android, o `Stopped` vem também quando a tela bloqueia, e aí o tom para. O desligar no fechamento não espera terminar, e o Android pode matar o processo antes do `*202*0*`. Nesse caso, a regra 2 desliga na próxima conexão.
- **Corrida na revisão (corrigida):** a frase do resultado ("O prazo de 30 s acabou…") chegava depois do estado "desligado", e um teste da tela falhava de vez em quando. Agora a frase entra junto com a confirmação.
- **Novos:**
  - `Gerador/GeradorSinal.cs`;
  - `Escrita/EnvioComResposta.cs`, que a 7b reusa;
  - `Telas/GeradorViewModel.cs`;
  - os ganchos `Sessao.AntesDeSoltar` e `LeituraAutomatica.AntesDoFim`.

- **Depois da revisão (22/10):** as correções 3, 4, 5, 6, 7, 9, 11, 16 e 18 da tabela abaixo entraram, com seis testes novos. Um efeito delas: uma leitura que lança agora também passa pela regra 2, e o `*202*0*` de segurança sai. Por isso, o teste antigo `Leitura_que_falhou_diz_que_o_app_ainda_nao_leu` anda o relógio falso, e o `ConferirSoAudio` da bancada aceita o `*202*0*`.
- **Verificação final:** Nucleo.Testes 538 de 538 e Telas.Testes 172 de 172, rodados três vezes; build Android com 0 erros e 0 avisos.

## Review Triage Log

| # | Achado | Veredito | Evidência | Rota |
|---|---|---|---|---|
| 1 | O prazo conta do envio, e a spec diz "da confirmação" | low | Contradição da própria spec com o critério de aceite; o envio é o lado seguro | rejeitado (anotado acima) |
| 2 | O `*202*0*` do prazo chega alguns ms depois de 30 s do envio | low | O timer vence em exatamente 30 s; falta só a latência da escrita BLE | rejeitado |
| 3 | O espelho volta a 202 = 1 depois de um Parar rápido | low | `Atualizar` vinha antes da checagem de `_cancelarLigar` | patch |
| 4 | Parar com o eco do 202 1 pendente: a releitura pega o `#202#0#` | low | A releitura com `Casar202(null)` não olhava o cancelamento | patch |
| 5 | Leitura que lança: a regra 2 não roda e a reconexão espera para sempre | medium | `Falhar` não chama `AntesDoFim`; `await tentativa.Task` sem prazo | patch |
| 6 | O ritmo de 30 ms quebra com dois envios ao mesmo tempo | low | Os dois leem o mesmo `_ultimoEnvio` | patch |
| 7 | 202 sem leitura e desligar sem confirmação: estado desconhecido, sem alarme | low | `_talvezLigado` ficava falso | patch |
| 8 | `Substituir202` monta um `cru` que o aparelho não mandou | low | Só no retrato salvo; o espelho está certo | rejeitado |
| 9 | Parar com o alarme troca a frase do alarme | low | `Avisar` sem olhar `Alarme`; o `Alarme` continuava valendo | patch |
| 10 | Sweep com a inicial ≥ a final | maybe-false | Não se sabe se o aparelho aceita sweep descendente | rejeitado (história 12) |
| 11 | O ViewModel não trava com uma memória em andamento | low | O núcleo recusa; a tela deixava tocar | patch |
| 12 | Os ganchos aceitam um só dono | false | Só existe um `GeradorSinal`; a 7b não usa os ganchos | rejeitado |
| 13 | O desligar ao sair não espera terminar | medium | Esperar na thread da tela arrisca travar; a regra 2 cobre a próxima conexão | defer (conferir no celular) |
| 14 | `DesconectarAsync` mudou quando já está Desconectado | false | O código antigo já voltava cedo nesse caso | rejeitado |
| 15 | "Gerador em uso" com `TalvezLigado` e estado Desligado | low | Com o item 7, esse estado leva à reconexão ou ao alarme | rejeitado |
| 16 | Faltam testes: erro ao ligar, sair do app, prazo no meio de uma leitura, 202 sem leitura, Parar entre o 202 1 e a confirmação | medium | Verification gap: as mutações passam verdes | patch |
| 17 | `EnvioComResposta` sem testes próprios e sem `Dispose` | low | Coberto pelos testes do gerador; vive o app inteiro | rejeitado |
| 18 | O log diz "3 reconexões" depois de parar antes | low | Texto fixo | patch |
| 19 | O Desconectar parece travado enquanto desliga (até ~7 s) | low | Só tocando e sem eco | rejeitado |
| 20 | A velocidade do sweep ignora um valor inválido; os campos não voltam ao espelho | low | Vem de uma lista na tela | rejeitado |

## Verification

**Commands:**
- `dotnet test app-controle/BgsControle.slnx`: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug`: 0 erros e 0 avisos.
