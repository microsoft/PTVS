To finish configuring your woker role, ensure the following items have been
added to your ServiceDefinition.csdef file.

<ServiceDefinition ...>
  <WorkerRole ...>
    ...
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


======================================
Troubleshooting Worker Role Deployment
======================================

If your worker role does not behave correctly after deployment, check the following:

1. Your Python project includes a bin\ folder with (at least):
   - ConfigureCloudService.ps1
   - LaunchWorker.ps1
   - ps.cmd

2. Your Cloud project includes the above XML and the command lines match.

3. Your Python project includes either:
   - a requirements.txt file listing all dependencies, OR
   - a virtual environment containing all dependencies.
   
   ConfigureCloudService.ps1 will not install from requirements.txt into a
   virtual environment (but you can of course modify the script to do this if
   you need it).

4. Enable Remote Desktop on your Cloud Service and investigate the log files.

   Logs for ConfigureCloudService.ps1 and LaunchWorker.ps1 are stored in the
   following path on the machine instance:
   
   C:\Resources\Directory\%RoleId%.DiagnosticStore\LogFiles

   Currently, the LaunchWorker.ps1 log is the only way to view any output
   or errors displayed by your Python program.

5. Start a discussion at http://pytools.codeplex.com/ for further help.
