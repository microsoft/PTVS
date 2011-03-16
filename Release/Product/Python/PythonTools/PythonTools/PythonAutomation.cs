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
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Repl;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Exposes language specific options for Python via automation.  This object can be fetched using Dte.GetObject("PythonOptions")
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
            foreach (var interpreter in PythonToolsPackage.Instance.InteractiveOptionsPage._options.Keys) {
                if (IsSameInterpreter(interpreter, interpreterName)) {
                    return new AutomationInterpreterOptions(interpreter);
                }
            }
            return null;
        }

        private bool IsSameInterpreter(IPythonInterpreterFactory interpreter, string interpreterName) {
            if (interpreter.GetInterpreterDisplay() == interpreterName) {
                return true;
            }

            return false;
        }

        bool IPythonOptions.PromptBeforeRunningWithBuildErrorSetting {
            get {
                return PythonToolsPackage.Instance.OptionsPage.PromptBeforeRunningWithBuildError;
            }
            set {
                PythonToolsPackage.Instance.OptionsPage.PromptBeforeRunningWithBuildError = value;
                PythonToolsPackage.Instance.OptionsPage.SaveSettingsToStorage();
            }
        }

        Severity IPythonOptions.IndentationInconsistencySeverity {
            get {
                return PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencySeverity;
            }
            set {
                PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencySeverity = value;
                PythonToolsPackage.Instance.OptionsPage.SaveSettingsToStorage();
            }
        }

        bool IPythonOptions.AutoAnalyzeStandardLibrary {
            get {
                return PythonToolsPackage.Instance.OptionsPage.AutoAnalyzeStandardLibrary;
            }
            set {
                PythonToolsPackage.Instance.OptionsPage.AutoAnalyzeStandardLibrary = value;
                PythonToolsPackage.Instance.OptionsPage.SaveSettingsToStorage();
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
            foreach (var command in PythonToolsPackage.Commands) {
                OpenReplCommand replCommand = command.Key as OpenReplCommand;
                if (replCommand != null && replCommand.Description == description) {
                    var dte = (EnvDTE.DTE)PythonToolsPackage.GetGlobalService(typeof(EnvDTE.DTE));
                    string name = "View.PythonInteractive";
                    if (replCommand.CommandId == PkgCmdIDList.cmdidReplWindow) {
                        name += "Default";
                    } else {
                        name += replCommand.CommandId - PkgCmdIDList.cmdidReplWindow;
                    }
                    dte.ExecuteCommand(name);
                    return;
                }
            }
            throw new KeyNotFoundException("Could not find interactive window with name: " + description);
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

    }
}
