param ($vstarget, $source, $outdir)

"Restoring Packages"

# These packages require a versionless symlink pointing to the versioned install.
$need_symlink = @(
    "python",
    "MicroBuild.Core",
    "Microsoft.VSSDK.BuildTools",
    "Newtonsoft.Json",
    "Wix"
)

if (-not $vstarget) {
    $vstarget = "15.0"
} elseif ($vstarget.ToString() -match "^\d\d$") {
    $vstarget = "$vstarget.0"
}

$buildroot = $MyInvocation.MyCommand.Definition | Split-Path -Parent | Split-Path -Parent

if (-not $outdir) {
    if ($env:BUILD_BINARIESDIRECTORY) {
        $outdir = "${env:BUILD_BINARIESDIRECTORY}"
    } else {
        $outdir = "$buildroot\packages"
    }
}

# Wonderful hack because Resolve-Path fails if the path doesn't exist
$outdir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($outdir)

pushd "$buildroot\Build"
try {
    if ($source) {
        .\nuget.exe sources add -Name PreBuildSource -Source $source
    }
    $arglist = "restore", "$vstarget\packages.config", "-OutputDirectory", $outdir

    try {
        Start-Process -Wait -NoNewWindow .\nuget.exe -ErrorAction Stop -ArgumentList $arglist
    } finally {
        if ($source) {
            .\nuget.exe sources remove -Name PreBuildSource
        }
    }

    $versions = @{}
    ([xml](gc "$vstarget\packages.config")).packages.package | %{ $versions[$_.id] = $_.version }

    $need_symlink | ?{ $versions[$_] } | %{
        $existing = gi "$outdir\$_" -EA 0
        if ($existing) {
            if ($existing.LinkType) {
                $existing.Delete()
            } else {
                Write-Host "Deleting directory $existing to create a symlink"
                del -Recurse -Force $existing
            }
        }
        Write-Host "Creating symlink for $_.$($versions[$_])"
        New-Item -ItemType Junction "$outdir\$_" -Value "$outdir\$_.$($versions[$_])"
    } | Out-Null
    
} finally {
    popd
}