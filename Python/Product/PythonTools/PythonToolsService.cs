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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools {

    /// <summary>
    /// Provides services and state which need to be available to various PTVS components.
    /// </summary>
    public sealed class PythonToolsService : IDisposable {
        private readonly IServiceContainer _container;
        private readonly Lazy<LanguagePreferences> _langPrefs;
        private IPythonToolsOptionsService _optionsService;
        private Lazy<IInterpreterOptionsService> _interpreterOptionsService;
        private VsProjectAnalyzer _analyzer;
        private readonly PythonToolsLogger _logger;
        private readonly Lazy<AdvancedEditorOptions> _advancedOptions;
        private readonly Lazy<DebuggerOptions> _debuggerOptions;
        private readonly Lazy<DiagnosticsOptions> _diagnosticsOptions;
        private readonly Lazy<GeneralOptions> _generalOptions;
        private readonly Lazy<PythonInteractiveOptions> _debugInteractiveOptions;
        private readonly Lazy<PythonInteractiveOptions> _interactiveOptions;
        private readonly Lazy<SuppressDialogOptions> _suppressDialogOptions;
        private readonly Lazy<SurveyNewsService> _surveyNews;
        private readonly AnalysisEntryService _entryService;
        private readonly IdleManager _idleManager;
        private readonly DiagnosticsProvider _diagnosticsProvider;
        private ExpansionCompletionSource _expansionCompletions;
        private Func<CodeFormattingOptions> _optionsFactory;
        private const string _formattingCat = "Formatting";

        private readonly Dictionary<IVsCodeWindow, CodeWindowManager> _codeWindowManagers = new Dictionary<IVsCodeWindow, CodeWindowManager>();

        private static readonly Dictionary<string, OptionInfo> _allFormattingOptions = new Dictionary<string, OptionInfo>();

        public static object CreateService(IServiceContainer container, Type serviceType) {
            if (serviceType.IsEquivalentTo(typeof(PythonToolsService))) {
                // register our PythonToolsService which provides access to core PTVS functionality
                try {
                    return new PythonToolsService(container);
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    ex.ReportUnhandledException(container, typeof(PythonToolsService), allowUI: false);
                    throw;
                }
            }
            return null;
        }

        internal PythonToolsService(IServiceContainer container) {
            _container = container;

            _langPrefs = new Lazy<LanguagePreferences>(() => new LanguagePreferences(this, typeof(PythonLanguageInfo).GUID));
            _interpreterOptionsService = new Lazy<IInterpreterOptionsService>(CreateInterpreterOptionsService);

            _optionsService = (IPythonToolsOptionsService)container.GetService(typeof(IPythonToolsOptionsService));

            _idleManager = new IdleManager(container);
            _advancedOptions = new Lazy<AdvancedEditorOptions>(CreateAdvancedEditorOptions);
            _debuggerOptions = new Lazy<DebuggerOptions>(CreateDebuggerOptions);
            _diagnosticsOptions = new Lazy<DiagnosticsOptions>(CreateDiagnosticsOptions);
            _generalOptions = new Lazy<GeneralOptions>(CreateGeneralOptions);
            _surveyNews = new Lazy<SurveyNewsService>(() => new SurveyNewsService(this));
            _suppressDialogOptions = new Lazy<SuppressDialogOptions>(() => new SuppressDialogOptions(this));
            _interactiveOptions = new Lazy<PythonInteractiveOptions>(() => CreateInteractiveOptions("Interactive"));
            _debugInteractiveOptions = new Lazy<PythonInteractiveOptions>(() => CreateInteractiveOptions("Debug Interactive Window"));
            _logger = new PythonToolsLogger(ComponentModel.GetExtensions<IPythonToolsLogger>().ToArray());
            _entryService = (AnalysisEntryService)ComponentModel.GetService<IAnalysisEntryService>();
            _diagnosticsProvider = new DiagnosticsProvider(container);

            _idleManager.OnIdle += OnIdleInitialization;

            EditorServices.SetPythonToolsService(this);
        }

        private void OnIdleInitialization(object sender, ComponentManagerEventArgs e) {
            Site.AssertShellIsInitialized();

            _idleManager.OnIdle -= OnIdleInitialization;

            _expansionCompletions = new ExpansionCompletionSource(Site);
            InitializeLogging();
        }

        public void Dispose() {
            // This will probably never be called by VS, but we use it in unit
            // tests to avoid leaking memory when we reinitialize state between
            // each test.

            if (_langPrefs.IsValueCreated) {
                _langPrefs.Value.Dispose();
            }

            if (_interpreterOptionsService.IsValueCreated) {
                _interpreterOptionsService.Value.DefaultInterpreterChanged -= UpdateDefaultAnalyzer;
                (_interpreterOptionsService.Value as IDisposable)?.Dispose();
            }

            _idleManager.Dispose();

            foreach (var window in _codeWindowManagers.Values.ToArray()) {
                window.RemoveAdornments();
            }
            _codeWindowManagers.Clear();
        }

        private void InitializeLogging() {
            try {
                var registry = ComponentModel.GetService<IInterpreterRegistryService>();
                if (registry != null) { // not available in some test cases...
                                                    // log interesting stats on startup
                    var installed = registry.Configurations.Count();
                    var installedV2 = registry.Configurations.Count(c => c.Version.Major == 2);
                    var installedV3 = registry.Configurations.Count(c => c.Version.Major == 3);

                    _logger.LogEvent(PythonLogEvent.InstalledInterpreters, new Dictionary<string, object> {
                        { "Total", installed },
                        { "3x", installedV3 },
                        { "2x", installedV2 }
                    });
                }

                _logger.LogEvent(PythonLogEvent.SurveyNewsFrequency, GeneralOptions.SurveyNewsCheck.ToString());
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }
        }

        private void UpdateDefaultAnalyzer(object sender, EventArgs args) {
            // no need to update if analyzer isn't created yet.
            if (_analyzer == null) {
                return;
            }

            _container.GetUIThread().InvokeTask(async () => {
                var analyzer = CreateAnalyzer();
                var oldAnalyzer = Interlocked.Exchange(ref _analyzer, analyzer);
                if (oldAnalyzer != null) {
                    await analyzer.TransferFromOldAnalyzer(oldAnalyzer);
                    if (oldAnalyzer.RemoveUser()) {
                        oldAnalyzer.Dispose();
                    }
                }
            }).DoNotWait();
        }

        /// <summary>
        /// Asks the interpreter to generate its completion database if the
        /// option is enabled (the default) and the database is not current.
        /// </summary>
        internal void EnsureCompletionDb(IPythonInterpreterFactory factory) {
            if (GeneralOptions.AutoAnalyzeStandardLibrary) {
                var withDb = factory as IPythonInterpreterFactoryWithDatabase;
                if (withDb != null && !withDb.IsCurrent) {
                    withDb.GenerateDatabase(GenerateDatabaseOptions.SkipUnchanged);
                }
            }
        }

        internal PythonEditorServices EditorServices => ComponentModel.GetService<PythonEditorServices>();

        internal string GetDiagnosticsLog(bool includeAnalysisLogs) {
            return _diagnosticsProvider.GetLog(includeAnalysisLogs);
        }

        private IInterpreterOptionsService CreateInterpreterOptionsService() {
            var service = ComponentModel.GetService<IInterpreterOptionsService>();
            // may not available in some test cases
            if (service != null) {
                service.DefaultInterpreterChanged += UpdateDefaultAnalyzer;
            }
            return service;
        }

        private VsProjectAnalyzer CreateAnalyzer() {
            var interpreters = _interpreterOptionsService.Value;

            // may not available in some test cases
            if (interpreters == null) {
                return new VsProjectAnalyzer(EditorServices, InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7)));
            }

            var defaultFactory = interpreters.DefaultInterpreter;
            EnsureCompletionDb(defaultFactory);
            return new VsProjectAnalyzer(EditorServices, defaultFactory);
        }

        internal PythonToolsLogger Logger => _logger;
        internal SurveyNewsService SurveyNews => _surveyNews.Value;

        #region Public API

        public VsProjectAnalyzer DefaultAnalyzer {
            get {
                if (_analyzer == null) {
                    _analyzer = _container.GetUIThread().Invoke(() => CreateAnalyzer());
                }
                return _analyzer;
            }
        }

        public VsProjectAnalyzer MaybeDefaultAnalyzer => _analyzer;

        public AdvancedEditorOptions AdvancedOptions => _advancedOptions.Value;
        public DebuggerOptions DebuggerOptions => _debuggerOptions.Value;
        public DiagnosticsOptions DiagnosticsOptions => _diagnosticsOptions.Value;
        public GeneralOptions GeneralOptions => _generalOptions.Value;
        internal PythonInteractiveOptions DebugInteractiveOptions => _debugInteractiveOptions.Value;

        private AdvancedEditorOptions CreateAdvancedEditorOptions() {
            var opts = new AdvancedEditorOptions(this);
            opts.Load();
            return opts;
        }

        private DebuggerOptions CreateDebuggerOptions() {
            var opts = new DebuggerOptions(this);
            opts.Load();
            return opts;
        }

        private DiagnosticsOptions CreateDiagnosticsOptions() {
            var opts = new DiagnosticsOptions(this);
            opts.Load();
            return opts;
        }

        private GeneralOptions CreateGeneralOptions() {
            var opts = new GeneralOptions(this);
            opts.Load();
            return opts;
        }

        #endregion

        internal SuppressDialogOptions SuppressDialogOptions => _suppressDialogOptions.Value;

        #region Code formatting options

        /// <summary>
        /// Gets a new CodeFormattinOptions object configured to the users current settings.
        /// </summary>
        public CodeFormattingOptions GetCodeFormattingOptions() {
            if (_optionsFactory == null) {
                // create a factory which can create CodeFormattingOptions without tons of reflection
                var initializers = new Dictionary<OptionInfo, Action<CodeFormattingOptions, object>>();
                foreach (CodeFormattingCategory curCat in Enum.GetValues(typeof(CodeFormattingCategory))) {
                    if (curCat == CodeFormattingCategory.None) {
                        continue;
                    }

                    var cat = OptionCategory.GetOptions(curCat);
                    foreach (var option in cat) {
                        var propInfo = typeof(CodeFormattingOptions).GetProperty(option.Key);

                        if (propInfo.PropertyType == typeof(bool)) {
                            initializers[option] = MakeFastSetter<bool>(propInfo);
                        } else if (propInfo.PropertyType == typeof(bool?)) {
                            initializers[option] = MakeFastSetter<bool?>(propInfo);
                        } else if (propInfo.PropertyType == typeof(int)) {
                            initializers[option] = MakeFastSetter<int>(propInfo);
                        } else {
                            throw new InvalidOperationException(String.Format("Unsupported formatting option type: {0}", propInfo.PropertyType.FullName));
                        }
                    }
                }

                _optionsFactory = CreateOptionsFactory(initializers);
            }

            return _optionsFactory();
        }

        private static Action<CodeFormattingOptions, object> MakeFastSetter<T>(PropertyInfo propInfo) {
            var fastSet = (Action<CodeFormattingOptions, T>)Delegate.CreateDelegate(typeof(Action<CodeFormattingOptions, T>), propInfo.GetSetMethod());
            return (options, value) => fastSet(options, (T)value);
        }

        private Func<CodeFormattingOptions> CreateOptionsFactory(Dictionary<OptionInfo, Action<CodeFormattingOptions, object>> initializers) {
            return () => {
                var res = new CodeFormattingOptions();
                foreach (var keyValue in initializers) {
                    var option = keyValue.Key;
                    var fastSet = keyValue.Value;

                    fastSet(res, option.DeserializeOptionValue(LoadString(option.Key, _formattingCat)));
                }
                return res;
            };
        }

        /// <summary>
        /// Sets the value for a formatting setting.  The name is one of the properties
        /// in CodeFormattingOptions.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetFormattingOption(string name, object value) {
            EnsureAllOptions();
            OptionInfo option;
            if (!_allFormattingOptions.TryGetValue(name, out option)) {
                throw new InvalidOperationException("Unknown option " + name);
            }

            SaveString(name, _formattingCat, option.SerializeOptionValue(value));
        }

        /// <summary>
        /// Gets the value for a formatting setting.  The name is one of the properties in
        /// CodeFormattingOptions.
        /// </summary>
        public object GetFormattingOption(string name) {
            EnsureAllOptions();
            OptionInfo option;
            if (!_allFormattingOptions.TryGetValue(name, out option)) {
                throw new InvalidOperationException("Unknown option " + name);
            }
            return option.DeserializeOptionValue(LoadString(name, _formattingCat));
        }

        private static void EnsureAllOptions() {
            if (_allFormattingOptions.Count == 0) {
                foreach (CodeFormattingCategory curCat in Enum.GetValues(typeof(CodeFormattingCategory))) {
                    if (curCat == CodeFormattingCategory.None) {
                        continue;
                    }

                    var cat = OptionCategory.GetOptions(curCat);
                    foreach (var optionInfo in cat) {
                        _allFormattingOptions[optionInfo.Key] = optionInfo;
                    }
                }
            }
        }

        #endregion

        #region Interactive Options

        internal PythonInteractiveOptions InteractiveOptions => _interactiveOptions.Value;

        /// <summary>
        /// Interactive window backend. If set, it overrides the value in the
        /// mode.txt file. For use by tests, rather than have them modify
        /// mode.txt directly.
        /// </summary>
        internal string InteractiveBackendOverride { get; set; }

        private PythonInteractiveOptions CreateInteractiveOptions(string category) {
            var opts = new PythonInteractiveOptions(this, category);
            opts.Load();
            return opts;
        }

        #endregion

        internal IComponentModel ComponentModel {
            get {
                return (IComponentModel)_container.GetService(typeof(SComponentModel));
            }
        }

        internal System.IServiceProvider Site => _container;

        internal LanguagePreferences LangPrefs => _langPrefs.Value;

        /// <summary>
        /// Ensures the shell is loaded before returning language preferences,
        /// as obtaining them while the shell is initializing can corrupt
        /// settings.
        /// </summary>
        /// <remarks>
        /// Should only be called from the UI thread, and you must not
        /// synchronously wait on the returned task.
        /// </remarks>
        internal async Task<LanguagePreferences> GetLangPrefsAsync() {
            if (_langPrefs.IsValueCreated) {
                return _langPrefs.Value;
            }
            await _container.WaitForShellInitializedAsync();
            return _langPrefs.Value;
        }

        #region Registry Persistance

        internal void DeleteCategory(string category) {
            _optionsService.DeleteCategory(category);
        }

        internal void SaveBool(string name, string category, bool value) {
            SaveString(name, category, value.ToString());
        }

        internal void SaveInt(string name, string category, int value) {
            SaveString(name, category, value.ToString());
        }

        internal void SaveString(string name, string category, string value) {
            _optionsService.SaveString(name, category, value);
        }

        internal string LoadString(string name, string category) {
            return _optionsService.LoadString(name, category);
        }

        internal void SaveEnum<T>(string name, string category, T value) where T : struct {
            SaveString(name, category, value.ToString());
        }

        internal void SaveDateTime(string name, string category, DateTime value) {
            SaveString(name, category, value.ToString(CultureInfo.InvariantCulture));
        }

        internal int? LoadInt(string name, string category) {
            string res = LoadString(name, category);
            if (res == null) {
                return null;
            }

            int val;
            if (int.TryParse(res, out val)) {
                return val;
            }
            return null;
        }

        internal bool? LoadBool(string name, string category) {
            string res = LoadString(name, category);
            if (res == null) {
                return null;
            }

            bool val;
            if (bool.TryParse(res, out val)) {
                return val;
            }
            return null;
        }

        internal T? LoadEnum<T>(string name, string category) where T : struct {
            string res = LoadString(name, category);
            if (res == null) {
                return null;
            }

            T enumRes;
            if (Enum.TryParse<T>(res, out enumRes)) {
                return enumRes;
            }
            return null;
        }

        internal DateTime? LoadDateTime(string name, string category) {
            string res = LoadString(name, category);
            if (res == null) {
                return null;
            }

            DateTime dateRes;
            if (DateTime.TryParse(res, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateRes)) {
                return dateRes;
            }
            return null;
        }

        #endregion

        #region Idle processing

        internal event EventHandler<ComponentManagerEventArgs> OnIdle {
            add {
                lock (_idleManager) {
                    _idleManager.OnIdle += value;
                }
            }
            remove {
                lock (_idleManager) {
                    _idleManager.OnIdle -= value;
                }
            }
        }

        #endregion

        #region Language Preferences

        internal LANGPREFERENCES2 GetLanguagePreferences() {
            var txtMgr = (IVsTextManager2)_container.GetService(typeof(SVsTextManager));
            var langPrefs = new[] { new LANGPREFERENCES2 { guidLang = GuidList.guidPythonLanguageServiceGuid } };
            ErrorHandler.ThrowOnFailure(txtMgr.GetUserPreferences2(null, null, langPrefs, null));
            return langPrefs[0];
        }

        internal void SetLanguagePreferences(LANGPREFERENCES2 langPrefs) {
            var txtMgr = (IVsTextManager2)_container.GetService(typeof(SVsTextManager));
            ErrorHandler.ThrowOnFailure(txtMgr.SetUserPreferences2(null, null, new[] { langPrefs }, null));
        }

        internal IEnumerable<CodeWindowManager> CodeWindowManagers {
            get {
                return _codeWindowManagers.Values;
            }
        }

        internal CodeWindowManager GetOrCreateCodeWindowManager(IVsCodeWindow window) {
            CodeWindowManager value;
            if (!_codeWindowManagers.TryGetValue(window, out value)) {
                _codeWindowManagers[window] = value = new CodeWindowManager(_container, window);

            }
            return value;
        }

        internal void CodeWindowClosed(IVsCodeWindow window) {
            _codeWindowManagers.Remove(window);
        }
        #endregion

        #region Intellisense

        public CompletionAnalysis GetCompletions(ICompletionSession session, ITextView view, ITextSnapshot snapshot, ITrackingSpan span, ITrackingPoint point, CompletionOptions options) {
            return VsProjectAnalyzer.GetCompletions(EditorServices, session, view, snapshot, span, point, options);
        }

        public SignatureAnalysis GetSignatures(ITextView view, ITextSnapshot snapshot, ITrackingSpan span) {
            AnalysisEntry entry;
            if (_entryService == null || !_entryService.TryGetAnalysisEntry(view, snapshot.TextBuffer, out entry)) {
                return new SignatureAnalysis("", 0, new ISignature[0]);
            }
            return entry.Analyzer.WaitForRequest(entry.Analyzer.GetSignaturesAsync(entry, view, snapshot, span), "GetSignatures");
        }

        public Task<SignatureAnalysis> GetSignaturesAsync(ITextView view, ITextSnapshot snapshot, ITrackingSpan span) {
            AnalysisEntry entry;
            if (_entryService == null || !_entryService.TryGetAnalysisEntry(view, snapshot.TextBuffer, out entry)) {
                return Task.FromResult(new SignatureAnalysis("", 0, new ISignature[0]));
            }
            return entry.Analyzer.GetSignaturesAsync(entry, view, snapshot, span);
        }

        public ExpressionAnalysis AnalyzeExpression(ITextView view, ITextSnapshot snapshot, ITrackingSpan span, bool forCompletion = true) {
            AnalysisEntry entry;
            if (_entryService == null || !_entryService.TryGetAnalysisEntry(view, snapshot.TextBuffer, out entry)) {
                return null;
            }
            return entry.Analyzer.WaitForRequest(entry.Analyzer.AnalyzeExpressionAsync(entry, span.GetStartPoint(snapshot)), "AnalyzeExpression");
        }

        public Task<IEnumerable<CompletionResult>> GetExpansionCompletionsAsync() {
            if (_expansionCompletions == null) {
                return Task.FromResult<IEnumerable<CompletionResult>>(null);
            }
            return _expansionCompletions.GetCompletionsAsync();
        }

        #endregion

        public Dictionary<string, string> GetFullEnvironment(LaunchConfiguration config) {
            if (config == null) {
                throw new ArgumentNullException(nameof(config));
            }

            // Start with global environment, add configured environment,
            // then add search paths.
            var baseEnv = Environment.GetEnvironmentVariables();
            // Clear search paths from the global environment. The launch
            // configuration should include the existing value

            var pathVar = config.Interpreter?.PathEnvironmentVariable;
            if (string.IsNullOrEmpty(pathVar)) {
                pathVar = "PYTHONPATH";
            }
            baseEnv[pathVar] = string.Empty;
            var env = PathUtils.MergeEnvironments(
                baseEnv.AsEnumerable<string, string>(),
                config.GetEnvironmentVariables(),
                "Path", pathVar
            );
            if (config.SearchPaths != null && config.SearchPaths.Any()) {
                env = PathUtils.MergeEnvironments(
                    env,
                    new[] {
                        new KeyValuePair<string, string>(
                            pathVar,
                            PathUtils.JoinPathList(config.SearchPaths)
                        )
                    },
                    pathVar
                );
            }
            return env;
        }
    }
}
