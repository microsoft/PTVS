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
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    internal class PySetObject : PyObject {
        public class setentry : StructProxy {
            private class Fields {
                public StructField<PointerProxy<PyObject>> key;
            }

            private readonly Fields _fields;

            public setentry(DkmProcess process, ulong address)
                : base(process, address) {
                InitializeStruct(this, out _fields);
            }

            public PointerProxy<PyObject> key {
                get { return GetFieldProxy(_fields.key); }
            }
        }

        private class DummyHolder : DkmDataItem {
            public readonly PointerProxy<PyObject> Dummy;

            public DummyHolder(DkmProcess process) {
                var pyrtInfo = process.GetPythonRuntimeInfo();
                Dummy =
                    pyrtInfo.LanguageVersion >= PythonLanguageVersion.V34 ?
                    pyrtInfo.DLLs.Python.GetStaticVariable<PointerProxy<PyObject>>("_PySet_Dummy") :
                    pyrtInfo.DLLs.Python.GetStaticVariable<PointerProxy<PyObject>>("dummy", "setobject.obj");
            }
        }

        private class Fields {
            public StructField<SSizeTProxy> mask;
            public StructField<PointerProxy<ArrayProxy<setentry>>> table;
        }

        private readonly Fields _fields;
        private readonly PyObject _dummy;

        public PySetObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
            CheckPyType<PySetObject>();

            _dummy = Process.GetOrCreateDataItem(() => new DummyHolder(Process)).Dummy.TryRead();
        }

        public SSizeTProxy mask {
            get { return GetFieldProxy(_fields.mask); }
        }

        public PointerProxy<ArrayProxy<setentry>> table {
            get { return GetFieldProxy(_fields.table); }
        }

        public IEnumerable<PyObject> ReadElements() {
            if (table.IsNull) {
                return Enumerable.Empty<PyObject>();
            }

            var count = mask.Read() + 1;
            var entries = table.Read().Take(count);
            var items = from entry in entries
                        let key = entry.key.TryRead()
                        where key != null && key != _dummy
                        select entry.key.Read();
            return items;
        }

        public override void Repr(ReprBuilder builder) {
            var count = ReadElements().Count();
            if (count == 0) {
                builder.Append(builder.Options.LanguageVersion >= PythonLanguageVersion.V30 ? "set()" : "set([])");
                return;
            }

            if (builder.IsTopLevel) {
                if (count > ReprBuilder.MaxJoinedItems) {
                    builder.AppendFormat("<set, len() = {0}>", count);
                    return;
                }
            }

            builder.Append(builder.Options.LanguageVersion >= PythonLanguageVersion.V30 ? "{" : "set([");
            builder.AppendJoined(", ", ReadElements(), obj => builder.AppendRepr(obj));
            builder.Append(builder.Options.LanguageVersion >= PythonLanguageVersion.V30 ? "}" : "])");
        }

        public override IEnumerable<PythonEvaluationResult> GetDebugChildren(ReprOptions reprOptions) {
            yield return new PythonEvaluationResult(new ValueStore<long>(ReadElements().Count()), "len()") {
                Category = DkmEvaluationResultCategory.Method
            };

            foreach (var item in ReadElements()) {
                yield return new PythonEvaluationResult(item);
            }
        }
    }
}
