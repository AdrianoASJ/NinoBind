param(
    [string]$Output = (Join-Path $PSScriptRoot '..\src\NinoBind.App\Assets\ninobind.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconFrame([int]$size) {
    $scale = $size / 32.0
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $accent = [System.Drawing.Color]::FromArgb(0xF0, 0xCC, 0x38)
    $brush = New-Object System.Drawing.SolidBrush $accent
    $pen = New-Object System.Drawing.Pen $accent, ([Math]::Max(1.5, 3 * $scale))
    $filled = New-RoundedPath (2 * $scale) (2 * $scale) (18 * $scale) (18 * $scale) (4 * $scale)
    $outline = New-RoundedPath (12 * $scale) (12 * $scale) (17 * $scale) (17 * $scale) (4 * $scale)
    $graphics.FillPath($brush, $filled)
    $graphics.DrawPath($pen, $outline)
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $filled.Dispose(); $outline.Dispose(); $pen.Dispose(); $brush.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
    return , $stream.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 256
$frames = foreach ($size in $sizes) { , (New-IconFrame $size) }

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
