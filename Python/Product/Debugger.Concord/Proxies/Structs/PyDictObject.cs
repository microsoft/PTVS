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
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    internal abstract class PyDictObject : PyObject {
        protected PyDictObject(DkmProcess process, ulong address)
            : base(process, address) {
        }

        public abstract IEnumerable<KeyValuePair<PyObject, PointerProxy<PyObject>>> ReadElements();

        public override void Repr(ReprBuilder builder) {
            var count = ReadElements().Count();
            if (count > ReprBuilder.MaxJoinedItems) {
                builder.AppendFormat("<dict, len() = {0}>", count);
                return;
            }

            builder.Append("{");
            builder.AppendJoined(", ", ReadElements(), entry => {
                builder.AppendRepr(entry.Key);
                builder.Append(": ");
                builder.AppendRepr(entry.Value.TryRead());
            });
            builder.Append("}");
        }

        public override IEnumerable<PythonEvaluationResult> GetDebugChildren(ReprOptions reprOptions) {
            yield return new PythonEvaluationResult(new ValueStore<long>(ReadElements().Count()), "len()") {
                Category = DkmEvaluationResultCategory.Method
            };

            var reprBuilder = new ReprBuilder(reprOptions);
            foreach (var entry in ReadElements()) {
                reprBuilder.Clear();
                reprBuilder.AppendFormat("[{0}]", entry.Key);
                yield return new PythonEvaluationResult(entry.Value, reprBuilder.ToString());
            }
        }
    }

    [StructProxy(MaxVersion = PythonLanguageVersion.V27, StructName = "PyDictEntry")]
    [StructProxy(MinVersion = PythonLanguageVersion.V33)]
    internal class PyDictKeyEntry : StructProxy {
        private class Fields {
            public StructField<PointerProxy<PyObject>> me_key;
            public StructField<PointerProxy<PyObject>> me_value;
        }

        private readonly Fields _fields;

        public PyDictKeyEntry(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public PointerProxy<PyObject> me_key {
            get { return GetFieldProxy(_fields.me_key); }
        }

        public PointerProxy<PyObject> me_value {
            get { return GetFieldProxy(_fields.me_value); }
        }
    }
}
