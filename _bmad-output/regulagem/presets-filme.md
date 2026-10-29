# Presets de filme por gênero (EQ de 15 bandas)

**Status (27/10 20h33):** a memória 3 passou a se chamar **CINEMA** (decisão do Denis), salva com
as bandas Cinema e os limitadores novos (L −12, R −15, sub −15). Ação v3 aprovado de ouvido; Drama,
Suspense e Terror guardados aqui sem escuta; Comédia fora da lista do Denis por enquanto. Os
presets ficam neste arquivo e são aplicados pelo notebook: o processador não guarda preset, e o
app da Taramps precisa do celular no USB.

Cada preset é **só o EQ gráfico de 15 bandas** (`020`), aplicado por cima da memória 3 CINEMA
(antes FILME). Ganho, limitadores, cortes, atrasos, fase e o EQ de saída (JBL 2 kHz +2,5)
continuam os da memória. A base é o preset herdado **Cinema** (as 15 bandas da FILME de 18/10).

## Regras usadas em todos

- **25 e 40 Hz sempre em 0.** O EQ gráfico vale para todas as saídas, e abaixo da Fb (51 Hz) o
  duto não segura o cone. Na FILME o subsônico está em 45 Hz por isso (seção 13 do memoria-spl.md).
- **Reforço de no máximo +2 dB por banda**, como manda o guia: EQ de gosto vem por último, 1 a 2 dB
  de cada vez. Cada reforço faz o limitador agir mais cedo.
- **63 Hz sobe no máximo +2.** No estéreo do streaming o LFE é descartado, e um pouco de 63 Hz
  devolve o peso. Com o limitador do sub em −12 dB (~20 V), 60 Hz fica em ~51 % do Xmax.
- **Faixas da voz** (guia-eq-filme.md, seção 3): 160 Hz = corpo; 250 a 400 Hz = embolado/caixa
  (corte limpa); 1 a 2 kHz = inteligibilidade; 2,5 kHz já tem o +2,5 do EQ de saída das JBL;
  4 a 8 kHz = aspereza e sibilância.
- **10 e 16 kHz** compensam as JBL fora do eixo, que perdem agudo acima de 5 kHz. Por isso o
  Cinema tem +2 nas duas.

## As curvas

| Banda (Hz) | 25 | 40 | 63 | 100 | 160 | 250 | 400 | 630 | 1k | 1,6k | 2,5k | 4k | 6,3k | 10k | 16k |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Cinema (base) | 0 | 0 | 0 | 0 | +1 | 0 | −1 | 0 | 0 | 0 | 0 | −1 | −2 | +2 | +2 |
| **Ação** (v3, aprovado 27/10) | 0 | 0 | +1 | +1 | +1 | 0 | −1 | 0 | 0 | +1 | 0 | 0 | −1 | +2 | +1 |
| **Drama** | 0 | 0 | 0 | 0 | +1 | −1 | −1 | 0 | 0 | +1 | 0 | 0 | −1 | +1 | +1 |
| **Terror** | 0 | 0 | +2 | +1 | 0 | −1 | 0 | −1 | 0 | 0 | 0 | 0 | −1 | +2 | +2 |
| **Comédia** | 0 | 0 | −1 | 0 | 0 | −2 | −1 | 0 | +1 | +1 | 0 | −1 | −2 | +1 | 0 |
| **Suspense** | 0 | 0 | +1 | +1 | 0 | −1 | −1 | 0 | 0 | +1 | 0 | 0 | −1 | +2 | +1 |

## Por que cada um

**Ação.** O impacto vem de 63 Hz (+1), no lugar do LFE que o estéreo perde. O 400 Hz cortado
limpa a mistura densa, e 1 e 1,6 kHz +1 seguram as palavras por cima dos efeitos. 6,3 kHz −2 tira
a aspereza de tiro, vidro e metal em volume alto, e 10 kHz +2 traz o ambiente.

Ajuste da escuta de 27/10 (v1 → v2), pelas notas do Denis:
- "Voz magra e baixa": 160 Hz de 0 para +1 (corpo), 250 Hz de −1 para 0 (o corte tirava calor),
  1 kHz de 0 para +1 (palavra mais à frente).
- "Grave explosivo, quase estourando": 63 Hz de +2 para +1 e 100 Hz de +1 para 0. Perto da Fb
  (51 Hz) o reforço empurra o duto e o limitador do sub ao mesmo tempo.
- "Falta detalhe no ambiente" (a cabeça da caveira caindo fora da cena principal): 4 kHz de −1
  para 0 (ataque de objetos pequenos) e 10 kHz de +1 para +2 (ambiente). O 6,3 kHz fica em −2,
  para os tiros não ficarem ásperos.

Segunda escuta (v2 → v3): "o grave estava melhor antes, os murros e os voos quase somem, menos
envolvente, diálogo continua baixo".
- 100 Hz de 0 para +1: volta o corpo do murro e do grave. O 63 Hz fica em +1, porque com +2 o
  grave quase estourava (v1); a v3 fica no meio.
- 6,3 kHz de −2 para −1: o estalo do murro e o sopro dos voos estão entre 4 e 8 kHz.
- 1 kHz de +1 para 0: o meio reforçado empurrava tudo para a frente e tirava o espaço, sem
  resolver o diálogo.
- A voz baixa na ação também vem da dinâmica da mixagem: o diálogo fica uns 20 dB abaixo dos
  picos. O EQ ajuda pouco nisso; o remédio de verdade é compressão (etapa 7 da regulagem).

**Drama.** O alvo é a voz natural e próxima. 160 Hz +1 dá calor, o corte em 250 e 400 Hz tira a
caixa, e 1,6 kHz +1 dá presença sem forçar. 4 kHz volta a 0 (timbre natural) e 6,3 kHz só −1 para
a sibilância. O grave fica plano: a trilha soa como foi mixada.

**Terror.** 63 e 100 Hz (+2/+1) dão o ronco e os drones que criam o medo. 630 Hz −1 abre o meio e
deixa o som mais espaçoso. 10 e 16 kHz +2 trazem rangidos, sussurros e ambiente. 6,3 kHz −1 impede
que os gritos furem o ouvido.

**Comédia.** É quase só diálogo rápido, então é o mais voltado para a voz. O grave baixa
(63 Hz −1), o corte em 250 Hz é o mais forte (−2) para clareza, e 1 e 1,6 kHz +1 ajudam a
entender a fala rápida. A sibilância fica cortada (6,3 kHz −2) e o agudo moderado.

**Suspense.** Muitas cenas com fala baixa e trilha tensa. 63 e 100 Hz +1 mantêm a tensão sem
dominar. 1,6 kHz +1 ajuda a entender sussurros. 4 kHz em 0 preserva os detalhes, e 10 kHz +2 traz
o ambiente.

## Lotes para o ajustar_processador.py

Cada lote escreve as 15 bandas (índice 0 = 25 Hz … 14 = 16 kHz).

```
Ação:     020:0=0 020:1=0 020:2=1 020:3=1 020:4=1 020:5=0 020:6=-1 020:7=0 020:8=0 020:9=1 020:10=0 020:11=0 020:12=-1 020:13=2 020:14=1
Drama:    020:0=0 020:1=0 020:2=0 020:3=0 020:4=1 020:5=-1 020:6=-1 020:7=0 020:8=0 020:9=1 020:10=0 020:11=0 020:12=-1 020:13=1 020:14=1
Terror:   020:0=0 020:1=0 020:2=2 020:3=1 020:4=0 020:5=-1 020:6=0 020:7=-1 020:8=0 020:9=0 020:10=0 020:11=0 020:12=-1 020:13=2 020:14=2
Comédia:  020:0=0 020:1=0 020:2=-1 020:3=0 020:4=0 020:5=-2 020:6=-1 020:7=0 020:8=1 020:9=1 020:10=0 020:11=-1 020:12=-2 020:13=1 020:14=0
Suspense: 020:0=0 020:1=0 020:2=1 020:3=1 020:4=0 020:5=-1 020:6=-1 020:7=0 020:8=0 020:9=1 020:10=0 020:11=0 020:12=-1 020:13=2 020:14=1
Cinema:   020:0=0 020:1=0 020:2=0 020:3=0 020:4=1 020:5=0 020:6=-1 020:7=0 020:8=0 020:9=0 020:10=0 020:11=-1 020:12=-2 020:13=2 020:14=2
```

## Pendências

- [x] FILME de volta no processador (19h29 + correção do EQ de saída e do ataque do sub às 19h31: ele tinha religado na SPL), conferida igual à memória 3 às 19h32.
- [x] Os 5 lotes simulados sobre a FILME conferida: todos APROVADOS (19h33).
- [ ] O Denis ouve cada um com uma cena típica do gênero e dá nota (voz, grave, detalhes, envolvimento).
- [ ] Guardar os aprovados como presets no celular.

## Limitadores da FILME (base de todos os presets), em teste desde 27/10 20h

Análise de 27/10: o diálogo baixo e os detalhes sumindo na ação vêm da profundidade dos limitadores
das JBL, não do EQ. Com o BT a 100 %, as JBL começavam a limitar em ~−11,8 dBFS (no nível dos picos
do diálogo) e o sub só em ~−6 dBFS [≈]. Aplicado para teste, ainda **não salvo na memória 3**:

| Limitador | Antes | Agora | Começa a agir em [≈] | Teto |
|---|---|---|---|---|
| JBL L | −15 dB | −12 dB | −8,8 dBFS | 9,8 V (0,76 × Xmax com LR24 150 Hz) |
| JBL R | −18 dB | −15 dB | −8,8 dBFS | 6,9 V |
| Sub | −12 dB | −15 dB | −9,0 dBFS | 14,4 V |

Para desfazer: `uv run ferramentas/ajustar_processador.py 071:2=-12.0 071:1=-18.0 071:0=-15.0`.
Se aprovado: pôr as bandas do Cinema e salvar a memória 3; depois voltar ao preset em uso.

## Pausa (27/10, noite)

O Denis pausou a análise: a JBL R soava fraca e o teste de mute por canal apontou o fio que pode
ter soltado do falante da R. Ele vai soldar os cabos nos fios das duas caixas.

Estado deixado no processador: FILME com os limitadores em teste (L −12, R −15, sub −15), preset
Ação v3 e **saída 2 (JBL R) mutada** pelo teste (`100:1=1`); a saída 1 está ativa. Nada salvo na
memória 3. Ao voltar: desmutar a R (`100:1=0`), refazer a escuta da ação com as duas caixas e só
então decidir os limitadores. A queixa antiga do tweeter da L mais fraco pode ter a mesma causa
(contato); conferir de novo depois da solda.
