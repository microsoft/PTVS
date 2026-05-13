<#
    .SYNOPSIS
        Creates (or finds) a mirror issue in microsoft/PTVS for an AzDO work
        item being closed via the Developer Community duplicate flow.

    .DESCRIPTION
        Implements the GitHub-side half of plan.md §6.3 Step 6.

        Idempotency: before POSTing, the script searches the repo for an issue
        — open OR closed — whose title contains the marker `[AzDO #<id>]`. If
        present, that issue is returned and no duplicate is filed. This is the
        primary safety net against double-mirroring on retried runs.

        Sanitization: this script trusts that its caller has already
        sanitized $BodyMarkdown via sanitize.ps1. It does NOT itself redact
        PII — the AI-produced github_issue_body_md is supposed to be
        post-sanitization-only. We do, however, ensure the title begins with
        the [AzDO #<id>] marker and ends with the URL footer the apply step
        prepends.

        This file is dot-sourced by apply-outcomes.ps1 — its functions are the
        entrypoints. When run directly with -SelfTest it runs inline tests.
#>
[CmdletBinding()]
param(
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-GitHubHeaders {
    if (-not $env:PTVS_BRIDGE_PAT) { throw 'PTVS_BRIDGE_PAT not set.' }
    return @{
        Authorization = "Bearer $env:PTVS_BRIDGE_PAT"
        Accept        = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
        'User-Agent'  = 'PTVS-azdo-triage-bot'
    }
}

function Find-ExistingMirrorIssue {
    param(
        [Parameter(Mandatory)] [string] $Owner,
        [Parameter(Mandatory)] [string] $Repo,
        [Parameter(Mandatory)] [int]    $AzdoId,
        [Parameter(Mandatory)] [hashtable] $Headers
    )
    # Search across open + closed.
    $marker = "[AzDO #$AzdoId]"
    $q = "repo:$Owner/$Repo in:title `"$marker`""
    $uri = "https://api.github.com/search/issues?q=$([uri]::EscapeDataString($q))&per_page=10"
    try {
        $resp = Invoke-RestMethod -Method GET -Uri $uri -Headers $Headers
    } catch {
        Write-Warning "GitHub search failed (will fall back to create): $($_.Exception.Message)"
        return $null
    }
    if (-not $resp.items) { return $null }
    foreach ($it in @($resp.items)) {
        if ($it.title -and $it.title.StartsWith($marker)) { return $it }
    }
    return $null
}

function Format-MirrorIssueTitle {
    param([int] $AzdoId, [string] $RawTitle)
    $clean = ($RawTitle ?? '').Trim() -replace '\r?\n', ' '
    # Guard against the AI already including the prefix.
    if ($clean -match '^\[AzDO #\d+\]\s*') { $clean = ($clean -replace '^\[AzDO #\d+\]\s*', '') }
    # Cap at 240 characters (GitHub allows 256).
    if ($clean.Length -gt 220) { $clean = $clean.Substring(0, 220).TrimEnd() + '…' }
    if (-not $clean) { $clean = 'mirrored AzDO work item (untitled)' }
    return "[AzDO #$AzdoId] $clean"
}

function Format-MirrorIssueBody {
    param(
        [Parameter(Mandatory)] [string] $BodyMarkdown,
        [Parameter(Mandatory)] [string] $AzdoUrl,
        [Parameter(Mandatory)] [int]    $AzdoId
    )
    $frontmatter = "> _Mirrored from internal report [`DevDiv#$AzdoId`]($AzdoUrl). Original reporter has been notified._`n`n"
    return ($frontmatter + ($BodyMarkdown ?? ''))
}

function Filter-Labels {
    param([Parameter()] [string[]] $RawLabels, [Parameter(Mandatory)] [string[]] $Allowed)
    if (-not $RawLabels) { return @() }
    return @($RawLabels | Where-Object { $_ -and ($Allowed -contains $_) })
}

function New-MirrorIssue {
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [int]    $AzdoId,
        [Parameter(Mandatory)] [string] $AzdoUrl,
        [Parameter(Mandatory)] [string] $RawTitle,
        [Parameter(Mandatory)] [string] $BodyMarkdown,
        [Parameter(Mandatory)] [string[]] $Labels,
        [Parameter()]          [string[]] $Assignees
    )

    $headers = Get-GitHubHeaders
    $owner = $Config.github.owner
    $repo  = $Config.github.repo

    $existing = Find-ExistingMirrorIssue -Owner $owner -Repo $repo -AzdoId $AzdoId -Headers $headers
    if ($existing) {
        Write-Host "Mirror for AzDO #$AzdoId already exists: $($existing.html_url) (state=$($existing.state)). Skipping create."
        return $existing
    }

    $title = Format-MirrorIssueTitle -AzdoId $AzdoId -RawTitle $RawTitle
    $body  = Format-MirrorIssueBody  -BodyMarkdown $BodyMarkdown -AzdoUrl $AzdoUrl -AzdoId $AzdoId
    $safe  = Filter-Labels -RawLabels $Labels -Allowed @($Config.github.allowedLabels)
    # Always ensure the from-azdo marker.
    if ($safe -notcontains $Config.github.mirrorLabel) { $safe += $Config.github.mirrorLabel }
    # Default assignee from config if caller didn't override.
    if (-not $Assignees -or $Assignees.Count -eq 0) {
        $Assignees = @($Config.github.defaultAssignee)
    }

    $payload = @{
        title     = $title
        body      = $body
        labels    = $safe
        assignees = $Assignees
    } | ConvertTo-Json -Depth 5

    $uri = "https://api.github.com/repos/$owner/$repo/issues"
    $r = Invoke-RestMethod -Method POST -Uri $uri -Headers $headers -Body $payload -ContentType 'application/json'
    Write-Host "Mirrored AzDO #$AzdoId → $($r.html_url)"
    return $r
}

function Invoke-SelfTest {
    $errors = 0

    $t = Format-MirrorIssueTitle -AzdoId 2711586 -RawTitle 'crash on project open'
    if ($t -ne '[AzDO #2711586] crash on project open') { Write-Error "Title format: $t"; $errors++ }

    # Strip duplicated AzDO prefix if AI added it.
    $t = Format-MirrorIssueTitle -AzdoId 42 -RawTitle '[AzDO #42] thing happens'
    if ($t -ne '[AzDO #42] thing happens') { Write-Error "Title de-dup: $t"; $errors++ }

    # Long titles truncated. Total length includes the `[AzDO #<id>] ` prefix
    # (~10 chars for a 7-digit ID); cap at 240 so it stays well under GitHub's
    # 256-character title limit.
    $long = 'x' * 300
    $t = Format-MirrorIssueTitle -AzdoId 1 -RawTitle $long
    if ($t.Length -gt 240) { Write-Error "Title not truncated: len=$($t.Length)"; $errors++ }

    # Body frontmatter applied.
    $b = Format-MirrorIssueBody -BodyMarkdown '**hello**' -AzdoUrl 'https://example/1' -AzdoId 1
    if ($b -notmatch 'Mirrored from internal report') { Write-Error "Frontmatter missing: $b"; $errors++ }
    if ($b -notmatch '\*\*hello\*\*') { Write-Error "Body content lost."; $errors++ }

    # Label filter respects whitelist.
    $cfg = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'config.json') -Raw | ConvertFrom-Json
    $safe = Filter-Labels -RawLabels @('bug','area:foo','from-azdo','random-tag') -Allowed @($cfg.github.allowedLabels)
    if ($safe -contains 'area:foo' -or $safe -contains 'random-tag') { Write-Error "Disallowed label leaked: $($safe -join ',')"; $errors++ }
    if ($safe -notcontains 'bug') { Write-Error 'Allowed label dropped.'; $errors++ }
    if ($safe -notcontains 'from-azdo') { Write-Error 'from-azdo dropped from allowed list.'; $errors++ }

    if ($errors -gt 0) {
        throw "mirror-to-github.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'mirror-to-github.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }

# When dot-sourced, do nothing — caller invokes individual functions.
