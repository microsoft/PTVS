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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Common.Parsing.Ast;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(MinVersion = PythonLanguageVersion.V39, StructName = "PyByteArrayObject")]
    [PyType(MinVersion = PythonLanguageVersion.V39, VariableName = "PyByteArray_Type")]
    internal class PyByteArrayObject : PyVarObject {
        public class Fields {
            public StructField<PointerProxy<ArrayProxy<ByteProxy>>> ob_start;
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

        public PointerProxy<ArrayProxy<ByteProxy>> ob_start {
            get { return GetFieldProxy(_fields.ob_start); }
        }

        protected ArrayProxy<ByteProxy> GetDataProxy() {
            return ob_start.Read();
        }

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
            var langVer = builder.Options.LanguageVersion;
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
                foreach (var b in GetDataProxy().Take(count)) {
                    yield return new PythonEvaluationResult(b);
                }
            }
        }
    }
}
