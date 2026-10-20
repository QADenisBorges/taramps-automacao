# Contexto de regulagem: memória SPL

**Status:** parâmetros dos JBLs definidos · sub e fonte pendentes · 2026-10-20

**Objetivo:** memória do processador dedicada a demonstrar a capacidade dos JBLs e do kit, tocando o mais alto possível com segurança, por no máximo 10 minutos (2 ou 3 músicas de 1 a 2 minutos). Não é memória de uso diário.

Este arquivo é a fonte de verdade da regulagem para quem for operar o sistema ou para o guardião do software. Qualquer valor alterado aqui exige remedir.

---

## 1. Equipamento

| Item | Dado | Observação |
|---|---|---|
| Processador | Taramps PRO 2.4BT + AUDIO | Faz todos os filtros, EQ e limiters |
| Módulo | Soundigital SD800.4 EVO, versão 4 Ω | ~200 W RMS por canal em 4 Ω (confirmar no manual). Chave de crossover em **F (full)** nos 4 canais: o processador define os cortes |
| Canal 1 | JBL Selenium 4TRMS80 (triaxial 4", 80 W RMS, 4 Ω) | Um falante por canal |
| Canal 2 | JBL Selenium 4TRMS80 (triaxial 4", 80 W RMS, 4 Ω) | Um falante por canal |
| Canais 3+4 | Sub em bridge | **PENDENTE**: modelo, RMS, impedância, bobina simples/dupla, ligação atual. Na versão 4 Ω do módulo, bridge em carga de 4 Ω costuma ficar abaixo da impedância mínima; conferir manual |
| Caixa dos JBLs | Selada, 3,5 L cada | Pequena para 4": sobe a ressonância para ~130-180 Hz e reduz excursão. Protege o cone. Recomendado amortecimento interno e vedação conferida |
| Sensibilidade JBL | ~87 dB (1 W / 1 m) | Dado do irmão 5TRMS80; confirmar na etiqueta |

## 2. Memórias do processador

| Memória | Fonte | Referência de nível | Alvo nos JBLs | Uso |
|---|---|---|---|---|
| Diária | Bluetooth (única entrada) | Tom 0 dBFS do celular chega em -13 dB; esse é o teto da fonte | 30 W → **11,0 V** em 400 Hz | Música no dia a dia |
| **SPL** | **PENDENTE** (entrada analógica, aparelho a definir) | Tom 0 dBFS da fonte, volume da fonte travado | 80 W → **17,9 V** em 400 Hz | Demonstração curta com playlist única |

A memória SPL **não pode ser copiada** da diária: sem o Bluetooth a folga de -13 dB deixa de existir e os mesmos níveis de saída entregariam cerca de 20× a potência.

## 3. Parâmetros da memória SPL: canais 1 e 2 (JBLs)

| Parâmetro | Valor | Motivo |
|---|---|---|
| Passa-alta | **170 Hz, 24 dB/oitava**, igual nos dois canais | Protege o cone de 4" contra excursão; fica na ressonância da caixa de 3,5 L ou acima. É a proteção principal |
| Passa-baixa | Desligado ou ≥ 18 kHz | O triaxial divide médio e tweeter internamente |
| Ganho / nível de saída | Até o multímetro marcar **17,9 V AC** em 400 Hz | 80 W em 4 Ω = limite térmico do falante. Módulo trabalha em 40% do canal |
| Limiter | Threshold no nível que gerou 17,9 V · ataque 1-5 ms · release 100-300 ms | Rede de segurança caso a fonte ou a música passe do previsto |
| EQ | Só cortes. Nenhum realce | Realce muda o teto medido. Se for inevitável, remedir na frequência realçada |
| Fase e delay | Iguais à memória diária até alinhar com o sub | Não afetam segurança |
| Chave do módulo | F (full) | Qualquer outra posição somaria filtro ao do processador |

### Potência e pressão sonora esperadas

| Momento | Por JBL | Os dois |
|---|---|---|
| Pico elétrico | 80 W | 160 W |
| Média elétrica (música a -7/-8 LUFS) | 8-13 W | 16-26 W |
| Pico acústico a 1 m, ambiente fechado | ~109 dB | ~111-112 dB |
| Média acústica a 1 m, ambiente fechado | ~100 dB | ~102-104 dB |

### Por que 80 W e não mais

- Limite térmico: 80 W é o RMS. Demonstração curta não acumula calor, mas o limite mecânico (curso do cone) não depende de tempo e é alcançado perto desse nível mesmo com passa-alta em 170 Hz.
- Passar para 160 W (25,3 V) renderia +3 dB com cone fora do curso. Não compensa.
- A sensação de potência vem da masterização alta (+2 a +3 dB de média) e do sub, não de watts acima do RMS.

## 4. Parâmetros da memória SPL: canais 3+4 (sub) — PENDENTE

Preencher após confirmar o sub e a ligação permitida pelo manual do módulo.

| Parâmetro | Valor previsto | Observação |
|---|---|---|
| Passa-baixa | a definir (típico 70-90 Hz, 24 dB/oitava) | |
| Subsônico | a definir: 5-10 Hz abaixo da afinação se dutada; 20-25 Hz se selada | |
| Ganho | Se o módulo for menor que o sub: regular pelo módulo, ~5% abaixo do máximo, em 50 Hz | LED de clip do módulo é a referência final |
| Limiter | Obrigatório, no nível medido | |

## 5. Procedimento de medição

Pré-requisitos: falantes **desconectados** do módulo; fonte da memória SPL no volume travado (marcar fisicamente); EQ plano; limiter desligado durante a medição inicial; multímetro em V AC.

1. Selecionar a memória SPL no processador.
2. Tocar o tom de **400 Hz a 0 dBFS** pela fonte da memória SPL.
3. Canal 1: subir o nível de saída até o multímetro marcar **17,9 V** nos bornes do canal 1 do módulo. Anotar a posição.
4. Repetir para o canal 2.
5. Ativar o limiter nos dois canais com threshold no nível atual. Confirmar: subir o nível de saída não passa de 17,9 V.
6. Aplicar passa-alta 170 Hz / 24 dB e eventuais cortes de EQ. Medir de novo em 400 Hz. Deve continuar 17,9 V.
7. Sub (quando definido): tom de **50 Hz a 0 dBFS**, medir no bridge, mesmo método.
8. Registrar tudo na tabela da seção 9.

## 6. Validação com os falantes ligados

1. Tocar a música mais pesada da playlist no volume travado.
2. Ouvir cada JBL de perto. Ruído mecânico ou "tec" no cone → subir o passa-alta em 20 Hz e repetir. **Não baixar a tensão.**
3. Observar o LED de clip do módulo durante a música inteira. Qualquer piscada → baixar 0,5 V no ganho daquele canal.
4. Ao final das músicas, encostar a mão no ímã. Morno é normal. Quente a ponto de incomodar → reduzir 1 dB de sonoridade nos arquivos.
5. Conferir vedação das caixas em volume alto (assobio = vazamento).

## 7. Playlist da memória SPL

Ainda a definir. Músicas de verdade, não tons ou faixas de teste. Critérios de entrada:

- 2 ou 3 músicas, 1 a 2 minutos cada, total abaixo de 10 minutos por demonstração.
- Escolha pelo que cada uma pede do kit: uma com grave seco (sub), uma com médios e vocal (JBLs), uma cheia.
- Masterização alta: cerca de **-7 a -8 LUFS** integrado, pico real **≤ -1 dBTP**, sem clipe no arquivo.
- Passa-alta de 30 Hz no arquivo (ou acima da afinação da caixa do sub).
- Mesma sonoridade entre as músicas para nenhuma surpreender.
- Proibido nessa memória depois de regulada: tom de teste, sweep, "teste de grave", streaming, shuffle, música fora da lista.

## 8. Premissas e bloqueios (regras para o guardião)

A regulagem só é válida enquanto todas estas condições valerem. Qualquer quebra exige remedir.

| Premissa | Se quebrar |
|---|---|
| Fonte da memória SPL é a definida, no volume travado | Outra fonte ou volume → teto desconhecido |
| JBLs são 4TRMS80 de 80 W RMS, 4 Ω, um por canal | Trocar falante → recalcular tensão |
| Chave do módulo em F nos 4 canais | Outra posição → filtro duplicado |
| Passa-alta ≥ 170 Hz nos canais 1 e 2 | Abaixo → risco mecânico mesmo em 17,9 V |
| Nenhum realce de EQ | Realce → teto medido deixa de valer |
| Limiter ativo no nível medido | Desligado → sem rede de segurança |
| Caixas seladas | Duto → excursão abaixo da afinação, mudar passa-alta |
| Duração ≤ 10 min por demonstração | Uso contínuo → limite térmico volta a importar |

Ações que a IA **nunca** libera nesta memória: baixar passa-alta abaixo de 170 Hz, desligar limiter, subir nível de saída acima da posição registrada, realçar EQ, trocar fonte de entrada, tocar tom de teste com falantes ligados.

## 9. Registro de medições

| Data | Memória | Canal | Frequência | Tensão alvo | Tensão medida | Posição de saída | Limiter | Passa-alta | Quem |
|---|---|---|---|---|---|---|---|---|---|
| | SPL | 1 | 400 Hz | 17,9 V | | | | 170 Hz / 24 dB | |
| | SPL | 2 | 400 Hz | 17,9 V | | | | 170 Hz / 24 dB | |
| | SPL | 3+4 | 50 Hz | pendente | | | | | |

## 10. Pendências

- [ ] Sub: modelo, RMS, impedância, bobina, ligação atual e o que o manual do SD800.4 EVO 4 Ω permite em bridge.
- [ ] Fonte da memória SPL: entrada do processador e aparelho. Travar e marcar o volume.
- [ ] Confirmar na etiqueta do 4TRMS80: 80 W RMS, 4 Ω.
- [ ] Confirmar no manual do módulo a potência por canal em 4 Ω.
- [ ] Definir e masterizar a playlist.
- [ ] Amortecimento interno e vedação das caixas de 3,5 L.
- [ ] Registrar o passa-alta atual da memória diária, para comparação.

## 11. Referências de cálculo

- Tensão alvo: V = √(P × Z). 80 W × 4 Ω → 17,9 V. 30 W × 4 Ω → 11,0 V.
- -13 dB em tensão = ×0,224; em potência = ÷20.
- +3 dB = dobro da potência; +10 dB = dobro da sensação de volume (10× a potência).
- Norma AES2 / IEC 60268-5: RMS = contínua (térmico); programa = 2× RMS; pico = 4× RMS. Ganho para música se regula no RMS.
- Fontes consultadas em 2026-10-20: listagens de revenda do 5TRMS80 (80 W RMS, 4 Ω, 87 dB, 76-20000 Hz) e do SD800.4 EVO (números divergem entre versões 2 Ω e 4 Ω; manual é a referência).
