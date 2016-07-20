<#
.Synopsis
    Creates a ZIP file containing the entire directory structure.

#>

[CmdletBinding()]
param([string] $root, [string] $target)

[Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem') | Out-Null
[System.IO.Compression.ZipFile]::CreateFromDirectory($root, $target, [System.IO.Compression.CompressionLevel]::Optimal, $false)
