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
    public class BaseAnalysisTest : IDisposable {
        public IPythonInterpreterFactory InterpreterFactory;
        public IPythonInterpreter Interpreter;
        public string[] _objectMembers, _functionMembers;
        public string[] _strMembers;
        public string[] _listMembers, _intMembers;

        static BaseAnalysisTest() {
            AnalysisLog.Reset();
            AnalysisLog.ResetTime();
            AssertListener.Initialize();
            PythonTestData.Deploy(includeTestData: false);
        }

        public BaseAnalysisTest()
            : this(InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7))) {
        }

        public BaseAnalysisTest(IPythonInterpreterFactory factory)
            : this(factory, factory.CreateInterpreter()) {
        }

        protected virtual IModuleContext DefaultContext {
            get { return null; }
        }

        public BaseAnalysisTest(IPythonInterpreterFactory factory, IPythonInterpreter interpreter) {
            InterpreterFactory = factory;
            Interpreter = interpreter;
            var objectType = Interpreter.GetBuiltinType(BuiltinTypeId.Object);
            Assert.IsNotNull(objectType);
            var intType = Interpreter.GetBuiltinType(BuiltinTypeId.Int);
            var bytesType = Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
            var listType = Interpreter.GetBuiltinType(BuiltinTypeId.List);
            var functionType = Interpreter.GetBuiltinType(BuiltinTypeId.Function);

            _objectMembers = objectType.GetMemberNames(DefaultContext).ToArray();
            _strMembers = bytesType.GetMemberNames(DefaultContext).ToArray();
            _listMembers = listType.GetMemberNames(DefaultContext).ToArray();
            _intMembers = intType.GetMemberNames(DefaultContext).ToArray();
            _functionMembers = functionType.GetMemberNames(DefaultContext).ToArray();
        }

        public static TextReader GetSourceUnit(string text, string name) {
            return new StringReader(text);
        }

        public static TextReader GetSourceUnit(string text) {
            return GetSourceUnit(text, "fob");
        }

        protected virtual AnalysisLimits GetLimits() {
            return AnalysisLimits.GetDefaultLimits();
        }

        protected virtual bool SupportsPython3 {
            get { return true; }
        }

        protected virtual bool ShouldUseUnicodeLiterals(PythonLanguageVersion version) {
            return version.Is3x();
        }

        public PythonAnalyzer CreateAnalyzer(PythonLanguageVersion version = PythonLanguageVersion.V27, string[] analysisDirs = null) {
            // Explicitly provide the builtins name, since we aren't recreating
            // the interpreter for each version like we should be.
            var fact = InterpreterFactory;
            var interp = Interpreter;
            var builtinsName = "__builtin__";
            if (version != fact.GetLanguageVersion()) {
                fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());
                interp = fact.CreateInterpreter();
                builtinsName = null;
            }
            var state = PythonAnalyzer.CreateSynchronously(fact, interp, builtinsName);

            if (ShouldUseUnicodeLiterals(version)) {
                var types = (KnownTypes)state.Types;
                types._types[(int)BuiltinTypeId.Str] = state.Types[BuiltinTypeId.Unicode];
                types._types[(int)BuiltinTypeId.StrIterator] = state.Types[BuiltinTypeId.UnicodeIterator];
                types._classInfos[(int)BuiltinTypeId.Str] = state.ClassInfos[BuiltinTypeId.Unicode];
                types._classInfos[(int)BuiltinTypeId.StrIterator] = state.ClassInfos[BuiltinTypeId.UnicodeIterator];
            }

            state.Limits = GetLimits();
            if (analysisDirs != null) {
                foreach (var dir in analysisDirs) {
                    state.AddAnalysisDirectory(dir);
                }
            }

            return state;
        }

        public ModuleAnalysis ProcessText(
            string text,
            PythonLanguageVersion version = PythonLanguageVersion.V27,
            string[] analysisDirs = null,
            CancellationToken cancel = default(CancellationToken)
        ) {
            var sourceUnit = GetSourceUnit(text, "fob");
            var state = CreateAnalyzer(version, analysisDirs);
            var entry = state.AddModule("fob", "fob", null);
            Prepare(entry, sourceUnit, version);
            entry.Analyze(cancel);

            return entry.Analysis;
        }

        public static void Prepare(IPythonProjectEntry entry, TextReader sourceUnit, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            using (var parser = Parser.CreateParser(sourceUnit, version, new ParserOptions() { BindReferences = true })) {
                entry.UpdateTree(parser.ParseFile(), null);
            }
        }

        #region IDisposable Members

        public void Dispose() {
            IDisposable dispose = Interpreter as IDisposable;
            if (dispose != null) {
                dispose.Dispose();
            }
        }

        #endregion
    }
}
