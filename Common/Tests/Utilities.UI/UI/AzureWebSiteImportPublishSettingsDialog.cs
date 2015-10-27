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
    public class AzureWebSiteImportPublishSettingsDialog : AutomationDialog {
        public AzureWebSiteImportPublishSettingsDialog(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public void ClickImportFromWindowsAzureWebSite() {
            WaitForInputIdle();
            ImportFromWindowsAzureWebSiteRadioButton().Select();
        }

        public void ClickSignOut() {
            WaitForInputIdle();
            var sign = new AutomationWrapper(FindByAutomationId("AzureSigninControl"));
            sign.ClickButtonByName("Sign Out");
        }

        public AzureManageSubscriptionsDialog ClickImportOrManageSubscriptions() {
            WaitForInputIdle();
            var importElement = ImportSubscriptionsHyperlink();
            if (importElement == null) {
                importElement = ManageSubscriptionsHyperlink();
            }
            importElement.GetInvokePattern().Invoke();
            return new AzureManageSubscriptionsDialog(App, AutomationElement.FromHandle(App.WaitForDialogToReplace(Element)));
        }

        public AzureWebSiteCreateDialog ClickNew() {
            WaitForInputIdle();
            ClickButtonByAutomationId("NewButton");
            return new AzureWebSiteCreateDialog(App, AutomationElement.FromHandle(App.WaitForDialogToReplace(Element)));
        }

        public void ClickOK() {
            WaitForInputIdle();
            WaitForClosed(TimeSpan.FromSeconds(10.0), () => ClickButtonByAutomationId("OKButton"));
        }

        private AutomationElement ImportFromWindowsAzureWebSiteRadioButton() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "ImportLabel"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton)
                )
            );
        }

        private AutomationElement ImportSubscriptionsHyperlink() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "Import subscriptions"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)
                )
            );
        }

        private AutomationElement ManageSubscriptionsHyperlink() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "Manage subscriptions"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)
                )
            );
        }
    }
}
