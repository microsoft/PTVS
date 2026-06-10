<#
    .SYNOPSIS
        Strips PII / secrets / Windows-user paths from work-item content before
        AI inference and before any public mirror to microsoft/PTVS.

    .DESCRIPTION
        Step 4 of the weekly triage workflow: PII / secret sanitization pass
        on the hydrated candidate JSON.

        Two modes:
          (default) full sanitization: emits one wi-<id>.json per candidate under
                    -OutDir, plus consumes any diag-<id>.json files in -DiagDir
                    (also sanitizes those in place into -OutDir).
          -TitlesOnly: minimal pass — scrubs emails and Windows-username path
                       fragments from titles only, leaves the rest of the work
                       item alone. Used by Job 1 to compose the public weekly
                       report.

        Behavior on secret-pattern hit depends on the action class declared
        on each regex in $Script:SecretRegexes:
          - 'abort'  : high-confidence pattern (e.g. ghp_..., PEM headers,
                       AKIA..., JWT, AccountKey=...). Flags the candidate
                       with `sanitization_aborted = true` so fetch.yml
                       Step 4 deletes the wi/diag pair from the artifact.
                       When -FailOnSecret is set, also exits non-zero.
                       As defense-in-depth, the literal token is ALSO
                       redacted in place (replaced with
                       `<redacted-secret:<name>>`) so the on-disk
                       artifact never carries the raw secret even if
                       Step 4 is skipped or reordered.
          - 'redact' : lower-confidence pattern that may appear naturally in
                       customer free-text (e.g. `Password=hunter2` in a
                       repro). Redacted in place via the per-regex
                       `replacement`, then surfaced in `redacted_secrets`
                       on the sanitized wi for operator visibility. The
                       item still flows through triage; no abort.

    .PARAMETER InFile
        Path to candidates.json (from query-azdo.ps1).

    .PARAMETER OutFile
        For -TitlesOnly mode: path to a single sanitized JSON.

    .PARAMETER OutDir
        For full mode: directory under which one wi-<id>.json per candidate is
        written.

    .PARAMETER DiagDir
        Directory containing diag-<id>.json files (from parse-diagnostics.ps1).
        Each found file is sanitized in place into -OutDir/diag-<id>.json.
        Optional.

    .PARAMETER TitlesOnly
        Use the lightweight (Job 1) sanitization path.

    .PARAMETER FailOnSecret
        Exit non-zero on a detected secret match.

    .PARAMETER SelfTest
        Run inline smoke tests against fixtures.
#>
[CmdletBinding()]
param(
    [string] $InFile,
    [string] $OutFile,
    [string] $OutDir,
    [string] $DiagDir,
    [switch] $TitlesOnly,
    [switch] $FailOnSecret,
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# PII regexes (redact-in-place). Generous on purpose — false positives
# here are cheap (just an extra `<redacted-*>` marker in the artifact)
# while false negatives are expensive (a PII leak on the public GitHub).
$Script:EmailRegex     = '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}'
$Script:WinUserPath    = '(?i)([A-Z]:\\Users\\)([^\\/<>"|?*\s]+)'
$Script:UnixHome       = '(?i)(/home/|/Users/)([A-Za-z0-9._-]+)'
$Script:SidRegex       = 'S-1-[0-9-]{6,}'
$Script:MachineNameLn  = '(?im)^\s*(Machine Name|MachineName|Computer Name|HostName)\s*[:=]\s*[^\r\n]+'
$Script:PhoneRegex     = '(?:\+?\d{1,3}[\s.-]?)?(?:\(?\d{3}\)?[\s.-]?)\d{3}[\s.-]?\d{4}'

# Secret regexes. Each entry carries an `action`:
#   - 'abort'  — high-confidence pattern (specific prefix + length + charset);
#                a match flags `sanitization_aborted` so fetch.yml Step 4
#                deletes the whole wi/diag pair from the artifact, AND
#                redacts the literal token in place with a
#                `<redacted-secret:<name>>` marker as defense-in-depth so
#                the on-disk artifact never carries the raw secret even
#                if Step 4 is skipped or reordered. False positives here
#                cost a lost triage item, so use only when the pattern is
#                specific enough that a hit is almost certainly a real
#                secret.
#   - 'redact' — lower-confidence pattern that may appear naturally in
#                customer free-text (e.g. `Password=hunter2` in a repro
#                description). On a hit we redact in place with the
#                per-regex `replacement` and let the item flow through
#                triage. Tracked separately on the sanitized wi as
#                `redacted_secrets` for operator visibility, without
#                triggering the abort drop.
# Feature #2 expanded the scan surface from compact log fields to full
# descriptions + repro + up to 10×16KB attachment excerpts per item, so
# generic redact-class patterns must NOT be promoted to abort or we
# silently shrink the triage feed on benign customer wording.
$Script:SecretRegexes = @(
    @{ name = 'github-pat-classic';   action = 'abort';
       pattern = 'ghp_[A-Za-z0-9]{36}' },
    @{ name = 'github-pat-fine';      action = 'abort';
       pattern = 'github_pat_[A-Za-z0-9_]{82}' },
    @{ name = 'github-app-token';     action = 'abort';
       pattern = '(?:ghs|gho|ghu|ghr)_[A-Za-z0-9]{36}' },
    @{ name = 'azure-storage-key';    action = 'abort';
       pattern = 'AccountKey=[A-Za-z0-9+/=]{40,}' },
    @{ name = 'sas-token';            action = 'abort';
       pattern = '[?&]sig=[A-Za-z0-9%+/=]{20,}' },
    @{ name = 'jwt';                  action = 'abort';
       pattern = 'eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}' },
    @{ name = 'aws-access-key';       action = 'abort';
       pattern = 'AKIA[0-9A-Z]{16}' },
    @{ name = 'private-key-pem';      action = 'abort';
       pattern = '-----BEGIN (?:RSA |EC |OPENSSH |DSA |PGP )?PRIVATE KEY-----' },
    # connection-string-pw: GENERIC pattern that fires on benign customer
    # wording like `Password=hunter2`, `Set Pwd=changeme in config`, etc.
    # Redact in place (preserving the `Password=` / `Pwd=` prefix so the
    # reader still sees the structure) instead of aborting the whole item.
    #
    # The `{6,}` value-length floor is intentional: customer free-text in
    # repros frequently contains short placeholders like `Password=1234`,
    # `Pwd=abc`, `Password=test`, which are example/illustrative values
    # rather than real credentials. Requiring 6+ chars trims that noise.
    # The trade-off is that a real credential happening to be shorter
    # than 6 chars (rare, but possible if someone uses `Pwd=abcde`) is
    # silently let through to triage. Accepted: real secrets that short
    # are extremely uncommon, and the higher-confidence patterns above
    # (ghp_, PEM, AKIA, JWT, AccountKey=) have no length floor since
    # their prefix already makes a hit almost certainly real.
    @{ name = 'connection-string-pw'; action = 'redact';
       pattern     = '(?i)(Password|Pwd)\s*=\s*[^;<>"\s]{6,}';
       replacement = '$1=<redacted-secret>' }
)

function Remove-PiiFromString {
    param(
        [Parameter()] [string] $Text,
        [ref] $SecretHitsRef,
        # Untyped: PowerShell's [ref] parameter binder rejects $null, so we
        # accept any object here. Callers pass `([ref] $list)` which produces
        # a [System.Management.Automation.PSReference]; we read .Value on it
        # the same way as $SecretHitsRef. Defaulting to $null lets existing
        # callers that only care about abort-class hits omit the parameter.
        [Parameter()] $RedactedHitsRef = $null
    )
    if ([string]::IsNullOrEmpty($Text)) { return $Text }

    $result = $Text

    # 1a. High-confidence secrets: flag for abort AND redact-in-place as
    #     defense-in-depth, so the literal token (`ghp_…`, PEM, `AKIA…`)
    #     never sits on disk in -OutDir even if fetch.yml Step 4 (which
    #     deletes the wi/diag pair when sanitization_aborted=true) is
    #     skipped, reordered, or fails to run. The abort flag is still
    #     authoritative — Step 4 will drop the pair downstream — this
    #     just stops the on-disk artifact from carrying the raw secret
    #     in the meantime. Process abort patterns FIRST so a redact-class
    #     pattern overlapping the same span (e.g. `Password=ghp_aaa...`)
    #     doesn't eat the high-confidence match before we can detect it.
    #     We use `.Value.Add()` (not `+= $s.name`) because `+=` on a
    #     [ref]-wrapped Generic.List[string] silently re-materializes the
    #     backing field as object[] (PowerShell + concat semantics), and
    #     any future `.Add()` from the caller would then break.
    foreach ($s in $Script:SecretRegexes) {
        if ($s.action -ne 'abort') { continue }
        if ($result -match $s.pattern) {
            if ($null -ne $SecretHitsRef) { $SecretHitsRef.Value.Add($s.name) | Out-Null }
            $result = [regex]::Replace($result, $s.pattern, "<redacted-secret:$($s.name)>")
        }
    }

    # 1b. Lower-confidence secrets: redact in place so the item still flows
    #     through triage instead of being silently dropped. Track separately
    #     on $RedactedHitsRef (when provided) for operator visibility.
    foreach ($s in $Script:SecretRegexes) {
        if ($s.action -ne 'redact') { continue }
        if ($result -match $s.pattern) {
            $result = [regex]::Replace($result, $s.pattern, $s.replacement)
            if ($null -ne $RedactedHitsRef) { $RedactedHitsRef.Value.Add($s.name) | Out-Null }
        }
    }

    # 2. Emails.
    $result = [regex]::Replace($result, $Script:EmailRegex, '<redacted-email>')

    # 3. Windows user paths: keep prefix C:\Users\, redact username, keep tail.
    $result = [regex]::Replace($result, $Script:WinUserPath, '$1<redacted-user>')

    # 4. Unix home paths.
    $result = [regex]::Replace($result, $Script:UnixHome, '$1<redacted-user>')

    # 5. SIDs.
    $result = [regex]::Replace($result, $Script:SidRegex, '<redacted-sid>')

    # 6. Machine-name lines.
    $result = [regex]::Replace($result, $Script:MachineNameLn, '$1: <redacted-host>')

    # 7. Phone numbers (best-effort; can over-trigger on long IDs — accept).
    $result = [regex]::Replace($result, $Script:PhoneRegex, '<redacted-phone>')

    return $result
}

function Remove-PiiFromValue {
    # Deep walker. Used to sanitize diag-<id>.json, which now carries nested
    # attachments[] objects with text excerpts that may contain PII or
    # secrets. Strings get scrubbed; arrays/objects are walked recursively;
    # everything else (ints, bools, dates, $null) is passed through
    # unchanged. The unary comma on the array branch prevents PowerShell
    # from collapsing single-element arrays into scalars on return.
    param(
        [Parameter()] [AllowNull()] [object] $Value,
        [ref] $SecretHitsRef,
        # Untyped optional ref (see Remove-PiiFromString for rationale).
        [Parameter()] $RedactedHitsRef = $null
    )
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) {
        return Remove-PiiFromString -Text $Value `
                                    -SecretHitsRef $SecretHitsRef `
                                    -RedactedHitsRef $RedactedHitsRef
    }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $newObj = [pscustomobject] @{}
        foreach ($p in $Value.PSObject.Properties) {
            Add-Member -InputObject $newObj -NotePropertyName $p.Name `
                -NotePropertyValue (Remove-PiiFromValue -Value $p.Value `
                                                       -SecretHitsRef $SecretHitsRef `
                                                       -RedactedHitsRef $RedactedHitsRef) -Force
        }
        return $newObj
    }
    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
        $items = New-Object System.Collections.Generic.List[object]
        foreach ($i in $Value) {
            $items.Add((Remove-PiiFromValue -Value $i `
                                            -SecretHitsRef $SecretHitsRef `
                                            -RedactedHitsRef $RedactedHitsRef)) | Out-Null
        }
        # Wrap with unary comma so a one-element list still serializes as
        # a JSON array — without this, PowerShell collapses single-element
        # function returns to a scalar and ConvertTo-Json emits `"x"`
        # instead of `["x"]`.
        return ,@($items.ToArray())
    }
    return $Value
}

function Remove-PiiFromTitle {
    param([string] $Title)
    if ([string]::IsNullOrEmpty($Title)) { return $Title }
    $t = [regex]::Replace($Title, $Script:EmailRegex, '<redacted-email>')
    $t = [regex]::Replace($t,     $Script:WinUserPath, '$1<redacted-user>')
    $t = [regex]::Replace($t,     $Script:UnixHome,   '$1<redacted-user>')
    return $t
}

function ConvertFrom-HtmlForScan {
    # HtmlDecode-only pre-pass for PII scanning of AzDO body HTML fields
    # (description_html / repro_steps_html / system_info). The PII and
    # secret regexes only match literal `@`, `=`, etc., so any entity-
    # encoded PII the customer or AzDO entity-encodes (e.g.
    # `alice&#64;example.com`, `Password&#61;hunter2`, `&amp;`) would
    # otherwise slip past the scanner. Decoding before the scan exposes
    # those forms to detection AND replaces them in the output with the
    # canonical decoded characters — so the on-disk artifact carries the
    # decoded-and-redacted text (never the encoded leak).
    # We do NOT strip tags here: keeping `<p>`/`<br>`/etc. in the field
    # preserves the original document structure for the operator
    # reviewing the artifact, and avoids losing repro detail that
    # happened to contain encoded angle brackets (the diag path uses
    # ConvertTo-PlainText with strip-then-decode for the same reason).
    # Safe on $null / empty / plain-text input — returns '' or the
    # original string when there's nothing to decode.
    param([Parameter()] [AllowNull()] [string] $Text)
    if ([string]::IsNullOrEmpty($Text)) { return '' }
    return [System.Net.WebUtility]::HtmlDecode($Text)
}

function Sanitize-Candidate {
    param([Parameter(Mandatory)] [object] $Candidate)
    $secrets  = New-Object System.Collections.Generic.List[string]
    $redacted = New-Object System.Collections.Generic.List[string]
    $secretsRef  = [ref] $secrets
    $redactedRef = [ref] $redacted

    $san = [pscustomobject] @{
        id                 = $Candidate.id
        rev                = $Candidate.rev
        work_item_type     = $Candidate.work_item_type
        state              = $Candidate.state
        area_path          = $Candidate.area_path
        tags               = $Candidate.tags
        url                = $Candidate.url
        created_date       = $Candidate.created_date
        comment_count      = $Candidate.comment_count

        title              = Remove-PiiFromString -Text $Candidate.title                                                                  -SecretHitsRef $secretsRef -RedactedHitsRef $redactedRef
        description_html   = Remove-PiiFromString -Text (ConvertFrom-HtmlForScan ([string] $Candidate.description_html))                  -SecretHitsRef $secretsRef -RedactedHitsRef $redactedRef
        repro_steps_html   = Remove-PiiFromString -Text (ConvertFrom-HtmlForScan ([string] $Candidate.repro_steps_html))                  -SecretHitsRef $secretsRef -RedactedHitsRef $redactedRef
        system_info        = Remove-PiiFromString -Text (ConvertFrom-HtmlForScan ([string] $Candidate.system_info))                       -SecretHitsRef $secretsRef -RedactedHitsRef $redactedRef
        comments           = @(
            foreach ($c in @($Candidate.comments)) {
                [pscustomobject] @{
                    id          = $c.id
                    createdDate = $c.createdDate
                    text        = Remove-PiiFromString -Text $c.text -SecretHitsRef $secretsRef -RedactedHitsRef $redactedRef
                }
            }
        )
        attachments        = @(
            foreach ($a in @($Candidate.attachments)) {
                [pscustomobject] @{
                    name = $a.name
                    url  = $a.url
                }
            }
        )

        # NEVER copy these to the sanitized object; the WI URL is enough for
        # internal maintainers to look up the original reporter.
        # created_by_display / created_by_email are deliberately omitted.

        sanitization_aborted = $false
        # secret_hits holds ONLY abort-class matches; presence of any entry
        # means fetch.yml Step 4 will drop this wi/diag pair.
        secret_hits          = @($secrets | Sort-Object -Unique)
        # redacted_secrets holds lower-confidence patterns (e.g.
        # connection-string-pw) that were redacted in place. Surfaced for
        # operator visibility; does NOT trigger the drop.
        redacted_secrets     = @($redacted | Sort-Object -Unique)
    }

    if ($secrets.Count -gt 0) {
        Write-Warning "Secret pattern(s) [$(($secrets | Sort-Object -Unique) -join ', ')] detected in WI #$($Candidate.id). Mirroring will be blocked."
        $san.sanitization_aborted = $true
    }
    return $san
}

function Invoke-TitlesOnly {
    param([string] $InFile, [string] $OutFile)
    if (-not (Test-Path -LiteralPath $InFile)) { throw "Input not found: $InFile" }
    if (-not $OutFile) { throw "-OutFile is required in -TitlesOnly mode." }
    $candidates = Get-Content -LiteralPath $InFile -Raw | ConvertFrom-Json

    # Normalize to array (ConvertFrom-Json yields a single object for length-1).
    if ($candidates -isnot [System.Array]) { $candidates = @($candidates) }

    $out = foreach ($c in $candidates) {
        [pscustomobject] @{
            id           = $c.id
            title        = Remove-PiiFromTitle -Title $c.title
            url          = $c.url
            area_path    = $c.area_path
            created_date = $c.created_date
            state        = $c.state
            work_item_type = $c.work_item_type
        }
    }
    ($out | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $OutFile -Encoding UTF8
    Write-Host "Sanitized $(@($out).Count) titles → $OutFile"
}

function Invoke-Full {
    param([string] $InFile, [string] $OutDir, [string] $DiagDir, [switch] $FailOnSecret)
    if (-not (Test-Path -LiteralPath $InFile)) { throw "Input not found: $InFile" }
    if (-not $OutDir) { throw "-OutDir is required in full mode." }
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

    $candidates = Get-Content -LiteralPath $InFile -Raw | ConvertFrom-Json
    if ($candidates -isnot [System.Array]) { $candidates = @($candidates) }
    $anyAborted = $false
    foreach ($c in $candidates) {
        $san = Sanitize-Candidate -Candidate $c
        if ($san.sanitization_aborted) { $anyAborted = $true }
        $path = Join-Path $OutDir ("wi-{0}.json" -f $c.id)
        ($san | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $path -Encoding UTF8
    }
    Write-Host "Sanitized $(@($candidates).Count) work item(s) → $OutDir"

    # Sanitize each diag-<id>.json if present. The current diag schema is
    # nested (work_item{...}, attachments[]{content_excerpt,...}, etc.) so
    # we use Remove-PiiFromValue to walk the whole tree. Any ABORT-class
    # secret hit inside a diag file gets PROPAGATED back to the matching
    # wi-<id>.json: fetch.yml Step 4 reads sanitization_aborted off the wi
    # file to decide whether to drop the pair, so a secret found only in
    # diag content must flip the wi flag too — otherwise the diag file
    # would still ride along in the AzDO artifact upload.
    # REDACT-class hits (e.g. connection-string-pw matching `Password=...`
    # in a customer repro) are propagated to wi.redacted_secrets for
    # operator visibility but do NOT trigger the abort drop, since
    # redact-class patterns are scrubbed in place.
    if ($DiagDir -and (Test-Path -LiteralPath $DiagDir)) {
        $diagFiles = Get-ChildItem -LiteralPath $DiagDir -Filter 'diag-*.json' -ErrorAction SilentlyContinue
        foreach ($df in $diagFiles) {
            $diag = Get-Content -LiteralPath $df.FullName -Raw | ConvertFrom-Json
            $secrets  = New-Object System.Collections.Generic.List[string]
            $redacted = New-Object System.Collections.Generic.List[string]
            $diagSan = Remove-PiiFromValue -Value $diag `
                                            -SecretHitsRef    ([ref] $secrets) `
                                            -RedactedHitsRef  ([ref] $redacted)
            $outPath = Join-Path $OutDir $df.Name
            ($diagSan | ConvertTo-Json -Depth 25) | Set-Content -LiteralPath $outPath -Encoding UTF8

            $hasAbort   = $secrets.Count  -gt 0
            $hasRedact  = $redacted.Count -gt 0

            if ($hasAbort -or $hasRedact) {
                $uniqAbort  = @($secrets  | Sort-Object -Unique)
                $uniqRedact = @($redacted | Sort-Object -Unique)
                if ($hasAbort) {
                    Write-Warning "Secret pattern(s) in $($df.Name): $($uniqAbort -join ', '). Blocking mirror downstream."
                    $anyAborted = $true
                }
                if ($hasRedact) {
                    Write-Host "Redacted lower-confidence secret pattern(s) in $($df.Name): $($uniqRedact -join ', ')."
                }

                # Find the sibling wi-<id>.json and propagate hits. Abort-class
                # hits flip sanitization_aborted (so fetch.yml Step 4 catches
                # both files); redact-class hits go into redacted_secrets for
                # operator visibility but don't trigger the drop.
                $idMatch = [regex]::Match($df.Name, '^diag-(\d+)\.json$')
                if ($idMatch.Success) {
                    $wiPath = Join-Path $OutDir ("wi-{0}.json" -f $idMatch.Groups[1].Value)
                    if (Test-Path -LiteralPath $wiPath) {
                        $wi = Get-Content -LiteralPath $wiPath -Raw | ConvertFrom-Json
                        $wiTouched = $false
                        if ($hasAbort) {
                            $wi.sanitization_aborted = $true
                            $existing = @()
                            if ($wi.PSObject.Properties['secret_hits'] -and $wi.secret_hits) {
                                $existing = @($wi.secret_hits)
                            }
                            $wi.secret_hits = @(($existing + $uniqAbort) | Sort-Object -Unique)
                            Write-Warning "Propagated diag secret hit(s) to $(Split-Path $wiPath -Leaf): $($wi.secret_hits -join ', ')."
                            $wiTouched = $true
                        }
                        if ($hasRedact) {
                            $existingR = @()
                            if ($wi.PSObject.Properties['redacted_secrets'] -and $wi.redacted_secrets) {
                                $existingR = @($wi.redacted_secrets)
                            } elseif (-not $wi.PSObject.Properties['redacted_secrets']) {
                                # Older wi shape pre-redacted_secrets — add the field.
                                Add-Member -InputObject $wi -NotePropertyName 'redacted_secrets' -NotePropertyValue @() -Force
                            }
                            $wi.redacted_secrets = @(($existingR + $uniqRedact) | Sort-Object -Unique)
                            $wiTouched = $true
                        }
                        if ($wiTouched) {
                            ($wi | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $wiPath -Encoding UTF8
                        }
                    } elseif ($hasAbort) {
                        # Fail-closed: orphan diag with an abort-class secret
                        # would NOT be dropped by fetch.yml Step 4 (Step 4
                        # iterates wi-*.json and reads sanitization_aborted
                        # to decide whether to delete the matching diag).
                        # Without a sibling wi file, the diag would ride
                        # along in the artifact upload with the secret
                        # intact. Delete the orphan diag immediately so the
                        # bytes never reach the artifact, regardless of
                        # whether -FailOnSecret was set.
                        Write-Warning "No matching wi-$($idMatch.Groups[1].Value).json next to $($df.Name); deleting orphan diag (abort-class secret hit, failing closed)."
                        Remove-Item -LiteralPath $outPath -Force -ErrorAction SilentlyContinue
                    }
                }
            }
        }
    }

    if ($anyAborted -and $FailOnSecret) {
        throw 'Secret pattern detected in at least one candidate (FailOnSecret set).'
    }
}

# ──────────────────────────────────────────────────────────────────────
function Invoke-SelfTest {
    $errors = 0

    # Email redaction.
    $t = Remove-PiiFromString -Text 'Contact me at alice.smith@example.com please.' -SecretHitsRef ([ref] (New-Object System.Collections.Generic.List[string]))
    if ($t -match 'alice\.smith@') { Write-Error "Email not redacted: $t"; $errors++ }
    if ($t -notmatch '<redacted-email>') { Write-Error "Email redaction marker missing: $t"; $errors++ }

    # Windows path redaction (preserves prefix and tail).
    $t = Remove-PiiFromString -Text 'See C:\Users\jdoe\AppData\Local\Temp for details.' -SecretHitsRef ([ref] (New-Object System.Collections.Generic.List[string]))
    if ($t -match 'jdoe') { Write-Error "Windows username not redacted: $t"; $errors++ }
    if ($t -notmatch [regex]::Escape('C:\Users\<redacted-user>\AppData')) { Write-Error "Windows path tail lost: $t"; $errors++ }

    # No PII → unchanged.
    $orig = 'Visual Studio crashes on open of a Python project.'
    $t = Remove-PiiFromString -Text $orig -SecretHitsRef ([ref] (New-Object System.Collections.Generic.List[string]))
    if ($t -ne $orig) { Write-Error "Non-PII text was modified: $t"; $errors++ }

    # Secret detection trips the bucket.
    $hits = New-Object System.Collections.Generic.List[string]
    $_   = Remove-PiiFromString -Text 'token: ghp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' -SecretHitsRef ([ref] $hits)
    if ($hits.Count -lt 1) { Write-Error 'GitHub PAT not detected.'; $errors++ }

    # Title-only path doesn't touch description.
    $titleIn = 'crash with C:\Users\jdoe\file.py — alice@x.com'
    $titleOut = Remove-PiiFromTitle -Title $titleIn
    if ($titleOut -match 'jdoe' -or $titleOut -match 'alice@x') {
        Write-Error "Title PII leak: $titleOut"; $errors++
    }

    # Remove-PiiFromValue: deep walk preserves shape and scrubs nested
    # strings; single-element arrays must survive as arrays (not collapse
    # to scalars on return).
    $nested = [pscustomobject] @{
        a = 'hello alice@example.com'
        list = @('only one path C:\Users\jdoe\f.py')
        nested = [pscustomobject] @{
            comment = 'unrelated text'
            n = 42
            flag = $true
        }
    }
    $hits = New-Object System.Collections.Generic.List[string]
    $clean = Remove-PiiFromValue -Value $nested -SecretHitsRef ([ref] $hits)
    if ($clean.a -match 'alice@')                               { Write-Error "Deep walk failed to scrub top-level string: $($clean.a)"; $errors++ }
    if (@($clean.list).Count -ne 1)                             { Write-Error 'Deep walk collapsed single-element array.'; $errors++ }
    if (@($clean.list)[0] -match 'jdoe')                        { Write-Error "Deep walk failed to scrub string inside array: $(@($clean.list)[0])"; $errors++ }
    if ($clean.nested.n -ne 42 -or $clean.nested.flag -ne $true) { Write-Error 'Deep walk altered non-string primitives.'; $errors++ }

    # Secret detection through deep walk: a secret hiding in a nested
    # attachment excerpt must still hit $hits, which the diag-sanitization
    # path uses to flip the abort flag on the matching wi-<id>.json.
    $diag = [pscustomobject] @{
        work_item = [pscustomobject] @{ id = 7; title = 'sample'; description = 'fine' }
        attachments = @(
            [pscustomobject] @{
                name = 'env.txt'
                content_excerpt = 'export GH_TOKEN=ghp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
            }
        )
    }
    $hits2 = New-Object System.Collections.Generic.List[string]
    $_     = Remove-PiiFromValue -Value $diag -SecretHitsRef ([ref] $hits2)
    if ($hits2.Count -lt 1) { Write-Error 'Deep walk did not detect secret inside nested attachment excerpt.'; $errors++ }
    if (-not ($hits2 -contains 'github-pat-classic')) { Write-Error "Expected github-pat-classic in secret hits, got: $($hits2 -join ', ')"; $errors++ }

    # End-to-end: a clean wi-<id>.json combined with a diag-<id>.json that
    # contains a secret must, after Invoke-Full runs, leave the wi file
    # with sanitization_aborted=true so fetch.yml Step 4 drops both.
    $tmpRoot = Join-Path ([IO.Path]::GetTempPath()) ("sanitize-test-{0}" -f ([Guid]::NewGuid().ToString('N')))
    $tmpInDir = Join-Path $tmpRoot 'in'
    $tmpDiag  = Join-Path $tmpRoot 'diag'
    $tmpOut   = Join-Path $tmpRoot 'out'
    New-Item -ItemType Directory -Force -Path $tmpInDir, $tmpDiag, $tmpOut | Out-Null
    try {
        $candIn = @(
            [pscustomobject] @{
                id = 12345
                rev = 1
                title = 'plain title'
                description_html = '<p>nothing sensitive</p>'
                repro_steps_html = ''
                system_info = ''
                tags = 'vsfeedback'
                area_path = 'DevDiv\Python and AI Tools\Python\VS IDE'
                work_item_type = 'Bug'
                state = 'Active'
                created_date = '2026-05-08T12:00:00Z'
                changed_date = '2026-05-08T12:00:00Z'
                created_by_display = 'Test User'
                created_by_email   = 'test@example.com'
                url = 'https://example/12345'
                comment_count = 0
                comments = @()
                attachments = @()
            }
        )
        $candInPath = Join-Path $tmpInDir 'candidates.json'
        ($candIn | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $candInPath -Encoding UTF8

        # Diag file with a secret in a nested attachment excerpt.
        $diagDoc = [pscustomobject] @{
            work_item = [pscustomobject] @{ id = 12345; title = 'plain title'; description = 'fine' }
            attachments = @(
                [pscustomobject] @{
                    name = 'env.txt'; url = 'u'; size_bytes = 100; kind = 'text';
                    downloaded = $true; skip_reason = $null;
                    content_excerpt = 'GH_TOKEN=ghp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
                    excerpt_truncated = $false
                }
            )
            python_tools_diagnostics = [pscustomobject] @{ present = $false; reason = 'n/a' }
        }
        ($diagDoc | ConvertTo-Json -Depth 25) | Set-Content -LiteralPath (Join-Path $tmpDiag 'diag-12345.json') -Encoding UTF8

        Invoke-Full -InFile $candInPath -OutDir $tmpOut -DiagDir $tmpDiag

        $wiOutPath = Join-Path $tmpOut 'wi-12345.json'
        if (-not (Test-Path -LiteralPath $wiOutPath)) {
            Write-Error 'Invoke-Full did not emit the wi file.'; $errors++
        } else {
            $wiOut = Get-Content -LiteralPath $wiOutPath -Raw | ConvertFrom-Json
            if (-not $wiOut.sanitization_aborted) {
                Write-Error 'Secret detected only in diag content but wi sanitization_aborted not propagated.'; $errors++
            }
            if (-not (@($wiOut.secret_hits) -contains 'github-pat-classic')) {
                Write-Error "Expected propagated 'github-pat-classic' in wi.secret_hits, got: $($wiOut.secret_hits -join ', ')"; $errors++
            }
        }
    } finally {
        if (Test-Path -LiteralPath $tmpRoot) { Remove-Item -LiteralPath $tmpRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }

    # Redact-in-place for low-confidence connection-string-pw: customer text
    # with `Password=hunter2` must be redacted in place (preserving the
    # `Password=` prefix so the structure is visible) and MUST NOT trigger
    # the abort drop. Pre-fix behavior: this would flip sanitization_aborted
    # and fetch.yml Step 4 would silently delete the wi/diag pair, shrinking
    # the triage feed on benign customer wording.
    foreach ($t in @(
        'Repro: set Password=hunter2 in your config file',
        'I had to add Pwd=changeme to my appsettings.json',
        'The error happens when Password=test123 is set',
        'Pwd=abcdef should not crash the app',
        'Server=foo;Database=bar;Password=correct-horse-battery-staple;'
    )) {
        $abortHits   = New-Object System.Collections.Generic.List[string]
        $redactHits  = New-Object System.Collections.Generic.List[string]
        $clean = Remove-PiiFromString -Text $t `
                                       -SecretHitsRef   ([ref] $abortHits) `
                                       -RedactedHitsRef ([ref] $redactHits)
        if ($abortHits.Count -ne 0) {
            Write-Error "Low-confidence Password= text triggered ABORT (should redact-in-place): '$t' -> hits=[$($abortHits -join ', ')]"; $errors++
        }
        if (-not ($redactHits -contains 'connection-string-pw')) {
            Write-Error "Low-confidence Password= text did not report 'connection-string-pw' to RedactedHitsRef: '$t' -> redacted=[$($redactHits -join ', ')]"; $errors++
        }
        if ($clean -match '(?i)(Password|Pwd)\s*=\s*[^;<>"\s<]{6,}') {
            Write-Error "Redacted output still contains a Password=/Pwd= match: '$clean'"; $errors++
        }
        if ($clean -notmatch '(?i)(Password|Pwd)=<redacted-secret>') {
            Write-Error "Redaction did not preserve the Password=/Pwd= prefix: '$clean'"; $errors++
        }
    }

    # High-confidence patterns still abort. Spot-check ghp_ and PEM/AKIA.
    foreach ($t in @(
        'token: ghp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
        '-----BEGIN RSA PRIVATE KEY-----abc',
        'access AKIAIOSFODNN7EXAMPLE here'
    )) {
        $abortHits = New-Object System.Collections.Generic.List[string]
        $_ = Remove-PiiFromString -Text $t -SecretHitsRef ([ref] $abortHits)
        if ($abortHits.Count -lt 1) {
            Write-Error "High-confidence pattern did not abort: '$t'"; $errors++
        }
    }

    # Mixed: when both a high-confidence (abort) and low-confidence (redact)
    # pattern appear in the same string, abort still wins (so the item is
    # dropped) AND the low-confidence portion is redacted in the output.
    $mixed = 'Set Password=hunter2 and token=ghp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
    $aMix = New-Object System.Collections.Generic.List[string]
    $rMix = New-Object System.Collections.Generic.List[string]
    $cleanMix = Remove-PiiFromString -Text $mixed `
                                      -SecretHitsRef   ([ref] $aMix) `
                                      -RedactedHitsRef ([ref] $rMix)
    if (-not ($aMix -contains 'github-pat-classic')) {
        Write-Error "Mixed text: abort-class ghp_ not detected. aMix=[$($aMix -join ', ')]"; $errors++
    }
    if (-not ($rMix -contains 'connection-string-pw')) {
        Write-Error "Mixed text: redact-class connection-string-pw not detected. rMix=[$($rMix -join ', ')]"; $errors++
    }
    if ($cleanMix -notmatch 'Password=<redacted-secret>') {
        Write-Error "Mixed text: Password= portion not redacted in cleaned output: '$cleanMix'"; $errors++
    }

    # Sanitize-Candidate: redact-class hit must NOT flip sanitization_aborted,
    # must populate redacted_secrets, and must leave secret_hits empty. Item
    # flows through triage instead of being dropped.
    $candR = [pscustomobject] @{
        id = 901; rev = 1; work_item_type = 'Bug'; state = 'Active';
        area_path = 'x'; tags = ''; url = 'u'; created_date = '2026-01-01';
        comment_count = 0; comments = @(); attachments = @();
        title            = 'plain title'
        description_html = 'Customer says: set Password=hunter2 in appsettings'
        repro_steps_html = ''
        system_info      = ''
    }
    $sanR = Sanitize-Candidate -Candidate $candR
    if ($sanR.sanitization_aborted) {
        Write-Error 'Sanitize-Candidate: low-confidence Password= match triggered abort (should redact instead).'; $errors++
    }
    if (@($sanR.secret_hits).Count -ne 0) {
        Write-Error "Sanitize-Candidate: redact-class match leaked into secret_hits: [$($sanR.secret_hits -join ', ')]"; $errors++
    }
    if (-not (@($sanR.redacted_secrets) -contains 'connection-string-pw')) {
        Write-Error "Sanitize-Candidate: redacted_secrets missing 'connection-string-pw': [$($sanR.redacted_secrets -join ', ')]"; $errors++
    }
    if ($sanR.description_html -match 'hunter2') {
        Write-Error "Sanitize-Candidate: Password value 'hunter2' not redacted from description_html: '$($sanR.description_html)'"; $errors++
    }

    # End-to-end: diag-only redact-class hit propagates to wi.redacted_secrets
    # WITHOUT flipping sanitization_aborted. fetch.yml Step 4 should NOT
    # drop this item.
    $tmpRoot2 = Join-Path ([IO.Path]::GetTempPath()) ("sanitize-test-r-{0}" -f ([Guid]::NewGuid().ToString('N')))
    $tmpDiag2 = Join-Path $tmpRoot2 'diag'
    $tmpOut2  = Join-Path $tmpRoot2 'out'
    New-Item -ItemType Directory -Force -Path $tmpDiag2, $tmpOut2 | Out-Null
    try {
        $candIn2 = @(
            [pscustomobject] @{
                id = 24680; rev = 1; title = 'plain'; description_html = ''; repro_steps_html = ''
                system_info = ''; tags = ''; area_path = 'x'; work_item_type = 'Bug'; state = 'Active'
                created_date = '2026-01-01'; changed_date = '2026-01-01'
                created_by_display = 'X'; created_by_email = 'x@e.com'
                url = 'https://example/24680'; comment_count = 0; comments = @(); attachments = @()
            }
        )
        $candIn2Path = Join-Path $tmpRoot2 'candidates.json'
        ($candIn2 | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $candIn2Path -Encoding UTF8

        $diagDoc2 = [pscustomobject] @{
            work_item = [pscustomobject] @{ id = 24680; title = 'plain'; description = 'fine' }
            attachments = @(
                [pscustomobject] @{
                    name = 'app.config'; url = 'u'; size_bytes = 100; kind = 'text';
                    downloaded = $true; skip_reason = $null;
                    content_excerpt = '<add key="conn" value="Server=db;Password=p@ssw0rd-here;" />'
                    excerpt_truncated = $false
                }
            )
            python_tools_diagnostics = [pscustomobject] @{ present = $false; reason = 'n/a' }
        }
        ($diagDoc2 | ConvertTo-Json -Depth 25) | Set-Content -LiteralPath (Join-Path $tmpDiag2 'diag-24680.json') -Encoding UTF8

        Invoke-Full -InFile $candIn2Path -OutDir $tmpOut2 -DiagDir $tmpDiag2

        $wiR = Get-Content -LiteralPath (Join-Path $tmpOut2 'wi-24680.json') -Raw | ConvertFrom-Json
        if ($wiR.sanitization_aborted) {
            Write-Error 'E2E: diag-only redact-class hit incorrectly flipped sanitization_aborted on the wi.'; $errors++
        }
        if (@($wiR.secret_hits).Count -ne 0) {
            Write-Error "E2E: redact-class diag hit leaked into wi.secret_hits: [$($wiR.secret_hits -join ', ')]"; $errors++
        }
        if (-not (@($wiR.redacted_secrets) -contains 'connection-string-pw')) {
            Write-Error "E2E: wi.redacted_secrets missing propagated 'connection-string-pw': [$($wiR.redacted_secrets -join ', ')]"; $errors++
        }
        # Verify the diag content was actually scrubbed on disk.
        $diagR = Get-Content -LiteralPath (Join-Path $tmpOut2 'diag-24680.json') -Raw
        if ($diagR -match 'p@ssw0rd-here') {
            Write-Error 'E2E: redacted-class password value leaked into sanitized diag file on disk.'; $errors++
        }
        if ($diagR -notmatch 'Password=<redacted-secret>') {
            Write-Error 'E2E: sanitized diag file does not contain the Password=<redacted-secret> marker.'; $errors++
        }
    } finally {
        if (Test-Path -LiteralPath $tmpRoot2) { Remove-Item -LiteralPath $tmpRoot2 -Recurse -Force -ErrorAction SilentlyContinue }
    }

    # Defense-in-depth: abort-class secret must also be REDACTED in place
    # so the on-disk artifact never contains the literal token, even if
    # fetch.yml Step 4 (which drops the wi/diag pair based on
    # sanitization_aborted) is skipped or reordered.
    $abortDefHits = New-Object System.Collections.Generic.List[string]
    $abortDefClean = Remove-PiiFromString -Text 'token: ghp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa here' `
                                          -SecretHitsRef ([ref] $abortDefHits)
    if ($abortDefHits.Count -lt 1 -or -not ($abortDefHits -contains 'github-pat-classic')) {
        Write-Error "Defense-in-depth: abort hit not flagged. hits=[$($abortDefHits -join ', ')]"; $errors++
    }
    if ($abortDefClean -match 'ghp_[A-Za-z0-9]{36}') {
        Write-Error "Defense-in-depth: literal abort-class secret survived in cleaned output: '$abortDefClean'"; $errors++
    }
    if ($abortDefClean -notmatch '<redacted-secret:github-pat-classic>') {
        Write-Error "Defense-in-depth: abort-class marker missing from cleaned output: '$abortDefClean'"; $errors++
    }
    # Same for PEM / AKIA — every abort regex must redact in place.
    foreach ($pair in @(
        @{ in = '-----BEGIN RSA PRIVATE KEY-----abcdef'; name = 'private-key-pem';  leak = '-----BEGIN RSA PRIVATE KEY-----' },
        @{ in = 'access AKIAIOSFODNN7EXAMPLE here';      name = 'aws-access-key';   leak = 'AKIAIOSFODNN7EXAMPLE' }
    )) {
        $h = New-Object System.Collections.Generic.List[string]
        $c = Remove-PiiFromString -Text $pair.in -SecretHitsRef ([ref] $h)
        if (-not ($h -contains $pair.name)) {
            Write-Error "Defense-in-depth: $($pair.name) not detected for input '$($pair.in)'."; $errors++
        }
        if ($c -match [regex]::Escape($pair.leak)) {
            Write-Error "Defense-in-depth: literal $($pair.name) survived: '$c'"; $errors++
        }
        if ($c -notmatch [regex]::Escape("<redacted-secret:$($pair.name)>")) {
            Write-Error "Defense-in-depth: $($pair.name) marker missing: '$c'"; $errors++
        }
    }

    # .Value.Add() (not +=) must preserve the Generic.List[string] type
    # of the caller's secret-hits collection across multiple matches.
    # With +=, a single hit re-materializes the backing field as
    # object[] and future .Add() calls from the caller would break.
    $typedHits = New-Object System.Collections.Generic.List[string]
    $ref       = [ref] $typedHits
    $_ = Remove-PiiFromString -Text 'first ghp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa and second AKIAIOSFODNN7EXAMPLE' `
                              -SecretHitsRef $ref
    if ($ref.Value -isnot [System.Collections.Generic.List[string]]) {
        Write-Error "List type lost after first .Add() — got $($ref.Value.GetType().FullName) instead of List[string]."; $errors++
    }
    # Verify .Add() is still callable on the caller-side variable after
    # the function returns.
    try { $typedHits.Add('manual-after-call') | Out-Null } catch {
        Write-Error "Caller-side .Add() broke after Remove-PiiFromString: $($_.Exception.Message)"; $errors++
    }

    # HtmlDecode pre-pass for wi body fields: entity-encoded PII in
    # description_html / repro_steps_html / system_info must be detected
    # and redacted, not preserved verbatim. Matches the diag path
    # (parse-diagnostics.ps1 ConvertTo-PlainText) which already decodes
    # entities before sanitization.
    $candHtml = [pscustomobject] @{
        id = 555; rev = 1; work_item_type = 'FeedbackTicket'; state = 'DC - New';
        area_path = 'x'; tags = ''; url = 'u'; created_date = '2026-01-01';
        comment_count = 0; comments = @(); attachments = @();
        title            = 'plain title'
        description_html = '<p>Contact alice&#64;example.com about bob&#64;example.com</p>'
        repro_steps_html = '<p>set token to ghp_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb</p>'
        system_info      = 'machine path: C:\Users\jdoe&#92;app.py'
    }
    $sanHtml = Sanitize-Candidate -Candidate $candHtml
    if ($sanHtml.description_html -match 'alice|bob') {
        Write-Error "HtmlDecode pre-pass: encoded email PII survived in description_html: '$($sanHtml.description_html)'"; $errors++
    }
    if ($sanHtml.description_html -notmatch '<redacted-email>') {
        Write-Error "HtmlDecode pre-pass: <redacted-email> marker missing from description_html: '$($sanHtml.description_html)'"; $errors++
    }
    if ($sanHtml.description_html -match '&#64;') {
        Write-Error "HtmlDecode pre-pass: raw entity &#64; left in description_html: '$($sanHtml.description_html)'"; $errors++
    }
    if (-not ($sanHtml.sanitization_aborted)) {
        Write-Error 'HtmlDecode pre-pass: abort-class ghp_ inside encoded repro_steps_html should flip sanitization_aborted.'; $errors++
    }
    if (-not (@($sanHtml.secret_hits) -contains 'github-pat-classic')) {
        Write-Error "HtmlDecode pre-pass: ghp_ in repro_steps_html should reach secret_hits. hits=[$($sanHtml.secret_hits -join ', ')]"; $errors++
    }
    if ($sanHtml.repro_steps_html -match 'ghp_[A-Za-z0-9]{36}') {
        Write-Error "HtmlDecode pre-pass: ghp_ literal survived in repro_steps_html (defense-in-depth + html-decode interaction): '$($sanHtml.repro_steps_html)'"; $errors++
    }

    # Orphan diag with abort-class secret: must be DELETED from -OutDir
    # so it doesn't ride along in the artifact upload. Step 4 keys off
    # sanitization_aborted in wi-*.json; without a sibling wi file Step 4
    # would never see this diag, so we have to fail closed locally.
    $tmpRoot3 = Join-Path ([IO.Path]::GetTempPath()) ("sanitize-test-orphan-{0}" -f ([Guid]::NewGuid().ToString('N')))
    $tmpDiag3 = Join-Path $tmpRoot3 'diag'
    $tmpOut3  = Join-Path $tmpRoot3 'out'
    New-Item -ItemType Directory -Force -Path $tmpDiag3, $tmpOut3 | Out-Null
    try {
        # Candidates list is empty — no wi-*.json will be emitted, so the
        # diag we drop below is a true orphan once sanitization runs.
        $candIn3Path = Join-Path $tmpRoot3 'candidates.json'
        '[]' | Set-Content -LiteralPath $candIn3Path -Encoding UTF8

        $diagDoc3 = [pscustomobject] @{
            work_item = [pscustomobject] @{ id = 77777; title = 'orphan'; description = 'fine' }
            attachments = @(
                [pscustomobject] @{
                    name = 'creds.txt'; url = 'u'; size_bytes = 100; kind = 'text';
                    downloaded = $true; skip_reason = $null;
                    content_excerpt = 'token=ghp_ccccccccccccccccccccccccccccccccccc1'
                    excerpt_truncated = $false
                }
            )
            python_tools_diagnostics = [pscustomobject] @{ present = $false; reason = 'n/a' }
        }
        $diagOrphanPath = Join-Path $tmpDiag3 'diag-77777.json'
        ($diagDoc3 | ConvertTo-Json -Depth 25) | Set-Content -LiteralPath $diagOrphanPath -Encoding UTF8

        Invoke-Full -InFile $candIn3Path -OutDir $tmpOut3 -DiagDir $tmpDiag3

        $orphanInOut = Join-Path $tmpOut3 'diag-77777.json'
        if (Test-Path -LiteralPath $orphanInOut) {
            Write-Error "Orphan diag with abort-class secret survived in OutDir (fail-closed violated): $orphanInOut"; $errors++
        }
    } finally {
        if (Test-Path -LiteralPath $tmpRoot3) { Remove-Item -LiteralPath $tmpRoot3 -Recurse -Force -ErrorAction SilentlyContinue }
    }

    # ConvertFrom-HtmlForScan helper: empty / null safe, leaves plain
    # text untouched, decodes a representative entity set.
    if ((ConvertFrom-HtmlForScan -Text $null) -ne '') { Write-Error 'ConvertFrom-HtmlForScan should return empty on $null.'; $errors++ }
    if ((ConvertFrom-HtmlForScan -Text '')    -ne '') { Write-Error 'ConvertFrom-HtmlForScan should return empty on empty.'; $errors++ }
    if ((ConvertFrom-HtmlForScan -Text 'no entities here') -ne 'no entities here') {
        Write-Error 'ConvertFrom-HtmlForScan should pass plain text through unchanged.'; $errors++
    }
    if ((ConvertFrom-HtmlForScan -Text 'a&#64;b &amp; c &lt;d&gt;') -ne 'a@b & c <d>') {
        Write-Error "ConvertFrom-HtmlForScan didn't decode entities correctly."; $errors++
    }

    if ($errors -gt 0) {
        throw "sanitize.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'sanitize.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }

if (-not $InFile) { throw "-InFile is required (omit only with -SelfTest)." }

if ($TitlesOnly) {
    Invoke-TitlesOnly -InFile $InFile -OutFile $OutFile
} else {
    Invoke-Full -InFile $InFile -OutDir $OutDir -DiagDir $DiagDir -FailOnSecret:$FailOnSecret
}
