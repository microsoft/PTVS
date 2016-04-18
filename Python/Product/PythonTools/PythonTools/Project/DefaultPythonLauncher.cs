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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Implements functionality of starting a project or a file with or without debugging.
    /// </summary>
    sealed class DefaultPythonLauncher : IProjectLauncher {
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;
        private readonly LaunchConfiguration _config;

        public DefaultPythonLauncher(IServiceProvider serviceProvider, LaunchConfiguration config) {
            _serviceProvider = serviceProvider;
            _pyService = _serviceProvider.GetPythonToolsService();
            _config = config;
        }

        public int LaunchProject(bool debug) {
            return Launch(_config, debug);
        }

        public int LaunchFile(string/*!*/ file, bool debug) {
            var config = _config.Clone();
            config.ScriptName = file;
            return Launch(_config, debug);
        }

        private int Launch(LaunchConfiguration config, bool debug) {
            if (debug) {
                StartWithDebugger(config);
            } else {
                StartWithoutDebugger(config);
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Creates language specific command line for starting the project without debigging.
        /// </summary>
        public string CreateCommandLineNoDebug(LaunchConfiguration config) {
            return string.Join(" ", new[] {
                config.InterpreterArguments,
                ProcessOutput.QuoteSingleArgument(config.ScriptName),
                config.ScriptArguments
            }.Where(s => !string.IsNullOrEmpty(s)));
        }

        /// <summary>
        /// Creates language specific command line for starting the project with debigging.
        /// </summary>
        public string CreateCommandLineDebug(LaunchConfiguration config) {
            return string.Join(" ", new[] {
                config.GetLaunchOption(PythonConstants.EnableNativeCodeDebugging).IsTrue() ? config.InterpreterArguments : null,
                ProcessOutput.QuoteSingleArgument(config.ScriptName),
                config.ScriptArguments
            }.Where(s => !string.IsNullOrEmpty(s)));
        }

        /// <summary>
        /// Default implementation of the "Start without Debugging" command.
        /// </summary>
        private Process StartWithoutDebugger(LaunchConfiguration config) {
            _pyService.Logger.LogEvent(Logging.PythonLogEvent.Launch, 0);
            var psi = CreateProcessStartInfoNoDebug(config);
            if (psi == null) {
                MessageBox.Show(
                    "The project cannot be started because its active Python environment does not have the interpreter executable specified.",
                    "Python Tools for Visual Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            return Process.Start(psi);
        }

        /// <summary>
        /// Default implementation of the "Start Debugging" command.
        /// </summary>
        private void StartWithDebugger(LaunchConfiguration config) {
            _pyService.Logger.LogEvent(Logging.PythonLogEvent.Launch, 1);
            VsDebugTargetInfo dbgInfo = new VsDebugTargetInfo();
            try {
                dbgInfo.cbSize = (uint)Marshal.SizeOf(dbgInfo);
                SetupDebugInfo(ref dbgInfo, config);

                if (string.IsNullOrEmpty(dbgInfo.bstrExe)) {
                    MessageBox.Show(Strings.DebugLaunchInterpreterMissing, Strings.ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                LaunchDebugger(_serviceProvider, dbgInfo);
            } finally {
                if (dbgInfo.pClsidList != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(dbgInfo.pClsidList);
                }
            }
        }

        private static void LaunchDebugger(IServiceProvider provider, VsDebugTargetInfo dbgInfo) {
            if (!Directory.Exists(UnquotePath(dbgInfo.bstrCurDir))) {
                MessageBox.Show(String.Format("Working directory \"{0}\" does not exist.", dbgInfo.bstrCurDir), "Python Tools for Visual Studio");
            } else if (!File.Exists(UnquotePath(dbgInfo.bstrExe))) {
                MessageBox.Show(String.Format("Interpreter \"{0}\" does not exist.", dbgInfo.bstrExe), "Python Tools for Visual Studio");
            } else {
                VsShellUtilities.LaunchDebugger(provider, dbgInfo);
            }
        }

        private static string UnquotePath(string p) {
            if (p.StartsWith("\"") && p.EndsWith("\"")) {
                return p.Substring(1, p.Length - 2);
            }
            return p;
        }

        /// <summary>
        /// Sets up debugger information.
        /// </summary>
        private unsafe void SetupDebugInfo(ref VsDebugTargetInfo dbgInfo, LaunchConfiguration config) {
            bool enableNativeCodeDebugging = false;

            dbgInfo.dlo = DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;
            dbgInfo.bstrExe = config.GetInterpreterPath();
            dbgInfo.bstrCurDir = config.WorkingDirectory;
            dbgInfo.bstrArg = CreateCommandLineDebug(config);
            dbgInfo.bstrRemoteMachine = null;
            dbgInfo.fSendStdoutToOutputWindow = 0;

            if (!enableNativeCodeDebugging) {
                // Set up project- and environment-specific debug options. Global debug options are passed
                // to the engine by CustomDebuggerEventHandler via IDebugEngine2.SetMetric in response to
                // the VsPackageMessage.SetDebugOptions custom debug event.

                dbgInfo.bstrOptions = AD7Engine.VersionSetting + "=" + config.Interpreter.Version.ToLanguageVersion().ToString();

                if (config.PreferWindowedInterpreter) {
                    dbgInfo.bstrOptions += ";" + AD7Engine.IsWindowsApplication + "=True";
                }

                if (!String.IsNullOrWhiteSpace(config.InterpreterArguments)) {
                    dbgInfo.bstrOptions += ";" + AD7Engine.InterpreterOptions + "=" + config.InterpreterArguments.Replace(";", ";;");
                }

                if (config.GetLaunchOption("DjangoDebugging").IsTrue()) {
                    dbgInfo.bstrOptions += ";" + AD7Engine.EnableDjangoDebugging + "=True";
                }
            }

            // Environment variables should be passed as a
            // null-terminated block of null-terminated strings. 
            // Each string is in the following form:name=value\0
            var buf = new StringBuilder();
            foreach (var kv in config.GetEnvironmentVariables()) {
                buf.AppendFormat("{0}={1}\0", kv.Key, kv.Value);
            }
            if (buf.Length > 0) {
                buf.Append("\0");
                dbgInfo.bstrEnv = buf.ToString();
            }

            if (config.GetLaunchOption(PythonConstants.EnableNativeCodeDebugging).IsTrue()) {
                dbgInfo.dwClsidCount = 2;
                dbgInfo.pClsidList = Marshal.AllocCoTaskMem(sizeof(Guid) * 2);
                var engineGuids = (Guid*)dbgInfo.pClsidList;
                engineGuids[0] = dbgInfo.clsidCustom = DkmEngineId.NativeEng;
                engineGuids[1] = AD7Engine.DebugEngineGuid;
            } else {
                // Set the Python debugger
                dbgInfo.clsidCustom = new Guid(AD7Engine.DebugEngineId);
                dbgInfo.grfLaunch = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_StopDebuggingOnEnd;
            }
        }

        /// <summary>
        /// Creates process info used to start the project with no debugging.
        /// </summary>
        private ProcessStartInfo CreateProcessStartInfoNoDebug(LaunchConfiguration config) {
            string command = CreateCommandLineNoDebug(config);

            ProcessStartInfo startInfo;
            if (!config.PreferWindowedInterpreter &&
                (_pyService.DebuggerOptions.WaitOnAbnormalExit || _pyService.DebuggerOptions.WaitOnNormalExit)) {
                command = "/c \"\"" + config.GetInterpreterPath() + "\" " + command;

                if (_pyService.DebuggerOptions.WaitOnNormalExit &&
                    _pyService.DebuggerOptions.WaitOnAbnormalExit) {
                    command += " & pause";
                } else if (_pyService.DebuggerOptions.WaitOnNormalExit) {
                    command += " & if not errorlevel 1 pause";
                } else if (_pyService.DebuggerOptions.WaitOnAbnormalExit) {
                    command += " & if errorlevel 1 pause";
                }

                command += "\"";
                startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), command);
            } else {
                startInfo = new ProcessStartInfo(config.GetInterpreterPath(), command);
            }

            startInfo.WorkingDirectory = config.WorkingDirectory;

            //In order to update environment variables we have to set UseShellExecute to false
            startInfo.UseShellExecute = false;

            foreach (var kv in config.GetEnvironmentVariables()) {
                startInfo.EnvironmentVariables[kv.Key] = kv.Value;
            }
            return startInfo;
        }
    }
}
