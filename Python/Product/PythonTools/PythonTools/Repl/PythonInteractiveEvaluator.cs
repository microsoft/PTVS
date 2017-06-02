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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Language;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Commands;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Repl {
    [InteractiveWindowRole("Execution")]
    [InteractiveWindowRole("Reset")]
    [ContentType(PythonCoreConstants.ContentType)]
    [ContentType(PredefinedInteractiveCommandsContentTypes.InteractiveCommandContentTypeName)]
    partial class PythonInteractiveEvaluator :
        IInteractiveEvaluator,
        IPythonInteractiveEvaluator,
        IMultipleScopeEvaluator,
        IPythonInteractiveIntellisense, 
        IDisposable
    {
        protected readonly IServiceProvider _serviceProvider;
        private readonly StringBuilder _deferredOutput;

        private PythonProjectNode _projectWithHookedEvents;

        protected CommandProcessorThread _thread;
        private IInteractiveWindowCommands _commands;
        private IInteractiveWindow _window;
        private PythonInteractiveOptions _options;

        private VsProjectAnalyzer _analyzer;
        private readonly string _analysisFilename;

        private bool _enableMultipleScopes;
        private IReadOnlyList<string> _availableScopes;

        private bool _isDisposed;

        internal const string DoNotResetConfigurationLaunchOption = "DoNotResetConfiguration";

        public PythonInteractiveEvaluator(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _deferredOutput = new StringBuilder();
            _analysisFilename = Guid.NewGuid().ToString() + ".py";
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_analyzer")]
        protected void Dispose(bool disposing) {
            if (_isDisposed) {
                return;
            }
            _isDisposed = true;

            if (_projectWithHookedEvents != null) {
                _projectWithHookedEvents.ActiveInterpreterChanged -= Project_ConfigurationChanged;
                _projectWithHookedEvents._searchPaths.Changed -= Project_ConfigurationChanged;
                _projectWithHookedEvents = null;
            }

            if (disposing) {
                var thread = Interlocked.Exchange(ref _thread, null);
                if (thread != null) {
                    thread.Dispose();
                    WriteError(Strings.ReplExited);
                }
                _analyzer?.Dispose();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PythonInteractiveEvaluator() {
            Dispose(false);
        }

        public string DisplayName { get; set; }
        public string ProjectMoniker { get; set; }
        public LaunchConfiguration Configuration { get; set; }
        public string ScriptsPath { get; set; }

        public PythonLanguageVersion LanguageVersion {
            get {
                return Configuration?.Interpreter?.Version.ToLanguageVersion() ?? PythonLanguageVersion.None;
            }
        }

        public bool UseSmartHistoryKeys { get; set; }
        public bool LiveCompletionsOnly { get; set; }
        public string BackendName { get; set; }

        internal virtual void OnConnected() { }
        internal virtual void OnAttach() { }
        internal virtual void OnDetach() { }

        internal bool AssociatedProjectHasChanged { get; set; }

        private PythonProjectNode GetAssociatedPythonProject(InterpreterConfiguration interpreter = null) {
            _serviceProvider.GetUIThread().MustBeCalledFromUIThread();

            var moniker = ProjectMoniker;
            if (interpreter == null) {
                interpreter = Configuration?.Interpreter;
            }

            if (string.IsNullOrEmpty(moniker) && interpreter != null) {
                var interpreterService = _serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
                moniker = interpreterService.GetProperty(interpreter.Id, "ProjectMoniker") as string;
            }

            if (string.IsNullOrEmpty(moniker)) {
                return null;
            }

            return _serviceProvider.GetProjectFromFile(moniker);
        }


        public VsProjectAnalyzer Analyzer {
            get {
                if (_analyzer != null) {
                    return _analyzer;
                }

                var config = Configuration;
                IPythonInterpreterFactory factory = null;
                if (config?.Interpreter != null) {
                    var interpreterService = _serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
                    factory = interpreterService.FindInterpreter(config.Interpreter.Id);
                }

                if (factory == null) {
                    _analyzer = _serviceProvider.GetPythonToolsService().DefaultAnalyzer;
                } else {
                    var projectFile = GetAssociatedPythonProject(config.Interpreter)?.BuildProject;
                    _analyzer = new VsProjectAnalyzer(
                        _serviceProvider,
                        factory,
                        projectFile: projectFile,
                        comment: "{0} Interactive".FormatInvariant(DisplayName.IfNullOrEmpty("Unnamed"))
                    );
                }
                return _analyzer;
            }
        }

        public string AnalysisFilename => _analysisFilename;

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

        public string CurrentScopePath {
            get {
                return (_thread?.IsConnected ?? false) ? _thread.CurrentScopeFileName : null;
            }
        }

        public string CurrentWorkingDirectory {
            get {
                return (_thread?.IsConnected ?? false) ? _thread.CurrentWorkingDirectory : null;
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

                    _options = _serviceProvider.GetPythonToolsService().InteractiveOptions;
                    _options.Changed += InteractiveOptions_Changed;
                    UseSmartHistoryKeys = _options.UseSmartHistory;
                    LiveCompletionsOnly = _options.LiveCompletionsOnly;
                } else {
                    if (_options != null) {
                        _options.Changed -= InteractiveOptions_Changed;
                        _options = null;
                    }
                }
                _window = value;
            }
        }

        private async void InteractiveOptions_Changed(object sender, EventArgs e) {
            if (!ReferenceEquals(sender, _options)) {
                return;
            }

            UseSmartHistoryKeys = _options.UseSmartHistory;
            LiveCompletionsOnly = _options.LiveCompletionsOnly;

            var window = CurrentWindow;
            if (window == null) {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            window.TextView.Options.SetOptionValue(InteractiveWindowOptions.SmartUpDown, UseSmartHistoryKeys);
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

        public async Task<bool> GetSupportsMultipleStatementsAsync() {
            var thread = await EnsureConnectedAsync();
            if (thread == null) {
                return false;
            }

            return await thread.GetSupportsMultipleStatementsAsync();
        }

        private async void Thread_AvailableScopesChanged(object sender, EventArgs e) {
            _availableScopes = (await ((CommandProcessorThread)sender).GetAvailableUserScopesAsync(10000))?.ToArray();
            AvailableScopesChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<KeyValuePair<string, string>> GetAvailableScopesAndPaths() {
            var t = _thread?.GetAvailableScopesAndPathsAsync(1000);
            if (t != null && t.Wait(1000) && t.Result != null) {
                return t.Result;
            }
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        public CompletionResult[] GetMemberNames(string text) {
            return _thread?.GetMemberNames(text) ?? new CompletionResult[0];
        }

        public OverloadDoc[] GetSignatureDocumentation(string text) {
            return _thread?.GetSignatureDocumentation(text) ?? new OverloadDoc[0];
        }

        public void AbortExecution() {
            _thread?.AbortCommand();
        }

        public bool CanExecuteCode(string text) {
            if (string.IsNullOrEmpty(text)) {
                return true;
            }
            if (string.IsNullOrWhiteSpace(text) && text.EndsWith("\n")) {
                return true;
            }

            var config = Configuration;
            using (var parser = Parser.CreateParser(new StringReader(text), LanguageVersion)) {
                ParseResult pr;
                parser.ParseInteractiveCode(out pr);
                if (pr == ParseResult.IncompleteStatement) {
                    return text.EndsWith("\n");
                }
                if (pr == ParseResult.Empty || pr == ParseResult.IncompleteToken) {
                    return false;
                }
            }
            return true;
        }

        protected async Task<CommandProcessorThread> EnsureConnectedAsync() {
            var thread = Volatile.Read(ref _thread);
            if (thread != null) {
                return thread;
            }

            return await _serviceProvider.GetUIThread().InvokeTask(async () => {
                try {
                    UpdatePropertiesFromProjectMoniker();
                } catch (NoInterpretersException ex) {
                    WriteError(ex.ToString());
                    return null;
                } catch (MissingInterpreterException ex) {
                    WriteError(ex.ToString());
                    return null;
                } catch (DirectoryNotFoundException ex) {
                    WriteError(ex.ToString());
                    return null;
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    WriteError(ex.ToUnhandledExceptionMessage(GetType()));
                    return null;
                }

                var scriptsPath = ScriptsPath;
                if (!Directory.Exists(scriptsPath) && Configuration?.Interpreter != null) {
                    scriptsPath = GetScriptsPath(_serviceProvider, Configuration.Interpreter.Description, Configuration.Interpreter);
                }

                if (!string.IsNullOrEmpty(scriptsPath)) {
                    var modeFile = PathUtils.GetAbsoluteFilePath(scriptsPath, "mode.txt");
                    if (File.Exists(modeFile)) {
                        try {
                            BackendName = File.ReadAllLines(modeFile).FirstOrDefault(line =>
                                !string.IsNullOrEmpty(line) && !line.TrimStart().StartsWith("#")
                            );
                        } catch (Exception ex) when (!ex.IsCriticalException()) {
                            WriteError(Strings.ReplCannotReadFile.FormatUI(modeFile));
                        }
                    } else {
                        BackendName = null;
                    }
                }

                try {
                    thread = await ConnectAsync(default(CancellationToken));
                } catch (OperationCanceledException) {
                    thread = null;
                }

                var newerThread = Interlocked.CompareExchange(ref _thread, thread, null);
                if (newerThread != null) {
                    thread.Dispose();
                    return newerThread;
                }

                if (thread != null) {
                    await ExecuteStartupScripts(scriptsPath);

                    thread.AvailableScopesChanged += Thread_AvailableScopesChanged;
                }

                return thread;
            });
        }

        protected virtual async Task ExecuteStartupScripts(string scriptsPath) {
            if (File.Exists(scriptsPath)) {
                if (!(await ExecuteFileAsync(scriptsPath, null))) {
                    WriteError("Error executing " + scriptsPath);
                }
            } else if (Directory.Exists(scriptsPath)) {
                foreach (var file in PathUtils.EnumerateFiles(scriptsPath, "*.py", recurse: false)) {
                    if (!(await ExecuteFileAsync(file, null))) {
                        WriteError("Error executing " + file);
                    }
                }
            }
        }

        internal void UpdatePropertiesFromProjectMoniker() {
            if (_projectWithHookedEvents != null) {
                _projectWithHookedEvents.ActiveInterpreterChanged -= Project_ConfigurationChanged;
                _projectWithHookedEvents._searchPaths.Changed -= Project_ConfigurationChanged;
                _projectWithHookedEvents = null;
            }

            AssociatedProjectHasChanged = false;
            var pyProj = GetAssociatedPythonProject();
            if (pyProj == null) {
                return;
            }

            if (Configuration?.GetLaunchOption(DoNotResetConfigurationLaunchOption) == null) {
                Configuration = pyProj.GetLaunchConfigurationOrThrow();
                if (Configuration?.Interpreter != null) {
                    ScriptsPath = GetScriptsPath(_serviceProvider, Configuration.Interpreter.Description, Configuration.Interpreter);
                }
            }

            _projectWithHookedEvents = pyProj;
            pyProj.ActiveInterpreterChanged += Project_ConfigurationChanged;
            pyProj._searchPaths.Changed += Project_ConfigurationChanged;
        }

        private void Project_ConfigurationChanged(object sender, EventArgs e) {
            var pyProj = _projectWithHookedEvents;
            _projectWithHookedEvents = null;

            if (pyProj != null) {
                Debug.Assert(pyProj == sender || pyProj._searchPaths == sender, "Unexpected project raised the event");
                // Only warn once
                pyProj.ActiveInterpreterChanged -= Project_ConfigurationChanged;
                pyProj._searchPaths.Changed -= Project_ConfigurationChanged;
                WriteError(Strings.ReplProjectConfigurationChanged.FormatUI(pyProj.Caption));
                AssociatedProjectHasChanged = true;
            }
        }

        internal static string GetScriptsPath(
            IServiceProvider provider,
            string displayName,
            InterpreterConfiguration config,
            bool onlyIfExists = true
        ) {
            // TODO: Allow customizing the scripts path
            //var root = _serviceProvider.GetPythonToolsService().InteractiveOptions.ScriptsPath;
            string root;
            try {
                if (!provider.TryGetShellProperty((__VSSPROPID)__VSSPROPID2.VSSPROPID_VisualStudioDir, out root)) {
                    root = PathUtils.GetAbsoluteDirectoryPath(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Visual Studio {0}".FormatInvariant(AssemblyVersionInfo.VSName)
                    );
                }

                root = PathUtils.GetAbsoluteDirectoryPath(root, "Python Scripts");
            } catch (ArgumentException ex) {
                ex.ReportUnhandledException(provider, typeof(PythonInteractiveEvaluator));
                return null;
            }

            string candidate;
            if (!string.IsNullOrEmpty(displayName)) {
                candidate = PathUtils.GetAbsoluteDirectoryPath(root, displayName);
                if (!onlyIfExists || Directory.Exists(candidate)) {
                    return candidate;
                }
            }

            var version = config?.Version?.ToString();
            if (!string.IsNullOrEmpty(version)) {
                candidate = PathUtils.GetAbsoluteDirectoryPath(root, version);
                if (!onlyIfExists || Directory.Exists(candidate)) {
                    return candidate;
                }
            }

            return null;
        }

        public async Task<ExecutionResult> ExecuteCodeAsync(string text) {
            var cmdRes = _commands.TryExecuteCommand();
            if (cmdRes != null) {
                return await cmdRes;
            }

            var thread = await EnsureConnectedAsync();
            if (thread != null) {
                ExecutionResult result = await thread.ExecuteText(text);

                try {
                    await _serviceProvider.GetUIThread().InvokeTask(async () => await _serviceProvider.RefreshVariableViewsAsync());
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    Debug.Fail(ex.ToString());
                }
                
                return result;
            }

            WriteError(Strings.ReplDisconnected);
            return ExecutionResult.Failure;
        }

        public async Task<bool> ExecuteFileAsync(string filename, string extraArgs) {
            var thread = await EnsureConnectedAsync();
            if (thread != null) {
                return await thread.ExecuteFile(filename, extraArgs, "script");
            }

            WriteError(Strings.ReplDisconnected);
            return false;
        }

        public async Task<bool> ExecuteModuleAsync(string name, string extraArgs) {
            var thread = await EnsureConnectedAsync();
            if (thread != null) {
                return await thread.ExecuteFile(name, extraArgs, "module");
            }

            WriteError(Strings.ReplDisconnected);
            return false;
        }

        public async Task<bool> ExecuteProcessAsync(string filename, string extraArgs) {
            var thread = await EnsureConnectedAsync();
            if (thread != null) {
                return await thread.ExecuteFile(filename, extraArgs, "process");
            }

            WriteError(Strings.ReplDisconnected);
            return false;
        }

        const string _splitRegexPattern = @"(?x)\s*,\s*(?=(?:[^""]*""[^""]*"")*[^""]*$)"; // http://regexhero.net/library/52/
        private static Regex _splitLineRegex = new Regex(_splitRegexPattern);

        public string FormatClipboard() {
            if (Clipboard.ContainsData(DataFormats.CommaSeparatedValue)) {
                string data = Clipboard.GetData(DataFormats.CommaSeparatedValue) as string;
                if (data != null) {
                    string[] lines = data.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    StringBuilder res = new StringBuilder();
                    res.AppendLine("[");
                    foreach (var line in lines) {
                        string[] items = _splitLineRegex.Split(line);

                        res.Append("  [");
                        for (int i = 0; i < items.Length; i++) {
                            res.Append(FormatItem(items[i]));

                            if (i != items.Length - 1) {
                                res.Append(", ");
                            }
                        }
                        res.AppendLine("],");
                    }
                    res.AppendLine("]");
                    return res.ToString();
                }
            }

            var txt = Clipboard.GetText();
            if (!_serviceProvider.GetPythonToolsService().AdvancedOptions.PasteRemovesReplPrompts) {
                return txt;
            }


            return ReplPromptHelpers.RemovePrompts(
                txt,
                _window.TextView.Options.GetNewLineCharacter()
            );
        }

        private static string FormatItem(string item) {
            if (String.IsNullOrWhiteSpace(item)) {
                return "None";
            }
            double doubleVal;
            int intVal;
            if (Double.TryParse(item, out doubleVal) ||
                Int32.TryParse(item, out intVal)) {
                return item;
            }

            if (item[0] == '"' && item[item.Length - 1] == '"' && item.IndexOf(',') != -1) {
                // remove outer quotes, remove "" escaping
                item = item.Substring(1, item.Length - 2).Replace("\"\"", "\"");
            }

            // put in single quotes and escape single quotes and backslashes
            return "'" + item.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
        }

        public IEnumerable<string> GetAvailableScopes() {
            return _availableScopes ?? Enumerable.Empty<string>();
        }

        public void SetScope(string scopeName) {
            _thread?.SetScope(scopeName);
        }

        public string GetPrompt() {
            if ((_window?.CurrentLanguageBuffer.CurrentSnapshot.LineCount ?? 1) > 1) {
                return SecondaryPrompt;
            } else {
                return PrimaryPrompt;
            }
        }

        internal string PrimaryPrompt => _thread?.PrimaryPrompt ?? ">>> ";
        internal string SecondaryPrompt => _thread?.SecondaryPrompt ?? "... ";

        public async Task<ExecutionResult> InitializeAsync() {
            if (_commands != null) {
                // Already initialized
                return ExecutionResult.Success;
            }

            var msg = Strings.ReplInitializationMessage.FormatUI(
                DisplayName,
                AssemblyVersionInfo.Version,
                AssemblyVersionInfo.VSVersion
            ).Replace("&#x1b;", "\x1b");

            WriteOutput(msg, addNewline: true);

            _window.TextView.Options.SetOptionValue(InteractiveWindowOptions.SmartUpDown, UseSmartHistoryKeys);
            _commands = GetInteractiveCommands(_serviceProvider, _window, this);

            return ExecutionResult.Success;
        }

        public Task<ExecutionResult> ResetAsync(bool initialize = true) {
            return ResetWorkerAsync(initialize, false);
        }

        public Task<ExecutionResult> ResetAsync(bool initialize, bool quiet) {
            return ResetWorkerAsync(initialize, quiet);
        }

        private async Task<ExecutionResult> ResetWorkerAsync(bool initialize, bool quiet) {
            // suppress reporting "failed to launch repl" process
            var thread = Interlocked.Exchange(ref _thread, null);
            if (thread == null) {
                if (!quiet) {
                    WriteError(Strings.ReplNotStarted);
                }
                return ExecutionResult.Success;
            }

            foreach (var buffer in CurrentWindow.TextView.BufferGraph.GetTextBuffers(b => b.ContentType.IsOfType(PythonCoreConstants.ContentType))) {
                buffer.Properties[BufferParser.DoNotParse] = BufferParser.DoNotParse;
            }

            if (!quiet) {
                WriteOutput(Strings.ReplReset);
            }

            thread.IsProcessExpectedToExit = quiet;
            thread.Dispose();

            var options = _serviceProvider.GetPythonToolsService().InteractiveOptions;
            UseSmartHistoryKeys = options.UseSmartHistory;
            LiveCompletionsOnly = options.LiveCompletionsOnly;

            EnableMultipleScopes = false;

            return ExecutionResult.Success;
        }

        internal Task InvokeAsync(Action action) {
            return _window.TextView.VisualElement.Dispatcher.InvokeAsync(action).Task;
        }

        internal void WriteFrameworkElement(System.Windows.UIElement control, System.Windows.Size desiredSize) {
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

    internal static class PythonInteractiveEvaluatorExtensions {
        public static PythonInteractiveEvaluator GetPythonEvaluator(this IInteractiveWindow window) {
            var pie = window?.Evaluator as PythonInteractiveEvaluator;
            if (pie != null) {
                return pie;
            }

            pie = (window?.Evaluator as SelectableReplEvaluator)?.Evaluator as PythonInteractiveEvaluator;
            return pie;
        }

        public static async Task<bool> GetSupportsMultipleStatements(this IInteractiveWindow window) {
            var pie = window.GetPythonEvaluator();
            if (pie == null) {
                return false;
            }
            return await pie.GetSupportsMultipleStatementsAsync();
        }
    }
}
