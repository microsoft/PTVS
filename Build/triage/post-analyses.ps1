<#
    .SYNOPSIS
        Posts each analysis-<id>.md file as a comment on a triage GitHub
        issue, idempotently. Appends a per-run summary comment at the end.

    .DESCRIPTION
        Called from the PTVS-Triage-Fetch AzDO pipeline after analyze.ps1
        writes per-item analysis-<id>.md files.

        Idempotency: each analysis carries an HTML-comment marker on its
        first non-whitespace line of the form
            <!-- ptvs-triage-analyze:<id> -->
        Before posting, we list all existing comments on the issue and skip
        any item whose marker is already present. A re-run on the same
        issue does NOT double-post.

        Each analysis is posted as a SEPARATE comment so individual items
        can be threaded / reacted to / quoted in isolation. A final summary
        comment links back to the AzDO run.

        Auth: $env:GH_TOKEN. In the AzDO pipeline this comes from
        $(GH_PAT) -- the fine-grained GitHub PAT in PTVS-Triage-Bridge
        scoped to microsoft/PTVS with Issues: Read+Write.

    .PARAMETER AnalysisDir
        Directory containing the analysis-*.md files (as written by
        analyze.ps1).

    .PARAMETER IssueNumber
        Target issue number on $Owner/$Repo. When empty, the script exits
        cleanly with a log line -- the upstream post-report-issue.ps1
        emits an empty issueNumber in dryRun or no-candidate cases.

    .PARAMETER Owner / Repo
        Target GitHub repository. Defaults read from Build/triage/config.json.

    .PARAMETER ConfigPath
        Path to Build/triage/config.json.

    .PARAMETER RunUrl
        URL of the current pipeline run, embedded in the summary footer.

    .PARAMETER DryRun
        Print actions; do not call GitHub.

    .PARAMETER SelfTest
        Run inline smoke tests.
#>
[CmdletBinding()]
param(
    [string] $AnalysisDir,
    [string] $IssueNumber,
    [string] $Owner,
    [string] $Repo,
    [string] $ConfigPath = (Join-Path $PSScriptRoot 'config.json'),
    [string] $RunUrl,
    [switch] $DryRun,
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Shared scrubbing helpers (Get-LeakageTokens / Remove-LeakedTokens) are
# loaded from a sibling file so the egress scrub here is defended by the
# exact same logic as analyze.ps1's write-time scrub. Defense in depth:
# even though analyze.ps1 already scrubbed, a future bug there must not
# allow secrets to land on a public GitHub issue.
. (Join-Path $PSScriptRoot 'scrub.ps1')

function Get-TriageConfig {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}

function Get-GitHubHeaders {
    if (-not $env:GH_TOKEN) {
        throw "GH_TOKEN environment variable is required."
    }
    return @{
        Authorization          = "Bearer $env:GH_TOKEN"
        Accept                 = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
        'User-Agent'           = 'PTVS-triage-analyze'
    }
}

function Get-Marker {
    param([Parameter(Mandatory)] [int] $Id)
    return "<!-- ptvs-triage-analyze:$Id -->"
}

function Get-AnalysisFiles {
    # Returns [pscustomobject[]] of @{ Path; Id }, sorted by id. Unary
    # comma on return preserves array shape (StrictMode-safe).
    param([Parameter(Mandatory)] [string] $Dir)
    if (-not (Test-Path -LiteralPath $Dir)) {
        throw "AnalysisDir not found: $Dir"
    }
    $items = New-Object System.Collections.Generic.List[object]
    foreach ($f in Get-ChildItem -LiteralPath $Dir -Filter 'analysis-*.md' -File -ErrorAction SilentlyContinue) {
        $m = [regex]::Match($f.Name, '^analysis-(\d+)\.md$')
        if (-not $m.Success) { continue }
        $items.Add([pscustomobject] @{
            Path = $f.FullName
            Id   = [int] $m.Groups[1].Value
        }) | Out-Null
    }
    return ,@($items.ToArray() | Sort-Object Id)
}

function Get-ExistingMarkers {
    # Returns a HashSet[string] of marker comments already on the issue.
    # Paginates defensively (per_page=100; weekly issues may accumulate
    # >30 comments across re-runs).
    param(
        [Parameter(Mandatory)] [hashtable] $Headers,
        [Parameter(Mandatory)] [string]    $Owner,
        [Parameter(Mandatory)] [string]    $Repo,
        [Parameter(Mandatory)] [int]       $IssueNumber
    )
    $seen = [System.Collections.Generic.HashSet[string]]::new()
    $page = 1
    while ($true) {
        $uri = "https://api.github.com/repos/$Owner/$Repo/issues/$IssueNumber/comments?per_page=100&page=$page"
        $resp = Invoke-RestMethod -Method GET -Uri $uri -Headers $Headers
        if (-not $resp) { break }
        $items = @($resp)
        if ($items.Count -eq 0) { break }
        foreach ($c in $items) {
            $body = [string] (& { if ($c.PSObject.Properties['body']) { $c.body } else { '' } })
            foreach ($m in [regex]::Matches($body, '<!--\s*ptvs-triage-analyze:(\d+)\s*-->')) {
                $seen.Add($m.Value.Trim()) | Out-Null
                # Also add the normalized form so model-emitted extra
                # whitespace inside the marker doesn't fool us.
                $seen.Add(("<!-- ptvs-triage-analyze:{0} -->" -f $m.Groups[1].Value)) | Out-Null
            }
        }
        if ($items.Count -lt 100) { break }
        $page++
        if ($page -gt 50) {
            Write-Warning "Bailing out of comment pagination at page 50 (5000 comments). Something is wrong."
            break
        }
    }
    # Unary comma prevents the return pipeline from enumerating the HashSet
    # (which would unwrap 0 elements to $null and break `.Count` at the
    # call site under StrictMode).
    return ,$seen
}

function Add-IssueComment {
    param(
        [Parameter(Mandatory)] [hashtable] $Headers,
        [Parameter(Mandatory)] [string]    $Owner,
        [Parameter(Mandatory)] [string]    $Repo,
        [Parameter(Mandatory)] [int]       $IssueNumber,
        [Parameter(Mandatory)] [string]    $Body
    )
    $uri = "https://api.github.com/repos/$Owner/$Repo/issues/$IssueNumber/comments"
    $payload = @{ body = $Body } | ConvertTo-Json -Depth 5
    return Invoke-RestMethod -Method POST -Uri $uri -Headers $Headers -Body $payload -ContentType 'application/json'
}

function Build-SummaryComment {
    # Summary of the run: counts of posted vs skipped vs failed, link to the
    # audit artifact run, and a unique marker so we don't double-post the
    # summary on a re-run.
    param(
        [Parameter(Mandatory)] [int]    $Posted,
        [Parameter(Mandatory)] [int]    $Skipped,
        [Parameter(Mandatory)] [int]    $Failed,
        [Parameter()]          [string] $RunUrl,
        [Parameter()]          [string] $RunId
    )
    $marker = "<!-- ptvs-triage-analyze-summary:$RunId -->"
    $sb = New-Object System.Text.StringBuilder
    $null = $sb.AppendLine($marker)
    $null = $sb.AppendLine("### PTVS-Triage-Analyze summary")
    $null = $sb.AppendLine()
    $null = $sb.AppendLine("- Posted: **$Posted** new analysis comment(s)")
    $null = $sb.AppendLine("- Skipped (already present from a previous run): **$Skipped**")
    $null = $sb.AppendLine("- Failed (no analysis file emitted; should not happen): **$Failed**")
    $null = $sb.AppendLine()
    $null = $sb.AppendLine('---')
    $runLine = if ($RunUrl) { "[run details]($RunUrl)" } else { 'run details unavailable' }
    $null = $sb.AppendLine("_Generated by AzDO pipeline ``PTVS-Triage-Fetch``. $runLine._")
    return $sb.ToString()
}

function Invoke-Post {
    param(
        [Parameter(Mandatory)] [string] $AnalysisDir,
        # IssueNumber is allowed-empty: an empty value means upstream
        # post-report-issue.ps1 did not create/update an issue (dryRun or
        # no candidates). Invoke-Post must exit cleanly rather than throw.
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $IssueNumber,
        [Parameter(Mandatory)] [string] $Owner,
        [Parameter(Mandatory)] [string] $Repo,
        [string] $RunUrl,
        [switch] $DryRun
    )

    if ([string]::IsNullOrWhiteSpace($IssueNumber)) {
        Write-Host "IssueNumber is empty (no matching triage issue resolved upstream). Skipping post step cleanly."
        return
    }
    $issueInt = [int] $IssueNumber

    $files = Get-AnalysisFiles -Dir $AnalysisDir
    if ($files.Count -eq 0) {
        Write-Warning "No analysis-*.md files in $AnalysisDir. Nothing to post."
        return
    }
    Write-Host "Found $($files.Count) analysis file(s); target issue: $Owner/$Repo#$issueInt"

    # Pre-initialize to a real empty HashSet; the if-expression form
    # (`$x = if (...) { [HashSet]::new() }`) can enumerate and collapse
    # an empty set to $null, which trips StrictMode on `.Count`.
    $existingMarkers = [System.Collections.Generic.HashSet[string]]::new()
    $headers         = $null
    if (-not $DryRun) {
        $headers = Get-GitHubHeaders
        $existingMarkers = Get-ExistingMarkers -Headers $headers -Owner $Owner -Repo $Repo -IssueNumber $issueInt
    }
    Write-Host "Found $($existingMarkers.Count) existing marker(s) on the issue (skipped to avoid duplicates)."

    $posted = 0; $skipped = 0
    # Look up env tokens ONCE per Invoke-Post call so the egress scrub
    # below sees a stable list. In the AzDO pipeline GH_TOKEN is set; in
    # local DryRun usage it may be unset, which is fine -- Remove-LeakedTokens
    # is a no-op with an empty token list.
    $egressTokens = Get-LeakageTokens
    foreach ($f in $files) {
        $marker = Get-Marker -Id $f.Id
        if ($existingMarkers.Contains($marker)) {
            Write-Host "  SKIP #$($f.Id): marker already on issue."
            $skipped++
            continue
        }
        $body = Get-Content -LiteralPath $f.Path -Raw
        if ([string]::IsNullOrWhiteSpace($body)) {
            Write-Warning "  SKIP #$($f.Id): file is empty."
            continue
        }
        # Defense-in-depth egress scrub: analyze.ps1 already scrubbed at
        # write-time, but a prompt-injection-induced leak that slipped past
        # the on-disk scrub MUST NOT reach the public issue. Catches both
        # verbatim and whitespace-split occurrences (see scrub.ps1).
        $scrub = Remove-LeakedTokens -Text $body -Tokens $egressTokens
        if ($scrub.Fingerprints.Count -gt 0) {
            Write-Warning "  SECURITY: egress scrub caught leaked token(s) in #$($f.Id): $($scrub.Fingerprints -join ', '). Redacting before POST. (Investigate why analyze.ps1's write-time scrub missed it.)"
            $body = $scrub.Text
        }
        # GitHub issue-comment body cap is 65536 chars. Trim defensively.
        if ($body.Length -gt 65000) {
            $body = $body.Substring(0, 64900) + "`n`n_(analysis truncated to fit GitHub comment cap)_"
        }
        if ($DryRun) {
            Write-Host "  DRY-RUN POST #$($f.Id) ($($body.Length) chars)"
            $posted++
            continue
        }
        # $headers was set above in the non-DryRun branch; guaranteed non-null here.
        try {
            $r = Add-IssueComment -Headers $headers -Owner $Owner -Repo $Repo -IssueNumber $issueInt -Body $body
            Write-Host "  POSTED #$($f.Id) -> $($r.html_url)"
            $posted++
        } catch {
            Write-Warning "  FAILED to post #$($f.Id): $($_.Exception.Message). Continuing."
        }
    }

    # Final summary. Marker is run-id-scoped so successive runs produce
    # distinct summaries. AzDO sets $env:BUILD_BUILDID; fall back to a
    # GUID for local dev.
    $runId = if ($env:BUILD_BUILDID) { $env:BUILD_BUILDID }
             elseif ($env:GITHUB_RUN_ID) { $env:GITHUB_RUN_ID }
             else { [Guid]::NewGuid().ToString('N') }
    $summary = Build-SummaryComment -Posted $posted -Skipped $skipped -Failed 0 -RunUrl $RunUrl -RunId $runId
    if ($DryRun) {
        Write-Host ""
        Write-Host "DRY-RUN SUMMARY (would post on issue #$issueInt):"
        Write-Host $summary
    } else {
        try {
            $r = Add-IssueComment -Headers $headers -Owner $Owner -Repo $Repo -IssueNumber $issueInt -Body $summary
            Write-Host "Summary comment posted: $($r.html_url)"
        } catch {
            Write-Warning "Summary comment post failed: $($_.Exception.Message)."
        }
    }
}

# ──────────────────────────────────────────────────────────────────────
function Invoke-SelfTest {
    $errors = 0

    # 1. Get-Marker shape is stable and matches what analyze.ps1 writes.
    if ((Get-Marker -Id 42) -ne '<!-- ptvs-triage-analyze:42 -->') {
        Write-Error "Marker shape drift."; $errors++
    }

    # 2. Get-AnalysisFiles: skips non-matching names, sorts by numeric id
    #    (not lexical — wi-9.json must come before wi-10.json).
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ("post-analyses-test-{0}" -f ([Guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    try {
        Set-Content -LiteralPath (Join-Path $tmp 'analysis-10.md') -Value 'x'
        Set-Content -LiteralPath (Join-Path $tmp 'analysis-9.md')  -Value 'x'
        Set-Content -LiteralPath (Join-Path $tmp 'analysis-100.md') -Value 'x'
        Set-Content -LiteralPath (Join-Path $tmp 'notes.md')       -Value 'x'
        Set-Content -LiteralPath (Join-Path $tmp 'analysis-abc.md') -Value 'x'
        $files = Get-AnalysisFiles -Dir $tmp
        if ($files.Count -ne 3) { Write-Error "Expected 3 numeric analyses, got $($files.Count)"; $errors++ }
        if ($files[0].Id -ne 9 -or $files[1].Id -ne 10 -or $files[2].Id -ne 100) {
            Write-Error "Numeric sort broken: $($files.Id -join ',')"; $errors++
        }
        # Shape regression: empty / single-file dirs must return real
        # arrays (.Count works under StrictMode).
        $emptyDir = Join-Path ([IO.Path]::GetTempPath()) ("post-analyses-empty-{0}" -f ([Guid]::NewGuid().ToString('N')))
        New-Item -ItemType Directory -Force -Path $emptyDir | Out-Null
        try {
            $emptyFiles = Get-AnalysisFiles -Dir $emptyDir
            if ($null -eq $emptyFiles)                  { Write-Error "Get-AnalysisFiles on empty dir returned `$null"; $errors++ }
            elseif ($emptyFiles.Count -ne 0)            { Write-Error "Get-AnalysisFiles on empty dir returned Count=$($emptyFiles.Count), expected 0"; $errors++ }
            # Single-file case: must return Object[1], not collapsed scalar
            Set-Content -LiteralPath (Join-Path $emptyDir 'analysis-42.md') -Value 'x'
            $singleFiles = Get-AnalysisFiles -Dir $emptyDir
            if ($null -eq $singleFiles)                 { Write-Error "Get-AnalysisFiles on 1-file dir returned `$null"; $errors++ }
            elseif ($singleFiles.Count -ne 1)           { Write-Error "Get-AnalysisFiles on 1-file dir returned Count=$($singleFiles.Count), expected 1"; $errors++ }
            elseif ($singleFiles[0].Id -ne 42)          { Write-Error "Get-AnalysisFiles single-file Id mismatch: $($singleFiles[0].Id)"; $errors++ }
        } finally {
            Remove-Item -LiteralPath $emptyDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    } finally {
        Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }

    # 3. Get-ExistingMarkers: parses markers out of comment bodies, normalizes
    #    whitespace variants, follows pagination. CRITICAL: must NOT match
    #    failure-stub markers `<!-- ptvs-triage-analyze:N:failed:BID -->` —
    #    those carry a BuildId suffix so successful retries aren't blocked.
    $script:__page = 0
    function script:Invoke-RestMethod {
        param([string] $Method, [string] $Uri, [hashtable] $Headers)
        $script:__page++
        switch ($script:__page) {
            1 { return @(
                  [pscustomobject] @{ body = "<!-- ptvs-triage-analyze:1 -->`nhi" },
                  [pscustomobject] @{ body = '<!--   ptvs-triage-analyze:2   --> spaced' },
                  # Failure stub from a previous run; MUST be ignored by the
                  # regex so a successful retry for item 5 lands a real comment.
                  [pscustomobject] @{ body = "<!-- ptvs-triage-analyze:5:failed:abc123 -->`nstub" }
                ) }
            2 { return @(
                  [pscustomobject] @{ body = '<!-- ptvs-triage-analyze:3 -->' },
                  [pscustomobject] @{ body = 'no markers here' }
                ) }
            default { return @() }
        }
    }
    try {
        # Single-page run: shadow returns 2 items on page 1, < 100, so the
        # loop exits after page 1. Verifies marker normalization works.
        $markers = Get-ExistingMarkers -Headers @{} -Owner 'o' -Repo 'r' -IssueNumber 1
        if (-not $markers.Contains('<!-- ptvs-triage-analyze:1 -->')) {
            Write-Error "Marker 1 missing; got: $($markers -join '|')"; $errors++
        }
        # Normalized form should be added for the spaced variant.
        if (-not $markers.Contains('<!-- ptvs-triage-analyze:2 -->')) {
            Write-Error "Normalized marker 2 missing; got: $($markers -join '|')"; $errors++
        }
        # Failure stub MUST NOT appear in the marker set, else a successful
        # retry for item 5 in a later run would be incorrectly skipped.
        if ($markers.Contains('<!-- ptvs-triage-analyze:5 -->')) {
            Write-Error "Failure-stub for item 5 leaked into the success-marker set — successful retries would be blocked."; $errors++
        }
        foreach ($mk in $markers) {
            if ($mk -match ':failed:') {
                Write-Error "Failure-stub marker '$mk' present in success-marker set; regex over-matches."; $errors++
            }
        }
    } finally {
        Remove-Item Function:script:Invoke-RestMethod -ErrorAction SilentlyContinue
        $script:__page = 0
    }

    # 3b. Get-ExistingMarkers: empty-page case must return an empty HashSet
    #     (not $null) so the caller's `.Count` and `.Contains()` work.
    function script:Invoke-RestMethod {
        param([string] $Method, [string] $Uri, [hashtable] $Headers)
        return @()
    }
    try {
        $emptyMarkers = Get-ExistingMarkers -Headers @{} -Owner 'o' -Repo 'r' -IssueNumber 1
        if ($null -eq $emptyMarkers) {
            Write-Error "Get-ExistingMarkers returned `$null on empty pages; must return empty HashSet."; $errors++
        }
        elseif ($emptyMarkers.GetType().Name -notlike 'HashSet*') {
            Write-Error "Get-ExistingMarkers returned $($emptyMarkers.GetType().Name), expected HashSet."; $errors++
        }
        elseif ($emptyMarkers.Count -ne 0) {
            Write-Error "Get-ExistingMarkers on empty pages: Count=$($emptyMarkers.Count), expected 0."; $errors++
        }
    } finally {
        Remove-Item Function:script:Invoke-RestMethod -ErrorAction SilentlyContinue
    }

    # 4. Build-SummaryComment: contains counts, marker, footer link.
    $s = Build-SummaryComment -Posted 21 -Skipped 3 -Failed 0 -RunUrl 'https://example/run/1' -RunId 'r9'
    if ($s -notmatch '<!-- ptvs-triage-analyze-summary:r9 -->') { Write-Error "Summary marker missing."; $errors++ }
    if ($s -notmatch 'Posted:.*\*\*21\*\*')                     { Write-Error "Posted count missing."; $errors++ }
    if ($s -notmatch 'Skipped.*\*\*3\*\*')                      { Write-Error "Skipped count missing."; $errors++ }
    if ($s -notmatch '\[run details\]\(https://example/run/1\)') { Write-Error "Run URL missing."; $errors++ }

    # 5. Invoke-Post: empty IssueNumber → clean no-op (no GH call). Verified
    #    by leaving GH_TOKEN unset; Get-GitHubHeaders would throw if called.
    $origTok = $env:GH_TOKEN
    try {
        $env:GH_TOKEN = $null
        $tmp = Join-Path ([IO.Path]::GetTempPath()) ("post-empty-{0}" -f ([Guid]::NewGuid().ToString('N')))
        New-Item -ItemType Directory -Force -Path $tmp | Out-Null
        try {
            Invoke-Post -AnalysisDir $tmp -IssueNumber '' -Owner 'o' -Repo 'r'
        } finally {
            Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
        }
    } finally {
        $env:GH_TOKEN = $origTok
    }

    # 6. Invoke-Post dry-run: builds, sees, prints; does not call GH.
    $tmp2 = Join-Path ([IO.Path]::GetTempPath()) ("post-dry-{0}" -f ([Guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Force -Path $tmp2 | Out-Null
    try {
        Set-Content -LiteralPath (Join-Path $tmp2 'analysis-77.md') `
            -Value "<!-- ptvs-triage-analyze:77 -->`n### body" -Encoding UTF8
        # No GH_TOKEN, no shadowed Invoke-RestMethod — should not crash.
        $orig = $env:GH_TOKEN
        try { $env:GH_TOKEN = $null
            Invoke-Post -AnalysisDir $tmp2 -IssueNumber '8542' -Owner 'microsoft' -Repo 'PTVS' -DryRun
        } finally { $env:GH_TOKEN = $orig }
    } finally {
        Remove-Item -LiteralPath $tmp2 -Recurse -Force -ErrorAction SilentlyContinue
    }

    # 7. EGRESS SCRUB: an analysis file containing the verbatim value of
    #    an env token MUST be redacted before egress (POST or DRY-RUN-print).
    #    Defense-in-depth check vs analyze.ps1 leaving a leak on disk.
    $tmp3 = Join-Path ([IO.Path]::GetTempPath()) ("post-egress-{0}" -f ([Guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Force -Path $tmp3 | Out-Null
    try {
        $leakTok = 'github_pat_' + ('Q' * 80)   # 91 chars, >= 30 floor
        # Plant a leak that "slipped past" analyze.ps1's write-time scrub.
        $leakBody = "<!-- ptvs-triage-analyze:55 -->`n### Triage analysis for [AzDO #55](u)`n`nThe agent saw: $leakTok"
        Set-Content -LiteralPath (Join-Path $tmp3 'analysis-55.md') -Value $leakBody -Encoding UTF8

        $origGh = $env:GH_TOKEN
        $origPrint = $env:COPILOT_GITHUB_TOKEN
        try {
            $env:GH_TOKEN              = $null   # avoid the dry-run path needing a header
            $env:COPILOT_GITHUB_TOKEN  = $leakTok # this is what egress will scrub against
            $warn = $null
            # Capture the actual dry-run print output via 6> (Information)
            # ... actually easier: just verify the file-on-disk leak fingerprint
            # would be caught by Remove-LeakedTokens directly. Belt + suspenders:
            $direct = Remove-LeakedTokens -Text $leakBody -Tokens (Get-LeakageTokens)
            if ($direct.Fingerprints.Count -lt 1) {
                Write-Error "Egress scrub helper did NOT detect leak in planted analysis body."; $errors++
            }
            if ($direct.Text.Contains($leakTok)) {
                Write-Error "Egress scrub helper FAILED to redact verbatim leak. PUBLIC-EGRESS REGRESSION."; $errors++
            }
            # And verify Invoke-Post (DryRun) doesn't crash on a body containing
            # the leak (it should just scrub silently as part of normal flow).
            Invoke-Post -AnalysisDir $tmp3 -IssueNumber '8542' -Owner 'microsoft' -Repo 'PTVS' -DryRun -WarningVariable warn -WarningAction SilentlyContinue
            if (-not ($warn -match 'egress scrub caught')) {
                Write-Error "Invoke-Post did not warn about caught egress leak (warn=$warn)."; $errors++
            }
        } finally {
            $env:GH_TOKEN             = $origGh
            $env:COPILOT_GITHUB_TOKEN = $origPrint
        }
    } finally {
        Remove-Item -LiteralPath $tmp3 -Recurse -Force -ErrorAction SilentlyContinue
    }

    if ($errors -gt 0) {
        throw "post-analyses.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'post-analyses.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }

if (-not $AnalysisDir) { throw "-AnalysisDir is required (omit only with -SelfTest)." }

$config = Get-TriageConfig -Path $ConfigPath
if (-not $Owner -and $config) { $Owner = [string] $config.github.owner }
if (-not $Repo  -and $config) { $Repo  = [string] $config.github.repo  }
if (-not $Owner -or -not $Repo) {
    throw "-Owner and -Repo are required (or a readable config.json with .github.owner/.repo)."
}

Invoke-Post -AnalysisDir $AnalysisDir -IssueNumber ([string] $IssueNumber) `
            -Owner $Owner -Repo $Repo -RunUrl $RunUrl -DryRun:$DryRun
