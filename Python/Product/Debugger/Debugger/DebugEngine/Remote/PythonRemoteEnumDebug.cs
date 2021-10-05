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

namespace Microsoft.PythonTools.Debugger.Remote
{
	internal class PythonRemoteEnumDebug<T>
		where T : class
	{

		private readonly T _elem;
		private bool _done;

		public PythonRemoteEnumDebug(T elem = null)
		{
			this._elem = elem;
			Reset();
		}

		protected T Element
		{
			get { return _elem; }
		}

		public int GetCount(out uint pcelt)
		{
			pcelt = (_elem == null) ? 0u : 1u;
			return 0;
		}

		public int Next(uint celt, T[] rgelt, ref uint pceltFetched)
		{
			if (_done)
			{
				pceltFetched = 0;
				return 1;
			}
			else
			{
				pceltFetched = 1;
				rgelt[0] = _elem;
				_done = true;
				return 0;
			}
		}

		public int Reset()
		{
			_done = (_elem == null);
			return 0;
		}

		public int Skip(uint celt)
		{
			if (celt == 0)
			{
				return 0;
			}
			else if (_done)
			{
				return 1;
			}
			else
			{
				_done = true;
				return celt > 1 ? 1 : 0;
			}
		}
	}
}
