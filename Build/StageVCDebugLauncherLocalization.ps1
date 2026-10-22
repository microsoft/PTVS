param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$languagesPath = Join-Path $PSScriptRoot 'VSLanguages.props'
if (!(Test-Path -LiteralPath $languagesPath -PathType Leaf)) {
    throw "Visual Studio language definitions '$languagesPath' were not found."
}
if (!(Test-Path -LiteralPath $SourceRoot -PathType Container)) {
    throw "VC debugger launcher source root '$SourceRoot' was not found."
}

[xml]$languagesXml = Get-Content -LiteralPath $languagesPath
$namespaceManager = [Xml.XmlNamespaceManager]::new($languagesXml.NameTable)
$namespaceManager.AddNamespace('msb', 'http://schemas.microsoft.com/developer/msbuild/2003')
$languages = @(
    $languagesXml.SelectNodes('/msb:Project/msb:ItemGroup/msb:VSLanguages', $namespaceManager) |
        ForEach-Object { $_.GetAttribute('Include') }
)
if ($languages.Count -eq 0) {
    throw "No localized languages were defined in '$languagesPath'."
}

$fileName = 'Microsoft.PythonTools.Debugger.VCDebugLauncher.xaml'
if (Test-Path -LiteralPath $OutputRoot -PathType Container) {
    Get-ChildItem -LiteralPath $OutputRoot -Filter $fileName -File -Recurse |
        Remove-Item -Force
}

$stagedFiles = [Collections.Generic.List[string]]::new()
foreach ($platform in @('Win32', 'x64')) {
    $sourcePath = Join-Path $SourceRoot "$platform\ImportAfter\$fileName"
    if (!(Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "VC debugger launcher XAML '$sourcePath' was not found."
    }

    foreach ($language in $languages) {
        $destinationPath = Join-Path $OutputRoot "$language\VCTargets\$platform\ImportAfter\$fileName"
        New-Item -ItemType Directory -Path (Split-Path -Parent $destinationPath) -Force | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force
        $stagedFiles.Add($destinationPath)
    }
}

Write-Host "Staged $($stagedFiles.Count) VC debugger launcher localization test files for $($languages.Count) languages."
