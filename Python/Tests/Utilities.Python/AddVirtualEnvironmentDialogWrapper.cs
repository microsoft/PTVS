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
    public class AddVirtualEnvironmentDialogWrapper : AddEnvironmentDialogWrapperBase {
        public AddVirtualEnvironmentDialogWrapper(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public static AddVirtualEnvironmentDialogWrapper FromDte(VisualStudioApp app) {
            return new AddVirtualEnvironmentDialogWrapper(
                app,
                AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Python.AddVirtualEnvironment"))
            );
        }

        public string Location => FindByAutomationId("Location").Current.Name;

        public string EnvName {
            get => new TextBox(FindByAutomationId("EnvName")).GetValue();
            set => new TextBox(FindByAutomationId("EnvName")).SetValue(value);
        }

        public string RequirementsPath {
            get => new TextBox(FindByAutomationId("RequirementsPath")).GetValue();
            set => new TextBox(FindByAutomationId("RequirementsPath")).SetValue(value);
        }

        public string BaseInterpreter {
            get => new ComboBox(FindByAutomationId("BaseInterpreter")).GetSelectedItemName();
            set => new ComboBox(FindByAutomationId("BaseInterpreter")).SelectItem(value);
        }

        public bool SetAsDefault {
            get => new CheckBox(FindByAutomationId("SetAsDefault")).ToggleState == ToggleState.On;
            set => new CheckBox(FindByAutomationId("SetAsDefault")).ToggleState = value ? ToggleState.On : ToggleState.Off;
        }

        public bool SetAsCurrent {
            get => new CheckBox(FindByAutomationId("SetAsCurrent")).ToggleState == ToggleState.On;
            set => new CheckBox(FindByAutomationId("SetAsCurrent")).ToggleState = value ? ToggleState.On : ToggleState.Off;
        }

        public bool ViewInEnvironmentWindow {
            get => new CheckBox(FindByAutomationId("ViewInEnvironmentWindow")).ToggleState == ToggleState.On;
            set => new CheckBox(FindByAutomationId("ViewInEnvironmentWindow")).ToggleState = value ? ToggleState.On : ToggleState.Off;
        }

        public bool RegisterGlobally {
            get => new CheckBox(FindByAutomationId("RegisterGlobally")).ToggleState == ToggleState.On;
            set => new CheckBox(FindByAutomationId("RegisterGlobally")).ToggleState = value ? ToggleState.On : ToggleState.Off;
        }
    }
}
