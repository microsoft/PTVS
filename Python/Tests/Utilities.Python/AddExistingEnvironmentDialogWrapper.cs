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

using System.Windows.Automation;

namespace TestUtilities.UI {
    public class AddExistingEnvironmentDialogWrapper : AddEnvironmentDialogWrapperBase {
        public AddExistingEnvironmentDialogWrapper(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public static AddExistingEnvironmentDialogWrapper FromDte(VisualStudioApp app) {
            return new AddExistingEnvironmentDialogWrapper(
                app,
                AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Python.AddExistingEnvironment"))
            );
        }

        public void SelectCustomInterpreter() {
            Interpreter = "<Custom>";
        }

        public bool IsCustomInterpreterSelected => Interpreter == "<Custom>";

        public string Interpreter {
            get => new ComboBox(FindByAutomationId("Interpreter")).GetSelectedItemName();
            set => new ComboBox(FindByAutomationId("Interpreter")).SelectItem(value);
        }

        public string Description {
            get => new TextBox(FindByAutomationId("Description")).GetValue();
            set => new TextBox(FindByAutomationId("Description")).SetValue(value);
        }

        public string PrefixPath {
            get => new TextBox(FindByAutomationId("PrefixPath")).GetValue();
            set => new TextBox(FindByAutomationId("PrefixPath")).SetValue(value);
        }

        public string InterpreterPath {
            get => new TextBox(FindByAutomationId("InterpreterPath")).GetValue();
            set => new TextBox(FindByAutomationId("InterpreterPath")).SetValue(value);
        }

        public string WindowsInterpreterPath {
            get => new TextBox(FindByAutomationId("WindowsInterpreterPath")).GetValue();
            set => new TextBox(FindByAutomationId("WindowsInterpreterPath")).SetValue(value);
        }

        public string LanguageVersion {
            get => new ComboBox(FindByAutomationId("LanguageVersion")).GetSelectedItemName();
            set => new ComboBox(FindByAutomationId("LanguageVersion")).SelectItem(value);
        }

        public string Architecture {
            get => new ComboBox(FindByAutomationId("Architecture")).GetSelectedItemName();
            set => new ComboBox(FindByAutomationId("Architecture")).SelectItem(value);
        }

        public string PathEnvironmentVariable {
            get => new TextBox(FindByAutomationId("PathEnvironmentVariable")).GetValue();
            set => new TextBox(FindByAutomationId("PathEnvironmentVariable")).SetValue(value);
        }

        public bool RegisterGlobally {
            get => new CheckBox(FindByAutomationId("RegisterGlobally")).ToggleState == ToggleState.On;
            set => new CheckBox(FindByAutomationId("RegisterGlobally")).ToggleState = value ? ToggleState.On : ToggleState.Off;
        }
    }
}
