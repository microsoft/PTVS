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

using System.Threading;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities.UI {
    public class AddEnvironmentDialogWrapperBase : AutomationDialog {
        public AddEnvironmentDialogWrapperBase(VisualStudioApp app, AutomationElement element)
            : base(app, element) {
        }

        public void WaitForReady() {
            var button = FindByAutomationId("AddButton");
            for (int i = 0; i < 20 && !button.Current.IsEnabled; i++) {
                Thread.Sleep(500);
            }

            Assert.IsTrue(button.Current.IsEnabled, "Timed out waiting for add button to be enabled.");
        }

        public void ClickAdd() => ClickButtonByAutomationId("AddButton");
    }
}
