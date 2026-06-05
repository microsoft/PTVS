<#
    .SYNOPSIS
        Builds the per-run research context the AI sees on every candidate.

    .DESCRIPTION
        Step 2 of the pipeline: builds the per-run research context (commit log,
        release tags, fixed-in-next-version map). Output is a single JSON file
        with:
          - recent_commits:        last ~90 days of PTVS commit messages
                                   (filtered to product paths).
          - recent_releases:       last N release tags + their dates.
          - fixed_in_next_version: open PTVS issues with that label.
          - upstream_tracker_urls: from config.json.

        Runs on the GitHub side (no AzDO calls — area-path history was removed
        when the workflow was simplified to read+draft only).

        Auth needed:
          - $env:GITHUB_TOKEN — for reading PTVS labels/releases. In GitHub
            Actions this is auto-injected.

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
        $logArgs = @(
            'log',
            "--since=$since",
            '--pretty=format:%h%x09%ai%x09%s',
            '--no-merges',
            '--',
            'Python/', 'Common/'
        )
        $out = & git @logArgs 2>$null
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
    $token = $env:GITHUB_TOKEN
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

function Invoke-Fetch {
    param([string] $OutFile, [string] $RepoRoot, [object] $Config)

    $context = [pscustomobject] @{
        generated_at          = (Get-Date).ToUniversalTime().ToString('o')
        recent_commits        = @(Get-RecentCommits      -RepoRoot $RepoRoot -Cap ([int] $Config.limits.recentCommitsCap))
        recent_releases       = @(Get-RecentReleases     -RepoRoot $RepoRoot -Cap ([int] $Config.limits.releaseTagsCap))
        fixed_in_next_version = @(Get-FixedInNextVersion -Config   $Config)
        upstream_tracker_urls = @($Config.upstream_tracker_urls)
    }
    ($context | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $OutFile -Encoding UTF8
    Write-Host "Wrote run context -> $OutFile"
    Write-Host ("  commits={0}  releases={1}  fixed_in_next_version={2}" -f `
        $context.recent_commits.Count, $context.recent_releases.Count, `
        $context.fixed_in_next_version.Count)
}

function Invoke-SelfTest {
    $errors = 0

    # Headers builder returns null without GITHUB_TOKEN.
    $oldTok = $env:GITHUB_TOKEN
    $env:GITHUB_TOKEN = $null
    $h = Get-GitHubHeaders
    if ($null -ne $h) { Write-Error 'Headers expected null when no token present.'; $errors++ }
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
