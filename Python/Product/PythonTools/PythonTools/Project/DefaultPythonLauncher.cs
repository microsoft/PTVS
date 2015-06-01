/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Implements functionality of starting a project or a file with or without debugging.
    /// </summary>
    sealed class DefaultPythonLauncher : IProjectLauncher2 {
        private readonly IPythonProject/*!*/ _project;
        private readonly PythonToolsService _pyService;
        private readonly IServiceProvider _serviceProvider;

        public DefaultPythonLauncher(IServiceProvider serviceProvider, PythonToolsService pyService, IPythonProject/*!*/ project) {
            Utilities.ArgumentNotNull("project", project);

            _serviceProvider = serviceProvider;
            _pyService = pyService;
            _project = project;
        }

        #region IPythonLauncher Members

        public int LaunchProject(bool debug) {
            string startupFile = ResolveStartupFile();
            return LaunchFile(startupFile, debug);
        }

        public int LaunchFile(string/*!*/ file, bool debug) {
            return LaunchFile(file, debug, null);
        }

        public int LaunchFile(string file, bool debug, IProjectLaunchProperties props) {
            if (debug) {
                StartWithDebugger(file, PythonProjectLaunchProperties.Create(_project, _serviceProvider, props));
            } else {
                StartWithoutDebugger(file, PythonProjectLaunchProperties.Create(_project, _serviceProvider, props));
            }

            return VSConstants.S_OK;
        }

        #endregion

        /// <summary>
        /// Creates language specific command line for starting the project without debigging.
        /// </summary>
        public string CreateCommandLineNoDebug(string startupFile, IPythonProjectLaunchProperties props) {
            return string.Join(" ", new[] {
                props.GetInterpreterArguments(),
                ProcessOutput.QuoteSingleArgument(startupFile),
                props.GetArguments()
            }.Where(s => !string.IsNullOrEmpty(s)));
        }

        /// <summary>
        /// Creates language specific command line for starting the project with debigging.
        /// </summary>
        public string CreateCommandLineDebug(string startupFile, IPythonProjectLaunchProperties props) {
            return string.Join(" ", new[] {
                (props.GetIsNativeDebuggingEnabled() ?? false) ? props.GetInterpreterArguments() : null,
                ProcessOutput.QuoteSingleArgument(startupFile),
                props.GetArguments()
            }.Where(s => !string.IsNullOrEmpty(s)));
        }

        /// <summary>
        /// Default implementation of the "Start without Debugging" command.
        /// </summary>
        private Process StartWithoutDebugger(string startupFile, IPythonProjectLaunchProperties props) {
            _pyService.Logger.LogEvent(Logging.PythonLogEvent.Launch, 0);
            var psi = CreateProcessStartInfoNoDebug(startupFile, props);
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
        private void StartWithDebugger(string startupFile, IPythonProjectLaunchProperties props) {
            _pyService.Logger.LogEvent(Logging.PythonLogEvent.Launch, 1);
            VsDebugTargetInfo dbgInfo = new VsDebugTargetInfo();
            try {
                dbgInfo.cbSize = (uint)Marshal.SizeOf(dbgInfo);
                SetupDebugInfo(ref dbgInfo, startupFile, props);

                if (string.IsNullOrEmpty(dbgInfo.bstrExe)) {
                    MessageBox.Show(SR.GetString(SR.DebugLaunchInterpreterMissing), SR.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        private unsafe void SetupDebugInfo(
            ref VsDebugTargetInfo dbgInfo,
            string startupFile,
            IPythonProjectLaunchProperties props
        ) {
            bool enableNativeCodeDebugging = false;

            dbgInfo.dlo = DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;
            dbgInfo.bstrExe = props.GetInterpreterPath();
            dbgInfo.bstrCurDir = props.GetWorkingDirectory();
            dbgInfo.bstrArg = CreateCommandLineDebug(startupFile, props);
            dbgInfo.bstrRemoteMachine = null;
            dbgInfo.fSendStdoutToOutputWindow = 0;

            if (!enableNativeCodeDebugging) {
                // Set up project- and environment-specific debug options. Global debug options are passed
                // to the engine by CustomDebuggerEventHandler via IDebugEngine2.SetMetric in response to
                // the VsPackageMessage.SetDebugOptions custom debug event.

                string interpArgs = _project.GetProperty(PythonConstants.InterpreterArgumentsSetting);
                dbgInfo.bstrOptions = AD7Engine.VersionSetting + "=" + _project.GetInterpreterFactory().GetLanguageVersion().ToString();

                if (props.GetIsWindowsApplication() ?? false) {
                    dbgInfo.bstrOptions += ";" + AD7Engine.IsWindowsApplication + "=True";
                }

                if (!String.IsNullOrWhiteSpace(interpArgs)) {
                    dbgInfo.bstrOptions += ";" + AD7Engine.InterpreterOptions + "=" + interpArgs.Replace(";", ";;");
                }

                var djangoDebugging = _project.GetProperty("DjangoDebugging");
                bool enableDjango;
                if (!String.IsNullOrWhiteSpace(djangoDebugging) && Boolean.TryParse(djangoDebugging, out enableDjango)) {
                    dbgInfo.bstrOptions += ";" + AD7Engine.EnableDjangoDebugging + "=True";
                }
            }

            var env = new Dictionary<string, string>(props.GetEnvironment(true));
            PythonProjectLaunchProperties.MergeEnvironmentBelow(env, null, true);
            if (env.Any()) {
                //Environment variables should be passed as a
                //null-terminated block of null-terminated strings. 
                //Each string is in the following form:name=value\0
                var buf = new StringBuilder();
                foreach (var kv in env) {
                    buf.AppendFormat("{0}={1}\0", kv.Key, kv.Value);
                }
                buf.Append("\0");
                dbgInfo.bstrEnv = buf.ToString();
            }

            if (props.GetIsNativeDebuggingEnabled() ?? false) {
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
        private ProcessStartInfo CreateProcessStartInfoNoDebug(string startupFile, IPythonProjectLaunchProperties props) {
            string command = CreateCommandLineNoDebug(startupFile, props);

            ProcessStartInfo startInfo;
            if (!(props.GetIsWindowsApplication() ?? false) &&
                (_pyService.DebuggerOptions.WaitOnAbnormalExit || _pyService.DebuggerOptions.WaitOnNormalExit)) {
                command = "/c \"\"" + props.GetInterpreterPath() + "\" " + command;

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
                startInfo = new ProcessStartInfo(props.GetInterpreterPath(), command);
            }

            startInfo.WorkingDirectory = props.GetWorkingDirectory();

            //In order to update environment variables we have to set UseShellExecute to false
            startInfo.UseShellExecute = false;

            var env = new Dictionary<string, string>(props.GetEnvironment(true));
            PythonProjectLaunchProperties.MergeEnvironmentBelow(env, null, true);
            foreach (var kv in env) {
                startInfo.EnvironmentVariables[kv.Key] = kv.Value;
            }
            return startInfo;
        }

        private string ResolveStartupFile() {
            string startupFile = _project.GetStartupFile();
            if (string.IsNullOrEmpty(startupFile)) {
                //TODO: need to start active file then
                throw new InvalidOperationException(SR.GetString(SR.NoStartupFileAvailable));
            }
            return startupFile;
        }
    }
}
