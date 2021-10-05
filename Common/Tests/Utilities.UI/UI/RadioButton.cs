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
	public class RadioButton : AutomationWrapper
	{
		public string Name { get; set; }

		public RadioButton(AutomationElement element)
			: base(element)
		{
			Name = (string)Element.GetCurrentPropertyValue(AutomationElement.NameProperty);
		}

		public void SetSelected()
		{
			Assert.IsTrue((bool)Element.GetCurrentPropertyValue(AutomationElement.IsSelectionItemPatternAvailableProperty), "Element is not a radio button");
			var pattern = (SelectionItemPattern)Element.GetCurrentPattern(SelectionItemPattern.Pattern);
			pattern.Select();

			Assert.IsTrue(pattern.Current.IsSelected);
		}

		public bool IsSelected
		{
			get
			{
				Assert.IsTrue((bool)Element.GetCurrentPropertyValue(AutomationElement.IsSelectionItemPatternAvailableProperty), "Element is not a radio button");
				var pattern = (SelectionItemPattern)Element.GetCurrentPattern(SelectionItemPattern.Pattern);
				return pattern.Current.IsSelected;
			}
		}
	}
}
