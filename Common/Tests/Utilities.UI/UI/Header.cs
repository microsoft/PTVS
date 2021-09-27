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
    public class Header : AutomationWrapper
    {
        private Dictionary<string, int> _columns = new Dictionary<string, int>();
        public Dictionary<string, int> Columns
        {
            get
            {
                return _columns;
            }
        }

        public Header(AutomationElement element) : base(element)
        {
            AutomationElementCollection headerItems = FindAllByControlType(ControlType.HeaderItem);
            for (int i = 0; i < headerItems.Count; i++)
            {
                string colName = headerItems[i].GetCurrentPropertyValue(AutomationElement.NameProperty) as string;
                if (colName != null && !_columns.ContainsKey(colName)) _columns[colName] = i;
            }
        }

        public int this[string colName]
        {
            get
            {
                Assert.IsTrue(_columns.ContainsKey(colName), "Header does not define header item {0}", colName);
                return _columns[colName];
            }
        }

    }
}
