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

using Microsoft.VisualStudio.Debugger;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "_frame", MaxVersion = PythonLanguageVersion.V310)]
    [PyType(MaxVersion = PythonLanguageVersion.V310, VariableName = "PyFrame_Type")]
    internal class PyFrameObject310 : PyFrameObject {
        internal class Fields {
            public StructField<PointerProxy<PyFrameObject>> f_back;
            public StructField<PointerProxy<PyCodeObject>> f_code;
            public StructField<PointerProxy<PyDictObject>> f_globals;
            public StructField<PointerProxy<PyDictObject>> f_locals;
            public StructField<PointerProxy<PyObject>> f_trace;
            public StructField<Int32Proxy> f_lineno;
            public StructField<ArrayProxy<PointerProxy<PyObject>>> f_localsplus;
        }

        private readonly Fields _fields;

        public override PointerProxy<PyFrameObject> f_back => GetFieldProxy(_fields.f_back);

        public override PointerProxy<PyCodeObject> f_code => GetFieldProxy(_fields.f_code);

        public override PointerProxy<PyDictObject> f_globals => GetFieldProxy(_fields.f_globals);

        public override PointerProxy<PyDictObject> f_locals => GetFieldProxy(_fields.f_locals);

        public override ArrayProxy<PointerProxy<PyObject>> f_localsplus => GetFieldProxy(_fields.f_localsplus);

        public override int ComputeLineNumber(DkmInspectionSession inspectionSession, DkmStackWalkFrame frame) => GetFieldProxy(_fields.f_lineno).Read();

        public PyFrameObject310(DkmProcess process, ulong address)
            : base(process, address) {
            var pythonInfo = process.GetPythonRuntimeInfo();
            InitializeStruct(this, out _fields);
            CheckPyType<PyFrameObject310>();
        }
    }
}
