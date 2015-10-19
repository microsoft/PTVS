param (
    [string] $vsversion,
    [switch] $install,
    [switch] $remove
)

$drop = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)
$install_msi = "$drop\setup\en-us\PythonToolsInstaller.msi"

$logs = mkdir "$drop\logs" -Force

"Finding $install_msi"
if (-not (Test-Path $install_msi)) {
    "ABORTING: $install_msi could not be found."
    throw "$install_msi could not be found."
}

$exitcode = 1

$ErrorActionPreference = 'SilentlyContinue'

$vsinstalldir = Split-Path (Split-Path (&{switch($vsversion) {
    "15.0" { $env:VS150COMNTOOLS }
    "14.0" { $env:VS140COMNTOOLS }
}}))

if ($install) {
    "Deploying TestSccPackage"
    copy "$drop\test\TestSccPackage.*" (mkdir "$vsinstalldir\Common7\IDE\CommonExtensions\Platform" -Force) -Force

    "Installing $install_msi"
    Start-Process -Wait msiexec -ArgumentList "/l*vx", "$logs\install.log", "/q", "/i", "$install_msi"
    if ((gwmi win32_product -Filter "name like 'Python Tools %'").Count -eq 0) {
        "ABORTING: Failed to install"
        throw "Failed to install"
    }

    "Creating build.root"
    "" | Out-File "$drop\build.root"

    "Refreshing Completion DB"
    & "$((gp HKLM:\Software\Wow6432Node\IronPython\2.7\InstallPath\).'(default)')\ipy.exe" "$drop\test\refreshdb.py"
} 

if ($remove) {
    "Removing TestSccPackage"
    del "$vsinstalldir\Common7\IDE\CommonExtensions\Platform\TestSccPackage.*"

    "Uninstalling $install_msi"
    Start-Process -Wait msiexec -ArgumentList "/l*vx", "$logs\uninstall.log", "/q", "/x", "$install_msi"
}

"Complete"
exit $exitcode
