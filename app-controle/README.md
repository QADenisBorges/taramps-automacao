# BGS Controle (POC 1)

App Android próprio para controlar o PRO 2.4BT + AUDIO pelo Bluetooth do celular.
Feito em .NET MAUI (C#), alvo só Android. Contrato: `_bmad-output/specs/spec-app-controle-taramps/SPEC.md`.
Protocolo: `protocolo/MAPA.md`.

Nesta versão (histórias 1 e 2) o app **procura, conecta, autentica, mostra o firmware e lê o
aparelho inteiro**: logo depois da senha aceita, faz a leitura completa (84 consultas do Marco 1
e 15 extras) e mostra o placar ("84 de 84") e a lista de valores. Nenhuma escrita sai do
celular, nem com o R003.

## Projetos

| Projeto | O que é |
|---|---|
| `src/BgsControle.Nucleo` | `net10.0`, sem Android: quadros do protocolo, autenticação, sessão (máquina de estados e trava de escrita), catálogo de parâmetros (consulta, decodificação, codificação de escrita), leitura completa e retrato, log com a senha mascarada. |
| `src/BgsControle.App` | MAUI `net10.0-android`: BLE pela API do Android, senha no SecureStorage, tela. |
| `tests/BgsControle.Nucleo.Testes` | xUnit com transporte e cofre falsos. Roda no notebook, sem celular. |

## Ferramentas

- SDK .NET 10 e o workload `maui-android` (`dotnet workload install maui-android`).
- JDK 17 e Android SDK em `%LOCALAPPDATA%\BgsFerramentas`. Os dois caminhos estão fixados no
  `Directory.Build.props`, para o build nunca pegar o JDK 11 da máquina.
- Para instalar ou atualizar o Android SDK (aceita as licenças):

```
dotnet build src/BgsControle.App -t:InstallAndroidDependencies -f net10.0-android ^
  -p:AndroidSdkDirectory=%LOCALAPPDATA%\BgsFerramentas\android-sdk ^
  -p:JavaSdkDirectory=%LOCALAPPDATA%\BgsFerramentas\jdk-17.0.20.1+1 ^
  -p:AcceptAndroidSDKLicenses=True
```

## Testar

```
dotnet test tests/BgsControle.Nucleo.Testes
```

## Compilar

```
dotnet build src/BgsControle.App -f net10.0-android -c Debug
```

O APK assinado sai em `src/BgsControle.App/bin/Debug/net10.0-android/br.com.bgs.controle-Signed.apk`,
com os assemblies embutidos (roda sem o Visual Studio).

## Instalar no celular

Use só o adb de `C:\platform-tools` (o do Android SDK não é usado):

```
C:\platform-tools\adb.exe install -r src\BgsControle.App\bin\Debug\net10.0-android\br.com.bgs.controle-Signed.apk
```

## Conferir no celular

Feche o app da Taramps antes: só cabe uma conexão BLE por vez.

1. Conectar com a senha certa → "Conectado · firmware R003".
2. "Esquecer a senha", Conectar, digitar uma senha errada → "Senha errada".
3. Digitar a certa, fechar o app e reabrir, Conectar → conecta sem pedir a senha.
4. Procurar a senha no logcat: `C:\platform-tools\adb.exe logcat -d | findstr /i SENHA`
   (troque SENHA pela senha real). Nenhuma ocorrência.
5. Depois de conectar, a leitura começa sozinha: placar "84 de 84" em ~15 s. Conferir na lista
   alguns valores da memória carregada (FILME: delay saída 1 = 0, saída 2 = 100, saídas 3 e 4 =
   80 cm; fase das saídas 3 e 4 = 180°). "Ler de novo" refaz a leitura.
6. O último retrato fica em JSON nos arquivos privados do app (`ultimo-retrato.json`); o
   `run-as` só funciona com o APK Debug (depurável):
   `C:\platform-tools\adb.exe exec-out run-as br.com.bgs.controle cat files/ultimo-retrato.json`.

O botão "Mostrar o registro" mostra o log do BLE na tela, com a senha como `*000*••••*`.

## Regras que o código garante

- A senha (4 caracteres `[0-9A-Z]`) só sai depois de a notificação estar ligada, e só no
  estado Autenticando. Fora do formato, nada é enviado.
- A senha só é guardada depois de aceita. Senha errada apaga a guardada; 3 recusas desconectam;
  60 s sem digitar desconectam.
- Firmware diferente de R003, ou produto desconhecido, entra em modo só leitura.
- A trava de escrita (`Sessao.EnviarAsync`) é a única porta para o BLE e só deixa passar as
  leituras do catálogo, cada código no seu escopo (com a sessão conectada), e o quadro da senha.
  Uma leitura fora do catálogo (`#004#`, `#081#4#`) e toda escrita são recusadas antes do BLE.
- A leitura completa faz uma consulta por vez, ~30 ms entre envios, 5 s de prazo e uma nova
  tentativa para a consulta do Marco 1 que falhou (os extras são perguntados uma vez). Se a
  conexão cair, para e o retrato fica "incompleto".

## Risco conhecido

Não se sabe se o firmware bloqueia depois de várias senhas erradas. O app para em 3 por conexão.
