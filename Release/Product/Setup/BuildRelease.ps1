param( [string] $outdir, [switch] $skiptests, [switch] $noclean, [switch] $uninstall, [string] $reinstall, [switch] $scorch)

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

if ($uninstall)
{
    $guidregexp = "<\?define InstallerGuid=(.*)\?>"
    foreach ($line in ( Get-Content .\PythonToolsInstaller\PythonToolsInstallerVars.wxi ))
    {
        if ($line -match $guidregexp) { $guid = $matches[1] ; break }
    }
    "Got product guid: $guid"
    start -wait msiexec "/uninstall","{$guid}","/passive"
}

$versionFiles = "..\..\..\Build\AssemblyVersion.cs", "..\Python\PythonTools\source.extension.vsixmanifest"
foreach($versionedFile in $versionFiles) {
    tf edit $versionedFile
    if ($LASTEXITCODE -gt 0) {
        # running outside of MS
        attrib -r $versionedFile
        copy -force $versionedFile ($versionedFile + ".bak")
    }
}


$prevOutDir = $outDir
foreach ($targetVs in ("11.0", "10.0")) {
    $asmverfile = dir ..\..\..\Build\AssemblyVersion.cs
    

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
            msbuild ..\..\Tests\dirs.proj /m /p:Configuration=$config /p:WixVersion=$version /p:VSTarget=$targetVs
            if ($LASTEXITCODE -gt 0)
            {
                Write-Error "Test build failed: $config"
                exit 4
            }
        }
    
        msbuild .\dirs.proj /m /p:Configuration=$config /p:WixVersion=$version /p:VSTarget=$targetVs
        if ($LASTEXITCODE -gt 0) {
            Write-Error "Build failed: $config"
            exit 3
        }
    }
    
    tf undo /noprompt $asmverfile
    
    mkdir $outdir\Debug
    copy -force ..\..\..\Binaries\Win32\Debug\PythonToolsInstaller.msi $outdir\Debug\PythonToolsInstaller.msi
    copy -force PythonToolsInstaller\SnInternal.reg $outdir\Debug\EnableSkipVerification.reg
    copy -force PythonToolsInstaller\SnInternal64.reg $outdir\Debug\EnableSkipVerificationX64.reg
    copy -force PythonToolsInstaller\SnInternalRemove.reg $outdir\Debug\DisableSkipVerification.reg
    copy -force PythonToolsInstaller\SnInternal64Remove.reg $outdir\Debug\DisableSkipVerificationX64.reg
    
    mkdir $outdir\Debug\Symbols
    copy -force -recurse ..\..\..\Binaries\Win32\Debug\*.pdb $outdir\Debug\Symbols\
    
    mkdir $outdir\Debug\Symbols\x64
    copy -force -recurse ..\..\..\Binaries\x64\Debug\*.pdb $outdir\Debug\Symbols\x64
    
    mkdir $outdir\Debug\Binaries
    copy -force -recurse ..\..\..\Binaries\Win32\Debug\*.dll $outdir\Debug\Binaries\
    copy -force -recurse ..\..\..\Binaries\Win32\Debug\*.exe $outdir\Debug\Binaries\
    
    mkdir $outdir\Debug\Binaries\x64
    mkdir $outdir\Debug\Binaries\ReplWindow
    copy -force -recurse ..\..\..\Binaries\x64\Debug\*.dll $outdir\Debug\Binaries\x64
    copy -force -recurse ..\..\..\Binaries\x64\Debug\*.exe $outdir\Debug\Binaries\x64
    copy -force -recurse ..\..\..\Binaries\x64\Debug\*.pkgdef $outdir\Debug\Binaries\x64
    copy -force -recurse ..\Python\ReplWindow\obj\Win32\Debug\extension.vsixmanifest $outdir\Debug\Binaries\ReplWindow
    
    mkdir $outdir\Release
    copy -force ..\..\..\Binaries\Win32\Release\PythonToolsInstaller.msi $outdir\Release\PythonToolsInstaller.msi
    copy -force PythonToolsInstaller\SnInternal.reg $outdir\Release\EnableSkipVerification.reg
    copy -force PythonToolsInstaller\SnInternal64.reg $outdir\Release\EnableSkipVerificationX64.reg
    copy -force PythonToolsInstaller\SnInternalRemove.reg $outdir\Release\DisableSkipVerification.reg
    copy -force PythonToolsInstaller\SnInternal64Remove.reg $outdir\Release\DisableSkipVerificationX64.reg
    
    mkdir $outdir\Release\Symbols
    copy -force -recurse ..\..\..\Binaries\Win32\Release\*.pdb $outdir\Release\Symbols\
    
    mkdir $outdir\Release\Symbols\x64
    copy -force -recurse ..\..\..\Binaries\x64\Release\*.pdb $outdir\Release\Symbols\x64
    
    mkdir $outdir\Release\Binaries
    mkdir $outdir\Release\Binaries\ReplWindow
    copy -force -recurse ..\..\..\Binaries\Win32\Release\*.dll $outdir\Release\Binaries\
    copy -force -recurse ..\..\..\Binaries\Win32\Release\*.exe $outdir\Release\Binaries\
    copy -force -recurse ..\..\..\Binaries\Win32\Release\*.pkgdef $outdir\Release\Binaries\
    copy -force -recurse ..\Python\ReplWindow\obj\Win32\Release\extension.vsixmanifest $outdir\Release\Binaries\ReplWindow
    
    mkdir $outdir\Release\Binaries\x64
    copy -force -recurse ..\..\..\Binaries\x64\Release\*.dll $outdir\Release\Binaries\x64
    copy -force -recurse ..\..\..\Binaries\x64\Release\*.exe $outdir\Release\Binaries\x64
}
    
if ($scorch) { tfpt scorch /noprompt }

$outdir = $prevOutDir
if (-not (Test-path $outdir\Sources\Incubation)) { mkdir $outdir\Sources\Incubation }
robocopy /s ..\..\.. $outdir\Sources /xd TestResults
    
foreach($versionedFile in $versionFiles) {
    tf undo /noprompt $versionedFile
    if ($LASTEXITCODE -gt 0) {
        copy -force ($versionedFile + ".bak") $versionedFile
        attrib +r $versionedFile
        del ($versionedFile + ".bak")
    }
}

if ($reinstall -eq "Debug" -or $reinstall -eq "Release")
{
    start -wait msiexec "/package","$outdir\$reinstall\PythonToolsInstaller.msi","/passive"
}
