@setlocal
@echo off
rem
rem The test targets files redirect projects to use the targets deployed
rem to the Experimental hive. By installing these targets, you can test
rem modifications easily without having to manually redeploy files to
rem your MSBuild directory.
rem
rem This script should be run as an administrator.
rem

echo This script should be run as an administrator.

set D=%~dp0

if NOT EXIST "%VSINSTALLDIR%" call :failure

rem Dynamically attempt copy for all installed Visual Studio MSBuild version folders (v*.0)
for /d %%D in ("%VSINSTALLDIR%\MSBuild\Microsoft\VisualStudio\v*.0") do call :docopy "%%~fD"

pause
exit /B 0

:docopy
set "_MSBUILD_VS_PATH=%~1"
if not exist "%_MSBUILD_VS_PATH%" (
 echo Skipping missing path: %_MSBUILD_VS_PATH%
 exit /B 0
)

rem Determine Visual Studio version folder name (v17.0, v18.0, etc.)
for %%F in ("%_MSBUILD_VS_PATH%") do set "_VS_VER_FOLDER=%%~nF"
if not exist "%~1" exit /B 1
rem Map to VC toolset folder dynamically (v17.0 -> v170, v18.0 -> v180, v19.0 -> v190, etc.)
set "_VC_TOOLSET_FOLDER="
for /f "tokens=1 delims=." %%A in ("%_VS_VER_FOLDER:~1%") do set "_VS_MAJOR=%%A"
if defined _VS_MAJOR set "_VC_TOOLSET_FOLDER=v%_VS_MAJOR%0"

echo.
echo Processing %_VS_VER_FOLDER% (VC toolset=%_VC_TOOLSET_FOLDER%)
rem Copy Python targets
set "TARGET=%_MSBUILD_VS_PATH%\Python Tools\"
pushd "%D%..\Product\BuildTasks\TestTargets"
echo.
echo Copying:
echo from %CD%
echo to %TARGET%
if not exist "%TARGET%" mkdir "%TARGET%" >nul 2>&1
copy /Y "*.targets" "%TARGET%" >nul
popd

rem Copy VC Debug Launcher targets if mapping succeeded
if not "%_VC_TOOLSET_FOLDER%"=="" (
 set "TARGET=%VSINSTALLDIR%\MSBuild\Microsoft\VC\%_VC_TOOLSET_FOLDER%\Platforms"
 pushd "%D%..\Product\VCDebugLauncher\VCTargets"
echo.
echo Copying:
echo from %CD%
echo to %TARGET%
 if not exist "%TARGET%" mkdir "%TARGET%" >nul 2>&1
 robocopy /E /XO Win32 "%TARGET%\Win32" *.* >nul
 robocopy /E /XO x64 "%TARGET%\X64" *.* >nul
 popd
 rem Verify VC platform copy
 if exist "%TARGET%\Win32" (
 echo [OK] Win32 platform directory deployed.
 ) else (
 echo [WARN] Win32 platform directory missing at %TARGET%\Win32
 )
 if exist "%TARGET%\X64" (
 echo [OK] X64 platform directory deployed.
 ) else (
 echo [WARN] X64 platform directory missing at %TARGET%\X64
 )
 dir /b "%TARGET%\Win32" > "%TEMP%\_vc_win32.lst"2>nul
 for /f %%C in ('find /c /v "" ^< "%TEMP%\_vc_win32.lst"') do set "_VC_W32_COUNT=%%C"
 dir /b "%TARGET%\X64" > "%TEMP%\_vc_x64.lst"2>nul
 for /f %%C in ('find /c /v "" ^< "%TEMP%\_vc_x64.lst"') do set "_VC_X64_COUNT=%%C"
 echo VC Debug Launcher files: Win32=%_VC_W32_COUNT% X64=%_VC_X64_COUNT%
 if /I "%VERBOSE%"=="1" (
 echo --- Win32 file list ---
 type "%TEMP%\_vc_win32.lst"
 echo --- X64 file list ---
 type "%TEMP%\_vc_x64.lst"
 )
) else (
 echo Skipping VC Debug Launcher copy (unrecognized VS version folder: %_VS_VER_FOLDER%)
)
echo Summary for %_VS_VER_FOLDER%: PythonTargets=%_PT_COUNT% Win32Files=%_VC_W32_COUNT% X64Files=%_VC_X64_COUNT%
exit /B 0

:failure
echo.
echo VSINSTALLDIR not found. You may need to run this from a Dev command prompt
echo.
exit /B 1
