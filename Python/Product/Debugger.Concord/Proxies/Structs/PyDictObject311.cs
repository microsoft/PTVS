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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "PyDictObject", MinVersion = PythonLanguageVersion.V311)]
    [PyType(VariableName = "PyDict_Type", MinVersion = PythonLanguageVersion.V311)]
    internal class PyDictObject311 : PyDictObject {
        private class Fields {
            public StructField<PointerProxy<PyDictKeysObject311>> ma_keys;
            public StructField<PointerProxy<ArrayProxy<PointerProxy<PyObject>>>> ma_values;
        }

        private readonly Fields _fields;

        public PyDictObject311(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyDictObject311>();
        }

        public PointerProxy<PyDictKeysObject311> ma_keys {
            get { return GetFieldProxy(_fields.ma_keys); }
        }

        public PointerProxy<ArrayProxy<PointerProxy<PyObject>>> ma_values {
            get { return GetFieldProxy(_fields.ma_values); }
        }

        public override IEnumerable<KeyValuePair<PyObject, PointerProxy<PyObject>>> ReadElements() {
            if (ma_keys.IsNull) {
                yield break;
            }

            var keys = ma_keys.Read();
            var size = 1 << (int)keys.dk_log2_size.Read();
            if (size <= 0) {
                yield break;
            }

            var n = keys.dk_nentries.Read();
            if (!ma_values.IsNull) {
                var values = ma_values.Read();
                var value = values[0];

                for (int i = 0; i < n; ++i) {
                    var entry = GetDkEntry(i);
                    var key = entry.me_key;
                    if (!value.IsNull && !key.IsNull) {
                        yield return new KeyValuePair<PyObject, PointerProxy<PyObject>>(key.Read(), value);
                    }
                    value = value.GetAdjacentProxy(1);
                }
            } else {
                for (int i = 0; i < n; ++i) {
                    var entry = GetDkEntry(i);
                    var key = entry.me_key;
                    var value = entry.me_value;
                    if (!key.IsNull && !value.IsNull) {
                        yield return new KeyValuePair<PyObject, PointerProxy<PyObject>>(key.Read(), value);
                    }
                }
            }
        }

        private IDictKeyEntry GetDkEntry(int position) {
            var keys = ma_keys.Read();
            if (keys.dk_kind.Read() == 0) {
                var entries = keys.dk_general_entries;
                return entries[position];
            } else {
                var entries = keys.dk_unicode_entries;
                return entries[position];
            }
        }

    }

    [StructProxy(MinVersion = PythonLanguageVersion.V311, StructName = "_dictkeysobject")]
    internal class PyDictKeysObject311 : StructProxy {
        public class Fields {
            public StructField<SSizeTProxy> dk_log2_size;
            public StructField<SSizeTProxy> dk_log2_index_bytes;
            public StructField<SSizeTProxy> dk_nentries;
            public StructField<ArrayProxy<SByteProxy>> dk_indices;
            public StructField<ByteProxy> dk_kind;
        }

        public readonly Fields _fields;

        public SSizeTProxy dk_log2_size => GetFieldProxy(_fields.dk_log2_size);
        public SSizeTProxy dk_log2_index_bytes => GetFieldProxy(_fields.dk_log2_index_bytes);

        public ByteProxy dk_kind => GetFieldProxy(_fields.dk_kind);

        public SSizeTProxy dk_nentries {
            get { return GetFieldProxy(_fields.dk_nentries); }
        }

        public ArrayProxy<PyDictKeyEntry311> dk_general_entries {
            get {
                // dk_entries is located after dk_indices, which is
                // variable length depending on the size of the table.
                long log_size = dk_log2_index_bytes.Read();
                long offset = _fields.dk_indices.Offset;
                offset += 1 << (int)log_size;

                return DataProxy.Create<ArrayProxy<PyDictKeyEntry311>>(
                    Process,
                    Address.OffsetBy(offset)
                );
            }
        }

        public ArrayProxy<PyDictUnicodeEntry> dk_unicode_entries {
            get {
                // dk_entries is located after dk_indices, which is
                // variable length depending on the size of the table.
                long log_size = dk_log2_index_bytes.Read();
                long offset = _fields.dk_indices.Offset;
                offset += 1 << (int)log_size;

                return DataProxy.Create<ArrayProxy<PyDictUnicodeEntry>>(
                    Process,
                    Address.OffsetBy(offset)
                );
            }
        }

        public PyDictKeysObject311(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }
    }

    internal interface IDictKeyEntry : IDataProxy<IDictKeyEntry> {
        public PointerProxy<PyObject> me_key { get; }

        public PointerProxy<PyObject> me_value { get; }
    }

    [StructProxy(MinVersion = PythonLanguageVersion.V311)]
    internal class PyDictKeyEntry311 : StructProxy, IDictKeyEntry {
        private class Fields {
            public StructField<PointerProxy<PyObject>> me_key;
            public StructField<PointerProxy<PyObject>> me_value;
        }

        private readonly Fields _fields;

        public PyDictKeyEntry311(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyObject> me_key {
            get { return GetFieldProxy(_fields.me_key); }
        }

        public PointerProxy<PyObject> me_value {
            get { return GetFieldProxy(_fields.me_value); }
        }

        public IDictKeyEntry Read() => this;
    }
    [StructProxy(MinVersion = PythonLanguageVersion.V311)]
    internal class PyDictUnicodeEntry : StructProxy, IDictKeyEntry {
        private class Fields {
            public StructField<PointerProxy<PyObject>> me_key;
            public StructField<PointerProxy<PyObject>> me_value;
        }

        private readonly Fields _fields;

        public PyDictUnicodeEntry(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyObject> me_key {
            get { return GetFieldProxy(_fields.me_key); }
        }

        public PointerProxy<PyObject> me_value {
            get { return GetFieldProxy(_fields.me_value); }
        }

        public IDictKeyEntry Read() => this;
    }
}
