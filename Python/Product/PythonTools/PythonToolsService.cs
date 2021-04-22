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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Environments;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
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
        private Lazy<IInterpreterRegistryService> _interpreterRegistryService;
        private readonly Lazy<PythonFormattingOptions> _formattingOptions;
        private readonly Lazy<PythonAdvancedEditorOptions> _advancedEditorOptions;
        private readonly Lazy<PythonDebuggingOptions> _debuggerOptions;
        private readonly Lazy<PythonCondaOptions> _condaOptions;
        private readonly Lazy<PythonAnalysisOptions> _analysisOptions;
        private readonly Lazy<PythonGeneralOptions> _generalOptions;
        private readonly Lazy<PythonInteractiveOptions> _debugInteractiveOptions;
        private readonly Lazy<PythonInteractiveOptions> _interactiveOptions;
        private readonly Lazy<SuppressDialogOptions> _suppressDialogOptions;
        private readonly IdleManager _idleManager;
        private readonly DiagnosticsProvider _diagnosticsProvider;

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

            _langPrefs = new Lazy<LanguagePreferences>(() => new LanguagePreferences(Site, typeof(PythonLanguageInfo).GUID));
            _interpreterOptionsService = new Lazy<IInterpreterOptionsService>(Site.GetComponentModel().GetService<IInterpreterOptionsService>);
            _interpreterRegistryService = new Lazy<IInterpreterRegistryService>(Site.GetComponentModel().GetService<IInterpreterRegistryService>);

            _optionsService = (IPythonToolsOptionsService)container.GetService(typeof(IPythonToolsOptionsService));

            _idleManager = new IdleManager(container);
            _formattingOptions = new Lazy<PythonFormattingOptions>(CreateFormattingOptions);
            _advancedEditorOptions = new Lazy<PythonAdvancedEditorOptions>(CreateAdvancedEditorOptions);
            _debuggerOptions = new Lazy<PythonDebuggingOptions>(CreateDebuggerOptions);
            _condaOptions = new Lazy<PythonCondaOptions>(CreateCondaOptions);
            _analysisOptions = new Lazy<PythonAnalysisOptions>(CreateAnalysisOptions);
            _generalOptions = new Lazy<PythonGeneralOptions>(CreateGeneralOptions);
            _suppressDialogOptions = new Lazy<SuppressDialogOptions>(() => new SuppressDialogOptions(this));
            _interactiveOptions = new Lazy<PythonInteractiveOptions>(() => CreateInteractiveOptions("Interactive"));
            _debugInteractiveOptions = new Lazy<PythonInteractiveOptions>(() => CreateInteractiveOptions("Debug Interactive Window"));
            _diagnosticsProvider = new DiagnosticsProvider(container);
            Logger = (IPythonToolsLogger)container.GetService(typeof(IPythonToolsLogger));
            EnvironmentSwitcherManager = new EnvironmentSwitcherManager(container);
            WorkspaceInfoBarManager = new WorkspaceInfoBarManager(container);

            _idleManager.OnIdle += OnIdleInitialization;
        }

        private void OnIdleInitialization(object sender, ComponentManagerEventArgs e) {
            Site.AssertShellIsInitialized();

            _idleManager.OnIdle -= OnIdleInitialization;

            InitializeLogging();
            EnvironmentSwitcherManager.Initialize();
        }

        public void Dispose() {
            if (_langPrefs.IsValueCreated) {
                _langPrefs.Value.Dispose();
            }

            _idleManager.Dispose();

            EnvironmentSwitcherManager.Dispose();
            WorkspaceInfoBarManager.Dispose();
        }

        private void InitializeLogging() {
            try {
                var registry = ComponentModel.GetService<IInterpreterRegistryService>();
                if (registry != null) {
                    // not available in some test cases...
                    // log interesting stats on startup
                    var installed = registry.Configurations.Count();
                    var installedV2 = registry.Configurations.Count(c => c.Version.Major == 2);
                    var installedV3 = registry.Configurations.Count(c => c.Version.Major == 3);

                    Logger.LogEvent(PythonLogEvent.InstalledInterpreters, new Dictionary<string, object> {
                        { "Total", installed },
                        { "3x", installedV3 },
                        { "2x", installedV2 },
                    });
                }
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }
        }

        internal void GetDiagnosticsLog(TextWriter writer, bool includeAnalysisLogs) {
            _diagnosticsProvider.WriteLog(writer, includeAnalysisLogs);
        }

        internal IInterpreterOptionsService InterpreterOptionsService => _interpreterOptionsService.Value;
        internal IInterpreterRegistryService InterpreterRegistryService => _interpreterRegistryService.Value;

        internal IPythonToolsLogger Logger { get; }

        internal EnvironmentSwitcherManager EnvironmentSwitcherManager { get; }

        internal WorkspaceInfoBarManager WorkspaceInfoBarManager { get; }

        internal LanguageServerClient.PythonLanguageClient LanguageClient { get; set; }

        #region Public API

        public PythonFormattingOptions FormattingOptions => _formattingOptions.Value;
        public PythonAdvancedEditorOptions AdvancedEditorOptions => _advancedEditorOptions.Value;
        public PythonDebuggingOptions DebuggerOptions => _debuggerOptions.Value;
        public PythonCondaOptions CondaOptions => _condaOptions.Value;
        public PythonAnalysisOptions AnalysisOptions => _analysisOptions.Value;
        public PythonGeneralOptions GeneralOptions => _generalOptions.Value;
        internal PythonInteractiveOptions DebugInteractiveOptions => _debugInteractiveOptions.Value;

        private PythonFormattingOptions CreateFormattingOptions() {
            var opts = new PythonFormattingOptions(this);
            opts.Load();
            return opts;
        }

        private PythonAdvancedEditorOptions CreateAdvancedEditorOptions() {
            var opts = new PythonAdvancedEditorOptions(this);
            opts.Load();
            return opts;
        }

        private PythonDebuggingOptions CreateDebuggerOptions() {
            var opts = new PythonDebuggingOptions(this);
            opts.Load();
            return opts;
        }

        private PythonCondaOptions CreateCondaOptions() {
            var opts = new PythonCondaOptions(this);
            opts.Load();
            return opts;
        }

        private PythonAnalysisOptions CreateAnalysisOptions() {
            var opts = new PythonAnalysisOptions(this);
            opts.Load();
            return opts;
        }

        private PythonGeneralOptions CreateGeneralOptions() {
            var opts = new PythonGeneralOptions(this);
            opts.Load();
            return opts;
        }

        #endregion

        internal SuppressDialogOptions SuppressDialogOptions => _suppressDialogOptions.Value;

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

        internal void DeleteCategory(string category)
            => _optionsService.DeleteCategory(category);

        internal bool SaveBool(string name, string category, bool value)
            => SaveString(name, category, value.ToString());

        internal bool SaveInt(string name, string category, int value)
            => SaveString(name, category, value.ToString());

        internal bool SaveString(string name, string category, string value) {
            if (LoadString(name, category) != value) {
                _optionsService.SaveString(name, category, value);
                return true;
            }
            return false;
        }

        internal bool SaveMultilineString(string name, string category, string[] values) {
            values = values ?? Array.Empty<string>();
            if (!Enumerable.SequenceEqual(LoadMultilineString(name, category), values)) {
                _optionsService.SaveString(name, category, string.Join("\n", values));
                return true;
            }
            return false;
        }


        internal string LoadString(string name, string category) => _optionsService.LoadString(name, category);

        internal string[] LoadMultilineString(string name, string category) {
            return _optionsService.LoadString(name, category)?
                .Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        }

        internal void SaveEnum<T>(string name, string category, T value) where T : struct
            => SaveString(name, category, value.ToString());

        internal void SaveDateTime(string name, string category, DateTime value)
            => SaveString(name, category, value.ToString(CultureInfo.InvariantCulture));

        internal int? LoadInt(string name, string category) {
            var res = LoadString(name, category);
            if (res == null) {
                return null;
            }
            return int.TryParse(res, out var val) ? val : (int?)null;
        }

        internal bool? LoadBool(string name, string category) {
            var res = LoadString(name, category);
            if (res == null) {
                return null;
            }

            return bool.TryParse(res, out var val) ? val : (bool?)null;
        }

        internal T? LoadEnum<T>(string name, string category) where T : struct {
            var res = LoadString(name, category);
            if (res == null) {
                return null;
            }

            return Enum.TryParse<T>(res, out var enumRes) ? (T?)enumRes : null;
        }

        internal DateTime? LoadDateTime(string name, string category) {
            var res = LoadString(name, category);
            if (res == null) {
                return null;
            }

            return DateTime.TryParse(res, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateRes) ? (DateTime?)dateRes : null;
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
            var langPrefs = new[] { new LANGPREFERENCES2 { guidLang = CommonGuidList.guidPythonLanguageServiceGuid } };
            ErrorHandler.ThrowOnFailure(txtMgr.GetUserPreferences2(null, null, langPrefs, null));
            return langPrefs[0];
        }

        internal void SetLanguagePreferences(LANGPREFERENCES2 langPrefs) {
            var txtMgr = (IVsTextManager2)_container.GetService(typeof(SVsTextManager));
            ErrorHandler.ThrowOnFailure(txtMgr.SetUserPreferences2(null, null, new[] { langPrefs }, null));
        }

        #endregion

        internal Dictionary<string, string> GetFullEnvironment(LaunchConfiguration config)
            => LaunchConfigurationUtils.GetFullEnvironment(config, _container);

        internal IEnumerable<string> GetGlobalPythonSearchPaths(InterpreterConfiguration interpreter) {
            if (!GeneralOptions.ClearGlobalPythonPath) {
                string pythonPath = Environment.GetEnvironmentVariable(interpreter.PathEnvironmentVariable) ?? string.Empty;
                return pythonPath
                    .Split(Path.PathSeparator)
                    // Just ensure the string is not empty - if people are passing
                    // through invalid paths this option is meant to allow it
                    .Where(p => !string.IsNullOrEmpty(p));
            }

            return Enumerable.Empty<string>();
        }
    }
}
