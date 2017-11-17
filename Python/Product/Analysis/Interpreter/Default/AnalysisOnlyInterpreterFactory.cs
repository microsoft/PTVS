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

namespace Microsoft.PythonTools.Interpreter.Default {
    class AnalysisOnlyInterpreterFactory : PythonInterpreterFactoryWithDatabase, ICustomInterpreterSerialization {
        readonly IEnumerable<string> _actualDatabasePaths;

        private readonly static InterpreterFactoryCreationOptions CreationOptions = new InterpreterFactoryCreationOptions {
            WatchFileSystem = false
        };

        public AnalysisOnlyInterpreterFactory(Version version, string description = null)
            : base(GetConfiguration(version), CreationOptions) { }

        public AnalysisOnlyInterpreterFactory(Version version, IEnumerable<string> databasePaths, string description = null)
            : base(GetConfiguration(version, databasePaths?.ToArray() ?? Array.Empty<string>()), CreationOptions) {
            _actualDatabasePaths = databasePaths?.ToList();
        }

        internal AnalysisOnlyInterpreterFactory(Dictionary<string, object> properties)
            : base(GetConfiguration(properties), CreationOptions){
            _actualDatabasePaths = GetDatabasePaths(properties);
        }

        public bool GetSerializationInfo(out string assembly, out string typeName, out Dictionary<string, object> properties) {
            assembly = GetType().Assembly.Location;
            typeName = GetType().FullName;
            properties = new Dictionary<string, object> {
                { nameof(Version), Configuration.Version.ToString() }
            };
            if (_actualDatabasePaths != null && _actualDatabasePaths.Any()) {
                properties[nameof(DatabasePath)] = _actualDatabasePaths.ToArray();
            }
            return true;
        }

        private static InterpreterConfiguration GetConfiguration(Dictionary<string, object> properties) {
            Version version = null;
            object o;
            string s;

            if (properties.TryGetValue(nameof(Version), out o) && (s = o as string) != null) {
                try {
                    version = Version.Parse(s);
                } catch (FormatException) {
                }
            }

            var databasePaths = GetDatabasePaths(properties);

            return GetConfiguration(version ?? new Version(), databasePaths ?? Array.Empty<string>());
        }

        private static string[] GetDatabasePaths(Dictionary<string, object> properties) {
            if (properties.TryGetValue(nameof(DatabasePath), out object o)) {
                if (o is IEnumerable<string> e) {
                    return e.ToArray();
                } else if (o is string s) {
                    return new[] { s };
                }
            }

            return null;
        }

        private static InterpreterConfiguration GetConfiguration(Version version, params string[] databasePaths) {
            return new InterpreterConfiguration(
                "AnalysisOnly|" + version.ToString() + "|" + String.Join("|", databasePaths), 
                string.Format("Analysis {0}", version),
                null,
                null,
                null,
                null,
                InterpreterArchitecture.Unknown,
                version,
                InterpreterUIMode.SupportsDatabase
            );
        }

        private static string GetDescription(Version version, string description) {
            return description ?? string.Format("Python {0} Analyzer", version);
        }

        public override PythonTypeDatabase MakeTypeDatabase(string databasePath, bool includeSitePackages = true) {
            if (_actualDatabasePaths != null) {
                return new PythonTypeDatabase(this, _actualDatabasePaths);
            } else {
                return PythonTypeDatabase.CreateDefaultTypeDatabase(this);
            }
        }
    }
}
