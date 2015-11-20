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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Repl {
    [InteractiveWindowRole("Execution")]
    [InteractiveWindowRole("Reset")]
    [ContentType(PythonCoreConstants.ContentType)]
    [ContentType(PredefinedInteractiveCommandsContentTypes.InteractiveCommandContentTypeName)]
    partial class PythonInteractiveEvaluator :
        IPythonInteractiveEvaluator,
        IMultipleScopeEvaluator,
        IPythonInteractiveIntellisense
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly StringBuilder _deferredOutput;

        private CommandProcessorThread _thread;
        private IInteractiveWindowCommands _commands;
        private IInteractiveWindow _window;

        private bool _enableMultipleScopes;
        private IReadOnlyList<string> _availableScopes;

        public PythonInteractiveEvaluator(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _deferredOutput = new StringBuilder();
            EnvironmentVariables = new Dictionary<string, string>();
            _enableMultipleScopes = true;
            var options = _serviceProvider.GetPythonToolsService().InteractiveOptions;
            UseSmartHistoryKeys = options.UseSmartHistory;
            LiveCompletionsOnly = options.LiveCompletionsOnly;
        }

        public string DisplayName { get; set; }
        public string InterpreterPath { get; set; }
        public PythonLanguageVersion LanguageVersion { get; set; }
        public string InterpreterArguments {get; set; }
        public string WorkingDirectory { get; set; }
        public IDictionary<string, string> EnvironmentVariables { get; set; }
        public string ScriptsPath { get; set; }

        public bool UseSmartHistoryKeys { get; set; }
        public bool LiveCompletionsOnly { get; set; }

        internal virtual void OnConnected() { }
        internal virtual void OnAttach() { }
        internal virtual void OnDetach() { }

        internal void WriteOutput(string text, bool addNewline = true) {
            var wnd = CurrentWindow;
            if (wnd == null) {
                lock (_deferredOutput) {
                    _deferredOutput.Append(text);
                }
            } else {
                AppendTextWithEscapes(wnd, text, addNewline, isError: false);
            }
        }

        internal void WriteError(string text, bool addNewline = true) {
            var wnd = CurrentWindow;
            if (wnd == null) {
                lock (_deferredOutput) {
                    _deferredOutput.Append(text);
                }
            } else {
                AppendTextWithEscapes(wnd, text, addNewline, isError: true);
            }
        }

        public bool IsDisconnected => !(_thread?.IsConnected ?? false);

        public bool IsExecuting => (_thread?.IsExecuting ?? false);

        public string CurrentScopeName {
            get {
                return (_thread?.IsConnected ?? false) ? _thread.CurrentScope : "<disconnected>";
            }
        }

        public IInteractiveWindow CurrentWindow {
            get {
                return _window;
            }
            set {
                if (_window != null) {
                }
                _commands = null;

                if (value != null) {
                    lock (_deferredOutput) {
                        AppendTextWithEscapes(value, _deferredOutput.ToString(), false, false);
                        _deferredOutput.Clear();
                    }
                }
                _window = value;
            }
        }

        public bool EnableMultipleScopes {
            get { return _enableMultipleScopes; }
            set {
                if (_enableMultipleScopes != value) {
                    _enableMultipleScopes = value;
                    MultipleScopeSupportChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler<EventArgs> AvailableScopesChanged;
        public event EventHandler<EventArgs> MultipleScopeSupportChanged;

        private async void Thread_AvailableScopesChanged(object sender, EventArgs e) {
            _availableScopes = (await ((CommandProcessorThread)sender).GetAvailableUserScopesAsync(10000))?.ToArray();
            AvailableScopesChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<KeyValuePair<string, bool>> GetAvailableScopesAndKind() {
            var t = _thread?.GetAvailableScopesAndKindAsync(1000);
            if (t != null && t.Wait(1000) && t.Result != null) {
                return t.Result;
            }
            return Enumerable.Empty<KeyValuePair<string, bool>>();
        }

        public MemberResult[] GetMemberNames(string text) {
            return _thread?.GetMemberNames(text) ?? new MemberResult[0];
        }

        public OverloadDoc[] GetSignatureDocumentation(string text) {
            return _thread?.GetSignatureDocumentation(text) ?? new OverloadDoc[0];
        }

        public void AbortExecution() {
            _thread?.AbortCommand();
        }

        public bool CanExecuteCode(string text) {
            if (text.EndsWith("\n")) {
                return true;
            }

            using (var parser = Parser.CreateParser(new StringReader(text), LanguageVersion)) {
                ParseResult pr;
                parser.ParseInteractiveCode(out pr);
                if (pr == ParseResult.IncompleteToken || pr == ParseResult.IncompleteStatement) {
                    return false;
                }
            }
            return true;
        }

        public void Dispose() {
            var thread = Interlocked.Exchange(ref _thread, null);
            if (thread != null) {
                thread.Dispose();
                WriteError(SR.GetString(SR.ReplExited));
            }
        }

        private async Task<CommandProcessorThread> EnsureConnectedAsync() {
            var thread = Volatile.Read(ref _thread);
            if (thread != null) {
                return thread;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            thread = Connect();

            var newerThread = Interlocked.CompareExchange(ref _thread, thread, null);
            if (newerThread != null) {
                thread.Dispose();
                return newerThread;
            }

            if (File.Exists(ScriptsPath)) {
                if (!(await ExecuteFileAsync(ScriptsPath, null)).IsSuccessful) {
                    WriteError("Error executing " + ScriptsPath);
                }
            } else if (Directory.Exists(ScriptsPath)) {
                foreach (var file in Directory.EnumerateFiles(ScriptsPath, "*.py", SearchOption.TopDirectoryOnly)) {
                    if (!(await ExecuteFileAsync(file, null)).IsSuccessful) {
                        WriteError("Error executing " + file);
                    }
                }
            }

            thread.AvailableScopesChanged += Thread_AvailableScopesChanged;
            return thread;
        }

        public async Task<ExecutionResult> ExecuteCodeAsync(string text) {
            var cmdRes = _commands.TryExecuteCommand();
            if (cmdRes != null) {
                return await cmdRes;
            }

            var thread = await EnsureConnectedAsync();
            if (thread != null) {
                return await thread.ExecuteText(text);
            }

            WriteError(SR.GetString(SR.ReplDisconnected));
            return ExecutionResult.Failure;
        }

        public async Task<ExecutionResult> ExecuteFileAsync(string filename, string extraArgs) {
            var thread = await EnsureConnectedAsync();
            if (thread != null) {
                return await thread.ExecuteFile(filename, extraArgs, "script");
            }

            WriteError(SR.GetString(SR.ReplDisconnected));
            return ExecutionResult.Failure;
        }

        public string FormatClipboard() {
            return System.Windows.Forms.Clipboard.GetText();
        }

        public IEnumerable<string> GetAvailableScopes() {
            return _availableScopes ?? Enumerable.Empty<string>();
        }

        public void SetScope(string scopeName) {
            _thread?.SetScope(scopeName);
        }

        public string GetPrompt() {
            if ((_window?.CurrentLanguageBuffer.CurrentSnapshot.LineCount ?? 1) > 1) {
                return _thread?.SecondaryPrompt ?? "... ";
            } else {
                return _thread?.PrimaryPrompt ?? ">>> ";
            }
        }

        public async Task<ExecutionResult> InitializeAsync() {
            if (_commands != null) {
                // Already initialized
                return ExecutionResult.Success;
            }

            var msg = SR.GetString(
                SR.ReplInitializationMessage,
                DisplayName,
                AssemblyVersionInfo.Version,
                AssemblyVersionInfo.VSVersion
            ).Replace("&#x1b;", "\x1b");

            WriteOutput(msg, addNewline: true);

            //_window.TextView.BufferGraph.GraphBuffersChanged += BufferGraphGraphBuffersChanged;

            _window.TextView.Options.SetOptionValue(InteractiveWindowOptions.SmartUpDown, UseSmartHistoryKeys);
            _commands = GetInteractiveCommands(_serviceProvider, _window, this);

            return ExecutionResult.Success;
        }

        public Task<ExecutionResult> ResetAsync(bool initialize = true) {
            return ResetWorkerAsync(initialize, false);
        }

        private async Task<ExecutionResult> ResetWorkerAsync(bool initialize, bool quiet) {
            // suppress reporting "failed to launch repl" process
            var thread = Interlocked.Exchange(ref _thread, null);
            if (thread == null) {
                if (!quiet) {
                    WriteError(SR.GetString(SR.ReplNotStarted));
                }
                return ExecutionResult.Success;
            }

            if (!quiet) {
                WriteOutput(SR.GetString(SR.ReplReset));
            }

            thread.IsProcessExpectedToExit = quiet;
            thread.Dispose();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            thread = Connect();
            
            return ExecutionResult.Success;
        }

        internal Task InvokeAsync(Action action) {
            return ((UIElement)_window).Dispatcher.InvokeAsync(action).Task;
        }

        internal void WriteFrameworkElement(UIElement control, Size desiredSize) {
            if (_window == null) {
                return;
            }

            _window.Write("");
            _window.FlushOutput();

            var caretPos = _window.TextView.Caret.Position.BufferPosition;
            var manager = InlineReplAdornmentProvider.GetManager(_window.TextView);
            manager.AddAdornment(new ZoomableInlineAdornment(control, _window.TextView, desiredSize), caretPos);
        }


        internal static IInteractiveWindowCommands GetInteractiveCommands(
            IServiceProvider serviceProvider,
            IInteractiveWindow window,
            IInteractiveEvaluator eval
        ) {
            var model = serviceProvider.GetComponentModel();
            var cmdFactory = model.GetService<IInteractiveWindowCommandsFactory>();
            var cmds = model.GetExtensions<IInteractiveWindowCommand>();
            var roles = eval.GetType()
                .GetCustomAttributes(typeof(InteractiveWindowRoleAttribute), true)
                .Select(r => ((InteractiveWindowRoleAttribute)r).Name)
                .ToArray();

            var contentTypeRegistry = model.GetService<IContentTypeRegistryService>();
            var contentTypes = eval.GetType()
                .GetCustomAttributes(typeof(ContentTypeAttribute), true)
                .Select(r => contentTypeRegistry.GetContentType(((ContentTypeAttribute)r).ContentTypes))
                .ToArray();

            return cmdFactory.CreateInteractiveCommands(
                window,
                "$",
                cmds.Where(x => IsCommandApplicable(x, roles, contentTypes))
            );
        }

        private static bool IsCommandApplicable(
            IInteractiveWindowCommand command,
            string[] supportedRoles,
            IContentType[] supportedContentTypes
        ) {
            var commandRoles = command.GetType().GetCustomAttributes(typeof(InteractiveWindowRoleAttribute), true).Select(r => ((InteractiveWindowRoleAttribute)r).Name).ToArray();

            // Commands with no roles are always applicable.
            // If a command specifies roles and none apply, exclude it
            if (commandRoles.Any() && !commandRoles.Intersect(supportedRoles).Any()) {
                return false;
            }

            var commandContentTypes = command.GetType()
                .GetCustomAttributes(typeof(ContentTypeAttribute), true)
                .Select(a => ((ContentTypeAttribute)a).ContentTypes)
                .ToArray();

            // Commands with no content type are always applicable
            // If a commands specifies content types and none apply, exclude it
            if (commandContentTypes.Any() && !commandContentTypes.Any(cct => supportedContentTypes.Any(sct => sct.IsOfType(cct)))) {
                return false;
            }

            return true;
        }

        #region Append Text helpers

        private static void AppendTextWithEscapes(
            IInteractiveWindow window,
            string text,
            bool addNewLine,
            bool isError
        ) {
            int start = 0, escape = text.IndexOf("\x1b[");
            var colors = window.OutputBuffer.Properties.GetOrCreateSingletonProperty(
                ReplOutputClassifier.ColorKey,
                () => new List<ColoredSpan>()
            );
            ConsoleColor? color = null;

            Span span;
            var write = isError ? (Func<string, Span>)window.WriteError : window.Write;

            while (escape >= 0) {
                span = write(text.Substring(start, escape - start));
                if (span.Length > 0) {
                    colors.Add(new ColoredSpan(span, color));
                }

                start = escape + 2;
                color = GetColorFromEscape(text, ref start);
                escape = text.IndexOf("\x1b[", start);
            }

            var rest = text.Substring(start);
            if (addNewLine) {
                rest += Environment.NewLine;
            }

            span = write(rest);
            if (span.Length > 0) {
                colors.Add(new ColoredSpan(span, color));
            }
        }

        private static ConsoleColor Change(ConsoleColor? from, ConsoleColor to) {
            return ((from ?? ConsoleColor.Black) & ConsoleColor.DarkGray) | to;
        }

        private static ConsoleColor? GetColorFromEscape(string text, ref int start) {
            // http://en.wikipedia.org/wiki/ANSI_escape_code
            // process any ansi color sequences...
            ConsoleColor? color = null;
            List<int> codes = new List<int>();
            int? value = 0;

            while (start < text.Length) {
                if (text[start] >= '0' && text[start] <= '9') {
                    // continue parsing the integer...
                    if (value == null) {
                        value = 0;
                    }
                    value = 10 * value.Value + (text[start] - '0');
                } else if (text[start] == ';') {
                    if (value != null) {
                        codes.Add(value.Value);
                        value = null;
                    } else {
                        // CSI ; - invalid or CSI ### ;;, both invalid
                        break;
                    }
                } else if (text[start] == 'm') {
                    start += 1;
                    if (value != null) {
                        codes.Add(value.Value);
                    }

                    // parsed a valid code
                    if (codes.Count == 0) {
                        // reset
                        color = null;
                    } else {
                        for (int j = 0; j < codes.Count; j++) {
                            switch (codes[j]) {
                                case 0: color = ConsoleColor.White; break;
                                case 1: // bright/bold
                                    color |= ConsoleColor.DarkGray;
                                    break;
                                case 2: // faint

                                case 3: // italic
                                case 4: // single underline
                                    break;
                                case 5: // blink slow
                                case 6: // blink fast
                                    break;
                                case 7: // negative
                                case 8: // conceal
                                case 9: // crossed out
                                case 10: // primary font
                                case 11: // 11-19, n-th alternate font
                                    break;
                                case 21: // bright/bold off 
                                    color &= ~ConsoleColor.DarkGray;
                                    break;
                                case 22: // normal intensity
                                case 24: // underline off
                                    break;
                                case 25: // blink off
                                    break;
                                case 27: // image - postive
                                case 28: // reveal
                                case 29: // not crossed out
                                case 30: color = Change(color, ConsoleColor.Black); break;
                                case 31: color = Change(color, ConsoleColor.DarkRed); break;
                                case 32: color = Change(color, ConsoleColor.DarkGreen); break;
                                case 33: color = Change(color, ConsoleColor.DarkYellow); break;
                                case 34: color = Change(color, ConsoleColor.DarkBlue); break;
                                case 35: color = Change(color, ConsoleColor.DarkMagenta); break;
                                case 36: color = Change(color, ConsoleColor.DarkCyan); break;
                                case 37: color = Change(color, ConsoleColor.Gray); break;
                                case 38: // xterm 286 background color
                                case 39: // default text color
                                    color = null;
                                    break;
                                case 40: // background colors
                                case 41:
                                case 42:
                                case 43:
                                case 44:
                                case 45:
                                case 46:
                                case 47: break;
                                case 90: color = ConsoleColor.DarkGray; break;
                                case 91: color = ConsoleColor.Red; break;
                                case 92: color = ConsoleColor.Green; break;
                                case 93: color = ConsoleColor.Yellow; break;
                                case 94: color = ConsoleColor.Blue; break;
                                case 95: color = ConsoleColor.Magenta; break;
                                case 96: color = ConsoleColor.Cyan; break;
                                case 97: color = ConsoleColor.White; break;
                            }
                        }
                    }
                    break;
                } else {
                    // unknown char, invalid escape
                    break;
                }
                start += 1;
            }
            return color;
        }

        #endregion
    }
}
