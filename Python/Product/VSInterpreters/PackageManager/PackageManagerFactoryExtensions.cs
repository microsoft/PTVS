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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter {
    public static class PackageManagerFactoryExtensions {
        /// <summary>
        /// Determines whether the interpreter factory contains the specified
        /// modules.
        /// </summary>
        /// <returns>The names of the modules that were found.</returns>
        public static async Task<bool> HasModuleAsync(this IPythonInterpreterFactory factory, string moduleName, IInterpreterOptionsService interpreterOptions = null) {
            if (interpreterOptions != null) {
                foreach (var pm in interpreterOptions.GetPackageManagers(factory)) {
                    if ((await pm.GetInstalledPackageAsync(new PackageSpec(moduleName), CancellationToken.None)).IsValid) {
                        return true;
                    }
                }
            }

            return await Task.Run(() => {
                foreach (var mp in ModulePath.GetModulesInLib(factory.Configuration)) {
                    if (mp.ModuleName == moduleName) {
                        return true;
                    }
                }

                return false;
            });
        }
    }
}
