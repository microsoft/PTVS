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

$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $VsixPath).Path)
try {
    $allEntries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/').TrimStart('/') })
} finally {
    $archive.Dispose()
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

$violations = [Collections.Generic.List[string]]::new()
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

if ($RequireLocalizedContent -and $localizedEntries.Count -eq 0) {
    $violations.Add("No localized VC debugger launcher XAML payloads were found.")
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
