<#
.SYNOPSIS
    Draws the application icon (src\BandBar\BandBar.ico): a red up arrow and a green down arrow, side by side.

.DESCRIPTION
    The icon is generated rather than hand-drawn so it can be reproduced and reviewed. Sizes 16–64 are stored as
    32-bit bitmaps with alpha, 256 as PNG, which is what Windows expects in an .ico file.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\make-icon.ps1
#>
[CmdletBinding()]
param(
    [string] $OutFile
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Drawing
# $PSScriptRoot is empty in parameter defaults on Windows PowerShell 5.1, so the default is set here.
if (-not $OutFile) { $OutFile = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\BandBar\BandBar.ico' }

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$red = [Drawing.Color]::FromArgb(0xE5, 0x39, 0x35)
$green = [Drawing.Color]::FromArgb(0x27, 0xAE, 0x60)

# Arrow as a polygon in a box: triangle head plus stem; $up decides the direction.
function Get-Arrow([double] $x, [double] $y, [double] $w, [double] $h, [bool] $up) {
    $head = $h * 0.48
    $half = $w * 0.22          # half of the stem width
    $cx = $x + $w / 2
    $left = $cx - $half
    $right = $cx + $half
    # (x, distance from the tip) pairs, drawn pointing up and mirrored for down.
    $xs = @($cx, ($x + $w), $right, $right, $left, $left, $x)
    $ds = @(0, $head, $head, $h, $h, $head, $head)
    $points = New-Object 'Drawing.PointF[]' 7
    for ($i = 0; $i -lt 7; $i++) {
        $py = if ($up) { $y + $ds[$i] } else { $y + $h - $ds[$i] }
        $points[$i] = New-Object Drawing.PointF ([single] $xs[$i]), ([single] $py)
    }
    return ,$points
}

function New-IconBitmap([int] $s) {
    $bmp = New-Object Drawing.Bitmap $s, $s, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([Drawing.Color]::Transparent)
    $m = $s * 0.05
    $gap = $s * 0.06
    $w = ($s - 2 * $m - $gap) / 2
    $h = $s - 2 * $m
    $brush = New-Object Drawing.SolidBrush $red
    $g.FillPolygon($brush, (Get-Arrow $m $m $w $h $true))
    $brush.Dispose()
    $brush = New-Object Drawing.SolidBrush $green
    $g.FillPolygon($brush, (Get-Arrow ($m + $w + $gap) $m $w $h $false))
    $brush.Dispose()
    $g.Dispose()
    return $bmp
}

# 32-bit DIB for an .ico entry: BITMAPINFOHEADER (double height), bottom-up BGRA rows, then a 1-bit AND mask.
function Get-DibBytes([Drawing.Bitmap] $bmp) {
    $s = $bmp.Width
    $ms = New-Object IO.MemoryStream
    $bw = New-Object IO.BinaryWriter $ms
    $bw.Write([int] 40); $bw.Write([int] $s); $bw.Write([int] ($s * 2)); $bw.Write([int16] 1); $bw.Write([int16] 32)
    $bw.Write([int] 0); $bw.Write([int] 0); $bw.Write([int] 0); $bw.Write([int] 0); $bw.Write([int] 0); $bw.Write([int] 0)
    $rect = New-Object Drawing.Rectangle 0, 0, $s, $s
    $data = $bmp.LockBits($rect, 'ReadOnly', ([Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $pixels = New-Object byte[] ($data.Stride * $s)
    [Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bmp.UnlockBits($data)
    for ($row = $s - 1; $row -ge 0; $row--) { $bw.Write($pixels, $row * $data.Stride, $s * 4) }
    $maskStride = [int]([Math]::Ceiling($s / 32.0) * 4)
    for ($row = $s - 1; $row -ge 0; $row--) {
        $mask = New-Object byte[] $maskStride
        for ($col = 0; $col -lt $s; $col++) {
            if ($pixels[$row * $data.Stride + $col * 4 + 3] -eq 0) { $mask[[int][Math]::Floor($col / 8)] = $mask[[int][Math]::Floor($col / 8)] -bor (0x80 -shr ($col % 8)) }
        }
        $bw.Write($mask)
    }
    $bw.Flush()
    return $ms.ToArray()
}

$images = foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    if ($s -ge 256) {
        $ms = New-Object IO.MemoryStream
        $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
        $bytes = $ms.ToArray()
    } else {
        $bytes = Get-DibBytes $bmp
    }
    $bmp.Dispose()
    [pscustomobject]@{ Size = $s; Bytes = [byte[]] $bytes }
}

$out = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $out
$w.Write([int16] 0); $w.Write([int16] 1); $w.Write([int16] $images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $w.Write([byte] $dim); $w.Write([byte] $dim); $w.Write([byte] 0); $w.Write([byte] 0)
    $w.Write([int16] 1); $w.Write([int16] 32); $w.Write([int] $img.Bytes.Length); $w.Write([int] $offset)
    $offset += $img.Bytes.Length
}
foreach ($img in $images) { $w.Write($img.Bytes) }
$w.Flush()
[IO.File]::WriteAllBytes($OutFile, $out.ToArray())
Write-Host "Wrote $OutFile ($($out.Length) bytes, sizes: $($sizes -join ', '))"
