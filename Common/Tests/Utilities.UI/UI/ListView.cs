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
	public class ListView : AutomationWrapper
	{
		private List<ListItem> _items;
		private Header _header;

		public Header Header
		{
			get
			{
				if (_header == null)
				{
					var headerel = FindFirstByControlType(ControlType.Header);
					if (headerel != null)
						_header = new Header(FindFirstByControlType(ControlType.Header));
				}
				return _header;
			}
		}

		public List<ListItem> Items
		{
			get
			{
				if (_items == null)
				{
					_items = new List<ListItem>();
					AutomationElementCollection rawItems = FindAllByControlType(ControlType.ListItem);
					foreach (AutomationElement el in rawItems)
					{
						_items.Add(new ListItem(el, this));
					}
				}
				return _items;
			}
		}

		public ListView(AutomationElement element) : base(element) { }

		public ListItem GetFirstByColumnNameAndValue(string col, string val)
		{
			Assert.IsNotNull(Header, "No header defined for this list");
			return GetFirstByColumnIdAndValue(Header[col], val);
		}

		public ListItem GetFirstByColumnIdAndValue(int col, string val)
		{
			foreach (ListItem r in Items)
			{
				if (r[col].Equals(val, StringComparison.CurrentCulture)) return r;
			}
			Assert.Fail("No item found with column {0} == {1}", col, val);
			return null;
		}

		public ListItem FindItem(string name)
		{
			var res = Element.FindFirst(
				TreeScope.Children,
				new PropertyCondition(
					AutomationElement.NameProperty,
					name
				)
			);

			if (res != null)
			{
				return new ListItem(res, this);
			}

			return null;
		}
	}
}
