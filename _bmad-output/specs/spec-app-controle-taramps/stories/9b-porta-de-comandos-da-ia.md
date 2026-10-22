---
title: 'História 9b: porta de comandos da IA'
type: 'feature'
created: '2026-10-22'
status: 'done'
baseline_revision: '1b54b1b7ac209fff07a8679270ca95a56f7b3aad'
route: 'dispatch'
baseline_commit: '1b54b1b7ac209fff07a8679270ca95a56f7b3aad'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/porta-de-comandos.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/chaves-de-acesso.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** A IA (Claude Code no notebook) ainda não tem como chegar ao app. Faltam a porta HTTP na rede local, o pareamento por código de 6 dígitos, a recusa de fora da rede, a confirmação na tela das operações de risco e o PARAR IA revogando o acesso (CAP-12, `porta-de-comandos.md`).

**Approach:** A porta fica no núcleo, em `src/BgsControle.Nucleo/Porta/`:
- um servidor HTTP/1.1 mínimo sobre `TcpListener`;
- um `PortaComandos` que confere a rede, a chave do cliente e o firmware, pede a confirmação quando a operação exige e chama os gerentes que já existem dentro de `Origem.Como(OrigemComando.Ia)`.

Em `BgsControle.Telas` ficam o `IaPelaRedeViewModel` e o `ConfirmacaoIaViewModel`, testados contra o `ProcessadorSimulado`. O XAML e o `MauiProgram` ficam para a 11.

## Boundaries & Constraints

**Always:**
- **Porta aberta só com a chave "IA pela rede" ligada.** Ela escuta em todas as interfaces, na porta 8787 (o número vai no construtor). Ao desligar a chave, a porta fecha, a IA para (`PararIa`) e o cliente pareado é apagado. Se a porta 8787 estiver ocupada, a tela mostra "Não consegui abrir a porta 8787." e nada escuta.
- **Só a rede local.** O endereço de quem pede tem de ser privado: IPv4 10/8, 172.16/12, 192.168/16 ou 169.254/16; IPv6 fe80::/10 ou fc00::/7, e IPv4 mapeado em IPv6. Também tem de estar na mesma sub-rede de uma interface do celular. Loopback é recusado. Fora disso a resposta é 403 "Só a rede local da casa." O corpo nem é lido e nada entra no histórico, só no `LogBle`.
- **Pareamento único** (um cliente por vez).
  - Com a porta aberta e ninguém pareado, a tela mostra um código de 6 dígitos (`RandomNumberGenerator`).
  - `POST /parear {"codigo","nome"}` com o código certo devolve `{"chave"}`: 32 bytes aleatórios em base64url. O celular guarda só o SHA-256 da chave, em `porta-ia.json` (formato 1, com o nome do cliente e a data). Depois disso o código some, e `UsoCompartilhado.Liberar()` é chamado.
  - A cada 5 códigos errados nasce um código novo.
  - Com um cliente já pareado, `/parear` recusa com 409 "Já existe um cliente pareado. Revogue no celular para parear outro."
  - Ao reabrir o app, o cliente guardado continua valendo. Se o arquivo estiver estragado, a porta começa sem cliente.
- **Chave do cliente.** Todo caminho, menos `/parear` e `/ajuda`, exige `Authorization: Bearer <chave>`. Sem chave ou com a chave errada: 401 "Cliente não pareado. Peça o código na tela IA pela rede do celular." A comparação usa o hash, em tempo constante.
- **PARAR IA e Revogar revogam o acesso.** O cliente é apagado e nasce um código novo. O `PortaComandos` reage ao `UsoCompartilhado.Mudou` quando `Parada` vira verdadeiro, então o PARAR IA do `IaAtivaViewModel` não muda. O Revogar da tela IA pela rede chama `PararIa` e `Revogar`.
- **Firmware diferente de R003** (`Sessao.SoLeitura`): só os GET respondem. Qualquer POST ou DELETE, menos `/parear`, recebe 409 "Firmware diferente do R003: a porta só responde às leituras."
- **Origem.** Cada comando roda dentro de `Origem.Como(OrigemComando.Ia)`. As conexões são aceitas com `ExecutionContext.SuppressFlow()`, para que a origem nunca vaze nem entre na porta (`deferred-work.md`, item da 9a).
- **Confirmação na tela.**
  - Valem as operações com `ConfirmacaoIa.Sempre`. O salvar memória (`AoSobrescrever`) também confirma sempre, porque as 3 memórias do aparelho sempre existem e a tela sempre trata o salvar como sobrescrever.
  - Antes de abrir a folha, a porta confere com `Sessao.Conferir` o primeiro quadro que a operação mandaria. A chave desligada, o só leitura, a falta de conexão e as recusas do uso compartilhado voltam na hora, sem folha.
  - O pedido HTTP espera a decisão por até 60 s.
  - O botão branco segue. "Não deixar" recusa com "O usuário recusou na tela." Sem resposta em 60 s, a recusa é "O usuário não confirmou a tempo." Com o PARAR IA durante a espera, a recusa é "A IA foi parada."
  - Só cabe um pedido esperando. Outro recebe "Já há um pedido esperando confirmação na tela."
- **Histórico.** As recusas que a própria porta devolve a um cliente pareado entram no histórico como IA, recusado, com o motivo: confirmação recusada ou sem resposta, firmware, pedido esperando. O que passa à `Sessao` já entra por ela.
- **Cliente sumido.** Se a IA está ativa e o cliente passa 30 s sem nenhum pedido (relógio `TimeProvider`), a porta chama `TerminarTarefa` e o aviso da tela vira "A IA perdeu a conexão."
- **Caminhos** (JSON UTF-8; as respostas trazem `{"ok":true,...}` ou `{"ok":false,"motivo"}`):
  - `GET /ajuda`: texto com os caminhos e os corpos.
  - `GET /estado`: conexão, firmware, só leitura, memória em uso e nomes, o estado da IA e a tarefa, o gerador, o voltímetro e os clips, e cada parâmetro lido (código, índice, onde, nome, valor no fio e exibição).
  - `GET /catalogo`: os parâmetros de áudio graváveis, com escopo, índices, mínimo, máximo, casas, unidade e opções.
  - `GET /eventos?desde=N`: pausou, retomou, parada e cada comando da tela enviado. Os eventos têm um número em sequência e ficam os últimos 200.
  - `GET /historico?limite=N`: até 200 entradas, a mais nova primeiro.
  - `POST /ajuste {"codigo","indice","valor"}`: vai ao `Escritor`.
  - `POST /memoria/salvar {"memoria":1..3,"nome"}` e `POST /memoria/carregar {"memoria":1..3}`. O número vai de 1 a 3, como na tela; o núcleo usa de 0 a 2.
  - `POST /gerador/tom {"hz","db"}`, `POST /gerador/sweep {"inicioHz","fimHz","db","velocidade"}` e `POST /gerador/parar`. O parar nunca é travado e não confirma.
  - `POST /aparelho/remote-out {"ligado"}`, `POST /aparelho/bluetooth {"ligado"}`, `POST /aparelho/nome {"nome"}`, `POST /aparelho/reset-audio` e `POST /aparelho/reset-fabrica`.
  - `POST /tarefa {"frase"}` chama `IniciarTarefa`; `DELETE /tarefa` chama `TerminarTarefa`.
- **Códigos HTTP:**
  - 400: JSON ou campo inválido.
  - 401: sem chave.
  - 403: fora da rede.
  - 404: caminho desconhecido.
  - 409: recusa do app, com o motivo.
  - 413: corpo acima de 64 KB.
  - Um cabeçalho que não chega em 10 s fecha a conexão.

**Never:**
- **A senha do processador pela porta.** A troca de senha não tem caminho, e nenhuma resposta traz a senha.
- A IA ligar uma chave, mudar o modo do toque ou parear sem o código da tela.
- Presets, link de canais, player ou nomear memória pela porta (fora desta história).
- XAML, navegação ou registro no `MauiProgram` (história 11).
- Kestrel, ASP.NET ou qualquer pacote NuGet novo.

## I/O & Edge-Case Matrix

| Cenário | Entrada / estado | Esperado | Erro |
|---|---|---|---|
| Chave desligada | Porta pedida | Nada escuta | — |
| Parear | Código certo, de 192.168.0.10 | 200 com a chave; o código some; cliente guardado | — |
| Código errado | 5 vezes | 401 em cada uma; nasce outro código | — |
| Segundo cliente | Já pareado; `/parear` | 409 | "Já existe um cliente pareado. Revogue no celular para parear outro." |
| Sem chave | `GET /estado` sem Bearer | 401 | "Cliente não pareado. Peça o código na tela IA pela rede do celular." |
| Fora da rede | De 8.8.8.8 ou de 127.0.0.1 | 403; nada no histórico | "Só a rede local da casa." |
| Ajuste | `POST /ajuste` ganho, saída 3, +2,0 | 200; histórico IA; o controle da saída 3 mostra o valor | — |
| Salvar | `POST /memoria/salvar {"memoria":2}` | Abre a folha; com o toque, salva e responde 200 | — |
| Recusou | Folha do carregar; "Não deixar" | 409; histórico IA recusado | "O usuário recusou na tela." |
| Sem resposta | Folha aberta por 60 s | 409 | "O usuário não confirmou a tempo." |
| Chave do gerador desligada | `POST /gerador/tom` | 409 sem folha | motivo da chave |
| Dois riscos | Folha aberta; outro risco chega | 409 | "Já há um pedido esperando confirmação na tela." |
| PARAR IA | Pareado; PARAR IA | Cliente apagado; código novo; o próximo pedido dá 401 | — |
| Firmware R004 | `POST /ajuste` | 409; `GET /estado` responde | "Firmware diferente do R003: a porta só responde às leituras." |
| Pausa | Modo pausa; a tela mexe | `GET /eventos` traz "pausou" e o comando da tela | — |
| Cliente sumido | IA ativa; 30 s sem pedidos | `TerminarTarefa`; aviso | "A IA perdeu a conexão." |
| Desligar a chave | Pareado; a tela desliga a chave | Porta fechada; cliente apagado | — |
| Reabrir | Cliente guardado; nova porta, mesmo repositório | A chave antiga vale | — |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Uso/UsoCompartilhado.cs`:
  - `IniciarTarefa` (l. 228), `TerminarTarefa` (246), `PararIa` (257), `Liberar` (269), `Parada`, `IaAtiva`;
  - eventos `Mudou`, `Pausou` e `Retomou` (74–80);
  - `Consultar` (152) é `internal`, mas a porta fica no mesmo assembly.
- `src/BgsControle.Nucleo/Uso/Origem.cs:24`: `Origem.Como(OrigemComando)`.
- `src/BgsControle.Nucleo/Uso/Historico.cs`:
  - `Registrar(ComandoNaPorta, OrigemComando, bool, string?)` (81, `internal`) registra as recusas da porta;
  - `Registrou` (64) alimenta os eventos da tela;
  - `Entradas`.
- `src/BgsControle.Nucleo/Uso/ComandoNaPorta.cs`: `TipoComando` e o record, para montar a entrada de uma recusa.
- `src/BgsControle.Nucleo/Sessao/Sessao.cs`:
  - `Conferir(quadro)` (508) faz a checagem antes da folha;
  - `SoLeitura`, `Firmware`, `Estado` mais `SegurandoConexao` dão a conexão;
  - os motivos `Motivo*` estão nas linhas 64–74;
  - no só leitura até o `*202*0*` é recusado (l. 452), e isso não muda.
- `src/BgsControle.Nucleo/Seguranca/Chaves.cs`:
  - `Chave.IaPelaRede`, `ChavesPadrao.De(chave).ConfirmacaoIa` (89–112);
  - `IChaves.Ligada` e o evento `Mudou(Chave)` abrem e fecham a porta.
- Gerentes que a porta chama (todos já escrevem pela `Sessao.EnviarAsync`):
  - `Escrita/Escritor.cs:121` `EscreverAsync(Parametro, int?, decimal)`, e as sobrecargas para bool e texto;
  - `Catalogo/Catalogo.cs:173` `ParametroDe(codigo)`, `EscritaDeAudio`, as constantes de código (233–381);
  - `Catalogo/Parametro.cs` (`Indices`, `Minimo`, `Maximo`, `Casas`, `Opcoes`, `Valor.Exibicao`);
  - `Memorias/GerenteMemorias.cs` `SalvarAsync(n, nome)` (146), `CarregarAsync(n)` (149), `Nome(n)`, `EmUso`;
  - `Gerador/GeradorSinal.cs` `TocarTomAsync` (246), `TocarSweepAsync` (270), `PararAsync` (481);
  - `Aparelho/ComandosAparelho.cs` `DefinirRemoteOutAsync` (143), `DefinirBluetoothAudioAsync` (147), `ResetAudioAsync` (208), `ResetFabricaAsync` (239), `NomearProdutoAsync` (335);
  - `Aparelho/MonitorAparelho.cs` (`Volts`, `ClipSaida`, `ClipEntrada`) e `Escrita/EstadoAparelho.cs` (`Copiar()`, `Valor`).
- `src/BgsControle.Nucleo/Escrita/IRepositorioTexto.cs`: `IRepositorioTexto` e o helper `Gravacao`, modelo do `porta-ia.json`. Outro modelo é `UsoCompartilhado.ParaJson`/`DeJson` (370, 384), com `Utf8JsonWriter` e `JsonDocument`.
- `src/BgsControle.Telas/`:
  - `TelaBase.cs` e `IaAtivaViewModel.cs` são o modelo de ViewModel. O PARAR IA não muda.
  - `ChavesViewModel.cs:59` `NotaIa` ("ainda não existe") passa a "Pareia por código de 6 dígitos."; o teste fica em `ChavesViewModelTestes.cs:45-55`.
  - `RepositorioTextoArquivos.cs` recebe a constante `porta-ia.json`.
- `src/BgsControle.App/Platforms/Android/AndroidManifest.xml`: falta INTERNET (o Debug a põe sozinho, o Release não) e falta ACCESS_NETWORK_STATE.
- Testes:
  - `tests/BgsControle.Telas.Testes/ControleParametroViewModelTestes.cs:20` (`Bancada`, com `usoCompartilhado: true`);
  - `tests/BgsControle.Nucleo.Testes/UsoTestes.cs:62-170` (montagem à mão, `TransporteSegurado` em 877);
  - `tests/BgsControle.Simulador/RelogioFalso.cs` (`Avancar`);
  - `Firmware = "R004"` dá o só leitura.

## Tasks & Acceptance

**Execution:**
- [x] `src/BgsControle.Nucleo/Porta/ServidorHttp.cs`: um `TcpListener` com HTTP/1.1 mínimo (linha de pedido, cabeçalhos, `Content-Length`, `Connection: close`), com 64 KB e 10 s de limite. Aceita com `SuppressFlow` e entrega um `PedidoPorta` (método, caminho, query, Bearer, corpo, IP remoto) a um `Func<PedidoPorta, Task<RespostaPorta>>`. Sem pacote novo, e roda igual no Windows e no Android.
- [x] `src/BgsControle.Nucleo/Porta/RedeLocal.cs`: `IRedeLocal.Aceita(IPAddress)` e a implementação com faixas privadas e a mesma sub-rede pelo `NetworkInterface`, mais o `Endereco` do celular para a tela. Faz a trava 3.
- [x] `src/BgsControle.Nucleo/Porta/Pareamento.cs`: código, tentativas, hash da chave, `porta-ia.json`, `Revogar`, evento `Mudou`. Faz a trava 2.
- [x] `src/BgsControle.Nucleo/Porta/PedidosConfirmacao.cs`: um pedido por vez (título, risco, texto do botão), `Confirmar`, `Recusar`, prazo de 60 s pelo `TimeProvider`, evento `Mudou`. Faz a trava 5.
- [x] `src/BgsControle.Nucleo/Porta/PortaComandos.cs`:
  - `AtenderAsync(PedidoPorta)`, testável sem socket: rede, chave do cliente, firmware, rotas, confirmação, gerentes sob `Origem.Como(Ia)`, recusas no histórico, eventos, cliente sumido;
  - abre e fecha com a chave;
  - revoga no PARAR IA.
  Junta as cinco travas num lugar só.
- [x] `src/BgsControle.Telas/IaPelaRedeViewModel.cs` e `ConfirmacaoIaViewModel.cs`, mais `ChavesViewModel.cs` e `RepositorioTextoArquivos.cs`:
  - estado Fechada, Pareando, Pareado ou Erro; código; endereço (IP:8787); cliente e data; Revogar; o seletor do modo do toque (`DefinirModoAsync`), com "Travar até PARAR IA" e "Pausar a IA"; o aviso "A IA perdeu a conexão.";
  - a folha "A IA pede para …" com o risco de `chaves-de-acesso.md` sem mudar o texto, o botão que diz o que faz e "Não deixar".
  A tela da 11 só liga o XAML.
- [x] `src/BgsControle.App/Platforms/Android/AndroidManifest.xml`: INTERNET e ACCESS_NETWORK_STATE, para a porta funcionar no Release.
- [x] `tests/BgsControle.Nucleo.Testes/PortaTestes.cs` e `tests/BgsControle.Telas.Testes/IaPelaRedeViewModelTestes.cs`:
  - um teste por linha da matriz;
  - um teste HTTP de ponta a ponta no loopback, com um `IRedeLocal` falso que aceita;
  - um teste com um handler da porta que escreve (a origem é IA e não vaza);
  - a senha "AB12" nunca aparece numa resposta.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md`: o registro no `MauiProgram` (porta, pareamento, confirmação, `IRedeLocal`) fica para a 11. Marca como feito o item da 9a sobre a origem nas entradas da porta.

**Acceptance Criteria:**
- Given um cliente pareado pela rede local, when ele lê `/estado`, muda o ganho da saída 3 e salva a memória 2 com o toque na folha, then as três respostas são `ok`, o histórico mostra as entradas IA e o controle da saída 3 mostra o valor novo em até 1 s.
- Given os testes que já existem, when a 9b entra, then todos continuam passando, e só o teste da `NotaIa` muda o que confere.

## Spec Change Log

## Review Triage Log

### 2026-10-22 — Revisão

- **Veredictos:** 46 achados. 0 high, 12 medium, 28 low, 6 false e 0 maybe-false. Viraram 18 grupos de patch (28 linhas), nenhum adiado e o resto rejeitado.

**Blind Hunter**
- `[medium]` `[patch]` **Nada limita o ritmo do `/parear`.** Com 5 erros por código e um código novo em seguida, um script na rede acha os 6 dígitos em minutos. Agrupado com E4.
  - Feito: as tentativas vão uma por vez, e cada código errado espera 1 s (`TimeProvider`).
- `[medium]` `[patch]` **Uma revogação durante um pareamento se perde.** Agrupado com E1, E2 e E3.
  - Feito: um contador de revogações. O pareamento que começou antes de uma revogação regrava o arquivo sem cliente e devolve 409 (`MsgRevogadoNoMeio`), sem `Liberar`. A leitura inicial ignora o cliente guardado se houve uma revogação.
- `[low]` `[patch]` **Revogação que não grava e cliente guardado com a chave desligada.** Agrupado com E5 e o achado extra da Verification Gap.
  - Feito: com a chave desligada, um cliente ainda pareado é revogado mesmo sem a porta ter aberto. Isso cobre a próxima abertura depois de uma gravação que falhou.
  - Uma gravação que falha com a chave ligada continua sem nova tentativa: depende de o disco falhar, raro demais para mais máquina.
- `[low]` `[patch]` **As recusas do `Conferir` antes da folha não entram no histórico.** Feito: passam pelo `RecusaDaPorta` e entram uma vez, como IA recusado.
- `[medium]` `[patch]` **O `RedeLocal` aceita a sub-rede dos dados móveis e da VPN.** Agrupado com E9.
  - Feito: ficam de fora os tipos Tunnel, Ppp e Wwanpp, e os nomes rmnet, ccmni, tun, ppp, clat e v4-rmnet.
- `[low]` `[patch]` **Triagem depois dos cabeçalhos e nenhum limite de conexões.** Agrupado com E10.
  - Feito: o 403 sai logo no aceite e o limite é de 16 conexões ao mesmo tempo.
  - O log das recusas continua sem limite de ritmo (rejeitado: só fora da rede local).
- `[low]` `[reject]` **Corridas no `Dispose` e folha aberta no `Dispose`.** São singletons da vida do app, e o conserto pede guardas novas.
- `[low]` `[reject]` **O vigia pode terminar a tarefa de um cliente que acabou de voltar.** A janela é de microssegundos, e o próximo pedido da IA a reativa.
- `[low]` `[patch]` **O `/eventos` não percebe o reinício do app.** Agrupado com E8.
  - Feito: um `desde` acima do último responde como 0.
  - Época e eventos de "tarefa terminada": rejeitado, porque mudaria o formato e a spec lista os eventos.
- `[low]` `[patch]` **`Folha(Chave.AjustesAudio, "", "")`.** Feito: a confirmação passa a ser `null`.
- `[low]` `[patch]` **O texto do `/ajuda` desatualizou.** Feito: o parar no só leitura, a chave desligada (409 sem folha), o 431 e o 500.
- `[low]` `[reject]` **O nome do cliente aceita caracteres de controle.** Só um cliente com o código da tela chega lá, e o conserto é uma guarda nova.
- `[low]` `[reject]` **A folha não mostra contagem nem diz por que fechou.** É acabamento da tela, da história 11.
- `[low]` `[patch]` **Comportamentos sem teste.** Ficaram cobertos pelos testes da Verification Gap e pelos novos da lista abaixo. Os de 431, `Transfer-Encoding` e prazos foram rejeitados como o VG7.
- `[false]` `[reject]` **A spec fica fora do diff.** Ela foi tirada do diff de propósito: vai só à camada de casos de borda. O arquivo existe.

**Verification Gap**
- `[low]` `[patch]` **Ligar e desligar a chave pelo `Mudou` não tinha teste.** Feito: testes que só chamam `DefinirAsync`.
- `[medium]` `[patch]` **Os resets nunca passavam pela porta.** Feito: título, risco e botão; "Não deixar" não manda `*004*` nem `*242*`.
- `[low]` `[patch]` **O "pedido esperando conta como presente" rodava com a IA inativa.** Feito: com a tarefa iniciada, a IA continua ativa depois de 31 s.
- `[medium]` `[patch]` **Nenhum teste de toque antigo decidindo um pedido novo.** Feito: o teste agora existe.
- `[medium]` `[patch]` **A metade "mesma sub-rede" da trava 3 nunca rodava.** Agrupado com IA4.
  - Feito: um construtor `internal` com a lista de interfaces e o teste com 192.168.0.5/24.
- `[low]` `[patch]` **O filtro de origem do `/eventos` não era conferido.** Feito: um `/ajuste` da IA antes, e o teste ainda acha um só "comando" da Tela.
- `[low]` `[reject]` **O prazo de 10 s do cabeçalho não tem teste.** Pediria 10 s reais ou um prazo injetável.
- `[low]` `[patch]` **Cliente guardado com a chave desligada na abertura.** É o mesmo grupo da revogação que não grava.

**Edge Case Hunter**
- `[medium]` `[patch]` **E1. Revogar durante a gravação do pareamento.** Grupo da revogação no meio.
- `[medium]` `[patch]` **E2. Desligar a chave com um `/parear` em andamento chama `Liberar`.** Mesmo grupo.
- `[medium]` `[patch]` **E3. Revogar antes da leitura inicial.** Mesmo grupo.
- `[medium]` `[patch]` **E4. Força bruta no código.** Grupo do ritmo do `/parear`.
- `[low]` `[patch]` **E5. O resultado da revogação é ignorado.** Grupo da revogação que não grava.
- `[low]` `[reject]` **E6. Desligar e religar a chave antes de a atualização rodar.** Ligar a chave passa pela folha de risco (segundos), e o conserto pede outro mecanismo.
- `[low]` `[reject]` **E7. Corrida no `Dispose`.** Igual ao do Blind Hunter.
- `[low]` `[patch]` **E8. `desde` acima do último.** Grupo do `/eventos`.
- `[medium]` `[patch]` **E9. Interfaces de dados móveis e VPN.** Grupo da rede local.
- `[low]` `[patch]` **E10. Conexões sem limite.** Grupo do servidor.
- `[low]` `[reject]` **E11. O laço de aceite gira em `SocketException` seguida.** Esgotar descritores no celular é improvável, e o conserto é uma guarda nova.
- `[low]` `[reject]` **E12. O laço sai sem cancelar e `Aberto` fica verdadeiro.** É improvável, e o conserto é uma guarda nova.
- `[low]` `[patch]` **E13. Corpo com BOM UTF-8.** Feito: o BOM é pulado.
- `[low]` `[reject]` **E14. PARAR IA entre o `Conferir` e a folha.** A folha abre, mas o envio é recusado pela sessão com "A IA foi parada."; não há efeito no aparelho.
- `[false]` `[reject]` **E15. No só leitura, o `DELETE /tarefa` e o parar são recusados.** É o que a spec manda ("qualquer POST ou DELETE"). No só leitura a IA nunca fica ativa, porque todo POST é recusado.
- `[low]` `[patch]` **E16. Revogar sem cliente dispara o PARAR IA.** Feito: volta cedo sem `PodeRevogar`.
- `[low]` `[reject]` **E17. O IP muda com a tela aberta.** A tela se atualiza no próximo aviso; assinar a mudança de rede é da história 11.
- `[false]` `[reject]` **E18. O "10 s" vira 20 s com o corpo.** A spec fala do cabeçalho; o corpo tem um prazo próprio, que ela não proíbe.
- `[false]` `[reject]` **E19. O teste do gerador mudou.** As asserções são as mesmas. Só a espera passou a incluir a mensagem, e ela falhava 1 vez em 3 antes da 9b.

**Intent Alignment**
- `[low]` `[reject]` **IA1. A porta fica inerte no app real.** O `stories.yaml` que a intenção cita põe o XAML e a navegação na 11 e o aparelho na 12. Está no `deferred-work.md`.
- `[false]` `[reject]` **IA2. O salvar confirma sempre.** As 3 memórias sempre existem, então salvar é sempre sobrescrever.
- `[false]` `[reject]` **IA3. Caminhos além do mínimo.** Vêm das travas e do sucesso da CAP-12 (ler, ajustar, salvar); não é defeito.
- `[medium]` `[patch]` **IA4. O caminho de aceitar do `RedeLocal` não tinha teste.** Grupo da mesma sub-rede.

**Testes novos dos patches**
- a espera de 1 s;
- a revogação no meio do pareamento e antes da leitura;
- a revogação com a chave desligada na abertura;
- a recusa do `Conferir` no histórico;
- o `desde` acima do último;
- o BOM;
- o 403 imediato no loopback;
- o Revogar sem cliente.

**Verificação depois dos patches:** Núcleo 809 de 809, Telas 313 de 313, build Android com 0 erros e 0 avisos.

## Design Notes

`TcpListener` e não `HttpListener`: no Windows o `HttpListener` usa o http.sys e pede permissão de administrador para escutar fora do localhost, e no Android usa outra implementação. Um HTTP mínimo próprio se comporta igual nos testes e no celular. O `PortaComandos.AtenderAsync` separa a regra do socket: quase todo teste roda sem rede.

```csharp
// dentro do PortaComandos, depois da rede, da chave do cliente e do firmware
using (Origem.Como(OrigemComando.Ia))
{
    if (_sessao.Conferir(quadro) is { } motivo) return Recusa(motivo);       // sem folha
    if (exige && !await _confirmacoes.PedirAsync(titulo, risco, acao)) return Recusa(...);
    var r = await _memorias.SalvarAsync(n - 1, nome);                          // 1..3 → 0..2
}
```

**Padrões escolhidos aqui, abertos à revisão do Denis:**
- porta 8787;
- prazo da folha de 60 s;
- 5 códigos errados geram outro;
- 30 s sem pedidos, "A IA perdeu a conexão.";
- desligar a chave apaga o cliente;
- o salvar memória confirma sempre;
- a tela mostra o endereço IP:8787;
- os eventos chegam por consulta (`/eventos?desde=N`).

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` e `dotnet test app-controle/tests/BgsControle.Telas.Testes`: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug`: 0 erros e 0 avisos.

## Auto Run Result

Status: done

**O que entrou.** A porta de comandos da IA no núcleo, testável sem socket:
- HTTP/1.1 mínimo sobre `TcpListener`, com a triagem da rede local logo no aceite e no máximo 16 conexões;
- pareamento por código de 6 dígitos, com a chave guardada só como SHA-256, tentativas uma por vez e 1 s por código errado;
- confirmação na tela das operações de risco, com prazo de 60 s;
- PARAR IA e Revogar apagando o cliente;
- só leituras com firmware diferente de R003;
- recusas da porta no histórico, `/eventos` por consulta e o aviso do cliente sumido em 30 s.

As telas `IaPelaRedeViewModel` e `ConfirmacaoIaViewModel` também entraram, e o manifesto ganhou INTERNET e ACCESS_NETWORK_STATE.

**Arquivos:**
- `app-controle/src/BgsControle.Nucleo/Porta/ServidorHttp.cs`: o servidor HTTP, a triagem no aceite e o limite de conexões.
- `app-controle/src/BgsControle.Nucleo/Porta/RedeLocal.cs`: as faixas privadas, a mesma sub-rede e sem dados móveis nem VPN.
- `app-controle/src/BgsControle.Nucleo/Porta/Pareamento.cs`: o código, a chave, o `porta-ia.json` e as revogações.
- `app-controle/src/BgsControle.Nucleo/Porta/PedidosConfirmacao.cs`: a folha, um pedido por vez, os 60 s.
- `app-controle/src/BgsControle.Nucleo/Porta/PortaComandos.cs`: as cinco travas, os caminhos e os eventos.
- `app-controle/src/BgsControle.Nucleo/Porta/JsonPorta.cs`: as respostas e a leitura do corpo.
- `app-controle/src/BgsControle.Telas/IaPelaRedeViewModel.cs` e `ConfirmacaoIaViewModel.cs`: as duas telas.
- `ChavesViewModel.cs`, `RepositorioTextoArquivos.cs` e `AndroidManifest.xml`: a nota da chave, o arquivo novo e as permissões de rede.
- `tests/BgsControle.Nucleo.Testes/PortaTestes.cs` e `tests/BgsControle.Telas.Testes/IaPelaRedeViewModelTestes.cs`: a matriz e os patches.
- `tests/BgsControle.Telas.Testes/GeradorViewModelTestes.cs`: a espera inclui a mensagem; antes falhava 1 vez em 3.
- `_bmad-output/implementation-artifacts/deferred-work.md`: o item da origem fechado e o registro no `MauiProgram` deixado para a 11.

**Revisão:**
- 46 achados: 18 grupos de patch (6 de verdict medium e 12 low), nenhum adiado, o resto rejeitado com o motivo no Review Triage Log.
- **Segunda revisão recomendada** (2 ou mais medium com patch). O risco que nenhum teste prova é o comportamento do `NetworkInterface` no Galaxy A52: o filtro de dados móveis e VPN por tipo e nome, a sub-rede do Wi-Fi e o IP mostrado na tela.

**Verificação:**
- Núcleo 809 de 809, Telas 313 de 313, build Android com 0 erros e 0 avisos.
- A auditoria da matriz deu um teste para cada linha, e todos rodaram.

**Riscos que ficam:**
- O `MauiProgram` ainda não monta a porta (história 11), então no app real a porta não abre.
- O prazo de 10 s do cabeçalho não tem teste.
- Uma revogação cuja gravação falha com a chave ligada pode voltar depois de reabrir o app.
- Os padrões escolhidos (porta 8787, 60 s, 5 códigos, 30 s, desligar apaga o cliente, salvar sempre confirma) esperam a revisão do Denis.
