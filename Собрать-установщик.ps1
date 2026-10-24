# Сборка установщика Джарвиса: dist\Jarvis-Setup.exe
#
#   powershell -ExecutionPolicy Bypass -File "Собрать-установщик.ps1"
#
# Сначала собирает один самодостаточный dist\Jarvis.exe, затем упаковывает его
# в установщик NSIS. NSIS (portable) скачивается один раз в
# %LOCALAPPDATA%\JarvisBuild\nsis-3.10 и дальше переиспользуется.
#
#   -NoConfig   не вкладывать publish\config.json в установщик

[CmdletBinding()]
param(
    [switch]$NoConfig   # установщик без готовых настроек по умолчанию
)

$ErrorActionPreference = 'Stop'

$Root        = Split-Path -Parent $MyInvocation.MyCommand.Path
$Dist        = Join-Path $Root 'dist'
$Exe         = Join-Path $Dist 'Jarvis.exe'
$Setup       = Join-Path $Dist 'Jarvis-Setup.exe'
$Icon        = Join-Path $Root 'jarvis.ico'
$Config      = Join-Path $Root 'publish\config.json'
$Nsi         = Join-Path $Root 'installer\Jarvis.nsi'
$NsisVersion = '3.10'
$Cache       = Join-Path $env:LOCALAPPDATA 'JarvisBuild'
$NsisDir     = Join-Path $Cache ('nsis-' + $NsisVersion)
$Makensis    = Join-Path $NsisDir 'makensis.exe'

function Head($text) { Write-Host ''; Write-Host ('=== ' + $text + ' ===') -ForegroundColor Cyan }
function Info($text) { Write-Host ('    ' + $text) }
function Good($text) { Write-Host ('    ' + $text) -ForegroundColor Green }
function Bad($text)  { Write-Host ('    ' + $text) -ForegroundColor Red }

Head 'Сборка программы'
& powershell.exe -ExecutionPolicy Bypass -File (Join-Path $Root 'Собрать-Джарвиса.ps1') -SingleFile
if ($LASTEXITCODE -ne 0) { Bad 'сборка программы не удалась'; exit 1 }
if (-not (Test-Path $Exe)) { Bad ('нет файла ' + $Exe); exit 1 }

Head 'NSIS'
if (-not (Test-Path $Makensis)) {
    New-Item -ItemType Directory -Force -Path $Cache | Out-Null
    $zip = Join-Path $Cache ('nsis-' + $NsisVersion + '.zip')
    $url = 'https://sourceforge.net/projects/nsis/files/NSIS%203/' + $NsisVersion + '/nsis-' + $NsisVersion + '.zip/download'
    Info 'скачиваю portable NSIS…'
    & curl.exe -L --fail --max-time 180 -o $zip $url
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $zip)) { Bad 'не удалось скачать NSIS'; exit 1 }

    $tmp = Join-Path $Cache 'nsis-tmp'
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $tmp -Force
    $inner = Join-Path $tmp ('nsis-' + $NsisVersion)
    if (-not (Test-Path $inner)) { Bad 'в архиве NSIS нет ожидаемой папки'; exit 1 }

    if (Test-Path $NsisDir) { Remove-Item $NsisDir -Recurse -Force }
    Move-Item $inner $NsisDir
    Remove-Item $tmp -Recurse -Force
    Remove-Item $zip -Force
}
Good ('NSIS: ' + $Makensis)

Head 'Компиляция установщика'
if (-not (Test-Path $Nsi)) { Bad ('нет сценария ' + $Nsi); exit 1 }
if (Test-Path $Setup) { Remove-Item $Setup -Force }

$nsiArgs = @('/V2', "/DAPP_EXE=$Exe", "/DAPP_ICON=$Icon", "/DOUT_FILE=$Setup")
if ($NoConfig) {
    Info 'настройки по умолчанию не вкладываются (-NoConfig)'
}
elseif (Test-Path $Config) {
    $nsiArgs += "/DAPP_CONFIG=$Config"
    Info ('настройки по умолчанию: ' + $Config)
}
& $Makensis @nsiArgs $Nsi
if ($LASTEXITCODE -ne 0) { Bad 'makensis завершился с ошибкой'; exit 1 }
if (-not (Test-Path $Setup)) { Bad 'установщик не создан'; exit 1 }

$mb = [Math]::Round((Get-Item $Setup).Length / 1MB, 1)
Head 'Готово'
Info ('Установщик: ' + $Setup + ' (' + $mb + ' МБ)')
Info 'Установка в профиль пользователя, права администратора не нужны.'
