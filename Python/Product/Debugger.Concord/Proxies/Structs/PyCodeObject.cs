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

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs
{
    internal class PyCodeObject : PyObject
    {
        public class Fields
        {
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

        public PyCodeObject(DkmProcess process, ulong address)
            : base(process, address)
        {
            InitializeStruct(this, out _fields);
            CheckPyType<PyCodeObject>();
        }

        public Int32Proxy co_nlocals
        {
            get { return GetFieldProxy(_fields.co_nlocals); }
        }

        public PointerProxy<PyTupleObject> co_names
        {
            get { return GetFieldProxy(_fields.co_names); }
        }

        public PointerProxy<PyTupleObject> co_varnames
        {
            get { return GetFieldProxy(_fields.co_varnames); }
        }

        public PointerProxy<PyTupleObject> co_freevars
        {
            get { return GetFieldProxy(_fields.co_freevars); }
        }

        public PointerProxy<PyTupleObject> co_cellvars
        {
            get { return GetFieldProxy(_fields.co_cellvars); }
        }

        public PointerProxy<IPyBaseStringObject> co_filename
        {
            get { return GetFieldProxy(_fields.co_filename); }
        }

        public PointerProxy<IPyBaseStringObject> co_name
        {
            get { return GetFieldProxy(_fields.co_name); }
        }

        public Int32Proxy co_firstlineno
        {
            get { return GetFieldProxy(_fields.co_firstlineno); }
        }

        public override void Repr(ReprBuilder builder)
        {
            string name = co_name.TryRead().ToStringOrNull() ?? "???";

            string filename = co_filename.TryRead().ToStringOrNull();
            if (filename == null)
            {
                filename = "???";
            }
            else
            {
                filename = '"' + filename + '"';
            }

            int lineno = co_firstlineno.Read();
            if (lineno == 0)
            {
                lineno = -1;
            }

            builder.AppendFormat("<code object {0} at {1:PTR}, file {2}, line {3}>", name, Address, filename, lineno);
        }
    }
}
