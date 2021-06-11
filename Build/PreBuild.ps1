param ($pylanceTgz, $vstarget, $source, $outdir)

$ErrorActionPreference = "Stop"

"Restoring Packages"

# These packages require a versionless symlink pointing to the versioned install.
$need_symlink = @(
    "python",
    "MicroBuild.Core",
    "Microsoft.Python.Parsing",
    "Microsoft.DiaSymReader.Pdb2Pdb",
    "Microsoft.Extensions.FileSystemGlobbing",
    "Microsoft.VisualStudio.LanguageServer.Protocol",
    "Microsoft.VisualStudio.Debugger.Engine",
    "Microsoft.VisualStudio.Interop",
    "Microsoft.VSSDK.BuildTools",
    "Microsoft.VSSDK.Debugger.VSDConfigTool",
    "Newtonsoft.Json"
)

if (-not $vstarget) {
    $vstarget = "17.0"
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

Push-Location "$buildroot\Build"
try {
    $arglist = "restore", "$vstarget\packages.config", "-OutputDirectory", "`"$outdir`"", "-Config", "nuget.config", "-NonInteractive"
    $nuget = Get-Command nuget.exe -EA 0
    if (-not $nuget) {
        $nuget = Get-Command .\nuget.exe
    }
    Start-Process -Wait -NoNewWindow $nuget.Source -ErrorAction Stop -ArgumentList $arglist

    $versions = @{}
    ([xml](Get-Content "$vstarget\packages.config")).packages.package | ForEach-Object{ $versions[$_.id] = $_.version }

    $need_symlink | Where-Object{ $versions[$_] } | ForEach-Object{
        $existing = Get-Item "$outdir\$_" -EA 0
        if ($existing) {
            if ($existing.LinkType) {
                $existing.Delete()
            } else {
                Write-Host "Deleting directory $existing to create a symlink"
                Remove-Item -Recurse -Force $existing
            }
        }
        Write-Host "Creating symlink for $_.$($versions[$_])"
        New-Item -ItemType Junction "$outdir\$_" -Value "$outdir\$_.$($versions[$_])"
    } | Out-Null

    $debugpyver = Get-Content "$buildroot\Build\debugpy-version.txt" -Raw 
    Write-Host "Downloading debugpy version $debugpyver"
    $debugpyarglist = "install_debugpy.py", $debugpyver, "`"$outdir`""
    Start-Process -Wait -NoNewWindow "$outdir\python\tools\python.exe" -ErrorAction Stop -ArgumentList $debugpyarglist

    Write-Host "Updating Microsoft.Python.*.dll pdbs to be windows format"
    Get-ChildItem "..\packages\Microsoft.Python.Parsing\lib\netstandard2.0" -Filter "*.pdb" | ForEach-Object {
        # Convert each pdb $_.FullName
        $dir = $_.Directory
        $base = $_.BaseName
        Write-Host "Modifying" $_.Name
        Start-Process -Wait -NoNewWindow "packages\Microsoft.DiaSymReader.Pdb2Pdb\tools\Pdb2Pdb.exe" -ErrorAction Stop -ArgumentList "$dir\$base.dll"
        # That should have created a pdb2 file. Rename it to the .pdb file
        Copy-Item $_.FullName "$dir\$base.old_pdb"
        Copy-Item "$dir\$base.pdb2" $_.FullName -Force
    } | Out-Null
} finally {
    Pop-Location
}