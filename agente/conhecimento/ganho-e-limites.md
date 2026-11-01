# Ganho e limites: tensão, potência e folga

Regras gerais de tensão, potência e soma de sinais. Servem à receita (o ganho e a rota), à medição assistida (o
esperado × o medido) e à regra do pedido, do calculado e do recomendado. Os tetos de cada caso vêm do `GET /limites`.

## Regras

- **Potência vira tensão.** P = V² / Z, então V = √(P × Z). Uma potência pedida pelo usuário vira a tensão que ela
  exige na saída, e é a tensão que a cerca compara.
  Fonte: `spec-rendimento/limites-e-niveis.md` ("Limite físico de uma saída"); Self, D., *Audio Power Amplifier Design*, 6ª ed. (Focal Press, 2013).
- **Decibéis de tensão e de potência.** dB = 20 × log10(V2 / V1) = 10 × log10(P2 / P1): +6 dB é o dobro da tensão
  (o quádruplo da potência), e +3 dB é o dobro da potência.
  Fonte: Davis, D. e Patronis, E., *Sound System Engineering*, 3ª ed. (Focal Press, 2006).
- **Três limites, dois tempos.** O ceifamento do canal e a excursão (Xmax) valem no instante, para o pico. O térmico
  vale para a média (RMS) ao longo de segundos a minutos: a bobina e o ímã esquentam com tempos diferentes.
  Fonte: `spec-rendimento/limites-e-niveis.md`; Button, D. J., "Heat Dissipation and Power Compression in Loudspeakers", *Journal of the Audio Engineering Society*, vol. 40, nº 1/2 (1992); Klippel, W., "Tutorial: Loudspeaker Nonlinearities — Causes, Parameters, Symptoms", *Journal of the Audio Engineering Society*, vol. 54, nº 10 (2006).
- **A potência RMS da ficha é média, não pico.** A música tem picos bem acima da média; o tom contínuo é o pior caso
  do térmico. Por isso o RMS é um limite de tempo, não de instante.
  Fonte: `spec-rendimento/limites-e-niveis.md` (nível Máximo); IEC 60268-5:2003, *Sound system equipment — Part 5: Loudspeakers* (potência com sinal de programa simulado).
- **Somar entradas soma nível e cancela o que está invertido.** Duas entradas iguais e em fase somam +6 dB; sem
  relação entre elas, +3 dB; em fase oposta, cancelam. Somadas, cada saída leva a música inteira (quem está fora do
  eixo ouve tudo, e a carga se divide), ao custo da imagem, da folga da soma e do que foi gravado com fase invertida.
  Fonte: Davis, D. e Patronis, E., *Sound System Engineering*, 3ª ed. (Focal Press, 2006); `cerebro-do-agente.md` (spec-agente-memoria, "Nenhuma direção fixa").
- **Reforço de EQ gasta folga.** +6 dB numa banda pede o dobro da tensão nela e chega antes ao teto; cortar o que
  sobra custa menos folga que reforçar o que falta.
  Fonte: Davis, D. e Patronis, E., *Sound System Engineering*, 3ª ed. (Focal Press, 2006).
- **A ordem da física.** Cerca, rota, ganho, corte, atraso e fase; depois o tom; por último a dinâmica. Um EQ feito
  antes de o tempo e a fase estarem certos corrige um erro que ainda vai mudar.
  Fonte: `estrategia-mesa.md` (mesa do forge de 29/10/2026), seção 4; `cerebro-do-agente.md` (spec-agente-memoria, "Fluxo de uma criação").
- **Sem a tensão medida, não há número.** O teto em dB do limitador só existe com o ganho do módulo medido; sem ele,
  a saída fica "sem calibração", e o app recusa ganho, limitador e EQ dela. Com qualquer saída do Setup sem
  calibração, ele recusa também o master, o EQ de entrada, as 15 bandas, o preset de EQ e a rota.
  Fonte: `spec-rendimento/limites-e-niveis.md` ("Sem calibração").
