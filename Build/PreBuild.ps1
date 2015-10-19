"Restoring Packages"
$buildroot = $MyInvocation.MyCommand.Definition | Split-Path -Parent | Split-Path -Parent
pushd "$buildroot\Build"
.\nuget.exe restore packages.config -PackagesDirectory "$buildroot\BuildOutput"
popd
