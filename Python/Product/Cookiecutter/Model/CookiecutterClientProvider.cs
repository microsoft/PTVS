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
using Microsoft.CookiecutterTools.Interpreters;
using Microsoft.CookiecutterTools.Infrastructure;
using System.Collections.Generic;

namespace Microsoft.CookiecutterTools.Model {
    class CookiecutterClientProvider {
        public ICookiecutterClient Create(bool useEmbedded) {
            var interpreter = useEmbedded ? FindEmbeddedInterpreter() : FindCompatibleInterpreter();
            if (interpreter != null) {
                return new CookiecutterClient(interpreter);
            }

            return null;
        }

        public bool CheckDependencies(out bool missingPython, out bool missingCookiecutter) {
            missingPython = true;
            missingCookiecutter = true;

            foreach (var r in GetAvailableInterpreters()) {
                missingPython = false;
                if (r.Item2) {
                    missingCookiecutter = false;
                    break;
                }
            }

            return missingPython || missingCookiecutter;
        }

        private CookiecutterPythonInterpreter FindCompatibleInterpreter() {
            foreach (var r in GetAvailableInterpreters()) {
                if (r.Item2) {
                    return new CookiecutterPythonInterpreter(r.Item1);
                }
            }

            return null;
        }

        private CookiecutterPythonInterpreter FindEmbeddedInterpreter() {
            var path = PythonToolsInstallPath.TryGetFile(@"python-3.5.1-embed-win32\python.exe");
            if (!string.IsNullOrEmpty(path)) {
                return new CookiecutterPythonInterpreter(path);
            }

            return null;
        }

        private IEnumerable<Tuple<string, bool>> GetAvailableInterpreters() {
            var res = PythonRegistrySearch.PerformDefaultSearch();
            foreach (var r in res) {
                var testResult = Check(r.Configuration.InterpreterPath);
                yield return Tuple.Create(r.Configuration.InterpreterPath, testResult == "ok");
            }
        }

        private string Check(string interpreterPath) {
            var checkScript = PythonToolsInstallPath.GetFile("cookiecutter_check.py");
            return RunPythonScript(interpreterPath, checkScript, "");
        }

        private string RunPythonScript(string interpreterPath, string script, string parameters) {
            var psi = new ProcessStartInfo(interpreterPath, string.Format("\"{0}\" {1}", script, parameters));
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            var proc = Process.Start(psi);
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();

            return output;
        }
    }
}
