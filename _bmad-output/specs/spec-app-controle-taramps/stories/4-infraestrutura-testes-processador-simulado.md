---
title: 'História 4: infraestrutura de testes com o processador simulado'
type: 'chore'
created: '2026-10-21'
status: 'ready-for-dev'
route: 'dispatch'
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
- [ ] `app-controle/src/BgsControle.Telas/`: o projeto, as abstrações e o `ConexaoViewModel` movido.
- [ ] `app-controle/src/BgsControle.Nucleo/Leitura/`: a leitura automática ao entrar em Conectado.
- [ ] `app-controle/src/BgsControle.App/`: os adaptadores MAUI, o DI e o `x:DataType`.
- [ ] `app-controle/tests/BgsControle.Simulador/`: o `ProcessadorSimulado`, a semente e o `SUPOSICOES.md`.
- [ ] `app-controle/tests/BgsControle.Telas.Testes/`: as linhas da matriz e o teste de arquitetura.
- [ ] `app-controle/BgsControle.slnx` e `README.md`: os projetos novos e como rodar os testes.

**Acceptance Criteria:**
- Given o simulador, when o ViewModel conecta com a senha certa, then a leitura automática dá 84 de 84 e o retrato é salvo, sem nenhum MAUI no teste.
- Given o build do App, when as referências são inspecionadas, then nenhum projeto de `src/` referencia `BgsControle.Simulador`.
- Given os testes antigos do núcleo, when rodam, then todos passam.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` e `dotnet test app-controle/tests/BgsControle.Telas.Testes`: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug`: 0 erros e 0 avisos.

## Implementation Notes

## Spec Change Log

## Review Triage Log
