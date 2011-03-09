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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IronPython.Runtime.Types;
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace AnalysisTest {
    /// <summary>
    /// Base class w/ common infrastructure for analysis unit tests.
    /// </summary>
    public class BaseAnalysisTest {
        public IPythonInterpreter Interpreter;
        public IPythonType PyObjectType, IntType, StringType, FloatType, TypeType, ListType, TupleType, BoolType, FunctionType, ComplexType, GeneratorType;
        public string[] _objectMembers, _functionMembers;
        public string[] _strMembers;
        public string[] _listMembers, _intMembers;

        public BaseAnalysisTest()
            : this(new CPythonInterpreter(CPythonInterpreterFactory.MakeDefaultTypeDatabase())) {
        }

        public BaseAnalysisTest(IPythonInterpreter interpreter) {
            Interpreter = interpreter;
            PyObjectType = Interpreter.GetBuiltinType(BuiltinTypeId.Object);
            IntType = Interpreter.GetBuiltinType(BuiltinTypeId.Int);
            ComplexType = Interpreter.GetBuiltinType(BuiltinTypeId.Complex);
            StringType = Interpreter.GetBuiltinType(BuiltinTypeId.Str);
            FloatType = Interpreter.GetBuiltinType(BuiltinTypeId.Float);
            TypeType = Interpreter.GetBuiltinType(BuiltinTypeId.Type);
            ListType = Interpreter.GetBuiltinType(BuiltinTypeId.List);
            TupleType = Interpreter.GetBuiltinType(BuiltinTypeId.Tuple);
            BoolType = Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
            FunctionType = Interpreter.GetBuiltinType(BuiltinTypeId.Function);
            GeneratorType = Interpreter.GetBuiltinType(BuiltinTypeId.Generator);

            _objectMembers = PyObjectType.GetMemberNames(IronPythonModuleContext.DontShowClrInstance).ToArray();
            _strMembers = StringType.GetMemberNames(IronPythonModuleContext.DontShowClrInstance).ToArray();
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

        public ModuleAnalysis ProcessText(string text, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            var sourceUnit = GetSourceUnit(text, "foo");
            var state = new PythonAnalyzer(Interpreter, version);
            var entry = state.AddModule("foo", "foo", null);
            Prepare(entry, sourceUnit, version);
            entry.Analyze();

            return entry.Analysis;
        }

        public static void Prepare(IPythonProjectEntry entry, TextReader sourceUnit, PythonLanguageVersion version = PythonLanguageVersion.V27) {
            CollectingErrorSink errorSink = new CollectingErrorSink();
            using (var parser = Parser.CreateParser(sourceUnit, errorSink, version)) {
                entry.UpdateTree(parser.ParseFile(), null);
            }
        }

        public void AssertContains<T>(IEnumerable<T> source, T value) {
            foreach (var v in source) {
                if (v.Equals(value)) {
                    return;
                }
            }

            Assert.Fail(String.Format("{0} does not contain {1}", MakeText(source), value));
        }

        public void AssertDoesntContain<T>(IEnumerable<T> source, T value) {
            foreach (var v in source) {
                if (v.Equals(value)) {
                    Assert.Fail(String.Format("{0} does not contain {1}", MakeText(source), value));
                }
            }

        }

        public void AssertContainsExactly<T>(IEnumerable<T> source, params T[] values) {
            AssertContainsExactly(new HashSet<T>(source), values);
        }

        public void AssertContainsExactly<T>(HashSet<T> set, params T[] values) {
            if (set.ContainsExactly(values)) {
                return;
            }
            Assert.Fail(String.Format("Expected {0}, got {1}", MakeText(values), MakeText(set)));
        }

        public string MakeText<T>(IEnumerable<T> values) {
            var sb = new StringBuilder("{");
            foreach (var value in values) {
                if (sb.Length > 1) {
                    sb.Append(", ");
                }
                if (value is PythonType) {
                    sb.AppendFormat("Type({0})", PythonType.Get__name__((PythonType)(object)value));
                } else {
                    sb.Append(value.ToString());
                }
            }
            sb.Append("}");
            return sb.ToString();
        }
    }
}
