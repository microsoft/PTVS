<#
    .SYNOPSIS
        Within-batch duplicate clustering for the triage workflow.

    .DESCRIPTION
        Reads the per-candidate sanitized JSONs from -SanitizedDir, builds a
        compact title+first-paragraph summary per candidate, and produces a
        clusters.json + primaries.json.

        Strategy: a fast offline heuristic — token overlap + title shingle
        similarity (Jaccard). The plan also describes an optional AI-based
        clusterer; for v1 we keep this step offline and deterministic to
        avoid an extra round-trip to the model server before Job 2's main
        triage.

        Output:
          clusters.json  -> [ [id1, id2, ...], [id3], ... ]    primary first
          primaries.json -> [ {"id": <int>}, ... ]             matrix-ready

    .PARAMETER SanitizedDir
        Directory containing wi-<id>.json files.

    .PARAMETER ClustersOut
        Path for clusters.json.

    .PARAMETER PrimariesOut
        Path for primaries.json.

    .PARAMETER Threshold
        Jaccard similarity threshold for merging (0..1). Default 0.55.

    .PARAMETER SelfTest
        Run inline smoke tests.
#>
[CmdletBinding()]
param(
    [string] $SanitizedDir,
    [string] $ClustersOut,
    [string] $PrimariesOut,
    [double] $Threshold = 0.55,
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Tokens {
    param([string] $Text)
    if (-not $Text) { return @() }
    $clean = ($Text -replace '<[^>]+>', ' ')           # strip HTML tags
    $clean = ($clean -replace '[^\w\s]', ' ').ToLowerInvariant()
    $words = $clean -split '\s+' | Where-Object { $_ -and $_.Length -ge 3 }
    return @($words | Select-Object -Unique)
}

function Get-Jaccard {
    param([string[]] $A, [string[]] $B)
    if (($null -eq $A -or $A.Count -eq 0) -and ($null -eq $B -or $B.Count -eq 0)) { return 0.0 }
    if ($null -eq $A -or $null -eq $B -or $A.Count -eq 0 -or $B.Count -eq 0) { return 0.0 }
    $setA = [System.Collections.Generic.HashSet[string]]::new([string[]] $A)
    $setB = [System.Collections.Generic.HashSet[string]]::new([string[]] $B)
    $inter = [System.Collections.Generic.HashSet[string]]::new($setA)
    $inter.IntersectWith($setB)
    $union = [System.Collections.Generic.HashSet[string]]::new($setA)
    $union.UnionWith($setB)
    if ($union.Count -eq 0) { return 0.0 }
    return [double] $inter.Count / [double] $union.Count
}

function Cluster-Candidates {
    param([Parameter(Mandatory)] [object[]] $Items, [double] $Threshold)

    # Items: [{ id, tokens }]; deterministic order: by id ascending.
    $sorted = @($Items | Sort-Object -Property id)
    $clusters = New-Object System.Collections.Generic.List[System.Collections.Generic.List[int]]
    # Use List<object> to hold string[] entries — List<string[]> trips up
    # PowerShell's dynamic method dispatch on indexer-set in some pwsh builds.
    $clusterTokens = New-Object System.Collections.Generic.List[object]

    foreach ($it in $sorted) {
        $assigned = $false
        $itTokens = @([string[]] $it.tokens)
        for ($i = 0; $i -lt $clusters.Count; $i++) {
            $existing = [string[]] $clusterTokens[$i]
            $j = Get-Jaccard -A $existing -B $itTokens
            if ($j -ge $Threshold) {
                $clusters[$i].Add([int] $it.id)
                # Merge into a new string[] (HashSet.ToArray isn't reliably
                # callable via PS dynamic dispatch).
                $merged = New-Object System.Collections.Generic.HashSet[string]
                foreach ($t in $existing) { [void] $merged.Add($t) }
                foreach ($t in $itTokens) { [void] $merged.Add($t) }
                $arr = [string[]]::new($merged.Count)
                $merged.CopyTo($arr)
                $clusterTokens[$i] = $arr
                $assigned = $true
                break
            }
        }
        if (-not $assigned) {
            $newC = New-Object System.Collections.Generic.List[int]
            $newC.Add([int] $it.id)
            $clusters.Add($newC) | Out-Null
            $clusterTokens.Add([string[]] $itTokens) | Out-Null
        }
    }
    # Materialize as array of int[] without piping (ForEach-Object would
    # unroll the inner lists and we'd lose the cluster grouping).
    $result = New-Object System.Collections.Generic.List[object]
    foreach ($cl in $clusters) {
        # Wrap each cluster's int[] in a 1-element object[] so List<object>'s
        # Add doesn't unroll it. The trailing `[0]` strip is in the caller's
        # iteration.
        $inner = New-Object 'System.Collections.Generic.List[int]'
        foreach ($x in $cl) { $inner.Add([int] $x) | Out-Null }
        $result.Add($inner) | Out-Null
    }
    # Return a regular array of int[]. We materialize to int[] here to give
    # callers a stable shape (each element is an int[]).
    $final = New-Object System.Collections.Generic.List[object]
    foreach ($lst in $result) {
        $final.Add([int[]] $lst.ToArray()) | Out-Null
    }
    return ,$final.ToArray()
}

function Invoke-Cluster {
    param([string] $SanitizedDir, [string] $ClustersOut, [string] $PrimariesOut, [double] $Threshold)
    if (-not (Test-Path -LiteralPath $SanitizedDir)) { throw "Sanitized dir not found: $SanitizedDir" }

    $files = Get-ChildItem -LiteralPath $SanitizedDir -Filter 'wi-*.json' -File
    $items = foreach ($f in $files) {
        $j = Get-Content -LiteralPath $f.FullName -Raw | ConvertFrom-Json
        $tokens = @()
        $tokens += Get-Tokens -Text $j.title
        $tokens += Get-Tokens -Text $j.description_html
        $tokens += Get-Tokens -Text $j.repro_steps_html
        [pscustomobject] @{
            id     = [int] $j.id
            tokens = @($tokens | Select-Object -Unique)
        }
    }

    if (-not $items -or @($items).Count -eq 0) {
        Set-Content -LiteralPath $ClustersOut  -Value '[]' -Encoding UTF8
        Set-Content -LiteralPath $PrimariesOut -Value '[]' -Encoding UTF8
        Write-Host 'No sanitized work items found; wrote empty cluster outputs.'
        return
    }

    $clusters = Cluster-Candidates -Items @($items) -Threshold $Threshold
    $primaries = @()
    foreach ($c in $clusters) {
        $primaries += [pscustomobject] @{ id = [int] $c[0] }
    }

    # Write outputs as JSON.
    # ConvertTo-Json with -InputObject keeps a single outer wrapper array
    # even when the array has one element; piping would unroll for length 1.
    (ConvertTo-Json -InputObject $clusters -Depth 5 -Compress) | Set-Content -LiteralPath $ClustersOut -Encoding UTF8
    # primaries.json is consumed directly as a GH Actions matrix list; compact JSON only.
    (ConvertTo-Json -InputObject $primaries -Depth 3 -Compress) | Set-Content -LiteralPath $PrimariesOut -Encoding UTF8
    Write-Host "Clusters: $(@($clusters).Count), primaries: $(@($primaries).Count) → $ClustersOut, $PrimariesOut"
}

function Invoke-SelfTest {
    $errors = 0
    $a = Get-Tokens 'Visual Studio crashes when opening a Python project'
    $b = Get-Tokens 'VS crashes on Python project open'
    $j = Get-Jaccard -A $a -B $b
    if ($j -lt 0.3) { Write-Error "Expected similar phrases to score ≥ 0.3 — got $j."; $errors++ }

    $c = Get-Tokens 'Adding pip packages fails with proxy error'
    $j2 = Get-Jaccard -A $a -B $c
    if ($j2 -ge 0.3) { Write-Error "Expected unrelated phrases to score < 0.3 — got $j2."; $errors++ }

    # Clustering merges similar items.
    $items = @(
        [pscustomobject] @{ id = 1; tokens = $a },
        [pscustomobject] @{ id = 2; tokens = $b },
        [pscustomobject] @{ id = 3; tokens = $c }
    )
    $clusters = Cluster-Candidates -Items $items -Threshold 0.3
    if ($clusters.Count -ne 2) { Write-Error "Expected 2 clusters, got $($clusters.Count)."; $errors++ }
    $hasMerged = $false
    foreach ($cl in $clusters) { if ($cl.Count -ge 2 -and ($cl -contains 1) -and ($cl -contains 2)) { $hasMerged = $true; break } }
    if (-not $hasMerged) { Write-Error 'Expected ids 1 and 2 to share a cluster.'; $errors++ }

    if ($errors -gt 0) {
        throw "cluster.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'cluster.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }
if (-not $SanitizedDir) { throw '-SanitizedDir is required.' }
if (-not $ClustersOut)  { throw '-ClustersOut is required.' }
if (-not $PrimariesOut) { throw '-PrimariesOut is required.' }

Invoke-Cluster -SanitizedDir $SanitizedDir -ClustersOut $ClustersOut -PrimariesOut $PrimariesOut -Threshold $Threshold
