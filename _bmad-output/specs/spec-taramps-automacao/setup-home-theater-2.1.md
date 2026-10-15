# Setup de referência: home theater 2.1

É a instalação real em que o software será testado, e o primeiro inventário da CAP-11. Os dados vêm do projeto Som (`Desktop/Som`: `componentes/*.md`, `projetos/home-theater-2.1/montagem-2.1.md`, guia v2.6). Esse projeto continua sendo a autoridade sobre a montagem.

## Cadeia

Celular (BT) ou notebook (P2) → **Taramps PRO 2.4BT + AUDIO** (modo BOB) → **SounDigital SD800.4 EVO** (alimentado pela **Taramps BOB 60A** a 13,8 V) → JBL L/R + sub em ponte.

| Saída do PRO | Entrada do SD800.4 | Carga | Cabo |
|---|---|---|---|
| 1 (origem A) | CH1, chave em F | JBL 4TRMS80 L na BS-369 (~3,5 L selada) | 2 × 2,5 mm², 3,0 m |
| 2 (origem B) | CH2, chave em F | JBL 4TRMS80 R na BS-369 | 2 × 2,5 mm², 3,0 m |
| 3 e 4 (A+B) | CH3/CH4, chave em F, **ponte**: CH4 ⊕ / CH3 ⊖ | Protech Light 8" 4 Ω, caixa pronta **com duto** | 2 × 2,5 mm², 1,0 m, emenda soldada |

Alimentação: fusível de 40 A no + do módulo (10 mm²); fusível do processador de 5 A provisório, a trocar por 1–2 A (1,5 mm²); REM OUT do processador → REM do módulo.

## Dados que pesam nas travas

| Falante | Dado | Valor | Fonte |
|---|---|---|---|
| JBL 4TRMS80 (triaxial 4"; tweeter e médio no mesmo cone, sem saída separada) | Corte mínimo do fabricante | **100 Hz a 24 dB/oit** | Ficha da caixa |
| | Re / Xmax / potência | 3,4 Ω / 1,75 mm (pico) / 80 W RMS | Ficha |
| | Na BS-369 | Fc ≈ 124 Hz, selada (soma 12 dB/oit acústicos) | Cálculo do projeto Som |
| | Excursão com BW2 em 120 Hz | Xmax com ~12 W perto de 122 Hz | `calc_som.py excursao`, 09/10 |
| Protech Light 8" | Potência / Xmax / Fs | 450 W RMS / 8 mm / 46 Hz | Lojas (sem ficha do fabricante) |
| | Caixa | Com duto (foto de 11/10); **Fb desconhecida** | Projeto Som |
| SD800.4 EVO | Ponte 4 Ω | 400 W RMS a 12,6 V (tabela de ganho: 40 V) | Manual |
| PRO 2.4BT + AUDIO | Saída máxima / nível por saída | 2,1 V RMS / −45 a **+15 dB** | Manual |

## Valores seguros (regras de `damage-thresholds.md` aplicadas a este setup)

| Trava | Saídas | Valor seguro | O guardião bloqueia |
|---|---|---|---|
| Passa-alta da JBL (classificada pelo cone, 4") | 1 e 2 | **≥ 100 Hz com ≥ 24 dB/oit** (ficha). O **Butterworth 12 dB/oit em 120 Hz** do projeto passa depois que o Denis registra no inventário a simulação de excursão de 09/10 como corte validado. | Passa-alta desligado; curva somada acima da referência abaixo do corte vigente; reforço de EQ abaixo do corte |
| Subsônico do sub | 3 e 4 | **35 Hz, 24 dB/oit** enquanto a Fb for desconhecida; depois **≥ 0,8 × Fb**, 24 dB/oit | Subsônico desligado; curva somada acima de um Butterworth de 24 dB/oit no mínimo vigente |
| Ponte | 3 e 4 | Filtros, fase, atraso, nível, EQ e limiter **idênticos** | Qualquer escrita que deixe as saídas 3 e 4 diferentes |
| Teto de nível | 1 e 2 | **11 V AC**: ponto validado pela excursão (≈30 W), sem margem extra | Desligar o limiter; subir threshold, nível, master ou EQ acima do teto; G_ef > −1 dB |
| Teto de nível | 3 e 4 (ponte) | **≈ 28 V AC**, calculado pelas fichas: 400 W × 4 Ω = 40 V, menos 3 dB. Passa a 40 V se o Denis registrar os 40 V como ponto validado. | Idem |

O projeto Som configura o subsônico em 30 Hz e o sub em 40 V. Os dois ficam bloqueados até a etapa de Fb e a validação do Denis.

## Etapas do setup ainda não cumpridas (CAP-11 e CAP-13)

Nenhuma leitura real de multímetro foi registrada até 15/10. Os valores acima são esperados ou alvos, e a configuração do setup pede ao Denis:
1. **Resistência** da JBL L e R (esperado ≈ 3,4 Ω) e do sub (3–3,6 Ω; ~7 Ω muda o alvo da ponte para ~46 V).
2. **Tensão da fonte** na régua do módulo (13,8 V DC).
3. **Calibração por saída** (passos 4.10 e 4.11 do guia Som): tensão AC com o celular a 100 % e tom de 60 Hz a 0 dBFS, sem falantes; o limitador fica no ponto calibrado. Só esta etapa fixa o teto real de cada saída.
4. **Fb do duto do sub:** medidas do duto e da caixa, ou varredura de 25 a 60 Hz (passo 5.3). Até concluir, o subsônico fica em 35 Hz, a 24 dB/oit.
