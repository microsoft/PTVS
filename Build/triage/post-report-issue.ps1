<#
    .SYNOPSIS
        Job 1 writer. Creates (or updates) the weekly summary issue in
        microsoft/PTVS that lists every open AzDO work item created in the
        lookback window.

    .DESCRIPTION
        Job 1 of the weekly triage workflow. Always runs — independent of the
        triage pipeline gate.

        Idempotency: the script searches for an OPEN issue whose title
        exactly matches the computed weekly title. If found, the body is
        updated in place rather than a duplicate filed. If only CLOSED
        issues match (the operator already closed the previous report
        to mean "done triaging this list"), a new issue is filed so the
        latest list surfaces in the operator's feed instead of silently
        zombie-updating the closed one.

        Empty candidate list: nothing is filed; the script exits 0 with a
        log line.

        Auth: $env:GITHUB_TOKEN. In GitHub Actions this is the auto-injected,
        repo-scoped, job-lifetime token (workflow declares `issues: write`).
        For local runs, set $env:GITHUB_TOKEN to a fine-grained PAT with
        `issues:write` + `metadata:read` on microsoft/PTVS.

    .PARAMETER CandidatesFile
        Path to the titles-only sanitized JSON (built by the pipeline's
        "Drop sanitization-aborted items..." step).

    .PARAMETER LookbackDays
        Window size, used for title computation.

    .PARAMETER RunUrl
        URL of the current pipeline run, embedded in the body footer for audit.

    .PARAMETER ConfigPath
        Path to Build/triage/config.json.

    .PARAMETER DryRun
        If set, prints the title/body to stdout and does not call GitHub.

    .PARAMETER SelfTest
        Run inline smoke tests.
#>
[CmdletBinding()]
param(
    [string] $CandidatesFile,
    [int]    $LookbackDays = 7,
    [string] $RunUrl,
    [string] $ConfigPath = (Join-Path $PSScriptRoot 'config.json'),
    [switch] $DryRun,
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-TriageConfig {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw "Config file not found: $Path" }
    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}

function Get-GitHubHeaders {
    if (-not $env:GITHUB_TOKEN) {
        throw 'GITHUB_TOKEN environment variable is required.'
    }
    return @{
        Authorization = "Bearer $env:GITHUB_TOKEN"
        Accept        = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
        'User-Agent'  = 'PTVS-azdo-triage-bot'
    }
}

function Format-AreaSubpath {
    param(
        [string] $Full,
        [Parameter(Mandatory)] [string] $Root
    )
    # Strip the configured root area path prefix for compactness in the report.
    # Ensure the root has the trailing separator so we don't accidentally strip
    # only a partial segment (e.g. "DevDiv\Python and AI Toolset" would not
    # match the root "DevDiv\Python and AI Tools\").
    if (-not $Root.EndsWith('\')) { $Root = "$Root\" }
    if ($Full -and $Full.StartsWith($Root)) { return $Full.Substring($Root.Length) }
    return ($Full ?? '')
}

function Format-CreatedDate {
    param($Iso)
    if (-not $Iso) { return '' }
    try {
        $dt = [DateTime]::Parse($Iso, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AdjustToUniversal -bor [Globalization.DateTimeStyles]::AssumeUniversal)
        return $dt.ToString('yyyy-MM-dd')
    } catch {
        return $Iso.ToString()
    }
}

function Build-IssueTitle {
    param(
        [Parameter(Mandatory)] [DateTime] $WindowEnd,
        [Parameter(Mandatory)] [int]      $LookbackDays,
        [Parameter(Mandatory)] [int]      $Count
    )
    $start = $WindowEnd.AddDays(-$LookbackDays).ToString('yyyy-MM-dd')
    $end   = $WindowEnd.ToString('yyyy-MM-dd')
    return "AzDO triage report $start to $end ($Count open items)"
}

function Build-IssueBody {
    param(
        [Parameter()] [AllowEmptyCollection()] [object[]] $Candidates,
        [Parameter()]          [string]   $RunUrl,
        [Parameter(Mandatory)] [int]      $LookbackDays,
        [Parameter(Mandatory)] [string]   $AreaRoot
    )
    if ($null -eq $Candidates) { $Candidates = @() }
    $sb = New-Object System.Text.StringBuilder
    $count = $Candidates.Count
    # Display form of the area root: ensure trailing \\** so the reader can see
    # we're recursing under it, regardless of whether config stored the trailing
    # backslash.
    $displayRoot = $AreaRoot.TrimEnd('\')
    if ($count -eq 0) {
        $null = $sb.AppendLine("No open AzDO work items had activity in the last $LookbackDays day(s) under ``$displayRoot\**``.")
    } else {
        $null = $sb.AppendLine("$count open work item(s) with activity in the past $LookbackDays day(s) under ``$displayRoot\**``.")
        $null = $sb.AppendLine()
        foreach ($c in $Candidates) {
            $subpath = Format-AreaSubpath -Full $c.area_path -Root $AreaRoot
            $date    = Format-CreatedDate -Iso $c.created_date
            $title   = $c.title
            # Collapse newlines so the bullet renders on one line.
            $title   = $title -replace '\r?\n', ' '
            $null = $sb.AppendLine("- [AzDO #$($c.id)]($($c.url)) — ``$title`` — created $date — area: $subpath")
        }
    }
    $null = $sb.AppendLine()
    $null = $sb.AppendLine('---')
    $runLine = if ($RunUrl) { "[run details]($RunUrl)" } else { 'run details unavailable' }
    $null = $sb.AppendLine("_Generated by AzDO Pipeline ``PTVS-Triage-Fetch``. $runLine._")
    return $sb.ToString()
}

function Find-ExistingReportIssue {
    param(
        [Parameter(Mandatory)] [string] $Owner,
        [Parameter(Mandatory)] [string] $Repo,
        [Parameter(Mandatory)] [string] $Title,
        [Parameter(Mandatory)] [string] $Label,
        [Parameter(Mandatory)] [hashtable] $Headers
    )
    # Use Search API: scope to repo + label + exact title match + OPEN only.
    # GitHub search needs the title wrapped in quotes for phrase match.
    #
    # `is:open` is load-bearing: without it, the Search API also returns
    # closed issues with the same title, and we'd silently zombie-update
    # an issue the operator already closed (which they typically close
    # to mean "done triaging this list"). Scoping to open issues means:
    #   - same-week re-runs with matching title → update the live issue
    #     in place (idempotent, the original goal).
    #   - re-runs after the operator closed the previous report → fall
    #     through to New-ReportIssue and file a fresh issue so the new
    #     list surfaces in the operator's feed.
    # The title already varies with `$candidates.Count`, so any week
    # where the count changes between runs creates a new issue
    # regardless.
    $q = "repo:$Owner/$Repo label:`"$Label`" in:title `"$Title`" is:open"
    $uri = "https://api.github.com/search/issues?q=$([uri]::EscapeDataString($q))"
    $resp = Invoke-RestMethod -Method GET -Uri $uri -Headers $Headers
    if (-not $resp.items) { return $null }
    foreach ($item in $resp.items) {
        # Belt-and-suspenders: filter again in code in case the API
        # contract changes. The Search API also surfaces pull requests
        # under `/search/issues`; explicitly require an issue (no
        # `pull_request` field) so a PR titled identically can't match.
        # StrictMode-safe property reads: `.PSObject.Properties[name]`
        # returns $null when absent instead of throwing.
        $isPullRequest = ($item.PSObject.Properties['pull_request'] -and $item.pull_request)
        if ($item.title -eq $Title -and $item.state -eq 'open' -and -not $isPullRequest) {
            return $item
        }
    }
    return $null
}

function New-ReportIssue {
    param(
        [string] $Owner, [string] $Repo, [string] $Title, [string] $Body,
        [string[]] $Labels, [string[]] $Assignees,
        [hashtable] $Headers
    )
    $uri = "https://api.github.com/repos/$Owner/$Repo/issues"
    $payload = @{
        title     = $Title
        body      = $Body
        labels    = @($Labels)
        assignees = @($Assignees)
    } | ConvertTo-Json -Depth 5
    return Invoke-RestMethod -Method POST -Uri $uri -Headers $Headers -Body $payload -ContentType 'application/json'
}

function Update-ReportIssue {
    param(
        [string] $Owner, [string] $Repo, [int] $Number, [string] $Body,
        [hashtable] $Headers
    )
    $uri = "https://api.github.com/repos/$Owner/$Repo/issues/$Number"
    $payload = @{ body = $Body } | ConvertTo-Json -Depth 3
    return Invoke-RestMethod -Method PATCH -Uri $uri -Headers $Headers -Body $payload -ContentType 'application/json'
}

function Invoke-Post {
    param(
        [string] $CandidatesFile,
        [int]    $LookbackDays,
        [string] $RunUrl,
        [object] $Config,
        [switch] $DryRun
    )

    if (-not (Test-Path -LiteralPath $CandidatesFile)) {
        throw "Candidates file not found: $CandidatesFile"
    }
    $raw = Get-Content -LiteralPath $CandidatesFile -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) {
        $candidates = @()
    } else {
        $parsed = $raw | ConvertFrom-Json
        # Use an explicit if/elseif/else statement (NOT the if-expression
        # form `$x = if (...) { @() }`). PowerShell unwraps single-statement
        # if-expressions when the branch yields an empty array — `$x` ends
        # up `$null` rather than `@()`, then `$candidates.Count` throws
        # under StrictMode. The if-statement form below assigns directly
        # in each branch, which preserves the array shape.
        if ($null -eq $parsed) {
            $candidates = @()
        } elseif ($parsed -is [System.Array]) {
            $candidates = @($parsed)
        } else {
            $candidates = @($parsed)
        }
    }

    $windowEnd = [DateTime]::UtcNow.Date
    $title = Build-IssueTitle -WindowEnd $windowEnd -LookbackDays $LookbackDays -Count $candidates.Count
    $body  = Build-IssueBody  -Candidates $candidates -RunUrl $RunUrl -LookbackDays $LookbackDays -AreaRoot $Config.azdo.areaPath

    # Defensive: body is small (just bullets + run link), but a future
    # regression could break this. GH issue body cap is 65536 chars.
    if ($body.Length -gt 65000) {
        throw "Issue body is $($body.Length) chars; GitHub's hard limit is 65536. Title list may be too long; reduce maxCandidatesPerRun in config.json."
    }

    if ($candidates.Count -eq 0) {
        Write-Host 'No candidates in the lookback window. Skipping issue creation.'
        return
    }

    if ($DryRun) {
        Write-Host "DRY RUN: would create issue with title:`n  $title"
        Write-Host "DRY RUN body:`n$body"
        return
    }

    $headers = Get-GitHubHeaders
    $existing = Find-ExistingReportIssue `
        -Owner $Config.github.owner `
        -Repo  $Config.github.repo `
        -Title $title `
        -Label $Config.github.reportLabel `
        -Headers $headers

    if ($existing) {
        Write-Host "Updating existing report issue #$($existing.number) at $($existing.html_url)."
        $r = Update-ReportIssue `
            -Owner $Config.github.owner `
            -Repo  $Config.github.repo `
            -Number $existing.number `
            -Body $body `
            -Headers $headers
        Write-Host "Updated $($r.html_url)."
    } else {
        Write-Host 'Creating new weekly report issue.'
        $r = New-ReportIssue `
            -Owner $Config.github.owner `
            -Repo  $Config.github.repo `
            -Title $title `
            -Body  $body `
            -Labels @($Config.github.reportLabel) `
            -Assignees @($Config.github.defaultAssignee) `
            -Headers $headers
        Write-Host "Created $($r.html_url)."
    }
}

# ──────────────────────────────────────────────────────────────────────
function Invoke-SelfTest {
    $errors = 0

    # Title format.
    $end = [DateTime]::Parse('2026-05-13T00:00:00Z', [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal).ToUniversalTime().Date
    $t = Build-IssueTitle -WindowEnd $end -LookbackDays 7 -Count 8
    if ($t -ne 'AzDO triage report 2026-05-06 to 2026-05-13 (8 open items)') {
        Write-Error "Title format mismatch: $t"; $errors++
    }

    # Empty body.
    $b = Build-IssueBody -Candidates @() -RunUrl 'https://example/run/1' -LookbackDays 7 -AreaRoot 'DevDiv\Python and AI Tools'
    if ($b -notmatch 'No open AzDO work items') { Write-Error "Empty-body branch missing: $b"; $errors++ }
    if ($b -notmatch [regex]::Escape('DevDiv\Python and AI Tools\**')) { Write-Error "Empty-body area string missing: $b"; $errors++ }

    # Non-empty body with one candidate.
    $cands = @(
        [pscustomobject] @{
            id           = 2711586
            title        = 'crash on project open'
            url          = 'https://dev.azure.com/devdiv/DevDiv/_workitems/edit/2711586'
            area_path    = 'DevDiv\Python and AI Tools\Python\VS IDE'
            created_date = '2026-05-08T12:00:00Z'
            state        = 'Active'
            work_item_type = 'Bug'
        }
    )
    $b = Build-IssueBody -Candidates $cands -RunUrl 'https://example/run/2' -LookbackDays 7 -AreaRoot 'DevDiv\Python and AI Tools\'
    if ($b -notmatch '\[AzDO #2711586\]') { Write-Error "Bullet link missing in body."; $errors++ }
    if ($b -notmatch 'area: Python\\VS IDE') { Write-Error "Subpath not formatted: $b"; $errors++ }
    if ($b -notmatch 'created 2026-05-08') { Write-Error "Date not formatted: $b"; $errors++ }
    if ($b -match 'Phase 2') { Write-Error "Body should not mention Phase 2 anymore: $b"; $errors++ }
    if ($b -match 'ptvs-triage-release') { Write-Error "Body should not contain release-tag marker: $b"; $errors++ }
    # Body should be small — no inline data.
    if ($b.Length -gt 2000) { Write-Error "Body unexpectedly large ($($b.Length) chars) — should be small"; $errors++ }

    # End-to-end: Invoke-Post against an empty `[]` candidates file MUST NOT
    # crash with "The property 'Count' cannot be found on this object".
    # Regression for the case the FeedbackTicket-only filter hits constantly:
    # a typical week has 0 open DC items in the lookback window.
    $tmpRoot = Join-Path ([IO.Path]::GetTempPath()) ("post-empty-{0}" -f ([Guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null
    try {
        $emptyFile = Join-Path $tmpRoot 'cands.json'
        '[]' | Set-Content -LiteralPath $emptyFile -Encoding UTF8
        $cfgPath = Join-Path $PSScriptRoot 'config.json'
        $cfg = Get-TriageConfig -Path $cfgPath
        try {
            # -DryRun avoids the GitHub call. If Invoke-Post crashes on
            # $candidates.Count, this throws and gets caught below.
            Invoke-Post -CandidatesFile $emptyFile -LookbackDays 7 -RunUrl 'https://example/run/x' -Config $cfg -DryRun
        } catch {
            Write-Error "Invoke-Post crashed on empty `[]` input: $($_.Exception.Message)"; $errors++
        }
    } finally {
        if (Test-Path -LiteralPath $tmpRoot) { Remove-Item -LiteralPath $tmpRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }

    # Find-ExistingReportIssue: the Search query MUST include `is:open`
    # and MUST filter the matched items to open-only, so a closed
    # previous report doesn't get zombie-updated. Regression repro:
    # earlier this week the operator closed report #N within a minute
    # of it being filed; the next pipeline run silently updated that
    # closed issue's body, hiding the latest list from the operator's
    # feed.
    $script:__capturedUri = $null
    $script:__searchResp  = $null
    function script:Invoke-RestMethod {
        param([string] $Method, [string] $Uri, [hashtable] $Headers,
              [string] $Body, [string] $ContentType)
        $script:__capturedUri = $Uri
        return $script:__searchResp
    }
    try {
        # Pretend GitHub returned both a closed and an open match.
        $script:__searchResp = [pscustomobject] @{
            items = @(
                [pscustomobject] @{ number = 100; title = 'Match'; state = 'closed'; html_url = 'u-closed' },
                [pscustomobject] @{ number = 101; title = 'Match'; state = 'open';   html_url = 'u-open'   }
            )
        }
        $found = Find-ExistingReportIssue -Owner 'o' -Repo 'r' -Title 'Match' -Label 'L' -Headers @{}
        if ($script:__capturedUri -notmatch 'is%3Aopen') {
            Write-Error "Find-ExistingReportIssue: query did not include is:open. Uri=$($script:__capturedUri)"; $errors++
        }
        if (-not $found) {
            Write-Error 'Find-ExistingReportIssue: returned $null when an open match existed.'; $errors++
        } elseif ($found.number -ne 101) {
            Write-Error "Find-ExistingReportIssue: returned wrong item (#$($found.number)); expected the OPEN one (#101)."; $errors++
        }

        # Closed-only: must return $null so caller files a fresh issue.
        $script:__searchResp = [pscustomobject] @{
            items = @(
                [pscustomobject] @{ number = 200; title = 'Match'; state = 'closed'; html_url = 'u-closed-only' }
            )
        }
        $found2 = Find-ExistingReportIssue -Owner 'o' -Repo 'r' -Title 'Match' -Label 'L' -Headers @{}
        if ($found2) {
            Write-Error "Find-ExistingReportIssue: returned closed issue when no open match exists. Got #$($found2.number)."; $errors++
        }

        # PR collision: /search/issues also returns PRs; the `pull_request`
        # field filter must reject them so an identically-titled PR can't
        # be mistaken for the report issue.
        $script:__searchResp = [pscustomobject] @{
            items = @(
                [pscustomobject] @{ number = 300; title = 'Match'; state = 'open'; pull_request = [pscustomobject] @{ url = 'pr-url' }; html_url = 'pr' }
            )
        }
        $found3 = Find-ExistingReportIssue -Owner 'o' -Repo 'r' -Title 'Match' -Label 'L' -Headers @{}
        if ($found3) {
            Write-Error "Find-ExistingReportIssue: matched a PR (#$($found3.number)) instead of skipping it."; $errors++
        }

        # Empty Search response: returns $null cleanly.
        $script:__searchResp = [pscustomobject] @{ items = @() }
        $found4 = Find-ExistingReportIssue -Owner 'o' -Repo 'r' -Title 'Match' -Label 'L' -Headers @{}
        if ($found4) {
            Write-Error 'Find-ExistingReportIssue: returned non-null on empty Search response.'; $errors++
        }
    } finally {
        Remove-Item Function:script:Invoke-RestMethod -ErrorAction SilentlyContinue
        $script:__capturedUri = $null
        $script:__searchResp  = $null
    }

    if ($errors -gt 0) {
        throw "post-report-issue.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'post-report-issue.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }

if (-not $CandidatesFile) { throw '-CandidatesFile is required (omit only with -SelfTest).' }

$cfg = Get-TriageConfig -Path $ConfigPath
Invoke-Post -CandidatesFile $CandidatesFile -LookbackDays $LookbackDays -RunUrl $RunUrl -Config $cfg -DryRun:$DryRun
