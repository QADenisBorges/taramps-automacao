---
title: 'Mapa do protocolo BLE do PRO 2.4BT + AUDIO'
type: 'feature'
created: '2026-10-16'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '001c761426168f575c7bf636b66e8bfbc7044ec1'
context:
  - '{project-root}/_bmad-output/specs/spec-taramps-automacao/SPEC.md'
  - '{project-root}/_bmad-output/specs/spec-taramps-automacao/protocol-discovery.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A IA não tem como ler nem escrever no PRO 2.4BT + AUDIO porque o protocolo BLE do app é proprietário e não está documentado. Sem esse mapa, nenhuma outra história (leitura, escrita, guardião) pode começar (CAP-1).

**Approach:** Aprender o protocolo por duas fontes: a análise estática do APK e capturas do HCI snoop log feitas pelo Denis, um parâmetro por vez, com valor antigo e novo anotados. O resultado é um mapa versionado com a codificação de cada parâmetro do Marco 1 e dos comandos de autenticação, leitura e escrita. A história para no critério de parada.

## Boundaries & Constraints

**Always:**
- A IA só analisa capturas prontas e anotadas; não analisa bytes sem anotação.
- Cada codificação do mapa cita a captura que a prova (arquivo + quadro + valor antigo/novo).
- Antes de mapear parâmetros, o veredito sobre proteção (criptografia, ofuscação, desafio-resposta) sai do APK e da captura C0.
- O processador fica desligado do amplificador durante todas as capturas.
- Orçamento: cerca de 2 dias de análise, contados a partir da entrega da captura C0. A data de início fica registrada no mapa.

**Decisões do Denis (2026-10-16):**
- APK e ferramentas: o Denis instala o Android platform-tools e extrai o APK do próprio celular (`adb shell pm path` + `adb pull`). O snoop log sai por `adb bugreport`. A IA baixa o jadx do release oficial no GitHub, com a autorização dele no momento do download.
- Preset exportado: entra como fonte. O Denis exporta 3 presets em arquivo: A (o ajuste em uso, que também é o backup, antes de qualquer captura), a memória "Factory default" do aparelho (carregada sem reset e exportada como referência neutra) e B (com valores conhecidos e anotados). Esclarecido pelo Denis em 2026-10-16. O `protocol-discovery.md` é corrigido, porque o app exporta e importa presets (manual, p. 14).
- Capturas: os logs brutos e os arquivos de preset ficam fora do git. Só as anotações e os trechos citados vão para o git, com a senha mascarada.
- Ordem (2026-10-16): primeiro a fase Demo, sem o processador: APK, levantamento das telas, pista do limiter e presets exportados no Modo Demo (Parte D do roteiro). O processador, sempre sem amplificador, só entra nas capturas quando o Denis liberar. O Modo Demo não gera tráfego BLE, então as capturas C0…C15 dependem dessa liberação.
- Automação da fase Demo (2026-10-16): o Denis pediu que o levantamento do Modo Demo fosse feito e anotado automaticamente. A IA opera o app via `adb` (toques e leitura da árvore de interface) só no Modo Demo, sem tocar em "Conectar". Isso não muda o non-goal da SPEC: a integração final não opera o app.
- Limiter: a resposta vem do protocolo e do APK e também de uma medição. O Denis mede com multímetro AC direto na saída RCA, sem amplificador, usando o gerador de senoide do app. Ele varia o ganho da saída com o threshold fixo e anota a tensão.

**Never:**
- Commitar o APK, o código decompilado ou trechos dele (nada do app é redistribuído).
- Conectar o notebook ao processador ou enviar comandos a ele (isso é da história 2).
- Capturar ou acionar "Reset Audio Settings", "Factory Reset" ou trocar a senha.
- Quebrar criptografia à força.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Bytes legíveis | Captura de 1 parâmetro com antigo→novo | Codificação (handle, comando, offset, tipo, escala) no mapa | N/A |
| Escala não linear | 3+ valores do mesmo parâmetro, incluindo mín. e máx. | Fórmula ajustada ou tabela no mapa | Pedir valores extras ao Denis |
| Protocolo protegido | Payload muda sem relação com o valor, ou crypto/ofuscação no APK | Parar e recomendar procurar a Taramps | Relatório com a evidência |
| Orçamento esgotado | 2 dias sem bytes legíveis | Parar e recomendar procurar a Taramps | Relatório do que foi tentado |
| Captura ambígua | Mais de uma escrita sem anotação correspondente | Captura marcada como inválida | Pedir refazer só aquele passo |

</frozen-after-approval>

## Code Map

O repositório ainda não tem código, só a spec e o BMAD. Fatos levantados:
- `C:\Users\denis\Desktop\Som\componentes\manuais\Taramps_PRO-2.4BT-AUDIO_manual_ENG.pdf`: o app se chama "PRO 2.4BT + AUDIO" e usa BLE 5.4 com senha de 4 dígitos. O firmware aparece como P012 e o app como 1.0 (23). O FCC ID é 2BQGR-PRO24BTAUDIO (as fotos internas na FCC podem revelar o chip BLE). O manual traz as faixas da p. 19–20, o preset exportável (p. 14) e o threshold do limiter exibido em volts ou como OFF (p. 11).
- `_bmad-output/specs/spec-taramps-automacao/damage-thresholds.md`: lista os parâmetros e faixas que o guardião vai consumir; o mapa usa os mesmos nomes.
- Parâmetros do Marco 1: master (−80..0 dB); por saída, ganho (−45..+15 dB), mute, roteamento A/B/A+B, HPF e LPF (frequência 10 Hz–22 kHz, tipo BT/LR, inclinação ou OFF), delay (0–272 cm), fase 0/180, EQ paramétrico (F, G ±12, Q 0,4–10) e limiter (threshold −48..0 dB ou OFF, ataque, release, Auto); na entrada, EQ gráfico de 15 bandas (0,1 dB), preset de EQ e EQ paramétrico. Também entram ON/OFF, a autenticação, a leitura completa ao conectar e o link de canais, se existir. O número de bandas do PEQ sai do APK.

## Tasks & Acceptance

**Execution:**
- [x] `.gitignore`: ignorar `protocolo/capturas/bruto/`, `*.apk`, `apk/` e os arquivos de preset exportados, porque nada do app nem a senha vão para o git.
- [x] `protocolo/ROTEIRO-CAPTURA.md`: escrever o passo a passo do Denis em 4 partes.
  - Preparação: platform-tools, opções de desenvolvedor, snoop log, extração do APK e do log por `adb bugreport`.
  - Captura C0: conectar com a senha, sem mexer em nada.
  - Capturas C1…Cn: um parâmetro por passo, com os botões +/− e não arrastando; mínimo, máximo e 1 valor intermediário. A tabela de anotação tem horário, parâmetro, canal e valor antigo→novo.
  - Exportação dos 2 presets e medição do limiter com multímetro AC na saída RCA, sem amplificador.
- [x] `_bmad-output/specs/spec-taramps-automacao/protocol-discovery.md`: corrigir a frase sobre exportação de presets e citar o manual, p. 14.
- [x] `ferramentas/btsnoop_att.py`: parser em stdlib que lê o btsnoop e lista as operações ATT (escrita, notificação, leitura) com horário, handle e hex. Ele cruza a lista com a tabela de anotação.
- [x] `ferramentas/tests/test_btsnoop_att.py`: testes com um btsnoop sintético (cabeçalho, ACL fragmentado, Write Request/Command, Notification).
- [x] `protocolo/APK.md`: framework do app, UUIDs GATT, montadores de comando, conversões (dB, Hz, cm, V) e veredito sobre proteção. Só descrições, sem código copiado.
- [ ] `protocolo/MAPA.md`: data de início, serviço e características, autenticação, leitura completa, formato de escrita, estrutura do arquivo de preset, uma tabela por parâmetro (codificação, escala, faixa, captura que prova), limiter vs. ganho com a unidade do threshold e a medição, e lacunas.

**Acceptance Criteria:**
- Given as capturas anotadas, when o mapa é concluído, then cada parâmetro do Marco 1 tem codificação e pelo menos uma captura de antigo→novo citada.
- Given o mapa, when se lê a seção do limiter, then ela diz se o limiter atua antes ou depois do ganho da saída e qual é a unidade do threshold no protocolo, com a evidência.
- Given o critério de parada disparou, when a história termina, then existe um relatório com a evidência e a recomendação de procurar a Taramps.
- Given o repositório, when se roda `git ls-files`, then não aparece APK, código decompilado nem log bruto com senha.

## Design Notes

Ordem de trabalho para gastar pouco orçamento. O APK vem primeiro: se houver criptografia ou ofuscação pesada, a história para antes de o Denis fazer dezenas de capturas. Se o app for Flutter ou React Native, a lógica fica em `libapp.so` ou num bundle JS, e o mapa passa a depender mais das capturas. A captura C0 diz se a senha trafega em claro e se o app baixa o estado completo ao conectar (a hipótese do `protocol-discovery.md`). Gestos de arrastar geram rajadas de escritas, por isso o roteiro manda usar os botões +/−.

## Verification

**Commands:**
- `python -I -m unittest discover -s ferramentas/tests`: espera todos os testes passando.
- `git ls-files | grep -Ei "\.apk|/bruto/|/presets/|btsnoop_hci|bugreport"`: espera saída vazia. O padrão não casa com `ferramentas/btsnoop_att.py`, que é a ferramenta e não um log.

**Manual checks (if no CLI):**
- Para 2 parâmetros escolhidos ao acaso, conferir a codificação do mapa contra o hex da captura citada.
- Nenhum trecho de captura versionado mostra a senha BT sem máscara.

## Implementation Notes

- 2026-10-16: entregues a ferramenta, o roteiro, a correção do `protocol-discovery.md` e os esqueletos de `APK.md` e `MAPA.md`. As duas últimas tarefas dependem do APK e das capturas do Denis.
- Revisão (3 camadas) feita em 2026-10-16: as correções da triagem foram aplicadas, 42 testes passam e a checagem do git está vazia. A história volta para in-progress, aguardando o APK e a C0. O achado 11 foi resolvido pelo Denis: exportar A, Factory default (F) e B; o roteiro §4.1 e o MAPA foram atualizados.
- Correção pós-implementação: a leitura da medição do limiter agora considera o ceifamento da Série 1 perto de 2,1 V e pede para anotar o threshold que a tela mostra a cada ponto.

- 2026-10-16, fase Demo e APK:
  - Levantamento automático do Modo Demo via `adb` (`protocolo/capturas/demo-telas.md`). Os presets do celular foram lidos direto da pasta do app (`Android/data/.../files/presets`), sem compartilhar nada.
  - O jadx 1.5.6 decompilou o app. **O protocolo BLE é texto ASCII sem cifra:** autenticação `*000*SENHA*` em claro, leitura `#CCC#c#`, escrita `*CCC*c*valor*`, canal de 0 a 3. O critério de parada não disparou.
  - O `MAPA.md` v1 tem a codificação de todos os parâmetros do Marco 1, lida do código. A tarefa do mapa segue aberta só pela prova por captura, que espera a liberação do processador.
  - Limiter: o threshold vai em dB (−48 a 0; `0.5` = OFF), e os volts existem só na UI (2,0 × 10^(dB/20)). A posição em relação ao ganho segue em aberto e depende da medição 4.2.
  - O `.trps` é AES-GCM com chave fixa no app. Não foi decifrado (desnecessário, e seria contornar uma proteção), e a chave não está no repositório.
  - Não existe memória "Factory default" no app; o preset F da decisão não tem origem.

## Spec Change Log

## Review Triage Log

| # | Origem | Achado | Veredito | Evidência | Rota |
|---|---|---|---|---|---|
| 1 | blind | O preset A (backup do ajuste atual) só é exportado na Parte 4, depois de a Parte 3 mexer em tudo | high | ROTEIRO Parte 3 termina com "carregue o preset A da Parte 4", que ainda não existe | patch |
| 2 | blind | As sessões da Parte 3 saem INVALIDA porque a reconexão gera escritas sem anotação | high | §1.4 manda forçar parada e religar o BT; só a C0 tem linha `conexao`; `cross_check` conta as escritas sem anotação como órfãs | patch |
| 3 | blind, edge | A máscara de senha esconde em silêncio bytes reais de parâmetro (01 02 03 04, 12 34, d2 04) | medium | `masked_hex` aplica os padrões de 2 bytes a toda operação, sem avisar | patch |
| 4 | blind, edge | Sem senha não há máscara nem aviso, e `--senha` fica no histórico do PowerShell | medium | O comando de §1.4 não passa senha; `password_patterns(None)` devolve [] em silêncio | patch |
| 5 | blind, edge | Registro truncado (`orig_len > incl_len`) é ignorado em silêncio | medium | `parse_btsnoop` descarta `_orig_len` | patch |
| 6 | blind | O polling pode remover passos de prova se os botões +/− mandarem comandos relativos | maybe-false (medium, não verificado) | Depende do formato real do comando; as capturas C1/C2 resolvem | defer |
| 7 | blind, edge | CSV salvo pelo Excel pt-BR (cp1252, "+15,0" vira fórmula) quebra `read_annotations` | medium | O leitor só aceita utf-8-sig; o roteiro manda editar no Excel | patch |
| 8 | blind, edge | O bugreport traz dados pessoais; o `.gitignore` não cobre o zip extraído nem outros nomes | medium | Padrões só para `bugreport*.zip` e `bruto/` | patch |
| 9 | blind | A medição do limiter não tem controle positivo: "antes do ganho" parece "nunca atuou" | medium | §4.2 não registra o LED do limiter | patch |
| 10 | blind | Status `in-review` com a história incompleta | false | É o ponto de pausa previsto (done_checkpoint); MAPA e APK dependem das capturas | rejeitado |
| 11 | blind | "Preset padrão" (decisão congelada) virou "A em uso" no roteiro; o aparelho tem memória "Factory default" (manual, p. 20) | medium | Duas leituras possíveis da decisão | resolvido pelo Denis: exportar A, Factory default e B |
| 12 | blind | O MAPA afirma que os nomes batem com `damage-thresholds.md`, que não tem identificadores | low | O `damage-thresholds.md` só usa prosa | patch (remover a frase) |
| 13 | blind | O `cross_check` não olha os Error Responses | low | Os erros já aparecem na listagem padrão; a correção adiciona ramo | rejeitado |
| 14 | blind, edge | Não há como medir a diferença entre os relógios do notebook e do celular | medium | Janela de 8 s; o roteiro só diz "use --ajuste-s" | patch |
| 15 | blind, vgap | Faltam testes: CSV com máscara, --ajuste-s, --ignorar-handle, Error/Prepare/0x23, conexao, truncado, modelo de anotação | medium | Os testes atuais não constroem esses opcodes nem essas opções | patch |
| 16 | vgap | O grep da Verification não pega código decompilado | medium | A correção é editar a spec desta build | rejeitado (regra) |
| 17 | edge | Bytes de tamanho zero em respostas de descoberta derrubam a ferramenta | low | Resposta malformada de um aparelho real é improvável; a correção é guarda | rejeitado |
| 18 | edge | Zip corrompido (CRC) escapa sem tratamento | low | Um zip truncado já cai em `is_zipfile` → erro tratado | rejeitado |
| 19 | edge | Senha com espaço em volta não é mascarada | low | Correção direta: `strip()` | patch (junto do 4) |
| 20 | edge | Linha de CSV com campos extras derruba com AttributeError | medium | `DictReader` põe os extras numa lista sob a chave None | patch (junto do 7) |
| 21 | edge | `fim` anterior ao `inicio` no mesmo dia vira janela de ~24 h e aprova captura inválida | medium | `cross_check` soma 1 dia | patch (junto do 7) |
| 22 | edge | Find Information marca declarações (0x2803) como descritor | low | Correção direta do rótulo | patch |
| 23 | edge | `protocolo\capturas\bruto` não é criada antes do primeiro bugreport | medium | A pasta está no `.gitignore` e o roteiro não tem `mkdir` | patch (junto do 8) |
| 24 | edge | Fragmento novo sobre PDU incompleta, --polling 1, --csv com --anotacao, pending após desconexão, Execute Write cancelado, senha em Prepare Writes separados, senha na anotação, timestamp zero | low | Improváveis no uso do roteiro; as correções seriam guardas novas | rejeitados |
