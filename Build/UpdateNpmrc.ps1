# DEVOPS_TOKEN is not secret so that it works on other branches. Since Devops pipeline is only accessible from 
# Microsoft, doesn't need to be secret in pipeline
$str = "
registry=https://devdiv.pkgs.visualstudio.com/_packaging/Pylance/npm/registry/
always-auth=true
; begin auth token
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/registry/:username=devdiv
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/registry/:_password=[BASE64_ENCODED_PERSONAL_ACCESS_TOKEN]
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/registry/:email=npm requires email to be set but doesn't use the value
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/:username=devdiv
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/:_password=[BASE64_ENCODED_PERSONAL_ACCESS_TOKEN]
//devdiv.pkgs.visualstudio.com/_packaging/Pylance%40Local/npm/:email=npm requires email to be set but doesn't use the value
; end auth token
"

Write-Host "Writing .npmrc with token $Env:MAPPED_AZURE_DEVOPS_TOKEN from environment"
Set-Content -Path ".npmrc" -Value $str
