<# 
.SYNOPSIS
Downloads drop.exe from the given URI to the given directory if not already present.
  
.EXAMPLE
./GetDropExe.ps1  "https://devdiv.artifacts.visualstudio.com" "C:\school"
#>

[CmdletBinding()]
Param(	
	[Parameter(Position=1)]
	[PSDefaultValue(Help = "Drop URI, defaults to https://devdiv.artifacts.visualstudio.com")]
	[string]$Uri = "https://devdiv.artifacts.visualstudio.com",
	
	[Parameter(Position=2)]
	[PSDefaultValue(Help = "Location to put Drop.exe. Defaults to current directory")]
	[string]$Path = $PSScriptRoot + "/../DropTools"
)

# Force this script to use TLS 1.2
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$dropExePath = "$Path\lib\net45\drop.exe"
if (!(test-path $dropExePath))
{
	"'$dropExePath' not found, downloading from '$Uri'."
	
	$zip = [System.IO.Path]::GetTempFileName()
	$zip = [System.IO.Path]::ChangeExtension($zip, "zip")

	$old = $ProgressPreference
    $ProgressPreference = "SilentlyContinue"
	Invoke-WebRequest -Uri "$Uri/_apis/drop/client/exe" -OutFile $zip
    $ProgressPreference = $old

    Add-Type -Assembly System.IO.Compression.FileSystem

    mkdir $Path | out-null
    del -r -force $Path

	"Extracting $zip to '$Path'..."
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $Path)

	del -force $zip
}

if (!(test-path $dropExePath))
{
	throw "Failed to download drop.exe."
}

"Drop.exe downloaded at '$dropExePath'."