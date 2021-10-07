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
	public class CheckListView : AutomationWrapper
	{
		private List<CheckBox> _items;
		private Header _header;

		public Header Header
		{
			get
			{
				if (_header == null)
				{
					var headerel = FindFirstByControlType(ControlType.Header);
					if (headerel != null)
					{
						_header = new Header(FindFirstByControlType(ControlType.Header));
					}
				}
				return _header;
			}
		}

		public List<CheckBox> Items
		{
			get
			{
				if (_items == null)
				{
					_items = new List<CheckBox>();
					AutomationElementCollection rawItems = FindAllByControlType(ControlType.CheckBox);
					foreach (AutomationElement el in rawItems)
					{
						_items.Add(new CheckBox(el));
					}
				}
				return _items;
			}
		}

		public CheckListView(AutomationElement element) : base(element) { }

		public CheckBox GetFirstByName(string name)
		{
			foreach (CheckBox r in Items)
			{
				if (r.Name.Equals(name, StringComparison.CurrentCulture))
				{
					return r;
				}
			}
			Assert.Fail("No item found with Name == {0}", name);
			return null;
		}

	}
}
