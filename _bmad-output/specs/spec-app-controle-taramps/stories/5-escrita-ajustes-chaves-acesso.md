---
title: 'História 5: escrita de ajustes e chaves de acesso'
type: 'feature'
created: '2026-10-21'
status: 'done'
route: 'dispatch'
baseline_commit: 'bf6259a2888b879fc3cf4a0cc77796f2bc09d2dd'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/chaves-de-acesso.md'
  - '{project-root}/protocolo/MAPA.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Hoje o app só lê: a trava recusa toda escrita. Para regular pelo celular (CAP-3), ele precisa escrever os ajustes de áudio do MAPA §4 com segurança. Cada operação de risco precisa ter uma chave de acesso que o usuário liga e desliga (CAP-11).

**Approach:** Abrir a trava só para a escrita de ajustes de áudio do catálogo, sempre pela porta única da sessão. Criar no núcleo um escritor que espera o eco e relê, um espelho do estado do aparelho e as chaves de acesso persistidas. Em `BgsControle.Telas`, criar o controle de um parâmetro (arrastar, escrever, − e +) e a tela das chaves, os dois testados contra o `ProcessadorSimulado`.

**Decisões do Denis (UX, 21/10/2026):**
- Mexer muda o som na hora.
- O valor escrito vale no ✓; um valor fora da faixa é recusado com a faixa escrita ao lado.
- O passo de − e + nas frequências é proporcional, 1/12 de oitava, com no mínimo 1 Hz.
- Segurar − ou + repete o passo.
- Ao ligar uma chave, o aviso do risco vem antes, numa folha que pede confirmação. Desligar não pede.

## Boundaries & Constraints

**Always:**
- **Trava (`Sessao.Trava`), a porta única.** Uma escrita só passa quando todas estas condições valem:
  - é exatamente o texto que `Parametro.CodificarEscrita` produz para um parâmetro de áudio do Marco 1;
  - a sessão está Conectada e fora do modo só leitura;
  - a chave "Ajustes de áudio" está ligada.

  Todo o resto continua recusado antes do BLE, com o motivo no log: memórias, gerador, 252, 251, 002, 004, 242, 001 e qualquer texto fora do catálogo. Esses comandos são das histórias 6 e 7.
- **Chaves de acesso (`IChaves`)**, com os 11 itens, os padrões e os riscos de `chaves-de-acesso.md`, guardados num repositório abstrato (JSON nos arquivos privados). A trava pergunta à `IChaves`. Ligar ou desligar uma chave tem uma origem (tela ou IA), e a origem IA é sempre recusada.
- **Escritor:**
  - Uma escrita por vez pela porta. Um intervalo de ~30 ms entre escritas.
  - **Arrasto:** no máximo uma escrita a cada 300 ms por parâmetro. Ao soltar, o último valor sempre vai.
  - **Confirmação:**
    - O eco no formato de leitura (`#CCC#c#v#`) confirma a escrita.
    - Sem eco em 2 s, o escritor relê o parâmetro.
    - Se o valor relido for diferente do pedido, vale o relido, e a tela mostra o relido.
    - Se nem a releitura responder, a escrita entra como falha, com o motivo.
- **Espelho do estado (`EstadoAparelho`):**
  - semeado pelo `Retrato` da leitura completa;
  - atualizado a cada eco ou releitura;
  - avisa mudança por parâmetro;
  - é o valor que as telas mostram.
- **`ControleParametroViewModel`** (Telas), um por parâmetro:
  - **Arrastar** (`Arrastando(valor)` / `Soltou()`) segue o ritmo do arrasto.
  - **− e +** andam o passo do parâmetro. Nas frequências, o passo é 1/12 de oitava, com no mínimo 1 Hz. **Segurar** repete o passo: começa depois de 0,4 s e acelera depois de 2 s, com o `TimeProvider`.
  - **Campo:** o texto aceita vírgula ou ponto. Só vale no ✓. Fora da faixa, é recusado com "−45,0 a +15,0 dB" (a faixa do parâmetro). Fechar sem ✓ descarta.
  - **Tipos de valor:**
    - **Opções** (tipo do filtro, OFF incluído) e **fase** (0/180) são escolhas diretas.
    - O **limitador** tem OFF (`0.5`) como opção direta.
  - **Travas na tela:** com a chave desligada ou em modo só leitura, o controle fica travado e diz por quê.
- **`ChavesViewModel`** (Telas):
  - a lista com o estado de cada chave;
  - "ligar" abre a folha de risco (confirmar ou desistir);
  - "desligar" age direto;
  - a chave "IA pela rede" aparece, mas a porta da IA é da história 9b.
- **Testes** (Nucleo.Testes e Telas.Testes) contra o `ProcessadorSimulado`. Os testes antigos que conferem "escrita bloqueada nesta versão do app" passam a conferir as regras novas.

**Never:**
- Escrever memórias, gerador, remote out, BT de áudio, nome, senha ou resets.
- Fazer o XAML das telas (histórias 10 e 11).
- Uma escrita que não passe por `Sessao.EnviarAsync`.

## I/O & Edge-Case Matrix

| Cenário | Entrada / estado | Esperado | Erro |
|---|---|---|---|
| Escrita simples | Ganho da saída 1 = −3,0, chave ligada | Sai `*081*0*-3.0*`; o eco confirma; o espelho mostra −3,0 | — |
| Arrasto | 20 valores em 1 s e solta | ≤ 4 escritas desse parâmetro e o último valor enviado | — |
| Eco diferente | O simulador ecoa −2,9 | Espelho e tela mostram −2,9 | — |
| Sem eco | Eco silenciado | Relê; se o relido vier, vale ele | Falha com motivo se a releitura também calar |
| Chave desligada | "Ajustes de áudio" desligada | Nada sai pelo BLE | "Chave desligada" |
| Só leitura | Firmware R004 | Nada sai | "Modo só leitura" |
| Fora do catálogo | `*240*0*`, `*202*1*`, `*004*` | Recusado antes do BLE | Motivo no log |
| Campo fora da faixa | "20" no ganho | Recusado, nada sai | "−45,0 a +15,0 dB" |
| Passo de frequência | + em 2000 Hz | 2119 Hz | — |
| Ligar chave | Ligar "Gerador" | Folha com "Um tom forte pode queimar o tweeter ou o sub."; só liga no confirmar | — |
| IA liga chave | Origem IA | Recusado | "A IA nunca liga nem desliga uma chave" |
| Persistência | Liga uma chave e recria | O estado continua | — |

</frozen-after-approval>

## Code Map

- `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs` (`Trava`, `EnviarAsync`, `Recebeu`, `SoLeitura`): abrir a escrita só nas condições acima.
- `app-controle/src/BgsControle.Nucleo/Catalogo/Parametro.cs` (`CodificarEscrita`, `Decodificar`, `Minimo`/`Maximo`/`Casas`/`Opcoes`/`Especiais`) e `Catalogo.cs`: reuso direto.
- `app-controle/src/BgsControle.Nucleo/Leitura/Retrato.cs`: semente do espelho. `LeitorCompleto.Casar` mostra como casar a resposta com o pedido.
- `app-controle/src/BgsControle.Telas/` (`TelaBase`, `Comando`, `IDespachante`, `IArquivosApp`): base das telas novas.
- `app-controle/tests/BgsControle.Simulador/ProcessadorSimulado.cs` (`Escritas`, `Silenciar`, `EcosDiferentes`, `RelogioFalso`): o simulador dos testes.

## Tasks & Acceptance

**Execution:**
- [x] `app-controle/src/BgsControle.Nucleo/Seguranca/`: `IChaves`, os padrões e o repositório.
- [x] `app-controle/src/BgsControle.Nucleo/Escrita/`: o escritor e o `EstadoAparelho`.
- [x] `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs`: a trava nova.
- [x] `app-controle/src/BgsControle.Telas/`: `ControleParametroViewModel` e `ChavesViewModel`.
- [x] `app-controle/src/BgsControle.App/MauiProgram.cs`: o registro no DI, sem tela nova.
- [x] Os testes: cada linha da matriz e os antigos atualizados.

**Acceptance Criteria:**
- Given o simulador e a chave ligada, when um ganho é arrastado e solto, then o simulador termina com o último valor e o espelho bate com ele.
- Given qualquer teste, when o simulador é inspecionado, then só chegaram a senha, as leituras do catálogo e as escritas de áudio do catálogo.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` e `dotnet test app-controle/tests/BgsControle.Telas.Testes`: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug`: 0 erros e 0 avisos.

## Implementation Notes

- **Ritmo do arrasto:** os 300 ms por parâmetro contam do **fim** da escrita anterior (eco ou releitura), não do envio. A primeira escrita sai na hora; as seguintes levam só o último valor pedido. Com o eco real (~85 ms), 20 valores em 1 s dão 4 escritas (0, ~385, ~770, ~1155 ms), e a última leva o último valor. Contado do envio, daria 5 (0, 300, 600, 900 e o último).
- **Releitura:** sem eco em 2 s, relê com prazo de 5 s (o prazo de consulta da leitura completa, MAPA §2).
- **Espelho:** só vale nesta conexão. Esvazia ao desconectar. Até a primeira leitura completa, e durante uma leitura, o `Escritor` não escreve ("O app ainda não leu o aparelho." / "O app está lendo o aparelho."). A leitura completa e cada escrita com a sua confirmação passam pela mesma exclusão interna da sessão (`Sessao.ExclusaoLeituraEscrita`). Um "Ler de novo" pedido durante um eco pendente espera a escrita terminar. Uma escrita pedida durante uma leitura é recusada.
- **Trava:** a ordem dos motivos é leitura e senha como antes; depois "modo só leitura", "comando não liberado nesta versão do app" (memórias, gerador, 23N, 251, 252, 001, 002, 004, 242), "escrita fora do catálogo" (fora do formato exato do codificador), "sem conexão autenticada" e "chave desligada". Sem `IChaves` na sessão, toda chave conta como desligada.
- **Chaves:** até o JSON guardado ser lido, toda chave conta como desligada. Um JSON estragado volta aos padrões. Se a gravação falhar, ligar não vale, mas desligar vale (o lado seguro).
- **Controle recolhido (pedido do Denis em 21/10, depois da spec):** `Expandido` começa em false. Tocar no valor recolhido abre o controle; com o controle aberto, tocar no valor abre o campo. `GrupoControles` mantém um aberto por grupo.
- O `ControleParametroViewModel` cobre os 22 parâmetros do Marco 1. Mute, limiter auto e preset de EQ também viram escolhas diretas.

- Revisão (21/10): 14 correções aplicadas, entre elas o portão comum que separa a leitura completa das escritas. Verificação final: Nucleo.Testes 350/350, Telas.Testes 120/120, build Android 0 erros e 0 avisos. Pendente: conferir no celular (abrir, conectar, mexer um ganho), junto com o pendente da história 4.

## Spec Change Log

## Review Triage Log

| # | Achado | Veredito | Evidência | Rota |
|---|---|---|---|---|
| 1 | Assinante de `Escreveu` que lança mata o laço da fila | medium | `Rodando` fica true; tarefas penduradas | patch |
| 2 | "Ler de novo" durante escrita sem eco mistura respostas | medium | A guarda `Lendo` só no começo da escrita | patch |
| 3 | Eco atrasado de escrita antiga confirma a nova | low | Só com eco > 2 s; janela rara | rejeitado |
| 4 | Eco no formato de escrita seria aceito | false | O aparelho real ecoa no formato de leitura (MAPA §9) | rejeitado |
| 5 | Escrita na fila depois da queda dá motivo errado | low | Correção direta | patch |
| 6 | Leitura da conexão anterior semeia depois de reconectar | low | A leitura para na queda; janela de milissegundos | rejeitado |
| 7 | Chave ligada em memória antes de gravar | low | Janela de milissegundos | rejeitado |
| 8 | Desligar com gravação falha volta ligada ao reabrir | medium | Segurança: a chave protege risco | patch (mensagem + nova tentativa) |
| 9 | Assinante de `Mudou` que lança no carregamento | low | Só assinantes internos | rejeitado |
| 10 | `formato` gravado e não conferido | low | Correção direta | patch |
| 11 | "Lendo o aparelho" quando a leitura falhou; `_lendo` não zera na queda | medium | Controle travado com motivo errado | patch |
| 12 | Valor local não confirmado fica na tela depois de travar | medium | Mostra valor que não veio do aparelho | patch |
| 13 | `ChavesViewModel` transient e descartável vaza | low | Correção direta | patch |
| 14 | Campo aceita valor fora do passo; `Casas ≥ 2` | low | O aparelho aceita 1 casa; nenhum parâmetro tem 2 | rejeitado |
| 15 | Attack em auto e valor fora da faixa mal exibidos | medium | Mostra "0,0 ms" no lugar de auto | patch |
| 16 | Repetição extra ao segurar de novo rápido | low | Um passo a mais, raro | rejeitado |
| 17 | Ligar chave já ligada abre a folha | low | Correção direta | patch |
| 18 | README contraditório | low | Correção direta | patch |
| 19 | Mudança de escopo fora do Spec Change Log | — | A correção é editar esta spec | rejeitado (registrado nas notas) |
| 20 | Testes da serialização, do bloqueio na leitura, da queda no eco e da leitura das chaves | medium | Mutação passa verde | patch |
| 21 | Escolhas diretas (booleano, preset) sem teste | low | Teste direto | patch |
| 22 | Espaçamento de 300 ms não medido | low | O teste aceita 2 a 4 | rejeitado |
| 23 | Montagem do DI no MauiProgram sem teste | medium | Só o build cobre | defer (conferir no celular) |
