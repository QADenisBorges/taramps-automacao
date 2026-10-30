---
title: 'História 12b: telas e aparelho inteiro no aparelho real'
type: 'chore'
created: '2026-10-29'
status: 'in-progress'
baseline_commit: '9971cf7f42dfda4bc8de8089dce0c75f40703a33'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/app-controle/README.md'
  - '{project-root}/app-controle/tests/BgsControle.Simulador/SUPOSICOES.md'
  - '{project-root}/_bmad-output/implementation-artifacts/deferred-work.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** As telas, os comandos de aparelho inteiro e o modelo novo das histórias 8f e 10c só rodaram contra o simulador; nada foi visto no A52 com o PRO 2.4BT de verdade, e 12 suposições do protocolo continuam sem medida (SUPOSICOES.md).

**Approach:** Instalar o APK do `main` no A52, passar por cada tela contra o aparelho, numa lista de conferências curtas (uma por vez, o Denis faz o físico), registrar o resultado de cada uma e corrigir no código, com teste contra o simulador, o que o aparelho mostrar diferente.

## Boundaries & Constraints

**Always:**
- **Antes de instalar:** copiar por `run-as` os arquivos privados do app atual (`setups.json`, `presets.json`, `playlists.json`, `link-canais.json`, `modelos.json`, se existirem) para `estado/backups/app-celular-2026-10-29/`. A instalação nova apaga as memórias e os presets do celular (decisão D2 de 29/10); a cópia é só para consulta.
- **Ordem:** instalar e conectar; dúvida da primeira conexão (o Casa 2.1 sem memória) e Escolher; ajustes pela tela (um ganho de cada saída, o Sub escrevendo no CH3 e no CH4 juntos); link automático do Casa 2.1 (Crossover só leitura); Setups e esquema do Casa 2.1 (pintura, sombra, giro, aproximar, toque abre a folha); folha de equipamento (selo de origem da JBL L, escolher uma origem, sugestão "JBL" do modelo); criar uma memória do som atual e conferir a conferência ao reconectar (sem mudança → entra; ganho mexido sem salvar → reaplica sozinho ("REAPLICANDO"; proposta de mudança de 30/10, aprovada pelo Denis); corte mexido sem salvar → muta e mostra a linha); presets (criar, aplicar, PRESET-APLICA-TUDO); Player com uma música do celular pelo Bluetooth; gerador; voltímetro e clip; remote out; Bluetooth de áudio; nome do produto; chaves; resets e troca de senha só na recusa; Criar Setup com os dois nomes.
- **Gerador** só com o RCA dos módulos desconectado; inclui fechar o app com o tom tocando e conferir na reconexão que o 202 voltou a 0.
- **Voltímetro** contra o VC830L em V⎓ 20 nos bornes da fonte do módulo, com a foto marcada do multímetro.
- **Nome do produto:** escrever o mesmo nome que o aparelho já tem.
- **Remote out:** com o volume do celular baixo.
- Cada conferência fica registrada com data, resultado e evidência (print por `adb exec-out screencap`, trecho do log BLE ou leitura): na seção Implementation Notes desta spec e, para as suposições, na coluna "Resultado no aparelho" do SUPOSICOES.md. As pendências do deferred-work fechadas aqui ganham `status`.
- Divergência do aparelho: corrigir o simulador e/ou o app, com teste que falhava antes; os testes e o build continuam verdes.
- No fim, carregar no processador a memória do aparelho que o Denis escolher (padrão: 3, FILME), para ele não ficar com o som de teste.
- Instruções ao Denis: uma ação por vez, curta, com o que conferir e o que responder.
- **Toques (decisão do Denis, 29/10):** misto. A IA navega pelo adb (`input tap`, `screencap`) e tira os prints; o Denis toca só no que precisa ser real (gestos do esquema, teclado, selo de origem) e faz o físico.
- **Tamanho:** a spec fica inteira (decisão do Denis, 29/10), acima de 1.600 tokens.

**Never:**
- Enviar `240`, `23N`, `*004*`, `*242*` ou `*001*` com a chave ligada (resets e troca de senha só na recusa, chave desligada).
- Gerador com o RCA dos módulos ligado ou acima do ganho combinado na hora.
- Pedir a senha no chat ou deixá-la no log, no print ou na spec.
- A porta da IA no Wi-Fi e os testes 1, 2 e 4 do sinal de sucesso: são da 12c.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Primeira conexão | App novo, Casa 2.1 sem memória | Mutado, "O Casa 2.1 ainda não tem memória."; Escolher desmuta sem escrever áudio | Não mutou: registrar o motivo e corrigir |
| Sub | Mexer o ganho do Sub na Mesa | CH3 e CH4 relidos iguais | Divergência: corrigir e testar |
| Conferência | Memória criada; reconectar sem mudança / com ganho / com corte mexido | Entra / reaplica sozinho ("REAPLICANDO") / muta com a linha do corte | Idem |
| Gerador | Tom tocando, fechar o app | Na reconexão, 202 = 0 relido antes de qualquer outra ação | "DESLIGUE O MÓDULO AGORA" se o 202 continuar 1 |
| Voltímetro | Tela do voltímetro | Valor bate com o VC830L dentro da precisão do aparelho | Sem valor: medir o 005 espontâneo e registrar |
| Recusas | Reset de fábrica, reset de áudio e troca de senha com a chave desligada | Recusados antes de qualquer envio | — |

</frozen-after-approval>

## Code Map

- `app-controle/README.md:95-130` -- compilar (`dotnet build src/BgsControle.App -f net10.0-android -c Debug`), instalar só pelo `C:\platform-tools\adb.exe install -r …\br.com.bgs.controle-Signed.apk`, `run-as br.com.bgs.controle` para os arquivos privados, o logcat da senha.
- `app-controle/tests/BgsControle.Simulador/SUPOSICOES.md:28-61` -- suposições ainda sem resultado: ECO-251-252, CLIP-ESPONTANEO, REGRA-NOMES, LINK-PARAMETROS, GERADOR-MODO, ECO-GERADOR, ACK-002, NOME-PRODUTO, VOLTIMETRO-ESPONTANEO, CLIP-ENTRADA, PRESET-APLICA-TUDO; `Suposicoes.cs` e `// Suposição <ID>` no `ProcessadorSimulado.cs` mudam juntos (`SuposicoesTestes`).
- `_bmad-output/implementation-artifacts/deferred-work.md:27-28` (mexer um ganho pela tela), `:38-40` (gerador ao fechar o app), `:50-53` (áudio real do Player); `stories/10-…md:210` (navegação com tela por cima, Concluído do teclado), `stories/10b-…md:19,193` (pintura e gestos do esquema), `stories/11-…md:21,28,239` (camadas modais, peças do Android, aviso do Bluetooth), `stories/7a-…md:150` (sweep descendente).
- `estado/log-ble/` -- o log BLE do app (puxado por `run-as`) e das sondas; `ferramentas/cliente_ble.py` para sondas de consulta pelo notebook, se preciso (o app da Taramps e o nosso nunca juntos).
- `.claude/skills/regulagem/references/multimetro/vdc-20.jpg` -- a foto do VC830L em V⎓ 20.
- `estado/memorias.json` -- os nomes das 3 memórias do aparelho (1 SPL, 2 MUSICA ORIG, 3 FILME).

## Tasks & Acceptance

**Execution:**
- [ ] `estado/backups/app-celular-2026-10-29/` -- copiar os arquivos privados do app atual -- consulta depois da migração.
- [ ] `app-controle/src/BgsControle.App` -- build Debug do `main` e instalação no A52 -- o app das histórias 8f e 10c.
- [ ] (aparelho) -- passar a lista da seção Always, uma conferência por vez -- a prova no aparelho real.
- [ ] `app-controle/tests/BgsControle.Simulador/SUPOSICOES.md`, `Suposicoes.cs`, `ProcessadorSimulado.cs` -- registrar cada suposição medida e corrigir o simulador onde cair -- o simulador fica fiel.
- [ ] `app-controle/src/**`, `tests/**` -- corrigir o que o aparelho mostrar diferente, com teste -- o app fica certo.
- [ ] `_bmad-output/implementation-artifacts/deferred-work.md` e esta spec -- fechar as pendências conferidas e registrar o resultado -- rastro.

**Acceptance Criteria:**
- Given o A52 com o app novo e o PRO 2.4BT, when cada conferência da lista é feita, then ela tem resultado (passou, caiu e foi corrigida, ou não deu para medir com o motivo) e evidência registrados.
- Given o fim da sessão, when o Denis escolhe a memória do aparelho, then ela é carregada e relida, e as 3 memórias do aparelho têm os mesmos nomes de antes.

## Implementation Notes

- 29/10, no A52 (evidência em `estado/prints-12b/`, log em `estado/log-ble/2026-10-29_1928-app-12b.log`): backup dos arquivos privados, instalação do `9971cf7`, primeira conexão mutada com "O Casa 2.1 ainda não tem memória." e Escolher desmutando sem áudio, ganho do Sub em CH3 e CH4 com eco, ganhos das JBL, Crossover Avançado e desenho do esquema: passaram (`estado/prints-12b/notas.md`).
- 29/10, decisões do Denis durante a 12b: a configuração começa do zero no app (nada copiado das memórias de hoje do processador); as memórias do processador somem do app inteiro (sem "Do aparelho" e sem carregar 241).
- 30/10, sem o celular:
  - Memórias do processador fora do app: a trava recusa o 241 como o 240 e o 23N; saíram a seção "Do aparelho", a chave "Carregar memória do aparelho", o `POST /memoria/carregar`, o estado "Memória N do aparelho", a leitura dos nomes 231–233 e o `GerenteMemorias`. SPEC (CAP-2, CAP-4, teste 3), EXPERIENCE, DESIGN, chaves, failure-modes, conhecimento herdado, README e SUPOSICOES (ACK-241 e CARREGAR-SEM-NOMES fora de uso) atualizados.
  - Início desconectado: sem Setup em uso, as saídas são CH1 a CH4 sem o link guardado da última sessão (`GerenteSetups.SaidasEmUso`, teste novo).
  - Telas: caixa do ganho da Mesa numa linha (18, folga 4); dica da edição "Aplica no OK (✓) do teclado"; arco lilás só no canto. Falta conferir no aparelho.
  - Travamento ao arrastar o esquema: sem causa no código (16.209 casos de ângulo e aproximação sem exceção). Continua aberto; precisa do aparelho ou de um emulador.
  - Transição de ~3 s do Início depois do Escolher: não mexido (precisa do aparelho para ver a causa).
  - Testes: núcleo 1024, telas 454; build Android 0 avisos e 0 erros.
- Pendente de decisão do Denis: o fim da sessão (bloco congelado, "carregar no processador a memória do aparelho que o Denis escolher") não cabe mais no app; e como um som feito no app da Taramps entra num Setup que já tem memória (nota em EXPERIENCE).

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- No A52: a lista da seção Always, com evidência de cada item.
