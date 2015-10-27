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
    public class AzureWebSiteCreateDialog : AutomationDialog {
        public AzureWebSiteCreateDialog(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public void ClickCreate() {
            // Wait for the create button to be enabled
            WaitFor(CreateButton, btn => btn.Element.Current.IsEnabled);

            // Wait for Locations and Databases to have a selection
            // (the create button may be enabled before they are populated)
            WaitFor(LocationComboBox, combobox => combobox.GetSelectedItemName() != null);
            WaitFor(DatabaseComboBox, combobox => combobox.GetSelectedItemName() != null);

            WaitForInputIdle();
            WaitForClosed(TimeSpan.FromSeconds(180.0), () => CreateButton.Click());
        }

        public string SiteName {
            get {
                return GetSiteNameBox().GetValuePattern().Current.Value;
            }
            set {
                WaitForInputIdle();
                GetSiteNameBox().GetValuePattern().SetValue(value);
            }
        }

        private Button CreateButton {
            get {
                return new Button(FindByName("Create"));
            }
        }

        private ComboBox LocationComboBox {
            get {
                return new ComboBox(FindByAutomationId("_azureSiteLocation"));
            }
        }

        private ComboBox DatabaseComboBox {
            get {
                return new ComboBox(FindByAutomationId("_azureDatabaseServer"));
            }
        }

        private AutomationElement GetSiteNameBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "_azureSiteName"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)
                )
            );
        }
    }
}
