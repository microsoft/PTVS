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
using System.Threading;
using System.Windows.Automation;

namespace TestUtilities.UI {
    public class AzureCloudServiceActivityLog : AutomationWrapper {
        public AzureCloudServiceActivityLog(AutomationElement element)
            : base(element) {
        }

        public void WaitForPublishComplete(string serviceName, int timeout) {
            var rowElement = WaitForRow(serviceName, 5000);
            WaitForRowComplete(rowElement, timeout);
        }

        private AutomationElement WaitForRow(string serviceName, int timeout) {
            const int interval = 200;
            for (int i = 0; i < timeout; i += interval) {
                var res = FindRowElement(serviceName);
                if (res != null) {
                    return res;
                }

                Thread.Sleep(interval);
            }

            throw new TimeoutException("Timed out waiting for publish to start.");
        }

        private void WaitForRowComplete(AutomationElement row, int timeout) {
            const int interval = 1000;
            for (int i = 0; i < timeout; i += interval) {
                if (HasCompleted(row)) {
                    return;
                }

                Thread.Sleep(interval);
            }

            throw new TimeoutException("Timed out waiting for publish to complete.");
        }

        private AutomationElement FindRowElement(string serviceName) {
            var rows = Element.FindAll(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(
                    AutomationElement.ClassNameProperty,
                    "DataGridRow"
                ),

                new PropertyCondition(
                    AutomationElement.NameProperty,
                    "Microsoft.Cct.StatusWindow.DispatchedStatusItemContainer"
                )
            ));

            foreach (AutomationElement row in rows) {
                var columns = row.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ClassNameProperty, "DataGridCell")
                );

                foreach (AutomationElement column in columns) {
                    if (column.Current.Name.Contains(serviceName)) {
                        return row;
                    }
                }
            }

            return null;
        }

        private bool HasCompleted(AutomationElement row) {
            return row.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.NameProperty, "Completed"),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)
            )) != null;
        }
    }
}
