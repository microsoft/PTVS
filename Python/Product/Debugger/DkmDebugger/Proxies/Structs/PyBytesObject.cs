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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    [StructProxy(MaxVersion = PythonLanguageVersion.V27, StructName = "PyStringObject")]
    [StructProxy(MinVersion = PythonLanguageVersion.V30)]
    [PyType(MaxVersion = PythonLanguageVersion.V27, VariableName = "PyString_Type")]
    [PyType(MinVersion = PythonLanguageVersion.V30)]
    internal class PyBytesObject : PyVarObject, IPyBaseStringObject {
        internal class Fields {
            // Not CStringProxy, because the array can contain embedded null chars.
            public StructField<ArrayProxy<ByteProxy>> ob_sval;
        }

        private readonly Fields _fields;

        public PyBytesObject(DkmProcess process, ulong address)
            : this(process, address, false) {
        }

        protected PyBytesObject(DkmProcess process, ulong address, bool checkType)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            if (checkType) {
                CheckPyType<PyBytesObject>();
            }
        }

        public static PyBytesObject Create(DkmProcess process, AsciiString value) {
            var allocator = process.GetDataItem<PyObjectAllocator>();
            Debug.Assert(allocator != null);

            var result = allocator.Allocate<PyBytesObject>(value.Bytes.Length);
            result.ob_size.Write(value.Bytes.Length);
            process.WriteMemory(result.ob_sval.Address, value.Bytes);

            return result;
        }

        public ArrayProxy<ByteProxy> ob_sval {
            get { return GetFieldProxy(_fields.ob_sval); }
        }

        public byte[] ToBytes() {
            var size = ob_size.Read();
            if (size == 0) {
                return new byte[0];
            }

            var buf = new byte[size];
            Process.ReadMemory(ob_sval.Address, DkmReadMemoryFlags.None, buf);
            return buf;
        }

        public override unsafe string ToString() {
            var bytes = ToBytes();
            fixed (byte* p = bytes) {
                return new string((sbyte*)p, 0, bytes.Length);
            }
        }

        public override void Repr(ReprBuilder builder) {
            builder.AppendLiteral(new AsciiString(ToBytes(), ToString()));
        }

        public override IEnumerable<PythonEvaluationResult> GetDebugChildren(ReprOptions reprOptions) {
            long count = ob_size.Read();
            yield return new PythonEvaluationResult(new ValueStore<long>(count), "len()") {
                Category = DkmEvaluationResultCategory.Method
            };

            foreach (var b in ob_sval.Take((int)count)) {
                yield return new PythonEvaluationResult(b);
            }
        }
    }
}
