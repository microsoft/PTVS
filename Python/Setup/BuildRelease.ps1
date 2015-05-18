<#
.Synopsis
    Builds a release of Python Tools for Visual Studio from this branch.

.Description
    This script is used to build a set of installers for Python Tools for
    Visual Studio based on the code in this branch.
    
    The assembly and file versions are generated automatically and provided by
    modifying .\Build\AssemblyVersion.cs.
    
    The source is determined from the location of this script; to build another
    branch, use its Copy-Item of BuildRelease.ps1.

.Parameter outdir
    Directory to store the build.
    
    If `release` is specified, defaults to '\\pytools\release\<build number>'.

.Parameter vstarget
    [Optional] The VS version to build for. If omitted, builds for all supported
    versions that are installed.
    
    Valid values: "10.0", "11.0", "12.0"

.Parameter name
    [Optional] A suffix to append to the name of the build.
    
    Typical values: "2.1 Alpha", "1.5 RC1", "My Feature Name", "2014-02-11 Dev Build"
    (Avoid: "RTM", "2.0 RTM")

.Parameter release
    When specified:
    * `outdir` will default to \\pytools\release\x.y if unspecified
    * A build number is generated and appended to `outdir`
     - The build number includes an index
    * Debug configurations are not built
    * Binaries and symbols are sent for indexing
    * Binaries and installers are sent for signing
    
    This switch requires the code signing object to be installed, and a smart
    card and reader must be available.
    
    See also: `mockrelease`.

.Parameter internal
    When specified:
    * `outdir` will default to \\pytools\release\Internal\$name if
      unspecified
    * A build number is generated and appended to `outdir`
     - The build number includes an index
    * Both Release and Debug configurations are built
    * No binaries are sent for indexing or signing
    
    See also: `release`, `mockrelease`

.Parameter mockrelease
    When specified:
    * A build number is generated and appended to `outdir`
     - The build number includes an index
    * Both Release and Debug configurations are built
    * Indexing requests are displayed in the output but are not sent
    * Signing requests are displayed in the output but are not sent
    
    Note that `outdir` is required and has no default.
    
    This switch requires the code signing object to be installed, but no smart
    card or reader is necessary.
    
    See also: `release`, `internal`

.Parameter scorch
    If specified, the enlistment is cleaned before and after building.

.Parameter skiptests
    If specified, test projects are not built.

.Parameter skipclean
    If specified, the output directory is not cleaned before building. This has
    no effect when used with `release`, since the output directory will not
    exist before the build.

.Parameter skipcopy
    If specified, does not copy the source files to the output directory.

.Parameter skipdebug
    If specified, does not build Debug configurations.

.Parameter dev
    If specified, generates a build name from the current date.

.Parameter copytests
    If specified, copies the built test folder to the output directory.

.Example
    .\BuildRelease.ps1 -release
    
    Creates signed installers for public release in \\pytools\release\<version>

.Example
    .\BuildRelease.ps1 -name "Beta" -release
    
    Create installers for a public beta in \\pytools\release\<version>

.Example
    .\BuildRelease.ps1 -name "My Feature" -internal
    
    Create installers for an internal feature test in 
    \\pytools\release\Internal\My Feature\<version>

#>
[CmdletBinding()]
param(
    [string] $outdir,
    [string[]] $vsTarget,
    [string] $name,
    [switch] $release,
    [switch] $internal,
    [switch] $mockrelease,
    [switch] $scorch,
    [switch] $skiptests,
    [switch] $skipclean,
    [switch] $skipcopy,
    [switch] $skipdebug,
    [switch] $skipbuild,
    [switch] $dev,
    [switch] $copytests
)

$buildroot = (Split-Path -Parent $MyInvocation.MyCommand.Definition)
while ((Test-Path $buildroot) -and -not (Test-Path "$buildroot\build.root")) {
    $buildroot = (Split-Path -Parent $buildroot)
}
Write-Output "Build Root: $buildroot"


# This value is used to determine the most significant digit of the build number.
$base_year = 2012
# This value is used to automatically generate outdir for -release and -internal builds
$base_outdir = "\\pytools\Release"

# This file is parsed to find version information
$version_file = gi "$buildroot\Python\Product\AssemblyVersion.cs"

$build_project = gi "$buildroot\Python\dirs.proj"
$setup_project = gi "$buildroot\Python\Setup\setup.proj"

# Project metadata
$project_name = "Python Tools for Visual Studio"
$project_url = "http://pytools.codeplex.com"
$project_keywords = "PTVS; Visual Studio; Python"

# These people are able to approve code signing operations
$approvers = "smortaz", "dinov", "stevdo", "pminaev", "gilbertw", "huvalo", "jinglou", "sitani", "crwilcox"

# These people are the contacts for the symbols uploaded to the symbol server
$symbol_contacts = "$env:username;dinov;smortaz;stevdo;gilbertw"

# This single person or DL is the contact for virus scan notifications
$vcs_contact = "ptvscore"

# These options are passed to all MSBuild processes
$global_msbuild_options = @("/v:m", "/m", "/nologo")

if ($skiptests) {
    $global_msbuild_options += "/p:IncludeTests=false"
} else {
    $global_msbuild_options += "/p:IncludeTests=true"
}

if ($release -or $mockrelease) {
    $global_msbuild_options += "/p:ReleaseBuild=true"
}

if (Test-Path $buildroot\..\PTVS-pr\VsLogger\VsLogger.csproj) {
    $global_msbuild_options += "/p:IncludeVsLogger=true"
}

# This function is used to get options for each configuration
#
# $target contains the following members:
#   VSTarget            e.g. 12.0
#   VSName              e.g. VS 2013
#   config              Name of the build configuration
#   msi_version         X.Y.Z.W installer version
#   release_version     X.Y install version
#   assembly_version    X.Y.Z assembly version
#   logfile             Build log file
#   destdir             Root directory of all outputs
#   unsigned_bindir     Output directory for unsigned binaries
#   unsigned_msidir     Output directory for unsigned installers
#   symboldir           Output directory for debug symbols
#   final_msidir        The directory where the final installers end up
#
# The following members are available if $release or $mockrelease
#   signed_logfile      Rebuild log file (after signing)
#   signed_bindir       Output directory for signed binaries
#   signed_msidir       Output directory for signed installers
#   signed_unsigned_msidir  Output directory for unsigned installers containing signed binaries
function msbuild-options($target, $config) {
    @(
        "/p:VSTarget=$($target.VSTarget)",
        "/p:VisualStudioVersion=$($target.VSTarget)",
        "/p:CopyOutputsToPath=$($target.destdir)",
        "/p:Configuration=$($target.config)",
        "/p:MsiVersion=$($target.msi_version)",
        "/p:ReleaseVersion=$($target.release_version)"
    )
}

# This function is invoked after each target is built.
function after-build($buildroot, $target) {
    Copy-Item -Force "$buildroot\Python\Prerequisites\*.reg" $($target.destdir)
    
    if ($copytests) {
        Copy-Item -Recurse -Force "$buildroot\BuildOutput\$($target.config)$($target.VSTarget)\Tests" "$($target.destdir)\Tests"
        Copy-Item -Recurse -Force "$buildroot\Python\Tests\TestData" "$($target.destdir)\Tests\TestData"
    }
}

# This function is invoked after the entire build process but before scorching
function after-build-all($buildroot, $outdir) {
    if (-not $release) {
        Copy-Item -Force "$buildroot\Python\Prerequisites\*.reg" $outdir
    }
}

# Add product name mappings here
#   {0} will be replaced by the major version preceded by a space
#   {1} will be replaced by the build name preceded by a space
#   {2} will be replaced by the VS name preceded by a space
#   {3} will be replaced by the config ('Debug') marker preceded by a space
$installer_names = @{
    'PythonToolsInstaller.msi'="PTVS{1}{2}{3}.msi";
    "WFastCGI.msi"="WFastCGI{1}{3}.msi";
    "Microsoft.PythonTools.Samples.vsix"="PTVS Samples{1}{3}.vsix";
    "Microsoft.PythonTools.ML.vsix"="PTVS ML{1}{3}.vsix";
}

# Add list of files requiring signing here
$managed_files = (
    "Microsoft.PythonTools.Analysis.dll", 
    "Microsoft.PythonTools.Analyzer.exe", 
    "Microsoft.PythonTools.Attacher.exe", 
    "Microsoft.PythonTools.AttacherX86.exe", 
    "Microsoft.PythonTools.BuildTasks.dll", 
    "Microsoft.PythonTools.Debugger.dll", 
    "Microsoft.PythonTools.EnvironmentsList.dll", 
    "Microsoft.PythonTools.dll", 
    "Microsoft.PythonTools.VSInterpreters.dll",
    "Microsoft.PythonTools.TestAdapter.dll",
    "Microsoft.PythonTools.Hpc.dll", 
    "Microsoft.PythonTools.ImportWizard.dll", 
    "Microsoft.PythonTools.IronPython.dll", 
    "Microsoft.PythonTools.IronPython.Interpreter.dll", 
    "Microsoft.PythonTools.ML.dll", 
    "Microsoft.PythonTools.MpiShim.exe", 
    "Microsoft.PythonTools.Profiling.dll", 
    "Microsoft.PythonTools.ProjectWizards.dll", 
    "Microsoft.VisualStudio.ReplWindow.dll",
    "Microsoft.PythonTools.WebRole.dll",
    "Microsoft.PythonTools.Django.dll",
    "Microsoft.PythonTools.VsLogger.dll",
    "Microsoft.PythonTools.Uwp.dll",
    "Microsoft.PythonTools.AzureSetup.exe",
    "Microsoft.IronPythonTools.Resolver.dll"
)

$native_files = (
    "PyDebugAttach.dll",
    "PyDebugAttachX86.dll",
    "Microsoft.PythonTools.Debugger.Helper.x86.dll",
    "Microsoft.PythonTools.Debugger.Helper.x64.dll",
    "VsPyProf.dll",
    "VsPyProfX86.dll"
)

$supported_vs_versions = (
    @{number="14.0"; name="VS 2015"; build_by_default=$true},
    @{number="12.0"; name="VS 2013"; build_by_default=$true},
    @{number="11.0"; name="VS 2012"; build_by_default=$false},
    @{number="10.0"; name="VS 2010"; build_by_default=$false}
)

# #############################################################################
# #############################################################################
#
# The remainder of this file is product independent.
#
# #############################################################################
# #############################################################################


if (-not (Get-Command msbuild -EA 0)) {
    Throw "Visual Studio build tools are required."
}

if (-not $outdir -and -not $release -and -not $internal) {
    if (-not $outdir) {
        Throw "Invalid output directory '$outdir'"
    }
}

if ($dev) {
    if ($name) {
        Throw "Cannot specify both -dev and -name"
    }
    $name = "Dev {0:yyyy-MM-dd}" -f (Get-Date)
}

if ($name -match "[0-9.]*\s*RTM") {
    $result = $host.ui.PromptForChoice(
        "Build Name",
        "'RTM' is not a recommended build name. Final releases should have a blank name.",
        [System.Management.Automation.Host.ChoiceDescription[]](
            (New-Object System.Management.Automation.Host.ChoiceDescription "&Continue", "Continue anyway"),
            (New-Object System.Management.Automation.Host.ChoiceDescription "&Abort", "Abort the build"),
            (New-Object System.Management.Automation.Host.ChoiceDescription "C&lear", "Clear the build name and continue")
        ),
        2
    )
    if ($result -eq 1) {
        exit 0
    } elseif ($result -eq 2) {
        $name = ""
    }
}

$signedbuild = $release -or $mockrelease
if ($signedbuild) {
    $approvers = @($approvers | Where-Object {$_ -ne $env:USERNAME})

    Push-Location (Split-Path -Parent $MyInvocation.MyCommand.Definition)
    if ($mockrelease) {
        Set-Variable -Name DebugPreference -Value "Continue" -Scope "global"
        Import-Module -Force $buildroot\Build\BuildReleaseMockHelpers.psm1
    } else {
        Import-Module -Force $buildroot\Build\BuildReleaseHelpers.psm1
    }
    Pop-Location
}


$spacename = ""
if ($name) {
    $spacename = " $name"
    $global_msbuild_options += "/p:CustomBuildIdentifier=$name"
} elseif ($internal) {
    Throw "'-name [build name]' must be specified when using '-internal'"
}


$version_file_backed_up = 0
# Force use of a backup if there are pending changes to $version_file
$version_file_force_backup = 0
if (-not (tf status $version_file /format:detailed | Select-String "There are no pending changes.")) {
    Write-Output "$version_file has pending changes. Using backup instead of tf undo."
    $version_file_force_backup = 1
}
$version_file_is_readonly = $version_file.Attributes -band [io.FileAttributes]::ReadOnly

$assembly_version = [regex]::Match((Get-Content $version_file), 'ReleaseVersion = "([0-9.]+)";').Groups[1].Value
$release_version = [regex]::Match((Get-Content $version_file), 'FileVersion = "([0-9.]+)";').Groups[1].Value

if ($internal) {
    $base_outdir = "$base_outdir\Internal\$name"
} elseif ($release) {
    $base_outdir = "$base_outdir\$release_version"
}

if (-not $outdir) {
    $outdir = $base_outdir
}

$buildnumber = '{0}{1:MMdd}.{2:D2}' -f (((Get-Date).Year - $base_year), (Get-Date), 0)
if ($release -or $mockrelease -or $internal) {
    for ($buildindex = 0; $buildindex -lt 10000; $buildindex += 1) {
        $buildnumber = '{0}{1:MMdd}.{2:D2}' -f (((Get-Date).Year - $base_year), (Get-Date), $buildindex)
        if (-not (Test-Path $outdir\$buildnumber)) {
            break
        }
        $buildnumber = ''
    }
}
if (-not $buildnumber) {
    Throw "Cannot create version number. Try another output folder."
}
if ([int]::Parse([regex]::Match($buildnumber, '^[0-9]+').Value) -ge 65535) {
    Throw "Build number $buildnumber is invalid. Update `$base_year in this script.
(If the year is not yet $($base_year + 7) then something else has gone wrong.)"
}

$msi_version = "$release_version.$buildnumber"

if ($internal -or $release -or $mockrelease) {
    $outdir = "$outdir\$buildnumber"
}

$target_versions = @()

if ($vstarget) {
    $vstarget = $vstarget | %{ "{0:00.0}" -f [float]::Parse($_) }
}
foreach ($target_vs in $supported_vs_versions) {
    if ((-not $vstarget -and $target_vs.build_by_default) -or ($target_vs.number -in $vstarget)) {
        $vspath = Get-ItemProperty -Path "HKLM:\Software\Wow6432Node\Microsoft\VisualStudio\$($target_vs.number)" -EA 0
        if (-not $vspath) {
            $vspath = Get-ItemProperty -Path "HKLM:\Software\Microsoft\VisualStudio\$($target_vs.number)" -EA 0
        }
        if ($vspath -and $vspath.InstallDir -and (Test-Path -Path $vspath.InstallDir)) {
            $target_versions += $target_vs
        }
    }
}

if (-not $target_versions) {
    Throw "No supported versions of Visual Studio installed."
}

if ($skipdebug -or $release) {
    $target_configs = ("Release")
} else {
    $target_configs = ("Debug", "Release")
}

Write-Output ""
Write-Output "============================================================"
Write-Output ""
if ($name) {
    Write-Output "Build Name: $name"
}
Write-Output "Output Dir: $outdir"
if ($mockrelease) {
    Write-Output "Auto-generated release outdir: $base_outdir\$release_version\$buildnumber"
}
Write-Output ""
Write-Output "Product version: $assembly_version.`$(VS version)"
Write-Output "MSI version: $msi_version"
Write-Output "Building for $([String]::Join(", ", ($target_versions | % { $_.name })))"
Write-Output ""
Write-Output "============================================================"
Write-Output ""

if (-not $skipclean) {
    if ((Test-Path $outdir) -and (Get-ChildItem $outdir)) {
        Write-Output "Cleaning previous release in $outdir"
        del -Recurse -Force $outdir\* -EA 0
        while (Get-ChildItem $outdir) {
            Write-Output "Failed to clean release. Retrying in five seconds. (Press Ctrl+C to abort)"
            Sleep -Seconds 5
            del -Recurse -Force $outdir\* -EA 0
        }
    }
    if (-not (Test-Path $outdir)) {
        mkdir $outdir -EA 0 | Out-Null
        if (-not $?) {
            Throw "Could not make output directory: $outdir"
        }
    }
}

$logdir = mkdir "$outdir\Logs" -Force

if ($scorch) {
    tfpt scorch $buildroot /noprompt
}

$failed_logs = @()

Push-Location $buildroot
try {
    $successful = $false
    if (-not $version_file_force_backup) {
        tf edit $version_file | Out-Null
    }
    if ($version_file_force_backup -or -not $?) {
        # running outside of MS
        Copy-Item -Force $version_file "$($version_file).bak"
        $version_file_backed_up = 1
    }
    Set-ItemProperty $version_file -Name IsReadOnly -Value $false
    (Get-Content $version_file) | %{ $_ -replace ' = "4100.00"', (' = "' + $buildnumber + '"') } | Set-Content $version_file

    foreach ($config in $target_configs) {
        # See the description near the msbuild_config function
        $target_info = @($target_versions | %{ 
            $i = @{
                VSTarget=$($_.number);
                VSName=$($_.name);
                destdir=mkdir "$outdir\$($_.name)\$config" -Force;
                logfile="$logdir\BuildRelease.$config.$($_.number).log";
                config=$config;
                msi_version=$msi_version;
                release_version=$release_version;
            }
            $i.unsigned_bindir = mkdir "$($i.destdir)\UnsignedBinaries" -Force
            $i.unsigned_msidir = mkdir "$($i.destdir)\UnsignedMsi" -Force
            $i.symboldir = mkdir "$($i.destdir)\Symbols" -Force
            if ($signedBuild) {
                $i.signed_bindir = mkdir "$($i.destdir)\SignedBinaries" -Force
                $i.signed_unsigned_msidir = mkdir "$($i.destdir)\SignedBinariesUnsignedMsi" -Force
                $i.signed_msidir = mkdir "$($i.destdir)\SignedMsi" -Force
                $i.final_msidir = $i.signed_msidir
                $i.signed_logfile = "$logdir\BuildRelease_Signed.$config.$($_.number).log"
            } else {
                $i.final_msidir = $i.unsigned_msidir
            }
            $i
        })
        
        foreach ($i in $target_info) {
            if (-not $skipbuild) {
                $target_msbuild_options = msbuild-options $i
                if (-not $skipclean) {
                    msbuild /t:Clean $global_msbuild_options $target_msbuild_options $build_project
                }
                msbuild $global_msbuild_options $target_msbuild_options /fl /flp:logfile=$($i.logfile) $build_project

                if (-not $?) {
                    Write-Error "Build failed: $($i.VSName) $config"
                    $failed_logs += $i.logfile
                    continue
                }
            }
            
            after-build $buildroot $i
        }
        
        ######################################################################
        ##  BEGIN SIGNING CODE
        ######################################################################
        if ($signedBuild) {
            $jobs = @()
            
            foreach ($i in $target_info) {
                if ($i.logfile -in $failed_logs) {
                    Write-Output "Skipping signing for $($i.VSName) because the build failed"
                    continue
                }
                Write-Output "Submitting signing jobs for $($i.VSName)"

                $jobs += begin_sign_files `
                    @($managed_files | %{@{path="$($i.unsigned_bindir)\$_"; name=$project_name}} | ?{Test-Path $_.path}) `
                    $i.signed_bindir $approvers `
                    $project_name $project_url "$project_name $($i.VSName) - managed code" $project_keywords `
                    "authenticode;strongname" `
                    -delaysigned

                $jobs += begin_sign_files `
                    @($native_files | %{@{path="$($i.unsigned_bindir)\$_"; name=$project_name}} | ?{Test-Path $_.path}) `
                    $i.signed_bindir $approvers `
                    $project_name $project_url "$project_name $($i.VSName) - native code" $project_keywords `
                    "authenticode" 
            }
            
            end_sign_files $jobs
            
            foreach ($i in $target_info) {
                if ($i.logfile -in $failed_logs) {
                    Write-Output "Skipping symbol submission for $($i.VSName) because the build failed"
                    continue
                }
                submit_symbols "$project_name$spacename" "$buildnumber $($i.VSName)" "binaries" $i.signed_bindir $symbol_contacts
                submit_symbols "$project_name$spacename" "$buildnumber $($i.VSName)" "symbols" $i.symboldir $symbol_contacts

                $target_msbuild_options = msbuild-options $i
                msbuild $global_msbuild_options $target_msbuild_options `
                    /fl /flp:logfile=$($i.signed_logfile) `
                    /p:SignedBinariesPath=$($i.signed_bindir) `
                    /p:RezipVSIXFiles=true `
                    $setup_project
            }

            $jobs = @()
            
            foreach ($i in $target_info) {
                if ($i.logfile -in $failed_logs) {
                    continue
                }

                $msi_files = @((Get-ChildItem "$($i.signed_unsigned_msidir)\*.msi") | %{ @{
                    path="$_";
                    name="$project_name - $($_.Name)"
                }})

                if ($msi_files.Count -gt 0) {
                    Write-Output "Submitting MSI signing job for $($i.VSName)"

                    $jobs += begin_sign_files $msi_files $i.signed_msidir $approvers `
                        $project_name $project_url "$project_name $($i.VSName) - installer" $project_keywords `
                        "authenticode"
                }


                $vsix_files = @((Get-ChildItem "$($i.signed_unsigned_msidir)\*.vsix") | %{ @{
                    path="$_";
                    name="$project_name - $($_.Name)"
                }})

                if ($vsix_files.Count -gt 0) {
                    Write-Output "Submitting VSIX signing job for $($i.VSName)"

                    $jobs += begin_sign_files $vsix_files $i.signed_msidir $approvers `
                        $project_name $project_url "$project_name $($i.VSName) - VSIX" $project_keywords `
                        "authenticode;opc"
                }
            }

            end_sign_files $jobs
        }
        ######################################################################
        ##  END SIGNING CODE
        ######################################################################
        
        $fmt = @{}
        if ($release_version) { $fmt.release_version = " $release_version"} else { $fmt.release_version = "" }
        if ($name) { $fmt.name = " $name" } else { $fmt.name = "" }
        if ($config -match "debug") { $fmt.config = " Debug" } else { $fmt.config = "" }
        
        foreach ($i in $target_info) {
            if ($i.logfile -in $failed_logs) {
                continue
            }
            
            if ($i.VSName) {$fmt.VSName = " $($i.VSName)"} else {$fmt.VSName = ""}
            
            Get-ChildItem "$($i.final_msidir)\*.msi", "$($i.final_msidir)\*.vsix" | `
                ?{ $installer_names[$_.Name] } | `
                %{ @{
                    src=$_;
                    dest="$outdir\" + ($installer_names[$_.Name] -f
                        $fmt.release_version,
                        $fmt.name,
                        $fmt.VSName,
                        $fmt.config
                    ); 
                } } | `
                %{ Copy-Item $_.src $_.dest -Force -EA 0; $_ } | `
                %{ "Copied $($_.src) -> $($_.dest)" }
        }
    }
    
    after-build-all $buildroot $outdir
    
    if ($signedBuild) {
        check_signing $outdir
    }
    
    if ($scorch) {
        tfpt scorch $buildroot /noprompt
    }
    
    if (-not $skipcopy) {
        Write-Output "Copying source files"
        robocopy /s . $outdir\Sources /xd BuildOutput TestResults | Out-Null
    }
    
    if ($signedbuild) {
        start_virus_scan "$project_name$spacename" $vcs_contact $outdir
    }
    
    $successful = $true
} finally {
    try {
        if ($version_file_backed_up) {
            Move-Item "$version_file.bak" $version_file -Force
            if ($version_file_is_readonly) {
                Set-ItemProperty $version_file -Name IsReadOnly -Value $true
            }
            Write-Output "Restored $version_file"
        } elseif (-not $version_file_force_backup) {
            tf undo /noprompt $version_file | Out-Null
        }
        
        if (-not (Get-Content $version_file) -match ' = "4100.00"') {
            Write-Error "Failed to undo $version_file"
        }
    } finally {
        Pop-Location
    }
}

if ($successful) {
    Write-Output ""
    Write-Output "Build complete"
    Write-Output ""
    Write-Output "Installers were output to:"
    Write-Output "    $outdir"
    if ($failed_logs.Count -gt 0) {
        Write-Output ""
        Write-Warning "Some configurations failed to build."
        Write-Output "Review these log files for details:"
        foreach ($name in $failed_logs) {
            Write-Output "    $name"
        }
        exit 1
    }
    exit 0
} else {
    exit 1
}
