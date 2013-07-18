/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
                Guid.NewGuid(),
                description ?? string.Format("Python {0} Analyzer", version),
                new InterpreterConfiguration(version),
                false) {
        }

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
