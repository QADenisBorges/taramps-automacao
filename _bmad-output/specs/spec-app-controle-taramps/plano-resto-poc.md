# Plano: resto do POC 1 sem o processador (mock nos testes, integração no fim)

## Context

As histórias 1 (conectar e autenticar) e 2 (catálogo e leitura completa, 84 de 84 no aparelho) estão no `main`. O Denis quer adiantar tudo o que não precisa do PRO 2.4BT. Isso inclui os pedaços 2 a 4 do SPEC (CAP-3 a CAP-14). O desenvolvimento usa um processador simulado, e a **última história** liga tudo no aparelho real e roda os testes.

Decisões do Denis (21/10):
- **O mock vive só nos testes.** Ele não entra no APK.
- **Entram os pedaços 2 a 4 completos.**
- **As telas saem de um trabalho de UX design** (bmad-ux: DESIGN.md e EXPERIENCE.md) antes de ser implementadas.
- **Ele só aprova decisões abertas.** Cada história segue o bmad-build (spec → implementar → revisar → commit) sem parar para aprovar a spec.

## Abordagem

### 1. Telas testáveis sem MAUI
- Novo projeto `app-controle/src/BgsControle.Telas` (net10.0, sem MAUI) para os ViewModels.
- Ele abstrai quatro coisas do MAUI:
  - `IDespachante`, para a thread da tela;
  - um `Comando : ICommand` próprio;
  - `IArquivosApp`, para os arquivos privados do app;
  - `IPreparoBluetooth`, para as permissões e o pedido de ligar o BT.
- Os prazos usam `TimeProvider`.
- O `ConexaoViewModel` sai de `src/BgsControle.App/ConexaoViewModel.cs` para esse projeto. A regra "ler uma vez ao entrar em Conectado" desce para o núcleo. Isso fecha o item de `_bmad-output/implementation-artifacts/deferred-work.md`.
- O App fica só com o XAML e com `DespachanteMaui`, `ArquivosMaui` e `PreparoBluetoothAndroid`.
- Projetos novos no `BgsControle.slnx`:
  - `tests/BgsControle.Simulador` (biblioteca), referenciado só pelos testes;
  - `tests/BgsControle.Telas.Testes` (xUnit).
- Um teste de arquitetura garante que o App não referencia o Simulador.

### 2. Simulador (`ProcessadorSimulado : ITransporte`, só nos testes)
- **Estado:** um dicionário `(código, índice) → valor no fio`, semeado com o `cru` de `estado/leituras/2026-10-18_2145.json` e com os extras reais da história 2 (`231#SPL`, `#200#-25.0#`, `#252#1#`, voltímetro `ERRO 0`…).
- **Leituras e escritas:** respondem no formato real do MAPA §2 e §9. O eco da escrita vem no formato de leitura, depois de ~85 ms, sem `OK`.
- **O que ele imita:** as memórias 240 e 241, os nomes 23N, o gerador (com a linha do tempo de 202), o clip espontâneo, o 251 e o 252, e os comandos 004, 242 e 001.
- **Falhas injetáveis:** queda, silêncio, eco diferente do pedido, fragmentos de 20 bytes e órfãos.
- **Suposições marcadas.** O ack de 240, 241, 23x, 252, 001 e 004 não foi medido. Cada suposição fica marcada no código e entra numa lista que a última história confere.
- Reusa `Catalogo` (`app-controle/src/BgsControle.Nucleo/Catalogo/`) para validar e codificar, e `Quadros.Analisar`/`MontadorQuadros` (`Protocolo/Quadros.cs`).

### 3. Histórias (reescrever `_bmad-output/specs/spec-app-controle-taramps/stories.yaml`)
As antigas histórias 3 (telas de leitura) e 4 (paridade) são absorvidas pelas novas 10–11 e 12.

| # | Escopo | CAPs |
|---|---|---|
| 3 | **UX design**, com o Denis: DESIGN.md e EXPERIENCE.md de todas as telas | todas |
| 4 | **Infraestrutura:** projeto Telas, Simulador, ConexaoViewModel movido e testado, leitura automática no núcleo | 1, 2 |
| 5 | **Escrita:** a trava passa a aceitar a escrita do catálogo com a chave ligada (e fora do modo só leitura), dentro de `Sessao.Trava`, que continua a porta única; escritor com eco e releitura, 30 ms entre escritas e 300 ms por parâmetro; espelho do estado; chaves de acesso persistidas, com aviso do risco | 3, 11 |
| 6 | **Memórias e link:** salvar, carregar com releitura e nomear; link 1–2 e 3–4; cópia antes de mexer e "aplicado pela metade"; aviso ao sobrescrever | 3, 4, 14 |
| 7 | **Gerador e aparelho:** gerador com prazo de 30 s e regras de falha 1 e 2; voltímetro e clip; remote out e BT de áudio; reset de fábrica, reset de áudio, troca de senha e nome do produto | 7, 8, 9, 10 |
| 8 | **Presets e conhecimento herdado:** presets no celular e o conhecimento herdado embutido | 5, 6 |
| 9a | **Uso compartilhado:** histórico, estado ao vivo entre a tela e a IA, pausa e janela de 10 s | 13 |
| 9b | **Porta da IA:** HTTP, pareamento de 6 dígitos, só a rede local, confirmação na tela, "Parar IA" | 12 |
| 10 | **Telas, parte 1** (pelo UX): leitura, ajustes, link e memórias | 2, 3, 4, 14 |
| 11 | **Telas, parte 2** (pelo UX): gerador, monitoração, aparelho, chaves, presets, IA e histórico | 5, 7–13 |
| 12 | **Integração e testes no aparelho:** conferir a lista de suposições do simulador; paridade nas 3 memórias; testes 1, 2 e 3 do sinal de sucesso; voltímetro contra o multímetro; gerador só com o RCA dos módulos desconectado; Wi-Fi real; o reset de fábrica testado só na recusa, a menos que o Denis decida outra coisa | todas |

- **Cada história do núcleo (5 a 9b)** entrega o seu ViewModel testado contra o simulador. As histórias 10 e 11 ficam com o XAML, a navegação e o acabamento.
- **Se uma spec passar muito do tamanho** (a 7 é a candidata), divido.

### 4. Ordem e paralelismo
- **A 3 (UX) é interativa com o Denis e roda nesta sessão.** A 4 não depende da 3: eu proponho abri-la numa sessão paralela, no seu próprio worktree.
- As histórias 5 a 9b vêm em sequência, depois da 4, porque mexem nos mesmos arquivos (`Sessao.cs`, `Telas`).
- As histórias 10 e 11 esperam a 3 e a 9b.
- A 12 é a última. Ela precisa do celular e do processador.

## Arquivos críticos
- `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs`: a trava (história 5).
- `app-controle/src/BgsControle.App/ConexaoViewModel.cs`: vai para `src/BgsControle.Telas` (história 4).
- `app-controle/tests/BgsControle.Nucleo.Testes/Falsos.cs`: fica para os testes de baixo nível; o Simulador é novo.
- `app-controle/src/BgsControle.Nucleo/Catalogo/Parametro.cs`: o codificador de escrita já existe e é reusado.
- `_bmad-output/specs/spec-app-controle-taramps/stories.yaml`: reescrito com as histórias 3 a 12.

## Verificação
- **Em cada história:**
  - `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` e `dotnet test app-controle/tests/BgsControle.Telas.Testes` passam.
  - `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` dá 0 erros e 0 avisos.
  - Revisão com os três revisores do bmad-build e commit local.
- **No fim de cada pedaço:** merge no `main` e push, como o Denis pediu nas histórias 1 e 2.
- **História 12:** o APK no Galaxy A52 com o processador; a leitura 84 de 84 e a paridade valor a valor nas 3 memórias; a MUSICA ORIG refeita só pelo celular; a IA lendo, ajustando e salvando pela porta, com a tela ao vivo.
