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
