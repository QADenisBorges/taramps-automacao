---
id: SPEC-taramps-automacao
companions:
  - permission-model.md
  - damage-thresholds.md
  - protocol-discovery.md
  - screen-route.md
  - stack.md
  - setup-home-theater-2.1.md
  - ../../../protocolo/capturas/demo-telas.md
sources:
  - ../../forge/taramps-automacao/forged-idea.md
  - ../../forge/taramps-automacao/forge-report.html
  - ../../party-mode/2026-10-15-limiares-travas.md
---

> **Contrato canônico.** Este SPEC e os arquivos em `companions:` são o contrato completo, validado por preservação, do que construir, testar e validar. As fontes no frontmatter servem só para rastreio; consulte-as apenas se precisar da justificativa narrativa que este contrato omite de propósito.

# Ponte IA ↔ Taramps PRO 2.4BT com software guardião

## Why

**Dor + visão.** A cada regulagem, o Denis tira prints de todas as telas e canais do app Taramps para a IA conferir. Ele não domina cada função do módulo e depende da IA para não danificar falantes nem o processador, mas a IA erra ao ler imagem e a segurança exige o valor exato. A IA passa a ler e configurar o Taramps PRO 2.4BT + AUDIO por duas vias: operando o app oficial no celular, com o processo visível na tela, ou direto pelo protocolo BLE, para quem não tem ou não quer usar o celular. Nas duas, um software guardião — e não o julgamento da IA — impede dano físico. Se der certo, a integração sustenta a fase 2 (microfone e equalização automática) e um pitch para a Taramps.

## Capabilities

- **CAP-1** — Mapa do protocolo
  - **intent:** O projeto obtém, a partir de capturas, a codificação de cada parâmetro e dos comandos de leitura e escrita do PRO 2.4BT.
  - **success:** Cada parâmetro do Marco 1 tem a codificação documentada, com uma captura de valor antigo/novo; ou o critério de parada dispara dentro do orçamento (ver `protocol-discovery.md`).
- **CAP-2** — Leitura completa (Marco 1)
  - **intent:** A IA obtém, como ferramenta e sem prints, todos os parâmetros do processador: entradas, 4 saídas, EQs, crossover, delay, fase, limiter e ganho.
  - **success:** O notebook conecta com a senha BT e os valores lidos batem 100% com o app em 2 ou mais presets.
- **CAP-3** — Escrita confirmada (Marco 2)
  - **intent:** A IA altera parâmetros do processador e sabe se a mudança foi aplicada.
  - **success:** Toda escrita é seguida de releitura, e o resultado informa se o valor relido é o pedido.
- **CAP-4** — Guardião de dano físico (Marco 2)
  - **intent:** O software impede que a IA aplique as 3 ações de dano físico: tweeter/médio sem passa-alta, sub sem subsônico, nível acima do ceifamento. Ele avalia o estado resultante das 4 saídas com os limiares de `damage-thresholds.md`.
  - **success:** Em qualquer das duas vias, cada uma das 3 tentativas feitas pela IA é bloqueada antes de chegar ao módulo e gera alerta para o Denis. Isso inclui tentativas indiretas por EQ abaixo do corte, link de canais, roteamento A+B, preset, restauração ou estado intermediário de uma sequência de toques.
- **CAP-5** — Liberação humana com registro
  - **intent:** Só o Denis libera uma ação bloqueada, por meio de um alerta explícito de risco.
  - **success:** A liberação não pode partir do canal da IA, e cada liberação fica registrada com ação, valores e momento.
- **CAP-6** — Regras configuráveis
  - **intent:** O comportamento de cada categoria de ação segue regras permitir/perguntar/negar configuráveis, no estilo do Claude Code (ver `permission-model.md`).
  - **success:** Mudar a regra de uma categoria configurável muda o comportamento; as negações fixas para a IA não podem ser relaxadas pela IA.
- **CAP-7** — Áreas protegidas
  - **intent:** A IA nunca altera o mapa de canais, os limites nem a senha BT.
  - **success:** Toda tentativa da IA de alterar esses itens é rejeitada, qualquer que seja a configuração de regras.
- **CAP-8** — Backup antes de escrever
  - **intent:** O estado completo do processador é salvo antes de qualquer escrita.
  - **success:** Para toda escrita existe um backup do estado anterior completo, gravado antes do envio ao módulo.
- **CAP-9** — Salvamento automático opcional
  - **intent:** Por padrão a IA edita só o estado atual; com o salvamento automático ligado pelo usuário, ela grava os ajustes em presets, distinguindo os que não criou.
  - **success:** Desligado, a IA nunca grava em preset salvo; ligado, sobrescrever um preset que a IA não criou exige confirmação do Denis.
- **CAP-10** — Restauração de backup
  - **intent:** O processador pode voltar a um backup anterior.
  - **success:** A IA pode propor a restauração, que só acontece após confirmação do Denis e é conferida por releitura.
- **CAP-11** — Configuração do setup
  - **intent:** Ao configurar, o software conduz o Denis por etapas, como um assistente de instalação, e monta o inventário — falante em cada saída e suas especificações, conexões, terminais, bitola dos cabos, módulos amplificadores, tipo de caixa — que alimenta o mapa de canais e os limites.
  - **success:** Cada uma das 4 saídas tem registro do que está ligado nela; o inventário só passa a valer depois que as etapas obrigatórias forem concluídas e o Denis confirmar, e o guardião aplica as travas com base nele. O inventário traz os campos listados em `damage-thresholds.md`, e um campo ausente faz o guardião usar o padrão conservador.
- **CAP-12** — Fail-safe de escrita
  - **intent:** Queda de conexão ou releitura divergente nunca deixa a IA escrevendo sobre um estado desconhecido.
  - **success:** Simulando uma queda no meio de uma escrita, nenhuma nova escrita é aceita até uma releitura completa; depois a IA apresenta diagnóstico e proposta de correção ao Denis.
- **CAP-13** — Calibração guiada
  - **intent:** Dentro da configuração do setup, o software conduz a etapa de medição: resistência de cada falante, tensão da fonte e tensão AC por saída com tom de referência. Com isso ele fixa o teto de nível de cada saída (ver `damage-thresholds.md`).
  - **success:**
    - Sem calibração concluída, nenhuma saída tem teto calibrado, e o guardião nega qualquer subida de nível, limiar ou EQ.
    - Com calibração concluída, cada saída tem as leituras registradas, um teto (calculado pelas fichas com −3 dB, ou ponto validado pelo Denis) e o limitador no teto.
    - Só o Denis calibra.
- **CAP-14** — Via pela tela
  - **intent:** Quem tem o app no celular Android escolhe que a IA leia e altere o processador operando o app oficial, com o processo visível no celular; quem não tem ou não quer usar o celular usa a via BLE (ver `screen-route.md`).
  - **success:**
    - Em 2 ou mais presets, a leitura pela tela devolve os valores que o app mostra.
    - Toda escrita pela tela passa pelo guardião e é confirmada por releitura da tela.
    - Valor que o OCR não lê com segurança volta como desconhecido, nunca estimado.
    - Tela inesperada ou versão do app não validada interrompe os toques e bloqueia escritas até uma releitura completa.

## Constraints

- O guardião vive no software (servidor), não no cliente de IA: permissões por ferramenta do cliente não validam valores de parâmetro.
- O guardião e as capacidades de leitura, escrita confirmada, backup, fail-safe e liberação humana valem igualmente para as duas vias.
- Na via pela tela, a IA nunca recebe adb, coordenadas nem toques livres: chama as mesmas ferramentas de parâmetro e valor do servidor, que converte em toques depois do guardião.
- A IA é entrada não confiável: nenhuma decisão de segurança depende do julgamento dela.
- Dano físico e áreas protegidas: a IA nunca libera, sem exceção; o humano mantém a palavra final com aceite consciente de risco.
- Toda confirmação ou liberação humana ("perguntar", alerta de risco, restauração, reset) acontece num canal do software que a IA não consegue responder, nunca no chat da IA.
- A IA recebe valores exatos de parâmetro, nunca imagens, e nenhuma leitura de valor depende da visão da IA.
- Alvo fixo: Taramps PRO 2.4BT + AUDIO; não trocar de processador.
- Cada sessão usa uma via só. A via BLE tem o notebook do home theater como central e não depende do celular; na via pela tela, o celular é a central e o notebook não conecta ao módulo.
- Durante desenvolvimento e testes, o processador fica desligado do amplificador.
- Parada: cerca de 2 dias (um fim de semana) de tokens para decodificar. Sem bytes legíveis até lá → procurar a Taramps.
- Protocolo protegido, criptografado ou ofuscado → procurar a Taramps antes de forçar a quebra.
- Captura disciplinada antes da análise: um parâmetro alterado por vez, com valor antigo/novo anotado; a IA só analisa capturas prontas.

## Non-goals

- IA com acesso direto ao adb ou a toques livres no celular, fora do guardião.
- Trocar por um processador de protocolo aberto.
- Leitura de valores pela visão da IA.
- Fase 2: microfone, equalização automática e verificação do mapa de canais por medição.
- As duas vias na mesma sessão, incluindo proxy BLE.
- Quebrar criptografia à força, inclusive a dos presets `.trps`.

## Success signal

- O Denis regula um preset conversando com o Claude sem tirar nenhum print, por qualquer das duas vias: o Claude lê todos os parâmetros, aplica ajustes confirmados por releitura, e as 3 ações de dano físico são bloqueadas até o Denis liberar.
- Primeiro sinal, antes de testar com o processador: no Modo Demo, o Claude lê e ajusta pela tela, e o guardião bloqueia as 3 ações de dano físico.
- Se a via BLE falhar na prova por captura, isso se resolve dentro do orçamento de um fim de semana, com a decisão registrada de procurar a Taramps.

## Assumptions

- O escopo é um único processador, embora o app suporte até 4.
- A engenharia reversa serve só à interoperabilidade de uso próprio; nada do app da Taramps é redistribuído.
- O inventário do setup (CAP-11) só vale depois que o Denis o confirma e, a partir daí, vira área protegida: a IA não o altera. As travas dependem de ele estar certo.
- Na fase 1, sem microfone, as etapas do setup coletam dados por perguntas ao Denis, fichas técnicas e leituras de multímetro que ele informa; o software não mede tensão sozinho (o voltímetro do PRO + AUDIO mede só a alimentação). A verificação do mapa de canais por medição fica para a fase 2.
- O software registra a autoria dos presets; presets anteriores ao software contam como não criados pela IA, e os criados pela IA podem ser sobrescritos sem perguntar.
- A restauração passa pelo guardião como qualquer escrita, porque um backup pode conter um estado perigoso anterior ao software.
- A calibração vale até alguém mexer no hardware, por exemplo no botão de ganho do módulo. O software não detecta essa mudança.
- Os fatores e padrões dos limiares vêm de prática de instalação, não de medição. O cálculo de excursão por Thiele-Small fica para a fase 2.
- A via pela tela é só Android (adb); iOS não é coberto.
- A via pela tela é validada por versão do app (hoje 1.0.8); uma versão nova exige revalidar o mapa de telas antes de qualquer escrita.
- Na via pela tela, o backup é a leitura completa das telas antes da primeira escrita, mais um preset salvo no celular como cópia restaurável pelo app.

## Open Questions

- O limiter do PRO 2.4BT atua antes ou depois do ganho da saída? Qual é a unidade do threshold no protocolo? É preciso confirmar na CAP-1, medindo.
- Na via pela tela, o app aplica ao módulo cada tecla digitada num campo, ou só ao confirmar? Isso define se digitar um valor cria estados intermediários perigosos (por exemplo, 8 Hz antes de 80 Hz).
