# Descoberta do protocolo — Taramps PRO 2.4BT + AUDIO

Etapa 1 do protótipo 1 (ver `_bmad-output/forge/automacao-app-taramps-dsp/forged-idea.md`).
Objetivo: entender como o app **Pro 2.4BT+Audio** conversa com o processador, até o notebook conseguir mudar o volume master.

Três frentes, todas no notebook Windows:

| Script | O que faz | Escreve no processador? |
|---|---|---|
| `extrair_apk.ps1` | Copia o APK do app do celular para `apk\` | Não |
| `mapear_gatt.py` | Conecta no processador e lista serviços e características BLE | Não (só lê; `--ouvir` assina notificações) |
| `puxar_log_bt.ps1` | Puxa o registro de Bluetooth do celular para `capturas\` | Não |

Regra da descoberta: nada é escrito nos ajustes de áudio até sabermos o que cada comando faz. Quando chegar a hora, só reenviamos comandos vistos saindo do app, com volume baixo e com a sua aprovação.

## 1. Preparar o notebook (uma vez)

No PowerShell:

```powershell
winget install Python.Python.3.12
winget install Google.PlatformTools
```

Feche e abra o PowerShell, entre nesta pasta e instale a biblioteca de Bluetooth:

```powershell
cd caminho\para\taramps-automacao\descoberta
py -m pip install -r requirements.txt
```

## 2. Preparar o celular (uma vez)

1. **Configurações → Sobre o telefone → Informações do software** → toque 7× em **Número de compilação**.
2. **Configurações → Opções do desenvolvedor**:
   - **Depuração USB**: ligada.
   - **Registro de rastreamento HCI do Bluetooth**: **Ativado** (não "com filtro"). Depois, desligue e ligue o Bluetooth.
3. Conecte o cabo USB no notebook e aceite **"Permitir depuração USB"** no celular.
4. Teste: `adb devices` deve mostrar o celular como `device`.

## 3. Extrair o app

```powershell
powershell -ExecutionPolicy Bypass -File .\extrair_apk.ps1
```

Mostra o nome do pacote e salva o(s) `.apk` em `apk\<pacote>\`. Se não achar nada com "taramps", ele lista os apps instalados: rode de novo com `-Filtro <parte do nome>`.
**Envie o(s) `.apk` pelo chat** (a pasta `apk\` não vai para o GitHub).

## 4. Mapear o processador

1. Processador ligado. **Feche o app no celular** (ou desligue o Bluetooth do celular).
2. Rode:

```powershell
py mapear_gatt.py
```

Escolha o processador na lista (o de sinal mais forte, perto do notebook, costuma ser ele). O relatório fica em `relatorios\gatt-AAAAMMDD-HHMMSS.json`.
Depois rode também com `--ouvir 15` para ver se o aparelho manda dados sozinho (ex.: voltímetro).

Envie os JSON pelo chat ou faça commit da pasta `relatorios\`.

## 5. Captura do app (se o código não bastar)

1. Com o registro HCI ativado: abra o app, conecte e faça poucas ações devagar, **anotando ordem e valores** (ex.: master −20 → −21 dB; 5 s; mute da saída 1 liga/desliga).
2. Rode:

```powershell
powershell -ExecutionPolicy Bypass -File .\puxar_log_bt.ps1
```

Fica só o `btsnoop` em `capturas\`; o relatório de bugs (com dados pessoais) é apagado. Envie o `btsnoop` pelo chat, junto com as anotações.
