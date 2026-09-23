$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$assetsDirectory = Join-Path $projectRoot "Assets"
$iconPath = Join-Path $assetsDirectory "ZenLoad.ico"
New-Item -ItemType Directory -Path $assetsDirectory -Force | Out-Null

function Add-RoundedRectangle([System.Drawing.Drawing2D.GraphicsPath] $path, [float] $x, [float] $y, [float] $width, [float] $height, [float] $radius) {
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
}

function New-PngBytes([int] $size) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $scale = $size / 256.0

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $backgroundPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
        Add-RoundedRectangle $backgroundPath (18 * $scale) (18 * $scale) (220 * $scale) (220 * $scale) (48 * $scale)
        $backgroundBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 17, 56, 83))
        $graphics.FillPath($backgroundBrush, $backgroundPath)

        $folderPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $folderPath.AddPolygon([System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(42 * $scale, 76 * $scale),
            [System.Drawing.PointF]::new(91 * $scale, 76 * $scale),
            [System.Drawing.PointF]::new(108 * $scale, 94 * $scale),
            [System.Drawing.PointF]::new(211 * $scale, 94 * $scale),
            [System.Drawing.PointF]::new(211 * $scale, 181 * $scale),
            [System.Drawing.PointF]::new(42 * $scale, 181 * $scale)
        ))
        $folderBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 77, 218, 190))
        $graphics.FillPath($folderBrush, $folderPath)

        $checkPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 17, 56, 83), 18 * $scale)
        $checkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $checkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $checkPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $graphics.DrawLines($checkPen, [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(70 * $scale, 132 * $scale),
            [System.Drawing.PointF]::new(101 * $scale, 160 * $scale),
            [System.Drawing.PointF]::new(171 * $scale, 108 * $scale)
        ))

        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }
    finally {
        $checkPen.Dispose()
        $folderBrush.Dispose()
        $backgroundBrush.Dispose()
        $folderPath.Dispose()
        $backgroundPath.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @($sizes | ForEach-Object { ,(New-PngBytes $_) })

$iconStream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($iconStream)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)

for ($index = 0; $index -lt $sizes.Count; $index++) {
    $size = $sizes[$index]
    $png = $pngs[$index]
    $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
    $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$png.Length)
    $writer.Write([uint32]$offset)
    $offset += $png.Length
}

foreach ($png in $pngs) {
    $writer.Write($png)
}

[System.IO.File]::WriteAllBytes($iconPath, $iconStream.ToArray())
$writer.Dispose()
$iconStream.Dispose()
Write-Host "Icono generado en: $iconPath"
