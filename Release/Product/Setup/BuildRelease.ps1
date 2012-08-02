param( [string] $outdir, [switch] $skiptests, [switch] $noclean, [switch] $uninstall, [string] $reinstall, [switch] $scorch, [string] $vsTarget)

try {
    get-command msbuild >out-null
} catch {
    Write-Error "Visual Studio x86 build tools are required."
    exit 1
}

if (-not $outdir)
{
    Write-Error "Must provide valid output directory: '$outdir'"
    exit 1
}

if (-not $noclean)
{
    if (Test-Path $outdir)
    {
        rmdir -Recurse -Force $outdir
        if (-not $?)
        {
            Write-Error "Could not clean output directory: $outdir"
            exit 1
        }
    }
    mkdir $outdir
    if (-not $?)
    {
        Write-Error "Could not make output directory: $outdir"
        exit 1
    }
}

# Add new products here:

$products = @{name="PythonTools"; wxi=".\PythonToolsInstaller\PythonToolsInstallerVars.wxi"; msi="PythonToolsInstaller.msi"}, `
            @{name="PyKinect";    wxi=".\PyKinectInstaller\PyKinectInstallerVars.wxi";       msi="PyKinectInstaller.msi"}, `
            @{name="Pyvot";       wxi=".\PyvotInstaller\PyvotInstallerVars.wxi";             msi="PyvotInstaller.msi"}

$buildroot = (Get-Location).Path
while ((Test-Path $buildroot) -and -not (Test-Path ([System.IO.Path]::Combine($buildroot, "build.root")))) {
    $buildroot = [System.IO.Path]::Combine($buildroot, "..")
}
$buildroot = [System.IO.Path]::GetFullPath($buildroot)
"Build Root: $buildroot"
Push-Location $buildroot

if ($uninstall)
{
    $guidregexp = "<\?define InstallerGuid=(.*)\?>"
    foreach ($product in $products)
    {
        foreach ($line in ( Get-Content ([System.IO.Path]::Combine($buildroot, "Release\Product\Setup", $product.wxi)) ))
        {
            if ($line -match $guidregexp) { $guid = $matches[1] ; break }
        }
        "Got product guid for $($product.name): $guid"
        start -wait msiexec "/uninstall","{$guid}","/passive"
    }
}

$versionFiles = "Build\AssemblyVersion.cs"
foreach($versionedFile in $versionFiles) {
    tf edit $versionedFile
    if ($LASTEXITCODE -gt 0) {
        # running outside of MS
        attrib -r $versionedFile
        copy -force $versionedFile ($versionedFile + ".bak")
    }
}


$prevOutDir = $outDir

$dev11InstallDir64 = Get-ItemProperty -path "HKLM:\Software\Wow6432Node\Microsoft\VisualStudio\11.0" -name InstallDir 2> out-null
$dev11InstallDir = Get-ItemProperty -path "HKLM:\Software\Microsoft\VisualStudio\11.0" -name InstallDir 2> out-null
$dev10InstallDir64 = Get-ItemProperty -path "HKLM:\Software\Wow6432Node\Microsoft\VisualStudio\10.0" -name InstallDir 2> out-null
$dev10InstallDir = Get-ItemProperty -path "HKLM:\Software\Microsoft\VisualStudio\10.0" -name InstallDir 2> out-null

$targetVersions = New-Object System.Collections.ArrayList($null)

if ($dev11InstallDir64 -or $dev11InstallDir) {
    if (-not $vsTarget -or $vsTarget -eq "11.0") {
        echo "Will build for Dev11"
        $targetVersions.Add("11.0")
    }
}

if ($dev10InstallDir64 -or $dev10InstallDir) {
    if (-not $vsTarget -or $vsTarget -eq "10.0") {
        echo "Will build for Dev10"
        $targetVersions.Add("10.0")
    }
}

foreach ($targetVs in $targetVersions) {
    $asmverfile = dir Build\AssemblyVersion.cs


    if ($targetVs -eq "10.0") {
        $version = "1.1." + ([DateTime]::Now.Year - 2011 + 4).ToString() + [DateTime]::Now.Month.ToString('00') + [DateTime]::Now.Day.ToString('00') + ".0"
        $outDir = $prevOutDir
    } else {
        $version = "1.8." + ([DateTime]::Now.Year - 2011 + 4).ToString() + [DateTime]::Now.Month.ToString('00') + [DateTime]::Now.Day.ToString('00') + ".0"
        $outDir = $prevOutDir + "\Dev" + $targetVs
    }

    tf edit $asmverfile
    (Get-Content $asmverfile) | %{ $_ -replace "0.7.4100.000", $version } | Set-Content $asmverfile

    Get-Content $asmverfile

    foreach ($config in ("Release","Debug"))
    {
        if (-not $skiptests)
        {
            msbuild Release\Tests\dirs.proj /m /p:Configuration=$config /p:WixVersion=$version /p:VSTarget=$targetVs /p:VisualStudioVersion=$targetVs
            if ($LASTEXITCODE -gt 0)
            {
                Write-Error "Test build failed: $config"
                exit 4
            }
        }

        msbuild Release\Product\Setup\dirs.proj /m /p:Configuration=$config /p:WixVersion=$version /p:VSTarget=$targetVs /p:VisualStudioVersion=$targetVs
        if ($LASTEXITCODE -gt 0) {
            Write-Error "Build failed: $config"
            exit 3
        }
    }

    tf undo /noprompt $asmverfile

    mkdir $outdir\Debug
    copy -force Binaries\Debug\*.msi $outdir\Debug\
    copy -force Prerequisites\*.reg $outdir\Debug\

    mkdir $outdir\Debug\Symbols
    copy -force -recurse Binaries\Debug\*.pdb $outdir\Debug\Symbols\

    mkdir $outdir\Debug\Binaries
    copy -force -recurse Binaries\Debug\*.dll $outdir\Debug\Binaries\
    copy -force -recurse Binaries\Debug\*.exe $outdir\Debug\Binaries\
    copy -force -recurse Binaries\Debug\*.pkgdef $outdir\Debug\Binaries\

    mkdir $outdir\Debug\Binaries\ReplWindow
    copy -force -recurse Release\Product\Python\ReplWindow\obj\Debug\extension.vsixmanifest $outdir\Debug\Binaries\ReplWindow

    mkdir $outdir\Release
    copy -force Binaries\Release\*.msi $outdir\Release\
    copy -force Prerequisites\*.reg $outdir\Release\

    mkdir $outdir\Release\Symbols
    copy -force -recurse Binaries\Release\*.pdb $outdir\Release\Symbols\

    mkdir $outdir\Release\Binaries
    copy -force -recurse Binaries\Release\*.dll $outdir\Release\Binaries\
    copy -force -recurse Binaries\Release\*.exe $outdir\Release\Binaries\
    copy -force -recurse Binaries\Release\*.pkgdef $outdir\Release\Binaries\

    mkdir $outdir\Release\Binaries\ReplWindow
    copy -force -recurse Release\Product\Python\ReplWindow\obj\Release\extension.vsixmanifest $outdir\Release\Binaries\ReplWindow
}

if ($scorch) { tfpt scorch /noprompt }

$outdir = $prevOutDir
if (-not (Test-path $outdir\Sources\Incubation)) { mkdir $outdir\Sources\Incubation }
robocopy /s . $outdir\Sources /xd TestResults

foreach($versionedFile in $versionFiles) {
    tf undo /noprompt $versionedFile
    if ($LASTEXITCODE -gt 0) {
        copy -force ($versionedFile + ".bak") $versionedFile
        attrib +r $versionedFile
        del ($versionedFile + ".bak")
    }
}

Pop-Location

if ($reinstall -eq "Debug" -or $reinstall -eq "Release")
{
    foreach ($product in $products)
    {
        "Installing $($product.name) from $outdir\$reinstall\$($product.msi)"
        start -wait msiexec "/package","$outdir\$reinstall\$($product.msi)","/passive"
    }
}
