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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    internal class PyListObject : PyVarObject {
        private class Fields {
            public StructField<PointerProxy<ArrayProxy<PointerProxy<PyObject>>>> ob_item;
        }

        private readonly Fields _fields;

        public PyListObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PyListObject>();
        }

        public PointerProxy<ArrayProxy<PointerProxy<PyObject>>> ob_item {
            get { return GetFieldProxy(_fields.ob_item); }
        }

        public IEnumerable<PointerProxy<PyObject>> ReadElements() {
            if (ob_item.IsNull) {
                return Enumerable.Empty<PointerProxy<PyObject>>();
            }

            return ob_item.Read().Take(ob_size.Read());
        }

        public override void Repr(ReprBuilder builder) {
            var count = ob_size.Read();
            if (count > ReprBuilder.MaxJoinedItems) {
                builder.AppendFormat("<list, len() = {0}>", count);
            } else {
                builder.Append("[");
                builder.AppendJoined(", ", ReadElements(), item => builder.AppendRepr(item.TryRead()));
                builder.Append("]");
            }
        }

        public override IEnumerable<PythonEvaluationResult> GetDebugChildren(ReprOptions reprOptions, DkmInspectionContext inspectionContext, DkmStackWalkFrame stackFrame) {
            yield return new PythonEvaluationResult(new ValueStore<long>(ob_size.Read()), "len()") {
                Category = DkmEvaluationResultCategory.Method
            };

            foreach (var item in ReadElements()) {
                yield return new PythonEvaluationResult(item);
            }
        }
    }
}
