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
using Microsoft.VisualStudio.Debugger.Interop.VSCodeDebuggerHost;

namespace Microsoft.PythonTools.Debugger {
    public sealed class DebugAdapterProcess : ITargetHostProcess {
        private readonly Process _process;
        private DebugAdapterProcess(ProcessStartInfo psi) {
            _process = new Process {
                EnableRaisingEvents = true,
                StartInfo = psi
            };
            _process.Start();
            _process.BeginErrorReadLine();
        }

        public static DebugAdapterProcess Start(string exe, string args = "", string workingDir = "") {
            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = workingDir,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            return new DebugAdapterProcess(psi);
        }

        public IntPtr Handle => _process.Handle;

        public Stream StandardInput => _process.StandardInput.BaseStream;

        public Stream StandardOutput => _process.StandardOutput.BaseStream;

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
    }
}
