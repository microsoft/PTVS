@echo off
setlocal

echo ======== CompileGlassTestExtension.cmd ========
echo.

if "%~1"=="-?" goto Help
if "%~1"=="/?" goto Help
if "%~1"=="" goto IncorrectUsageError
if "%~2"=="" goto IncorrectUsageError

set SourceFile=%~1
set SourceFileName=%~n1
set GlassDir=%~2
set OutDir=%GlassDir%temp\
set OutFile=%OutDir%%SourceFileName%.dll
set DotNetFrameworkDir=%windir%\Microsoft.NET\Framework\v4.0.30319

if NOT EXIST "%OutFile%" goto RebuildNeeded

REM Check if the Source file is newer than the Output file.
call :CompareFileTimes "%SourceFile%" "%OutFile%"
if NOT "%ERRORLEVEL%"=="0" call :ResetErrorLevel&goto RebuildNeeded

echo Skipping build of %OutFile% because it is already up-to-date.&goto Success

:RebuildNeeded
if NOT exist "%OutDir%" mkdir "%OutDir%"
if NOT "%ERRORLEVEL%"=="0" goto OutDirCreationFailedError

echo Invoking: csc /out:"%OutFile%" /target:library /debug+ /o- /unsafe+ /d:DEBUG /r:"%GlassDir%\glass2.dll" /r:"%GlassDir%\Microsoft.VisualStudio.Debugger.Engine.dll" /r:"%GlassDir%\Microsoft.VisualStudio.Debugger.InteropA.dll" /r:"%GlassDir%\Microsoft.VisualStudio.Debugger.Interop.11.0.dll" /r:"%GlassDir%\Microsoft.VisualStudio.Debugger.Interop.14.0.dll" /r:"%GlassDir%\Microsoft.PythonTools.Debugger.Concord.dll" "%SourceFile%"
call "%DotNetFrameworkDir%\csc.exe" /out:"%OutFile%" /target:library /debug+ /o- /unsafe+ /d:DEBUG /r:"%GlassDir%\glass2.dll" /r:"%GlassDir%\Microsoft.VisualStudio.Debugger.Engine.dll" /r:"%GlassDir%\Microsoft.VisualStudio.Debugger.InteropA.dll" /r:System.dll /r:"%GlassDir%\Microsoft.VisualStudio.Debugger.Interop.11.0.dll" /r:"%GlassDir%\Microsoft.VisualStudio.Debugger.Interop.14.0.dll" /r:"%GlassDir%\Microsoft.PythonTools.Debugger.Concord.dll" "%SourceFile%"

if NOT "%ERRORLEVEL%"=="0" goto CompileFailed
echo '%OutFile%' built successfully.

goto Success

::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
REM Procedure to check if the time of %1 is > %2. Returns 1 if true, 0 otherwise. 
:CompareFileTimes
call powershell -NoProfile -ExecutionPolicy ByPass -File "%~dp0CompareFileTimes.ps1" "%~1" "%~2"
exit /b %ERRORLEVEL%

:ResetErrorLevel
exit /b 0

:IncorrectUsageError
echo Error : Invalid arguments passed. Try CompileGlassTestExtension /?
set ERRORLEVEL=1&goto Error

:OutDirCreationFailedError
echo Error : Failed to create output directory for the Glass extension '%OutDir%'.
set ERRORLEVEL=2&goto Error

:CompileFailed
echo Error : csc failed with exit code "%ERRORLEVEL%".
goto Error

:Help
echo Usage: CompileGlassTestExtension ^<Source file^> ^<Glass directory^>
echo Compiles a Glass test extension.
echo.
echo Example: CompileGlassTestExtension "C:\concord\SDK\TestsV2\Native\NatChildProcess\NatChildProcess.GlassExtension.cs" "C:\binaries\glass\"
goto Done

:Success
echo ======== CompileGlassTestExtension.cmd completed successfully. ========&goto Done

:Error
echo ======== CompileGlassTestExtension.cmd failed. ========&exit /b %ERRORLEVEL%

:Done