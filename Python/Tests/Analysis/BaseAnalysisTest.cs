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
using IronPython.Runtime.Types;
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.PyAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    /// <summary>
    /// Base class w/ common infrastructure for analysis unit tests.
    /// </summary>
    public class BaseAnalysisTest {
        public IPythonInterpreter Interpreter;
        public IPythonType PyObjectType, IntType, LongType, BytesType, UnicodeType, FloatType, TypeType, ListType, TupleType, SetType, BoolType, FunctionType, ComplexType, GeneratorType, NoneType, ModuleType;
        public string[] _objectMembers, _functionMembers;
        public string[] _strMembers;
        public string[] _listMembers, _intMembers;

        static BaseAnalysisTest() {
            AnalysisLog.Reset();
            AnalysisLog.ResetTime();
            TestData.Deploy(includeTestData: false);
        }

        public BaseAnalysisTest()
            : this(InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7)).CreateInterpreter()) {
        }

        public BaseAnalysisTest(IPythonInterpreter interpreter) {
            Interpreter = interpreter;
            PyObjectType = Interpreter.GetBuiltinType(BuiltinTypeId.Object);
            Assert.IsNotNull(PyObjectType);
            IntType = Interpreter.GetBuiltinType(BuiltinTypeId.Int);
            LongType = Interpreter.GetBuiltinType(BuiltinTypeId.Long);
            ComplexType = Interpreter.GetBuiltinType(BuiltinTypeId.Complex);
            BytesType = Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
            UnicodeType = Interpreter.GetBuiltinType(BuiltinTypeId.Unicode);
            FloatType = Interpreter.GetBuiltinType(BuiltinTypeId.Float);
            TypeType = Interpreter.GetBuiltinType(BuiltinTypeId.Type);
            ListType = Interpreter.GetBuiltinType(BuiltinTypeId.List);
            TupleType = Interpreter.GetBuiltinType(BuiltinTypeId.Tuple);
            SetType = Interpreter.GetBuiltinType(BuiltinTypeId.Set);
            BoolType = Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
            FunctionType = Interpreter.GetBuiltinType(BuiltinTypeId.Function);
            GeneratorType = Interpreter.GetBuiltinType(BuiltinTypeId.Generator);
            NoneType = Interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
            ModuleType = Interpreter.GetBuiltinType(BuiltinTypeId.Module);

            _objectMembers = PyObjectType.GetMemberNames(IronPythonModuleContext.DontShowClrInstance).ToArray();
            _strMembers = BytesType.GetMemberNames(IronPythonModuleContext.DontShowClrInstance).ToArray();
            _listMembers = ListType.GetMemberNames(IronPythonModuleContext.DontShowClrInstance).ToArray();
            _intMembers = IntType.GetMemberNames(IronPythonModuleContext.DontShowClrInstance).ToArray();
            _functionMembers = FunctionType.GetMemberNames(IronPythonModuleContext.DontShowClrInstance).ToArray();
        }

        public static TextReader GetSourceUnit(string text, string name) {
            return new StringReader(text);
        }

        public static TextReader GetSourceUnit(string text) {
            return GetSourceUnit(text, "foo");
        }

        protected virtual AnalysisLimits GetLimits() {
            return AnalysisLimits.GetDefaultLimits();
        }

        public ModuleAnalysis ProcessText(string text, PythonLanguageVersion version = PythonLanguageVersion.V27, string[] analysisDirs = null, bool useAnalysisLog = false) {
            var sourceUnit = GetSourceUnit(text, "foo");
            // Explicitly provide the builtins name, since we aren't recreating
            // the interpreter for each version like we should be.
            var state = new PythonAnalyzer(Interpreter, version, "__builtin__");

            if (version.Is3x() || this is IronPythonAnalysisTest) {
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
            var entry = state.AddModule("foo", "foo", null);
            Prepare(entry, sourceUnit, version);
            entry.Analyze(CancellationToken.None);

            return entry.Analysis;
        }

        public static void Prepare(IPythonProjectEntry entry, TextReader sourceUnit, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            using (var parser = Parser.CreateParser(sourceUnit, version, new ParserOptions() { BindReferences = true })) {
                entry.UpdateTree(parser.ParseFile(), null);
            }
        }
    }
}
