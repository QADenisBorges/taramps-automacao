# Duto novo do sub e nova regulagem da CINEMA (novembro de 2026)

Registro vindo do projeto de som (`QADenisBorges/sound`, `projetos/sub-protech-duto/projeto.md`, onde
estão as contas completas). Substitui as premissas de Fb 51 Hz usadas em `memoria-spl.md` (seções 12
e 13) e em `presets-filme.md` (regra "25 e 40 Hz sempre em 0").

## 1. O que mudou na caixa (07–09/11)

| | Antes | Agora |
|---|---|---|
| Duto | preto, 11 cm, Ø int. 7,5 cm | preto 11 cm + PVC esgoto 75 mm 22 cm (bolsa por fora), total ~33 cm |
| **Fb medida** | ~51 Hz | **~34,5 Hz** (gerador do PRO 2.4, 09/11: cone mais parado em 34, ar mais forte em 35) |
| Pico de resposta | +6,5 dB em ~54 Hz | ~+3 dB em ~37 Hz |
| Corte −3 dB | ~43 Hz | ~31 Hz |
| Falante | — | etiqueta marca **4 Ω** (bobina simples); multímetro **3,7 Ω** → ponte 4 Ω liberada |

Caixa vedada com silicone em 09/11 (cura: 24 h; camada grossa, 2–3 dias antes de volume alto). Ponta
do PVC lixada em meia-lua. Mês que vem: manta butílica 3 mm e, opcional, joelho 90° (Fb ~32 Hz).

Atenção na varredura: o HPF dos canais 3/4 em 45 Hz BT24 (ajuste antigo) mascara a Fb. Para medir,
HPF em 10 Hz e ganho do gerador até −20 dB; depois voltar o HPF.

## 2. Proteção recalculada (Fb 34,5; modelo de Small, Xmax 8 mm)

Calibração do limitador do sub (medida em 18/10): −6 dB → 40,6 V · −9 dB → 28,6 V · −12 dB → 20 V
nos bornes do sub (V ≈ 40,6 × 10^((L+6)/20)).

| HPF do sub (LR24) | Tensão em que o cone chega ao Xmax |
|---|---|
| 30 Hz | 34–42 V |
| 25 Hz | 26–33 V |
| 22 Hz | 21–28 V |

O ar no duto passa de ~40 m/s a 400 W em qualquer duto que caiba (7 cm é pequeno para 400 W num 8");
na prática não sopra até ~100 W em grave contínuo.

## 3. Memória 3 CINEMA (configurada no app em 09/11)

| Item | Leitura de 27/10 | Agora |
|---|---|---|
| Sub (3 e 4) HPF | 45 Hz BT24 | **25 Hz LR24** |
| Sub LPF | 150 Hz LR24 | 150 Hz LR24 (casa com HPF 150 LR24 das JBL) |
| Sub limitador | −15 dB (~14 V) · 15 ms · 200 ms | **−10 dB (632 mV, ~25 V)** · 15 ms · 200 ms |
| JBL limitador | L −12 / R −15 · 50 ms | L −12 / R −15 (sugestão pendente: −10 nas duas, ~12,5 V) |
| Níveis | master 0 · +10 / +7 / +6 / +6 | master −1 · **+6 / +5 / +5 / +5** |
| EQ de saída sub 45 Hz | 0 | 0 (não voltar o +3) |
| Fase / atraso do sub | 180° / 80 cm | refazer: sub mudou para debaixo da mesa, ponta esquerda |

Memória de música: HPF do sub 30 Hz LR24, limitador −6 dB (~40 V).

## 4. Presets de EQ gráfico novos (sobre a CINEMA)

| Banda | 25 | 40 | 63 | 100 | 160 | 250 | 400 | 630 | 1k | 1,6k | 2,5k | 4k | 6,3k | 10k | 16k |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Ação v3 (27/10, Fb 51) | 0 | 0 | +1 | +1 | +1 | 0 | −1 | 0 | 0 | +1 | 0 | 0 | −1 | +2 | +1 |
| **Ação Profunda** | +1 | +2 | +2 | +1 | +1 | −1 | −1 | 0 | 0 | +1 | 0 | +1 | −1 | +2 | +1 |
| **Tremor** | +2 | +2 | +1 | 0 | +1 | −1 | −1 | 0 | 0 | +1 | 0 | 0 | −1 | +2 | +1 |

- A regra "25 e 40 Hz sempre em 0" valia para Fb 51 Hz. Com Fb 34,5 e HPF 25 LR24 + limitador
  ~25 V, 25 e 40 Hz podem subir até +2.
- **Tremor** pede, no sub: HPF **22 Hz LR24** e limitador **−11,5 dB (~21 V)** (+1,9 dB em 25 Hz e
  +1,2 dB em 30 Hz contra HPF 25).
- Enquanto o diagnóstico abaixo não fecha: 25 e 40 Hz em +1 e volume 50–60%.

## 5. Escuta de 09/11 e pendências

Percepção do Denis: grave pouco estrondoso; nas explosões o som "afunda/corta"; o sub "parece ignorar"
o grave mais profundo. Fonte: **celular + Netflix via Bluetooth** (estéreo, sem controle do LFE).

Pendente:
1. Gerador em 30 Hz, ganho −20 e −15: se o sub tremer bem, o limite é o sinal (LFE ausente/fraco).
2. Bolinhas do limitador durante a explosão (1/2 = JBL; 3/4 = sub).
3. Tensão DC no SD800.4 durante a explosão (abaixo de ~11 V = fonte em proteção).
4. Fase (tom de 150 Hz, 0° × 180°) e atrasos com as distâncias novas até onde deitam.
5. Comparação com arquivo no notebook pelo MPC-BE/LAV Audio, misturando o LFE no estéreo.
