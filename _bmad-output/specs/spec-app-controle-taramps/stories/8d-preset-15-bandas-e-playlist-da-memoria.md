---
title: 'História 8d: preset de 15 bandas e playlist da memória do celular'
type: 'feature'
created: '2026-10-23'
status: 'done'
baseline_revision: 'c3c235cc1f142b97a51f416d0933c64b78cf58f0'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/SPEC.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/conhecimento-herdado.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/porta-de-comandos.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/8c-setup-e-memoria-no-celular.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
warnings:
  - oversized
deferred:
  - summary: >-
      Desconectado, o Player fica sem playlist e para, mas o EXPERIENCE.md diz que ele continua tocando, porque a música vai pelo Bluetooth de áudio e não pelo BLE.
    evidence: |-
      A spec da 8d pôs "desconectado" na lista de "sem playlist", e a queda limpa o som em uso (8c). O estado "Desconectado" do EXPERIENCE.md diz que o Player continua tocando, porque é Bluetooth de áudio e não o BLE. O Player ainda não está registrado no MauiProgram (o reprodutor real é da história 11), então nada toca hoje. Corrigir na 11, com a decisão do Denis.
    location: >-
      app-controle/src/BgsControle.Nucleo/Player/Player.cs
    severity: medium
---

<intent-contract>

## Intent

**Problem:** Depois da 8c, o preset ainda é o som completo (84 valores, com o herdado FILME), a memória do celular não lembra preset, o Salvar ignora as 15 bandas, as playlists ainda são por posição das 3 memórias do aparelho, o Player segue a memória do aparelho e a IA não aplica memória do celular (CAP-5, CAP-12, CAP-15).

**Approach:** O preset vira só as 15 bandas (`020`), com o herdado "Cinema"; cada memória do celular lembra o último preset e o aplica junto; o Salvar ganha a linha "15 bandas → preset"; as playlists passam a ser por id de memória do celular, com migração do `playlists.json` antigo; o Player segue a memória do celular em uso; a porta da IA ganha `POST /memoria/aplicar`.

## Boundaries & Constraints

**Always:**
- Um preset guarda só as 15 bandas (`020`, banda 0 a 14). O `220` (preset de EQ do aparelho) não entra em preset nem em memória. Aplicar um preset escreve só as bandas que diferem, com a cópia antes e a releitura (regra 3); a chave é "Ajustes de áudio" (sem confirmação da IA).
- O preset herdado é o "Cinema" (id fixo, as 15 bandas da leitura de 18/10, `ConhecimentoHerdado.BandasCinema`), no lugar do preset herdado FILME. Herdado não se renomeia, não se apaga e não se sobrescreve.
- `presets.json` passa ao formato 2 (só as 15 bandas). Um arquivo no formato 1 é lido assim: cada preset próprio que tem as 15 bandas vira um preset de formato 2 com essas bandas; o que não tem é descartado. O arquivo é regravado no formato 2 na próxima gravação.
- Cada memória do celular guarda o id do preset lembrado (ou nenhum). A FILME herdada lembra o Cinema. Aplicar uma memória escreve também as bandas do preset lembrado (no mesmo alvo da cópia antes). Aplicar um preset com uma memória do celular em uso faz a memória lembrar esse preset. Memória nova lembra o "preset em uso": o lembrado pela memória do celular em uso, ou o último aplicado nesta conexão; nenhum depois de carregar uma memória do aparelho ou de uma queda.
- Apagar um preset lembrado por alguma memória é recusado com "Lembrado por N memória(s)." (frase no singular para 1).
- Salvar por camadas, a linha das bandas (a referência é o preset lembrado pela memória do celular em uso; sem preset lembrado, o EQ plano, todas as bandas em 0,0 dB):
  - memória do celular em uso e bandas diferentes da referência: "15 bandas → preset X", com a escolha **Atualizar o preset X** ("Vale para toda memória que usa ele.") ou **Criar preset novo** (com nome); nenhuma vem escolhida; com preset herdado, Atualizar fica travado ("Preset herdado não é sobrescrito.") e Criar vem escolhido; sem preset lembrado, só Criar. Criar faz a memória lembrar o preset novo.
  - memória nova (memória do aparelho, "Som do aparelho" ou Setup sem memórias): se as bandas lidas são iguais às de um preset existente, a memória nova lembra esse preset sem pergunta; senão, "15 bandas → preset novo", com nome, lembrado pela memória nova.
  - Nada mudou (calibração, caráter e bandas): "Nada para salvar.", como na 8c.
- Playlists: `playlists.json` formato 2 = playlists por id de memória do celular, mais as "pendentes" por nome. Migração do formato 1 (por posição 0 a 2): cada playlist não vazia vira pendente com o nome do protótipo daquela posição (MUSICA HIGH, MUSICA ORIG, FILME, de `ConhecimentoHerdado.NotaMemoria`); vazias somem. Uma pendente é adotada pela primeira memória do celular com o mesmo nome (sem diferença de caixa), procurando no Casa 2.1 primeiro e depois nos outros Setups na ordem, memórias na ordem de criação; a adoção acontece ao carregar e sempre que memórias são criadas ou renomeadas. Sem memória correspondente, a pendente espera, sem prazo.
- Apagar uma memória do celular (ou o Setup dela) apaga a playlist dela. Nada mais apaga playlist (um `setups.json` ilegível não apaga playlists).
- O Player toca a playlist da memória do celular em uso; sem ela (memória do aparelho, "Som do aparelho", sem Setup, desconectado), não há playlist: "Sem memória do celular em uso, não há playlist. Aplique uma memória do celular." Ao trocar a memória em uso, o Player para (decisão do Denis de 22/10, mantida).
- Porta da IA: `POST /memoria/aplicar` com `{"memoria": "<id ou nome>"}` (nome procurado só no Setup em uso, sem diferença de caixa), chave "Aplicar memória do celular", sempre com confirmação na tela ("A IA pede para aplicar a memória FILME", risco "Troca todo o som de uma vez."); bloqueado como os outros POST durante a dúvida. `GET /estado` ganha a lista das memórias do celular do Setup em uso (id, nome, em uso, preset lembrado) e o preset em uso. `/ajuda` descreve o caminho novo.

**Never:**
- Não mexer na conferência ao conectar (as bandas não entram nela), nas camadas da 8c nem no salvar/renomear do aparelho (continuam fora).
- Não criar telas XAML (histórias 10 e 11); o chip de preset do Início é da história 10 (aqui fica o núcleo).
- Não apagar playlist por falta de memória correspondente.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Aplicar preset | FILME em uso, aplica um preset "Rock" | Só bandas diferentes escritas; releitura; FILME passa a lembrar Rock | Queda no meio: pela metade |
| Aplicar memória | FILME lembra Cinema; bandas planas no aparelho | Calibração + caráter + bandas do Cinema; releitura confere | Preset lembrado sumiu (arquivo editado): aplica sem as bandas, com aviso |
| presets.json antigo | Formato 1 com 2 presets de 84 valores e 1 sem bandas | 2 presets de 15 bandas; o terceiro descartado | Arquivo estragado: nenhum próprio |
| Apagar preset lembrado | Rock lembrado por 2 memórias | Recusa "Lembrado por 2 memórias." | Cinema: "Preset herdado não muda." |
| Salvar, bandas mexidas | FILME (Cinema) em uso, banda 100 Hz mudou | Linha "15 bandas → preset Cinema"; Atualizar travado; Criar escolhido; com nome "CINEMA 2", preset novo e FILME lembra ele | Nome inválido ou repetido: recusa, nada guardado |
| Salvar, atualizar preset | Memória lembra "Rock" (próprio), bandas mudaram, escolhe Atualizar | Rock com as bandas novas | Confirmar travado até escolher |
| Salvar, memória nova | Memória do aparelho em uso; bandas iguais às do Cinema | Memória nova lembra o Cinema, sem pedir nome de preset | Bandas diferentes de todos: pede nome do preset novo |
| Migrar playlists | Formato 1: posição 2 com 3 faixas, posição 1 com 2, posição 0 vazia | FILME (herdada) adota as 3; "MUSICA ORIG" fica pendente | — |
| Adoção tardia | Pendente "MUSICA ORIG"; Denis cria a memória MUSICA ORIG | A memória nova já tem as 2 faixas; a pendente sai | — |
| Apagar memória | Memória com playlist | Memória e playlist somem | — |
| Player | Aplica a memória FILME | Player mostra a playlist da FILME; troca para outra memória → para | Memória do aparelho em uso: sem playlist, tocar travado |
| IA aplica memória | POST /memoria/aplicar {"memoria":"FILME"} | Folha de confirmação; confirmado, aplica; histórico com origem IA | Chave desligada: 409 sem folha; nome inexistente: 400; dúvida: 409 |

</intent-contract>

## Code Map

- `app-controle/src/BgsControle.Nucleo/Presets/Preset.cs` e `GerentePresets.cs` -- hoje um preset são os 84 valores do Marco 1 (`Catalogo.ConsultasMarco1` em `GuardarAsync`, `ValoresQueFaltam`, `DeJson`); `Formato = 1`; herdados vêm de `ConhecimentoHerdado.Presets`. `AplicarInternoAsync` (cópia antes, diferenças, releitura) serve como está para as bandas. Precisa: formato 2 e a leitura do 1, criar preset das bandas atuais (o antigo "guardar o som atual"), atualizar as bandas de um próprio, recusa de apagar lembrado (pergunta a quem lembra por um `Func` posto pelo `GerenteSetups`, como o `GerenteMemorias.Bloqueio` da 8c), evento depois de aplicar com sucesso.
- `app-controle/src/BgsControle.Nucleo/Herdado/ConhecimentoHerdado.cs:29-110,190-205` -- `PresetFilme` (84 valores) é a base do Setup Casa 2.1 e da memória FILME (via `Da(...)`): manter a leitura completa interna (para o Setup e a memória) e expor o preset herdado `Cinema` (id fixo novo, 15 bandas = `BandasCinema`). As notas e a seção "Presets" que falam do preset FILME passam a falar do Cinema. A memória FILME herdada lembra o Cinema.
- `app-controle/src/BgsControle.Nucleo/Setups/Setup.cs` -- `MemoriaCelular` ganha o id do preset lembrado; `FolhaSalvar` ganha a linha e as perguntas das bandas; `ResultadoSetup` como está.
- `app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs` -- `Alvo(setup, memoria)` (~linha 1416) passa a incluir as bandas do preset lembrado; `CriarMemoriaAsync`, `NovaMemoria`, `PrepararSalvar` e `SalvarAsync` (~1174-1330) ganham o preset; `ApagarMemoriaAsync`/`ApagarSetupAsync` avisam a playlist; `AoMudarSessao`/`AoCarregar` limpam o preset em uso; `ParaJson`/`DeJson` guardam o preset lembrado (campo ausente = nenhum). Recebe o `GerentePresets` e o `GerentePlaylists` no construtor.
- `app-controle/src/BgsControle.Nucleo/Player/GerentePlaylists.cs` -- hoje 3 listas por posição (`Playlist(int n)`, `AdicionarAsync(int n, …)`, formato 1). Passa a ser por id de memória (string), formato 2 com as pendentes, a migração e a adoção; apagar a playlist de uma memória.
- `app-controle/src/BgsControle.Nucleo/Player/Player.cs:15-70,210-260` -- segue `GerenteMemorias.EmUso` (int). Passa a seguir `GerenteSetups.EmUso` (memória do celular, id) e o nome da memória do celular; `MsgSemMemoria` muda.
- `app-controle/src/BgsControle.Telas/PlayerViewModel.cs:110-160,279-345` -- usa `GerenteMemorias` e `int` para a memória; passa ao id e ao `GerenteSetups`.
- `app-controle/src/BgsControle.Telas/PresetsViewModel.cs` -- sai "Guardar o som atual" (o preset novo nasce no Salvar); texto vazio "Mexa nas 15 bandas e toque em Salvar para criar um preset."; Apagar travado com o motivo (lembrado, herdado); aplicar diz "Preset X aplicado na MEMORIA." com memória do celular em uso.
- `app-controle/src/BgsControle.Telas/SalvarViewModel.cs` e `MemoriasViewModel.cs` -- a folha ganha a linha das bandas, a escolha Atualizar/Criar e o nome do preset; o cartão de memória mostra o preset lembrado ("Preset: Cinema").
- `app-controle/src/BgsControle.Nucleo/Porta/PortaComandos.cs:455-475,543-560,600-625,740-760,950-1000` -- caminhos, `Operacao`/`Folha`, `CarregarMemoria` como modelo, `EscreverEstado` e `/ajuda`. A `Operacao` aceita `Quadro` null (sem conferência prévia do primeiro quadro): conferir a chave "Aplicar memória do celular" antes da folha.
- `app-controle/src/BgsControle.App/MauiProgram.cs` -- a ordem de criação: `GerentePresets` e `GerentePlaylists` antes do `GerenteSetups`; o `Player` com o `GerenteSetups`.
- Testes que mudam: `tests/BgsControle.Nucleo.Testes/PresetsTestes.cs` (usa `ConhecimentoHerdado.IdFilme`/`PresetFilme` e 84 valores), `PlaylistsTestes.cs`, `SetupsTestes.cs` (linha ~154 usa `PresetFilme` para a ida e volta), `PortaTestes.cs`; `tests/BgsControle.Telas.Testes/PresetsViewModelTestes.cs`, `PlayerViewModelTestes.cs`, `SalvarViewModelTestes.cs`, `MemoriasViewModelTestes.cs`, `ControleParametroViewModelTestes.cs` (`Bancada`).

## Tasks & Acceptance

**Execution:**
- `app-controle/src/BgsControle.Nucleo/Presets/Preset.cs`, `GerentePresets.cs` -- preset de 15 bandas, formato 2 com a leitura do 1, criar das bandas atuais, atualizar, apagar travado se lembrado, evento ao aplicar -- CAP-5.
- `app-controle/src/BgsControle.Nucleo/Herdado/ConhecimentoHerdado.cs` -- preset herdado Cinema; FILME lembra Cinema; textos -- CAP-6.
- `app-controle/src/BgsControle.Nucleo/Setups/Setup.cs`, `GerenteSetups.cs` -- preset lembrado, preset em uso, bandas no aplicar, linha das bandas no Salvar, aviso de playlist ao apagar -- CAP-4, CAP-5.
- `app-controle/src/BgsControle.Nucleo/Player/GerentePlaylists.cs`, `Player.cs` -- playlists por memória, migração, adoção, Player na memória do celular -- CAP-15.
- `app-controle/src/BgsControle.Nucleo/Porta/PortaComandos.cs` -- `POST /memoria/aplicar`, `/estado` e `/ajuda` -- CAP-12.
- `app-controle/src/BgsControle.Telas/PresetsViewModel.cs`, `SalvarViewModel.cs`, `MemoriasViewModel.cs`, `PlayerViewModel.cs`; `app-controle/src/BgsControle.App/MauiProgram.cs` -- telas e registro.
- `app-controle/tests/...` -- a matriz inteira contra o `ProcessadorSimulado` (núcleo e ViewModels), mais os testes existentes ajustados; o cenário da 8c que confere nenhum `*240*`/`*23N*` continua passando.
- `_bmad-output/specs/spec-app-controle-taramps/conhecimento-herdado.md` -- só se algum texto ainda disser "preset herdado FILME" (o documento já fala do Cinema).

**Acceptance Criteria:**
- Given uma memória do celular em uso, when o Denis aplica outro preset, then a releitura mostra só as 15 bandas mudadas, e a calibração e o caráter iguais (CAP-5).
- Given a FILME herdada aplicada, when o app relê, then o espelho mostra a calibração do Casa 2.1, o caráter da FILME e as bandas do Cinema, iguais à leitura de 18/10 nessas três camadas (CAP-4, CAP-6).
- Given o app reaberto, when os arquivos são lidos de novo, then presets, preset lembrado de cada memória, playlists por memória e pendentes voltam iguais.
- Given o cliente IA pareado e a chave ligada, when ele pede `POST /memoria/aplicar` e o Denis confirma, then a memória é aplicada e o `/estado` a mostra em uso (CAP-12).

## Spec Change Log

## Review Triage Log

### 2026-10-23 — Review pass
- verdicts: 36 findings — high 0, medium 5, low 24, false 7, maybe-false 0
- findings:
  - `[false]` `[reject]` (intenção) Player e telas não ligados no app — o XAML e o reprodutor real são da história 11; o núcleo e os ViewModels estão testados.
  - `[false]` `[reject]` (intenção) Migração pelo nome do protótipo, não pelo nome lido do aparelho — regra decidida na spec (o `invoke_dev_with` pedia uma regra); o nome do protótipo é fixo e não depende de estar conectado.
  - `[false]` `[reject]` (intenção) Memória nova lembra o preset em uso mesmo com bandas mexidas — é a frase do UX ("o preset lembrado é o preset em uso"); as bandas mexidas seguem sem salvar, como as outras mudanças.
  - `[false]` `[reject]` (intenção) Sem "Atualizar" com memória do aparelho em uso — a linha 3 da tabela do Salvar no UX manda para preset novo.
  - `[false]` `[reject]` (intenção) `220` fora do preset — decisão da spec (é o preset de EQ do aparelho, não o do app).
  - `[false]` `[reject]` (intenção) Player para ao trocar de memória — decisão do Denis de 22/10, mantida na spec.
  - `[low]` `[reject]` (intenção) Nenhum teste reabre tudo junto — cada arquivo tem a sua ida e volta.
  - `[low]` `[reject]` (intenção) Teste do link pelo caminho do preset removido — o preset não escreve mais o `081`.
  - `[low]` `[patch]` (lacuna) Adoção pelo Salvar sem teste — teste com o playlists.json antigo e a MUSICA ORIG criada pelo Salvar.
  - `[low]` `[patch]` (lacuna) Escolher sem teste das bandas e do preset que sumiu — dois testes.
  - `[medium]` `[patch]` (lacuna) `setups.json` da 8c sem o campo `preset` faz a FILME esquecer o Cinema — sem o campo, a FILME herdada lembra o Cinema; teste com o arquivo no formato da 8c.
  - `[low]` `[reject]` (lacuna) Salvar não é tudo-ou-nada se o `setups.json` falhar depois do preset — só com falha de gravação no armazenamento privado; a correção pede compensação.
  - `[medium]` `[patch]` (cego) FILME esquece o Cinema depois da 8c — o mesmo patch.
  - `[low]` `[patch]` (cego) `/memoria/aplicar` mostrava a folha para memória de outro Setup ou sem Setup — confere antes da folha e responde 400; mensagem corrigida; testes.
  - `[medium]` `[patch]` (cego) Recusas de `/memoria/aplicar` fora do histórico (trava 4) — `TipoComando.AplicarMemoriaCelular` no fim do enum, comando passado nas duas operações, rótulo "Aplicar a memória X"; testes.
  - `[low]` `[reject]` (cego) Salvar parcial com falha do `setups.json` — o mesmo da lacuna.
  - `[low]` `[reject]` (cego) Atualizar com a folha velha aberta pode cair noutro preset — precisa de outra aplicação no meio da folha aberta; a correção pede parâmetro novo.
  - `[low]` `[patch]` (cego) Preset próprio chamado "Cinema" ao lado do herdado — descartado na leitura; teste.
  - `[low]` `[reject]` (cego) Falhas de gravação ignoradas (apagar playlist, preset lembrado) — raras; ficam no log.
  - `[low]` `[patch]` (cego) Bandas fora da conferência sem teste — teste com só as bandas diferentes → Confere.
  - `[low]` `[patch]` (cego) Nota fixa da FILME dizia "Lembra o preset Cinema." — frase tirada.
  - `[false]` `[reject]` (cego) Memória nova lembra preset que não bate — o mesmo da intenção (frase do UX).
  - `[low]` `[patch]` (cego) Erro do nome do preset igual ao do nome da memória — prefixo "Nome do preset:"; teste com "a#b".
  - `[low]` `[reject]` (cego) Testes de `/estado` sem Setup, do id de preset pendurado e do plural de `MsgIncompleta` — não são defeitos.
  - `[low]` `[reject]` (borda) Preset órfão se o `setups.json` falhar — o mesmo da lacuna.
  - `[low]` `[reject]` (borda) Apagar o preset novo entre criar e ligar — corrida sem caminho de tela.
  - `[low]` `[reject]` (borda) AoAplicar durante o Salvar com retrato velho — a IA não aplica preset, e a tela não aplica com a folha aberta.
  - `[low]` `[reject]` (borda) Preset lembrado gravado só em memória se a gravação falhar — o mesmo das falhas de gravação.
  - `[low]` `[reject]` (borda) Playlist de memória apagada fica se a gravação falhar — idem.
  - `[low]` `[patch]` (borda) Preset próprio com nome do herdado — o mesmo patch.
  - `[low]` `[reject]` (borda) Preset de formato 2 com menos de 15 bandas — só com arquivo editado à mão.
  - `[low]` `[reject]` (borda) Bandas mudam com a folha aberta — a recusa volta com a mensagem; raro.
  - `[medium]` `[patch]` (borda) FILME sem o Cinema depois da 8c — o mesmo patch.
  - `[low]` `[reject]` (borda) Playlist órfã após falha de gravação — idem.
  - `[low]` `[reject]` (borda) Preset lembrado volta antigo após falha de gravação — idem.
  - `[medium]` `[defer]` (verificação, relato da implementação) Desconectado, o Player fica sem playlist e para; o EXPERIENCE.md diz que ele continua tocando (é Bluetooth de áudio, não BLE) — a regra veio da própria spec (dentro do contrato); o Player ainda não está ligado no app, e a tela dele é da história 11; vai para `deferred` e para a decisão do Denis.

## Design Notes

- **Por que a referência sem preset é o EQ plano:** a memória criada sem preset em uso não tem com que comparar as bandas; o plano é o "nenhum preset" natural, e a linha só aparece se o Denis mexeu nas bandas.
- **Por que a migração vai por nome:** a posição antiga é a memória do aparelho, que este app não salva mais; o que liga a playlist ao som é o nome (o teste 1 refaz a MUSICA ORIG como memória do celular, e a playlist antiga volta sozinha).
- **O Player para ao trocar de memória:** o EXPERIENCE.md tem um `[ASSUMPTION]` de continuar a faixa, mas a decisão do Denis de 22/10 (parar) está no código e não foi revogada; fica a decisão.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.
- `git grep -n "PresetFilme\|IdFilme" -- app-controle/src app-controle/tests` -- expected: nenhuma linha (o preset herdado é o Cinema; a leitura completa interna tem outro nome).

## Auto Run Result

Status: done

**O que entrou.**
- **Preset:** passa a ser só as 15 bandas (`020`), no formato 2 do `presets.json`; o formato 1 é convertido quando tem as 15 bandas. O herdado agora é o "Cinema". O "Guardar o som atual" saiu: o preset novo nasce no Salvar. Apagar um preset lembrado é recusado.
- **Memória do celular:** lembra um preset (a FILME lembra o Cinema, também num `setups.json` da 8c). Aplicar a memória, ou escolher o Setup, traz as bandas do preset lembrado. Aplicar um preset com uma memória do celular em uso faz a memória lembrar esse preset.
- **Salvar:** ganha a linha das 15 bandas, com Atualizar ou Criar; o herdado só cria; sem preset, a referência é o EQ plano. Uma memória nova lembra o preset de bandas iguais, ou cria um preset novo.
- **Playlists:** passam a ser por id de memória do celular. O `playlists.json` antigo vira pendentes por nome (MUSICA HIGH, MUSICA ORIG, FILME), adotadas pela memória de mesmo nome ao carregar, ao criar, ao renomear e ao salvar.
- **Player:** segue a memória do celular em uso.
- **Porta da IA:** ganha `POST /memoria/aplicar` (id ou nome no Setup em uso, chave "Aplicar memória do celular", sempre com a folha), com as recusas no histórico. O `/estado` lista as memórias do Setup e o preset em uso.

**Arquivos:**
- Núcleo: `GerentePresets.cs` e `Preset.cs`; `ConhecimentoHerdado.cs`; `GerenteSetups.cs`, `Setup.cs` e `Camadas.cs`; `GerentePlaylists.cs` e `Player.cs`; `PortaComandos.cs` e `ComandoNaPorta.cs`.
- Telas: `PresetsViewModel.cs`, `SalvarViewModel.cs`, `MemoriasViewModel.cs`, `PlayerViewModel.cs`, `HistoricoViewModel.cs` e `RepositorioTextoArquivos.cs`.
- App: `MauiProgram.cs`.
- Testes: `SetupsTestes`, `PresetsTestes`, `PlaylistsTestes`, `PortaTestes`, `UsoTestes`, `PresetsViewModelTestes`, `PlayerViewModelTestes`, `SalvarViewModelTestes`, `MemoriasViewModelTestes` e `HistoricoIaViewModelTestes`.

**Revisão:** 36 achados.
- **Corrigidos:** 2 entradas medium (a FILME sem o Cinema depois da 8c, achada por 3 revisores; as recusas fora do histórico) e 9 low (a folha da IA para memória de outro Setup, o nome "Cinema" repetido, a nota da FILME, o prefixo do nome do preset e os testes de adoção pelo Salvar, de escolher com as bandas e de só as bandas diferentes).
- **Adiado (1):** o Player desconectado.
- **Rejeitados:** os demais, com o motivo no triage log.

**Revisão de acompanhamento recomendada:** sim (2 entradas medium corrigidas). O risco que ficou sem segunda leitura é a gravação em dois arquivos no Salvar (`presets.json` e depois `setups.json`): uma falha no meio deixa o preset criado ou atualizado sem a memória.

**Verificação:**
- `dotnet test` do Nucleo.Testes: 916 passam.
- `dotnet test` do Telas.Testes: 329 passam.
- `dotnet build` do App para `net10.0-android`: 0 erros e 0 avisos.
- `git grep` de `PresetFilme` e `IdFilme`: nenhuma linha.

**Riscos que ficam:**
- O Player desconectado (adiado, para o Denis decidir).
- O Player e o reprodutor real ainda não estão ligados no app (história 11).
- Nada foi testado no celular nem no processador (história 12).
