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

using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTests {
    [TestClass]
    public class ModulePathTests {
        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public void IsPythonFile() {
            foreach (var test in new[] {
                new { SourceFile = @"spam\abc.py", ExpectedStrict = true, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc.pyc", ExpectedStrict = true, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc.pyo", ExpectedStrict = true, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc.pyd", ExpectedStrict = true, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc.cp35-win_amd64.pyd", ExpectedStrict = true, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc_d.pyd", ExpectedStrict = true, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc_d.cp35-win_amd64.pyd", ExpectedStrict = true, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc-123.py", ExpectedStrict = false, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc-123.pyc", ExpectedStrict = false, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc-123.pyo", ExpectedStrict = false, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc-123.pyd", ExpectedStrict = false, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc.123.py", ExpectedStrict = false, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc.123.pyc", ExpectedStrict = false, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc.123.pyo", ExpectedStrict = false, ExpectedNoStrict = true },
                new { SourceFile = @"spam\abc.123.pyd", ExpectedStrict = true, ExpectedNoStrict = true },
            }) {
                Assert.AreEqual(test.ExpectedStrict, ModulePath.IsPythonFile(test.SourceFile, true, true), test.SourceFile);
                Assert.AreEqual(test.ExpectedNoStrict, ModulePath.IsPythonFile(test.SourceFile, false, true), test.SourceFile);
                var withForwards = test.SourceFile.Replace('\\', '/');
                Assert.AreEqual(test.ExpectedStrict, ModulePath.IsPythonFile(withForwards, true, true), withForwards);
                Assert.AreEqual(test.ExpectedNoStrict, ModulePath.IsPythonFile(withForwards, false, true), withForwards);
            }
        }
    }
}
