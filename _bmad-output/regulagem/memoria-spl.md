# Contexto de regulagem: memória SPL

**Status:** recalculado do zero em 2026-10-20 a partir das medidas de 17 e 18/10 (seção 12) · JBLs em 17,9 V com LR4 em 200 Hz, confirmado por conta independente · sub em **28,6 V** (450 W confirmados em 27/10: o limite passa a ser o curso do cone e o duto, não o calor; ver seção 13) · fonte: Bluetooth serve · **Fb do duto medida: 51 Hz** → subsônico BW 24 dB em 40 Hz · módulo e sub confirmados pelo Denis (20/10)

**Fontes de verdade externas:** repositório `QADenisBorges/sound` — `componentes/alto-falantes.md`, `componentes/subwoofers.md`, `componentes/amplificadores.md`, `projetos/home-theater-2.1/montagem-2.1.md` e `.claude/skills/consultor-som/scripts/calc_som.py excursao`.

**Objetivo:** memória do processador dedicada a demonstrar a capacidade dos JBLs e do kit, tocando o mais alto possível com segurança, por no máximo 10 minutos (2 ou 3 músicas de 1 a 2 minutos). Não é memória de uso diário.

Este arquivo é a fonte de verdade da regulagem para quem for operar o sistema ou para o guardião do software. Qualquer valor alterado aqui exige remedir.

---

## 1. Equipamento

| Item | Dado | Observação |
|---|---|---|
| Processador | Taramps PRO 2.4BT + AUDIO | Faz todos os filtros, EQ e limiters |
| Módulo | SounDigital **SD800.4 EVO** (não é a EVO6), marcação **BRIDGE 4Ω** (confirmado pelo Denis em 20/10) | Tabela usada: 4 × 132 W em 4 Ω; ponte 400 W em 4 Ω (mínimo em ponte: 4 Ω; ponte em 2 Ω proibida). Coerente com o medido: 26 V por canal sem carga (14/10). Chave de crossover em **F (full)** nos 4 canais: o processador define os cortes |
| Canal 1 | JBL Selenium 4TRMS80 (triaxial 4", 80 W RMS, 4 Ω) | Um falante por canal |
| Canal 2 | JBL Selenium 4TRMS80 (triaxial 4", 80 W RMS, 4 Ω) | Um falante por canal |
| Canais 3+4 | Sub **Protech Light 8" 4 Ω** (modelo e 450 W RMS confirmados pelo Denis em 27/10; R$ 390 no site da Protech), em ponte (sub + no CH4 ⊕, sub − no CH3 ⊖) | Ficha completa na seção 13: Re 3,7 Ω, Fs 46 Hz, Qts 0,52, Vas 10 L, Bl 13, **Xmax 8 mm**, Sd 240 cm², 84 dB/1 W. **3,7 a 4,0 Ω medidos** (17/10 e 15/10): bobina de 4 Ω. Caixa com duto, ≈28 L, **Fb medida 51 Hz** (20/10) |
| Caixa dos JBLs | BS-369, selada, ~3,5 L cada | Fc ≈ 124 Hz, Qtc ≈ 0,81-0,90 (Sound). Reduz excursão e protege o cone. Recomendado amortecimento interno e vedação conferida |
| JBL 4TRMS80 (ficha impressa, Sound) | 80 W RMS / 160 W pico · 4 Ω (Re 3,4 Ω) · 87 dB declarado (~85,8 dB efetivo) · Fs 98 Hz · Vas 2,06 L · Sd 64 cm² · **Xmax 1,75 mm de pico** · corte mínimo recomendado pela ficha: 100 Hz a 24 dB/oit | O limite real do falante é o curso do cone, não o calor |

## 2. Memórias do processador

| Memória | Fonte | Referência de nível | Alvo nos JBLs | Uso |
|---|---|---|---|---|
| Diária | Bluetooth (única entrada) | BT a 100 % chega ~13 dB abaixo do 0 dB interno (medido em 18/10) | Limitador em −11 dB → **11,7 V** medidos · passa-alta LR 24 dB/oit em 150 Hz (desde 18/10) | Música no dia a dia |
| **SPL** | **Bluetooth do celular a 100 %** (seção 12.5) | Ganho de saída ≈ +6 dB para o BT chegar ao limitador | 80 W → **17,9 V** · passa-alta **LR 24 dB/oit em 200 Hz** | Demonstração curta com playlist única |

A memória SPL **não pode ser copiada** da diária: o corte, o ganho e os limitadores mudam, então os níveis de saída precisam ser remedidos.

### Por que 11 V é o seguro da memória diária (origem: repositório Sound, revisão técnica de 09/10/2026)

O 11 V não vem de limite térmico. Vem de **excursão do cone**: Xmax de 1,75 mm de pico, caixa de 3,5 L com Fc ≈ 124 Hz e passa-alta Butterworth de só 12 dB/oit em 120 Hz. Nessa combinação a pior frequência é 122 Hz e o Xmax é atingido com apenas ~12 W. O alvo original era 15,5 V (60 W); a revisão achou que a excursão tinha sido calculada em valor eficaz e comparada com o Xmax (que é pico), o que dobrava a potência segura, e baixou para 11 V (30 W). Mesmo em 11 V, tom contínuo no máximo em 122 Hz dá ~2,8 mm (1,6 × Xmax); foi aceito como pior caso com tom, não com música. Regra herdada: o passa-alta das saídas 1/2 **nunca é removido nem baixado** de 120 Hz BW2.

**Sobre a referência de -13 dB do Bluetooth:** não consta no Sound, mas foi **medida em 18/10**: tom de 400 Hz a 0 dBFS pelo BT, com a JBL em −5 dB, deu 4,8 V no CH1 e 4,7 V no CH2; dois celulares deram 11,1 V iguais (`analise-nivel-2026-10-18.md`).

## 3. Parâmetros da memória SPL: canais 1 e 2 (JBLs)

| Parâmetro | Valor | Motivo |
|---|---|---|
| Passa-alta | **Linkwitz-Riley 24 dB/oitava em 200 Hz**, igual nos dois canais | Proteção principal. Com esse filtro, 80 W fica em 0,8 × Xmax (tabela abaixo). O tipo importa: Butterworth 24 dB em 170 Hz daria 1,6 × Xmax em 80 W. Nunca abaixo de 120 Hz BW2 (trava do Sound) |
| Passa-baixa | Desligado ou ≥ 18 kHz | O triaxial divide médio e tweeter internamente |
| Passa-baixa do sub nesta memória | LR 24 dB/oit em **200 Hz** (saídas 3 e 4 iguais) | Acompanha o passa-alta dos JBLs para não abrir buraco entre 120 e 200 Hz. Na diária fica em 120 Hz |
| Ganho / nível de saída | **≈ +6 dB**, para o BT a 100 % chegar ao limitador | 80 W em 4 Ω = limite térmico do falante e, com LR4 em 200 Hz, dentro do Xmax. O canal do módulo ceifa por volta de 23 V (132 W em 4 Ω), 2,2 dB acima |
| Limiter | **−7,0 dB** (0,89 V no app) → ~17,5 V · ataque **5 ms** · release 200 ms | Quem segura o teto. O app mostra o limiar em volts pela fórmula 2,0 × 10^(dB/20). Fica 0,4 dB abaixo de 17,9 V porque o limitador deixa vazar (+2 dB de ganho → +0,5 dB na saída, medido em 17/10). Conta na seção 12.4 |
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

### Pico e média: os dois acontecem ao mesmo tempo

Com o ganho em 17,9 V, o JBL recebe **80 W nos picos** (transientes, ataques, trechos fortes) e **10 a 20 W de média** ao longo da música. Nenhuma música entrega média igual ao pico; isso seria um tom contínuo. O pico move o cone e define o volume ouvido. A média aquece a bobina.

| Música | Média por JBL | Média nos dois |
|---|---|---|
| Masterização normal, dinâmica | 5 a 8 W | 10 a 16 W |
| Masterização alta, -7 a -8 LUFS (planejada) | 8 a 13 W | 16 a 26 W |
| Trechos mais densos dessas músicas | até 20 W | até 40 W |

Nuance da norma: os 80 W RMS são medidos com ruído rosa de 6 dB de fator de crista, ou seja, 80 W de média com picos de 320 W. Termicamente o JBL tolera 80 W de média. Em 17,9 V com música, a bobina trabalha a cerca de um quarto disso.

### Excursão de pico × Xmax (calc_som.py do Sound, caixa 3,5 L, Xmax 1,75 mm)

| Passa-alta | 11 V (30 W) | 17,9 V (80 W) | 22 V (120 W) |
|---|---|---|---|
| BW 12 dB em 120 Hz (memória diária) | 1,6 × Xmax | 2,6 × Xmax | — |
| BW 24 dB em 170 Hz | 1,0 × Xmax | 1,6 × Xmax | — |
| LR 24 dB em 170 Hz | 0,7 × Xmax | 1,1 × Xmax | — |
| **LR 24 dB em 200 Hz (memória SPL)** | 0,5 × Xmax | **0,8 × Xmax** | 1,0 × Xmax |

Leitura: com LR4 em 200 Hz, a memória SPL em 80 W é mecanicamente mais segura que a diária em 30 W. O teto de 22 V do ajuste fino coincide com o Xmax.

### Por que 80 W de pico e não mais

- O que limita um 4" não é calor, é curso do cone. Com LR4 em 200 Hz, 17,9 V entrega os picos no RMS dentro do Xmax e deixa a média folgada.
- Passar para 160 W (25,3 V) renderia +3 dB com cone provavelmente fora do curso. Não compensa.
- A sensação de potência vem da masterização alta (+2 a +3 dB de média) e do sub, não de watts acima do RMS.

### Ajuste fino opcional após a validação

> **Não sustentado pelo recálculo de 20/10 (seção 12).** Acima de 17,9 V a JBL passa da potência RMS da ficha (80 W). Isso só se justifica usando potência de pico como referência, o que o recálculo não faz. O teto continua sendo 17,9 V. A tabela abaixo fica só como registro.

Subir além de 17,9 V é opção técnica, não erro, desde que validada por escuta. Só depois da seção 6 passar limpa e com folga:

| Ganho | Pico por JBL | Média | Condição |
|---|---|---|---|
| 17,9 V | 80 W | 10 a 20 W | Padrão. Ponto de partida obrigatório |
| 18 a 22 V | 80 a 120 W | 12 a 25 W | +0 a 1,8 dB. Só se o teste de escuta não acusar ruído mecânico nem clip, com LR4 em 200 Hz. Em 22 V a excursão chega exatamente ao Xmax |
| 25,3 V | 160 W | 20 a 40 W | Teto de programa da norma. Não recomendado para 4" |

Método: subir em passos de **1 V**, tocando a música mais pesada da playlist no volume travado a cada passo, até o primeiro ruído mecânico no cone ou piscada do LED de clip. Voltar 1 V. Esse é o máximo real do JBL nessa caixa; o esperado é entre 18 e 22 V. Reajustar o threshold do limiter para o novo valor e anotar no registro (seção 9). Acima de 22 V não subir, independentemente do teste.

## 4. Parâmetros da memória SPL: canais 3+4 (sub) — valores previstos, dependem da verificação prévia

| Parâmetro | Valor previsto | Observação |
|---|---|---|
| Ligação | Ponte CH4 ⊕ / CH3 ⊖, **só se o sub fechar em ≥ 4 Ω e a versão do módulo permitir ponte nessa carga** | Saídas 3 e 4 do processador sempre idênticas (fase, atraso, filtros, nível, EQ, limiter). Fase invertida em uma só cancela a ponte |
| Passa-baixa | LR 24 dB/oit em 200 Hz | Acompanha os JBLs nesta memória |
| Subsônico | **BW 24 dB/oit em 40 Hz** = 0,78 × Fb (Fb medida: 51 Hz, 20/10) | Regra: 0,75 a 0,8 × Fb → 38 a 41 Hz. Abaixo da Fb o cone fica sem controle do duto |
| Ganho | **≈ +5 dB** para o BT (seção 12.5), sem realce de EQ. Sub medido em 3,7 Ω (17/10) = 4 Ω, ponte liberada | 40 V é o ponto em que a ponte ceifa (400 W em 4 Ω), não um alvo. Teto: **28,6 V** (200 W) (decisão do Denis em 20/10: potência do sub sem prova → vale o seguro de 200 W; 34 V só se a compra confirmar 450 W) |
| Limiter | **−9,0 dB** (0,71 V no app) → **28,6 V medidos** (18/10) · com 450 W confirmados: −7,5 dB (0,84 V) → ~34 V · ataque **10 ms** · release 200 ms · igual nas saídas 3 e 4 | Medido em 18/10: −6 dB → 40,6 V; −9 dB → 28,6 V; −12 dB → 20 V |
| Fase | 0° nas duas (testar 180° nas duas juntas) | |

## 5. Procedimento de medição

### 5.0 Verificações prévias obrigatórias (antes de qualquer medição)

1. **Versão do módulo.** Etiqueta lateral/inferior ("4 OHMS" indica versão 4 Ω), marcação da ponte no painel ("BRIDGE 4Ω" indica versão 2 Ω; "BRIDGE 8Ω" indica versão 4 Ω), nota fiscal ou anúncio. Registrar.
2. **Impedância do sub.** Multímetro em Ω nos fios que saem da caixa, sub desligado do módulo:

| Leitura | Significa | Ponte |
|---|---|---|
| 3 a 3,6 Ω | 4 Ω | Só na versão 2 Ω do módulo (400 W, 40 V) |
| 6,5 a 7,2 Ω | 8 Ω (dupla em série) | Nas duas versões (~264 W, ~46 V) |
| 1,5 a 1,8 Ω | 2 Ω (dupla em paralelo) | **Nunca.** Um canal só (200 W, 20 V) ou religar em série |

3. **Tipo da caixa do sub.** Varredura lenta de 25 a 60 Hz em volume baixo, lanterna no cone. Cone quase parado numa frequência = duto, e essa é a Fb. Define o subsônico (seção 4).
4. Conferir a chave do módulo em **F** nos 4 canais e o GAIN do módulo na posição da memória diária (o ajuste é pelo nível de saída do processador).

### 5.1 Medição (método do guia do Sound, adaptado à memória SPL)

Pré-requisitos: falantes **desconectados** do módulo; fonte da memória SPL no volume travado (marcar fisicamente); níveis de saída em 0 dB; **filtros, EQ e limiter desligados**; multímetro em V AC.

1. Selecionar a memória SPL no processador.
2. Tocar o tom de **60 Hz a 0 dBFS** pela fonte da memória SPL (mesma frequência do guia do Sound; com filtros desligados qualquer frequência serve, e 60 Hz é onde o multímetro é mais preciso).
3. Canal 1: subir o nível de saída até o multímetro marcar **17,9 V** nos bornes do canal 1 do módulo. Anotar a posição.
4. Repetir para o canal 2.
5. Sub: tom de 60 Hz, medir na ponte CH4 ⊕ / CH3 ⊖, alvo conforme a seção 4 (saídas 3 e 4 sempre juntas). LED de saída 3 ou 4 aceso = soma A+B ceifando: baixar as duas.
6. Ativar o limiter em cada saída com threshold no nível atual (no app o limiar aparece em volts na saída do processador: 0 dB = 2,0 V; 2,1 V é o que o app mostra em OFF). Confirmar: subir 2 dB o nível de saída, o multímetro não passa do alvo.
7. Aplicar os filtros (JBLs: LR4 200 Hz; sub: LR4 200 Hz + subsônico) e eventuais cortes de EQ.
8. Conferência: tom de **400 Hz** nos canais 1 e 2 deve marcar ~17,5 a 17,9 V (LR4 em 200 Hz tira ~0,5 dB em 400 Hz). Tom de 50 Hz na ponte deve marcar o alvo do sub.
9. Registrar tudo na tabela da seção 9. Salvar e exportar a memória.

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
| Passa-alta LR 24 dB/oit em 200 Hz nos canais 1 e 2 | Filtro mais suave ou mais baixo → excursão acima do Xmax em 17,9 V (tabela da seção 3) |
| Saídas 3 e 4 idênticas | Diferença → ponte cancela ou módulo em carga errada |
| Nenhum realce de EQ | Realce → teto medido deixa de valer |
| JBLs em no máximo 17,9 V (80 W RMS) e sub em no máximo 28,6 V; 34 V só com 450 W confirmados (seção 12) | Acima disso → JBL além do RMS da ficha; sub perto do ceifamento da ponte (40 V) |
| Limiter ativo no nível medido | Desligado → sem rede de segurança |
| Caixas seladas | Duto → excursão abaixo da afinação, mudar passa-alta |
| Duração ≤ 10 min por demonstração | Uso contínuo → limite térmico volta a importar |

Ações que a IA **nunca** libera nesta memória: baixar ou suavizar o passa-alta dos canais 1 e 2 (mínimo absoluto herdado do Sound: 120 Hz BW2), desligar limiter, alterar só uma das saídas 3/4, subir nível de saída acima da posição registrada, realçar EQ, trocar fonte de entrada, tocar tom de teste com falantes ligados.

## 9. Registro de medições

| Data | Memória | Canal | Frequência | Tensão alvo | Tensão medida | Posição de saída | Limiter | Passa-alta | Quem |
|---|---|---|---|---|---|---|---|---|---|
| | SPL | 1 | 60 Hz | 17,9 V (ajuste fino: até 22 V) | | | (V no app) | LR4 200 Hz | |
| | SPL | 2 | 60 Hz | 17,9 V (ajuste fino: até 22 V) | | | (V no app) | LR4 200 Hz | |
| | SPL | 3+4 ponte | 50 Hz | 28,6 V (34 V se 450 W) | | | (V no app) | LP LR4 200 Hz + subsônico | |

## 10. Pendências

- [x] Verificação prévia 5.0: módulo SD800.4 EVO com BRIDGE 4Ω (não EVO6), sub Protech Speakers 8" de 3,7 a 4,0 Ω, caixa com duto e Fb de 51 Hz (20/10).
- [x] Nível real do Bluetooth: ~13 dB abaixo do 0 dB interno (medido em 18/10).
- [x] Fonte da memória SPL: Bluetooth do celular a 100 % (seção 12.5).
- [x] Fb do duto do sub: **51 Hz** (medida pelo Denis em 20/10, gerador a −25 dB, lanterna no cone; região confirmada de 5 em 5 Hz e ponto exato de 1 em 1 Hz). Confere com a conta pelas medidas da caixa (51 a 58 Hz). Subsônico: 40 Hz BW24.
- [ ] Soldar as emendas da linha de 40 A, ou instalar o porta-fusível midi. Decisão do Denis (20/10): fica para **depois de configurar** a memória SPL. A configuração (tons sem falantes, corrente baixa) pode ser feita antes. A demonstração em volume máximo espera a solda; até lá, depois de cada uso alto, tocar na emenda e no porta-fusível (morno é normal; quente, parar).
- [x] Prender o duto do sub que vibra em ~35 Hz: feito pelo Denis em 20/10 ("subwoofer prendido"). Conferido com o gerador a −30 dB em 35, 45, 55 e 70 Hz: sem barulho.
- [ ] Confirmar se o EQ entra antes do limitador (seção 12.5).
- [ ] Fase do sub no corte de 200 Hz: começou em **0°** (20/10, decisão do Denis); comparar de ouvido com 180° e ficar com a que soar mais cheia. **Sem atrasos:** regra do Denis de 27/10, nas memórias de música L e R tocam igual e os atrasos ficam em 0; a compensação por distância (R 100 cm, sub 80 cm) é só da FILME.
- [ ] Definir e masterizar a playlist.
- [x] Amortecimento interno e vedação das caixas: silicone e fibra feitos, caixas instaladas (confirmado pelo Denis em 20/10). Com a fibra, o volume efetivo sobe ~10 a 20 % (até ~4,2 L): com LR4 em 200 Hz e 17,9 V, a excursão fica em 0,79 a 0,80 × Xmax, sem mudança no teto.
- [ ] Registrar o passa-alta atual da memória diária, para comparação.
- [x] Potência e modelo do sub: Protech Light 8" 4 Ω, 450 W RMS (confirmado pelo Denis em 27/10). Ficha e simulação na seção 13.
- [ ] Decisão do Denis (seção 13.4): manter o sub em 28,6 V ou passar a 34 V com o subsônico em 43 Hz.
- [x] FILME (seção 13.3): reforço de 45 Hz tirado, subsônico em 45 Hz BW24 e ataque do sub em 15 ms. Aprovado de ouvido pelo Denis e salvo na memória 3 em 27/10 19h00 (`estado/backups/2026-10-27_1900.json`).

## 11. Referências de cálculo

- Tensão alvo: V = √(P × Z). 80 W × 4 Ω → 17,9 V. 30 W × 4 Ω → 11,0 V.
- -13 dB em tensão = ×0,224; em potência = ÷20.
- +3 dB = dobro da potência; +10 dB = dobro da sensação de volume (10× a potência).
- Norma AES2 / IEC 60268-5: RMS = contínua (térmico); programa = 2× RMS; pico = 4× RMS. Ganho para música se regula no RMS.
- Excursão: `calc_som.py excursao --re 3.4 --bl 4.1 --sd 64 --fs 98 --vas 2.06 --qms 3.19 --volume 3.5 --xmax 1.75 --filtro lr4 --corte 200 --tensao 17.9` (repositório Sound).
- Fontes consultadas em 2026-10-20: listagens de revenda do SD800.4 EVO (números divergem entre versões 2 Ω e 4 Ω; manual é a referência). A JBL é a **4TRMS80**; a ficha impressa (foto) é a referência.

## 12. Cálculo do zero (2026-10-20)

Feito sem partir dos limites já estabelecidos nem do guardião. Origem de cada número: **[M]** medido com o multímetro em 17 e 18/10 · **[E]** especificação (ficha ou manual) · **[C]** conta · **[≈]** estimativa. Nenhuma conta usa potência de pico: os tetos são em W RMS, que é a tensão RMS de um tom contínuo.

### 12.1 Dados de partida

| Item | Valor | Origem |
|---|---|---|
| Fonte nos bornes do módulo, sem carga | 13,89 V DC | [M] 17/10 |
| BOB 60A | 40 A contínuos; 60 A por 4 s | [E] |
| JBL L e R, resistência | 3,6 Ω (4,9 lido − 1,3 das pontas) | [M] 18/10 |
| JBL 4TRMS80 | 80 W RMS, 4 Ω, Re 3,4, Fs 98 Hz, Qes 0,81, Qms 3,19, Vas 2,06 L, Sd 64 cm², Bl 4,1, Xmax 1,75 mm de pico | [E] ficha impressa |
| Caixa BS-369 | Selada, 3,5 L (faixa 3,2 a 3,9 L) | [≈] medidas por foto e régua |
| Sub, resistência | 3,7 Ω (5,0 lido − 1,3) → bobina de 4 Ω | [M] 17/10 |
| Sub Protech 8" | 450 W RMS (sem prova), 84 dB, Xmax 8 mm; Bl, Qts e Vas desconhecidos | [E] loja e informação do Denis; sem etiqueta |
| SD800.4 EVO por canal | 132 W em 4 Ω a 12,6 V e 1 % de distorção → ceifa por volta de **23 V** | [E] manual |
| SD800.4 EVO em ponte | 400 W em 4 Ω → ceifa por volta de **40 V**; ponte em 2 Ω proibida | [E] manual |
| Ganho do módulo | ×19,5 por canal (−11 dB = 0,564 V → 11,0 V); ×40,5 em ponte (−6 dB = 1,0 V → 40,6 V) | [M] 17 e 18/10 |
| Escala do limitador | V na saída do processador = 2,0 × 10^(dB/20) | [M] 11 pontos lidos no app |
| Bluetooth a 100 % | ~13 dB abaixo do 0 dB interno | [M] 18/10 |
| Vazamento do limitador | Ganho +2 dB acima do limiar → +0,5 dB na saída (11,0 → 11,6 V) | [M] 17/10 |

Limites das medidas: todas as tensões foram medidas **sem carga** (falantes soltos). Com o falante ligado a tensão cai um pouco. O multímetro mostra RMS e **não enxerga ceifamento**.

### 12.2 JBL: o limite é o curso do cone

Modelo eletromecânico com fonte de tensão, script `calculo_excursao_jbl.py` nesta pasta:

x_pico = (Bl · V_rms · √2 / Re) / (Mms·s² + (Rms + Bl²/Re)·s + 1/Cms + ρc²Sd²/Vb) × filtro

Da ficha: Cms = Vas / (ρc²Sd²) = 355 µm/N; Mms = 1 / ((2π·Fs)²·Cms) = 7,43 g. O Bl da ficha (4,1) não fecha com o Qes (dá 4,38). A conta roda com os dois e com a caixa de 3,2 a 3,9 L, e o resultado muda menos de 3 %. Na caixa de 3,5 L: Fc ≈ 124 Hz, Qtc ≈ 0,81 a 0,90.

| Passa-alta | Pior frequência | Tensão em que o cone chega ao Xmax | Excursão com 11 V | Excursão com 17,9 V |
|---|---|---|---|---|
| Nenhum | ~65 a 75 Hz | 4,2 V (4,5 W) | 2,6 × Xmax | 4,2 × Xmax |
| BW 12 dB em 120 Hz | 122 Hz | 7,0 V (12 W) | 1,6 × Xmax | 2,6 × Xmax |
| BW 24 dB em 150 Hz | 156 Hz | 9,2 V (21 W) | 1,2 × Xmax | 1,9 × Xmax |
| **LR 24 dB em 150 Hz (memórias de hoje)** | 161 Hz | **12,9 V (42 W)** | 0,85 × Xmax | 1,4 × Xmax |
| LR 24 dB em 180 Hz | 183 Hz | 18,0 V (81 W) | 0,61 × Xmax | 0,99 × Xmax |
| **LR 24 dB em 200 Hz** | 200 Hz | **22,2 V (123 W)** | 0,50 × Xmax | **0,81 × Xmax** |
| LR 24 dB em 250 Hz | 245 Hz | 35 V (307 W) | 0,31 × Xmax | 0,51 × Xmax |

O tipo de filtro importa tanto quanto a frequência: BW4 em 180 Hz protege menos que LR4 em 150 Hz.

Teto térmico da ficha: 80 W RMS em 4 Ω → V = √(80 × 4) = **17,9 V** [C]. O canal do módulo ceifa por volta de 23 V, 2,2 dB acima.

**Teto da JBL: 17,9 V, só com LR 24 dB em 200 Hz** (0,81 × Xmax). É o corte mais baixo que deixa folga no Xmax com a potência RMS cheia. Subir mais o corte não aumenta o teto, porque aí quem limita é o calor (80 W). Para comparar: a memória diária (LR4 em 150 Hz, 11,7 V) fica em 0,90 × Xmax.

Sensibilidade pelos dados da ficha: η0 = 4π²/c³ × Fs³ × Vas / Qes = 0,234 % → **85,8 dB/1 W/1 m** [C]. A ficha declara 87 dB.

### 12.3 Sub: o limite é o módulo

- A ponte entrega 400 W em 4 Ω [E] e ceifa por volta de 40 V. A potência do sub **não tem prova** (450 W informados, sem etiqueta, caixa fechada). As medidas da caixa não dizem a potência do falante: quem faz caixa sob encomenda dimensiona pelo tamanho (8"), não pelos watts.
- Os 40,6 V medidos com o limitador em −6 dB estão no ponto de ceifamento. O multímetro não mostra isso e a medida foi sem carga.
- **Teto em uso: 28,6 V** (≈200 W em 4 Ω; √(200 × 4) = 28,3 V), com o limitador em −9 dB, já medido em 18/10. (decisão do Denis em 20/10: potência do sub sem prova → vale o seguro de 200 W; 34 V só se a compra confirmar 450 W).
- Se a compra confirmar 450 W: **34 V**, ou seja, 34² / 4 = **289 W** [C], 1,4 dB abaixo do ceifamento da ponte. A margem cobre a queda de tensão com o falante ligado e o vazamento do limitador no ataque.
- **A excursão do sub não dá para calcular:** faltam Bl, Qts e Vas, e a **Fb** (frequência de sintonia do duto) é desconhecida.
  - Na Fb, quem faz o som é o ar do duto e o cone quase não se mexe.
  - Abaixo dela, o cone fica solto e pode passar do Xmax.
  - O subsônico tem de cortar abaixo da Fb (0,75 a 0,8 × Fb).
  - **Fb medida em 20/10: 51 Hz** (gerador a −25 dB, lanterna no cone: 50 Hz o mais parado de 40 a 65 Hz; 51 Hz o mais parado de 48 a 52 Hz). Bate com a conta pela caixa (≈28 L, duto de 7 × 8 a 10 cm: 51 a 58 Hz).
  - **Subsônico: BW 24 dB em 40 Hz = 0,78 × Fb.** É o mesmo valor das memórias de hoje, agora confirmado pela medida.
  - Duto: 38,5 cm² de área. Com o sub em 34 V, o ar do duto deve soprar audivelmente perto de 51 Hz a partir de ~103 a 106 dB a 1 m [≈]. Não danifica; se incomodar, corte de EQ de 2 a 3 dB em 51 Hz.
  - Para que potência a caixa foi feita [≈] (análise de 20/10): volume e Fb dependem dos parâmetros do falante, não da potência, e a caixa não traz ficha. O que indica potência é o duto: com 84 dB/1 W/1 m e o som saindo pelo duto na Fb, a velocidade de pico do ar fica em ~27 m/s com 200 W, ~32 m/s com 289 W e ~40 m/s com 450 W. O sopro começa por volta de 17 a 25 m/s, ou seja, **~80 a 170 W perto de 51 Hz**. Junto com o MDF de 15 mm (caixas para potência alta usam 18 mm ou mais, com reforços), a caixa é de classe **~200 W**, não 450 W. O limite é ruído (sopro e parede vibrando), não dano ao falante: o teto de 34 V continua valendo, e o corte de EQ em 51 Hz passa a ser a primeira opção se o sopro aparecer.

### 12.4 Potência total e fonte

| Saída | Teto RMS | Tensão | Quem limita |
|---|---|---|---|
| JBL L | 80 W | 17,9 V | Potência RMS da ficha + Xmax (com LR4 em 200 Hz) |
| JBL R | 80 W | 17,9 V | Idem |
| Sub (ponte) | 204 W | 28,6 V | Potência do sub sem prova: seguro de 200 W (com 450 W confirmados: 289 W, 34 V) |
| **Total** | **~365 W RMS** (~450 W com 450 W confirmados) | | |

- Tom contínuo nos três ao mesmo tempo: 365 W ÷ 0,8 de eficiência ≈ 455 W ÷ 13,8 V ≈ **33 A**, abaixo dos 40 A contínuos da BOB (com 34 V no sub: ~41 A, no limite). Só acontece com tom de teste.
- Com música passando pelo limitador [≈], a média fica em 20 a 25 % do teto: ~90 W, ~8 A. A fonte sobra.
- Durante a demonstração, a tensão nos bornes do módulo deve ficar **≥ 12,6 V**.

Limiares do limitador [C]:
- JBL: dB = 20·log(17,9 / (19,5 × 2,0)) = **−6,8 dB**. Com a margem para o vazamento, usar **−7,0 dB** (0,89 V no app → ~17,5 V).
- Sub: **−9,0 dB** (0,71 V no app) → 28,6 V, medido em 18/10. Com 450 W confirmados: dB = 20·log(34 / (40,5 × 2,0)) = −7,5 dB (0,84 V no app).

### 12.5 Ganho para o Bluetooth e risco de ceifamento

- JBL: o BT a 0 dBFS com ganho −5 dB deu 4,8 V [M]. Para 17,9 V: −5 + 20·log(17,9 / 4,8) = **+6,4 dB**. Usar **≈ +6 dB**.
- Sub: o tom de 50 Hz em nível de BT (−13 dB), com ganho efetivo +1 dB, deu 18,5 V [M]. Para 28,6 V: +1 + 20·log(28,6 / 18,5) = **+4,8 dB**, ou seja, **≈ +5 dB**, sem realce de EQ (para 34 V: +6,3 dB).
- Dentro do processador: −13 dB + 6 dB = −7 dB. Sobra margem para o 0 dB interno, então o Bluetooth serve de fonte e **não precisa de entrada analógica**.
- Pelo teste de 1 kHz com +2 dB de EQ (deu 11,7 V e não 13,9 V), o EQ parece entrar **antes** do limitador. É uma inferência e precisa ser confirmada.

Riscos de ceifamento:
1. Gerador do app em 0 dB com ganho acima de 0 → o sinal passa do 0 dB interno e o processador ceifa por dentro. Para medir, usar ganho 0.
2. Ataque lento do limitador → o pico do transiente passa. Por isso o ataque de 5 ms (JBL) e 10 ms (sub) e a margem no sub.
3. Medidas sem carga → conferir a fonte durante a música (≥ 12,6 V).
4. Faixa do multímetro em V~ [≈]: medir em 50 a 400 Hz, nunca em 1 kHz.

### 12.6 Ganho em volume e SPL esperado [≈]

| Saída | Hoje (MUSICA HIGH) | Memória SPL | Diferença |
|---|---|---|---|
| JBL | 11,4 V | 17,9 V | +3,9 dB |
| Sub | 28,6 V (teto) | 28,6 V (34 V se 450 W) | 0 dB (+1,5 dB se 450 W) |

- JBL: 85,8 dB + 10·log(80) = ~105 dB a 1 m. No lugar de escuta (2,2 e 3,2 m), somando as duas: **~99 a 100 dB**.
- Sub: 84 dB + 10·log(204) = ~107 dB a 1 m; a 2,4 m, **~99,5 dB** (com 34 V: ~101 dB). No grave, a sala costuma somar +6 a +10 dB.

### 12.7 Conferência antes de usar (multímetro em V~ 200, falantes desligados)

1. Ganho em 0 dB, gerador a 0 dB, 400 Hz nas JBL: **17,0 a 17,5 V**. O LR4 em 200 Hz tira 0,5 dB em 400 Hz.
2. Gerador a 50 Hz na ponte (CH4 ⊕ / CH3 ⊖): **~28,6 V** (34 V só com 450 W confirmados).
3. Com os falantes ligados e música, V⎓ 20 nos bornes do módulo: **≥ 12,6 V**.
4. Refazer a fase do sub com o corte em 200 Hz (hoje está em 180°, escolhida com corte de 150 Hz).

Confirmado em 20/10: módulo SD800.4 EVO com BRIDGE 4Ω (não EVO6), sub Protech Speakers 8" e Fb de 51 Hz. Continua sem ficha: a potência do sub (450 W informados, sem prova: vale o seguro de 200 W até a nota ou o pedido da compra confirmar) e os parâmetros Bl, Qts e Vas do sub.

### 12.8 Configuração salva e medida (20/10/2026, memória 1 "SPL")

Calibrada pelo **Bluetooth do celular a 100 %**, com arquivos de tom a 0 dBFS (`Música/TestesSPL` no celular), falantes soltos, multímetro em V~ 200. Salva no DSP às 13h22 (`memoria.py salvar 1 "SPL"`, guardião aprovado, nome relido).

| | JBL L (saída 1) | JBL R (saída 2) | Sub (saídas 3 e 4, ponte) |
|---|---|---|---|
| Rota | B | A | A+B |
| Corte | Passa-alta LR24 em 200 Hz | idem | Subsônico BW24 em 40 Hz + passa-baixa LR24 em 200 Hz |
| Ganho | +6,0 dB | +6,0 dB | +2,6 dB |
| Limitador | −7,0 dB, ataque 5 ms, liberação 200 ms | idem | −9,0 dB, ataque 10 ms, liberação 200 ms |
| Fase / atraso | 0° / 0 | 0° / 0 | **0°** / 0 (pendente: comparar com 180°; atraso fica em 0, regra de 27/10) |
| EQ | plano | plano | plano |
| **Medido (tom a 0 dBFS pelo BT)** | **17,7 V** (400 Hz) → ~78 W, 0,80 × Xmax | **17,6 V** → ~77 W | **27,5 V** (80 Hz) → ~190 W em 4 Ω |
| Calculado | 17,9 V | 17,9 V | 28,3 a 28,6 V (seguro de 200 W) |

Conferências feitas no caminho:
- Limitador das JBL segura: com −8 dB, +2 dB de ganho levaram a saída de 15,7 para 16,2 V (+0,5 dB).
- Sub: com o ganho em +0,2 dB, o tom de 80 Hz a 0 dBFS deu 21,7 V (linear); +2,4 dB levaram a 27,5 V em vez de 28,6 V, ou seja, o limitador de −9 dB começa a segurar 0,3 dB abaixo do alvo, como rede de segurança logo acima do uso.
- As leituras oscilantes (2 a 27 V) e os 26,2 V do sub vieram de pontas do multímetro mal encaixadas, não do sistema.
- O guardião foi ajustado por decisão do Denis: teto das JBL até −7,0 dB só com passa-alta LR ≥ 200 Hz a 24 dB/oit (código e `guardiao.json`), teto do sub em −8,0 dB.
- Escuta com os falantes religados (20/10): 30 % e 70 % sem chiado, estalo, raspado nas JBL ou sopro no duto; configuração relida igual depois de religar a fonte. Emenda e porta-fusível de 40 A **frios** depois do teste de 70 %.
- Próximos: soldar a linha de 40 A → validação a 100 % (seção 6) → comparar a fase do sub 0° × 180° (sem atrasos: L = R e atraso 0, regra de 27/10).

### 12.9 Playlist da memória SPL (em montagem, 20/10/2026)

Arquivos no celular em `Music/SPL SOUNDS` e no PC em `Desktop/SPL SOUNDS`, separados nas pastas `Sub 200W` e `Sub 400W`, em duas versões: **sub200W** (memória atual, seguro de 200 W) e **sub400W** (para quando a potência do sub for confirmada e a memória passar a 34 V). Scripts de análise e adaptação em `playlist/` (decodificar, analisar, estrutura, adaptar).

| # | Faixa | Original | Adaptação | Resultado |
|---|---|---|---|---|
| 1 | ABERTURA ALEXA 2026 (Mega Bob Amostradinha, DJ Pablo MG), **inteira** (5:50) | 5:50, −9 LUFS, **+1,8 dBTP (clipado)**, grave com pico em 58 Hz, 1 % abaixo de 40 Hz; abertura sem grave até 0:43, pancadas em 0:44, 1:36 e 1:52 | ver regra comum abaixo | −10,7 LUFS, P90 −9,5 a −9,6, −1,0 dBTP |
| 2 | Tô Bem Mal (Thiago Aquino), **inteira** (2:59) | 2:59, −7,8 LUFS constante, **+1,6 dBTP (clipado)**, grave com pico em **51 Hz** (= Fb), 30 % entre 40 e 60 Hz | ver regra comum | −10,2 LUFS, P90 −9,4, −1,0 dBTP |
| 3 | Barulho do Foguete (Thiago Aquino, CD Sem Limites), **inteira** (2:26), **origem trocada para o MP3 320 kbps do Sua Música** | Mesmo master do Opus do YouTube (−7,8 LUFS, mesmo clipe de 0,7 %), mas sem a recompressão: pico +0,5 dBTP em vez de +1,5 | ver regra comum | −10,4 LUFS, P90 −9,6, −1,0 dBTP |
| 4 | Quem é o Louco Entre Nóis (Thiago Aquino, CD Sem Limites, faixa 23), inteira (3:02), MP3 320 kbps do Sua Música | −7,9 LUFS, +0,6 dBTP, clipe de 0,7 % no master, grave com pico em 51 Hz (34 % entre 40 e 60 Hz) | ver regra comum | −10,4 LUFS, P90 −9,5, −1,0 dBTP |
| 5 | Quatro da Manhã Cê Pega o Uber com a Cara de Choro (Heitor Costa, Estouradinho), inteira (2:21), Opus do YouTube | −9,1 LUFS, +1,3 dBTP, quase sem clipe, grave em 55 Hz, 62 % acima de 200 Hz (voz) | ver regra comum (alvo −9,1) | −11,4 LUFS, P90 −9,3, −1,0 dBTP |
| 6 | É Que Eu Te Sarrei (MC Arizinho e Tavin, DJ THG, funk de BH), inteira (2:27), Opus do YouTube | −12,6 LUFS, o master mais limpo e dinâmico, grave em 62 Hz | ver regra comum; limitador chega a 7,6 dB | −12,4 LUFS, **P90 −11,0: fica ~1,5 dB mais baixa que as outras** (subir mais só esmagando os transientes) |
| 7 | Falling (Trevor Daniel, Bacca Chew Remix, Bass Boosted), inteira (2:58), Opus do YouTube (não é brasileira) | −7,9 LUFS, +0,8 dBTP, grave com pico em **87 Hz** (44 % entre 60 e 100 Hz), só 17 % acima de 200 Hz | ver regra comum (alvo −9,4) | −10,7 LUFS, P90 −9,4, −1,0 dBTP. Faixa de demonstração do sub |

Regra comum (`adaptar2.py`): trecho com entrada de 0,3 s e saída de 2 s; LR4 em 40 Hz no arquivo; **corte em 51 Hz (Q 2) de −2 dB na versão 200 W e −3 dB na 400 W**, para o duto não soprar (o grave das três está na Fb ou perto dela); na versão 200 W, também −1,5 dB abaixo de 100 Hz (prateleira), para o limitador do sub trabalhar menos; volume igualado pelo P90 de curto prazo (alvo −8,5, ficou entre −9,0 e −9,6 depois do limitador); limitador de pico verdadeiro em −1 dBTP (até 5 dB, acima de 1 dB em 16 a 19 % do tempo). Músicas inteiras (decisão do Denis em 20/10: o tempo da demonstração, até 10 min, é controlado por ele na hora). Total com as 7 faixas: ~20 min (o Denis escolhe na hora o que toca nos 10 min). Conta térmica com as músicas inteiras no máximo da SPL [≈]: média de 2 a 4 W nas JBL (pior trecho de 30 s: 5 W, contra 80 W) e de 7 a 9 W no sub (pior trecho: 16 W, contra 200 W); a fonte puxa ~2 a 3 A em média, picos de ~17 A.

O LUFS do trecho fica abaixo de −8 por causa dos 44 s de abertura sem grave; as pancadas ficam em ~−7,5 LUFS de curto prazo. As próximas faixas (brasileiras) seguem o mesmo processo e são igualadas pelo volume das pancadas.

## 13. Sub com ficha confirmada (2026-10-27)

### 13.1 Ficha e conferência

O Denis confirmou que o sub é o **Protech Light 8" 4 Ω, 450 W RMS**. A ficha vem da página do produto no site da Protech (`protechspeakers.com/protech-light-8/`, lida em 27/10); o manual em PDF não abriu.

| Parâmetro | Ficha | Confere com |
|---|---|---|
| Potência RMS | 450 W | √(450 × 4) = **42 V**: acima do ceifamento da ponte (40 V) |
| Re | 3,7 Ω | **3,7 Ω medidos** em 17/10 [M] |
| Fs | 46 Hz | dado de loja anterior |
| Qms / Qes / Qts | 3,90 / 0,61 / 0,52 | Qts calculado pelos outros dois: 0,527 [C] |
| Vas | 10 L | Vas recalculada por Mms e Cms: 9,9 L [C] |
| Bl | 13 T·m | Mms derivada: 96 g; Cms 0,124 mm/N [C] |
| Xmax | 8 mm | |
| Le | 1,63 mH | |
| Sd | 240 cm² | |
| Sensibilidade | 84 dB/1 W/1 m (87 dB/2,83 V) | eficiência calculada 0,154 % → 83,9 dB [C] (ficha: 0,16 %) |

A ficha fecha consigo mesma e com o que medimos: dá para confiar nela.

Na caixa (≈28 L, Fb 51 Hz medida): Vas/Vb = 0,36, Fb/Fs = 1,11 e duto efetivo de ~16 cm para a área de 38,5 cm².

### 13.2 O que muda

- **O calor deixou de ser o limite.** Com 450 W RMS, nem o ceifamento da ponte (40 V ≈ 400 W) passa da potência do falante. O seguro de 200 W existia só porque a potência não tinha prova.
- **O limite agora é o curso do cone abaixo da Fb e o ar no duto em volta da Fb.** Simulação em `calculo_excursao_sub.py` (modelo de Small com perda Ql 7, sem ganho da sala), tom senoidal a 0 dBFS pelo BT, na pior frequência [C]:

| Cenário | Excursão máx. | Ar no duto em 51 Hz |
|---|---|---|
| SPL hoje: +2,6, teto 28,6 V, subsônico 40 Hz | 6,8 mm (**86 %** do Xmax) em 40 Hz | ~49 m/s |
| SPL a 34 V, subsônico 40 Hz | 8,4 mm (**105 %**) em 40 Hz | ~61 m/s |
| SPL a 34 V, subsônico 43 Hz | 7,2 mm (**90 %**) em 42 Hz | ~58 m/s |
| FILME hoje: +6, EQ 45 Hz +3, teto 20 V | 8,2 mm (**102 %**) em 35 Hz | ~38 m/s |
| FILME hoje, pico dentro do ataque de 50 ms (até 40 V) | 13,7 mm (**172 %**) em 40 Hz | ~75 m/s |
| FILME sem o reforço, subsônico 45 Hz, teto 20 V | 7,0 mm (88 %) em 38 Hz | ~38 m/s |
| FILME sem o reforço, subsônico 45 Hz, pico (40 V) | 8,1 mm (101 %) em 70 Hz | ~66 m/s |
| Tom puro no ceifamento da ponte (40 V, ex.: MUSICA ORIG com fonte forte) | 18,3 mm (**229 %**) em 30 Hz | ~75 m/s |

- O Xmax é o limite da região linear (a distorção sobe), não o batente mecânico, que costuma ficar 1,5 a 2 vezes acima. Ainda assim, 172 % é zona de batida do cone.
- **O duto é o gargalo.** Na Fb, o modelo dá 38 a 49 m/s já no teto de hoje; o sopro começa por volta de 17 a 25 m/s. O modelo não tem compressão do duto, então o valor real é menor, mas a conclusão vale: subir de 28,6 V para 34 V dá só **+1,5 dB**, e na região de 51 Hz vira sopro e compressão, não grave limpo. Isso confirma a análise da caixa de 20/10 (seção 12.3): a caixa é da classe de ~200 W.

### 13.3 FILME: correção recomendada

O reforço de 45 Hz (+3 dB, Q 0,7) foi feito em 18/10, antes da Fb ser medida. Somado ao ganho +6, ele leva o sinal ao limitador já em 35 Hz, abaixo da Fb, onde o duto não segura o cone. O ataque de 50 ms deixa passar dois ciclos inteiros em 40 Hz antes de o limitador agir. Na cena pesada de 18/10 o multímetro marcou 24 V com o limitador em 20 V, e esse é um valor médio.

Proposta (o Denis decide):
1. Tirar o reforço de 45 Hz. Se faltar peso, usar no máximo +1,5 dB em 58 Hz.
2. Subir o subsônico do sub para 45 Hz BW24.
3. Baixar o ataque do limitador do sub para 10 a 20 ms. As JBL continuam em 50 ms.

### 13.4 SPL: 28,6 V ou 34 V

- **Manter 28,6 V (recomendado):** 86 % do Xmax, sem mudar o subsônico. A diferença para 34 V é de 1,5 dB, e a caixa e o duto não aproveitam bem esse ganho.
- **Passar a 34 V:** limitador em −7,5 dB, subsônico em **43 Hz** (sem isso o cone passa do Xmax em 38 a 42 Hz), usar as faixas `Sub 400W` (corte de −3 dB em 51 Hz) e só depois da solda da linha de 40 A (a corrente de pico sobe para ~41 A).
