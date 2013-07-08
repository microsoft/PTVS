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
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
    using IReplEvaluator = IInteractiveEngine;
#endif

    [ReplRole("Execution")]
    [ReplRole("Reset")]
    internal class PythonReplEvaluator : BasePythonReplEvaluator {
        private readonly IErrorProviderFactory _errorProviderFactory;
        private readonly string _envVars;
        private IPythonInterpreterFactory _interpreter;
        private readonly IInterpreterOptionsService _interpreterService;
        private VsProjectAnalyzer _replAnalyzer;
        private bool _ownsAnalyzer, _enableAttach;

        public PythonReplEvaluator(IPythonInterpreterFactory interpreter, IErrorProviderFactory errorProviderFactory, IInterpreterOptionsService interpreterService = null) 
            : this(interpreter, errorProviderFactory, new DefaultPythonReplEvaluatorOptions(PythonToolsPackage.Instance.InteractiveOptionsPage.GetOptions(interpreter)), interpreterService) {
        }

        public PythonReplEvaluator(IPythonInterpreterFactory interpreter, IErrorProviderFactory errorProviderFactory, PythonReplEvaluatorOptions options, IInterpreterOptionsService interpreterService = null) :
            this(interpreter, errorProviderFactory, options, "", interpreterService) {
        }

        public PythonReplEvaluator(IPythonInterpreterFactory interpreter, IErrorProviderFactory errorProviderFactory, PythonReplEvaluatorOptions options, string envVars, IInterpreterOptionsService interpreterService = null)
            : base(options) {
            _interpreter = interpreter;
            _errorProviderFactory = errorProviderFactory;
            _interpreterService = interpreterService;
            _envVars = envVars;
            if (_interpreterService != null) {
                _interpreterService.InterpretersChanged += InterpretersChanged;
            }
        }

        void InterpretersChanged(object sender, EventArgs e) {
            var interpreter = _interpreterService.FindInterpreter(Interpreter.Id, Interpreter.Configuration.Version);
            if (interpreter != null && interpreter != _interpreter) {
                // the interpreter has been reconfigured, we want the new settings
                _interpreter = interpreter;
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
                    _replAnalyzer = new VsProjectAnalyzer(Interpreter, _interpreterService.Interpreters.ToArray(), _errorProviderFactory);
                    _ownsAnalyzer = true;
                }
                return _replAnalyzer;
            }
        }

        protected override PythonLanguageVersion AnalyzerProjectLanguageVersion {
            get {
                return _replAnalyzer.Project.LanguageVersion;
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
                return _enableAttach;
            }
        }

        public override void Dispose() {
            if (_ownsAnalyzer && _replAnalyzer != null) {
                _replAnalyzer.Dispose();
                _replAnalyzer = null;
            }
            base.Dispose();
        }

        protected override void Close() {
            base.Close();
            if (_interpreterService != null) {
                _interpreterService.InterpretersChanged -= InterpretersChanged;
            }
        }

        protected override void Connect() {
            if (Interpreter == null) {
                Window.WriteError("The interpreter is not available.");
                return;
            } else if (String.IsNullOrWhiteSpace(Interpreter.Configuration.InterpreterPath)) {
                Window.WriteError(String.Format("The interpreter {0} cannot be started.  The path to the interpreter has not been configured." + Environment.NewLine + "Please update the interpreter in Tools->Options->Python Tools->Interpreter Options" + Environment.NewLine, Interpreter.Description));
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

            if (!String.IsNullOrWhiteSpace(_envVars)) {
                foreach (var envVar in _envVars.Split(new[] { ';' })) {
                    var nameAndValue = envVar.Split(new[] { '=' }, 2);
                    if (nameAndValue.Length == 2) {
                        processInfo.EnvironmentVariables[nameAndValue[0]] = nameAndValue[1];
                    }
                }
            }

            List<string> args = new List<string>();

            if (!String.IsNullOrWhiteSpace(CurrentOptions.InterpreterOptions)) {
                args.Add(CurrentOptions.InterpreterOptions);
            }

            var workingDir = CurrentOptions.WorkingDirectory;
            if (workingDir != null) {
                processInfo.WorkingDirectory = workingDir;
            }

            var searchPaths = CurrentOptions.SearchPaths;
            if (searchPaths != null) {
                string pathEnvVar = Interpreter.Configuration.PathEnvironmentVariable;
                if (!string.IsNullOrEmpty(searchPaths) && !String.IsNullOrWhiteSpace(pathEnvVar)) {
                    processInfo.EnvironmentVariables[pathEnvVar] = searchPaths;
                }
            }

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

            args.Add(ProcessOutput.QuoteSingleArgument(Path.Combine(PythonToolsPackage.GetPythonToolsInstallPath(), "visualstudio_py_repl.py")));
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
                var modes = Microsoft.PythonTools.Options.ExecutionMode.GetRegisteredModes();
                string modeValue = CurrentOptions.ExecutionMode;
                foreach (var mode in modes) {
                    if (mode.Id == CurrentOptions.ExecutionMode) {
                        modeValue = mode.Type;
                        multipleScopes = mode.SupportsMultipleScopes;
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
                process.Start();
            } catch (Exception e) {
                Win32Exception wex = e as Win32Exception;
                if (wex != null && wex.NativeErrorCode == Microsoft.VisualStudioTools.Project.NativeMethods.ERROR_FILE_NOT_FOUND) {
                    Window.WriteError(
                        String.Format(
                            "Failed to start interactive process, the interpreter could not be found: {0}{1}",
                            Interpreter.Configuration.InterpreterPath,
                            Environment.NewLine
                        )
                    );
                } else {
                    Window.WriteError(String.Format("Failed to start interactive process: {0}{1}{2}", Environment.NewLine, e.ToString(), Environment.NewLine));
                }
                return;
            }

            CreateCommandProcessor(conn, null, processInfo.RedirectStandardOutput, process);
        }

        const int ERROR_FILE_NOT_FOUND = 2;
    }


    [ReplRole("DontPersist")]
    class PythonReplEvaluatorDontPersist : PythonReplEvaluator {
        public PythonReplEvaluatorDontPersist(IPythonInterpreterFactory interpreter, IErrorProviderFactory errorProviderFactory, PythonReplEvaluatorOptions options, string envVars, IInterpreterOptionsService interpreterService) :
            base(interpreter, errorProviderFactory, options, envVars, interpreterService) {
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
        private readonly string _workingDir;

        public ConfigurablePythonReplOptions(string workingDir) {
            _workingDir = workingDir;
        }

        public override string InterpreterOptions {
            get { return ""; }
        }

        public override string WorkingDirectory {
            get { return _workingDir; }
        }

        public override string StartupScript {
            get { return ""; }
        }

        public override string SearchPaths {
            get { return ""; }
        }

        public override string InterpreterArguments {
            get { return ""; }
        }

        public override VsProjectAnalyzer ProjectAnalyzer {
            get {
                return null;
            }
        }

        public override bool UseInterpreterPrompts {
            get { return true; }
        }

        public override string ExecutionMode {
            get { return null; }
        }

        public override bool EnableAttach {
            get { return false; }
        }

        public override bool InlinePrompts {
            get { return true; }
        }

        public override bool ReplSmartHistory {
            get { return true; }
        }

        public override bool LiveCompletionsOnly {
            get { return false; }
        }

        public override string PrimaryPrompt {
            get { return ">>> "; }
        }

        public override string SecondaryPrompt {
            get { return "... "; }
        }
    }

    /// <summary>
    /// Provides REPL options based upon options stored in our UI.
    /// </summary>
    class DefaultPythonReplEvaluatorOptions : PythonReplEvaluatorOptions {
        private readonly PythonInteractiveCommonOptions _options;

        public DefaultPythonReplEvaluatorOptions(PythonInteractiveCommonOptions options) {
            _options = options;
        }

        public override string InterpreterOptions {
            get {
                return ((PythonInteractiveOptions)_options).InterpreterOptions;
            }
        }

        public override bool EnableAttach {
            get {
                return ((PythonInteractiveOptions)_options).EnableAttach;
            }
        }

        public override string StartupScript {
            get {
                return ((PythonInteractiveOptions)_options).StartupScript;
            }
        }

        public override string ExecutionMode {
            get {
                return ((PythonInteractiveOptions)_options).ExecutionMode;
            }
        }

        public override string WorkingDirectory {
            get {
                var startupProj = PythonToolsPackage.GetStartupProject();
                if (startupProj != null) {
                    return startupProj.GetWorkingDirectory();
                }

                var textView = CommonPackage.GetActiveTextView();
                if (textView != null) {
                    return Path.GetDirectoryName(textView.GetFilePath());
                }

                return null;
            }
        }

        public override string SearchPaths {
            get {
                var startupProj = PythonToolsPackage.GetStartupProject();
                if (startupProj != null) {
                    return startupProj.GetProjectProperty(CommonConstants.SearchPath, true);
                }

                return null;
            }
        }

        public override string InterpreterArguments {
            get {
                var startupProj = PythonToolsPackage.GetStartupProject();
                if (startupProj != null) {
                    return startupProj.GetProjectProperty(CommonConstants.InterpreterArguments, true);
                }
                return null;
            }
        }

        public override VsProjectAnalyzer ProjectAnalyzer {
            get { 
                var startupProj = PythonToolsPackage.GetStartupProject();
                if (startupProj != null) {
                    return ((PythonProjectNode)startupProj).GetAnalyzer();
                }
                return null;
            }
        }

        public override bool UseInterpreterPrompts {
            get { return _options.UseInterpreterPrompts; }
        }

        public override bool InlinePrompts {
            get { return _options.InlinePrompts;  }
        }

        public override bool ReplSmartHistory {
            get { return _options.ReplSmartHistory; }
        }

        public override bool LiveCompletionsOnly {
            get { return _options.LiveCompletionsOnly; }
        }

        public override string PrimaryPrompt {
            get { return _options.PrimaryPrompt; }
        }

        public override string SecondaryPrompt {
            get { return _options.SecondaryPrompt;  }
        }
    }
}