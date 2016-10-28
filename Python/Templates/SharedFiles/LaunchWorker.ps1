<#
.Synopsis
    Launches Python with the specified worker on a Microsoft Azure Cloud Service

.Description
    This script is deployed with your worker role and is used to launch the
    correct version of Python with the worker script. You may freely modify it
    to customize how your worker is run, though most customizations can be made
    through your Python project.

    To specify the version of Python your worker should run with, make it the
    active environment for your project. (Ensure that you have a WebPI reference
    or startup task to install this version on the instance - see the
    documentation for ConfigureCloudService.ps1 for more details.)

    If your version of Python cannot be detected normally, you can add the
    DeployedPythonInterpreterPath property to your Python project by editing the
    .pyproj file. This path will take precedence over the active environment.

    To install packages using pip, include a requirements.txt file in the root
    directory of your project.

    To set PYTHONPATH (or equivalent) before running the worker, add the 
    necessary Search Paths to your project.

    To specify the script to run, make it the startup file in your project.

    To specify command-line arguments, add them to the Command Line Arguments
    property under Project Properties\Debug.


    Ensure the following entry point specification is added to the
    ServiceDefinition.csdef file in your Cloud project. Note that the value for
    PYTHON should be set to either a Company\Tag pair (suitable for locating an
    installation in the registry), or the full path to your Python executable.
    PYTHONPATH may be freely configured, and extra environment variables should
    be added here.

    <Runtime>
      <Environment>
          <Variable name="EMULATED">
            <RoleInstanceValue xpath="/RoleEnvironment/Deployment/@emulated"/>
          </Variable>
          <Variable name="PYTHON" value="..." />
          <Variable name="PYTHONPATH" value="" />
      </Environment>
      <EntryPoint>
        <ProgramEntryPoint commandLine="bin\ps.cmd LaunchWorker.ps1 worker.py" setReadyOnProcessStart="true" />
      </EntryPoint>
    </Runtime>
#>

[xml]$rolemodel = Get-Content $env:RoleRoot\RoleModel.xml

$ns = @{ sd="http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition" };
$is_debug = (Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Properties/sd:Property[@name='Configuration'][@value='Debug']").Count -eq 1
$is_emulated = $env:EMULATED -eq 'true'

if ($env:PYTHON -eq $null) {
    $interpreter_path = "${env:WINDIR}\py.exe"
} elseif (Test-Path -PathType Leaf $env:PYTHON) {
    $interpreter_path = $env:PYTHON
} else {
    foreach ($key in @('HKLM:\Software\Wow6432Node', 'HKLM:\Software', 'HKCU:\Software')) {
        $regkey = gp "$key\Python\${env:PYTHON}\InstallPath" -EA SilentlyContinue
        if ($regkey) {
            if ($regkey.ExecutablePath) {
                $interpreter_path = $regkey.ExecutablePath;
            } else {
                $interpreter_path = "$($regkey.'(default)')\python.exe"
            }
            if (Test-Path -PathType Leaf $interpreter_path) {
                break
            }
        }
    }
}

Start-Process -Wait -NoNewWindow $interpreter_path -ArgumentList $args