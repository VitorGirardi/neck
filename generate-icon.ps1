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

$navy = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 11, 19, 36))
$cyan = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 14, 165, 168), 18)
$white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)

$graphics.FillPath($navy, $path)
$cyan.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$cyan.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$top = [System.Drawing.Drawing2D.GraphicsPath]::new()
$bottom = [System.Drawing.Drawing2D.GraphicsPath]::new()
$top.AddBezier(45, 66, 91, 66, 87, 108, 128, 108)
$top.AddBezier(128, 108, 169, 108, 165, 66, 211, 66)
$bottom.AddBezier(45, 190, 91, 190, 87, 148, 128, 148)
$bottom.AddBezier(128, 148, 169, 148, 165, 190, 211, 190)
$graphics.DrawPath($cyan, $top)
$graphics.DrawPath($cyan, $bottom)
$graphics.FillEllipse($white, 115, 115, 26, 26)

$handle = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    $stream = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create)
    try { $icon.Save($stream) } finally { $stream.Dispose() }
} finally {
    [NeckIconNative]::DestroyIcon($handle) | Out-Null
    $top.Dispose()
    $bottom.Dispose()
    $white.Dispose()
    $cyan.Dispose()
    $navy.Dispose()
    $path.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Get-Item -LiteralPath $OutputPath | Select-Object FullName, Length
