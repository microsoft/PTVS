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
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.DkmDebugger.Proxies.Structs {
    internal abstract class PyByteArrayObject : PyVarObject {
        public class Fields {
            // Not CStringProxy, because the array can contain embedded null chars.
            public StructField<PointerProxy<ArrayProxy<ByteProxy>>> ob_bytes;
        }

        // Use Latin-1 here because it just zero-extends 8-bit characters to 16-bit, preserving their numerical values.
        // We display the higher 128 chars as \x## escape sequences anyway, so the actual glyphs don't matter.
        private static readonly Encoding _latin1 = Encoding.GetEncoding(28591);

        private readonly Fields _fields;

        public PyByteArrayObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<ArrayProxy<ByteProxy>> ob_bytes {
            get { return GetFieldProxy(_fields.ob_bytes); }
        }

        protected abstract ArrayProxy<ByteProxy> GetDataProxy();

        public byte[] ToBytes() {
            var size = ob_size.Read();
            if (size == 0) {
                return new byte[0];
            }

            var buf = new byte[size];
            Process.ReadMemory(GetDataProxy().Address, DkmReadMemoryFlags.None, buf);
            return buf;
        }

        public override unsafe string ToString() {
            return _latin1.GetString(ToBytes());
        }

        public override void Repr(ReprBuilder builder) {
            // In Python 2.7, string literals in bytearray repr use the 3.x-style prefixed b'...' form.
            var langVer =
                builder.Options.LanguageVersion <= PythonLanguageVersion.V27 ?
                PythonLanguageVersion.V33 :
                builder.Options.LanguageVersion;
            var constExpr = new ConstantExpression(new AsciiString(ToBytes(), ToString()));
            builder.Append("bytearray(");
            builder.Append(constExpr.GetConstantRepr(langVer, escape8bitStrings: true));
            builder.Append(")");
        }

        public override IEnumerable<PythonEvaluationResult> GetDebugChildren(ReprOptions reprOptions) {
            long count = ob_size.Read();
            yield return new PythonEvaluationResult(new ValueStore<long>(count), "len()") {
                Category = DkmEvaluationResultCategory.Method
            };

            if (count > 0) {
                foreach (var b in GetDataProxy().Take((int)count)) {
                    yield return new PythonEvaluationResult(b);
                }
            }
        }
    }

    [StructProxy(MaxVersion = PythonLanguageVersion.V33, StructName = "PyByteArrayObject")]
    [PyType(MaxVersion = PythonLanguageVersion.V33, VariableName = "PyByteArray_Type")]
    internal class PyByteArrayObject33 : PyByteArrayObject {
        public PyByteArrayObject33(DkmProcess process, ulong address)
            : base(process, address) {
            CheckPyType<PyByteArrayObject33>();
        }

        protected override ArrayProxy<ByteProxy> GetDataProxy() {
            return ob_bytes.Read();
        }
    }

    [StructProxy(MinVersion = PythonLanguageVersion.V34, StructName = "PyByteArrayObject")]
    [PyType(MinVersion = PythonLanguageVersion.V34, VariableName = "PyByteArray_Type")]
    internal class PyByteArrayObject34 : PyByteArrayObject {
        private new class Fields {
            public StructField<PointerProxy<ArrayProxy<ByteProxy>>> ob_start;
        }

        private readonly Fields _fields;

        public PyByteArrayObject34(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyByteArrayObject34>();
        }

        public PointerProxy<ArrayProxy<ByteProxy>> ob_start {
            get { return GetFieldProxy(_fields.ob_start); }
        }

        protected override ArrayProxy<ByteProxy> GetDataProxy() {
            return ob_start.Read();
        }
    }
}
