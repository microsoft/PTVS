// Visual Studio Shared Project
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
using System.Windows.Automation;

namespace TestUtilities.UI {
    public class AzureCloudServiceCreateDialog : AutomationDialog {
        public AzureCloudServiceCreateDialog(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public void ClickCreate() {
            // Wait for the create button to be enabled
            WaitFor(OkButton, btn => btn.Element.Current.IsEnabled);

            WaitForInputIdle();
            WaitForClosed(TimeSpan.FromSeconds(30.0), () => OkButton.Click());
        }

        public string ServiceName {
            get {
                return GetServiceNameBox().GetValuePattern().Current.Value;
            }
            set {
                WaitForInputIdle();
                GetServiceNameBox().GetValuePattern().SetValue(value);
            }
        }

        public string Location {
            get {
                return LocationComboBox.GetSelectedItemName();
            }
            set {
                WaitForInputIdle();
                WaitFor(LocationComboBox, combobox => combobox.GetSelectedItemName() != "<Loading...>");
                LocationComboBox.SelectItem(value);
            }
        }

        private Button OkButton {
            get {
                return new Button(FindByAutomationId("OkButton"));
            }
        }

        private ComboBox LocationComboBox {
            get {
                return new ComboBox(FindByAutomationId("LocationComboBox"));
            }
        }

        private AutomationElement GetServiceNameBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "ServiceNameTextBox"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)
                )
            );
        }
    }
}
