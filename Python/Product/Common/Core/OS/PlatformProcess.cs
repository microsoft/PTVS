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

namespace Microsoft.PythonTools.Common.Core.OS {
    public sealed class PlatformProcess : IProcess {
        public Process Process { get; private set; }

        private readonly IProcessServices _ps;
        public PlatformProcess(IProcessServices ps, Process process) {
            _ps = ps;
            Process = process;
        }

        public int Id => Process.Id;
        public StreamWriter StandardInput => Process.StandardInput;
        public StreamReader StandardOutput => Process.StandardOutput;
        public StreamReader StandardError => Process.StandardError;
        public bool HasExited => Process.HasExited;
        public int ExitCode => Process.ExitCode;

        public event EventHandler Exited {
            add {
                Process.EnableRaisingEvents = true;
                Process.Exited += value;
            }
            remove => Process.Exited -= value;
        }

        public bool WaitForExit(int milliseconds) => Process.WaitForExit(milliseconds);
        public void Dispose() => Process.Dispose();
    }
}
