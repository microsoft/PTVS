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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using EnvDTE;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.SharedProject;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    public class SnippetsTests {
        private static ProjectDefinition BasicProjectDefinition = new ProjectDefinition(
            "SnippetsTest",
            ProjectGenerator.Compile("app", ""),
            ProjectGenerator.Compile("indented", "if True:\r\n    \r\n    pass"),
            ProjectGenerator.Compile("nonempty", "42"),
            ProjectGenerator.Compile("multiline", "1\r\n2\r\n3"),
            ProjectGenerator.Compile("imported", "import unittest\r\n"),
            ProjectGenerator.Compile("importedas", "import unittest as foo\r\n"),
            ProjectGenerator.Compile("badimport", "import\r\n")
        );

        private static readonly Snippet[] BasicSnippets = new Snippet[] {
            new Snippet(
                "class",
                "class ClassName(object):\r\n    $body$",
                new Declaration("myclass", "class myclass(object):\r\n    $body$"),
                new Declaration("(base)", "class myclass(base):\r\n    $body$")
            ),
            new Snippet(
                "def",
                "def function(args):\r\n    $body$",
                new Declaration("foo", "def foo(args):\r\n    $body$"),
                new Declaration("a, b, c", "def foo(a, b, c):\r\n    $body$")
            ),
            new Snippet(
                "for",
                "for x in []:\r\n    $body$",
                new Declaration("var", "for var in []:\r\n    $body$"),
                new Declaration("mylist", "for var in mylist:\r\n    $body$")
            ),
            new Snippet(
                "if",
                "if True:\r\n    $body$",
                new Declaration("False", "if False:\r\n    $body$")
            ),
            new Snippet(
                "while",
                "while True:\r\n    $body$",
                new Declaration("False", "while False:\r\n    $body$")
            ),
            new Snippet(
                "with",
                "with None:\r\n    $body$",
                new Declaration("mgr", "with mgr:\r\n    $body$")
            ),
            new Snippet(
                "try",
                "try:\r\n    $body$\r\nexcept :\r\n    pass",
                new Declaration("Exception", "try:\r\n    $body$\r\nexcept Exception:\r\n    pass")
            ),
            new Snippet(
                "except",
                "try:\r\n    pass\r\nexcept :\r\n    $body$",
                new Declaration("Exception", "try:\r\n    pass\r\nexcept Exception:\r\n    $body$")
            )
        };

        public void TestBasicSnippetsTab(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                foreach (var snippet in BasicSnippets) {
                    TestOneTabSnippet(vs, snippet);

                    vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
                }
            }
        }

        private static IEditor TestOneTabSnippet(IVisualStudioInstance vs, Snippet snippet) {
            Console.WriteLine("Testing: {0}", snippet.Shortcut);
            var editor = vs.OpenItem("SnippetsTest", "app.py");
            editor.MoveCaret(1, 1);
            editor.Invoke(() => editor.TextView.Caret.EnsureVisible());

            return VerifySnippet(snippet, "pass", editor);
        }

        private static IEditor TestOneSurroundWithSnippet(IVisualStudioInstance vs, Snippet snippet, string body = "42", string file = "nonempty.py") {
            Console.WriteLine("Testing: {0}", snippet.Shortcut);
            var editor = vs.OpenItem("SnippetsTest", file);
            editor.Select(1, 1, editor.Text.Length);
            editor.Invoke(() => editor.TextView.Caret.EnsureVisible());

            vs.ExecuteCommand("Edit.SurroundWith");
            return VerifySnippet(snippet, body, editor);
        }

        private static IEditor TestOneInsertSnippet(IVisualStudioInstance vs, Snippet snippet, string category, string body = "42", string file = "nonempty.py") {
            Console.WriteLine("Testing: {0}", snippet.Shortcut);
            var editor = vs.OpenItem("SnippetsTest", file);
            editor.Select(1, 1, editor.Text.Length);
            editor.Invoke(() => editor.TextView.Caret.EnsureVisible());

            vs.ExecuteCommand("Edit.InsertSnippet");
            Keyboard.Type(category + "\t");

            return VerifySnippet(snippet, body, editor);
        }

        private static IEditor TestOneInsertSnippetMoveCaret(IVisualStudioInstance vs, Snippet snippet, string category, string body = "42", string file = "nonempty.py", int line = 1) {
            Console.WriteLine("Testing: {0}", snippet.Shortcut);
            var editor = vs.OpenItem("SnippetsTest", file);
            editor.MoveCaret(line, 1);
            editor.Invoke(() => editor.TextView.Caret.EnsureVisible());

            vs.ExecuteCommand("Edit.InsertSnippet");
            Keyboard.Type(category + "\t");

            return VerifySnippet(snippet, body, editor);
        }

        private static IEditor VerifySnippet(Snippet snippet, string body, IEditor editor) {
            Keyboard.Type(snippet.Shortcut + "\t");

            editor.WaitForText(snippet.Expected.Replace("$body$", body));

            foreach (var decl in snippet.Declarations) {
                Console.WriteLine("Declaration: {0}", decl.Replacement);
                Keyboard.Type(decl.Replacement);
                editor.WaitForText(decl.Expected.Replace("$body$", body));
                Keyboard.Type("\t");
            }
            Keyboard.Type("\r");
            return editor;
        }

        public void TestPassSelected(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            var snippet = new Snippet(
                "class",
                "class ClassName(object):\r\n    pass",
                new Declaration("myclass", "class myclass(object):\r\n    pass"),
                new Declaration("(base)", "class myclass(base):\r\n    pass")
            );

            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                var editor = TestOneTabSnippet(vs, snippet);

                Keyboard.Type("42");
                editor.WaitForText("class myclass(base):\r\n    42");

                vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        public void TestPassSelectedIndented(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                var editor = vs.OpenItem("SnippetsTest", "indented.py");
                editor.MoveCaret(2, 5);
                editor.Invoke(() => editor.TextView.Caret.EnsureVisible());
                editor.SetFocus();

                Keyboard.Type("class\t");
                editor.WaitForText("if True:\r\n    class ClassName(object):\r\n        pass\r\n    pass");
                Keyboard.Type("\r");
                Keyboard.Type("42");
                editor.WaitForText("if True:\r\n    class ClassName(object):\r\n        42\r\n    pass");

                vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        public void TestSurroundWith(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                foreach (var snippet in BasicSnippets) {
                    TestOneSurroundWithSnippet(vs, snippet);

                    vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
                }
            }
        }

        public void TestSurroundWithMultiline(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                foreach (var snippet in BasicSnippets) {
                    TestOneSurroundWithSnippet(
                        vs,
                        snippet,
                        "1\r\n    2\r\n    3",
                        "multiline.py"
                    );

                    vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
                }
            }
        }

        public void TestInsertSnippet(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                foreach (var snippet in BasicSnippets) {
                    TestOneInsertSnippet(vs, snippet, "Python");

                    vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
                }
            }
        }

        public void TestInsertSnippetEmptySelectionNonEmptyLine(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                foreach (var snippet in BasicSnippets) {
                    Console.WriteLine("Testing: {0}", snippet.Shortcut);
                    var editor = vs.OpenItem("SnippetsTest", "nonempty.py");
                    editor.MoveCaret(1, 1);
                    editor.Invoke(() => editor.TextView.Caret.EnsureVisible());
                    editor.SetFocus();

                    vs.ExecuteCommand("Edit.InsertSnippet");
                    Keyboard.Type("Python\t");

                    Keyboard.Type(snippet.Shortcut + "\t");
                    editor.WaitForText(snippet.Expected.Replace("$body$", "pass") + "\r\n" + "42");

                    vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
                }
            }
        }

        public void TestTestClassSnippet(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                var snippet = new Snippet(
                    "testc",
                    "import unittest\r\n\r\nclass MyTestClass(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n",
                    new Declaration("mytest", "import unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n"),
                    new Declaration("quox", "import unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_quox(self):\r\n        self.fail(\"Not implemented\")\r\n")
                );

                TestOneInsertSnippet(vs, snippet, "Test");

                vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        public void TestTestClassSnippetBadImport(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                var snippet = new Snippet(
                    "testc",
                    "import\r\nimport unittest\r\n\r\nclass MyTestClass(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n",
                    new Declaration("mytest", "import\r\nimport unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n"),
                    new Declaration("quox", "import\r\nimport unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_quox(self):\r\n        self.fail(\"Not implemented\")\r\n")
                );

                TestOneInsertSnippetMoveCaret(vs, snippet, "Test", file: "badimport.py", line: 2);

                vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        public void TestTestClassSnippetImportAs(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                var snippet = new Snippet(
                    "testc",
                    "import unittest as foo\r\nimport unittest\r\n\r\nclass MyTestClass(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n",
                    new Declaration("mytest", "import unittest as foo\r\nimport unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n"),
                    new Declaration("quox", "import unittest as foo\r\nimport unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_quox(self):\r\n        self.fail(\"Not implemented\")\r\n")
                );

                TestOneInsertSnippetMoveCaret(vs, snippet, "Test", file: "importedas.py", line: 2);

                vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        public void TestTestClassSnippetUnitTestImported(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                var snippet = new Snippet(
                    "testc",
                    "import unittest\r\n\r\nclass MyTestClass(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n",
                    new Declaration("mytest", "import unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n"),
                    new Declaration("quox", "import unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_quox(self):\r\n        self.fail(\"Not implemented\")\r\n")
                );

                TestOneInsertSnippetMoveCaret(vs, snippet, "Test", file: "imported.py", line: 2);

                vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        /// <summary>
        /// Starting a nested session should dismiss the initial session
        /// </summary>
        public void TestNestedSession(PythonVisualStudioApp app, PythonProjectGenerator pg) {
            using (var vs = pg.Generate(BasicProjectDefinition).ToVs(app)) {
                var editor = vs.OpenItem("SnippetsTest", "app.py");
                editor.MoveCaret(1, 1);
                editor.Invoke(() => editor.TextView.Caret.EnsureVisible());
                editor.SetFocus();

                // start session
                Keyboard.Type("if\t");
                // select inserted pass
                editor.Select(2, 5, 4);
                // start nested session
                vs.ExecuteCommand("Edit.SurroundWith");
                Keyboard.Type("if\t");
                editor.WaitForText("if True:\r\n    if True:\r\n        pass");

                vs.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        class Snippet {
            public readonly string Shortcut;
            public readonly string Expected;
            public readonly Declaration[] Declarations;

            public Snippet(string shortcut, string expected, params Declaration[] declarations) {
                Shortcut = shortcut;
                Expected = expected;
                Declarations = declarations;
            }
        }

        class Declaration {
            public readonly string Replacement;
            public readonly string Expected;

            public Declaration(string replacement, string expected) {
                Replacement = replacement;
                Expected = expected;
            }
        }
    }
}
