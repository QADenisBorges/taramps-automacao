---
title: 'História 14c: telas do lado do app do Agente'
type: 'feature'
created: '2026-11-01'
baseline_revision: 'f0202887c5f6ffb24573e6c2417a9e57df77182f'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/specs/spec-agente-memoria/lado-do-app.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/DESIGN.md'
warnings:
  - oversized
deferred:
  - summary: >-
      Nenhum teste confere que o contêiner do app entrega a PropostaFichaViewModel à casca e a casca às páginas que espelham o aviso rápido.
    evidence: |-
      CascaViewModel (ficha), ConfiguracoesPage, SetupPage e PropostaFichaPage recebem parâmetros opcionais (= null), resolvidos pelo DI do MauiProgram.
      Os testes montam a casca à mão com ficha: _vm. Se o registro virar uma fábrica sem o argumento, a folha nunca sobe e todos os testes passam.
      Os parâmetros opcionais de antes (criacao, player, folhaRisco) têm a mesma lacuna. Fecha com um teste da raiz de composição ou com a conferência no A52 (12e).
    location: >-
      app-controle/src/BgsControle.App/MauiProgram.cs:375
    severity: medium
---

<intent-contract>

## Intent

**Problem:** A 14b deixou no núcleo e nos ViewModels a espera do "+ Nova memória", a escuta "1 ou 2" e a proposta de ficha, mas nenhuma tela as mostra. O usuário não vê a espera, não consegue ouvir e escolher na escuta, e a folha da ficha nunca sobe.

**Approach:** XAML no visual BGS Sound (DESIGN.md, UX de 30/10), ligado aos ViewModels da 14b:
- a espera no chip "+ Nova memória";
- a fase `Escuta` na `CriacaoPage`;
- uma folha global nova, a `PropostaFichaPage`.

Os avisos curtos vão ao aviso rápido da casca. Tudo o que o UX não desenhou fica marcado como [ASSUMPTION] para a 12e.

## Boundaries & Constraints

**Always:**
- **A espera do pedido** ([ASSUMPTION]: a regra do spinner do EXPERIENCE, o anel pequeno de 18 px no botão que disparou a ação):
  - Enquanto `EsperandoAgente`, o chip "+ Nova memória" mostra o `AnelDuploView` de 18 px à esquerda do texto. Embaixo dele fica o `MotivoTravaNovaMemoria` ("Começando a nova memória…").
  - O prazo vencido (`MsgSemResposta`) e a chave desligada (`MsgLigueControle`) também vão ao aviso rápido da casca (2,6 s), que aparece em Memórias e nas Configurações (a tela Aparelho). O `Mensagem` continua como está.
  - Com o movimento reduzido, o anel gira devagar (`Lento`).
- **A escuta "1 ou 2"** (`CriacaoPage`, `FaseEscuta`):
  - A barra do fluxo fica como nas perguntas, com o ✕ e sem o ‹.
  - O `TextoEscuta` fica em `tipo-question`.
  - Dois botões grandes lado a lado, "1" e "2", quadrados, de pelo menos 120 px de altura.
    - O marcado (`Marcado1`/`Marcado2`) tem o fundo `brand-12` e o contorno `brand` de 1,5. O outro fica em `surface-1`.
    - Embaixo do número marcado aparece "tocando agora" (`TextoTocando`, novo no VM).
    - Têm a `SemanticProperties.Description` de `DescricaoBotao1`/`DescricaoBotao2`.
  - Enquanto `Escrevendo`, os botões ficam a 0,35 e o anel duplo de 18 px gira.
  - O `AvisoEscuta` aparece em `apoio`.
  - Embaixo, em coluna: "Prefiro o 1" e "Prefiro o 2" (`botao-fluxo-secundario`) e "Tanto faz" (`botao-fluxo-secundario`).
  - A escuta entra como uma pergunta (0,32 s, de 28 px pela direita; fade com o movimento reduzido). Ao entrar, o palco e o Aplicando somem. Depois da escolha, a fase que vem anima como hoje.
  - O Voltar do Android já abre a folha de cancelar (VM).
- **A folha da proposta de ficha** (`PropostaFichaPage`, nova; [ASSUMPTION]: o UX não desenhou, questão 17):
  - É uma camada global, abaixo da folha de risco, como as folhas do nível e do outro player. Usa o `FolhaView Bgs="True"`, com `Aberta` e com `Fechar` (o véu e o Voltar do Android fecham sem gravar).
  - No topo: o `Titulo` ("Ficha do ABC 123") em `tipo-sheet-title`, o `Equipamento` e o `Explica`.
  - **Cada dado:**
    - o rótulo e o valor proposto (`brand`);
    - a origem;
    - "Agora: {ValorAtual} ({OrigemAtual})", quando há valor atual;
    - a fonte, em `text-secondary`, com quebra de linha;
    - o aviso em `warning`, quando há;
    - "Usar" e "Recusar". O escolhido fica marcado: `Usado` com o contorno `brand`, `Recusado` com o texto apagado.
  - Depois vêm "Usar todos" e "Recusar todos" lado a lado, o `Erro` em `warning`, "Salvar" (`botao-fluxo`, com o anel de 18 px enquanto `Salvando`) e "Fechar" (`botao-fluxo-secundario`).
  - O `AvisoFinal` ("2 dados salvos na ficha do …") vai ao aviso rápido da casca.
  - A lista rola dentro da folha (a folha já tem `ScrollView`).
- **Navegação:**
  - `INavegacao.MostrarFolhas` ganha o 3º argumento, `fichaProposta`.
  - O `CascaViewModel` ouve `PropostaFichaViewModel.Aberta` (parâmetro opcional, como os outros).
  - A `NavegacaoMaui` empilha a `PropostaFichaPage` depois da `OutroPlayerPage` e antes da folha de risco.
- **Textos:** todo texto vem do VM ou de `x:Static` (nada escrito à mão no XAML), e nenhum contém "IA". As cores só pelos tokens, e cada XAML com `x:DataType` (`ArquiteturaTestes`).

**Never:**
- Mudar as regras do núcleo, da porta, dos prazos ou dos ViewModels da 14b, fora os textos e os eventos novos de aviso.
- A Roda (o chip continua em Memórias até ela existir).
- As skills do Agente (14d) e o teste no aparelho (12e).
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Espera | Chave ligada; toque no chip | O anel de 18 px no chip; "Começando a nova memória…" | 2º toque: nada |
| Prazo | 15 s sem abrir | O aviso rápido com `MsgSemResposta`; o anel some | — |
| Chave desligada | Toque | O aviso rápido com `MsgLigueControle`; vai às Configurações | — |
| Escuta | A porta manda a escuta no refino | A camada mostra o texto, "1" marcado com "tocando agora", e os três botões | — |
| Trocar | Toque em "2" | Botões travados enquanto `Escrevendo`; depois o "2" marcado | Releitura diferente: o `AvisoEscuta` |
| Proposta | 3 dados chegam | A folha sobe por cima de qualquer tela, com os 3 dados | — |
| Salvar ficha | "Usar" em 2 e Salvar | A folha fecha; o aviso rápido "2 dados salvos na ficha do …" | Erro: o texto do erro na folha |
| Fechar ficha | Toque no véu ou Voltar | A folha fecha; nada gravado | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.App/Telas/CriacaoPage.xaml` (as telas na grade da linha 121; o modelo é o `ScrollView` das perguntas, l.124–196, e o estilo `resposta`) e `CriacaoPage.xaml.cs`:
  - `AplicarFase` (95), onde entra o `case FaseCriacao.Escuta`;
  - `EntrarPerguntaAsync` (148), para animar o bloco da escuta;
  - `OnAppearing` (48).
- `src/BgsControle.Telas/CriacaoMemoriaViewModel.cs`: os membros da escuta estão nas l.545–635 (`FaseEscuta`, `TextoEscuta`, `Marcado1/2`, `DescricaoBotao1/2`, `Escrevendo`, `PodeTocarEscuta`, `AvisoEscuta` e os comandos `Ouvir1/2`, `Prefiro1/2` e `TantoFaz`). As constantes de texto ficam perto da l.171; entra `TextoTocando`. `AvisarEscuta` (1151) avisa as propriedades.
- `src/BgsControle.App/Telas/MemoriasPage.xaml:92-103` (o chip) e `src/BgsControle.Telas/MemoriasViewModel.cs`:
  - `EsperandoAgente` (294), `PedirNovaMemoria` (367), `AoVencerPrazo` (400) e `TerminarEsperaCom` (≈743);
  - o modelo do evento é o `PlayerViewModel.AvisoRapido`.
- `src/BgsControle.Telas/PropostaFichaViewModel.cs` (todo; `DadoPropostoViewModel` com `Usado`, `Recusado`, `ValorAtual`, `OrigemAtual` e `Aviso`; `AvisoFinal`).
- `src/BgsControle.App/Telas/OutroPlayerPage.xaml(.cs)` e `NivelFolhaPage.xaml.cs` são o modelo da folha global (fundo transparente e `OnBackButtonPressed`). `Controles/FolhaView.cs` tem `Bgs`, `Aberta` e `Fechar`, e já rola.
- `src/BgsControle.Telas/CascaViewModel.cs`:
  - `SincronizarFolhas` (133) e `AoMudarFolhas` (144);
  - `AoAvisar`/`MostrarAviso`;
  - o construtor (55–122) com os parâmetros opcionais.
- `src/BgsControle.Telas/Abstracoes.cs:130` (`MostrarFolhas`), `src/BgsControle.App/NavegacaoMaui.cs` (`_folhas`, `MostrarFolhas` 152, `Reconciliar`) e `tests/BgsControle.Telas.Testes/Falsos.cs:168-170`.
- `src/BgsControle.App/Telas/ConfiguracoesPage.xaml(.cs)`: entra o `AvisoRapidoView` com o `EspelhoAvisoRapido.Ligar` (como em `MemoriasPage.xaml.cs:22`). O construtor ganha o `CascaViewModel?`.
- `src/BgsControle.App/MauiProgram.cs:477-478` (as páginas das folhas; entra a `PropostaFichaPage`).
- `src/BgsControle.App/App.xaml`: os estilos `botao-fluxo`, `botao-fluxo-secundario`, `tipo-question`, `tipo-sheet-title`, `tipo-body`, `tipo-answer` e os tokens `warning`, `brand-12`, `surface-1` e `text-secondary`.
- Testes:
  - `tests/BgsControle.Telas.Testes/ArquiteturaTestes.cs` (a varredura do "IA" em 208, com a lista de XAML que tem de existir, e o `x:DataType` em 317);
  - `CriacaoMemoriaViewModelTestes`, `MemoriasViewModelTestes`, `PropostaFichaViewModelTestes`;
  - `TesteSecoViewModelTestes:428-434` (usa `Nav.Folhas`).

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Telas/MemoriasViewModel.cs`: o evento `AvisoRapido` (string), disparado com `MsgSemResposta` e `MsgLigueControle`.
- `src/BgsControle.Telas/CriacaoMemoriaViewModel.cs`: `TextoTocandoAgora = "tocando agora"` e `TextoTocando`.
- `src/BgsControle.Telas/CascaViewModel.cs`: o parâmetro `PropostaFichaViewModel? ficha`; ouve `Aberta` (as folhas) e `AvisoFinal` (o aviso rápido); ouve `_memorias.AvisoRapido`; expõe `FolhaFichaPorCima`. `Abstracoes.cs`, `NavegacaoMaui.cs` e `Falsos.cs` ganham o 3º argumento.
- `src/BgsControle.App/Telas/PropostaFichaPage.xaml(.cs)` (novos), `MauiProgram.cs` (registro) e `NavegacaoMaui.cs` (a ordem).
- `src/BgsControle.App/Telas/CriacaoPage.xaml(.cs)`: o bloco da escuta e o `case FaseCriacao.Escuta`.
- `src/BgsControle.App/Telas/MemoriasPage.xaml`: o anel no chip. `ConfiguracoesPage.xaml(.cs)`: o aviso rápido espelhado.
- Testes:
  - `MemoriasViewModelTestes`: o evento no prazo e na chave desligada;
  - `CascaViewModel`: a folha da ficha sobe e desce, e o aviso final e o da espera vão ao aviso rápido (num arquivo de testes da casca que já existir, ou em `PropostaFichaViewModelTestes`);
  - `CriacaoMemoriaViewModelTestes`: `TextoTocando` segue a versão marcada;
  - `ArquiteturaTestes`: `PropostaFichaPage.xaml` na lista do "IA". Um teste novo confere que a `CriacaoPage` liga `Ouvir1`, `Ouvir2`, `Prefiro1`, `Prefiro2`, `TantoFaz` e `FaseEscuta`, que a `PropostaFichaPage` liga `Usar`, `Recusar`, `UsarTodos`, `RecusarTodos`, `Salvar` e `Fechar`, e que a `MemoriasPage` liga `EsperandoAgente`.

**Acceptance Criteria:**
- Given a criação no refino, when a porta manda uma escuta, then a camada mostra os dois botões grandes com o marcado, e os toques chegam aos comandos do VM.
- Given uma proposta de ficha pendente, when ela chega com qualquer tela aberta, then a folha global sobe, e Salvar, Fechar e o véu se comportam como o VM manda.
- Given os testes que já existem, when a 14c entra, then todos passam, e o build Android sai sem erro nem aviso.

## Spec Change Log

## Review Triage Log

### 2026-11-01 — Review pass
- verdicts: 28 findings — high 0, medium 4, low 20, false 4, maybe-false 0
- findings:
  - `[low]` `[reject]` Intenção: o visual e o layout não têm teste — a conferência visual é da 12e; o XAML não roda em Telas.Testes.
  - `[low]` `[reject]` Intenção: a ordem das camadas e o registro só pela navegação falsa — a `NavegacaoMaui` não roda nos testes; o mesmo caso do diferido do DI.
  - `[low]` `[reject]` Intenção: o teste das ligações não confere o `x:DataType` de cada nome — o conserto (casar cada ligação ao seu escopo) acrescenta complexidade; o XAML compilado já recusa ligações erradas.
  - `[medium]` `[defer]` Intenção: o espelho do aviso nas Configurações depende de o DI preencher o parâmetro opcional — junto com o diferido do Verification Gap.
  - `[low]` `[reject]` Intenção: as suposições concretas da 14c não estão em `lado-do-app.md`, onde a 12e lê — mudar o contrato do Agente é do Denis; a lista vai no Auto Run Result, para a 12e.
  - `[false]` `[reject]` Intenção: a espera pequena no chip em vez da tela toda — escolha registrada nas Design Notes, pela regra do spinner do EXPERIENCE; não é defeito.
  - `[false]` `[reject]` Intenção: o prazo não "volta à Roda" — a Roda não existe; ficar em Memórias é o equivalente.
  - `[false]` `[reject]` Intenção: mudanças além do XAML — o "Never" da spec permite os textos e os eventos de aviso.
  - `[medium]` `[defer]` Verification Gap: a ligação do DI da `PropostaFichaViewModel` na casca e da casca nas páginas não tem teste — no `deferred` (teste da raiz de composição ou o A52 na 12e).
  - `[medium]` `[patch]` Edge: o aviso "2 dados salvos" fica atrás das páginas sem espelho (a `SetupPage`, de onde a ficha é pedida) — a `SetupPage` ganhou o `AvisoRapidoView` com o `EspelhoAvisoRapido`.
  - `[low]` `[patch]` Edge: com outra proposta na fila, a folha fica e o aviso do Salvar some atrás dela — a `PropostaFichaPage` também espelha o aviso (mesmo conserto).
  - `[low]` `[reject]` Edge: o Salvar com o equipamento mudado fecha a folha sem mensagem (`MsgPrazo`) — raro (a folha é modal; só a porta muda o equipamento por baixo), e o conserto é um ramo novo no ViewModel da 14b.
  - `[low]` `[reject]` Edge (alegação): "os avisos curtos vão ao aviso rápido" não cobre o `MsgPrazo` do Salvar — o mesmo do achado acima.
  - `[low]` `[reject]` Blind: o `MsgPrazo` do Salvar se perde — o mesmo do Edge acima.
  - `[low]` `[reject]` Blind: Salvar sem nada marcado fecha sem aviso — é o mesmo que Fechar, que não grava nada (14b).
  - `[low]` `[reject]` Blind: "Usar todos" pula o medido sem explicar — o cartão já mostra "Agora: … (medido ✓)"; a 12e confere.
  - `[low]` `[reject]` Blind: a 2ª proposta da fila abre com a rolagem no meio — raro; mexe na folha comum.
  - `[low]` `[patch]` Blind: os botões da escuta só apagam enquanto escreve, e o TalkBack os lê como ativos — `IsEnabled="{Binding PodeTocarEscuta}"` nos cinco botões.
  - `[low]` `[patch]` Blind: o TalkBack lê "tocando agora" duas vezes — o rótulo saiu da árvore de acessibilidade (`AutomationProperties.IsInAccessibleTree="False"`).
  - `[low]` `[reject]` Blind: Usar/Recusar sem estado para o TalkBack, e o Recusar sem marca — pede propriedades novas; a 12e confere.
  - `[low]` `[reject]` Blind: o anel ciano sobre o botão ciano no Salvar — o mesmo padrão do Salvar da criação (10d); a 12e confere.
  - `[medium]` `[patch]` Blind: o aviso do Salvar e do prazo não aparece em várias telas — o mesmo conserto do Edge (o espelho na `SetupPage` e na folha).
  - `[low]` `[reject]` Blind: o texto do prazo aparece duas vezes em Memórias — a spec manda manter o `Mensagem`.
  - `[low]` `[patch]` Blind: desligar a chave nas Configurações durante a espera mostra "Ligue o Controle pela rede" ali mesmo — o aviso rápido desse caso saiu (fica só o `Mensagem` de Memórias); o teste foi ajustado.
  - `[low]` `[reject]` Blind: o teste da arquitetura é frouxo (listas à mão, `{}`) — o mesmo do achado de intenção; o XAML compilado já confere as ligações.
  - `[low]` `[patch]` Blind: a fonte quebra no meio da palavra (`CharacterWrap`) — trocado por `WordWrap`.
  - `[low]` `[reject]` Blind: o chip muda de largura na espera, com o 42 mágico — visual, para a 12e.
  - `[false]` `[reject]` Blind: "px" nos comentários em vez de dp — o DESIGN.md e a spec medem em px (as unidades independentes do MAUI); não engana a 12e.

## Design Notes

- **O anel no chip, e não uma tela:** o EXPERIENCE diz que o anel pequeno de 18 px fica no botão que disparou a ação, e o grande só ao conectar e aplicando. A criação em tela toda só sobe quando ela abre de fato. Assim, o prazo vencido não deixa uma tela vazia para trás.
- **O aviso rápido para o prazo e a chave:** a chave desligada leva às Configurações, e o `Mensagem` de Memórias sairia da tela junto com ela. O aviso da casca acompanha o usuário.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- A conferência visual no A52 fica para a 12e (sem celular no adb nesta história).

## Auto Run Result

Status: done

**O que entrou.** As telas do lado do app do Agente (lado-do-app.md), no visual BGS Sound:
- **A espera do "+ Nova memória":** o anel duplo de 18 px no chip e "Começando a nova memória…" embaixo. O prazo vencido e a chave desligada no toque vão também ao aviso rápido da casca, que agora aparece nas Configurações.
- **A escuta "1 ou 2"** na `CriacaoPage`:
  - o texto;
  - os botões quadrados "1" e "2", com "tocando agora" no marcado;
  - o anel e o aviso;
  - "Prefiro o 1", "Prefiro o 2" e "Tanto faz".

  Ela entra como uma pergunta, e os botões travam enquanto escreve.
- **A folha da proposta de ficha** (`PropostaFichaPage`), global, abaixo da folha de risco:
  - cada dado com o valor, a origem, o valor de agora, a fonte, o aviso e Usar/Recusar;
  - "Usar todos", "Recusar todos", Salvar e Fechar.

  O "2 dados salvos …" vai ao aviso rápido.

**Arquivos** (em `app-controle/`):
- `src/BgsControle.App/Telas/CriacaoPage.xaml(.cs)`: o bloco da escuta e a entrada dele.
- `src/BgsControle.App/Telas/PropostaFichaPage.xaml(.cs)` (novos): a folha da ficha, com o aviso rápido espelhado.
- `src/BgsControle.App/Telas/MemoriasPage.xaml(.cs)`: o anel no chip.
- `src/BgsControle.App/Telas/ConfiguracoesPage.xaml(.cs)` e `SetupPage.xaml(.cs)`: o aviso rápido espelhado.
- `src/BgsControle.App/NavegacaoMaui.cs` e `MauiProgram.cs`: a folha da ficha na pilha e o registro.
- `src/BgsControle.Telas/CascaViewModel.cs` (a folha da ficha e os avisos) e `Abstracoes.cs` (o 3º argumento de `MostrarFolhas`).
- `src/BgsControle.Telas/MemoriasViewModel.cs` (o evento `AvisoRapido`), `CriacaoMemoriaViewModel.cs` (`TextoTocando`) e `PropostaFichaViewModel.cs` (`TextoAtual`).
- Testes: `ArquiteturaTestes`, `MemoriasViewModelTestes`, `PropostaFichaViewModelTestes`, `CriacaoMemoriaViewModelTestes`, `TesteSecoViewModelTestes` e `Falsos`.

**Revisão.** 28 achados: 4 medium, 20 low e 4 false.
- **Patches:**
  - o aviso rápido espelhado na `SetupPage` e na folha da ficha (o medium);
  - os botões da escuta travados também para o TalkBack;
  - o "tocando agora" lido uma vez;
  - o aviso da chave desligada na espera retirado;
  - a fonte com `WordWrap`.
- **Diferido:** a ligação do DI (casca, ficha e páginas), sem teste da raiz de composição.
- **Rejeitados:** com o motivo no Review Triage Log.

**Revisão de acompanhamento: não recomendada.** Foi corrigida uma entrada medium (o espelho do aviso), sem nenhum high.

**Verificação:**
- `dotnet test` Núcleo: 1365 de 1365. Rodou antes dos patches, que não tocam o núcleo.
- `dotnet test` Telas: 550 de 550, depois dos patches. Antes dos patches, uma rodada teve 1 falha, que não se repetiu em 5 rodadas seguidas. É um teste instável, não identificado.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- A matriz tem pelo menos um teste por linha:
  - o prazo e a chave nos avisos;
  - `TextoTocando`;
  - a folha na casca, o Salvar e o Fechar;
  - a espera, a troca e a escrita, que já vêm da 14b.

**[ASSUMPTION] para a 12e** (o UX não desenhou; conferir no A52):
- a espera como o anel de 18 px no chip, não uma tela;
- o prazo e a chave desligada também no aviso rápido;
- a escuta: os botões quadrados de 120 no mínimo, "tocando agora" embaixo do marcado e as três escolhas em coluna;
- a folha da ficha: um cartão por dado, o usado com contorno ciano, o recusado apagado e "Agora: valor (origem)".

**Riscos:**
- a ligação do DI (diferido);
- a conferência visual e a do TalkBack, que só dão no aparelho;
- o teste instável das Telas.
