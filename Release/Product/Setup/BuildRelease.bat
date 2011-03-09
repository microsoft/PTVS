set MySku=Public
@echo off
if "%1" == "" (
	echo Must provide out dir
	exit 1
)

msbuild .\dirs.proj /m /p:Configuration=Release
if errorlevel 1 (
	echo Build failed
	exit 2
)

msbuild .\dirs.proj /p:Configuration=Debug

if errorlevel 1 (
	echo Build failed
	exit 3
)


mkdir %1\Debug
copy ..\..\..\Binaries\Win32\Debug\PythonToolsInstaller.msi %1\Debug\PythonToolsInstaller.msi

mkdir %1\Debug\Symbols
copy ..\..\..\Binaries\Win32\Debug\*.pdb %1\Debug\Symbols\

mkdir %1\Debug\Symbols\x64
copy ..\..\..\Binaries\x64\Debug\*.pdb %1\Debug\Symbols\x64

mkdir %1\Debug\Binaries
copy ..\..\..\Binaries\Win32\Debug\*.dll %1\Debug\Binaries\
copy ..\..\..\Binaries\Win32\Debug\*.exe %1\Debug\Binaries\

mkdir %1\Debug\Binaries\x64
mkdir %1\Debug\Binaries\ReplWindow
copy ..\..\..\Binaries\x64\Debug\*.dll %1\Debug\Binaries\x64
copy ..\..\..\Binaries\x64\Debug\*.exe %1\Debug\Binaries\x64
copy ..\..\..\Binaries\x64\Debug\*.pkgdef %1\Debug\Binaries\x64
copy ..\Python\ReplWindow\obj\Win32\Debug\extension.vsixmanifest %1\Debug\Binaries\ReplWindow

mkdir %1\Release
copy ..\..\..\Binaries\Win32\Release\PythonToolsInstaller.msi %1\Release\PythonToolsInstaller.msi

mkdir %1\Release\Symbols
copy ..\..\..\Binaries\Win32\Release\*.pdb %1\Release\Symbols\

mkdir %1\Release\Symbols\x64
copy ..\..\..\Binaries\x64\Release\*.pdb %1\Release\Symbols\x64

mkdir %1\Release\Binaries
mkdir %1\Release\Binaries\ReplWindow
copy ..\..\..\Binaries\Win32\Release\*.dll %1\Release\Binaries\
copy ..\..\..\Binaries\Win32\Release\*.exe %1\Release\Binaries\
copy ..\..\..\Binaries\Win32\Release\*.pkgdef %1\Release\Binaries\
copy ..\Python\ReplWindow\obj\Win32\Release\extension.vsixmanifest %1\Release\Binaries\ReplWindow

mkdir %1\Release\Binaries\x64
copy ..\..\..\Binaries\x64\Release\*.dll %1\Release\Binaries\x64
copy ..\..\..\Binaries\x64\Release\*.exe %1\Release\Binaries\x64


:tfpt scorch /noprompt

mkdir %1\Sources\Incubation
xcopy /s ..\..\..\* %1\Sources

:end