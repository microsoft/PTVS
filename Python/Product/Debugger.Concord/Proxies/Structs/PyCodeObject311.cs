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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "PyCodeObject", MinVersion = PythonLanguageVersion.V311)]
    [PyType(MinVersion = PythonLanguageVersion.V311, VariableName = "PyCode_Type")]
    internal class PyCodeObject311 : PyCodeObject {
        public class Fields {
            public StructField<Int32Proxy> co_nlocals;
            public StructField<PointerProxy<PyTupleObject>> co_names;
            public StructField<PointerProxy<IPyBaseStringObject>> co_filename;
            public StructField<PointerProxy<IPyBaseStringObject>> co_name;
            public StructField<Int32Proxy> co_firstlineno;
            public StructField<PointerProxy<PyTupleObject>> co_localsplusnames;
            public StructField<PointerProxy<PyBytesObject>> co_localspluskinds;
        }

        private readonly Fields _fields;

        public override Int32Proxy co_nlocals => GetFieldProxy(_fields.co_nlocals);

        public override PointerProxy<PyTupleObject> co_names => GetFieldProxy(_fields.co_names);

        public override IWritableDataProxy<PyTupleObject> co_varnames => GetVariableTupleProxy(VariableKind.CO_FAST_LOCAL);

        public override IWritableDataProxy<PyTupleObject> co_freevars => GetVariableTupleProxy(VariableKind.CO_FAST_FREE);

        public override IWritableDataProxy<PyTupleObject> co_cellvars => GetVariableTupleProxy(VariableKind.CO_FAST_CELL);

        public override PointerProxy<IPyBaseStringObject> co_filename => GetFieldProxy(_fields.co_filename);

        public override PointerProxy<IPyBaseStringObject> co_name => GetFieldProxy(_fields.co_name);

        public override Int32Proxy co_firstlineno => GetFieldProxy(_fields.co_firstlineno);

        public PyCodeObject311(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyCodeObject311>();
        }

        [Flags]
        internal enum VariableKind {
            CO_FAST_LOCAL = 0x20,
            CO_FAST_CELL = 0x40,
            CO_FAST_FREE = 0x80
        }

        private IWritableDataProxy<PyTupleObject> GetVariableTupleProxy(VariableKind kind) {
            return new PyVariableTuplePointerProxy(Process, kind, GetFieldProxy(_fields.co_localsplusnames), GetFieldProxy(_fields.co_localspluskinds));
        }

        // In 3.11, the co_varnames, co_freevars, and co_cellvars were all removed. In order to not have to change our usage
        // of them, this class and the PyVariableTuple were created in order to pretend to behave like the original co_varnames etc.
        internal class PyVariableTuplePointerProxy : IWritableDataProxy<PyTupleObject> {
            private readonly PyVariableTuple _pseudoTuple;
            private readonly PointerProxy<PyTupleObject> _realTuple;
            private readonly DkmProcess _process;

            public PyVariableTuplePointerProxy(DkmProcess process, VariableKind kind, PointerProxy<PyTupleObject> localsplusnames, PointerProxy<PyBytesObject> localspluskinds) {
                _pseudoTuple = new PyVariableTuple(process, kind, localsplusnames, localspluskinds);
                _realTuple = localsplusnames;
                _process = process;
            }

            public DkmProcess Process => _process;

            public ulong Address => _realTuple.Address;

            public long ObjectSize => _realTuple.ObjectSize;

            public PyTupleObject Read() => _pseudoTuple;

            public void Write(PyTupleObject value) => throw new NotImplementedException();
            public void Write(object value) => throw new NotImplementedException();
            object IValueStore.Read() => _pseudoTuple;
        }

        [PyType(Hidden = true)]
        internal class PyVariableTuple : PyTupleObject {
            private readonly VariableKind _kind;
            private readonly PointerProxy<PyTupleObject> _localsplusnames;
            private readonly PointerProxy<PyBytesObject> _localspluskinds;
            private readonly DkmProcess _process;

            public PyVariableTuple(DkmProcess process, VariableKind kind, PointerProxy<PyTupleObject> localsplusnames, PointerProxy<PyBytesObject> localspluskinds)
                : base(process, localsplusnames.Read().Address) {
                _kind = kind;
                _localsplusnames = localsplusnames;
                _localspluskinds = localspluskinds;
                _process = process;
            }

            public override IEnumerable<PointerProxy<PyObject>> ReadElements() {
                var localsplusnames = new List<PointerProxy<PyObject>>(_localsplusnames.Read().ReadElements());
                var localspluskinds = _localspluskinds.Read().ToBytes();
                Debug.Assert(localsplusnames.Count == localspluskinds.Length);
                for (var i = 0; i < localsplusnames.Count; i++) {
                    var name = localsplusnames[i];
                    var kind = (VariableKind)localspluskinds[i];
                    if ((kind & _kind) == _kind) {
                        yield return name;
                    }
                }
            }

        }
    }
}
