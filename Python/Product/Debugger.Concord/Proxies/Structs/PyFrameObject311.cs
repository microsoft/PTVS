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

using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "_frame", MinVersion = PythonLanguageVersion.V311)]
    [PyType(MinVersion = PythonLanguageVersion.V311, VariableName = "PyFrame_Type")]
    internal class PyFrameObject311 : PyFrameObject {
        internal class Fields {
            public StructField<PointerProxy<PyFrameObject>> f_back;
            public StructField<PointerProxy<PyInterpreterFrame>> f_frame;
            public StructField<PointerProxy<PyObject>> f_trace;
            public StructField<Int32Proxy> f_lineno;
        }

        private readonly Fields _fields;

        public PyFrameObject311(DkmProcess process, ulong address)
            : base(process, address) {
            var pythonInfo = process.GetPythonRuntimeInfo();
            InitializeStruct(this, out _fields);
            CheckPyType<PyFrameObject311>();
        }

        public PointerProxy<PyInterpreterFrame> f_frame {
            get { return GetFieldProxy(_fields.f_frame); }
        }

        private PyInterpreterFrame GetFrame() {
            return f_frame.TryRead();
        }

        public override PointerProxy<PyFrameObject> f_back {
            get {
                // See the code here https://github.com/python/cpython/blob/a0866f4c81ecc057d4521e8e7a02f4e1fff175a1/Objects/frameobject.c#L1491,
                // f_back can be null when there is actually a frame because it's created lazily.
                var back = GetFieldProxy(_fields.f_back);
                if (back.IsNull) {
                    back = GetFrame().FindBackFrame();
                }
                return back;
            }
        }

        public override PointerProxy<PyCodeObject> f_code => GetFrame().f_code;

        public override PointerProxy<PyDictObject> f_globals => GetFrame().f_globals;

        public override PointerProxy<PyDictObject> f_locals => GetFrame().f_locals;

        public override ArrayProxy<PointerProxy<PyObject>> f_localsplus => GetFrame().f_localsplus;
        public override Int32Proxy f_lineno => GetFieldProxy(_fields.f_lineno);

    }
}
