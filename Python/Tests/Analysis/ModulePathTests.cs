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
using System.IO;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTests {
    [TestClass]
    public class ModulePathTests {
        [TestMethod, Priority(0)]
        public void ModuleName() {
            foreach (var test in new[] {
                new { FullName = "abc", Name = "abc", ModuleName = "abc", IsSpecialName = false },
                new { FullName = "test.__init__", Name = "__init__", ModuleName = "test", IsSpecialName = true },
                new { FullName = "test.__main__", Name = "__main__", ModuleName = "test.__main__", IsSpecialName = true },
                new { FullName = "test.support", Name = "support", ModuleName = "test.support", IsSpecialName = false }
            }) {
                var mp = new ModulePath(test.FullName, string.Empty, string.Empty);
                Assert.AreEqual(test.Name, mp.Name);
                Assert.AreEqual(test.ModuleName, mp.ModuleName);
                Assert.AreEqual(test.IsSpecialName, mp.IsSpecialName);
            }
        }

        [TestMethod, Priority(0)]
        public void ModuleIsCompiled() {
            foreach (var test in new[] {
                new { SourceFile = "abc.py", IsCompiled = false, IsNative = false },
                new { SourceFile = "abc.pyc", IsCompiled = true, IsNative = false },
                new { SourceFile = "abc.pyo", IsCompiled = true, IsNative = false },
                new { SourceFile = "abc.pyd", IsCompiled = true, IsNative = true },
                new { SourceFile = "abc.cp35-win_amd64.pyd", IsCompiled = true, IsNative = true },
                new { SourceFile = "abc_d.pyd", IsCompiled = true, IsNative = true },
                new { SourceFile = "abc_d.cp35-win_amd64.pyd", IsCompiled = true, IsNative = true }
            }) {
                var mp = new ModulePath(string.Empty, test.SourceFile, string.Empty);
                Assert.AreEqual(test.IsCompiled, mp.IsCompiled, test.SourceFile);
                Assert.AreEqual(test.IsNative, mp.IsNativeExtension, test.SourceFile);
            }
        }

        [TestMethod, Priority(0)]
        public void IsPythonFile() {
            foreach (var test in new[] {
                new { SourceFile = @"spam\abc.py", ExpectedStrict = true, ExpectedNoStrict = true, ExpectedWithoutCompiled = true, ExpectedWithoutCache = true },
                new { SourceFile = @"spam\abc.pyc", ExpectedStrict = true, ExpectedNoStrict = true, ExpectedWithoutCompiled = true, ExpectedWithoutCache = false },
                new { SourceFile = @"spam\abc.pyo", ExpectedStrict = true, ExpectedNoStrict = true, ExpectedWithoutCompiled = true, ExpectedWithoutCache = false },
                new { SourceFile = @"spam\abc.pyd", ExpectedStrict = true, ExpectedNoStrict = true, ExpectedWithoutCompiled = false, ExpectedWithoutCache = true },
                new { SourceFile = @"spam\abc.cp35-win_amd64.pyd", ExpectedStrict = true, ExpectedNoStrict = true, ExpectedWithoutCompiled = false, ExpectedWithoutCache = true },
                new { SourceFile = @"spam\abc_d.pyd", ExpectedStrict = true, ExpectedNoStrict = true, ExpectedWithoutCompiled = false, ExpectedWithoutCache = true },
                new { SourceFile = @"spam\abc_d.cp35-win_amd64.pyd", ExpectedStrict = true, ExpectedNoStrict = true, ExpectedWithoutCompiled = false, ExpectedWithoutCache = true },
                new { SourceFile = @"spam\abc-123.py", ExpectedStrict = false, ExpectedNoStrict = true, ExpectedWithoutCompiled = true, ExpectedWithoutCache = true },
                new { SourceFile = @"spam\abc-123.pyc", ExpectedStrict = false, ExpectedNoStrict = true, ExpectedWithoutCompiled = true, ExpectedWithoutCache = false },
                new { SourceFile = @"spam\abc-123.pyo", ExpectedStrict = false, ExpectedNoStrict = true, ExpectedWithoutCompiled = true, ExpectedWithoutCache = false },
                new { SourceFile = @"spam\abc-123.pyd", ExpectedStrict = false, ExpectedNoStrict = true, ExpectedWithoutCompiled = false, ExpectedWithoutCache = true },
                new { SourceFile = @"spam\abc.123.py", ExpectedStrict = false, ExpectedNoStrict = true, ExpectedWithoutCompiled = true, ExpectedWithoutCache = true },
                new { SourceFile = @"spam\abc.123.pyc", ExpectedStrict = true, ExpectedNoStrict = true, ExpectedWithoutCompiled = true, ExpectedWithoutCache = false },
                new { SourceFile = @"spam\abc.123.pyo", ExpectedStrict = true, ExpectedNoStrict = true, ExpectedWithoutCompiled = true, ExpectedWithoutCache = false },
                new { SourceFile = @"spam\abc.123.pyd", ExpectedStrict = true, ExpectedNoStrict = true, ExpectedWithoutCompiled = false, ExpectedWithoutCache = true },
            }) {
                Assert.AreEqual(test.ExpectedStrict, ModulePath.IsPythonFile(test.SourceFile, true, true, true), test.SourceFile);
                Assert.AreEqual(test.ExpectedNoStrict, ModulePath.IsPythonFile(test.SourceFile, false, true, true), test.SourceFile);
                Assert.AreEqual(test.ExpectedWithoutCompiled, ModulePath.IsPythonFile(test.SourceFile, false, false, true), test.SourceFile);
                Assert.AreEqual(test.ExpectedWithoutCache, ModulePath.IsPythonFile(test.SourceFile, false, true, false), test.SourceFile);
                var withForwards = test.SourceFile.Replace('\\', '/');
                Assert.AreEqual(test.ExpectedStrict, ModulePath.IsPythonFile(withForwards, true, true, true), withForwards);
                Assert.AreEqual(test.ExpectedNoStrict, ModulePath.IsPythonFile(withForwards, false, true, true), withForwards);
            }
        }

        [TestMethod, Priority(0)]
        public void IsDebug() {
            foreach (var test in new[] {
                new { SourceFile = @"spam\abc.py", Expected = false },
                new { SourceFile = @"spam\abc.pyd", Expected = false },
                new { SourceFile = @"spam\abc.cp35-win32.pyd", Expected = false },
                new { SourceFile = @"spam\abc.cpython-35d.pyd", Expected = true },  // not a real filename, but should match the tag still
                new { SourceFile = @"spam\abc_d.pyd", Expected = true },
                new { SourceFile = @"spam\abc_d.cp3-win_amd64.pyd", Expected = true },
                new { SourceFile = @"spam\abc.cpython-35.so", Expected = false },
                new { SourceFile = @"spam\abc.cpython-35u.so", Expected = false },
                new { SourceFile = @"spam\abc.pypy-35m.so", Expected = false },
                new { SourceFile = @"spam\abc.cpython-35d.so", Expected = true },
                new { SourceFile = @"spam\abc.cpython-35dmu.so", Expected = true },
                new { SourceFile = @"spam\abc.jython-35udm.dylib", Expected = true },
                new { SourceFile = @"spam\abc.cpython-35umd.dylib", Expected = true },
            }) {
                Assert.AreEqual(test.Expected, new ModulePath("abc", test.SourceFile, null).IsDebug, test.SourceFile);
            }
        }

        /// <summary>
        /// Verify that the analyzer has the proper algorithm for turning a filename into a package name
        /// </summary>
        [TestMethod, Priority(0)]
        public void ModulePathFromFullPath() {
            var basePath = @"C:\Not\A\Real\Path\";

            // Replace the usual File.Exists(p + '__init__.py') check so we can
            // test without real files.
            var packagePaths = new HashSet<string>(PathEqualityComparer.Instance) {
                basePath + @"A\",
                basePath + @"A\B\"
            };

            Func<string, bool> isPackage = p => {
                Console.WriteLine("isPackage({0})", p);
                return packagePaths.Contains(p);
            };

            // __init__ files appear in the full name but not the module name.
            var mp = ModulePath.FromFullPath(Path.Combine(basePath, "A", "B", "__init__.py"), isPackage: isPackage);
            Assert.AreEqual("A.B", mp.ModuleName);
            Assert.AreEqual("A.B.__init__", mp.FullName);
            Assert.AreEqual("__init__", mp.Name);

            mp = ModulePath.FromFullPath(Path.Combine(basePath, "A", "B", "Module.py"), isPackage: isPackage);
            Assert.AreEqual("A.B.Module", mp.ModuleName);

            // Ensure we don't go back past the top-level directory if specified
            mp = ModulePath.FromFullPath(
                Path.Combine(basePath, "A", "B", "Module.py"),
                Path.Combine(basePath, "A"),
                isPackage
            );
            Assert.AreEqual("B.Module", mp.ModuleName);
        }
    }
}
