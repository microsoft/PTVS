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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Windows;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Repl {
#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
    using IReplEvaluator = IInteractiveEngine;
#endif

    internal class PythonReplEvaluator : BasePythonReplEvaluator {
        private readonly IErrorProviderFactory _errorProviderFactory;
        private readonly IPythonInterpreterFactoryProvider _factProvider;
        private readonly Guid _guid;
        private readonly Version _version;
        private IPythonInterpreterFactory _interpreter;
        private VsProjectAnalyzer _replAnalyzer;
        private bool _ownsAnalyzer, _enableAttach;

        public PythonReplEvaluator(IPythonInterpreterFactoryProvider factoryProvider, Guid guid, Version version, IErrorProviderFactory errorProviderFactory) {
            _factProvider = factoryProvider;
            _guid = guid;
            _version = version;
            _errorProviderFactory = errorProviderFactory;
        }

        private IPythonInterpreterFactory GetInterpreterFactory() {
            foreach (var factory in _factProvider.GetInterpreterFactories()) {
                if (factory.Id == _guid && _version == factory.Configuration.Version) {
                    return factory;
                }
            }
            return null;
        }

        public IPythonInterpreterFactory Interpreter {
            get {
                if (_interpreter == null) {
                    _interpreter = GetInterpreterFactory();
                }
                return _interpreter;
            }
        }

        internal VsProjectAnalyzer ReplAnalyzer {
            get {
                if (_replAnalyzer == null && Interpreter != null) {
                    _replAnalyzer = new VsProjectAnalyzer(Interpreter, _factProvider.GetInterpreterFactories().ToArray(), _errorProviderFactory);
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
                return Interpreter != null ? Interpreter.GetInterpreterDisplay() : string.Empty;
            }
        }

        public bool AttachEnabled {
            get {
                return _enableAttach;
            }
        }

        protected override PythonInteractiveCommonOptions CreatePackageOptions() {
            return new PythonInteractiveOptions();
        }

        protected override PythonInteractiveCommonOptions GetPackageOptions() {
            return PythonToolsPackage.Instance.InteractiveOptionsPage.GetOptions(Interpreter);
        }

        private bool EnableAttach {
            get {
                return ((PythonInteractiveOptions)CurrentOptions).EnableAttach;
            }
        }

        private string StartupScript {
            get {
                return ((PythonInteractiveOptions)CurrentOptions).StartupScript;
            }
        }

        private string InterpreterOptions {
            get {
                return ((PythonInteractiveOptions)CurrentOptions).InterpreterOptions;
            }
        }

        private string ExecutionMode {
            get {
                return ((PythonInteractiveOptions)CurrentOptions).ExecutionMode;
            }
        }

        protected override void Close() {
            base.Close();
            if (_ownsAnalyzer && _replAnalyzer != null) {
                _replAnalyzer.Dispose();
                _replAnalyzer = null;
            }
        }

        protected override void Connect() {
            _interpreter = GetInterpreterFactory();

            if (Interpreter == null) {
                Window.WriteError("The interpreter is not available.");
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

            if (!String.IsNullOrWhiteSpace(InterpreterOptions)) {
                args.Add(InterpreterOptions);
            }

            string filename, dir, extraArgs = null;
            VsProjectAnalyzer analyzer;
            if (PythonToolsPackage.TryGetStartupFileAndDirectory(out filename, out dir, out analyzer)) {
                processInfo.WorkingDirectory = dir;
                var startupProj = PythonToolsPackage.GetStartupProject();
                if (startupProj != null) {
                    string searchPath = startupProj.GetProjectProperty(CommonConstants.SearchPath, true);
                    string pathEnvVar = Interpreter.Configuration.PathEnvironmentVariable;
                    if (!string.IsNullOrEmpty(searchPath) && !String.IsNullOrWhiteSpace(pathEnvVar)) {
                        processInfo.EnvironmentVariables[pathEnvVar] = searchPath;
                    }

                    string interpArgs = startupProj.GetProjectProperty(CommonConstants.InterpreterArguments, true);
                    if (!String.IsNullOrWhiteSpace(interpArgs)) {
                        args.Add(interpArgs);
                    }

                    extraArgs = startupProj.GetProjectProperty(CommonConstants.CommandLineArguments, true);
                }
                if (analyzer.InterpreterFactory == _interpreter) {
                    if (_replAnalyzer != null && _replAnalyzer != analyzer) {
                        analyzer.SwitchAnalyzers(_replAnalyzer);
                    }
                    _replAnalyzer = analyzer;
                    _ownsAnalyzer = false;
                }
            }

            args.Add("\"" + Path.Combine(PythonToolsPackage.GetPythonToolsInstallPath(), "visualstudio_py_repl.py") + "\"");
            args.Add("--port");
            args.Add(portNum.ToString());

            if (!String.IsNullOrWhiteSpace(StartupScript)) {
                args.Add("--launch_file");
                args.Add("\"" + StartupScript + "\"");
            }

            _enableAttach = EnableAttach;
            if (EnableAttach) {
                args.Add("--enable-attach");
            }

            bool multipleScopes = true;
            if (!String.IsNullOrWhiteSpace(ExecutionMode)) {
                // change ID to module name if we have a registered mode
                var modes = Microsoft.PythonTools.Options.ExecutionMode.GetRegisteredModes();
                string modeValue = ExecutionMode;
                foreach (var mode in modes) {
                    if (mode.Id == ExecutionMode) {
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
                Window.WriteError(String.Format("Failed to start interactive process: {0}{1}{2}", Environment.NewLine, e.ToString(), Environment.NewLine));
                return;
            }

            CreateCommandProcessor(conn, null, processInfo.RedirectStandardOutput, process);
        }
    }
}