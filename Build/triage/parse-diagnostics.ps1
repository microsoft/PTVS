<#
    .SYNOPSIS
        Builds a rich per-candidate diagnostics document combining work-item
        context, every attachment's metadata + small text excerpt, and the
        structured PythonToolsDiagnostics_*.log field extraction.

    .DESCRIPTION
        Step 2 of the pipeline. For each candidate in candidates.json, writes
        a single diag-<id>.json under -OutDir shaped like:

          {
            "work_item":   { id, title, url, work_item_type, state,
                             area_path, created_date, tags,
                             description, repro_steps, system_info },
            "attachments": [ { name, url, size_bytes, kind,
                               downloaded, skip_reason,
                               content_excerpt, excerpt_truncated }, ... ],
            "python_tools_diagnostics": {
              present, reason,
              source_attachment_name, source_attachment_url,
              vs_version, ptvs_version, python_version,
              debugger_type, machine_os,
              loaded_assemblies, last_exceptions
            }
          }

        - HTML fields from the work item (description, repro steps, system
          info) are stripped to plain text so downstream consumers don't have
          to deal with markup. Tags are removed from the *raw* HTML first
          (before HtmlDecode) so that entity-encoded angle brackets like
          `List&lt;int&gt;` or pyproj/XML fragments survive as `List<int>`
          in the output instead of being silently deleted as if they were
          tags. HtmlDecode then runs broadly (not a whitelist) on the
          remaining text so PII / secrets hidden behind entity encoding are
          exposed to sanitize.ps1.
        - Attachment processing is data-driven: every relation with
          rel == 'AttachedFile' is enumerated, classified by extension
          (text-friendly = .log .txt .json .xml .md .csv .config .yaml .yml)
          and pre-gated by AzDO's resourceSize so we don't download a 50 MB
          log just to discover it's too big.
        - For text-friendly attachments within MaxLogBytes, we download via
          the AzDO attachment API, capture the first ExcerptBytes of text as
          content_excerpt, and flag excerpt_truncated when the file was
          larger than that. Per-candidate text download count is capped by
          MaxTextAttachments so a single noisy item can't blow the budget --
          except for the PythonToolsDiagnostics_*.log itself, which is the
          primary structured-extraction source and is always downloaded
          regardless of position in the attachment list. It also doesn't
          count against the budget so it can't be starved out by ordering.
        - For the PythonToolsDiagnostics_*.log specifically, we ALSO run the
          legacy field extractor over the captured text and stash the result
          under python_tools_diagnostics. When no such log is attached or it
          can't be captured, python_tools_diagnostics.present is false and
          python_tools_diagnostics.reason explains why.

        Always emits one diag-<id>.json per candidate — including when there
        are zero attachments — so the artifact has a 1:1 mapping with
        wi-<id>.json and downstream consumers never miss a file.

        Auth: $env:AZDO_ACCESS_TOKEN.

    .PARAMETER CandidatesFile
        Path to candidates.json (from query-azdo.ps1).

    .PARAMETER OutDir
        Where to write diag-<id>.json files.

    .PARAMETER MaxLogBytes
        Maximum attachment size to download (anything larger is skipped to
        keep runtime bounded). Default 4 MB.

    .PARAMETER ExcerptBytes
        How many bytes of decoded text to keep as content_excerpt. Default
        16 KB.

    .PARAMETER MaxTextAttachments
        Cap on the number of text-friendly attachments downloaded per
        candidate (cost-safety). Default 10. Anything beyond the cap is
        recorded with downloaded=$false and skip_reason set.

    .PARAMETER SelfTest
        Run inline smoke tests.
#>
[CmdletBinding()]
param(
    [string] $CandidatesFile,
    [string] $OutDir,
    [int]    $MaxLogBytes        = 4MB,
    [int]    $ExcerptBytes       = 16KB,
    [int]    $MaxTextAttachments = 10,
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Extension allow-list for text-friendly attachments. We download these for
# excerpting; everything else is metadata-only.
$Script:TextFriendlyExt = @('.log', '.txt', '.json', '.xml', '.md', '.csv', '.config', '.yaml', '.yml')

function Get-AzdoAuthHeader {
    if ($env:AZDO_ACCESS_TOKEN) { return @{ Authorization = "Bearer $env:AZDO_ACCESS_TOKEN" } }
    if ($env:AZDO_PAT) {
        $b64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$env:AZDO_PAT"))
        return @{ Authorization = "Basic $b64" }
    }
    throw 'No AzDO credentials present (AZDO_ACCESS_TOKEN or AZDO_PAT).'
}

# Defensive optional-property read. ConvertFrom-Json + StrictMode will throw
# on direct `.foo` access for absent properties, so every read from a JSON
# blob goes through this helper.
function Get-OptionalProp {
    param([object] $Object, [string] $Name, $Default = $null)
    if ($null -eq $Object) { return $Default }
    if (-not $Object.PSObject.Properties[$Name]) { return $Default }
    $v = $Object.$Name
    if ($null -eq $v) { return $Default }
    return $v
}

function ConvertTo-PlainText {
    param([string] $Html)
    if ([string]::IsNullOrEmpty($Html)) { return '' }
    # 1. Replace block-level tags with newlines so paragraph structure
    #    survives, then drop all remaining tags. We drop with the empty
    #    string (not a space) so `<b>foo</b>.` collapses to `foo.` instead
    #    of `foo .` — inline tags shouldn't introduce whitespace.
    #    These two passes run on the *raw* HTML (before HtmlDecode) so that
    #    entity-encoded angle brackets like `List&lt;int&gt;` or
    #    `<MyType&lt;T&gt;>` survive: they have no literal `<`/`>` in the
    #    raw text, so the tag-strip regex can't see them as tags. Doing it
    #    the other way around (decode-then-strip) would turn `List&lt;int&gt;`
    #    into `List<int>` and then silently delete `<int>` as if it were a
    #    tag, destroying exactly the repro detail the artifact is meant to
    #    surface (generic types, XML/pyproj fragments).
    $stripped = [regex]::Replace($Html,     '(?i)<\s*(br|/p|/div|/li|/h[1-6]|/tr)[^>]*>', "`n")
    $stripped = [regex]::Replace($stripped, '<[^>]+>', '')
    # 2. Broad HTML entity decode (NOT a whitelist) — exposes any PII /
    #    secrets that the original poster may have entity-encoded so the
    #    downstream sanitizer's regexes can see them. Runs *after* tag
    #    stripping (see above) so encoded brackets aren't mistaken for tags.
    $decoded = [System.Net.WebUtility]::HtmlDecode($stripped)
    # 3. Collapse runs of whitespace; preserve paragraph breaks.
    $decoded = [regex]::Replace($decoded, '[ \t]+', ' ')
    $decoded = [regex]::Replace($decoded, '(\r?\n\s*){2,}', "`n`n")
    return $decoded.Trim()
}

function Get-AttachmentKind {
    param([string] $Name)
    if ([string]::IsNullOrEmpty($Name)) { return 'binary' }
    $ext = [IO.Path]::GetExtension($Name).ToLowerInvariant()
    if ($Script:TextFriendlyExt -contains $ext) { return 'text' }
    return 'binary'
}

function Test-IsPythonToolsDiagnosticsLog {
    # PTVS attaches its diagnostics dump as `PythonToolsDiagnostics_<ts>.log`
    # (or `PythonToolsDiagnostics.log`). This is the primary structured-
    # extraction source for each candidate: it gets priority handling in the
    # attachment loop (bypasses the per-candidate text-attachment budget and
    # doesn't count against it) so a noisy item with many other text
    # attachments can't starve out the diagnostics log via ordering.
    param([string] $Name)
    if ([string]::IsNullOrEmpty($Name)) { return $false }
    return ($Name -match '(?i)PythonToolsDiagnostics.*\.log$')
}

function Extract-DiagnosticsFields {
    param([Parameter(Mandatory)] [string] $LogText)

    $fields = [ordered] @{
        vs_version            = $null
        ptvs_version          = $null
        python_version        = $null
        debugger_type         = $null
        machine_os            = $null
        loaded_assemblies     = @()
        last_exceptions       = @()
    }

    # Best-effort line scans. PTVS diagnostics files vary across versions, so
    # we use forgiving patterns rather than positional parsing.
    $patterns = @{
        vs_version     = '^\s*(?:Visual Studio Version|VS Version|VsVersion)\s*[:=]\s*(.+)$'
        ptvs_version   = '^\s*(?:PTVS Version|Python Tools Version|PTVSVersion)\s*[:=]\s*(.+)$'
        python_version = '^\s*(?:Python Version|InterpreterVersion)\s*[:=]\s*(.+)$'
        debugger_type  = '^\s*(?:Debugger Type|DebuggerType)\s*[:=]\s*(.+)$'
        machine_os     = '^\s*(?:OS Version|OSVersion|OS)\s*[:=]\s*(.+)$'
    }

    foreach ($line in ($LogText -split "`r?`n")) {
        foreach ($k in @($patterns.Keys)) {
            if (-not $fields[$k] -and ($line -match $patterns[$k])) {
                $fields[$k] = $Matches[1].Trim()
            }
        }
    }

    # Loaded assemblies (best-effort cap).
    $asmMatches = [regex]::Matches($LogText, '(?im)^\s*(?:Loaded Assembly|Assembly)\s*[:=]\s*(?<n>[^\r\n]+)\s*$')
    $asm = @()
    foreach ($m in $asmMatches) {
        $name = $m.Groups['n'].Value.Trim()
        if ($name) { $asm += $name }
        if ($asm.Count -ge 50) { break }
    }
    $fields.loaded_assemblies = @($asm | Select-Object -Unique)

    # Last exceptions (lines containing "Exception" with the next 1-2 lines).
    $excMatches = [regex]::Matches($LogText, '(?ms)^(?<line>.*?\b\w+Exception\b.*?)(?:\r?\n|$)(?<trace>(?:\s+at\s+[^\r\n]+\r?\n){0,4})')
    $exc = @()
    foreach ($m in $excMatches) {
        $exc += ([string]($m.Groups['line'].Value + "`n" + $m.Groups['trace'].Value)).Trim()
        if ($exc.Count -ge 5) { break }
    }
    $fields.last_exceptions = $exc

    return [pscustomobject] $fields
}

function Get-AzdoAttachmentBytes {
    # Downloads an attachment to a temp file and returns the path. The
    # caller is responsible for deleting. Returns $null on failure.
    #
    # When $MaxBytes > 0 we add an HTTP Range header (`bytes=0-$MaxBytes`)
    # so an AzDO relation whose `resourceSize` was omitted (the
    # `vsfeedback-notes.txt` case is exactly this) can't blow the cap by
    # streaming an unbounded download — without the Range, a multi-MB
    # attachment with no advertised size would be fully pulled before
    # being rejected by the post-download `MaxLogBytes` check, defeating
    # part of the cap's bandwidth purpose. AzDO's attachment service
    # supports byte-range serving; servers that ignore the header simply
    # respond 200 with the full body, in which case the caller's post-
    # download size check still rejects oversize files (degraded from
    # bandwidth-capped to bytes-capped at the disk write).
    # The Range request asks for bytes [0..MaxBytes] inclusive, i.e. up
    # to MaxBytes+1 bytes; the caller compares the downloaded length to
    # the original cap (`>` not `>=`) so any read above MaxBytes signals
    # an oversize source.
    param(
        [object]    $Attachment,
        [hashtable] $Headers,
        [int]       $MaxBytes = 0
    )
    $url = $Attachment.url
    if ($url -notmatch '\?') { $url += '?api-version=7.1' } else { $url += '&api-version=7.1' }
    if ($url -notmatch '(?i)[?&]download=') { $url += '&download=true' }
    $reqHeaders = $Headers
    if ($MaxBytes -gt 0) {
        # Clone so we don't mutate the caller's auth-headers hashtable.
        $reqHeaders = @{}
        foreach ($k in $Headers.Keys) { $reqHeaders[$k] = $Headers[$k] }
        $reqHeaders['Range'] = "bytes=0-$MaxBytes"
    }
    $tmp = [IO.Path]::GetTempFileName()
    try {
        Invoke-WebRequest -Uri $url -Headers $reqHeaders -OutFile $tmp -ErrorAction Stop | Out-Null
        return $tmp
    } catch {
        if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
        throw
    }
}

function Get-AttachmentEntry {
    param(
        [object]    $Attachment,
        [hashtable] $Headers,
        [int]       $MaxLogBytes,
        [int]       $ExcerptBytes
    )
    $name = Get-OptionalProp -Object $Attachment -Name 'name'
    $url  = Get-OptionalProp -Object $Attachment -Name 'url'
    $size = Get-OptionalProp -Object $Attachment -Name 'size_bytes'
    $kind = Get-AttachmentKind -Name ([string] $name)

    $entry = [pscustomobject] @{
        name              = $name
        url               = $url
        size_bytes        = $size
        kind              = $kind
        downloaded        = $false
        skip_reason       = $null
        content_excerpt   = $null
        excerpt_truncated = $null
    }

    if ($kind -ne 'text') {
        $entry.skip_reason = 'binary or non-text-friendly extension'
        return $entry
    }

    # Pre-download size gate using AzDO's resourceSize when available, so we
    # don't pull a 50 MB log just to discover it exceeds the cap.
    if ($null -ne $size -and $size -gt $MaxLogBytes) {
        $entry.skip_reason = "size $size bytes exceeds cap $MaxLogBytes"
        return $entry
    }

    $tmp = $null
    try {
        # Pass MaxLogBytes through so Get-AzdoAttachmentBytes range-caps
        # the download. Critical for relations where AzDO omitted
        # resourceSize (the pre-download size gate above is skipped in
        # that case): without the range cap we'd stream the whole file
        # before the post-download check rejects it.
        $tmp = Get-AzdoAttachmentBytes -Attachment $Attachment -Headers $Headers -MaxBytes $MaxLogBytes
        $actualSize = (Get-Item -LiteralPath $tmp).Length
        # Post-download size check guards both (a) the case where AzDO
        # omitted resourceSize on the relation (or it was wrong), and
        # (b) the case where the server ignored our Range header and
        # streamed the whole file anyway.
        if ($null -eq $size) { $entry.size_bytes = $actualSize }
        if ($actualSize -gt $MaxLogBytes) {
            $entry.skip_reason = "downloaded size $actualSize bytes exceeds cap $MaxLogBytes"
            return $entry
        }
        $text = Get-Content -LiteralPath $tmp -Raw -ErrorAction Stop
        $entry.downloaded = $true
        if ($null -ne $text -and $text.Length -gt $ExcerptBytes) {
            $entry.content_excerpt   = $text.Substring(0, $ExcerptBytes)
            $entry.excerpt_truncated = $true
        } else {
            $entry.content_excerpt   = $text
            $entry.excerpt_truncated = $false
        }
    } catch {
        $entry.skip_reason = "download failed: $($_.Exception.Message)"
    } finally {
        if ($tmp -and (Test-Path -LiteralPath $tmp)) {
            Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
        }
    }
    return $entry
}

function New-DiagnosticsStub {
    # Default python_tools_diagnostics block — same shape whether or not
    # we found a parseable PTVS log so downstream consumers see a stable
    # schema.
    param([string] $Reason = 'no PythonToolsDiagnostics_*.log attachment on this work item')
    return [pscustomobject] @{
        present                 = $false
        reason                  = $Reason
        source_attachment_name  = $null
        source_attachment_url   = $null
        vs_version              = $null
        ptvs_version            = $null
        python_version          = $null
        debugger_type           = $null
        machine_os              = $null
        loaded_assemblies       = @()
        last_exceptions         = @()
    }
}

function Get-PythonToolsDiagnosticsBlock {
    # Locates a PythonToolsDiagnostics_*.log entry in the already-processed
    # attachments list, and (if downloaded) runs the field extractor.
    param([System.Collections.IEnumerable] $AttachmentEntries)
    $ptd = New-DiagnosticsStub
    $ptdEntry = $null
    foreach ($e in $AttachmentEntries) {
        $n = Get-OptionalProp -Object $e -Name 'name'
        if (Test-IsPythonToolsDiagnosticsLog -Name ([string] $n)) {
            $ptdEntry = $e
            break
        }
    }
    if (-not $ptdEntry) { return $ptd }

    $ptd.source_attachment_name = $ptdEntry.name
    $ptd.source_attachment_url  = $ptdEntry.url

    if ($ptdEntry.downloaded -and $ptdEntry.content_excerpt) {
        # If excerpt_truncated, we only have the head of the file, but the
        # PTVS diagnostics format puts version headers + initial assemblies
        # + first exceptions near the top, so head-extraction is fine.
        $fields = Extract-DiagnosticsFields -LogText $ptdEntry.content_excerpt
        $ptd.present           = $true
        $ptd.reason            = $null
        $ptd.vs_version        = $fields.vs_version
        $ptd.ptvs_version      = $fields.ptvs_version
        $ptd.python_version    = $fields.python_version
        $ptd.debugger_type     = $fields.debugger_type
        $ptd.machine_os        = $fields.machine_os
        $ptd.loaded_assemblies = @($fields.loaded_assemblies)
        $ptd.last_exceptions   = @($fields.last_exceptions)
    } else {
        $ptd.reason = if ($ptdEntry.skip_reason) { $ptdEntry.skip_reason } else { 'attachment present but content not captured' }
    }
    return $ptd
}

function New-WorkItemBlock {
    # Build the work_item context block. HTML body fields are decoded +
    # stripped to plain text for the consumer.
    param([object] $Candidate)
    return [pscustomobject] @{
        id             = Get-OptionalProp -Object $Candidate -Name 'id'
        title          = Get-OptionalProp -Object $Candidate -Name 'title'
        url            = Get-OptionalProp -Object $Candidate -Name 'url'
        work_item_type = Get-OptionalProp -Object $Candidate -Name 'work_item_type'
        state          = Get-OptionalProp -Object $Candidate -Name 'state'
        area_path      = Get-OptionalProp -Object $Candidate -Name 'area_path'
        created_date   = Get-OptionalProp -Object $Candidate -Name 'created_date'
        tags           = Get-OptionalProp -Object $Candidate -Name 'tags'
        description    = ConvertTo-PlainText -Html ([string] (Get-OptionalProp -Object $Candidate -Name 'description_html' -Default ''))
        repro_steps    = ConvertTo-PlainText -Html ([string] (Get-OptionalProp -Object $Candidate -Name 'repro_steps_html' -Default ''))
        system_info    = ConvertTo-PlainText -Html ([string] (Get-OptionalProp -Object $Candidate -Name 'system_info'      -Default ''))
    }
}

function Invoke-Parse {
    param(
        [string] $CandidatesFile,
        [string] $OutDir,
        [int]    $MaxLogBytes,
        [int]    $ExcerptBytes,
        [int]    $MaxTextAttachments
    )
    if (-not (Test-Path -LiteralPath $CandidatesFile)) { throw "Candidates file not found: $CandidatesFile" }
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

    $headers = Get-AzdoAuthHeader
    $raw = Get-Content -LiteralPath $CandidatesFile -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) {
        Write-Host 'No candidates; nothing to parse.'
        return
    }
    $candidates = $raw | ConvertFrom-Json
    if ($candidates -isnot [System.Array]) { $candidates = @($candidates) }

    foreach ($c in $candidates) {
        $outPath = Join-Path $OutDir ("diag-{0}.json" -f $c.id)

        $workItem = New-WorkItemBlock -Candidate $c
        $allAttachments = @(Get-OptionalProp -Object $c -Name 'attachments' -Default @())

        $entries      = New-Object System.Collections.Generic.List[object]
        $textCount    = 0
        foreach ($a in $allAttachments) {
            if (-not $a) { continue }
            $aname = Get-OptionalProp -Object $a -Name 'name'
            $akind = Get-AttachmentKind -Name ([string] $aname)
            # The PythonToolsDiagnostics_*.log is the primary structured-
            # extraction source; always process it even when the per-candidate
            # text-attachment budget would otherwise skip it (e.g. when ≥
            # MaxTextAttachments noisier text attachments enumerate first in
            # the AzDO relation order). It also doesn't count against the
            # budget so its position in the attachment list can't starve out
            # the diagnostics extraction.
            $isPtdLog = Test-IsPythonToolsDiagnosticsLog -Name ([string] $aname)
            if ($akind -eq 'text' -and -not $isPtdLog -and $textCount -ge $MaxTextAttachments) {
                # Budget exhausted — record metadata only.
                $entries.Add([pscustomobject] @{
                    name              = $aname
                    url               = (Get-OptionalProp -Object $a -Name 'url')
                    size_bytes        = (Get-OptionalProp -Object $a -Name 'size_bytes')
                    kind              = 'text'
                    downloaded        = $false
                    skip_reason       = "per-candidate text-attachment budget ($MaxTextAttachments) exhausted"
                    content_excerpt   = $null
                    excerpt_truncated = $null
                }) | Out-Null
                continue
            }
            $entry = Get-AttachmentEntry -Attachment $a -Headers $headers `
                       -MaxLogBytes $MaxLogBytes -ExcerptBytes $ExcerptBytes
            # PTD logs are exempt from the budget so we don't count them; all
            # other downloaded text attachments do count.
            if ($entry.downloaded -and -not $isPtdLog) { $textCount++ }
            $entries.Add($entry) | Out-Null
        }

        $ptd = Get-PythonToolsDiagnosticsBlock -AttachmentEntries $entries

        # Convert List[object] to a native array BEFORE building the
        # pscustomobject — PowerShell 7.5's @() around a generic List
        # raises "Argument types do not match" inside ConvertTo-Json.
        # (Same fix the pipeline's assemble step uses for the titles list.)
        $attachmentsArr = $entries.ToArray()

        $doc = [pscustomobject] @{
            work_item                = $workItem
            attachments              = $attachmentsArr
            python_tools_diagnostics = $ptd
        }

        ($doc | ConvertTo-Json -Depth 25) | Set-Content -LiteralPath $outPath -Encoding UTF8
    }
    Write-Host "Emitted $($candidates.Count) diag-*.json document(s) -> $OutDir"
}

function Invoke-SelfTest {
    $errors = 0

    # 1. Legacy field-extractor still works (back-compat).
    $sample = @"
Visual Studio Version: 17.10.1
PTVS Version: 17.10.123
Python Version: 3.11.4
Debugger Type: New
OS Version: Windows 11 Build 22631
Loaded Assembly: Microsoft.PythonTools, Version=17.10.123.0
Loaded Assembly: Microsoft.PythonTools.Common, Version=17.10.123.0
System.NullReferenceException: Object reference not set to an instance of an object.
   at Foo.Bar() in Foo.cs:line 12
   at Baz.Qux() in Baz.cs:line 34
"@
    $r = Extract-DiagnosticsFields -LogText $sample
    if ($r.vs_version -ne '17.10.1') { Write-Error "vs_version: '$($r.vs_version)'"; $errors++ }
    if ($r.ptvs_version -ne '17.10.123') { Write-Error "ptvs_version: '$($r.ptvs_version)'"; $errors++ }
    if ($r.python_version -ne '3.11.4') { Write-Error "python_version: '$($r.python_version)'"; $errors++ }
    if ($r.debugger_type -ne 'New') { Write-Error "debugger_type: '$($r.debugger_type)'"; $errors++ }
    if (@($r.loaded_assemblies).Count -lt 2) { Write-Error 'loaded_assemblies: expected ≥ 2'; $errors++ }
    if (@($r.last_exceptions).Count -lt 1)   { Write-Error 'last_exceptions: expected ≥ 1';  $errors++ }
    if (@($r.last_exceptions)[0] -notmatch 'NullReferenceException') { Write-Error 'exception text malformed.'; $errors++ }

    # 2. ConvertTo-PlainText: decodes HTML entities (broad, not whitelist),
    #    drops tags, preserves paragraph breaks for <br>/<p>/<li>/etc.
    $html = '<p>VS crashed at <b>C:\Users\jdoe\app.py</b>.<br/>Contact: alice&#64;example.com &amp; bob&#64;example.com.</p><ul><li>step 1</li><li>step 2</li></ul>'
    $plain = ConvertTo-PlainText -Html $html
    if ($plain -notmatch 'VS crashed at C:\\Users\\jdoe\\app\.py\.') { Write-Error "ConvertTo-PlainText didn't strip tags around path: '$plain'"; $errors++ }
    if ($plain -notmatch 'alice@example\.com & bob@example\.com')   { Write-Error "ConvertTo-PlainText didn't decode &#64; / &amp;: '$plain'"; $errors++ }
    if ($plain -match '<') { Write-Error "ConvertTo-PlainText left HTML tags: '$plain'"; $errors++ }
    if ((ConvertTo-PlainText -Html $null) -ne '') { Write-Error 'ConvertTo-PlainText should return empty on $null.'; $errors++ }

    # 3. Get-AttachmentKind: text-friendly extensions vs binary.
    foreach ($n in @('foo.log', 'bar.txt', 'baz.json', 'qux.xml', 'README.md', 'data.csv', 'app.config', 'pipe.yaml', 'pipe.yml')) {
        if ((Get-AttachmentKind -Name $n) -ne 'text') { Write-Error "Get-AttachmentKind: '$n' should be 'text'."; $errors++ }
    }
    foreach ($n in @('screenshot.png', 'capture.zip', 'crash.dmp', 'movie.mp4', '', 'noext')) {
        if ((Get-AttachmentKind -Name $n) -ne 'binary') { Write-Error "Get-AttachmentKind: '$n' should be 'binary'."; $errors++ }
    }

    # 4. Get-OptionalProp: StrictMode-safe — never throws for missing
    #    properties, returns the default instead.
    $o = [pscustomobject] @{ a = 'x'; b = $null }
    if ((Get-OptionalProp -Object $o    -Name 'a')             -ne 'x')         { Write-Error 'Optional read of present prop broken.'; $errors++ }
    if ((Get-OptionalProp -Object $o    -Name 'b' -Default 'd') -ne 'd')        { Write-Error 'Optional read should treat $null as missing.'; $errors++ }
    if ((Get-OptionalProp -Object $o    -Name 'missing')       -ne $null)      { Write-Error 'Optional read of missing prop should be $null.'; $errors++ }
    if ((Get-OptionalProp -Object $null -Name 'a' -Default 42) -ne 42)         { Write-Error 'Optional read on $null object should yield default.'; $errors++ }

    # 5. New-WorkItemBlock builds the expected shape from a candidate
    #    sample with HTML body fields.
    $cand = [pscustomobject] @{
        id               = 99
        title            = 'sample'
        url              = 'https://example/99'
        work_item_type   = 'Bug'
        state            = 'Active'
        area_path        = 'DevDiv\Python and AI Tools\Python\VS IDE'
        created_date     = '2026-05-08T12:00:00Z'
        tags             = 'vsfeedback'
        description_html = '<p>line 1</p><p>line 2 &amp; more</p>'
        repro_steps_html = '<ol><li>open VS</li><li>open .pyproj</li></ol>'
        system_info      = 'Windows 11, VS 17.10'
    }
    $wi = New-WorkItemBlock -Candidate $cand
    if ($wi.id -ne 99)                              { Write-Error "work_item.id wrong: $($wi.id)"; $errors++ }
    if ($wi.description -notmatch 'line 1')         { Write-Error "work_item.description missing 'line 1'"; $errors++ }
    if ($wi.description -match '<p>')               { Write-Error 'work_item.description should be plain text.'; $errors++ }
    if ($wi.description -notmatch 'line 2 & more')  { Write-Error "work_item.description didn't decode &amp;"; $errors++ }
    if ($wi.repro_steps -notmatch 'open VS')        { Write-Error 'work_item.repro_steps malformed.'; $errors++ }

    # 6. Get-PythonToolsDiagnosticsBlock: produces a clean stub when no PTVS
    #    log attachment is present, and a fully populated block when one is.
    $entriesNone = @(
        [pscustomobject] @{ name = 'screenshot.png'; url = 'u1'; size_bytes = 1024; kind = 'binary';
                            downloaded = $false; skip_reason = 'binary or non-text-friendly extension';
                            content_excerpt = $null; excerpt_truncated = $null }
    )
    $ptdNone = Get-PythonToolsDiagnosticsBlock -AttachmentEntries $entriesNone
    if ($ptdNone.present)                                { Write-Error 'PTD stub should have present=false.'; $errors++ }
    if (-not $ptdNone.reason)                            { Write-Error 'PTD stub should set a reason.';        $errors++ }

    $entriesWithLog = @(
        [pscustomobject] @{ name = 'PythonToolsDiagnostics_xx.log'; url = 'u2'; size_bytes = 1024; kind = 'text';
                            downloaded = $true; skip_reason = $null;
                            content_excerpt = $sample; excerpt_truncated = $false }
    )
    $ptdWith = Get-PythonToolsDiagnosticsBlock -AttachmentEntries $entriesWithLog
    if (-not $ptdWith.present)                           { Write-Error 'PTD with log should have present=true.'; $errors++ }
    if ($ptdWith.vs_version -ne '17.10.1')               { Write-Error 'PTD with log should populate vs_version.'; $errors++ }
    if ($ptdWith.source_attachment_name -notmatch 'PythonToolsDiagnostics') { Write-Error 'PTD should record source_attachment_name.'; $errors++ }

    # 7. Get-PythonToolsDiagnosticsBlock: PTVS log present but skipped
    #    (size cap) — present=$false but the skip_reason is carried over.
    $entriesSkipped = @(
        [pscustomobject] @{ name = 'PythonToolsDiagnostics_big.log'; url = 'u3'; size_bytes = 99999999; kind = 'text';
                            downloaded = $false; skip_reason = 'size 99999999 bytes exceeds cap 4194304';
                            content_excerpt = $null; excerpt_truncated = $null }
    )
    $ptdSkip = Get-PythonToolsDiagnosticsBlock -AttachmentEntries $entriesSkipped
    if ($ptdSkip.present)                                { Write-Error 'PTD with skipped log should have present=false.'; $errors++ }
    if ($ptdSkip.reason -notmatch 'exceeds cap')         { Write-Error 'PTD reason should carry over skip_reason.';      $errors++ }

    # 8. ConvertTo-PlainText: entity-encoded angle brackets MUST survive.
    #    Decode-then-strip ordering (the prior bug) would turn `List&lt;int&gt;`
    #    into `List<int>` and then silently delete `<int>` as if it were a tag.
    #    Strip-then-decode preserves it. This is the repro detail feature
    #    that the artifact exists to surface (generic types, XML / pyproj
    #    fragments).
    $generics = 'use List&lt;int&gt; and Dictionary&lt;K,V&gt; here'
    $genericsPlain = ConvertTo-PlainText -Html $generics
    if ($genericsPlain -ne 'use List<int> and Dictionary<K,V> here') {
        Write-Error "ConvertTo-PlainText destroyed encoded angle brackets: '$genericsPlain'"
        $errors++
    }
    # Same input but with real surrounding tags: real tags are still stripped,
    # encoded brackets still survive.
    $mixed = '<p>before <i>middle</i> &lt;encoded&gt; after</p>'
    $mixedPlain = ConvertTo-PlainText -Html $mixed
    if ($mixedPlain -notmatch 'before middle <encoded> after') {
        Write-Error "ConvertTo-PlainText didn't preserve encoded brackets in mixed HTML: '$mixedPlain'"
        $errors++
    }
    # An XML / pyproj-style fragment with real tags as content (i.e. the
    # user pasted XML inside their repro): encoded `&lt;`/`&gt;` correctly
    # roundtrip to `<`/`>` and aren't mistaken for tags.
    $pyprojSnippet = '<p>Add to .pyproj:</p>&lt;ItemGroup&gt;&lt;Compile Include=&quot;a.py&quot;/&gt;&lt;/ItemGroup&gt;'
    $pyprojPlain = ConvertTo-PlainText -Html $pyprojSnippet
    if ($pyprojPlain -notmatch '<ItemGroup><Compile Include="a\.py"/></ItemGroup>') {
        Write-Error "ConvertTo-PlainText destroyed pyproj fragment: '$pyprojPlain'"
        $errors++
    }

    # 9. Test-IsPythonToolsDiagnosticsLog: case-insensitive, tolerates the
    #    timestamp suffix (PythonToolsDiagnostics_<ts>.log). Used to give
    #    the PTD log priority over the per-candidate text-attachment budget.
    foreach ($n in @('PythonToolsDiagnostics.log',
                      'PythonToolsDiagnostics_20260508.log',
                      'PythonToolsDiagnostics_20260508_120000.log',
                      'pythontoolsdiagnostics_x.LOG')) {
        if (-not (Test-IsPythonToolsDiagnosticsLog -Name $n)) {
            Write-Error "Test-IsPythonToolsDiagnosticsLog: '$n' should match."
            $errors++
        }
    }
    foreach ($n in @('', 'screenshot.png', 'other.log', 'PythonToolsDiagnostics.txt',
                      'crashdump.dmp', 'app.config')) {
        if (Test-IsPythonToolsDiagnosticsLog -Name $n) {
            Write-Error "Test-IsPythonToolsDiagnosticsLog: '$n' should NOT match."
            $errors++
        }
    }
    if (Test-IsPythonToolsDiagnosticsLog -Name $null) {
        Write-Error 'Test-IsPythonToolsDiagnosticsLog should return $false on $null.'; $errors++
    }

    # 10. Attachment-loop budget bypass for the PTVS diagnostics log: even
    #     when MaxTextAttachments text attachments enumerate before the PTD
    #     log, the PTD log is NOT skipped and does NOT count against the
    #     budget. Re-implements the loop's decision logic against an in-memory
    #     attachment list (no network) to verify the priority rule without
    #     having to mock Get-AttachmentEntry.
    $maxText = 10
    $attList = @()
    1..$maxText | ForEach-Object {
        $attList += [pscustomobject] @{ name = "extra-$_.log" }
    }
    # PTD log LAST — exactly the failure pattern in the PR review comment.
    $attList += [pscustomobject] @{ name = 'PythonToolsDiagnostics_20260508.log' }
    $attList += [pscustomobject] @{ name = 'extra-final.txt' }  # past budget AND past PTD
    $simTextCount = 0
    $simProcessed = New-Object System.Collections.Generic.List[string]
    $simSkipped   = New-Object System.Collections.Generic.List[string]
    foreach ($a in $attList) {
        $aname = $a.name
        $akind = Get-AttachmentKind -Name $aname
        $isPtdLog = Test-IsPythonToolsDiagnosticsLog -Name $aname
        if ($akind -eq 'text' -and -not $isPtdLog -and $simTextCount -ge $maxText) {
            $simSkipped.Add($aname) | Out-Null
            continue
        }
        $simProcessed.Add($aname) | Out-Null
        if (-not $isPtdLog) { $simTextCount++ }
    }
    if ($simProcessed -notcontains 'PythonToolsDiagnostics_20260508.log') {
        Write-Error 'PTD log must be processed even when the per-candidate text budget is exhausted.'; $errors++
    }
    if ($simSkipped   -contains    'PythonToolsDiagnostics_20260508.log') {
        Write-Error 'PTD log should never appear in the skipped list.'; $errors++
    }
    if ($simSkipped   -notcontains 'extra-final.txt') {
        Write-Error 'Non-PTD text attachment past budget should be skipped.'; $errors++
    }
    if ($simTextCount -ne $maxText) {
        Write-Error "PTD log should not count against budget; expected textCount=$maxText, got $simTextCount."; $errors++
    }

    # 11. Get-AzdoAttachmentBytes Range-cap: when MaxBytes > 0 the request
    #     MUST carry an HTTP `Range: bytes=0-<MaxBytes>` header so an
    #     unbounded attachment with no advertised resourceSize can't blow
    #     the bandwidth cap. Shadow Invoke-WebRequest locally to capture
    #     the headers actually sent, without making a real network call.
    $script:__captured = [pscustomobject] @{ Uri = $null; Headers = $null; OutFile = $null }
    function script:Invoke-WebRequest {
        param([string] $Uri, [hashtable] $Headers, [string] $Method,
              [string] $OutFile, $ErrorAction)
        $script:__captured.Uri     = $Uri
        $script:__captured.Headers = $Headers
        $script:__captured.OutFile = $OutFile
        if ($OutFile) { [IO.File]::WriteAllBytes($OutFile, [byte[]](0..9)) }
    }
    try {
        $attMock = [pscustomobject] @{ url = 'https://example/_apis/wit/attachments/abc' }
        $base    = @{ Authorization = 'Bearer sample' }

        # MaxBytes > 0: Range header set, base headers preserved, caller's
        # base hashtable not mutated.
        $script:__captured.Uri = $null; $script:__captured.Headers = $null
        $tmp1 = Get-AzdoAttachmentBytes -Attachment $attMock -Headers $base -MaxBytes 4096
        try {
            if (-not $script:__captured.Headers) {
                Write-Error 'Range cap: Invoke-WebRequest was not invoked.'; $errors++
            } else {
                if (-not $script:__captured.Headers.ContainsKey('Range')) {
                    Write-Error 'Range cap: missing Range header when MaxBytes > 0.'; $errors++
                } elseif ($script:__captured.Headers['Range'] -ne 'bytes=0-4096') {
                    Write-Error "Range cap: wrong Range value '$($script:__captured.Headers['Range'])', expected 'bytes=0-4096'."; $errors++
                }
                if ($script:__captured.Headers['Authorization'] -ne 'Bearer sample') {
                    Write-Error 'Range cap: base Authorization header was lost when Range was added.'; $errors++
                }
            }
            if ($base.ContainsKey('Range')) {
                Write-Error 'Range cap: caller-side Headers hashtable was mutated (Range key added).'; $errors++
            }
        } finally {
            if ($tmp1 -and (Test-Path -LiteralPath $tmp1)) { Remove-Item -LiteralPath $tmp1 -Force -ErrorAction SilentlyContinue }
        }

        # MaxBytes default (0): NO Range header — keep the legacy code path
        # for callers that don't need a cap.
        $script:__captured.Headers = $null
        $tmp2 = Get-AzdoAttachmentBytes -Attachment $attMock -Headers $base
        try {
            if (-not $script:__captured.Headers) {
                Write-Error 'Range cap (off): Invoke-WebRequest was not invoked.'; $errors++
            } elseif ($script:__captured.Headers.ContainsKey('Range')) {
                Write-Error 'Range cap (off): Range header set when MaxBytes was 0.'; $errors++
            }
        } finally {
            if ($tmp2 -and (Test-Path -LiteralPath $tmp2)) { Remove-Item -LiteralPath $tmp2 -Force -ErrorAction SilentlyContinue }
        }
    } finally {
        # Restore the real cmdlet by removing the function shadow.
        Remove-Item Function:script:Invoke-WebRequest -ErrorAction SilentlyContinue
        $script:__captured = $null
    }

    if ($errors -gt 0) {
        throw "parse-diagnostics.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'parse-diagnostics.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }
if (-not $CandidatesFile) { throw '-CandidatesFile is required.' }
if (-not $OutDir) { throw '-OutDir is required.' }

Invoke-Parse -CandidatesFile $CandidatesFile -OutDir $OutDir `
             -MaxLogBytes $MaxLogBytes -ExcerptBytes $ExcerptBytes `
             -MaxTextAttachments $MaxTextAttachments
