<#
    .SYNOPSIS
        Strips PII / secrets / Windows-user paths from work-item content before
        AI inference and before any public mirror to microsoft/PTVS.

    .DESCRIPTION
        Implements Step 4 of the weekly triage workflow (see plan.md §6.3 Step 4).

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

    # Sanitize each diag-<id>.json if present.
    if ($DiagDir -and (Test-Path -LiteralPath $DiagDir)) {
        $diagFiles = Get-ChildItem -LiteralPath $DiagDir -Filter 'diag-*.json' -ErrorAction SilentlyContinue
        foreach ($df in $diagFiles) {
            $diag = Get-Content -LiteralPath $df.FullName -Raw | ConvertFrom-Json
            $secrets = New-Object System.Collections.Generic.List[string]
            $ref = [ref] $secrets
            $diagSan = [pscustomobject] @{}
            foreach ($p in $diag.PSObject.Properties) {
                $v = $p.Value
                if ($v -is [string]) { $v = Remove-PiiFromString -Text $v -SecretHitsRef $ref }
                Add-Member -InputObject $diagSan -NotePropertyName $p.Name -NotePropertyValue $v -Force
            }
            $outPath = Join-Path $OutDir $df.Name
            ($diagSan | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $outPath -Encoding UTF8
            if ($secrets.Count -gt 0) {
                Write-Warning "Secret pattern(s) in $($df.Name): $($secrets -join ', '). Blocking mirror downstream."
                $anyAborted = $true
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
