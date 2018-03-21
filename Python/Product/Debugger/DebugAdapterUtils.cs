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

using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace Microsoft.PythonTools {
    public class DebugAdapterUtils {
        public static void GetProcessInfoFromUri(Uri uri, out int pid, out string processName) {
            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = @"C:\Windows\System32\NETSTAT.EXE",
                Arguments = "-a -n -o -p TCP",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(psi)) {
                process.WaitForExit(250);
                var portStr = $":{uri.Port}";

                var delimiters = new string[] { " " };
                while (!process.StandardOutput.EndOfStream) {
                    var line = process.StandardOutput.ReadLine().ToUpper();
                    var parts = line.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 3
                        && parts[0] == "TCP"
                        && parts[1].EndsWith(portStr)
                        && parts[parts.Length - 2] == "LISTENING") {
                        if (int.TryParse(parts[parts.Length - 1], out pid)) {
                            var proc = Process.GetProcessById(pid);
                            processName = proc.ProcessName;
                            return;
                        }
                    }
                }

                pid = 55555;
                processName = "python";
            }
        }

        public static bool UseExperimentalDebugger() {
            bool defaultValue = true;
            if (!_useExperimental.HasValue) {
                try {
                    var experimentalKey = @"Software\Microsoft\PythonTools\Experimental";
                    using (var root = Registry.CurrentUser.OpenSubKey(experimentalKey, false)) {
                        var value = root?.GetValue("UseVsCodeDebugger", 1);
                        if (value == null) {
                            _useExperimental = defaultValue;
                        } else {
                            int? asInt = value as int?;
                            _useExperimental = asInt.HasValue ? (asInt.GetValueOrDefault() == 1) : defaultValue;
                        }
                    }
                } catch(Exception) {
                    _useExperimental = defaultValue;
                }
            }

            return _useExperimental.Value;
        }
        private static bool? _useExperimental = null;
    }
}
