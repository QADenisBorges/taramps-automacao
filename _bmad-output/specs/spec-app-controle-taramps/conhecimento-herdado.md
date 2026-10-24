# Conhecimento herdado dos protótipos (CAP-6)

Fica com os POCs só o conhecimento. Os arquivos citados aqui são a fonte, e o código dos protótipos não é reaproveitado.

## Protocolo
- `protocolo/MAPA.md` (catálogo, formato `*cod*canal*valor*` e `#cod#canal#`, senha, comandos de aparelho inteiro, link de canais, limiter em dB) e `protocolo/APK.md` (conversões).
- As lacunas do MAPA §8 continuam abertas. A primeira leitura real deu 84 de 84 em ~13 s, com o firmware R003 (MAPA §9).
- A resposta da senha traz o firmware nos 4 últimos caracteres. `ERRO` = senha errada.

## O sistema do Denis (medido em 18/10/2026)
- 2.1: saída 1 = JBL L (rota B), saída 2 = JBL R (rota A), saídas 3+4 = sub em ponte. O L do Bluetooth chega pela entrada B.
- JBL 4TRMS80, 3,6 Ω, com passa-alta mínimo de 100 Hz a 24 dB/oit (ficha do fabricante).
- Módulo: ×19,6 nas JBL e ×40 no sub em ponte. O teto técnico vem do recálculo de 20/10 (`_bmad-output/regulagem/memoria-spl.md`, seção 12): JBL 17,9 V (80 W na impedância nominal de 4 Ω, com passa-alta LR24 em 200 Hz) e sub 28,6 V (200 W, até a potência ser confirmada; em −6 dB o limitador do sub passava de 40 V). O limitador das JBL em −11 dB (11,7 V) foi posto baixo, sem explicação técnica.
- Fonte principal: o Bluetooth do celular, que entra ~13 dB abaixo do gerador. Com o BT a 100 % e ganho +2, a saída dá 11,4 V. O gerador só pode ser usado em −13 dB, ou em 0 dB **sem os alto-falantes**.
- Sala: L a 3,20 m, sub a 2,40 m, R a 2,20 m, com 1,60 m entre as JBL. O ouvinte fica 0,9 m à direita da R.
- Os limites atuais estão em `estado/guardiao.json`. Eles são **provisórios**: não são regra nem referência, e quem decide os valores é o Denis.

## Os equipamentos do Setup "Casa 2.1" (CAP-16)

Decisão do Denis de 23/10/2026: o que não se sabe fica genérico.

| Equipamento | Grupo | Ligação |
|---|---|---|
| "Módulo genérico" (amplificador) | 2 | Entradas alimentadas pelo CH1, CH2, CH3 e CH4; ×19,6 nos canais das JBL e ×40 no bridge do sub |
| JBL L: JBL 4TRMS80, 3,6 Ω, passa-alta mínimo de 100 Hz a 24 dB/oit | alto-falante passivo | Saída do módulo alimentada pelo CH1 |
| JBL R: JBL 4TRMS80, 3,6 Ω, passa-alta mínimo de 100 Hz a 24 dB/oit | alto-falante passivo | Saída do módulo alimentada pelo CH2 |
| "Sub": categoria subwoofer, modelo desconhecido (aparece como genérico) | alto-falante passivo | Bridge do módulo, alimentado pelo CH3 e pelo CH4; a saída do app se chama "Sub" |

O Link 3–4 do Casa 2.1 fica ligado: o CH3 e o CH4 alimentam o mesmo bridge e precisam ficar iguais. Na tela, o Sub é uma saída só.

## As 3 memórias do aparelho (índice em `estado/memorias.json`)

O app só lê os nomes e carrega estas memórias. Quem salva e renomeia é o app da Taramps. As notas abaixo valem pela posição.

1. **SPL** (salva em 20/10; antes era a MUSICA HIGH): o máximo seguro de cada falante pelo BT a 100 %. JBL +6 com passa-alta LR24 em 200 Hz e limitador −7 (ataque 5 ms); sub +2,6 com passa-alta BT24 em 40 Hz, passa-baixa LR24 em 200 Hz e limitador −9 (ataque 10 ms); liberação 200 ms, fase e atrasos 0, EQ plano. Medido: 17,7 V nas JBL e 27,5 V no sub (`_bmad-output/regulagem/memoria-spl.md`, seção 12.8; leitura de 24/10 em `estado/leituras/2026-10-24_1714-app-memoria1-spl.json`). Decisão do Denis de 24/10.
2. **MUSICA ORIG:** ainda é a versão antiga (ganhos −5/−10, limitadores −11/−6). Precisa ser refeita, e esse é o teste 1 do sinal de sucesso.
3. **FILME:** a versão definitiva, retratada em `estado/leituras/2026-10-18_2145.json`.
   - Atrasos: L 0, R 100 cm, sub 80 cm.
   - Ganhos: L +10, R +7, sub +6.
   - Limitadores: −15 / −18 / −12, com liberação de 200 ms.
   - EQ de saída: JBL 2 kHz +2,5 (Q 0,7); sub 45 Hz +3 (Q 0,7).
   - EQ global: 160 Hz +1, 400 Hz −1, 4 kHz −1, 6,3 kHz −2, 10 kHz +2, 16 kHz +2.
   - Sub: fase 180°, passa-alta 40 Hz BT24, passa-baixa 150 Hz LR24.
   - JBL: corte LR24 em 150 Hz.

## A FILME dividida nas três camadas (CAP-4, CAP-5, CAP-16)

A leitura `estado/leituras/2026-10-18_2145.json` entra no app dividida assim:

| Camada | Valores |
|---|---|
| **Setup "Casa 2.1"** (calibração) | Atrasos L 0, R 100 cm, sub 80 cm; fase do sub 180°; JBL com corte LR24 em 150 Hz; sub com passa-alta BT24 em 40 Hz e passa-baixa LR24 em 150 Hz; rotas 1 = B e 2 = A; Link 3–4 ligado; equilíbrio da saída JBL R (CH2): −3 dB no ganho e no limiter |
| **Memória FILME** (caráter) | Ganhos L +10, sub +6; limiter JBL −15, sub −12; ataque 50 ms, liberação 200 ms; EQ de saída JBL 2 kHz +2,5 (Q 0,7) e sub 45 Hz +3 (Q 0,7) |
| **Preset "Cinema"** (15 bandas) | 160 Hz +1, 400 Hz −1, 4 kHz −1, 6,3 kHz −2, 10 kHz +2, 16 kHz +2; o resto 0 |

Ganho e limiter finais = valor da memória + equilíbrio do Setup: a R fica com ganho +7 e limiter −18, como na leitura.

## Presets
- O preset é só o EQ de 15 bandas. O herdado é o "Cinema", acima.
- Os presets DIALOGO e NOITE, que estão no celular, são de antes do alinhamento e estão desatualizados.
- Os arquivos `.trps` do app da Taramps (em `protocolo/presets/`) são cifrados e não são lidos. Um preset antigo entra carregando-o pelo app da Taramps e salvando a leitura como memória do celular (CAP-4).

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
