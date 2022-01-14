param ($vstarget, $outdir, $pylanceVersion, $debugpyVersion)

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

if (-not $pylanceVersion) {
    $pylanceVersion = "latest"
}

if (-not $debugpyVersion) {
    $debugpyVersion = "latest"
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

    # overwrite the pylance version in the package.json with the specified version
    $packageJsonFile = Join-Path $buildroot "package.json"
    $packageJson = Get-Content $packageJsonFile -Raw | ConvertFrom-Json
    # only overwrite if the values are different
    if ($packageJson.devDependencies.'@pylance/pylance' -ne $pylanceVersion) {
        $packageJson.devDependencies.'@pylance/pylance' = $pylanceVersion
        # ConvertTo-Json has a default depth of 2, so make it bigger to avoid strange errors
        $packageJson | ConvertTo-Json -depth 8 | Set-Content $packageJsonFile
    }

    # install pylance and update the package-lock.json file
    npm install --save

    # print out the installed version
    $npmLsOutput = & npm ls @pylance/pylance
    $installedPylanceVersion = $npmLsOutput[1] -split "@" | Select-Object -Last 1
    $installedPylanceVersion = $installedPylanceVersion.Trim()
    "Installed Pylance $installedPylanceVersion"
    
    # add azdo build tag
    Write-Host "##vso[build.addbuildtag]Pylance-$installedPylanceVersion"

    "-----"
    "Restoring Packages"
    $arglist = "restore", "$vstarget\packages.config", "-OutputDirectory", "`"$outdir`"", "-Config", "nuget.config", "-NonInteractive"
    $nuget = Get-Command nuget.exe -EA 0
    if (-not $nuget) {
        $nuget = Get-Command .\nuget.exe
    }
    Start-Process -Wait -NoNewWindow $nuget.Source -ErrorAction Stop -ArgumentList $arglist

    $versions = @{}
    ([xml](Get-Content "$vstarget\packages.config")).packages.package | ForEach-Object { $versions[$_.id] = $_.version }

    $need_symlink | Where-Object { $versions[$_] } | ForEach-Object {
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

    # debugpy install must come after package restore because it uses python which is symlinked as part of the previous step

    "-----"
    "Installing Debugpy"
    # pip install python packaging utilities
    # SilentlyContinue on error since pip warnings will cause the build to fail, and installing debugpy will fail later if this step fails anyway
    $pipArgList = "-m", "pip", "--disable-pip-version-check", "install", "packaging" 
    Start-Process -Wait -NoNewWindow "$outdir\python\tools\python.exe" -ErrorAction SilentlyContinue -ArgumentList $pipArgList

    # install debugpy
    $debugpyArglist = "install_debugpy.py", $debugpyVersion, "`"$outdir`""
    Start-Process -Wait -NoNewWindow "$outdir\python\tools\python.exe" -ErrorAction Stop -ArgumentList $debugpyArglist

    # print out the installed version
    $installedDebugpyVersion = ""
    $versionPyFile = Join-Path $outdir "debugpy\_version.py"
    foreach ($line in Get-Content $versionPyFile) {
        if ($line.Trim().StartsWith("`"version`"")) {
            $installedDebugpyVersion = $line.split(":")[1].Trim(" `"") # trim spaces and double quotes
            break
        }
    }
    "Installed Debugpy $installedDebugpyVersion"

    # write debugpy version out to $buildroot\build\debugpy-version.txt, since that file is used by Debugger.csproj and various other classes
    Set-Content -NoNewline -Force -Path "$buildroot\build\debugpy-version.txt" -Value $installedDebugpyVersion

    # add azdo build tag
    Write-Host "##vso[build.addbuildtag]Debugpy-$installedDebugpyVersion"

    "-----"
    "Updating Microsoft.Python.*.dll pdbs to be windows format"
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