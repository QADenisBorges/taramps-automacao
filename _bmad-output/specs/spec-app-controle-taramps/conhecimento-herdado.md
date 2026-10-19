# Conhecimento herdado dos protótipos (CAP-6)

Fica com os POCs só o conhecimento. Os arquivos citados aqui são a fonte, e o código dos protótipos não é reaproveitado.

## Protocolo
- `protocolo/MAPA.md` (catálogo, formato `*cod*canal*valor*` e `#cod#canal#`, senha, comandos de aparelho inteiro, link de canais, limiter em dB) e `protocolo/APK.md` (conversões).
- As lacunas do MAPA §8 continuam abertas. A primeira leitura real deu 84 de 84 em ~13 s, com o firmware R003 (MAPA §9).
- A resposta da senha traz o firmware nos 4 últimos caracteres. `ERRO` = senha errada.

## O sistema do Denis (medido em 18/10/2026)
- 2.1: saída 1 = JBL L (rota B), saída 2 = JBL R (rota A), saídas 3+4 = sub em ponte. O L do Bluetooth chega pela entrada B.
- JBL 4TRMS80, 3,6 Ω, com passa-alta mínimo de 100 Hz a 24 dB/oit (ficha do fabricante).
- Módulo: ×19,6 nas JBL e ×40 no sub em ponte. Limitador das JBL em −11 dB = 11,7 V; do sub em −9 dB = 28,6 V (em −6 dB passava de 40 V).
- Fonte principal: o Bluetooth do celular, que entra ~13 dB abaixo do gerador. Com o BT a 100 % e ganho +2, a saída dá 11,4 V. O gerador só pode ser usado em −13 dB, ou em 0 dB **sem os alto-falantes**.
- Sala: L a 3,20 m, sub a 2,40 m, R a 2,20 m, com 1,60 m entre as JBL. O ouvinte fica 0,9 m à direita da R.
- Os limites atuais estão em `estado/guardiao.json`. Eles são **provisórios**: não são regra nem referência, e quem decide os valores é o Denis.

## As 3 memórias do DSP (índice em `estado/memorias.json`)
1. **MUSICA HIGH:** JBL +2, sub −1,4, EQ plano, ataque 10 ms, limitadores −11 (JBL) / −9 (sub). Aprovada.
2. **MUSICA ORIG:** ainda é a versão antiga (ganhos −5/−10, limitadores −11/−6). Precisa ser refeita, e esse é o teste 1 do sinal de sucesso.
3. **FILME:** a versão definitiva, retratada em `estado/leituras/2026-10-18_2145.json`.
   - Atrasos: L 0, R 100 cm, sub 80 cm.
   - Ganhos: L +10, R +7, sub +6.
   - Limitadores: −15 / −18 / −12, com liberação de 200 ms.
   - EQ de saída: JBL 2 kHz +2,5 (Q 0,7); sub 45 Hz +3 (Q 0,7).
   - EQ global: 160 Hz +1, 400 Hz −1, 4 kHz −1, 6,3 kHz −2, 10 kHz +2, 16 kHz +2.
   - Sub: fase 180°, passa-alta 40 Hz BT24, passa-baixa 150 Hz LR24.
   - JBL: corte LR24 em 150 Hz.

## Presets
- Os presets DIALOGO e NOITE, que estão no celular, são de antes do alinhamento e estão desatualizados.
- Os arquivos `.trps` do app da Taramps (em `protocolo/presets/`) são cifrados e não são lidos. Um preset antigo entra carregando-o pelo app da Taramps e salvando a leitura como preset próprio (CAP-5).

## Preferências e método do Denis
- Ele avalia sempre as 4 dimensões juntas (voz, grave, detalhes, envolvimento) e dá notas.
- Antes de propor EQ, confere posição, tempo, fase e LFE.
- As instruções vêm curtas, um passo por vez, cada uma com o que conferir e o que responder.
- Toda medida vem com a foto do multímetro VC830L marcada.
- A diretriz de 18/10 ("não mexer no celular") valia para o app da Taramps operado por adb. Com este app, o celular volta a ser a tela dele.

## Cuidados herdados do app da Taramps (para os testes de paridade)
- Deslizar uma memória na tela Salvar **carrega** essa memória.
- Abrir a tela Limiter com o Auto ligado faz o app reescrever o attack e o release.
- Depois de usar o app da Taramps, é preciso reler o aparelho.

## Situação registrada: regulagem presa à fonte
- Uma regulagem feita sobre o nível do gerador do processador deixou o som pelo Bluetooth muito baixo, cerca de 13 dB abaixo. O Denis passou a usar só o BT como fonte, e a equalização melhorou.
- Não é uma capacidade do POC 1. Os sons de teste pelo celular ficam para o POC 2. Marcar em cada preset a fonte para a qual ele foi regulado fica para quando houver uma segunda fonte (TV box).
