param($root, $build, [switch] $mock)

if ($mock) {
    Import-Module "$root\Build\BuildReleaseMockHelpers.psm1" -Force
} else {
    Import-Module "$root\Build\BuildReleaseHelpers.psm1" -Force
}

$AuthenticodeAndStrongNameFiles = @(
    "Microsoft.PythonTools.Analysis.dll",
    "Microsoft.PythonTools.Analyzer.exe",
    "Microsoft.PythonTools.Attacher.exe",
    "Microsoft.PythonTools.AttacherX86.exe",
    "Microsoft.PythonTools.BuildTasks.dll",
    "Microsoft.PythonTools.Debugger.dll",
    "Microsoft.PythonTools.EnvironmentsList.dll",
    "Microsoft.PythonTools.dll",
    "Microsoft.PythonTools.VSInterpreters.dll",
    "Microsoft.PythonTools.ImportWizard.dll",
    "Microsoft.PythonTools.IronPython.dll",
    "Microsoft.PythonTools.IronPython.Interpreter.dll",
    "Microsoft.PythonTools.Profiling.dll",
    "Microsoft.PythonTools.ProjectWizards.dll",
    "Microsoft.PythonTools.Django.dll",
    "Microsoft.IronPythonTools.Resolver.dll",
    "Microsoft.PythonTools.TestAdapter.dll",
    "Microsoft.PythonTools.Uwp.dll",
    "Microsoft.PythonTools.WebRole.dll",
    "Microsoft.PythonTools.AzureSetup.exe"
) | %{ @{path="$build\raw\binaries\$_"; name="$_"} } | ?{ Test-Path "$($_.path)" }

$AuthenticodeFiles = @(
    "PyDebugAttach.dll",
    "PyDebugAttachX86.dll",
    "Microsoft.PythonTools.Debugger.Helper.x86.dll",
    "Microsoft.PythonTools.Debugger.Helper.x64.dll",
    "VsPyProf.dll",
    "VsPyProfX86.dll"
) | %{ @{path="$build\raw\binaries\$_"; name="$_"} } | ?{ Test-Path "$($_.path)" }

$ErrorActionPreference = "Stop"

$jobs = @()
$jobs += begin_sign_files $AuthenticodeFiles `
         (mkdir "$build\raw\signed" -Force) @("stevdo", "dinov") `
         "Python Tools for Visual Studio" "http://aka.ms/ptvs" "Python Tools for Visual Studio" "python;visual studio" `
         "authenticode"

$jobs += begin_sign_files $AuthenticodeAndStrongNameFiles `
         (mkdir "$build\raw\signed" -Force) @("stevdo", "dinov") `
         "Python Tools for Visual Studio" "http://aka.ms/ptvs" "Python Tools for Visual Studio" "python;visual studio" `
         "authenticode;strongname" -delaysigned

end_sign_files $jobs

gci "$build\raw\signed" | `
    %{ $src = "$build\raw\signed\$_"; gci "$build\layout\$($_.Name)" -r | `
    %{ copy $src "$_" -force; Write-Output "Copied $src to $_"; } }

