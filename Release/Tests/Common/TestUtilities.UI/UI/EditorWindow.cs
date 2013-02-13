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
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace TestUtilities.UI {
    public class EditorWindow : AutomationWrapper {
        private readonly string _filename;

        public EditorWindow(string filename, AutomationElement element)
            : base(element) {
            _filename = filename;
        }

        public string Text {
            get {
                return GetValue();
            }
        }

        public virtual IWpfTextView TextView {
            get {
                return GetTextView(_filename);
            }
        }

        public void MoveCaret(SnapshotPoint newPoint) {
            ((UIElement)TextView).Dispatcher.Invoke((Action)(() => {
                TextView.Caret.MoveTo(newPoint);
            }));
        }

        public void Select(int line, int column, int length) {
            var textLine = TextView.TextViewLines[line - 1];
            Span span;
            if (column - 1 == textLine.Length) {
                span = new Span(textLine.End, length);
            } else {
                span = new Span(textLine.Start + column - 1, length);
            }

            ((UIElement)TextView).Dispatcher.Invoke((Action)(() => {
                TextView.Selection.Select(
                    new SnapshotSpan(TextView.TextBuffer.CurrentSnapshot, span),
                    false
                );
            }));
        }

        /// <summary>
        /// Moves the caret to the 1 based line and column
        /// </summary>
        public void MoveCaret(int line, int column) {
            var textLine = TextView.TextViewLines[line - 1];
            if (column - 1 == textLine.Length) {
                MoveCaret(textLine.End);
            } else {
                MoveCaret(new SnapshotPoint(TextView.TextBuffer.CurrentSnapshot, textLine.Start + column - 1));
            }
        }

        public void WaitForText(string text) {
            for (int i = 0; i < 10; i++) {
                if (Text != text) {
                    System.Threading.Thread.Sleep(1000);
                } else {
                    break;
                }
            }

            Assert.AreEqual(text, Text);
        }

        public void StartSmartTagSessionNoSession() {
            ShowSmartTag();
            System.Threading.Thread.Sleep(100);
            Assert.IsTrue(!(IntellisenseSessionStack.TopSession is ISmartTagSession));
        }

        private static void ShowSmartTag() {
            ThreadPool.QueueUserWorkItem(ShowSmartTagWorker);
        }

        private static void ShowSmartTagWorker(object dummy) {
            for (int i = 0; i < 20; i++) {
                try {
                    VsIdeTestHostContext.Dte.ExecuteCommand("View.ShowSmartTag");
                    break;
                } catch {
                    System.Threading.Thread.Sleep(500);
                }
            }
        }

        public ISmartTagSession StartSmartTagSession() {
            ShowSmartTag();
            return WaitForSession<ISmartTagSession>();
        }

        public T WaitForSession<T>() where T : IIntellisenseSession {
            var sessionStack = IntellisenseSessionStack;
            for (int i = 0; i < 100; i++) {
                if (sessionStack.TopSession is T) {
                    break;
                }
                System.Threading.Thread.Sleep(100);
            }

            Assert.IsTrue(sessionStack.TopSession is T);
            return (T)sessionStack.TopSession;
        }

        public IIntellisenseSessionStack IntellisenseSessionStack {
            get {
                var compModel = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
                var stackMapService = compModel.GetService<IIntellisenseSessionStackMapService>();

                return stackMapService.GetStackForTextView(TextView);
            }
        }

        public IClassifier Classifier {
            get {

                var compModel = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));

                var provider = compModel.GetService<IClassifierAggregatorService>();
                return provider.GetClassifier(TextView.TextBuffer);
            }
        }

        public ITagAggregator<T> GetTaggerAggregator<T>(ITextBuffer buffer) where T : ITag {
            var compModel = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));

            return compModel.GetService<Microsoft.VisualStudio.Text.Tagging.IBufferTagAggregatorFactoryService>().CreateTagAggregator<T>(buffer);
        }

        internal static IWpfTextView GetTextView(string filePath) {
            IVsUIHierarchy uiHierarchy;
            uint itemID;
            IVsWindowFrame windowFrame;

            if (VsShellUtilities.IsDocumentOpen(VsIdeTestHostContext.ServiceProvider, filePath, Guid.Empty, out uiHierarchy, out itemID, out windowFrame)) {
                var textView = VsShellUtilities.GetTextView(windowFrame);
                IComponentModel compModel = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
                var adapterFact = compModel.GetService<IVsEditorAdaptersFactoryService>();
                return adapterFact.GetWpfTextView(textView);
            }

            return null;
        }

        public void Invoke(Action action) {
            Exception excep = null;
            ((UIElement)TextView).Dispatcher.Invoke(
                (Action)(() => {
                    try {
                        action();
                    } catch (Exception e) {
                        excep = e;

                    }
                })
            );

            if (excep != null) {
                Assert.Fail("Exception on UI thread: " + excep.ToString());
            }
        }
    }
}
