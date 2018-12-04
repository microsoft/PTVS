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
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Linq;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Infrastructure {
    static class InterpreterCatalog {
        internal const string FactoryProvidersCollection = @"PythonTools\InterpreterFactories";
        // If this collection exists in the settings provider, no factories will
        // be loaded. This is meant for tests.
        public const string FactoryProviderCodeBaseSetting = "CodeBase";
        // The second is a static registry entry for the local machine and/or
        // the current user (HKCU takes precedence), intended for being set by
        // other installers.
        private const string FactoryProvidersRegKeyBase = @"Software\Microsoft\PythonTools\";
        private const string FactoryProvidersRegKeySuffix = @"\InterpreterFactories";

        public static CompositionContainer CreateContainer(params Type[] additionalTypes) {
            return CreateContainer(null, additionalTypes);
        }

        public static CompositionContainer CreateContainer(ICatalogLog log, params Type[] additionalTypes) {
            return CreateContainer(log, GetProviderPaths(additionalTypes).ToArray());
        }

        public static CompositionContainer CreateContainer(ICatalogLog log, params string[] paths) {
            return CreateCatelog(log, paths);
        }

        private static void LoadOneProvider(
            ICatalogLog log,
            string codebase,
            AggregateCatalog catalog
        ) {

            AssemblyCatalog assemblyCatalog = null;

            const string FailedToLoadAssemblyMessage = "Failed to load interpreter provider assembly {0} {1}";
            try {
                assemblyCatalog = new AssemblyCatalog(codebase);
            } catch (Exception ex) {
                log.Log(String.Format(FailedToLoadAssemblyMessage, codebase, ex));
            }

            if (assemblyCatalog == null) {
                return;
            }

            const string FailedToLoadMessage = "Failed to load interpreter provider {0} {1}";
            try {
                catalog.Catalogs.Add(assemblyCatalog);
            } catch (Exception ex) {
                log.Log(String.Format(FailedToLoadMessage, codebase, ex));
            }
        }

        public static IEnumerable<string> GetProviderPaths(Type[] types) {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase); ;
            var catalog = new List<ComposablePartCatalog>();

            var version = Version.Parse(AssemblyVersionInfo.Version);

            // Load all of the compatible versions up to our current version.
            // Load in reverse order so we pick up the most featureful compatible version.
            for (int minorVersion = version.Minor; minorVersion >= 0; minorVersion--) {

                foreach (var baseKey in new[] { Registry.CurrentUser, Registry.LocalMachine }) {

                    var keyName = FactoryProvidersRegKeyBase + version.Major + "." + minorVersion + FactoryProvidersRegKeySuffix;

                    using (var key = baseKey.OpenSubKey(keyName)) {
                        if (key != null) {
                            foreach (var idStr in key.GetSubKeyNames()) {
                                // if we've seen this ID before don't re-register it, we only
                                // want to pick up the latest compatible version.
                                if (!seenIds.Contains(idStr)) {
                                    using (var subkey = key.OpenSubKey(idStr)) {
                                        if (subkey != null) {
                                            var asm = subkey.GetValue(FactoryProviderCodeBaseSetting, "") as string;
                                            if (asm != null) {
                                                seenIds.Add(idStr);
                                                seen.Add(asm);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (var type in types) {
                seen.Add(type.Assembly.Location);
            }

            return seen;
        }

        private static CompositionContainer CreateCatelog(ICatalogLog log, IEnumerable<string> asms) {
            var catalog = new AggregateCatalog();
            foreach (var codeBase in asms) {
                LoadOneProvider(
                    log,
                    codeBase,
                    catalog
                );
            }

            return new CompositionContainer(catalog);
        }
    }
}
