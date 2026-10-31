---
id: SPEC-rendimento
companions:
  - limites-e-niveis.md
  - telas-rendimento.md
  - ../spec-app-controle-taramps/SPEC.md
  - ../spec-app-controle-taramps/porta-de-comandos.md
  - ../spec-app-controle-taramps/chaves-de-acesso.md
  - ../spec-app-controle-taramps/failure-modes.md
  - ../../regulagem/memoria-spl.md
  - ../../planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/DESIGN.md
  - ../../planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/EXPERIENCE.md
sources:
  - ../../forge/teste-de-playlist/forged-idea.md
---

> **Contrato canônico.** Este SPEC e os arquivos em `companions:` formam o contrato completo, já validado quanto à preservação, do que construir, testar e validar. As fontes do frontmatter servem só para rastrear a origem.

# Rendimento: limite físico, cerca e níveis Baixo a Extra

## Why

Uma visão que nasceu de uma dor. Nas sessões de regulagem, valores como os 11 V da JBL foram postos sem motivo, e os limites do guardião antigo nunca vieram da física. O Denis quer tirar o máximo de cada Setup (a sala hoje, o carro depois) sem queimar nada e sem refazer conta: o app calcula o limite de cada saída pelas fichas, segura o limitador nesse limite e oferece níveis de rendimento com a conta à vista. Esta spec também entrega a cerca física em código que o Agente da memória vai usar (proposta de 29/10/2026). Ela estende o app de `../spec-app-controle-taramps/SPEC.md`.

## Capabilities

- **CAP-1**
  - **intent:** O usuário vê o limite físico de cada saída do Setup em uso, em volts e em dB do limitador: térmico, excursão (com o corte da memória) e ceifamento do módulo, com a conta, o que manda e o selo de origem ou "estimado" (`limites-e-niveis.md`).
  - **success:** Com as fichas do exemplo de `limites-e-niveis.md` e o ganho ×19,5, a JBL mostra térmico 17,9 V, excursão 22,2 V com LR24 em 200 Hz, ceifamento ≈23 V e limite −6,8 dB, dentro de 0,2 dB da conta da `memoria-spl.md` §12. Um alto-falante sem Fb numa caixa dutada mostra o térmico/ceifamento −3 dB, com o selo "estimado" e o aviso do dado que falta.
- **CAP-2**
  - **intent:** A cerca recusa, pela tela e pela porta, toda escrita que deixe o limitador de uma saída desligado ou acima do teto do nível em uso, e mostra a conta. A porta lê os limites e o nível.
  - **success:** Contra o simulador, um limitador pedido 0,1 dB acima do teto é recusado sem nenhum envio ao aparelho, pela tela e pela porta, e a releitura fica igual. No Máximo, o mesmo pedido acima do térmico e abaixo da excursão e do ceifamento passa.
- **CAP-3**
  - **intent:** O app guia a calibração de cada saída: o ganho do módulo, pelo gerador e pelo multímetro, com os alto-falantes desligados, e o nível do Bluetooth, por um tom de 0 dBFS no player do app. Os dois ficam no Setup com a origem "medido". Sem calibração, a porta não mexe na saída e o app não oferece níveis.
  - **success:** Digitando 11,0 V para o tom do gerador com o limitador em −11 dB, o app grava o ganho ×19,5. Numa saída sem calibração, ganho, limitador, EQ e MASTER pedidos pela porta são recusados, e pela tela passam com o aviso "sem calibração".
- **CAP-4**
  - **intent:** O usuário escolhe por memória o nível Baixo, Médio, Alto ou Máximo, e o app põe o master de cada combinação memória + preset no ponto em que o pior caso (um tom a 0 dBFS pela cadeia inteira) chega ao teto do nível menos a margem. Ele refaz a conta sozinho quando a cadeia, as 15 bandas, a ficha ou o ganho mudam. O Máximo passa do térmico e pede confirmação na folha de risco.
  - **success:** Com uma memória plana e o Bluetooth a −13 dB, o pior caso da JBL fica em 17,9 V −6/−3/−1 dB (Baixo/Médio/Alto) e em min(22,2; 23) −1 dB no Máximo, dentro de 0,2 dB. Um reforço de +3 dB no EQ de saída baixa o master em 3 dB na hora. Trocar o preset leva o master ao valor da nova combinação.
- **CAP-5**
  - **intent:** No Extra, o app analisa a playlist da memória simulando a cadeia por saída, acha os trechos mais fortes e a potência média, e põe o master na menor folga entre músicas e saídas, por preset. Cada faixa mostra quantos dB tira do teto. Uma música nova é analisada em segundo plano e não toca até terminar.
  - **success:** Num conjunto de faixas sintéticas com picos conhecidos, o teto do Extra fica na faixa mais pesada menos 1 dB, e o "quem está pesando" aponta essa faixa com o valor certo. Uma faixa em análise não toca.
- **CAP-6**
  - **intent:** O teste a seco confirma o teto do Extra: com o módulo desligado (252), o player toca os trechos fortes (~10 s, 2 por música) e o app escuta o clip. O teto do Extra só sobe depois dele.
  - **success:** Contra o simulador, o teste a seco desliga o módulo antes do primeiro trecho, só religa com o master no valor validado, e um clip simulado num trecho derruba o teto daquela combinação. Uma queda no meio deixa o módulo desligado.
- **CAP-7**
  - **intent:** O Extra passa do térmico por tempo, guiado por um modelo térmico de duas constantes alimentado pela média da playlist: ele corta na temperatura máxima da ficha e trava o Bluetooth (251 desligado e mute 100) até a bobina estimada voltar à temperatura ambiente do Setup. Sem dados térmicos na ficha, o Extra não aparece.
  - **success:** Com dados térmicos de teste, o tempo de Extra e o de descanso batem com a solução analítica do modelo, dentro de 5 %. Sem os dados, o Extra fica apagado, com o motivo. No descanso, o simulador recebe o 251 desligado e o mute.
- **CAP-8**
  - **intent:** No uso do Extra, o clip vira fiscal (o master desce um passo, a música é marcada, e 2 clips não previstos na mesma música mandam a memória para "revalidar"), outro player tocando faz o app mutar e pausar, e o volume do celular fica travado no valor da conta.
  - **success:** Contra o simulador e dublês do Android, um clip não previsto desce o master um passo e marca a faixa, o segundo põe a memória em "revalidar", e um outro player simulado leva ao mute e à mensagem.

## Constraints

- O app começa zerado: o limite sai só do que o usuário cadastrou no Setup e mediu (decisão do Denis, 30/10/2026). Os números do sistema do Denis servem só de dado de teste.
- Limites pela física (fichas, medidas e contas). O guardião antigo, as configurações atuais e as 3 memórias do processador não são referência; os valores de margem e limite são do Denis.
- Dado desconhecido não bloqueia o uso: entra conservador, com o selo "estimado" (regra dos três casos em `limites-e-niveis.md`). O app nunca inventa um tempo seguro para o Extra.
- A cerca vale para qualquer origem do comando (tela, porta, Agente) e não tem chave para passar.
- As 3 memórias do processador são intocáveis: nenhum 240, 241 ou 23N sai do app.
- O cálculo, a cerca, a análise e o modelo térmico vivem no núcleo e são testados contra o `ProcessadorSimulado`. Decodificar arquivos, tocar, detectar outro player e travar o volume ficam atrás de interfaces do Android.
- Toda escrita segue as regras do app: cópia antes, releitura, chaves de acesso e `failure-modes.md`. O estado seguro numa falha é módulo desligado ou mudo no aparelho.

## Non-goals

- O cérebro do Agente da memória: spec própria, que consome a cerca desta.
- Vigiar a média de fontes de fora do app (YouTube e outros) no Baixo a Máximo: o Máximo só avisa.
- O sopro do duto como limite: é ruído, não dano; fica a cargo do EQ da memória.
- Medição por microfone (POC 2) e medição de tensão em cada saída do módulo (POC 3).
- Outras fontes além do Bluetooth (entrada analógica, TV box).
- Guardião completo além da cerca do limitador.

## Success signal

1. Contra o simulador, com as fichas do exemplo cadastradas e calibradas: os limites e os tetos de Baixo a Máximo batem com a `memoria-spl.md` §12–13 dentro de 0,2 dB, a cerca recusa pela tela e pela porta o que passa deles, e o Extra completo (análise, teste a seco, tempo e descanso) roda com dados de teste.
2. No aparelho, com o Setup cadastrado do zero pelo Denis: a calibração guiada bate com o VC830L, o teste a seco desliga o módulo e acha o clip, e uma memória no Alto toca a playlist sem clip.

## Assumptions

- Pela inferência da `memoria-spl.md` §12.5, o EQ entra antes do limitador, então a cerca vigia só o limitador. A ordem é medida no aparelho.
- O Bluetooth a 100 % entra ~13 dB abaixo do 0 dB interno (medido com o YouTube Music). Com o player do app, o valor é medido na calibração.
- O bit 150 marca o clip de cada saída (e o 111 o da entrada), e o 251 desliga de fato o A2DP; os três são conferidos no aparelho.
- A temperatura ambiente é dado do Setup, digitável, com padrão de 25 °C.
- Dados térmicos novos na ficha do alto-falante: temperatura máxima da bobina, resistências térmicas bobina→ímã e ímã→ar, e constantes de tempo da bobina e do ímã, cada um com origem.
- A cerca é um teto do limitador, não o valor dele: a memória pode usar o limitador mais baixo, como compressor (a forja rejeitou o teto pelo limitador da memória por isso). O teto do nível é posto pelo master.
