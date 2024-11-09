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
    [StructProxy(StructName = "_cframe", MinVersion = PythonLanguageVersion.V310, MaxVersion = PythonLanguageVersion.V310)]
    [StructProxy(StructName = "_PyCFrame", MinVersion = PythonLanguageVersion.V311)]
    internal class CFrameProxy : StructProxy {
        internal class Fields {
            public StructField<Int32Proxy> use_tracing;
            public StructField<PointerProxy<CFrameProxy>> previous;
            [FieldProxy(MinVersion = PythonLanguageVersion.V311)]
            public StructField<PointerProxy<PyInterpreterFrame>> current_frame;
        }

        private readonly Fields _fields;

        public CFrameProxy(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public Int32Proxy use_tracing {
            get { return GetFieldProxy(_fields.use_tracing); }
        }

        public PointerProxy<PyInterpreterFrame> current_frame => GetFieldProxy(_fields.current_frame);
    }
}
