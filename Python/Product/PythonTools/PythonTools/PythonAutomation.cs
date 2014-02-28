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
        internal PythonAutomation() {
        }

        #region IPythonOptions Members

        IPythonIntellisenseOptions IPythonOptions.Intellisense {
            get { return this; }
        }

        IPythonInteractiveOptions IPythonOptions.GetInteractiveOptions(string interpreterName) {
            var interpreters = PythonToolsPackage.Instance.InterpreterOptionsPage._options.Keys;
            var factory = interpreters.FirstOrDefault(i => i.Description == interpreterName);

            return factory == null ? null : new AutomationInterpreterOptions(factory);
        }

        bool IPythonOptions.PromptBeforeRunningWithBuildErrorSetting {
            get {
                return PythonToolsPackage.Instance.DebuggingOptionsPage.PromptBeforeRunningWithBuildError;
            }
            set {
                PythonToolsPackage.Instance.DebuggingOptionsPage.PromptBeforeRunningWithBuildError = value;
                PythonToolsPackage.Instance.DebuggingOptionsPage.SaveSettingsToStorage();
            }
        }

        Severity IPythonOptions.IndentationInconsistencySeverity {
            get {
                return PythonToolsPackage.Instance.DebuggingOptionsPage.IndentationInconsistencySeverity;
            }
            set {
                PythonToolsPackage.Instance.DebuggingOptionsPage.IndentationInconsistencySeverity = value;
                PythonToolsPackage.Instance.DebuggingOptionsPage.SaveSettingsToStorage();
            }
        }

        bool IPythonOptions.AutoAnalyzeStandardLibrary {
            get {
                return PythonToolsPackage.Instance.DebuggingOptionsPage.AutoAnalyzeStandardLibrary;
            }
            set {
                PythonToolsPackage.Instance.DebuggingOptionsPage.AutoAnalyzeStandardLibrary = value;
                PythonToolsPackage.Instance.DebuggingOptionsPage.SaveSettingsToStorage();
            }
        }

        bool IPythonOptions.TeeStandardOutput {
            get {
                return PythonToolsPackage.Instance.DebuggingOptionsPage.TeeStandardOutput;
            }
            set {
                PythonToolsPackage.Instance.DebuggingOptionsPage.TeeStandardOutput = value;
                PythonToolsPackage.Instance.DebuggingOptionsPage.SaveSettingsToStorage();
            }
        }

        bool IPythonOptions.WaitOnAbnormalExit {
            get {
                return PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit;
            }
            set {
                PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnAbnormalExit = value;
                PythonToolsPackage.Instance.DebuggingOptionsPage.SaveSettingsToStorage();
            }
        }

        bool IPythonOptions.WaitOnNormalExit {
            get {
                return PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit;
            }
            set {
                PythonToolsPackage.Instance.DebuggingOptionsPage.WaitOnNormalExit = value;
                PythonToolsPackage.Instance.DebuggingOptionsPage.SaveSettingsToStorage();
            }
        }

        #endregion

        #region IPythonIntellisenseOptions Members

        bool IPythonIntellisenseOptions.AddNewLineAtEndOfFullyTypedWord {
            get {
                return PythonToolsPackage.Instance.AdvancedEditorOptionsPage.AddNewLineAtEndOfFullyTypedWord;
            }
            set {
                PythonToolsPackage.Instance.AdvancedEditorOptionsPage.AddNewLineAtEndOfFullyTypedWord = value;
                PythonToolsPackage.Instance.AdvancedEditorOptionsPage.SaveSettingsToStorage();
            }
        }

        bool IPythonIntellisenseOptions.EnterCommitsCompletion {
            get {
                return PythonToolsPackage.Instance.AdvancedEditorOptionsPage.EnterCommitsIntellisense;
            }
            set {
                PythonToolsPackage.Instance.AdvancedEditorOptionsPage.EnterCommitsIntellisense = value;
                PythonToolsPackage.Instance.AdvancedEditorOptionsPage.SaveSettingsToStorage();
            }
        }

        bool IPythonIntellisenseOptions.UseMemberIntersection {
            get {
                return PythonToolsPackage.Instance.AdvancedEditorOptionsPage.IntersectMembers;
            }
            set {
                PythonToolsPackage.Instance.AdvancedEditorOptionsPage.IntersectMembers = value;
                PythonToolsPackage.Instance.AdvancedEditorOptionsPage.SaveSettingsToStorage();

            }
        }

        string IPythonIntellisenseOptions.CompletionCommittedBy {
            get {
                return PythonToolsPackage.Instance.AdvancedEditorOptionsPage.CompletionCommittedBy;
            }
            set {
                PythonToolsPackage.Instance.AdvancedEditorOptionsPage.CompletionCommittedBy = value;
                PythonToolsPackage.Instance.AdvancedEditorOptionsPage.SaveSettingsToStorage();
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
                var dte = (EnvDTE.DTE)PythonToolsPackage.GetGlobalService(typeof(EnvDTE.DTE));
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

        public AutomationInterpreterOptions(IPythonInterpreterFactory interpreterFactory) {
            _interpreterFactory = interpreterFactory;
        }

        internal PythonInteractiveOptions CurrentOptions {
            get {
                return PythonToolsPackage.Instance.InteractiveOptionsPage.GetOptions(_interpreterFactory);
            }
        }

        private static void SaveSettingsToStorage() {
            PythonToolsPackage.Instance.InteractiveOptionsPage.SaveSettingsToStorage();
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
                return CurrentOptions.InlinePrompts;
            }
            set {
                CurrentOptions.InlinePrompts = value;
                SaveSettingsToStorage();
            }
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
                            "Bad intellisense mode, must be one of: ",
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
                foreach (var mode in ExecutionMode.GetRegisteredModes()) {
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
