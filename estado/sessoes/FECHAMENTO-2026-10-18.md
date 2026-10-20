# Fechamento da regulagem de 18/10/2026

Estado do home theater 2.1 (Taramps PRO 2.4BT + AUDIO → SounDigital SD800.4 EVO → 2× JBL 4TRMS80
nas BS-369 + Protech 8" em ponte) ao fim da sessão de 18/10/2026, com os motivos de cada decisão
e o que ficou pendente. Detalhes e medidas: `_bmad-output/specs/spec-taramps-automacao/analise-nivel-2026-10-18.md`;
base técnica: `guia-eq-filme.md`; diário: `estado/sessoes/2026-10-18.md`.

## 1. Decisões de base (valem para as três memórias)

| Decisão | Valor | Motivo |
|---|---|---|
| Fonte principal | **só Bluetooth do celular** | o BT entra ~13 dB abaixo do gerador do app; dois celulares deram 11,1 V iguais. O gerador fica só para medir: em −13 dB, ou em 0 dB **sem os alto-falantes** |
| Rotas | saída 1 = **B** (JBL L, CH1), saída 2 = **A** (JBL R, CH2), 3/4 = A+B (sub) | o L do BT chega ao processador como entrada B (teste com ruído L/R) |
| Corte das JBL | LR24 em 150 Hz | ficha da JBL 4" (≥ 100 Hz/24 dB) e excursão: com 11 V, só ≥ 140–150 Hz fica dentro do Xmax |
| Corte do sub | BT24 em 40 Hz + LR24 em 150 Hz, fase 180° | Fb do duto desconhecida (35 Hz barrado pela trava 2); fase escolhida de ouvido com ruído 80–250 Hz |
| Limitador das JBL (teto) | −11 dB → **11,7 V** medidos | ≤ 12,6 V da ficha; o gerador a 0 dB não passou disso |
| Limitador do sub (teto) | −9 dB → **28,6 V** medidos | o ganho do módulo em ponte é ~×40: com −6 dB passavam 40,6 V (~440 W), perto do clip do módulo. 28,6 V = margem de −3 dB sobre os 400 W da ponte |
| Guardião | regra 6 off nas 4 saídas; regra 7 = +18 dB; teto do sub −9 dB | decisões do Denis em 18/10, registradas no `estado/guardiao.json`; limites ainda em definição |

Lado físico: RCA das saídas 1/2 e 3/4 estavam trocados no módulo (corrigido); JBL L no CH1 e R no CH2;
JBL medidas com 3,6 Ω depois do grave sem corte.

## 2. Memórias do DSP

### Memória 1 — MUSICA HIGH (aprovada)
JBL **+2 dB** (BT a 100 % = 11,4 V, no teto), sub **−1,4 dB** (~20 V, 3 dB abaixo do teto, pelo
equilíbrio da música), EQ plano, ataque das JBL 10 ms, limitadores −11/−9, sem atrasos.
Máximo útil com o BT; para o sub também no máximo, passar para +1,6 dB.

### Memória 2 — MUSICA ORIG (**desatualizada**)
Ainda a versão da tarde: ganhos −5/−10, limitador do sub em −6 dB (teto ~40 V). Refazer.

### Memória 3 — FILME definitiva (21h45, retrato `estado/leituras/2026-10-18_2145.json`)
Sala medida: JBL L 3,20 m, sub 2,40 m, JBL R 2,20 m, 1,60 m entre as JBL → ouvinte 0,9 m à direita da R
(51° fora do eixo da L, 24° da R), caixas no chão apontadas para a parede oposta.

| | L (saída 1) | R (saída 2) | Sub (3/4) | Motivo |
|---|---|---|---|---|
| Atraso | 0 | **100 cm** | **80 cm** | iguala a chegada no ouvinte (prática de áudio automotivo) |
| Ganho | +10 | **+7** | +6 | 20·log(3,20/2,20) = 3,3 dB a mais na R |
| Limitador | −15 | **−18** | −12 | compressão suave: a voz (−27 LKFS) passa, os picos da ação são segurados |
| Ataque / liberação | 50 / 200 ms | idem | 50 / 200 ms | cauda do estrondo volta logo (1000 ms cortava a sustentação) |
| EQ de saída | 2 kHz +2,5 Q 0,7 | idem | 45 Hz +3 Q 0,7 | presença da voz; sustentação do grave |

EQ global: 160 Hz +1, 400 Hz −1, 4 kHz −1, 6,3 kHz −2, 10 kHz +2, 16 kHz +2. Volume do celular: 60–70 %.
Medidas com as caixas ligadas numa cena pesada: JBL 10,2 V, sub 24 V (abaixo dos tetos).

**Testado e descartado (22h):** ACAO com compressão mais forte (−19/−22/−15, ataque 20 ms), sub +2 e
voz +4 dB em 2 kHz: piorou. A FILME definitiva ficou melhor.

## 3. Presets do celular
DIALOGO e NOITE existem no app, mas foram salvos **antes** dos atrasos e da compressão: desatualizados.

## 4. Pendências (por impacto)
1. Refazer a MUSICA ORIG (Memória 2).
2. Refazer ou apagar DIALOGO e NOITE no celular.
3. Físico e grátis: virar as JBL para o ouvinte, levantar à altura do ouvido, sentar entre elas, tapete.
   Depois recalcular atrasos e níveis.
4. Tweeter da JBL esquerda mais fraco (acompanha a caixa): abrir e conferir.
5. UMIK-1 + REW: medir a sala e a Fb do duto (só então descer o subsônico).
6. Fonte melhor que o BT: TV box Android 14 + extrator HDMI → RCA; recalibrar o ganho.
7. Código sem commit: `energia.py` (remote out 252), `tom.py`, `memoria.py` e alterações em
   `ferramentas/`; rodar a suíte de testes antes de usar no aparelho.
8. Linha de base do guardião com rotas e ganhos antigos (não pesa com a regra 6 desligada).
