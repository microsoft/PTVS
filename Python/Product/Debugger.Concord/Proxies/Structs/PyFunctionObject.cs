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
    [StructProxy(StructName = "PyFunctionObject", MinVersion = PythonLanguageVersion.V310)]
    internal class PyFunctionObject : PyObject {
        internal class Fields {
            public StructField<PointerProxy<PyObject>> func_globals;
            public StructField<PointerProxy<PyObject>> func_builtins;
            public StructField<PointerProxy<PyObject>> func_name;
            public StructField<PointerProxy<PyObject>> func_qualname;
            public StructField<PointerProxy<PyObject>> func_code;
            public StructField<PointerProxy<PyObject>> func_defaults;
            public StructField<PointerProxy<PyObject>> func_kwdefaults;
            public StructField<PointerProxy<PyObject>> func_closure;
            public StructField<PointerProxy<PyObject>> func_doc;
            public StructField<PointerProxy<PyObject>> func_dict;
            public StructField<PointerProxy<PyObject>> func_weakreflist;
            public StructField<PointerProxy<PyObject>> func_module;
            public StructField<PointerProxy<PyObject>> func_annotations;
            public StructField<PointerProxy<UInt64Proxy>> vectorcall;
            [FieldProxy(MinVersion = PythonLanguageVersion.V311)]
            public StructField<UInt32Proxy> func_version;
        }

        private readonly Fields _fields;

        public PyFunctionObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyFunctionObject>();
        }
    }
}
