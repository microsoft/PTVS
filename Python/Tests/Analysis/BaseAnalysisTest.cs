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

        public ModuleAnalysis ProcessText(string text, PythonLanguageVersion version = PythonLanguageVersion.V27, string[] analysisDirs = null) {
            var sourceUnit = GetSourceUnit(text, "fob");
            var state = CreateAnalyzer(version, analysisDirs);
            var entry = state.AddModule("fob", "fob", null);
            Prepare(entry, sourceUnit, version);
            entry.Analyze(CancellationToken.None);

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
