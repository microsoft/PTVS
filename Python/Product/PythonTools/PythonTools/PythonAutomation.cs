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

using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Repl;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Exposes language specific options for Python via automation. This object
    /// can be fetched using Dte.GetObject("VsPython").
    /// </summary>
    [ComVisible(true)]
    public sealed class PythonAutomation : IVsPython, IPythonOptions3, IPythonIntellisenseOptions {
        private readonly IServiceProvider _serviceProvider;
        private readonly PythonToolsService _pyService;
        private AutomationInteractiveOptions _interactiveOptions;

        internal PythonAutomation(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _pyService = serviceProvider.GetPythonToolsService();
            Debug.Assert(_pyService != null, "Did not find PythonToolsService");
        }

        #region IPythonOptions Members

        IPythonIntellisenseOptions IPythonOptions.Intellisense {
            get { return this; }
        }

        IPythonInteractiveOptions IPythonOptions.Interactive {
            get {
                if (_interactiveOptions == null) {
                    _interactiveOptions = new AutomationInteractiveOptions(_serviceProvider);
                }
                return _interactiveOptions;
            }
        }

        bool IPythonOptions.PromptBeforeRunningWithBuildErrorSetting {
            get {
                return _pyService.DebuggerOptions.PromptBeforeRunningWithBuildError;
            }
            set {
                _pyService.DebuggerOptions.PromptBeforeRunningWithBuildError = value;
                _pyService.DebuggerOptions.Save();
            }
        }

        Severity IPythonOptions.IndentationInconsistencySeverity {
            get {
                return _pyService.GeneralOptions.IndentationInconsistencySeverity;
            }
            set {
                _pyService.GeneralOptions.IndentationInconsistencySeverity = value;
                _pyService.GeneralOptions.Save();
            }
        }

        bool IPythonOptions.AutoAnalyzeStandardLibrary {
            get {
                return _pyService.GeneralOptions.AutoAnalyzeStandardLibrary;
            }
            set {
                _pyService.GeneralOptions.AutoAnalyzeStandardLibrary = value;
                _pyService.GeneralOptions.Save();
            }
        }

        bool IPythonOptions.TeeStandardOutput {
            get {
                return _pyService.DebuggerOptions.TeeStandardOutput;
            }
            set {
                _pyService.DebuggerOptions.TeeStandardOutput = value;
                _pyService.DebuggerOptions.Save();
            }
        }

        bool IPythonOptions.WaitOnAbnormalExit {
            get {
                return _pyService.DebuggerOptions.WaitOnAbnormalExit;
            }
            set {
                _pyService.DebuggerOptions.WaitOnAbnormalExit = value;
                _pyService.DebuggerOptions.Save();
            }
        }

        bool IPythonOptions.WaitOnNormalExit {
            get {
                return _pyService.DebuggerOptions.WaitOnNormalExit;
            }
            set {
                _pyService.DebuggerOptions.WaitOnNormalExit = value;
                _pyService.DebuggerOptions.Save();
            }
        }

        public bool UseLegacyDebugger {
            get {
                return _pyService.DebuggerOptions.UseLegacyDebugger;
            }
            set {
                _pyService.DebuggerOptions.UseLegacyDebugger = value;
                _pyService.DebuggerOptions.Save();
            }
        }

        bool IPythonOptions3.PromptForEnvCreate {
            get {
                return _pyService.GeneralOptions.PromptForEnvCreate;
            }
            set {
                _pyService.GeneralOptions.PromptForEnvCreate = value;
                _pyService.GeneralOptions.Save();
            }
        }

        bool IPythonOptions3.PromptForPackageInstallation {
            get {
                return _pyService.GeneralOptions.PromptForPackageInstallation;
            }
            set {
                _pyService.GeneralOptions.PromptForPackageInstallation = value;
                _pyService.GeneralOptions.Save();
            }
        }


        #endregion

        #region IPythonIntellisenseOptions Members

        bool IPythonIntellisenseOptions.AddNewLineAtEndOfFullyTypedWord {
            get {
                return _pyService.AdvancedOptions.AddNewLineAtEndOfFullyTypedWord;
            }
            set {
                _pyService.AdvancedOptions.AddNewLineAtEndOfFullyTypedWord = value;
                _pyService.AdvancedOptions.Save();
            }
        }

        bool IPythonIntellisenseOptions.EnterCommitsCompletion {
            get {
                return _pyService.AdvancedOptions.EnterCommitsIntellisense;
            }
            set {
                _pyService.AdvancedOptions.EnterCommitsIntellisense = value;
                _pyService.AdvancedOptions.Save();
            }
        }

        bool IPythonIntellisenseOptions.UseMemberIntersection {
            get {
                return _pyService.AdvancedOptions.IntersectMembers;
            }
            set {
                _pyService.AdvancedOptions.IntersectMembers = value;
                _pyService.AdvancedOptions.Save();

            }
        }

        string IPythonIntellisenseOptions.CompletionCommittedBy {
            get {
                return _pyService.AdvancedOptions.CompletionCommittedBy;
            }
            set {
                _pyService.AdvancedOptions.CompletionCommittedBy = value;
                _pyService.AdvancedOptions.Save();
            }
        }

        bool IPythonIntellisenseOptions.AutoListIdentifiers {
            get {
                return _pyService.AdvancedOptions.AutoListIdentifiers;
            }
            set {
                _pyService.AdvancedOptions.AutoListIdentifiers = value;
                _pyService.AdvancedOptions.Save();
            }
        }

        #endregion

        void IVsPython.OpenInteractive(string description) {
            var compModel = _pyService.ComponentModel;
            if (compModel == null) {
                throw new InvalidOperationException("Could not activate component model");
            }

            var provider = compModel.GetService<InteractiveWindowProvider>();
            var interpreters = compModel.GetService<IInterpreterRegistryService>();

            var factory = interpreters.Configurations
                .Where(PythonInterpreterFactoryExtensions.IsRunnable)
                .FirstOrDefault(f => f.Description.Equals(description, StringComparison.CurrentCultureIgnoreCase));
            if (factory == null) {
                throw new KeyNotFoundException("Could not create interactive window with name: " + description);
            }

            var window = provider.OpenOrCreate(
                PythonReplEvaluatorProvider.GetEvaluatorId(factory)
            );

            if (window == null) {
                throw new InvalidOperationException("Could not create interactive window");
            }

            window.Show(true);
        }
    }

    [ComVisible(true)]
    public sealed class AutomationInteractiveOptions : IPythonInteractiveOptions {
        private readonly IServiceProvider _serviceProvider;

        public AutomationInteractiveOptions(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        internal PythonInteractiveOptions CurrentOptions {
            get {
                return _serviceProvider.GetPythonToolsService().InteractiveOptions;
            }
        }

        private void SaveSettingsToStorage() {
            CurrentOptions.Save();
        }

        bool IPythonInteractiveOptions.UseSmartHistory {
            get {
                return CurrentOptions.UseSmartHistory;

            }
            set {
                CurrentOptions.UseSmartHistory = value;
                SaveSettingsToStorage();
            }
        }

        string IPythonInteractiveOptions.CompletionMode {
            get {
                return CurrentOptions.CompletionMode.ToString();
            }
            set {
                ReplIntellisenseMode mode;
                if (Enum.TryParse(value, out mode)) {
                    CurrentOptions.CompletionMode = mode;
                    SaveSettingsToStorage();
                } else {
                    throw new InvalidOperationException(
                        String.Format(
                            "Bad intellisense mode, must be one of: {0}",
                            String.Join(", ", Enum.GetNames(typeof(ReplIntellisenseMode)))
                        )
                    );
                }
            }
        }

        string IPythonInteractiveOptions.StartupScripts {
            get {
                return CurrentOptions.Scripts;
            }
            set {
                CurrentOptions.Scripts = value;
                SaveSettingsToStorage();
            }
        }
    }
}
