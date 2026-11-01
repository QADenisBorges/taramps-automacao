# Sala e posição: o que o processador não faz

Regras gerais de propagação, sala e posição das caixas. Servem às perguntas (que variáveis o Setup permite), à receita
(o atraso e o corte) e ao diagnóstico (o que é posição da caixa, sem ajuste).

## Regras

- **O som anda cerca de 343 m/s.** A 20 °C, 1 m de diferença de distância é cerca de 2,9 ms. O atraso de uma saída
  compensa a diferença de distância entre as caixas e quem ouve.
  Fonte: Kinsler, L. E., Frey, A. R., Coppens, A. B. e Sanders, J. V., *Fundamentals of Acoustics*, 4ª ed. (Wiley, 2000).
- **Atraso só vale para um ponto.** O atraso certo para um lugar de escuta piora outro; com caixas no mesmo lugar, ou
  com gente espalhada, ele não existe como variável.
  Fonte: Blauert, J., *Spatial Hearing: The Psychophysics of Human Sound Localization*, ed. revisada (MIT Press, 1997); `cerebro-do-agente.md` (spec-agente-memoria, "cada variável só existe se o Setup permitir").
- **No grave da sala, a posição vem antes do EQ.** Abaixo da frequência de transição, os modos da sala fazem picos e
  vales que mudam com a posição. Um vale é um cancelamento: reforço de EQ não o enche, só gasta folga.
  Fonte: Toole, F. E., *Sound Reproduction: The Acoustics and Psychoacoustics of Loudspeakers and Rooms*, 3ª ed. (Routledge, 2018); Welti, T. e Devantier, A., "Low-Frequency Optimization Using Multiple Subwoofers", *Journal of the Audio Engineering Society*, vol. 54, nº 5 (2006).
- **Parede e canto mudam o grave.** Perto de uma superfície, a caixa ganha grave pela reflexão, e a resposta muda com
  a distância até ela.
  Fonte: Allison, R. F., "The Influence of Room Boundaries on Loudspeaker Power Output", *Journal of the Audio Engineering Society*, vol. 22, nº 5 (1974).
- **Ao ar livre não há sala.** Sem reflexões, o nível cai 6 dB a cada vez que a distância dobra, e não há o reforço de
  grave que uma sala ou um carro dão.
  Fonte: Kinsler, L. E., Frey, A. R., Coppens, A. B. e Sanders, J. V., *Fundamentals of Acoustics*, 4ª ed. (Wiley, 2000).
- **Separação, espaço e alcance são posição da caixa.** O processador muda o nível, o tom, o tempo e a rota de cada
  saída; a imagem e o espaço dependem do ângulo das caixas e das reflexões. Um pedido assim recebe a orientação de
  posição, sem ajuste.
  Fonte: Toole, F. E., *Sound Reproduction*, 3ª ed. (Routledge, 2018); `estrategia-mesa.md` (mesa do forge de 29/10/2026), seção 4.
- **A imagem entre duas caixas pede um triângulo.** Com as caixas a cerca de 30° de cada lado de quem ouve, na mesma
  distância, a imagem fica entre elas; fora do eixo, ela puxa para a caixa mais perto.
  Fonte: Recomendação ITU-R BS.775-3 (2012), *Multichannel stereophonic sound system with and without accompanying picture*.
- **No corte entre duas caixas, a fase decide.** Na faixa do corte, as duas tocam juntas: o tempo e a fase entre elas
  decidem se somam ou cancelam ali.
  Fonte: Linkwitz, S. H., "Active Crossover Networks for Noncoincident Drivers", *Journal of the Audio Engineering Society*, vol. 24, nº 1 (1976).
