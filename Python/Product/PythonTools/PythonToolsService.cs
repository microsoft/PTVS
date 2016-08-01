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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
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
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.Win32;

namespace Microsoft.PythonTools {

    /// <summary>
    /// Provides services and state which need to be available to various PTVS components.
    /// </summary>
    public sealed class PythonToolsService : IDisposable {
        private readonly IServiceContainer _container;
        private readonly Lazy<LanguagePreferences> _langPrefs;
        private IPythonToolsOptionsService _optionsService;
        internal readonly IInterpreterRegistryService _interpreterRegistry;
        internal readonly IInterpreterOptionsService _interpreterOptionsService;
        private VsProjectAnalyzer _analyzer;
        private readonly PythonToolsLogger _logger;
        private readonly AdvancedEditorOptions _advancedOptions;
        private readonly DebuggerOptions _debuggerOptions;
        private readonly GeneralOptions _generalOptions;
        private readonly PythonInteractiveOptions _debugInteractiveOptions;
        private readonly GlobalInterpreterOptions _globalInterpreterOptions;
        private readonly PythonInteractiveOptions _interactiveOptions;
        internal readonly Dictionary<string, InterpreterOptions> _interpreterOptions = new Dictionary<string, InterpreterOptions>();
        private readonly SuppressDialogOptions _suppressDialogOptions;
        private readonly SurveyNewsService _surveyNews;
        private readonly IdleManager _idleManager;
        private Func<CodeFormattingOptions> _optionsFactory;
        private const string _formattingCat = "Formatting";

        private readonly Dictionary<IVsCodeWindow, CodeWindowManager> _codeWindowManagers = new Dictionary<IVsCodeWindow, CodeWindowManager>();

        private readonly object _suppressEnvironmentsLock = new object();
        private int _suppressEnvironmentsChanged;
        private bool _environmentsChangedWasSuppressed;

        private static readonly Dictionary<string, OptionInfo> _allFormattingOptions = new Dictionary<string, OptionInfo>();

        private const string DefaultInterpreterOptionsCollection = @"SOFTWARE\\Microsoft\\PythonTools\\Interpreters";

        private const string DefaultInterpreterSetting = "DefaultInterpreterId";
        private readonly IEnumerable<Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>> _factoryProviders;

        internal PythonToolsService(IServiceContainer container) {
            _container = container;

            var langService = new PythonLanguageInfo(container);
            _container.AddService(langService.GetType(), langService, true);

            _langPrefs = new Lazy<LanguagePreferences>(() => new LanguagePreferences(this, typeof(PythonLanguageInfo).GUID));

            _optionsService = (IPythonToolsOptionsService)container.GetService(typeof(IPythonToolsOptionsService));
            var compModel = (IComponentModel)container.GetService(typeof(SComponentModel));
            _interpreterRegistry = compModel.GetService<IInterpreterRegistryService>();
            if (_interpreterRegistry != null) {
                _interpreterRegistry.InterpretersChanged += InterpretersChanged;
            }

            _interpreterOptionsService = compModel.GetService<IInterpreterOptionsService>();
            if (_interpreterOptionsService != null) {   // not available in some test cases...
                _interpreterOptionsService.DefaultInterpreterChanged += UpdateDefaultAnalyzer;
                LoadInterpreterOptions();
            }

            _idleManager = new IdleManager(container);
            _advancedOptions = new AdvancedEditorOptions(this);
            _debuggerOptions = new DebuggerOptions(this);
            _generalOptions = new GeneralOptions(this);
            _surveyNews = new SurveyNewsService(container);
            _suppressDialogOptions = new SuppressDialogOptions(this);
            _globalInterpreterOptions = new GlobalInterpreterOptions(this, _interpreterOptionsService, _interpreterRegistry);
            _globalInterpreterOptions.Load();
            _interactiveOptions = new PythonInteractiveOptions(this, "Interactive");
            _interactiveOptions.Load();
            _debugInteractiveOptions = new PythonInteractiveOptions(this, "Debug Interactive Window");
            _debuggerOptions.Load();
            _factoryProviders = ComponentModel.DefaultExportProvider.GetExports<IPythonInterpreterFactoryProvider, Dictionary<string, object>>();
            _logger = new PythonToolsLogger(ComponentModel.GetExtensions<IPythonToolsLogger>().ToArray());
            InitializeLogging();
        }

        public void Dispose() {
            // This will probably never be called by VS, but we use it in unit
            // tests to avoid leaking memory when we reinitialize state between
            // each test.

            IDisposable disposable;

            if (_langPrefs.IsValueCreated) {
                _langPrefs.Value.Dispose();
            }

            if (_interpreterRegistry != null) {
                _interpreterRegistry.InterpretersChanged -= InterpretersChanged;
            }

            if (_interpreterOptionsService != null) {
                _interpreterOptionsService.DefaultInterpreterChanged -= UpdateDefaultAnalyzer;
            }

            if ((disposable = _interpreterOptionsService as IDisposable) != null) {
                disposable.Dispose();
            }

            _idleManager.Dispose();

            foreach (var window in _codeWindowManagers.Values.ToArray()) {
                window.RemoveAdornments();
            }
            _codeWindowManagers.Clear();
        }

        private void InitializeLogging() {
            if (_interpreterOptionsService != null) { // not available in some test cases...
                                                      // log interesting stats on startup
                var knownProviders = ComponentModel.GetExtensions<IPythonInterpreterFactoryProvider>();

                var installed = knownProviders
                    //.Where(x => !(x is ConfigurablePythonInterpreterFactoryProvider) &&
                    //            !(x is LoadedProjectInterpreterFactoryProvider))
                    .SelectMany(x => x.GetInterpreterConfigurations())
                    .Count();

                var configured = knownProviders.
                    SelectMany(x => x.GetInterpreterConfigurations()).
                    Select(x => x.Id).
                    Where(x => _interpreterOptionsService.IsConfigurable(x)).
                    Count();

                _logger.LogEvent(PythonLogEvent.InstalledInterpreters, installed);
                _logger.LogEvent(PythonLogEvent.ConfiguredInterpreters, configured);
            }

            _logger.LogEvent(PythonLogEvent.SurveyNewsFrequency, GeneralOptions.SurveyNewsCheck);
        }

        private void UpdateDefaultAnalyzer(object sender, EventArgs args) {
            // no need to update if analyzer isn't created yet.
            if (_analyzer == null) {
                return;
            }

            _container.GetUIThread().InvokeAsync(() => {
                var analyzer = CreateAnalyzer();
                var oldAnalyzer = Interlocked.Exchange(ref _analyzer, analyzer);
                if (oldAnalyzer != null) {
                    analyzer.SwitchAnalyzers(oldAnalyzer);
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

        private VsProjectAnalyzer CreateAnalyzer() {
            var defaultFactory = _interpreterOptionsService.DefaultInterpreter;
            EnsureCompletionDb(defaultFactory);
            return new VsProjectAnalyzer(
                _container,
                defaultFactory
            );
        }

        internal PythonToolsLogger Logger {
            get {
                return _logger;
            }
        }

        internal SurveyNewsService SurveyNews {
            get {
                return _surveyNews;
            }
        }

        #region Public API

        public InterpreterConfiguration DefaultInterpreterConfiguration {
            get {
                return _interpreterOptionsService.DefaultInterpreter.Configuration;
            }
        }

        public VsProjectAnalyzer DefaultAnalyzer {
            get {
                if (_analyzer == null) {
                    _analyzer = _container.GetUIThread().Invoke(() => CreateAnalyzer());
                }
                return _analyzer;
            }
        }

        public AdvancedEditorOptions AdvancedOptions {
            get {
                return _advancedOptions;
            }
        }

        public DebuggerOptions DebuggerOptions {
            get {
                return _debuggerOptions;
            }
        }

        public GeneralOptions GeneralOptions {
            get {
                return _generalOptions;
            }
        }

        internal PythonInteractiveOptions DebugInteractiveOptions {
            get {
                return _debugInteractiveOptions;
            }
        }

        public GlobalInterpreterOptions GlobalInterpreterOptions {
            get {
                return _globalInterpreterOptions;
            }
        }

        #endregion

        internal SuppressDialogOptions SuppressDialogOptions {
            get {
                return _suppressDialogOptions;
            }
        }

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

        #region Interpreter Options

        internal void LoadInterpreterOptions() {
            BeginSuppressRaiseEnvironmentsChanged();
            try {
                var placeholders = InterpreterOptions.Where(kv => kv.Key.StartsWith("Placeholder;")).ToArray();
                ClearInterpreterOptions();
                foreach (var interpreter in _interpreterRegistry.Configurations) {
                    GetInterpreterOptions(interpreter.Id);
                }

                foreach (var kv in placeholders) {
                    AddInterpreterOptions(kv.Key, kv.Value);
                }
            } finally {
                EndSuppressRaiseEnvironmentsChanged();
            }
        }

        internal void SaveInterpreterOptions() {
            _interpreterRegistry.BeginSuppressInterpretersChangedEvent();
            try {
                _interpreterOptionsService.DefaultInterpreterId = GlobalInterpreterOptions.DefaultInterpreter;
                // Remove any items
                foreach (var option in InterpreterOptions.Select(kv => kv.Value).Where(o => o.Removed).ToList()) {
                    _interpreterOptionsService.RemoveConfigurableInterpreter(option._config.Id);
                    RemoveInterpreterOptions(option._config.Id);
                }

                // Add or update any items that weren't removed
                foreach (var option in InterpreterOptions.Select(x => x.Value)) {
                    if (option.Added) {
                        if (String.IsNullOrWhiteSpace(option.Id)) {
                            option.Id = Guid.NewGuid().ToString();
                        }
                        option.Added = false;
                    }

                    if (option.IsConfigurable) {
                        // save configurable interpreter options
                        var actualFactory = _interpreterOptionsService.AddConfigurableInterpreter(
                            option.Description,
                            new InterpreterConfiguration(
                                option.Id,
                                option.Description,
                                !String.IsNullOrWhiteSpace(option.InterpreterPath) ? PathUtils.GetParent(option.InterpreterPath) : "",
                                option.InterpreterPath ?? "",
                                option.WindowsInterpreterPath ?? "",
                                option.PathEnvironmentVariable ?? "",
                                InterpreterArchitecture.TryParse(option.Architecture),
                                Version.Parse(option.Version) ?? new Version(2, 7)
                            )
                        );
                    }
                }


                foreach (var factory in InterpreterOptions.Where(x => x.Value.Id.StartsWith("Placeholder;")).ToArray()) {
                    RemoveInterpreterOptions(factory.Value.Id);
                }
            } finally {
                _interpreterRegistry.EndSuppressInterpretersChangedEvent();
            }
        }

        private void InterpretersChanged(object sender, EventArgs e) {
            GlobalInterpreterOptions.Load();
            LoadInterpreterOptions();
        }

        internal InterpreterOptions GetInterpreterOptions(string id) {
            InterpreterOptions options;
            if (!_interpreterOptions.TryGetValue(id, out options)) {
                _interpreterOptions[id] = options = new InterpreterOptions(this, _interpreterRegistry.FindConfiguration(id));
                options.Load();
                RaiseEnvironmentsChanged();
            }
            return options;
        }

        internal bool TryGetInterpreterOptions(string id, out InterpreterOptions options) {
            return _interpreterOptions.TryGetValue(id, out options);
        }

        private void ClearInterpreterOptions() {
            _interpreterOptions.Clear();
            RaiseEnvironmentsChanged();
        }

        internal void AddInterpreterOptions(string id, InterpreterOptions options) {
            _interpreterOptions[id] = options;
            RaiseEnvironmentsChanged();
        }

        internal IEnumerable<KeyValuePair<string, InterpreterOptions>> InterpreterOptions {
            get {
                return _interpreterOptions;
            }
        }

        internal void RemoveInterpreterOptions(string id) {
            _interpreterOptions.Remove(id);
            RaiseEnvironmentsChanged();
        }

        private void BeginSuppressRaiseEnvironmentsChanged() {
            lock (_suppressEnvironmentsLock) {
                _suppressEnvironmentsChanged += 1;
            }
        }

        private void EndSuppressRaiseEnvironmentsChanged() {
            bool raiseEvent = false;
            lock (_suppressEnvironmentsLock) {
                if (--_suppressEnvironmentsChanged == 0) {
                    raiseEvent = _environmentsChangedWasSuppressed;
                    _environmentsChangedWasSuppressed = false;
                }
            }

            if (raiseEvent) {
                RaiseEnvironmentsChanged();
            }
        }

        private void RaiseEnvironmentsChanged() {
            lock (_suppressEnvironmentsLock) {
                if (_suppressEnvironmentsChanged > 0) {
                    _environmentsChangedWasSuppressed = true;
                    return;
                }
            }
            var changed = EnvironmentsChanged;
            if (changed != null) {
                changed(this, EventArgs.Empty);
            }
        }

        internal event EventHandler EnvironmentsChanged;

        /// <summary>
        /// Gets a path for the interpreter setting.  This is different from interactive
        /// path for backwards compatibility of stored settings w/ 2.1 and earlier.
        /// </summary>
        private static string GetInterpreterSettingPath(Guid id, Version version) {
            return id.ToString() + "\\" + version.ToString() + "\\";
        }

        #endregion

        #region Interactive Options

        internal PythonInteractiveOptions InteractiveOptions => _interactiveOptions;

        /// <summary>
        /// Gets a path which is unique for this interpreter (based upon the Id and version).
        /// </summary>
        internal static string GetInteractivePath(InterpreterConfiguration config) {
            return config.Id;
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
                _idleManager.OnIdle += value;
            }
            remove {
                _idleManager.OnIdle -= value;
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
            return VsProjectAnalyzer.GetCompletions(_container, session, view, snapshot, span, point, options);
        }

        public SignatureAnalysis GetSignatures(ITextView view, ITextSnapshot snapshot, ITrackingSpan span) {
            return VsProjectAnalyzer.GetSignaturesAsync(_container, view, snapshot, span).WaitOrDefault(1000);
        }

        public Task<SignatureAnalysis> GetSignaturesAsync(ITextView view, ITextSnapshot snapshot, ITrackingSpan span) {
            return VsProjectAnalyzer.GetSignaturesAsync(_container, view, snapshot, span);
        }

        public ExpressionAnalysis AnalyzeExpression(ITextView view, ITextSnapshot snapshot, ITrackingSpan span, bool forCompletion = true) {
            return VsProjectAnalyzer.AnalyzeExpressionAsync(_container, view, span.GetStartPoint(snapshot)).WaitOrDefault(1000);
        }

        #endregion

        public Dictionary<string, string> GetFullEnvironment(LaunchConfiguration config) {
            // Start with global environment, add configured environment,
            // then add search paths.
            var baseEnv = Environment.GetEnvironmentVariables();
            if (GeneralOptions.ClearGlobalPythonPath) {
                // Clear search paths from the global environment
                baseEnv[config.Interpreter.PathEnvironmentVariable] = string.Empty;
            }
            var env = PathUtils.MergeEnvironments(
                baseEnv.AsEnumerable<string, string>(),
                config.GetEnvironmentVariables(),
                "Path", config.Interpreter.PathEnvironmentVariable
            );
            if (config.SearchPaths != null && config.SearchPaths.Any()) {
                env = PathUtils.MergeEnvironments(
                    env,
                    new[] {
                        new KeyValuePair<string, string>(
                            config.Interpreter.PathEnvironmentVariable,
                            PathUtils.JoinPathList(config.SearchPaths)
                        )
                    },
                    config.Interpreter.PathEnvironmentVariable
                );
            }
            return env;
        }


    }
}
