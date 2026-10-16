# Levantamento do Modo Demo (Parte D do roteiro)

**Data:** 2026-10-16. **App:** `com.taramps.pro24bt_audio` 1.0.8 (versionCode 12), num Galaxy A52 (SM-A525M) com Android 14.
**Método:** feito automaticamente pela IA via `adb`, com o processador desligado. Os valores com texto vêm da árvore de interface (`uiautomator dump`). Alguns valores são desenhados direto na tela e não aparecem na árvore: master, ganho dos canais, delay e o threshold em volts. Esses foram lidos em recortes da captura de tela. O botão "Conectar" nunca foi tocado.

No Demo, o app não gera tráfego BLE. Nada aqui prova a codificação do protocolo; são as faixas, os passos e as opções que o protocolo precisa representar.

## Estado inicial do Demo

O Demo não começa zerado:
- master em 0,0 dB;
- canal 1 em −16,4 dB, canal 2 em −16,5 dB, canal 3 em −12,0 dB;
- roteamento: canal 1 = A, canal 2 = B, canais 3 e 4 = A+B;
- fase 0 em todos os canais;
- crossover em 10 Hz / 22000 Hz com filtros OFF;
- limiter em OFF, com attack 1,0 e release 500.

Tudo o que a sondagem mudou foi desfeito, menos o preset `D0DEMO`, que foi salvo no celular.

## Telas e controles

| Tela | Controle | Faixa no app | Passo do +/− | Observações |
|---|---|---|---|---|
| Home | ON/OFF, Áudio BT | — | — | Mute por canal, **link de canais 1–2 e 3–4** ("Vincular canais"), voltímetro (12,0 V no Demo), indicadores In Clip e Limiter/Out Clip, 5 atalhos favoritos. |
| Roteam. Ent/Saída | rota por canal | A / B / A+B | seletor | |
| EQ 15 Bandas (entrada) | ganho por banda | −12 a +12 dB (manual) | 0,1 dB | Bandas: 25, 40, 63, 100, 160, 250, 400, 630 Hz; 1,0; 1,6; 2,5; 4,0; 6,3; 10,0; 16,0 kHz. |
| EQ 15 Bandas | preset de EQ | Flat, Loudness, Bass Boost, Mid-Bass Boost, Treble Boost, Powerful, Electronic, Rock Style, Hip-Hop Style, Pop Music, Vocal, Competition + "Custom" | seletor | "Custom" aparece antes de Flat quando há edição. |
| EQ Param Entr | ganho / freq / Q | −12 a +12 dB / 10 a 22000 Hz / **0,4 a 10,0** | 0,1 dB / 1 Hz / 0,1 | **Uma banda só.** Não separa as entradas A e B. Valor digitado fora da faixa **trava no limite**. Padrão: 0,0 dB, 1000 Hz, Q 1,0. |
| Crossover (por canal) | HPF e LPF freq. | 10 a 22000 Hz | 1 Hz (também em 10 Hz e 22 kHz) | Embaixo do gráfico há caixas "Canal 1…4"; a função ainda não foi testada. |
| Crossover | HPF e LPF filtro | 16 opções, nesta ordem: OFF, LR12, LR18, LR24, LR30, LR36, LR42, LR48, BT6, BT12, BT18, BT24, BT30, BT36, BT42, BT48 | seletor; para no fim, sem voltar ao começo | Essa ordem é um bom palpite para o índice no protocolo. |
| Alinhamento (por canal) | delay | 0 a 272 cm (manual; o máximo não foi confirmado) | 0,5 cm | O app mostra cm e ms: 0,5 cm = 0,015 ms, 26,0 cm = 0,756 ms (velocidade do som ≈ 344 m/s). A tela também tem fase 0/180 e mute. |
| Fase | fase por canal | 0 / 180 | seletor | |
| Limiter (por canal) | threshold | −48,0 a 0,0 dB e **OFF acima de 0** | 0,1 dB | Do OFF, o − leva a 0,0. Abaixo de −48 o valor trava em −48. Auto liga/desliga. |
| Limiter | attack / release | 0,1 a 100 ms / 1 a 1600 ms (manual) | 0,1 ms / 1 ms | Padrão: 1,0 / 500. |
| Nível das Saídas | master / ganho por canal | −80 a 0 / −45 a +15 dB (manual) | 0,1 dB | 10 toques = 1,0 dB. Segurar o botão repete o passo cerca de 3 vezes por segundo. |
| EQ Param Saída (por canal) | ganho / freq / Q | −12 a +12 dB / 10 a 22000 Hz / **0,1 a 10,0** | 0,1 dB / 1 Hz / 0,1 | **Uma banda só por canal.** Valor digitado fora da faixa é **rejeitado** (o campo mantém o anterior). Padrão: Q 0,4. |
| Gerador de Áudio | ganho / freq | padrão −45,0 dB / 1000 Hz | — | Modos Gerador fixo e Sweep. Não foi ligado. |
| Configurações | nome do produto, senha | nome com até 12 caracteres (A–Z, 0–9, _ e espaço) | — | **Senha de 4 caracteres A–Z, a–z, 0–9** (não só dígitos). O Demo avisa "salvar atualiza apenas o estado local". Firmware "-" no Demo. |
| Salvar / Carregar | presets | Memória 1–3 (DSP) + presets no celular | — | Não existe memória "Factory default". Há botões "Resetar configurações de Áudio" e "Resetar aos Padrões de Fábrica" (não tocados). |

## Pista do limiter (D2)

O app mostra o threshold em volts calculado por **V = 2,00 V × 10^(threshold/20)**. Os pontos lidos batem com a fórmula:

| Threshold (dB) | 0 | −0,2 | −1 | −3 | −6 | −10 | −12 | −20 | −24 | −30 | −40 | −48 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Tela | 2.00V | 1.95V | 1.78V | 1.42V | 1.00V | 632mV | 502mV | 200mV | 126mV | 63mV | 20mV | 8mV |

- Em OFF, a tela mostra "2.10V", que é a saída máxima do manual.
- Com o threshold em −6 dB, mudar o ganho do canal 1 em +1,0 dB ou o master em −1,0 dB **não muda** o valor de 1.00V.
- Conclusão: a escala em volts do app é fixa (0 dB = 2,00 V) e não leva o ganho em conta. Isso não diz se o limiter atua antes ou depois do ganho no DSP; a medição com multímetro (roteiro 4.2) continua necessária.

## Implicações para o guardião (para as histórias 4 e 5)

- O app aceita **Q 0,1** no EQ de saída, abaixo do 0,4 do manual. Um Q tão baixo afeta uma faixa larga, inclusive abaixo do corte.
- O EQ de saída tem uma banda e o de entrada também. O EQ gráfico de 15 bandas é da entrada e vale para todas as saídas.
- O link de canais (1–2 e 3–4) existe, então uma escrita num canal pode alterar o par.

## Presets (D3)

- O celular já tinha 7 presets do Denis: MUSICA R1 A B, FILME, TSUB, PRIMEIROSOM, LIMITADOR, GANHO e TESTE.
- A IA salvou o estado do Demo como `D0DEMO` (no Smartphone).
- **Exportar:** deslizar o preset para a **direita** mostra "Carregar" e "Exportar"; para a esquerda, "Editar" (renomear) e "Excluir". "Exportar" abre o menu de compartilhar do Android com o arquivo **`d0demo.trps`**. A IA fechou o menu sem escolher destino, porque ele só oferecia contatos e apps de mensagem.
- Pendente: escolher um destino local para salvar os `.trps` no celular e copiá-los para `protocolo\presets\`.
