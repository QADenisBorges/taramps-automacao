# Limites e níveis de rendimento

Decisões do Denis de 30/10/2026. Método e números de conferência: `_bmad-output/regulagem/memoria-spl.md`, seções 12 e 13. Os valores do sistema do Denis servem só de dado de teste: o app começa zerado.

## Limite físico de uma saída (CAP-1)

Calculado pelas fichas do Setup em uso e pelo corte da memória. Três limites, em tensão RMS de um tom contínuo:

| Limite | Conta | Dados |
|---|---|---|
| Térmico (T) | V = √(P_RMS × Z_nominal) | potência RMS e impedância do alto-falante |
| Excursão (X) | tensão em que o cone chega ao Xmax na pior frequência, com o passa-alta/subsônico da memória (modelo eletromecânico; caixa selada ou dutada) | Re, Fs, Qes, Qms, Vas, Bl, Sd, Xmax; tipo, volume e Fb da caixa |
| Ceifamento (C) | tensão de ceifamento do canal ou da ponte que alimenta o alto-falante | ficha do módulo |

Dados faltando para X:
1. Ficha completa do falante e da caixa: X pela conta.
2. Caixa com duto, Fb conhecida e subsônico de 24 dB/oit em até 0,8 × Fb: X conta como protegida pelo duto (X = ∞). Selo "estimado".
3. Senão: X = min(T, C) − 3 dB. Selo "estimado" e aviso do dado que falta (ex.: medir a Fb).

Nunca bloqueia o uso. Um dado sem origem confirmada leva o resultado a "estimado".

Conversão para o limitador: V_saída = 2,0 × 10^(dB/20) × ganho do módulo (medido, CAP-3). Sem ganho medido, a saída fica "sem calibração" e nada aqui vira número.

Conferência com o exemplo (JBL 4TRMS80 em 4 Ω, caixa selada de 3,5 L, LR24 em 200 Hz, canal do SD800.4 EVO com ganho ×19,5): T = 17,9 V; X = 22,2 V; C ≈ 23 V; min = 17,9 V → −6,8 dB. Sub Protech Light 8" na ponte (×40,5), dutado 28 L, Fb 51 Hz: T = 42 V; C ≈ 40 V; X pela conta da §13.2.

## Níveis (CAP-4, CAP-5, CAP-7)

| Nível | Teto da saída | Margem | Fonte | Como o master é posto |
|---|---|---|---|---|
| Baixo | min(T, X, C) | −6 dB | qualquer | pior caso |
| Médio | min(T, X, C) | −3 dB | qualquer | pior caso |
| Alto | min(T, X, C) | −1 dB | qualquer | pior caso |
| Máximo | min(X, C) | −1 dB | qualquer, com aviso do risco de calor | pior caso |
| Extra | min(X, C) | −1 dB | só a playlist auditada | picos reais da playlist |

- **Pior caso:** um tom a 0 dBFS, em qualquer frequência, passando pela cadeia inteira da memória e do preset (EQ de entrada, 15 bandas, EQ de saída com os reforços, cortes, ganho, nível do Bluetooth). O master fica no maior valor em que nenhuma saída passa do teto menos a margem.
- **Picos reais (Extra):** o master fica na menor folga entre músicas e saídas, menos a margem. Se uma música passa, ele desce para todas; se nenhuma passa, sobe até quase encostar.
- **Um master por preset:** ao trocar o preset, o master vai para o da combinação memória + preset.
- **Máximo:** passa do térmico porque o RMS da ficha é potência média (AES: picos de 4× por 2 h) e música tem média de ~1/10 do pico. O risco que fica: tom contínuo ou música esmagada no máximo por muito tempo. O app avisa e não vigia a média de fontes de fora.
- **Extra térmico:** a média vem da análise da playlist, no master em uso. Modelo de duas constantes de tempo (bobina e ímã). O Extra corta quando a bobina estimada chega à temperatura máxima da ficha, sem folga. Depois vem o descanso com o Bluetooth travado (251 desligado e mute 100) até a bobina estimada voltar à temperatura ambiente do Setup. Sem modelo térmico válido, o Extra não aparece. Com dado térmico só digitado, ele leva o selo "estimado".

## Cerca (CAP-2)

O limitador é a última etapa da cadeia. Pela inferência da §12.5, o EQ entra antes dele, e isso ainda precisa ser medido. Por isso a cerca vigia só o limitador de cada saída:
- Do Baixo ao Alto, e sem nível: limitador ligado e com limiar ≤ min(T, X, C).
- Máximo e Extra: limitador ligado e com limiar ≤ min(X, C).

Uma escrita que desligue o limitador ou passe do limiar é recusada antes de qualquer envio, pela tela e pela porta, com a conta ("limite da JBL L: −6,8 dB, pelo térmico"). Para mudar o limite, muda-se a ficha ou a medida, com a origem. A cerca vale para qualquer origem do comando, inclusive o Agente da memória.

## Sem calibração (CAP-3)

Enquanto uma saída não tem o ganho do módulo medido:
- a porta recusa ganho, limitador, EQ e MASTER dela;
- o app não oferece níveis;
- a tela deixa mexer, com o aviso "sem calibração: o app não sabe a tensão".

O ganho medido é dado físico do Setup (ficha do módulo, origem "medido"). Se ele mudar, as memórias do Setup vão para "revalidar".

## Revalidação

| O que mudou | Baixo a Máximo | Extra |
|---|---|---|
| Cadeia da memória (EQ, corte, ganho, limitador) ou 15 bandas de um preset | A conta é refeita na hora e o master se ajusta | A análise é refeita sozinha. Se o teto desceu, aplica na hora; se subiu, só depois do teste a seco |
| Ficha ou ganho medido do Setup | O mesmo, em todas as memórias do Setup | O mesmo, e a memória fica em "revalidar" até o teste a seco |
| Música nova | — | Analisada em segundo plano; não toca até terminar; dá para adicionar outras enquanto isso. Se pesa mais, o master desce |
| Clip não previsto no uso | — | O master desce um passo e a música é marcada (saída e momento). 2 clips não previstos na mesma música mandam a memória para "revalidar" |

Baixar o master é sempre seguro e acontece na hora. Subir no Extra exige o teste a seco.

## Teste a seco (CAP-6)

Módulo desligado (252). O player do app toca só os trechos mais fortes que a análise achou (~10 s, 2 por música), e o app escuta o clip (150). Um clip não previsto derruba o teto daquela combinação. O módulo só volta a ligar depois de o master estar no valor validado. Se o app fechar ou a conexão cair no meio, o módulo fica desligado (estado seguro).

## Fiscal no uso do Extra (CAP-8)

- Clip: segue a linha "Clip não previsto" da revalidação.
- Exclusividade: outro player tocando faz o app mutar (100) e pausar, com mensagem.
- O volume de mídia do celular fica travado no valor da conta (100 %); se mudar, o app o devolve.
- Se o app fechar durante o descanso do Extra, o mudo fica no aparelho.
