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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Interpreter.Default {
    class AnalysisOnlyInterpreterFactory : PythonInterpreterFactoryWithDatabase {
        readonly IEnumerable<string> _actualDatabasePaths;
        readonly PythonTypeDatabase _actualDatabase;

        public AnalysisOnlyInterpreterFactory(Version version, string description = null)
            : base(
                description ?? string.Format("Python {0} Analyzer", version),
                new InterpreterConfiguration(version.ToString(), "Analysis", version),
                false
        ) { }

        public AnalysisOnlyInterpreterFactory(Version version, IEnumerable<string> databasePaths, string description = null)
            : this(version, description) {
            _actualDatabasePaths = databasePaths.ToList();
        }

        public AnalysisOnlyInterpreterFactory(Version version, PythonTypeDatabase database, string description = null)
            : this(version, description) {
            _actualDatabase = database;
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
