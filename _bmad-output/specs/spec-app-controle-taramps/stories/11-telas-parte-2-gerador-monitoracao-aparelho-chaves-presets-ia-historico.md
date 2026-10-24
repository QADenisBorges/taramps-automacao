---
title: 'História 11: telas, parte 2 — gerador, monitoração, aparelho, chaves, presets, IA e histórico'
type: 'feature'
created: '2026-10-24'
status: 'done'
baseline_revision: 'f8fe5364747971acd12d62dfd29ece52721ee8c3'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/10-telas-parte-1-leitura-ajustes-link-setups-equipamentos-memorias.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/porta-de-comandos.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/chaves-de-acesso.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** Depois da história 10, o app tem a casca e as telas de leitura, ajuste, Setups, equipamentos e memórias. Faltam na tela as demais funções, cujos ViewModels já existem e estão testados: Player, presets, gerador, voltímetro e clip, aparelho, chaves de acesso, IA pela rede, IA ativa, a confirmação da IA e o histórico (CAP-5, CAP-7 a CAP-13, CAP-15). Há também duas decisões do Denis de 24/10/2026 a cumprir no núcleo.

**Approach:** Montar essas telas em XAML na casca da 10, com bindings compilados aos ViewModels existentes. Acrescentar a aba Player, as linhas de Configurações, a folha de presets do chip, a faixa de monitoração da Mesa, a faixa e a tela da IA ativa e a folha de confirmação da IA. Fazer o reprodutor real de áudio no Android e o seletor de músicas. Cumprir as duas decisões no núcleo: o Player segue tocando quando o BLE cai, e os links ficam desligados sem Setup em uso.

## Boundaries & Constraints

**Always:**
- Visual e comportamento pelo `DESIGN.md` e pelo `EXPERIENCE.md`, nos mesmos moldes da 10:
  - tokens por nome, só o tema escuro, `x:DataType` em toda página e template, a lógica nos ViewModels de `BgsControle.Telas`, "—" sem leitura;
  - as saídas do Setup (`GerenteSetups.SaidasEmUso`) onde a tela mostra saídas (faixa de monitoração, voltímetro e clip).
- **Entram nesta história:**
  - **Aba Player:** a barra passa a Início · Mesa · Áudio · Player. A tela é a da seção "Player" do `EXPERIENCE.md`:
    - a playlist da memória do celular em uso, Adicionar músicas, Editar (remover e arrastar) e os controles fixos;
    - os estados "Sem playlist", "Playlist vazia", "Arquivo sumiu", "Bluetooth de áudio desligado" (com Ligar), "Celular não está mandando o som" e "Sem permissão".
  - **Presets:** a **folha de presets** do chip "Preset: X ▾" (Início), que aplica as 15 bandas por cima da memória; e **Configurações › Presets** (aplicar, renomear, apagar com as travas de lembrado e de herdado, exportar).
  - **Gerador:** o bloco da grade Áudio abre a tela, no lugar do "Gerador na próxima versão": tom fixo e sweep, a contagem de 30 s, o Parar grande e a faixa "DESLIGUE O MÓDULO AGORA" por cima de tudo quando a regra 1 manda.
  - **Monitoração:** a **faixa de monitoração** da Mesa (voltímetro e clip por saída) e **Configurações › Aparelho › Voltímetro e clip**.
  - **Aparelho** (grupo em Configurações): remote out, Bluetooth de áudio, nome do produto, troca de senha; reset de áudio e reset de fábrica em Avançado. Cada um com a chave, a folha de risco e a releitura.
  - **Chaves de acesso**, com o aviso do risco ao ligar.
  - **IA pela rede:** a chave, o código de 6 dígitos, o cliente pareado, Revogar e o modo do toque durante a IA.
  - **IA ativa:** abre sozinha quando a IA começa uma tarefa, com a faixa, o painel ao vivo, PARAR IA e os dois modos de toque.
  - **Confirmação da IA:** a folha por cima de tudo para cada operação de risco, com o prazo da porta.
  - **Histórico:** origem, hora e valor de cada comando.
  - **Configurações** completas: Setups, Memórias, Presets, Aparelho, Chaves, IA pela rede e Histórico, mais Reset de áudio e Reset de fábrica em Avançado.
- **Reprodutor real (Android):**
  - uma implementação de `IReprodutor` que toca a URI de conteúdo guardada e avisa o fim da faixa;
  - o seletor de arquivos de áudio do Android com várias músicas de uma vez, guardando a permissão persistente da URI;
  - o `Player` e o `PlayerViewModel` registrados no `MauiProgram`;
  - a música continua com o app em segundo plano enquanto o Android deixar (sem notificação nesta história).
- **Decisão 1 (Denis, 24/10/2026):** quando o BLE cai, o Player **continua tocando** a playlist da memória do celular que estava em uso.
  - A queda não troca nem limpa a playlist do Player.
  - Só uma memória diferente em uso depois de reconectar (aplicada, conferida ou escolhida) faz o Player parar e trocar, pela regra de 22/10.
  - Corrige o item adiado da 8d.
- **Decisão 2 (Denis, 24/10/2026):** sem Setup em uso, os links 1–2 e 3–4 ficam **desligados**.
  - Isso vale na dúvida, ao trocar de Setup e em Seguir sem Setup. Desistir da troca devolve os links do Setup em uso.
  - Um Setup criado do aparelho nasce com os links desligados.
  - Escolher um Setup aplica os links dele, como hoje.
  - Fecha o item adiado da 8e.

**Never:**
- Não mudar as telas da história 10 além de encaixar as entradas novas: a aba, a faixa da Mesa, o chip com folha e as linhas de Configurações.
- Não fazer a notificação de mídia nem o serviço em primeiro plano do Player (fica para depois).
- Não usar biblioteca de interface ou de áudio de terceiros.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Player na memória | FILME em uso com 3 faixas | Aba Player: "Playlist da FILME", as 3 faixas, tocar toca a primeira | Arquivo sumiu: faixa apagada com "Arquivo não encontrado", pulada |
| BLE cai tocando | Tocando a faixa 2; a conexão cai | Continua tocando; a playlist da FILME fica na tela | — |
| Reconecta noutra memória | Reconexão confere a MUSICA ORIG | Player para e mostra a playlist da MUSICA ORIG | — |
| Sem memória do celular | Memória do aparelho em uso | "Sem memória do celular em uso, não há playlist. Aplique uma memória do celular." e Tocar travado | — |
| Chip de preset | Toque em "Preset: Cinema ▾", escolhe Rock | Aplica só as 15 bandas; "Preset Rock aplicado na FILME." | Queda: pela metade |
| Gerador | Chave ligada, tom de 1 kHz a −20 dB | Contagem de 30 s e Parar; no fim, desliga e relê | Chave desligada: bloco travado com "Chave desligada" |
| Falha do gerador | Não consegue desligar e 3 reconexões falham | Faixa "DESLIGUE O MÓDULO AGORA" por cima de tudo | — |
| Voltímetro | Monitoração ao vivo | Faixa da Mesa com tensão e clip por saída do Setup | Sem leitura: "—" |
| IA pede risco | IA pede aplicar a FILME | Folha "A IA pede para aplicar a memória FILME" com o risco, por cima de tudo | Sem resposta no prazo: a folha some e a IA recebe a recusa |
| IA ativa | IA começa uma tarefa | Tela IA ativa abre sozinha; PARAR IA sempre visível | Modo "pausar ao toque": tocar pausa e mostra "IA pausada · retoma após 10 s sem toques" |
| Links sem Setup | Seguir sem Setup no Casa 2.1 | Links 1–2 e 3–4 desligados; quatro saídas CH1 a CH4 | Desistir da troca: links do Setup em uso voltam |
| Setup do aparelho | Criar Setup novo na dúvida | O Setup novo tem Link12 e Link34 desligados | — |

</intent-contract>

## Code Map

- `app-controle/src/BgsControle.Telas/CascaViewModel.cs` (`enum Aba { Inicio, Mesa, Audio }`) e `app-controle/src/BgsControle.App/Telas/CascaPage.xaml` -- a barra e as abas; entra `Player`.
- `app-controle/src/BgsControle.Telas/Abstracoes.cs` (`INavegacao`) e `app-controle/src/BgsControle.App/NavegacaoMaui.cs` -- a navegação da 10: telas cheias, `MostrarPorCima` (escolha do Setup, pela metade) e a exceção da conexão. A confirmação da IA, a IA ativa e a faixa "DESLIGUE O MÓDULO AGORA" entram aqui como camadas por cima.
- `app-controle/src/BgsControle.App/Telas/ConfiguracoesPage.xaml` + o `ConfiguracoesViewModel` (no arquivo de `SetupsViewModel.cs`) -- hoje só Setups e Memórias.
- ViewModels prontos, que as telas só ligam:
  - `PlayerViewModel.cs` e `PresetsViewModel.cs` (8d);
  - `GeradorViewModel.cs` (7a);
  - `MonitoracaoViewModel.cs` e `AparelhoViewModel.cs` (7b);
  - `ChavesViewModel.cs` (5);
  - `IaPelaRedeViewModel.cs` e `ConfirmacaoIaViewModel.cs` (9b);
  - `IaAtivaViewModel.cs` e `HistoricoViewModel.cs` (9a).
- `app-controle/src/BgsControle.Nucleo/Player/IReprodutor.cs`, `Player.cs`, `GerentePlaylists.cs` -- a porta do reprodutor e o Player. Hoje o `Player` segue `GerenteSetups.EmUso`, que a queda limpa (decisão 1). No `MauiProgram` só o `GerentePlaylists` está registrado.
- `app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs` -- `ConferirAsync` (dúvida), `TrocarAsync`, `DesistirAsync`, `SeguirSemSetupAsync`, `CriarSetupAsync` e `_link.DefinirEstadoAsync` (decisão 2).
- `app-controle/src/BgsControle.App/Controles/` e `Telas/` -- os controles e as páginas da 10, como modelo (fader, folha, pílula, linha, campo).
- `app-controle/src/BgsControle.App/Platforms/Android/` -- onde fica o reprodutor real (`Android.Media.MediaPlayer`) e o seletor (`Intent.ActionOpenDocument` com `audio/*`, múltiplos, `TakePersistableUriPermission`); `AndroidManifest.xml` para as permissões que faltarem.
- `app-controle/tests/BgsControle.Telas.Testes/` (`Bancada`, `TelasSaidasDoSetupTestes`, `TelasParte1Testes`) e `tests/BgsControle.Nucleo.Testes/` (`SetupsTestes`, `PlaylistsTestes`) -- os testes da casca e do núcleo.

## Tasks & Acceptance

**Execution:**
- `app-controle/src/BgsControle.Telas/CascaViewModel.cs`, `Abstracoes.cs` e `ConfiguracoesViewModel` -- a aba Player, os pedidos de navegação novos e as linhas de Configurações completas.
- `app-controle/src/BgsControle.App/Telas/` (novas) -- `PlayerView`, `PresetsPage` e a folha de presets, `GeradorPage`, `MonitoracaoPage` e a faixa da Mesa, `AparelhoPage`, `ChavesPage`, `IaPelaRedePage`, `IaAtivaPage`, a folha de confirmação da IA, a faixa "DESLIGUE O MÓDULO AGORA" e `HistoricoPage`; ligar o bloco Gerador e o chip de preset.
- `app-controle/src/BgsControle.App/NavegacaoMaui.cs` -- as telas cheias novas e as camadas por cima, com a prioridade: confirmação da IA e "DESLIGUE O MÓDULO AGORA" acima de tudo.
- `app-controle/src/BgsControle.App/Platforms/Android/ReprodutorAndroid.cs` e `SeletorMusicas.cs` (novos) e `MauiProgram.cs` -- o reprodutor, o seletor, e o registro do `Player`, do `PlayerViewModel` e dos ViewModels.
- `app-controle/src/BgsControle.Nucleo/Player/Player.cs` -- decisão 1.
- `app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs` -- decisão 2.
- `app-controle/tests/...` -- a matriz contra o simulador e um reprodutor falso, mais testes do núcleo para as duas decisões; os testes que dependiam dos links do Casa 2.1 na dúvida passam a esperar os links desligados.

**Acceptance Criteria:**
- Given o app compilado para Android, when o build roda, then não há erro nem aviso, com o XamlC validando os bindings.
- Given o Player tocando a FILME e a conexão caindo, when a sessão fica desconectada, then o reprodutor continua tocando e o Player continua na playlist da FILME.
- Given o Casa 2.1 com o Link 3–4 ligado, when o app entra em dúvida, then os links ficam desligados, e escolher o Casa 2.1 de novo os religa como o Setup guarda.

## Spec Change Log

## Review Triage Log

## Design Notes

- **Por que o reprodutor fica no projeto Android:** o núcleo e os ViewModels já falam com `IReprodutor`; só a ponta real é do Android, como o transporte BLE.
- **Sem aparelho:** se não houver celular no adb, a prova é o build com XamlC e os testes; a conferência visual e o áudio real ficam para a história 12.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- Com o celular no adb, instalar o APK e conferir as telas novas, o Player com uma música do celular e a confirmação da IA; sem celular, registrar que não foi feito.

## Auto Run Result

Status: done

**O que entrou.**
- **Decisão 1 (núcleo, `Player`):** na queda do BLE o Player fica com a memória de antes (`Retida`) e segue tocando; só uma memória do celular em uso depois de reconectar o solta (outra: para e troca; a mesma: segue). `GerenteSetups.ConexaoAberta` diz ao Player que foi queda. Conectado, a regra de 22/10 continua (carregar uma memória do aparelho para o Player).
- **Decisão 2 (núcleo, `GerenteSetups`):** links 1–2 e 3–4 desligados na dúvida (também a interrompida e sem Setup de referência), na troca e em Seguir sem Setup; Desistir devolve os do Setup em uso; Criar Setup nasce com `Link12`/`Link34` falsos; Escolher aplica os do Setup.
- **Telas:** aba Player (`PlayerView`, com Editar, − e arrastar ≡, e os avisos Sem permissão, Bluetooth de áudio desligado com Ligar, Som pelo celular); folha de presets do chip e `PresetsPage` (aplicar, renomear, apagar com as travas, exportar JSON); `GeradorPage` (tom fixo, sweep em Avançado, contagem e Parar) e o bloco Gerador travado pela chave (abre Chaves); faixa de monitoração na Mesa (por saída do Setup, ponto de clip no topo do canal) e `MonitoracaoPage`; Configurações completas (SOM, APARELHO com remote out, Bluetooth de áudio, nome, Senha › e Voltímetro ›, ACESSO E IA, Avançado com os resets e a folha do risco); `AparelhoPage` (troca de senha); `ChavesPage`; `IaPelaRedePage` (a chave, o código, o cliente, Revogar, o modo do toque); `IaAtivaPage`, `ConfirmacaoIaPage` (com o prazo contando) e `AlarmeGeradorPage` como camadas modais, com a confirmação e o alarme acima de tudo; `HistoricoPage`.
- **Android:** `ReprodutorAndroid` (MediaPlayer, wake lock parcial), `SeletorMusicas` (ACTION_OPEN_DOCUMENT `audio/*` múltiplo, permissão persistente), `AudioDoCelularAndroid` (saída Bluetooth, READ_MEDIA_AUDIO), `ExportarMaui` (compartilhar); manifesto com WAKE_LOCK, READ_MEDIA_AUDIO e READ_EXTERNAL_STORAGE (≤ 32). O MauiProgram registra o Player, a porta da IA (histórico e uso compartilhado na sessão) e os ViewModels; o App grava o histórico ao sair.

**Decisões de implementação a conferir com o Denis:**
- Depois de uma queda, o Player só solta a memória retida quando uma memória do celular entra em uso; "Som do aparelho" ou a dúvida na reconexão não o param.
- "Sem permissão" aparece quando o Android nega ler uma música guardada (a permissão da URI se perdeu); Permitir pede READ_MEDIA_AUDIO.
- "Celular não está mandando o som" olha se há um aparelho Bluetooth de áudio ligado ao celular (não sabe se é o processador).
- O Aparelho fica dentro de Configurações (como o mock); a `AparelhoPage` é a tela da Senha.

**Sem revisão:** a revisão de código (bmad-review) não rodou; recomendada sobretudo nas camadas modais do `NavegacaoMaui` e nas peças do Android, que não têm teste automático.

**Verificação:**
- `dotnet test` do Nucleo.Testes: 1001 passam (novo: `SetupsDecisoes2410Testes`).
- `dotnet test` do Telas.Testes: 440 passam (novo: `TelasParte2Testes`, com a matriz).
- `dotnet build` do App para `net10.0-android` (Debug, XamlC validando): 0 erros e 0 avisos.
- Sem celular no adb: nenhuma tela foi vista num aparelho, nenhuma música tocou de verdade e a porta da IA não foi testada na rede (história 12).
