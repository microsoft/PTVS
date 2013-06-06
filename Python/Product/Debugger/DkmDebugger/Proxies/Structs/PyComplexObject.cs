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
using System.Numerics;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    internal class PyComplexObject : PyObject {
        public class Fields {
            public StructField<Py_complex> cval;
        }

        private readonly Fields _fields;

        public PyComplexObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyComplexObject>();
        }

        public static PyComplexObject Create(DkmProcess process, Complex value) {
            var allocator = process.GetDataItem<PyObjectAllocator>();
            Debug.Assert(allocator != null);

            var result = allocator.Allocate<PyComplexObject>();
            result.cval.real.Write(value.Real);
            result.cval.imag.Write(value.Imaginary);
            return result;
        }

        public Py_complex cval {
            get { return GetFieldProxy(_fields.cval); }
        }

        public Complex ToComplex() {
            return new Complex(cval.real.Read(), cval.imag.Read());
        }

        protected override string Repr(Func<PyObject, string> repr) {
            var pyrtInfo = Process.GetPythonRuntimeInfo();
            var expr = new ConstantExpression(ToComplex());
            return expr.GetConstantRepr(pyrtInfo.LanguageVersion);
        }
    }

    internal class Py_complex : StructProxy {
        public class Fields {
            public StructField<DoubleProxy> real;
            public StructField<DoubleProxy> imag;
        }

        private readonly Fields _fields;

        public Py_complex(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public DoubleProxy real {
            get { return GetFieldProxy(_fields.real); }
        }

        public DoubleProxy imag {
            get { return GetFieldProxy(_fields.imag); }
        }
    }
}
