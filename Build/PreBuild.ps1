param ($config, $source)
"Restoring Packages"
$buildroot = $MyInvocation.MyCommand.Definition | Split-Path -Parent | Split-Path -Parent
pushd "$buildroot\Build"
if ($source) {
    .\nuget.exe sources add -Name PreBuildSource -Source $source
}
if ($env:TF_BUILD_BinariesDirectory) {
    $packages = "${env:TF_BUILD_BinariesDirectory}"
} else {
    $packages = "$buildroot\BuildOutput"
}
.\nuget.exe restore $config -PackagesDirectory $packages
if ($source) {
    .\nuget.exe sources remove -Name PreBuildSource
}
popd
