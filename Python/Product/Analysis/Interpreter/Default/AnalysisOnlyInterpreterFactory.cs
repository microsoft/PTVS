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
        readonly string _actualDatabasePath;
        readonly PythonTypeDatabase _actualDatabase;

        public AnalysisOnlyInterpreterFactory(Version version)
            : base(Guid.NewGuid(), "Python Code Analysis", new InterpreterConfiguration("", "", "", "", ProcessorArchitecture.None, version), false) {
        }

        public AnalysisOnlyInterpreterFactory(Version version, string databasePath)
            : this(version) {
            _actualDatabasePath = databasePath;
        }

        public AnalysisOnlyInterpreterFactory(Version version, PythonTypeDatabase database)
            : this(version) {
            _actualDatabase = database;
        }

        protected override PythonTypeDatabase MakeTypeDatabase(string databasePath) {
            if (_actualDatabase != null) {
                return _actualDatabase;
            } else if (_actualDatabasePath != null) {
                return new PythonTypeDatabase(_actualDatabasePath, Configuration.Version);
            } else {
                return PythonTypeDatabase.CreateDefaultTypeDatabase(Configuration.Version);
            }
        }

        protected override IPythonInterpreter MakeInterpreter(PythonTypeDatabase typeDb) {
            return new CPythonInterpreter(this, typeDb);
        }
    }
}
