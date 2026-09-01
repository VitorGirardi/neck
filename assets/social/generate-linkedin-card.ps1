param(
    [string]$ScreenshotPath = (Join-Path $PSScriptRoot '..\screenshots\neck-dashboard.png'),
    [string]$OutputPath = (Join-Path $PSScriptRoot 'neck-linkedin-beta.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param([float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Font {
    param([string]$Preferred, [float]$Size, [System.Drawing.FontStyle]$Style)
    try { return [System.Drawing.Font]::new($Preferred, $Size, $Style, [System.Drawing.GraphicsUnit]::Pixel) }
    catch { return [System.Drawing.Font]::new('Segoe UI', $Size, $Style, [System.Drawing.GraphicsUnit]::Pixel) }
}

$width = 1280
$height = 640
$bitmap = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$screenshot = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $ScreenshotPath))

try {
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $graphics.Clear([System.Drawing.Color]::FromArgb(244, 243, 237))

    $ink = [System.Drawing.Color]::FromArgb(31, 41, 37)
    $muted = [System.Drawing.Color]::FromArgb(82, 100, 91)
    $lime = [System.Drawing.Color]::FromArgb(182, 239, 103)
    $green = [System.Drawing.Color]::FromArgb(47, 125, 89)
    $card = [System.Drawing.Color]::FromArgb(255, 255, 252)

    $accentBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(68, 182, 239, 103))
    $graphics.FillEllipse($accentBrush, -90, 390, 520, 380)
    $accentBrush.Dispose()

    $darkBrush = New-Object System.Drawing.SolidBrush($ink)
    $limeBrush = New-Object System.Drawing.SolidBrush($lime)
    $leftMark = New-Object System.Drawing.Drawing2D.GraphicsPath
    $leftMark.StartFigure()
    $leftMark.AddLine(38, 24, 88, 24)
    $leftMark.AddBezier(88, 24, 88, 72, 97, 104, 122, 128)
    $leftMark.AddBezier(122, 128, 97, 152, 88, 184, 88, 232)
    $leftMark.AddLine(88, 232, 38, 232)
    $leftMark.AddBezier(38, 232, 38, 180, 51, 148, 76, 128)
    $leftMark.AddBezier(76, 128, 51, 108, 38, 76, 38, 24)
    $leftMark.CloseFigure()

    $rightMark = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rightMark.StartFigure()
    $rightMark.AddLine(218, 24, 168, 24)
    $rightMark.AddBezier(168, 24, 168, 72, 159, 104, 134, 128)
    $rightMark.AddBezier(134, 128, 159, 152, 168, 184, 168, 232)
    $rightMark.AddLine(168, 232, 218, 232)
    $rightMark.AddBezier(218, 232, 218, 180, 205, 148, 180, 128)
    $rightMark.AddBezier(180, 128, 205, 108, 218, 76, 218, 24)
    $rightMark.CloseFigure()

    $markState = $graphics.Save()
    $graphics.TranslateTransform(62, 54)
    $graphics.ScaleTransform(0.42, 0.42)
    $graphics.FillPath($darkBrush, $leftMark)
    $graphics.FillPath($darkBrush, $rightMark)
    $flowPen = New-Object System.Drawing.Pen($lime, 18)
    $flowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $flowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($flowPen, 58, 128, 182, 128)
    $graphics.FillPolygon($limeBrush, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(176, 105),
        [System.Drawing.PointF]::new(206, 128),
        [System.Drawing.PointF]::new(176, 151)
    ))
    $graphics.Restore($markState)
    $flowPen.Dispose()
    $leftMark.Dispose()
    $rightMark.Dispose()

    $brandFont = New-Font 'Segoe UI Variable Display' 58 ([System.Drawing.FontStyle]::Bold)
    $eyebrowFont = New-Font 'Segoe UI Semibold' 19 ([System.Drawing.FontStyle]::Bold)
    $titleFont = New-Font 'Segoe UI Variable Display' 47 ([System.Drawing.FontStyle]::Bold)
    $bodyFont = New-Font 'Segoe UI' 23 ([System.Drawing.FontStyle]::Regular)
    $smallFont = New-Font 'Segoe UI Semibold' 18 ([System.Drawing.FontStyle]::Bold)
    $inkBrush = New-Object System.Drawing.SolidBrush($ink)
    $mutedBrush = New-Object System.Drawing.SolidBrush($muted)
    $greenBrush = New-Object System.Drawing.SolidBrush($green)

    $graphics.DrawString('Neck', $brandFont, $inkBrush, 176, 62)
    $graphics.DrawString('BETA PÚBLICA  •  OPEN SOURCE  •  WINDOWS', $eyebrowFont, $greenBrush, 72, 170)
    $graphics.DrawString("Destrave o fluxo do`nseu computador.", $titleFont, $inkBrush, 67, 215)
    $graphics.DrawString("Diagnóstico local. Ações seguras.`nMudanças reversíveis.", $bodyFont, $mutedBrush, 72, 344)

    $pill = New-RoundedRectanglePath 72 466 465 62 20
    $graphics.FillPath($limeBrush, $pill)
    $graphics.DrawString('github.com/VitorGirardi/neck', $smallFont, $inkBrush, 101, 483)
    $pill.Dispose()

    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(28, 31, 41, 37))
    $shadow = New-RoundedRectanglePath 607 65 620 490 30
    $graphics.FillPath($shadowBrush, $shadow)
    $shadow.Dispose()
    $shadowBrush.Dispose()

    $frame = New-RoundedRectanglePath 595 53 620 490 30
    $cardBrush = New-Object System.Drawing.SolidBrush($card)
    $graphics.FillPath($cardBrush, $frame)
    $oldClip = $graphics.Clip
    $graphics.SetClip($frame)
    $graphics.DrawImage($screenshot, [System.Drawing.RectangleF]::new(609, 67, 592, 462))
    $graphics.Clip = $oldClip
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(218, 224, 216), 2)
    $graphics.DrawPath($borderPen, $frame)

    $borderPen.Dispose()
    $cardBrush.Dispose()
    $frame.Dispose()
    $oldClip.Dispose()
    $inkBrush.Dispose()
    $mutedBrush.Dispose()
    $greenBrush.Dispose()
    $darkBrush.Dispose()
    $limeBrush.Dispose()
    $brandFont.Dispose()
    $eyebrowFont.Dispose()
    $titleFont.Dispose()
    $bodyFont.Dispose()
    $smallFont.Dispose()

    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) { [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null }
    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    Get-Item -LiteralPath $OutputPath | Select-Object FullName, Length
}
finally {
    $screenshot.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}
