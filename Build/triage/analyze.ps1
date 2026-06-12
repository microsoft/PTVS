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
    # Returns [pscustomobject[]] of WiFile, DiagFile, Id, Wi, Diag, Aborted,
    # sorted by id ascending. Unary comma on return preserves array shape
    # (StrictMode-safe for 0/1 element cases).
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
    # Markdown body for items where Copilot CLI failed. The marker comment
    # carries a BuildId suffix (`...:failed:<id>`) so it doesn't collide
    # with post-analyses.ps1's success-marker regex — successful retries
    # in later runs are never blocked by a previous failure-stub comment.
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
    # Defense against prompt-injection-induced secret leakage. Scans $Text
    # for the literal value of any token in $Tokens; returns opaque
    # fingerprints (`<token len=N head=X***>`) for matches. NEVER returns
    # the secret value or any reconstructible substring. AzDO's automatic
    # secret-masking only covers live job logs, not file writes / artifacts
    # / outbound REST, so we must scrub before any of those happen.
    param(
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $Text,
        [Parameter()]          [string[]] $Tokens = @()
    )
    if ([string]::IsNullOrEmpty($Text) -or -not $Tokens) { return ,([string[]] @()) }
    $found = New-Object System.Collections.Generic.List[string]
    foreach ($t in $Tokens) {
        if ([string]::IsNullOrEmpty($t)) { continue }
        # 30-char floor: real fine-grained PAT is ≥80 chars; unresolved AzDO
        # macros are 14 chars. Catches realistic tokens without false-
        # positiving on every Foo / Bar.
        if ($t.Length -lt 30) { continue }
        if ($Text.Contains($t)) {
            $found.Add(("<token len={0} head={1}***>" -f $t.Length, $t.Substring(0, 1))) | Out-Null
        }
    }
    # Unary comma preserves typed [string[]] shape across the return
    # pipeline. Call sites must NOT wrap in @(...) — that would produce
    # a 1-element Object[] containing the inner array.
    return ,$found.ToArray()
}

function Get-AnalysisFromCopilotStdOut {
    # Strips Copilot CLI's non-interactive trace preamble (tool-call bullets,
    # box-drawing chars, mid-stream commentary) from raw stdout and returns
    # just the published analysis with the idempotency marker as line 1.
    #
    # Strategy: anchor on the LAST occurrence of the prompt-mandated heading
    # `### Triage analysis for [AzDO #<Id>]` and discard everything before
    # it, then prepend the marker. LAST occurrence guards against the trace
    # quoting the heading while echoing the prompt. If the heading is
    # absent, fall back to raw stdout with a warning so the maintainer
    # notices the formatting drift.
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
    # with a per-invocation wall-clock timeout. Returns:
    #   @{ ExitCode = int; StdOut = string; StdErr = string; TimedOut = bool }
    #
    # Uses [ProcessStartInfo]::new() + ArgumentList.Add() directly instead
    # of `Start-Process -ArgumentList`. Start-Process serializes args back
    # to a single Arguments string which Linux then re-splits on whitespace,
    # mangling any arg with spaces or special chars (e.g. the prompt's
    # `<!-- ... -->` markup). ProcessStartInfo.ArgumentList passes each
    # entry through to execve unmodified.
    # See https://github.com/PowerShell/PowerShell/issues/13089
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

    # Read stdout/stderr async: kicking off ReadToEndAsync BEFORE WaitForExit
    # drains both pipes in parallel with the child, so a child writing more
    # than ~64KB doesn't block on a full pipe and trip our timeout.
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
    # variable isn't in scope (variable group unlinked, name typo). The
    # 14-char macro fingerprint would pass a null-check and silently 401
    # downstream, so detect it explicitly.
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

    $pairs = Get-CandidatePairs -Root $SanitizedDir
    if ($pairs.Count -eq 0) {
        Write-Warning "No wi-*.json files found under $SanitizedDir. Nothing to analyze."
        return
    }

    # Belt-and-suspenders: Fetch already drops aborted items. @(...) keeps
    # array shape if Where-Object yields a single match.
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
    # BuildId in failure-stub markers prevents successful retries from being
    # blocked by an earlier run's failure stub. Fall back to a process-unique
    # value for local dev.
    $buildId = if ($env:BUILD_BUILDID) { $env:BUILD_BUILDID } else { ([Guid]::NewGuid().ToString('N')).Substring(0, 8) }
    foreach ($p in $eligible) {
        $id     = $p.Id
        $wiUrl  = [string] (Get-OptionalProp -Object $p.Wi -Name 'url' -Default '')
        $wiJson = ($p.Wi   | ConvertTo-Json -Depth 25)
        # diag-<id>.json is optional. Embed an explicit marker when absent
        # so the model knows the data is missing, not just empty.
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

        # Always write stderr for post-mortem; scrub for env-token leaks
        # before write since the .logs/ dir ships in the audit artifact.
        if ($r.StdErr) {
            $errText = [string] $r.StdErr
            # @(...) keeps array shape if Where|Sort yields a single match.
            $envTokensErr = @(@($env:COPILOT_GITHUB_TOKEN, $env:GH_TOKEN, $env:GITHUB_TOKEN) |
                              Where-Object { -not [string]::IsNullOrEmpty($_) -and $_.Length -ge 30 } |
                              Sort-Object Length -Descending)
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
        # Strip Copilot CLI's non-interactive trace preamble.
        $output = Get-AnalysisFromCopilotStdOut -RawStdOut ([string] $r.StdOut) -Id $id

        # Scrub any env-var token value that the model echoed back in its
        # output. Tokens sorted by length DESCENDING so a longer token is
        # fully redacted before a shorter substring iteration would mutate
        # it mid-text. See Find-LeakedTokenFingerprints.
        $envTokens = @(@($env:COPILOT_GITHUB_TOKEN, $env:GH_TOKEN, $env:GITHUB_TOKEN) |
                       Where-Object { -not [string]::IsNullOrEmpty($_) -and $_.Length -ge 30 } |
                       Sort-Object Length -Descending)
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
        # Zero successes is worth signaling loudly — usually auth, quota, or
        # binary. Don't fail the step; the artifact still carries the stubs.
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
    #    aborted items, attaches diag when present, no diag => $null.
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

    # 2b. Shape regression: empty-dir and 1-item cases must return real
    #     arrays whose .Count works under StrictMode (not $null, not scalar).
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
    #    the BuildId so it does not collide with the success-marker regex
    #    used by post-analyses.ps1 for idempotency.
    $stub = Get-FailureStub -Id 999 -WiUrl 'https://example/999' -Reason 'quota exhausted' -BuildId 'abc123'
    $firstLine = ($stub -split "`r?`n")[0]
    if ($firstLine -ne '<!-- ptvs-triage-analyze:999:failed:abc123 -->') {
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
    #     either missing or the literal AzDO macro `$(COPILOT_PAT)` (means
    #     the variable group is not linked / variable not present).
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

    # 5e. Pipeline-collapse regression: a `@(a,b,c) | Where | Sort-Object`
    #     pipeline collapses to a scalar string when only ONE element
    #     survives the filter. Under StrictMode, `.Count` on a string throws
    #     and any caller that doesn't defend against this crashes.
    $longTokForCollapse = 'github_pat_' + ('Z' * 80)
    $collapsed = @($longTokForCollapse, $null, '') |
                 Where-Object { -not [string]::IsNullOrEmpty($_) -and $_.Length -ge 30 } |
                 Sort-Object Length -Descending
    # The pipeline produces a scalar string; the @(...) wrap repairs the
    # array shape before any downstream .Count or foreach.
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
    #     normalization. Unit-tests the helper directly so the contract is
    #     verified on Windows too (the full E2E only runs on Linux/macOS).
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

            # The stub also dumps its received argv to $COPILOT_ARGV_DUMP so
            # the test below can verify every argument arrived as a single
            # token (regression guard for the Start-Process word-split bug).
            $argvDumpPath = Join-Path $tmp 'argv-dump.txt'
            # The stub emulates the real Copilot CLI's non-interactive output:
            # a multi-line trace preamble (tool calls with `●` / box-drawing
            # chars) BEFORE the actual analysis. analyze.ps1 must strip it.
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
                # Use a token long enough (>=30 chars) to exercise the
                # leak-scrub path. Regression guard against the scalar-
                # collapse bug when only one env token survives the filter.
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
                # Trace-preamble strip regression: every `●` / `│` / `└` line
                # emitted before the heading must be gone from the posted
                # analysis, and the marker must appear EXACTLY once.
                $bulletChar = [char] 0x25CF   # ● — the trace-line bullet
                if ($text.Contains($bulletChar))                              { Write-Error "E2E: trace preamble bullet '$bulletChar' survived strip. Got: $text"; $errors++ }
                if ($text -match     'Now I have enough context')              { Write-Error "E2E: trace-tail commentary survived strip. Got: $text"; $errors++ }
                if ($text -match     'gh issue view 7249')                     { Write-Error "E2E: tool-trace shell command leaked into posted analysis. Got: $text"; $errors++ }
                $markerCount = ([regex]::Matches($text, '<!-- ptvs-triage-analyze:7 -->')).Count
                if ($markerCount -ne 1)                                        { Write-Error "E2E: marker should appear exactly once after strip, got ${markerCount}. Got: $text"; $errors++ }
                # The marker MUST be the first non-whitespace line so
                # post-analyses.ps1's regex still matches.
                if ($text -notmatch '\A\s*<!-- ptvs-triage-analyze:7 -->')     { Write-Error "E2E: marker is not the first line after strip. Got: $text"; $errors++ }
                # The heading must be immediately after the marker.
                if ($text -notmatch '<!-- ptvs-triage-analyze:7 -->\s*\n###\s+Triage analysis for') {
                    Write-Error "E2E: marker not immediately followed by heading. Got: $text"; $errors++
                }
            }

            # 6b. Argv-preservation regression: the prompt MUST arrive as a
            # single argv entry. Start-Process used to split it on whitespace
            # and Copilot rejected the resulting `-->\`` token. Assert
            # ARGC == 6 (-p, $Prompt, --add-dir, $AddDir, --allow-all-tools,
            # --no-color) and that the prompt arg contains the marker
            # comment intact.
            if (-not (Test-Path -LiteralPath $argvDumpPath)) {
                Write-Error "Argv-preservation: stub never wrote argv dump (did it run?)."; $errors++
            } else {
                $argv = Get-Content -LiteralPath $argvDumpPath -Raw
                if ($argv -notmatch '^ARGC=6\b') {
                    Write-Error "Argv-preservation REGRESSION: prompt was word-split. Expected ARGC=6, got:`n$argv"; $errors++
                }
                if ($argv -notmatch [regex]::Escape('ARG[0]:::-p:::END')) { Write-Error "Argv: -p not at index 0. Got:`n$argv"; $errors++ }
                if ($argv -notmatch [regex]::Escape('ARG[2]:::--add-dir:::END')) { Write-Error "Argv: --add-dir not at index 2. Got:`n$argv"; $errors++ }
                # The prompt arg at index 1 must contain `-->` followed by
                # a literal backtick — the exact sequence that historically
                # tripped the word-split bug.
                if ($argv -notmatch [regex]::Escape('-->`')) {
                    Write-Error "Argv: prompt did not contain the literal '-->\`' sequence."; $errors++
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
        # Windows path: directly validate Invoke-CopilotOnce's process
        # plumbing using pwsh itself as the "binary". The Linux E2E with a
        # bash stub doesn't run on Windows, so this is the best we can do.
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
    #    value, Invoke-Analyze MUST redact it from the analysis file on disk
    #    before publishing.
    if ($IsLinux -or $IsMacOS) {
        $tmp = Join-Path ([IO.Path]::GetTempPath()) ("analyze-scrub-{0}" -f ([Guid]::NewGuid().ToString('N')))
        $sanDir = Join-Path $tmp 'san'
        $outDir = Join-Path $tmp 'out'
        $binDir = Join-Path $tmp 'bin'
        New-Item -ItemType Directory -Force -Path $sanDir,$outDir,$binDir | Out-Null
        try {
            @{ id = 99; title = 'leak-test'; url = 'https://example/99' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $sanDir 'wi-99.json') -Encoding UTF8

            # Simulate a prompt-injection-induced env-var leak: stub echoes
            # the value of COPILOT_GITHUB_TOKEN. Models the worst case.
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
