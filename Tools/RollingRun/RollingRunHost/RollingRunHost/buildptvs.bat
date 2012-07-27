cd %~dp0
pushd ..\..\..\..\release\product\setup
powershell .\buildrelease.ps1 \\wtestserver\Build\ptvs
robocopy /s /e ..\..\..\..\binaries\release\completionDB\* \\wtestserver\build\ptvs\release\completiondb\
robocopy /s /e ..\..\..\..\binaries\release\Python.VS.TestData\* \\wtestserver\build\ptvs\release\Python.VS.TestData\
copy ..\..\..\..\binaries\release\*.py \\wtestserver\build\ptvs\release\

popd

