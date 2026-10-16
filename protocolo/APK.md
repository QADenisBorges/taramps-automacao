# Análise estática do APK — PRO 2.4BT + AUDIO

**Status:** pendente. O APK ainda não foi extraído (ROTEIRO-CAPTURA.md, 1.3) e o jadx ainda não foi baixado. A IA baixa o jadx do release oficial (`github.com/skylot/jadx/releases`) só com a autorização do Denis no momento do download.

**Regras deste arquivo**

- Só descrições com palavras próprias. Nenhum trecho de código decompilado, nenhum recurso do app.
- Nomes de classe e de método aparecem só como localizador ("o montador fica na classe X"), nunca com o corpo.
- O APK e a saída do jadx ficam em `protocolo/apk/`, que está no `.gitignore`.

## 1. Identificação

| Campo | Valor |
|---|---|
| Pacote | pendente (hipótese da spec: `com.taramps.pro1`, não confirmada) |
| Versão (`versionName` / `versionCode`) | pendente (o app mostra 1.0 (23); manual, p. 15) |
| Arquivos (`base.apk`, splits) | pendente |
| SHA-256 de cada arquivo | pendente |
| Origem | `adb pull` do celular do Denis |
| Ferramenta | jadx, versão pendente, baixado em pendente |
| Data da análise | pendente |

## 2. Framework do app

Como identificar, nesta ordem:

| Sinal no APK | Framework | Consequência para o mapa |
|---|---|---|
| `lib/*/libflutter.so` e `libapp.so` | Flutter | Lógica compilada em `libapp.so`; o jadx não a mostra. O mapa depende mais das capturas e das strings do `libapp.so`. |
| `assets/index.android.bundle` | React Native | Lógica no bundle JS (ou bytecode Hermes). Ler o bundle no lugar do jadx. |
| `assemblies/` ou `libmonodroid.so` | Xamarin / .NET MAUI | Lógica em DLLs .NET. |
| Só `classes*.dex` com código do app | Java/Kotlin nativo | O jadx mostra a lógica direto. |

Resultado: pendente.

## 3. Biblioteca BLE

Pendente. Procurar o uso de `BluetoothGatt` ou de bibliotecas conhecidas (Nordic Android BLE, FastBle, RxAndroidBle, flutter_blue_plus, react-native-ble-plx) e descrever: tipo de escrita (com ou sem resposta), uso de notificação ou indicação, MTU pedido e fila de comandos.

## 4. UUIDs GATT

| UUID | Papel (serviço, escrita, notificação, leitura) | Onde aparece |
|---|---|---|
| pendente | | |

Conferir contra a descoberta de serviços da captura C0 (`btsnoop_att.py` lista o GATT quando o Android não usa o cache).

## 5. Montadores de comando

Pendente. Descrever o formato de cada quadro que o app monta: cabeçalho, código do comando, endereço do canal ou da banda, valor, tamanho e verificação (checksum, CRC, contador). Um item por família de comando (autenticação, leitura completa, escrita de parâmetro, preset).

## 6. Conversões

| Grandeza | Conversão app → protocolo | Faixa | Onde aparece |
|---|---|---|---|
| dB (ganho, master, EQ, threshold) | pendente | | |
| Hz (crossover, PEQ) | pendente | | |
| cm (delay) | pendente | | |
| V (threshold do limiter) | pendente | | |
| Q, ataque, release | pendente | | |

## 7. Autenticação

Pendente. Como a senha de 4 dígitos é enviada (texto, número, hash, desafio-resposta) e o que o processador responde.

## 8. Arquivo de preset

Pendente. Formato do arquivo exportado (JSON, XML, binário), se é o mesmo bloco que trafega no BLE e se tem verificação.

## 9. Veredito sobre proteção

Critérios (a história para no veredito "protegido"):

| Veredito | Sinais | Ação |
|---|---|---|
| Livre | Payloads montados direto dos valores, sem cifra no caminho BLE | Seguir para o mapa |
| Ofuscação leve | Nomes curtos do R8/ProGuard, mas a lógica é legível | Seguir para o mapa |
| Protegido | Cifra (AES, `javax.crypto`), HMAC, assinatura ou desafio-resposta no caminho de escrita; strings cifradas (DexGuard); código nativo empacotado; ou payload da captura que muda sem relação com o valor | Parar e recomendar procurar a Taramps, com a evidência |

Veredito: pendente (depende do APK e da captura C0).
