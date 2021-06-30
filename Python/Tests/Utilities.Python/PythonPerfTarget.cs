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
using System.Windows.Automation;

namespace TestUtilities.UI.Python {
    public class PythonPerfTarget : AutomationWrapper, IDisposable {
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
