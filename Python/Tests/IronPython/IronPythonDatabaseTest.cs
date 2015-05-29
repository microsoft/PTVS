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
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities.Mocks;

namespace IronPythonTests {
    [TestClass]
    public class IronPythonDatabaseTest {
        [TestMethod, Priority(0)]
        public void InvalidIronPythonDatabase() {
            using (var db = MockCompletionDB.Create(PythonLanguageVersion.V27,
                // __bad_builtin__ is missing str
                Tuple.Create("__bad_builtin__", "__builtin__")
            )) {
                var ptd = db.Database;

                Assert.IsNotNull(ptd.GetModule("__builtin__"));

                var factory = new IronPythonInterpreterFactory();
                // Explicitly create an IronPythonInterpreter from factory that
                // will use the database in db.Factory.
                using (var analyzer = PythonAnalyzer.CreateSynchronously(factory, factory.MakeInterpreter(db.Factory))) {
                    // String type should have been loaded anyway
                    Assert.IsNotNull(analyzer.ClassInfos[BuiltinTypeId.Str]);
                }
            }
        }

    }
}
