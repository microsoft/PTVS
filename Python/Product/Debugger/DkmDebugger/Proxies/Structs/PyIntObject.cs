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
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    [StructProxy(MaxVersion = PythonLanguageVersion.V27)]
    [PyType(MaxVersion = PythonLanguageVersion.V27)]
    internal class PyIntObject : PyObject {
        private class Fields {
            public StructField<Int32Proxy> ob_ival;
        }

        private readonly Fields _fields;

        public PyIntObject(DkmProcess process, ulong address)
            : this(process, address, true) {
        }

        protected PyIntObject(DkmProcess process, ulong address, bool checkType)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            if (checkType) {
                CheckPyType<PyIntObject>();
            }
        }

        public static PyIntObject Create(DkmProcess process, int value) {
            var allocator = process.GetDataItem<PyObjectAllocator>();
            Debug.Assert(allocator != null);

            var result = allocator.Allocate<PyIntObject>();
            result.ob_ival.Write(value);
            return result;
        }

        private Int32Proxy ob_ival {
            get { return GetFieldProxy(_fields.ob_ival); }
        }

        public Int32 ToInt32() {
            return ob_ival.Read();
        }

        public override void Repr(ReprBuilder builder) {
            builder.Append(ToInt32());
        }
    }
}
