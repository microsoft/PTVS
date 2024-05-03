# This script is used to run a pipeline in azure devops.
# It uses the run-pipeline rest api. Docs are at https://learn.microsoft.com/en-us/rest/api/azure/devops/pipelines/runs/run-pipeline?view=azure-devops-rest-7.1

# Required Inputs
# token: The azdo personal access token (PAT) with "Build (Read & execute)" permissions
    # You can either pass the token in, or store it in the "AZDO_PAT" environment variable
# organization: The azdo organization where the pipeline lives
# project: The azdo project where the pipeline lives
# pipelineId: The id of the pipeline you want to run.
    # This is called "definitionId" in the url. For example, https://devdiv.visualstudio.com/DevDiv/_build?definitionId=14121&_a=summary

# Optional Inputs
# branch: the branch that the pipeline should run against. Defaults to main.
# runtimeParams: A comma separated list of runtime parameters
    # Each param should be a key:value pair. For example, "p1Name:p1Value,p2Name:p2Value"

Param(
    [string] $token = $env:AZDO_PAT,
    [string] $organization = $(throw "organization is required"),
    [string] $project = $(throw "project is required"),
    [int] $pipelineId = $(throw "pipelineId is required"),
    [string] $branch = "main",
    [string[]] $runtimeParams
)

# stop on all errors
$ErrorActionPreference = "Stop"

# use strict syntax
Set-StrictMode -Version Latest

if (!$token) {
    throw "You must specify a PAT or set the AZDO_PAT environment variable"
}

$user = ""
$restApiUrl = "https://dev.azure.com/$organization/$project/_apis/pipelines/$pipelineId/runs?api-version=7.1-preview.1"

# build the request headers
$base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("{0}:{1}" -f $user, $token)))
$headers = @{Authorization = ("Basic {0}" -f $base64AuthInfo) }

# build the resources section of the body, since the branch is required for launching a pipeline
$branchStr = "refs/heads/$branch"
$resources = @{
    repositories = @{
        self = @{
            refName = $branchStr
        }
    }
}

# if there are runtime params, build the template parameters section
$templateParameters = @{}
if ($runtimeParams) {
    foreach ($runtimeParam in $runtimeParams) {

        # split on colon to get key value pair
        $keyValuePair = $runtimeParam -split ":"

        # ignore invalid params
        if ($keyValuePair.Length -le 1) {
            continue
        }

        # store the pair in the template parameters dict
        $key = $keyValuePair[0]
        $value = $keyValuePair[1]
        $templateParameters[$key] = $value
    }
}

# build the request body
$body = @{}
$body["resources"] = $resources
if ($templateParameters.Count -gt 0) {
    $body["templateParameters"] = $templateParameters
}
$bodyAsJson = $body | ConvertTo-Json -Depth 10

# run the request
$result = Invoke-RestMethod -Uri $restApiUrl -Method Post -ContentType "application/json" -Headers $headers -Body $bodyAsJson
$result

if (!$result) {
    throw "Invoke-RestMethod failed, check output for details"
}

# the state should always be "inProgress" if the request was successful
if ($result.state -ne "inProgress") {
    throw "Pipeline failed to start, check output for details"
}
