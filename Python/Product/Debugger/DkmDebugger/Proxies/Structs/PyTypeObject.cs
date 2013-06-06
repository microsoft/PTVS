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

using System;
using System.Linq;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    internal class PyTypeObject : PyObject {
        private class Fields {
            public StructField<PointerProxy<CStringProxy>> tp_name;
            public StructField<SSizeTProxy> tp_basicsize;
            public StructField<SSizeTProxy> tp_itemsize;
            public StructField<PointerProxy<ArrayProxy<PyMemberDef>>> tp_members;
            public StructField<SSizeTProxy> tp_dictoffset;
            public StructField<PointerProxy<PyTypeObject>> tp_base;
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

        public PointerProxy<ArrayProxy<PyMemberDef>> tp_members {
            get { return GetFieldProxy(_fields.tp_members); }
        }

        public SSizeTProxy tp_dictoffset {
            get { return GetFieldProxy(_fields.tp_dictoffset); }
        }

        public PointerProxy<PyTypeObject> tp_base {
            get { return GetFieldProxy(_fields.tp_base); }
        }

        public PointerProxy<PyTupleObject> tp_bases {
            get { return GetFieldProxy(_fields.tp_bases); }
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

        protected override string Repr(Func<PyObject, string> repr) {
            return "<class '" + tp_name.Read().Read() + "'>";
        }
    }
}
