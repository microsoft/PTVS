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
using System.IO;
using System.Linq;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;

namespace Microsoft.CookiecutterTools.Model {
    static class CookiecutterClientProvider {

        public static ICookiecutterClient Create(IServiceProvider provider, Redirector redirector) {

            var interpreter = FindCompatibleInterpreter(provider);
            if (interpreter != null) {
                return new CookiecutterClient(provider, interpreter, redirector);
            }

            return null;
        }

        public static bool IsCompatiblePythonAvailable(IServiceProvider provider) {
            return FindCompatibleInterpreter(provider) != null;
        }

        private static CookiecutterPythonInterpreter FindCompatibleInterpreter(IServiceProvider provider) {

            if (provider == null) {
                return null;
            }

            var compModel = provider.GetService(typeof(SComponentModel)) as IComponentModel;
            var interpreters = compModel.GetService<IInterpreterRegistryService>();

            var all = interpreters.Configurations
                .Where(x => File.Exists(x.InterpreterPath))
                .Where(x => x.Version >= new Version(3, 5))
                .OrderByDescending(x => x.Version)
                .ToList();

            // Prefer a CPython installation if there is one because
            // some Anaconda installs have trouble creating a venv.
            // The linq for this is a little messy, so I'm just using a normal loop.
            var cpython = new List<InterpreterConfiguration>();
            foreach (var configuration in all) {
                var company = interpreters.GetProperty(configuration.Id, "Company").ToString();
                if (company.IndexOfOrdinal("Python Software Foundation", ignoreCase: true) == 0) {
                    cpython.Add(configuration);
                }
            }          
            
            var best = cpython.FirstOrDefault() ?? all.FirstOrDefault();

            return best != null ? new CookiecutterPythonInterpreter(
                best.GetPrefixPath(),
                best.InterpreterPath
            ) : null;
        }
    }
}
