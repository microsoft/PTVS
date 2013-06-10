/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
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
