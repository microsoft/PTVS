param($root, $build, $buildidentifier, [switch] $mock)

if ($mock) {
    Import-Module "$root\Build\BuildReleaseMockHelpers.psm1" -Force
} else {
    Import-Module "$root\Build\BuildReleaseHelpers.psm1" -Force
}

$MsiFiles = @(@(
    "PythonToolsInstaller.msi"
) | %{ @{path="$build\raw\setup\en-us\$_"; name="PTVS$buildidentifier"} } | ?{ Test-Path "$($_.path)" })

$ErrorActionPreference = "Stop"

$jobs = @()
$jobs += begin_sign_files $MsiFiles `
         (mkdir "$build\raw\setup\signed" -Force) @("stevdo", "dinov") `
         "Python Tools for Visual Studio" "http://aka.ms/ptvs" "Python Tools for Visual Studio" "python;visual studio" `
         "msi"

end_sign_files $jobs

mkdir "$build\release" -Force
copy "$build\raw\setup\signed\PythonToolsInstaller.msi" "$build\release\PTVS$buildidentifier.msi"
