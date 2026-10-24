param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'src\NinoBind.App\NinoBind.App.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$artifacts = Join-Path $root 'artifacts'
$name = "NinoBind-$version-win-x64"
$package = Join-Path $artifacts $name
$zip = Join-Path $artifacts "$name.zip"

if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Recurse -Force }
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }

dotnet publish $project -c $Configuration -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o $package --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou ($LASTEXITCODE)." }

foreach ($file in 'install.ps1', 'uninstall.ps1', 'LEIA-ME.txt') {
    Copy-Item -LiteralPath (Join-Path $root "installer\$file") -Destination $package
}

Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$zip.sha256" -Value "$hash  $name.zip" -Encoding ASCII
Get-ChildItem -LiteralPath $package | Select-Object Name, Length | Format-Table -AutoSize | Out-String | Write-Host
Write-Host "$zip"
Write-Host "sha256 $hash"
