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
    [Optional] The VS version to build for. If omitted, builds for all versions
    that are installed.
    
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
    [string] $vsTarget,
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
    [switch] $dev
)

# This value is used to determine the most significant digit of the build number.
$base_year = 2012
# This value is used to automatically generate outdir for -release and -internal builds
$base_outdir = "\\pytools\Release"

$buildroot = (Split-Path -Parent $MyInvocation.MyCommand.Definition)
while ((Test-Path $buildroot) -and -not (Test-Path "$buildroot\build.root")) {
    $buildroot = (Split-Path -Parent $buildroot)
}
Write-Output "Build Root: $buildroot"

if (-not (get-command msbuild -EA 0)) {
    Write-Error -EA:Stop "
    Visual Studio build tools are required."
}

if (-not $outdir -and -not $release) {
    if ($internal) {
        $outdir = "$base_outdir\Internal"
    }
    if (-not $outdir) {
        Write-Error -EA:Stop "
    Invalid output directory '$outdir'"
    }
}

if ($dev) {
    if ($name) {
        Write-Error -EA:Stop "
    Cannot specify both -dev and -name"
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

$spacename = ""
if ($name) {
    $spacename = " $name"
} elseif ($internal) {
    Write-Error -EA:Stop "
    '-name [build name]' must be specified when using '-internal'"
}

$signedbuild = $release -or $mockrelease
if ($signedbuild) {
    $signedbuildText = "true"
    $approvers = "smortaz", "dinov", "stevdo", "pminaev", "gilbertw", "huvalo", "jinglou", "sitani"
    $approvers = @($approvers | Where-Object {$_ -ne $env:USERNAME})
    $symbol_contacts = "$env:username;dinov;smortaz;stevdo;gilbertw"
    
    $projectName = "Python Tools for Visual Studio"
    $projectUrl = "http://pytools.codeplex.com"
    $projectKeywords = "PTVS; Visual Studio; Python"

    Push-Location (Split-Path -Parent $MyInvocation.MyCommand.Definition)
    if ($mockrelease) {
        Set-Variable -Name DebugPreference -Value "Continue" -Scope "global"
        Import-Module -force $buildroot\Common\Setup\ReleaseMockHelpers.psm1
    } else {
        Import-Module -force $buildroot\Common\Setup\ReleaseHelpers.psm1
    }
    Pop-Location
} else {
    $signedbuildText = "false"
}

# Add new products here
# $($_.name) is currently unused
# $($_.msi) is the name of the built MSI
# $($_.outname1)$(buildname) $(targetvs.name)$($_.outname2) is the name of the final MSI
$products = @(
    @{name="PythonTools";
      msi="PythonToolsInstaller.msi";
      signtag="";
      outname1="PTVS"; outname2=".msi"
    }
)

$nonvs_products = @(
    @{name="WFastCGI";
      msi="WFastCGI.msi";
      signtag=" - WFastCGI";
      outname1="WFastCGI"; outname2=".msi"
    }
)

Push-Location $buildroot

$asmverfileBackedUp = 0
$asmverfile = Get-ChildItem Python\Product\AssemblyVersion.cs
# Force use of a backup if there are pending changes to $asmverfile
$asmverfileUseBackup = 0
if (-not (tf status $asmverfile /format:detailed | Select-String "There are no pending changes.")) {
    Write-Output "$asmverfile has pending changes. Using backup instead of tf undo."
    $asmverfileUseBackup = 1
}
$asmverfileIsReadOnly = $asmverfile.Attributes -band [io.fileattributes]::ReadOnly

$releaseVersion = [regex]::Match((Get-Content $asmverfile), 'ReleaseVersion = "([0-9.]+)";').Groups[1].Value
$fileVersion = [regex]::Match((Get-Content $asmverfile), 'FileVersion = "([0-9.]+)";').Groups[1].Value

if ($release -and -not $outdir) {
    $outdir = "$base_outdir\$fileVersion"
}

$buildnumber = '{0}{1:MMdd}.{2:D2}' -f (((Get-Date).Year - $base_year), (Get-Date), 0)
if ($release -or $mockrelease -or $internal) {
    if ($internal) {
        $outdirwithname = "$outdir\$name"
    } else {
        $outdirwithname = $outdir
    }
    for ($buildindex = 0; $buildindex -lt 10000; $buildindex += 1) {
        $buildnumber = '{0}{1:MMdd}.{2:D2}' -f (((Get-Date).Year - $base_year), (Get-Date), $buildindex)
        if (-not (Test-Path $outdirwithname\$buildnumber)) {
            break
        }
        $buildnumber = ''
    }
}
if (-not $buildnumber) {
    Write-Error -EA:Stop "
    Cannot create version number. Try another output folder."
}
if ([int]::Parse([regex]::Match($buildnumber, '^[0-9]+').Value) -ge 65535) {
    Write-Error -EA:Stop "
    Build number $buildnumber is invalid. Update `$base_year in this script.
    (If the year is not yet $($base_year + 7) then something else has gone wrong.)"
}

$version = "$fileVersion.$buildnumber"

if ($internal) {
    $outdir = "$outdir\$name\$buildnumber"
} elseif ($release -or $mockrelease) {
    $outdir = "$outdir\$buildnumber"
}

$supportedVersions = @{number="12.0"; name="VS 2013"}, @{number="11.0"; name="VS 2012"}, @{number="10.0"; name="VS 2010"}
$targetVersions = @()

foreach ($targetVs in $supportedVersions) {
    if (-not $vstarget -or ($vstarget -match $targetVs.number)) {
        $vspath = Get-ItemProperty -Path "HKLM:\Software\Wow6432Node\Microsoft\VisualStudio\$($targetVs.number)" -EA 0
        if (-not $vspath) {
            $vspath = Get-ItemProperty -Path "HKLM:\Software\Microsoft\VisualStudio\$($targetVs.number)" -EA 0
        }
        if ($vspath -and $vspath.InstallDir -and (Test-Path -Path $vspath.InstallDir)) {
            $targetVersions += $targetVs
        }
    }
}

if (-not $targetVersions) {
    Write-Error -EA:Stop "
    No supported versions of Visual Studio installed."
}

if ($skipdebug -or $release) {
    $targetConfigs = ("Release")
} else {
    $targetConfigs = ("Debug", "Release")
}

$target = "Rebuild"
if ($skipclean) {
    $target = "Build"
}


Write-Output ""
Write-Output "============================================================"
Write-Output ""
if ($name) {
    Write-Output "Build Name: $name"
}
Write-Output "Output Dir: $outdir"
if ($mockrelease) {
    Write-Output "Auto-generated release outdir: $base_outdir\$fileVersion\$buildnumber"
}
Write-Output ""
Write-Output "Product version: $releaseversion.`$(VS version)"
Write-Output "File version: $version"
Write-Output "Building for $([String]::Join(", ", ($targetversions | % { $_.name })))"
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
            Write-Error -EA:Stop "
    Could not make output directory: $outdir"
        }
    }
}

if ($scorch) {
    tfpt scorch /noprompt
}

$failed_logs = @()

try {
    $successful = $false
    if ($asmverfileUseBackup -eq 0) {
        tf edit $asmverfile
    }
    if ($asmverfileUseBackup -or $LASTEXITCODE -gt 0) {
        # running outside of MS
        Copy-Item -force $asmverfile "$($asmverfile).bak"
        $asmverfileBackedUp = 1
    }
    Set-ItemProperty $asmverfile -Name IsReadOnly -Value $false
    (Get-Content $asmverfile) | %{ $_ -replace ' = "4100.00"', (' = "' + $buildnumber + '"') } | Set-Content $asmverfile

    foreach ($config in $targetConfigs) {
        foreach ($targetVs in $targetVersions) {
            $bindir = "Binaries\$config$($targetVs.number)"
            $destdir = "$outdir\$($targetVs.name)\$config"
            mkdir $destdir -EA 0 | Out-Null
            $includeVsLogger = test-path Internal\Python\VsLogger\VsLogger.csproj
            
            if (-not $skiptests -and -not $skipbuild) {
                msbuild /m /v:m /fl /flp:"Verbosity=n;LogFile=BuildRelease.$config.$($targetVs.number).tests.log" `
                    /t:$target `
                    /p:Configuration=$config `
                    /p:WixVersion=$version `
                    /p:WixReleaseVersion=$fileVersion `
                    /p:VSTarget=$($targetVs.number) `
                    /p:VisualStudioVersion=$($targetVs.number) `
                    /p:"CustomBuildIdentifier=$name" `
                    /p:ReleaseBuild=$signedbuildText `
                    Python\Tests\dirs.proj

                if ($LASTEXITCODE -gt 0) {
                    Write-Error -EA:Continue "Test build failed: $config"
                    $failed_logs += Get-Item "BuildRelease.$config.$($targetVs.number).tests.log"
                    continue
                }
            }
            
            if (-not $skipbuild) {
                msbuild /v:n /m /fl /flp:"Verbosity=d;LogFile=BuildRelease.$config.$($targetVs.number).log" `
                    /t:$target `
                    /p:Configuration=$config `
                    /p:WixVersion=$version `
                    /p:WixReleaseVersion=$fileVersion `
                    /p:VSTarget=$($targetVs.number) `
                    /p:VisualStudioVersion=$($targetVs.number) `
                    /p:"CustomBuildIdentifier=$name" `
                    /p:IncludeVsLogger=$includeVsLogger `
                    /p:ReleaseBuild=$signedbuildText `
                    Python\Setup\dirs.proj

                if ($LASTEXITCODE -gt 0) {
                    Write-Error -EA:Continue "Build failed: $config"
                    $failed_logs += Get-Item "BuildRelease.$config.$($targetVs.number).log"
                    continue
                }
            }
            
            Copy-Item -force $bindir\en-us\*.msi $destdir\
            Copy-Item -force Python\Prerequisites\*.reg $destdir\
            
            mkdir $destdir\Symbols -EA 0 | Out-Null
            Copy-Item -force -recurse $bindir\*.pdb $destdir\Symbols\
            
            mkdir $destdir\Binaries -EA 0 | Out-Null
            Copy-Item -force -recurse $bindir\*.dll $destdir\Binaries\
            Copy-Item -force -recurse $bindir\*.exe $destdir\Binaries\
            Copy-Item -force -recurse $bindir\*.pkgdef $destdir\Binaries\
            Copy-Item -force -recurse $bindir\*.py $destdir\Binaries\
            Copy-Item -force -recurse $bindir\*.config $destdir\Binaries\
            
            mkdir $destdir\Binaries\ReplWindow -EA 0 | Out-Null
            Copy-Item -force -recurse Python\Product\ReplWindow\obj\Dev$($targetVs.number)\$config\extension.vsixmanifest $destdir\Binaries\ReplWindow
        }
        
        ######################################################################
        ##  BEGIN SIGNING CODE
        ######################################################################
        if ($signedBuild) {
            $jobs = @()
            
            foreach ($targetVs in $targetVersions) {
                $destdir = "$outdir\$($targetVs.name)\$config"

                $managed_files = @((
                    "Microsoft.PythonTools.Analysis.dll", 
                    "Microsoft.PythonTools.Analyzer.exe", 
                    "Microsoft.PythonTools.Attacher.exe", 
                    "Microsoft.PythonTools.AttacherX86.exe", 
                    "Microsoft.PythonTools.BuildTasks.dll", 
                    "Microsoft.PythonTools.Debugger.dll", 
                    "Microsoft.PythonTools.dll", 
                    "Microsoft.PythonTools.VSInterpreters.dll",
                    "Microsoft.PythonTools.TestAdapter.dll",
                    "Microsoft.PythonTools.Hpc.dll", 
                    "Microsoft.PythonTools.ImportWizard.dll", 
                    "Microsoft.PythonTools.IronPython.dll", 
                    "Microsoft.PythonTools.IronPython.Interpreter.dll", 
                    "Microsoft.PythonTools.MpiShim.exe", 
                    "Microsoft.PythonTools.Profiling.dll", 
                    "Microsoft.VisualStudio.ReplWindow.dll",
                    "Microsoft.PythonTools.WebRole.dll",
                    "Microsoft.PythonTools.Django.dll",
                    "Microsoft.PythonTools.VsLogger.dll",
                    "Microsoft.PythonTools.AzureSetup.exe",
                    "Microsoft.IronPythonTools.Resolver.dll"
                    ) | ForEach {@{path="$destdir\Binaries\$_"; name=$projectName}} `
                      | Where-Object {Test-Path $_.path})
                
                $native_files = @((
                    "PyDebugAttach.dll",
                    "PyDebugAttachX86.dll",
                    "Microsoft.PythonTools.Debugger.Helper.x86.dll",
                    "Microsoft.PythonTools.Debugger.Helper.x64.dll",
                    "VsPyProf.dll",
                    "VsPyProfX86.dll"
                    ) | ForEach {@{path="$destdir\Binaries\$_"; name=$projectName}} `
                      | Where-Object {Test-Path $_.path})

                Write-Output "Submitting signing jobs for $($targetVs.name)"

                $jobs += begin_sign_files $managed_files "$destdir\SignedBinaries" $approvers `
                    $projectName $projectUrl "$projectName $($targetVs.name) - managed code" $projectKeywords `
                    "authenticode;strongname" `
                    -delaysigned

                $jobs += begin_sign_files $native_files "$destdir\SignedBinaries" $approvers `
                    $projectName $projectUrl "$projectName $($targetVs.name) - native code" $projectKeywords `
                    "authenticode" 
            }
            
            end_sign_files $jobs
            
            foreach ($targetVs in $targetVersions) {
                $bindir = "Binaries\$config$($targetVs.number)"
                $destdir = "$outdir\$($targetVs.name)\$config"

                Copy-Item "$destdir\SignedBinaries\*" $bindir -Recurse -Force

                submit_symbols "PTVS$spacename" "$buildnumber $($targetvs.name)" "binaries" "$destdir\SignedBinaries" $symbol_contacts
                submit_symbols "PTVS$spacename" "$buildnumber $($targetvs.name)" "symbols" "$destdir\Symbols" $symbol_contacts

                foreach ($cmd in (Get-Content "BuildRelease.$config.$($targetVs.number).log") | Select-String "light.exe.+-out") {
                    $targetdir = [regex]::Match($cmd, 'Python\\Setup\\([^\\]+)').Groups[1].Value

                    Write-Output "Rebuilding MSI in $targetdir"

                    try {
                        Push-Location $buildroot\Python\Setup\$targetdir
                    } catch {
                        Write-Error "Unable to cd to $targetdir to execute line $cmd"
                        Write-Output "Enter directory name to cd to: "
                        $targetDir = [Console]::ReadLine()
                        Push-Location $targetdir
                    }

                    try {
                        Invoke-Expression $cmd | Out-Null
                    } finally {
                        Pop-Location
                    }
                }

                mkdir $destdir\UnsignedMsi -EA 0 | Out-Null
                mkdir $destdir\SignedBinariesUnsignedMsi -EA 0 | Out-Null
                
                Move-Item $destdir\*.msi $destdir\UnsignedMsi -Force
                Move-Item $bindir\en-us\*.msi $destdir\SignedBinariesUnsignedMsi -Force
            }
            
            $jobs = @()
            $done_nonvs_products = 0
            
            foreach ($targetVs in $targetVersions) {
                $destdir = "$outdir\$($targetVs.name)\$config"
                
                if (-not $done_nonvs_products) {
                    $_products = $products + $nonvs_products
                    $done_nonvs_products = 1
                } else {
                    $_products = $products
                }
                
                $msi_files = @($_products | 
                    ForEach {@{
                        path="$destdir\SignedBinariesUnsignedMsi\$($_.msi)";
                        name="Python Tools for Visual Studio$($_.signtag)"
                    }}
                )

                Write-Output "Submitting MSI signing job for $($targetVs.name)"

                $jobs += begin_sign_files $msi_files $destdir $approvers `
                    $projectName $projectUrl "$projectName $($targetVs.name) - installer" $projectKeywords `
                    "authenticode"
            }
            
            end_sign_files $jobs
        }
        ######################################################################
        ##  END SIGNING CODE
        ######################################################################
        
        $done_nonvs_products = 0
        foreach ($targetVs in $targetVersions) {
            $destdir = "$outdir\$($targetVs.name)\$config"
            if ($config -match "debug") {
                $config_mark = " Debug"
            } else {
                $config_mark = ""
            }
            
            if (-not $done_nonvs_products) {
                foreach ($product in $nonvs_products) {
                    Copy-Item "$destdir\$($product.msi)" "$outdir\$($product.outname1)$spacename$config_mark$($product.outname2)" -Force -EA:0
                    if (-not $?) {
                        Write-Output "Failed to copy $destdir\$($product.msi)"
                    }
                }
                $done_nonvs_products = 1
            }
            
            foreach ($product in $products) {
                Copy-Item "$destdir\$($product.msi)" "$outdir\$($product.outname1)$spacename $($targetvs.name)$config_mark$($product.outname2)" -Force -EA:0
                if (-not $?) {
                    Write-Output "Failed to copy $destdir\$($product.msi)"
                }
            }
        }
    }
    
    if ($scorch) {
        tfpt scorch /noprompt
    }
    
    if (-not $skipcopy) {
        Write-Output "Copying source files"
        robocopy /s . $outdir\Sources /xd TestResults Binaries Servicing obj | Out-Null
    }
    $successful = $true
} finally {
    if ($asmverfileBackedUp) {
        Move-Item "$asmverfile.bak" $asmverfile -Force
        if ($asmverfileIsReadOnly) {
            Set-ItemProperty $asmverfile -Name IsReadOnly -Value $true
        }
        Write-Output "Restored $asmverfile"
    } elseif (-not $asmverfileUseBackup) {
        tf undo /noprompt $asmverfile
    }
    
    if (-not (Get-Content $asmverfile) -match ' = "4100.00"') {
        Write-Error "Failed to undo $asmverfile"
    }
    
    Pop-Location
}

if ($successful) {
    Write-Output ""
    Write-Output "Build complete"
    Write-Output ""
    Write-Output "Installers were output to:"
    Write-Output "    $outdir"
    if ($failed_logs.Count -ne 0) {
        Write-Output ""
        Write-Warning "Some configurations failed to build."
        Write-Output "Review these log files for details:"
        foreach ($name in $failed_logs) {
            Write-Output "    $name"
        }
    }
}
