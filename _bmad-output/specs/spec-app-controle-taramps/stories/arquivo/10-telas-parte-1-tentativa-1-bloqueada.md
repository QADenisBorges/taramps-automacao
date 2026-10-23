---
title: 'História 10: telas, parte 1 — leitura, ajustes, link, Setups e memórias'
type: 'feature'
created: '2026-10-23'
status: 'blocked'
baseline_revision: 'e51bf93ec7351db9d76070c7c3558283ee046692'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/SPEC.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/8c-setup-e-memoria-no-celular.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/8d-preset-15-bandas-e-playlist-da-memoria.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** O app tem uma tela só (a de conexão, `MainPage`). O núcleo e os ViewModels de leitura, ajustes, link, Setups e memórias existem e estão testados, mas o Denis não tem como usá-los no celular (CAP-2, CAP-3, CAP-4, CAP-14, CAP-16).

**Approach:** Montar em .NET MAUI a casca do app (tema escuro e tokens do `DESIGN.md`, barra inferior, engrenagem, telas cheias com ‹, folhas) e as telas desta parte, ligadas por bindings compilados aos ViewModels; criar os ViewModels que faltam (Início, Mesa, Áudio, Ajuste da saída, telas de função, Setups) e testá-los contra o `ProcessadorSimulado`.

## Boundaries & Constraints

**Always:**
- Visual e comportamento pelo `DESIGN.md` (tokens de cor, tipografia, espaçamento, formas, componentes, "Do's and Don'ts") e pelo `EXPERIENCE.md` (arquitetura de informação, "O que fica à vista e o que vai para Avançado", estados, textos). Só tema escuro, retrato, a fonte e os tamanhos do `DESIGN.md`; as cores vêm de recursos nomeados com os nomes dos tokens (`palco`, `sup-1`, `saida-l`…), nunca de valores soltos nas páginas.
- Nesta história entram:
  - a **barra inferior** com Início · Mesa · Áudio (o Player entra na história 11) e a **engrenagem** que abre Configurações;
  - **Início**: seletor de Setup, cartão da memória em uso (os casos da tabela "O que o cartão da memória em uso mostra"), o chip de preset só exibindo o nome (a folha de presets é da 11), os 3 botões de memória + "…", os cartões das três saídas (JBL L = saída 1, JBL R = saída 2, Sub = saídas 3 e 4) com "Ajustar ›", e o estado Desconectado com Conectar;
  - **Conectar e senha**: o fluxo da `ConexaoViewModel` (senha, recusa, leitura com contagem, só leitura) no visual do `DESIGN.md`;
  - **Mesa**: faders de ganho das três saídas e do MASTER, os mesmos 3 botões + "…" no topo (a faixa de monitoração é da 11);
  - **Áudio**: a grade 2 × 5 na ordem do app da Taramps; abrem nesta história Roteamento, EQ 15 bandas, EQ de entrada, Crossover (com Link 1–2 e 3–4 em Avançado), Alinhamento, Fase, Limitador, Nível das saídas e EQ de saída; o bloco Gerador aparece travado com "Gerador na próxima versão" até a 11;
  - **Ajuste da saída** (JBL L, JBL R, Sub), com Avançado e, na JBL L e na JBL R, o equilíbrio L/R do Setup (`GerenteSetups.AjustarEquilibrioAsync`), e o botão **Salvar** fixo no pé desta tela e das telas de função;
  - **Setup em dúvida / Trocar de Setup** (modal, sem barra; abre sozinha quando o `GerenteSetups` entra em dúvida ou troca, e o Voltar do Android não a fecha na dúvida);
  - **Lista de memórias** (folha do "…"), **folha do Salvar**, **folha "Aplicado pela metade"**;
  - **Configurações** com as linhas Setups e Memórias (as outras linhas entram na 11) e as telas **Configurações › Setups** (listar, renomear, apagar, selo HERDADO) e **Configurações › Memórias** (a `MemoriasViewModel`).
- Todo controle de parâmetro usa a `ControleParametroViewModel` (via `FabricaControles`), com o comportamento da história 5: o som muda no ✓ ou ao soltar o fader, o valor mostrado é o relido, e o motivo de trava aparece quando a escrita não pode sair.
- Toda página e todo template usa `x:DataType` (bindings compilados). A lógica fica nos ViewModels de `BgsControle.Telas`; o code-behind só cuida de navegação, teclado e animação.
- Nunca mostrar valor que não veio do aparelho: sem leitura, "—".

**Never:**
- Não fazer as telas da história 11: Player, folha de presets e tela Presets, Gerador, Voltímetro e clip, Aparelho, Chaves de acesso, IA pela rede, IA ativa, confirmação da IA e Histórico.
- Não mudar regras do núcleo (`BgsControle.Nucleo`) além de expor o que um ViewModel novo precisar ler.
- Não usar biblioteca de interface de terceiros.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Abrir desconectado | App aberto, sem conexão | Início com o cartão "Desconectado" e Conectar; saídas com "—" | — |
| Conectar e conferir | Conectar, senha guardada, simulador na FILME | Pílula "Conectado · R003"; Início com "Setup Casa 2.1 ›", FILME em uso, chip "Preset: Cinema", três saídas com os valores lidos | Senha errada: "Senha errada. Digite de novo (tentativa N de 3)" |
| Dúvida | Calibração diferente ao conectar | Abre "Setup em dúvida" por cima de tudo, sem barra; escolher fecha e volta ao Início | Voltar do Android não fecha |
| Trocar de Setup | Toca no seletor | Abre "Trocar de Setup" com ‹; ‹ desiste e desmuta | — |
| Botões de memória | Três memórias usadas; toca na segunda | Aplica sem confirmação; botão em branco; os outros não mudam de lugar | Chave desligada: a frase da trava, nada aplicado |
| Lista "…" | Toca em "…" | Folha com as memórias do Setup em uso e a seção "Do aparelho"; tocar aplica ou carrega e fecha | Sem Setup: memórias do celular travadas com a frase |
| Ajuste da JBL R | Avançado › equilíbrio ganho −3 → −4 | O ganho final no aparelho desce 1 dB; Salvar mostra "Calibração → Setup Casa 2.1" com o equilíbrio | No limite: "No limite da faixa com o equilíbrio do Casa 2.1." |
| Crossover com link | Avançado › Link 1–2 Lig. | Folha de aviso da cópia; confirmado, mexer na JBL L muda a JBL R | — |
| Salvar | Nada mudou | Salvar travado com "Nada para salvar." | — |
| Aplicado pela metade | Reconecta com a marca | Folha com Terminar e Voltar à cópia | — |
| Só leitura | Firmware ≠ R003 | Pílula "Só leitura · firmware Rxxx", aviso fixo no Início, controles travados | — |

</intent-contract>

## Code Map

- `app-controle/src/BgsControle.App/App.xaml`, `App.xaml.cs` -- hoje três cores e a janela com a `MainPage`. Vira o dicionário de recursos do `DESIGN.md` (cores pelos nomes dos tokens, estilos de texto, botões, cartões) e a janela com a casca (Shell com TabBar ou página raiz própria).
- `app-controle/src/BgsControle.App/MainPage.xaml(.cs)` -- a conexão atual (`ConexaoViewModel`: senha, leitura, só leitura, "Mostrar o registro"). Os bindings servem de modelo; vira a parte Conectar e senha do Início/Conectar no visual novo, sem perder nenhuma função (inclusive fechar o teclado ao enviar a senha).
- `app-controle/src/BgsControle.App/MauiProgram.cs` -- registro dos serviços e ViewModels (singletons; as páginas também).
- `app-controle/src/BgsControle.Telas/ControleParametroViewModel.cs` (`FabricaControles.Criar(parametro, indice, grupo)`, `Aumentar`, `Diminuir`, `ComecarSegurar`, `Arrastando`, `Soltou`, `AbrirCampo`, `Confirmar`, `Escolher`, `AlternarOff`) -- o controle de valor de todas as telas.
- `app-controle/src/BgsControle.Telas/LinkViewModel.cs` -- Link 1–2 e 3–4 com a folha de aviso (Crossover › Avançado).
- `app-controle/src/BgsControle.Telas/SetupViewModel.cs`, `MemoriasViewModel.cs`, `SalvarViewModel.cs`, `ConexaoViewModel.cs` -- prontos (8c, 8d, 1); as telas só os ligam.
- `app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs` -- `Botoes`, `EmUso`, `SetupEmUso`, `UltimoSetup`, `Estado`, `EmEscolha`, `MemoriaEmUso`, `PresetEmUso`, `AjustarEquilibrioAsync`, `EquilibrioEfetivo`, `RenomearSetupAsync`, `ApagarSetupAsync`, `Mudou`: a base dos ViewModels de Início, Mesa, Ajuste e Setups.
- `app-controle/src/BgsControle.Nucleo/Escrita/CopiaAntes.cs` -- `PelaMetade`, `TerminarAsync`, `VoltarAsync` (a folha "Aplicado pela metade" já tem lógica na `MemoriasViewModel`; reaproveitar).
- `app-controle/src/BgsControle.Telas/TelaBase.cs`, `Comando.cs`, `Abstracoes.cs` (`IDespachante`), `FormatoValor.cs` -- a base dos ViewModels e a formatação dos valores.
- `app-controle/tests/BgsControle.Telas.Testes/ControleParametroViewModelTestes.cs:20` (`Bancada`) e `Falsos.cs` -- a montagem de teste dos ViewModels contra o simulador.
- `_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/mockups/key-screens-3b.html`, `key-screens.html`, `direction-sintese-v2.html` -- os desenhos de referência citados no `EXPERIENCE.md`.

## Tasks & Acceptance

**Execution:**
- `app-controle/src/BgsControle.Telas/InicioViewModel.cs` -- seletor de Setup ("Setup Casa 2.1 ›", "Sem Setup ›"), cartão da memória em uso, chip (texto), 3 botões e "…", lista de memórias, cartões das três saídas (ganho e limitador lidos, "—" sem leitura), estado de conexão -- a tela Início.
- `app-controle/src/BgsControle.Telas/MesaViewModel.cs` -- quatro faders (três saídas e MASTER) pela `FabricaControles`, com os botões de memória do Início.
- `app-controle/src/BgsControle.Telas/AudioViewModel.cs` e `FuncaoViewModel.cs` -- a grade (rótulo e valor-resumo de cada bloco, Gerador travado) e uma tela de função genérica com abas de saída e os controles "à vista" e "Avançado" de cada função, pela tabela do `EXPERIENCE.md`.
- `app-controle/src/BgsControle.Telas/AjusteSaidaViewModel.cs` -- os controles de uma saída (à vista e Avançado), o equilíbrio na JBL L e na JBL R, o título com a cor da saída.
- `app-controle/src/BgsControle.Telas/SetupsViewModel.cs` -- Configurações › Setups: lista, selo, renomear, apagar com folha e as travas (em uso, herdado).
- `app-controle/src/BgsControle.App/` (páginas `.xaml` e `.xaml.cs`, uma por tela ou folha da lista acima, mais `Recursos/` ou o `App.xaml` com os tokens) -- a casca, a navegação e as telas; a fonte do `DESIGN.md` embutida em `Resources/Fonts` se ele a pedir.
- `app-controle/src/BgsControle.App/MauiProgram.cs` -- registrar páginas e ViewModels novos.
- `app-controle/tests/BgsControle.Telas.Testes/` -- testes dos ViewModels novos contra o simulador, cobrindo a matriz (a parte que vive no ViewModel: textos, travas, o que é escrito, navegação pedida).

**Acceptance Criteria:**
- Given o app compilado para Android, when o build roda, then não há erro nem aviso (inclusive de binding não compilado).
- Given o simulador na FILME e o Casa 2.1 conferido, when o `InicioViewModel` é lido, then ele mostra "Setup Casa 2.1 ›", "FILME", "Preset: Cinema" e os ganhos lidos das três saídas.
- Given uma tela de função ou o Ajuste, when o Denis mexe num controle, then o valor vai ao aparelho pela trava da sessão e a tela mostra o valor relido.
- Given o `GerenteSetups` em dúvida, when isso acontece com qualquer tela aberta, then a casca abre a tela Setup em dúvida por cima e nada mais fica tocável até a escolha.

## Spec Change Log

## Review Triage Log

### 2026-10-23 — Review pass
- verdicts: 47 findings — high 0, medium 10, low 31, false 5, maybe-false 1
- intent_gap achado: as entradas mais baixas ficam moot; tentativa salva em `_bmad-output/implementation-artifacts/historia-10-tentativa-1.patch` e o código voltou ao commit da 8d.
- findings:
  - `[medium]` `[intent_gap]` (intenção) Com o Link 3–4 desligado, o Sub (cartão, Ajuste, Mesa, abas) só alcança a saída 3; a saída 4 fica sem ajuste pelas telas (CAP-3: todos os parâmetros por canal) — o EXPERIENCE.md diz que este caso "não está desenhado" ([NOTE FOR UX] do estado Link ligado), e a spec só disse "Sub = saídas 3 e 4"; há mais de uma leitura (abas Sub 3 / Sub 4, a aba Sub escrever nas duas, ou o Sub ser só a saída 3 na ponte); decisão do Denis
  - `[false]` `[reject]` (intenção) Os testes ficam nos ViewModels e não rodam as páginas — sem celular no adb e sem harness MAUI; a spec registra a conferência visual como pendente
  - `[low]` `[reject]` (intenção) EQ 15 bandas aberto na tela de função genérica, embora o UX diga que o layout não foi desenhado — leitura defensável; o mesmo padrão das outras telas de função
  - `[low]` `[patch]` (intenção) O comentário de documentação da ConexaoViewModel passou para o record CaixaSenha — moot: o código voltou com a lacuna de intenção
  - `[false]` `[reject]` (intenção) Três abas e duas linhas de Configurações — é a divisão com a história 11, escrita na spec
  - `[false]` `[reject]` (intenção) Criar e renomear memória testados só nos testes antigos — a MemoriasViewModel já tinha esses testes (8c/8d)
  - `[false]` `[reject]` (intenção) Avisos da CAP-14 testados como texto — o texto é o que a tela liga; a tela não roda nos testes
  - `[low]` `[patch]` (lacuna) Equilíbrio sem teste de toques seguidos e de segurar — moot
  - `[low]` `[patch]` (lacuna) Opção escolhida (ItensOpcao.Escolhida) sem teste — moot
  - `[low]` `[patch]` (lacuna) Pílula tocada desconectada sem teste — moot
  - `[low]` `[patch]` (lacuna) "Caiu às" e a contagem da leitura sem teste — moot
  - `[low]` `[patch]` (lacuna) Criar memória do som atual pelo Início sem teste — moot
  - `[low]` `[patch]` (lacuna) Resumos da grade Áudio sem teste e sem atualização ao vivo testada — moot
  - `[medium]` `[defer]` (lacuna) Voltar do Android na dúvida e "nada mais tocável" sem verificação automática — precisa do aparelho (história 12)
  - `[low]` `[patch]` (lacuna) Tocar na memória em uso na lista "…" não fecha a folha — moot
  - `[low]` `[patch]` (lacuna) Comentário de documentação da ConexaoViewModel — moot
  - `[medium]` `[patch]` (cego) Tipo.cs troca a fonte de todo texto e perde o FontFamily monospace do registro; comentário diz Android 12, código testa 28 — moot
  - `[low]` `[patch]` (cego) Tipo.cs usa Android.* fora de Platforms/Android — moot
  - `[medium]` `[patch]` (cego) Registro do BLE e Esquecer a senha inacessíveis desconectado; ConexaoPage sem o indicador de ocupado — moot
  - `[medium]` `[intent_gap]` (cego) Saída 4 sem ajuste com o Link 3–4 desligado — a mesma lacuna da intenção
  - `[low]` `[patch]` (cego) Canais da Mesa não mostram o motivo de trava nem o aviso — moot
  - `[low]` `[patch]` (cego) Doc da ConexaoViewModel — moot
  - `[medium]` `[patch]` (cego) Equilíbrio pode perder o último valor pedido — moot
  - `[medium]` `[patch]` (cego) ComandoCom<T> async void sem try/catch e sem trava de duplo toque — moot
  - `[low]` `[patch]` (cego) Linha da memória em uso não fecha a lista — moot
  - `[maybe-false]` `[reject]` (cego) Campo de edição pode não descartar com o Voltar do teclado — só no aparelho; se real, low
  - `[low]` `[patch]` (cego) Splash ainda no azul antigo — moot
  - `[low]` `[reject]` (cego) O teste de cor solta não pega cores nomeadas nem as do C# — melhoria do teste
  - `[low]` `[reject]` (cego) Mesmo ícone em todos os blocos da grade — simplificação registrada
  - `[low]` `[reject]` (cego) Tamanho do nome da memória por número de letras — simplificação registrada
  - `[low]` `[reject]` (cego) Unidades fixas nos resumos — o catálogo não muda
  - `[false]` `[reject]` (cego) A spec não tem o resultado da verificação — o resultado vai no Auto Run Result, escrito no fim
  - `[medium]` `[patch]` (borda) Equilíbrio: passo novo entre a última conferência e o NaTela é perdido — moot
  - `[low]` `[patch]` (borda) Fader travado no meio do arrasto não solta — moot
  - `[low]` `[patch]` (borda) Giratório travado no meio do arrasto não solta — moot
  - `[low]` `[patch]` (borda) Pílula desconectada na Mesa ou no Áudio pede a senha no Início escondido — moot
  - `[low]` `[patch]` (borda) Memória em uso na lista "…" não fecha — moot
  - `[low]` `[reject]` (borda) Renomear Setup que lança deixa a folha sem erro — o núcleo não lança nesse caminho
  - `[low]` `[reject]` (borda) Confirmar duas vezes em Setups — o núcleo já recusa com "ocupado"
  - `[low]` `[patch]` (borda) Tela de função não ouve os nomes das memórias do aparelho — moot
  - `[low]` `[patch]` (borda) Resumo do Crossover esconde o passa-baixa quando os dois estão ligados — moot
  - `[low]` `[patch]` (borda) Frequência mostrada com o tipo do filtro ainda não lido (função) — moot
  - `[low]` `[patch]` (borda) Frequência mostrada com o tipo do filtro ainda não lido (Mesa) — moot
  - `[medium]` `[patch]` (borda) Tipo.cs perde o FontFamily — moot
  - `[low]` `[patch]` (borda) Empurrar com modal aberto não avisa — moot
  - `[medium]` `[patch]` (borda) Funções da MainPage perdidas desconectado — moot
  - `[low]` `[patch]` (borda) Depois da dúvida a casca não volta ao Início, como a matriz pede — moot

## Design Notes

- **Divisão com a história 11:** esta parte cobre o que o teste 1 e o teste 4 do sinal de sucesso usam (conectar, ajustar, salvar, memórias, Setups). O Player, os presets, o gerador e a IA ficam com a 11, que acrescenta a aba Player e as linhas de Configurações.
- **Sem aparelho nesta verificação:** não há celular no adb agora. A prova é o build com bindings compilados e os testes dos ViewModels; a conferência visual fica para o Denis (ou para a história 12).

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- Com o celular no adb (`C:\platform-tools\adb.exe`), instalar o APK e conferir as telas contra os mocks; sem celular, registrar que a conferência visual não foi feita.

## Auto Run Result

Status: blocked
Blocking condition: intent gap

**Pergunta para o Denis:** com o Link 3–4 desligado, como as telas tratam as saídas 3 e 4 do Sub?
- (a) **Recomendado:** abas separadas "Sub 3" e "Sub 4" nas telas de função e no Ajuste, e o cartão do Início e a Mesa mostram a saída 3 com a nota da 4. É o que respeita a CAP-3 (todos os parâmetros por canal) sem mexer em nada que o Denis não pediu.
- (b) A aba Sub escreve nas duas saídas, como se o link estivesse ligado.
- (c) O Sub é só a saída 3, porque o módulo em ponte usa só uma entrada; a saída 4 fica fora das telas.

Junto: o Setup herdado "Casa 2.1" nasceu na 8c com o Link 3–4 desligado. Se no 2.1 o link fica ligado, ele precisa ser corrigido.

**O que foi feito antes de parar:**
- **Implementação completa:** casca, Início, Conectar, Mesa, Áudio e as 9 telas de função, Ajuste da saída com equilíbrio, Setup em dúvida, lista "…", folha do Salvar, "Aplicado pela metade", Configurações, Setups e Memórias.
- **ViewModels e testes:** 7 ViewModels novos, 916 + 358 testes passando, e o build do Android com 0 erros e 0 avisos, com o XamlC validando os bindings no Debug.
- **Revisão:** 47 achados; além da lacuna, várias correções pequenas ficaram anotadas no triage log.
- **Tentativa salva:** `_bmad-output/implementation-artifacts/historia-10-tentativa-1.patch`. O `git apply --check` passa contra o commit `e51bf93`.

**Para retomar:** depois da resposta, aplicar o patch, ajustar o Sub conforme a escolha e corrigir os achados `patch` deste log; não é preciso refazer as telas do zero.

**Verificação:** nenhuma tela foi vista num celular (não havia aparelho no adb).
