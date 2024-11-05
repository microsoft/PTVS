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

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "_ts")]
    internal class PyThreadState : StructProxy {
        private class Fields {
            public StructField<PointerProxy<PyThreadState>> next;
            [FieldProxy(MaxVersion = PythonLanguageVersion.V310)]
            public StructField<PointerProxy<PyFrameObject>> frame;
            [FieldProxy(MinVersion = PythonLanguageVersion.V311)]
            public StructField<PointerProxy<PyInterpreterState>> interp;
            [FieldProxy(MaxVersion = PythonLanguageVersion.V39)]
            public StructField<Int32Proxy> use_tracing;
            [FieldProxy(MinVersion = PythonLanguageVersion.V310)]
            public StructField<PointerProxy<CFrameProxy>> cframe;
            public StructField<PointerProxy> c_tracefunc;
            public StructField<PointerProxy<PyObject>> curexc_type;
            public StructField<PointerProxy<PyObject>> curexc_value;
            public StructField<PointerProxy<PyObject>> curexc_traceback;
            public StructField<PyErr_StackItem> exc_state;
            public StructField<PointerProxy<PyErr_StackItem>> exc_info;
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
            get {
                if (_fields.frame.Process != null) {
                    return GetFieldProxy(_fields.frame);
                }

                // In 3.11, the current frame was moved into the cframe
                var cframe = GetFieldProxy(_fields.cframe).Read();
                var interpFrame = cframe.current_frame.TryRead();
                return interpFrame.frame_obj;
            }
        }

        public Int32Proxy use_tracing {
            get { return GetFieldProxy(_fields.use_tracing); }
        }

        public PointerProxy<CFrameProxy> cframe {
            get { return GetFieldProxy(_fields.cframe); }
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

        public PointerProxy<PyObject> exc_type(PythonLanguageVersion version) => 
            GetFieldProxy(_fields.exc_state).exc_type;

        public PointerProxy<PyObject> exc_value(PythonLanguageVersion version) => 
            GetFieldProxy(_fields.exc_state).exc_value;

        public PointerProxy<PyObject> exc_traceback(PythonLanguageVersion version) => 
            GetFieldProxy(_fields.exc_state).exc_traceback;


        public Int32Proxy thread_id {
            get { return GetFieldProxy(_fields.thread_id); }
        }

        public static IEnumerable<PyThreadState> GetThreadStates(DkmProcess process) {
            return PyInterpreterState.GetInterpreterStates(process).SelectMany(interp => interp.GetThreadStates(process));
        }

        public void RegisterTracing(ulong traceFunc) {
            if (_fields.use_tracing.Process != null) {
                use_tracing.Write(1);
            }
            if (_fields.cframe.Process != null) {
                var frame = cframe.Read();
                frame.use_tracing.Write(255); // In 3.11 this flag has to be zero or 255
            }
            c_tracefunc.Write(traceFunc);
        }

        
    }

    [StructProxy(MinVersion = PythonLanguageVersion.V39, StructName = "_PyErr_StackItem")]
    class PyErr_StackItem : StructProxy {
        private class Fields {
            public StructField<PointerProxy<PyObject>> exc_type, exc_value, exc_traceback;
        }

        private readonly Fields _fields;

        public PyErr_StackItem(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyObject> exc_type => GetFieldProxy(_fields.exc_type);
        public PointerProxy<PyObject> exc_value => GetFieldProxy(_fields.exc_value);
        public PointerProxy<PyObject> exc_traceback => GetFieldProxy(_fields.exc_traceback);
    }

}
