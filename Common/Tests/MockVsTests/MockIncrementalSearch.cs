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
	internal class MockIncrementalSearch : IIncrementalSearch
	{
		private readonly MockTextView _view;

		public MockIncrementalSearch(MockTextView textView)
		{
			_view = textView;
		}

		public IncrementalSearchResult AppendCharAndSearch(char toAppend)
		{
			throw new NotImplementedException();
		}

		public void Clear()
		{
			throw new NotImplementedException();
		}

		public IncrementalSearchResult DeleteCharAndSearch()
		{
			throw new NotImplementedException();
		}

		public void Dismiss()
		{
			throw new NotImplementedException();
		}

		public bool IsActive => false;

		public IncrementalSearchDirection SearchDirection
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		public string SearchString
		{
			get => throw new NotImplementedException();
			set => throw new NotImplementedException();
		}

		public IncrementalSearchResult SelectNextResult()
		{
			throw new NotImplementedException();
		}

		public void Start()
		{
			throw new NotImplementedException();
		}

		public VisualStudio.Text.Editor.ITextView TextView => throw new NotImplementedException();
	}
}
