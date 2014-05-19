To finish configuring your woker role, ensure the following items have been
added to your ServiceDefinition.csdef file.

    <Startup>
      <Task commandLine="bin\ps.cmd ConfigureCloudService.ps1" executionContext="elevated" taskType="simple">
        <Environment>
          <Variable name="EMULATED">
            <RoleInstanceValue xpath="/RoleEnvironment/Deployment/@emulated"/>
          </Variable>
        </Environment>
      </Task>
    </Startup>
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

While the code above and the included PowerShell scripts may be edited freely,
it is possible to set most configuration options through your Python project.

To specify the version of Python your worker should run with, make it the
active environment for your project. (Ensure that you have a WebPI reference
or startup task to install this version on the instance - see the
documentation for ConfigureCloudService.ps1 for more details.)

If your version of Python cannot be detected normally, you can add the
DeployedPythonInterpreterPath property to your Python project by editing the
.pyproj file. This path will take precedence over the active environment.

To install packages using pip, update the requirements.txt file in the root
directory of your project.

To set PYTHONPATH (or equivalent) before running the worker, add the 
necessary Search Paths to your project.

To specify the script to run, make it the startup file in your project.

To specify command-line arguments, add them to the Command Line Arguments
property under Project Properties\Debug.
