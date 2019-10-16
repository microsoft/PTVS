@echo off
setlocal

echo ======== RegisterPythonEngine.cmd ========
echo.

if "%~1"=="-?" goto Help
if "%~1"=="/?" goto Help
if NOT "%~1"=="" goto Help

REM Call this from the 32-bit command prompt.
if /i NOT "%PROCESSOR_ARCHITECTURE%"=="x86" echo Invoking RegisterPythonEngine.cmd from 32-bit prompt...&call %SystemRoot%\SysWow64\cmd.exe /C "%~dpf0" %* &goto Done
if /i NOT "%PROCESSOR_ARCHITECTURE%"=="x86" goto UnsupportedProcessorError

REM Determine the registry root first, as it is used in the help output
if not exist "%~dp0glass2.exe.regroot" goto RegRootNotFoundError
set RegistryRoot=
for /f "delims=" %%l in (%~sdp0glass2.exe.regroot) do call :ProcessRegRootLine %%l
if "%RegistryRoot%"=="" goto IncorrectRegRootFormatError
if "%RegistryRoot%"=="ERROR" goto IncorrectRegRootFormatError

echo Importing PythonEngine.regdef to registry...
if exist %tmp%\PythonEngine.reg del %tmp%\PythonEngine.reg>NUL
call C:\GlassTesting\GlassStandAlone\Glass\GlassRegGen.exe %~dp0PythonEngine.regdef %RegistryRoot% %tmp%\PythonEngine.reg
if NOT "%ERRORLEVEL%"=="0" goto GlassRegGenError

REM Clear natvis diagnostics unless specifically enabled via a glass setup script specific to the test
REM This needs to be handled separately from PythonEngine.regdef because the setting is in HKCU not HKLM.
reg add HKEY_CURRENT_USER\%RegistryRoot%\Debugger\NatvisDiagnostics /v Level /t REG_SZ /d Off /f>NUL

call reg.exe import %tmp%\PythonEngine.reg 2>NUL
if NOT "%ERRORLEVEL%"=="0" goto RegImportFailedError
echo Done.

echo Python engine was registered successfully.
goto Success

::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:ProcessRegRootLine
set line=%1
if "%line%"=="" goto Done
if "%line:~0,1%"=="#" goto Done
if NOT "%RegistryRoot%"=="" set RegistryRoot=ERROR& goto Done
set RegistryRoot=%line%
goto Done

:UnsupportedProcessorError
echo Error : Unsupported processor. RegisterPythonEngine.cmd should only be run from an X86 or X64 OS.
set ERRORLEVEL=1&goto Error

:RegRootNotFoundError
echo Error : %~dp0glass2.exe.regroot was not found.
set ERRORLEVEL=2&goto Error

:IncorrectRegRootFormatError
echo Error : Incorrect format in %~dp0glass2.exe.regroot.
set ERRORLEVEL=3&goto Error

:GlassRegGenError
echo Error : GlassRegGen.exe failed.
set ERRORLEVEL=4&goto Error

:RegImportFailedError
echo Error : Importing PythonEngine.regdef to registry failed.
set ERRORLEVEL=5&goto Error

:EnableMsvsmonDevModeFailedError
echo Error : EnableMsvsmonDevMode failed.
set ERRORLEVEL=6&goto Error

:Help
echo RegisterPythonEngine.cmd
echo.
echo This script is used to register glass2.exe for use with the Concord debug engine.
echo Registry keys are written under the registry root in glass2.exe.regroot (eg. HKLM\Software\Microsoft\Glass\14.0). Delete this key to uninstall.
echo.
echo This script must be run as an administrator.
echo.
goto Done

:Success
echo ======== RegisterPythonEngine.cmd completed successfully. ========&goto Done

:Error
echo ======== RegisterPythonEngine.cmd failed. ========&exit /b %ERRORLEVEL%

:Done