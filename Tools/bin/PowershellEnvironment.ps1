Param([string]$TfsWorkspace='')

function Get-Batchfile ($file) {
    $cmd = "`"$file`" & set"
    cmd /c $cmd | Foreach-Object {
        $p, $v = $_.split('=')
        Set-Item -path env:$p -value $v
    }
}

function VsVars32()
{
    $vs100comntools = (Get-ChildItem env:VS100COMNTOOLS).Value
    $batchFile = [System.IO.Path]::Combine($vs100comntools, "vsvars32.bat")
    Get-Batchfile $BatchFile
}

"Initializing TC Workbench Powershell VS2010 Environment"

# determine enlistment root
$TCWBToolsBin = $script:MyInvocation.MyCommand.Path | Split-Path -parent;
$TCWBRoot = $TCWBToolsBin | Split-Path -parent | Split-Path -parent;
"TCWBRoot = " + $TCWBRoot;

# get VS tools
"Calling vsvars32"
VsVars32

# add tools to path
$Env:Path = $TCWBToolsBin + ";" + $Env:Path;
$env:PTVS_DEV="true"

# set environment var for codereview.bat
if ( $TfsWorkspace -ne '' )
{
    $Env:TfsWorkspace = $TfsWorkspace
}

""
prereq.exe

"Environment Ready"
""
