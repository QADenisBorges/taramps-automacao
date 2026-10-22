# BGS Controle (POC 1)

App Android próprio para controlar o PRO 2.4BT + AUDIO pelo Bluetooth do celular.
Feito em .NET MAUI (C#), alvo só Android. Contrato: `_bmad-output/specs/spec-app-controle-taramps/SPEC.md`.
Protocolo: `protocolo/MAPA.md`.

Nesta versão (até a história 5) o app **procura, conecta, autentica, mostra o firmware e lê o
aparelho inteiro**: logo depois da senha aceita, faz a leitura completa (84 consultas do Marco 1
e 15 extras) e mostra o placar ("84 de 84") e a lista de valores. A história 4 trouxe as telas
testáveis sem MAUI e o processador simulado dos testes.

Desde a história 5, o núcleo **escreve os ajustes de áudio do MAPA §4** (só eles), pelo
`Escritor` (eco, releitura, ritmo de 300 ms por parâmetro) e sempre pela trava da sessão: com o
R003, fora do modo só leitura e com a chave de acesso "Ajustes de áudio" ligada. Remote
out, Bluetooth de áudio, nome do produto, senha e resets continuam recusados antes do BLE.
As chaves de acesso (`IChaves`) ficam em JSON nos arquivos privados. Os ViewModels do controle
de parâmetro e da tela de chaves existem em `BgsControle.Telas`; o XAML é das histórias 10 e 11.

Desde a história 6, a trava deixa passar também `*240*n*` e `*23N*NOME*` (chave "Salvar
memória") e `*241*n*` (chave "Carregar memória"), só na forma exata. O `GerenteMemorias` salva,
carrega e nomeia as 3 memórias. O salvar envia o 240 e depois o nome: com a resposta `#240#n#`
(medida em 20/10, ~1,17 s) diz "Memória N salva"; sem ela, "enviada (o aparelho não confirma o
salvar)". Enquanto uma operação de memória anda ou ficou pela metade, as escritas de áudio e a
cópia do link são recusadas. A `CopiaAntes` guarda o estado e a marca da operação em
`copia-antes.json` e, se a operação cair no meio, mostra "aplicado pela metade" na conexão
seguinte (Terminar ou Voltar à cópia); o `LinkCanais` (1–2 e 3–4, só do app) fica em
`link-canais.json`. Os ViewModels são o `MemoriasViewModel` e o `LinkViewModel`.

Desde a história 7a, a trava deixa passar os comandos do gerador (`*200*`, `*201*`, `*210*` a
`*213*` e `*202*1*`) só na forma exata e com a chave "Gerador"; o `*202*0*` (desligar) passa sem
a chave. O `GeradorSinal` toca o tom fixo e o sweep (cada parâmetro confirmado pelo eco ou pela
releitura, e o `*202*1*` por último) e aplica as regras 1 e 2 de `failure-modes.md`: desliga no
máximo 30 s depois do `*202*1*`, no Parar, antes de Desconectar ou Esquecer a senha e ao sair do
app; se a conexão cai com o gerador talvez ligado, reconecta até 3 vezes e, sem volta, mostra
"DESLIGUE O MÓDULO AGORA"; no fim de cada leitura, antes do espelho, confere o 202 e desliga.
O `EnvioComResposta` é o envio com espera reusável. O ViewModel é o `GeradorViewModel`.

## Projetos

| Projeto | O que é |
|---|---|
| `src/BgsControle.Nucleo` | `net10.0`, sem Android: quadros do protocolo, autenticação, sessão (máquina de estados e trava de escrita), catálogo de parâmetros (consulta, decodificação, codificação de escrita), leitura completa e retrato, log com a senha mascarada. |
| `src/BgsControle.Telas` | `net10.0`, sem MAUI: os ViewModels (hoje o `ConexaoViewModel`) e as abstrações do celular: `IDespachante` (thread da tela), `Comando`, `IArquivosApp` (arquivos privados), `IPreparoBluetooth` (permissão e Bluetooth ligado) e `TimeProvider` (prazos). |
| `src/BgsControle.App` | MAUI `net10.0-android`: só o XAML e os adaptadores (`DespachanteMaui`, `ArquivosMaui`, `PreparoBluetoothAndroid`), o BLE pela API do Android e a senha no SecureStorage. |
| `tests/BgsControle.Nucleo.Testes` | xUnit com transporte e cofre falsos. Roda no notebook, sem celular. |
| `tests/BgsControle.Simulador` | Biblioteca só de testes: o `ProcessadorSimulado` (um `ITransporte` que responde no formato real do PRO 2.4BT, semeado com a leitura real de 18/10) e o `RelogioFalso`. **Nunca entra no APK**: nenhum projeto de `src/` o referencia (há um teste de arquitetura). O que não foi medido está em `tests/BgsControle.Simulador/SUPOSICOES.md`. |
| `tests/BgsControle.Telas.Testes` | xUnit: o `ConexaoViewModel` e o simulador testados juntos, sem MAUI, e os testes de arquitetura. |

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
dotnet test tests/BgsControle.Telas.Testes
```

Os dois rodam no notebook, sem celular e sem o processador. O segundo usa o processador simulado.

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
- A leitura automática é do núcleo (`LeituraAutomatica`): uma leitura a cada entrada em
  Conectado, nunca de novo enquanto continua conectado nem enquanto outra leitura roda. A tela
  só acompanha e salva o retrato.
- A leitura completa faz uma consulta por vez, ~30 ms entre envios, 5 s de prazo e uma nova
  tentativa para a consulta do Marco 1 que falhou (os extras são perguntados uma vez). Se a
  conexão cair, para e o retrato fica "incompleto".

## Risco conhecido

Não se sabe se o firmware bloqueia depois de várias senhas erradas. O app para em 3 por conexão.
