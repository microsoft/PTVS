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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Policy;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Profiling {
    sealed class ETWProfiledProcess : IDisposable {
        private readonly string _interpreterPath, _scriptArguments, _projectDirectory, _etwTracePackageDirectory;
        private readonly Dictionary<string, string> _envVars;
        private Process _process;

        public event EventHandler ProcessExited;
        public ETWProfiledProcess(string exe, string args, string dir, Dictionary<string, string> envVars) {

            if (string.IsNullOrEmpty(exe)) {
                // TODO: Localize the error message.
                throw new ArgumentNullException("Interpreter path cannot be null or empty", nameof(exe));
            }

            var etwTracePackageDirectory = Path.GetDirectoryName(PythonToolsInstallPath.GetFile("etwtrace\\__init__.py", typeof(ETWProfiledProcess).Assembly));
            var parentDirectory = Path.GetDirectoryName(etwTracePackageDirectory);
            _etwTracePackageDirectory = parentDirectory;

            _interpreterPath = exe;
            _scriptArguments = args ?? string.Empty;
            _projectDirectory = string.IsNullOrWhiteSpace(dir) ? "." : PathUtils.TrimEndSeparator(dir);
            _envVars = envVars ?? new Dictionary<string, string>();
        }


        public void Dispose() {
            _process.Dispose();
        }

        public void StartProfiling() {

            // Validate interpreter path
            if (!File.Exists(_interpreterPath)) {
                // TODO: Localize the error message.
                throw new FileNotFoundException($"Python interpreter not found at path: {_interpreterPath}");
            }

            var startInfo = new ProcessStartInfo {
                FileName = _interpreterPath,
                Arguments = $"-m etwtrace --diaghub -- {_scriptArguments}",
                // Arguments = $"-m etwtrace --capture output.etl -- {_scriptArguments}", // Used for testing
                // Arguments = _scriptArguments, // Used for testing
                WorkingDirectory = _projectDirectory,
                UseShellExecute = false,
                // UseShellExecute = true, // Used for testing
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // Verb = "runas" // Request admin privileges, used for testing.
            };

            // Add environment variables
            if (_envVars != null) {
                foreach (var keyValue in _envVars) {
                    startInfo.EnvironmentVariables[keyValue.Key] = keyValue.Value;
                }
            }


            // Add PYTHONPATH to locate the etwtrace module
            if (!string.IsNullOrEmpty(_etwTracePackageDirectory)) {
                string existingPythonPath = string.Empty;
                if (startInfo.EnvironmentVariables.ContainsKey("PYTHONPATH")) {
                    existingPythonPath = startInfo.EnvironmentVariables["PYTHONPATH"];
                }

                // Append the etwtrace directory to the existing PYTHONPATH
                startInfo.EnvironmentVariables["PYTHONPATH"] = string.IsNullOrEmpty(existingPythonPath)
                    ? _etwTracePackageDirectory
                    : $"{existingPythonPath};{_etwTracePackageDirectory}";
            }


            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _process.Exited += (sender, args) => ProcessExited?.Invoke(this, EventArgs.Empty);

            // Used for testing.
            //_process.OutputDataReceived += (sender, e) => {
            //    if (!string.IsNullOrEmpty(e.Data)) {
            //        Debug.WriteLine($"[StdOut]: {e.Data}");
            //    }
            //};

            //_process.ErrorDataReceived += (sender, e) => {
            //    if (!string.IsNullOrEmpty(e.Data)) {
            //        Debug.WriteLine($"PYTHONPATH: {startInfo.EnvironmentVariables["PYTHONPATH"]}");
            //        Debug.WriteLine($"[StdErr]: {e.Data}");
            //    }
            //};


            try {
                _process.Start();
                //_process.BeginOutputReadLine();
                //_process.BeginErrorReadLine();
            } catch (Exception ex) {
                // TODO: Localize the error message.
                throw new InvalidOperationException("Failed to start the profiler process.", ex);
            }
        }

        internal void StopProfiling() {
            if (_process != null && !_process.HasExited) {
                _process.Kill();
            }
        }

        public string GetStandardOutput() {
            return _process?.StandardOutput.ReadToEnd();
        }

        public bool HasExited => _process?.HasExited ?? true;
    }
}
