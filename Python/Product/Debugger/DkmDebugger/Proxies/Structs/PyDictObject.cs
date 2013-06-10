/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    using Microsoft.VisualStudio.Debugger.Evaluation;
    using PyDictEntry = Microsoft.PythonTools.DkmDebugger.Proxies.Structs.PyDictKeyEntry;

    internal abstract class PyDictObject : PyObject {
        protected PyDictObject(DkmProcess process, ulong address)
            : base(process, address) {
        }

        public abstract IEnumerable<KeyValuePair<PyObject, PointerProxy<PyObject>>> ReadElements();

        public override void Repr(ReprBuilder builder) {
            var count = ReadElements().Count();
            if (count > ReprBuilder.MaxJoinedItems) {
                builder.AppendFormat("<dict, len() = {0}>", count);
                return;
            }

            builder.Append("{");
            builder.AppendJoined(", ", ReadElements(), entry => {
                builder.AppendRepr(entry.Key);
                builder.Append(": ");
                builder.AppendRepr(entry.Value.TryRead());
            });
            builder.Append("}");
        }

        public override IEnumerable<PythonEvaluationResult> GetDebugChildren(ReprOptions reprOptions) {
            yield return new PythonEvaluationResult(new ValueStore<long>(ReadElements().Count()), "len()") {
                Category = DkmEvaluationResultCategory.Method
            };

            var reprBuilder = new ReprBuilder(reprOptions);
            foreach (var entry in ReadElements()) {
                reprBuilder.Clear();
                reprBuilder.AppendFormat("[{0}]", entry.Key);
                yield return new PythonEvaluationResult(entry.Value, reprBuilder.ToString());
            }
        }
    }

    [StructProxy(StructName = "PyDictObject", MaxVersion = PythonLanguageVersion.V27)]
    [PyType(VariableName = "PyDict_Type", MaxVersion = PythonLanguageVersion.V27)]
    internal class PyDictObject27 : PyDictObject {
        private class DummyHolder : DkmDataItem {
            public readonly PointerProxy<PyObject> Dummy;

            public DummyHolder(DkmProcess process) {
                Dummy = process.GetPythonRuntimeInfo().DLLs.Python.GetStaticVariable<PointerProxy<PyObject>>("dummy", "dictobject.obj");
            }
        }

        private class Fields {
            public StructField<SSizeTProxy> ma_mask;
            public StructField<PointerProxy<ArrayProxy<PyDictEntry>>> ma_table;
        }

        private readonly Fields _fields;
        private readonly PyObject _dummy;

        public PyDictObject27(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyDictObject27>();

            _dummy = Process.GetOrCreateDataItem(() => new DummyHolder(Process)).Dummy.TryRead();
        }

        public PointerProxy<ArrayProxy<PyDictEntry>> ma_table {
            get { return GetFieldProxy(_fields.ma_table); }
        }

        public SSizeTProxy ma_mask {
            get { return GetFieldProxy(_fields.ma_mask); }
        }

        public override IEnumerable<KeyValuePair<PyObject, PointerProxy<PyObject>>> ReadElements() {
            var count = ma_mask.Read() + 1;
            var entries = ma_table.Read().Take((int)count);
            var items = from entry in entries
                        let key = entry.me_key.TryRead()
                        where key != null && key != _dummy
                        let valuePtr = entry.me_value
                        where !valuePtr.IsNull
                        select new KeyValuePair<PyObject, PointerProxy<PyObject>>(key, valuePtr);
            return items;
        }
    }

    [StructProxy(StructName = "PyDictObject", MinVersion = PythonLanguageVersion.V33)]
    [PyType(VariableName = "PyDict_Type", MinVersion = PythonLanguageVersion.V33)]
    internal class PyDictObject33 : PyDictObject {
        private class Fields {
            public StructField<PointerProxy<PyDictKeysObject>> ma_keys;
            public StructField<PointerProxy<ArrayProxy<PointerProxy<PyObject>>>> ma_values;
        }

        private readonly Fields _fields;

        public PyDictObject33(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyDictObject33>();
        }

        public PointerProxy<PyDictKeysObject> ma_keys {
            get { return GetFieldProxy(_fields.ma_keys); }
        }

        public PointerProxy<ArrayProxy<PointerProxy<PyObject>>> ma_values {
            get { return GetFieldProxy(_fields.ma_values); }
        }

        public override IEnumerable<KeyValuePair<PyObject, PointerProxy<PyObject>>> ReadElements() {
            var keys = this.ma_keys.Read();
            var entries = keys.dk_entries.Take((int)keys.dk_size.Read());

            var ma_values = this.ma_values;
            if (ma_values.IsNull) {
                foreach (PyDictKeyEntry entry in entries) {
                    var key = entry.me_key.TryRead();
                    if (key != null) {
                        yield return new KeyValuePair<PyObject, PointerProxy<PyObject>>(key, entry.me_value);
                    }
                }
            } else {
                var valuePtr = ma_values.Read()[0];
                foreach (PyDictKeyEntry entry in entries) {
                    var key = entry.me_key.TryRead();
                    if (key != null) {
                        yield return new KeyValuePair<PyObject, PointerProxy<PyObject>>(key, valuePtr);
                    }
                    valuePtr = valuePtr.GetAdjacentProxy(1);
                }
            }
        }
    }

    [StructProxy(MinVersion = PythonLanguageVersion.V33)]
    internal class PyDictKeysObject : StructProxy {
        private class Fields {
            public StructField<SSizeTProxy> dk_size;
            public StructField<ArrayProxy<PyDictKeyEntry>> dk_entries;
        }

        private readonly Fields _fields;

        public SSizeTProxy dk_size {
            get { return GetFieldProxy(_fields.dk_size); }
        }

        public ArrayProxy<PyDictKeyEntry> dk_entries {
            get { return GetFieldProxy(_fields.dk_entries); }
        }

        public PyDictKeysObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }
    }

    [StructProxy(MaxVersion = PythonLanguageVersion.V27, StructName = "PyDictEntry")]
    [StructProxy(MinVersion = PythonLanguageVersion.V33)]
    internal class PyDictKeyEntry : StructProxy {
        private class Fields {
            public StructField<PointerProxy<PyObject>> me_key;
            public StructField<PointerProxy<PyObject>> me_value;
        }

        private readonly Fields _fields;

        public PyDictKeyEntry(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyObject> me_key {
            get { return GetFieldProxy(_fields.me_key); }
        }

        public PointerProxy<PyObject> me_value {
            get { return GetFieldProxy(_fields.me_value); }
        }
    }
}
