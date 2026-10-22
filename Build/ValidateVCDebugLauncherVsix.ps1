param(
    [Parameter(Mandatory = $true)]
    [string]$VsixPath,
    [Parameter(Mandatory = $true)]
    [string]$VSTarget,
    [switch]$RequireLocalizedContent
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (!(Test-Path -LiteralPath $VsixPath -PathType Leaf)) {
    throw "VC debugger launcher VSIX '$VsixPath' was not found."
}
if ($VSTarget -notmatch '^\d+\.0$') {
    throw "VSTarget '$VSTarget' is not in the expected '<major>.0' format."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$targetVersion = "v$($VSTarget.Replace('.', ''))"
$targetPrefix = '$MSBuild/Microsoft/VC/{0}/' -f $targetVersion
$launcherFilePattern = 'Microsoft\.PythonTools\.Debugger\.VCDebugLauncher\.(props|targets|xaml)$'
$expectedBaseEntries = @(
    '$MSBuild/Microsoft/VC/{0}/Platforms/Win32/ImportAfter/Microsoft.PythonTools.Debugger.VCDebugLauncher.targets' -f $targetVersion
    '$MSBuild/Microsoft/VC/{0}/Platforms/Win32/ImportAfter/Microsoft.PythonTools.Debugger.VCDebugLauncher.xaml' -f $targetVersion
    '$MSBuild/Microsoft/VC/{0}/Platforms/Win32/ImportBefore/Microsoft.PythonTools.Debugger.VCDebugLauncher.props' -f $targetVersion
    '$MSBuild/Microsoft/VC/{0}/Platforms/x64/ImportAfter/Microsoft.PythonTools.Debugger.VCDebugLauncher.targets' -f $targetVersion
    '$MSBuild/Microsoft/VC/{0}/Platforms/x64/ImportAfter/Microsoft.PythonTools.Debugger.VCDebugLauncher.xaml' -f $targetVersion
    '$MSBuild/Microsoft/VC/{0}/Platforms/x64/ImportBefore/Microsoft.PythonTools.Debugger.VCDebugLauncher.props' -f $targetVersion
)

$manifestContent = $null
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $VsixPath).Path)
try {
    $allEntries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/').TrimStart('/') })
    $manifestEntry = $archive.Entries | Where-Object FullName -eq 'extension.vsixmanifest' | Select-Object -First 1
    if ($manifestEntry) {
        $manifestReader = [IO.StreamReader]::new($manifestEntry.Open())
        try {
            $manifestContent = $manifestReader.ReadToEnd()
        } finally {
            $manifestReader.Dispose()
        }
    }
} finally {
    $archive.Dispose()
}
if (!$manifestContent) {
    throw "The VSIX does not contain extension.vsixmanifest."
}

$launcherEntries = @(
    $allEntries | Where-Object {
        $_ -match '^\$MSBuild/Microsoft/VC/v[^/]+/Platforms/(Win32|x64)/Import(After|Before)(/[^/]+)?/' + $launcherFilePattern
    }
)
$baseEntries = @(
    $launcherEntries | Where-Object {
        $_ -match '^\$MSBuild/Microsoft/VC/v[^/]+/Platforms/(Win32|x64)/Import(After|Before)/' + $launcherFilePattern
    }
)
$localizedEntries = @(
    $launcherEntries | Where-Object {
        $_ -match '^\$MSBuild/Microsoft/VC/v[^/]+/Platforms/(Win32|x64)/ImportAfter/([^/]+)/Microsoft\.PythonTools\.Debugger\.VCDebugLauncher\.xaml$'
    } | ForEach-Object {
        [pscustomobject]@{
            Path = $_
            Platform = $Matches[1]
            Locale = $Matches[2]
        }
    }
)
$expectedLocalizedLocales = @()
if ($RequireLocalizedContent) {
    $languagesPath = Join-Path $PSScriptRoot 'VSLanguages.props'
    if (!(Test-Path -LiteralPath $languagesPath -PathType Leaf)) {
        throw "Visual Studio language definitions '$languagesPath' were not found."
    }

    [xml]$languagesXml = Get-Content -LiteralPath $languagesPath
    $namespaceManager = [Xml.XmlNamespaceManager]::new($languagesXml.NameTable)
    $namespaceManager.AddNamespace('msb', 'http://schemas.microsoft.com/developer/msbuild/2003')
    $expectedLocalizedLocales = @(
        $languagesXml.SelectNodes('/msb:Project/msb:ItemGroup/msb:VSLanguages/msb:LCID', $namespaceManager) |
            ForEach-Object { $_.InnerText } |
            Sort-Object -Unique
    )
    if ($expectedLocalizedLocales.Count -eq 0) {
        throw "No localized LCIDs were defined in '$languagesPath'."
    }
}

$violations = [Collections.Generic.List[string]]::new()
[xml]$manifestXml = $manifestContent
$manifestNamespaceManager = [Xml.XmlNamespaceManager]::new($manifestXml.NameTable)
$manifestNamespaceManager.AddNamespace('vsix', 'http://schemas.microsoft.com/developer/vsx-schema/2011')
$nextVSTarget = "$([int]$VSTarget.Split('.')[0] + 1).0"
$expectedVersionRange = "[$VSTarget,$nextVSTarget)"
$installationTargets = @($manifestXml.SelectNodes('/vsix:PackageManifest/vsix:Installation/vsix:InstallationTarget', $manifestNamespaceManager))
if ($installationTargets.Count -eq 0) {
    $violations.Add('The VSIX manifest does not contain an installation target.')
}
foreach ($installationTarget in $installationTargets) {
    if ($installationTarget.Version -ne $expectedVersionRange) {
        $violations.Add("Installation target '$($installationTarget.Id)' must use version range $expectedVersionRange, found $($installationTarget.Version).")
    }
}
$prerequisites = @($manifestXml.SelectNodes('/vsix:PackageManifest/vsix:Prerequisites/vsix:Prerequisite', $manifestNamespaceManager))
foreach ($prerequisite in $prerequisites) {
    if ($prerequisite.Version -ne $expectedVersionRange) {
        $violations.Add("Prerequisite '$($prerequisite.Id)' must use version range $expectedVersionRange, found $($prerequisite.Version).")
    }
}
$duplicateEntries = @($launcherEntries | Group-Object | Where-Object Count -gt 1)
foreach ($duplicateEntry in $duplicateEntries) {
    $violations.Add("Duplicate payload entry ($($duplicateEntry.Count)x): $($duplicateEntry.Name)")
}
if ($baseEntries.Count -ne $expectedBaseEntries.Count) {
    $violations.Add("Expected $($expectedBaseEntries.Count) base payload entries, found $($baseEntries.Count).")
}
$actualBaseEntries = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $baseEntries) {
    [void]$actualBaseEntries.Add($entry)
}
$expectedBaseEntrySet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $expectedBaseEntries) {
    [void]$expectedBaseEntrySet.Add($entry)
    if (!$actualBaseEntries.Contains($entry)) {
        $violations.Add("Missing base payload: $entry")
    }
}
foreach ($entry in $actualBaseEntries) {
    if ($entry.StartsWith($targetPrefix, [StringComparison]::OrdinalIgnoreCase) -and !$expectedBaseEntrySet.Contains($entry)) {
        $violations.Add("Unexpected base payload: $entry")
    }
}
foreach ($entry in $launcherEntries) {
    if (!$entry.StartsWith($targetPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        $violations.Add("Payload targets a different VC version: $entry")
    }
}

if ($RequireLocalizedContent) {
    $actualLocalizedLocales = @($localizedEntries | ForEach-Object { $_.Locale } | Sort-Object -Unique)
    $expectedLocalizedEntryCount = $expectedLocalizedLocales.Count * 2
    if ($localizedEntries.Count -ne $expectedLocalizedEntryCount) {
        $violations.Add("Expected $expectedLocalizedEntryCount localized XAML payloads, found $($localizedEntries.Count).")
    }
    foreach ($locale in $expectedLocalizedLocales) {
        if ($actualLocalizedLocales -notcontains $locale) {
            $violations.Add("Missing localized payload for LCID $locale.")
        }
    }
    foreach ($locale in $actualLocalizedLocales) {
        if ($expectedLocalizedLocales -notcontains $locale) {
            $violations.Add("Unexpected localized payload for LCID $locale.")
        }
    }
}
foreach ($localeGroup in $localizedEntries | Group-Object Locale) {
    $platforms = @($localeGroup.Group.Platform | Sort-Object -Unique)
    if ($localeGroup.Count -ne 2 -or $platforms.Count -ne 2 -or $platforms[0] -ne 'Win32' -or $platforms[1] -ne 'x64') {
        $paths = @($localeGroup.Group.Path | Sort-Object)
        $violations.Add("Localized payload '$($localeGroup.Name)' must contain one Win32 and one x64 XAML entry: $($paths -join ', ')")
    }
}

if ($violations.Count -gt 0) {
    $details = $violations | Sort-Object -Unique | ForEach-Object { " - $_" }
    throw "VC debugger launcher VSIX validation failed for VSTarget $VSTarget.`n$($details -join "`n")"
}

Write-Host "VC debugger launcher VSIX validation passed for $targetVersion with $($baseEntries.Count) base and $($localizedEntries.Count) localized payloads."
