// Python Tools for Visual Studio
// Copyright(c) 2018 Intel Corporation.  All rights reserved.
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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver {

    class ProcessAsyncRunner {

        public static Task<Process> Run(string execfname,
                                        string args = null,
                                        CancellationToken cancellationToken = default(CancellationToken),
                                        IProgress<ProcessProgressDataload> progress = null
                                        ) {

            TaskCompletionSource<Process> taskCS = new TaskCompletionSource<Process>();
            bool stdoutToProgress = (progress != null);

            ProcessStartInfo psi = new ProcessStartInfo(execfname) {
                UseShellExecute = false,
                Arguments = args,
                CreateNoWindow = false,
                RedirectStandardOutput = stdoutToProgress
            };
            psi.EnvironmentVariables["AMPLXE_EXPERIMENTAL"] = "time-cl";

            Process process = new Process {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            if (progress != null) {
                process.OutputDataReceived += (sender, localEventArgs) => {
                    if (localEventArgs.Data == null) {
                        taskCS.SetResult(process);
                    } else {
                        ProcessProgressDataload e = new ProcessProgressDataload { Message = localEventArgs.Data };
                        progress.Report(e);
                    }
                };

                process.ErrorDataReceived += (sender, localEventArgs) => {
                    Console.WriteLine($"Encountered error while running inferior process: {localEventArgs.Data}");
                    // TODO: should probably cancel
                };
            }

            if (cancellationToken.IsCancellationRequested) {
                cancellationToken.ThrowIfCancellationRequested();
            }

            process.Start();

            if (progress != null) {
                process.BeginOutputReadLine();
            }
            cancellationToken.Register(() => {
                process.CloseMainWindow();
                cancellationToken.ThrowIfCancellationRequested();
            });

            return taskCS.Task;
        }

        public static void RunWrapper(string exe, string args) {
            CancellationTokenSource cts = new CancellationTokenSource();
            var progress = new Progress<ProcessProgressDataload>();
            progress.ProgressChanged += (s, e) => {
                //Console.SetCursorPosition(Console.CursorTop + 2, 0);
                Console.WriteLine($"From process: {e.Message}");
            };

            try {
                Task<Process> t = ProcessAsyncRunner.Run(exe, args, cts.Token, progress);

                bool processRunning = true;
                t.ContinueWith((p) => { processRunning = false; });

                while (processRunning) {
                    // TODO: Find a less obstrusive way to report (that works on VSCode channels and console stdout)
                    // Console.Write(".");
                }
                t.Wait();
            } catch (OperationCanceledException ce) {
                Console.WriteLine($"Operation was cancelled with message: {ce.Message}");
            }
        }
    }

    class ProcessProgressDataload {
        public string Message { get; set; }
    }

}