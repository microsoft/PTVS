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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;

namespace AnalysisTests {
    [TestClass]
    public class TypeAnnotationTests : BaseAnalysisTest {
        internal static TypeAnnotation Parse(string expr, PythonLanguageVersion version = PythonLanguageVersion.V36) {
            var errors = new CollectingErrorSink();
            var ops = new ParserOptions { ErrorSink = errors };
            using (var p = Parser.CreateParser(new StringReader(expr), version, ops)) {
                var ast = p.ParseTopExpression();
                if (errors.Errors.Any()) {
                    foreach (var e in errors.Errors) {
                        Console.WriteLine(e);
                    }
                    Assert.Fail(string.Join("\n", errors.Errors.Select(e => e.ToString())));
                    return null;
                }
                var node = Statement.GetExpression(ast.Body);
                return new TypeAnnotation(version, node);
            }
        }

        private static void AssertTransform(string expr, params string[] steps) {
            var ta = Parse(expr);
            AssertUtil.AreEqual(ta.GetTransformSteps(), steps);
        }

        private static void AssertConvert(string expr, string expected = null) {
            var ta = Parse(expr);
            var actual = ta.GetValue(new StringConverter());
            Assert.AreEqual(expected ?? expr, actual);
        }

        [TestMethod, Priority(0)]
        public void AnnotationParsing() {
            AssertTransform("List", "NameOp:List");
            AssertTransform("List[Int]", "NameOp:List", "NameOp:Int", "MakeGenericOp");
            AssertTransform("Dict[Int, Str]", "NameOp:Dict", "StartUnionOp", "NameOp:Int", "NameOp:Str", "MakeGenericOp");

            AssertTransform("'List'", "NameOp:List");
            AssertTransform("List['Int']", "NameOp:List", "NameOp:Int", "MakeGenericOp");
            AssertTransform("Dict['Int, Str']", "NameOp:Dict", "StartUnionOp", "NameOp:Int", "NameOp:Str", "MakeGenericOp");
        }

        [TestMethod, Priority(0)]
        public void AnnotationConversion() {
            AssertConvert("List");
            AssertConvert("List[Int]");
            AssertConvert("Dict[Int, Str]");
            AssertConvert("typing.Container[typing.Iterable]");

            AssertConvert("List");
            AssertConvert("'List[Int]'", "List[Int]");
            AssertConvert("Dict['Int, Str']", "Dict[Int, Str]");
            AssertConvert("typing.Container['typing.Iterable']", "typing.Container[typing.Iterable]");
        }

        private class StringConverter : TypeAnnotationConverter<string> {
            public override string LookupName(string name) => name;
            public override string GetTypeMember(string baseType, string member) => $"{baseType}.{member}";
            public override string MakeUnion(IReadOnlyList<string> types) => string.Join(", ", types);
            public override string MakeGeneric(string baseType, IReadOnlyList<string> args) => $"{baseType}[{string.Join(", ", args)}]";

            public override IReadOnlyList<string> GetUnionTypes(string unionType) => unionType.Split(',').Select(n => n.Trim()).ToArray();

            public override string GetBaseType(string genericType) {
                int i = genericType.IndexOf('[');
                if (i < 0) {
                    return null;
                }

                return genericType.Remove(i);
            }

            public override IReadOnlyList<string> GetGenericArguments(string genericType) {
                if (!genericType.EndsWith("]")) {
                    return null;
                }

                int i = genericType.IndexOf('[');
                if (i < 0) {
                    return null;
                }

                return genericType.Substring(i + 1, genericType.Length - i - 2).Split(',').Select(n => n.Trim()).ToArray();
            }
        }

        [TestMethod, Priority(0)]
        public void TypingModuleAnalysis() {
            var python = (PythonPaths.Python36_x64 ?? PythonPaths.Python36);
            python.AssertInstalled();
            var analyzer = CreateAnalyzer(
                new AstPythonInterpreterFactory(python.Configuration, new InterpreterFactoryCreationOptions { PackageManager = null, WatchFileSystem = false })
            );
            analyzer.AddModule("test-module", @"from typing import *

i : SupportsInt = ...
lst : List = ...
lst_i : List[int] = ...
lst_i_0 = lst_i[0]
dct : Union[Mapping, MappingView, MutableMapping] = ...
dct_s_i : Mapping[str, int] = ...
dct_s_i_a = dct_s_i['a']
dct_s_i_key = next(dct_s_i.keys())
dct_s_i_value = next(dct_s_i.values())
");
            analyzer.WaitForAnalysis();

            analyzer.AssertIsInstance("i", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("lst", BuiltinTypeId.List);
            analyzer.AssertIsInstance("lst_i", BuiltinTypeId.List);
            analyzer.AssertIsInstance("lst_i_0", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("dct", BuiltinTypeId.Dict);
            analyzer.AssertIsInstance("dct_s_i", BuiltinTypeId.Dict);
            analyzer.AssertIsInstance("dct_s_i_a", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("dct_s_i_key", BuiltinTypeId.Str);
            analyzer.AssertIsInstance("dct_s_i_value", BuiltinTypeId.Int);
        }
    }
}
