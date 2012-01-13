======================================================================
How to integrate a new project with the TCWB build system:
======================================================================
Following these instructions lets your new project:
	*  Be automatically built and tested along with other TCWB projects.
	*  Share the common objects of the TCWB projects.
	*  Share the common versioning of TCWB assemblies and files.

Before starting, your project source files folder should already reside within the 
directory tree of your workspace mapped to tcvstf\TC in TFS.

Make the following edits:

(1) Edit your .csproj file, adding the following lines at the top just below 
the <Project> tag:
----------------------------------------------------------------------
  <PropertyGroup>
    <BuildRoot>$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), Common.Build.settings))</BuildRoot>
  </PropertyGroup>

  <Import Project="$(BuildRoot)\Common.Build.settings" />
----------------------------------------------------------------------

(2) Edit your Properties\AssemblyInfo.cs file.  Remove the lines that duplicate the lines 
in the files Build\AssemblyInfoCommon.cs and Build\AssemblyVersion.cs.  (See the comments 
in those 2 files for more details.)

(3) Check out from source control and edit the file dirs.proj in the parent folder above 
your project folder.  Insert between the <itemGroup> tags a "ProjectFile Include" line of 
the form:

----------------------------------------------------------------------
  <ItemGroup>
    ...
    <ProjectFile Include="MyProjectFolder\MyProject.csproj"/>
    ...
  </ItemGroup>
----------------------------------------------------------------------

Now do the following tests:

(4) Open and build your project in Visual Studio.  Ensure there are no errors or warnings.

(5) Build your project using MSBuild.exe and ensure there are no errors or warnings.  That is: 
Bring up a Visual Studio Command Prompt.  In the parent folder above your project folder, 
give the command:

	MSBUILD dirs.proj

	-- or to write the output to file msbuild.log, give the command:

	MSBUILD dirs.proj /fileLogger

Read through the output listing to make sure your project got built.  Warnings and errors 
will display in yellow and red in the command window.

(6) Run ProjectConsistencyTest.  That is: Bring up a Visual Studio Command Prompt.  In the 
enlistment root folder (folder containing Binaries\ and Incubation\) give the command:

	MSTEST /testcontainer:Binaries\Win32\Debug\ProjectConsistencyTests.dll

If there are any failed tests, double-click the Results file (location is shown at the end 
of the test run), view details of the failed test, make necessary corrections, and repeat.

(7) When the above are warning- and error-free, add your project to source control and 
check in all changes.

(8) In VS in Team Explorer, view builds.  Check the next Rolling Checkin build following 
your checkin for any warnings or errors.

