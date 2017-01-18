// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

[assembly: AssemblyTitle("Visual Studio - Python support")]
[assembly: AssemblyDescription("Provides Python support within Visual Studio.")]

[assembly: ComVisible(false)]
[assembly: CLSCompliant(false)]
[assembly: NeutralResourcesLanguage("en-US")]

[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools", CodeBase = "Microsoft.PythonTools.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Analyzer", CodeBase = "Microsoft.PythonTools.Analyzer.exe", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Attacher", CodeBase = "Microsoft.PythonTools.Attacher.exe", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.AttacherX86", CodeBase = "Microsoft.PythonTools.AttacherX86.exe", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Analysis", CodeBase = "Microsoft.PythonTools.Analysis.dll", Version = AssemblyVersionInfo.StableVersionPrefix + ".0")]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Common", CodeBase = "Microsoft.PythonTools.Common.dll", Version = AssemblyVersionInfo.StableVersionPrefix + ".0")]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.EnvironmentsList", CodeBase = "Microsoft.PythonTools.EnvironmentsList.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Debugger", CodeBase = "Microsoft.PythonTools.Debugger.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.ImportWizard", CodeBase = "Microsoft.PythonTools.ImportWizard.dll", Version = "2.1.0.0")]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Ipc.Json", CodeBase = "Microsoft.PythonTools.Ipc.Json.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.ProjectWizards", CodeBase = "Microsoft.PythonTools.ProjectWizards.dll", Version = "2.1.0.0")]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.VSCommon", CodeBase = "Microsoft.PythonTools.VSCommon.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.VSInterpreters", CodeBase = "Microsoft.PythonTools.VSInterpreters.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "Microsoft.PythonTools.Workspace", CodeBase = "Microsoft.PythonTools.Workspace.dll", Version = AssemblyVersionInfo.StableVersion)]
