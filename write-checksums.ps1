param(
    [Parameter(Mandatory = $true)]
    [string[]]$Path
)

$ErrorActionPreference = 'Stop'

foreach ($requestedPath in $Path) {
    $resolvedPath = (Resolve-Path -LiteralPath $requestedPath).Path
    $item = Get-Item -LiteralPath $resolvedPath
    if ($item.PSIsContainer) { throw "O caminho aponta para uma pasta: $resolvedPath" }

    $hash = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $hashPath = $resolvedPath + '.sha256'
    Set-Content -LiteralPath $hashPath -Value ($hash + '  ' + $item.Name) -Encoding ascii
    Get-Item -LiteralPath $resolvedPath, $hashPath | Select-Object FullName, Length, LastWriteTime
}
