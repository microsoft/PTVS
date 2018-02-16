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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
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

        public static PyThreadState TryCreate(DkmProcess process, ulong address) {
            if (address == 0) {
                return null;
            }
            return new PyThreadState(process, address);
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
