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
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using Microsoft.Win32;

namespace Microsoft.PythonTools.BuildTasks {
    public static class InterpreterCatalog {
        internal const string FactoryProvidersCollection = @"PythonTools\InterpreterFactories";
        // If this collection exists in the settings provider, no factories will
        // be loaded. This is meant for tests.
        public const string FactoryProviderCodeBaseSetting = "CodeBase";
        // The second is a static registry entry for the local machine and/or
        // the current user (HKCU takes precedence), intended for being set by
        // other installers.
        private const string FactoryProvidersRegKeyBase = @"Software\Microsoft\PythonTools\";
        private const string FactoryProvidersRegKeySuffix = @"\InterpreterFactories";

        public static T GetService<T>() {
            CompositionContainer container = CreateContainer<T>();

            return container.GetExportedValue<T>();
        }

        public static CompositionContainer CreateContainer<T>() {
            var paths = GetProviderPaths(
                typeof(T)
            );

            var container = CreateCatelog(paths);
            return container;
        }

        private static void LoadOneProvider(
            string codebase,
            AggregateCatalog catalog
        ) {

            AssemblyCatalog assemblyCatalog = null;

            const string FailedToLoadAssemblyMessage = "Failed to load interpreter provider assembly";
            try {
                assemblyCatalog = new AssemblyCatalog(codebase);
            } catch (Exception ex) {
                LogException(FailedToLoadAssemblyMessage, codebase, ex);
            }

            if (assemblyCatalog == null) {
                return;
            }

            const string FailedToLoadMessage = "Failed to load interpreter provider";
            try {
                catalog.Catalogs.Add(assemblyCatalog);
            } catch (Exception ex) {
                LogException(FailedToLoadMessage, codebase, ex);
            }
        }

        public static IEnumerable<string> GetProviderPaths(Type type) {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var catalog = new List<ComposablePartCatalog>();

            foreach (var baseKey in new[] { Registry.CurrentUser, Registry.LocalMachine }) {
                var version = Version.Parse(AssemblyVersionInfo.Version);

                // Load all of the compatible versions up to our current version.
                for (int minorVersion = 0; minorVersion <= version.Minor; minorVersion++) {

                    var keyName = FactoryProvidersRegKeyBase + version.Major + "." + minorVersion + FactoryProvidersRegKeySuffix;

                    using (var key = baseKey.OpenSubKey(keyName)) {
                        if (key != null) {
                            foreach (var idStr in key.GetSubKeyNames()) {
                                using (var subkey = key.OpenSubKey(idStr)) {
                                    if (subkey != null) {
                                        var asm = subkey.GetValue(FactoryProviderCodeBaseSetting, "") as string;
                                        if (asm != null) {
                                            seen.Add(asm);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (type != null) {
                seen.Add(type.Assembly.Location);
            }
            return seen;
        }

        private static CompositionContainer CreateCatelog(IEnumerable<string> asms) {
            var catalog = new AggregateCatalog();
            foreach (var codeBase in asms) {
                LoadOneProvider(
                    codeBase,
                    catalog
                );
            }

            return new CompositionContainer(catalog);
        }

        private static void LogException(
            string message,
            string path,
            Exception ex,
            IEnumerable<object> data = null
        ) {
            //if (log == null) {
            //    return;
            //}

            //var fullMessage = string.Format("{1}:{0}{2}{0}{3}",
            //    Environment.NewLine,
            //    message,
            //    ex,
            //    data == null ? string.Empty : string.Join(Environment.NewLine, data)
            //).Trim();

            //if (string.IsNullOrEmpty(path)) {
            //    log.LogEntry(
            //        (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
            //        "Python Tools",
            //        fullMessage
            //    );
            //} else {
            //    log.LogEntryPath(
            //        (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
            //        "Python Tools",
            //        fullMessage,
            //        path
            //    );
            //}
        }

    }
}
