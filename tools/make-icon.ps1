$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src'
$assets = Join-Path $root 'assets'
New-Item -ItemType Directory -Force -Path $src, $assets | Out-Null

function New-RoundedPath([System.Drawing.RectangleF]$rect, [float]$radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = [float]($radius * 2)
    $arc = [System.Drawing.RectangleF]::new([float]$rect.X, [float]$rect.Y, $d, $d)
    $path.AddArc($arc, 180, 90)
    $arc.X = [float]($rect.Right - $d)
    $path.AddArc($arc, 270, 90)
    $arc.Y = [float]($rect.Bottom - $d)
    $path.AddArc($arc, 0, 90)
    $arc.X = [float]$rect.X
    $path.AddArc($arc, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $inset = [float][Math]::Max(1, [int]($size * 0.035))
    $side = [float]($size - 2 * $inset)
    $rect = [System.Drawing.RectangleF]::new($inset, $inset, $side, $side)
    $radius = [float]($size * 0.22)
    $path = New-RoundedPath $rect $radius

    $top = [System.Drawing.Color]::FromArgb(255, 96, 165, 250)
    $bottom = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect, $top, $bottom, [float]45)
    $g.FillPath($brush, $path)

    if ($size -ge 32) {
        $hw = [float]($rect.Width - 2)
        $hh = [float]($rect.Height * 0.42)
        $highlightRect = [System.Drawing.RectangleF]::new([float]($rect.X + 1), [float]($rect.Y + 1), $hw, $hh)
        $highlightPath = New-RoundedPath $highlightRect ([float]($radius * 0.85))
        $highlight = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(28, 255, 255, 255))
        $g.FillPath($highlight, $highlightPath)
        $highlight.Dispose()
        $highlightPath.Dispose()
    }

    $ringInset = [float]($size * 0.13)
    $ringSide = [float]($size - 2 * $ringInset)
    $ringRect = [System.Drawing.RectangleF]::new($ringInset, $ringInset, $ringSide, $ringSide)
    $ringWidth = [float][Math]::Max(1, $size * 0.012)
    $ringPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(34, 255, 255, 255), $ringWidth)
    $g.DrawEllipse($ringPen, $ringRect)

    $fontSize = [float]($size * 0.40)
    if ($size -le 20) { $fontSize = [float]($size * 0.50) }
    $font = [System.Drawing.Font]::new('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textRect = [System.Drawing.RectangleF]::new(0, [float](-$size * 0.01), [float]$size, [float]$size)
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $g.DrawString('Aa', $font, $white, $textRect, $format)

    $white.Dispose()
    $format.Dispose()
    $font.Dispose()
    $ringPen.Dispose()
    $brush.Dispose()
    $path.Dispose()
    $g.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngData = [System.Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
    $bmp = New-IconBitmap $size
    if ($size -eq 256) {
        $bmp.Save((Join-Path $assets 'icon-256.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData.Add($ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$icoPath = Join-Path $src 'app.ico'
$fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
$writer = [System.IO.BinaryWriter]::new($fs)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $dimension = [byte]$(if ($size -ge 256) { 0 } else { $size })
    $writer.Write($dimension)
    $writer.Write($dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$pngData[$i].Length)
    $writer.Write([UInt32]$offset)
    $offset += $pngData[$i].Length
}
foreach ($data in $pngData) {
    $writer.Write($data)
}
$writer.Flush()
$writer.Dispose()
$fs.Dispose()

Write-Host "Icon written: $icoPath"
