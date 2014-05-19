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
    ServiceDefinition.csdef file in your Cloud project:

    <Runtime>
      <Environment>
          <Variable name="EMULATED">
            <RoleInstanceValue xpath="/RoleEnvironment/Deployment/@emulated"/>
          </Variable>
      </Environment>
      <EntryPoint>
        <ProgramEntryPoint commandLine="bin\ps.cmd LaunchWorker.ps1" setReadyOnProcessStart="true" />
      </EntryPoint>
    </Runtime>
#>

[xml]$rolemodel = Get-Content $env:RoleRoot\RoleModel.xml

$ns = @{ sd="http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition" };
$is_debug = (Select-Xml -Xml $rolemodel -Namespace $ns -XPath "/sd:RoleModel/sd:Properties/sd:Property[@name='Configuration'][@value='Debug']").Count -eq 1
$is_emulated = $env:EMULATED -eq 'true'

$env:RootDir = (gi "$($MyInvocation.MyCommand.Path)\..\..").FullName
cd "${env:RootDir}"

$config = Get-Content "$(Get-Location)\bin\AzureSetup.cfg" -EA:Stop

function read_value($name, $default) {
    $value = (@($default) + @($config | %{ [regex]::Match($_, $name + '=(.+)') } | ?{ $_.Success } | %{ $_.Groups[1].Value }))[-1]
    return [Environment]::ExpandEnvironmentVariables($value)
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

$python_path_variable = read_value 'python_path_variable' 'PYTHONPATH'
${env:$python_path_variable} = read_value 'python_path' ''

$worker_directory = read_value 'worker_directory' '.'
cd $worker_directory

$worker_command = read_value 'worker_command' 'worker.py'
iex "py $worker_command"
