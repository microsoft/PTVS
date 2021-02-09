param ($vstarget, $source, $outdir)

$ErrorActionPreference = "Stop"

"Restoring Packages"

# These packages require a versionless symlink pointing to the versioned install.
$need_symlink = @(
    "python",
    "MicroBuild.Core",
    "Microsoft.VSSDK.BuildTools",
    "Microsoft.VSSDK.Debugger.VSDConfigTool",
    "Newtonsoft.Json",
    "Microsoft.VisualStudio.Python.LanguageServer"
)

if (-not $vstarget) {
    $vstarget = "16.0"
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
    $arglist = "restore", "$vstarget\packages.config", "-OutputDirectory", $outdir, "-Config", "$vstarget\nuget.config", "-NonInteractive"
    $nuget = gcm nuget.exe -EA 0
    if (-not $nuget) {
        $nuget = gcm .\nuget.exe
    }
    Start-Process -Wait -NoNewWindow $nuget.Source -ErrorAction Stop -ArgumentList $arglist

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

    $debugpyver = Get-Content "$buildroot\Build\debugpy-version.txt" -Raw 
    Write-Host "Downloading debugpy version $debugpyver"
    $debugpyarglist = "install_debugpy.py", $debugpyver, $outdir
    Start-Process -Wait -NoNewWindow "$outdir\python\tools\python.exe" -ErrorAction Stop -ArgumentList $debugpyarglist
} finally {
    popd
}