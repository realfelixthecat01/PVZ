<#
    Устанавливает PVZS на домашний Windows-сервер (запускать от Администратора):

      1. публикует PVZS в <InstallDir>\server (один самодостаточный PVZS.exe);
      2. регистрирует службу Windows PVZS с автозапуском;
      3. устанавливает Caddy <InstallDir>\caddy — бесплатный HTTPS (Let's Encrypt)
         для pvz.k11.by, проксирует на http://127.0.0.1:1729;
      4. открывает в брандмауэре вход на порты 80 и 443.

    Перед запуском: создайте A-запись `pvz` → IP этого компьютера в панели
    управления k11.by и пробросьте порты 80 и 443 роутера. URL для учителя/учеников:
    https://pvz.k11.by  (захардкожен в клиентах как адрес по умолчанию).
#>
#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\PVZS\PVZS.csproj'),
    [string]$InstallDir = 'C:\PVZS',
    [string]$Domain = 'pvz.k11.by'
)
$ErrorActionPreference = 'Stop'

$serverDir   = Join-Path $InstallDir 'server'
$caddyDir    = Join-Path $InstallDir 'caddy'
$serverExe   = Join-Path $serverDir 'PVZS.exe'
$caddyExe    = Join-Path $caddyDir 'caddy.exe'
$caddyConf   = Join-Path $caddyDir 'Caddyfile'

# ---- 1. Публикация сервера -------------------------------------------------
if (-not (Test-Path -LiteralPath $Source)) {
    throw "Не найден проект PVZS: $Source"
}
New-Item -ItemType Directory -Force -Path $serverDir | Out-Null
Write-Host 'Публикую PVZS...'
& dotnet publish $Source -c Release -r win-x64 --self-contained true -o $serverDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish завершился с ошибкой.' }
if (-not (Test-Path -LiteralPath $serverExe)) {
    throw "После публикации нет $serverExe"
}

# ---- 1b. Клиенты PVZP/PVZT для скачивания с сайта -------------------------
$downloadsDir = Join-Path $serverDir 'downloads'
New-Item -ItemType Directory -Force -Path $downloadsDir | Out-Null
foreach ($client in @(
    @{ Project = '..\PVZP\PVZP.csproj'; Name = 'PVZP.exe' },
    @{ Project = '..\PVZT\PVZT.csproj'; Name = 'PVZT.exe' }
)) {
    $project = Join-Path $PSScriptRoot $client.Project
    if (-not (Test-Path -LiteralPath $project)) {
        Write-Warning "Пропускаю $($client.Name): не найден $project"
        continue
    }
    Write-Host "Публикую $($client.Name)..."
    & dotnet publish $project -c Release -r win-x64 --self-contained true -o $downloadsDir
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Не удалось опубликовать $($client.Name). Положите файл вручную в $downloadsDir"
        continue
    }
    $produced = Join-Path $downloadsDir $client.Name
    if (Test-Path -LiteralPath $produced) { Write-Host "  → $produced" }
    else { Write-Warning "После публикации нет $produced" }
}
Write-Host "Клиенты для сайта лежат в: $downloadsDir"

# ---- 2. Служба Windows PVZS ------------------------------------------------
Write-Host 'Настраиваю службу Windows PVZS...'
& sc.exe stop PVZS 2>$null | Out-Null
& sc.exe delete PVZS 2>$null | Out-Null
& sc.exe create PVZS type= own start= auto binPath= "$serverExe"
if ($LASTEXITCODE -ne 0) { throw 'Не удалось создать службу PVZS.' }

# ---- 3. Caddy: HTTPS для pvz.k11.by -----------------------------------
if (-not (Test-Path -LiteralPath $caddyExe)) {
    Write-Host 'Скачиваю Caddy...'
    $zip = Join-Path $env:TEMP 'pvz-caddy.zip'
    Invoke-WebRequest -Uri 'https://caddyserver.com/api/download?os=windows&arch=amd64' -OutFile $zip
    New-Item -ItemType Directory -Force -Path $caddyDir | Out-Null
    Expand-Archive -LiteralPath $zip -DestinationPath $caddyDir -Force
    Remove-Item -LiteralPath $zip -Force
}
if (-not (Test-Path -LiteralPath $caddyExe)) {
    throw "В архиве Caddy не оказалось caddy.exe в $caddyDir"
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Caddyfile') -Destination $caddyConf -Force
Write-Host 'Настраиваю службу Caddy...'
& sc.exe stop PVZ-Caddy 2>$null | Out-Null
& sc.exe delete PVZ-Caddy 2>$null | Out-Null
& sc.exe create PVZ-Caddy type= own start= auto binPath= "`"$caddyExe`" run --config $caddyConf --watch"
if ($LASTEXITCODE -ne 0) { throw 'Не удалось создать службу Caddy.' }

# ---- 4. Брандмауэр: 80 и 443 ---------------------------------------------
New-NetFirewallRule -DisplayName 'PVZS HTTP (80)'  -Direction Inbound -Protocol TCP -LocalPort 80  -Action Allow -ErrorAction SilentlyContinue | Out-Null
New-NetFirewallRule -DisplayName 'PVZS HTTPS (443)' -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow -ErrorAction SilentlyContinue | Out-Null

# ---- Запуск --------------------------------------------------------------
Write-Host 'Запускаю службы...'
& sc.exe start PVZS | Out-Null
& sc.exe start PVZ-Caddy | Out-Null

Write-Host ''
Write-Host "Готово. Сайт: https://$Domain"
Write-Host 'Если адрес не открывается — проверьте проброс портов 80 и 443 роутера к этому компьютеру.'
Write-Host 'Данные PVZS лежат в: ' (Join-Path $serverDir 'sessions')