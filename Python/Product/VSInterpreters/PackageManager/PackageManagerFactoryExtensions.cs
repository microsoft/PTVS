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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter.LegacyDB;

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

            var withDb = factory as LegacyDB.PythonInterpreterFactoryWithDatabase;
            if (withDb != null && withDb.IsCurrent) {
                var db = withDb.GetCurrentDatabase();
                if (db.GetModule(moduleName) != null) {
                    return true;
                }

                // Always stop searching after this step
                return false;
            }

            if (withDb != null) {
                try {
                    var paths = await LegacyDB.PythonTypeDatabase.GetDatabaseSearchPathsAsync(withDb);
                    if (LegacyDB.PythonTypeDatabase.GetDatabaseExpectedModules(withDb.Configuration.Version, paths)
                        .SelectMany()
                        .Any(g => g.ModuleName == moduleName)) {
                        return true;
                    }
                } catch (InvalidOperationException) {
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

        /// <summary>
        /// Generates the completion database and returns a task that will
        /// complete when the database is regenerated.
        /// </summary>
        internal static Task<int> GenerateDatabaseAsync(
            this LegacyDB.IPythonInterpreterFactoryWithDatabase factory,
            GenerateDatabaseOptions options
        ) {
            var tcs = new TaskCompletionSource<int>();
            factory.GenerateDatabase(options, tcs.SetResult);
            return tcs.Task;
        }

    }
}
