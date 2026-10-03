param(
    [Parameter(Mandatory = $true)]
    [string]$VerificationPath,
    [Parameter(Mandatory = $true)]
    [string]$BinInspectPath,
    [Parameter(Mandatory = $true)]
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$fonts = @(Get-ChildItem -LiteralPath $VerificationPath -File -Filter '*.ttf')
if ($fonts.Count -ne 4) {
    throw "Expected four prepared template fonts in '$VerificationPath', found $($fonts.Count)."
}
if (!(Test-Path -LiteralPath $BinInspectPath -PathType Leaf)) {
    throw "Insertion verifier not found at '$BinInspectPath'."
}

# Keep the exact inputs alongside the report, outside the folder being scanned.
$reportFonts = Join-Path $ReportPath 'fonts'
New-Item -ItemType Directory -Path $ReportPath | Out-Null
New-Item -ItemType Directory -Path $reportFonts | Out-Null
$hashes = @{}
foreach ($font in $fonts) {
    $hashes[$font.FullName] = (Get-FileHash -LiteralPath $font.FullName -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $font.FullName -Destination $reportFonts
    Write-Host "Insertion verification input: $($font.Name), SHA256: $($hashes[$font.FullName])"
}
$fonts | ForEach-Object {
    [PSCustomObject]@{ File = $_.Name; SHA256 = $hashes[$_.FullName] }
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ReportPath 'FontHashes.json') -Encoding UTF8

Write-Host "BinInspect SHA256: $((Get-FileHash -LiteralPath $BinInspectPath -Algorithm SHA256).Hash)"
Write-Host "Running insertion verifier: $BinInspectPath /a /v /3p /o `"$ReportPath`" `"$VerificationPath`""
& $BinInspectPath /a /v /3p /o $ReportPath $VerificationPath
$verifierExitCode = $LASTEXITCODE

foreach ($font in $fonts) {
    if ((Get-FileHash -LiteralPath $font.FullName -Algorithm SHA256).Hash -ne $hashes[$font.FullName]) {
        throw "Insertion verification modified '$($font.FullName)'. Original bytes are preserved in '$reportFonts'."
    }
}

$resultsPath = Join-Path $ReportPath 'SignResults.xml'
if (!(Test-Path -LiteralPath $resultsPath -PathType Leaf)) {
    throw "Insertion verifier produced no SignResults.xml (exit code $verifierExitCode). See '$ReportPath'."
}
[xml]$results = Get-Content -LiteralPath $resultsPath -Raw
$certificateResults = @($results.SelectNodes('/DATA/ROW[Type="Certificate"]'))
if ($certificateResults.Count -ne $fonts.Count) {
    throw "Expected four certificate results, found $($certificateResults.Count). See '$resultsPath'."
}

$failures = 0
foreach ($font in $fonts) {
    $matches = @($certificateResults | Where-Object { $_.Full -eq $font.FullName })
    if ($matches.Count -ne 1) {
        throw "Expected one certificate result for '$($font.FullName)', found $($matches.Count)."
    }
    $result = $matches[0]
    if ($result.Pass -ne 'True' -or $result.MSSigned -ne 'True') {
        $errorNode = $result.SelectSingleNode('Err')
        $detail = if ($errorNode) { $errorNode.InnerText } else { $result.OuterXml }
        Write-Host "Insertion font verification failed: $($font.Name): $detail"
        $failures++
    }
    else {
        Write-Host "Insertion font verification passed: $($font.Name)"
    }
}
if ($failures -gt 0) {
    throw "Insertion signature verification failed for $failures template fonts. See '$ReportPath'."
}
if ($verifierExitCode -ne 0) {
    throw "Insertion verifier exited with code $verifierExitCode. See '$ReportPath'."
}
