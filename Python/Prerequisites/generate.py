#! /usr/bin/env python3
'''Regenerates the strong name verification scripts based on the list of
assemblies stored in this file.

The generated files should be checked in.
'''

ASSEMBLIES = sorted([
    "AnalysisTest",
    "AnalysisTests",
    "AzurePublishingUITests",
    "DebuggerTests",
    "DebuggerUITests",
    "DjangoTests",
    "DjangoUITests",
    "FastCgiTest",
    "IronPythonTests",
    "Microsoft.IronPythonTools.Resolver",
    "Microsoft.PythonTools",
    "Microsoft.PythonTools.Analysis",
    "Microsoft.PythonTools.Analysis.Browser",
    "Microsoft.PythonTools.Analyzer",
    "Microsoft.PythonTools.Attacher",
    "Microsoft.PythonTools.AttacherX86",
    "Microsoft.PythonTools.AzureSetup",
    "Microsoft.PythonTools.BuildTasks",
    "Microsoft.PythonTools.Debugger",
    "Microsoft.PythonTools.Django",
    "Microsoft.PythonTools.EnvironmentsList",
    "Microsoft.PythonTools.EnvironmentsList.Host",
    "Microsoft.PythonTools.Hpc",
    "Microsoft.PythonTools.ImportWizard",
    "Microsoft.PythonTools.IronPython",
    "Microsoft.PythonTools.IronPython.Interpreter",
    "Microsoft.PythonTools.ML",
    "Microsoft.PythonTools.MpiShim",
    "Microsoft.PythonTools.Profiling",
    "Microsoft.PythonTools.ProjectWizards",
    "Microsoft.PythonTools.PyKinect",
    "Microsoft.PythonTools.Pyvot",
    "Microsoft.PythonTools.TestAdapter",
    "Microsoft.PythonTools.Uwp",
    "Microsoft.PythonTools.VSInterpreters",
    "Microsoft.PythonTools.VsLogger",
    "Microsoft.PythonTools.WebRole",
    "Microsoft.VisualStudio.ReplWindow",
    "MockVsTests",
    "ProfilingUITests",
    "PythonToolsTests",
    "PythonToolsMockTests",
    "PythonToolsUITests",
    "ReplWindowUITests",
    "ReplWindowUITests25",
    "ReplWindowUITests26",
    "ReplWindowUITests27",
    "ReplWindowUITestsIRON27",
    "ReplWindowUITests30",
    "ReplWindowUITests31",
    "ReplWindowUITests32",
    "ReplWindowUITests33",
    "ReplWindowUITests34",
    "SharedProjectTests",
    "TestAdapterTests",
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
