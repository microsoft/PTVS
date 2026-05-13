<#
    .SYNOPSIS
        Reads per-candidate verdicts and cluster map, then dispatches the
        per-verdict outcomes (post on AzDO and close, mirror to GitHub and
        close as DC duplicate, or leave alone).

    .DESCRIPTION
        Implements plan.md §6.3 Step 6.

        Inputs:
          - verdict-<id>.json files under -VerdictsDir (one per cluster
            primary). Each is the JSON produced by actions/ai-inference and
            conforms to triage.prompt.yml's response schema:
              { verdict, confidence, response_md, github_issue_body_md,
                missing_info[], related_urls[], source_issue_for_resolution }
          - clusters.json under -RunArtifacts (or sanitized/ within it):
            [[primary, follower, follower], [primary], ...].
          - sanitized/wi-<id>.json under -RunArtifacts: gives us the rev,
            existing tags, area_path, etc. for each candidate.

        Crash-safe ordering (per verdict that requires multiple side
        effects):
          1. Create the GitHub mirror issue (idempotent search first).
          2. Post the AzDO comment containing the GH issue URL.
          3. PATCH the AzDO work item state + duplicate field + tags
             (concurrency-safe with `test /rev`).

        Behavior gates:
          - $env:DRY_RUN = 'true'  → no writes; emits run-summary.md only.
          - sanitization_aborted   → skip mirror, flag for manual review.
          - confidence < threshold → skip writes, flag for manual review.
          - verdict == security    → never mirror; log only.

        Output: $env:RUNNER_TEMP/run-summary.md (Markdown table).

    .PARAMETER VerdictsDir
        Directory containing verdict-<id>.json files (and where downloaded
        artifacts have been placed by the workflow).

    .PARAMETER RunArtifacts
        Directory containing the prepare step's outputs: clusters.json,
        sanitized/, candidates.json, run-context.json.

    .PARAMETER ConfigPath
        Path to Build/triage/config.json.

    .PARAMETER SelfTest
        Run inline smoke tests.
#>
[CmdletBinding()]
param(
    [string] $VerdictsDir,
    [string] $RunArtifacts,
    [string] $ConfigPath = (Join-Path $PSScriptRoot 'config.json'),
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Dot-source helper functions. Both helper scripts declare their own `$SelfTest`
# param block; dot-sourcing executes that param block in the current scope and
# would otherwise clobber our own `$SelfTest`. Save and restore around the
# dot-source so this script's `-SelfTest` switch is preserved.
$__SelfTestSaved = $SelfTest
. (Join-Path $PSScriptRoot 'post-azdo.ps1')
. (Join-Path $PSScriptRoot 'mirror-to-github.ps1')
$SelfTest = $__SelfTestSaved
Remove-Variable -Name __SelfTestSaved -ErrorAction SilentlyContinue

function Get-TriageConfig { param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw "Config file not found: $Path" }
    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}

function Test-DryRun {
    $v = $env:DRY_RUN
    if (-not $v) { return $true }   # default: dry-run if unset, per plan §10 phase 1.
    return ($v -match '^(?i:true|1|yes|y)$')
}

function Read-Verdicts {
    param([string] $Dir)
    $files = Get-ChildItem -LiteralPath $Dir -Filter 'verdict-*.json' -File -ErrorAction SilentlyContinue
    $map = @{}
    foreach ($f in $files) {
        if ($f.BaseName -notmatch '^verdict-(\d+)$') { continue }
        $id = [int] $Matches[1]
        try {
            $obj = Get-Content -LiteralPath $f.FullName -Raw | ConvertFrom-Json
            $map[$id] = $obj
        } catch {
            Write-Warning "Could not parse $($f.Name): $($_.Exception.Message)"
        }
    }
    return $map
}

function Read-Clusters {
    param([string] $RunArtifacts)
    $path = Join-Path $RunArtifacts 'clusters.json'
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Warning "clusters.json not found at $path; assuming each candidate is its own cluster."
        return @()
    }
    return @((Get-Content -LiteralPath $path -Raw | ConvertFrom-Json))
}

function Read-SanitizedWorkItem {
    param([string] $RunArtifacts, [int] $Id)
    $candidates = @(
        (Join-Path $RunArtifacts ("sanitized\wi-{0}.json" -f $Id)),
        (Join-Path $RunArtifacts ("sanitized/wi-{0}.json" -f $Id))
    )
    foreach ($p in $candidates) {
        if (Test-Path -LiteralPath $p) { return (Get-Content -LiteralPath $p -Raw | ConvertFrom-Json) }
    }
    Write-Warning "Sanitized WI not found for #$Id (looked under $RunArtifacts/sanitized/)."
    return $null
}

function Read-OriginalCandidate {
    # Used to get tags/rev directly from the AzDO snapshot if needed (the
    # sanitized JSON already includes them, but for apply we re-read live
    # from AzDO so we have a fresh rev for the optimistic-concurrency test).
    param([string] $RunArtifacts, [int] $Id)
    $path = Join-Path $RunArtifacts 'candidates.json'
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    $all = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($all -isnot [System.Array]) { $all = @($all) }
    return ($all | Where-Object { [int] $_.id -eq $Id } | Select-Object -First 1)
}

function Get-ThresholdForVerdict {
    param([object] $Config, [string] $Verdict)
    if ($Config.thresholds.PSObject.Properties[$Verdict]) {
        return [double] $Config.thresholds.$Verdict
    }
    return 1.1   # unknown verdict → never act automatically
}

function Get-LabelsForVerdict {
    param([object] $Config, [string] $Verdict)
    if ($Config.github.labelsByVerdict.PSObject.Properties[$Verdict]) {
        return @($Config.github.labelsByVerdict.$Verdict)
    }
    return @($Config.github.mirrorLabel)
}

function Format-NeedsInfoBlock {
    param([Parameter(Mandatory)] [string[]] $Missing)
    if (-not $Missing -or $Missing.Count -eq 0) { return '' }
    $sb = New-Object System.Text.StringBuilder
    $null = $sb.AppendLine()
    $null = $sb.AppendLine('### What we need from you')
    $null = $sb.AppendLine()
    foreach ($m in $Missing) { $null = $sb.AppendLine("- $m") }
    return $sb.ToString()
}

function Invoke-Apply {
    param(
        [Parameter(Mandatory)] [string] $VerdictsDir,
        [Parameter(Mandatory)] [string] $RunArtifacts,
        [Parameter(Mandatory)] [object] $Config
    )

    $isDryRun  = Test-DryRun
    Write-Host "DRY_RUN = $isDryRun"

    $verdicts  = Read-Verdicts -Dir $VerdictsDir
    $clusters  = Read-Clusters -RunArtifacts $RunArtifacts

    # Build primary→followers map. Primaries can also appear without a verdict
    # if AI inference failed for them; we record those for manual triage.
    $primaryToFollowers = @{}
    foreach ($cl in $clusters) {
        if (-not $cl -or @($cl).Count -eq 0) { continue }
        $primary = [int] $cl[0]
        $primaryToFollowers[$primary] = @($cl | Select-Object -Skip 1 | ForEach-Object { [int] $_ })
    }
    # Fallback: ensure every verdict has a primary entry (1-element cluster).
    foreach ($id in $verdicts.Keys) {
        if (-not $primaryToFollowers.ContainsKey([int] $id)) {
            $primaryToFollowers[[int] $id] = @()
        }
    }

    $summary = New-Object System.Collections.Generic.List[object]

    foreach ($primary in $primaryToFollowers.Keys) {
        $verdict = $verdicts[$primary]
        $san     = Read-SanitizedWorkItem -RunArtifacts $RunArtifacts -Id $primary
        $followers = $primaryToFollowers[$primary]

        if (-not $verdict) {
            $summary.Add([pscustomobject] @{
                id = $primary; verdict = 'manual-triage-needed'; confidence = ''; action = 'no verdict produced'; gh_url = ''; followers = $followers
            }) | Out-Null
            continue
        }

        $v          = ($verdict.verdict | Out-String).Trim()
        $confidence = [double] $verdict.confidence
        $threshold  = Get-ThresholdForVerdict -Config $Config -Verdict $v
        $low        = $confidence -lt $threshold

        # Security verdict — never write anything, never mirror.
        if ($v -eq 'security') {
            $summary.Add([pscustomobject] @{
                id = $primary; verdict = $v; confidence = $confidence
                action = 'flagged for MSRC (no public mirror, no AzDO write)'
                gh_url = ''; followers = $followers
            }) | Out-Null
            continue
        }

        # Sanitization aborted (secret detected in customer content)?
        if ($san -and $san.sanitization_aborted) {
            $summary.Add([pscustomobject] @{
                id = $primary; verdict = $v; confidence = $confidence
                action = 'manual review (sanitization aborted — secret pattern matched)'
                gh_url = ''; followers = $followers
            }) | Out-Null
            continue
        }

        # Below threshold or not_actionable → leave alone.
        if ($v -eq 'not_actionable' -or $low) {
            $reason = if ($low) { "low confidence ($([Math]::Round($confidence,2)) < $threshold)" } else { 'not_actionable' }
            $summary.Add([pscustomobject] @{
                id = $primary; verdict = $v; confidence = $confidence
                action = "manual review ($reason)"
                gh_url = ''; followers = $followers
            }) | Out-Null
            continue
        }

        $actionTaken = ''
        $ghUrl = ''

        if ($v -eq 'answered') {
            $msg = ($verdict.response_md | Out-String).Trim()
            if (-not $msg) {
                $summary.Add([pscustomobject] @{
                    id = $primary; verdict = $v; confidence = $confidence
                    action = 'manual review (empty response_md)'
                    gh_url = ''; followers = $followers
                }) | Out-Null
                continue
            }
            if ($isDryRun) {
                $actionTaken = 'DRY: would comment + close AzDO as answered'
            } else {
                $live = Get-AzdoWorkItem -Config $Config -Id $primary
                $rev  = [int] $live.fields.'System.Rev'
                $tags = ($live.fields.'System.Tags' ?? '')
                [void] (Add-AzdoComment -Config $Config -Id $primary -Text $msg)
                [void] (Close-AzdoAsAnswered -Config $Config -Id $primary -Rev $rev -ExistingTags $tags)
                $actionTaken = 'commented + closed AzDO as answered'
            }
            # Followers of an `answered` primary: same comment, then close.
            foreach ($f in $followers) {
                if ($isDryRun) {
                    $actionTaken += ", DRY follower #$f would receive same response_md + close"
                } else {
                    try {
                        $liveF = Get-AzdoWorkItem -Config $Config -Id $f
                        $revF  = [int] $liveF.fields.'System.Rev'
                        $tagsF = ($liveF.fields.'System.Tags' ?? '')
                        [void] (Add-AzdoComment -Config $Config -Id $f -Text $msg)
                        [void] (Close-AzdoAsAnswered -Config $Config -Id $f -Rev $revF -ExistingTags $tagsF)
                    } catch {
                        Write-Warning "Follower close failed for #${f}: $($_.Exception.Message)"
                    }
                }
            }

        } elseif ($v -in @('needs_info','real_bug','real_feature_request')) {

            $body = ($verdict.github_issue_body_md | Out-String).Trim()
            if (-not $body) {
                $summary.Add([pscustomobject] @{
                    id = $primary; verdict = $v; confidence = $confidence
                    action = 'manual review (empty github_issue_body_md)'
                    gh_url = ''; followers = $followers
                }) | Out-Null
                continue
            }

            $title = if ($san) { $san.title } else { "(no sanitized title)" }
            $azdoUrl = "$($Config.azdo.baseUrl)/$($Config.azdo.project)/_workitems/edit/$primary"

            if ($v -eq 'needs_info') {
                $body += (Format-NeedsInfoBlock -Missing @($verdict.missing_info))
            }
            $labels = Get-LabelsForVerdict -Config $Config -Verdict $v

            if ($isDryRun) {
                $actionTaken = "DRY: would mirror to GitHub with labels [$($labels -join ', ')] + DC duplicate close on AzDO"
            } else {
                # Step 1 — create (or find) the mirror.
                $ghIssue = New-MirrorIssue `
                    -Config $Config `
                    -AzdoId $primary `
                    -AzdoUrl $azdoUrl `
                    -RawTitle $title `
                    -BodyMarkdown $body `
                    -Labels $labels `
                    -Assignees @($Config.github.defaultAssignee)
                $ghUrl = $ghIssue.html_url

                # Step 2 — comment on AzDO with the DC duplicate template.
                $comment = Format-DcDuplicateComment -Config $Config -GithubIssueUrl $ghUrl
                $live    = Get-AzdoWorkItem -Config $Config -Id $primary
                $rev     = [int] $live.fields.'System.Rev'
                $tags    = ($live.fields.'System.Tags' ?? '')
                [void] (Add-AzdoComment -Config $Config -Id $primary -Text $comment)

                # Step 3 — PATCH state + duplicate field + tags. Re-read rev
                # because step 2 just incremented it.
                $live2 = Get-AzdoWorkItem -Config $Config -Id $primary
                $rev2  = [int] $live2.fields.'System.Rev'
                $tags2 = ($live2.fields.'System.Tags' ?? '')
                [void] (Close-AzdoAsDuplicate `
                    -Config $Config `
                    -Id $primary `
                    -Rev $rev2 `
                    -GithubIssueUrl $ghUrl `
                    -ExistingTags $tags2)
                $actionTaken = "mirrored → $ghUrl; AzDO closed as DC duplicate"
            }

            # Followers point at the same mirror.
            foreach ($f in $followers) {
                if ($isDryRun) {
                    $actionTaken += ", DRY follower #$f would close as duplicate of $ghUrl"
                } else {
                    try {
                        $comment = Format-DcDuplicateComment -Config $Config -GithubIssueUrl $ghUrl
                        $liveF = Get-AzdoWorkItem -Config $Config -Id $f
                        $revF  = [int] $liveF.fields.'System.Rev'
                        $tagsF = ($liveF.fields.'System.Tags' ?? '')
                        [void] (Add-AzdoComment -Config $Config -Id $f -Text $comment)
                        $liveF2 = Get-AzdoWorkItem -Config $Config -Id $f
                        $revF2  = [int] $liveF2.fields.'System.Rev'
                        $tagsF2 = ($liveF2.fields.'System.Tags' ?? '')
                        [void] (Close-AzdoAsDuplicate `
                            -Config $Config `
                            -Id $f `
                            -Rev $revF2 `
                            -GithubIssueUrl $ghUrl `
                            -ExistingTags $tagsF2)
                    } catch {
                        Write-Warning "Follower mirror-close failed for #${f}: $($_.Exception.Message)"
                    }
                }
            }
        } else {
            $actionTaken = "no handler for verdict '$v'"
        }

        $summary.Add([pscustomobject] @{
            id = $primary; verdict = $v; confidence = $confidence
            action = $actionTaken; gh_url = $ghUrl; followers = $followers
        }) | Out-Null
    }

    # ── Run summary ────────────────────────────────────────────────────
    $sb = New-Object System.Text.StringBuilder
    $null = $sb.AppendLine('# AzDO triage run summary')
    $null = $sb.AppendLine()
    $null = $sb.AppendLine(("dry_run = ``{0}``  •  primaries = {1}  •  followers handled = {2}" -f $isDryRun, $summary.Count, (($summary | ForEach-Object { @($_.followers).Count } | Measure-Object -Sum).Sum)))
    $null = $sb.AppendLine()
    $null = $sb.AppendLine('| AzDO ID | Verdict | Confidence | Action | GitHub mirror | Followers |')
    $null = $sb.AppendLine('|---|---|---|---|---|---|')
    foreach ($r in $summary) {
        $folStr = if ($r.followers -and @($r.followers).Count -gt 0) { ($r.followers -join ', ') } else { '—' }
        $gh = if ($r.gh_url) { $r.gh_url } else { '—' }
        $conf = if ($null -eq $r.confidence -or $r.confidence -eq '') { '—' } else { [Math]::Round([double] $r.confidence, 2) }
        $null = $sb.AppendLine("| $($r.id) | $($r.verdict) | $conf | $($r.action) | $gh | $folStr |")
    }

    $outPath = if ($env:RUNNER_TEMP) { Join-Path $env:RUNNER_TEMP 'run-summary.md' } else { Join-Path $RunArtifacts 'run-summary.md' }
    $sb.ToString() | Set-Content -LiteralPath $outPath -Encoding UTF8
    Write-Host "Wrote run summary → $outPath"
}

function Invoke-SelfTest {
    $errors = 0

    # Dry-run defaults to true when DRY_RUN is unset.
    $old = $env:DRY_RUN; $env:DRY_RUN = $null
    if (-not (Test-DryRun)) { Write-Error 'Expected Test-DryRun = true when DRY_RUN unset.'; $errors++ }
    $env:DRY_RUN = 'false'
    if (Test-DryRun) { Write-Error 'Expected Test-DryRun = false when DRY_RUN=false.'; $errors++ }
    $env:DRY_RUN = 'true'
    if (-not (Test-DryRun)) { Write-Error 'Expected Test-DryRun = true when DRY_RUN=true.'; $errors++ }
    $env:DRY_RUN = $old

    # Threshold lookup.
    $cfg = Get-TriageConfig -Path $ConfigPath
    $thr = Get-ThresholdForVerdict -Config $cfg -Verdict 'answered'
    if ($thr -lt 0.6 -or $thr -gt 0.9) { Write-Error "answered threshold unexpected: $thr"; $errors++ }
    $thr = Get-ThresholdForVerdict -Config $cfg -Verdict 'made-up-verdict'
    if ($thr -lt 1.0) { Write-Error 'Unknown-verdict threshold should be unreachable.'; $errors++ }

    # Label lookup.
    $labels = Get-LabelsForVerdict -Config $cfg -Verdict 'needs_info'
    if ($labels -notcontains 'waiting for response') { Write-Error 'needs_info labels missing waiting for response.'; $errors++ }
    $labels = Get-LabelsForVerdict -Config $cfg -Verdict 'real_bug'
    if ($labels -contains 'waiting for response') { Write-Error 'real_bug should not include waiting for response.'; $errors++ }

    # End-to-end dry-run path against fixtures.
    $fxRoot = Join-Path $PSScriptRoot 'tests\fixtures'
    $vDir = Join-Path $PSScriptRoot 'tests\.tmp-verdicts'
    $aDir = Join-Path $PSScriptRoot 'tests\.tmp-artifacts'
    Remove-Item -LiteralPath $vDir,$aDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $vDir,$aDir,(Join-Path $aDir 'sanitized') | Out-Null
    Copy-Item -LiteralPath (Join-Path $fxRoot 'verdict-2711586.json') -Destination $vDir
    Copy-Item -LiteralPath (Join-Path $fxRoot 'wi-2711586.json') -Destination (Join-Path $aDir 'sanitized')
    @(@(2711586)) | ConvertTo-Json -Depth 5 -Compress | Set-Content -LiteralPath (Join-Path $aDir 'clusters.json') -Encoding UTF8

    $env:DRY_RUN = 'true'
    $env:RUNNER_TEMP = $aDir
    Invoke-Apply -VerdictsDir $vDir -RunArtifacts $aDir -Config $cfg

    $summaryPath = Join-Path $aDir 'run-summary.md'
    if (-not (Test-Path -LiteralPath $summaryPath)) { Write-Error 'run-summary.md not written.'; $errors++ }
    else {
        $content = Get-Content -LiteralPath $summaryPath -Raw
        if ($content -notmatch 'AzDO triage run summary') { Write-Error 'summary missing heading'; $errors++ }
        if ($content -notmatch '2711586') { Write-Error 'summary missing primary id'; $errors++ }
        if ($content -notmatch 'DRY') { Write-Error 'summary did not record DRY action'; $errors++ }
    }
    Remove-Item -LiteralPath $vDir,$aDir -Recurse -Force -ErrorAction SilentlyContinue
    $env:DRY_RUN = $old

    if ($errors -gt 0) {
        throw "apply-outcomes.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'apply-outcomes.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }
if (-not $VerdictsDir)  { throw '-VerdictsDir is required.' }
if (-not $RunArtifacts) { throw '-RunArtifacts is required.' }

$cfg = Get-TriageConfig -Path $ConfigPath
Invoke-Apply -VerdictsDir $VerdictsDir -RunArtifacts $RunArtifacts -Config $cfg
