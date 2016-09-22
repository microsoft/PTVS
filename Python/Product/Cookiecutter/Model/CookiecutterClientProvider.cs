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
        public ICookiecutterClient Create() {
            var interpreter = FindCompatibleInterpreter();
            if (interpreter != null) {
                return new CookiecutterClient(interpreter);
            }

            return null;
        }

        public bool CompatiblePythonAvailable() {
            return FindCompatibleInterpreter() != null;
        }

        private CookiecutterPythonInterpreter FindCompatibleInterpreter() {
            foreach (var r in GetAvailableInterpreters()) {
                if (r.Item2) {
                    return new CookiecutterPythonInterpreter(r.Item1);
                }
            }

            return null;
        }

        private IEnumerable<Tuple<string, bool>> GetAvailableInterpreters() {
            var res = PythonRegistrySearch.PerformDefaultSearch();
            foreach (var r in res) {
                if (r.Configuration.Version < new Version(3, 3)) {
                    continue;
                }

                yield return Tuple.Create(r.Configuration.InterpreterPath, true);
            }
        }
    }
}
