param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputPath = Join-Path $OutputDirectory 'Neck.exe'
if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Force
}

$artifactDirectory = Join-Path $PSScriptRoot 'build-artifacts'
$iconPath = Join-Path $artifactDirectory 'neck.ico'
& (Join-Path $PSScriptRoot 'generate-icon.ps1') -OutputPath $iconPath

$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'O compilador do .NET Framework 4.8 não foi encontrado.'
}

$arguments = @(
    '/nologo'
    '/target:winexe'
    '/platform:x64'
    '/optimize+'
    '/warn:4'
    "/win32manifest:$PSScriptRoot\app.manifest"
    "/win32icon:$iconPath"
    "/out:$outputPath"
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Management.dll'
    '/reference:System.Windows.Forms.dll'
)
$sourceFiles = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' |
    Where-Object { $_.Name -ne 'SelfTest.cs' } |
    Sort-Object Name |
    ForEach-Object { $_.FullName }
$arguments += $sourceFiles

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "A compilação falhou com o código $LASTEXITCODE."
}

Get-Item -LiteralPath $outputPath | Select-Object FullName, Length, LastWriteTime
