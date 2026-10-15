---
id: SPEC-taramps-automacao
companions:
  - permission-model.md
  - damage-thresholds.md
  - protocol-discovery.md
  - stack.md
  - setup-home-theater-2.1.md
sources:
  - ../../forge/taramps-automacao/forged-idea.md
  - ../../forge/taramps-automacao/forge-report.html
  - ../../party-mode/2026-10-15-limiares-travas.md
---

> **Contrato canônico.** Este SPEC e os arquivos em `companions:` são o contrato completo, validado por preservação, do que construir, testar e validar. As fontes no frontmatter servem só para rastreio; consulte-as apenas se precisar da justificativa narrativa que este contrato omite de propósito.

# Ponte IA ↔ Taramps PRO 2.4BT com software guardião

## Why

**Dor + visão.** A cada regulagem, o Denis tira prints de todas as telas e canais do app Taramps para a IA conferir. Ele não domina cada função do módulo e depende da IA para não danificar falantes nem o processador, mas a IA erra ao ler imagem e a segurança exige o valor exato. A aposta é decifrar o protocolo BLE do Taramps PRO 2.4BT + AUDIO para que a IA leia e configure o processador direto, com um software guardião — e não o julgamento da IA — impedindo dano físico. Se der certo, a integração sustenta a fase 2 (microfone e equalização automática) e um pitch para a Taramps.

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
  - **success:** Cada uma das 3 tentativas feitas pela IA é bloqueada antes de chegar ao módulo e gera alerta para o Denis. Isso inclui tentativas indiretas por EQ abaixo do corte, link de canais, roteamento A+B, preset ou restauração.
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
  - **success:** Cada uma das 4 saídas tem registro do que está ligado nela; o inventário só passa a valer depois que as etapas obrigatórias forem concluídas e o Denis confirmar, e o guardião aplica as travas com base nele.
- **CAP-12** — Fail-safe de escrita
  - **intent:** Queda de conexão ou releitura divergente nunca deixa a IA escrevendo sobre um estado desconhecido.
  - **success:** Simulando uma queda no meio de uma escrita, nenhuma nova escrita é aceita até uma releitura completa; depois a IA apresenta diagnóstico e proposta de correção ao Denis.
- **CAP-13** — Calibração do teto de nível
  - **intent:** O Denis registra, por saída, uma medição de tensão na saída do módulo, e o software deriva dela o teto de nível. O procedimento está em `damage-thresholds.md`.
  - **success:** Com calibração, nível acima de √(P_alvo × Z) − 3 dB é bloqueado. Sem calibração, a IA não sobe o nível acima da linha de base confirmada. Calibrar e entrar no modo de calibração só o Denis faz.
- **CAP-13** — Calibração guiada
  - **intent:** Dentro da configuração do setup, o software conduz a etapa de medição — resistência de cada falante, tensão da fonte, tensão AC por saída com tom de referência — e fixa o teto de nível de cada saída no ponto calibrado.
  - **success:** Sem calibração concluída, nenhuma saída tem teto calibrado e o guardião nega qualquer subida de nível, limiar ou EQ; concluída, cada saída tem as leituras registradas e o limitador no ponto calibrado.

## Constraints

- O guardião vive no software (servidor), não no cliente de IA: permissões por ferramenta do cliente não validam valores de parâmetro.
- A IA é entrada não confiável: nenhuma decisão de segurança depende do julgamento dela.
- Dano físico e áreas protegidas: a IA nunca libera, sem exceção; o humano mantém a palavra final com aceite consciente de risco.
- A saída são valores exatos de parâmetro, nunca imagens.
- Alvo fixo: Taramps PRO 2.4BT + AUDIO; não trocar de processador.
- A central BLE é o notebook do home theater; o software não pode depender de o app do celular estar conectado ao mesmo tempo.
- Durante desenvolvimento e testes, o processador fica desligado do amplificador.
- Parada: cerca de 2 dias (um fim de semana) de tokens para decodificar. Sem bytes legíveis até lá → procurar a Taramps.
- Protocolo protegido, criptografado ou ofuscado → procurar a Taramps antes de forçar a quebra.
- Captura disciplinada antes da análise: um parâmetro alterado por vez, com valor antigo/novo anotado; a IA só analisa capturas prontas.

## Non-goals

- IA operando o app via ADB/toques na tela (frágil a cada atualização, lento, não definitivo).
- Trocar por um processador de protocolo aberto.
- Prints automáticos ou leitura de valores por visão.
- Fase 2: microfone, equalização automática e verificação do mapa de canais por medição.
- Uso simultâneo do app no celular durante a sessão, incluindo proxy BLE.
- Quebrar criptografia à força.

## Success signal

- O Denis regula um preset conversando com o Claude sem tirar nenhum print: o Claude lê todos os parâmetros, aplica ajustes confirmados por releitura, e as 3 ações de dano físico são bloqueadas até o Denis liberar.
- Ou, se a aposta falhar, ela se resolve dentro do orçamento de um fim de semana com a decisão registrada de procurar a Taramps.

## Assumptions

- O app Android alvo é `com.taramps.pro1` (não confirmado; pode haver outro app para a versão +AUDIO).
- O protocolo é decodificável sem criptografia ou ofuscação pesada — é a aposta central, ainda não testada.
- O escopo é um único processador, embora o app suporte até 4.
- A engenharia reversa serve só à interoperabilidade de uso próprio; nada do app da Taramps é redistribuído.
- O inventário do setup (CAP-11) só vale depois que o Denis o confirma e, a partir daí, vira área protegida: a IA não o altera. As travas dependem de ele estar certo.
- Na fase 1, sem microfone, as etapas do setup coletam dados por perguntas ao Denis, fichas técnicas e leituras de multímetro que ele informa; o software não mede tensão sozinho (o voltímetro do PRO + AUDIO mede só a alimentação). A verificação do mapa de canais por medição fica para a fase 2.
- O software registra a autoria dos presets; presets anteriores ao software contam como não criados pela IA, e os criados pela IA podem ser sobrescritos sem perguntar.
- A restauração passa pelo guardião como qualquer escrita, porque um backup pode conter um estado perigoso anterior ao software.

## Open Questions

- Quem pode liberar o reset de fábrica?
- Na regra "perguntar", quem responde e por qual canal — sempre o Denis, fora do chat da IA?
