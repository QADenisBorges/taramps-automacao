# Roteiro de regulagem do zero

Mesma ordem para casa e carro. Cada linha vira uma ou mais telas (Faça / Confira / Responda).
Não pule etapas: cada uma depende da anterior.

## 0 · Antes de tudo (com o módulo desligado ou sem RCA)

1. Backup: o que está no processador hoje foi salvo? (memórias exportadas no celular, ou foto
   das telas). Se ele vai resetar, o reset é feito por ele no app.
2. Reset escolhido: **"Resetar configurações de Áudio"** (mantém a senha BT) é o normal.
   "Padrões de Fábrica" apaga também a senha e os presets — só se ele quiser mesmo.
3. **Proteções primeiro** (regra de segurança 1): passa-alta das JBL, subsônico do sub,
   limitador ligado em cada saída, ganhos das saídas em −20 dB, master em 0. Rotas: JBL L = A,
   JBL R = B, sub = A+B (saídas 3 e 4 iguais).

## 1 · Inventário (só confirmar)

Falante em cada saída, impedância medida, ponte, chaves do módulo (F/HP/LP), ganho do módulo no
mínimo, fusíveis, tensão da fonte. Use o que já está em `setup-denis.md`; meça só o que faltar.

## 2 · Crossover

- JBL: passa-alta LR24 em 150 Hz (dia a dia). Para volume máximo de demonstração, até 200 Hz.
- Sub: subsônico BT24 40 Hz; passa-baixa LR24 no mesmo ponto do passa-alta das JBL.
- Carro: tweeter ≥ 3,5 kHz/24 dB (ou ficha); médio-grave de porta 80–100 Hz/24 dB; sub
  subsônico pela caixa.

## 3 · Estrutura de ganho (falantes soltos, multímetro)

1. Tom de 400 Hz (JBL) a 0 dBFS, fonte a 100 %: V AC na saída do módulo; ajuste o limitador da
   saída até o alvo (casa: 11 V hoje, ~12,6 V com o corte em 150 Hz e validação).
2. Tom de 50 Hz para o sub na ponte: alvo ≤ 28 V.
3. Confira +2 dB de ganho: a tensão não deve passar do alvo + 0,5 V (o limitador segura).
4. Religar os falantes (+ no vermelho), com a fonte fora da tomada.

## 4 · Fase, atraso e polaridade (volume baixo)

1. Polaridade das JBL: as duas iguais (mono no centro deve soar "na frente", não "espalhado").
2. Fase do sub: 0° e 180°, ouvindo um grave contínuo na posição de escuta; fica a que soar mais
   forte em 80–150 Hz.
3. Atraso: distâncias da posição de escuta a cada falante; atrasar os mais próximos (cm).

## 5 · EQ e nível (ouvido)

- Nível do sub em relação às JBL primeiro; depois EQ por corte (embolado 200–400 Hz, aspereza
  2,5–4 kHz).
- Uma ou duas bandas por vez, 1–3 dB; ouvir a mesma música de referência.

## 6 · Memórias

- `MUSICA HIGH`: volume e impacto (corte das JBL mais alto se precisar, limitador no teto
  validado), até 15 min, mão no módulo ao fim.
- `MUSICA ORIG`: dia a dia, equilíbrio.
- `FILME`: sub um pouco abaixo, voz clara (corte 100–250 Hz), sem reforço de grave.
- Salvar no app (Memória 1–3) e, se quiser, uma cópia no celular.

## 7 · Conferência final

Resistências de novo depois de uso forte (mesmos valores = sem dano), módulo morno, nenhum LED
de proteção, nada raspando em volume baixo.
