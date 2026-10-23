---
title: 'História 8c: Setup e memória no celular'
type: 'feature'
created: '2026-10-23'
status: 'done'
baseline_revision: '60906cb5e4d8c25a9707531beb060253e6bc27db'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/SPEC.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/failure-modes.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/chaves-de-acesso.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/conhecimento-herdado.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
warnings:
  - oversized
deferred:
  - summary: >-
      Com o link 1–2 ligado e equilíbrio L/R diferente de 0, o link copia o valor final da saída 1 para a 2 e perde o equilíbrio na saída 2.
    evidence: |-
      LinkCanais repete na outra saída o valor que o aparelho confirmou na mestre, sem saber do equilíbrio do Setup. O Casa 2.1 vem com o link desligado, por isso nenhum teste nem o uso de hoje passa por isso. Registrado também em deferred-work.md para a história 10.
    location: >-
      app-controle/src/BgsControle.Nucleo/Escrita/LinkCanais.cs
    severity: low
---

<intent-contract>

## Intent

**Problem:** O app ainda trata as 3 memórias do processador como as memórias do usuário (salvar `240`, renomear `23N`), e não conhece Setup nem as três camadas do som. A proposta de 23/10 e o UX da 3b decidiram: o app só carrega as memórias do aparelho; o usuário tem Setups (calibração) e memórias no celular (caráter), e ao conectar o app confere o Setup antes de tocar (CAP-4, CAP-6, CAP-11, CAP-16; failure-modes regra 5).

**Approach:** Um núcleo novo (`BgsControle.Nucleo.Setups`) com Setups, memórias no celular, a soma do equilíbrio L/R, a conferência na primeira leitura de cada conexão (com o mudo da dúvida guardado no aparelho e no celular) e o Salvar por camadas; o `GerenteMemorias` fica só com o carregar; a FILME herdada é dividida em Setup "Casa 2.1", memória FILME e as 15 bandas do "Cinema"; três ViewModels (Setup, Memórias, Salvar). Tudo testado contra o `ProcessadorSimulado`.

## Boundaries & Constraints

**Always:**
- Camadas por parâmetro do Marco 1 (decisão desta história, seguindo a tabela "Três camadas do som" do EXPERIENCE.md): **calibração** = `010` rota, `060` fase, `040`/`041`/`042`/`043` cortes, `050` atraso, `030`/`031`/`032` EQ de entrada, mais o link 1–2 e 3–4 (só do app) e o equilíbrio L/R; **caráter** = `080` MASTER, `081` ganho, `070`–`073` limiter, `090`–`092` EQ de saída; **bandas** = `020` (15 bandas) e `220` (preset de EQ do aparelho); **fora das camadas** = `100` mute e os extras.
- Equilíbrio L/R: só nas saídas 1 (JBL L) e 2 (JBL R), um valor de ganho e um de limiter por saída, em dB com passo 0,1, de −12,0 a +12,0. Final no aparelho = memória + equilíbrio, preso à faixa do MAPA (ganho −45,0 a +15,0; limiter −48,0 a 0,0). Limiter em OFF (`0.5`) fica OFF, sem soma. Parte da memória = final lido − equilíbrio.
- Aplicar memória do celular, escolher Setup e o "Manter a calibração" do Salvar usam a `CopiaAntes` (regra 3), escrevem só o que difere pelo `Escritor`, relêem com `LerNovaAsync` e conferem; uma queda deixa "aplicado pela metade" com Terminar e Voltar à cópia. O alvo de aplicar memória é a calibração do Setup em uso + o caráter da memória somado ao equilíbrio; as bandas, o mute e os extras não entram.
- Chaves: "Aplicar memória do celular" (nova, ligada, a IA pede confirmação, risco "Troca todo o som de uma vez.") passa antes de aplicar memória e de escolher Setup; "Carregar memória do aparelho" é o novo nome da chave do `241`; a chave "Salvar memória" sai. Continuam 11 chaves. O id guardado `carregar-memoria` não muda; um id desconhecido no JSON é ignorado.
- Conferência: só na primeira leitura completa de cada conexão nova, depois da regra 2 do gerador. A referência é a calibração salva do último Setup usado (no primeiro uso, "Casa 2.1"). O equilíbrio não é conferido na calibração: ele entra na comparação do caráter (final lido contra memória ativa + equilíbrio).
- O mudo da dúvida: antes de mutar, o app guarda no celular o mute anterior dos 4 canais e qual Setup era a referência; muta os 4 (`100` = 1, com `daCopia`, para passar mesmo com operação pendente); "desmutar" é escrever de volta o mute anterior e apagar a marca. Enquanto há dúvida (ou troca), a porta da IA recusa todo POST com 409 "Setup em dúvida; escolha na tela." (os GET seguem).
- Criar, renomear e apagar Setup e memória do celular não mandam nada pelo BLE. Nomes: as regras do preset (1 a 24 caracteres, letras, números, espaço, `_` e `-`), sem repetir (sem diferença de caixa) entre os Setups, e entre as memórias do mesmo Setup.
- Textos da tela em português, curtos, como no EXPERIENCE.md.

**Never:**
- Nenhum `*240*` ou `*23N*` sai do app: a trava da `Sessao` recusa os dois com `MotivoNaoLiberado`, e o código que os montava some.
- Não mexer no `GerentePresets`, no preset herdado FILME, nas playlists, no `Player` nem em "aplicar memória" pela porta da IA: isso é da 8d. Os 3 botões do Início, a Mesa, o Ajuste e o equilíbrio na tela são da história 10 (aqui fica só o núcleo deles).
- Não usar `Rendimento`, limites físicos nem componentes do Setup (spec própria).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Primeira conexão, aparelho na FILME | Sem `setups.json`; o simulador com a leitura de 18/10 | Estado "Setup confere"; Setup em uso Casa 2.1; em uso a memória FILME; nada escrito | — |
| Só o caráter difere | Calibração igual à do Casa 2.1; ganho da saída 1 diferente | "Som diferente da FILME", em uso "Som do aparelho", sem mutar; Reaplicar escreve só o que difere e volta a "Setup confere" | — |
| Calibração difere | Atraso da saída 3 = 0 no aparelho, 80 no Casa 2.1 | Muta os 4 canais, marca guardada, estado "Setup em dúvida" com a linha da diferença; POST da IA → 409 | Mutar recusado (chave, só leitura): dúvida aberta mesmo assim, com o motivo |
| Dúvida interrompida | Marca guardada e os 4 canais lidos mutados | Volta à dúvida sem mutar de novo, com o mute anterior da marca | Marca guardada mas o aparelho desmutado (app da Taramps): apaga a marca e confere normal |
| Escolher o Setup | Na dúvida, escolhe Casa 2.1 | Cópia antes, calibração + memória ativa (com equilíbrio), releitura, link do Setup, desmuta para o mute anterior, marca apagada, "Setup confere" | Queda no meio: pela metade, continua mutado; Terminar refaz e desmuta |
| Criar Setup novo | Na dúvida ou na troca, nome "Carro" | Setup com a calibração lida, equilíbrio 0, link atual; em uso, sem memórias; desmuta | Nome inválido ou repetido: recusa, nada muda |
| Seguir sem Setup | Na dúvida | Desmuta sem aplicar nada; sem Setup em uso; aplicar e criar memória do celular recusados com "Escolha um Setup para usar as memórias do celular." | — |
| Trocar de Setup e desistir | Conectado, toca no Setup | Muta, estado "Trocando de Setup"; desistir desmuta e volta ao estado de antes | — |
| Criar memória do som atual | Casa 2.1 em uso, ganho da saída 2 lido +7, equilíbrio R −3 | Memória nova guarda ganho +10 na saída 2; vira a em uso e a ativa, entra nos botões; aviso "Memória MUSICA ORIG criada no Casa 2.1." | Em uso memória do aparelho ou "Som do aparelho": não cria; pede a folha do Salvar |
| Aplicar memória do celular | Memória com limiter OFF na saída 1 | Escreve `0.5` (sem soma); releitura confere; vira a em uso | Chave desligada: recusa antes do BLE |
| Botões fixos | 3 botões [A, B, C], aplica D (C usada há mais tempo) | Botões [A, B, D] | Apagar B: a vaga fica com a usada por último fora dos botões, ou vazia |
| Apagar | Memória em uso; Setup em uso; Casa 2.1 herdado | Recusados com o motivo | — |
| Carregar memória do aparelho | Carrega a 2 | Em uso "Memória 2 do aparelho", Setup em uso não muda | Reconexão depois: "Som do aparelho" |
| Salvar, memória do celular em uso | Mudou o atraso e o ganho | Folha: "Calibração → Setup Casa 2.1" e "Caráter → memória X" com antes e depois; salvar guarda cada parte na sua camada | Nada mudou: travado "Nada para salvar."; sem Setup: "Escolha um Setup para salvar." |
| Salvar, memória do aparelho em uso | Calibração lida difere do Setup | Pede nome; pergunta Manter (reaplica a calibração do Setup pelo BLE, com cópia antes) ou Atualizar o Setup ("Vale para todas as memórias do Casa 2.1.") | Confirmar travado até escolher e dar nome |
| 240 ou 23N | Qualquer origem | Recusado pela trava com `MotivoNaoLiberado`; nada sai | — |

</intent-contract>

## Code Map

- `app-controle/src/BgsControle.Nucleo/Memorias/GerenteMemorias.cs` -- hoje salva (240+23N), carrega (241) e renomeia. Fica só o carregar, a leitura dos nomes, `EmUso` (o `Player` ainda usa até a 8d), `Ocupado` e o registro `TipoCarregar` na `CopiaAntes`. Sai `SalvarAsync`, `RenomearAsync`, `TipoSalvar`, `ConferirSalvo` e as mensagens só do salvar. Ganha o evento `Carregou(int n)` depois de um carregar que terminou bem.
- `app-controle/src/BgsControle.Nucleo/Catalogo/Catalogo.cs:225-311` -- seção das memórias: sai `SalvarMemoria(n)`, `NomearMemoria(n, nome)` e o reconhecimento de 240/23N em `ComandoDeMemoria` (fica só o `Carregar`). `CodigoNomeMemoria`, `NormalizarNomeMemoria` e `CodigoSalvarMemoria` ficam (leitura dos nomes, nome do produto e a recusa).
- `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs:76-79,453-499` -- a trava: 240 e 23N (qualquer forma) recusados com `MotivoNaoLiberado`; o 241 exato com `Chave.CarregarMemoria`; um 241 fora do formato continua `MotivoMemoriaForaDoFormato`. Atualizar o comentário das linhas 88-92.
- `app-controle/src/BgsControle.Nucleo/Uso/ComandoNaPorta.cs:55-75` -- sem o mapeamento de salvar e nomear (os valores do enum `TipoComando` ficam, por causa do `historico.json` antigo).
- `app-controle/src/BgsControle.Nucleo/Seguranca/Chaves.cs:7-20,90-111` -- enum e tabela: sai `SalvarMemoria`; entra `AplicarMemoriaCelular` (id `aplicar-memoria-celular`) logo depois de `CarregarMemoria`, que passa a se chamar "Carregar memória do aparelho". Conferir que o carregamento do JSON ignora id desconhecido.
- `app-controle/src/BgsControle.Nucleo/Escrita/CopiaAntes.cs` -- reaproveitar: `IniciarAsync`, `ConcluirAsync`, `Interromper`, `MarcarPelaMetade`, `Registrar(tipo, aoReconectar, refazer)`, `Diferencas` (interno, recebe uma `Copia`) e `Iguais`. O modelo de aplicar é o `GerentePresets.AplicarInternoAsync` (`Presets/GerentePresets.cs:301-394`).
- `app-controle/src/BgsControle.Nucleo/Escrita/Escritor.cs:121-170` -- `EscreverAsync` público (tela) e o interno com `espelhar`/`daCopia` (usar `daCopia: true` no aplicar, no mutar e no desmutar).
- `app-controle/src/BgsControle.Nucleo/Escrita/LinkCanais.cs` -- o link é só do app (`link-canais.json`). Precisa de um método interno para pôr o estado do link de um Setup sem copiar a saída mestre (o aplicar já escreveu as duas).
- `app-controle/src/BgsControle.Nucleo/Leitura/LeituraAutomatica.cs` -- eventos `Terminou(Retrato)`; `AntesDoFim` é a regra 2 do gerador, que roda antes. `LerNovaAsync` é a releitura depois de aplicar.
- `app-controle/src/BgsControle.Nucleo/Escrita/EstadoAparelho.cs` -- o espelho: `Pronto`, `Lendo`, `Copiar()`, `Valor(...)`.
- `app-controle/src/BgsControle.Nucleo/Herdado/ConhecimentoHerdado.cs` -- já lê a leitura de 18/10 embutida (`LerFilme`, 84 valores). Daqui saem o Setup "Casa 2.1", a memória FILME e as 15 bandas do Cinema; o preset FILME atual e as notas das 3 memórias ficam como estão (a 8d troca o preset).
- `app-controle/src/BgsControle.Nucleo/Porta/PortaComandos.cs:455-475,600-635,740-760` -- sai `/memoria/salvar`; `/memoria/carregar` fica, com a chave renomeada; dúvida ou troca de Setup recusa todo POST; `/estado` ganha um bloco `setup` (nome em uso ou null, `duvida`). O construtor recebe o `GerenteSetups`.
- `app-controle/src/BgsControle.Telas/MemoriasViewModel.cs` -- hoje são as 3 memórias do aparelho com Salvar aqui e Renomear. Vira a tela Memórias do EXPERIENCE.md (cartões das memórias do celular do Setup em uso e a seção "Do aparelho"). Manter o bloco "aplicado pela metade" (`Terminar`, `VoltarACopia`).
- `app-controle/src/BgsControle.Telas/PresetsViewModel.cs` e `IaPelaRedeViewModel.cs` -- modelos de folha com nome e de mensagens; `TelaBase`, `Comando`, `IDespachante` em `Abstracoes.cs`.
- `app-controle/src/BgsControle.Telas/RepositorioTextoArquivos.cs` -- os nomes dos arquivos privados; entram `setups.json` e `setup-mudo.json`.
- `app-controle/src/BgsControle.App/MauiProgram.cs:60-170` -- registrar o `GerenteSetups` (criado cedo, junto do `GerenteMemorias`, para ver a primeira leitura e registrar os tipos na `CopiaAntes`) e os ViewModels novos.
- `app-controle/tests/BgsControle.Nucleo.Testes/MemoriasTestes.cs`, `PortaTestes.cs`, `UsoTestes.cs`, `tests/BgsControle.Telas.Testes/MemoriasViewModelTestes.cs`, `HistoricoIaViewModelTestes.cs`, `ChavesViewModelTestes.cs` -- testes do salvar, do renomear e das 11 chaves que mudam. A montagem de teste está em `MemoriasTestes.cs:60-110` e em `Telas.Testes/ControleParametroViewModelTestes.cs:20` (`Bancada`).
- `app-controle/tests/BgsControle.Simulador/ProcessadorSimulado.cs` e `Suposicoes.cs` -- o simulador guarda `Escritas`; o 240 dele pode ficar (só testes). Tirar da lista de suposições as que eram só do salvar (`ACK-240-SALVA`), se nenhum código as usa.

## Tasks & Acceptance

**Execution:**
- `app-controle/src/BgsControle.Nucleo/Setups/Camadas.cs` -- a camada de cada parâmetro do Marco 1 e a soma/subtração do equilíbrio (com faixa e OFF) -- uma regra só para a conferência, o aplicar e o salvar.
- `app-controle/src/BgsControle.Nucleo/Setups/Setup.cs` -- records `Setup` (id, nome, quando, calibração como `ValorCopiado`, equilíbrio, link 1–2 e 3–4, herdado, nota), `MemoriaCelular` (id, id do Setup, nome, quando, caráter sem o equilíbrio, herdado, nota), `SomEmUso` (memória do celular, memória do aparelho N, som do aparelho, nenhuma), estado da conferência (nenhuma, confere, som diferente, dúvida, trocando), `DiferencaCalibracao` e os resultados -- os dados do núcleo.
- `app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs` -- o gerente: carrega e grava `setups.json` (formato 1: Setups, memórias, último Setup, e por Setup os usados por último e os 3 botões fixos) e `setup-mudo.json` (a marca do mudo); semeia o herdado quando não há arquivo; conferência na primeira leitura completa de cada conexão; escolher, criar Setup do aparelho, seguir sem Setup, trocar e desistir; renomear e apagar Setup; criar memória do som atual, aplicar, reaplicar a ativa, renomear e apagar memória; ajustar o equilíbrio (escreve o final e guarda o equilíbrio pendente até o Salvar); preparar e executar o Salvar por camadas (os casos da tabela "Salvar por camadas" do EXPERIENCE.md, menos a linha das 15 bandas); tipos `aplicar-memoria-celular`, `aplicar-setup` e `manter-calibracao` registrados na `CopiaAntes`; ouve `GerenteMemorias.Carregou`; uma operação por vez; evento `Mudou` -- CAP-4, CAP-16, regra 5.
- `app-controle/src/BgsControle.Nucleo/Herdado/ConhecimentoHerdado.cs` -- Setup "Casa 2.1" (ids fixos, calibração da leitura de 18/10, equilíbrio R −3 no ganho e no limiter, link desligado), memória FILME (caráter da leitura menos o equilíbrio: R +10 e −15), `BandasCinema` (as 15 bandas da leitura, para a 8d), notas do selo e uma seção de texto "A FILME em três camadas" -- CAP-6.
- `GerenteMemorias.cs`, `Catalogo.cs`, `Sessao.cs`, `ComandoNaPorta.cs`, `Chaves.cs`, `PortaComandos.cs`, `LinkCanais.cs` -- as mudanças do Code Map.
- `app-controle/src/BgsControle.Telas/SetupViewModel.cs` -- a tela Setup em dúvida / Trocar de Setup e o seletor ("Setup Casa 2.1 ›", "Sem Setup ›"): título, frase, faixa "Som mutado até você escolher.", as linhas do que não bate ("Atraso da saída 3: 80 cm no Casa 2.1, 0 cm no aparelho"), lista de Setups, Escolher, Criar Setup novo (nome e erro), Seguir sem Setup, Desistir só na troca.
- `app-controle/src/BgsControle.Telas/MemoriasViewModel.cs` -- cartões das memórias do Setup em uso (nome, EM USO, selo HERDADO, Aplicar, Renomear, Apagar com folha "Apagar a X?" + "A memória e a playlist dela somem." e travado na em uso), Criar memória do som atual (abre a folha do Salvar quando precisa), travas sem Setup; seção "Do aparelho" com as 3 pelos nomes lidos, só Carregar, a nota herdada pela posição e o lembrete "Salvar e renomear estas memórias só no app da Taramps."; aplicado pela metade.
- `app-controle/src/BgsControle.Telas/SalvarViewModel.cs` -- a folha do Salvar: "Salvar o que mudou?", uma linha por camada com antes → depois, a pergunta Manter / Atualizar quando cabe, o nome da memória nova, Confirmar travado até escolher, e o aviso final ("Salvo: calibração no Casa 2.1, caráter na MUSICA ORIG.").
- `app-controle/src/BgsControle.Telas/RepositorioTextoArquivos.cs`, `MauiProgram.cs` -- arquivos e registro.
- `app-controle/tests/BgsControle.Nucleo.Testes/SetupsTestes.cs` -- a matriz inteira contra o `ProcessadorSimulado`, a ida e volta da FILME herdada (Casa 2.1 + FILME + Cinema = os valores da leitura de 18/10) e um cenário que percorre conectar, dúvida, escolher, criar, aplicar, salvar e carregar e confere que nenhuma das `Escritas` do simulador começa com `*240*` ou `*23`.
- Testes que mudam (`MemoriasTestes`, `PortaTestes`, `UsoTestes`, `ChavesViewModelTestes`, `MemoriasViewModelTestes`, os que montam `PortaComandos`) e novos `SetupViewModelTestes` e `SalvarViewModelTestes` em `tests/BgsControle.Telas.Testes`.
- `_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md` -- trocar o `[NOTE FOR UX]` "Falta decidir, na 8c, como a conferência o separa" pela decisão: a calibração confere sem o equilíbrio; o equilíbrio entra na comparação do caráter.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- registrar: com o link 1–2 ligado e equilíbrio diferente de 0, o link copia o valor final da mestre e perde o equilíbrio na outra saída (conferir na história 10).

**Acceptance Criteria:**
- Given a FILME herdada, when o Casa 2.1 e a memória FILME são compostos com as bandas do Cinema, then o resultado é igual, parâmetro por parâmetro, à leitura de 18/10 nas camadas calibração, caráter e bandas.
- Given uma memória do celular aplicada, when o app relê, then o espelho mostra a calibração do Setup e o caráter da memória somado ao equilíbrio, e o mute e as 15 bandas não mudaram.
- Given qualquer fluxo do núcleo e dos ViewModels desta história, when ele roda contra o simulador, then nenhum quadro `*240*` ou `*23N*` chega ao simulador.
- Given o app reaberto, when os arquivos são lidos de novo, then Setups, memórias, último Setup, botões fixos e a marca do mudo voltam iguais; um `setups.json` estragado volta ao herdado sem erro.
- Given a tela de chaves, when ela lista as chaves, then são 11, com "Carregar memória do aparelho" e "Aplicar memória do celular", e sem "Salvar memória".

## Spec Change Log

## Review Triage Log

### 2026-10-23 — Review pass
- verdicts: 44 findings — high 0, medium 12, low 24, false 6, maybe-false 2
- findings:
  - `[false]` `[reject]` (intenção) Superfície: sem XAML, só núcleo e ViewModels — as telas são da história 10 ("Telas, parte 1 … Setups e memórias"); o padrão das histórias anteriores é núcleo + ViewModel testado.
  - `[false]` `[reject]` (intenção) Conferência contra a última calibração salva, não contra o último 241 — é a redação da regra 5 de failure-modes.md e do EXPERIENCE.md.
  - `[false]` `[reject]` (intenção) "Reaplicar se perdeu o estado" sem detecção própria — o EXPERIENCE.md define isso como o caminho da dúvida (calibração não bate → escolher o Setup reaplica a memória ativa).
  - `[false]` `[reject]` (intenção) IA bloqueada também antes do fim da conferência — segue "nada toca antes de conferir"; agora coberto por teste.
  - `[low]` `[defer]` (intenção) Link 1–2 ligado com equilíbrio ≠ 0 perde o equilíbrio na saída 2 — já registrado em deferred-work.md para a história 10; vai para `deferred`.
  - `[false]` `[reject]` (intenção) Salvar sem a linha das 15 bandas — é da 8d pelo próprio `invoke_dev_with` dela.
  - `[low]` `[patch]` (intenção) A asserção "sem 240" do PortaTestes não cobria `*23` — asserção estendida.
  - `[medium]` `[patch]` (cego) `_setupEmUso` sobrevivia à queda: aplicar/salvar/equilíbrio podiam rodar antes da conferência nova — a queda agora limpa o Setup em uso; teste novo.
  - `[medium]` `[patch]` (cego) `AjustarEquilibrioAsync` fora de "uma operação por vez" — pega `_operacao` e recusa sem sessão.
  - `[low]` `[reject]` (cego) Equilíbrio sobre valor preso no teto perde a parte da memória — só no limite da faixa (ganho +15), raro; a correção pede outro ramo.
  - `[low]` `[patch]` (cego) Escolher com ajustes que ficaram diferentes desmuta; frase sem singular — o desmutar segue a escolha do Denis (o aparelho prende o valor); a frase ganhou o singular.
  - `[medium]` `[patch]` (cego) Exceção na conferência deixava `_conferiu` verdadeiro e a IA livre — a conferência volta a valer na próxima leitura completa; a primeira leitura incompleta bloquear a IA é o lado seguro (rejeitado como defeito).
  - `[medium]` `[patch]` (cego) 409 da dúvida bloqueava `POST /gerador/parar` — o parar ficou isento; ajuda atualizada; teste novo. O 400 antes do 409 com corpo ruim: low, rejeitado.
  - `[low]` `[reject]` (cego) `setups.json` parcial enfraquece a conferência — só com arquivo editado à mão; o app sempre grava completo.
  - `[medium]` `[patch]` (cego) `SalvarViewModel` não atualizava a trava depois de mexer — ouve o espelho e o link; teste novo.
  - `[medium]` `[patch]` (cego) Manter: memória gravada antes do reaplicar; falha deixava a folha aberta e o repetir errado — devolve sucesso com a frase do que não voltou; teste novo.
  - `[low]` `[reject]` (cego) A FILME herdada pode ser apagada; "Casa 2.1" renomeado não volta a ter o ponto — o UX só protege o Setup herdado e o preset herdado; nenhum uso diário.
  - `[low]` `[patch]` (cego) Resultado do `DefinirEstadoAsync` do link ignorado — o motivo entra na frase.
  - `[low]` `[patch]` (cego) Frase do mudo repetida — texto corrigido; teste novo.
  - `[low]` `[reject]` (cego) `/estado` não diz a memória do celular em uso — a porta da IA para memórias do celular é da 8d.
  - `[low]` `[patch]` (cego) SUPOSICOES e comentários ainda falavam do `*23N*` enviado — textos atualizados.
  - `[low]` `[reject]` (cego) Renomear e apagar Setup sem ViewModel; seletor marca a referência na dúvida — a tela Configurações › Setups é da história 10/11; o núcleo está testado.
  - `[medium]` `[patch]` (lacuna) Link do Setup nunca testado com valor diferente — testes de escolher com link ligado e criar Setup com link, com ida e volta no JSON.
  - `[medium]` `[patch]` (lacuna) Mudo da dúvida com operação pendente sem teste — teste com o 241 caído no meio e a calibração diferente.
  - `[low]` `[patch]` (lacuna) IA bloqueada antes do fim da conferência sem teste — teste no PortaTestes.
  - `[low]` `[patch]` (lacuna) Salvar no Setup sem memórias nunca rodado — teste com o Carro.
  - `[low]` `[patch]` (lacuna) Equilíbrio do limiter, OFF e "No limite" sem teste — três testes.
  - `[low]` `[patch]` (lacuna) Frase antiga depois do Terminar de um carregar — a frase vem do gerente dono da operação; teste novo.
  - `[medium]` `[patch]` (borda) Conferência com `Pronto` falso marcava conferido — o mesmo patch da exceção.
  - `[medium]` `[patch]` (borda) `_setupEmUso` não limpo na queda — o mesmo patch do cego.
  - `[low]` `[reject]` (borda) Queda e reconexão no meio de uma conferência antiga — janela de milissegundos; pede contador de geração.
  - `[low]` `[reject]` (borda) Valor no teto no equilíbrio — o mesmo do cego.
  - `[low]` `[patch]` (borda) `Manter` velho sem a pergunta — vira `Nenhuma`; teste novo.
  - `[false]` `[reject]` (borda) Manter separa a memória com o equilíbrio pendente — certo: o equilíbrio é calibração, e Manter descarta a calibração mexida; a parte da memória é o que se ouvia menos o equilíbrio de então.
  - `[low]` `[patch]` (borda) Manter sem a chave "Aplicar memória do celular" — confere a chave antes de guardar; teste novo.
  - `[low]` `[patch]` (borda) Motivo do link ignorado — o mesmo do cego.
  - `[low]` `[patch]` (borda) "1 ajustes" — o mesmo do cego.
  - `[maybe-false]` `[reject]` (borda) Mudo parcial dito "não mutado" — precisaria de uma escrita do mute falhar entre canais; se real, seria low.
  - `[medium]` `[patch]` (borda) `/gerador/parar` bloqueado — o mesmo do cego.
  - `[low]` `[reject]` (borda) Apagar a marca do mudo falha e ela volta no reinício — só vira dúvida se os 4 canais estiverem mutados; raro.
  - `[low]` `[reject]` (borda) Apagar o Setup de referência durante a dúvida — a tela da dúvida é modal; não há como chegar a Setups.
  - `[low]` `[reject]` (borda) Calibração parcial no `setups.json` — o mesmo do cego.
  - `[maybe-false]` `[reject]` (borda) Marca antiga de "salvar-memoria" na cópia — só se o app tivesse salvado no aparelho antes desta versão (não foi usado assim); Voltar à cópia resolve; se real, seria low.
  - `[medium]` `[patch]` (borda) `AjustarEquilibrioAsync` sem `_operacao` — o mesmo do cego.

## Design Notes

- **Por que o equilíbrio sai da conferência da calibração:** o aparelho só guarda o ganho e o limiter finais, então não há como lê-lo à parte. Se ele mudou, o caráter final lido deixa de bater com memória ativa + equilíbrio, e o caso cai em "som diferente" (sem mutar), que oferece Reaplicar. Isso responde à nota do UX que deixava a decisão para a 8c.
- **Por que `220` fica com as bandas:** é o preset de EQ do aparelho e mora na tela EQ 15 bandas; escrevê-lo junto do caráter poderia mexer nas bandas.
- **Primeira leitura da conexão:** marcar "conferir" quando a `Sessao` entra em Conectado e consumir a marca no primeiro `Terminou` completo; a releitura de um aplicar e o "Ler de novo" não conferem.
- **Composição** (exemplo da R na FILME): memória 081 saída 2 = +10,0; equilíbrio ganho R = −3,0 → escreve `*081*1*+7.0*`; limiter memória −15,0 + (−3,0) → `-18.0`; limiter OFF fica `0.5`.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.
- `git grep -n "SalvarMemoria(\|NomearMemoria(" -- app-controle/src` -- expected: nenhuma linha.

## Auto Run Result

Status: done

**O que entrou.** O app deixa de salvar (`240`) e de renomear (`23N`) as memórias do processador: a trava da `Sessao` recusa os dois, e o `GerenteMemorias` fica só com o carregar (`241`). Entra o núcleo `Setups`:
- as três camadas por parâmetro;
- a soma do equilíbrio L/R;
- Setups e memórias no celular em `setups.json`, com os 3 botões fixos por Setup;
- a conferência na primeira leitura completa de cada conexão, com o mudo da dúvida guardado no aparelho e em `setup-mudo.json`;
- escolher, criar e seguir sem Setup, trocar e desistir;
- aplicar, reaplicar, criar, renomear e apagar memória;
- o Salvar por camadas, com Manter e Atualizar.

A FILME herdada virou Setup "Casa 2.1" + memória FILME + as 15 bandas do Cinema. As chaves continuam 11: entra "Aplicar memória do celular", e o carregar passa a se chamar "Carregar memória do aparelho". A porta da IA perde `/memoria/salvar`; com a dúvida aberta, a troca de Setup em andamento ou a conferência ainda rodando, ela recusa todo POST, menos `/gerador/parar`.

**Arquivos:**
- `app-controle/src/BgsControle.Nucleo/Setups/Camadas.cs`, `Setup.cs`, `GerenteSetups.cs`: o núcleo novo.
- `GerenteMemorias.cs`, `Catalogo.cs`, `Sessao.cs`, `ComandoNaPorta.cs`, `Chaves.cs`: sem o salvar e o renomear; chave nova; evento `Carregou`.
- `ConhecimentoHerdado.cs`: Casa 2.1, memória FILME, `BandasCinema` e a seção "A FILME em três camadas".
- `PortaComandos.cs`: sem `/memoria/salvar`, bloqueio da dúvida, bloco `setup` no `/estado`.
- `LinkCanais.cs`: pôr o link de um Setup sem copiar a saída mestre.
- `BgsControle.Telas/SetupViewModel.cs`, `SalvarViewModel.cs` (novos) e `MemoriasViewModel.cs` (refeito); `RepositorioTextoArquivos.cs` e `MauiProgram.cs`: arquivos e registro.
- Testes: `SetupsTestes.cs`, `SetupViewModelTestes.cs` e `SalvarViewModelTestes.cs` (novos); os de memórias, porta, chaves e leitura ajustados.
- `EXPERIENCE.md`: a decisão sobre o equilíbrio na conferência. `deferred-work.md`: link com equilíbrio. `SUPOSICOES.md`: sem a ACK-240-SALVA; MEMORIA-N-NOME e REGRA-NOMES reescritas.

**Revisão:** 44 achados.
- **Corrigidos (19 linhas):** 12 medium e 7 low. Entre eles: a queda limpa o Setup em uso; a conferência que falha volta a valer; o equilíbrio respeita "uma operação por vez"; o `/gerador/parar` passa na dúvida; o Manter tem chave, não se perde em "nome repetido" e ignora escolha velha; a folha do Salvar se atualiza; frases; e os testes que faltavam (link, mudo com operação pendente, Salvar sem memórias, equilíbrio do limiter, IA antes da conferência).
- **Adiado (1):** link 1–2 com equilíbrio.
- **Rejeitados:** os demais, com o motivo no triage log.

**Revisão de acompanhamento recomendada:** sim (12 medium corrigidos). O risco que ficou sem uma segunda leitura é o tempo da conexão: a queda e a reconexão no meio da conferência ou de um aplicar, e o caminho do Manter depois das correções. Nada disso foi exercitado no aparelho.

**Verificação:**
- `dotnet test` do Nucleo.Testes: 865 passam.
- `dotnet test` do Telas.Testes: 325 passam.
- `dotnet build` do App para `net10.0-android`: 0 erros e 0 avisos.
- `git grep` de `SalvarMemoria(` e `NomearMemoria(` em `app-controle/src`: nenhuma linha.

**Riscos que ficam:**
- Nada foi testado no celular nem no processador (é da história 12).
- O preset ainda é o de som completo e não volta com a memória (8d).
- O Player ainda segue a memória do aparelho (8d).
- As telas XAML são da história 10.
