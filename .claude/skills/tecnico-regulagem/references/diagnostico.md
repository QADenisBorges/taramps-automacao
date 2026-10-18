# Diagnóstico — do mais provável e barato ao mais caro

Sempre uma hipótese por tela, com um teste que confirma ou descarta.

## "Som fraco" / "parece caixa de 3 W"

1. **Fonte:** volume de mídia no máximo, limitador de volume do celular desligado, volume
   absoluto BT, normalização do app de música (Spotify: "Normalizar volume").
2. **Polaridade invertida** num lado (+/− trocados): grave e médio somem; som "espalhado".
3. **Fase do sub** contra as JBL: buraco em 80–150 Hz.
4. **Rota errada** (canal sem sinal, mute ligado, A/B trocados).
5. **Ganho/limitador muito baixos** para o que o sistema aguenta (comparar com as medidas).
6. **Limite físico real:** falante pequeno (4") tem pouco curso; mais volume exige corte mais
   alto e o sub assumindo mais. Dizer o ganho possível em dB.
7. Comparação objetiva: decibelímetro no celular a 1 m, mesma música, volume máximo da fonte.

## "Sem grave"

Fase do sub, subsônico alto demais, passa-baixa do sub abaixo do passa-alta das JBL (buraco),
posição do sub (canto reforça), caixa dutada com duto obstruído.

## "Voz apagada" / "embolado"

Corte 200–400 Hz; sub cobrindo alto demais (voz "cai" no sub: baixar o cruzamento); atraso entre
JBL e sub.

## "Distorce" / "raspa"

Parar. Volume baixo: ainda raspa? → falante danificado (medir resistência). Só alto? → ganho
do módulo acima do calibrado, limitador desligado, corte baixo demais para o falante, ou fonte
caindo (medir V DC com som alto: não pode cair abaixo de ~12 V).

## "Chiado" / "zumbido"

Loop de terra (P2 com carregador do notebook ligado), RCA solto, ganho do módulo alto demais
com o DSP baixo (estrutura de ganho invertida).
