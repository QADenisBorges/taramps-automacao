---
title: 'História 5: escrita de ajustes e chaves de acesso'
type: 'feature'
created: '2026-10-21'
status: 'ready-for-dev'
route: 'dispatch'
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
- [ ] `app-controle/src/BgsControle.Nucleo/Seguranca/`: `IChaves`, os padrões e o repositório.
- [ ] `app-controle/src/BgsControle.Nucleo/Escrita/`: o escritor e o `EstadoAparelho`.
- [ ] `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs`: a trava nova.
- [ ] `app-controle/src/BgsControle.Telas/`: `ControleParametroViewModel` e `ChavesViewModel`.
- [ ] `app-controle/src/BgsControle.App/MauiProgram.cs`: o registro no DI, sem tela nova.
- [ ] Os testes: cada linha da matriz e os antigos atualizados.

**Acceptance Criteria:**
- Given o simulador e a chave ligada, when um ganho é arrastado e solto, then o simulador termina com o último valor e o espelho bate com ele.
- Given qualquer teste, when o simulador é inspecionado, then só chegaram a senha, as leituras do catálogo e as escritas de áudio do catálogo.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` e `dotnet test app-controle/tests/BgsControle.Telas.Testes`: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug`: 0 erros e 0 avisos.

## Implementation Notes

## Spec Change Log

## Review Triage Log
