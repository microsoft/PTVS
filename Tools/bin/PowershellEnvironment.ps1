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
    #Scan for the most recent version of Visual Studio
    #Order:
    #   Visual Studio 2012
    #   Visual Studio 2010
    #
    $vscomntools = (Get-ChildItem env:VS120COMNTOOLS).Value
    if($vscomntools -eq '') {
        "Visual Studio 2013 not installed, Falling back to 2012"
        $vscomntools = (Get-ChildItem env:VS110COMNTOOLS).Value
        if($vscomntools -eq '')
        {
            "Visual Studio 2012 not installed, Falling back to 2010"
            $vscomntools = (Get-ChildItem env:VS100COMNTOOLS).Value
        }
    }

    $batchFile = [System.IO.Path]::Combine($vscomntools, "vsvars32.bat")
    Get-Batchfile $BatchFile
}

"Initializing Python Powershell Environment"

# determine enlistment root
$pnjsToolsBin = $script:MyInvocation.MyCommand.Path | Split-Path -parent;
$pnjsRoot = $pnjsToolsBin | Split-Path -parent | Split-Path -parent;
"Python Tools Root = " + $pnjsRoot;

# get VS tools
"Calling vsvars32"
VsVars32

# add tools to path
$Env:Path = $pnjsToolsBin + ";" + $Env:Path;
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

# Update the title of Window
(Get-Host).UI.RawUI.WindowTitle = "Python Powershell Environment"