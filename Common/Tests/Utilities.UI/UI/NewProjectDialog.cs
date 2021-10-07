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
	/// Wrapps VS's File->New Project dialog.
	/// </summary>
	public class NewProjectDialog : AutomationDialog
	{
		private TreeView _installedTemplates;
		private ListView _projectTypesTable;

		public NewProjectDialog(VisualStudioApp app, AutomationElement element)
			: base(app, element)
		{
		}

		public static NewProjectDialog FromDte(VisualStudioApp app)
		{
			return app.FileNewProject();
		}

		public override void OK()
		{
			ClickButtonAndClose("btn_OK", nameIsAutomationId: true);
		}

		/// <summary>
		/// Gets the installed templates tree view which enables access to all of the project types.
		/// </summary>
		public TreeView InstalledTemplates
		{
			get
			{
				if (_installedTemplates == null)
				{
					var templates = Element.FindAll(
						TreeScope.Descendants,
						new PropertyCondition(
							AutomationElement.AutomationIdProperty,
#if DEV11_OR_LATER
                            "Installed"
#else
							"Installed Templates"
#endif
						)
					);


					// all the templates have the same name (Installed, Recent, etc...)
					// so we need to find the one that actually has our templates.
					foreach (AutomationElement template in templates)
					{
						TreeView temp = new TreeView(template);
#if DEV11_OR_LATER
                        var item = temp.FindItem("Templates");
#else
						var item = temp.FindItem("Visual C#");
#endif
						if (item != null)
						{
							_installedTemplates = temp;
							break;
						}
					}
				}
				return _installedTemplates;
			}
		}

		/// <summary>
		/// Gets the project types table which enables selecting an individual project type.
		/// </summary>
		public ListView ProjectTypes
		{
			get
			{
				if (_projectTypesTable == null)
				{
					var extensions = Element.FindAll(
						TreeScope.Descendants,
						new PropertyCondition(
							AutomationElement.AutomationIdProperty,
							"lvw_Extensions"
						)
					);

					if (extensions.Count != 1)
					{
						throw new Exception("multiple controls match");
					}
					_projectTypesTable = new ListView(extensions[0]);

				}
				return _projectTypesTable;
			}
		}

		public string ProjectName
		{
			get => ProjectNameBox.GetValuePattern().Current.Value;
			set => ProjectNameBox.GetValuePattern().SetValue(value);
		}

		public string Location
		{
			get => LocationBox.GetValuePattern().Current.Value;
			set => LocationBox.GetValuePattern().SetValue(value);
		}

		public void FocusLanguageNode(string name = "Python")
		{
			if (InstalledTemplates == null)
			{
				Console.WriteLine("Failed to find InstalledTemplates:");
				AutomationWrapper.DumpElement(Element);
			}
#if DEV11_OR_LATER
            var item = InstalledTemplates.FindItem("Templates", name);
            if (item == null) {
                item = InstalledTemplates.FindItem("Templates", "Other Languages", name);
            }
#else
			var item = InstalledTemplates.FindItem("Other Languages", name);
			if (item == null)
			{
				// VS can be configured so that there is no Other Languages category
				item = InstalledTemplates.FindItem(name);
			}
#endif
			if (item == null)
			{
				Console.WriteLine("Failed to find templates for " + name);
				AutomationWrapper.DumpElement(InstalledTemplates.Element);
			}
			item.SetFocus();
		}

		private AutomationElement ProjectNameBox => Element.FindFirst(TreeScope.Descendants,
					new AndCondition(
						new PropertyCondition(AutomationElement.AutomationIdProperty, "txt_Name"),
						new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)
					)
				);

		private AutomationElement LocationBox => Element.FindFirst(TreeScope.Descendants,
					new AndCondition(
						new PropertyCondition(AutomationElement.AutomationIdProperty, "PART_EditableTextBox"),
						new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)
					)
				);
	}
}
