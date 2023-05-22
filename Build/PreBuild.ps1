<#
    This script installs dependencies for PTVS, including pylance, debugpy, and all nuget packages.

    PTVS consumes a public azure feed, defined in nuget.config.
    However, the feed needs to be populated (once) when upgrading to new package versions, and feed population requires authentication.
    See https://github.com/microsoft/PTVS/wiki/Build-and-Debug-Instructions-for-PTVS for instructions on how to authenticate.
#>

param (
    # The visual studio major version we are targeting, defaults to 17.0
    [Parameter()]
    [string] $vstarget = "17.0",

    # The directory where packages should be restored to, defaults to the root of the repo
    [Parameter()]
    [string] $outdir, 
    
    # The version of pylance we should download, defaults to "latest"
    [Parameter()]
    [string] $pylanceVersion = "latest", 
    
    # The version of debugpy we should download, defaults to "latest"
    [Parameter()]
    [string] $debugpyVersion = "latest", 
    
    # Run in interactive mode for azure feed authentication, defaults to false
    [Parameter()]
    [switch] $interactive
)

$ErrorActionPreference = "Stop"

if ($vstarget.ToString() -match "^\d\d$") {
    $vstarget = "$vstarget.0"
}

# Use a different MicroBuildCore package for VS >= 17.0
$microBuildCorePackageName = "Microsoft.Core"
if ([int] $vstarget -ge 17) {
    $microBuildCorePackageName = "Microsoft.VisualStudioEng.MicroBuild.Core"
}

# These packages require a versionless symlink pointing to the versioned install.
$need_symlink = @(
    "python",
    "Microsoft.DiaSymReader.Pdb2Pdb",
    "Microsoft.Extensions.FileSystemGlobbing",
    "Microsoft.VisualStudio.LanguageServer.Protocol",
    "Microsoft.VisualStudio.Debugger.Engine",
    "Microsoft.VisualStudio.Interop",
    "Microsoft.VSSDK.BuildTools",
    "Microsoft.VSSDK.Debugger.VSDConfigTool",
    "Newtonsoft.Json",
    $microBuildCorePackageName
)

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

    # delete pylance install folder to make sure we always get latest
    $nodeModulesPath = Join-Path $buildroot "node_modules"
    if (Test-Path -Path $nodeModulesPath) {
        Remove-Item -Recurse -Force $nodeModulesPath
    }

    # install latest pylance
    npm install

    # exit on error
    if ($LASTEXITCODE -ne 0) {
        "npm returned non-zero, exiting..."
        exit 1
    }

    # print out the installed version
    $npmLsOutput = & npm ls @pylance/pylance
    $installedPylanceVersion = $npmLsOutput[1] -split "@" | Select-Object -Last 1
    $installedPylanceVersion = $installedPylanceVersion.Trim()
    "Installed Pylance $installedPylanceVersion"
    
    # add azdo build tag for non-pull requests
    if ($env:BUILD_REASON -and -not $env:BUILD_REASON.Contains("PullRequest")) {
        Write-Host "##vso[build.addbuildtag]Pylance $installedPylanceVersion"
    }

    "-----"
    "Restoring Packages"
    # If you have authentication errors here, try passing -interactive on the command line
    $arglist = "restore", "$vstarget\packages.config", "-OutputDirectory", "`"$outdir`"", "-Config", "nuget.config"
    if (-not $interactive) {
        $arglist += "-NonInteractive"
    }
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
    if ($env:BUILD_REASON -and -not $env:BUILD_REASON.Contains("PullRequest")) {
        Write-Host "##vso[build.addbuildtag]Debugpy $installedDebugpyVersion"
    }

} finally {
    Pop-Location
}