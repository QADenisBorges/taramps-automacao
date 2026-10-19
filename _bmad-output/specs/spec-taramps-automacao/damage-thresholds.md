# Limiares das travas de dano físico

As regras do guardião para as 3 travas de dano físico (CAP-4) e para a calibração guiada (CAP-13). São regras genéricas: os valores vêm do inventário do setup (CAP-11). Quando falta dado, o guardião usa um padrão fixo e conservador.

> **Status (decisão do Denis, 18/10/2026):** os limites do guardião (`estado/guardiao.json` e as
> regras 6 e 7 em `ferramentas/guardiao.py`) **ainda estão em definição**. Eles não são regra
> nem referência para analisar ou propor ajustes. A análise parte da física, isto é, das fichas,
> das medidas e das contas (ver `analise-nivel-2026-10-18.md`). O guardião continua barrando
> as escritas até o Denis mudar os valores. Se a análise propuser algo que ele recusa, a saída
> é mostrar o número e a decisão ao Denis, e não contornar o guardião.

## Equipamento (manual do PRO 2.4BT + AUDIO)

- Crossover por saída, de 10 Hz a 22 kHz. Opções: OFF, Butterworth de 6 a 48 dB/oit e Linkwitz-Riley de 12 a 48 dB/oit.
- Ganho por saída de −45 a +15 dB e master de −80 a 0 dB. A saída máxima é 2,1 V RMS.
- EQ paramétrico de ±12 dB na entrada e na saída, mais um EQ gráfico de 15 bandas na entrada.
- Limiter por saída: threshold de −48 a 0 dB (o PRO 2.4BT sem "+ AUDIO" vai de −24 a 0 dB), ataque de 0,1 a 100 ms, liberação de 1 a 1.600 ms ou modo Auto. Pode ser desligado. No app, o threshold aparece em volts na saída do processador.
- Entrada RCA com ganho fixo, até 2,1 V RMS.
- O roteamento aceita A, B e A+B.
- O gerador de senoide vai de 10 Hz a 22 kHz, até 0 dB.
- "Reset Audio Settings" volta o áudio ao padrão de fábrica.

## Princípios

1. O guardião simula a escrita e valida o **estado resultante** das 4 saídas, não só o parâmetro enviado.
2. A saída é classificada pelo falante de frequência mais baixa ligado nela. Coaxial e triaxial são classificados pelo cone.
3. O f_min vem da primeira fonte disponível, nesta ordem:
   1. corte validado no inventário: simulação ou medição, com método e data, registrado pelo Denis;
   2. ficha do fabricante, frequência e inclinação;
   3. fórmula;
   4. padrão sem ficha.
4. Filtros fora do DSP (chave de crossover do módulo, capacitor em série, crossover passivo) não contam como proteção. Eles ficam no inventário só para orientar a calibração.
5. Ações cujo estado resultante o guardião não consegue prever contam como dano físico: reset de áudio, carregar memória interna, gerador de sinal e modo de calibração.
6. Quando a leitura encontra um estado inseguro, o guardião alerta o Denis. Uma escrita não pode criar violação nova nem alterar uma saída em violação sem resolver a violação.
7. Saídas em ponte precisam estar idênticas em todos os parâmetros.

## Travas

O teste das travas 1 e 2 compara a curva somada da saída (crossover mais todos os EQs da entrada e da saída) com uma curva de referência. Abaixo de f_min, a curva somada não pode passar da referência. Esse único teste bloqueia filtro em OFF, inclinação insuficiente, corte abaixo de f_min e reforço de EQ abaixo do corte.

| Trava | Fórmula | Padrão sem ficha | Referência do teste |
|---|---|---|---|
| 1. Passa-alta: tweeter, driver e midrange com menos de 6" | 2 × Fs | 3,5 kHz para tweeter e driver; 5 kHz para super tweeter e piezo; 300 Hz para midrange | Inclinação da ficha, ou Butterworth de 12 dB/oit em f_min |
| 1. Passa-alta: médio-grave de 6" ou mais | 1,5 × F_ref (F_ref = Fs em porta, baffle ou montagem desconhecida; Fc em caixa selada; Fb em caixa dutada) | 80 Hz | Inclinação da ficha, ou Butterworth de 12 dB/oit em f_min |
| 2. Subsônico do sub | 0,8 × Fb em caixa dutada; máx(20 Hz; 0,5 × Fc) em caixa selada; Fs em baffle infinito | 35 Hz para caixa desconhecida; 25 Hz para caixa selada sem Fc | Butterworth de 24 dB/oit em f_min |

**Trava 3: ceifamento.** O guardião calcula o ganho efetivo de cada saída e o compara com dois tetos.

- **Ganho efetivo (G_ef):** master + ganho da saída + pico da curva somada + 6 dB se a rota for A+B. A fonte é considerada em 0 dBFS.
- **Teto do DSP, sempre aplicado:** G_ef ≤ −1 dB.
- **Teto do módulo e do falante, com calibração (CAP-13):** a tensão na saída do módulo não pode passar do teto da saída. O teto vem de uma de duas formas:
  - **calculado pelas fichas:** √(P_alvo × Z) − 3 dB, com P_alvo = mín(RMS do falante; potência do canal do módulo naquela carga ou em ponte);
  - **ponto validado:** registrado pelo Denis com justificativa, por exemplo um limite de excursão. Vale como está, sem margem extra.
- **Teto sem calibração:** a IA não sobe nível, threshold do limiter nem EQ acima da linha de base confirmada. Ela só pode baixar.
- **Limiter:** fica ligado com o threshold no teto ou abaixo dele, como segunda camada. A IA não pode desligá-lo nem subir o threshold.
- **Margem menor que a da regra:** só por liberação do Denis, por saída, registrada.

## Calibração guiada (CAP-13)

A calibração é uma etapa da configuração do setup. Só o Denis a executa, e só depois de confirmar que os falantes estão desconectados.

1. Registra a resistência de cada falante e a tensão da fonte.
2. Mede, por saída, a tensão AC na saída do módulo com um tom dentro da faixa de passagem do DSP e dos filtros externos.
3. Faz a leitura de um destes dois jeitos:
   - com a fonte a 100% e o tom a 0 dBFS;
   - num nível baixo, e o software extrapola de forma linear (cada +6 dB dobra a tensão).
4. Coloca o limiter de cada saída no teto.

O registro guarda data e valores. A calibração vale até alguém mexer no hardware, por exemplo no botão de ganho do módulo.

## Justificativas

- **2 × Fs** é a prática consagrada de crossover para tweeter. O médio-grave queima por excursão, e o fator 1,5 absorve a tolerância de ±15% dos parâmetros Thiele-Small.
- **0,8 × Fb:** abaixo da Fb, o cone de uma caixa dutada fica sem carga. O fator 0,8 é o topo da faixa usual de 0,7 a 0,8.
- **35 Hz para caixa desconhecida:** caixas de porta-malas costumam ser sintonizadas entre 40 e 45 Hz.
- **G_ef ≤ −1 dB:** um DSP ceifando entrega onda quadrada, com +3 dB de potência e harmônicos que vão para o tweeter.
- **Margem de −3 dB no teto calculado:** cobre potência de módulo declarada a 14,4 V, fonte de 12 a 13,8 V, erro de multímetro e ficha otimista. Um ponto validado já embute a margem que o Denis escolheu.
- **Teto por medição:** o ganho do módulo é um botão físico que o software não lê. Por isso o teto vem de medição, e sem medição vale a linha de base.
- **Sem crédito automático de caixa selada:** a caixa reduz o som abaixo da Fc, mas não reduz a excursão na mesma proporção. A inclinação acústica só conta se entrar como corte validado.

## Campos do inventário (CAP-11)

Cada saída registra:

- tipo e tamanho do falante;
- Fs, e Fc ou Fb;
- montagem (porta, baffle, caixa selada ou dutada);
- corte e inclinação da ficha;
- RMS e impedância;
- potência do canal do módulo por carga;
- se está em ponte, e com qual saída;
- limite de excursão validado e corte validado, com método e data;
- filtros externos;
- resistência medida e calibração, com data.
