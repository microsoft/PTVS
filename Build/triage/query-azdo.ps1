<#
    .SYNOPSIS
        Queries Azure DevOps for open work items under DevDiv\Python and AI Tools\**
        that were created within the lookback window, and writes them to a JSON file.

    .DESCRIPTION
        Implements Step 1 of the weekly triage workflow (see plan.md §6.3 Step 1).
        Used by both Job 1 (weekly report) and Job 2 (triage prepare).

        Authentication: expects an AzDO-resource bearer token in $env:AZDO_ACCESS_TOKEN
        (minted by `az account get-access-token --resource 499b84ac-...`).
        As a fallback, accepts $env:AZDO_PAT for the Basic-auth path.

    .PARAMETER OutFile
        Path to write the candidates JSON. Required.

    .PARAMETER LookbackDays
        How many days back to scan. Default 7.

    .PARAMETER MaxCandidates
        Hard cap on the number of candidates emitted. Default 25.

    .PARAMETER WorkItemId
        If set, bypass the date filter and process only this single work item ID.
        Used for the ad-hoc replay path on workflow_dispatch.

    .PARAMETER ConfigPath
        Path to Build/triage/config.json. Defaults to the sibling file.

    .PARAMETER SelfTest
        Runs in-script smoke tests against fixtures and exits.
#>
[CmdletBinding()]
param(
    [string] $OutFile,
    [int]    $LookbackDays = 7,
    [int]    $MaxCandidates = 25,
    [string] $WorkItemId,
    [string] $ConfigPath = (Join-Path $PSScriptRoot 'config.json'),
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-TriageConfig {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Config file not found: $Path"
    }
    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}

function Get-AzdoAuthHeader {
    if ($env:AZDO_ACCESS_TOKEN) {
        return @{ Authorization = "Bearer $env:AZDO_ACCESS_TOKEN" }
    }
    if ($env:AZDO_PAT) {
        $b64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$env:AZDO_PAT"))
        return @{ Authorization = "Basic $b64" }
    }
    throw "No AzDO credentials present. Set AZDO_ACCESS_TOKEN (preferred) or AZDO_PAT."
}

function Build-WiqlQuery {
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [int]    $LookbackDays
    )
    # Quote-escape the area path. WIQL uses single quotes for string literals
    # and supports backslashes in area paths verbatim.
    $area = $Config.azdo.areaPath
    $types = ($Config.azdo.workItemTypes | ForEach-Object { "'$_'" }) -join ', '
    $excludedStates = ($Config.azdo.excludedStates | ForEach-Object { "[System.State] <> '$_'" }) -join ' AND '
    $excludedTagsClause = ($Config.azdo.excludedTags | ForEach-Object { "NOT [System.Tags] CONTAINS '$_'" }) -join ' AND '

    $wiql = @"
SELECT [System.Id]
FROM   workitems
WHERE  [System.TeamProject] = '$($Config.azdo.project)'
  AND  [System.AreaPath] UNDER '$area'
  AND  [System.WorkItemType] IN ($types)
  AND  [System.CreatedDate] >= @Today - $LookbackDays
  AND  $excludedStates
  AND  $excludedTagsClause
ORDER BY [System.CreatedDate] ASC
"@
    return $wiql
}

function Invoke-AzdoRestWithRetry {
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Uri,
        [Parameter()] [hashtable] $Headers,
        [Parameter()] [object] $Body,
        [Parameter()] [string] $ContentType = 'application/json',
        [int] $MaxAttempts = 3
    )
    $attempt = 0
    $lastError = $null
    while ($attempt -lt $MaxAttempts) {
        $attempt++
        try {
            $params = @{
                Method  = $Method
                Uri     = $Uri
                Headers = $Headers
                ContentType = $ContentType
            }
            if ($null -ne $Body) {
                if ($Body -is [string]) {
                    $params.Body = $Body
                } else {
                    $params.Body = ($Body | ConvertTo-Json -Depth 20 -Compress)
                }
            }
            return Invoke-RestMethod @params
        } catch {
            $lastError = $_
            $status = $null
            if ($_.Exception.Response) { $status = [int] $_.Exception.Response.StatusCode }
            # Retry only on transient 5xx / 429.
            if ($status -ge 500 -or $status -eq 429) {
                $sleep = [Math]::Pow(2, $attempt)
                Write-Warning "AzDO REST $Method $Uri failed (HTTP $status). Retry $attempt/$MaxAttempts after ${sleep}s."
                Start-Sleep -Seconds $sleep
                continue
            }
            throw
        }
    }
    throw $lastError
}

function Get-CandidateIds {
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [int]    $LookbackDays,
        [Parameter(Mandatory)] [hashtable] $Headers,
        [string] $ExplicitWorkItemId
    )

    if ($ExplicitWorkItemId) {
        return @([int] $ExplicitWorkItemId)
    }

    $wiql = Build-WiqlQuery -Config $Config -LookbackDays $LookbackDays
    $uri = "$($Config.azdo.baseUrl)/$($Config.azdo.project)/_apis/wit/wiql?api-version=7.1"
    Write-Host "WIQL endpoint: $uri"

    $body = @{ query = $wiql }
    $response = Invoke-AzdoRestWithRetry -Method POST -Uri $uri -Headers $Headers -Body $body

    if (-not $response.workItems) { return @() }
    return @($response.workItems | ForEach-Object { [int] $_.id })
}

function Get-WorkItemBatch {
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter()] [AllowEmptyCollection()] [int[]]  $Ids,
        [Parameter(Mandatory)] [hashtable] $Headers
    )
    if (-not $Ids -or $Ids.Count -eq 0) { return @() }
    $uri = "$($Config.azdo.baseUrl)/_apis/wit/workitemsbatch?api-version=7.1"
    $batchSize = 200
    $all = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $Ids.Count; $i += $batchSize) {
        $slice = $Ids[$i..([Math]::Min($i + $batchSize - 1, $Ids.Count - 1))]
        $body = @{
            ids = @($slice)
            '$expand' = 'All'
        }
        $resp = Invoke-AzdoRestWithRetry -Method POST -Uri $uri -Headers $Headers -Body $body
        if ($resp.value) { $all.AddRange([object[]] $resp.value) }
    }
    return $all.ToArray()
}

function Get-WorkItemComments {
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [int]    $Id,
        [Parameter(Mandatory)] [hashtable] $Headers
    )
    $uri = "$($Config.azdo.baseUrl)/$($Config.azdo.project)/_apis/wit/workItems/$Id/comments?api-version=7.0-preview.3"
    try {
        $resp = Invoke-AzdoRestWithRetry -Method GET -Uri $uri -Headers $Headers
        if ($resp -and (Get-Member -InputObject $resp -Name comments -MemberType Properties -ErrorAction SilentlyContinue)) {
            return @($resp.comments | ForEach-Object {
                [pscustomobject] @{
                    id           = $_.id
                    createdBy    = $_.createdBy.displayName
                    createdDate  = $_.createdDate
                    text         = $_.text
                }
            })
        }
    } catch {
        Write-Warning "Failed to read comments for WI #${Id}: $($_.Exception.Message)"
    }
    return @()
}

function Convert-WorkItemToCandidate {
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [object] $WorkItem,
        [Parameter()] [object[]] $Comments = @()
    )

    $fields = $WorkItem.fields
    $f = @{}
    foreach ($p in $fields.PSObject.Properties) { $f[$p.Name] = $p.Value }

    $attachments = @()
    if ($WorkItem.PSObject.Properties['relations']) {
        foreach ($rel in @($WorkItem.relations)) {
            if ($rel.rel -eq 'AttachedFile') {
                $url = $rel.url
                $name = $null
                if ($rel.attributes -and $rel.attributes.name) { $name = $rel.attributes.name }
                $attachments += [pscustomobject] @{
                    name = $name
                    url  = $url
                }
            }
        }
    }

    $created = $null
    if ($f.ContainsKey('System.CreatedDate')) { $created = $f['System.CreatedDate'] }
    $createdByDisplay = $null; $createdByEmail = $null
    if ($f.ContainsKey('System.CreatedBy')) {
        $cb = $f['System.CreatedBy']
        if ($cb -is [string]) { $createdByDisplay = $cb }
        elseif ($cb -and $cb.PSObject.Properties['displayName']) { $createdByDisplay = $cb.displayName }
        if ($cb -and $cb.PSObject.Properties['uniqueName']) { $createdByEmail = $cb.uniqueName }
    }

    return [pscustomobject] @{
        id                = $WorkItem.id
        rev               = if ($f.ContainsKey('System.Rev')) { $f['System.Rev'] } else { $null }
        title             = if ($f.ContainsKey('System.Title')) { $f['System.Title'] } else { '' }
        description_html  = if ($f.ContainsKey('System.Description')) { $f['System.Description'] } else { '' }
        repro_steps_html  = if ($f.ContainsKey('Microsoft.VSTS.TCM.ReproSteps')) { $f['Microsoft.VSTS.TCM.ReproSteps'] } else { '' }
        system_info       = if ($f.ContainsKey('Microsoft.VSTS.TCM.SystemInfo')) { $f['Microsoft.VSTS.TCM.SystemInfo'] } else { '' }
        tags              = if ($f.ContainsKey('System.Tags')) { $f['System.Tags'] } else { '' }
        area_path         = if ($f.ContainsKey('System.AreaPath')) { $f['System.AreaPath'] } else { '' }
        work_item_type    = if ($f.ContainsKey('System.WorkItemType')) { $f['System.WorkItemType'] } else { '' }
        state             = if ($f.ContainsKey('System.State')) { $f['System.State'] } else { '' }
        created_date      = $created
        created_by_display = $createdByDisplay
        created_by_email   = $createdByEmail
        url               = "$($Config.azdo.baseUrl)/$($Config.azdo.project)/_workitems/edit/$($WorkItem.id)"
        comment_count     = $Comments.Count
        comments          = $Comments
        attachments       = $attachments
    }
}

function Invoke-Query {
    param(
        [Parameter(Mandatory)] [string] $OutFile,
        [Parameter(Mandatory)] [int]    $LookbackDays,
        [Parameter(Mandatory)] [int]    $MaxCandidates,
        [string] $WorkItemId,
        [Parameter(Mandatory)] [object] $Config
    )

    $headers = Get-AzdoAuthHeader
    $ids = Get-CandidateIds -Config $Config -LookbackDays $LookbackDays -Headers $headers -ExplicitWorkItemId $WorkItemId
    Write-Host "WIQL returned $($ids.Count) candidate ID(s)."

    if ($ids.Count -gt $MaxCandidates) {
        Write-Warning "Truncating to MaxCandidates=$MaxCandidates (was $($ids.Count))."
        $ids = $ids[0..($MaxCandidates - 1)]
    }

    $items = Get-WorkItemBatch -Config $Config -Ids $ids -Headers $headers
    $candidates = New-Object System.Collections.Generic.List[object]
    foreach ($wi in $items) {
        $comments = Get-WorkItemComments -Config $Config -Id $wi.id -Headers $headers
        $candidate = Convert-WorkItemToCandidate -Config $Config -WorkItem $wi -Comments $comments
        $candidates.Add($candidate) | Out-Null
    }

    $json = $candidates.ToArray() | ConvertTo-Json -Depth 20
    Set-Content -LiteralPath $OutFile -Value $json -Encoding UTF8
    Write-Host "Wrote $($candidates.Count) candidate(s) to $OutFile."
}

# ──────────────────────────────────────────────────────────────────────
# Self-tests (no network).
# ──────────────────────────────────────────────────────────────────────
function Invoke-SelfTest {
    $errors = 0
    $config = Get-TriageConfig -Path $ConfigPath

    # 1. WIQL is well-formed.
    $wiql = Build-WiqlQuery -Config $config -LookbackDays 7
    if ($wiql -notmatch [regex]::Escape("UNDER 'DevDiv\Python and AI Tools'")) {
        Write-Error "WIQL missing area path clause."; $errors++
    }
    if ($wiql -notmatch '@Today - 7') {
        Write-Error "WIQL missing date predicate."; $errors++
    }
    if ($wiql -notmatch "NOT \[System.Tags\] CONTAINS 'triaged-by-ai'") {
        Write-Error "WIQL missing triaged-by-ai exclusion."; $errors++
    }
    if ($wiql -notmatch "NOT \[System.Tags\] CONTAINS 'do-not-triage'") {
        Write-Error "WIQL missing do-not-triage exclusion."; $errors++
    }

    # 2. Convert-WorkItemToCandidate handles a minimal fixture.
    $fixtureFile = Join-Path $PSScriptRoot 'tests\fixtures\workitem-sample.json'
    if (Test-Path -LiteralPath $fixtureFile) {
        $wi = Get-Content -LiteralPath $fixtureFile -Raw | ConvertFrom-Json
        $cand = Convert-WorkItemToCandidate -Config $config -WorkItem $wi -Comments @()
        if (-not $cand.title) { Write-Error "Candidate title is empty."; $errors++ }
        if ($cand.id -ne $wi.id) { Write-Error "Candidate id mismatch."; $errors++ }
        if (-not $cand.url.EndsWith("/_workitems/edit/$($wi.id)")) { Write-Error "Candidate url malformed."; $errors++ }
    } else {
        Write-Warning "Fixture not present at $fixtureFile (skipping conversion test)."
    }

    # 3. Empty ID set returns empty (offline path).
    $headers = @{ Authorization = 'Bearer dummy' }
    $emptyResult = Get-WorkItemBatch -Config $config -Ids @() -Headers $headers
    # PowerShell collapses @() returned from a function to $null in many call
    # sites; the contract we care about is "doesn't throw and has 0 items".
    $countOk = ($null -eq $emptyResult) -or (@($emptyResult).Count -eq 0)
    if (-not $countOk) {
        Write-Error "Get-WorkItemBatch returned non-empty for empty input."; $errors++
    }

    if ($errors -gt 0) {
        throw "query-azdo.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'query-azdo.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }

if (-not $OutFile) { throw "-OutFile is required (omit only with -SelfTest)." }

$config = Get-TriageConfig -Path $ConfigPath
Invoke-Query -OutFile $OutFile -LookbackDays $LookbackDays -MaxCandidates $MaxCandidates -WorkItemId $WorkItemId -Config $config
