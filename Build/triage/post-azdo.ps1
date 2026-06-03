<#
    .SYNOPSIS
        Helpers for writing back to Azure DevOps: post a comment, close a work
        item using the Developer Community duplicate flow (or the
        answered/fixed flow).

    .DESCRIPTION
        Used by apply-outcomes.ps1. Each public function returns the
        deserialized response, or throws on non-retryable error.

        Concurrency-safe: callers pass the rev they observed when they fetched
        the work item; the PATCH includes `{op: test, path: /rev, value: $rev}`
        so AzDO rejects with HTTP 409 if the WI was modified by a human in the
        meantime.

        Auth: $env:AZDO_ACCESS_TOKEN.

    .NOTES
        This file is dot-sourced by apply-outcomes.ps1 — it exposes functions
        rather than running on its own. When run directly with -SelfTest, it
        executes inline smoke tests.
#>
[CmdletBinding()]
param(
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
    throw 'No AzDO credentials present.'
}

function Get-AzdoWorkItem {
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [int]    $Id
    )
    $uri = "$($Config.azdo.baseUrl)/$($Config.azdo.project)/_apis/wit/workitems/$Id?api-version=7.1&`$expand=all"
    return Invoke-RestMethod -Method GET -Uri $uri -Headers (Get-AzdoAuthHeader)
}

function Add-AzdoComment {
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [int]    $Id,
        [Parameter(Mandatory)] [string] $Text
    )
    $uri = "$($Config.azdo.baseUrl)/$($Config.azdo.project)/_apis/wit/workItems/$Id/comments?api-version=7.0-preview.3"
    $body = @{ text = $Text } | ConvertTo-Json -Depth 3
    return Invoke-RestMethod -Method POST -Uri $uri -Headers (Get-AzdoAuthHeader) -Body $body -ContentType 'application/json'
}

function Get-MergedTagsValue {
    param(
        [Parameter()] [string] $Existing,
        [Parameter(Mandatory)] [string[]] $Add
    )
    $cur = @()
    if ($Existing) { $cur = @($Existing -split '[;,]' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
    foreach ($t in $Add) { if ($t -and ($cur -notcontains $t)) { $cur += $t } }
    return ($cur -join '; ')
}

function Invoke-AzdoPatch {
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [int]    $Id,
        [Parameter(Mandatory)] [object[]] $PatchOps
    )
    $uri = "$($Config.azdo.baseUrl)/$($Config.azdo.project)/_apis/wit/workitems/$Id?api-version=7.1"
    $body = ConvertTo-Json -InputObject $PatchOps -Depth 10
    return Invoke-RestMethod `
        -Method PATCH `
        -Uri $uri `
        -Headers (Get-AzdoAuthHeader) `
        -Body $body `
        -ContentType 'application/json-patch+json'
}

function Close-AzdoAsDuplicate {
    <#
        DC duplicate close flow. Sets state to DC - Closed - Duplicated,
        populates the Duplicate Feedback Ticket ID field with $GithubIssueUrl,
        and tags the work item triaged-by-ai + moved-to-github.
    #>
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [int]    $Id,
        [Parameter(Mandatory)] [int]    $Rev,
        [Parameter(Mandatory)] [string] $GithubIssueUrl,
        [Parameter()]          [string] $ExistingTags = ''
    )
    $field = $Config.azdo.duplicateFieldName
    if (-not $field -or $field -eq '<DuplicateFeedbackTicketIdField>') {
        throw 'Refusing to write DC duplicate close: config.azdo.duplicateFieldName is still the placeholder. Confirm the exact REST field name in phase 0 and set it in Build/triage/config.json before enabling live mode.'
    }

    $newTags = Get-MergedTagsValue -Existing $ExistingTags -Add @($Config.azdo.ai_tag, $Config.azdo.movedToGithubTag)
    $patch = @(
        @{ op = 'test'; path = '/rev'; value = $Rev }
        @{ op = 'add';  path = '/fields/System.State';        value = $Config.azdo.states.duplicate }
        @{ op = 'add';  path = "/fields/$field";              value = $GithubIssueUrl }
        @{ op = 'add';  path = '/fields/System.Tags';         value = $newTags }
    )
    return Invoke-AzdoPatch -Config $Config -Id $Id -PatchOps $patch
}

function Close-AzdoAsAnswered {
    <#
        For the `answered` verdict path. Sets state to the DC `answered` close
        state and tags triaged-by-ai. Caller is responsible for posting the
        customer-facing $response_md as a comment BEFORE calling this (so the
        close timestamp is after the explanation).
    #>
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [int]    $Id,
        [Parameter(Mandatory)] [int]    $Rev,
        [Parameter()]          [string] $ExistingTags = '',
        [Parameter()]          [string] $StateOverride
    )
    $state = if ($StateOverride) { $StateOverride } else { $Config.azdo.states.answered }
    $newTags = Get-MergedTagsValue -Existing $ExistingTags -Add @($Config.azdo.ai_tag)
    $patch = @(
        @{ op = 'test'; path = '/rev';                value = $Rev }
        @{ op = 'add';  path = '/fields/System.State'; value = $state }
        @{ op = 'add';  path = '/fields/System.Tags';  value = $newTags }
    )
    return Invoke-AzdoPatch -Config $Config -Id $Id -PatchOps $patch
}

function Mark-AzdoTriagedManually {
    <#
        For verdicts where we don't auto-close — apply the `triaged-by-ai`
        tag so the next weekly WIQL skips the item until a human re-tags it.
    #>
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [int]    $Id,
        [Parameter(Mandatory)] [int]    $Rev,
        [Parameter()]          [string] $ExistingTags = ''
    )
    $newTags = Get-MergedTagsValue -Existing $ExistingTags -Add @($Config.azdo.ai_tag)
    $patch = @(
        @{ op = 'test'; path = '/rev';               value = $Rev }
        @{ op = 'add';  path = '/fields/System.Tags'; value = $newTags }
    )
    return Invoke-AzdoPatch -Config $Config -Id $Id -PatchOps $patch
}

function Format-DcDuplicateComment {
    param(
        [Parameter(Mandatory)] [object] $Config,
        [Parameter(Mandatory)] [string] $GithubIssueUrl
    )
    $base = $Config.dc_duplicate_template -replace '\{GH_URL\}', $GithubIssueUrl
    $optOut = if ($Config.PSObject.Properties['opt_out_clause']) { $Config.opt_out_clause } else { '' }
    return ($base + $optOut)
}

function Test-AlreadyTriagedByAi {
    <#
        Returns $true when the AI-triage tag (config.azdo.ai_tag) is already
        present on the given work-item tags string. Used by apply-outcomes.ps1
        as the per-write idempotence guard so that re-running the apply step
        after a mid-batch failure (or an ad-hoc -WorkItemId retry) does not
        post a duplicate comment or close the work item twice.

        The WIQL `excludedTags` filter in the prepare step handles the common
        case of fresh weekly runs, but ad-hoc retries / matrix re-runs
        bypass that path entirely.
    #>
    param(
        [Parameter(Mandatory)] [object] $Config,
        [string] $Tags
    )
    if (-not $Tags) { return $false }
    if (-not $Config.azdo.PSObject.Properties['ai_tag']) { return $false }
    $aiTag = [string] $Config.azdo.ai_tag
    if (-not $aiTag) { return $false }
    $parts = $Tags -split '\s*;\s*' | Where-Object { $_ }
    foreach ($p in $parts) {
        if ($p -ieq $aiTag) { return $true }
    }
    return $false
}

function Invoke-SelfTest {
    $errors = 0

    # Tag merging.
    $merged = Get-MergedTagsValue -Existing 'foo; bar' -Add @('baz','foo')
    if ($merged -notmatch 'foo' -or $merged -notmatch 'bar' -or $merged -notmatch 'baz') { Write-Error "Tag merge result: $merged"; $errors++ }
    $parts = $merged -split '\s*;\s*'
    if (@($parts | Where-Object { $_ -eq 'foo' }).Count -ne 1) { Write-Error 'Duplicate foo present.'; $errors++ }

    # Template substitution.
    $fakeCfg = [pscustomobject] @{
        dc_duplicate_template = 'See [link]({GH_URL}).'
        opt_out_clause = ' (opt-out: do not publish.)'
    }
    $t = Format-DcDuplicateComment -Config $fakeCfg -GithubIssueUrl 'https://github.com/x/y/issues/1'
    if ($t -ne 'See [link](https://github.com/x/y/issues/1). (opt-out: do not publish.)') {
        Write-Error "Template render mismatch: $t"; $errors++
    }

    # Refuse to PATCH duplicate close when field name is the placeholder.
    $cfg = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'config.json') -Raw | ConvertFrom-Json
    $threw = $false
    try {
        Close-AzdoAsDuplicate -Config $cfg -Id 1 -Rev 1 -GithubIssueUrl 'https://example/1' -ExistingTags ''
    } catch {
        $threw = ($_.Exception.Message -match 'placeholder')
    }
    if (-not $threw) { Write-Error 'Expected refusal when duplicateFieldName is the placeholder.'; $errors++ }

    # Test-AlreadyTriagedByAi: must spot the tag by exact (case-insensitive) match.
    $cfgTag = [pscustomobject] @{ azdo = [pscustomobject] @{ ai_tag = 'triaged-by-ai' } }
    if (-not (Test-AlreadyTriagedByAi -Config $cfgTag -Tags 'foo; Triaged-By-AI; bar')) {
        Write-Error 'Test-AlreadyTriagedByAi should be true (case-insensitive).'; $errors++
    }
    if (Test-AlreadyTriagedByAi -Config $cfgTag -Tags 'foo; bar') {
        Write-Error 'Test-AlreadyTriagedByAi should be false when ai_tag absent.'; $errors++
    }
    if (Test-AlreadyTriagedByAi -Config $cfgTag -Tags '') {
        Write-Error 'Test-AlreadyTriagedByAi should be false on empty tags.'; $errors++
    }
    # Substring should not falsely match.
    if (Test-AlreadyTriagedByAi -Config $cfgTag -Tags 'triaged-by-ai-bot') {
        Write-Error 'Test-AlreadyTriagedByAi must require exact tag, not substring.'; $errors++
    }

    if ($errors -gt 0) {
        throw "post-azdo.ps1 self-test failed with $errors error(s)."
    }
    Write-Host 'post-azdo.ps1 self-test: PASS'
}

if ($SelfTest) { Invoke-SelfTest; return }

# When dot-sourced, do nothing — caller invokes individual functions.
