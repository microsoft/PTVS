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
	[StructProxy(StructName = "PyDictObject", MaxVersion = PythonLanguageVersion.V27)]
	[PyType(VariableName = "PyDict_Type", MaxVersion = PythonLanguageVersion.V27)]
	internal class PyDictObject27 : PyDictObject
	{
		private class DummyHolder : DkmDataItem
		{
			public readonly PointerProxy<PyObject> Dummy;

			public DummyHolder(DkmProcess process)
			{
				Dummy = process.GetPythonRuntimeInfo().DLLs.Python.GetStaticVariable<PointerProxy<PyObject>>("dummy", "dictobject.obj");
			}
		}

		private class Fields
		{
			public StructField<SSizeTProxy> ma_mask;
			public StructField<PointerProxy<ArrayProxy<PyDictKeyEntry>>> ma_table;
		}

		private readonly Fields _fields;
		private readonly PyObject _dummy;

		public PyDictObject27(DkmProcess process, ulong address)
			: base(process, address)
		{
			InitializeStruct(this, out _fields);
			CheckPyType<PyDictObject27>();

			_dummy = Process.GetOrCreateDataItem(() => new DummyHolder(Process)).Dummy.TryRead();
		}

		public PointerProxy<ArrayProxy<PyDictKeyEntry>> ma_table
		{
			get { return GetFieldProxy(_fields.ma_table); }
		}

		public SSizeTProxy ma_mask
		{
			get { return GetFieldProxy(_fields.ma_mask); }
		}

		public override IEnumerable<KeyValuePair<PyObject, PointerProxy<PyObject>>> ReadElements()
		{
			if (ma_table.IsNull)
			{
				return Enumerable.Empty<KeyValuePair<PyObject, PointerProxy<PyObject>>>();
			}

			var count = ma_mask.Read() + 1;
			var entries = ma_table.Read().Take(count);
			var items = from entry in entries
						let key = entry.me_key.TryRead()
						where key != null && key != _dummy
						let valuePtr = entry.me_value
						where !valuePtr.IsNull
						select new KeyValuePair<PyObject, PointerProxy<PyObject>>(key, valuePtr);
			return items;
		}
	}
}
