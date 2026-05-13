<#
    .SYNOPSIS
        Builds the per-run research context the AI sees on every candidate.

    .DESCRIPTION
        Implements Step 2 of the workflow (see plan.md §6.3 Step 2). Output is a
        single JSON file with:
          - recent_commits:  last ~90 days of PTVS commit messages
                             (filtered to product paths).
          - recent_releases: last N release tags + their dates.
          - fixed_in_next_version: open PTVS issues with that label.
          - area_path_history: last 90 days of AzDO work items under our area
                               path (titles + ids + state only).
          - upstream_tracker_urls: from config.json.

        Auth needed:
          - $env:AZDO_ACCESS_TOKEN — for AzDO area-path history.
          - $env:PTVS_TRIAGE_MCP_READONLY_PAT (or $env:GITHUB_TOKEN as fallback) —
            for reading PTVS labels/releases.

    .PARAMETER OutFile
        Path of the run-context JSON to write.

    .PARAMETER RepoRoot
        Working directory of the cloned repo (default: cwd).

    .PARAMETER ConfigPath
        Path to Build/triage/config.json.

    .PARAMETER SelfTest
        Run inline smoke tests.
#>
[CmdletBinding()]
param(
    [string] $OutFile,
    [string] $RepoRoot = (Get-Location).Path,
    [string] $ConfigPath = (Join-Path $PSScriptRoot 'config.json'),
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-TriageConfig {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw "Config file not found: $Path" }
    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}

function Get-RecentCommits {
    param([string] $RepoRoot, [int] $DaysBack = 90, [int] $Cap = 150)
    Push-Location $RepoRoot
    try {
        $since = (Get-Date).AddDays(-$DaysBack).ToString('yyyy-MM-dd')
        # Filter commits that touched product paths. We pass paths as `git log -- <paths>`.
        $args = @(
            'log',
            "--since=$since",
            '--pretty=format:%h%x09%ai%x09%s',
            '--no-merges',
            '--',
            'Python/', 'Common/'
        )
        $out = & git @args 2>$null
        if (-not $out) { return @() }
        $lines = @($out | Where-Object { $_ -and $_.Trim() })
        if ($lines.Count -gt $Cap) { $lines = $lines[0..($Cap - 1)] }
        $result = foreach ($l in $lines) {
            $parts = $l -split "`t", 3
            if ($parts.Count -ge 3) {
                [pscustomobject] @{
                    sha     = $parts[0]
                    date    = $parts[1]
                    subject = $parts[2]
                }
            }
        }
        return @($result)
    } finally {
        Pop-Location
    }
}

function Get-RecentReleases {
    param([string] $RepoRoot, [int] $Cap = 3)
    Push-Location $RepoRoot
    try {
        # Best-effort: list tags sorted by author date.
        $out = & git for-each-ref --sort=-creatordate --format='%(refname:short)%09%(creatordate:short)' 'refs/tags/' 2>$null
        if (-not $out) { return @() }
        $result = @()
        foreach ($line in @($out)) {
            if (-not $line) { continue }
            $parts = $line -split "`t", 2
            if ($parts.Count -eq 2) {
                $result += [pscustomobject] @{
                    tag = $parts[0]
                    date = $parts[1]
                }
            }
            if ($result.Count -ge $Cap) { break }
        }
        return $result
    } finally {
        Pop-Location
    }
}

function Get-GitHubHeaders {
    $token = $env:PTVS_TRIAGE_MCP_READONLY_PAT
    if (-not $token) { $token = $env:GITHUB_TOKEN }
    if (-not $token) {
        Write-Warning 'No GitHub token available; fixed_in_next_version will be empty.'
        return $null
    }
    return @{
        Authorization = "Bearer $token"
        Accept        = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
        'User-Agent'  = 'PTVS-azdo-triage-bot'
    }
}

function Get-FixedInNextVersion {
    param([object] $Config)
    $headers = Get-GitHubHeaders
    if (-not $headers) { return @() }
    $owner = $Config.github.owner
    $repo  = $Config.github.repo
    $label = 'fixed in next version'
    $uri = "https://api.github.com/repos/$owner/$repo/issues?state=open&per_page=100&labels=$([uri]::EscapeDataString($label))"
    try {
        $resp = Invoke-RestMethod -Method GET -Uri $uri -Headers $headers
    } catch {
        Write-Warning "Could not read fixed-in-next-version issues: $($_.Exception.Message)"
        return @()
    }
    return @($resp | Where-Object { -not $_.PSObject.Properties['pull_request'] } | ForEach-Object {
        [pscustomobject] @{
            number = $_.number
            title  = $_.title
            url    = $_.html_url
        }
    })
}

function Get-AzdoAuthHeader {
    if ($env:AZDO_ACCESS_TOKEN) { return @{ Authorization = "Bearer $env:AZDO_ACCESS_TOKEN" } }
    if ($env:AZDO_PAT) {
        $b64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$env:AZDO_PAT"))
        return @{ Authorization = "Basic $b64" }
    }
    return $null
}

function Get-AreaPathHistory {
    param([object] $Config)
    $headers = Get-AzdoAuthHeader
    if (-not $headers) {
        Write-Warning 'No AzDO token available; area_path_history will be empty.'
        return @()
    }

    $days = [int] $Config.limits.areaPathHistoryDays
    $cap  = [int] $Config.limits.areaPathHistoryCap
    $types = ($Config.azdo.workItemTypes | ForEach-Object { "'$_'" }) -join ', '
    $wiql = @"
SELECT [System.Id]
FROM   workitems
WHERE  [System.TeamProject] = '$($Config.azdo.project)'
  AND  [System.AreaPath] UNDER '$($Config.azdo.areaPath)'
  AND  [System.WorkItemType] IN ($types)
  AND  [System.CreatedDate] >= @Today - $days
ORDER BY [System.CreatedDate] DESC
"@
    $uri = "$($Config.azdo.baseUrl)/$($Config.azdo.project)/_apis/wit/wiql?api-version=7.1"
    try {
        $resp = Invoke-RestMethod -Method POST -Uri $uri -Headers $headers -Body (@{ query = $wiql } | ConvertTo-Json -Compress) -ContentType 'application/json'
    } catch {
        Write-Warning "AzDO history WIQL failed: $($_.Exception.Message)"
        return @()
    }

    if (-not $resp.workItems) { return @() }
    $ids = @($resp.workItems | ForEach-Object { [int] $_.id })
    if ($ids.Count -gt $cap) { $ids = $ids[0..($cap - 1)] }

    $batchUri = "$($Config.azdo.baseUrl)/_apis/wit/workitemsbatch?api-version=7.1"
    $result = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $ids.Count; $i += 200) {
        $slice = $ids[$i..([Math]::Min($i + 199, $ids.Count - 1))]
        $body = @{ ids = @($slice); fields = @('System.Id','System.Title','System.State','System.WorkItemType','System.CreatedDate') } | ConvertTo-Json -Depth 5
        try {
            $batch = Invoke-RestMethod -Method POST -Uri $batchUri -Headers $headers -Body $body -ContentType 'application/json'
        } catch {
            Write-Warning "AzDO history batch failed: $($_.Exception.Message)"
            continue
        }
        foreach ($wi in @($batch.value)) {
            $f = $wi.fields
            $result.Add([pscustomobject] @{
                id    = $wi.id
                title = $f.'System.Title'
                state = $f.'System.State'
                type  = $f.'System.WorkItemType'
                created_date = $f.'System.CreatedDate'
            }) | Out-Null
        }
    }
    return $result.ToArray()
}

function Invoke-Fetch {
    param([string] $OutFile, [string] $RepoRoot, [object] $Config)

    $context = [pscustomobject] @{
        generated_at          = (Get-Date).ToUniversalTime().ToString('o')
        recent_commits        = @(Get-RecentCommits        -RepoRoot $RepoRoot -Cap ([int] $Config.limits.recentCommitsCap))
        recent_releases       = @(Get-RecentReleases       -RepoRoot $RepoRoot -Cap ([int] $Config.limits.releaseTagsCap))
        fixed_in_next_version = @(Get-FixedInNextVersion   -Config   $Config)
        area_path_history     = @(Get-AreaPathHistory      -Config   $Config)
        upstream_tracker_urls = @($Config.upstream_tracker_urls)
    }
    ($context | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $OutFile -Encoding UTF8
    Write-Host "Wrote run context → $OutFile"
    Write-Host ("  commits={0}  releases={1}  fixed_in_next_version={2}  area_history={3}" -f `
        $context.recent_commits.Count, $context.recent_releases.Count, `
        $context.fixed_in_next_version.Count, $context.area_path_history.Count)
}

function Invoke-SelfTest {
    $errors = 0
    $cfg = Get-TriageConfig -Path $ConfigPath

    # Headers builder returns null without env vars.
    $oldPat = $env:PTVS_TRIAGE_MCP_READONLY_PAT
    $oldTok = $env:GITHUB_TOKEN
    $env:PTVS_TRIAGE_MCP_READONLY_PAT = $null
    $env:GITHUB_TOKEN = $null
    $h = Get-GitHubHeaders
    if ($null -ne $h) { Write-Error 'Headers expected null when no token present.'; $errors++ }
    $env:PTVS_TRIAGE_MCP_READONLY_PAT = $oldPat
    $env:GITHUB_TOKEN = $oldTok

    # git log smoke (only if we're in a git repo).
    if (Test-Path -LiteralPath (Join-Path $RepoRoot '.git')) {
        $commits = Get-RecentCommits -RepoRoot $RepoRoot -DaysBack 365 -Cap 5
        # Empty result is acceptable on a brand-new clone, so we don't fail.
        Write-Host "  self-test: git log returned $(@($commits).Count) commit(s)."
    }

    if ($errors -gt 0) {
        throw "fetch-context.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'fetch-context.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }
if (-not $OutFile) { throw '-OutFile is required (omit only with -SelfTest).' }

$cfg = Get-TriageConfig -Path $ConfigPath
Invoke-Fetch -OutFile $OutFile -RepoRoot $RepoRoot -Config $cfg
