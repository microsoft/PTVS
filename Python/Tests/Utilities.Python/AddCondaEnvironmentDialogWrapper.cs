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
using System.Threading;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities.UI {
    public class AddCondaEnvironmentDialogWrapper : AddEnvironmentDialogWrapperBase {
        public AddCondaEnvironmentDialogWrapper(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public static AddCondaEnvironmentDialogWrapper FromDte(VisualStudioApp app) {
            return new AddCondaEnvironmentDialogWrapper(
                app,
                AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Python.AddCondaEnvironment"))
            );
        }

        public string EnvName {
            get => new TextBox(FindByAutomationId("EnvName")).GetValue();
            set => new TextBox(FindByAutomationId("EnvName")).SetValue(value);
        }

        public string Packages {
            get => new TextBox(FindByAutomationId("Packages")).GetValue();
            set => new TextBox(FindByAutomationId("Packages")).SetValue(value);
        }

        public string EnvFile {
            get => new TextBox(FindByAutomationId("EnvFile")).GetValue();
            set => new TextBox(FindByAutomationId("EnvFile")).SetValue(value);
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

        public void SetPackagesMode() => new RadioButton(FindByAutomationId("PackagesMode")).SetSelected();

        public void SetEnvFileMode() => new RadioButton(FindByAutomationId("EnvFileMode")).SetSelected();

        public bool IsPackagesMode => new RadioButton(FindByAutomationId("PackagesMode")).IsSelected;

        public bool IsEnvFileMode => new RadioButton(FindByAutomationId("EnvFileMode")).IsSelected;

        public AutomationWrapper WaitForPreviewPackage(string packageName, TimeSpan timeout) {
            var previewResult = FindByAutomationId("PreviewResult");
            for (int i = 0; i < timeout.TotalMilliseconds; i += 100) {
                AutomationElement element = previewResult.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, packageName));
                if (element != null && !element.Current.IsOffscreen) {
                    return new AutomationWrapper(element);
                }
                Thread.Sleep(100);
            }

            Assert.Fail($"Could not find '{packageName}' in preview result.");
            return null;
        }
    }
}
