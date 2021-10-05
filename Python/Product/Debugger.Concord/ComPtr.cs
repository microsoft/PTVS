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

namespace Microsoft.PythonTools.Debugger.Concord
{
	/// <summary>
	/// An owning reference to a COM object. When disposed, calls <see cref="Marshal.ReleaseComObject"/> on the pointee.
	/// </summary>
	internal struct ComPtr<T> : IDisposable
		where T : class
	{

		private T _obj;

		public ComPtr(T obj)
		{
			_obj = obj;
		}

		public T Object
		{
			get
			{
				return _obj;
			}
		}

		public void Dispose()
		{
			if (_obj != null && Marshal.IsComObject(_obj))
			{
				Marshal.ReleaseComObject(_obj);
			}
			_obj = null;
		}

		public ComPtr<T> Detach()
		{
			var result = this;
			_obj = null;
			return result;
		}

		public static implicit operator T(ComPtr<T> ptr)
		{
			return ptr._obj;
		}
	}

	internal static class ComPtr
	{
		public static ComPtr<T> Create<T>(T obj) where T : class
		{
			return new ComPtr<T>(obj);
		}
	}
}
