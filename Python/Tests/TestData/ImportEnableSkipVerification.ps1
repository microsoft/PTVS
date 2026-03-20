param(
    [Parameter(Mandatory = $true)]
    [string]$RegFilePath
)

$ErrorActionPreference = 'Stop'

Write-Host "Importing skip verification settings from: $RegFilePath"

if (-not (Test-Path -LiteralPath $RegFilePath -PathType Leaf)) {
    throw "Registry file not found: $RegFilePath"
}

reg import "$RegFilePath"

if ($LASTEXITCODE -ne 0) {
    throw "reg import failed with exit code $LASTEXITCODE for: $RegFilePath"
}

Write-Host "Successfully imported skip verification settings."