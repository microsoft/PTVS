<#
    .SYNOPSIS
        Downloads and parses any PythonToolsDiagnostics_*.log attachments on
        each candidate work item.

    .DESCRIPTION
        Implements Step 3 of the workflow (see plan.md §6.3 Step 3). For each
        candidate that has a matching attachment, this script downloads it via
        the AzDO attachment API and writes a small structured JSON
        (diag-<id>.json) under -OutDir containing:

          vs_version, ptvs_version, python_version, debugger_type,
          machine_os, loaded_assemblies (top-N), last_exceptions (top-N),
          source_attachment_name, source_attachment_url.

        The output is intentionally compact — the AI doesn't need the entire
        log, just the headline fields. Skips silently if no matching
        attachment.

        Auth: $env:AZDO_ACCESS_TOKEN.

    .PARAMETER CandidatesFile
        Path to candidates.json.

    .PARAMETER OutDir
        Where to write diag-<id>.json files.

    .PARAMETER MaxLogBytes
        Maximum log size to download (anything larger is skipped to keep
        runtime bounded). Default 4 MB.

    .PARAMETER SelfTest
        Run inline smoke tests.
#>
[CmdletBinding()]
param(
    [string] $CandidatesFile,
    [string] $OutDir,
    [int]    $MaxLogBytes = 4MB,
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-AzdoAuthHeader {
    if ($env:AZDO_ACCESS_TOKEN) { return @{ Authorization = "Bearer $env:AZDO_ACCESS_TOKEN" } }
    if ($env:AZDO_PAT) {
        $b64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$env:AZDO_PAT"))
        return @{ Authorization = "Basic $b64" }
    }
    throw 'No AzDO credentials present (AZDO_ACCESS_TOKEN or AZDO_PAT).'
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

function Get-DiagnosticsAttachment {
    param([object] $Attachment, [hashtable] $Headers, [int] $MaxBytes)
    # The attachment URL is already the GET endpoint; append api-version + download=true.
    $url = $Attachment.url
    if ($url -notmatch '\?') { $url += '?api-version=7.1' }
    else { $url += '&api-version=7.1' }
    if ($url -notmatch '(?i)[?&]download=') { $url += '&download=true' }

    $tmp = [IO.Path]::GetTempFileName()
    try {
        Invoke-WebRequest -Uri $url -Headers $Headers -OutFile $tmp -ErrorAction Stop | Out-Null
        $size = (Get-Item $tmp).Length
        if ($size -gt $MaxBytes) {
            Write-Warning "Attachment $($Attachment.name) exceeds $MaxBytes bytes ($size); skipping."
            return $null
        }
        return Get-Content -LiteralPath $tmp -Raw -ErrorAction Stop
    } catch {
        Write-Warning "Failed to download attachment $($Attachment.name): $($_.Exception.Message)"
        return $null
    } finally {
        if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    }
}

function Invoke-Parse {
    param([string] $CandidatesFile, [string] $OutDir, [int] $MaxLogBytes)
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

    $hits = 0
    foreach ($c in $candidates) {
        $diagAttach = $null
        foreach ($a in @($c.attachments)) {
            if ($a.name -and ($a.name -match '(?i)PythonToolsDiagnostics.*\.log$')) {
                $diagAttach = $a
                break
            }
        }
        if (-not $diagAttach) { continue }

        $text = Get-DiagnosticsAttachment -Attachment $diagAttach -Headers $headers -MaxBytes $MaxLogBytes
        if (-not $text) { continue }

        $fields = Extract-DiagnosticsFields -LogText $text
        $fields | Add-Member -NotePropertyName 'source_attachment_name' -NotePropertyValue $diagAttach.name -Force
        $fields | Add-Member -NotePropertyName 'source_attachment_url'  -NotePropertyValue $diagAttach.url  -Force

        $outPath = Join-Path $OutDir ("diag-{0}.json" -f $c.id)
        ($fields | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $outPath -Encoding UTF8
        $hits++
    }
    Write-Host "Parsed diagnostics for $hits candidate(s) → $OutDir"
}

function Invoke-SelfTest {
    $errors = 0
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

    if ($errors -gt 0) {
        throw "parse-diagnostics.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'parse-diagnostics.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }
if (-not $CandidatesFile) { throw '-CandidatesFile is required.' }
if (-not $OutDir) { throw '-OutDir is required.' }

Invoke-Parse -CandidatesFile $CandidatesFile -OutDir $OutDir -MaxLogBytes $MaxLogBytes
