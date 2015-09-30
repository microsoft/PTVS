$buildroot = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition))
pushd "$buildroot\BuildOutput"
& "$buildroot\Build\nuget.exe" install "$buildroot\Build\packages.config"
popd
