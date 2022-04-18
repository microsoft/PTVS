#! /usr/bin/env python3
'''Regenerates the strong name verification scripts based on the list of
assemblies stored in this file.

The generated files should be checked in.
'''

ASSEMBLIES = sorted([
    "CookiecutterTests",
    "DebuggerTests",
    "DebuggerUITests",
    "DebuggerUITestsRunner",
    "DjangoTests",
    "DjangoUITests",
    "DjangoUITestsRunner",
    "ExternalProfilerDriver",
    "FastCgiTests",
    "IpcJsonTests",
    "Microsoft.Python.Analysis",
    "Microsoft.Python.Analysis.Core",
    "Microsoft.Python.LanguageServer",
    "Microsoft.PythonTools",
    "Microsoft.PythonTools.Attacher",
    "Microsoft.PythonTools.AttacherX86",
    "Microsoft.PythonTools.BuildTasks",
    "Microsoft.PythonTools.Common",
    "Microsoft.PythonTools.Core",
    "Microsoft.CookiecutterTools",
    "Microsoft.PythonTools.Debugger",
    "Microsoft.PythonTools.Debugger.Concord",
    "Microsoft.PythonTools.Debugger.VCLauncher",
    "Microsoft.PythonTools.Django",
    "Microsoft.PythonTools.Django.Analysis",
    "Microsoft.PythonTools.EnvironmentsList",
    "Microsoft.PythonTools.Ipc.Json",
    "Microsoft.PythonTools.Profiling",
    "Microsoft.PythonTools.ProjectWizards",
    "Microsoft.PythonTools.RunElevated",
    "Microsoft.PythonTools.TestAdapter",
    "Microsoft.PythonTools.TestAdapter.Executor",
    "Microsoft.PythonTools.VSCommon",
    "Microsoft.PythonTools.VSInterpreters",
    "Microsoft.PythonTools.WebRole",
    "Microsoft.PythonTools.Workspace",
    "Microsoft.PythonTools.Wsl",
    "MockVsTests",
    "ProfilingTests",
    "ProfilingUITests",
    "ProfilingUITestsRunner",
    "ProjectUITests",
    "ProjectUITestsRunner",
    "PythonToolsTests",
    "PythonToolsMockTests",
    "PythonToolsUITests",
    "PythonToolsUITestsRunner",
    "ReplWindowUITests",
    "ReplWindowUITestsRunner",
    "SharedProjectUITests",
    "TestAdapterTests",
    "TestRunnerInterop",
    "TestSccPackage",
    "TestUtilities",
    "TestUtilities.Python",
    "TestUtilities.Python.Analysis",
    "TestUtilities.UI",
    "VSInterpretersTests",
])

def EnableSkipVerification():
    yield 'Windows Registry Editor Version 5.00'
    yield ''
    for name in ASSEMBLIES:
        yield '[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\StrongName\Verification\{},B03F5F7F11D50A3A]'.format(name)
    for name in ASSEMBLIES:
        yield '[HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\StrongName\Verification\{},B03F5F7F11D50A3A]'.format(name)

def EnableSkipVerificationX86():
    yield 'Windows Registry Editor Version 5.00'
    yield ''
    for name in ASSEMBLIES:
        yield '[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\StrongName\Verification\{},B03F5F7F11D50A3A]'.format(name)

def DisableSkipVerification():
    yield 'Windows Registry Editor Version 5.00'
    yield ''
    for name in ASSEMBLIES:
        yield '[-HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\StrongName\Verification\{},B03F5F7F11D50A3A]'.format(name)
    for name in ASSEMBLIES:
        yield '[-HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\StrongName\Verification\{},B03F5F7F11D50A3A]'.format(name)

def DisableSkipVerificationX86():
    yield 'Windows Registry Editor Version 5.00'
    yield ''
    for name in ASSEMBLIES:
        yield '[-HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\StrongName\Verification\{},B03F5F7F11D50A3A]'.format(name)

FILES = [
    EnableSkipVerification,
    EnableSkipVerificationX86,
    DisableSkipVerification,
    DisableSkipVerificationX86,
]

if __name__ == '__main__':
    for file in FILES:
        with open(file.__name__ + '.reg', 'w', encoding='utf-8') as f:
            f.writelines(line + '\n' for line in file())
        print('Wrote {}.reg'.format(file.__name__))
