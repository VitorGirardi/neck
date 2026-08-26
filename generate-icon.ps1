param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not ([System.Management.Automation.PSTypeName]'NeckIconNative').Type) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class NeckIconNative {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool DestroyIcon(IntPtr handle);
}
'@
}

$directory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $directory -Force | Out-Null

$bitmap = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$graphics.Clear([System.Drawing.Color]::Transparent)

$path = [System.Drawing.Drawing2D.GraphicsPath]::new()
$path.AddArc(12, 12, 72, 72, 180, 90)
$path.AddArc(172, 12, 72, 72, 270, 90)
$path.AddArc(172, 172, 72, 72, 0, 90)
$path.AddArc(12, 172, 72, 72, 90, 90)
$path.CloseFigure()

$navy = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 15, 23, 42))
$cyan = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 6, 182, 212))
$white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$font = [System.Drawing.Font]::new('Segoe UI Semibold', 132, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

$graphics.FillPath($navy, $path)
$graphics.FillRectangle($cyan, 28, 42, 18, 172)
$format = [System.Drawing.StringFormat]::new()
$format.Alignment = [System.Drawing.StringAlignment]::Center
$format.LineAlignment = [System.Drawing.StringAlignment]::Center
$graphics.DrawString('N', $font, $white, [System.Drawing.RectangleF]::new(34, 20, 194, 210), $format)

$handle = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    $stream = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create)
    try { $icon.Save($stream) } finally { $stream.Dispose() }
} finally {
    [NeckIconNative]::DestroyIcon($handle) | Out-Null
    $format.Dispose()
    $font.Dispose()
    $white.Dispose()
    $cyan.Dispose()
    $navy.Dispose()
    $path.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Get-Item -LiteralPath $OutputPath | Select-Object FullName, Length
