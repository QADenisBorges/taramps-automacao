---
title: 'História 13g: telas do Rendimento'
type: 'feature'
created: '2026-11-01'
baseline_revision: '3a5c30869db77aea3f490fbce282b558a46c78ab'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-rendimento/telas-rendimento.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/EXPERIENCE.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** O Rendimento (13a a 13f) está no núcleo e nos ViewModels, mas nenhuma tela o mostra. O usuário não vê o limite da saída e não consegue calibrar, escolher o nível, otimizar a playlist, rodar o teste a seco nem acompanhar o Extra. A spec do Rendimento lista essas telas em `telas-rendimento.md`.

**Approach:** XAML para cada linha de `telas-rendimento.md`, ligado aos ViewModels que já existem:
- `CalibracaoViewModel`, `NivelViewModel`, `OtimizarViewModel`, `TesteSecoViewModel`, `ExtraAoVivoViewModel` e `PlayerViewModel`;
- um `LimiteSaidaViewModel` novo para o limite da saída e a conta.

As telas seguem o visual BGS Sound do UX de 30/10. Cada lugar que `telas-rendimento.md` marca como [ASSUMPTION] fica marcado no XAML e na spec, para a 12d conferir.

## Boundaries & Constraints

**Always:**
- **Limite da saída** (`AjusteSaidaPage`, perto do limitador) [ASSUMPTION: o lugar]:
  - uma linha com o teto do nível em uso, em V e em dB, o que manda (térmico, curso do cone ou módulo) e o selo ("verificado" ou "estimado");
  - um toque abre a folha "A conta", com os três limites, cada um com o valor, a conta, os dados usados com a origem e o dado que falta;
  - sem calibração, a faixa "Sem calibração: o app não sabe a tensão" com "Calibrar ›";
  - sem limite, o motivo.
  
  O `LimiteSaidaViewModel` (novo, em Telas) lê `GerenteSetups.LimitesEmUso`/`ContaEmUso` e refaz a cada mudança.
- **Recusa da cerca:** o motivo aparece onde a escrita foi pedida, no aviso do controle que já existe, e também no aviso rápido de 2,6 s da casca. Nada é escrito.
- **Calibração guiada** (`CalibracaoPage`, nova, tela cheia, um passo por tela):
  - os passos, os textos e os botões do `CalibracaoViewModel` (13b): escolher a saída, "Desligue os alto-falantes", o multímetro, o tom com a contagem, o campo de volts com teclado numérico e vírgula, o Bluetooth, o fim, "Tocar de novo", "Medir o Bluetooth de novo" e Cancelar;
  - entra por Configurações › "Calibrar saídas" e pelo "Calibrar ›" das faixas de sem calibração [ASSUMPTION: o caminho "Ajustes › Medir › Calibrar" vira Configurações até a Roda existir];
  - a tecla Voltar do Android segue o Cancelar.
- **Nível da memória** (folha que sobe do pé, `NivelFolhaView`):
  - um chip "Nível: {nome} ›" (ou "Sem nível ›") no cartão da memória em uso do Início e da `MemoriasPage` abre a folha [ASSUMPTION: o lugar, até a Roda e o Salvar];
  - uma linha por nível, com o teto e a fonte ("Alto · até 16 V nas JBL L e JBL R · qualquer fonte");
  - a linha apagada mostra o motivo;
  - o Extra vem com o selo "estimado" quando houver;
  - "Sem nível";
  - o selo "revalidar", com o motivo.
- **Aviso do Máximo:** a folha de risco do `NivelViewModel` ("Passa da potência da ficha. Seguro com música; um tom contínuo ou música muito comprimida por muito tempo podem aquecer a bobina."), com Confirmar e Cancelar, no visual da folha de risco do app.
- **Otimizar na playlist** (`PlayerView`, na playlist da memória):
  - o botão "Otimizar configuração na playlist";
  - durante a análise, o anel duplo sem números;
  - depois, o resultado ("Teto do Extra: … Falta o teste a seco."), com "Fazer o teste a seco ›";
  - o motivo quando não há resultado;
  - o Cancelar.
- **Teste a seco** (`TesteSecoPage`, nova, tela cheia, um passo por tela):
  1. "O módulo vai ser desligado.", com Confirmar e Cancelar;
  2. os trechos tocando ("Faixa n de N: nome, trecho 1|2"), com o anel e o Cancelar;
  3. o resultado (o teto validado, ou o clip na faixa e saída), com Concluir.
  
  A tecla Voltar faz o Cancelar no passo 2 e fecha nos passos 1 e 3.
- **Quem está pesando** (`PlayerView`, em cada faixa): o texto do peso ("−3,0 dB no teto"), "Em análise", a falha e a marca de clip ("Clip na Sub aos 42,3 s"). Uma faixa em análise fica apagada.
- **Extra ao vivo** (no topo do `PlayerView` e no cartão do Início) [ASSUMPTION: o mini player e a Roda]:
  - "Extra: mm:ss", "Extra: sem limite nesta faixa", "Descansando: mm:ss" e "Bluetooth travado";
  - o motivo sem modelo;
  - some fora do Extra.
- **Outro player:** a folha "Outro app começou a tocar. O som foi mutado.", com "Voltar ao player" (`PlayerViewModel.FolhaOutroPlayer`/`VoltarAoPlayer`); o aviso do volume no aviso rápido.
- **Visual:**
  - os tokens do `App.xaml` (o que faltar do DESIGN.md entra lá);
  - o anel duplo (`AnelDuploView`) em todo carregamento;
  - as folhas que sobem do pé no visual BGS;
  - os alvos de toque ≥ 48;
  - `SemanticProperties` nos botões só com ícone e nos anéis;
  - `x:DataType` em toda raiz e `DataTemplate`, e nenhuma cor solta (as regras do `ArquiteturaTestes`).
  
  Nenhum texto visível contém "IA".
- Os textos fixos saem dos ViewModels (constantes), não do XAML, como no resto do app.

**Never:**
- Mudar a regra do núcleo (13a a 13f).
- A Roda e o redesenho das outras telas.
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Limite na saída | JBL L calibrada, corte 200 Hz, sem nível | `LimiteSaidaViewModel`: "17,9 V · −6,8 dB · pelo térmico · verificado"; a conta com T, X, C e as origens | Sem calibração: a faixa e "Calibrar ›" |
| Calibrar › | Toque na faixa | Navega para a `CalibracaoPage` com a saída escolhida | — |
| Nível | Chip "Nível" no Início | A folha abre com as linhas do `NivelViewModel` | Máximo: a folha de risco antes |
| Otimizar → teste | Análise pronta | "Fazer o teste a seco ›" navega para a `TesteSecoPage` | — |
| Voltar no teste | Tecla Voltar no passo 2 | Cancela (`TesteSecoViewModel.Cancelar`) | — |
| Extra ao vivo | Extra tocando | O topo do player mostra o tempo | Descanso: "Descansando: …" |
| Arquitetura | Todos os XAML novos | `ArquiteturaTestes` passa (x:DataType, cores, sem "IA") | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- ViewModels prontos (13b a 13f):
  - `src/BgsControle.Telas/CalibracaoViewModel.cs`;
  - `NivelViewModel.cs` (as linhas, o Escolher, o `RiscoAberto`/Confirmar/Cancelar, o `EscolherExtra` e o selo revalidar);
  - `OtimizarViewModel.cs`;
  - `TesteSecoViewModel.cs`;
  - `ExtraAoVivoViewModel.cs`;
  - `PlayerViewModel.cs` (`LinhaPlaylistViewModel.TextoAnalise`/`TextoMarca`/`EmAnalise`, `FolhaOutroPlayer`, `VoltarAoPlayer` e o aviso);
  - `AjusteSaidaViewModel.cs` (`AvisoCalibracao`).
- Núcleo para o `LimiteSaidaViewModel`: `GerenteSetups.LimitesEmUso`/`ContaEmUso`/`NivelEmUso` e `Rendimento/LimiteFisico.cs` (`LimiteSaida`, `LimiteCanal`, os limites com `V`, `Conta`, `Selo`, `Falta`).
- App:
  - `src/BgsControle.App/Telas/AjusteSaidaPage.xaml`, `PlayerView.xaml`, `InicioView.xaml`, `MemoriasPage.xaml`, `ConfiguracoesPage.xaml` e `CriacaoPage.xaml` (o modelo de tela cheia com passos, a barra do fluxo e o anel);
  - `Controles/AnelDuploView.cs`, `FolhaView.cs` e `AvisoRapidoView.cs`;
  - `FolhaRiscoPage.xaml` (o visual da folha de risco);
  - `App.xaml` (os tokens), `NavegacaoMaui.cs` (`Empurrar`) e `MauiProgram.cs`.
- `src/BgsControle.Telas/Abstracoes.cs` (`INavegacao`: os destinos novos) e `tests/BgsControle.Telas.Testes/Falsos.cs` (`NavegacaoFalsa`).
- `tests/BgsControle.Telas.Testes/ArquiteturaTestes.cs`: as regras do XAML e do "IA".

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Telas/LimiteSaidaViewModel.cs` (novo): a linha do limite, a folha da conta e a faixa de sem calibração com o comando "Calibrar ›".
- `src/BgsControle.Telas/Abstracoes.cs` e `Falsos.cs`: os destinos `Calibracao(saida?)` e `TesteSeco`, e os comandos de navegar nos ViewModels (o "Calibrar ›", o "Fazer o teste a seco ›" e o "Calibrar saídas").
- `src/BgsControle.App/Telas/CalibracaoPage.xaml(.cs)` e `TesteSecoPage.xaml(.cs)` (novas).
- `src/BgsControle.App/Controles/NivelFolhaView.xaml` (novo).
- `AjusteSaidaPage.xaml`, `PlayerView.xaml`, `InicioView.xaml`, `MemoriasPage.xaml` e `ConfiguracoesPage.xaml`: as partes do Rendimento.
- `App.xaml`, `NavegacaoMaui.cs` e `MauiProgram.cs`: os tokens, as rotas e o registro.
- Testes:
  - `tests/BgsControle.Telas.Testes/LimiteSaidaViewModelTestes.cs`;
  - a navegação dos comandos novos (`NavegacaoFalsa`);
  - o `ArquiteturaTestes`, que já varre os XAML novos.

**Acceptance Criteria:**
- Given o app no build Android, when as telas novas compilam (XamlC com os bindings conferidos), then o build sai sem erro nem aviso, e o `ArquiteturaTestes` passa.
- Given a JBL L calibrada, when o usuário abre o ajuste da saída, then o `LimiteSaidaViewModel` mostra o teto, o que manda e o selo, e a conta tem os três limites com as origens.
- Given os testes que já existem, when a 13g entra, then todos passam.

## Spec Change Log

- **O teto da linha do limite** é o da cerca no nível em uso (o físico: min(T, X, C); no Máximo e no Extra, min(X, C)), como o limitador ao lado. Com um nível, uma linha de apoio diz até onde o nível leva o pico, pela `ContaEmUso` ("Nível Alto: até 16 V."). Leitura de "o teto do nível em uso"; a 12d confere.
- **A folha BGS** é uma variante da `FolhaView` (`Bgs="True"`: surface-sheet, fio border-sheet, raio 22, pegador 36 × 4, véu a 60 %). As folhas antigas ficam como estavam (nada de redesenho das outras telas).
- **O aviso rápido** também aparece no Ajuste da saída e nas telas de função (a cerca recusa ali, por cima da casca), espelhando o da casca, como a `MemoriasPage` já fazia.
- **A recusa da cerca** é reconhecida na tela pelo começo "Limite: " (e pela `Cerca.MsgFalhou`), em `ControleParametroViewModel.DaCerca`; um teste amarra o prefixo às mensagens da `Cerca`. A `FabricaControles` avisa a casca (`RecusouNaCerca`).
- **O "Calibrar ›" da folha do nível** abre a calibração da primeira saída sem calibração.

## Review Triage Log

## Design Notes

- **Por que Configurações e o cartão da memória:** a Roda (o redesenho) ainda não existe. Esses lugares são [ASSUMPTION] de `telas-rendimento.md`, para a 12d conferir no aparelho.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- Sem celular no adb: a conferência visual de cada tela fica para a 12d (registrar no Auto Run Result).

## Auto Run Result

Status: done

**O que entrou** (em `app-controle/`):
- **Telas:**
  - `LimiteSaidaViewModel.cs` (novo): a linha do teto ("17,9 V · −6,8 dB · pelo térmico · verificado"), a faixa de sem calibração com "Calibrar ›", o motivo sem limite, a linha do nível e a folha "A conta" (T, X e C com o valor, o selo, a conta, os dados com a origem e o que falta; o alto-falante que manda e o ganho do módulo). O `AjusteSaidaViewModel` leva um (`Limite`).
  - `INavegacao`: `AbrirCalibracao(canal?)` e `AbrirTesteSeco()`; os comandos "Calibrar saídas" (Configurações), "Calibrar ›" (limite e folha do nível) e "Fazer o teste a seco ›" (`OtimizarViewModel`).
  - `NivelViewModel`: o chip ("Nível: Alto ›" / "Sem nível ›"), a faixa de sem calibração, os itens com o toque e o Fechar. `PlayerViewModel` e `InicioViewModel`: o Extra ao vivo e o Otimizar. `MemoriasViewModel`: o nível no cartão.
  - Os passos de tela (`NaEscolha`, `NoAviso`, `Andando`…) e as descrições do TalkBack em `CalibracaoViewModel` e `TesteSecoViewModel`.
  - A recusa da cerca e o aviso do volume do Extra no aviso rápido da casca.
- **App:**
  - `CalibracaoPage` e `TesteSecoPage` (novas, tela cheia, um passo por tela, Voltar como pedido);
  - `NivelFolhaView` (com a folha de risco do Máximo), `ChipNivelView`, `ExtraAoVivoView`, `LimiteSaidaView` e `ContaLimiteView` (novos);
  - as partes do Rendimento em `AjusteSaidaPage`, `PlayerView`, `InicioView`, `MemoriasPage`, `ConfiguracoesPage` e `CascaPage`;
  - `App.xaml` (os tokens que faltavam e os estilos BGS com alvo de 48), `FolhaView` (o visual BGS), `NavegacaoMaui` e `MauiProgram`.
- **Testes:** `LimiteSaidaViewModelTestes` (novo), o "Fazer o teste a seco" no `OtimizarViewModelTestes`, a `NavegacaoFalsa` e o `ArquiteturaTestes` (as telas novas na varredura).

**Verificação.**
- `dotnet build src/BgsControle.App -f net10.0-android -c Debug`: 0 erros e 0 avisos (XamlC conferindo os bindings).
- `dotnet test tests/BgsControle.Telas.Testes`: 517 passam. `dotnet test tests/BgsControle.Nucleo.Testes`: 1338 passam.

**Fica para a 12d.** Sem celular no adb: a conferência visual de cada tela (os lugares [ASSUMPTION], os tamanhos e as folhas) no aparelho. Sem revisão de código nesta rodada.

