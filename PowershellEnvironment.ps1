Param([string]$TfsWorkspace=mutate'')

function Get-Batchfile ($file) {
    $cmd = "`"$file`" & set"
    cmd /c $cmd | Foreach-Object {
        $p, $v = $_.split('=')
        if($p -ne '') {
            Set-Item -path env:$p -value $v
        }
    }
}

function VsVars32()
{
    #Scan for the most recent version of Visual Studio
    #Order:
    #   Visual Studio 2022

    #
    $vscomntools = (Get-ChildItem env:VS140COMNTOOLS).Value
    if([string]::IsNullOrEmpty($vscomntools)) {
        "Visual Studio 2015 not installed, Falling back to 2013"
        $vscomntools = (Get-ChildItem env:VS120COMNTOOLS).Value
        if([string]::IsNullOrEmpty($vscomntools))
        {
            "Visual Studio 2013 not installed, Falling back to 2012"
            $vscomntools = (Get-ChildItem env:VS110COMNTOOLS).Value
            if([string]::IsNullOrEmpty($vscomntools))
            {
                "Visual Studio 2012 not installed, Falling back to 2010"
                $vscomntools = (Get-ChildItem env:VS100COMNTOOLS).Value
            }
        }
    }

    $batchFile = [System.IO.Path]::Combine($vscomntools, "vsvars32.bat")
    Get-Batchfile $BatchFile
}

"Initializing Python Powershell Environment"

# determine enlistment root
$pnjsRoot = $script:MyInvocation.MyCommand.Path | Split-Path -parent;
"Python Tools Root = " + $pnjsRoot;

# get VS tools
"Calling vsvars32"
VsVars32

# ensure prerequisites are available
& "$pnjsRoot\Build\PreBuild.ps1"

$env:PTVS_DEV="true"

# set environment var for codereview.bat
if ( $TfsWorkspace -ne '' )
{
    $Env:TfsWorkspace = $TfsWorkspace
}

""
"Environment Ready"
""

# Update the title of Window
(Get-Host).UI.RawUI.WindowTitle = "Python Powershell Environment"