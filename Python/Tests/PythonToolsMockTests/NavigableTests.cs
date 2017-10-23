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
        }

        private static PythonVersion Version => PythonPaths.Python27 ?? PythonPaths.Python27_x64;

        [TestInitialize]
        public void TestInit() {
            MockPythonToolsPackage.SuppressTaskProvider = true;
            VsProjectAnalyzer.SuppressTaskProvider = true;
        }

        [TestCleanup]
        public void TestCleanup() {
            MockPythonToolsPackage.SuppressTaskProvider = false;
            VsProjectAnalyzer.SuppressTaskProvider = false;
        }

        [TestMethod, Priority(0)]
        public async Task ModuleDefinition() {
            var code = @"import os
";
            using (var helper = new NavigableHelper(code, Version)) {
                // os
                await helper.CheckDefinitionLocations(7, 2, ExternalLocation(1, 1, "os.py"));
            }
        }

        [TestMethod, Priority(0)]
        public async Task ModuleImportDefinition() {
            var code = @"import sys

sys.version
";
            using (var helper = new NavigableHelper(code, Version)) {
                // sys
                await helper.CheckDefinitionLocations(14, 3, Location(1, 8));

                // version
                await helper.CheckDefinitionLocations(18, 7, null);
            }
        }

        [TestMethod, Priority(0)]
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
                await helper.CheckDefinitionLocations(57, 9, Location(1, 7));
                
                // ClassDerived
                await helper.CheckDefinitionLocations(88, 12, Location(4, 7));

                // object
                await helper.CheckDefinitionLocations(16, 6, null);

                // obj
                await helper.CheckDefinitionLocations(82, 3, Location(7, 1));

                // obj
                await helper.CheckDefinitionLocations(104, 3, Location(7, 1));
            }
        }

        [TestMethod, Priority(0)]
        public async Task ParameterDefinition() {
            var code = @"def my_func(param1, param2 = True):
    print(param1)
    print(param2)

my_func(1)
my_func(2, param2=False)
";
            using (var helper = new NavigableHelper(code, Version)) {
                // param1
                await helper.CheckDefinitionLocations(12, 6, Location(1, 13));
                await helper.CheckDefinitionLocations(47, 6, Location(1, 13));
                
                // param2
                await helper.CheckDefinitionLocations(20, 6, Location(1, 21));
                await helper.CheckDefinitionLocations(66, 6, Location(1, 21));
                await helper.CheckDefinitionLocations(100, 6, Location(1, 21));
                
                // my_func
                await helper.CheckDefinitionLocations(4, 7, Location(1, 5));
                await helper.CheckDefinitionLocations(77, 7, Location(1, 5));
                await helper.CheckDefinitionLocations(89, 7, Location(1, 5));
            }
        }

        [TestMethod, Priority(0)]
        public async Task NamedArgumentDefinition() {
            var code = @"class MyClass(object):
    def my_class_func1(self, param2 = True):
        pass

    def my_class_func2(self, param3 = True):
        pass

def my_func(param3 = True):
    pass

def different_func():
    pass

my_func(param3=False)

obj = MyClass()
obj.my_class_func1(param2=False)
obj.my_class_func2(param3=False)
";
            using (var helper = new NavigableHelper(code, Version)) {
                // param3 in my_func(param3=False)
                await helper.CheckDefinitionLocations(232, 6, Location(8, 13));

                // param2 in obj.my_class_func1(param2=False)
                await helper.CheckDefinitionLocations(285, 6, Location(2, 30));

                // param3 in obj.my_class_func2(param3=False)
                await helper.CheckDefinitionLocations(319, 6, Location(5, 30));
            }
        }

        [TestMethod, Priority(0)]
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
                await helper.CheckDefinitionLocations(71, 7, Location(9, 9));
                await helper.CheckDefinitionLocations(128, 7, Location(9, 9));
                await helper.CheckDefinitionLocations(152, 7, Location(9, 9));
                await helper.CheckDefinitionLocations(229, 7, Location(13, 5), Location(9, 9));
                await helper.CheckDefinitionLocations(252, 7, Location(13, 5), Location(9, 9));

                // val
                await helper.CheckDefinitionLocations(166, 3, Location(9, 23));
                await helper.CheckDefinitionLocations(201, 3, Location(9, 23));

                // _my_attr_val
                await helper.CheckDefinitionLocations(28, 12, Location(2, 5));
                await helper.CheckDefinitionLocations(107, 12, Location(10, 14), Location(2, 5));
                await helper.CheckDefinitionLocations(186, 12, Location(10, 14), Location(2, 5));
                await helper.CheckDefinitionLocations(272, 12, Location(10, 14), Location(2, 5));

                // self
                await helper.CheckDefinitionLocations(79, 4, Location(5, 17));
                await helper.CheckDefinitionLocations(102, 4, Location(5, 17));
                await helper.CheckDefinitionLocations(160, 4, Location(9, 17));
                await helper.CheckDefinitionLocations(181, 4, Location(9, 17));
            }
        }

        [TestMethod, Priority(0)]
        public async Task VariableDefinition() {
            var code = @"my_var = 0
my_var = 1
res = my_var * 10
";
            using (var helper = new NavigableHelper(code, Version)) {
                // 2 definitions for my_var
                await helper.CheckDefinitionLocations(30, 6, Location(1, 1), Location(2, 1));
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

            public async Task CheckDefinitionLocations(int pos, int length, params AnalysisLocation[] expectedLocations) {
                var entry = (AnalysisEntry)_view.GetAnalysisEntry();
                entry.Analyzer.WaitForCompleteAnalysis(_ => true);

                var trackingSpan = _view.CurrentSnapshot.CreateTrackingSpan(pos, length, SpanTrackingMode.EdgeInclusive);
                var snapshotSpan = trackingSpan.GetSpan(_view.CurrentSnapshot);
                Console.WriteLine("Finding definition of \"{0}\"", snapshotSpan.GetText());
                var actualLocations = await NavigableSymbolSource.GetDefinitionLocationsAsync(entry, snapshotSpan.Start);
                if (expectedLocations != null) {
                    Assert.IsNotNull(actualLocations);

                    Console.WriteLine($"Actual locations for pos={pos}, length={length}:");
                    foreach (var actualLocation in actualLocations) {
                        Console.WriteLine($"{actualLocation.Line}, {actualLocation.Column}");
                    }

                    Assert.AreEqual(expectedLocations.Length, actualLocations.Length);
                    for (int i = 0; i < expectedLocations.Length; i++) {
                        Assert.AreEqual(expectedLocations[i].Line, actualLocations[i].Line);
                        Assert.AreEqual(expectedLocations[i].Column, actualLocations[i].Column);
                        if (expectedLocations[i].FilePath != null) {
                            Assert.AreEqual(expectedLocations[i].FilePath, Path.GetFileName(actualLocations[i].FilePath));
                        }
                    }
                } else {
                    Assert.AreEqual(0, actualLocations.Length);
                }
            }
        }

        #endregion
    }
}
