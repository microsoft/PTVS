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

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs
{
	[StructProxy(MaxVersion = PythonLanguageVersion.V27)]
	[PyType(MaxVersion = PythonLanguageVersion.V27)]
	internal class PyIntObject : PyObject
	{
		private class Fields
		{
			public StructField<Int32Proxy> ob_ival;
		}

		private readonly Fields _fields;

		public PyIntObject(DkmProcess process, ulong address)
			: this(process, address, true)
		{
		}

		protected PyIntObject(DkmProcess process, ulong address, bool checkType)
			: base(process, address)
		{
			InitializeStruct(this, out _fields);
			if (checkType)
			{
				CheckPyType<PyIntObject>();
			}
		}

		public static PyIntObject Create(DkmProcess process, int value)
		{
			var allocator = process.GetDataItem<PyObjectAllocator>();
			Debug.Assert(allocator != null);

			var result = allocator.Allocate<PyIntObject>();
			result.ob_ival.Write(value);
			return result;
		}

		private Int32Proxy ob_ival => GetFieldProxy(_fields.ob_ival);

		public Int32 ToInt32()
		{
			return ob_ival.Read();
		}

		public override void Repr(ReprBuilder builder)
		{
			builder.Append(ToInt32());
		}
	}
}
