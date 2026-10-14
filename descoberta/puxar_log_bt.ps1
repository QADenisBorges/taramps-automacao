# Puxa o registro de Bluetooth (btsnoop_hci.log) do celular para descoberta\capturas\.
# Requer: "Registro de rastreamento HCI do Bluetooth" = Ativado (e o Bluetooth religado
# depois disso), depuracao USB ligada e adb no PATH.
# Uso: powershell -ExecutionPolicy Bypass -File .\puxar_log_bt.ps1
param([string]$Destino = (Join-Path $PSScriptRoot "capturas"))
$ErrorActionPreference = "Stop"

if (-not (Get-Command adb -ErrorAction SilentlyContinue)) {
  Write-Host "adb nao encontrado. Instale: winget install Google.PlatformTools (e abra um terminal novo)."
  exit 1
}
New-Item -ItemType Directory -Force $Destino | Out-Null
$carimbo = Get-Date -Format "yyyyMMdd-HHmmss"
$zip = Join-Path $Destino "bugreport-$carimbo.zip"
$tmp = Join-Path $Destino "bugreport-$carimbo"

Write-Host "Gerando relatorio de bugs (leva alguns minutos)..."
adb bugreport $zip
Expand-Archive -Path $zip -DestinationPath $tmp -Force

$logs = Get-ChildItem -Path $tmp -Recurse -Filter "btsnoop_hci.log*"
if (-not $logs) {
  Write-Host "btsnoop_hci.log nao encontrado. O registro HCI esta Ativado (sem filtro) e o Bluetooth foi religado?"
  exit 1
}
foreach ($l in $logs) {
  $saida = Join-Path $Destino "btsnoop-$carimbo-$($l.Name)"
  Copy-Item $l.FullName $saida
  Write-Host "Captura: $saida"
}

# O relatorio de bugs tem dados pessoais do celular: so o btsnoop fica.
Remove-Item -Recurse -Force $tmp, $zip
