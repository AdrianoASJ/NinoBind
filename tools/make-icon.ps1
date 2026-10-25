param(
    [string]$Source = (Join-Path $PSScriptRoot '..\src\NinoBind.App\Assets\ninobind-icon.png'),
    [string]$Output = (Join-Path $PSScriptRoot '..\src\NinoBind.App\Assets\ninobind.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $Source)) {
    throw "PNG da logo não encontrado: $Source"
}

function Get-Zoom([int]$size) {
    if ($size -le 16) { return 0.36 }
    if ($size -le 20) { return 0.38 }
    if ($size -le 24) { return 0.40 }
    if ($size -le 32) { return 0.46 }
    if ($size -le 40) { return 0.58 }
    if ($size -le 48) { return 0.70 }
    return 1.0
}

function New-IconFrame([System.Drawing.Image]$source, [int]$size) {
    $zoom = Get-Zoom $size
    $crop = [Math]::Max(8, [int]($source.Width * $zoom))
    $x = [int](($source.Width - $crop) / 2)
    $y = [int](($source.Height - $crop) / 2)
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $dest = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $src = New-Object System.Drawing.Rectangle $x, $y, $crop, $crop
    $graphics.DrawImage($source, $dest, $src, [System.Drawing.GraphicsUnit]::Pixel)
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    return , $stream.ToArray()
}

$sourceImage = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $Source))
$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$frames = foreach ($size in $sizes) { , (New-IconFrame $sourceImage $size) }
$sourceImage.Dispose()

$directory = Split-Path -Parent $Output
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$file = [System.IO.File]::Create($Output)
$writer = New-Object System.IO.BinaryWriter $file
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $edge = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$edge)
    $writer.Write([byte]$edge)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$frames[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $writer.Write($frame) }
$writer.Dispose()
Write-Output "ico $Output $((Get-Item $Output).Length) bytes, $($sizes.Count) frames"
