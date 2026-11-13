# Gera o ícone da Central DLB (mesmo estilo do DLB Criativo: fundo escuro arredondado,
# laranja/azul/creme e "DLB" no canto). Saída: central\icone.ico e site\icone.png
# Uso: powershell -ExecutionPolicy Bypass -File tools\gerar-icone.ps1
Add-Type -AssemblyName System.Drawing
$raiz = Split-Path $PSScriptRoot -Parent

$escuro  = [Drawing.Color]::FromArgb(255, 0x17, 0x17, 0x19)
$laranja = [Drawing.Color]::FromArgb(255, 0xF9, 0x7A, 0x1F)
$azul    = [Drawing.Color]::FromArgb(255, 0x3B, 0x8B, 0xEB)
$creme   = [Drawing.Color]::FromArgb(255, 0xEC, 0xEB, 0xE7)

function Arred([Drawing.Drawing2D.GraphicsPath]$p, [float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
}

function Desenhar([int]$t) {
    $bmp = New-Object Drawing.Bitmap $t, $t
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.Clear([Drawing.Color]::Transparent)
    $s = $t / 512.0
    $comTexto = $t -ge 48

    # fundo
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    Arred $p (16*$s) (16*$s) (480*$s) (480*$s) (68*$s)
    $g.FillPath((New-Object Drawing.SolidBrush $escuro), $p)
    # 4 quadradinhos: 3 programas (azul, creme, laranja) + seta de baixar
    $m = $(if ($comTexto) { 96 } else { 56 }) * $s
    $lado = $(if ($comTexto) { 300 } else { 400 }) * $s
    $gap = $lado * 0.09
    $q = ($lado - $gap) / 2
    $x0 = (512*$s - $lado) / 2; $y0 = $(if ($comTexto) { 60*$s } else { (512*$s - $lado) / 2 })
    $cores = @($azul, $creme, $laranja)
    for ($i = 0; $i -lt 3; $i++) {
        $cx = $x0 + ($i % 2) * ($q + $gap); $cy = $y0 + [Math]::Floor($i / 2) * ($q + $gap)
        $pq = New-Object Drawing.Drawing2D.GraphicsPath
        Arred $pq $cx $cy $q $q ($q * 0.22)
        $g.FillPath((New-Object Drawing.SolidBrush $cores[$i]), $pq)
    }
    # seta para baixo no 4º lugar
    $ax = $x0 + $q + $gap; $ay = $y0 + $q + $gap
    $pen = New-Object Drawing.Pen $laranja, ([Math]::Max(2, $q * 0.17))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $mx = $ax + $q / 2
    $g.DrawLine($pen, $mx, ($ay + $q * 0.12), $mx, ($ay + $q * 0.70))
    $g.DrawLines($pen, [Drawing.PointF[]]@((New-Object Drawing.PointF ($ax + $q*0.22), ($ay + $q*0.46)), (New-Object Drawing.PointF $mx, ($ay + $q*0.74)), (New-Object Drawing.PointF ($ax + $q*0.78), ($ay + $q*0.46))))
    $g.DrawLine($pen, ($ax + $q*0.15), ($ay + $q*0.92), ($ax + $q*0.85), ($ay + $q*0.92))

    if ($comTexto) {
        $f = New-Object Drawing.Font 'Segoe UI', (60*$s), ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
        $sf = New-Object Drawing.StringFormat; $sf.Alignment = 'Far'; $sf.LineAlignment = 'Far'
        $g.DrawString('DLB', $f, (New-Object Drawing.SolidBrush $creme), (New-Object Drawing.RectangleF 0, 0, (432*$s), (452*$s)), $sf)
    }
    $g.Dispose()
    return $bmp
}
# PNG grande
$grande = Desenhar 512
$grande.Save((Join-Path $raiz 'site\icone.png'), [Drawing.Imaging.ImageFormat]::Png)

# ICO com vários tamanhos: 256 em PNG; os menores em BMP 32 bits (o formato que todo programa entende)
function Dib([Drawing.Bitmap]$bmp) {
    $t = $bmp.Width
    $ms = New-Object IO.MemoryStream
    $bw = New-Object IO.BinaryWriter $ms
    $bw.Write([UInt32]40); $bw.Write([Int32]$t); $bw.Write([Int32]($t * 2)); $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]0); $bw.Write([UInt32]0); $bw.Write([Int32]0); $bw.Write([Int32]0); $bw.Write([UInt32]0); $bw.Write([UInt32]0)
    for ($y = $t - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $t; $x++) { $c = $bmp.GetPixel($x, $y); $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A) }
    }
    $linha = [int]([Math]::Ceiling($t / 32.0) * 4)
    $bw.Write((New-Object byte[] ($linha * $t)))   # máscara AND vazia (a transparência vem do alfa)
    return ,$ms.ToArray()
}
$tamanhos = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = foreach ($t in $tamanhos) {
    $img = Desenhar $t
    if ($t -ge 256) { $ms = New-Object IO.MemoryStream; $img.Save($ms, [Drawing.Imaging.ImageFormat]::Png); ,$ms.ToArray() }
    else { ,(Dib $img) }
}
$out = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$tamanhos.Count)
$offset = 6 + 16 * $tamanhos.Count
for ($i = 0; $i -lt $tamanhos.Count; $i++) {
    $t = $tamanhos[$i]; $b = if ($t -ge 256) { 0 } else { $t }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
[IO.File]::WriteAllBytes((Join-Path $raiz 'central\icone.ico'), $out.ToArray())
"Icone gerado: site\icone.png e central\icone.ico"
