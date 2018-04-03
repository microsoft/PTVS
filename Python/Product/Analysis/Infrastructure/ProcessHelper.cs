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
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    sealed class ProcessHelper : IDisposable {
        private readonly ProcessStartInfo _psi;
        private Process _process;
        private int? _exitCode;
        private readonly SemaphoreSlim _seenNullOutput, _seenNullError;

        public ProcessHelper(string filename, IEnumerable<string> arguments, string workingDir = null) {
            if (!File.Exists(filename)) {
                throw new FileNotFoundException("Could not launch process", filename);
            }

            _psi = new ProcessStartInfo(
                filename,
                arguments.AsQuotedArguments()
            );
            _psi.WorkingDirectory = workingDir ?? Path.GetDirectoryName(filename);
            _psi.UseShellExecute = false;
            _psi.ErrorDialog = false;
            _psi.CreateNoWindow = true;
            _psi.RedirectStandardInput = true;
            _psi.RedirectStandardOutput = true;
            _psi.RedirectStandardError = true;

            _seenNullOutput = new SemaphoreSlim(1);
            _seenNullError = new SemaphoreSlim(1);
        }

        public ProcessStartInfo StartInfo => _psi;
        public string FileName => _psi.FileName;
        public string Arguments => _psi.Arguments;

        public Action<string> OnOutputLine { get; set; }
        public Action<string> OnErrorLine { get; set; }

        public void Dispose() {
            _seenNullOutput.Dispose();
            _seenNullError.Dispose();
            _process?.Dispose();
        }

        public void Start() {
            _seenNullOutput.Wait(0);
            _seenNullError.Wait(0);

            var p = new Process {
                StartInfo = _psi
            };

            p.OutputDataReceived += Process_OutputDataReceived;
            p.ErrorDataReceived += Process_ErrorDataReceived;

            try {
                p.Start();
            } catch (Exception ex) {
                // Capture the error as stderr and exit code, then
                // clean up.
                _exitCode = ex.HResult;
                OnErrorLine?.Invoke(ex.ToString());
                _seenNullError.Release();
                _seenNullOutput.Release();
                p.OutputDataReceived -= Process_OutputDataReceived;
                p.ErrorDataReceived -= Process_ErrorDataReceived;
                return;
            }

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            p.EnableRaisingEvents = true;

            // Close stdin so that if the process tries to read it will exit
            p.StandardInput.Close();

            _process = p;
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e) {
            try {
                if (e.Data == null) {
                    _seenNullError.Release();
                } else {
                    OnErrorLine?.Invoke(e.Data.TrimEnd());
                }
            } catch (ObjectDisposedException) {
                ((Process)sender).ErrorDataReceived -= Process_ErrorDataReceived;
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            try {
                if (e.Data == null) {
                    _seenNullOutput.Release();
                } else {
                    OnOutputLine?.Invoke(e.Data.TrimEnd());
                }
            } catch (ObjectDisposedException) {
                ((Process)sender).OutputDataReceived -= Process_OutputDataReceived;
            }
        }

        public void Kill() {
            try {
                if (_process != null && !_process.HasExited) {
                    _process.Kill();
                }
            } catch (SystemException) {
            }
        }

        public int? Wait(int milliseconds) {
            if (_exitCode != null) {
                return _exitCode;
            }
            var cts = new CancellationTokenSource(milliseconds);
            try {
                var t = WaitAsync(cts.Token);
                try {
                    t.Wait(cts.Token);
                    return t.Result;
                } catch (AggregateException ae) when (ae.InnerException != null) {
                    throw ae.InnerException;
                }
            } catch (OperationCanceledException) {
                return null;
            }
        }

        public async Task<int> WaitAsync(CancellationToken cancellationToken) {
            if (_exitCode != null) {
                return _exitCode.Value;
            }
            if (_process == null) {
                throw new InvalidOperationException("Process was not started");
            }

            if (!_process.HasExited) {
                await _seenNullOutput.WaitAsync(cancellationToken).ConfigureAwait(false);
                await _seenNullError.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            for (var i = 0; i < 5 && !_process.HasExited; i++) {
                await Task.Delay(100);
            }

            Debug.Assert(_process.HasExited, "Process still has not exited.");
            return _process.ExitCode;
        }
    }
}
