# Análise de nível: JBL e sub pelo Bluetooth (18/10/2026)

Esta análise parte da física, isto é, das fichas, das medidas e das contas, e não dos
limites do guardião. Esses limites ainda estão em definição (decisão do Denis, 18/10/2026;
ver `damage-thresholds.md`).

## Medidas e dados usados

| Dado | Valor | Fonte |
|---|---|---|
| Ganho do módulo nas JBL | ×19,6 (+25,8 dB) | 0,564 V → 11,0 V, 17/10 |
| 0 dB interno do processador | ≈ 2,0 V (máx. 2,1 V RMS) | −11 dB = 0,564 V; manual |
| Ganho do módulo no sub (ponte) | ≈ ×23,4 | 23,4 V a 50 Hz com o limiter em −6 dB (1,0 V), 17/10 |
| Tom de 400 Hz a 0 dBFS pelo BT (YouTube Music, celular 15/15), JBL em −5 dB | 4,8 V (CH1) / 4,7 V (CH2) | 18/10 |
| → nível do BT dentro do processador | **≈ −13 dB** abaixo do gerador interno (0 dB) | conta: 4,8 / 19,6 = 0,245 V = −18,2 dB, com ganho −5 |
| JBL 4TRMS80 | 80 W RMS, Re 3,4 Ω, 3,6 Ω medido, Xmax 1,75 mm, 87 dB | ficha (foto) |
| Excursão da JBL com LR24 em 150 Hz (tom contínuo, pior caso) | 0,86× Xmax a 11 V; 0,98× a 12,6 V; 1,21× a 15,5 V | auditoria 2026-10-17_guardiao |
| SD800.4 EVO | ceifa por volta de 23–25 V por canal em 4 Ω; ponte 400 W | ficha |
| Protech 8" (sub) | 450 W RMS, 3,7 Ω medido | loja |

## Conclusões

1. **Hoje o som é baixo porque o BT entra uns 13 dB abaixo do gerador.** Com as JBL em −5 dB,
   um tom a 0 dBFS dá 4,8 V, e música normal fica em torno de 1 a 2 V (menos de 1 W). Os
   limitadores (−11 dB nas JBL, −6 dB no sub) quase não chegam a agir.
2. **Quem protege de verdade é o limitador**, com o corte LR24 em 150 Hz. O ganho só decide
   com que frequência o limitador age. Com o limitador em −11 dB, as JBL não passam de
   ~11 V (≈ 33 W em 3,6 Ω; 0,86× Xmax no pior caso), seja qual for o ganho.
3. **Ganho para o BT chegar ao limitador:** JBL em **≈ +2 dB** (4,8 V × 10^(7/20) ≈ 10,7 V).
   Para manter o equilíbrio de hoje, o sub sobe os mesmos 7 dB: de −10 para **≈ −3 dB**.
   A conta prevê ≈ 14,5 V na ponte com um tom de 50 Hz pelo BT (≈ 57 W), abaixo do teto do
   limitador do sub (≈ 23,4 V) e dos 400 W da ponte.
4. **O risco de subir assim é a fonte mais forte, não o BT.** Com o ganho acima de 0 dB, uma
   fonte a 0 dBFS no processador (gerador do app, P2 do notebook no máximo, outro app sem
   normalização) passa do 0 dB interno. O DSP pode ceifar por dentro antes do limitador, e
   isso não está medido (auditoria, item 12). Por isso, um ganho assim vale para uma memória
   **só de BT pelo celular** (MUSICA HIGH), com a ORIGINAL e a FILME mais baixas.
5. **O nível do BT depende do app.** O YouTube Music pode normalizar o volume. Medir de novo
   com o app de uso real (Spotify etc.) antes de fechar os números.

## Próximas medidas (se o Denis decidir subir)

- JBL em +2 dB: tom de 400 Hz a 0 dBFS pelo BT, no CH1 e no CH2. Esperado ≈ 10,7 V; o
  limitador deve segurar em ~11 V.
- Sub em −3 dB: tom de 50 Hz a 0 dBFS pelo BT, na ponte (CH4+ / CH3−). Esperado ≈ 14,5 V.
- Dissipador do módulo depois de 15 min no volume máximo.

## Medidas da estrutura de ganho (18/10/2026, à tarde)

Feitas com o gerador do app e os falantes soltos. JBL em 0 dB com +2 dB em 1 kHz (EQ de saída);
sub em −2 dB com +3 dB em 50 Hz; regra 7 em +8 dB.

| Teste | Resultado | Leitura |
|---|---|---|
| JBL, 400 Hz a −13 dB (simula o BT a 100 %) | 9,3 V no CH1 e no CH2 | confirma que o BT entra ~13 dB abaixo do gerador |
| JBL, 1 kHz a 0 dB (fonte forte) | 11,7 V no CH1 e no CH2 | o limitador em −11 dB segura (≤ 12,6 V) |
| Sub, 50 Hz a −13 dB (BT) | 18,5 V na ponte | ≈ 90 W |
| Sub, 50 Hz a 0 dB, limitador em −6 dB | **40,6 V** | **o limitador em −6 dB deixa passar ~40 V (~440 W)** |
| Sub, 50 Hz a 0 dB, limitador em −12 dB | 20 V | o limitador funciona |
| Sub, 50 Hz a 0 dB, limitador em −9 dB | **28,6 V** | teto novo do sub (≈ 220 W em 3,7 Ω) |

**Correção:** o ganho do módulo no sub em ponte é **~×40** (o dobro dos ×19,6 das JBL), e não os
×23,4 anotados em 17/10. Esses 23,4 V foram medidos sem o limitador agir. Com o limitador em
−6 dB, o teto real do sub era ~40 V, perto de onde o módulo ceifa, desde 17/10 e em todas as
memórias. O limitador do sub passa para **−9 dB** (28,6 V medidos). O BT no máximo (18,5 V)
fica abaixo desse teto.

## Configuração definitiva de filme (18/10/2026, 21h45) — retrato `estado/leituras/2026-10-18_2145.json`

**Geometria medida pelo Denis:** JBL L 3,20 m, sub 2,40 m, JBL R 2,20 m até os ouvidos; 1,60 m entre
as JBL. Pela lei dos cossenos, o ouvinte está **0,9 m à direita da JBL R** e 2,0 m à frente da linha
das caixas: 51° fora do eixo da L e 24° da R. Não há triângulo estéreo. As caixas e o sub ficam no
chão, encostados na parede sob o lençol de projeção, apontando para a parede oposta, não para o
ouvinte. Sala com piso frio, paredes nuas e um colchão como único absorvedor.

**O que foi feito (prática de áudio automotivo para ouvinte fora do centro):**

| Parâmetro | L (saída 1) | R (saída 2) | Sub (3/4) | Por quê |
|---|---|---|---|---|
| Atraso (`050`, cm) | 0 | **100** | **80** | iguala a chegada: atrasa quem está mais perto, pela diferença de distância até o ouvinte |
| Ganho (`081`) | +10 | **+7** | +6 | 20·log(3,20/2,20) = 3,3 dB: a R chegaria mais forte |
| Limitador (`071`) | −15 | **−18** | −12 | 3 dB abaixo na R para limitar no mesmo nível de programa; −15/−12 funcionam como compressor suave (voz × ação) |
| Ataque / liberação | 50 ms / 200 ms | idem | 50 / 200 | transientes passam; a cauda do estrondo volta logo |
| EQ de saída | 2 kHz +2,5 dB Q 0,7 | idem | 45 Hz +3 dB Q 0,7 | presença da voz; sustentação do grave |
| Corte | LR24 150 Hz | idem | BT24 40 Hz + LR24 150 Hz | inalterado (Fb do duto desconhecida) |
| Fase | 0° | 0° | 180° | teste de ouvido com ruído 80–250 Hz |

EQ de 15 bandas (global): 160 Hz +1; 400 Hz −1; 4 kHz −1; 6,3 kHz −2; 10 kHz +2; 16 kHz +2; o resto 0.
Rotas 1 = B, 2 = A (o L do BT chega como entrada B). Salvo na **Memória 3 (FILME)**.

**Teste de ouvido do atraso do sub (antes da medida):** 0 cm nota 5, 30 cm "grave veio para a frente",
60 cm melhor, 90 cm igual. Compatível com os 80 cm calculados.

**Limites físicos que a configuração não resolve** (ordem de impacto): posição do ouvinte fora do par
estéreo; caixas no chão apontadas para a parede, sem toe-in; tweeter da JBL L mais fraco; sala
reflexiva; LFE descartado no downmix do BT; codec SBC. Os presets DIALOGO e NOITE do celular foram
salvos antes do alinhamento e **não têm os atrasos**: estão desatualizados.
