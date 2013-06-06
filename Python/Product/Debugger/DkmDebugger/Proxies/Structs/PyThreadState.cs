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
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    internal class PyThreadState : StructProxy {
        private class Fields {
            public StructField<PointerProxy<PyThreadState>> next;
            public StructField<PointerProxy<PyFrameObject>> frame;
            public StructField<Int32Proxy> use_tracing;
            public StructField<PointerProxy> c_tracefunc;
            public StructField<PointerProxy<PyObject>> curexc_type;
            public StructField<PointerProxy<PyObject>> curexc_value;
            public StructField<PointerProxy<PyObject>> curexc_traceback;
            public StructField<PointerProxy<PyObject>> exc_type;
            public StructField<PointerProxy<PyObject>> exc_value;
            public StructField<PointerProxy<PyObject>> exc_traceback;
            public StructField<Int32Proxy> thread_id;
        }

        private readonly Fields _fields;

        public PyThreadState(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyThreadState> next {
            get { return GetFieldProxy(_fields.next); }
        }

        public PointerProxy<PyFrameObject> frame {
            get { return GetFieldProxy(_fields.frame); }
        }

        public Int32Proxy use_tracing {
            get { return GetFieldProxy(_fields.use_tracing); }
        }

        public PointerProxy c_tracefunc {
            get { return GetFieldProxy(_fields.c_tracefunc); }
        }

        public PointerProxy<PyObject> curexc_type {
            get { return GetFieldProxy(_fields.curexc_type); }
        }

        public PointerProxy<PyObject> curexc_value {
            get { return GetFieldProxy(_fields.curexc_value); }
        }

        public PointerProxy<PyObject> curexc_traceback {
            get { return GetFieldProxy(_fields.curexc_traceback); }
        }

        public PointerProxy<PyObject> exc_type {
            get { return GetFieldProxy(_fields.exc_type); }
        }

        public PointerProxy<PyObject> exc_value {
            get { return GetFieldProxy(_fields.exc_value); }
        }

        public PointerProxy<PyObject> exc_traceback {
            get { return GetFieldProxy(_fields.exc_traceback); }
        }

        public Int32Proxy thread_id {
            get { return GetFieldProxy(_fields.thread_id); }
        }

        public static IEnumerable<PyThreadState> GetThreadStates(DkmProcess process) {
            return PyInterpreterState.GetInterpreterStates(process).SelectMany(interp => interp.GetThreadStates());
        }
    }
}
