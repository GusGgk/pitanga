param([string]$OutDir, [string]$MsixDir)
# Desenha a pitanga (fruta gomada, vermelho-alaranjada, com coroa verde) e empacota um .ico multi-tamanho (entradas PNG).
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutDir | Out-Null

function Draw-Pitanga([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.Clear([System.Drawing.Color]::Transparent)

    $m = [math]::Max(1, $s * 0.05)
    $top = $s * 0.2; $h = $s * 0.66; $W = $s - 2 * $m
    $area = New-Object System.Drawing.RectangleF $m, $top, $W, $h
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush $area, ([System.Drawing.Color]::FromArgb(255, 255, 84, 56)), ([System.Drawing.Color]::FromArgb(255, 178, 16, 30)), 70

    # Gomos: elipses verticais sobrepostas -> contorno recortado típico da pitanga.
    # Desenha das bordas para o centro; cada gomo ganha um contorno escuro sutil (o "vinco").
    $lobes = 5; $lw = $W * 0.36; $step = ($W - $lw) / ($lobes - 1)
    $order = 0, 4, 1, 3, 2
    $crease = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(($(if ($s -ge 24) { 90 } else { 0 })), 110, 8, 0)), ([math]::Max(1, $s / 48))
    foreach ($i in $order) {
        $x = $m + $i * $step
        $inset = [math]::Abs($i - 2) * $h * 0.06   # gomos das pontas um pouco mais baixos
        $r = New-Object System.Drawing.RectangleF $x, ($top + $inset), $lw, ($h - 2 * $inset)
        $g.FillEllipse($grad, $r)
        if ($s -ge 24) { $g.DrawEllipse($crease, $r) }
    }
    $crease.Dispose()

    # Brilho no gomo da esquerda
    $hl = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(80, 255, 255, 255))
    $g.FillEllipse($hl, $m + $W * 0.2, $top + $h * 0.14, $W * 0.16, $h * 0.26)

    # Coroa: sépalas verdes pequenas, no topo
    $green = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 70, 150, 64))
    $cx = $s / 2; $cy = $top + $s * 0.03; $R = $s * 0.11; $r0 = $s * 0.035
    $pts = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
    for ($i = 0; $i -lt 10; $i++) {
        $a = -[math]::PI / 2 + $i * [math]::PI / 5
        $rr = if ($i % 2 -eq 0) { $R } else { $r0 }
        $pts.Add((New-Object System.Drawing.PointF ($cx + $rr * [math]::Cos($a)), ($cy + $rr * 0.75 * [math]::Sin($a))))
    }
    $g.FillPolygon($green, $pts.ToArray())

    $g.Dispose(); $grad.Dispose(); $hl.Dispose(); $green.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$pngs = foreach ($s in $sizes) {
    $b = Draw-Pitanga $s
    $ms = New-Object IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($s -eq 256) { $b.Save((Join-Path $OutDir 'Pitanga-256.png')) }
    $b.Dispose(); , $ms.ToArray()
}

# ICO: ICONDIR + ICONDIRENTRY[] + dados PNG
$out = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$len); $w.Write([uint32]$offset); $offset += $len
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush(); [IO.File]::WriteAllBytes((Join-Path $OutDir 'Pitanga.ico'), $out.ToArray())

# Logos do pacote MSIX (fruta centralizada com respiro, fundo transparente)
if ($MsixDir) {
    New-Item -ItemType Directory -Force $MsixDir | Out-Null
    foreach ($logo in @(@{ Name = 'Square44x44Logo.png'; Canvas = 44; Fruit = 40 }, @{ Name = 'Square150x150Logo.png'; Canvas = 150; Fruit = 104 }, @{ Name = 'StoreLogo.png'; Canvas = 50; Fruit = 44 })) {
        $canvas = New-Object System.Drawing.Bitmap $logo.Canvas, $logo.Canvas, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($canvas); $g.Clear([System.Drawing.Color]::Transparent)
        $fruit = Draw-Pitanga $logo.Fruit
        $off = [int](($logo.Canvas - $logo.Fruit) / 2)
        $g.DrawImage($fruit, $off, $off, $logo.Fruit, $logo.Fruit)
        $g.Dispose(); $fruit.Dispose()
        $canvas.Save((Join-Path $MsixDir $logo.Name), [System.Drawing.Imaging.ImageFormat]::Png); $canvas.Dispose()
    }
}
Get-ChildItem $OutDir | Select-Object Name, Length
