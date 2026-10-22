---
title: 'História 8: presets no celular e conhecimento herdado'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
baseline_revision: 'f48037e3c6468dd620a0445c15a867a861d09207'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/conhecimento-herdado.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/failure-modes.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/chaves-de-acesso.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/6-memorias-link-copia-antes.md'
warnings: [oversized]
deferred:
  - summary: >-
      A composição do DI no MauiProgram (GerentePresets criado cedo, ArquivoPresets, os dois ViewModels novos) não tem teste automático.
    evidence: |-
      Nenhum projeto de teste referencia o MauiProgram (Android). Tirar o `GetRequiredService<GerentePresets>()` do arranque, ou ligar o `ArquivoLink` no lugar do `ArquivoPresets`, passa em todos os testes e no build. Sem o gerente criado cedo, uma marca `aplicar-preset` achada na reconexão responde "O app não sabe refazer esta operação" até a tela de presets ser aberta. Conferir no celular na história 12 (fechar o app no meio de um aplicar, reconectar e tocar em Terminar).
    location: >-
      app-controle/src/BgsControle.App/MauiProgram.cs
    severity: medium
---

<intent-contract>

## Intent

**Problem:** O Denis ainda não guarda sons próprios no celular (CAP-5) e o app abre sem nada do que os protótipos aprenderam (CAP-6): a FILME definitiva, as notas das 3 memórias, o sistema dele e os cuidados com o app da Taramps.

**Approach:** No núcleo, um `GerentePresets` guarda, aplica, renomeia e apaga presets em JSON nos arquivos privados, aplicando pela `CopiaAntes` da história 6 (cópia antes, "aplicado pela metade"), e um `ConhecimentoHerdado` embutido no app (notas das memórias, o preset herdado FILME e as seções de texto). Em `BgsControle.Telas`, o `PresetsViewModel` e o `HerdadoViewModel`, e o selo herdado nos cartões de memória, testados contra o `ProcessadorSimulado`.

## Boundaries & Constraints

**Always:**
- **O que é um preset.** Os valores de áudio do Marco 1 (`Catalogo.ConsultasMarco1`): uma lista de `ValorCopiado` (código, índice, valor como o aparelho devolve), tirada do espelho `EstadoAparelho`. Guardar não manda nada ao aparelho e não precisa de chave.
- **Guardar o som atual.**
  - Exige leitura completa no espelho: sem leitura, ou lendo, recusa com `Escritor.MsgSemLeitura` ou `Escritor.MsgLendo`; faltando valor de algum parâmetro do Marco 1, recusa com "A leitura está incompleta (N ajustes sem valor)." e nada é guardado.
  - **Nome do preset:** de 1 a 24 caracteres depois de aparar os espaços das pontas, entre letras (acentos valem), números, espaço, `_` e `-`; fica como foi digitado. Inválido: "Use de 1 a 24 letras, números, espaço, _ ou -".
  - Não pode repetir o nome de outro preset (próprio ou herdado), sem distinguir maiúsculas: "Já existe um preset com esse nome."
  - Preset novo tem id único estável, a hora (`TimeProvider`) e a lista de valores.
- **Guardado.** Um arquivo `presets.json` nos arquivos privados (`RepositorioTextoArquivos.ArquivoPresets`, via `IRepositorioTexto`), formato 1, só dos presets próprios. Gravar com `Gravacao.GravarAsync` (duas tentativas); se não gravar, a lista não muda e a frase é "Não consegui guardar o preset no celular." Arquivo ausente, estragado ou de outro formato: lista de próprios vazia, sem erro. Lê no início, como `LinkCanais`; `Carregada` completa quando leu.
- **Aplicar.** Sem confirmação (decisão da UX). Pela `CopiaAntes`, no mesmo molde do carregar da história 6:
  1. Sem leitura completa no espelho, ou lendo: recusa como acima. Preset inexistente: "O preset não existe mais."
  2. Calcula os parâmetros do preset diferentes do espelho (reusa `CopiaAntes.Diferencas`, que passa a ser `internal`, mais a função de comparar). Valor do preset que não decodifica ou não codifica: nada sai, `CopiaAntes.MsgCopiaIlegivel(n)`. Nenhum diferente: "Preset NOME já está aplicado." sem cópia, sem escrita.
  3. Confere a trava do primeiro quadro com `Sessao.Conferir` (chave "Ajustes de áudio" ligada, sessão Conectada, fora do modo só leitura); recusado: nada sai, o motivo da sessão.
  4. `CopiaAntes.IniciarAsync` com `OperacaoPendente(TipoAplicar = "aplicar-preset", Alvo = id do preset, Descricao = "Aplicar o preset NOME", Nome = NOME)`. Se não guardar a cópia, nada sai.
  5. Escreve cada diferente por `Escritor.EscreverAsync(parametro, indice, valor, espelhar: false, daCopia: true)`, uma de cada vez, sem o link repetir. Qualquer falha (`!Ok` ou `Diferente`) com a sessão aberta: `CopiaAntes.MarcarPelaMetade()` e "N ajustes não foram escritos: MOTIVO"; com a conexão caída: `CopiaAntes.Interromper()` (a marca fica para a próxima conexão).
  6. Escreveu tudo: `LeituraAutomatica.LerNovaAsync()` (releitura completa). Retrato nulo ou incompleto: com a conexão caída, `Interromper()`; aberta, `MarcarPelaMetade()` com `GerenteMemorias.MsgLeituraParou`.
  7. Conferiu o retrato novo contra o preset (`CopiaAntes.Iguais`): `ConcluirAsync()` e "Preset NOME aplicado. Para guardar numa memória, use Salvar." Se algum valor relido difere do preset (o aparelho ajustou), também conclui, mas diz "Preset NOME aplicado, mas N ajustes ficaram diferentes."
  - [SUPOSIÇÃO PRESET-APLICA-TUDO] Escrever todos os ajustes diferentes em sequência e a releitura confirmam o preset no PRO 2.4BT; a história 12 confere no aparelho.
- **Aplicado pela metade.** O `GerentePresets` registra `TipoAplicar` na `CopiaAntes` (`Registrar`) no construtor: ao reconectar, `Retomada.PelaMetade`; Terminar refaz a aplicação pelo id da marca, com `manterCopia: true`; preset apagado entretanto: "O preset não existe mais; use Voltar à cópia." e continua pela metade. Voltar à cópia é o da `CopiaAntes`.
- **Renomear e apagar** só os próprios. Renomear segue as regras de nome; apagar tira da lista e grava. Herdado: "Preset herdado não muda." Nenhum dos dois manda nada ao aparelho. Todas as operações do gerente (guardar, aplicar, renomear, apagar) são exclusivas entre si: outra em andamento recusa com "Uma operação de preset já está em andamento." Aplicar também recusa, sem escrever, com `CopiaAntes.MotivoBloqueio()` (memória em andamento ou aplicado pela metade).
- **Exportar.** `GerentePresets.ExportarJson(id)` devolve o JSON de um preset próprio ou herdado (formato 1, com o nome, as notas e os valores); o arquivo e o compartilhar são da história 11. Nunca é `.trps`.
- **Conhecimento herdado** (`Nucleo/Herdado/ConhecimentoHerdado.cs`), sempre presente, sem arquivo do usuário e sem digitar nada (CAP-6):
  - `NotaMemoria(n)` para as 3 memórias, pela posição, com o nome que o protótipo usava: 1 = MUSICA HIGH (JBL +2, sub −1,4, EQ plano, ataque 10 ms, limitadores −11/−9, aprovada), 2 = MUSICA ORIG (versão antiga, ganhos −5/−10, limitadores −11/−6, precisa ser refeita: é o teste 1), 3 = FILME (a versão definitiva de 18/10). O nome que o aparelho tem hoje pode ser outro; o selo vale pela posição.
  - O preset herdado **FILME**, id `herdado-filme-2026-10-18`, com os valores do Marco 1 da leitura `estado/leituras/2026-10-18_2145.json`, embutida como recurso do projeto (`EmbeddedResource` linkado, como faz o `BgsControle.Simulador`) e decodificada com `Quadros.Analisar` e o catálogo. Os herdados vêm sempre primeiro na lista, não ficam em `presets.json` e não mudam.
  - As seções de texto de `conhecimento-herdado.md` em português: Protocolo, O sistema do Denis, As 3 memórias, Presets, Preferências e método, Cuidados com o app da Taramps, Situação registrada. A de Presets diz que DIALOGO e NOITE não entram (`.trps` cifrados). Os limites do guardião aparecem como provisórios: quem decide é o Denis.
- **`PresetsViewModel`** (Telas): a lista (herdados, com selo e nota, e depois os próprios), "Guardar o som atual" com o campo do nome e a frase de erro, Aplicar por item, Renomear (campo e confirmar), Apagar com folha "Apagar o preset X?" e Confirmar ou Cancelar, o texto "Guarde o som atual como preset." quando não há próprio, a frase da última operação, a bandeira "ocupado", as travas com o motivo ("Chave desligada", "Sem conexão"…), e o aplicado pela metade com Terminar e Voltar à cópia, como no `MemoriasViewModel`. Nada sai antes do botão de confirmar da folha.
- **`HerdadoViewModel`** (Telas): as seções e o texto do selo ("HERDADO"). O cartão de memória ganha a nota herdada e o selo.
- **DI.** `GerentePresets` singleton no `MauiProgram`, criado cedo (como o `GerenteMemorias`), para registrar o tipo na `CopiaAntes` antes da primeira leitura. Sem tela nova.
- **Simulador e suposições.** A nova suposição entra no código (`Suposição PRESET-APLICA-TUDO`), em `Suposicoes.cs` e em `SUPOSICOES.md`, com o mesmo texto.

**Never:**
- XAML ou navegação (histórias 10 e 11).
- Ler, importar ou decifrar `.trps`, ou importar arquivo de preset.
- Marcar em cada preset a fonte para a qual foi regulado (fica para quando houver segunda fonte).
- Escrever o preset em memória do aparelho (240, 23N): quem salva em memória é a história 6.
- Escrita fora de `Sessao.EnviarAsync`, ou aplicar preset sem passar pela `CopiaAntes`.
- A IA e a porta de comandos (9b), o player (8b).

## I/O & Edge-Case Matrix

| Cenário | Entrada / estado | Esperado | Erro |
|---|---|---|---|
| Guardar | Espelho completo, nome "NOITE" | Preset próprio com todos os valores do Marco 1; `presets.json` gravado; nada vai ao aparelho | — |
| Nome inválido | "", 25 caracteres ou "a#b" | Nada guardado | "Use de 1 a 24 letras, números, espaço, _ ou -" |
| Nome repetido | "noite" com "NOITE" ou "FILME" (herdado) já na lista | Nada guardado | "Já existe um preset com esse nome." |
| Leitura incompleta | Espelho sem um valor do Marco 1 | Nada guardado | "A leitura está incompleta (1 ajuste sem valor)." |
| Aplicar | Preset guardado; ganho do canal 1 e uma banda do EQ mudados; chave ligada | Cópia antes; só os 2 diferentes escritos; releitura; o aparelho e o espelho iguais ao preset | — |
| Já aplicado | Estado igual ao preset | Nada sai, sem cópia | "Preset NOITE já está aplicado." |
| Chave desligada | "Ajustes de áudio" desligada | Nada sai | "Chave desligada" |
| Aplicar o herdado | FILME, depois de outras mudanças | Igual a Aplicar; o aparelho fica com os valores da leitura de 18/10 | — |
| Queda ao aplicar | Cai depois de algumas escritas | Ao reconectar e ler: "Aplicar o preset NOITE caiu no meio." com Terminar e Voltar à cópia | — |
| Terminar | Pela metade, preset ainda existe | Escreve só o que falta, com a mesma cópia; releitura; conclui | — |
| Terminar sem preset | Preset apagado entretanto | Continua pela metade | "O preset não existe mais; use Voltar à cópia." |
| Voltar à cópia | Pela metade | Os ajustes voltam ao retrato da cópia (o da `CopiaAntes`) | — |
| Renomear / apagar | Um próprio; um herdado | O próprio muda ou sai (apagar só depois do Confirmar); o herdado é recusado | "Preset herdado não muda." |
| Arquivo estragado | `presets.json` ilegível | Só os herdados na lista; guardar um novo funciona | — |
| Falha ao gravar | Repositório que falha | A lista não muda | "Não consegui guardar o preset no celular." |
| Primeira abertura | Sem `presets.json` | FILME herdado com selo; as 3 memórias com nota e selo; as seções disponíveis; "Guarde o som atual como preset." | — |

</intent-contract>

## Code Map

- `app-controle/src/BgsControle.Nucleo/Escrita/CopiaAntes.cs`: `IniciarAsync(operacao, manterCopia)`, `ConcluirAsync`, `Interromper`, `MarcarPelaMetade`, `Registrar(tipo, aoReconectar, refazer)`, `TerminarAsync`/`VoltarAsync`, `MotivoBloqueio`; o `Diferencas` (`private static`, ~l. 417) e o `Escrita` (record privado, final do arquivo) passam a `internal`; `Iguais` (público), `MsgCopiaIlegivel`. Os comentários já preveem os presets ("Genérica").
- `app-controle/src/BgsControle.Nucleo/Memorias/GerenteMemorias.cs`: o modelo do fluxo carregar (`CarregarInternoAsync`: `Ocupar`/`Desocupar`, `IniciarAsync`, `LerNovaAsync`, `MarcarPelaMetade`/`Interromper`, `ConcluirAsync`), do `Registrar` no construtor e das frases; `MsgLeituraParou`, `Terminar(ResultadoMemoria)`.
- `app-controle/src/BgsControle.Nucleo/Escrita/Escritor.cs`: `EscreverAsync(parametro, indice, Valor, bool espelhar, bool daCopia)` (`internal`), `ResultadoEscrita` (`Ok`, `Diferente`, `Motivo`), `MsgSemLeitura`, `MsgLendo`.
- `app-controle/src/BgsControle.Nucleo/Escrita/EstadoAparelho.cs`: `Copiar()`, `Pronto`, `Lendo`. `Escrita/IRepositorioTexto.cs`: `IRepositorioTexto` e `Gravacao` (`GravarAsync`, `LerAsync`, duas tentativas).
- `app-controle/src/BgsControle.Nucleo/Escrita/LinkCanais.cs`: modelo de persistência em JSON com `Carregada` e `Formato`.
- `app-controle/src/BgsControle.Nucleo/Catalogo/Catalogo.cs`: `ConsultasMarco1`, `ParametroDe`, `Consultas`; `Parametro.Decodificar(noFio)`, `CodificarEscrita`; `Protocolo/Quadros.Analisar`.
- `app-controle/src/BgsControle.Nucleo/Leitura/LeituraAutomatica.cs`: `LerNovaAsync`. `Sessao.Conferir(quadro)`, `Sessao.MotivoSemConexao`.
- `app-controle/src/BgsControle.Nucleo/BgsControle.Nucleo.csproj`: onde entra o `EmbeddedResource` da leitura de 18/10; o modelo é `app-controle/tests/BgsControle.Simulador/BgsControle.Simulador.csproj` (`LogicalName`, caminho `..\..\..\estado\leituras\2026-10-18_2145.json`) e `ProcessadorSimulado.LerSemente`/`Guardar` (como extrair o valor de cada `cru`).
- `app-controle/src/BgsControle.Telas/`: `MemoriasViewModel.cs` (`CartaoMemoriaViewModel`, `PelaMetade`, `Terminar`, `VoltarACopia`, `Agir`, folhas) é o modelo do `PresetsViewModel`; `TelaBase`, `Comando`, `Abstracoes.cs` (`IDespachante`, `IArquivosApp`); `RepositorioTextoArquivos.cs` (`ArquivoCopia`, `ArquivoLink`: acrescentar `ArquivoPresets = "presets.json"`).
- `app-controle/src/BgsControle.App/MauiProgram.cs`: o DI e a criação cedo.
- `app-controle/tests/BgsControle.Nucleo.Testes/MemoriasTestes.cs` e `Falsos.cs`; `app-controle/tests/BgsControle.Telas.Testes/MemoriasViewModelTestes.cs`, `LinkViewModelTestes.cs`, `Falsos.cs` (bancadas com o `ProcessadorSimulado`, o relógio falso e o repositório em memória); `SuposicoesTestes.cs`.
- `app-controle/tests/BgsControle.Simulador/Suposicoes.cs` e `SUPOSICOES.md`: a nova suposição.
- Só leitura: `estado/leituras/2026-10-18_2145.json` (a FILME), `protocolo/MAPA.md` §4.

## Tasks & Acceptance

**Execution:**
- [x] `app-controle/src/BgsControle.Nucleo/Herdado/ConhecimentoHerdado.cs` e `BgsControle.Nucleo.csproj` -- notas das memórias, preset herdado FILME (recurso embutido) e seções de texto -- CAP-6
- [x] `app-controle/src/BgsControle.Nucleo/Presets/` (`Preset`, `GerentePresets` com JSON) e `Escrita/CopiaAntes.cs` (`internal` no `Diferencas`) -- guardar, aplicar, renomear, apagar, exportar -- CAP-5
- [x] `app-controle/src/BgsControle.Telas/` (`PresetsViewModel`, `HerdadoViewModel`, selo no cartão de memória, `ArquivoPresets`) -- a camada das telas
- [x] `app-controle/src/BgsControle.App/MauiProgram.cs` -- DI e criação cedo
- [x] `app-controle/tests/` -- uma linha de teste para cada linha da matriz (Nucleo.Testes e Telas.Testes), a suposição nova em `Suposicoes.cs` e `SUPOSICOES.md`

**Acceptance Criteria:**
- Given o simulador com o estado da FILME, when o Denis guarda "A", muda o ganho do canal 1 e uma banda do EQ e aplica "A", then o simulador termina com o Marco 1 igual ao de "A", o espelho mostra os mesmos valores e o app diz "Preset A aplicado…".
- Given qualquer teste da história 8, when o simulador é inspecionado, then só chegaram a senha, as leituras do catálogo e as escritas de áudio pedidas.
- Given a primeira abertura (repositório vazio), when o `PresetsViewModel`, o `HerdadoViewModel` e o `MemoriasViewModel` são criados, then o FILME herdado, as notas das 3 memórias e as seções estão lá, sem arquivo e sem digitar nada.

## Spec Change Log

## Review Triage Log

### 2026-10-22 — Review pass
- verdicts: 35 findings — high 0, medium 5, low 22, false 8, maybe-false 0
- findings:
  - Intent Alignment
    - `[false]` `[reject]` Nenhuma tela consome os ViewModels — a intenção exclui XAML (histórias 10 e 11 no `stories.yaml`).
    - `[false]` `[reject]` A confirmação por releitura só vale contra o simulador — já registrada como a suposição PRESET-APLICA-TUDO, conferida na história 12.
    - `[low]` `[reject]` Textos herdados escritos à mão podem se afastar de `conhecimento-herdado.md` — texto da spec; a correção seria um teste acoplado ao md, sem dano nomeado.
    - `[false]` `[reject]` "Valores atuais" lidos como o estado inteiro do sistema — o FILME herdado é a leitura correta; a spec exclui o resto.
    - `[medium]` `[defer]` A composição do DI no `MauiProgram` não tem teste — mesmo achado do Verification Gap (adiado).
    - `[false]` `[reject]` Sem evidência de que o build e os testes rodaram — rodaram: 676/227 e build Android 0 erros e 0 avisos (678/228 depois dos patches).
    - `[low]` `[reject]` Nome repetido recusa em vez de sobrescrever — decisão registrada nas Design Notes; sobrescrever apagaria um preset sem folha.
  - Blind Hunter
    - `[low]` `[reject]` `presets.json` ilegível some no próximo guardar — a spec manda "lista vazia, sem erro"; arquivo estragado é raro e a correção (quarentena) é mais que uma correção direta.
    - `[low]` `[reject]` Falhas na carga não isoladas — `Gravacao.LerAsync` já engole erros; o `Lazy` só falha se o recurso embutido sumir, e o teste dos 84 valores pega isso na compilação.
    - `[low]` `[reject]` Espelho velho pode dar "já aplicado" — o aparelho só aceita uma conexão e a leitura roda a cada conexão; sem caminho demonstrado.
    - `[false]` `[reject]` Ordem das escritas e só o primeiro quadro conferido — `Sessao.Conferir` vale igual para todo quadro de áudio, e o limitador é o teto dos dois estados, que são seguros.
    - `[low]` `[reject]` O laço continua depois da primeira falha — é o que o passo 5 da spec manda (conta N); o Voltar à cópia já existente trata igual.
    - `[low]` `[reject]` O selo herdado fica na posição para sempre — decisão da UX ("o selo vale pela posição").
    - `[low]` `[reject]` Recurso embutido é a leitura inteira e usa `AssumeLocal` — a spec manda embutir; o fuso só mexe numa hora de exibição.
    - `[low]` `[reject]` Textos herdados duplicados e soltos dos valores — mesmo caso do primeiro item do Intent Alignment.
    - `[medium]` `[patch]` Asserções dos testes mais fracas que o critério de aceite 2 e sem teste de aplicar com memória em andamento — guarda estrito nos dois projetos de teste e teste novo.
    - `[low]` `[reject]` `DeJson` mais permissivo que os invariantes — só vale para arquivo editado à mão; o aplicar já recusa o que não decodifica.
    - `[low]` `[reject]` `"formato": 1` igual na exportação e no arquivo — a importação é não-meta; sem consumidor que se confunda.
    - `[false]` `[reject]` Spec e código divergem — `Iguais` é usado por dentro do `Diferencas`; o build roda; checklist é fechado na finalização.
    - `[low]` `[reject]` `PresetsViewModel` repete a lógica das travas — segue o `MemoriasViewModel`; o Terminar sem checar a chave é recusado pelo núcleo.
    - `[false]` `[reject]` `Agir` pode travar — o `IDespachante` sempre executa a ação; o código é o mesmo do `MemoriasViewModel`.
    - `[low]` `[reject]` Listas alocadas a cada acesso — sem efeito perceptível com 1 herdado e poucos próprios.
  - Edge Case Hunter
    - `[low]` `[reject]` Aplicar sobre espelho sem um valor — o valor do preset é escrito (o desejado); só a cópia não restaura esse ajuste.
    - `[false]` `[reject]` Exceção depois do `IniciarAsync` deixa a cópia em andamento — nenhum caminho que lança foi mostrado; o `GerenteMemorias` tem o mesmo desenho.
    - `[low]` `[reject]` Entradas descartadas somem do arquivo no próximo guardar — mesmo caso do primeiro item do Blind Hunter.
    - `[low]` `[reject]` `InvalidOperationException` do herdado engolida no `DeJson` — mesmo caso do segundo item do Blind Hunter (recurso embutido).
    - `[low]` `[reject]` `Lazy` guarda a exceção do herdado — mesmo caso.
    - `[low]` `[patch]` Nome com acento decomposto recusado — `Normalize(FormC)` antes de validar, mais teste.
    - `[low]` `[patch]` Toque duplo no Confirmar de apagar fecha a folha e zera o andamento — `Ocupado` mostra a mensagem e não fecha; teste novo.
    - `[low]` `[reject]` "Recusa como acima" não vale para espelho incompleto no Aplicar — o passo 1 da spec só pede recusa de leitura ausente ou em andamento, que existe.
    - `[medium]` `[patch]` O guarda do `Dispose` aceita comandos de memória — mesma raiz do patch dos testes.
  - Verification Gap
    - `[medium]` `[defer]` O DI do `MauiProgram` (criação cedo, `presets.json`) sem teste — não há projeto de teste que carregue o `MauiProgram`; desenho das histórias 5 a 7b; conferir no celular na história 12.
    - `[low]` `[reject]` A marca "aplicado pela metade" de um preset também abre a folha em Memórias (e vice-versa), com a frase do gerente errado depois do Terminar — a operação funciona pelo registro da `CopiaAntes`; a folha única é das histórias 10 e 11.
    - `[medium]` `[patch]` Guarda de teste mais fraco que o critério de aceite 2 — mesma raiz do patch dos testes.
    - `[low]` `[patch]` `SUPOSICOES.md` aponta `AplicarAsync` e a marca está em `AplicarInternoAsync` — coluna corrigida.

### Patches aplicados (re-engajamento do mesmo subagente)
- Guardas estritos em `PresetsTestes.cs` e `PresetsViewModelTestes.cs` (sem comandos de memória, a não ser no teste novo de aplicar durante um carregar em andamento).
- `NomeValido` normaliza para FormC; teste com acento decomposto.
- `ConfirmarApagarAsync` recusa o segundo toque sem fechar a folha; teste.
- Coluna de local da suposição PRESET-APLICA-TUDO corrigida.

## Design Notes

- **Por que a `CopiaAntes` e não um fluxo próprio.** O Voltar à cópia já escreve "o que difere do estado atual" por `Diferencas`; aplicar um preset é o mesmo cálculo com outro alvo. Reusar mantém a regra 3 de `failure-modes.md` (cópia antes, "aplicado pela metade") numa só implementação, e o Terminar usa `manterCopia: true`, como o carregar.
- **Herdados fixos.** A UX diz que a lista vazia mostra "só os herdados"; por isso eles nascem do código e não de `presets.json`. Não há flag de "primeira abertura" para perder.
- **Selo pela posição.** A memória 1 do aparelho hoje se chama SPL (20/10), mas a nota herdada é da MUSICA HIGH; a nota diz o nome do protótipo, e o selo vale para a posição.
- **Guardar sem sobrescrever.** A CAP-5 pede guardar, aplicar, renomear e apagar; nome repetido recusa em vez de substituir, para um preset nunca sumir sem a folha de confirmação. Quem quer trocar apaga o antigo.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` e `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam (sem testes novos filtrados ou ignorados)
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos

## Auto Run Result

Status: done

**Resumo.** Os presets no celular (CAP-5) e o conhecimento herdado embutido (CAP-6) estão no núcleo e nas camadas das telas, testados contra o `ProcessadorSimulado`. Guardar tira os valores do Marco 1 do espelho e não manda nada ao aparelho. Aplicar passa pela `CopiaAntes`, escreve só os ajustes diferentes, relê tudo e confere. Uma queda no meio deixa "aplicado pela metade", com Terminar e Voltar à cópia. Renomear, apagar e exportar valem só para os presets próprios. Os herdados (FILME, as notas das 3 memórias e 7 seções de texto) vêm do código.

**Arquivos.**
- `app-controle/src/BgsControle.Nucleo/Presets/GerentePresets.cs` e `Preset.cs`: guardar, aplicar, renomear, apagar, exportar e o `presets.json`.
- `app-controle/src/BgsControle.Nucleo/Herdado/ConhecimentoHerdado.cs`: notas, preset FILME (recurso embutido) e seções.
- `app-controle/src/BgsControle.Nucleo/BgsControle.Nucleo.csproj`: o recurso da leitura de 18/10.
- `app-controle/src/BgsControle.Nucleo/Escrita/CopiaAntes.cs`: `Diferencas` e `Escrita` passam a `internal`.
- `app-controle/src/BgsControle.Telas/PresetsViewModel.cs`, `HerdadoViewModel.cs`, `MemoriasViewModel.cs` (selo e nota nos cartões), `RepositorioTextoArquivos.cs`.
- `app-controle/src/BgsControle.App/MauiProgram.cs`: DI e criação cedo.
- `app-controle/tests/`: `PresetsTestes.cs`, `PresetsViewModelTestes.cs`, a bancada, `Suposicoes.cs` e `SUPOSICOES.md` (PRESET-APLICA-TUDO).

**Revisão.** 35 achados: 0 high, 5 medium, 22 low, 8 false. Patches aplicados: 3 grupos (guarda estrito dos testes e teste de aplicar com memória em andamento, nome com acento decomposto, toque duplo no Apagar) mais a coluna de local da suposição. Adiado: 1 (composição do DI sem teste, medium). Os demais foram rejeitados com o motivo no Review Triage Log. Revisão de acompanhamento: não recomendada (`followup_review_recommended: false`); nenhum patch high e só um grupo medium.

**Verificação.** `dotnet test` Nucleo.Testes 678/678 e Telas.Testes 228/228, nenhum ignorado; `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` com 0 erros e 0 avisos. Cada linha da matriz tem teste que rodou e passou.

**Riscos residuais.**
- A suposição PRESET-APLICA-TUDO (escrever tudo em sequência e reler confirma o preset no PRO 2.4BT) só se confere no aparelho real, na história 12.
- A composição do DI do `MauiProgram` só é coberta pelo build (item adiado).
- A folha "aplicado pela metade" aparece nas telas de Memórias e de Presets; a frase depois de um Terminar feito na tela "errada" pode vir do gerente errado. Uma folha única é das histórias 10 e 11.
- Os textos herdados são cópias à mão de `conhecimento-herdado.md`; se o arquivo mudar, o app não acompanha sozinho.
