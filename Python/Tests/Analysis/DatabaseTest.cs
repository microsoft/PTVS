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
using TestUtilities;
using TestUtilities.Mocks;

namespace AnalysisTests {
    [TestClass]
    public class DatabaseTest {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void Invalid2xDatabase() {
            using (var db = MockCompletionDB.Create(PythonLanguageVersion.V27,
                // __bad_builtin__ is missing str
                Tuple.Create("__bad_builtin__", "__builtin__")
                )) {
                var ptd = db.Database;

                Assert.IsNotNull(ptd.GetModule("__builtin__"));

                var analyzer = db.MakeAnalyzer();

                // String type should have been loaded from the default DB.
                Assert.IsNotNull(analyzer.ClassInfos[BuiltinTypeId.Str]);
                Assert.IsNotNull(analyzer.ClassInfos[BuiltinTypeId.Bytes]);
            }
        }

        [TestMethod, Priority(0)]
        public void InvalidIronPythonDatabase() {
            using (var db = MockCompletionDB.Create(PythonLanguageVersion.V27,
                // __bad_builtin__ is missing str
                Tuple.Create("__bad_builtin__", "__builtin__")
                )) {
                var ptd = db.Database;

                Assert.IsNotNull(ptd.GetModule("__builtin__"));

                var factory = new IronPythonInterpreterFactory();
                var analyzer = new PythonAnalyzer(factory, factory.MakeInterpreter(ptd));

                // String type should have been loaded anyway
                Assert.IsNotNull(analyzer.ClassInfos[BuiltinTypeId.Str]);
            }
        }

        [TestMethod, Priority(0)]
        public void Invalid3xDatabase() {
            using (var db = MockCompletionDB.Create(PythonLanguageVersion.V33,
                // bad_builtins is missing str
                Tuple.Create("bad_builtins", "builtins")
                )) {
                var ptd = db.Database;

                Assert.IsNotNull(ptd.GetModule("builtins"));

                var analyzer = db.MakeAnalyzer();

                // String type should have been loaded from the default DB.
                Assert.IsNotNull(analyzer.ClassInfos[BuiltinTypeId.Str]);
                // String type is the same as unicode, but not bytes.
                Assert.AreNotEqual(analyzer.Types[BuiltinTypeId.Bytes], analyzer.Types[BuiltinTypeId.Str]);
                Assert.AreEqual(analyzer.Types[BuiltinTypeId.Unicode], analyzer.Types[BuiltinTypeId.Str]);
                // String's module name is 'builtins' and not '__builtin__'
                // because we replace the module name based on the version
                // despite using the 2.7-based database.
                Assert.AreEqual("builtins", analyzer.Types[BuiltinTypeId.Str].DeclaringModule.Name);
            }
        }

        [TestMethod, Priority(0)]
        public void LayeredDatabase() {
            using (var db1 = MockCompletionDB.Create(PythonLanguageVersion.V27, "os"))
            using (var db2 = MockCompletionDB.Create(PythonLanguageVersion.V27, "posixpath")) {
                Assert.IsNotNull(db1.Database.GetModule("os"));
                Assert.IsNull(db1.Database.GetModule("posixpath"));

                var ptd1 = db1.Database;
                var ptd2 = ptd1.Clone();
                ptd2.LoadDatabase(db2.DBPath);

                Assert.IsNull(ptd1.GetModule("posixpath"));
                Assert.IsNotNull(ptd2.GetModule("os"));
                Assert.IsNotNull(ptd2.GetModule("posixpath"));
                Assert.AreSame(ptd1.GetModule("os"), db1.Database.GetModule("os"));
                Assert.AreSame(ptd2.GetModule("os"), db1.Database.GetModule("os"));
            }

            var factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7));

            using (var db1 = MockCompletionDB.Create(PythonLanguageVersion.V27, "os", "posixpath"))
            using (var db2 = MockCompletionDB.Create(PythonLanguageVersion.V27, "posixpath")) {
                var ptd1 = new PythonTypeDatabase(factory, new[] {
                    db1.DBPath,
                    db2.DBPath
                });

                Assert.IsNotNull(ptd1.GetModule("posixpath"));
                Assert.AreNotSame(ptd1.GetModule("posixpath"), db1.Database.GetModule("posixpath"));
                Assert.AreNotSame(ptd1.GetModule("posixpath"), db2.Database.GetModule("posixpath"));

                var ptd2 = new PythonTypeDatabase(factory, new[] {
                    db2.DBPath,
                    db1.DBPath
                });

                Assert.IsNotNull(ptd2.GetModule("posixpath"));
                Assert.AreNotSame(ptd2.GetModule("posixpath"), db1.Database.GetModule("posixpath"));
                Assert.AreNotSame(ptd2.GetModule("posixpath"), db2.Database.GetModule("posixpath"));
            }

            using (var db1 = MockCompletionDB.Create(PythonLanguageVersion.V27, "os", "posixpath"))
            using (var db2 = MockCompletionDB.Create(PythonLanguageVersion.V27, "posixpath"))
            using (var db3 = MockCompletionDB.Create(PythonLanguageVersion.V27, "ntpath")) {
                var ptd = db1.Database;
                Assert.AreSame(ptd.GetModule("posixpath"), db1.Database.GetModule("posixpath"));
                Assert.AreNotSame(ptd.GetModule("posixpath"), db2.Database.GetModule("posixpath"));

                ptd = ptd.Clone();
                ptd.LoadDatabase(db2.DBPath);

                Assert.AreNotSame(ptd.GetModule("posixpath"), db1.Database.GetModule("posixpath"));

                var ptd2 = ptd.Clone();
                ptd2.LoadDatabase(db3.DBPath);

                Assert.IsNotNull(ptd2.GetModule("ntpath"));
                Assert.IsNull(ptd.GetModule("ntpath"));
            }
        }
    }

}
