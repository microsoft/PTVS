<#
    .SYNOPSIS
        Job 1 writer. Creates (or updates) the weekly summary issue in
        microsoft/PTVS that lists every open AzDO work item created in the
        lookback window.

    .DESCRIPTION
        Job 1 of the weekly triage workflow. Always runs — independent of the
        triage pipeline gate.

        Idempotency: the script searches for an existing issue whose title
        exactly matches the computed weekly title. If found, the body is
        updated in place rather than a duplicate filed.

        Empty candidate list: nothing is filed; the script exits 0 with a
        log line.

        Auth: $env:GITHUB_TOKEN. In GitHub Actions this is the auto-injected,
        repo-scoped, job-lifetime token (workflow declares `issues: write`).
        For local runs, set $env:GITHUB_TOKEN to a fine-grained PAT with
        `issues:write` + `metadata:read` on microsoft/PTVS.

    .PARAMETER CandidatesFile
        Path to the titles-only sanitized JSON (from sanitize.ps1 -TitlesOnly).

    .PARAMETER LookbackDays
        Window size, used for title computation.

    .PARAMETER RunUrl
        URL of the current pipeline run (AzDO or GH Actions), embedded in the
        body footer for audit.

    .PARAMETER BundleFile
        Optional. Path to a slim sanitized bundle JSON (work-item metadata +
        condensed diagnostics) that gets embedded as a hidden HTML comment in
        the issue body. Used by Phase 2's GH workflow: the workflow's
        `on: issues` trigger reads `github.event.issue.body`, extracts the
        bundle from the hidden marker, runs AI analysis per work item, and
        posts the analyses back as comments under the same issue.

        The bundle is base64-encoded (UTF-8 JSON → base64) and wrapped
        between sentinels:
          <!--ptvs-triage-bundle-v1
          <base64...>
          -->
        Base64 keeps user-controlled `-->` from prematurely closing the
        HTML comment. Phase 2 must base64-decode before parsing JSON.

    .PARAMETER PhaseTwoStatus
        Optional. Human-readable footer label describing whether Phase 2
        (AI analysis comments) is expected to run on this issue. Examples:
        'enabled', 'disabled (fetch only)', 'pending'.

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
    [string] $BundleFile,
    [string] $PhaseTwoStatus = 'enabled',
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
        [Parameter(Mandatory)] [string]   $AreaRoot,
        [Parameter()]          [string]   $PhaseTwoStatus = 'enabled',
        [Parameter()] [AllowNull()]       $Bundle = $null
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
        $null = $sb.AppendLine("AI analysis comments will appear below as Phase 2 processes each item.")
        $null = $sb.AppendLine()
        foreach ($c in $Candidates) {
            $subpath = Format-AreaSubpath -Full $c.area_path -Root $AreaRoot
            $date    = Format-CreatedDate -Iso $c.created_date
            $title   = $c.title
            # Escape pipe so it doesn't break our bullet visually in some GH renderers.
            $title   = $title -replace '\r?\n', ' '
            $null = $sb.AppendLine("- [AzDO #$($c.id)]($($c.url)) — ``$title`` — created $date — area: $subpath")
        }
    }
    $null = $sb.AppendLine()
    $null = $sb.AppendLine('---')
    $runLine = if ($RunUrl) { "[run details]($RunUrl)" } else { 'run details unavailable' }
    $null = $sb.AppendLine("_Generated by AzDO Pipeline ``PTVS-Triage-Fetch``. Phase 2 analysis: $PhaseTwoStatus. $runLine._")

    # Hidden, machine-readable bundle for Phase 2. Surrounded by stable
    # sentinels so a triggered GH workflow can locate + parse it
    # deterministically from `github.event.issue.body`. Bundle is base64-
    # encoded so user-controlled content (e.g., a title containing `-->`)
    # cannot break out of the HTML comment.
    if ($null -ne $Bundle) {
        $null = $sb.AppendLine()
        $null = $sb.AppendLine('<!--ptvs-triage-bundle-v1')
        $json = ($Bundle | ConvertTo-Json -Depth 20 -Compress)
        $b64  = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
        $null = $sb.AppendLine($b64)
        $null = $sb.AppendLine('-->')
    }
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
    # Use Search API: scope to repo + label + exact title match.
    # GitHub search needs the title wrapped in quotes for phrase match.
    $q = "repo:$Owner/$Repo label:`"$Label`" in:title `"$Title`""
    $uri = "https://api.github.com/search/issues?q=$([uri]::EscapeDataString($q))"
    $resp = Invoke-RestMethod -Method GET -Uri $uri -Headers $Headers
    if (-not $resp.items) { return $null }
    foreach ($item in $resp.items) {
        if ($item.title -eq $Title) { return $item }
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
        [string] $BundleFile,
        [string] $PhaseTwoStatus,
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
        $candidates = if ($null -eq $parsed) { @() } elseif ($parsed -is [System.Array]) { @($parsed) } else { @($parsed) }
    }

    # Load the slim bundle if supplied (for Phase 2 hidden-comment embedding).
    $bundle = $null
    if ($BundleFile) {
        if (-not (Test-Path -LiteralPath $BundleFile)) {
            throw "Bundle file not found: $BundleFile"
        }
        $bundleRaw = Get-Content -LiteralPath $BundleFile -Raw
        if (-not [string]::IsNullOrWhiteSpace($bundleRaw)) {
            $bundle = $bundleRaw | ConvertFrom-Json
            $bundleSize = ($bundleRaw.Length)
            Write-Host "Loaded bundle from $BundleFile ($bundleSize chars)."
            # Hard-cap the body at 60000 chars to leave headroom under GH's
            # 65536-char limit for the human-readable bullets + footer.
            if ($bundleSize -gt 55000) {
                throw "Bundle is too large to embed in issue body ($bundleSize chars > 55000). Reduce per-WI content in sanitize.ps1 before retrying."
            }
        }
    }

    $windowEnd = [DateTime]::UtcNow.Date
    $title = Build-IssueTitle -WindowEnd $windowEnd -LookbackDays $LookbackDays -Count $candidates.Count
    $body  = Build-IssueBody  -Candidates $candidates -RunUrl $RunUrl -LookbackDays $LookbackDays -AreaRoot $Config.azdo.areaPath -PhaseTwoStatus $PhaseTwoStatus -Bundle $bundle

    # Final defensive check — issue body limit is 65536 chars. Catches the case
    # where bullets + footer + base64 bundle together overflow even though the
    # raw bundle was under the per-file cap.
    if ($body.Length -gt 65000) {
        throw "Issue body is $($body.Length) chars; GitHub's hard limit is 65536. Reduce per-WI content (short_description cap, top_exceptions count) in the bundle assembly step before retrying."
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
    $b = Build-IssueBody -Candidates @() -RunUrl 'https://example/run/1' -LookbackDays 7 -AreaRoot 'DevDiv\Python and AI Tools' -PhaseTwoStatus 'enabled'
    if ($b -notmatch 'No open AzDO work items') { Write-Error "Empty-body branch missing: $b"; $errors++ }
    if ($b -notmatch 'Phase 2 analysis: enabled') { Write-Error "Empty-body footer missing: $b"; $errors++ }
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
    $b = Build-IssueBody -Candidates $cands -RunUrl 'https://example/run/2' -LookbackDays 7 -AreaRoot 'DevDiv\Python and AI Tools\' -PhaseTwoStatus 'pending'
    if ($b -notmatch '\[AzDO #2711586\]') { Write-Error "Bullet link missing in body."; $errors++ }
    if ($b -notmatch 'area: Python\\VS IDE') { Write-Error "Subpath not formatted: $b"; $errors++ }
    if ($b -notmatch 'created 2026-05-08') { Write-Error "Date not formatted: $b"; $errors++ }
    if ($b -notmatch 'Phase 2 analysis: pending') { Write-Error "Phase-two status line missing: $b"; $errors++ }
    if ($b -match 'ptvs-triage-bundle-v1') { Write-Error "Bundle marker should NOT appear when no bundle is passed: $b"; $errors++ }

    # Body with a slim bundle embedded (base64-encoded).
    $bundle = [pscustomobject] @{
        schema_version = '1'
        generated_at   = '2026-06-04T08:00:00Z'
        items          = @(
            [pscustomobject] @{
                id    = 2711586
                title = 'crash on project open'
                area  = 'Python\VS IDE'
                state = 'Active'
                short_description = 'VS crashes immediately after opening pyproj.'
            }
        )
    }
    $b = Build-IssueBody -Candidates $cands -RunUrl 'https://example/run/3' -LookbackDays 7 -AreaRoot 'DevDiv\Python and AI Tools\' -PhaseTwoStatus 'enabled' -Bundle $bundle
    if ($b -notmatch '<!--ptvs-triage-bundle-v1') { Write-Error "Bundle open sentinel missing"; $errors++ }
    if ($b -notmatch '-->') { Write-Error "Bundle close sentinel missing"; $errors++ }
    # Verify base64 round-trip. Extract the line between the sentinels and
    # decode it back to JSON.
    if ($b -match '(?ms)<!--ptvs-triage-bundle-v1\s*\r?\n(.+?)\r?\n-->') {
        $decoded = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Matches[1].Trim()))
        if ($decoded -notmatch '"id":2711586') { Write-Error "Decoded bundle JSON missing id field: $decoded"; $errors++ }
    } else {
        Write-Error "Bundle sentinel block not found or malformed in body"; $errors++
    }
    # Adversarial input: a title containing `-->` MUST NOT break the bundle
    # extraction. The `-->` in visible bullet text is harmless (rendered as
    # literal text); what matters is that Phase 2 can still extract the
    # bundle correctly using a non-greedy match.
    $candsAdv = @(
        [pscustomobject] @{
            id           = 4242
            title        = 'crash --> see attached log'
            url          = 'https://dev.azure.com/devdiv/DevDiv/_workitems/edit/4242'
            area_path    = 'DevDiv\Python and AI Tools\Python\VS IDE'
            created_date = '2026-06-01T12:00:00Z'
            state        = 'Active'
            work_item_type = 'Bug'
        }
    )
    $bundleAdv = [pscustomobject] @{ schema_version = '1'; items = @([pscustomobject] @{ id = 4242; title = '--> escape this' }) }
    $bAdv = Build-IssueBody -Candidates $candsAdv -RunUrl 'x' -LookbackDays 7 -AreaRoot 'DevDiv\Python and AI Tools\' -PhaseTwoStatus 'enabled' -Bundle $bundleAdv
    if ($bAdv -notmatch '(?ms)<!--ptvs-triage-bundle-v1\s*\r?\n([A-Za-z0-9+/=]+)\r?\n-->') {
        Write-Error "Adversarial: bundle marker not recoverable with non-greedy match: $bAdv"; $errors++
    } else {
        $advB64 = $Matches[1].Trim()
        $advDecoded = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($advB64))
        if ($advDecoded -notmatch '"id":4242') { Write-Error "Adversarial decoded bundle missing id: $advDecoded"; $errors++ }
        if ($advDecoded -notmatch '"--> escape this"') { Write-Error "Adversarial decoded bundle missing escaped title: $advDecoded"; $errors++ }
    }

    if ($errors -gt 0) {
        throw "post-report-issue.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'post-report-issue.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }

if (-not $CandidatesFile) { throw '-CandidatesFile is required (omit only with -SelfTest).' }

$cfg = Get-TriageConfig -Path $ConfigPath
Invoke-Post -CandidatesFile $CandidatesFile -LookbackDays $LookbackDays -RunUrl $RunUrl -BundleFile $BundleFile -PhaseTwoStatus $PhaseTwoStatus -Config $cfg -DryRun:$DryRun
