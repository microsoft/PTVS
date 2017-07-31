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
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudioTools.MockVsTests;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsMockTests {
    [TestClass]
    public class NavigableTests {
        private MockVs _vs;

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
        }

        [TestInitialize]
        public void TestInit() {
            MockPythonToolsPackage.SuppressTaskProvider = true;
            VsProjectAnalyzer.SuppressTaskProvider = true;
            _vs = new MockVs();
        }

        [TestCleanup]
        public void TestCleanup() {
            MockPythonToolsPackage.SuppressTaskProvider = false;
            VsProjectAnalyzer.SuppressTaskProvider = false;
            _vs.Dispose();
        }

        [Ignore] // This works in the IDE, need to figure out why it doesn't here
        [TestMethod, Priority(1)]
        public async Task ModuleDefinition() {
            var code = @"import os
";
            using (var helper = new ClassifierHelper(_vs.ServiceProvider, code, PythonLanguageVersion.V27)) {
                // os
                await helper.CheckDefinitionLocation(7, 2, new AnalysisLocation("os.py", 1, 1));
            }
        }

        [TestMethod, Priority(1)]
        public async Task ModuleImportDefinition() {
            var code = @"import sys

sys.version
";
            using (var helper = new ClassifierHelper(_vs.ServiceProvider, code, PythonLanguageVersion.V27)) {
                // sys
                await helper.CheckDefinitionLocation(15, 3, new AnalysisLocation("", 1, 8));
            }
        }

        [TestMethod, Priority(1)]
        public async Task ClassDefinition() {
            var code = @"class ClassBase(object):
    pass

class ClassDerived(ClassBase):
    pass

obj = ClassDerived()
";
            using (var helper = new ClassifierHelper(_vs.ServiceProvider, code, PythonLanguageVersion.V27)) {
                // ClassBase
                await helper.CheckDefinitionLocation(57, 9, new AnalysisLocation("", 1, 7));
                // ClassDerived
                await helper.CheckDefinitionLocation(88, 12, new AnalysisLocation("", 4, 7));
            }
        }

        [TestMethod, Priority(1)]
        public async Task ParameterDefinition() {
            var code = @"def my_func(param1, param2 = True):
    print(param1)
    print(param2)

my_func(1)
";
            using (var helper = new ClassifierHelper(_vs.ServiceProvider, code, PythonLanguageVersion.V27)) {
                // param1
                await helper.CheckDefinitionLocation(47, 6, new AnalysisLocation("", 1, 13));
                // param2
                await helper.CheckDefinitionLocation(66, 6, new AnalysisLocation("", 1, 21));
                // my_func
                await helper.CheckDefinitionLocation(77, 7, new AnalysisLocation("", 1, 5));
            }
        }

        [TestMethod, Priority(1)]
        public async Task ConfusingParameterDefinition() {
            var code = @"#TODO
";
            using (var helper = new ClassifierHelper(_vs.ServiceProvider, code, PythonLanguageVersion.V27)) {
                // TODO: goes to wrong parameter when a class method and global function have same parameter name
                // at call site where keyword parameter is used
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
            using (var helper = new ClassifierHelper(_vs.ServiceProvider, code, PythonLanguageVersion.V27)) {
                // my_attr in obj.my_attr = 5
                await helper.CheckDefinitionLocation(229, 7, new AnalysisLocation("", 13, 5));
                // my_attr in print(obj.my_attr)
                await helper.CheckDefinitionLocation(252, 7, new AnalysisLocation("", 13, 5));
                // _my_attr_val in print(obj._my_attr_val)
                await helper.CheckDefinitionLocation(272, 12, new AnalysisLocation("", 10, 14));
            }
        }

        #region ClassifierHelper class

        private class ClassifierHelper : IDisposable {
            private readonly IServiceProvider _serviceProvider;
            private readonly PythonClassifierProvider _provider1;
            private readonly PythonAnalysisClassifierProvider _provider2;
            private readonly ManualResetEventSlim _classificationsReady1, _classificationsReady2;
            private readonly PythonEditor _view;

            public ClassifierHelper(IServiceProvider serviceProvider, string code, PythonLanguageVersion version) {
                _serviceProvider = serviceProvider;
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

            public ITextView TextView {
                get {
                    return _view.View.TextView;
                }
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

            public IEnumerable<ClassificationSpan> AstClassifierSpans {
                get {
                    _classificationsReady1.Wait();
                    return AstClassifier.GetClassificationSpans(
                        new SnapshotSpan(TextBuffer.CurrentSnapshot, 0, TextBuffer.CurrentSnapshot.Length)
                    ).OrderBy(s => s.Span.Start.Position);
                }
            }

            public IClassifier AnalysisClassifier {
                get {
                    return _provider2.GetClassifier(TextBuffer);
                }
            }

            public IEnumerable<ClassificationSpan> AnalysisClassifierSpans {
                get {
                    _classificationsReady2.Wait();
                    return AnalysisClassifier.GetClassificationSpans(
                        new SnapshotSpan(TextBuffer.CurrentSnapshot, 0, TextBuffer.CurrentSnapshot.Length)
                    ).OrderBy(s => s.Span.Start.Position);
                }
            }

            public async Task CheckDefinitionLocation(int position, int length, AnalysisLocation expectedLocation) {
                _classificationsReady1.Wait();
                _classificationsReady2.Wait();

                var entry = (AnalysisEntry)_view.GetAnalysisEntry();
                var span = _view.CurrentSnapshot.CreateTrackingSpan(position, length, SpanTrackingMode.EdgeInclusive).GetSpan(_view.CurrentSnapshot);
                var result = await NavigableSymbolSource.GetDefinitionLocationAsync(entry, _view.View.TextView, span);
                if (expectedLocation != null) {
                    Assert.IsNotNull(result);
                    Assert.AreEqual(expectedLocation.Line, result.Line);
                    Assert.AreEqual(expectedLocation.Column, result.Column);
                    if (string.IsNullOrEmpty(expectedLocation.FilePath)) {
                        Assert.AreEqual("file.py", Path.GetFileName(result.FilePath));
                    } else {
                        Assert.AreEqual(expectedLocation.FilePath, Path.GetFileName(result.FilePath));
                    }
                } else {
                    Assert.IsNull(result);
                }
            }
        }

        #endregion
    }
}
