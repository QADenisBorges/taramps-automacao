---
date: 2026-10-15
topic: Limiares das 3 travas de dano físico do guardião (taramps-automacao)
mesa: Valdir (assistência técnica) · Bia (suporte de DSP) · Otávio (segurança funcional) · Tati (instalação)
decisões do Denis: margem de −3 dB no teto de nível; usar o projeto Som como banco de prova, mantendo os valores genéricos
fontes: manual oficial do PRO 2.4BT (ENG, Taramps, 2025) · projeto C:\Users\denis\Desktop\Som (banco de prova, não molde)
---

# Resultado da mesa: limiares das travas de dano físico

As três travas tiram os valores das especificações de cada falante, registradas no levantamento do setup (CAP-11). Quando falta dado, o software usa um padrão fixo e conservador. Nenhum valor foi ajustado para um setup específico.

## Fatos do equipamento (manual PRO 2.4BT)

- Crossover por saída, de 10 Hz a 22 kHz. Opções: OFF, Butterworth 6 a 48 dB/oit e Linkwitz-Riley 12 a 48 dB/oit.
- Ganho por saída de −45 a +15 dB e master de −80 a 0 dB. A saída máxima é 2,1 V RMS.
- EQ paramétrico de ±12 dB na entrada e na saída (Q de 0,4 a 10), mais um EQ gráfico de 15 bandas (±12 dB) na entrada.
- Limiter por saída: threshold de −24 a 0 dB, ataque de 0,1 a 100 ms e modo Auto. Pode ser desligado. Com o limiter desligado, o LED da saída indica clip.
- O roteamento aceita A, B e A+B.
- O gerador de senoide vai de 10 Hz a 22 kHz, até 0 dB.
- "Reset Audio Settings" volta o áudio ao padrão de fábrica.

## Princípios (valem para as 3 travas)

1. **Estado resultante.** O guardião simula a escrita e valida o estado resultante das 4 saídas, não só o parâmetro enviado. Assim pega link de canais, roteamento A+B, carregamento de preset e restauração.
2. **Classificação da saída.** A saída é classificada pelo falante de frequência mais baixa ligado nela. Coaxial e triaxial são classificados pelo cone. O tweeter embutido fica protegido pelo crossover passivo do próprio falante.
3. **Fonte do f_min, por ordem de precedência:**
   1. corte validado no inventário: simulação de excursão ou medição, com método e data, registrado pelo Denis;
   2. ficha do fabricante, frequência e inclinação;
   3. fórmula;
   4. padrão sem ficha.

   O inventário é área protegida, então a IA não consegue baixar o f_min editando dados.
4. **Filtros externos.** Filtros fora do DSP (chave de crossover do módulo, capacitor em série, crossover passivo) não contam como proteção. Ficam registrados no inventário só para orientar a calibração.
5. **Estado imprevisível.** Toda ação cujo estado resultante o software não consegue prever conta como dano físico e é negada para a IA: reset de áudio, carregar memória interna, gerador de sinal e modo de calibração.
6. **Estado já inseguro.** Se o guardião encontrar um estado inseguro na leitura, ele alerta o Denis. Uma escrita não pode criar violação nova nem alterar uma saída em violação sem resolver a violação.
7. **Ponte.** Saídas declaradas em ponte no inventário precisam estar idênticas em todos os parâmetros. Qualquer diferença bloqueia a escrita.

## Trava 1: passa-alta de tweeter, driver e médio

- **Fórmula** (quando não há corte validado nem ficha):
  - tweeter, driver e midrange com menos de 6": f_min = 2 × Fs;
  - médio-grave de 6" ou mais: f_min = 1,5 × F_ref. F_ref é a Fs em porta, baffle infinito ou montagem desconhecida, a Fc em caixa selada e a Fb em caixa dutada.
- **Padrão sem ficha:**
  - tweeter e driver: 3,5 kHz;
  - super tweeter e piezo: 5 kHz;
  - midrange com menos de 6": 300 Hz;
  - médio-grave: 80 Hz.
- **Teste:** abaixo de f_min, a curva somada (crossover mais todos os EQs da entrada e da saída) não pode passar da curva de referência. A referência é a inclinação da ficha ou, sem ela, um Butterworth de 12 dB/oit em f_min. Um único teste bloqueia HPF em OFF, BT6, corte abaixo de f_min e reforço de EQ abaixo do corte.
- **Justificativa:**
  - O fator 2 × Fs é a prática consagrada de crossover para tweeter.
  - Médio-grave queima por excursão, e abaixo da Fs, Fc ou Fb o cone perde controle.
  - O fator 1,5 absorve a tolerância de ±15% dos parâmetros Thiele-Small.
  - Os padrões cobrem os transdutores nacionais mais comuns sem ficha.
  - O EQ entra no teste porque um reforço abaixo do corte desfaz o filtro.
- **Ficou para a fase 2:** calcular a excursão automaticamente a partir de Xmax e dos parâmetros Thiele-Small. Muitos falantes nacionais não publicam esses dados ou publicam errado.

## Trava 2: subsônico do sub

- **Fórmula:**
  - caixa dutada: 0,8 × Fb;
  - caixa selada: máx(20 Hz; 0,5 × Fc);
  - baffle infinito: Fs.
- **Padrão:** 35 Hz para caixa desconhecida e 25 Hz para caixa selada sem Fc. Caixas de porta-malas costumam ser sintonizadas entre 40 e 45 Hz, e 30 Hz não as protege. Quem descobre a Fb (varredura lenta, observando onde o cone quase para) e a registra pode descer o corte.
- **Teste:** abaixo de f_min, a curva somada não pode passar de um Butterworth de 24 dB/oit em f_min. Inclinação de 12 dB/oit não basta para sub.
- **Justificativa:**
  - Abaixo da Fb, o cone de uma caixa dutada fica sem carga e a excursão dispara. O fator 0,8 é o topo da faixa usual de 0,7 a 0,8.
  - Em caixa selada, a excursão abaixo da Fc fica num patamar, por isso o corte pode ser mais baixo.

## Trava 3: nível acima do ceifamento

- **Ganho efetivo da saída (G_ef):** master + ganho da saída + pico da curva somada (EQs e crossover) + 6 dB se a rota for A+B. A fonte é considerada em 0 dBFS, que é o pior caso.
- **Sempre:** G_ef ≤ −1 dB, para que o DSP não ceife. Um DSP ceifando entrega onda quadrada, com +3 dB de potência e harmônicos que vão para o tweeter.
- **Com calibração:** a tensão na saída do módulo não pode passar de √(P_alvo × Z) − 3 dB.
  - P_alvo = mín(RMS do falante; potência do canal do módulo na carga ou na ponte; limite de excursão validado, se houver).
  - Calibração: o Denis mede com multímetro AC, falante desconectado, tom dentro da faixa de passagem e num nível baixo. O software extrapola de forma linear, já que +6 dB dobra a tensão. A calibração tem data e vale até alguém mexer no hardware (por exemplo, no botão de ganho do módulo).
- **Sem calibração:** G_ef não pode passar da linha de base confirmada pelo Denis. A IA pode baixar o nível ou mantê-lo, nunca subir.
- **Limiter:** como segunda camada, o limiter fica ligado com o threshold no teto ou abaixo dele. Falta confirmar na CAP-1, medindo, se ele atua antes ou depois do ganho da saída. A IA não pode desligá-lo nem subir o threshold.
- **Margem de −3 dB (metade da potência), escolhida pelo Denis.** Ela cobre:
  - potência de módulo medida a 14,4 V;
  - fonte de 12 a 13,8 V;
  - erro de multímetro barato;
  - ficha otimista.

  Uma margem menor só existe por liberação do Denis, por saída, registrada.
- **Justificativa:**
  - O ceifamento é a causa mais comum de tweeter queimado.
  - O ganho do módulo é um botão físico que o software não lê. Por isso o teto vem de medição, e sem medição o único estado comprovado é a linha de base.

## Banco de prova: o projeto Som (2.1)

Do jeito que o projeto Som está configurado hoje, as regras genéricas barram três ajustes. Cada um passa a exigir uma liberação registrada ou um dado validado no inventário:

- **JBL 4TRMS80:** o projeto usa Butterworth de 12 dB/oit em 120 Hz, e a ficha pede 100 Hz a 24 dB/oit. Passa se a simulação de excursão de 09/10/2026 for registrada como corte validado.
- **Sub em caixa com duto e Fb desconhecida:** o projeto usa subsônico de 30 Hz, abaixo do padrão de 35 Hz. Passa depois de descobrir a Fb.
- **Alvos de ganho de 11 V (JBL) e 40 V (sub em ponte):** passam do teto com a margem de −3 dB (o sub cairia para cerca de 28 V). Passam com liberação do Denis por saída.

## Pendências levadas para a spec

- Confirmar na CAP-1 se o limiter atua antes ou depois do ganho da saída e qual é a unidade do threshold no protocolo.
- Campos do inventário (CAP-11): tipo e tamanho do falante, Fs, Fc ou Fb, montagem, ficha do fabricante (corte e inclinação), RMS, impedância, potência do canal do módulo por carga, ponte, limite de excursão validado, filtros externos e calibração com data.
- Calibração e modo de calibração são operações só do Denis, com confirmação de falantes desconectados.
