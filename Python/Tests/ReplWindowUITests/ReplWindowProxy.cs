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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using TestUtilities;
using TestUtilities.UI.Python;

namespace ReplWindowUITests {
    internal sealed class ReplWindowProxySettings {
        public ReplWindowProxySettings() {
            PrimaryPrompt = ">>>";
            SecondaryPrompt = "...";
            InlinePrompts = true;
            UseInterpreterPrompts = false;
            SourceFileName = "stdin";
            IntFirstMember = "bit_length";
            RawInput = "raw_input";
            IPythonIntDocumentation = Python2IntDocumentation;
            Print42Output = "42";
            ImportError = "ImportError: No module named {0}";
        }

        public ReplWindowProxySettings Clone() {
            return (ReplWindowProxySettings)MemberwiseClone();
        }

        public const string Python2IntDocumentation = @"Type:        int
String form: 42
Docstring:
int(x=0) -> int or long
int(x, base=10) -> int or long

Convert a number or string to an integer, or return 0 if no arguments
are given.  If x is floating point, the conversion truncates towards zero.
If x is outside the integer range, the function returns a long instead.

If x is not a number or if base is given, then x must be a string or
Unicode object representing an integer literal in the given base.  The
literal can be preceded by '+' or '-' and be surrounded by whitespace.
The base defaults to 10.  Valid bases are 0 and 2-36.  Base 0 means to
interpret the base from the string as an integer literal.
>>> int('0b100', base=0)
4";

        public const string Python3IntDocumentation = @"Type:        int
String form: 42
Docstring:
int(x=0) -> integer
int(x, base=10) -> integer

Convert a number or string to an integer, or return 0 if no arguments
are given.  If x is a number, return x.__int__().  For floating point
numbers, this truncates towards zero.

If x is not a number or if base is given, then x must be a string,
bytes, or bytearray instance representing an integer literal in the
given base.  The literal can be preceded by '+' or '-' and be surrounded
by whitespace.  The base defaults to 10.  Valid bases are 0 and 2-36.
Base 0 means to interpret the base from the string as an integer literal.
>>> int('0b100', base=0)
4";

        public PythonVersion Version { get; set; }

        public string PrimaryPrompt { get; set; }

        public string SecondaryPrompt { get; set; }

        public bool UseInterpreterPrompts { get; set; }

        public bool InlinePrompts { get; set; }

        public string SourceFileName { get; set; }

        public string IPythonIntDocumentation { get; set; }

        public string IntFirstMember { get; set; }

        public string RawInput { get; set; }

        public string Print42Output { get; set; }

        public bool KeyboardInterruptHasTracebackHeader { get; set; }

        public bool EnableAttach { get; set; }

        public string ImportError { get; set; }
    }

    internal sealed class ReplWindowProxy : IDisposable {
        private readonly PythonVisualStudioApp _app;
        private readonly ReplWindow _window;
        private readonly ReplWindowProxySettings _settings;
        private readonly ReplWindowInfo _replWindowInfo;
        private readonly IEditorOperations _editorOperations;

        private Dictionary<ReplOptions, object> _restoreOptions;
        private List<Action> _onDispose;

        private static ConditionalWeakTable<ReplWindow, ReplWindowInfo> _replWindows =
            new ConditionalWeakTable<ReplWindow, ReplWindowInfo>();

        internal ReplWindowProxy(PythonVisualStudioApp app, ReplWindow window, ReplWindowProxySettings settings) {
            Assert.IsNotNull(app, "app is required");
            Assert.IsNotNull(window, "window is required");
            _app = app;
            _window = window;
            _settings = settings;
            _replWindowInfo = _replWindows.GetOrCreateValue(_window);
            _window.ReadyForInput += _replWindowInfo.OnReadyForInput;
            _editorOperations = _app.ComponentModel.GetService<IEditorOperationsFactoryService>()
                .GetEditorOperations(_window.TextView);
        }

        public void Dispose() {
            Invoke(() => {
                ClearInput();

                if (_restoreOptions != null) {
                    foreach (var kv in _restoreOptions) {
                        _window.SetOptionValue(kv.Key, kv.Value);
                    }
                }
                if (_onDispose != null) {
                    foreach (var a in _onDispose) {
                        a();
                    }
                }

                ((IVsWindowFrame)_window.Frame).Hide();
            });

            _app.Dispose();
        }

        public void OnDispose(Action action) {
            if (_onDispose == null) {
                _onDispose = new List<Action>();
            }
            _onDispose.Add(action);
        }

        public void Invoke(Action action) {
            ((UIElement)_window.TextView).Dispatcher.Invoke(action);
        }

        public T Invoke<T>(Func<T> func) {
            return ((UIElement)_window.TextView).Dispatcher.Invoke(func);
        }

        public static ReplWindowProxy Prepare(
            ReplWindowProxySettings settings,
            bool useIPython = false
        ) {
            settings.Version.AssertInstalled();

            var app = new PythonVisualStudioApp();
            ReplWindowProxy result = null;
            try {
                var wnd = OpenInteractive(app, settings, useIPython ? "IPython" : "Standard");
                result = new ReplWindowProxy(app, wnd, settings);
                app = null;

                result.Window.Reset();
                result.ClearInput();

                for (int retries = 10; retries > 0; --retries) {
                    result.Window.Reset();
                    try {
                        var task = result.Window.Evaluator.ExecuteText("print('READY')");
                        Assert.IsTrue(task.Wait(useIPython ? 30000 : 10000), "ReplWindow did not initialize in time");
                        if (!task.Result.IsSuccessful) {
                            continue;
                        }
                    } catch (TaskCanceledException) {
                        continue;
                    }

                    result.WaitForTextEnd("READY", ">");
                    if (result.TextView.TextBuffer.CurrentSnapshot.Lines
                            .Any(l => l.GetText().Contains("Error using selected REPL back-end")) &&
                        useIPython) {
                        Assert.Inconclusive("IPython is not available");
                    }
                    result.ClearScreen();
                    result.ClearHistory();
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

        private static ReplWindow OpenInteractive(
            PythonVisualStudioApp app,
            ReplWindowProxySettings settings,
            string executionMode
        ) {
            string description = null;
            if (settings.Version.IsCPython) {
                description = string.Format("{0} {1}",
                    settings.Version.Isx64 ? CPythonInterpreterFactoryConstants.Description64 : CPythonInterpreterFactoryConstants.Description32,
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
            var options = ((IPythonOptions)automation).GetInteractiveOptions(description);

            options.InlinePrompts = settings.InlinePrompts;
            options.UseInterpreterPrompts = settings.UseInterpreterPrompts;
            options.PrimaryPrompt = settings.PrimaryPrompt;
            options.SecondaryPrompt = settings.SecondaryPrompt;
            options.EnableAttach = settings.EnableAttach;

            var oldExecutionMode = options.ExecutionMode;
            app.OnDispose(() => options.ExecutionMode = oldExecutionMode);
            options.ExecutionMode = executionMode;

            bool success = false;
            for (int retries = 1; retries < 20; ++retries) {
                try {
                    app.ExecuteCommand("Python.Interactive", "/e:\"" + description + "\"");
                    success = true;
                    break;
                } catch (AggregateException) {
                }
                app.DismissAllDialogs();
                app.SetFocus();
                Thread.Sleep(retries * 100);
            }
            Assert.IsTrue(success, "Unable to open " + description + " through DTE");
            var provider = app.ComponentModel.GetService<IReplWindowProvider>();
            var interpreters = app.ComponentModel.GetService<IInterpreterOptionsService>();
            var replId = PythonReplEvaluatorProvider.GetReplId(
                interpreters.FindInterpreter(settings.Version.Id, settings.Version.Version.ToVersion())
            );

            var interactive = provider.FindReplWindow(replId) as ReplWindow;

            if (interactive == null) {
                // This is a failure, since we check if the environment is
                // installed in TestInitialize().
                Assert.Fail("Need " + description);
            }

            return interactive;
        }


        public PythonVisualStudioApp App { get { return _app; } }
        public ReplWindow Window { get { return _window; } }

        public ReplWindowProxySettings Settings { get { return _settings; } }

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

        private List<string> GetReplLines(IEnumerable<string> lines) {
            var primary = _window.GetOptionValue(ReplOptions.PrimaryPrompt) as string ?? ">>>";
            var secondary = _window.GetOptionValue(ReplOptions.SecondaryPrompt) as string ?? "...";
            var input = _window.GetOptionValue(ReplOptions.StandardInputPrompt) as string ?? "";

            if (_window.GetOptionValue(ReplOptions.DisplayPromptInMargin) as bool? ?? false) {
                primary = secondary = input = "";
            }

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

        private void WaitForTextInternal(IList<string> expected, bool matchAtStart, bool matchAtEnd, TimeSpan? timeout = null) {
            using (var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15)))
            using (var changed = new ManualResetEventSlim()) {
                EventHandler<TextContentChangedEventArgs> handler = (s, e) => changed.Set();
                Window.TextBuffer.Changed += handler;
                try {
                    while (!MatchTextInternal(expected, matchAtStart, matchAtEnd, true)) {
                        changed.Wait(cts.Token);
                        changed.Reset();
                    }
                    return;
                } catch (OperationCanceledException) {
                } finally {
                    Window.TextBuffer.Changed -= handler;
                }
            }

            MatchTextInternal(expected, matchAtStart, matchAtEnd, true);
            Assert.Fail("Failed to match text. See Output for details.");
        }

        private bool MatchTextInternal(IList<string> expected, bool matchAtStart, bool matchAtEnd, bool showOutput) {
            // Resplit lines to handle cases where linebreaks are embedded in
            // a single string. This helps ensure the comparison is correct and
            // the output is sensible.
            expected = expected.SelectMany(l => l.Split('\n')).Select(l => l.TrimEnd('\r', '\n')).ToList();
            var actual = Window.TextBuffer.CurrentSnapshot.Lines
                .SelectMany(l => l.GetText().Split('\n'))
                .Select(l => l.TrimEnd('\r', '\n'))
                .ToList();

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
                    if (e_i >= 0 && a_i >= 0) {
                        lineMatch = expected[e_i] == actual[a_i];
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
                    Session.Dismissed += (s, e) => evt.Set();
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

            bool canExecute = Invoke(() => {
                if (!string.IsNullOrEmpty(line)) {
                _editorOperations.InsertText(line);
                    }
                var res = _window.Evaluator.CanExecuteText(_window.CurrentLanguageBuffer.CurrentSnapshot.GetText());
                var pkgCmdSet = VSConstants.VSStd2K;
                ErrorHandler.ThrowOnFailure(_window.Exec(
                    ref pkgCmdSet,
                    (uint)VSConstants.VSStd2KCmdID.RETURN,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero
                ));
                return res;
            });

            if (wait && canExecute) {
                // A series of quick checks for scenarios where we don't get
                // notifications via ReadyForInput.
                for (int checks = 10; checks > 0; --checks) {
                    if (_replWindowInfo.ReadyForInput.WaitOne(TimeSpan.FromSeconds(0.1))) {
                        return;
                    }
                    if (_window.CaretInStandardInputRegion) {
                        return;
                    }
                }
                Assert.IsTrue(
                    _replWindowInfo.ReadyForInput.WaitOne(TimeSpan.FromSeconds(9.0)),
                    "Timed out waiting for submitted code to execute: " + line
                );
            }
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

            ((UIElement)_window.TextView).Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            foreach (var line in lines.Take(lines.Count - 1)) {
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
            _window.Submit(SplitCodeIntoBlocks(text.Split('\n').Select(s => s.Trim('\r'))));
            if (wait) {
                Assert.IsTrue(
                    _replWindowInfo.ReadyForInput.WaitOne(timeout ?? TimeSpan.FromSeconds(60)),
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

        public void ClearScreen() {
            for (int retries = 10; retries > 0 && !_window.CaretInActiveCodeRegion; --retries) {
                Thread.Sleep(100);
            }

            ((UIElement)_window.TextView).Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            _replWindowInfo.ReadyForInput.Reset();
            Invoke(() => {
                var pkgCmdSet = Guids.guidReplWindowCmdSet;
                ErrorHandler.ThrowOnFailure(_window.Exec(
                    ref pkgCmdSet,
                    (uint)Microsoft.VisualStudio.Repl.PkgCmdIDList.cmdidReplClearScreen,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero
                ));
                _window.ClearHistory();
            });
            Assert.IsTrue(
                _replWindowInfo.ReadyForInput.WaitOne(TimeSpan.FromSeconds(10.0)),
                "Timed out waiting for ClearScreen()"
            );
        }

        public void ClearHistory() {
            Invoke(_window.ClearHistory);
        }

        public void SubmitCurrentText() {
            Invoke(_window.Submit);
        }

        public void Backspace(int count = 1) {
            while (count-- > 0) {
                _app.ExecuteCommand("Edit.DeleteBackwards");
            }
        }

        public void PreviousHistoryItem(int count = 1, bool search = false) {
            while (count-- > 0) {
                _app.ExecuteCommand(search ?
                    "PythonInteractive.SearchHistoryPrevious" :
                    "PythonInteractive.HistoryPrevious"
                );
            }
        }

        public void NextHistoryItem(int count = 1, bool search = false) {
            while (count-- > 0) {
                _app.ExecuteCommand(search ?
                    "PythonInteractive.SearchHistoryNext" :
                    "PythonInteractive.HistoryNext"
                );
            }
        }

        public void Reset() {
            var t = _window.Reset();
            Assert.IsTrue(t.Wait(TimeSpan.FromSeconds(15)), "Timed out resetting the window");
            Assert.AreEqual(ExecutionResult.Success, t.Result, "Window failed to reset");
        }
        
        public void CancelExecution(int attempts = 100) {
            Console.WriteLine("REPL Cancelling Execution");
            var rfi = _replWindowInfo.ReadyForInput;
            rfi.Reset();
            for (int i = 0; i < attempts && !rfi.WaitOne(0); i++) {
                rfi.Reset();
                try {
                    _app.ExecuteCommand("PythonInteractive.Cancel");
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

        public object GetOptionValue(ReplOptions option) {
            return _window.GetOptionValue(option);
        }

        public void SetOptionValue(ReplOptions option, object value) {
            if (_restoreOptions == null) {
                _restoreOptions = new Dictionary<ReplOptions, object>();
            }

            try {
                _restoreOptions.Add(option, _window.GetOptionValue(option));
            } catch (ArgumentException) {
                // Don't overwrite existing 'restore' values
            }
            _window.SetOptionValue(option, value);
        }

        public void WaitForAnalysis(TimeSpan? timeout = null) {
            var stopAt = DateTime.Now.Add(timeout ?? TimeSpan.FromSeconds(60));
            _window.TextView.GetAnalyzer().WaitForCompleteAnalysis(_ => DateTime.Now < stopAt);
            if (DateTime.Now >= stopAt) {
                Assert.Fail("Timeout waiting for complete analysis");
            }
            // Most of the time we're waiting to ensure that IntelliSense will
            // work, which normally requires a bit more time.
            Thread.Sleep(500);
        }

        public bool AddNewLineAtEndOfFullyTypedWord {
            get {
                var options = (IPythonOptions)App.Dte.GetObject("VsPython");
                return options.Intellisense.AddNewLineAtEndOfFullyTypedWord;
            }
            set {
                var options = (IPythonOptions)App.Dte.GetObject("VsPython");
                var currentValue = options.Intellisense.AddNewLineAtEndOfFullyTypedWord;
                options.Intellisense.AddNewLineAtEndOfFullyTypedWord = value;
                OnDispose(() => options.Intellisense.AddNewLineAtEndOfFullyTypedWord = currentValue);
            }
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

        public void RequirePrimaryPrompt() {
            if (string.IsNullOrEmpty(Settings.PrimaryPrompt)) {
                Assert.Inconclusive("Test requires a non-empty primary prompt");
            }
        }

        public void RequireSecondaryPrompt() {
            if (string.IsNullOrEmpty(Settings.SecondaryPrompt) || !Settings.InlinePrompts) {
                Assert.Inconclusive("Test requires a non-empty secondary prompt");
            }
        }

        public void EnsureInputFunction() {
            if (Settings.RawInput != "input") {
                Type("input = " + Settings.RawInput);
                WaitForTextEnd(">input = " + Settings.RawInput, ">");
                ClearScreen();
            }
        }


        private sealed class ReplWindowInfo {
            public readonly ManualResetEvent Idle = new ManualResetEvent(false);
            public readonly ManualResetEvent ReadyForInput = new ManualResetEvent(false);

            public void OnReadyForInput() {
                ReadyForInput.Set();
            }
        }
    }

}
