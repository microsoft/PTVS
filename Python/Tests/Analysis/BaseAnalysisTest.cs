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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.PyAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Python;

namespace AnalysisTests {
    /// <summary>
    /// Base class w/ common infrastructure for analysis unit tests.
    /// </summary>
    public class BaseAnalysisTest {
        private readonly IPythonInterpreterFactory _defaultFactoryV2 = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7));
        private readonly IPythonInterpreterFactory _defaultFactoryV3 = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(3, 5));

        static BaseAnalysisTest() {
            AnalysisLog.Reset();
            AnalysisLog.ResetTime();
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
        }

        [TestInitialize]
        public void StartAnalysisLog() {
            AnalysisLog.Reset();
            AnalysisLog.Output = Console.Out;
        }

        [TestCleanup]
        public void EndAnalysisLog() {
            AnalysisLog.Flush();
            AnalysisLog.Output = null;
        }

        protected virtual IPythonInterpreterFactory DefaultFactoryV2 => _defaultFactoryV2;
        protected virtual IPythonInterpreterFactory DefaultFactoryV3 => _defaultFactoryV3;
        protected virtual bool SupportsPython3 => _defaultFactoryV3 != null;
        protected virtual IModuleContext DefaultContext => null;
        protected virtual AnalysisLimits GetLimits() => AnalysisLimits.GetDefaultLimits();

        protected virtual BuiltinTypeId StrType(PythonAnalysis analyzer) => analyzer.BuiltinTypeId_Str;
        protected virtual BuiltinTypeId StrIteratorType(PythonAnalysis analyzer) => analyzer.BuiltinTypeId_StrIterator;

        public PythonAnalysis CreateAnalyzer(IPythonInterpreterFactory factory) {
            var analysis = new PythonAnalysis(factory) {
                AssertOnParseErrors = true,
                ModuleContext = DefaultContext
            };
            analysis.SetLimits(GetLimits());
            return analysis;
        }

        public PythonAnalysis ProcessTextV2(string text) {
            var analysis = CreateAnalyzer(DefaultFactoryV2);
            analysis.AddModuleAsync("test-module", text, CancellationTokens.After5s).WaitAndUnwrapExceptions();
            return analysis;
        }

        public PythonAnalysis ProcessTextV3(string text) {
            var analysis = CreateAnalyzer(DefaultFactoryV3);
            analysis.AddModuleAsync("test-module", text, CancellationTokens.After5s).WaitAndUnwrapExceptions();
            return analysis;
        }

        public PythonAnalysis ProcessText(string text, PythonLanguageVersion version = PythonLanguageVersion.None) {
            // TODO: Analyze against multiple versions when the version is None
            if (version == PythonLanguageVersion.None) {
                return ProcessTextV2(text);
            }

            var analysis = CreateAnalyzer(InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion()));
            analysis.AddModuleAsync("test-module", text, CancellationTokens.After5s).WaitAndUnwrapExceptions();
            return analysis;
        }
    }
}
