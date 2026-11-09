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

## 2. Proteção recalculada (Fb 34,5; ficha da Protech de 27/10; Xmax 8 mm)

Conta em `calculo_excursao_sub_duto_novo.py` (mesmo modelo de `calculo_excursao_sub.py`: Small, Ql 7,
caixa de ~27 L, duto de 39,6 cm², tom senoidal na pior frequência, sem ganho da sala). Substitui a
primeira estimativa deste arquivo, feita no projeto Som sem a ficha (Qts e Vas estimados).

Calibração do limitador do sub (medida em 18/10): −6 dB → 40,6 V · −9 dB → 28,6 V · −12 dB → 20 V
nos bornes do sub (V ≈ 40,6 × 10^((L+6)/20)).

| Subsônico do sub (LR24) | Cone chega ao Xmax com | Limiar máximo do limitador | Ar no duto nessa tensão |
|---|---|---|---|
| 35 Hz | 36,6 V | −7,0 dB | ~25 m/s |
| 30 Hz | 33,9 V | −7,5 dB | ~31 m/s |
| 28 Hz (= 0,8 × Fb) | 33,1 V | −8,0 dB | ~33 m/s |
| 25 Hz | 28,8 V | −9,0 dB | ~33 m/s |
| 22 Hz | 23,9 V | −10,5 dB | ~30 m/s |

- Com 25 V (−10 dB) e subsônico de 25 Hz o cone fica em 87 % do Xmax.
- **O teto de 40 V (−6 dB) não serve mais em nenhum subsônico**: com 30 Hz dá 118 % do Xmax. Para
  música, limiar de no máximo −8 dB (~32 V).
- O ar no duto passa de 25 m/s perto do teto: em grave contínuo e volume alto o duto pode soprar
  e comprimir. Bocas arredondadas ajudam (a ponta do PVC foi lixada).

### Guardião (não alterado: decisão do Denis)

`estado/guardiao.json`, saídas 3 e 4, ainda traz `f_min_hz: 35` ("enquanto a Fb do duto for
desconhecida") e `teto_limiter_db: -8.0`. Com a Fb medida em 34,5 Hz:
- pela trava 2 de `damage-thresholds.md` (0,8 × Fb), o mínimo seria **28 Hz**, 24 dB/oit;
- a CINEMA gravada em 09/11 usa **25 Hz LR24**, abaixo do mínimo atual e também de 0,8 × Fb. Ela se
  apoia na conta de excursão acima (limitador em −10 dB), não na regra do 0,8 × Fb;
- o teto de −8 dB continua coerente com 28 Hz ou mais.

Opções: (a) `f_min_hz` 28 e CINEMA em 28 Hz; (b) `f_min_hz` 25 com o teto amarrado a −10 dB quando
o subsônico estiver abaixo de 28 Hz. Até o Denis escolher, o guardião segue em 35 Hz e vai apontar
a CINEMA nova como fora da trava.

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

Memória de música: HPF do sub 30 Hz LR24, limitador de no máximo **−8 dB (~32 V)** (não −6 dB).

## 4. Presets de EQ gráfico novos (sobre a CINEMA)

| Banda | 25 | 40 | 63 | 100 | 160 | 250 | 400 | 630 | 1k | 1,6k | 2,5k | 4k | 6,3k | 10k | 16k |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Ação v3 (27/10, Fb 51) | 0 | 0 | +1 | +1 | +1 | 0 | −1 | 0 | 0 | +1 | 0 | 0 | −1 | +2 | +1 |
| **Ação Profunda** | +1 | +2 | +2 | +1 | +1 | −1 | −1 | 0 | 0 | +1 | 0 | +1 | −1 | +2 | +1 |
| **Tremor** | +2 | +2 | +1 | 0 | +1 | −1 | −1 | 0 | 0 | +1 | 0 | 0 | −1 | +2 | +1 |

- A regra "25 e 40 Hz sempre em 0" valia para Fb 51 Hz. Com Fb 34,5 e HPF 25 LR24 + limitador
  ~25 V, 25 e 40 Hz podem subir até +2.
- **Tremor** pede, no sub: HPF **22 Hz LR24** e limitador **−11,5 dB (~21 V)** (88 % do Xmax; +1,9 dB
  em 25 Hz e +1,2 dB em 30 Hz contra HPF 25).
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
