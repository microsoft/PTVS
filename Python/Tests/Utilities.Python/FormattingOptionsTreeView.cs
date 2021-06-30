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

namespace TestUtilities.UI.Python {
    class FormattingOptionsTreeView : TreeView {
        public FormattingOptionsTreeView(AutomationElement element)
            : base(element) {
        }

        public static FormattingOptionsTreeView FromDialog(ToolsOptionsDialog dialog) {
            dialog.WaitForInputIdle();
            var spacingViewElement = dialog.FindByAutomationId("_optionsTree");
            for (int retries = 10; retries > 0 && spacingViewElement == null; retries -= 1) {
                Thread.Sleep(100);
                dialog.WaitForInputIdle();
                spacingViewElement = dialog.FindByAutomationId("_optionsTree");
            }

            return new FormattingOptionsTreeView(spacingViewElement);
        }
    }
}
