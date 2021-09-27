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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace TestUtilities.UI
{
    public class ListBox : AutomationWrapper
    {
        public ListBox(AutomationElement element)
            : base(element)
        {
        }

        public ListBoxItem this[int index]
        {
            get
            {
                var items = FindAllByControlType(ControlType.ListItem);
                Assert.IsTrue(0 <= index && index < items.Count, "Index {0} is out of range of item count {1}", index, items.Count);
                return new ListBoxItem(items[index], this);
            }
        }

        public int Count
        {
            get
            {
                var items = FindAllByControlType(ControlType.ListItem);
                return items.Count;
            }
        }
    }
}
