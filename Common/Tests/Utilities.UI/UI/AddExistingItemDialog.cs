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
	public class AddExistingItemDialog : AutomationDialog, IAddExistingItem
	{
		public AddExistingItemDialog(VisualStudioApp app, AutomationElement element)
			: base(app, element)
		{
		}

		public static AddExistingItemDialog FromDte(VisualStudioApp app)
		{
			return new AddExistingItemDialog(
				app,
				AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("Project.AddExistingItem"))
			);
		}

		public override void OK()
		{
			Add();
		}

		public void Add()
		{
			WaitForClosed(DefaultTimeout, () => Keyboard.PressAndRelease(Key.A, Key.LeftAlt));
		}

		public void AddLink()
		{
			var addButton = Element.FindFirst(TreeScope.Children,
				new AndCondition(
					new PropertyCondition(AutomationElement.NameProperty, "Add"),
					new PropertyCondition(AutomationElement.ClassNameProperty, "Button")
				)
			);

			// click the chevron to open the menu
			var bottomRight = addButton.Current.BoundingRectangle.BottomRight;
			Mouse.MoveTo(new Point(bottomRight.X - 3, bottomRight.Y - 3));

			Mouse.Click(MouseButton.Left);

			// type the keyboard short cut for Add to Link
			Keyboard.Type(Key.L);

			WaitForClosed(DefaultTimeout);
		}

		public string FileName
		{
			get => GetFilenameEditBox().GetValuePattern().Current.Value;
			set
			{
				for (int retries = 10; retries > 0 && FileName != value; --retries)
				{
					GetFilenameEditBox().GetValuePattern().SetValue(value);
					Thread.Sleep(100);
				}
			}
		}

		private AutomationElement GetFilenameEditBox()
		{
			return Element.FindFirst(TreeScope.Descendants,
				new AndCondition(
					new PropertyCondition(AutomationElement.ClassNameProperty, "Edit"),
					new PropertyCondition(AutomationElement.NameProperty, "File name:")
				)
			);
		}
	}
}
