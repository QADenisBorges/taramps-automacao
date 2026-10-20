---
title: 'História 1: esqueleto do app Android e conexão com o processador'
type: 'feature'
created: '2026-10-19'
status: 'review'
baseline_commit: 'ea539943d446206e35aecf0202c266eb832c352d'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/SPEC.md'
  - '{project-root}/protocolo/MAPA.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** O POC 1 ainda não existe. Sem um app que encontre, conecte e autentique no PRO 2.4BT + AUDIO, nenhuma outra capacidade do SPEC tem onde morar (CAP-1).

**Approach:** Criar do zero um app Android em .NET MAUI na pasta `app-controle/`. Ele procura o processador pelo BLE, conecta, envia a senha guardada no celular, mostra o firmware e entra em modo só leitura se o firmware não for R003.

**Decisões do Denis (19/10/2026):** tecnologia .NET MAUI (C#, alvo só Android); pasta `app-controle/`; a IA instala as ferramentas (workload MAUI, JDK 17, Android SDK) e aceita a licença do Android SDK em nome dele.

## Boundaries & Constraints

**Always:**
- Protocolo pelo MAPA §1–§3: anúncio `0x2055`; serviço `…e58f`; escrita `1122` (com resposta); notificação `1123`; texto ASCII sem terminador; MTU padrão; ordem conectar (3 tentativas, até 15 s cada) → descobrir → ligar notificação → `*000*SENHA*`, uma operação GATT por vez (a senha só sai depois de a notificação ser ligada com sucesso).
- Quadros: os fragmentos são juntados e recortados por `\r`/`\n` ou pelo padrão `#CCC#…#` (MAPA §2, §9). A resposta da senha é casada só pela função `000`; um quadro que chega antes da senha é órfão, nunca resposta.
- Senha = 4 caracteres `[0-9A-Z]`. O app tira os espaços e converte para maiúsculas. Uma senha fora do formato não sai pelo BLE. Sem senha guardada, o app pede a senha **antes** de conectar, para não segurar a única conexão BLE.
- Resposta `000`: o payload `ERRO` = senha errada. Qualquer outro payload = aceita, e o firmware são os 4 últimos caracteres do payload sem espaços (`#000#PRO2.4MIR003#` → `R003`). Um payload cortado (prefixo de `ERRO`, ou vazio) espera o resto e não conta como aceito.
- Firmware ≠ `R003`, ou payload sem o prefixo `PRO2.4MI` → modo só leitura, com aviso fixo na tela.
- Trava de escrita num ponto único do código, por lista de permitidos: passam as leituras `#CCC#`, `#CCC#c#` e `020#b`, e o quadro da senha só no estado Autenticando. Todo o resto (inclusive `020*b*v`, sem `*` no início) é recusado antes do BLE. Nesta história nenhuma escrita é liberada, nem com R003; no modo só leitura a trava continua fechada nas histórias seguintes.
- A senha só é guardada depois de aceita, no `SecureStorage` do MAUI (cifrado pelo Android Keystore), com `allowBackup=false`. Um erro do Keystore ao ler conta como "sem senha guardada" e apaga a entrada. Ela nunca aparece na tela (campo mascarado, sem botão "mostrar"), no logcat nem no log do app (onde o quadro vira `*000*••••*`).
- Senha errada: nada além do quadro da senha é enviado. A senha guardada é apagada, o app pede a senha de novo (prazo de 60 s para digitar, senão desconecta) e aceita até 3 tentativas por conexão. Depois disso, desconecta. Risco registrado: não se sabe se o firmware bloqueia depois de várias senhas erradas.
- Todo o código é novo. `ferramentas/` serve só de consulta.

**Never:**
- Ler parâmetros, escrever ajustes, salvar memórias ou abrir a porta da IA (histórias seguintes).
- Usar um `applicationId` ou uma marca da Taramps.
- Ler ou decifrar `.trps`.

## I/O & Edge-Case Matrix

| Cenário | Entrada / estado | Esperado | Erro |
|---|---|---|---|
| Caminho feliz | Senha certa; resposta `#000#PRO2.4MIR003#` | Tela "Conectado · firmware R003"; senha guardada | — |
| Senha guardada | 2ª abertura | Conecta sem pedir a senha | — |
| Senha errada | Resposta `#000#ERRO#` | "Senha errada"; só o quadro da senha foi escrito | Apaga a senha guardada e pede de novo |
| 3 erros | 3 × `ERRO` | Desconecta | "Senha recusada 3 vezes" |
| Firmware diferente | `#000#PRO2.4MIR004#` | "R004 · modo só leitura" + aviso | Escrita → recusada sem envio |
| Outro produto | `#000#XYZR003#` | Modo só leitura + aviso "produto desconhecido" | — |
| Órfão | `#005#120#` antes da senha | Ignorado; espera a `000` | — |
| Formato inválido | `12a`, `12345`, `12-4` | Nada é enviado | "A senha tem 4 letras ou números" |
| Sem resposta | Nenhuma `000` em 5 s | Desconecta | "O processador não respondeu à senha" |
| Fragmentado | `#000#PRO2.4` + `MIR003#` | Junta e aceita R003 | — |
| Não encontrado | Scan de 10 s sem `0x2055` | Para | "Não achei o processador. O app da Taramps está fechado?" |
| Queda | Desconexão no meio | Estado Desconectado | "A conexão caiu" + botão Conectar |
| Bluetooth/permissão | BT desligado ou permissão negada | Pede ao Android | Mensagem explicando o que liberar |

</frozen-after-approval>

## Code Map

- `protocolo/MAPA.md` §1–§3, §9 -- UUIDs, formato, ordem de conexão, resposta real da `000`.
- `ferramentas/cliente_ble.py` (`autenticar`, `procurar`) e `ferramentas/protocolo_taramps.py` (`montar_autenticacao`, montador de quadros) -- só consulta: filtro pelo anúncio `0x2055` ou pelo serviço; `ERRO` cortado tratado como incompleto.
- `C:\platform-tools\adb.exe` -- adb existente, para instalar o APK no celular (Samsung, One UI 6).
- Ambiente: SDK .NET 10.0.401 e Visual Studio já instalados; falta o workload `maui-android`, o JDK 17 e o Android SDK.
- Não tocar em `ferramentas/`, `estado/` nem `protocolo/`.

## Tasks & Acceptance

**Execution:**
- [x] Ferramentas -- instalar o workload `maui-android` e o Microsoft OpenJDK 17 (zip em `%LOCALAPPDATA%`); **depois de criar o projeto do app**, rodar o target `InstallAndroidDependencies` com `-p:AndroidSdkDirectory`, `-p:JavaSdkDirectory=<JDK 17>` e `-p:AcceptAndroidSDKLicenses=True`. Fixar os dois caminhos no `Directory.Build.props` para o JDK 11 nunca ser escolhido.
- [x] `app-controle/` (`BgsControle.slnx`, `Directory.Build.props`, `.gitignore`, `README.md`) -- solução com três projetos; `net10.0`; o README diz como compilar, testar e instalar.
- [x] `app-controle/src/BgsControle.Nucleo/` (class library `net10.0`, sem Android) -- `Protocolo/Quadros.cs` (montar a senha; montador de quadros a partir dos fragmentos; quebra em código e segmentos), `Protocolo/Autenticacao.cs` (Aceita(firmware) / Recusada / Incompleta; `FirmwareSuportado = "R003"`), `Enlace/ITransporte.cs` (procurar, conectar, escrever, evento de notificação, queda), `Sessao/Sessao.cs` (máquina de estados Desconectado → Procurando → Conectando → Autenticando → PedindoSenha / Conectado(firmware, soLeitura); porta única de escrita com a trava; até 3 tentativas), `Seguranca/ICofreSenha.cs`, `Registro/LogBle.cs` (log em memória com a senha mascarada).
- [x] `app-controle/src/BgsControle.App/` (MAUI `net10.0-android`, minSdk 26, targetSdk padrão do .NET 10, `EmbedAssembliesIntoApk=true` também no Debug, `ApplicationId` `br.com.bgs.controle`, nome "BGS Controle") -- `Platforms/Android/TransporteAndroid.cs` (API `Android.Bluetooth` direta: scan com filtro `0x2055`, no máximo 5 scans a cada 30 s; `ConnectGatt(..., TRANSPORT_LE)`; `Close()` depois de cada falha (ex.: status 133), com pausa antes da próxima tentativa; serviço ausente (cache do GATT) conta como tentativa; descritor 0x2902; escrita com resposta pelas APIs do Android 13+ com volta para as antigas; callbacks levados para a sessão sem bloquear a thread do Binder; nada do valor escrito vai ao logcat), `CofreSecureStorage.cs`, permissões no manifesto (`BLUETOOTH_SCAN` neverForLocation e `BLUETOOTH_CONNECT`; `BLUETOOTH`, `BLUETOOTH_ADMIN` e `ACCESS_FINE_LOCATION` com `maxSdkVersion=30`; BT desligado → `ACTION_REQUEST_ENABLE`), `ConexaoViewModel.cs` e `MainPage.xaml` (botão Conectar, estado, firmware, campo de senha mascarado, faixa "modo só leitura", botão "Esquecer a senha", pedidos de permissão e de ligar o BT).
- [x] `app-controle/tests/BgsControle.Nucleo.Testes/` (xUnit `net10.0`) -- testes com transporte e cofre falsos para todas as linhas da matriz, mais: o log nunca contém a senha em claro.

**Acceptance Criteria:**
- Given a senha certa e o firmware R003, when o Denis toca em Conectar, then a tela mostra "R003" e o modo normal.
- Given qualquer resposta `ERRO`, when a sessão termina, then o transporte falso registrou só quadros `*000*…*`.
- Given um firmware simulado ≠ R003, when uma escrita é pedida, then ela é recusada e o transporte não recebe nada.
- Given o app rodando, when o log do app e o logcat são inspecionados, then a senha não aparece.

## Implementation Notes

- Ferramentas (19/10, 18:30): JDK 17 extraído em `C:\Users\denis\AppData\Local\BgsFerramentas\jdk-17.0.20.1+1` (SHA-256 conferido com o da Microsoft). O workload `maui-android` está sendo instalado por um processo à parte (`dotnet workload install maui-android`, iniciado às 18:17): não rode outra instalação de workload; espere `dotnet workload list` mostrar `maui-android`. O Android SDK vai em `C:\Users\denis\AppData\Local\BgsFerramentas\android-sdk`. O adb a usar é o `C:\platform-tools\adb.exe`. O celular não está conectado ao notebook agora; instalar no aparelho fica para o teste do Denis.
- Ferramentas (20/10): o disco C: encheu durante a instalação do workload em 19/10. Depois que o Denis liberou espaço, o `maui-android` (10.0.110) e o Android SDK (`platforms/android-36`, build-tools, platform-tools) foram instalados; as licenças foram aceitas pelo `InstallAndroidDependencies`.
- Build (20/10): o primeiro build do app deu 6 erros de compilação, corrigidos: faltava o `using Microsoft.Extensions.Logging` (`ClearProviders`); o `OperationCanceledException` era ambíguo com `Android.OS`; a constante de sucesso das escritas do Android 13+ é `CurrentBluetoothStatusCodes.Success` (a `BluetoothStatusCodes.Success` está obsoleta como erro). Resultado: 0 erros e 0 avisos, `br.com.bgs.controle-Signed.apk` (84 MB, Debug, arm64-v8a e x86_64, assemblies embutidos).
- Conferido no APK (`aapt`): minSdk 26, targetSdk 36, `allowBackup=false`, `BLUETOOTH_SCAN` com neverForLocation, as três permissões antigas com `maxSdkVersion=30`. O MAUI acrescenta `INTERNET` por padrão; o app não usa a rede.
- Pendente: o teste manual no celular (seção Verification), que é do Denis. O celular não estava conectado ao notebook.

## Spec Change Log

## Review Triage Log

## Design Notes

O núcleo (protocolo, sessão, trava, log) fica num projeto `net10.0` sem Android, atrás da interface `ITransporte`. Assim, os testes rodam no notebook com `dotnet test`, sem o aparelho e sem o Android SDK. O firmware "simulado" vem do transporte falso dos testes; o app real só fala com o aparelho real. O BLE usa a API do Android direto, sem biblioteca de terceiros, para controlar o que vai ao logcat e evitar incompatibilidade com o .NET 10.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos os testes passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: APK assinado `br.com.bgs.controle-Signed.apk` em `bin/Debug/net10.0-android/`, com os assemblies embutidos.
- `C:\platform-tools\adb.exe install -r <apk>` -- expected: "Success" (um único adb; o do Android SDK não é usado).

**Manual checks (if no CLI):**
- No celular, com o app da Taramps fechado: conectar com a senha certa → "R003"; "Esquecer a senha" e digitar uma errada → "Senha errada"; digitar a certa, fechar e reabrir → conecta sem pedir a senha.
- `adb logcat -d` depois do teste, buscando a senha: nenhuma ocorrência.
