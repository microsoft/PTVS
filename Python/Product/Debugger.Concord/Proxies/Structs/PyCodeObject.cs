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
    internal abstract class PyCodeObject : PyObject {
        public PyCodeObject(DkmProcess process, ulong address)
            : base(process, address) {
        }

        public abstract Int32Proxy co_nlocals { get; }

        public abstract PointerProxy<PyTupleObject> co_names { get; }

        public abstract IWritableDataProxy<PyTupleObject> co_varnames { get; }

        public abstract IWritableDataProxy<PyTupleObject> co_freevars { get; }

        public abstract IWritableDataProxy<PyTupleObject> co_cellvars { get; }

        public abstract PointerProxy<IPyBaseStringObject> co_filename { get; }

        public abstract PointerProxy<IPyBaseStringObject> co_name { get; }

        public abstract Int32Proxy co_firstlineno { get; }

        public override void Repr(ReprBuilder builder) {
            string name = co_name.TryRead().ToStringOrNull() ?? "???";

            string filename = co_filename.TryRead().ToStringOrNull();
            if (filename == null) {
                filename = "???";
            } else {
                filename = '"' + filename + '"';
            }

            int lineno = co_firstlineno.Read();
            if (lineno == 0) {
                lineno = -1;
            }

            builder.AppendFormat("<code object {0} at {1:PTR}, file {2}, line {3}>", name, Address, filename, lineno);
        }
    }
}
