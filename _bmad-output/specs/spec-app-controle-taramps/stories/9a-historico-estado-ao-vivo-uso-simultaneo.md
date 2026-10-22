---
title: 'História 9a: histórico, estado ao vivo e uso simultâneo'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
baseline_commit: 'cf370d11e7540b0959adc028234f494b4155a317'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/porta-de-comandos.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A tela e a futura IA (9b) mandam comandos ao mesmo processador, mas nenhum comando sabe quem o pediu: não há histórico, nem a trava "controles travados até PARAR IA", nem a pausa no toque com a janela de 10 s (CAP-13, `porta-de-comandos.md`).

**Approach:** No núcleo:
- uma **origem ambiente** (Tela, IA ou App) acompanha cada comando até a porta única `Sessao.EnviarAsync`;
- ali, um `Historico` registra cada comando que chega à porta;
- e um `UsoCompartilhado` decide, pela origem, o que passa enquanto a IA trabalha.

Em `BgsControle.Telas` ficam o `HistoricoViewModel` e o `IaAtivaViewModel`, testados contra o `ProcessadorSimulado`. A rede é da 9b, e o XAML é da 11.

## Boundaries & Constraints

**Always:**
- **Origem.** A tela é a origem padrão. A IA entra por um escopo explícito, que a 9b vai usar. O desligar automático do gerador (prazo, queda, ao reconectar, ao sair) vai como App.
- **Escritor.** Ele junta pedidos por endereço; cada pedido leva a sua origem até o envio.
- **Histórico.** Cada entrada guarda a hora (do `TimeProvider`), a origem, o tipo, o endereço e o valor, e se foi enviada ou recusada (com o motivo).
  - As leituras e o quadro da senha nunca entram.
  - A troca de senha entra como "Troca de senha", sem as senhas.
  - A mais nova fica em cima, e o evento `Registrou` avisa cada entrada nova.
  - **As recusas entram, com o motivo** (decisão do Denis, 22/10).
  - **O que o app faz sozinho entra com a origem "App"** (decisão do Denis, 22/10).
  - **Fica guardado no celular** (decisão do Denis, 22/10). O arquivo é `historico.json`, formato 1, com as últimas 1000 entradas. As gravações se juntam a cada 2 s. Ao abrir, o histórico volta. Se o arquivo estiver estragado, o histórico começa vazio e sem erro. Se a gravação falhar, a lista em memória continua, e a próxima gravação tenta de novo.
- **Modo do toque** (decisão do Denis, 21/10). Fica guardado no celular. O padrão é "Controles travados até PARAR IA"; a opção é "O toque pausa a IA". Só a tela muda o modo.
- **IA ativa:** uma tarefa (com a frase da faixa) foi iniciada, ou chegou um comando da IA. Termina ao fim da tarefa ou no PARAR IA.
- **Travado** (IA ativa, modo padrão): um comando da tela é recusado com "Controles travados. Toque em PARAR IA para mexer."
- **Pausa** (IA ativa, modo opção):
  - um comando da tela passa e pausa a IA;
  - cada novo comando da tela reinicia os 10 s;
  - com 10 s sem comandos da tela, a IA retoma sozinha;
  - os eventos `Pausou` e `Retomou` avisam a 9b.
- **Recusas à IA:**
  - Pausada: "IA pausada: o usuário está mexendo."
  - Um endereço que a tela mexeu há menos de 10 s, mesmo sem tarefa: "o usuário está mexendo nisso."
  - Depois do PARAR IA: "A IA foi parada." Isso vale até a 9b liberar, num pareamento novo.
- **Nunca travados:** o `*202*0*` (desligar o gerador) e o PARAR IA.
- **Chaves de acesso.** São conferidas antes do uso compartilhado; a recusa de uma chave desligada não muda.
- **Ao vivo.** Uma mudança da IA aparece no `ControleParametroViewModel` em até 1 s.

**Never:**
- Rede, porta HTTP, pareamento ou confirmação de operações de risco (9b).
- XAML, navegação ou registro no `MauiProgram` (11).
- A IA mudar o modo do toque ou uma chave.

## I/O & Edge-Case Matrix

| Cenário | Entrada / estado | Esperado | Erro |
|---|---|---|---|
| Comando da tela | Ganho da saída 3 para +2,0 dB | Enviado; entrada Tela, hora, "Ganho · saída 3 · +2,0 dB" | — |
| Recusa no histórico | Chave "Ajustes de áudio" desligada | Entrada recusada com o motivo da chave | — |
| Travado | IA ativa, modo padrão; a tela mexe | Nada sai | "Controles travados. Toque em PARAR IA para mexer." |
| Gerador no travado | IA ativa, modo padrão; prazo do tom acaba | `*202*0*` sai; entrada App | — |
| Pausa | IA ativa, modo opção; a tela mexe | Sai; IA pausada; `Pausou` | — |
| IA na pausa | Pausada; IA pede um ajuste | Nada sai | "IA pausada: o usuário está mexendo." |
| Retomar | Pausada; 10 s sem comandos da tela | Ativa; `Retomou` | — |
| Endereço tocado | Sem tarefa; tela mexe no ganho 3; IA mexe nele 5 s depois | Nada sai | "o usuário está mexendo nisso." |
| PARAR IA | IA ativa; PARAR IA | Inativa; um comando da IA depois disso é recusado | "A IA foi parada." |
| Ao vivo | IA muda o ganho 3 | O controle da saída 3 mostra em até 1 s; entrada IA | — |
| Senha | Troca de senha pela tela | Entrada "Troca de senha", sem as senhas | — |
| Reabrir | 3 entradas gravadas; novo `Historico` no mesmo repositório | As 3 voltam, na ordem | — |
| Limite | 1001 entradas | Fica com as 1000 mais novas | — |
| Arquivo estragado | `historico.json` ilegível | Começa vazio; o registro funciona | — |

</frozen-after-approval>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Sessao/Sessao.cs`:
  - `EnviarAsync` (l. 350) é a porta única. É ali que entram o registro e o uso compartilhado.
  - `Trava` (l. 399) confere as chaves; a regra nova vem depois dela, e o `*202*0*` fica isento.
  - `Conferir` (l. 463) mostra o mesmo resultado sem enviar.
  - Use `Catalogo.EhLeitura` e `QuadroSenha` para tirar as leituras e a senha do histórico.
- `src/BgsControle.Nucleo/Seguranca/Chaves.cs:23`: `OrigemComando { Tela, Ia }` ganha `App`. `Chaves.DefinirAsync` (l. 155) continua recusando o que não vem da tela.
- `src/BgsControle.Nucleo/Escrita/Escritor.cs`:
  - `Pedir` (l. 168) junta os pedidos por endereço e roda `RodarAsync` em `Task.Run`.
  - O `Pedido` precisa guardar a origem, que é restaurada antes de `EnviarEConfirmarAsync` (l. 306).
- `src/BgsControle.Nucleo/Gerador/GeradorSinal.cs`: os caminhos do desligar automático (`DesligarAoSairAsync` l. 495, o fim do prazo e a regra 2) rodam como App.
- `src/BgsControle.Nucleo/Escrita/EstadoAparelho.cs`: o `Mudou` (l. 43) já serve de estado ao vivo. Não mude o evento.
- `src/BgsControle.Nucleo/Registro/LogBle.cs`: modelo do registro em memória (limite, `TimeProvider`, evento).
- `src/BgsControle.Nucleo/Presets/GerentePresets.cs`: modelo do guardado (l. 403–533: `Gravacao`, `"formato": 1`, arquivo estragado vira o padrão).
- `src/BgsControle.Nucleo/Catalogo/Parametro.cs`: `Nome` e `Onde(indice)` (l. 179) servem para a frase. Os valores usam `src/BgsControle.Telas/FormatoValor.cs` (`ComUnidade`).
- `src/BgsControle.Telas/TelaBase.cs` e `MemoriasViewModel.cs`: modelo de ViewModel (`NaTela`, `Dispose`, a lista trocada inteira). `RepositorioTextoArquivos.cs` recebe as constantes dos arquivos novos.
- Testes:
  - `tests/BgsControle.Telas.Testes/ControleParametroViewModelTestes.cs:19` (`Bancada`);
  - `tests/BgsControle.Simulador/RelogioFalso.cs` (`Avancar`);
  - `EscritaTestes.cs:103` (`Conectar`).

## Tasks & Acceptance

**Execution:**
- [x] `src/BgsControle.Nucleo/Uso/` (`Origem` com `AsyncLocal` e escopo, `Historico`, `UsoCompartilhado`, `ModoToque`) -- registro, estados Inativa/Ativa/Pausada, janela de 10 s pelo `TimeProvider`, endereços tocados; o modo vai em `uso-ia.json` e o histórico em `historico.json` -- CAP-13
- [x] `src/BgsControle.Nucleo/Sessao/Sessao.cs`, `Escrita/Escritor.cs`, `Gerador/GeradorSinal.cs`, `Seguranca/Chaves.cs` -- a porta registra e consulta o uso; o Escritor leva a origem; o desligar automático vai como App -- uma porta só
- [x] `src/BgsControle.Telas/` (`HistoricoViewModel`, `IaAtivaViewModel`) -- linhas (hora, origem, frase, motivo); faixa, travado ou pausado, PARAR IA, "O que a IA fez" e o aviso da última mudança da IA
- [x] `tests/` -- um teste por linha da matriz, o modo guardado e reaberto, e a origem que atravessa a fila do Escritor

**Acceptance Criteria:**
- Given um pedido da IA e um da tela no mesmo endereço, juntados pelo Escritor, when o quadro sai, then o histórico mostra a origem do pedido que foi enviado.
- Given os testes que já existem, when a 9a entra, then todos continuam passando, sem mudar o que conferem.

## Implementation Notes

- A origem é lida na entrada de `Sessao.EnviarAsync`; o `ComandoNaPorta` (em `Uso/`) tira das escritas o tipo, o endereço e o valor, e devolve null para as leituras e qualquer texto do código 000. Um texto fora do catálogo entra só com o código; o `*001*` fora do formato entra como "Troca de senha", sem valor.
- Ordem na porta: a `Trava` (chaves) primeiro; só o que passa nela vai ao `UsoCompartilhado.Admitir`. `Conferir` usa `Consultar` (a mesma regra, sem efeitos), então quem prepara uma operação (gerador, memórias, presets, cópia) já vê "Controles travados".
- A origem App passa sempre, sem efeitos, como o `*202*0*`. Só um comando da IA que passa no uso ativa a IA; uma IA recusada (pela chave, pela pausa ou por um endereço tocado) não abre a IA ativa.
- Com o modo mudado para travar no meio de uma pausa, a tela fica travada até a IA retomar ou parar.
- Na porta, antes da fila do GATT vale `Consultar` (sem efeitos); depois da segunda `Trava`, `Admitir` (com efeitos). No Escritor, a tela e a IA não se juntam: um pedido da IA sobre um pendente da tela é recusado na hora com "o usuário está mexendo nisso.", e um pedido da tela que toma o lugar de um da IA faz a IA terminar com essa mesma recusa.
- O Escritor guarda a origem no `Pedido` e a restaura (com a cópia do link) em `RodarAsync`. O gerador desliga como App no prazo, no erro ao ligar, antes de soltar a conexão, ao sair e na regra 2 (`DesligarSozinhoAsync`); o Parar da tela segue Tela.
- `Chaves.DefinirAsync` e `UsoCompartilhado.DefinirModoAsync` recusam também quando a origem ambiente não é a tela.
- `Historico.GravarAgoraAsync` existe para a 11 chamar ao sair do app. `Sessao` recebe o histórico e o uso por parâmetros opcionais; o `MauiProgram` (11) ainda não os cria.
- "O que a IA fez" mostra as entradas da IA desde que ela ficou ativa (`AtivaDesde`); o aviso da última mudança é "Agora: " + a frase da última entrada da IA enviada.

## Spec Change Log

## Review Triage Log

### 2026-10-22 — Revisão

- **Veredictos:** 34 achados. 0 high, 4 medium, 22 low, 2 false e 1 maybe-false; 5 lacunas de verificação já conferidas. Viraram 9 grupos de patch, 2 adiados e o resto rejeitado.

**Blind Hunter**
- `[medium]` `[patch]` **O Escritor junta pedidos da tela e da IA no mesmo endereço.** Um toque pendente trocado por um pedido da IA não chega à porta: não pausa, não marca o endereço e recebe o resultado da IA. No caso contrário, a IA recebe `Ok` por um valor que não mandou. O teste `Pedido_da_tela_trocado_por_um_da_IA_sai_como_IA` confirma. Agrupado com 2 achados do Edge Case Hunter.
- `[medium]` `[patch]` **`Admitir` roda antes da espera do `_gatt` e não é refeito depois.** Um comando da IA que espera na fila sai depois do PARAR IA. Os efeitos (ativar, pausar, marcar o endereço) ficam mesmo quando o segundo `Trava` recusa. Isso foi conferido em `Sessao.cs:381-411`. Agrupado com o Edge Case Hunter e a nota da Verification Gap.
- `[low]` `[reject]` **Carregar memória ou dar reset marca só o próprio código, não todos os parâmetros.** A regra frozen fala do endereço mexido. Para a IA pegar isso, ela teria de mandar um ajuste no segundo seguinte a um carregar, e sem tarefa. O conserto pede um conjunto de endereços por comando.
- `[low]` `[reject]` **Uma operação da tela com várias escritas (preset, voltar à cópia) pode ser cortada se a IA ficar ativa no meio.** É o que a regra frozen manda: um comando da IA ativa, e o travado recusa a tela. Só acontece com a IA mandando comandos durante os cerca de 3 s de um preset. A 9b reavalia.
- `[maybe-false]` `[defer]` **A origem ambiente vaza para trabalho em segundo plano** (timers, `Task.Run`, eventos levantados num fluxo da IA). Hoje os caminhos que escrevem nesses fluxos (gerador e Escritor) põem a origem certa, e não achei nenhum outro. Se for real, é medium. Está no `deferred-work.md`.
- `[low]` `[reject]` **O PARAR IA não fica guardado.** A revogação de verdade é a chave do cliente, que é da 9b (`porta-de-comandos.md`, trava 4). Nesta história não existe IA.
- `[low]` `[reject]` **Nada desativa a IA sozinho.** A regra frozen diz que ela termina no fim da tarefa ou no PARAR IA, e o PARAR IA está sempre à mão. A 9b chama `TerminarTarefa` quando o cliente cair.
- `[low]` `[reject]` **As recusas antes da porta (`Conferir` nos gerentes, o controle travado pela chave) não entram no histórico.** A regra frozen diz "cada comando que chega à porta". A 9b registra as recusas que ela mesma devolver à IA.
- `[low]` `[patch]` **Gravação que falha só tenta de novo com uma entrada nova.** Agrupado com 2 achados do Edge Case Hunter.
- `[medium]` `[patch]` **O `HistoricoViewModel` refaz as 1000 linhas a cada entrada.** Isso dá cerca de 100 mil frases na thread da tela num preset de 100 escritas, e os ViewModels são singletons (história 11).
- `[low]` `[reject]` **`Pausou` e `Mudou` rodam no caminho da porta.** Os handlers da 9b vão ser só sinais sem espera; o evento não bloqueia por si.
- `[low]` `[patch]` **Testes que faltam:** `TerminarTarefa`, `Conferir` sem efeito no modo pausa e o PARAR IA com um comando da IA na fila. Agrupado com as lacunas da Verification Gap.
- `[low]` `[reject]` **Um Parar da tela que se junta a um desligar automático já em curso sai como App.** É o mesmo quadro já em andamento; o histórico mostra quem o mandou.
- `[low]` `[patch]` **As linhas não têm data, e o histórico guardado atravessa dias.**
- `[low]` `[reject]` **`Chaves` recusa a origem App com `MsgIaNaoMuda`.** Nenhum caminho do App muda chaves.
- `[low]` `[patch]` **"Gerador ligado · desligado" se contradiz.**

**Edge Case Hunter**
- `[medium]` `[patch]` **`Admitir` aplica os efeitos antes do envio.** É o mesmo grupo do `Admitir` acima.
- `[medium]` `[patch]` **PARAR IA durante a espera do `_gatt`.** Mesmo grupo.
- `[medium]` `[patch]` **A tela pendente é trocada pela IA.** Mesmo grupo do Escritor.
- `[low]` `[patch]` **A IA pendente trocada pela tela recebe `Ok`.** Mesmo grupo do Escritor.
- `[low]` `[reject]` **A reconexão herda o App do `DesligarAoSairAsync`.** O fluxo de reconexão só lê e desliga o gerador, que já vai como App.
- `[low]` `[patch]` **Uma leitura fora do catálogo (`#004#`) entra como "Comando desconhecido".** A regra frozen diz que as leituras nunca entram.
- `[low]` `[patch]` **O braço `_` dos `switch` de memória e de aparelho chamaria um valor futuro de nome ou de troca de senha.** O conserto é deixar os braços explícitos, como faz a `Sessao`.
- `[low]` `[patch]` **`DefinirModoAsync` troca o modo antes de gravar.** Um comando nesse meio tempo usa um modo que depois volta.
- `[low]` `[reject]` **Nada desativa a IA sozinho.** Igual ao do Blind Hunter.
- `[low]` `[patch]` **A hora é lida fora da trava em `Registrar`.** Agrupado na persistência do histórico.
- `[low]` `[patch]` **As últimas entradas se perdem se o app sair antes dos 2 s ou se a gravação falhar.** O agendamento depois da falha está no grupo da persistência. A chamada ao `GravarAgoraAsync` na saída é da história 11 e está no `deferred-work.md`.
- `[false]` `[defer]` **O `MauiProgram` não monta o histórico nem o uso.** A spec deixa o `MauiProgram` para a 11 (Never). O que a 11 precisa fazer está no `deferred-work.md`.

**Verification Gap**
- `[low]` `[patch]` **Só o prazo tem teste para o desligar automático como App.** Faltam o erro ao ligar, antes de soltar, a regra 2, ao sair, e o Parar da tela como Tela.
- `[low]` `[patch]` **As saídas de falha do `EnviarAsync` (conexão terminou, falha na escrita) não têm teste.**
- `[low]` `[patch]` **`ComandoNaPorta.Ler` e `FraseComando` só são testados separados.**
- `[low]` `[patch]` **A origem da cópia do link não tem teste.**
- `[false]` `[reject]` **O valor do gerador fica só no primeiro segmento.** O `ComandoDeGerador` só aceita um segmento.
- `[low]` `[reject]` **O endereço de memória vale para as 3 posições.** Quem mexe numa memória está mexendo nas memórias; recusar a IA por 10 s é o lado seguro.

**Patches aplicados** (reenvio ao mesmo subagente):
- **Porta:** antes da fila do GATT, `Consultar` (sem efeitos); depois do segundo `Trava`, `Admitir` (com efeitos). Um PARAR IA ou uma pausa durante a fila recusam o comando.
- **Escritor:** a tela e a IA não se juntam no mesmo endereço.
  - Um pedido da IA sobre um pendente da tela é recusado na hora com "o usuário está mexendo nisso.".
  - Um pedido da tela que troca um pendente da IA termina os da IA com a mesma frase.
- **Histórico:**
  - a hora é lida dentro da trava;
  - uma gravação que falha agenda outra;
  - qualquer exceção na leitura começa vazio;
  - a tela acrescenta só a linha nova em cima, com a hora em `dd/MM HH:mm:ss`;
  - "Gerador · ligado/desligado".
- **`ComandoNaPorta`:** toda leitura fica fora; os braços dos `switch` estão explícitos.
- **Modo do toque:** só muda depois de gravar.
- **Testes novos:**
  - o desligar automático como App em cada caminho, e o Parar da tela como Tela;
  - as saídas de falha da porta;
  - a frase de cada quadro do catálogo (22 linhas);
  - a origem da cópia do link;
  - `TerminarTarefa`;
  - `Conferir` sem efeito;
  - PARAR IA com um comando na fila;
  - `#004#`;
  - a IA sobre um pendente da tela.
- **Verificação depois dos patches:** Núcleo 746 de 746, Telas 300 de 300, build Android com 0 erros e 0 avisos.

## Design Notes

A origem é ambiente (`AsyncLocal`) porque um parâmetro de origem teria de atravessar todos os gerentes. O padrão Tela deixa o caminho da tela sem mudança. Os únicos pontos marcados são a IA (9b, num escopo) e o desligar automático (App).

```csharp
using (Origem.Como(OrigemComando.Ia))
    await escritor.EscreverAsync(ganho, 2, 2.0m); // histórico: IA; recusado se pausada
```

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` e `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos
