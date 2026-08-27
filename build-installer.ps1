param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $OutputDirectory

$compilerCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

if ($compilerCandidates.Count -eq 0) {
    throw 'Inno Setup 6 não encontrado. Instale com: winget install --id JRSoftware.InnoSetup --exact'
}

$scriptPath = Join-Path $PSScriptRoot 'installer\Neck.iss'
$innoCompiler = @($compilerCandidates)[0]
& $innoCompiler $scriptPath
if ($LASTEXITCODE -ne 0) {
    throw "A criação do instalador falhou com o código $LASTEXITCODE."
}

$files = @(
    (Join-Path $OutputDirectory 'Neck.exe'),
    (Join-Path $OutputDirectory 'Neck-Setup-1.10.0.exe')
)
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Artefato não encontrado: $file" }
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    $hashPath = $file + '.sha256'
    Set-Content -LiteralPath $hashPath -Value ($hash + '  ' + (Split-Path -Leaf $file)) -Encoding ascii
    Get-Item -LiteralPath $file, $hashPath | Select-Object FullName, Length, LastWriteTime
}
