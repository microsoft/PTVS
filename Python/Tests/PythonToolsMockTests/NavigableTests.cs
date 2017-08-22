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
using System.IO;
using System.Threading.Tasks;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation.Navigable;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsMockTests {
    [TestClass]
    public class NavigableTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
        }

        private static PythonVersion Version => PythonPaths.Python27 ?? PythonPaths.Python27_x64;

        [TestMethod, Priority(1)]
        public async Task ModuleDefinition() {
            var code = @"import os
";
            using (var helper = new NavigableHelper(code, Version)) {
                // os
                await helper.CheckDefinitionLocation(7, 2, ExternalLocation(1, 1, "os.py"));
            }
        }

        [TestMethod, Priority(1)]
        public async Task ModuleImportDefinition() {
            var code = @"import sys

sys.version
";
            using (var helper = new NavigableHelper(code, Version)) {
                // sys
                await helper.CheckDefinitionLocation(15, 3, Location(1, 8));

                // version
                await helper.CheckDefinitionLocation(18, 7, null);
            }
        }

        [TestMethod, Priority(1)]
        public async Task ClassDefinition() {
            var code = @"class ClassBase(object):
    pass

class ClassDerived(ClassBase):
    pass

obj = ClassDerived()
obj
";
            using (var helper = new NavigableHelper(code, Version)) {
                // ClassBase
                await helper.CheckDefinitionLocation(57, 9, Location(1, 7));
                
                // ClassDerived
                await helper.CheckDefinitionLocation(88, 12, Location(4, 7));

                // object
                await helper.CheckDefinitionLocation(16, 6, null);

                // obj
                await helper.CheckDefinitionLocation(82, 3, Location(7, 1));

                // obj
                await helper.CheckDefinitionLocation(104, 3, Location(7, 1));
            }
        }

        [TestMethod, Priority(1)]
        public async Task ParameterDefinition() {
            var code = @"def my_func(param1, param2 = True):
    print(param1)
    print(param2)

my_func(1)
my_func(2, param2=False)
";
            using (var helper = new NavigableHelper(code, Version)) {
                // param1
                await helper.CheckDefinitionLocation(12, 6, Location(1, 13));
                await helper.CheckDefinitionLocation(47, 6, Location(1, 13));
                
                // param2
                await helper.CheckDefinitionLocation(20, 6, Location(1, 21));
                await helper.CheckDefinitionLocation(66, 6, Location(1, 21));
                await helper.CheckDefinitionLocation(100, 6, Location(1, 21));
                
                // my_func
                await helper.CheckDefinitionLocation(4, 7, Location(1, 5));
                await helper.CheckDefinitionLocation(77, 7, Location(1, 5));
                await helper.CheckDefinitionLocation(89, 7, Location(1, 5));
            }
        }

        [Ignore] // https://github.com/Microsoft/PTVS/issues/2869
        [TestMethod, Priority(1)]
        public async Task NamedArgumentDefinition() {
            var code = @"class MyClass(object):
    def my_class_func1(self, param2 = True):
        pass

    def my_class_func2(self, param3 = True):
        pass

def my_func(param3 = True):
    pass

my_func(param3=False)

obj = MyClass()
obj.my_class_func1(param2=False)
obj.my_class_func2(param3=False)
";
            using (var helper = new NavigableHelper(code, Version)) {
                // param3 in my_func(param3=False)
                await helper.CheckDefinitionLocation(197, 6, Location(8, 13));

                // BUG: can't go to definition
                // param2 in obj.my_class_func1(param2=False)
                await helper.CheckDefinitionLocation(250, 6, Location(2, 30));

                // BUG: goes to my_func instead of my_class_func2
                // param3 in obj.my_class_func2(param3=False)
                await helper.CheckDefinitionLocation(284, 6, Location(5, 30));
            }
        }

        [TestMethod, Priority(1)]
        public async Task PropertyDefinition() {
            var code = @"class MyClass(object):
    _my_attr_val = 0

    @property
    def my_attr(self):
        return self._my_attr_val

    @my_attr.setter
    def my_attr(self, val):
        self._my_attr_val = val

obj = MyClass()
obj.my_attr = 5
print(obj.my_attr)
print(obj._my_attr_val)
";
            using (var helper = new NavigableHelper(code, Version)) {
                // my_attr
                await helper.CheckDefinitionLocation(71, 7, Location(9, 9));
                await helper.CheckDefinitionLocation(128, 7, Location(9, 9));
                await helper.CheckDefinitionLocation(152, 7, Location(9, 9));
                await helper.CheckDefinitionLocation(229, 7, Location(13, 5));
                await helper.CheckDefinitionLocation(252, 7, Location(13, 5));

                // val
                await helper.CheckDefinitionLocation(166, 3, Location(9, 23));
                await helper.CheckDefinitionLocation(201, 3, Location(9, 23));

                // _my_attr_val
                await helper.CheckDefinitionLocation(28, 12, Location(2, 5));
                await helper.CheckDefinitionLocation(107, 12, Location(10, 14));
                await helper.CheckDefinitionLocation(186, 12, Location(10, 14));
                await helper.CheckDefinitionLocation(272, 12, Location(10, 14));

                // self
                await helper.CheckDefinitionLocation(79, 4, Location(5, 17));
                await helper.CheckDefinitionLocation(102, 4, Location(5, 17));
                await helper.CheckDefinitionLocation(160, 4, Location(9, 17));
                await helper.CheckDefinitionLocation(181, 4, Location(9, 17));
            }
        }

        private AnalysisLocation Location(int line, int col) =>
            new AnalysisLocation(null, line, col);

        private AnalysisLocation ExternalLocation(int line, int col, string filename) =>
            new AnalysisLocation(filename, line, col);

        #region NavigableHelper class

        private class NavigableHelper : IDisposable {
            private readonly PythonEditor _view;

            public NavigableHelper(string code, PythonVersion version) {
                var factory = InterpreterFactoryCreator.CreateInterpreterFactory(version.Configuration, new InterpreterFactoryCreationOptions() { WatchFileSystem = false });
                _view = new PythonEditor("", version.Version, factory: factory);
                _view.Text = code;
            }

            public void Dispose() {
                _view.Dispose();
            }

            public async Task CheckDefinitionLocation(int pos, int length, AnalysisLocation expected) {
                var entry = (AnalysisEntry)_view.GetAnalysisEntry();
                entry.Analyzer.WaitForCompleteAnalysis(_ => true);

                var trackingSpan = _view.CurrentSnapshot.CreateTrackingSpan(pos, length, SpanTrackingMode.EdgeInclusive);
                var snapshotSpan = trackingSpan.GetSpan(_view.CurrentSnapshot);
                var results = await NavigableSymbolSource.GetDefinitionLocationsAsync(entry, snapshotSpan.Start);
                if (expected != null) {
                    Assert.IsNotNull(results);
                    Assert.AreEqual(expected.Line, results[0].Line);
                    Assert.AreEqual(expected.Column, results[0].Column);
                    if (expected.FilePath != null) {
                        Assert.AreEqual(expected.FilePath, Path.GetFileName(results[0].FilePath));
                    }
                } else {
                    Assert.AreEqual(0, results.Length);
                }
            }
        }

        #endregion
    }
}
