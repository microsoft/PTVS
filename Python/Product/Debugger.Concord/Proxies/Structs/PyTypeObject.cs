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

using System.Linq;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    internal class PyTypeObject : PyObject {
        private class Fields {
            public StructField<PointerProxy<CStringProxy>> tp_name;
            public StructField<SSizeTProxy> tp_basicsize;
            public StructField<SSizeTProxy> tp_itemsize;
            public StructField<Int32Proxy> tp_flags;
            public StructField<PointerProxy<ArrayProxy<PyMemberDef>>> tp_members;
            public StructField<SSizeTProxy> tp_dictoffset;
            public StructField<PointerProxy<PyTypeObject>> tp_base;
            public StructField<PointerProxy<PyObject>> tp_dict;
            public StructField<PointerProxy<PyTupleObject>> tp_bases;
        }

        private readonly Fields _fields;

        private PyTypeObject(DkmProcess process, ulong addr, bool checkType)
            : base(process, addr) {
            InitializeStruct(this, out _fields);
        }

        public PyTypeObject(DkmProcess process, ulong addr)
            : this(process, addr, true) {
        }

        public unsafe static PyTypeObject FromNativeGlobalVariable(DkmProcess process, string name) {
            var addr = process.GetPythonRuntimeInfo().DLLs.Python.GetStaticVariableAddress(name);
            return new PyTypeObject(process, addr);
        }

        public PointerProxy<CStringProxy> tp_name {
            get { return GetFieldProxy(_fields.tp_name); }
        }

        public SSizeTProxy tp_basicsize {
            get { return GetFieldProxy(_fields.tp_basicsize); }
        }

        public SSizeTProxy tp_itemsize {
            get { return GetFieldProxy(_fields.tp_itemsize); }
        }

        public Int32Proxy tp_flags {
            get { return GetFieldProxy(_fields.tp_flags); }
        }

        public PointerProxy<ArrayProxy<PyMemberDef>> tp_members {
            get { return GetFieldProxy(_fields.tp_members); }
        }

        public SSizeTProxy tp_dictoffset {
            get { return GetFieldProxy(_fields.tp_dictoffset); }
        }

        public PointerProxy<PyTypeObject> tp_base {
            get { return GetFieldProxy(_fields.tp_base); }
        }

        public PointerProxy<PyObject> tp_dict {
            get { return GetFieldProxy(_fields.tp_dict); }
        }

        public PointerProxy<PyTupleObject> tp_bases {
            get { return GetFieldProxy(_fields.tp_bases); }
        }

        public string __name__ {
            get {
                if ((tp_flags.Read() & (int)Py_TPFLAGS.HEAPTYPE) != 0) {
                    var heapType = new PyHeapTypeObject(Process, Address);
                    var nameObj = heapType.ht_name.Read() as IPyBaseStringObject;
                    return nameObj.ToStringOrNull();
                } else {
                    string name = tp_name.Read().ReadUnicode();
                    return name.Split('.').LastOrDefault();
                }
            }
        }

        public string __module__ {
            get {
                if ((tp_flags.Read() & (int)Py_TPFLAGS.HEAPTYPE) != 0) {
                    var dict = tp_dict.TryRead() as PyDictObject;
                    if (dict == null) {
                        return null;
                    }

                    var module = (from pair in dict.ReadElements()
                                  let name = (pair.Key as IPyBaseStringObject).ToStringOrNull()
                                  where name == "__module__"
                                  let value = pair.Value.TryRead()
                                  where value != null
                                  select value
                                 ).FirstOrDefault();
                    return (module as IPyBaseStringObject).ToStringOrNull();
                } else {
                    string name = tp_name.Read().ReadUnicode();

                    int lastDot = name.LastIndexOf('.');
                    if (lastDot < 0) {
                        return "builtins";
                    }

                    return name.Substring(0, lastDot);
                }
            }
        }

        public bool IsSubtypeOf(PyTypeObject type) {
            if (this == type) {
                return true;
            }

            var tp_base = this.tp_base.TryRead();
            if (tp_base != null && tp_base.IsSubtypeOf(type)) {
                return true;
            }

            var tp_bases = this.tp_bases.TryRead();
            if (tp_bases != null) {
                var bases = tp_bases.ReadElements().OfType<PyTypeObject>();
                if (bases.Any(t => t.IsSubtypeOf(type))) {
                    return true;
                }
            }

            return false;
        }

        public override void Repr(ReprBuilder builder) {
            builder.AppendFormat("<class '{0}'>", tp_name.Read().ReadUnicode());
        }
    }

    internal class PyHeapTypeObject : StructProxy {
        private class Fields {
            public StructField<PointerProxy<PyObject>> ht_name;
        }

        private readonly Fields _fields;

        public PyHeapTypeObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyObject> ht_name {
            get { return GetFieldProxy(_fields.ht_name); }
        }
    }

    internal enum Py_TPFLAGS {
        HEAPTYPE = 1 << 9
    }
}
