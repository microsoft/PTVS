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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Navigation.Navigable;
using Microsoft.PythonTools.Parsing;
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

        [Ignore] // TODO: Figure out why we don't get any definition, this works in IDE
        [TestMethod, Priority(1)]
        public async Task ModuleDefinition() {
            var code = @"import os
";
            using (var helper = new NavigableHelper(code, PythonLanguageVersion.V27)) {
                // os
                await helper.CheckDefinitionLocation(7, 2, ExternalLocation(1, 1, "os.py"));
            }
        }

        [TestMethod, Priority(1)]
        public async Task ModuleImportDefinition() {
            var code = @"import sys

sys.version
";
            using (var helper = new NavigableHelper(code, PythonLanguageVersion.V27)) {
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
            using (var helper = new NavigableHelper(code, PythonLanguageVersion.V27)) {
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
            using (var helper = new NavigableHelper(code, PythonLanguageVersion.V27)) {
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
            using (var helper = new NavigableHelper(code, PythonLanguageVersion.V27)) {
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
            using (var helper = new NavigableHelper(code, PythonLanguageVersion.V27)) {
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
            new AnalysisLocation("file.py", line, col);

        private AnalysisLocation ExternalLocation(int line, int col, string filename) =>
            new AnalysisLocation(filename, line, col);

        #region NavigableHelper class

        private class NavigableHelper : IDisposable {
            private readonly PythonClassifierProvider _provider1;
            private readonly PythonAnalysisClassifierProvider _provider2;
            private readonly ManualResetEventSlim _classificationsReady1, _classificationsReady2;
            private readonly PythonEditor _view;

            public NavigableHelper(string code, PythonLanguageVersion version) {
                _view = new PythonEditor("", version);

                var providers = _view.VS.ComponentModel.GetExtensions<IClassifierProvider>().ToArray();
                _provider1 = providers.OfType<PythonClassifierProvider>().Single();
                _provider2 = providers.OfType<PythonAnalysisClassifierProvider>().Single();

                _classificationsReady1 = new ManualResetEventSlim();
                _classificationsReady2 = new ManualResetEventSlim();

                AstClassifier.ClassificationChanged += (s, e) => SafeSetEvent(_classificationsReady1);
                var startVersion = _view.CurrentSnapshot.Version;
                AnalysisClassifier.ClassificationChanged += (s, e) => {
                    var entry = (AnalysisEntry)_view.GetAnalysisEntry();
                    // make sure we have classifications from the version we analyzed after
                    // setting the text below.
                    if (entry.GetAnalysisVersion(_view.CurrentSnapshot.TextBuffer).VersionNumber > startVersion.VersionNumber) {
                        SafeSetEvent(_classificationsReady2);
                    }
                };

                _view.Text = code;
            }

            private static void SafeSetEvent(ManualResetEventSlim evt) {
                try {
                    evt.Set();
                } catch (ObjectDisposedException) {
                }
            }

            public void Dispose() {
                _classificationsReady1.Dispose();
                _classificationsReady2.Dispose();
                _view.Dispose();
            }

            public ITextBuffer TextBuffer {
                get {
                    return _view.View.TextView.TextBuffer;
                }
            }

            public IClassifier AstClassifier {
                get {
                    return _provider1.GetClassifier(TextBuffer);
                }
            }

            public IClassifier AnalysisClassifier {
                get {
                    return _provider2.GetClassifier(TextBuffer);
                }
            }

            public async Task CheckDefinitionLocation(int pos, int length, AnalysisLocation expected) {
                _classificationsReady1.Wait();
                _classificationsReady2.Wait();

                var entry = (AnalysisEntry)_view.GetAnalysisEntry();
                var trackingSpan = _view.CurrentSnapshot.CreateTrackingSpan(pos, length, SpanTrackingMode.EdgeInclusive);
                var snapshotSpan = trackingSpan.GetSpan(_view.CurrentSnapshot);
                var result = await NavigableSymbolSource.GetDefinitionLocationAsync(entry, _view.View.TextView, snapshotSpan);
                if (expected != null) {
                    Assert.IsNotNull(result);
                    Assert.AreEqual(expected.Line, result.Line);
                    Assert.AreEqual(expected.Column, result.Column);
                    Assert.AreEqual(expected.FilePath, Path.GetFileName(result.FilePath));
                } else {
                    Assert.IsNull(result);
                }
            }
        }

        #endregion
    }
}
