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
    class AnalysisOnlyInterpreterFactory : PythonInterpreterFactoryWithDatabase {
        readonly IEnumerable<string> _actualDatabasePaths;
        readonly PythonTypeDatabase _actualDatabase;

        public AnalysisOnlyInterpreterFactory(Version version, string description = null)
            : base(
                GetDescription(version, description),
                GetConfiguration(version),
                false
        ) { }

        public AnalysisOnlyInterpreterFactory(Version version, IEnumerable<string> databasePaths, string description = null)
            : base(GetDescription(version, description), GetConfiguration(version, databasePaths?.ToArray() ?? Array.Empty<string>()), false) {
            _actualDatabasePaths = databasePaths?.ToList();
        }

        public AnalysisOnlyInterpreterFactory(Version version, PythonTypeDatabase database, string description = null)
            : base(GetDescription(version, description), GetConfiguration(version, database.DatabaseDirectory), false) {
            _actualDatabase = database;
        }

        private static InterpreterConfiguration GetConfiguration(Version version, params string[] databasePaths) {
            return new InterpreterConfiguration(
                "AnalysisOnly;" + version.ToString() + ";" + String.Join(";", databasePaths.ToArray()), 
                "Analysis", 
                version
            );
        }

        private static string GetDescription(Version version, string description) {
            return description ?? string.Format("Python {0} Analyzer", version);
        }

        public override PythonTypeDatabase MakeTypeDatabase(string databasePath, bool includeSitePackages = true) {
            if (_actualDatabase != null) {
                return _actualDatabase;
            } else if (_actualDatabasePaths != null) {
                return new PythonTypeDatabase(this, _actualDatabasePaths);
            } else {
                return PythonTypeDatabase.CreateDefaultTypeDatabase(this);
            }
        }
    }
}
