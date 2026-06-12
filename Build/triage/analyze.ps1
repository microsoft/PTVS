<#
    .SYNOPSIS
        Per-item driver: walks the sanitized Fetch artifact, builds a Copilot
        CLI prompt from Build/triage/prompts/triage-prompt.md, invokes
        `copilot -p`, captures the analysis markdown.

    .DESCRIPTION
        Called from the PTVS-Triage-Fetch AzDO pipeline after the sanitize
        step has written wi-<id>.json + diag-<id>.json files. For every
        wi-<id>.json under -SanitizedDir that does NOT have
        `sanitization_aborted = true`:
          1. Load wi-<id>.json + sibling diag-<id>.json (if present).
          2. Substitute {WI_ID}, {WI_URL}, {WI_JSON}, {DIAG_JSON} into the
             prompt template.
          3. Run `copilot -p <prompt> --add-dir <PtvsRepo> --allow-all-tools
              --no-color` with COPILOT_GITHUB_TOKEN in the environment
             (sourced from $(COPILOT_PAT) in the variable group; a GitHub
             fine-grained PAT with the `Copilot Requests` permission).
          4. Save stdout to -OutDir/analysis-<id>.md and stderr to
             -OutDir/.logs/copilot-<id>.log.

        On per-item copilot failure (non-zero exit, missing binary, timeout):
        write a stub analysis-<id>.md that records the failure reason and
        continue to the next item. One bad item must not kill the batch.

        Defense in depth: the script REFUSES to run on a wi-*.json whose
        `sanitization_aborted` is true (those should already have been
        deleted by fetch.yml's drop step; we re-check as belt-and-suspenders).

    .PARAMETER SanitizedDir
        Directory containing the wi-*.json + diag-*.json files written by
        sanitize.ps1. In the AzDO pipeline this is $(Agent.TempDirectory)/sanitized.

    .PARAMETER PtvsRepo
        Path to the PTVS checkout that Copilot CLI should be able to read for
        citations. Defaults to $env:BUILD_SOURCESDIRECTORY (AzDO) or
        $env:GITHUB_WORKSPACE (GitHub Actions) when not supplied explicitly.

    .PARAMETER OutDir
        Where to write analysis-<id>.md files. The .logs/ subdir gets per-item
        Copilot stderr captures. Created if missing.

    .PARAMETER PromptTemplate
        Path to the prompt template. Defaults to the sibling
        prompts/triage-prompt.md.

    .PARAMETER MaxItems
        Safety cap on number of items to process per invocation. Defaults to
        0 (no cap; process everything in SanitizedDir).

    .PARAMETER PerItemTimeoutSeconds
        Wall-clock cap on a single Copilot CLI invocation. Past that we kill
        the process, mark the item failed, and move on. Default 180 seconds.

    .PARAMETER CopilotPath
        Path/name of the copilot binary. Defaults to 'copilot' (PATH lookup).

    .PARAMETER ExtraCopilotArgs
        Optional additional arguments appended to every Copilot CLI invocation.

    .PARAMETER SelfTest
        Run inline smoke tests (no network, no copilot binary needed).
#>
[CmdletBinding()]
param(
    [string]   $SanitizedDir,
    [string]   $PtvsRepo,
    [string]   $OutDir,
    [string]   $PromptTemplate = (Join-Path $PSScriptRoot 'prompts/triage-prompt.md'),
    [int]      $MaxItems       = 0,
    [int]      $PerItemTimeoutSeconds = 180,
    [string]   $CopilotPath    = 'copilot',
    [string[]] $ExtraCopilotArgs = @(),
    [switch]   $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-OptionalProp {
    param([object] $Object, [string] $Name, $Default = $null)
    if ($null -eq $Object) { return $Default }
    if (-not $Object.PSObject.Properties[$Name]) { return $Default }
    $v = $Object.$Name
    if ($null -eq $v) { return $Default }
    return $v
}

function Get-PromptTemplate {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Prompt template not found: $Path"
    }
    return Get-Content -LiteralPath $Path -Raw
}

function Build-Prompt {
    # Substitutes {WI_ID}, {WI_URL}, {WI_JSON}, {DIAG_JSON} in the template.
    # JSON blobs go in verbatim — they have already passed sanitize.ps1.
    # Uses substring `.Replace()` not PowerShell's `-replace` so literal `$`
    # / `{` characters in JSON don't trigger regex / capture interpretation.
    param(
        [Parameter(Mandatory)] [string] $Template,
        [Parameter(Mandatory)] [string] $WiId,
        [Parameter(Mandatory)] [string] $WiUrl,
        [Parameter(Mandatory)] [string] $WiJson,
        [Parameter(Mandatory)] [string] $DiagJson
    )
    $r = $Template
    $r = $r.Replace('{WI_ID}',    $WiId)
    $r = $r.Replace('{WI_URL}',   $WiUrl)
    $r = $r.Replace('{WI_JSON}',  $WiJson)
    $r = $r.Replace('{DIAG_JSON}',$DiagJson)
    return $r
}

function Get-CandidatePairs {
    # Returns an array of [pscustomobject] with WiFile, DiagFile (may be $null),
    # Id (int), Wi (parsed object), Diag (parsed object or $null), Aborted (bool).
    # Sorted by id ascending so analyses post in a stable order on the issue.
    # Always returns a typed array (not $null, not a scalar) via the unary
    # comma operator — the PowerShell function-return pipeline would
    # otherwise unwrap a 0-element collection to $null or a 1-element
    # collection to the scalar element, breaking StrictMode `.Count`.
    param([Parameter(Mandatory)] [string] $Root)

    if (-not (Test-Path -LiteralPath $Root)) {
        throw "SanitizedDir not found: $Root"
    }

    $wiFiles = @(Get-ChildItem -LiteralPath $Root -Filter 'wi-*.json' -File -ErrorAction SilentlyContinue |
                 Sort-Object { try { [int] (($_.BaseName -split '-')[1]) } catch { 0 } })
    if ($wiFiles.Count -eq 0) {
        return ,([object[]] @())
    }

    $pairs = New-Object System.Collections.Generic.List[object]
    foreach ($wf in $wiFiles) {
        $wi = $null
        try { $wi = Get-Content -LiteralPath $wf.FullName -Raw | ConvertFrom-Json } catch {
            Write-Warning "Failed to parse $($wf.Name): $($_.Exception.Message). Skipping."
            continue
        }
        if (-not $wi -or -not $wi.PSObject.Properties['id']) {
            Write-Warning "$($wf.Name) has no .id; skipping."
            continue
        }
        $id = [int] $wi.id

        $aborted = $false
        if ($wi.PSObject.Properties['sanitization_aborted'] -and $wi.sanitization_aborted) {
            $aborted = $true
        }

        $diagFile = Join-Path $Root ("diag-{0}.json" -f $id)
        $diag = $null
        if (Test-Path -LiteralPath $diagFile) {
            try { $diag = Get-Content -LiteralPath $diagFile -Raw | ConvertFrom-Json } catch {
                Write-Warning "Failed to parse diag-$id.json: $($_.Exception.Message). Continuing without diag."
                $diag = $null
            }
        }

        $pairs.Add([pscustomobject] @{
            WiFile   = $wf.FullName
            DiagFile = if (Test-Path -LiteralPath $diagFile) { $diagFile } else { $null }
            Id       = $id
            Wi       = $wi
            Diag     = $diag
            Aborted  = $aborted
        }) | Out-Null
    }
    return ,$pairs.ToArray()
}

function Get-FailureStub {
    # Markdown body written when Copilot CLI failed for an item.
    #
    # The marker comment intentionally INCLUDES the BuildId after the id
    # (e.g. `<!-- ptvs-triage-analyze:5:failed:14400000 -->`) so it does
    # NOT collide with post-analyses.ps1's idempotency regex (which only
    # matches `<!-- ptvs-triage-analyze:\d+ -->`). Consequence: a
    # successful retry in a later run is NEVER blocked by a previous run's
    # failure-stub comment. The trade-off is that re-runs accumulate
    # distinct failure-stub comments on the issue — operator can manually
    # delete stale ones if the issue gets noisy.
    param(
        [Parameter(Mandatory)] [int]    $Id,
        [Parameter(Mandatory)] [string] $WiUrl,
        [Parameter(Mandatory)] [string] $Reason,
        [Parameter()]          [string] $BuildId = '0'
    )
    return @"
<!-- ptvs-triage-analyze:${Id}:failed:${BuildId} -->
### Triage analysis for [AzDO #$Id]($WiUrl)

**Root cause category:** Unable to determine
**Confidence:** Low

**Diagnosis**

_Automated triage failed for this item._

Reason: $Reason

This is an infrastructure failure, not a finding about the work item itself. A
maintainer can re-run the PTVS-Triage-Fetch pipeline to retry.

**Recommended maintainer action:** Investigate further
"@
}

function Find-LeakedTokenFingerprints {
    # Defense-in-depth against prompt-injection-induced secret leakage.
    # Scans the model's output for the LITERAL value of any token we passed
    # into the Copilot CLI invocation env. If a customer feedback ticket
    # contains a prompt-injection asking the model to dump env vars, the
    # model could in principle echo COPILOT_GITHUB_TOKEN or GH_TOKEN into
    # its stdout — which would then be posted as a public GitHub comment.
    # AzDO's automatic secret-masking applies to live job logs only, NOT to
    # files / artifact uploads / outbound REST calls, so we must scrub here.
    #
    # Returns an array of opaque fingerprints (`<token len=N head=X***>`)
    # for any tokens found in $Text. The function never returns the actual
    # secret value (or any substring long enough to reconstruct it) — the
    # fingerprint is for log-warning purposes only.
    param(
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $Text,
        [Parameter()]          [string[]] $Tokens = @()
    )
    if ([string]::IsNullOrEmpty($Text) -or -not $Tokens) { return ,([string[]] @()) }
    $found = New-Object System.Collections.Generic.List[string]
    foreach ($t in $Tokens) {
        if ([string]::IsNullOrEmpty($t)) { continue }
        # Ignore short / placeholder values: a real fine-grained PAT is ≥80
        # chars; an unresolved macro is 14 chars; the 30-char floor catches
        # only realistic tokens without false-positiving on every Foo / Bar.
        if ($t.Length -lt 30) { continue }
        if ($Text.Contains($t)) {
            $found.Add(("<token len={0} head={1}***>" -f $t.Length, $t.Substring(0, 1))) | Out-Null
        }
    }
    # Return a typed [string[]] array. The PowerShell return pipeline will
    # unwrap an [string[]] cleanly to the call site WITHOUT collapsing 0/1
    # element collections to $null / scalar (which IS what `List<T>` would
    # have done if returned directly via `$found`). Call sites should still
    # NOT wrap this in `@(...)` because `@(<array>)` only enumerates the
    # outermost layer and would produce a 1-element Object[] containing the
    # typed array. Pattern: `$x = Find-LeakedTokenFingerprints ...; $x.Count`
    # is safe (Count works on [string[]] of any length including 0).
    return ,$found.ToArray()
}

function Get-AnalysisFromCopilotStdOut {
    # Strips the Copilot CLI's non-interactive "trace" preamble from raw
    # stdout, returning just the model's published analysis with the
    # idempotency marker as the first line.
    #
    # Copilot CLI's non-interactive mode (`-p`) writes per-tool-call
    # trace lines to stdout BEFORE the model's actual reply, e.g.:
    #     ● Search (grep)
    #       │ "ReturnCode" in **/*.cs
    #       └ 3 files found
    #     ● Check duplicate issue details (shell)
    #       │ gh issue view 7249 ...
    #       └ 2 lines...
    #     Now I have enough context to write the triage analysis.
    #     <!-- ptvs-triage-analyze:N -->
    #     ### Triage analysis for [AzDO #N](...)
    #     ...
    # That preamble is meant for an interactive terminal, not a public
    # GitHub issue comment. Strategy: find the LAST occurrence of the
    # mandated heading `### Triage analysis for [AzDO #<Id>]` and
    # discard everything before it. The heading shape is dictated by
    # the prompt template and is structurally distinct from any tool-
    # trace line, so anchoring on it is safe. We use the LAST occurrence
    # in case the trace preamble itself happened to quote the heading.
    #
    # The function then prepends the idempotency marker — Copilot is
    # told to emit the marker BEFORE the heading, but we sliced from the
    # heading onward, so we always re-add it. Re-prepending unconditionally
    # also covers the rare case where the model omitted the marker entirely.
    #
    # If the heading is not found at all (model ignored the template),
    # return the raw stdout with the marker prepended and emit a warning;
    # let the maintainer notice the formatting drift on the posted issue.
    param(
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $RawStdOut,
        [Parameter(Mandatory)] [int]                         $Id
    )
    $marker = "<!-- ptvs-triage-analyze:$Id -->"
    if ([string]::IsNullOrWhiteSpace($RawStdOut)) {
        return $marker
    }
    $headingRegex = [regex] ('(?m)^[ \t]*###[ \t]+Triage analysis for \[AzDO #' + [regex]::Escape([string] $Id) + '\]')
    $headingHits  = $headingRegex.Matches($RawStdOut)
    if ($headingHits.Count -gt 0) {
        $lastHit = $headingHits[$headingHits.Count - 1]
        $body    = $RawStdOut.Substring($lastHit.Index).TrimEnd()
    } else {
        Write-Warning "Heading '### Triage analysis for [AzDO #$Id]' not found in Copilot output for #${Id}; posting raw stdout."
        $body = $RawStdOut.TrimEnd()
    }
    return "$marker`n" + $body.TrimStart()
}

function Invoke-CopilotOnce {
    # Runs `copilot -p <prompt> --add-dir <repo> --allow-all-tools --no-color`
    # with a per-invocation wall-clock timeout. Returns a hashtable:
    #   @{ ExitCode = int; StdOut = string; StdErr = string; TimedOut = bool }
    #
    # IMPORTANT: do NOT use `Start-Process -ArgumentList` here. PowerShell's
    # Start-Process serializes ArgumentList back to a single
    # ProcessStartInfo.Arguments string, and on Linux the receiving process
    # re-splits that string on whitespace — so any argument that contains
    # spaces or special chars gets mangled. Production build 14364610 had
    # all 12 items fail with:
    #     error: unknown option '-->`'
    # because the prompt contained the marker `<!-- ptvs-triage-analyze:N -->`
    # immediately followed by a backtick, and the split-on-space behaviour
    # treated `-->\`` as a CLI option name.
    #
    # ProcessStartInfo.ArgumentList (a Collection<string> on .NET Core 3.0+)
    # passes each entry through to execve without round-tripping through
    # the Arguments string. This is the only safe way to pass multi-word /
    # special-character arguments cross-platform from pwsh.
    #
    # See https://github.com/PowerShell/PowerShell/issues/13089 for the
    # underlying PowerShell behaviour.
    param(
        [Parameter(Mandatory)] [string]   $CopilotPath,
        [Parameter(Mandatory)] [string]   $Prompt,
        [Parameter(Mandatory)] [string]   $WorkDir,
        [Parameter(Mandatory)] [string]   $AddDir,
        [Parameter()]          [string[]] $ExtraArgs = @(),
        [Parameter(Mandatory)] [int]      $TimeoutSeconds
    )

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName               = $CopilotPath
    $psi.WorkingDirectory       = $WorkDir
    $psi.UseShellExecute        = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.CreateNoWindow         = $true
    [void] $psi.ArgumentList.Add('-p')
    [void] $psi.ArgumentList.Add($Prompt)
    [void] $psi.ArgumentList.Add('--add-dir')
    [void] $psi.ArgumentList.Add($AddDir)
    [void] $psi.ArgumentList.Add('--allow-all-tools')
    [void] $psi.ArgumentList.Add('--no-color')
    if ($ExtraArgs) {
        foreach ($a in $ExtraArgs) {
            if ($null -ne $a) { [void] $psi.ArgumentList.Add([string] $a) }
        }
    }

    $proc = [System.Diagnostics.Process]::new()
    $proc.StartInfo = $psi
    [void] $proc.Start()

    # Read stdout/stderr asynchronously: if the child writes more than the
    # pipe buffer (~64KB on Linux) and we don't drain it, the child will
    # block on write and we'll hit our timeout instead of getting a real
    # exit. Kicking off ReadToEndAsync BEFORE WaitForExit guarantees both
    # streams drain in parallel with the child running.
    $stdoutTask = $proc.StandardOutput.ReadToEndAsync()
    $stderrTask = $proc.StandardError.ReadToEndAsync()

    $exited   = $proc.WaitForExit($TimeoutSeconds * 1000)
    $timedOut = -not $exited
    if ($timedOut) {
        try { $proc.Kill($true) } catch { try { $proc.Kill() } catch { } }
        try { $proc.WaitForExit(5000) | Out-Null } catch { }
    }

    $stdout = ''
    $stderr = ''
    try { $stdout = $stdoutTask.GetAwaiter().GetResult() } catch { $stdout = '' }
    try { $stderr = $stderrTask.GetAwaiter().GetResult() } catch { $stderr = '' }

    $code = if ($timedOut) { 124 } else { [int] $proc.ExitCode }
    $proc.Dispose()

    return @{
        ExitCode = $code
        StdOut   = ([string] $stdout)
        StdErr   = ([string] $stderr)
        TimedOut = $timedOut
    }
}

function Invoke-Analyze {
    param(
        [Parameter(Mandatory)] [string]   $SanitizedDir,
        [Parameter(Mandatory)] [string]   $PtvsRepo,
        [Parameter(Mandatory)] [string]   $OutDir,
        [Parameter(Mandatory)] [string]   $PromptTemplate,
        [int]      $MaxItems       = 0,
        [int]      $PerItemTimeoutSeconds = 180,
        [string]   $CopilotPath    = 'copilot',
        [string[]] $ExtraCopilotArgs = @()
    )

    # AzDO leaves the literal `$(VarName)` in the env var when the named
    # variable doesn't exist in any visible scope (variable group not linked,
    # name typo, etc). A null-or-whitespace check alone treats the 14-char
    # placeholder as a real value and silently 401s downstream. Same
    # fingerprint detection as the smoke step used.
    $token = $env:COPILOT_GITHUB_TOKEN
    $isUnresolvedMacro = ($token -and ($token -match '^\$\([A-Za-z_][A-Za-z0-9_.]*\)$'))
    if ([string]::IsNullOrWhiteSpace($token) -or $isUnresolvedMacro) {
        if ($isUnresolvedMacro) {
            throw "COPILOT_GITHUB_TOKEN resolved to the literal macro '$token'. The COPILOT_PAT variable group entry does not exist in any visible scope; check that PTVS-Triage-Bridge contains COPILOT_PAT and that this pipeline is authorized to use the variable group."
        }
        throw "COPILOT_GITHUB_TOKEN is required (a GitHub fine-grained PAT with the Copilot Requests permission; in the AzDO pipeline this comes from the COPILOT_PAT secret variable in PTVS-Triage-Bridge)."
    }
    if (-not (Test-Path -LiteralPath $PtvsRepo)) {
        throw "PtvsRepo not found: $PtvsRepo"
    }
    if (-not (Get-Command $CopilotPath -ErrorAction SilentlyContinue)) {
        throw "copilot binary not found on PATH ('$CopilotPath'). Did the 'Install Copilot CLI' step succeed?"
    }

    $template = Get-PromptTemplate -Path $PromptTemplate
    foreach ($k in @('{WI_ID}', '{WI_URL}', '{WI_JSON}', '{DIAG_JSON}')) {
        if ($template -notlike "*$k*") {
            throw "Prompt template at $PromptTemplate is missing required placeholder $k."
        }
    }

    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    $logDir = Join-Path $OutDir '.logs'
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null

    # Get-CandidatePairs returns a typed array via `,(...)` (unary comma)
    # so its shape is guaranteed — do NOT wrap in @() here (which would
    # produce a 1-element Object[] wrapping the inner array).
    $pairs = Get-CandidatePairs -Root $SanitizedDir
    if ($pairs.Count -eq 0) {
        Write-Warning "No wi-*.json files found under $SanitizedDir. Nothing to analyze."
        return
    }

    # Filter aborted items (belt-and-suspenders; Fetch already drops these).
    # @(...) wrap defends against the 0-or-1-result Where-Object collapse:
    # PowerShell would otherwise unwrap a 1-element pipeline output to a
    # scalar, breaking `.Count` and the `Select-Object -First $MaxItems`
    # path under StrictMode.
    $eligible = @($pairs | Where-Object { -not $_.Aborted })
    $skipped  = @($pairs | Where-Object { $_.Aborted })
    foreach ($s in $skipped) {
        Write-Warning "Skipping AzDO #$($s.Id): sanitization_aborted=true (should have been dropped upstream)."
    }
    if ($MaxItems -gt 0 -and $eligible.Count -gt $MaxItems) {
        Write-Host "Truncating eligible set to -MaxItems=$MaxItems (was $($eligible.Count))."
        $eligible = @($eligible | Select-Object -First $MaxItems)
    }

    Write-Host "Analyzing $($eligible.Count) item(s); $($skipped.Count) skipped as aborted."
    $okCount = 0; $failCount = 0
    # BuildId is propagated into failure-stub markers so re-runs that
    # succeed are NOT blocked by an earlier run's failure-stub comment.
    # Use AzDO's BUILD_BUILDID; fall back to a process-unique value so
    # local dev still produces distinct stubs across invocations.
    $buildId = if ($env:BUILD_BUILDID) { $env:BUILD_BUILDID } else { ([Guid]::NewGuid().ToString('N')).Substring(0, 8) }
    foreach ($p in $eligible) {
        $id     = $p.Id
        $wiUrl  = [string] (Get-OptionalProp -Object $p.Wi -Name 'url' -Default '')
        $wiJson = ($p.Wi   | ConvertTo-Json -Depth 25)
        # diag-<id>.json is optional — when none was emitted (shouldn't happen
        # in the current Fetch pipeline, but the schema allows it), embed an
        # explicit "no diagnostics" marker so the model knows the data is
        # missing, not just empty.
        $diagJson = if ($p.Diag) { $p.Diag | ConvertTo-Json -Depth 25 } else { '{"note":"no diag-*.json was emitted for this work item"}' }

        $prompt = Build-Prompt -Template $template `
                               -WiId    ([string] $id) `
                               -WiUrl   $wiUrl `
                               -WiJson  $wiJson `
                               -DiagJson $diagJson

        $analysisPath = Join-Path $OutDir ("analysis-{0}.md" -f $id)
        $logPath      = Join-Path $logDir ("copilot-{0}.log"  -f $id)

        Write-Host ""
        Write-Host "==> [$($okCount + $failCount + 1)/$($eligible.Count)] Analyzing AzDO #$id (timeout ${PerItemTimeoutSeconds}s)..."
        $start = Get-Date
        $r = $null
        try {
            $r = Invoke-CopilotOnce -CopilotPath $CopilotPath -Prompt $prompt -WorkDir $PtvsRepo `
                                     -AddDir $PtvsRepo -ExtraArgs $ExtraCopilotArgs -TimeoutSeconds $PerItemTimeoutSeconds
        } catch {
            $reason = "Invocation threw: $($_.Exception.Message)"
            Set-Content -LiteralPath $analysisPath -Value (Get-FailureStub -Id $id -WiUrl $wiUrl -Reason $reason -BuildId $buildId) -Encoding UTF8
            Set-Content -LiteralPath $logPath      -Value $reason -Encoding UTF8
            Write-Warning "  FAILED: $reason"
            $failCount++
            continue
        }
        $dur = [int] ((Get-Date) - $start).TotalSeconds

        # Always write the stderr log for post-mortem regardless of success.
        # Run the same leak scrub as on the analysis output: Copilot CLI
        # itself could in principle echo a token in an error message, and
        # the .logs/ dir ships in the public-internal audit artifact.
        if ($r.StdErr) {
            $errText = [string] $r.StdErr
            # @(...) forces array shape: a `Where | Sort` pipeline yielding
            # a single match would otherwise collapse to a scalar string
            # under StrictMode, and `.Count` on a string throws.
            $envTokensErr = @(@($env:COPILOT_GITHUB_TOKEN, $env:GH_TOKEN, $env:GITHUB_TOKEN) |
                              Where-Object { -not [string]::IsNullOrEmpty($_) -and $_.Length -ge 30 } |
                              Sort-Object Length -Descending)
            # Note: Find-LeakedTokenFingerprints returns a typed [string[]]
            # via `return ,$arr.ToArray()` — DO NOT wrap in @(...) here, or
            # the typed array becomes a 1-element Object[] wrapping it.
            $errLeak = Find-LeakedTokenFingerprints -Text $errText -Tokens $envTokensErr
            if ($errLeak.Count -gt 0) {
                Write-Warning "  SECURITY: stderr for #${id} contained leaked token(s) $($errLeak -join ', '); redacting before write."
                foreach ($tok in $envTokensErr) {
                    $errText = $errText.Replace($tok, '<redacted-leaked-secret>')
                }
            }
            Set-Content -LiteralPath $logPath -Value $errText -Encoding UTF8
        }

        if ($r.TimedOut) {
            $reason = "Copilot CLI exceeded per-item timeout of ${PerItemTimeoutSeconds}s."
            Set-Content -LiteralPath $analysisPath -Value (Get-FailureStub -Id $id -WiUrl $wiUrl -Reason $reason -BuildId $buildId) -Encoding UTF8
            Write-Warning "  TIMEOUT after ${dur}s"
            $failCount++
            continue
        }
        if ($r.ExitCode -ne 0) {
            $reason = "Copilot CLI exited with code $($r.ExitCode). See .logs/copilot-$id.log for details."
            Set-Content -LiteralPath $analysisPath -Value (Get-FailureStub -Id $id -WiUrl $wiUrl -Reason $reason -BuildId $buildId) -Encoding UTF8
            Write-Warning "  FAILED with exit $($r.ExitCode) after ${dur}s"
            $failCount++
            continue
        }
        if ([string]::IsNullOrWhiteSpace($r.StdOut)) {
            $reason = "Copilot CLI exited 0 but produced empty stdout (no analysis content)."
            Set-Content -LiteralPath $analysisPath -Value (Get-FailureStub -Id $id -WiUrl $wiUrl -Reason $reason -BuildId $buildId) -Encoding UTF8
            Write-Warning "  EMPTY OUTPUT after ${dur}s"
            $failCount++
            continue
        }
        # Strip the Copilot CLI's non-interactive trace preamble (tool-call
        # bullets, box-drawing chars, mid-stream commentary). See
        # Get-AnalysisFromCopilotStdOut for full rationale.
        $output = Get-AnalysisFromCopilotStdOut -RawStdOut ([string] $r.StdOut) -Id $id

        # Defense against prompt-injection-induced token leakage: if the
        # model's output contains the literal value of any token we passed
        # in via env, redact it before the analysis ever touches disk (and
        # therefore before it can be uploaded as an artifact or posted as
        # a public GitHub comment). See Find-LeakedTokenFingerprints.
        # Tokens sorted by length DESCENDING so a longer token that
        # contains a shorter one as a substring is fully redacted before
        # the shorter-token iteration runs (otherwise the substring scrub
        # would mutate the longer token mid-text and leave a partial leak).
        # @(...) wraps force array shape: a 0-element or 1-element pipeline
        # result would otherwise unwrap to $null / scalar under StrictMode
        # and crash on the next `.Count` access.
        $envTokens = @(@($env:COPILOT_GITHUB_TOKEN, $env:GH_TOKEN, $env:GITHUB_TOKEN) |
                       Where-Object { -not [string]::IsNullOrEmpty($_) -and $_.Length -ge 30 } |
                       Sort-Object Length -Descending)
        # Note: DO NOT wrap in @(...) — see analogous comment in stderr scrub.
        $leakedFingerprints = Find-LeakedTokenFingerprints -Text $output -Tokens $envTokens
        if ($leakedFingerprints.Count -gt 0) {
            Write-Warning "  SECURITY: analysis for #${id} contained leaked token(s) $($leakedFingerprints -join ', '); redacting before write."
            foreach ($tok in $envTokens) {
                $output = $output.Replace($tok, '<redacted-leaked-secret>')
            }
        }

        Set-Content -LiteralPath $analysisPath -Value $output -Encoding UTF8
        Write-Host "  OK in ${dur}s -> $analysisPath"
        $okCount++
    }

    Write-Host ""
    Write-Host "Analyses complete: $okCount succeeded, $failCount failed, $($skipped.Count) skipped-aborted."
    if ($okCount -eq 0 -and $failCount -gt 0) {
        # Total wipeout is worth signaling loudly — likely auth, quota, or
        # binary issue. But don't fail the step; the analyses artifact still
        # carries useful triage stubs for the operator.
        Write-Warning "Zero successful analyses in this run. Check .logs/copilot-*.log for the failure pattern (auth? quota? network?)."
    }
}

# ──────────────────────────────────────────────────────────────────────
function Invoke-SelfTest {
    $errors = 0

    # 1. Build-Prompt: substitutes all four placeholders, preserves verbatim
    #    JSON content (including literal `$` and `{` chars that would otherwise
    #    confuse regex-style substitution).
    $tpl = "id={WI_ID} url={WI_URL}`nwi:`n{WI_JSON}`ndiag:`n{DIAG_JSON}"
    $json = '{"a":"$has dollar","b":"{nested}","c":1}'
    $p = Build-Prompt -Template $tpl -WiId '42' -WiUrl 'https://example/42' -WiJson $json -DiagJson '{"d":null}'
    if ($p -notmatch 'id=42 url=https://example/42') { Write-Error "Build-Prompt did not substitute id/url. Got: $p"; $errors++ }
    if ($p -notmatch [regex]::Escape('"$has dollar"')) { Write-Error "Build-Prompt mangled literal `$ in JSON. Got: $p"; $errors++ }
    if ($p -notmatch [regex]::Escape('"{nested}"')) { Write-Error "Build-Prompt mangled literal {} in JSON. Got: $p"; $errors++ }
    if ($p -notmatch [regex]::Escape('{"d":null}')) { Write-Error "Build-Prompt didn't include diag JSON. Got: $p"; $errors++ }

    # 2. Get-CandidatePairs: walks dir, parses JSON, sorts by id, marks
    #    aborted items, attaches diag when present, no diag → property = $null.
    #    Returns a typed [object[]] via `,(...)` — call sites must NOT wrap
    #    in @(...) (would produce 1-elem array wrapping the inner array).
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ("analyze-test-{0}" -f ([Guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    try {
        # Three items: clean, aborted, no-diag.
        @{ id = 3; title = 'three'; url = 'u3' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $tmp 'wi-3.json') -Encoding UTF8
        @{ work_item = @{ id = 3; title = 'three' } } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $tmp 'diag-3.json') -Encoding UTF8
        @{ id = 1; title = 'one';   url = 'u1'; sanitization_aborted = $true } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $tmp 'wi-1.json') -Encoding UTF8
        @{ id = 2; title = 'two';   url = 'u2' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $tmp 'wi-2.json') -Encoding UTF8
        # No diag for #2 — exercises the missing-diag path.

        $pairs = Get-CandidatePairs -Root $tmp
        if ($pairs.Count -ne 3) { Write-Error "Expected 3 pairs, got $($pairs.Count)"; $errors++ }
        if ($pairs[0].Id -ne 1 -or $pairs[1].Id -ne 2 -or $pairs[2].Id -ne 3) {
            Write-Error "Pairs not sorted by id: $($pairs.Id -join ',')"; $errors++
        }
        if (-not $pairs[0].Aborted)   { Write-Error "Aborted flag not set for wi-1.json"; $errors++ }
        if ($pairs[1].Aborted)        { Write-Error "Aborted flag wrongly set for wi-2.json"; $errors++ }
        if ($pairs[2].Diag -eq $null) { Write-Error "Diag for wi-3 should have been parsed."; $errors++ }
        if ($pairs[1].Diag -ne $null) { Write-Error "Diag for wi-2 should be `$null (no diag file present)."; $errors++ }
    } finally {
        Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }

    # 2b. Get-CandidatePairs shape regression: empty-dir and 1-item cases
    #     must return real arrays whose .Count works under StrictMode (not
    #     $null and not collapsed scalar).
    $emptyDir = Join-Path ([IO.Path]::GetTempPath()) ("analyze-empty-{0}" -f ([Guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Force -Path $emptyDir | Out-Null
    try {
        $emptyPairs = Get-CandidatePairs -Root $emptyDir
        if ($null -eq $emptyPairs)         { Write-Error "Get-CandidatePairs on empty dir returned `$null"; $errors++ }
        elseif ($emptyPairs.Count -ne 0)   { Write-Error "Get-CandidatePairs on empty dir Count=$($emptyPairs.Count), expected 0"; $errors++ }
        # Single-item: must NOT collapse to scalar PSCustomObject.
        @{ id = 99; title = 'solo'; url = 'u99' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $emptyDir 'wi-99.json') -Encoding UTF8
        $solo = Get-CandidatePairs -Root $emptyDir
        if ($null -eq $solo)               { Write-Error "Get-CandidatePairs on 1-item dir returned `$null"; $errors++ }
        elseif ($solo.Count -ne 1)         { Write-Error "Get-CandidatePairs on 1-item dir Count=$($solo.Count), expected 1"; $errors++ }
        elseif ($solo[0].Id -ne 99)        { Write-Error "Get-CandidatePairs 1-item Id mismatch: $($solo[0].Id)"; $errors++ }
    } finally {
        Remove-Item -LiteralPath $emptyDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    # 3. Get-FailureStub: marker comment is the FIRST line and INCLUDES
    #    the BuildId so it does not collide with the success-marker
    #    regex used by post-analyses.ps1 for idempotency. A successful
    #    retry must never be blocked by a previous run's failure stub.
    $stub = Get-FailureStub -Id 999 -WiUrl 'https://example/999' -Reason 'quota exhausted' -BuildId '14400000'
    $firstLine = ($stub -split "`r?`n")[0]
    if ($firstLine -ne '<!-- ptvs-triage-analyze:999:failed:14400000 -->') {
        Write-Error "Failure stub first line should be the BuildId-suffixed marker; got: $firstLine"; $errors++
    }
    if ($stub -notmatch 'quota exhausted') { Write-Error "Failure stub should embed the reason."; $errors++ }
    if ($stub -notmatch 'AzDO #999')       { Write-Error "Failure stub should reference the AzDO id."; $errors++ }
    if ($stub -notmatch '\*\*Root cause category:\*\* Unable to determine') { Write-Error "Failure stub missing category."; $errors++ }
    # Critical: post-analyses.ps1's marker regex must NOT match a failure-stub marker.
    if ($firstLine -match '^<!--\s*ptvs-triage-analyze:\d+\s*-->$') {
        Write-Error "Failure-stub marker MATCHES the success regex — successful retries would be blocked."; $errors++
    }
    # And the default BuildId='0' still produces a distinct marker.
    $stub0 = Get-FailureStub -Id 1 -WiUrl 'https://example/1' -Reason 'x'
    if (($stub0 -split "`r?`n")[0] -ne '<!-- ptvs-triage-analyze:1:failed:0 -->') {
        Write-Error "Failure stub with default BuildId malformed."; $errors++
    }

    # 4. Get-OptionalProp: defensive read; returns default on absent or $null.
    $o = [pscustomobject] @{ a = 'x'; b = $null }
    if ((Get-OptionalProp -Object $o -Name 'a')             -ne 'x') { Write-Error 'Optional present-prop read broken.'; $errors++ }
    if ((Get-OptionalProp -Object $o -Name 'b' -Default 'd') -ne 'd') { Write-Error 'Optional null-value should yield default.'; $errors++ }
    if ((Get-OptionalProp -Object $o -Name 'nope')          -ne $null){ Write-Error 'Optional absent-prop should yield $null.'; $errors++ }

    # 5. Prompt template (the real one on disk) must contain all four
    #    placeholders. Catches accidental deletions before they hit prod.
    $realTpl = Join-Path $PSScriptRoot 'prompts/triage-prompt.md'
    if (Test-Path -LiteralPath $realTpl) {
        $tplText = Get-Content -LiteralPath $realTpl -Raw
        foreach ($k in @('{WI_ID}','{WI_URL}','{WI_JSON}','{DIAG_JSON}')) {
            if ($tplText -notlike "*$k*") {
                Write-Error "Live prompt template missing placeholder $k."; $errors++
            }
        }
        # Marker MUST be in the template's output section so the model is
        # nudged to emit it. Defense-in-depth Invoke-Analyze also prepends it.
        if ($tplText -notmatch '<!--\s*ptvs-triage-analyze:\{WI_ID\}\s*-->') {
            Write-Error "Live prompt template missing idempotency marker in the output section."; $errors++
        }
    } else {
        Write-Warning "Real prompt template not present at $realTpl (skipping template check)."
    }

    # 5b. Invoke-Analyze guard: refuses to run when COPILOT_GITHUB_TOKEN is
    #     either missing or the literal AzDO macro `$(COPILOT_PAT)` (which
    #     means the variable group is not linked / variable not present).
    #     This is the same fingerprint detection the smoke step proved out.
    $tmpSan = Join-Path ([IO.Path]::GetTempPath()) ("analyze-tok-{0}" -f ([Guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Force -Path $tmpSan | Out-Null
    try {
        @{ id = 1; title = 't'; url = 'u' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $tmpSan 'wi-1.json') -Encoding UTF8
        $origTok = $env:COPILOT_GITHUB_TOKEN
        try {
            $env:COPILOT_GITHUB_TOKEN = $null
            $threwOnEmpty = $false; $emptyMsg = ''
            try { Invoke-Analyze -SanitizedDir $tmpSan -PtvsRepo $tmpSan -OutDir $tmpSan -PromptTemplate (Join-Path $PSScriptRoot 'prompts/triage-prompt.md') } catch { $threwOnEmpty = $true; $emptyMsg = $_.Exception.Message }
            if (-not $threwOnEmpty)                                { Write-Error "Invoke-Analyze should throw when COPILOT_GITHUB_TOKEN is empty."; $errors++ }
            elseif ($emptyMsg -notmatch 'COPILOT_GITHUB_TOKEN is required') { Write-Error "Empty-token throw should mention 'COPILOT_GITHUB_TOKEN is required'; got: $emptyMsg"; $errors++ }

            $env:COPILOT_GITHUB_TOKEN = '$(COPILOT_PAT)'
            $threwOnMacro = $false; $macroMsg = ''
            try { Invoke-Analyze -SanitizedDir $tmpSan -PtvsRepo $tmpSan -OutDir $tmpSan -PromptTemplate (Join-Path $PSScriptRoot 'prompts/triage-prompt.md') } catch { $threwOnMacro = $true; $macroMsg = $_.Exception.Message }
            if (-not $threwOnMacro)                                { Write-Error "Invoke-Analyze should throw on unresolved AzDO macro."; $errors++ }
            elseif ($macroMsg -notmatch 'literal macro')           { Write-Error "Macro throw should mention 'literal macro'; got: $macroMsg"; $errors++ }
        } finally { $env:COPILOT_GITHUB_TOKEN = $origTok }
    } finally {
        Remove-Item -LiteralPath $tmpSan -Recurse -Force -ErrorAction SilentlyContinue
    }

    # 5c. Find-LeakedTokenFingerprints: returns fingerprints (never the raw
    #     token value) when output contains a token. Ignores tokens that
    #     are too short to be real secrets. Empty / no-tokens => empty.
    #     The function returns a typed [string[]] via `return ,$arr.ToArray()`
    #     so call sites can use `.Count` directly without an @(...) wrap
    #     (which would produce a 1-element Object[] containing the array).
    $realLookingToken = 'ghp_' + ('A' * 36)   # 40 chars, matches PAT shape, >=30 floor
    $textClean        = 'This analysis mentions no secrets.'
    $textLeak         = "Token starts with: $realLookingToken"
    $fpClean = Find-LeakedTokenFingerprints -Text $textClean -Tokens @($realLookingToken)
    if ($fpClean.Count -ne 0) { Write-Error "Clean text reported leak: $($fpClean -join ',')"; $errors++ }
    $fpLeak = Find-LeakedTokenFingerprints -Text $textLeak -Tokens @($realLookingToken)
    if ($fpLeak.Count -ne 1) { Write-Error "Leaking text should report exactly one fingerprint; got $($fpLeak.Count)"; $errors++ }
    if ($fpLeak.Count -gt 0 -and $fpLeak[0] -match [regex]::Escape($realLookingToken)) {
        Write-Error "Fingerprint MUST NOT echo the token value: $($fpLeak[0])"; $errors++
    }
    if ($fpLeak.Count -gt 0 -and $fpLeak[0] -notmatch 'len=40') {
        Write-Error "Fingerprint should include length; got: $($fpLeak[0])"; $errors++
    }
    # Short / empty / null inputs ignored
    $fpShort = Find-LeakedTokenFingerprints -Text 'shortvalue' -Tokens @('short')
    if ($fpShort.Count -ne 0) { Write-Error "Short tokens (<30 chars) should be ignored to avoid false-positives"; $errors++ }
    $fpEmptyInput = Find-LeakedTokenFingerprints -Text '' -Tokens @($realLookingToken)
    if ($fpEmptyInput.Count -ne 0) { Write-Error "Empty text should not report leaks"; $errors++ }
    $fpNoTokens = Find-LeakedTokenFingerprints -Text $textLeak -Tokens @()
    if ($fpNoTokens.Count -ne 0) { Write-Error "Empty token list should not report leaks"; $errors++ }

    # 5e. Regression: pipeline-collapse scenario.
    #     A `@(a,b,c) | Where | Sort-Object` pipeline collapses to a SCALAR
    #     string when only ONE element survives the filter. Under StrictMode,
    #     `.Count` on a string throws and any caller that doesn't defend
    #     against this crashes. This reproduces production failure 14364420
    #     where COPILOT_GITHUB_TOKEN was the only env token long enough to
    #     pass the 30-char floor.
    $longTokForCollapse = 'github_pat_' + ('Z' * 80)
    $collapsed = @($longTokForCollapse, $null, '') |
                 Where-Object { -not [string]::IsNullOrEmpty($_) -and $_.Length -ge 30 } |
                 Sort-Object Length -Descending
    # The pipeline produces a scalar string; the @(...) wrap MUST repair
    # the array shape before any downstream .Count or foreach.
    $wrapped = @($collapsed)
    if ($wrapped.Count -ne 1) {
        Write-Error "Pipeline-collapse repair: expected 1, got $($wrapped.Count)"; $errors++
    }
    # Find-LeakedTokenFingerprints must accept a (possibly scalar) -Tokens
    # arg and return a typed [string[]] whose .Count never throws.
    $fpRegression = Find-LeakedTokenFingerprints -Text 'no leak here' -Tokens $wrapped
    try {
        $null = $fpRegression.Count
    } catch {
        Write-Error "Regression: Find-LeakedTokenFingerprints return value .Count threw: $($_.Exception.Message)"; $errors++
    }
    # Same test for the scalar-passed-directly case (caller forgot @()):
    $fpRegression2 = Find-LeakedTokenFingerprints -Text 'no leak here' -Tokens $collapsed
    try {
        $null = $fpRegression2.Count
    } catch {
        Write-Error "Regression: Find-LeakedTokenFingerprints accepting scalar -Tokens, .Count threw: $($_.Exception.Message)"; $errors++
    }

    # 5f. Get-AnalysisFromCopilotStdOut: trace-preamble strip and marker
    #     normalization. Production build 14364772 posted Copilot CLI's
    #     interactive trace lines (`● Search (grep)`, `│`, `└`, mid-stream
    #     commentary like "Now I have enough context...") to public GitHub
    #     issues because the analysis emitter only checked for a marker
    #     at the start of stdout, not for the actual heading. This unit-
    #     tests the helper directly so the contract is verified on Windows
    #     too (the full E2E only runs on Linux/macOS).
    $bulletChar = [char] 0x25CF
    $boxV       = [char] 0x2502
    $boxL       = [char] 0x2514
    # Case A: heading present after a typical trace preamble.
    $rawA = @"
$bulletChar Search (grep)
  $boxV "ReturnCode" in **/*.cs
  $boxL 3 files found

$bulletChar Check duplicate issue details (shell)
  $boxV gh issue view 7249 --repo microsoft/PTVS
  $boxL 24 lines...

Now I have enough context to write the triage analysis.

<!-- ptvs-triage-analyze:42 -->
### Triage analysis for [AzDO #42](https://example/42)

**Root cause category:** PTVS
**Confidence:** Medium

body text
"@
    $cleanA = Get-AnalysisFromCopilotStdOut -RawStdOut $rawA -Id 42
    if (-not $cleanA.StartsWith('<!-- ptvs-triage-analyze:42 -->'))               { Write-Error "Strip A: marker not at start. Got: $cleanA"; $errors++ }
    if ($cleanA.Contains($bulletChar))                                            { Write-Error "Strip A: bullet '$bulletChar' survived. Got: $cleanA"; $errors++ }
    if ($cleanA.Contains($boxV))                                                  { Write-Error "Strip A: box-V '$boxV' survived. Got: $cleanA"; $errors++ }
    if ($cleanA.Contains('Now I have enough context'))                            { Write-Error "Strip A: mid-stream commentary survived. Got: $cleanA"; $errors++ }
    if ($cleanA.Contains('gh issue view 7249'))                                   { Write-Error "Strip A: shell command leaked. Got: $cleanA"; $errors++ }
    if (([regex]::Matches($cleanA, '<!-- ptvs-triage-analyze:42 -->')).Count -ne 1) {
        Write-Error "Strip A: marker should appear exactly once. Got: $cleanA"; $errors++
    }
    if ($cleanA -notmatch '<!-- ptvs-triage-analyze:42 -->\s*\n###\s+Triage analysis for') {
        Write-Error "Strip A: marker not immediately followed by heading. Got: $cleanA"; $errors++
    }
    if (-not $cleanA.Contains('body text'))                                       { Write-Error "Strip A: body text lost. Got: $cleanA"; $errors++ }

    # Case B: heading absent (model ignored template). Should fall back to
    # raw stdout with the marker prepended, plus a warning. Capture the
    # warning so it doesn't pollute the test output.
    $warnB = $null
    $cleanB = Get-AnalysisFromCopilotStdOut -RawStdOut "just random text with no heading" -Id 99 -WarningVariable warnB -WarningAction SilentlyContinue
    if ($cleanB -notmatch '\A<!-- ptvs-triage-analyze:99 -->\s*\njust random text') {
        Write-Error "Strip B: heading-absent fallback wrong. Got: $cleanB"; $errors++
    }
    if (-not $warnB)                                                              { Write-Error "Strip B: expected a warning when heading is missing."; $errors++ }

    # Case C: empty / whitespace-only stdout — return just the marker
    # (don't crash, don't emit a no-content false analysis).
    $cleanC = Get-AnalysisFromCopilotStdOut -RawStdOut '' -Id 5
    if ($cleanC -ne '<!-- ptvs-triage-analyze:5 -->')                             { Write-Error "Strip C: empty stdout should return bare marker. Got: $cleanC"; $errors++ }
    $cleanC2 = Get-AnalysisFromCopilotStdOut -RawStdOut "   `n  `t  `n" -Id 5
    if ($cleanC2 -ne '<!-- ptvs-triage-analyze:5 -->')                            { Write-Error "Strip C2: whitespace-only stdout should return bare marker. Got: $cleanC2"; $errors++ }

    # Case D: multiple `### Triage analysis for [AzDO #N]` lines in stdout
    # (e.g., trace preamble quoted the heading while showing the prompt).
    # The LAST one is the model's real reply; earlier ones are decoy.
    $rawD = @"
$bulletChar Read prompt (shell)
  $boxV cat /tmp/prompt.txt
  $boxL ### Triage analysis for [AzDO #77]   <- inside the trace, NOT the analysis

<!-- ptvs-triage-analyze:77 -->
### Triage analysis for [AzDO #77](https://example/77)
REAL body
"@
    $cleanD = Get-AnalysisFromCopilotStdOut -RawStdOut $rawD -Id 77
    if (-not $cleanD.Contains('REAL body'))                                       { Write-Error "Strip D: real body lost. Got: $cleanD"; $errors++ }
    if ($cleanD.Contains('inside the trace'))                                     { Write-Error "Strip D: decoy heading not stripped — should have picked LAST occurrence. Got: $cleanD"; $errors++ }
    if (([regex]::Matches($cleanD, '###\s+Triage analysis')).Count -ne 1)         { Write-Error "Strip D: heading should appear exactly once. Got: $cleanD"; $errors++ }

    # Case E: heading for a DIFFERENT id is not the anchor. If model wrote
    # `### Triage analysis for [AzDO #999]` (wrong id) but we asked for id=5,
    # the strip should fall back to the raw-stdout path (with warning) so
    # the maintainer sees the obvious drift instead of silently posting a
    # truncated or wrongly-attributed analysis.
    $warnE = $null
    $rawE = "### Triage analysis for [AzDO #999](u)`nwrong-id body"
    $cleanE = Get-AnalysisFromCopilotStdOut -RawStdOut $rawE -Id 5 -WarningVariable warnE -WarningAction SilentlyContinue
    if ($cleanE -notmatch '\A<!-- ptvs-triage-analyze:5 -->')                     { Write-Error "Strip E: marker for asked id missing. Got: $cleanE"; $errors++ }
    if (-not $cleanE.Contains('### Triage analysis for [AzDO #999]'))             { Write-Error "Strip E: heading text should still be preserved for visibility. Got: $cleanE"; $errors++ }
    if (-not $warnE)                                                              { Write-Error "Strip E: expected warning when id mismatch."; $errors++ }

    # 5d. Substring-token scrub ordering: when two tokens overlap (one is
    #     a substring prefix of the other), replacing the SHORTER first
    #     would mutate the longer mid-text and leave a partial leak.
    #     Verify the call-site sorts by length DESCENDING before scrubbing.
    $shortTok = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'                  # 30 chars
    $longTok  = $shortTok + 'XYZ'                                  # 33 chars; contains shortTok as prefix
    $text     = "the long one is: $longTok and that's it"
    # Sort the way Invoke-Analyze does (longest first), then iterate-replace.
    $sorted = @($shortTok, $longTok) | Sort-Object Length -Descending
    $scrubbed = $text
    foreach ($t in $sorted) { $scrubbed = $scrubbed.Replace($t, '<redacted-leaked-secret>') }
    if ($scrubbed.Contains('XYZ')) {
        Write-Error "Substring-scrub ordering BUG: longer token's tail survived. Got: $scrubbed"; $errors++
    }
    if ($scrubbed.Contains($shortTok)) {
        Write-Error "Substring-scrub ordering BUG: shorter token still present. Got: $scrubbed"; $errors++
    }

    # 6. Invoke-Analyze: end-to-end with a stubbed copilot binary on Linux /
    #    macOS where bash scripts can be invoked directly. On Windows we run
    #    a smaller smoke test of Invoke-CopilotOnce against pwsh-as-binary
    #    (the .cmd shim would force every prompt char through cmd.exe arg
    #    parsing, which breaks on `|`, `<`, `>`, `&` even when fully quoted —
    #    a self-test limitation, NOT a bug in production: the real Copilot
    #    CLI in ubuntu-latest is a native binary with no shell layer).
    if ($IsLinux -or $IsMacOS) {
        $tmp = Join-Path ([IO.Path]::GetTempPath()) ("analyze-e2e-{0}" -f ([Guid]::NewGuid().ToString('N')))
        $sanDir = Join-Path $tmp 'san'
        $outDir = Join-Path $tmp 'out'
        $binDir = Join-Path $tmp 'bin'
        New-Item -ItemType Directory -Force -Path $sanDir,$outDir,$binDir | Out-Null
        try {
            @{ id = 7; title = 'seven'; url = 'https://example/7' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $sanDir 'wi-7.json') -Encoding UTF8
            @{ work_item = @{ id = 7; title = 'seven' } } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $sanDir 'diag-7.json') -Encoding UTF8

            # The stub ALSO dumps its received argv to $COPILOT_ARGV_DUMP so
            # the test below can verify that every argument arrived as a
            # single token (regression guard for the Start-Process word-split
            # bug in production build 14364610 — Copilot CLI rejected `-->\``
            # because the prompt was tokenised on whitespace).
            $argvDumpPath = Join-Path $tmp 'argv-dump.txt'
            # The stub emulates the real Copilot CLI's non-interactive output:
            # a multi-line "trace" preamble (tool calls with `●` / box-drawing
            # chars) BEFORE the actual analysis. analyze.ps1 must strip that
            # preamble — production build 14364772 posted the trace to public
            # GitHub issues, which is the bug this E2E now guards against.
            $stubScript = @"
#!/usr/bin/env bash
DUMP="`${COPILOT_ARGV_DUMP:-/dev/null}"
echo "ARGC=`$#" > "`$DUMP"
i=0
for a in "`$@"; do
  printf 'ARG[%d]:::%s:::END\n' "`$i" "`$a" >> "`$DUMP"
  i=`$((i+1))
done
# === Begin emulated Copilot CLI non-interactive trace preamble ===
printf '\xe2\x97\x8f Search (grep)\n'
printf '  \xe2\x94\x82 "ReturnCode" in **/*.cs\n'
printf '  \xe2\x94\x94 3 files found\n'
printf '\n'
printf '\xe2\x97\x8f Check duplicate issue details (shell)\n'
printf '  \xe2\x94\x82 gh issue view 7249 --repo microsoft/PTVS\n'
printf '  \xe2\x94\x94 24 lines...\n'
printf '\n'
printf 'Now I have enough context to write the triage analysis.\n'
printf '\n'
# === End preamble. Below this line is what the model actually emitted. ===
echo '<!-- ptvs-triage-analyze:7 -->'
echo '### Triage analysis for [AzDO #7](https://example/7)'
echo ''
echo '**Root cause category:** PTVS'
echo '**Confidence:** Low'
echo ''
echo 'stub analysis body'
"@
            $stubPath = Join-Path $binDir 'copilot'
            Set-Content -LiteralPath $stubPath -Value $stubScript -Encoding UTF8
            & chmod +x $stubPath 2>$null

            $origToken    = $env:COPILOT_GITHUB_TOKEN
            $origArgvDump = $env:COPILOT_ARGV_DUMP
            try {
                # Use a token long enough to exercise the leak-scrub path
                # (>=30 chars). This guards against the production regression
                # in build 14364420: when only ONE env token had length>=30,
                # the `$envTokens` pipeline collapsed to a scalar string and
                # `.Count` threw under StrictMode.
                $env:COPILOT_GITHUB_TOKEN = 'github_pat_' + ('S' * 80)
                $env:COPILOT_ARGV_DUMP    = $argvDumpPath
                Invoke-Analyze -SanitizedDir $sanDir -PtvsRepo $tmp -OutDir $outDir -PromptTemplate $PromptTemplate -CopilotPath $stubPath -PerItemTimeoutSeconds 30
            } finally {
                $env:COPILOT_GITHUB_TOKEN = $origToken
                $env:COPILOT_ARGV_DUMP    = $origArgvDump
            }

            $out = Join-Path $outDir 'analysis-7.md'
            if (-not (Test-Path -LiteralPath $out)) { Write-Error "E2E: analysis-7.md not created."; $errors++ }
            else {
                $text = Get-Content -LiteralPath $out -Raw
                if ($text -notmatch '<!-- ptvs-triage-analyze:7 -->')         { Write-Error "E2E: marker missing. Got: $text"; $errors++ }
                if ($text -notmatch 'stub analysis body')                      { Write-Error "E2E: stub body missing. Got: $text"; $errors++ }
                if ($text -match     'Automated triage failed')                { Write-Error "E2E: failure stub written despite stub copilot exiting 0. Got: $text"; $errors++ }
                if ($text -notmatch '\*\*Root cause category:\*\* PTVS')       { Write-Error "E2E: category missing. Got: $text"; $errors++ }
                # Trace-preamble strip (build 14364772 regression): every
                # `●` / `│` / `└` line emitted before the heading must be
                # gone from the posted analysis, and the marker must appear
                # EXACTLY once.
                $bulletChar = [char] 0x25CF   # ● — the trace-line bullet
                if ($text.Contains($bulletChar))                              { Write-Error "E2E: trace preamble bullet '$bulletChar' survived strip. Got: $text"; $errors++ }
                if ($text -match     'Now I have enough context')              { Write-Error "E2E: trace-tail commentary survived strip. Got: $text"; $errors++ }
                if ($text -match     'gh issue view 7249')                     { Write-Error "E2E: tool-trace shell command leaked into posted analysis. Got: $text"; $errors++ }
                $markerCount = ([regex]::Matches($text, '<!-- ptvs-triage-analyze:7 -->')).Count
                if ($markerCount -ne 1)                                        { Write-Error "E2E: marker should appear exactly once after strip, got ${markerCount}. Got: $text"; $errors++ }
                # The marker MUST be the first non-whitespace line so
                # post-analyses.ps1's regex still matches.
                if ($text -notmatch '\A\s*<!-- ptvs-triage-analyze:7 -->')     { Write-Error "E2E: marker is not the first line after strip. Got: $text"; $errors++ }
                # The heading must be immediately after the marker (no
                # blank line or stray content between them).
                if ($text -notmatch '<!-- ptvs-triage-analyze:7 -->\s*\n###\s+Triage analysis for') {
                    Write-Error "E2E: marker not immediately followed by heading. Got: $text"; $errors++
                }
            }

            # 6b. Argv-preservation regression (build 14364610): the prompt
            # MUST arrive as a single argv entry — Start-Process used to
            # split it on whitespace and Copilot rejected the resulting
            # `-->\`` token. Assert ARGC == 6 (-p, $Prompt, --add-dir,
            # $AddDir, --allow-all-tools, --no-color) and that the prompt
            # arg contains the marker comment intact (the `-->` from the
            # template line `<!-- ptvs-triage-analyze:{WI_ID} -->`).
            if (-not (Test-Path -LiteralPath $argvDumpPath)) {
                Write-Error "Argv-preservation: stub never wrote argv dump (did it run?)."; $errors++
            } else {
                $argv = Get-Content -LiteralPath $argvDumpPath -Raw
                if ($argv -notmatch '^ARGC=6\b') {
                    Write-Error "Argv-preservation REGRESSION: prompt was word-split. Expected ARGC=6, got:`n$argv"; $errors++
                }
                if ($argv -notmatch [regex]::Escape('ARG[0]:::-p:::END')) { Write-Error "Argv: -p not at index 0. Got:`n$argv"; $errors++ }
                if ($argv -notmatch [regex]::Escape('ARG[2]:::--add-dir:::END')) { Write-Error "Argv: --add-dir not at index 2. Got:`n$argv"; $errors++ }
                # The prompt arg at index 1 must contain the marker comment
                # complete with `-->` followed by a literal backtick — the
                # exact sequence that broke production.
                if ($argv -notmatch [regex]::Escape('-->`')) {
                    Write-Error "Argv: prompt did not contain the literal '-->\`' sequence (template line 16's code-spanned marker)."; $errors++
                }
                # The literal `-->\`` MUST NOT appear as its own argv entry.
                if ($argv -match 'ARG\[\d+\]:::-->`?:::END') {
                    Write-Error "Argv REGRESSION: '-->' arrived as its own argv element — Start-Process is back."; $errors++
                }
            }
        } finally {
            Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
        }
    } else {
        # Windows path: directly validate Invoke-CopilotOnce's Start-Process
        # plumbing. Uses pwsh itself as the "binary" (a real .exe — no cmd
        # shim, no metacharacter mangling). We can't usefully test the
        # timeout branch on pwsh because the implicit `-p` / `--add-dir`
        # arguments Invoke-CopilotOnce always appends make pwsh exit fast
        # on argument parsing (before `-Command` ever runs). The basic
        # invocation regression-tests the bug we actually hit: passing a
        # [pscustomobject] to `Start-Process @psi` instead of a hashtable
        # broke parameter binding and started the process with the entire
        # object printed as the file path.
        $pwshPath = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
        if (-not $pwshPath) { Write-Warning 'pwsh not on PATH; skipping Windows Invoke-CopilotOnce smoke.' }
        else {
            $ok = Invoke-CopilotOnce -CopilotPath $pwshPath `
                    -Prompt 'unused' `
                    -WorkDir ([IO.Path]::GetTempPath()) `
                    -AddDir  ([IO.Path]::GetTempPath()) `
                    -ExtraArgs @('-NoLogo','-NoProfile','-Command','Write-Output hello-from-stub; exit 0') `
                    -TimeoutSeconds 30
            if (-not $ok)                       { Write-Error 'Win smoke: Invoke-CopilotOnce returned $null.'; $errors++ }
            elseif ($ok.TimedOut)               { Write-Error "Win smoke: unexpected timeout."; $errors++ }
            elseif ($ok.ExitCode -isnot [int])  { Write-Error "Win smoke: ExitCode not int (got $($ok.ExitCode.GetType().FullName))."; $errors++ }
            else { Write-Host "(Windows Invoke-CopilotOnce smoke OK: exit=$($ok.ExitCode), stderr-len=$($ok.StdErr.Length))" }
        }
    }

    # 7. End-to-end leak scrub: when COPILOT_GITHUB_TOKEN contains a real-
    #    looking PAT (>=30 chars) AND the stubbed copilot echoes that exact
    #    value, Invoke-Analyze MUST redact it from the analysis file on
    #    disk before publishing. Catches a regression where the scrub
    #    code path is bypassed (e.g., wrong if-branch, ordering bug).
    if ($IsLinux -or $IsMacOS) {
        $tmp = Join-Path ([IO.Path]::GetTempPath()) ("analyze-scrub-{0}" -f ([Guid]::NewGuid().ToString('N')))
        $sanDir = Join-Path $tmp 'san'
        $outDir = Join-Path $tmp 'out'
        $binDir = Join-Path $tmp 'bin'
        New-Item -ItemType Directory -Force -Path $sanDir,$outDir,$binDir | Out-Null
        try {
            @{ id = 99; title = 'leak-test'; url = 'https://example/99' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $sanDir 'wi-99.json') -Encoding UTF8

            # Simulate a prompt-injection-induced env-var leak: the stub
            # echoes the value of COPILOT_GITHUB_TOKEN. Real Copilot CLI
            # would only do this if a customer ticket told it to and the
            # model complied; we model the worst case.
            $stubScript = "#!/usr/bin/env bash`necho '<!-- ptvs-triage-analyze:99 -->'`necho '### Triage analysis for [AzDO #99](https://example/99)'`necho ''`necho 'BTW the env var I was given starts with:' `$COPILOT_GITHUB_TOKEN`n"
            $stubPath = Join-Path $binDir 'copilot'
            Set-Content -LiteralPath $stubPath -Value $stubScript -Encoding UTF8
            & chmod +x $stubPath 2>$null

            $leakToken = 'github_pat_' + ('A' * 80)   # 91 chars; >=30 floor
            $origTok = $env:COPILOT_GITHUB_TOKEN
            try {
                $env:COPILOT_GITHUB_TOKEN = $leakToken
                Invoke-Analyze -SanitizedDir $sanDir -PtvsRepo $tmp -OutDir $outDir `
                               -PromptTemplate (Join-Path $PSScriptRoot 'prompts/triage-prompt.md') `
                               -CopilotPath $stubPath -PerItemTimeoutSeconds 30 *>$null
            } finally { $env:COPILOT_GITHUB_TOKEN = $origTok }

            $diskText = Get-Content -LiteralPath (Join-Path $outDir 'analysis-99.md') -Raw
            if ($diskText.Contains($leakToken)) {
                Write-Error "LEAK SCRUB FAILED: raw token persisted to disk. The 'redact before write' code path is broken."; $errors++
            }
            if ($diskText -notmatch '<redacted-leaked-secret>') {
                Write-Error "LEAK SCRUB: expected '<redacted-leaked-secret>' marker in scrubbed output. Got: $diskText"; $errors++
            }
        } finally {
            Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    if ($errors -gt 0) {
        throw "analyze.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'analyze.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }

if (-not $SanitizedDir) { throw "-SanitizedDir is required (omit only with -SelfTest)." }
if (-not $OutDir)       { throw "-OutDir is required (omit only with -SelfTest)."       }
if (-not $PtvsRepo) {
    if     ($env:BUILD_SOURCESDIRECTORY) { $PtvsRepo = $env:BUILD_SOURCESDIRECTORY }
    elseif ($env:GITHUB_WORKSPACE)       { $PtvsRepo = $env:GITHUB_WORKSPACE }
    else { throw "-PtvsRepo is required when BUILD_SOURCESDIRECTORY / GITHUB_WORKSPACE are not set." }
}

Invoke-Analyze -SanitizedDir $SanitizedDir -PtvsRepo $PtvsRepo -OutDir $OutDir `
               -PromptTemplate $PromptTemplate -MaxItems $MaxItems `
               -PerItemTimeoutSeconds $PerItemTimeoutSeconds -CopilotPath $CopilotPath `
               -ExtraCopilotArgs $ExtraCopilotArgs
