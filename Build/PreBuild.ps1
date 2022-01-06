param ($pylanceTgz, $vstarget, $source, $outdir)

$ErrorActionPreference = "Stop"

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

    # Install pylance version specified in package.json
    # If this doesn't work, you probably need to set up your .npmrc file or you need permissions to the feed.
    # See https://microsoft.sharepoint.com/teams/python/_layouts/15/Doc.aspx?sourcedoc=%7B30d33826-9f98-4d3e-890e-b7d198bbbcbe%7D&action=edit&wd=target(Python%20VS%2FDev%20Docs.one%7Cd7206ce2-cf40-437b-8ce9-1e55f4bc2f44%2FPylance%20in%20VS%7C6000d391-4e62-4a4d-89d2-7f7c1f005639%2F)&share=IgEmONMwmJ8-TYkOt9GYu7y-AeCM6R8r8Myty0Lj8CeOs4E
    # Note that this will modify your package-lock.json file if the version was updated. This file should be committed into source control.
    "Installing Pylance"
    npm install --save
    # add the package lock file changes into git
    $packageLockPath = Join-Path $buildroot "package-lock.json"
    git add $packageLockPath
    # print the installed version
    $output = & npm ls @pylance/pylance
    $pylanceVersion = $output[1] -split "@" | Select-Object -Last 1
    "Installed Pylance $pylanceVersion"
    # add azdo build tag
    # commenting this out for now since azdo is throwing errors for an unknown reason
    #Write-Host "##vso[build.addbuildtag]Pylance-$pylanceVersion"

    "Restoring Packages"
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
    Get-ChildItem "$outdir\Microsoft.Python.Parsing\lib\netstandard2.0" -Filter "*.pdb" | ForEach-Object {
        # Skip if there's already a pdb2 file
        # Convert each pdb $_.FullName
        $dir = $_.Directory
        $base = $_.BaseName
        $pdb2 = "$dir\$base.pdb2"
        if (!(Test-Path $pdb2)) {
            Write-Host "Modifying" $_.Name
            Start-Process -Wait -NoNewWindow "$outdir\Microsoft.DiaSymReader.Pdb2Pdb\tools\Pdb2Pdb.exe" -ErrorAction Stop -ArgumentList "$dir\$base.dll"
            # That should have created a pdb2 file. Rename it to the .pdb file
            Copy-Item $_.FullName "$dir\$base.old_pdb"
            Copy-Item "$dir\$base.pdb2" $_.FullName -Force
        } else {
            Write-Host "Already updated the pdb for" $_.FullName
        }
    } | Out-Null
} finally {
    Pop-Location
}