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

[xml]$rolemodel = Get-Content $env:RoleRoot\RoleModel.xml

$defaultpython = "python"       # or pythonx86, python2, python2x86
$defaultpythonversion = "3.5.2" # see nuget.org for current available versions

$interpreter_path = $env:PYTHON

$ns = @{ sd="http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition" };
$is_worker = (Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Properties/sd:Property[@name='RoleType'][@value='Worker']").Count -eq 1
$is_web = -not $is_worker
$is_debug = (Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Properties/sd:Property[@name='Configuration'][@value='Debug']").Count -eq 1
$is_emulated = $env:EMULATED -eq "true"

$bindir = split-path $MyInvocation.MyCommand.Path

if ($is_web) {
    $env:RootDir = (gi $((Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Sites/sd:Site")[0].Node.physicalDirectory)).FullName
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

    if (Test-Path "$bindir\$package.$version\tools\python.exe") {
        return (gi "$bindir\$package.$version\tools\python.exe");
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
    & $nuget install -OutputDirectory $bindir -Version "$version" "$package" | Out-Null;
    if ($?) {
        return (gi "$bindir\$package.$version\tools\python.exe");
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


if (Test-Path requirements.txt) {
    Set-Alias py (gi $interpreter_path -EA Stop)
    py -m pip -V
    if (-not $?) {
        $pip_downloader = gi "$bindir\pip_downloader.py" -EA SilentlyContinue
        if (-not $pip_downloader) {
            Invoke-WebRequest "https://go.microsoft.com/fwlink/?LinkID=393490" -OutFile "$bindir\pip_downloader.py"
            $pip_downloader = gi "$bindir\pip_downloader.py" -EA Stop
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

    $fastcgihandler = iex "& `"$interpreter_path`" -c `"import wfastcgi; print('$%1s|%$2s'%wfastcgi._enable())`" $env:appcmd"

    if ($is_emulated -and (Test-Path web.emulator.config)) {
        $webconfig = gi web.emulator.config -EA Stop
    } else if (-not $is_emulated -and (Test-Path web.cloud.config)) {
        $webconfig = gi web.cloud.config -EA Stop
    } else {
        $webconfig = gi web.config -EA Stop
    }

    "Updating $($webconfig.FullName) to reference $fastcgihandler"
    if (Test-Path web.config) {
        copy -force web.config "$webconfig.bak"
    }
    $xml = [xml](gc "$webconfig")
    foreach ($e in $xml.configuration.'system.webServer'.handlers.add) {
        if ($e.scriptProcessor -ieq '%fastcgihandler%') {
            $e.scriptProcessor = $fastcgihandler
        }
    }
    $xml.Save("web.config")
}
