[CmdletBinding()]
param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\NinoBind'),
    [string]$AppId = 'NinoBind',
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
$exe = Join-Path $source 'NinoBind.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw 'NinoBind.exe não está na mesma pasta do instalador.'
}
if ($AppId -notmatch '^[A-Za-z0-9._-]{1,64}$') {
    throw 'AppId aceita só letras, números, ponto, hífen e sublinhado.'
}

$InstallRoot = [System.IO.Path]::GetFullPath($InstallRoot)
$version = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
$displayName = if ($AppId -eq 'NinoBind') { 'NinoBind' } else { "NinoBind ($AppId)" }
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) "$displayName.lnk"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
$manifestPath = Join-Path $InstallRoot 'install-manifest.json'
$payload = @(Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Name -ne 'install.ps1' })

$running = @(Get-Process -Name NinoBind -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and $_.Path.StartsWith($InstallRoot + '\', [StringComparison]::OrdinalIgnoreCase)
})
if ($running.Count -gt 0) {
    throw "Feche o NinoBind que está rodando de $InstallRoot antes de instalar."
}

$previous = $null
if (Test-Path -LiteralPath $manifestPath) {
    $previous = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
}

Write-Host "NinoBind $version - instalação por usuário, sem administrador"
Write-Host ''
Write-Host 'Mudanças planejadas:'
Write-Host "  Pasta:      $InstallRoot"
foreach ($file in $payload) { Write-Host "    + $($file.Name)" }
Write-Host "  Atalho:     $shortcut"
Write-Host "  Registro:   $uninstallKey (para aparecer em Aplicativos instalados)"
if ($previous) { Write-Host "  Atualiza a versão $($previous.version) já instalada nessa pasta." }
Write-Host '  Não altera: iniciar com o Windows, configurações e perfis em %LOCALAPPDATA%\NinoBind.'
Write-Host ''
if (-not $Yes) {
    $answer = Read-Host 'Continuar? (s/N)'
    if ($answer -notmatch '^(s|sim|y|yes)$') {
        Write-Host 'Nada foi alterado.'
        exit 1
    }
}

New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
foreach ($file in $payload) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $InstallRoot $file.Name) -Force
}
$names = @($payload | ForEach-Object { $_.Name })
if ($previous) {
    foreach ($old in @($previous.files)) {
        if ($names -notcontains $old -and $old -notmatch '[\\/:]') {
            Remove-Item -LiteralPath (Join-Path $InstallRoot $old) -Force -ErrorAction SilentlyContinue
        }
    }
}

$installedExe = Join-Path $InstallRoot 'NinoBind.exe'
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = $installedExe
$link.WorkingDirectory = $InstallRoot
$link.IconLocation = "$installedExe,0"
$link.Description = 'Universal Keyboard & Mouse Configurator'
$link.Save()
[void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell)

$uninstallScript = Join-Path $InstallRoot 'uninstall.ps1'
$sizeKb = [int][Math]::Ceiling((($payload | Measure-Object -Property Length -Sum).Sum) / 1KB)
New-Item -Path $uninstallKey -Force | Out-Null
$values = @{
    DisplayName          = $displayName
    DisplayVersion       = $version
    Publisher            = 'NinoBind'
    InstallLocation      = $InstallRoot
    DisplayIcon          = "$installedExe,0"
    UninstallString      = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`""
    QuietUninstallString = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`" -Yes"
}
foreach ($name in $values.Keys) {
    New-ItemProperty -Path $uninstallKey -Name $name -Value $values[$name] -PropertyType String -Force | Out-Null
}
foreach ($name in 'NoModify', 'NoRepair') {
    New-ItemProperty -Path $uninstallKey -Name $name -Value 1 -PropertyType DWord -Force | Out-Null
}
New-ItemProperty -Path $uninstallKey -Name EstimatedSize -Value $sizeKb -PropertyType DWord -Force | Out-Null

$manifest = [ordered]@{
    appId        = $AppId
    version      = $version
    installRoot  = $InstallRoot
    files        = $names
    shortcut     = $shortcut
    uninstallKey = $uninstallKey
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "Instalado em $InstallRoot."
