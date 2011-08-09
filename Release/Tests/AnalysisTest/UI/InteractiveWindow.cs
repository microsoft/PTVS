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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text.Editor;
using System.Diagnostics;

namespace AnalysisTest.UI {
#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
    using IReplWindowProvider = IInteractiveWindowProvider;
#endif

    class InteractiveWindow : EditorWindow {
        private sealed class ReplWindowInfo {
            public readonly ManualResetEvent Idle = new ManualResetEvent(false);
            public readonly ManualResetEvent ReadyForInput = new ManualResetEvent(false);

            public void OnReadyForInput() {
                Debug.WriteLine("Ready for input");
                ReadyForInput.Set();
            }
        }

        private static ConditionalWeakTable<IReplWindow, ReplWindowInfo> _replWindows = new ConditionalWeakTable<IReplWindow, ReplWindowInfo>();

        private readonly string _title;
        private readonly IReplWindow _replWindow;
        private readonly ReplWindowInfo _replWindowInfo;

        public InteractiveWindow(string title, AutomationElement element)
            : base(null, element) {
            _title = title;

            var compModel = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
            var replWindowProvider = compModel.GetService<IReplWindowProvider>();
            _replWindow = GetReplWindow(replWindowProvider);

            _replWindowInfo = _replWindows.GetValue(_replWindow, window => {
                var info = new ReplWindowInfo();
                window.ReadyForInput += new Action(info.OnReadyForInput);
                return info;
            });
        }

        public void WaitForReadyState(int timeout = 500) {
            Assert.IsTrue(_replWindowInfo.ReadyForInput.WaitOne(timeout));
        }

        public void WaitForIdleState() {
            DispatchAndWait(_replWindowInfo.Idle, () => { }, DispatcherPriority.ApplicationIdle);
        }

        public void DispatchAndWait(EventWaitHandle waitHandle, Action action, DispatcherPriority priority = DispatcherPriority.Normal) {
            Dispatcher dispatcher = ((FrameworkElement)ReplWindow.TextView).Dispatcher;
            waitHandle.Reset();

            dispatcher.Invoke(new Action(() => { 
                action();
                waitHandle.Set(); 
            }), priority);

            Assert.IsTrue(waitHandle.WaitOne(500));
        }

        public void WaitForText(params string[] text) {
            WaitForText((IList<string>)text);
        }

        public void WaitForText(IList<string> text) {
            string expected = null;
            for (int i = 0; i < 100; i++) {
                WaitForIdleState();
                expected = GetExpectedText(text);
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

        public void WaitForTextEnd(params string[] text) {
            string expected = GetExpectedText(text);

            for (int i = 0; i < 100; i++) {
                string curText = Text;

                if (Text.EndsWith(expected)) {
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

        private static string GetExpectedText(IList<string> text) {
            StringBuilder finalString = new StringBuilder();
            for (int i = 0; i < text.Count; i++) {
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
            Debug.WriteLine("REPL Clearing screen");
            _replWindowInfo.ReadyForInput.Reset();
            VsIdeTestHostContext.Dte.ExecuteCommand("OtherContextMenus.InteractiveConsole.ClearScreen");
            WaitForReadyState();
        }

        public void CancelExecution(int attempts = 100) {
            Debug.WriteLine("REPL Cancelling Execution");
            _replWindowInfo.ReadyForInput.Reset();
            for (int i = 0; i < attempts; i++) {
                try {
                    VsIdeTestHostContext.Dte.ExecuteCommand("OtherContextMenus.InteractiveConsole.CancelExecution");
                    break;
                } catch {
                    // command may not be immediately available
                    Thread.Sleep(1000);
                }
            }
            WaitForReadyState(10000);
        }

        public IReplWindow ReplWindow {
            get {
                return _replWindow;
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
            Debug.WriteLine("REPL resetting");
            
            Assert.IsTrue(ReplWindow.Reset().Wait(10000));
        }

        public void WithStandardInputPrompt(string prompt, Action<string> action) {
            if ((bool)ReplWindow.GetOptionValue(ReplOptions.DisplayPromptInMargin)) {
                action("");
                return;
            }

            string oldPrompt = (string)ReplWindow.GetOptionValue(ReplOptions.StandardInputPrompt);
            ReplWindow.SetOptionValue(ReplOptions.StandardInputPrompt, prompt);
            try {
                action(prompt);
            } finally {
                ReplWindow.SetOptionValue(ReplOptions.StandardInputPrompt, oldPrompt);
            }
        }
    }
}
