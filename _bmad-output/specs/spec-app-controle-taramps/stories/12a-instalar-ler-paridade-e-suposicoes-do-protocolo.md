---
title: 'Instalar, ler, paridade e suposições do protocolo'
type: 'chore'
created: '2026-10-24'
status: 'done'
baseline_commit: 'cec4d7cb136e2b57b5a2defa8ef99dc157146160'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/conhecimento-herdado.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Da história 3 à 11, tudo foi feito contra o processador simulado. Ninguém conferiu no PRO 2.4BT a leitura do app, a paridade com o app da Taramps (teste 3 do sinal de sucesso) nem as suposições de protocolo do simulador.

**Approach:** Instalar o APK Debug no A52, conectar e ler; comparar valor a valor com o app da Taramps no estado atual, depois de carregar a memória 3 do aparelho (FILME) e depois de aplicar a FILME herdada do celular; medir as 19 suposições de protocolo da lista abaixo e corrigir o simulador onde alguma cair.

## Boundaries & Constraints

**Always:**
- Um passo por vez para o Denis, dizendo o que conferir e o que responder. O app da Taramps é operado por mim, por adb.
- Antes de carregar ou aplicar uma memória, o retrato atual (`ultimo-retrato.json`) é copiado para `estado/leituras/`.
- O app da Taramps e este app nunca ficam conectados juntos; depois de usar o app da Taramps, este app relê tudo. Vale também para o cliente BLE do notebook.
- No app da Taramps: não deslizar memória na tela Salvar; não abrir a tela Limiter com o Auto ligado.
- Suposições desta história: ACK-241, CONTEUDO-MEMORIA, MEMORIAS-INICIAIS, ACK-001, RESET-004, RESET-242, FABRICA, FORMATO-ECO, MODO-ESTRITO, ESCRITA-INVALIDA, MUDO-SEM-SENHA, QUEDA-APOS-ERRO, VOLTIMETRO-CRLF, MEMORIA-N-NOME, CARREGAR-SEM-NOMES, SENHA-ERRADA-DESAUTENTICA, FILA-UNICA, QUEDA-DESCARTA-FILA, ATRASO-RESPOSTAS. As outras 11 são da 12b.
- Uma suposição que cair muda junto o `ProcessadorSimulado`, o `Suposicoes.cs` e o `SUPOSICOES.md`, e os testes continuam passando.
- Decisão do Denis (24/10/2026): reset de fábrica, reset de áudio e troca de senha são testados só na recusa. ACK-001, RESET-004, RESET-242 e FABRICA ficam "não medido, decisão do Denis de 24/10".

**Never:**
- Mandar `*004*`, `*242*`, `*001*`, `240` ou `23N` ao aparelho.
- Escrita fora do texto do codificador do app (MODO-ESTRITO e ESCRITA-INVALIDA ficam "não medido": o app só manda o texto do codificador, e medir exigiria escrita fora do guardião).
- Mudar os limites do guardião ou os valores das memórias do aparelho. Push ou PR.

## I/O & Edge-Case Matrix

| Cenário | Estado | Esperado | Se falhar |
|---|---|---|---|
| Conectar | Senha guardada | "Conectado · firmware R003" e 84 de 84; a senha não aparece no logcat | Corrigir e repetir |
| Conferência do Setup | Aparelho fora da calibração do Casa 2.1 | Dúvida: muta e pede escolha (comportamento certo, não é falha) | — |
| Paridade | Estado atual; memória 3 carregada; FILME do celular aplicada | Cada valor igual ao do app da Taramps | Corrigir o decodificador ou registrar a lacuna do MAPA |
| Suposição de protocolo | Leitura, senha e carregar | Resultado escrito na tabela | Corrigir o simulador e os testes |

</frozen-after-approval>

## Code Map

- `app-controle/README.md:95-130` -- build, instalação por `C:\platform-tools\adb.exe install -r …\br.com.bgs.controle-Signed.apk`, logcat da senha e `run-as br.com.bgs.controle cat files/ultimo-retrato.json`.
- `app-controle/tests/BgsControle.Simulador/SUPOSICOES.md:28-61` -- tabela "A conferir na história 12"; a coluna "Resultado no aparelho" recebe o medido, com data e a fonte (log ou print).
- `app-controle/tests/BgsControle.Simulador/Suposicoes.cs:9` e `ProcessadorSimulado.cs` (`// Suposição <ID>`) -- mudam junto; `SuposicoesTestes` amarra os dois.
- `ferramentas/cliente_ble.py` -- `ClienteTaramps.autenticar`, `bruto` e `enviar` só deixam passar consulta e senha: servem para MUDO-SEM-SENHA, QUEDA-APOS-ERRO, SENHA-ERRADA-DESAUTENTICA, VOLTIMETRO-CRLF e os atrasos. O script da sonda fica no scratchpad, e o log em `estado/log-ble/`.
- `estado/leituras/2026-10-18_2145.json` -- referência da FILME; `estado/memorias.json` -- nomes das 3 memórias.
- `_bmad-output/specs/spec-app-controle-taramps/stories/4-infraestrutura-testes-processador-simulado.md:107` e `6-memorias-link-copia-antes.md:45,50` -- pendências "no aparelho" desta parte: fechar.

## Tasks & Acceptance

**Execution:**
- [x] `app-controle/src/BgsControle.App` -- build Debug, instalar, conectar, 84 de 84, logcat sem a senha -- base.
- [x] `estado/leituras/` -- retrato atual; comparar com as telas do app da Taramps -- paridade 1.
- [x] Carregar a memória 3 pelo app (241) e comparar de novo; conferir nomes, gerador, 251 e 252 iguais -- paridade 2, ACK-241, CONTEUDO-MEMORIA, CARREGAR-SEM-NOMES, MEMORIA-N-NOME.
- [x] Aplicar a FILME herdada do celular e comparar de novo; o eco de uma escrita no registro -- paridade 3, FORMATO-ECO.
- [x] Sonda pelo notebook (app desconectado) -- as suposições de senha, voltímetro, fila e atrasos.
- [x] `app-controle/tests/BgsControle.Simulador/SUPOSICOES.md` -- escrever as 19; corrigir o simulador e os testes onde cair.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- o que ficar aberto.

**Acceptance Criteria:**
- Given as 19 suposições desta história, when ela fecha, then cada linha tem resultado (medido, ou "não medido" com o motivo).
- Given as três paridades, when ela fecha, then cada diferença foi corrigida ou está no deferred-work com o valor dos dois apps.
- Given qualquer correção de código, when ela fecha, then os dois projetos de teste passam e o build Android dá 0 erros e 0 avisos.

## Implementation Notes

**24/10/2026, 15:40–16:10 (pausa pedida pelo Denis; o adb caiu na mesma hora).** A implementação é conduzida na sessão principal, sem subagente, porque precisa do Denis e do aparelho.

- Build Debug: 0 erros e 0 avisos. APK instalado no A52 (`br.com.bgs.controle`).
- Ao abrir, antes de conectar, o Início mostra "Sem Setup". Depois de conectar, a conferência deu o Casa 2.1 direto, com a memória FILME e o preset Cinema.
- Conectado · R003. A leitura deu 84 de 84 em 12,4 s e 14 de 15 extras: o `#005#` ficou sem resposta, com o órfão `ERRO 0`, como na história 2. Retrato em `estado/leituras/2026-10-24_1551-app-atual.json`.
- Logcat: as 4 ocorrências da senha (4 dígitos) são coincidências de outros processos (SurfaceFlinger, Instagram, SDHMS, Watchdog). Nenhuma veio do app.
- A leitura do app é igual à FILME de 18/10 (`2026-10-18_2145.json`) nos 84 valores. Achados: a memória 1 se chama "SPL" (salva em 20/10), o 252 está em 0 (módulo OFF) e o 251 em 0.
- Paridade 1 com o app da Taramps, até aqui: a Home (OFF, Áudio BT off, Master 0,0), o roteamento (B, A, A+B, A+B), as 15 bandas com o preset Custom, o EQ de entrada (0,0 / 1000 / 1,0) e o crossover do canal 1 (150 LR24, PB 22000 OFF). Tudo igual. Falta: crossover dos canais 2 a 4, alinhamento, fase, limiter (o auto está em manual nos 4, então a tela é segura), nível e EQ de saída.
- O app da Taramps mostra o voltímetro em 13,8 V, e o nosso não recebe resposta ao `#005#`. Fica para a 12b (voltímetro e VOLTIMETRO-ESPONTANEO).
- O app da Taramps não deixa trocar de aba com o módulo em OFF. Com o OK do Denis, liguei o módulo (ON) pelo app da Taramps. **O módulo ficou ligado** e o app da Taramps ficou conectado ao processador.
- Ferramentas no Git Bash: usar `MSYS_NO_PATHCONV=1` no `adb shell uiautomator dump /sdcard/...`. Os scripts `tela.sh` e `toque.sh` ficaram no scratchpad da sessão. No crossover da Taramps, as abas de canal ficam em y=288 (x 165/414/661/910); não tocar nas caixas "Canal N" abaixo do gráfico.

**24/10/2026, 16:50–17:35 (retomada).**

- Paridade 1 completa: crossover dos 4 canais, alinhamento (0/100/80/80 cm, fases 0/0/180/180), limiter (−15/−18/−12/−12, 50 ms, 200 ms, Auto desligado), nível (10/7/6/6, mudo em "com som"), EQ de saída (2 kHz +2,5 Q 0,7 nas JBL; 45 Hz +3 Q 0,7 no sub) e gerador (OFF, −45 dB, 1000 Hz). Tudo igual ao nosso app.
- Para medir o ack do 241 pelo app, o APK Debug passou a gravar o registro do BLE, já mascarado, em `files/registro-ble.txt` (`MauiProgram.cs`, só com `#if DEBUG`). O registro continua fora do logcat. A senha aparece como `*000*••••*`, e o mascaramento não apagou nenhum outro valor. Log em `estado/log-ble/2026-10-24_1712-app-12a.log`.
- Paridade 2: carregar a memória 3 (`*241*2*` → `#241#2#` em 1,41 s) deixou os 84 valores, os nomes, o gerador e o 251 iguais. O 252 foi para 1 porque eu liguei o módulo pelo app da Taramps. O app mostra "Memória 3 do aparelho (app da Taramps)", e o histórico registra "Tela · CarregarMemoria · 241 · 2".
- Fora do plano, para a paridade 3 provar alguma coisa: carreguei a memória 1 (SPL), porque a FILME do celular sobre a memória 3 não escreveria nada. Ela mudou 39 valores, e com eles o 200 (−45 → −25) e o 201 (1000 → 51), então CONTEUDO-MEMORIA caiu. Os nomes, o 202, o 251 e o 252 não mudaram.
- Paridade 3: a FILME do celular por cima da SPL fez 31 escritas, todas com eco no formato da leitura (FORMATO-ECO). A releitura ficou idêntica à memória 3 do aparelho nos 84 valores. Só o 200 e o 201 ficaram com os valores da SPL, porque o gerador não faz parte da memória do celular. O app da Taramps mostrou −25,0 dB e 51 Hz na tela do gerador.
- Atrasos medidos no A52: senha em 115 ms; leituras em 73–189 ms (mediana 94); eco em 60–240 ms; eco da fase em ~1,1 s (o `PrazoEco` é de 2 s).
- Sondas pelo notebook (só consultas e senhas; `estado/log-ble/2026-10-24_1725-sonda-12a.log` e `…1730-sonda-12a-b.log`):
  - antes da senha, a leitura recebe `ERRO 0`;
  - um ERRO seguido da senha certa não derruba a conexão;
  - dois ERRO seguidos derrubam a conexão 28,3 s depois do primeiro;
  - já autenticado, uma senha errada recebe a resposta da certa e a sessão continua;
  - o `#005#` responde `ERRO 0` com CR LF;
  - a 1122 só aceita escrita com resposta, então não sobra fila na queda.
  - A primeira sonda parou na parte E (o cliente recusou o `#200#` antes do BLE) e foi refeita na segunda.
- Simulador corrigido nas 3 que caíram: CONTEUDO-MEMORIA (a memória guarda o 200 e o 201), MUDO-SEM-SENHA (leitura antes da senha → `ERRO 0`) e SENHA-ERRADA-DESAUTENTICA (a senha errada já autenticado é respondida como a certa). O texto mudou junto no `Suposicoes.cs` e no `SUPOSICOES.md`. Os testes `Antes_da_senha_…` e `Senha_errada_depois_da_certa_…` foram trocados pelo comportamento medido, e entrou `A_memoria_guarda_o_ganho_e_a_frequencia_do_tom_…`. ATRASO-RESPOSTAS ficou "em parte": o simulador mantém os atrasos fixos, porque os prazos do app são de 2 s ou mais.
- As 19 suposições têm resultado na tabela: 8 confirmadas, 3 caíram, 1 em parte, 1 não se aplica, 1 não medível e 6 não medidas (4 pela decisão do Denis; MODO-ESTRITO e ESCRITA-INVALIDA pela spec).
- Testes: Núcleo 1010 e Telas 448 passando. Build Android com 0 erros e 0 avisos.
- No fim, carreguei de novo a memória 3: o aparelho voltou ao estado do começo, menos o 252 (o módulo continua ligado). Uma tentativa anterior de recarregar falhou sem enviar nada (o app não tinha conectado), e o arquivo `fim-12a.json` dela foi apagado.
- Pendências do aparelho fechadas aqui: a história 4:107 (instalar, conectar, leitura automática). Os itens 6:45 e 6:50 (formato do 23N e resposta ao 240) ficaram sem objeto, porque a 8c tirou do app o salvar e o renomear. O DI do MauiProgram foi conferido em parte (ver o deferred-work). Ficam para a 12b: o voltímetro, o 202 na memória, o "Sem Setup" antes de conectar e a nota "MUSICA HIGH" do conhecimento herdado.

- Correções da revisão: a contagem certa é 6 confirmadas, 2 caíram, 2 em parte, 1 não se aplica, 2 não medíveis e 6 não medidas. FILA-UNICA passou de "confirmada" para "não medível", porque as respostas nunca se sobrepõem. Os atrasos do notebook são de 23–175 ms na senha e 20–223 ms nas leituras. Os "dois ERRO seguidos" da sonda foram duas senhas erradas. A nota "MUSICA HIGH" do conhecimento herdado é decisão do Denis (deferred-work), não da 12b. O `registro-ble.txt` recomeça a cada abertura do app, e gravar o arquivo não mudou o tempo da leitura (12,1–12,4 s com ele, 12,4 s sem ele).

## Spec Change Log

## Review Triage Log

Revisão 1 (24/10). O diff dos revisores deixou de fora as 6 leituras em JSON de `estado/leituras/` (dado cru medido, ~10 mil linhas).

| # | Revisor | Achado | Veredito | Rota | Evidência |
|---|---|---|---|---|---|
| 1 | blind | A contagem das 19 suposições nas notas soma 20 | low | patch | Eram 7 confirmadas, não 8. Corrigido nas notas (e a FILA-UNICA mudou de classe, item 9). |
| 2 | blind, verif., edge | `CopiaAntes` não volta o 200 e o 201 depois de carregar memória | low | defer | `CopiaAntes.cs:423` só percorre o Marco 1. O gerador reescreve 201 e 200 antes de tocar, então o som não muda. Entrada no deferred-work. |
| 3 | blind, verif., edge | O teste de memória afirma o 202, o 251 e os nomes sem mudá-los | medium | patch | Afirmações vazias, confirmadas. O teste agora muda o nome entre salvar e carregar e não diz nada do 202 nem do 251/252. Comentário e texto da CONTEUDO-MEMORIA marcam essa parte como não medida. |
| 4 | blind, edge | O `registro-ble.txt` cresce sem limite | low | patch | Confirmado. Agora o arquivo é apagado a cada abertura do app. |
| 5 | blind, edge | O I/O síncrono no `Registrou` atrasa o BLE e as medidas | false | — | As durações da leitura são iguais com e sem o arquivo (12,4 s antes; 12,1–12,4 s depois). |
| 6 | blind, edge | O catch só pega `IOException` e `UnauthorizedAccessException` | low | patch | Outra exceção subiria pelo `LogBle.Registrar` até o BLE. Agora é `catch (Exception)`. |
| 7 | blind | O Spec Change Log está vazio apesar dos desvios | false | — | O template reserva o Change Log para os loopbacks da revisão. Os desvios (SPL, `MauiProgram`, módulo ON) estão nas Implementation Notes. |
| 8 | blind | O módulo ficou ligado e nada registra | false | — | Está nas Implementation Notes, e o Denis foi perguntado se quer desligar. |
| 9 | blind | FILA-UNICA "confirmada" com prova que não testa sobreposição | low | patch | Correto: as respostas nunca se sobrepõem. Passou para "não medível". |
| 10 | blind, edge | Leitura antes da senha: o `ERRO 0` arma a queda? E a escrita antes da senha? | maybe-false | defer | Não medido. A escrita já estava marcada assim. A queda vai para o deferred-work com o jeito de medir. |
| 11 | blind, edge | Os atrasos do simulador não usam os medidos (fase em ~1,1 s) | low | defer | O `PrazoEco` de 2 s cobre, e se estourar o escritor relê. Entrada no deferred-work para simular a fase. |
| 12 | edge | Os atrasos do notebook no md estão errados | medium | patch | Os logs dão 23–175 ms na senha e 20–223 ms nas leituras. Corrigido no md e nas notas. |
| 13 | edge | O texto da ATRASO-RESPOSTAS não mudou, apesar de ter caído em parte | low | patch | Texto atualizado no `Suposicoes.cs` e no md. |
| 14 | edge | O comentário da ACK-241 ainda diz "não medido" | low | patch | Comentário atualizado (1,41 s medidos). |
| 15 | blind | Os logs citados ficam fora do git | low | rejeitado | É regra do projeto (`.gitignore:31`). O log de 20/10 citado no md também fica fora. |
| 16 | blind | O teste antes da senha mistura leitura e escrita | low | patch | Agora são separados, e a leitura respondida não conta mais como "recusada". |
| 17 | blind | Contabilidade inconsistente (DI, `stories.yaml`, cabeçalho do md, MUSICA HIGH) | low | patch | Texto corrigido em cada lugar. O nome do id SENHA-ERRADA-DESAUTENTICA ficou, com a nota "era a suposição contrária". |
| 18 | verif. | Nenhum teste confere a máscara no argumento do `Registrou` | medium | patch | Entrou `O_evento_Registrou_entrega_a_linha_ja_mascarada`. |
| 19 | edge | Carregar memória com o tom tocando faz o ganho saltar | maybe-false | defer | Medido que o 241 troca o 200. Falta saber se o app deixa carregar com o gerador ligado (12b). |
| 20 | edge | Depois do `*004*`, as memórias de fábrica não têm 200/201 | low | rejeitado | O reset é todo suposição e não foi medido. A correção acrescentaria complexidade. |
| 21 | edge | Código desconhecido antes da senha segue mudo | low | rejeitado | Especulação sem medida. O app nunca manda isso. |

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` e `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: tudo passa.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros, 0 avisos.

**Manual checks:**
- No A52, com o Denis: cada tarefa, com print quando houver valor comparado.
