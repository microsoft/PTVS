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
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Debugger.Interop.VSCodeDebuggerHost;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.Debugger {
    public sealed class DebugAdapterProcess : ITargetHostProcess {
        private const int _debuggerConnectionTimeout = 5000; // 5000 ms
        private const int _connectionCloseTimeout = 5000; // 5000 ms

        private Process _process;
        private readonly Guid _processGuid;
        private PythonDebugOptions _debugOptions;
        private string _interpreterOptions;
        private int _listenerPort = -1;
        private Stream _stream;
        private Socket _socket;
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

            List<string> argsList = new List<string> {
                string.IsNullOrWhiteSpace(_interpreterOptions) ? "" : _interpreterOptions,
                PythonToolsInstallPath.GetFile("ptvsd_launcher.py").AddQuotes(),
                cwd.Trim('\\').AddQuotes(),
                _listenerPort.ToString(),
                _processGuid.ToString(),
                _debugOptions.ToString().AddQuotes(),
                "-g",
                args
            };
            var arguments = string.Join(" ", argsList);

            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = exe,
                Arguments = arguments,
                WorkingDirectory = cwd,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            var env = json["env"].Value<JArray>();
            if (env.Count > 0) {
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
            if (!_connectedEvent.WaitOne(_debuggerConnectionTimeout)){
                Debug.WriteLine("Timed out waiting for debuggee to connect.", nameof(DebugAdapterProcess));
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e) {
            Debug.WriteLine($"Debug adapter stderr: {e.Data}", nameof(DebugAdapterProcess));
        }

        private void OnExited(object sender, EventArgs e) {
            Debug.WriteLine($"Debug adapter exited with code: {_process.ExitCode}", nameof(DebugAdapterProcess));
            _socket?.Close(_connectionCloseTimeout);
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
            var socketSource = ((Socket)iar.AsyncState);
            try {
                _socket = socketSource.EndAccept(iar);
            } catch (SocketException ex) {
                Debug.WriteLine("Debug Adapter Host socket failed");
                Debug.WriteLine(ex);
            } catch (ObjectDisposedException) {
                Debug.WriteLine("Debug Adapter Host socket closed");
            }

            if (_socket != null) {
                _stream = new NetworkStream(_socket, ownsSocket: true);
                _connectedEvent.Set();
            }
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
                    }
                }
            }
        }
    }
}
