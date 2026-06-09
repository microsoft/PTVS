<#
    .SYNOPSIS
        Queries Azure DevOps for open work items under DevDiv\Python and AI Tools\**
        that were created within the lookback window, and writes them to a JSON file.

    .DESCRIPTION
        Step 1 of the weekly triage workflow: AzDO WIQL query + workitemsbatch
        hydrate. Produces a normalized array of candidates.
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

    # Empty workItemTypes => emit no [System.WorkItemType] clause, matching
    # the manual top-level-items filter where Work Item Type = [Any].
    $typeClause = ''
    if ($Config.azdo.workItemTypes -and @($Config.azdo.workItemTypes).Count -gt 0) {
        $types = ($Config.azdo.workItemTypes | ForEach-Object { "'$_'" }) -join ', '
        $typeClause = "  AND  [System.WorkItemType] IN ($types)`n"
    }

    # Use substring NOT CONTAINS so 'Closed' also excludes DC states like
    # 'DC - Closed - Duplicated' (set by this pipeline when closing).
    $excludedStates = ($Config.azdo.excludedStates | ForEach-Object { "NOT [System.State] CONTAINS '$_'" }) -join ' AND '
    $excludedTagsClause = ($Config.azdo.excludedTags | ForEach-Object { "NOT [System.Tags] CONTAINS '$_'" }) -join ' AND '

    # Date field is configurable; falls back to CreatedDate for back-compat.
    $dateField = 'System.ChangedDate'
    if ($Config.azdo.PSObject.Properties['dateField'] -and $Config.azdo.dateField) {
        $df = [string] $Config.azdo.dateField
        $dateField = if ($df.StartsWith('System.')) { $df } else { "System.$df" }
    }

    $wiql = @"
SELECT [System.Id]
FROM   workitems
WHERE  [System.TeamProject] = '$($Config.azdo.project)'
  AND  [System.AreaPath] UNDER '$area'
$typeClause  AND  [$dateField] > @Today - $LookbackDays
  AND  $excludedStates
  AND  $excludedTagsClause
ORDER BY [$dateField] DESC
"@
    return $wiql
}

function Get-AzdoErrorDetails {
    param([Parameter(Mandatory)] [System.Management.Automation.ErrorRecord] $ErrRec)
    # Surface the bits AzDO actually puts useful diagnostics in: the response
    # body (TF400813/VS401034/etc message codes) and the WWW-Authenticate
    # header (which on AzDO indicates whether PAT is disabled by org policy,
    # whether Entra auth is required, etc.). Lost otherwise because
    # Invoke-RestMethod throws on non-2xx without surfacing the body.
    $status = $null
    $reason = $null
    $body   = $null
    $wwwAuth = $null
    $allHeaders = @()
    if ($ErrRec.Exception.Response) {
        $resp = $ErrRec.Exception.Response
        $status = [int] $resp.StatusCode
        $reason = $resp.ReasonPhrase
        if ($resp.Headers) {
            # WWW-Authenticate on its own.
            try {
                if ($resp.Headers.WwwAuthenticate) {
                    $wwwAuth = ($resp.Headers.WwwAuthenticate -join '; ')
                }
            } catch { }
            # Dump *all* response headers as a fallback — even when AzDO
            # omits WWW-Authenticate, X-TFS-* / X-VSS-* headers often carry
            # the actual reason.
            try {
                foreach ($h in $resp.Headers) {
                    $allHeaders += ("{0}: {1}" -f $h.Key, ($h.Value -join ', '))
                }
            } catch { }
        }
        # Try Content.ReadAsStringAsync — sometimes the body is here when
        # ErrorDetails.Message is empty (stream already consumed elsewhere).
        try {
            if ($resp.Content) {
                $task = $resp.Content.ReadAsStringAsync()
                $task.Wait(2000) | Out-Null
                if ($task.IsCompletedSuccessfully) {
                    $body = $task.Result
                }
            }
        } catch { }
    }
    # In PowerShell 7+ the response body is in ErrorDetails.Message for
    # HttpResponseException; in 5.1 it's on the Response stream.
    if (-not $body -and $ErrRec.ErrorDetails -and $ErrRec.ErrorDetails.Message) {
        $body = $ErrRec.ErrorDetails.Message
    }
    [pscustomobject] @{
        Status     = $status
        Reason     = $reason
        WwwAuth    = $wwwAuth
        Body       = $body
        AllHeaders = $allHeaders
    }
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
            # Non-retryable: dump AzDO's diagnostic payload before rethrowing
            # so the workflow log shows the actual cause (TF/VS code, body)
            # instead of a bare "401 (Unauthorized)".
            $det = Get-AzdoErrorDetails -ErrRec $_
            Write-Host "AzDO REST $Method $Uri failed."
            Write-Host "  HTTP status     : $($det.Status) $($det.Reason)"
            if ($det.WwwAuth) { Write-Host "  WWW-Authenticate: $($det.WwwAuth)" }
            if ($det.Body)    { Write-Host "  Response body   : $($det.Body)" }
            if ($det.AllHeaders -and $det.AllHeaders.Count -gt 0) {
                Write-Host "  Response headers:"
                foreach ($h in $det.AllHeaders) { Write-Host "    $h" }
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
                $sizeBytes = $null
                if ($rel.PSObject.Properties['attributes'] -and $rel.attributes) {
                    if ($rel.attributes.PSObject.Properties['name'] -and $rel.attributes.name) {
                        $name = $rel.attributes.name
                    }
                    # AzDO reports resourceSize when work-item relations are
                    # expanded ($expand=All / Relations). Use it as a pre-download
                    # gate in parse-diagnostics.ps1 so we don't pull a 50MB log
                    # just to discover it's too big.
                    if ($rel.attributes.PSObject.Properties['resourceSize'] -and `
                        $null -ne $rel.attributes.resourceSize -and `
                        '' -ne ([string] $rel.attributes.resourceSize)) {
                        try { $sizeBytes = [int64] $rel.attributes.resourceSize } catch { $sizeBytes = $null }
                    }
                }
                $attachments += [pscustomobject] @{
                    name       = $name
                    url        = $url
                    size_bytes = $sizeBytes
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
        changed_date      = if ($f.ContainsKey('System.ChangedDate')) { $f['System.ChangedDate'] } else { $null }
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
    # @(...) keeps single-element returns from collapsing — without it the
    # ad-hoc replay path (WorkItemId set) hits a StrictMode .Count failure.
    $ids = @(Get-CandidateIds -Config $Config -LookbackDays $LookbackDays -Headers $headers -ExplicitWorkItemId $WorkItemId)
    Write-Host "WIQL returned $($ids.Count) candidate ID(s)."

    if ($ids.Count -gt $MaxCandidates) {
        Write-Warning "Truncating to MaxCandidates=$MaxCandidates (was $($ids.Count))."
        $ids = $ids[0..($MaxCandidates - 1)]
    }

    $items = Get-WorkItemBatch -Config $Config -Ids $ids -Headers $headers
    $candidates = New-Object System.Collections.Generic.List[object]
    foreach ($wi in $items) {
        # @() prevents PS from collapsing an empty-comments return to $null,
        # which would trip StrictMode on $Comments.Count inside the converter.
        $comments = @(Get-WorkItemComments -Config $Config -Id $wi.id -Headers $headers)
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
    if ($wiql -notmatch "NOT \[System.State\] CONTAINS 'Closed'") {
        Write-Error "WIQL missing 'Closed' substring exclusion."; $errors++
    }
    # Default config now scopes to FeedbackTicket only — the type the VS
    # Developer Community sync creates. Verified empirically (2026-06-09):
    # 81/81 FeedbackTickets in DevDiv\Python and AI Tools over the last
    # year carried Microsoft.DevDiv.DeveloperCommunityId; 0 non-Feedback
    # items in that area were directly DC-originated. Type filter is
    # therefore both necessary and sufficient.
    if ($wiql -notmatch "\[System\.WorkItemType\] IN \('FeedbackTicket'\)") {
        Write-Error "WIQL missing FeedbackTicket type filter."; $errors++
    }
    # Date field should be ChangedDate per config.
    if ($wiql -notmatch '\[System\.ChangedDate\] > @Today') {
        Write-Error "WIQL should use ChangedDate when configured."; $errors++
    }

    # 1b. Non-empty workItemTypes path with multiple entries renders correctly.
    $cfg2 = ($config | ConvertTo-Json -Depth 20 | ConvertFrom-Json)
    $cfg2.azdo.workItemTypes = @('Bug', 'Task')
    $wiql2 = Build-WiqlQuery -Config $cfg2 -LookbackDays 7
    if ($wiql2 -notmatch "\[System\.WorkItemType\] IN \('Bug', 'Task'\)") {
        Write-Error "WIQL with non-empty types should emit IN clause."; $errors++
    }

    # 1c. Empty workItemTypes array => no IN (...) clause emitted (this is
    # the back-compat "any work item type" path that an operator could opt
    # into if they wanted to broaden the scope beyond DC items).
    $cfg3 = ($config | ConvertTo-Json -Depth 20 | ConvertFrom-Json)
    $cfg3.azdo.workItemTypes = @()
    $wiql3 = Build-WiqlQuery -Config $cfg3 -LookbackDays 7
    if ($wiql3 -match '\[System\.WorkItemType\] IN') {
        Write-Error "Empty workItemTypes should suppress the IN clause entirely."; $errors++
    }

    # 2. Convert-WorkItemToCandidate handles a minimal fixture.
    $fixtureFile = Join-Path $PSScriptRoot 'tests\fixtures\workitem-sample.json'
    if (Test-Path -LiteralPath $fixtureFile) {
        $wi = Get-Content -LiteralPath $fixtureFile -Raw | ConvertFrom-Json
        $cand = Convert-WorkItemToCandidate -Config $config -WorkItem $wi -Comments @()
        if (-not $cand.title) { Write-Error "Candidate title is empty."; $errors++ }
        if ($cand.id -ne $wi.id) { Write-Error "Candidate id mismatch."; $errors++ }
        if (-not $cand.url.EndsWith("/_workitems/edit/$($wi.id)")) { Write-Error "Candidate url malformed."; $errors++ }

        # Attachment metadata: pass-through of resourceSize when AzDO gives
        # us one, $null when the relation omits it. parse-diagnostics.ps1
        # uses this to skip large attachments without downloading them.
        $atts = @($cand.attachments)
        if ($atts.Count -lt 3) { Write-Error "Expected ≥3 attachments from fixture, got $($atts.Count)."; $errors++ }
        $logAtt    = $atts | Where-Object { $_.name -eq 'PythonToolsDiagnostics_20260508.log' } | Select-Object -First 1
        $shotAtt   = $atts | Where-Object { $_.name -eq 'screenshot.png' }                     | Select-Object -First 1
        $notesAtt  = $atts | Where-Object { $_.name -eq 'vsfeedback-notes.txt' }               | Select-Object -First 1
        if (-not $logAtt   -or $logAtt.size_bytes  -ne 12345) { Write-Error "Attachment size pass-through broken for the diagnostics log."; $errors++ }
        if (-not $shotAtt  -or $shotAtt.size_bytes -ne 67890) { Write-Error "Attachment size pass-through broken for the screenshot."; $errors++ }
        if (-not $notesAtt -or $null -ne $notesAtt.size_bytes) { Write-Error "Attachment with no resourceSize should expose size_bytes=`$null."; $errors++ }
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
