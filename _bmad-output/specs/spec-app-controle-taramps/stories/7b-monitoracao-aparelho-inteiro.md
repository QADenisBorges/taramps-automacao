---
title: 'História 7b: monitoração, remote out, Bluetooth de áudio e comandos de aparelho inteiro'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
baseline_commit: '641684ea3e591d2614cc6e617ef53ca61404188e'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/chaves-de-acesso.md'
  - '{project-root}/protocolo/MAPA.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/7a-gerador-regras-de-falha.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Para deixar o app da Taramps, o Denis ainda precisa de três coisas:
- acompanhar o voltímetro e o clip ao vivo (CAP-7);
- ligar e desligar o módulo (remote out) e o Bluetooth de áudio (CAP-9);
- usar os comandos de aparelho inteiro: reset de fábrica, reset de áudio, troca de senha e nome do produto (CAP-10).

Hoje a trava recusa esses comandos como "não liberados".

**Approach:** No núcleo, entram duas peças:
- um `MonitorAparelho`, que ouve o que o aparelho manda sozinho;
- um `ComandosAparelho`, que passa pela porta única da sessão, com a chave de cada comando e uma releitura depois.

Em `BgsControle.Telas`, entram o `MonitoracaoViewModel` e o `AparelhoViewModel`, testados contra o `ProcessadorSimulado`. Reusa o `EnvioComResposta` da 7a.

## Boundaries & Constraints

**Always:**
- **Trava.** Os comandos abaixo passam só na forma exata, com a sessão Conectada e fora do modo só leitura, e com a chave do comando:

  | Comando | Forma | Chave |
  |---|---|---|
  | Remote out | `*252*0*` / `*252*1*` | "Remote out" |
  | Bluetooth de áudio | `*251*0*` / `*251*1*` | "Bluetooth de áudio" |
  | Nome do produto | `*002*NOME*` | "Nome do produto" |
  | Reset de fábrica | `*004*` | "Reset de fábrica" |
  | Reset de áudio | `*242*` | "Reset de áudio" |
  | Troca de senha | `*001*ANTIGA*NOVA*` | "Troca de senha" |

  - O nome do produto tem de 1 a 12 caracteres A–Z, 0–9, espaço e `_`, em maiúsculas (a regra do Demo).
  - As duas senhas da troca seguem `[0-9A-Z]{4}`, e a nova é diferente da antiga.
  - Fora da forma exata, o comando é recusado como "comando do aparelho fora do formato".
  - Com isso, nenhum comando conhecido continua como "não liberado".
- **Remote out e Bluetooth de áudio:**
  - Envia o comando e confirma pelo eco ou pela releitura de `#252#` / `#251#` (Suposição ECO-251-252, que já existe).
  - O espelho fica com o valor confirmado.
  - A confirmação acontece na própria linha, sem folha.
- **Comandos de aparelho inteiro.** Cada um espera a sua resposta (Suposições ACK-001, ACK-002, RESET-004 e RESET-242) e depois faz uma leitura completa nova (CAP-10).
  - **Reset de áudio:** folha com "O estado resultante é imprevisível."
  - **Reset de fábrica:** folha com "Apaga a senha e as 3 memórias." Depois da leitura, o app esquece a senha guardada e desconecta: a próxima conexão pede a senha.
  - **Troca de senha:** a senha atual, a nova digitada duas vezes e a folha "Uma senha perdida tranca o aparelho.". Com `#001#OK#`, o app guarda a nova no celular. Com `#001#ERRO#`, nada muda e o app diz "O aparelho recusou a senha atual." Nenhuma senha aparece no log nem na tela.
  - **Nome do produto:** sem folha. Com a resposta, o app diz "Nome do produto: NOME".
  - [SUPOSIÇÃO NOME-PRODUTO] O 002 não muda o começo `PRO2.4MI` da resposta 000. Se mudar, a próxima conexão entra em modo só leitura, que é o lado seguro.
  - **Ficam recusados, sem enviar nada:**
    - sem leitura ou lendo;
    - com uma operação de memória em andamento ou pela metade;
    - com o gerador talvez ligado;
    - com outro comando de aparelho em andamento.
- **Monitoração (CAP-7).** O app ouve os quadros que chegam pela sessão.
  - `#005#v#`: voltímetro em V × 10. [SUPOSIÇÃO VOLTIMETRO-ESPONTANEO] O aparelho manda o 005 sozinho, neste formato; a consulta `#005#` responde `ERRO 0` (medido).
  - `#150#bits#`: clip das saídas, um bit por saída.
  - `#111#v#`: clip da entrada. [SUPOSIÇÃO CLIP-ENTRADA] O 111 chega sozinho, no formato da leitura.
  - A leitura completa também semeia os três valores.
  - Um clip fica aceso por 3 s depois do último aviso com o seu bit (UX).
  - Sem valor do voltímetro, a tela mostra "—".
  - Ao desconectar, tudo volta a "—" e a apagado.
- **ViewModels** (Telas):
  - `MonitoracaoViewModel`: o voltímetro ("12,4 V" ou "—") e o clip das saídas 1 a 4 e da entrada.
  - `AparelhoViewModel`:
    - Remote out e Bluetooth de áudio Lig./Desl., com o valor relido;
    - o campo do nome do produto;
    - a troca de senha;
    - os dois resets com as folhas.
  - Cada ação fica travada com o motivo ("Chave desligada", "Sem conexão"…). Nada sai antes do botão de confirmar da folha.
- **Simulador:** aceita `*002*NOME*` com o ack `#002#NOME#`, e emite o voltímetro e o clip da entrada sob comando do teste. As suposições novas entram em `SUPOSICOES.md` e em `Suposicoes.cs`.

**Never:**
- Gerador (é da 7a).
- XAML (é da história 11).
- Escrita fora de `Sessao.EnviarAsync`.
- Uma consulta periódica ao voltímetro (o `#005#` responde `ERRO 0`).
- A IA e a confirmação na tela dos pedidos da IA (são da 9b).

## I/O & Edge-Case Matrix

| Cenário | Entrada / estado | Esperado | Erro |
|---|---|---|---|
| Remote out | Desl. com a chave ligada | `*252*0*`, eco, espelho em 0 | — |
| Sem eco no 252 | `*252*0*` silenciado | Relê `#252#`; vale o relido | — |
| BT de áudio | Lig. | `*251*1*`, espelho em 1 | — |
| Chave desligada | Reset de áudio com a chave desligada | Nada sai | "Chave desligada" |
| Reset de áudio | Chave ligada, folha, Confirmar | `*242*`, `#242#`, leitura nova; o espelho mostra a fábrica | — |
| Cancelar a folha | Reset e Cancelar | Nada sai | — |
| Reset de fábrica | Chave ligada, Confirmar | `*004*`, leitura nova, senha apagada do celular, desconectado | — |
| Troca de senha | AB12 → CD34, chave ligada | `*001*AB12*CD34*`, `#001#OK#`, o celular guarda CD34; o log não mostra as senhas | — |
| Senha atual errada | XXXX → CD34 | `#001#ERRO#`; a senha guardada não muda | "O aparelho recusou a senha atual." |
| Nova não confere | Nova e confirmação diferentes | Nada sai | "As duas senhas novas não são iguais." |
| Nome do produto | "sala 1" | `*002*SALA 1*`, ack, leitura nova | — |
| Nome inválido | "sala#1" | Nada sai | "Use até 12 letras, números, espaço ou _" |
| Gerador ligado | Reset com o gerador tocando | Nada sai | "O gerador está em uso." |
| Voltímetro | O simulador emite `#005#124#` | "12,4 V" | — |
| Clip | O simulador emite `#150#4#` | A saída 3 acende e apaga 3 s depois | — |
| Clip da entrada | `#111#1#` | A entrada acende | — |
| Queda | Desconecta | Voltímetro "—", clips apagados | — |

</frozen-after-approval>

## Code Map

- `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs` (`Trava`, `ComandosNaoLiberados`): os comandos da tabela; o motivo novo.
- `app-controle/src/BgsControle.Nucleo/Catalogo/Catalogo.cs`:
  - `ComandoDeAparelho(quadro)`, no padrão de `ComandoDeGerador`;
  - `NormalizarNomeMemoria`, a mesma regra de nome;
  - `Extras` 005, 111, 150, 251 e 252, que já existem.
- `app-controle/src/BgsControle.Nucleo/Escrita/EnvioComResposta.cs` (7a): enviar e esperar.
- `app-controle/src/BgsControle.Nucleo/Gerador/GeradorSinal.cs` (`TalvezLigado`), `CopiaAntes.MotivoBloqueio` e `EstadoAparelho` (`Pronto`, `Lendo`, `Atualizar`): as recusas.
- `app-controle/src/BgsControle.Nucleo/Leitura/LeituraAutomatica.cs` (`LerNovaAsync`): a leitura depois.
- `app-controle/src/BgsControle.Nucleo/Seguranca/ICofreSenha.cs` e `Sessao.EsquecerSenhaAsync`: guardar a senha nova; esquecer depois do reset de fábrica.
- `app-controle/src/BgsControle.Nucleo/Registro/LogBle.cs`: já mascara o `*001*`. Também chamar `ProtegerSegredo` para as duas senhas.
- `app-controle/src/BgsControle.Nucleo/Catalogo/Parametro.cs` (`Decivolt`, `BitsPorSaida`): a exibição.
- `app-controle/tests/BgsControle.Simulador/ProcessadorSimulado.cs`: `Processar` já trata 004, 242, 001, 251, 252 e `EmitirClip`. Falta o 002, o `EmitirVoltimetro` e o `EmitirClipEntrada`.
- `app-controle/tests/BgsControle.Telas.Testes/ControleParametroViewModelTestes.cs` (`Bancada`, `ConferirSoAudio`) e `GeradorTestes`: modelo da bancada com o gerador; a 7a mostra como andar o relógio falso nos ecos.
- Testes que esperam "não liberado" para 251, 252, 002, 004, 242 e 001: `EscritaTestes`, `GeradorTestes` (`Os_comandos_da_7b_continuam_nao_liberados`), `LeituraTestes` e `SessaoTestes`.

## Tasks & Acceptance

**Execution:**
- [x] `app-controle/src/BgsControle.Nucleo/Catalogo/Catalogo.cs` e `Sessao/Sessao.cs`: a trava.
- [x] `app-controle/src/BgsControle.Nucleo/Aparelho/ComandosAparelho.cs`: remote out, Bluetooth de áudio, resets, senha e nome.
- [x] `app-controle/src/BgsControle.Nucleo/Aparelho/MonitorAparelho.cs`: o voltímetro e o clip.
- [x] `app-controle/src/BgsControle.Telas/AparelhoViewModel.cs` e `MonitoracaoViewModel.cs`.
- [x] `app-controle/src/BgsControle.App/MauiProgram.cs`: o DI.
- [x] O simulador, as suposições e os testes: as linhas da matriz e os testes antigos atualizados.

**Acceptance Criteria:**
- Given qualquer teste da 7b, when o simulador é inspecionado, then só chegaram a senha, as leituras do catálogo e os comandos da tabela da trava pedidos pelo teste.
- Given o log da sessão depois da troca de senha, when ele é lido, then nenhuma das duas senhas aparece.

## Implementation Notes

- **Suposições novas** (em `SUPOSICOES.md` e `Suposicoes.cs`): ACK-002, NOME-PRODUTO,
  VOLTIMETRO-ESPONTANEO e CLIP-ENTRADA.
- Decisões que a spec não fecha:
  - Remote out e Bluetooth de áudio também ficam recusados sem leitura, lendo e com outro
    comando de aparelho andando (como as escritas de áudio). Não esperam a memória nem o gerador.
  - Um eco ou releitura com outro valor conta como falha; o espelho fica com o valor do aparelho.
  - Sem a resposta de um reset ou do nome, o app relê mesmo assim e diz que o aparelho não
    confirmou. Sem a resposta do reset de fábrica, a senha guardada fica: se não valer mais, a
    próxima conexão recebe `ERRO`, apaga a senha e pede a nova.
  - Com `#001#ERRO#` não há leitura nova (nada mudou).
  - O prazo da resposta dos comandos de aparelho inteiro é 5 s (nenhum foi medido).
  - Um `*005*v*`, `*111*v*` ou `*150*v*` (códigos só leitura) agora é "escrita fora do catálogo".
    O motivo "não liberado" continua existindo só como reserva do `Escritor`.
  - A leitura completa semeia o voltímetro só quando o 005 vem legível; com o `ERRO 0`, fica o
    último valor espontâneo (ou "—").
  - Os campos de senha limpam depois do envio, com sucesso ou não.
- **Riscos que ficam:**
  - Entre a resposta de um comando de aparelho inteiro e o começo da leitura nova, o gerador ou
    uma escrita de áudio ainda podem entrar (eles não conhecem o `ComandosAparelho`).
  - Depois de um reset, a "memória em uso" do `GerenteMemorias` não é limpa.
- **Verificação:** Nucleo.Testes 597 de 597 e Telas.Testes 196 de 196, rodados três vezes; build
  Android (`--no-incremental`) com 0 erros e 0 avisos.

- **Depois da revisão (22/10):** as correções 1, 2, 3, 4, 8, 10, 14, 15, 16, 19 e 22 da tabela abaixo entraram, com 8 testes novos. Os itens 5 e 13 foram para o deferred-work.
- **Verificação final:** Nucleo.Testes 605 de 605 e Telas.Testes 196 de 196, rodados três vezes; build Android com 0 erros e 0 avisos.

## Review Triage Log

| # | Achado | Veredito | Evidência | Rota |
|---|---|---|---|---|
| 1 | Troca de senha sem resposta: a senha nova se perde (a tela limpa os campos) | medium | O aparelho pode ter aceitado; o app guarda só a antiga | patch |
| 2 | Resposta do 001 que não é OK nem ERRO conta como recusa | low | Só o ERRO é uma recusa clara | patch |
| 3 | A resposta do 002 não é comparada com o nome enviado; a tela mostra o digitado | low | Qualquer `#002#…#` vale | patch |
| 4 | As condições são conferidas só antes da porta; uma leitura ou uma memória entra no meio | medium | O `Escritor` confere de novo com a porta; aqui não | patch |
| 5 | Entre a resposta e a leitura nova, uma escrita de áudio ou o gerador podem entrar | medium | Escritor, memórias e gerador não sabem do `ComandosAparelho` | defer |
| 6 | Esquecer a senha falha no cofre e a tela diz que apagou | low | Raro; a sessão registra no log | rejeitado |
| 7 | "A leitura parou" quando o leitor anda fora da leitura automática | low | No app, o leitor só roda pela `LeituraAutomatica` | rejeitado |
| 8 | A releitura que não sai troca o primeiro envio e diz "sem conexão" | low | O comando pode ter sido aplicado | patch |
| 9 | Remote out sem confirmação deixa o espelho com o valor velho | low | O resultado é falha, com a frase certa | rejeitado |
| 10 | O último ramo do tipo → chave é implícito | low | Um tipo novo cairia na "Troca de senha" | patch |
| 11 | Uma exceção na troca de senha limpa os campos sem erro | low | Só com bug no núcleo | rejeitado |
| 12 | `Saida(n)` fora de 1 a 4 sem mensagem clara | low | O indexador lança de todo jeito | rejeitado |
| 13 | A máscara do log esconde `1000`, `1234`… em outras linhas | medium | Comportamento anterior do `LogBle` (a senha da conexão já entrava) | defer |
| 14 | O reset de fábrica ignora a leitura que parou | low | Os outros comandos avisam | patch |
| 15 | `Recusa` devolve os motivos da sessão em minúsculas | low | O resto passa por `Frase()` | patch |
| 16 | A tela repete as regras de bloqueio do núcleo com outras frases | low | A tela e o núcleo já dão textos diferentes | patch |
| 17 | O campo do nome nunca mostra o nome atual | false | Não há consulta de leitura para o 002 (NOME-PRODUTO) | rejeitado |
| 18 | O DI não passa o relógio ao `ComandosAparelho` | false | O padrão é o mesmo `TimeProvider.System` registrado | rejeitado |
| 19 | ACK-002 diz "no prazo de um eco", e o código espera 5 s | low | Texto da suposição errado | patch |
| 20 | `MonitorAparelho` sem limpeza e sem log no aviso | low | Vive o app inteiro | rejeitado |
| 21 | O `Dispose` de `EscritaTestes` aceita 251/252 na classe toda | low | Só um teste precisa | rejeitado |
| 22 | Faltam testes: reset e senha sem resposta, cofre falhando, 252 sem confirmação, reset de áudio sem resposta, recusa com memória pela metade | medium | Verification gap: as mutações passam verdes | patch |

## Verification

**Commands:**
- `dotnet test app-controle/BgsControle.slnx`: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug`: 0 erros e 0 avisos.
