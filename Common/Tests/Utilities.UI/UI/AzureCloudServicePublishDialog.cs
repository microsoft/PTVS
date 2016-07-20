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
using System.IO;
using System.Threading.Tasks;
using System.Windows.Automation;
using Microsoft.VisualStudio.Shell.Interop;

namespace TestUtilities.UI {
    public class AzureCloudServicePublishDialog : AutomationDialog {
        public AzureCloudServicePublishDialog(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public static AzureCloudServicePublishDialog FromDte(VisualStudioApp app) {
            var publishDialogHandle = app.OpenDialogWithDteExecuteCommand("Build.PublishSelection");
            return new AzureCloudServicePublishDialog(app, AutomationElement.FromHandle(publishDialogHandle));
        }

        public AzureManageSubscriptionsDialog SelectManageSubscriptions() {
            WaitForInputIdle();

            // <Manage...> is different from other list item, selecting it 
            // using SelectItem will throw COMException
            // This is what the cloud service team did in their tests for their 
            // combo box special items that bring up dialogs.
            AccountComboBox.ClickItem("<Manage...>");

            return new AzureManageSubscriptionsDialog(App, AutomationElement.FromHandle(App.WaitForDialogToReplace(Element)));
        }

        public AzureCloudServiceCreateDialog SelectCreateNewService() {
            WaitForInputIdle();

            // <Create New...> is different from other list item, selecting it 
            // using SelectItem will throw COMException
            // This is what the cloud service team did in their tests for their 
            // combo box special items that bring up dialogs.
            ServiceComboBox.ClickItem("<Create New...>");

            return new AzureCloudServiceCreateDialog(App, AutomationElement.FromHandle(App.WaitForDialogToReplace(Element)));
        }

        public void ClickPublish() {
            // Wait for the publish button to be enabled
            // It will not be enabled until all combo boxes have a valid
            // selection, such as the storage account combo box.
            WaitFor(PublishButton, btn => btn.Element.Current.IsEnabled);

            WaitForInputIdle();
            WaitForClosed(TimeSpan.FromSeconds(10.0), () => PublishButton.Click());
        }

        public void ClickNext() {
            WaitForInputIdle();
            WaitForClosed(TimeSpan.FromSeconds(10.0), () => ClickButtonByAutomationId("PART_NextCommandSource"));
        }

        private Button PublishButton {
            get {
                return new Button(FindByAutomationId("PART_FinishCommandSource"));
            }
        }

        private ComboBox AccountComboBox {
            get {
                return new ComboBox(FindByAutomationId("accountCombo"));
            }
        }

        private ComboBox ServiceComboBox {
            get {
                return new ComboBox(FindByAutomationId("ServiceComboBox"));
            }
        }
    }
}
