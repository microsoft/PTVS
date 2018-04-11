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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using TestUtilities;
using TestUtilities.Python;

namespace AnalysisTests {
    /// <summary>
    /// Base class w/ common infrastructure for analysis unit tests.
    /// </summary>
    public class BaseAnalysisTest {
        private readonly IPythonInterpreterFactory _defaultFactoryV2 = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7));
        private readonly IPythonInterpreterFactory _defaultFactoryV3 = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(3, 6));

        private List<IDisposable> _toDispose;

        static BaseAnalysisTest() {
            AnalysisLog.Reset();
        }

        public void StartAnalysisLog() {
            AnalysisLog.Reset();
            AnalysisLog.OutputToConsole = true;
        }

        public void EndAnalysisLog() {
            AnalysisLog.Flush();
            AnalysisLog.OutputToConsole = false;
            foreach (var d in _toDispose.MaybeEnumerate()) {
                d.Dispose();
            }
            _toDispose = null;
        }

        protected virtual IPythonInterpreterFactory DefaultFactoryV2 => _defaultFactoryV2;
        protected virtual IPythonInterpreterFactory DefaultFactoryV3 => _defaultFactoryV3;
        protected virtual bool SupportsPython3 => _defaultFactoryV3 != null;
        protected virtual IModuleContext DefaultContext => null;
        protected virtual AnalysisLimits GetLimits() => AnalysisLimits.GetDefaultLimits();

        protected virtual PythonAnalysis CreateAnalyzerInternal(IPythonInterpreterFactory factory) {
            return new PythonAnalysis(factory);
        }

        public PythonAnalysis CreateAnalyzer(IPythonInterpreterFactory factory = null, bool allowParseErrors = false) {
            var analysis = CreateAnalyzerInternal(factory ?? DefaultFactoryV2);
            analysis.AssertOnParseErrors = !allowParseErrors;
            analysis.Analyzer.EnableDiagnostics = true;
            analysis.ModuleContext = DefaultContext;
            analysis.SetLimits(GetLimits());

            if (_toDispose == null) {
                _toDispose = new List<IDisposable>();
            }
            _toDispose.Add(analysis);

            return analysis;
        }

        public PythonAnalysis ProcessTextV2(string text, bool allowParseErrors = false) {
            var analysis = CreateAnalyzer(DefaultFactoryV2, allowParseErrors);
            analysis.AddModule("test-module", text).WaitForCurrentParse();
            analysis.WaitForAnalysis();
            return analysis;
        }

        public PythonAnalysis ProcessTextV3(string text, bool allowParseErrors = false) {
            var analysis = CreateAnalyzer(DefaultFactoryV3, allowParseErrors);
            analysis.AddModule("test-module", text).WaitForCurrentParse();
            analysis.WaitForAnalysis();
            return analysis;
        }

        public PythonAnalysis ProcessText(
            string text,
            PythonLanguageVersion version = PythonLanguageVersion.None,
            bool allowParseErrors = false
        ) {
            // TODO: Analyze against multiple versions when the version is None
            if (version == PythonLanguageVersion.None) {
                return ProcessTextV2(text, allowParseErrors);
            }

            var analysis = CreateAnalyzer(InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion()), allowParseErrors);
            analysis.AddModule("test-module", text).WaitForCurrentParse();
            analysis.WaitForAnalysis();
            return analysis;
        }
    }
}
