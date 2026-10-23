param(
    [Parameter(Mandatory = $true)]
    [string] $ResultsPath,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $MinimumExpectedTests,

    [Parameter(Mandatory = $true)]
    [string] $TestRunName
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resultFiles = @(Get-ChildItem -Path $ResultsPath -Filter '*.trx' -File -Recurse -ErrorAction SilentlyContinue)
if ($resultFiles.Count -eq 0) {
    throw "$TestRunName did not produce any TRX results under '$ResultsPath'."
}

$counters = @{
    total = 0
    executed = 0
    passed = 0
    failed = 0
    error = 0
    timeout = 0
    aborted = 0
}
$counterNames = @('total', 'executed', 'passed', 'failed', 'error', 'timeout', 'aborted')

foreach ($resultFile in $resultFiles) {
    [xml] $trx = Get-Content -LiteralPath $resultFile.FullName -Raw
    $counterNode = $trx.SelectSingleNode(
        "/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']"
    )
    if (-not $counterNode) {
        throw "TRX result '$($resultFile.FullName)' does not contain ResultSummary/Counters."
    }

    foreach ($name in $counterNames) {
        $attribute = $counterNode.Attributes[$name]
        if ($attribute) {
            $counters[$name] += [int] $attribute.Value
        }
    }
}

Write-Host "$TestRunName result summary from $($resultFiles.Count) TRX file(s):"
foreach ($name in $counterNames | Sort-Object) {
    Write-Host "  ${name}: $($counters[$name])"
}

if ($counters.total -lt $MinimumExpectedTests -or $counters.executed -lt $MinimumExpectedTests) {
    throw "$TestRunName discovered $($counters.total) and executed $($counters.executed) tests; expected at least $MinimumExpectedTests."
}

$unsuccessful = $counters.failed + $counters.error + $counters.timeout + $counters.aborted
if ($unsuccessful -gt 0) {
    throw "$TestRunName contains $unsuccessful failed, errored, timed out, or aborted test result(s)."
}
