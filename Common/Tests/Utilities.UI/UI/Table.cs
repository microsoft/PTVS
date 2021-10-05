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
	public class Table : AutomationWrapper
	{
		private readonly GridPattern _pattern;

		public Table(AutomationElement element)
			: base(element)
		{
			_pattern = (GridPattern)element.GetCurrentPattern(GridPattern.Pattern);

		}

		public AutomationElement this[int row, int column]
		{
			get
			{
				return _pattern.GetItem(row, column);
			}
		}

		public AutomationElement FindItem(string name)
		{
			return Element.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, name));
		}
	}
}
