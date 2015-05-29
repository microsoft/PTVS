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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace AnalysisTests {
    [TestClass]
    public class AnalyzerTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
        }

        [TestMethod, Priority(0)]
        public async Task LogFileEncoding() {
            // Ensure that log messages round-trip correctly.

            const string TEST = "Abc \u01FA\u0299\uFB3B";
            var log1 = Path.GetTempFileName();
            var log2 = Path.GetTempFileName();

            try {
                using (var analyzer = new PyLibAnalyzer(
                    Guid.Empty,
                    new Version(),
                    null,
                    null,
                    null,
                    null,
                    log1,
                    log2,
                    null,
                    false,
                    false,
                    null,
                    1
                )) {
                    await analyzer.StartTraceListener();
                    analyzer.TraceError(TEST);
                    analyzer.TraceWarning(TEST);
                    analyzer.TraceInformation(TEST);
                    analyzer.TraceVerbose(TEST);

                    analyzer.LogToGlobal(TEST);
                }

                var content1 = File.ReadLines(log1, Encoding.UTF8)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Skip(1)    // Skip the header
                    .Select(line => line.Trim())
                    .ToArray();
                Console.WriteLine(string.Join(Environment.NewLine, content1));
                Console.WriteLine();
                Assert.IsTrue(Regex.IsMatch(content1[0], @"^\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d: \[ERROR\] " + TEST + "$"), content1[0]);
                Assert.IsTrue(Regex.IsMatch(content1[1], @"^\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d: \[WARNING\] " + TEST + "$"), content1[1]);
                Assert.IsTrue(Regex.IsMatch(content1[2], @"^\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d: " + TEST + "$"), content1[2]);
#if DEBUG
                Assert.IsTrue(Regex.IsMatch(content1[3], @"^\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d: \[VERBOSE\] " + TEST + "$"), content1[3]);
#endif

                var content2 = File.ReadAllText(log2, Encoding.UTF8);
                Console.WriteLine(content2);
                Assert.IsTrue(Regex.IsMatch(content2, @"\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d " + TEST + " .+$"), content2);
            } finally {
                File.Delete(log1);
                File.Delete(log2);
            }
        }

        private static DateTime LastWeek {
            get {
                return DateTime.Now.Subtract(TimeSpan.FromDays(7));
            }
        }

        private static DateTime Yesterday {
            get {
                return DateTime.Now.Subtract(TimeSpan.FromDays(1));
            }
        }

        [TestMethod, Priority(0)]
        public void TemporaryLibTest() {
            string libPath = "C:\\", dbPath = "C:\\";

            using (var libDb = new TemporaryLibAndDB(
                "a.py",
                "b.pyd",
                "A1\\__init__.py",
                "A1\\a.py",
                "A2\\__init__.py",
                "A2\\a.py",
                "site-packages\\B\\__init__.py",
                "site-packages\\B\\b.py"
            )) {
                libPath = libDb.Library;
                dbPath = libDb.Database;

                Assert.AreEqual(8, libDb.FilesInDatabase.Count());
                Assert.AreEqual(8, libDb.FilesInLibrary.Count());

                foreach (var p in libDb.FilesInDatabase) {
                    Console.WriteLine(p);
                }

                foreach (var p in libDb.FilesInLibrary) {
                    Console.WriteLine(p);
                }

                Assert.IsTrue(File.Exists(Path.Combine(libDb.Database, "database.ver")));
                Assert.AreEqual(
                    PythonTypeDatabase.CurrentVersion,
                    int.Parse(File.ReadAllText(Path.Combine(libDb.Database, "database.ver")))
                );

                var path = Path.Combine(libDb.Library, "a.py");
                Assert.IsTrue(File.Exists(path), path);
                path = Path.Combine(libDb.Library, "b.pyd");
                Assert.IsTrue(File.Exists(path), path);
                path = Path.Combine(libDb.Library, "A1\\a.py");
                Assert.IsTrue(File.Exists(path), path);
                path = Path.Combine(libDb.Library, "A2\\a.py");
                Assert.IsTrue(File.Exists(path), path);
                path = Path.Combine(libDb.Library, "site-packages\\B\\__init__.py");
                Assert.IsTrue(File.Exists(path), path);
                path = Path.Combine(libDb.Library, "site-packages\\B\\b.py");
                Assert.IsTrue(File.Exists(path), path);

                path = Path.Combine(libDb.Database, "a.idb");
                Assert.IsTrue(File.Exists(path), path);
                path = Path.Combine(libDb.Database, "b.idb");
                Assert.IsTrue(File.Exists(path), path);
                path = Path.Combine(libDb.Database, "A1.a.idb");
                Assert.IsTrue(File.Exists(path), path);
                path = Path.Combine(libDb.Database, "A2.a.idb");
                Assert.IsTrue(File.Exists(path), path);
                path = Path.Combine(libDb.Database, "B", "B.idb");
                Assert.IsTrue(File.Exists(path), path);
                path = Path.Combine(libDb.Database, "B", "B.b.idb");
                Assert.IsTrue(File.Exists(path), path);

                var path1 = Path.Combine(libDb.Library, "a.py");
                var path2 = Path.Combine(libDb.Database, "a.idb");
                libDb.TouchLibrary(LastWeek, "a.py");
                libDb.TouchDatabase("a.py");
                Assert.IsTrue(File.GetLastWriteTime(path1) < File.GetLastWriteTime(path2));
                libDb.TouchDatabase(LastWeek, "a.py");
                libDb.TouchLibrary("a.py");
                Assert.IsTrue(File.GetLastWriteTime(path1) > File.GetLastWriteTime(path2));
            }

            Assert.IsFalse(Directory.Exists(libPath), libPath);
            Assert.IsFalse(Directory.Exists(dbPath), dbPath);
        }

        private static readonly string[] BasicFiles = new[] {
            "a.py",
            "b.pyd",
            "A1\\__init__.py",
            "A1\\a.py",
            "A2\\__init__.py",
            "A2\\a.py",
            "site-packages\\B\\__init__.py",
            "site-packages\\B\\b.py"
        };

        [TestMethod, Priority(0)]
        public async Task NoFilesOutOfDate() {
            var files = BasicFiles;
            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                libDb.TouchLibrary(LastWeek, files);
                libDb.TouchDatabase(Yesterday, files);
                Assert.IsFalse(await analyzer.Prepare(true));

                Assert.AreEqual(files.Count(), libDb.FilesInDatabase.Count());
                Assert.AreEqual(0, analyzer._analyzeFileGroups.Count);
                Assert.AreEqual(0, analyzer._scrapeFileGroups.Count);
            }
        }


        [TestMethod, Priority(0)]
        public async Task AllFilesOutOfDate() {
            var files = BasicFiles;
            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                libDb.TouchLibrary(files);
                Assert.IsTrue(await analyzer.Prepare(true));

                Assert.AreEqual(0, libDb.FilesInDatabase.Count());
                Assert.AreEqual(2, analyzer._analyzeFileGroups.Count);
                Assert.AreEqual(files.Count() - 1, analyzer._analyzeFileGroups.SelectMany(i => i).Count());
                Assert.AreEqual(1, analyzer._scrapeFileGroups.Count);
                Assert.AreEqual(1, analyzer._scrapeFileGroups.SelectMany(i => i).Count());
            }
        }

        [TestMethod, Priority(0)]
        public async Task FileInStdLibMissing() {
            var files = BasicFiles;
            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                libDb.DeleteLibrary("a.py");

                Assert.IsFalse(await analyzer.Prepare(true));

                // This is the result we'd expect.
                //Assert.AreEqual(files.Count() - 1, libDb.FilesInDatabase.Count())

                // But because we don't provide an interpreter here, no files
                // from the top-level database directory will ever be deleted.
                // Otherwise, sys.builtin_module_names is used to determine
                // which files to keep.
                Assert.AreEqual(files.Count(), libDb.FilesInDatabase.Count());

                Assert.AreEqual(0, analyzer._analyzeFileGroups.Count);
                Assert.AreEqual(0, analyzer._scrapeFileGroups.Count);
            }
        }

        [TestMethod, Priority(0)]
        public async Task FileInSitePackageMissing() {
            var files = BasicFiles;
            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                libDb.DeleteLibrary("site-packages\\B\\b.py");

                Assert.IsFalse(await analyzer.Prepare(true));

                Assert.AreEqual(files.Count() - 1, libDb.FilesInDatabase.Count());
                Assert.AreEqual(0, analyzer._analyzeFileGroups.Count);
                Assert.AreEqual(0, analyzer._scrapeFileGroups.Count);
            }
        }

        [TestMethod, Priority(0)]
        public async Task FileInStdLibOutOfDate() {
            var files = BasicFiles;
            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                libDb.TouchLibrary("a.py");

                Assert.IsTrue(await analyzer.Prepare(true));

                Assert.AreEqual(0, libDb.FilesInDatabase.Count());
                Assert.AreEqual(2, analyzer._analyzeFileGroups.Count);
                Assert.AreEqual(files.Count() - 1, analyzer._analyzeFileGroups.SelectMany(i => i).Count());
                Assert.AreEqual(1, analyzer._scrapeFileGroups.Count);
                Assert.AreEqual(1, analyzer._scrapeFileGroups.SelectMany(i => i).Count());
            }
        }

        [TestMethod, Priority(0)]
        public async Task FileInSitePackageOutOfDate() {
            var files = BasicFiles;
            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                libDb.TouchLibrary("site-packages\\B\\__init__.py");

                Assert.IsTrue(await analyzer.Prepare(true));

                Assert.AreEqual(files.Count() - 2, libDb.FilesInDatabase.Count());
                Assert.AreEqual(1, analyzer._analyzeFileGroups.Count);
                Assert.AreEqual(2, analyzer._analyzeFileGroups.SelectMany(i => i).Count());
                Assert.AreEqual(0, analyzer._scrapeFileGroups.Count);
            }
        }

        [TestMethod, Priority(0)]
        public async Task IdbInStdLibMissing() {
            var files = BasicFiles;
            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                libDb.DeleteDatabase("a.py");

                Assert.IsTrue(await analyzer.Prepare(true));

                Assert.AreEqual(0, libDb.FilesInDatabase.Count());
                Assert.AreEqual(2, analyzer._analyzeFileGroups.Count);
                Assert.AreEqual(files.Count() - 1, analyzer._analyzeFileGroups.SelectMany(i => i).Count());
                Assert.AreEqual(1, analyzer._scrapeFileGroups.Count);
                Assert.AreEqual(1, analyzer._scrapeFileGroups.SelectMany(i => i).Count());
            }
        }

        [TestMethod, Priority(0)]
        public async Task IdbInSitePackageMissing() {
            var files = BasicFiles;
            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                libDb.DeleteDatabase("site-packages\\B\\__init__.py");

                Assert.IsTrue(await analyzer.Prepare(true));

                Assert.AreEqual(files.Count() - 2, libDb.FilesInDatabase.Count());
                Assert.AreEqual(1, analyzer._analyzeFileGroups.Count);
                Assert.AreEqual(2, analyzer._analyzeFileGroups.SelectMany(i => i).Count());
                Assert.AreEqual(0, analyzer._scrapeFileGroups.Count);
            }
        }

        [TestMethod, Priority(0)]
        public async Task SitePackageAdded() {
            var files = BasicFiles;
            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                var path = Path.Combine(libDb.Library, "site-packages", "newPackage");
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "__init__.py"), "Not a real .py file");
                File.WriteAllText(Path.Combine(path, "newMod.py"), "Not a real .py file");

                Assert.IsTrue(await analyzer.Prepare(true));

                // Nothing deleted, and only one analysis group queued.
                Assert.AreEqual(files.Count(), libDb.FilesInDatabase.Count());
                Assert.AreEqual(1, analyzer._analyzeFileGroups.Count);
                Assert.AreEqual(2, analyzer._analyzeFileGroups.SelectMany(i => i).Count());
                Assert.AreEqual(0, analyzer._scrapeFileGroups.Count);
            }
        }

        [TestMethod, Priority(0)]
        public async Task SitePackageRemoved() {
            var files = BasicFiles;
            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                var path = Path.Combine(libDb.Library, "site-packages", "B");
                Directory.Delete(path, true);

                Assert.IsFalse(await analyzer.Prepare(true));

                // Two files deleted and nothing queued.
                Assert.AreEqual(files.Count() - 2, libDb.FilesInDatabase.Count());
                Assert.AreEqual(0, analyzer._analyzeFileGroups.Count);
                Assert.AreEqual(0, analyzer._scrapeFileGroups.Count);
            }
        }


        [TestMethod, Priority(0)]
        public async Task ConflictingPyAndPyd() {
            var files = new[] {
                "a.py",
                "a.pyd",
                "b.pyd",
                "b.pyw"
            };
            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                libDb.DeleteDatabase(files);

                Assert.IsTrue(await analyzer.Prepare(true));

                Assert.AreEqual(0, analyzer._analyzeFileGroups.Count);
                Assert.AreEqual(1, analyzer._scrapeFileGroups.Count);
                Assert.AreEqual(2, analyzer._scrapeFileGroups[0].Count);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ChangeAllToTrueOnSecondGroup() {
            var files = new[] {
                "a.py",
                "site-packages\\b.py",
                "site-packages\\C\\__init__.py"
            };

            using (var libDb = new TemporaryLibAndDB(files))
            using (var analyzer = libDb.Analyzer) {
                Assert.IsTrue(analyzer.SkipUnchanged);

                Assert.IsTrue(await analyzer.Prepare(true));

                Assert.IsFalse(analyzer.SkipUnchanged);
                Assert.AreEqual(3, analyzer._analyzeFileGroups.Count);
            }
        }

        [TestMethod, Priority(0)]
        public async Task SitePackagesInPthFile() {
            var files = new[] {
                "a.py",
                "site-packages\\b.py",
                "site-packages\\C\\__init__.py",
                "site-packages\\D\\__init__.py"
            };

            using (var libDb = new TemporaryLibAndDB(files)) {
                libDb.AddFileToLibrary("site-packages\\self.pth", ".");

                using (var analyzer = libDb.Analyzer) {
                    Assert.IsTrue(await analyzer.Prepare(true));

                    // Expect four groups, whereas if self.pth was allowed we'd
                    // only see three.
                    Assert.AreEqual(4, analyzer._analyzeFileGroups.Count);
                }
            }
        }

        /// <summary>
        /// Creates a temporary 'library' and 'database' on disk for testing the
        /// analyzer's change detection.
        /// 
        /// By default, the library was last modified 7 days ago and the
        /// database was last modifies 1 day ago.
        /// 
        /// Use the TouchLibrary() or DeleteLibrary() methods to refresh or
        /// remove files from the library.
        /// 
        /// Use the TouchDatabase() or DeleteDatabase() methods to refresh or
        /// remove files from the database. The original filename should be
        /// passed to these functions.
        /// </summary>
        class TemporaryLibAndDB : IDisposable {
            public readonly string Database;
            public readonly string Library;

            public TemporaryLibAndDB(params string[] pyFiles) {
                var path = Path.GetTempFileName();
                File.Delete(path);
                path = Path.ChangeExtension(path, null);
                Directory.CreateDirectory(path);

                Database = Path.Combine(path, "Database");
                Directory.CreateDirectory(Database);

                Library = Path.Combine(path, "Library");
                Directory.CreateDirectory(Library);

                File.WriteAllText(
                    Path.Combine(Database, "database.ver"),
                    PythonTypeDatabase.CurrentVersion.ToString()
                );

                var yesterday = DateTime.Now.Subtract(TimeSpan.FromDays(1));
                var lastWeek = DateTime.Now.Subtract(TimeSpan.FromDays(7));

                foreach (var file in pyFiles) {
                    path = Path.Combine(Database, GetIdbName(file));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, "Not really a database file");
                    File.SetLastWriteTime(path, yesterday);

                    path = Path.Combine(Library, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, "Not really a Python file");
                    File.SetLastWriteTime(path, lastWeek);
                }

                PrintFiles();
            }

            public void PrintFiles() {
                foreach (var p in FilesInDatabase) {
                    Console.WriteLine(p);
                }

                foreach (var p in FilesInLibrary) {
                    Console.WriteLine(p);
                }
                Console.WriteLine();
            }

            public PyLibAnalyzer Analyzer {
                get {
                    return new PyLibAnalyzer(Guid.Empty,
                        new Version(2, 7),
                        null,
                        new [] {
                            new PythonLibraryPath(Library, true, null),
                            new PythonLibraryPath(Path.Combine(Library, "site-packages"), false, null),
                        },
                        null,
                        Database,
                        null,
                        null,
                        null,
                        false,
                        false,
                        null,
                        1
                    );
                }
            }

            private static string GetIdbName(string pyFile) {
                var file = Path.ChangeExtension(pyFile.Replace('\\', '.'), ".idb");
                if (file.StartsWith("site-packages.")) {
                    int firstDot = file.IndexOf('.'),
                        secondDot = file.IndexOf('.', firstDot + 1),
                        lastDot = file.LastIndexOf('.');
                    if (firstDot != lastDot) {
                        file = Path.Combine(
                            file.Substring(firstDot + 1, secondDot - firstDot),
                            file.Substring(firstDot + 1)
                        );
                    }
                }
                int lastInit = file.LastIndexOf(".__init__.idb");
                if (lastInit > 0) {
                    file = file.Remove(lastInit) + ".idb";
                }
                return file;
            }

            public IEnumerable<string> FilesInDatabase {
                get {
                    return Directory.EnumerateFiles(Database, "*.idb", SearchOption.AllDirectories);
                }
            }

            public IEnumerable<string> FilesInLibrary {
                get {
                    return Directory.EnumerateFiles(Library, "*.*", SearchOption.AllDirectories);
                }
            }

            public void TouchDatabase(DateTime time, params string[] pyFiles) {
                foreach (var file in pyFiles) {
                    var path = Path.Combine(Database, GetIdbName(file));
                    File.SetLastWriteTime(path, time);
                }
            }

            public void TouchDatabase(params string[] pyFiles) {
                TouchDatabase(DateTime.Now, pyFiles);
            }

            public void TouchLibrary(DateTime time, params string[] pyFiles) {
                foreach (var file in pyFiles) {
                    var path = Path.Combine(Library, file);
                    File.SetLastWriteTime(path, time);
                }
            }

            public void TouchLibrary(params string[] pyFiles) {
                TouchLibrary(DateTime.Now, pyFiles);
            }

            public void DeleteDatabase(params string[] pyFiles) {
                foreach (var file in pyFiles) {
                    var path = Path.Combine(Database, GetIdbName(file));
                    File.Delete(path);
                    Console.WriteLine("Deleted " + path);
                }

                PrintFiles();
            }

            public void DeleteLibrary(params string[] pyFiles) {
                foreach (var file in pyFiles) {
                    var path = Path.Combine(Library, file);
                    File.Delete(path);
                    Console.WriteLine("Deleted " + path);
                }

                PrintFiles();
            }

            public void AddFileToLibrary(string file, string contents) {
                File.WriteAllText(Path.Combine(Library, file), contents);
            }

            public void Dispose() {
                try {
                    Directory.Delete(Database, true);
                } catch (Exception ex) {
                    Console.WriteLine("Error while tidying " + Database);
                    Console.WriteLine(ex);
                }

                try {
                    Directory.Delete(Library, true);
                } catch (Exception ex) {
                    Console.WriteLine("Error while tidying " + Library);
                    Console.WriteLine(ex);
                }
            }
        }
    }
}
