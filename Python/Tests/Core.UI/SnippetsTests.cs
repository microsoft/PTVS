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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.SharedProject;
using TestUtilities.UI;

namespace PythonToolsUITests {
    [TestClass]
    public class SnippetsTests : PythonProjectTest {
        private static ProjectDefinition BasicProject = Project(
            "SnippetsTest",
            Compile("app", ""),
            Compile("indented", "if True:\r\n    \r\n    pass"),
            Compile("nonempty", "42"),
            Compile("multiline", "1\r\n2\r\n3"),
            Compile("imported", "import unittest\r\n"),
            Compile("importedas", "import unittest as foo\r\n"),
            Compile("badimport", "import\r\n")
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
                "try:\r\n    $body$\r\nexcept  :\r\n    pass",
                new Declaration("Exception", "try:\r\n    $body$\r\nexcept Exception:\r\n    pass")
            ),
            new Snippet(
                "except",
                "try:\r\n    pass\r\nexcept  :\r\n    $body$",
                new Declaration("Exception", "try:\r\n    pass\r\nexcept Exception:\r\n    $body$")
            )
        };

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestBasicSnippetsTab() {
            using (var solution = BasicProject.Generate().ToVs()) {
                foreach (var snippet in BasicSnippets) {
                    TestOneTabSnippet(solution, snippet);

                    solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
                }
            }
        }

        private static IEditor TestOneTabSnippet(IVisualStudioInstance solution, Snippet snippet) {
            Console.WriteLine("Testing: {0}", snippet.Shortcut);
            var app = solution.OpenItem("SnippetsTest", "app.py");
            app.MoveCaret(1, 1);
            app.Invoke(() => app.TextView.Caret.EnsureVisible());
            app.SetFocus();

            return VerifySnippet(snippet, "pass", app);
        }

        private static IEditor TestOneSurroundWithSnippet(IVisualStudioInstance solution, Snippet snippet, string body = "42", string file = "nonempty.py") {
            Console.WriteLine("Testing: {0}", snippet.Shortcut);
            var app = solution.OpenItem("SnippetsTest", file);
            app.Select(1, 1, app.Text.Length);
            app.Invoke(() => app.TextView.Caret.EnsureVisible());
            app.SetFocus();

            solution.ExecuteCommand("Edit.SurroundWith");
            return VerifySnippet(snippet, body, app);
        }

        private static IEditor TestOneInsertSnippet(IVisualStudioInstance solution, Snippet snippet, string category, string body = "42", string file = "nonempty.py") {
            Console.WriteLine("Testing: {0}", snippet.Shortcut);
            var app = solution.OpenItem("SnippetsTest", file);
            app.Select(1, 1, app.Text.Length);
            app.Invoke(() => app.TextView.Caret.EnsureVisible());
            app.SetFocus();

            solution.ExecuteCommand("Edit.InsertSnippet");
            Keyboard.Type(category + "\t");

            return VerifySnippet(snippet, body, app);
        }

        private static IEditor TestOneInsertSnippetMoveCaret(IVisualStudioInstance solution, Snippet snippet, string category, string body = "42", string file = "nonempty.py", int line = 1) {
            Console.WriteLine("Testing: {0}", snippet.Shortcut);
            var app = solution.OpenItem("SnippetsTest", file);
            app.MoveCaret(line, 1);
            app.Invoke(() => app.TextView.Caret.EnsureVisible());
            app.SetFocus();

            solution.ExecuteCommand("Edit.InsertSnippet");
            Keyboard.Type(category + "\t");

            return VerifySnippet(snippet, body, app);
        }

        private static IEditor VerifySnippet(Snippet snippet, string body, IEditor app) {
            Keyboard.Type(snippet.Shortcut + "\t");

            app.WaitForText(snippet.Expected.Replace("$body$", body));

            foreach (var decl in snippet.Declarations) {
                Console.WriteLine("Declaration: {0}", decl.Replacement);
                Keyboard.Type(decl.Replacement);
                app.WaitForText(decl.Expected.Replace("$body$", body));
                Keyboard.Type("\t");
            }
            Keyboard.Type("\r");
            return app;
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestPassSelected() {
            var snippet = new Snippet(
                "class",
                "class ClassName(object):\r\n    pass",
                new Declaration("myclass", "class myclass(object):\r\n    pass"),
                new Declaration("(base)", "class myclass(base):\r\n    pass")
            );

            using (var solution = BasicProject.Generate().ToVs()) {
                var app = TestOneTabSnippet(solution, snippet);

                Keyboard.Type("42");
                app.WaitForText("class myclass(base):\r\n    42");

                solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestPassSelectedIndented() {
            using (var solution = BasicProject.Generate().ToVs()) {
                var app = solution.OpenItem("SnippetsTest", "indented.py");
                app.MoveCaret(2, 5);
                app.Invoke(() => app.TextView.Caret.EnsureVisible());
                app.SetFocus();

                Keyboard.Type("class\t");
                app.WaitForText("if True:\r\n    class ClassName(object):\r\n        pass\r\n    pass");
                Keyboard.Type("\r");
                Keyboard.Type("42");
                app.WaitForText("if True:\r\n    class ClassName(object):\r\n        42\r\n    pass");

                solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestSurroundWith() {
            using (var solution = BasicProject.Generate().ToVs()) {
                foreach (var snippet in BasicSnippets) {
                    TestOneSurroundWithSnippet(solution, snippet);

                    solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestSurroundWithMultiline() {
            using (var solution = BasicProject.Generate().ToVs()) {
                foreach (var snippet in BasicSnippets) {
                    TestOneSurroundWithSnippet(
                        solution,
                        snippet,
                        "1\r\n    2\r\n    3",
                        "multiline.py"
                    );

                    solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestInsertSnippet() {
            using (var solution = BasicProject.Generate().ToVs()) {
                foreach (var snippet in BasicSnippets) {
                    TestOneInsertSnippet(solution, snippet, "Python");

                    solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestInsertSnippetEmptySelectionNonEmptyLine() {
            using (var solution = BasicProject.Generate().ToVs()) {
                foreach (var snippet in BasicSnippets) {
                    Console.WriteLine("Testing: {0}", snippet.Shortcut);
                    var app = solution.OpenItem("SnippetsTest", "nonempty.py");
                    app.MoveCaret(1, 1);
                    app.Invoke(() => app.TextView.Caret.EnsureVisible());
                    app.SetFocus();

                    solution.ExecuteCommand("Edit.InsertSnippet");
                    Keyboard.Type("Python\t");

                    Keyboard.Type(snippet.Shortcut + "\t");
                    app.WaitForText(snippet.Expected.Replace("$body$", "pass") + "\r\n" + "42");

                    solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestTestClassSnippet() {
            using (var solution = BasicProject.Generate().ToVs()) {
                var snippet = new Snippet(
                    "testc",
                    "\r\nimport unittest\r\nclass MyTestClass(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n",
                    new Declaration("mytest", "\r\nimport unittest\r\nclass mytest(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n"),
                    new Declaration("quox", "\r\nimport unittest\r\nclass mytest(unittest.TestCase):\r\n    def test_quox(self):\r\n        self.fail(\"Not implemented\")\r\n")
                );

                TestOneInsertSnippet(solution, snippet, "Test");

                solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestTestClassSnippetBadImport() {
            using (var solution = BasicProject.Generate().ToVs()) {
                var snippet = new Snippet(
                    "testc",
                    "import\r\nimport unittest\r\n\r\nclass MyTestClass(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n",
                    new Declaration("mytest", "import\r\nimport unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n"),
                    new Declaration("quox", "import\r\nimport unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_quox(self):\r\n        self.fail(\"Not implemented\")\r\n")
                );

                TestOneInsertSnippetMoveCaret(solution, snippet, "Test", file: "badimport.py", line:2);

                solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestTestClassSnippetImportAs() {
            using (var solution = BasicProject.Generate().ToVs()) {
                var snippet = new Snippet(
                    "testc",
                    "import unittest as foo\r\nimport unittest\r\n\r\nclass MyTestClass(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n",
                    new Declaration("mytest", "import unittest as foo\r\nimport unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n"),
                    new Declaration("quox", "import unittest as foo\r\nimport unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_quox(self):\r\n        self.fail(\"Not implemented\")\r\n")
                );

                TestOneInsertSnippetMoveCaret(solution, snippet, "Test", file: "importedas.py", line: 2);

                solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestTestClassSnippetUnitTestImported() {
            using (var solution = BasicProject.Generate().ToVs()) {
                var snippet = new Snippet(
                    "testc",
                    "import unittest\r\n\r\nclass MyTestClass(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n",
                    new Declaration("mytest", "import unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_name(self):\r\n        self.fail(\"Not implemented\")\r\n"),
                    new Declaration("quox", "import unittest\r\n\r\nclass mytest(unittest.TestCase):\r\n    def test_quox(self):\r\n        self.fail(\"Not implemented\")\r\n")
                );

                TestOneInsertSnippetMoveCaret(solution, snippet, "Test", file: "imported.py", line: 2);

                solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
            }
        }

        /// <summary>
        /// Starting a nested session should dismiss the initial session
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestNestedSession() {
            using (var solution = BasicProject.Generate().ToVs()) {
                var app = solution.OpenItem("SnippetsTest", "app.py");
                app.MoveCaret(1, 1);
                app.Invoke(() => app.TextView.Caret.EnsureVisible());
                app.SetFocus();

                // start session
                Keyboard.Type("if\t");
                // select inserted pass
                app.Select(2, 5, 4);
                // start nested session
                solution.ExecuteCommand("Edit.SurroundWith");
                Keyboard.Type("if\t");
                app.WaitForText("if True:\r\n    if True:\r\n        pass");

                solution.CloseActiveWindow(vsSaveChanges.vsSaveChangesNo);
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
