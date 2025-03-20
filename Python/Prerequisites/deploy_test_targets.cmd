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

call :docopy "%VSINSTALLDIR%\MSBuild\Microsoft\VisualStudio\v17.0"
call :docopy "%VSINSTALLDIR%\MSBuild\Microsoft\VisualStudio\v18.0"

pause
exit /B 0

:docopy

if not exist "%~1" exit /B 1

set TARGET=%~1\Python Tools\

pushd "%D%..\Product\BuildTasks\TestTargets"
echo.
echo Copying:
echo     from %CD%
echo     to %TARGET%
if not exist "%TARGET%" mkdir "%TARGET%"
copy /Y "*.targets" "%TARGET%"
popd

set TARGET=%VSINSTALLDIR%\MSBuild\Microsoft\VC\v170\Platforms
pushd "%D%..\Product\VCDebugLauncher\VCTargets"
echo.
echo Copying:
echo     from %CD%
echo     to %TARGET%
if not exist "%TARGET%" mkdir "%TARGET%"
robocopy /E /XO Win32 "%TARGET%\Win32" *.*
robocopy /E /XO x64 "%TARGET%\X64" *.*
popd

exit /B 0

:failure
echo.
echo VSINSTALLDIR not found. You may need to run this from a Dev command prompt
echo.
exit /B 1
