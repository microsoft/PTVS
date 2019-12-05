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

using Microsoft.PythonTools.Editor.Formatting;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PythonToolsTests {
    [TestClass]
    public class LspDiffTextEditFactoryTests {
        [TestMethod, Priority(0)]
        public void EditFirstLine() {
            VerifyTextEdits(
                @"hundred=100
",
                @"--- f0.py	2019-10-19 00:22:02.996719 +0000
+++ f0.py	2019-10-19 00:22:12.981332 +0000
@@ -1,2 +1,2 @@
-hundred=100
+hundred = 100
 
",
                new TextEdit[] {
                    new TextEdit() {
                        Range = new Range() { Start = new Position(0, 0), End = new Position(0, 11) },
                        NewText = "hundred = 100",
                    },
                }
            );
        }

        [TestMethod, Priority(0)]
        public void EditOneLine() {
            VerifyTextEdits(
                @"1
2
3
four_hundred=4* 100
5

",
                @"--- f1.py	2019-10-18 23:55:57.018193 +0000
+++ f1.py	2019-10-18 23:58:04.032307 +0000
@@ -1,6 +1,6 @@
 1
 2
 3
-four_hundred=4* 100
+four_hundred = 4 * 100
 5
 
",
                new TextEdit[] {
                    new TextEdit() {
                        Range = new Range() { Start = new Position(2, 1), End = new Position(3, 19) },
                        NewText = "\r\nfour_hundred = 4 * 100",
                    },
                }
            );
        }

        [TestMethod, Priority(0)]
        public void EditTwoSeparateLines() {
            VerifyTextEdits(
                @"1
two_hundred=2*100;
3
four_hundred=4* 100
5

",
                @"--- f2.py	2019-10-19 00:13:36.273763 +0000
+++ f2.py	2019-10-19 00:13:44.165458 +0000
@@ -1,6 +1,6 @@
 1
-two_hundred=2*100;
+two_hundred = 2 * 100
 3
-four_hundred=4* 100
+four_hundred = 4 * 100
 5
 
",
                new TextEdit[] {
                    new TextEdit() {
                        Range = new Range() { Start = new Position(0, 1), End = new Position(1, 18) },
                        NewText = "\r\ntwo_hundred = 2 * 100",
                    },
                    new TextEdit() {
                        Range = new Range() { Start = new Position(2, 1), End = new Position(3, 19) },
                        NewText = "\r\nfour_hundred = 4 * 100",
                    },
                }
            );
        }

        [TestMethod, Priority(0)]
        public void EditTwoConsecutiveLines() {
            VerifyTextEdits(
                @"1
2
three_hundred=3*100;
four_hundred=4* 100
5

",
                @"--- f3.py	2019-10-19 00:16:58.190701 +0000
+++ f3.py	2019-10-19 00:18:39.648348 +0000
@@ -1,6 +1,6 @@
 1
 2
-three_hundred=3*100;
-four_hundred=4* 100
+three_hundred = 3 * 100
+four_hundred = 4 * 100
 5
 
",
                new TextEdit[] {
                    new TextEdit() {
                        Range = new Range() { Start = new Position(1, 1), End = new Position(3, 19) },
                        NewText = "\r\nthree_hundred = 3 * 100\r\nfour_hundred = 4 * 100",
                    },
                }
            );
        }

        [TestMethod, Priority(0)]
        public void EditAndRemoveLines() {

            VerifyTextEdits(
                @"list=[
1,2,3]
",
                @"--- f4.py	2019-10-19 01:30:10.279318 +0000
+++ f4.py	2019-10-19 01:30:16.811136 +0000
@@ -1,3 +1,2 @@
-list=[
-1,2,3]
+list = [1, 2, 3]
 
",
                new TextEdit[] {
                    new TextEdit() {
                        Range = new Range() { Start = new Position(0, 0), End = new Position(1, 6) },
                        NewText = "list = [1, 2, 3]",
                    },
                }
            );
        }

        [TestMethod, Priority(0)]
        public void AddNewLineAtEndOfDoc() {
            // TODO: 1, 0 is outside the range of the document (there is no second line)
            // so this crashes VS right now.
            VerifyTextEdits(
                @"100",
                @"--- f5.py	2019-10-19 01:27:44.001478 +0000
+++ f5.py	2019-10-19 01:27:56.438751 +0000
@@ -1 +1,2 @@
 100
+
",
                new TextEdit[] {
                    new TextEdit() {
                        Range = new Range() { Start = new Position(0, 3), End = new Position(0, 3) },
                        NewText = "\r\n",
                    },
                }
            );
        }

        [TestMethod, Priority(0)]
        public void EditBugDeletesCode() {
            // using autopep8, applying this diff deletes some decorators
            VerifyTextEdits(
                @"""""""
Routes and views for the bottle application.
""""""
import bottle
from bottle import route, view
from datetime import datetime

@route('/')
@route('/home')
@view('index')
def home():
    """"""Renders the home page.""""""
    return dict(
        year = datetime.now().year
    )

@route('/contact')
@view('contact')
def contact():
    """"""Renders the contact page.""""""
    return dict(
        title = 'Contact',
        message = 'Your contact page.',
        year = datetime.now().year
    )

@route('/about')
@view('about')
def about():
    """"""Renders the about page.""""""
    return dict(
        title = 'About',
        message = 'Your application description page.',
        year = datetime.now().year
    )



bottle.app
bottle.abort()
",
                @"--- original/routes.py
+++ fixed/routes.py
@@ -5,35 +5,37 @@
 from bottle import route, view
 from datetime import datetime
 
+
 @route('/')
 @route('/home')
 @view('index')
 def home():
     """"""Renders the home page.""""""
     return dict(
-        year = datetime.now().year
+        year=datetime.now().year
     )
+
 
 @route('/contact')
 @view('contact')
 def contact():
     """"""Renders the contact page.""""""
     return dict(
-        title = 'Contact',
-        message = 'Your contact page.',
-        year = datetime.now().year
+        title='Contact',
+        message='Your contact page.',
+        year=datetime.now().year
     )
+
 
 @route('/about')
 @view('about')
 def about():
     """"""Renders the about page.""""""
     return dict(
-        title = 'About',
-        message = 'Your application description page.',
-        year = datetime.now().year
+        title='About',
+        message='Your application description page.',
+        year=datetime.now().year
     )
-
 
 
 bottle.app
",
                 new TextEdit[] {
                    new TextEdit() {
                        Range = new Range() { Start = new Position(6, 0), End = new Position(6, 0) },
                        NewText = "\r\n",
                    },
                    new TextEdit() {
                        Range = new Range() { Start = new Position(12, 16), End = new Position(13, 34) },
                        NewText = "\r\n        year=datetime.now().year",
                    },
                    new TextEdit() {
                        Range = new Range() { Start = new Position(14, 5), End = new Position(14, 5) },
                        NewText = "\r\n",
                    },
                    new TextEdit() {
                        Range = new Range() { Start = new Position(20, 16), End = new Position(23, 34) },
                        NewText = "\r\n        title='Contact',\r\n        message='Your contact page.',\r\n        year=datetime.now().year",
                    },
                    new TextEdit() {
                        Range = new Range() { Start = new Position(24, 5), End = new Position(24, 5) },
                        NewText = "\r\n",
                    },
                    new TextEdit() {
                        Range = new Range() { Start = new Position(30, 16), End = new Position(33, 34) },
                        NewText = "\r\n        title='About',\r\n        message='Your application description page.',\r\n        year=datetime.now().year",
                    },
                     new TextEdit() {
                        Range = new Range() { Start = new Position(34, 5), End = new Position(35, 0) },
                        NewText = null,
                    },
                }
            );
        }

        [TestMethod, Priority(0)]
        public void EditMany() {
            VerifyTextEdits(@"1
            2
            3
            four_hundred=4* 100
            5

            seven=[1,2, 3]

            nine = ( 1,
            2,
            3,
            4)
            end = 13",
            @"@@ -1,13 +1,11 @@
             1
             2
             3
            -four_hundred=4* 100
            +four_hundred = 4 * 100
             5

            -seven=[1,2, 3]
            +seven = [1, 2, 3]

            -nine = ( 1,
            -2,
            -3,
            -4)
            +nine = (1, 2, 3, 4)
             end = 13
            +
            ",
             new TextEdit[] { });
            Assert.Inconclusive();
        }

        private void VerifyTextEdits(string text, string diff, TextEdit[] expected) {
            var actual = LspDiffTextEditFactory.GetEdits(text, diff);
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++) {
                Assert.AreEqual(expected[i].Range, actual[i].Range);
                Assert.AreEqual(expected[i].NewText, actual[i].NewText);
            }
        }
    }
}
