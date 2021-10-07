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
	public class ToolsOptionsDialog : AutomationDialog
	{
		public ToolsOptionsDialog(VisualStudioApp app, AutomationElement element)
			: base(app, element)
		{
		}

		public static ToolsOptionsDialog FromDte(VisualStudioApp app)
		{
			return new ToolsOptionsDialog(
				app,
				AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Tools.Options"))
			);
		}

		public override void OK()
		{
			ClickButtonAndClose("1", nameIsAutomationId: true);
		}

		public override void Cancel()
		{
			ClickButtonAndClose("2", nameIsAutomationId: true);
		}

		public string SelectedView
		{
			set
			{
				TreeView treeView = new TreeView(Element.FindFirst(
					TreeScope.Descendants,
					new PropertyCondition(AutomationElement.ClassNameProperty, "SysTreeView32")
				));

				treeView.FindItem(value.Split('\\', '/')).SetFocus();
			}
		}
	}
}
