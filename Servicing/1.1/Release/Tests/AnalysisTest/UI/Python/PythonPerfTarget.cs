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
using System.Windows.Automation;

namespace AnalysisTest.UI.Python {
    class PythonPerfTarget : AutomationWrapper {
        public PythonPerfTarget(IntPtr hwnd)
            : base(AutomationElement.FromHandle(hwnd)) {            
        }

        /// <summary>
        /// Checks the Profile Project radio box
        /// </summary>
        public void SelectProfileProject() {
            Select(FindByAutomationId("_profileProject"));
        }

        /// <summary>
        /// Checks the Profile Script radio box
        /// </summary>
        public void SelectProfileScript() {
            var elem = FindByAutomationId("_profileScript");
            var pats = elem.GetSupportedPatterns();
            string[] names = new string[pats.Length];
            for (int i = 0; i < pats.Length; i++) {
                names[i] = pats[i].ProgrammaticName;
            }
            Select(FindByAutomationId("_profileScript"));
        }

        public string SelectedProject {
            get {
                return SelectedProjectComboBox.GetSelectedItemName();
            }
        }

        public ComboBox SelectedProjectComboBox {
            get {
                return new ComboBox(FindByAutomationId("_project"));
            }
        }

        /// <summary>
        /// Returns the current selection in the combo box.
        /// </summary>
        public string SelectedInterpreter {
            get {
                return InterpreterComboBox.GetSelectedItemName();
            }
        }

        /// <summary>
        /// Gets the interpreter combo box.
        /// </summary>
        public ComboBox InterpreterComboBox {
            get {
                return new ComboBox(FindByAutomationId("_pythonInterpreter"));
            }
        }

        /// <summary>
        /// Returns the string the user entered into the interpreter combo box.
        /// </summary>
        public string EnteredInterpreter {
            get {
                return InterpreterComboBox.GetEnteredText();
            }
        }

        public string WorkingDir {
            get {
                return WorkingDirectoryTextBox.GetValue();
            }
            set {
                WorkingDirectoryTextBox.SetValue(value);
            }
        }

        private AutomationWrapper WorkingDirectoryTextBox {
            get {
                return new AutomationWrapper(FindByAutomationId("_workingDir"));
            }
        }

        public string ScriptName {
            get {
                return ScriptNameTextBox.GetValue();
            }
            set {
                ScriptNameTextBox.SetValue(value);
            }
        }

        private AutomationWrapper ScriptNameTextBox {
            get {
                return new AutomationWrapper(FindByAutomationId("_scriptName"));
            }
        }

        public string Arguments {
            get {
                return ArgumentsTextBox.GetValue();
            }
            set {
                ArgumentsTextBox.SetValue(value);
            }
        }

        private AutomationWrapper ArgumentsTextBox {
            get {
                return new AutomationWrapper(FindByAutomationId("_cmdLineArgs"));
            }
        }

        public void Ok() {
            Invoke(FindButton("Ok"));
        }

        public void Cancel() {
            Invoke(FindButton("Cancel"));
        }
    }
}
