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
                // No service provider is available (e.g. when called from tests).
                // Fall back to enumerating Python installations directly from the
                // registry so we can still find a compatible interpreter.
                return FindCompatibleInterpreterFromRegistry();
            }

            var compModel = provider.GetService(typeof(SComponentModel)) as IComponentModel;
            var interpreters = compModel?.GetService<IInterpreterRegistryService>();
            if (interpreters == null) {
                return null;
            }

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
                var companyObj = interpreters.GetProperty(configuration.Id, "Company");
                if (companyObj != null && 
                    companyObj.ToString().IndexOfOrdinal("Python Software Foundation", ignoreCase: true) == 0) {
                    cpython.Add(configuration);
                }
            }          

            var best = cpython.FirstOrDefault() ?? all.FirstOrDefault();

            return best != null ? new CookiecutterPythonInterpreter(
                best.GetPrefixPath(),
                best.InterpreterPath
            ) : null;
        }

        private static CookiecutterPythonInterpreter FindCompatibleInterpreterFromRegistry() {
            // Enumerate Python installations directly from the registry. This path
            // is used when no IServiceProvider is available (for example, in unit
            // tests), where the MEF-based IInterpreterRegistryService can't be
            // resolved.
            var all = PythonRegistrySearch.PerformDefaultSearch()
                .Where(info => File.Exists(info.Configuration.InterpreterPath))
                .Where(info => info.Configuration.Version >= new Version(3, 5))
                .OrderByDescending(info => info.Configuration.Version)
                .ToList();

            // Prefer a CPython installation if there is one because
            // some Anaconda installs have trouble creating a venv.
            var cpython = all
                .Where(info => info.Vendor != null &&
                    info.Vendor.IndexOfOrdinal("Python Software Foundation", ignoreCase: true) == 0)
                .ToList();

            var best = cpython.FirstOrDefault() ?? all.FirstOrDefault();

            return best != null ? new CookiecutterPythonInterpreter(
                best.Configuration.GetPrefixPath(),
                best.Configuration.InterpreterPath
            ) : null;
        }
    }
}
