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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Debugger {
    sealed class DebugAdapterProcess : ITargetHostProcess, IDisposable {
        private const int _debuggerConnectionTimeout = 20000; // 20 seconds

        private Process _process;
        private readonly Guid _processGuid;
        private PythonDebugOptions _debugOptions;
        private string _webBrowserUrl;
        private string _interpreterOptions;
        private int _listenerPort = -1;
        private DebugAdapterProcessStream _stream;
        private bool _debuggerConnected = false;

        public DebugAdapterProcess() {
            _processGuid = Guid.NewGuid();
        }

        public void Dispose() {
            if (_stream != null) {
                _stream.Dispose();
            }
            if (_process != null) {
                try {
                    if (!_process.HasExited) {
                        _process.Kill();
                    }
                } catch (InvalidOperationException) {
                } catch (Win32Exception) {
                }
                _process.Dispose();
            }
        }

        public static ITargetHostProcess Start(string launchJson) {
            var debugProcess = new DebugAdapterProcess();
            debugProcess.StartProcess(launchJson);
            return debugProcess;
        }

        private void StartProcess(string launchJson) {
            var connection = InitializeListenerSocket();

            var json = JObject.Parse(launchJson);
            var exe = json["exe"].Value<string>();
            var scriptAndScriptArgs = json["args"].Value<string>();
            var cwd = json["cwd"].Value<string>();
            ParseOptions(json["options"].Value<string>());

            var argsList = new List<string> {
                string.IsNullOrWhiteSpace(_interpreterOptions) ? "" : _interpreterOptions,
                PythonToolsInstallPath.GetFile("ptvsd_launcher.py"),
                cwd.Trim('\\'),
                $"{_listenerPort}",
                $"{_processGuid}",
                $"{_debugOptions}",
                "-g"
            };
            var launcherArgs = string.Join(" ", argsList.Where(a => !string.IsNullOrWhiteSpace(a)).Select(ProcessOutput.QuoteSingleArgument));
            var arguments = $"{launcherArgs} {scriptAndScriptArgs}";

            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = exe,
                Arguments = arguments,
                WorkingDirectory = cwd,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            var env = json["env"].Value<JArray>();
            foreach (JObject curValue in env) {
                var name = curValue["name"].Value<string>();
                var value = curValue["value"].Value<string>();
                if (!string.IsNullOrWhiteSpace(name)) {
                    psi.EnvironmentVariables[name] = value;
                }
            }

            _process = new Process {
                EnableRaisingEvents = true,
                StartInfo = psi
            };

            _process.Exited += OnExited;
            _process.Start();

            try {
                if (connection.Wait(_debuggerConnectionTimeout)) {
                    var socket = connection.Result;
                    if (socket != null) {
                        _debuggerConnected = true;
                        _stream = new DebugAdapterProcessStream(new NetworkStream(connection.Result, ownsSocket: true));
                        _stream.Initialized += OnInitialized;
                        if (!string.IsNullOrEmpty(_webBrowserUrl) && Uri.TryCreate(_webBrowserUrl, UriKind.RelativeOrAbsolute, out Uri uri)) {
                            OnPortOpenedHandler.CreateHandler(uri.Port, null, null, ProcessExited, LaunchBrowserDebugger);
                        }
                    }
                } else {
                    Debug.WriteLine("Timed out waiting for debuggee to connect.", nameof(DebugAdapterProcess));
                }
            } catch (AggregateException ex) {
                Debug.WriteLine("Error waiting for debuggee to connect {0}".FormatInvariant(ex.InnerException ?? ex), nameof(DebugAdapterProcess));
            }

            if (_stream == null && !_process.HasExited) {
                _process.Kill();
            }
        }

        private void OnInitialized(object sender, EventArgs e) {
            CustomDebugAdapterProtocolExtension.SendRequest(
                new PtvsdVersionRequest(),
                PtvsdVersionHelper.VerifyPtvsdVersion,
                PtvsdVersionHelper.VerifyPtvsdVersionError);
        }

        private void LaunchBrowserDebugger() {
            var vsDebugger = (IVsDebugger2)VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService(typeof(SVsShellDebugger));

            VsDebugTargetInfo2 info = new VsDebugTargetInfo2();
            var infoSize = Marshal.SizeOf(info);
            info.cbSize = (uint)infoSize;
            info.bstrExe = _webBrowserUrl;
            info.dlo = (uint)_DEBUG_LAUNCH_OPERATION3.DLO_LaunchBrowser;
            info.LaunchFlags = (uint)__VSDBGLAUNCHFLAGS4.DBGLAUNCH_UseDefaultBrowser | (uint)__VSDBGLAUNCHFLAGS.DBGLAUNCH_NoDebug;
            IntPtr infoPtr = Marshal.AllocCoTaskMem(infoSize);
            Marshal.StructureToPtr(info, infoPtr, false);

            try {
                vsDebugger.LaunchDebugTargets2(1, infoPtr);
            } finally {
                if (infoPtr != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(infoPtr);
                }
            }
        }

        private bool ProcessExited() {
            var process = _process;
            if (process != null) {
                return process.HasExited;
            }
            return true;
        }

        private void OnExited(object sender, EventArgs e) {
            Debug.WriteLine($"Debug adapter exited with code: {_process.ExitCode}", nameof(DebugAdapterProcess));
            if (_stream != null) {
                _stream.Dispose();
            }

            if (_process.ExitCode == 126 && !_debuggerConnected) {
                // 126 : ERROR_MOD_NOT_FOUND
                // This error code is returned only for the experimental debugger. MessageBox must be
                // bound to the VS Main window otherwise it can be hidden behind the main window and the 
                // user may not see it.
                MessageBox.Show(
                    new Win32Window(Process.GetCurrentProcess().MainWindowHandle),
                    Strings.ImportPtvsdFailedMessage,
                    Strings.ImportPtvsdFailedTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public IntPtr Handle => _process.Handle;

        public Stream StandardInput => _stream;

        public Stream StandardOutput => _stream;

        public bool HasExited => _process.HasExited;

        public event EventHandler Exited {
            add => _process.Exited += value;
            remove => _process.Exited -= value;
        }

        public event DataReceivedEventHandler ErrorDataReceived {
            add => _process.ErrorDataReceived += value;
            remove => _process.ErrorDataReceived -= value;
        }

        public void Terminate() => _process.Kill();

        private Task<Socket> InitializeListenerSocket() {
            if (_listenerPort < 0) {
                var socketSource = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                socketSource.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                socketSource.Listen(0);
                _listenerPort = ((IPEndPoint)socketSource.LocalEndPoint).Port;
                Debug.WriteLine("Listening for debug connections on port {0}", _listenerPort);
                return Task.Factory.FromAsync(socketSource.BeginAccept, socketSource.EndAccept, null);
            }

            return Task.FromResult((Socket)null);
        }

        

        /***************************************************************************************************************
        * The code below should go into refactored PythonDebugOptions class. See AD7Engine ParseOptions for more info
        ****************************************************************************************************************/


        private static string[] SplitOptions(string options) {
            List<string> res = new List<string>();
            int lastStart = 0;
            for (int i = 0; i < options.Length; i++) {
                if (options[i] == ';') {
                    if (i < options.Length - 1 && options[i + 1] != ';') {
                        // valid option boundary
                        res.Add(options.Substring(lastStart, i - lastStart));
                        lastStart = i + 1;
                    } else {
                        i++;
                    }
                }
            }
            if (options.Length - lastStart > 0) {
                res.Add(options.Substring(lastStart, options.Length - lastStart));
            }
            return res.ToArray();
        }

        private void ParseOptions(string options) {
            foreach (var optionSetting in SplitOptions(options)) {
                var setting = optionSetting.Split(new[] { '=' }, 2);
                if (setting.Length == 2) {
                    switch (setting[0]) {
                        case AD7Engine.WaitOnAbnormalExitSetting:
                            bool value;
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.WaitOnAbnormalExit;
                            }
                            break;

                        case AD7Engine.WaitOnNormalExitSetting:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.WaitOnNormalExit;
                            }
                            break;

                        case AD7Engine.RedirectOutputSetting:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.RedirectOutput;
                            }
                            break;

                        case AD7Engine.BreakSystemExitZero:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.BreakOnSystemExitZero;
                            }
                            break;

                        case AD7Engine.DebugStdLib:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.DebugStdLib;
                            }
                            break;

                        case AD7Engine.IsWindowsApplication:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.IsWindowsApplication;
                            }
                            break;

                        case AD7Engine.InterpreterOptions:
                            _interpreterOptions = setting[1];
                            break;

                        case AD7Engine.AttachRunning:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.AttachRunning;
                            }
                            break;

                        case AD7Engine.EnableDjangoDebugging:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.DjangoDebugging;
                            }
                            break;
                        case AD7Engine.WebBrowserUrl:
                            _webBrowserUrl = HttpUtility.UrlDecode(setting[1]);
                            break;
                    }
                }
            }
        }
    }
}
