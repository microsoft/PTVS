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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows.Forms;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project.Web {
    /// <summary>
    /// Web launcher.  This wraps the default launcher and provides it with a
    /// different IPythonProject which launches manage.py with the appropriate
    /// options.  Upon a successful launch we will then automatically load the
    /// appropriate page into the users web browswer.
    /// </summary>
    [Export(typeof(IProjectLauncher))]
    class PythonWebLauncher : IProjectLauncher {
        private int? _testServerPort;

        private readonly IPythonProject _project;
        private readonly IAsyncCommand _runServerCommand;
        private readonly IAsyncCommand _debugServerCommand;

        public PythonWebLauncher(IPythonProject project) {
            _project = project;

            var project2 = project as IPythonProject2;
            if (project2 != null) {
                // The provider may return its own object, but the web launcher only
                // supports instances of CustomCommand.
                _runServerCommand = project2.FindCommand("PythonRunWebServerCommand");
                _debugServerCommand = project2.FindCommand("PythonDebugWebServerCommand");
            }

            var portNumber = _project.GetProperty(PythonConstants.WebBrowserPortSetting);
            int portNum;
            if (Int32.TryParse(portNumber, out portNum)) {
                _testServerPort = portNum;
            }
        }

        #region IPythonLauncher Members

        public int LaunchProject(bool debug) {
            return LaunchFile(_project.GetStartupFile(), debug);
        }

        public int LaunchFile(string file, bool debug) {
            bool isWindows;
            if (!Boolean.TryParse(_project.GetProperty(CommonConstants.IsWindowsApplication) ?? Boolean.FalseString, out isWindows)) {
                isWindows = false;
            }

            var cmd = debug ? _debugServerCommand : _runServerCommand;
            var customCmd = cmd as CustomCommand;
            if (customCmd == null && cmd != null) {
                // We have a command we don't understand, so we'll execute it
                // but won't start debugging. The (presumably) flavored project
                // that provided the command is responsible for handling the
                // attach.
                cmd.Execute(null);
                return VSConstants.S_OK;
            }

            CommandStartInfo startInfo;
            var project2 = _project as IPythonProject2;
            if (customCmd != null && project2 != null) {
                // We have one of our own commands, so let's use the actual
                // start info.
                startInfo = customCmd.GetStartInfo(project2);
            } else {
                // No command, so set up a startInfo that looks like the default
                // launcher.
                startInfo = new CommandStartInfo {
                    Filename = file,
                    Arguments = _project.GetProperty(CommonConstants.CommandLineArguments) ?? string.Empty,
                    WorkingDirectory = _project.GetWorkingDirectory(),
                    EnvironmentVariables = null,
                    TargetType = "script",
                    ExecuteIn = "console"
                };
            }

            if (isWindows) {
                // Must run hidden if running with pythonw
                startInfo.ExecuteIn = "hidden";
            }

            var env = startInfo.EnvironmentVariables;
            if (env == null) {
                env = startInfo.EnvironmentVariables = new Dictionary<string, string>();
            }

            string value;
            if (!env.TryGetValue("SERVER_HOST", out value) || string.IsNullOrEmpty(value)) {
                env["SERVER_HOST"] = "localhost";
            }
            int dummyInt;
            if (!env.TryGetValue("SERVER_PORT", out value) ||
                string.IsNullOrEmpty(value) ||
                !int.TryParse(value, out dummyInt)) {
                env["SERVER_PORT"] = TestServerPortString;
            }

            if (debug) {
                PythonToolsPackage.Instance.Logger.LogEvent(Logging.PythonLogEvent.Launch, 1);

                using (var dsi = CreateDebugTargetInfo(startInfo, GetInterpreterPath(_project, isWindows))) {
                    dsi.Launch(PythonToolsPackage.Instance);
                }
            } else {
                PythonToolsPackage.Instance.Logger.LogEvent(Logging.PythonLogEvent.Launch, 0);

                var psi = CreateProcessStartInfo(startInfo, GetInterpreterPath(_project, isWindows));

                var process = Process.Start(psi);
                if (process != null) {
                    StartBrowser(GetFullUrl(), () => process.HasExited);
                }
            }

            return VSConstants.S_OK;
        }

        private static string GetInterpreterPath(IPythonProject project, bool isWindows) {
            var factory = project.GetInterpreterFactory();

            if (factory == null) {
                throw new NoInterpretersException();
            }

            var interpreterService = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
            if (interpreterService == null || factory == interpreterService.NoInterpretersValue) {
                throw new NoInterpretersException();
            }

            return isWindows ?
                factory.Configuration.WindowsInterpreterPath :
                factory.Configuration.InterpreterPath;
        }

        private static void StartBrowser(string url, Func<bool> shortCircuitPredicate) {
            Uri uri;
            if (!String.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri)) {
                OnPortOpenedHandler.CreateHandler(
                    uri.Port,
                    shortCircuitPredicate: shortCircuitPredicate,
                    action: () => {
                        var web = PythonToolsPackage.GetGlobalService(typeof(SVsWebBrowsingService)) as IVsWebBrowsingService;
                        if (web == null) {
                            PythonToolsPackage.OpenWebBrowser(url);
                            return;
                        }

                        ErrorHandler.ThrowOnFailure(
                            web.CreateExternalWebBrowser(
                                (uint)__VSCREATEWEBBROWSER.VSCWB_ForceNew,
                                VSPREVIEWRESOLUTION.PR_Default,
                                url
                            )
                        );
                    }
                );
            }
        }

        sealed class DebugTargetInfo : IDisposable {
            public VsDebugTargetInfo Info;

            public DebugTargetInfo() {
                Info = new VsDebugTargetInfo();
                Info.cbSize = (uint)Marshal.SizeOf(Info);
            }

            private static string UnquotePath(string p) {
                if (string.IsNullOrEmpty(p) || !p.StartsWith("\"") || !p.EndsWith("\"")) {
                    return p;
                }
                return p.Substring(1, p.Length - 2);
            }

            public void Validate() {
                if (!Directory.Exists(UnquotePath(Info.bstrCurDir))) {
                    // TODO: Use resource string
                    throw new Exception(string.Format("Working directory \"{0}\" does not exist.", Info.bstrCurDir));
                }
                
                if (!File.Exists(UnquotePath(Info.bstrExe))) {
                    // TODO: Use resource string
                    throw new Exception(string.Format("Interpreter \"{0}\" does not exist.", Info.bstrExe));
                }
            }

            public void Launch(IServiceProvider provider) {
                try {
                    Validate();
                } catch (Exception ex) {
                    MessageBox.Show(
                        ex.Message,
                        SR.GetString(SR.PythonToolsForVisualStudio)
                    );
                    return;
                }

                VsShellUtilities.LaunchDebugger(provider, Info);
            }

            public void Dispose() {
                if (Info.pClsidList != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(Info.pClsidList);
                    Info.pClsidList = IntPtr.Zero;
                }
            }
        }

        private static IEnumerable<string> GetGlobalDebuggerOptions(bool allowPauseAtEnd, bool alwaysPauseAtEnd) {
            var options = PythonToolsPackage.Instance.DebuggingOptionsPage;

            if (alwaysPauseAtEnd || allowPauseAtEnd && options.WaitOnAbnormalExit) {
                yield return AD7Engine.WaitOnAbnormalExitSetting + "=True";
            }
            if (alwaysPauseAtEnd || allowPauseAtEnd && options.WaitOnNormalExit) {
                yield return AD7Engine.WaitOnNormalExitSetting + "=True";
            }
            if (options.TeeStandardOutput) {
                yield return AD7Engine.RedirectOutputSetting + "=True";
            }
            if (options.BreakOnSystemExitZero) {
                yield return AD7Engine.BreakSystemExitZero + "=True";
            }
            if (options.DebugStdLib) {
                yield return AD7Engine.DebugStdLib + "=True";
            }
        }

        private IEnumerable<string> GetProjectDebuggerOptions() {
            var factory = _project.GetInterpreterFactory();

            yield return string.Format("{0}={1}", AD7Engine.VersionSetting, factory.Configuration.Version);
            yield return string.Format("{0}={1}",
                AD7Engine.InterpreterOptions,
                _project.GetProperty(CommonConstants.InterpreterArguments) ?? string.Empty
            );
            var url = GetFullUrl();
            if (!String.IsNullOrWhiteSpace(url)) {
                yield return string.Format("{0}={1}", AD7Engine.WebBrowserUrl, HttpUtility.UrlEncode(url));
            }

        }

        private unsafe DebugTargetInfo CreateDebugTargetInfo(
            CommandStartInfo startInfo,
            string interpreterPath
        ) {
            var dti = new DebugTargetInfo();

            var alwaysPause = startInfo.ExecuteInConsoleAndPause;
            // We only want to debug a web server in a console.
            startInfo.ExecuteIn = "console";
            startInfo.AdjustArgumentsForProcessStartInfo(interpreterPath, handleConsoleAndPause: false);

            try {
                bool enableNativeCodeDebugging = false;
#if DEV11_OR_LATER
                bool.TryParse(_project.GetProperty(PythonConstants.EnableNativeCodeDebugging), out enableNativeCodeDebugging);
#endif

                dti.Info.dlo = DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;
                dti.Info.bstrExe = startInfo.Filename;
                dti.Info.bstrCurDir = startInfo.WorkingDirectory;

                dti.Info.bstrRemoteMachine = null;
                dti.Info.fSendStdoutToOutputWindow = 0;

                if (!enableNativeCodeDebugging) {
                    dti.Info.bstrOptions = string.Join(";",
                        GetGlobalDebuggerOptions(true, alwaysPause)
                            .Concat(GetProjectDebuggerOptions())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Select(s => s.Replace(";", ";;"))
                    );
                }

                if (startInfo.EnvironmentVariables != null && startInfo.EnvironmentVariables.Any()) {
                    // Environment variables should be passed as a 
                    // null-terminated block of null-terminated strings. 
                    // Each string is in the following form:name=value\0
                    var buf = new StringBuilder();
                    foreach (var kv in startInfo.EnvironmentVariables) {
                        buf.AppendFormat("{0}={1}\0", kv.Key, kv.Value);
                    }
                    buf.Append("\0");
                    dti.Info.bstrEnv = buf.ToString();
                }

                dti.Info.bstrArg = startInfo.Arguments;

                if (enableNativeCodeDebugging) {
#if DEV11_OR_LATER
                    dti.Info.dwClsidCount = 2;
                    dti.Info.pClsidList = Marshal.AllocCoTaskMem(sizeof(Guid) * 2);
                    var engineGuids = (Guid*)dti.Info.pClsidList;
                    engineGuids[0] = dti.Info.clsidCustom = DkmEngineId.NativeEng;
                    engineGuids[1] = AD7Engine.DebugEngineGuid;
#else
                    Debug.Fail("enableNativeCodeDebugging cannot be true in VS 2010");
#endif
                } else {
                    // Set the Python debugger
                    dti.Info.clsidCustom = new Guid(AD7Engine.DebugEngineId);
                    dti.Info.grfLaunch = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_StopDebuggingOnEnd;
                }

                // Null out dti so that it is not disposed before we return.
                var result = dti;
                dti = null;
                return result;
            } finally {
                if (dti != null) {
                    dti.Dispose();
                }
            }
        }

        private ProcessStartInfo CreateProcessStartInfo(
            CommandStartInfo startInfo,
            string interpreterPath
        ) {
            bool alwaysPause = startInfo.ExecuteInConsoleAndPause;
            // We only want to run the webserver in a console.
            startInfo.ExecuteIn = "console";
            startInfo.AdjustArgumentsForProcessStartInfo(interpreterPath, handleConsoleAndPause: false);

            var psi = new ProcessStartInfo {
                FileName = startInfo.Filename,
                Arguments = startInfo.Arguments,
                WorkingDirectory = startInfo.WorkingDirectory,
                UseShellExecute = false
            };

            if (startInfo.EnvironmentVariables != null) {
                foreach (var kv in startInfo.EnvironmentVariables) {
                    psi.EnvironmentVariables[kv.Key] = kv.Value;
                }
            }
            if (!psi.EnvironmentVariables.ContainsKey("SERVER_HOST") ||
                string.IsNullOrEmpty(psi.EnvironmentVariables["SERVER_HOST"])) {
                psi.EnvironmentVariables["SERVER_HOST"] = "localhost";
            }
            int dummyInt;
            if (!psi.EnvironmentVariables.ContainsKey("SERVER_PORT") ||
                string.IsNullOrEmpty(psi.EnvironmentVariables["SERVER_PORT"]) ||
                !int.TryParse(psi.EnvironmentVariables["SERVER_PORT"], out dummyInt)) {
                    psi.EnvironmentVariables["SERVER_PORT"] = TestServerPortString;
            }

            // Pause if the user has requested it.
            string pauseCommand = null;
            if (alwaysPause ||
                PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit &&
                PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit) {
                pauseCommand = "pause";
            } else if (PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit &&
                !PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit) {
                pauseCommand = "if errorlevel 1 pause";
            } else if (PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit &&
                !PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit) {
                pauseCommand = "if not errorlevel 1 pause";
            }
            if (!string.IsNullOrEmpty(pauseCommand)) {
                psi.Arguments = string.Format("/c \"{0} {1}\" & {2}",
                    ProcessOutput.QuoteSingleArgument(psi.FileName),
                    psi.Arguments,
                    pauseCommand
                );
                psi.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            }

            return psi;
        }

        #endregion

        private string GetFullUrl() {
            var host = _project.GetProperty(PythonConstants.WebBrowserUrlSetting);

            try {
                return GetFullUrl(host, TestServerPort);
            } catch (UriFormatException) {
                var output = OutputWindowRedirector.GetGeneral(PythonToolsPackage.Instance);
                output.WriteErrorLine(SR.GetString(SR.ErrorInvalidLaunchUrl, host));
                output.ShowAndActivate();
                return string.Empty;
            }
        }

        internal static string GetFullUrl(string host, int port) {
            UriBuilder builder;
            Uri uri;
            if (Uri.TryCreate(host, UriKind.Absolute, out uri)) {
                builder = new UriBuilder(uri);
            } else {
                builder = new UriBuilder();
                builder.Scheme = Uri.UriSchemeHttp;
                builder.Host = "localhost";
                builder.Path = host;
            }

            builder.Port = port;

            return builder.ToString();
        }

        private string TestServerPortString {
            get {
                if (!_testServerPort.HasValue) {
                    _testServerPort = GetFreePort();
                }
                return _testServerPort.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private int TestServerPort {
            get {
                if (!_testServerPort.HasValue) {
                    _testServerPort = GetFreePort();
                }
                return _testServerPort.Value;
            }
        }

        private static int GetFreePort() {
            return Enumerable.Range(new Random().Next(49152, 65536), 60000).Except(
                from connection in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections()
                select connection.LocalEndPoint.Port
            ).First();
        }
    }
}
