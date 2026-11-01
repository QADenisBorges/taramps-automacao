# Telas do Rendimento

Sem rodada de UX (decisão do Denis, 30/10/2026). As telas seguem o `DESIGN.md` e o `EXPERIENCE.md` de `planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30`: visual BGS Sound, um passo por tela, folhas que sobem do pé, anel duplo em todo carregamento, aviso rápido de 2,6 s e nenhuma tela com a palavra "IA". O que não está desenhado lá está marcado com [ASSUMPTION] e é conferido no celular no fim.

| Tela | Onde | O que mostra |
|---|---|---|
| **Limite da saída** | Ajustes › Editor de saída, perto do limitador | O teto do nível em uso em V e em dB, o limite que manda (térmico, curso do cone ou módulo) e o selo de origem ou "estimado". Um toque abre a conta: os três limites, os dados usados com a origem e o dado que falta. [ASSUMPTION] |
| **Recusa da cerca** | Aviso rápido, onde a escrita foi pedida | "Limite da <saída>: <dB>, pelo <motivo>." Nada é escrito. |
| **Sem calibração** | Editor de saída e escolha do nível | A faixa "Sem calibração: o app não sabe a tensão" com "Calibrar ›". [ASSUMPTION] |
| **Calibração guiada** | Ajustes › Medir › Calibrar, um passo por tela | 1) Desligue os alto-falantes do módulo (confirmar). 2) Multímetro em V~ 200 na saída do <canal ou ponte>. 3) O gerador toca o tom (50 a 400 Hz, nunca 1 kHz; regras do Gerador e a chave dele). 4) Digite a tensão lida. 5) Tom de 0 dBFS pelo player do app, com o celular a 100 %. 6) Digite a tensão. Fim: o ganho do módulo e o nível do Bluetooth, com a origem "medido". |
| **Nível da memória** | Folha a partir do centro da Roda ou do Salvar [ASSUMPTION] | Baixo, Médio, Alto, Máximo e Extra, cada um com o teto e a fonte numa linha ("Alto · até 16 V nas JBL · qualquer fonte"). O Extra aparece apagado, com o motivo, sem modelo térmico ou sem playlist analisada. |
| **Aviso do Máximo** | Folha de risco do app, ao escolher o Máximo | "Passa da potência da ficha. Seguro com música; um tom contínuo ou música muito comprimida por muito tempo podem aquecer a bobina." Confirmar e Cancelar. |
| **Otimizar na playlist** | Player, na playlist da memória | "Otimizar configuração na playlist": o progresso da análise (anel duplo, sem números) e depois o pedido do teste a seco. |
| **Teste a seco** | Tela toda, um passo por tela | 1) "O módulo vai ser desligado." 2) Os trechos tocando, com a música e o trecho atual. 3) Resultado: teto validado ou o clip encontrado (música e saída). |
| **Quem está pesando** | Player, em cada faixa | Quantos dB a faixa tira do teto. A faixa "em análise" fica sem tocar até terminar. Faixa marcada por clip mostra a saída e o momento. |
| **Extra ao vivo** | Mini player e Roda [ASSUMPTION] | O tempo que falta de Extra. No descanso: "Descansando: <tempo>", com o Bluetooth travado e sem tocar. |
| **Revalidar** | Roda, na memória | O selo "revalidar" e o motivo. Um toque leva ao "Otimizar na playlist". |
| **Outro player** | Folha, ao detectar outra fonte no Extra | "Outro app começou a tocar. O som foi mutado." Voltar ao player do app. |

## Lugares escolhidos na 13g [ASSUMPTION]

Sem a Roda, a 13g pôs as telas nestes lugares. A 12d confere no aparelho.

- **Calibração guiada:** entra por Configurações › "Calibrar saídas" (a escolha da saída) e pelos "Calibrar ›" das faixas de sem calibração (no Ajuste da saída e na folha do nível), que já abrem a saída no passo 1.
- **Nível da memória:** o chip "Nível: Alto ›" (ou "Sem nível ›") fica no cartão da memória em uso do Início e no cartão da memória em uso em Memórias. A folha sobe por cima de tudo, uma só.
- **Extra ao vivo:** no topo do Player e no cartão da memória em uso do Início.
- **Revalidar:** o selo "revalidar", com o motivo, fica dentro da folha do nível; um toque leva ao "Otimizar na playlist", no Player.
- **Aviso rápido (2,6 s):** além da casca, aparece também no Ajuste da saída, nas telas de função, na calibração guiada e no teste a seco.
