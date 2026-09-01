param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,
    [string]$PreviewPath = ''
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

$left = [System.Drawing.Drawing2D.GraphicsPath]::new()
$left.StartFigure()
$left.AddLine(38, 24, 88, 24)
$left.AddBezier(88, 24, 88, 72, 97, 104, 122, 128)
$left.AddBezier(122, 128, 97, 152, 88, 184, 88, 232)
$left.AddLine(88, 232, 38, 232)
$left.AddBezier(38, 232, 38, 180, 51, 148, 76, 128)
$left.AddBezier(76, 128, 51, 108, 38, 76, 38, 24)
$left.CloseFigure()

$right = [System.Drawing.Drawing2D.GraphicsPath]::new()
$right.StartFigure()
$right.AddLine(218, 24, 168, 24)
$right.AddBezier(168, 24, 168, 72, 159, 104, 134, 128)
$right.AddBezier(134, 128, 159, 152, 168, 184, 168, 232)
$right.AddLine(168, 232, 218, 232)
$right.AddBezier(218, 232, 218, 180, 205, 148, 180, 128)
$right.AddBezier(180, 128, 205, 108, 218, 76, 218, 24)
$right.CloseFigure()

$ink = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 31, 41, 37))
$lime = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 182, 239, 103))
$flow = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 182, 239, 103), 18)
$flow.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$flow.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

$graphics.FillPath($ink, $left)
$graphics.FillPath($ink, $right)
$graphics.DrawLine($flow, 58, 128, 184, 128)
$arrow = [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(176, 105),
    [System.Drawing.PointF]::new(206, 128),
    [System.Drawing.PointF]::new(176, 151)
)
$graphics.FillPolygon($lime, $arrow)

if (-not [string]::IsNullOrWhiteSpace($PreviewPath)) {
    $previewDirectory = Split-Path -Parent $PreviewPath
    if ($previewDirectory) { New-Item -ItemType Directory -Path $previewDirectory -Force | Out-Null }
    $bitmap.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
}

$handle = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    $stream = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create)
    try { $icon.Save($stream) } finally { $stream.Dispose() }
} finally {
    [NeckIconNative]::DestroyIcon($handle) | Out-Null
    $flow.Dispose()
    $lime.Dispose()
    $ink.Dispose()
    $left.Dispose()
    $right.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Get-Item -LiteralPath $OutputPath | Select-Object FullName, Length
