# Contexto de regulagem: memória SPL

**Status:** parâmetros dos JBLs definidos e conferidos com a calculadora de excursão do repositório Sound · sub documentado, verificação física pendente · fonte pendente · 2026-10-20

**Fontes de verdade externas:** repositório `QADenisBorges/sound` — `componentes/alto-falantes.md`, `componentes/subwoofers.md`, `componentes/amplificadores.md`, `projetos/home-theater-2.1/montagem-2.1.md` e `.claude/skills/consultor-som/scripts/calc_som.py excursao`.

**Objetivo:** memória do processador dedicada a demonstrar a capacidade dos JBLs e do kit, tocando o mais alto possível com segurança, por no máximo 10 minutos (2 ou 3 músicas de 1 a 2 minutos). Não é memória de uso diário.

Este arquivo é a fonte de verdade da regulagem para quem for operar o sistema ou para o guardião do software. Qualquer valor alterado aqui exige remedir.

---

## 1. Equipamento

| Item | Dado | Observação |
|---|---|---|
| Processador | Taramps PRO 2.4BT + AUDIO | Faz todos os filtros, EQ e limiters |
| Módulo | Soundigital SD800.4 EVO, **versão a confirmar** | Denis acredita ser a versão 4 Ω. O Sound registra marcação "BRIDGE 4Ω" no painel e usa a tabela da versão 2 Ω (4 × 132 W em 4 Ω; ponte 2 × 400 W em 4 Ω; ponte em 8 Ω ~264 W; ponte em 2 Ω proibida). Verificação prévia obrigatória (seção 5). Chave de crossover em **F (full)** nos 4 canais: o processador define os cortes |
| Canal 1 | JBL Selenium 4TRMS80 (triaxial 4", 80 W RMS, 4 Ω) | Um falante por canal |
| Canal 2 | JBL Selenium 4TRMS80 (triaxial 4", 80 W RMS, 4 Ω) | Um falante por canal |
| Canais 3+4 | Sub Protech Speakers, provável **Protech Light 8"**, em ponte (sub + no CH4 ⊕, sub − no CH3 ⊖) | 450 W RMS, 84 dB, Fs 46 Hz, Xmax 8 mm. Impedância **a confirmar no multímetro** (existe em 4 Ω simples ou 4+4 Ω dupla). Caixa pronta, tipo (selada/dutada) desconhecido. Verificação prévia obrigatória (seção 5) |
| Caixa dos JBLs | BS-369, selada, ~3,5 L cada | Fc ≈ 124 Hz, Qtc ≈ 0,81-0,90 (Sound). Reduz excursão e protege o cone. Recomendado amortecimento interno e vedação conferida |
| JBL 4TRMS80 (ficha impressa, Sound) | 80 W RMS / 160 W pico · 4 Ω (Re 3,4 Ω) · 87 dB declarado (~85,8 dB efetivo) · Fs 98 Hz · Vas 2,06 L · Sd 64 cm² · **Xmax 1,75 mm de pico** · corte mínimo recomendado pela ficha: 100 Hz a 24 dB/oit | O limite real do falante é o curso do cone, não o calor |

## 2. Memórias do processador

| Memória | Fonte | Referência de nível | Alvo nos JBLs | Uso |
|---|---|---|---|---|
| Diária | Bluetooth (única entrada) | Celular a 100 % com tom a 0 dBFS = sinal máximo (método do Sound) | 30 W → **11,0 V**, tom de 60 Hz, filtros desligados · passa-alta BW 12 dB/oit em 120 Hz | Música no dia a dia |
| **SPL** | **PENDENTE** (entrada analógica, aparelho a definir) | Tom 0 dBFS da fonte, volume da fonte travado | 80 W → **17,9 V**, tom de 60 Hz, filtros desligados · passa-alta **LR 24 dB/oit em 200 Hz** | Demonstração curta com playlist única |

A memória SPL **não pode ser copiada** da diária: a fonte muda, então os níveis de saída precisam ser remedidos do zero.

### Por que 11 V é o seguro da memória diária (origem: repositório Sound, revisão técnica de 09/10/2026)

O 11 V não vem de limite térmico. Vem de **excursão do cone**: Xmax de 1,75 mm de pico, caixa de 3,5 L com Fc ≈ 124 Hz e passa-alta Butterworth de só 12 dB/oit em 120 Hz. Nessa combinação a pior frequência é 122 Hz e o Xmax é atingido com apenas ~12 W. O alvo original era 15,5 V (60 W); a revisão achou que a excursão tinha sido calculada em valor eficaz e comparada com o Xmax (que é pico), o que dobrava a potência segura, e baixou para 11 V (30 W). Mesmo em 11 V, tom contínuo no máximo em 122 Hz dá ~2,8 mm (1,6 × Xmax); foi aceito como pior caso com tom, não com música. Regra herdada: o passa-alta das saídas 1/2 **nunca é removido nem baixado** de 120 Hz BW2.

**Sobre a referência de -13 dB do Bluetooth:** não consta em nenhum documento do Sound. O método documentado trata celular a 100 % com tom a 0 dBFS como o sinal máximo, sem desconto. Antes de usar -13 dB como premissa em qualquer conta, ela precisa ser medida e registrada.

## 3. Parâmetros da memória SPL: canais 1 e 2 (JBLs)

| Parâmetro | Valor | Motivo |
|---|---|---|
| Passa-alta | **Linkwitz-Riley 24 dB/oitava em 200 Hz**, igual nos dois canais | Proteção principal. Com esse filtro, 80 W fica em 0,8 × Xmax (tabela abaixo). O tipo importa: Butterworth 24 dB em 170 Hz daria 1,6 × Xmax em 80 W. Nunca abaixo de 120 Hz BW2 (trava do Sound) |
| Passa-baixa | Desligado ou ≥ 18 kHz | O triaxial divide médio e tweeter internamente |
| Passa-baixa do sub nesta memória | LR 24 dB/oit em **200 Hz** (saídas 3 e 4 iguais) | Acompanha o passa-alta dos JBLs para não abrir buraco entre 120 e 200 Hz. Na diária fica em 120 Hz |
| Ganho / nível de saída | Até o multímetro marcar **17,9 V AC** com tom de 60 Hz a 0 dBFS, filtros desligados | 80 W em 4 Ω = limite térmico do falante e, com LR4 em 200 Hz, dentro do Xmax. Módulo em 40 % (versão 4 Ω) ou 60 % (versão 2 Ω) do canal |
| Limiter | Threshold no nível que gerou 17,9 V · ataque 1-5 ms · release 100-300 ms | Rede de segurança caso a fonte ou a música passe do previsto. O app mostra o threshold em dB, não em volts: com o tom em 400 Hz e 17,9 V no multímetro, baixar o threshold até a tensão começar a cair e voltar até 17,9 V; anotar o valor em dB |
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
| Subsônico | Caixa selada → 25 Hz; com duto → 0,75-0,8 × Fb; sem conclusão → **30 Hz, 24 dB/oit** | Teste do duto antes (seção 5) |
| Ganho | Sub 4 Ω em ponte (400 W disponíveis, sub de 450 W): regular pelo módulo, **40 V** em 50 Hz (tabela do fabricante, usada no Sound); com margem de 5 %, ~38 V | LED de clip do módulo é a referência final. Sub em 8 Ω em ponte: ~264 W → 46 V. Sub em 2 Ω: um canal só, 200 W → 20 V |
| Limiter | Obrigatório, no nível medido, igual nas saídas 3 e 4 | |
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
6. Ativar o limiter em cada saída com threshold no nível atual (no app o limiar aparece em volts na saída do processador; 2,10 V é o máximo). Confirmar: subir 2 dB o nível de saída, o multímetro não passa do alvo.
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
| Ganho entre 17,9 V e no máximo 22 V, e só acima de 17,9 V após validação por escuta | Acima de 22 V → cone fora do curso |
| Limiter ativo no nível medido | Desligado → sem rede de segurança |
| Caixas seladas | Duto → excursão abaixo da afinação, mudar passa-alta |
| Duração ≤ 10 min por demonstração | Uso contínuo → limite térmico volta a importar |

Ações que a IA **nunca** libera nesta memória: baixar ou suavizar o passa-alta dos canais 1 e 2 (mínimo absoluto herdado do Sound: 120 Hz BW2), desligar limiter, alterar só uma das saídas 3/4, subir nível de saída acima da posição registrada, realçar EQ, trocar fonte de entrada, tocar tom de teste com falantes ligados.

## 9. Registro de medições

| Data | Memória | Canal | Frequência | Tensão alvo | Tensão medida | Posição de saída | Limiter | Passa-alta | Quem |
|---|---|---|---|---|---|---|---|---|---|
| | SPL | 1 | 60 Hz | 17,9 V (ajuste fino: até 22 V) | | | (V no app) | LR4 200 Hz | |
| | SPL | 2 | 60 Hz | 17,9 V (ajuste fino: até 22 V) | | | (V no app) | LR4 200 Hz | |
| | SPL | 3+4 ponte | 60 Hz | 40 V se 4 Ω / versão 2 Ω (ver 5.0) | | | (V no app) | LP LR4 200 Hz + subsônico | |

## 10. Pendências

- [ ] Verificação prévia 5.0: versão do módulo, impedância real do sub e tipo da caixa do sub.
- [ ] Medir e registrar o nível real que o Bluetooth entrega (a referência de -13 dB não está documentada no Sound).
- [ ] Fonte da memória SPL: entrada do processador e aparelho. Travar e marcar o volume.
- [ ] Definir e masterizar a playlist.
- [ ] Amortecimento interno e vedação das caixas de 3,5 L.
- [ ] Registrar o passa-alta atual da memória diária, para comparação.

## 11. Referências de cálculo

- Tensão alvo: V = √(P × Z). 80 W × 4 Ω → 17,9 V. 30 W × 4 Ω → 11,0 V.
- -13 dB em tensão = ×0,224; em potência = ÷20.
- +3 dB = dobro da potência; +10 dB = dobro da sensação de volume (10× a potência).
- Norma AES2 / IEC 60268-5: RMS = contínua (térmico); programa = 2× RMS; pico = 4× RMS. Ganho para música se regula no RMS.
- Excursão: `calc_som.py excursao --re 3.4 --bl 4.1 --sd 64 --fs 98 --vas 2.06 --qms 3.19 --volume 3.5 --xmax 1.75 --filtro lr4 --corte 200 --tensao 17.9` (repositório Sound).
- Fontes consultadas em 2026-10-20: listagens de revenda do 5TRMS80 (80 W RMS, 4 Ω, 87 dB, 76-20000 Hz) e do SD800.4 EVO (números divergem entre versões 2 Ω e 4 Ω; manual é a referência).
