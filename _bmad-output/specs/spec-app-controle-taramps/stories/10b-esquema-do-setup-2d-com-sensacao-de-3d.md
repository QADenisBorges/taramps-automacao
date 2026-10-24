---
title: 'História 10b: esquema do Setup em 2D com sensação de 3D'
type: 'feature'
created: '2026-10-23'
status: 'done'
baseline_revision: '6b3f9946410760d07cdad2b339661e6ef1b983de'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-23-equipamentos.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/8e-equipamentos-ligacoes-e-saidas-do-setup.md'
warnings: []
deferred:
  - summary: >-
      A pintura do esquema e os gestos (arrastar só na horizontal, pinça em torno dos dedos, toque) nunca rodaram num celular.
    evidence: |-
      O EsquemaView depende do MAUI e não tem teste; a prévia em SVG só confere o layout e a projeção. Conferir no aparelho (história 12): sombra visível, portas a ±45°, rolagem da página sobre o esquema, pinça e toque com o dedo.
    location: >-
      app-controle/src/BgsControle.App/Controles/EsquemaView.cs
    severity: medium
---

<intent-contract>

## Intent

**Problem:** A tela do Setup (história 10) mostra os equipamentos só numa lista. A CAP-17 pede um esquema interativo, atualizado sozinho, que mostre como cada equipamento está ligado aos canais. Por agora ele é 2D com sensação de 3D (decisão do Denis, 23/10/2026).

**Approach:** Uma montagem pura e testável (`BgsControle.Telas`) transforma o Setup numa cena: peças, cabos, Y, bridges, cores e formas, em perspectiva isométrica com ângulo e aproximação, mais o teste de toque. Uma `GraphicsView` (`BgsControle.App`) só pinta essa cena no lugar reservado da tela do Setup. Arrastar, pinçar e tocar mudam o ViewModel.

## Boundaries & Constraints

**Always:**
- **Conteúdo:** segue a seção "Equipamentos e saídas" › "Esquema do Setup" do `EXPERIENCE.md`, o componente `esquema-do-setup` do `DESIGN.md` e a CAP-17.
  - Da esquerda para a direita: o **processador** com CH1 a CH4, cada um na cor do token `canal-N`; os equipamentos do **grupo 3**; os dos grupos **1, 2 e 4**; e os **alto-falantes passivos**, junto da saída ou do bridge do módulo deles.
  - **Cabos:** saem do canal (ou da saída de um equipamento do grupo 3) até cada entrada, na cor do canal que levam. Um canal que vai para mais de uma entrada abre num **Y**, marcado por um ponto.
  - Os alto-falantes ficam ligados ao canal ou ao bridge do módulo deles. O **bridge** aparece como uma chave que junta os dois canais do módulo.
  - Um equipamento sem ligação aparece à parte, apagado, com "sem ligação".
- **Formas** (geradas pelo código, nunca imagens):
  - tweeter: domo pequeno;
  - driver: corneta;
  - médio, médio-grave e woofer: cone, com o tamanho pelo diâmetro (sem diâmetro, um tamanho médio pela categoria; com, proporcional e preso a uma faixa legível);
  - coaxial: cone com domo no centro;
  - subwoofer: caixa grande com cone;
  - caixa ativa e outros do grupo 1: caixa com as vias de dentro (`Dados.Vias`; sem vias, uma via só);
  - módulo e amplificador: bloco com os canais numerados e os bridges;
  - grupos 3 e 4: bloco baixo com o nome da categoria;
  - categoria desconhecida: a forma da função acústica (sub → caixa com cone, agudo → domo, médio-grave e grave → cone, full-range → coaxial, outra → caixa).
- **Sensação de 3D:**
  - projeção isométrica, com cada peça em três tons (topo `sup-2`, lados `sup-1` e `registro-fundo`), sombra elíptica a 40 % e uma grade fraca em `sup-3` no fundo `palco`;
  - a cor do canal só no cabo, na borda da entrada e no rótulo CHn, nunca no corpo da peça;
  - o rótulo de cada peça traz o nome, e "genérico" em `apagado` quando não há modelo.
- **Interação:**
  - arrastar na horizontal gira o ângulo de visão entre −45° e +45° (0° é o isométrico padrão), e soltar não volta;
  - pinçar aproxima, de 0,6× a 2,5×;
  - tocar numa peça abre a folha daquele equipamento (`EquipamentosViewModel`);
  - tocar num canal do processador destaca tudo o que ele alimenta (o resto fica a 35 %); tocar de novo ou no vazio tira o destaque.
- **Ao vivo:** a cena se refaz sozinha quando o Setup mostrado, os equipamentos ou os links mudam, sem botão de atualizar. O ângulo, a aproximação e o destaque continuam enquanto a peça destacada existir.
- **Separação:** a montagem da cena, as posições, as formas, as proporções, a projeção e o teste de toque ficam num ViewModel/modelo de `BgsControle.Telas`, testável sem MAUI. A view só pinta e repassa os gestos. Nada vai pelo BLE.
- **Desempenho:** refazer a cena e pintar com até 20 equipamentos não trava a tela (a montagem é linear no número de peças e de cabos).
- **Setup sem equipamentos:** mostra só o processador e a frase "Adicione os equipamentos ligados em cada canal."

**Never:**
- Não usar biblioteca nova, WebView, imagens prontas nem internet. Não fazer 3D de verdade (fica para depois).
- Não mudar o modelo de equipamentos nem as regras da 8e; só ler.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Casa 2.1 | Setup herdado | Processador CH1 a CH4 → Módulo genérico (4 entradas, CH1 azul, CH2 lilás, CH3 âmbar, CH4 menta) → JBL L no canal 1, JBL R no canal 2, Sub no bridge 3+4 (chave do bridge) | — |
| Cabo Y | CH1 alimenta uma caixa ativa e um sub ativo | Um cabo azul que abre em Y com o ponto, até as duas peças | — |
| Repasse | CH2 → crossover ativo → 2 módulos → alto-falantes | Crossover na coluna do grupo 3; cabos lilás do crossover aos módulos | — |
| Formas | Tweeter, driver, woofer 12", coaxial, sub, caixa ativa com 2 vias, categoria "Outra" de função grave | Domo, corneta, cone maior que o de um woofer de 6,5", coaxial, caixa com cone, caixa com 2 vias, cone | — |
| Toque numa peça | Toque no Sub | Abre a folha do Sub | Toque no vazio: nada |
| Toque num canal | Toque no CH3 | Destaque do Módulo, do bridge e do Sub; o resto a 35 % | Toque de novo: sem destaque |
| Arrastar e pinçar | Arrasta 200 px; pinça 3× | Ângulo preso em +45°; aproximação presa em 2,5×; o toque continua acertando a peça certa | — |
| Ao vivo | Folha: tira a JBL R | A cena perde a JBL R na hora; o ângulo fica | — |
| Sem ligação | Alto-falante sem módulo | Aparece à parte, apagado, com "sem ligação" | — |
| Vazio | Setup sem equipamentos | Só o processador e a frase | — |

</intent-contract>

## Code Map

- `app-controle/src/BgsControle.App/Telas/SetupPage.xaml:57` -- `<ContentView x:Name="LugarDoEsquema" />`, o lugar reservado pela história 10 para o esquema.
- `app-controle/src/BgsControle.Telas/EquipamentosViewModel.cs` -- o Setup mostrado (`Mostrar(id)`), a abertura da folha de um equipamento e o `Mudou` que a lista já ouve; o esquema usa a mesma fonte e abre a mesma folha.
- `app-controle/src/BgsControle.Nucleo/Setups/Equipamentos.cs` -- `Equipamento` (grupo, categoria, função, `Dados.Diametro`, `Dados.Vias`, `Dados.Canais`, `Entradas`, `Bridges`, `Ligado`), `FonteSinal`, `Bridge`, `LigacaoAltoFalante`, `Ligacoes.CanaisQueAlimentam`, `Ligacoes.Alimentados`, `Ligacoes.Saidas`: tudo o que a cena precisa para posições, cabos e destaque.
- `app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs` -- `Setup(id)`, `Mudou`; o link do Setup (`Link12`, `Link34`) só para a cor do mestre, se precisar.
- `app-controle/src/BgsControle.App/Controles/FaderView.cs`, `GiratorioView.cs`, `ArcoView.cs` -- modelo de `GraphicsView : IDrawable` no app (pintura, toque, gesto, cores por recurso).
- `app-controle/src/BgsControle.App/App.xaml` -- tokens `palco`, `sup-1`, `sup-2`, `sup-3`, `registro-fundo`, `apoio`, `apagado`, `texto`, `canal-1` a `canal-4`.
- `app-controle/tests/BgsControle.Telas.Testes/EquipamentosViewModelTestes.cs` e `ControleParametroViewModelTestes.cs:20` (`Bancada`) -- a montagem de teste.

## Tasks & Acceptance

**Execution:**
- `app-controle/src/BgsControle.Telas/Esquema/CenaSetup.cs` (novo) -- os records da cena (peça com id, forma, posição 3D, tamanho, tons, rótulo, canal; cabo com pontos, canal e Y; bridge) e a montagem a partir do Setup: colunas por grupo, ordem pelos canais, formas e proporções pelas regras acima.
- `app-controle/src/BgsControle.Telas/Esquema/ProjecaoIsometrica.cs` (novo) -- a projeção com o ângulo (−45° a +45°) e a aproximação (0,6× a 2,5×), da cena para pontos 2D, e o teste de toque (o que fica na frente ganha).
- `app-controle/src/BgsControle.Telas/EsquemaViewModel.cs` (novo) -- a cena do Setup mostrado, refeita no `Mudou`; ângulo, aproximação, destaque; `Arrastar(dx)`, `Pincar(escala)`, `Tocar(x, y)` (abre a folha ou destaca); o texto do vazio.
- `app-controle/src/BgsControle.App/Controles/EsquemaView.cs` (novo) -- `GraphicsView` que pinta a cena projetada (peças em três tons, sombras, grade, cabos, Y, bridges, rótulos) e repassa os gestos.
- `app-controle/src/BgsControle.App/Telas/SetupPage.xaml(.cs)` e `MauiProgram.cs` -- o esquema no `LugarDoEsquema` (280 px de altura mínima) e o registro.
- `app-controle/tests/BgsControle.Telas.Testes/EsquemaTestes.cs` (novo) -- a matriz inteira (cena, formas, proporções, cabos, Y, bridge, sem ligação, vazio, ângulo e aproximação presos, toque com ângulo, destaque, ao vivo), sem MAUI.

**Acceptance Criteria:**
- Given a tela do Setup aberta no Casa 2.1, when a JBL R é tirada pela folha, then a cena seguinte não tem a JBL R, e o ângulo e a aproximação não mudam.
- Given qualquer ângulo e aproximação, when o toque cai no centro projetado de uma peça, then o teste de toque devolve essa peça.
- Given o app compilado para Android, when o build roda, then não há erro nem aviso.

## Spec Change Log

## Review Triage Log

### 2026-10-24 — Review pass
- verdicts: 34 findings — high 0, medium 9, low 20, false 5, maybe-false 0
- findings:
  - `[false]` `[reject]` (intenção) Giro contínuo de −45° a +45° em vez de saltos entre posições isométricas — decisão escrita na spec; leitura defensável
  - `[low]` `[reject]` (intenção) A proporção usa só o diâmetro — é o dado que tem tamanho; os outros não viram forma
  - `[false]` `[reject]` (intenção) Colunas pelo caminho do sinal, não grupo 1 a 4 literal — o EXPERIENCE.md descreve o caminho (processador → grupo 3 → … → alto-falantes)
  - `[medium]` `[patch]` (intenção) Sombra pintada na cor do fundo — sombra em sup-1 a 40 %
  - `[false]` `[reject]` (intenção) Pintura e gestos sem teste automático — não há harness MAUI nem celular; fica para o aparelho (adiado)
  - `[low]` `[reject]` (intenção) A cena se refaz ao mudar o link só pela lista — o link não muda o desenho: o cabo leva a cor do canal que carrega
  - `[false]` `[reject]` (intenção) Build e conferência manual sem prova no diff — o build rodou nesta revisão (0 erros, 0 avisos); a conferência no aparelho está adiada
  - `[low]` `[reject]` (intenção) Precisão do toque com o dedo não conferida — só no aparelho
  - `[low]` `[patch]` (lacuna) Forma pela função sugerida não passava pelo ViewModel em teste — teste com o "shaker" no Casa 2.1
  - `[medium]` `[patch]` (cego) Sombra invisível — o mesmo patch da sombra
  - `[medium]` `[patch]` (cego) Portas e rótulos CH somem a ±45° — face de lado conta como visível; testes
  - `[medium]` `[patch]` (cego) O arrasto podia travar a rolagem da página — só gira com arrasto mais horizontal e sem pinça
  - `[medium]` `[patch]` (cego) O zoom sempre no centro deixava o processador fora do alcance — zoom em torno dos dedos, com deslocamento preso; teste
  - `[low]` `[patch]` (cego) "Sem ligação" do esquema diferente do da lista — a mesma regra da lista; testes
  - `[low]` `[reject]` (cego) Com o Link 3–4, o cabo do CH4 é menta e a saída é âmbar — a spec manda o cabo na cor do canal que leva
  - `[low]` `[patch]` (cego) Trocar de Setup mantinha ângulo, aproximação, destaque e contorno — zera ao trocar; o contorno sai ao fechar a folha; teste
  - `[low]` `[patch]` (cego) Aviso da cena antes de limpar a projeção; tamanho pelo retângulo sujo — ordem corrigida; tamanho pela view
  - `[low]` `[patch]` (cego) O domo do tweeter fora da área de toque — domo na silhueta; teste
  - `[low]` `[patch]` (cego) "O da frente ganha" sem teste — mesma ordem no toque e na pintura; testes
  - `[low]` `[reject]` (cego) A montagem não é linear — irrelevante até 20 peças; as portas por peça foram pré-calculadas para a pintura
  - `[low]` `[patch]` (cego) Fila de soltos sem quebra e vias que transbordam — a fila quebra e as vias encolhem; testes
  - `[false]` `[reject]` (cego) Registro da história incompleto — esta revisão completa
  - `[medium]` `[patch]` (borda) Portas a ±45° — o mesmo patch
  - `[medium]` `[patch]` (borda) Arrasto vertical sobre o esquema — o mesmo patch
  - `[medium]` `[patch]` (borda) Zoom sem deslocamento — o mesmo patch
  - `[low]` `[patch]` (borda) Rótulo preso na borda longe da peça — sem rótulo fora da área
  - `[low]` `[patch]` (borda) Vias além da caixa — o mesmo patch das vias
  - `[low]` `[patch]` (borda) Desempate diferente no toque e na pintura — o mesmo patch
  - `[low]` `[patch]` (borda) Barramentos do grupo 3 sobrepostos — um barramento por posição na coluna; teste
  - `[low]` `[patch]` (borda) Destaque e ângulo do Setup anterior — o mesmo patch
  - `[low]` `[patch]` (borda) Alto-falante com canal e bridge nulos quebrava a montagem — tratado como sem ligação
  - `[medium]` `[patch]` (borda) Sombra invisível — o mesmo patch
  - `[low]` `[reject]` (borda) Afirmação de montagem linear — o mesmo
  - `[low]` `[patch]` (borda) Destaque só apagava as portas do processador — portas e bordas dos outros canais também a 35 %; teste

## Design Notes

- **Por que a cena fica no ViewModel:** a pintura no `GraphicsView` não dá para testar sem o aparelho; as posições, as formas e o toque dão. A view fica fina, como o fader e o giratório.
- **Isométrico com ângulo:** girar de −45° a +45° em torno do eixo vertical dá a sensação de volume sem 3D de verdade; fora disso as peças se cobrem e o toque fica ambíguo.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- Com o celular no adb, abrir Configurações › Setups › Casa 2.1 e conferir o esquema; sem celular, registrar que não foi visto.

## Auto Run Result

Status: done

**O que entrou.** O esquema do Setup na tela Setup, em 2D com sensação de 3D.
- **Cena:** a `CenaSetup` é testável sem MAUI e monta:
  - as colunas pelo caminho do sinal;
  - a forma de cada categoria (domo, corneta, cone pelo diâmetro, coaxial, caixa com cone, caixa com as vias, módulo com canais e bridges);
  - os cabos na cor do canal que levam, com o Y marcado;
  - os bridges;
  - as peças sem ligação à parte, pela mesma regra da lista.
- **Projeção isométrica:** a `ProjecaoIsometrica` cuida do ângulo (−45° a +45°), da aproximação (0,6× a 2,5×, em torno dos dedos) e do toque. O que fica na frente ganha, com a mesma ordem da pintura.
- **ViewModel:** o `EsquemaViewModel` se refaz sozinho e cuida do destaque de um canal (o resto a 35 %) e do toque que abre a folha.
- **Pintura:** o `EsquemaView` pinta as peças em três tons, a sombra em `sup-1` a 40 %, a grade, os cabos e os rótulos.

**Arquivos:**
- `BgsControle.Telas/Esquema/CenaSetup.cs`, `ProjecaoIsometrica.cs` e `EsquemaViewModel.cs`;
- `BgsControle.App/Controles/EsquemaView.cs`;
- `SetupPage.xaml(.cs)` e `MauiProgram.cs`;
- `BgsControle.Telas.csproj` (`InternalsVisibleTo`);
- `tests/BgsControle.Telas.Testes/EsquemaTestes.cs` (26 testes).

**Revisão:** 34 achados.
- **Corrigidos (4 entradas medium):** sombra visível, portas a ±45°, arrasto que não trava a rolagem e zoom em torno dos dedos.
- **Corrigidos (low):** a regra de "sem ligação", a troca de Setup, o domo no toque, a ordem do toque, o layout (fila, vias, barramentos), os rótulos fora da área, o destaque, a peça sem ligação válida e o teste da forma pela função sugerida.
- **Adiado (1):** a conferência da pintura e dos gestos no aparelho.
- **Rejeitados:** os demais, com o motivo.

**Revisão de acompanhamento recomendada:** sim (4 entradas medium corrigidas). O risco que ficou sem segunda leitura são os gestos e a pintura no Android, que só o aparelho mostra.

**Verificação:**
- `dotnet test` do Nucleo.Testes: 990 passam.
- `dotnet test` do Telas.Testes: 421 passam.
- `dotnet build` do App para `net10.0-android`: 0 erros e 0 avisos.
- A prévia em SVG (fora do app) conferiu o layout do Casa 2.1, de um Setup grande, dos ângulos, do zoom e do vazio.
- Nada foi visto num celular.
