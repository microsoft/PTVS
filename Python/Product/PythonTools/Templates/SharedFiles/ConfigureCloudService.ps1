#  Copyright (c) Microsoft Corporation.
#
#  This source code is subject to terms and conditions of the Apache License, Version 2.0. A
#  copy of the license can be found in the License.html file at the root of this distribution. If
#  you cannot locate the Apache License, Version 2.0, please send an email to
#  vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
#  by the terms of the Apache License, Version 2.0.
# 
#  You must not remove this notice, or any other, from this software.

<#
.Synopsis
    Configures a Microsoft Azure Cloud Service to run Python roles

.Description
    This script is deployed with your Python role and is used to install and
    configure dependencies before your role start. You may freely modify it
    to customize how your role is configured, though most customizations can be
    made through your Python project.

    To specify the version of Python to install, you can add WebPI references to
    your project. If the active environment for your project is Python 2.7, a
    reference will be added automatically. If you host your own WebPI feed,
    simply specify the URL when adding the reference to include products from
    that feed.

    (If WebPI is not available on the remote machine, it will be downloaded and
    installed automatically. To avoid charges associated with downloads, you can
    deploy your own copy of the US English WebPI installer alongside this file.
    Note that WebPI has different installers for 32-bit and 64-bit systems. You
    will need to include the correct one or both with your deployment.)

    To install packages using pip, include a requirements.txt file in the root
    directory of your project.
    
    (If pip is not already available, it will be downloaded and installed
    automatically. To avoid charges associated with downloads, you can deploy
    your own copy of the 'pip_downloader.py' script downloaded from
    https://go.microsoft.com/fwlink/?LinkID=393490 and the sources for pip and
    setuptools named 'pip.tar.gz' and 'setuptools.tar.gz' alongside this file.)

    As an alternative to WebPI and pip, you can deploy installers and add custom
    startup tasks to your ServiceDefinition.csdef file.


    For worker roles, ensure the following startup task specification is
    included in the ServiceDefinition.csdef file in your Cloud project:

    <Startup>
      <Task commandLine="bin\ps.cmd ConfigureCloudService.ps1" executionContext="elevated" taskType="simple">
        <Environment>
          <Variable name="EMULATED">
            <RoleInstanceValue xpath="/RoleEnvironment/Deployment/@emulated"/>
          </Variable>
        </Environment>
      </Task>
    </Startup>

    For web roles, ensure the following startup task specification is
    included in the ServiceDefinition.csdef file in your Cloud project:
    (Note the different value for commandLine.)

    <Startup>
      <Task commandLine="ps.cmd ConfigureCloudService.ps1" executionContext="elevated" taskType="simple">
        <Environment>
          <Variable name="EMULATED">
            <RoleInstanceValue xpath="/RoleEnvironment/Deployment/@emulated"/>
          </Variable>
        </Environment>
      </Task>
    </Startup>
#>

[xml]$rolemodel = Get-Content $env:RoleRoot\RoleModel.xml

$ns = @{ sd="http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition" };
$is_worker = (Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Properties/sd:Property[@name='RoleType'][@value='Worker']").Count -eq 1
$is_web = -not $is_worker
$is_debug = (Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Properties/sd:Property[@name='Configuration'][@value='Debug']").Count -eq 1
$is_emulated = $env:EMULATED -eq "true"

if ($is_web) {
    cd "${env:RoleRoot}\"
    $env:RootDir = (gi $((Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Sites/sd:Site")[0].Node.physicalDirectory)).FullName
} else {
    $env:RootDir = (gi "$($MyInvocation.MyCommand.Path)\..\..").FullName
}
cd $env:RootDir

if ($is_web -and -not $is_emulated) {
    $os_features = @(
        "IIS-WebServerRole", "IIS-WebServer", "IIS-CommonHttpFeatures", "IIS-StaticContent", "IIS-DefaultDocument",
        "IIS-DirectoryBrowsing", "IIS-HttpErrors", "IIS-HealthAndDiagnostics", "IIS-HttpLogging", 
        "IIS-LoggingLibraries", "IIS-RequestMonitor", "IIS-Security", "IIS-RequestFiltering",
        "IIS-HttpCompressionStatic", "IIS-WebServerManagementTools", "IIS-ManagementConsole", 
        "WAS-WindowsActivationService", "WAS-ProcessModel", "WAS-NetFxEnvironment", "WAS-ConfigurationAPI", "IIS-CGI"
    );
    
    try {
        gcm dism -EA Stop
        Start-Process -wait dism -ArgumentList (@('/Online', '/NoRestart', '/Enable-Feature') + @($os_features | %{ '/Feature ' + $_ }))
    } catch {
        Start-Process -wait pkgmgr -ArgumentList /quiet, /iu:$([string]::Join(';', $os_features))
    }
}

$config = Get-Content "$(Get-Location)\bin\AzureSetup.cfg" -EA:Stop

function read_value($name, $default) {
    $value = (@($default) + @($config | %{ [regex]::Match($_, $name + '=(.+)') } | ?{ $_.Success } | %{ $_.Groups[1].Value }))[-1]
    return [Environment]::ExpandEnvironmentVariables($value)
}

if (-not $is_emulated) {
    if ([Environment]::Is64BitOperatingSystem) {
        $webpisetup = "https://go.microsoft.com/fwlink/?linkid=226239"
        $webpicmdname = "webpicmd-x64.exe"
        $webpimsiname = "WebPlatformInstaller_amd64_en-US.msi"
    } else {
        $webpisetup = "https://go.microsoft.com/fwlink/?linkid=226238"
        $webpicmdname = "webpicmd.exe"
        $webpimsiname = "WebPlatformInstaller_x86_en-US.msi"
    }

    $webpicmd = (gcm $webpicmdname -EA SilentlyContinue).Path
    if (-not $webpicmd) {
        $webpicmd = (gi "${env:ProgramFiles}\Microsoft\Web Platform Installer\$webpicmdname" -EA SilentlyContinue).FullName
        if (-not $webpicmd) {
            $msi = gi "$(Get-Location)\$webpimsiname" -EA SilentlyContinue
            if (-not $msi) {
                $msi = gi "$(Get-Location)\bin\$webpimsiname" -EA SilentlyContinue
            }
            if (-not $msi) {
                $req = Invoke-WebRequest $webpisetup -UseBasicParsing -EA Stop
                [System.IO.File]::WriteAllBytes("$(Get-Location)\bin\$webpimsiname", $req.Content)
                $msi = gi "$(Get-Location)\bin\$webpimsiname" -EA Stop
            }
            Start-Process -wait (gcm msiexec).Path -ArgumentList /quiet, /i, $msi, ADDLOCAL=ALL
            $webpicmd = (gi "${env:ProgramFiles}\Microsoft\Web Platform Installer\$webpicmdname" -EA Stop).FullName
        }
    }

    $webpi_products = @(
        $config |
            %{ [regex]::Match($_, 'webpi_install=(.+);(.+)') } |
            ?{ $_.Success } |
            %{ @{ feed=$_.Groups[1].Value; product=$_.Groups[2].Value } }
    )
    
    try {
        try {
            $key = [Microsoft.Win32.Registry]::Users.OpenSubKey(".DEFAULT\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders", $true)
            $oldvalue = $key.GetValue("Local AppData", "", [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
            $key.SetValue("Local AppData", "$env:SystemDrive\LocalAppData_$([Guid]::NewGuid().ToString('N'))", [Microsoft.Win32.RegistryValueKind]::String)
        } catch {
            $oldvalue = ''
        }

        $grouped = @($webpi_products | group {$_.feed})
        foreach ($item in $grouped) {
            Start-Process -wait $webpicmd -ArgumentList /Install, /Log:"$(Get-Location)\webpi.log", /AcceptEula, /Feeds:$($item.Name), /Products:$([string]::Join(',', $item.Group.product))
        }
    } finally {
        if ($oldvalue) {
            $key.SetValue("Local AppData", $oldvalue, [Microsoft.Win32.RegistryValueKind]::ExpandString)
        }
    }
}

$interpreter_path = read_value 'interpreter_path'
$interpreter_path_emulated = read_value 'interpreter_path_emulated'

if ($is_emulated -and $interpreter_path_emulated -and (Test-Path $interpreter_path_emulated)) {
    $interpreter_path = $interpreter_path_emulated
}

if (-not $interpreter_path -or -not (Test-Path $interpreter_path)) {
    $interpreter_version = read_value 'interpreter_version' '2.7'
    foreach ($key in @('HKLM:\Software\Wow6432Node', 'HKLM:\Software', 'HKCU:\Software')) {
        $regkey = gp "$key\Python\PythonCore\$interpreter_version\InstallPath" -EA SilentlyContinue
        if ($regkey) {
            $interpreter_path = "$($regkey.'(default)')\python.exe"
            if (Test-Path $interpreter_path) {
                break
            }
        }
    }
}
Set-Alias py (gi $interpreter_path -EA Stop)


if (Test-Path requirements.txt) {
    py -m pip -V
    if (-not $?) {
        $pip_downloader = gi "$(Get-Location)\pip_downloader.py" -EA SilentlyContinue
        if (-not $pip_downloader) {
            $pip_downloader = gi "$(Get-Location)\bin\pip_downloader.py" -EA SilentlyContinue
        }
        if (-not $pip_downloader) {
            $req = Invoke-WebRequest "https://go.microsoft.com/fwlink/?LinkID=393490" -UseBasicParsing
            [System.IO.File]::WriteAllBytes("$(Get-Location)\bin\pip_downloader.py", $req.Content)
            $pip_downloader = gi "$(Get-Location)\bin\pip_downloader.py" -EA Stop
        }
        py $pip_downloader
    }
    py -m pip install -r requirements.txt
}

if ($is_web) {
    $appcmdargs = ''
    if ($env:appcmd) {
        Set-Alias appcmd (gi ($env:appcmd -replace '^("(.+?)"|(\S+)).*$', '$2$3'))
        $appcmdargs = $env:appcmd -replace '^(".+?"|\S+)\s*(.*)$', '$2'
    } else {
        try {
            gcm appcmd -EA Stop
        } catch {
            Set-Alias appcmd (gi "$env:SystemRoot\System32\inetsrv\appcmd.exe" -EA Stop)
        }
    }

    # The first -replace parameter needs backslashes escaped, while the second
    # does not. So we are really replacing 1 backslash with 2.
    $interp = $interpreter_path -replace '\\', '\\'
    $wfastcgi = (gi "$(Get-Location)\bin\wfastcgi.py" -EA Stop).FullName -replace '\\', '\\'

    if ($is_debug) {
        $max_requests = 1
    } else {
        $max_requests = 1000
    }

    iex "appcmd $appcmdargs set config /section:system.webServer/fastCGI ""/+[fullPath='$interp',arguments='\""""$wfastcgi\""""',instanceMaxRequests='$max_requests',signalBeforeTerminateSeconds='30']"""

    if ($is_emulated) {
        $webconfig = gi web.config -EA Stop
    } else {
        $webconfig = gi web.cloud.config -EA SilentlyContinue
        if (-not $webconfig) {
            $webconfig = gi web.config -EA Stop
        }
    }

    copy -force $webconfig "$webconfig.bak"
    Get-Content "$webconfig.bak" | `
        %{ $_ -replace '%wfastcgipath%', "&quot;$wfastcgi&quot;" } | `
        %{ $_ -replace '%interpreterpath%', $interp } | `
        %{ $_ -replace '%rootdir%', $env:RootDir } | `
        Out-File -Encoding UTF8 "web.config" -Force
}
