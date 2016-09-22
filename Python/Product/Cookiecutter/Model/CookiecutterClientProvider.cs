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
using System.Linq;
using Microsoft.CookiecutterTools.Interpreters;

namespace Microsoft.CookiecutterTools.Model {
    static class CookiecutterClientProvider {
        public static ICookiecutterClient Create() {
            var interpreter = FindCompatibleInterpreter();
            if (interpreter != null) {
                return new CookiecutterClient(interpreter);
            }

            return null;
        }

        public static bool IsCompatiblePythonAvailable() {
            return FindCompatibleInterpreter() != null;
        }

        private static CookiecutterPythonInterpreter FindCompatibleInterpreter() {
            var interpreters = PythonRegistrySearch.PerformDefaultSearch();
            var compatible = interpreters.Where(interp => interp.Configuration.Version >= new Version(3, 3)).FirstOrDefault();
            return compatible != null ? new CookiecutterPythonInterpreter(compatible.Configuration.InterpreterPath) : null;
        }
    }
}
