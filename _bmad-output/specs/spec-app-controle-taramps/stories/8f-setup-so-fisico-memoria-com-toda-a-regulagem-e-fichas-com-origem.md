---
title: 'História 8f: Setup só físico, memória com toda a regulagem e fichas com origem'
type: 'feature'
created: '2026-10-29'
status: 'done'
baseline_revision: 'f737de9f461c5d3f8efdc94c8fefcc009a42dc9f'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-29.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/SPEC.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/conhecimento-herdado.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/failure-modes.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/chaves-de-acesso.md'
warnings:
  - multiple-goals
  - oversized
deferred:
  - summary: >-
      Nenhum teste confere que o MauiProgram passa a BaseModelos ao GerenteSetups; sem ela, o app real nunca guarda modelo na base.
    evidence: |-
      O parâmetro modelos do GerenteSetups é opcional (padrão null) e MudarEquipamentosAsync pula a base quando é null. O projeto não tem bancada que rode o MauiProgram; o teste precisaria de uma.
    location: >-
      app-controle/src/BgsControle.App/MauiProgram.cs:150
    severity: low
---

<intent-contract>

## Intent

**Problem:** O Setup mistura o físico com a regulagem (calibração e equilíbrio), o link é um ajuste do usuário, o app nasce com a FILME e o Cinema herdados de calibrações mal feitas, a ficha dos equipamentos não diz de onde veio cada dado, e a IA pode trocar o som inteiro ajuste por ajuste sem o toque do usuário. O Denis decidiu mudar tudo isso em 29/10/2026 (SPEC CAP-4, CAP-6, CAP-12, CAP-16; failure-modes 5; chaves-de-acesso).

**Approach:** No núcleo: uma camada só de regulagem na memória, com o valor final; o Setup com equipamentos e fichas com origem; o link tirado das ligações; a conferência ao conectar contra a memória ativa; o herdado sem memórias e sem presets; uma base de modelos; o `setups.json` e o `presets.json` num formato novo que começa do zero; e a regra do Continuar na porta. Nas telas, só o que o núcleo obriga para compilar e para não mostrar o que deixou de existir.

## Boundaries & Constraints

**Always:**
- **Camadas:** `Regulagem` = 010, 060, 040–043, 050, 030–032, 080, 081, 070–073, 090–092; `Bandas` = 020 e 220; `Fora` = o resto. Saem `Calibracao`, `Carater` e o `Equilibrio` (record, soma, subtração, composição, separação, ajuste, pendente, efetivo e a parte dele no JSON e na folha do Salvar).
- **Conferência ao conectar** (failure-modes 5), contra a memória ativa do Setup de referência, comparando só os endereços da regulagem dela (as bandas não entram):
  - tudo igual → `Confere`, com a memória em uso;
  - só 080, 081, 070–073, 090–092 ou 030–032 diferem → `SomDiferente`, sem mutar;
  - 010, 050, 060 ou 040–043 diferem, ou o Setup não tem memória ativa → muta e `Duvida`, com a lista das diferenças desses códigos (rótulo pelo nome da saída do Setup e o valor da memória e o do aparelho).
  - A dúvida interrompida, a marca do mudo e o bloqueio da IA ficam como estão.
- **Escolher o Setup** aplica a memória ativa (mais o preset lembrado), relê e desmuta; num Setup sem memórias, desmuta sem aplicar nada.
- **Criar Setup novo** (`CriarSetupAsync(nomeSetup, nomeMemoria = null)`): o Setup nasce sem equipamentos, e a regulagem lida vira a primeira memória dele, com o nome dado (nulo ou vazio = o nome do Setup); ela lembra o preset cujas bandas são iguais às lidas, se houver. O Setup e a memória ficam em uso, e desmuta.
- **Link automático:** `Ligacoes` dá o par ligado quando os dois canais de um bridge de um equipamento do grupo 2 são alimentados, cada um, por um dos dois canais vizinhos do processador (CH1+CH2 → Link 1–2; CH3+CH4 → Link 3–4). O Setup não guarda link. O `GerenteSetups` põe o link no `LinkCanais` sempre que hoje põe o do Setup, e também quando os equipamentos do Setup em uso mudam. Sem Setup em uso (dúvida, troca, Seguir sem Setup), os links ficam desligados, como hoje. Saem o `VincularAsync`/`DesvincularAsync` públicos e o aviso "ligue o Link" (`MsgBridgeSemLink`); o espelho das escritas no par continua.
- **Memória:** `MemoriaCelular` guarda `Regulagem` (os valores finais) e `IdPreset`; sai `Herdado`. Aplicar escreve a regulagem e as bandas do preset lembrado (sem preset, não mexe nas bandas), com a cópia antes e a releitura, pela chave "Aplicar memória do celular".
- **Salvar por camadas:** regulagem → memória em uso; 15 bandas → preset lembrado (atualizar ou criar), como hoje. Com memória do aparelho, "Som do aparelho" ou Setup sem memórias: regulagem → memória nova, bandas → preset igual ou novo. Sai a `EscolhaCalibracao` (Manter/Atualizar) e a escrita que ela fazia. O Salvar não manda nada pelo BLE.
- **Herdado (CAP-6):** só o Setup "Casa 2.1" com os equipamentos e as fichas de `conhecimento-herdado.md` (Módulo SD800.4 EVO com bridge 3+4, JBL L, JBL R e Sub Protech Light 8"), sem memórias e sem presets. Os textos herdados (notas, seções) deixam de citar FILME, Cinema e equilíbrio. A leitura embutida de 18/10 (`LerFilme`) fica, para o simulador e a paridade.
- **Ficha:** cada dado técnico vira `DadoTecnico(Valor, Origem, Fonte?)`, com `Origem` = Oficial, OutraFonte, Medido, Estimado ou Digitado, e `Fonte` (endereço ou descrição, até 120 caracteres). Verificado = Oficial ou Medido. Os campos de hoje continuam, e entram: Re, Fs, Qes, Qms, Vas, Bl, Sd, Xmax, Le; tipo de caixa (texto), volume, Fb, área do duto; potência e tensão de ceifamento por canal e em ponte, ganho em ponte; tensão e corrente da alimentação. Todos opcionais e > 0, como hoje. A sugestão de função usa o corte da memória ativa do Setup (sem memória, só a categoria).
- **Base de modelos** (`modelos.json`, novo arquivo privado): guarda, por modelo (texto aparado, sem diferença de maiúsculas), a categoria, o grupo e os dados Oficial e OutraFonte de um equipamento confirmado com modelo; `Sugerir(prefixo)` lista os modelos que começam com o texto; `Ficha(modelo)` devolve os dados guardados com as origens. Nasce com JBL 4TRMS80, Protech Light 8" e SD800.4 EVO.
- **Formatos:** `setups.json` formato 3 e `presets.json` formato 3. Ao ler um formato 1 ou 2, ficam os Setups e os equipamentos (os dados viram `DadoTecnico` com origem Digitado, menos os do Casa 2.1 herdado, que vêm do herdado) e o último Setup usado; somem as memórias, os botões, as memórias ativas e os presets próprios; o arquivo é regravado no formato 3. As playlists das memórias que sumiram não aparecem mais.
- **Continuar (chaves-de-acesso):** na porta, a cada 3 `/ajuste` da IA sem um toque do usuário no meio (uma escrita de origem Tela, uma confirmação na tela ou PARAR IA), o quarto pede confirmação pela folha da IA ("A IA quer seguir mexendo", botão "Continuar"); confirmar zera a conta e executa; recusar ou vencer o prazo recusa só aquele pedido. A conta zera também ao desconectar e ao revogar o cliente.
- Nenhum `240` ou `23N` sai do app. Todo teste roda contra o `ProcessadorSimulado`.

**Never:**
- Telas novas: os campos novos da ficha, o selo de origem, o autocompletar, o segundo nome no Criar Setup, os textos da dúvida sem memória e da primeira abertura são da história 10c. Aqui, as telas só param de usar o que saiu (equilíbrio, toggles de link, Manter/Atualizar a calibração, selos de memória e preset herdados) e continuam compilando.
- Limites físicos (potência e tensão contra o teto): spec do Rendimento.
- Porta nos dois sentidos, modal e o agente: spec do Agente.
- Mexer nas 3 memórias do aparelho.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Primeira abertura | Sem arquivos | Casa 2.1 com 4 equipamentos e fichas; nenhuma memória, nenhum preset; base com 3 modelos | — |
| Primeira conexão | Casa 2.1 sem memória | Muta, `Duvida`; Escolher o Casa 2.1 desmuta sem escrever nada | — |
| Confere | Leitura igual à memória ativa | `Confere`, memória em uso | — |
| Só ganho difere | 081 diferente | `SomDiferente`, sem mutar | — |
| Corte difere | 040 diferente | Muta, `Duvida`, uma linha "Passa-alta da JBL L" com os dois valores | — |
| Criar Setup novo | Na dúvida, nomes "Carro" e "ESTRADA" | Setup Carro sem equipamentos, memória ESTRADA com a regulagem lida, em uso; desmuta | Nome inválido ou repetido recusa sem guardar |
| Link | CH3 e CH4 nos dois canais do bridge 3+4 | Link 3–4 ligado, Sub é uma saída só; tirar a ligação do CH4 desliga o link | — |
| Migração | `setups.json` formato 2 com FILME e equilíbrio; `presets.json` formato 2 | Setups e equipamentos ficam, memórias e presets somem, arquivos no formato 3 | Equipamento ilegível descartado, os outros ficam |
| Base de modelos | Equipamento confirmado com modelo "JBL 4TRMS80" e Fs Oficial | `Sugerir("jbl")` traz o modelo; `Ficha` devolve Fs com a origem Oficial; Re Medido não entra na base | — |
| Continuar | 3 `/ajuste` da IA seguidos | O 4º espera a folha; Continuar executa e zera; recusar devolve recusa e não escreve | Prazo vencido = recusa |
| Toque no meio | 2 ajustes da IA, 1 escrita da tela, 2 da IA | Nenhuma folha | — |

</intent-contract>

## Code Map

Caminhos relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Setups/Camadas.cs` -- `enum Camada` (L7–20), conjuntos de códigos (L97–99), `De` L102, `Consultas` L109; `Equilibrio` (L27–68) e `TemEquilibrio/Somar/Subtrair/Compor/Separar` (L128–172) saem; `Tirar` L179, `Diferentes` L197, `Iguais` L209/217, `Rotulo` L228, `Exibir` L263 ficam. Entra a pergunta "este código muta na conferência?" (010, 050, 060, 040–043).
- `src/BgsControle.Nucleo/Setups/Setup.cs` -- `Setup` (L19–35: sai `Calibracao`, `Equilibrio`, `Link12`, `Link34`); `MemoriaCelular` (L57–65: `Carater` → `Regulagem`, sai `Herdado`); `DiferencaCalibracao` L119 (passa a ser da conferência); `EscolhaCalibracao` L141 sai; `FolhaSalvar` L187–213.
- `src/BgsControle.Nucleo/Setups/GerenteSetups.cs` -- `DadosSetups` L23; `Formato=2` L67; `_pendente` L202 e `EquilibrioPendente/Efetivo` L476–486 saem; `SaidasEmUso/Saidas/SaidaDe` L501–509 (link derivado); `FuncaoSugerida/Corte` L520–548 (corte da memória ativa); conferência `ConferirAsync` L624–721 e `DiferencasDeCalibracao` L819; `EscolherInternoAsync` L837; `CriarSetupAsync` L925; `SeguirSemSetupAsync` L985, `TrocarAsync` L1021, `DesistirAsync` L1065; equipamentos L1172–1246 (religar o link do Setup em uso); `CriarMemoriaAsync` L1263; `AplicarMemoriaAsync` L1339 e `Alvo` L1972; `AjustarEquilibrioAsync` L1465 sai; Salvar `PrepararSalvar` L1519, `LinhaBandasMemoriaNova` L1603, `SalvarAsync` L1617 (Manter em L1687–1712), `MudancasDeCalibracao` L1876 sai, `MudancasDeCarater` L1913 vira da regulagem, `NovaMemoria` L1931; `Herdados()` L2130; `ParaJson` L2238, `DeJson` L2321 (migração 1→2 em L2341–2370, FILME sem preset em L2395–2399), `EscreverEquipamentos` L2574, `EscreverDados` L2662, `LerEquipamento` L2725, `LerDados` L2810, `NumeroEq/Eq` L2520–2536 saem.
- `src/BgsControle.Nucleo/Setups/Equipamentos.cs` -- `DadosTecnicos` L88–102 (campos e `Nenhum`), `Via` L75, `Equipamento` L113–140, `Bridge` L60, `Ligacoes.Seguir` L589 (bridges em L620–627), `CanaisQueAlimentam` L646, `Saidas(lista, link12, link34)` L659 e o aviso do bridge sem link L687–710 (`MsgBridgeSemLink` L227 sai; `MsgBridgeCanaisDiferentes` L219 e `MsgBridgeUmCanal` L223 ficam), `DadosValidos` L422–426, `SugerirFuncao` L733.
- `src/BgsControle.Nucleo/Escrita/LinkCanais.cs` -- `Ligado` L87, `VincularAsync` L110 e `DesvincularAsync` L183 (saem do público), `DefinirEstadoAsync` L204 (o caminho único), `Espelho` L228, `link-canais.json` (`ParaJson` L348).
- `src/BgsControle.Nucleo/Herdado/ConhecimentoHerdado.cs` -- ids L31–44, `EquilibrioCasa` L47, `EquipamentosCasa` L74–100 (fichas novas), notas L59/L102/L106, `Casa` L110, `MemoriaFilmeHerdada` L113 e `Cinema` L117 saem, acessores L144–162 (`Presets` vazio), textos L200–221, `LerFilme` L247 fica.
- `src/BgsControle.Nucleo/Presets/GerentePresets.cs` -- `Formato=2` L35; lista `[.. ConhecimentoHerdado.Presets, .. Proprios]` L176; `ParaJson` L604, `DeJson` L641; as travas de herdado (L185, L326, L375, L405) ficam genéricas.
- `src/BgsControle.Nucleo/Player/GerentePlaylists.cs` -- playlists por id de memória; `GerenteSetups.AdotarPlaylistsAsync` L1860 e `OrdemDeAdocao` L1852 (prioriza o Casa).
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs` -- `RodarAsync` L491–519 é o ponto por onde passa todo POST da IA; `Ajuste` L567–606 (`Confirmacao: null`); `AoRegistrar` L300–304 já vê as escritas de origem Tela; `Operacao` L547; `Recusa` L530. `Porta/PedidosConfirmacao.cs`: `PedidoConfirmacao(Titulo, Risco?, TextoBotao)` L9, `PedirAsync` L81, prazo 60 s L41.
- `src/BgsControle.Telas/RepositorioTextoArquivos.cs` -- constantes L9–33 (entra `modelos.json`); `Nucleo/Escrita/IRepositorioTexto.cs` (`Gravacao`); `App/MauiProgram.cs` L141–142 (repos do `GerenteSetups`).
- Telas que só se ajustam: `AjusteSaidaViewModel.cs` (`ControleEquilibrioViewModel` L17, L180, L213, L271, L457–512), `LinkViewModel.cs` (toggles L163–266; vira só leitura) e `App/Telas/FuncaoPage.xaml` L131, `SalvarViewModel.cs` (L81, L99, L224, L294, L348), `SetupViewModel.cs` L285, `EquipamentosViewModel.cs` (`Montar` L857–889, `AbrirEditar` L716–741), `MemoriasViewModel.cs`/`PresetsViewModel.cs` (selos), `HerdadoViewModel.cs`, as páginas XAML que ligam o equilíbrio e os toggles.
- Testes: `tests/BgsControle.Nucleo.Testes/SetupsTestes.cs` (+ `SetupsEquipamentosTestes.cs`, `SetupsDecisoes2410Testes.cs`; montagem L67–150, aliases `Casa`/`Filme` L60–61), `EquipamentosTestes.cs` (L227, L401–406), `PresetsTestes.cs`, `MemoriasTestes.cs` (`TextoMemoria` L12), `PlaylistsTestes.cs`, `PortaTestes.cs` (bancada L87–232, confirmações L530–600), `UsoTestes.cs`; `tests/BgsControle.Telas.Testes/` (`Bancada` em `ControleParametroViewModelTestes.cs` L22, `SalvarViewModelTestes`, `TelasSaidasDoSetupTestes`, `TelasParte1Testes`, `LinkViewModelTestes`, `EquipamentosViewModelTestes`, `EsquemaTestes` L57/65). O simulador nasce com a leitura de 18/10 (`ProcessadorSimulado.cs` L84).

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Nucleo/Setups/Camadas.cs` -- camada `Regulagem`, o conjunto que muta na conferência, e a saída do equilíbrio -- uma regra só para conferir, aplicar e salvar.
- `src/BgsControle.Nucleo/Setups/Equipamentos.cs` -- `DadoTecnico`, `OrigemDado`, os campos novos, validação, e o link derivado das ligações (`Ligacoes.Links(lista)`), sem o aviso de bridge sem link -- o link sai do físico.
- `src/BgsControle.Nucleo/Setups/BaseModelos.cs` (novo) -- a base de modelos com `modelos.json`, `Guardar`, `Sugerir`, `Ficha` e os 3 modelos herdados -- reaproveitar a ficha entre Setups.
- `src/BgsControle.Nucleo/Setups/Setup.cs` e `GerenteSetups.cs` -- o Setup só físico, a memória com a regulagem, a conferência nova, Escolher/Criar/Seguir/Trocar/Desistir, aplicar, Salvar, o link derivado, o formato 3 com a migração, e guardar na base o equipamento confirmado com modelo -- o modelo de 29/10.
- `src/BgsControle.Nucleo/Herdado/ConhecimentoHerdado.cs` e `Presets/GerentePresets.cs` -- o herdado sem FILME e sem Cinema, as fichas com origem, e o `presets.json` formato 3 -- começar do zero.
- `src/BgsControle.Nucleo/Escrita/LinkCanais.cs` -- sem toggles públicos; o estado vem só do `GerenteSetups` -- o usuário não mexe no link.
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs` -- a conta dos ajustes seguidos da IA e o pedido de Continuar -- fechar a brecha.
- `src/BgsControle.Telas/*` e `src/BgsControle.App/*` -- tirar o equilíbrio, os toggles de link (a tela do link vira uma linha só de leitura), o Manter/Atualizar da calibração e os selos de memória e preset herdados; a ficha monta `DadoTecnico` com origem Digitado; registrar a base de modelos -- compilar sem mostrar o que saiu.
- `tests/**` -- reescrever os testes que dependiam da FILME e do Cinema herdados (criar a memória de teste a partir do simulador com a leitura de 18/10) e cobrir cada linha da matriz -- a prova contra o simulador.

**Acceptance Criteria:**
- Given o app sem arquivos, when ele abre, then há só o Setup Casa 2.1 com os 4 equipamentos e as fichas com origem, nenhuma memória, nenhum preset, e a base com 3 modelos.
- Given um equipamento com o modelo de um equipamento já guardado, when o usuário confirma o Setup, then a base tem o modelo uma vez só, com a ficha mais nova.
- Given qualquer caminho de aplicar, escolher, salvar ou conferir, when o app escreve no aparelho, then nenhum `240` ou `23N` sai e nenhuma escrita soma equilíbrio.
- Given o código completo, when roda o build do App para Android, then não há erro nem aviso, e nenhuma tela mostra equilíbrio, toggle de link ou selo de memória e preset herdados.

## Spec Change Log

## Review Triage Log

### 2026-10-29 — Review pass
- verdicts: 31 findings — high 0, medium 9, low 19, false 3, maybe-false 0
- findings:
  - `[low]` `[reject]` (intenção) As 15 bandas ficam fora da conferência — é a regra escrita na spec e o comportamento de antes; a diferença aparece no Salvar; mudar seria editar a spec.
  - `[false]` `[reject]` (intenção) Carregar uma memória do aparelho não vira referência — memória do aparelho não pertence a Setup (CAP-4); a conferência compara com a memória ativa do celular, por desenho.
  - `[medium]` `[patch]` (intenção) Confirmar pela tela um modelo já guardado gravava uma ficha vazia na base — agrupado com a base de modelos abaixo.
  - `[false]` `[reject]` (intenção) `link-canais.json` não migra — o link é reescrito pelo valor tirado do Setup na próxima conferência ou escolha.
  - `[medium]` `[patch]` (cego) Pedidos `/ajuste` simultâneos passavam do Continuar — a vaga é reservada no mesmo lock da conta e devolvida se o ajuste falha; teste com 5 pedidos ao mesmo tempo.
  - `[medium]` `[patch]` (cego) A base trocava a ficha oficial por uma vazia — ficha filtrada vazia não mexe no que está guardado.
  - `[low]` `[patch]` (cego) O nome guardado pegava a caixa digitada por último — o nome existente fica.
  - `[low]` `[patch]` (cego) A corrente da BOB (da instalação) entrava na base do SD800.4 como Oficial — a base não guarda os campos de alimentação.
  - `[low]` `[patch]` (cego) Falha ao gravar `modelos.json` era silenciosa — a mensagem ganha "Não consegui guardar o modelo na base."
  - `[low]` `[reject]` (cego) `modelos.json` de formato desconhecido é sobrescrito — não existe outro formato; guardar contra ele é complexidade para um caso que não acontece.
  - `[low]` `[patch]` (cego) Crossover mostrava um bloco de link vazio sem bridge — `MostrarLink` exige um par ligado.
  - `[low]` `[patch]` (cego) Textos herdados contradiziam as fichas (×19,6/×40, 3,6 Ω) — alinhados (4 Ω nominal, Re 3,6 Ω medida, ×19,5/×40,5 medidos) e fonte real para o volume do Sub.
  - `[low]` `[patch]` (cego) A migração apagava a Nota do usuário nos equipamentos herdados — Nota própria fica; só a nota herdada antiga é trocada.
  - `[medium]` `[patch]` (cego) Playlists pendentes antigas sobreviviam ao começo do zero e eram adotadas por uma FILME recriada — a migração do `setups.json` descarta as pendentes.
  - `[low]` `[patch]` (cego) O `/ajuda` tinha o 3 fixo no texto — o texto sai da constante.
  - `[low]` `[reject]` (cego) O mesmo erro para o nome do Setup e o da memória — hoje a tela tem um campo só; a história 10c, que põe o segundo campo, precisa separar a mensagem.
  - `[low]` `[reject]` (cego) Potência sem a impedância de referência — o desenho dos campos é da spec; fica para a spec do Rendimento.
  - `[low]` `[patch]` (cego) Sobras (parâmetro do `CascaViewModel`, assinatura do link no `SalvarViewModel`, `Quando` duplicado, doc dos formatos, coluna vazia nos XAML) — removidas.
  - `[medium]` `[patch]` (borda) Corrida no Continuar — mesmo defeito do pedido simultâneo acima.
  - `[medium]` `[patch]` (borda) Ficha vazia ou parcial sobrescrevendo a base — mesmo defeito da base acima.
  - `[low]` `[patch]` (borda) Trocar o modelo na edição levava as origens Oficial do modelo antigo para o novo — as origens só ficam com o mesmo grupo e o mesmo modelo; senão, viram Digitado.
  - `[low]` `[patch]` (borda) `GuardarAsync` falso ignorado — mesmo defeito da falha silenciosa acima.
  - `[false]` `[reject]` (borda) Link com o CH1 alimentando também outro equipamento — os dois canais do bridge têm de ser iguais de qualquer jeito; quem mais estiver no canal recebe o mesmo sinal.
  - `[low]` `[reject]` (borda) Memória com regulagem incompleta confere sem mutar — só com arquivo corrompido; criar memória exige leitura completa; a guarda seria complexidade para um caso fora do uso.
  - `[medium]` `[patch]` (borda) Ligar o link sem copiar deixava o bridge com as metades diferentes sem aviso — ao ligar, a diferença é recalculada pelo espelho e o aviso aparece na hora; teste novo.
  - `[low]` `[patch]` (verificação) Terminar um "aplicar-setup" pendente num Setup sem memórias não tinha teste — teste com marca e `setups.json` formato 2.
  - `[low]` `[patch]` (verificação) Adoção de playlist pelo Criar Setup sem teste — teste com a MUSICA ORIG.
  - `[medium]` `[patch]` (verificação) Reabrir a base de modelos de um `modelos.json` existente não tinha teste — testes de reabrir e de arquivo corrompido.
  - `[low]` `[defer]` (verificação) A ligação da BaseModelos no MauiProgram sem teste — não há bancada que rode o MauiProgram.
  - `[low]` `[patch]` (verificação) Limite de 32 caracteres do tipo de caixa sem teste — teste novo.
  - `[medium]` `[patch]` (verificação) O aviso de saídas diferentes sumia ao ligar o link — mesmo defeito do link sem cópia acima.

## Design Notes

- **Por que o link sai das ligações:** dois canais no mesmo bridge precisam ficar iguais; isso é físico, e o Denis decidiu que o usuário não mexe. Sem Setup, desligado continua valendo (decisão 2 da história 11).
- **Por que a conferência separa os códigos:** corte, atraso, fase e rota errados podem forçar o tweeter ou indicar outra instalação; ganho e EQ diferentes quase sempre são um ajuste que não foi salvo (D3 da proposta).
- **Por que a base guarda só Oficial e OutraFonte:** Medido e Estimado são da instalação (a Re 3,6 Ω da JBL do Casa), não do modelo.
- **Memória de teste:** o simulador nasce com a leitura de 18/10; nos testes, conectar e `CriarMemoriaAsync("FILME")` dá a mesma memória que era herdada, sem herdá-la.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- No aparelho, é a história 12b.

## Auto Run Result

Status: done

**O que entrou.**
- **Camadas:** só `Regulagem`, `Bandas` e `Fora`; o equilíbrio saiu inteiro; `MutaNaConferencia` = 010, 050, 060, 040–043.
- **Setup e memória:** o Setup guarda nome, equipamentos e fichas; a memória guarda a regulagem com o valor final e o preset lembrado. A conferência compara com a memória ativa (`Confere`, `SomDiferente` sem mutar, ou muta e `Duvida`, inclusive sem memória). Escolher um Setup sem memórias só desmuta. `CriarSetupAsync(nomeSetup, nomeMemoria)` cria o Setup e a primeira memória com o som lido.
- **Link:** sai das ligações (`Ligacoes.Links`); sem toggles públicos; religado quando os equipamentos do Setup em uso mudam; ao ligar, o aviso de saídas diferentes é recalculado.
- **Fichas e base:** `DadoTecnico(Valor, Origem, Fonte)` com os campos novos; `BaseModelos` em `modelos.json`, com JBL 4TRMS80, Protech Light 8" e SD800.4 EVO; só Oficial e OutraFonte, sem os campos de alimentação.
- **Herdado e formatos:** só o Casa 2.1 com as fichas; `setups.json` e `presets.json` no formato 3; a migração mantém Setups e equipamentos e descarta memórias, presets, playlists delas e as pendentes antigas.
- **Porta:** Continuar a cada 3 `/ajuste` da IA sem toque, à prova de pedidos simultâneos.
- **Telas:** saíram o equilíbrio, os toggles de link (linha só de leitura), o Manter/Atualizar e os selos herdados de memória e preset; a ficha grava com origem Digitado.

**Arquivos.** Núcleo: `Setups/Camadas.cs`, `Setup.cs`, `Equipamentos.cs`, `GerenteSetups.cs`, `BaseModelos.cs` (novo), `Herdado/ConhecimentoHerdado.cs`, `Presets/GerentePresets.cs`, `Player/GerentePlaylists.cs`, `Escrita/LinkCanais.cs`, `Porta/PortaComandos.cs`. Telas/App: `AjusteSaidaViewModel`, `LinkViewModel`, `FuncaoViewModel`, `SalvarViewModel`, `SetupViewModel`, `EquipamentosViewModel`, `MemoriasViewModel`, `PresetsViewModel`, `CascaViewModel`, `MauiProgram` e os XAML que ligavam o que saiu. Testes: Setups, Equipamentos, Memórias, Presets, Porta, Uso e as telas; `tests/BgsControle.Simulador/CenarioFilme.cs` cria a FILME e o Cinema de teste da leitura de 18/10.

**Revisão.** 31 achados: 21 corrigidos (9 médios), 1 adiado (ligação da base no MauiProgram sem teste), 9 recusados com o motivo no Review Triage Log.

**Decisões de implementação a conferir com o Denis.**
- O ajuste liberado pelo Continuar não conta: depois dele passam mais 3 sem folha; ajuste recusado não conta.
- A base guarda a ficha mais nova inteira, menos quando ela vem sem dado Oficial ou OutraFonte (aí a guardada fica).
- Na dúvida sem memória, a tela de escolha ainda diz "O aparelho está no Casa 2.1." — texto da 10c.
- Na 10c, o Criar Setup com dois nomes precisa de uma mensagem própria para o nome da memória inválido.

**Follow-up review recomendada:** sim (9 médios corrigidos nesta passada). Risco a conferir: a reserva de vaga do Continuar com contador de geração e o recálculo do aviso ao ligar o link, que só foram exercitados contra o simulador.

**Verificação.**
- `dotnet test` Nucleo.Testes: 1040 passam.
- `dotnet test` Telas.Testes: 436 passam.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- Nada visto no aparelho: é a história 12b.

**Riscos.** A migração apaga as memórias e os presets do celular do Denis na primeira abertura depois da atualização (decisão D2); as 3 memórias do processador não são tocadas.
