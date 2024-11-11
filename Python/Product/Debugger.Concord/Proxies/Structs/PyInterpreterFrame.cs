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

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    // This was added in commit https://github.com/python/cpython/commit/ae0a2b756255629140efcbe57fc2e714f0267aa3
    [StructProxy(StructName = "_PyInterpreterFrame", MinVersion = PythonLanguageVersion.V311)]
    internal class PyInterpreterFrame : StructProxy {
        internal class Fields {
            public StructField<PointerProxy<PyDictObject>> f_globals;
            public StructField<PointerProxy<PyDictObject>> f_builtins;
            public StructField<PointerProxy<PyDictObject>> f_locals;
            public StructField<PointerProxy<PyCodeObject>> f_code;
            public StructField<PointerProxy<PyFrameObject>> frame_obj;
            public StructField<PointerProxy<PyInterpreterFrame>> previous;
            public StructField<ArrayProxy<PointerProxy<PyObject>>> localsplus;
        }

        private readonly Fields _fields;

        public PyInterpreterFrame(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyCodeObject> f_code {
            get { return GetFieldProxy(_fields.f_code); }
        }

        public PointerProxy<PyDictObject> f_globals {
            get { return GetFieldProxy(_fields.f_globals); }
        }

        public PointerProxy<PyDictObject> f_locals {
            get { return GetFieldProxy(_fields.f_locals); }
        }

        public ArrayProxy<PointerProxy<PyObject>> f_localsplus {
            get { return GetFieldProxy(_fields.localsplus); }
        }

        public PointerProxy<PyFrameObject> frame_obj {
            get { return GetFieldProxy(_fields.frame_obj); }
        }

    }
}
