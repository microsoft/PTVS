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
using System.Linq;
using System.Reflection;
using System.Threading;
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
using Microsoft.VisualStudio.OLE.Interop;
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
        private LanguagePreferences _langPrefs;
        private IPythonToolsOptionsService _optionsService;
        internal readonly IInterpreterOptionsService _interpreterOptionsService;
        private VsProjectAnalyzer _analyzer;
        private readonly PythonToolsLogger _logger;
        private readonly AdvancedEditorOptions _advancedOptions;
        private readonly DebuggerOptions _debuggerOptions;
        private readonly GeneralOptions _generalOptions;
        private readonly PythonInteractiveCommonOptions _debugInteractiveOptions;
        private readonly GlobalInterpreterOptions _globalInterpreterOptions;
        internal readonly Dictionary<IPythonInterpreterFactory, PythonInteractiveOptions> _interactiveOptions = new Dictionary<IPythonInterpreterFactory, PythonInteractiveOptions>();
        internal readonly Dictionary<IPythonInterpreterFactory, InterpreterOptions> _interpreterOptions = new Dictionary<IPythonInterpreterFactory, InterpreterOptions>();
        private readonly SurveyNewsService _surveyNews;
        private readonly IdleManager _idleManager;
        private Func<CodeFormattingOptions> _optionsFactory;
        private const string _formattingCat = "Formatting";
        private uint _langPrefsTextManagerCookie;

        private readonly Dictionary<IVsCodeWindow, CodeWindowManager> _codeWindowManagers = new Dictionary<IVsCodeWindow, CodeWindowManager>();

        private readonly object _suppressEnvironmentsLock = new object();
        private int _suppressEnvironmentsChanged;
        private bool _environmentsChangedWasSuppressed;

        private static readonly Dictionary<string, OptionInfo> _allFormattingOptions = new Dictionary<string, OptionInfo>();

        private string _defaultInterpreter;

        private const string DefaultInterpreterOptionsCollection = @"SOFTWARE\\Microsoft\\PythonTools\\Interpreters";

        private const string DefaultInterpreterSetting = "DefaultInterpreterId";
        private readonly IEnumerable<Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>> _factoryProviders;

        internal PythonToolsService(IServiceContainer container) {
            _container = container;

            LoadDefaultInterpreter(suppressChangeEvent: true);

            var langService = new PythonLanguageInfo(container);
            _container.AddService(langService.GetType(), langService, true);

            IVsTextManager textMgr = (IVsTextManager)container.GetService(typeof(SVsTextManager));
            if (textMgr != null) {
                var langPrefs = new LANGPREFERENCES[1];
                langPrefs[0].guidLang = typeof(PythonLanguageInfo).GUID;
                ErrorHandler.ThrowOnFailure(textMgr.GetUserPreferences(null, null, langPrefs, null));
                _langPrefs = new LanguagePreferences(this, langPrefs[0]);

                Guid guid = typeof(IVsTextManagerEvents2).GUID;
                IConnectionPoint connectionPoint;
                ((IConnectionPointContainer)textMgr).FindConnectionPoint(ref guid, out connectionPoint);
                connectionPoint.Advise(_langPrefs, out _langPrefsTextManagerCookie);
            }

            _optionsService = (IPythonToolsOptionsService)container.GetService(typeof(IPythonToolsOptionsService));
            var compModel = (IComponentModel)container.GetService(typeof(SComponentModel));
            _interpreterOptionsService = compModel.GetService<IInterpreterOptionsService>();
            if (_interpreterOptionsService != null) {   // not available in some test cases...
                _interpreterOptionsService.InterpretersChanged += InterpretersChanged;
                _interpreterOptionsService.DefaultInterpreterChanged += UpdateDefaultAnalyzer;
                LoadInterpreterOptions();
            }

            _idleManager = new IdleManager(container);
            _advancedOptions = new AdvancedEditorOptions(this);
            _debuggerOptions = new DebuggerOptions(this);
            _generalOptions = new GeneralOptions(this);
            _surveyNews = new SurveyNewsService(container);
            _globalInterpreterOptions = new GlobalInterpreterOptions(this, _interpreterOptionsService);
            _globalInterpreterOptions.Load();
            _debugInteractiveOptions = new PythonInteractiveCommonOptions(this, "Debug Interactive Window", "");
            _factoryProviders = ComponentModel.DefaultExportProvider.GetExports<IPythonInterpreterFactoryProvider, Dictionary<string, object>>();
            _logger = new PythonToolsLogger(ComponentModel.GetExtensions<IPythonToolsLogger>().ToArray());
            InitializeLogging();
        }

        void IDisposable.Dispose() {
            // This will probably never be called by VS, but we use it in unit
            // tests to avoid leaking memory when we reinitialize state between
            // each test.

            IDisposable disposable;

            if (_langPrefs != null && _langPrefsTextManagerCookie != 0) {
                IVsTextManager textMgr = (IVsTextManager)_container.GetService(typeof(SVsTextManager));
                if (textMgr != null) {
                    Guid guid = typeof(IVsTextManagerEvents2).GUID;
                    IConnectionPoint connectionPoint;
                    ((IConnectionPointContainer)textMgr).FindConnectionPoint(ref guid, out connectionPoint);
                    connectionPoint.Unadvise(_langPrefsTextManagerCookie);
                }
            }
            
            if (_interpreterOptionsService != null) {
                _interpreterOptionsService.InterpretersChanged -= InterpretersChanged;
                _interpreterOptionsService.DefaultInterpreterChanged -= UpdateDefaultAnalyzer;
            }

            if ((disposable = _interpreterOptionsService as IDisposable) != null) {
                disposable.Dispose();
            }

            _idleManager.Dispose();

            foreach (var window in _codeWindowManagers.Values) {
                window.RemoveAdornments();
            }
            _codeWindowManagers.Clear();
        }

        private void InitializeLogging() {
            if (_interpreterOptionsService != null) { // not available in some test cases...
                // log interesting stats on startup
                var installed = _interpreterOptionsService.KnownProviders
                    //.Where(x => !(x is ConfigurablePythonInterpreterFactoryProvider) &&
                    //            !(x is LoadedProjectInterpreterFactoryProvider))
                    .SelectMany(x => x.GetInterpreterConfigurations())
                    .Count();

                var configured = _interpreterOptionsService.KnownProviders.
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
                if (_analyzer != null) {
                    var analyzer = CreateAnalyzer();

                    if (_analyzer != null) {
                        analyzer.SwitchAnalyzers(_analyzer);
                    }
                    _analyzer = analyzer;
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
                defaultFactory.CreateInterpreter(),
                defaultFactory,
                _interpreterOptionsService.Interpreters.ToArray()
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

        #region Default interpreter


        private void SaveDefaultInterpreter() {
            using (var interpreterOptions = Registry.CurrentUser.CreateSubKey(DefaultInterpreterOptionsCollection, true)) {
                if (_defaultInterpreter == null) {
                    interpreterOptions.SetValue(DefaultInterpreterSetting, "");
                } else {
                    interpreterOptions.SetValue(DefaultInterpreterSetting, _defaultInterpreter);
                }
            }
        }

        private void LoadDefaultInterpreter(bool suppressChangeEvent = false) {
            string newDefault = string.Empty;

            using (var interpreterOptions = Registry.CurrentUser.OpenSubKey(DefaultInterpreterOptionsCollection)) {
                if (interpreterOptions != null) {
                    newDefault = interpreterOptions.GetValue(DefaultInterpreterSetting) as string ?? string.Empty;
                }

                if (suppressChangeEvent) {
                    _defaultInterpreter = newDefault;
                } else {
                    DefaultInterpreterId = newDefault;
                }
            }
        }

        private void InitializeDefaultInterpreterWatcher() {
            RegistryHive hive = RegistryHive.CurrentUser;
            RegistryView view = RegistryView.Default;
            if (RegistryWatcher.Instance.TryAdd(
                hive, view, DefaultInterpreterOptionsCollection,
                DefaultInterpreterRegistry_Changed,
                recursive: false, notifyValueChange: true, notifyKeyChange: false
            ) == null) {
                // DefaultInterpreterOptions subkey does not exist yet, so
                // create it and then start the watcher.
                SaveDefaultInterpreter();

                RegistryWatcher.Instance.Add(
                    hive, view, DefaultInterpreterOptionsCollection,
                    DefaultInterpreterRegistry_Changed,
                    recursive: false, notifyValueChange: true, notifyKeyChange: false
                );
            }
        }

        private void DefaultInterpreterRegistry_Changed(object sender, RegistryChangedEventArgs e) {
            try {
                LoadDefaultInterpreter();
            } catch (Exception ex) {
                try {
                    //ActivityLog.LogError(
                    //    "Python Tools for Visual Studio",
                    //    string.Format("Exception updating default interpreter: {0}", ex)
                    //);
                } catch (InvalidOperationException) {
                    // Can't get the activity log service either. This probably
                    // means we're being used from outside of VS, but also
                    // occurs during some unit tests. We want to debug this if
                    // possible, but generally avoid crashing.
                    Debug.Fail(ex.ToString());
                }
            }
        }

        #endregion

        #region Public API


        public string DefaultInterpreterId {
            get {
                return _defaultInterpreter;
            }
            set {
                if (_defaultInterpreter != value) {
                    _defaultInterpreter = value;
                    DefaultInterpreterChanged?.Invoke(this, EventArgs.Empty);
                }

            }
        }

        public IPythonInterpreterFactory DefaultInterpreter {
            get {
                return _factoryProviders.GetInterpreterFactory(DefaultInterpreterId);
            }
        }

        public event EventHandler DefaultInterpreterChanged;
        
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

        internal PythonInteractiveCommonOptions DebugInteractiveOptions {
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
                var placeholders = InterpreterOptions.Where(kv => kv.Key is InterpreterPlaceholder).ToArray();
                ClearInterpreterOptions();
                foreach (var interpreter in _interpreterOptionsService.Interpreters) {
                    GetInterpreterOptions(interpreter);
                }

                foreach (var kv in placeholders) {
                    AddInterpreterOptions(kv.Key, kv.Value);
                }
            } finally {
                EndSuppressRaiseEnvironmentsChanged();
            }
        }

        internal void SaveInterpreterOptions() {
            _interpreterOptionsService.BeginSuppressInterpretersChangedEvent();
            try {
                // Remove any items
                foreach (var option in InterpreterOptions.Select(kv => kv.Value).Where(o => o.Removed).ToList()) {
                    _interpreterOptionsService.RemoveConfigurableInterpreter(option.Factory.Configuration.Id);
                    RemoveInteractiveOptions(option.Factory);
                    RemoveInterpreterOptions(option.Factory);
                }

                // Add or update any items that weren't removed
                foreach (var option in InterpreterOptions.Select(x => x.Value)) {
                    if (option.Added) {
                        if (option.Id == Guid.Empty) {
                            option.Id = Guid.NewGuid();
                        }
                        option.Added = false;
                    }

                    if (option.IsConfigurable) {
                        // save configurable interpreter options
                        var actualFactory = _interpreterOptionsService.AddConfigurableInterpreter(
                            new InterpreterFactoryCreationOptions {
                                Id = option.Id,
                                InterpreterPath = option.InterpreterPath ?? "",
                                WindowInterpreterPath = option.WindowsInterpreterPath ?? "",
                                LibraryPath = option.LibraryPath ?? "",
                                PathEnvironmentVariableName = option.PathEnvironmentVariable ?? "",
                                ArchitectureString = option.Architecture ?? "x86",
                                LanguageVersionString = option.Version ?? "2.7",
                                Description = option.Display,
                            }
                        );
                        if (option.InteractiveOptions != null) {
                            option.InteractiveOptions._id = actualFactory;
                            option.InteractiveOptions.Save(actualFactory);
                        }
                    }
                }


                foreach (var factory in InterpreterOptions.Select(x => x.Key).OfType<InterpreterPlaceholder>().ToArray()) {
                    RemoveInterpreterOptions(factory);
                }
            } finally {
                _interpreterOptionsService.EndSuppressInterpretersChangedEvent();
            }
        }

        private void InterpretersChanged(object sender, EventArgs e) {
            GlobalInterpreterOptions.Load();
            LoadInterpreterOptions();
        }

        internal InterpreterOptions GetInterpreterOptions(IPythonInterpreterFactory interpreterFactory) {
            InterpreterOptions options;
            if (!_interpreterOptions.TryGetValue(interpreterFactory, out options)) {
                var path = GetInteractivePath(interpreterFactory);
                _interpreterOptions[interpreterFactory] = options = new InterpreterOptions(this, interpreterFactory);
                options.Load();
                RaiseEnvironmentsChanged();
            }
            return options;
        }

        internal bool TryGetInterpreterOptions(IPythonInterpreterFactory factory, out InterpreterOptions options) {
            return _interpreterOptions.TryGetValue(factory, out options);
        }

        private void ClearInterpreterOptions() {
            _interpreterOptions.Clear();
            RaiseEnvironmentsChanged();
        }

        internal void AddInterpreterOptions(IPythonInterpreterFactory interpreterFactory, InterpreterOptions options, bool addInteractive = false) {
            _interpreterOptions[interpreterFactory] = options;
            if (addInteractive) {
                _interactiveOptions[interpreterFactory] = options.InteractiveOptions;
            }
            RaiseEnvironmentsChanged();
        }

        internal IEnumerable<KeyValuePair<IPythonInterpreterFactory, InterpreterOptions>> InterpreterOptions {
            get {
                return _interpreterOptions;
            }
        }

        internal void RemoveInterpreterOptions(IPythonInterpreterFactory interpreterFactory) {
            _interpreterOptions.Remove(interpreterFactory);
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

        internal PythonInteractiveOptions GetInteractiveOptions(IPythonInterpreterFactory interpreterFactory) {
            PythonInteractiveOptions options;
            if (!_interactiveOptions.TryGetValue(interpreterFactory, out options)) {
                var path = GetInteractivePath(interpreterFactory);
                _interactiveOptions[interpreterFactory] = options = new PythonInteractiveOptions(_container, this, "Interactive Windows", path);
                options.Load();
            }
            return options;
        }

        internal IEnumerable<KeyValuePair<IPythonInterpreterFactory, PythonInteractiveOptions>> InteractiveOptions {
            get {
                return _interactiveOptions;
            }
        }

        internal void AddInteractiveOptions(IPythonInterpreterFactory interpreterFactory, PythonInteractiveOptions options) {
            _interactiveOptions[interpreterFactory] = options;
        }

        internal void RemoveInteractiveOptions(IPythonInterpreterFactory interpreterFactory) {
            _interactiveOptions.Remove(interpreterFactory);
        }

        internal void ClearInteractiveOptions() {
            _interactiveOptions.Clear();
        }

        /// <summary>
        /// Gets a path which is unique for this interpreter (based upon the Id and version).
        /// </summary>
        internal static string GetInteractivePath(IPythonInterpreterFactory interpreterFactory) {
            return interpreterFactory.Id.ToString("B") + "\\" + interpreterFactory.Configuration.Version + "\\";
        }

        #endregion

        internal IComponentModel ComponentModel {
            get {
                return (IComponentModel)_container.GetService(typeof(SComponentModel));
            }
        }

        internal LanguagePreferences LangPrefs {
            get {
                return _langPrefs;
            }
        }


        #region Registry Persistance

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
            ErrorHandler.ThrowOnFailure(txtMgr.SetUserPreferences2(null, null, new [] { langPrefs }, null));
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

        #endregion
    }
}
