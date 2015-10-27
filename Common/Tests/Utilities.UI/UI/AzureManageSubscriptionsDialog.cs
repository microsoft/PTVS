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
    public class AzureManageSubscriptionsDialog : AutomationDialog {
        public AzureManageSubscriptionsDialog(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public void ClickCertificates() {
            WaitForInputIdle();
            CertificatesTab().Select();
        }

        public AzureImportSubscriptionDialog ClickImport() {
            WaitForInputIdle();
            ClickButtonByAutomationId("ImportButton");

            return new AzureImportSubscriptionDialog(App, AutomationElement.FromHandle(App.WaitForDialogToReplace(Element)));
        }

        public void ClickRemove() {
            WaitForInputIdle();
            var button = new Button(FindByAutomationId("DeleteButton"));
            WaitFor(button, btn => btn.Element.Current.IsEnabled);
            button.Click();
        }

        public void Close() {
            WaitForInputIdle();
            WaitForClosed(TimeSpan.FromSeconds(10.0), () => ClickButtonByName("Close"));
        }

        public ListBox SubscriptionsListBox {
            get {
                return new ListBox(FindByAutomationId("SubscriptionsListBox"));
            }
        }

        private AutomationElement CertificatesTab() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "CertificatesTab"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem)
                )
            );
        }
    }
}
