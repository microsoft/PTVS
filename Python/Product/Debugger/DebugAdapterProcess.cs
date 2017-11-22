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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Debugger.Interop.VSCodeDebuggerHost;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Debugger {
    public sealed class DebugAdapterProcess : ITargetHostProcess {
        private Process _process;
        private readonly Guid _processGuid;
        private PythonDebugOptions _debugOptions;
        private string _interpreterOptions;
        private int _listenerPort = -1;
        private Stream _stream;
        private ManualResetEvent _connectedEvent = new ManualResetEvent(false);

        public DebugAdapterProcess() {
            _processGuid = Guid.NewGuid();
        }

        public static DebugAdapterProcess Start(string launchJson) {
            var debugProcess = new DebugAdapterProcess();
            debugProcess.StartProcess(launchJson);
            return debugProcess;
        }
        private void StartProcess(string launchJson) {
            InitializeListenerSocket();

            var json = JObject.Parse(launchJson);
            var exe = json["exe"].Value<string>();
            var args = json["args"].Value<string>();
            var cwd = json["cwd"].Value<string>();
            ParseOptions(json["options"].Value<string>());

            var arguments = (String.IsNullOrWhiteSpace(_interpreterOptions) ? "" : (_interpreterOptions + " ")) +
                "\"" + PythonToolsInstallPath.GetFile("ptvsd_launcher.py") + "\" " +
                "\"" + cwd + "\" " +
                " " + _listenerPort + " " +
                " " + _processGuid + " " +
                "\"" + _debugOptions + "\" " +
                "-g " +
                args;

            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = cwd,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            var env = json["env"].Value<JArray>();
            if (env.Count>0) {
                foreach (JObject curValue in env) {
                    var name = curValue["name"].Value<string>();
                    var value = curValue["value"].Value<string>();
                    if (!string.IsNullOrWhiteSpace(name)) {
                        psi.EnvironmentVariables[name] = value;
                    }
                }
            }

            _process = new Process {
                EnableRaisingEvents = true,
                StartInfo = psi
            };
            _process.Exited += OnExited;
            _process.ErrorDataReceived += OnErrorDataReceived;
            _process.Start();
            _process.BeginErrorReadLine();
            if (!_connectedEvent.WaitOne()){
                Debug.WriteLine("Connection with debugger timedout.");
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e) {
            Debug.WriteLine("Adapter process error: {0}", e.Data);
        }

        private void OnExited(object sender, EventArgs e) {
            Debug.WriteLine("Python process exited with code: {0}", _process.ExitCode);
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

        private void InitializeListenerSocket() {
            if (_listenerPort < 0) {
                var socketSource = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                socketSource.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                socketSource.Listen(0);
                _listenerPort = ((IPEndPoint)socketSource.LocalEndPoint).Port;
                Debug.WriteLine("Listening for debug connections on port {0}", _listenerPort);
                socketSource.BeginAccept(AcceptConnection, socketSource);
            }
        }

        private void AcceptConnection(IAsyncResult iar) {
            Socket socket = null;
            var socketSource = ((Socket)iar.AsyncState);
            try {
                socket = socketSource.EndAccept(iar);
            } catch (SocketException ex) {
                Debug.WriteLine("DebugConnectionListener socket failed");
                Debug.WriteLine(ex);
            } catch (ObjectDisposedException) {
                Debug.WriteLine("DebugConnectionListener socket closed");
            }

            if (socket != null) {
                _stream = new NetworkStream(socket, ownsSocket: true);
                _connectedEvent.Set();
            }
            socketSource.BeginAccept(AcceptConnection, socketSource);
        }

        /***************************************************************************************************************
        * Thew code below should go into refactored PythonDebugOptions class
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


        /// <summary>
        /// Specifies whether the process should prompt for input before exiting on an abnormal exit.
        /// </summary>
        public const string WaitOnAbnormalExitSetting = "WAIT_ON_ABNORMAL_EXIT";

        /// <summary>
        /// Specifies whether the process should prompt for input before exiting on a normal exit.
        /// </summary>
        public const string WaitOnNormalExitSetting = "WAIT_ON_NORMAL_EXIT";

        /// <summary>
        /// Specifies if the output should be redirected to the visual studio output window.
        /// </summary>
        public const string RedirectOutputSetting = "REDIRECT_OUTPUT";

        /// <summary>
        /// Specifies if the debugger should break on SystemExit exceptions with an exit code of zero.
        /// </summary>
        public const string BreakSystemExitZero = "BREAK_SYSTEMEXIT_ZERO";

        /// <summary>
        /// Specifies if the debugger should step/break into std lib code.
        /// </summary>
        public const string DebugStdLib = "DEBUG_STDLIB";

        /// <summary>
        /// Specifies if the debugger should treat the application as if it doesn't have a console.
        /// </summary>
        /// <remarks>
        /// Currently, the only effect this has is suppressing <see cref="WaitOnAbnormalExitSetting"/>
        /// and <see cref="WaitOnNormalExitSetting"/> if they're set.
        /// </remarks>
        public const string IsWindowsApplication = "IS_WINDOWS_APPLICATION";

        /// <summary>
        /// Specifies options which should be passed to the Python interpreter before the script.  If
        /// the interpreter options should include a semicolon then it should be escaped as a double
        /// semi-colon.
        /// </summary>
        public const string InterpreterOptions = "INTERPRETER_OPTIONS";

        public const string AttachRunning = "ATTACH_RUNNING";

        /// <summary>
        /// True if Django debugging is enabled.
        /// </summary>
        public const string EnableDjangoDebugging = "DJANGO_DEBUG";

        private void ParseOptions(string options) {
            foreach (var optionSetting in SplitOptions(options)) {
                var setting = optionSetting.Split(new[] { '=' }, 2);
                if (setting.Length == 2) {
                    switch (setting[0]) {
                        case WaitOnAbnormalExitSetting:
                            bool value;
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.WaitOnAbnormalExit;
                            }
                            break;

                        case WaitOnNormalExitSetting:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.WaitOnNormalExit;
                            }
                            break;

                        case RedirectOutputSetting:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.RedirectOutput;
                            }
                            break;

                        case BreakSystemExitZero:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.BreakOnSystemExitZero;
                            }
                            break;

                        case DebugStdLib:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.DebugStdLib;
                            }
                            break;

                        case IsWindowsApplication:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.IsWindowsApplication;
                            }
                            break;

                        case InterpreterOptions:
                            _interpreterOptions = setting[1];
                            break;

                        case AttachRunning:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.AttachRunning;
                            }
                            break;

                        case EnableDjangoDebugging:
                            if (Boolean.TryParse(setting[1], out value) && value) {
                                _debugOptions |= PythonDebugOptions.DjangoDebugging;
                            }
                            break;
                    }
                }
            }
        }
    }
}
