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

namespace Microsoft.PythonTools.EnvironmentsList {
    sealed class DataListBox : ListBox {
        protected override AutomationPeer OnCreateAutomationPeer() {
            return new DataListBoxAutomationPeer(this);
        }

        sealed class DataListBoxAutomationPeer : ListBoxAutomationPeer {
            public DataListBoxAutomationPeer(ListBox owner) : base(owner) { }

            protected override ItemAutomationPeer CreateItemAutomationPeer(object item) {
                return new DataListBoxItemAutomationPeer(item, this);
            }
        }

        sealed class DataListBoxItemAutomationPeer : ListBoxItemAutomationPeer {
            public DataListBoxItemAutomationPeer(object owner, SelectorAutomationPeer selectorAutomationPeer) : base(owner, selectorAutomationPeer) {
            }

            protected override string GetClassNameCore() {
                return "DataListBoxItem";
            }

            protected override AutomationControlType GetAutomationControlTypeCore() {
                return AutomationControlType.DataItem;
            }
        }
    }
}
