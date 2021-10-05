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
	public class AttachToProcessDialog : AutomationWrapper
	{
		private ListView _processList;
		private int _hwnd;

		public AttachToProcessDialog(AutomationElement element) : base(element) { _hwnd = element.Current.NativeWindowHandle; }

		public AttachToProcessDialog(IntPtr hwnd) : this(AutomationElement.FromHandle(hwnd)) { _hwnd = (int)hwnd; }

		public SelectCodeTypeDialog SelectCodeTypeForDebugging()
		{
			ThreadPool.QueueUserWorkItem(x =>
			{
				try
				{
					ClickSelect();
				}
				catch (Exception e)
				{
					Assert.Fail("Unexpected Exception - ClickSelect(){0}{1}", Environment.NewLine, e.ToString());
				}
			});
			AutomationElement sctel = FindByName("Select Code Type");
			Assert.IsNotNull(sctel, "Could not find the Select Code Type dialog!");
			return new SelectCodeTypeDialog(sctel);
		}

		public void ClickSelect()
		{
			ClickButtonByAutomationId("4103"); // AutomationId discovered with UISpy
		}

		public void ClickAttach()
		{
			ClickButtonByName("Attach"); // AutomationId discovered with UISpy
		}

		public void ClickCancel()
		{
			ClickButtonByName("Cancel");
		}

		public void SelectProcessForDebuggingByPid(int pid)
		{
			Select(_processList.GetFirstByColumnNameAndValue("ID", pid.ToString()).Element);
		}

		public void SelectProcessForDebuggingByName(string name)
		{
			Select(_processList.GetFirstByColumnNameAndValue("Process", name).Element);
		}

		// Available Processes list: AutomationId 4102
		public ListView ProcessList
		{
			get
			{
				if (_processList == null)
				{
					var plElement = Element.FindFirst(
						TreeScope.Descendants,
						new PropertyCondition(
							AutomationElement.AutomationIdProperty,
							"4102"));
					_processList = new ListView(plElement);
				}
				return _processList;
			}
		}
	}
}
