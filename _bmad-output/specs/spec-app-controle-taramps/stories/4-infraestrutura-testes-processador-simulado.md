---
title: 'História 4: infraestrutura de testes com o processador simulado'
type: 'chore'
created: '2026-10-21'
status: 'done'
route: 'dispatch'
baseline_commit: '907ba83a8a3b6ea8c9680201f572238bc43f02d1'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/plano-resto-poc.md'
  - '{project-root}/protocolo/MAPA.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** As histórias 5 a 11 precisam ser desenvolvidas sem o processador. Hoje a lógica da tela (`ConexaoViewModel`) depende do MAUI e não tem teste. A leitura automática ao entrar em Conectado também não tem teste (item de `deferred-work.md`). E não existe um processador simulado que responda como o aparelho real.

**Approach:** Tirar os ViewModels do MAUI para um projeto `net10.0` testável. Descer para o núcleo a regra "ler uma vez ao entrar em Conectado". Criar um `ProcessadorSimulado` que vive **só nos testes** e imita o formato real do PRO 2.4BT. O comportamento do app não muda.

**Decisões do Denis (21/10/2026):** o mock fica só nos testes, nunca no APK. O Denis aprova só as decisões abertas.

## Boundaries & Constraints

**Always:**
- **Projeto `src/BgsControle.Telas` (net10.0, sem MAUI).** O `ConexaoViewModel` vem para cá com o mesmo comportamento. Ele recebe abstrações no lugar do MAUI:
  - `IDespachante`, para pôr na thread da tela;
  - `Comando : ICommand` próprio;
  - `IArquivosApp`, para ler e gravar nos arquivos privados;
  - `IPreparoBluetooth.PrepararAsync()`, que devolve null quando está tudo pronto, ou a mensagem do que falta (sem permissão, BT desligado);
  - `TimeProvider`, para os prazos.
- **O App fica só com o XAML e os adaptadores:** `DespachanteMaui` (MainThread), `ArquivosMaui` (`FileSystem.AppDataDirectory`) e `PreparoBluetoothAndroid` (o mesmo fluxo de hoje com `PermissaoBluetooth` e `PedidoLigarBluetooth`). O `x:DataType` do `MainPage.xaml` aponta para o assembly novo.
- **A leitura automática desce para o núcleo.** Um componente do núcleo dispara `LeitorCompleto.LerAsync` uma vez a cada entrada em Conectado. Ele não dispara de novo enquanto continua em Conectado, nem enquanto outra leitura roda. O ViewModel só observa o resultado e salva o retrato.
- **`tests/BgsControle.Simulador`** (biblioteca net10.0, referenciada só por projetos de teste): `ProcessadorSimulado : ITransporte`.
  - **Estado:** um dicionário `(código, índice) → valor no fio`. Ele é semeado com o `cru` de `estado/leituras/2026-10-18_2145.json` (arquivo linkado, sem cópia) e com os extras reais da história 2: `231#SPL`, `232#MUSICA ORIG`, `233#FILME`, `#111#0#`, `#150#0#`, `#200#-25.0#`, `#201#51#`, `#202#0#`, `#210#10#`, `#211#22000#`, `#212#-45.0#`, `#213#0#`, `#251#1#`, `#252#1#`. O voltímetro `#005#` responde `ERRO 0`.
  - **Senha:** configurável. A certa responde `#000#PRO2.4MIR003#` (o firmware também é configurável) e a errada responde `#000#ERRO#`. Depois de um `ERRO`, a conexão cai sozinha num prazo configurável, 29 s por padrão, que é o valor medido em 20/10.
  - **Leitura:** responde no formato real (MAPA §2 e §9): `#CCC#[c#]v#\r\n`, a banda como `020#b#v` e os nomes como `23N#NOME`.
  - **Escrita** (`*CCC*[c*]v*`, `020*b*v`): valida pelo `Catalogo`, aplica e devolve o eco no formato de leitura depois de ~85 ms. Nunca manda `OK`.
  - **Memórias:** `*240*n*` salva, `*241*n*` carrega e `*23N*nome*` nomeia.
  - **Outros comandos:** gerador, 251, 252, `*242*` (reset de áudio), `*004*` (fábrica), `*001*antiga*nova*`.
  - **Falhas injetáveis:** queda na N-ésima escrita, silêncio, eco diferente, fragmentos de até 20 bytes e órfãos.
  - **Registro:** a lista `Escritas` guarda o que chegou.
- **Suposições não medidas** ficam marcadas no código (`// SUPOSIÇÃO:`) e listadas em `tests/BgsControle.Simulador/SUPOSICOES.md`, que a história 12 confere. Exemplos: o ack de 240, 241, 23x, 252, 001 e 004, e o estado depois do reset.
- **`tests/BgsControle.Telas.Testes` (xUnit).** O `ConexaoViewModel` é testado contra o simulador, e há um teste de arquitetura: o csproj do App não referencia o Simulador.
- **`BgsControle.slnx`** ganha os três projetos. Os testes de `BgsControle.Nucleo.Testes` continuam passando sem mudança de comportamento.

**Never:**
- O Simulador no APK ou referenciado por `src/`.
- Liberar escrita na trava (é a história 5) ou mudar telas e textos visíveis.
- Tocar em `ferramentas/`, `estado/` ou `protocolo/`.

## I/O & Edge-Case Matrix

| Cenário | Entrada / estado | Esperado | Erro |
|---|---|---|---|
| Caminho feliz | Simulador com a senha certa | ViewModel: pede a senha, envia, chega a Conectado R003; leitura automática 84 de 84; retrato salvo por `IArquivosApp` | — |
| Uma leitura por entrada | Vários `Mudou` em Conectado | Uma leitura só | — |
| Reentrada | Desconecta e conecta de novo | Uma leitura nova | — |
| Senha errada | `ZZZZ` | "Senha errada…"; a conexão é solta; nenhuma escrita além da senha | — |
| Queda do simulador depois do ERRO | 29 s (tempo falso) | A sessão segue pedindo a senha | — |
| Sem permissão / BT desligado | `IPreparoBluetooth` devolve a mensagem | Mensagem na tela; nada é procurado | — |
| Escrita no simulador | `*081*0*-3.0*` direto no simulador | O estado muda; o eco `#081#0#-3.0#` chega | — |
| Escrita inválida | `*081*9*-3.0*` | Não aplica; sem eco | Registrada |
| Fragmentos | Modo de 20 bytes | O leitor junta e lê 84 de 84 | — |

</frozen-after-approval>

## Code Map

- `app-controle/src/BgsControle.App/ConexaoViewModel.cs`: usa `MainThread`, `Command`, `FileSystem` e `Permissions`/`PedidoLigarBluetooth` (l. 38–62, 202, 252–262). Vai para Telas.
- `app-controle/src/BgsControle.App/MauiProgram.cs`, `MainPage.xaml(.cs)`, `App.xaml.cs`: o registro no DI e o `x:DataType`.
- `app-controle/src/BgsControle.Nucleo/Leitura/LeitorCompleto.cs` (`LerAsync`, `Avancou`, `Lendo`) e `Sessao/Sessao.cs` (`Mudou`, `Estado`, `Recebeu`): base da leitura automática.
- `app-controle/tests/BgsControle.Nucleo.Testes/Falsos.cs` (`TransporteFalso`) e `.csproj` (o link do JSON real): modelo para o simulador. O `TransporteFalso` continua existindo.
- `app-controle/src/BgsControle.Nucleo/Catalogo/` (`Catalogo.ConsultaDe`, `Parametro.CodificarEscrita`/`Decodificar`) e `Protocolo/Quadros.cs` (`Analisar`, `MontadorQuadros`): reusar no simulador.

## Tasks & Acceptance

**Execution:**
- [x] `app-controle/src/BgsControle.Telas/`: o projeto, as abstrações e o `ConexaoViewModel` movido.
- [x] `app-controle/src/BgsControle.Nucleo/Leitura/`: a leitura automática ao entrar em Conectado.
- [x] `app-controle/src/BgsControle.App/`: os adaptadores MAUI, o DI e o `x:DataType`.
- [x] `app-controle/tests/BgsControle.Simulador/`: o `ProcessadorSimulado`, a semente e o `SUPOSICOES.md`.
- [x] `app-controle/tests/BgsControle.Telas.Testes/`: as linhas da matriz e o teste de arquitetura.
- [x] `app-controle/BgsControle.slnx` e `README.md`: os projetos novos e como rodar os testes.

**Acceptance Criteria:**
- Given o simulador, when o ViewModel conecta com a senha certa, then a leitura automática dá 84 de 84 e o retrato é salvo, sem nenhum MAUI no teste.
- Given o build do App, when as referências são inspecionadas, then nenhum projeto de `src/` referencia `BgsControle.Simulador`.
- Given os testes antigos do núcleo, when rodam, then todos passam.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` e `dotnet test app-controle/tests/BgsControle.Telas.Testes`: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug`: 0 erros e 0 avisos.

## Implementation Notes

- 21/10: `LeituraAutomatica` no núcleo dispara a leitura a cada entrada em Conectado e serve também ao "Ler de novo" (eventos Comecou/Avancou/Terminou/Falhou). O ViewModel guarda um contador de leitura para que o fim de um salvamento antigo não encerre a leitura nova.
- `Comando` próprio com `ExecutarAsync()` para os testes. `RelogioFalso : TimeProvider` escrito à mão (o pacote FakeTimeProvider não está no cache local).
- O teste "vários Mudou" dispara o evento `Sessao.Mudou` por reflexão (a sessão não tem um jeito público de repetir o evento em Conectado); falha alto se o campo mudar de nome.
- 15 suposições não medidas do simulador em `app-controle/tests/BgsControle.Simulador/SUPOSICOES.md` (acks de 240/241/23N/242/004/001, senha de fábrica `1234`, eco com sinal, só o texto exato do codificador, nada antes da senha).
- O item de `deferred-work.md` sobre a leitura automática sem teste está coberto por `ConexaoViewModelTestes`.
- Verificação: Nucleo.Testes 288/288, Telas.Testes 63/63 (estável em 5 rodadas), build Android 0 erros e 0 avisos; o APK tem Telas e não tem Simulador.
- Revisão (21/10): 7 correções aplicadas (leitura pendente quando a reentrada chega antes de a anterior soltar; `Terminou`/`Falhou` antes de soltar; testes da guarda, do progresso ao vivo, do `Falhou` e da queda de 29 s no simulador; ecos atrasados em ordem). Verificação final: Nucleo.Testes 288/288, Telas.Testes 71/71, build Android 0 erros e 0 avisos.
- **Pendente:** o teste rápido no celular (instalar, abrir, conectar com a senha guardada e ver a leitura automática) não foi feito porque o celular não estava ligado ao notebook. Fazer no próximo contato com o aparelho, ou na história 12.

## Spec Change Log

## Review Triage Log

| # | Achado | Veredito | Evidência | Rota |
|---|---|---|---|---|
| 1 | Reentrada em Conectado durante o fim da leitura anterior perde a leitura automática | medium | `_conectadoAntes` é consumido e `LerAsync` volta null; nada tenta de novo | patch |
| 2 | `Soltar()` antes de `Terminou`: o `Comecou` novo pode passar à frente do `Terminou` antigo | low | Janela pequena, mas a correção é só trocar a ordem | patch |
| 3 | Guarda "não ler enquanto outra roda" sem teste | medium | Nenhum teste chama `LerAsync` com leitura ativa | patch |
| 4 | Progresso ao vivo e limpeza do aviso no começo sem teste | medium | As asserções rodam depois do `Terminou` | patch |
| 5 | Caminho `Falhou` sem teste | medium | Nenhuma injeção faz o leitor lançar | patch |
| 6 | Teste dos 29 s não exercita a queda do simulador | medium | O app solta o link antes; `Quedas == 0` | patch |
| 7 | Ecos atrasados podem chegar fora de ordem | medium | `ContinueWith` independentes no pool; vai afetar a história 5 | patch |
| 8 | Salvamento antigo terminando durante leitura nova (contador) sem teste | low | Só alcançável com gravação mais lenta que uma reconexão | defer |
| 9 | DI e adaptadores MAUI só verificados pelo build | medium | Teste net10.0 não alcança o MAUI | defer (conferido no celular nesta história) |
| 10 | `Comecou` que lança deixa `_lendo` preso | low | O único assinante só agenda na thread da tela | rejeitado |
| 11 | `TimeProvider` injetado e sem uso | low | Fica para os prazos das histórias 7 e 9a | rejeitado |
| 12 | Asserções de "nada aconteceu" com espera real | low | Passam hoje; só fragilidade | rejeitado |
| 13 | Item do deferred-work não fechado | false | O fluxo proíbe editar entradas existentes; o fechamento está nas notas | rejeitado |
| 14 | Teste de arquitetura só olha referências diretas | low | Improvável vir por .props | rejeitado |
| 15 | Falhas injetáveis e suposição 13 sem teste | low | Código de teste; a 13 entra no item 6 | rejeitado |
| 16 | Contadores e coleções do simulador sem trava | low | Só nos testes | rejeitado |
| 17 | `DispararMudou` por reflexão | low | Falha alto se mudar | rejeitado |
| 18 | `LeitorCompleto` público no DI | low | Nenhum outro chamador hoje | rejeitado |
| 19 | `LerTextoAsync` sem uso; gravação não atômica | low | Decisão da história 2 (último retrato) | rejeitado |
| 20 | `Comando` aceita toque duplo | false | `Sessao.ConectarAsync` e `LerAsync` já se guardam | rejeitado |
| 21 | `DespachanteMaui` roda direto quando já está na thread da tela | low | Mesmo comportamento do app antes da mudança | rejeitado |
| 22 | Casos de borda do `RelogioFalso`, `Memoria(n)` e overflow do fragmento | low | Só nos testes | rejeitado |
| 23 | Nome vazio depois do `*004*` não decodifica | false | `231#` sem segmentos vira nome vazio pelo `Casar` da história 2 | rejeitado |
