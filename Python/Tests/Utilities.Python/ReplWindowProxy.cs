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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudioTools;

namespace TestUtilities.UI.Python {
    public sealed class ReplWindowProxy : IDisposable {
        private readonly VisualStudioApp _app;
        private readonly ToolWindowPane _toolWindow;
        private readonly IInteractiveWindow _window;
        private readonly ReplWindowProxySettings _settings;
        private readonly ReplWindowInfo _replWindowInfo;
        private readonly IEditorOperations _editorOperations;

        private List<Action> _onDispose;

        private static ConditionalWeakTable<ToolWindowPane, ReplWindowInfo> _replWindows =
            new ConditionalWeakTable<ToolWindowPane, ReplWindowInfo>();

        public const string StandardBackend = "standard";
        public const string IPythonBackend = "ptvsd.repl.ipython.IPythonBackend";

        internal ReplWindowProxy(VisualStudioApp app, IInteractiveWindow window, ToolWindowPane toolWindow, ReplWindowProxySettings settings) {
            Assert.IsNotNull(app, "app is required");
            Assert.IsNotNull(window, "window is required");
            _app = app;
            _window = window;
            _toolWindow = toolWindow;
            _settings = settings;
            _replWindowInfo = _replWindows.GetOrCreateValue(toolWindow);
            _window.ReadyForInput += _replWindowInfo.OnReadyForInput;
            _editorOperations = _app.ComponentModel.GetService<IEditorOperationsFactoryService>()
                .GetEditorOperations(_window.TextView);
        }

        public void Dispose() {
            Invoke(() => {
                ClearInput();

                if (_onDispose != null) {
                    foreach (var a in _onDispose) {
                        a();
                    }
                }

                Hide();
            });
        }

        public void OnDispose(Action action) {
            if (_onDispose == null) {
                _onDispose = new List<Action>();
            }
            _onDispose.Add(action);
        }

        public void Show() {
            ErrorHandler.ThrowOnFailure(((IVsWindowFrame)_toolWindow.Frame).Show());
        }

        public void Hide() {
            ErrorHandler.ThrowOnFailure(((IVsWindowFrame)_toolWindow.Frame).Hide());
            ErrorHandler.ThrowOnFailure(((IVsWindowPane)_toolWindow.GetIVsWindowPane()).ClosePane());
        }

        public void Invoke(Action action) {
            ((UIElement)_window.TextView).Dispatcher.Invoke(action);
        }

        public T Invoke<T>(Func<T> func) {
            return ((UIElement)_window.TextView).Dispatcher.Invoke(func);
        }

        public static ReplWindowProxy Prepare(
            PythonVisualStudioApp app,
            ReplWindowProxySettings settings,
            string projectName,
            string workspaceName,
            bool useIPython = false
        ) {
            settings.AssertValid();

            ReplWindowProxy result = null;
            try {
                result = OpenInteractive(app, settings, projectName, workspaceName, useIPython ? IPythonBackend : StandardBackend);
                app = null;

                for (int retries = 10; retries > 0; --retries) {
                    result.Reset();
                    result.ClearScreen();
                    result.ClearInput();

                    try {
                        var task = result.ExecuteText("print('READY')");
                        Assert.IsTrue(task.Wait(useIPython ? 30000 : 15000), "ReplWindow did not initialize in time");
                        if (!task.Result.IsSuccessful) {
                            continue;
                        }
                    } catch (TaskCanceledException) {
                        continue;
                    }


                    if (useIPython) {
                        // The longer we wait, the better are the chances of detecting this error
                        // This seems long enough to detect it when running locally
                        Thread.Sleep(500);

                        if (result.TextView.TextBuffer.CurrentSnapshot.Lines
                                .Any(l => l.GetText().Contains("Error using selected REPL back-end"))
                            ) {
                            Assert.Inconclusive("IPython is not available");
                        }

                        // In IPython mode, a help header appears at startup,
                        // but the output order is inconsistent, so we can't WaitForTextEnd
                        // (sometimes READY appears before help, sometimes after)
                        result.WaitForAnyLineContainsTextInternal("READY");
                        result.WaitForReadyForInput(TimeSpan.FromSeconds(5));
                    } else {
                        result.WaitForTextEnd("READY", ">");
                    }

                    result.ClearScreen();
                    return result;
                }
                Assert.Fail("ReplWindow did not initialize");
                return null;
            } finally {
                if (app != null) {
                    app.Dispose();
                }
            }
        }

        private static ToolWindowPane ActivateInteractiveWindow(ReplWindowProxySettings settings, VisualStudioApp app, string projectName, string workspaceName, string backend) {
            string description = null;
            if (settings.Version.IsCPython) {
                description = string.Format("{0} {1}",
                    settings.Version.Isx64 ? "Python 64-bit" : "Python 32-bit",
                    settings.Version.Version.ToVersion()
                );
            } else if (settings.Version.IsIronPython) {
                description = string.Format("{0} {1}",
                    settings.Version.Isx64 ? "IronPython 64-bit" : "IronPython",
                    settings.Version.Version.ToVersion()
                );
            }
            Assert.IsNotNull(description, "Unknown interpreter");

            var automation = (IVsPython)app.Dte.GetObject("VsPython");
            var options = (IPythonOptions)automation;
            var replOptions = options.Interactive;
            Assert.IsNotNull(replOptions, "Could not find options for " + description);

            var oldAddNewLineAtEndOfFullyTypedWord = options.Intellisense.AddNewLineAtEndOfFullyTypedWord;
            app.OnDispose(() => options.Intellisense.AddNewLineAtEndOfFullyTypedWord = oldAddNewLineAtEndOfFullyTypedWord);
            options.Intellisense.AddNewLineAtEndOfFullyTypedWord = settings.AddNewLineAtEndOfFullyTypedWord;

            var interpreters = app.ComponentModel.GetService<IInterpreterRegistryService>();
            var replId = PythonReplEvaluatorProvider.GetEvaluatorId(
                interpreters.FindConfiguration(settings.Version.Id)
            );

            if (!string.IsNullOrEmpty(projectName)) {
                var dteProj = app.GetProject(projectName);
                var proj = (PythonProjectNode)dteProj.GetCommonProject();
                replId = PythonReplEvaluatorProvider.GetEvaluatorId(proj);
            } else if (!string.IsNullOrEmpty(workspaceName)) {
                var workspaceContextProvider = app.ComponentModel.GetService<IPythonWorkspaceContextProvider>();
                replId = PythonReplEvaluatorProvider.GetEvaluatorId(workspaceContextProvider.Workspace);
            }

            return app.ServiceProvider.GetUIThread().Invoke(() => {
                app.ServiceProvider.GetPythonToolsService().InteractiveBackendOverride = backend;
                var provider = app.ComponentModel.GetService<InteractiveWindowProvider>();
                return (ToolWindowPane)provider.OpenOrCreate(replId);
            });
        }

        public string CurrentPrimaryPrompt {
            get {
                dynamic eval = _window.Evaluator;
                return eval.PrimaryPrompt as string ?? ">>>";
            }
        }

        public string CurrentSecondaryPrompt {
            get {
                dynamic eval = _window.Evaluator;
                return eval.SecondaryPrompt as string ?? "...";
            }
        }

        private static ReplWindowProxy OpenInteractive(
            VisualStudioApp app,
            ReplWindowProxySettings settings,
            string projectName,
            string workspaceName,
            string backend
        ) {
            var toolWindow = ActivateInteractiveWindow(settings, app, projectName, workspaceName, backend);

            var interactive = toolWindow != null ? ((IVsInteractiveWindow)toolWindow).InteractiveWindow : null;

            Assert.IsNotNull(interactive, "Could not find interactive window");
            return new ReplWindowProxy(app, interactive, toolWindow, settings);
        }


        public VisualStudioApp App { get { return _app; } }
        public IInteractiveWindow Window { get { return _window; } }

        public ReplWindowProxySettings Settings { get { return _settings; } }

        public Task<ExecutionResult> ExecuteText(string text) {
            return _window.Evaluator.ExecuteCodeAsync(text);
        }

        /// <summary>
        /// <para>Waits for the provided text to appear.</para>
        /// <para>
        /// A '&gt;', '.' or '&lt;' character at the start of a line is
        /// replaced with the current primary, secondary or input prompt
        /// respectively. A '\' character is removed (this may be used where
        /// a following '&gt;', '.' or '&lt;' should not be replaced).
        /// </para>
        /// </summary>
        public void WaitForText(params string[] lines) {
            WaitForTextInternal(GetReplLines(lines), true, true);
        }

        /// <summary>
        /// <para>Waits for the provided text to appear.</para>
        /// <para>
        /// A '&gt;', '.' or '&lt;' character at the start of a line is
        /// replaced with the current primary, secondary or input prompt
        /// respectively. A '\' character is removed (this may be used where
        /// a following '&gt;', '.' or '&lt;' should not be replaced).
        /// </para>
        /// </summary>
        public void WaitForText(IEnumerable<string> lines) {
            WaitForTextInternal(GetReplLines(lines), true, true);
        }

        /// <summary>
        /// <para>Waits for the provided text to appear at the top of the
        /// window.</para>
        /// <para>
        /// A '&gt;', '.' or '&lt;' character at the start of a line is
        /// replaced with the current primary, secondary or input prompt
        /// respectively. A '\' character is removed (this may be used where
        /// a following '&gt;', '.' or '&lt;' should not be replaced).
        /// </para>
        /// </summary>
        public void WaitForTextStart(params string[] lines) {
            WaitForTextInternal(GetReplLines(lines), true, false);
        }

        /// <summary>
        /// <para>Waits for the provided text to appear at the end of the
        /// window.</para>
        /// <para>
        /// A '&gt;', '.' or '&lt;' character at the start of a line is
        /// replaced with the current primary, secondary or input prompt
        /// respectively. A '\' character is removed (this may be used where
        /// a following '&gt;', '.' or '&lt;' should not be replaced).
        /// </para>
        /// </summary>
        public void WaitForTextEnd(params string[] lines) {
            WaitForTextInternal(GetReplLines(lines), false, true);
        }

        /// <summary>
        /// Waits for any line of text to contain the specified expected text.
        /// </summary>
        public void WaitForAnyLineContainsText(string expected) {
            WaitForAnyLineContainsTextInternal(expected);
        }

        private static readonly Regex IPythonPromptRegex = new Regex(
            @"^(
                \s*In\s*\[(?<num>\d+)\]\s*:(\s|\s*$)
               )",
            RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace,
            TimeSpan.FromSeconds(0.5)
        );

        private static string RemoveIndexFromIPythonPrompt(string line) {
            var m = IPythonPromptRegex.Match(line);
            if (m.Success) {
                var g = m.Groups["num"];
                line = line.Remove(g.Index, g.Length);
            }

            return line;
        }

        private static bool IsIPythonPrompt(string text) {
            return IPythonPromptRegex.Match(text).Success;
        }

        private List<string> GetReplLines(IEnumerable<string> lines) {
            dynamic eval = _window.Evaluator;
            var primary = eval.PrimaryPrompt as string ?? ">>>";
            var secondary = eval.SecondaryPrompt as string ?? "...";
            var input = "";

            // IPython prompts include an incrementing index, which we must remove for comparison
            primary = RemoveIndexFromIPythonPrompt(primary);

            return lines.Select(s => {
                if (string.IsNullOrEmpty(s)) {
                    return string.Empty;
                } else if (s[0] == '\\') {
                    return s.Substring(1);
                } else if (s[0] == '>') {
                    return primary + s.Substring(1);
                } else if (s[0] == '.') {
                    return secondary + s.Substring(1);
                } else if (s[0] == '<') {
                    return input + s.Substring(1);
                } else {
                    return s;
                }
            }).ToList();
        }

        private void WaitForAnyLineContainsTextInternal(string expected, TimeSpan? timeout = null) {
            using (var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15)))
            using (var changed = new ManualResetEventSlim()) {
                EventHandler<TextContentChangedEventArgs> handler = (s, e) => changed.SetIfNotDisposed();
                Window.TextView.TextBuffer.Changed += handler;
                try {
                    while (!MatchAnyLineContainsTextInternal(expected, true)) {
                        changed.Wait(cts.Token);
                        changed.Reset();
                    }
                    return;
                } catch (OperationCanceledException) {
                } finally {
                    Window.TextView.TextBuffer.Changed -= handler;
                }
            }

            Assert.Fail("Failed to find a line that contains the following text:\n{0}", expected);
        }

        private bool MatchAnyLineContainsTextInternal(string expected, bool showOutput) {
            var snapshot = Window.TextView.TextBuffer.CurrentSnapshot;
            var lines = snapshot.Lines;
            return lines.Any(l => l.GetText().Contains(expected));
        }

        private void WaitForTextInternal(IList<string> expected, bool matchAtStart, bool matchAtEnd, TimeSpan? timeout = null) {
            using (var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15)))
            using (var changed = new ManualResetEventSlim()) {
                EventHandler<TextContentChangedEventArgs> handler = (s, e) => changed.SetIfNotDisposed();
                Window.TextView.TextBuffer.Changed += handler;
                try {
                    while (!MatchTextInternal(expected, matchAtStart, matchAtEnd, true)) {
                        changed.Wait(cts.Token);
                        changed.Reset();
                    }
                    return;
                } catch (OperationCanceledException) {
                } finally {
                    Window.TextView.TextBuffer.Changed -= handler;
                }
            }

            MatchTextInternal(expected, matchAtStart, matchAtEnd, true);
            Assert.Fail("Failed to match text. See Output for details.");
        }

        private bool MatchTextInternal(IList<string> expected, bool matchAtStart, bool matchAtEnd, bool showOutput) {
            // Resplit lines to handle cases where linebreaks are embedded in
            // a single string. This helps ensure the comparison is correct and
            // the output is sensible.
            expected = expected.SelectMany(l => l.Split('\n')).Select(l => l.TrimEnd('\r', '\n', ' ')).ToList();
            var snapshot = Window.TextView.TextBuffer.CurrentSnapshot;
            var lines = snapshot.Lines;
            // Cap the number of lines we'll ever look at to avoid breaking here
            // when tests get stuck in infinite loops
            if (matchAtStart && !matchAtEnd) {
                lines = lines.Take(expected.Count + 1);
            } else if (!matchAtStart && matchAtEnd) {
                lines = lines.Skip(snapshot.LineCount - expected.Count - 2);
            }
            var actual = lines
                .SelectMany(l => l.GetText().Split('\n'))
                .Select(l => l.TrimEnd('\r', '\n', ' '))
                .ToList();

            var primary = CurrentPrimaryPrompt;
            if (IsIPythonPrompt(primary)) {
                // IPython prompts include an incrementing index, which we must remove for comparison
                actual = actual.Select(l => RemoveIndexFromIPythonPrompt(l)).ToList();
            }

            bool isMatch = true;
            var leftWidth = Math.Max("Expected".Length, expected.Max(s => s.Length));
            var format = string.Format("{{0}}{{1}}{{2}}{{3,-{0}}}   {{4}}", leftWidth);

            if (showOutput) {
                Console.WriteLine(format, " ", " ", " ", "Expected", "Actual");
                if (matchAtEnd && !matchAtStart) {
                    Console.WriteLine("(Lines in reverse order)");
                }
            }

            if (matchAtStart) {
                for (int i = 0; ; ++i) {
                    if (i >= expected.Count && i >= actual.Count) {
                        break;
                    } else if ((i >= expected.Count || i >= actual.Count) && matchAtEnd) {
                        isMatch = false;
                    }

                    bool lineMatch = false;
                    if (i < expected.Count && i < actual.Count) {
                        lineMatch = expected[i] == actual[i];
                        isMatch &= lineMatch;
                    }

                    if (showOutput) {
                        Console.WriteLine(
                            format,
                            i < expected.Count ? " " : "-",
                            lineMatch ? " " : "*",
                            i < actual.Count ? " " : "-",
                            i < expected.Count ? expected[i] : "",
                            i < actual.Count ? actual[i] : ""
                        );
                    }
                }
            } else if (matchAtEnd) {
                for (int i = -1; ; --i) {
                    int e_i = expected.Count + i;
                    int a_i = actual.Count + i;
                    if (e_i < 0 && a_i < 0) {
                        break;
                    }

                    bool lineMatch = false;
                    if (e_i > 0 && a_i > 0) {
                        lineMatch = expected[e_i] == actual[a_i];
                        isMatch &= lineMatch;
                    } else if (e_i == 0 && a_i >= 0) {
                        lineMatch = actual[a_i].EndsWith(expected[e_i]);
                        isMatch &= lineMatch;
                    }

                    if (showOutput) {
                        Console.WriteLine(
                            format,
                            e_i >= 0 ? " " : "-",
                            lineMatch ? " " : "*",
                            a_i >= 0 ? " " : "-",
                            e_i >= 0 ? expected[e_i] : "",
                            a_i >= 0 ? actual[a_i] : ""
                        );
                    }
                }
            } else {
                throw new NotImplementedException();
            }

            return isMatch;
        }


        public SessionHolder<T> WaitForSession<T>() where T : IIntellisenseSession {
            var sessionStack = _app.ComponentModel.GetService<IIntellisenseSessionStackMapService>().GetStackForTextView(_window.TextView);
            for (int retries = 0; retries < 40; retries++) {
                var res = sessionStack.TopSession;
                if (res is T) {
                    return new SessionHolder<T>((T)res, this);
                }
                Thread.Sleep(250);
                AssertListener.ThrowUnhandled();
            }

            Assert.Fail("Failed to find session " + typeof(T).FullName);
            throw new InvalidOperationException();
        }

        public void AssertNoSession(TimeSpan? delay = null) {
            Thread.Sleep(delay ?? TimeSpan.FromSeconds(5));
            var sessionStack = _app.ComponentModel.GetService<IIntellisenseSessionStackMapService>().GetStackForTextView(_window.TextView);
            Assert.IsNull(sessionStack.TopSession);
        }

        public class SessionHolder<T> : IDisposable where T : IIntellisenseSession {
            public readonly T Session;
            private readonly ReplWindowProxy _owner;

            public SessionHolder(T session, ReplWindowProxy owner) {
                Assert.IsNotNull(session);
                Session = session;
                _owner = owner;
            }

            void IDisposable.Dispose() {
                if (!Session.IsDismissed) {
                    _owner.Invoke(() => Session.Dismiss());
                }
            }

            public void Commit() {
                Assert.IsInstanceOfType(
                    Session,
                    typeof(ICompletionSession),
                    string.Format("{0} cannot be committed", typeof(T).Name)
                );
                _owner.Invoke(() => ((ICompletionSession)Session).Commit());
            }

            public void Dismiss() {
                _owner.Invoke(() => Session.Dismiss());
            }

            public void WaitForSessionDismissed(TimeSpan? timeout = null) {
                if (Session.IsDismissed) {
                    return;
                }

                using (var evt = new ManualResetEventSlim()) {
                    Session.Dismissed += (s, e) => evt.SetIfNotDisposed();
                    if (Session.IsDismissed) {
                        evt.Set();
                    }

                    Assert.IsTrue(
                        evt.Wait(timeout ?? TimeSpan.FromSeconds(10)),
                        string.Format("Timeout waiting for {0} to dismiss", typeof(T).Name)
                    );
                }
            }
        }

        private void SubmitOneLine(string line, bool wait) {
            if (wait) {
                _replWindowInfo.ReadyForInput.Reset();
            }

            if (!string.IsNullOrEmpty(line)) {
                Invoke(() => {
                    _editorOperations.InsertText(line);
                });
            }
            bool canExecute = Invoke(() => _window.Operations.TrySubmitStandardInput() || _window.Operations.Return());

            if (wait && canExecute) {
                Assert.IsTrue(
                    WaitForReadyForInput(TimeSpan.FromSeconds(10)),
                    "Timed out waiting for submitted code to execute: " + line
                );
            }
        }

        private bool WaitForReadyForInput(TimeSpan timeout) {
            // A series of quick checks for scenarios where we don't get
            // notifications via ReadyForInput.
            for (int checks = 10; checks > 0; --checks) {
                if (_replWindowInfo.ReadyForInput.WaitOne(TimeSpan.FromSeconds(0.1))) {
                    return true;
                }
                if (IsCaretInStandardInputRegion) {
                    return true;
                }
            }
            return _replWindowInfo.ReadyForInput.WaitOne(timeout.Subtract(TimeSpan.FromSeconds(1)));
        }

        /// <summary>
        /// Simulate typing lines of text into the window. This function
        /// effectively types an entire line and simulates the user pressing
        /// Enter at the end of it.
        /// 
        /// Use SubmitCode() to submit blocks where possible, as it is more
        /// efficient but does not perform auto-indent and cannot type into
        /// standard input.
        /// 
        /// Use Keyboard.Type() to simulate typing each character. This is
        /// necessary for IntelliSense tests, as Type() and SubmitCode() will
        /// not trigger IntelliSense sessions.
        /// </summary>
        public void Type(string text, bool commitLastLine = true, bool waitForLastLine = true) {
            var lines = text.Split('\n').Select(s => s.Trim('\r')).ToList();

            foreach (var line in lines.Take(lines.Count - 1)) {
                ((UIElement)_window.TextView).Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                SubmitOneLine(line, true);
            }
            if (commitLastLine) {
                SubmitOneLine(lines.Last(), waitForLastLine);
            } else {
                Invoke(() => {
                    _editorOperations.InsertText(lines.Last());
                });
            }
        }

        private static IEnumerable<string> SplitCodeIntoBlocks(IEnumerable<string> lines) {
            var sb = new StringBuilder();
            foreach (var line in lines) {
                if ((string.IsNullOrEmpty(line) || !char.IsWhiteSpace(line[0])) && sb.Length > 0) {
                    yield return sb.ToString();
                    sb.Clear();
                }

                if (sb.Length > 0) {
                    sb.AppendLine();
                }
                sb.Append(line);

            }
            if (sb.Length > 0) {
                yield return sb.ToString();
            }
        }

        /// <summary>
        /// Execute a string of code in the window. This is more efficient than
        /// simulating typing, but does not perform auto-indent and cannot type
        /// into standard input.
        /// 
        /// Use Type() to simulate typing lines of text. It is slightly less
        /// efficient, but can type into standard input and can also type
        /// without executing the code.
        /// 
        /// Use Keyboard.Type() to simulate typing each character. This is
        /// necessary for IntelliSense tests, as Type() and SubmitCode() will
        /// not trigger IntelliSense sessions.
        /// </summary>
        public void SubmitCode(string text, bool wait = true, TimeSpan? timeout = null) {
            if (wait) {
                _replWindowInfo.ReadyForInput.Reset();
            }
            var code = SplitCodeIntoBlocks(text.Split('\n').Select(s => s.Trim('\r')));
            _window.SubmitAsync(code);
            if (wait) {
                Assert.IsTrue(
                    WaitForReadyForInput(timeout ?? TimeSpan.FromSeconds(60)),
                    "Timed out waiting for code to submit"
                );
            }
        }

        public void Paste(string text) {
            Invoke(() => {
                Clipboard.SetText(text, TextDataFormat.Text);
            });
            _app.ExecuteCommand("Edit.Paste");
        }

        public void ClearInput() {
            Invoke(() => {
                var buffer = _window.CurrentLanguageBuffer;
                if (buffer == null) {
                    return;
                }

                var edit = buffer.CreateEdit();
                edit.Delete(0, edit.Snapshot.Length);
                edit.Apply();
            });
        }

        private bool IsCaretInStandardInputRegion {
            get {
                var point = _window.TextView.BufferGraph.MapDownToInsertionPoint(
                    _window.TextView.Caret.Position.BufferPosition,
                    PointTrackingMode.Positive,
                    _ => true
                );
                return point.HasValue && point.Value.Snapshot.ContentType.IsOfType(PredefinedInteractiveContentTypes.InteractiveContentTypeName);
            }
        }

        private bool IsCaretInActiveCodeRegion {
            get {
                if (_window.CurrentLanguageBuffer == null) {
                    return false;
                }

                return _window.TextView.BufferGraph.MapDownToBuffer(
                    _window.TextView.Caret.Position.BufferPosition,
                    PointTrackingMode.Positive,
                    _window.CurrentLanguageBuffer,
                    PositionAffinity.Successor
                ) != null;
            }
        }

        public void ClearScreen() {
            for (int retries = 10; retries > 0 && !IsCaretInActiveCodeRegion; --retries) {
                Thread.Sleep(100);
            }

            ((UIElement)_window.TextView).Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            _replWindowInfo.ReadyForInput.Reset();
            Invoke(_window.Operations.ClearHistory);
            Invoke(_window.Operations.ClearView);
            Assert.IsTrue(
                _replWindowInfo.ReadyForInput.WaitOne(TimeSpan.FromSeconds(10.0)),
                "Timed out waiting for ClearScreen()"
            );
        }

        public void ClearHistory() {
            Invoke(_window.Operations.ClearHistory);
        }

        public void SubmitCurrentText() {
            Invoke(_window.Operations.ExecuteInput);
        }

        public void Backspace(int count = 1) {
            while (count-- > 0) {
                Invoke(_window.Operations.Backspace);
            }
        }

        public void PreviousHistoryItem(int count = 1, bool search = false) {
            while (count-- > 0) {
                Invoke(search ?
                    (Action)(() => _window.Operations.HistorySearchPrevious()) :
                    (Action)(() => _window.Operations.HistoryPrevious())
                );
            }
        }

        public void NextHistoryItem(int count = 1, bool search = false) {
            while (count-- > 0) {
                Invoke(search ?
                    (Action)(() => _window.Operations.HistorySearchNext()) :
                    (Action)(() => _window.Operations.HistoryNext())
                );
            }
        }

        public void Reset() {
            var t = _window.Operations.ResetAsync();
            Assert.IsTrue(t.Wait(TimeSpan.FromSeconds(15)), "Timed out resetting the window");
            Assert.IsTrue(t.Result.IsSuccessful, "Window failed to reset");
        }

        public void CancelExecution(int attempts = 100) {
            Console.WriteLine("REPL Cancelling Execution");
            var rfi = _replWindowInfo.ReadyForInput;
            rfi.Reset();
            for (int i = 0; i < attempts && !rfi.WaitOne(0); i++) {
                rfi.Reset();
                try {
                    Invoke(() => {
                        _window.Evaluator.AbortExecution();
                    });
                    // The command succeeded, so wait longer
                    if (rfi.WaitOne(1000)) {
                        break;
                    }
                } catch {
                    // command may not be immediately available
                }
                if (rfi.WaitOne(100)) {
                    break;
                }
            }
            Assert.IsTrue(rfi.WaitOne(10000));
        }

        public IWpfTextView TextView {
            get {
                return _window.TextView;
            }
        }

        public IClassifier Classifier {
            get {
                var provider = _app.ComponentModel.GetService<IClassifierAggregatorService>();
                return provider.GetClassifier(TextView.TextBuffer);
            }
        }

        private sealed class ReplWindowInfo {
            public readonly ManualResetEvent ReadyForInput = new ManualResetEvent(false);

            public void OnReadyForInput() {
                ReadyForInput.Set();
            }
        }

        internal SnapshotSpan? GetContainingRegion(SnapshotPoint snapshotPoint) {
            var point = _window.TextView.BufferGraph.MapDownToInsertionPoint(
                snapshotPoint,
                PointTrackingMode.Positive,
                _ => true
            );
            if (point == null) {
                return null;
            }
            return new SnapshotSpan(point.Value.Snapshot, 0, point.Value.Snapshot.Length);
        }
    }
}
