param(
    [ValidateRange(20, 86400)]
    [int]$DurationSeconds = 600,
    [ValidateRange(1, 60)]
    [int]$SampleIntervalSeconds = 2
)

$ErrorActionPreference = 'Stop'

$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'O compilador do .NET Framework 4.8 não foi encontrado.'
}

$testDirectory = Join-Path $PSScriptRoot 'test-output'
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
$artifactDirectory = Join-Path $PSScriptRoot 'build-artifacts'
$iconPath = Join-Path $artifactDirectory 'neck.ico'
& (Join-Path $PSScriptRoot 'generate-icon.ps1') -OutputPath $iconPath

$applicationSources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' |
    Where-Object { $_.Name -ne 'SelfTest.cs' -and $_.Name -ne 'SoakTest.cs' } |
    Sort-Object Name |
    ForEach-Object { $_.FullName }

$probePath = Join-Path $testDirectory 'Neck.SoakTest.exe'
$arguments = @(
    '/nologo'
    '/target:exe'
    '/platform:x64'
    '/optimize+'
    '/warn:4'
    '/main:Neck.SoakTest'
    "/win32icon:$iconPath"
    "/out:$probePath"
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Management.dll'
    '/reference:System.Windows.Forms.dll'
)
$arguments += $applicationSources
$arguments += (Join-Path $PSScriptRoot 'SoakTest.cs')

& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o teste de resistência.' }

& $probePath '--duration-seconds' $DurationSeconds '--sample-seconds' $SampleIntervalSeconds
if ($LASTEXITCODE -ne 0) { throw "O teste de resistência falhou com o código $LASTEXITCODE." }
