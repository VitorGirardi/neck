$ErrorActionPreference = 'Stop'

$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testDirectory = Join-Path $PSScriptRoot 'test-output'
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null

$common = @(
    '/nologo'
    '/platform:x64'
    '/optimize+'
    '/warn:4'
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
)

$selfTest = Join-Path $testDirectory 'MestrePCCare.SelfTest.exe'
$selfArguments = $common + @(
    '/target:exe'
    '/main:MestrePCCare.SelfTest'
    "/out:$selfTest"
    (Join-Path $PSScriptRoot 'Program.cs')
    (Join-Path $PSScriptRoot 'SelfTest.cs')
)
& $compiler $selfArguments
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o autoteste.' }
& $selfTest
if ($LASTEXITCODE -ne 0) { throw 'O autoteste falhou.' }

$uiProbe = Join-Path $testDirectory 'MestrePCCare.UIProbe.exe'
$uiArguments = $common + @(
    '/target:winexe'
    '/define:NOELEVATION'
    '/main:MestrePCCare.Program'
    "/out:$uiProbe"
    (Join-Path $PSScriptRoot 'Program.cs')
)
& $compiler $uiArguments
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar a verificação visual.' }

Write-Output "UI_PROBE=$uiProbe"
