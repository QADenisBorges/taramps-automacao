# Mapa do protocolo BLE do PRO 2.4BT + AUDIO

**Versão do mapa:** 1 (2026-10-16), com a codificação completa lida do APK. A prova por captura ainda está pendente.
**Alvo:** app `com.taramps.pro24bt_audio` 1.0.8 (Kotlin nativo, código em `br.com.taramps.processador_mini`).
**Início do orçamento:** 2026-10-16, com a análise do APK. A decodificação levou menos de um dia; o critério de parada não disparou.
**Veredito sobre proteção:** **livre**. O protocolo é texto ASCII sem cifra, assinatura, contador nem desafio-resposta (ver `APK.md` §9). A única cifra do app protege o arquivo de preset `.trps`, que o mapa não usa.

**Como ler este mapa**

- **[APK]** = lido no código decompilado, citado como `arquivo:linha` sob `protocolo/apk/jadx-out/sources/br/com/taramps/processador_mini/` (pasta fora do git).
- **[Demo]** = confirmado na interface do Modo Demo (`capturas/demo-telas.md`).
- **[Captura]** = confirmado no ar por HCI snoop log. **Ainda nenhum.** Pela regra da história, uma codificação só conta como provada com a captura citada. Hoje todas são **[APK]**, e a prova em campo depende da fase com o processador (roteiro, Partes 2 a 4).
- Nenhum trecho de código do app é copiado aqui.
- Abreviações: `P` = `ble/Protocol.java`, `BVM` = `ble/BleViewModel.java`, `MBM` = `ble/MyBleManager.java`.

## 1. GATT

| Papel | UUID | Fonte |
|---|---|---|
| Anúncio (filtro do scan) | UUID de 16 bits `0x2055` no anúncio. Não há filtro por nome. | [APK] `BVM:4997`, `ble/BleScanner:132` |
| Serviço | `00000000-cc7a-482a-984a-7f2ed5b3e58f` | [APK] `ble/UuidConfig` |
| Escrita de comandos | `00001122-8e22-4541-9d4c-21edae82ed19` | [APK] `ble/UuidConfig` |
| Respostas (notificação) | `00001123-8e22-4541-9d4c-21edae82ed19`. Se só houver indicação, o app usa indicação. | [APK] `ble/UuidConfig`, `MBM:485` |

- O app **não pede MTU**, não pede bond e não ajusta prioridade de conexão, então fica no MTU padrão (20 bytes úteis) [APK] `MBM:516-551`.
- O tipo de escrita depende das propriedades da característica: as consultas e os ajustes em fluxo vão **sem resposta** quando ela permite; a senha e os "apply" vão **com resposta** [APK] `MBM:575-587`.

## 2. Formato das mensagens

- **Texto ASCII (UTF-8), sem terminador** (nem `\r` nem `\n`) [APK] `MBM:1729`.
- **Leitura:** `#CCC#` (parâmetro global) ou `#CCC#c#` (com canal ou banda).
- **Escrita:** `*CCC*valor*` (global) ou `*CCC*c*valor*`.
- `CCC` = código de função com 3 dígitos. `c` = **índice do canal de 0 a 3** (saída 1 = 0) [APK] `BVM:9554`, ou da banda do EQ gráfico de 0 a 14.
- **Números:**
  - dB, Q, attack e delay vão com **uma casa decimal e ponto**, como `-3.5` (`%.1f`, Locale US) [APK] `P:306`;
  - Hz, ms do release, índices e booleanos vão como **inteiros**.
- **Respostas:**
  - O app aceita `#` ou `*` como separador. Ele troca `*` por `#`, tira os separadores das pontas, divide em segmentos e exige que o primeiro tenha 3 dígitos [APK] `P:405-434`.
  - Os fragmentos de notificação são juntados num buffer e os quadros são separados por `\r`/`\n` ou pelo padrão `#CCC#…#` [APK] `MBM:1946-2023`.
  - A resposta é casada com o pedido pelo código de função e, quando há, pelo canal [APK] `MBM` waiter.
  - **Respostas de leitura:** `[c, valor]` para parâmetro com canal, `[valor]` para global [APK] `BVM:4445-4960`.
  - **Ack de escrita "apply":** payload `OK` [APK] `writeWithAck$2$1:74`.
- **Ritmo:**
  - uma resposta pendente por vez;
  - cerca de 30 ms entre escritas;
  - no máximo um envio a cada 300 ms por parâmetro durante arrasto [APK] `BVM:1608, 2208-3032`.
  - Timeouts típicos: ack em 2 a 10 s, consulta em 5 s. O vigia cancela a fila se uma escrita ficar presa 15 s [APK] `MBM:1505-1664`.

## 3. Conexão e autenticação

Ordem [APK]:
1. conectar (3 tentativas, até 15 s);
2. descobrir o serviço;
3. ligar a notificação;
4. enviar a senha.

- **Senha:** `*000*SENHA*` **em texto claro**, com SENHA = 4 caracteres `[0-9A-Z]` (o app converte para maiúsculas) [APK] `P:385-398`.
- **Resposta:** função `000`. Payload `ERRO` = senha errada [APK] `P:400`. Qualquer outro payload = sucesso, e traz informações do produto, com o firmware nos últimos 4 caracteres [APK] `BVM:4482`.
- **Senha errada:** a conexão continua e dá para tentar de novo dentro de uma janela de tempo. Depois o app desconecta [APK] `BVM:5380-5507`.
- **Sem keep-alive.** O único polling periódico é o RSSI, que é do próprio GATT e não um comando do protocolo [APK] `BVM:5012`.
- **Não há "leitura completa".** O app lê cada parâmetro com um comando próprio, tela a tela (bootstrap preguiçoso), na ordem: Home, Roteamento, Alinhamento, Fase, EQ de entrada, Crossover, Limiter, EQ de saída, Gerador, EQ 15 bandas [APK] `BVM:1615`. Isso **refuta** a hipótese do `protocol-discovery.md` de que o app baixa o estado completo ao conectar.

## 4. Parâmetros do Marco 1

`c` = canal de 0 a 3. Todas as escritas com canal validam 0 a 3 no app, exceto o mute.

| Parâmetro | Leitura | Escrita | Valor no fio | Faixa e passo no app | Fonte |
|---|---|---|---|---|---|
| Master | `#080#` | `*080*v*` | dB, 1 casa | −80,0 a 0,0; 0,1 | [APK] `P:199`, `BVM:6257` · [Demo] passo 0,1 |
| Ganho da saída | `#081#c#` | `*081*c*v*` | dB, 1 casa | −45,0 a +15,0; 0,1 | [APK] `BVM:1517, 4759` · [Demo] passo 0,1 |
| Mute | `#100#c#` | `*100*c*1*` / `0` | 1 = mutado; na leitura, ≠0 = mutado | — | [APK] `P:219`, `BVM:4815` |
| Roteamento | `#010#c#` | `*010*c*a*`, `b` ou `a+b` | texto minúsculo; a leitura aceita também `ab` | A / B / A+B | [APK] `BVM:3928, 3959` · [Demo] |
| Fase | `#060#c#` | `*060*c*0*` / `180` | graus (**0 ou 180**, não 0/1) | — | [APK] `AlinhamentoSliderKnobKt:303, 492` |
| HPF freq. | `#040#c#` | `*040*c*v*` | Hz inteiro | 10 a 22000; 1 | [APK] `BVM:1637, 4581` · [Demo] |
| LPF freq. | `#041#c#` | `*041*c*v*` | Hz inteiro | 10 a 22000; 1 (o setter direto não limita) | [APK] `BVM:4602, 7787` · [Demo] |
| HPF filtro | `#042#c#` | `*042*c*i*` | índice | 0 = OFF; 1–7 = LR12, LR18, LR24, LR30, LR36, LR42, LR48; 8–15 = BT6, BT12, BT18, BT24, BT30, BT36, BT42, BT48 | [APK] `crossover/FilterOptionsKt:12` · [Demo] mesma ordem |
| LPF filtro | `#043#c#` | `*043*c*i*` | índice | igual ao HPF | idem |
| Delay | `#050#c#` | `*050*c*v*` | **cm**, 1 casa (o parâmetro se chama "db" no código, mas é cm) | 0,0 a 275,0; 0,5 | [APK] `BVM:1519, 11329`, `P:175` · [Demo] passo 0,5 |
| Limiter auto | `#070#c#` | `*070*c*1*` / `0` | booleano | — | [APK] `BVM:4698` |
| Limiter threshold | `#071#c#` | `*071*c*v*` | **dB**, 1 casa; **`0.5` = OFF** | −48,0 a 0,0; 0,1; 0,5 = OFF | [APK] `limiter/ChannelLimiterContentKt:1200, 1227` · [Demo] |
| Limiter attack | `#072#c#` | `*072*c*v*` | ms, 1 casa | 0,1 a 100; 0,1 | [APK] `ChannelLimiterContentKt:1330` · [Demo] passo 0,1 |
| Limiter release | `#073#c#` | `*073*c*v*` | ms inteiro | 1 a 1600; 1 | [APK] `ChannelLimiterContentKt:1444` · [Demo] passo 1 |
| EQ entrada, ganho | `#030#` | `*030*v*` | dB, 1 casa; **sem canal** | −12 a +12; 0,1 | [APK] `BVM:4566, 6474` |
| EQ entrada, freq. | `#031#` | `*031*v*` | Hz inteiro | 10 a 22000; 1 | [APK] `models/EqEntradaViewModel:59` |
| EQ entrada, Q | `#032#` | `*032*v*` | Q, 1 casa | 0,4 a 10,0; 0,1 | idem · [Demo] |
| EQ saída, freq. | `#090#c#` | `*090*c*v*` | Hz inteiro | 10 a 22000; 1 | [APK] `BVM:4776` |
| EQ saída, ganho | `#091#c#` | `*091*c*v*` | dB, 1 casa | −12 a +12; 0,1 | [APK] `BVM:7239` |
| EQ saída, Q | `#092#c#` | `*092*c*v*` | Q, 1 casa | **0,1** a 10,0; 0,1 (o BVM não limita) | [APK] `eq_saida/ChannelEqSaidaContentKt:1348`, `BVM:8146` · [Demo] aceita 0,1 |
| EQ gráfico, banda b | `020#b` (sem `#` inicial) | `020*b*v` (**sem `*` inicial nem final**) | dB, 1 casa; b = 0 a 14 | bandas 25, 40, 63, 100, 160, 250, 400, 630 Hz, 1k, 1,6k, 2,5k, 4k, 6,3k, 10k, 16k; −12 a +12; 0,1 | [APK] `P:49, 139`, `BVM:1507, 4553` · [Demo] |
| Preset de EQ | `#220#` | `*220*i*` | índice | 0 Flat, 1 Loudness, 2 Bass Boost, 3 Mid-Bass Boost, 4 Treble Boost, 5 Powerful, 6 Electronic, 7 Rock Style, 8 Hip-Hop Style, 9 Pop Music, 10 Vocal, 11 Competition, 255 Custom | [APK] `models/Eq15BandasViewModel:75` |

Também lidos do código, fora do Marco 1:
- **ON/OFF da Home** = `*252*1*`/`0`. É o *remote out*, que liga os amplificadores; não existe outro comando de energia [APK] `screens/home/SourceOnKt:190`.
- **Gerador:**
  - 200 = ganho do tom fixo, em dB (−60 a 0);
  - 201 = frequência fixa;
  - 202 = liga o gerador (1/0);
  - 210 e 211 = frequências inicial e final do sweep;
  - 212 = ganho do sweep;
  - 213 = velocidade (0 desligado, 1 lento, 2 médio, 3 rápido).
- **Voltímetro:** 005, em volts × 10.
- **Clip:** 150 = clip das saídas, um bit por canal; 111 = clip da entrada. O app não pede esses três; o aparelho provavelmente os envia sozinho.
- **Audio Classic (A2DP):** 251.

## 5. Comandos que mexem no aparelho inteiro (para o guardião)

| Comando | Efeito | Observação |
|---|---|---|
| `*004*` | **Reset de fábrica** (apaga senha e presets) | O guardião deve negar sempre. |
| `*242*` | **Reset das configurações de áudio** | Estado resultante imprevisível = trava de dano. |
| `*241*n*` | **Carrega a memória n** (n = 0 a 2) do DSP | Estado resultante imprevisível sem ler a memória antes. |
| `*240*n*` | Salva o estado na memória n | Grava preset (CAP-9). |
| `*001*antiga*nova*` | **Troca a senha** | Área protegida (CAP-7). |
| `*002*nome*` | Troca o nome do produto (até 12 caracteres) | — |
| `231*NOME`, `*231*nome*` (e 232, 233) | Nomeia as memórias 1 a 3 | — |
| `*202*1*`, `*200*`, `*201*`, `*21x*` | Gerador de sinal | Trava de dano (damage-thresholds, princípio 5). |
| `*252*0*` / `1` | Liga e desliga o remote out (amplificadores) | — |

## 6. Link de canais

É **lógica só do app**; não existe comando de link no protocolo [APK] `models/ChannelLinkViewModel:76`, `BVM:16290`. Os pares possíveis são 1–2 e 3–4, e ficam guardados só no celular. Ao vincular, o app lê todos os parâmetros do canal mestre e os escreve no outro. Um cliente no notebook não herda o link: o guardião precisa tratar cada canal como independente e, se quiser o link, implementá-lo.

## 7. Limiter: unidade do threshold e posição em relação ao ganho

| Item | Resposta | Evidência |
|---|---|---|
| Unidade do threshold no protocolo | **dB**, de −48,0 a 0,0, com 1 casa; `0.5` = OFF | [APK] `P:187`, `ChannelLimiterContentKt:1200, 1227` |
| Conversão para volts que o app mostra | **Só na interface:** V = 2,0 × 10^(dB/20), ou 2,1 V quando está em OFF. Nada em volts vai no fio. | [APK] `limiter/GraphView:336` · [Demo] 11 pontos batem com a fórmula |
| Os volts mostrados mudam com o ganho da saída ou com o master? | **Não.** A escala é fixa (0 dB = 2,00 V). | [Demo] D2: ganho +1,0 dB e master −1,0 dB, e o valor ficou em 1.00V |
| O limiter atua antes ou depois do ganho da saída? | **Ainda não determinado.** O app trata o threshold como tensão absoluta na saída, o que sugere "depois do ganho". Mas isso é só a interface, não o DSP. | Depende da medição com multímetro (roteiro 4.2), na fase com o processador |

## 8. Lacunas que só uma captura com o processador resolve

1. Se os bytes no ar são os que o código monta, inclusive a senha em claro (C0).
2. O formato exato das respostas: separador `#` ou `*`, presença do canal, número de casas, ack `OK`, eco nas escritas sem resposta.
3. As propriedades reais das características 1122 e 1123, e se o firmware exige pareamento ou criptografia do enlace.
4. Se `020` sem separadores devolve uma banda ou as 15.
5. Se o aparelho envia sozinho 005, 150 e 111, e com que período.
6. O valor do attack no modo Auto: o app manda `0.05` ou `0.01` com 2 casas, sem a formatação usual.
7. Se `*220*255*` (Custom) é aceito.
8. A posição física do limiter em relação ao ganho (medição 4.2).

Com o código em mãos, a fase com o processador encolhe: basta a C0 e uma captura curta conferindo 3 ou 4 parâmetros (ganho, threshold, filtro, EQ 15 bandas), em vez das capturas C1 a C15.
