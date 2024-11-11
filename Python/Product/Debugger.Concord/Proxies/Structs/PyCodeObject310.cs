﻿// Python Tools for Visual Studio
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
    [StructProxy(StructName = "PyCodeObject", MaxVersion = PythonLanguageVersion.V310)]
    [PyType(MaxVersion = PythonLanguageVersion.V310, VariableName = "PyCode_Type")]
    internal class PyCodeObject310 : PyCodeObject {
        public class Fields {
            public StructField<Int32Proxy> co_nlocals;
            public StructField<PointerProxy<PyTupleObject>> co_names;
            public StructField<PointerProxy<PyTupleObject>> co_varnames;
            public StructField<PointerProxy<PyTupleObject>> co_freevars;
            public StructField<PointerProxy<PyTupleObject>> co_cellvars;
            public StructField<PointerProxy<IPyBaseStringObject>> co_filename;
            public StructField<PointerProxy<IPyBaseStringObject>> co_name;
            public StructField<Int32Proxy> co_firstlineno;
        }

        private readonly Fields _fields;

        public override Int32Proxy co_nlocals => GetFieldProxy(_fields.co_nlocals);

        public override PointerProxy<PyTupleObject> co_names => GetFieldProxy(_fields.co_names);

        public override IWritableDataProxy<PyTupleObject> co_varnames => GetFieldProxy(_fields.co_varnames);

        public override IWritableDataProxy<PyTupleObject> co_freevars => GetFieldProxy(_fields.co_freevars);

        public override IWritableDataProxy<PyTupleObject> co_cellvars => GetFieldProxy(_fields.co_cellvars);

        public override PointerProxy<IPyBaseStringObject> co_filename => GetFieldProxy(_fields.co_filename);

        public override PointerProxy<IPyBaseStringObject> co_name => GetFieldProxy(_fields.co_name);

        public override Int32Proxy co_firstlineno => GetFieldProxy(_fields.co_firstlineno);

        public PyCodeObject310(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyCodeObject310>();
        }

    }
}
