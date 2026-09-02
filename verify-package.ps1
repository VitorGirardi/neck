param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'),
    [string]$ExpectedVersion = '1.20.0.0',
    [switch]$AllowUnsigned,
    [string]$ExpectedSigner = '',
    [switch]$RequireTimestamp
)

$ErrorActionPreference = 'Stop'

$artifacts = @(
    (Join-Path $OutputDirectory 'Neck.exe'),
    (Join-Path $OutputDirectory 'Neck-Setup-1.19.0.exe')
)

foreach ($artifact in $artifacts) {
    $item = Get-Item -LiteralPath $artifact -ErrorAction Stop
    $fileVersion = ([Convert]::ToString($item.VersionInfo.FileVersion)).Trim()
    if ($fileVersion -ne $ExpectedVersion) {
        throw "Versão inesperada em $($item.Name): $fileVersion; esperado $ExpectedVersion."
    }

    $checksumPath = $artifact + '.sha256'
    $checksumLine = (Get-Content -LiteralPath $checksumPath -Raw -ErrorAction Stop).Trim()
    $expectedLine = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $item.Name
    if (-not [string]::Equals($checksumLine, $expectedLine, [StringComparison]::Ordinal)) {
        throw "Checksum inválido para $($item.Name)."
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $artifact
    if ($AllowUnsigned) {
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::NotSigned -and
            $signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            throw "Estado de assinatura inesperado em $($item.Name): $($signature.Status)."
        }
    }
    else {
        & (Join-Path $PSScriptRoot 'verify-authenticode.ps1') -Path $artifact -ExpectedSubject $ExpectedSigner -RequireTimestamp:$RequireTimestamp
    }

    [pscustomobject]@{
        Name = $item.Name
        Version = $fileVersion
        Length = $item.Length
        SHA256 = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
        Signature = $signature.Status
    }
}
