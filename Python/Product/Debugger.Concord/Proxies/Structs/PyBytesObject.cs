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
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(MaxVersion = PythonLanguageVersion.V27, StructName = "PyStringObject")]
    [StructProxy(MinVersion = PythonLanguageVersion.V30)]
    [PyType(MaxVersion = PythonLanguageVersion.V27, VariableName = "PyString_Type")]
    [PyType(MinVersion = PythonLanguageVersion.V30)]
    internal class PyBytesObject : PyVarObject, IPyBaseStringObject {
        // Use Latin-1 here because it just zero-extends 8-bit characters to 16-bit, preserving their numerical values.
        // We display the higher 128 chars as \x## escape sequences anyway, so the actual glyphs don't matter.
        private static readonly Encoding _latin1 = Encoding.GetEncoding(28591);

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

            var result = allocator.Allocate<PyBytesObject>(value.Bytes.Count);
            result.ob_size.Write(value.Bytes.Count);
            process.WriteMemory(result.ob_sval.Address, value.Bytes.ToArray());

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
            return _latin1.GetString(ToBytes());
        }

        public override void Repr(ReprBuilder builder) {
            builder.AppendLiteral(new AsciiString(ToBytes(), ToString()));
        }

        public override IEnumerable<PythonEvaluationResult> GetDebugChildren(ReprOptions reprOptions) {
            long count = ob_size.Read();
            yield return new PythonEvaluationResult(new ValueStore<long>(count), "len()") {
                Category = DkmEvaluationResultCategory.Method
            };

            foreach (var b in ob_sval.Take(count)) {
                if (reprOptions.LanguageVersion <= PythonLanguageVersion.V27) {
                    // In 2.x, bytes is a string type, so display characters in object expansion.
                    byte[] bytes = new[] { b.Read() };
                    string s = _latin1.GetString(bytes);
                    yield return new PythonEvaluationResult(new ValueStore<AsciiString>(new AsciiString(bytes, s)));
                } else {
                    // In 3.x, it's supposed to be used for byte arrays only, so display numeric values in expansion.
                    yield return new PythonEvaluationResult(b);
                }
            }
        }
    }
}
