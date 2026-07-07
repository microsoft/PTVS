<#
    .SYNOPSIS
        Job 1 writer. Creates (or updates) the weekly summary issue in
        microsoft/PTVS that lists every open AzDO work item created in the
        lookback window.

    .DESCRIPTION
        Job 1 of the weekly triage workflow. Always runs — independent of the
        triage pipeline gate.

        Idempotency: the script lists open issues with the report label via
        the read-your-writes-consistent List Issues endpoint, then matches
        on exact title. If found, the body is updated in place rather than
        a duplicate filed. If only CLOSED issues match (the operator already
        closed the previous report to mean "done triaging this list"), a
        new issue is filed so the latest list surfaces in the operator's
        feed instead of silently zombie-updating the closed one.

        We deliberately avoid the GitHub Search API for the existence
        check — Search is eventually consistent (newly-created issues
        take 30 s to several minutes to index) and a same-week manual
        re-run would silently file a duplicate during that window.

        Empty candidate list: nothing is filed; the script exits 0 with a
        log line.

        Auth: $env:GITHUB_TOKEN. In the AzDO pipeline this comes from
        the `GH_PAT` secret variable in the `PTVS-Triage-Bridge` variable
        group — a fine-grained GitHub PAT scoped to microsoft/PTVS with
        Issues: Read+Write. For local runs, set $env:GITHUB_TOKEN to a
        similarly-scoped PAT.

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

function Format-MarkdownInlineCode {
    # Wrap text in Markdown inline-code spans, safely escaping any
    # backticks the text itself contains. CommonMark's rule: an inline-
    # code span is delimited by a run of N backticks (N >= 1); inside
    # the span any run of <N backticks is literal. So if the text
    # contains a single backtick, wrap with two; if it contains a
    # run of two, wrap with three; etc. We also pad with a leading
    # and trailing space when the text starts/ends with a backtick,
    # which CommonMark trims away — preventing GitHub from chopping
    # off our delimiters.
    # Without this, a customer-supplied title or pipeline name
    # containing a literal `…` terminates the inline-code span early
    # and the rest of the bullet renders as plain text.
    param([string] $Text)
    if ([string]::IsNullOrEmpty($Text)) { return '``' }
    # Find the longest run of consecutive backticks in $Text and pick
    # a delimiter one longer.
    $longest = 0
    foreach ($m in [regex]::Matches($Text, '`+')) {
        if ($m.Length -gt $longest) { $longest = $m.Length }
    }
    $delim = '`' * ($longest + 1)
    $pad = ''
    if ($Text.StartsWith('`') -or $Text.EndsWith('`')) { $pad = ' ' }
    return "$delim$pad$Text$pad$delim"
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
    $rootCode    = Format-MarkdownInlineCode -Text ("$displayRoot\**")
    if ($count -eq 0) {
        $null = $sb.AppendLine("No open AzDO work items had activity in the last $LookbackDays day(s) under $rootCode.")
    } else {
        $null = $sb.AppendLine("$count open work item(s) with activity in the past $LookbackDays day(s) under $rootCode.")
        $null = $sb.AppendLine()
        foreach ($c in $Candidates) {
            $subpath = Format-AreaSubpath -Full $c.area_path -Root $AreaRoot
            $date    = Format-CreatedDate -Iso $c.created_date
            $title   = $c.title
            # Collapse newlines so the bullet renders on one line.
            $title   = $title -replace '\r?\n', ' '
            $titleCode = Format-MarkdownInlineCode -Text $title
            $null = $sb.AppendLine("- [AzDO #$($c.id)]($($c.url)) — $titleCode — created $date — area: $subpath")
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
    # Use the LIST issues endpoint (`GET /repos/{owner}/{repo}/issues`),
    # NOT the Search API. The Search index is eventually consistent —
    # newly-created issues commonly take 30 s to several minutes to
    # become discoverable. With Search, a same-week manual re-run
    # kicked off shortly after a successful run (or a manual run that
    # races the future cron) would see "no match", fall through to
    # New-ReportIssue, and file a SECOND issue with the same title.
    # The list endpoint is read-your-writes consistent, so it always
    # sees the just-created issue.
    #
    # Filtering:
    #   - state=open: same rationale as before. A closed previous
    #     report means "operator is done with that list" — we want a
    #     fresh issue to surface in the feed rather than silently
    #     zombie-updating the closed one.
    #   - labels=<reportLabel>: server-side narrowing so we don't
    #     paginate every open issue in the repo.
    #   - per_page=100: max page size. With the report label scoped
    #     to weekly summary issues that the operator typically closes
    #     within days, the open set is small (≤ a handful); one page
    #     is plenty. Defensive belt-and-suspenders: if the API ever
    #     returns ≥100 open report issues, we'd need to paginate, but
    #     in practice that's a signal something else is broken (the
    #     operator stopped closing reports).
    #
    # Post-filter on:
    #   - exact-match title
    #   - state -eq 'open' (defensive)
    #   - NOT a pull request (this endpoint also returns PRs alongside
    #     issues; `pull_request` field presence distinguishes them)
    $label = [uri]::EscapeDataString($Label)
    $uri   = "https://api.github.com/repos/$Owner/$Repo/issues?state=open&labels=$label&per_page=100"
    $resp  = Invoke-RestMethod -Method GET -Uri $uri -Headers $Headers
    if (-not $resp) { return $null }
    # The list endpoint returns an array directly (not an `items`
    # wrapper like Search does). A single-item response can collapse
    # to a scalar under StrictMode — guard with @().
    foreach ($item in @($resp)) {
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

function Set-AzdoOutputVariable {
    # Emit `##vso[task.setvariable variable=NAME]VALUE` so subsequent steps
    # in the same job can read it as `variables['NAME']` in conditions or
    # `$(NAME)` in scripts. Same-job consumption doesn't need isOutput=true.
    # Outside AzDO this marker is just text in stdout.
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter()] [AllowEmptyString()] [string] $Value = ''
    )
    Write-Host "##vso[task.setvariable variable=$Name]$Value"
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
        Set-AzdoOutputVariable -Name 'issueNumber' -Value ''
        return
    }

    if ($DryRun) {
        Write-Host "DRY RUN: would create issue with title:`n  $title"
        Write-Host "DRY RUN body:`n$body"
        Set-AzdoOutputVariable -Name 'issueNumber' -Value ''
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
        Set-AzdoOutputVariable -Name 'issueNumber' -Value ([string] $existing.number)
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
        Set-AzdoOutputVariable -Name 'issueNumber' -Value ([string] $r.number)
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

    # Format-MarkdownInlineCode: backtick-safe wrapping.
    if ((Format-MarkdownInlineCode -Text 'plain text') -ne '`plain text`') {
        Write-Error 'Inline-code helper broke the simple no-backtick case.'; $errors++
    }
    # Single backtick in text → wrap with double backticks.
    $oneBt = Format-MarkdownInlineCode -Text 'has a ` backtick'
    if ($oneBt -ne '``has a ` backtick``') {
        Write-Error "Inline-code helper didn't widen the delimiter for a single backtick: '$oneBt'"; $errors++
    }
    # Run of two backticks → wrap with triple.
    $twoBt = Format-MarkdownInlineCode -Text 'has `` two'
    if ($twoBt -ne '```has `` two```') {
        Write-Error "Inline-code helper didn't widen the delimiter for a `` run: '$twoBt'"; $errors++
    }
    # Leading backtick → pad with space so CommonMark doesn't eat the delimiter.
    $leadBt = Format-MarkdownInlineCode -Text '`start'
    if ($leadBt -ne '`` `start ``') {
        Write-Error "Inline-code helper didn't pad a leading backtick: '$leadBt'"; $errors++
    }

    # Backtick-in-title regression: a customer-supplied AzDO title
    # containing a literal `` ` `` would terminate the bullet's inline-
    # code span early and leave the rest of the bullet as plain text
    # (date / area subpath). The fix widens the delimiter and
    # round-trips correctly on GitHub. Use single-quoted strings so
    # PowerShell doesn't eat the backtick as its escape character.
    $btCands = @(
        [pscustomobject] @{
            id           = 999000
            title        = 'crash in foo`bar with backtick'  # literal ` in title (single-quoted to escape)
            url          = 'https://dev.azure.com/devdiv/DevDiv/_workitems/edit/999000'
            area_path    = 'DevDiv\Python and AI Tools\Python'
            created_date = '2026-06-01T12:00:00Z'
            state        = 'Active'
            work_item_type = 'Bug'
        }
    )
    $btBody = Build-IssueBody -Candidates $btCands -RunUrl 'https://example/run/bt' -LookbackDays 7 -AreaRoot 'DevDiv\Python and AI Tools\'
    # The full bullet must survive — the trailing `— created` / `— area`
    # segments are how we detect early termination of the code span.
    if ($btBody -notmatch 'created 2026-06-01') {
        Write-Error 'Backtick title: created-date segment missing — code span terminated early.'; $errors++
    }
    if ($btBody -notmatch 'area: Python') {
        Write-Error 'Backtick title: area subpath segment missing — code span terminated early.'; $errors++
    }
    # The single-backtick title must be wrapped in DOUBLE backticks
    # (delimiter widened by one beyond the longest run of backticks in
    # the title).
    if ($btBody -notmatch '``crash in foo`bar with backtick``') {
        Write-Error "Backtick title: not wrapped in double backticks. Body: $btBody"; $errors++
    }

    # End-to-end: Invoke-Post against an empty `[]` candidates file MUST NOT
    # crash with "The property 'Count' cannot be found on this object".
    # Regression for the case the FeedbackTicket-only filter hits constantly:
    # a typical week has 0 open DC items in the lookback window.
    # Also asserts that the no-candidates path still emits an empty
    # issueNumber output variable — the Analyze block gates on this.
    $tmpRoot = Join-Path ([IO.Path]::GetTempPath()) ("post-empty-{0}" -f ([Guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null
    try {
        $emptyFile = Join-Path $tmpRoot 'cands.json'
        '[]' | Set-Content -LiteralPath $emptyFile -Encoding UTF8
        $cfgPath = Join-Path $PSScriptRoot 'config.json'
        $cfg = Get-TriageConfig -Path $cfgPath
        try {
            # Capture stdout to assert the AzDO output-variable marker is emitted.
            $out = Invoke-Post -CandidatesFile $emptyFile -LookbackDays 7 -RunUrl 'https://example/run/x' -Config $cfg -DryRun *>&1 | Out-String
            if ($out -notmatch '##vso\[task\.setvariable variable=issueNumber\]') {
                Write-Error "Invoke-Post on empty candidates did not emit the issueNumber output marker. Captured:`n$out"; $errors++
            }
        } catch {
            Write-Error "Invoke-Post crashed on empty `[]` input: $($_.Exception.Message)"; $errors++
        }
    } finally {
        if (Test-Path -LiteralPath $tmpRoot) { Remove-Item -LiteralPath $tmpRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }

    # Set-AzdoOutputVariable format: emits the exact marker AzDO parses.
    $out = Set-AzdoOutputVariable -Name 'foo' -Value 'bar-baz' *>&1 | Out-String
    if ($out.Trim() -ne '##vso[task.setvariable variable=foo]bar-baz') {
        Write-Error "Set-AzdoOutputVariable wrong format: '$($out.Trim())'"; $errors++
    }
    $outEmpty = Set-AzdoOutputVariable -Name 'foo' -Value '' *>&1 | Out-String
    if ($outEmpty.Trim() -ne '##vso[task.setvariable variable=foo]') {
        Write-Error "Set-AzdoOutputVariable empty-value wrong format: '$($outEmpty.Trim())'"; $errors++
    }

    # Find-ExistingReportIssue: uses the read-your-writes-consistent
    # List Issues endpoint (NOT Search), filters to state=open + the
    # report label, and post-filters on exact title. Two regressions
    # this test locks down:
    #  (a) closed-issue zombie-update — earlier this week the operator
    #      closed report #N within a minute of it being filed; the next
    #      pipeline run silently updated that closed issue's body,
    #      hiding the latest list from the operator's feed.
    #  (b) Search-API eventual-consistency duplicate — Search takes 30s
    #      to several minutes to index a new issue. A same-week re-run
    #      kicked off in that window would silently file a duplicate.
    #      List Issues is read-your-writes consistent, so the
    #      just-filed issue is always visible.
    $script:__capturedUri = $null
    $script:__listResp    = $null
    function script:Invoke-RestMethod {
        param([string] $Method, [string] $Uri, [hashtable] $Headers,
              [string] $Body, [string] $ContentType)
        $script:__capturedUri = $Uri
        return $script:__listResp
    }
    try {
        # Pretend the List endpoint returned both a closed and an open
        # match (it would normally only return open with state=open in
        # the URL, but the post-filter still rejects a hypothetical
        # closed entry as defense-in-depth).
        $script:__listResp = @(
            [pscustomobject] @{ number = 100; title = 'Match'; state = 'closed'; html_url = 'u-closed' },
            [pscustomobject] @{ number = 101; title = 'Match'; state = 'open';   html_url = 'u-open'   }
        )
        $found = Find-ExistingReportIssue -Owner 'o' -Repo 'r' -Title 'Match' -Label 'L' -Headers @{}
        if ($script:__capturedUri -notmatch '/repos/o/r/issues') {
            Write-Error "Find-ExistingReportIssue: did NOT call the list-issues endpoint. Uri=$($script:__capturedUri)"; $errors++
        }
        if ($script:__capturedUri -match '/search/') {
            Write-Error "Find-ExistingReportIssue: still using Search API. Uri=$($script:__capturedUri)"; $errors++
        }
        if ($script:__capturedUri -notmatch 'state=open') {
            Write-Error "Find-ExistingReportIssue: missing state=open. Uri=$($script:__capturedUri)"; $errors++
        }
        if ($script:__capturedUri -notmatch 'labels=L') {
            Write-Error "Find-ExistingReportIssue: missing labels= query. Uri=$($script:__capturedUri)"; $errors++
        }
        if (-not $found) {
            Write-Error 'Find-ExistingReportIssue: returned $null when an open match existed.'; $errors++
        } elseif ($found.number -ne 101) {
            Write-Error "Find-ExistingReportIssue: returned wrong item (#$($found.number)); expected the OPEN one (#101)."; $errors++
        }

        # Closed-only response: must return $null so caller files fresh.
        $script:__listResp = @(
            [pscustomobject] @{ number = 200; title = 'Match'; state = 'closed'; html_url = 'u-closed-only' }
        )
        $found2 = Find-ExistingReportIssue -Owner 'o' -Repo 'r' -Title 'Match' -Label 'L' -Headers @{}
        if ($found2) {
            Write-Error "Find-ExistingReportIssue: returned closed issue when no open match exists. Got #$($found2.number)."; $errors++
        }

        # PR collision: /repos/{owner}/{repo}/issues also returns PRs
        # alongside issues; the `pull_request` field-presence filter
        # must reject them so an identically-titled PR can't be
        # mistaken for the report issue.
        $script:__listResp = @(
            [pscustomobject] @{ number = 300; title = 'Match'; state = 'open'; pull_request = [pscustomobject] @{ url = 'pr-url' }; html_url = 'pr' }
        )
        $found3 = Find-ExistingReportIssue -Owner 'o' -Repo 'r' -Title 'Match' -Label 'L' -Headers @{}
        if ($found3) {
            Write-Error "Find-ExistingReportIssue: matched a PR (#$($found3.number)) instead of skipping it."; $errors++
        }

        # Empty list response: returns $null cleanly.
        $script:__listResp = @()
        $found4 = Find-ExistingReportIssue -Owner 'o' -Repo 'r' -Title 'Match' -Label 'L' -Headers @{}
        if ($found4) {
            Write-Error 'Find-ExistingReportIssue: returned non-null on empty list response.'; $errors++
        }

        # Single-item list response (PowerShell collapses to scalar
        # without the @(...) wrap inside the function): must still
        # work, since the matching path is the most common one.
        $script:__listResp = [pscustomobject] @{ number = 400; title = 'Match'; state = 'open'; html_url = 'u-single' }
        $found5 = Find-ExistingReportIssue -Owner 'o' -Repo 'r' -Title 'Match' -Label 'L' -Headers @{}
        if (-not $found5 -or $found5.number -ne 400) {
            Write-Error "Find-ExistingReportIssue: single-item collapsed response was not handled. Got: $($found5.number)."; $errors++
        }

        # Label with special chars must be URL-escaped in the query.
        $script:__capturedUri = $null
        $script:__listResp    = @()
        $null = Find-ExistingReportIssue -Owner 'o' -Repo 'r' -Title 'X' -Label 'with space' -Headers @{}
        if ($script:__capturedUri -notmatch 'labels=with%20space') {
            Write-Error "Find-ExistingReportIssue: label not URL-escaped. Uri=$($script:__capturedUri)"; $errors++
        }
    } finally {
        Remove-Item Function:script:Invoke-RestMethod -ErrorAction SilentlyContinue
        $script:__capturedUri = $null
        $script:__listResp    = $null
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
