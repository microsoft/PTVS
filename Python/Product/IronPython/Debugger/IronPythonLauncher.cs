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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Project;
using Microsoft.Win32;

namespace Microsoft.IronPythonTools.Debugger {
    class IronPythonLauncher : IProjectLauncher {
        private static Process _chironProcess;
        private static string _chironDir;
        private static int _chironPort;
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
                return Path.Combine(path, "NoIronPython.mht");
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

            //if (factory.Id == _cpyInterpreterGuid || factory.Id == _cpy64InterpreterGuid) {
            //    MessageBox.Show(
            //        "The project is currently set to use the .NET debugger for IronPython debugging but the project is configured to start with a CPython interpreter.\r\n\r\nTo fix this change the debugger type in project properties->Debug->Launch mode.\r\nIf IronPython is not an available interpreter you may need to download it from http://ironpython.codeplex.com.",
            //        "Visual Studio");
            //    return VSConstants.S_OK;
            //}

            string extension = Path.GetExtension(config.ScriptName);
            if (string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase)) {
                try {
                    StartSilverlightApp(config, debug);
                } catch (ChironNotFoundException ex) {
                    MessageBox.Show(ex.Message, Strings.ProductTitle);
                }
                return VSConstants.S_OK;
            }

            try {
                if (debug) {
                    if (string.IsNullOrEmpty(config.InterpreterArguments)) {
                        config.InterpreterArguments = "-X:Debug";
                    } else if (config.InterpreterArguments.IndexOf("-X:Debug", StringComparison.InvariantCultureIgnoreCase) < 0) {
                        config.InterpreterArguments = "-X:Debug " + config.InterpreterArguments;
                    }

                    var debugStdLib = _project.GetProperty(IronPythonLauncherOptions.DebugStandardLibrarySetting);
                    bool debugStdLibResult;
                    if (!bool.TryParse(debugStdLib, out debugStdLibResult) || !debugStdLibResult) {
                        string interpDir = config.Interpreter.PrefixPath;
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


        private static Guid? guidSilverlightDebug = new Guid("{032F4B8C-7045-4B24-ACCF-D08C9DA108FE}");

        public void StartSilverlightApp(LaunchConfiguration config, bool debug) {
            var root = Path.GetFullPath(config.WorkingDirectory).TrimEnd('\\');
            var file = Path.Combine(root, config.ScriptName);
            int port = EnsureChiron(root);
            var url = string.Format(
                "http://localhost:{0}/{1}",
                port,
                (file.StartsWith(root + "\\") ? file.Substring(root.Length + 1) : file.TrimStart('\\')).Replace('\\', '/')
            );

            StartInBrowser(url, debug ? guidSilverlightDebug : null);
        }

        public void StartInBrowser(string url, Guid? debugEngine) {
            if (debugEngine.HasValue) {
                // launch via VS debugger, it'll take care of figuring out the browsers
                VsDebugTargetInfo dbgInfo = new VsDebugTargetInfo();
                dbgInfo.dlo = (DEBUG_LAUNCH_OPERATION)_DEBUG_LAUNCH_OPERATION3.DLO_LaunchBrowser;
                dbgInfo.bstrExe = url;
                dbgInfo.clsidCustom = debugEngine.Value;
                dbgInfo.grfLaunch = (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_StopDebuggingOnEnd | (uint)__VSDBGLAUNCHFLAGS4.DBGLAUNCH_UseDefaultBrowser;
                dbgInfo.cbSize = (uint)Marshal.SizeOf(dbgInfo);

                VsShellUtilities.LaunchDebugger(_serviceProvider, dbgInfo);
            } else {
                // run the users default browser
                var handler = GetBrowserHandlerProgId();
                var browserCmd = (string)Registry.ClassesRoot.OpenSubKey(handler).OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command").GetValue("");

                if (browserCmd.IndexOf("%1") != -1) {
                    browserCmd = browserCmd.Replace("%1", url);
                } else {
                    browserCmd = browserCmd + " " + url;
                }
                bool inQuote = false;
                string cmdLine = null;
                for (int i = 0; i < browserCmd.Length; i++) {
                    if (browserCmd[i] == '"') {
                        inQuote = !inQuote;
                    }

                    if (browserCmd[i] == ' ' && !inQuote) {
                        cmdLine = browserCmd.Substring(0, i);
                        break;
                    }
                }
                if (cmdLine == null) {
                    cmdLine = browserCmd;
                }

                Process.Start(cmdLine, browserCmd.Substring(cmdLine.Length));
            }
        }

        private static string GetBrowserHandlerProgId() {
            try {
                return (string)Registry.CurrentUser.OpenSubKey("Software").OpenSubKey("Microsoft").OpenSubKey("Windows").OpenSubKey("CurrentVersion").OpenSubKey("Explorer").OpenSubKey("FileExts").OpenSubKey(".html").OpenSubKey("UserChoice").GetValue("Progid");
            } catch {
                return (string)Registry.ClassesRoot.OpenSubKey(".html").GetValue("");
            }
        }

        private int EnsureChiron(string/*!*/ webSiteRoot) {
            Debug.Assert(!webSiteRoot.EndsWith("\\"));

            if (_chironDir != webSiteRoot && _chironProcess != null && !_chironProcess.HasExited) {
                try {
                    _chironProcess.Kill();
                } catch {
                    // process already exited
                }
                _chironProcess = null;
            }

            if (_chironProcess == null || _chironProcess.HasExited) {
                // start Chiron
                var chironPath = ChironPath;

                // Get a free port
                _chironPort = GetFreePort();

                // TODO: race condition - the port might be taked by the time Chiron attempts to open it
                // TODO: we should wait for Chiron before launching the browser

                string commandLine = "/w:" + _chironPort + " /notification /d:";

                if (webSiteRoot.IndexOf(' ') != -1) {
                    commandLine += "\"" + webSiteRoot + "\"";
                } else {
                    commandLine += webSiteRoot;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo(chironPath, commandLine);
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                _chironDir = webSiteRoot;
                _chironProcess = Process.Start(startInfo);
            }

            return _chironPort;
        }

        private static int GetFreePort() {
            return Enumerable.Range(new Random().Next(1200, 2000), 60000).Except(
                from connection in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections()
                select connection.LocalEndPoint.Port
            ).First();
        }


        public string ChironPath {
            get {
                string result = GetPythonInstallDir();
                if (result != null) {
                    result = Path.Combine(result, @"Silverlight\bin\Chiron.exe");
                    if (File.Exists(result)) {
                        return result;
                    }
                }

                result = Path.Combine(Path.GetDirectoryName(typeof(IronPythonLauncher).Assembly.Location), "Chiron.exe");
                if (File.Exists(result)) {
                    return result;
                }

                throw new ChironNotFoundException();
            }
        }


        internal static string GetPythonInstallDir() {
            using (var ipy = Registry.LocalMachine.OpenSubKey("SOFTWARE\\IronPython")) {
                if (ipy != null) {
                    using (var twoSeven = ipy.OpenSubKey("2.7")) {
                        if (twoSeven != null) {
                            using (var installPath = twoSeven.OpenSubKey("InstallPath")) {
                                var path = installPath.GetValue("") as string;
                                if (path != null) {
                                    return path;
                                }
                            }
                        }
                    }
                }
            }

            var paths = Environment.GetEnvironmentVariable("PATH");
            if (paths != null) {
                foreach (string dir in paths.Split(Path.PathSeparator)) {
                    try {
                        if (IronPythonExistsIn(dir)) {
                            return dir;
                        }
                    } catch {
                        // ignore
                    }
                }
            }

            return null;
        }


        private static bool IronPythonExistsIn(string/*!*/ dir) {
            return File.Exists(Path.Combine(dir, "ipy.exe"));
        }


        [Serializable]
        class ChironNotFoundException : Exception {
            public ChironNotFoundException()
                : this(Strings.IronPythonSilverlightToolsNotFound) {
            }

            public ChironNotFoundException(string message) : base(message) { }
            public ChironNotFoundException(string message, Exception inner) : base(message, inner) { }
            protected ChironNotFoundException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context)
                : base(info, context) { }
        }
    }
}
