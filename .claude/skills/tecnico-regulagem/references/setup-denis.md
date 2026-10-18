# Setup do Denis e o que já se sabe (base para começar do zero)

Fontes: `_bmad-output/specs/spec-taramps-automacao/setup-home-theater-2.1.md`, as medidas da
sessão de 17/10/2026 (`estado/sessoes/2026-10-17.md`) e a auditoria independente
(`estado/auditorias/2026-10-17_guardiao.md`). Em conflito, a medida mais recente vence.

## Home theater 2.1 (casa)

| Elo | Equipamento | Dados que pesam |
|---|---|---|
| Fonte | celular (Bluetooth) ou notebook (P2 → 2 RCA; falta o cabo P2 macho → 2 RCA) | celular: limitador de mídia desligado, volume BT 15/15, volume absoluto ativo |
| Processador | Taramps PRO 2.4BT + AUDIO (4 saídas, 2,1 V RMS máx.) | limitador em dB (0 dB ≈ 2,0 V); só 3 memórias no DSP + presets no celular; o app reescreve attack/release ao abrir a tela Limiter em Auto |
| Módulo | SounDigital SD800.4 EVO, fonte Taramps BOB 60A | CH1/CH2 normais; CH3+CH4 em ponte (sub: + no CH4 ⊕, − no CH3 ⊖) |
| Saídas 1/2 | JBL 4TRMS80 (4" triaxial) em BS-369 selada ~3,5 L | corte da ficha 100 Hz/24 dB; Xmax 1,75 mm; 80 W RMS; Fc ≈ 124 Hz |
| Saídas 3/4 | Protech Light 8" 4 Ω, caixa dutada (Fb desconhecida) | 450 W RMS (dado de loja); alvo na ponte ≤ 28 V |

## Medidas (17/10/2026)

| Medida | Valor |
|---|---|
| Fonte (régua do módulo) | 13,89 V DC |
| JBL L / JBL R | 3,69 Ω / 3,69 Ω |
| Sub | 3,7 Ω |
| Saída 1 com tom de 400 Hz, ganho −11 dB, limitador −11 dB | 11,0 V AC |
| Saída 1 com +2 dB de ganho | 11,6 V (o limitador segurou ~1,5 dos 2 dB) |
| Sub na ponte, tom de 50 Hz, limitador −6 dB | 23,4 V AC (abaixo do alvo de 28 V) |
| Ganho implícito do módulo nas JBL | ≈ 19,6× (0,56 V → 11 V) |
| Ganho implícito no sub | ≈ 23,4× (1,0 V → 23,4 V) |

As medidas valem **enquanto ninguém mexer no botão de ganho do módulo.** Se mexer, refazer.

## O que se aprendeu (17/10)

- Configuração que funcionou: JBL em LR24 150 Hz (saída 1 em rota A, saída 2 em B, nada
  mutado), sub em BT24 40 Hz + LR24 150 Hz, ponte igual, EQ de saída zerado.
- Saída 1 em A+B soma +6 dB antes do limitador: a JBL L passava muito do curso. **Não usar A+B
  nas JBL.**
- Volume das JBL: com corte em 150 Hz o seguro é até ~12,6 V; a 15,5 V (60 W de pico) só com o
  corte em ~200 Hz (o curso cai com o quadrado da frequência). A 11 V a sensação é de "pouco
  volume": a média com música fica em ~3 W.
- Sub com folga medida: limitador de −6 para ~−4,5 dB leva a ~28 V (+1,5 dB).
- Spotify normaliza o volume (pode tirar 3–6 dB): "Normalizar volume" desligado ou "Alto".
- Antes de culpar a regulagem por "som fraco": conferir polaridade das JBL (+ no vermelho nas
  duas) e a fase do sub (estava em 180°).

## Histórico

<!-- Acrescente aqui, com data, cada etapa feita a partir de 18/10 (valores e medidas). -->
