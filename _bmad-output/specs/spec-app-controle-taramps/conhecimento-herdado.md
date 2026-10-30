# Conhecimento herdado dos protótipos (CAP-6)

Fica com os POCs só o conhecimento. Os arquivos citados aqui são a fonte, e o código dos protótipos não é reaproveitado.

## Protocolo
- `protocolo/MAPA.md` (catálogo, formato `*cod*canal*valor*` e `#cod#canal#`, senha, comandos de aparelho inteiro, link de canais, limiter em dB) e `protocolo/APK.md` (conversões).
- As lacunas do MAPA §8 continuam abertas. A primeira leitura real deu 84 de 84 em ~13 s, com o firmware R003 (MAPA §9).
- A resposta da senha traz o firmware nos 4 últimos caracteres. `ERRO` = senha errada.

## O sistema do Denis (medido em 18/10/2026)
- 2.1: saída 1 = JBL L (rota B), saída 2 = JBL R (rota A), saídas 3+4 = sub em ponte. O L do Bluetooth chega pela entrada B.
- JBL 4TRMS80, 3,6 Ω, com passa-alta mínimo de 100 Hz a 24 dB/oit (ficha do fabricante).
- Módulo: ×19,6 nas JBL e ×40 no sub em ponte. O teto técnico vem do recálculo de 20/10 (`_bmad-output/regulagem/memoria-spl.md`, seção 12): JBL 17,9 V (80 W na impedância nominal de 4 Ω, com passa-alta LR24 em 200 Hz) e sub 28,6 V (200 W, até a potência ser confirmada; em −6 dB o limitador do sub passava de 40 V). A potência do sub foi confirmada em 27/10 (Protech Light 8", 450 W; `memoria-spl.md` §13). O limitador das JBL em −11 dB (11,7 V) foi posto baixo, sem explicação técnica.
- Fonte principal: o Bluetooth do celular, que entra ~13 dB abaixo do gerador. Com o BT a 100 % e ganho +2, a saída dá 11,4 V. O gerador só pode ser usado em −13 dB, ou em 0 dB **sem os alto-falantes**.
- Sala: L a 3,20 m, sub a 2,40 m, R a 2,20 m, com 1,60 m entre as JBL. O ouvinte fica 0,9 m à direita da R.
- Os limites atuais estão em `estado/guardiao.json`. Eles são **provisórios**: não são regra nem referência, e quem decide os valores é o Denis.

## Os equipamentos do Setup "Casa 2.1" (CAP-16)

Decisões do Denis de 23/10/2026 (o que não se sabe fica genérico) e de 29/10/2026 (o Setup é só o físico; cada dado da ficha tem origem; o que não se sabe fica em branco).

| Equipamento | Grupo | Ligação |
|---|---|---|
| Módulo SD800.4 EVO (amplificador, 4 canais, bridge 3+4) | 2 | Entradas alimentadas pelo CH1, CH2, CH3 e CH4 |
| JBL L: JBL 4TRMS80 | alto-falante passivo | Saída do módulo alimentada pelo CH1 |
| JBL R: JBL 4TRMS80 | alto-falante passivo | Saída do módulo alimentada pelo CH2 |
| "Sub": Protech Light 8" | alto-falante passivo | Bridge do módulo, alimentado pelo CH3 e pelo CH4; a saída do app se chama "Sub" |

Fichas (entram também na base de modelos), com a origem de cada dado:

| Modelo | Dados | Origem |
|---|---|---|
| JBL 4TRMS80 | 80 W RMS, 4 Ω, Re 3,4 Ω, Fs 98 Hz, Qes 0,81, Qms 3,19, Vas 2,06 L, Sd 64 cm², Bl 4,1, Xmax 1,75 mm, 87 dB; passa-alta mínimo de 100 Hz a 24 dB/oit | oficial (ficha impressa); o Bl não fecha com o Qes (`_bmad-output/regulagem/memoria-spl.md` §12.2) |
| JBL 4TRMS80 no Casa 2.1 | Re 3,6 Ω; caixa selada de ~3,5 L | medido (18/10); o volume é estimado por foto e régua |
| Protech Light 8" | 450 W RMS, 4 Ω, Re 3,7 Ω, Fs 46 Hz, Qms 3,90, Qes 0,61, Qts 0,52, Vas 10 L, Bl 13, Xmax 8 mm, Le 1,63 mH, Sd 240 cm², 84 dB | oficial (site do fabricante, lido em 27/10; `memoria-spl.md` §13.1) |
| Protech Light 8" no Casa 2.1 | Re 3,7 Ω; caixa dutada de ~28 L, Fb 51 Hz, duto de 38,5 cm² | medido (17 e 20/10); o volume é estimado |
| SD800.4 EVO | 132 W por canal em 4 Ω a 12,6 V (ceifa ~23 V); 400 W em ponte em 4 Ω (ceifa ~40 V); ponte em 2 Ω proibida | oficial (manual) |
| SD800.4 EVO no Casa 2.1 | ganho ×19,5 por canal e ×40,5 em ponte; fonte 13,89 V sem carga; BOB 60A (40 A contínuos) | medido (17 e 18/10); a BOB é oficial |

O Link 3–4 sai sozinho: o CH3 e o CH4 alimentam o mesmo bridge. Na tela, o Sub é uma saída só.

## As 3 memórias do aparelho (índice em `estado/memorias.json`)

As memórias do processador não aparecem no app (decisão do Denis, 29/10/2026): o app não lê os nomes nem carrega; quem salva, renomeia e carrega é o app da Taramps. Elas são intocáveis e não são referência para as memórias novas do celular. Também não são referência para o agente. As notas abaixo valem pela posição e ficam só como registro.

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

## Memórias e presets do celular

O app nasce **sem memórias e sem presets do celular** (decisão do Denis, 29/10/2026: as calibrações de hoje estão mal feitas, e tudo começa do zero). A FILME dividida em camadas e o preset "Cinema", herdados até 29/10, saíram. Para comparar com o app da Taramps, os dois olham o estado atual do aparelho; este app não carrega memória do aparelho.

## Presets
- O preset é só o EQ de 15 bandas. Não há preset herdado.
- Os presets DIALOGO e NOITE do app da Taramps são de antes do alinhamento e estão desatualizados.
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
