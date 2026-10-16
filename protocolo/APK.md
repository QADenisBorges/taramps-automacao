# Análise estática do APK — PRO 2.4BT + AUDIO

**Status:** análise concluída em 2026-10-16. A saída do jadx fica em `protocolo/apk/jadx-out/`, fora do git. A IA baixa o jadx do release oficial (`github.com/skylot/jadx/releases`) só com a autorização do Denis no momento do download.

**Regras deste arquivo**

- Só descrições com palavras próprias. Nenhum trecho de código decompilado, nenhum recurso do app.
- Nomes de classe e de método aparecem só como localizador ("o montador fica na classe X"), nunca com o corpo.
- O APK e a saída do jadx ficam em `protocolo/apk/`, que está no `.gitignore`.

## 1. Identificação

| Campo | Valor |
|---|---|
| Pacote | `com.taramps.pro24bt_audio` (a hipótese `com.taramps.pro1` estava errada); código em `br.com.taramps.processador_mini`, atividade `MainActivity` |
| Versão (`versionName` / `versionCode`) | 1.0.8 / 12; minSdk 26, targetSdk 36; instalado pela Play Store em 2026-10-12 |
| Arquivos (`base.apk`, splits) | `base.apk` (70,7 MB), `split_config.arm64_v8a.apk`, `split_config.pt.apk`, `split_config.xxhdpi.apk` |
| SHA-256 de cada arquivo | base `68c9e42e…87c9e1`; arm64 `7c8e0ab4…befde246`; pt `8bd8c99a…a78b2917`; xxhdpi `6dde9824…a5991a` |
| Origem | `adb pull` do celular do Denis |
| Ferramenta | jadx 1.5.6 (release oficial do GitHub, SHA-256 `545ea2be…a230e8974`, conferido com o digest do GitHub), baixado em 2026-10-16 com autorização do Denis; rodou no JDK 11 |
| Data da análise | 2026-10-16 |

## 2. Framework do app

Como identificar, nesta ordem:

| Sinal no APK | Framework | Consequência para o mapa |
|---|---|---|
| `lib/*/libflutter.so` e `libapp.so` | Flutter | Lógica compilada em `libapp.so`; o jadx não a mostra. O mapa depende mais das capturas e das strings do `libapp.so`. |
| `assets/index.android.bundle` | React Native | Lógica no bundle JS (ou bytecode Hermes). Ler o bundle no lugar do jadx. |
| `assemblies/` ou `libmonodroid.so` | Xamarin / .NET MAUI | Lógica em DLLs .NET. |
| Só `classes*.dex` com código do app | Java/Kotlin nativo | O jadx mostra a lógica direto. |

Resultado: **Java/Kotlin nativo.** São 5 `classes*.dex` (cerca de 66 MB) e nenhum `libflutter.so`, `libapp.so`, bundle JS ou `assemblies/`. As únicas bibliotecas nativas são do AndroidX e de USB (`libusb-lib.so`). O jadx deve mostrar a lógica direto. Os textos do app indicam extensão de preset `.trps` e a ação "Compartilhar preset".

## 3. Biblioteca BLE

**Nordic Android BLE Library** (`ble/MyBleManager`, que estende o BleManager).

- **Tipo de escrita:** depende das propriedades da característica. Vai sem resposta quando permitido; vai com resposta na senha e nos "apply".
- **Notificação:** sim, ou indicação se for a única opção.
- **MTU:** não é pedido.
- **Fila:** mutexes garantem uma resposta pendente por vez.

Detalhes no `MAPA.md` §1–§3.

## 4. UUIDs GATT

| UUID | Papel (serviço, escrita, notificação, leitura) | Onde aparece |
|---|---|---|
| `00000000-cc7a-482a-984a-7f2ed5b3e58f` | serviço | `ble/UuidConfig` (SERVICE_UUID) |
| `00001122-8e22-4541-9d4c-21edae82ed19` | escrita de comandos | `ble/UuidConfig` (WRITE_CHAR_UUID) |
| `00001123-8e22-4541-9d4c-21edae82ed19` | notificação das respostas | `ble/UuidConfig` (NOTIFY_CHAR_UUID) |

Conferir contra a descoberta de serviços da captura C0 (`btsnoop_att.py` lista o GATT quando o Android não usa o cache).

## 5. Montadores de comando

Todos os comandos são montados em `ble/Protocol` como texto ASCII:

- **Leitura:** `#CCC#` ou `#CCC#c#`.
- **Escrita:** `*CCC*valor*` ou `*CCC*c*valor*`.
- **Exceções:** o EQ de 15 bandas (`020#b` / `020*b*v`) e os nomes das memórias (`231*NOME`) vão sem separador inicial.
- **Sem cabeçalho binário**, checksum, CRC ou contador.

A tabela completa por parâmetro está no `MAPA.md` §4–§5.

## 6. Conversões

| Grandeza | Conversão app → protocolo | Faixa | Onde aparece |
|---|---|---|---|
| dB (ganho, master, EQ, threshold) | Texto com 1 casa e ponto (`%.1f`, Locale US); o threshold usa `0.5` para OFF | −80..0 / −45..+15 / ±12 / −48..0 | `ble/Protocol` fmtDb |
| Hz (crossover, PEQ) | Inteiro | 10..22000 | `ble/BleViewModel` sanitizeFreq |
| cm (delay) | cm com 1 casa; a conversão para ms (cm / 34,376) existe só na UI | 0..275, passo 0,5 | `BleViewModel` sanitizeChannelAlign, `AlinhamentoSliderKnobKt` |
| V (threshold do limiter) | **Não vai no fio**; a UI mostra 2,0 × 10^(dB/20), ou 2,1 V em OFF | — | `screens/ajustes/limiter/GraphView` rmsText |
| Q, ataque, release | Q e ataque com 1 casa; release inteiro | Q 0,4..10 (entrada) / 0,1..10 (saída); 0,1..100 ms; 1..1600 ms | `ChannelLimiterContentKt`, `ChannelEqSaidaContentKt` |

## 7. Autenticação

A senha vai **em texto claro**, como comando `*000*SENHA*`. Antes de enviar, o app normaliza a senha: tira espaços, converte para maiúsculas e exige exatamente 4 caracteres `0-9A-Z` (`ble/Protocol`, normalizePin). A senha está certa quando a resposta tem a função `000` e o payload é diferente de `ERRO` (isPasswordOk). Não há hash nem desafio-resposta.

## 8. Arquivo de preset

O `.trps` é um JSON com o conteúdo **cifrado** no campo `ctB64`. A cifra é AES-GCM com uma chave fixa embutida no app (`state/PresetLocalRepository`, staticKeySpec). A chave não é registrada neste repositório. Os presets do celular ficam em `Android/data/com.taramps.pro24bt_audio/files/presets/`, onde o `adb` consegue ler. Para exportar, o app copia o arquivo para o cache e compartilha via FileProvider.

O mapa **não depende** desses arquivos, porque o protocolo BLE lê cada parâmetro diretamente. Decifrar os `.trps` ficou fora da história: seria contornar uma proteção do fabricante sem necessidade.

## 9. Veredito sobre proteção

Critérios (a história para no veredito "protegido"):

| Veredito | Sinais | Ação |
|---|---|---|
| Livre | Payloads montados direto dos valores, sem cifra no caminho BLE | Seguir para o mapa |
| Ofuscação leve | Nomes curtos do R8/ProGuard, mas a lógica é legível | Seguir para o mapa |
| Protegido | Cifra (AES, `javax.crypto`), HMAC, assinatura ou desafio-resposta no caminho de escrita; strings cifradas (DexGuard); código nativo empacotado; ou payload da captura que muda sem relação com o valor | Parar e recomendar procurar a Taramps, com a evidência |

Veredito: **Livre no caminho BLE.** Os comandos são texto ASCII montados direto dos valores (`ble/Protocol`), sem cifra, assinatura, contador nem desafio-resposta. No pacote do app, a única cifra é a do arquivo de preset (AES-GCM, chave fixa), que fica fora do caminho BLE. As outras ocorrências de `javax.crypto` vêm de bibliotecas de terceiros (Tink, okio, media3). Os nomes de classes e métodos do app não estão ofuscados. Falta só confirmar numa captura real (C0) que os bytes no ar são os mesmos que o código monta.
