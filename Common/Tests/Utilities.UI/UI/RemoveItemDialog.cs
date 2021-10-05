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
	/// <summary>
	/// Wraps the Delete/Remove/Cancel dialog displayed when removing something from a hierarchy window (such as the solution explorer).
	/// </summary>
	public class RemoveItemDialog : AutomationDialog
	{
		public RemoveItemDialog(IntPtr hwnd)
			: base(null, AutomationElement.FromHandle(hwnd))
		{
		}

		public RemoveItemDialog(VisualStudioApp app, AutomationElement element)
			: base(app, element)
		{
		}

		public static RemoveItemDialog FromDte(VisualStudioApp app)
		{
			return new RemoveItemDialog(app, AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Edit.Delete")));
		}

		public override void OK()
		{
			throw new NotSupportedException();
		}

		public void Remove()
		{
			WaitForInputIdle();
			WaitForClosed(DefaultTimeout, () => ClickButtonByName("Remove"));
		}

		public void Delete()
		{
			WaitForInputIdle();
			WaitForClosed(DefaultTimeout, () => ClickButtonByName("Delete"));
		}
	}
}
