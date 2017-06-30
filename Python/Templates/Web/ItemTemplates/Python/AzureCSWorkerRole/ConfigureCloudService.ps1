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
    to customize how your role is configured.

    By default, Python will be downloaded and installed from nuget.org. Modify
    the $defaultpython and $defaultpythonversion variables to change the version
    installed. Alternatively, add a startup task to install your choice of
    Python from any installer and add a PYTHON environment variable pointing at
    the executable to run.

    To install packages using pip, include a requirements.txt file in the root
    directory of your project. If pip is not already available, it will be
    downloaded and installed automatically. To avoid security risks and
    bandwidth charges associated with downloads, you can deploy your own copy of
    the 'pip_downloader.py' script downloaded from
    https://go.microsoft.com/fwlink/?LinkID=393490 and the source packages for
    pip and setuptools named 'pip.tar.gz' and 'setuptools.tar.gz' in your 'bin'
    directory.


    For worker roles, ensure the following startup task specification is
    included in the ServiceDefinition.csdef file in your Cloud project:

    <Startup>
      <Task commandLine="bin\ps.cmd ConfigureCloudService.ps1" executionContext="elevated" taskType="simple">
        <Environment>
          <Variable name="EMULATED">
            <RoleInstanceValue xpath="/RoleEnvironment/Deployment/@emulated"/>
          </Variable>
          <Variable name="PYTHON" value="<optional path to python.exe installed by a prior Task>" />
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
          <Variable name="PYTHON" value="<optional path to python.exe installed by a prior Task>" />
        </Environment>
      </Task>
    </Startup>

    For web roles, you will also require a suitable web.config in your site's
    root directory. An example is included in the bin folder containing this
    file or in your project directory.
#>

(Get-Host).UI.WriteLine("")
(Get-Host).UI.WriteLine("=====================================")
(Get-Host).UI.WriteLine("Script started at $(Get-Date -format s)")
(Get-Host).UI.WriteErrorLine("")
(Get-Host).UI.WriteErrorLine("=====================================")
(Get-Host).UI.WriteErrorLine("Script started at $(Get-Date -format s)")

[xml]$rolemodel = Get-Content $env:RoleRoot\RoleModel.xml

$defaultpython = "python"       # or pythonx86, python2, python2x86
$defaultpythonversion = ""      # see nuget.org for current available versions

$interpreter_path = $env:PYTHON

$ns = @{ sd="http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition" };
$is_worker = (Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Properties/sd:Property[@name='RoleType'][@value='Worker']").Count -eq 1
$is_web = -not $is_worker
$is_debug = (Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Properties/sd:Property[@name='Configuration'][@value='Debug']").Count -eq 1
$is_emulated = $env:EMULATED -eq "true"

$bindir = split-path $MyInvocation.MyCommand.Path

if ($is_web) {
    $roledir = (Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Sites/sd:Site")[0].Node.physicalDirectory
    if (split-path $roledir -IsAbsolute) {
        $env:RootDir = $roledir
    } else {
        $env:RootDir = join-path $env:RoleRoot $roledir
    }
} else {
    $env:RootDir = split-path $bindir
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

function install-python-from-nuget {
    param([string]$package=$defaultpython, [string]$version=$defaultpythonversion)

    if (-not $version) {
        $expected = "$bindir\$package\tools\python.exe"
    } else {
        $expected = "$bindir\$package.$version\tools\python.exe"
    }
    if (Test-Path $expected) {
        return (gi $expected);
    }

    # Find nuget.exe in the bin folder first
    $nuget = gcm "$bindir\nuget.exe" -EA SilentlyContinue;
    if (-not $nuget) {
        # Fall back on looking throughout the system
        $nuget = gcm nuget.exe -EA SilentlyContinue;
    }
    if (-not $nuget) {
        # Finally, download it into the bin directory
        Invoke-WebRequest https://aka.ms/nugetclidl -OutFile "$bindir\nuget.exe";
        $nuget = gcm "$bindir\nuget.exe" -EA SilentlyContinue;
    }
    if (-not $version) {
        & $nuget install -OutputDirectory $bindir -ExcludeVersion "$package" | Out-Null;
    } else {
        & $nuget install -OutputDirectory $bindir -Version "$version" "$package" | Out-Null;
    }
    if ($?) {
        return (gi $expected);
    }
}

if (-not $interpreter_path) {
    $interpreter_path = install-python-from-nuget;
}

if (-not $interpreter_path) {
    throw "Cannot find a Python installation.";
} elseif (-not (Test-Path $interpreter_path)) {
    throw "Cannot find Python installation at $interpreter_path.";
}

function py {
    Start-Process -Wait -NoNewWindow $interpreter_path -ArgumentList $args -WorkingDirectory $env:RootDir
}

if (Test-Path requirements.txt) {
    py -m pip -V
    if (-not $?) {
        if (-not (Test-Path "$bindir\pip_downloader.py")) {
            Invoke-WebRequest "https://go.microsoft.com/fwlink/?LinkID=393490" -OutFile "$bindir\pip_downloader.py"
        }
        py "$bindir\pip_downloader.py"
    }
    py -m pip install -r requirements.txt
}

if ($is_web) {
    $appcmd = $null
    $appcmdargs = @()
    if ($env:appcmd) {
        function get-args { $args }
        $appcmd = (iex "get-args $env:appcmd") | select -first 1
        $appcmdargs = (iex "get-args $env:appcmd") | select -skip 1
    } else {
        $appcmd = (gcm appcmd -EA 0).Path
        if (-not $appcmd) {
            $appcmd = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
        }
    }

    $quoted_interpreter_path = $interpreter_path
    if ($quoted_interpreter_path -contains ' ') {
        $quoted_interpreter_path = "`"$quoted_interpreter_path`""
    }
    $quoted_wfastcgi_path = "$bindir\wfastcgi.py"
    if ($quoted_wfastcgi_path -contains ' ') {
        $quoted_wfastcgi_path = "`"$quoted_wfastcgi_path`""
    }

    $appcmdargs = @(
        $appcmdargs,
        "set",
        "config",
        "/section:system.webServer/fastCGI",
        "/+[fullPath='$quoted_interpreter_path',arguments='$quoted_wfastcgi_path',signalBeforeTerminateSeconds='30']"
    )

    "Configuring FastCGI with `"$appcmd`" $appcmdargs"
    Start-Process -Wait -NoNewWindow $appcmd -ArgumentList $appcmdargs
    $fastcgihandler = "$quoted_interpreter_path|$quoted_wfastcgi_path"

    if ($is_emulated -and (Test-Path web.emulator.config)) {
        $webconfig = gi web.emulator.config -EA Stop
    } elseif (-not $is_emulated -and (Test-Path web.cloud.config)) {
        $webconfig = gi web.cloud.config -EA Stop
    } else {
        $webconfig = gi web.config -EA Stop
    }

    "Updating $($webconfig.FullName) to reference $fastcgihandler"
    if (Test-Path web.config) {
        copy -force web.config "web.config.bak"
    }
    $xml = [xml](gc "$webconfig")
    foreach ($e in $xml.configuration.'system.webServer'.handlers.add) {
        if ($e.name -ieq 'PythonHandler') {
            $e.scriptProcessor = $fastcgihandler
        }
    }
    $xml.Save("$(get-location)\web.config")
}
