Add-Type -AssemblyName System.Drawing

function New-IconFromBitmap([System.Drawing.Bitmap]$bmp, [string]$path) {
    $icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
    $fs = [System.IO.File]::Create($path)
    try { $icon.Save($fs) } finally { $fs.Close(); $icon.Dispose() }
}

function New-AppIcon([int]$size, [string]$pngPath, [string]$path) {
    $src = [System.Drawing.Image]::FromFile($pngPath)
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $ratio = [Math]::Min($size / $src.Width, $size / $src.Height)
    $w = [int]($src.Width * $ratio)
    $h = [int]($src.Height * $ratio)
    $x = [int](($size - $w) / 2)
    $y = [int](($size - $h) / 2)
    $g.DrawImage($src, $x, $y, $w, $h)

    $g.Dispose()
    $src.Dispose()
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

$tmpDir = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

$mqttPng = Resolve-Path 'src/TaskbarMqtt/Assets/mqtt-icon.png'
$appPngs = @()
foreach ($s in 16, 32, 48, 64) {
    $p = Join-Path $tmpDir ("app-{0}.png" -f $s)
    New-AppIcon $s $mqttPng $p
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
