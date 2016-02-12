param($root, $build, $buildidentifier, [switch] $mock)

if ($mock) {
    Import-Module "$root\Build\BuildReleaseMockHelpers.psm1" -Force
} else {
    Import-Module "$root\Build\BuildReleaseHelpers.psm1" -Force
}

$AuthenticodeFiles = @(
    "PythonToolsInstaller.msi"
) | %{ @{path="$build\raw\setup\en-us\$_"; name="$_"} } | ?{ Test-Path "$($_.path)" }

$ErrorActionPreference = "Stop"

$jobs = @()
$jobs += begin_sign_files $AuthenticodeFiles `
         (mkdir "$build\raw\setup\signed" -Force) @("stevdo", "dinov") `
         "Python Tools for Visual Studio" "http://aka.ms/ptvs" "Python Tools for Visual Studio" "python;visual studio" `
         "authenticode"

end_sign_files $jobs

copy "$build\raw\setup\signed\PythonToolsInstaller.msi" "$build\release\PTVS$buildidentifier.msi"
