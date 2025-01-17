# This script is used to get a list of azdo builds
# It uses the azdo rest api. Docs are at https://learn.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-7.1

# Required Inputs
# token: The azdo personal access token (PAT) with "Build (Read & Execute)" permissions
    # You can either pass the token in, or store it in the "AZDO_PAT" environment variable
    # If you don't have a token, go to https://devdiv.visualstudio.com/_usersSettings/tokens and create one. It must have "Build (Read & Execute)" permissions
# organization: The azdo organization where the pipeline lives
    # Defaults to DevDiv
# project: The azdo project where the pipeline lives
    # Defaults to DevDiv
# pipelineId: The id of the pipeline you are interested in.
    # This is called "definitionId" in the url. For example, https://devdiv.visualstudio.com/DevDiv/_build?definitionId=14121&_a=summary
    # Defaults to 14121, which is the PTVS build pipeline
# buildNumber: The build number to search for

Param(
    [string] $token = $env:AZDO_PAT,
    [string] $organization = "DevDiv",
    [string] $project = "DevDiv",
    [string] $pipelineId = "14121",
    [string] $buildNumber = $(throw "buildNumber is required")
)

# stop on all errors
$ErrorActionPreference = "Stop"

# use strict syntax
Set-StrictMode -Version Latest

if (!$token) {
    throw "You must specify a PAT or set the AZDO_PAT environment variable"
}

$user = ""
$restApiUrl = "https://dev.azure.com/$organization/$project/_apis/build/builds?api-version=7.1&definitions=$pipelineId&buildNumber=$buildNumber"

# build the request headers
$base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("{0}:{1}" -f $user, $token)))
$headers = @{Authorization = ("Basic {0}" -f $base64AuthInfo) }

# run the request
$result = Invoke-RestMethod -Uri $restApiUrl -Method Get -ContentType "application/json" -Headers $headers

if (!$result) {
    throw "Invoke-RestMethod failed, check output for details"
}

if ($result.count -eq 0) {
    throw "No builds found for pipeline $pipelineId with build number $buildNumber"
}

$result.value