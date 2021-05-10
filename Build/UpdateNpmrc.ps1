# DEVOPS_TOKEN is not secret so that it works on other branches. Since Devops pipeline is only accessible from 
# Microsoft, doesn't need to be secret in pipeline
# See here on how to generate a new token:
# https://devdiv.visualstudio.com/DevDiv/_packaging?_a=connect&feed=Pylance%40Local
$str = "registry=https://devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/registry/ 
always-auth=true
; begin auth token
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/registry/:username=devdiv
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/registry/:_password=$Env:MAPPED_AZURE_DEVOPS_TOKEN
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/registry/:email=npm requires email to be set but doesn't use the value
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/:username=devdiv
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/:_password=$Env:MAPPED_AZURE_DEVOPS_TOKEN
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/:email=npm requires email to be set but doesn't use the value
; end auth token
"

Write-Host "Writing .npmrc with token $Env:MAPPED_AZURE_DEVOPS_TOKEN from environment"
Set-Content -Path ".npmrc" -Value $str
