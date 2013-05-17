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
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Implements functionality of starting a project or a file with or without debugging.
    /// </summary>
    sealed class DefaultPythonLauncher : IProjectLauncher {
        private readonly IPythonProject/*!*/ _project;

        public DefaultPythonLauncher(IPythonProject/*!*/ project) {
            Utilities.ArgumentNotNull("project", project);

            _project = project;
        }

        #region IPythonLauncher Members

        public int LaunchProject(bool debug) {
            string startupFile = ResolveStartupFile();
            return LaunchFile(startupFile, debug);
        }

        public int LaunchFile(string/*!*/ file, bool debug) {
            if (debug) {
                StartWithDebugger(file);
            } else {
                StartWithoutDebugger(file);
            }
            return VSConstants.S_OK;
        }

        #endregion

        private string GetInterpreterExecutableInternal(out bool isWindows) {
            if (!Boolean.TryParse(_project.GetProperty(CommonConstants.IsWindowsApplication) ?? Boolean.FalseString, out isWindows)) {
                isWindows = false;
            }
            
            string result;
            result = (_project.GetProperty(CommonConstants.InterpreterPath) ?? string.Empty).Trim();
            if (!String.IsNullOrEmpty(result)) {
                result = CommonUtils.GetAbsoluteFilePath(_project.ProjectDirectory, result);

                if (!File.Exists(result)) {
                    throw new FileNotFoundException(String.Format("Interpreter specified in the project does not exist: '{0}'", result), result);
                }

                return result;
            }

            var interpreter = _project.GetInterpreterFactory();
            var interpService = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
            if (interpService == null || interpService.NoInterpretersValue == interpreter) {
                PythonToolsPackage.OpenVsWebBrowser(CommonUtils.GetAbsoluteFilePath(PythonToolsPackage.GetPythonToolsInstallPath(), "NoInterpreters.html"));
                return null;
            }

            return !isWindows ?
                interpreter.Configuration.InterpreterPath :
                interpreter.Configuration.WindowsInterpreterPath;
        }

        /// <summary>
        /// Creates language specific command line for starting the project without debigging.
        /// </summary>
        public string CreateCommandLineNoDebug(string startupFile) {
            string cmdLineArgs = _project.GetProperty(CommonConstants.CommandLineArguments) ?? string.Empty;
            string interpArgs = _project.GetProperty(CommonConstants.InterpreterArguments) ?? string.Empty;

            return String.Format("{0} \"{1}\" {2}", interpArgs, startupFile, cmdLineArgs);
        }

        /// <summary>
        /// Creates language specific command line for starting the project with debigging.
        /// </summary>
        public string CreateCommandLineDebug(string startupFile) {
            string cmdLineArgs = _project.GetProperty(CommonConstants.CommandLineArguments) ??  string.Empty;

            return String.Format("\"{0}\" {1}", startupFile, cmdLineArgs);
        }

        /// <summary>
        /// Default implementation of the "Start withput Debugging" command.
        /// </summary>
        private void StartWithoutDebugger(string startupFile) {
            var psi = CreateProcessStartInfoNoDebug(startupFile);
            if (psi == null) {
                return;
            }
            Process.Start(psi);
        }

        /// <summary>
        /// Default implementation of the "Start Debugging" command.
        /// </summary>
        private void StartWithDebugger(string startupFile) {
            VsDebugTargetInfo dbgInfo = new VsDebugTargetInfo();
            dbgInfo.cbSize = (uint)Marshal.SizeOf(dbgInfo);

            SetupDebugInfo(ref dbgInfo, startupFile);
            if (!string.IsNullOrEmpty(dbgInfo.bstrExe)) {
                LaunchDebugger(PythonToolsPackage.Instance, dbgInfo);
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
        private void SetupDebugInfo(ref VsDebugTargetInfo dbgInfo, string startupFile) {
            dbgInfo.dlo = DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;
            bool isWindows;
            var interpreterPath = GetInterpreterExecutableInternal(out isWindows);
            if (string.IsNullOrEmpty(interpreterPath)) {
                return;
            }
            dbgInfo.bstrExe = interpreterPath;
            dbgInfo.bstrCurDir = _project.GetWorkingDirectory();
            dbgInfo.bstrArg = CreateCommandLineDebug(startupFile);
            dbgInfo.bstrRemoteMachine = null;

            dbgInfo.fSendStdoutToOutputWindow = 0;
            StringDictionary env = new StringDictionary();
            string interpArgs = _project.GetProperty(CommonConstants.InterpreterArguments);
            dbgInfo.bstrOptions = AD7Engine.VersionSetting + "=" + _project.GetInterpreterFactory().GetLanguageVersion().ToString();
            if (!isWindows) {
                if (PythonToolsPackage.Instance.OptionsPage.WaitOnAbnormalExit) {
                    dbgInfo.bstrOptions += ";" + AD7Engine.WaitOnAbnormalExitSetting + "=True";
                }
                if (PythonToolsPackage.Instance.OptionsPage.WaitOnNormalExit) {
                    dbgInfo.bstrOptions += ";" + AD7Engine.WaitOnNormalExitSetting + "=True";
                }
            }
            if (PythonToolsPackage.Instance.OptionsPage.TeeStandardOutput) {
                dbgInfo.bstrOptions += ";" + AD7Engine.RedirectOutputSetting + "=True";
            }
            if (PythonToolsPackage.Instance.OptionsPage.BreakOnSystemExitZero) {
                dbgInfo.bstrOptions += ";" + AD7Engine.BreakSystemExitZero + "=True";
            }
            if (PythonToolsPackage.Instance.OptionsPage.DebugStdLib) {
                dbgInfo.bstrOptions += ";" + AD7Engine.DebugStdLib + "=True";
            }
            if (!String.IsNullOrWhiteSpace(interpArgs)) {
                dbgInfo.bstrOptions += ";" + AD7Engine.InterpreterOptions + "=" + interpArgs.Replace(";", ";;");
            }
            var djangoDebugging = _project.GetProperty("DjangoDebugging");
            bool enableDjango;
            if (!String.IsNullOrWhiteSpace(djangoDebugging) && Boolean.TryParse(djangoDebugging, out enableDjango)) {
                dbgInfo.bstrOptions += ";" + AD7Engine.EnableDjangoDebugging + "=True";
            }

            SetupEnvironment(env);
            if (env.Count > 0) {
                // add any inherited env vars
                var variables = Environment.GetEnvironmentVariables();
                foreach (var key in variables.Keys) {
                    string strKey = (string)key;
                    if (!env.ContainsKey(strKey)) {
                        env.Add(strKey, (string)variables[key]);
                    }
                }

                //Environemnt variables should be passed as a
                //null-terminated block of null-terminated strings. 
                //Each string is in the following form:name=value\0
                StringBuilder buf = new StringBuilder();
                foreach (DictionaryEntry entry in env) {
                    buf.AppendFormat("{0}={1}\0", entry.Key, entry.Value);
                }
                buf.Append("\0");
                dbgInfo.bstrEnv = buf.ToString();
            }
            // Set the Python debugger
            dbgInfo.clsidCustom = new Guid(AD7Engine.DebugEngineId);
            dbgInfo.grfLaunch = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_StopDebuggingOnEnd;
        }

        /// <summary>
        /// Sets up environment variables before starting the project.
        /// </summary>
        private void SetupEnvironment(StringDictionary environment) {
            string pathEnvVar = _project.GetInterpreterFactory().Configuration.PathEnvironmentVariable;
            if (!String.IsNullOrWhiteSpace(pathEnvVar)) {
                environment[pathEnvVar] = string.Join(";", _project.GetSearchPaths());
            }
        }

        /// <summary>
        /// Creates process info used to start the project with no debugging.
        /// </summary>
        private ProcessStartInfo CreateProcessStartInfoNoDebug(string startupFile) {
            string command = CreateCommandLineNoDebug(startupFile);

            bool isWindows;
            string interpreter = GetInterpreterExecutableInternal(out isWindows);
            if (string.IsNullOrEmpty(interpreter)) {
                return null;
            }
            ProcessStartInfo startInfo;
            if (!isWindows && (PythonToolsPackage.Instance.OptionsPage.WaitOnAbnormalExit || PythonToolsPackage.Instance.OptionsPage.WaitOnNormalExit)) {
                command = "/c \"\"" + interpreter + "\" " + command;

                if (PythonToolsPackage.Instance.OptionsPage.WaitOnNormalExit &&
                    PythonToolsPackage.Instance.OptionsPage.WaitOnAbnormalExit) {
                    command += " & pause";
                } else if (PythonToolsPackage.Instance.OptionsPage.WaitOnNormalExit) {
                    command += " & if not errorlevel 1 pause";
                } else if (PythonToolsPackage.Instance.OptionsPage.WaitOnAbnormalExit) {
                    command += " & if errorlevel 1 pause";
                }

                command += "\"";
                startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), command);
            } else {
                startInfo = new ProcessStartInfo(interpreter, command);
            }

            startInfo.WorkingDirectory = _project.GetWorkingDirectory();

            //In order to update environment variables we have to set UseShellExecute to false
            startInfo.UseShellExecute = false;
            SetupEnvironment(startInfo.EnvironmentVariables);
            return startInfo;
        }

        private string ResolveStartupFile() {
            string startupFile = _project.GetStartupFile();
            if (string.IsNullOrEmpty(startupFile)) {
                //TODO: need to start active file then
                throw new ApplicationException("No startup file is defined for the startup project.");
            }
            return startupFile;
        }
    }
}
