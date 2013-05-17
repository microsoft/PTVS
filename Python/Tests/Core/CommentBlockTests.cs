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

using Microsoft.PythonTools.Editor.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities.Mocks;

namespace PythonToolsTests {
    [TestClass]
    public class CommentBlockTests {
        [TestMethod, Priority(0)]
        public void TestCommentCurrentLine() {
            var view = new MockTextView(
                new MockTextBuffer(@"print 'hello'
print 'goodbye'"));

            view.Caret.MoveTo(view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(0).Start);

            EditorExtensions.CommentOrUncommentBlock(view, true);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"#print 'hello'
print 'goodbye'");

            view.Caret.MoveTo(view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start);

            EditorExtensions.CommentOrUncommentBlock(view, true);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
        @"#print 'hello'
#print 'goodbye'");
        }

        [TestMethod, Priority(0)]
        public void TestUnCommentCurrentLine() {
            var view = new MockTextView(
                new MockTextBuffer(@"#print 'hello'
#print 'goodbye'"));

            view.Caret.MoveTo(view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(0).Start);

            EditorExtensions.CommentOrUncommentBlock(view, false);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"print 'hello'
#print 'goodbye'");

            view.Caret.MoveTo(view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start);

            EditorExtensions.CommentOrUncommentBlock(view, false);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
        @"print 'hello'
print 'goodbye'");
        }

        [TestMethod, Priority(0)]
        public void TestComment() {
            var view = new MockTextView(
                new MockTextBuffer(@"print 'hello'
print 'goodbye'"));

            view.Selection.Select(
                new SnapshotSpan(view.TextBuffer.CurrentSnapshot, new Span(0, view.TextBuffer.CurrentSnapshot.Length)),
                false
            );

            EditorExtensions.CommentOrUncommentBlock(view, true);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"#print 'hello'
#print 'goodbye'");
        }

        [TestMethod, Priority(0)]
        public void TestCommentEmptyLine() {
            var view = new MockTextView(
                new MockTextBuffer(@"print 'hello'

print 'goodbye'"));

            view.Selection.Select(
                new SnapshotSpan(view.TextBuffer.CurrentSnapshot, new Span(0, view.TextBuffer.CurrentSnapshot.Length)),
                false
            );

            EditorExtensions.CommentOrUncommentBlock(view, true);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"#print 'hello'

#print 'goodbye'");
        }

        [TestMethod, Priority(0)]
        public void TestCommentWhiteSpaceLine() {
            var view = new MockTextView(
                new MockTextBuffer(@"print 'hello'
   
print 'goodbye'"));

            view.Selection.Select(
                new SnapshotSpan(view.TextBuffer.CurrentSnapshot, new Span(0, view.TextBuffer.CurrentSnapshot.Length)),
                false
            );

            EditorExtensions.CommentOrUncommentBlock(view, true);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"#print 'hello'
   
#print 'goodbye'");
        }

        [TestMethod, Priority(0)]
        public void TestCommentIndented() {
            var view = new MockTextView(
                new MockTextBuffer(@"def f():
    print 'hello'
    print 'still here'
    print 'goodbye'"));

            view.Selection.Select(
                new SnapshotSpan(
                    view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start,
                    view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(2).End
                ),
                false
            );

            EditorExtensions.CommentOrUncommentBlock(view, true);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"def f():
    #print 'hello'
    #print 'still here'
    print 'goodbye'");
        }

        [TestMethod, Priority(0)]
        public void TestCommentIndentedBlankLine() {
            var view = new MockTextView(
                new MockTextBuffer(@"def f():
    print 'hello'

    print 'still here'
    print 'goodbye'"));

            view.Selection.Select(
                new SnapshotSpan(
                    view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start,
                    view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(3).End
                ),
                false
            );

            EditorExtensions.CommentOrUncommentBlock(view, true);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"def f():
    #print 'hello'

    #print 'still here'
    print 'goodbye'");
        }

        [TestMethod, Priority(0)]
        public void TestCommentBlankLine() {
            var view = new MockTextView(
                new MockTextBuffer(@"print('hi')

print('bye')"));

            view.Caret.MoveTo(view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start);

            EditorExtensions.CommentOrUncommentBlock(view, true);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"print('hi')

print('bye')");
        }

        [TestMethod, Priority(0)]
        public void TestCommentIndentedWhiteSpaceLine() {
            var view = new MockTextView(
                new MockTextBuffer(@"def f():
    print 'hello'
  
    print 'still here'
    print 'goodbye'"));

            view.Selection.Select(
                new SnapshotSpan(
                    view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start,
                    view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(3).End
                ),
                false
            );

            EditorExtensions.CommentOrUncommentBlock(view, true);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"def f():
    #print 'hello'
  
    #print 'still here'
    print 'goodbye'");
        }

        [TestMethod, Priority(0)]
        public void TestUnCommentIndented() {
            var view = new MockTextView(
                new MockTextBuffer(@"def f():
    #print 'hello'
    #print 'still here'
    print 'goodbye'"));

            view.Selection.Select(
                new SnapshotSpan(
                    view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start,
                    view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(2).End
                ),
                false
            );

            EditorExtensions.CommentOrUncommentBlock(view, false);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"def f():
    print 'hello'
    print 'still here'
    print 'goodbye'");
        }

        [TestMethod, Priority(0)]
        public void TestUnComment() {
            var view = new MockTextView(
                new MockTextBuffer(@"#print 'hello'
#print 'goodbye'"));

            view.Selection.Select(
                new SnapshotSpan(view.TextBuffer.CurrentSnapshot, new Span(0, view.TextBuffer.CurrentSnapshot.Length)),
                false
            );

            EditorExtensions.CommentOrUncommentBlock(view, false);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"print 'hello'
print 'goodbye'");
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/814
        /// </summary>
        [TestMethod, Priority(0)]
        public void TestCommentStartOfLastLine() {
            var view = new MockTextView(
                new MockTextBuffer(@"print 'hello'
print 'goodbye'"));

            view.Selection.Select(
                new SnapshotSpan(view.TextBuffer.CurrentSnapshot, new Span(0, view.TextBuffer.CurrentSnapshot.GetText().IndexOf("print 'goodbye'"))),
                false
            );

            EditorExtensions.CommentOrUncommentBlock(view, true);

            Assert.AreEqual(view.TextBuffer.CurrentSnapshot.GetText(),
                @"#print 'hello'
print 'goodbye'");
        }
    }
}
