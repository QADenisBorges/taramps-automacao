---
title: 'História 10c: telas do Setup físico e da memória completa'
type: 'feature'
created: '2026-10-29'
status: 'done'
baseline_revision: '8aa17b8f3a3ce5cc1efc1ea601f7e56e7da258d6'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/DESIGN.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/8f-setup-so-fisico-memoria-com-toda-a-regulagem-e-fichas-com-origem.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** A história 8f mudou o núcleo (Setup só físico, ficha com origem, base de modelos, Criar Setup com a primeira memória, dúvida sem memória), mas as telas ainda mostram só 7 dados da ficha sem origem, não completam o modelo pela base, pedem um nome só no Criar Setup e dizem "O aparelho está no Casa 2.1." quando o Setup não tem memória (EXPERIENCE.md, nota "Mudança de 29/10/2026").

**Approach:** Ligar nas telas o que o núcleo da 8f já oferece: a ficha completa com o selo de origem por dado, o modelo que se completa pela base, os dois nomes do Criar Setup com o erro de cada um, e os textos da dúvida sem memória e da primeira abertura.

## Boundaries & Constraints

**Always:**
- **Ficha (folha de equipamento, em DADOS TÉCNICOS, recolhido):** os dados que valem para o grupo do equipamento:
  - alto-falante passivo e grupo 1: diâmetro, impedância, potência RMS, sensibilidade, faixa (mínima e máxima), Re, Fs, Qes, Qms, Vas, Bl, Sd, Xmax, Le, e a caixa (tipo, volume, Fb, área do duto); as vias continuam no grupo 1;
  - grupo 2: canais, ganho, ganho em ponte, potência e tensão de ceifamento por canal e em ponte, tensão e corrente da alimentação;
  - grupos 3 e 4: os dados de hoje.
  - Todos opcionais; o que não se sabe fica em branco. Cada linha tem o rótulo com a unidade, o campo e o **selo de origem**.
- **Selo de origem:** texto curto (oficial, outra fonte, medido, estimado, digitado). Só oficial e medido levam a marca de verificado (✓). Tocar no selo abre a escolha da origem (as 5) e o campo Fonte (até 120 caracteres; para oficial, o endereço do manual ou da ficha). Um valor digitado ou mudado vira digitado, a menos que o usuário escolha outra origem. Um campo vazio não tem selo.
- **Modelo que se completa:** com 2 ou mais letras no Modelo, aparecem até 5 modelos da base que começam com o texto (`BaseModelos.Sugerir`). Tocar num deles põe o nome no Modelo e preenche os dados que a base tem, com as origens dela (`Ficha`); os dados que a base não tem ficam como estavam. Sem toque, nada é preenchido.
- **Criar Setup** (escolha e dúvida): dois campos, "Nome do Setup" (placeholder "Carro") e "Nome da primeira memória" (vazio = o nome do Setup). Um erro aparece embaixo do campo que errou: o núcleo ganha uma mensagem própria para o nome da memória inválido ou repetido, distinta da do Setup.
- **Textos da escolha do Setup:**
  - frase com o Setup sem memória para comparar: "O {Setup} ainda não tem memória."; com diferenças, a lista continua; sem diferenças e com memória, "O aparelho está no {Setup}." continua;
  - Escolher: "Aplica a memória ativa dele, relê e desmuta. Sem memória, só desmuta.";
  - Criar: "O som do aparelho vira a primeira memória dele.";
  - a linha da folha do Criar e os textos fixos do XAML (O QUE NÃO BATE) passam a vir do ViewModel.
- **Primeira abertura:** conectar sem memória cai na escolha com "O Casa 2.1 ainda não tem memória."; Escolher desmuta e o Início mostra "Nenhuma memória" e "Criar memória do som atual"; Presets mostra o vazio.
- **DESIGN.md:** entra o componente `selo-de-origem` (mesma forma do `selo-herdado`; verificado com ✓) e a linha da sugestão de modelo.
- A `Bancada` de testes passa a montar o `GerenteSetups` com a `BaseModelos`.

**Never:**
- Mudar regra do núcleo além da mensagem do nome da memória.
- Copiar a ficha da base sem o toque do usuário, ou mexer em dados que a base não tem.
- Telas da IA além do que já existe (a folha do Continuar já funciona pela confirmação da IA).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Ficha da JBL L | Editar a JBL L do Casa 2.1 | Fs 98 com selo "oficial ✓", Re 3,6 com "medido ✓", volume com "estimado" | — |
| Digitar um dado | Fs vazio, digita 80 | Selo "digitado"; ao confirmar, Fs Digitado | Valor ≤ 0 ou não número: erro do campo, nada guardado |
| Escolher a origem | Toca no selo de Fs, escolhe oficial e escreve a fonte | Fs guardado Oficial com a fonte | Fonte > 120: erro, nada guardado |
| Sugestão | Equipamento novo, Modelo "jb" | Lista com "JBL 4TRMS80"; tocar preenche Fs, Qes… com as origens da base | Menos de 2 letras: sem lista |
| Criar Setup | Na dúvida, "Carro" e "ESTRADA" | Setup Carro e memória ESTRADA | Nome da memória inválido: erro embaixo do campo da memória; o do Setup fica sem erro |
| Dúvida sem memória | Primeira conexão | Frase "O Casa 2.1 ainda não tem memória.", sem lista | — |
| Primeira abertura | Escolher o Casa 2.1 | Início com "Nenhuma memória" e "Criar memória do som atual" | — |

</intent-contract>

## Code Map

Caminhos relativos a `app-controle/`.

- `src/BgsControle.Telas/EquipamentosViewModel.cs` -- campos de texto L272-278 e propriedades L565-578 (`Mudar` L1145); `Modelo` L508, `DicaModelo` L521; `DadosAbertos/AlternarDados` L433-449; `MostraGanho` L452, `MostraDadosAltoFalante` L455; `Vias` L581; `AbrirAdicionar` L698-717; `AbrirEditar` L720-745 (hoje lê só `Valor` por `Texto()` L1154 e perde a origem); `ConfirmarAsync` L779-815; `Montar` L865-910 (`Dado(antes, digitado)` L878-879, `mesmoModelo` L881-883, campos fora da folha mantidos, comentário L860-862 apontando para esta história); `AvisarFolha` L1096-1123.
- `src/BgsControle.Nucleo/Setups/Equipamentos.cs` -- `OrigemDado` L75, `DadoTecnico` L100, `DadosTecnicos` L123 (campos L130-217), `So()` L258, `ComoDigitados()` L270.
- `src/BgsControle.Nucleo/Setups/BaseModelos.cs` -- `Sugerir` L66, `Ficha` L77, `Chave`; `GerenteSetups.Modelos` L273.
- `src/BgsControle.App/Telas/SetupPage.xaml` -- a folha inline L123-307: MODELO `Entry` L174-175; DADOS TÉCNICOS L245-298 (rótulo + `Entry` numérico repetidos, sem controle reutilizável); vias L281-290; template `pilula` L18-37; selo HERDADO inline L49-52 (modelo de forma para o selo de origem; também em `SetupEscolhaPage.xaml` L114-118 e `SetupsPage.xaml` ~L42).
- `src/BgsControle.Telas/SetupViewModel.cs` -- constantes L53-64 (`FraseEscolher`, `FraseCriar`, `RotuloDiferencas`, `SubRotuloDiferencas`); `Titulo` L127-132; `Frase` L138-151 (dá "O aparelho está no {nome}." sempre que não há diferença); `LinhasDiferenca/TemDiferencas` L173-187; `AbrirCriar` L92-99, `NomeNovo/ErroNome` L246-269, `ConfirmarCriarAsync` L278-294 (chama `CriarSetupAsync(NomeNovo)`).
- `src/BgsControle.App/Telas/SetupEscolhaPage.xaml` -- Titulo L32, Frase L34, O QUE NÃO BATE fixo L57-60, lista L61-75, botões L84-94, folha do Criar L139-151 (um `Entry`, linha fixa "O Setup nasce com a calibração…").
- `src/BgsControle.Nucleo/Setups/GerenteSetups.cs` -- `CriarSetupAsync` L948-1005 (nome do Setup: `GerentePresets.MsgNomeInvalido` L958, `MsgNomeRepetidoSetup` L960; nome da memória por `NovaMemoria` L1796-1807 com o mesmo `MsgNomeInvalido` L1800 ou `MsgNomeRepetidoMemoria` L1802, devolvido em L968); `DiferencasDaConferencia` L824-827 (vazia sem memória); `MemoriaDaReferencia` L402; `MsgSetupCriado` L137.
- Início e vazio: `SomEmUsoTexto.cs` L20/L47-51; `InicioViewModel` `TextoCriarMemoria` L97, `MostrarCriarMemoria` L246; `InicioView.xaml` L125-126 (texto fixo); `PresetsViewModel.TextoSemProprios` L111.
- `DESIGN.md` -- `selo-herdado` YAML L577-583 e prosa L879; `folha-de-equipamento` L894; `campo-numerico` L417-426.
- Testes: `tests/BgsControle.Telas.Testes/ControleParametroViewModelTestes.cs` (`Bancada` L22, `GerenteSetups` montado em L84-86 sem `modelos:`, `CriarSetupViewModel` L126, `Conectar` L153-175, `filme` L46); `EquipamentosViewModelTestes.cs` L12-15; `SetupViewModelTestes.cs` (L14-35 `EntrarNaDuvida`, Criar L88-108, frase L132); `ArquiteturaTestes.cs` (L45-71 proibições, L101-121 `x:DataType` e sem cor `#hex`).

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Nucleo/Setups/GerenteSetups.cs` -- mensagem própria para o nome da memória inválido ou repetido no `CriarSetupAsync`, e um jeito de a tela saber qual nome errou -- erro embaixo do campo certo.
- `src/BgsControle.Telas/EquipamentosViewModel.cs` (e um ViewModel de linha de dado, se ajudar) -- a ficha completa por grupo, a origem e a fonte por dado (ler na edição, escolher no selo, digitado ao mudar), a lista de sugestões e o preencher pela base -- a ficha da 8f na tela.
- `src/BgsControle.App/Telas/SetupPage.xaml` (e um controle de linha de dado reutilizável em `Controles/`) -- os dados por grupo com o selo, a folha da origem, a lista de sugestões embaixo do Modelo -- sem repetir 25 blocos à mão.
- `src/BgsControle.Telas/SetupViewModel.cs` e `src/BgsControle.App/Telas/SetupEscolhaPage.xaml` -- os dois nomes com os erros, a frase sem memória e os textos novos vindos do ViewModel -- a escolha do Setup do modelo novo.
- `src/BgsControle.App/Controles/InicioView.xaml` e `InicioViewModel.cs` -- o texto do Criar memória vindo do ViewModel -- sem texto fixo no XAML.
- `DESIGN.md` -- `selo-de-origem` e a linha de sugestão -- o documento manda nas telas.
- `tests/BgsControle.Telas.Testes/*` -- `Bancada` com a base; um teste por linha da matriz; o `ArquiteturaTestes` continua passando -- a prova.

**Acceptance Criteria:**
- Given o Casa 2.1 herdado, when o usuário abre a folha de cada um dos 4 equipamentos, then cada dado com valor mostra a origem que o núcleo guarda, e só oficial e medido mostram ✓.
- Given uma ficha editada sem mudar nada, when o usuário confirma, then todas as origens e fontes ficam como estavam.
- Given o código completo, when roda o build do App para Android, then não há erro nem aviso, e nenhum XAML tem texto fixo de O QUE NÃO BATE, da frase do Criar ou do Criar memória.

## Spec Change Log

## Review Triage Log

### 2026-10-29 — Review pass
- verdicts: 36 findings — high 0, medium 11, low 22, false 3, maybe-false 0
- findings:
  - `[low]` `[reject]` (intenção) A folha do Continuar não tem teste de tela — a folha é a confirmação genérica da IA, já testada; o texto vem do núcleo, testado em `PortaTestes`.
  - `[false]` `[reject]` (intenção) Salvar, Ajuste e Crossover sem mudança — entregues na 8f (commit 8aa17b8), com o `ArquiteturaTestes` barrando o que saiu.
  - `[false]` `[reject]` (intenção) A lista de diferenças da dúvida sem teste novo — os testes existentes do `SetupViewModel` cobrem a lista.
  - `[low]` `[patch]` (intenção) A lista "…" sem memórias não era testada — o teste da primeira abertura confere só as 3 do aparelho.
  - `[low]` `[patch]` (intenção) EXPERIENCE com 4 origens — "estimado" entrou no documento.
  - `[low]` `[patch]` (intenção) EXPERIENCE dizia "fabricante e modelo" — "ao digitar o modelo (com o fabricante)… do mesmo grupo".
  - `[low]` `[patch]` (intenção) Qual nome errou era decidido comparando textos — mesmo defeito da comparação de texto abaixo.
  - `[false]` `[reject]` (intenção) Sem prova do build Android — o build roda na verificação (0 erros, 0 avisos).
  - `[low]` `[patch]` (cego) Editar só a fonte travava a origem — a fonte fica à parte e não fixa a origem.
  - `[low]` `[patch]` (cego) Apagar o campo mantinha a origem e a fonte — apagar esquece as duas.
  - `[medium]` `[patch]` (cego) Dados da base mantinham a origem depois de trocar o Modelo — só valem enquanto o Modelo é o escolhido.
  - `[medium]` `[patch]` (cego) Arredondamento em "0.###" regravava o dado como digitado — a tela mostra todas as casas; teste com Le 0,0453.
  - `[medium]` `[patch]` (cego) Sugestões de outro grupo e campos escondidos preenchidos — só modelos do mesmo grupo.
  - `[low]` `[patch]` (cego) Erro do nome da memória por comparação de texto — `NovaMemoria` devolve se o motivo é do nome; o resultado ganhou `ErroNomeSetup`.
  - `[low]` `[patch]` (cego) Teclado do Criar Setup (Return não ia para o campo da memória) — o Return passa o foco; sem `CapitalizeCharacter`, porque nenhum campo de memória do app usa.
  - `[low]` `[patch]` (cego) Listas recriadas a cada tecla no Modelo — listas em cache, sugestões uma vez por mudança, `Origens` só na linha aberta.
  - `[low]` `[patch]` (cego) Divisória e tokens da sugestão faltando — divisória `sup-3` e `alvo-min`/`alvo-lista` em `App.xaml`.
  - `[low]` `[patch]` (cego) Sem descrição para leitor de tela — selo, pílulas, campos e sugestões com descrição e dica.
  - `[low]` `[patch]` (cego) Sem teste de que toda linha existe para cada campo — teste novo.
  - `[low]` `[reject]` (cego) Caminhos errados no Code Map da spec — a correção seria editar a spec.
  - `[low]` `[patch]` (cego) Testes da primeira abertura fracos — conferem o Início, a `FraseEscolher` e o número exato de escritas.
  - `[low]` `[reject]` (cego) Valores digitados em linhas de outro grupo somem ao trocar de grupo — raro, e o comportamento já era esse antes; avisar seria fluxo novo.
  - `[medium]` `[patch]` (verificação) Ficha com 4 casas sem teste — teste novo de ida e volta.
  - `[medium]` `[patch]` (verificação) O arredondamento em si — mesmo defeito acima.
  - `[medium]` `[patch]` (verificação) Origem da base depois de trocar o Modelo — mesmo defeito acima.
  - `[medium]` `[patch]` (borda) Arredondamento — mesmo defeito acima.
  - `[medium]` `[patch]` (borda) Origem da base com outro Modelo — mesmo defeito acima.
  - `[medium]` `[patch]` (borda) Sugestão de outro grupo — mesmo defeito acima.
  - `[medium]` `[patch]` (borda) Modelo da base com menos canais apagava os canais 3–4 de um módulo existente — na edição, a base só aumenta a contagem.
  - `[low]` `[patch]` (borda) Dica do nome da memória velha ao reabrir — avisada ao abrir e ao fechar a folha.
  - `[low]` `[patch]` (borda) Falhas que não são de nome apareciam embaixo do nome do Setup — vão para a mensagem da folha.
  - `[low]` `[patch]` (borda) Apagar o campo mantinha a origem — mesmo defeito acima.
  - `[low]` `[patch]` (borda) Mensagem da fonte com quebra de linha errada — "a fonte vai até 120 caracteres, sem quebra de linha".
  - `[low]` `[patch]` (borda) Canais apagados fora do grupo 2 — ficam como estavam.
  - `[low]` `[reject]` (borda) Potência RMS escondida no módulo — a spec dá ao grupo 2 a potência por canal e em ponte; é escolha da spec.
  - `[medium]` `[patch]` (borda) Critério "sem mudar nada" quebrado pelo arredondamento — mesmo defeito acima.

## Design Notes

- **Por que um controle de linha:** são ~25 dados com rótulo, campo e selo; uma linha reutilizável mantém a folha legível e o teste no ViewModel.
- **Por que só preencher com toque:** a base pode ter uma ficha de outro tamanho ou versão; o Denis decide se usa.

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
- **Ficha:** os dados por grupo (GERAIS, ALTO-FALANTE e CAIXA no alto-falante passivo e no grupo 1; MÓDULO e ALIMENTAÇÃO no grupo 2), numa linha reutilizável (`DadoViewModel` + `Controles/DadoTecnicoView.xaml`) com o selo de origem; só oficial e medido levam ✓; tocar no selo abre as 5 origens e a fonte; o valor mostra todas as casas decimais, então confirmar sem mexer não muda nada.
- **Modelo que se completa:** com 2 letras, até 5 modelos da base do mesmo grupo; tocar preenche só o que a base tem, com as origens dela, que valem enquanto o Modelo continua o escolhido; num módulo existente, a base não reduz os canais.
- **Criar Setup:** "Nome do Setup" e "Nome da primeira memória", cada um com o seu erro (o núcleo diz qual nome errou por um sinal, não pelo texto); outras falhas na mensagem da folha; Return passa ao segundo campo.
- **Escolha do Setup:** "O {Setup} ainda não tem memória." sem memória; frases novas do Escolher e do Criar; os textos fixos saíram do XAML.
- **Primeira abertura:** testada do conectar ao Início com "Nenhuma memória" e Presets vazio.
- **Docs:** DESIGN.md com `selo-de-origem` e `linha-de-sugestao-de-modelo`; EXPERIENCE com as 5 origens.

**Arquivos.** Núcleo: `Setups/Setup.cs`, `Setups/GerenteSetups.cs` (só o sinal do nome). Telas: `DadoViewModel.cs` (novo), `EquipamentosViewModel.cs`, `SetupViewModel.cs`. App: `Controles/DadoTecnicoView.xaml` (novo), `Telas/SetupPage.xaml`, `Telas/SetupEscolhaPage.xaml(.cs)`, `Telas/InicioView.xaml`, `App.xaml` (`alvo-min`, `alvo-lista`). Testes: `EquipamentosViewModelTestes`, `SetupViewModelTestes`, `PrimeiraAberturaTelasTestes` (novo), `ArquiteturaTestes`, `ControleParametroViewModelTestes` (`Bancada` com a base), `SetupsTestes`.

**Revisão.** 36 achados: 28 corrigidos (11 médios), 0 adiados, 8 recusados com o motivo no Review Triage Log.

**Decisões a conferir com o Denis.**
- A linha "Canais" do módulo é só leitura e segue "Onde está ligado".
- A fonte longa demais só dá erro no ✓ da folha.
- Valores digitados em linhas de outro grupo ficam escondidos ao trocar de grupo e não são guardados.
- Os campos de nome de memória do app não usam letra maiúscula automática; o novo segue os outros.

**Follow-up review recomendada:** sim (11 médios corrigidos). Risco a conferir: a regra de origem da linha (escolhida, carregada do equipamento, carregada da base) só foi exercida no ViewModel; o toque no selo e a lista de sugestões nunca foram vistos numa tela.

**Verificação.**
- `dotnet test` Nucleo.Testes: 1040 passam.
- `dotnet test` Telas.Testes: 462 passam.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- Nada visto no aparelho: é a história 12b.
