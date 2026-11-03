# Regenerates src\App\Assets\icon.ico, icon.png and icon.icns (a git branch graph on a teal tile).
# Windows only (uses System.Drawing). The generated files are committed, so this is only needed to change the design.
Add-Type -AssemblyName System.Drawing

$assets = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\App\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

function New-MasterIcon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 1024.0

    # Rounded tile with a vertical teal gradient.
    $r = 224 * $s
    $inset = 32 * $s
    $rect = New-Object System.Drawing.RectangleF $inset, $inset, ($size - 2 * $inset), ($size - 2 * $inset)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $r, $r, 180, 90)
    $path.AddArc($rect.Right - $r, $rect.Y, $r, $r, 270, 90)
    $path.AddArc($rect.Right - $r, $rect.Bottom - $r, $r, $r, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $r, $r, $r, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
        ([System.Drawing.Color]::FromArgb(255, 20, 125, 140)), ([System.Drawing.Color]::FromArgb(255, 6, 60, 78)), 90
    $g.FillPath($brush, $path)

    # Branch graph: main line on the left, a feature branch merging in from the right.
    $white = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
    $pen = New-Object System.Drawing.Pen $white, (64 * $s)
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $top = [System.Drawing.PointF]::new(380 * $s, 270 * $s)
    $bottom = [System.Drawing.PointF]::new(380 * $s, 754 * $s)
    $side = [System.Drawing.PointF]::new(660 * $s, 440 * $s)
    $mid = [System.Drawing.PointF]::new(380 * $s, 600 * $s)
    $g.DrawLine($pen, $top, $bottom)
    $g.DrawBezier($pen, $top, [System.Drawing.PointF]::new(380 * $s, 380 * $s), [System.Drawing.PointF]::new(660 * $s, 330 * $s), $side)
    $g.DrawBezier($pen, $side, [System.Drawing.PointF]::new(660 * $s, 540 * $s), [System.Drawing.PointF]::new(380 * $s, 500 * $s), $mid)

    $nodeBrush = New-Object System.Drawing.SolidBrush $white
    $holeBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 12, 85, 104))
    foreach ($p in @($top, $bottom, $side)) {
        $rad = 92 * $s; $hole = 44 * $s
        $g.FillEllipse($nodeBrush, $p.X - $rad, $p.Y - $rad, 2 * $rad, 2 * $rad)
        $g.FillEllipse($holeBrush, $p.X - $hole, $p.Y - $hole, 2 * $hole, 2 * $hole)
    }
    $g.Dispose()
    return $bmp
}

function Get-PngBytes($master, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    $g.SmoothingMode = 'AntiAlias'
    $g.DrawImage($master, 0, 0, $size, $size)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

function ConvertTo-BigEndian([uint32]$v) {
    $b = [BitConverter]::GetBytes($v)
    [Array]::Reverse($b)
    return , $b
}

$master = New-MasterIcon 1024

# icon.png: used for the window icon.
[IO.File]::WriteAllBytes((Join-Path $assets 'icon.png'), (Get-PngBytes $master 256))

# icon.ico: PNG-compressed entries, supported since Windows Vista.
$sizes = 16, 24, 32, 48, 64, 128, 256
$images = $sizes | ForEach-Object { , (Get-PngBytes $master $_) }
$ms = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ms
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $assets 'icon.ico'), $ms.ToArray())

# icon.icns: PNG chunks. Type codes: ic07=128, ic08=256, ic09=512, ic10=1024, ic11=32 (16@2x), ic12=64 (32@2x).
$chunks = [ordered]@{ ic07 = 128; ic08 = 256; ic09 = 512; ic10 = 1024; ic11 = 32; ic12 = 64 }
$body = New-Object System.IO.MemoryStream
foreach ($type in $chunks.Keys) {
    $png = Get-PngBytes $master $chunks[$type]
    $body.Write([Text.Encoding]::ASCII.GetBytes($type), 0, 4)
    $len = ConvertTo-BigEndian ([uint32]($png.Length + 8))
    $body.Write($len, 0, 4)
    $body.Write($png, 0, $png.Length)
}
$icns = New-Object System.IO.MemoryStream
$icns.Write([Text.Encoding]::ASCII.GetBytes('icns'), 0, 4)
$total = ConvertTo-BigEndian ([uint32]($body.Length + 8))
$icns.Write($total, 0, 4)
$body.WriteTo($icns)
[IO.File]::WriteAllBytes((Join-Path $assets 'icon.icns'), $icns.ToArray())

$master.Dispose()
Get-ChildItem $assets | Select-Object Name, Length
