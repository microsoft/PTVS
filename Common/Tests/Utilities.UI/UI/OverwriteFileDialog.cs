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
	public class OverwriteFileDialog : AutomationDialog, IOverwriteFile
	{
		private OverwriteFileDialog(VisualStudioApp app, AutomationElement element)
			: base(app, element)
		{
		}

		public static OverwriteFileDialog Wait(VisualStudioApp app)
		{
			var hwnd = app.WaitForDialog();
			Assert.AreNotEqual(IntPtr.Zero, hwnd, "Did not find OverwriteFileDialog");
			var element = AutomationElement.FromHandle(hwnd);

			try
			{
				Assert.IsNotNull(element.FindFirst(
					TreeScope.Descendants,
					new PropertyCondition(AutomationElement.AutomationIdProperty, "_allItems")
				), "Not correct dialog - missing '_allItems'");
				Assert.IsNotNull(element.FindFirst(
					TreeScope.Descendants,
					new PropertyCondition(AutomationElement.AutomationIdProperty, "_yes")
				), "Not correct dialog - missing '_yes'");

				OverwriteFileDialog res = new OverwriteFileDialog(app, element);
				element = null;
				return res;
			}
			finally
			{
				if (element != null)
				{
					AutomationWrapper.DumpElement(element);
				}
			}
		}

		public override void OK()
		{
			ClickButtonAndClose("_yes", nameIsAutomationId: true);
		}

		public void No()
		{
			ClickButtonAndClose("_no", nameIsAutomationId: true);
		}

		public void Yes()
		{
			OK();
		}

		public override void Cancel()
		{
			ClickButtonAndClose("_cancel", nameIsAutomationId: true);
		}


		public bool AllItems
		{
			get => FindByAutomationId("_allItems").GetTogglePattern().Current.ToggleState == ToggleState.On;
			set
			{
				if (AllItems)
				{
					if (!value)
					{
						FindByAutomationId("_allItems").GetTogglePattern().Toggle();
					}
				}
				else
				{
					if (value)
					{
						FindByAutomationId("_allItems").GetTogglePattern().Toggle();
					}
				}
			}
		}


		public override string Text => FindByAutomationId("_message").GetValuePattern().Current.Value;
	}
}
