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
using System.Threading;
using System.Windows.Automation;

namespace TestUtilities.UI.Python {
    class PythonPerfTarget : AutomationWrapper, IDisposable {
        public PythonPerfTarget(IntPtr hwnd)
            : base(AutomationElement.FromHandle(hwnd)) {
            WaitForInputIdle();
        }

        public void Dispose() {
            object pattern;
            if (Element.TryGetCurrentPattern(WindowPattern.Pattern, out pattern)) {
                try {
                    ((WindowPattern)pattern).Close();
                } catch (ElementNotAvailableException) {
                }
            }
        }

        /// <summary>
        /// Checks the Profile Project radio box
        /// </summary>
        public void SelectProfileProject() {
            Select(FindByAutomationId("ProfileProject"));
        }

        /// <summary>
        /// Checks the Profile Script radio box
        /// </summary>
        public void SelectProfileScript() {
            var elem = FindByAutomationId("ProfileScript");
            var pats = elem.GetSupportedPatterns();
            string[] names = new string[pats.Length];
            for (int i = 0; i < pats.Length; i++) {
                names[i] = pats[i].ProgrammaticName;
            }
            Select(FindByAutomationId("ProfileScript"));
        }

        public string SelectedProject {
            get {
                return SelectedProjectComboBox.GetSelectedItemName();
            }
        }

        public ComboBox SelectedProjectComboBox {
            get {
                return new ComboBox(FindByAutomationId("Project"));
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
                return new ComboBox(FindByAutomationId("Standalone.Interpreter"));
            }
        }

        /// <summary>
        /// Returns the string the user entered into the interpreter combo box.
        /// </summary>
        public string InterpreterPath {
            get {
                return InterpreterPathTextBox.GetValue();
            }
        }

        private AutomationWrapper InterpreterPathTextBox {
            get {
                return new AutomationWrapper(FindByAutomationId("Standalone.InterpreterPath"));
            }
        }

        public string WorkingDir
        {
            get {
                return WorkingDirectoryTextBox.GetValue();
            }
            set {
                WorkingDirectoryTextBox.SetValue(value);
            }
        }

        private AutomationWrapper WorkingDirectoryTextBox {
            get {
                return new AutomationWrapper(FindByAutomationId("Standalone.WorkingDirectory"));
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
                return new AutomationWrapper(FindByAutomationId("Standalone.ScriptPath"));
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
                return new AutomationWrapper(FindByAutomationId("Standalone.Arguments"));
            }
        }

        public void Ok() {
            WaitForInputIdle();
            WaitForClosed(TimeSpan.FromSeconds(10.0), () => ClickButtonByAutomationId("OK"));
        }

        public void Cancel() {
            WaitForInputIdle();
            WaitForClosed(TimeSpan.FromSeconds(10.0), () => ClickButtonByAutomationId("Cancel"));
        }
    }
}
