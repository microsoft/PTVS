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
using System.Text;
using System.Threading;
using System.Windows.Automation;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text.Editor;

namespace AnalysisTest.UI {
    class InteractiveWindow : EditorWindow {
        private readonly string _title;

        public InteractiveWindow(string title, AutomationElement element)
            : base(null, element) {
            _title = title;
        }
        
        public void WaitForText(params string[] text) {
            string expected = GetExpectedText(text);

            for (int i = 0; i < 100; i++) {
                string curText = Text;

                if (expected == Text) {
                    return;
                }
                Thread.Sleep(100);
            }

            FailWrongText(expected);
        }

        public void WaitForTextStart(params string[] text) {
            string expected = GetExpectedText(text);

            for (int i = 0; i < 100; i++) {
                string curText = Text;

                if (Text.StartsWith(expected)) {
                    return;
                }
                Thread.Sleep(100);
            }

            FailWrongText(expected);
        }

        private void FailWrongText(string expected) {
            StringBuilder msg = new StringBuilder("Did not get text: ");
            AppendRepr(msg, expected);
            msg.Append(" instead got ");
            AppendRepr(msg, Text);
            Assert.Fail(msg.ToString());
        }

        private static string GetExpectedText(string[] text) {
            StringBuilder finalString = new StringBuilder();
            for (int i = 0; i < text.Length; i++) {
                if (i != 0) {
                    finalString.Append(Environment.NewLine);
                }

                finalString.Append(text[i]);
            }

            string expected = finalString.ToString();
            return expected;
        }

        private static void AppendRepr(StringBuilder msg, string str) {
            for (int i = 0; i < str.Length; i++) {
                if (str[i] >= 32) {
                    msg.Append(str[i]);
                } else {
                    switch (str[i]) {
                        case '\n': msg.Append("\\n"); break;

                        case '\r': msg.Append("\\r"); break;
                        case '\t': msg.Append("\\t"); break;
                        default: msg.AppendFormat("\\u00{0:D2}", (int)str[i]); break;
                    }
                }
            }
        }

        public void WaitForSessionDismissed() {
            var sessionStack = IntellisenseSessionStack;
            for (int i = 0; i < 100; i++) {
                if (sessionStack.TopSession == null) {
                    break;
                }
                System.Threading.Thread.Sleep(100);
            }
            Assert.AreEqual(null, sessionStack.TopSession);
        }

        public void ClearScreen() {
            VsIdeTestHostContext.Dte.ExecuteCommand("OtherContextMenus.InteractiveConsole.ClearScreen");
        }

        public void CancelExecution() {
            VsIdeTestHostContext.Dte.ExecuteCommand("OtherContextMenus.InteractiveConsole.CancelExecution");
        }

        public IReplWindow ReplWindow {
            get {
                var compModel = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
                var replWindowProvider = compModel.GetService<IReplWindowProvider>();
                return GetReplWindow(replWindowProvider);
            }
        }

        public override IWpfTextView TextView {
            get {
                return ReplWindow.TextView;
            }
        }

        private IReplWindow GetReplWindow(IReplWindowProvider replWindowProvider) {
            IReplWindow curWindow = null;
            foreach (var provider in replWindowProvider.GetReplWindows()) {
                if (provider.Title == _title) {
                    curWindow = provider;
                    break;
                }
            }
            return curWindow;
        }

        public void Reset() {
            ReplWindow.Reset();
        }
    }
}
