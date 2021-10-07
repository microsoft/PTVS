// Python Tools for Visual Studio
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

extern alias pt;

namespace TestAdapterTests
{
	public abstract partial class TestExecutorTests
	{
		private class StackFrameComparer : IComparer
		{
			public int Compare(object x, object y)
			{
				if (x == y)
				{
					return 0;
				}

				var a = x as StackFrame;
				var b = y as StackFrame;

				if (a == null)
				{
					return -1;
				}

				if (b == null)
				{
					return 1;
				}

				int res = a.FileName.CompareTo(b.FileName);
				if (res != 0)
				{
					return res;
				}

				res = a.LineNumber.CompareTo(b.LineNumber);
				if (res != 0)
				{
					return res;
				}

				res = a.MethodDisplayName.CompareTo(b.MethodDisplayName);
				if (res != 0)
				{
					return res;
				}

				return 0;
			}
		}
	}
}
