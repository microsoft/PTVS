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
using System.Diagnostics;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    internal class PyFloatObject : PyObject {
        private class Fields {
            public StructField<DoubleProxy> ob_fval;
        }

        private readonly Fields _fields;

        public PyFloatObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyFloatObject>();
        }

        public static PyFloatObject Create(DkmProcess process, double value) {
            var allocator = process.GetDataItem<PyObjectAllocator>();
            Debug.Assert(allocator != null);

            var result = allocator.Allocate<PyFloatObject>();
            result.ob_fval.Write(value);
            return result;
        }

        private DoubleProxy ob_fval {
            get { return GetFieldProxy(_fields.ob_fval); }
        }

        public Double ToDouble() {
            return ob_fval.Read();
        }

        public override void Repr(ReprBuilder builder) {
            builder.AppendLiteral(ToDouble());
        }
    }
}
