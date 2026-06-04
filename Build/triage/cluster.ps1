<#
    .SYNOPSIS
        Within-batch duplicate clustering for the triage workflow.

    .DESCRIPTION
        Reads the per-candidate sanitized JSONs from -SanitizedDir, builds a
        compact title+first-paragraph summary per candidate, and produces a
        clusters.json + primaries.json + cluster-meta.json.

        Strategy: a fast offline heuristic — token overlap + title shingle
        similarity (Jaccard). The first cluster whose token set meets the
        Jaccard threshold wins (greedy, first-match) — kept deterministic so
        the same batch produces the same clustering across re-runs.

        Output:
          clusters.json     -> [ [id1, id2, ...], [id3], ... ]    primary first
          primaries.json    -> [ {"id": <int>}, ... ]             matrix-ready
          cluster-meta.json -> [ { primary: {id, title},
                                   followers: [{id, title, similarity}, ...] },
                                 ... ]
                               Consumed by apply-outcomes.ps1 to surface
                               follower titles + similarity scores in
                               run-summary.md so the human approver can see
                               WHY each follower was merged with its primary.

    .PARAMETER SanitizedDir
        Directory containing wi-<id>.json files.

    .PARAMETER ClustersOut
        Path for clusters.json.

    .PARAMETER PrimariesOut
        Path for primaries.json.

    .PARAMETER MetaOut
        Path for cluster-meta.json. Optional; if omitted, written as
        cluster-meta.json next to clusters.json.

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
    [string] $MetaOut,
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
    <#
        Returns: array of [pscustomobject] @{
            primary   = <int>
            followers = @( [pscustomobject] @{ id = <int>; similarity = <double> }, ... )
        }
        Primaries are the first id (lowest id) to land in the cluster; followers
        carry the Jaccard score at the moment they merged.
    #>
    param([Parameter(Mandatory)] [object[]] $Items, [double] $Threshold)

    # Items: [{ id, tokens }]; deterministic order: by id ascending.
    $sorted = @($Items | Sort-Object -Property id)
    $clusters = New-Object System.Collections.Generic.List[object]

    foreach ($it in $sorted) {
        $assigned = $false
        $itTokens = @([string[]] $it.tokens)
        foreach ($cl in $clusters) {
            $existing = [string[]] $cl.tokens
            $j = Get-Jaccard -A $existing -B $itTokens
            if ($j -ge $Threshold) {
                $cl.followers.Add([pscustomobject] @{
                    id         = [int] $it.id
                    similarity = [Math]::Round([double] $j, 3)
                }) | Out-Null
                # Merge token sets (preserve cluster's growing vocabulary so
                # later items can match against the combined fingerprint).
                $merged = New-Object System.Collections.Generic.HashSet[string]
                foreach ($t in $existing) { [void] $merged.Add($t) }
                foreach ($t in $itTokens) { [void] $merged.Add($t) }
                $arr = [string[]]::new($merged.Count)
                $merged.CopyTo($arr)
                $cl.tokens = $arr
                $assigned = $true
                break
            }
        }
        if (-not $assigned) {
            $newC = [pscustomobject] @{
                primary   = [int] $it.id
                followers = New-Object 'System.Collections.Generic.List[object]'
                tokens    = [string[]] $itTokens
            }
            $clusters.Add($newC) | Out-Null
        }
    }

    # Strip the internal `tokens` field and materialize as array of objects.
    # NOTE: `@($list)` where $list is a Generic.List[object] of pscustomobject
    # trips a PS 7.x "Argument types do not match" inside the array sub-
    # expression operator. Use `.ToArray()` everywhere we surface followers.
    $result = New-Object System.Collections.Generic.List[object]
    foreach ($cl in $clusters) {
        $entry = [pscustomobject] @{
            primary   = $cl.primary
            followers = $cl.followers.ToArray()
        }
        [void] $result.Add($entry)
    }
    return ,$result.ToArray()
}

function Invoke-Cluster {
    param(
        [string] $SanitizedDir,
        [string] $ClustersOut,
        [string] $PrimariesOut,
        [string] $MetaOut,
        [double] $Threshold
    )
    if (-not (Test-Path -LiteralPath $SanitizedDir)) { throw "Sanitized dir not found: $SanitizedDir" }
    if (-not $MetaOut) { $MetaOut = Join-Path (Split-Path -Parent $ClustersOut) 'cluster-meta.json' }

    $files = Get-ChildItem -LiteralPath $SanitizedDir -Filter 'wi-*.json' -File
    # idToTitle is used to enrich cluster-meta.json so the run summary can
    # show the human approver WHY each follower was merged (its title + score).
    $idToTitle = @{}
    $items = foreach ($f in $files) {
        $j = Get-Content -LiteralPath $f.FullName -Raw | ConvertFrom-Json
        $tokens = @()
        $tokens += Get-Tokens -Text $j.title
        $tokens += Get-Tokens -Text $j.description_html
        $tokens += Get-Tokens -Text $j.repro_steps_html
        $idToTitle[[int] $j.id] = [string] $j.title
        [pscustomobject] @{
            id     = [int] $j.id
            tokens = @($tokens | Select-Object -Unique)
        }
    }

    if (-not $items -or @($items).Count -eq 0) {
        Set-Content -LiteralPath $ClustersOut  -Value '[]' -Encoding UTF8
        Set-Content -LiteralPath $PrimariesOut -Value '[]' -Encoding UTF8
        Set-Content -LiteralPath $MetaOut      -Value '[]' -Encoding UTF8
        Write-Host 'No sanitized work items found; wrote empty cluster outputs.'
        return
    }

    $clustersRich = Cluster-Candidates -Items @($items) -Threshold $Threshold

    # Legacy clusters.json shape ([[primaryId, followerId, ...], ...]) so
    # apply-outcomes.ps1 doesn't need to change to read it.
    $clustersLegacy = @()
    foreach ($cl in $clustersRich) {
        $row = @([int] $cl.primary)
        foreach ($f in @($cl.followers)) { $row += [int] $f.id }
        $clustersLegacy += ,$row
    }

    $primaries = @()
    foreach ($cl in $clustersRich) {
        $primaries += [pscustomobject] @{ id = [int] $cl.primary }
    }

    # cluster-meta.json — the human-approval-friendly view: each cluster
    # carries its primary's title and a per-follower {id, title, similarity}
    # so the apply-outcomes run summary can surface what each follower's
    # close action was based on.
    $meta = @()
    foreach ($cl in $clustersRich) {
        $followerMeta = @()
        foreach ($f in @($cl.followers)) {
            $followerMeta += [pscustomobject] @{
                id         = [int] $f.id
                title      = ($idToTitle[[int] $f.id])
                similarity = [double] $f.similarity
            }
        }
        $meta += [pscustomobject] @{
            primary   = [pscustomobject] @{ id = [int] $cl.primary; title = $idToTitle[[int] $cl.primary] }
            followers = $followerMeta
        }
    }

    # Write outputs as JSON.
    # -Compress for primaries.json keeps the file single-line; the workflow's
    # `prepare` job uses a heredoc-delimited GITHUB_OUTPUT block so multi-line
    # JSON would technically also work, but the single-line invariant is kept
    # as defense-in-depth (asserted below). clusters.json + cluster-meta.json
    # are downloaded as artifacts and -Compress is purely a size optimization.
    (ConvertTo-Json -InputObject $clustersLegacy -Depth 5 -Compress) | Set-Content -LiteralPath $ClustersOut -Encoding UTF8
    (ConvertTo-Json -InputObject $primaries      -Depth 3 -Compress) | Set-Content -LiteralPath $PrimariesOut -Encoding UTF8
    (ConvertTo-Json -InputObject $meta           -Depth 6 -Compress) | Set-Content -LiteralPath $MetaOut     -Encoding UTF8

    # Defensive assertion: if primaries.json ever drifts off a single line,
    # the workflow's `primaries=$(cat primaries.json)` step would corrupt
    # GITHUB_OUTPUT and the matrix would silently get garbage.
    $primariesText = Get-Content -LiteralPath $PrimariesOut -Raw
    if ($primariesText -match "`r?`n.+") {
        throw "primaries.json must be single-line for GITHUB_OUTPUT consumption (saw multi-line content)."
    }

    Write-Host "Clusters: $(@($clustersRich).Count), primaries: $(@($primaries).Count) -> $ClustersOut, $PrimariesOut, $MetaOut"
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
    foreach ($cl in $clusters) {
        $followerIds = @($cl.followers | ForEach-Object { [int] $_.id })
        if ([int] $cl.primary -eq 1 -and ($followerIds -contains 2)) { $hasMerged = $true; break }
        if ([int] $cl.primary -eq 2 -and ($followerIds -contains 1)) { $hasMerged = $true; break }
    }
    if (-not $hasMerged) { Write-Error 'Expected ids 1 and 2 to share a cluster.'; $errors++ }

    # Followers carry a similarity score in [0,1] at the moment of merge.
    foreach ($cl in $clusters) {
        foreach ($f in @($cl.followers)) {
            if ($null -eq $f.similarity -or [double] $f.similarity -lt 0 -or [double] $f.similarity -gt 1) {
                Write-Error "Follower similarity out of [0,1]: id=$($f.id) score=$($f.similarity)"; $errors++
            }
        }
    }

    if ($errors -gt 0) {
        throw "cluster.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'cluster.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }
if (-not $SanitizedDir) { throw '-SanitizedDir is required.' }
if (-not $ClustersOut)  { throw '-ClustersOut is required.' }
if (-not $PrimariesOut) { throw '-PrimariesOut is required.' }

Invoke-Cluster -SanitizedDir $SanitizedDir -ClustersOut $ClustersOut -PrimariesOut $PrimariesOut -MetaOut $MetaOut -Threshold $Threshold
