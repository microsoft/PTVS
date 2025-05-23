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

using System.Windows.Automation;
using Microsoft.PythonTools;

namespace TestUtilities.UI.Python {
    public class PythonUntrustedWorkspaceInfoBar : AutomationWrapper {
        public PythonUntrustedWorkspaceInfoBar(AutomationElement element)
            : base(element) {
        }

        // Default to allways trust the workspace
        public static Condition FindCondition = new AndCondition(
            new PropertyCondition(AutomationElement.NameProperty, Strings.AlwaysTrustWorkspaceInfoBarAction),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)
        );
    }
}
