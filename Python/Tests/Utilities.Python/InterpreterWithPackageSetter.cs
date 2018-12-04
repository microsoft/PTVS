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
using System.IO;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities.UI;

namespace TestUtilities.Python {
    /// <summary>
    /// Sets the default interpreter to the first interpreter that is found to
    /// have the specified package, by searching folders under site-packages.
    /// </summary>
    public class InterpreterWithPackageSetter : IDisposable {
        private DefaultInterpreterSetter _defaultSetter;

        public InterpreterWithPackageSetter(IServiceProvider site, string packageName) {
            IPythonInterpreterFactory factory = FindInterpreterWithPackage(site, packageName);
            Assert.IsNotNull(factory, $"Could not find an interpreter with '{packageName}' package installed.");

            _defaultSetter = new DefaultInterpreterSetter(factory, site);
        }

        private static IPythonInterpreterFactory FindInterpreterWithPackage(IServiceProvider site, string packageName) {
            var model = (IComponentModel)site.GetService(typeof(SComponentModel));
            var interpreterService = model.GetService<IInterpreterRegistryService>();
            var optionsService = model.GetService<IInterpreterOptionsService>();

            foreach (var ver in PythonPaths.Versions) {
                if (!ver.IsCPython) {
                    continue;
                }

                foreach (var p in Directory.EnumerateDirectories(Path.Combine(ver.PrefixPath, "Lib", "site-packages"))) {
                    if (Path.GetFileName(p).StartsWith(packageName, StringComparison.InvariantCultureIgnoreCase)) {
                        var factory = interpreterService.FindInterpreter(ver.Id);
                        if (factory != null) {
                            return factory;
                        }
                    }
                }
            }

            return null;
        }

        public void Dispose() {
            _defaultSetter.Dispose();
        }
    }
}
