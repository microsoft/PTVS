<#
.Synopsis
    Launches Python with the specified worker on a Microsoft Azure Cloud Service

.Description
    This script is deployed with your worker role and is used to launch the
    correct version of Python with the worker script. You may freely modify it
    to customize how your worker is run.

    To specify the version of Python your worker should run with, add a PYTHON
    environment variable with the path to the python.exe to run. If omitted,
    all 'bin\python*\tools' directories will be searched for the last python.exe
    available. These packages may be installed from nuget in
    ConfigureCloudService.ps1.

    To set PYTHONPATH (or equivalent) before running the worker, modify the
    variable in your ServiceDefinition.csdef file.

    To specify the script to run or command line arguments to use, update the
    ProgramEntryPoint element in the service definition.


    Ensure the following entry point specification is added to the
    ServiceDefinition.csdef file in your Cloud project. Note that the value for
    PYTHON should be set to the full path to your Python executable or omitted.
    PYTHONPATH may be freely configured, and extra environment variables should
    be added here. Modify ProgramEntryPoint to specify a different startup
    script or arguments.

    <Runtime>
      <Environment>
          <Variable name="EMULATED">
            <RoleInstanceValue xpath="/RoleEnvironment/Deployment/@emulated"/>
          </Variable>
          <Variable name="PYTHON" value="<path to python.exe of a previously installed Python>" />
          <Variable name="PYTHONPATH" value="" />
      </Environment>
      <EntryPoint>
        <ProgramEntryPoint commandLine="bin\ps.cmd LaunchWorker.ps1 worker.py" setReadyOnProcessStart="true" />
      </EntryPoint>
    </Runtime>
#>

[xml]$rolemodel = Get-Content $env:RoleRoot\RoleModel.xml

# These should match your ConfigureCloudService.ps1 file
$defaultpython = "python"
$defaultpythonversion = ""

$ns = @{ sd="http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition" };
$is_debug = (Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Properties/sd:Property[@name='Configuration'][@value='Debug']").Count -eq 1
$is_emulated = $env:EMULATED -eq 'true'

$bindir = split-path $MyInvocation.MyCommand.Path

$interpreter_path = $env:PYTHON;
if (-not $interpreter_path) {
    if (-not $defaultpythonversion) {
        $interpreter_path = "$bindir\$defaultpython\tools\python.exe"
    } else {
        $interpreter_path = "$bindir\$defaultpython.$defaultpythonversion\tools\python.exe"
    }
}

if (-not $interpreter_path) {
    throw "Cannot find a Python installation.";
} elseif (-not (Test-Path $interpreter_path)) {
    throw "Cannot find Python installation at $interpreter_path.";
}

"Executing $interpreter_path $args"
Start-Process -Wait -NoNewWindow $interpreter_path -ArgumentList $args
