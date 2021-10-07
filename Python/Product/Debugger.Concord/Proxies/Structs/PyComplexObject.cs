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
	internal class PyComplexObject : PyObject
	{
		public class Fields
		{
			public StructField<Py_complex> cval;
		}

		private readonly Fields _fields;

		public PyComplexObject(DkmProcess process, ulong address)
			: base(process, address)
		{
			InitializeStruct(this, out _fields);
			CheckPyType<PyComplexObject>();
		}

		public static PyComplexObject Create(DkmProcess process, Complex value)
		{
			var allocator = process.GetDataItem<PyObjectAllocator>();
			Debug.Assert(allocator != null);

			var result = allocator.Allocate<PyComplexObject>();
			result.cval.real.Write(value.Real);
			result.cval.imag.Write(value.Imaginary);
			return result;
		}

		public Py_complex cval => GetFieldProxy(_fields.cval);

		public Complex ToComplex()
		{
			return new Complex(cval.real.Read(), cval.imag.Read());
		}

		public override void Repr(ReprBuilder builder)
		{
			builder.AppendLiteral(ToComplex());
		}
	}

	internal class Py_complex : StructProxy
	{
		public class Fields
		{
			public StructField<DoubleProxy> real;
			public StructField<DoubleProxy> imag;
		}

		private readonly Fields _fields;

		public Py_complex(DkmProcess process, ulong address)
			: base(process, address)
		{
			InitializeStruct(this, out _fields);
		}

		public DoubleProxy real => GetFieldProxy(_fields.real);

		public DoubleProxy imag => GetFieldProxy(_fields.imag);
	}
}
