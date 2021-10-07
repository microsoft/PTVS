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

namespace Microsoft.PythonTools.Debugger
{
	internal class IdDispenser
	{
		private readonly List<int> _freedInts = new List<int>();
		private int _curValue;

		public int Allocate()
		{
			lock (this)
			{
				if (_freedInts.Count > 0)
				{
					int res = _freedInts[_freedInts.Count - 1];
					_freedInts.RemoveAt(_freedInts.Count - 1);
					return res;
				}
				else
				{
					int res = _curValue++;
					return res;
				}
			}
		}

		public void Free(int id)
		{
			lock (this)
			{
				if (id + 1 == _curValue)
				{
					_curValue--;
				}
				else
				{
					_freedInts.Add(id);
				}
			}
		}
	}
}
