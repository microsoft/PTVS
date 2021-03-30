# DEVOPS_TOKEN is not secret so that it works on other branches. Since Devops pipeline is only accessible from 
# Microsoft, doesn't need to be secret in pipeline
# See here on how to generate a new token:
# https://devdiv.visualstudio.com/DevDiv/_packaging?_a=connect&feed=Pylance%40Local
$str = "registry=https://registry.npmjs.org/
@pylance:registry=https://msdata.pkgs.visualstudio.com/_packaging/DSVM_Image/npm/registry/
always-auth=true
; begin auth token
//msdata.pkgs.visualstudio.com/_packaging/DSVM_Image/npm/registry/:username=msdata
//msdata.pkgs.visualstudio.com/_packaging/DSVM_Image/npm/registry/:_password=$Env:MAPPED_AZURE_DEVOPS_TOKEN
//msdata.pkgs.visualstudio.com/_packaging/DSVM_Image/npm/registry/:email=email
//msdata.pkgs.visualstudio.com/_packaging/DSVM_Image/npm/:username=msdata
//msdata.pkgs.visualstudio.com/_packaging/DSVM_Image/npm/:_password=$Env:MAPPED_AZURE_DEVOPS_TOKEN
//msdata.pkgs.visualstudio.com/_packaging/DSVM_Image/npm/:email=email
; end auth token
"

Write-Host "Writing .npmrc with token $Env:MAPPED_AZURE_DEVOPS_TOKEN from environment"
Set-Content -Path ".npmrc" -Value $str
Get-Content -Path ".npmrc"
