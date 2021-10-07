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
	[StructProxy(StructName = "PyDictObject", MinVersion = PythonLanguageVersion.V36)]
	[PyType(VariableName = "PyDict_Type", MinVersion = PythonLanguageVersion.V36)]
	internal class PyDictObject36 : PyDictObject
	{
		private class Fields
		{
			public StructField<PointerProxy<PyDictKeysObject36>> ma_keys;
			public StructField<PointerProxy<ArrayProxy<PointerProxy<PyObject>>>> ma_values;
		}

		private readonly Fields _fields;

		public PyDictObject36(DkmProcess process, ulong address)
			: base(process, address)
		{
			InitializeStruct(this, out _fields);
			CheckPyType<PyDictObject36>();
		}

		public PointerProxy<PyDictKeysObject36> ma_keys => GetFieldProxy(_fields.ma_keys);

		public PointerProxy<ArrayProxy<PointerProxy<PyObject>>> ma_values => GetFieldProxy(_fields.ma_values);

		public override IEnumerable<KeyValuePair<PyObject, PointerProxy<PyObject>>> ReadElements()
		{
			if (ma_keys.IsNull)
			{
				yield break;
			}

			PyDictKeysObject36 keys = ma_keys.Read();
			var size = keys.dk_size.Read();
			if (size <= 0)
			{
				yield break;
			}

			var n = keys.dk_nentries.Read();
			ArrayProxy<PyDictKeyEntry> dk_entries = keys.dk_entries;
			PyDictKeyEntry entry = dk_entries[0];

			if (!ma_values.IsNull)
			{
				ArrayProxy<PointerProxy<PyObject>> values = ma_values.Read();
				PointerProxy<PyObject> value = values[0];

				for (int i = 0; i < n; ++i)
				{
					PointerProxy<PyObject> key = entry.me_key;
					if (!value.IsNull && !key.IsNull)
					{
						yield return new KeyValuePair<PyObject, PointerProxy<PyObject>>(key.Read(), value);
					}
					entry = entry.GetAdjacentProxy(1);
					value = value.GetAdjacentProxy(1);
				}
			}
			else
			{
				for (int i = 0; i < n; ++i)
				{
					PointerProxy<PyObject> key = entry.me_key;
					PointerProxy<PyObject> value = entry.me_value;
					if (!key.IsNull && !value.IsNull)
					{
						yield return new KeyValuePair<PyObject, PointerProxy<PyObject>>(key.Read(), value);
					}
					entry = entry.GetAdjacentProxy(1);
				}
			}
		}
	}

	[StructProxy(MinVersion = PythonLanguageVersion.V36, StructName = "_dictkeysobject")]
	internal class PyDictKeysObject36 : StructProxy
	{
		public class Fields
		{
			public StructField<SSizeTProxy> dk_size;
			public StructField<SSizeTProxy> dk_nentries;
			public StructField<ArrayProxy<SByteProxy>> dk_indices;
		}

		public readonly Fields _fields;

		public SSizeTProxy dk_size => GetFieldProxy(_fields.dk_size);

		public SSizeTProxy dk_nentries => GetFieldProxy(_fields.dk_nentries);

		public ArrayProxy<PyDictKeyEntry> dk_entries
		{
			get
			{
				// dk_entries is located after dk_indices, which is
				// variable length depending on the size of the table.
				long size = dk_size.Read();
				long offset = _fields.dk_indices.Offset;
				if (size <= 0)
				{
					return default(ArrayProxy<PyDictKeyEntry>);
				}
				else if (size <= 0xFF)
				{
					offset += size;
				}
				else if (size <= 0xFFFF)
				{
					offset += size * 2;
				}
				else if (size <= 0xFFFFFFFF)
				{
					offset += size * 4;
				}
				else
				{
					offset += size * 8;
				}

				return DataProxy.Create<ArrayProxy<PyDictKeyEntry>>(
					Process,
					Address.OffsetBy(offset)
				);
			}
		}

		public PyDictKeysObject36(DkmProcess process, ulong address)
			: base(process, address)
		{
			InitializeStruct(this, out _fields);
		}
	}
}
