[CmdletBinding()]
param(
    [switch]$Yes,
    [switch]$RemoveUserData
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($PSScriptRoot)
$manifestPath = Join-Path $root 'install-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Não há install-manifest.json em $root. Nada foi removido."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not [string]::Equals([System.IO.Path]::GetFullPath($manifest.installRoot), $root, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'O manifesto aponta para outra pasta. Nada foi removido.'
}
$expectedKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$($manifest.appId)"
if ($manifest.appId -notmatch '^[A-Za-z0-9._-]{1,64}$' -or $manifest.uninstallKey -ne $expectedKey) {
    throw 'O manifesto tem uma chave de registro inesperada. Nada foi removido.'
}
$programs = [Environment]::GetFolderPath('Programs')
$shortcutOwned = $manifest.shortcut -and
    [string]::Equals([System.IO.Path]::GetDirectoryName($manifest.shortcut), $programs, [StringComparison]::OrdinalIgnoreCase) -and
    $manifest.shortcut.EndsWith('.lnk', [StringComparison]::OrdinalIgnoreCase)

$files = @($manifest.files | Where-Object { $_ -and $_ -notmatch '[\\/:]' -and $_ -ne '..' })
$installedExe = Join-Path $root 'NinoBind.exe'
$running = @(Get-Process -Name NinoBind -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and $_.Path.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)
})
if ($running.Count -gt 0) {
    throw 'Feche o NinoBind (menu da bandeja, Sair) antes de desinstalar.'
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$startupCommand = (Get-ItemProperty -Path $runKey -Name NinoBind -ErrorAction SilentlyContinue).NinoBind
$startupOwned = $startupCommand -and $startupCommand.IndexOf($installedExe, [StringComparison]::OrdinalIgnoreCase) -ge 0
$userData = Join-Path $env:LOCALAPPDATA 'NinoBind'
$removeData = $RemoveUserData -and $manifest.appId -eq 'NinoBind' -and (Test-Path -LiteralPath $userData)

Write-Host "Desinstalar NinoBind $($manifest.version)"
Write-Host ''
Write-Host 'Mudanças planejadas:'
foreach ($file in $files) { Write-Host "  - $(Join-Path $root $file)" }
Write-Host "  - $manifestPath"
if ($shortcutOwned) { Write-Host "  - $($manifest.shortcut)" }
Write-Host "  - $($manifest.uninstallKey)"
if ($startupOwned) { Write-Host "  - Iniciar com o Windows ($runKey\NinoBind)" }
if ($removeData) {
    Write-Host "  - $userData (configurações, perfis, macros e log)"
} else {
    Write-Host "  Mantém: $userData (use -RemoveUserData para apagar)"
}
Write-Host ''
if (-not $Yes) {
    $answer = Read-Host 'Continuar? (s/N)'
    if ($answer -notmatch '^(s|sim|y|yes)$') {
        Write-Host 'Nada foi alterado.'
        exit 1
    }
}

if ($startupOwned) { Remove-ItemProperty -Path $runKey -Name NinoBind }
if ($shortcutOwned -and (Test-Path -LiteralPath $manifest.shortcut)) { Remove-Item -LiteralPath $manifest.shortcut -Force }
if (Test-Path -LiteralPath $manifest.uninstallKey) { Remove-Item -LiteralPath $manifest.uninstallKey -Recurse -Force }
foreach ($file in $files) {
    $path = Join-Path $root $file
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}
Remove-Item -LiteralPath $manifestPath -Force
if (-not (Get-ChildItem -LiteralPath $root -Force)) { Remove-Item -LiteralPath $root -Force }
if ($removeData) { Remove-Item -LiteralPath $userData -Recurse -Force }

Write-Host 'NinoBind desinstalado.'
