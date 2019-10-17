For developers working on Python Tools at Microsoft: Please refer to internal documentation for more information

Note: Some the instructions are path specific. If you use a different path than what is specified below, you will need to modify the configurations to point to the new path.
	For example, Python 38 64 bit must be installed in the default location. If it is not, "Python38.64GlassTestProps" needs to be updated
	
Setup Glass Testing Environment
    Install Python in default location (install symbols as well)
	Install VS 
		Select "Development with C++" workload			
	Download Drop.exe
	Find the file path of the latest version of Glass
    Create a root folder where all your glass and test files will be located
		Use this exact folder name because the file paths for the Concord Python files are hard coded
        "C:\GlassTesting"
	Download the Glass files with Drop.exe
	Put "PythonTests" folder and all subfiles into "C:\GlassTesting" folder
	Install the glass test adapter extension from "C:\GlassTesting\GlassStandAlone\Glass.TestAdapter.vsix"
	
	Transfer the following files from PTVS binaries folder to C:\GlassTesting\GlassStandAlone\Glass
		DkmDebugger.vsdconfig". Located in PTVS build output folder
		Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces.dll
	Transfer all files that start with "Microsoft.Python" to "C:\GlassTesting\GlassStandAlone\Glass" and "C:\GlassTesting\GlassStandAlone\Glass\Remote Debugger\x64"
	Transfer "PythonEngine.regdef" to "C:\GlassTesting\GlassStandAlone\Glass"
	
Running Glass Tests
	Run VS as admin and load "C:\GlassTesting\PythonTests" in open folder environment
	Run the tests through test explorer
	
	
