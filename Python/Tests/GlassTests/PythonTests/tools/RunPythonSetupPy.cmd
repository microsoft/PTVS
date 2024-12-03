@echo off
setlocal

echo ======== RunPythonSetupPy.cmd ========
echo.

if "%~1"=="-?" goto Help
if "%~1"=="/?" goto Help
if "%~1"=="" goto IncorrectUsageError
if "%~2"=="" goto IncorrectUsageError

:ConfigurationDone
set PythonExe=%~1
set OutputDir=%~2

echo Executable is %PythonExe%
echo Output dir is %OutputDir%

pushd "%OutputDir%"
call "%PythonExe%" -m pip install setuptools
call "%PythonExe%" setup.py install --user
popd
if NOT "%ERRORLEVEL%"=="0" goto Error

goto Success

::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:IncorrectUsageError
echo Error : Invalid arguments passed. Try RunPythonSetupPy /?
set ERRORLEVEL=1&goto Error

:Help
echo Usage: RunPythonSetupPy ^<Python exe path^> ^<Out dir^>
echo Run setup.py in the output directory.
echo.
echo Example: RunPythonSetupPy "C:\python27\python.exe" "C:\GlassTests\Script\ScriptEE\bin\x86\"
goto Done

:Success
echo ======== RunPythonSetupPy.cmd completed successfully. ========&goto Done

:Error
echo ======== RunPythonSetupPy.cmd failed. ========&exit /b %ERRORLEVEL%

:Done
