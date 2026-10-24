# Сборка Джарвиса.
#
#   powershell -ExecutionPolicy Bypass -File "Собрать-Джарвиса.ps1"
#       обычная папка publish (exe + dll + model)
#
#   powershell -ExecutionPolicy Bypass -File "Собрать-Джарвиса.ps1" -SingleFile
#       один самодостаточный dist\Jarvis.exe (модель и иконка внутри)
#
#   powershell -ExecutionPolicy Bypass -File "Собрать-Джарвиса.ps1" -Install -Run
#       установить в %LOCALAPPDATA%\Programs\Jarvis, ярлыки, запуск
#
# Для сборки нужен только .NET SDK 10.

[CmdletBinding()]
param(
    [switch]$Install,      # скопировать в %LOCALAPPDATA%\Programs\Jarvis и создать ярлыки
    [switch]$Run,          # запустить после сборки
    [switch]$SingleFile    # собрать всё в один exe (dist\Jarvis.exe)
)

$ErrorActionPreference = 'Stop'

$Root     = Split-Path -Parent $MyInvocation.MyCommand.Path
$Proj     = Join-Path $Root 'Jarvis.csproj'
$Pub      = Join-Path $Root 'publish'
$Dist     = Join-Path $Root 'dist'
$ModelDir = Join-Path $Root 'model'
$Icon     = Join-Path $Root 'jarvis.ico'

function Head($text) { Write-Host ''; Write-Host ('=== ' + $text + ' ===') -ForegroundColor Cyan }
function Info($text) { Write-Host ('    ' + $text) }
function Good($text) { Write-Host ('    ' + $text) -ForegroundColor Green }
function Bad($text)  { Write-Host ('    ' + $text) -ForegroundColor Red }

function Backup-Config([string]$dir) {
    $p = Join-Path $dir 'config.json'
    if (-not (Test-Path $p)) { return $null }
    $tmp = Join-Path $env:TEMP ('jarvis-config-' + [Guid]::NewGuid().ToString('N') + '.json')
    Copy-Item $p $tmp -Force
    return $tmp
}

function Restore-Config([string]$dir, [string]$backup) {
    if (-not $backup) { return }
    if (-not (Test-Path $backup)) { return }
    Copy-Item $backup (Join-Path $dir 'config.json') -Force
    Remove-Item $backup -Force
}

Head 'Проверки'
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { Bad 'не найден dotnet. Поставьте .NET SDK 10.'; exit 1 }
Good ('dotnet: ' + $dotnet.Source)
if (-not (Test-Path $Proj)) { Bad ('не найден проект: ' + $Proj); exit 1 }
if (-not (Test-Path $ModelDir)) { Bad 'нет папки model с моделью распознавания'; exit 1 }

$model = Get-ChildItem $ModelDir -Filter 'ggml-*.bin' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $model) { Bad 'в папке model нет файла ggml-*.bin'; exit 1 }
Info ('модель: ' + $model.Name + ' (' + [Math]::Round($model.Length / 1MB, 1) + ' МБ)')

if ($SingleFile -and -not (Test-Path (Join-Path $ModelDir 'ggml-base-q5_1.bin'))) {
    Bad 'для сборки одним файлом нужен model\ggml-base-q5_1.bin'
    exit 1
}

# ---------------------------------------------------------------- публикация
if ($SingleFile) {
    Head 'Сборка одним файлом'
    $cfg = Backup-Config $Dist
    if (Test-Path $Dist) { Remove-Item $Dist -Recurse -Force }

    & dotnet publish $Proj -c Release -r win-x64 --self-contained true -o $Dist -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:EmbedModel=true -p:DebugType=None -p:DebugSymbols=false -v q --nologo
    if ($LASTEXITCODE -ne 0) { Bad 'сборка не удалась'; exit 1 }

    Get-ChildItem $Dist -Force | Where-Object { $_.Name -ne 'Jarvis.exe' -and $_.Name -ne 'config.json' } | Remove-Item -Recurse -Force
    Restore-Config $Dist $cfg

    $exe = Join-Path $Dist 'Jarvis.exe'
    if (-not (Test-Path $exe)) { Bad 'в dist нет Jarvis.exe'; exit 1 }
    $mb = [Math]::Round((Get-Item $exe).Length / 1MB, 1)
    Good ('готово: ' + $exe + ' (' + $mb + ' МБ)')
    $Pub = $Dist
}
else {
    Head 'Публикация'
    $cfg = Backup-Config $Pub
    if (Test-Path $Pub) { Remove-Item $Pub -Recurse -Force }

    & dotnet publish $Proj -c Release -r win-x64 --self-contained true -o $Pub -v q --nologo
    if ($LASTEXITCODE -ne 0) { Bad 'сборка не удалась'; exit 1 }

    # лишние платформы и отладочные символы
    Remove-Item (Join-Path $Pub 'runtimes\win-x86') -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $Pub 'runtimes\win-arm64') -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $Pub 'Jarvis.pdb') -Force -ErrorAction SilentlyContinue
    Restore-Config $Pub $cfg

    # модель и значок должны лежать рядом с exe
    $pubModel = Join-Path $Pub 'model'
    if (-not (Test-Path $pubModel)) { New-Item -ItemType Directory -Force -Path $pubModel | Out-Null }
    if (-not (Get-ChildItem $pubModel -Filter 'ggml-*.bin' -ErrorAction SilentlyContinue)) {
        Copy-Item (Join-Path $ModelDir '*.bin') $pubModel -Force
    }
    if ((Test-Path $Icon) -and -not (Test-Path (Join-Path $Pub 'jarvis.ico'))) {
        Copy-Item $Icon $Pub -Force
    }

    $need = @('Jarvis.exe', 'jarvis.ico', 'model')
    foreach ($n in $need) {
        if (-not (Test-Path (Join-Path $Pub $n))) { Bad ('в сборке нет: ' + $n); exit 1 }
    }
    $size = [Math]::Round((Get-ChildItem $Pub -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
    Good ('готово: ' + $Pub + ' (' + $size + ' МБ)')
}

# ---------------------------------------------------------------- установка
if ($Install) {
    Head 'Установка'
    $dest = Join-Path $env:LOCALAPPDATA 'Programs\Jarvis'
    $destCfg = Backup-Config $dest
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item (Join-Path $Pub '*') $dest -Recurse -Force
    Restore-Config $dest $destCfg
    Good ('скопировано в ' + $dest)

    $exe = Join-Path $dest 'Jarvis.exe'
    $shell = New-Object -ComObject WScript.Shell
    $targets = @(
        (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Джарвис.lnk'),
        (Join-Path ([Environment]::GetFolderPath('Programs')) 'Джарвис.lnk')
    )
    foreach ($lnk in $targets) {
        $s = $shell.CreateShortcut($lnk)
        $s.TargetPath = $exe
        $s.WorkingDirectory = $dest
        $s.IconLocation = $exe
        $s.Description = 'Джарвис — голосовой помощник'
        $s.Save()
        Good ('ярлык: ' + $lnk)
    }

    $Pub = $dest
}

if ($Run) {
    Head 'Запуск'
    Start-Process -FilePath (Join-Path $Pub 'Jarvis.exe')
    Good 'запущено'
}

Head 'Готово'
Info ('Программа: ' + (Join-Path $Pub 'Jarvis.exe'))
if ($SingleFile) {
    Info 'Это один файл: рядом с ним ничего не нужно.'
} else {
    Info 'Первый запуск показывает окно. Дальше — иконка в трее.'
}
