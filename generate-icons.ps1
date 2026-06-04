Add-Type -AssemblyName System.Drawing

function New-IconFromBitmap([System.Drawing.Bitmap]$bmp, [string]$path) {
    $icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
    $fs = [System.IO.File]::Create($path)
    try { $icon.Save($fs) } finally { $fs.Close(); $icon.Dispose() }
}

function New-AppIcon([int]$size, [string]$path) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # Filled rounded square (MQTT purple)
    $rect = New-Object System.Drawing.Rectangle 1, 1, ($size - 2), ($size - 2)
    $path1 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $radius = [Math]::Max(2, [int]($size / 6))
    $d = $radius * 2
    $path1.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path1.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path1.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path1.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path1.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(123, 31, 162))), $path1)

    # White "M" letter (larger, centered)
    $fontSize = [Math]::Max(7, [int]($size * 0.70))
    $font = New-Object System.Drawing.Font 'Arial Black', $fontSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textRect = New-Object System.Drawing.RectangleF 0, 0, $size, $size
    $g.DrawString('M', $font, [System.Drawing.Brushes]::White, $textRect, $sf)

    $g.Dispose()
    New-IconFromBitmap $bmp $path
    $bmp.Dispose()
}

function New-ButtonDefaultIcon([int]$size, [string]$path) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Gray filled circle
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(90, 100, 110))
    $g.FillEllipse($brush, 1, 1, ($size - 2), ($size - 2))

    # Inner lighter circle
    $innerBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(140, 150, 160))
    $inset = [int]($size / 5)
    $g.FillEllipse($innerBrush, $inset, $inset, ($size - 2 * $inset), ($size - 2 * $inset))

    $g.Dispose()
    New-IconFromBitmap $bmp $path
    $bmp.Dispose()
}

# Build a multi-resolution .ico by concatenating entries (16, 32, 48, 64)
function New-MultiResIcon([string[]]$bitmaps, [string]$outPath) {
    $entries = @()
    foreach ($p in $bitmaps) {
        $bmp = [System.Drawing.Image]::FromFile($p)
        $entries += [pscustomobject]@{ Size = $bmp.Size; Image = $bmp }
    }

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    # ICONDIR
    $bw.Write([uint16]0)        # reserved
    $bw.Write([uint16]1)        # type icon
    $bw.Write([uint16]$entries.Count)

    $headerSize = 6 + (16 * $entries.Count)
    $dataOffset = $headerSize

    # Build each icon entry's PNG data
    $pngBytes = @()
    foreach ($e in $entries) {
        $pngMs = New-Object System.IO.MemoryStream
        $e.Image.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngMs.Position = 0
        $bytes = New-Object byte[] $pngMs.Length
        [void]$pngMs.Read($bytes, 0, $bytes.Length)
        $pngBytes += ,$bytes
    }

    # ICONDIRENTRYs
    for ($i = 0; $i -lt $entries.Count; $i++) {
        $e = $entries[$i]
        $w = [byte]([Math]::Max(0, $e.Size.Width))
        $h = [byte]([Math]::Max(0, $e.Size.Height))
        if ($w -ge 256) { $w = 0 }
        if ($h -ge 256) { $h = 0 }
        $bw.Write([byte]$w)
        $bw.Write([byte]$h)
        $bw.Write([byte]0)            # color count
        $bw.Write([byte]0)            # reserved
        $bw.Write([uint16]1)          # planes
        $bw.Write([uint16]32)         # bit count
        $bw.Write([uint32]$pngBytes[$i].Length)
        $bw.Write([uint32]$dataOffset)
        $dataOffset += $pngBytes[$i].Length
    }

    # Image data
    foreach ($b in $pngBytes) { $bw.Write($b) }
    $bw.Flush()

    [System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
    $bw.Close()
    foreach ($e in $entries) { $e.Image.Dispose() }
}

$tmp = New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetTempFileName())
$tmpDir = $tmp.FullName
Remove-Item $tmpDir -Recurse -Force
$tmpDir = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

$appPngs = @()
foreach ($s in 16, 32, 48, 64) {
    $p = Join-Path $tmpDir ("app-{0}.png" -f $s)
    New-AppIcon $s $p
    $appPngs += $p
}
$appOut = Join-Path (Resolve-Path 'src/TaskbarMqtt/Assets').Path 'app.ico'
New-MultiResIcon $appPngs $appOut

$btnPngs = @()
foreach ($s in 16, 32, 48, 64) {
    $p = Join-Path $tmpDir ("btn-{0}.png" -f $s)
    New-ButtonDefaultIcon $s $p
    $btnPngs += $p
}
$btnOut = Join-Path (Resolve-Path 'src/TaskbarMqtt/Assets').Path 'button-default.ico'
New-MultiResIcon $btnPngs $btnOut

Remove-Item $tmpDir -Recurse -Force

Write-Host "Wrote $appOut"
Write-Host "Wrote $btnOut"
