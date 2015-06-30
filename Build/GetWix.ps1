<#
.Synopsis
    Downloads and extracts WiX if it is not currently available.

#>
$buildroot = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition))
$target = "$buildroot\BuildOutput\Wix\3.9"
if (Test-Path "$target\wix.targets") {
    Write-Output "Wix Location: $target"
    return
}

Write-Output "Downloading Wix to $target"

$file = [IO.Path]::GetTempFileName()
Write-Output "  - temporary storage: $file"

Invoke-WebRequest "https://wix.codeplex.com/downloads/get/1421697" -UseBasicParsing -OutFile $file

[Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem') | Out-Null
[System.IO.Compression.ZipFile]::ExtractToDirectory($file, $target)

del $file
