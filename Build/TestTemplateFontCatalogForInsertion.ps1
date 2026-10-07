param(
    [Parameter(Mandatory = $true)]
    [string]$VerificationPath,
    [Parameter(Mandatory = $true)]
    [string]$OriginalFontPath,
    [Parameter(Mandatory = $true)]
    [string]$BinInspectPath,
    [Parameter(Mandatory = $true)]
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (!(Test-Path -LiteralPath $BinInspectPath -PathType Leaf)) {
    throw "BinInspect executable '$BinInspectPath' does not exist."
}

$expectedFontNames = @(
    'Microsoft.PythonTools.Django.Templates-0.ttf',
    'Microsoft.PythonTools.Web.Templates-0.ttf',
    'Microsoft.PythonTools.Web.Templates-1.ttf',
    'Microsoft.PythonTools.Web.Templates-2.ttf'
)

if (!(Test-Path -LiteralPath $VerificationPath -PathType Container)) {
    throw "VerificationPath '$VerificationPath' does not exist."
}

New-Item -ItemType Directory -Path $ReportPath -Force | Out-Null
$inputReportPath = Join-Path $ReportPath 'inputs'
$nativeReportPath = Join-Path $ReportPath 'native'
if (Test-Path -LiteralPath $nativeReportPath) {
    throw "Native report directory '$nativeReportPath' already exists."
}
New-Item -ItemType Directory -Path $inputReportPath -Force | Out-Null
New-Item -ItemType Directory -Path $nativeReportPath -Force | Out-Null

$binInspectExe = (Resolve-Path -LiteralPath $BinInspectPath).Path
Write-Host "BinInspect executable: $binInspectExe"

$inputFiles = @(Get-ChildItem -LiteralPath $VerificationPath -File)
if ($inputFiles.Count -ne 5) {
    throw "Expected exactly four template fonts and one catalog in '$VerificationPath', found $($inputFiles.Count) files."
}

$unexpectedFiles = @($inputFiles | Where-Object { $_.Extension -notin @('.ttf', '.cat') })
if ($unexpectedFiles.Count -gt 0) {
    throw "Unexpected file(s) in '$VerificationPath': $(@($unexpectedFiles.Name) -join ', ')."
}

$fontFiles = @($inputFiles | Where-Object { $_.Extension -ieq '.ttf' })
$catFiles = @($inputFiles | Where-Object { $_.Extension -ieq '.cat' })
if ($fontFiles.Count -ne 4) {
    throw "Expected exactly four TTF files in '$VerificationPath', found $($fontFiles.Count)."
}
if ($catFiles.Count -ne 1) {
    throw "Expected exactly one CAT file in '$VerificationPath', found $($catFiles.Count)."
}

$actualFontNames = @($fontFiles.Name | Sort-Object)
$expectedFontNamesSorted = @($expectedFontNames | Sort-Object)
$fontInputDiff = Compare-Object -ReferenceObject $expectedFontNamesSorted -DifferenceObject $actualFontNames
if ($null -ne $fontInputDiff) {
    throw "Template font filenames in '$VerificationPath' do not match the expected set: $(@($expectedFontNamesSorted) -join ', ')."
}

foreach ($font in $fontFiles) {
    $original = Join-Path $OriginalFontPath $font.Name
    if ((Get-FileHash -LiteralPath $original).Hash -ne (Get-FileHash -LiteralPath $font.FullName).Hash) {
        throw "Catalog generation or signing changed packaged font '$($font.Name)'."
    }
}

$inputSnapshot = @()
foreach ($file in $inputFiles) {
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $inputReportPath $file.Name) -Force
    $inputSnapshot += [PSCustomObject]@{
        File = $file.Name
        Path = $file.FullName
        SHA256 = $hash
    }
    Write-Host "Input snapshot: $($file.Name) SHA256=$hash"
}

$inputSnapshot | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $inputReportPath 'manifest.json') -Encoding UTF8

Write-Host "Running insertion verifier: `"$binInspectExe`" /a /v /3p /o `"$nativeReportPath`" `"$VerificationPath`""
& $binInspectExe /a /v /3p /o $nativeReportPath $VerificationPath
$verifierExitCode = $LASTEXITCODE

foreach ($snapshot in $inputSnapshot) {
    $currentHash = (Get-FileHash -LiteralPath (Join-Path $VerificationPath $snapshot.File) -Algorithm SHA256).Hash
    if ($currentHash -ne $snapshot.SHA256) {
        throw "Input file '$($snapshot.File)' was mutated during verification."
    }
}

$resultsPath = $null
foreach ($candidate in @('FullSignResults.xml', 'SignResults.xml')) {
    $candidatePath = Join-Path $nativeReportPath $candidate
    if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
        $resultsPath = $candidatePath
        break
    }
}
if ($null -eq $resultsPath) {
    throw "BinInspect produced no FullSignResults.xml or SignResults.xml in '$nativeReportPath' (exit code $verifierExitCode)."
}

[xml]$results = Get-Content -LiteralPath $resultsPath -Raw
$rows = @($results.SelectNodes('/DATA/ROW'))
if ($rows.Count -eq 0) {
    throw "No results rows were found in '$resultsPath'."
}

function Get-RowLeafName {
    param([System.Xml.XmlElement]$Row)
    return [System.IO.Path]::GetFileName([string]$Row.File)
}

$certificateRows = @($rows | Where-Object { $_.Type -ieq 'Certificate' })
if ($certificateRows.Count -ne 5) {
    throw "Expected exactly five certificate rows for four fonts and their catalog."
}
foreach ($row in $certificateRows) {
    $path = [IO.Path]::GetFullPath([string]$row.File)
    if (@($inputSnapshot | Where-Object { [IO.Path]::GetFullPath($_.Path) -ieq $path }).Count -ne 1) {
        throw "Unexpected certificate result path '$($row.File)'."
    }
}
$fontRows = @($certificateRows | Where-Object { [System.IO.Path]::GetExtension((Get-RowLeafName $_)) -ieq '.ttf' })
$catRows = @($certificateRows | Where-Object { (Get-RowLeafName $_) -ieq $catFiles[0].Name })

$fontNamesFromResults = @($fontRows | ForEach-Object { Get-RowLeafName $_ } | Sort-Object)
$fontResultDiff = Compare-Object -ReferenceObject $expectedFontNamesSorted -DifferenceObject $fontNamesFromResults
if ($null -ne $fontResultDiff) {
    throw "Expected four certificate rows for the template fonts in '$resultsPath', found: $(@($fontNamesFromResults) -join ', ')."
}
if ($fontRows.Count -ne 4) {
    throw "Expected exactly four certificate rows for template fonts in '$resultsPath', found $($fontRows.Count)."
}
if (@($fontRows | Group-Object { Get-RowLeafName $_ } | Where-Object { $_.Count -ne 1 }).Count -gt 0) {
    throw "Expected exactly one certificate row for each template font in '$resultsPath'."
}

if ($catRows.Count -ne 1) {
    throw "Expected exactly one certificate row for the catalog '$($catFiles[0].Name)' in '$resultsPath'."
}
$catNamesFromResults = @($catRows | ForEach-Object { Get-RowLeafName $_ } | Sort-Object -Unique)
$catResultDiff = Compare-Object -ReferenceObject @($catFiles[0].Name) -DifferenceObject $catNamesFromResults
if ($null -ne $catResultDiff) {
    throw "Expected certificate rows only for catalog '$($catFiles[0].Name)' in '$resultsPath', found: $(@($catNamesFromResults) -join ', ')."
}

$failures = @()
foreach ($row in $fontRows) {
    if ($row.Pass -ne 'True' -or $row.MSSigned -ne 'True') {
        $failures += "Font '$((Get-RowLeafName $row))' failed: $($row.Err)"
    }
}
foreach ($row in $catRows) {
    if ($row.Pass -ne 'True' -or $row.MSSigned -ne 'True') {
        $failures += "Catalog '$((Get-RowLeafName $row))' failed: $($row.Err)"
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host $failure
    }
    throw "Insertion verification reported signature failures. See '$resultsPath'."
}

if ($verifierExitCode -ne 0) {
    throw "Insertion verifier exited with code $verifierExitCode. See '$resultsPath'."
}

Write-Host "Insertion verification passed for four template fonts and the signed catalog."
