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
	class MockOutputWindow : IVsOutputWindow
	{
		private static Dictionary<Guid, MockOutputWindowPane> _panes = new Dictionary<Guid, MockOutputWindowPane>() {
			{VSConstants.OutputWindowPaneGuid.GeneralPane_guid, new MockOutputWindowPane("General") }
		};

		public int CreatePane(ref Guid rguidPane, string pszPaneName, int fInitVisible, int fClearWithSolution)
		{
			if (_panes.TryGetValue(rguidPane, out MockOutputWindowPane pane))
			{
				_panes[rguidPane] = new MockOutputWindowPane(pszPaneName);
			}
			return VSConstants.S_OK;
		}

		public int DeletePane(ref Guid rguidPane)
		{
			_panes.Remove(rguidPane);
			return VSConstants.S_OK;
		}

		public int GetPane(ref Guid rguidPane, out IVsOutputWindowPane ppPane)
		{
			if (_panes.TryGetValue(rguidPane, out MockOutputWindowPane pane))
			{
				ppPane = pane;
				return VSConstants.S_OK;
			}
			ppPane = null;
			return VSConstants.E_FAIL;
		}
	}
}
