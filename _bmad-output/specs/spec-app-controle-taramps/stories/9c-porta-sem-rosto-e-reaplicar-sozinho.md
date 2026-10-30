---
title: 'História 9c: porta sem rosto e reaplicar sozinho'
type: 'feature'
created: '2026-10-30'
status: 'done'
baseline_revision: '5d07e11bc0c05184a97df4979aa53dc84db1f5e8'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-30.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/porta-de-comandos.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/chaves-de-acesso.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/failure-modes.md'
warnings:
  - oversized
deferred:
  - summary: >-
      A tela REAPLICANDO não tem saída nem tempo máximo; se uma escrita BLE travar sem a queda ser detectada, o usuário fica preso nela.
    evidence: |-
      ReaplicandoPage bloqueia o Voltar e só fecha quando GerenteSetups.Reaplicando volta a null. Para resolver, conferir se a Sessao tem timeout por escrita que sempre termina AplicarMemoriaAsync.
    location: >-
      app-controle/src/BgsControle.App/Telas/ReaplicandoPage.xaml.cs
    severity: medium (unverified)
---

<intent-contract>

## Intent

**Problem:** O app ainda mostra a IA (página IA ativa com faixa e PARAR IA, página IA pela rede, Histórico, folha "A IA pede para…", modo "travado até PARAR IA"), e na diferença leve ao conectar só avisa e oferece Reaplicar. O Denis decidiu em 30/10 que nenhuma tela mostra a IA e que a diferença leve reaplica sozinha (proposta de 30/10, seções 4.2 a 4.4).

**Approach:** Tirar da interface tudo o que é da IA, mantendo o núcleo da porta, do pareamento e do histórico; a chave vira "Controle pela rede", no grupo APARELHO das Configurações, com o código embaixo; a confirmação vira a folha de risco do app. No núcleo, o toque sempre pausa, e a conferência ao conectar reaplica a memória ativa e o preset dela na diferença leve, com a tela toda "REAPLICANDO".

## Boundaries & Constraints

**Always:**
- **Saem do app:** `IaAtivaPage`, `IaPelaRedePage`, `HistoricoPage` e os ViewModels deles (com `FraseComando`/`LinhaHistorico`, se só eles usam), as linhas "IA pela rede" e "Histórico" e o rótulo "ACESSO E IA" das Configurações, a camada IA ativa da navegação, `AbrirIaPelaRede`/`AbrirHistorico`.
- **Chave "Controle pela rede":** `Chave.IaPelaRede` vira `Chave.ControlePelaRede`, com o nome "Controle pela rede" e o risco "Deixa um computador da casa comandar o som pela rede."; o id guardado `ia-pela-rede` continua (não muda o que está no celular). A linha da chave sai da tela Chaves de acesso e entra no grupo APARELHO das Configurações (a "tela Aparelho" do app de hoje), com a folha do risco ao ligar. Com a chave ligada, embaixo dela: o código de 6 dígitos enquanto ninguém pareou; pareado, o nome do cliente e a data; o endereço (IP:8787); o erro se a porta não abriu. Sem Revogar e sem modo do toque: desligar a chave revoga (já é o que o núcleo faz).
- **Folha de risco do app** (no lugar de `ConfirmacaoIaViewModel`/`Page`): título que diz o que muda, o risco da chave sem mudar o texto, botões "Confirmar" e "Cancelar", prazo "Sem resposta em N s, o pedido é recusado.". Os títulos montados em `PortaComandos` deixam de dizer "A IA pede para …" (ex.: "Aplicar a memória FILME?", "Tocar um tom de 1000 Hz a -20 dB?"); o Continuar vira título "Seguir mexendo no som?" e risco "Já foram 3 ajustes seguidos pela rede sem um toque seu. Ajuste por ajuste, o som inteiro pode mudar." (o 3 vem de `AjustesSemToque`).
- **Uso compartilhado:** saem `ModoToque.TravarAtePararIa`, o modo guardado (`uso-ia.json`, `DefinirModoAsync`), `Travado` e `MsgTravado`; o toque da tela nunca é recusado e sempre pausa a IA ativa; janela de 10 s e "o usuário está mexendo nisso" continuam. `/estado` deixa de mandar `modoToque`. PARAR IA interno (`PararIa`/`Parada`/`Liberar`) fica só como o efeito do revogar. Histórico, `/historico`, `/eventos`, `/tarefa` e o vigia de 30 s continuam no núcleo; o `TextoAjuda` da porta deixa de falar em faixa e PARAR IA.
- **Reaplicar sozinho (regra 5):** quando a conferência acha só ganho, limitador, EQ ou MASTER diferentes, o app, na mesma operação e com origem App, aplica a memória ativa do Setup com o preset que ela lembra (`Alvo`), sem mutar, e só então fica `Confere` com a memória em uso. Enquanto escreve, o núcleo expõe "reaplicando" com o nome da memória e do preset, e a casca mostra por cima de tudo uma tela toda com "REAPLICANDO", o nome da memória, o nome do preset (se houver) e "Mantenha o celular perto do processador."; a tela fecha ao terminar, com sucesso ou não. Os textos vêm do ViewModel.
- Se a chave "Aplicar memória do celular" estiver desligada, ou houver "aplicado pela metade" pendente, não escreve nada e fica como hoje: `SomDiferente`, com o aviso e o Reaplicar das telas. Uma falha no meio segue a regra 3 (pela metade). Dúvida e mudo (corte, atraso, fase, rota, sem memória) não mudam.
- **Teste de arquitetura:** nenhum texto de tela contém a palavra "IA" (maiúsculas, palavra inteira): valores de atributo de todo XAML do App, constantes `string` públicas e propriedades `string` de texto dos ViewModels de `BgsControle.Telas`, nome e risco de cada chave, títulos das folhas montadas pela porta e do Continuar.
- Tudo testado contra o `ProcessadorSimulado`; o build Android sem erro nem aviso.

**Never:**
- Desfazer o núcleo da porta, do pareamento, do histórico ou da pausa no toque.
- Mostrar a IA em qualquer tela, inclusive "IA" em aviso de controle.
- Rotas da criação da memória (9d) ou telas do BGS Sound (redesenho).
- Reaplicar fora da diferença leve, ou mexer no mudo da dúvida.
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Diferença leve | FILME ativa no Casa 2.1 com o preset Cinema; reconecta com 081 (ganho) mexido | Tela REAPLICANDO "FILME" e "Cinema"; escreve só o que difere; releitura igual; `Confere`, FILME em uso, sem mudo | — |
| Leve com a chave desligada | Idem, chave "Aplicar memória do celular" desligada | Nada escrito; `SomDiferente` com Reaplicar | Reaplicar recusado pela chave, como hoje |
| Leve com pela metade pendente | Marca de pela metade guardada | Nada escrito; a folha de pela metade manda | — |
| Falha no meio | BLE cai na reaplicação | Tela REAPLICANDO fecha; "aplicado pela metade" na volta | Regra 3 |
| Diferença forte | Corte mexido | Muta e entra na dúvida, sem reaplicar | — |
| Toque com a IA ativa | `/tarefa` ativa; usuário mexe num ganho | Ganho escrito; a IA pausa; porta recusa esse ajuste por 10 s | — |
| Desligar a chave | Cliente pareado; chave desligada em APARELHO | Porta fecha, cliente revogado, código novo ao religar | — |
| Risco pela porta | Cliente pede aplicar a FILME | Folha "Aplicar a memória FILME?", Confirmar/Cancelar, sem "IA" | Cancelar: a porta recebe a recusa |

</intent-contract>

## Code Map

Caminhos relativos a `app-controle/`. GS = `src/BgsControle.Nucleo/Setups/GerenteSetups.cs`.

- `src/BgsControle.Nucleo/Uso/ModoToque.cs` (L7-14) e `Uso/UsoCompartilhado.cs` -- modo e trava: `_modo` L54, `Travado` L104-111, `AvaliarTela` L179-197 (trava L181-182), `DefinirModoAsync` L326-357, `ParaJson/DeJson` L370-406, `Carregado`; `Sessao.cs` L387-388, 411-412, 512-513 chama `Consultar/Admitir`.
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs` -- `MsgNaoPareado` L52, `MsgChaveDesligada` L56, `TituloContinuar/BotaoContinuar/RiscoContinuar` L62-66 (uso L579); títulos `Folha(...)` L735-822; ao desligar a chave já cancela, `PararIa` e revoga (`AtualizarAsync` L206-261, L242-253); `RevogarAsync` L264-269; `AoMudarUso` L292-318; `/estado` L896-906 (`modoToque` L904); `TextoAjuda` L1087-1117. `Porta/Pareamento.cs` `MsgCodigoErrado` L33, `MsgRevogadoNoMeio` L36 (citam "tela IA pela rede"); `Codigo` nulo depois de parear (L75-82), `Cliente`, `Desde`. `Porta/PedidosConfirmacao.cs` (comentário L6).
- `src/BgsControle.Nucleo/Seguranca/Chaves.cs` -- `Chave.IaPelaRede` e a definição L91-92; JSON por id (L248-287).
- `src/BgsControle.Telas/IaPelaRedeViewModel.cs` -- lógica do código, endereço, cliente e erro em `Atualizar` L182-198, para reaproveitar num `ControlePelaRedeViewModel`. `ConfirmacaoIaViewModel.cs` -- vira a folha de risco (`MsgPrazo` L16, `TextoNaoDeixar`). `IaAtivaViewModel.cs`, `HistoricoViewModel.cs` -- saem.
- `src/BgsControle.Telas/SetupsViewModel.cs` -- `ConfiguracoesViewModel` (classe ~L317): `AbrirIaPelaRede/AbrirHistorico` L335-336, L367-368, títulos L353-356, `Aparelho` (grupo APARELHO).
- `src/BgsControle.Telas/CascaViewModel.cs` -- parâmetros `iaAtiva`/`confirmacaoIa` L61-62, assinaturas L89-92, `Sincronizar` L182-203 (escolha e pela metade), `SincronizarCamadas` L219-229, `IaAtivaPorCima` L206, `ConfirmacaoIaPorCima` L209, filtro L233.
- `src/BgsControle.Telas/Abstracoes.cs` -- `AbrirIaPelaRede` L95-96, `AbrirHistorico` L98-99, `MostrarPorCima` L105, `MostrarCamadas` L107-112. `src/BgsControle.App/NavegacaoMaui.cs` -- L31, L79, L81, `Reconciliar` L110-167 (pilha SetupEscolha → PelaMetade → IaAtiva → ConfirmacaoIa → AlarmeGerador).
- `src/BgsControle.App/MauiProgram.cs` -- `UsoCompartilhado` L48-51 (`ArquivoUsoIa`), DI dos ViewModels L264-274 e páginas L280-283, comentários L237, 261, 334-335. `RepositorioTextoArquivos.cs` L32-33 (`ArquivoUsoIa`).
- XAML: `Telas/ConfiguracoesPage.xaml` (APARELHO L82-194, "ACESSO E IA" L197, linhas L211-231, `FolhaView` do Aparelho L304-306); `Telas/IaPelaRedePage.xaml(.cs)` (linha da chave L39-64 com `ItemChaveViewModel` e folha L116-128, ligadas no code-behind L15-30: o bloco a mover); `Telas/ChavesPage.xaml` L28, L80 (textos com "IA"), lista L36-77; `Telas/ConfirmacaoIaPage.xaml` L28 ("PEDIDO DA IA"); `Telas/AlarmeGeradorPage.xaml` (modelo de tela toda por cima). `App.xaml` estilo `botao-parar-ia` L290 é usado pelo `GeradorPage.xaml:186` (fica).
- `src/BgsControle.Telas/ChavesViewModel.cs` -- `Itens` por `Definicoes`, `Item(Chave)` L82, `LigarAsync` L113-126 com a folha do risco, `NotaIa` L62.
- Conferência (GS): `AoTerminarLeitura` L596-609 → `ConferirAsync` L636-739 segura `_operacao` (L639/L737); ramo leve L686-710 (`SomDiferente`, `SomDoAparelho`); dúvida L712-733 com `Origem.Como(OrigemComando.App)` L716. `ReaplicarAsync` L1371-1378 passa por `AplicarMemoriaComTravaAsync` L1380-1393 (`WaitAsync(0)` → `MsgOcupado`: não chamar de dentro da conferência); `AplicarMemoriaAsync` L1396-1452 (privado, espera a trava; chave L1409); `Alvo` L1836-1846 (regulagem + bandas do `IdPreset`); `AplicarAlvoAsync` L1853-1914 (`_copia.MotivoBloqueio()`). `Setup.cs` `EstadoConferencia` L82-98, `MemoriaCelular.IdPreset` L53. `CopiaAntes.cs` `RetomarAsync` L477-497 também roda no fim da leitura.
- Telas da diferença leve que ficam (chave desligada): `SomEmUsoTexto.cs` L38-44, `InicioViewModel` L97/143/236/275, `MemoriasViewModel` L181-187/269.
- Testes que mudam: `tests/BgsControle.Telas.Testes/HistoricoIaViewModelTestes.cs`, `IaPelaRedeViewModelTestes.cs` (L317, L345 testam a folha), `TelasParte2Testes.cs` (fixture L40-78; L497, 526, 546, 568-576), `ControleParametroViewModelTestes.cs` L62, L103-105, `ChavesViewModelTestes.cs` L51-60, `Falsos.cs` L148-164 (`NavegacaoFalsa`), `ArquiteturaTestes.cs` (`Raiz()` L9-17, leitura de atributos L108-126; modelo para o teste do "IA"); `tests/BgsControle.Nucleo.Testes/UsoTestes.cs` (L163-166, 357, 374, 539, 560, 576, 585, 708; 406-455, 723), `PortaTestes.cs` (IaPelaRede L172…1058; modo L701, 1467; títulos L746-749; `/estado` L868), `EscritaTestes.cs` L573-593. Diferença leve: `SetupsTestes` L327, 341, 1090, 1597; `SetupsDecisoes2410Testes` L253; `SetupsEquipamentosTestes` L67; `PortaTestes` L1214; `TelasParte1Testes` L219; `MemoriasViewModelTestes` L183. Usam a diferença leve para ter "som do aparelho" antes de `Conectar()` e passam a ver a memória reaplicada: `SetupsTestes` L831, 845, 1062, 1720, 1863; `PortaTestes` L1246, 1483; `SalvarViewModelTestes` L83, 113, 244; `MemoriasViewModelTestes` L199; conferir `LinkViewModelTestes` L67 e `MemoriasTestes` L668.

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Nucleo/Uso/ModoToque.cs`, `Uso/UsoCompartilhado.cs`, `Sessao/Sessao.cs` -- tirar o modo, a trava e o `uso-ia.json`; o toque sempre pausa -- regra "o toque pausa".
- `src/BgsControle.Nucleo/Seguranca/Chaves.cs`, `Porta/PortaComandos.cs`, `Porta/Pareamento.cs`, `Porta/PedidosConfirmacao.cs` -- `ControlePelaRede` com nome e risco novos; títulos das folhas e do Continuar sem "IA"; `/estado` sem `modoToque`; ajuda e mensagens sem "tela IA pela rede" -- travas 1, 4 e 5.
- `src/BgsControle.Nucleo/Setups/Setup.cs` e `GerenteSetups.cs` -- reaplicar sozinho no ramo leve dentro da trava da conferência, com origem App, pulando com a chave desligada ou pela metade pendente; o sinal "reaplicando" (memória e preset) com aviso de mudança -- regra 5.
- `src/BgsControle.Telas/` -- apagar `IaAtivaViewModel`, `IaPelaRedeViewModel`, `HistoricoViewModel`; criar `ControlePelaRedeViewModel` (código, cliente, data, endereço, erro); `ConfirmacaoIaViewModel` → `FolhaRiscoViewModel` com Confirmar/Cancelar e o prazo novo; `ConfiguracoesViewModel` sem as linhas da IA; `CascaViewModel` sem a camada IA ativa, com a tela REAPLICANDO e os textos dela; `Abstracoes.cs` e `ChavesViewModel` (sem a linha da Controle pela rede e sem a `NotaIa`) -- telas sem a IA.
- `src/BgsControle.App/` -- apagar as três páginas; `ConfirmacaoIaPage` → `FolhaRiscoPage`; `ReaplicandoPage` nova no modelo da `AlarmeGeradorPage`; a linha da chave e a folha do risco no grupo APARELHO da `ConfiguracoesPage`; `ChavesPage` sem textos com "IA"; `NavegacaoMaui`, `MauiProgram`, `RepositorioTextoArquivos` -- o app de hoje sem a IA.
- `tests/` -- atualizar os testes do Code Map; um teste por linha da matriz; o teste de arquitetura do "IA" -- a prova.

**Acceptance Criteria:**
- Given o código completo, when roda o teste de arquitetura, then nenhum atributo de XAML, texto de ViewModel, nome ou risco de chave, título de folha da porta ou do Continuar contém a palavra "IA".
- Given a chave "Controle pela rede" ligada nas Configurações e ninguém pareado, when a tela é aberta, then aparece o código de 6 dígitos embaixo da chave; e when um cliente pareia, then aparecem o nome e a data no lugar do código.
- Given o núcleo da porta, when um cliente pareado lê `/historico` depois de um ajuste pela tela, then a entrada está lá com a origem tela.
- Given o código completo, when roda o build do App para Android, then não há erro nem aviso.

## Spec Change Log

## Review Triage Log

### 2026-10-30 — Review pass
- verdicts: 35 findings — high 0, medium 3, low 25, false 6, maybe-false 1
- findings:
  - `[low]` `reject` Intent: o teste de "IA" olha o código-fonte e os VMs em repouso, não a tela renderizada — cobre exatamente as fontes que a spec lista; ampliar exige montar cada estado.
  - `[low]` `reject` Intent: a tela REAPLICANDO é testada só no VM e na navegação falsa — a casca é fina; o aparelho fica na 12b.
  - `[low]` `reject` Intent: falha na reaplicação com a sessão aberta não tem teste de tela — termina em SomDiferente com a mensagem do Terminar, o caminho de hoje.
  - `[low]` `reject` Intent: durante a escrita o estado é SomDiferente por baixo da tela toda — a tela toda cobre; o fim é Confere como pede a spec.
  - `[false]` `reject` Intent: a folha ao ligar a chave é a da tela Chaves e não a folha de risco da porta — a spec pede "a folha do risco ao ligar", que é a folha de ligar chave.
  - `[low]` `reject` Intent: preset com bandas diferentes na reaplicação sem teste — AplicarMemoriaAsync já é testado com preset; caso raro.
  - `[medium]` `patch` Intent: origem App da reaplicação sem teste (diferença forte e build cobertos por testes existentes e pela verificação) — teste novo de origem App adicionado.
  - `[low]` `reject` Blind: critérios de aceite sem a regra 5 — a correção editaria a spec.
  - `[low]` `reject` Blind: releitura diferente ainda fica Confere — regra já existente de AplicarMemoriaAsync, com o aviso MsgAplicadaDiferente.
  - `[low]` `reject` Blind: falha com a sessão aberta sem teste — mesmo caso do Intent acima.
  - `[medium]` `patch` Blind: nada prova a origem App da reaplicação — teste O_reaplicar_sozinho_da_diferenca_leve_escreve_como_App_e_nao_prende_o_ajuste_para_a_porta.
  - `[low]` `reject` Blind: sobra o aviso "Memória FILME aplicada." após reaplicar — escolha deliberada, informa o que mudou.
  - `[maybe-false]` `defer` Blind: REAPLICANDO sem saída nem tempo máximo — depende do timeout por escrita da Sessao; diferido.
  - `[low]` `patch` Blind: falha ao salvar a chave invisível nas Configurações — linha de Chaves.Mensagem adicionada no bloco APARELHO.
  - `[low]` `reject` Blind: AvisoConexao/MsgPerdeuConexao sem tela — estado do núcleo sem leitor; não chega a nenhuma tela.
  - `[low]` `reject` Blind: regex dos títulos da porta pega só uma linha — os títulos atuais estão todos em uma linha; robustez extra seria mais código.
  - `[low]` `patch` Blind: TextoAjuda com duas descrições da confirmação — todas dizem "folha de risco".
  - `[low]` `patch` Blind: comentários antigos de modo e PARAR IA — comentários de UsoCompartilhado e PortaComandos atualizados.
  - `[low]` `patch` Blind: summaries trocados em PortaTestes e nome do helper — summary de volta em Pronto(), helper GanhoDiferentePeloApp.
  - `[low]` `patch` Blind: helpers sobrando em UsoTestes e linha em branco dupla — IaAtiva() síncrono; linha removida.
  - `[low]` `reject` Blind: indentação, x:DataType duplo e cor fixa no XAML — cosmético, sem dano.
  - `[low]` `reject` Blind: a folha do ChavesViewModel é compartilhada entre duas telas — funciona hoje; corrigir pede estado novo.
  - `[low]` `reject` Blind: pedido da porta durante REAPLICANDO sem teste — a trava da operação já recusa; caso raro.
  - `[low]` `reject` Edge: firmware só leitura ou chave de ajustes desligada fazem o REAPLICANDO piscar com falha — caso raro; o aviso diz o motivo; guarda extra é complexidade.
  - `[low]` `reject` Edge: contagem de diferenças velha no log após falha — só o log.
  - `[false]` `reject` Edge: sessão cai ao pular a reaplicação e a conferência conta como feita — `if (!SessaoAberta) return true` deixa a conexão nova conferir de novo.
  - `[low]` `reject` Edge: exceção dentro de AplicarMemoriaAsync pula o Terminar — o finally limpa o Reaplicando; mesmo tratamento dos outros chamadores.
  - `[false]` `reject` Edge: ControlePelaRede nulo nas Configurações — a injeção sempre fornece o VM.
  - `[low]` `reject` Edge: folha da Chaves aberta reaparece nas Configurações — mesmo caso da folha compartilhada.
  - `[low]` `reject` Edge: AvisoConexao sem leitor — mesmo caso acima.
  - `[false]` `reject` Edge: sem o Revogar, trocar de cliente pede desligar e religar a chave — é o que a spec pede.
  - `[low]` `reject` Edge: uso-ia.json antigo fica órfão — arquivo pequeno, nunca lido.
  - `[false]` `reject` Edge: /estado sem modoToque — a spec manda tirar.
  - `[false]` `reject` Edge: MsgIaNaoMuda ainda diz IA — vai só para o cliente da porta, nunca para a tela.
  - `[medium]` `patch` Verification gap: nenhum teste fixa a origem App da reaplicação — teste adicionado e provado (falha sem o escopo).

## Design Notes

- **"Tela Aparelho" do app de hoje:** a `AparelhoPage` é a tela Senha; o grupo APARELHO (remote out, Bluetooth, nome) fica nas Configurações. A chave entra nesse grupo, reaproveitando a linha `ItemChaveViewModel` e a folha do `ChavesViewModel` que a `IaPelaRedePage` já embutia. A tela Aparelho do BGS Sound é do redesenho.
- **Por que não chamar `ReaplicarAsync`:** a conferência já segura `_operacao`, e `ReaplicarAsync` pega a trava com `WaitAsync(0)` e devolveria "ocupado". Dentro da conferência, marcar o Setup em uso e chamar `AplicarMemoriaAsync` com `Origem.Como(OrigemComando.App)`; sem o escopo, a origem seria Tela e contaria como toque.
- **Chave desligada na reaplicação:** a regra das chaves recusa antes de qualquer envio; o caminho de hoje (aviso + Reaplicar travado pela chave) é o que sobra, sem fluxo novo.
- **Id da chave:** fica `ia-pela-rede`, porque o id não aparece e trocá-lo desligaria em silêncio uma chave ligada no celular.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- No aparelho, é a história 12b (reconectar com o ganho mexido → REAPLICANDO).

## Auto Run Result

- **Resumo:** nenhuma tela mostra mais a IA (saem IA ativa, IA pela rede e Histórico; folha de risco do app com Confirmar/Cancelar; chave "Controle pela rede" no grupo APARELHO das Configurações, com código, cliente, endereço e erro embaixo). O toque sempre pausa (sai o modo travado). Na diferença leve ao conectar, o app reaplica sozinho a memória ativa e o preset dela, com origem App e a tela toda REAPLICANDO; pula com a chave desligada ou pela metade pendente.
- **Arquivos:** `GerenteSetups.cs` (reaplicar na conferência, `Reaplicando`), `UsoCompartilhado.cs` e `EstadoIa.cs` (sem modo), `PortaComandos.cs` (títulos, Continuar, `/estado`, ajuda), `Chaves.cs` (`ControlePelaRede`), `Pareamento.cs`/`PedidosConfirmacao.cs` (textos), Telas: `FolhaRiscoViewModel`, `ReaplicandoViewModel`, `ControlePelaRedeViewModel`, `CascaViewModel`, `Abstracoes`; App: `FolhaRiscoPage`, `ReaplicandoPage`, `ConfiguracoesPage`, `NavegacaoMaui`, `MauiProgram`; removidas IaAtiva, IaPelaRede e Historico (páginas e VMs); testes atualizados e novos (arquitetura sem "IA", matriz, origem App).
- **Revisão:** 35 achados; 8 corrigidos (3 medium: teste de origem App; 5 low: mensagem da chave, ajuda da porta, comentários, testes); 1 diferido (REAPLICANDO sem tempo máximo, não verificado); 26 rejeitados com o motivo no Triage Log.
- **Revisão de acompanhamento:** false (nenhum high corrigido; os 3 medium são um único teste que falta, agora provado).
- **Verificação:** `dotnet test` Nucleo 1023 e Telas 413 passam; build Android 0 erros, 0 avisos (rodado de novo após as correções).
- **Riscos:** não testado no aparelho real (história 12b); tela REAPLICANDO sem tempo máximo (diferido).
