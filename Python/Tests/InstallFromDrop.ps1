param (
    [string] $drop,
    [string] $local,
    [string] $install_msi,
    [string] $vsversion,
    [switch] $remove
)

$exitcode = 1

$ErrorActionPreference = 'SilentlyContinue'

#$start_time = (Get-Date).ToUniversalTime()

$vsinstalldir = Split-Path (Split-Path (&{switch($vsversion) {
    "10.0" { $env:VS100COMNTOOLS }
    "11.0" { $env:VS110COMNTOOLS }
    "12.0" { $env:VS120COMNTOOLS }
    "14.0" { $env:VS140COMNTOOLS }
}}))

$refreshdb = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)\refreshdb.py"

"Working in $local"
pushd (mkdir $local -Force)

if ($remove) {
    "Removing TestSccPackage"
    del "$vsinstalldir\Common7\IDE\CommonExtensions\Platform\TestSccPackage.*"

    "Uninstalling $install_msi"
    Start-Process -Wait msiexec -ArgumentList "/l*vx", "TestResults\uninstall.log", "/q", "/x", "$install_msi"

#    "Collecting Application log events"
#    Start-Process -Wait wevtutil -ArgumentList "export-log", "Application", `
#        "TestResults\Application.evtx", `
#        ("/q:*[System[TimeCreated[@SystemTime>='{0:s}Z']]]" -f ($start_time))

    "Complete"
    exit $exitcode
}


try {
    "Clearing old files"
    gci -Recurse -Attributes ReparsePoint | %{ & cmd /c rmdir "$($_.Fullname)" }
    if (gci *) { del * -Recurse -Force; sleep 5 }
    if (gci *) { del * -Recurse -Force; sleep 5 }
    if (gci *) { del * -Recurse -Force -ErrorAction Stop }
    "Copying MSIs from drop"
    copy "$drop\UnsignedMsi" . -r -fo
    "Copying skip verification scripts"
    copy "$drop\*.reg" . -fo
    "Copying test binaries"
    copy "$drop\Tests" Tests -r -fo
    "" | Out-File build.root

    "Executing EnableSkipVerification.reg"
    if (Test-Path "$env:WinDir\Sysnative\cmd.exe") {
        & "$env:WinDir\Sysnative\cmd.exe" /C "regedit.exe /s EnableSkipVerification.reg"
    } else {
        regedit.exe /s EnableSkipVerification.reg
    }

    "Restarting msiserver service"
    if ((Get-Service msiserver).Status -ne 'Stopped') { (Get-Service msiserver).Stop(); sleep 1; }
    if ((Get-Service msiserver).Status -ne 'Stopped') { (Get-Service msiserver).Stop(); sleep 1; }
    if ((Get-Service msiserver).Status -ne 'Stopped') { (Get-Service msiserver).Stop(); sleep 1; }
    if ((Get-Service msiserver).Status -ne 'Stopped') { throw "Failed to stop msiserver" }

    "Looking for $install_msi"
    if (-not (Test-Path $install_msi)) {
        "ABORTING: $install_msi could not be found."
        throw "$install_msi could not be found."
    }

    "Deploying TestSccPackage"
    copy "$drop\Tests\TestSccPackage.*" (mkdir "$vsinstalldir\Common7\IDE\CommonExtensions\Platform" -Force) -Force

    "Installing $install_msi"
    Start-Process -Wait msiexec -ArgumentList "/l*vx", "TestResults\install.log", "/q", "/i", "$install_msi"
    if ((gwmi win32_product -Filter "name like 'Python Tools %'").Count -eq 0) {
        "ABORTING: Failed to install"
        throw "Failed to install"
    }
    
    "Refreshing Completion DB"
    "$((gp HKLM:\Software\Wow6432Node\IronPython\2.7\InstallPath\).'(default)')\ipy.exe" "$drop\Tests\refreshdb.py"

    exit $exitcode
} finally {
    popd
}

exit $exitcode
