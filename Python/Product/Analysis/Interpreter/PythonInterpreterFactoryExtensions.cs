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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    public static class PythonInterpreterFactoryExtensions {
        /// <summary>
        /// Executes the interpreter with the specified arguments. Any output is
        /// captured and returned via the <see cref="ProcessOutput"/> object.
        /// </summary>
        internal static ProcessOutput Run(
            this IPythonInterpreterFactory factory,
            params string[] arguments) {
            return ProcessOutput.RunHiddenAndCapture(factory.Configuration.InterpreterPath, arguments);
        }

        /// <summary>
        /// Determines whether two interpreter factories are equivalent.
        /// </summary>
        public static bool IsEqual(this IPythonInterpreterFactory x, IPythonInterpreterFactory y) {
            if (x == null || y == null) {
                return x == null && y == null;
            }
            if (x.GetType() != y.GetType()) {
                return false;
            }

            return x.Configuration.Equals(y.Configuration);
        }

        /// <summary>
        /// Determines whether the interpreter factory contains the specified
        /// modules.
        /// </summary>
        /// <returns>The names of the modules that were found.</returns>
        public static async Task<HashSet<string>> FindModulesAsync(this IPythonInterpreterFactory factory, params string[] moduleNames) {
            var withPackages = factory as IPackageManager;
            if (withPackages != null) {
                var res = new HashSet<string>();
                foreach (var m in moduleNames) {
                    if ((await withPackages.GetInstalledPackageAsync(new PackageSpec(m), CancellationToken.None)).IsValid) {
                        res.Add(m);
                    }
                }
                if (res.Count == moduleNames.Length) {
                    return res;
                }
            }

            var withDb = factory as PythonInterpreterFactoryWithDatabase;
            if (withDb != null && withDb.IsCurrent) {
                var db = withDb.GetCurrentDatabase();
                var set = new HashSet<string>(moduleNames.Where(m => db.GetModule(m) != null));
                return set;
            }

            var expected = new HashSet<string>(moduleNames);

            if (withDb != null) {
                try {
                    var paths = PythonTypeDatabase.GetCachedDatabaseSearchPaths(withDb.DatabasePath) ??
                        await PythonTypeDatabase.GetUncachedDatabaseSearchPathsAsync(withDb.Configuration.InterpreterPath).ConfigureAwait(false);
                    var db = PythonTypeDatabase.GetDatabaseExpectedModules(withDb.Configuration.Version, paths)
                        .SelectMany()
                        .Select(g => g.ModuleName);
                    expected.IntersectWith(db);
                    return expected;
                } catch (InvalidOperationException) {
                }
            }

            return await Task.Run(() => {
                var result = new HashSet<string>();
                foreach (var mp in ModulePath.GetModulesInLib(factory.Configuration)) {
                    if (expected.Count == 0) {
                        break;
                    }

                    if (expected.Remove(mp.ModuleName)) {
                        result.Add(mp.ModuleName);
                    }
                }
                return result;
            });
        }

        /// <summary>
        /// Generates the completion database and returns a task that will
        /// complete when the database is regenerated.
        /// </summary>
        internal static Task<int> GenerateDatabaseAsync(
            this IPythonInterpreterFactoryWithDatabase factory,
            GenerateDatabaseOptions options
        ) {
            var tcs = new TaskCompletionSource<int>();
            factory.GenerateDatabase(options, tcs.SetResult);
            return tcs.Task;
        }

        /// <summary>
        /// Returns <c>true</c> if the factory should appear in the UI.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool IsUIVisible(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.Hidden);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory should appear in the UI.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool IsUIVisible(this InterpreterConfiguration config) {
            return config != null &&
                !config.UIMode.HasFlag(InterpreterUIMode.Hidden);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory can ever be the default
        /// interpreter.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeDefault(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeDefault);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory can be automatically selected as
        /// the default interpreter.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeAutoDefault(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeDefault) &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeAutoDefault);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory can be automatically selected as
        /// the default interpreter.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeAutoDefault(this InterpreterConfiguration config) {
            return config != null &&
                !config.UIMode.HasFlag(InterpreterUIMode.CannotBeDefault) &&
                !config.UIMode.HasFlag(InterpreterUIMode.CannotBeAutoDefault);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory can be configured.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeConfigured(this InterpreterConfiguration config) { 
            return config != null &&
                !config.UIMode.HasFlag(InterpreterUIMode.CannotBeConfigured);
        }


        /// <summary>
        /// Returns <c>true</c> if the factory can be configured.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeConfigured(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeConfigured);
        }
    }
}
