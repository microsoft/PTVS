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
using System.Reflection;
using System.Threading;
using AnalysisTest.Mocks;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Refactoring;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace AnalysisTest {
    [TestClass]
    [DeploymentItem(@"..\\PythonTools\\CompletionDB\\", "CompletionDB")]
    [DeploymentItem("PyDebugAttach.dll")]
    public class RefactorRenameTests {
        private const string ErrorModuleName = "Cannot rename a module name";

        [TestMethod]
        public void SanityClassField() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"class C:
    abc = 100

a = C()
a.abc
",
@"class C:
    foo = 100

a = C()
a.foo
"
                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("abc = 100"),
                    new ExpectedPreviewItem("a.abc")
                )
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"class C:
    abc = 100

a = C()
a.abc
abc = 200
",
@"class C:
    foo = 100

a = C()
a.foo
abc = 200
"
                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("abc = 100"),
                    new ExpectedPreviewItem("a.abc")
                )
            );
        }

        [TestMethod]
        public void TypelessForVariable() {
            RefactorTest("baz", "abc",
                new[] { 
                    new FileInput(
@"def f():
    l = foo
    for abc in l:
        l.append(abc)",
@"def f():
    l = foo
    for baz in l:
        l.append(baz)"
                    )
                }
            );
        }


        [TestMethod]
        public void SanityInstanceField() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"class C:
    def __init__(self):
        self.abc = 100

a = C()
a.abc
",
@"class C:
    def __init__(self):
        self.foo = 100

a = C()
a.foo
"
                    )
                }
            );
        }

        [TestMethod]
        public void SanityParameter() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(abc):
    print abc
    abc = 100
",
@"def f(foo):
    print foo
    foo = 100
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f(abc):
    print abc
    abc = 100

abc = 200
",
@"def f(foo):
    print foo
    foo = 100

abc = 200
"
                    )
                }
            );
        }

        [TestMethod]
        public void SanityLocal() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    print abc
",
@"def f():
    foo = 100
    print foo
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    print abc

abc = 200
",
@"def f():
    foo = 100
    print foo

abc = 200
"
                    )
                }
            );
        }

        [TestMethod]
        public void SanityClosure() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    def g():
        print abc
",
@"def f():
    foo = 100
    def g():
        print foo
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    abc = 100
    def g():
        print abc

abc = 200
",
@"def f():
    foo = 100
    def g():
        print foo

abc = 200
"
                    )
                }
            );
        }

        [TestMethod]
        public void SanityGlobal() {
            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    global abc
    print abc

abc = 100
",
@"def f():
    global foo
    print foo

foo = 100
"
                    )
                }
            );

            RefactorTest("foo", "abc",
                new[] { 
                    new FileInput(
@"def f():
    global abc
    print abc

def g():
    abc = 200

abc = 100
",
@"def f():
    global foo
    print foo

def g():
    abc = 200

foo = 100
"
                    )
                }
            );
        }

        [TestMethod]
        public void SanityNonLocal() {
            RefactorTest("foo", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
def g():
    abc = 100
    def f():
        nonlocal abc
        print(abc)

abc = 100
",
@"
def g():
    foo = 100
    def f():
        nonlocal foo
        print(foo)

abc = 100
"                    )
                }
            );

            RefactorTest("foo", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
def g():
    abc = 100
    def f():
        nonlocal  abc
        print(abc)

abc = 100
",
@"
def g():
    foo = 100
    def f():
        nonlocal  foo
        print(foo)

abc = 100
"                    )
                }
            );
        }

        [TestMethod]
        public void SanityRenameClass() {
            RefactorTest("foo", "abc", 
            new[] { 
                    new FileInput(
@"
4

class abc(object):
    pass

x = abc()",
@"
4

class foo(object):
    pass

x = foo()"
                    )

                }
            );

            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
4

class  abc(object):
    pass

x = abc()",
@"
4

class  foo(object):
    pass

x = foo()"
                    )

                }
            );
        }

        [TestMethod]
        public void SanityRenameFunction() {
            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
4

def abc(x):
    pass

x = abc()",
@"
4

def foo(x):
    pass

x = foo()"
                    )

                }
            );

            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
4

def  abc(x):
    pass

x = abc()",
@"
4

def  foo(x):
    pass

x = foo()"
                    )

                }
            );
        }

        [TestMethod]
        public void SanityDelLocal() {
            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
def f():
    abc = 100
    del abc",
@"
def f():
    foo = 100
    del foo"                    
                    )

                }
            );
        }

        [TestMethod]
        public void SanityDelInstanceMember() {
            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
class C(object):
    def __init__(self):
        self.abc = 42

def f():    
    del C().abc",
@"
class C(object):
    def __init__(self):
        self.foo = 42

def f():    
    del C().foo"
                    )

                }
            );
        }

        [TestMethod]
        public void SanityDelClassMember() {
            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
class C(object):
    abc = 42

def f():    
    del C().abc
",
@"
class C(object):
    foo = 42

def f():    
    del C().foo
"
                    )

                }
            );
        }

        [TestMethod]
        public void SanityDelGlobal() {
            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
def f():
    global abc
    del abc

abc = 100
",
@"
def f():
    global foo
    del foo

foo = 100
"
                    )

                }
            );
        }

        [TestMethod]
        public void DelNonLocal() {
            RefactorTest("foo", "abc", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
def g():
    abc = 100
    def f():
        nonlocal abc
        del abc

abc = 100
",
@"
def g():
    foo = 100
    def f():
        nonlocal foo
        del foo

abc = 100
"                    )
                }
            );

        }

        [TestMethod]
        public void Decorators() {
            RefactorTest("abc", "foo", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
def foo():
    pass

@foo
def f():
    pass
",
@"
def abc():
    pass

@abc
def f():
    pass
"                    )
                }
            );

            RefactorTest("abc", "foo", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
class C:
    @staticmethod
    def foo():
        pass

@C.foo
def f():
    pass
",
@"
class C:
    @staticmethod
    def abc():
        pass

@C.abc
def f():
    pass
"                    )
                }
            );

        }

        [TestMethod]
        public void TryStatement() {
            RefactorTest("abc", "foo", 
            new[] { 
                    new FileInput(
@"
try:
    pass
except Exception, foo:
    print(foo)
",
@"
try:
    pass
except Exception, abc:
    print(abc)
"                    )
                }
            );

            RefactorTest("abc", "foo", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
try:
    pass
except Exception as foo:
    print(foo)
",
@"
try:
    pass
except Exception as abc:
    print(abc)
"                    )
                }
            );

        }

        [TestMethod]
        public void WithStatement() {
            RefactorTest("abc", "foo", version: new Version(3, 2),
            inputs: new[] { 
                    new FileInput(
@"
with foo as foo:
    print(foo)
",
@"
with foo as abc:
    print(abc)
"                    )
                }
            );

        }

        [TestMethod]
        public void KeywordParameter() {
            RefactorTest("foo", "abc", 
            new[] { 
                    new FileInput(
@"
def f(abc):
    pass

f(abc = 10)
",
@"
def f(foo):
    pass

f(foo = 10)
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("def f(abc):"),
                    new ExpectedPreviewItem("f(abc = 10)")
                )   
            );

            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
def g():
    f(abc = 10)

def f(abc):
    pass
",
@"
def g():
    f(foo = 10)

def f(foo):
    pass
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("f(abc = 10)"),
                    new ExpectedPreviewItem("def f(abc):")
                )
            );

        }

        [TestMethod]
        public void ImportAsStatement() {
            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
import sys as abc
x = abc
",
@"
import sys as foo
x = foo
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("import sys as abc"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

        }

        [TestMethod]
        public void FromImportAsStatement() {
            RefactorTest("foo", "abc",
            new[] { 
                    new FileInput(
@"
from sys import bar as abc
x = abc
",
@"
from sys import bar as foo
x = foo
"                    )
                },
                new ExpectedPreviewItem("test.py",
                    new ExpectedPreviewItem("from sys import bar as abc"),
                    new ExpectedPreviewItem("x = abc")
                )
            );

        }

        [TestMethod]
        public void CannotRename() {
            CannotRename("abc", "import abc", ErrorModuleName);
            CannotRename("abc", "from abc import bar", ErrorModuleName);
            CannotRename("abc", "import abc as bar", ErrorModuleName);
        }

        class FileInput {
            public readonly string Input, Output, Filename;

            public FileInput(string input, string output, string filename = "test.py") {
                Input = input;
                Output = output;
                Filename = filename;
            }
        }

        private void RefactorTest(string newName, string caretText, FileInput[] inputs, params ExpectedPreviewItem[] items) {
            RefactorTest(newName, caretText, inputs, null, items);
        }

        private void RefactorTest(string newName, string caretText, FileInput[] inputs, Version version = null, params ExpectedPreviewItem[] items) {
            foreach(bool preview in new[] { true, false } ) {
                OneRefactorTest(newName, caretText, inputs, version, preview, null, items);

                // try again w/ a longer name
                MutateTest(newName, caretText, inputs, version, newName + newName, preview);

                // and a shorter name
                MutateTest(newName, caretText, inputs, version, new string(newName[0], 1), preview);
            }
        }

        private void MutateTest(string newName, string caretText, FileInput[] inputs, Version version, string altNewName, bool preview) {
            FileInput[] moreInputs = new FileInput[inputs.Length];
            for (int i = 0; i < moreInputs.Length; i++) {
                moreInputs[i] = new FileInput(
                    inputs[i].Input,
                    inputs[i].Output.Replace(newName, altNewName),
                    inputs[i].Filename
                );
            }

            OneRefactorTest(altNewName, caretText, moreInputs, version, preview, null);
        }

        class ExpectedPreviewItem {
            public readonly string Name;
            public readonly ExpectedPreviewItem[] Children;

            public ExpectedPreviewItem(string name, params ExpectedPreviewItem[] children) {
                Name = name;
                Children = children;
            }
        }

        private static void CannotRename(string caretText, string text, string error) {
            OneRefactorTest("foo", caretText, new[] { new FileInput(text, null), new FileInput("def bar(): pass", "", "abc.py") }, null, false, error, null);
        }

        private static void OneRefactorTest(string newName, string caretText, FileInput[] inputs, Version version, bool preview, string error, ExpectedPreviewItem[] expected = null) {
            var fact = new CPythonInterpreterFactory(version ?? new Version(2, 6), new Guid(), "test interpreter", "C:\\foo\\python.exe", "C:\\foo\\pythonw.exe", "PYTHONPATH", ProcessorArchitecture.X86);
            var analyzer = new ProjectAnalyzer(fact, new MockErrorProviderFactory());
            MockTextBuffer[] buffers = new MockTextBuffer[inputs.Length];
            MockTextView[] views = new MockTextView[inputs.Length];
            Dictionary<string, ITextBuffer> bufferTable = new Dictionary<string, ITextBuffer>();
            List<MonitoredBufferResult> analysis = new List<MonitoredBufferResult>();
            for (int i = 0; i < inputs.Length; i++) {
                buffers[i] = new MockTextBuffer(inputs[i].Input, inputs[i].Filename);
                views[i] = new MockTextView(buffers[i]);
                buffers[i].AddProperty(typeof(ProjectAnalyzer), analyzer);
                bufferTable[inputs[i].Filename] = buffers[i];
                analysis.Add(analyzer.MonitorTextBuffer(views[i], buffers[i]));
            }

            bool missingAnalysis;
            do {
                missingAnalysis = false;
                for (int i = 0; i < analysis.Count; i++) {
                    if (!analysis[i].ProjectEntry.IsAnalyzed) {
                        missingAnalysis = true;
                    }
                }
                Thread.Sleep(10);
            } while (missingAnalysis);


            var caretPos = inputs[0].Input.IndexOf(caretText);
            views[0].Caret.MoveTo(new SnapshotPoint(buffers[0].CurrentSnapshot, caretPos));

            var extractInput = new RenameVariableTestInput(newName, bufferTable, preview);
            var previewChangesService = new TestPreviewChanges(expected);

            new VariableRenamer(views[0]).RenameVariable(extractInput, previewChangesService);
            if (error != null) {
                Assert.AreEqual(error, extractInput.Failure);
                return;
            }
            Assert.AreEqual(previewChangesService.Previewed, preview);
            for (int i = 0; i < buffers.Length; i++) {
                Assert.AreEqual(inputs[i].Output, buffers[i].CurrentSnapshot.GetText());
            }

            foreach (var monitored in analysis) {
                analyzer.StopMonitoringTextBuffer(monitored.BufferParser);
            }
        }

        class RenameVariableTestInput : IRenameVariableInput {
            private readonly string _name;
            private readonly bool _preview, _searchInStrings, _searchInComments;
            internal readonly List<string> Log = new List<string>();
            private readonly Dictionary<string, ITextBuffer> _buffers;
            internal string Failure;

            public RenameVariableTestInput(string name, Dictionary<string, ITextBuffer> buffers, bool preview = true, bool searchInStrings = false, bool searchInComments = false) {
                _name = name;
                _preview = preview;
                _searchInComments = searchInComments;
                _searchInStrings = searchInStrings;
                _buffers = buffers;
            }

            public RenameVariableRequest GetRenameInfo(string originalName) {
                return new RenameVariableRequest(_name, _preview, _searchInComments, _searchInStrings);
            }

            public void CannotRename(string message) {
                Failure = message;
            }

            public void OutputLog(string message) {
                Log.Add(message);
            }

            public ITextBuffer GetBufferForDocument(string filename) {
                return _buffers[filename];
            }

            IVsLinkedUndoTransactionManager IRenameVariableInput.BeginGlobalUndo() {
                return null;
            }

            public void EndGlobalUndo(IVsLinkedUndoTransactionManager undo) {
            }

            public void ClearRefactorPane() {
            }
        }

        class TestPreviewChanges : IVsPreviewChangesService {
            public bool Previewed = false;
            private readonly ExpectedPreviewItem[] _expected;

            public TestPreviewChanges(ExpectedPreviewItem[] expected) {
                _expected = expected;
            }

            public int PreviewChanges(IVsPreviewChangesEngine pIVsPreviewChangesEngine) {
                object rootList;
                pIVsPreviewChangesEngine.GetRootChangesList(out rootList);
                IVsLiteTreeList list = rootList as IVsLiteTreeList;
                IVsPreviewChangesList preview = rootList as IVsPreviewChangesList;

                Assert.AreNotEqual(null, list);
                Assert.AreNotEqual(null, preview);

                if (_expected != null && _expected.Length > 0) {
                    try {
                        VerifyList(list, _expected);
                    } catch {
                        PrintList(list);
                        throw;
                    }
                } else {
                    PrintList(list);
                }
                
                Previewed = true;
                return pIVsPreviewChangesEngine.ApplyChanges();
            }

            private static void VerifyList(IVsLiteTreeList list, ExpectedPreviewItem[] expected) {
                uint count;
                list.GetItemCount(out count);

                Assert.AreEqual(expected.Length, (int)count);
                for (int i = 0; i < expected.Length; i++) {
                    string text;
                    list.GetText((uint)i, VSTREETEXTOPTIONS.TTO_DEFAULT, out text);
                    Assert.AreEqual(expected[i].Name, text);

                    int expandable;
                    list.GetExpandable((uint)i, out expandable);
                    if (expected[i].Children.Length != 0) {
                        Assert.AreEqual(1, expandable);
                        int canRecurse;
                        IVsLiteTreeList subList;
                        list.GetExpandedList((uint)i, out canRecurse, out subList);

                        VerifyList(subList, expected[i].Children);
                    } else {
                        Assert.AreEqual(0, expandable);
                    }
                }
            }

            private static void PrintList(IVsLiteTreeList list, int indent = 0) {
                uint count;
                list.GetItemCount(out count);

                
                for (int i = 0; i < count; i++) {
                    string text;
                    list.GetText((uint)i, VSTREETEXTOPTIONS.TTO_DEFAULT, out text);
                    Console.Write("{1}new ExpectedPreviewItem(\"{0}\"", text, new string(' ', indent * 4));

                    int expandable;
                    list.GetExpandable((uint)i, out expandable);
                    if (expandable != 0) {
                        Console.WriteLine(", ");
                        int canRecurse;
                        IVsLiteTreeList subList;
                        list.GetExpandedList((uint)i, out canRecurse, out subList);

                        PrintList(subList, indent + 1);
                    }

                    VSTREEDISPLAYDATA[] data = new VSTREEDISPLAYDATA[1];
                    list.GetDisplayData((uint)i, data);

                    // TODO: Validate display data

                    uint changeCnt = 0;
                    list.GetListChanges(ref changeCnt, null);

                    VSTREELISTITEMCHANGE[] changes = new VSTREELISTITEMCHANGE[changeCnt];
                    list.GetListChanges(ref changeCnt, changes);

                    // TODO: Valiate changes

                    if (i != count - 1) {
                        Console.WriteLine("),");
                    } else {
                        Console.WriteLine(")");
                    }
                }
            }
        }
    }
}
