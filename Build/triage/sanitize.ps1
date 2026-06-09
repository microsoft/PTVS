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

        Behavior on secret-pattern hit: ABORT that candidate. The script either
        marks it with a `sanitization_aborted` flag (kept out of public mirror)
        or — when -FailOnSecret is set — exits non-zero. Default is to flag.

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

# Patterns. These are intentionally generous — false positives on customer
# content are cheap (just an extra `<redacted>`) while false negatives are
# expensive (a PII leak on the public GitHub).
$Script:EmailRegex     = '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}'
$Script:WinUserPath    = '(?i)([A-Z]:\\Users\\)([^\\/<>"|?*\s]+)'
$Script:UnixHome       = '(?i)(/home/|/Users/)([A-Za-z0-9._-]+)'
$Script:SidRegex       = 'S-1-[0-9-]{6,}'
$Script:MachineNameLn  = '(?im)^\s*(Machine Name|MachineName|Computer Name|HostName)\s*[:=]\s*[^\r\n]+'
$Script:PhoneRegex     = '(?:\+?\d{1,3}[\s.-]?)?(?:\(?\d{3}\)?[\s.-]?)\d{3}[\s.-]?\d{4}'

# Secret regexes — fail loudly when these match in user content.
$Script:SecretRegexes = @(
    @{ name = 'github-pat-classic';   pattern = 'ghp_[A-Za-z0-9]{36}' },
    @{ name = 'github-pat-fine';      pattern = 'github_pat_[A-Za-z0-9_]{82}' },
    @{ name = 'github-app-token';     pattern = '(?:ghs|gho|ghu|ghr)_[A-Za-z0-9]{36}' },
    @{ name = 'azure-storage-key';    pattern = 'AccountKey=[A-Za-z0-9+/=]{40,}' },
    @{ name = 'sas-token';            pattern = '[?&]sig=[A-Za-z0-9%+/=]{20,}' },
    @{ name = 'jwt';                  pattern = 'eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}' },
    @{ name = 'aws-access-key';       pattern = 'AKIA[0-9A-Z]{16}' },
    @{ name = 'private-key-pem';      pattern = '-----BEGIN (?:RSA |EC |OPENSSH |DSA |PGP )?PRIVATE KEY-----' },
    @{ name = 'connection-string-pw'; pattern = '(?i)(Password|Pwd)\s*=\s*[^;<>"\s]{6,}' }
)

function Remove-PiiFromString {
    param(
        [Parameter()] [string] $Text,
        [ref] $SecretHitsRef
    )
    if ([string]::IsNullOrEmpty($Text)) { return $Text }

    $result = $Text

    # 1. Secret detection (no redaction — we abort or flag instead).
    foreach ($s in $Script:SecretRegexes) {
        if ($result -match $s.pattern) {
            if ($null -ne $SecretHitsRef) { $SecretHitsRef.Value += $s.name }
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
        [ref] $SecretHitsRef
    )
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) {
        return Remove-PiiFromString -Text $Value -SecretHitsRef $SecretHitsRef
    }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $newObj = [pscustomobject] @{}
        foreach ($p in $Value.PSObject.Properties) {
            Add-Member -InputObject $newObj -NotePropertyName $p.Name `
                -NotePropertyValue (Remove-PiiFromValue -Value $p.Value -SecretHitsRef $SecretHitsRef) -Force
        }
        return $newObj
    }
    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
        $items = New-Object System.Collections.Generic.List[object]
        foreach ($i in $Value) {
            $items.Add((Remove-PiiFromValue -Value $i -SecretHitsRef $SecretHitsRef)) | Out-Null
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

function Sanitize-Candidate {
    param([Parameter(Mandatory)] [object] $Candidate)
    $secrets = New-Object System.Collections.Generic.List[string]
    $secretsRef = [ref] $secrets

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

        title              = Remove-PiiFromString -Text $Candidate.title             -SecretHitsRef $secretsRef
        description_html   = Remove-PiiFromString -Text $Candidate.description_html  -SecretHitsRef $secretsRef
        repro_steps_html   = Remove-PiiFromString -Text $Candidate.repro_steps_html  -SecretHitsRef $secretsRef
        system_info        = Remove-PiiFromString -Text $Candidate.system_info       -SecretHitsRef $secretsRef
        comments           = @(
            foreach ($c in @($Candidate.comments)) {
                [pscustomobject] @{
                    id          = $c.id
                    createdDate = $c.createdDate
                    text        = Remove-PiiFromString -Text $c.text -SecretHitsRef $secretsRef
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
        secret_hits          = @($secrets | Sort-Object -Unique)
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
    # we use Remove-PiiFromValue to walk the whole tree. Any secret hit
    # inside a diag file gets PROPAGATED back to the matching wi-<id>.json:
    # fetch.yml Step 4 reads sanitization_aborted off the wi file to decide
    # whether to drop the pair, so a secret found only in diag content must
    # flip the wi flag too — otherwise the diag file would still ride along
    # in the AzDO artifact upload.
    if ($DiagDir -and (Test-Path -LiteralPath $DiagDir)) {
        $diagFiles = Get-ChildItem -LiteralPath $DiagDir -Filter 'diag-*.json' -ErrorAction SilentlyContinue
        foreach ($df in $diagFiles) {
            $diag = Get-Content -LiteralPath $df.FullName -Raw | ConvertFrom-Json
            $secrets = New-Object System.Collections.Generic.List[string]
            $diagSan = Remove-PiiFromValue -Value $diag -SecretHitsRef ([ref] $secrets)
            $outPath = Join-Path $OutDir $df.Name
            ($diagSan | ConvertTo-Json -Depth 25) | Set-Content -LiteralPath $outPath -Encoding UTF8

            if ($secrets.Count -gt 0) {
                $uniq = @($secrets | Sort-Object -Unique)
                Write-Warning "Secret pattern(s) in $($df.Name): $($uniq -join ', '). Blocking mirror downstream."
                $anyAborted = $true

                # Find the sibling wi-<id>.json and flip sanitization_aborted
                # so fetch.yml Step 4's drop loop catches both files.
                $idMatch = [regex]::Match($df.Name, '^diag-(\d+)\.json$')
                if ($idMatch.Success) {
                    $wiPath = Join-Path $OutDir ("wi-{0}.json" -f $idMatch.Groups[1].Value)
                    if (Test-Path -LiteralPath $wiPath) {
                        $wi = Get-Content -LiteralPath $wiPath -Raw | ConvertFrom-Json
                        $wi.sanitization_aborted = $true
                        $existing = @()
                        if ($wi.PSObject.Properties['secret_hits'] -and $wi.secret_hits) {
                            $existing = @($wi.secret_hits)
                        }
                        $wi.secret_hits = @(($existing + $uniq) | Sort-Object -Unique)
                        ($wi | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $wiPath -Encoding UTF8
                        Write-Warning "Propagated diag secret hit(s) to $(Split-Path $wiPath -Leaf): $($wi.secret_hits -join ', ')."
                    } else {
                        Write-Warning "No matching wi-$($idMatch.Groups[1].Value).json next to $($df.Name); diag will still be dropped by Step 4 if a sibling appears later."
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
