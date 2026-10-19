# Guia de equalização e calibração para filme (2.1 por Bluetooth)

Base técnica para a IA avaliar e propor ajustes sem depender só da percepção do Denis
(pedido dele em 18/10/2026). Cada regra traz o porquê, a aplicação no nosso sistema e a fonte.
Os limites físicos (tetos dos limitadores, cortes) estão em `analise-nivel-2026-10-18.md`.

## 1. Ordem profissional de trabalho

Do mais grosso ao mais fino. Não se corrige com EQ o que é de posição ou de tempo.

1. **Geometria:** triângulo equilátero, as duas JBL à mesma distância, voltadas para o ouvinte.
2. **Tempo e fase:** atrasar o alto-falante mais perto para todos chegarem juntos; conferir a
   polaridade do sub no cruzamento.
3. **Medir a resposta no lugar de ouvir**, com microfone calibrado: UMIK-1 + REW, que é grátis.
4. **Corrigir com EQ só os picos abaixo de ~300 Hz**, com cortes. Não reforçar vales: são
   cancelamentos da sala e não sobem com EQ.
5. **Curva-alvo:** levemente descendente para o agudo, com uma "prateleira" de grave de
   +4 a +6 dB abaixo de ~100 Hz (preferência medida pela Harman, Olive e Welti).
6. **Só por último, o gosto:** presets por gênero, ajustados de ouvido, 1 a 2 dB de cada vez.

## 2. O que o filme pelo Bluetooth tira do som (e o que fazer)

| Fato | Consequência no 2.1 | Ação |
|---|---|---|
| No downmix estéreo padrão (Lo/Ro, Lt/Rt), **o canal LFE (.1) é descartado**. | O "ronco" de explosão que só existe no LFE **não chega** ao celular nem ao processador. É a causa nº 1 de "falta alguma coisa" no filme por estéreo. | Compensar no sub: +3 a +6 dB entre 30 e 70 Hz na memória de filme (feito: sub +9, +3 dB em 45 Hz). A solução real seria uma fonte 5.1, que o BT não leva. |
| O Netflix mixa o diálogo em **−27 LKFS** (com gate de diálogo), com picos até −2 dBTP e LRA de 4 a 18. | O diálogo fica uns 20 dB abaixo dos picos: fala baixa, explosão alta. O teto que medimos é atingido só pelas explosões. | O volume do diálogo vem de **compressão**: modo noturno do app ou da TV box. Ganho não resolve. |
| Os filmes são mixados para ~79 a 85 dB SPL. Em volume mais baixo, o ouvido perde **grave e agudo** (curvas de igual audibilidade, ISO 226). | Abaixo do volume de referência, o som fica "sem vida", principalmente no grave e na presença. | O preset NOITE leva compensação de audibilidade: mais grave e um pouco mais de agudo, como o Dynamic EQ da Audyssey. |

## 3. A voz: faixas e o que cada uma faz

| Faixa | Na voz | Sobra | Falta |
|---|---|---|---|
| 100–160 Hz | fundamental da voz masculina, "força" | embolado | voz magra |
| 160–250 Hz | corpo, calor | "abafado", caixa | sem vida |
| 300–600 Hz | "caixa", oco | som de lata | — |
| 1–2 kHz | inteligibilidade das palavras | nasal | voz distante |
| 2–4 kHz | clareza; nasal se sobra | áspero, nasal | sem definição |
| 4–8 kHz | sibilância e sopro (4–6 kHz no homem, 6–8 kHz na mulher) | "assoprando", chiado | opaco |

**No nosso sistema:** o corte das JBL em 150 Hz (as de 4" não aguentam menos) manda
**a fundamental da voz masculina para o sub**. Se o sub não estiver perto e entre as JBL, essa
parte da voz vem de outro lugar, e a voz perde corpo e presença. O padrão THX é cortar em
80 Hz; acima de ~80–120 Hz o sub passa a ser localizável.

## 4. Integração sub × JBL

- Com o corte em 150 Hz, a diferença de distância pesa muito: meio período em 150 Hz são
  ~114 cm. Uma diferença de 50 a 60 cm entre o sub e as JBL até o ouvinte já cancela parte do
  cruzamento, o "buraco" entre o grave e a voz.
- O processador tem atraso por saída (`050`, em cm). O certo é atrasar o que está **mais
  perto**, na diferença de distância.
- A fase de 180° no sub foi escolhida de ouvido, com ruído de 80 a 250 Hz e atraso 0 em todos.
  Depois de acertar os atrasos, o teste de fase tem de ser refeito.

## 5. Problemas conhecidos deste sistema que afetam o filme

1. LFE descartado (seção 2): compensado em parte.
2. Corte alto (150 Hz) e posição do sub: a medir (seção 3).
3. **Atrasos em 0** em todas as saídas: a medir as distâncias.
4. **Tweeter da JBL esquerda mais fraco** (achado de 18/10): desequilibra o centro fantasma, e a
   voz "puxa" para a direita, perdendo presença no meio. Corrigir na caixa, não no EQ.
5. Sem microfone de medição: vales e picos da sala, comuns entre 60 e 200 Hz, ficam invisíveis.

## 6. Ouvinte fora do centro (o caso desta sala)

Medido em 18/10/2026: o ouvinte fica 0,9 m à direita da JBL R, a 51° da L e 24° da R. Um tweeter de
4" perde vários dB acima de 5 kHz a 50° fora do eixo: a L chega mais fraca e mais "fechada" por
geometria, antes de qualquer defeito. O remédio do processador é o de áudio automotivo: **atrasar
as fontes mais próximas pela diferença de distância e baixar o nível delas por 20·log(d_longe/d_perto)**.
Isso recoloca a imagem no meio, mas não devolve o triângulo estéreo nem o envolvimento. O que devolve:
virar as caixas para o ouvinte (toe-in), levantá-las à altura do ouvido e, se possível, sentar entre
elas. Compressão suave pelos limitadores (−15/−18 nas JBL, −12 no sub, liberação 200 ms) levanta a
voz contra a ação sem EQ, porque o diálogo do streaming fica ~20 dB abaixo dos picos.

## Fontes

- Downmix e LFE: [TV Technology, LoRo](https://www.tvtechnology.com/opinions/what-is-downmixing-part-1-stereo-loro); [Dolby, uso do LFE](https://professional.dolby.com/en-in/gaming/downloads/lfe-usage); [Free TV Australia OP-60](https://www.freetv.com.au/wp-content/uploads/2019/08/OP-60-Multi-Channel-DownMix-Issue-3-November-2014.pdf); [cinemaconfig, LtRt/LoRo](https://cinemaconfig.com/reference/downmixing-ltrt-loro)
- Netflix: [Netflix Sound Mix Specifications](https://backlothelp.netflix.com/hc/en-us/articles/360001794307-Netflix-Sound-Mix-Specifications-Best-Practices-v1-5)
- Curva-alvo: [AES, Olive e Welti](https://aes2.org/publications/elibrary-page/?id=16768); [Acoustics Today](https://acoustictoday.org/issues/2022AT/Spring2022/files/basic-html/page60.html)
- Audibilidade e volume: [Sound & Vision, Dynamic EQ](https://www.soundandvision.com/content/dueling-loudness-corrections-explained); [cinemaconfig, Dynamic EQ](https://cinemaconfig.com/reference/audyssey-dynamic-eq)
- Voz: [Lynda, EQ de diálogo](https://origin-www.lynda.com/Pro-Tools-tutorials/Master-EQ-settings-production-sound/761953/5025749-4.html); [Vashi Visuals](https://vashivisuals.com/5-eq-audio-for-video-tips-)
- Cruzamento e sub: [KEF, além dos 80 Hz](https://us.kef.com/blogs/news/theres-more-to-home-theater-than-the-80-hz-crossover); [Audioholics, gerência de grave](https://www.audioholics.com/subwoofer-setup/bass-management-the-right-stuff); [miniDSP, alinhamento com o REW](https://www.minidsp.com/community/threads/time-alignment-method-for-sub-integration-for-stereo-speakers-with-shd-and-rew.19931/post-59156)
- Geometria: [Teufel, posição estéreo](https://support.teufel.de/hc/en-us/articles/115005701029-How-to-Properly-Position-Stereo-Speakers); [cinemaconfig, triângulo](https://cinemaconfig.com/reference/stereo-listening-triangle)
- Medição: [miniDSP UMIK-1 (PDF)](https://homemovie.hu/wp-content/uploads/2020/06/pdf_minidsp_UMIK-1_1.pdf); [Violet Recording, REW](https://violetrecording.com/how-to-use-room-eq-wizard/)
