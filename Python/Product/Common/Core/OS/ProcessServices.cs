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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Core.OS {
    public sealed class ProcessServices : IProcessServices {
        public IProcess Start(ProcessStartInfo psi) {
            var process = Process.Start(psi);
            return process != null ? new PlatformProcess(this, process) : null;
        }

        public IProcess Start(string path) {
            var process = Process.Start(path);
            return process != null ? new PlatformProcess(this, process) : null;
        }

        public void Kill(IProcess process) => Kill(process.Id);
        public void Kill(int pid) => Process.GetProcessById(pid).Kill();
        public bool IsProcessRunning(string processName) => Process.GetProcessesByName(processName).Any();

        public async Task<string> ExecuteAndCaptureOutputAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default) {
            var output = string.Empty;
            try {
                using (var process = Start(startInfo)) {

                    if (startInfo.RedirectStandardError && process is PlatformProcess p) {
                        p.Process.ErrorDataReceived += (s, e) => { };
                        p.Process.BeginErrorReadLine();
                    }

                    try {
                        output = await process.StandardOutput.ReadToEndAsync();
                        await process.WaitForExitAsync(30000, cancellationToken);
                    } catch (IOException) { } catch (OperationCanceledException) { }

                    return output;
                }
            // Handle the case when trying to call python from the Microsoft store and we get access denied
            } catch (Win32Exception) {
                return output;
            }
        }
    }
}
