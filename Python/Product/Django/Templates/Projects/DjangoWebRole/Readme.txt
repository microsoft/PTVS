To finish configuring your web role, ensure the following item has been added to
your ServiceDefinition.csdef file.

<ServiceDefinition ...>
  <WebRole ...>
    ...
    <Startup>
      <Task commandLine="ps.cmd ConfigureCloudService.ps1" executionContext="elevated" taskType="simple">
        <Environment>
          <Variable name="EMULATED">
            <RoleInstanceValue xpath="/RoleEnvironment/Deployment/@emulated"/>
          </Variable>
        </Environment>
      </Task>
    </Startup>

This file can be safely deleted once your configuration has been updated.


===================================
Troubleshooting Web Role Deployment
===================================

If your web role does not behave correctly after deployment, check the following:

1. Your Python project includes a bin\ folder with (at least):
   - ConfigureCloudService.ps1
   - ps.cmd

2. Your Cloud project includes the above XML and the command line matches.

   Earlier versions of PTVS would create similar XML with a different command
   line. If this exists, it should be removed when you add the above XML.

3. Your Python project includes either:
   - a requirements.txt file listing all dependencies, OR
   - a virtual environment containing all dependencies.
   
   ConfigureCloudService.ps1 will not install from requirements.txt into a
   virtual environment (but you can of course modify the script to do this if
   you need it).

4. Enable Remote Desktop on your Cloud Service and investigate the log files.

   Logs for ConfigureCloudService.ps1 are stored in the following path on the
   machine instance:
   
   C:\Resources\Directory\%RoleId%.DiagnosticStore\LogFiles

5. Start a discussion at http://pytools.codeplex.com/ for further help.
