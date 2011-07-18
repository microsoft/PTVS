@echo off
setlocal

REM Disable verification for assemblies signed with MS public keys
REM If the processor architecture is amd64 the x64 version of sn.exe is called in addition to the x86 version

REM Batch file installed with Visual Studio which sets environment variables for Visual Studio tools 
REM including the path for sn.exe
call "%VS100COMNTOOLS%"\vsvars32.bat

call:RUN_SN "%WindowsSDKDir%\bin"

REM If process is running as 64 bit or as WOW64 (32 bit on 64 bit machine) run the x64 version of sn.exe
if "%PROCESSOR_ARCHITECTURE%" == "AMD64" call:RUN_SN "%WindowsSDKDir%\bin\x64"
if "%PROCESSOR_ARCHITEW6432%" == "AMD64" call:RUN_SN "%WindowsSDKDir%\bin\x64"

REM The Windows Installer caches some of the verification data.
REM Stop it to ensure it picks up the changes.
echo.
echo Stopping the installer service. It is okay if it was not started.
net stop MSIServer

GOTO:EOF

:RUN_SN
%1\sn.exe -q -Vr *,31bf3856ad364e35
%1\sn.exe -q -Vr *,b03f5f7f11d50a3a

%1\sn.exe -q -Vl

:EOF
endlocal
