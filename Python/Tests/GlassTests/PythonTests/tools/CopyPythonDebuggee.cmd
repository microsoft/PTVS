@echo off
setlocal

echo ======== CopyScriptDebuggee.cmd ========
echo.

if "%~1"=="-?" goto Help
if "%~1"=="/?" goto Help
if "%~1"=="" goto IncorrectUsageError
if "%~2"=="" goto IncorrectUsageError
if "%~3"=="" goto IncorrectUsageError
if "%~4"=="" goto IncorrectUsageError

set Platform=
if /I "%~1"=="X86" set Platform=X86&goto PlatformConfigurationDone
if /I "%~1"=="X64" set Platform=X64&goto PlatformConfigurationDone
goto UnknownTestConfigurationError

:PlatformConfigurationDone
set SourceFile=%~2
set OutDir=%~3
set GlassDir=%~4
set PythonTestRootDir=%~5
set RdbgDir=%GlassDir%\Remote Debugger\%Platform%

if not exist "%OutDir%" mkdir "%OutDir%"&if NOT "%ERRORLEVEL%"=="0" goto Error
xcopy /dy "%SourceFile%" "%OutDir%"

REM call :CopyFile vsassert.dll
REM if NOT "%ERRORLEVEL%"=="0" goto Error
REM call :CopyFile ucrtbased.dll
REM if NOT "%ERRORLEVEL%"=="0" goto Error
REM call :CopyFile vcruntime140d.dll
REM if NOT "%ERRORLEVEL%"=="0" goto Error
REM call :CopyFile msvcp140d.dll
REM if NOT "%ERRORLEVEL%"=="0" goto Error

call :CopyCommonTestFile initmod.cpp
if NOT "%ERRORLEVEL%"=="0" goto Error

call :CopyCommonTestFile setup_cpp_mod.py
if NOT "%ERRORLEVEL%"=="0" goto Error

goto Success

::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:CopyCommonTestFile

set FileName=%~1
if not exist "%OutDir%\%FileName%" echo Copying "%PythonTestRootDir%%FileName%" to "%OutDir%"...&xcopy /dy "%PythonTestRootDir%%FileName%" "%OutDir%">NUL
exit /b %ERRORLEVEL%

::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:CopyFile

set FileName=%~1
if not exist "%OutDir%\%FileName%" echo Copying "%RdbgDir%\%FileName%" to "%OutDir%"...&xcopy /dy "%RdbgDir%\%FileName%" "%OutDir%">NUL
exit /b %ERRORLEVEL%

::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:IncorrectUsageError
echo Error : Invalid arguments passed. Try CopyScriptDebuggee /?
set ERRORLEVEL=1&goto Error

:UnknownTestConfigurationError
echo Error : Unknown test configuration '%~1' passed. Only X86, X64 are supported.
set ERRORLEVEL=2&goto Error

:Help
echo Usage: CopyScriptDebuggee X86/X64 ^<Source file^> ^<Out dir^> ^<Glass dir^>
echo Copies the script debuggee to the output directory, and copies vsassert.dll and the CRT from the Glass directory.
echo.
echo Example: CopyScriptDebuggee X86 "C:\GlassTests\Script\ScriptEE\ScriptEE.debuggee.js" "C:\GlassTests\Script\ScriptEE\bin\x86\" "C:\dd\vs\out\binaries\x86chk\suitebin\concordsdk\tools\glass\"
goto Done

:Success
echo ======== CopyScriptDebuggee.cmd completed successfully. ========&goto Done

:Error
echo ======== CopyScriptDebuggee.cmd failed. ========&exit /b %ERRORLEVEL%

:Done
