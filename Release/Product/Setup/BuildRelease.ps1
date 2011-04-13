if ($args.Length -eq 0) {
	echo "Must provide out dir"
	exit 1
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

$version = "0.8." + ([DateTime]::Now.Year - 2011 + 4).ToString() + [DateTime]::Now.Month.ToString('00') + [DateTime]::Now.Day.ToString('00') + ".0"
$text = [System.IO.File]::ReadAllText((Resolve-Path ..\..\..\Build\AssemblyVersion.cs))
$text = $text.Replace("0.7.4100.000", $version)

[System.IO.File]::WriteAllText((Resolve-Path ..\..\..\Build\AssemblyVersion.cs), $text)

msbuild .\dirs.proj /m /p:Configuration=Release /p:WixVersion=$version
if ($LASTEXITCODE -gt 0) {
	echo Build failed
	exit 3
}

msbuild .\dirs.proj /p:Configuration=Debug /p:WixVersion=$version
if ($LASTEXITCODE -gt 0) {
	echo "Build failed"
	exit 4
}


mkdir $args\Debug
copy -force ..\..\..\Binaries\Win32\Debug\PythonToolsInstaller.msi $args\Debug\PythonToolsInstaller.msi
copy -force PythonToolsInstaller\SnInternal.reg $args\Debug\EnableSkipVerification.reg
copy -force PythonToolsInstaller\SnInternal64.reg $args\Debug\EnableSkipVerificationX64.reg
copy -force PythonToolsInstaller\SnInternalRemove.reg $args\Debug\DisableSkipVerification.reg
copy -force PythonToolsInstaller\SnInternal64Remove.reg $args\Debug\DisableSkipVerificationX64.reg

mkdir $args\Debug\Symbols
copy -force -recurse ..\..\..\Binaries\Win32\Debug\*.pdb $args\Debug\Symbols\

mkdir $args\Debug\Symbols\x64
copy -force -recurse ..\..\..\Binaries\x64\Debug\*.pdb $args\Debug\Symbols\x64

mkdir $args\Debug\Binaries
copy -force -recurse ..\..\..\Binaries\Win32\Debug\*.dll $args\Debug\Binaries\
copy -force -recurse ..\..\..\Binaries\Win32\Debug\*.exe $args\Debug\Binaries\

mkdir $args\Debug\Binaries\x64
mkdir $args\Debug\Binaries\ReplWindow
copy -force -recurse ..\..\..\Binaries\x64\Debug\*.dll $args\Debug\Binaries\x64
copy -force -recurse ..\..\..\Binaries\x64\Debug\*.exe $args\Debug\Binaries\x64
copy -force -recurse ..\..\..\Binaries\x64\Debug\*.pkgdef $args\Debug\Binaries\x64
copy -force -recurse ..\Python\ReplWindow\obj\Win32\Debug\extension.vsixmanifest $args\Debug\Binaries\ReplWindow

mkdir $args\Release
copy -force ..\..\..\Binaries\Win32\Release\PythonToolsInstaller.msi $args\Release\PythonToolsInstaller.msi
copy -force PythonToolsInstaller\SnInternal.reg $args\Release\EnableSkipVerification.reg
copy -force PythonToolsInstaller\SnInternal64.reg $args\Release\EnableSkipVerificationX64.reg
copy -force PythonToolsInstaller\SnInternalRemove.reg $args\Release\DisableSkipVerification.reg
copy -force PythonToolsInstaller\SnInternal64Remove.reg $args\Release\DisableSkipVerificationX64.reg

mkdir $args\Release\Symbols
copy -force -recurse ..\..\..\Binaries\Win32\Release\*.pdb $args\Release\Symbols\

mkdir $args\Release\Symbols\x64
copy -force -recurse ..\..\..\Binaries\x64\Release\*.pdb $args\Release\Symbols\x64

mkdir $args\Release\Binaries
mkdir $args\Release\Binaries\ReplWindow
copy -force -recurse ..\..\..\Binaries\Win32\Release\*.dll $args\Release\Binaries\
copy -force -recurse ..\..\..\Binaries\Win32\Release\*.exe $args\Release\Binaries\
copy -force -recurse ..\..\..\Binaries\Win32\Release\*.pkgdef $args\Release\Binaries\
copy -force -recurse ..\Python\ReplWindow\obj\Win32\Release\extension.vsixmanifest $args\Release\Binaries\ReplWindow

mkdir $args\Release\Binaries\x64
copy -force -recurse ..\..\..\Binaries\x64\Release\*.dll $args\Release\Binaries\x64
copy -force -recurse ..\..\..\Binaries\x64\Release\*.exe $args\Release\Binaries\x64


#tfpt scorch /noprompt

mkdir $args\Sources\Incubation
xcopy /s ..\..\..\* $args\Sources

foreach($versionedFile in $versionFiles) {
    tf undo /noprompt $versionedFile
    if ($LASTEXITCODE -gt 0) {
        copy -force ($versionedFile + ".bak") $versionedFile
        attrib +r $versionedFile
        del ($versionedFile + ".bak")
    }
}