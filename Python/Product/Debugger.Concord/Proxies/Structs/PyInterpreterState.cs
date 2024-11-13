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
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [StructProxy(StructName = "_is")]
    internal class PyInterpreterState : StructProxy {
        private class Fields {
            public StructField<PointerProxy<PyInterpreterState>> next;
            [FieldProxy(MaxVersion = PythonLanguageVersion.V310)]
            public StructField<PointerProxy<PyThreadState>> tstate_head;
            [FieldProxy(MinVersion = PythonLanguageVersion.V311)]
            public StructField<PyThreads> threads;
            [FieldProxy(MaxVersion = PythonLanguageVersion.V311)]
            public StructField<PointerProxy<PyDictObject>> modules;
            [FieldProxy(MinVersion = PythonLanguageVersion.V312)]
            public StructField<ImportState> imports;
            public StructField<PointerProxy> eval_frame;
            public StructField<ceval_state> ceval;
        }

        private readonly Fields _fields;

        public PyInterpreterState(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public static PyInterpreterState TryCreate(DkmProcess process, ulong address) {
            if (address == 0) {
                return null;
            }

            return new PyInterpreterState(process, address);
        }

        public PointerProxy<PyInterpreterState> next {
            get { return GetFieldProxy(_fields.next); }
        }

        public PointerProxy<PyThreadState> tstate_head {
            get { 
                if (_fields.tstate_head.Process != null) {
                    return GetFieldProxy(_fields.tstate_head);
                }
                var threads = GetFieldProxy(_fields.threads);
                return threads.head;
            }
        }

        public PointerProxy<PyDictObject> modules {
            get {
                if (_fields.modules.Process != null) {
                    return GetFieldProxy(_fields.modules);
                }
                var imports = GetFieldProxy(_fields.imports);
                return imports.modules;
            }
        }

        public PointerProxy eval_frame {
            get { return GetFieldProxy(_fields.eval_frame); }
        }

        public ceval_state ceval => GetFieldProxy(_fields.ceval);


        public static IEnumerable<PyInterpreterState> GetInterpreterStates(DkmProcess process) {
            var pyrtInfo = process.GetPythonRuntimeInfo();
            var runtimeState = pyrtInfo.GetRuntimeState();
            var interpreters = runtimeState.interpreters;
            var head = interpreters.head.TryRead();
            while (head != null) {
                yield return head;
                head = head.next.TryRead();
            }
        }

        public IEnumerable<PyThreadState> GetThreadStates(DkmProcess process) {
            var pyrtInfo = process.GetPythonRuntimeInfo();
            if (pyrtInfo.LanguageVersion <= PythonLanguageVersion.V310) {
                for (var tstate = tstate_head.TryRead(); tstate != null; tstate = tstate.next.TryRead()) {
                    yield return tstate;
                }
            } else {
                var threads = GetFieldProxy(_fields.threads);
                var head = threads.head.TryRead();
                while (head != null && head.Address != 0) {
                    yield return head;
                    head = head.next.TryRead();
                }
            }
        }

        [StructProxy(MinVersion = PythonLanguageVersion.V39, StructName = "_ceval_state")]
        public class ceval_state : StructProxy {
            private class Fields {
                public StructField<Int32Proxy> recursion_limit;
                [FieldProxy(MaxVersion = PythonLanguageVersion.V39)]
                public StructField<Int32Proxy> tracing_possible;
            }

            private readonly Fields _fields;

            public ceval_state(DkmProcess process, ulong address)
                : base(process, address) {
                InitializeStruct(this, out _fields);
            }

            public Int32Proxy recursion_limit => GetFieldProxy(_fields.recursion_limit);
            public Int32Proxy tracing_possible => GetFieldProxy(_fields.tracing_possible);
        }
    }
}
