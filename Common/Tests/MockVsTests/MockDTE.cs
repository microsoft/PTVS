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

namespace Microsoft.VisualStudioTools.MockVsTests
{
	internal class MockDTE : EnvDTE.DTE
	{
		internal readonly MockVs _vs;

		public MockDTE(MockVs vs)
		{
			_vs = vs;
		}

		public Document ActiveDocument => throw new NotImplementedException();

		public object ActiveSolutionProjects => throw new NotImplementedException();

		public Window ActiveWindow => throw new NotImplementedException();

		public AddIns AddIns => throw new NotImplementedException();

		public DTE Application => throw new NotImplementedException();

		public object CommandBars => throw new NotImplementedException();

		public string CommandLineArguments => throw new NotImplementedException();

		public Commands Commands => throw new NotImplementedException();

		public ContextAttributes ContextAttributes => throw new NotImplementedException();

		public Debugger Debugger => throw new NotImplementedException();

		public vsDisplay DisplayMode
		{
			get => throw new NotImplementedException();

			set => throw new NotImplementedException();
		}

		public Documents Documents => throw new NotImplementedException();

		public DTE DTE => this;

		public string Edition => throw new NotImplementedException();

		public Events Events => throw new NotImplementedException();

		public string FileName => throw new NotImplementedException();

		public Find Find => throw new NotImplementedException();

		public string FullName => throw new NotImplementedException();

		public Globals Globals => throw new NotImplementedException();

		public ItemOperations ItemOperations => throw new NotImplementedException();

		public int LocaleID => throw new NotImplementedException();

		public Macros Macros => throw new NotImplementedException();

		public DTE MacrosIDE => throw new NotImplementedException();

		public Window MainWindow => throw new NotImplementedException();

		public vsIDEMode Mode => throw new NotImplementedException();

		public string Name => throw new NotImplementedException();

		public ObjectExtenders ObjectExtenders => throw new NotImplementedException();

		public string RegistryRoot => throw new NotImplementedException();

		public SelectedItems SelectedItems => throw new NotImplementedException();

		public Solution Solution => new MockDTESolution(this);

		public SourceControl SourceControl => throw new NotImplementedException();

		public StatusBar StatusBar => throw new NotImplementedException();

		public bool SuppressUI
		{
			get => throw new NotImplementedException();

			set => throw new NotImplementedException();
		}

		public UndoContext UndoContext => throw new NotImplementedException();

		public bool UserControl
		{
			get => throw new NotImplementedException();

			set => throw new NotImplementedException();
		}

		public string Version => throw new NotImplementedException();

		public WindowConfigurations WindowConfigurations => throw new NotImplementedException();

		public EnvDTE.Windows Windows => throw new NotImplementedException();

		public void ExecuteCommand(string CommandName, string CommandArgs = "")
		{
			throw new NotImplementedException();
		}

		public object GetObject(string Name)
		{
			throw new NotImplementedException();
		}

		public bool get_IsOpenFile(string ViewKind, string FileName)
		{
			throw new NotImplementedException();
		}

		public Properties get_Properties(string Category, string Page)
		{
			Properties res;
			if (_properties.TryGetValue(Category, out Dictionary<global::System.String, Properties> pages) &&
				pages.TryGetValue(Page, out res))
			{
				return res;
			}
			return null;
		}

		public wizardResult LaunchWizard(string VSZFile, ref object[] ContextParams)
		{
			throw new NotImplementedException();
		}

		public Window OpenFile(string ViewKind, string FileName)
		{
			throw new NotImplementedException();
		}

		public void Quit()
		{
			throw new NotImplementedException();
		}

		public string SatelliteDllPath(string Path, string Name)
		{
			throw new NotImplementedException();
		}

		private Dictionary<string, Dictionary<string, Properties>> _properties = new Dictionary<string, Dictionary<string, Properties>>() {
			{
				"Environment",
				new Dictionary<string, Properties>() {
					{
						"ProjectsAndSolution",
						new MockDTEProperties() {
							{ "MSBuildOutputVerbosity", 2 }
						}
					}
				}
			}
		};
	}
}