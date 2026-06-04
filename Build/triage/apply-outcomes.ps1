<#
    .SYNOPSIS
        Reads per-candidate verdicts and cluster map, then dispatches the
        per-verdict outcomes (post on AzDO and close, mirror to GitHub and
        close as DC duplicate, or leave alone).

    .DESCRIPTION
        Step 6 of the weekly triage pipeline (see Build/triage/README.md).

        Inputs:
          - verdict-<id>.json files under -VerdictsDir (one per cluster
            primary). Each is the JSON produced by the agentic triage step
            (.github/workflows/azdo-triage-agent.md) and conforms to the
            response schema defined in that file:
              { verdict, confidence, response_md, github_issue_body_md,
                missing_info[], related_urls[], source_issue_for_resolution }
          - clusters.json under -RunArtifacts (or sanitized/ within it):
            [[primary, follower, follower], [primary], ...].
          - cluster-meta.json under -RunArtifacts (optional, since the
            workflow only uploads it from prepare): the human-friendly view
            of follower titles + per-follower Jaccard similarity. Used to
            populate the "Expanded follower details" section of run-summary.md
            so the approver can see what each follower close was based on.
          - sanitized/wi-<id>.json under -RunArtifacts: gives us the rev,
            existing tags, area_path, etc. for each candidate.

        Crash-safe ordering (per verdict that requires multiple side
        effects):
          1. Create the GitHub mirror issue (idempotent search first).
          2. Post the AzDO comment containing the GH issue URL.
          3. PATCH the AzDO work item state + duplicate field + tags
             (concurrency-safe with `test /rev`).

        Idempotence: before any per-work-item write, we GET the live work
        item and skip the write entirely if the configured `triaged-by-ai`
        tag is already present. The WIQL `excludedTags` filter in the
        prepare step handles the common case of fresh weekly runs, but
        ad-hoc -WorkItemId retries and matrix re-runs after a mid-batch
        failure bypass that path entirely. For mirror primaries, when the
        tag is already present we additionally recover the existing GitHub
        mirror URL by re-running the title-marker search so the follower
        loop can still close followers as duplicates of that mirror —
        otherwise an interrupted-then-resumed run could leave followers
        orphaned.

        Pre-flight: if any verdict would mirror to GitHub (needs_info,
        real_bug, real_feature_request), we validate that
        `config.azdo.duplicateFieldName` AND `config.azdo.states.duplicate`
        are no longer the config placeholders BEFORE creating any public
        artifact. Likewise, if any verdict is `answered` we validate
        `config.azdo.states.answered`. A misconfigured run cannot leak a
        public GitHub issue while still failing on the subsequent AzDO
        PATCH.

        Behavior gates:
          - $env:DRY_RUN = 'true'  → no writes; emits run-summary.md only.
          - sanitization_aborted   → skip mirror, flag for manual review.
          - confidence < threshold → skip writes, flag for manual review.
          - verdict == security    → never mirror; log only.

        Output: $env:RUNNER_TEMP/run-summary.md (Markdown table + expanded
        follower details).

    .PARAMETER VerdictsDir
        Directory containing verdict-<id>.json files (and where downloaded
        artifacts have been placed by the workflow).

    .PARAMETER RunArtifacts
        Directory containing the prepare step's outputs: clusters.json,
        cluster-meta.json (optional), sanitized/, candidates.json,
        run-context.json.

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
    if (-not $v) { return $true }   # default: dry-run if DRY_RUN is unset.
    return ($v -match '^(?i:true|1|yes|y)$')
}

function Get-WorkItemTags {
    # AzDO omits unset fields from `fields` entirely; under StrictMode
    # the bare `?? ''` pattern would throw before the coalesce ran.
    param([Parameter(Mandatory)] [object] $Fields)
    if ($Fields.PSObject.Properties['System.Tags']) { return [string] $Fields.'System.Tags' }
    return ''
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
    $parsed = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($null -eq $parsed) { return @() }

    # PowerShell's ConvertFrom-Json unwraps single-element outer arrays:
    # `[[1,2]]` is returned as `@(1,2)` (a flat 2-int array) instead of
    # `@(@(1,2))`. Detect by inspecting the first element: if it's a scalar
    # (not an array/list), the outer wrapper was eaten and we put it back.
    $arr = @($parsed)
    if ($arr.Count -eq 0) { return @() }
    $first = $arr[0]
    $firstIsArray = ($first -is [System.Array]) -or ($first -is [System.Collections.IList])
    if (-not $firstIsArray) {
        # Flat numeric array means a single cluster with N members.
        # Wrap in a List[object[]] (not a plain comma-wrapped array) because
        # PowerShell unwraps single-element arrays at the function-return
        # boundary, which would re-collapse the cluster shape.
        $wrapper = New-Object 'System.Collections.Generic.List[object]'
        $wrapper.Add(@($arr | ForEach-Object { [int] $_ })) | Out-Null
        return ,$wrapper.ToArray()
    }
    return $arr
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

function Read-ClusterMeta {
    # cluster-meta.json is produced by cluster.ps1 alongside clusters.json
    # and primaries.json. It carries the per-follower titles + similarity
    # scores so the run summary can surface what each follower close was
    # based on. Returns a hashtable keyed by primary id; missing/empty
    # file → empty hashtable (downstream falls back to bare follower ids).
    param([string] $RunArtifacts)
    $path = Join-Path $RunArtifacts 'cluster-meta.json'
    if (-not (Test-Path -LiteralPath $path)) { return @{} }
    $raw = Get-Content -LiteralPath $path -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) { return @{} }
    try {
        $arr = $raw | ConvertFrom-Json
    } catch {
        Write-Warning "cluster-meta.json present but unreadable: $($_.Exception.Message)"
        return @{}
    }
    if ($null -eq $arr) { return @{} }
    if ($arr -isnot [System.Array]) { $arr = @($arr) }
    $map = @{}
    foreach ($entry in $arr) {
        if (-not $entry -or -not $entry.PSObject.Properties['primary']) { continue }
        $primaryKey = [int] $entry.primary.id
        $map[$primaryKey] = $entry
    }
    return $map
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

    $verdicts   = Read-Verdicts -Dir $VerdictsDir
    $clusters   = Read-Clusters -RunArtifacts $RunArtifacts
    $clusterMeta = Read-ClusterMeta -RunArtifacts $RunArtifacts

    # Pre-flight: if any verdict would mirror to GitHub and the duplicate
    # field name is still the config placeholder, fail BEFORE creating any
    # public GitHub artifact. Without this check the first mirror leg would
    # succeed in creating a public issue and only the subsequent AzDO PATCH
    # would throw — leaving an orphaned public GitHub issue behind.
    #
    # Also validate the AzDO state strings up front. If any of the configured
    # close-states looks like a placeholder (literal '<…>' from config.json),
    # we'd hit the same orphan-mirror failure mode on the AzDO PATCH leg
    # even though the duplicate field itself is set correctly.
    if (-not $isDryRun) {
        $needsDupField = $false
        $needsAnswered = $false
        foreach ($vid in $verdicts.Keys) {
            $vv = ($verdicts[$vid].verdict | Out-String).Trim()
            if ($vv -in @('needs_info','real_bug','real_feature_request')) { $needsDupField = $true }
            if ($vv -eq 'answered') { $needsAnswered = $true }
        }
        if ($needsDupField) {
            $field = $Config.azdo.duplicateFieldName
            if (-not $field -or $field -eq '<DuplicateFeedbackTicketIdField>') {
                throw "Refusing to start apply: at least one verdict requires a mirror-then-DC-duplicate close, but config.azdo.duplicateFieldName is still the placeholder. Confirm the exact REST field name in phase 0 and set it in Build/triage/config.json before enabling live mode."
            }
            $stateDup = $Config.azdo.states.duplicate
            if (-not $stateDup -or $stateDup -match '^<.*>$') {
                throw "Refusing to start apply: at least one verdict requires a DC-duplicate close, but config.azdo.states.duplicate is missing or still a placeholder ('$stateDup'). Confirm the exact AzDO state string in phase 0 and set it in Build/triage/config.json."
            }
        }
        if ($needsAnswered) {
            $stateAns = $Config.azdo.states.answered
            if (-not $stateAns -or $stateAns -match '^<.*>$') {
                throw "Refusing to start apply: at least one verdict requires an 'answered' close, but config.azdo.states.answered is missing or still a placeholder ('$stateAns'). Confirm the exact AzDO state string in phase 0 and set it in Build/triage/config.json."
            }
        }
    }

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
                try {
                    # Idempotence guard: GET once before any write; if the
                    # ai_tag is already present, treat as already triaged.
                    $live = Get-AzdoWorkItem -Config $Config -Id $primary
                    $rev  = [int] $live.fields.'System.Rev'
                    $tags = Get-WorkItemTags -Fields $live.fields
                    if (Test-AlreadyTriagedByAi -Config $Config -Tags $tags) {
                        $actionTaken = 'skipped (already triaged-by-ai)'
                    } else {
                        [void] (Add-AzdoComment -Config $Config -Id $primary -Text $msg)
                        # Close uses the original rev so the `test /rev` op
                        # rejects any concurrent human edit that happened
                        # between our GET and this PATCH. The AzDO /comments
                        # endpoint does not bump rev, so reusing it is safe.
                        [void] (Close-AzdoAsAnswered -Config $Config -Id $primary -Rev $rev -ExistingTags $tags)
                        $actionTaken = 'commented + closed AzDO as answered'
                    }
                } catch {
                    Write-Warning "Answered primary failed for #${primary}: $($_.Exception.Message)"
                    $actionTaken = "manual review (answered primary failed: $($_.Exception.Message))"
                }
            }
            # Followers of an `answered` primary: same comment, then close.
            $followerFailures = New-Object System.Collections.Generic.List[string]
            foreach ($f in $followers) {
                if ($isDryRun) {
                    $actionTaken += ", DRY follower #$f would receive same response_md + close"
                } else {
                    try {
                        $liveF = Get-AzdoWorkItem -Config $Config -Id $f
                        $revF  = [int] $liveF.fields.'System.Rev'
                        $tagsF = Get-WorkItemTags -Fields $liveF.fields
                        if (Test-AlreadyTriagedByAi -Config $Config -Tags $tagsF) {
                            $actionTaken += ", follower #${f} skipped (already triaged-by-ai)"
                            continue
                        }
                        [void] (Add-AzdoComment -Config $Config -Id $f -Text $msg)
                        [void] (Close-AzdoAsAnswered -Config $Config -Id $f -Rev $revF -ExistingTags $tagsF)
                    } catch {
                        Write-Warning "Follower close failed for #${f}: $($_.Exception.Message)"
                        $followerFailures.Add("#${f}: $($_.Exception.Message)") | Out-Null
                    }
                }
            }
            if ($followerFailures.Count -gt 0) {
                $actionTaken += " ⚠ follower failures: $($followerFailures -join '; ')"
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
                try {
                    # Idempotence guard: check BEFORE creating the mirror.
                    # If this primary is already tagged, the GH issue + AzDO
                    # close should not be re-run — but we still need $ghUrl
                    # so the follower loop can close them as duplicates of
                    # the existing mirror. Recover it via the same title-
                    # marker search that New-MirrorIssue uses internally.
                    $live = Get-AzdoWorkItem -Config $Config -Id $primary
                    $rev  = [int] $live.fields.'System.Rev'
                    $tags = Get-WorkItemTags -Fields $live.fields
                    if (Test-AlreadyTriagedByAi -Config $Config -Tags $tags) {
                        $existing = Find-ExistingMirrorIssue `
                            -Owner   $Config.github.owner `
                            -Repo    $Config.github.repo `
                            -AzdoId  $primary `
                            -Headers (Get-GitHubHeaders)
                        if ($existing) {
                            $ghUrl = $existing.html_url
                            $actionTaken = "skipped primary (already triaged-by-ai); recovered existing mirror $ghUrl for follower processing"
                        } else {
                            $actionTaken = 'skipped (already triaged-by-ai; no existing mirror found — followers will be skipped)'
                        }
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
                        [void] (Add-AzdoComment -Config $Config -Id $primary -Text $comment)

                        # Step 3 — PATCH state + duplicate field + tags using
                        # the original rev. The `test /rev` op rejects any
                        # concurrent human edit that happened between our GET
                        # at the top of this block and now (the AzDO /comments
                        # endpoint does not bump rev, so reusing the original
                        # rev is safe and strictly stronger than re-reading).
                        [void] (Close-AzdoAsDuplicate `
                            -Config $Config `
                            -Id $primary `
                            -Rev $rev `
                            -GithubIssueUrl $ghUrl `
                            -ExistingTags $tags)
                        $actionTaken = "mirrored -> $ghUrl; AzDO closed as DC duplicate"
                    }
                } catch {
                    Write-Warning "Mirror primary failed for #${primary}: $($_.Exception.Message)"
                    $actionTaken = "manual review (mirror primary failed: $($_.Exception.Message))"
                }
            }

            # Followers point at the same mirror.
            $followerFailures = New-Object System.Collections.Generic.List[string]
            foreach ($f in $followers) {
                if ($isDryRun) {
                    $actionTaken += ", DRY follower #$f would close as duplicate of $ghUrl"
                } else {
                    if (-not $ghUrl) {
                        # Primary failed before producing a mirror URL — skip.
                        $actionTaken += ", follower #${f} skipped (primary did not produce a mirror)"
                        continue
                    }
                    try {
                        $liveF = Get-AzdoWorkItem -Config $Config -Id $f
                        $revF  = [int] $liveF.fields.'System.Rev'
                        $tagsF = Get-WorkItemTags -Fields $liveF.fields
                        if (Test-AlreadyTriagedByAi -Config $Config -Tags $tagsF) {
                            $actionTaken += ", follower #${f} skipped (already triaged-by-ai)"
                            continue
                        }
                        $comment = Format-DcDuplicateComment -Config $Config -GithubIssueUrl $ghUrl
                        [void] (Add-AzdoComment -Config $Config -Id $f -Text $comment)
                        [void] (Close-AzdoAsDuplicate `
                            -Config $Config `
                            -Id $f `
                            -Rev $revF `
                            -GithubIssueUrl $ghUrl `
                            -ExistingTags $tagsF)
                    } catch {
                        Write-Warning "Follower mirror-close failed for #${f}: $($_.Exception.Message)"
                        $followerFailures.Add("#${f}: $($_.Exception.Message)") | Out-Null
                    }
                }
            }
            if ($followerFailures.Count -gt 0) {
                $actionTaken += " ⚠ follower failures: $($followerFailures -join '; ')"
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

    # Expanded follower details: surfaces what each follower close was based
    # on (title + Jaccard similarity score) so the human approver of the
    # `azdo-triage-apply` environment gate can see WHY each follower was
    # closed as a duplicate of its primary — not just bare ids.
    $clustersWithFollowers = @($summary | Where-Object { $_.followers -and @($_.followers).Count -gt 0 })
    if ($clustersWithFollowers.Count -gt 0) {
        $null = $sb.AppendLine()
        $null = $sb.AppendLine('## Expanded follower details')
        foreach ($r in $clustersWithFollowers) {
            $null = $sb.AppendLine()
            $primaryId = [int] $r.id
            $primaryTitle = $null
            if ($clusterMeta.ContainsKey($primaryId) -and $clusterMeta[$primaryId].primary.PSObject.Properties['title']) {
                $primaryTitle = [string] $clusterMeta[$primaryId].primary.title
            }
            $heading = if ($primaryTitle) { "### Primary #$primaryId — $primaryTitle" } else { "### Primary #$primaryId" }
            $null = $sb.AppendLine($heading)
            $null = $sb.AppendLine()
            $null = $sb.AppendLine('| Follower | Title | Similarity to primary |')
            $null = $sb.AppendLine('|---|---|---|')
            foreach ($fid in @($r.followers)) {
                $fidInt = [int] $fid
                $ftitle = '(title not in cluster-meta.json)'
                $fsim   = '—'
                if ($clusterMeta.ContainsKey($primaryId)) {
                    $followerEntries = @($clusterMeta[$primaryId].followers | Where-Object { [int] $_.id -eq $fidInt })
                    if ($followerEntries.Count -gt 0) {
                        if ($followerEntries[0].PSObject.Properties['title'] -and $followerEntries[0].title) {
                            $ftitle = [string] $followerEntries[0].title
                        }
                        if ($followerEntries[0].PSObject.Properties['similarity']) {
                            $fsim = [string] [Math]::Round([double] $followerEntries[0].similarity, 3)
                        }
                    }
                }
                $null = $sb.AppendLine("| #$fidInt | $ftitle | $fsim |")
            }
        }
    }

    $outPath = if ($env:RUNNER_TEMP) { Join-Path $env:RUNNER_TEMP 'run-summary.md' } else { Join-Path $RunArtifacts 'run-summary.md' }
    $sb.ToString() | Set-Content -LiteralPath $outPath -Encoding UTF8
    Write-Host "Wrote run summary -> $outPath"
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
    # Synthesize a 2-id cluster (primary 2711586 + follower 9999001) so the
    # expanded-follower-details section gets exercised. NOTE: must use
    # `-InputObject` with an explicitly wrapped outer array — PowerShell's
    # pipeline unwraps `@(@(2711586, 9999001))` into a flat `[2711586, 9999001]`
    # which would be parsed as two separate primaries instead of one cluster.
    ConvertTo-Json -Depth 5 -Compress -InputObject @(,@(2711586, 9999001)) | Set-Content -LiteralPath (Join-Path $aDir 'clusters.json') -Encoding UTF8
    @(
        [pscustomobject] @{
            primary   = [pscustomobject] @{ id = 2711586; title = 'VS crashes when opening pyproj' }
            followers = @(
                [pscustomobject] @{ id = 9999001; title = 'Visual Studio crashes opening Python project'; similarity = 0.42 }
            )
        }
    ) | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $aDir 'cluster-meta.json') -Encoding UTF8

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
        if ($content -notmatch '## Expanded follower details') { Write-Error 'summary missing expanded follower section'; $errors++ }
        if ($content -notmatch '9999001') { Write-Error 'expanded section missing follower id'; $errors++ }
        if ($content -notmatch 'Visual Studio crashes opening Python project') { Write-Error 'expanded section missing follower title'; $errors++ }
        if ($content -notmatch '0\.42') { Write-Error 'expanded section missing similarity score'; $errors++ }
    }

    # Pre-flight placeholder guard: if duplicateFieldName is still the
    # placeholder and a verdict requires the mirror leg, Invoke-Apply MUST
    # throw before any side effect (including the first GET).
    $env:DRY_RUN = 'false'
    $threw = $false
    try {
        Invoke-Apply -VerdictsDir $vDir -RunArtifacts $aDir -Config $cfg
    } catch {
        if ($_.Exception.Message -match 'duplicateFieldName') { $threw = $true }
    }
    if (-not $threw) {
        Write-Error 'Expected Invoke-Apply to throw on placeholder duplicateFieldName in non-dry-run.'
        $errors++
    }

    # Pre-flight: also reject a still-placeholder states.duplicate value.
    # Clone the config, fix the duplicate field name (so we get past the
    # first guard), but leave states.duplicate looking like '<...>' to
    # confirm the second guard fires.
    $cfgDupState = $cfg | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $cfgDupState.azdo.duplicateFieldName = 'Microsoft.VSTS.Common.DuplicateFeedbackTicketId'
    $cfgDupState.azdo.states.duplicate = '<DC-Closed-Duplicated-placeholder>'
    $threw = $false
    try {
        Invoke-Apply -VerdictsDir $vDir -RunArtifacts $aDir -Config $cfgDupState
    } catch {
        if ($_.Exception.Message -match 'states\.duplicate') { $threw = $true }
    }
    if (-not $threw) {
        Write-Error 'Expected Invoke-Apply to throw on placeholder states.duplicate in non-dry-run.'
        $errors++
    }

    # Pre-flight: reject a still-placeholder states.answered when an
    # answered verdict is queued.
    $vDirAns = Join-Path $PSScriptRoot 'tests\.tmp-verdicts-answered'
    Remove-Item -LiteralPath $vDirAns -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $vDirAns | Out-Null
    @{
        verdict = 'answered'; confidence = 0.9
        response_md = 'see the docs'
        github_issue_body_md = ''
        missing_info = @(); related_urls = @(); source_issue_for_resolution = ''
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $vDirAns 'verdict-2711586.json') -Encoding UTF8
    $cfgAnsState = $cfg | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $cfgAnsState.azdo.states.answered = '<AnsweredStatePlaceholder>'
    $threw = $false
    try {
        Invoke-Apply -VerdictsDir $vDirAns -RunArtifacts $aDir -Config $cfgAnsState
    } catch {
        if ($_.Exception.Message -match 'states\.answered') { $threw = $true }
    }
    if (-not $threw) {
        Write-Error 'Expected Invoke-Apply to throw on placeholder states.answered in non-dry-run.'
        $errors++
    }
    Remove-Item -LiteralPath $vDirAns -Recurse -Force -ErrorAction SilentlyContinue

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
