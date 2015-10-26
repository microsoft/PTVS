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
    public class AzureImportSubscriptionDialog : AutomationDialog {
        public AzureImportSubscriptionDialog(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public void ClickImport() {
            WaitForInputIdle();
            WaitForClosed(TimeSpan.FromSeconds(10.0), () => ClickButtonByAutomationId("OKButton"));
        }

        public string FileName {
            get {
                return GetFileNameBox().GetValuePattern().Current.Value;
            }
            set {
                GetFileNameBox().GetValuePattern().SetValue(value);
            }
        }

        private AutomationElement GetFileNameBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "PublishSettingsFileTextBox"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)
                )
            );
        }
    }
}
