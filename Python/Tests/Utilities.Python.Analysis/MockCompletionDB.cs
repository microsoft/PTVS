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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities.Mocks {
    public class MockCompletionDB : IDisposable {
        public readonly PythonLanguageVersion LanguageVersion;
        public readonly string DBPath;
        private PythonTypeDatabase _database;
        private readonly PythonInterpreterFactoryWithDatabase _factory;

        public MockCompletionDB(string path, PythonLanguageVersion version) {
            DBPath = path;
            LanguageVersion = version;
            _factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion(), null, DBPath);
            Directory.CreateDirectory(DBPath);
        }

        /// <summary>
        /// Returns the database instance that represents the mock DB.
        /// </summary>
        /// <remarks>This instance is created lazily and cached.</remarks>
        public PythonTypeDatabase Database {
            get {
                if (_database == null) {
                    _database = _factory.MakeTypeDatabase(DBPath);
                }
                return _database;
            }
        }

        /// <summary>
        /// Returns a CPython factory using the mock DB.
        /// </summary>
        /// <remarks>This instance is created lazily and cached.</remarks>
        public PythonInterpreterFactoryWithDatabase Factory {
            get {
                return _factory;
            }
        }

        /// <summary>
        /// Returns an analyzer using the mock DB. If not provided,
        /// <see cref="Factory"/> is used in place of <paramref name="factory"/>
        /// </summary>
        /// <param name="factory">The factory to use for the analyzer. If
        /// omitted, <see cref="Factory"/> will be used.</param>
        public PythonAnalyzer MakeAnalyzer(IPythonInterpreterFactory factory = null) {
            return new PythonAnalyzer(factory ?? Factory);
        }

        /// <summary>
        /// Returns true if the specified module has cached information for
        /// a module.
        /// </summary>
        public bool Contains(string module) {
            return File.Exists(Path.Combine(DBPath, module + ".idb"));
        }

        /// <summary>
        /// Cleans up the mock database.
        /// </summary>
        public void Dispose() {
            Directory.Delete(DBPath, true);
        }

        /// <summary>
        /// Creates a database containing the default modules and any overrides
        /// specified in modules. The overrides will be copied from
        /// TestData\Databases\V27.
        /// </summary>
        public static MockCompletionDB Create(params string[] modules) {
            return Create(PythonLanguageVersion.V27, modules);
        }

        /// <summary>
        /// Creates a database containing the default modules and any overrides
        /// specified in modules. The overrides will be copied from
        /// TestData\Databases\Vxx, where Vxx is taken from the provided
        /// version number.
        /// </summary>
        public static MockCompletionDB Create(PythonLanguageVersion version, params string[] modules) {
            return Create(version, modules.Select(m => Tuple.Create(m, m)).ToArray());
        }

        /// <summary>
        /// Creates a database containing the default modules and any overrides
        /// specified in modules. The overrides will be copied from
        /// TestData\Databases\Vxx, where Vxx is taken from the provided
        /// version number.
        /// 
        /// Each tuple in modules specifies the source filename and destination
        /// filename, respectively.
        /// </summary>
        public static MockCompletionDB Create(PythonLanguageVersion version, params Tuple<string, string>[] modules) {
            var source1 = PythonTypeDatabase.BaselineDatabasePath;
            var source2 = TestData.GetPath(Path.Combine("TestData", "Databases", version.ToString()));
            Assert.IsTrue(Directory.Exists(source1), "Cannot find " + source1);
            Assert.IsTrue(Directory.Exists(source2), "Cannot find " + source2);

            var db = new MockCompletionDB(TestData.GetTempPath(randomSubPath: true), version);
            Assert.IsNotNull(db, "Unable to create DB path");
            Console.WriteLine("Creating temporary database at {0}", db.DBPath);

            foreach (var src in Directory.EnumerateFiles(source1, "*.idb")) {
                File.Copy(src, Path.Combine(db.DBPath, Path.GetFileName(src)));
            }

            foreach (var mod in modules) {
                var src = Path.Combine(source2, mod.Item1 + ".idb");
                Assert.IsTrue(File.Exists(src), "No IDB file for " + mod.Item1);

                Console.WriteLine("Copying {0} from {1} as {2}", mod.Item1, src, mod.Item2);
                File.Copy(src, Path.Combine(db.DBPath, mod.Item2 + ".idb"), true);
                Assert.IsTrue(db.Contains(mod.Item2), "Failed to copy module " + mod.Item1);
            }

            File.WriteAllText(Path.Combine(db.DBPath, "database.ver"),
                PythonTypeDatabase.CurrentVersion.ToString());

            return db;
        }
    }
}
