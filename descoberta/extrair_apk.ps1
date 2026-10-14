# Copia o APK do app Pro 2.4BT+Audio do celular para descoberta\apk\ (sem root).
# Requer: adb no PATH, depuracao USB ligada e o celular autorizado.
# Uso: powershell -ExecutionPolicy Bypass -File .\extrair_apk.ps1
param(
  [string]$Filtro = "taramps",
  [string]$Destino = (Join-Path $PSScriptRoot "apk")
)
$ErrorActionPreference = "Stop"

if (-not (Get-Command adb -ErrorAction SilentlyContinue)) {
  Write-Host "adb nao encontrado. Instale: winget install Google.PlatformTools (e abra um terminal novo)."
  exit 1
}
if (-not (adb devices | Select-String "`tdevice$")) {
  Write-Host "Nenhum celular autorizado. Ligue a depuracao USB, conecte o cabo e aceite o aviso no celular."
  exit 1
}

$pacotes = adb shell pm list packages -3 | ForEach-Object { ($_ -replace '^package:', '').Trim() }
$alvos = $pacotes | Where-Object { $_ -match $Filtro }
if (-not $alvos) {
  Write-Host "Nenhum pacote com '$Filtro'. Apps instalados por voce:"
  $pacotes | Sort-Object | ForEach-Object { Write-Host "  $_" }
  Write-Host "Rode de novo com -Filtro <parte do nome do pacote>."
  exit 1
}

foreach ($p in $alvos) {
  $pasta = Join-Path $Destino $p
  New-Item -ItemType Directory -Force $pasta | Out-Null
  $linha = adb shell dumpsys package $p | Select-String "versionName=" | Select-Object -First 1
  $versao = if ($linha) { $linha.ToString().Trim() } else { "versao desconhecida" }
  Write-Host "`nPacote: $p  ($versao)"
  foreach ($caminho in (adb shell pm path $p)) {
    $remoto = ($caminho -replace '^package:', '').Trim()
    adb pull $remoto $pasta
  }
  Set-Content -Path (Join-Path $pasta "versao.txt") -Value "$p $versao"
  Write-Host "APK(s) em $pasta"
}
