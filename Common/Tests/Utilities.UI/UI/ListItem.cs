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
	public class ListItem : AutomationWrapper
	{
		private readonly ListView _parent;
		private AutomationElementCollection _columns;
		public ListItem(AutomationElement element, ListView parent) : base(element)
		{
			_parent = parent;
			_columns = FindAllByControlType(ControlType.Text);
		}

		public string this[int index]
		{
			get
			{
				Assert.IsNotNull(_columns);
				Assert.IsTrue(0 <= index && index < _columns.Count, "Index {0} is out of range of column count {1}", index, _columns.Count);
				return _columns[index].GetCurrentPropertyValue(AutomationElement.NameProperty) as string;
			}
		}

		public string this[string columnName]
		{
			get
			{
				Assert.IsNotNull(_parent.Header, "Parent List does not define column headers!");
				return this[_parent.Header[columnName]];
			}
		}
	}
}
