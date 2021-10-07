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
	internal class MockOutputWindowPane : IVsOutputWindowPane
	{
		private string _name;
		private readonly StringBuilder _content = new StringBuilder();

		public MockOutputWindowPane(string pszPaneName)
		{
			_name = pszPaneName;
		}

		public int Activate()
		{
			return VSConstants.S_OK;
		}

		public int Clear()
		{
			_content.Clear();
			return VSConstants.S_OK;
		}

		public int FlushToTaskList()
		{
			throw new NotImplementedException();
		}

		public int GetName(ref string pbstrPaneName)
		{
			pbstrPaneName = _name;
			return VSConstants.S_OK;
		}

		public int Hide()
		{
			return VSConstants.S_OK;
		}

		public int OutputString(string pszOutputString)
		{
			lock (this)
			{
				_content.Append(pszOutputString);
			}
			return VSConstants.S_OK;
		}

		public int OutputStringThreadSafe(string pszOutputString)
		{
			lock (this)
			{
				_content.Append(pszOutputString);
			}
			return VSConstants.S_OK;
		}

		public int OutputTaskItemString(string pszOutputString, VSTASKPRIORITY nPriority, VSTASKCATEGORY nCategory, string pszSubcategory, int nBitmap, string pszFilename, uint nLineNum, string pszTaskItemText)
		{
			throw new NotImplementedException();
		}

		public int OutputTaskItemStringEx(string pszOutputString, VSTASKPRIORITY nPriority, VSTASKCATEGORY nCategory, string pszSubcategory, int nBitmap, string pszFilename, uint nLineNum, string pszTaskItemText, string pszLookupKwd)
		{
			throw new NotImplementedException();
		}

		public int SetName(string pszPaneName)
		{
			_name = pszPaneName;
			return VSConstants.S_OK;
		}
	}
}
