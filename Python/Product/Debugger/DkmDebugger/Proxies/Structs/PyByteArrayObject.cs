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
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    internal class PyByteArrayObject : PyVarObject {
        public class Fields {
            // Not CStringProxy, because the array can contain embedded null chars.
            public StructField<PointerProxy<ArrayProxy<ByteProxy>>> ob_bytes;
        }

        private readonly Fields _fields;

        public PyByteArrayObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyByteArrayObject>();
        }

        public PointerProxy<ArrayProxy<ByteProxy>> ob_bytes {
            get { return GetFieldProxy(_fields.ob_bytes); }
        }

        public byte[] ToBytes() {
            var size = ob_size.Read();
            if (size == 0) {
                return new byte[0];
            }

            var buf = new byte[size];
            Process.ReadMemory(ob_bytes.Read().Address, DkmReadMemoryFlags.None, buf);
            return buf;
        }

        public override unsafe string ToString() {
            var bytes = ToBytes();
            fixed (byte* p = bytes) {
                return new string((sbyte*)p, 0, bytes.Length);
            }
        }

        protected override string Repr(System.Func<PyObject, string> repr) {
            var pyrtInfo = Process.GetPythonRuntimeInfo();
            var expr = new ConstantExpression(new AsciiString(ToBytes(), ToString()));
            return "bytearray(" + expr.GetConstantRepr(pyrtInfo.LanguageVersion) + ")";
        }

        public override IEnumerable<KeyValuePair<string, IValueStore>> GetDebugChildren() {
            return ob_bytes.Read().Take((int)ob_size.Read()).Select((b, i) => new KeyValuePair<string, IValueStore>("[" + i + "]", b));
        }
    }
}
