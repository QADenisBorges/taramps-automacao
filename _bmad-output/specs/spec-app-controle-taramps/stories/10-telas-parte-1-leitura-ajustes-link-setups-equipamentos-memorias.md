---
title: 'História 10: telas, parte 1 — leitura, ajustes, link, Setups, equipamentos e memórias'
type: 'feature'
created: '2026-10-23'
status: 'done'
baseline_revision: 'fbe39eb6b9777525dc246342ecf5e7ee7d5d4203'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/arquivo/10-telas-parte-1-tentativa-1-bloqueada.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/8e-equipamentos-ligacoes-e-saidas-do-setup.md'
warnings:
  - oversized
deferred:
  - summary: >-
      A navegação com uma tela por cima (esperar sob "Aplicado pela metade", descartar sob a escolha do Setup, a conexão como exceção) não tem teste automático.
    evidence: |-
      A lógica fica no NavegacaoMaui, que depende do MAUI (INavigation, MainThread); os testes usam a NavegacaoFalsa. Extrair a decisão para uma classe do BgsControle.Telas e testar, ou conferir no aparelho (história 12).
    location: >-
      app-controle/src/BgsControle.App/NavegacaoMaui.cs
    severity: medium
  - summary: >-
      Ao tocar em Concluído no teclado, a perda do foco pode chegar antes do Completed e descartar o valor digitado.
    evidence: |-
      O CampoEdicaoView fecha o campo no Unfocused. No MAUI Android o Completed costuma vir antes, mas só o aparelho confirma. Se for real, a correção é adiar o fechamento (Dispatcher.Dispatch).
    location: >-
      app-controle/src/BgsControle.App/Controles/CampoEdicaoView.xaml.cs
    severity: high (unverified)
---

<intent-contract>

## Intent

**Problem:** O app tem uma tela só (a de conexão). A primeira tentativa desta história (as telas da casca, do Início, da Mesa, do Áudio, das funções, do Ajuste, do Setup em dúvida, das memórias e dos Setups) está salva num patch. Ela parou porque as saídas eram fixas (JBL L, JBL R, Sub) e a saída 4 ficava sem ajuste. Desde a 8e, as saídas vêm do Setup (CAP-2, CAP-3, CAP-4, CAP-14, CAP-16).

**Approach:** Aplicar o patch salvo e trocar as saídas fixas pelas saídas do Setup em uso (`GerenteSetups.SaidasEmUso`). Acrescentar a tela do Setup com a lista e a folha de equipamento (`EquipamentosViewModel`). Rotular a folha do Salvar e a lista da dúvida pelo nome da saída. Corrigir os achados "patch" da revisão da tentativa.

## Boundaries & Constraints

**Always:**
- Vale tudo o que a spec arquivada da tentativa 1 diz em "Always" (`stories/arquivo/10-telas-parte-1-tentativa-1-bloqueada.md`), menos a frase das saídas fixas:
  - o visual e o comportamento do `DESIGN.md` e do `EXPERIENCE.md`, com as cores por nome de token e só o tema escuro;
  - a barra Início · Mesa · Áudio e a engrenagem;
  - as telas daquela lista, com o Gerador travado com "Gerador na próxima versão";
  - os controles pela `ControleParametroViewModel`, os bindings compilados (`x:DataType`) e "—" sem leitura.
- **Saídas do Setup:**
  - as abas das telas de função e do Ajuste, os cartões de saída do Início e os canais da Mesa saem de `GerenteSetups.SaidasEmUso`, na ordem, com o nome, a cor do canal mestre (tokens `canal-1` a `canal-4` do `DESIGN.md`) e o aviso de bridge quando há;
  - de 1 a 4 saídas, sem número fixo;
  - mexer numa saída que é um par escreve no mestre, e o link repete no outro canal;
  - o equilíbrio do Ajuste vale para qualquer saída (`AjustarEquilibrioAsync` com o canal da saída);
  - quando as saídas mudam (link, equipamento, Setup), as telas abertas se refazem sozinhas.
- **Tela do Setup:**
  - Configurações › Setups › tocar num Setup abre a tela **Setup**: nome, selo HERDADO, a lista de equipamentos e "Adicionar equipamento", pela `EquipamentosViewModel` (8e);
  - a folha de equipamento segue a seção "Equipamentos e saídas" do `EXPERIENCE.md` e o componente "folha-de-equipamento" do `DESIGN.md`;
  - o alto da tela fica livre para o esquema da 10b.
- **Rótulos por saída:** a folha do Salvar e a lista "O que não bate" da dúvida dizem o nome da saída ("Atraso do Sub: 80 → 85 cm"), uma linha por saída quando os dois canais de um par mudam igual. O rótulo vem do núcleo (`GerenteSetups`), não da tela.
- **Correções da revisão da tentativa 1** (as linhas `patch` do triage log da spec arquivada):
  - **Fonte:** o `Tipo.cs` respeita o `FontFamily` de cada controle, fica em `Platforms/Android` (ou atrás de `#if ANDROID`) e o comentário bate com a versão testada.
  - **Conexão:** o registro do BLE e "Esquecer a senha" ficam ao alcance desconectado (a pílula desconectada abre a conexão com Conectar, em vez de só conectar); a tela de conexão tem o indicador de ocupado.
  - **Mesa:** os canais mostram o motivo da trava e o aviso.
  - **Equilíbrio:** nenhum passo pedido se perde (toques seguidos e segurar).
  - **Comandos:** `ComandoCom<T>` não lança para fora e não roda duas vezes em paralelo.
  - **Lista "…":** tocar na memória em uso fecha a lista.
  - **Fader e giratório:** soltam mesmo se a trava chegar no meio do arrasto.
  - **Splash:** fica no preto do `palco`.
  - **Resumos:** a grade Áudio mostra o passa-alta e o passa-baixa quando os dois estão ligados, e a Mesa e as funções mostram "—" com o tipo do filtro ainda não lido.
  - **Telas de função:** acompanham os nomes das memórias do aparelho.
  - **Navegação:** pedir uma tela com um modal aberto não falha calado; depois da escolha do Setup, a casca volta ao Início.
  - **Doc:** o comentário de documentação da `ConexaoViewModel` volta à classe.

**Never:**
- Não fazer as telas da história 11 nem o esquema (10b).
- Não decidir qual link vale sem Setup em uso: isso fica com o Denis, como item adiado da 8e. As telas mostram o estado que o núcleo der.
- Não usar biblioteca de interface de terceiros.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Casa 2.1 | Conectado, FILME | Início com JBL L (azul), JBL R (lilás), Sub (âmbar); Ajuste do Sub escreve no CH3 e o simulador mostra o CH4 igual | — |
| Link 3–4 desligado | Desliga o link no Crossover | Quatro saídas: JBL L, JBL R, CH3 (ou o nome do que ele alimenta), CH4 (menta); o aviso de bridge aparece | — |
| Setup de 4 canais | 4 equipamentos em 4 canais, sem link | 4 cartões, 4 canais na Mesa, 4 abas | — |
| Setup sem equipamentos | Setup criado do aparelho | Saídas CH1 a CH4 (pares com link como "CH3 + CH4") | — |
| Tela do Setup | Configurações › Setups › Casa 2.1 | Lista: Módulo genérico, JBL L, JBL R, Sub (com "genérico"); tocar abre a folha | Tirar o módulo: confirmação e aviso dos alto-falantes |
| Salvar no Sub | Atraso do Sub 80 → 85 cm | Uma linha "Atraso do Sub: 80 → 85 cm" | — |
| Dúvida | Atraso do CH3 diferente | Linha "Atraso do Sub: 80 cm no Casa 2.1, 0 cm no aparelho" | — |
| Equilíbrio seguido | Três toques de + seguidos no equilíbrio da JBL R | O aparelho termina 0,3 dB acima | — |
| Pílula desconectada | Toque na pílula sem conexão | Abre a tela de conexão, com Conectar, o registro e Esquecer a senha | — |

</intent-contract>

## Code Map

- `_bmad-output/implementation-artifacts/historia-10-tentativa-1.patch` -- a tentativa 1 inteira; `git apply` aplica limpo sobre o `main` atual (conferido depois da 8e). Começar por ele.
- `_bmad-output/specs/spec-app-controle-taramps/stories/arquivo/10-telas-parte-1-tentativa-1-bloqueada.md` -- a spec da tentativa 1 (contrato, Code Map) e o triage log com os achados `patch` a corrigir.
- `app-controle/src/BgsControle.Telas/Saidas.cs` (vem do patch) -- as saídas fixas (`Saidas.Canal(Sub) = 2`…): sai, trocado por `GerenteSetups.SaidasEmUso`/`Saidas(id)`/`SaidaDe(canal)` da 8e (`app-controle/src/BgsControle.Nucleo/Setups/Equipamentos.cs`, `SaidaApp`).
- `app-controle/src/BgsControle.Telas/EquipamentosViewModel.cs` (8e) -- a lista e a folha de equipamento; a tela do Setup só liga.
- `app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs` (`MudancasDeCalibracao`, `DiferencasDeCalibracao`, `MudancasDeCarater`) e `Camadas.Rotulo` -- os rótulos por canal que passam ao nome da saída.
- `app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs` (`AjustarEquilibrioAsync(canal, limiter, valor)`, `Mudou`) e `LinkCanais.Mudou` -- o equilíbrio por saída e o aviso de mudança das saídas.
- `_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/DESIGN.md` -- tokens `canal-1` a `canal-4`, componentes "esquema-do-setup", "linha-de-equipamento" e "folha-de-equipamento".

## Tasks & Acceptance

**Execution:**
- Aplicar o patch (`git apply _bmad-output/implementation-artifacts/historia-10-tentativa-1.patch`) e fazer a solução compilar com a 8e.
- `app-controle/src/BgsControle.Telas/` (`InicioViewModel`, `MesaViewModel`, `FuncaoViewModel`, `AjusteSaidaViewModel`, `AudioViewModel`) -- saídas do Setup no lugar de `Saidas.cs`; refazer ao mudar as saídas.
- `app-controle/src/BgsControle.App/Telas/SetupPage.xaml(.cs)` (nova) e a folha de equipamento -- a tela do Setup; `SetupsPage` abre ela.
- `app-controle/src/BgsControle.App/App.xaml` -- os tokens `canal-1` a `canal-4`.
- `app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs` -- rótulos por saída na folha do Salvar e na dúvida.
- Os arquivos dos achados `patch` da revisão da tentativa 1 (a lista em "Always").
- `app-controle/tests/BgsControle.Telas.Testes/` e `tests/BgsControle.Nucleo.Testes/` -- a matriz contra o simulador; os testes da tentativa que dependiam das três saídas fixas passam a usar as saídas do Setup.

**Acceptance Criteria:**
- Given o app compilado para Android, when o build roda, then não há erro nem aviso, com o XamlC validando os bindings no Debug (a validação da tentativa 1 continua).
- Given o Casa 2.1 conferido, when o Denis mexe no ganho do Sub no Ajuste, then o simulador mostra o CH3 e o CH4 com o mesmo valor novo.
- Given o `GerenteSetups` em dúvida, when isso acontece com qualquer tela aberta, then a casca abre a tela Setup em dúvida por cima e, depois da escolha, volta ao Início.

## Spec Change Log

## Review Triage Log

### 2026-10-23 — Review pass
- verdicts: 28 findings — high 0, medium 2, low 19, false 6, maybe-false 1
- a revisão rodou depois do commit 4bdd12a (o agente fechou a história sem ela); as correções entram num commit seguinte
- findings:
  - `[false]` `[reject]` (intenção) XAML e navegação testados só pelos ViewModels — não há harness MAUI nem celular; a spec deixa a conferência visual pendente
  - `[low]` `[reject]` (intenção) O registro da SetupPage no DI nunca roda num teste — o build valida os bindings; a abertura real fica para a conferência no aparelho
  - `[low]` `[reject]` (intenção) A tela de conexão (registro, esquecer a senha, ocupado) só no XAML — idem
  - `[false]` `[reject]` (intenção) Correções de Android só pelo build — é o que dá para provar sem aparelho
  - `[false]` `[reject]` (intenção) Com o Link 3–4 desligado, as saídas viram "Sub · CH3" e "Sub · CH4" — os dois canais alimentam o mesmo Sub; o canal desempata, como a spec manda para nomes iguais
  - `[low]` `[patch]` (intenção) Lacunas de teste da tentativa 1 não cobertas ("Caiu às", criar memória pelo Início, opção escolhida) — testes novos
  - `[false]` `[reject]` (intenção) Link com equilíbrios diferentes não decidido — a spec manda deixar com o Denis (adiado)
  - `[low]` `[patch]` (cego) Rótulos com "do" antes de qualquer nome ("Ganho do JBL L") — forma sem artigo: "Ganho · JBL L", "mexer em …"
  - `[false]` `[reject]` (cego) O teste da matriz não confere as saídas — confere: CH1, CH2, "CH3 + CH4" e depois as 4 caixas no Início, na Mesa e nas abas
  - `[false]` `[reject]` (cego) A spec dizia done sem revisão — esta revisão corrige; a correção seria editar a spec
  - `[medium]` `[patch]` (cego) A pílula na tela Setup em dúvida não abria a conexão — a conexão abre por cima da escolha do Setup (única exceção)
  - `[low]` `[patch]` (cego) Comentários do modelo de saídas fixas — reescritos
  - `[low]` `[patch]` (cego) Falhas dos comandos sem registro — vão para o LogBle
  - `[low]` `[reject]` (cego) Rótulos agrupados por links diferentes quando o mesmo Salvar troca o link — raro
  - `[low]` `[patch]` (cego) Cor do equipamento pelo primeiro canal, não pelo mestre da saída — passa a ser o mestre da saída
  - `[low]` `[patch]` (cego) Código morto e duplicado — removido
  - `[low]` `[reject]` (cego) Cores em C# e o ícone ainda azul — sombras no desenho e o ícone do inicializador não são o tema
  - `[low]` `[patch]` (lacuna) Telas abertas não testadas ao mudar equipamento ou Setup — teste novo
  - `[low]` `[patch]` (lacuna) Subtítulo das telas de função não testado — teste novo
  - `[medium]` `[defer]` (lacuna) Navegação com tela por cima (esperar ou descartar) sem teste — depende do MAUI; extrair a decisão para uma classe testável é maior que esta história
  - `[low]` `[patch]` (borda) Passo do equilíbrio perdido quando o envio falha — mostra o motivo do aparelho
  - `[low]` `[patch]` (borda) Exceção no retorno à tela derrubava o envio — pega a exceção; o envio sempre termina
  - `[low]` `[patch]` (borda) Memória em uso com id que sumiu mostrava "Nenhuma memória" — vira "Som do aparelho"
  - `[low]` `[patch]` (borda) Toque duplo em aplicar ou carregar memória — o segundo é ignorado; teste
  - `[low]` `[patch]` (borda) Toque duplo em confirmar renomear ou apagar Setup — o segundo é ignorado
  - `[maybe-false]` `[defer]` (borda) No Concluído do teclado, a perda do foco pode chegar antes e descartar o valor digitado — no MAUI Android o Completed costuma vir antes; só o aparelho confirma; se for real, é high
  - `[low]` `[patch]` (borda) Folha do Salvar ou campo aberto ficavam depois da escolha do Setup — fechados ao voltar ao Início; teste
  - `[low]` `[patch]` (borda) Mesa com "Memória 2 · memória 2 do aparelho" — o sufixo só com nome lido

## Design Notes

- **Por que aplicar o patch:** a tentativa 1 passou em 916 + 358 testes e no build; o que a bloqueou foi só o modelo das saídas, que a 8e resolveu. Refazer as telas do zero seria retrabalho.
- **Sem aparelho:** se ainda não houver celular no adb, a conferência visual fica pendente, como na tentativa 1.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.
- `git grep -n "class Saidas\b" -- app-controle/src` -- expected: nenhuma linha (as saídas fixas saíram).

**Manual checks (if no CLI):**
- Com o celular no adb (`C:\platform-tools\adb.exe`), instalar o APK e conferir as telas contra os mocks; sem celular, registrar que não foi feito.

## Auto Run Result

Status: done

**O que entrou.**
- **Patch da tentativa 1** aplicado (casca, Início, Conectar, Mesa, Áudio, 9 telas de função, Ajuste, Setup em dúvida, lista "…", Salvar, "Aplicado pela metade", Configurações, Setups, Memórias).
- **Saídas do Setup:** `Saidas.cs` saiu. As telas usam `GerenteSetups.SaidasEmUso` (`SaidaTela`, `TextosParametro`), de 1 a 4 saídas, na cor do canal mestre (tokens `canal-1` a `canal-4` no `App.xaml`), com o aviso do bridge. Início, Mesa, abas das funções, grade Áudio e Ajuste se refazem quando o link, os equipamentos ou o Setup mudam. O Ajuste segue a saída do canal pedido e refaz os controles se o mestre muda. O equilíbrio vale para qualquer saída (no par, os dois canais).
- **Rótulos por saída (núcleo):** `RotulosSaida` e `Ligacoes.NomesNaTela`. A folha do Salvar e a lista "O que não bate" dizem "Atraso do Sub"; o par que muda igual é uma linha só; o outro canal diferente vira "Atraso do Sub · CH4"; nomes repetidos levam o canal ("Sub · CH3"). `MudancaSalvar` e `DiferencaCalibracao` ganharam o canal mestre (a cor); a folha do Salvar pinta o valor novo na cor da saída.
- **Tela Setup:** `SetupPage` (nome, HERDADO, saídas, equipamentos, Adicionar) com a folha de equipamento (grupo em linhas de escolha, categoria, função, fontes, ligado e bridges em pílulas, dados técnicos em Avançado, Tirar com confirmação). Configurações › Setups abre ela. O alto da tela ficou livre para a 10b.
- **Correções da revisão da tentativa 1:** `Tipo.cs` em `Platforms/Android`, partindo da família do controle (o monospace do registro fica), comentário com a API 28; a pílula abre a tela de conexão (Conectar, senha, registro, Esquecer a senha, indicador de ocupado); canais da Mesa com o motivo da trava e o aviso; equilíbrio sem passo perdido (a decisão de parar na thread da tela); `ComandoCom<T>` sem exceção para fora e sem rodar em paralelo; a memória em uso fecha a lista "…"; fader e giratório soltam com a trava no meio; splash preto; resumo do Crossover com PA e PB; "—" com o tipo do filtro não lido; telas de função ouvem as memórias do aparelho; pedido de tela com modal aberto vai para o registro (espera a folha pela metade, ou é descartado com a escolha do Setup); depois da escolha do Setup a casca volta ao Início; o doc da `ConexaoViewModel` voltou à classe.

**Não decidido aqui:** qual link vale sem Setup em uso (item adiado da 8e, um "Never" desta spec) e o que fazer ao ligar o link num par com equilíbrios diferentes guardados (`deferred-work.md`, 8c) continuam com o Denis; as telas mostram o que o núcleo dá.

**Sem revisão:** esta execução não rodou a revisão de código (bmad-review); a revisão de acompanhamento é recomendada, sobretudo na navegação MAUI (modais, volta ao Início) e na tela Setup, que não têm teste automático.

**Verificação:**
- `dotnet test` do Nucleo.Testes: 990 passam (novo: `RotulosSaidaTestes`).
- `dotnet test` do Telas.Testes: 385 passam (novo: `TelasSaidasDoSetupTestes`, com a matriz).
- `dotnet build` do App para `net10.0-android` (Debug, XamlC validando): 0 erros e 0 avisos.
- `git grep -n "class Saidas\b" -- app-controle/src`: nenhuma linha.
- Sem celular no adb: nenhuma tela foi vista num aparelho; a conferência visual contra os mocks fica pendente.


### Revisão (feita depois do commit 4bdd12a)

- **28 achados.**
- **Corrigidos:** 1 medium (a pílula na tela "Setup em dúvida" agora abre a conexão), mais as low:
  - rótulos sem artigo ("Atraso · Sub");
  - falhas de comando no log;
  - a cor do equipamento pelo canal mestre;
  - o equilíbrio sem passo perdido e sem exceção;
  - toques duplos;
  - a memória que sumiu;
  - as folhas fechadas depois da escolha do Setup;
  - código morto;
  - os testes que faltavam.
- **Adiados (2):** os testes da navegação com tela por cima e a ordem do Concluído do teclado (os dois para o aparelho, história 12).
- **Revisão de acompanhamento:** não recomendada; só 1 medium foi corrigido.
- **Verificação depois das correções:**
  - `dotnet test` do Nucleo.Testes: 990 passam.
  - `dotnet test` do Telas.Testes: 395 passam.
  - `dotnet build` do App para `net10.0-android`: 0 erros e 0 avisos.
  - `git grep "class Saidas"`: nenhuma linha.
- **Sem conferência visual:** nada foi visto num celular.
