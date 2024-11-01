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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    /// <summary>
    /// In Python 3.7, many global variables were combined into the _PyRuntime struct.
    /// </summary>
    [StructProxy(MinVersion = PythonLanguageVersion.V39, StructName = "_PyRuntimeState")]
    internal class PyRuntimeState : StructProxy {
        private class Fields {
            public StructField<BoolProxy> core_initialized;
            public StructField<BoolProxy> initialized;
            public StructField<pyinterpreters> interpreters;
        }

        private readonly Fields _fields;

        public PyRuntimeState(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public BoolProxy core_initialized => GetFieldProxy(_fields.core_initialized);
        public BoolProxy initialized => GetFieldProxy(_fields.initialized);
        public pyinterpreters interpreters => GetFieldProxy(_fields.interpreters);


        [StructProxy(MinVersion = PythonLanguageVersion.V39, StructName = "pyinterpreters")]
        public class pyinterpreters : StructProxy {
            private class Fields {
                public StructField<PointerProxy<PyInterpreterState>> head;
                public StructField<PointerProxy<PyInterpreterState>> main;
            }

            private readonly Fields _fields;

            public pyinterpreters(DkmProcess process, ulong address)
                : base(process, address) {
                InitializeStruct(this, out _fields);
            }

            public PointerProxy<PyInterpreterState> head => GetFieldProxy(_fields.head);
            public PointerProxy<PyInterpreterState> main => GetFieldProxy(_fields.main);
        }


    }

}
