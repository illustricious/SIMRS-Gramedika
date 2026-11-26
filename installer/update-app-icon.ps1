# Run with powershell.exe -NoProfile -STA -File installer/update-app-icon.ps1.
# The SVG paths drive both the vector Avalonia mark and the multi-size Windows ICO.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

$ui = Join-Path $PSScriptRoot '../src/SimrsGramedika.UI'
$assets = Join-Path $ui 'Assets'
[xml]$svg = Get-Content -LiteralPath (Join-Path $assets 'app.svg') -Raw
$viewBox = $svg.DocumentElement.GetAttribute('viewBox') -split '\s+'
$width = [double]::Parse($viewBox[2], [Globalization.CultureInfo]::InvariantCulture)
$height = [double]::Parse($viewBox[3], [Globalization.CultureInfo]::InvariantCulture)
$paths = $svg.DocumentElement.SelectNodes("*[local-name()='path']")

$drawing = [Windows.Media.DrawingGroup]::new()
$xamlPaths = foreach ($path in $paths) {
    $data = $path.GetAttribute('d')
    $color = $path.GetAttribute('fill')
    $geometry = [Windows.Media.Geometry]::Parse($data)
    $brush = [Windows.Media.BrushConverter]::new().ConvertFromInvariantString($color)
    $drawing.Children.Add([Windows.Media.GeometryDrawing]::new($brush, $null, $geometry))
    '                <GeometryDrawing Brush="{0}" Geometry="{1}" />' -f $color, [Security.SecurityElement]::Escape($data)
}

$xaml = @"
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Generated from Assets/app.svg by installer/update-app-icon.ps1. -->
    <DrawingImage x:Key="GramedikaMark">
        <DrawingImage.Drawing>
            <DrawingGroup>
$($xamlPaths -join [Environment]::NewLine)
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>
</ResourceDictionary>
"@
[IO.File]::WriteAllText((Join-Path $ui 'Styles/AppMark.axaml'), $xaml + [Environment]::NewLine)

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = foreach ($size in $sizes) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([Windows.Media.ScaleTransform]::new($size / $width, $size / $height))
    $context.DrawDrawing($drawing)
    $context.Pop()
    $context.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    try {
        $encoder.Save($stream)
        [pscustomobject]@{ Size = $size; Png = $stream.ToArray() }
    }
    finally { $stream.Dispose() }
}

$target = [IO.File]::Create((Join-Path $assets 'app.ico'))
$writer = [IO.BinaryWriter]::new($target)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = [byte]($frame.Size % 256)
        $writer.Write($dimension)
        $writer.Write($dimension)
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Png.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Png.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Png) }
}
finally { $writer.Dispose() }
