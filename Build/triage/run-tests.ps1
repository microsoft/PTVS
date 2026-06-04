<#
    .SYNOPSIS
        Runs the -SelfTest path on every script under Build/triage/.

    .DESCRIPTION
        The repo does not use Pester. Each script accepts a -SelfTest switch
        that runs inline assertions against the fixtures under
        Build/triage/tests/fixtures/. This runner just invokes them all and
        fails if any one fails. Wired into the workflow via the triage-tests
        job (see .github/workflows/azdo-triage.yml).
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scripts = @(
    'query-azdo.ps1',
    'sanitize.ps1',
    'post-report-issue.ps1',
    'fetch-context.ps1',
    'parse-diagnostics.ps1',
    'cluster.ps1',
    'post-azdo.ps1',
    'mirror-to-github.ps1',
    'apply-outcomes.ps1'
)

$failed = New-Object System.Collections.Generic.List[string]

foreach ($s in $scripts) {
    $path = Join-Path $PSScriptRoot $s
    Write-Host ''
    Write-Host "=== $s -SelfTest ===" -ForegroundColor Cyan
    try {
        & $path -SelfTest
    } catch {
        Write-Host "FAILED: $s -- $($_.Exception.Message)" -ForegroundColor Red
        $failed.Add($s) | Out-Null
    }
}

Write-Host ''
if ($failed.Count -gt 0) {
    Write-Host "Self-tests FAILED for: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host 'All Build/triage self-tests passed.' -ForegroundColor Green
