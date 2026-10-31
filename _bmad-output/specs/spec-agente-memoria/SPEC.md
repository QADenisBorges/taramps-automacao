---
id: SPEC-agente-memoria
companions:
  - cerebro-do-agente.md
  - lado-do-app.md
  - ../../forge/montagem-do-setup/estrategia-mesa.md
  - ../spec-rendimento/SPEC.md
  - ../spec-rendimento/limites-e-niveis.md
  - ../spec-app-controle-taramps/SPEC.md
  - ../spec-app-controle-taramps/porta-de-comandos.md
  - ../spec-app-controle-taramps/chaves-de-acesso.md
  - ../spec-app-controle-taramps/failure-modes.md
  - ../../planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/EXPERIENCE.md
  - ../../planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/DESIGN.md
sources:
  - ../../forge/montagem-do-setup/forged-idea.md
  - ../../forge/montagem-do-setup/proposta-ia-setup.md
  - ../../planning-artifacts/sprint-change-proposal-2026-10-30.md
---

> **Contrato canônico.** Este SPEC e os arquivos em `companions:` formam o contrato completo, já validado quanto à preservação, do que construir, testar e validar. As fontes do frontmatter servem só para rastrear a origem.

# Agente da memória: o cérebro

## Why

Uma visão que nasceu de uma dor. Nas sessões de regulagem, a IA ajustava solta, sem motivo para cada valor, e o Denis refazia o trabalho. O Agente cria uma memória como um técnico de áudio: pergunta só o que muda o resultado, aplica na ordem da física, dentro da cerca, com o motivo de cada valor, e refina pela escuta até o usuário aprovar. O usuário só usa o app; o Agente roda no notebook e fala pela porta. Esta spec é o cérebro; a porta nos dois sentidos e as telas da criação são da CAP-18 do app.

## Capabilities

- **CAP-1**
  - **intent:** Um serviço no notebook escuta os eventos da porta e acorda o Agente quando o usuário pede uma nova memória ou cadastra um modelo sem ficha. Roda um Agente por vez.
  - **success:** Contra a porta do núcleo sobre o simulador, com o modelo falso, um toque em "+ Nova memória" leva o Agente a abrir a criação em até 15 s. Um segundo pedido durante a criação não abre outro Agente. Sem o serviço, o app mostra o texto de `lado-do-app.md` em 15 s.
- **CAP-2**
  - **intent:** O Agente faz as perguntas uma por tela (3 ou 4 respostas e "Outro…"), montadas pelo Setup, pelo ambiente e pelo objetivo, nunca por uma lista fixa de usos. Ele escolhe sempre a variável de maior dúvida × impacto, trata todo padrão como dúvida e para quando nenhuma resposta muda um parâmetro em mais de 1 dB nem uma rota (`cerebro-do-agente.md`).
  - **success:** Na avaliação real, nenhum perfil passa de 8 perguntas, dois perfis com gostos diferentes recebem sequências diferentes, e um carro e uma sala recebem primeiras perguntas com respostas diferentes.
- **CAP-3**
  - **intent:** A receita transforma as variáveis em ajustes na ordem da física (cerca, rota, ganho, corte, atraso e fase; depois tom; por último dinâmica), cada um com frase, valor de antes, valor novo e motivo, dentro da cerca do Rendimento. A rota (entradas separadas ou somadas em cada saída) é decisão com motivo, nunca padrão.
  - **success:** Na avaliação real, nenhum ajuste sai sem motivo, nenhum passa da cerca e nenhum EQ vem antes do tempo e da fase. O app recusa o que passar da cerca, e a recusa conta como violação.
- **CAP-4**
  - **intent:** No refino, o Agente compara uma variável por vez pela escuta "1 ou 2", com o volume igualado pelo app, e cada escolha corta pela metade a dúvida daquela variável.
  - **success:** Com o ouvinte simulado, uma variável cujo gosto escondido está a 4 dB do padrão chega a até 1,5 dB em até 4 escutas. O app recebe as duas versões, troca o som ao vivo e devolve a escolha.
- **CAP-5**
  - **intent:** Diante de uma queixa ("as vozes estão baixas"), o Agente levanta as hipóteses e pergunta o que separa uma da outra. Quando o pedido é algo que o processador não faz (separação, espaço, alcance), ele diz que é posição da caixa.
  - **success:** Na avaliação real, um perfil com a queixa "voz baixa só na ação" termina com mudança de dinâmica, não de EQ. Um pedido de "mais espaço" recebe a orientação de posição, sem ajuste.
- **CAP-6**
  - **intent:** Para um modelo sem ficha, o Agente busca a ficha, confere se ela fecha e manda a proposta com a origem e o endereço. Só o toque do usuário salva.
  - **success:** Com o modelo falso, a proposta chega à folha do app com a origem de cada dado, e nada vai para a base sem o toque. Na avaliação real, uma ficha com o Qts que não fecha com o Qes e o Qms vem marcada.
- **CAP-7**
  - **intent:** O Agente aprende por arquivos: os registros de cada criação e um perfil por situação, que é o ponto de partida e só muda quando a mesma escolha se repete em memórias diferentes. Ele começa vazio.
  - **success:** Depois de duas criações de música com a mesma escolha de grave, o perfil de música muda e o de filme não. Depois de uma só, nada muda.
- **CAP-8**
  - **intent:** O Agente é provado por ouvintes simulados com gosto escondido: um modelo falso no build-auto e uma avaliação real por comando, com custo mostrado antes, que barra mudanças nas skills que não passem.
  - **success:** A avaliação real passa nos 8 perfis, de Setups e ambientes variados (até 1,5 dB, até 8 perguntas, nenhuma violação, a rota certa). Um Agente genérico, com as mesmas perguntas para todos, falha, e um Agente que supõe as entradas separadas falha no perfil da SPL.

## Constraints

- O Agente começa zerado: nada do sistema do Denis (Setup, fichas, memórias do aparelho, regulagens antigas, preferências), só conhecimento geral com fonte; o caso é lido pela porta (decisão do Denis, 30/10/2026).
- Roda fora do app (Claude Code no notebook) e fala só pela porta, com as travas de `porta-de-comandos.md`. Um modelo só no começo (Opus 5.5); sem treino de modelo próprio.
- Tudo no modelo, com skills; só a cerca é código, a do app (decisão do Denis, 30/10/2026).
- A cerca do Rendimento vale sempre; nenhum valor sai sem motivo; a IA não salva; nenhuma tela mostra a palavra "IA".
- Uma pergunta por tela; responder à última pergunta autoriza aplicar (UX de 30/10, que vale sobre a forja).
- Nada é específico de um caso: nem direções (esquerda, direita, entrada A ou B), nem usos fixos (música, filme), nem um tipo de instalação. As variáveis são fenômenos, e todo padrão carrega dúvida (decisão do Denis, 30/10/2026).
- As 3 memórias do processador são intocáveis e não são referência.
- Instruções em camadas, versionadas no git; uma mudança nas skills só entra no `main` se a avaliação real passar.

## Non-goals

- IA dentro do app (custo por uso, chave da API no APK): fica para depois.
- Modelo local no celular.
- Imitar o Akinator ao pé da letra: a resposta são números contínuos.
- As memórias e calibrações de hoje como catálogo ou gabarito.
- Aplicar sozinho ou perguntar no chat do notebook: o usuário usa só o app.
- Uma janela de ajuda para medir dentro da criação: a medição é a calibração guiada do app.
- Escolher outro modelo por tarefa sem medida.

## Success signal

1. A avaliação real passa nos 8 ouvintes simulados e reprova o Agente genérico.
2. No aparelho, o Denis toca em "+ Nova memória", responde às perguntas no celular, ouve, refina com uma escuta "1 ou 2" e salva, sem abrir o notebook e sem ver a palavra "IA".

## Assumptions

- O Agente roda com o Claude Code em modo não interativo, lançado pelo serviço, e fala com a porta só por uma ferramenta de linha de comando com formato fixo (JSON), que também é o ponto de troca do modelo falso.
- O serviço, a ferramenta da porta, os registros e os ouvintes ficam em .NET 10, numa pasta `agente/` na raiz, para os testes de ponta a ponta usarem a Porta do núcleo do app sobre o `ProcessadorSimulado`.
- Na escuta, o app iguala o volume compensando o master pela diferença de nível das duas versões, ponderada sobre um espectro de ruído rosa. A conferência de ouvido fica para o aparelho.
- Os prazos de 15 s (pedido de criação) e 60 s (proposta de ficha) e os textos de `lado-do-app.md` foram definidos aqui.
- O custo de ~US$ 1,50 por memória (forja de 29/10) dá ~US$ 12 por rodada da avaliação real.
