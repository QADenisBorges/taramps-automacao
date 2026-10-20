# Multímetro do Denis: VC830L (foto de 17/10)

Em toda tela que pede medida, mande a foto marcada (SendUserFile) e diga a posição em meia linha.
Pontas: preta no **COM**, vermelha no **VΩmA**, sempre (o furo 10A não é usado).

| Medida | Posição no botão | Foto |
|---|---|---|
| Tensão da fonte (`fonte_vdc`, ~13,8 V) | **V⎓ 20** (arco branco, esquerda) | `vdc-20.jpg` |
| Tons de 400 Hz e 50 Hz (`*_vac`, 11 a 28 V) | **V~ 200** (direita, ao lado do OFF) | `vac-200.jpg` |
| Resistência dos falantes (`*_ohm`, ~3,4 Ω) | **Ω 200** (arco laranja, embaixo) | `ohm-200.jpg` |

Na faixa 200 Ω, encoste as duas pontas uma na outra antes: o número que aparecer (ex.: 0,3) é o
do cabo; desconte da medida do falante.

O VC830L não é True RMS, o que não atrapalha com tom senoidal. A faixa de frequência do V AC
desse modelo costuma ser 40–400 Hz: 400 Hz fica no limite, onde a leitura pode sair um pouco
baixa. Isso entra no passo 3.14 (`multimetro_banda_400hz`); confirme no manual, se ele tiver.
