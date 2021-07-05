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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;

namespace Microsoft.IronPythonTools.Debugger {
    class IronPythonLauncher : IProjectLauncher {
        private static readonly Guid _cpyInterpreterGuid = new Guid("{2AF0F10D-7135-4994-9156-5D01C9C11B7E}");
        private static readonly Guid _cpy64InterpreterGuid = new Guid("{9A7A9026-48C1-4688-9D5D-E5699D47D074}");

        private readonly IPythonProject _project;
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;

        public IronPythonLauncher(IServiceProvider serviceProvider, PythonToolsService pyService, IPythonProject project) {
            _serviceProvider = serviceProvider;
            _pyService = pyService;
            _project = project;
        }

        #region IPythonLauncher Members

        private static readonly Lazy<string> NoIronPythonHelpPage = new Lazy<string>(() => {
            try {
                var path = Path.GetDirectoryName(typeof(IronPythonLauncher).Assembly.Location);
                return Path.Combine(path, "NoIronPython.html");
            } catch (ArgumentException) {
            } catch (NotSupportedException) {
            }
            return null;
        });

        public int LaunchProject(bool debug) {
            LaunchConfiguration config;
            try {
                config = _project.GetLaunchConfigurationOrThrow();
            } catch (NoInterpretersException) {
                throw new NoInterpretersException(null, NoIronPythonHelpPage.Value);
            }

            return Launch(config, debug);
        }

        public int LaunchFile(string file, bool debug) {
            LaunchConfiguration config;
            try {
                config = _project.GetLaunchConfigurationOrThrow();
            } catch (NoInterpretersException) {
                throw new NoInterpretersException(null, NoIronPythonHelpPage.Value);
            }

            return Launch(config, debug);
        }

        private int Launch(LaunchConfiguration config, bool debug) {
            DebugLaunchHelper.RequireStartupFile(config);

            //if (factory.Id == _cpyInterpreterGuid || factory.Id == _cpy64InterpreterGuid) {
            //    MessageBox.Show(
            //        "The project is currently set to use the .NET debugger for IronPython debugging but the project is configured to start with a CPython interpreter.\r\n\r\nTo fix this change the debugger type in project properties->Debug->Launch mode.\r\nIf IronPython is not an available interpreter you may need to download it from http://ironpython.codeplex.com.",
            //        "Visual Studio");
            //    return VSConstants.S_OK;
            //}

            try {
                if (debug) {
                    if (string.IsNullOrEmpty(config.InterpreterArguments)) {
                        config.InterpreterArguments = "-X:Debug";
                    } else if (config.InterpreterArguments.IndexOf("-X:Debug", StringComparison.OrdinalIgnoreCase) < 0) {
                        config.InterpreterArguments = "-X:Debug " + config.InterpreterArguments;
                    }

                    var debugStdLib = _project.GetProperty(IronPythonLauncherOptions.DebugStandardLibrarySetting);
                    bool debugStdLibResult;
                    if (!bool.TryParse(debugStdLib, out debugStdLibResult) || !debugStdLibResult) {
                        string interpDir = config.Interpreter.GetPrefixPath();
                        config.InterpreterArguments += " -X:NoDebug \"" + System.Text.RegularExpressions.Regex.Escape(Path.Combine(interpDir, "Lib\\")) + ".*\"";
                    }

                    using (var dti = DebugLaunchHelper.CreateDebugTargetInfo(_serviceProvider, config)) {
                        // Set the CLR debugger
                        dti.Info.clsidCustom = VSConstants.CLSID_ComPlusOnlyDebugEngine;
                        dti.Info.grfLaunch = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_StopDebuggingOnEnd;

                        // Clear the CLSID list while launching, then restore it
                        // so Dispose() can free it.
                        var clsidList = dti.Info.pClsidList;
                        dti.Info.pClsidList = IntPtr.Zero;
                        dti.Info.dwClsidCount = 0;
                        try {
                            dti.Launch();
                        } finally {
                            dti.Info.pClsidList = clsidList;
                        }
                    }
                } else {
                    var psi = DebugLaunchHelper.CreateProcessStartInfo(_serviceProvider, config);
                    Process.Start(psi).Dispose();
                }
            } catch (FileNotFoundException) {
            }
            return VSConstants.S_OK;
        }

        #endregion
    }
}
