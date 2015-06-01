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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Windows;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
#else
using Microsoft.VisualStudio.Repl;
#endif
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using SR = Microsoft.PythonTools.Project.SR;

namespace Microsoft.PythonTools.Repl {
#if DEV14_OR_LATER
    using IReplWindow = IInteractiveWindow;
    using IReplEvaluator = IInteractiveEvaluator;
    using ReplRoleAttribute = InteractiveWindowRoleAttribute;
#endif

    [ReplRole("Execution")]
    [ReplRole("Reset")]
    internal class PythonReplEvaluator : BasePythonReplEvaluator {
        private IPythonInterpreterFactory _interpreter;
        private readonly IInterpreterOptionsService _interpreterService;
        private VsProjectAnalyzer _replAnalyzer;
        private bool _ownsAnalyzer, _enableAttach, _supportsMultipleCompleteStatementInputs;

        public PythonReplEvaluator(IPythonInterpreterFactory interpreter, IServiceProvider serviceProvider, IInterpreterOptionsService interpreterService = null)
            : this(interpreter, serviceProvider, new DefaultPythonReplEvaluatorOptions(serviceProvider, () => serviceProvider.GetPythonToolsService().GetInteractiveOptions(interpreter)), interpreterService) {
        }

        public PythonReplEvaluator(IPythonInterpreterFactory interpreter, IServiceProvider serviceProvider, PythonReplEvaluatorOptions options, IInterpreterOptionsService interpreterService = null)
            : base(serviceProvider, serviceProvider.GetPythonToolsService(), options) {
            _interpreter = interpreter;
            _interpreterService = interpreterService;
            if (_interpreterService != null) {
                _interpreterService.InterpretersChanged += InterpretersChanged;
            }
        }

        private class UnavailableFactory : IPythonInterpreterFactory {
            public UnavailableFactory(string id, string version) {
                Id = Guid.Parse(id);
                Configuration = new InterpreterConfiguration(Version.Parse(version));
            }
            public string Description { get { return Id.ToString(); } }
            public InterpreterConfiguration Configuration { get; private set; }
            public Guid Id { get; private set; }
            public IPythonInterpreter CreateInterpreter() { return null; }
        }

        public static IPythonReplEvaluator Create(
            IServiceProvider serviceProvider,
            string id,
            string version,
            IInterpreterOptionsService interpreterService
        ) {
            var factory = interpreterService != null ? interpreterService.FindInterpreter(id, version) : null;
            if (factory == null) {
                try {
                    factory = new UnavailableFactory(id, version);
                } catch (FormatException) {
                    return null;
                }
            }
            return new PythonReplEvaluator(factory, serviceProvider, interpreterService);
        }

        async void InterpretersChanged(object sender, EventArgs e) {
            var current = _interpreter;
            if (current == null) {
                return;
            }

            var interpreter = _interpreterService.FindInterpreter(current.Id, current.Configuration.Version);
            if (interpreter != null && interpreter != current) {
                // the interpreter has been reconfigured, we want the new settings
                _interpreter = interpreter;
                if (_replAnalyzer != null) {
                    var oldAnalyser = _replAnalyzer;
                    bool disposeOld = _ownsAnalyzer && oldAnalyser != null;
                    
                    _replAnalyzer = null;
                    var newAnalyzer = ReplAnalyzer;
                    if (newAnalyzer != null && oldAnalyser != null) {
                        newAnalyzer.SwitchAnalyzers(oldAnalyser);
                    }
                    if (disposeOld) {
                        oldAnalyser.Dispose();
                    }
                }

                // if the previous interpreter was not available, we will want
                // to reset afterwards
                if (current is UnavailableFactory) {
                    await Reset();
                }
            }
        }

        public IPythonInterpreterFactory Interpreter {
            get {
                return _interpreter;
            }
        }

        internal VsProjectAnalyzer ReplAnalyzer {
            get {
                if (_replAnalyzer == null && Interpreter != null && _interpreterService != null) {
                    _replAnalyzer = new VsProjectAnalyzer(_serviceProvider, Interpreter, _interpreterService.Interpreters.ToArray());
                    _ownsAnalyzer = true;
                }
                return _replAnalyzer;
            }
        }

        protected override PythonLanguageVersion AnalyzerProjectLanguageVersion {
            get {
                if (_replAnalyzer != null && _replAnalyzer.Project != null) {
                    return _replAnalyzer.Project.LanguageVersion;
                }
                return LanguageVersion;
            }
        }

        protected override PythonLanguageVersion LanguageVersion {
            get {
                return Interpreter != null ? Interpreter.GetLanguageVersion() : PythonLanguageVersion.None;
            }
        }

        internal override string DisplayName {
            get {
                return Interpreter != null ? Interpreter.Description : string.Empty;
            }
        }

        public bool AttachEnabled {
            get {
                return _enableAttach && !(Interpreter is UnavailableFactory);
            }
        }

        public override void Dispose() {
            if (_ownsAnalyzer && _replAnalyzer != null) {
                _replAnalyzer.Dispose();
                _replAnalyzer = null;
            }
            base.Dispose();
        }

        public override void Close() {
            base.Close();
            if (_interpreterService != null) {
                _interpreterService.InterpretersChanged -= InterpretersChanged;
            }
        }

        public override bool SupportsMultipleCompleteStatementInputs {
            get {
                return _supportsMultipleCompleteStatementInputs;
            }
        }

        protected override void WriteInitializationMessage() {
            if (Interpreter is UnavailableFactory) {
                Window.WriteError(SR.GetString(SR.ReplEvaluatorInterpreterNotFound));
            } else {
                base.WriteInitializationMessage();
            }
        }

        protected override void Connect() {
            _serviceProvider.GetUIThread().MustBeCalledFromUIThread();
            
            var configurableOptions = CurrentOptions as ConfigurablePythonReplOptions;
            if (configurableOptions != null) {
                _interpreter = configurableOptions.InterpreterFactory ?? _interpreter;
            }

            if (Interpreter == null || Interpreter is UnavailableFactory) {
                Window.WriteError(SR.GetString(SR.ReplEvaluatorInterpreterNotFound));
                return;
            } else if (String.IsNullOrWhiteSpace(Interpreter.Configuration.InterpreterPath)) {
                Window.WriteError(SR.GetString(SR.ReplEvaluatorInterpreterNotConfigured, Interpreter.Description));
                return;
            }
            var processInfo = new ProcessStartInfo(Interpreter.Configuration.InterpreterPath);

#if DEBUG
            bool debugMode = Environment.GetEnvironmentVariable("DEBUG_REPL") != null;
            processInfo.CreateNoWindow = !debugMode;
            processInfo.UseShellExecute = debugMode;
            processInfo.RedirectStandardOutput = !debugMode;
            processInfo.RedirectStandardError = !debugMode;
            processInfo.RedirectStandardInput = !debugMode;
#else
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardInput = true;
#endif

            Socket conn;
            int portNum;
            CreateConnection(out conn, out portNum);

            
            List<string> args = new List<string>();

            if (!String.IsNullOrWhiteSpace(CurrentOptions.InterpreterOptions)) {
                args.Add(CurrentOptions.InterpreterOptions);
            }

            var workingDir = CurrentOptions.WorkingDirectory;
            if (!string.IsNullOrEmpty(workingDir)) {
                processInfo.WorkingDirectory = workingDir;
            } else if (configurableOptions != null && configurableOptions.Project != null) {
                processInfo.WorkingDirectory = configurableOptions.Project.GetWorkingDirectory();
            } else {
                processInfo.WorkingDirectory = Interpreter.Configuration.PrefixPath;
            }

#if DEBUG
            if (!debugMode) {
#endif
                var envVars = CurrentOptions.EnvironmentVariables;
                if (envVars != null) {
                    foreach (var keyValue in envVars) {
                        processInfo.EnvironmentVariables[keyValue.Key] = keyValue.Value;
                    }
                }

                string pathEnvVar = Interpreter.Configuration.PathEnvironmentVariable ?? "";

                if (!string.IsNullOrWhiteSpace(pathEnvVar)) {
                    var searchPaths = CurrentOptions.SearchPaths;

                    if (string.IsNullOrEmpty(searchPaths)) {
                        if (_serviceProvider.GetPythonToolsService().GeneralOptions.ClearGlobalPythonPath) {
                            processInfo.EnvironmentVariables[pathEnvVar] = "";
                        }
                    } else if (_serviceProvider.GetPythonToolsService().GeneralOptions.ClearGlobalPythonPath) {
                        processInfo.EnvironmentVariables[pathEnvVar] = searchPaths;
                    } else {
                        processInfo.EnvironmentVariables[pathEnvVar] = searchPaths + ";" + Environment.GetEnvironmentVariable(pathEnvVar);
                    }
                }
#if DEBUG
            }
#endif
            var interpreterArgs = CurrentOptions.InterpreterArguments;
            if (!String.IsNullOrWhiteSpace(interpreterArgs)) {
                args.Add(interpreterArgs);
            }

            var analyzer = CurrentOptions.ProjectAnalyzer;
            if (analyzer != null && analyzer.InterpreterFactory == _interpreter) {
                if (_replAnalyzer != null && _replAnalyzer != analyzer) {
                    analyzer.SwitchAnalyzers(_replAnalyzer);
                }
                _replAnalyzer = analyzer;
                _ownsAnalyzer = false;
            }

            args.Add(ProcessOutput.QuoteSingleArgument(PythonToolsInstallPath.GetFile("visualstudio_py_repl.py")));
            args.Add("--port");
            args.Add(portNum.ToString());

            if (!String.IsNullOrWhiteSpace(CurrentOptions.StartupScript)) {
                args.Add("--launch_file");
                args.Add(ProcessOutput.QuoteSingleArgument(CurrentOptions.StartupScript));
            }

            _enableAttach = CurrentOptions.EnableAttach;
            if (CurrentOptions.EnableAttach) {
                args.Add("--enable-attach");
            }

            bool multipleScopes = true;
            if (!String.IsNullOrWhiteSpace(CurrentOptions.ExecutionMode)) {
                // change ID to module name if we have a registered mode
                var modes = Microsoft.PythonTools.Options.ExecutionMode.GetRegisteredModes(_serviceProvider);
                string modeValue = CurrentOptions.ExecutionMode;
                foreach (var mode in modes) {
                    if (mode.Id == CurrentOptions.ExecutionMode) {
                        modeValue = mode.Type;
                        multipleScopes = mode.SupportsMultipleScopes;
                        _supportsMultipleCompleteStatementInputs = mode.SupportsMultipleCompleteStatementInputs;
                        break;
                    }
                }
                args.Add("--execution_mode");
                args.Add(modeValue);
            }

            SetMultipleScopes(multipleScopes);

            processInfo.Arguments = String.Join(" ", args);

            var process = new Process();
            process.StartInfo = processInfo;
            try {
                if (!File.Exists(processInfo.FileName)) {
                    throw new Win32Exception(Microsoft.VisualStudioTools.Project.NativeMethods.ERROR_FILE_NOT_FOUND);
                }
                process.Start();
            } catch (Exception e) {
                if (e.IsCriticalException()) {
                    throw;
                }

                Win32Exception wex = e as Win32Exception;
                if (wex != null && wex.NativeErrorCode == Microsoft.VisualStudioTools.Project.NativeMethods.ERROR_FILE_NOT_FOUND) {
                    Window.WriteError(SR.GetString(SR.ReplEvaluatorInterpreterNotFound));
                } else {
                    Window.WriteError(SR.GetString(SR.ErrorStartingInteractiveProcess, e.ToString()));
                }
                return;
            }

            CreateCommandProcessor(conn, processInfo.RedirectStandardOutput, process);
        }

        const int ERROR_FILE_NOT_FOUND = 2;
    }


    [ReplRole("DontPersist")]
    class PythonReplEvaluatorDontPersist : PythonReplEvaluator {
        public PythonReplEvaluatorDontPersist(IPythonInterpreterFactory interpreter, IServiceProvider serviceProvider, PythonReplEvaluatorOptions options, IInterpreterOptionsService interpreterService) :
            base(interpreter, serviceProvider, options, interpreterService) {
        }
    }

    /// <summary>
    /// Base class used for providing REPL options
    /// </summary>
    abstract class PythonReplEvaluatorOptions {
        public abstract string InterpreterOptions {
            get;
        }

        public abstract string WorkingDirectory {
            get;
        }

        public abstract IDictionary<string, string> EnvironmentVariables {
            get;
        }

        public abstract string StartupScript {
            get;
        }

        public abstract string SearchPaths {
            get;
        }

        public abstract string InterpreterArguments {
            get;
        }

        public abstract VsProjectAnalyzer ProjectAnalyzer {
            get;
        }

        public abstract bool UseInterpreterPrompts {
            get;
        }

        public abstract string ExecutionMode {
            get;
        }

        public abstract bool EnableAttach {
            get;
        }

        public abstract bool InlinePrompts {
            get;
        }

        public abstract bool ReplSmartHistory {
            get;
        }

        public abstract bool LiveCompletionsOnly {
            get;
        }

        public abstract string PrimaryPrompt {
            get;
        }

        public abstract string SecondaryPrompt {
            get;
        }
    }

    class ConfigurablePythonReplOptions : PythonReplEvaluatorOptions {
        private IPythonInterpreterFactory _factory;
        private PythonProjectNode _project;

        internal string _interpreterOptions;
        internal string _workingDir;
        internal IDictionary<string, string> _envVars;
        internal string _startupScript;
        internal string _searchPaths;
        internal string _interpreterArguments;
        internal VsProjectAnalyzer _projectAnalyzer;
        internal bool _useInterpreterPrompts;
        internal string _executionMode;
        internal bool _liveCompletionsOnly;
        internal bool _replSmartHistory;
        internal bool _inlinePrompts;
        internal bool _enableAttach;
        internal string _primaryPrompt;
        internal string _secondaryPrompt;

        public ConfigurablePythonReplOptions() {
            _replSmartHistory = true;
            _inlinePrompts = true;
            _primaryPrompt = ">>> ";
            _secondaryPrompt = "... ";
        }

        internal ConfigurablePythonReplOptions Clone() {
            var newOptions = (ConfigurablePythonReplOptions)MemberwiseClone();
            if (_envVars != null) {
                newOptions._envVars = new Dictionary<string, string>();
                foreach (var kv in _envVars) {
                    newOptions._envVars[kv.Key] = kv.Value;
                }
            }
            return newOptions;
        }

        public IPythonInterpreterFactory InterpreterFactory {
            get { return _factory; }
            set { _factory = value; }
        }

        public PythonProjectNode Project {
            get { return _project; }
            set {
                _project = value;
                if (_workingDir == null) {
                    _workingDir = _project.GetWorkingDirectory();
                }
                if (_searchPaths == null) {
                    _searchPaths = string.Join(";", _project.GetSearchPaths());
                }
            }
        }

        public override string InterpreterOptions {
            get { return _interpreterOptions ?? ""; }
        }

        public override string WorkingDirectory {
            get { return _workingDir ?? ""; }
        }

        public override IDictionary<string, string> EnvironmentVariables {
            get { return _envVars; }
        }

        public override string StartupScript {
            get { return _startupScript ?? ""; }
        }

        public override string SearchPaths {
            get { return _searchPaths ?? ""; }
        }

        public override string InterpreterArguments {
            get { return _interpreterArguments ?? ""; }
        }

        public override VsProjectAnalyzer ProjectAnalyzer {
            get { return _projectAnalyzer; }
        }

        public override bool UseInterpreterPrompts {
            get { return _useInterpreterPrompts; }
        }

        public override string ExecutionMode {
            get { return _executionMode; }
        }

        public override bool EnableAttach {
            get { return _enableAttach; }
        }

        public override bool InlinePrompts {
            get { return _inlinePrompts; }
        }

        public override bool ReplSmartHistory {
            get { return _replSmartHistory; }
        }

        public override bool LiveCompletionsOnly {
            get { return _liveCompletionsOnly; }
        }

        public override string PrimaryPrompt {
            get { return _primaryPrompt; }
        }

        public override string SecondaryPrompt {
            get { return _secondaryPrompt; }
        }
    }

    /// <summary>
    /// Provides REPL options based upon options stored in our UI.
    /// </summary>
    class DefaultPythonReplEvaluatorOptions : PythonReplEvaluatorOptions {
        private readonly Func<PythonInteractiveCommonOptions> _options;
        private readonly IServiceProvider _serviceProvider;

        public DefaultPythonReplEvaluatorOptions(IServiceProvider serviceProvider, Func<PythonInteractiveCommonOptions> options) {
            _serviceProvider = serviceProvider;
            _options = options;
        }

        public override string InterpreterOptions {
            get {
                return ((PythonInteractiveOptions)_options()).InterpreterOptions;
            }
        }

        public override bool EnableAttach {
            get {
                return ((PythonInteractiveOptions)_options()).EnableAttach;
            }
        }

        public override string StartupScript {
            get {
                return ((PythonInteractiveOptions)_options()).StartupScript;
            }
        }

        public override string ExecutionMode {
            get {
                return ((PythonInteractiveOptions)_options()).ExecutionMode;
            }
        }

        public override string WorkingDirectory {
            get {
                var startupProj = PythonToolsPackage.GetStartupProject(_serviceProvider);
                if (startupProj != null) {
                    return startupProj.GetWorkingDirectory();
                }

                var textView = CommonPackage.GetActiveTextView(_serviceProvider);
                if (textView != null) {
                    return Path.GetDirectoryName(textView.GetFilePath());
                }

                return null;
            }
        }

        public override IDictionary<string, string> EnvironmentVariables {
            get {
                return null;
            }
        }

        public override string SearchPaths {
            get {
                var startupProj = PythonToolsPackage.GetStartupProject(_serviceProvider) as IPythonProject;
                if (startupProj != null) {
                    return string.Join(";", startupProj.GetSearchPaths());
                }

                return null;
            }
        }

        public override string InterpreterArguments {
            get {
                var startupProj = PythonToolsPackage.GetStartupProject(_serviceProvider);
                if (startupProj != null) {
                    return startupProj.GetProjectProperty(PythonConstants.InterpreterArgumentsSetting, true);
                }
                return null;
            }
        }

        public override VsProjectAnalyzer ProjectAnalyzer {
            get {
                var startupProj = PythonToolsPackage.GetStartupProject(_serviceProvider);
                if (startupProj != null) {
                    return ((PythonProjectNode)startupProj).GetAnalyzer();
                }
                return null;
            }
        }

        public override bool UseInterpreterPrompts {
            get { return _options().UseInterpreterPrompts; }
        }

        public override bool InlinePrompts {
            get { return _options().InlinePrompts;  }
        }

        public override bool ReplSmartHistory {
            get { return _options().ReplSmartHistory; }
        }

        public override bool LiveCompletionsOnly {
            get { return _options().LiveCompletionsOnly; }
        }

        public override string PrimaryPrompt {
            get { return _options().PrimaryPrompt; }
        }

        public override string SecondaryPrompt {
            get { return _options().SecondaryPrompt;  }
        }
    }
}