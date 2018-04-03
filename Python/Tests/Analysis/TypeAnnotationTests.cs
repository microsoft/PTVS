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
        [TestInitialize]
        public void TestInitialize() {
            StartAnalysisLog();
        }

        [TestCleanup]
        public void TestCleanup() {
            EndAnalysisLog();
            AssertListener.ThrowUnhandled();
        }

        internal static TypeAnnotation Parse(string expr, PythonLanguageVersion version = PythonLanguageVersion.V36) {
            var errors = new CollectingErrorSink();
            var ops = new ParserOptions { ErrorSink = errors };
            var p = Parser.CreateParser(new StringReader(expr), version, ops);
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
            AssertTransform("Dict[Int, Str]", "NameOp:Dict", "StartListOp", "NameOp:Int", "NameOp:Str", "MakeGenericOp");

            AssertTransform("'List'", "NameOp:List");
            AssertTransform("List['Int']", "NameOp:List", "NameOp:Int", "MakeGenericOp");
            AssertTransform("Dict['Int, Str']", "NameOp:Dict", "StartListOp", "NameOp:Int", "NameOp:Str", "MakeGenericOp");
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
        public void TypingModuleContainerAnalysis() {
            var python = (PythonPaths.Python36_x64 ?? PythonPaths.Python36);
            python.AssertInstalled();
            var analyzer = CreateAnalyzer(
                new AstPythonInterpreterFactory(python.Configuration, new InterpreterFactoryCreationOptions { WatchFileSystem = false })
            );
            analyzer.AddModule("test-module", @"from typing import *

i : SupportsInt = ...
lst : List = ...
lst_i : List[int] = ...
lst_i_0 = lst_i[0]
dct : Union[Mapping, MappingView, MutableMapping] = ...
dct_s_i : Mapping[str, int] = ...
dct_s_i_a = dct_s_i['a']
dct_s_i_keys = dct_s_i.keys()
dct_s_i_key = next(iter(dct_s_i_keys))
dct_s_i_values = dct_s_i.values()
dct_s_i_value = next(iter(dct_s_i_values))
dct_s_i_items = dct_s_i.items()
dct_s_i_item_1, dct_s_i_item_2 = next(iter(dct_s_i_items))

dctv_s_i_keys : KeysView[str] = ...
dctv_s_i_key = next(iter(dctv_s_i_keys))
dctv_s_i_values : ValuesView[int] = ...
dctv_s_i_value = next(iter(dctv_s_i_values))
dctv_s_i_items : ItemsView[str, int] = ...
dctv_s_i_item_1, dctv_s_i_item_2 = next(iter(dctv_s_i_items))
");
            analyzer.WaitForAnalysis();

            Assert.IsTrue(analyzer.Analyzer.Modules.TryGetImportedModule("typing", out var mod));
            Assert.IsInstanceOfType(mod.AnalysisModule.Single(), typeof(Microsoft.PythonTools.Analysis.Values.TypingModuleInfo));

            analyzer.AssertIsInstance("i", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("lst", BuiltinTypeId.List);
            analyzer.AssertIsInstance("lst_i", BuiltinTypeId.List);
            analyzer.AssertIsInstance("lst_i_0", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("dct", BuiltinTypeId.Dict);
            analyzer.AssertIsInstance("dct_s_i", BuiltinTypeId.Dict);
            analyzer.AssertDescription("dct_s_i", "dict[str, int]");
            analyzer.AssertIsInstance("dct_s_i_a", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("dct_s_i_keys", BuiltinTypeId.DictKeys);
            analyzer.AssertDescription("dct_s_i_keys", "dict_keys[str]");
            analyzer.AssertIsInstance("dct_s_i_key", BuiltinTypeId.Str);
            analyzer.AssertIsInstance("dct_s_i_values", BuiltinTypeId.DictValues);
            analyzer.AssertDescription("dct_s_i_values", "dict_values[int]");
            analyzer.AssertIsInstance("dct_s_i_value", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("dct_s_i_items", BuiltinTypeId.DictItems);
            analyzer.AssertDescription("dct_s_i_items", "dict_items[tuple[str, int]]");
            analyzer.AssertIsInstance("dct_s_i_item_1", BuiltinTypeId.Str);
            analyzer.AssertIsInstance("dct_s_i_item_2", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("dctv_s_i_keys", BuiltinTypeId.DictKeys);
            analyzer.AssertIsInstance("dctv_s_i_key", BuiltinTypeId.Str);
            analyzer.AssertIsInstance("dctv_s_i_values", BuiltinTypeId.DictValues);
            analyzer.AssertIsInstance("dctv_s_i_value", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("dctv_s_i_items", BuiltinTypeId.DictItems);
            analyzer.AssertIsInstance("dctv_s_i_item_1", BuiltinTypeId.Str);
            analyzer.AssertIsInstance("dctv_s_i_item_2", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void TypingModuleProtocolAnalysis() {
            var python = (PythonPaths.Python36_x64 ?? PythonPaths.Python36);
            python.AssertInstalled();
            var analyzer = CreateAnalyzer(
                new AstPythonInterpreterFactory(python.Configuration, new InterpreterFactoryCreationOptions { WatchFileSystem = false })
            );
            analyzer.AddModule("test-module", @"from typing import *

i : Iterable = ...
ii : Iterator = ...
i_int : Iterable[int] = ...
ii_int : Iterator[int] = ...
g_int : Generator[int] = ...

call_i_s : Callable[[int], str] = ...
call_i_s_ret = call_i_s()
call_iis_i : Callable[[int, int, str], int] = ...
call_iis_i_ret = call_iis_i()
");
            analyzer.WaitForAnalysis();

            analyzer.AssertDescription("i", "iterable");
            analyzer.AssertDescription("ii", "iterator");
            analyzer.AssertDescription("i_int", "iterable[int]");
            analyzer.AssertDescription("ii_int", "iterator[int]");
            analyzer.AssertDescription("g_int", "generator[int]");

            analyzer.AssertIsInstance("call_i_s", BuiltinTypeId.Function);
            analyzer.AssertIsInstance("call_i_s_ret", BuiltinTypeId.Str);
            analyzer.AssertIsInstance("call_iis_i", BuiltinTypeId.Function);
            analyzer.AssertIsInstance("call_iis_i_ret", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void TypingModuleNamedTupleAnalysis() {
            var python = (PythonPaths.Python36_x64 ?? PythonPaths.Python36);
            python.AssertInstalled();
            var analyzer = CreateAnalyzer(
                new AstPythonInterpreterFactory(python.Configuration, new InterpreterFactoryCreationOptions { WatchFileSystem = false })
            );
            analyzer.AddModule("test-module", @"from typing import *

n : NamedTuple = ...
n1 : NamedTuple('n1', [('x', int), ['y', float]]) = ...
n2 : ""NamedTuple('n2', [('x', int), ['y', float]])"" = ...

n1_x = n1.x
n1_y = n1.y
n2_x = n2.x
n2_y = n2.y

n1_0 = n1[0]
n1_1 = n1[1]
n2_0 = n2[0]
n2_1 = n2[1]

n1_m2 = n1[-2]
n1_m1 = n1[-1]
n2_m2 = n2[-2]
n2_m1 = n2[-1]

i = 0
i = 1
n1_i = n1[i]
n2_i = n2[i]
");
            analyzer.WaitForAnalysis();

            analyzer.AssertDescription("n", "tuple");
            analyzer.AssertDescription("n1", "n1(x : int, y : float)");
            analyzer.AssertDescription("n2", "n2(x : int, y : float)");

            analyzer.AssertIsInstance("n1_x", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("n1_y", BuiltinTypeId.Float);
            analyzer.AssertIsInstance("n2_x", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("n2_y", BuiltinTypeId.Float);

            analyzer.AssertIsInstance("n1_0", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("n1_1", BuiltinTypeId.Float);
            analyzer.AssertIsInstance("n2_0", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("n2_1", BuiltinTypeId.Float);

            analyzer.AssertIsInstance("n1_m2", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("n1_m1", BuiltinTypeId.Float);
            analyzer.AssertIsInstance("n2_m2", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("n2_m1", BuiltinTypeId.Float);

            analyzer.AssertIsInstance("n1_i", BuiltinTypeId.Int, BuiltinTypeId.Float);
            analyzer.AssertIsInstance("n2_i", BuiltinTypeId.Int, BuiltinTypeId.Float);
        }

        [TestMethod, Priority(0)]
        public void TypingModuleNamedTypeAlias() {
            var python = (PythonPaths.Python36_x64 ?? PythonPaths.Python36);
            python.AssertInstalled();
            var analyzer = CreateAnalyzer(
                new AstPythonInterpreterFactory(python.Configuration, new InterpreterFactoryCreationOptions { WatchFileSystem = false })
            );
            analyzer.AddModule("test-module", @"from typing import *

MyInt = int
MyStrList = List[str]
MyNamedTuple = NamedTuple('MyNamedTuple', [('x', MyInt)])

i : MyInt = ...
sl : MyStrList = ...
sl_0 = sl[0]
n1 : MyNamedTuple = ...
");
            analyzer.WaitForAnalysis();

            analyzer.AssertIsInstance("i", BuiltinTypeId.Int);
            analyzer.AssertIsInstance("sl", BuiltinTypeId.List);
            analyzer.AssertIsInstance("sl_0", BuiltinTypeId.Str);
            analyzer.AssertDescription("n1", "MyNamedTuple(x : int)");

            analyzer.AssertIsInstance("n1.x", BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void TypingModuleNestedIndex() {
            var python = (PythonPaths.Python36_x64 ?? PythonPaths.Python36);
            python.AssertInstalled();
            var analyzer = CreateAnalyzer(
                new AstPythonInterpreterFactory(python.Configuration, new InterpreterFactoryCreationOptions { WatchFileSystem = false })
            );
            analyzer.AddModule("test-module", @"from typing import *

MyList = List[List[str]]

l_l_s : MyList = ...
l_s = l_l_s[0]
s = l_s[0]
");
            analyzer.WaitForAnalysis();

            analyzer.AssertIsInstance("l_l_s", BuiltinTypeId.List);
            analyzer.AssertIsInstance("l_s", BuiltinTypeId.List);
            analyzer.AssertIsInstance("s", BuiltinTypeId.Str);
        }

        [TestMethod, Priority(0)]
        public void TypingModuleGenerator() {
            var python = (PythonPaths.Python36_x64 ?? PythonPaths.Python36);
            python.AssertInstalled();
            var analyzer = CreateAnalyzer(
                new AstPythonInterpreterFactory(python.Configuration, new InterpreterFactoryCreationOptions { WatchFileSystem = false })
            );
            var code = @"from typing import *

gen : Generator[str, None, int] = ...

def g():
    x = yield from gen

g_g = g()
g_i = next(g_g)
";
            analyzer.AddModule("test-module", code);
            analyzer.WaitForAnalysis();

            analyzer.AssertIsInstance("g_g", BuiltinTypeId.Generator);
            analyzer.AssertIsInstance("g_i", BuiltinTypeId.Str);
            analyzer.AssertIsInstance("x", code.IndexOf("x ="), BuiltinTypeId.Int);
        }

        [TestMethod, Priority(0)]
        public void FunctionAnnotation() {
            var python = (PythonPaths.Python36_x64 ?? PythonPaths.Python36);
            python.AssertInstalled();
            var analyzer = CreateAnalyzer(
                new AstPythonInterpreterFactory(python.Configuration, new InterpreterFactoryCreationOptions { WatchFileSystem = false })
            );
            var code = @"
def f(a : int, b : float) -> str: pass

x = f()
";
            analyzer.AddModule("test-module", code);
            analyzer.WaitForAnalysis();

            var sigs = analyzer.GetSignatures("f").Single();
            Assert.AreEqual("a : int, b : float", string.Join(", ", sigs.Parameters.Select(p => $"{p.Name} : {p.Type}")));
            analyzer.AssertIsInstance("x", BuiltinTypeId.Str);
        }

        private void TypingModuleDocumentationExample(string code, IEnumerable<string> signatures) {
            var python = (PythonPaths.Python36_x64 ?? PythonPaths.Python36);
            python.AssertInstalled();
            var analyzer = CreateAnalyzer(
                new AstPythonInterpreterFactory(python.Configuration, new InterpreterFactoryCreationOptions { WatchFileSystem = false })
            );

            analyzer.AddModule("test-module", code);
            analyzer.WaitForAnalysis();

            foreach (var sig in signatures) {
                int i = sig.IndexOf(':');
                Assert.AreNotEqual(-1, i, sig);
                var f = analyzer.GetSignatures(sig.Substring(0, i));
                var actualSig = string.Join("|", f.Select(o => o.ToString()));

                Console.WriteLine("Expected: {0}", sig.Substring(i + 1));
                Console.WriteLine("Actual:   {0}", actualSig);

                Assert.AreEqual(sig.Substring(i + 1), actualSig);
            }
        }

        [TestMethod, Priority(0)]
        public void TypingModuleDocumentationExample_1() {
            TypingModuleDocumentationExample(@"def greeting(name: str) -> str:
    return 'Hello ' + name
", 
                new[] {
                    "greeting:greeting(name:str=)->[str]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public void TypingModuleDocumentationExample_2() {
            TypingModuleDocumentationExample(@"from typing import List
Vector = List[float]

def scale(scalar: float, vector: Vector) -> Vector:
    return [scalar * num for num in vector]

# typechecks; a list of floats qualifies as a Vector.
new_vector = scale(2.0, [1.0, -4.2, 5.4])
",
                new[] {
                    "scale:scale(scalar:float=,vector:list, list[float]=)->[list,list[float]]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public void TypingModuleDocumentationExample_3() {
            TypingModuleDocumentationExample(@"from typing import Dict, Tuple, List

ConnectionOptions = Dict[str, str]
Address = Tuple[str, int]
Server = Tuple[Address, ConnectionOptions]

def broadcast_message(message: str, servers: List[Server]) -> None:
    ...

# The static type checker will treat the previous type signature as
# being exactly equivalent to this one.
def broadcast_message(
        message: str,
        servers: List[Tuple[Tuple[str, int], Dict[str, str]]]) -> None:
    ...
",
                new[] {
                    // Two matching functions means only one overload is returned
                    "broadcast_message:broadcast_message(message:str=,servers:list[tuple[tuple[str, int], dict[str, str]]]=)->[]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public void TypingModuleDocumentationExample_4() {
            TypingModuleDocumentationExample(@"from typing import NewType

UserId = NewType('UserId', int)
some_id = UserId(524313)

def get_user_name(user_id: UserId) -> str:
    ...

# typechecks
user_a = get_user_name(UserId(42351))

# does not typecheck; an int is not a UserId
user_b = get_user_name(-1)
",
                new[] {
                    "get_user_name:get_user_name(user_id:int, UserId=)->[str]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public void TypingModuleDocumentationExample_5() {
            TypingModuleDocumentationExample(@"from typing import NewType

UserId = NewType('UserId', int)

# Fails at runtime and does not typecheck
class AdminUserId(UserId): pass

ProUserId = NewType('ProUserId', UserId)

def f(u : UserId, a : AdminUserId, p : ProUserId):
    return p
",
                new[] {
                    "f:f(u:UserId=,a:AdminUserId=,p:ProUserId=)->[ProUserId]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public void TypingModuleDocumentationExample_6() {
            TypingModuleDocumentationExample(@"from typing import Callable

def feeder(get_next_item: Callable[[], str]) -> None:
    # Body
    pass

def async_query(on_success: Callable[[int], None],
                on_error: Callable[[int, Exception], None]) -> None:
    # Body
    pass

",
                new[] {
                    "feeder:feeder(get_next_item:function() -> str=)->[]",
                    "async_query:async_query(on_success:function(int)=,on_error:function(int, Exception)=)->[]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public void TypingModuleDocumentationExample_7() {
            TypingModuleDocumentationExample(@"from typing import Mapping, Sequence

class Employee: pass

def notify_by_email(employees: Sequence[Employee],
                    overrides: Mapping[str, str]) -> None: ...
",
                new[] {
                    "notify_by_email:notify_by_email(employees:list[Employee]=,overrides:dict[str, str]=)->[]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public void TypingModuleDocumentationExample_8() {
            TypingModuleDocumentationExample(@"from typing import Sequence, TypeVar

T = TypeVar('T')      # Declare type variable

def first(l: Sequence[T]) -> T:   # Generic function
    return l[0]
",
                new[] {
                    "first:first(l:list[T]=)->[T]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public void TypingModuleDocumentationExample_9() {
            TypingModuleDocumentationExample(@"from typing import TypeVar, Generic, Iterable
from logging import Logger

T = TypeVar('T')

class LoggedVar(Generic[T]):
    def __init__(self, value: T, name: str, logger: Logger) -> None:
        self.name = name
        self.logger = logger
        self.value = value

    def set(self, new: T) -> None:
        self.log('Set ' + repr(self.value))
        self.value = new

    def get(self) -> T:
        self.log('Get ' + repr(self.value))
        return self.value

    def log(self, message: str) -> None:
        self.logger.info('%s: %s', self.name, message)

def zero_all_vars(vars: Iterable[LoggedVar[int]]) -> None:
    for var in vars:
        var.set(0)
",
                new[] {
                    "LoggedVar.set:set(self:LoggedVar=,new:int, T=)->[]",
                    "zero_all_vars:zero_all_vars(vars:iterable[LoggedVar]=)->[]"
                }
            );
        }

        [TestMethod, Priority(0)]
        public void TypingModuleDocumentationExample_10() {
            TypingModuleDocumentationExample(@"from typing import TypeVar, Generic
...

T = TypeVar('T')
S = TypeVar('S', int, str)

class StrangePair(Generic[T, S]):
    ...

class Pair(Generic[T, T]):   # INVALID
    ...

class LinkedList(Sized, Generic[T]):
    ...

class MyDict(Mapping[str, T]):
    ...

def f(s: StrangePair[int, int], p: Pair, l: LinkedList, m: MyDict): ...
",
                new[] {
                    "f:f(s:StrangePair=,p:Pair=,l:LinkedList=,m:MyDict=)->[]"
                }
            );
        }
    }
}
