param(
    [Parameter(Mandatory = $true)]
    [string[]]$Path,
    [string]$ExpectedSubject = '',
    [switch]$RequireTimestamp
)

$ErrorActionPreference = 'Stop'

$results = foreach ($requestedPath in $Path) {
    $resolvedPath = (Resolve-Path -LiteralPath $requestedPath).Path
    $signature = Get-AuthenticodeSignature -LiteralPath $resolvedPath
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Assinatura inválida em ${resolvedPath}: $($signature.Status) — $($signature.StatusMessage)"
    }
    if ($null -eq $signature.SignerCertificate) {
        throw "O Windows não informou o certificado do assinante: $resolvedPath"
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedSubject) -and
        $signature.SignerCertificate.Subject.IndexOf($ExpectedSubject, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Publicador inesperado em ${resolvedPath}: $($signature.SignerCertificate.Subject)"
    }
    if ($RequireTimestamp -and $null -eq $signature.TimeStamperCertificate) {
        throw "A assinatura não possui timestamp verificável: $resolvedPath"
    }

    [pscustomobject]@{
        Path = $resolvedPath
        Status = $signature.Status
        Subject = $signature.SignerCertificate.Subject
        Thumbprint = $signature.SignerCertificate.Thumbprint
        TimestampSubject = if ($signature.TimeStamperCertificate) { $signature.TimeStamperCertificate.Subject } else { '' }
    }
}

$results
