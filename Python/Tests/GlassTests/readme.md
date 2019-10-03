# Running Glass Tests

Please note that running these tests currently requires Microsoft internal NuGet package 'Microsoft.VisualStudio.Debugger.Glass.Tests.Internal'. While glass test tools are available publicly, the Python mixed-mode debugger is a concord plugin, and some of the tools needed to test concord plugins are not public yet.

Path to Python executables are currently hard coded in the Python*.GlassTestProps files, so make sure to install in the specified location below.

Some tests are currently failing and need to be fixed. Those have been renamed from TestScript.xml to TestScript.xml.skip, so that vstest doesn't discover/execute them.


## Install dependencies

Python 2.7, 2.7x64, manually download their symbols
```
c:\python27
c:\python27amd64
```

Python 3.6, 3.6x64 (customize, all users, install symbols)
```
C:\Program Files (x86)\Python36-32\python.exe
C:\Program Files\Python36\python.exe
```

VC for Python 2.7
https://www.microsoft.com/EN-US/DOWNLOAD/DETAILS.ASPX?ID=44266

Visual Studio 2017 C++ Desktop workload (VC for Python 3.5 and 3.6)


## Install glass

Create working folder at the root:

```
mkdir C:\mmd
cd c:\mmd
```

You'll need to have added the internal Debugger-glass feed to install the package:

```
.\nuget install Microsoft.VisualStudio.Debugger.Glass.Tests.Internal -version 15.0.27508-Build1460640
```

Run as Admin:

```
Set-ExecutionPolicy Unrestricted
C:\mmd\Microsoft.VisualStudio.Debugger.Glass.15.0.27507-Build1454857\UnpackGlass.ps1
```


## Copy PTVS binaries

Copy from the PTVS `binaries` folder into `C:\ConcordSDK\Tools\Glass` folder:

```
Microsoft.PythonTools.*
Microsoft.Python.*
DkmDebugger.vsdconfig
Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces.dll
```

Copy `Microsoft.PythonTools.*` and `Microsoft.Python.*` to `C:\ConcordSDK\Tools\Glass\Remote Debugger\x64`


## Copy PTVS tests

Copy `PythonTests` folder such that it is located at `C:\ConcordSDK\PythonTests`

Copy these files from `C:\ConcordSDK\PythonTests` into `C:\ConcordSDK\Tools\Glass` folder:

```

PythonEngine.regdef
```


## Running tests

Run tests **as admin** from a VS Command Prompt:

To list available tests:

```
vstest.console.exe C:\ConcordSDK\PythonTests\PythonConcord.GlassTestRoot /TestAdapterPath:C:\concordsdk\tools\testadapter\ /lt
```

To run tests:
```
vstest.console.exe C:\ConcordSDK\PythonTests\PythonConcord.GlassTestRoot /TestAdapterPath:C:\concordsdk\tools\testadapter\
```



delete [RegisterPythonEngine.cmd]



