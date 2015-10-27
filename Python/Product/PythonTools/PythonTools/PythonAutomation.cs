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
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Repl;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Exposes language specific options for Python via automation. This object
    /// can be fetched using Dte.GetObject("VsPython").
    /// </summary>
    [ComVisible(true)]
    public sealed class PythonAutomation : IVsPython, IPythonOptions, IPythonIntellisenseOptions {
        private readonly IServiceProvider _serviceProvider;
        private readonly PythonToolsService _pyService;

        internal PythonAutomation(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _pyService = serviceProvider.GetPythonToolsService();
            Debug.Assert(_pyService != null, "Did not find PythonToolsService");
        }

        #region IPythonOptions Members

        IPythonIntellisenseOptions IPythonOptions.Intellisense {
            get { return this; }
        }

        IPythonInteractiveOptions IPythonOptions.GetInteractiveOptions(string interpreterName) {
            var interpreters = _pyService.ComponentModel.GetService<IInterpreterOptionsService>().Interpreters;
            var factory = interpreters.FirstOrDefault(i => i.Description == interpreterName);

            return factory == null ? null : new AutomationInterpreterOptions(_serviceProvider, factory);
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
            int? commandId = null;
            lock (PythonToolsPackage.CommandsLock) {
                foreach (var command in PythonToolsPackage.Commands) {
                    OpenReplCommand replCommand = command.Key as OpenReplCommand;
                    if (replCommand != null && replCommand.Description == description) {
                        commandId = replCommand.CommandId;
                        break;
                    }
                }
            }

            if (commandId.HasValue) {
                var dte = (EnvDTE.DTE)_serviceProvider.GetService(typeof(EnvDTE.DTE));
                object inObj = null, outObj = null;
                dte.Commands.Raise(GuidList.guidPythonToolsCmdSet.ToString("B"), commandId.Value, ref inObj, ref outObj);
            } else {
                throw new KeyNotFoundException("Could not find interactive window with name: " + description);
            }
        }
    }

    [ComVisible(true)]
    public sealed class AutomationInterpreterOptions : IPythonInteractiveOptions {
        private readonly IPythonInterpreterFactory _interpreterFactory;
        private readonly IServiceProvider _serviceProvider;

        [Obsolete("A IServiceProvider should be provided")]
        public AutomationInterpreterOptions(IPythonInterpreterFactory interpreterFactory) {
            _interpreterFactory = interpreterFactory;
        }

        public AutomationInterpreterOptions(IServiceProvider serviceProvider, IPythonInterpreterFactory interpreterFactory) {
            _serviceProvider = serviceProvider;
            _interpreterFactory = interpreterFactory;
        }

        internal PythonInteractiveOptions CurrentOptions {
            get {
                return _serviceProvider.GetPythonToolsService().GetInteractiveOptions(_interpreterFactory);
            }
        }

        private void SaveSettingsToStorage() {
            CurrentOptions.Save(_interpreterFactory);
        }

        string IPythonInteractiveOptions.PrimaryPrompt {
            get {
                return CurrentOptions.PrimaryPrompt;
            }
            set {
                CurrentOptions.PrimaryPrompt = value;
                SaveSettingsToStorage();
            }
        }

        string IPythonInteractiveOptions.SecondaryPrompt {
            get {
                return CurrentOptions.SecondaryPrompt;
            }
            set {
                CurrentOptions.SecondaryPrompt = value;
                SaveSettingsToStorage();
            }
        }

        bool IPythonInteractiveOptions.UseInterpreterPrompts {
            get {
                return CurrentOptions.UseInterpreterPrompts;

            }
            set {
                CurrentOptions.UseInterpreterPrompts = value;
                SaveSettingsToStorage();
            }
        }

        bool IPythonInteractiveOptions.InlinePrompts {
            get {
                return true;
            }
            set { }
        }

        bool IPythonInteractiveOptions.ReplSmartHistory {
            get {
                return CurrentOptions.ReplSmartHistory;

            }
            set {
                CurrentOptions.ReplSmartHistory = value;
                SaveSettingsToStorage();
            }
        }

        string IPythonInteractiveOptions.ReplIntellisenseMode {
            get {
                return CurrentOptions.ReplIntellisenseMode.ToString();
            }
            set {
                ReplIntellisenseMode mode;
                if (Enum.TryParse<ReplIntellisenseMode>(value, out mode)) {
                    CurrentOptions.ReplIntellisenseMode = mode;
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

        string IPythonInteractiveOptions.StartupScript {
            get {
                return CurrentOptions.StartupScript;
            }
            set {
                CurrentOptions.StartupScript = value;
                SaveSettingsToStorage();
            }
        }

        string IPythonInteractiveOptions.ExecutionMode {
            get {
                return CurrentOptions.ExecutionMode;
            }
            set {
                foreach (var mode in ExecutionMode.GetRegisteredModes(_serviceProvider)) {
                    if (mode.FriendlyName.Equals(value, StringComparison.OrdinalIgnoreCase)) {
                        value = mode.Id;
                        break;
                    }
                }

                CurrentOptions.ExecutionMode = value;
                SaveSettingsToStorage();
            }
        }

        string IPythonInteractiveOptions.InterpreterArguments {
            get {
                return CurrentOptions.InterpreterOptions;
            }
            set {
                CurrentOptions.InterpreterOptions = value;
                SaveSettingsToStorage();
            }
        }

        bool IPythonInteractiveOptions.EnableAttach {
            get {
                return CurrentOptions.EnableAttach;
            }
            set {
                CurrentOptions.EnableAttach = value;
                SaveSettingsToStorage();
            }
        }
    }
}
