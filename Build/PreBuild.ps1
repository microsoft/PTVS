param ($packages, $source)
"Restoring Packages"
$buildroot = $MyInvocation.MyCommand.Definition | Split-Path -Parent | Split-Path -Parent
pushd "$buildroot\Build"
if ($source) {
    .\nuget.exe sources add -Name PreBuildSource -Source $source
}
.\nuget.exe restore $packages -PackagesDirectory "$buildroot\BuildOutput"
if ($source) {
    .\nuget.exe sources remove -Name PreBuildSource
}
popd
